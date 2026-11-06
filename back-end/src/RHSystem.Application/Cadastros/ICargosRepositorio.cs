using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Cadastros;

/// <summary>O acesso aos cargos. As gravações passam pelo <c>SaveChanges</c> do contexto, onde o interceptor grava o evento de auditoria na mesma transação.</summary>
public interface ICargosRepositorio
{
    /// <summary>Uma página de cargos, por nome (desempate pelo id). <c>busca</c> procura no nome e na descrição.</summary>
    Task<RespostaPaginada<CargoItem>> ListarAsync(ParametrosDeLista p, CancellationToken ct);

    /// <summary>O cargo para editar, rastreado; <c>null</c> se não existe.</summary>
    Task<Cargo?> ObterAsync(int cargoId, CancellationToken ct);

    /// <summary>Já existe outro cargo com este nome (sem diferenciar maiúsculas nem acentos)? <paramref name="ignorarId"/> é o próprio cargo, na edição.</summary>
    Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct);

    /// <summary>Quantas pessoas ativas têm o cargo.</summary>
    Task<int> QuantidadeDePessoasAsync(int cargoId, CancellationToken ct);

    /// <summary>Grava o cargo novo. Nome repetido que passou pela verificação (corrida) volta como <see cref="ConflitoException"/> <c>NOME_JA_CADASTRADO</c>.</summary>
    Task AdicionarAsync(Cargo cargo, CancellationToken ct);

    /// <summary>Grava o cargo editado, com a mesma tradução do nome repetido.</summary>
    Task SalvarAsync(CancellationToken ct);
}
