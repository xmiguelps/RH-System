using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// O que a auditoria registra do cadastro de colaborador (as marcas que a 1b acrescentou ao <c>Funcionario</c>), com o interceptor de verdade e o relógio falso, no banco
/// REAL. Um banco para a classe; cada teste cria o colaborador dele.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaDoCadastroTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private Task<(string Conexao, PessoaDeTeste Ator)> BancoAsync() =>
        BancoCompartilhado.ObterAsync(mysql, nameof(AuditoriaDoCadastroTests), PrepararBancoAsync);

    /// <summary>Um colaborador novo, gravado pelo contexto auditado (o evento <c>Criou</c> já existe) com cargo e setor, e o contexto em que ele continua rastreado.</summary>
    private static async Task<(ContextoAuditadoDeTeste Db, Funcionario Pessoa, int CargoAtual, int CargoNovo, int SetorId)> NovoColaboradorAsync(string conexao, PessoaDeTeste ator, string sufixo)
    {
        var analista = await SementeDoCadastro.CargoAsync(conexao, $"Analista {sufixo}", 40m);
        var pleno = await SementeDoCadastro.CargoAsync(conexao, $"Analista pleno {sufixo}", 40m);
        var setor = await SementeDoCadastro.SetorAsync(conexao, $"Setor {sufixo}");
        var db = NovoContexto(conexao, PessoaComIp(ator), RelogioEm(Agora));
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");
        var pessoa = Funcionario.Criar("Bruno Lima", $"bruno.{sufixo}@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
        pessoa.AtualizarCadastro("Bruno Lima", $"bruno.{sufixo}@empresa.com.br", new DateOnly(1990, 5, 17), new DateOnly(2026, 1, 2), TipoDeContrato.Clt, analista, setor, null, nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return (db, pessoa, analista, pleno, setor);
    }

    [Fact]
    public async Task Alterar_cargo_do_funcionario_gera_Alterou_com_antes_e_depois_pelo_NOME_do_cargo()
    {
        var (conexao, ator) = await BancoAsync();
        var (db, pessoa, _, cargoNovo, setor) = await NovoColaboradorAsync(conexao, ator, "cargo");
        await using var _ = db;
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");

        pessoa.AtualizarCadastro(pessoa.Nome, pessoa.EmailCorporativo!, new DateOnly(1990, 5, 17), pessoa.DataAdmissao, TipoDeContrato.Clt, cargoNovo, setor, null, nivel);
        await db.SaveChangesAsync();

        var alterou = Assert.Single(await EventosAsync(conexao, "FUNCIONARIOS", pessoa.Id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal("Cargo de Bruno Lima", alterou.Descricao);
        var mudanca = Assert.Single(alterou.Mudancas);
        Assert.Equal(("Cargo", "Analista cargo", "Analista pleno cargo", false), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois, mudanca.ValorOculto));
    }

    [Fact]
    public async Task Alterar_nivel_de_acesso_gera_Alterou_com_o_NOME_do_nivel_como_a_tela_mostra()
    {
        var (conexao, ator) = await BancoAsync();
        var (db, pessoa, cargo, _, setor) = await NovoColaboradorAsync(conexao, ator, "nivel");
        await using var _ = db;
        var gestor = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario Gestor");

        // Só a navegação muda (o domínio não toca na chave): o log tem de enxergar a troca mesmo assim.
        pessoa.AtualizarCadastro(pessoa.Nome, pessoa.EmailCorporativo!, new DateOnly(1990, 5, 17), pessoa.DataAdmissao, TipoDeContrato.Clt, cargo, setor, null, gestor);
        await db.SaveChangesAsync();

        var alterou = Assert.Single(await EventosAsync(conexao, "FUNCIONARIOS", pessoa.Id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Equal("Nível de acesso de Bruno Lima", alterou.Descricao);
        var mudanca = Assert.Single(alterou.Mudancas);
        Assert.Equal(("Nível de acesso", "Colaborador", "Gestor", false), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois, mudanca.ValorOculto));
    }

    [Fact]
    public async Task Alterar_data_de_nascimento_e_tipo_de_contrato_registra_so_que_mudou_e_nunca_a_data()
    {
        var (conexao, ator) = await BancoAsync();
        var (db, pessoa, cargo, _, setor) = await NovoColaboradorAsync(conexao, ator, "nascimento");
        await using var _ = db;
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");

        pessoa.AtualizarCadastro(pessoa.Nome, pessoa.EmailCorporativo!, new DateOnly(1991, 6, 18), pessoa.DataAdmissao, TipoDeContrato.Estagio, cargo, setor, null, nivel);
        await db.SaveChangesAsync();

        var alterou = Assert.Single(await EventosAsync(conexao, "FUNCIONARIOS", pessoa.Id), e => e.Acao == AuditoriaAcao.Alterou);
        Assert.Contains(alterou.Mudancas, m => m is { Campo: "Data de nascimento", ValorAntes: null, ValorDepois: null, ValorOculto: true });
        Assert.Contains(alterou.Mudancas, m => m is { Campo: "Tipo de contrato", ValorAntes: "CLT", ValorDepois: "ESTAGIO", ValorOculto: false });

        // Nenhuma data de nascimento, antes ou depois, em lugar nenhum do log (o evento Criou também tem o campo oculto).
        var (_, texto) = await TudoDoLogAsync(conexao);
        Assert.DoesNotContain("1990-05-17", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("17/05/1990", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("1991-06-18", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("18/06/1991", texto, StringComparison.Ordinal);
    }
}
