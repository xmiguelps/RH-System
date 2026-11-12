using System.Text.Json;
using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>Uma resposta já conferida contra a pergunta: <c>Resposta</c> é o texto, o texto da alternativa ou o número da escala; na pergunta de arquivo fica vazia e <c>ArquivoIndice</c> diz qual arquivo do envio é (o caso de uso põe o nome).</summary>
public sealed record RespostaValidada(PerguntaLida Pergunta, string Resposta, int? AlternativaId, int? ArquivoIndice);

/// <summary>
/// A parte <c>respostas</c> do envio (texto JSON), conferida contra as perguntas do formulário, com TODOS os erros juntos em chaves como <c>respostas[2].texto</c>. Cada item usa só o campo do tipo da pergunta (campo de outro tipo é erro); curta até 200 caracteres, longa até 4000, múltipla escolha com UMA alternativa
/// DA pergunta, escala de 1 a 5, arquivo por <c>arquivoIndice</c> (uma posição válida de <c>arquivos</c>, usada uma vez só). Pergunta opcional em branco não gera resposta; obrigatória em branco ou fora da lista é erro; ao menos uma pergunta precisa ser respondida; arquivo que nenhuma
/// resposta cita é erro em <c>arquivos</c>. Nada aqui lê o banco.
/// </summary>
public static class ValidacaoDaResposta
{
    public const int MaximoDaCurta = 200;

    public const int MaximoDaLonga = 4000;

    public static IReadOnlyList<RespostaValidada> Validar(ColetorDeErros erros, IReadOnlyList<PerguntaLida> perguntas, string? json, int totalDeArquivos)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            erros.Adicionar("respostas", "Informe as respostas.");
            return [];
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            erros.Adicionar("respostas", "As respostas precisam ser uma lista em JSON.");
            return [];
        }

        using (documento)
        {
            var raiz = documento.RootElement;
            if (raiz.ValueKind != JsonValueKind.Array)
            {
                erros.Adicionar("respostas", "As respostas precisam ser uma lista em JSON.");
                return [];
            }

            if (raiz.GetArrayLength() == 0)
            {
                erros.Adicionar("respostas", "Responda ao menos uma pergunta.");
                return [];
            }

            return Conferir(erros, perguntas, raiz, totalDeArquivos);
        }
    }

    private static List<RespostaValidada> Conferir(ColetorDeErros erros, IReadOnlyList<PerguntaLida> perguntas, JsonElement lista, int totalDeArquivos)
    {
        var linhas = new List<RespostaValidada>();
        var citadas = new HashSet<int>();
        var arquivosUsados = new HashSet<int>();
        var posicao = -1;
        foreach (var item in lista.EnumerateArray())
        {
            posicao++;
            var chave = $"respostas[{posicao}]";
            if (item.ValueKind != JsonValueKind.Object)
            {
                erros.Adicionar(chave, "Cada resposta precisa ser um objeto.");
                continue;
            }

            if (!InteiroDe(item, "perguntaId", out var perguntaId) || perguntaId < 1)
            {
                erros.Adicionar($"{chave}.perguntaId", "Informe a pergunta.");
                continue;
            }

            var pergunta = perguntas.FirstOrDefault(p => p.Id == perguntaId);
            if (pergunta is null)
            {
                erros.Adicionar($"{chave}.perguntaId", "Esta pergunta não é deste formulário.");
                continue;
            }

            if (!citadas.Add(perguntaId))
            {
                erros.Adicionar($"{chave}.perguntaId", "Esta pergunta já foi respondida acima.");
                continue;
            }

            var resposta = pergunta.Tipo switch
            {
                TipoDePergunta.RespostaCurta => Texto(erros, chave, item, pergunta, MaximoDaCurta),
                TipoDePergunta.RespostaLonga => Texto(erros, chave, item, pergunta, MaximoDaLonga),
                TipoDePergunta.MultiplaEscolha => Alternativa(erros, chave, item, pergunta),
                TipoDePergunta.Escala => Escala(erros, chave, item, pergunta),
                _ => Arquivo(erros, chave, item, pergunta, totalDeArquivos, arquivosUsados),
            };
            if (resposta is not null)
            {
                linhas.Add(resposta);
            }
        }

        foreach (var pergunta in perguntas.Where(p => p.Obrigatoria && !citadas.Contains(p.Id)).OrderBy(p => p.Ordem))
        {
            erros.Adicionar("respostas", $"Responda a pergunta {pergunta.Ordem}, que é obrigatória.");
        }

        if (linhas.Count == 0 && !erros.TemErros)
        {
            erros.Adicionar("respostas", "Responda ao menos uma pergunta.");
        }

        if (Enumerable.Range(0, totalDeArquivos).Any(i => !arquivosUsados.Contains(i)))
        {
            erros.Adicionar("arquivos", "Há um arquivo que nenhuma resposta usa.");
        }

        return [.. linhas.OrderBy(l => l.Pergunta.Ordem).ThenBy(l => l.Pergunta.Id)];
    }

    private static RespostaValidada? Texto(ColetorDeErros erros, string chave, JsonElement item, PerguntaLida pergunta, int maximo)
    {
        ProibirOutros(erros, chave, item, "alternativaIds", "valorEscala", "arquivoIndice");
        var texto = string.Empty;
        if (Presente(item, "texto", out var elemento))
        {
            if (elemento.ValueKind != JsonValueKind.String)
            {
                erros.Adicionar($"{chave}.texto", "O texto precisa ser um texto.");
                return null;
            }

            texto = elemento.GetString()!.Trim();
        }

        if (texto.Length == 0)
        {
            return EmBranco(erros, $"{chave}.texto", pergunta, "Responda esta pergunta.");
        }

        if (texto.Length > maximo)
        {
            erros.Adicionar($"{chave}.texto", $"Use até {maximo} caracteres.");
            return null;
        }

        return new RespostaValidada(pergunta, texto, null, null);
    }

    private static RespostaValidada? Alternativa(ColetorDeErros erros, string chave, JsonElement item, PerguntaLida pergunta)
    {
        ProibirOutros(erros, chave, item, "texto", "valorEscala", "arquivoIndice");
        if (!Presente(item, "alternativaIds", out var ids) || (ids.ValueKind == JsonValueKind.Array && ids.GetArrayLength() == 0))
        {
            return EmBranco(erros, $"{chave}.alternativaIds", pergunta, "Escolha uma alternativa.");
        }

        if (ids.ValueKind != JsonValueKind.Array || ids.GetArrayLength() != 1 || !Inteiro(ids[0], out var id))
        {
            erros.Adicionar($"{chave}.alternativaIds", "Escolha uma só alternativa.");
            return null;
        }

        var alternativa = pergunta.Alternativas.FirstOrDefault(a => a.Id == id);
        if (alternativa is null)
        {
            erros.Adicionar($"{chave}.alternativaIds", "Esta alternativa não é desta pergunta.");
            return null;
        }

        return new RespostaValidada(pergunta, alternativa.Texto, alternativa.Id, null);
    }

    private static RespostaValidada? Escala(ColetorDeErros erros, string chave, JsonElement item, PerguntaLida pergunta)
    {
        ProibirOutros(erros, chave, item, "texto", "alternativaIds", "arquivoIndice");
        if (!Presente(item, "valorEscala", out var elemento))
        {
            return EmBranco(erros, $"{chave}.valorEscala", pergunta, "Escolha um valor de 1 a 5.");
        }

        if (!Inteiro(elemento, out var valor) || valor is < 1 or > 5)
        {
            erros.Adicionar($"{chave}.valorEscala", "O valor vai de 1 a 5.");
            return null;
        }

        return new RespostaValidada(pergunta, valor.ToString(System.Globalization.CultureInfo.InvariantCulture), null, null);
    }

    private static RespostaValidada? Arquivo(ColetorDeErros erros, string chave, JsonElement item, PerguntaLida pergunta, int totalDeArquivos, HashSet<int> usados)
    {
        ProibirOutros(erros, chave, item, "texto", "alternativaIds", "valorEscala");
        if (!Presente(item, "arquivoIndice", out var elemento))
        {
            return EmBranco(erros, $"{chave}.arquivoIndice", pergunta, "Envie o arquivo desta pergunta.");
        }

        if (!Inteiro(elemento, out var indice) || indice < 0 || indice >= totalDeArquivos)
        {
            erros.Adicionar($"{chave}.arquivoIndice", "Este arquivo não foi enviado.");
            return null;
        }

        if (!usados.Add(indice))
        {
            erros.Adicionar($"{chave}.arquivoIndice", "Este arquivo já foi usado em outra resposta.");
            return null;
        }

        return new RespostaValidada(pergunta, string.Empty, null, indice);
    }

    /// <summary>A resposta em branco: pergunta opcional não gera linha (<c>null</c> sem erro); obrigatória é erro.</summary>
    private static RespostaValidada? EmBranco(ColetorDeErros erros, string chave, PerguntaLida pergunta, string mensagem)
    {
        if (pergunta.Obrigatoria)
        {
            erros.Adicionar(chave, mensagem);
        }

        return null;
    }

    private static void ProibirOutros(ColetorDeErros erros, string chave, JsonElement item, params string[] campos)
    {
        foreach (var campo in campos)
        {
            if (Presente(item, campo, out _))
            {
                erros.Adicionar($"{chave}.{campo}", "Esta pergunta não usa este campo.");
            }
        }
    }

    /// <summary>O campo existe e não é <c>null</c>.</summary>
    private static bool Presente(JsonElement item, string campo, out JsonElement valor) => item.TryGetProperty(campo, out valor) && valor.ValueKind != JsonValueKind.Null;

    private static bool InteiroDe(JsonElement item, string campo, out int valor)
    {
        valor = 0;
        return Presente(item, campo, out var elemento) && Inteiro(elemento, out valor);
    }

    /// <summary>Um número inteiro (texto, lista, objeto, booleano e decimal não são).</summary>
    private static bool Inteiro(JsonElement elemento, out int valor)
    {
        valor = 0;
        return elemento.ValueKind == JsonValueKind.Number && elemento.TryGetInt32(out valor);
    }
}
