using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Cadastros;

/// <summary>O acesso aos benefícios do catálogo. As gravações passam pelo <c>SaveChanges</c> do contexto, onde o interceptor grava o evento de auditoria na mesma transação.</summary>
public interface IBeneficiosRepositorio
{
    /// <summary>Uma página de benefícios, por id. <c>busca</c> procura no nome e na descrição.</summary>
    Task<RespostaPaginada<BeneficioCatalogoItem>> ListarAsync(ParametrosDeLista p, CancellationToken ct);

    /// <summary>O benefício para editar ou excluir, rastreado; <c>null</c> se não existe.</summary>
    Task<Beneficio?> ObterAsync(int beneficioId, CancellationToken ct);

    /// <summary>Já existe outro benefício com este nome (sem diferenciar maiúsculas nem acentos)? <paramref name="ignorarId"/> é o próprio benefício, na edição.</summary>
    Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct);

    /// <summary>Quantas linhas de <c>BeneficioDoFuncionario</c> ativas apontam para o benefício (pedido pendente ou inativo não conta).</summary>
    Task<int> QuantidadeDePessoasAsync(int beneficioId, CancellationToken ct);

    /// <summary>Alguma linha de <c>BeneficioDoFuncionario</c>, de qualquer status e ativa ou não, aponta para o benefício?</summary>
    Task<bool> TemVinculoAsync(int beneficioId, CancellationToken ct);

    /// <summary>Grava o benefício novo. Nome repetido que passou pela verificação (corrida) volta como <see cref="ConflitoException"/> <c>NOME_JA_CADASTRADO</c>.</summary>
    Task AdicionarAsync(Beneficio beneficio, CancellationToken ct);

    /// <summary>Grava o benefício editado, com a mesma tradução do nome repetido.</summary>
    Task SalvarAsync(CancellationToken ct);

    /// <summary>
    /// Exclui o benefício (<c>Remove</c> rastreado: o interceptor grava <c>Excluiu</c>). Um vínculo criado entre a conferência e a exclusão faz o banco recusar (<c>1451</c>), e isso volta como
    /// <see cref="ConflitoException"/> <c>CADASTRO_EM_USO</c>, nunca como erro 500.
    /// </summary>
    Task ExcluirAsync(Beneficio beneficio, CancellationToken ct);
}
