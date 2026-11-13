using System.Text.Json.Serialization;
using RHSystem.Application.Formularios.Destinatarios;

namespace RHSystem.Application.Formularios.Lembretes;

/// <summary>O corpo de <c>lembrarQuemFaltaFormulario</c>: as pessoas a lembrar (de 0 a 200; vazio = todas que faltam).</summary>
public sealed record FormularioLembreteComando([property: JsonRequired] IReadOnlyList<int> FuncionarioIds);

/// <summary>
/// A resposta do lembrete manual. Com nome, <c>Enviados</c> e <c>Ignorados</c> são os números. No anônimo a resposta só diz que o lembrete saiu: <c>Anonimo</c> verdadeiro e os dois NULOS, sempre iguais (com muita gente, com pouca, com ninguém ou se o de hoje já tinha saído).
/// </summary>
public sealed record FormularioLembreteResultado(bool Anonimo, int? Enviados, int? Ignorados);

/// <summary>Quem pode receber o lembrete: pessoa ativa, com e-mail corporativo.</summary>
public sealed record PessoaParaLembrar(int Id, string Nome, string Email, int? SetorId);

/// <summary>
/// O que o lembrete precisa ler do banco, SÓ para montar e enviar (nunca devolvido por uma rota): quem já respondeu ou participou (a lista de quem falta, inclusive no anônimo, onde só o envio sigiloso a usa) e quem pode receber e-mail. Pessoa sem e-mail corporativo ou inativa não recebe.
/// </summary>
public interface IConsultaDeLembretes
{
    /// <summary>Os pares que já responderam (com nome) ou participaram (anônimo).</summary>
    Task<IReadOnlyList<ParDeResposta>> JaResponderamAsync(int formularioId, bool anonimo, CancellationToken ct);

    /// <summary>As pessoas entre <paramref name="ids"/> que estão ativas e têm e-mail corporativo.</summary>
    Task<IReadOnlyList<PessoaParaLembrar>> PodemReceberAsync(IReadOnlyList<int> ids, CancellationToken ct);
}
