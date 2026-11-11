using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Importacoes;

namespace RHSystem.Application.Importacoes;

/// <summary>As importações no banco. Quem chama abre a transação.</summary>
public interface IImportacoesRepositorio
{
    /// <summary>Grava a importação; o <c>Id</c> sai preenchido.</summary>
    Task AdicionarAsync(Importacao importacao, CancellationToken ct);

    /// <summary>A importação como está no banco (sem rastreamento); <c>null</c> se não existe.</summary>
    Task<Importacao?> ObterAsync(int id, CancellationToken ct);

    /// <summary>
    /// A REIVINDICAÇÃO: passa a importação de <c>PRONTA</c> para <c>CONFIRMADA</c> numa instrução só, <c>WHERE SITUACAO = 'PRONTA' AND EXPIRA_EM &gt; agora</c>, e já limpa a prévia e o caminho do arquivo. Devolve <c>false</c> se a linha não
    /// mudou (outra confirmação chegou antes, ou venceu): quem chama responde 409. O banco exige os quatro números do resultado junto com a situação <c>CONFIRMADA</c>: a reivindicação grava zero em cada um e <see cref="RegistrarResultadoAsync"/> grava os reais logo depois, na mesma transação.
    /// </summary>
    Task<bool> ReivindicarAsync(int id, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Os números reais do cadastro (convites na fila, incluídos no onboarding e no formulário).</summary>
    Task RegistrarResultadoAsync(int id, int convites, int onboarding, int formulario, CancellationToken ct);

    /// <summary>Id, nome, situação e os nomes do cargo e do setor das pessoas dadas, NA ORDEM dos ids (a lista <c>funcionarios</c> do resultado).</summary>
    Task<IReadOnlyList<FuncionarioResumo>> ResumirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct);

    /// <summary>
    /// As importações que já venceram (<c>EXPIRA_EM &lt;= agora</c>) e ainda guardam a prévia ou o caminho do arquivo, as mais antigas primeiro, até <paramref name="maximo"/>. A confirmada não aparece: a reivindicação já limpou as duas coisas.
    /// </summary>
    Task<IReadOnlyList<ImportacaoVencida>> ListarVencidasAsync(DateTime agoraUtc, int maximo, CancellationToken ct);

    /// <summary>
    /// Apaga a PRÉVIA (o dado pessoal) da importação vencida e, com <paramref name="tambemOCaminho"/>, o caminho do arquivo. Com <c>false</c> o caminho fica: o arquivo não pôde ser apagado e a próxima execução tenta de novo.
    /// </summary>
    Task LimparVencidaAsync(int id, bool tambemOCaminho, CancellationToken ct);
}

/// <summary>Uma importação vencida a limpar: o id e o caminho do arquivo guardado (<c>null</c> se o arquivo já foi apagado).</summary>
public sealed record ImportacaoVencida(int Id, string? CaminhoArquivo);
