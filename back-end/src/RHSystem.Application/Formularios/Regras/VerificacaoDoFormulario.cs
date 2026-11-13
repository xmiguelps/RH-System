using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>A situação de um item da verificação (o contrato: <c>OK</c>, <c>AVISO</c>, <c>BLOQUEIO</c>).</summary>
public enum SituacaoDoItem
{
    Ok,
    Aviso,
    Bloqueio,
}

/// <summary>Um item da etapa Verificação. <c>PerguntaId</c> aponta a pergunta de que o item fala (vazio nos itens do formulário todo).</summary>
public sealed record ItemDeVerificacao(string Codigo, SituacaoDoItem Situacao, string Mensagem, int? PerguntaId);

/// <summary>O resultado da etapa Verificação (<c>FormularioVerificacao</c>): <c>PodePublicar</c> é "nenhum item em <c>BLOQUEIO</c>".</summary>
public sealed record FormularioVerificacao(bool PodePublicar, IReadOnlyList<ItemDeVerificacao> Itens);

/// <summary>O que a verificação lê: o texto do formulário, as perguntas e o público (o já guardado, na etapa Verificação, ou o do corpo, ao publicar).</summary>
public sealed record EntradaDaVerificacao(string Titulo, string? Descricao, TipoDeFormulario Tipo, bool Anonimo, IReadOnlyList<PerguntaLida> Perguntas, QuemDeveResponder Quem);

/// <summary>
/// Os itens da etapa Verificação, na ordem do contrato: <c>TITULO_E_DESCRICAO</c>, <c>PERGUNTAS</c>, <c>ALTERNATIVAS</c>, <c>ANONIMO_SEM_ARQUIVO</c> (só quando bloqueia), <c>ANONIMO_RESULTADO_MINIMO</c> e <c>ANONIMO_PUBLICO_MINIMO</c> (só no anônimo) e
/// <c>DADO_SENSIVEL</c>. Os textos dos exemplos do contrato saem ao pé da letra; os que o contrato não dá (nenhuma pergunta, arquivo no anônimo, a avaliação no resultado mínimo) são do executor. O dado sensível é por palavra-chave e só avisa, nunca bloqueia.
/// </summary>
public static class RegrasDaVerificacao
{
    public static FormularioVerificacao Montar(EntradaDaVerificacao entrada)
    {
        var itens = new List<ItemDeVerificacao>();
        AdicionarTituloEDescricao(itens, entrada);
        AdicionarPerguntas(itens, entrada);
        AdicionarAlternativas(itens, entrada);
        if (entrada.Anonimo)
        {
            AdicionarArquivoNoAnonimo(itens, entrada);
            AdicionarResultadoMinimo(itens, entrada);
            AdicionarPublicoMinimo(itens, entrada);
        }

        AdicionarDadoSensivel(itens, entrada);
        return new FormularioVerificacao(itens.All(i => i.Situacao != SituacaoDoItem.Bloqueio), itens);
    }

    private static void AdicionarTituloEDescricao(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada) =>
        itens.Add(string.IsNullOrWhiteSpace(entrada.Descricao)
            ? new ItemDeVerificacao("TITULO_E_DESCRICAO", SituacaoDoItem.Aviso, "O formulário não tem descrição.", null)
            : new ItemDeVerificacao("TITULO_E_DESCRICAO", SituacaoDoItem.Ok, "Título e descrição preenchidos.", null));

    private static void AdicionarPerguntas(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada)
    {
        var total = entrada.Perguntas.Count;
        if (total == 0)
        {
            itens.Add(new ItemDeVerificacao("PERGUNTAS", SituacaoDoItem.Bloqueio, "O formulário não tem nenhuma pergunta.", null));
            return;
        }

        var obrigatorias = entrada.Perguntas.Count(p => p.Obrigatoria);
        itens.Add(new ItemDeVerificacao("PERGUNTAS", SituacaoDoItem.Ok, $"{Contar(total, "pergunta", "perguntas")}, {Contar(obrigatorias, "obrigatória", "obrigatórias")}.", null));
    }

    private static void AdicionarAlternativas(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada)
    {
        var faltando = entrada.Perguntas.Where(p => p.Tipo == TipoDePergunta.MultiplaEscolha && p.Alternativas.Count < ValidacaoDaPergunta.MinimoDeAlternativas).OrderBy(p => p.Ordem).ToList();
        if (faltando.Count == 0)
        {
            itens.Add(new ItemDeVerificacao("ALTERNATIVAS", SituacaoDoItem.Ok, "Toda pergunta de múltipla escolha tem pelo menos 2 alternativas.", null));
            return;
        }

        itens.AddRange(faltando.Select(p => new ItemDeVerificacao("ALTERNATIVAS", SituacaoDoItem.Bloqueio, $"A pergunta {p.Ordem} é de múltipla escolha e precisa de pelo menos 2 alternativas.", p.Id)));
    }

    private static void AdicionarArquivoNoAnonimo(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada) =>
        itens.AddRange(entrada.Perguntas.Where(p => p.Tipo == TipoDePergunta.Arquivo).OrderBy(p => p.Ordem).Select(p => new ItemDeVerificacao("ANONIMO_SEM_ARQUIVO", SituacaoDoItem.Bloqueio, MensagensDosFormularios.AnonimoSemArquivo.Detalhe, p.Id)));

    private static void AdicionarResultadoMinimo(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada) =>
        itens.Add(new ItemDeVerificacao(
            "ANONIMO_RESULTADO_MINIMO",
            SituacaoDoItem.Aviso,
            entrada.Tipo == TipoDeFormulario.Avaliacao
                ? "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas e dos avaliados com 3 respostas ou mais."
                : "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais.",
            null));

    private static void AdicionarPublicoMinimo(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada)
    {
        if (entrada.Quem.Alvo is null)
        {
            itens.Add(new ItemDeVerificacao("ANONIMO_PUBLICO_MINIMO", SituacaoDoItem.Aviso, "Escolha pelo menos 5 pessoas na etapa Publicação.", null));
            return;
        }

        var pessoas = entrada.Quem.PessoasQueRespondem;
        itens.Add(pessoas >= MensagensDosFormularios.MinimoDePessoasNoAnonimo
            ? new ItemDeVerificacao("ANONIMO_PUBLICO_MINIMO", SituacaoDoItem.Ok, $"{pessoas} pessoas.", null)
            : new ItemDeVerificacao("ANONIMO_PUBLICO_MINIMO", SituacaoDoItem.Bloqueio, $"O formulário anônimo está para {Contar(pessoas, "pessoa", "pessoas")} e precisa de pelo menos {MensagensDosFormularios.MinimoDePessoasNoAnonimo}.", null));
    }

    private static void AdicionarDadoSensivel(List<ItemDeVerificacao> itens, EntradaDaVerificacao entrada)
    {
        var avisos = entrada.Perguntas.OrderBy(p => p.Ordem)
            .Select(p => (Pergunta: p, Categoria: DadoSensivel.Categoria(p.Texto + " " + string.Join(' ', p.Alternativas.Select(a => a.Texto)))))
            .Where(x => x.Categoria is not null)
            .Select(x => new ItemDeVerificacao("DADO_SENSIVEL", SituacaoDoItem.Aviso, $"A pergunta {x.Pergunta.Ordem} parece pedir dado de {x.Categoria}. Confira se ela é necessária.", x.Pergunta.Id))
            .ToList();
        if (avisos.Count == 0)
        {
            itens.Add(new ItemDeVerificacao("DADO_SENSIVEL", SituacaoDoItem.Ok, "Nenhuma pergunta pede dado sensível, como saúde, religião ou orientação sexual.", null));
            return;
        }

        itens.AddRange(avisos);
    }

    private static string Contar(int quantas, string singular, string plural) => $"{quantas} {(quantas == 1 ? singular : plural)}";
}

/// <summary>
/// A verificação do dado sensível por palavra-chave (hipótese do contrato, <c>x-provisorio</c>): saúde, religião e orientação sexual, sem diferenciar caixa nem acento. A palavra vale pelo começo (<c>religi</c> pega religião e religioso); as curtas valem inteiras
/// (<c>gay</c> não pega o nome de uma pessoa). Quem usa só avisa, nunca bloqueia.
/// </summary>
internal static class DadoSensivel
{
    private static readonly (string Categoria, Regex Padrao)[] Categorias =
    [
        ("saúde", Montar(["saude", "doenca", "doente", "medicament", "remedio", "diagnostic", "deficienc", "gravidez", "cirurgia", "depressao", "ansiedade", "psicolog"], [])),
        ("religião", Montar(["religi", "igreja", "crenca"], [])),
        ("orientação sexual", Montar(["orientacao sexual", "homossexual", "heterossexual", "bissexual", "lgbt"], ["gay", "lesbica"])),
    ];

    /// <summary>A categoria do primeiro padrão que o texto acusa, ou <c>null</c>.</summary>
    public static string? Categoria(string texto)
    {
        var normalizado = SemAcento(texto);
        return Categorias.FirstOrDefault(c => c.Padrao.IsMatch(normalizado)).Categoria;
    }

    private static Regex Montar(string[] comeco, string[] inteiras) =>
        new($@"\b(?:{string.Join('|', comeco.Select(Regex.Escape))}){(inteiras.Length > 0 ? $@"|\b(?:{string.Join('|', inteiras.Select(Regex.Escape))})\b" : string.Empty)}", RegexOptions.CultureInvariant | RegexOptions.Compiled);

    private static string SemAcento(string texto)
    {
        var decomposto = texto.Normalize(NormalizationForm.FormD);
        var sb = new StringBuilder(decomposto.Length);
        foreach (var c in decomposto)
        {
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)
            {
                sb.Append(char.ToLowerInvariant(c));
            }
        }

        return sb.ToString();
    }
}
