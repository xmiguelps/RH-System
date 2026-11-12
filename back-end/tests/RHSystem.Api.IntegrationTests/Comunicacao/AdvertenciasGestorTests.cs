using System.Text.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Comunicacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Comunicacao.AdvertenciasDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// As advertências pelo gestor (<c>/api/gestor</c>), no banco REAL com o relógio em 2026-10-07T12:42Z: SÓ a equipe dele (as pessoas ativas do setor, sem o próprio gestor); outra equipe, o próprio gestor e pessoa inativa dão 404. O gestor vê a situação mas nunca o texto da
/// contestação. Registrar aplica em nome do gestor logado, manda o e-mail à pessoa e uma cópia a cada pessoa ativa do RH (nunca ao gestor). Cada teste usa o seu setor, então as listas e os números não dependem dos outros testes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AdvertenciasGestorTests(MySqlFixture mysql)
{
    private const string Rota = "/api/gestor/advertencias";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AdvertenciasGestorTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpClient> GestorAsync(ApiDeAcesso api, SetorDeAdvertencia setor) => await ClienteDeAsync(api, setor.GestorFuncionarioId);

    private static async Task<JsonElement> CorpoDeAsync(HttpClient cliente, string rota)
    {
        var resposta = await cliente.GetAsync(rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static object Registro(string data = "2026-10-05", string tipo = "ESCRITA", string motivo = "Atrasos repetidos", string descricao = "Cinco atrasos na abertura da loja.") => new { dataDoOcorrido = data, tipo, motivo, descricao };

    // ---------------------------------------------------------------- a lista da equipe

    [Fact]
    public async Task A_lista_traz_so_a_equipe_registrada_por_qualquer_um_de_um_ano_do_mais_recente_para_o_mais_antigo()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var outro = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var (b, _) = await NovoMembroAsync(api, setor);
        var (inativo, _) = await NovoMembroAsync(api, setor);
        var (estranho, _) = await NovoMembroAsync(api, outro);
        await SemearAsync(api, inativo, "2026-07-01");
        await InativarAsync(api, inativo); // quem saiu deixa de ser da equipe, e as advertências dele saem da lista do gestor
        var doGestor = await SemearAsync(api, a, "2026-03-01", gestorId: setor.GestorId);
        var doRh = await SemearAsync(api, b, "2026-05-01");
        var maisRecente = await SemearAsync(api, a, "2026-09-01", situacao: "CANCELADA", textoContestacao: "Contestei.", contestacaoLocal: "2026-09-02 10:00:00");
        await SemearAsync(api, a, "2025-12-01"); // outro ano
        await SemearAsync(api, estranho, "2026-04-01"); // outra equipe
        await SemearAsync(api, setor.GestorFuncionarioId, "2026-06-01"); // o próprio gestor não é da equipe

        var gestor = await GestorAsync(api, setor);
        var lista = await CorpoDeAsync(gestor, Rota);

        Assert.Equal([maisRecente, doRh, doGestor], Ids(lista));
        Assert.Equal((3, 1), (lista.GetProperty("total").GetInt32(), lista.GetProperty("totalPaginas").GetInt32()));
        Assert.Equal([doRh, doGestor], Ids(await CorpoDeAsync(gestor, $"{Rota}?situacao=PENDENTE&ano=2026")));
        Assert.Equal([doGestor], Ids(await CorpoDeAsync(gestor, $"{Rota}?situacao=PENDENTE&ano=2026&pagina=2&tamanhoPagina=1")));
        Assert.Equal([maisRecente], Ids(await CorpoDeAsync(gestor, $"{Rota}?situacao=CANCELADA")));
        Assert.Empty(Ids(await CorpoDeAsync(gestor, $"{Rota}?ano=2030")));
        Assert.Equal(1, (await CorpoDeAsync(gestor, $"{Rota}?ano=2025")).GetProperty("total").GetInt32());
    }

    [Fact]
    public async Task O_gestor_sem_setor_recebe_listas_vazias_resumo_zerado_e_404_no_resto()
    {
        var api = await ApiAsync();
        var cliente = await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId);

        var lista = await CorpoDeAsync(cliente, Rota);
        var resumo = await CorpoDeAsync(cliente, $"{Rota}/resumo");
        var detalhe = await cliente.GetAsync($"{Rota}/1");
        var pessoa = await cliente.GetAsync($"/api/gestor/equipe/{api.Elenco.MembroA.FuncionarioId}/advertencias");
        var registrar = await PostAsync(cliente, $"/api/gestor/equipe/{api.Elenco.MembroA.FuncionarioId}/advertencias", Registro());

        Assert.Empty(Ids(lista));
        Assert.Equal((0, 0, 0, 0), (resumo.GetProperty("registradas").GetInt32(), resumo.GetProperty("aguardandoCiencia").GetProperty("quantidade").GetInt32(), resumo.GetProperty("pessoasComAdvertencia").GetInt32(), resumo.GetProperty("totalDaEquipe").GetInt32()));
        Assert.Empty(resumo.GetProperty("porPessoa").EnumerateArray());
        Assert.Empty(resumo.GetProperty("aguardandoCiencia").GetProperty("pessoas").EnumerateArray());
        Assert.Equal((404, 404, 404), ((int)detalhe.StatusCode, (int)pessoa.StatusCode, (int)registrar.StatusCode));
    }

    [Theory]
    [InlineData("ano=1999", "ano")]
    [InlineData("ano=2101", "ano")]
    [InlineData("situacao=ABC", "situacao")]
    [InlineData("pagina=0", "pagina")]
    [InlineData("tamanhoPagina=101", "tamanhoPagina")]
    public async Task Parametro_invalido_da_lista_da_equipe_e_400_com_o_erro_no_campo(string consulta, string campo)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);

        var resposta = await (await GestorAsync(api, setor)).GetAsync($"{Rota}?{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
    }

    // ---------------------------------------------------------------- o resumo

    [Fact]
    public async Task O_resumo_usa_a_regra_unica_das_nao_canceladas_do_ano_e_traz_todas_as_pessoas_da_equipe_inclusive_com_zero()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (ana, nomeAna) = await NovoMembroAsync(api, setor, prefixo: "Ana Resumo");
        var (bia, nomeBia) = await NovoMembroAsync(api, setor, prefixo: "Bia Resumo");
        var (carla, nomeCarla) = await NovoMembroAsync(api, setor, prefixo: "Carla Resumo");
        var (dora, nomeDora) = await NovoMembroAsync(api, setor, prefixo: "Dora Resumo");
        await SemearAsync(api, bia, "2026-01-10", situacao: "PENDENTE");
        await SemearAsync(api, bia, "2026-02-10", situacao: "APLICADA", cienciaLocal: "2026-02-11 10:00:00");
        await SemearAsync(api, bia, "2026-03-10", situacao: "PENDENTE");
        await SemearAsync(api, ana, "2026-04-10", situacao: "CONTESTADA", textoContestacao: "Contestei.", contestacaoLocal: "2026-04-11 10:00:00");
        await SemearAsync(api, carla, "2026-05-10", situacao: "CANCELADA", textoContestacao: "Contestei e ganhei.", contestacaoLocal: "2026-05-11 10:00:00"); // cancelada: não conta
        await SemearAsync(api, dora, "2025-05-10"); // outro ano: não conta

        var resumo = await CorpoDeAsync(await GestorAsync(api, setor), $"{Rota}/resumo");

        Assert.Equal((2026, 4, 2, 4), (resumo.GetProperty("ano").GetInt32(), resumo.GetProperty("registradas").GetInt32(), resumo.GetProperty("pessoasComAdvertencia").GetInt32(), resumo.GetProperty("totalDaEquipe").GetInt32()));
        var aguardando = resumo.GetProperty("aguardandoCiencia");
        Assert.Equal(2, aguardando.GetProperty("quantidade").GetInt32());
        Assert.Equal([nomeBia], [.. aguardando.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("nome").GetString()!)]);
        Assert.Equal(
            [(nomeBia, 3), (nomeAna, 1), (nomeCarla, 0), (nomeDora, 0)],
            [.. resumo.GetProperty("porPessoa").EnumerateArray().Select(p => (p.GetProperty("funcionario").GetProperty("nome").GetString()!, p.GetProperty("quantidade").GetInt32()))]);
        Assert.Equal(4, resumo.GetProperty("porPessoa").EnumerateArray().Sum(p => p.GetProperty("quantidade").GetInt32())); // a soma das quantidades é registradas
        Assert.Equal(1, (await CorpoDeAsync(await GestorAsync(api, setor), $"{Rota}/resumo?ano=2025")).GetProperty("registradas").GetInt32());
        Assert.Equal(400, (int)(await (await GestorAsync(api, setor)).GetAsync($"{Rota}/resumo?ano=1999")).StatusCode);
    }

    // ---------------------------------------------------------------- o detalhe

    [Fact]
    public async Task O_detalhe_da_equipe_nao_traz_o_texto_da_contestacao_nem_o_desfecho_e_de_fora_da_equipe_e_404()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var outro = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var (estranho, _) = await NovoMembroAsync(api, outro);
        var (inativo, _) = await NovoMembroAsync(api, setor);
        var deInativo = await SemearAsync(api, inativo, "2026-09-15");
        await InativarAsync(api, inativo);
        var contestada = await SemearAsync(api, a, "2026-09-15", situacao: "CONTESTADA", textoContestacao: "segredo-da-contestacao", contestacaoLocal: "2026-09-17 10:05:00", gestorId: setor.GestorId);
        var deOutroSetor = await SemearAsync(api, estranho, "2026-09-15");
        var doProprioGestor = await SemearAsync(api, setor.GestorFuncionarioId, "2026-09-15");
        var gestor = await GestorAsync(api, setor);

        var resposta = await gestor.GetAsync($"{Rota}/{contestada}");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(["id", "data", "funcionario", "tipo", "motivo", "aplicadaPor", "situacao", "descricao", "dataCienciaUtc"], [.. corpo.EnumerateObject().Select(p => p.Name)]);
        Assert.Equal(("CONTESTADA", setor.GestorNome), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("nome").GetString()));
        Assert.DoesNotContain("segredo-da-contestacao", corpo.GetRawText(), StringComparison.Ordinal);
        Assert.Equal((404, 404, 404, 404), ((int)(await gestor.GetAsync($"{Rota}/{deOutroSetor}")).StatusCode, (int)(await gestor.GetAsync($"{Rota}/{doProprioGestor}")).StatusCode, (int)(await gestor.GetAsync($"{Rota}/{int.MaxValue}")).StatusCode, (int)(await gestor.GetAsync($"{Rota}/{deInativo}")).StatusCode));
    }

    // ---------------------------------------------------------------- a lista de uma pessoa da equipe

    [Fact]
    public async Task A_lista_da_pessoa_traz_todos_os_anos_com_as_canceladas_e_pessoa_de_fora_da_equipe_e_404()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var outro = await NovoSetorAsync(api);
        var (a, _) = await NovoMembroAsync(api, setor);
        var (estranho, _) = await NovoMembroAsync(api, outro);
        var (inativo, _) = await NovoMembroAsync(api, setor, ativo: false);
        var velha = await SemearAsync(api, a, "2024-03-01");
        var cancelada = await SemearAsync(api, a, "2025-03-01", situacao: "CANCELADA", textoContestacao: "Contestei.", contestacaoLocal: "2025-03-02 10:00:00");
        var atual = await SemearAsync(api, a, "2026-03-01");
        var gestor = await GestorAsync(api, setor);

        var lista = await CorpoDeAsync(gestor, $"/api/gestor/equipe/{a}/advertencias");

        Assert.Equal([atual, cancelada, velha], Ids(lista));
        Assert.Equal(404, (int)(await gestor.GetAsync($"/api/gestor/equipe/{estranho}/advertencias")).StatusCode);
        Assert.Equal(404, (int)(await gestor.GetAsync($"/api/gestor/equipe/{setor.GestorFuncionarioId}/advertencias")).StatusCode);
        Assert.Equal(404, (int)(await gestor.GetAsync($"/api/gestor/equipe/{inativo}/advertencias")).StatusCode);
        Assert.Equal(404, (int)(await gestor.GetAsync($"/api/gestor/equipe/{int.MaxValue}/advertencias")).StatusCode);
        Assert.Equal(400, (int)(await gestor.GetAsync($"/api/gestor/equipe/{a}/advertencias?pagina=0")).StatusCode);
    }

    // ---------------------------------------------------------------- registrar

    [Fact]
    public async Task Registrar_aplica_em_nome_do_gestor_logado_manda_o_e_mail_a_pessoa_e_a_copia_a_cada_pessoa_do_RH_e_nunca_ao_gestor()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, nome) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = 'gestor.adv.{setor.SetorId}@empresa.com.br' WHERE ID_FUNCIONARIO = {setor.GestorFuncionarioId}");
        var gestor = await GestorAsync(api, setor);

        var resposta = await PostAsync(gestor, $"/api/gestor/equipe/{pessoa}/advertencias", Registro());

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(["id", "data", "funcionario", "tipo", "motivo", "aplicadaPor", "situacao", "descricao", "dataCienciaUtc"], [.. corpo.EnumerateObject().Select(p => p.Name)]);
        Assert.Equal(("PENDENTE", "GESTOR", setor.GestorNome, nome), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("tipo").GetString(), corpo.GetProperty("aplicadaPor").GetProperty("nome").GetString(), corpo.GetProperty("funcionario").GetProperty("nome").GetString()));
        Assert.Equal(setor.GestorId.ToString(), await TextoAsync(api, $"SELECT ID_GESTOR FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        var rhs = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS f JOIN NIVEIS_DE_ACESSO n ON n.ID_NIVEL_ACESSO = f.ID_NIVEL_ACESSO WHERE f.ATIVO = TRUE AND f.EMAIL_CORPORATIVO IS NOT NULL AND n.NOME_NIVEL_ACESSO = 'RH'");
        Assert.True(rhs >= 1);
        Assert.Equal(rhs + 1, EmailsDa(api, "ADVERTENCIA_REGISTRADA", id)); // a pessoa e uma cópia a cada pessoa do RH
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {pessoa}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {setor.GestorFuncionarioId}"));
        Assert.Equal(rhs, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id} AND JSON_EXTRACT(CARGA, '$.paraRh') = TRUE"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "ADVERTENCIAS", id));
        Assert.Equal((AuditoriaArea.Advertencias, AuditoriaAcao.Criou, $"Registrou uma advertência escrita de 05/10/2026 para {nome}.", setor.GestorFuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Quem_do_RH_e_a_propria_pessoa_advertida_recebe_um_e_mail_so_e_nao_a_copia()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_NIVEL_ACESSO = (SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = 'RH') WHERE ID_FUNCIONARIO = {pessoa}");

        var resposta = await PostAsync(await GestorAsync(api, setor), $"/api/gestor/equipe/{pessoa}/advertencias", Registro());

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Pessoa_sem_e_mail_nao_trava_o_registro_e_o_RH_ainda_recebe_a_copia()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO = {pessoa}");

        var resposta = await PostAsync(await GestorAsync(api, setor), $"/api/gestor/equipe/{pessoa}/advertencias", Registro());

        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND REFERENCIA_ID = {id} AND ID_FUNCIONARIO = {pessoa}"));
        Assert.True(EmailsDa(api, "ADVERTENCIA_REGISTRADA", id) >= 1);
    }

    [Fact]
    public async Task Fora_da_equipe_o_proprio_gestor_e_inativo_dao_404_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var outro = await NovoSetorAsync(api);
        var (estranho, _) = await NovoMembroAsync(api, outro);
        var (inativo, _) = await NovoMembroAsync(api, setor, ativo: false);
        var gestor = await GestorAsync(api, setor);

        foreach (var alvo in new[] { estranho, setor.GestorFuncionarioId, inativo, int.MaxValue })
        {
            Assert.Equal(404, (int)(await PostAsync(gestor, $"/api/gestor/equipe/{alvo}/advertencias", Registro())).StatusCode);
        }

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO IN ({estranho}, {setor.GestorFuncionarioId}, {inativo})"));
    }

    [Theory]
    [InlineData("dataDoOcorrido", "2026-10-08")]
    [InlineData("dataDoOcorrido", "0001-01-01")]
    [InlineData("motivo", "")]
    [InlineData("descricao", "   ")]
    public async Task Campo_invalido_do_registro_do_gestor_e_400_com_o_erro_no_campo(string campo, string valor)
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var corpo = new Dictionary<string, object?> { ["dataDoOcorrido"] = "2026-10-05", ["tipo"] = "VERBAL", ["motivo"] = "Motivo", ["descricao"] = "Descrição" };
        corpo[campo] = valor;

        var resposta = await PostAsync(await GestorAsync(api, setor), $"/api/gestor/equipe/{pessoa}/advertencias", corpo);

        Assert.Equal(400, (int)resposta.StatusCode);
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    private sealed class ConsultaQueInativaDepoisDeLer(IConsultaDeAdvertencias real, string conexao) : IConsultaDeAdvertencias
    {
        public Task<PaginaDeAdvertencias> ListarAsync(EscopoDeAdvertencias escopo, FiltroDeAdvertencias filtro, RHSystem.Application.Comum.Consultas.ParametrosDeLista parametros, CancellationToken ct) => real.ListarAsync(escopo, filtro, parametros, ct);

        public Task<AdvertenciaLida?> ObterAsync(int advertenciaId, EscopoDeAdvertencias escopo, CancellationToken ct) => real.ObterAsync(advertenciaId, escopo, ct);

        public async Task<PessoaParaAdvertencia?> ObterPessoaAsync(int funcionarioId, EscopoDeAdvertencias escopo, CancellationToken ct)
        {
            var pessoa = await real.ObterPessoaAsync(funcionarioId, escopo, ct);
            if (pessoa is not null)
            {
                // a pessoa é desligada logo depois de lida (outra aba): a gravação encontra o trigger do banco
                await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionarioId}");
            }

            return pessoa;
        }

        public Task<IReadOnlyList<AdvertenciaParaResumoRh>> DoAnoParaOResumoDoRhAsync(int ano, CancellationToken ct) => real.DoAnoParaOResumoDoRhAsync(ano, ct);

        public Task<(IReadOnlyList<RHSystem.Application.Colaboradores.FuncionarioResumo> Equipe, IReadOnlyList<AdvertenciaParaResumoDaEquipe> Advertencias)> DoAnoParaOResumoDaEquipeAsync(int ano, RHSystem.Application.Escopo.EquipeDoGestor equipe, CancellationToken ct) =>
            real.DoAnoParaOResumoDaEquipeAsync(ano, equipe, ct);

        public Task<IReadOnlyList<DestinatarioDoRh>> DestinatariosDoRhAsync(CancellationToken ct) => real.DestinatariosDoRhAsync(ct);
    }

    [Fact]
    public async Task A_pessoa_desligada_entre_a_leitura_e_a_gravacao_e_422_FUNCIONARIO_INATIVO_pelo_trigger_e_nao_deixa_e_mail_nem_evento()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IConsultaDeAdvertencias>();
            servicos.AddScoped<IConsultaDeAdvertencias>(sp => new ConsultaQueInativaDepoisDeLer(ActivatorUtilities.CreateInstance<ConsultaDeAdvertencias>(sp), api.Conexao));
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await GestorAsync(api, setor)).DefaultRequestHeaders.Authorization;

        var resposta = await PostAsync(cliente, $"/api/gestor/equipe/{pessoa}/advertencias", Registro());

        Assert.Equal(422, (int)resposta.StatusCode);
        var erro = await CorpoAsync(resposta);
        Assert.Equal(("FUNCIONARIO_INATIVO", "Nao e possivel registrar advertencia para funcionario inativo."), (erro.GetProperty("codigo").GetString(), erro.GetProperty("regraDoBanco").GetString()));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO = {pessoa}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'ADVERTENCIA_REGISTRADA' AND ID_FUNCIONARIO = {pessoa}"));
    }

    // ---------------------------------------------------------------- perfis e contrato

    [Fact]
    public async Task As_rotas_do_gestor_so_atendem_o_gestor()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-09-15");

        foreach (var (metodo, rota) in new[] { ("GET", Rota), ("GET", $"{Rota}/resumo"), ("GET", $"{Rota}/{id}"), ("GET", $"/api/gestor/equipe/{pessoa}/advertencias"), ("POST", $"/api/gestor/equipe/{pessoa}/advertencias") })
        {
            foreach (var quem in new[] { api.Elenco.MembroA, api.Elenco.Rh })
            {
                var cliente = await api.ClienteAgoraAsync(quem.UsuarioId);
                var resposta = metodo == "GET" ? await cliente.GetAsync(rota) : await PostAsync(cliente, rota, Registro());
                Assert.Equal(403, (int)resposta.StatusCode);
            }

            var anonimo = api.Api.ClienteAnonimo();
            Assert.Equal(401, (int)(metodo == "GET" ? await anonimo.GetAsync(rota) : await PostAsync(anonimo, rota, Registro())).StatusCode);
        }
    }

    [Fact]
    public async Task As_cinco_rotas_respondem_como_o_contrato_manda()
    {
        var api = await ApiAsync();
        var setor = await NovoSetorAsync(api);
        var (pessoa, _) = await NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, pessoa, "2026-09-15");
        var gestor = await GestorAsync(api, setor);

        await (await gestor.GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarAdvertenciasDaEquipe");
        await (await gestor.GetAsync($"{Rota}?ano=1999")).AfirmarConformeAoContratoAsync("listarAdvertenciasDaEquipe"); // 400
        await (await gestor.GetAsync($"{Rota}/resumo")).AfirmarConformeAoContratoAsync("obterResumoDasAdvertenciasDaEquipe");
        await (await gestor.GetAsync($"{Rota}/resumo?ano=2101")).AfirmarConformeAoContratoAsync("obterResumoDasAdvertenciasDaEquipe"); // 400
        await (await gestor.GetAsync($"{Rota}/{id}")).AfirmarConformeAoContratoAsync("obterAdvertenciaDaEquipe");
        await (await gestor.GetAsync($"{Rota}/{int.MaxValue}")).AfirmarConformeAoContratoAsync("obterAdvertenciaDaEquipe"); // 404
        await (await gestor.GetAsync($"/api/gestor/equipe/{pessoa}/advertencias")).AfirmarConformeAoContratoAsync("listarAdvertenciasDoFuncionarioDaEquipe");
        await (await gestor.GetAsync($"/api/gestor/equipe/{int.MaxValue}/advertencias")).AfirmarConformeAoContratoAsync("listarAdvertenciasDoFuncionarioDaEquipe"); // 404
        await (await PostAsync(gestor, $"/api/gestor/equipe/{pessoa}/advertencias", Registro())).AfirmarConformeAoContratoAsync("registrarAdvertenciaDaEquipe");
        await (await PostAsync(gestor, $"/api/gestor/equipe/{pessoa}/advertencias", Registro(motivo: ""))).AfirmarConformeAoContratoAsync("registrarAdvertenciaDaEquipe"); // 400
        await (await PostAsync(gestor, $"/api/gestor/equipe/{int.MaxValue}/advertencias", Registro())).AfirmarConformeAoContratoAsync("registrarAdvertenciaDaEquipe"); // 404
    }
}
