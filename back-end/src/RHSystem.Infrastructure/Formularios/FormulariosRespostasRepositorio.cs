using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A gravação e a leitura das respostas do funcionário. A linha do formulário é segurada por um <c>UPDATE</c> sem mudança (como a das perguntas): dois envios da mesma pessoa esperam um pelo outro e o encerrar também. A entidade de resposta NÃO é auditada pelo interceptor e nada aqui grava evento: responder com
/// nome não deixa linha em <c>AUDITORIA_EVENTOS</c>. Recusa do banco (trigger de obrigatória, de formulário inativo e de pessoa inativa) vira a resposta do contrato pelo <see cref="TradutorDeRecusaDosFormularios"/>.
/// </summary>
public sealed class FormulariosRespostasRepositorio(AppDbContext db) : IFormulariosRespostasRepositorio
{
    public async Task<bool> SegurarAbertoAsync(int formularioId, bool anonimo, CancellationToken ct)
    {
        // AUDITORIA-OK: não muda valor nenhum (TITULO = TITULO): só segura a linha do formulário até o fim da transação, para o outro envio da mesma pessoa, o encerrar e o publicar esperarem; nada a auditar.
        var linhas = await db.Formularios.Where(f => f.Id == formularioId && f.Ativo && f.EncerradoEmUtc == null && f.Anonimo == anonimo).ExecuteUpdateAsync(s => s.SetProperty(f => f.Titulo, f => f.Titulo), ct);
        return linhas == 1;
    }

    public Task<bool> JaRespondeuAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct) =>
        db.FormulariosRespostas.AsNoTracking().AnyAsync(r => r.FormularioId == formularioId && r.FuncionarioId == funcionarioId && r.AvaliadoId == avaliadoId, ct);

    public Task<bool> JaParticipouAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct) =>
        db.FormulariosParticipacoes.AsNoTracking().AnyAsync(p => p.FormularioId == formularioId && p.FuncionarioId == funcionarioId && p.AvaliadoId == avaliadoId, ct);

    public async Task<DateTime?> UltimoEnvioAsync(int formularioId, int funcionarioId, CancellationToken ct) =>
        await db.FormulariosRespostas.AsNoTracking().Where(r => r.FormularioId == formularioId && r.FuncionarioId == funcionarioId).MaxAsync(r => r.RespondidaEmUtc, ct);

    public async Task GravarDeFuncionarioAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime instanteUtc, IReadOnlyList<LinhaParaGravar> linhas, CancellationToken ct)
    {
        try
        {
            foreach (var linha in linhas)
            {
                db.FormulariosRespostas.Add(FormularioResposta.DeFuncionario(formularioId, linha.PerguntaId, funcionarioId, linha.Resposta, instanteUtc, avaliadoId, linha.AlternativaId, linha.CaminhoArquivo, linha.NomeArquivoOriginal));
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception erro) when (TradutorDeRecusaDosFormularios.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }

    public async Task<Guid> GravarAnonimaAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime diaDaRodadaUtc, IReadOnlyList<LinhaParaGravar> linhas, CancellationToken ct)
    {
        try
        {
            foreach (var linha in linhas)
            {
                db.FormulariosRespostas.Add(FormularioResposta.Anonima(formularioId, linha.PerguntaId, linha.Resposta, diaDaRodadaUtc, avaliadoId, linha.AlternativaId));
            }

            var participacao = FormularioParticipacao.Criar(formularioId, funcionarioId, avaliadoId);
            db.FormulariosParticipacoes.Add(participacao);
            await db.SaveChangesAsync(ct);
            return participacao.Id;
        }
        catch (Exception erro) when (TradutorDeRecusaDosFormularios.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }

    public async Task<IReadOnlyList<RespostaLida>> DaPessoaAsync(int formularioId, int funcionarioId, int? avaliadoId, CancellationToken ct) =>
        await db.FormulariosRespostas.AsNoTracking()
            .Where(r => r.FormularioId == formularioId && r.FuncionarioId == funcionarioId && r.AvaliadoId == avaliadoId)
            .OrderBy(r => r.PerguntaId)
            .Select(r => new RespostaLida(r.Id, r.PerguntaId, r.Resposta, r.AlternativaId, r.NomeArquivoOriginal, r.RespondidaEmUtc))
            .ToListAsync(ct);
}

/// <summary>
/// Diz à limpeza de sobras (<c>LIMPAR_ARQUIVOS_ORFAOS</c>) quais caminhos de <c>formularios/</c> têm linha: o arquivo de uma resposta (<c>FORMULARIOS_RESPOSTAS.CAMINHO_ARQUIVO</c>) ou o currículo de um candidato (<c>FORMULARIOS_CANDIDATOS.CAMINHO_CURRICULO</c>). Sem esta fonte a limpeza nunca toca o
/// prefixo; com ela, o arquivo que o banco referencia nunca é apagado e a sobra de um envio que caiu entre o blob e a linha é apagada depois do mínimo de horas.
/// </summary>
public sealed class FonteDeReferenciasDosFormularios(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => EnvioDeArquivosDeFormulario.Pasta + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var dasRespostas = await db.FormulariosRespostas.AsNoTracking().Where(r => r.CaminhoArquivo != null && lista.Contains(r.CaminhoArquivo)).Select(r => r.CaminhoArquivo!).ToListAsync(ct);
        var dosCurriculos = await db.FormulariosCandidatos.AsNoTracking().Where(c => c.CaminhoCurriculo != null && lista.Contains(c.CaminhoCurriculo)).Select(c => c.CaminhoCurriculo!).ToListAsync(ct);
        return dasRespostas.Concat(dosCurriculos).ToHashSet(StringComparer.Ordinal);
    }
}
