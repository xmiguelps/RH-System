using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A escrita dos treinamentos. As entidades NÃO são auditadas pelo interceptor (o evento é escrito à mão pelo caso de uso). A atualização é um <c>UPDATE</c> condicional ao estado lido; as colunas que o banco liga por padrão (<c>EMITE_CERTIFICADO</c>, <c>INCLUI_ADMITIDOS</c>) saem SEMPRE no <c>INSERT</c> e no <c>UPDATE</c>, inclusive "falso".
/// </summary>
public sealed class TreinamentosRepositorio(AppDbContext db) : ITreinamentosRepositorio
{
    public async Task<int> CriarAsync(CamposDoTreinamento campos, CancellationToken ct)
    {
        var treinamento = Treinamento.Criar(campos.Titulo, campos.Descricao, campos.Onboarding, campos.Obrigatorio, campos.Publico, campos.EmiteCertificado, campos.IncluiAdmitidos, campos.ComecaEm);
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync(ct);
        AdicionarPublico(treinamento.Id, campos);
        await db.SaveChangesAsync(ct);
        return treinamento.Id;
    }

    public async Task<bool> AtualizarAsync(TreinamentoLido lido, CamposDoTreinamento novos, CancellationToken ct)
    {
        var id = lido.Id;
        var encerradoLido = lido.EncerradoEmUtc;
        var comecaLido = lido.ComecaEm;
        var titulo = novos.Titulo;
        var descricao = novos.Descricao;
        var comecaEm = novos.ComecaEm;
        var admitidos = novos.IncluiAdmitidos;

        if (lido.Publicado)
        {
            // AUDITORIA-OK: a alteração do treinamento é auditada à mão pelo caso de uso (evento Alterou, na mesma transação); depois de publicado só estes quatro campos mudam e a data de início só quando o corpo a traz diferente (o caso de uso já barrou o resto).
            var alteradas = await db.Treinamentos
                .Where(t => t.Id == id && t.Publicado && t.EncerradoEmUtc == encerradoLido && t.ComecaEm == comecaLido)
                .ExecuteUpdateAsync(s => s.SetProperty(t => t.Titulo, titulo).SetProperty(t => t.Descricao, descricao).SetProperty(t => t.IncluiAdmitidos, admitidos).SetProperty(t => t.ComecaEm, comecaEm), ct);
            return alteradas == 1;
        }

        var tipo = novos.Onboarding ? Treinamento.TipoOnboarding : null;
        var obrigatorio = novos.Obrigatorio;
        var emite = novos.EmiteCertificado;
        PublicoDoTreinamento? publico = novos.Publico;

        // AUDITORIA-OK: a alteração do treinamento é auditada à mão pelo caso de uso (evento Alterou, na mesma transação); no rascunho todos os campos mudam e o UPDATE só vale se ele continua rascunho.
        var linhas = await db.Treinamentos
            .Where(t => t.Id == id && !t.Publicado && t.EncerradoEmUtc == null)
            .ExecuteUpdateAsync(
                s => s.SetProperty(t => t.Titulo, titulo)
                    .SetProperty(t => t.Descricao, descricao)
                    .SetProperty(t => t.TipoTreinamento, tipo)
                    .SetProperty(t => t.ComecaEm, comecaEm)
                    .SetProperty(t => t.Obrigatorio, obrigatorio)
                    .SetProperty(t => t.Publico, publico)
                    .SetProperty(t => t.EmiteCertificado, emite)
                    .SetProperty(t => t.IncluiAdmitidos, admitidos),
                ct);
        if (linhas != 1)
        {
            return false;
        }

        // AUDITORIA-OK: as linhas do público não são auditadas; o evento Alterou do caso de uso cobre a troca. Apaga ANTES de inserir as novas (chave única do destino).
        await db.TreinamentosPublico.Where(p => p.TreinamentoId == id).ExecuteDeleteAsync(ct);
        AdicionarPublico(id, novos);
        await db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> PublicarAsync(int treinamentoId, CancellationToken ct)
    {
        var id = treinamentoId;

        // AUDITORIA-OK: a publicação é auditada à mão pelo caso de uso (evento Publicou, na mesma transação); o UPDATE só vale se o treinamento ainda não foi publicado nem encerrado (duas chamadas ao mesmo tempo: só uma ganha).
        var linhas = await db.Treinamentos.Where(t => t.Id == id && !t.Publicado && t.EncerradoEmUtc == null).ExecuteUpdateAsync(s => s.SetProperty(t => t.Publicado, true), ct);
        return linhas == 1;
    }

    public async Task<int> InscreverAsync(int treinamentoId, IReadOnlyList<int> funcionarioIds, CancellationToken ct)
    {
        var existentes = await db.TreinamentosFuncionarios.AsNoTracking().Where(l => l.TreinamentoId == treinamentoId).Select(l => l.FuncionarioId).ToListAsync(ct);
        var novos = funcionarioIds.Distinct().Except(existentes).ToList();
        db.TreinamentosFuncionarios.AddRange(novos.Select(pessoa => TreinamentoFuncionario.Criar(treinamentoId, pessoa, SituacaoNoTreinamento.Inscrito)));
        await db.SaveChangesAsync(ct);
        return novos.Count;
    }

    /// <summary><c>TODOS</c> não guarda linha; <c>SETORES</c> uma por setor; <c>PESSOAS</c> uma por pessoa (o caso de uso já zerou a lista do outro público).</summary>
    private void AdicionarPublico(int treinamentoId, CamposDoTreinamento campos)
    {
        if (campos.Publico == PublicoDoTreinamento.Setores)
        {
            db.TreinamentosPublico.AddRange(campos.SetorIds.Select(setor => TreinamentoPublico.DoSetor(treinamentoId, setor)));
        }
        else if (campos.Publico == PublicoDoTreinamento.Pessoas)
        {
            db.TreinamentosPublico.AddRange(campos.FuncionarioIds.Select(pessoa => TreinamentoPublico.DaPessoa(treinamentoId, pessoa)));
        }
    }
}
