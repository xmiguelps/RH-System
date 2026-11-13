using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta da adesão a benefício com as colunas do pedido, dos dependentes e dos trajetos do vale-transporte no banco REAL, relendo com OUTRO contexto: o pedido nasce <c>PENDENTE</c> e INATIVO (o padrão <c>BENEFICIO_ATIVO = TRUE</c> do banco não vence), as datas ficam em -03:00 com o relógio da aplicação
/// (o <c>DEFAULT CURRENT_TIMESTAMP</c> dos filhos não vale), a concessão direta da fatia 1 continua sem as colunas do pedido, e os <c>CHECK</c> do banco recusam as combinações que a aplicação nunca grava.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDosBeneficiosTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddMilliseconds(600);

    private static async Task<(int PessoaId, int BeneficioId)> PessoaEBeneficioAsync(string conexao, string rotulo)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa {rotulo}", $"{rotulo}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS (NOME_BENEFICIO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO) VALUES ('Beneficio {rotulo}', TRUE, TRUE, TRUE)");
        var beneficio = Convert.ToInt32(await Sql.EscalarAsync(conexao, $"SELECT ID_BENEFICIO FROM BENEFICIOS WHERE NOME_BENEFICIO = 'Beneficio {rotulo}'"), CultureInfo.InvariantCulture);
        return (pessoa.Id, beneficio);
    }

    private static async Task<string?> TextoAsync(string conexao, string sql) => (await Sql.EscalarAsync(conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    private static async Task<MySqlException> RecusaDoSqlAsync(string conexao, string sql) => await Assert.ThrowsAsync<MySqlException>(() => Sql.ExecutarAsync(conexao, sql));

    [Fact]
    public async Task O_pedido_de_adesao_nasce_PENDENTE_e_inativo_com_as_datas_em_menos_3_horas_e_le_igual()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (pessoa, beneficio) = await PessoaEBeneficioAsync(conexao, "adesao");
        int id;
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var adesao = BeneficioDoFuncionario.Pedir(pessoa, beneficio, new DateOnly(2026, 11, 1), true, Agora);
            db.BeneficiosDosFuncionarios.Add(adesao);
            await db.SaveChangesAsync();
            id = adesao.Id;
        }

        // o padrão do banco é BENEFICIO_ATIVO = TRUE: o "falso" só chega porque a entidade o envia
        Assert.Equal(("0", "PENDENTE", "2026-10-07 09:42:00", "2026-11-01", "1", (string?)null, (string?)null, (string?)null), (
            await TextoAsync(conexao, $"SELECT BENEFICIO_ATIVO + 0 FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_INICIO_DESEJADA, '%Y-%m-%d') FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT AUTORIZA_DESCONTO_FOLHA + 0 FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT ID_FUNCIONARIO_RH FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT DATA_RESPOSTA FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}"),
            await TextoAsync(conexao, $"SELECT MOTIVO_RECUSA FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}")));
        await using var leitura = ContextoDeTeste.Novo(conexao);
        var relida = await leitura.BeneficiosDosFuncionarios.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal((StatusDoPedidoDeBeneficio.Pendente, false, new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), new DateOnly(2026, 11, 1), true), (relida.Status, relida.Ativo, relida.PedidoEmUtc, relida.DataInicioDesejada, relida.AutorizaDescontoNaFolha));
        Assert.Null(relida.FuncionarioRhId);
        Assert.Null(relida.RespondidoEmUtc);
        Assert.Null(relida.MotivoDaRecusa);
    }

    [Fact]
    public async Task A_concessao_direta_da_fatia_1_continua_aprovada_e_ativa_sem_as_colunas_do_pedido()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (pessoa, beneficio) = await PessoaEBeneficioAsync(conexao, "direta");
        int id;
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var direta = BeneficioDoFuncionario.Conceder(pessoa, beneficio);
            db.BeneficiosDosFuncionarios.Add(direta);
            await db.SaveChangesAsync();
            id = direta.Id;
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var relida = await leitura.BeneficiosDosFuncionarios.AsNoTracking().SingleAsync(b => b.Id == id);
        Assert.Equal((StatusDoPedidoDeBeneficio.Aprovado, true, false), (relida.Status, relida.Ativo, relida.AutorizaDescontoNaFolha));
        Assert.Null(relida.PedidoEmUtc);
        Assert.Null(relida.DataInicioDesejada);
    }

    [Fact]
    public async Task O_dependente_e_o_trajeto_ida_e_volta_com_a_data_do_pedido_da_aplicacao_e_status_PENDENTE()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (pessoa, beneficio) = await PessoaEBeneficioAsync(conexao, "filhos");
        int adesaoId;
        int dependenteId;
        int trajetoId;
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var adesao = BeneficioDoFuncionario.Pedir(pessoa, beneficio, null, false, Agora);
            db.BeneficiosDosFuncionarios.Add(adesao);
            await db.SaveChangesAsync();
            adesaoId = adesao.Id;
            var dependente = Dependente.Pedir(adesaoId, "Lucas Lima", "Filho", new DateOnly(2018, 5, 4), Agora);
            var trajeto = TrajetoDeValeTransporte.Pedir(adesaoId, "Rua das Flores, 100", "Av. Paulista, 1000", "Linha 875A, Metrô Linha 2", Agora);
            db.Dependentes.Add(dependente);
            db.TrajetosDeValeTransporte.Add(trajeto);
            await db.SaveChangesAsync();
            dependenteId = dependente.Id;
            trajetoId = trajeto.Id;
        }

        Assert.Equal(("PENDENTE", "2026-10-07 09:42:00", "PENDENTE", "2026-10-07 09:42:00"), (
            await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM DEPENDENTES WHERE ID_DEPENDENTE = {dependenteId}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM DEPENDENTES WHERE ID_DEPENDENTE = {dependenteId}"),
            await TextoAsync(conexao, $"SELECT STATUS_PEDIDO FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_TRAJETO = {trajetoId}"),
            await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_TRAJETO = {trajetoId}")));
        await using var leitura = ContextoDeTeste.Novo(conexao);
        var d = await leitura.Dependentes.AsNoTracking().SingleAsync(x => x.Id == dependenteId);
        var t = await leitura.TrajetosDeValeTransporte.AsNoTracking().SingleAsync(x => x.Id == trajetoId);
        Assert.Equal((adesaoId, "Lucas Lima", "Filho", new DateOnly(2018, 5, 4), StatusDoPedidoDeBeneficio.Pendente, new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), (d.AdesaoId, d.Nome, d.Parentesco, d.DataDeNascimento, d.Status, d.PedidoEmUtc));
        Assert.Equal((adesaoId, "Rua das Flores, 100", "Av. Paulista, 1000", "Linha 875A, Metrô Linha 2", StatusDoPedidoDeBeneficio.Pendente), (t.AdesaoId, t.Origem, t.Destino, t.LinhasEMeios, t.Status));
        Assert.Null(d.FuncionarioRhId);
        Assert.Null(t.MotivoDaRecusa);
    }

    [Fact]
    public async Task Os_CHECK_do_banco_recusam_resposta_pela_metade_recusa_sem_motivo_e_pendente_sem_data_do_pedido()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (pessoa, beneficio) = await PessoaEBeneficioAsync(conexao, "checks");
        int adesaoId;
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            var adesao = BeneficioDoFuncionario.Pedir(pessoa, beneficio, null, false, Agora);
            db.BeneficiosDosFuncionarios.Add(adesao);
            await db.SaveChangesAsync();
            adesaoId = adesao.Id;
            db.Dependentes.Add(Dependente.Pedir(adesaoId, "Dependente", "Filho", new DateOnly(2018, 5, 4), Agora));
            await db.SaveChangesAsync();
        }

        var dependenteId = Convert.ToInt32(await Sql.EscalarAsync(conexao, $"SELECT ID_DEPENDENTE FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}"), CultureInfo.InvariantCulture);

        var respostaPelaMetade = await RecusaDoSqlAsync(conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET ID_FUNCIONARIO_RH = {pessoa} WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");
        var recusaSemMotivo = await RecusaDoSqlAsync(conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET STATUS_PEDIDO = 'RECUSADO', ID_FUNCIONARIO_RH = {pessoa}, DATA_RESPOSTA = '2026-10-07 09:42:00', MOTIVO_RECUSA = '   ' WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");
        var dependenteSemMotivo = await RecusaDoSqlAsync(conexao, $"UPDATE DEPENDENTES SET STATUS_PEDIDO = 'RECUSADO', MOTIVO_RECUSA = ' ' WHERE ID_DEPENDENTE = {dependenteId}");
        var pendenteSemData = await RecusaDoSqlAsync(conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET DATA_PEDIDO = NULL WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");

        Assert.Contains("CHK_BENEFICIOS_FUNCIONARIOS_RESPOSTA_JUNTAS", respostaPelaMetade.Message, StringComparison.Ordinal);
        Assert.Contains("CHK_BENEFICIOS_FUNCIONARIOS_RECUSA_COMPLETA", recusaSemMotivo.Message, StringComparison.Ordinal);
        Assert.Contains("CHK_DEPENDENTES_RECUSA_COM_MOTIVO", dependenteSemMotivo.Message, StringComparison.Ordinal);
        Assert.Contains("CHK_BENEFICIOS_FUNCIONARIOS_PENDENTE_COM_DATA", pendenteSemData.Message, StringComparison.Ordinal);
    }
}
