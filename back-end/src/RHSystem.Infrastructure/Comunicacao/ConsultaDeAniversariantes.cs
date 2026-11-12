using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comunicacao.Aniversarios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>Os candidatos a aniversariante: a empresa inteira (ativos, admitidos até hoje, com data de nascimento). O ano de nascimento sai daqui só para a conta da janela, dentro do caso de uso; a resposta nunca o leva.</summary>
public sealed class ConsultaDeAniversariantes(AppDbContext db) : IConsultaDeAniversariantes
{
    public async Task<IReadOnlyList<CandidatoAAniversario>> CandidatosAsync(DateOnly hoje, CancellationToken ct) =>
        await db.Funcionarios.AsNoTracking()
            .Where(f => f.Ativo && f.DataNascimento != null && f.DataAdmissao <= hoje)
            .Select(f => new CandidatoAAniversario(new FuncionarioResumo(f.Id, f.Nome, f.Ativo, f.Cargo != null ? f.Cargo.Nome : null, f.Setor != null ? f.Setor.Nome : null), f.DataNascimento!.Value))
            .ToListAsync(ct);
}
