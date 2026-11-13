using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>obterConteudoDeModulo</c> e <c>baixarArquivoDeModulo</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. O conteúdo abre em QUALQUER ordem, sem "Começar" para quem tem linha <c>INSCRITO</c>, e continua aberto depois de concluído e com o treinamento encerrado para quem começou ou concluiu. Sem linha: 404 (obrigatório, fora do público, rascunho, agendado, linha <c>REPROVADO</c>, encerrado em que ela nunca
/// começou) ou 422 <c>INSCRICAO_PENDENTE</c> (opcional do público dela sem inscrição). O download só existe para módulo PDF, devolve um link temporário sem o caminho do armazenamento e segue as mesmas regras de acesso.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConteudoDoModuloTests(MySqlFixture mysql)
{
    private static string Rota(int treinamento, int modulo, string sufixo = "") => $"/api/eu/treinamentos/{treinamento}/modulos/{modulo}{sufixo}";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(ConteudoDoModuloTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, HttpClient Cliente)> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, Perfil perfil = Perfil.Funcionario)
    {
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), perfil, setor, true, new DateOnly(2026, 1, 2));
        return (id, await api.ClienteAgoraAsync(await AdvertenciasDeTeste.ComLoginAsync(api, id)));
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")})");

    private static async Task ProgressoAsync(ApiDeAcesso api, int modulo, int pessoa, string instanteLocal) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({modulo}, {pessoa}, '{instanteLocal}')");

    private static async Task PublicoDoSetorAsync(ApiDeAcesso api, int treinamento, int setor) => await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({treinamento}, {setor})");

    private static Task<int> EmAndamentoAsync(ApiDeAcesso api, string titulo, string situacao = "EM_ANDAMENTO", bool obrigatorio = true, string publico = "TODOS") => SemearAsync(api, titulo, situacao, obrigatorio, publico);

    [Fact]
    public async Task Cada_tipo_de_modulo_abre_com_o_que_e_dele_o_texto_como_veio_e_o_progresso_so_da_pessoa()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCon1 Ana");
        var (outra, _) = await PessoaAsync(api, "ZCon1 Bia");
        var t = await EmAndamentoAsync(api, Titulo("ZCon1"));
        var texto = await ModuloAsync(api, t, 1, "TEXTO", duracao: 20);
        var video = await ModuloAsync(api, t, 2, "VIDEO", duracao: 15);
        var link = await ModuloAsync(api, t, 3, "LINK", duracao: null);
        var pdf = await ModuloAsync(api, t, 4, "PDF", duracao: 5, caminho: "treinamentos/segredo-do-modulo.pdf", nomeArquivo: "apostila.pdf");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_MODULOS SET CONTEUDO_TEXTO = '<b>Nome</b>, CPF & e-mail são dados pessoais.' WHERE ID_MODULO = {texto}");
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        await LinhaAsync(api, t, outra, "EM_ANDAMENTO", "2026-10-05");
        await ProgressoAsync(api, texto, eu, "2026-08-02 10:35:00");
        await ProgressoAsync(api, video, outra, "2026-08-02 10:00:00"); // da outra pessoa: nunca aparece para mim

        var rTexto = await cliente.GetAsync(Rota(t, texto));
        var rVideo = await cliente.GetAsync(Rota(t, video));
        var rLink = await cliente.GetAsync(Rota(t, link));
        var rPdf = await cliente.GetAsync(Rota(t, pdf));

        Assert.Equal([200, 200, 200, 200], new[] { rTexto, rVideo, rLink, rPdf }.Select(r => (int)r.StatusCode));
        await rTexto.AfirmarConformeAoContratoAsync("obterConteudoDeModulo");
        var c = await CorpoAsync(rTexto);
        Assert.Equal((texto, 1, "TEXTO", 20, "<b>Nome</b>, CPF & e-mail são dados pessoais.", JsonValueKind.Null, false, true, "2026-08-02T13:35:00Z"), (c.GetProperty("id").GetInt32(), c.GetProperty("ordem").GetInt32(), c.GetProperty("tipo").GetString(), c.GetProperty("duracaoMinutos").GetInt32(), c.GetProperty("conteudoTexto").GetString(), c.GetProperty("urlExterna").ValueKind, c.GetProperty("temArquivo").GetBoolean(), c.GetProperty("concluido").GetBoolean(), c.GetProperty("concluidoEmUtc").GetString()));
        var v = await CorpoAsync(rVideo);
        Assert.Equal(("VIDEO", "https://videos.exemplo.com.br/aula", JsonValueKind.Null, false, false, JsonValueKind.Null), (v.GetProperty("tipo").GetString(), v.GetProperty("urlExterna").GetString(), v.GetProperty("conteudoTexto").ValueKind, v.GetProperty("temArquivo").GetBoolean(), v.GetProperty("concluido").GetBoolean(), v.GetProperty("concluidoEmUtc").ValueKind)); // o progresso da outra pessoa não conta
        var l = await CorpoAsync(rLink);
        Assert.Equal(("LINK", "https://artigo.exemplo.com.br", JsonValueKind.Null), (l.GetProperty("tipo").GetString(), l.GetProperty("urlExterna").GetString(), l.GetProperty("duracaoMinutos").ValueKind));
        var p = await CorpoAsync(rPdf);
        Assert.Equal(("PDF", true), (p.GetProperty("tipo").GetString(), p.GetProperty("temArquivo").GetBoolean()));
        Assert.DoesNotContain("segredo-do-modulo", await rPdf.Content.ReadAsStringAsync(), StringComparison.Ordinal); // o caminho do armazenamento nunca sai
        Assert.Equal(["concluido", "concluidoEmUtc", "conteudoTexto", "duracaoMinutos", "id", "ordem", "temArquivo", "tipo", "titulo", "urlExterna"], c.EnumerateObject().Select(x => x.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Os_modulos_abrem_em_qualquer_ordem_sem_comecar_e_sem_mudar_a_linha_e_o_concluido_e_o_encerrado_de_quem_comecou_continuam_abertos()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCon2 Ana");
        var inscrito = await EmAndamentoAsync(api, Titulo("ZCon2 inscrito"));
        var ultimoModulo = 0;
        for (var i = 1; i <= 3; i++)
        {
            ultimoModulo = await ModuloAsync(api, inscrito, i);
        }

        await LinhaAsync(api, inscrito, eu, "INSCRITO");
        var concluido = await EmAndamentoAsync(api, Titulo("ZCon2 concluido"));
        var moduloConcluido = await ModuloAsync(api, concluido, 1);
        await LinhaAsync(api, concluido, eu, "CONCLUIDO");
        await ProgressoAsync(api, moduloConcluido, eu, "2026-09-15 11:05:00");
        var encerradoAndamento = await EmAndamentoAsync(api, Titulo("ZCon2 encerrado andamento"), "ENCERRADO");
        var moduloEncerrado = await ModuloAsync(api, encerradoAndamento, 1);
        await LinhaAsync(api, encerradoAndamento, eu, "EM_ANDAMENTO", "2026-09-10");

        var terceiroPrimeiro = await cliente.GetAsync(Rota(inscrito, ultimoModulo));
        var rever = await cliente.GetAsync(Rota(concluido, moduloConcluido));
        var encerrado = await cliente.GetAsync(Rota(encerradoAndamento, moduloEncerrado));

        Assert.Equal([200, 200, 200], new[] { terceiroPrimeiro, rever, encerrado }.Select(r => (int)r.StatusCode));
        Assert.Equal(("2026-09-15T14:05:00Z", true), ((await CorpoAsync(rever)).GetProperty("concluidoEmUtc").GetString(), (await CorpoAsync(rever)).GetProperty("concluido").GetBoolean()));
        Assert.Equal("INSCRITO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {inscrito} AND ID_FUNCIONARIO = {eu}")); // ler não começa nada
    }

    [Fact]
    public async Task Sem_acesso_e_404_ou_422_conforme_o_caso_e_modulo_de_outro_treinamento_e_404()
    {
        var api = await ApiAsync();
        var meuSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZCon3 meu"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZCon3 outro"));
        var (eu, cliente) = await PessoaAsync(api, "ZCon3 Ana", meuSetor);
        var casos = new Dictionary<string, (int Treinamento, int Modulo)>();

        async Task<(int, int)> CasoAsync(string nome, string situacao = "EM_ANDAMENTO", bool obrigatorio = true, string publico = "TODOS", string? linha = null, int? setorDoPublico = null)
        {
            var t = await EmAndamentoAsync(api, Titulo($"ZCon3 {nome}"), situacao, obrigatorio, publico);
            var m = await ModuloAsync(api, t, 1);
            if (setorDoPublico is { } setor)
            {
                await PublicoDoSetorAsync(api, t, setor);
            }

            if (linha is not null)
            {
                await LinhaAsync(api, t, eu, linha);
            }

            casos[nome] = (t, m);
            return (t, m);
        }

        _ = await CasoAsync("opcional sem inscricao", obrigatorio: false, setorDoPublico: null);
        _ = await CasoAsync("obrigatorio sem linha");
        _ = await CasoAsync("opcional de outro setor", obrigatorio: false, publico: "SETORES", setorDoPublico: outroSetor);
        _ = await CasoAsync("rascunho", situacao: "RASCUNHO", linha: "INSCRITO");
        _ = await CasoAsync("agendado", situacao: "AGENDADO", linha: "INSCRITO");
        _ = await CasoAsync("reprovado", linha: "REPROVADO");
        _ = await CasoAsync("encerrado inscrito", situacao: "ENCERRADO", linha: "INSCRITO");
        _ = await CasoAsync("encerrado opcional sem linha", situacao: "ENCERRADO", obrigatorio: false);
        var (umTreinamento, umModulo) = await CasoAsync("tem linha", linha: "INSCRITO");
        var (_, moduloDeOutro) = await CasoAsync("modulo de outro", linha: "INSCRITO");

        async Task<int> StatusAsync(string nome, string sufixo = "") => (int)(await cliente.GetAsync(Rota(casos[nome].Treinamento, casos[nome].Modulo, sufixo))).StatusCode;

        Assert.Equal(422, await StatusAsync("opcional sem inscricao"));
        Assert.Equal(404, await StatusAsync("obrigatorio sem linha"));
        Assert.Equal(404, await StatusAsync("opcional de outro setor"));
        Assert.Equal(404, await StatusAsync("rascunho"));
        Assert.Equal(404, await StatusAsync("agendado"));
        Assert.Equal(404, await StatusAsync("reprovado"));
        Assert.Equal(404, await StatusAsync("encerrado inscrito"));
        Assert.Equal(404, await StatusAsync("encerrado opcional sem linha"));
        Assert.Equal(404, (int)(await cliente.GetAsync(Rota(umTreinamento, moduloDeOutro))).StatusCode); // módulo de outro treinamento
        Assert.Equal(404, (int)(await cliente.GetAsync(Rota(umTreinamento, 987654))).StatusCode);
        Assert.Equal(404, (int)(await cliente.GetAsync(Rota(987654, umModulo))).StatusCode);

        var pendente = await cliente.GetAsync(Rota(casos["opcional sem inscricao"].Treinamento, casos["opcional sem inscricao"].Modulo));
        await pendente.AfirmarConformeAoContratoAsync("obterConteudoDeModulo");
        var corpo = await CorpoAsync(pendente);
        Assert.Equal(("INSCRICAO_PENDENTE", "Inscreva-se no treinamento antes de começar."), (Codigo(corpo), corpo.GetProperty("detail").GetString()));
    }

    [Fact]
    public async Task O_download_do_pdf_devolve_o_link_temporario_sem_o_caminho_e_as_mesmas_regras_de_acesso_valem()
    {
        var api = await ApiAsync();
        var (eu, cliente) = await PessoaAsync(api, "ZCon4 Ana");
        var t = await EmAndamentoAsync(api, Titulo("ZCon4"));
        var caminho = $"treinamentos/{Guid.NewGuid():N}.pdf";
        api.Api.Armazenamento.Semear(caminho, [37, 80, 68, 70], ElencoDeTeste.Hoje);
        var pdf = await ModuloAsync(api, t, 1, "PDF", caminho: caminho, nomeArquivo: "apostila-de-seguranca.pdf");
        var texto = await ModuloAsync(api, t, 2, "TEXTO");
        var video = await ModuloAsync(api, t, 3, "VIDEO");
        var link = await ModuloAsync(api, t, 4, "LINK");
        await LinhaAsync(api, t, eu, "INSCRITO");
        var opcional = await EmAndamentoAsync(api, Titulo("ZCon4 opcional"), obrigatorio: false);
        var pdfDoOpcional = await ModuloAsync(api, opcional, 1, "PDF", caminho: caminho, nomeArquivo: "outra.pdf");
        var encerrado = await EmAndamentoAsync(api, Titulo("ZCon4 encerrado"), "ENCERRADO");
        var pdfDoEncerrado = await ModuloAsync(api, encerrado, 1, "PDF", caminho: caminho, nomeArquivo: "velha.pdf");
        await LinhaAsync(api, encerrado, eu, "INSCRITO");

        var resposta = await cliente.GetAsync(Rota(t, pdf, "/download"));
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("baixarArquivoDeModulo");
        Assert.StartsWith("https://", corpo.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal(["expiraEmUtc", "url"], corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // nenhum campo com o caminho do armazenamento
        Assert.True(corpo.GetProperty("expiraEmUtc").GetDateTime() > ElencoDeTeste.Hoje);
        var semArquivo = new List<int>();
        foreach (var modulo in new[] { texto, video, link })
        {
            semArquivo.Add((int)(await cliente.GetAsync(Rota(t, modulo, "/download"))).StatusCode);
        }

        var semAcesso = new[] { (int)(await cliente.GetAsync(Rota(opcional, pdfDoOpcional, "/download"))).StatusCode, (int)(await cliente.GetAsync(Rota(encerrado, pdfDoEncerrado, "/download"))).StatusCode, (int)(await cliente.GetAsync(Rota(t, 987654, "/download"))).StatusCode };
        Assert.Equal([404, 404, 404], semArquivo);
        Assert.Equal([422, 404, 404], semAcesso);
    }

    [Fact]
    public async Task Gestor_e_rh_usam_as_mesmas_rotas_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var (gestor, clienteDoGestor) = await PessoaAsync(api, "ZCon5 Gestor", perfil: Perfil.Gestor);
        var t = await EmAndamentoAsync(api, Titulo("ZCon5"));
        var m = await ModuloAsync(api, t, 1);
        await LinhaAsync(api, t, gestor, "INSCRITO");

        var comoGestor = await clienteDoGestor.GetAsync(Rota(t, m));
        var comoRh = await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota(t, m));
        var semLogin = new[] { await api.Api.ClienteAnonimo().GetAsync(Rota(t, m)), await api.Api.ClienteAnonimo().GetAsync(Rota(t, m, "/download")) };

        Assert.Equal(200, (int)comoGestor.StatusCode);
        Assert.Equal(404, (int)comoRh.StatusCode); // o RH do elenco não tem linha neste treinamento: a regra é a mesma para todo perfil
        Assert.Equal([401, 401], semLogin.Select(r => (int)r.StatusCode));
    }
}
