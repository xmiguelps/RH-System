using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Tests.Treinamentos;

/// <summary>
/// As regras puras dos treinamentos, sem banco: a situação sai das datas de hoje (rascunho, agendado, em andamento, encerrado; sem agendador), os campos que travam depois de publicado, a validação do comando e do módulo (todos os erros juntos), o progresso (piso do percentual, módulo atual, duração total),
/// a visibilidade e as abas de "meus treinamentos", o número do certificado e os nomes dos arquivos.
/// </summary>
public class RegrasDosTreinamentosTests
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);
    private static readonly DateTime Encerrado = new(2026, 10, 6, 15, 0, 0, DateTimeKind.Utc);

    private static CamposDoTreinamento Campos(string titulo = "LGPD na prática", DateOnly? comecaEm = null, bool obrigatorio = true, bool onboarding = false, bool emite = true, PublicoDoTreinamento? publico = PublicoDoTreinamento.Setores, int[]? setores = null, int[]? pessoas = null, bool admitidos = true) =>
        new(titulo, null, comecaEm ?? Hoje, obrigatorio, onboarding, emite, publico, setores ?? [3, 4], pessoas ?? [], admitidos);

    private static Dictionary<string, string[]> ErrosDe(Action<ColetorDeErros> acao)
    {
        var erros = new ColetorDeErros();
        acao(erros);
        try
        {
            erros.LancarSeHouver();
            return [];
        }
        catch (ErroDeValidacaoException e)
        {
            return e.Erros.ToDictionary(x => x.Key, x => x.Value);
        }
    }

    // ---------------------------------------------------------------- situação

    [Fact]
    public void A_situacao_sai_das_datas_de_hoje_e_o_encerramento_vale_mais_que_a_data()
    {
        Assert.Equal(SituacaoDoTreinamento.Rascunho, RegraDaSituacaoDoTreinamento.De(false, Hoje, null, Hoje));
        Assert.Equal(SituacaoDoTreinamento.Rascunho, RegraDaSituacaoDoTreinamento.De(false, null, null, Hoje));
        Assert.Equal(SituacaoDoTreinamento.Agendado, RegraDaSituacaoDoTreinamento.De(true, Hoje.AddDays(1), null, Hoje));
        Assert.Equal(SituacaoDoTreinamento.EmAndamento, RegraDaSituacaoDoTreinamento.De(true, Hoje, null, Hoje)); // a data de hoje já libera
        Assert.Equal(SituacaoDoTreinamento.EmAndamento, RegraDaSituacaoDoTreinamento.De(true, null, null, Hoje)); // publicado sem data (dado antigo): já liberado
        Assert.Equal(SituacaoDoTreinamento.Encerrado, RegraDaSituacaoDoTreinamento.De(true, Hoje.AddDays(5), Encerrado, Hoje));
    }

    [Fact]
    public void Liberado_e_publicado_com_a_data_ja_chegada_ou_sem_data()
    {
        Assert.True(RegraDaSituacaoDoTreinamento.Liberado(true, Hoje, Hoje));
        Assert.True(RegraDaSituacaoDoTreinamento.Liberado(true, null, Hoje));
        Assert.False(RegraDaSituacaoDoTreinamento.Liberado(true, Hoje.AddDays(1), Hoje));
        Assert.False(RegraDaSituacaoDoTreinamento.Liberado(false, Hoje.AddDays(-9), Hoje));
    }

    // ---------------------------------------------------------------- validação do comando

    [Fact]
    public void O_comando_valido_vem_aparado_com_a_descricao_em_branco_como_nula_e_todos_os_erros_vem_juntos_no_invalido()
    {
        var erros = new ColetorDeErros();
        var valido = ValidacaoDoTreinamento.Validar(erros, "  LGPD na prática  ", "   ", Hoje, true, false, true, PublicoDoTreinamento.Setores, [3, 4], [], true);
        var invalido = ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "ab", new string('d', 2001), null, false, false, true, PublicoDoTreinamento.Todos, [0, 3, 3], [7, 7], true));

        Assert.False(erros.TemErros);
        Assert.Equal(("LGPD na prática", null), (valido!.Titulo, valido.Descricao));
        Assert.Equal(["descricao", "funcionarioIds", "setorIds", "titulo"], invalido.Keys.Order()); // com público TODOS qualquer lista é de outro público; o setor 0 e o repetido também
    }

    [Fact]
    public void O_titulo_vai_de_3_a_200_e_a_descricao_ate_2000_contados_por_caractere_visivel()
    {
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "abc", new string('d', 2000), null, false, false, true, null, [], [], true)));
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, new string('t', 200), null, null, false, false, true, null, [], [], true)));
        Assert.Contains("titulo", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, new string('t', 201), null, null, false, false, true, null, [], [], true)).Keys);
        Assert.Contains("titulo", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "  ab ", null, null, false, false, true, null, [], [], true)).Keys);
        Assert.Contains("titulo", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, null, null, null, false, false, true, null, [], [], true)).Keys);
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, string.Concat(Enumerable.Repeat("😀", 200)), null, null, false, false, true, null, [], [], true))); // 200 emojis = 200 caracteres
    }

    [Fact]
    public void As_listas_so_valem_com_o_publico_certo_sem_repeticao_e_dentro_do_limite()
    {
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Setores, [1, 2], [], true)));
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Pessoas, [], [1, 2], true)));
        Assert.Contains("funcionarioIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Setores, [1], [5], true)).Keys);
        Assert.Contains("setorIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Pessoas, [1], [5], true)).Keys);
        Assert.Contains("setorIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, null, [1], [], true)).Keys); // sem público escolhido não há lista
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Setores, [.. Enumerable.Range(1, 100)], [], true)));
        Assert.Contains("setorIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Setores, [.. Enumerable.Range(1, 101)], [], true)).Keys);
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Pessoas, [], [.. Enumerable.Range(1, 500)], true)));
        Assert.Contains("funcionarioIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Pessoas, [], [.. Enumerable.Range(1, 501)], true)).Keys);
        Assert.Contains("funcionarioIds", ErrosDe(e => ValidacaoDoTreinamento.Validar(e, "Treinamento", null, null, false, false, true, PublicoDoTreinamento.Pessoas, [], [1, 2, 1], true)).Keys);
    }

    [Fact]
    public void Para_publicar_exige_data_publico_com_gente_e_ao_menos_um_modulo_e_diz_tudo_junto()
    {
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.ConferirParaPublicar(e, Campos(), 1)));
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.ConferirParaPublicar(e, Campos(publico: PublicoDoTreinamento.Todos, setores: []), 2)));
        var tudo = ErrosDe(e => ValidacaoDoTreinamento.ConferirParaPublicar(e, new CamposDoTreinamento("Treinamento", null, null, false, false, true, null, [], [], true), 0));
        var setorVazio = ErrosDe(e => ValidacaoDoTreinamento.ConferirParaPublicar(e, Campos(publico: PublicoDoTreinamento.Setores, setores: []), 1));
        var pessoaVazia = ErrosDe(e => ValidacaoDoTreinamento.ConferirParaPublicar(e, Campos(publico: PublicoDoTreinamento.Pessoas, setores: [], pessoas: []), 1));

        Assert.Equal(["comecaEm", "modulos", "publico"], tudo.Keys.Order());
        Assert.Equal("Inclua pelo menos um módulo.", tudo["modulos"][0]);
        Assert.Equal("Informe a data em que o acesso é liberado.", tudo["comecaEm"][0]);
        Assert.Equal(["publico"], setorVazio.Keys.ToArray());
        Assert.Equal(["publico"], pessoaVazia.Keys.ToArray());
    }

    // ---------------------------------------------------------------- campos travados

    [Fact]
    public void O_rascunho_muda_tudo_e_o_encerrado_nada()
    {
        var atual = Campos();
        var outro = Campos("Outro título", comecaEm: Hoje.AddDays(9), obrigatorio: false, onboarding: true, emite: false, publico: PublicoDoTreinamento.Pessoas, setores: [], pessoas: [8], admitidos: false);

        Assert.Equal(ResultadoDaEdicao.Permitida, CamposTravados.Decidir(SituacaoDoTreinamento.Rascunho, atual, outro));
        Assert.Equal(ResultadoDaEdicao.Encerrado, CamposTravados.Decidir(SituacaoDoTreinamento.Encerrado, atual, atual));
    }

    [Fact]
    public void Depois_de_publicado_so_muda_titulo_descricao_a_caixa_de_admitidos_e_a_data_enquanto_agendado()
    {
        var atual = Campos(comecaEm: Hoje.AddDays(3));
        var livre = atual with { Titulo = "Novo título", Descricao = "Nova", IncluiAdmitidos = false, ComecaEm = Hoje.AddDays(1) };

        Assert.Equal(ResultadoDaEdicao.Permitida, CamposTravados.Decidir(SituacaoDoTreinamento.Agendado, atual, livre));
        Assert.Equal(ResultadoDaEdicao.CampoTravado, CamposTravados.Decidir(SituacaoDoTreinamento.EmAndamento, atual, livre)); // em andamento a data também trava
        Assert.Equal(ResultadoDaEdicao.Permitida, CamposTravados.Decidir(SituacaoDoTreinamento.EmAndamento, atual, atual with { Titulo = "Novo título", Descricao = "Nova", IncluiAdmitidos = false }));
        foreach (var travado in new[] { atual with { Obrigatorio = false }, atual with { Onboarding = true }, atual with { EmiteCertificado = false }, atual with { Publico = PublicoDoTreinamento.Todos, SetorIds = [] }, atual with { SetorIds = [3] }, atual with { SetorIds = [3, 4, 5] } })
        {
            Assert.Equal(ResultadoDaEdicao.CampoTravado, CamposTravados.Decidir(SituacaoDoTreinamento.Agendado, atual, travado));
        }
    }

    [Fact]
    public void As_listas_do_publico_se_comparam_como_conjuntos_e_o_agendado_publicado_nao_aceita_data_nula()
    {
        var atual = Campos(setores: [3, 4], comecaEm: Hoje.AddDays(3));

        Assert.Equal(ResultadoDaEdicao.Permitida, CamposTravados.Decidir(SituacaoDoTreinamento.Agendado, atual, atual with { SetorIds = [4, 3] }));
        Assert.Contains("comecaEm", ErrosDe(e => ValidacaoDoTreinamento.ConferirDataDoPublicado(e, null)).Keys);
        Assert.Empty(ErrosDe(e => ValidacaoDoTreinamento.ConferirDataDoPublicado(e, Hoje)));
    }

    // ---------------------------------------------------------------- módulo

    [Fact]
    public void Cada_tipo_de_modulo_exige_o_seu_campo_e_recusa_os_dos_outros()
    {
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Abertura", TipoDeModulo.Video, 12, "https://video.exemplo.com.br/a", null, false, false)));
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, "Texto.", false, false)));
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Artigo", TipoDeModulo.Link, null, "http://artigo.exemplo.com.br", null, false, false)));
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Apostila", TipoDeModulo.Pdf, 30, null, null, true, false)));
        Assert.Contains("urlExterna", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Abertura", TipoDeModulo.Video, null, null, null, false, false)).Keys);
        Assert.Contains("conteudoTexto", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Leitura", TipoDeModulo.Texto, null, null, "   ", false, false)).Keys);
        Assert.Contains("urlExterna", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Texto com link", TipoDeModulo.Texto, null, "https://a.com", "Texto.", false, false)).Keys);
        Assert.Contains("conteudoTexto", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Link com texto", TipoDeModulo.Link, null, "https://a.com", "Texto.", false, false)).Keys);
        Assert.Contains("arquivo", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Vídeo com arquivo", TipoDeModulo.Video, null, "https://a.com", null, true, false)).Keys); // o vídeo é só link externo
        Assert.Contains("arquivo", ErrosDe(e => ValidacaoDoModulo.Validar(e, "PDF sem arquivo", TipoDeModulo.Pdf, null, null, null, false, false)).Keys);
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "PDF que mantém o arquivo", TipoDeModulo.Pdf, null, null, null, false, true))); // na edição, sem arquivo novo o atual fica
        Assert.Contains("urlExterna", ErrosDe(e => ValidacaoDoModulo.Validar(e, "PDF com link", TipoDeModulo.Pdf, null, "https://a.com", null, true, false)).Keys);
    }

    [Fact]
    public void O_modulo_confere_titulo_duracao_e_endereco_e_trata_texto_vazio_de_outro_tipo_como_ausente()
    {
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, new string('t', 200), TipoDeModulo.Texto, 32767, "", "Texto.", false, false))); // "" no campo de outro tipo vale como ausente
        Assert.Contains("titulo", ErrosDe(e => ValidacaoDoModulo.Validar(e, "  ", TipoDeModulo.Texto, null, null, "Texto.", false, false)).Keys);
        Assert.Contains("titulo", ErrosDe(e => ValidacaoDoModulo.Validar(e, new string('t', 201), TipoDeModulo.Texto, null, null, "Texto.", false, false)).Keys);
        Assert.Contains("duracaoMinutos", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Texto, 0, null, "Texto.", false, false)).Keys);
        Assert.Contains("duracaoMinutos", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Texto, 32768, null, "Texto.", false, false)).Keys);
        Assert.Contains("urlExterna", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Link, null, "javascript:alert(1)", null, false, false)).Keys);
        Assert.Contains("urlExterna", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Link, null, "https://" + new string('a', 493), null, false, false)).Keys); // 501 caracteres
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Link, null, "https://" + new string('a', 492), null, false, false))); // 500
        Assert.Empty(ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Texto, null, null, new string('c', 50000), false, false)));
        Assert.Contains("conteudoTexto", ErrosDe(e => ValidacaoDoModulo.Validar(e, "Módulo", TipoDeModulo.Texto, null, null, new string('c', 50001), false, false)).Keys);
    }

    [Fact]
    public void O_modulo_valido_sai_com_so_o_campo_do_tipo_e_o_titulo_aparado()
    {
        var erros = new ColetorDeErros();

        var texto = ValidacaoDoModulo.Validar(erros, "  Leitura  ", TipoDeModulo.Texto, 5, "", "Texto.", false, false);
        var link = ValidacaoDoModulo.Validar(erros, "Artigo", TipoDeModulo.Link, null, " https://a.com ", null, false, false);

        Assert.False(erros.TemErros);
        Assert.Equal(("Leitura", (short)5, null, "Texto."), (texto!.Titulo, texto.DuracaoMinutos!.Value, texto.UrlExterna, texto.ConteudoTexto));
        Assert.Equal(("https://a.com", null), (link!.UrlExterna, link.ConteudoTexto));
    }

    // ---------------------------------------------------------------- progresso

    [Fact]
    public void O_progresso_e_o_piso_do_percentual_o_primeiro_modulo_que_falta_e_a_soma_das_duracoes_que_existem()
    {
        var modulos = new[] { new ModuloParaProgresso(1, 10, true), new ModuloParaProgresso(2, null, false), new ModuloParaProgresso(3, 5, true) };

        var p = ProgressoDoTreinamento.Calcular(modulos);

        Assert.Equal((3, 2, 66, 2, 15, false), (p.Total, p.Concluidos, p.Percentual, p.ModuloAtual, p.DuracaoTotalMinutos, p.TudoConcluido)); // 66,6 para baixo
    }

    [Fact]
    public void Tudo_concluido_nao_tem_modulo_atual_sem_duracao_a_soma_e_nula_e_sem_modulo_nada_esta_concluido()
    {
        var todos = ProgressoDoTreinamento.Calcular([new ModuloParaProgresso(1, null, true), new ModuloParaProgresso(2, null, true)]);
        var nenhum = ProgressoDoTreinamento.Calcular([]);
        var comBuraco = ProgressoDoTreinamento.Calcular([new ModuloParaProgresso(4, 1, false), new ModuloParaProgresso(2, 1, false)]);

        Assert.Equal((100, (int?)null, (int?)null, true), (todos.Percentual, todos.ModuloAtual, todos.DuracaoTotalMinutos, todos.TudoConcluido));
        Assert.Equal((0, 0, 0, false), (nenhum.Total, nenhum.Concluidos, nenhum.Percentual, nenhum.TudoConcluido));
        Assert.Equal(2, comBuraco.ModuloAtual); // o primeiro pela ORDEM, não pela posição na lista
    }

    // ---------------------------------------------------------------- visibilidade e abas

    [Fact]
    public void A_pessoa_so_ve_o_publicado_ja_liberado_que_e_dela_e_o_encerrado_so_para_quem_comecou()
    {
        bool V(bool publicado = true, DateOnly? data = null, DateTime? encerrado = null, SituacaoNoTreinamento? linha = null, bool opcional = false) =>
            VisibilidadeParaAPessoa.Visivel(publicado, data ?? Hoje, encerrado, Hoje, linha, opcional);

        Assert.True(V(linha: SituacaoNoTreinamento.Inscrito));
        Assert.True(V(opcional: true)); // opcional do público dela, sem linha
        Assert.False(V()); // fora do público e sem linha
        Assert.False(V(publicado: false, linha: SituacaoNoTreinamento.Inscrito));
        Assert.False(V(data: Hoje.AddDays(1), linha: SituacaoNoTreinamento.Inscrito)); // ainda não liberado
        Assert.False(V(encerrado: Encerrado, linha: SituacaoNoTreinamento.Inscrito)); // encerrado e nunca começou
        Assert.False(V(encerrado: Encerrado, opcional: true));
        Assert.True(V(encerrado: Encerrado, linha: SituacaoNoTreinamento.EmAndamento));
        Assert.True(V(encerrado: Encerrado, linha: SituacaoNoTreinamento.Concluido));
    }

    [Fact]
    public void A_situacao_minha_e_as_abas_e_as_contagens_seguem_o_contrato()
    {
        var situacoes = new[] { MinhaSituacaoNoTreinamento.Disponivel, MinhaSituacaoNoTreinamento.NaoIniciado, MinhaSituacaoNoTreinamento.EmAndamento, MinhaSituacaoNoTreinamento.EmAndamento, MinhaSituacaoNoTreinamento.Concluido };

        Assert.Equal(MinhaSituacaoNoTreinamento.Disponivel, MinhaSituacao.De(null));
        Assert.Equal(MinhaSituacaoNoTreinamento.NaoIniciado, MinhaSituacao.De(SituacaoNoTreinamento.Inscrito));
        Assert.Equal(MinhaSituacaoNoTreinamento.EmAndamento, MinhaSituacao.De(SituacaoNoTreinamento.EmAndamento));
        Assert.Equal(MinhaSituacaoNoTreinamento.Concluido, MinhaSituacao.De(SituacaoNoTreinamento.Concluido));
        Assert.Equal([4, 2, 2, 1], new[] { AbaDosMeusTreinamentos.Todos, AbaDosMeusTreinamentos.Disponiveis, AbaDosMeusTreinamentos.EmAndamento, AbaDosMeusTreinamentos.Concluidos }.Select(a => situacoes.Count(s => AbasDosMeusTreinamentos.Pertence(a, s))));
        var contagens = AbasDosMeusTreinamentos.Contar(situacoes);
        Assert.Equal((4, 2, 2, 1), (contagens.Todos, contagens.Disponiveis, contagens.EmAndamento, contagens.Concluidos)); // o "todos" do contrato não inclui o concluído
    }

    // ---------------------------------------------------------------- certificado e nomes

    [Fact]
    public void O_numero_do_certificado_tem_o_ano_e_o_id_com_cinco_digitos_ou_mais()
    {
        Assert.Equal("CERT-2026-00012", NumeroDoCertificado.De(2026, 12));
        Assert.Equal("CERT-2027-00001", NumeroDoCertificado.De(2027, 1));
        Assert.Equal("CERT-2026-123456", NumeroDoCertificado.De(2026, 123456));
    }

    [Fact]
    public void O_nome_do_CSV_e_do_certificado_saem_limpos_e_o_certificado_nunca_perde_a_extensao()
    {
        Assert.Equal("treinamento-lgpd-na-pratica.csv", NomesDeArquivoDoTreinamento.CsvDosParticipantes("LGPD na Prática"));
        Assert.Equal("treinamento-seguranca-da-informacao-2026.csv", NomesDeArquivoDoTreinamento.CsvDosParticipantes("  Segurança da Informação — 2026!  "));
        Assert.Equal("treinamento.csv", NomesDeArquivoDoTreinamento.CsvDosParticipantes("???"));
        Assert.Equal("certificado-LGPD na prática.pdf", NomesDeArquivoDoTreinamento.PdfDoCertificado("LGPD na prática"));
        Assert.Equal("certificado-a-b.pdf", NomesDeArquivoDoTreinamento.PdfDoCertificado("a/b"));
        var longo = NomesDeArquivoDoTreinamento.PdfDoCertificado(new string('t', 300));
        Assert.Equal(100, longo.Length);
        Assert.EndsWith(".pdf", longo, StringComparison.Ordinal);
    }
}
