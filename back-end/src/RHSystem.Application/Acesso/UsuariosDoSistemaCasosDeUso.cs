using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Acesso;

/// <summary>A lista de "Usuários do sistema". Só o perfil RH. Os erros de paginação e de <c>situacao</c> voltam juntos, num 400 só.</summary>
public sealed class ListarUsuariosDoSistemaCasoDeUso(IConsultaDeUsuariosDoSistema consulta)
{
    /// <exception cref="ErroDeValidacaoException">Algum parâmetro inválido; todos os erros vêm juntos.</exception>
    public async Task<RespostaPaginada<UsuarioSistemaItem>> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? situacao, bool? ativo, CancellationToken ct)
    {
        var erros = new Dictionary<string, string[]>();
        ParametrosDeLista? paginacao = null;
        UsuarioSistemaSituacao? filtroDeSituacao = null;
        try
        {
            paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        }
        catch (ErroDeValidacaoException erro)
        {
            Juntar(erros, erro);
        }

        try
        {
            filtroDeSituacao = EnumsDeConsulta.Ler<UsuarioSistemaSituacao>(situacao, "situacao");
        }
        catch (ErroDeValidacaoException erro)
        {
            Juntar(erros, erro);
        }

        if (erros.Count > 0)
        {
            throw new ErroDeValidacaoException(erros);
        }

        return await consulta.ListarAsync(paginacao!, filtroDeSituacao, ativo, ct);
    }

    private static void Juntar(Dictionary<string, string[]> destino, ErroDeValidacaoException erro)
    {
        foreach (var (campo, mensagens) in erro.Erros)
        {
            destino[campo] = mensagens;
        }
    }
}

/// <summary>
/// Envia ou reenvia o convite. Abre a transação e chama <see cref="IServicoDeConvite.EmitirAsync"/> (os 404 e os 422 vêm dele: inativo, sem e-mail corporativo, senha já definida e login
/// desativado, que o convite NÃO reativa: o RH usa "Ativar" antes). Devolve a linha de hoje.
/// </summary>
public sealed class EnviarConviteDeAcessoCasoDeUso(IUnidadeDeTrabalho unidade, IServicoDeConvite convite, IConsultaDeUsuariosDoSistema consulta)
{
    public async Task<UsuarioSistemaItem> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                await convite.EmitirAsync([funcionarioId], ct);
                return 0;
            },
            ct);
        return await consulta.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}

/// <summary>Libera o login de um colaborador ativo que já tem login. Já liberado devolve a linha como está, sem evento.</summary>
public sealed class AtivarUsuarioDoSistemaCasoDeUso(IConsultaDeUsuariosDoSistema consulta, IUsuarioRepositorio usuarios, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<UsuarioSistemaItem> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        var item = await consulta.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var usuario = item.Situacao == UsuarioSistemaSituacao.SemConvite ? null : await usuarios.ObterPorFuncionarioIdAsync(funcionarioId, ct);
        if (usuario is null)
        {
            throw UsuariosDoSistemaErros.SemUsuario();
        }

        if (!usuario.Funcionario.Ativo)
        {
            throw new ErroDeNegocioException("FUNCIONARIO_INATIVO", "A pessoa escolhida está inativa.");
        }

        if (usuario.Ativo)
        {
            return item;
        }

        usuario.Reativar();
        auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Alterou, $"Acesso de {usuario.Funcionario.Nome} liberado", $"Colaborador {usuario.Funcionario.Nome}", "USUARIOS", usuario.Id));
        await unidade.SalvarAsync(ct); // o login e o evento na mesma gravação
        return await consulta.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}

/// <summary>
/// Bloqueia o login: <c>Inativar()</c>, encerra todas as sessões da pessoa (refresh tokens) e registra o evento, numa transação. Ninguém desativa o próprio login (senão o RH se trancaria
/// para fora). Já desativado devolve a linha como está, sem evento.
/// </summary>
public sealed class DesativarUsuarioDoSistemaCasoDeUso(
    IConsultaDeUsuariosDoSistema consulta,
    IUsuarioRepositorio usuarios,
    IRefreshTokenRepositorio tokens,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IAtorAtual ator,
    IRelogio relogio)
{
    public async Task<UsuarioSistemaItem> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        var item = await consulta.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var usuario = item.Situacao == UsuarioSistemaSituacao.SemConvite ? null : await usuarios.ObterPorFuncionarioIdAsync(funcionarioId, ct);
        if (usuario is null)
        {
            throw UsuariosDoSistemaErros.SemUsuario();
        }

        if (ator.FuncionarioId == funcionarioId)
        {
            throw new ErroDeNegocioException("NAO_PODE_DESATIVAR_A_SI_MESMO", "Você não pode desativar o seu próprio login.");
        }

        if (!usuario.Ativo)
        {
            return item;
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                usuario.Inativar();
                await tokens.RevogarTodosDoUsuarioAsync(usuario.Id, relogio.AgoraUtc, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Alterou, $"Acesso de {usuario.Funcionario.Nome} bloqueado", $"Colaborador {usuario.Funcionario.Nome}", "USUARIOS", usuario.Id));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);
        return await consulta.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}

internal static class UsuariosDoSistemaErros
{
    public static ErroDeNegocioException SemUsuario() => new("SEM_USUARIO", "Esta pessoa ainda não tem login. Envie o convite primeiro.");
}
