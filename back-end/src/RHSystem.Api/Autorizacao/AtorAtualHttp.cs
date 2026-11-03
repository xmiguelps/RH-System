using System.Security.Claims;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.Autorizacao;

/// <summary>
/// O <see cref="IAtorAtual"/> lido do <c>HttpContext</c>: autenticado é <see cref="TipoDeAtorAtual.Pessoa"/>; sem login é
/// <see cref="TipoDeAtorAtual.Anonimo"/>; sem <c>HttpContext</c> (serviço em segundo plano) é <see cref="TipoDeAtorAtual.Sistema"/>, canal Sistema e sem IP.
/// Os claims são os que o emissor de tokens grava: <c>sub</c>, <c>fid</c>, <c>nome</c> e <c>perfil</c>.
/// </summary>
internal sealed class AtorAtualHttp(IHttpContextAccessor acessor) : IAtorAtual
{
    private HttpContext? Http => acessor.HttpContext;

    private ClaimsPrincipal? Pessoa => Http?.User is { Identity.IsAuthenticated: true } usuario ? usuario : null;

    public TipoDeAtorAtual Tipo => Http is null ? TipoDeAtorAtual.Sistema : Pessoa is null ? TipoDeAtorAtual.Anonimo : TipoDeAtorAtual.Pessoa;

    public int? UsuarioId => Inteiro("sub");

    public int? FuncionarioId => Inteiro("fid");

    public Perfil? Perfil
    {
        get
        {
            var texto = Pessoa?.FindFirstValue("perfil");

            // Só o nome de um dos três perfis: "1" ou "rh" não viram perfil (o Enum.TryParse aceitaria os dois).
            return texto is not null && Enum.TryParse<Perfil>(texto, ignoreCase: false, out var perfil) && Enum.GetName(perfil) == texto
                ? perfil
                : null;
        }
    }

    public string Nome => Tipo switch
    {
        TipoDeAtorAtual.Pessoa => Pessoa?.FindFirstValue("nome") ?? "Pessoa",
        TipoDeAtorAtual.Sistema => "Sistema",
        _ => "Anônimo",
    };

    public string? Ip => Http?.Connection.RemoteIpAddress?.ToString();

    public CanalDeOrigem Canal => Http is null ? CanalDeOrigem.Sistema : CanalDeOrigem.Portal;

    private int? Inteiro(string claim) =>
        int.TryParse(Pessoa?.FindFirstValue(claim), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var valor) ? valor : null;
}
