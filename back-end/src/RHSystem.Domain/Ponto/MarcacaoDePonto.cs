namespace RHSystem.Domain.Ponto;

/// <summary>
/// Uma batida (<c>PONTOS_MARCACOES</c>). O instante é UTC dentro da aplicação e vai CORTADO AO SEGUNDO (a coluna não tem fração e o MySQL arredondaria: dois cliques a 0,4 s e a 0,6 s cairiam em segundos diferentes). <c>CORRIGIDA_RH</c>
/// anda junto com a data da correção (o banco exige as duas coisas juntas).
/// </summary>
public class MarcacaoDePonto
{
    private MarcacaoDePonto()
    {
    }

    public int Id { get; private set; }

    public int PontoId { get; private set; }

    public DateTime InstanteUtc { get; private set; }

    public TipoDeMarcacao Tipo { get; private set; }

    public OrigemDaMarcacao Origem { get; private set; }

    public DateTime? CorrigidaEmUtc { get; private set; }

    public static MarcacaoDePonto Nova(int pontoId, DateTime instanteUtc, TipoDeMarcacao tipo) =>
        new() { PontoId = pontoId, InstanteUtc = AoSegundo(instanteUtc), Tipo = tipo, Origem = OrigemDaMarcacao.Sistema };

    /// <summary>Uma batida que o RH incluiu (direto ou por um pedido aprovado).</summary>
    public static MarcacaoDePonto NovaCorrigidaPeloRh(int pontoId, DateTime instanteUtc, TipoDeMarcacao tipo, DateTime agoraUtc) =>
        new() { PontoId = pontoId, InstanteUtc = AoSegundo(instanteUtc), Tipo = tipo, Origem = OrigemDaMarcacao.CorrigidaRh, CorrigidaEmUtc = AoSegundo(agoraUtc) };

    /// <summary>O RH mudou o horário e/ou o tipo desta batida: passa a <c>CORRIGIDA_RH</c> com a data da correção.</summary>
    public void CorrigirPeloRh(DateTime instanteUtc, TipoDeMarcacao tipo, DateTime agoraUtc)
    {
        InstanteUtc = AoSegundo(instanteUtc);
        Tipo = tipo;
        Origem = OrigemDaMarcacao.CorrigidaRh;
        CorrigidaEmUtc = AoSegundo(agoraUtc);
    }

    public static DateTime AoSegundo(DateTime instanteUtc)
    {
        if (instanteUtc.Kind != DateTimeKind.Utc)
        {
            throw new ArgumentException("O instante da batida precisa ter Kind = Utc.", nameof(instanteUtc));
        }

        return new DateTime(instanteUtc.Ticks - (instanteUtc.Ticks % TimeSpan.TicksPerSecond), DateTimeKind.Utc);
    }
}
