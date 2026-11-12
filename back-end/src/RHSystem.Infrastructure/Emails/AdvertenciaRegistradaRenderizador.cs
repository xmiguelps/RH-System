using System.Globalization;
using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de uma advertência registrada (<c>ADVERTENCIA_REGISTRADA</c>): vai SEMPRE à pessoa e, quando o gestor registra, em cópia a cada pessoa do RH (<c>paraRh</c>). Só diz que existe uma advertência (o tipo e o dia do ocorrido): o motivo e a descrição ficam no RH-System, onde a
/// pessoa confirma a ciência ou contesta. Todo valor no Html passa por <c>HtmlEncode</c>; a auditoria do envio (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário.
/// </summary>
public sealed class AdvertenciaRegistradaRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "ADVERTENCIA_REGISTRADA";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var tipo = Texto(carga, "tipo") ?? "verbal";
        var dia = Dia(Texto(carga, "data"));
        var pessoa = Texto(carga, "pessoa") ?? "a pessoa";
        var gestor = Texto(carga, "gestor") ?? "O gestor";
        var paraRh = carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty("paraRh", out var valor) && valor.ValueKind == JsonValueKind.True;

        var abertura = paraRh ? $"{gestor} registrou uma advertência {tipo} de {dia} para {pessoa}." : $"Foi registrada uma advertência {tipo} em seu nome, referente a {dia}.";
        var fecho = paraRh ? "O registro completo está em Advertências, no RH-System." : "Abra o RH-System, em Advertências, para ler o que foi registrado e confirmar a ciência ou contestar.";
        var texto = $"Olá, {nome}!\n\n{abertura}\n\n{fecho}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p><p>{WebUtility.HtmlEncode(abertura)}</p><p>{WebUtility.HtmlEncode(fecho)}</p>";
        var assunto = paraRh ? $"Advertência registrada para {pessoa}" : "Advertência registrada em seu nome";
        return new ModeloDeEmail(assunto, texto, html, AuditoriaArea.Advertencias, "E-mail de advertência registrada enviado", $"Colaborador {nome}");
    }

    private static string Dia(string? iso) => DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data) ? data.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "data não informada";

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
