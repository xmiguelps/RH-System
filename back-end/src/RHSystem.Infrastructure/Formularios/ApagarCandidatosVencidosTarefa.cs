using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Publico;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// <c>APAGAR_CANDIDATOS_VENCIDOS</c>: a retenção dos dados de quem se candidatou pelo link (LGPD). O candidato com MAIS de <c>Formularios:RetencaoDeCandidatosDias</c> dias (180 por padrão, contados da data de cadastro) é apagado com as respostas dele; 180 dias exatos ainda ficam. UMA transação apaga as respostas e os candidatos
/// vencidos de TODOS os formulários e grava um evento <c>EXCLUIU</c> do Sistema por formulário, só com o título e a quantidade (nunca nome, e-mail ou telefone). DEPOIS dela os arquivos (o currículo e os arquivos de resposta) saem do armazenamento; o que não sair agora fica sem linha e a limpeza diária de órfãos o apaga
/// (o erro vai ao log só pelo tipo). Idempotente: sem vencidos não faz nada e não grava evento; rodar de novo não acha mais nada. A recuperação agenda uma tarefa por dia (<c>APAGAR_CANDIDATOS_VENCIDOS:dia=AAAA-MM-DD</c>, o dia de Brasília).
/// </summary>
public sealed class ApagarCandidatosVencidosTarefa(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IArmazenamentoDeArquivos armazenamento,
    IAgendador agendador,
    IRelogio relogio,
    OpcoesDoFormularioPublico opcoes,
    ILogger<ApagarCandidatosVencidosTarefa> log) : ITarefaAgendada
{
    public const string TipoDaTarefa = "APAGAR_CANDIDATOS_VENCIDOS";

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var dias = opcoes.RetencaoDeCandidatosDias;
        var corte = relogio.AgoraUtc.AddDays(-dias);

        var arquivos = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var porFormulario = await db.FormulariosCandidatos.AsNoTracking()
                    .Where(c => c.CadastradoEmUtc < corte)
                    .GroupBy(c => c.FormularioId)
                    .Select(g => new { FormularioId = g.Key, Total = g.Count() })
                    .ToListAsync(ct);
                if (porFormulario.Count == 0)
                {
                    return new List<string>();
                }

                var curriculos = await db.FormulariosCandidatos.AsNoTracking().Where(c => c.CadastradoEmUtc < corte && c.CaminhoCurriculo != null).Select(c => c.CaminhoCurriculo!).ToListAsync(ct);
                var dasRespostas = await db.FormulariosRespostas.AsNoTracking()
                    .Where(r => r.CaminhoArquivo != null && r.CandidatoId != null && db.FormulariosCandidatos.Any(c => c.Id == r.CandidatoId && c.CadastradoEmUtc < corte))
                    .Select(r => r.CaminhoArquivo!)
                    .ToListAsync(ct);
                var ids = porFormulario.Select(p => p.FormularioId).ToList();
                var titulos = await db.Formularios.AsNoTracking().Where(f => ids.Contains(f.Id)).ToDictionaryAsync(f => f.Id, f => f.Titulo, ct);

                // AUDITORIA-OK: as respostas de candidato não são auditadas (dado pessoal que não vai a evento); o evento Excluiu é um por formulário, só com o título e a quantidade, gravado na mesma transação.
                await db.FormulariosRespostas.Where(r => r.CandidatoId != null && db.FormulariosCandidatos.Any(c => c.Id == r.CandidatoId && c.CadastradoEmUtc < corte)).ExecuteDeleteAsync(ct);

                // AUDITORIA-OK: os candidatos não são auditados (nenhum nome, e-mail ou telefone vai a evento); o evento Excluiu por formulário, com a quantidade, sai nesta transação.
                await db.FormulariosCandidatos.Where(c => c.CadastradoEmUtc < corte).ExecuteDeleteAsync(ct);

                foreach (var grupo in porFormulario.OrderBy(p => p.FormularioId))
                {
                    var titulo = titulos.GetValueOrDefault(grupo.FormularioId, string.Empty);
                    var quantidade = grupo.Total == 1 ? "1 candidatura vencida" : $"{grupo.Total} candidaturas vencidas";
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Excluiu, $"Apagou {quantidade} do formulário \"{titulo}\" (retenção de {dias} dias).", $"Formulário \"{titulo}\"", "FORMULARIOS", grupo.FormularioId));
                }

                await unidade.SalvarAsync(ct);
                return new List<string>([.. curriculos, .. dasRespostas]);
            },
            ct);

        foreach (var caminho in arquivos)
        {
            try
            {
                await armazenamento.ApagarAsync(caminho, CancellationToken.None);
            }
            catch (Exception erro)
            {
                // a linha já foi apagada: o arquivo que não saiu agora fica sem referência e sai na limpeza de órfãos (só o tipo do erro vai ao log: a mensagem pode trazer o caminho)
                log.LogWarning("Não foi possível apagar agora um arquivo de candidato vencido ({Tipo}); a limpeza de órfãos o apaga.", erro.GetType().Name);
            }
        }
    }
}
