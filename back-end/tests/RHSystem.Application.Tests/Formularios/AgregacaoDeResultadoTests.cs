using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>
/// As contas do resultado, sem banco: a múltipla escolha traz TODAS as alternativas (total e percentual de uma casa, a metade para cima); a escala traz a média de uma casa e SEMPRE os cinco valores (nula se ninguém respondeu); os textos saem em ordem alfabética, sem diferenciar maiúscula nem acento e com desempate pelo texto (nunca pela ordem de entrada);
/// o arquivo só tem a quantidade; com mínimo a pergunta com menos respostas some e conta como oculta; sem mínimo aparecem todas.
/// </summary>
public class AgregacaoDeResultadoTests
{
    private static readonly PerguntaLida Multipla = new(1, 1, "Recomenda?", TipoDePergunta.MultiplaEscolha, true, [new AlternativaLida(12, 2, "Talvez"), new AlternativaLida(11, 1, "Sim"), new AlternativaLida(13, 3, "Não")]);
    private static readonly PerguntaLida Escala = new(2, 2, "Nota", TipoDePergunta.Escala, true, []);
    private static readonly PerguntaLida Longa = new(3, 3, "Comentário", TipoDePergunta.RespostaLonga, false, []);
    private static readonly PerguntaLida Curta = new(4, 4, "Nome", TipoDePergunta.RespostaCurta, false, []);
    private static readonly PerguntaLida Anexo = new(5, 5, "Comprovante", TipoDePergunta.Arquivo, false, []);

    private static RespostaParaResultado R(int pergunta, string? texto = null, int? alternativa = null, int? avaliado = null) => new(pergunta, texto, alternativa, avaliado);

    private static IReadOnlyList<RespostaParaResultado> Muitas(int pergunta, int quantas, Func<int, RespostaParaResultado> fazer) => [.. Enumerable.Range(0, quantas).Select(fazer)];

    [Fact]
    public void A_multipla_escolha_traz_todas_as_alternativas_em_ordem_com_total_e_percentual_de_uma_casa()
    {
        var respostas = new List<RespostaParaResultado>();
        respostas.AddRange(Muitas(1, 55, _ => R(1, "Sim", 11)));
        respostas.AddRange(Muitas(1, 13, _ => R(1, "Talvez", 12)));
        respostas.AddRange(Muitas(1, 3, _ => R(1, "Não", 13)));

        var (perguntas, ocultas) = AgregacaoDeResultado.Agregar([Multipla], respostas, 3);

        var pergunta = Assert.Single(perguntas);
        Assert.Equal(0, ocultas);
        Assert.Equal(71, pergunta.TotalRespostas);
        Assert.Equal([("Sim", 55, 77.5m), ("Talvez", 13, 18.3m), ("Não", 3, 4.2m)], pergunta.Alternativas.Select(a => (a.Texto, a.Total, a.Percentual)));
        Assert.Equal([11, 12, 13], pergunta.Alternativas.Select(a => a.AlternativaId));
        Assert.Null(pergunta.Escala);
        Assert.Empty(pergunta.Textos);
    }

    [Fact]
    public void Alternativa_que_ninguem_escolheu_aparece_com_zero_e_sem_respostas_o_percentual_e_zero()
    {
        var (perguntas, _) = AgregacaoDeResultado.Agregar([Multipla], [R(1, "Sim", 11), R(1, "Sim", 11), R(1, "Não", 13)], null);
        var (vazia, _) = AgregacaoDeResultado.Agregar([Multipla], [], null);

        Assert.Equal([("Sim", 2, 66.7m), ("Talvez", 0, 0m), ("Não", 1, 33.3m)], perguntas[0].Alternativas.Select(a => (a.Texto, a.Total, a.Percentual)));
        Assert.Equal([0m, 0m, 0m], vazia[0].Alternativas.Select(a => a.Percentual));
    }

    [Theory]
    [InlineData(1, 8, 12.5)]
    [InlineData(1, 16, 6.3)]
    [InlineData(1, 3, 33.3)]
    [InlineData(2, 3, 66.7)]
    [InlineData(0, 5, 0)]
    [InlineData(5, 5, 100)]
    [InlineData(0, 0, 0)]
    public void O_percentual_tem_uma_casa_e_a_metade_vai_para_cima(int parte, int total, double esperado)
    {
        Assert.Equal((decimal)esperado, AgregacaoDeResultado.Percentual(parte, total));
    }

    [Fact]
    public void A_escala_traz_a_media_de_uma_casa_e_sempre_os_cinco_valores()
    {
        var (perguntas, _) = AgregacaoDeResultado.Agregar([Escala], [R(2, "4"), R(2, "5"), R(2, "5"), R(2, "1"), R(2, "4"), R(2, "5")], null);

        var escala = perguntas[0].Escala!;
        Assert.Equal(4.0m, escala.Media); // 24 / 6
        Assert.Equal([(1, 1), (2, 0), (3, 0), (4, 2), (5, 3)], escala.Distribuicao.Select(v => (v.Valor, v.Total)));
        Assert.Equal(6, perguntas[0].TotalRespostas);
    }

    [Theory]
    [InlineData("1,2,2", 1.7)]
    [InlineData("4,5", 4.5)]
    [InlineData("1,1,2", 1.3)]
    [InlineData("3", 3.0)]
    public void A_media_arredonda_a_metade_para_cima(string valores, double esperado)
    {
        var respostas = valores.Split(',').Select(v => R(2, v)).ToList();

        var (perguntas, _) = AgregacaoDeResultado.Agregar([Escala], respostas, null);

        Assert.Equal((decimal)esperado, perguntas[0].Escala!.Media);
    }

    [Fact]
    public void Escala_sem_resposta_valida_vem_nula_e_valor_fora_de_1_a_5_nao_conta()
    {
        var (nenhuma, _) = AgregacaoDeResultado.Agregar([Escala], [], null);
        var (lixo, _) = AgregacaoDeResultado.Agregar([Escala], [R(2, "9"), R(2, "x"), R(2, null), R(2, "3")], null);

        Assert.Null(nenhuma[0].Escala);
        Assert.Equal(3.0m, lixo[0].Escala!.Media);
        Assert.Equal(4, lixo[0].TotalRespostas); // o total é de respostas gravadas
    }

    [Fact]
    public void Os_textos_saem_em_ordem_alfabetica_sem_caixa_nem_acento_e_o_empate_nao_depende_da_ordem_de_entrada()
    {
        string[] entrada = ["Zebra", "abacate", "Mamão", "ábaco", "a", "A", "mamao"];

        var (a, _) = AgregacaoDeResultado.Agregar([Longa], [.. entrada.Select(t => R(3, t))], null);
        var (b, _) = AgregacaoDeResultado.Agregar([Longa], [.. entrada.Reverse().Select(t => R(3, t))], null);

        Assert.Equal(["A", "a", "abacate", "ábaco", "Mamão", "mamao", "Zebra"], a[0].Textos);
        Assert.Equal(a[0].Textos, b[0].Textos);
    }

    [Fact]
    public void O_arquivo_so_tem_a_quantidade()
    {
        var (perguntas, _) = AgregacaoDeResultado.Agregar([Anexo], [R(5, "a.pdf"), R(5, "b.pdf")], null);

        Assert.Equal((2, 0, 0), (perguntas[0].TotalRespostas, perguntas[0].Alternativas.Count, perguntas[0].Textos.Count));
        Assert.Null(perguntas[0].Escala);
    }

    [Fact]
    public void Com_minimo_a_pergunta_com_menos_respostas_some_e_conta_como_oculta_e_com_exatamente_o_minimo_aparece()
    {
        var respostas = new List<RespostaParaResultado>();
        respostas.AddRange(Muitas(3, 3, i => R(3, $"t{i}")));
        respostas.AddRange(Muitas(4, 2, i => R(4, $"n{i}")));

        var (perguntas, ocultas) = AgregacaoDeResultado.Agregar([Longa, Curta, Anexo], respostas, 3);

        Assert.Equal([3], perguntas.Select(p => p.PerguntaId));
        Assert.Equal(2, ocultas); // a curta (2 respostas) e o arquivo (0)
    }

    [Fact]
    public void Sem_minimo_aparecem_todas_na_ordem_da_pergunta_mesmo_sem_resposta()
    {
        var (perguntas, ocultas) = AgregacaoDeResultado.Agregar([Escala, Multipla, Longa], [], null);

        Assert.Equal([1, 2, 3], perguntas.Select(p => p.PerguntaId));
        Assert.Equal(0, ocultas);
    }

    [Fact]
    public void Nenhuma_pergunta_com_minimo_deixa_a_lista_vazia()
    {
        var (perguntas, ocultas) = AgregacaoDeResultado.Agregar([Longa, Curta], [R(3, "x"), R(4, "y")], AgregacaoDeResultado.MinimoNoAnonimo);

        Assert.Empty(perguntas);
        Assert.Equal(2, ocultas);
        Assert.Equal(3, AgregacaoDeResultado.MinimoNoAnonimo);
    }
}
