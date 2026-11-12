using RHSystem.Application.Colaboradores;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// Um formulário que a pessoa deve responder ou já respondeu, como o banco o guarda, com a posição DELA: <c>MeusPares</c> são os pares que ela deve responder agora (calculados na hora; no anônimo publicado, os da fotografia) e <c>Feitos</c> os que já fez (resposta com nome, ou participação
/// no anônimo). <c>UltimaRespostaEmUtc</c> é a mais recente (nulo no anônimo, que não guarda data). <c>TiposDasPerguntas</c> dá o total e os minutos estimados.
/// </summary>
public sealed record CartaoDoFormulario(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? Sentido,
    bool Anonimo,
    bool Ativo,
    DateTime? PublicadoEmUtc,
    DateTime? EncerradoEmUtc,
    DateOnly? Prazo,
    IReadOnlyList<TipoDePergunta> TiposDasPerguntas,
    IReadOnlyList<ParDeResposta> MeusPares,
    IReadOnlyList<ParDeResposta> Feitos,
    DateTime? UltimaRespostaEmUtc);

/// <summary>O que "responder" precisa saber da pessoa logada diante de um formulário: os pares que ela deve responder e os que já fez.</summary>
public sealed record PosicaoDaPessoaNoFormulario(IReadOnlyList<ParDeResposta> MeusPares, IReadOnlyList<ParDeResposta> Feitos);

/// <summary>
/// A leitura dos formulários de quem está logado (nunca de outra pessoa). Só os formulários com público por pessoas ou setores (o de link nunca aparece aqui) e que estão PUBLICADOS em que a pessoa deve responder, mais TODOS os que ela já respondeu.
/// </summary>
public interface IConsultaDeMeusFormularios
{
    /// <summary>
    /// Os formulários da pessoa. <paramref name="soParaResponder"/> traz só admissão e avaliação publicados que ela ainda deve responder, sem os já respondidos (a bolinha do menu: poucos formulários e nenhuma lista carregada).
    /// </summary>
    Task<IReadOnlyList<CartaoDoFormulario>> CartoesAsync(int funcionarioId, DateOnly hoje, DateTime agoraUtc, bool soParaResponder, CancellationToken ct);

    /// <summary>A posição da pessoa num formulário: os pares que ela deve responder e os que já fez.</summary>
    Task<PosicaoDaPessoaNoFormulario> PosicaoAsync(int formularioId, int funcionarioId, DateOnly hoje, CancellationToken ct);

    /// <summary>A identificação curta das pessoas (os avaliados), na mesma ordem dos ids que existem.</summary>
    Task<IReadOnlyList<FuncionarioResumo>> ResumosAsync(IReadOnlyList<int> ids, CancellationToken ct);
}
