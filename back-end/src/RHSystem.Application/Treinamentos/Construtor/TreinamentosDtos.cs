using RHSystem.Application.Colaboradores;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>
/// O corpo de <c>criarTreinamento</c> e <c>atualizarTreinamento</c> (<c>TreinamentoComando</c>): só o título é obrigatório e o <c>PUT</c> substitui TODOS os campos (campo omitido volta ao padrão: <c>obrigatorio</c> e <c>onboarding</c> falsos, <c>emiteCertificado</c> e <c>incluiAdmitidos</c> VERDADEIROS, listas vazias,
/// descrição, data e público nulos). <c>SetorIds</c> só vale com <c>SETORES</c> e <c>FuncionarioIds</c> só com <c>PESSOAS</c>.
/// </summary>
public sealed record TreinamentoComando(string Titulo)
{
    public string? Descricao { get; init; }

    public DateOnly? ComecaEm { get; init; }

    public bool Obrigatorio { get; init; }

    public bool Onboarding { get; init; }

    public bool EmiteCertificado { get; init; } = true;

    public PublicoDoTreinamento? Publico { get; init; }

    public IReadOnlyList<int> SetorIds { get; init; } = [];

    public IReadOnlyList<int> FuncionarioIds { get; init; } = [];

    public bool IncluiAdmitidos { get; init; } = true;
}

/// <summary>Um setor escolhido no público (<c>TreinamentoSetorResumo</c>).</summary>
public sealed record TreinamentoSetorResumo(int Id, string Nome);

/// <summary>Um módulo como o RH o vê (<c>TreinamentoModuloRh</c>): com o texto e o link, o PDF só como <c>NomeArquivo</c> (o caminho de armazenamento nunca sai).</summary>
public sealed record TreinamentoModuloRh(int Id, int Ordem, string Titulo, TipoDeModulo Tipo, int? DuracaoMinutos, string? UrlExterna, string? ConteudoTexto, string? NomeArquivo);

/// <summary>
/// O treinamento inteiro, como o RH o edita (<c>TreinamentoRh</c>): a <c>Situacao</c> é calculada pelas datas de hoje; <c>Setores</c> só tem itens com <c>SETORES</c> e <c>Pessoas</c> só com <c>PESSOAS</c>; <c>Descricao</c>, <c>ComecaEm</c> e <c>Publico</c> são opcionais no contrato (sempre devolvidos, com <c>null</c>).
/// </summary>
public sealed record TreinamentoRh(
    int Id,
    string Titulo,
    DateTime? EncerradoEmUtc,
    bool Obrigatorio,
    bool Onboarding,
    bool EmiteCertificado,
    bool Publicado,
    SituacaoDoTreinamento Situacao,
    IReadOnlyList<TreinamentoSetorResumo> Setores,
    IReadOnlyList<FuncionarioResumo> Pessoas,
    bool IncluiAdmitidos,
    IReadOnlyList<TreinamentoModuloRh> Modulos,
    string? Descricao = null,
    DateOnly? ComecaEm = null,
    PublicoDoTreinamento? Publico = null);

/// <summary>Uma linha da lista do RH (<c>TreinamentoRhResumo</c>). <c>Concluidos</c> são as pessoas com <c>CONCLUIDO</c>; <c>CertificadosEmitidos</c> é igual a <c>Concluidos</c> quando o treinamento emite certificado e 0 quando não emite.</summary>
public sealed record TreinamentoRhResumo(int Id, string Titulo, bool Obrigatorio, bool Onboarding, bool EmiteCertificado, SituacaoDoTreinamento Situacao, DateOnly? ComecaEm, DateTime? EncerradoEmUtc, PublicoDoTreinamento? Publico, int TotalModulos, int TotalParticipantes, int Concluidos, int CertificadosEmitidos);

/// <summary>O número de cada aba da lista (<c>TreinamentoRhContagens</c>).</summary>
public sealed record TreinamentoRhContagens(int Todos, int Rascunhos, int Agendados, int EmAndamento, int Encerrados);

/// <summary>A lista do RH (<c>TreinamentoRhLista</c>): a página e as contagens das abas.</summary>
public sealed record TreinamentoRhLista(IReadOnlyList<TreinamentoRhResumo> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, TreinamentoRhContagens Contagens);

/// <summary>Os números do topo da tela 22 (<c>TreinamentoRhIndicadores</c>); <c>Ano</c> é o ano de <c>CertificadosEmitidosNoAno</c>.</summary>
public sealed record TreinamentoRhIndicadores(int EmAndamento, int Obrigatorios, int CertificadosEmitidosNoAno, int Ano, int PessoasComObrigatorioPendente);

/// <summary>A contagem do passo Publicação (<c>TreinamentoPublicoContagem</c>).</summary>
public sealed record TreinamentoPublicoContagem(int Total);
