using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Um item como o banco o guarda.</summary>
public sealed record ItemLido(int Id, int? MarcacaoId, TipoDeMarcacao Tipo, DateTime InstanteUtc);

/// <summary>
/// Um pedido como o banco o guarda, com as pessoas já identificadas (a do pedido, a do gestor da etapa e a do RH que confirmou) e as batidas do dia AGORA. <paramref name="GestorId"/> é <c>GESTORES.ID_GESTOR</c> e
/// <paramref name="Gestor"/> é a pessoa dessa linha.
/// </summary>
public sealed record PedidoLido(
    int Id,
    FuncionarioResumo Funcionario,
    DateOnly Dia,
    DateTime? SolicitadoEmUtc,
    string Justificativa,
    StatusDoPedidoDeAjuste Status,
    int? GestorId,
    FuncionarioResumo? Gestor,
    string? Observacao,
    ResultadoDaEtapaDoGestor? ResultadoDoGestor,
    DateTime? EtapaDoGestorEmUtc,
    bool EtapaDoGestorAutomatica,
    FuncionarioResumo? FuncionarioRh,
    DateTime? ConfirmadoPeloRhEmUtc,
    IReadOnlyList<ItemLido> Itens,
    IReadOnlyList<BatidaLida> BatidasDoDia);

/// <summary>
/// Do pedido do banco para o <see cref="PontoPedidoAjuste"/> do contrato. <b>Quem decidiu</b>: o gestor da etapa só aparece se o pedido TEM etapa do gestor (pendente com <c>ID_GESTOR</c>, ou com resultado dele); no pedido sem etapa do gestor
/// o banco grava em <c>ID_GESTOR</c> a linha de quem do RH decidiu (o trigger exige) e isso NÃO vira <c>gestorResponsavel</c>. <b>A observação</b> é uma só no banco: é a do gestor enquanto o RH não decidiu (ou se o gestor recusou) e passa a ser a do RH
/// quando ele decide.
/// </summary>
public static class MapeadorDoPedidoDeAjuste
{
    public static PontoPedidoAjuste Montar(PedidoLido p, QuemConsultaOPedido quem)
    {
        var etapa = EtapaDoPedidoDeAjuste.De(p.Status, p.GestorId, p.ResultadoDoGestor);
        var temEtapaDoGestor = p.ResultadoDoGestor is not null || (p.Status == StatusDoPedidoDeAjuste.Pendente && p.GestorId is not null);
        var rhDecidiu = p.Status != StatusDoPedidoDeAjuste.Pendente && p.ResultadoDoGestor != ResultadoDaEtapaDoGestor.Recusou && p.FuncionarioRh is not null;
        var itens = p.Itens.OrderBy(i => i.Id).ToList();

        var decisaoDoGestor = p.ResultadoDoGestor is { } resultado
            ? new DecisaoDeEtapa(resultado == ResultadoDaEtapaDoGestor.Aprovou ? DecisaoDaEtapa.Aprovada : DecisaoDaEtapa.Rejeitada, p.EtapaDoGestorEmUtc ?? default, p.EtapaDoGestorAutomatica)
            {
                DecididaPor = p.EtapaDoGestorAutomatica ? null : p.Gestor,
                Observacao = rhDecidiu ? null : p.Observacao,
            }
            : null;

        var decisaoDoRh = rhDecidiu
            ? new DecisaoDeEtapa(p.Status == StatusDoPedidoDeAjuste.Aprovada ? DecisaoDaEtapa.Aprovada : DecisaoDaEtapa.Rejeitada, p.ConfirmadoPeloRhEmUtc ?? default, false)
            {
                DecididaPor = p.FuncionarioRh,
                Observacao = p.Observacao,
            }
            : null;

        var (pode, motivo) = RegrasDeDecisaoDoPedido.PodeDecidir(new PedidoParaDecisao(etapa, p.Funcionario.Id, p.GestorId), quem);

        return new PontoPedidoAjuste(
            p.Id,
            p.Funcionario,
            p.Dia,
            p.SolicitadoEmUtc ?? default,
            p.Justificativa,
            p.Status,
            etapa,
            temEtapaDoGestor ? p.Gestor : null,
            decisaoDoGestor,
            decisaoDoRh,
            [.. itens.Select(i => new PontoPedidoItem(i.Id, i.MarcacaoId, i.Tipo, i.InstanteUtc))],
            ResumoDaCorrecao.Montar([.. itens.Select(i => new ItemParaAplicar(i.MarcacaoId, i.InstanteUtc, i.Tipo))]),
            PeriodosDoDia.De(p.BatidasDoDia),
            pode,
            motivo);
    }
}
