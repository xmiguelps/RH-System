using System.Linq.Expressions;

namespace RHSystem.Application.Comum.Consultas;

/// <summary>
/// Os valores de <c>ordenarPor</c> de uma rota (os do enum do contrato, como <c>NOME</c> e <c>DATA_ADMISSAO</c>) e a coluna de cada um. O
/// <c>desempate</c> é a chave única (o id) e entra sempre por último, para a paginação ser estável: sem ele, linhas empatadas na coluna podem repetir
/// ou sumir de uma página para a outra.
/// </summary>
public sealed class MapaDeOrdenacao<T>(string padrao, DirecaoDeOrdenacao direcaoPadrao, Expression<Func<T, object?>> desempate)
{
    private readonly Dictionary<string, Expression<Func<T, object?>>> _chaves = new(StringComparer.Ordinal);

    public MapaDeOrdenacao<T> Por(string nome, Expression<Func<T, object?>> chave)
    {
        _chaves[nome] = chave;
        return this;
    }

    /// <summary>
    /// Sem <c>ordenarPor</c>, vale a coluna e a direção padrão. Com <c>ordenarPor</c> e sem <c>direcao</c>, vale ASC (o contrato não declara padrão).
    /// Só <c>direcao</c>: a coluna padrão naquela direção.
    /// </summary>
    /// <exception cref="ErroDeValidacaoException"><c>ordenarPor</c> não é um dos valores aceitos da rota.</exception>
    /// <exception cref="InvalidOperationException">O padrão do mapa não foi registrado com <see cref="Por"/> (erro de programação).</exception>
    public IQueryable<T> Aplicar(IQueryable<T> consulta, ParametrosDeLista p)
    {
        var nome = p.OrdenarPor ?? padrao;
        if (!_chaves.TryGetValue(nome, out var chave))
        {
            if (p.OrdenarPor is null)
            {
                throw new InvalidOperationException($"A ordenação padrão \"{padrao}\" não foi registrada no mapa (use Por(\"{padrao}\", ...)).");
            }

            throw new ErroDeValidacaoException("ordenarPor", $"Valor inválido. Use um destes: {string.Join(", ", _chaves.Keys)}.");
        }

        var direcao = p.Direcao ?? (p.OrdenarPor is null ? direcaoPadrao : DirecaoDeOrdenacao.Asc);
        var ordenada = direcao == DirecaoDeOrdenacao.Desc ? consulta.OrderByDescending(chave) : consulta.OrderBy(chave);
        return ordenada.ThenBy(desempate);
    }
}
