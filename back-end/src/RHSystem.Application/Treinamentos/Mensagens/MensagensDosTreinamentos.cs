using RHSystem.Application.Comum;

namespace RHSystem.Application.Treinamentos.Mensagens;

/// <summary>
/// Um erro dos treinamentos como o contrato o mostra: o status, o <c>title</c>, o <c>detail</c> e o <c>codigo</c> (alguns erros não têm código). <see cref="ComoProblema"/> monta a resposta; <paramref name="regraDoBanco"/> vai na extensão <c>regraDoBanco</c> quando a recusa é a de uma regra do banco (o front a mostra como diagnóstico).
/// </summary>
public sealed record MensagemDoTreinamento(int Status, string Titulo, string Detalhe, string? Codigo)
{
    public ProblemaDeApiException ComoProblema(string? regraDoBanco = null) =>
        new(Status, Titulo, Detalhe, Codigo, regraDoBanco is null ? null : new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });
}

/// <summary>Os textos das regras do banco (as mensagens dos triggers e os nomes das restrições de <c>V001</c>) que os treinamentos reconhecem. São a mensagem ORIGINAL do banco, sem acento; o contrato a devolve em <c>regraDoBanco</c>.</summary>
public static class RegrasDoBancoDosTreinamentos
{
    public const string FuncionarioInativo = "Nao e possivel inscrever funcionario inativo em treinamento.";

    public const string ConcluidoNaoMuda = "Treinamento concluido nao pode mudar de status.";

    public const string NotaAbaixoDe70 = "Nota abaixo de 70: use o status REPROVADO.";

    public const string ChaveDaOrdemDoModulo = "UQ_TREINAMENTOS_MODULOS_TREINAMENTO_ORDEM";

    public const string ChaveDaLinhaDaPessoa = "UQ_TREINAMENTOS_FUNCIONARIOS_TREINAMENTO_FUNCIONARIO";

    public const string ChaveDoProgresso = "UQ_TREINAMENTOS_MODULOS_PROGRESSO_MODULO_FUNC";

    public const string ChaveDoCodigoDoCertificado = "UQ_TREINAMENTOS_CERTIFICADOS_CODIGO";

    public const string ChaveDoCertificadoDaPessoa = "UQ_TREINAMENTOS_CERTIFICADOS_TREINAMENTO_FUNCIONARIO";

    public const string ChaveDoPublico = "UQ_TREINAMENTOS_PUBLICO_DESTINO";
}

/// <summary>O que o RH está pedindo quando há pessoas que ainda não concluíram: encerrar ou excluir (o texto do 409 muda no fim da frase).</summary>
public enum AcaoQueExigeConfirmacao
{
    Encerrar,
    Excluir,
}

/// <summary>
/// Os textos e os códigos de erro dos treinamentos, copiados dos EXEMPLOS do contrato (<c>60-fatia-6-tad.yaml</c>) e nunca reescritos (o front mostra o <c>detail</c> como vem). O teste <c>MensagensDosTreinamentosTests</c> confere cada um contra o contrato, com a lista das exceções que o contrato cita e não
/// exemplifica (o executor escreve o texto, registrado no ledger).
/// </summary>
public static class MensagensDosTreinamentos
{
    private const string Conflito = "Conflito.";

    private const string Recusada = "Operação recusada.";

    private const string Encerrado = "Treinamento encerrado.";

    // ---------------------------------------------------------------- 422: o estado do treinamento

    public static MensagemDoTreinamento TreinamentoJaPublicado { get; } = new(422, Recusada, "Este treinamento já foi publicado.", "TREINAMENTO_JA_PUBLICADO");

    public static MensagemDoTreinamento CampoTravadoDoTreinamento { get; } =
        new(422, Recusada, "Depois de publicado, só dá para mudar o título, a descrição e a opção de incluir quem for admitido.", "TREINAMENTO_CAMPO_TRAVADO");

    public static MensagemDoTreinamento CampoTravadoDoModulo { get; } =
        new(422, Recusada, "Depois de publicado, o tipo do módulo não muda. Dá para corrigir o título, a duração e o conteúdo.", "TREINAMENTO_CAMPO_TRAVADO");

    public static MensagemDoTreinamento ModulosTravados { get; } =
        new(422, Recusada, "Este treinamento já foi publicado. Não dá mais para acrescentar, reordenar nem excluir módulos; só corrigir o conteúdo deles.", "TREINAMENTO_MODULOS_TRAVADOS");

    public static MensagemDoTreinamento EncerradoParaEditar { get; } = new(422, Encerrado, "Este treinamento foi encerrado e não pode mais ser alterado.", "TREINAMENTO_ENCERRADO");

    public static MensagemDoTreinamento EncerradoParaEncerrar { get; } = new(422, Encerrado, "Este treinamento já foi encerrado.", "TREINAMENTO_ENCERRADO");

    public static MensagemDoTreinamento EncerradoParaAPessoa { get; } = new(422, Encerrado, "O RH encerrou este treinamento. Fale com o RH se precisar dele.", "TREINAMENTO_ENCERRADO");

    public static MensagemDoTreinamento NaoEstaEmAndamento { get; } = new(422, Recusada, "Só dá para encerrar um treinamento em andamento. Rascunho e agendado se excluem.", "TREINAMENTO_NAO_ESTA_EM_ANDAMENTO");

    // ---------------------------------------------------------------- 422: a pessoa

    public static MensagemDoTreinamento NaoEOpcional { get; } = new(422, Recusada, "Este treinamento é obrigatório e não precisa de inscrição.", "TREINAMENTO_NAO_E_OPCIONAL");

    public static MensagemDoTreinamento InscricaoPendente { get; } = new(422, Recusada, "Inscreva-se no treinamento antes de começar.", "INSCRICAO_PENDENTE");

    // ---------------------------------------------------------------- 422: onboarding

    /// <summary>O texto da rota <c>incluirFuncionarioNoOnboarding</c>; o do cadastro, da readmissão e da importação é o longo, que a porta <c>IInclusaoNoOnboarding</c> já lança.</summary>
    public static MensagemDoTreinamento SemTreinamentoDeOnboarding { get; } = new(422, Recusada, "Não há treinamento de onboarding publicado.", "SEM_TREINAMENTO_DE_ONBOARDING");

    /// <summary>O contrato traz <c>regraDoBanco</c> no exemplo mesmo quando é a aplicação que barra antes: a extensão sai sempre, com o texto fixo do trigger.</summary>
    public static ProblemaDeApiException FuncionarioInativo() =>
        new(422, Recusada, "Não é possível incluir uma pessoa inativa em treinamento.", "FUNCIONARIO_INATIVO", new Dictionary<string, object?> { ["regraDoBanco"] = RegrasDoBancoDosTreinamentos.FuncionarioInativo });

    // ---------------------------------------------------------------- 409 e 422 com números

    /// <summary>
    /// O 409 de <c>encerrarTreinamento</c> e <c>excluirTreinamento</c> sem <c>confirmar=true</c>: título próprio e os três números (<c>pendentes</c> = <c>emAndamento</c> + <c>naoIniciaram</c>) sempre no corpo. "38 pessoas ainda não concluíram este treinamento. Confirme para encerrar."; o singular ("1 pessoa ainda não
    /// concluiu este treinamento.") não tem exemplo no contrato: texto do executor.
    /// </summary>
    public static ProblemaDeApiException ConfirmacaoNecessaria(int emAndamento, int naoIniciaram, AcaoQueExigeConfirmacao acao)
    {
        var pendentes = emAndamento + naoIniciaram;
        var quando = pendentes == 1 ? "1 pessoa ainda não concluiu este treinamento" : $"{pendentes} pessoas ainda não concluíram este treinamento";
        return new ProblemaDeApiException(
            409,
            "Confirmação necessária.",
            $"{quando}. Confirme para {(acao == AcaoQueExigeConfirmacao.Encerrar ? "encerrar" : "excluir")}.",
            "CONFIRMACAO_NECESSARIA",
            new Dictionary<string, object?> { ["pendentes"] = pendentes, ["emAndamento"] = emAndamento, ["naoIniciaram"] = naoIniciaram });
    }

    /// <summary>
    /// O 422 de <c>excluirTreinamento</c> quando já há certificado emitido (vale também com <c>confirmar=true</c>): "Este treinamento já emitiu 63 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre." com <c>certificadosEmitidos</c>; o singular não tem exemplo no contrato:
    /// texto do executor.
    /// </summary>
    public static ProblemaDeApiException ComCertificados(int certificadosEmitidos) =>
        new(
            422,
            Recusada,
            $"Este treinamento já emitiu {(certificadosEmitidos == 1 ? "1 certificado" : $"{certificadosEmitidos} certificados")} e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.",
            "TREINAMENTO_COM_CERTIFICADOS",
            new Dictionary<string, object?> { ["certificadosEmitidos"] = certificadosEmitidos });

    // ---------------------------------------------------------------- 404 e 409 sem código

    /// <summary>O 404 da conferência pública: título próprio, sem código, o MESMO para código inexistente, de treinamento que não emite e de conclusão sem certificado.</summary>
    public static ProblemaDeApiException CertificadoNaoEncontrado { get; } = new(404, "Certificado não encontrado.", "Não encontramos um certificado com este código.", null);

    /// <summary>A corrida de duas telas mexendo nos módulos na mesma posição (a chave única da ordem recusa a segunda): 409 sem código, que pede para atualizar. Sem exemplo no contrato: texto do executor.</summary>
    public static ProblemaDeApiException ModulosMudaramEmOutraTela { get; } = new(409, Conflito, "Os módulos deste treinamento mudaram em outra tela. Atualize a página e tente de novo.");

    /// <summary>O treinamento mudou (publicado, encerrado ou excluído) entre a leitura e a gravação mais vezes do que o caso de uso tenta de novo. Sem exemplo no contrato: texto do executor.</summary>
    public static ProblemaDeApiException TreinamentoMudouEmOutraTela { get; } = new(409, Conflito, "Este treinamento mudou em outra tela. Atualize a página e tente de novo.");

    /// <summary>
    /// A recusa de uma regra do banco que nenhum caso de uso previu: 422 com a regra em <c>regraDoBanco</c>. Fora de <see cref="Todas"/> porque o contrato a mostra em exemplos sem código.
    /// </summary>
    public static ProblemaDeApiException OperacaoRecusada(string regraDoBanco) =>
        new(422, Recusada, "A operação foi recusada por uma regra do banco de dados.", null, new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });

    /// <summary>Todas as de texto fixo, para o teste contra o contrato conferir uma a uma.</summary>
    public static IReadOnlyList<MensagemDoTreinamento> Todas { get; } =
    [
        TreinamentoJaPublicado, CampoTravadoDoTreinamento, CampoTravadoDoModulo, ModulosTravados, EncerradoParaEditar, EncerradoParaEncerrar, EncerradoParaAPessoa, NaoEstaEmAndamento,
        NaoEOpcional, InscricaoPendente, SemTreinamentoDeOnboarding,
    ];
}
