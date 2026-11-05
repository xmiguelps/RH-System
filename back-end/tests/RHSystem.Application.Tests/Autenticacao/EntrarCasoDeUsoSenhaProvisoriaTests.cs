using RHSystem.Application.Acesso;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Autenticacao;

/// <summary>Quem recebeu o convite e ainda não definiu a senha não entra, e nada na resposta nem no tempo diz que o convite está pendente.</summary>
public class EntrarCasoDeUsoSenhaProvisoriaTests
{
    private static readonly AtorDoEvento Ana = new(1, "Ana Souza", Perfil.RH);

    [Fact]
    public async Task Entrar_com_login_de_senha_provisoria_devolve_CredenciaisInvalidas_e_ainda_chama_Verificar_do_hasher()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario(senhaProvisoria: true);

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "qualquer senha"), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
        Assert.Null(resultado.Sessao);
        // Sem esta verificação (contra um hash fictício, de mesmo custo) o tempo da resposta separaria "convite pendente" de "senha errada".
        Assert.Equal(1, cenario.Hasher.VerificarChamadas);
    }

    [Fact]
    public async Task Entrar_com_senha_provisoria_e_senha_certa_para_o_hash_provisorio_nao_entra()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario(senhaProvisoria: true);
        // O hasher falso confere "hash:{senha}": o hash que a conta guarda seria conferido com a senha certa. Com a marca de provisória, nem chega a ser conferido.
        Assert.True(cenario.Hasher.Verificar(usuario.SenhaHash, Cenario.SenhaCerta));
        var antes = cenario.Hasher.VerificarChamadas;

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
        Assert.Empty(cenario.Tokens.Todos);
        Assert.Equal(0, cenario.Unidade.SalvarChamadas); // nada foi gravado: nem último login nem refresh token
        Assert.Equal(1, cenario.Hasher.VerificarChamadas - antes); // só a verificação contra o hash fictício
    }

    [Fact]
    public async Task Entrar_com_login_de_senha_provisoria_registra_AcessoNegado_pelo_metodo_com_limite_de_1_minuto_e_devolve_a_mesma_falha_da_senha_errada()
    {
        var provisoria = new Cenario();
        provisoria.NovoUsuario(senhaProvisoria: true);
        var senhaErrada = new Cenario();
        senhaErrada.NovoUsuario();

        var daProvisoria = await provisoria.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "senha errada"), CancellationToken.None);
        var daSenhaErrada = await senhaErrada.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "senha errada"), CancellationToken.None);

        Assert.Equal(daSenhaErrada, daProvisoria); // a mesma falha, sem sessão
        var chamada = Assert.Single(provisoria.Auditor.Chamadas);
        var chamadaDaSenhaErrada = Assert.Single(senhaErrada.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.RegistrarEGravarComLimiteAsync), chamada.Metodo);
        Assert.Equal(TimeSpan.FromMinutes(1), chamada.Janela);
        Assert.Equal(Ana, chamada.Ator);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login com senha incorreta", "Colaborador Ana Souza"), chamada.Evento);
        Assert.Equal((chamadaDaSenhaErrada.Metodo, chamadaDaSenhaErrada.Janela, chamadaDaSenhaErrada.Ator, chamadaDaSenhaErrada.Evento), (chamada.Metodo, chamada.Janela, chamada.Ator, chamada.Evento));
        Assert.False(chamada.EmTransacaoNaHora);
    }

    [Fact]
    public async Task Entrar_com_senha_ja_definida_continua_igual()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario();

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Null(resultado.Falha);
        Assert.NotNull(resultado.Sessao);
    }

    [Fact]
    public void O_hash_provisorio_do_cenario_nao_e_o_hash_da_senha_certa()
    {
        // Controle do cenário: o usuário provisório do teste usa o hash que a produção usa (HashProvisorio), não o da senha certa.
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario(senhaProvisoria: true, hashProvisorio: HashProvisorio.Gerar());

        Assert.False(cenario.Hasher.Verificar(usuario.SenhaHash, Cenario.SenhaCerta));
        Assert.True(usuario.SenhaProvisoria);
    }
}
