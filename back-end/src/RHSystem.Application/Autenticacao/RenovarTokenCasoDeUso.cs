using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Autenticacao;

/// <summary>
/// Troca um refresh token por uma sessão nova (rotação): revoga o antigo e cria o novo na mesma transação. Devolve <c>null</c> para
/// qualquer token inválido (desconhecido, expirado, já revogado, perdeu a corrida) sem dizer qual.
/// </summary>
public sealed class RenovarTokenCasoDeUso(
    IUsuarioRepositorio usuarios,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    IEmissorDeTokens emissor,
    IRelogio relogio)
{
    public async Task<SessaoResposta?> ExecutarAsync(RenovarComando comando, CancellationToken ct)
    {
        var hashAntigo = emissor.HashDoRefresh(comando.RefreshToken);
        var antigo = await tokens.ObterPorHashAsync(hashAntigo, ct);
        var agora = relogio.AgoraUtc;
        if (antigo is null || !antigo.EstaAtivo(agora))
        {
            return null; // um token já revogado NÃO derruba os outros tokens do usuário (outro aparelho, outra aba)
        }

        var usuario = await usuarios.ObterPorIdAsync(antigo.UsuarioId, ct);
        if (usuario is null)
        {
            return null;
        }

        var perfil = usuario.Funcionario.Perfil;
        if (!usuario.PodeEntrar || perfil is null)
        {
            // A pessoa foi inativada (ou perdeu o nível) com sessões abertas: nenhuma delas renova mais.
            await tokens.RevogarTodosDoUsuarioAsync(usuario.Id, agora, ct);
            return null;
        }

        return await unidade.ExecutarEmTransacaoAsync<SessaoResposta?>(
            async () =>
            {
                // Duas renovações simultâneas com o mesmo token: só quem revoga de fato recebe a sessão nova.
                if (!await tokens.TentarRevogarAsync(hashAntigo, agora, ct))
                {
                    return null;
                }

                var acesso = emissor.EmitirAcesso(usuario);
                var novo = emissor.NovoRefresh();
                await tokens.AdicionarAsync(RefreshToken.Criar(usuario.Id, novo.Hash, agora, emissor.ValidadeDoRefresh), ct);
                await unidade.SalvarAsync(ct);
                return Mapeamento.Sessao(usuario, perfil.Value, acesso, novo);
            },
            ct);
    }
}
