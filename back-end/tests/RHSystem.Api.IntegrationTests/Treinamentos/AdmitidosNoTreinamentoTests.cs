using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// A tarefa diária <c>INCLUIR_ADMITIDOS_NO_TREINAMENTO</c>, no banco REAL com o relógio em 2026-10-07T12:42Z (09:42 local). Para cada treinamento OBRIGATÓRIO, publicado, não encerrado e com a caixa <c>incluiAdmitidos</c>, cria a linha <c>INSCRITO</c> de toda pessoa ATIVA, com a admissão já ocorrida, do público e sem linha. Idempotente; um evento <c>ALTEROU</c> do Sistema por treinamento e dia só com a contagem (sem nomes);
/// opcional, encerrado, rascunho e treinamento sem a caixa não recebem ninguém; a recuperação agenda uma tarefa por dia.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AdmitidosNoTreinamentoTests(MySqlFixture mysql)
{
    private const string Tarefa = "INCLUIR_ADMITIDOS_NO_TREINAMENTO";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AdmitidosNoTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setor, bool ativa = true, DateOnly? admissao = null) =>
        ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, setor, ativa, admissao ?? new DateOnly(2026, 10, 6));

    private static async Task ExecutarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).ExecutarAsync(new TarefaParaExecutar(0, $"{Tarefa}:dia=2026-10-07", null, new DateOnly(2026, 10, 7), null, 0), CancellationToken.None);
    }

    private static Task<long> LinhasAsync(ApiDeAcesso api, int treinamento, params int[] pessoas) =>
        ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO IN ({string.Join(", ", pessoas)}) AND STATUS_TREINAMENTO = 'INSCRITO'");

    [Fact]
    public async Task Inclui_o_admitido_o_que_ja_comecou_e_quem_mudou_para_um_setor_do_publico_pula_quem_nao_deve_e_nao_duplica_ao_rodar_de_novo()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZAdm1 setor"));
        var outroSetor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZAdm1 outro"));
        var titulo = Titulo("ZAdm1 trein");
        var t = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true, publico: "SETORES", admitidos: true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({t}, {setor})");
        var admitidoOntem = await PessoaAsync(api, "ZAdm1 Ana", setor, admissao: new DateOnly(2026, 10, 6));
        var comecouHoje = await PessoaAsync(api, "ZAdm1 Bia", setor, admissao: new DateOnly(2026, 10, 7)); // a admissão de hoje já ocorreu
        var mudouDeSetor = await PessoaAsync(api, "ZAdm1 Caio", outroSetor);
        var emAdmissao = await PessoaAsync(api, "ZAdm1 Dani", setor, admissao: new DateOnly(2026, 10, 8));
        var inativa = await PessoaAsync(api, "ZAdm1 Edu", setor, ativa: false, admissao: new DateOnly(2026, 1, 2));
        var jaTinha = await PessoaAsync(api, "ZAdm1 Fabi", setor);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({t}, {jaTinha}, 'EM_ANDAMENTO', '2026-10-02')");

        await ExecutarAsync(api);
        await ExecutarAsync(api); // idempotente

        Assert.Equal(2L, await LinhasAsync(api, t, admitidoOntem, comecouHoje));
        Assert.Equal(0L, await LinhasAsync(api, t, mudouDeSetor, emAdmissao, inativa));
        Assert.Equal("EM_ANDAMENTO|2026-10-02", await TextoAsync(api, $"SELECT CONCAT(STATUS_TREINAMENTO, '|', DATE_FORMAT(DATA_INICIO, '%Y-%m-%d')) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {jaTinha}")); // quem já tinha linha não muda
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {admitidoOntem}"));
        var evento = Assert.Single(await EventosAsync(api, t)); // um evento por treinamento e dia: a segunda rodada não acha ninguém e não grava outro
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.Contains("2 pessoas admitidas", evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain("ZAdm1 Ana", evento.Descricao, StringComparison.Ordinal); // só a contagem, nunca nomes

        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor} WHERE ID_FUNCIONARIO = {mudouDeSetor}"); // entrou num setor do público
        await ExecutarAsync(api);

        Assert.Equal(1L, await LinhasAsync(api, t, mudouDeSetor));
        Assert.Equal(2, (await EventosAsync(api, t)).Count);
    }

    [Fact]
    public async Task Opcional_encerrado_rascunho_e_sem_a_caixa_nao_recebem_ninguem()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "ZAdm2 Ana", null);
        var opcional = await SemearAsync(api, Titulo("ZAdm2 opcional"), "EM_ANDAMENTO", obrigatorio: false, admitidos: true);
        var encerrado = await SemearAsync(api, Titulo("ZAdm2 encerrado"), "ENCERRADO", obrigatorio: true, admitidos: true);
        var rascunho = await SemearAsync(api, Titulo("ZAdm2 rascunho"), obrigatorio: true, admitidos: true);
        var semCaixa = await SemearAsync(api, Titulo("ZAdm2 sem caixa"), "EM_ANDAMENTO", obrigatorio: true, admitidos: false);
        var agendado = await SemearAsync(api, Titulo("ZAdm2 agendado"), "AGENDADO", obrigatorio: true, admitidos: true);

        await ExecutarAsync(api);

        Assert.Equal(0L, await LinhasAsync(api, opcional, pessoa) + await LinhasAsync(api, encerrado, pessoa) + await LinhasAsync(api, rascunho, pessoa) + await LinhasAsync(api, semCaixa, pessoa));
        Assert.Equal(1L, await LinhasAsync(api, agendado, pessoa)); // o agendado entra: a pessoa o verá quando comecaEm chegar
        Assert.Empty(await EventosAsync(api, opcional));
        Assert.Empty(await EventosAsync(api, semCaixa));
    }

    [Fact]
    public async Task Ligar_a_caixa_depois_de_publicado_inclui_todo_o_atraso_de_uma_vez()
    {
        var api = await ApiAsync();
        var t = await SemearAsync(api, Titulo("ZAdm3"), "EM_ANDAMENTO", obrigatorio: true, publico: "TODOS", admitidos: false);
        var pessoas = new List<int>();
        for (var i = 1; i <= 5; i++)
        {
            pessoas.Add(await PessoaAsync(api, $"ZAdm3 pessoa {i}", null, admissao: new DateOnly(2026, 9, i)));
        }

        await ExecutarAsync(api);
        var antes = await LinhasAsync(api, t, [.. pessoas]);
        var rh = await api.ClienteDoRhAgoraAsync();
        var resposta = await rh.PutAsJsonAsync($"/api/rh/treinamentos/{t}", new { titulo = await TextoAsync(api, $"SELECT TITULO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {t}"), comecaEm = "2026-10-01", obrigatorio = true, publico = "TODOS", incluiAdmitidos = true });
        await ExecutarAsync(api);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(0L, antes);
        Assert.Equal(5L, await LinhasAsync(api, t, [.. pessoas]));
    }

    [Fact]
    public async Task A_recuperacao_agenda_a_tarefa_do_dia_uma_vez_so()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, $"DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}'");

        await using (var escopo = api.Api.Services.CreateAsyncScope())
        {
            var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa);
            await tarefa.RecuperarAsync(CancellationToken.None);
            await tarefa.RecuperarAsync(CancellationToken.None);
        }

        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND CHAVE_IDEMPOTENCIA = '{Tarefa}:dia=2026-10-07'"));
    }
}
