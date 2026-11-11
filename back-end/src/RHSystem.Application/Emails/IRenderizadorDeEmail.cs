using System.Text.Json;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Emails;

/// <summary>A mensagem pronta e o que a auditoria grava quando ela sai (<c>ENVIOU_EMAIL</c>, ator Sistema), sem destinatário no texto.</summary>
public sealed record ModeloDeEmail(string Assunto, string Texto, string Html, AuditoriaArea AreaDeAuditoria, string DescricaoDeAuditoria, string RegistroAfetado);

/// <summary>
/// Um por tipo da lista fechada de e-mails (<c>contrato/CONVENCOES.md</c>, seção 12). Quem monta o link (a base é <c>App:UrlDoFront</c>) é o
/// renderizador. TODO valor interpolado no <c>Html</c> passa por <c>HtmlEncode</c>.
/// </summary>
public interface IRenderizadorDeEmail
{
    string Tipo { get; }

    ModeloDeEmail Renderizar(JsonElement carga, string destinatario);
}
