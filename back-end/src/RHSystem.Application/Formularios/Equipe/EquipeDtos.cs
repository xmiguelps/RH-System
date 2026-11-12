using RHSystem.Application.Colaboradores;
using RHSystem.Application.Formularios.Acompanhar;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Equipe;

/// <summary>Um pedido do RH aberto na equipe do gestor (<c>FormularioEquipeItem</c>). Contagens SÓ da equipe; no anônimo aberto <c>Respondidos</c> e a taxa são nulos (só <c>Esperados</c> é exato). Nunca traz a avaliação do gestor nem a Sugestão.</summary>
public sealed record FormularioEquipeItem(int Id, string Titulo, TipoDeFormulario Tipo, SentidoDaAvaliacao? SentidoAvaliacao, bool Anonimo, DateOnly? Prazo, int? Respondidos, int Esperados, int? TaxaDeRespostaPercentual);

/// <summary>A coluna "Pedidos do RH" da tela do gestor: sem paginação, pelo prazo mais próximo e depois pelo título.</summary>
public sealed record FormularioEquipeLista(IReadOnlyList<FormularioEquipeItem> Itens);

/// <summary>Uma pessoa da equipe e se respondeu: só QUEM e QUANDO, nunca o conteúdo. <c>Observacao</c> vem das férias aprovadas que cobrem hoje ("de férias até 17/10").</summary>
public sealed record FormularioEquipeRespondente(FuncionarioResumo Funcionario, string? Observacao, SituacaoDoRespondente Situacao, DateTime? RespondidoEmUtc);

/// <summary>O painel "Quem respondeu" do gestor (<c>FormularioEquipeRespondentes</c>). No anônimo <c>Itens</c> é sempre vazio e, enquanto aberto, só <c>Esperados</c> vem exato.</summary>
public sealed record FormularioEquipeRespondentes(int FormularioId, string Titulo, bool Anonimo, int? Respondidos, int? Faltam, int Esperados, int? TaxaDeRespostaPercentual, IReadOnlyList<FormularioEquipeRespondente> Itens);

/// <summary>Uma pessoa da equipe de um gestor (a definição única: ativa, com a admissão já começada, do setor, sem o próprio gestor).</summary>
public sealed record MembroDaEquipe(int Id, string Nome, bool Ativo, string? Cargo, string? Setor);

/// <summary>Um formulário PUBLICADO que pode chegar à equipe (público de pessoas ou setores; fora a Sugestão e as avaliações), como o banco o guarda.</summary>
public sealed record FormularioParaAEquipe(int Id, string Titulo, TipoDeFormulario Tipo, bool Anonimo, bool Ativo, DateTime? PublicadoEmUtc, DateTime? EncerradoEmUtc, DateOnly? Prazo);

/// <summary>A leitura do gestor sobre os formulários da equipe dele. Nunca olha o conteúdo de uma resposta.</summary>
public interface IConsultaDeEquipeDoFormulario
{
    /// <summary>A equipe do setor: ativos, admissão já começada (hoje conta), sem o gestor do setor, por nome.</summary>
    Task<IReadOnlyList<MembroDaEquipe>> MembrosAsync(int setorId, DateOnly hoje, CancellationToken ct);

    /// <summary>Os formulários ativos e não encerrados, por pessoas ou setores, que não são Sugestão nem avaliação (o prazo vencido é filtrado por quem chama).</summary>
    Task<IReadOnlyList<FormularioParaAEquipe>> AbertosAsync(CancellationToken ct);

    /// <summary>Quantas participações do formulário anônimo são de gente desta lista (só a contagem: o gestor fica sabendo quantos da equipe participaram, nunca quem).</summary>
    Task<int> ParticipacoesDaEquipeAsync(int formularioId, IReadOnlyCollection<int> membros, CancellationToken ct);
}
