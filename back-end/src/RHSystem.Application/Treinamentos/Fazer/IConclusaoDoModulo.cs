namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>O andamento da pessoa num treinamento: quantos módulos ele tem e quais ela já concluiu.</summary>
public sealed record ProgressoParaConcluir(int TotalDeModulos, IReadOnlyList<int> ModulosConcluidos);

/// <summary>
/// A escrita da conclusão de módulo e do treinamento. Tudo roda na transação do chamador, que já TRAVOU a linha do treinamento. A aplicação nunca insere em <c>TREINAMENTOS_CERTIFICADOS</c>: o trigger do banco cria a linha (com um UUID versão 1 e a data do relógio REAL) quando a linha da pessoa vira <c>CONCLUIDO</c>, e a aplicação a corrige na mesma transação.
/// </summary>
public interface IConclusaoDoModulo
{
    Task<ProgressoParaConcluir> LerProgressoAsync(int funcionarioId, int treinamentoId, CancellationToken ct);

    /// <summary>Grava o módulo como concluído pela pessoa, no instante dado (UTC, cortado ao segundo).</summary>
    Task RegistrarProgressoAsync(int moduloId, int funcionarioId, DateTime instanteUtc, CancellationToken ct);

    /// <summary>Passa a linha da pessoa a <c>CONCLUIDO</c> com <c>DATA_CONCLUSAO</c> = <paramref name="hoje"/> por <c>UPDATE</c> condicional (só de <c>INSCRITO</c> ou <c>EM_ANDAMENTO</c>); <c>false</c> se a linha já não estava num desses estados.</summary>
    Task<bool> ConcluirTreinamentoAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct);

    /// <summary>Troca, na linha de certificado que o trigger acabou de criar, o código (UUID v1) por um UUID versão 4 e a data de emissão (a do relógio real do banco) pela do relógio da aplicação. <c>false</c> se a linha não existe.</summary>
    Task<bool> TrocarCodigoDoCertificadoAsync(int funcionarioId, int treinamentoId, string codigoVersao4, DateOnly emitidoEm, CancellationToken ct);
}

/// <summary>
/// Gera o PDF do certificado e guarda o caminho na linha dele. Roda DEPOIS do commit da conclusão e NUNCA lança: se o armazenamento ou o gerador falhar, a conclusão continua valendo e o primeiro download gera o arquivo na hora.
/// </summary>
public interface IEmissaoDoCertificado
{
    Task EmitirDepoisDoCommitAsync(int funcionarioId, int treinamentoId, CancellationToken ct);
}
