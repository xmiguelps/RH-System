using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestores;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// De quem são os pedidos que a consulta enxerga: os da própria pessoa (<c>/api/eu</c>), os da EQUIPE do gestor que aguardam ou passaram por ele (<c>ID_GESTOR</c> é o dele), ou todos (RH). O que está fora do escopo nunca é lido, então "não existe" e "é de outro" não
/// se distinguem. <paramref name="GestorId"/> é <c>GESTORES.ID_GESTOR</c> de quem consulta, se ele está em <c>GESTORES</c>.
/// </summary>
public sealed record EscopoDePedidos(PapelNaConsulta Papel, int FuncionarioId, EquipeDoGestor? Equipe, int? GestorId);

/// <summary>Os filtros das listas de pedidos (todos opcionais): o status do banco, a etapa deduzida e, para o gestor e o RH, a pessoa.</summary>
public sealed record FiltroDePedidos(StatusDoPedidoDeAjuste? Status, EtapaDoPedido? Etapa, int? FuncionarioId);

/// <summary>Uma página de pedidos lidos, o total que atende aos filtros e a contagem por etapa (dos pedidos <c>PENDENTE</c> do escopo, sem os filtros).</summary>
public sealed record PaginaDePedidos(IReadOnlyList<PedidoLido> Itens, int Total, PontoPedidoFilaResumo Resumo);

/// <summary>As leituras de pedidos de ajuste, sem rastreamento. Cada pedido vem completo (itens, pessoas e as batidas do dia agora).</summary>
public interface IConsultaDePedidosDeAjuste
{
    /// <summary><c>null</c> se o pedido não existe OU não é do escopo.</summary>
    Task<PedidoLido?> ObterAsync(int pedidoId, EscopoDePedidos escopo, CancellationToken ct);

    /// <summary>Sem <c>ordenarPor</c>: o mais recente primeiro (<c>CRIADO_EM</c> decrescente); com <c>ordenarPor</c> e sem <c>direcao</c>, decrescente também (o padrão do ponto).</summary>
    Task<PaginaDePedidos> ListarAsync(EscopoDePedidos escopo, FiltroDePedidos filtro, ParametrosDeLista parametros, CancellationToken ct);
}

/// <summary>As gravações do pedido de ajuste (o criar e, nas tarefas seguintes, as decisões).</summary>
public interface IPedidosDeAjusteRepositorio
{
    /// <summary>Já existe um pedido <c>PENDENTE</c> deste dia da pessoa.</summary>
    Task<bool> TemPendenteNoDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct);

    /// <summary>
    /// Cria o pedido (e o dia do ponto, se faltar): a etapa e o gestor vêm de <see cref="EtapaInicialDoPedido"/> (a aprovação por ausência já nasce gravada). A recusa do banco sai traduzida (<c>ProblemaDeApiException</c>, com os textos
    /// desta rota: pedido já em análise, sequência dos itens, inativo).
    /// </summary>
    Task<int> CriarAsync(int funcionarioId, DateOnly dia, string justificativa, DateTime agoraUtc, EtapaInicialDoPedido etapa, IReadOnlyList<ItemParaAplicar> itens, CancellationToken ct);

    /// <summary>
    /// A decisão do gestor como <b>UPDATE condicional</b> (a corrida com o gestor ausente, com o RH e com um segundo clique se resolve aqui): só vale para o pedido <c>PENDENTE</c> daquele <c>ID_GESTOR</c> que ainda não tem resultado dele. Aprovar grava
    /// <c>APROVOU</c> e a hora (o pedido segue <c>PENDENTE</c>, na etapa do RH); recusar grava <c>RECUSOU</c>, <c>REJEITADA</c>, a hora da etapa e a da análise. Devolve se aplicou (<c>false</c>: o pedido já não estava na etapa do gestor).
    /// </summary>
    Task<bool> DecidirComoGestorAsync(int pedidoId, int gestorId, bool aprovar, DateTime agoraUtc, string? observacao, CancellationToken ct);

    /// <summary>O e-mail corporativo da pessoa, ou <c>null</c> se ela não tem (o aviso do resultado então não é enviado).</summary>
    Task<string?> EmailCorporativoAsync(int funcionarioId, CancellationToken ct);

    /// <summary><c>GESTORES.ID_GESTOR</c> da pessoa, ou <c>null</c> se ela não está em <c>GESTORES</c> (o RH só decide pedido sem etapa do gestor se está).</summary>
    Task<int?> GestorIdDaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>
    /// A decisão FINAL do RH como <b>UPDATE condicional</b>: só vale para o pedido <c>PENDENTE</c> na etapa do RH (sem gestor, ou com o gestor já aprovado). Grava <c>APROVADA</c> ou <c>REJEITADA</c>, quem do RH confirmou, quando, a hora da análise e a observação (que
    /// substitui a do gestor). No pedido que não tem etapa do gestor grava também em <c>ID_GESTOR</c> a linha de <paramref name="gestorIdDoRh"/> (o trigger do banco exige). Devolve se aplicou.
    /// </summary>
    Task<bool> DecidirComoRhAsync(int pedidoId, bool aprovar, int funcionarioRhId, int? gestorIdDoRh, DateTime agoraUtc, string? observacao, CancellationToken ct);

    /// <summary>
    /// Grava no dia do ponto as batidas que o pedido muda (<see cref="BatidaDepoisDeAplicar.Mudou"/>): a nova entra como <c>CORRIGIDA_RH</c> e a existente é corrigida no lugar (<c>CORRIGIDA_RH</c> com a data da correção). Tudo na transação de quem chama; a recusa do banco
    /// (batida no mesmo segundo) sai traduzida (409 da aprovação).
    /// </summary>
    Task AplicarBatidasAsync(int funcionarioId, DateOnly dia, IReadOnlyList<BatidaDepoisDeAplicar> batidas, DateTime agoraUtc, CancellationToken ct);
}
