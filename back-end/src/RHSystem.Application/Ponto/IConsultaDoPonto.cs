using RHSystem.Application.Escopo;

namespace RHSystem.Application.Ponto;

/// <summary>A pessoa como o espelho a conhece: a identificação do <c>FuncionarioResumo</c>, as datas que limitam os dias que aparecem e a carga semanal do cargo (em horas).</summary>
public sealed record PessoaDoEspelho(int Id, string Nome, string? Cargo, string? Setor, bool Ativo, DateOnly DataAdmissao, DateOnly? DataDesligamento, decimal? CargaSemanalHoras);

/// <summary>Uma batida e o dia do ponto a que ela pertence (o dia da ENTRADA, mesmo que o instante caia depois da meia-noite).</summary>
public sealed record BatidaDoDia(int FuncionarioId, DateOnly Dia, BatidaLida Batida);

/// <summary>Um atestado VALIDADO: de dia inteiro (<c>HoraInicio</c> nula, vários dias) ou de algumas horas de um dia só.</summary>
public sealed record AtestadoValidadoDoPonto(int FuncionarioId, DateOnly Inicio, DateOnly Fim, TimeOnly? HoraInicio, TimeOnly? HoraFim);

/// <summary>Um pedido de ajuste <c>PENDENTE</c>: o dia do ponto, e se aguarda a etapa do gestor (<c>ID_GESTOR</c> preenchido e sem resultado dele) e qual gestor (<c>GESTORES.ID_GESTOR</c>).</summary>
public sealed record PedidoPendenteDoDia(int FuncionarioId, DateOnly Dia, int PedidoId, bool NaEtapaDoGestor, int? GestorId);

/// <summary>As leituras do ponto que o espelho e o ponto da equipe fazem (só leitura; a gravação é de <see cref="IPontoRepositorio"/>).</summary>
public interface IConsultaDoPonto
{
    /// <summary>Qualquer pessoa (a própria ou, para o RH, de qualquer um); <c>null</c> se não existe.</summary>
    Task<PessoaDoEspelho?> PessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Só se a pessoa é da equipe (<c>DaEquipe</c>: ativos do setor, sem o gestor); fora dela, <c>null</c>.</summary>
    Task<PessoaDoEspelho?> PessoaDaEquipeAsync(EquipeDoGestor equipe, int funcionarioId, CancellationToken ct);

    /// <summary>A equipe do gestor, por nome e depois por id.</summary>
    Task<IReadOnlyList<PessoaDoEspelho>> PessoasDaEquipeAsync(EquipeDoGestor equipe, CancellationToken ct);

    /// <summary>As batidas dos dias do ponto entre as duas datas (inclusive) das pessoas, por horário e depois por id.</summary>
    Task<IReadOnlyList<BatidaDoDia>> BatidasAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly de, DateOnly ate, CancellationToken ct);

    /// <summary>Os atestados validados que têm algum dia entre as duas datas (inclusive).</summary>
    Task<IReadOnlyList<AtestadoValidadoDoPonto>> AtestadosValidadosAsync(IReadOnlyCollection<int> funcionarioIds, DateOnly de, DateOnly ate, CancellationToken ct);

    /// <summary>Os pedidos <c>PENDENTE</c> das pessoas, de qualquer mês.</summary>
    Task<IReadOnlyList<PedidoPendenteDoDia>> PedidosPendentesAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct);
}
