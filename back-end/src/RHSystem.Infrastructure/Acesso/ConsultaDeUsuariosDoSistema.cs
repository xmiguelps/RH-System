using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Acesso;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Acesso;

/// <summary>
/// "Usuários do sistema": a linha é o colaborador (o login pode não existir). A situação é CALCULADA no banco numa expressão só, e o filtro por <c>situacao</c> e a projeção usam essa mesma coluna
/// calculada (nada de duas regras que um dia divergem). Convite em aberto = token <c>CONVITE</c> com <c>USADO_EM</c> e <c>INVALIDADO_EM</c> nulos e <c>EXPIRA_EM &gt; agora</c> (no instante exato do
/// vencimento já é expirado). O perfil vem do nome do nível de acesso, convertido em memória.
/// </summary>
public sealed class ConsultaDeUsuariosDoSistema(AppDbContext db, IRelogio relogio) : IConsultaDeUsuariosDoSistema
{
    private sealed class Linha
    {
        public int Id { get; init; }

        public string Nome { get; init; } = string.Empty;

        public string? EmailCorporativo { get; init; }

        public string? Nivel { get; init; }

        public bool Ativo { get; init; }

        public DateTime? UltimoLogin { get; init; }

        public DateTime? ConviteEnviadoEm { get; init; }

        public UsuarioSistemaSituacao Situacao { get; init; }
    }

    private IQueryable<Linha> Linhas(bool apenasAtivos)
    {
        var agora = relogio.AgoraUtc;
        return
            from f in db.Funcionarios.AsNoTracking()
            where !apenasAtivos || f.Ativo
            join u in db.Usuarios.AsNoTracking() on f.Id equals u.FuncionarioId into logins
            from u in logins.DefaultIfEmpty()
            select new Linha
            {
                Id = f.Id,
                Nome = f.Nome,
                EmailCorporativo = f.EmailCorporativo,
                Nivel = f.NivelDeAcesso != null ? f.NivelDeAcesso.Nome : null,
                Ativo = u != null && u.Ativo,
                UltimoLogin = u != null ? u.UltimoLoginEm : null,
                ConviteEnviadoEm = u == null
                    ? null
                    : db.UsuariosTokensSenha.Where(t => t.UsuarioId == u.Id && t.Tipo == TipoDeTokenDeSenha.Convite).OrderByDescending(t => t.CriadoEm).Select(t => (DateTime?)t.CriadoEm).FirstOrDefault(),
                Situacao = u == null
                    ? UsuarioSistemaSituacao.SemConvite
                    : !u.SenhaProvisoria
                        ? UsuarioSistemaSituacao.SenhaDefinida
                        : db.UsuariosTokensSenha.Any(t => t.UsuarioId == u.Id && t.Tipo == TipoDeTokenDeSenha.Convite && t.UsadoEm == null && t.InvalidadoEm == null && t.ExpiraEm > agora)
                            ? UsuarioSistemaSituacao.ConviteEnviado
                            : UsuarioSistemaSituacao.ConviteExpirado,
            };
    }

    private static UsuarioSistemaItem ParaItem(Linha l) =>
        new(l.Id, l.Nome, l.EmailCorporativo, Perfis.PerfilPorNome(l.Nivel), l.Situacao, l.Ativo, l.UltimoLogin, l.ConviteEnviadoEm);

    public async Task<RespostaPaginada<UsuarioSistemaItem>> ListarAsync(ParametrosDeLista p, UsuarioSistemaSituacao? situacao, bool? ativo, CancellationToken ct)
    {
        var consulta = Linhas(apenasAtivos: true);
        if (p.Busca is { } busca)
        {
            var like = BuscaLike.Contem(busca);
            consulta = consulta.Where(l => EF.Functions.Like(l.Nome, like) || (l.EmailCorporativo != null && EF.Functions.Like(l.EmailCorporativo, like)));
        }

        if (situacao is { } desejada)
        {
            consulta = consulta.Where(l => l.Situacao == desejada);
        }

        if (ativo is { } liberado)
        {
            consulta = consulta.Where(l => l.Ativo == liberado);
        }

        consulta = consulta.OrderBy(l => l.Nome).ThenBy(l => l.Id);
        var total = await consulta.CountAsync(ct);
        var linhas = await consulta.Skip(p.Pular).Take(p.TamanhoPagina).ToListAsync(ct);
        return RespostaPaginada<UsuarioSistemaItem>.De([.. linhas.Select(ParaItem)], total, p);
    }

    public async Task<UsuarioSistemaItem?> ObterAsync(int funcionarioId, CancellationToken ct)
    {
        var linha = await Linhas(apenasAtivos: false).Where(l => l.Id == funcionarioId).FirstOrDefaultAsync(ct);
        return linha is null ? null : ParaItem(linha);
    }
}
