using RHSystem.Application.Documentos;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>Os limites do arquivo do módulo: 10 MiB (o contrato diz "10 MB"), a requisição com folga de 1 MiB para o texto (o erro de arquivo grande é o 400 do contrato, não o 413) e a pasta no armazenamento (que também é o prefixo da fonte de referências).</summary>
public static class LimitesDoModulo
{
    public const long TamanhoMaximoBytes = 10L * 1024 * 1024;

    public const long TetoDaRequisicaoBytes = TamanhoMaximoBytes + (1024 * 1024);

    public const string Pasta = "treinamentos";
}

/// <summary>O módulo como chegou (<c>multipart/form-data</c>), em texto cru: o caso de uso valida tudo e devolve os erros juntos num 400 só. <see cref="ArquivosRecebidos"/> conta TODAS as partes de arquivo, de qualquer nome de campo.</summary>
public sealed record NovoModulo(string? Titulo, string? Tipo, string? DuracaoMinutos, string? UrlExterna, string? ConteudoTexto, ArquivoRecebido? Arquivo, int ArquivosRecebidos);

/// <summary>O corpo de <c>reordenarModulosDeTreinamento</c> (<c>TreinamentoModulosOrdemComando</c>): TODOS os ids do treinamento, na nova ordem.</summary>
public sealed record TreinamentoModulosOrdemComando(IReadOnlyList<int> ModuloIds);

/// <summary>Os módulos em ordem (<c>TreinamentoModulosRh</c>).</summary>
public sealed record TreinamentoModulosRh(IReadOnlyList<TreinamentoModuloRh> Itens);

/// <summary>O módulo como o banco o guarda, com o caminho do arquivo (que nunca sai da API).</summary>
public sealed record ModuloLido(int Id, int TreinamentoId, int Ordem, string Titulo, TipoDeModulo Tipo, short? DuracaoMinutos, string? UrlExterna, string? CaminhoArquivo, string? ConteudoTexto, string? NomeArquivoOriginal)
{
    public TreinamentoModuloRh ParaOContrato => new(Id, Ordem, Titulo, Tipo, DuracaoMinutos, UrlExterna, ConteudoTexto, NomeArquivoOriginal);
}

/// <summary>O estado do treinamento relido DENTRO da transação, depois de travar a linha dele: é o que as rotas de módulo decidem (só rascunho muda a estrutura).</summary>
public sealed record EstadoDoTreinamentoTravado(bool Publicado, DateTime? EncerradoEmUtc);

/// <summary>A escrita dos módulos. Tudo roda na transação do chamador e começa por <see cref="TravarTreinamentoAsync"/>: publicar espera esta transação e acrescentar módulo espera a outra, então a ordem nunca colide.</summary>
public interface IModulosRepositorio
{
    /// <summary>Trava a linha do treinamento (um <c>UPDATE</c> que não muda valor) e devolve o estado dele; <c>null</c> se não existe.</summary>
    Task<EstadoDoTreinamentoTravado?> TravarTreinamentoAsync(int treinamentoId, CancellationToken ct);

    Task<IReadOnlyList<ModuloLido>> ListarAsync(int treinamentoId, CancellationToken ct);

    /// <summary>O módulo DESTE treinamento, ou <c>null</c> (não existe ou é de outro).</summary>
    Task<ModuloLido?> ObterAsync(int treinamentoId, int moduloId, CancellationToken ct);

    /// <summary>Acrescenta no fim (<c>ordem</c> = a última + 1).</summary>
    Task<ModuloLido> AdicionarAsync(int treinamentoId, CamposDoModulo campos, string? caminhoDoArquivo, string? nomeDoArquivo, CancellationToken ct);

    /// <summary>Reordena em DUAS fases (a ordem é única por treinamento): todos sobem 1000 e depois cada um recebe a posição final.</summary>
    Task ReordenarAsync(int treinamentoId, IReadOnlyList<int> idsNaOrdemNova, CancellationToken ct);

    /// <summary>Substitui o módulo sem mudar a ordem, num só <c>UPDATE</c> (o <c>CHECK</c> do conteúdo confere a linha inteira): o caminho e o nome do arquivo andam juntos.</summary>
    Task SubstituirAsync(int moduloId, CamposDoModulo campos, string? caminhoDoArquivo, string? nomeDoArquivo, CancellationToken ct);

    /// <summary>Apaga o módulo e renumera os que sobram (1, 2, 3...), em duas fases.</summary>
    Task ExcluirAsync(int treinamentoId, int moduloId, CancellationToken ct);
}
