using System.Runtime.InteropServices;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Emails;
using RHSystem.Domain.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Emails;

/// <summary>
/// A fila de e-mails (<c>FILA_EMAILS</c>). Grava com o <see cref="AppDbContext"/> da requisição, na mesma transação do fato quando o chamador está em
/// <c>ExecutarEmTransacaoAsync</c>: se ela desfaz, a linha some. <c>CriadoEm</c> e <c>ProximaTentativaEm</c> vêm do <see cref="IRelogio"/>, nunca do padrão do banco.
/// </summary>
public sealed class FilaDeEmails(AppDbContext db, IRelogio relogio) : IFilaDeEmails
{
    private static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public async Task<bool> EnfileirarAsync(EmailParaEnfileirar email, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var entidade = EmailNaFila.Criar(
            email.Tipo,
            email.ChaveDeIdempotencia,
            email.DestinatarioEmail,
            agora,
            email.ReferenciaId,
            email.FuncionarioId,
            email.Sigiloso,
            email.Carga is null ? null : JsonSerializer.Serialize(email.Carga, Json),
            proximaTentativaEmUtc: email.EnviarApos);

        // Salva o que já estava pendente ANTES: um 1062 desta mensagem não pode levar junto o resto do que o chamador tinha no contexto.
        await db.SaveChangesAsync(ct);
        db.EmailsNaFila.Add(entidade);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException erro) when (erro.InnerException is MySqlException { Number: 1062 })
        {
            db.Entry(entidade).State = EntityState.Detached; // a chave já existe (na fila ou já enviada): vale como sucesso
            return false;
        }
    }

    public async Task<int> EnfileirarLoteAsync(IReadOnlyList<EmailParaEnfileirar> emails, CancellationToken ct)
    {
        var lista = emails.ToList();
        if (lista.Any(e => e.Sigiloso))
        {
            // A ordem de gravação (e portanto o id) não pode ligar a pessoa ao lembrete anônimo.
            Random.Shared.Shuffle(CollectionsMarshal.AsSpan(lista));
        }

        var entraram = 0;
        foreach (var email in lista)
        {
            if (await EnfileirarAsync(email, ct))
            {
                entraram++;
            }
        }

        return entraram;
    }

    public async Task<int> CancelarPendentesAsync(string tipo, int referenciaId, CancellationToken ct)
    {
        // AUDITORIA-OK: tabela técnica, sem registro por decisão (a fila de e-mails não é fato de negócio)
        var canceladas = await db.EmailsNaFila.Where(e => e.Tipo == tipo && e.ReferenciaId == referenciaId && e.Situacao == SituacaoDoEmail.Pendente && !e.Sigiloso).ExecuteUpdateAsync(s => s.SetProperty(e => e.Situacao, SituacaoDoEmail.Cancelado).SetProperty(e => e.Carga, (string?)null), ct);

        // AUDITORIA-OK: tabela técnica, sem registro por decisão (a sigilosa não pode ficar CANCELADO: o CHECK do V001 a recusaria)
        var apagadas = await db.EmailsNaFila.Where(e => e.Tipo == tipo && e.ReferenciaId == referenciaId && e.Situacao == SituacaoDoEmail.Pendente && e.Sigiloso).ExecuteDeleteAsync(ct);

        return canceladas + apagadas;
    }
}
