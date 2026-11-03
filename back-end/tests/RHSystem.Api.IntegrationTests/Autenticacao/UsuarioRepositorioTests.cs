using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Autenticacao;

[Collection(MySqlCollection.Nome)]
public class UsuarioRepositorioTests(MySqlFixture mysql)
{
    // O trigger de USUARIOS recusa senha com menos de 40 caracteres: um valor do tamanho de um BCrypt.
    private const string HashDeTeste = "$2a$11$abcdefghijklmnopqrstuvwxyzABCDEFGHIJKLMNOPQRSTUVWXYZ0123456";

    private static async Task ExecutarAsync(string conexao, string sql)
    {
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(sql, c);
        await comando.ExecuteNonQueryAsync();
    }

    [Fact]
    public async Task UsuarioRepositorio_acha_pelo_email_corporativo_ignorando_maiusculas_e_traz_o_perfil()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var nivel = await escrita.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
            var funcionario = Funcionario.Criar("Ana Souza", "ana@empresa.com.br", new DateOnly(2026, 1, 2), nivel);
            escrita.Usuarios.Add(Usuario.Criar(funcionario, HashDeTeste));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var repositorio = new UsuarioRepositorio(leitura);

        var usuario = await repositorio.ObterPorEmailCorporativoAsync("ANA@Empresa.com.BR", CancellationToken.None);

        Assert.NotNull(usuario);
        Assert.Equal("Ana Souza", usuario.Funcionario.Nome);
        Assert.Equal(Perfil.RH, usuario.Funcionario.Perfil);
        Assert.Null(await repositorio.ObterPorEmailCorporativoAsync("outra@empresa.com.br", CancellationToken.None));

        var pelaChave = await repositorio.ObterPorIdAsync(usuario.Id, CancellationToken.None);
        Assert.Equal(usuario.Id, pelaChave!.Id);
        Assert.Equal(Perfil.RH, pelaChave.Funcionario.Perfil);
        Assert.Null(await repositorio.ObterPorIdAsync(usuario.Id + 1000, CancellationToken.None));
    }

    [Fact]
    public async Task UsuarioRepositorio_traz_o_usuario_mesmo_quando_o_funcionario_nao_tem_nivel()
    {
        // A navegação é opcional: nada de INNER JOIN que esconda o usuário (o login responde "perfil não configurado", e não "não existe").
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await ExecutarAsync(conexao, "INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO) VALUES ('Sem Nivel', '2026-01-02', TRUE, 'sem.nivel@empresa.com.br')");
        await ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) SELECT ID_FUNCIONARIO, '{HashDeTeste}' FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = 'sem.nivel@empresa.com.br'");
        await using var db = ContextoDeTeste.Novo(conexao);

        var usuario = await new UsuarioRepositorio(db).ObterPorEmailCorporativoAsync("sem.nivel@empresa.com.br", CancellationToken.None);

        Assert.NotNull(usuario);
        Assert.Null(usuario.Funcionario.NivelDeAcesso);
        Assert.Null(usuario.Funcionario.Perfil);
    }

    [Fact]
    public async Task UsuarioRepositorio_traz_cargo_e_setor_quando_existem_e_null_quando_o_funcionario_nao_tem()
    {
        // Cargo e Setor também são opcionais (ID_CARGO e ID_SETOR aceitam NULL): sem INNER JOIN.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var nivel = await escrita.NiveisDeAcesso.SingleAsync(n => n.Nome == "Funcionario Gestor");
            escrita.Usuarios.Add(Usuario.Criar(Funcionario.Criar("Com Cargo", "com@empresa.com.br", new DateOnly(2026, 1, 2), nivel), HashDeTeste));
            escrita.Usuarios.Add(Usuario.Criar(Funcionario.Criar("Sem Cargo", "sem@empresa.com.br", new DateOnly(2026, 1, 2), nivel), HashDeTeste));
            await escrita.SaveChangesAsync();
        }

        await ExecutarAsync(conexao, "INSERT INTO CARGOS (NOME_CARGO) VALUES ('Analista de RH')");
        await ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Recursos Humanos')");
        await ExecutarAsync(conexao, "UPDATE FUNCIONARIOS SET ID_CARGO = (SELECT ID_CARGO FROM CARGOS LIMIT 1), ID_SETOR = (SELECT ID_SETOR FROM SETORES LIMIT 1) WHERE EMAIL_CORPORATIVO = 'com@empresa.com.br'");

        await using var db = ContextoDeTeste.Novo(conexao);
        var repositorio = new UsuarioRepositorio(db);

        var com = await repositorio.ObterPorEmailCorporativoAsync("com@empresa.com.br", CancellationToken.None);
        var sem = await repositorio.ObterPorEmailCorporativoAsync("sem@empresa.com.br", CancellationToken.None);

        Assert.Equal("Analista de RH", com!.Funcionario.Cargo!.Nome);
        Assert.Equal("Recursos Humanos", com.Funcionario.Setor!.Nome);
        Assert.Equal(Perfil.Gestor, com.Funcionario.Perfil);
        Assert.NotNull(sem);
        Assert.Null(sem.Funcionario.Cargo);
        Assert.Null(sem.Funcionario.Setor);

        // O mesmo vale para a busca pelo id (usada na renovação).
        await using var outro = ContextoDeTeste.Novo(conexao);
        var pelaChave = await new UsuarioRepositorio(outro).ObterPorIdAsync(com.Id, CancellationToken.None);
        Assert.Equal("Analista de RH", pelaChave!.Funcionario.Cargo!.Nome);
    }
}
