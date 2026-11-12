using RHSystem.Application.Formularios.Responder;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>Um envio de formulário COM NOME: a pessoa, o avaliado (nas avaliações) e o instante em que gravou (o mesmo em todas as linhas do envio).</summary>
public sealed record EnvioLido(int PessoaId, int? AvaliadoId, DateTime RespondidoEmUtc);

/// <summary>Uma pessoa como a tela a mostra, com o setor para o filtro "Todos os setores".</summary>
public sealed record PessoaDaLista(int Id, string Nome, bool Ativo, string? Cargo, string? Setor, int? SetorId);

/// <summary>O arquivo de uma resposta: o caminho (que nunca sai da API) e o nome que a pessoa deu.</summary>
public sealed record ArquivoDeRespostaLido(string Caminho, string Nome);

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

    /// <summary>O arquivo da resposta <paramref name="respostaId"/> DESTE formulário, ou <c>null</c> (não existe, é de outro formulário, é anônima ou não tem arquivo).</summary>
    Task<ArquivoDeRespostaLido?> ArquivoAsync(int formularioId, int respostaId, CancellationToken ct);
}
