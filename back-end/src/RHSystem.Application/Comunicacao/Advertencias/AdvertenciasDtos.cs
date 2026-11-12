using RHSystem.Application.Colaboradores;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>Quem aplicou a advertência (<c>AdvertenciaAplicadaPor.tipo</c>): o gestor (com o nome) ou o RH (sem nome: o banco não guarda qual pessoa do RH registrou).</summary>
public enum QuemAplicouAAdvertencia
{
    Gestor,
    Rh,
}

/// <summary>Em nome de quem o RH registra (<c>aplicadaPor</c> do corpo): o gestor da pessoa ou o RH.</summary>
public enum AplicadaPorNoRegistro
{
    GestorDoColaborador,
    Rh,
}

/// <summary>A decisão do RH sobre a contestação: manter a advertência ou acatar a contestação (cancelar).</summary>
public enum DecisaoDaResposta
{
    Manter,
    Acatar,
}

public sealed record AdvertenciaAplicadaPor(QuemAplicouAAdvertencia Tipo, string? Nome);

/// <summary>Uma advertência numa lista (RH, gestor ou ficha), sem a descrição (<c>AdvertenciaItem</c>).</summary>
public sealed record AdvertenciaItem(int Id, DateOnly Data, FuncionarioResumo Funcionario, TipoDeAdvertencia Tipo, string Motivo, AdvertenciaAplicadaPor AplicadaPor, SituacaoDaAdvertencia Situacao);

/// <summary>Uma advertência inteira como o RH vê (<c>AdvertenciaDetalheRh</c>), com a contestação e o desfecho.</summary>
public sealed record AdvertenciaDetalheRh(
    int Id,
    DateOnly Data,
    FuncionarioResumo Funcionario,
    TipoDeAdvertencia Tipo,
    string Motivo,
    AdvertenciaAplicadaPor AplicadaPor,
    SituacaoDaAdvertencia Situacao,
    string Descricao,
    DateTime? DataCienciaUtc,
    string? TextoContestacao,
    DateTime? DataContestacaoUtc,
    DesfechoDaContestacao? DesfechoDaContestacao);

/// <summary>Uma advertência inteira como o gestor vê (<c>AdvertenciaDetalheGestor</c>): a situação (inclusive <c>CONTESTADA</c>) mas nunca o texto da contestação nem o desfecho.</summary>
public sealed record AdvertenciaDetalheGestor(
    int Id,
    DateOnly Data,
    FuncionarioResumo Funcionario,
    TipoDeAdvertencia Tipo,
    string Motivo,
    AdvertenciaAplicadaPor AplicadaPor,
    SituacaoDaAdvertencia Situacao,
    string Descricao,
    DateTime? DataCienciaUtc);

/// <summary>Uma advertência da própria pessoa, completa (<c>AdvertenciaEu</c>): sem o resumo da pessoa (é ela) e com tudo, para "Ver detalhes" não precisar de outra chamada.</summary>
public sealed record AdvertenciaEu(
    int Id,
    DateOnly Data,
    TipoDeAdvertencia Tipo,
    string Motivo,
    string Descricao,
    AdvertenciaAplicadaPor AplicadaPor,
    SituacaoDaAdvertencia Situacao,
    DateTime? DataCienciaUtc,
    string? TextoContestacao,
    DateTime? DataContestacaoUtc,
    DesfechoDaContestacao? DesfechoDaContestacao);

public sealed record AdvertenciaContagensRh(int Todas, int Pendentes, int Aplicadas, int Contestadas, int Canceladas);

public sealed record AdvertenciaSetorComMaisRegistros(string Setor, int Quantidade);

/// <summary>Os números do topo da tela de advertências do RH (<c>AdvertenciaResumoRh</c>): do ano, pela data do ocorrido.</summary>
public sealed record AdvertenciaResumoRh(int Ano, AdvertenciaContagensRh Contagens, AdvertenciaSetorComMaisRegistros? SetorComMaisRegistros);

public sealed record AdvertenciaAguardandoCiencia(int Quantidade, IReadOnlyList<FuncionarioResumo> Pessoas);

public sealed record AdvertenciaPorPessoa(FuncionarioResumo Funcionario, int Quantidade);

/// <summary>Os números da tela de advertências do setor (<c>AdvertenciaResumoGestor</c>): do ano, e todos pela regra única das não canceladas.</summary>
public sealed record AdvertenciaResumoGestor(int Ano, int Registradas, AdvertenciaAguardandoCiencia AguardandoCiencia, int PessoasComAdvertencia, int TotalDaEquipe, IReadOnlyList<AdvertenciaPorPessoa> PorPessoa);

/// <summary>O corpo do registro pelo RH (<c>AdvertenciaRegistroRhComando</c>).</summary>
public sealed record AdvertenciaRegistroRhComando(DateOnly DataDoOcorrido, TipoDeAdvertencia Tipo, AplicadaPorNoRegistro AplicadaPor, string Motivo, string Descricao);

/// <summary>O corpo do registro pelo gestor (<c>AdvertenciaRegistroGestorComando</c>): quem aplica é sempre o gestor logado.</summary>
public sealed record AdvertenciaRegistroGestorComando(DateOnly DataDoOcorrido, TipoDeAdvertencia Tipo, string Motivo, string Descricao);

public sealed record AdvertenciaContestacaoComando(string Texto);

public sealed record AdvertenciaRespostaComando(DecisaoDaResposta Decisao, string Resposta);

/// <summary>Uma advertência como veio do banco; o nome do gestor vem <c>null</c> quando foi o RH.</summary>
public sealed record AdvertenciaLida(
    int Id,
    DateOnly Data,
    FuncionarioResumo Funcionario,
    TipoDeAdvertencia Tipo,
    string Motivo,
    string Descricao,
    SituacaoDaAdvertencia Situacao,
    string? GestorNome,
    DateTime? CienciaEmUtc,
    string? TextoContestacao,
    DateTime? ContestacaoEmUtc);

/// <summary>Quem consulta e por qual porta: a própria pessoa (só as dela), o gestor (só as da equipe dele, <c>null</c> sem equipe é nenhuma) ou o RH (todas).</summary>
public sealed record EscopoDeAdvertencias(PapelNaConsulta Papel, int FuncionarioId, EquipeDoGestor? Equipe);

/// <summary>Os filtros das listas. <c>Ano</c> é o da data do ocorrido (<c>null</c> = todos os anos); <c>SetorId</c> é o setor ATUAL da pessoa; a <c>busca</c> (nome e motivo) vai em <see cref="Comum.Consultas.ParametrosDeLista"/>.</summary>
public sealed record FiltroDeAdvertencias(int? Ano, SituacaoDaAdvertencia? Situacao, TipoDeAdvertencia? Tipo, int? SetorId, int? FuncionarioId);

public sealed record PaginaDeAdvertencias(IReadOnlyList<AdvertenciaLida> Itens, int Total);

/// <summary>O que o registro precisa saber da pessoa: o nome, se está ativa, o e-mail, o gestor dela (<c>FUNCIONARIOS.ID_GESTOR</c>, que pode apontar para um gestor já desligado) e o nome.</summary>
public sealed record PessoaParaAdvertencia(int Id, string Nome, bool Ativo, string? EmailCorporativo, int? GestorId, bool GestorAtivo);

/// <summary>Uma advertência do ano para o resumo do RH: a situação e o setor atual da pessoa (<c>null</c> sem setor).</summary>
public sealed record AdvertenciaParaResumoRh(SituacaoDaAdvertencia Situacao, string? Setor);

/// <summary>Uma advertência do ano de uma pessoa da equipe, para o resumo do gestor.</summary>
public sealed record AdvertenciaParaResumoDaEquipe(int FuncionarioId, SituacaoDaAdvertencia Situacao);

/// <summary>Quem do RH recebe a cópia do e-mail de uma advertência registrada pelo gestor: a pessoa ativa com perfil RH e e-mail corporativo.</summary>
public sealed record DestinatarioDoRh(int FuncionarioId, string Nome, string Email);
