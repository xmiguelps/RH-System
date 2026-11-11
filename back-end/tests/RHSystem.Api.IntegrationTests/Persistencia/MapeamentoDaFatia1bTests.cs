using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta do que a fatia 1b mapeia, no banco REAL (MySQL em contêiner com os scripts), relendo com OUTRO contexto. Um banco para a classe inteira; cada teste
/// usa gente, cargo, setor e benefício com nome próprio.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDaFatia1bTests(MySqlFixture mysql)
{
    private Task<string> BancoAsync() => BancoCompartilhado.ObterAsync(mysql, nameof(MapeamentoDaFatia1bTests));

    private static async Task<Funcionario> NovaPessoaAsync(string conexao, string sufixo, string nivel = "Funcionario")
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivelDeAcesso = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nivel);
        var pessoa = Funcionario.Criar($"Pessoa {sufixo}", $"pessoa.{sufixo}@empresa.com.br", new DateOnly(2026, 1, 2), nivelDeAcesso);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return pessoa;
    }

    /// <summary>Faz da pessoa um gestor (linha em <c>GESTORES</c>) e devolve o <c>ID_GESTOR</c>.</summary>
    private static async Task<int> TornarGestorAsync(string conexao, int funcionarioId)
    {
        await ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({funcionarioId})");
        return await EscalarIntAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionarioId}");
    }

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    private static async Task<object?> EscalarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        return await comando.ExecuteScalarAsync();
    }

    private static async Task<int> EscalarIntAsync(string conexao, string sql) =>
        Convert.ToInt32(await EscalarAsync(conexao, sql), CultureInfo.InvariantCulture);

    private static MySqlException ErroDoMySql(DbUpdateException erro) => Assert.IsType<MySqlException>(erro.InnerException);

    [Fact]
    public async Task FuncionarioDetalhes_ida_e_volta_com_CPF_char_11_e_UF_em_maiusculas()
    {
        var conexao = await BancoAsync();
        var pessoa = await NovaPessoaAsync(conexao, "detalhes");
        var cpf = CpfsDeTeste.Valido(1001);
        var dados = new DadosPessoais(cpf, "11987654410", "pessoal@exemplo.com", "123456789", "SSP", "01310100", "Av. Paulista", "1000", "Sala 2", "Bela Vista", "São Paulo", "sp");

        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var doBanco = await escrita.Funcionarios.SingleAsync(f => f.Id == pessoa.Id);
            escrita.DetalhesDosFuncionarios.Add(FuncionarioDetalhes.Criar(doBanco, dados));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = await leitura.DetalhesDosFuncionarios.SingleAsync(d => d.FuncionarioId == pessoa.Id);
        Assert.Equal(cpf, lido.Cpf);
        Assert.Equal(11, lido.Cpf.Length);
        Assert.Equal(dados with { Uf = "SP" }, lido.ParaDados()); // o trigger do banco põe a UF em maiúsculas; o resto volta como foi

        // E a navegação inversa lê o mesmo registro.
        var comDetalhes = await leitura.Funcionarios.Include(f => f.Detalhes).SingleAsync(f => f.Id == pessoa.Id);
        Assert.Equal(cpf, comDetalhes.Detalhes!.Cpf);
    }

    [Fact]
    public async Task BeneficioDoFuncionario_grava_Ativo_true_e_false_e_Status_APROVADO_como_enviados()
    {
        var conexao = await BancoAsync();
        var pessoa = await NovaPessoaAsync(conexao, "beneficio");
        var beneficioId = await SementeDoCadastro.BeneficioAsync(conexao, "Vale-refeição 1b");

        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.BeneficiosDosFuncionarios.Add(BeneficioDoFuncionario.Conceder(pessoa.Id, beneficioId));
            await escrita.SaveChangesAsync();
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var linha = await leitura.BeneficiosDosFuncionarios.Include(b => b.Beneficio).SingleAsync(b => b.FuncionarioId == pessoa.Id);
            Assert.True(linha.Ativo);
            Assert.Equal(StatusDoPedidoDeBeneficio.Aprovado, linha.Status);
            Assert.Equal("Vale-refeição 1b", linha.Beneficio!.Nome);
        }

        // O que está no banco é o que o EF enviou: STATUS_PEDIDO não tem padrão desde o V001 e BENEFICIO_ATIVO tem, mas o EF manda sempre o valor.
        Assert.Equal("APROVADO", await EscalarAsync(conexao, $"SELECT STATUS_PEDIDO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa.Id}"));

        await using (var desativa = ContextoDeTeste.Novo(conexao))
        {
            var linha = await desativa.BeneficiosDosFuncionarios.SingleAsync(b => b.FuncionarioId == pessoa.Id);
            linha.Desativar();
            await desativa.SaveChangesAsync();
        }

        Assert.Equal(0, await EscalarIntAsync(conexao, $"SELECT BENEFICIO_ATIVO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa.Id}")); // false de verdade (o padrão do banco é TRUE)

        await using var depois = ContextoDeTeste.Novo(conexao);
        var desativada = await depois.BeneficiosDosFuncionarios.SingleAsync(b => b.FuncionarioId == pessoa.Id);
        Assert.False(desativada.Ativo);
        Assert.Equal(StatusDoPedidoDeBeneficio.Aprovado, desativada.Status);
    }

    [Fact]
    public async Task Funcionario_grava_DataNascimento_DataDesligamento_TipoContrato_e_GestorId_e_o_gestor_do_setor_ida_e_volta()
    {
        var conexao = await BancoAsync();
        var gestor = await NovaPessoaAsync(conexao, "gestor.ida", "Funcionario Gestor");
        var idGestor = await TornarGestorAsync(conexao, gestor.Id);
        var setorId = await SementeDoCadastro.SetorAsync(conexao, "Setor ida e volta", idGestor);
        var cargoId = await SementeDoCadastro.CargoAsync(conexao, "Cargo ida e volta", 40m);

        int id;
        int nivelId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var nivel = await escrita.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario");
            nivelId = nivel.Id;
            var pessoa = Funcionario.Criar("Bruno Lima", "bruno.ida@empresa.com.br", new DateOnly(2026, 2, 3), nivel);
            pessoa.AtualizarCadastro("Bruno Lima", "bruno.ida@empresa.com.br", new DateOnly(1990, 5, 17), new DateOnly(2026, 2, 3), TipoDeContrato.Estagio, cargoId, setorId, idGestor, nivel);
            escrita.Funcionarios.Add(pessoa);
            await escrita.SaveChangesAsync();
            id = pessoa.Id;
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var pessoa = await leitura.Funcionarios.Include(f => f.Setor).SingleAsync(f => f.Id == id);
            Assert.Equal(new DateOnly(1990, 5, 17), pessoa.DataNascimento);
            Assert.Equal("ESTAGIO", pessoa.TipoContrato);
            Assert.Equal(idGestor, pessoa.GestorId);
            Assert.Equal((cargoId, setorId, nivelId), (pessoa.CargoId, pessoa.SetorId, pessoa.NivelDeAcessoId));
            Assert.Equal(idGestor, pessoa.Setor!.GestorId); // o gestor do colaborador é o gestor do setor
            Assert.Null(pessoa.DataDesligamento);
        }

        await using (var desliga = ContextoDeTeste.Novo(conexao))
        {
            var pessoa = await desliga.Funcionarios.SingleAsync(f => f.Id == id);
            pessoa.Desligar(new DateOnly(2026, 10, 7));
            await desliga.SaveChangesAsync();
        }

        await using var depois = ContextoDeTeste.Novo(conexao);
        var desligada = await depois.Funcionarios.SingleAsync(f => f.Id == id);
        Assert.Equal(new DateOnly(2026, 10, 7), desligada.DataDesligamento);
        Assert.False(desligada.Ativo);
    }

    [Fact]
    public async Task Tipo_de_contrato_fora_de_CLT_e_ESTAGIO_le_sem_cair_e_sem_valor_no_enum()
    {
        var conexao = await BancoAsync();
        var pessoa = await NovaPessoaAsync(conexao, "contrato.livre");
        await SementeDoCadastro.DefinirTipoDeContratoAsync(conexao, pessoa.Id, "PJ");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lida = await leitura.Funcionarios.SingleAsync(f => f.Id == pessoa.Id);
        Assert.Equal("PJ", lida.TipoContrato); // o banco guarda o que veio
        Assert.Null(TiposDeContrato.DoTexto(lida.TipoContrato)); // e a ficha mostra null (decisão 9 do plano)
    }

    [Fact]
    public async Task Cargo_le_CargaSemanalHoras_44_00_como_decimal()
    {
        var conexao = await BancoAsync();
        var comCarga = await SementeDoCadastro.CargoAsync(conexao, "Analista 44h", 44.00m);
        await ExecutarAsync(conexao, "INSERT INTO CARGOS (NOME_CARGO) VALUES ('Cargo sem carga')");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        Assert.Equal(44.00m, (await leitura.Cargos.SingleAsync(c => c.Id == comCarga)).CargaSemanalHoras);
        Assert.Null((await leitura.Cargos.SingleAsync(c => c.Nome == "Cargo sem carga")).CargaSemanalHoras);
    }

    [Fact]
    public async Task Setor_DefinirGestor_grava_e_limpa_o_gestor()
    {
        var conexao = await BancoAsync();
        var gestor = await NovaPessoaAsync(conexao, "gestor.setor", "Funcionario Gestor");
        var idGestor = await TornarGestorAsync(conexao, gestor.Id);
        var setorId = await SementeDoCadastro.SetorAsync(conexao, "Setor sem gestor ainda");

        await using (var define = ContextoDeTeste.Novo(conexao))
        {
            (await define.Setores.SingleAsync(s => s.Id == setorId)).DefinirGestor(idGestor);
            await define.SaveChangesAsync();
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            Assert.Equal(idGestor, (await leitura.Setores.SingleAsync(s => s.Id == setorId)).GestorId);
        }

        await using (var limpa = ContextoDeTeste.Novo(conexao))
        {
            (await limpa.Setores.SingleAsync(s => s.Id == setorId)).DefinirGestor(null);
            await limpa.SaveChangesAsync();
        }

        await using var depois = ContextoDeTeste.Novo(conexao);
        Assert.Null((await depois.Setores.SingleAsync(s => s.Id == setorId)).GestorId);
    }

    [Fact]
    public async Task Trigger_recusa_gestor_igual_ao_funcionario_e_gestor_inativo_com_SQLSTATE_45000()
    {
        // Documenta a rede de segurança do banco: a API valida antes (400/422), mas se algo passar, o trigger recusa.
        var conexao = await BancoAsync();
        var cargoId = await SementeDoCadastro.CargoAsync(conexao, "Cargo dos triggers", 40m);
        var setorId = await SementeDoCadastro.SetorAsync(conexao, "Setor dos triggers");

        async Task<DbUpdateException> TentarAsync(int funcionarioId, int idGestor)
        {
            await using var db = ContextoDeTeste.Novo(conexao);
            var pessoa = await db.Funcionarios.Include(f => f.NivelDeAcesso).SingleAsync(f => f.Id == funcionarioId);
            pessoa.AtualizarCadastro(pessoa.Nome, pessoa.EmailCorporativo!, new DateOnly(1990, 1, 1), pessoa.DataAdmissao, TipoDeContrato.Clt, cargoId, setorId, idGestor, pessoa.NivelDeAcesso!);
            return await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());
        }

        // Gestor igual ao funcionário: a pessoa é gestora e aponta para si mesma.
        var gestora = await NovaPessoaAsync(conexao, "trigger.si.mesma", "Funcionario Gestor");
        var idDela = await TornarGestorAsync(conexao, gestora.Id);
        var erroDeSiMesma = ErroDoMySql(await TentarAsync(gestora.Id, idDela));
        Assert.Equal("45000", erroDeSiMesma.SqlState);
        Assert.Contains("gestor de si mesmo", erroDeSiMesma.Message, StringComparison.Ordinal);

        // Gestor inativo: o gestor é desligado depois de cadastrado e outra pessoa tenta apontar para ele.
        var aposentado = await NovaPessoaAsync(conexao, "trigger.gestor.inativo", "Funcionario Gestor");
        var idDoAposentado = await TornarGestorAsync(conexao, aposentado.Id);
        await ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {aposentado.Id}");
        var outra = await NovaPessoaAsync(conexao, "trigger.sobre.inativo");
        var erroDeInativo = ErroDoMySql(await TentarAsync(outra.Id, idDoAposentado));
        Assert.Equal("45000", erroDeInativo.SqlState);
        Assert.Contains("funcionario inativo", erroDeInativo.Message, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Semente_DefinirSenhaAsync_grava_o_hash_que_o_hasher_de_verdade_confere()
    {
        var conexao = await BancoAsync();
        var pessoa = await AuditoriaDeTeste.NovaPessoaAsync(conexao, "Pessoa da senha", "pessoa.da.senha@empresa.com.br", "Funcionario");

        await SementeDoCadastro.DefinirSenhaAsync(conexao, pessoa, "Segredo#123");

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var usuario = await leitura.Usuarios.SingleAsync(u => u.Id == pessoa.UsuarioId);
        var hasher = new SenhaHasher();
        Assert.True(hasher.Verificar(usuario.SenhaHash, "Segredo#123"));
        Assert.False(hasher.Verificar(usuario.SenhaHash, "outra senha"));
        Assert.False(usuario.SenhaProvisoria);
    }
}
