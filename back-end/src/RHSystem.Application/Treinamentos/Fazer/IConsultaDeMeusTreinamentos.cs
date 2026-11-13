namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>A leitura dos treinamentos da pessoa logada (as telas 41 e 42). A pessoa só vê a própria linha.</summary>
public interface IConsultaDeMeusTreinamentos
{
    /// <summary>
    /// Todos os treinamentos que a pessoa pode ver hoje (ver <see cref="MeuTreinamentoLido"/>), sem ordem. <paramref name="busca"/> procura no título e na descrição (como as outras listas: sem diferenciar caixa nem acento) e <paramref name="obrigatorio"/> deixa só os obrigatórios (<c>true</c>) ou só os opcionais (<c>false</c>).
    /// </summary>
    Task<IReadOnlyList<MeuTreinamentoLido>> ListarAsync(int funcionarioId, DateOnly hoje, string? busca, bool? obrigatorio, CancellationToken ct);

    /// <summary>O instante (UTC) em que a pessoa concluiu o módulo, do progresso DELA; <c>null</c> se ainda não concluiu.</summary>
    Task<DateTime?> InstanteDaConclusaoDoModuloAsync(int funcionarioId, int moduloId, CancellationToken ct);

    /// <summary>Um treinamento que a pessoa pode ver; <c>null</c> se ele não existe ou ela não pode vê-lo (rascunho, não liberado, sem linha e fora do público, encerrado sem ter começado).</summary>
    Task<MeuTreinamentoLido?> ObterAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct);
}
