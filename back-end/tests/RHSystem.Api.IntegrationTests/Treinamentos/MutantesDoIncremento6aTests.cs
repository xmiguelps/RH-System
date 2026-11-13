using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Treinamentos;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// Um teste por mutante real que sobreviveu à mutação do incremento 6a (T2 a T8): o limite de tentativas na corrida de editar e de encerrar, a leitura velha que o UPDATE condicional desmente, o limite de 100 setores, o progresso dos OUTROS treinamentos que a exclusão não pode levar, a trava do treinamento que não existe e as duas pontas do ano dos certificados.
/// A corrida é provada com decoradores (<c>WithWebHostBuilder</c> + <c>ConfigureTestServices</c>): o repositório devolve "mudou em outra tela" N vezes e depois grava de verdade.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MutantesDoIncremento6aTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MutantesDoIncremento6aTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>O repositório dos treinamentos que recusa as primeiras N atualizações e encerramentos (como se outra aba tivesse mudado o treinamento) e depois grava de verdade.</summary>
    private sealed class RepositorioQueFalhaAsPrimeiras(ITreinamentosRepositorio real, int[] falhas) : ITreinamentosRepositorio
    {
        public Task<int> CriarAsync(CamposDoTreinamento campos, CancellationToken ct) => real.CriarAsync(campos, ct);

        public async Task<bool> AtualizarAsync(TreinamentoLido lido, CamposDoTreinamento novos, CancellationToken ct) => Interlocked.Decrement(ref falhas[0]) >= 0 ? false : await real.AtualizarAsync(lido, novos, ct);

        public Task<bool> PublicarAsync(int treinamentoId, CancellationToken ct) => real.PublicarAsync(treinamentoId, ct);

        public Task<int> InscreverAsync(int treinamentoId, IReadOnlyList<int> funcionarioIds, CancellationToken ct) => real.InscreverAsync(treinamentoId, funcionarioIds, ct);

        public async Task<bool> EncerrarAsync(TreinamentoLido lido, DateTime agoraUtc, CancellationToken ct) => Interlocked.Decrement(ref falhas[1]) >= 0 ? false : await real.EncerrarAsync(lido, agoraUtc, ct);

        public Task ExcluirAsync(int treinamentoId, CancellationToken ct) => real.ExcluirAsync(treinamentoId, ct);
    }

    /// <summary>A consulta que mostra, na PRIMEIRA leitura, o treinamento como rascunho (a leitura "velha"), embora outra aba já o tenha publicado.</summary>
    private sealed class ConsultaComLeituraVelha(IConsultaDeTreinamentos real, int[] primeiraLeitura) : IConsultaDeTreinamentos
    {
        public async Task<TreinamentoLido?> ObterAsync(int treinamentoId, CancellationToken ct)
        {
            var lido = await real.ObterAsync(treinamentoId, ct);
            return lido is not null && Interlocked.Decrement(ref primeiraLeitura[0]) >= 0 ? lido with { Publicado = false } : lido;
        }

        public Task<TreinamentoRh?> ObterDetalheAsync(int treinamentoId, DateOnly hoje, CancellationToken ct) => real.ObterDetalheAsync(treinamentoId, hoje, ct);

        public Task<int> ContarPublicoAsync(PublicoDoTreinamento publico, IReadOnlyList<int> setorIds, IReadOnlyList<int> funcionarioIds, DateOnly hoje, CancellationToken ct) => real.ContarPublicoAsync(publico, setorIds, funcionarioIds, hoje, ct);

        public Task<(IReadOnlyList<TreinamentoRhResumo> Itens, int Total, TreinamentoRhContagens Contagens)> ListarAsync(SituacaoDoTreinamento? situacao, ParametrosDeLista parametros, DateOnly hoje, CancellationToken ct) => real.ListarAsync(situacao, parametros, hoje, ct);

        public Task<TreinamentoRhIndicadores> IndicadoresAsync(DateOnly hoje, CancellationToken ct) => real.IndicadoresAsync(hoje, ct);

        public Task<PendentesDoTreinamento> ContarPendentesAsync(int treinamentoId, CancellationToken ct) => real.ContarPendentesAsync(treinamentoId, ct);

        public Task<int> ContarCertificadosEmitidosAsync(int treinamentoId, CancellationToken ct) => real.ContarCertificadosEmitidosAsync(treinamentoId, ct);

        public Task<IReadOnlyList<int>> SetoresInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct) => real.SetoresInexistentesAsync(ids, ct);

        public Task<IReadOnlyList<int>> FuncionariosInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct) => real.FuncionariosInexistentesAsync(ids, ct);
    }

    private static async Task<HttpClient> ComoRhAsync(ApiDeAcesso api, Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> fabrica)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        return cliente;
    }

    private static Microsoft.AspNetCore.Mvc.Testing.WebApplicationFactory<Program> ComRepositorioQueFalha(ApiDeAcesso api, int[] falhas) =>
        api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<ITreinamentosRepositorio>();
            s.AddScoped<TreinamentosRepositorio>();
            s.AddScoped<ITreinamentosRepositorio>(sp => new RepositorioQueFalhaAsPrimeiras(sp.GetRequiredService<TreinamentosRepositorio>(), falhas));
        }));

    // ---------------------------------------------------------------- M103, M106, M141, M153: a corrida de editar e de encerrar

    [Fact]
    public async Task Editar_refaz_a_leitura_e_tenta_de_novo_ate_5_vezes_e_na_sexta_recusa_com_409_sem_gravar_nada()
    {
        var api = await ApiAsync();
        var titulo = Titulo("ZMut6a editar");
        var id = await SemearAsync(api, titulo);
        var corpo = new { titulo = "Titulo depois da corrida", obrigatorio = false, publico = "TODOS" };

        await using var comQuatroFalhas = ComRepositorioQueFalha(api, [4, 0]);
        var quartaTentativa = await (await ComoRhAsync(api, comQuatroFalhas)).PutAsJsonAsync($"/api/rh/treinamentos/{id}", corpo);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET TITULO = {Valor(titulo)} WHERE ID_TREINAMENTO = {id}");
        await using var comCincoFalhas = ComRepositorioQueFalha(api, [5, 0]);
        var quintaFalha = await (await ComoRhAsync(api, comCincoFalhas)).PutAsJsonAsync($"/api/rh/treinamentos/{id}", corpo);

        Assert.Equal(200, (int)quartaTentativa.StatusCode); // a 5ª tentativa grava
        Assert.Equal(409, (int)quintaFalha.StatusCode); // cinco recusas seguidas: desiste
        Assert.Equal("Este treinamento mudou em outra tela. Atualize a página e tente de novo.", (await CorpoAsync(quintaFalha)).GetProperty("detail").GetString());
        Assert.Equal(titulo, await TextoAsync(api, $"SELECT TITULO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}")); // nada foi gravado
    }

    [Fact]
    public async Task Encerrar_refaz_a_leitura_e_tenta_de_novo_ate_5_vezes_e_na_sexta_recusa_com_409()
    {
        var api = await ApiAsync();
        var quatro = await SemearAsync(api, Titulo("ZMut6a encerrar 4"), "EM_ANDAMENTO");
        var cinco = await SemearAsync(api, Titulo("ZMut6a encerrar 5"), "EM_ANDAMENTO");

        await using var comQuatroFalhas = ComRepositorioQueFalha(api, [0, 4]);
        var quartaTentativa = await (await ComoRhAsync(api, comQuatroFalhas)).PostAsync($"/api/rh/treinamentos/{quatro}/encerrar", null);
        await using var comCincoFalhas = ComRepositorioQueFalha(api, [0, 5]);
        var quintaFalha = await (await ComoRhAsync(api, comCincoFalhas)).PostAsync($"/api/rh/treinamentos/{cinco}/encerrar", null);

        Assert.Equal(200, (int)quartaTentativa.StatusCode);
        Assert.Equal(409, (int)quintaFalha.StatusCode);
        Assert.Null(await TextoAsync(api, $"SELECT ENCERRADO_EM FROM TREINAMENTOS WHERE ID_TREINAMENTO = {cinco}"));
    }

    [Fact]
    public async Task Editar_com_a_leitura_velha_de_rascunho_e_o_treinamento_ja_publicado_relê_e_recusa_o_campo_travado_em_vez_de_dar_200()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZMut6a velha"), "EM_ANDAMENTO", obrigatorio: false, publico: "TODOS");
        var primeiraLeitura = new[] { 1 };
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(s =>
        {
            s.RemoveAll<IConsultaDeTreinamentos>();
            s.AddScoped<ConsultaDeTreinamentos>();
            s.AddScoped<IConsultaDeTreinamentos>(sp => new ConsultaComLeituraVelha(sp.GetRequiredService<ConsultaDeTreinamentos>(), primeiraLeitura));
        }));

        var resposta = await (await ComoRhAsync(api, fabrica)).PutAsJsonAsync($"/api/rh/treinamentos/{id}", new { titulo = "Mudou o obrigatório", obrigatorio = true, publico = "TODOS", comecaEm = "2026-10-01" });

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("TREINAMENTO_CAMPO_TRAVADO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal("nao", await TextoAsync(api, $"SELECT IF(OBRIGATORIO, 'sim', 'nao') FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}")); // o UPDATE de rascunho não gravou em treinamento publicado
    }

    // ---------------------------------------------------------------- M108: o limite de setores

    [Fact]
    public async Task A_contagem_do_publico_aceita_exatamente_100_setores_e_recusa_101()
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();
        string Consulta(int quantos) => "publico=SETORES&" + string.Join('&', Enumerable.Range(1, quantos).Select(i => $"setorIds={i}"));

        var cem = await rh.GetAsync($"/api/rh/treinamentos/publico/contagem?{Consulta(100)}");
        var cento_e_um = await rh.GetAsync($"/api/rh/treinamentos/publico/contagem?{Consulta(101)}");

        Assert.Equal(200, (int)cem.StatusCode);
        Assert.Equal(400, (int)cento_e_um.StatusCode);
        Assert.Equal("Escolha até 100 setores.", (await CorpoAsync(cento_e_um)).GetProperty("errors").GetProperty("setorIds")[0].GetString());
    }

    // ---------------------------------------------------------------- M161: a exclusão não leva o progresso dos outros

    [Fact]
    public async Task Excluir_um_treinamento_nao_apaga_o_progresso_nem_os_modulos_de_outro_treinamento()
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut6a Ana"), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
        var vizinho = await SemearAsync(api, Titulo("ZMut6a vizinho"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        var moduloDoVizinho = await ModuloAsync(api, vizinho, 1);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({vizinho}, {pessoa}, 'EM_ANDAMENTO')");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({moduloDoVizinho}, {pessoa}, '2026-10-05 10:00:00')");
        var apagado = await SemearAsync(api, Titulo("ZMut6a apagado"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        var moduloDoApagado = await ModuloAsync(api, apagado, 1);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({apagado}, {pessoa}, 'EM_ANDAMENTO')");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO, DATA_CONCLUSAO) VALUES ({moduloDoApagado}, {pessoa}, '2026-10-05 11:00:00')");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"/api/rh/treinamentos/{apagado}?confirmar=true");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {moduloDoVizinho} AND ID_FUNCIONARIO = {pessoa}"));
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {moduloDoApagado}"));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_MODULOS WHERE ID_MODULO = {moduloDoVizinho}"));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {vizinho}"));
    }

    // ---------------------------------------------------------------- M170: a trava prende a linha CERTA

    [Fact]
    public async Task A_trava_do_treinamento_devolve_nulo_para_o_que_nao_existe_e_o_estado_para_o_que_existe()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZMut6a trava rascunho"));
        var encerrado = await SemearAsync(api, Titulo("ZMut6a trava encerrado"), "ENCERRADO");
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var repositorio = escopo.ServiceProvider.GetRequiredService<IModulosRepositorio>();

        var inexistente = await repositorio.TravarTreinamentoAsync(987654, CancellationToken.None);
        var doRascunho = await repositorio.TravarTreinamentoAsync(rascunho, CancellationToken.None);
        var doEncerrado = await repositorio.TravarTreinamentoAsync(encerrado, CancellationToken.None);

        Assert.Null(inexistente);
        Assert.Equal((false, false), (doRascunho!.Publicado, doRascunho.EncerradoEmUtc is not null));
        Assert.Equal((true, true), (doEncerrado!.Publicado, doEncerrado.EncerradoEmUtc is not null));
    }

    // ---------------------------------------------------------------- M201, M202: as duas pontas do ano dos certificados

    [Fact]
    public async Task O_indicador_de_certificados_do_ano_conta_o_primeiro_e_o_ultimo_dia_do_ano_e_nao_o_dia_antes_nem_o_dia_depois()
    {
        var api = await ApiAsync();
        var rh = await api.ClienteDoRhAgoraAsync();
        var antes = (await CorpoAsync(await rh.GetAsync("/api/rh/treinamentos/resumo"))).GetProperty("certificadosEmitidosNoAno").GetInt32();
        var t = await SemearAsync(api, Titulo("ZMut6a anos"), "EM_ANDAMENTO", obrigatorio: true, emite: true);
        var datas = new[] { "2025-12-31", "2026-01-01", "2026-12-31", "2027-01-01" };
        foreach (var data in datas)
        {
            var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut6a Pessoa"), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({t}, {pessoa}, 'CONCLUIDO')");
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET DATA_EMISSAO = '{data}' WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {pessoa}");
        }

        var depois = (await CorpoAsync(await rh.GetAsync("/api/rh/treinamentos/resumo"))).GetProperty("certificadosEmitidosNoAno").GetInt32();

        Assert.Equal(2, depois - antes);
    }
}
