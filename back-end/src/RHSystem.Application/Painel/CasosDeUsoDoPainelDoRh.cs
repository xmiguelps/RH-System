using Microsoft.Extensions.Logging;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Aniversarios;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Ponto;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Painel;

/// <summary>A parte do painel que falha sai vazia e registra um <c>Warning</c> só com o nome da parte e o tipo da exceção (a mensagem pode trazer dado pessoal); o cancelamento do pedido sobe.</summary>
internal static class ParteIsoladaDoPainel
{
    public static async Task<T> ExecutarAsync<T>(ILogger log, string painel, string parte, Func<Task<T>> consulta, T vazio)
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
            log.LogWarning("A parte {Parte} do painel {Painel} falhou ({Erro}); ela sai vazia.", parte, painel, erro.GetType().Name);
            return vazio;
        }
    }
}

/// <summary>As cinco filas de pendências do RH juntas (a fonte única do painel, da lista e dos contadores), cada uma isolada: uma fila que falha vira lista vazia com <c>Warning</c>.</summary>
public sealed class ColetorDePendenciasDoRh(IConsultaDoPainelDoRh consulta, IRelogio relogio, ILogger<ColetorDePendenciasDoRh> log)
{
    public async Task<IReadOnlyList<PendenciaLida>> TodasAsync(string painel, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var todas = new List<PendenciaLida>();
        todas.AddRange(await ParteIsoladaDoPainel.ExecutarAsync(log, painel, "ajustes de ponto", () => consulta.AjustesDePontoNaEtapaDoRhAsync(ct), []));
        todas.AddRange(await ParteIsoladaDoPainel.ExecutarAsync(log, painel, "férias", () => consulta.FeriasNaEtapaDoRhAsync(ct), []));
        todas.AddRange(await ParteIsoladaDoPainel.ExecutarAsync(log, painel, "atestados", () => consulta.AtestadosEnviadosAsync(ct), []));
        todas.AddRange(await ParteIsoladaDoPainel.ExecutarAsync(log, painel, "admissões", () => consulta.AdmissoesComFormularioPendenteAsync(hoje, ct), []));
        todas.AddRange(await ParteIsoladaDoPainel.ExecutarAsync(log, painel, "benefícios", () => consulta.BeneficiosPendentesAsync(ct), []));
        return todas;
    }
}

/// <summary>
/// <c>GET /api/rh/painel</c>: a tela inicial do RH. <c>pendencias</c>: um cartão por tipo com quantidade maior que zero, só o que o RH JÁ pode decidir (ajuste e férias na etapa do RH, atestado enviado, admissão que começa em até 7 dias sem o formulário e pedido de benefício pendente); <c>totalPendencias</c> é a soma
/// das quantidades. <c>proximosDias</c>: feriados que valem para a empresa e aniversários dos próximos 21 dias, em ordem de data (no mesmo dia, o feriado antes), no máximo 8. <c>treinamentosEmAndamento</c>: os em andamento (publicados, liberados e não encerrados) do mais antigo ao mais novo, no máximo 5, com o
/// total da lista do RH. <c>presencaAgora</c>: a situação de hoje de todas as pessoas ativas e admitidas pela MESMA conta da equipe do gestor. <c>ultimasAlteracoes</c>: as 4 últimas linhas do log só com alterações. <b>Uma parte que falha sai vazia</b> e registra um <c>Warning</c>, nunca 500.
/// </summary>
public sealed class ObterPainelDoRhCasoDeUso(
    ColetorDePendenciasDoRh pendencias,
    IConsultaDoPainelDoRh consulta,
    ICalendarioDoPonto calendario,
    IConsultaDeAniversariantes aniversariantes,
    IConsultaDeTreinamentos treinamentos,
    ISituacaoDeHoje situacaoDeHoje,
    IConsultaDeAuditoria auditoria,
    IRelogio relogio,
    ILogger<ObterPainelDoRhCasoDeUso> log)
{
    private const string Painel = "do RH";
    private const int DiasAFrente = 21;
    private const int MaximoDeProximosDias = 8;
    private const int MaximoDeTreinamentos = 5;
    private const int UltimasAlteracoes = 4;

    public async Task<PainelRhDto> ExecutarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);

        var resumos = FilaDePendenciasDoRh.Resumos(await pendencias.TodasAsync(Painel, ct));
        var proximosDias = await ParteIsoladaDoPainel.ExecutarAsync(log, Painel, "próximos dias", () => ProximosDiasAsync(hoje, ct), []);
        var emAndamento = await ParteIsoladaDoPainel.ExecutarAsync(log, Painel, "treinamentos em andamento", () => TreinamentosAsync(hoje, ct), []);
        var presenca = await ParteIsoladaDoPainel.ExecutarAsync(log, Painel, "presença agora", () => PresencaAsync(hoje, agora, ct), new PainelPresencaAgoraDto(agora, true, 0, PresencaContagens.De([])));
        var alteracoes = await ParteIsoladaDoPainel.ExecutarAsync(log, Painel, "últimas alterações", () => AlteracoesAsync(agora, ct), []);
        return new PainelRhDto(resumos.Sum(r => r.Quantidade), resumos, proximosDias, emAndamento, presenca, alteracoes);
    }

    private async Task<IReadOnlyList<PainelProximoDiaDto>> ProximosDiasAsync(DateOnly hoje, CancellationToken ct)
    {
        var feriados = (await calendario.FeriadosQueValemAsync(hoje, hoje.AddDays(DiasAFrente), ct))
            .Select(f => new PainelProximoDiaDto(f.Data, PainelTipoDeProximoDia.Feriado, RotuloDoFeriado(f.Tipo), f.Nome));
        var comemoracoes = (await aniversariantes.CandidatosAsync(hoje, ct))
            .Where(c => JanelaDeAniversarios.DiasAte(c.Nascimento, hoje) <= DiasAFrente)
            .Select(c => new PainelProximoDiaDto(JanelaDeAniversarios.ProximoAniversario(c.Nascimento, hoje), PainelTipoDeProximoDia.Aniversario, "Aniversário", c.Funcionario.Setor is { } setor ? $"{c.Funcionario.Nome}, {setor}" : c.Funcionario.Nome));
        return [.. feriados.Concat(comemoracoes).OrderBy(d => d.Data).ThenBy(d => d.Tipo).ThenBy(d => d.Titulo, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true)).Take(MaximoDeProximosDias)];
    }

    private static string RotuloDoFeriado(TipoDeFeriado tipo) => tipo switch
    {
        TipoDeFeriado.Nacional => "Feriado nacional",
        TipoDeFeriado.Estadual => "Feriado estadual",
        TipoDeFeriado.Municipal => "Feriado municipal",
        TipoDeFeriado.Empresa => "Feriado da empresa",
        _ => throw new ArgumentOutOfRangeException(nameof(tipo), tipo, "Tipo de feriado sem rótulo."),
    };

    private async Task<IReadOnlyList<PainelTreinamentoAndamentoDto>> TreinamentosAsync(DateOnly hoje, CancellationToken ct)
    {
        var (itens, _, _) = await treinamentos.ListarAsync(SituacaoDoTreinamento.EmAndamento, new ParametrosDeLista(1, ParametrosDeLista.TamanhoMaximoDaPagina), hoje, ct);
        return [.. itens.OrderBy(t => t.ComecaEm ?? DateOnly.MinValue).ThenBy(t => t.Id).Take(MaximoDeTreinamentos).Select(t => new PainelTreinamentoAndamentoDto(t.Id, t.Titulo, t.Concluidos, t.TotalParticipantes))];
    }

    private async Task<PainelPresencaAgoraDto> PresencaAsync(DateOnly hoje, DateTime agora, CancellationToken ct)
    {
        var ids = await consulta.IdsDasPessoasAtivasEAdmitidasAsync(hoje, ct);
        var dia = await situacaoDeHoje.ObterAsync(ids, ct);
        var contagens = PresencaContagens.De(dia.Situacoes.Values);
        return new PainelPresencaAgoraDto(agora, dia.DiaUtil, contagens.Trabalhando + contagens.Ferias + contagens.Atestado + contagens.SemRegistro + contagens.AindaNaoComecou, contagens);
    }

    private async Task<IReadOnlyList<PainelAlteracaoDto>> AlteracoesAsync(DateTime agora, CancellationToken ct)
    {
        var filtro = new FiltroDeAuditoria(DateTime.SpecifyKind(DateTime.MinValue, DateTimeKind.Utc), agora.AddDays(1), null, null, null, ApenasAlteracoes: true);
        var pagina = await auditoria.ListarAsync(filtro, new ParametrosDeLista(1, UltimasAlteracoes), ct);
        return [.. pagina.Itens.Select(e => new PainelAlteracaoDto(e.QuandoUtc, Autor(e.Ator), e.Descricao))];
    }

    /// <summary>"Ana Souza (RH)", "Carlos Ribeiro (gestor)", "Bruno Lima (funcionário)" ou "Sistema".</summary>
    private static string Autor(AuditoriaAtor ator) => ator.Perfil switch
    {
        Perfil.RH => $"{ator.Nome} (RH)",
        Perfil.Gestor => $"{ator.Nome} (gestor)",
        Perfil.Funcionario => $"{ator.Nome} (funcionário)",
        _ => ator.Nome,
    };
}

/// <summary>
/// <c>GET /api/rh/painel/pendencias</c>: as pendências do RH uma a uma, do mais antigo ao mais novo (<c>desdeEmUtc</c>), das cinco origens, montadas em memória e paginadas (a fila de um RH tem dezenas de itens), com o filtro <c>tipo</c> (o nome do contrato; outro valor: 400). A MESMA conta do painel. Uma origem que falha
/// sai vazia, com <c>Warning</c>.
/// </summary>
public sealed class ListarPendenciasDoRhCasoDeUso(ColetorDePendenciasDoRh pendencias)
{
    public async Task<RespostaPaginada<PainelPendenciaItemDto>> ExecutarAsync(int pagina, int tamanhoPagina, string? tipo, CancellationToken ct)
    {
        var filtro = EnumsDeConsulta.Ler<PainelPendenciaTipo>(tipo, "tipo");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        return FilaDePendenciasDoRh.Pagina(await pendencias.TodasAsync("de pendências do RH", ct), filtro, parametros);
    }
}

/// <summary>
/// <c>GET /api/rh/painel/presenca/sem-registro</c>: as pessoas contadas em <c>semRegistro</c> de "Presença agora" (dia com expediente, sem entrada, depois do horário de entrada mais a tolerância), em ordem alfabética do nome (empate: o id) e paginadas. Quem ainda não começou não entra; em dia sem expediente a lista vem
/// vazia.
/// </summary>
public sealed class ListarPessoasSemRegistroHojeCasoDeUso(IConsultaDoPainelDoRh consulta, ISituacaoDeHoje situacaoDeHoje, IRelogio relogio)
{
    public async Task<RespostaPaginada<PainelSemRegistroItemDto>> ExecutarAsync(int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var ids = await consulta.IdsDasPessoasAtivasEAdmitidasAsync(FusoBrasil.Hoje(relogio.AgoraUtc), ct);
        var semRegistro = (await situacaoDeHoje.ObterAsync(ids, ct)).Situacoes.Where(s => s.Value == SituacaoHoje.SemRegistro).Select(s => s.Key).ToList();
        var pessoas = (await consulta.ResumosAsync(semRegistro, ct))
            .OrderBy(p => p.Nome, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true))
            .ThenBy(p => p.Id)
            .ToList();
        var itens = pessoas.Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(p => new PainelSemRegistroItemDto(p.Id, p.Nome, p.Ativo, SituacaoHoje.SemRegistro, p.Cargo, p.Setor)).ToList();
        return RespostaPaginada<PainelSemRegistroItemDto>.De(itens, pessoas.Count, parametros);
    }
}
