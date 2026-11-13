using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>O formulário que a pessoa pode abrir ou responder agora, com a situação (sempre <c>PUBLICADO</c>) e o lugar dela nele.</summary>
public sealed record AcessoAoFormulario(FormularioLido Lido, PosicaoDaPessoaNoFormulario Posicao);

/// <summary>
/// A porta de entrada de abrir e de responder um formulário (a mesma conferência, na mesma ordem, para as duas rotas): 404 se não existe, é rascunho, é por link ou a pessoa NÃO deve respondê-lo (e não o respondeu), sem dizer qual dos casos; depois, agendado = 422
/// <c>FORMULARIO_AINDA_NAO_ABERTO</c> e encerrado (inclusive o prazo vencido com a tarefa de encerrar parada) = 422 <c>FORMULARIO_ENCERRADO</c>.
/// </summary>
public static class AcessoAoFormularioParaResponder
{
    public static async Task<AcessoAoFormulario> ConferirAsync(IConsultaDeFormularios consulta, IConsultaDeMeusFormularios meus, int formularioId, int funcionarioId, DateTime agoraUtc, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agoraUtc);
        if (situacao == SituacaoDoFormulario.Rascunho || lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores))
        {
            throw new NaoEncontradoException();
        }

        var posicao = await meus.PosicaoAsync(formularioId, funcionarioId, FusoBrasil.Hoje(agoraUtc), ct);
        if (posicao.MeusPares.Count == 0 && posicao.Feitos.Count == 0)
        {
            throw new NaoEncontradoException();
        }

        if (situacao == SituacaoDoFormulario.Agendado)
        {
            throw MensagensDosFormularios.FormularioAindaNaoAberto.ComoProblema();
        }

        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaResponder.ComoProblema();
        }

        return new AcessoAoFormulario(lido, posicao);
    }
}
