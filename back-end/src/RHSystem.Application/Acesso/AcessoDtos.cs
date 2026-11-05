using System.Text.RegularExpressions;
using RHSystem.Application.Comum;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Acesso;

// Tipos de borda das rotas de senha: as propriedades NÃO são anuláveis no contrato, mas chegam nulas quando o corpo não as traz; a validação em tempo de execução dá o 400 com
// `errors` por campo (e não um erro de binding).

/// <summary>Qual texto o front mostra ao abrir o link: <c>CONVITE</c> ("Bem-vindo, defina a sua senha") ou <c>REDEFINICAO</c> ("Defina uma nova senha").</summary>
public enum ConviteLinkTipo
{
    Convite,
    Redefinicao,
}

public sealed record ConviteLinkInfo(ConviteLinkTipo Tipo, string Nome, string EmailCorporativo, DateTime ExpiraEmUtc);

public sealed record ConviteEsqueciSenhaComando(string Email);

public sealed record ConviteLinkValidarComando(string Token);

public sealed record ConviteDefinirSenhaComando(string Token, string NovaSenha);

public sealed record SenhaTrocarComando(string SenhaAtual, string NovaSenha);

/// <summary>As regras de campo das rotas de senha, em um lugar só.</summary>
public static partial class RegrasDeSenha
{
    /// <summary>De 8 a 128 caracteres, sem exigir maiúscula, número ou símbolo (<c>contrato/fontes/20-fatia-1-colaboradores.yaml</c>, <c>ConviteDefinirSenhaComando</c>). O 128 é o limite do hasher.</summary>
    public const int Minimo = 8;

    public const int Maximo = 128;

    public const int TamanhoMaximoDoEmail = 150;

    /// <summary>Campo vazio, nulo ou só de espaços: "O {campo} é obrigatório." (o nome do campo é o do corpo, em camelCase).</summary>
    public static bool Obrigatorio(ColetorDeErros erros, string campo, string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            erros.Adicionar(campo, $"O {campo} é obrigatório.");
            return false;
        }

        return true;
    }

    public static void Validar(ColetorDeErros erros, string campo, string? senha)
    {
        if (Obrigatorio(erros, campo, senha) && (senha!.Length < Minimo || senha.Length > Maximo))
        {
            erros.Adicionar(campo, $"A senha precisa ter de {Minimo} a {Maximo} caracteres.");
        }
    }

    /// <summary>Um formato simples (algo@dominio.ext, sem espaço nem segunda arroba). Não confirma que o endereço existe: quem decide é o envio.</summary>
    public static bool EmailComFormatoValido(string email) =>
        email.Length <= TamanhoMaximoDoEmail && FormatoDoEmail().IsMatch(email);

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+\z")]
    private static partial Regex FormatoDoEmail();
}

public static class ErrosDeAcesso
{
    public const string CodigoDeLinkInvalido = "LINK_INVALIDO_OU_VENCIDO";

    /// <summary>
    /// A MESMA resposta para link inexistente, usado, invalidado, vencido e de login ou colaborador inativo (<c>Erro422LinkDeSenhaInvalido</c>): não revela qual. Texto e código do contrato.
    /// </summary>
    public static ProblemaDeApiException LinkInvalido() =>
        new(422, "Link inválido ou vencido.", "Este link não vale mais. Peça um novo ao RH ou use \"Esqueci minha senha\".", CodigoDeLinkInvalido);

    public static ProblemaDeApiException MuitasTentativas(TimeSpan espera) =>
        new(
            429,
            "Muitas tentativas.",
            "Muitas tentativas. Aguarde um minuto e tente de novo.",
            cabecalhos: new Dictionary<string, string> { ["Retry-After"] = Math.Max(1, (int)Math.Ceiling(espera.TotalSeconds)).ToString(System.Globalization.CultureInfo.InvariantCulture) });

    public static ProblemaDeApiException NaoAutenticado() =>
        new(401, "Não autenticado.", "Entre novamente para continuar.");
}

/// <summary>O link de senha que vale agora, com quem é a pessoa dele.</summary>
public sealed record LinkDeSenhaValido(int TokenId, int UsuarioId, int FuncionarioId, TipoDeTokenDeSenha Tipo, string Nome, string EmailCorporativo, DateTime ExpiraEmUtc, Perfil? Perfil);

/// <summary>Quem sabe os links de senha no banco. Qualquer motivo para não valer (inexistente, usado, invalidado, vencido, login ou colaborador inativo, convite de quem já tem senha) é "não vale".</summary>
public interface ILinksDeSenha
{
    /// <summary>O link em aberto, dentro do prazo (<b>no instante exato do vencimento já não vale</b>), de login ativo e colaborador ativo e, se for convite, de login ainda provisório. Não gasta o link.</summary>
    Task<LinkDeSenhaValido?> ObterValidoAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Consome o link de forma atômica (<c>UPDATE ... WHERE EM_ABERTO ... AND EXPIRA_EM &gt; agora</c>): com dois usos simultâneos, só um recebe <c>true</c>.</summary>
    Task<bool> TentarConsumirAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Invalida os links ainda em aberto da pessoa (de qualquer tipo). Devolve quantos.</summary>
    Task<int> InvalidarOutrosDaPessoaAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct);
}

/// <summary>
/// O limite de erros de SENHA ATUAL de <c>trocarMinhaSenha</c>: <c>Autenticacao:LimiteSenhaAtualPorMinuto</c> (5) erros por pessoa nos últimos 60 s bloqueiam a próxima tentativa, mesmo com a
/// senha certa. Em memória, por instância (a mesma limitação do limitador por IP da fatia 0).
/// </summary>
public interface ILimiteDeSenhaAtual
{
    /// <summary>Nulo = pode tentar; senão, quanto falta para o erro mais antigo da janela sair (vira o <c>Retry-After</c>).</summary>
    TimeSpan? Bloqueio(int usuarioId);

    /// <summary>Anota um erro de senha atual.</summary>
    void RegistrarErro(int usuarioId);
}
