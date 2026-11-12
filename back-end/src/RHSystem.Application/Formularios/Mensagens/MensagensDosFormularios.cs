using RHSystem.Application.Comum;

namespace RHSystem.Application.Formularios.Mensagens;

/// <summary>
/// Um erro dos formulários como o contrato o mostra: o status, o <c>title</c>, o <c>detail</c> e o <c>codigo</c>. <see cref="ComoProblema"/> monta a resposta; <paramref name="regraDoBanco"/> vai na extensão <c>regraDoBanco</c> quando a recusa é de uma
/// regra do banco (o front a mostra como diagnóstico).
/// </summary>
public sealed record MensagemDoFormulario(int Status, string Titulo, string Detalhe, string Codigo)
{
    public ProblemaDeApiException ComoProblema(string? regraDoBanco = null) =>
        new(Status, Titulo, Detalhe, Codigo, regraDoBanco is null ? null : new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });
}

/// <summary>
/// Os textos das regras do banco (as mensagens dos triggers e os nomes das restrições de <c>V001</c>) que os formulários reconhecem. São a mensagem ORIGINAL do banco, sem acento; o contrato as devolve em <c>regraDoBanco</c>, e a aplicação usa as mesmas
/// quando confere antes de gravar (a recusa tem de sair igual, venha de onde vier).
/// </summary>
public static class RegrasDoBancoDosFormularios
{
    public const string PerguntaJaRespondidaAoAlterar = "Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.";

    public const string PerguntaJaRespondidaAoExcluir = "Pergunta ja respondida: nao e possivel exclui-la.";

    public const string FormularioInativoNaoAceitaRespostas = "Formulario inativo nao aceita respostas.";

    public const string FuncionarioInativoNaoRespondeFormularios = "Funcionario inativo nao pode responder formularios.";

    public const string PerguntaObrigatoriaSemResposta = "Pergunta obrigatoria: informe a resposta.";

    public const string AnonimoSemLink = "CHK_FORMULARIOS_ANONIMO_SEM_LINK";

    // ---- chaves únicas e estrangeiras, reconhecidas pelo NOME (a mensagem do MySQL traz o valor da chave e nunca sai)

    public const string ChaveDoEmailDoCandidato = "UQ_FORMULARIOS_CANDIDATOS_FORMULARIO_EMAIL";

    public const string ChaveDaParticipacao = "UQ_FORMULARIOS_PARTICIPACOES_PESSOA";

    public const string ChaveDaOrdemDaPergunta = "UQ_FORMULARIOS_PERGUNTAS_FORMULARIO_ORDEM";

    public const string ChaveDaOrdemDaAlternativa = "UQ_FORMULARIOS_ALTERNATIVAS_PERGUNTA_ORDEM";

    public const string ChaveDoDestinatario = "UQ_FORMULARIOS_DESTINATARIOS_DESTINO";

    public const string ChaveDoTokenDoLink = "UQ_FORMULARIOS_LINK_PUBLICO_CODIGO";

    public const string ChaveEstrangeiraDoAnonimato = "FK_FORMULARIOS_RESPOSTAS_FORMULARIOS_ANONIMO";
}

/// <summary>
/// Os erros com código da fatia 5 (formulários), <b>copiados dos exemplos do contrato</b> (<c>contrato/fontes/50-fatia-5-formularios.yaml</c>) e nunca reescritos: o front mostra o <c>detail</c> como vem. <c>MensagensDosFormulariosTests</c> lê o contrato e
/// confere cada um. Os títulos são os genéricos do contrato ("Operação recusada." no 422, "Conflito." no 409), exceto <c>CONFIRMACAO_NECESSARIA</c> ("Confirmação necessária."). O contrato não dá exemplo para alguns textos: o executor os escreveu
/// (decisão 10 do plano), e o teste tem a lista deles. O mesmo código tem textos diferentes por rota (<c>FORMULARIO_ENCERRADO</c>, <c>JA_RESPONDIDO</c>, <c>FUNCIONARIO_INATIVO</c>...): cada texto é uma propriedade.
/// </summary>
public static class MensagensDosFormularios
{
    private const string Recusada = "Operação recusada.";
    private const string Conflito = "Conflito.";

    /// <summary>O mínimo de pessoas de um formulário anônimo (regra 5 de "Formulários"): menos que isso identificaria quem respondeu.</summary>
    public const int MinimoDePessoasNoAnonimo = 5;

    // ---------------------------------------------------------------- 409

    public static MensagemDoFormulario AnonimatoFixoAposPublicar { get; } = new(409, Conflito, "Depois de publicado, o formulário não muda entre anônimo e identificado.", "ANONIMATO_FIXO_APOS_PUBLICAR");

    public static MensagemDoFormulario AnonimoPerguntasFixas { get; } = new(409, Conflito, "As perguntas de um formulário anônimo publicado não mudam.", "ANONIMO_PERGUNTAS_FIXAS");

    public static MensagemDoFormulario AnonimoSemNovosDestinatarios { get; } = new(409, Conflito, "Um formulário anônimo publicado não recebe pessoas novas.", "ANONIMO_SEM_NOVOS_DESTINATARIOS");

    public static MensagemDoFormulario ResultadoAnonimoIndisponivel { get; } = new(409, Conflito, "O resultado de um formulário anônimo só aparece depois que ele é encerrado.", "RESULTADO_ANONIMO_INDISPONIVEL");

    public static MensagemDoFormulario JaRespondidoPeloFuncionario { get; } = new(409, Conflito, "Você já respondeu este formulário.", "JA_RESPONDIDO");

    public static MensagemDoFormulario JaRespondidoPeloCandidato { get; } = new(409, Conflito, "Já recebemos uma candidatura com este e-mail.", "JA_RESPONDIDO");

    /// <summary>
    /// O 409 de <c>encerrarFormulario</c> e <c>excluirFormulario</c> sem <c>confirmar=true</c>: título próprio e <c>pendentes</c> no corpo (sempre presente, <c>null</c> no anônimo). Com nome: "63 pessoas ainda não responderam. Confirme para continuar." (o
    /// plural segue o número; nas avaliações o número é de avaliações que faltam e o texto do contrato continua falando de pessoas: o número certo vai em <c>pendentes</c>). Anônimo: o texto que não diz quantas.
    /// </summary>
    public static ProblemaDeApiException ConfirmacaoNecessaria(int? pendentes) =>
        new(
            409,
            "Confirmação necessária.",
            pendentes is { } n
                ? $"{n} {(n == 1 ? "pessoa ainda não respondeu" : "pessoas ainda não responderam")}. Confirme para continuar."
                : "Num formulário anônimo o sistema não diz quantas pessoas ainda não responderam. Confirme para continuar.",
            "CONFIRMACAO_NECESSARIA",
            new Dictionary<string, object?> { ["pendentes"] = pendentes });

    /// <summary>
    /// A corrida de duas telas mexendo nas perguntas na mesma posição (a chave única da ordem recusa a segunda): 409 sem código, que pede para atualizar. O contrato não tem exemplo (lacuna L:E): texto do executor.
    /// </summary>
    public static ProblemaDeApiException PerguntasMudaramEmOutraTela { get; } =
        new(409, Conflito, "As perguntas deste formulário mudaram em outra tela. Atualize a página e tente de novo.");

    /// <summary>
    /// A edição que perdeu a corrida mais vezes do que o caso de uso tenta de novo (outra aba muda o formulário sem parar): 409 sem código. Sem exemplo no contrato: texto do executor.
    /// </summary>
    public static ProblemaDeApiException FormularioMudouEmOutraTela { get; } =
        new(409, Conflito, "Este formulário mudou em outra tela. Atualize a página e tente de novo.");

    // ---------------------------------------------------------------- 422: o estado do formulário

    public static MensagemDoFormulario FormularioEncerradoParaEditar { get; } = new(422, Recusada, "O formulário está encerrado e não pode mais ser alterado.", "FORMULARIO_ENCERRADO");

    public static MensagemDoFormulario FormularioEncerradoParaResponder { get; } = new(422, Recusada, "Este formulário não está mais aceitando respostas.", "FORMULARIO_ENCERRADO");

    public static MensagemDoFormulario FormularioDeAdmissaoEncerrado { get; } = new(422, Recusada, "O formulário de admissão escolhido já foi encerrado. Escolha outro.", "FORMULARIO_ENCERRADO");

    public static MensagemDoFormulario FormularioNaoPublicadoParaLembrar { get; } = new(422, Recusada, "Só dá para lembrar quem falta em formulário publicado.", "FORMULARIO_NAO_PUBLICADO");

    /// <summary>Sem exemplo no contrato (o <c>encerrarFormulario</c> só diz "só publicado se encerra"): texto do executor.</summary>
    public static MensagemDoFormulario FormularioNaoPublicadoParaEncerrar { get; } = new(422, Recusada, "Só um formulário publicado pode ser encerrado.", "FORMULARIO_NAO_PUBLICADO");

    /// <summary>Sem exemplo no contrato (publicar o que já está publicado ou agendado): texto do executor.</summary>
    public static MensagemDoFormulario FormularioJaPublicado { get; } = new(422, Recusada, "Este formulário já está publicado ou agendado.", "FORMULARIO_JA_PUBLICADO");

    public static MensagemDoFormulario FormularioAindaNaoAberto { get; } = new(422, Recusada, "Este formulário ainda não está aberto. Tente de novo a partir da data de publicação.", "FORMULARIO_AINDA_NAO_ABERTO");

    /// <summary>
    /// Código NOVO, que o contrato não tem (decisão do plano para a lacuna L:E.6): o formulário com nome, publicado e já respondido, que recebe troca de tipo ou de sentido. O contrato só diz "só título e descrição mudam" e não escolhe o código.
    /// </summary>
    public static MensagemDoFormulario FormularioJaRespondido { get; } = new(422, Recusada, "Este formulário já tem respostas. Só o título e a descrição podem mudar.", "FORMULARIO_JA_RESPONDIDO");

    public static MensagemDoFormulario EmpresaNaoConfigurada { get; } = new(422, Recusada, "Este formulário ainda não está disponível. Fale com o RH.", "EMPRESA_NAO_CONFIGURADA");

    // ---------------------------------------------------------------- 422: perguntas

    public static MensagemDoFormulario PerguntaJaRespondidaParaEditar { get; } =
        new(422, Recusada, "Esta pergunta já foi respondida. Só dá para mudar se ela é obrigatória e a posição dela.", "PERGUNTA_JA_RESPONDIDA");

    /// <summary>Sem exemplo no contrato (no DELETE o texto de editar não serve): texto do executor.</summary>
    public static MensagemDoFormulario PerguntaJaRespondidaParaExcluir { get; } = new(422, Recusada, "Esta pergunta já foi respondida e não pode ser excluída.", "PERGUNTA_JA_RESPONDIDA");

    public static MensagemDoFormulario AnonimoSemArquivo { get; } = new(422, Recusada, "Formulário anônimo não pode ter pergunta de envio de arquivo.", "ANONIMO_SEM_ARQUIVO");

    // ---------------------------------------------------------------- 422: publicação e público

    public static MensagemDoFormulario AnonimoSemLink { get; } = new(422, Recusada, "Formulário anônimo não pode ser respondido por link público.", "ANONIMO_SEM_LINK");

    public static MensagemDoFormulario LinkSoParaEntrevista { get; } = new(422, Recusada, "Só a entrevista automática pode ser respondida por link. Escolha setores ou pessoas.", "LINK_SO_PARA_ENTREVISTA");

    /// <summary>O número é montado ("Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para 4.").</summary>
    public static MensagemDoFormulario AnonimoPequeno(int pessoas) =>
        new(422, Recusada, $"Um formulário anônimo precisa ir para pelo menos {MinimoDePessoasNoAnonimo} pessoas. Este vai para {pessoas}.", "FORMULARIO_ANONIMO_PUBLICO_PEQUENO");

    public static MensagemDoFormulario VerificacaoComBloqueio { get; } = new(422, Recusada, "O formulário tem itens a corrigir antes de publicar. Veja a etapa Verificação.", "VERIFICACAO_COM_BLOQUEIO");

    public static MensagemDoFormulario PessoaInativaNoPublico { get; } = new(422, Recusada, "Uma das pessoas escolhidas está inativa e não pode receber o formulário.", "FUNCIONARIO_INATIVO");

    /// <summary>Sem exemplo no contrato (quem responde está inativo; o trigger recusa com <c>regraDoBanco</c>): texto do executor.</summary>
    public static MensagemDoFormulario PessoaInativaParaResponder { get; } = new(422, Recusada, "Uma pessoa inativa não pode responder formulários.", "FUNCIONARIO_INATIVO");

    public static MensagemDoFormulario FormularioPorLinkNaLista { get; } = new(422, Recusada, "Este formulário é respondido por link. Veja a lista de candidatos.", "FORMULARIO_POR_LINK");

    /// <summary>Sem exemplo no contrato (lembrete em formulário por link): texto do executor.</summary>
    public static MensagemDoFormulario FormularioPorLinkNoLembrete { get; } = new(422, Recusada, "Este formulário é respondido por link: não há pessoas do cadastro para lembrar.", "FORMULARIO_POR_LINK");

    /// <summary>Sem exemplo no contrato ("use a lista de funcionários"): texto do executor.</summary>
    public static MensagemDoFormulario FormularioSemLink { get; } = new(422, Recusada, "Este formulário não é respondido por link. Veja a lista de funcionários.", "FORMULARIO_SEM_LINK");

    public static MensagemDoFormulario FormularioNaoEPorPessoas { get; } = new(422, Recusada, "Só dá para incluir pessoas em formulário feito para pessoas escolhidas.", "FORMULARIO_NAO_E_POR_PESSOAS");

    /// <summary>Sem exemplo no contrato: texto do executor.</summary>
    public static MensagemDoFormulario FormularioNaoEAvaliacao { get; } = new(422, Recusada, "Este formulário não é uma avaliação.", "FORMULARIO_NAO_E_AVALIACAO");

    // ---------------------------------------------------------------- 422: anonimato, sugestão e resultado

    public static MensagemDoFormulario FormularioAnonimo { get; } = new(422, Recusada, "Este formulário é anônimo: o sistema não guarda quem respondeu o quê.", "FORMULARIO_ANONIMO");

    public static MensagemDoFormulario SugestaoNoLembrete { get; } =
        new(422, Recusada, "A caixa de sugestões é sempre aberta e ninguém é obrigado a responder, então não há quem lembrar.", "FORMULARIO_SUGESTAO");

    /// <summary>Sem exemplo no contrato ("ver minhas respostas" na Sugestão, que a tela nem oferece): texto do executor.</summary>
    public static MensagemDoFormulario SugestaoNasMinhasRespostas { get; } = new(422, Recusada, "Na caixa de sugestões não há respostas suas para rever: cada envio é uma sugestão nova.", "FORMULARIO_SUGESTAO");

    public static MensagemDoFormulario ResultadoSemRespostasNaExportacao { get; } =
        new(422, Recusada, "Nenhuma pergunta deste formulário anônimo teve 3 respostas ou mais, então não há resultado para exportar.", "RESULTADO_SEM_RESPOSTAS_SUFICIENTES");

    public static MensagemDoFormulario ResultadoSemRespostasDoAvaliado { get; } =
        new(422, Recusada, "Esta pessoa recebeu menos de 3 avaliações, então o resultado dela não aparece.", "RESULTADO_SEM_RESPOSTAS_SUFICIENTES");

    public static MensagemDoFormulario AvaliadoInvalido { get; } = new(422, Recusada, "Você não deve avaliar esta pessoa neste formulário.", "AVALIADO_INVALIDO");

    /// <summary>
    /// A recusa de uma regra do banco que o contrato não nomeia (a pergunta obrigatória sem resposta, que a aplicação confere antes): sem <c>codigo</c>, o texto genérico e a mensagem original em <c>regraDoBanco</c>. Fica fora de
    /// <see cref="Todas"/> porque o contrato a mostra em exemplos sem código de várias fatias.
    /// </summary>
    public static ProblemaDeApiException OperacaoRecusada(string regraDoBanco) =>
        new(422, Recusada, "A operação foi recusada por uma regra do banco de dados.", null, new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });

    /// <summary>Todas as de texto fixo, para o teste contra o contrato conferir uma a uma (o <c>AnonimoPequeno</c> entra com o exemplo do contrato: 4 pessoas).</summary>
    public static IReadOnlyList<MensagemDoFormulario> Todas { get; } =
    [
        AnonimatoFixoAposPublicar, AnonimoPerguntasFixas, AnonimoSemNovosDestinatarios, ResultadoAnonimoIndisponivel, JaRespondidoPeloFuncionario, JaRespondidoPeloCandidato,
        FormularioEncerradoParaEditar, FormularioEncerradoParaResponder, FormularioDeAdmissaoEncerrado, FormularioNaoPublicadoParaLembrar, FormularioNaoPublicadoParaEncerrar, FormularioJaPublicado, FormularioAindaNaoAberto,
        FormularioJaRespondido, EmpresaNaoConfigurada, PerguntaJaRespondidaParaEditar, PerguntaJaRespondidaParaExcluir, AnonimoSemArquivo, AnonimoSemLink, LinkSoParaEntrevista, AnonimoPequeno(4), VerificacaoComBloqueio,
        PessoaInativaNoPublico, PessoaInativaParaResponder, FormularioPorLinkNaLista, FormularioPorLinkNoLembrete, FormularioSemLink, FormularioNaoEPorPessoas, FormularioNaoEAvaliacao, FormularioAnonimo, SugestaoNoLembrete,
        SugestaoNasMinhasRespostas, ResultadoSemRespostasNaExportacao, ResultadoSemRespostasDoAvaliado, AvaliadoInvalido,
    ];
}
