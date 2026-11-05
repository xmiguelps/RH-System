using System.Net;
using System.Text.Json;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Acesso;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// O e-mail de convite (primeiro acesso). O link é <c>{App:UrlDoFront}/definir-senha?token={token}</c>; todo valor interpolado no Html passa por <c>HtmlEncode</c>. A auditoria do envio
/// (<c>ENVIOU_EMAIL</c>) nunca leva o token nem o destinatário. Sem <c>App:UrlDoFront</c> o renderizador lança: é melhor a fila desistir (e o RH ver a falha) do que mandar um link quebrado.
/// </summary>
public sealed class ConviteRenderizador(OpcoesDoApp app, OpcoesDeSenha senha) : IRenderizadorDeEmail
{
    public string Tipo => "CONVITE";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var (nome, link) = RenderizadorDeSenha.Ler(carga, app);
        var validade = senha.ConviteDias == 1 ? "1 dia" : $"{senha.ConviteDias} dias";
        var texto = $"""
            Olá, {nome}!

            O RH criou o seu acesso ao RH-System. Para escolher a sua senha, abra o link abaixo:

            {link}

            Este link vale por {validade} e só pode ser usado uma vez. Se ele vencer, peça um novo convite ao RH.

            Se você não esperava este e-mail, pode ignorá-lo.
            """;
        var html = RenderizadorDeSenha.Html(
            nome,
            link,
            "O RH criou o seu acesso ao RH-System. Para escolher a sua senha, use o botão abaixo.",
            "Escolher a minha senha",
            $"Este link vale por {validade} e só pode ser usado uma vez. Se ele vencer, peça um novo convite ao RH.",
            "Se você não esperava este e-mail, pode ignorá-lo.");
        return new ModeloDeEmail("Seu acesso ao RH-System", texto, html, AuditoriaArea.Acesso, "E-mail de convite enviado", $"Colaborador {nome}");
    }
}

/// <summary>O e-mail de redefinição de senha ("Esqueci minha senha"). Mesmas regras do convite.</summary>
public sealed class RedefinicaoSenhaRenderizador(OpcoesDoApp app, OpcoesDeSenha senha) : IRenderizadorDeEmail
{
    public string Tipo => "REDEFINICAO_SENHA";

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var (nome, link) = RenderizadorDeSenha.Ler(carga, app);
        var validade = RenderizadorDeSenha.Minutos(senha.RedefinicaoMinutos);
        var texto = $"""
            Olá, {nome}!

            Recebemos um pedido para redefinir a sua senha do RH-System. Para escolher uma senha nova, abra o link abaixo:

            {link}

            Este link vale por {validade} e só pode ser usado uma vez.

            Se você não fez este pedido, ignore este e-mail: a sua senha continua a mesma.
            """;
        var html = RenderizadorDeSenha.Html(
            nome,
            link,
            "Recebemos um pedido para redefinir a sua senha do RH-System. Para escolher uma senha nova, use o botão abaixo.",
            "Redefinir a minha senha",
            $"Este link vale por {validade} e só pode ser usado uma vez.",
            "Se você não fez este pedido, ignore este e-mail: a sua senha continua a mesma.");
        return new ModeloDeEmail("Redefinição de senha do RH-System", texto, html, AuditoriaArea.Acesso, "E-mail de redefinição de senha enviado", $"Colaborador {nome}");
    }
}

internal static class RenderizadorDeSenha
{
    /// <summary>O nome e o link da carga <c>{ nome, token, expiraEmUtc }</c>. Carga sem token ou sem nome, ou sem a URL do front configurada, lança (a fila desiste em vez de mandar e-mail sem link).</summary>
    public static (string Nome, string Link) Ler(JsonElement carga, OpcoesDoApp app)
    {
        if (string.IsNullOrWhiteSpace(app.UrlDoFront))
        {
            throw new InvalidOperationException("Falta App:UrlDoFront na configuração: sem ela o e-mail sairia com o link quebrado.");
        }

        var nome = carga.GetProperty("nome").GetString();
        var token = carga.GetProperty("token").GetString();
        if (string.IsNullOrWhiteSpace(nome) || string.IsNullOrWhiteSpace(token))
        {
            throw new InvalidOperationException("A carga do e-mail precisa de nome e token.");
        }

        return (nome, $"{app.UrlDoFront.TrimEnd('/')}/definir-senha?token={Uri.EscapeDataString(token)}");
    }

    /// <summary>Validade em horas quando fecha hora cheia ("1 hora", "2 horas"), senão em minutos.</summary>
    public static string Minutos(int minutos) => minutos % 60 == 0
        ? (minutos / 60 == 1 ? "1 hora" : $"{minutos / 60} horas")
        : (minutos == 1 ? "1 minuto" : $"{minutos} minutos");

    public static string Html(string nome, string link, string abertura, string botao, string validade, string aviso)
    {
        static string E(string valor) => WebUtility.HtmlEncode(valor);
        return $"""
            <p>Olá, {E(nome)}!</p>
            <p>{E(abertura)}</p>
            <p><a href="{E(link)}">{E(botao)}</a></p>
            <p>Se o botão não abrir, copie e cole este endereço no navegador:<br>{E(link)}</p>
            <p>{E(validade)}</p>
            <p>{E(aviso)}</p>
            """;
    }
}
