using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Holerites;

/// <summary>
/// Os limites do envio de holerite (<c>enviarHolerite</c> e <c>substituirHolerite</c>): só PDF, até 5 MiB (5 × 1024 × 1024 bytes), conferido pelo conteúdo. O teto da requisição tem folga para os campos do formulário: entre 5 e 6 MiB o erro é o
/// 400 em <c>arquivo</c> que o contrato manda, e não o 413 do servidor.
/// </summary>
public static class LimitesDeHolerite
{
    public const long TamanhoMaximoBytes = 5L * 1024 * 1024;

    public const long TetoDaRequisicaoBytes = 6L * 1024 * 1024;

    /// <summary>A pasta dos holerites no armazenamento (e, com a barra, o prefixo da <c>IFonteDeReferenciasDeArquivo</c> deles).</summary>
    public const string PastaNoArmazenamento = "holerites";
}

/// <summary>Um holerite como a lista do RH o mostra (<c>HoleriteItemRh</c>). Nunca leva o caminho do arquivo: o download é por link temporário.</summary>
public sealed record HoleriteItemRh(int Id, int FuncionarioId, TipoDeHolerite Tipo, int Mes, int Ano, int Versao, bool VersaoAtual, SituacaoDoHolerite Situacao, DateTime PublicacaoEmUtc);

/// <summary>O formulário do envio individual, como chegou: os campos de texto sem conferir e o arquivo (com a contagem de TODAS as partes de arquivo, de qualquer nome de campo).</summary>
public sealed record NovoHolerite(string? Mes, string? Ano, string? Tipo, string? PublicarEm, string? NoDiaDoPagamento, ArquivoRecebido? Arquivo, int ArquivosRecebidos);

/// <summary>O formulário da nova versão, como chegou.</summary>
public sealed record NovaVersaoDeHolerite(string? PublicarEm, string? NoDiaDoPagamento, ArquivoRecebido? Arquivo, int ArquivosRecebidos);

/// <summary>O que o envio precisa saber da pessoa: a admissão e o desligamento (a competência se confere contra eles) e o nome (o evento de auditoria).</summary>
public sealed record PessoaDoHolerite(int Id, string Nome, bool Ativo, DateOnly DataAdmissao, DateOnly? DataDesligamento);

/// <summary>Um holerite lido do banco; <c>VersaoAtual</c> é a maior versão da competência e do tipo. O caminho do arquivo só o download usa.</summary>
public sealed record HoleriteLido(int Id, int FuncionarioId, int Mes, int Ano, TipoDeHolerite Tipo, int Versao, DateTime? PublicacaoEmUtc, string? CaminhoArquivo, bool VersaoAtual);

/// <summary>Qual operação grava: o texto do erro da chave repetida muda (<c>HOLERITE_JA_EXISTE</c> no envio, <c>HOLERITE_VERSAO_DESATUALIZADA</c> na nova versão).</summary>
public enum OperacaoDeHolerite
{
    Envio,
    NovaVersao,
}
