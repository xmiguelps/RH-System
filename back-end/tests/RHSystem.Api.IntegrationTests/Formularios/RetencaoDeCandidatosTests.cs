using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.CandidaturasDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// A tarefa <c>APAGAR_CANDIDATOS_VENCIDOS</c> (a retenção dos dados de candidato, LGPD), no banco REAL com o relógio em 2026-10-07T12:42Z. O candidato com MAIS de <c>Formularios:RetencaoDeCandidatosDias</c> (180) dias some com as respostas dele, o currículo e os arquivos de resposta; o de 180 dias exatos fica; rodar
/// de novo é inofensivo; um evento do Sistema por formulário, só com o título e a quantidade (nunca nome, e-mail ou telefone); resposta de funcionário e candidato de outro formulário não são tocados; a recuperação agenda uma tarefa por dia.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class RetencaoDeCandidatosTests(MySqlFixture mysql)
{
    private const string Tarefa = "APAGAR_CANDIDATOS_VENCIDOS";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(RetencaoDeCandidatosTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        return api;
    }

    private static async Task RodarAsync(ApiDeAcesso api, WebApplicationFactory<Program>? fabrica = null)
    {
        await using var escopo = (fabrica ?? api.Api).Services.CreateAsyncScope();
        var dia = DateOnly.FromDateTime(api.Api.Relogio.AgoraUtc.AddHours(-3));
        await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa).ExecutarAsync(new TarefaParaExecutar(0, $"{Tarefa}:dia={dia:yyyy-MM-dd}", null, dia, null, 0), CancellationToken.None);
    }

    private static Task<long> CandidatosAsync(ApiDeAcesso api, int formulario) => ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {formulario}");

    private static Task<long> RespostasDeCandidatosAsync(ApiDeAcesso api, int formulario) => ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {formulario} AND ID_FORMULARIO_CANDIDATO IS NOT NULL");

    private sealed record Candidatura(int Id, string? Curriculo, string? Arquivo);

    /// <summary>Um candidato com currículo e arquivo de resposta, cadastrado no instante dado (o relógio volta ao que estava).</summary>
    private static async Task<Candidatura> CandidatarEmAsync(ApiDeAcesso api, Entrevista e, string nome, string email, DateTime quando)
    {
        var antes = api.Api.Relogio.AgoraUtc;
        api.Api.Relogio.Fixar(quando);
        var id = await CandidatarAsync(api, e, nome, email, curriculo: ("cv.pdf", ArquivosDeTeste.Pdf()), comArquivo: true, arquivos: ("anexo.pdf", ArquivosDeTeste.Pdf()));
        api.Api.Relogio.Fixar(antes);
        return new Candidatura(
            id,
            await TextoAsync(api, $"SELECT CAMINHO_CURRICULO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO_CANDIDATO = {id}"),
            await TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_CANDIDATO = {id} AND CAMINHO_ARQUIVO IS NOT NULL"));
    }

    [Fact]
    public async Task O_candidato_com_mais_de_180_dias_some_com_as_respostas_o_curriculo_e_os_arquivos_e_o_de_179_fica()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet1");
        var velho = await CandidatarEmAsync(api, e, "ZRet1 Velho Prado", "velho@email.com.br", ElencoDeTeste.Hoje);
        var novo = await CandidatarEmAsync(api, e, "ZRet1 Novo Prado", "novo@email.com.br", ElencoDeTeste.Hoje.AddDays(2));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(181));

        await RodarAsync(api);

        Assert.Equal((1L, (long)novo.Id), (await CandidatosAsync(api, e.Id), await ContarAsync(api, $"SELECT ID_FORMULARIO_CANDIDATO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}")));
        Assert.Equal(3L, await RespostasDeCandidatosAsync(api, e.Id)); // só as do que ficou (3 perguntas respondidas)
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_CANDIDATO = {velho.Id}"));
        Assert.All(new[] { velho.Curriculo!, velho.Arquivo! }, caminho => Assert.False(api.Api.Armazenamento.Existe(caminho)));
        Assert.All(new[] { novo.Curriculo!, novo.Arquivo! }, caminho => Assert.True(api.Api.Armazenamento.Existe(caminho)));
        Assert.Contains(velho.Curriculo!, api.Api.Armazenamento.Apagados);
        Assert.Contains(velho.Arquivo!, api.Api.Armazenamento.Apagados);
    }

    [Fact]
    public async Task O_prazo_e_de_mais_de_180_dias_exatos_180_ainda_fica_e_um_segundo_depois_some()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet2");
        await CandidatarEmAsync(api, e, "ZRet2 Limite", "limite@email.com.br", ElencoDeTeste.Hoje);

        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(180));
        await RodarAsync(api);
        var noLimite = await CandidatosAsync(api, e.Id);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(180).AddSeconds(1));
        await RodarAsync(api);

        Assert.Equal((1L, 0L), (noLimite, await CandidatosAsync(api, e.Id)));
    }

    [Fact]
    public async Task Rodar_de_novo_e_inofensivo_e_o_que_nao_e_candidato_nao_e_tocado()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet3");
        var outro = await EntrevistaAsync(api, "ZRet3 outro");
        var velho = await CandidatarEmAsync(api, e, "ZRet3 Velho", "velho@email.com.br", ElencoDeTeste.Hoje);
        var doOutro = await CandidatarEmAsync(api, outro, "ZRet3 Outro Velho", "outro@email.com.br", ElencoDeTeste.Hoje);
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZRet3 Funcionaria"), Perfil.Funcionario);
        var dePessoas = await SemearAsync(api, Titulo("ZRet3 pessoas"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, dePessoas, "Como foi?");
        await EscolherPessoasAsync(api, dePessoas, [pessoa]);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO, ID_FORMULARIO_PERGUNTA, ID_FUNCIONARIO, RESPOSTA, ANONIMO, DATA_RESPOSTA) VALUES ({dePessoas}, {pergunta}, {pessoa}, 'Bem', FALSE, '2020-01-01 10:00:00')");
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(400));

        await RodarAsync(api);
        var depoisDaPrimeira = (await CandidatosAsync(api, e.Id), await CandidatosAsync(api, outro.Id), (await EventosAsync(api, e.Id)).Count);
        await RodarAsync(api);

        Assert.Equal((0L, 0L, 1), depoisDaPrimeira); // os dois formulários limpos, um evento no primeiro
        Assert.Single(await EventosAsync(api, e.Id)); // a segunda rodada não achou nada: nenhum evento novo
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {dePessoas}")); // a resposta do funcionário (de 2020) não é de candidato
        Assert.False(api.Api.Armazenamento.Existe(velho.Curriculo!) || api.Api.Armazenamento.Existe(doOutro.Arquivo!));
    }

    [Fact]
    public async Task Cada_formulario_ganha_um_evento_do_Sistema_so_com_o_titulo_e_a_quantidade_sem_dado_pessoal_e_nada_apagado_nao_gera_evento()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet4");
        var semNada = await EntrevistaAsync(api, "ZRet4 vazio");
        await CandidatarEmAsync(api, e, "ZRet4 Zuleica Quintanilha", "zuleica.quintanilha@email.com.br", ElencoDeTeste.Hoje);
        await CandidatarEmAsync(api, e, "ZRet4 Caio Prado", "caio.prado@email.com.br", ElencoDeTeste.Hoje.AddHours(1));
        await CandidatarEmAsync(api, e, "ZRet4 Ana Souza", "ana.souza@email.com.br", ElencoDeTeste.Hoje.AddDays(100));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(190));

        await RodarAsync(api);

        var evento = Assert.Single(await EventosAsync(api, e.Id));
        Assert.Equal((TipoDeAtor.Sistema, AuditoriaAcao.Excluiu, $"Apagou 2 candidaturas vencidas do formulário \"{await TextoAsync(api, $"SELECT TITULO FROM FORMULARIOS WHERE ID_FORMULARIO = {e.Id}")}\" (retenção de 180 dias)."), (evento.AtorTipo, evento.Acao, evento.Descricao));
        Assert.All(new[] { "Zuleica", "Quintanilha", "Caio", "email.com.br", "formularios/" }, parte => Assert.DoesNotContain(parte, evento.Descricao, StringComparison.OrdinalIgnoreCase));
        Assert.Empty(await EventosAsync(api, semNada.Id));
        Assert.Equal(1L, await CandidatosAsync(api, e.Id)); // o de 90 dias ficou
    }

    [Fact]
    public async Task Uma_execucao_apaga_todos_os_vencidos_de_uma_vez_com_um_evento_so_por_formulario()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet5");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_CANDIDATOS (ID_FORMULARIO, NOME, EMAIL, DATA_CADASTRO) WITH RECURSIVE n AS (SELECT 1 AS i UNION ALL SELECT i + 1 FROM n WHERE i < 250) SELECT {e.Id}, CONCAT('ZRet5 ', i), CONCAT('zret5-', i, '@email.com.br'), '2025-01-01 10:00:00' FROM n");
        await CandidatarEmAsync(api, e, "ZRet5 Recente", "recente@email.com.br", ElencoDeTeste.Hoje.AddDays(-5));
        Assert.Equal(251L, await CandidatosAsync(api, e.Id));

        await RodarAsync(api);

        Assert.Equal(1L, await CandidatosAsync(api, e.Id));
        Assert.StartsWith("Apagou 250 candidaturas vencidas", Assert.Single(await EventosAsync(api, e.Id)).Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Se_o_armazenamento_falha_ao_apagar_o_banco_ja_foi_limpo_a_tarefa_termina_e_a_limpeza_de_orfaos_cuida_do_arquivo()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet6");
        var velho = await CandidatarEmAsync(api, e, "ZRet6 Velho", "velho@email.com.br", ElencoDeTeste.Hoje);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(200));
        api.Api.Armazenamento.FalharNaExclusao = new IOException("armazenamento fora do ar");
        try
        {
            await RodarAsync(api);
        }
        finally
        {
            api.Api.Armazenamento.FalharNaExclusao = null;
        }

        Assert.Equal(0L, await CandidatosAsync(api, e.Id));
        Assert.Equal(0L, await RespostasDeCandidatosAsync(api, e.Id));
        Assert.All(new[] { velho.Curriculo!, velho.Arquivo! }, caminho => Assert.True(api.Api.Armazenamento.Existe(caminho))); // o arquivo ficou sem linha: é órfão, e a tarefa diária de órfãos o apaga
        Assert.Equal(2, api.Api.Armazenamento.ExclusoesPedidas.Count(c => c == velho.Curriculo || c == velho.Arquivo)); // a limpeza foi TENTADA nos dois
    }

    [Fact]
    public async Task O_prazo_vem_da_configuracao_e_30_dias_apaga_o_de_31()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet7");
        await CandidatarEmAsync(api, e, "ZRet7 Candidato", "candidato@email.com.br", ElencoDeTeste.Hoje);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(31));
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.UseSetting("Formularios:RetencaoDeCandidatosDias", "30"));

        await RodarAsync(api, fabrica);

        Assert.Equal(0L, await CandidatosAsync(api, e.Id));
    }

    [Fact]
    public async Task A_recuperacao_agenda_uma_tarefa_por_dia_e_repetir_nao_duplica()
    {
        var api = await ApiAsync();
        await Sql.ExecutarAsync(api.Conexao, $"DELETE FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}'");
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tarefa);

        await tarefa.RecuperarAsync(CancellationToken.None);
        await tarefa.RecuperarAsync(CancellationToken.None);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(1));
        await tarefa.RecuperarAsync(CancellationToken.None);

        Assert.Equal(2L, await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}' AND SITUACAO = 'PENDENTE'"));
        Assert.Equal($"{Tarefa}:dia=2026-10-07", await TextoAsync(api, $"SELECT MIN(CHAVE_IDEMPOTENCIA) FROM TAREFAS_AGENDADAS WHERE TIPO = '{Tarefa}'"));
    }

    [Fact]
    public async Task Na_mesma_rodada_o_de_180_dias_exatos_fica_inteiro_com_respostas_e_arquivos_e_so_o_mais_velho_some()
    {
        var api = await ApiAsync();
        var e = await EntrevistaAsync(api, "ZRet8");
        var velho = await CandidatarEmAsync(api, e, "ZRet8 Velho", "velho@email.com.br", ElencoDeTeste.Hoje.AddDays(-10));
        var noLimite = await CandidatarEmAsync(api, e, "ZRet8 No Limite", "limite@email.com.br", ElencoDeTeste.Hoje);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddDays(180));

        await RodarAsync(api);

        Assert.Equal((1L, (long)noLimite.Id), (await CandidatosAsync(api, e.Id), await ContarAsync(api, $"SELECT ID_FORMULARIO_CANDIDATO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id}")));
        Assert.Equal(3L, await RespostasDeCandidatosAsync(api, e.Id)); // as respostas do que ficou
        Assert.All(new[] { noLimite.Curriculo!, noLimite.Arquivo! }, caminho => Assert.True(api.Api.Armazenamento.Existe(caminho)));
        Assert.All(new[] { velho.Curriculo!, velho.Arquivo! }, caminho => Assert.False(api.Api.Armazenamento.Existe(caminho)));
    }
}
