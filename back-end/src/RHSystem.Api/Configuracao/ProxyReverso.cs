using Microsoft.AspNetCore.HttpOverrides;

namespace RHSystem.Api.Configuracao;

public static class ProxyReversoExtensions
{
    private const string Habilitado = "ProxyReverso:Habilitado";

    /// <summary>
    /// Atrás de um proxy (o front de borda do App Service), o IP de origem vem em <c>X-Forwarded-For</c>. Só se aceita o cabeçalho de quem é
    /// confiável: por padrão o 127.0.0.1; em produção, <c>ProxyReverso:ProxiesConfiaveis</c> lista os IPs ou redes (CIDR) da plataforma.
    /// Desabilitado (o padrão), o cabeçalho é ignorado: quem o manda não escolhe o próprio IP, e o limite de tentativas não se contorna.
    /// </summary>
    public static IServiceCollection AdicionarProxyReverso(this IServiceCollection servicos, IConfiguration configuracao)
    {
        if (!configuracao.GetValue<bool>(Habilitado))
        {
            return servicos;
        }

        servicos.Configure<ForwardedHeadersOptions>(opcoes =>
        {
            opcoes.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
            opcoes.ForwardLimit = 1;

            var confiaveis = configuracao.GetSection("ProxyReverso:ProxiesConfiaveis").Get<string[]>()?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            if (confiaveis is { Length: > 0 })
            {
                opcoes.KnownProxies.Clear();
                opcoes.KnownIPNetworks.Clear();
                foreach (var confiavel in confiaveis)
                {
                    if (confiavel.Contains('/', StringComparison.Ordinal))
                    {
                        opcoes.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(confiavel));
                    }
                    else
                    {
                        opcoes.KnownProxies.Add(System.Net.IPAddress.Parse(confiavel));
                    }
                }
            }
        });

        return servicos;
    }

    /// <summary>Tem de ser o primeiro da fila: tudo depois (limite de tentativas, logs) enxerga o IP já corrigido.</summary>
    public static WebApplication UsarProxyReverso(this WebApplication app)
    {
        if (app.Configuration.GetValue<bool>(Habilitado))
        {
            var confiaveis = app.Configuration.GetSection("ProxyReverso:ProxiesConfiaveis").Get<string[]>()?.Where(p => !string.IsNullOrWhiteSpace(p)).ToArray();
            if (confiaveis is null or { Length: 0 })
            {
                app.Logger.LogWarning(
                    "ProxyReverso:Habilitado está ligado sem ProxyReverso:ProxiesConfiaveis: só o endereço 127.0.0.1 é aceito como proxy. Atrás de um proxy de verdade (o front de borda do Azure), o X-Forwarded-For seria ignorado e todas as pessoas dividiriam o mesmo limite de tentativas de login. Informe os IPs ou redes (CIDR) do proxy.");
            }

            app.UseForwardedHeaders();
        }

        return app;
    }
}
