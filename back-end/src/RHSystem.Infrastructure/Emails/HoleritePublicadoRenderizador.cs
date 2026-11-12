using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de um holerite publicado (<c>HOLERITE_PUBLICADO</c>), na hora em que ele fica visível e também a cada nova versão: só diz que o holerite está disponível (a competência por extenso, e "corrigido" a partir da versão 2). Nunca leva valor do holerite, nem
/// o arquivo, nem link direto: a pessoa abre o RH-System. Todo valor no Html passa por <c>HtmlEncode</c>; a auditoria do envio (<c>ENVIOU_EMAIL</c>) nunca leva o destinatário.
/// </summary>
public sealed class HoleritePublicadoRenderizador : IRenderizadorDeEmail
{
    public string Tipo => "HOLERITE_PUBLICADO";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = Texto(carga, "nome") ?? "colaborador";
        var competencia = Texto(carga, "competencia") ?? "do mês";
        var corrigido = carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty("versao", out var versao) && versao.ValueKind == JsonValueKind.Number && versao.GetInt32() > 1;

        var abertura = corrigido ? $"O seu holerite {competencia} foi corrigido e a nova versão já está disponível no RH-System." : $"O seu holerite {competencia} já está disponível no RH-System.";
        var fecho = "Abra o RH-System, em Holerites, para consultar e baixar o PDF.";
        var texto = $"Olá, {nome}!\n\n{abertura}\n\n{fecho}";
        var html = $"<p>Olá, {WebUtility.HtmlEncode(nome)}!</p><p>{WebUtility.HtmlEncode(abertura)}</p><p>{WebUtility.HtmlEncode(fecho)}</p>";
        var assunto = corrigido ? $"Holerite corrigido: {competencia}" : $"Seu holerite {competencia} está disponível";
        return new ModeloDeEmail(assunto, texto, html, AuditoriaArea.Holerites, "E-mail do holerite publicado enviado", $"Colaborador {nome}");
    }

    private static string? Texto(JsonElement carga, string campo) =>
        carga.ValueKind == JsonValueKind.Object && carga.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;
}
