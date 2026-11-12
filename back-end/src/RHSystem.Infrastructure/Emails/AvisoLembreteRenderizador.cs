using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de lembrete de um aviso que a pessoa ainda não abriu (<c>AVISO_LEMBRETE</c>, o botão "Lembrar quem não viu" do RH). Mesmo cuidado do <c>AVISO_PUBLICADO</c>: o conteúdo é texto do usuário e no Html todo valor passa por <c>HtmlEncode</c>;
/// a auditoria do envio nunca leva o destinatário.
/// </summary>
public sealed class AvisoLembreteRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "AVISO_LEMBRETE";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var titulo = Texto(carga, "titulo") ?? "Aviso";
        var previa = Texto(carga, "previa");

        var linhas = new List<string> { $"Você ainda não abriu um aviso do RH no RH-System: {titulo}" };
        if (!string.IsNullOrWhiteSpace(previa))
        {
            linhas.Add(previa);
        }

        linhas.Add("Abra o RH-System para ler o aviso inteiro.");
        var texto = $"Olá, {nome}!\n\n{string.Join("\n\n", linhas)}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p>{string.Concat(linhas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>"))}";
        return new ModeloDeEmail($"Lembrete: {titulo}", texto, html, AuditoriaArea.Avisos, "E-mail de lembrete do aviso enviado", $"Colaborador {nome}");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
