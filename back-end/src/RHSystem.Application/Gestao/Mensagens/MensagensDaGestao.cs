using RHSystem.Application.Comum;
using RHSystem.Application.Ferias.Regras;

namespace RHSystem.Application.Gestao.Mensagens;

/// <summary>
/// Um erro da gestão como o contrato o mostra: o status, o <c>title</c>, o <c>detail</c> e o <c>codigo</c>. <see cref="ComoProblema"/> monta a resposta; <paramref name="regraDoBanco"/> vai na extensão <c>regraDoBanco</c> quando a recusa é a de uma regra do banco (o front a mostra como diagnóstico).
/// </summary>
public sealed record MensagemDaGestao(int Status, string Titulo, string Detalhe, string? Codigo)
{
    public ProblemaDeApiException ComoProblema(string? regraDoBanco = null) =>
        new(Status, Titulo, Detalhe, Codigo, regraDoBanco is null ? null : new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });
}

/// <summary>Os textos das regras do banco (as mensagens dos triggers e os nomes das restrições de <c>V001</c>) que a gestão reconhece: a mensagem ORIGINAL do banco, sem acento; o contrato a devolve em <c>regraDoBanco</c>.</summary>
public static class RegrasDoBancoDaGestao
{
    public const string FeriasDeInativo = "Funcionario inativo nao pode solicitar ferias.";

    public const string FeriasProprias = "O gestor nao pode analisar as proprias ferias.";

    public const string FeriasSemGestor = "Informe o gestor responsavel pela analise das ferias.";

    public const string BeneficioDeInativo = "Nao e possivel ativar beneficio para funcionario inativo.";

    public const string ChaveDaAdesao = "UQ_BENEFICIOS_FUNCIONARIOS_FUNCIONARIO_BENEFICIO";

    public const string ChaveDaExcecaoDePagamento = "UQ_PAGAMENTOS_EXCECOES_ANO_MES";
}

/// <summary>Qual dos três 422 de regras de férias: o título e o texto mudam (pedir ou aceitar; a sugestão do gestor; o registro do RH).</summary>
public enum VarianteDasRegras
{
    PedirOuAceitar,
    Sugerir,
    Registrar,
}

/// <summary>
/// Os textos e os códigos de erro da gestão (férias, benefícios, pagamento), copiados dos EXEMPLOS do contrato (<c>70-fatia-7-gestao.yaml</c>) e nunca reescritos (o front mostra o <c>detail</c> como vem). Os dois 409 de pedido têm texto PRÓPRIO desta fatia (diferente dos do ponto). O teste
/// <c>MensagensDaGestaoTests</c> confere cada um contra o contrato, com a lista das exceções que o contrato cita e não exemplifica (o executor escreve o texto, registrado no ledger).
/// </summary>
public static class MensagensDaGestao
{
    private const string Recusada = "Operação recusada.";

    // ---------------------------------------------------------------- 409

    public static MensagemDaGestao PedidoNaEtapaErrada { get; } = new(409, "Pedido na etapa errada.", "Este pedido ainda não chegou à sua etapa ou já passou por ela.", "PEDIDO_ETAPA_INCORRETA");

    public static MensagemDaGestao PedidoJaDecidido { get; } = new(409, "Pedido já decidido.", "Este pedido já foi decidido.", "PEDIDO_JA_DECIDIDO");

    public static MensagemDaGestao BeneficioJaAtivoOuPendente { get; } =
        new(409, "Você já pediu este benefício.", "Você já tem este benefício ou já pediu a adesão e o RH ainda vai responder.", "BENEFICIO_JA_ATIVO_OU_PENDENTE");

    public static MensagemDaGestao BeneficioAdesaoNaoAtiva { get; } =
        new(409, "A adesão não está ativa.", "Este benefício ainda não está ativo para a pessoa; o pedido só vale em adesão aprovada.", "BENEFICIO_ADESAO_NAO_ATIVA");

    public static MensagemDaGestao BeneficioPedidoJaDecidido { get; } =
        new(409, "Pedido de benefício já respondido.", "Este pedido de benefício já foi aprovado, recusado ou cancelado.", "BENEFICIO_PEDIDO_JA_DECIDIDO");

    // ---------------------------------------------------------------- 422: benefícios

    public static MensagemDaGestao BeneficioNaoOpcional { get; } = new(422, "Este benefício não se pede.", "Este benefício não é opcional: o RH o concede direto na ficha.", "BENEFICIO_NAO_OPCIONAL");

    public static MensagemDaGestao BeneficioNaoAceitaDependentes { get; } = new(422, "Este benefício não aceita dependentes.", "Este benefício não permite incluir dependentes.", "BENEFICIO_NAO_ACEITA_DEPENDENTES");

    public static MensagemDaGestao BeneficioNaoAceitaTrajeto { get; } = new(422, "Este benefício não tem trajeto.", "Só o vale-transporte tem trajeto.", "BENEFICIO_NAO_ACEITA_TRAJETO");

    /// <summary>O pedido de adesão, dependente ou trajeto de pessoa inativa (o exemplo do contrato não traz <c>regraDoBanco</c>).</summary>
    public static MensagemDaGestao FuncionarioInativoParaPedirBeneficio { get; } = new(422, Recusada, "Não é possível pedir nem ativar benefício para uma pessoa inativa.", "FUNCIONARIO_INATIVO");

    /// <summary>A aprovação da adesão de pessoa inativa: o banco não barra (o trigger só olha o INSERT), a aplicação responde com este texto.</summary>
    public static MensagemDaGestao FuncionarioInativoParaAprovarBeneficio { get; } = new(422, Recusada, "Não é possível ativar benefício para uma pessoa inativa.", "FUNCIONARIO_INATIVO");

    // ---------------------------------------------------------------- 422: férias

    /// <summary>Sem exemplo no contrato (o código é citado na descrição de <c>pedirFerias</c>): texto do executor.</summary>
    public static MensagemDaGestao FeriasSemPeriodoAquisitivo { get; } = new(422, Recusada, "Você ainda não completou um ano de casa, então ainda não há férias para pedir.", "FERIAS_SEM_PERIODO_AQUISITIVO");

    /// <summary>Sem exemplo no contrato: o título e o texto são do executor; a regra do banco é a do trigger de <c>FERIAS</c>.</summary>
    public static ProblemaDeApiException FuncionarioInativoDeFerias() =>
        new(422, Recusada, "Não é possível pedir nem registrar férias para uma pessoa inativa.", "FUNCIONARIO_INATIVO", new Dictionary<string, object?> { ["regraDoBanco"] = RegrasDoBancoDaGestao.FeriasDeInativo });

    /// <summary>O RH (ou quem decidiria) é a dona do pedido: 422 com a regra do trigger no <c>regraDoBanco</c>, que o contrato mostra mesmo quando é a aplicação que barra antes.</summary>
    public static ProblemaDeApiException PedidoProprio() =>
        new(422, "Você não pode decidir o próprio pedido.", "A operação foi recusada por uma regra do banco de dados.", "PEDIDO_PROPRIO", new Dictionary<string, object?> { ["regraDoBanco"] = RegrasDoBancoDaGestao.FeriasProprias });

    public static ProblemaDeApiException PedidoRhForaDeGestores() =>
        new(
            422,
            "Quem decide precisa ser gestor também.",
            "Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor.",
            "PEDIDO_RH_FORA_DE_GESTORES",
            new Dictionary<string, object?> { ["regraDoBanco"] = RegrasDoBancoDaGestao.FeriasSemGestor });

    /// <summary>O 422 de regras não atendidas, com a lista <c>regras</c> (todas as conferidas, não só as que falharam).</summary>
    public static ProblemaDeApiException FeriasRegrasNaoAtendidas(VarianteDasRegras variante, IReadOnlyList<ResultadoDaRegra> regras)
    {
        var (titulo, detalhe) = variante switch
        {
            VarianteDasRegras.Sugerir => ("Sugestão recusada pelas regras.", "As datas sugeridas não cumprem uma ou mais regras de férias."),
            VarianteDasRegras.Registrar => ("Registro de férias recusado.", "O registro não cumpre uma ou mais regras de férias."),
            _ => ("Pedido de férias recusado.", "O pedido não cumpre uma ou mais regras de férias."),
        };
        IReadOnlyList<IReadOnlyDictionary<string, object?>> lista =
            [.. regras.Select(r => new Dictionary<string, object?> { ["codigo"] = CodigosDasRegrasDeFerias.Texto(r.Regra), ["atendida"] = r.Atendida, ["mensagem"] = r.Mensagem })];
        return new ProblemaDeApiException(422, titulo, detalhe, "FERIAS_REGRAS_NAO_ATENDIDAS", new Dictionary<string, object?> { ["regras"] = lista });
    }

    /// <summary>A recusa de uma regra do banco que nenhum caso de uso previu: 422 com a regra em <c>regraDoBanco</c>.</summary>
    public static ProblemaDeApiException OperacaoRecusada(string regraDoBanco) =>
        new(422, Recusada, "A operação foi recusada por uma regra do banco de dados.", null, new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });

    /// <summary>Todas as de texto fixo e código, para o teste contra o contrato conferir uma a uma.</summary>
    public static IReadOnlyList<MensagemDaGestao> Todas { get; } =
    [
        PedidoNaEtapaErrada, PedidoJaDecidido, BeneficioJaAtivoOuPendente, BeneficioAdesaoNaoAtiva, BeneficioPedidoJaDecidido,
        BeneficioNaoOpcional, BeneficioNaoAceitaDependentes, BeneficioNaoAceitaTrajeto, FuncionarioInativoParaPedirBeneficio, FuncionarioInativoParaAprovarBeneficio, FeriasSemPeriodoAquisitivo,
    ];
}
