using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// <c>INCLUIR_ADMITIDOS_NO_TREINAMENTO</c>: todo dia, para cada treinamento OBRIGATÓRIO, publicado, não encerrado e com a caixa <c>incluiAdmitidos</c> ligada (o agendado entra), cria a linha <c>INSCRITO</c> de toda pessoa ATIVA, com a admissão já ocorrida, que é do público dele e ainda não tem linha: o admitido novo, o "Em admissão" que começou, o readmitido e quem entrou num
/// setor do público (e, ligada a caixa depois de publicado, todo o atraso de uma vez). Opcional, encerrado, rascunho e treinamento sem a caixa não recebem ninguém. Idempotente (quem já tem linha é pulado; inativo nem entra no <c>SELECT</c> do público); rodar de novo não duplica. Ator Sistema, UM evento <c>ALTEROU</c> por treinamento e dia só com a contagem (nunca nomes). Nenhum e-mail.
/// A recuperação agenda a tarefa do dia (<c>INCLUIR_ADMITIDOS_NO_TREINAMENTO:dia=AAAA-MM-DD</c>, o dia de Brasília).
/// </summary>
public sealed class IncluirAdmitidosNoTreinamentoTarefa(AppDbContext db, ITreinamentosRepositorio treinamentos, IPublicoDoTreinamento publico, IUnidadeDeTrabalho unidade, IAuditor auditor, IAgendador agendador, IRelogio relogio) : ITarefaAgendada
{
    public const string TipoDaTarefa = "INCLUIR_ADMITIDOS_NO_TREINAMENTO";

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var elegiveis = await db.Treinamentos.AsNoTracking()
            .Where(t => t.Obrigatorio && t.Publicado && t.EncerradoEmUtc == null && t.IncluiAdmitidos)
            .OrderBy(t => t.Id)
            .Select(t => new { t.Id, t.Titulo })
            .ToListAsync(ct);

        foreach (var treinamento in elegiveis)
        {
            _ = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var pessoas = await publico.PessoasAsync(treinamento.Id, hoje, ct);
                    var incluidas = pessoas.Count == 0 ? 0 : await treinamentos.InscreverAsync(treinamento.Id, pessoas, ct);
                    if (incluidas > 0)
                    {
                        auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Incluiu {incluidas} {(incluidas == 1 ? "pessoa admitida" : "pessoas admitidas")} no treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamento.Id));
                        await unidade.SalvarAsync(ct);
                    }

                    return incluidas;
                },
                ct);
        }
    }
}
