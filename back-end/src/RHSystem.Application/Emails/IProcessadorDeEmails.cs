namespace RHSystem.Application.Emails;

/// <summary>
/// Um ciclo do envio: reserva um lote, envia cada mensagem e atualiza a fila. O serviço em segundo plano o chama a cada <c>Email:IntervaloSegundos</c>; os testes o
/// chamam direto.
/// </summary>
public interface IProcessadorDeEmails
{
    /// <summary>Quantas mensagens tratou (reservou) neste ciclo.</summary>
    Task<int> ProcessarCicloAsync(CancellationToken ct);
}
