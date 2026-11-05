namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A <b>única</b> porta que a 1b e a 1c injetam para mexer nos pedidos que estão parados na etapa de um gestor (o composto <see cref="PedidosParadosNoGestorComposto"/>).
/// A etapa de um pedido é fixada na criação (<c>CONVENCOES.md</c>, seção 9): quando o gestor sai do caminho, nada a muda sozinho, e "passar ao RH" é uma ação explícita de
/// quem tira o gestor. <c>gestorId</c> é <c>Gestor.Id</c> (a linha de <c>GESTORES</c>, o mesmo id de <c>Setor.DefinirGestor</c>). As duas ações são <b>idempotentes</b> e
/// rodam no <c>AppDbContext</c> (e na transação) de quem chamou: o fato e a passagem ao RH se desfazem juntos.
/// </summary>
public interface IPedidosParadosNoGestor
{
    /// <summary>
    /// "Gestor ausente" ligado: aprova a etapa do gestor dos pedidos já parados nele e os manda ao RH (o contrato exclui férias com sugestão de data em andamento).
    /// Devolve quantos aprovou agora; sem nenhuma fatia registrada, <see cref="PedidosAprovados.Zero"/>.
    /// </summary>
    Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct);

    /// <summary>O gestor deixou o setor, foi trocado ou foi desligado: os pedidos parados na etapa dele passam à etapa do RH, sem aprovação.</summary>
    Task PassarParaORhAsync(int gestorId, CancellationToken ct);
}

/// <summary>
/// O que cada fatia dona de pedidos implementa sobre as tabelas dela (3: ponto; 7: férias). Cada uma a registra com
/// <c>AddScoped&lt;IPedidosParadosNoGestorDaFatia, ...&gt;</c> e <b>nunca</b> como <see cref="IPedidosParadosNoGestor"/>: duas fatias que trocassem o registro da mesma
/// porta apagariam uma à outra. O composto junta todas.
/// </summary>
public interface IPedidosParadosNoGestorDaFatia
{
    Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct);

    Task PassarParaORhAsync(int gestorId, CancellationToken ct);
}
