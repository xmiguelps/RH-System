using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>O certificado da pessoa neste treinamento (<c>CertificadoResumo</c>): só existe quando ela concluiu e o treinamento emite certificado. O código é sempre um UUID versão 4.</summary>
public sealed record CertificadoResumo(string Numero, string CodigoValidacao, DateOnly EmitidoEm);

/// <summary>O cartão de "Meus treinamentos" (<c>TreinamentoMeuCartao</c>).</summary>
public sealed record TreinamentoMeuCartao(
    int Id,
    string Titulo,
    string? Descricao,
    bool Obrigatorio,
    bool Onboarding,
    MinhaSituacaoNoTreinamento Situacao,
    bool Encerrado,
    DateOnly? ComecaEm,
    int TotalModulos,
    int? DuracaoTotalMinutos,
    int ModulosConcluidos,
    int ProgressoPercentual,
    int? ModuloAtual,
    DateOnly? IniciadoEm,
    DateTime? ConcluidoEmUtc,
    CertificadoResumo? Certificado);

/// <summary>A lista de cartões com as contagens das abas (<c>TreinamentoMeuLista</c>).</summary>
public sealed record TreinamentoMeuLista(IReadOnlyList<TreinamentoMeuCartao> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, ContagensDosMeusTreinamentos Contagens);

/// <summary>Um módulo na tela de execução, sem o conteúdo (<c>TreinamentoModuloEu</c>).</summary>
public sealed record TreinamentoModuloEu(int Id, int Ordem, string Titulo, TipoDeModulo Tipo, int? DuracaoMinutos, bool Concluido, DateTime? ConcluidoEmUtc);

/// <summary>O cartão e a lista de módulos (<c>TreinamentoMeuDetalhe</c>, o cartão mais <c>modulos</c>).</summary>
public sealed record TreinamentoMeuDetalhe(
    int Id,
    string Titulo,
    string? Descricao,
    bool Obrigatorio,
    bool Onboarding,
    MinhaSituacaoNoTreinamento Situacao,
    bool Encerrado,
    DateOnly? ComecaEm,
    int TotalModulos,
    int? DuracaoTotalMinutos,
    int ModulosConcluidos,
    int ProgressoPercentual,
    int? ModuloAtual,
    DateOnly? IniciadoEm,
    DateTime? ConcluidoEmUtc,
    CertificadoResumo? Certificado,
    IReadOnlyList<TreinamentoModuloEu> Modulos);

/// <summary>O módulo aberto na tela de execução (<c>TreinamentoModuloConteudo</c>): <c>ConteudoTexto</c> é texto (nunca HTML), <c>UrlExterna</c> o link de vídeo ou de página, <c>TemArquivo</c> diz se o PDF existe (o caminho nunca sai); <c>Concluido</c> e <c>ConcluidoEmUtc</c> são do progresso da PRÓPRIA pessoa.</summary>
public sealed record TreinamentoModuloConteudo(int Id, int Ordem, string Titulo, TipoDeModulo Tipo, int? DuracaoMinutos, string? ConteudoTexto, string? UrlExterna, bool TemArquivo, bool Concluido, DateTime? ConcluidoEmUtc);

/// <summary>Um módulo como a pessoa o vê: <c>ConcluidoEmUtc</c> é o <c>DATA_CONCLUSAO</c> do progresso dela (<c>null</c> se ainda não concluiu).</summary>
public sealed record MeuModuloLido(int Id, int Ordem, string Titulo, TipoDeModulo Tipo, short? DuracaoMinutos, DateTime? ConcluidoEmUtc);

/// <summary>A linha do certificado como o banco a guarda (o trigger a cria na conclusão; a aplicação troca o código por um UUID v4 na mesma transação).</summary>
public sealed record CertificadoLido(int Id, DateOnly? EmitidoEm, string? CodigoValidacao);

/// <summary>
/// Um treinamento que a PESSOA pode ver, com a linha dela (<c>null</c> = opcional do público dela que ainda não escolheu), os módulos com o progresso dela e o certificado. Só vem o que a visibilidade deixa: publicado, liberado, com linha <c>INSCRITO</c>, <c>EM_ANDAMENTO</c> ou <c>CONCLUIDO</c> (ou opcional do público dela sem linha) e, se encerrado,
/// só para quem começou ou concluiu.
/// </summary>
public sealed record MeuTreinamentoLido(
    int Id,
    string Titulo,
    string? Descricao,
    bool Obrigatorio,
    bool Onboarding,
    DateOnly? ComecaEm,
    DateTime? EncerradoEmUtc,
    bool EmiteCertificado,
    SituacaoNoTreinamento? Linha,
    DateOnly? IniciadoEm,
    IReadOnlyList<MeuModuloLido> Modulos,
    CertificadoLido? Certificado);
