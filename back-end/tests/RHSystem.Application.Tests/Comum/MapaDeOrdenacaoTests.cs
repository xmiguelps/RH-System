using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Tests.Comum;

public class MapaDeOrdenacaoTests
{
    private sealed record Pessoa(int Id, string Nome, DateTime Admissao);

    private static MapaDeOrdenacao<Pessoa> Mapa() =>
        new MapaDeOrdenacao<Pessoa>("NOME", DirecaoDeOrdenacao.Asc, p => p.Id)
            .Por("NOME", p => p.Nome)
            .Por("DATA_ADMISSAO", p => p.Admissao);

    private static readonly Pessoa[] Pessoas =
    [
        new(1, "Carla", new DateTime(2024, 3, 1)),
        new(2, "Ana", new DateTime(2026, 1, 15)),
        new(3, "Bia", new DateTime(2025, 6, 30)),
    ];

    private static List<int> Ids(IEnumerable<Pessoa> pessoas) => pessoas.Select(p => p.Id).ToList();

    [Fact]
    public void Ordena_pela_coluna_e_pela_direcao_pedidas_e_usa_o_padrao_sem_parametro()
    {
        var fonte = Pessoas.AsQueryable();

        Assert.Equal([2, 3, 1], Ids(Mapa().Aplicar(fonte, new ParametrosDeLista())));                                                      // padrão: NOME ASC
        Assert.Equal([1, 3, 2], Ids(Mapa().Aplicar(fonte, new ParametrosDeLista(OrdenarPor: "NOME", Direcao: DirecaoDeOrdenacao.Desc))));
        Assert.Equal([1, 3, 2], Ids(Mapa().Aplicar(fonte, new ParametrosDeLista(Direcao: DirecaoDeOrdenacao.Desc))));                      // só a direção: a coluna padrão
        Assert.Equal([1, 3, 2], Ids(Mapa().Aplicar(fonte, new ParametrosDeLista(OrdenarPor: "DATA_ADMISSAO"))));                          // coluna sem direção: ASC
        Assert.Equal([2, 3, 1], Ids(Mapa().Aplicar(fonte, new ParametrosDeLista(OrdenarPor: "DATA_ADMISSAO", Direcao: DirecaoDeOrdenacao.Desc))));
    }

    [Fact]
    public void Direcao_padrao_do_mapa_vale_quando_nao_ha_ordenarPor_nem_direcao()
    {
        var descendente = new MapaDeOrdenacao<Pessoa>("DATA_ADMISSAO", DirecaoDeOrdenacao.Desc, p => p.Id).Por("DATA_ADMISSAO", p => p.Admissao);

        Assert.Equal([2, 3, 1], Ids(descendente.Aplicar(Pessoas.AsQueryable(), new ParametrosDeLista())));
    }

    [Fact]
    public void OrdenarPor_desconhecido_lanca_ErroDeValidacao_listando_os_valores_aceitos()
    {
        var erro = Assert.Throws<ErroDeValidacaoException>(() => Mapa().Aplicar(Pessoas.AsQueryable(), new ParametrosDeLista(OrdenarPor: "SALARIO")));

        Assert.Equal(["ordenarPor"], erro.Erros.Keys);
        var mensagem = Assert.Single(erro.Erros["ordenarPor"]);
        Assert.Contains("NOME", mensagem);
        Assert.Contains("DATA_ADMISSAO", mensagem);
        Assert.DoesNotContain("SALARIO", mensagem);
    }

    [Fact]
    public void Empate_na_coluna_ordena_pelo_desempate_e_as_paginas_nao_repetem_nem_pulam_itens()
    {
        // Review Focus 5: dez pessoas com o MESMO nome, ids fora de ordem. Sem desempate a ordem dentro do empate fica a critério do banco e a
        // paginação pode repetir ou perder gente de uma página para a outra.
        var empatados = new[] { 7, 3, 10, 1, 8, 5, 2, 9, 4, 6 }.Select(id => new Pessoa(id, "Igual", new DateTime(2026, 1, 1))).AsQueryable();
        var vistos = new List<int>();

        for (var pagina = 1; pagina <= 4; pagina++)
        {
            var p = new ParametrosDeLista(pagina, 3);
            vistos.AddRange(Ids(Mapa().Aplicar(empatados, p).Skip(p.Pular).Take(p.TamanhoPagina)));
        }

        Assert.Equal(Enumerable.Range(1, 10), vistos);
    }

    [Fact]
    public void Padrao_que_nao_foi_registrado_e_erro_de_programacao_e_nao_de_validacao()
    {
        var mapa = new MapaDeOrdenacao<Pessoa>("NOME", DirecaoDeOrdenacao.Asc, p => p.Id).Por("DATA_ADMISSAO", p => p.Admissao);

        Assert.Throws<InvalidOperationException>(() => mapa.Aplicar(Pessoas.AsQueryable(), new ParametrosDeLista()));
    }
}
