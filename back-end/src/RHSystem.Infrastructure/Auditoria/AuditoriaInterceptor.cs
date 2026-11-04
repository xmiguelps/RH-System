using System.Collections.Concurrent;
using System.Globalization;
using System.Reflection;
using System.Runtime.CompilerServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.EntityFrameworkCore.Storage;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Auditoria;

/// <summary>
/// Registra sozinho o que muda nas entidades <c>[Auditado]</c>, só nas propriedades <c>[Auditar]</c>: criar (<c>CRIOU</c>), alterar (<c>ALTEROU</c>, ou
/// <c>INATIVOU</c> se <c>Ativo</c> passou de verdadeiro para falso) e excluir (<c>EXCLUIU</c>). O evento sai NA MESMA transação da alteração (abre uma se o
/// chamador não tem): como o id de uma entidade nova só existe depois do <c>INSERT</c>, o evento dela é gravado no <c>SavedChanges</c>, e se gravá-lo falhar
/// a alteração desfaz junto. Requisição sem login não registra; segundo plano registra como Sistema. NÃO vê efeito de trigger nem operação em lote
/// (<c>ExecuteUpdate</c>, <c>ExecuteSql</c>): esses pedem <c>IAuditor.Registrar</c> explícito. O <c>Quando</c> vem do <see cref="IRelogio"/>.
/// </summary>
public sealed class AuditoriaInterceptor(IAtorAtual ator, IRelogio relogio, ContextoDeAuditoria contexto) : SaveChangesInterceptor
{
    private readonly ConditionalWeakTable<DbContext, Salvamento> _salvamentos = [];

    private sealed class Salvamento
    {
        public List<Item> Itens { get; } = [];

        public IDbContextTransaction? TransacaoPropria { get; set; }
    }

    private sealed record Item(object Entidade, Type Tipo, EntityState Estado, string Tabela, PropertyInfo Chave, List<Campo> Campos);

    private sealed record Campo(PropertyInfo Propriedade, AuditarAttribute Marca, object? Antes, string? TextoAntes, Type? PrincipalAuditavel);

    public override async ValueTask<InterceptionResult<int>> SavingChangesAsync(DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context;
        if (db is null || contexto.EmSalvamentoDeAuditoria || contexto.Suspenso)
        {
            return result;
        }

        // O EF chama o MESMO interceptor duas vezes por SaveChanges se ele estiver duas vezes nas opções (já aconteceu: ver DependencyInjection). A segunda
        // chamada não pode apagar o estado da primeira: era isso que deixava a transação própria aberta, sem confirmar, e perdia os dados em silêncio.
        if (_salvamentos.TryGetValue(db, out _))
        {
            return result;
        }

        // Detecta à mão, por garantia. Medido: tirar esta linha não derruba nenhum teste (o controle de mutação passou despercebido), o que combina com o
        // ChangeTracker.Entries() logo abaixo já detectar as mudanças com as opções padrão do EF (suponho, pela documentação do EF; não medi). Inofensiva.
        db.ChangeTracker.DetectChanges();
        var entradas = db.ChangeTracker.Entries()
            .Where(e => e.State is EntityState.Added or EntityState.Modified or EntityState.Deleted && MarcasDeAuditoria.Auditado(e.Entity.GetType()) is not null)
            .ToList();
        if (entradas.Count == 0 || ator.Tipo == TipoDeAtorAtual.Anonimo)
        {
            return result;
        }

        var salvamento = new Salvamento();
        foreach (var entrada in entradas)
        {
            var item = await CapturarAsync(db, entrada, cancellationToken);
            if (item is not null)
            {
                salvamento.Itens.Add(item);
            }
        }

        if (salvamento.Itens.Count == 0)
        {
            return result;
        }

        if (db.Database.CurrentTransaction is null)
        {
            salvamento.TransacaoPropria = await db.Database.BeginTransactionAsync(cancellationToken);
        }

        _salvamentos.AddOrUpdate(db, salvamento);
        return result;
    }

    public override async ValueTask<int> SavedChangesAsync(SaveChangesCompletedEventData eventData, int result, CancellationToken cancellationToken = default)
    {
        var db = eventData.Context;
        if (db is null || !_salvamentos.TryGetValue(db, out var salvamento))
        {
            return result;
        }

        _salvamentos.Remove(db);
        try
        {
            contexto.EmSalvamentoDeAuditoria = true;
            var dados = DadosDoAtor.De(ator);
            foreach (var item in salvamento.Itens)
            {
                db.Set<EventoDeAuditoria>().Add(ConstrutorDeEvento.Criar(relogio.AgoraUtc, dados, await MontarAsync(db, item, cancellationToken)));
            }

            await db.SaveChangesAsync(cancellationToken);
            if (salvamento.TransacaoPropria is not null)
            {
                await salvamento.TransacaoPropria.CommitAsync(cancellationToken);
            }
        }
        catch
        {
            if (salvamento.TransacaoPropria is not null)
            {
                await salvamento.TransacaoPropria.RollbackAsync(CancellationToken.None);
            }

            throw;
        }
        finally
        {
            contexto.EmSalvamentoDeAuditoria = false;
            if (salvamento.TransacaoPropria is not null)
            {
                await salvamento.TransacaoPropria.DisposeAsync();
            }
        }

        return result;
    }

    public override async Task SaveChangesFailedAsync(DbContextErrorEventData eventData, CancellationToken cancellationToken = default) =>
        await DescartarAsync(eventData.Context);

    public override async Task SaveChangesCanceledAsync(DbContextEventData eventData, CancellationToken cancellationToken = default) =>
        await DescartarAsync(eventData.Context);

    // As versões síncronas valem o mesmo: sem elas, um SaveChanges() síncrono passaria sem auditar, em silêncio.
    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result) =>
        SavingChangesAsync(eventData, result).AsTask().GetAwaiter().GetResult();

    public override int SavedChanges(SaveChangesCompletedEventData eventData, int result) =>
        SavedChangesAsync(eventData, result).AsTask().GetAwaiter().GetResult();

    public override void SaveChangesFailed(DbContextErrorEventData eventData) =>
        DescartarAsync(eventData.Context).GetAwaiter().GetResult();

    public override void SaveChangesCanceled(DbContextEventData eventData) =>
        DescartarAsync(eventData.Context).GetAwaiter().GetResult();

    private async Task DescartarAsync(DbContext? db)
    {
        if (db is null || !_salvamentos.TryGetValue(db, out var salvamento))
        {
            return;
        }

        _salvamentos.Remove(db);
        if (salvamento.TransacaoPropria is not null)
        {
            await salvamento.TransacaoPropria.RollbackAsync(CancellationToken.None);
            await salvamento.TransacaoPropria.DisposeAsync();
        }
    }

    /// <summary>Anota o que mudou antes do <c>INSERT/UPDATE/DELETE</c>: os valores de antes (e o nome da entidade apontada por uma chave estrangeira, que depois pode não existir mais).</summary>
    private static async Task<Item?> CapturarAsync(DbContext db, EntityEntry entrada, CancellationToken ct)
    {
        var tipo = entrada.Entity.GetType();
        var campos = new List<Campo>();
        foreach (var (propriedade, marca) in MarcasDeAuditoria.Campos(tipo))
        {
            var antes = entrada.State == EntityState.Added ? null : entrada.OriginalValues[propriedade.Name];
            var depois = entrada.State == EntityState.Deleted ? null : entrada.CurrentValues[propriedade.Name];
            var mudou = entrada.State switch
            {
                EntityState.Added => depois is not null,
                EntityState.Deleted => antes is not null,
                _ => !Equals(antes, depois),
            };
            if (!mudou)
            {
                continue;
            }

            var principal = PrincipalAuditavelDe(entrada, propriedade.Name);
            var textoAntes = marca.Sensivel ? null : await TextoAsync(db, principal, antes, ct);
            campos.Add(new Campo(propriedade, marca, antes, textoAntes, principal));
        }

        if (entrada.State == EntityState.Modified && campos.Count == 0)
        {
            return null; // só propriedade que não é marcada: não é evento
        }

        var chave = entrada.Metadata.FindPrimaryKey()!.Properties[0].Name;
        return new Item(entrada.Entity, tipo, entrada.State, entrada.Metadata.GetTableName()!, tipo.GetProperty(chave)!, campos);
    }

    /// <summary>Monta o evento depois do salvamento (o id de uma entidade nova já existe e o nome da entidade apontada já pode ser lido).</summary>
    private static async Task<EventoDeAuditoriaNovo> MontarAsync(DbContext db, Item item, CancellationToken ct)
    {
        var entidade = (IAuditavel)item.Entidade;
        var rotulo = MarcasDeAuditoria.Auditado(item.Tipo)!;
        var nome = entidade.NomeParaAuditoria;

        var mudancas = new List<MudancaDeAuditoriaNova>();
        foreach (var campo in item.Campos)
        {
            if (campo.Marca.Sensivel)
            {
                mudancas.Add(new MudancaDeAuditoriaNova(campo.Marca.Campo, null, null, ValorOculto: true));
                continue;
            }

            var depois = item.Estado == EntityState.Deleted ? null : await TextoAsync(db, campo.PrincipalAuditavel, campo.Propriedade.GetValue(item.Entidade), ct);
            mudancas.Add(new MudancaDeAuditoriaNova(campo.Marca.Campo, campo.TextoAntes, depois));
        }

        var (acao, descricao) = item.Estado switch
        {
            EntityState.Added => (AuditoriaAcao.Criou, $"{rotulo.Rotulo} {nome}"),
            EntityState.Deleted => (AuditoriaAcao.Excluiu, $"{rotulo.Rotulo} {nome} excluído"),
            _ when Inativou(item) => (AuditoriaAcao.Inativou, $"Inativação de {nome}"),
            _ => (AuditoriaAcao.Alterou, $"{string.Join(", ", item.Campos.Select(c => c.Marca.Campo))} de {nome}"),
        };

        return new EventoDeAuditoriaNovo(
            rotulo.Area,
            acao,
            descricao,
            entidade.RegistroAfetado,
            item.Tabela,
            Convert.ToInt64(item.Chave.GetValue(item.Entidade), CultureInfo.InvariantCulture),
            mudancas);
    }

    private static bool Inativou(Item item) =>
        item.Campos.Any(c => c.Propriedade.Name == "Ativo" && c.Propriedade.PropertyType == typeof(bool) && c.Antes is true && c.Propriedade.GetValue(item.Entidade) is false);

    private static Type? PrincipalAuditavelDe(EntityEntry entrada, string propriedade)
    {
        var chaveEstrangeira = entrada.Metadata.FindProperty(propriedade)?.GetContainingForeignKeys().FirstOrDefault(f => f.Properties.Count == 1);
        var principal = chaveEstrangeira?.PrincipalEntityType.ClrType;
        return principal is not null && typeof(IAuditavel).IsAssignableFrom(principal) ? principal : null;
    }

    /// <summary>O texto do valor: "Sim"/"Não", a data como dd/MM/yyyy e, numa chave estrangeira para entidade <see cref="IAuditavel"/>, o NOME da apontada (se não achar, o id).</summary>
    private static async Task<string?> TextoAsync(DbContext db, Type? principalAuditavel, object? valor, CancellationToken ct)
    {
        if (valor is null)
        {
            return null;
        }

        if (principalAuditavel is not null && await db.FindAsync(principalAuditavel, [valor], ct) is IAuditavel apontada)
        {
            return apontada.NomeParaAuditoria;
        }

        return valor switch
        {
            bool b => b ? "Sim" : "Não",
            DateOnly d => d.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture),
            TimeOnly t => t.ToString("HH:mm", CultureInfo.InvariantCulture),
            IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
            _ => valor.ToString(),
        };
    }
}

/// <summary>As marcas <c>[Auditado]</c> e <c>[Auditar]</c> de cada tipo, lidas uma vez por reflexão e na ordem de declaração.</summary>
internal static class MarcasDeAuditoria
{
    private static readonly ConcurrentDictionary<Type, (AuditadoAttribute? Classe, IReadOnlyList<(PropertyInfo Propriedade, AuditarAttribute Marca)> Campos)> Cache = new();

    public static AuditadoAttribute? Auditado(Type tipo) => Ler(tipo).Classe;

    public static IReadOnlyList<(PropertyInfo Propriedade, AuditarAttribute Marca)> Campos(Type tipo) => Ler(tipo).Campos;

    private static (AuditadoAttribute? Classe, IReadOnlyList<(PropertyInfo Propriedade, AuditarAttribute Marca)> Campos) Ler(Type tipo) =>
        Cache.GetOrAdd(tipo, t =>
        {
            var classe = t.GetCustomAttribute<AuditadoAttribute>();
            var campos = classe is null
                ? []
                : t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                    .Select(p => (Propriedade: p, Marca: p.GetCustomAttribute<AuditarAttribute>()))
                    .Where(x => x.Marca is not null)
                    .OrderBy(x => x.Propriedade.MetadataToken)
                    .Select(x => (x.Propriedade, x.Marca!))
                    .ToList();
            return (classe, (IReadOnlyList<(PropertyInfo, AuditarAttribute)>)campos);
        });
}
