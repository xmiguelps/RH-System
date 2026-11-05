using RHSystem.Application.Acesso;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Application.Tests.Falsos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Tests.Acesso;

public class SenhaCasosDeUsoTests
{
    private sealed class AtorFalso(int? usuarioId) : IAtorAtual
    {
        public TipoDeAtorAtual Tipo => TipoDeAtorAtual.Pessoa;

        public int? UsuarioId => usuarioId;

        public int? FuncionarioId => 1;

        public Perfil? Perfil => Domain.Autenticacao.Perfil.RH;

        public string Nome => "Ana Souza";

        public string? Ip => "10.0.0.7";

        public CanalDeOrigem Canal => CanalDeOrigem.Portal;
    }

    private sealed class LimiteFalso : ILimiteDeSenhaAtual
    {
        public TimeSpan? Bloqueado { get; set; }

        public List<int> Erros { get; } = [];

        public TimeSpan? Bloqueio(int usuarioId) => Bloqueado;

        public void RegistrarErro(int usuarioId) => Erros.Add(usuarioId);
    }

    private static (Cenario Cenario, Usuario Usuario, LimiteFalso Limite, TrocarMinhaSenhaCasoDeUso Caso) Montar()
    {
        var cenario = new Cenario();
        var usuario = cenario.NovoUsuario();
        var limite = new LimiteFalso();
        var caso = new TrocarMinhaSenhaCasoDeUso(new AtorFalso(usuario.Id), cenario.Usuarios, cenario.Hasher, limite, cenario.Tokens, cenario.Unidade, cenario.Auditor, cenario.Relogio);
        return (cenario, usuario, limite, caso);
    }

    [Theory]
    [InlineData(null, false)]
    [InlineData("", false)]
    [InlineData("        ", false)] // só espaços não é senha
    [InlineData("1234567", false)] // 7
    [InlineData("12345678", true)] // 8
    [InlineData("simples sem maiuscula nem simbolo", true)] // sem exigir maiúscula, número ou símbolo
    public void Senha_de_7_ou_129_caracteres_e_vazia_dao_400_em_novaSenha_e_8_e_128_passam(string? senha, bool vale)
    {
        var coletor = new ColetorDeErros();

        RegrasDeSenha.Validar(coletor, "novaSenha", senha);

        Assert.Equal(!vale, coletor.TemErros);
        if (!vale)
        {
            var erro = Assert.Throws<ErroDeValidacaoException>(coletor.LancarSeHouver);
            Assert.Contains("novaSenha", erro.Erros.Keys);
        }
    }

    [Fact]
    public void Senha_de_128_passa_e_de_129_nao()
    {
        var passa = new ColetorDeErros();
        var recusa = new ColetorDeErros();

        RegrasDeSenha.Validar(passa, "novaSenha", new string('a', 128));
        RegrasDeSenha.Validar(recusa, "novaSenha", new string('a', 129));

        Assert.False(passa.TemErros);
        Assert.True(recusa.TemErros);
    }

    [Fact]
    public async Task Trocar_para_a_mesma_senha_da_400_em_novaSenha()
    {
        var (cenario, _, limite, caso) = Montar();

        var erro = await Assert.ThrowsAsync<ErroDeValidacaoException>(() =>
            caso.ExecutarAsync(new SenhaTrocarComando(Cenario.SenhaCerta, Cenario.SenhaCerta), null, CancellationToken.None));

        Assert.Equal(["A nova senha precisa ser diferente da atual."], erro.Erros["novaSenha"]);
        Assert.Empty(limite.Erros); // a senha atual estava certa: não conta como erro de senha atual
        Assert.Equal(0, cenario.Unidade.SalvarChamadas); // nada mudou
        Assert.Equal(0, cenario.Hasher.GerarChamadas - 1); // só o Gerar do cenário (hash da senha certa)
    }

    [Fact]
    public async Task Trocar_com_senha_atual_errada_registra_o_erro_no_limite_e_devolve_422_sem_mudar_nada()
    {
        var (cenario, usuario, limite, caso) = Montar();

        var erro = await Assert.ThrowsAsync<ErroDeNegocioException>(() =>
            caso.ExecutarAsync(new SenhaTrocarComando("errada de proposito", "NovaSenha#2026"), null, CancellationToken.None));

        Assert.Equal(("SENHA_ATUAL_INCORRETA", "A senha atual está incorreta."), (erro.Codigo, erro.Detalhe));
        Assert.Equal([usuario.Id], limite.Erros);
        Assert.Equal(0, cenario.Unidade.SalvarChamadas);
        Assert.Empty(cenario.Auditor.Chamadas);
    }

    [Fact]
    public async Task Trocar_bloqueado_devolve_429_com_Retry_After_arredondado_para_cima_antes_de_conferir_a_senha()
    {
        var (cenario, _, limite, caso) = Montar();
        limite.Bloqueado = TimeSpan.FromSeconds(12.2);

        // Mesmo com a senha atual CERTA: senão o limite não protegeria nada.
        var erro = await Assert.ThrowsAsync<ProblemaDeApiException>(() =>
            caso.ExecutarAsync(new SenhaTrocarComando(Cenario.SenhaCerta, "NovaSenha#2026"), null, CancellationToken.None));

        Assert.Equal((429, "Muitas tentativas.", "Muitas tentativas. Aguarde um minuto e tente de novo."), (erro.Status, erro.Titulo, erro.Detalhe));
        Assert.Equal("13", erro.Cabecalhos!["Retry-After"]);
        Assert.Equal(0, cenario.Hasher.VerificarChamadas); // a senha nem foi conferida
        Assert.Empty(limite.Erros);
    }

    [Fact]
    public async Task Trocar_com_campos_vazios_da_400_por_campo_e_nao_conta_como_tentativa()
    {
        var (_, _, limite, caso) = Montar();

        var erro = await Assert.ThrowsAsync<ErroDeValidacaoException>(() => caso.ExecutarAsync(new SenhaTrocarComando(string.Empty, "curta"), null, CancellationToken.None));

        Assert.Equal(["O senhaAtual é obrigatório."], erro.Erros["senhaAtual"]);
        Assert.Contains("novaSenha", erro.Erros.Keys);
        Assert.Empty(limite.Erros);
    }

    [Fact]
    public async Task Trocar_com_sucesso_grava_o_hash_revoga_so_os_outros_e_registra_o_evento_dentro_da_transacao()
    {
        var (cenario, usuario, _, caso) = Montar();
        var sessaoAtual = cenario.NovoRefreshDoCliente(usuario, "refresh-da-sessao-atual");
        cenario.NovoRefreshDoCliente(usuario, "refresh-de-outro-aparelho");
        var hashDaSessao = cenario.Emissor.HashDoRefresh(sessaoAtual);

        await caso.ExecutarAsync(new SenhaTrocarComando(Cenario.SenhaCerta, "NovaSenha#2026"), hashDaSessao, CancellationToken.None);

        Assert.Equal("hash:NovaSenha#2026", usuario.SenhaHash);
        Assert.False(usuario.SenhaProvisoria);
        Assert.Equal([(usuario.Id, (string?)hashDaSessao)], cenario.Tokens.RevogacoesDeOutros);
        Assert.Null(cenario.Tokens.Todos.Single(t => t.TokenHash == hashDaSessao).RevogadoEm);
        Assert.NotNull(cenario.Tokens.Todos.Single(t => t.TokenHash != hashDaSessao).RevogadoEm);
        var chamada = Assert.Single(cenario.Auditor.Chamadas);
        Assert.Equal(nameof(IAuditor.Registrar), chamada.Metodo); // na mesma conexão e transação do fato
        Assert.True(chamada.EmTransacaoNaHora);
        Assert.Equal(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Alterou, "Senha trocada", "Colaborador Ana Souza", "USUARIOS", usuario.Id), chamada.Evento);
        Assert.DoesNotContain("NovaSenha", $"{chamada.Evento}");
    }

    [Fact]
    public async Task Trocar_sem_hash_de_sessao_revoga_todos()
    {
        var (cenario, usuario, _, caso) = Montar();
        cenario.NovoRefreshDoCliente(usuario, "a");
        cenario.NovoRefreshDoCliente(usuario, "b");

        await caso.ExecutarAsync(new SenhaTrocarComando(Cenario.SenhaCerta, "NovaSenha#2026"), null, CancellationToken.None);

        Assert.All(cenario.Tokens.Todos, t => Assert.NotNull(t.RevogadoEm));
    }
}
