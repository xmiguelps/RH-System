namespace RHSystem.Application.Gestores;

/// <summary>Quem decide primeiro um pedido novo.</summary>
public enum EtapaInicial
{
    Gestor,
    Rh,
}

/// <summary>
/// O que as fatias 3 (ajuste de ponto) e 7 (férias) gravam ao criar o pedido: a etapa em que ele nasce, o gestor dele (<c>null</c> quando o RH decide sem gestor) e se a etapa do gestor já nasce aprovada
/// porque ele está ausente hoje ("aprovada automaticamente").
/// </summary>
public sealed record EtapaInicialDoPedido(EtapaInicial Etapa, int? GestorId, bool EtapaDoGestorAprovadaPorAusencia);

/// <summary>
/// A regra de "gestor ausente" e de "quem decide primeiro" (<c>CONVENCOES.md</c>, seção 9), que as fatias 3 e 7 chamam ao CRIAR um pedido. Ela <b>não</b> decide <c>PEDIDO_RH_FORA_DE_GESTORES</c> nem <c>PEDIDO_PROPRIO</c>
/// (são da 3 e da 7) e <b>não</b> mexe em pedido já criado (isso é a porta <c>IPedidosParadosNoGestor</c>, da 1a).
/// </summary>
public interface IGestorAusente
{
    /// <summary>
    /// Quem decide primeiro o pedido novo desta pessoa:
    /// <list type="bullet">
    /// <item>a pessoa é gestor (linha em <c>GESTORES</c>, ativa, e gerencia um setor ou é do RH cadastrado), ou não tem setor, ou o setor não tem gestor, ou o gestor dele está inativo:
    /// <c>Rh</c>, sem gestor, nada aprovado;</item>
    /// <item>o gestor do setor está ausente HOJE: <c>Rh</c>, com o <c>GestorId</c> dele e a etapa do gestor já aprovada por ausência;</item>
    /// <item>senão: <c>Gestor</c>, com o <c>GestorId</c> dele.</item>
    /// </list>
    /// </summary>
    /// <exception cref="Comum.NaoEncontradoException">A pessoa não existe.</exception>
    Task<EtapaInicialDoPedido> EtapaInicialDoPedidoAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Marcado como ausente e (sem data de volta, ou hoje antes dela), com "hoje" no dia de Brasília. Gestor que não existe: <c>false</c>.</summary>
    Task<bool> EstaAusenteHojeAsync(int gestorId, CancellationToken ct);
}
