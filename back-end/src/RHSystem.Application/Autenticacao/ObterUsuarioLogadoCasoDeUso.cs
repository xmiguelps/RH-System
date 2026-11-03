namespace RHSystem.Application.Autenticacao;

/// <summary>
/// Quem está logado, com cargo e setor (que não vão dentro do JWT). Devolve <c>null</c> se a pessoa não existe mais, foi inativada ou perdeu
/// o nível de acesso: o token ainda vale por alguns minutos, mas a API deixa de tratá-la como logada.
/// </summary>
public sealed class ObterUsuarioLogadoCasoDeUso(IUsuarioRepositorio usuarios)
{
    public async Task<UsuarioResposta?> ExecutarAsync(int usuarioId, CancellationToken ct)
    {
        var usuario = await usuarios.ObterPorIdAsync(usuarioId, ct);
        var perfil = usuario?.Funcionario.Perfil;
        if (usuario is null || !usuario.PodeEntrar || perfil is null)
        {
            return null;
        }

        return Mapeamento.Usuario(usuario, perfil.Value);
    }
}
