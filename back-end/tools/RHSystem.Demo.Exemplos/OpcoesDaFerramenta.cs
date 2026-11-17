namespace RHSystem.Demo.Exemplos;

public sealed record OpcoesDaFerramenta(Uri Url, string Dominio, string Senha, string? EmailDoRh, DateOnly? Hoje)
{
    public const string DominioPadrao = "rhsystem-demo.example";

    /// <summary>A senha pública da demo (a mesma do <c>seed_demo.sql</c>): não é segredo, está no <c>LEIA-ME</c> do seed.</summary>
    public const string SenhaPadrao = "Demo#RH2026";

    public const string Uso = """
        Envia os holerites e o atestado de exemplo da demo pela API, depois do seed.

        Uso:  dotnet run --project back-end/tools/RHSystem.Demo.Exemplos -- --url <endereço da API> [opções]

          --url <endereço>      obrigatório: o endereço da API, com http:// ou https://
          --dominio <domínio>   o domínio dos e-mails do seed (padrão: rhsystem-demo.example)
          --senha <senha>       a senha da demo (padrão: a senha pública do seed)
          --email-do-rh <e-mail> o login do RH (padrão: helena.martins@<domínio>)
          --hoje <aaaa-mm-dd>   o dia "de hoje" da demo (padrão: hoje em Brasília)
          --ajuda               mostra esta mensagem
        """;

    public static bool PediuAjuda(IReadOnlyList<string> argumentos) => argumentos.Any(a => a is "--ajuda" or "-h");

    /// <summary>As opções, ou <c>null</c> com a mensagem do que consertar (ou do uso, quando pediram ajuda).</summary>
    public static (OpcoesDaFerramenta? Opcoes, string? Mensagem) Ler(IReadOnlyList<string> argumentos)
    {
        if (PediuAjuda(argumentos))
        {
            return (null, Uso);
        }

        string? url = null, dominio = null, senha = null, emailDoRh = null, hoje = null;
        for (var i = 0; i < argumentos.Count; i += 2)
        {
            var opcao = argumentos[i];
            if (opcao is not ("--url" or "--dominio" or "--senha" or "--email-do-rh" or "--hoje"))
            {
                return (null, $"A opção '{opcao}' não existe. Use --ajuda para ver as opções.");
            }

            if (i + 1 >= argumentos.Count)
            {
                return (null, $"Faltou o valor depois de {opcao}.");
            }

            var valor = argumentos[i + 1];
            switch (opcao)
            {
                case "--url": url = valor; break;
                case "--dominio": dominio = valor; break;
                case "--senha": senha = valor; break;
                case "--email-do-rh": emailDoRh = valor; break;
                default: hoje = valor; break;
            }
        }

        if (url is null)
        {
            return (null, "Falta o endereço da API: --url https://... (use --ajuda para ver as opções).");
        }

        if (!Uri.TryCreate(url, UriKind.Absolute, out var endereco) || endereco.Scheme is not ("http" or "https"))
        {
            return (null, $"O valor de --url ('{url}') não é um endereço http:// ou https:// completo.");
        }

        DateOnly? dia = null;
        if (hoje is not null)
        {
            if (!DateOnly.TryParseExact(hoje, "yyyy-MM-dd", out var lido))
            {
                return (null, $"O valor de --hoje ('{hoje}') não é uma data no formato aaaa-mm-dd.");
            }

            dia = lido;
        }

        return (new OpcoesDaFerramenta(endereco, dominio ?? DominioPadrao, senha ?? SenhaPadrao, emailDoRh, dia), null);
    }

    /// <summary>O dia de hoje no Brasil (UTC-3, sem horário de verão desde 2019), que é o dia que a API usa.</summary>
    public static DateOnly HojeEmBrasilia(DateTime agoraEmUtc) => DateOnly.FromDateTime(agoraEmUtc.AddHours(-3));
}
