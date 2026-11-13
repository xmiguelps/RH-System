using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Beneficios.Adesoes;

/// <summary>A pessoa que pede (travada na transação): só o que a regra precisa.</summary>
public sealed record PessoaParaBeneficio(int Id, string Nome, bool Ativo);

/// <summary>Uma adesão vista por quem pede dependente ou trajeto: de quem é, em que situação e o que o catálogo aceita.</summary>
public sealed record AdesaoParaFilho(int AdesaoId, int FuncionarioId, int BeneficioId, string NomeDoBeneficio, bool AceitaDependentes, bool AceitaTrajeto, StatusDoPedidoDeBeneficio Status, bool Ativo);

public sealed record DependenteDaPessoa(
    int Id,
    int AdesaoId,
    string Nome,
    string Parentesco,
    DateOnly DataNascimento,
    StatusDoPedidoDeBeneficio Status,
    DateTime PedidoEmUtc,
    DateTime? RespondidoEmUtc,
    string? MotivoRecusa);

public sealed record TrajetoDaPessoa(
    int Id,
    int AdesaoId,
    string Origem,
    string Destino,
    string LinhasMeios,
    StatusDoPedidoDeBeneficio Status,
    DateTime PedidoEmUtc,
    DateTime? RespondidoEmUtc,
    string? MotivoRecusa);

/// <summary>O que um pedido de adesão grava: a linha e, junto, os dependentes e o trajeto pedidos com ela.</summary>
public sealed record NovaAdesao(int FuncionarioId, int BeneficioId, DateOnly DataInicioDesejada, bool AutorizaDescontoEmFolha, IReadOnlyList<DadosDoDependente> Dependentes, DadosDoTrajeto? Trajeto, DateTime AgoraUtc);

/// <summary>As leituras da tela "Benefícios" da própria pessoa.</summary>
public interface IConsultaDeAdesoes
{
    Task<IReadOnlyList<ItemDoCatalogo>> CatalogoAsync(CancellationToken ct);

    /// <summary>As linhas de <c>BENEFICIOS_FUNCIONARIOS</c> da pessoa, de qualquer situação, ativas ou não.</summary>
    Task<IReadOnlyList<AdesaoDaPessoa>> AdesoesDaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Os dependentes de todas as adesões da pessoa, de qualquer situação.</summary>
    Task<IReadOnlyList<DependenteDaPessoa>> DependentesDaPessoaAsync(int funcionarioId, CancellationToken ct);

    Task<IReadOnlyList<TrajetoDaPessoa>> TrajetosDaPessoaAsync(int funcionarioId, CancellationToken ct);
}

/// <summary>
/// As gravações dos pedidos de benefício. Tudo acontece na transação em que a pessoa já foi travada (<see cref="TravarPessoaAsync"/>, <c>FOR UPDATE</c> em <c>FUNCIONARIOS</c>, SEMPRE antes de tocar na adesão): é o que serializa dois pedidos da mesma pessoa e o pedido e a edição do RH.
/// A recusa do banco (a chave única da adesão, o trigger de pessoa inativa) sobe já traduzida.
/// </summary>
public interface IAdesoesRepositorio
{
    /// <summary>Trava a linha da pessoa (só vale dentro de transação). <c>null</c>: a pessoa não existe.</summary>
    Task<PessoaParaBeneficio?> TravarPessoaAsync(int funcionarioId, CancellationToken ct);

    Task<ItemDoCatalogo?> ItemDoCatalogoAsync(int beneficioId, CancellationToken ct);

    /// <summary>A linha da pessoa para o benefício (a chave única garante uma só), ou <c>null</c>.</summary>
    Task<AdesaoDaPessoa?> AdesaoDaPessoaAsync(int funcionarioId, int beneficioId, CancellationToken ct);

    /// <summary>A adesão <paramref name="adesaoId"/> SE for da pessoa; de outra pessoa ou inexistente, <c>null</c> (a API não diz qual dos dois).</summary>
    Task<AdesaoParaFilho?> AdesaoDaPessoaPorIdAsync(int adesaoId, int funcionarioId, CancellationToken ct);

    /// <summary>Uma linha nova <c>PENDENTE</c> e INATIVA, com os dependentes e o trajeto pedidos junto. Devolve o id da adesão.</summary>
    Task<int> CriarAdesaoAsync(NovaAdesao dados, CancellationToken ct);

    /// <summary>
    /// A MESMA linha volta a ser pedido <c>PENDENTE</c> (a chave única recusa uma segunda): resposta, data e motivo se limpam juntos, os dependentes e trajetos <c>PENDENTE</c> antigos viram <c>CANCELADO</c> (sem resposta) e os novos entram.
    /// </summary>
    Task ReabrirAdesaoAsync(int adesaoId, NovaAdesao dados, CancellationToken ct);

    /// <summary>O pedido de um dependente numa adesão. Devolve o id.</summary>
    Task<int> CriarDependenteAsync(int adesaoId, DadosDoDependente dependente, DateTime agoraUtc, CancellationToken ct);

    Task<int> CriarTrajetoAsync(int adesaoId, DadosDoTrajeto trajeto, DateTime agoraUtc, CancellationToken ct);
}
