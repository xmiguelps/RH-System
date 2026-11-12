using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Avisos;

/// <summary>Quantas pessoas já abriram o aviso (<c>AvisoLeituraContagem</c>). Só pessoas <b>ativas hoje</b> entram na conta (decisão 2 do plano da fatia 4).</summary>
public sealed record AvisoLeituraContagem(int Viram, int Total);

/// <summary>Um aviso na lista do RH (<c>AvisoItemRh</c>), sem o conteúdo inteiro. <c>Leitura</c> só nos avisos ativos e expirados; <c>null</c> em rascunho e agendado.</summary>
public sealed record AvisoItemRh(
    int Id,
    TipoDeAviso Tipo,
    string Titulo,
    string Previa,
    SituacaoDoAviso Situacao,
    bool Fixado,
    DateTime? PublicacaoEmUtc,
    DateTime? ExpiraEmUtc,
    string? AutorNome,
    AvisoLeituraContagem? Leitura);

/// <summary>Um aviso com o conteúdo inteiro, para o painel de detalhes do RH (<c>AvisoDetalheRh</c>).</summary>
public sealed record AvisoDetalheRh(
    int Id,
    TipoDeAviso Tipo,
    string Titulo,
    string Previa,
    SituacaoDoAviso Situacao,
    bool Fixado,
    DateTime? PublicacaoEmUtc,
    DateTime? ExpiraEmUtc,
    string? AutorNome,
    AvisoLeituraContagem? Leitura,
    string Conteudo);

public sealed record AvisoContagens(int Ativos, int Agendados, int Expirados, int Rascunhos);

public sealed record AvisoProximoAExpirar(int AvisoId, string Titulo, DateTime ExpiraEmUtc);

/// <summary>Os números do topo da tela de avisos do RH e a contagem de cada aba (<c>AvisoPainelRh</c>).</summary>
public sealed record AvisoPainelRh(AvisoContagens Contagens, int? LeituraMediaDosAtivosPercentual, int ExpiramNosProximos7Dias, AvisoProximoAExpirar? ProximoAExpirar);

/// <summary>
/// O corpo de <c>criarAviso</c>. <c>PublicarEmUtc</c> e <c>ExpiraEm</c> são opcionais no contrato (por isso não estão no construtor: o comparador de contrato exige que o <c>required</c> bata exatamente). Com <c>Publicar = true</c>,
/// data vazia ou passada publica agora e futura agenda; com <c>false</c> é a data prevista do rascunho. <c>ExpiraEm</c> é o último dia em que o aviso vale (fim do dia -03:00).
/// </summary>
public sealed record AvisoCriacaoComando(TipoDeAviso Tipo, string Titulo, string Conteudo, bool Fixado, bool Publicar)
{
    public DateTime? PublicarEmUtc { get; init; }

    public DateOnly? ExpiraEm { get; init; }
}

/// <summary>O corpo de <c>editarAviso</c>: o tipo não muda. <c>PublicarEmUtc</c> só muda em rascunho e agendado; <c>ExpiraEm</c> vazio tira a expiração.</summary>
public sealed record AvisoEdicaoComando(string Titulo, string Conteudo, bool Fixado)
{
    public DateTime? PublicarEmUtc { get; init; }

    public DateOnly? ExpiraEm { get; init; }
}

/// <summary>Os filtros da lista do RH, já conferidos: a aba (padrão ativos) e o tipo.</summary>
public sealed record FiltroDeAvisosRh(SituacaoDoAviso Situacao, TipoDeAviso? Tipo);

/// <summary>Um aviso lido do banco para a consulta, com o nome do autor e a contagem de leitura (só pessoas ativas). O caso de uso calcula a situação, a prévia e decide se mostra a leitura.</summary>
public sealed record AvisoLido(
    int Id,
    TipoDeAviso? Tipo,
    string Titulo,
    string Conteudo,
    DateTime? PublicacaoEmUtc,
    DateTime? ExpiracaoEmUtc,
    bool Fixado,
    bool Enviado,
    string? AutorNome,
    int Viram,
    int Total);

/// <summary>O que a consulta sabe do resumo: as contagens de cada aba, a média de leitura dos ativos (já em percentual inteiro) e o próximo a expirar.</summary>
public sealed record ResumoDosAvisos(int Ativos, int Agendados, int Expirados, int Rascunhos, int? LeituraMediaPercentual, int ExpiramNosProximos7Dias, AvisoProximoAExpirar? ProximoAExpirar);
