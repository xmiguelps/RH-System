using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>Uma pessoa avaliada (<c>FormularioAvaliadoResumo</c>): <c>TotalRespostas</c> vazio no anônimo com menos de 3 participações (a tela escreve "menos de 3 respostas"), <c>Esperadas</c> quantas pessoas deviam avaliá-la e <c>ResultadoLiberado</c> se há pergunta para mostrar.</summary>
public sealed record FormularioAvaliadoResumo(FuncionarioResumo Funcionario, int? TotalRespostas, int Esperadas, bool ResultadoLiberado);

/// <summary>A lista de avaliados (<c>FormularioAvaliadosLista</c>): <c>MinimoPorAvaliado</c> é 3 na avaliação anônima e vazio na com nome.</summary>
public sealed record FormularioAvaliadosLista(int? MinimoPorAvaliado, IReadOnlyList<FormularioAvaliadoResumo> Itens);

/// <summary>Um avaliado já apurado, antes de ganhar os dados da pessoa: o número, o total REAL (o caso de uso o esconde no anônimo abaixo do mínimo), as esperadas e se o resultado está liberado.</summary>
public sealed record AvaliadoApurado(int AvaliadoId, int Total, int Esperadas, bool ResultadoLiberado);

/// <summary>
/// As contas puras da lista de avaliados. Entram na lista os avaliados que ainda são esperados (os pares de quem deve responder: na hora com nome, a fotografia no anônimo) E os que já têm resposta mesmo tendo saído do público. <c>Esperadas</c> = quantos pares têm o avaliado (0 para quem saiu).
/// <c>ResultadoLiberado</c>: com nome, ao menos uma resposta; anônimo, o mínimo de participações E ao menos uma pergunta que chegue ao mínimo (<paramref name="temPerguntaParaMostrar"/>).
/// </summary>
public static class RegrasDosAvaliados
{
    public static IReadOnlyList<AvaliadoApurado> Apurar(bool anonimo, IReadOnlyList<ParDeResposta> esperados, IReadOnlyDictionary<int, int> totais, Func<int, bool> temPerguntaParaMostrar)
    {
        var avaliados = esperados.Where(p => p.AvaliadoId is not null).Select(p => p.AvaliadoId!.Value).Concat(totais.Keys).Distinct().Order();
        return [.. avaliados.Select(a =>
        {
            var total = totais.GetValueOrDefault(a);
            var liberado = anonimo ? total >= AgregacaoDeResultado.MinimoNoAnonimo && temPerguntaParaMostrar(a) : total > 0;
            return new AvaliadoApurado(a, total, esperados.Count(p => p.AvaliadoId == a), liberado);
        })];
    }
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/avaliados</c> (só o RH): as pessoas avaliadas numa avaliação, por ordem de nome e sem paginação. Ordem das recusas: 404, 422 <c>FORMULARIO_NAO_E_AVALIACAO</c>, 409 <c>RESULTADO_ANONIMO_INDISPONIVEL</c> (anônimo que não está ENCERRADO DE VERDADE, pela mesma função de situação que considera o prazo
/// vencido: a lista é, na prática, a contagem de respostas por avaliado). Com nome a contagem sai sempre (a de envios, a mesma de <c>obterResultadoFormulario</c>); no anônimo é a de participações e o avaliado com menos de 3 vem com <c>totalRespostas</c> nulo (nunca diz quantas), e o resultado só é
/// liberado com 3 participações E uma pergunta com 3 respostas ou mais.
/// </summary>
public sealed class ListarAvaliadosDoFormularioCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeResultado resultado, IConsultaDeAcompanhamento acompanhamento, IDestinatariosDoFormulario destinatarios, IRelogio relogio)
{
    public async Task<FormularioAvaliadosLista> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Tipo != TipoDeFormulario.Avaliacao)
        {
            throw MensagensDosFormularios.FormularioNaoEAvaliacao.ComoProblema();
        }

        var agora = relogio.AgoraUtc;
        if (lido.Anonimo && RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agora) != SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.ResultadoAnonimoIndisponivel.ComoProblema();
        }

        var quem = (await destinatarios.ObterAsync(lido.Id, FusoBrasil.Hoje(agora), ct)).OuNaoEncontrado();
        IReadOnlyDictionary<int, int> totais;
        Func<int, bool> temPerguntaParaMostrar = _ => true;
        if (lido.Anonimo)
        {
            totais = await resultado.ParticipacoesPorAvaliadoAsync(lido.Id, ct);
            var respostas = await resultado.RespostasAsync(lido.Id, null, ct);
            temPerguntaParaMostrar = avaliado => AgregacaoDeResultado.Agregar(lido.Perguntas, [.. respostas.Where(r => r.AvaliadoId == avaliado)], AgregacaoDeResultado.MinimoNoAnonimo).Perguntas.Count > 0;
        }
        else
        {
            totais = (await acompanhamento.EnviosAsync(lido.Id, ct)).Where(e => e.AvaliadoId is not null).GroupBy(e => e.AvaliadoId!.Value).ToDictionary(g => g.Key, g => g.Count());
        }

        var apurados = RegrasDosAvaliados.Apurar(lido.Anonimo, quem.Pares, totais, temPerguntaParaMostrar);
        var pessoas = (await acompanhamento.PessoasAsync([.. apurados.Select(a => a.AvaliadoId)], ct)).ToDictionary(p => p.Id);
        var itens = apurados
            .Where(a => pessoas.ContainsKey(a.AvaliadoId))
            .Select(a => (Apurado: a, Pessoa: pessoas[a.AvaliadoId]))
            .OrderBy(x => x.Pessoa.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Pessoa.Id)
            .Select(x => new FormularioAvaliadoResumo(
                new FuncionarioResumo(x.Pessoa.Id, x.Pessoa.Nome, x.Pessoa.Ativo, x.Pessoa.Cargo, x.Pessoa.Setor),
                lido.Anonimo && x.Apurado.Total < AgregacaoDeResultado.MinimoNoAnonimo ? null : x.Apurado.Total,
                x.Apurado.Esperadas,
                x.Apurado.ResultadoLiberado))
            .ToList();
        return new FormularioAvaliadosLista(lido.Anonimo ? AgregacaoDeResultado.MinimoNoAnonimo : null, itens);
    }
}
