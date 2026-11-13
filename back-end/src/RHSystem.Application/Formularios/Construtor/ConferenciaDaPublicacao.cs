using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// As conferências do corpo da publicação que <c>salvarPublicacaoFormulario</c> e <c>publicarFormulario</c> compartilham, na ordem: 400 (campos juntos; pessoa ou setor que não existe também), 422 pelo estado do formulário, 422 de público que o tipo ou o anonimato não aceita (anônimo
/// por link; só entrevista por link) e 422 de pessoa inativa. Ao SALVAR, o agendado também pode ser salvo; ao PUBLICAR, só o rascunho (publicado ou agendado = <c>FORMULARIO_JA_PUBLICADO</c>; encerrado = <c>FORMULARIO_ENCERRADO</c>).
/// </summary>
internal static class ConferenciaDaPublicacao
{
    public static async Task<PublicacaoValidada> ConferirAsync(IConsultaDeFormularios consulta, FormularioLido lido, SituacaoDoFormulario situacao, FormularioPublicacaoComando corpo, DateTime agoraUtc, bool paraPublicar, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var campos = ValidacaoDaPublicacao.Validar(erros, corpo, FusoBrasil.Hoje(agoraUtc), lido.Tipo);
        if (!paraPublicar)
        {
            ConferirPrazoContraODiaAgendado(erros, corpo, lido, situacao);
        }

        if (campos is not null)
        {
            await ConferirExistenciaAsync(consulta, erros, campos, ct);
        }

        erros.LancarSeHouver();
        ConferirSituacao(situacao, paraPublicar);
        if (RegrasDaPublicacao.Recusa(lido.Tipo, lido.Anonimo, campos!.Alvo) is { } recusa)
        {
            throw (recusa == RecusaDePublicacao.AnonimoSemLink ? MensagensDosFormularios.AnonimoSemLink : MensagensDosFormularios.LinkSoParaEntrevista).ComoProblema();
        }

        if (campos.FuncionarioIds.Count > 0 && (await consulta.PessoasAsync(campos.FuncionarioIds, ct)).Any(p => !p.Ativo))
        {
            throw MensagensDosFormularios.PessoaInativaNoPublico.ComoProblema();
        }

        return campos;
    }

    /// <summary>Num agendado sem <c>agendarPara</c> no corpo o dia guardado continua valendo, e o prazo não pode ser anterior a ele.</summary>
    private static void ConferirPrazoContraODiaAgendado(ColetorDeErros erros, FormularioPublicacaoComando corpo, FormularioLido lido, SituacaoDoFormulario situacao)
    {
        if (situacao == SituacaoDoFormulario.Agendado && corpo.AgendarPara is null && corpo.Prazo is { } prazo && lido.PublicadoEmUtc is { } publicacao && prazo < FusoBrasil.DiaLocalDe(publicacao))
        {
            erros.Adicionar("prazo", "O prazo não pode ser anterior ao dia agendado.");
        }
    }

    private static async Task ConferirExistenciaAsync(IConsultaDeFormularios consulta, ColetorDeErros erros, PublicacaoValidada campos, CancellationToken ct)
    {
        if (campos.FuncionarioIds.Count > 0 && (await consulta.PessoasAsync(campos.FuncionarioIds, ct)).Count != campos.FuncionarioIds.Count)
        {
            erros.Adicionar("funcionarioIds", "Uma das pessoas escolhidas não existe.");
        }

        if (campos.SetorIds.Count > 0 && await consulta.ContarSetoresExistentesAsync(campos.SetorIds, ct) != campos.SetorIds.Count)
        {
            erros.Adicionar("setorIds", "Um dos setores escolhidos não existe.");
        }
    }

    private static void ConferirSituacao(SituacaoDoFormulario situacao, bool paraPublicar)
    {
        if (situacao == SituacaoDoFormulario.Publicado || (paraPublicar && situacao == SituacaoDoFormulario.Agendado))
        {
            throw MensagensDosFormularios.FormularioJaPublicado.ComoProblema();
        }

        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaEditar.ComoProblema();
        }
    }
}
