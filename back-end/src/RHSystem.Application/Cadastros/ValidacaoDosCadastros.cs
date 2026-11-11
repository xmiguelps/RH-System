using RHSystem.Application.Comum;

namespace RHSystem.Application.Cadastros;

/// <summary>
/// As regras de campo dos cadastros de apoio (cargo, setor, benefício), com a mensagem em português que o front mostra ao lado do campo. O nome do campo é o do JSON do contrato (<c>nome</c>,
/// <c>descricao</c>, <c>cargaSemanalHoras</c>). O domínio recusa o mesmo (com <see cref="ArgumentException"/>) como última barreira; aqui o erro sai com o campo certo, e todos juntos.
/// </summary>
public static class ValidacaoDosCadastros
{
    public const int TamanhoMaximoDoNome = 100;
    public const int TamanhoMaximoDaDescricao = 255;
    public const decimal CargaSemanalMaxima = 168m;

    /// <summary>Nome de 1 a 100 caracteres depois de aparado.</summary>
    public static void Nome(ColetorDeErros erros, string? nome)
    {
        var texto = nome?.Trim();
        if (string.IsNullOrEmpty(texto))
        {
            erros.Adicionar("nome", "O nome é obrigatório.");
        }
        else if (texto.Length > TamanhoMaximoDoNome)
        {
            erros.Adicionar("nome", $"O nome pode ter até {TamanhoMaximoDoNome} caracteres.");
        }
    }

    /// <summary>Descrição opcional, de até 255 caracteres depois de aparada (vazia vale como ausente).</summary>
    public static void Descricao(ColetorDeErros erros, string? descricao)
    {
        if (descricao?.Trim() is { Length: > TamanhoMaximoDaDescricao })
        {
            erros.Adicionar("descricao", $"A descrição pode ter até {TamanhoMaximoDaDescricao} caracteres.");
        }
    }

    /// <summary>Carga semanal maior que zero, de no máximo 168 horas, com até duas casas decimais.</summary>
    public static void CargaSemanal(ColetorDeErros erros, decimal carga)
    {
        if (carga <= 0 || carga > CargaSemanalMaxima || decimal.Round(carga, 2) != carga)
        {
            erros.Adicionar("cargaSemanalHoras", "A carga semanal é maior que zero, de no máximo 168 horas, com até duas casas decimais.");
        }
    }
}
