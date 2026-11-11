namespace RHSystem.Domain.Auditoria;

/// <summary>
/// Uma linha de <c>AUDITORIA_MUDANCAS</c>: um campo que mudou num evento (valor antes e depois). Campo sensível guarda <c>ValorOculto = true</c>
/// e antes e depois nulos (o <c>CHECK</c> do banco impede o valor junto da marca).
/// </summary>
public class MudancaDeAuditoria
{
    private MudancaDeAuditoria()
    {
    }

    public long Id { get; private set; }

    public long EventoId { get; private set; }

    public int Ordem { get; private set; }

    public string Campo { get; private set; } = string.Empty;

    public string? ValorAntes { get; private set; }

    public string? ValorDepois { get; private set; }

    public bool ValorOculto { get; private set; }

    public static MudancaDeAuditoria Criar(int ordem, string campo, string? valorAntes, string? valorDepois, bool valorOculto)
    {
        if (ordem < 1)
        {
            throw new ArgumentException("A ordem começa em 1.", nameof(ordem));
        }

        ArgumentException.ThrowIfNullOrWhiteSpace(campo);

        if (valorOculto && (valorAntes is not null || valorDepois is not null))
        {
            throw new ArgumentException("Um campo oculto não guarda valor antes nem depois.", nameof(valorOculto));
        }

        return new MudancaDeAuditoria
        {
            Ordem = ordem,
            Campo = campo,
            ValorAntes = valorAntes,
            ValorDepois = valorDepois,
            ValorOculto = valorOculto,
        };
    }
}
