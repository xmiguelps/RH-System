using System.Text;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using RHSystem.Application.Autenticacao;
using RHSystem.Infrastructure.Autenticacao;

namespace RHSystem.Api.Configuracao;

public static class AutenticacaoExtensions
{
    /// <summary>Registra os casos de uso de autenticação (a camada Application não conhece o contêiner de dependências).</summary>
    public static IServiceCollection AdicionarCasosDeUsoDeAutenticacao(this IServiceCollection servicos)
    {
        servicos.AddScoped<EntrarCasoDeUso>();
        servicos.AddScoped<RenovarTokenCasoDeUso>();
        servicos.AddScoped<SairCasoDeUso>();
        servicos.AddScoped<ObterUsuarioLogadoCasoDeUso>();
        return servicos;
    }

    /// <summary>
    /// JwtBearer lendo <c>Jwt:*</c>. Tokens por cabeçalho, não cookie. A validade do token é medida pelo <see cref="IRelogio"/> da aplicação
    /// (e não pelo relógio do sistema), com 30 segundos de folga: assim os testes vencem um token avançando o relógio falso.
    /// </summary>
    public static IServiceCollection AdicionarAutenticacaoJwt(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var jwt = new OpcoesJwt();
        configuracao.GetSection("Jwt").Bind(jwt); // a AdicionarInfraestrutura já recusou chave curta e emissor ou audiência vazios

        servicos
            .AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(opcoes =>
            {
                opcoes.MapInboundClaims = false; // "sub" continua "sub"
                opcoes.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwt.Emissor,
                    ValidateAudience = true,
                    ValidAudience = jwt.Audiencia,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Chave)),
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromSeconds(30),
                    NameClaimType = "nome",
                    RoleClaimType = "perfil",
                };
                opcoes.Events = new JwtBearerEvents
                {
                    // Sozinho o JwtBearer devolve 401 sem corpo: aqui sai o ProblemDetails do contrato.
                    OnChallenge = async contexto =>
                    {
                        contexto.HandleResponse();
                        await Problemas.NaoAutenticadoAsync(contexto.HttpContext);
                    },
                };
            });

        servicos.AddOptions<JwtBearerOptions>(JwtBearerDefaults.AuthenticationScheme)
            .Configure<IRelogio>((opcoes, relogio) =>
            {
                opcoes.TokenValidationParameters.LifetimeValidator = (naoAntesDe, expiraEm, _, parametros) =>
                {
                    var agora = relogio.AgoraUtc;
                    return (naoAntesDe is null || agora >= naoAntesDe.Value - parametros.ClockSkew)
                        && (expiraEm is not null && agora <= expiraEm.Value + parametros.ClockSkew);
                };
            });

        servicos.AddAuthorization();
        return servicos;
    }
}
