using System.Security.Cryptography;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>O que a matriz de edição recusa (cada valor vira o erro do contrato com o mesmo nome).</summary>
public enum RecusaDeEdicao
{
    FormularioEncerrado,
    AnonimatoFixoAposPublicar,
    FormularioJaRespondido,
    LinkSoParaEntrevista,
}

/// <summary>O que a combinação de alvo, anonimato e tipo recusa ao publicar ou guardar a publicação.</summary>
public enum RecusaDePublicacao
{
    AnonimoSemLink,
    LinkSoParaEntrevista,
}

/// <summary>
/// A matriz de edição dos detalhes (<c>editarFormulario</c>): rascunho e agendado mudam tudo; encerrado nada; publicado depende do anonimato. <b>Anônimo publicado</b>: só título e descrição, SEMPRE, sem olhar se há resposta (o mesmo veredito com e sem resposta: nenhuma diferença
/// observável). <b>Com nome publicado</b>: o anonimato nunca muda; sem resposta o resto muda (o formulário por link não sai de entrevista); com resposta só título e descrição mudam.
/// </summary>
public static class MatrizDeEdicao
{
    /// <param name="mudaAnonimo">O corpo traz um <c>anonimo</c> diferente do atual.</param>
    /// <param name="mudaTipoOuSentido">O corpo traz um <c>tipo</c> ou <c>sentidoAvaliacao</c> diferente do atual.</param>
    /// <param name="alvoAtual">O alvo guardado (<c>null</c> se ainda não definido).</param>
    /// <param name="tipoPedido">O tipo que o corpo pede.</param>
    public static RecusaDeEdicao? Conferir(SituacaoDoFormulario situacao, bool anonimoAtual, bool temResposta, bool mudaAnonimo, bool mudaTipoOuSentido, AlvoDoFormulario? alvoAtual, TipoDeFormulario tipoPedido)
    {
        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            return RecusaDeEdicao.FormularioEncerrado;
        }

        if (situacao != SituacaoDoFormulario.Publicado)
        {
            return null;
        }

        if (mudaAnonimo)
        {
            return RecusaDeEdicao.AnonimatoFixoAposPublicar;
        }

        if (anonimoAtual)
        {
            return mudaTipoOuSentido ? RecusaDeEdicao.AnonimatoFixoAposPublicar : null; // nunca olha temResposta
        }

        if (temResposta && mudaTipoOuSentido)
        {
            return RecusaDeEdicao.FormularioJaRespondido;
        }

        return alvoAtual == AlvoDoFormulario.Link && tipoPedido != TipoDeFormulario.Entrevista ? RecusaDeEdicao.LinkSoParaEntrevista : null;
    }
}

/// <summary>Anônimo não vai por link público (os candidatos não são pessoas do cadastro) e só a entrevista automática vai por link. A primeira recusa é a do anonimato.</summary>
public static class RegrasDaPublicacao
{
    public static RecusaDePublicacao? Recusa(TipoDeFormulario tipo, bool anonimo, AlvoDoFormulario alvo)
    {
        if (anonimo && alvo == AlvoDoFormulario.Link)
        {
            return RecusaDePublicacao.AnonimoSemLink;
        }

        return alvo == AlvoDoFormulario.Link && tipo != TipoDeFormulario.Entrevista ? RecusaDePublicacao.LinkSoParaEntrevista : null;
    }
}

/// <summary>O código do link público: 32 bytes aleatórios do gerador seguro em base64url, sem preenchimento (43 caracteres). Nunca sequencial e nunca derivado do formulário.</summary>
public static class TokenDoLink
{
    private const int TamanhoMinimo = 22;
    private const int TamanhoMaximo = 64;

    public static string Novo() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>O formato do contrato (<c>^[A-Za-z0-9_-]{22,64}$</c>): token fora dele é 404, igual ao inexistente.</summary>
    public static bool Valido(string? texto) =>
        texto is { Length: >= TamanhoMinimo and <= TamanhoMaximo } && texto.All(c => c is (>= 'A' and <= 'Z') or (>= 'a' and <= 'z') or (>= '0' and <= '9') or '_' or '-');
}
