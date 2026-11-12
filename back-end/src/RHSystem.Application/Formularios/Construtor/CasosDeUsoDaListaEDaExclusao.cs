using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// Quantos ainda podem responder o formulário PUBLICADO, para pedir confirmação ao encerrar e ao excluir. <c>null</c> = pede SEMPRE sem número (o anônimo: mesmo com todos já tendo participado, encerrar ou excluir direto diria que não falta ninguém; nem olha respostas).
/// Formulário por link e Sugestão: 0 (ninguém é obrigado a responder). Com nome: os pares que devem responder (calculados na hora) menos os que já responderam (nas avaliações, avaliações).
/// </summary>
internal static class PendentesDoFormulario
{
    public static async Task<int?> ContarAsync(IConsultaDeFormularios consulta, IDestinatariosDoFormulario destinatarios, FormularioLido lido, DateOnly hoje, CancellationToken ct)
    {
        if (lido.Anonimo)
        {
            return null;
        }

        if (lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores) || lido.Tipo == TipoDeFormulario.Outro)
        {
            return 0;
        }

        var quem = (await destinatarios.ObterAsync(lido.Id, hoje, ct)).OuNaoEncontrado();
        return quem.QuemFalta(await consulta.ParesQueResponderamAsync(lido.Id, ct)).Count;
    }
}

/// <summary>
/// <c>GET /api/rh/formularios</c>: a lista do RH (tela 16). Ordem padrão: os mais recentes primeiro (<c>criadoEm</c> decrescente); com <c>ordenarPor</c> (<c>criadoEm</c>, <c>titulo</c>, <c>prazo</c>) e sem <c>direcao</c> vale ASC; só <c>direcao</c> vale a coluna padrão naquela direção. A situação do
/// filtro e a da linha são as CALCULADAS (o prazo vencido vale encerrado). <c>contagemPorTipo</c> respeita a busca e a situação e ignora o tipo. Por linha: o resumo de quem responde e os números por alvo e anonimato (ver <see cref="FormularioRhItem"/>); a taxa é inteira com
/// arredondamento para longe do zero (5 de 8 = 63%). Erros de campo (<c>tipo</c>, <c>situacao</c>, <c>ordenarPor</c>, <c>direcao</c>, página, busca) vêm juntos num 400.
/// </summary>
public sealed class ListarFormulariosRhCasoDeUso(IConsultaDeFormularios consulta, IDestinatariosDoFormulario destinatarios, IRelogio relogio)
{
    private static readonly string[] ValoresDeOrdenarPor = ["criadoEm", "titulo", "prazo"];

    public async Task<FormularioRhLista> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? tipo, string? situacao, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        TipoDeFormulario? tipoLido = null;
        SituacaoDoFormulario? situacaoLida = null;
        DirecaoDeOrdenacao? direcaoLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => tipoLido = EnumsDeConsulta.Ler<TipoDeFormulario>(tipo, "tipo"));
        LeituraDeFiltros.Tentar(erros, () => situacaoLida = EnumsDeConsulta.Ler<SituacaoDoFormulario>(situacao, "situacao"));
        LeituraDeFiltros.Tentar(erros, () => direcaoLida = EnumsDeConsulta.Ler<DirecaoDeOrdenacao>(direcao, "direcao"));
        if (!string.IsNullOrEmpty(ordenarPor) && !ValoresDeOrdenarPor.Contains(ordenarPor, StringComparer.Ordinal))
        {
            erros.Adicionar("ordenarPor", $"Valor inválido. Use um destes: {string.Join(", ", ValoresDeOrdenarPor)}.");
        }

        erros.LancarSeHouver();

        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var parametros = paginacao! with { OrdenarPor = string.IsNullOrEmpty(ordenarPor) ? null : ordenarPor, Direcao = direcaoLida };
        var (linhas, total, contagem) = await consulta.ListarAsync(new FiltroDeFormulariosRh(tipoLido, situacaoLida), parametros, hoje, ct);
        var respostas = await consulta.RespostasPorFormularioAsync([.. linhas.Select(l => l.Id)], ct);

        var itens = new List<FormularioRhItem>(linhas.Count);
        foreach (var linha in linhas)
        {
            var quem = (await destinatarios.ObterAsync(linha.Id, hoje, ct)).OuNaoEncontrado();
            itens.Add(Montar(linha, quem, respostas[linha.Id], agora));
        }

        return new FormularioRhLista(itens, parametros.Pagina, parametros.TamanhoPagina, total, (int)(((long)total + parametros.TamanhoPagina - 1) / parametros.TamanhoPagina), contagem);
    }

    private static FormularioRhItem Montar(FormularioDaLista l, QuemDeveResponder quem, RespostasDoFormulario respostas, DateTime agora)
    {
        var situacao = RegraDaSituacaoDoFormulario.De(l.Ativo, l.PublicadoEmUtc, l.EncerradoEmUtc, l.Prazo, agora);
        int? esperados = l.Alvo is AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores && l.Tipo != TipoDeFormulario.Outro ? quem.Esperados : null;
        int? respondidos = l.Alvo == AlvoDoFormulario.Link
            ? respostas.Candidatos
            : l.Tipo == TipoDeFormulario.Outro
                ? respostas.Envios
                : l.Anonimo
                    ? (situacao == SituacaoDoFormulario.Encerrado ? respostas.Participacoes : null)
                    : quem.Pares.Count - quem.QuemFalta(respostas.Pares).Count;
        int? taxa = respondidos is { } r && esperados is > 0 and { } e ? (int)Math.Round(r * 100.0 / e, MidpointRounding.AwayFromZero) : null;
        var encerrado = l.EncerradoEmUtc ?? (situacao == SituacaoDoFormulario.Encerrado && l.Prazo is { } prazo ? InstantesDoFormulario.EncerramentoNominal(prazo) : null);
        return new FormularioRhItem(l.Id, l.Titulo, l.Tipo, l.Sentido, l.Anonimo, situacao, l.Alvo, QuemRespondeResumo.Texto(quem.Resumo), respondidos, esperados, taxa, l.Prazo, encerrado, l.CriadoEmUtc);
    }
}

/// <summary>
/// <c>DELETE /api/rh/formularios/{formularioId}?confirmar=</c>: a exclusão FÍSICA, em qualquer situação, em UMA transação (respostas, participações, destinatários, candidatos, alternativas, perguntas, formulário); depois apaga os arquivos do armazenamento (a falha de um arquivo não desfaz
/// a exclusão: sobra para a limpeza de órfãos). Cancela as tarefas e os e-mails de lembrete pendentes: nada roda depois. Só o PUBLICADO pede confirmação (<c>pendentes</c>, ou sempre e sem número no anônimo); nas outras situações ninguém está esperando. Evento <c>EXCLUIU</c> com o título e
/// QUANTAS respostas tinha, SEM a contagem no anônimo ainda não encerrado (a contagem diria o andamento). Duas abas excluindo: um 204 e um 404.
/// </summary>
public sealed class ExcluirFormularioCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRepositorio formularios,
    IDestinatariosDoFormulario destinatarios,
    IAgendamentoDoFormulario agendamento,
    IArmazenamentoDeArquivos armazenamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task ExecutarAsync(int formularioId, bool confirmar, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var situacao = MapeadorDoFormulario.SituacaoDe(lido, agora);
        if (!confirmar && situacao == SituacaoDoFormulario.Publicado)
        {
            var pendentes = await PendentesDoFormulario.ContarAsync(consulta, destinatarios, lido, FusoBrasil.Hoje(agora), ct);
            if (pendentes is not 0)
            {
                throw MensagensDosFormularios.ConfirmacaoNecessaria(pendentes);
            }
        }

        var recebido = (await consulta.RespostasPorFormularioAsync([formularioId], ct))[formularioId];
        var quantas = lido.Anonimo ? recebido.Participacoes : recebido.Envios + recebido.Candidatos;
        var descricao = lido.Anonimo && situacao != SituacaoDoFormulario.Encerrado
            ? $"Excluiu o formulário \"{lido.Titulo}\"."
            : $"Excluiu o formulário \"{lido.Titulo}\" com {quantas} {(quantas == 1 ? "resposta" : "respostas")}.";

        var arquivos = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var caminhos = await formularios.ExcluirAsync(formularioId, ct);
                if (caminhos is null)
                {
                    return null;
                }

                await agendamento.CancelarTudoAsync(formularioId, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Excluiu, descricao, $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                await unidade.SalvarAsync(ct);
                return caminhos;
            },
            ct) ?? throw new NaoEncontradoException();

        foreach (var caminho in arquivos)
        {
            try
            {
                await armazenamento.ApagarAsync(caminho, CancellationToken.None);
            }
            catch (Exception)
            {
                // o formulário já foi excluído: o arquivo que não saiu agora sai na limpeza de órfãos
            }
        }
    }
}
