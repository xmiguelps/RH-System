using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Cadastros;

/// <summary>O acesso aos setores e ao que a troca de gestor mexe. As gravações passam pelo <c>SaveChanges</c> do contexto, onde o interceptor grava o evento de auditoria na mesma transação.</summary>
public interface ISetoresRepositorio
{
    /// <summary>Uma página de setores, por nome (desempate pelo id). <c>busca</c> procura no nome e na descrição. <paramref name="hoje"/> é o dia em -03:00 (a pessoa em admissão ainda não conta).</summary>
    Task<RespostaPaginada<SetorItem>> ListarAsync(ParametrosDeLista p, DateOnly hoje, CancellationToken ct);

    /// <summary>O setor como a lista o mostra; <c>null</c> se não existe.</summary>
    Task<SetorItem?> ObterItemAsync(int setorId, DateOnly hoje, CancellationToken ct);

    /// <summary>O setor RASTREADO, com o gestor, a pessoa dele e o nível dela (o log de auditoria mostra o NOME do gestor antes e depois); <c>null</c> se não existe.</summary>
    Task<Setor?> ObterAsync(int setorId, CancellationToken ct);

    /// <summary>Já existe outro setor com este nome (sem diferenciar maiúsculas nem acentos)? <paramref name="ignorarId"/> é o próprio setor, na edição.</summary>
    Task<bool> NomeEmUsoAsync(string nome, int? ignorarId, CancellationToken ct);

    /// <summary>A pessoa RASTREADA, com o nível de acesso; <c>null</c> se não existe.</summary>
    Task<Funcionario?> ObterPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>O nome do setor que esta pessoa já gerencia, fora <paramref name="ignorarSetorId"/>; <c>null</c> se não gerencia nenhum. A regra "um gestor, um setor" é da API: o banco não a impõe.</summary>
    Task<string?> NomeDoSetorQueGerenciaAsync(int funcionarioId, int? ignorarSetorId, CancellationToken ct);

    /// <summary>A linha de <c>GESTORES</c> RASTREADA da pessoa, com ela; <c>null</c> se ainda não é gestor.</summary>
    Task<Gestor?> ObterGestorDaPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>O nível de acesso RASTREADO do perfil.</summary>
    Task<NivelDeAcesso> ObterNivelAsync(Perfil perfil, CancellationToken ct);

    void AdicionarGestor(Gestor gestor);

    void AdicionarSetor(Setor setor);

    /// <summary>Grava a linha nova de <c>GESTORES</c> e as mudanças de nível, sem tradução de erro (a única chave única da tabela é a da pessoa).</summary>
    Task SalvarGestorEOsNiveisAsync(CancellationToken ct);

    /// <summary>
    /// Define o <c>ID_GESTOR</c> do colaborador ATIVO (um UPDATE só, no contexto da transação do chamador), sem evento de auditoria próprio: o gestor do colaborador é derivado do setor e o evento é o da troca de gestor do setor.
    /// </summary>
    Task DefinirGestorDoColaboradorAsync(int funcionarioId, int gestorId, CancellationToken ct);

    /// <summary>Grava o setor. Nome repetido que passou pela verificação (corrida) volta como <see cref="ConflitoException"/> <c>NOME_JA_CADASTRADO</c>.</summary>
    Task SalvarAsync(CancellationToken ct);
}
