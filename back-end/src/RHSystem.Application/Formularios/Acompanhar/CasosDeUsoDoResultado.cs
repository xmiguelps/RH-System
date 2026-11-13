using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Exportacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/resultado</c> (só o RH): o resultado geral, pergunta por pergunta. Ordem das recusas: 404, 400 (<c>avaliadoId</c> obrigatório nas avaliações e proibido nos outros tipos), 409 <c>RESULTADO_ANONIMO_INDISPONIVEL</c> (anônimo que não está ENCERRADO DE VERDADE, pela mesma função de
/// situação que considera o prazo vencido), 422 <c>RESULTADO_SEM_RESPOSTAS_SUFICIENTES</c> (avaliação anônima com menos de 3 respostas para o avaliado; sem o total dele). Com nome sai em qualquer situação e sem mínimo; no anônimo cada pergunta precisa de 3 respostas para aparecer. Nada daqui identifica, data ou ordena uma resposta anônima.
/// </summary>
public sealed class ObterResultadoDoFormularioCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeResultado resultado, IConsultaDeAcompanhamento acompanhamento, IRelogio relogio)
{
    public async Task<FormularioResultado> ExecutarAsync(int formularioId, int? avaliadoId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        if (lido.Tipo == TipoDeFormulario.Avaliacao)
        {
            if (avaliadoId is not { } id || id < 1)
            {
                erros.Adicionar("avaliadoId", "Informe de qual avaliado se quer o resultado (um número de 1 em diante).");
            }
        }
        else if (avaliadoId is not null)
        {
            erros.Adicionar("avaliadoId", "Este formulário não tem avaliado.");
        }

        erros.LancarSeHouver();

        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, relogio.AgoraUtc);
        if (lido.Anonimo && situacao != SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.ResultadoAnonimoIndisponivel.ComoProblema();
        }

        FuncionarioResumo? avaliado = null;
        if (avaliadoId is { } quem)
        {
            var pessoa = (await acompanhamento.PessoasAsync([quem], ct)).FirstOrDefault().OuNaoEncontrado();
            avaliado = new FuncionarioResumo(pessoa.Id, pessoa.Nome, pessoa.Ativo, pessoa.Cargo, pessoa.Setor);
        }

        var total = await resultado.TotalAsync(lido.Id, lido.Anonimo, avaliadoId, ct);
        if (lido.Anonimo && avaliadoId is not null && total < AgregacaoDeResultado.MinimoNoAnonimo)
        {
            throw MensagensDosFormularios.ResultadoSemRespostasDoAvaliado.ComoProblema();
        }

        int? minimo = lido.Anonimo ? AgregacaoDeResultado.MinimoNoAnonimo : null;
        var (perguntas, ocultas) = AgregacaoDeResultado.Agregar(lido.Perguntas, await resultado.RespostasAsync(lido.Id, avaliadoId, ct), minimo);
        return new FormularioResultado(lido.Id, lido.Titulo, lido.Anonimo, situacao, total, minimo, ocultas, avaliado, perguntas);
    }
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/exportacao</c> (só o RH): o CSV (UTF-8 com BOM, <c>;</c>, toda célula de texto por <see cref="CsvSeguro.Texto"/>, datas em -03:00 <c>yyyy-MM-dd HH:mm</c>, <c>formulario-{id}-respostas.csv</c>). <b>Com nome ou por link</b>: uma linha por resposta
/// (<c>formulario;respondente;email;setor;avaliado;pergunta;resposta;respondido_em</c>; candidato sem setor; arquivo = o nome). <b>Anônimo</b>: só o resultado geral das perguntas com 3 respostas ou mais (nas avaliações, uma seção por avaliado com 3 respostas ou mais, e a regra por pergunta dentro dele; quem ficou abaixo do mínimo sai sem aviso de quantos),
/// uma linha por alternativa, por valor da escala e por texto (em ordem alfabética), e só DEPOIS de encerrado (409 <c>RESULTADO_ANONIMO_INDISPONIVEL</c>; nada que chegue a 3: 422 <c>RESULTADO_SEM_RESPOSTAS_SUFICIENTES</c>). Grava o evento <c>BAIXOU</c> só com o título do formulário, depois de gerar o arquivo.
/// </summary>
public sealed class ExportarRespostasDoFormularioCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeResultado resultado, IConsultaDeAcompanhamento acompanhamento, IAuditor auditor, IRelogio relogio)
{
    private static readonly string[] TitulosComNome = ["formulario", "respondente", "email", "setor", "avaliado", "pergunta", "resposta", "respondido_em"];

    private static readonly string[] TitulosAnonimo = ["pergunta", "opcao", "total", "percentual"];

    private static readonly string[] TitulosAnonimoAvaliacao = ["avaliado", "pergunta", "opcao", "total", "percentual"];

    public async Task<ArquivoExportado> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, relogio.AgoraUtc);
        byte[] conteudo;
        if (lido.Anonimo)
        {
            if (situacao != SituacaoDoFormulario.Encerrado)
            {
                throw MensagensDosFormularios.ResultadoAnonimoIndisponivel.ComoProblema();
            }

            conteudo = await AnonimoAsync(lido, ct);
        }
        else
        {
            var linhas = await resultado.LinhasParaExportarAsync(lido.Id, ct);
            conteudo = CsvSeguro.Escrever(TitulosComNome, linhas.Select(l => ComNome(lido.Titulo, l)));
        }

        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Baixou, $"Exportou as respostas do formulário \"{lido.Titulo}\" em CSV.", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id),
            ct);
        return new ArquivoExportado(conteudo, $"formulario-{lido.Id.ToString(CultureInfo.InvariantCulture)}-respostas.csv");
    }

    private async Task<byte[]> AnonimoAsync(FormularioLido lido, CancellationToken ct)
    {
        var respostas = await resultado.RespostasAsync(lido.Id, null, ct);
        var linhas = new List<IReadOnlyList<string>>();
        if (lido.Tipo != TipoDeFormulario.Avaliacao)
        {
            linhas.AddRange(Linhas(null, AgregacaoDeResultado.Agregar(lido.Perguntas, respostas, AgregacaoDeResultado.MinimoNoAnonimo).Perguntas));
        }
        else
        {
            var participacoes = await resultado.ParticipacoesPorAvaliadoAsync(lido.Id, ct);
            var liberados = participacoes.Where(p => p.Value >= AgregacaoDeResultado.MinimoNoAnonimo).Select(p => p.Key).ToList();
            var nomes = (await acompanhamento.PessoasAsync(liberados, ct)).ToDictionary(p => p.Id, p => p.Nome);
            foreach (var avaliado in liberados.Where(nomes.ContainsKey).OrderBy(a => nomes[a], StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a))
            {
                var dele = respostas.Where(r => r.AvaliadoId == avaliado).ToList();
                linhas.AddRange(Linhas(nomes[avaliado], AgregacaoDeResultado.Agregar(lido.Perguntas, dele, AgregacaoDeResultado.MinimoNoAnonimo).Perguntas));
            }
        }

        if (linhas.Count == 0)
        {
            throw MensagensDosFormularios.ResultadoSemRespostasNaExportacao.ComoProblema();
        }

        return CsvSeguro.Escrever(lido.Tipo == TipoDeFormulario.Avaliacao ? TitulosAnonimoAvaliacao : TitulosAnonimo, linhas);
    }

    private static IEnumerable<IReadOnlyList<string>> Linhas(string? avaliado, IReadOnlyList<FormularioResultadoPergunta> perguntas)
    {
        foreach (var pergunta in perguntas)
        {
            foreach (var celulas in LinhasDaPergunta(pergunta))
            {
                yield return avaliado is null ? celulas : [CsvSeguro.Texto(avaliado), .. celulas];
            }
        }
    }

    private static IEnumerable<IReadOnlyList<string>> LinhasDaPergunta(FormularioResultadoPergunta pergunta)
    {
        var enunciado = CsvSeguro.Texto(pergunta.Texto);
        foreach (var alternativa in pergunta.Alternativas)
        {
            yield return [enunciado, CsvSeguro.Texto(alternativa.Texto), CsvSeguro.Numero(alternativa.Total), Decimal(alternativa.Percentual)];
        }

        if (pergunta.Escala is { } escala)
        {
            foreach (var valor in escala.Distribuicao)
            {
                yield return [enunciado, CsvSeguro.Numero(valor.Valor), CsvSeguro.Numero(valor.Total), Decimal(AgregacaoDeResultado.Percentual(valor.Total, pergunta.TotalRespostas))];
            }
        }

        foreach (var texto in pergunta.Textos)
        {
            yield return [enunciado, CsvSeguro.Texto(texto), string.Empty, string.Empty];
        }
    }

    private static IReadOnlyList<string> ComNome(string titulo, LinhaDeExportacaoDeResposta l) =>
    [
        CsvSeguro.Texto(titulo),
        CsvSeguro.Texto(l.Respondente),
        CsvSeguro.Texto(l.Email),
        CsvSeguro.Texto(l.Setor),
        CsvSeguro.Texto(l.Avaliado),
        CsvSeguro.Texto(l.PerguntaTexto),
        CsvSeguro.Texto(l.Resposta),
        l.RespondidoEmUtc is { } quando ? FusoBrasil.ParaLocal(quando).ToString("yyyy-MM-dd HH:mm", CultureInfo.InvariantCulture) : string.Empty,
    ];

    /// <summary>O decimal com vírgula (o Excel em português lê como número); nunca começa com <c>=</c>, <c>+</c>, <c>-</c> nem <c>@</c>.</summary>
    private static string Decimal(decimal valor) => valor.ToString("0.0", CultureInfo.GetCultureInfo("pt-BR"));
}
