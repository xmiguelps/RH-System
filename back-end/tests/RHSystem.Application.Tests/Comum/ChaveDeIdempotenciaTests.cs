using RHSystem.Application.Comum;

namespace RHSystem.Application.Tests.Comum;

public class ChaveDeIdempotenciaTests
{
    [Fact]
    public void Montar_junta_tipo_e_partes_em_ASCII_na_ordem_dada()
    {
        Assert.Equal("AVISO_PUBLICADO:aviso=15:func=21", ChaveDeIdempotencia.Montar("AVISO_PUBLICADO", ("aviso", 15), ("func", 21)));
        Assert.Equal("CONVITE:func=7", ChaveDeIdempotencia.Montar("CONVITE", ("func", 7)));
        Assert.Equal("LEMBRETE:dia=2026-10-07:livre=", ChaveDeIdempotencia.Montar("LEMBRETE", ("dia", new DateOnly(2026, 10, 7)), ("livre", null))); // valor nulo vira vazio; data em texto invariante
    }

    [Fact]
    public void Montar_aceita_tipo_de_50_caracteres_e_recusa_o_de_51_e_o_de_2()
    {
        var com50 = "T" + new string('A', 49);

        Assert.StartsWith(com50 + ":", ChaveDeIdempotencia.Montar(com50, ("id", 1)));
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar(com50 + "A", ("id", 1)));
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("AB", ("id", 1)));
        Assert.StartsWith("ABC:", ChaveDeIdempotencia.Montar("ABC", ("id", 1))); // 3 é o mínimo
    }

    [Fact]
    public void Montar_recusa_tipo_fora_do_formato_acento_e_chave_acima_de_190_caracteres()
    {
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("aviso_publicado", ("id", 1))); // minúsculas
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("AVISO-PUBLICADO", ("id", 1))); // hífen
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("1AVISO", ("id", 1))); // começa por número
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("AVISÃO", ("id", 1))); // acento no tipo
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("AVISO_PUBLICADO", ("nome", "João"))); // acento no valor
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("AVISO_PUBLICADO", ("texto", new string('x', 200)))); // passa de 190
        Assert.Equal(190, ChaveDeIdempotencia.Montar("TIPO", ("t", new string('x', 190 - "TIPO:t=".Length))).Length); // 190 ainda cabe
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("TIPO", ("t", new string('x', 191 - "TIPO:t=".Length)))); // 191 não
    }

    [Fact]
    public void Montar_recusa_chave_sem_nenhuma_parte()
    {
        // Sem parte nenhuma, toda mensagem do tipo teria a MESMA chave e só a primeira entraria.
        Assert.Throws<ArgumentException>(() => ChaveDeIdempotencia.Montar("CONVITE"));
    }
}
