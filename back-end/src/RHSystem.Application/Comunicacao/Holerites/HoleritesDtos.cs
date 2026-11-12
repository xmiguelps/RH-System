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

/// <summary>A página de holerites de uma pessoa, como o RH a vê (<c>HoleriteListaRh</c>): <c>UltimoPublicado</c> é o já publicado mais recentemente, sem filtro nem página.</summary>
public sealed record HoleriteListaRh(IReadOnlyList<HoleriteItemRh> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, HoleriteItemRh? UltimoPublicado);

/// <summary>Um holerite na lista da própria pessoa (<c>HoleriteItemEu</c>): só publicados, só a versão atual. Sem versão, situação nem caminho.</summary>
public sealed record HoleriteItemEu(int Id, TipoDeHolerite Tipo, int Mes, int Ano, DateTime PublicadoEmUtc);

/// <summary>As abas da lista da pessoa (<c>contagens</c>), do ano pedido e sem olhar o filtro de tipo.</summary>
public sealed record HoleriteContagensEu(int Todos, int Mensais, int Ferias, int DecimoTerceiro);

/// <summary>A página de holerites da própria pessoa (<c>HoleriteListaEu</c>): o <c>Ano</c> usado, os anos do seletor (do mais recente para o mais antigo) e as contagens das abas.</summary>
public sealed record HoleriteListaEu(IReadOnlyList<HoleriteItemEu> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas, int Ano, IReadOnlyList<int> AnosDisponiveis, HoleriteContagensEu Contagens);

/// <summary>Os filtros da lista do RH: o ano e o tipo da competência, e se a lista traz também as versões substituídas.</summary>
public sealed record FiltroDeHoleritesRh(int? Ano, TipoDeHolerite? Tipo, bool TodasAsVersoes);

/// <summary>Uma linha da lista do RH como veio do banco; a situação (publicado ou agendado) se decide pelo relógio, no caso de uso.</summary>
public sealed record HoleriteDaListaDoRh(int Id, int FuncionarioId, TipoDeHolerite Tipo, int Mes, int Ano, int Versao, bool VersaoAtual, DateTime? PublicacaoEmUtc);

/// <summary>O que o download precisa: o caminho do arquivo (que nunca sai da API), o nome da pessoa e a competência e a versão (o evento de auditoria e o nome do arquivo).</summary>
public sealed record HoleriteParaDownload(int Id, string FuncionarioNome, int Mes, int Ano, TipoDeHolerite Tipo, int Versao, string CaminhoArquivo);

/// <summary>O que a lista da pessoa leu do banco: a página, o total, os anos com holerite publicado e os tipos dos visíveis do ano (as contagens saem daí).</summary>
public sealed record ListaDeHoleritesDaPessoa(IReadOnlyList<HoleriteItemEu> Itens, int Total, IReadOnlyList<int> AnosDisponiveis, IReadOnlyList<TipoDeHolerite> TiposDoAno);

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

/// <summary>Os limites do envio em lote (<c>enviarHoleritesEmLote</c>): até 200 PDFs e 100 MiB no total (cada PDF segue o limite de 5 MiB). O teto da requisição tem folga para os campos e o JSON dos itens.</summary>
public static class LimitesDoLoteDeHolerites
{
    public const int MaximoDeItens = 200;

    public const long TamanhoTotalMaximoBytes = 100L * 1024 * 1024;

    public const long TetoDaRequisicaoBytes = 110L * 1024 * 1024;
}

/// <summary>Uma linha da lista <c>itens</c> do lote: o PDF (<c>nomeDoArquivo</c>, o nome do arquivo enviado em <c>arquivos</c>) e a pessoa dona do holerite. Quem liga cada arquivo a uma pessoa é o RH: a API não adivinha pelo conteúdo nem pelo nome.</summary>
public sealed record HoleriteLoteItemComando(int FuncionarioId, string NomeDoArquivo);

/// <summary>O formulário do lote, como chegou: os campos de texto, <c>itens</c> como o JSON que veio, os arquivos do campo <c>arquivos</c> e a contagem de TODAS as partes de arquivo de qualquer campo.</summary>
public sealed record NovoLoteDeHoleritesBruto(string? Mes, string? Ano, string? Tipo, string? PublicarEm, string? NoDiaDoPagamento, string? Itens, IReadOnlyList<ArquivoRecebido> Arquivos, int ArquivosRecebidos);

/// <summary>O resultado de um item (<c>HoleriteLoteItemResultado</c>): o holerite criado, ou o código e o texto do erro dele.</summary>
public sealed record HoleriteLoteItemResultado(int FuncionarioId, string NomeDoArquivo, bool Sucesso, HoleriteItemRh? Holerite, string? CodigoDoErro, string? MensagemDoErro);

/// <summary>O resultado do lote (<c>HoleriteLoteResultado</c>): <c>Enviados</c> são os publicados mais os agendados.</summary>
public sealed record HoleriteLoteResultado(int Total, int Enviados, int ComErro, IReadOnlyList<HoleriteLoteItemResultado> Itens);
