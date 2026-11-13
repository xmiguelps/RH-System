namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>A pasta do armazenamento onde ficam os PDFs dos certificados (também o prefixo da fonte de referências da limpeza de sobras).</summary>
public static class LimitesDoCertificado
{
    public const string Pasta = "certificados";
}

/// <summary>
/// O certificado de uma pessoa como o banco o guarda, junto do que o PDF precisa. Só existe (não é <c>null</c>) quando o treinamento EMITE certificado, a pessoa o concluiu (linha <c>CONCLUIDO</c>) e a linha do certificado existe: o trigger do banco cria a linha de certificado até em treinamento que não emite, e a API a trata como inexistente.
/// <c>CargaHorariaMinutos</c> é a soma das durações dos módulos que têm duração (<c>null</c> se nenhum tem).
/// </summary>
public sealed record CertificadoParaEmitir(int Id, int TreinamentoId, int FuncionarioId, string NomeDaPessoa, string TituloDoTreinamento, int? CargaHorariaMinutos, DateOnly? EmitidoEm, string? CodigoValidacao, string? CaminhoArquivo);

/// <summary>O que a conferência pública mostra de um certificado válido.</summary>
public sealed record CertificadoConferido(string Codigo, string TitularNome, string TreinamentoTitulo, DateOnly EmitidoEm);

/// <summary>A leitura e a gravação do caminho do PDF do certificado.</summary>
public interface ICertificadosRepositorio
{
    /// <summary><c>null</c> se o treinamento não emite certificado, a pessoa não concluiu ou não há linha de certificado.</summary>
    Task<CertificadoParaEmitir?> LerAsync(int treinamentoId, int funcionarioId, CancellationToken ct);

    /// <summary>
    /// A conferência pública: o certificado com ESTE código (versão 4 em minúsculas), só se o treinamento EMITE certificado e a pessoa o concluiu (o mesmo critério de <see cref="LerAsync"/>); <c>null</c> em qualquer outro caso, sem distinguir o motivo. O nome de quem saiu da empresa continua valendo.
    /// </summary>
    Task<CertificadoConferido?> ConferirPorCodigoAsync(string codigo, CancellationToken ct);

    /// <summary>Grava o caminho do PDF na linha do certificado por <c>UPDATE</c> condicional a "ainda sem arquivo": <c>false</c> se outra chamada gravou antes (o chamador apaga o próprio arquivo).</summary>
    Task<bool> GravarCaminhoAsync(int treinamentoId, int funcionarioId, string caminho, CancellationToken ct);
}

/// <summary>O que o PDF do certificado mostra: a pessoa, o título (do dia da emissão), a carga horária (se há duração), o número, a data, o código de validação, a empresa (se configurada) e o endereço da conferência (só se a página do front está configurada).</summary>
public sealed record CertificadoParaPdf(string NomeDaPessoa, string TituloDoTreinamento, int? CargaHorariaMinutos, string Numero, DateOnly EmitidoEm, string Codigo, string? NomeDaEmpresa, string? EnderecoDaConferencia);

/// <summary>Monta o PDF do certificado (uma página, paisagem).</summary>
public interface IGeradorDePdfDoCertificado
{
    byte[] Gerar(CertificadoParaPdf entrada);
}

/// <summary>O endereço da página de conferência do certificado, ou <c>null</c> quando o endereço do front não está configurado.</summary>
public interface IEnderecoDaConferencia
{
    string? Endereco { get; }
}
