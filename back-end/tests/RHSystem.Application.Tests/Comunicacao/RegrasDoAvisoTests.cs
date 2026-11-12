using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Tests.Comunicacao;

/// <summary>As regras puras dos avisos, sem banco: a situação calculada das datas e do relógio, o fim do dia da expiração (-03:00), a prévia do aviso detalhado (com os dois exemplos do contrato) e a validação dos campos.</summary>
public class RegrasDoAvisoTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    /// <summary>Um emoji tem dois pedaços UTF-16 e é um caractere: a conta do banco (VARCHAR) é por caractere.</summary>
    private static string Emojis(int quantidade) => string.Concat(Enumerable.Repeat("😀", quantidade));

    // ---------------------------------------------------------------- situação

    [Fact]
    public void Rascunho_e_o_que_nao_foi_enviado_qualquer_que_seja_a_data()
    {
        Assert.Equal(SituacaoDoAviso.Rascunho, RegraDaSituacaoDoAviso.De(false, null, null, Agora));
        Assert.Equal(SituacaoDoAviso.Rascunho, RegraDaSituacaoDoAviso.De(false, Agora.AddDays(3), null, Agora)); // com data prevista
        Assert.Equal(SituacaoDoAviso.Rascunho, RegraDaSituacaoDoAviso.De(false, Agora.AddDays(-3), Agora.AddDays(-1), Agora)); // com datas passadas
    }

    [Fact]
    public void Agendado_e_o_enviado_com_publicacao_depois_de_agora_e_publicar_no_proprio_instante_ja_e_ativo()
    {
        Assert.Equal(SituacaoDoAviso.Agendado, RegraDaSituacaoDoAviso.De(true, Agora.AddSeconds(1), null, Agora));
        Assert.Equal(SituacaoDoAviso.Ativo, RegraDaSituacaoDoAviso.De(true, Agora, null, Agora));
        Assert.Equal(SituacaoDoAviso.Ativo, RegraDaSituacaoDoAviso.De(true, Agora.AddSeconds(-1), null, Agora));
    }

    [Fact]
    public void Expirado_e_a_expiracao_ate_agora_e_encerrar_agora_ja_sai_expirado()
    {
        var publicado = Agora.AddDays(-5);

        Assert.Equal(SituacaoDoAviso.Expirado, RegraDaSituacaoDoAviso.De(true, publicado, Agora, Agora));
        Assert.Equal(SituacaoDoAviso.Expirado, RegraDaSituacaoDoAviso.De(true, publicado, Agora.AddSeconds(-1), Agora));
        Assert.Equal(SituacaoDoAviso.Ativo, RegraDaSituacaoDoAviso.De(true, publicado, Agora.AddSeconds(1), Agora));
        Assert.Equal(SituacaoDoAviso.Ativo, RegraDaSituacaoDoAviso.De(true, publicado, null, Agora)); // sem expiração nunca vence
    }

    [Fact]
    public void Enviado_sem_data_de_publicacao_e_ativo_e_o_agendado_vence_o_expirado()
    {
        Assert.Equal(SituacaoDoAviso.Ativo, RegraDaSituacaoDoAviso.De(true, null, null, Agora));
        Assert.Equal(SituacaoDoAviso.Agendado, RegraDaSituacaoDoAviso.De(true, Agora.AddDays(2), Agora.AddDays(3), Agora));
    }

    // ---------------------------------------------------------------- fim do dia

    [Fact]
    public void O_fim_do_dia_da_expiracao_e_23_59_59_em_menos_3_horas()
    {
        Assert.Equal(new DateTime(2026, 10, 19, 2, 59, 59, DateTimeKind.Utc), ExpiracaoDoAviso.FimDoDia(new DateOnly(2026, 10, 18)));
        Assert.Equal(DateTimeKind.Utc, ExpiracaoDoAviso.FimDoDia(new DateOnly(2026, 10, 18)).Kind);
        Assert.Equal(new DateTime(2027, 1, 1, 2, 59, 59, DateTimeKind.Utc), ExpiracaoDoAviso.FimDoDia(new DateOnly(2026, 12, 31))); // virada de ano
    }

    // ---------------------------------------------------------------- prévia

    [Fact]
    public void A_previa_do_simples_e_a_mensagem_inteira()
    {
        Assert.Equal("Reunião geral amanhã às 9h.", PreviaDoAviso.Montar(TipoDeAviso.Simples, "  Reunião geral amanhã às 9h.  "));
        Assert.Equal("## Não é título\nem aviso simples", PreviaDoAviso.Montar(TipoDeAviso.Simples, "## Não é título\nem aviso simples")); // o simples é texto: nada de markdown a tirar
    }

    [Fact]
    public void Exemplo_398_do_contrato_tira_os_titulos_e_junta_os_paragrafos_com_espaco()
    {
        var conteudo = "## Quando\nDias 13 e 14/10, das 09:00 às 16:00, na sala de treinamento.\n\n## Quem pode\nTodos os colaboradores. Dependentes podem se inscrever com o RH até 10/10.";

        Assert.Equal("Dias 13 e 14/10, das 09:00 às 16:00, na sala de treinamento. Todos os colaboradores. Dependentes podem se inscrever com o RH até 10/10.", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, conteudo));
    }

    [Fact]
    public void Exemplo_409_do_contrato_para_no_primeiro_paragrafo_porque_o_segundo_passaria_de_200()
    {
        var conteudo = "## O que mudou\nQuem trabalha nos setores administrativos pode pedir até 2 dias de trabalho remoto por semana, combinados com o gestor.\n\n## Como pedir\nPelo portal, com pelo menos 2 dias úteis de antecedência. O gestor aprova ou recusa em até 1 dia útil.\n\n## Quem continua presencial\nLoja, logística e recepção, por causa do atendimento ao público.";

        Assert.Equal("Quem trabalha nos setores administrativos pode pedir até 2 dias de trabalho remoto por semana, combinados com o gestor.", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, conteudo));
    }

    [Fact]
    public void Paragrafos_inteiros_enquanto_couber_em_200_com_o_espaco_entre_eles()
    {
        var p1 = new string('a', 100);
        var p2 = new string('b', 99); // 100 + 1 + 99 = 200: cabe
        var p3 = "c";

        Assert.Equal($"{p1} {p2}", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, $"{p1}\n\n{p2}\n\n{p3}")); // o terceiro passaria de 200
        Assert.Equal(p1, PreviaDoAviso.Montar(TipoDeAviso.Detalhado, $"{p1}\n\n{new string('b', 100)}")); // 201: o segundo não cabe
        Assert.Equal($"{p1} c d", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, $"{p1}\n\nc\n\nd")); // vários pequenos entram
    }

    [Fact]
    public void Se_o_primeiro_paragrafo_sozinho_passa_de_200_corta_em_200_sem_reticencias()
    {
        var longo = new string('x', 250);

        Assert.Equal(new string('x', 200), PreviaDoAviso.Montar(TipoDeAviso.Detalhado, longo));
        Assert.Equal(new string('x', 199), PreviaDoAviso.Montar(TipoDeAviso.Detalhado, new string('x', 199) + " " + new string('y', 50))); // o corte cai no espaço: sem espaço sobrando
        Assert.Equal(Emojis(200), PreviaDoAviso.Montar(TipoDeAviso.Detalhado, Emojis(250))); // conta caracteres, não pedaços UTF-16
    }

    [Fact]
    public void A_previa_e_texto_puro_sem_marcas_de_markdown()
    {
        var conteudo = "### Atenção\n**Importante:** leia o [manual](https://exemplo.com/manual) e veja _tudo_ em `config`.\n- primeiro item\n- segundo item\n1. passo um\n> citação\n![logo](https://exemplo.com/logo.png)";

        Assert.Equal("Importante: leia o manual e veja tudo em config. primeiro item segundo item passo um citação logo", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, conteudo));
    }

    [Fact]
    public void Conteudo_so_de_titulos_usa_o_texto_dos_titulos_e_a_previa_nunca_sai_vazia()
    {
        Assert.Equal("Só um título", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "## Só um título"));
        Assert.Equal("Um Dois", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "# Um\n\n## Dois"));
    }

    [Fact]
    public void Quebras_de_linha_do_windows_valem_como_as_do_unix()
    {
        Assert.Equal("Primeiro. Segundo.", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "## Título\r\nPrimeiro.\r\n\r\nSegundo."));
    }

    [Fact]
    public void Linha_em_branco_com_espacos_tambem_separa_paragrafos_e_o_cr_sozinho_vale_como_quebra()
    {
        var a = new string('a', 150);

        Assert.Equal(a, PreviaDoAviso.Montar(TipoDeAviso.Detalhado, $"{a}\n  \n{new string('b', 100)}")); // o segundo parágrafo passaria de 200
        Assert.Equal(a, PreviaDoAviso.Montar(TipoDeAviso.Detalhado, $"{a}\r\r{new string('b', 100)}"));
    }

    [Fact]
    public void Titulo_de_seis_niveis_sai_como_os_outros()
    {
        Assert.Equal("Texto.", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "###### Pequeno\nTexto."));
    }

    [Fact]
    public void Regra_horizontal_e_cerca_de_codigo_saem_e_o_codigo_de_dentro_fica()
    {
        Assert.Equal("Antes codigo Depois", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "Antes\n\n---\n\n```\ncodigo\n```\n\nDepois"));
    }

    [Fact]
    public void Italico_com_asterisco_e_riscado_saem_e_espacos_repetidos_viram_um()
    {
        Assert.Equal("Isto é importante e errado mesmo.", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "Isto é *importante* e ~~errado~~ mesmo."));
        Assert.Equal("muito espaço aqui", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "muito    espaço\t aqui"));
    }

    [Fact]
    public void Sublinhado_dentro_de_palavra_nao_e_enfase()
    {
        Assert.Equal("use snake_case_name e ênfase aqui", PreviaDoAviso.Montar(TipoDeAviso.Detalhado, "use snake_case_name e _ênfase_ aqui"));
    }

    // ---------------------------------------------------------------- validação

    private static (CamposDoAviso? Campos, IReadOnlyDictionary<string, string[]> Erros) Validar(TipoDeAviso tipo, string? titulo, string? conteudo, DateOnly? expiraEm = null, DateOnly? publicacao = null)
    {
        var coletor = new ColetorDeErros();
        var campos = ValidacaoDoAviso.Validar(coletor, tipo, titulo, conteudo, expiraEm, publicacao ?? new DateOnly(2026, 10, 7));
        if (!coletor.TemErros)
        {
            return (campos, new Dictionary<string, string[]>());
        }

        Assert.Null(campos); // com erro, nunca devolve campos
        return (campos, Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver).Erros);
    }

    [Fact]
    public void Campos_validos_voltam_aparados()
    {
        var (campos, erros) = Validar(TipoDeAviso.Detalhado, "  Recesso  ", "  ## Datas\nO escritório fecha.  ", new DateOnly(2026, 12, 31), new DateOnly(2026, 12, 1));

        Assert.Empty(erros);
        Assert.Equal(new CamposDoAviso("Recesso", "## Datas\nO escritório fecha."), campos);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Titulo_vazio_e_erro_no_campo_titulo(string? titulo)
    {
        var (_, erros) = Validar(TipoDeAviso.Simples, titulo, "Texto");

        Assert.Equal("Informe o título do aviso, com até 200 caracteres.", Assert.Single(erros["titulo"]));
        Assert.Single(erros);
    }

    [Fact]
    public void Titulo_vai_ate_200_caracteres_contando_caracteres_e_nao_pedacos_utf16()
    {
        Assert.Empty(Validar(TipoDeAviso.Simples, new string('t', 200), "Texto").Erros);
        Assert.Empty(Validar(TipoDeAviso.Simples, Emojis(200), "Texto").Erros); // 400 pedaços UTF-16, 200 caracteres: cabe no VARCHAR(200)
        Assert.Contains("titulo", Validar(TipoDeAviso.Simples, new string('t', 201), "Texto").Erros.Keys);
        Assert.Contains("titulo", Validar(TipoDeAviso.Simples, Emojis(201), "Texto").Erros.Keys);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData(" \n ")]
    public void Conteudo_vazio_e_erro_no_campo_conteudo(string? conteudo)
    {
        var (_, erros) = Validar(TipoDeAviso.Detalhado, "Título", conteudo);

        Assert.Equal("Informe o conteúdo do aviso.", Assert.Single(erros["conteudo"]));
    }

    [Fact]
    public void O_simples_vai_ate_280_e_o_detalhado_ate_10000()
    {
        Assert.Empty(Validar(TipoDeAviso.Simples, "T", new string('c', 280)).Erros);
        Assert.Equal("O aviso simples tem no máximo 280 caracteres.", Assert.Single(Validar(TipoDeAviso.Simples, "T", new string('c', 281)).Erros["conteudo"]));
        Assert.Empty(Validar(TipoDeAviso.Detalhado, "T", new string('c', 281)).Erros); // o detalhado não tem o limite do simples
        Assert.Empty(Validar(TipoDeAviso.Detalhado, "T", new string('c', 10000)).Erros);
        Assert.Equal("O conteúdo do aviso tem no máximo 10000 caracteres.", Assert.Single(Validar(TipoDeAviso.Detalhado, "T", new string('c', 10001)).Erros["conteudo"]));
    }

    [Fact]
    public void A_expiracao_nao_pode_ser_antes_do_dia_da_publicacao_mas_no_mesmo_dia_vale()
    {
        var publicacao = new DateOnly(2026, 10, 7);

        Assert.Empty(Validar(TipoDeAviso.Simples, "T", "C", null, publicacao).Erros); // sem expiração
        Assert.Empty(Validar(TipoDeAviso.Simples, "T", "C", publicacao, publicacao).Erros); // o último dia é o da publicação
        Assert.Equal("A data de expiração não pode ser anterior ao dia da publicação.", Assert.Single(Validar(TipoDeAviso.Simples, "T", "C", publicacao.AddDays(-1), publicacao).Erros["expiraEm"]));
    }

    [Fact]
    public void Todos_os_erros_vem_juntos_um_por_campo()
    {
        var (_, erros) = Validar(TipoDeAviso.Simples, " ", " ", new DateOnly(2026, 10, 1), new DateOnly(2026, 10, 7));

        Assert.Equal(["conteudo", "expiraEm", "titulo"], erros.Keys.Order());
    }
}
