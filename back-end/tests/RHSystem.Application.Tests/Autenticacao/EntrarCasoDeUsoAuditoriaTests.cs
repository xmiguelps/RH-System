using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Autenticacao;

/// <summary>
/// Os eventos de acesso do login (Task 8.2): explícitos, e sempre DEPOIS de salvar o último login e o refresh token, fora de transação (a regra de qual
/// conexão grava, em <c>IAuditor</c>). O auditor é um falso que só anota as chamadas.
/// </summary>
public class EntrarCasoDeUsoAuditoriaTests
{
    private static readonly AtorDoEvento Ana = new(1, "Ana Souza", Perfil.RH);

    [Fact]
    public async Task Entrar_bem_sucedido_registra_Acesso_Entrou_com_o_ator_informado()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario();

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Null(resultado.Falha);
        var chamada = Assert.Single(cenario.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.RegistrarEGravarComoAsync), chamada.Metodo);
        Assert.Equal(Ana, chamada.Ator);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Entrou, "Entrou no portal", "Colaborador Ana Souza"), chamada.Evento);
        Assert.Equal(1, chamada.SalvarChamadasNaHora); // depois de salvar o último login e o refresh token
        Assert.False(chamada.EmTransacaoNaHora); // e fora de transação
    }

    [Fact]
    public async Task Entrar_com_acesso_desativado_ou_sem_perfil_registra_AcessoNegado()
    {
        var desativado = new Cenario();
        desativado.NovoUsuario(usuarioAtivo: false);
        var semPerfil = new Cenario();
        semPerfil.NovoUsuario(nivel: null);

        var r1 = await desativado.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);
        var r2 = await semPerfil.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.UsuarioInativo, r1.Falha);
        var bloqueado = Assert.Single(desativado.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.RegistrarEGravarComoAsync), bloqueado.Metodo);
        Assert.Equal(Ana, bloqueado.Ator);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login com usuário bloqueado", "Colaborador Ana Souza"), bloqueado.Evento);

        Assert.Equal(FalhaDeEntrada.PerfilNaoConfigurado, r2.Falha);
        var sem = Assert.Single(semPerfil.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.RegistrarEGravarComoAsync), sem.Metodo);
        Assert.Equal(Ana with { Perfil = null }, sem.Ator);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login sem perfil de acesso configurado", "Colaborador Ana Souza"), sem.Evento);
    }

    [Fact]
    public async Task Entrar_com_senha_errada_de_usuario_que_existe_registra_AcessoNegado_pelo_metodo_com_limite_de_1_minuto()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario();

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "senha errada"), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
        var chamada = Assert.Single(cenario.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.RegistrarEGravarComLimiteAsync), chamada.Metodo);
        Assert.Equal(TimeSpan.FromMinutes(1), chamada.Janela);
        Assert.Equal(Ana, chamada.Ator);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.AcessoNegado, "Tentativa de login com senha incorreta", "Colaborador Ana Souza"), chamada.Evento);
        Assert.DoesNotContain("senha errada", $"{chamada.Evento}"); // a senha digitada nunca vai para o evento
    }

    [Fact]
    public async Task Entrar_com_email_inexistente_nao_registra_nada()
    {
        var cenario = new Cenario();
        cenario.NovoUsuario();

        var resultado = await cenario.Entrar().ExecutarAsync(new EntrarComando("quem@nao.existe", "qualquer"), CancellationToken.None);

        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, resultado.Falha);
        Assert.Empty(cenario.Auditor.Chamadas);
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_de_acesso_nao_derruba_a_entrada_nem_a_recusa_e_fica_no_log()
    {
        // Decisão desta tarefa: o evento de acesso é registro, não condição. Se a auditoria falhar, o login que já foi salvo continua valendo, a
        // recusa continua sendo a mesma recusa, e a falha aparece no log (nível Error) para alguém ver.
        var entrada = new Cenario();
        entrada.NovoUsuario();
        entrada.Auditor.Falhar = true;
        var recusa = new Cenario();
        recusa.NovoUsuario();
        recusa.Auditor.Falhar = true;

        var bem = await entrada.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", Cenario.SenhaCerta), CancellationToken.None);
        var mal = await recusa.Entrar().ExecutarAsync(new EntrarComando("ana@empresa.com.br", "senha errada"), CancellationToken.None);

        Assert.Null(bem.Falha);
        Assert.NotNull(bem.Sessao);
        Assert.Equal(FalhaDeEntrada.CredenciaisInvalidas, mal.Falha);
        Assert.Contains(entrada.LogDaEntrada.Registros, r => r.Nivel == LogLevel.Error);
        Assert.Contains(recusa.LogDaEntrada.Registros, r => r.Nivel == LogLevel.Error);
    }
}
