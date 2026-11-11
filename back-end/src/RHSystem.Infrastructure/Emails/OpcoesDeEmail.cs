namespace RHSystem.Infrastructure.Emails;

public enum SegurancaDoSmtp
{
    /// <summary>Sem criptografia (só o Mailpit local).</summary>
    Nenhuma,

    /// <summary>Conecta em texto e sobe para TLS (porta 587 dos provedores).</summary>
    StartTls,

    /// <summary>TLS desde o início (porta 465).</summary>
    Ssl,
}

public sealed class OpcoesDeSmtp
{
    public string Servidor { get; set; } = string.Empty;

    public int Porta { get; set; } = 587;

    public SegurancaDoSmtp Seguranca { get; set; } = SegurancaDoSmtp.StartTls;

    public string Usuario { get; set; } = string.Empty;

    /// <summary>Só por user-secrets ou configuração do App Service: nunca no repositório.</summary>
    public string Senha { get; set; } = string.Empty;
}

/// <summary>
/// A seção <c>Email</c>. <c>EnvioAtivo</c> é <c>false</c> no <c>appsettings.json</c> e <c>true</c> no de desenvolvimento: sem provedor configurado a fila só
/// acumula, e nada vira <c>FALHOU</c> por engano.
/// </summary>
public sealed class OpcoesDeEmail
{
    public const string Secao = "Email";

    public bool EnvioAtivo { get; set; }

    public int IntervaloSegundos { get; set; } = 10;

    public int LoteMaximoPorCiclo { get; set; } = 20;

    public int MaximoDeTentativas { get; set; } = 5;

    public int ReservaMinutos { get; set; } = 5;

    public string Remetente { get; set; } = string.Empty;

    public string NomeDoRemetente { get; set; } = "RH System";

    public OpcoesDeSmtp Smtp { get; set; } = new();

    /// <summary>Configuração errada falha na inicialização, com mensagem clara, e não no primeiro e-mail.</summary>
    public void Validar()
    {
        if (IntervaloSegundos < 1 || LoteMaximoPorCiclo < 1 || MaximoDeTentativas < 1 || ReservaMinutos < 1)
        {
            throw new InvalidOperationException("Email:IntervaloSegundos, Email:LoteMaximoPorCiclo, Email:MaximoDeTentativas e Email:ReservaMinutos precisam ser pelo menos 1.");
        }

        if (EnvioAtivo && (string.IsNullOrWhiteSpace(Remetente) || string.IsNullOrWhiteSpace(Smtp.Servidor)))
        {
            throw new InvalidOperationException("Com Email:EnvioAtivo ligado faltam Email:Remetente e Email:Smtp:Servidor na configuração.");
        }

        if (Smtp.Porta is < 1 or > 65535)
        {
            throw new InvalidOperationException("Email:Smtp:Porta precisa ficar entre 1 e 65535.");
        }
    }
}

/// <summary>A seção <c>App</c>: <c>UrlDoFront</c> é a base dos links dos e-mails (quem monta o link é cada renderizador).</summary>
public sealed class OpcoesDoApp
{
    public const string Secao = "App";

    public string UrlDoFront { get; set; } = string.Empty;

    /// <summary>
    /// Com o envio de e-mail ligado, a URL do front tem de ser absoluta, <c>http</c> ou <c>https</c>: sem ela o renderizador do convite e o da redefinição lançam, a fila marca cada mensagem como
    /// <c>FALHOU</c> e o RH vê "convite enviado" sem que nada saia. Configuração faltando falha na inicialização, não no primeiro e-mail. Com o envio desligado nada sai e a URL pode faltar.
    /// </summary>
    /// <exception cref="InvalidOperationException">O envio está ligado e a URL falta ou não é http/https absoluta.</exception>
    public void Validar(bool envioAtivo)
    {
        if (envioAtivo && !(Uri.TryCreate(UrlDoFront, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps)))
        {
            throw new InvalidOperationException("Com Email:EnvioAtivo ligado, App:UrlDoFront precisa ser a URL do front (http ou https), por exemplo https://app.exemplo.com.br: sem ela os e-mails de convite e de redefinição ficam sem link e a fila os marca como FALHOU.");
        }
    }
}
