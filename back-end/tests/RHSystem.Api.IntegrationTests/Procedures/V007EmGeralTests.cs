using Microsoft.Extensions.Logging.Abstractions;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Esquema;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>O V007 como um todo: o que o script cria, que ele pode ser aplicado de novo e como a API o aplica (o divisor de scripts e o executor).</summary>
[Collection(MySqlCollection.Nome)]
public class V007EmGeralTests(MySqlFixture mysql)
{
    private static readonly string[] AsVinteEOitoProcedures =
    [
        "SP_REGISTRAR_MARCACAO_PONTO", "SP_SOLICITAR_AJUSTE_PONTO",
        "SP_CONFERIR_FERIAS", "SP_SOLICITAR_FERIAS", "SP_REGISTRAR_FERIAS_RH", "SP_DECIDIR_FERIAS_GESTOR", "SP_RESPONDER_SUGESTAO_FERIAS", "SP_CONFIRMAR_FERIAS_RH", "SP_RECUSAR_FERIAS_RH",
        "SP_PUBLICAR_HOLERITE", "SP_SUBSTITUIR_HOLERITE", "SP_ENVIAR_ATESTADO", "SP_DECIDIR_ATESTADO", "SP_SOLICITAR_BENEFICIO", "SP_DECIDIR_BENEFICIO",
        "SP_GERAR_ADVERTENCIA", "SP_REGISTRAR_CIENCIA_ADVERTENCIA", "SP_CONTESTAR_ADVERTENCIA", "SP_RESPONDER_CONTESTACAO_ADVERTENCIA", "SP_CONCLUIR_MODULO_TREINAMENTO", "SP_ENCERRAR_FORMULARIO",
        "SP_AGENDAR_DESLIGAMENTO", "SP_CANCELAR_DESLIGAMENTO", "SP_EFETIVAR_DESLIGAMENTO",
        "SP_CRIAR_USUARIO", "SP_CADASTRAR_FUNCIONARIO", "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", "SP_CADASTRAR_CARGO",
    ];

    private static async Task<List<string>> ProceduresDoBancoAsync(string conexao) =>
        await Sql.ColunaAsync(conexao, "SELECT ROUTINE_NAME FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE' ORDER BY ROUTINE_NAME");

    [Fact]
    public async Task O_banco_com_os_scripts_tem_exatamente_as_28_procedures_e_nenhuma_funcao_ou_trigger_nova_do_V007()
    {
        var conexao = await NovoBancoAsync(mysql);

        var procedures = await ProceduresDoBancoAsync(conexao);

        Assert.Equal(AsVinteEOitoProcedures.Order(StringComparer.Ordinal), procedures);
        Assert.DoesNotContain("SP_LANCAR_BANCO_HORAS", procedures); // removida: o banco de horas só nasce da consolidação da API
        Assert.DoesNotContain("SP_CADASTRAR_NIVEL_ACESSO", procedures); // removida: a API só conhece os três níveis do V005
        Assert.Equal(8L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM information_schema.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'FUNCTION'")); // as 8 do V003
    }

    [Fact]
    public async Task O_V007_aplicado_de_novo_recria_as_procedures_sem_erro_porque_cada_uma_comeca_por_DROP_IF_EXISTS()
    {
        var conexao = await NovoBancoAsync(mysql);
        var v007 = new ScriptsEmbutidos().Carregar().Single(s => s.Versao == 7);

        await using (var c = new MySqlConnection(conexao))
        {
            await c.OpenAsync();
            foreach (var comando in DivisorDeScript.Dividir(v007.Conteudo))
            {
                await using var executar = new MySqlCommand(comando, c);
                await executar.ExecuteNonQueryAsync();
            }

            // uma segunda aplicação (a subida foi interrompida no meio da primeira, por exemplo) não pode parar em "already exists"
            foreach (var comando in DivisorDeScript.Dividir(v007.Conteudo))
            {
                await using var executar = new MySqlCommand(comando, c);
                await executar.ExecuteNonQueryAsync();
            }
        }

        Assert.Equal(AsVinteEOitoProcedures.Order(StringComparer.Ordinal), await ProceduresDoBancoAsync(conexao));
    }

    [Fact]
    public async Task O_divisor_parte_o_V007_em_28_DROP_e_28_CREATE_e_cada_CREATE_termina_em_END()
    {
        var v007 = new ScriptsEmbutidos().Carregar().Single(s => s.Versao == 7);

        var comandos = DivisorDeScript.Dividir(v007.Conteudo);

        Assert.Equal(28, comandos.Count(c => c.Contains("DROP PROCEDURE IF EXISTS SP_", StringComparison.Ordinal) && !c.Contains("CREATE PROCEDURE", StringComparison.Ordinal)));
        var criacoes = comandos.Where(c => c.Contains("CREATE PROCEDURE SP_", StringComparison.Ordinal)).ToList();
        Assert.Equal(28, criacoes.Count);
        Assert.All(criacoes, c => Assert.EndsWith("END", c, StringComparison.Ordinal));
        Assert.Equal(56, comandos.Count);
    }

    [Fact]
    public async Task A_API_aplica_o_V007_ao_subir_e_o_registra_como_versao_aplicada()
    {
        var conexao = await mysql.NovoBancoAsync();
        var executor = new ExecutorDeScripts(conexao, NullLogger<ExecutorDeScripts>.Instance);

        await executor.AplicarPendentesAsync(new ScriptsEmbutidos().Carregar(), CancellationToken.None);

        Assert.Equal(28, (await ProceduresDoBancoAsync(conexao)).Count);
        await executor.AplicarPendentesAsync(new ScriptsEmbutidos().Carregar(), CancellationToken.None); // a segunda subida não reaplica nada
        Assert.Equal(28, (await ProceduresDoBancoAsync(conexao)).Count);
    }
}
