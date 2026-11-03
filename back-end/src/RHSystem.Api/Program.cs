using System.Text.Json.Serialization;
using RHSystem.Api.Autenticacao;
using RHSystem.Api.Configuracao;
using RHSystem.Api.Saude;
using RHSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarCasosDeUsoDeAutenticacao();
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration);
builder.Services.AdicionarTratamentoDeErros();
builder.Services.AdicionarProxyReverso(builder.Configuration);
builder.Services.AdicionarCors(builder.Configuration);
builder.Services.AdicionarLimiteDeTentativas(builder.Configuration);
builder.Services.AdicionarSaude();
builder.Services.AdicionarDocumentacao(builder.Configuration);

// Enums saem como texto ("RH", "Gestor"), não como número; números só como número (o contrato diz integer).
builder.Services.ConfigureHttpJsonOptions(opcoes =>
{
    opcoes.SerializerOptions.Converters.Add(new JsonStringEnumConverter());
    opcoes.SerializerOptions.NumberHandling = JsonNumberHandling.Strict;
});

var app = builder.Build();

// A ordem importa: o proxy corrige o IP antes de tudo; o limite de tentativas vem antes da autenticação.
app.UsarProxyReverso();
app.UseExceptionHandler();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAutenticacao();
app.MapSaude();
app.UsarDocumentacao();
if (builder.Configuration.GetValue<bool>("Testes:RotasDeTeste"))
{
    app.MapRotasDeTeste();
}

await app.InicializarBancoAsync();

app.Run();

/// <summary>Torna o ponto de entrada visível para o WebApplicationFactory dos testes.</summary>
public partial class Program;
