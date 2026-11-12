using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Comunicacao;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// O tradutor de recusa do banco com exceções REAIS do MySQL: as dos triggers do holerite e da advertência e a chave única do holerite vêm de gravações de verdade; para os textos que a aplicação só vê em condições raras (o gestor que
/// advertiria a si mesmo, a expiração anterior à publicação), o teste levanta o mesmo <c>SIGNAL SQLSTATE '45000'</c> com a mensagem original do trigger. O que não é da comunicação volta <c>null</c> (o tratador global decide).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TradutorDeRecusaDaComunicacaoTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static int _sequencia;

    private async Task<string> ConexaoAsync()
    {
        var (conexao, _) = await BancoCompartilhado.ObterAsync(mysql, nameof(TradutorDeRecusaDaComunicacaoTests), async nova =>
        {
            await Sql.ExecutarAsync(nova, "CREATE PROCEDURE P_SINALIZA(IN mensagem VARCHAR(200)) BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = mensagem; END");
            return 0;
        });
        return conexao;
    }

    private static async Task<int> NovaPessoaAsync(string conexao, string? email = null)
    {
        var n = Interlocked.Increment(ref _sequencia);
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa tradutor {n}", email ?? $"tradutor.comunicacao{n}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return pessoa.Id;
    }

    private static async Task<Exception> ExcecaoDeAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception erro)
        {
            return erro;
        }

        throw new InvalidOperationException("A gravação devia ter sido recusada pelo banco.");
    }

    private static async Task<Exception> HoleriteRecusadoAsync(string conexao, int pessoa, int mes, int ano, int versao = 1)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        db.Holerites.Add(Holerite.Criar(pessoa, mes, ano, TipoDeHolerite.Mensal, versao, Agora, $"holerites/{Guid.NewGuid():N}.pdf"));
        return await ExcecaoDeAsync(() => db.SaveChangesAsync());
    }

    private static readonly ContextoDaComunicacao Envio = new(OperacaoDaComunicacao.EnviarHolerite, TipoDeHolerite.Mensal, 10, 2026);

    // ---------------------------------------------------------------- o que o trigger recusa

    [Fact]
    public async Task A_competencia_do_holerite_que_o_trigger_recusa_vira_o_422_do_contrato_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var futura = await HoleriteRecusadoAsync(conexao, await NovaPessoaAsync(conexao), 1, 2100);
        var anterior = await HoleriteRecusadoAsync(conexao, await NovaPessoaAsync(conexao), 12, 2025); // a pessoa foi admitida em 2026-01-02
        var desligada = await NovaPessoaAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-03-31' WHERE ID_FUNCIONARIO = {desligada}");
        var posterior = await HoleriteRecusadoAsync(conexao, desligada, 5, 2026);

        var daFutura = TradutorDeRecusaDaComunicacao.Traduzir(futura, Envio)!;
        var daAnterior = TradutorDeRecusaDaComunicacao.Traduzir(anterior, Envio)!;
        var daPosterior = TradutorDeRecusaDaComunicacao.Traduzir(posterior, Envio)!;

        Assert.Equal((422, "HOLERITE_COMPETENCIA_FUTURA", "Só o holerite de férias pode ter competência futura.", "Nao e possivel cadastrar holerite de competencia futura."), (daFutura.Status, daFutura.Codigo, daFutura.Detalhe, daFutura.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", "A competência do holerite é anterior à admissão da pessoa.", "A competencia do holerite e anterior a admissao."), (daAnterior.Status, daAnterior.Codigo, daAnterior.Detalhe, daAnterior.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", "A competência do holerite é posterior ao desligamento da pessoa.", "A competencia do holerite e posterior ao desligamento."), (daPosterior.Status, daPosterior.Codigo, daPosterior.Detalhe, daPosterior.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Advertencia_para_pessoa_inativa_vira_FUNCIONARIO_INATIVO_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {pessoa}");
        await using var db = ContextoDeTeste.Novo(conexao);
        db.Advertencias.Add(Advertencia.Registrar(pessoa, null, new DateOnly(2026, 10, 5), TipoDeAdvertencia.Verbal, "Atraso", "Descrição"));
        var erro = await ExcecaoDeAsync(() => db.SaveChangesAsync());

        var problema = TradutorDeRecusaDaComunicacao.Traduzir(erro, new ContextoDaComunicacao(OperacaoDaComunicacao.RegistrarAdvertencia))!;

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Não é possível registrar advertência para uma pessoa inativa.", "Nao e possivel registrar advertencia para funcionario inativo."), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task O_gestor_que_advertiria_a_si_mesmo_vem_por_SIGNAL_com_a_mensagem_original_e_vira_o_codigo_do_plano()
    {
        var conexao = await ConexaoAsync();
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "CALL P_SINALIZA('O gestor nao pode advertir a si mesmo.')"));

        var problema = TradutorDeRecusaDaComunicacao.Traduzir(erro, new ContextoDaComunicacao(OperacaoDaComunicacao.RegistrarAdvertencia))!;

        Assert.Equal((422, "ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO", "O gestor não pode aplicar uma advertência a si mesmo.", "O gestor nao pode advertir a si mesmo."), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Regra_do_banco_que_o_contrato_nao_nomeia_vira_a_recusa_generica_sem_codigo_com_a_mensagem_original()
    {
        var conexao = await ConexaoAsync();
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "CALL P_SINALIZA('A expiracao do aviso nao pode ser anterior a publicacao.')"));
        var desconhecida = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "CALL P_SINALIZA('Uma regra que ninguem conhece.')"));

        var daExpiracao = TradutorDeRecusaDaComunicacao.Traduzir(erro, Envio)!;
        var daDesconhecida = TradutorDeRecusaDaComunicacao.Traduzir(desconhecida, Envio)!;

        Assert.Equal((422, (string?)null, "A operação foi recusada por uma regra do banco de dados.", "A expiracao do aviso nao pode ser anterior a publicacao."), (daExpiracao.Status, daExpiracao.Codigo, daExpiracao.Detalhe, daExpiracao.Extensoes!["regraDoBanco"]));
        Assert.Equal("Uma regra que ninguem conhece.", daDesconhecida.Extensoes!["regraDoBanco"]);
        Assert.Equal("Operação recusada.", daDesconhecida.Titulo);
    }

    // ---------------------------------------------------------------- a chave única do holerite

    [Fact]
    public async Task A_chave_repetida_do_holerite_e_ja_existe_no_envio_e_versao_desatualizada_na_nova_versao()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.Holerites.Add(Holerite.Criar(pessoa, 1, 2026, TipoDeHolerite.Mensal, 1, Agora, "holerites/primeiro.pdf"));
            await db.SaveChangesAsync();
        }

        var repetido = await HoleriteRecusadoAsync(conexao, pessoa, 1, 2026);

        var noEnvio = TradutorDeRecusaDaComunicacao.Traduzir(repetido, new ContextoDaComunicacao(OperacaoDaComunicacao.EnviarHolerite, TipoDeHolerite.Mensal, 1, 2026))!;
        var naVersao = TradutorDeRecusaDaComunicacao.Traduzir(repetido, new ContextoDaComunicacao(OperacaoDaComunicacao.SubstituirHolerite, TipoDeHolerite.Mensal, 1, 2026))!;
        var emOutra = TradutorDeRecusaDaComunicacao.Traduzir(repetido, new ContextoDaComunicacao(OperacaoDaComunicacao.RegistrarAdvertencia));

        Assert.Equal((409, "HOLERITE_JA_EXISTE", "Já existe um holerite mensal de janeiro de 2026 para esta pessoa. Para corrigir, envie uma nova versão.", (object?)null), (noEnvio.Status, noEnvio.Codigo, noEnvio.Detalhe, noEnvio.Extensoes));
        Assert.Equal((409, "HOLERITE_VERSAO_DESATUALIZADA", "Este holerite já foi substituído por uma versão mais nova. Atualize a lista."), (naVersao.Status, naVersao.Codigo, naVersao.Detalhe));
        Assert.Null(emOutra);
    }

    // ---------------------------------------------------------------- o que não é da comunicação

    [Fact]
    public async Task Chave_repetida_de_outra_tabela_e_erro_que_nao_e_do_mysql_voltam_null()
    {
        var conexao = await ConexaoAsync();
        var email = $"repetido.comunicacao{Interlocked.Increment(ref _sequencia)}@teste.com";
        await NovaPessoaAsync(conexao, email);
        var repetida = await ExcecaoDeAsync(() => NovaPessoaAsync(conexao, email)); // UQ do e-mail corporativo: outra chave, outra fatia

        Assert.Null(TradutorDeRecusaDaComunicacao.Traduzir(repetida, Envio));
        Assert.Null(TradutorDeRecusaDaComunicacao.Traduzir(new InvalidOperationException("outro erro"), Envio));
    }
}
