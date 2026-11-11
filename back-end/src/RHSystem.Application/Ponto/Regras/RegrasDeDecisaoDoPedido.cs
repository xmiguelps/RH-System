using RHSystem.Application.Comum;

namespace RHSystem.Application.Ponto.Regras;

/// <summary>Quem consulta o pedido e por qual porta: a própria pessoa (<c>/api/eu</c>, nunca decide), o gestor da equipe ou o RH.</summary>
public enum PapelNaConsulta
{
    PropriaPessoa,
    Gestor,
    Rh,
}

/// <summary>
/// Quem consulta. <paramref name="GestorId"/> é a linha dele em <c>GESTORES.ID_GESTOR</c> (<c>null</c> se não está em <c>GESTORES</c>): o gestor da etapa tem de ser a do pedido, e o RH só decide pedido sem etapa do gestor se tem uma
/// (o trigger do banco exige o gestor).
/// </summary>
public sealed record QuemConsultaOPedido(PapelNaConsulta Papel, int FuncionarioId, int? GestorId);

/// <summary>O que as regras precisam do pedido: a etapa, a pessoa do pedido e o <c>ID_GESTOR</c> (<c>null</c> = sem etapa do gestor).</summary>
public sealed record PedidoParaDecisao(EtapaDoPedido Etapa, int FuncionarioId, int? GestorIdDoPedido);

/// <summary>
/// Se <b>quem está consultando</b> pode decidir o pedido agora, pela tela do perfil dele (<c>podeDecidir</c>), e o porquê quando não (<c>motivoNaoPodeDecidir</c>): os CINCO textos fixos do contrato, que a fatia 7 copia para as férias.
/// </summary>
public static class RegrasDeDecisaoDoPedido
{
    public const string AguardandoOGestor = "Aguardando a análise do gestor.";
    public const string AguardandoORh = "Aguardando a análise do RH.";
    public const string JaDecidido = "Este pedido já foi decidido.";
    public const string RhForaDeGestores = "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH.";
    public const string PedidoProprio = "Você não pode decidir o seu próprio pedido.";

    public static (bool Pode, string? Motivo) PodeDecidir(PedidoParaDecisao pedido, QuemConsultaOPedido quem)
    {
        if (pedido.Etapa == EtapaDoPedido.Concluida)
        {
            return (false, JaDecidido);
        }

        if (quem.Papel == PapelNaConsulta.PropriaPessoa)
        {
            return (false, pedido.Etapa == EtapaDoPedido.Gestor ? AguardandoOGestor : AguardandoORh);
        }

        if (pedido.Etapa == EtapaDoPedido.Gestor)
        {
            return quem.Papel == PapelNaConsulta.Gestor && pedido.GestorIdDoPedido == quem.GestorId ? (true, null) : (false, AguardandoOGestor);
        }

        if (quem.Papel == PapelNaConsulta.Gestor)
        {
            return (false, AguardandoORh);
        }

        // Etapa do RH, quem consulta é do RH. Pedido que passou pelo gestor: qualquer RH confirma, inclusive a própria pessoa do pedido (o trigger só compara o ID_GESTOR).
        if (pedido.GestorIdDoPedido is not null)
        {
            return (true, null);
        }

        // Sem etapa do gestor o banco grava em ID_GESTOR a linha de GESTORES de quem decide: a própria pessoa não pode (vale antes do texto seguinte), e quem não está em GESTORES também não.
        if (quem.FuncionarioId == pedido.FuncionarioId)
        {
            return (false, PedidoProprio);
        }

        return quem.GestorId is null ? (false, RhForaDeGestores) : (true, null);
    }
}
