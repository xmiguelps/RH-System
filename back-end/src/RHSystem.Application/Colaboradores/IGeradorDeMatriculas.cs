namespace RHSystem.Application.Colaboradores;

/// <summary>Quem entrega as matrículas de cinco dígitos (<c>00001</c> a <c>99999</c>), uma por colaborador, sem repetir e sem buraco.</summary>
public interface IGeradorDeMatriculas
{
    /// <summary>
    /// Reserva <paramref name="quantidade"/> (1 a 500) matrículas consecutivas, na transação que o chamador já abriu. A linha do contador
    /// fica travada até o <c>COMMIT</c> (cadastros simultâneos esperam): reserve no fim do caso de uso, logo antes de salvar, para a
    /// transação ser curta. Se a transação for desfeita, os números voltam.
    /// </summary>
    /// <exception cref="ArgumentOutOfRangeException">A quantidade está fora de 1 a 500.</exception>
    /// <exception cref="InvalidOperationException">Não há transação aberta, a linha do contador não existe ou o contador passaria de 99999.</exception>
    Task<IReadOnlyList<string>> ReservarAsync(int quantidade, CancellationToken ct);
}
