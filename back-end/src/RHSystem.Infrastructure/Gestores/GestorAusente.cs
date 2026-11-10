using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Gestores;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Gestores;

/// <summary>
/// "Gestor ausente" e "quem decide primeiro" (<c>CONVENCOES.md</c>, seção 9). A etapa de um pedido é decidida na criação e só muda por ação explícita (a porta <c>IPedidosParadosNoGestor</c>): esta classe só responde
/// o que acontece com o pedido que está nascendo. A marca vale até o dia anterior à data de volta, com "hoje" no dia de Brasília (<see cref="Domain.Colaboradores.Gestor.AusenteEm"/>).
/// </summary>
public sealed class GestorAusente(AppDbContext db, IRelogio relogio) : IGestorAusente
{
    private static readonly string NivelRh = NomesDeNivel.DoPerfil(Perfil.RH);

    private static readonly EtapaInicialDoPedido ParaORhSemGestor = new(EtapaInicial.Rh, null, false);

    public async Task<EtapaInicialDoPedido> EtapaInicialDoPedidoAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await db.Funcionarios.AsNoTracking()
            .Where(f => f.Id == funcionarioId)
            .Select(f => new { f.Ativo, f.SetorId, Nivel = f.NivelDeAcesso!.Nome })
            .FirstOrDefaultAsync(ct) ?? throw new NaoEncontradoException();

        // Quem é gestor (linha em GESTORES, ativo, e gerencia um setor ou é do RH cadastrado) não tem a etapa de gestor: o RH decide. Quem foi rebaixado a Funcionario já não é gestor e segue o gestor do setor dele.
        var eGestor = pessoa.Ativo && await db.Gestores.AnyAsync(g => g.FuncionarioId == funcionarioId && (db.Setores.Any(s => s.GestorId == g.Id) || pessoa.Nivel == NivelRh), ct);
        if (eGestor || pessoa.SetorId is not { } setorId)
        {
            return ParaORhSemGestor;
        }

        var gestor = await db.Gestores.AsNoTracking()
            .Include(g => g.Funcionario)
            .FirstOrDefaultAsync(g => db.Setores.Any(s => s.Id == setorId && s.GestorId == g.Id), ct);
        if (gestor is null || !gestor.Funcionario.Ativo)
        {
            return ParaORhSemGestor;
        }

        return gestor.AusenteEm(Hoje())
            ? new EtapaInicialDoPedido(EtapaInicial.Rh, gestor.Id, EtapaDoGestorAprovadaPorAusencia: true)
            : new EtapaInicialDoPedido(EtapaInicial.Gestor, gestor.Id, EtapaDoGestorAprovadaPorAusencia: false);
    }

    public async Task<bool> EstaAusenteHojeAsync(int gestorId, CancellationToken ct)
    {
        var gestor = await db.Gestores.AsNoTracking().FirstOrDefaultAsync(g => g.Id == gestorId, ct);
        return gestor?.AusenteEm(Hoje()) ?? false;
    }

    private DateOnly Hoje() => FusoBrasil.Hoje(relogio.AgoraUtc);
}
