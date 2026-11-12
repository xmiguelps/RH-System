using RHSystem.Application.Comum.Tempo;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>A expiração que a tela manda é um <b>dia</b> (<c>expiraEm</c>, o último dia em que o aviso vale): o aviso vale até o fim desse dia em Brasília, <c>23:59:59 -03:00</c> (<c>2026-10-18</c> vira <c>2026-10-19T02:59:59Z</c>).</summary>
public static class ExpiracaoDoAviso
{
    public static DateTime FimDoDia(DateOnly dia) => FusoBrasil.InicioDoDiaEmUtc(dia.AddDays(1)).AddSeconds(-1);
}
