using System.Globalization;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// As respostas de UM envio como as telas de "ver respostas" as mostram (a da própria pessoa e a do RH): a lista sai das PERGUNTAS do formulário, em ordem, não das linhas gravadas, então a pergunta opcional deixada em branco (e a que foi acrescentada depois) vem com tudo vazio. Só o campo do tipo
/// vem preenchido: <c>texto</c>, a alternativa escolhida, o número da escala ou o arquivo (só <c>respostaId</c> e nome: o caminho nunca sai).
/// </summary>
public static class MontadorDeRespostasDadas
{
    public static IReadOnlyList<RespostaFormularioDada> Montar(FormularioLido lido, IReadOnlyList<RespostaLida> lidas)
    {
        var porPergunta = lidas.GroupBy(l => l.PerguntaId).ToDictionary(g => g.Key, g => g.First());
        return [.. lido.Perguntas.OrderBy(p => p.Ordem).Select(p => Uma(p, porPergunta.GetValueOrDefault(p.Id)))];
    }

    private static RespostaFormularioDada Uma(PerguntaLida pergunta, RespostaLida? linha)
    {
        if (linha is null)
        {
            return new RespostaFormularioDada(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, null, [], null, null);
        }

        return pergunta.Tipo switch
        {
            TipoDePergunta.RespostaCurta or TipoDePergunta.RespostaLonga => new RespostaFormularioDada(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, linha.Resposta, [], null, null),
            TipoDePergunta.MultiplaEscolha => new RespostaFormularioDada(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, null, [.. pergunta.Alternativas.Where(a => a.Id == linha.AlternativaId).Select(a => new PerguntaAlternativa(a.Id, a.Ordem, a.Texto))], null, null),
            TipoDePergunta.Escala => new RespostaFormularioDada(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, null, [], int.TryParse(linha.Resposta, NumberStyles.None, CultureInfo.InvariantCulture, out var valor) ? valor : null, null),
            _ => new RespostaFormularioDada(pergunta.Id, pergunta.Ordem, pergunta.Texto, pergunta.Tipo, null, [], null, linha.NomeArquivoOriginal is { } nome ? new RespostaFormularioArquivo(linha.Id, nome) : null),
        };
    }
}
