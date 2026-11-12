using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Emails;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>O escopo do RH (todas as advertências) para a consulta.</summary>
internal static class EscoposDeAdvertencia
{
    public static EscopoDeAdvertencias DoRh(IEscopoDoUsuario escopo) => new(PapelNaConsulta.Rh, escopo.FuncionarioId, null);

    public static EscopoDeAdvertencias DoGestor(IEscopoDoUsuario escopo, EquipeDoGestor? equipe) => new(PapelNaConsulta.Gestor, escopo.FuncionarioId, equipe);

    public static EscopoDeAdvertencias DaPessoa(IEscopoDoUsuario escopo) => new(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null);
}

/// <summary>
/// <c>GET /api/rh/advertencias</c> (só o RH): as advertências de TODOS os setores de UM ano (<c>ano</c>, padrão o ano atual no horário do Brasil) pela data do ocorrido. <c>situacao</c> são as abas, <c>tipo</c> e <c>setorId</c> (o setor ATUAL da pessoa) filtram, e a <c>busca</c>
/// procura no nome da pessoa e no motivo. Ordem: data do ocorrido mais recente e, no empate, a registrada por último. Os filtros de enum e o ano são conferidos antes (a ordem dos erros é tipo, situação, ano, parâmetros).
/// </summary>
public sealed class ListarAdvertenciasDoRhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IRelogio relogio)
{
    public async Task<RespostaPaginada<AdvertenciaItem>> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, int? ano, string? situacao, int? setorId, string? tipo, CancellationToken ct)
    {
        var situacaoLida = EnumsDeConsulta.Ler<SituacaoDaAdvertencia>(situacao, "situacao");
        var tipoLido = EnumsDeConsulta.Ler<TipoDeAdvertencia>(tipo, "tipo");
        AnoDaLista.Conferir(ano);
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        var anoUsado = ano ?? FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        var lista = await consulta.ListarAsync(EscoposDeAdvertencia.DoRh(escopo), new FiltroDeAdvertencias(anoUsado, situacaoLida, tipoLido, setorId, null), parametros, ct);
        return RespostaPaginada<AdvertenciaItem>.De([.. lista.Itens.Select(MapeadorDaAdvertencia.Item)], lista.Total, parametros);
    }
}

/// <summary>
/// <c>GET /api/rh/advertencias/resumo</c> (só o RH): os números do ano (<c>ano</c>, padrão o ano atual). <c>contagens</c> são as abas e somam as quatro situações (as canceladas contam em <c>todas</c>, como a lista); <c>setorComMaisRegistros</c> é o setor ATUAL com mais
/// advertências NÃO canceladas do ano (empate: o primeiro em ordem alfabética; pessoa sem setor não conta para nenhum), ou <c>null</c> se não há nenhuma.
/// </summary>
public sealed class ObterResumoDasAdvertenciasCasoDeUso(IConsultaDeAdvertencias consulta, IRelogio relogio)
{
    public async Task<AdvertenciaResumoRh> ExecutarAsync(int? ano, CancellationToken ct)
    {
        AnoDaLista.Conferir(ano);
        var anoUsado = ano ?? FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        return ResumosDeAdvertencia.DoRh(anoUsado, await consulta.DoAnoParaOResumoDoRhAsync(anoUsado, ct));
    }
}

/// <summary><c>GET /api/rh/advertencias/{advertenciaId}</c> (só o RH): a advertência inteira, com a contestação e o desfecho. 404 se não existe.</summary>
public sealed class ObterAdvertenciaDoRhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta)
{
    public async Task<AdvertenciaDetalheRh> ExecutarAsync(int advertenciaId, CancellationToken ct) =>
        MapeadorDaAdvertencia.DetalheRh((await consulta.ObterAsync(advertenciaId, EscoposDeAdvertencia.DoRh(escopo), ct)).OuNaoEncontrado());
}

/// <summary>
/// <c>POST /api/rh/advertencias/{advertenciaId}/resposta</c> (só o RH): responde à contestação. Ordem: 404, 400 (a resposta), 422 <c>ADVERTENCIA_NAO_ESTA_CONTESTADA</c>; o efeito é um UPDATE condicional em <c>CONTESTADA</c> (duas abas: uma só vale). <c>MANTER</c> volta a
/// <c>APLICADA</c> e <c>ACATAR</c> cancela (e cancelada não muda mais). A resposta NÃO fica no banco: vai por e-mail à pessoa e para a auditoria (<c>VALOR_DEPOIS</c> do campo <c>resposta</c>); a pessoa vê só a nova situação.
/// </summary>
public sealed class ResponderContestacaoDeAdvertenciaCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IAdvertenciasRepositorio advertencias, IFilaDeEmails fila, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public const string TipoDoEmail = "ADVERTENCIA_RESPONDIDA";

    public async Task<AdvertenciaDetalheRh> ExecutarAsync(int advertenciaId, AdvertenciaRespostaComando? corpo, CancellationToken ct)
    {
        var doRh = EscoposDeAdvertencia.DoRh(escopo);
        var advertencia = (await consulta.ObterAsync(advertenciaId, doRh, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        var resposta = ValidacaoDaAdvertencia.ValidarResposta(erros, corpo?.Resposta);
        erros.LancarSeHouver();
        var decisao = corpo!.Decisao;
        var pessoa = (await consulta.ObterPessoaAsync(advertencia.Funcionario.Id, doRh, ct)).OuNaoEncontrado();
        var nova = RegrasDaAdvertencia.SituacaoDaDecisao(decisao);

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await advertencias.ResponderAsync(advertenciaId, decisao, ct))
                {
                    throw MensagensDaComunicacao.AdvertenciaNaoEstaContestada.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Advertencias,
                    AuditoriaAcao.Alterou,
                    $"Respondeu à contestação da advertência de {pessoa.Nome}: {(decisao == DecisaoDaResposta.Acatar ? "acatada" : "mantida")}.",
                    $"Advertência de {pessoa.Nome}",
                    "ADVERTENCIAS",
                    advertenciaId,
                    [
                        new MudancaDeAuditoriaNova("situacao", NomesDoContrato.Enum(SituacaoDaAdvertencia.Contestada), NomesDoContrato.Enum(nova)),
                        new MudancaDeAuditoriaNova("resposta", null, resposta),
                    ]));
                await unidade.SalvarAsync(ct);
                if (!string.IsNullOrWhiteSpace(pessoa.EmailCorporativo))
                {
                    await fila.EnfileirarAsync(
                        new EmailParaEnfileirar(TipoDoEmail, ChaveDeIdempotencia.Montar(TipoDoEmail, ("advertencia", advertenciaId)), pessoa.EmailCorporativo, new { nome = pessoa.Nome, decisao = NomesDoContrato.Enum(decisao), resposta, data = advertencia.Data.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture) }, pessoa.Id, advertenciaId),
                        ct);
                }

                return 0;
            },
            ct);

        return MapeadorDaAdvertencia.DetalheRh((await consulta.ObterAsync(advertenciaId, doRh, ct)).OuNaoEncontrado());
    }
}

/// <summary>
/// <c>GET /api/rh/funcionarios/{funcionarioId}/advertencias</c> (só o RH): TODAS as advertências da pessoa, de todos os anos e inclusive as canceladas, da data do ocorrido mais recente para a mais antiga. Funciona para pessoa inativa; 404 se a pessoa não existe. O
/// indicador da ficha (<c>advertenciasQuantidade</c>) é outra conta: só as não canceladas do ano atual.
/// </summary>
public sealed class ListarAdvertenciasDoFuncionarioCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta)
{
    public async Task<RespostaPaginada<AdvertenciaItem>> ExecutarAsync(int funcionarioId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var doRh = EscoposDeAdvertencia.DoRh(escopo);
        _ = (await consulta.ObterPessoaAsync(funcionarioId, doRh, ct)).OuNaoEncontrado();
        var lista = await consulta.ListarAsync(doRh, new FiltroDeAdvertencias(null, null, null, null, funcionarioId), parametros, ct);
        return RespostaPaginada<AdvertenciaItem>.De([.. lista.Itens.Select(MapeadorDaAdvertencia.Item)], lista.Total, parametros);
    }
}

/// <summary>
/// <c>POST /api/rh/funcionarios/{funcionarioId}/advertencias</c> (só o RH): registra uma advertência (nasce <c>PENDENTE</c>). Ordem: 404 (a pessoa), 400 (campos, todos juntos), 422 <c>FUNCIONARIO_INATIVO</c> (com o <c>regraDoBanco</c> do trigger), 422 <c>ADVERTENCIA_SEM_GESTOR</c>
/// (só com <c>aplicadaPor: GESTOR_DO_COLABORADOR</c> e a pessoa sem gestor ativo). <c>RH</c> grava sem gestor; o e-mail sai SÓ à pessoa.
/// </summary>
public sealed class RegistrarAdvertenciaDoRhCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, RegistradorDeAdvertencia registrador, IRelogio relogio)
{
    public async Task<AdvertenciaDetalheRh> ExecutarAsync(int funcionarioId, AdvertenciaRegistroRhComando corpo, CancellationToken ct)
    {
        var doRh = EscoposDeAdvertencia.DoRh(escopo);
        var pessoa = (await consulta.ObterPessoaAsync(funcionarioId, doRh, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        var campos = ValidacaoDaAdvertencia.ValidarRegistro(erros, corpo.DataDoOcorrido, FusoBrasil.Hoje(relogio.AgoraUtc), corpo.Motivo, corpo.Descricao);
        erros.LancarSeHouver();

        if (!pessoa.Ativo)
        {
            throw MensagensDaComunicacao.AdvertenciaDeInativo.ComoProblema(RegrasDoBancoDaComunicacao.AdvertenciaDeInativo);
        }

        int? gestorId = null;
        if (corpo.AplicadaPor == AplicadaPorNoRegistro.GestorDoColaborador)
        {
            if (pessoa.GestorId is not { } gestor || !pessoa.GestorAtivo)
            {
                throw MensagensDaComunicacao.AdvertenciaSemGestor.ComoProblema();
            }

            gestorId = gestor;
        }

        var id = await registrador.RegistrarAsync(pessoa, gestorId, null, corpo.DataDoOcorrido, corpo.Tipo, campos!, false, ct);
        return MapeadorDaAdvertencia.DetalheRh((await consulta.ObterAsync(id, doRh, ct)).OuNaoEncontrado());
    }
}
