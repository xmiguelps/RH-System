using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de um aviso publicado (<c>AVISO_PUBLICADO</c>): o título e a prévia em texto puro, e o convite para abrir o aviso inteiro no RH-System. O conteúdo é texto do usuário: no Html todo valor passa por <c>HtmlEncode</c> (nada de HTML
/// ou markdown interpretado). A auditoria do envio (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário.
/// </summary>
public sealed class AvisoPublicadoRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "AVISO_PUBLICADO";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var titulo = Texto(carga, "titulo") ?? "Aviso";
        var previa = Texto(carga, "previa");

        var linhas = new List<string> { $"O RH publicou um novo aviso no RH-System: {titulo}" };
        if (!string.IsNullOrWhiteSpace(previa))
        {
            linhas.Add(previa);
        }

        linhas.Add("Abra o RH-System para ler o aviso inteiro.");
        var texto = $"Olá, {nome}!\n\n{string.Join("\n\n", linhas)}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p>{string.Concat(linhas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>"))}";
        return new ModeloDeEmail($"Novo aviso: {titulo}", texto, html, AuditoriaArea.Avisos, "E-mail do aviso publicado enviado", $"Colaborador {nome}");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
