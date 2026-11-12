using RHSystem.Application.Comum;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>Os campos do aviso depois de conferidos: título e conteúdo aparados.</summary>
public sealed record CamposDoAviso(string Titulo, string Conteudo);

/// <summary>
/// A conferência dos campos do aviso (criar e editar), <b>todos os erros juntos</b> (o 400 traz um por campo): título de 1 a 200 caracteres, conteúdo de 1 a 10000 (o simples vai só até 280) e a expiração não antes do dia da publicação
/// (o mesmo dia vale: é o último dia). As contas são por caractere, como o banco. O banco ainda tem o <c>CHK_AVISOS_DATAS</c> e o trigger como rede de baixo; o 400 vem daqui, com o nome do campo.
/// </summary>
public static class ValidacaoDoAviso
{
    public const int TituloMaximo = 200;
    public const int SimplesMaximo = 280;
    public const int DetalhadoMaximo = 10000;

    public static CamposDoAviso? Validar(ColetorDeErros erros, TipoDeAviso tipo, string? titulo, string? conteudo, DateOnly? expiraEm, DateOnly diaDaPublicacao)
    {
        var valido = true;
        var tituloAparado = titulo?.Trim() ?? string.Empty;
        if (tituloAparado.Length == 0 || ContagemDeCaracteres.De(tituloAparado) > TituloMaximo)
        {
            erros.Adicionar("titulo", $"Informe o título do aviso, com até {TituloMaximo} caracteres.");
            valido = false;
        }

        var conteudoAparado = conteudo?.Trim() ?? string.Empty;
        if (conteudoAparado.Length == 0)
        {
            erros.Adicionar("conteudo", "Informe o conteúdo do aviso.");
            valido = false;
        }
        else if (tipo == TipoDeAviso.Simples && ContagemDeCaracteres.De(conteudoAparado) > SimplesMaximo)
        {
            erros.Adicionar("conteudo", $"O aviso simples tem no máximo {SimplesMaximo} caracteres.");
            valido = false;
        }
        else if (tipo != TipoDeAviso.Simples && ContagemDeCaracteres.De(conteudoAparado) > DetalhadoMaximo)
        {
            erros.Adicionar("conteudo", $"O conteúdo do aviso tem no máximo {DetalhadoMaximo} caracteres.");
            valido = false;
        }

        if (expiraEm is { } expiracao && expiracao < diaDaPublicacao)
        {
            erros.Adicionar("expiraEm", "A data de expiração não pode ser anterior ao dia da publicação.");
            valido = false;
        }

        return valido ? new CamposDoAviso(tituloAparado, conteudoAparado) : null;
    }
}
