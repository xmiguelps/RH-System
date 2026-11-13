using RHSystem.Application.Formularios.Responder;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>Um envio de formulário COM NOME: a pessoa, o avaliado (nas avaliações) e o instante em que gravou (o mesmo em todas as linhas do envio).</summary>
public sealed record EnvioLido(int PessoaId, int? AvaliadoId, DateTime RespondidoEmUtc);

/// <summary>Uma pessoa como a tela a mostra, com o setor para o filtro "Todos os setores".</summary>
public sealed record PessoaDaLista(int Id, string Nome, bool Ativo, string? Cargo, string? Setor, int? SetorId);

/// <summary>O arquivo de uma resposta: o caminho (que nunca sai da API) e o nome que a pessoa deu.</summary>
public sealed record ArquivoDeRespostaLido(string Caminho, string Nome);

/// <summary>Um candidato como a leitura do RH o traz: o caminho do currículo fica aqui dentro (só o caso de uso do download o usa para gerar o link) e nunca sai na resposta.</summary>
public sealed record CandidatoLido(int Id, string Nome, string Email, string? Telefone, string? CaminhoDoCurriculo, DateTime CadastradoEmUtc);

/// <summary>A leitura do RH sobre as respostas de um formulário. Nenhuma rota daqui devolve o id de uma resposta anônima nem a ordem de gravação dela: o anônimo só tem a CONTAGEM de participações.</summary>
public interface IConsultaDeAcompanhamento
{
    /// <summary>Os envios COM NOME do formulário (distintos por pessoa, avaliado e instante). Não olha respostas anônimas nem de candidato.</summary>
    Task<IReadOnlyList<EnvioLido>> EnviosAsync(int formularioId, CancellationToken ct);

    /// <summary>Quantas participações o formulário anônimo tem (uma por pessoa, e por avaliado nas avaliações).</summary>
    Task<int> ParticipacoesAsync(int formularioId, CancellationToken ct);

    /// <summary>As pessoas entre <paramref name="ids"/> que existem (inclusive inativas), com cargo, setor e o número do setor.</summary>
    Task<IReadOnlyList<PessoaDaLista>> PessoasAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>As linhas de UM envio da pessoa (para este avaliado, nas avaliações; no instante dado na Sugestão, <c>null</c> nos outros tipos), em ordem de pergunta.</summary>
    Task<IReadOnlyList<RespostaLida>> DoEnvioAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime? instanteUtc, CancellationToken ct);

    /// <summary>Uma página dos candidatos do formulário, os mais recentes primeiro (empate pelo número, o maior primeiro), com o total já filtrado pela <paramref name="busca"/> (nome ou e-mail, sem diferenciar caixa nem acento; <c>null</c> = tudo).</summary>
    Task<(IReadOnlyList<CandidatoLido> Itens, int Total)> CandidatosAsync(int formularioId, string? busca, int pular, int tamanho, CancellationToken ct);

    /// <summary>O candidato <paramref name="candidatoId"/> DESTE formulário, ou <c>null</c> (não existe ou é de outro formulário).</summary>
    Task<CandidatoLido?> CandidatoAsync(int formularioId, int candidatoId, CancellationToken ct);

    /// <summary>As linhas de resposta do candidato, em ordem de pergunta.</summary>
    Task<IReadOnlyList<RespostaLida>> DoCandidatoAsync(int formularioId, int candidatoId, CancellationToken ct);

    /// <summary>O arquivo da resposta <paramref name="respostaId"/> DESTE formulário, ou <c>null</c> (não existe, é de outro formulário, é anônima ou não tem arquivo).</summary>
    Task<ArquivoDeRespostaLido?> ArquivoAsync(int formularioId, int respostaId, CancellationToken ct);
}
