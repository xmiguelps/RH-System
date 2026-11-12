using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Formularios;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A leitura dos formulários para o RH. Lê as colunas, o autor, as perguntas (com as alternativas) e o público já escolhido (setores e pessoas das linhas de destinatário), cada um em uma consulta curta. Nada daqui devolve o caminho de um arquivo.
/// A situação é calculada por quem usa (<c>RegraDaSituacaoDoFormulario</c>), nunca no banco: o banco não fecha o formulário no prazo.
/// </summary>
public sealed class ConsultaDeFormularios(AppDbContext db) : IConsultaDeFormularios
{
    private sealed record Base(
        int Id,
        string Titulo,
        string? Descricao,
        TipoDeFormulario? Tipo,
        SentidoDaAvaliacao? Sentido,
        bool Anonimo,
        bool Ativo,
        DateTime? CriadoEmUtc,
        int? AutorId,
        AlvoDoFormulario? Alvo,
        DateTime? PublicadoEmUtc,
        DateTime? EncerradoEmUtc,
        DateOnly? Prazo,
        int? RecorrenciaIntervalo,
        UnidadeDeRecorrencia? RecorrenciaUnidade,
        DateOnly? RecorrenciaAte,
        DateTime? ProximaReaberturaEmUtc,
        bool LembreteAutomatico,
        string? TokenDoLink,
        bool TemRespostas);

    public async Task<FormularioLido?> ObterAsync(int formularioId, CancellationToken ct)
    {
        var f = await db.Formularios.AsNoTracking()
            .Where(x => x.Id == formularioId)
            .Select(x => new Base(
                x.Id,
                x.Titulo,
                x.Descricao,
                x.Tipo,
                x.SentidoAvaliacao,
                x.Anonimo,
                x.Ativo,
                x.CriadoEmUtc,
                x.AutorId,
                x.Alvo,
                x.PublicadoEmUtc,
                x.EncerradoEmUtc,
                x.Prazo,
                x.RecorrenciaIntervalo,
                x.RecorrenciaUnidade,
                x.RecorrenciaAte,
                x.ProximaReaberturaEmUtc,
                x.LembreteAutomatico,
                x.TokenDoLink,
                db.FormulariosRespostas.Any(r => r.FormularioId == x.Id)))
            .SingleOrDefaultAsync(ct);
        if (f is null)
        {
            return null;
        }

        var autor = f.AutorId is { } autorId
            ? await db.Funcionarios.AsNoTracking().Where(p => p.Id == autorId).Select(p => new FuncionarioResumo(p.Id, p.Nome, p.Ativo, p.Cargo != null ? p.Cargo.Nome : null, p.Setor != null ? p.Setor.Nome : null)).SingleOrDefaultAsync(ct)
            : null;
        var perguntas = await PerguntasAsync(formularioId, ct);
        // uma pessoa ou um setor pode ter várias linhas de destinatário (os pares da avaliação): aqui só interessa quem está escolhido, uma vez
        var setores = await db.Setores.AsNoTracking()
            .Where(s => db.FormulariosDestinatarios.Any(d => d.FormularioId == formularioId && d.SetorId == s.Id))
            .OrderBy(s => s.Nome)
            .ThenBy(s => s.Id)
            .Select(s => new FormularioSetor(s.Id, s.Nome))
            .ToListAsync(ct);
        var pessoas = await db.Funcionarios.AsNoTracking()
            .Where(p => db.FormulariosDestinatarios.Any(d => d.FormularioId == formularioId && d.FuncionarioId == p.Id))
            .OrderBy(p => p.Nome)
            .ThenBy(p => p.Id)
            .Select(p => new FuncionarioResumo(p.Id, p.Nome, p.Ativo, p.Cargo != null ? p.Cargo.Nome : null, p.Setor != null ? p.Setor.Nome : null))
            .ToListAsync(ct);

        return new FormularioLido(
            f.Id,
            f.Titulo,
            f.Descricao,
            f.Tipo ?? TipoDeFormulario.Pesquisa,
            f.Sentido,
            f.Anonimo,
            f.Ativo,
            f.CriadoEmUtc ?? DateTime.UnixEpoch,
            autor,
            f.Alvo,
            f.PublicadoEmUtc,
            f.EncerradoEmUtc,
            f.Prazo,
            f.RecorrenciaIntervalo,
            f.RecorrenciaUnidade,
            f.RecorrenciaAte,
            f.ProximaReaberturaEmUtc,
            f.LembreteAutomatico,
            f.TokenDoLink,
            f.TemRespostas,
            perguntas,
            setores,
            pessoas);
    }

    public async Task<bool> PerguntaTemRespostaAsync(int perguntaId, CancellationToken ct) => await db.FormulariosRespostas.AsNoTracking().AnyAsync(r => r.PerguntaId == perguntaId, ct);

    private async Task<IReadOnlyList<PerguntaLida>> PerguntasAsync(int formularioId, CancellationToken ct)
    {
        var linhas = await db.FormulariosPerguntas.AsNoTracking().Where(p => p.FormularioId == formularioId).OrderBy(p => p.Ordem).ThenBy(p => p.Id).ToListAsync(ct);
        var ids = linhas.Select(p => p.Id).ToList();
        var alternativas = (await db.FormulariosAlternativas.AsNoTracking().Where(a => ids.Contains(a.PerguntaId)).OrderBy(a => a.Ordem).ThenBy(a => a.Id).ToListAsync(ct))
            .ToLookup(a => a.PerguntaId);
        return
        [
            .. linhas.Select(p => new PerguntaLida(
                p.Id,
                p.Ordem ?? 1,
                p.Texto,
                p.Tipo ?? TipoDePergunta.RespostaCurta,
                p.Obrigatoria,
                [.. alternativas[p.Id].Select(a => new AlternativaLida(a.Id, a.Ordem, a.Texto))])),
        ];
    }
}
