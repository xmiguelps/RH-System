using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A escrita da conclusão. Roda na transação do caso de uso, que já travou a linha do treinamento. As entidades NÃO são auditadas pelo interceptor (o evento de conclusão é do caso de uso). O <c>UPDATE</c> do certificado mexe na linha que o TRIGGER criou: o EF não a rastreia e a aplicação nunca a insere. A data de conclusão, a do início implícito e a de emissão saem do relógio da aplicação, nunca do <c>CURDATE()</c> do banco.
/// </summary>
public sealed class ConclusaoDoTreinamentoRepositorio(AppDbContext db) : IConclusaoDoModulo
{
    public async Task<ProgressoParaConcluir> LerProgressoAsync(int funcionarioId, int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;
        var eu = funcionarioId;
        var idsDosModulos = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == id).Select(m => m.Id).ToListAsync(ct);
        var concluidos = await db.TreinamentosModulosProgresso.AsNoTracking().Where(p => p.FuncionarioId == eu && idsDosModulos.Contains(p.ModuloId)).Select(p => p.ModuloId).ToListAsync(ct);
        return new ProgressoParaConcluir(idsDosModulos.Count, concluidos);
    }

    public async Task RegistrarProgressoAsync(int moduloId, int funcionarioId, DateTime instanteUtc, CancellationToken ct)
    {
        db.TreinamentosModulosProgresso.Add(TreinamentoModuloProgresso.Criar(moduloId, funcionarioId, instanteUtc));
        await db.SaveChangesAsync(ct);
    }

    public async Task<bool> ConcluirTreinamentoAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct)
    {
        var id = treinamentoId;
        var eu = funcionarioId;
        SituacaoNoTreinamento? concluido = SituacaoNoTreinamento.Concluido;
        DateOnly? dia = hoje;

        // AUDITORIA-OK: a conclusão do treinamento é auditada à mão pelo caso de uso (evento Alterou, na mesma transação); o UPDATE só vale de INSCRITO ou EM_ANDAMENTO (o banco recusa mudar um CONCLUIDO) e a data vem do relógio da aplicação. Sem nota.
        var linhas = await db.TreinamentosFuncionarios
            .Where(l => l.TreinamentoId == id && l.FuncionarioId == eu && (l.Situacao == SituacaoNoTreinamento.Inscrito || l.Situacao == SituacaoNoTreinamento.EmAndamento))
            .ExecuteUpdateAsync(s => s.SetProperty(l => l.Situacao, concluido).SetProperty(l => l.ConcluidoEm, dia), ct);
        return linhas == 1;
    }

    public async Task<bool> TrocarCodigoDoCertificadoAsync(int funcionarioId, int treinamentoId, string codigoVersao4, DateOnly emitidoEm, CancellationToken ct)
    {
        var id = treinamentoId;
        var eu = funcionarioId;
        DateOnly? emissao = emitidoEm;

        // AUDITORIA-OK: a linha do certificado nasce por trigger (UUID v1 e a data do relógio real); a aplicação a corrige na mesma transação da conclusão, antes de o código aparecer em qualquer lugar. O código novo nunca vai para log nem para a auditoria; o evento da conclusão é do caso de uso.
        var linhas = await db.TreinamentosCertificados
            .Where(c => c.TreinamentoId == id && c.FuncionarioId == eu)
            .ExecuteUpdateAsync(s => s.SetProperty(c => c.CodigoValidacao, codigoVersao4).SetProperty(c => c.EmitidoEm, emissao), ct);
        return linhas == 1;
    }
}
