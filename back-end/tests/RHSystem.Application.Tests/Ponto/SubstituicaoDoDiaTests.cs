using System.Globalization;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Tests.Ponto;

/// <summary>A correção direta do RH: a lista substitui o dia. Os quatro casos (corrigida, igual, nova, ausente) e as recusas, tudo em memória.</summary>
public class SubstituicaoDoDiaTests
{
    private static int _id;

    private static DateTime Utc(string texto) => DateTime.Parse(texto, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal);

    private static BatidaLida Atual(TipoDeMarcacao tipo, string utc) => new(++_id, Utc(utc), tipo, OrigemDaMarcacao.Sistema, null);

    private static BatidaNaLista Item(int? id, TipoDeMarcacao tipo, string utc) => new(id, Utc(utc), tipo);

    [Fact]
    public void Os_quatro_casos_corrigida_igual_nova_e_ausente()
    {
        var entrada = Atual(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z");
        var saidaDoAlmoco = Atual(TipoDeMarcacao.Saida, "2026-10-05T15:00:00Z");
        var volta = Atual(TipoDeMarcacao.Entrada, "2026-10-05T16:00:00Z"); // sai da lista: apagada
        var saida = Atual(TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z");

        var resultado = SubstituicaoDoDia.Substituir(
            [entrada, saidaDoAlmoco, volta, saida],
            [Item(entrada.Id, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(saidaDoAlmoco.Id, TipoDeMarcacao.Saida, "2026-10-05T15:05:00Z"), Item(null, TipoDeMarcacao.Entrada, "2026-10-05T16:10:00Z"), Item(saida.Id, TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([(entrada.Id, false), (saidaDoAlmoco.Id, true), ((int?)null, true), (saida.Id, false)], resultado.Dia.Select(b => (b.Id, b.Mudou))); // igual não muda, corrigida e nova mudam
        Assert.Equal([volta.Id], resultado.Removidas);
    }

    [Fact]
    public void Corrigir_so_o_tipo_tambem_e_correcao_e_pode_consertar_a_sequencia()
    {
        var primeira = Atual(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z");
        var segunda = Atual(TipoDeMarcacao.Entrada, "2026-10-05T20:00:00Z");

        var resultado = SubstituicaoDoDia.Substituir([primeira, segunda], [Item(primeira.Id, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(segunda.Id, TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([false, true], resultado.Dia.Select(b => b.Mudou));
    }

    [Fact]
    public void Lista_vazia_limpa_o_dia_sem_erro_e_apaga_todas()
    {
        var entrada = Atual(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z");
        var saida = Atual(TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z");

        var resultado = SubstituicaoDoDia.Substituir([entrada, saida], []);

        Assert.Null(resultado.Erro);
        Assert.Empty(resultado.Dia);
        Assert.Equal([entrada.Id, saida.Id], resultado.Removidas);
    }

    [Fact]
    public void Dia_sem_batida_recebe_a_lista_inteira_como_nova()
    {
        var resultado = SubstituicaoDoDia.Substituir([], [Item(null, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(null, TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([true, true], resultado.Dia.Select(b => b.Mudou));
        Assert.Empty(resultado.Removidas);
    }

    [Fact]
    public void Id_de_batida_que_nao_e_do_dia_ou_repetido_e_erro()
    {
        var entrada = Atual(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z");

        var deOutroDia = SubstituicaoDoDia.Substituir([entrada], [Item(999999, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z")]);
        var repetido = SubstituicaoDoDia.Substituir([entrada], [Item(entrada.Id, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(entrada.Id, TipoDeMarcacao.Saida, "2026-10-05T12:00:00Z")]);

        Assert.Equal((ErroDeAplicacao.MarcacaoInexistenteNoDia, ErroDeAplicacao.MarcacaoRepetida), (deOutroDia.Erro, repetido.Erro));
    }

    [Fact]
    public void Duas_batidas_no_mesmo_segundo_sao_duplicadas_inclusive_com_fracao()
    {
        var entrada = Atual(TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z");

        var duasNovas = SubstituicaoDoDia.Substituir([], [Item(null, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(null, TipoDeMarcacao.Saida, "2026-10-05T11:00:00Z")]);
        var comFracao = SubstituicaoDoDia.Substituir([entrada], [Item(entrada.Id, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), new BatidaNaLista(null, Utc("2026-10-05T11:00:00Z").AddMilliseconds(900), TipoDeMarcacao.Saida)]);

        Assert.Equal((ErroDeAplicacao.BatidaDuplicada, ErroDeAplicacao.BatidaDuplicada), (duasNovas.Erro, comFracao.Erro));
    }

    [Fact]
    public void Sequencia_quebrada_e_erro_mas_so_se_o_dia_nao_fica_vazio()
    {
        var quebrada = SubstituicaoDoDia.Substituir([], [Item(null, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z"), Item(null, TipoDeMarcacao.Entrada, "2026-10-05T12:00:00Z"), Item(null, TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z")]);
        var aberta = SubstituicaoDoDia.Substituir([], [Item(null, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z")]);

        Assert.Equal((ErroDeAplicacao.SequenciaInvalida, ErroDeAplicacao.SequenciaInvalida), (quebrada.Erro, aberta.Erro));
    }

    [Fact]
    public void A_ordem_da_lista_nao_importa_o_dia_sai_por_horario()
    {
        var resultado = SubstituicaoDoDia.Substituir([], [Item(null, TipoDeMarcacao.Saida, "2026-10-05T20:00:00Z"), Item(null, TipoDeMarcacao.Entrada, "2026-10-05T11:00:00Z")]);

        Assert.Null(resultado.Erro);
        Assert.Equal([TipoDeMarcacao.Entrada, TipoDeMarcacao.Saida], resultado.Dia.Select(b => b.Tipo));
    }
}
