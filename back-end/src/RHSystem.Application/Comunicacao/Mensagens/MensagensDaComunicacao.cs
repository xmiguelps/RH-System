using RHSystem.Application.Comum;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Mensagens;

/// <summary>
/// Um erro da comunicação como o contrato o mostra: o status, o <c>title</c>, o <c>detail</c> e o <c>codigo</c>. <see cref="ComoProblema"/> monta a resposta; <paramref name="regraDoBanco"/> vai na extensão <c>regraDoBanco</c> quando
/// a recusa é de uma regra do banco (o front a mostra como diagnóstico).
/// </summary>
public sealed record MensagemDaComunicacao(int Status, string Titulo, string Detalhe, string Codigo)
{
    public ProblemaDeApiException ComoProblema(string? regraDoBanco = null) =>
        new(Status, Titulo, Detalhe, Codigo, regraDoBanco is null ? null : new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });
}

/// <summary>
/// Os textos das regras do banco (as mensagens dos triggers de <c>V002</c>) que a comunicação reconhece. São a mensagem ORIGINAL do banco, sem acento; o contrato as devolve em <c>regraDoBanco</c>, e a aplicação usa as mesmas quando confere
/// antes de gravar (a recusa tem de sair igual, venha de onde vier).
/// </summary>
public static class RegrasDoBancoDaComunicacao
{
    public const string HoleriteAnteriorAAdmissao = "A competencia do holerite e anterior a admissao.";

    public const string HoleritePosteriorAoDesligamento = "A competencia do holerite e posterior ao desligamento.";

    public const string HoleriteCompetenciaFutura = "Nao e possivel cadastrar holerite de competencia futura.";

    public const string AdvertenciaDeInativo = "Nao e possivel registrar advertencia para funcionario inativo.";

    public const string GestorAdvertePorSiMesmo = "O gestor nao pode advertir a si mesmo.";

    public const string AvisoComExpiracaoAnterior = "A expiracao do aviso nao pode ser anterior a publicacao.";
}

/// <summary>
/// Os erros com código da fatia 4 (comunicação e documentos), <b>copiados dos exemplos do contrato</b> (<c>contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml</c>) e nunca reescritos: o front mostra o <c>detail</c> como vem.
/// <c>MensagensDaComunicacaoTests</c> lê o contrato e confere cada um. Os títulos são os genéricos do contrato ("Operação recusada." no 422, "Conflito." no 409). O mesmo código tem textos diferentes por rota (<c>AVISO_NAO_ESTA_ATIVO</c> ao encerrar
/// e ao lembrar) e <c>HOLERITE_JA_EXISTE</c> leva o tipo e o mês por extenso: esses são montados. <c>ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO</c> não tem exemplo no contrato: o texto é escolha do plano da fatia 4 (decisão 11).
/// </summary>
public static class MensagensDaComunicacao
{
    private const string Recusada = "Operação recusada.";
    private const string Conflito = "Conflito.";

    private static readonly string[] Meses = ["janeiro", "fevereiro", "março", "abril", "maio", "junho", "julho", "agosto", "setembro", "outubro", "novembro", "dezembro"];

    // ---------------------------------------------------------------- avisos (422)

    public static MensagemDaComunicacao AvisoPublicacaoNaoEditavel { get; } = new(422, Recusada, "Este aviso já foi publicado; a data de publicação não pode mudar.", "AVISO_PUBLICACAO_NAO_EDITAVEL");

    public static MensagemDaComunicacao AvisoNaoPodeSerExcluido { get; } =
        new(422, Recusada, "Só é possível excluir um aviso que ainda não foi publicado. Para tirar um aviso do ar, encerre-o.", "AVISO_NAO_PODE_SER_EXCLUIDO");

    public static MensagemDaComunicacao AvisoJaPublicado { get; } = new(422, Recusada, "Este aviso já foi publicado.", "AVISO_JA_PUBLICADO");

    public static MensagemDaComunicacao AvisoNaoEstaAtivoParaEncerrar { get; } = new(422, Recusada, "Só um aviso ativo pode ser encerrado.", "AVISO_NAO_ESTA_ATIVO");

    public static MensagemDaComunicacao AvisoNaoEstaAtivoParaLembrar { get; } = new(422, Recusada, "Só é possível lembrar sobre um aviso ativo.", "AVISO_NAO_ESTA_ATIVO");

    public static MensagemDaComunicacao AvisoAindaNaoPublicado { get; } = new(422, Recusada, "Este aviso ainda não foi publicado.", "AVISO_AINDA_NAO_PUBLICADO");

    // ---------------------------------------------------------------- holerites

    public static MensagemDaComunicacao HoleriteVersaoDesatualizada { get; } = new(409, Conflito, "Este holerite já foi substituído por uma versão mais nova. Atualize a lista.", "HOLERITE_VERSAO_DESATUALIZADA");

    public static MensagemDaComunicacao EmpresaSemDiaDePagamento { get; } =
        new(422, Recusada, "A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data.", "EMPRESA_NAO_CONFIGURADA");

    public static MensagemDaComunicacao HoleriteCompetenciaFutura { get; } = new(422, Recusada, "Só o holerite de férias pode ter competência futura.", "HOLERITE_COMPETENCIA_FUTURA");

    public static MensagemDaComunicacao HoleriteCompetenciaAnteriorAAdmissao { get; } = new(422, Recusada, "A competência do holerite é anterior à admissão da pessoa.", "HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO");

    public static MensagemDaComunicacao HoleriteCompetenciaPosteriorAoDesligamento { get; } =
        new(422, Recusada, "A competência do holerite é posterior ao desligamento da pessoa.", "HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO");

    /// <summary>O 409 do envio individual: o texto traz o tipo e o mês por extenso e sugere a nova versão (<c>Já existe um holerite mensal de outubro de 2026 para esta pessoa. Para corrigir, envie uma nova versão.</c>).</summary>
    public static MensagemDaComunicacao HoleriteJaExiste(TipoDeHolerite tipo, int mes, int ano) =>
        new(409, Conflito, $"{HoleriteJaExisteNoLote(tipo, mes, ano)} Para corrigir, envie uma nova versão.", "HOLERITE_JA_EXISTE");

    /// <summary>O texto do mesmo erro no resultado do lote (<c>mensagemDoErro</c>), sem a sugestão de nova versão (o lote não cria versão).</summary>
    public static string HoleriteJaExisteNoLote(TipoDeHolerite tipo, int mes, int ano) => $"Já existe um holerite {TextoDaCompetencia(tipo, mes, ano)} para esta pessoa.";

    /// <summary>A competência por extenso, depois da palavra "holerite": <c>mensal de outubro de 2026</c>, <c>do 13º salário de dezembro de 2026</c>, <c>de férias de março de 2027</c>.</summary>
    public static string TextoDaCompetencia(TipoDeHolerite tipo, int mes, int ano) => $"{NomeDoTipo(tipo)} de {Meses[mes - 1]} de {ano}";

    // ---------------------------------------------------------------- erros de item do lote (só no resultado, sem texto no contrato)

    public const string CodigoDoArquivoInvalidoNoLote = "ARQUIVO_INVALIDO";

    public const string CodigoDaPessoaNaoEncontradaNoLote = "FUNCIONARIO_NAO_ENCONTRADO";

    public const string TextoDaPessoaNaoEncontradaNoLote = "Esta pessoa não foi encontrada.";

    // ---------------------------------------------------------------- advertências

    public static MensagemDaComunicacao AdvertenciaNaoEstaContestada { get; } = new(422, Recusada, "Só uma advertência contestada pode receber resposta.", "ADVERTENCIA_NAO_ESTA_CONTESTADA");

    public static MensagemDaComunicacao AdvertenciaDeInativo { get; } = new(422, Recusada, "Não é possível registrar advertência para uma pessoa inativa.", "FUNCIONARIO_INATIVO");

    public static MensagemDaComunicacao AdvertenciaSemGestor { get; } = new(422, Recusada, "Esta pessoa não tem gestor ativo. Escolha \"RH\" em \"Aplicada por\".", "ADVERTENCIA_SEM_GESTOR");

    public static MensagemDaComunicacao AdvertenciaGestorAdvertePorSiMesmo { get; } = new(422, Recusada, "O gestor não pode aplicar uma advertência a si mesmo.", "ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO");

    public static MensagemDaComunicacao AdvertenciaNaoEstaPendente { get; } = new(422, Recusada, "Esta advertência já teve ciência ou contestação.", "ADVERTENCIA_NAO_ESTA_PENDENTE");

    /// <summary>
    /// A recusa de uma regra do banco que o contrato não nomeia (por exemplo a expiração antes da publicação, que a aplicação confere antes): sem <c>codigo</c>, o texto genérico e a mensagem original em <c>regraDoBanco</c>. Fica fora de
    /// <see cref="Todas"/> porque o contrato a mostra em exemplos sem código de várias fatias.
    /// </summary>
    public static ProblemaDeApiException OperacaoRecusada(string regraDoBanco) =>
        new(422, Recusada, "A operação foi recusada por uma regra do banco de dados.", null, new Dictionary<string, object?> { ["regraDoBanco"] = regraDoBanco });

    /// <summary>Todas, para o teste contra o contrato conferir uma a uma (o <c>HoleriteJaExiste</c> entra com o exemplo do contrato: mensal, outubro de 2026).</summary>
    public static IReadOnlyList<MensagemDaComunicacao> Todas { get; } =
    [
        AvisoPublicacaoNaoEditavel, AvisoNaoPodeSerExcluido, AvisoJaPublicado, AvisoNaoEstaAtivoParaEncerrar, AvisoNaoEstaAtivoParaLembrar, AvisoAindaNaoPublicado, HoleriteVersaoDesatualizada, EmpresaSemDiaDePagamento,
        HoleriteCompetenciaFutura, HoleriteCompetenciaAnteriorAAdmissao, HoleriteCompetenciaPosteriorAoDesligamento, HoleriteJaExiste(TipoDeHolerite.Mensal, 10, 2026), AdvertenciaNaoEstaContestada, AdvertenciaDeInativo,
        AdvertenciaSemGestor, AdvertenciaGestorAdvertePorSiMesmo, AdvertenciaNaoEstaPendente,
    ];

    private static string NomeDoTipo(TipoDeHolerite tipo) => tipo switch
    {
        TipoDeHolerite.DecimoTerceiro => "do 13º salário",
        TipoDeHolerite.Ferias => "de férias",
        _ => "mensal",
    };
}
