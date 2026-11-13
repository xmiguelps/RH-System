using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Emails;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Ferias;

/// <summary>As decisões que mandam e-mail (uma por decisão, na chave de idempotência) e o que cada uma diz no texto.</summary>
public static class DecisaoDeFeriasPorEmail
{
    public const string Tipo = "FERIAS_DECIDIDAS";

    public const string GestorAprovou = "GESTOR_APROVOU";

    public const string GestorRecusou = "GESTOR_RECUSOU";

    public const string GestorSugeriu = "GESTOR_SUGERIU";

    public const string RhConfirmou = "RH_CONFIRMOU";

    public const string RhRecusou = "RH_RECUSOU";

    /// <summary>
    /// O aviso de uma decisão ao e-mail corporativo da pessoa do pedido; quem não tem e-mail é pulado (nada entra na fila e a decisão vale). A chave (<c>pedido</c> e <c>decisao</c>) impede o aviso de sair duas vezes. A carga leva o nome, as datas, quem decidiu e o motivo ou as datas
    /// sugeridas; o renderizador conta o resto. A falha do envio (feito depois, pela fila) nunca desfaz a decisão.
    /// </summary>
    public static async Task EnfileirarAsync(IFilaDeEmails fila, IConsultaDeFerias consulta, FeriasLida pedido, string decisao, string decididoPor, string? motivo, DateOnly? sugestaoInicio, DateOnly? sugestaoFim, CancellationToken ct)
    {
        var destinatario = await consulta.EmailCorporativoAsync(pedido.Funcionario.Id, ct);
        if (string.IsNullOrWhiteSpace(destinatario))
        {
            return;
        }

        await fila.EnfileirarAsync(
            new EmailParaEnfileirar(
                Tipo,
                ChaveDeIdempotencia.Montar(Tipo, ("pedido", pedido.Id), ("decisao", decisao)),
                destinatario,
                new
                {
                    nome = pedido.Funcionario.Nome,
                    dataInicio = pedido.DataInicio.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    dataFim = pedido.DataFim.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    decisao,
                    decididoPor,
                    motivo,
                    sugestaoInicio = sugestaoInicio?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                    sugestaoFim = sugestaoFim?.ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture),
                },
                pedido.Funcionario.Id,
                pedido.Id),
            ct);
    }
}

/// <summary>O motivo da recusa (gestor e RH): de 5 a 500 caracteres, aparado; é o que a pessoa vê.</summary>
public static class MotivoDaRecusaDeFerias
{
    public static string Ler(FeriasRecusarComando? corpo)
    {
        var motivo = (corpo?.Motivo ?? string.Empty).Trim();
        if (motivo.Length is < 5 or > 500)
        {
            throw new ErroDeValidacaoException("motivo", "O motivo da recusa tem de 5 a 500 caracteres.");
        }

        return motivo;
    }
}

/// <summary>O que as rotas do gestor têm em comum: a equipe de hoje (gestor sem setor = 404), quem consulta e os dois 409 de quem decide fora da vez.</summary>
internal static class GestorDasFerias
{
    public static async Task<EquipeDoGestor> EquipeAsync(IEscopoDoUsuario escopo, CancellationToken ct) => await escopo.ObterEquipeAsync(ct) ?? throw new NaoEncontradoException();

    public static QuemConsultaOPedido Quem(IEscopoDoUsuario escopo, EquipeDoGestor equipe) => new(PapelNaConsulta.Gestor, escopo.FuncionarioId, equipe.GestorId);

    /// <summary>Só o pedido <c>COM_O_GESTOR</c> se decide aqui: terminado = <c>PEDIDO_JA_DECIDIDO</c>; com o RH ou esperando a resposta da pessoa = <c>PEDIDO_ETAPA_INCORRETA</c>.</summary>
    public static void ConferirNaEtapaDoGestor(FeriasLida pedido)
    {
        switch (SituacaoDoPedidoDeFerias.Situacao(pedido.ComoLinha()))
        {
            case SituacaoDasFerias.ComOGestor:
                return;
            case SituacaoDasFerias.ComORh or SituacaoDasFerias.SugestaoDeData:
                throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
            default:
                throw MensagensDaGestao.PedidoJaDecidido.ComoProblema();
        }
    }

    public static async Task<FeriasPedidoAnalise> AnaliseAsync(IConsultaDeFerias consulta, FeriasLida pedido, QuemConsultaOPedido quem, CancellationToken ct)
    {
        var contexto = (await consulta.ContextoDaEquipeAsync([pedido], ct))[pedido.Id];
        return FeriasPedidoAnalise.De(MontadorDoPedidoDeFerias.Montar(pedido, quem), contexto.Conflitos, contexto.EquipeDisponivel);
    }
}

/// <summary>Os valores de <c>situacao</c> da lista (<c>FeriasSituacao</c>); só para o documento da API descrevê-los.</summary>
public static class LeitorDaSituacaoDasFerias
{
    public static (ParametrosDeLista Parametros, SituacaoDasFerias Situacao) Ler(int pagina, int tamanhoPagina, string? situacao, SituacaoDasFerias padrao)
    {
        var lida = EnumsDeConsulta.Ler<SituacaoDasFerias>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, null, null, null).Validado();
        return (parametros, lida ?? padrao);
    }
}

/// <summary>
/// <c>GET /api/gestor/ferias</c>: os pedidos da EQUIPE do gestor (os que têm o <c>ID_GESTOR</c> dele; o do próprio gestor, o de outro gestor e o de outro setor não aparecem), por padrão os que esperam a análise dele, do mais antigo ao mais novo, cada um com os conflitos e a equipe
/// disponível. Gestor sem setor: lista vazia.
/// </summary>
public sealed class ListarFeriasDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeFerias consulta)
{
    public async Task<RespostaPaginada<FeriasPedidoAnalise>> ExecutarAsync(int pagina, int tamanhoPagina, string? situacao, CancellationToken ct)
    {
        var (parametros, filtro) = LeitorDaSituacaoDasFerias.Ler(pagina, tamanhoPagina, situacao, SituacaoDasFerias.ComOGestor);
        var equipe = await escopo.ObterEquipeAsync(ct);
        if (equipe is null)
        {
            return RespostaPaginada<FeriasPedidoAnalise>.De([], 0, parametros);
        }

        var (itens, total) = await consulta.ListarDaEquipeAsync(equipe, filtro, parametros, ct);
        var contexto = await consulta.ContextoDaEquipeAsync(itens, ct);
        var quem = GestorDasFerias.Quem(escopo, equipe);
        return RespostaPaginada<FeriasPedidoAnalise>.De([.. itens.Select(i => FeriasPedidoAnalise.De(MontadorDoPedidoDeFerias.Montar(i, quem), contexto[i.Id].Conflitos, contexto[i.Id].EquipeDisponivel))], total, parametros);
    }
}

/// <summary>
/// <c>POST /api/gestor/ferias/{feriasId}/aprovar</c> e <c>.../recusar</c>. Pedido fora da equipe, do próprio gestor, de outro gestor ou que não existe = o mesmo 404; pelo estado lido: já decidido = 409 <c>PEDIDO_JA_DECIDIDO</c>; com o RH ou esperando a resposta da pessoa = 409
/// <c>PEDIDO_ETAPA_INCORRETA</c>. A decisão é UM <c>UPDATE</c> condicional ao estado (o segundo clique, o gestor ausente e o RH devolvem o 409 que a leitura teria dado); aprovar passa o pedido ao RH (o status continua <c>SOLICITADA</c>) e recusar o encerra (<c>REJEITADA</c>, com o motivo).
/// O evento e o e-mail vão na mesma transação; a mensagem da pessoa nunca é tocada.
/// </summary>
public sealed class DecidirFeriasComoGestorCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeFerias consulta,
    IFeriasRepositorio repositorio,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IFilaDeEmails fila,
    IRelogio relogio)
{
    public Task<FeriasPedidoAnalise> AprovarAsync(int feriasId, CancellationToken ct) => DecidirAsync(feriasId, aprovar: true, motivo: null, ct);

    public Task<FeriasPedidoAnalise> RecusarAsync(int feriasId, FeriasRecusarComando? corpo, CancellationToken ct) => DecidirAsync(feriasId, aprovar: false, MotivoDaRecusaDeFerias.Ler(corpo), ct);

    private async Task<FeriasPedidoAnalise> DecidirAsync(int feriasId, bool aprovar, string? motivo, CancellationToken ct)
    {
        var equipe = await GestorDasFerias.EquipeAsync(escopo, ct);
        var pedido = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
        GestorDasFerias.ConferirNaEtapaDoGestor(pedido);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                // A pessoa do pedido é travada ANTES do UPDATE, como o sugerir, o pedir e o aceite: a mesma ordem de travas em toda escrita de férias (a linha da pessoa, depois a do pedido) evita o deadlock entre duas decisões que se cruzam
                // (o e-mail da fila referencia a pessoa e pede a trava dela de leitura).
                _ = await repositorio.TravarPessoaAsync(pedido.Funcionario.Id, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;
                var decidiu = aprovar
                    ? await repositorio.AprovarComoGestorAsync(feriasId, equipe.GestorId, agora, ct)
                    : await repositorio.RecusarComoGestorAsync(feriasId, equipe.GestorId, agora, motivo!, ct);
                if (!decidiu)
                {
                    // Outra decisão chegou antes (o mesmo clique, o gestor ausente, o RH): o estado de agora diz qual dos dois 409.
                    var atual = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
                    GestorDasFerias.ConferirNaEtapaDoGestor(atual);
                    throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
                }

                var descricao = $"{(aprovar ? "Aprovou" : "Recusou")} o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy} (etapa do gestor).";
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Ferias, aprovar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou, descricao, $"Férias de {pedido.Funcionario.Nome}", "FERIAS", feriasId));
                await unidade.SalvarAsync(ct);
                await DecisaoDeFeriasPorEmail.EnfileirarAsync(fila, consulta, pedido, aprovar ? DecisaoDeFeriasPorEmail.GestorAprovou : DecisaoDeFeriasPorEmail.GestorRecusou, escopo.Nome, motivo, null, null, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var decidido = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
        return await GestorDasFerias.AnaliseAsync(consulta, decidido, GestorDasFerias.Quem(escopo, equipe), ct);
    }
}

/// <summary>
/// <c>POST /api/gestor/ferias/{feriasId}/sugerir-outra-data</c>: o pedido fica esperando a resposta da pessoa (<c>SUGESTAO_DE_DATA</c>: o gestor continua com a etapa, ninguém decide). As datas têm de ser depois do fim do período aquisitivo do pedido e não passadas (400) e cumprir as
/// regras de férias, TODAS menos a antecedência (422 com a lista): senão a pessoa nunca conseguiria aceitar. A pessoa é travada antes de conferir (o pedido dela, o aceite e o registro do RH contam no saldo). O abono é o do pedido.
/// </summary>
public sealed class SugerirOutraDataDasFeriasCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeFerias consulta,
    IFeriasRepositorio repositorio,
    ConferidorDeFerias conferidor,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IFilaDeEmails fila,
    IRelogio relogio)
{
    public async Task<FeriasPedidoAnalise> ExecutarAsync(int feriasId, FeriasSugerirComando corpo, CancellationToken ct)
    {
        ValidacaoDeFerias.Validar(corpo.DataInicio, corpo.DataFim, 0, null, "mensagem");
        var equipe = await GestorDasFerias.EquipeAsync(escopo, ct);
        var pedido = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
        GestorDasFerias.ConferirNaEtapaDoGestor(pedido);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(pedido.Funcionario.Id, ct) ?? throw new NaoEncontradoException();
                var agora = relogio.AgoraUtc;
                var hoje = FusoBrasil.Hoje(agora);
                if (corpo.DataInicio <= pedido.PeriodoFim)
                {
                    throw new ErroDeValidacaoException("dataInicio", "A sugestão precisa começar depois do fim do período aquisitivo do pedido.");
                }

                if (corpo.DataInicio < hoje)
                {
                    throw new ErroDeValidacaoException("dataInicio", "A sugestão não pode começar antes de hoje.");
                }

                // As outras férias da pessoa (SEM este pedido: ele é o que está sendo trocado) e o período fixo dele.
                var outras = (await consulta.DaPessoaAsync(pedido.Funcionario.Id, ct)).Where(f => f.Id != feriasId).ToList();
                var conferencia = await conferidor.ConferirAsync(new ComandoDeFerias(corpo.DataInicio, corpo.DataFim, pedido.AbonoPecuniario, pedido.DiasAbono), ModoDaConferencia.SugestaoDoGestor, pessoa, outras, hoje, pedido.PeriodoInicio, ct);
                if (!conferencia.PodeEnviar)
                {
                    throw MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.Sugerir, conferencia.Regras);
                }

                if (!await repositorio.SugerirComoGestorAsync(feriasId, equipe.GestorId, agora, corpo.DataInicio, corpo.DataFim, ct))
                {
                    var atual = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
                    GestorDasFerias.ConferirNaEtapaDoGestor(atual);
                    throw MensagensDaGestao.PedidoNaEtapaErrada.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Ferias,
                    AuditoriaAcao.Alterou,
                    $"Sugeriu outras datas ({corpo.DataInicio:dd/MM/yyyy} a {corpo.DataFim:dd/MM/yyyy}) para o pedido de férias de {pedido.DataInicio:dd/MM/yyyy} a {pedido.DataFim:dd/MM/yyyy}.",
                    $"Férias de {pedido.Funcionario.Nome}",
                    "FERIAS",
                    feriasId));
                await unidade.SalvarAsync(ct);
                await DecisaoDeFeriasPorEmail.EnfileirarAsync(fila, consulta, pedido, DecisaoDeFeriasPorEmail.GestorSugeriu, escopo.Nome, null, corpo.DataInicio, corpo.DataFim, ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        var sugerido = await consulta.ObterDaEquipeAsync(feriasId, equipe, ct) ?? throw new NaoEncontradoException();
        return await GestorDasFerias.AnaliseAsync(consulta, sugerido, GestorDasFerias.Quem(escopo, equipe), ct);
    }
}
