using System.Text;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Emails;
using RHSystem.Infrastructure.Treinamentos;
using UglyToad.PdfPig;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// Um teste por mutante real que sobreviveu à mutação dos incrementos 6b e 6c (T9 a T18): a corrida de iniciar com o RH encerrando no meio (decoradores), o UPDATE de iniciar que não pode tocar a linha dos outros, o progresso e o certificado dos OUTROS que a conclusão não pode contar nem trocar, a carga horária e o negrito do PDF do certificado,
/// o endereço de conferência do front e as contas de "Quem fez" (um módulo só, progresso de outro treinamento, mais de 100 linhas no CSV), mais os que ficaram nos testes de equipe, onboarding e lista da pessoa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MutantesDosIncrementos6b6cTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MutantesDosIncrementos6b6cTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, int Usuario, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true, int? setor = null)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, setor, ativa, new DateOnly(2026, 1, 2));
        var usuario = await AdvertenciasDeTeste.ComLoginAsync(api, id);
        return (id, usuario, await api.ClienteAgoraAsync(usuario));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null, string? conclusao = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO, DATA_CONCLUSAO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")}, {(conclusao is null ? "NULL" : $"'{conclusao}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    /// <summary>Um certificado emitido com o código dado (o trigger cria a linha na conclusão; aqui o código e a data são trocados como a API faz).</summary>
    private static async Task<string> CertificadoAsync(ApiDeAcesso api, int treinamento, int pessoa)
    {
        await LinhaAsync(api, treinamento, pessoa, "CONCLUIDO");
        var codigo = CodigoDoCertificado.Novo();
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET CODIGO_VALIDACAO = '{codigo}', DATA_EMISSAO = '2026-09-15' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");
        return codigo;
    }

    private static Task<string?> CodigoAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CONCAT(CODIGO_VALIDACAO, '|', DATE_FORMAT(DATA_EMISSAO, '%Y-%m-%d')) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    // ---------------------------------------------------------------- iniciar: a corrida com o RH encerrando e a linha dos outros

    /// <summary>A participação com a corrida simulada: as primeiras N leituras mostram o treinamento SEM o encerramento (a leitura velha) e as primeiras M chamadas de iniciar fingem que outra tela ganhou (devolvem "false" sem mudar nada).</summary>
    private sealed class ParticipacaoComCorrida(IMinhaParticipacaoRepositorio real, int[] estado) : IMinhaParticipacaoRepositorio
    {
        public async Task<TreinamentoParaParticipar?> LerAsync(int funcionarioId, int treinamentoId, CancellationToken ct)
        {
            var lido = await real.LerAsync(funcionarioId, treinamentoId, ct);
            return lido is not null && Interlocked.Decrement(ref estado[0]) >= 0 ? lido with { EncerradoEmUtc = null } : lido;
        }

        public Task<bool> InscreverAsync(int funcionarioId, int treinamentoId, CancellationToken ct) => real.InscreverAsync(funcionarioId, treinamentoId, ct);

        public async Task<bool> IniciarAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct) => Interlocked.Decrement(ref estado[1]) >= 0 ? false : await real.IniciarAsync(funcionarioId, treinamentoId, hoje, ct);
    }

    private static async Task<HttpClient> ComCorridaAsync(ApiDeAcesso api, int usuario, int[] estado, List<IDisposable> descartar)
    {
        var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IMinhaParticipacaoRepositorio>();
            s.AddScoped<MinhaParticipacaoRepositorio>();
            s.AddScoped<IMinhaParticipacaoRepositorio>(sp => new ParticipacaoComCorrida(sp.GetRequiredService<MinhaParticipacaoRepositorio>(), estado));
        }));
        descartar.Add(fabrica);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(usuario)).DefaultRequestHeaders.Authorization;
        return cliente;
    }

    [Fact]
    public async Task Iniciar_quando_o_RH_encerra_entre_a_leitura_e_o_UPDATE_relê_e_responde_422_e_nao_gera_404()
    {
        var api = await ApiAsync();
        var (eu, usuario, _) = await PessoaAsync(api, "ZMut6b Ana");
        var t = await SemearAsync(api, Titulo("ZMut6b encerrado"), "ENCERRADO", obrigatorio: true);
        await LinhaAsync(api, t, eu, "INSCRITO");
        var descartar = new List<IDisposable>();
        try
        {
            var cliente = await ComCorridaAsync(api, usuario, [1, 0], descartar); // a primeira leitura ainda não vê o encerramento

            var resposta = await cliente.PostAsync($"/api/eu/treinamentos/{t}/iniciar", null);

            Assert.Equal(422, (int)resposta.StatusCode);
            Assert.Equal("TREINAMENTO_ENCERRADO", Codigo(await CorpoAsync(resposta)));
            Assert.Equal("INSCRITO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}"));
        }
        finally
        {
            descartar.ForEach(d => d.Dispose());
        }
    }

    [Fact]
    public async Task Iniciar_quando_outra_tela_ganhou_mas_o_treinamento_segue_aberto_relê_e_devolve_o_estado_sem_recusar()
    {
        var api = await ApiAsync();
        var (eu, usuario, _) = await PessoaAsync(api, "ZMut6b Bia");
        var t = await SemearAsync(api, Titulo("ZMut6b aberto"), "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, t, eu, "INSCRITO");
        var descartar = new List<IDisposable>();
        try
        {
            var cliente = await ComCorridaAsync(api, usuario, [0, 1], descartar); // o UPDATE "perde" uma vez, sem mudar nada

            var resposta = await cliente.PostAsync($"/api/eu/treinamentos/{t}/iniciar", null);

            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.Equal("NAO_INICIADO", (await CorpoAsync(resposta)).GetProperty("situacao").GetString());
        }
        finally
        {
            descartar.ForEach(d => d.Dispose());
        }
    }

    [Fact]
    public async Task Iniciar_so_muda_a_linha_da_propria_pessoa_e_nunca_a_dos_outros_do_mesmo_treinamento()
    {
        var api = await ApiAsync();
        var (a, _, clienteA) = await PessoaAsync(api, "ZMut6b Ana");
        var (b, _, _) = await PessoaAsync(api, "ZMut6b Bia");
        var t = await SemearAsync(api, Titulo("ZMut6b duas pessoas"), "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, t, a, "INSCRITO");
        await LinhaAsync(api, t, b, "INSCRITO");

        var resposta = await clienteA.PostAsync($"/api/eu/treinamentos/{t}/iniciar", null);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("EM_ANDAMENTO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {a}"));
        Assert.Equal("INSCRITO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {b}"));
    }

    // ---------------------------------------------------------------- concluir: o progresso e o certificado dos outros

    [Fact]
    public async Task O_progresso_de_outra_pessoa_nao_conta_para_concluir_e_o_certificado_dos_outros_nao_e_trocado()
    {
        var api = await ApiAsync();
        var (a, _, clienteA) = await PessoaAsync(api, "ZMut6b Ana");
        var (b, _, _) = await PessoaAsync(api, "ZMut6b Bia");
        var x = await SemearAsync(api, Titulo("ZMut6b X"), "EM_ANDAMENTO", obrigatorio: true, emite: true);
        _ = await ModuloAsync(api, x, 1);
        var codigoDeAEmX = await CertificadoAsync(api, x, a); // a Ana já concluiu X
        var y = await SemearAsync(api, Titulo("ZMut6b Y"), "EM_ANDAMENTO", obrigatorio: true, emite: true);
        var y1 = await ModuloAsync(api, y, 1);
        var y2 = await ModuloAsync(api, y, 2);
        var codigoDeBEmY = await CertificadoAsync(api, y, b); // a Bia concluiu Y (e tem o progresso dos dois módulos)
        await ProgressoAsync(api, y1, b, "2026-09-10 10:00:00");
        await ProgressoAsync(api, y2, b, "2026-09-10 11:00:00");
        await LinhaAsync(api, y, a, "EM_ANDAMENTO", "2026-10-05"); // a Ana, em Y, só terá o módulo 2

        var primeira = await clienteA.PostAsync($"/api/eu/treinamentos/{y}/modulos/{y2}/concluir", null);

        Assert.Equal(200, (int)primeira.StatusCode);
        Assert.False((await CorpoAsync(primeira)).GetProperty("treinamentoConcluido").GetBoolean()); // faltava o módulo 1: o progresso da Bia não conta
        Assert.Equal("EM_ANDAMENTO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {y} AND ID_FUNCIONARIO = {a}"));

        var segunda = await clienteA.PostAsync($"/api/eu/treinamentos/{y}/modulos/{y1}/concluir", null);

        Assert.True((await CorpoAsync(segunda)).GetProperty("treinamentoConcluido").GetBoolean());
        Assert.Equal($"{codigoDeAEmX}|2026-09-15", await CodigoAsync(api, x, a)); // o certificado da Ana em X não mudou
        Assert.Equal($"{codigoDeBEmY}|2026-09-15", await CodigoAsync(api, y, b)); // nem o da Bia em Y
        var novo = (await CodigoAsync(api, y, a))!;
        Assert.True(CodigoDoCertificado.EhVersao4(novo[..36]));
        Assert.EndsWith("|2026-10-07", novo, StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- "Quem fez": um módulo só, progresso de outro treinamento, mais de 100 linhas

    [Fact]
    public async Task Quem_fez_conta_os_modulos_concluidos_de_um_treinamento_de_um_modulo_so_e_ignora_o_progresso_em_outro_treinamento()
    {
        var api = await ApiAsync();
        var (a, _, _) = await PessoaAsync(api, "ZMut6c Ana");
        var umModulo = await SemearAsync(api, Titulo("ZMut6c um modulo"), "EM_ANDAMENTO", obrigatorio: true);
        var modulo = await ModuloAsync(api, umModulo, 1);
        var outro = await SemearAsync(api, Titulo("ZMut6c outro"), "EM_ANDAMENTO", obrigatorio: true);
        var moduloDoOutro = await ModuloAsync(api, outro, 1);
        await LinhaAsync(api, umModulo, a, "EM_ANDAMENTO", "2026-10-02");
        await LinhaAsync(api, outro, a, "EM_ANDAMENTO", "2026-10-02");
        await ProgressoAsync(api, moduloDoOutro, a, "2026-10-03 09:00:00");
        var rh = await api.ClienteDoRhAgoraAsync();

        var antes = (await CorpoAsync(await rh.GetAsync($"/api/rh/treinamentos/{umModulo}/participantes"))).GetProperty("itens")[0].GetProperty("modulosConcluidos").GetInt32();
        await ProgressoAsync(api, modulo, a, "2026-10-04 09:00:00");
        var depois = (await CorpoAsync(await rh.GetAsync($"/api/rh/treinamentos/{umModulo}/participantes"))).GetProperty("itens")[0].GetProperty("modulosConcluidos").GetInt32();

        Assert.Equal((0, 1), (antes, depois)); // o progresso do outro treinamento não conta; o do único módulo, sim
    }

    [Fact]
    public async Task O_CSV_traz_todas_as_linhas_mesmo_com_mais_de_100_participantes()
    {
        var api = await ApiAsync();
        var t = await SemearAsync(api, Titulo("ZMut6c cento e cinco"), "EM_ANDAMENTO", obrigatorio: true);
        for (var i = 1; i <= 105; i++)
        {
            var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, $"ZMut6c pessoa {i:000} {Titulo("x")}", Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
            await LinhaAsync(api, t, pessoa, "INSCRITO");
        }

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/treinamentos/{t}/participantes/exportacao");
        var texto = new UTF8Encoding(false).GetString((await resposta.Content.ReadAsByteArrayAsync())[3..]);

        Assert.Equal(106, texto.Split("\r\n", StringSplitOptions.RemoveEmptyEntries).Length); // cabeçalho e 105 linhas: a página de 100 não vale na exportação
    }

    // ---------------------------------------------------------------- pendência: pessoa inativa

    [Fact]
    public async Task A_pessoa_inativa_nao_tem_pendencia_e_o_obrigatorio_que_comeca_hoje_ja_conta_para_a_ativa()
    {
        var api = await ApiAsync();
        var (ativa, _, _) = await PessoaAsync(api, "ZMut6c Ativa");
        var (saiu, _, _) = await PessoaAsync(api, "ZMut6c Saiu");
        var t = await SemearAsync(api, Titulo("ZMut6c hoje"), "EM_ANDAMENTO", obrigatorio: true);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '2026-10-07' WHERE ID_TREINAMENTO = {t}");
        await LinhaAsync(api, t, ativa, "INSCRITO");
        await LinhaAsync(api, t, saiu, "INSCRITO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var antes = (await CorpoAsync(await rh.GetAsync($"/api/rh/funcionarios/{saiu}/treinamentos"))).GetProperty("resumo").GetProperty("pendentes").GetInt32();
        await AdvertenciasDeTeste.InativarAsync(api, saiu);
        var depois = (await CorpoAsync(await rh.GetAsync($"/api/rh/funcionarios/{saiu}/treinamentos"))).GetProperty("resumo").GetProperty("pendentes").GetInt32();
        var daAtiva = (await CorpoAsync(await rh.GetAsync($"/api/rh/funcionarios/{ativa}/treinamentos"))).GetProperty("resumo").GetProperty("pendentes").GetInt32();

        Assert.Equal((1, 0, 1), (antes, depois, daAtiva)); // inativa sem pendência; a que ativa tem 1 (o treinamento de hoje já vale)
    }

    // ---------------------------------------------------------------- o PDF do certificado: carga horária, negrito e endereço de conferência

    [Theory]
    [InlineData(0, "0 min")]
    [InlineData(1, "1 min")]
    [InlineData(59, "59 min")]
    [InlineData(60, "1h")]
    [InlineData(61, "1h01")]
    [InlineData(100, "1h40")]
    [InlineData(120, "2h")]
    public void A_carga_horaria_se_le_como_num_certificado(int minutos, string esperado) => Assert.Equal(esperado, GeradorDePdfDoCertificado.CargaHoraria(minutos));

    private static CertificadoParaPdf Entrada(int? carga, string? empresa = "Acme Treinos", string? endereco = "https://app.exemplo.com.br") =>
        new("Zenaida Souza", "Segurança digital", carga, "CERT-2026-00012", new DateOnly(2026, 10, 7), "a7f3c1d2-5e9b-4c68-b2d1-8f04e6a93b57", empresa, endereco);

    private static Dictionary<string, string> FontePorPalavra(byte[] pdf)
    {
        using var documento = PdfDocument.Open(pdf);
        var porPalavra = new Dictionary<string, string>();
        foreach (var palavra in documento.GetPage(1).GetWords())
        {
            porPalavra.TryAdd(palavra.Text, palavra.Letters[0].FontName ?? string.Empty);
        }

        return porPalavra;
    }

    [Fact]
    public void O_pdf_poe_em_negrito_so_a_empresa_o_titulo_o_nome_e_o_treinamento_e_o_resto_em_texto_normal()
    {
        var fontes = FontePorPalavra(new GeradorDePdfDoCertificado().Gerar(Entrada(100)));

        string[] negrito = ["Acme", "Certificado", "Zenaida", "Segurança"];
        string[] normal = ["Certificamos", "concluiu", "carga", "Emitido", "Código", "Confira"];
        Assert.All(negrito, palavra => Assert.Contains("Bold", fontes[palavra], StringComparison.OrdinalIgnoreCase));
        Assert.All(normal, palavra => Assert.DoesNotContain("Bold", fontes[palavra], StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void O_pdf_so_fala_de_carga_horaria_com_duracao_positiva_e_so_confere_com_o_endereco_do_front()
    {
        string Texto(byte[] bytes)
        {
            using var documento = PdfDocument.Open(bytes);
            return string.Join(' ', documento.GetPage(1).GetWords().Select(w => w.Text));
        }

        var comCarga = Texto(new GeradorDePdfDoCertificado().Gerar(Entrada(60)));
        var zero = Texto(new GeradorDePdfDoCertificado().Gerar(Entrada(0)));
        var semDuracao = Texto(new GeradorDePdfDoCertificado().Gerar(Entrada(null, empresa: null, endereco: null)));

        Assert.Contains("carga horária de 1h", comCarga, StringComparison.Ordinal);
        Assert.Contains("Confira a autenticidade em https://app.exemplo.com.br", comCarga, StringComparison.Ordinal);
        Assert.DoesNotContain("carga horária", zero, StringComparison.Ordinal);
        Assert.DoesNotContain("carga horária", semDuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("Confira", semDuracao, StringComparison.Ordinal);
        Assert.DoesNotContain("Acme", semDuracao, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("https://app.exemplo.com.br", "https://app.exemplo.com.br")]
    [InlineData("https://app.exemplo.com.br/", "https://app.exemplo.com.br")]
    [InlineData("http://localhost:5173", "http://localhost:5173")]
    [InlineData("ftp://arquivos.exemplo.com.br", null)]
    [InlineData("app.exemplo.com.br", null)]
    [InlineData("", null)]
    public void O_endereco_da_conferencia_so_existe_com_a_url_do_front_http_ou_https_absoluta(string configurada, string? esperado) =>
        Assert.Equal(esperado, new EnderecoDaConferencia(new OpcoesDoApp { UrlDoFront = configurada }).Endereco);
}
