using RHSystem.Application.Colaboradores;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Equipe;

/// <summary>Os três números do topo da ficha (<c>resumo</c>): <c>Pendentes</c> pela definição única (obrigatório EM ANDAMENTO em que a pessoa está <c>INSCRITO</c> ou <c>EM_ANDAMENTO</c>; opcional nunca é pendência; pessoa inativa não tem pendência).</summary>
public sealed record TreinamentoFichaResumo(int Pendentes, int EmAndamento, int Concluidos);

/// <summary>Uma linha da pessoa num treinamento na ficha do RH (<c>TreinamentoFichaItem</c>): com o certificado para o RH baixar. O gestor NUNCA recebe o certificado, por isso o item dele é outro tipo.</summary>
public sealed record TreinamentoFichaItemRh(int TreinamentoId, string Titulo, bool Obrigatorio, bool Onboarding, SituacaoNoTreinamento Situacao, DateOnly? IniciadoEm, DateTime? ConcluidoEmUtc, int ModulosConcluidos, int TotalModulos, bool CertificadoDisponivel);

/// <summary><c>TreinamentoFichaRhResposta</c>: o resumo e as linhas da pessoa (sem paginação; lista curta).</summary>
public sealed record TreinamentoFichaRhResposta(TreinamentoFichaResumo Resumo, IReadOnlyList<TreinamentoFichaItemRh> Itens);

/// <summary>A linha da ficha do gestor (<c>TreinamentoFichaGestorItem</c>): sem certificado nem data final.</summary>
public sealed record TreinamentoFichaItemGestor(int TreinamentoId, string Titulo, bool Obrigatorio, bool Onboarding, SituacaoNoTreinamento Situacao, DateOnly? IniciadoEm, DateTime? ConcluidoEmUtc, int ModulosConcluidos, int TotalModulos);

/// <summary><c>TreinamentoFichaGestorResposta</c>.</summary>
public sealed record TreinamentoFichaGestorResposta(TreinamentoFichaResumo Resumo, IReadOnlyList<TreinamentoFichaItemGestor> Itens);

/// <summary>Um treinamento na lista da equipe (<c>TreinamentoGestorResumo</c>): <c>TotalNaEquipe</c> é o número de pessoas da equipe com linha (obrigatório) ou no público (opcional), <c>Concluiram</c> as com <c>CONCLUIDO</c> e <c>Inscritos</c> as com linha.</summary>
public sealed record TreinamentoGestorResumo(int Id, string Titulo, bool Obrigatorio, bool Onboarding, SituacaoDoTreinamento Situacao, DateOnly? ComecaEm, int TotalNaEquipe, int Concluiram, int Inscritos);

/// <summary><c>TreinamentoGestorLista</c>: sem paginação.</summary>
public sealed record TreinamentoGestorLista(IReadOnlyList<TreinamentoGestorResumo> Itens);

/// <summary>Uma pessoa da equipe no treinamento (<c>TreinamentoGestorPessoa</c>): sem nota nem certificado. <c>ModuloAtual</c> é a ordem do primeiro módulo que falta; <c>EmFeriasAte</c> vem das férias de hoje (<c>FERIAS</c>).</summary>
public sealed record TreinamentoGestorPessoa(FuncionarioResumo Funcionario, SituacaoNoTreinamento Situacao, DateTime? ConcluidoEmUtc, int ModulosConcluidos, int TotalModulos, int? ModuloAtual, DateOnly? EmFeriasAte);

/// <summary><c>TreinamentoGestorEquipeResposta</c>: o treinamento, os dois números do topo e as pessoas por nome.</summary>
public sealed record TreinamentoGestorEquipeResposta(TreinamentoGestorResumo Treinamento, int Concluiram, int FaltamConcluir, IReadOnlyList<TreinamentoGestorPessoa> Itens);

/// <summary>Uma pessoa da equipe com linha no treinamento, como a consulta a lê.</summary>
public sealed record PessoaDaEquipeNoTreinamento(int FuncionarioId, string Nome, string? Cargo, string? Setor, bool Ativo, SituacaoNoTreinamento Situacao, DateTime? ConcluidoEmUtc, IReadOnlyList<ModuloParaProgresso> Modulos);

/// <summary>Um treinamento publicado com os números da equipe do gestor (a consulta já conta; o caso de uso só monta).</summary>
public sealed record TreinamentoDaEquipeLido(int Id, string Titulo, bool Obrigatorio, bool Onboarding, DateOnly? ComecaEm, DateTime? EncerradoEmUtc, int TotalNaEquipe, int Concluiram, int Inscritos);

/// <summary>A leitura dos treinamentos para a pendência, a ficha e a equipe.</summary>
public interface IConsultaDaEquipeNosTreinamentos
{
    /// <summary>
    /// Quantas pendências a pessoa ATIVA tem (obrigatório em andamento em que está <c>INSCRITO</c> ou <c>EM_ANDAMENTO</c>); <c>0</c> para pessoa inativa. É a fonte do indicador <c>treinamentosPendentes</c> da ficha e o <c>resumo.pendentes</c> das duas fichas.
    /// </summary>
    Task<int> PendenciasAsync(int funcionarioId, DateOnly hoje, CancellationToken ct);

    /// <summary>Os treinamentos publicados que alcançam ao menos uma pessoa da equipe (ou em que ela tem linha), com os números dela; <paramref name="equipe"/> e <paramref name="setorId"/> são os do gestor. Sem ordem.</summary>
    Task<IReadOnlyList<TreinamentoDaEquipeLido>> TreinamentosDaEquipeAsync(IReadOnlyList<int> equipe, int setorId, CancellationToken ct);

    /// <summary>Quem da equipe tem linha válida no treinamento (ordem por nome).</summary>
    Task<IReadOnlyList<PessoaDaEquipeNoTreinamento>> PessoasDaEquipeAsync(int treinamentoId, IReadOnlyList<int> equipe, CancellationToken ct);
}
