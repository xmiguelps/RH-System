namespace RHSystem.Api.Configuracao;

public static class CorsExtensions
{
    /// <summary>
    /// CORS só para as origens de <c>Cors:OrigensPermitidas</c> (sem nenhuma configurada, nenhuma origem passa). A API expõe
    /// <c>Content-Disposition</c> e <c>Retry-After</c>: o navegador esconde do JavaScript os cabeçalhos que não são "seguros" para CORS, e
    /// sem isso o nome do arquivo baixado e a espera do 429 não chegariam à tela do front. Os tokens vão por cabeçalho, não por cookie.
    /// </summary>
    public static IServiceCollection AdicionarCors(this IServiceCollection servicos, IConfiguration configuracao)
    {
        var origens = (configuracao.GetSection("Cors:OrigensPermitidas").Get<string[]>() ?? [])
            .Where(origem => !string.IsNullOrWhiteSpace(origem))
            .ToArray();

        servicos.AddCors(opcoes => opcoes.AddDefaultPolicy(politica =>
            politica
                .WithOrigins(origens)
                .AllowAnyHeader()
                .AllowAnyMethod()
                .WithExposedHeaders("Content-Disposition", "Retry-After")));

        return servicos;
    }
}
