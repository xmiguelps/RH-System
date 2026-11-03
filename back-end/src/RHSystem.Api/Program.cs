using System.Text.Json.Serialization;
using RHSystem.Api.Autenticacao;
using RHSystem.Api.Configuracao;
using RHSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarCasosDeUsoDeAutenticacao();
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration);
builder.Services.AdicionarTratamentoDeErros();
builder.Services.AdicionarDocumentacao(builder.Configuration);

// Enums saem como texto ("RH", "Gestor"), não como número.
builder.Services.ConfigureHttpJsonOptions(opcoes => opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter()));

var app = builder.Build();

app.UseExceptionHandler();
app.UseAuthentication();
app.UseAuthorization();

app.MapAutenticacao();
app.UsarDocumentacao();
if (builder.Configuration.GetValue<bool>("Testes:RotasDeTeste"))
{
    app.MapRotasDeTeste();
}

app.Run();

/// <summary>Torna o ponto de entrada visível para o WebApplicationFactory dos testes.</summary>
public partial class Program;
