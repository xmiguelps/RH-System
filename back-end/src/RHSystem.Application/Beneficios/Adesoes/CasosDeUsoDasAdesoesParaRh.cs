using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes;

/// <summary>Monta a adesão do RH no formato do contrato: o prazo de resposta só existe para quem pediu (a concedida direto na ficha não tem pedido).</summary>
internal static class MontadorDaAdesaoParaRh
{
    public static async Task<AdesaoParaRhDto> MontarAsync(AdesaoLidaPeloRh adesao, ICalendarioDoPonto calendario, CancellationToken ct)
    {
        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [.. DatasDoPedido([adesao])], ct);
        return Montar(adesao, feriados);
    }

    public static IEnumerable<DateTime> DatasDoPedido(IEnumerable<AdesaoLidaPeloRh> adesoes) => adesoes.Where(a => a.PedidoEmUtc is not null).Select(a => a.PedidoEmUtc!.Value);

    public static AdesaoParaRhDto Montar(AdesaoLidaPeloRh a, IReadOnlySet<DateOnly> feriados) =>
        new(
            a.AdesaoId,
            a.Funcionario,
            new BeneficioDoCatalogoDto(a.Beneficio.Id, a.Beneficio.Nome, a.Beneficio.Descricao),
            a.Status,
            a.PedidoEmUtc,
            a.PedidoEmUtc is { } pedido ? RespostaPrevista.De(pedido, feriados) : null,
            a.DataInicioDesejada,
            a.AutorizaDescontoEmFolha,
            a.RespondidoPor,
            a.RespondidoEmUtc,
            a.MotivoRecusa,
            [.. a.Dependentes.OrderBy(d => d.Id).Select(d => new DependenteDto(d.Id, d.Nome, d.Parentesco, d.DataNascimento, d.Status, d.PedidoEmUtc, d.RespondidoEmUtc, d.MotivoRecusa))],
            [.. a.Trajetos.OrderBy(t => t.Id).Select(t => new TrajetoDto(t.Id, t.Origem, t.Destino, t.LinhasMeios, t.Status, t.PedidoEmUtc, t.RespondidoEmUtc, t.MotivoRecusa))]);
}

/// <summary>
/// <c>GET /api/rh/beneficios-adesoes</c>: uma linha por ADESÃO, com os dependentes e os trajetos dela dentro. Por padrão (<c>PENDENTE</c>) as adesões pendentes e as já aprovadas que têm dependente ou trajeto pendente, a mais antiga esperando primeiro; <c>situacao</c> filtra pela adesão; <c>beneficioId</c> e a busca
/// (no nome da pessoa) também. Só o RH: tem dado pessoal (dependentes e endereço de casa).
/// </summary>
public sealed class ListarPedidosDeBeneficioCasoDeUso(IConsultaDeAdesoes consulta, ICalendarioDoPonto calendario)
{
    public async Task<RespostaPaginada<AdesaoParaRhDto>> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? situacao, int? beneficioId, CancellationToken ct)
    {
        var filtro = EnumsDeConsulta.Ler<StatusDoPedidoDeBeneficio>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca, null, null).Validado();
        var (itens, total) = await consulta.ListarParaORhAsync(filtro ?? StatusDoPedidoDeBeneficio.Pendente, beneficioId, parametros, ct);
        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [.. MontadorDaAdesaoParaRh.DatasDoPedido(itens)], ct);
        return RespostaPaginada<AdesaoParaRhDto>.De([.. itens.Select(i => MontadorDaAdesaoParaRh.Montar(i, feriados))], total, parametros);
    }
}

/// <summary>
/// As seis decisões do RH sobre os pedidos de benefício (adesão, dependente e trajeto, cada um aprovar e recusar). Cada uma numa transação com a pessoa da adesão TRAVADA antes (a mesma ordem de travas de toda escrita da gestão) e UM <c>UPDATE</c> condicional ao pedido continuar <c>PENDENTE</c>. O RH decide até o
/// próprio pedido. <b>Nenhum e-mail sai</b>: a pessoa vê a resposta em "Seus pedidos".
/// <para>
/// Adesão: 404, 409 <c>BENEFICIO_PEDIDO_JA_DECIDIDO</c> (já foi respondida), 422 <c>FUNCIONARIO_INATIVO</c> só ao aprovar (o banco não barra a aprovação). Dependente e trajeto: 404 (também o que é de outra adesão), 409 <c>BENEFICIO_ADESAO_NAO_ATIVA</c> (a adesão precisa estar aprovada E ativa; vence), depois 409
/// <c>BENEFICIO_PEDIDO_JA_DECIDIDO</c>. A recusa pede o motivo (1 a 500, aparado): 400 antes de tudo. O log não leva o motivo nem dado de dependente ou trajeto.
/// </para>
/// </summary>
public sealed class DecidirPedidoDeBeneficioCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeAdesoes consulta,
    IAdesoesRepositorio repositorio,
    ICalendarioDoPonto calendario,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public Task<AdesaoParaRhDto> AprovarAdesaoAsync(int adesaoId, CancellationToken ct) => DecidirAdesaoAsync(adesaoId, aprovar: true, motivo: null, ct);

    public Task<AdesaoParaRhDto> RecusarAdesaoAsync(int adesaoId, AdesaoRecusarComando corpo, CancellationToken ct) => DecidirAdesaoAsync(adesaoId, aprovar: false, ValidacaoDoPedidoDeBeneficio.Motivo(corpo.Motivo), ct);

    public Task<AdesaoParaRhDto> AprovarDependenteAsync(int adesaoId, int dependenteId, CancellationToken ct) =>
        DecidirFilhoAsync(adesaoId, dependenteId, aprovar: true, motivo: null, "a inclusão de um dependente", "DEPENDENTES", repositorio.DependenteParaDecidirAsync, (agora, motivo, c) => repositorio.AprovarDependenteAsync(adesaoId, dependenteId, escopo.FuncionarioId, agora, c), ct);

    public Task<AdesaoParaRhDto> RecusarDependenteAsync(int adesaoId, int dependenteId, AdesaoRecusarComando corpo, CancellationToken ct) =>
        DecidirFilhoAsync(adesaoId, dependenteId, aprovar: false, ValidacaoDoPedidoDeBeneficio.Motivo(corpo.Motivo), "a inclusão de um dependente", "DEPENDENTES", repositorio.DependenteParaDecidirAsync, (agora, motivo, c) => repositorio.RecusarDependenteAsync(adesaoId, dependenteId, escopo.FuncionarioId, agora, motivo!, c), ct);

    public Task<AdesaoParaRhDto> AprovarTrajetoAsync(int adesaoId, int trajetoId, CancellationToken ct) =>
        DecidirFilhoAsync(adesaoId, trajetoId, aprovar: true, motivo: null, "a atualização do trajeto", "TRAJETOS_VALE_TRANSPORTE", repositorio.TrajetoParaDecidirAsync, (agora, motivo, c) => repositorio.AprovarTrajetoAsync(adesaoId, trajetoId, escopo.FuncionarioId, agora, c), ct);

    public Task<AdesaoParaRhDto> RecusarTrajetoAsync(int adesaoId, int trajetoId, AdesaoRecusarComando corpo, CancellationToken ct) =>
        DecidirFilhoAsync(adesaoId, trajetoId, aprovar: false, ValidacaoDoPedidoDeBeneficio.Motivo(corpo.Motivo), "a atualização do trajeto", "TRAJETOS_VALE_TRANSPORTE", repositorio.TrajetoParaDecidirAsync, (agora, motivo, c) => repositorio.RecusarTrajetoAsync(adesaoId, trajetoId, escopo.FuncionarioId, agora, motivo!, c), ct);

    private async Task<AdesaoParaRhDto> DecidirAdesaoAsync(int adesaoId, bool aprovar, string? motivo, CancellationToken ct)
    {
        var previa = await repositorio.AdesaoParaDecidirAsync(adesaoId, ct) ?? throw new NaoEncontradoException();
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                // A pessoa é travada ANTES do UPDATE e a adesão é relida já com a trava: dois RHs ao mesmo tempo se serializam.
                var pessoa = await repositorio.TravarPessoaAsync(previa.FuncionarioId, ct) ?? throw new NaoEncontradoException();
                var adesao = await repositorio.AdesaoParaDecidirAsync(adesaoId, ct) ?? throw new NaoEncontradoException();
                if (adesao.Status != StatusDoPedidoDeBeneficio.Pendente)
                {
                    throw MensagensDaGestao.BeneficioPedidoJaDecidido.ComoProblema();
                }

                if (aprovar && !pessoa.Ativo)
                {
                    throw MensagensDaGestao.FuncionarioInativoParaAprovarBeneficio.ComoProblema();
                }

                var agora = relogio.AgoraUtc;
                var decidiu = aprovar
                    ? await repositorio.AprovarAdesaoAsync(adesaoId, escopo.FuncionarioId, agora, ct)
                    : await repositorio.RecusarAdesaoAsync(adesaoId, escopo.FuncionarioId, agora, motivo!, ct);
                if (!decidiu)
                {
                    throw MensagensDaGestao.BeneficioPedidoJaDecidido.ComoProblema();
                }

                var mudancas = new List<MudancaDeAuditoriaNova> { new("Situação", RotulosDosBeneficios.Situacao(StatusDoPedidoDeBeneficio.Pendente), RotulosDosBeneficios.Situacao(aprovar ? StatusDoPedidoDeBeneficio.Aprovado : StatusDoPedidoDeBeneficio.Recusado)) };
                if (aprovar)
                {
                    mudancas.Add(new MudancaDeAuditoriaNova("Benefício ativo", RotulosDosBeneficios.SimOuNao(false), RotulosDosBeneficios.SimOuNao(true)));
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Beneficios,
                    aprovar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou,
                    $"{(aprovar ? "Aprovou" : "Recusou")} a adesão de {pessoa.Nome} ao benefício {adesao.NomeDoBeneficio}.",
                    $"Benefício {adesao.NomeDoBeneficio} de {pessoa.Nome}",
                    "BENEFICIOS_FUNCIONARIOS",
                    adesaoId,
                    mudancas));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        return await RelerAsync(adesaoId, ct);
    }

    private async Task<AdesaoParaRhDto> DecidirFilhoAsync(
        int adesaoId,
        int filhoId,
        bool aprovar,
        string? motivo,
        string oQue,
        string entidade,
        Func<int, int, CancellationToken, Task<FilhoParaDecidir?>> ler,
        Func<DateTime, string?, CancellationToken, Task<bool>> decidir,
        CancellationToken ct)
    {
        var previa = await repositorio.AdesaoParaDecidirAsync(adesaoId, ct) ?? throw new NaoEncontradoException();
        _ = await ler(adesaoId, filhoId, ct) ?? throw new NaoEncontradoException();
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(previa.FuncionarioId, ct) ?? throw new NaoEncontradoException();
                var adesao = await repositorio.AdesaoParaDecidirAsync(adesaoId, ct) ?? throw new NaoEncontradoException();
                var filho = await ler(adesaoId, filhoId, ct) ?? throw new NaoEncontradoException();
                if (adesao is not { Status: StatusDoPedidoDeBeneficio.Aprovado, Ativo: true })
                {
                    throw MensagensDaGestao.BeneficioAdesaoNaoAtiva.ComoProblema();
                }

                if (filho.Status != StatusDoPedidoDeBeneficio.Pendente || !await decidir(relogio.AgoraUtc, motivo, ct))
                {
                    throw MensagensDaGestao.BeneficioPedidoJaDecidido.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Beneficios,
                    aprovar ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou,
                    $"{(aprovar ? "Aprovou" : "Recusou")} {oQue} em {adesao.NomeDoBeneficio} de {pessoa.Nome}.",
                    $"Benefício {adesao.NomeDoBeneficio} de {pessoa.Nome}",
                    entidade,
                    filhoId,
                    [new MudancaDeAuditoriaNova("Situação", RotulosDosBeneficios.Situacao(StatusDoPedidoDeBeneficio.Pendente), RotulosDosBeneficios.Situacao(aprovar ? StatusDoPedidoDeBeneficio.Aprovado : StatusDoPedidoDeBeneficio.Recusado))]));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        return await RelerAsync(adesaoId, ct);
    }

    private async Task<AdesaoParaRhDto> RelerAsync(int adesaoId, CancellationToken ct) =>
        await MontadorDaAdesaoParaRh.MontarAsync(await consulta.ObterParaORhAsync(adesaoId, ct) ?? throw new NaoEncontradoException(), calendario, ct);
}
