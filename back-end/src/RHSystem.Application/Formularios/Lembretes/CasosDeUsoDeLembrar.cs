using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Lembretes;

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/lembretes</c> (só o RH): o lembrete manual por e-mail. Ordem das recusas: 404, 400 (<c>funcionarioIds</c> obrigatório, de 0 a 200 números de 1 em diante), 422 <c>FORMULARIO_NAO_PUBLICADO</c> (só PUBLICADO, e o prazo vencido não é publicado), 422 <c>FORMULARIO_POR_LINK</c>, 422 <c>FORMULARIO_SUGESTAO</c>
/// (ninguém "falta" na caixa de sugestões), 422 <c>FORMULARIO_ANONIMO</c> (no anônimo só vale com a lista vazia: lembrar uma pessoa específica revelaria quem falta). Com nome: <c>enviados</c> e <c>ignorados</c> (já respondeu, não deve responder, inativa, sem e-mail ou já recebeu hoje), e o evento <c>ENVIOU_EMAIL</c> só quando alguém recebeu.
/// <b>Anônimo</b>: a resposta é SEMPRE <c>{anonimo: true, enviados: null, ignorados: null}</c> (com muita gente, com pouca, com ninguém e quando o de hoje já tinha saído), a rota só marca o dia e agenda a tarefa, e o evento é gravado SEMPRE, sem número nem destinatário.
/// </summary>
public sealed class LembrarQuemFaltaFormularioCasoDeUso(Construtor.IConsultaDeFormularios consulta, LembretesDoFormulario lembretes, IAuditor auditor, IRelogio relogio)
{
    public const int MaximoDePessoas = 200;

    public async Task<FormularioLembreteResultado> ExecutarAsync(int formularioId, FormularioLembreteComando? corpo, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var ids = LerIds(corpo);
        var agora = relogio.AgoraUtc;
        if (RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agora) != SituacaoDoFormulario.Publicado)
        {
            throw MensagensDosFormularios.FormularioNaoPublicadoParaLembrar.ComoProblema();
        }

        if (lido.Alvo == AlvoDoFormulario.Link)
        {
            throw MensagensDosFormularios.FormularioPorLinkNoLembrete.ComoProblema();
        }

        if (lido.Tipo == TipoDeFormulario.Outro)
        {
            throw MensagensDosFormularios.SugestaoNoLembrete.ComoProblema();
        }

        var hoje = FusoBrasil.Hoje(agora);
        if (lido.Anonimo)
        {
            if (ids.Count > 0)
            {
                throw MensagensDosFormularios.FormularioAnonimo.ComoProblema();
            }

            await lembretes.MarcarEAgendarSigilosoAsync(lido, hoje, agora, null, ct);
            await auditor.RegistrarEGravarAsync(Evento(lido, $"Mandou o lembrete do formulário \"{lido.Titulo}\"."), ct); // sempre, sem número nem destinatário
            return new FormularioLembreteResultado(true, null, null);
        }

        var (enviados, ignorados) = await lembretes.LembrarComNomeAsync(lido, ids, hoje, null, ct);
        if (enviados > 0)
        {
            await auditor.RegistrarEGravarAsync(Evento(lido, $"Mandou o lembrete do formulário \"{lido.Titulo}\" para {enviados} pessoa(s) que ainda não responderam."), ct);
        }

        return new FormularioLembreteResultado(false, enviados, ignorados);
    }

    private static IReadOnlyList<int> LerIds(FormularioLembreteComando? corpo)
    {
        var erros = new ColetorDeErros();
        if (corpo?.FuncionarioIds is not { } ids)
        {
            erros.Adicionar("funcionarioIds", "Informe a lista de pessoas (vazia para lembrar todas que faltam).");
            erros.LancarSeHouver();
            return [];
        }

        if (ids.Count > MaximoDePessoas)
        {
            erros.Adicionar("funcionarioIds", $"Escolha até {MaximoDePessoas} pessoas.");
        }

        if (ids.Any(id => id < 1))
        {
            erros.Adicionar("funcionarioIds", "Cada pessoa é um número de 1 em diante.");
        }

        erros.LancarSeHouver();
        return ids;
    }

    public static EventoDeAuditoriaNovo Evento(Construtor.FormularioLido lido, string descricao) =>
        new(AuditoriaArea.Formularios, AuditoriaAcao.EnviouEmail, descricao, $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id);
}
