using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http.Metadata;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Routing;
using RHSystem.Api.Configuracao;

namespace RHSystem.Api.IntegrationTests.Erros;

public class ProduzProblemasTests
{
    [Fact]
    public void ProduzProblemas_declara_cada_status_como_application_problem_json_com_ProblemDetails()
    {
        var app = WebApplication.CreateBuilder().Build();
        app.MapGet("/exemplo", () => "ok").ProduzProblemas(404, 409, 422);

        var endpoint = ((IEndpointRouteBuilder)app).DataSources.SelectMany(d => d.Endpoints).Single();
        var declarados = endpoint.Metadata.GetOrderedMetadata<IProducesResponseTypeMetadata>().Where(m => m.StatusCode != 200).ToList();

        Assert.Equal([404, 409, 422], declarados.Select(m => m.StatusCode).Order());
        Assert.All(declarados, m =>
        {
            Assert.Equal(typeof(ProblemDetails), m.Type);
            Assert.Contains("application/problem+json", m.ContentTypes);
        });
    }
}
