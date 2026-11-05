using RHSystem.Api.Acesso;
using RHSystem.Api.Auditoria;
using RHSystem.Api.Autenticacao;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Api.Painel;
using RHSystem.Api.Saude;
using RHSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarCasosDeUsoDeAutenticacao();
builder.Services.AdicionarCasosDeUsoDeAuditoria();
builder.Services.AdicionarCasosDeUsoDeAcesso();
builder.Services.AdicionarCasosDeUsoDeUsuariosDoSistema();
builder.Services.AdicionarCasosDeUsoDePainel();
builder.Services.AdicionarAutenticacaoJwt(builder.Configuration);
builder.Services.AdicionarAutorizacaoPorPerfil();
builder.Services.AdicionarTratamentoDeErros();
builder.Services.AdicionarProxyReverso(builder.Configuration);
builder.Services.AdicionarCors(builder.Configuration);
builder.Services.AdicionarLimiteDeTentativas(builder.Configuration);
builder.Services.AdicionarSaude();
builder.Services.AdicionarDocumentacao(builder.Configuration);

// O JSON do contrato (CONVENCOES.md, seção 3): enums como texto, números só como número, instante em UTC com Z, data e hora do dia.
builder.Services.ConfigureHttpJsonOptions(opcoes => JsonDoContrato.Configurar(opcoes.SerializerOptions));

var app = builder.Build();

// A ordem importa: o proxy corrige o IP antes de tudo; o limite de tentativas vem antes da autenticação.
app.UsarProxyReverso();
app.UseExceptionHandler();
app.UsarRespostasDeErroPadrao();
app.UseCors();
app.UseRateLimiter();
app.UseAuthentication();
app.UseAuthorization();

app.MapAutenticacao();
app.MapSenha();
app.MapUsuariosDoSistema();
app.MapAuditoria();
app.MapContadoresDoMenu();
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
