using System.Globalization;
using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de cada decisão de um pedido de férias (<c>FERIAS_DECIDIDAS</c>): o gestor aprovou (o pedido segue ao RH), recusou (com o motivo) ou sugeriu outras datas; o RH confirmou ou recusou (com o motivo). Todo valor interpolado no Html passa por <c>HtmlEncode</c> e a auditoria do envio
/// (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário nem o motivo.
/// </summary>
public sealed class ResultadoFeriasRenderizador : IRenderizadorDeEmail
{
    public string Tipo => DecisaoDeFeriasPorEmail.Tipo;

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var decisao = Texto(carga, "decisao");
        var periodo = $"{Data(Texto(carga, "dataInicio"))} a {Data(Texto(carga, "dataFim"))}";
        var decididoPor = Texto(carga, "decididoPor");
        var motivo = Texto(carga, "motivo");
        var quem = string.IsNullOrWhiteSpace(decididoPor) ? string.Empty : $" ({decididoPor})";

        var sugeriu = decisao == DecisaoDeFeriasPorEmail.GestorSugeriu;
        var linhas = new List<string>();
        switch (decisao)
        {
            case DecisaoDeFeriasPorEmail.GestorAprovou:
                linhas.Add($"O seu pedido de férias de {periodo} foi aprovado pelo gestor{quem}. Ele agora segue para a confirmação do RH.");
                break;
            case DecisaoDeFeriasPorEmail.GestorRecusou:
                linhas.Add($"O seu pedido de férias de {periodo} foi recusado pelo gestor{quem}.");
                break;
            case DecisaoDeFeriasPorEmail.GestorSugeriu:
                linhas.Add($"O gestor{quem} sugeriu outras datas para o seu pedido de férias de {periodo}: de {Data(Texto(carga, "sugestaoInicio"))} a {Data(Texto(carga, "sugestaoFim"))}.");
                linhas.Add("Aceite ou recuse a sugestão em \"Minhas férias\", no RH-System. Sem resposta, a sugestão expira e o pedido é cancelado.");
                break;
            case DecisaoDeFeriasPorEmail.RhConfirmou:
                linhas.Add($"O seu pedido de férias de {periodo} foi confirmado pelo RH{quem}. Boas férias!");
                break;
            default:
                linhas.Add($"O seu pedido de férias de {periodo} foi recusado pelo RH{quem}.");
                break;
        }

        if (!string.IsNullOrWhiteSpace(motivo))
        {
            linhas.Add($"Motivo: {motivo}");
        }

        var fim = sugeriu ? string.Empty : "Você também vê a resposta em \"Minhas férias\", no RH-System.";
        var texto = $"Olá, {nome}!\n\n{string.Join("\n\n", linhas)}{(fim.Length == 0 ? string.Empty : $"\n\n{fim}")}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p>{string.Concat(linhas.Select(l => $"<p>{WebUtility.HtmlEncode(l)}</p>"))}{(fim.Length == 0 ? string.Empty : $"<p>{WebUtility.HtmlEncode(fim)}</p>")}";
        var assunto = sugeriu ? "Sugestão de outra data para as suas férias" : "Resposta do seu pedido de férias";
        return new ModeloDeEmail(assunto, texto, html, AuditoriaArea.Ferias, "E-mail do resultado do pedido de férias enviado", $"Colaborador {nome}");
    }

    private static string Data(string? iso) =>
        DateOnly.TryParseExact(iso, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dia) ? dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : "informada";

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
