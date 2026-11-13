using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Treinamentos;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// O tradutor de recusa do banco com exceções REAIS do MySQL: o trigger da pessoa inativa, o do concluído que não muda, as chaves únicas dos módulos e das linhas (a ordem do módulo vira o 409 sem código; as outras voltam <c>null</c> porque não são erro para quem chama) e o que não é dos treinamentos (<c>null</c>).
/// A mensagem do MySQL traz o VALOR da chave e nunca é copiada para a resposta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TradutorDeRecusaDosTreinamentosTests(MySqlFixture mysql)
{
    private static int _sequencia;

    private async Task<string> ConexaoAsync()
    {
        var (conexao, _) = await BancoCompartilhado.ObterAsync(mysql, nameof(TradutorDeRecusaDosTreinamentosTests), _ => Task.FromResult(0));
        return conexao;
    }

    private static async Task<int> NovaPessoaAsync(string conexao, bool ativa = true)
    {
        var n = Interlocked.Increment(ref _sequencia);
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa tradutor {n}", $"tradutor.treinamentos{n}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        if (!ativa)
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO = {pessoa.Id}");
        }

        return pessoa.Id;
    }

    private static async Task<int> NovoTreinamentoAsync(string conexao)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var treinamento = Treinamento.Criar($"Treinamento {Guid.NewGuid():N}", null, false, false, null, true, true, null);
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        return treinamento.Id;
    }

    [Fact]
    public async Task A_pessoa_inativa_vira_o_422_FUNCIONARIO_INATIVO_com_a_regra_do_banco_e_o_concluido_que_muda_vira_o_422_generico()
    {
        var conexao = await ConexaoAsync();
        var inativa = await NovaPessoaAsync(conexao, ativa: false);
        var ativa = await NovaPessoaAsync(conexao);
        var treinamento = await NovoTreinamentoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_CONCLUSAO) VALUES ({treinamento}, {ativa}, 'CONCLUIDO', '2026-10-07')");

        var inativoRecusado = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {inativa}, 'INSCRITO')"));
        var concluidoRecusado = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_FUNCIONARIOS SET STATUS_TREINAMENTO = 'EM_ANDAMENTO' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {ativa}"));

        var inativo = TradutorDeRecusaDosTreinamentos.Traduzir(inativoRecusado)!;
        var concluido = TradutorDeRecusaDosTreinamentos.Traduzir(concluidoRecusado)!;
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Nao e possivel inscrever funcionario inativo em treinamento."), (inativo.Status, inativo.Codigo, inativo.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, (string?)null, "Treinamento concluido nao pode mudar de status."), (concluido.Status, concluido.Codigo, concluido.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task A_ordem_repetida_do_modulo_vira_o_409_sem_codigo_e_sem_o_valor_da_chave()
    {
        var conexao = await ConexaoAsync();
        var treinamento = await NovoTreinamentoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, CONTEUDO_TEXTO) VALUES ({treinamento}, 1, 'Primeiro', 'TEXTO', 'a')");

        var repetida = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, CONTEUDO_TEXTO) VALUES ({treinamento}, 1, 'Segundo', 'TEXTO', 'b')"));

        var problema = TradutorDeRecusaDosTreinamentos.Traduzir(repetida)!;
        Assert.Equal((409, (string?)null, "Os módulos deste treinamento mudaram em outra tela. Atualize a página e tente de novo."), (problema.Status, problema.Codigo, problema.Detalhe));
        Assert.DoesNotContain("UQ_TREINAMENTOS", problema.Detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public async Task As_outras_chaves_unicas_dos_treinamentos_e_o_que_nao_e_deles_voltam_null()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var treinamento = await NovoTreinamentoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, 'INSCRITO')");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS (ID_TREINAMENTO, ORDEM, TITULO, TIPO_MODULO, CONTEUDO_TEXTO) VALUES ({treinamento}, 1, 'Módulo', 'TEXTO', 'a')");
        var modulo = (int)await Sql.ContarAsync(conexao, $"SELECT ID_MODULO FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {treinamento}");
        await Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO) VALUES ({modulo}, {pessoa})");

        var linhaRepetida = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, 'INSCRITO')"));
        var progressoRepetido = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO) VALUES ({modulo}, {pessoa})"));
        var deOutraTabela = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({treinamento}, 999999)"));

        Assert.Null(TradutorDeRecusaDosTreinamentos.Traduzir(linhaRepetida));
        Assert.Null(TradutorDeRecusaDosTreinamentos.Traduzir(progressoRepetido));
        Assert.Null(TradutorDeRecusaDosTreinamentos.Traduzir(deOutraTabela)); // chave estrangeira que não existe: o caso de uso confere antes, aqui nada a traduzir
        Assert.Null(TradutorDeRecusaDosTreinamentos.Traduzir(new InvalidOperationException("qualquer outra coisa")));
    }
}
