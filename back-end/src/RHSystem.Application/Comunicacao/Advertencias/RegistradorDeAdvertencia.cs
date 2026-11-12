using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>
/// O que o registro pelo RH e pelo gestor têm em comum: numa transação grava a advertência (nasce <c>PENDENTE</c>, com a data e a situação da APLICAÇÃO: o trigger usaria o <c>CURDATE()</c> real), o evento <c>ADVERTENCIAS</c>/<c>CRIOU</c> (sem motivo nem descrição) e os e-mails
/// <c>ADVERTENCIA_REGISTRADA</c>: SEMPRE à pessoa (se tem e-mail corporativo) e, quando o gestor registra, uma cópia a cada pessoa do RH. O gestor nunca recebe cópia. Quem não tem e-mail não trava o registro.
/// </summary>
public sealed class RegistradorDeAdvertencia(IAdvertenciasRepositorio advertencias, IConsultaDeAdvertencias consulta, IFilaDeEmails fila, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public const string TipoDoEmail = "ADVERTENCIA_REGISTRADA";

    /// <param name="gestorId">A linha de <c>GESTORES</c> de quem aplicou; <c>null</c> é o RH.</param>
    /// <param name="gestorNome">O nome de quem aplicou, quando foi o gestor (vai na cópia ao RH).</param>
    /// <param name="copiaParaORh">Só quando o gestor registra.</param>
    public async Task<int> RegistrarAsync(PessoaParaAdvertencia pessoa, int? gestorId, string? gestorNome, DateOnly data, TipoDeAdvertencia tipo, CamposDaAdvertencia campos, bool copiaParaORh, CancellationToken ct) =>
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var advertencia = Advertencia.Registrar(pessoa.Id, gestorId, data, tipo, campos.Motivo, campos.Descricao);
                var id = await advertencias.AdicionarAsync(advertencia, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Advertencias,
                    AuditoriaAcao.Criou,
                    string.Create(CultureInfo.InvariantCulture, $"Registrou uma advertência {RegrasDaAdvertencia.NomeDoTipo(tipo)} de {data:dd/MM/yyyy} para {pessoa.Nome}."),
                    $"Advertência de {pessoa.Nome}",
                    "ADVERTENCIAS",
                    id));
                await unidade.SalvarAsync(ct);
                await EnfileirarAsync(pessoa, id, gestorNome, data, tipo, copiaParaORh, ct);
                return id;
            },
            ct);

    private async Task EnfileirarAsync(PessoaParaAdvertencia pessoa, int id, string? gestorNome, DateOnly data, TipoDeAdvertencia tipo, bool copiaParaORh, CancellationToken ct)
    {
        var dia = data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        var nomeDoTipo = RegrasDaAdvertencia.NomeDoTipo(tipo);
        if (!string.IsNullOrWhiteSpace(pessoa.EmailCorporativo))
        {
            await fila.EnfileirarAsync(
                new EmailParaEnfileirar(TipoDoEmail, ChaveDeIdempotencia.Montar(TipoDoEmail, ("advertencia", id), ("func", pessoa.Id)), pessoa.EmailCorporativo, new { nome = pessoa.Nome, tipo = nomeDoTipo, data = dia, paraRh = false, pessoa = pessoa.Nome, gestor = gestorNome }, pessoa.Id, id),
                ct);
        }

        if (!copiaParaORh)
        {
            return;
        }

        // Quem do RH é a própria pessoa advertida recebe um e-mail só: a chave leva a advertência e a pessoa, então a cópia dela repete a chave da mensagem que ela já ganhou e a fila a descarta.
        foreach (var rh in await consulta.DestinatariosDoRhAsync(ct))
        {
            await fila.EnfileirarAsync(
                new EmailParaEnfileirar(TipoDoEmail, ChaveDeIdempotencia.Montar(TipoDoEmail, ("advertencia", id), ("func", rh.FuncionarioId)), rh.Email, new { nome = rh.Nome, tipo = nomeDoTipo, data = dia, paraRh = true, pessoa = pessoa.Nome, gestor = gestorNome }, rh.FuncionarioId, id),
                ct);
        }
    }
}
