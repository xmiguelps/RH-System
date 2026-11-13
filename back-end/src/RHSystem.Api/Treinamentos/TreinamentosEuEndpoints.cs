using RHSystem.Api.Autorizacao;
using RHSystem.Api.Configuracao;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Treinamentos.Fazer;

namespace RHSystem.Api.Treinamentos;

/// <summary>Os parâmetros de <c>GET /api/eu/treinamentos</c>; <c>Aba</c> chega como texto (o caso de uso o lê e devolve todos os erros juntos num 400 só); <c>Obrigatorio</c> é <c>true</c> (só obrigatórios), <c>false</c> (só opcionais) ou ausente (os dois).</summary>
public sealed record FiltrosDaListaDeMeusTreinamentos(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? Aba = null, bool? Obrigatorio = null);

/// <summary>
/// Os treinamentos de quem está logado (<c>/api/eu/treinamentos</c>, qualquer perfil: gestor e RH também fazem os treinamentos deles). Os <c>operationId</c> e os códigos de resposta são os de <c>contrato/fontes/60-fatia-6-tad.yaml</c>. Nenhuma rota desta família tem 403; a pessoa só mexe na PRÓPRIA linha. Esta parte: a lista e o detalhe; as outras rotas entram nas tarefas seguintes da fatia.
/// </summary>
public static class TreinamentosEuEndpoints
{
    private const string Etiqueta = "Treinamentos";

    public static IServiceCollection AdicionarCasosDeUsoDosTreinamentosEu(this IServiceCollection servicos)
    {
        servicos.AddScoped<ListarMeusTreinamentosCasoDeUso>();
        servicos.AddScoped<ObterMeuTreinamentoCasoDeUso>();
        servicos.AddScoped<InscreverMeEmTreinamentoCasoDeUso>();
        servicos.AddScoped<IniciarMeuTreinamentoCasoDeUso>();
        servicos.AddScoped<ObterConteudoDeModuloCasoDeUso>();
        servicos.AddScoped<BaixarArquivoDeModuloCasoDeUso>();
        servicos.AddScoped<ConcluirModuloDeTreinamentoCasoDeUso>();
        servicos.AddScoped<BaixarMeuCertificadoCasoDeUso>();
        return servicos;
    }

    public static IEndpointRouteBuilder MapTreinamentosEu(this IEndpointRouteBuilder rotas)
    {
        var eu = rotas.GrupoEu().WithTags(Etiqueta);

        eu.MapGet("/treinamentos", Listar)
            .WithName("listarMeusTreinamentos")
            .Produces<TreinamentoMeuLista>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status400BadRequest, StatusCodes.Status401Unauthorized, StatusCodes.Status500InternalServerError);

        eu.MapGet("/treinamentos/{treinamentoId:int}", Obter)
            .WithName("obterMeuTreinamento")
            .Produces<TreinamentoMeuDetalhe>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        eu.MapPost("/treinamentos/{treinamentoId:int}/inscricao", Inscrever)
            .WithName("inscreverMeEmTreinamento")
            .Produces<TreinamentoMeuDetalhe>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapPost("/treinamentos/{treinamentoId:int}/iniciar", Iniciar)
            .WithName("iniciarMeuTreinamento")
            .Produces<TreinamentoMeuDetalhe>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapGet("/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}", ObterConteudo)
            .WithName("obterConteudoDeModulo")
            .Produces<TreinamentoModuloConteudo>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapGet("/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}/download", BaixarArquivo)
            .WithName("baixarArquivoDeModulo")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapPost("/treinamentos/{treinamentoId:int}/modulos/{moduloId:int}/concluir", ConcluirModulo)
            .WithName("concluirModuloDeTreinamento")
            .Produces<TreinamentoModuloConclusaoResposta>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status422UnprocessableEntity, StatusCodes.Status500InternalServerError);

        eu.MapGet("/treinamentos/{treinamentoId:int}/certificado/download", BaixarCertificado)
            .WithName("baixarMeuCertificado")
            .WithTags("Certificados")
            .Produces<LinkTemporario>(StatusCodes.Status200OK)
            .ProduzProblemas(StatusCodes.Status401Unauthorized, StatusCodes.Status404NotFound, StatusCodes.Status500InternalServerError);

        return rotas;
    }

    private static async Task<TreinamentoMeuLista> Listar([AsParameters] FiltrosDaListaDeMeusTreinamentos f, ListarMeusTreinamentosCasoDeUso casoDeUso, CancellationToken ct) =>
        await casoDeUso.ExecutarAsync(f.Pagina, f.TamanhoPagina, f.Busca, f.Aba, f.Obrigatorio, ct);

    private static async Task<TreinamentoMeuDetalhe> Inscrever(int treinamentoId, InscreverMeEmTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoMeuDetalhe> Iniciar(int treinamentoId, IniciarMeuTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoModuloConteudo> ObterConteudo(int treinamentoId, int moduloId, ObterConteudoDeModuloCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, moduloId, ct);

    private static async Task<LinkTemporario> BaixarArquivo(int treinamentoId, int moduloId, BaixarArquivoDeModuloCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, moduloId, ct);

    private static async Task<TreinamentoModuloConclusaoResposta> ConcluirModulo(int treinamentoId, int moduloId, ConcluirModuloDeTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, moduloId, ct);

    private static async Task<LinkTemporario> BaixarCertificado(int treinamentoId, BaixarMeuCertificadoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);

    private static async Task<TreinamentoMeuDetalhe> Obter(int treinamentoId, ObterMeuTreinamentoCasoDeUso casoDeUso, CancellationToken ct) => await casoDeUso.ExecutarAsync(treinamentoId, ct);
}
