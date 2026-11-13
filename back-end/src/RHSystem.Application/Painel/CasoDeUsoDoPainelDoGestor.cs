using Microsoft.Extensions.Logging;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Advertencias;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Comunicacao;
using RHSystem.Domain.Formularios;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Painel;

/// <summary>
/// <c>GET /api/gestor/painel</c>: o painel da equipe do gestor, só dele, sobre a MESMA equipe de <c>GET /api/gestor/equipe</c> (ativos do setor dele, sem ele). Cada parte sai da consulta da rota de origem e todas em número CONSTANTE de consultas, com 3 ou com 30 pessoas (nenhuma consulta por pessoa): a
/// situação de hoje e o cargo da lista da equipe, o saldo do banco de horas, os dias de férias disponíveis (o mesmo saldo da rota de férias), a primeira entrada de hoje, o fim das férias de hoje, as advertências NÃO canceladas do ano e a avaliação. As pessoas saem em ordem alfabética do nome (empate: o id).
/// <list type="bullet">
/// <item><c>aguardandoAprovacao.ferias</c>: os itens de <c>listarFeriasDaEquipe</c> (esperando o gestor), TODOS, sem paginação (as páginas são lidas até acabar); <c>ajustesDePonto</c>: os pedidos de ajuste do ponto na etapa do gestor, do mais antigo ao mais novo.</item>
/// <item><c>avaliacao</c>: o formulário <c>GESTOR_AVALIA_EQUIPE</c> publicado em que o gestor tem pares (o de prazo mais próximo; sem prazo por último); o <c>ciclo</c> é o que vem depois de "ciclo " no título, senão o título. Cada pessoa: <c>RESPONDIDA</c> (o gestor já avaliou), <c>PENDENTE</c> (falta avaliar) ou <c>NAO_SE_APLICA</c> (não há par do gestor com ela, ou ela está de férias hoje, como o exemplo do contrato); sem formulário aberto, <c>null</c> e a coluna some.</item>
/// </list>
/// <b>Uma parte que falha sai vazia</b> (a lista, o número, a coluna) e registra um <c>Warning</c> só com o nome da parte e o tipo da exceção. O gestor sem setor recebe <c>setor</c> nulo, zeros e listas vazias.
/// </summary>
public sealed class ObterPainelDaEquipeCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDaEquipe consultaDaEquipe,
    ListarFeriasDaEquipeCasoDeUso feriasDaEquipe,
    ListarPedidosDaEquipeCasoDeUso ajustesDaEquipe,
    ISaldoDoBancoDeHoras saldoDoBanco,
    IConsultaDeFerias consultaDeFerias,
    IConsultaDoPonto consultaDoPonto,
    IFeriasDoPonto feriasDoPonto,
    IConsultaDeAdvertencias advertencias,
    IConsultaDeMeusFormularios meusFormularios,
    IRelogio relogio,
    ILogger<ObterPainelDaEquipeCasoDeUso> log)
{
    private const int TamanhoDaPagina = 100;

    public async Task<PainelGestorDto> ExecutarAsync(CancellationToken ct)
    {
        var equipe = await escopo.ObterEquipeAsync(ct);
        if (equipe is null)
        {
            return new PainelGestorDto(null, 0, new PainelGestorAprovacoesDto([], []), null, []);
        }

        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var lista = await IsolarAsync<FuncionarioEquipeResposta?>("equipe", async () => await consultaDaEquipe.ListarAsync(equipe, ct), null);
        var itens = lista?.Itens ?? [];
        var ids = itens.Select(i => i.Id).ToList();

        var aprovacoes = new PainelGestorAprovacoesDto(
            await IsolarAsync("férias esperando o gestor", () => TodasAsFeriasAsync(ct), []),
            await IsolarAsync("ajustes de ponto esperando o gestor", () => TodosOsAjustesAsync(ct), []));
        var saldos = await IsolarAsync("banco de horas", () => saldoDoBanco.ObterSaldosAsync(ids, ct), new Dictionary<int, int>());
        var dias = await IsolarAsync("férias disponíveis", () => consultaDeFerias.DiasDisponiveisDasPessoasAsync(ids, hoje, ct), new Dictionary<int, int>());
        var entradas = await IsolarAsync("primeira entrada de hoje", () => PrimeirasEntradasAsync(ids, hoje, ct), new Dictionary<int, string>());
        var feriasAte = await IsolarAsync("fim das férias", () => FimDasFeriasAsync(ids, hoje, ct), new Dictionary<int, DateOnly>());
        var advertenciasDoAno = await IsolarAsync("advertências do ano", () => AdvertenciasDoAnoAsync(hoje.Year, equipe, ct), new Dictionary<int, int>());
        var avaliacao = await IsolarAsync("avaliação", () => AvaliacaoAsync(equipe, hoje, agora, ids, ct), null);

        var pessoas = itens
            .OrderBy(i => i.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
            .ThenBy(i => i.Id)
            .Select(i => new PainelPessoaDaEquipeDto(
                new FuncionarioResumo(i.Id, i.Nome, i.Ativo, i.Cargo, i.Setor),
                new PainelPontoHojeDto(
                    i.SituacaoHoje,
                    i.SituacaoHoje == SituacaoHoje.Trabalhando ? entradas.GetValueOrDefault(i.Id) : null,
                    i.SituacaoHoje == SituacaoHoje.Ferias && feriasAte.TryGetValue(i.Id, out var fim) ? fim : null),
                saldos.GetValueOrDefault(i.Id),
                dias.GetValueOrDefault(i.Id),
                avaliacao?.SituacaoDe(i.Id, emFerias: i.SituacaoHoje == SituacaoHoje.Ferias),
                advertenciasDoAno.GetValueOrDefault(i.Id)))
            .ToList();

        return new PainelGestorDto(
            lista?.Setor is { } setor ? new PainelSetorDto(setor.Id, setor.Nome) : null,
            lista?.Total ?? 0,
            aprovacoes,
            avaliacao?.Botao,
            pessoas);
    }

    private async Task<IReadOnlyList<FeriasPedidoAnalise>> TodasAsFeriasAsync(CancellationToken ct)
    {
        var todas = new List<FeriasPedidoAnalise>();
        for (var pagina = 1; ; pagina++)
        {
            var lida = await feriasDaEquipe.ExecutarAsync(pagina, TamanhoDaPagina, null, ct);
            todas.AddRange(lida.Itens);
            if (pagina >= lida.TotalPaginas)
            {
                return todas;
            }
        }
    }

    private async Task<IReadOnlyList<PainelAjusteDePontoPendenteDto>> TodosOsAjustesAsync(CancellationToken ct)
    {
        var todos = new List<PontoPedidoAjuste>();
        for (var pagina = 1; ; pagina++)
        {
            var lida = await ajustesDaEquipe.ExecutarAsync(pagina, TamanhoDaPagina, "PENDENTE", "GESTOR", null, null, null, ct);
            todos.AddRange(lida.Itens);
            if (pagina >= lida.TotalPaginas)
            {
                break;
            }
        }

        return [.. todos.OrderBy(p => p.CriadoEmUtc).ThenBy(p => p.Id).Select(p => new PainelAjusteDePontoPendenteDto(p.Id, p.Funcionario, p.Data, p.ResumoDaCorrecao, p.Justificativa, p.CriadoEmUtc))];
    }

    /// <summary>A primeira ENTRADA de hoje de cada pessoa, em <c>HH:mm</c> no fuso de Brasília.</summary>
    private async Task<IReadOnlyDictionary<int, string>> PrimeirasEntradasAsync(IReadOnlyCollection<int> ids, DateOnly hoje, CancellationToken ct) =>
        (await consultaDoPonto.BatidasAsync(ids, hoje, hoje, ct))
            .Where(b => b.Batida.Tipo == TipoDeMarcacao.Entrada)
            .GroupBy(b => b.FuncionarioId)
            .ToDictionary(g => g.Key, g => FusoBrasil.ParaLocal(g.Min(b => b.Batida.InstanteUtc)).ToString("HH:mm", System.Globalization.CultureInfo.InvariantCulture));

    /// <summary>O último dia das férias que cobrem hoje, de cada pessoa da equipe.</summary>
    private async Task<IReadOnlyDictionary<int, DateOnly>> FimDasFeriasAsync(IReadOnlyCollection<int> ids, DateOnly hoje, CancellationToken ct) =>
        (await feriasDoPonto.NoPeriodoAsync(hoje, hoje, ct))
            .Where(f => ids.Contains(f.FuncionarioId))
            .GroupBy(f => f.FuncionarioId)
            .ToDictionary(g => g.Key, g => g.Max(f => f.Fim));

    /// <summary>As advertências do ano que NÃO foram canceladas (a mesma regra única de contagem das outras telas).</summary>
    private async Task<IReadOnlyDictionary<int, int>> AdvertenciasDoAnoAsync(int ano, EquipeDoGestor equipe, CancellationToken ct) =>
        (await advertencias.DoAnoParaOResumoDaEquipeAsync(ano, equipe, ct)).Advertencias
            .Where(a => a.Situacao != SituacaoDaAdvertencia.Cancelada)
            .GroupBy(a => a.FuncionarioId)
            .ToDictionary(g => g.Key, g => g.Count());

    private async Task<AvaliacaoDoCiclo?> AvaliacaoAsync(EquipeDoGestor equipe, DateOnly hoje, DateTime agora, IReadOnlyCollection<int> ids, CancellationToken ct)
    {
        var cartoes = await meusFormularios.CartoesAsync(escopo.FuncionarioId, hoje, agora, soParaResponder: false, ct);
        var aberto = cartoes
            .Where(c => c.Tipo == TipoDeFormulario.Avaliacao && c.Sentido == SentidoDaAvaliacao.GestorAvaliaEquipe && RegraDaSituacaoDoFormulario.De(c.Ativo, c.PublicadoEmUtc, c.EncerradoEmUtc, c.Prazo, agora) == SituacaoDoFormulario.Publicado && c.MeusPares.Count > 0)
            .OrderBy(c => c.Prazo is null)
            .ThenBy(c => c.Prazo)
            .ThenBy(c => c.Id)
            .FirstOrDefault();
        return aberto is null ? null : new AvaliacaoDoCiclo(new PainelAvaliacaoDoCicloDto(Ciclo(aberto.Titulo), aberto.Id), [.. aberto.MeusPares.Where(p => p.AvaliadoId is not null).Select(p => p.AvaliadoId!.Value)], [.. aberto.Feitos.Where(p => p.AvaliadoId is not null).Select(p => p.AvaliadoId!.Value)]);
    }

    /// <summary>O que vem depois de "ciclo " no título ("Avaliação da equipe, ciclo 2026.2" vira "2026.2"); sem a palavra, o título inteiro.</summary>
    internal static string Ciclo(string titulo)
    {
        var posicao = titulo.LastIndexOf("ciclo ", StringComparison.OrdinalIgnoreCase);
        var depois = posicao < 0 ? string.Empty : titulo[(posicao + "ciclo ".Length)..].Trim();
        return depois.Length == 0 ? titulo : depois;
    }

    private async Task<T> IsolarAsync<T>(string parte, Func<Task<T>> consulta, T vazio)
    {
        try
        {
            return await consulta();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            log.LogWarning("A parte {Parte} do painel da equipe falhou ({Erro}); ela sai vazia.", parte, erro.GetType().Name);
            return vazio;
        }
    }

    /// <summary>O formulário de avaliação aberto: o botão e quem o gestor deve avaliar (e já avaliou) nele.</summary>
    private sealed record AvaliacaoDoCiclo(PainelAvaliacaoDoCicloDto Botao, IReadOnlyList<int> Avaliar, IReadOnlyList<int> Avaliados)
    {
        /// <summary>Quem o gestor já avaliou é <c>RESPONDIDA</c>; quem está de férias hoje (como o exemplo do contrato) e quem não tem par com o gestor é <c>NAO_SE_APLICA</c>; o resto é <c>PENDENTE</c>.</summary>
        public SituacaoDaAvaliacaoNoPainel SituacaoDe(int pessoaId, bool emFerias) =>
            Avaliados.Contains(pessoaId) ? SituacaoDaAvaliacaoNoPainel.Respondida
            : !emFerias && Avaliar.Contains(pessoaId) ? SituacaoDaAvaliacaoNoPainel.Pendente
            : SituacaoDaAvaliacaoNoPainel.NaoSeAplica;
    }
}
