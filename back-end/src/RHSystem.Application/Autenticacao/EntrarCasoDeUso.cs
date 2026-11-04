using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Autenticacao;

/// <summary>
/// Entrar com e-mail e senha. A senha é verificada ANTES de olhar se a conta está ativa: só quem sabe a senha descobre que a conta foi
/// desativada (inativo com senha errada é "credenciais inválidas", igual a e-mail inexistente).
/// <para>
/// Eventos de acesso (Acesso): login bem-sucedido (<c>Entrou</c>, depois de salvar o último login e o refresh token) e as recusas de quem existe
/// (<c>AcessoNegado</c>: senha errada, no máximo um por pessoa por minuto; conta bloqueada; sem perfil). E-mail que não existe NÃO grava nada: não há
/// pessoa para ser o ator, e assim o log não guarda e-mail digitado errado. Todos saem fora de transação, por <c>RegistrarEGravar*</c>. Nunca levam a senha.
/// Se a gravação do evento falhar, a entrada (ou a recusa) segue como estava e a falha vai para o log em nível Error: o evento é registro, não condição.
/// </para>
/// </summary>
public sealed class EntrarCasoDeUso(
    IUsuarioRepositorio usuarios,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    ISenhaHasher hasher,
    IEmissorDeTokens emissor,
    IRelogio relogio,
    IAuditor auditor,
    ILogger<EntrarCasoDeUso> log)
{
    private static readonly TimeSpan JanelaDaSenhaErrada = TimeSpan.FromMinutes(1);

    // Um hash fictício por hasher, gerado uma vez (e pelo mesmo hasher, para ter o mesmo custo de uma verificação de verdade).
    private static readonly ConditionalWeakTable<ISenhaHasher, string> HashesFicticios = new();

    public async Task<ResultadoDeEntrada> ExecutarAsync(EntrarComando comando, CancellationToken ct)
    {
        var email = comando.Email.Trim().ToLowerInvariant();
        var usuario = await usuarios.ObterPorEmailCorporativoAsync(email, ct);

        if (usuario is null)
        {
            // Sem esta verificação o tempo da resposta revelaria quais e-mails existem.
            hasher.Verificar(HashesFicticios.GetValue(hasher, h => h.Gerar(Guid.NewGuid().ToString("N"))), comando.Senha);
            return Falha(FalhaDeEntrada.CredenciaisInvalidas);
        }

        if (!hasher.Verificar(usuario.SenhaHash, comando.Senha))
        {
            // A pessoa existe: um evento por pessoa por minuto (a tentativa em massa contra uma conta não inunda o log). Sem a senha digitada.
            await RegistrarAsync(usuario, AuditoriaAcao.AcessoNegado, "Tentativa de login com senha incorreta", comLimite: true, ct);
            return Falha(FalhaDeEntrada.CredenciaisInvalidas);
        }

        if (!usuario.PodeEntrar)
        {
            await RegistrarAsync(usuario, AuditoriaAcao.AcessoNegado, "Tentativa de login com usuário bloqueado", comLimite: false, ct);
            return Falha(FalhaDeEntrada.UsuarioInativo);
        }

        var perfil = usuario.Funcionario.Perfil;
        if (perfil is null)
        {
            log.LogWarning(
                "O funcionário {FuncionarioId} tem usuário ativo e senha certa, mas o nível de acesso não está configurado (sem nível ou nome desconhecido).",
                usuario.FuncionarioId);
            await RegistrarAsync(usuario, AuditoriaAcao.AcessoNegado, "Tentativa de login sem perfil de acesso configurado", comLimite: false, ct);
            return Falha(FalhaDeEntrada.PerfilNaoConfigurado);
        }

        var agora = relogio.AgoraUtc;
        usuario.RegistrarLogin(agora);
        var acesso = emissor.EmitirAcesso(usuario);
        var refresh = emissor.NovoRefresh();
        await tokens.AdicionarAsync(RefreshToken.Criar(usuario.Id, refresh.Hash, agora, emissor.ValidadeDoRefresh), ct);
        await unidade.SalvarAsync(ct);

        // Depois de salvar o último login e o refresh token, e fora de transação: o evento grava em contexto próprio.
        await RegistrarAsync(usuario, AuditoriaAcao.Entrou, "Entrou no portal", comLimite: false, ct);
        return new ResultadoDeEntrada(Mapeamento.Sessao(usuario, perfil.Value, acesso, refresh), null);
    }

    private static ResultadoDeEntrada Falha(FalhaDeEntrada falha) => new(null, falha);

    /// <summary>O ator é a pessoa dona do e-mail (a requisição ainda é anônima). Falha de gravação não derruba a entrada nem a recusa: vai para o log.</summary>
    private async Task RegistrarAsync(Usuario usuario, AuditoriaAcao acao, string descricao, bool comLimite, CancellationToken ct)
    {
        try
        {
            var funcionario = usuario.Funcionario;
            var ator = new AtorDoEvento(usuario.FuncionarioId, funcionario.Nome, funcionario.Perfil);
            var evento = new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, acao, descricao, $"Colaborador {funcionario.Nome}");
            if (comLimite)
            {
                await auditor.RegistrarEGravarComLimiteAsync(ator, evento, JanelaDaSenhaErrada, ct);
            }
            else
            {
                await auditor.RegistrarEGravarComoAsync(ator, evento, ct);
            }
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (Exception erro)
        {
            log.LogError(
                erro,
                "Não foi possível gravar o evento de acesso {Acao} do funcionário {FuncionarioId}; o resultado da entrada não muda.",
                acao,
                usuario.FuncionarioId);
        }
    }
}
