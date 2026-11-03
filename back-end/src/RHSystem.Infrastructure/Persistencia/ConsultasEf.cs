using System.Linq.Expressions;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Infrastructure.Persistencia;

public static class ConsultasEf
{
    /// <summary>
    /// Conta o total e devolve só a página pedida, com a ordenação que o chamador JÁ aplicou (use o <see cref="MapaDeOrdenacao{T}"/>: sem ordem
    /// total a paginação repete ou perde linhas). Uma página além do fim devolve <c>Itens</c> vazio e o <c>Total</c> verdadeiro.
    /// </summary>
    public static async Task<RespostaPaginada<TDto>> ToRespostaPaginadaAsync<T, TDto>(
        this IQueryable<T> consulta,
        ParametrosDeLista p,
        Expression<Func<T, TDto>> projecao,
        CancellationToken ct)
    {
        var total = await consulta.CountAsync(ct);
        var itens = await consulta.Skip(p.Pular).Take(p.TamanhoPagina).Select(projecao).ToListAsync(ct);
        return RespostaPaginada<TDto>.De(itens, total, p);
    }
}

public static class BuscaLike
{
    /// <summary>
    /// <c>%texto%</c> com <c>\</c>, <c>%</c> e <c>_</c> escapados, para a busca procurar o caractere literal (<c>100%</c> não pode casar <c>1000</c>).
    /// Use com <c>EF.Functions.Like(coluna, BuscaLike.Contem(busca))</c>. A comparação ignora maiúsculas e acentos (<c>utf8mb4_unicode_ci</c>).
    /// </summary>
    public static string Contem(string texto) =>
        "%" + texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("%", "\\%", StringComparison.Ordinal).Replace("_", "\\_", StringComparison.Ordinal) + "%";
}
