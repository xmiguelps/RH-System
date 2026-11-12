using RHSystem.Application.Comum;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>O filtro <c>ano</c> das listas da comunicação (holerites e advertências): de 2000 a 2100, como o contrato. Sem ele (<c>null</c>) não há o que conferir.</summary>
public static class AnoDaLista
{
    public const int Primeiro = 2000;
    public const int Ultimo = 2100;

    /// <exception cref="ErroDeValidacaoException">Com o erro em <c>ano</c>.</exception>
    public static void Conferir(int? ano)
    {
        if (ano is { } valor && (valor < Primeiro || valor > Ultimo))
        {
            var erros = new ColetorDeErros();
            erros.Adicionar("ano", $"Informe um ano de {Primeiro} a {Ultimo}.");
            erros.LancarSeHouver();
        }
    }
}
