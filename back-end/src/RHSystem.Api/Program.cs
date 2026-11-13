using RHSystem.Api.Acesso;
using RHSystem.Api.Auditoria;
using RHSystem.Api.Autenticacao;
using RHSystem.Api.Autorizacao;
using RHSystem.Api.Cadastros;
using RHSystem.Api.Colaboradores;
using RHSystem.Api.Configuracao;
using RHSystem.Api.Documentos;
using RHSystem.Api.Empresa;
using RHSystem.Api.Formularios;
using RHSystem.Api.Treinamentos;
using RHSystem.Api.Importacoes;
using RHSystem.Api.Painel;
using RHSystem.Api.Comunicacao;
using RHSystem.Api.Ponto;
using RHSystem.Api.Saude;
using RHSystem.Infrastructure;

var builder = WebApplication.CreateBuilder(args);

builder.Services.AdicionarInfraestrutura(builder.Configuration);
builder.Services.AdicionarCasosDeUsoDeAutenticacao();
builder.Services.AdicionarCasosDeUsoDeAuditoria();
builder.Services.AdicionarCasosDeUsoDeAcesso();
builder.Services.AdicionarCasosDeUsoDeUsuariosDoSistema();
builder.Services.AdicionarCasosDeUsoDeColaboradores();
builder.Services.AdicionarCasosDeUsoDeEquipe();
builder.Services.AdicionarCasosDeUsoDeCargos();
builder.Services.AdicionarCasosDeUsoDeBeneficios();
builder.Services.AdicionarCasosDeUsoDeSetores();
builder.Services.AdicionarCasosDeUsoDeGestores();
builder.Services.AdicionarCasosDeUsoDeDocumentos();
builder.Services.AdicionarCasosDeUsoDeEmpresa();
builder.Services.AdicionarCasosDeUsoDeImportacao();
builder.Services.AdicionarCasosDeUsoDePainel();
builder.Services.AdicionarCasosDeUsoDePontoEu();
builder.Services.AdicionarCasosDeUsoDePedidosProprios();
builder.Services.AdicionarCasosDeUsoDePedidosDaEquipe();
builder.Services.AdicionarCasosDeUsoDePedidosParaRh();
builder.Services.AdicionarCasosDeUsoDeAvisosDoRh();
builder.Services.AdicionarCasosDeUsoDeAvisosEu();
builder.Services.AdicionarCasosDeUsoDeHoleritesDoRh();
builder.Services.AdicionarCasosDeUsoDeHoleritesEu();
builder.Services.AdicionarCasosDeUsoDeAdvertenciasDoRh();
builder.Services.AdicionarCasosDeUsoDeAdvertenciasDoGestor();
builder.Services.AdicionarCasosDeUsoDeAdvertenciasEu();
builder.Services.AdicionarCasosDeUsoDeAniversariantes();
builder.Services.AdicionarCasosDeUsoDosFormulariosDoRh();
builder.Services.AdicionarCasosDeUsoDosFormulariosEu();
builder.Services.AdicionarCasosDeUsoDoAcompanhamentoDosFormularios();
builder.Services.AdicionarCasosDeUsoDosFormulariosDoGestor();
builder.Services.AdicionarCasosDeUsoDosTreinamentosDoRh();
builder.Services.AdicionarCasosDeUsoDoFormularioPublico(builder.Configuration);
builder.Services.AdicionarCasosDeUsoDeAtestados();
builder.Services.AdicionarCasosDeUsoDeFeriados();
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
app.MapColaboradores();
app.MapEquipe();
app.MapCargos();
app.MapBeneficios();
app.MapSetores();
app.MapGestores();
app.MapDocumentosDoRh();
app.MapMeusDocumentos();
app.MapEmpresa();
app.MapImportacoes();
app.MapPontoEu();
app.MapPontoPedidosEu();
app.MapPontoPedidosGestor();
app.MapPontoPedidosRh();
app.MapAtestados();
app.MapFeriados();
app.MapPontoGestor();
app.MapPontoRh();
app.MapAvisosRh();
app.MapAvisosEu();
app.MapHoleritesRh();
app.MapHoleritesEu();
app.MapAdvertenciasRh();
app.MapAdvertenciasGestor();
app.MapAdvertenciasEu();
app.MapAniversariantes();
app.MapFormulariosRh();
app.MapFormulariosEu();
app.MapFormulariosRhAcompanhamento();
app.MapFormulariosDoGestor();
app.MapFormulariosPublico();
app.MapTreinamentosRh();
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
