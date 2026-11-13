using System.Data;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes;

/// <summary>O prazo de resposta do RH precisa dos feriados da empresa entre o primeiro pedido e o prazo do último.</summary>
internal static class FeriadosDosPedidos
{
    public static async Task<IReadOnlySet<DateOnly>> CarregarAsync(ICalendarioDoPonto calendario, IReadOnlyCollection<DateTime> pedidosEmUtc, CancellationToken ct)
    {
        if (pedidosEmUtc.Count == 0)
        {
            return new HashSet<DateOnly>();
        }

        var de = RespostaPrevista.JanelaDeFeriados(pedidosEmUtc.Min()).De;
        var ate = RespostaPrevista.JanelaDeFeriados(pedidosEmUtc.Max()).Ate;
        var feriados = await calendario.FeriadosQueValemAsync(de, ate, ct);
        return feriados.Select(f => f.Data).ToHashSet();
    }
}

/// <summary>Os textos que o log de auditoria usa para a situação de um pedido de benefício.</summary>
internal static class RotulosDosBeneficios
{
    public static string Situacao(StatusDoPedidoDeBeneficio situacao) => situacao switch
    {
        StatusDoPedidoDeBeneficio.Pendente => "Pendente",
        StatusDoPedidoDeBeneficio.Aprovado => "Aprovado",
        StatusDoPedidoDeBeneficio.Recusado => "Recusado",
        StatusDoPedidoDeBeneficio.Cancelado => "Cancelado",
        _ => throw new ArgumentOutOfRangeException(nameof(situacao), situacao, "Situação de benefício desconhecida."),
    };

    public static string SimOuNao(bool valor) => valor ? "Sim" : "Não";

    public static string Dia(DateOnly dia) => dia.ToString("dd/MM/yyyy", System.Globalization.CultureInfo.InvariantCulture);
}

/// <summary>
/// <c>GET /api/eu/beneficios</c>: os benefícios da PRÓPRIA pessoa (qualquer perfil; o gestor usa a mesma rota para os dele e nunca vê dependente nem trajeto da equipe). Cartões (adesão ativa e aprovada, com os dependentes aprovados e o trajeto em vigor), os opcionais disponíveis, "Seus pedidos" do mais
/// novo para o mais antigo (só o que a PESSOA pediu: a adesão que o RH deu direto não é pedido) e os dependentes já aprovados, sem repetir.
/// </summary>
public sealed class ObterMeusBeneficiosCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdesoes consulta, ICalendarioDoPonto calendario)
{
    public async Task<BeneficiosEuResposta> ExecutarAsync(CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        var catalogo = await consulta.CatalogoAsync(ct);
        var adesoes = await consulta.AdesoesDaPessoaAsync(eu, ct);
        var dependentes = await consulta.DependentesDaPessoaAsync(eu, ct);
        var trajetos = await consulta.TrajetosDaPessoaAsync(eu, ct);

        var listas = ListasDoBeneficio.Montar(catalogo, adesoes);
        var cartoes = listas.Cartoes.Select(c => Cartao(c, dependentes, trajetos)).ToList();
        var disponiveis = listas.Disponiveis.Select(d => new BeneficioDisponivelDto(Catalogo(d.Item), d.Item.AceitaDependentes, d.Item.AceitaTrajeto, d.UltimoPedido)).ToList();

        var nomes = catalogo.ToDictionary(i => i.Id, i => i.Nome);
        var beneficioDaAdesao = adesoes.ToDictionary(a => a.AdesaoId, a => a.BeneficioId);
        var pedidos = new List<PedidoParaResumir>();
        pedidos.AddRange(adesoes
            .Where(a => a.PedidoEmUtc is not null)
            .Select(a => new PedidoParaResumir(TipoDoPedidoDeBeneficio.Adesao, a.AdesaoId, a.AdesaoId, nomes[a.BeneficioId], a.PedidoEmUtc!.Value, a.Status, a.MotivoDaRecusa)));
        pedidos.AddRange(dependentes.Select(d => new PedidoParaResumir(TipoDoPedidoDeBeneficio.Dependente, d.Id, d.AdesaoId, nomes[beneficioDaAdesao[d.AdesaoId]], d.PedidoEmUtc, d.Status, d.MotivoRecusa)));
        pedidos.AddRange(trajetos.Select(t => new PedidoParaResumir(TipoDoPedidoDeBeneficio.Trajeto, t.Id, t.AdesaoId, nomes[beneficioDaAdesao[t.AdesaoId]], t.PedidoEmUtc, t.Status, t.MotivoRecusa)));
        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [.. pedidos.Select(p => p.PedidoEmUtc)], ct);

        var cadastrados = DependentesCadastrados.SemRepetir(
            dependentes.Where(d => d.Status == StatusDoPedidoDeBeneficio.Aprovado).Select(d => new DadosDoDependente(d.Nome, d.Parentesco, d.DataNascimento)));
        return new BeneficiosEuResposta(cartoes, disponiveis, PedidosDaPessoa.Montar(pedidos, feriados), cadastrados);
    }

    private static BeneficioDoCatalogoDto Catalogo(ItemDoCatalogo item) => new(item.Id, item.Nome, item.Descricao);

    private static CartaoDeBeneficioDto Cartao(CartaoDeBeneficio cartao, IReadOnlyList<DependenteDaPessoa> dependentes, IReadOnlyList<TrajetoDaPessoa> trajetos)
    {
        var adesaoId = cartao.Adesao.AdesaoId;
        var aprovados = dependentes
            .Where(d => d.AdesaoId == adesaoId && d.Status == StatusDoPedidoDeBeneficio.Aprovado)
            .Select(d => new DependenteDto(d.Id, d.Nome, d.Parentesco, d.DataNascimento, d.Status, d.PedidoEmUtc, d.RespondidoEmUtc, d.MotivoRecusa))
            .ToList();
        var doCartao = trajetos.Where(t => t.AdesaoId == adesaoId).ToList();
        var vigente = TrajetoEmVigor.IdDe(doCartao.Select(t => new TrajetoParaEscolher(t.Id, t.Status, t.RespondidoEmUtc)));
        var trajeto = doCartao
            .Where(t => t.Id == vigente)
            .Select(t => new TrajetoDto(t.Id, t.Origem, t.Destino, t.LinhasMeios, t.Status, t.PedidoEmUtc, t.RespondidoEmUtc, t.MotivoRecusa))
            .FirstOrDefault();
        return new CartaoDeBeneficioDto(adesaoId, Catalogo(cartao.Item), cartao.Item.AceitaDependentes, cartao.Item.AceitaTrajeto, cartao.Adesao.InicioEm, cartao.Adesao.AutorizaDescontoEmFolha, aprovados, trajeto);
    }
}

/// <summary>
/// <c>POST /api/eu/beneficios-adesoes</c>: a pessoa pede um benefício OPCIONAL para si, com quem entra, a data de início e a autorização de desconto. Tudo se confere numa transação com a pessoa TRAVADA (dois pedidos iguais ao mesmo tempo: um 201 e um 409). A pessoa que já teve pedido recusado ou cancelado
/// (ou o benefício aprovado e desativado pelo RH) REABRE a mesma linha: volta a <c>PENDENTE</c>, o que estava pendente nos filhos é cancelado e a autorização anterior fica só no log. Ordem dos erros: 400, 404 (benefício), 409, 422 (pessoa inativa, benefício não opcional, não aceita dependentes, não
/// aceita trajeto). <b>Nenhum e-mail sai</b>; o evento de auditoria nunca leva dependente nem trajeto (só "mudou", sem valor).
/// </summary>
public sealed class PedirAdesaoABeneficioCasoDeUso(
    IEscopoDoUsuario escopo,
    IAdesoesRepositorio repositorio,
    ICalendarioDoPonto calendario,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task<ResumoDoPedido> ExecutarAsync(AdesaoPedirComando corpo, CancellationToken ct)
    {
        var agora = InstanteAoSegundo.De(relogio.AgoraUtc);
        var dependentes = (corpo.Dependentes ?? []).Select(d => d.Aparado()).ToList(); // o corpo pode trazer "dependentes": null
        var trajeto = corpo.Trajeto?.Aparado();
        ValidacaoDoPedidoDeBeneficio.Adesao(corpo.DataInicioDesejada, dependentes, trajeto, FusoBrasil.Hoje(agora));

        var funcionarioId = escopo.FuncionarioId;
        var (adesaoId, nomeDoBeneficio) = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var item = await repositorio.ItemDoCatalogoAsync(corpo.BeneficioId, ct) ?? throw new NaoEncontradoException();
                var existente = await repositorio.AdesaoDaPessoaAsync(funcionarioId, item.Id, ct);
                if (existente is { Status: StatusDoPedidoDeBeneficio.Pendente } or { Status: StatusDoPedidoDeBeneficio.Aprovado, Ativo: true })
                {
                    throw MensagensDaGestao.BeneficioJaAtivoOuPendente.ComoProblema();
                }

                if (!pessoa.Ativo)
                {
                    throw MensagensDaGestao.FuncionarioInativoParaPedirBeneficio.ComoProblema();
                }

                if (!item.Opcional)
                {
                    throw MensagensDaGestao.BeneficioNaoOpcional.ComoProblema();
                }

                if (dependentes.Count > 0 && !item.AceitaDependentes)
                {
                    throw MensagensDaGestao.BeneficioNaoAceitaDependentes.ComoProblema();
                }

                if (trajeto is not null && !item.AceitaTrajeto)
                {
                    throw MensagensDaGestao.BeneficioNaoAceitaTrajeto.ComoProblema();
                }

                var dados = new NovaAdesao(funcionarioId, item.Id, corpo.DataInicioDesejada, corpo.AutorizaDescontoEmFolha, dependentes, trajeto, agora);
                int id;
                if (existente is null)
                {
                    id = await repositorio.CriarAdesaoAsync(dados, ct);
                }
                else
                {
                    id = existente.AdesaoId;
                    await repositorio.ReabrirAdesaoAsync(id, dados, ct);
                }

                auditor.Registrar(Evento(existente, id, item.Nome, corpo, dependentes.Count > 0, trajeto is not null));
                await unidade.SalvarAsync(ct);
                return (id, item.Nome);
            },
            IsolationLevel.ReadCommitted,
            ct);

        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [agora], ct);
        return PedidosDaPessoa.Resumir(new PedidoParaResumir(TipoDoPedidoDeBeneficio.Adesao, adesaoId, adesaoId, nomeDoBeneficio, agora, StatusDoPedidoDeBeneficio.Pendente, null), feriados);
    }

    /// <summary>
    /// A linha nova só tem o "depois"; a reaberta mostra a situação, a autorização e o início de ANTES (a autorização anterior fica só aqui). Dependentes e trajeto entram como "mudou", sem valor.
    /// </summary>
    private static EventoDeAuditoriaNovo Evento(AdesaoDaPessoa? antes, int adesaoId, string nomeDoBeneficio, AdesaoPedirComando corpo, bool comDependentes, bool comTrajeto)
    {
        var mudancas = new List<MudancaDeAuditoriaNova>
        {
            new("Situação", antes is null ? null : RotulosDosBeneficios.Situacao(antes.Status), RotulosDosBeneficios.Situacao(StatusDoPedidoDeBeneficio.Pendente)),
            new("Autorização de desconto em folha", antes is null ? null : RotulosDosBeneficios.SimOuNao(antes.AutorizaDescontoEmFolha), RotulosDosBeneficios.SimOuNao(corpo.AutorizaDescontoEmFolha)),
            new("Início desejado", antes?.InicioEm is { } inicio ? RotulosDosBeneficios.Dia(inicio) : null, RotulosDosBeneficios.Dia(corpo.DataInicioDesejada)),
        };
        if (comDependentes)
        {
            mudancas.Add(new MudancaDeAuditoriaNova("Dependentes", null, null, ValorOculto: true));
        }

        if (comTrajeto)
        {
            mudancas.Add(new MudancaDeAuditoriaNova("Trajeto", null, null, ValorOculto: true));
        }

        return new EventoDeAuditoriaNovo(
            AuditoriaArea.Beneficios,
            antes is null ? AuditoriaAcao.Criou : AuditoriaAcao.Alterou,
            $"Pediu adesão ao benefício {nomeDoBeneficio}.",
            $"Benefício {nomeDoBeneficio}",
            "BENEFICIOS_FUNCIONARIOS",
            adesaoId,
            mudancas);
    }
}

/// <summary>
/// <c>POST /api/eu/beneficios-adesoes/{adesaoId}/dependentes</c>: a pessoa pede a inclusão de um dependente numa adesão ATIVA e aprovada DELA (de outra pessoa ou inexistente: 404). Ordem: 400, 404, 409 <c>BENEFICIO_ADESAO_NAO_ATIVA</c>, 422 (benefício não aceita dependentes, pessoa inativa). O evento de
/// auditoria diz que houve o pedido, sem nome, parentesco nem nascimento.
/// </summary>
public sealed class PedirInclusaoDeDependenteCasoDeUso(
    IEscopoDoUsuario escopo,
    IAdesoesRepositorio repositorio,
    ICalendarioDoPonto calendario,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task<ResumoDoPedido> ExecutarAsync(int adesaoId, DadosDoDependente corpo, CancellationToken ct)
    {
        var agora = InstanteAoSegundo.De(relogio.AgoraUtc);
        var dependente = corpo.Aparado();
        ValidacaoDoPedidoDeBeneficio.Dependente(dependente, FusoBrasil.Hoje(agora));

        var funcionarioId = escopo.FuncionarioId;
        var (id, nomeDoBeneficio) = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var adesao = await repositorio.AdesaoDaPessoaPorIdAsync(adesaoId, funcionarioId, ct) ?? throw new NaoEncontradoException();
                if (adesao is not { Status: StatusDoPedidoDeBeneficio.Aprovado, Ativo: true })
                {
                    throw MensagensDaGestao.BeneficioAdesaoNaoAtiva.ComoProblema();
                }

                if (!adesao.AceitaDependentes)
                {
                    throw MensagensDaGestao.BeneficioNaoAceitaDependentes.ComoProblema();
                }

                if (!pessoa.Ativo)
                {
                    throw MensagensDaGestao.FuncionarioInativoParaPedirBeneficio.ComoProblema();
                }

                var novo = await repositorio.CriarDependenteAsync(adesaoId, dependente, agora, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Beneficios,
                    AuditoriaAcao.Criou,
                    $"Pediu a inclusão de um dependente em {adesao.NomeDoBeneficio}.",
                    $"Benefício {adesao.NomeDoBeneficio}",
                    "DEPENDENTES",
                    novo,
                    [new MudancaDeAuditoriaNova("Dependente", null, null, ValorOculto: true)]));
                await unidade.SalvarAsync(ct);
                return (novo, adesao.NomeDoBeneficio);
            },
            IsolationLevel.ReadCommitted,
            ct);

        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [agora], ct);
        return PedidosDaPessoa.Resumir(new PedidoParaResumir(TipoDoPedidoDeBeneficio.Dependente, id, adesaoId, nomeDoBeneficio, agora, StatusDoPedidoDeBeneficio.Pendente, null), feriados);
    }
}

/// <summary>
/// <c>POST /api/eu/beneficios-adesoes/{adesaoId}/trajetos</c>: a pessoa pede um trajeto NOVO do vale-transporte; o em vigor continua valendo até o RH aprovar. Mesmas regras e mesma ordem de erros do dependente (o benefício precisa aceitar trajeto). O evento de auditoria diz que o trajeto mudou, SEM
/// origem, destino nem linhas.
/// </summary>
public sealed class PedirAtualizacaoDeTrajetoCasoDeUso(
    IEscopoDoUsuario escopo,
    IAdesoesRepositorio repositorio,
    ICalendarioDoPonto calendario,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task<ResumoDoPedido> ExecutarAsync(int adesaoId, DadosDoTrajeto corpo, CancellationToken ct)
    {
        var agora = InstanteAoSegundo.De(relogio.AgoraUtc);
        var trajeto = corpo.Aparado();
        ValidacaoDoPedidoDeBeneficio.Trajeto(trajeto);

        var funcionarioId = escopo.FuncionarioId;
        var (id, nomeDoBeneficio) = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var pessoa = await repositorio.TravarPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
                var adesao = await repositorio.AdesaoDaPessoaPorIdAsync(adesaoId, funcionarioId, ct) ?? throw new NaoEncontradoException();
                if (adesao is not { Status: StatusDoPedidoDeBeneficio.Aprovado, Ativo: true })
                {
                    throw MensagensDaGestao.BeneficioAdesaoNaoAtiva.ComoProblema();
                }

                if (!adesao.AceitaTrajeto)
                {
                    throw MensagensDaGestao.BeneficioNaoAceitaTrajeto.ComoProblema();
                }

                if (!pessoa.Ativo)
                {
                    throw MensagensDaGestao.FuncionarioInativoParaPedirBeneficio.ComoProblema();
                }

                var novo = await repositorio.CriarTrajetoAsync(adesaoId, trajeto, agora, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Beneficios,
                    AuditoriaAcao.Criou,
                    $"Pediu a atualização do trajeto de {adesao.NomeDoBeneficio}.",
                    $"Benefício {adesao.NomeDoBeneficio}",
                    "TRAJETOS_VALE_TRANSPORTE",
                    novo,
                    [new MudancaDeAuditoriaNova("Trajeto", null, null, ValorOculto: true)]));
                await unidade.SalvarAsync(ct);
                return (novo, adesao.NomeDoBeneficio);
            },
            IsolationLevel.ReadCommitted,
            ct);

        var feriados = await FeriadosDosPedidos.CarregarAsync(calendario, [agora], ct);
        return PedidosDaPessoa.Resumir(new PedidoParaResumir(TipoDoPedidoDeBeneficio.Trajeto, id, adesaoId, nomeDoBeneficio, agora, StatusDoPedidoDeBeneficio.Pendente, null), feriados);
    }
}
