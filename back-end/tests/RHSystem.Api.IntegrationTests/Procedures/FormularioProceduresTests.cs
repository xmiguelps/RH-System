using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ApoioDaParteD;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary><c>SP_ENCERRAR_FORMULARIO</c> do <c>V007</c> (parte D), num MySQL 8.4 de verdade: só o formulário publicado é encerrado, com a data de agora e sem a próxima reabertura.</summary>
[Collection(MySqlCollection.Nome)]
public class FormularioProceduresTests(MySqlFixture mysql)
{
    private const string Encerrar = "SP_ENCERRAR_FORMULARIO";
    private const string SoPublicado = "So um formulario publicado pode ser encerrado.";

    /// <summary>Um formulário com as colunas que a situação usa; o resto fica no padrão do banco. <paramref name="colunas"/> é a lista <c>COLUNA = valor</c> aplicada depois de criar.</summary>
    private static async Task<int> FormularioAsync(string conexao, string colunas)
    {
        var titulo = $"Formulario {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FORMULARIOS (TITULO, TIPO_FORMULARIO, ATIVO) VALUES ('{titulo}', 'PESQUISA', FALSE)");
        var id = await IdAsync(conexao, $"SELECT ID_FORMULARIO FROM FORMULARIOS WHERE TITULO = '{titulo}'");
        await Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET {colunas} WHERE ID_FORMULARIO = {id}");
        return id;
    }

    private static Task<int> PublicadoAsync(string conexao, string mais = "DATA_PRAZO = NULL") =>
        FormularioAsync(conexao, $"ATIVO = TRUE, DATA_PUBLICACAO = NOW() - INTERVAL 1 DAY, {mais}");

    private static Task<string?> LinhaAsync(string conexao, int id) =>
        TextoAsync(conexao, $"SELECT CONCAT_WS('|', ATIVO, COALESCE(DATE_FORMAT(DATA_ENCERRAMENTO, '%Y-%m-%d'), 'sem encerramento'), COALESCE(DATE_FORMAT(PROXIMA_REABERTURA, '%Y-%m-%d'), 'sem reabertura'), COALESCE(RECORRENCIA_INTERVALO, 'sem intervalo'), COALESCE(RECORRENCIA_UNIDADE, 'sem unidade')) FROM FORMULARIOS WHERE ID_FORMULARIO = {id}");

    [Fact]
    public async Task Encerrar_o_formulario_publicado_desliga_o_ativo_grava_a_data_de_agora_e_cancela_a_proxima_reabertura()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = await DiaAsync(conexao);
        var id = await PublicadoAsync(conexao, "DATA_PRAZO = CURDATE() + INTERVAL 5 DAY, RECORRENCIA_INTERVALO = 1, RECORRENCIA_UNIDADE = 'MES', PROXIMA_REABERTURA = NOW() + INTERVAL 20 DAY");

        await ChamarAsync(conexao, Encerrar, id);

        Assert.Equal($"0|{Iso(hoje)}|sem reabertura|1|MES", await LinhaAsync(conexao, id)); // a recorrência continua descrita, mas não reabre mais (a antiga deixava a reabertura marcada)
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO = {id} AND DATA_ENCERRAMENTO BETWEEN NOW() - INTERVAL 2 MINUTE AND NOW() AND DATA_PRAZO = CURDATE() + INTERVAL 5 DAY"));
    }

    [Fact]
    public async Task Depois_de_encerrado_o_banco_nao_aceita_mais_resposta()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var id = await PublicadoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FORMULARIOS_PERGUNTAS (ID_FORMULARIO, TEXTO_PERGUNTA, TIPO_RESPOSTA, ORDEM) VALUES ({id}, 'Como foi a semana?', 'TEXTO', 1)");
        var pergunta = await IdAsync(conexao, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {id}");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA) VALUES ({pergunta}, {id}, {pessoa}, 'Boa.')"); // publicado: aceita

        await ChamarAsync(conexao, Encerrar, id);

        var outra = await FuncionarioAsync(conexao);
        var erro = await Assert.ThrowsAsync<MySqlConnector.MySqlException>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA) VALUES ({pergunta}, {id}, {outra}, 'Tarde demais.')"));
        Assert.Equal("Formulario inativo nao aceita respostas.", erro.Message); // TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}")); // encerrar não apaga o que já foi respondido
    }

    [Fact]
    public async Task O_formulario_com_o_prazo_vencendo_hoje_ainda_esta_publicado_e_pode_ser_encerrado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var id = await PublicadoAsync(conexao, "DATA_PRAZO = CURDATE()");

        await ChamarAsync(conexao, Encerrar, id);

        Assert.StartsWith("0|", await LinhaAsync(conexao, id), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Rascunho_agendado_e_o_de_prazo_vencido_nao_estao_publicados_e_sao_recusados_sem_mudar()
    {
        var conexao = await NovoBancoAsync(mysql);
        var rascunho = await FormularioAsync(conexao, "ATIVO = FALSE");
        var agendado = await FormularioAsync(conexao, "ATIVO = FALSE, DATA_PUBLICACAO = NOW() + INTERVAL 3 DAY");
        var vencido = await PublicadoAsync(conexao, "DATA_PRAZO = CURDATE() - INTERVAL 1 DAY, RECORRENCIA_INTERVALO = 2, RECORRENCIA_UNIDADE = 'SEMANA', PROXIMA_REABERTURA = NOW() + INTERVAL 9 DAY"); // a tarefa da API ainda não gravou o encerramento, mas a API já o mostra encerrado

        Assert.Equal("Informe o formulario.", await RecusaDeNegocioAsync(conexao, Encerrar, [null]));
        Assert.Equal("Formulario nao encontrado.", await RecusaDeNegocioAsync(conexao, Encerrar, 999_999));
        Assert.Equal(SoPublicado, await RecusaDeNegocioAsync(conexao, Encerrar, rascunho));
        Assert.Equal(SoPublicado, await RecusaDeNegocioAsync(conexao, Encerrar, agendado));
        Assert.Equal(SoPublicado, await RecusaDeNegocioAsync(conexao, Encerrar, vencido)); // a antiga encerrava: bastava ATIVO ligado

        Assert.Equal("0|sem encerramento|sem reabertura|sem intervalo|sem unidade", await LinhaAsync(conexao, rascunho));
        Assert.Equal("0|sem encerramento|sem reabertura|sem intervalo|sem unidade", await LinhaAsync(conexao, agendado));
        Assert.Equal($"1|sem encerramento|{Iso(await DiaAsync(conexao, 9))}|2|SEMANA", await LinhaAsync(conexao, vencido));
    }

    [Fact]
    public async Task Encerrar_duas_vezes_e_recusado_e_a_primeira_data_fica()
    {
        var conexao = await NovoBancoAsync(mysql);
        var id = await PublicadoAsync(conexao);
        await ChamarAsync(conexao, Encerrar, id);
        await Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET DATA_ENCERRAMENTO = '2026-01-02 03:04:05' WHERE ID_FORMULARIO = {id}");

        Assert.Equal(SoPublicado, await RecusaDeNegocioAsync(conexao, Encerrar, id));

        Assert.Equal("2026-01-02 03:04:05", await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_ENCERRAMENTO, '%Y-%m-%d %H:%i:%s') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Dois_encerramentos_ao_mesmo_tempo_um_vale_e_o_outro_e_recusado()
    {
        var conexao = await NovoBancoAsync(mysql);
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var id = await PublicadoAsync(conexao);

            var desfechos = await AoMesmoTempoAsync(() => TentarAsync(conexao, Encerrar, id), () => TentarAsync(conexao, Encerrar, id));

            Assert.Single(desfechos, d => d.Passou);
            var recusa = Assert.Single(desfechos, d => !d.Passou);
            Assert.Equal(SoPublicado, recusa.Mensagem);
            Assert.Equal("45000", recusa.SqlState);
            Assert.StartsWith("0|", await LinhaAsync(conexao, id), StringComparison.Ordinal);
        }
    }
}
