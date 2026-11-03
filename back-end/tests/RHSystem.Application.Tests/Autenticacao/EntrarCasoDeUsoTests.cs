using Microsoft.Extensions.Logging;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Autenticacao;

public class EntrarCasoDeUsoTests
{
    [Fact]
    public async Task Entrar_com_email_em_maiusculas_e_espacos_autentica_o_mesmo_usuario() // Review Focus 1
    {
        var cenario = new Cenario();
        cenario.NovoUsuario("ana@empresa.com.br");

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("  ANA@Empresa.com.BR  ", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Null(resultado.Falha);
        Assert.Equal("ana@empresa.com.br", resultado.Sessao!.Usuario.Email);
    }

    [Theory]
    [InlineData("ana@empresa.com.br", "senha errada")]
    [InlineData("quem@nao.existe", Cenario.SenhaCerta)]
    public async Task Entrar_com_senha_errada_ou_email_inexistente_devolve_falha_CredenciaisInvalidas(string email, string senha)
    {
        var cenario = new Cenario();
        cenario.NovoUsuario("ana@empresa.com.br");

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando(email, senha), CancellationToken.None);

        Assert.Null(resultado.Sessao);
        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
        Assert.Empty(cenario.Tokens.Todos);
    }

    [Fact]
    public async Task Entrar_com_email_inexistente_ainda_chama_Verificar_do_hasher() // contra enumeração por tempo
    {
        var cenario = new Cenario();

        await cenario.Entrar().ExecutarAsync(new EntrarComando("quem@nao.existe", "qualquer"), CancellationToken.None);

        Assert.Equal(1, cenario.Hasher.VerificarChamadas);
        Assert.True(cenario.Hasher.GerarChamadas >= 1, "o hash fictício deve vir do mesmo ISenhaHasher (mesmo custo)");
    }

    [Fact]
    public async Task Entrar_com_hash_ficticio_e_gerado_uma_vez_so_por_hasher()
    {
        var cenario = new Cenario();
        var caso = cenario.Entrar();

        await caso.ExecutarAsync(new EntrarComando("a@nao.existe", "x"), CancellationToken.None);
        var geradosDepoisDaPrimeira = cenario.Hasher.GerarChamadas;
        await caso.ExecutarAsync(new EntrarComando("b@nao.existe", "x"), CancellationToken.None);
        await cenario.Entrar().ExecutarAsync(new EntrarComando("c@nao.existe", "x"), CancellationToken.None);

        Assert.Equal(geradosDepoisDaPrimeira, cenario.Hasher.GerarChamadas);
        Assert.Equal(3, cenario.Hasher.VerificarChamadas);
    }

    [Fact]
    public async Task Entrar_com_usuario_inativo_e_senha_correta_devolve_falha_UsuarioInativo()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario(usuarioAtivo: false);

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.UsuarioInativo, resultado.Falha);
        Assert.Null(resultado.Sessao);
    }

    [Fact]
    public async Task Entrar_com_funcionario_inativo_e_senha_correta_devolve_falha_UsuarioInativo()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario(funcionarioAtivo: false);

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.UsuarioInativo, resultado.Falha);
    }

    [Fact]
    public async Task Entrar_com_usuario_inativo_e_senha_errada_devolve_falha_CredenciaisInvalidas()
    {
        // A mesma resposta de quem não existe: só quem sabe a senha descobre que a conta está desativada.
        var cenario = new Cenario();
        cenario.NovoUsuario(usuarioAtivo: false);

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "senha errada"), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("Administrador")]
    public async Task Entrar_com_funcionario_sem_nivel_ou_com_nivel_desconhecido_e_senha_correta_devolve_falha_PerfilNaoConfigurado(string? nivel)
    {
        var cenario = new Cenario();
        cenario.NovoUsuario(nivel: nivel);

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.PerfilNaoConfigurado, resultado.Falha);
        Assert.Null(resultado.Sessao);
        Assert.Contains(cenario.LogDaEntrada.Registros, r => r.Nivel == LogLevel.Warning);
        Assert.Empty(cenario.Tokens.Todos);
    }

    [Fact]
    public async Task Entrar_registra_UltimoLoginEm_e_guarda_apenas_o_hash_do_refresh()
    {
        var cenario = new Cenario(); // relógio em 2026-10-07T12:42:00.700Z
        var usuario = cenario.NovoUsuario();

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), usuario.UltimoLoginEm); // truncado ao segundo
        var sessao = resultado.Sessao!;
        var guardado = Assert.Single(cenario.Tokens.Todos);
        Assert.Equal(cenario.Emissor.HashDoRefresh(sessao.RefreshToken), guardado.TokenHash);
        Assert.NotEqual(sessao.RefreshToken, guardado.TokenHash); // o token em si nunca é guardado
        Assert.Equal(usuario.Id, guardado.UsuarioId);
        Assert.Equal(cenario.Relogio.AgoraUtc.AddDays(7), guardado.ExpiraEm);
        Assert.Equal(1, cenario.Unidade.SalvarChamadas);
        Assert.Equal("jwt-do-usuario-" + usuario.Id, sessao.TokenDeAcesso);
        Assert.Equal(cenario.Relogio.AgoraUtc.AddMinutes(15), sessao.AcessoExpiraEmUtc);
    }

    [Fact]
    public async Task Entrar_devolve_o_nome_do_cargo_e_do_setor_e_null_quando_o_funcionario_nao_tem()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario("com@empresa.com.br", nivel: "Funcionario Gestor", cargo: "Analista de RH", setor: "Recursos Humanos");
        cenario.NovoUsuario("sem@empresa.com.br");

        var com = await cenario.Entrar().ExecutarAsync(new EntrarComando("com@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);
        var sem = await cenario.Entrar().ExecutarAsync(new EntrarComando("sem@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal("Analista de RH", com.Sessao!.Usuario.Cargo);
        Assert.Equal("Recursos Humanos", com.Sessao.Usuario.Setor);
        Assert.Equal(Perfil.Gestor, com.Sessao.Usuario.Perfil);
        Assert.Null(sem.Sessao!.Usuario.Cargo);
        Assert.Null(sem.Sessao.Usuario.Setor);
        Assert.Equal(Perfil.RH, sem.Sessao.Usuario.Perfil);
    }
}
