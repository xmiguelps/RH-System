namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>As colunas de data dos avisos não têm fração e o MySQL arredondaria: todo instante que a comunicação grava vai cortado ao segundo (o <c>Kind</c> se mantém).</summary>
public static class InstanteAoSegundo
{
    public static DateTime De(DateTime instante) => new(instante.Ticks - (instante.Ticks % TimeSpan.TicksPerSecond), instante.Kind);
}
