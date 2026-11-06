using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A leitura da equipe pelo gestor. TODA consulta parte de <c>DaEquipe(equipe)</c> (ativos, com a admissão já começada, do setor dele, sem ele): o que está fora do escopo nunca é lido, então "não existe" e "é de
/// outro setor" não se distinguem. A lista não tem paginação (nome de A a Z, desempate por id), e as contagens de hoje saem DESTA lista pela mesma situação de hoje da ficha do RH.
/// </summary>
public sealed class ConsultaDaEquipe(AppDbContext db, ISituacaoDeHoje situacaoDeHoje) : IConsultaDaEquipe
{
    public async Task<FuncionarioEquipeResposta> ListarAsync(EquipeDoGestor? equipe, CancellationToken ct)
    {
        if (equipe is null)
        {
            // O gestor sem setor: equipe vazia. O dia útil vem do mesmo calendário, mesmo sem ninguém para classificar.
            var vazio = await situacaoDeHoje.ObterAsync([], ct);
            return new FuncionarioEquipeResposta(null, 0, vazio.DiaUtil, PresencaContagens.De([]), []);
        }

        var linhas = await db.Funcionarios.AsNoTracking()
            .DaEquipe(equipe)
            .OrderBy(f => f.Nome)
            .ThenBy(f => f.Id)
            .Select(f => new
            {
                f.Id,
                f.Nome,
                Cargo = f.Cargo != null ? f.Cargo.Nome : null,
                Setor = f.Setor != null ? f.Setor.Nome : null,
                f.Ativo,
            })
            .ToListAsync(ct);

        var dia = await situacaoDeHoje.ObterAsync([.. linhas.Select(l => l.Id)], ct);
        var itens = linhas.Select(l => new FuncionarioEquipeItem(l.Id, l.Nome, l.Cargo, l.Setor, l.Ativo, dia.Situacoes[l.Id])).ToList();
        return new FuncionarioEquipeResposta(new SetorReferencia(equipe.SetorId, equipe.SetorNome), itens.Count, dia.DiaUtil, PresencaContagens.De(itens.Select(i => i.SituacaoHoje)), itens);
    }

    public async Task<FuncionarioFichaGestor?> ObterFichaAsync(EquipeDoGestor? equipe, int funcionarioId, CancellationToken ct)
    {
        if (equipe is null)
        {
            return null;
        }

        var f = await db.Funcionarios.AsNoTracking()
            .DaEquipe(equipe)
            .Where(x => x.Id == funcionarioId)
            .Select(x => new
            {
                x.Id,
                x.Nome,
                Cargo = x.Cargo != null ? x.Cargo.Nome : null,
                Setor = x.Setor != null ? x.Setor.Nome : null,
                x.Ativo,
                x.EmailCorporativo,
                Telefone = x.Detalhes != null ? x.Detalhes.Telefone : null,
                x.DataAdmissao,
                x.TipoContrato,
                Jornada = x.Cargo != null ? x.Cargo.CargaSemanalHoras : (decimal?)null,
            })
            .FirstOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }

        var situacao = (await situacaoDeHoje.ObterAsync([f.Id], ct)).Situacoes[f.Id];
        return new FuncionarioFichaGestor(f.Id, f.Nome, f.Cargo, f.Setor, f.Ativo, situacao, f.EmailCorporativo, TelefoneMascarado.Mascarar(f.Telefone), f.DataAdmissao, TiposDeContrato.DoTexto(f.TipoContrato), f.Jornada);
    }
}
