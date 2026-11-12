using System.Globalization;
using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail da resposta do RH à contestação (<c>ADVERTENCIA_RESPONDIDA</c>), à pessoa que contestou: diz se a advertência foi mantida ou acatada (cancelada) e traz o texto que o RH escreveu (a resposta NÃO fica guardada no banco: é este e-mail e a auditoria). Todo valor no Html passa por
/// <c>HtmlEncode</c>; a auditoria do envio (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário.
/// </summary>
public sealed class AdvertenciaRespondidaRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "ADVERTENCIA_RESPONDIDA";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var resposta = Texto(carga, "resposta") ?? string.Empty;
        var dia = DateOnly.TryParseExact(Texto(carga, "data"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "data não informada";
        var acatada = string.Equals(Texto(carga, "decisao"), "ACATAR", StringComparison.Ordinal);

        var abertura = acatada
            ? $"O RH analisou a sua contestação da advertência de {dia} e acatou: a advertência foi cancelada."
            : $"O RH analisou a sua contestação da advertência de {dia} e manteve a advertência.";
        var texto = $"Olá, {nome}!\n\n{abertura}\n\nResposta do RH:\n{resposta}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p><p>{WebUtility.HtmlEncode(abertura)}</p><p>Resposta do RH:</p><p>{WebUtility.HtmlEncode(resposta)}</p>";
        var assunto = acatada ? "Sua contestação foi acatada" : "Resposta à sua contestação de advertência";
        return new ModeloDeEmail(assunto, texto, html, AuditoriaArea.Advertencias, "E-mail da resposta à contestação enviado", $"Colaborador {nome}");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
