using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Gestores;

/// <summary>
/// Os gestores no banco. "Quem é gestor" (lista e 404) é: linha em <c>GESTORES</c>, pessoa <b>ativa</b> e (gerencia um setor ou tem o nível <c>RH</c>). Quem só tem o nível <c>Gestor</c>, sem setor, e quem
/// voltou ao nível <c>Funcionario</c> mantêm a linha no banco (os pedidos antigos a referenciam), mas não são gestores aqui.
/// </summary>
public interface IGestoresRepositorio
{
    /// <summary>Quem é gestor, na ordem de <c>gestorId</c>, com o setor que gerencia e a equipe (a definição única da 1a). <paramref name="hoje"/> é o dia em -03:00.</summary>
    Task<IReadOnlyList<GestorItem>> ListarAsync(DateOnly hoje, CancellationToken ct);

    /// <summary>O gestor, ou <c>null</c> se não existe ou se não é gestor (ver o resumo da interface).</summary>
    Task<GestorItem?> ObterAsync(int gestorId, DateOnly hoje, CancellationToken ct);

    /// <summary>A linha de <c>GESTORES</c> RASTREADA, com a pessoa (o nome entra no log de auditoria), para marcar ou desmarcar a ausência.</summary>
    Task<Gestor?> ObterParaAusenciaAsync(int gestorId, CancellationToken ct);

    /// <summary>A pessoa RASTREADA, com o nível de acesso; <c>null</c> se não existe.</summary>
    Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct);

    Task<bool> JaECadastradoAsync(int funcionarioId, CancellationToken ct);

    /// <summary>Grava a linha nova. Uma corrida em que outra chamada cadastrou a mesma pessoa antes (a chave única do banco) vira 409 <c>GESTOR_JA_CADASTRADO</c>.</summary>
    Task<Gestor> AdicionarAsync(Funcionario pessoa, CancellationToken ct);

    Task SalvarAsync(CancellationToken ct);
}
