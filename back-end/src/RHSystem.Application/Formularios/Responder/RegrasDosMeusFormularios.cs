using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// O cartão de "meus formulários" a partir da posição da pessoa (regras puras). Entra o PUBLICADO em que ela deve responder e todo formulário que ela já respondeu (mesmo encerrado). Admissão e avaliação são "para responder"; pesquisa é "disponível"; a Sugestão fica SEMPRE disponível (aceita várias
/// respostas da mesma pessoa), com <c>respondidos</c> 0 de <c>total</c> 1, sem data e sem "ver minhas respostas". Fora da Sugestão há uma resposta por pessoa (por avaliado, na avaliação da equipe, em que <c>total</c> é o número de pessoas a avaliar e o cartão continua "para responder" até avaliar todas).
/// </summary>
public static class RegrasDosMeusFormularios
{
    /// <summary>O cartão, ou <c>null</c> se o formulário não aparece para esta pessoa.</summary>
    public static FormularioMeuItem? Montar(CartaoDoFormulario c, DateTime agoraUtc)
    {
        var publicado = RegraDaSituacaoDoFormulario.De(c.Ativo, c.PublicadoEmUtc, c.EncerradoEmUtc, c.Prazo, agoraUtc) == SituacaoDoFormulario.Publicado;
        var deve = c.MeusPares.Count > 0;
        var daEquipe = c.Tipo == TipoDeFormulario.Avaliacao && c.Sentido == SentidoDaAvaliacao.GestorAvaliaEquipe;
        var total = daEquipe ? Math.Max(c.MeusPares.Union(c.Feitos).Count(), 1) : 1;
        var respondidos = daEquipe ? c.Feitos.Count : (c.Feitos.Count > 0 ? 1 : 0);
        var minutos = MinutosEstimados.De(c.TiposDasPerguntas, daEquipe ? total : 1);

        if (c.Tipo == TipoDeFormulario.Outro)
        {
            return publicado && deve ? Item(c, SituacaoMinhaDoFormulario.Disponivel, 0, 1, minutos, respondidoEmUtc: null, prazo: null, podeVer: false) : null;
        }

        var fez = respondidos > 0;
        if ((fez && respondidos >= total) || (fez && (!publicado || !deve)))
        {
            return Item(c, SituacaoMinhaDoFormulario.Respondido, respondidos, total, minutos, c.Anonimo ? null : c.UltimaRespostaEmUtc, c.Prazo, podeVer: !c.Anonimo);
        }

        if (publicado && deve)
        {
            var situacao = c.Tipo is TipoDeFormulario.Admissao or TipoDeFormulario.Avaliacao ? SituacaoMinhaDoFormulario.ParaResponder : SituacaoMinhaDoFormulario.Disponivel;
            return Item(c, situacao, respondidos, total, minutos, respondidoEmUtc: null, c.Prazo, podeVer: !c.Anonimo && fez);
        }

        return null;
    }

    private static FormularioMeuItem Item(CartaoDoFormulario c, SituacaoMinhaDoFormulario situacao, int respondidos, int total, int minutos, DateTime? respondidoEmUtc, DateOnly? prazo, bool podeVer) =>
        new(c.Id, c.Titulo, c.Descricao, c.Tipo, c.Sentido, c.Anonimo, situacao, c.TiposDasPerguntas.Count, minutos, prazo, respondidos, total, respondidoEmUtc, podeVer);

    /// <summary>A ordem das abas: para responder, depois disponíveis, depois respondidos; dentro de cada uma o prazo mais próximo primeiro (sem prazo por último), o título e o id.</summary>
    public static IEnumerable<FormularioMeuItem> Ordenar(IEnumerable<FormularioMeuItem> itens) =>
        itens.OrderBy(i => i.Situacao).ThenBy(i => i.Prazo is null).ThenBy(i => i.Prazo).ThenBy(i => i.Titulo, StringComparer.CurrentCultureIgnoreCase).ThenBy(i => i.Id);

    /// <summary>As contagens das abas sobre os cartões já filtrados por busca e tipo (a situação não filtra): <c>todos = paraResponder + disponiveis</c>.</summary>
    public static FormularioMeusContagens Contar(IReadOnlyCollection<FormularioMeuItem> itens)
    {
        var para = itens.Count(i => i.Situacao == SituacaoMinhaDoFormulario.ParaResponder);
        var disponiveis = itens.Count(i => i.Situacao == SituacaoMinhaDoFormulario.Disponivel);
        return new FormularioMeusContagens(para + disponiveis, para, disponiveis, itens.Count(i => i.Situacao == SituacaoMinhaDoFormulario.Respondido));
    }
}
