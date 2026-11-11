namespace RHSystem.Application.Acesso;

public sealed record ConviteEmitido(int FuncionarioId, int UsuarioId, bool LoginCriado, DateTime ExpiraEmUtc);

/// <summary>Emite o link de convite (primeiro acesso) e o de redefinição de senha. O token nunca vai para o log, a auditoria nem o banco em claro.</summary>
public interface IServicoDeConvite
{
    /// <summary>
    /// <b>Exige transação aberta</b> (como <c>IGeradorDeMatriculas</c>). De 1 a 500 pessoas. Trava as linhas de <c>FUNCIONARIOS</c> (<c>FOR UPDATE</c>, em ordem de id: é o que serializa
    /// dois convites da mesma pessoa), cria o login que falta (<c>SenhaProvisoria = true</c>, hash provisório, ativo), invalida o convite em aberto e grava o novo (só o hash) em DUAS
    /// gravações (a chave única de link em aberto não aceita o <c>INSERT</c> antes do <c>UPDATE</c>), enfileira o e-mail <c>CONVITE</c> e registra o evento Acesso/Criou (login novo) ou
    /// Acesso/Alterou (reenvio) por <c>IAuditor.Registrar</c>, na mesma transação. <b>Não ativa login desativado: recusa.</b>
    /// <para>
    /// 404 se algum id não existe. 422, nesta ordem (a do contrato, <c>enviarConviteDeAcesso</c>): <c>FUNCIONARIO_INATIVO</c>, <c>FUNCIONARIO_SEM_EMAIL_CORPORATIVO</c>,
    /// <c>SENHA_JA_DEFINIDA</c> e <c>USUARIO_DESATIVADO</c>; no lote, o primeiro erro desfaz o lote todo (a transação do chamador).
    /// </para>
    /// </summary>
    Task<IReadOnlyList<ConviteEmitido>> EmitirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct);

    /// <summary>
    /// Abre a própria transação. <b>Nunca revela se o e-mail existe nem lança por ele.</b> Só para colaborador ATIVO com login ATIVO. Dentro de
    /// <c>Senha:IntervaloMinimoEntreRedefinicoesSegundos</c> do último pedido da pessoa não faz nada. Senão invalida o link anterior, grava o token <c>REDEFINICAO</c>, enfileira o e-mail
    /// <c>REDEFINICAO_SENHA</c> e registra o evento Acesso/Alterou por <c>IAuditor.RegistrarComo</c> (a pessoa como ator), tudo na mesma transação e com a linha da pessoa travada.
    /// </summary>
    Task SolicitarRedefinicaoAsync(string emailNormalizado, CancellationToken ct);
}
