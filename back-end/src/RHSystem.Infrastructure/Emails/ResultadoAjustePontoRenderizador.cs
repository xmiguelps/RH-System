using System.Globalization;
using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail do resultado de um pedido de ajuste de ponto (<c>RESULTADO_AJUSTE_PONTO</c>), a cada decisão: a do gestor (aprovar passa o pedido ao RH; recusar o encerra) e a do RH. Todo valor interpolado no Html passa por <c>HtmlEncode</c>. A auditoria do envio
/// (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário nem a observação.
/// </summary>
public sealed class ResultadoAjustePontoRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "RESULTADO_AJUSTE_PONTO";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var data = DateOnly.TryParseExact(Texto(carga, "data"), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia) ? dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "informado";
        var aprovado = Texto(carga, "resultado") == "APROVADA";
        var peloGestor = Texto(carga, "etapa") == "GESTOR";
        var decididoPor = Texto(carga, "decididoPor");
        var observacao = Texto(carga, "observacao");

        var quem = peloGestor ? "pelo gestor" : "pelo RH";
        var resultado = aprovado ? "aprovado" : "recusado";
        var proximo = aprovado && peloGestor ? "Ele agora segue para a análise do RH." : aprovado ? "As batidas do dia foram corrigidas." : "As batidas do dia ficam como estavam.";

        var linhas = new List<string>
        {
            $"O seu pedido de ajuste de ponto do dia {data} foi {resultado} {quem}{(string.IsNullOrWhiteSpace(decididoPor) ? string.Empty : $" ({decididoPor})")}.",
            proximo,
        };
        if (!string.IsNullOrWhiteSpace(observacao))
        {
            linhas.Add($"Observação: {observacao}");
        }

        var texto = $"Olá, {nome}!\n\n{string.Join("\n\n", linhas)}\n\nVocê também vê a resposta em \"Meus pedidos de ajuste\", no RH-System.";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p>{string.Concat(linhas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>"))}<p>Você também vê a resposta em \"Meus pedidos de ajuste\", no RH-System.</p>";
        return new ModeloDeEmail("Resposta do seu pedido de ajuste de ponto", texto, html, AuditoriaArea.Ponto, "E-mail do resultado do pedido de ajuste enviado", $"Colaborador {nome}");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
