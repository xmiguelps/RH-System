using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Acesso;

/// <summary>"Esqueci minha senha": valida o e-mail e pede o link. A resposta da rota é sempre a mesma (202): nada aqui revela se o e-mail existe.</summary>
public sealed class SolicitarRedefinicaoDeSenhaCasoDeUso(IServicoDeConvite servico)
{
    public async Task ExecutarAsync(ConviteEsqueciSenhaComando comando, CancellationToken ct)
    {
        var email = (comando.Email ?? string.Empty).Trim().ToLowerInvariant();
        var erros = new ColetorDeErros();
        if (RegrasDeSenha.Obrigatorio(erros, "email", email) && !RegrasDeSenha.EmailComFormatoValido(email))
        {
            erros.Adicionar("email", "Informe um e-mail válido.");
        }

        erros.LancarSeHouver();
        await servico.SolicitarRedefinicaoAsync(email, ct);
    }
}

/// <summary>Confere se o link ainda vale (e diz de que tipo é, para o front escolher o texto) sem gastá-lo.</summary>
public sealed class ValidarLinkDeSenhaCasoDeUso(IGeradorDeTokenDeSenha gerador, ILinksDeSenha links, IRelogio relogio)
{
    public async Task<ConviteLinkInfo> ExecutarAsync(ConviteLinkValidarComando comando, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        RegrasDeSenha.Obrigatorio(erros, "token", comando.Token);
        erros.LancarSeHouver();

        var link = await links.ObterValidoAsync(gerador.Hash(comando.Token.Trim()), relogio.AgoraUtc, ct) ?? throw ErrosDeAcesso.LinkInvalido();
        return new ConviteLinkInfo(link.Tipo == TipoDeTokenDeSenha.Convite ? ConviteLinkTipo.Convite : ConviteLinkTipo.Redefinicao, link.Nome, link.EmailCorporativo, link.ExpiraEmUtc);
    }
}

/// <summary>
/// Define a senha pelo link do convite ou da redefinição, numa transação: consome o link (uma linha afetada; dois usos simultâneos, só um passa), grava o hash, invalida os outros
/// links em aberto da pessoa, encerra as sessões dela e registra o evento (a pessoa como ator: a requisição é anônima) NA MESMA transação e no mesmo <c>SalvarAsync</c>: se o evento
/// falhar, nada disso fica. Não há login automático.
/// </summary>
public sealed class DefinirSenhaPeloLinkCasoDeUso(
    IGeradorDeTokenDeSenha gerador,
    ILinksDeSenha links,
    IUsuarioRepositorio usuarios,
    ISenhaHasher hasher,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task ExecutarAsync(ConviteDefinirSenhaComando comando, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        RegrasDeSenha.Obrigatorio(erros, "token", comando.Token);
        RegrasDeSenha.Validar(erros, "novaSenha", comando.NovaSenha);
        erros.LancarSeHouver();

        var tokenHash = gerador.Hash(comando.Token.Trim());
        var agora = relogio.AgoraUtc;
        var link = await links.ObterValidoAsync(tokenHash, agora, ct) ?? throw ErrosDeAcesso.LinkInvalido();
        var novoHash = hasher.Gerar(comando.NovaSenha); // o custo do hash fica fora da transação

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await links.TentarConsumirAsync(tokenHash, agora, ct))
                {
                    throw ErrosDeAcesso.LinkInvalido(); // outro uso passou na frente
                }

                // Conferido de novo dentro da transação: a pessoa pode ter sido inativada entre a conferência e o uso. Lançar aqui desfaz o consumo do link.
                var usuario = await usuarios.ObterPorIdAsync(link.UsuarioId, ct) ?? throw ErrosDeAcesso.LinkInvalido();
                if (!usuario.PodeEntrar || (link.Tipo == TipoDeTokenDeSenha.Convite && !usuario.SenhaProvisoria))
                {
                    throw ErrosDeAcesso.LinkInvalido();
                }

                usuario.DefinirSenha(novoHash);
                await links.InvalidarOutrosDaPessoaAsync(usuario.Id, agora, ct);
                await tokens.RevogarTodosDoUsuarioAsync(usuario.Id, agora, ct);
                var funcionario = usuario.Funcionario;
                auditor.RegistrarComo(
                    new AtorDoEvento(usuario.FuncionarioId, funcionario.Nome, funcionario.Perfil),
                    new EventoDeAuditoriaNovo(
                        AuditoriaArea.Acesso,
                        AuditoriaAcao.Alterou,
                        link.Tipo == TipoDeTokenDeSenha.Convite ? "Senha definida (convite)" : "Senha definida (redefinição)",
                        $"Colaborador {funcionario.Nome}",
                        "USUARIOS",
                        usuario.Id));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);
    }
}

/// <summary>
/// A troca de senha de quem está logado. Ordem: (1) campos (400, não conta como tentativa); (2) o limite, ANTES de conferir a senha (429 mesmo com a senha certa); (3) a senha atual
/// (422 <c>SENHA_ATUAL_INCORRETA</c>, conta um erro); (4) nova igual à atual (400 em <c>novaSenha</c>); (5) grava o hash, encerra as OUTRAS sessões (a sessão atual é a do
/// <c>rth</c>; sem ele, todas) e registra o evento, tudo na mesma transação.
/// </summary>
public sealed class TrocarMinhaSenhaCasoDeUso(
    IAtorAtual ator,
    IUsuarioRepositorio usuarios,
    ISenhaHasher hasher,
    ILimiteDeSenhaAtual limite,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task ExecutarAsync(SenhaTrocarComando comando, string? hashDaSessaoAtual, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        RegrasDeSenha.Obrigatorio(erros, "senhaAtual", comando.SenhaAtual);
        RegrasDeSenha.Validar(erros, "novaSenha", comando.NovaSenha);
        erros.LancarSeHouver();

        var usuarioId = ator.UsuarioId ?? throw ErrosDeAcesso.NaoAutenticado();
        if (limite.Bloqueio(usuarioId) is { } espera)
        {
            throw ErrosDeAcesso.MuitasTentativas(espera);
        }

        // O token ainda vale por minutos depois de a pessoa ser inativada: quem não pode mais entrar também não troca a senha.
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct);
        if (usuario is null || !usuario.PodeEntrar)
        {
            throw ErrosDeAcesso.NaoAutenticado();
        }

        if (!hasher.Verificar(usuario.SenhaHash, comando.SenhaAtual))
        {
            limite.RegistrarErro(usuarioId);
            throw new ErroDeNegocioException("SENHA_ATUAL_INCORRETA", "A senha atual está incorreta.");
        }

        if (comando.NovaSenha == comando.SenhaAtual)
        {
            throw new ErroDeValidacaoException("novaSenha", "A nova senha precisa ser diferente da atual.");
        }

        var novoHash = hasher.Gerar(comando.NovaSenha);
        var agora = relogio.AgoraUtc;
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                usuario.DefinirSenha(novoHash);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Alterou, "Senha trocada", $"Colaborador {usuario.Funcionario.Nome}", "USUARIOS", usuario.Id));
                await unidade.SalvarAsync(ct);
                await tokens.RevogarOutrosDoUsuarioAsync(usuarioId, hashDaSessaoAtual, agora, ct);
                return 0;
            },
            ct);
    }
}
