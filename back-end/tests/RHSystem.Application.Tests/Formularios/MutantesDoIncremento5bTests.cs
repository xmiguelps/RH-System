using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Tests.Formularios;

/// <summary>Os mutantes do incremento 5b (T11 a T18) que sobreviveram e são regras puras: só a avaliação do GESTOR conta uma resposta por avaliado, e as abas contam os respondidos pela situação.</summary>
public class MutantesDoIncremento5bTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);
    private static readonly DateTime Publicado = new(2026, 10, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Na_avaliacao_do_gestor_pela_equipe_o_total_e_sempre_1_mesmo_com_dois_pares()
    {
        var cartao = new CartaoDoFormulario(1, "Avaliação", null, TipoDeFormulario.Avaliacao, SentidoDaAvaliacao.EquipeAvaliaGestor, false, true, Publicado, null, null, [TipoDePergunta.Escala], [new ParDeResposta(7, 10), new ParDeResposta(7, 11)], [], null);

        var item = RegrasDosMeusFormularios.Montar(cartao, Agora)!;

        Assert.Equal((1, 0), (item.Total, item.Respondidos)); // só GESTOR_AVALIA_EQUIPE tem um par por avaliado; aqui o total não é o número de pares
    }

    [Fact]
    public void Na_avaliacao_do_gestor_sobre_a_equipe_o_total_e_o_numero_de_pares()
    {
        var cartao = new CartaoDoFormulario(1, "Avaliação", null, TipoDeFormulario.Avaliacao, SentidoDaAvaliacao.GestorAvaliaEquipe, false, true, Publicado, null, null, [TipoDePergunta.Escala], [new ParDeResposta(7, 10), new ParDeResposta(7, 11)], [new ParDeResposta(7, 12)], null);

        var item = RegrasDosMeusFormularios.Montar(cartao, Agora)!;

        Assert.Equal((3, 1), (item.Total, item.Respondidos));
    }

    [Fact]
    public void As_contagens_das_abas_contam_os_respondidos_pela_situacao_e_nao_os_outros()
    {
        FormularioMeuItem Item(int id, SituacaoMinhaDoFormulario situacao) => new(id, "F" + id, null, TipoDeFormulario.Pesquisa, null, false, situacao, 1, 1, null, 0, 1, null, false);
        IReadOnlyCollection<FormularioMeuItem> itens =
        [
            Item(1, SituacaoMinhaDoFormulario.ParaResponder),
            Item(2, SituacaoMinhaDoFormulario.ParaResponder),
            Item(3, SituacaoMinhaDoFormulario.Disponivel),
            Item(4, SituacaoMinhaDoFormulario.Respondido),
        ];

        var contagens = RegrasDosMeusFormularios.Contar(itens);

        Assert.Equal((3, 2, 1, 1), (contagens.Todos, contagens.ParaResponder, contagens.Disponiveis, contagens.Respondidos));
    }
}
