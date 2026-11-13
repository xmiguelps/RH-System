using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Gestao;

namespace RHSystem.Api.IntegrationTests.Gestao;

/// <summary>
/// O tradutor de recusa do banco com exceções REAIS do MySQL: os triggers de férias (pessoa inativa, gestor analisando as próprias férias, aprovação sem gestor), o do benefício ativo para pessoa inativa e a chave única da adesão (a pessoa já tem ou já pediu o benefício). A exceção de transição inválida
/// e outras recusas de trigger viram o 422 genérico com a regra em <c>regraDoBanco</c>; a chave única da exceção de pagamento devolve <c>null</c> (o caso de uso a trata como "já existe, atualizar"). A mensagem do MySQL traz o VALOR da chave e nunca é copiada para a resposta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TradutorDeRecusaDaGestaoTests(MySqlFixture mysql)
{
    private static int _sequencia;

    private async Task<string> ConexaoAsync()
    {
        var (conexao, _) = await BancoCompartilhado.ObterAsync(mysql, nameof(TradutorDeRecusaDaGestaoTests), _ => Task.FromResult(0));
        return conexao;
    }

    private static async Task<int> NovaPessoaAsync(string conexao, bool ativa = true)
    {
        var n = Interlocked.Increment(ref _sequencia);
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa tradutor {n}", $"tradutor.gestao{n}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        if (!ativa)
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO = {pessoa.Id}");
        }

        return pessoa.Id;
    }

    private static async Task<int> InteiroAsync(string conexao, string sql) => Convert.ToInt32(await Sql.EscalarAsync(conexao, sql), CultureInfo.InvariantCulture);

    private static async Task<int> GestorAsync(string conexao, int funcionarioId)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionarioId})");
        return await InteiroAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionarioId}");
    }

    private static async Task<int> FeriasAsync(string conexao, int pessoa, string status = "SOLICITADA", int? gestor = null)
    {
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS, ID_GESTOR) VALUES ({pessoa}, '2026-01-02', '2026-02-01', '2026-12-01', '2026-12-10', '{status}', {(gestor is null ? "NULL" : gestor.Value.ToString(CultureInfo.InvariantCulture))})");
        return await InteiroAsync(conexao, $"SELECT MAX(ID_FERIAS) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa}");
    }

    private static async Task<int> BeneficioAsync(string conexao)
    {
        var nome = $"Beneficio {Guid.NewGuid():N}";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS (NOME_BENEFICIO, OPCIONAL) VALUES ('{nome}', TRUE)");
        return await InteiroAsync(conexao, $"SELECT ID_BENEFICIO FROM BENEFICIOS WHERE NOME_BENEFICIO = '{nome}'");
    }

    [Fact]
    public async Task Os_tres_triggers_de_ferias_viram_os_422_com_codigo_e_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var inativa = await NovaPessoaAsync(conexao, ativa: false);
        var pessoa = await NovaPessoaAsync(conexao);
        var gestoraDeSiMesma = await GestorAsync(conexao, pessoa);
        var comGestorProprio = await FeriasAsync(conexao, pessoa, gestor: gestoraDeSiMesma);
        var semGestor = await FeriasAsync(conexao, pessoa);

        var deInativa = await Assert.ThrowsAnyAsync<Exception>(() => FeriasAsync(conexao, inativa));
        var proprias = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'APROVADA' WHERE ID_FERIAS = {comGestorProprio}"));
        var semResponsavel = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'APROVADA' WHERE ID_FERIAS = {semGestor}"));

        var inativo = TradutorDeRecusaDaGestao.Traduzir(deInativa)!;
        var proprio = TradutorDeRecusaDaGestao.Traduzir(proprias)!;
        var foraDeGestores = TradutorDeRecusaDaGestao.Traduzir(semResponsavel)!;
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Funcionario inativo nao pode solicitar ferias."), (inativo.Status, inativo.Codigo, inativo.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "PEDIDO_PROPRIO", "O gestor nao pode analisar as proprias ferias."), (proprio.Status, proprio.Codigo, proprio.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "PEDIDO_RH_FORA_DE_GESTORES", "Informe o gestor responsavel pela analise das ferias."), (foraDeGestores.Status, foraDeGestores.Codigo, foraDeGestores.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Outra_recusa_de_trigger_vira_o_422_generico_com_a_regra_e_sem_codigo()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var ferias = await FeriasAsync(conexao, pessoa);

        var salto = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"UPDATE FERIAS SET STATUS_FERIAS = 'EM_GOZO' WHERE ID_FERIAS = {ferias}"));

        var problema = TradutorDeRecusaDaGestao.Traduzir(salto)!;
        Assert.Equal((422, (string?)null, "Transicao de status de ferias invalida."), (problema.Status, problema.Codigo, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task O_beneficio_ativo_para_pessoa_inativa_vira_o_422_de_pedir_e_a_chave_unica_da_adesao_vira_o_409_sem_o_valor()
    {
        var conexao = await ConexaoAsync();
        var inativa = await NovaPessoaAsync(conexao, ativa: false);
        var pessoa = await NovaPessoaAsync(conexao);
        var beneficio = await BeneficioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO) VALUES ({pessoa}, {beneficio}, FALSE, 'CANCELADO')");

        var deInativa = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO) VALUES ({inativa}, {beneficio}, TRUE, 'APROVADO')"));
        var repetida = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO) VALUES ({pessoa}, {beneficio}, FALSE, 'PENDENTE', '2026-10-07 09:42:00')"));

        var inativo = TradutorDeRecusaDaGestao.Traduzir(deInativa)!;
        var jaTem = TradutorDeRecusaDaGestao.Traduzir(repetida)!;
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Não é possível pedir nem ativar benefício para uma pessoa inativa."), (inativo.Status, inativo.Codigo, inativo.Detalhe));
        Assert.Equal((409, "BENEFICIO_JA_ATIVO_OU_PENDENTE", "Você já tem este benefício ou já pediu a adesão e o RH ainda vai responder."), (jaTem.Status, jaTem.Codigo, jaTem.Detalhe));
        Assert.DoesNotContain("UQ_BENEFICIOS", jaTem.Detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task A_chave_unica_da_excecao_de_pagamento_e_o_que_nao_e_do_banco_devolvem_null()
    {
        var conexao = await ConexaoAsync();
        await Sql.ExecutarAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 5, '2031-05-06')");

        var repetida = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, "INSERT INTO PAGAMENTOS_EXCECOES (ANO, MES, DATA_PAGAMENTO) VALUES (2031, 5, '2031-05-07')"));

        Assert.IsType<MySqlException>(repetida);
        Assert.Null(TradutorDeRecusaDaGestao.Traduzir(repetida));
        Assert.Null(TradutorDeRecusaDaGestao.Traduzir(new InvalidOperationException("outra coisa")));
    }
}
