using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;

namespace RHSystem.Demo.Exemplos;

/// <summary>O que o envio dos exemplos fez, para o <c>Program</c> contar ao dono e para os testes conferirem.</summary>
public sealed record RelatorioDosExemplos(int HoleritesEnviados, int HoleritesQueJaExistiam, int HoleritesRecusados, int AtestadosEnviados, int AtestadosValidados, IReadOnlyList<string> Avisos);

/// <summary>
/// Envia os holerites e o atestado de exemplo da demo <b>pela própria API</b>, entrando com o login de verdade e usando só as rotas que o front usa, depois do <c>seed_demo.sql</c> (o seed não grava arquivo, porque o arquivo precisa existir no armazenamento). Cada pessoa ativa ganha o holerite mensal dos dois meses anteriores a
/// <paramref name="hoje"/>; a pessoa que faltou num dia útil recente (no seed, o Thiago) envia um atestado desse dia e o RH o valida. Pode rodar mais de uma vez: o que já existe é contado e pulado, nada é duplicado. Uma recusa da API (competência antes da admissão, por exemplo) vira aviso e não derruba o resto; já um erro de
/// login, de permissão ou do servidor para tudo com uma mensagem.
/// </summary>
/// <param name="http">O cliente HTTP com o endereço da API (<c>BaseAddress</c>) ou, nos testes, o cliente da API em memória.</param>
/// <param name="hoje">O dia "de hoje" da demo, o mesmo que a API usa (em Brasília).</param>
/// <param name="dominio">O domínio dos e-mails do seed (<c>@dominio</c> do <c>seed_demo.sql</c>).</param>
/// <param name="senha">A senha de demonstração (pública).</param>
/// <param name="emailDoRh">O login do RH; sem ele, <c>helena.martins@dominio</c>.</param>
public sealed class EnviadorDeExemplos(HttpClient http, DateOnly hoje, string dominio, string senha, string? emailDoRh = null)
{
    private const string LocalDoRh = "helena.martins";
    private const string LocalDeQuemFalta = "thiago.mello";

    public async Task<RelatorioDosExemplos> EnviarAsync(CancellationToken cancelamento = default)
    {
        var avisos = new List<string>();
        var tokenDoRh = await EntrarAsync(emailDoRh ?? $"{LocalDoRh}@{dominio}", cancelamento);
        var pessoas = await ListarAtivasAsync(tokenDoRh, cancelamento);

        int enviados = 0, jaExistiam = 0, recusados = 0;
        foreach (var competencia in new[] { hoje.AddMonths(-2), hoje.AddMonths(-1) })
        {
            foreach (var pessoa in pessoas)
            {
                var resposta = await EnviarHoleriteAsync(tokenDoRh, pessoa, competencia, cancelamento);
                switch (resposta.Status)
                {
                    case HttpStatusCode.Created:
                        enviados++;
                        break;
                    case HttpStatusCode.Conflict:
                        jaExistiam++;
                        break;
                    case HttpStatusCode.BadRequest or HttpStatusCode.UnprocessableEntity:
                        recusados++;
                        avisos.Add($"Holerite {competencia.Month:00}/{competencia.Year} de {pessoa.Nome}: a API recusou ({(int)resposta.Status}): {Resumo(resposta.Corpo)}");
                        break;
                    default:
                        throw Falha("POST holerites", resposta.Status, resposta.Corpo);
                }
            }
        }

        var (atestadosEnviados, atestadosValidados) = await EnviarOAtestadoAsync(tokenDoRh, pessoas, avisos, cancelamento);
        return new RelatorioDosExemplos(enviados, jaExistiam, recusados, atestadosEnviados, atestadosValidados, avisos);
    }

    private async Task<(int Enviados, int Validados)> EnviarOAtestadoAsync(string tokenDoRh, IReadOnlyList<PessoaDaLista> pessoas, List<string> avisos, CancellationToken cancelamento)
    {
        var email = $"{LocalDeQuemFalta}@{dominio}";
        var pessoa = pessoas.FirstOrDefault(p => string.Equals(p.Email, email, StringComparison.OrdinalIgnoreCase));
        if (pessoa is null)
        {
            avisos.Add($"Atestado: {email} não está entre as pessoas ativas; pulei o atestado de exemplo.");
            return (0, 0);
        }

        string tokenDaPessoa;
        try
        {
            tokenDaPessoa = await EntrarAsync(email, cancelamento);
        }
        catch (InvalidOperationException erro)
        {
            avisos.Add($"Atestado: {erro.Message} Pulei o atestado de exemplo.");
            return (0, 0);
        }

        // O atestado de exemplo é um só por pessoa, em qualquer dia em que a ferramenta rode: quem já tem um validado não recebe outro, e um que ficou "enviado" (a rodada anterior parou entre o envio e a validação) é validado em vez de mandar mais um.
        var existentes = await AtestadosDaPessoaAsync(tokenDaPessoa, cancelamento);
        if (existentes.FirstOrDefault(a => a.Situacao == "ENVIADO") is { } aguardando)
        {
            await ValidarAsync(tokenDoRh, aguardando.Id, cancelamento);
            return (0, 1);
        }

        if (existentes.Any(a => a.Situacao == "VALIDADO"))
        {
            return (0, 0);
        }

        var dia = await UltimaFaltaAsync(tokenDaPessoa, cancelamento);
        if (dia is null)
        {
            return (0, 0); // ninguém com falta para justificar (a demo não tem falta recente)
        }

        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(dia.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "dataInicio" },
            { new StringContent(dia.Value.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)), "dataFim" },
            { Pdf(PdfsDeExemplo.Atestado(pessoa.Nome, dia.Value, dia.Value)), "arquivo", $"atestado-{Apelido(pessoa.Nome)}-{dia.Value:yyyy-MM-dd}.pdf" },
        };
        var envio = await EnviarAsync(HttpMethod.Post, "/api/eu/ponto/atestados", tokenDaPessoa, formulario, cancelamento);
        if (envio.Status != HttpStatusCode.Created)
        {
            if (envio.Status is HttpStatusCode.BadRequest or HttpStatusCode.Conflict or HttpStatusCode.UnprocessableEntity)
            {
                avisos.Add($"Atestado de {pessoa.Nome} para {dia.Value:dd/MM/yyyy}: a API recusou ({(int)envio.Status}): {Resumo(envio.Corpo)}");
                return (0, 0);
            }

            throw Falha("POST atestado", envio.Status, envio.Corpo);
        }

        await ValidarAsync(tokenDoRh, Campo(Ler(envio.Corpo, "POST atestado"), "id", "POST atestado").GetInt32(), cancelamento);
        return (1, 1);
    }

    private async Task ValidarAsync(string tokenDoRh, int atestadoId, CancellationToken cancelamento)
    {
        using var vazio = JsonContent.Create(new { });
        var validacao = await EnviarAsync(HttpMethod.Post, $"/api/rh/ponto/atestados/{atestadoId}/validar", tokenDoRh, vazio, cancelamento);
        if (validacao.Status != HttpStatusCode.OK)
        {
            throw Falha("validar atestado", validacao.Status, validacao.Corpo);
        }
    }

    /// <summary>Os atestados que a própria pessoa enviou (a primeira página de 100, do mais recente para o mais antigo).</summary>
    private async Task<IReadOnlyList<AtestadoDaLista>> AtestadosDaPessoaAsync(string token, CancellationToken cancelamento)
    {
        var resposta = await EnviarAsync(HttpMethod.Get, "/api/eu/ponto/atestados?tamanhoPagina=100", token, null, cancelamento);
        if (resposta.Status != HttpStatusCode.OK)
        {
            throw Falha("GET atestados", resposta.Status, resposta.Corpo);
        }

        var itens = Campo(Ler(resposta.Corpo, "GET atestados"), "itens", "GET atestados");
        return [.. itens.EnumerateArray().Select(a => new AtestadoDaLista(Campo(a, "id", "GET atestados").GetInt32(), Campo(a, "situacao", "GET atestados").GetString()!))];
    }

    /// <summary>O dia útil mais recente, antes de hoje, que o espelho de ponto da pessoa mostra como <c>SEM_REGISTRO</c> (dia com expediente e nenhuma batida: a falta) (olha o mês anterior e o atual).</summary>
    private async Task<DateOnly?> UltimaFaltaAsync(string token, CancellationToken cancelamento)
    {
        DateOnly? ultima = null;
        foreach (var mes in new[] { hoje.AddMonths(-1), hoje })
        {
            var resposta = await EnviarAsync(HttpMethod.Get, $"/api/eu/ponto/espelho?ano={mes.Year}&mes={mes.Month}", token, null, cancelamento);
            if (resposta.Status != HttpStatusCode.OK)
            {
                throw Falha("GET espelho", resposta.Status, resposta.Corpo);
            }

            foreach (var dia in Campo(Ler(resposta.Corpo, "GET espelho"), "dias", "GET espelho").EnumerateArray())
            {
                var data = DateOnly.ParseExact(Campo(dia, "data", "GET espelho").GetString()!, "yyyy-MM-dd", CultureInfo.InvariantCulture);
                if (data < hoje && Campo(dia, "situacao", "GET espelho").GetString() == "SEM_REGISTRO" && (ultima is null || data > ultima))
                {
                    ultima = data;
                }
            }
        }

        return ultima;
    }

    private async Task<Resposta> EnviarHoleriteAsync(string token, PessoaDaLista pessoa, DateOnly competencia, CancellationToken cancelamento)
    {
        using var formulario = new MultipartFormDataContent
        {
            { new StringContent(competencia.Month.ToString(CultureInfo.InvariantCulture)), "mes" },
            { new StringContent(competencia.Year.ToString(CultureInfo.InvariantCulture)), "ano" },
            { new StringContent("MENSAL"), "tipo" },
            { Pdf(PdfsDeExemplo.Holerite(pessoa.Nome, pessoa.Cargo ?? "Colaborador", pessoa.Setor ?? "Empresa", competencia.Month, competencia.Year)), "arquivo", $"holerite-{competencia.Year}-{competencia.Month:00}-{Apelido(pessoa.Nome)}.pdf" },
        };
        return await EnviarAsync(HttpMethod.Post, $"/api/rh/funcionarios/{pessoa.Id}/holerites", token, formulario, cancelamento);
    }

    private async Task<IReadOnlyList<PessoaDaLista>> ListarAtivasAsync(string token, CancellationToken cancelamento)
    {
        var pessoas = new List<PessoaDaLista>();
        for (var pagina = 1; ; pagina++)
        {
            var resposta = await EnviarAsync(HttpMethod.Get, $"/api/rh/funcionarios?situacao=ATIVO&tamanhoPagina=100&pagina={pagina}", token, null, cancelamento);
            if (resposta.Status != HttpStatusCode.OK)
            {
                throw Falha("GET colaboradores", resposta.Status, resposta.Corpo);
            }

            var corpo = Ler(resposta.Corpo, "GET colaboradores");
            foreach (var item in Campo(corpo, "itens", "GET colaboradores").EnumerateArray())
            {
                pessoas.Add(new PessoaDaLista(Campo(item, "id", "GET colaboradores").GetInt32(), Campo(item, "nome", "GET colaboradores").GetString()!, Texto(item, "emailCorporativo"), Texto(item, "cargo"), Texto(item, "setor")));
            }

            if (pagina >= Campo(corpo, "totalPaginas", "GET colaboradores").GetInt32())
            {
                return pessoas;
            }
        }
    }

    private async Task<string> EntrarAsync(string email, CancellationToken cancelamento)
    {
        using var resposta = await http.PostAsJsonAsync("/api/auth/entrar", new { email, senha }, cancelamento);
        ConferirEndereco(resposta);
        if (resposta.StatusCode == HttpStatusCode.TooManyRequests)
        {
            throw new InvalidOperationException($"O login de {email} foi barrado por excesso de tentativas seguidas (429). Espere um minuto e rode de novo.");
        }

        if (!resposta.IsSuccessStatusCode)
        {
            throw new InvalidOperationException($"O login de {email} falhou ({(int)resposta.StatusCode}). Confira o endereço da API, o e-mail e a senha (a demo usa Demo#RH2026) e se o seed já foi rodado.");
        }

        return Campo(Ler(await resposta.Content.ReadAsStringAsync(cancelamento), "O login"), "tokenDeAcesso", "O login").GetString()!;
    }

    private async Task<Resposta> EnviarAsync(HttpMethod metodo, string rota, string token, HttpContent? conteudo, CancellationToken cancelamento)
    {
        using var pedido = new HttpRequestMessage(metodo, rota) { Content = conteudo };
        pedido.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        using var resposta = await http.SendAsync(pedido, cancelamento);
        ConferirEndereco(resposta);
        return new Resposta(resposta.StatusCode, await resposta.Content.ReadAsStringAsync(cancelamento));
    }

    /// <summary>Um redirecionamento (por exemplo de http para https) refaz o pedido sem o cabeçalho de acesso e o resultado é um erro enganoso; melhor parar e dizer o endereço certo.</summary>
    private void ConferirEndereco(HttpResponseMessage resposta)
    {
        if (http.BaseAddress is { } esperado && resposta.RequestMessage?.RequestUri is { } final
            && !string.Equals(final.GetLeftPart(UriPartial.Authority), esperado.GetLeftPart(UriPartial.Authority), StringComparison.OrdinalIgnoreCase))
        {
            throw new InvalidOperationException($"A API redirecionou o pedido para {final.GetLeftPart(UriPartial.Authority)}. Use esse endereço em --url.");
        }
    }

    private static JsonElement Ler(string corpo, string o_que)
    {
        try
        {
            return JsonDocument.Parse(corpo).RootElement.Clone();
        }
        catch (JsonException)
        {
            throw Estranha(o_que);
        }
    }

    private static JsonElement Campo(JsonElement pai, string nome, string o_que) =>
        pai.ValueKind == JsonValueKind.Object && pai.TryGetProperty(nome, out var valor) ? valor : throw Estranha(o_que);

    private static InvalidOperationException Estranha(string o_que) =>
        new($"{o_que} devolveu algo que não é a resposta da API do RH System. Confira se o --url é o endereço da API (e não o do site) e se ela está no ar.");

    private static ByteArrayContent Pdf(byte[] bytes)
    {
        var conteudo = new ByteArrayContent(bytes);
        conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/pdf");
        return conteudo;
    }

    private static InvalidOperationException Falha(string o_que, HttpStatusCode status, string corpo) =>
        new($"{o_que} respondeu {(int)status}: {Resumo(corpo)}. O envio dos exemplos parou aqui.");

    private static string Resumo(string corpo)
    {
        var limpo = corpo.ReplaceLineEndings(" ").Trim();
        return limpo.Length <= 240 ? limpo : limpo[..240] + "...";
    }

    private static string? Texto(JsonElement item, string campo) => item.TryGetProperty(campo, out var valor) && valor.ValueKind == JsonValueKind.String ? valor.GetString() : null;

    /// <summary>O nome sem acento, em minúsculas e com hífen, para o nome do arquivo (<c>Patrícia Gomes</c> vira <c>patricia-gomes</c>).</summary>
    private static string Apelido(string nome)
    {
        var decomposto = nome.Normalize(NormalizationForm.FormD);
        var texto = new StringBuilder();
        foreach (var letra in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(letra) == UnicodeCategory.NonSpacingMark)
            {
                continue;
            }

            texto.Append(char.IsAsciiLetterOrDigit(letra) ? char.ToLowerInvariant(letra) : '-');
        }

        return string.Join('-', texto.ToString().Split('-', StringSplitOptions.RemoveEmptyEntries));
    }

    private sealed record PessoaDaLista(int Id, string Nome, string? Email, string? Cargo, string? Setor);

    private sealed record AtestadoDaLista(int Id, string Situacao);

    private sealed record Resposta(HttpStatusCode Status, string Corpo);
}
