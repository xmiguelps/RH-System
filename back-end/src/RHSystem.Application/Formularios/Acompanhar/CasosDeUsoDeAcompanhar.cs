using System.Globalization;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/respondentes</c>: a tela "Quem respondeu" (só o RH). 404 (não existe), 400 (todos os campos juntos), 422 <c>FORMULARIO_POR_LINK</c> (a lista dos candidatos é outra). Quem deve responder é calculado NA HORA (a fotografia só no anônimo publicado); ordem por nome. <c>resumo</c> não muda com os filtros.
/// <b>Anônimo</b>: <c>itens</c> sempre vazio e <c>total</c> 0 (busca, setor e aba ignorados); os números só depois de ENCERRADO DE VERDADE (a mesma função de situação, que considera o prazo vencido), antes só <c>esperados</c>. <b>Sugestão</b>: uma linha por envio, o mais recente primeiro para a mesma pessoa, sem "faltam" nem taxa.
/// <b>Avaliação</b>: uma linha por avaliação. Quem respondeu e já saiu do público aparece como Respondeu mas não entra nas contas do resumo.
/// </summary>
public sealed class ListarRespondentesDoFormularioCasoDeUso(IConsultaDeFormularios consulta, IDestinatariosDoFormulario destinatarios, IConsultaDeAcompanhamento acompanhamento, IRelogio relogio)
{
    public async Task<FormularioRespondentesRh> ExecutarAsync(int formularioId, int pagina, int tamanhoPagina, string? busca, string? situacao, int? setorId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        SituacaoDoRespondente? situacaoLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => situacaoLida = EnumsDeConsulta.Ler<SituacaoDoRespondente>(situacao, "situacao"));
        if (setorId is < 1)
        {
            erros.Adicionar("setorId", "O setor precisa ser um número de 1 em diante.");
        }

        erros.LancarSeHouver();
        if (lido.Alvo == AlvoDoFormulario.Link)
        {
            throw MensagensDosFormularios.FormularioPorLinkNaLista.ComoProblema();
        }

        var agora = relogio.AgoraUtc;
        var encerrado = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agora) == SituacaoDoFormulario.Encerrado;
        var quem = (await destinatarios.ObterAsync(formularioId, FusoBrasil.Hoje(agora), ct)).OuNaoEncontrado();
        var sugestao = lido.Tipo == TipoDeFormulario.Outro;
        if (lido.Anonimo)
        {
            var participacoes = encerrado ? await acompanhamento.ParticipacoesAsync(formularioId, ct) : 0;
            return new FormularioRespondentesRh([], paginacao!.Pagina, paginacao.TamanhoPagina, 0, 0, true, RegrasDosRespondentes.Resumo(sugestao, true, encerrado, quem, [], participacoes));
        }

        var envios = await acompanhamento.EnviosAsync(formularioId, ct);
        var resumo = RegrasDosRespondentes.Resumo(sugestao, false, encerrado, quem, envios, 0);
        var linhas = RegrasDosRespondentes.Linhas(sugestao, quem, envios);
        var ids = linhas.SelectMany(l => l.AvaliadoId is { } avaliado ? new[] { l.PessoaId, avaliado } : [l.PessoaId]).Distinct().ToList();
        var pessoas = (await acompanhamento.PessoasAsync(ids, ct)).ToDictionary(p => p.Id);
        var filtro = new FiltroDeRespondentes(situacaoLida, setorId, paginacao!.Busca);

        var itens = linhas
            .Where(l => pessoas.ContainsKey(l.PessoaId) && (l.AvaliadoId is null || pessoas.ContainsKey(l.AvaliadoId.Value)))
            .Select(l => (Linha: l, Quem: pessoas[l.PessoaId], Avaliado: l.AvaliadoId is { } a ? pessoas[a] : null))
            .Where(x => Passa(x.Linha, x.Quem, filtro))
            .OrderBy(x => x.Quem.Nome, StringComparer.CurrentCultureIgnoreCase)
            .ThenBy(x => x.Avaliado?.Nome ?? string.Empty, StringComparer.CurrentCultureIgnoreCase)
            .ThenByDescending(x => x.Linha.RespondidoEmUtc)
            .ThenBy(x => x.Quem.Id)
            .ThenBy(x => x.Avaliado?.Id ?? 0)
            .ToList();
        var pagina2 = itens.Skip(paginacao.Pular).Take(paginacao.TamanhoPagina).Select(x => Montar(x.Linha, x.Quem, x.Avaliado)).ToList();
        return new FormularioRespondentesRh(pagina2, paginacao.Pagina, paginacao.TamanhoPagina, itens.Count, (int)(((long)itens.Count + paginacao.TamanhoPagina - 1) / paginacao.TamanhoPagina), false, resumo);
    }

    private static bool Passa(LinhaDeRespondente linha, PessoaDaLista quem, FiltroDeRespondentes filtro) =>
        (filtro.Situacao is null || (linha.RespondidoEmUtc is not null) == (filtro.Situacao == SituacaoDoRespondente.Respondido))
        && (filtro.SetorId is null || quem.SetorId == filtro.SetorId)
        && (filtro.Busca is null || CultureInfo.InvariantCulture.CompareInfo.IndexOf(quem.Nome, filtro.Busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0);

    private static FormularioRespondenteRh Montar(LinhaDeRespondente linha, PessoaDaLista quem, PessoaDaLista? avaliado) =>
        new(Resumo(quem), avaliado is null ? null : Resumo(avaliado), linha.RespondidoEmUtc is null ? SituacaoDoRespondente.NaoRespondeu : SituacaoDoRespondente.Respondido, linha.RespondidoEmUtc, linha.RespondidoEmUtc is not null);

    private static FuncionarioResumo Resumo(PessoaDaLista p) => new(p.Id, p.Nome, p.Ativo, p.Cargo, p.Setor);
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/respostas/funcionarios/{funcionarioId}</c>: o que uma pessoa respondeu (só COM NOME). 404 (formulário), 422 <c>FORMULARIO_ANONIMO</c> (a resposta anônima não guarda quem respondeu), 400 (<c>avaliadoId</c> obrigatório nas avaliações e proibido nos outros tipos; <c>respondidoEmUtc</c>
/// obrigatório na Sugestão, que separa um envio do outro pelo instante, e proibido nos outros), 404 se a pessoa não respondeu (naquele avaliado ou envio). Vem TODA pergunta do formulário, na ordem; o arquivo só com <c>respostaId</c> e nome.
/// </summary>
public sealed class ObterRespostasDeFuncionarioCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeAcompanhamento acompanhamento)
{
    public async Task<RespostaFormularioFuncionario> ExecutarAsync(int formularioId, int funcionarioId, int? avaliadoId, DateTime? respondidoEmUtc, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Anonimo)
        {
            throw MensagensDosFormularios.FormularioAnonimo.ComoProblema();
        }

        var erros = new ColetorDeErros();
        if (lido.Tipo == TipoDeFormulario.Avaliacao)
        {
            if (avaliadoId is not { } id || id < 1)
            {
                erros.Adicionar("avaliadoId", "Informe de qual avaliação se fala (o avaliado, um número de 1 em diante).");
            }
        }
        else if (avaliadoId is not null)
        {
            erros.Adicionar("avaliadoId", "Este formulário não tem avaliado.");
        }

        if (lido.Tipo == TipoDeFormulario.Outro)
        {
            if (respondidoEmUtc is null)
            {
                erros.Adicionar("respondidoEmUtc", "Informe qual envio se quer ver (o respondidoEmUtc da lista).");
            }
        }
        else if (respondidoEmUtc is not null)
        {
            erros.Adicionar("respondidoEmUtc", "Só a Sugestão tem mais de um envio por pessoa.");
        }

        erros.LancarSeHouver();

        var instante = respondidoEmUtc is { } dado ? InstanteAoSegundo.De(EmUtc(dado)) : (DateTime?)null;
        var lidas = await acompanhamento.DoEnvioAsync(lido.Id, funcionarioId, avaliadoId, instante, ct);
        if (lidas.Count == 0)
        {
            throw new NaoEncontradoException();
        }

        var pessoas = (await acompanhamento.PessoasAsync(avaliadoId is { } a ? [funcionarioId, a] : [funcionarioId], ct)).ToDictionary(p => p.Id);
        var quem = pessoas[funcionarioId];
        var avaliado = avaliadoId is { } b && pessoas.TryGetValue(b, out var pessoaAvaliada) ? new FuncionarioResumo(pessoaAvaliada.Id, pessoaAvaliada.Nome, pessoaAvaliada.Ativo, pessoaAvaliada.Cargo, pessoaAvaliada.Setor) : null;
        return new RespostaFormularioFuncionario(lido.Id, lido.Titulo, new FuncionarioResumo(quem.Id, quem.Nome, quem.Ativo, quem.Cargo, quem.Setor), avaliado, lidas.Max(l => l.RespondidaEmUtc) ?? default, MontadorDeRespostasDadas.Montar(lido, lidas));
    }

    /// <summary>O instante em UTC, qualquer que seja o jeito de o texto da consulta ter chegado: com fuso (já convertido), sem fuso (vale como UTC).</summary>
    private static DateTime EmUtc(DateTime valor) => valor.Kind switch
    {
        DateTimeKind.Local => valor.ToUniversalTime(),
        DateTimeKind.Utc => valor,
        _ => DateTime.SpecifyKind(valor, DateTimeKind.Utc),
    };
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/arquivos/{respostaId}/download</c>: o link temporário do arquivo de uma resposta (só o RH), baixa como anexo; o caminho do armazenamento nunca aparece. 404 (formulário, resposta de OUTRO formulário, resposta sem arquivo e o formulário anônimo inteiro). Cada link
/// pedido grava o evento <c>BAIXOU</c> só com o título do formulário (nunca o nome do arquivo nem o da pessoa).
/// </summary>
public sealed class BaixarArquivoDeRespostaCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeAcompanhamento acompanhamento, IAuditor auditor, IArmazenamentoDeArquivos armazenamento)
{
    public async Task<LinkTemporario> ExecutarAsync(int formularioId, int respostaId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Anonimo)
        {
            throw new NaoEncontradoException();
        }

        var arquivo = (await acompanhamento.ArquivoAsync(formularioId, respostaId, ct)).OuNaoEncontrado();
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Baixou, $"Baixou um arquivo de resposta do formulário \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(arquivo.Caminho, NomeParaDownload.Limpar(arquivo.Nome, Path.GetExtension(arquivo.Caminho)), ct);
    }
}
