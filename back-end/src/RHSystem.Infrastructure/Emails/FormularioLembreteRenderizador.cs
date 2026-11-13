using System.Globalization;
using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Application.Formularios.Lembretes;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de lembrete de um formulário que a pessoa ainda não respondeu (<c>FORMULARIO_LEMBRETE</c>: o botão "Lembrar quem falta" do RH e do gestor e o automático 7 dias antes do prazo). O título é texto do usuário, então no Html todo valor passa por <c>HtmlEncode</c>; a auditoria do envio só diz o
/// formulário, NUNCA a pessoa (e a linha SIGILOSA do formulário anônimo nem chega a ser auditada: é apagada ao enviar).
/// </summary>
public sealed class FormularioLembreteRenderizador : IRenderizadorDeEmail
{
    public string Tipo => RegrasDoLembrete.TipoDoEmail;

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var titulo = Texto(carga, "titulo") ?? "Formulário";
        var linhas = new List<string> { $"Você ainda não respondeu o formulário \"{titulo}\" no RH-System." };
        if (Texto(carga, "prazo") is { } prazo && DateOnly.TryParseExact(prazo, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia))
        {
            linhas.Add($"O prazo para responder é {dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)}.");
        }

        linhas.Add("Abra o RH-System para responder.");
        var texto = $"Olá, {nome}!\n\n{string.Join("\n\n", linhas)}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p>{string.Concat(linhas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>"))}";
        return new ModeloDeEmail($"Lembrete: {titulo}", texto, html, AuditoriaArea.Formularios, "E-mail de lembrete do formulário enviado", $"Formulário \"{titulo}\"");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
