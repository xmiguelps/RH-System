using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta de <c>FERIAS</c> no banco REAL (relendo com OUTRO contexto) e a sonda dos triggers da fatia 7: o registro direto do RH grava <c>APROVADA</c> (o padrão <c>SOLICITADA</c> do banco não vence), as datas ficam em -03:00 com o relógio da aplicação (o <c>CHECK</c> de análise não recusa a decisão),
/// as cinco recusas de trigger saem com o texto exato, as transições aceitas e recusadas, e o cancelamento pela inativação (com o sufixo na <c>OBSERVACAO</c>; férias em gozo e as que começam hoje ficam).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDasFeriasTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddMilliseconds(600);

    private static readonly DateOnly InicioDoPeriodo = new(2026, 1, 2);
    private static readonly DateOnly FimDoPeriodo = new(2026, 2, 1);

    private static async Task<Funcionario> NovaPessoaAsync(string conexao, string email, bool ativa = true)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa {email}", email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        if (!ativa)
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO = {pessoa.Id}");
        }

        return pessoa;
    }

    private static async Task<int> GestorAsync(string conexao, int funcionarioId)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionarioId})");
        return Convert.ToInt32(await Sql.EscalarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionarioId}"), CultureInfo.InvariantCulture);
    }

    private static async Task<string?> TextoAsync(string conexao, string sql) => (await Sql.EscalarAsync(conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    private static async Task<int> SalvarAsync(string conexao, PedidoDeFerias pedido)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        db.PedidosDeFerias.Add(pedido);
        await db.SaveChangesAsync();
        return pedido.Id;
    }

    private static PedidoDeFerias Pedido(int pessoaId, DateOnly? inicioDoPeriodo = null, DateOnly? inicio = null, int? gestorId = null, bool ausente = false) =>
        PedidoDeFerias.Solicitar(pessoaId, inicioDoPeriodo ?? InicioDoPeriodo, FimDoPeriodo, inicio ?? new DateOnly(2026, 12, 1), (inicio ?? new DateOnly(2026, 12, 1)).AddDays(9), false, 0, false, "Quero descansar", Agora, gestorId, ausente);

    private static async Task<MySqlException> RecusaDoBancoAsync(string conexao, PedidoDeFerias pedido)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        db.PedidosDeFerias.Add(pedido);
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        return Assert.IsType<MySqlException>(erro.InnerException);
    }

    private static async Task<MySqlException> RecusaDoSqlAsync(string conexao, string sql) => await Assert.ThrowsAsync<MySqlException>(() => Sql.ExecutarAsync(conexao, sql));

    [Fact]
    public async Task O_registro_do_RH_grava_APROVADA_sem_gestor_e_o_pedido_com_gestor_ausente_grava_a_etapa_automatica_e_tudo_le_igual()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var pessoa = await NovaPessoaAsync(conexao, "ferias.ida.volta");
        var dono = await NovaPessoaAsync(conexao, "ferias.ida.volta.gestor");
        var gestorId = await GestorAsync(conexao, dono.Id);

        var registro = PedidoDeFerias.Registrar(pessoa.Id, InicioDoPeriodo, FimDoPeriodo, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14), true, 5, true, "Registrado pelo RH", Agora);
        var registroId = await SalvarAsync(conexao, registro);
        var automatico = await SalvarAsync(conexao, Pedido(pessoa.Id, gestorId: gestorId, ausente: true));

        // o padrão do banco é SOLICITADA: a APROVADA só chega porque a entidade envia o status
        Assert.Equal("APROVADA", await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {registroId}"));
        Assert.Equal(("2026-10-07 09:42:00", null, null, "0"), (
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_SOLICITACAO, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {registroId}"),
            await TextoAsync(conexao, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {registroId}"),
            await TextoAsync(conexao, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {registroId}"),
            await TextoAsync(conexao, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {registroId}")));
        Assert.Equal(("SOLICITADA", "APROVOU", "2026-10-07 09:42:00", "1", gestorId.ToString(CultureInfo.InvariantCulture)), (
            await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {automatico}"),
            await TextoAsync(conexao, $"SELECT RESULTADO_GESTOR FROM FERIAS WHERE ID_FERIAS = {automatico}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_ETAPA_GESTOR, '%Y-%m-%d %H:%i:%s') FROM FERIAS WHERE ID_FERIAS = {automatico}"),
            await TextoAsync(conexao, $"SELECT ETAPA_GESTOR_AUTOMATICA + 0 FROM FERIAS WHERE ID_FERIAS = {automatico}"),
            await TextoAsync(conexao, $"SELECT ID_GESTOR FROM FERIAS WHERE ID_FERIAS = {automatico}")));

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var relido = await leitura.PedidosDeFerias.AsNoTracking().SingleAsync(f => f.Id == registroId);
        Assert.Equal((StatusDasFerias.Aprovada, new DateOnly(2026, 9, 1), new DateOnly(2026, 9, 14), true, 5, true, "Registrado pelo RH"), (relido.Status, relido.DataInicio, relido.DataFim, relido.AbonoPecuniario, relido.DiasAbono, relido.Adiantar13Salario, relido.Observacao));
        Assert.Equal((new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), InicioDoPeriodo, FimDoPeriodo), (relido.SolicitadaEmUtc, relido.InicioDoPeriodoAquisitivo, relido.FimDoPeriodoAquisitivo));
        Assert.Null(relido.GestorId);
        Assert.Null(relido.ResultadoDoGestor);
        var ausente = await leitura.PedidosDeFerias.AsNoTracking().SingleAsync(f => f.Id == automatico);
        Assert.Equal((StatusDasFerias.Solicitada, ResultadoDoGestorNasFerias.Aprovou, true, new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), gestorId), (ausente.Status, ausente.ResultadoDoGestor, ausente.EtapaDoGestorAutomatica, ausente.EtapaDoGestorEmUtc, ausente.GestorId));
    }

    [Fact]
    public async Task A_decisao_gravada_com_o_relogio_da_aplicacao_passa_no_CHECK_de_analise_e_uma_analise_anterior_a_solicitacao_e_recusada()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var pessoa = await NovaPessoaAsync(conexao, "ferias.check.analise");
        var outro = await NovaPessoaAsync(conexao, "ferias.check.analise.gestor");
        var gestorId = await GestorAsync(conexao, outro.Id);
        var id = await SalvarAsync(conexao, Pedido(pessoa.Id, gestorId: gestorId));

        // solicitada em 09:42 (-03:00); analisada um segundo antes é recusada pelo CHECK, a do mesmo instante passa
        var recusa = await RecusaDoSqlAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'REJEITADA', RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = '2026-10-07 09:42:00', MOTIVO_RECUSA = 'Equipe desfalcada', DATA_ANALISE = '2026-10-07 09:41:59' WHERE ID_FERIAS = {id}");
        Assert.Contains("CHK_FERIAS_DATAS_ANALISE", recusa.Message, StringComparison.Ordinal);
        await Sql.ExecutarAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'REJEITADA', RESULTADO_GESTOR = 'RECUSOU', DATA_ETAPA_GESTOR = '2026-10-07 09:42:00', MOTIVO_RECUSA = 'Equipe desfalcada', DATA_ANALISE = '2026-10-07 09:42:00' WHERE ID_FERIAS = {id}");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var relido = await leitura.PedidosDeFerias.AsNoTracking().SingleAsync(f => f.Id == id);
        Assert.Equal((StatusDasFerias.Rejeitada, ResultadoDoGestorNasFerias.Recusou, "Equipe desfalcada", new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), (relido.Status, relido.ResultadoDoGestor, relido.MotivoRecusa, relido.AnalisadaEmUtc));
        Assert.Equal("Quero descansar", relido.Observacao);
    }

    [Fact]
    public async Task O_trigger_de_insercao_recusa_pessoa_inativa_periodo_antes_da_admissao_e_inicio_antes_do_fim_do_periodo_com_o_texto_exato()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var inativa = await NovaPessoaAsync(conexao, "ferias.trigger.inativa", ativa: false);
        var ativa = await NovaPessoaAsync(conexao, "ferias.trigger.ativa");

        var deInativa = await RecusaDoBancoAsync(conexao, Pedido(inativa.Id));
        var antesDaAdmissao = await RecusaDoBancoAsync(conexao, Pedido(ativa.Id, inicioDoPeriodo: new DateOnly(2025, 12, 31)));
        var antesDoFimDoPeriodo = await RecusaDoBancoAsync(conexao, Pedido(ativa.Id, inicio: FimDoPeriodo));

        Assert.Equal("45000", deInativa.SqlState);
        Assert.Equal("Funcionario inativo nao pode solicitar ferias.", deInativa.Message);
        Assert.Equal("Periodo aquisitivo nao pode comecar antes da admissao.", antesDaAdmissao.Message);
        Assert.Equal("Inicio das ferias deve ser posterior ao fim do periodo aquisitivo.", antesDoFimDoPeriodo.Message);
    }

    [Fact]
    public async Task O_trigger_de_atualizacao_so_aceita_as_transicoes_da_tabela_e_exige_gestor_que_nao_seja_a_propria_pessoa_ao_aprovar_ou_rejeitar()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var pessoa = await NovaPessoaAsync(conexao, "ferias.transicoes");
        var outro = await NovaPessoaAsync(conexao, "ferias.transicoes.gestor");
        var gestorDeOutro = await GestorAsync(conexao, outro.Id);
        var gestoraDeSiMesma = await GestorAsync(conexao, pessoa.Id);
        var semGestor = await SalvarAsync(conexao, Pedido(pessoa.Id));
        var comGestorProprio = await SalvarAsync(conexao, Pedido(pessoa.Id, gestorId: gestoraDeSiMesma));
        var normal = await SalvarAsync(conexao, Pedido(pessoa.Id, gestorId: gestorDeOutro));

        var saltoInvalido = await RecusaDoSqlAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'EM_GOZO' WHERE ID_FERIAS = {normal}");
        var semResponsavel = await RecusaDoSqlAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'APROVADA' WHERE ID_FERIAS = {semGestor}");
        var proprias = await RecusaDoSqlAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'APROVADA' WHERE ID_FERIAS = {comGestorProprio}");

        Assert.Equal("Transicao de status de ferias invalida.", saltoInvalido.Message);
        Assert.Equal("Informe o gestor responsavel pela analise das ferias.", semResponsavel.Message);
        Assert.Equal("O gestor nao pode analisar as proprias ferias.", proprias.Message);

        // as transições aceitas: SOLICITADA -> APROVADA -> EM_GOZO -> CONCLUIDA (e CONCLUIDA não volta)
        foreach (var (para, esperado) in new[] { ("APROVADA", "APROVADA"), ("EM_GOZO", "EM_GOZO"), ("CONCLUIDA", "CONCLUIDA") })
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = '{para}', DATA_ANALISE = COALESCE(DATA_ANALISE, '2026-10-07 09:42:00') WHERE ID_FERIAS = {normal}");
            Assert.Equal(esperado, await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {normal}"));
        }

        var volta = await RecusaDoSqlAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'EM_GOZO' WHERE ID_FERIAS = {normal}");
        Assert.Equal("Transicao de status de ferias invalida.", volta.Message);
    }

    [Fact]
    public async Task Inativar_a_pessoa_cancela_as_futuras_SOLICITADA_e_APROVADA_com_o_sufixo_na_observacao_e_nao_toca_em_EM_GOZO_nem_nas_que_comecam_hoje()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var pessoa = await NovaPessoaAsync(conexao, "ferias.inativacao");

        async Task<int> InserirAsync(string status, string inicio, string fim, string? observacao)
        {
            await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS, OBSERVACAO) VALUES ({pessoa.Id}, '2026-01-02', '2026-02-01', {inicio}, {fim}, '{status}', {(observacao is null ? "NULL" : $"'{observacao}'")})");
            return Convert.ToInt32(await Sql.EscalarAsync(conexao, $"SELECT MAX(ID_FERIAS) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa.Id}"), CultureInfo.InvariantCulture);
        }

        // as datas saem do CURDATE() do banco porque o trigger de inativação compara com ele (o relógio real, não o da aplicação)
        var solicitada = await InserirAsync("SOLICITADA", "DATE_ADD(CURDATE(), INTERVAL 60 DAY)", "DATE_ADD(CURDATE(), INTERVAL 69 DAY)", "Quero descansar");
        var aprovada = await InserirAsync("APROVADA", "DATE_ADD(CURDATE(), INTERVAL 80 DAY)", "DATE_ADD(CURDATE(), INTERVAL 89 DAY)", null);
        var emGozo = await InserirAsync("EM_GOZO", "DATE_SUB(CURDATE(), INTERVAL 2 DAY)", "DATE_ADD(CURDATE(), INTERVAL 7 DAY)", null);
        var comecaHoje = await InserirAsync("APROVADA", "CURDATE()", "DATE_ADD(CURDATE(), INTERVAL 9 DAY)", null);

        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO = {pessoa.Id}");

        Assert.Equal(("CANCELADA", "Quero descansar | Cancelada automaticamente: funcionario inativado"), (
            await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {solicitada}"),
            await TextoAsync(conexao, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {solicitada}")));
        Assert.Equal(("CANCELADA", "Cancelada automaticamente: funcionario inativado"), (
            await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {aprovada}"),
            await TextoAsync(conexao, $"SELECT OBSERVACAO FROM FERIAS WHERE ID_FERIAS = {aprovada}")));
        Assert.Equal("EM_GOZO", await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {emGozo}"));
        Assert.Equal("APROVADA", await TextoAsync(conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FERIAS = {comecaHoje}"));
    }
}
