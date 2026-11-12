namespace RHSystem.Application.Colaboradores;

/// <summary>Os quatro indicadores da ficha do colaborador (<c>obterIndicadoresDoFuncionario</c>).</summary>
public enum IndicadorDaFicha
{
    BancoDeHorasMinutos,
    FeriasDisponiveisDias,
    AdvertenciasQuantidade,
    TreinamentosPendentes,
}

/// <summary>
/// Quem sabe um indicador da ficha (banco de horas: fatia 3; advertências: fatia 4; treinamentos: 6; férias: 7). O valor é o MESMO da rota de origem (a tabela do contrato em
/// <c>obterIndicadoresDoFuncionario</c>). A 1b não registra nenhuma fonte (a leitura da ficha, na Task 7, diz o que cada indicador vale sem fonte).
/// </summary>
public interface IFonteDeIndicadorDaFicha
{
    IndicadorDaFicha Indicador { get; }

    Task<int?> ObterAsync(int funcionarioId, CancellationToken ct);
}

/// <summary>Quem sabe o dia do próximo pagamento (fatia 7). A 1b não registra nenhuma.</summary>
public interface IFonteDoProximoPagamento
{
    Task<DateOnly?> ObterAsync(CancellationToken ct);
}
