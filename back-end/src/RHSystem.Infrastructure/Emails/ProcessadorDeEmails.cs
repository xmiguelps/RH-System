using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// Um ciclo do envio. <b>Reserva</b> até <c>LoteMaximoPorCiclo</c> mensagens com <c>SELECT ... FOR UPDATE SKIP LOCKED</c> pelo <c>FromSql</c> (a forma que a
/// sonda <c>SkipLockedPeloFromSqlTests</c> mediu), numa transação curta: elegíveis são <c>PENDENTE</c> com <c>ProximaTentativaEm &lt;= agora</c> ou <c>EM_ENVIO</c> com a
/// reserva vencida; marca <c>EM_ENVIO</c> com <c>ReservadoAte = agora + ReservaMinutos</c> e confirma. Só depois trata cada uma, FORA da transação. Dois processadores nunca
/// pegam a mesma. Todo instante vem do <see cref="IRelogio"/>, nunca de <c>NOW()</c> do SQL.
/// <para>
/// <b>Sucesso:</b> <c>ENVIADO</c> (carga apagada, reserva limpa, num <c>UPDATE</c> só) e, se não for sigilosa, o evento <c>ENVIOU_EMAIL</c> (ator Sistema). <b>Falha
/// temporária:</b> volta a <c>PENDENTE</c> com espera de 1, 5, 30 e 120 minutos; ao chegar em <c>MaximoDeTentativas</c>, <c>FALHOU</c>. <b>Permanente:</b> <c>FALHOU</c>
/// na hora. <b>Sigilosa</b> (lembrete de formulário anônimo): ao sair ou desistir a linha é APAGADA e nada vai para a auditoria. <c>UltimoErro</c> guarda só o NOME da
/// exceção e o status SMTP, nunca a mensagem (ela pode trazer o endereço); o log traz o id e o tipo, nunca o endereço. <b>At-least-once</b>: se o processo cair depois de
/// enviar e antes de gravar, a reserva vence e a mensagem sai de novo.
/// </para>
/// </summary>
public sealed class ProcessadorDeEmails(
    AppDbContext db,
    IUnidadeDeTrabalho unidade,
    IEnviadorDeEmail enviador,
    IEnumerable<IRenderizadorDeEmail> renderizadores,
    IAuditor auditor,
    IRelogio relogio,
    OpcoesDeEmail opcoes,
    ILogger<ProcessadorDeEmails> log) : IProcessadorDeEmails
{
    /// <summary>A espera depois da 1ª, 2ª, 3ª e 4ª falha temporária (a partir da 5ª, vale a última, se o máximo for maior).</summary>
    private static readonly TimeSpan[] Esperas = [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromMinutes(120)];

    private readonly Dictionary<string, IRenderizadorDeEmail> _renderizadores = renderizadores.ToDictionary(r => r.Tipo, StringComparer.Ordinal);

    public async Task<int> ProcessarCicloAsync(CancellationToken ct)
    {
        var reservadas = await ReservarAsync(ct);
        foreach (var email in reservadas)
        {
            try
            {
                await TratarAsync(email, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw; // o serviço está parando: a mensagem fica EM_ENVIO e a reserva vence
            }
            catch (Exception erro)
            {
                // Uma mensagem com problema não impede as outras do ciclo. Ela fica EM_ENVIO até a reserva vencer e ser retomada.
                log.LogError("Falha ao tratar o e-mail {Id} ({Tipo}): {Erro}", email.Id, email.Tipo, erro.GetType().Name);
                db.Entry(email).State = EntityState.Detached; // se foi o SaveChanges que falhou, a mudança dela não pode travar o próximo
            }
        }

        return reservadas.Count;
    }

    private async Task<List<EmailNaFila>> ReservarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var reservaAte = agora.AddMinutes(opcoes.ReservaMinutos);
        var lote = opcoes.LoteMaximoPorCiclo;
        return await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var elegiveis = await db.EmailsNaFila
                    .FromSql($"SELECT * FROM FILA_EMAILS WHERE (SITUACAO = 'PENDENTE' AND PROXIMA_TENTATIVA_EM <= {agora}) OR (SITUACAO = 'EM_ENVIO' AND RESERVADO_ATE < {agora}) ORDER BY PROXIMA_TENTATIVA_EM, ID_EMAIL LIMIT {lote} FOR UPDATE SKIP LOCKED")
                    .ToListAsync(ct);
                foreach (var email in elegiveis)
                {
                    email.Reservar(reservaAte);
                }

                await db.SaveChangesAsync(ct);
                return elegiveis;
            },
            ct);
    }

    private async Task TratarAsync(EmailNaFila email, CancellationToken ct)
    {
        if (!_renderizadores.TryGetValue(email.Tipo, out var renderizador))
        {
            await DesistirAsync(email, $"SemRenderizador:{email.Tipo}", ct);
            return;
        }

        ModeloDeEmail modelo;
        try
        {
            using var carga = JsonDocument.Parse(email.Carga ?? "{}");
            modelo = renderizador.Renderizar(carga.RootElement, email.DestinatarioEmail);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            await DesistirAsync(email, $"ErroAoRenderizar:{erro.GetType().Name}", ct); // erro de código ou de dado: repetir não adianta
            return;
        }

        try
        {
            await enviador.EnviarAsync(new MensagemDeEmail(email.DestinatarioEmail, modelo.Assunto, modelo.Texto, modelo.Html), ct);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            throw;
        }
        catch (FalhaDeEnvioException falha)
        {
            await RegistrarFalhaAsync(email, falha.Permanente, Descrever(falha), ct);
            return;
        }
        catch (Exception erro)
        {
            await RegistrarFalhaAsync(email, permanente: false, Descrever(erro), ct); // falha que não sabemos classificar: tenta de novo
            return;
        }

        await ConcluirAsync(email, modelo, ct);
    }

    private async Task ConcluirAsync(EmailNaFila email, ModeloDeEmail modelo, CancellationToken ct)
    {
        if (email.Sigiloso)
        {
            db.EmailsNaFila.Remove(email); // sigilosa: apagada depois de enviar, nunca ENVIADO; e nenhum evento de auditoria
            await db.SaveChangesAsync(ct);
            return;
        }

        email.MarcarComoEnviado(relogio.AgoraUtc);
        await db.SaveChangesAsync(ct);
        try
        {
            await auditor.RegistrarEGravarAsync(
                new EventoDeAuditoriaNovo(modelo.AreaDeAuditoria, AuditoriaAcao.EnviouEmail, modelo.DescricaoDeAuditoria, modelo.RegistroAfetado),
                ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            // A mensagem já saiu e já está ENVIADO: repetir o envio por causa do log seria pior. A falha vai para o log de erros.
            log.LogError("O e-mail {Id} ({Tipo}) saiu, mas o evento ENVIOU_EMAIL não foi gravado: {Erro}", email.Id, email.Tipo, erro.GetType().Name);
        }
    }

    /// <summary>Falha de render (ou tipo sem renderizador): <c>FALHOU</c> na hora, com o motivo curto. Sigilosa é apagada.</summary>
    private Task DesistirAsync(EmailNaFila email, string motivo, CancellationToken ct) => RegistrarFalhaAsync(email, permanente: true, motivo, ct);

    private async Task RegistrarFalhaAsync(EmailNaFila email, bool permanente, string erro, CancellationToken ct)
    {
        var tentativa = email.Tentativas + 1;
        var desistir = permanente || tentativa >= opcoes.MaximoDeTentativas;
        log.LogWarning("O e-mail {Id} ({Tipo}) não saiu na tentativa {Tentativa}: {Erro}{Fim}", email.Id, email.Tipo, tentativa, erro, desistir ? " (desistiu)" : string.Empty);

        if (desistir && email.Sigiloso)
        {
            db.EmailsNaFila.Remove(email); // sigilosa nunca fica FALHOU (CHECK do V001): é apagada
        }
        else if (desistir)
        {
            email.MarcarComoFalhou(erro);
        }
        else
        {
            email.AgendarNovaTentativa(relogio.AgoraUtc + Esperas[Math.Min(tentativa - 1, Esperas.Length - 1)], erro);
        }

        await db.SaveChangesAsync(ct);
    }

    /// <summary>Só o NOME da exceção (e o da interna) e o status SMTP: a mensagem pode trazer o endereço do destinatário.</summary>
    private static string Descrever(Exception erro) => erro switch
    {
        FalhaDeEnvioException falha => $"{nameof(FalhaDeEnvioException)}{(falha.InnerException is null ? string.Empty : ":" + falha.InnerException.GetType().Name)}{(falha.StatusSmtp is null ? string.Empty : " " + falha.StatusSmtp)}",
        _ => erro.GetType().Name,
    };
}
