using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Holerites;

/// <summary>
/// <c>GET /api/rh/funcionarios/{funcionarioId}/holerites</c> (só o RH): os holerites de uma pessoa, também de quem está inativo. Sem <c>todasAsVersoes</c> só a versão atual de cada competência e tipo (os agendados entram); com ela, o histórico. A ordem é ano, mês, versão
/// (mais nova primeiro) e o mais recente cadastrado. <c>ultimoPublicado</c> é o já publicado mais recentemente, sem olhar filtro nem página. 400 antes de 404 (a conferência dos parâmetros não depende do banco).
/// </summary>
public sealed class ListarHoleritesDoFuncionarioCasoDeUso(IConsultaDeHolerites consulta, IRelogio relogio)
{
    public async Task<HoleriteListaRh> ExecutarAsync(int funcionarioId, int pagina, int tamanhoPagina, int? ano, string? tipo, bool todasAsVersoes, CancellationToken ct)
    {
        var tipoLido = EnumsDeConsulta.Ler<TipoDeHolerite>(tipo, "tipo");
        AnoDaLista.Conferir(ano);
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        _ = (await consulta.ObterPessoaAsync(funcionarioId, ct)).OuNaoEncontrado();

        var agora = relogio.AgoraUtc;
        var (itens, total, ultimo) = await consulta.ListarDaPessoaAsync(funcionarioId, new FiltroDeHoleritesRh(ano, tipoLido, todasAsVersoes), parametros, agora, ct);
        var resposta = RespostaPaginada<HoleriteItemRh>.De([.. itens.Select(l => RegrasDasListasDeHolerite.ItemDoRh(l, agora))], total, parametros);
        return new HoleriteListaRh(resposta.Itens, resposta.Pagina, resposta.TamanhoPagina, resposta.Total, resposta.TotalPaginas, ultimo is null ? null : RegrasDasListasDeHolerite.ItemDoRh(ultimo, agora));
    }
}

/// <summary>
/// <c>GET /api/eu/holerites</c> (qualquer perfil): só os holerites DA PESSOA LOGADA, já PUBLICADOS e só a maior versão publicada de cada competência e tipo (um agendado e uma versão substituída não aparecem; com a versão 2 agendada, a 1 continua à vista). O <c>ano</c> é o pedido, ou o
/// ano atual no horário do Brasil; <c>contagens</c> são desse ano e não dependem de <c>tipo</c>; <c>anosDisponiveis</c> são os anos com algum holerite publicado.
/// </summary>
public sealed class ListarMeusHoleritesCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeHoleritesDaPessoa consulta, IRelogio relogio)
{
    public async Task<HoleriteListaEu> ExecutarAsync(int pagina, int tamanhoPagina, int? ano, string? tipo, CancellationToken ct)
    {
        var tipoLido = EnumsDeConsulta.Ler<TipoDeHolerite>(tipo, "tipo");
        AnoDaLista.Conferir(ano);
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();

        var agora = relogio.AgoraUtc;
        var anoUsado = ano ?? FusoBrasil.Hoje(agora).Year;
        var lista = await consulta.ListarAsync(escopo.FuncionarioId, anoUsado, tipoLido, parametros, agora, ct);
        var resposta = RespostaPaginada<HoleriteItemEu>.De(lista.Itens, lista.Total, parametros);
        return new HoleriteListaEu(resposta.Itens, resposta.Pagina, resposta.TamanhoPagina, resposta.Total, resposta.TotalPaginas, anoUsado, lista.AnosDisponiveis, RegrasDasListasDeHolerite.Contagens(lista.TiposDoAno));
    }
}

/// <summary>
/// O download do PDF do holerite, pelo link temporário (o caminho do arquivo nunca sai da API). O evento <c>HOLERITES</c>/<c>BAIXOU</c> (quem, quando, qual holerite; nunca o nome do arquivo nem o caminho) é gravado ANTES de o link existir, a cada link gerado: se o registro
/// falhar, não há link.
/// </summary>
internal static class LinkDoHolerite
{
    public static async Task<LinkTemporario> CriarAsync(HoleriteParaDownload holerite, bool comVersao, IAuditor auditor, IArmazenamentoDeArquivos armazenamento, CancellationToken ct)
    {
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Holerites, AuditoriaAcao.Baixou, RegrasDasListasDeHolerite.DescricaoDoDownload(holerite), $"Holerite de {holerite.FuncionarioNome}", "HOLERITES", holerite.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(holerite.CaminhoArquivo, RegrasDasListasDeHolerite.NomeDoArquivo(holerite, comVersao), ct);
    }
}

/// <summary><c>GET /api/rh/holerites/{holeriteId}/download</c> (só o RH): qualquer versão, publicada ou agendada, de qualquer pessoa (também de inativa). 404 se não existe.</summary>
public sealed class BaixarHoleriteComoRhCasoDeUso(IConsultaDeHolerites consulta, IAuditor auditor, IArmazenamentoDeArquivos armazenamento)
{
    public async Task<LinkTemporario> ExecutarAsync(int holeriteId, CancellationToken ct) =>
        await LinkDoHolerite.CriarAsync((await consulta.ObterParaDownloadDoRhAsync(holeriteId, ct)).OuNaoEncontrado(), true, auditor, armazenamento, ct);
}

/// <summary>
/// <c>GET /api/eu/holerites/{holeriteId}/download</c> (qualquer perfil): só o holerite DA PESSOA LOGADA, já publicado e na versão atual. Qualquer outro caso (de outra pessoa, mesmo para o gestor e o RH; agendado; versão substituída; inexistente) dá o mesmo 404: a API não revela
/// qual dos casos é.
/// </summary>
public sealed class BaixarMeuHoleriteCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeHoleritesDaPessoa consulta, IAuditor auditor, IArmazenamentoDeArquivos armazenamento, IRelogio relogio)
{
    public async Task<LinkTemporario> ExecutarAsync(int holeriteId, CancellationToken ct) =>
        await LinkDoHolerite.CriarAsync((await consulta.ObterParaDownloadAsync(escopo.FuncionarioId, holeriteId, relogio.AgoraUtc, ct)).OuNaoEncontrado(), false, auditor, armazenamento, ct);
}
