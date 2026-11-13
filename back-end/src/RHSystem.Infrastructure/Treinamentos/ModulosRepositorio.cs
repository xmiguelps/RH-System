using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A escrita dos módulos. As entidades NÃO são auditadas pelo interceptor (o evento é escrito à mão). Toda escrita começa por <see cref="TravarTreinamentoAsync"/>: acrescentar módulo, publicar e as outras escritas do treinamento se enfileiram na linha dele, então a ordem (<c>UQ (ID_TREINAMENTO, ORDEM)</c>) nunca colide entre duas telas e o
/// "ainda é rascunho" é conferido com a linha travada. Reordenar e renumerar usam DUAS fases (a ordem é única e o MySQL confere linha a linha): todos sobem 1000, depois cada um recebe a posição final.
/// </summary>
public sealed class ModulosRepositorio(AppDbContext db) : IModulosRepositorio
{
    private const int Salto = 1000;

    public async Task<EstadoDoTreinamentoTravado?> TravarTreinamentoAsync(int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;

        // AUDITORIA-OK: não muda valor nenhum (TITULO = TITULO): só segura a linha do treinamento até o fim da transação (publicar e as outras escritas de módulo esperam) para conferir o estado; o evento Alterou é do caso de uso.
        var linhas = await db.Treinamentos.Where(t => t.Id == id).ExecuteUpdateAsync(s => s.SetProperty(t => t.Titulo, t => t.Titulo), ct);
        return linhas == 0
            ? null
            : await db.Treinamentos.AsNoTracking().Where(t => t.Id == id).Select(t => new EstadoDoTreinamentoTravado(t.Publicado, t.EncerradoEmUtc)).SingleAsync(ct);
    }

    public async Task<IReadOnlyList<ModuloLido>> ListarAsync(int treinamentoId, CancellationToken ct) =>
        await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamentoId).OrderBy(m => m.Ordem)
            .Select(m => new ModuloLido(m.Id, m.TreinamentoId, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos, m.UrlExterna, m.CaminhoArquivo, m.ConteudoTexto, m.NomeArquivoOriginal))
            .ToListAsync(ct);

    public async Task<ModuloLido?> ObterAsync(int treinamentoId, int moduloId, CancellationToken ct) =>
        await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamentoId && m.Id == moduloId)
            .Select(m => new ModuloLido(m.Id, m.TreinamentoId, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos, m.UrlExterna, m.CaminhoArquivo, m.ConteudoTexto, m.NomeArquivoOriginal))
            .SingleOrDefaultAsync(ct);

    public async Task<ModuloLido> AdicionarAsync(int treinamentoId, CamposDoModulo campos, string? caminhoDoArquivo, string? nomeDoArquivo, CancellationToken ct)
    {
        var ultima = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamentoId).MaxAsync(m => (int?)m.Ordem, ct) ?? 0;
        var modulo = TreinamentoModulo.Criar(treinamentoId, ultima + 1, campos.Titulo, campos.Tipo, campos.DuracaoMinutos, campos.UrlExterna, caminhoDoArquivo, campos.ConteudoTexto, nomeDoArquivo);
        db.TreinamentosModulos.Add(modulo);
        await db.SaveChangesAsync(ct);
        return new ModuloLido(modulo.Id, treinamentoId, modulo.Ordem, modulo.Titulo, modulo.Tipo, modulo.DuracaoMinutos, modulo.UrlExterna, modulo.CaminhoArquivo, modulo.ConteudoTexto, modulo.NomeArquivoOriginal);
    }

    public async Task ReordenarAsync(int treinamentoId, IReadOnlyList<int> idsNaOrdemNova, CancellationToken ct) => await GravarPosicoesAsync(treinamentoId, idsNaOrdemNova, ct);

    public async Task SubstituirAsync(int moduloId, CamposDoModulo campos, string? caminhoDoArquivo, string? nomeDoArquivo, CancellationToken ct)
    {
        var id = moduloId;
        var titulo = campos.Titulo;
        var tipo = campos.Tipo;
        var duracao = campos.DuracaoMinutos;
        var url = campos.UrlExterna;
        var texto = campos.ConteudoTexto;

        // AUDITORIA-OK: a alteração do módulo é auditada à mão pelo caso de uso (evento Alterou, na mesma transação); o conteúdo do módulo não vai para o evento. Um UPDATE só: o CHECK do banco confere a linha inteira (o campo do tipo novo preenchido e os dos outros anulados juntos).
        _ = await db.TreinamentosModulos
            .Where(m => m.Id == id)
            .ExecuteUpdateAsync(
                s => s.SetProperty(m => m.Titulo, titulo)
                    .SetProperty(m => m.Tipo, tipo)
                    .SetProperty(m => m.DuracaoMinutos, duracao)
                    .SetProperty(m => m.UrlExterna, url)
                    .SetProperty(m => m.ConteudoTexto, texto)
                    .SetProperty(m => m.CaminhoArquivo, caminhoDoArquivo)
                    .SetProperty(m => m.NomeArquivoOriginal, nomeDoArquivo),
                ct);
    }

    public async Task ExcluirAsync(int treinamentoId, int moduloId, CancellationToken ct)
    {
        var id = moduloId;

        // AUDITORIA-OK: o módulo some junto com a alteração do treinamento, que o caso de uso audita (evento Alterou); só existe em rascunho, onde ninguém tem progresso nele.
        _ = await db.TreinamentosModulos.Where(m => m.Id == id).ExecuteDeleteAsync(ct);
        var restantes = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamentoId).OrderBy(m => m.Ordem).Select(m => m.Id).ToListAsync(ct);
        await GravarPosicoesAsync(treinamentoId, restantes, ct);
    }

    private async Task GravarPosicoesAsync(int treinamentoId, IReadOnlyList<int> idsNaOrdem, CancellationToken ct)
    {
        if (idsNaOrdem.Count == 0)
        {
            return;
        }

        var treinamento = treinamentoId;

        // AUDITORIA-OK: a primeira fase só afasta as posições (todas sobem 1000) para a segunda não colidir com a chave única; a alteração é auditada à mão pelo caso de uso.
        _ = await db.TreinamentosModulos.Where(m => m.TreinamentoId == treinamento).ExecuteUpdateAsync(s => s.SetProperty(m => m.Ordem, m => m.Ordem + Salto), ct);
        for (var i = 0; i < idsNaOrdem.Count; i++)
        {
            var moduloId = idsNaOrdem[i];
            var posicao = i + 1;

            // AUDITORIA-OK: a segunda fase dá a posição final de cada módulo; a alteração é auditada à mão pelo caso de uso.
            _ = await db.TreinamentosModulos.Where(m => m.Id == moduloId && m.TreinamentoId == treinamento).ExecuteUpdateAsync(s => s.SetProperty(m => m.Ordem, posicao), ct);
        }
    }
}

/// <summary>
/// As sobras de <c>treinamentos/</c> na limpeza de órfãos: o arquivo que um módulo referencia nunca é apagado; o que ficou sem linha (envio que caiu entre o blob e a linha, troca de arquivo cujo apagamento falhou) sai depois do mínimo de horas. Sem esta fonte as sobras nunca seriam limpas.
/// </summary>
public sealed class FonteDeReferenciasDosModulos(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDoModulo.Pasta + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var dosModulos = await db.TreinamentosModulos.AsNoTracking().Where(m => m.CaminhoArquivo != null && lista.Contains(m.CaminhoArquivo)).Select(m => m.CaminhoArquivo!).ToListAsync(ct);
        return dosModulos.ToHashSet(StringComparer.Ordinal);
    }
}
