using RHSystem.Application.Comum;

namespace RHSystem.Application.Ponto.Mensagens;

/// <summary>
/// Um erro do ponto como o contrato o mostra: o status, o <c>title</c>, o <c>detail</c> e o <c>codigo</c> (nulo na recusa genérica do banco). <see cref="ComoProblema"/> monta a resposta; <paramref name="regraDoBanco"/> vai na extensão
/// <c>regraDoBanco</c> quando a recusa é de uma regra do banco (o front a mostra como diagnóstico).
/// </summary>
public sealed record MensagemDoPonto(int Status, string Titulo, string Detalhe, string? Codigo)
{
    public ProblemaDeApiException ComoProblema(string? regraDoBanco = null) =>
        new(Status, Titulo, Detalhe, Codigo, regraDoBanco is null ? null : new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });
}

/// <summary>
/// Os textos das regras do banco (as mensagens dos triggers de <c>V002</c>) que o ponto reconhece. São a mensagem ORIGINAL do banco, sem acento; o contrato as devolve em <c>regraDoBanco</c>, e a aplicação usa as mesmas quando ela mesma
/// confere antes de gravar (a recusa tem de sair igual, venha de onde vier).
/// </summary>
public static class RegrasDoBancoDoPonto
{
    public const string FuncionarioInativo = "Nao e possivel registrar ponto de funcionario inativo.";

    public const string PontoDeInativoAlterado = "Nao e possivel alterar ponto de funcionario inativo.";

    public const string AnteriorAAdmissao = "A data do ponto e anterior a admissao.";

    public const string PosteriorAoDesligamento = "A data do ponto e posterior ao desligamento.";

    public const string SolicitacaoAnalisada = "Solicitacao analisada nao pode mudar de status.";

    public const string PedidoSemGestor = "Informe o gestor responsavel pela analise.";

    public const string PedidoProprio = "O gestor nao pode analisar a propria solicitacao.";
}

/// <summary>
/// Os erros com código da fatia 3 (ponto), <b>copiados dos exemplos do contrato</b> (<c>contrato/fontes/30-fatia-3-ponto.yaml</c>) e nunca reescritos: o front mostra o <c>detail</c> como vem. <c>MensagensDoPontoTests</c> lê o contrato de
/// verdade e confere cada um. O mesmo código tem textos diferentes por rota (a batida duplicada e o colaborador inativo): uma mensagem para cada texto. <c>FERIADO_NAO_EDITAVEL</c> não tem exemplo no contrato: o texto é escolha do plano.
/// </summary>
public static class MensagensDoPonto
{
    private const string Conflito = "PONTO_BATIDA_DUPLICADA";
    private const string Inativo = "FUNCIONARIO_INATIVO";
    private const string SequenciaInvalida = "PONTO_SEQUENCIA_INVALIDA";

    // ---------------------------------------------------------------- 409

    public static MensagemDoPonto SequenciaMudou { get; } =
        new(409, "A sequência do ponto mudou.", "O seu ponto mudou desde a última vez que você abriu a tela. Confira e tente de novo.", "PONTO_SEQUENCIA_MUDOU");

    public static MensagemDoPonto BatidaDuplicadaNoRegistro { get; } = new(409, "Batida duplicada.", "O seu ponto já foi registrado agora há pouco.", Conflito);

    public static MensagemDoPonto BatidaDuplicadaNaCorrecao { get; } = new(409, "Batida duplicada.", "Há duas batidas no mesmo horário neste dia. Corrija os horários.", Conflito);

    public static MensagemDoPonto BatidaDuplicadaNaAprovacao { get; } = new(409, "Batida duplicada.", "Já existe uma batida neste horário. Nada foi alterado.", Conflito);

    public static MensagemDoPonto PedidoJaEmAnalise { get; } =
        new(409, "Já existe um pedido para este dia.", "Este dia já tem um pedido de ajuste em análise. Espere a resposta antes de pedir outro.", "PONTO_PEDIDO_JA_EM_ANALISE");

    public static MensagemDoPonto DiaComPedidoEmAnalise { get; } =
        new(409, "O dia tem um pedido em análise.", "Responda o pedido de ajuste deste dia antes de editar as batidas.", "PONTO_DIA_COM_PEDIDO_EM_ANALISE");

    public static MensagemDoPonto MarcacaoEmUso { get; } =
        new(409, "Batida em uso.", "Esta batida está ligada a um pedido de ajuste e não pode ser removida.", "PONTO_MARCACAO_EM_USO");

    public static MensagemDoPonto PedidoJaDecidido { get; } = new(409, "Pedido já decidido.", "Este pedido já foi respondido. Atualize a lista.", "PEDIDO_JA_DECIDIDO");

    /// <summary>As decisões do gestor e a recusa do RH: o pedido saiu da etapa de quem decide.</summary>
    public static MensagemDoPonto EtapaIncorreta { get; } =
        new(409, "Pedido fora da vez.", "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista.", "PEDIDO_ETAPA_INCORRETA");

    /// <summary>A aprovação do RH de um pedido que ainda está na etapa do gestor.</summary>
    public static MensagemDoPonto EtapaIncorretaDoGestor { get; } =
        new(409, "Pedido fora da vez.", "Este pedido ainda está na etapa do gestor. Espere a análise dele.", "PEDIDO_ETAPA_INCORRETA");

    public static MensagemDoPonto FeriadoDuplicado { get; } = new(409, "Feriado repetido.", "Já existe um feriado com este nome nesta data.", "FERIADO_DUPLICADO");

    public static MensagemDoPonto AtestadoJaDecidido { get; } = new(409, "Atestado já decidido.", "Este atestado já foi respondido. Atualize a lista.", "ATESTADO_JA_DECIDIDO");

    // ---------------------------------------------------------------- 422

    public static MensagemDoPonto FuncionarioInativoNoRegistro { get; } = new(422, "Colaborador inativo.", "Este colaborador está inativo e não pode registrar ponto.", Inativo);

    public static MensagemDoPonto FuncionarioInativoNoPedido { get; } = new(422, "Colaborador inativo.", "Este colaborador está inativo e não pode pedir ajuste de ponto.", Inativo);

    public static MensagemDoPonto FuncionarioInativoNaCorrecao { get; } =
        new(422, "Colaborador inativo.", "Este colaborador está inativo e o ponto dele não pode mais ser alterado.", Inativo);

    /// <summary>A recusa de uma regra do banco que o contrato não nomeia (dia anterior à admissão, posterior ao desligamento...): sem <c>codigo</c>, a mensagem original em <c>regraDoBanco</c>.</summary>
    public static MensagemDoPonto OperacaoRecusada { get; } = new(422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.", null);

    public static MensagemDoPonto SequenciaInvalidaNoPedido { get; } =
        new(422, "Batidas inconsistentes.", "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários.", SequenciaInvalida);

    public static MensagemDoPonto SequenciaInvalidaNaAprovacao { get; } =
        new(422, "Batidas inconsistentes.", "Com as batidas do pedido o dia fica com uma entrada sem saída. Peça à pessoa um novo pedido.", SequenciaInvalida);

    public static MensagemDoPonto PedidoDiaNaoPassou { get; } =
        new(422, "O dia ainda não passou.", "Só dá para pedir ajuste de um dia que já passou. Para hoje, use o cartão \"Seu ponto hoje\".", "PONTO_PEDIDO_DIA_NAO_PASSOU");

    public static MensagemDoPonto InstanteNoFuturo { get; } =
        new(422, "Horário no futuro.", "Uma das batidas pedidas tem horário que ainda não chegou. Confira os horários.", "PONTO_INSTANTE_NO_FUTURO");

    public static MensagemDoPonto InstanteForaDoDia { get; } =
        new(422, "Horário fora do dia.", "Cada batida precisa cair no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite).", "PONTO_INSTANTE_FORA_DO_DIA");

    public static MensagemDoPonto PedidoRhForaDeGestores { get; } =
        new(422, "Quem decide precisa ser gestor também.", "Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor.", "PEDIDO_RH_FORA_DE_GESTORES");

    public static MensagemDoPonto PedidoProprio { get; } =
        new(422, "Você não pode decidir o próprio pedido.", "A operação foi recusada por uma regra do banco de dados.", "PEDIDO_PROPRIO");

    /// <summary>O texto do ponto (título "Empresa não configurada."), não o de <c>MensagensDoApoio</c>.</summary>
    public static MensagemDoPonto EmpresaNaoConfigurada { get; } =
        new(422, "Empresa não configurada.", "Cadastre a UF e o município da empresa antes de criar feriados municipais.", "EMPRESA_NAO_CONFIGURADA");

    /// <summary>Sem exemplo no contrato: o texto é escolha do plano (decisão 12).</summary>
    public static MensagemDoPonto FeriadoNaoEditavel { get; } = new(422, "Feriado não editável.", "Só feriados municipais podem ser editados.", "FERIADO_NAO_EDITAVEL");

    /// <summary>Todas, para o teste contra o contrato conferir uma a uma.</summary>
    public static IReadOnlyList<MensagemDoPonto> Todas { get; } =
    [
        SequenciaMudou, BatidaDuplicadaNoRegistro, BatidaDuplicadaNaCorrecao, BatidaDuplicadaNaAprovacao, PedidoJaEmAnalise, DiaComPedidoEmAnalise, MarcacaoEmUso, PedidoJaDecidido, EtapaIncorreta, EtapaIncorretaDoGestor,
        FeriadoDuplicado, AtestadoJaDecidido, FuncionarioInativoNoRegistro, FuncionarioInativoNoPedido, FuncionarioInativoNaCorrecao, OperacaoRecusada, SequenciaInvalidaNoPedido, SequenciaInvalidaNaAprovacao, PedidoDiaNaoPassou,
        InstanteNoFuturo, InstanteForaDoDia, PedidoRhForaDeGestores, PedidoProprio, EmpresaNaoConfigurada, FeriadoNaoEditavel,
    ];
}

/// <summary>De que rota o erro do banco veio: o texto de "colaborador inativo" e o de "batida duplicada" dependem dela.</summary>
public enum ContextoDaRota
{
    RegistrarMarcacao,
    CriarPedido,
    CorrigirDia,
    DecidirPedido,
    Feriado,
}
