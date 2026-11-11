using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Documentos;

// Os nomes são os dos schemas de contrato/fontes/20-fatia-1-colaboradores.yaml e o JSON é camelCase. Todos os campos de DocumentoColaborador são obrigatórios no contrato (os que podem faltar aceitam null), por isso nenhum tem padrão.

/// <summary>O modo do formulário "Adicionar documento": <c>ANEXAR_AGORA</c> ("Você já tem o arquivo") ou <c>PEDIR_PARA_ENVIAR</c> (a pessoa envia pelo portal; nenhum e-mail sai).</summary>
public enum ModoDeDocumento
{
    AnexarAgora,
    PedirParaEnviar,
}

/// <summary>Um documento da pessoa. <b>Nunca</b> leva o caminho do arquivo: o arquivo só se baixa pelo link temporário.</summary>
public sealed record DocumentoColaborador(
    int Id,
    TipoDeDocumento Tipo,
    string? Descricao,
    DateOnly? DataValidade,
    SituacaoDoDocumento Situacao,
    string? NomeArquivoOriginal,
    DateTime? PedidoEmUtc,
    DateTime? EnviadoEmUtc);

/// <summary>Lista curta, sem paginação, dos mais recentes para os mais antigos.</summary>
public sealed record DocumentoColaboradorListaResposta(IReadOnlyList<DocumentoColaborador> Itens);

/// <summary>O arquivo que chegou no formulário. A Api embrulha o <c>IFormFile</c> aqui para o Application não depender do ASP.NET: <see cref="Abrir"/> devolve um fluxo NOVO a cada chamada.</summary>
public sealed record ArquivoRecebido(string NomeOriginal, long Tamanho, Func<Stream> Abrir);

/// <summary>O formulário como chegou, em texto cru: o caso de uso valida tudo e devolve todos os erros juntos. <see cref="ArquivosRecebidos"/> conta TODAS as partes de arquivo, de qualquer nome de campo.</summary>
public sealed record NovoDocumentoDoRh(string? Modo, string? Tipo, string? Descricao, string? DataValidade, ArquivoRecebido? Arquivo, int ArquivosRecebidos);
