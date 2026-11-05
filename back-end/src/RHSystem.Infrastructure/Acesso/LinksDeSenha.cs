using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Acesso;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Acesso;

/// <summary>
/// Os links de senha no banco (<c>USUARIOS_TOKENS_SENHA</c>). Todo instante vem do chamador (o <c>IRelogio</c>), nunca de <c>NOW()</c>. "Vale" é <c>EM_ABERTO</c>, <c>EXPIRA_EM &gt; agora</c> (no
/// instante exato do vencimento já não vale), login e colaborador ativos e, se for convite, login ainda provisório.
/// </summary>
public sealed class LinksDeSenha(AppDbContext db) : ILinksDeSenha
{
    public async Task<LinkDeSenhaValido?> ObterValidoAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct)
    {
        var achado = await (
            from t in db.UsuariosTokensSenha.AsNoTracking()
            join u in db.Usuarios.AsNoTracking() on t.UsuarioId equals u.Id
            join f in db.Funcionarios.AsNoTracking() on u.FuncionarioId equals f.Id
            where t.TokenHash == tokenHash
                && t.UsadoEm == null
                && t.InvalidadoEm == null
                && t.ExpiraEm > agoraUtc
                && u.Ativo
                && f.Ativo
                && (t.Tipo != TipoDeTokenDeSenha.Convite || u.SenhaProvisoria)
            select new
            {
                TokenId = t.Id,
                UsuarioId = u.Id,
                FuncionarioId = f.Id,
                t.Tipo,
                f.Nome,
                f.EmailCorporativo,
                t.ExpiraEm,
                Nivel = f.NivelDeAcesso == null ? null : f.NivelDeAcesso.Nome,
            }).FirstOrDefaultAsync(ct);

        return achado is null
            ? null
            : new LinkDeSenhaValido(achado.TokenId, achado.UsuarioId, achado.FuncionarioId, achado.Tipo, achado.Nome, achado.EmailCorporativo ?? string.Empty, achado.ExpiraEm, Perfis.PerfilPorNome(achado.Nivel));
    }

    public async Task<bool> TentarConsumirAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct)
    {
        // AUDITORIA-OK: tabela técnica, o evento é o de senha definida (registrado na mesma transação pelo caso de uso)
        var linhas = await db.UsuariosTokensSenha
            .Where(t => t.TokenHash == tokenHash && t.UsadoEm == null && t.InvalidadoEm == null && t.ExpiraEm > agoraUtc)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.UsadoEm, agoraUtc), ct);
        return linhas == 1;
    }

    public async Task<int> InvalidarOutrosDaPessoaAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct) =>
        // AUDITORIA-OK: tabela técnica, o evento é o de senha definida (registrado na mesma transação pelo caso de uso)
        await db.UsuariosTokensSenha
            .Where(t => t.UsuarioId == usuarioId && t.UsadoEm == null && t.InvalidadoEm == null)
            .ExecuteUpdateAsync(s => s.SetProperty(t => t.InvalidadoEm, agoraUtc), ct);
}
