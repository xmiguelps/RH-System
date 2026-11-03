using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Listas;

/// <summary>
/// Paginação e busca no banco REAL, usando <c>SETORES</c> (nome único) como tabela de exemplo. Um banco por classe de teste: os testes só
/// LEEM os setores semeados e escolhem as linhas pelo nome.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ListasEfTests(MySqlFixture mysql)
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static string? _conexao;

    private async Task<string> ConexaoAsync()
    {
        await Trava.WaitAsync();
        try
        {
            if (_conexao is null)
            {
                var conexao = await mysql.NovoBancoComScriptsAsync();
                await SemearAsync(conexao);
                _conexao = conexao;
            }

            return _conexao;
        }
        finally
        {
            Trava.Release();
        }
    }

    private static async Task SemearAsync(string conexao)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();

        // 25 setores "Pag 01".."Pag 25" para a paginação; os demais são o material das buscas.
        var nomes = Enumerable.Range(1, 25).Select(n => $"Pag {n:D2}")
            .Concat(["100%", "1000", "a_b", "axb", "c\\d", "cxd", "José"]);
        foreach (var nome in nomes)
        {
            await using var comando = new MySqlCommand("INSERT INTO SETORES (NOME_SETOR) VALUES (@nome)", c);
            comando.Parameters.AddWithValue("@nome", nome);
            await comando.ExecuteNonQueryAsync();
        }
    }

    private async Task<List<string>> BuscarAsync(string busca)
    {
        await using var db = ContextoDeTeste.Novo(await ConexaoAsync());
        return await db.Setores
            .Where(s => EF.Functions.Like(s.Nome, BuscaLike.Contem(busca)))
            .OrderBy(s => s.Nome)
            .Select(s => s.Nome)
            .ToListAsync();
    }

    [Fact]
    public async Task ToRespostaPaginadaAsync_devolve_total_totalPaginas_e_pagina_alem_do_fim_com_itens_vazios()
    {
        // Review Focus 5.
        await using var db = ContextoDeTeste.Novo(await ConexaoAsync());
        IQueryable<Setor> Paginados() => db.Setores.Where(s => s.Nome.StartsWith("Pag ")).OrderBy(s => s.Id);

        var primeira = await Paginados().ToRespostaPaginadaAsync(new ParametrosDeLista(1, 10), s => s.Nome, CancellationToken.None);
        var terceira = await Paginados().ToRespostaPaginadaAsync(new ParametrosDeLista(3, 10), s => s.Nome, CancellationToken.None);
        var alemDoFim = await Paginados().ToRespostaPaginadaAsync(new ParametrosDeLista(4, 10), s => s.Nome, CancellationToken.None);
        var vazia = await db.Setores.Where(s => s.Nome == "não existe").OrderBy(s => s.Id)
            .ToRespostaPaginadaAsync(new ParametrosDeLista(), s => s.Nome, CancellationToken.None);

        Assert.Equal(Enumerable.Range(1, 10).Select(n => $"Pag {n:D2}"), primeira.Itens);
        Assert.Equal((1, 10, 25, 3), (primeira.Pagina, primeira.TamanhoPagina, primeira.Total, primeira.TotalPaginas));
        Assert.Equal(Enumerable.Range(21, 5).Select(n => $"Pag {n:D2}"), terceira.Itens);
        Assert.Empty(alemDoFim.Itens);
        Assert.Equal((4, 25, 3), (alemDoFim.Pagina, alemDoFim.Total, alemDoFim.TotalPaginas));
        Assert.Empty(vazia.Itens);
        Assert.Equal((0, 0), (vazia.Total, vazia.TotalPaginas));
    }

    [Fact]
    public async Task Busca_com_percentual_e_sublinhado_procura_o_caractere_literal()
    {
        Assert.Equal(["100%"], await BuscarAsync("100%")); // "100%" não casa "1000"
        Assert.Equal(["a_b"], await BuscarAsync("a_b")); // "a_b" não casa "axb"
    }

    [Fact]
    public async Task Busca_com_barra_invertida_procura_a_barra_literal()
    {
        Assert.Equal(["c\\d"], await BuscarAsync("c\\d")); // "c\d" não casa "cxd"
    }

    [Fact]
    public async Task Busca_ignora_maiusculas_e_acentos()
    {
        // utf8mb4_unicode_ci: "jose" acha "José".
        Assert.Equal(["José"], await BuscarAsync("jose"));
        Assert.Equal(["José"], await BuscarAsync("JOSÉ"));
    }

    [Fact]
    public async Task Mapa_de_ordenacao_vira_ORDER_BY_no_banco_com_chave_de_texto_e_de_inteiro()
    {
        // As chaves são Expression<Func<T, object?>>: uma coluna int entra embrulhada num Convert para object, e o EF precisa desembrulhá-lo.
        await using var db = ContextoDeTeste.Novo(await ConexaoAsync());
        var mapa = new MapaDeOrdenacao<Setor>("NOME", DirecaoDeOrdenacao.Asc, s => s.Id).Por("NOME", s => s.Nome).Por("ID", s => s.Id);
        IQueryable<Setor> Paginados() => db.Setores.Where(s => s.Nome.StartsWith("Pag "));

        var porNomeDescendente = await mapa.Aplicar(Paginados(), new ParametrosDeLista(OrdenarPor: "NOME", Direcao: DirecaoDeOrdenacao.Desc))
            .Take(2).Select(s => s.Nome).ToListAsync();
        var porIdAscendente = await mapa.Aplicar(Paginados(), new ParametrosDeLista(OrdenarPor: "ID")).Take(2).Select(s => s.Nome).ToListAsync();
        var porIdDescendente = await mapa.Aplicar(Paginados(), new ParametrosDeLista(OrdenarPor: "ID", Direcao: DirecaoDeOrdenacao.Desc))
            .Take(2).Select(s => s.Nome).ToListAsync();

        Assert.Equal(["Pag 25", "Pag 24"], porNomeDescendente);
        Assert.Equal(["Pag 01", "Pag 02"], porIdAscendente);
        Assert.Equal(["Pag 25", "Pag 24"], porIdDescendente);
    }

    [Fact]
    public void BuscaLike_escapa_barra_percentual_e_sublinhado_e_cerca_com_percentual()
    {
        Assert.Equal("%a\\_b\\%c\\\\d%", BuscaLike.Contem("a_b%c\\d"));
        Assert.Equal("%%", BuscaLike.Contem(string.Empty));
    }
}
