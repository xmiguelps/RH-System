using System.Runtime.CompilerServices;
using Microsoft.Extensions.Logging;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Autenticacao;

/// <summary>
/// Entrar com e-mail e senha. A senha é verificada ANTES de olhar se a conta está ativa: só quem sabe a senha descobre que a conta foi
/// desativada (inativo com senha errada é "credenciais inválidas", igual a e-mail inexistente).
/// </summary>
public sealed class EntrarCasoDeUso(
    IUsuarioRepositorio usuarios,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    ISenhaHasher hasher,
    IEmissorDeTokens emissor,
    IRelogio relogio,
    ILogger<EntrarCasoDeUso> log)
{
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
            return Falha(FalhaDeEntrada.CredenciaisInvalidas);
        }

        if (!usuario.PodeEntrar)
        {
            return Falha(FalhaDeEntrada.UsuarioInativo);
        }

        var perfil = usuario.Funcionario.Perfil;
        if (perfil is null)
        {
            log.LogWarning(
                "O funcionário {FuncionarioId} tem usuário ativo e senha certa, mas o nível de acesso não está configurado (sem nível ou nome desconhecido).",
                usuario.FuncionarioId);
            return Falha(FalhaDeEntrada.PerfilNaoConfigurado);
        }

        var agora = relogio.AgoraUtc;
        usuario.RegistrarLogin(agora);
        var acesso = emissor.EmitirAcesso(usuario);
        var refresh = emissor.NovoRefresh();
        await tokens.AdicionarAsync(RefreshToken.Criar(usuario.Id, refresh.Hash, agora, emissor.ValidadeDoRefresh), ct);
        await unidade.SalvarAsync(ct);

        return new ResultadoDeEntrada(Mapeamento.Sessao(usuario, perfil.Value, acesso, refresh), null);
    }

    private static ResultadoDeEntrada Falha(FalhaDeEntrada falha) => new(null, falha);
}
