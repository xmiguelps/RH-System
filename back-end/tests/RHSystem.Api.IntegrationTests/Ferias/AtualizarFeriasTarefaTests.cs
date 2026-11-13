using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary>
/// A tarefa diária <c>ATUALIZAR_FERIAS</c> no banco REAL, com o relógio em 2026-10-07T12:42Z (quarta, 09:42 em Brasília): <c>APROVADA</c> que começa vira <c>EM_GOZO</c>, a que terminou vira <c>CONCLUIDA</c> (inclusive quando a API ficou parada dias e o pedido pulou o gozo), o pedido <c>SOLICITADA</c> cujo
/// início já passou é cancelado (menos o que espera a resposta da pessoa a uma sugestão: esse só cai pela expiração da sugestão). Só as transições que o trigger do banco aceita; idempotente; sem e-mail; os eventos saem como Sistema.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AtualizarFeriasTarefaTests(MySqlFixture mysql)
{
    private const string Tarefa = "ATUALIZAR_FERIAS";

    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(AtualizarFeriasTarefaTests));

    private static async Task ExecutarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).ExecutarAsync(new TarefaParaExecutar(0, $"{Tarefa}:dia=2026-10-07", null, new DateOnly(2026, 10, 7), null, 0), CancellationToken.None);
    }

    private static Task<string?> StatusAsync(ApiDeAcesso api, int id) => TextoAsync(api, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {id}");

    private static long EventosDoSistema(ApiDeAcesso api) => Contar(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE AREA = 'FERIAS' AND ATOR_TIPO = 'SISTEMA'");

    [Fact]
    public async Task Entra_em_gozo_conclui_e_cancela_so_o_que_a_regra_manda_e_rodar_de_novo_nao_muda_nada()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, _, _) = await EquipeAsync(api, "Tarefa de ferias");
        var pessoa = await PessoaAsync(api, "Da tarefa", new DateOnly(2022, 3, 14), setor);
        var rh = api.Elenco.Rh.FuncionarioId;
        Task<int> Linha(string status, string inicio, string fim, int? gestor = null, bool gestorAprovou = false, int? rhId = null) =>
            SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", inicio, fim, status, gestorId: gestor, gestorAprovou: gestorAprovou, rhId: rhId);

        var comecaHoje = await Linha("APROVADA", "2026-10-07", "2026-10-16", gestorId, true, rh);
        var emCurso = await Linha("APROVADA", "2026-10-04", "2026-10-13", gestorId, true, rh);
        var futura = await Linha("APROVADA", "2026-11-09", "2026-11-18", gestorId, true, rh);
        var terminouOntem = await Linha("APROVADA", "2026-09-27", "2026-10-06", gestorId, true, rh); // a API ficou parada: pulou o gozo
        var jaEmGozoTerminou = await Linha("EM_GOZO", "2026-09-20", "2026-10-06", gestorId, true, rh);
        var emGozoTerminaHoje = await Linha("EM_GOZO", "2026-09-28", "2026-10-07", gestorId, true, rh);
        var vencidaComGestor = await Linha("SOLICITADA", "2026-10-06", "2026-10-15", gestorId);
        var vencidaComRh = await Linha("SOLICITADA", "2026-10-01", "2026-10-10", gestorId, true);
        var comecaHojeEmAnalise = await Linha("SOLICITADA", "2026-10-07", "2026-10-16", gestorId);
        var futuraEmAnalise = await Linha("SOLICITADA", "2026-12-07", "2026-12-16", gestorId);
        var concluida = await Linha("CONCLUIDA", "2026-08-03", "2026-08-12", gestorId, true, rh);
        var cancelada = await Linha("CANCELADA", "2026-08-17", "2026-08-26", gestorId);

        await ExecutarAsync(api);
        var depoisDaPrimeira = new[] { comecaHoje, emCurso, futura, terminouOntem, jaEmGozoTerminou, emGozoTerminaHoje, vencidaComGestor, vencidaComRh, comecaHojeEmAnalise, futuraEmAnalise, concluida, cancelada };
        var primeira = new List<string?>();
        foreach (var id in depoisDaPrimeira)
        {
            primeira.Add(await StatusAsync(api, id));
        }

        var eventos = EventosDoSistema(api);
        await ExecutarAsync(api);
        var segunda = new List<string?>();
        foreach (var id in depoisDaPrimeira)
        {
            segunda.Add(await StatusAsync(api, id));
        }

        Assert.Equal(["EM_GOZO", "EM_GOZO", "APROVADA", "CONCLUIDA", "CONCLUIDA", "EM_GOZO", "CANCELADA", "CANCELADA", "SOLICITADA", "SOLICITADA", "CONCLUIDA", "CANCELADA"], primeira);
        Assert.Equal(primeira, segunda); // idempotente
        Assert.Equal(eventos, EventosDoSistema(api)); // a segunda rodada não achou nada: nenhum evento novo
        Assert.True(eventos >= 3, $"eventos do Sistema = {eventos}");
        Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId)); // nenhuma transição manda e-mail
    }

    [Fact]
    public async Task O_pedido_em_sugestao_de_data_so_expira_pelo_prazo_da_sugestao_mesmo_com_o_inicio_original_vencido()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, _, _) = await EquipeAsync(api, "Tarefa de sugestao");
        var pessoa = await PessoaAsync(api, "Da sugestao", new DateOnly(2022, 3, 14), setor);
        Task<int> Sugestao(string solicitadaEm, string inicio, string fim, string sugestaoInicio, string sugestaoFim) =>
            SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", inicio, fim, "SOLICITADA", solicitadaEm, gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: sugestaoInicio, sugestaoFim: sugestaoFim);

        var noPrazoComInicioVencido = await Sugestao("2026-10-05 10:00:00", "2026-10-06", "2026-10-15", "2026-12-07", "2026-12-21"); // sugerida em 06/10: vale até 11/10
        var expirada = await Sugestao("2026-09-28 10:00:00", "2026-11-16", "2026-11-30", "2026-12-07", "2026-12-21"); // sugerida em 29/09: venceu em 04/10
        var noUltimoDia = await Sugestao("2026-10-01 10:00:00", "2026-12-14", "2026-12-23", "2027-01-11", "2027-01-25"); // sugerida em 02/10: vale até hoje
        var inicioSugeridoPerto = await Sugestao("2026-10-05 10:00:00", "2026-11-23", "2026-12-02", "2026-10-08", "2026-10-17"); // 2 dias antes do início sugerido (08/10) é 06/10: já passou

        await ExecutarAsync(api);

        Assert.Equal(("SOLICITADA", "CANCELADA", "SOLICITADA", "CANCELADA"), (await StatusAsync(api, noPrazoComInicioVencido), await StatusAsync(api, expirada), await StatusAsync(api, noUltimoDia), await StatusAsync(api, inicioSugeridoPerto)));
        foreach (var id in new[] { expirada, inicioSugeridoPerto })
        {
            Assert.Equal(("EXPIROU", "2026-10-07 09:42:00"), (
                await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {id}"),
                await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_RESPOSTA_SUGESTAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {id}")));
        }

        Assert.Null(await TextoAsync(api, $"SELECT RESPOSTA_SUGESTAO FROM FERIAS WHERE ID_FERIAS = {noPrazoComInicioVencido}"));
        Assert.Equal(0, EmailsDe(api, pessoa.FuncionarioId));
    }

    [Fact]
    public async Task A_recuperacao_agenda_a_tarefa_do_dia_e_o_tipo_esta_registrado_uma_so_vez()
    {
        var api = await ApiAsync();
        await using var escopo = api.Api.Services.CreateAsyncScope();

        var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa);
        await tarefa.RecuperarAsync(CancellationToken.None);
        await tarefa.RecuperarAsync(CancellationToken.None); // idempotente

        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND CHAVE_IDEMPOTENCIA = '{Tarefa}:dia=2026-10-07'"));
    }

    [Fact]
    public async Task Ferias_aprovadas_que_terminam_hoje_entram_em_gozo()
    {
        var api = await ApiAsync();
        var (setor, _, gestorId, _, _) = await EquipeAsync(api, "Termina hoje");
        var pessoa = await PessoaAsync(api, "Termina hoje", new DateOnly(2022, 3, 14), setor);
        var id = await SemearAsync(api, pessoa.FuncionarioId, "2025-03-14", "2026-03-13", "2026-09-28", "2026-10-07", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: api.Elenco.Rh.FuncionarioId);

        await ExecutarAsync(api);

        Assert.Equal("EM_GOZO", await StatusAsync(api, id));
    }
}
