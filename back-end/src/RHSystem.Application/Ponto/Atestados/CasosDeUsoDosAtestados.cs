using System.Data;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Documentos;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Atestados;

/// <summary>O atestado lido, como o contrato o mostra e como a auditoria o descreve (nunca o nome do arquivo: é dado de saúde).</summary>
internal static class MapeadorDoAtestado
{
    public static PontoAtestado Montar(AtestadoLido a) =>
        new(a.Id, a.Funcionario, a.DataInicio, a.DataFim, a.HoraInicio, a.HoraFim, a.Situacao, a.NomeArquivo, a.EnviadoEmUtc, a.DecididoEmUtc, a.DecididoPorNome, a.MotivoRecusa);

    /// <summary>"Atestado de Diego Farias": o <c>RegistroAfetado</c> dos eventos.</summary>
    public static string Registro(AtestadoLido a) => $"Atestado de {a.Funcionario.Nome}";

    /// <summary>"o atestado de Diego Farias (01/10/2026 a 02/10/2026)": o texto dos eventos de download, validação e recusa.</summary>
    public static string Descricao(AtestadoLido a) => $"o atestado de {a.Funcionario.Nome} ({PeriodoDoAtestado.Texto(a.DataInicio, a.DataFim, a.HoraInicio, a.HoraFim)})";
}

/// <summary>Lê os filtros de texto das listas de atestados (<c>ENVIADO</c>, <c>VALIDADO</c>, <c>RECUSADO</c>) e confere a paginação, com os erros por campo.</summary>
public static class LeitorDeFiltrosDeAtestados
{
    public static (ParametrosDeLista Parametros, FiltroDeAtestados Filtro) Ler(int pagina, int tamanhoPagina, string? situacao, string? busca, int? funcionarioId)
    {
        var filtroDeSituacao = EnumsDeConsulta.Ler<SituacaoDoAtestado>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        return (parametros, new FiltroDeAtestados(filtroDeSituacao, funcionarioId));
    }
}

/// <summary>
/// As três listas de atestados (mais recente primeiro): <c>GET /api/eu/ponto/atestados</c> (qualquer perfil, só os da própria pessoa), <c>GET /api/gestor/ponto/atestados</c> (a equipe do gestor, sem ele; pessoa de outra equipe no filtro dá a lista vazia; gestor sem setor, lista
/// vazia) e <c>GET /api/rh/ponto/atestados</c> (a empresa toda, com <c>busca</c> no nome). O escopo sai na consulta, nunca depois.
/// </summary>
public sealed class ListarAtestadosCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAtestados consulta)
{
    public Task<RespostaPaginada<PontoAtestado>> MeusAsync(int pagina, int tamanhoPagina, string? situacao, CancellationToken ct) =>
        ListarAsync(new EscopoDeAtestados(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null), pagina, tamanhoPagina, situacao, null, null, ct);

    public async Task<RespostaPaginada<PontoAtestado>> DaEquipeAsync(int pagina, int tamanhoPagina, string? situacao, int? funcionarioId, CancellationToken ct) =>
        await ListarAsync(new EscopoDeAtestados(PapelNaConsulta.Gestor, escopo.FuncionarioId, await escopo.ObterEquipeAsync(ct)), pagina, tamanhoPagina, situacao, null, funcionarioId, ct);

    public Task<RespostaPaginada<PontoAtestado>> ParaRhAsync(int pagina, int tamanhoPagina, string? situacao, string? busca, int? funcionarioId, CancellationToken ct) =>
        ListarAsync(new EscopoDeAtestados(PapelNaConsulta.Rh, escopo.FuncionarioId, null), pagina, tamanhoPagina, situacao, busca, funcionarioId, ct);

    private async Task<RespostaPaginada<PontoAtestado>> ListarAsync(EscopoDeAtestados doEscopo, int pagina, int tamanhoPagina, string? situacao, string? busca, int? funcionarioId, CancellationToken ct)
    {
        var (parametros, filtro) = LeitorDeFiltrosDeAtestados.Ler(pagina, tamanhoPagina, situacao, busca, funcionarioId);
        var resultado = await consulta.ListarAsync(doEscopo, filtro, parametros, ct);
        return RespostaPaginada<PontoAtestado>.De([.. resultado.Itens.Select(MapeadorDoAtestado.Montar)], resultado.Total, parametros);
    }
}

/// <summary>
/// <c>POST /api/eu/ponto/atestados</c> (multipart): a pessoa envia o atestado dela, de dias inteiros ou de algumas horas de um dia. Todos os erros de campo e de arquivo vêm juntos num 400 (<c>dataInicio</c>, <c>dataFim</c>, <c>horaInicio</c>, <c>horaFim</c>, <c>arquivo</c>), antes de
/// qualquer efeito; o tipo do arquivo vem dos primeiros bytes, até 5 MiB. O arquivo só é guardado depois (fora de qualquer transação) e a linha com o evento <c>ATESTADOS</c>/<c>CRIOU</c> vão numa transação curta, pela receita de <see cref="EnvioDeArquivoDeDocumento"/>: se a
/// linha falhar, o blob é apagado. O atestado nasce <c>ENVIADO</c>. O evento não leva o nome do arquivo (dado de saúde) e nenhum e-mail sai.
/// </summary>
public sealed class EnviarAtestadoCasoDeUso(
    IEscopoDoUsuario escopo,
    IAtestadosRepositorio atestados,
    IConsultaDeAtestados consulta,
    EnvioDeArquivoDeDocumento envio,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    public async Task<PontoAtestado> ExecutarAsync(NovoAtestado novo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var campos = LeitorDosCamposDoAtestado.Ler(erros, novo.DataInicio, novo.DataFim, novo.HoraInicio, novo.HoraFim);
        var tipoDoArquivo = await ValidacaoDeArquivoDeDocumento.ValidarAsync(erros, novo.Arquivo, novo.ArquivosRecebidos, "O arquivo é obrigatório.", LimitesDeAtestado.TamanhoMaximoBytes, "O arquivo pode ter até 5 MB.", ct);
        erros.LancarSeHouver();

        var nomeGuardado = NomeParaDownload.Limpar(novo.Arquivo!.NomeOriginal, tipoDoArquivo!.Extensao);
        var id = await envio.GravarEEfetivarAsync(
            novo.Arquivo,
            LimitesDeAtestado.PastaNoArmazenamento,
            tipoDoArquivo,
            caminho => unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var atestado = Atestado.Enviar(escopo.FuncionarioId, campos!.DataInicio, campos.DataFim, campos.HoraInicio, campos.HoraFim, caminho, nomeGuardado, relogio.AgoraUtc);
                    var atestadoId = await atestados.AdicionarAsync(atestado, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(
                        AuditoriaArea.Atestados,
                        AuditoriaAcao.Criou,
                        $"Enviou o atestado de {PeriodoDoAtestado.Texto(campos.DataInicio, campos.DataFim, campos.HoraInicio, campos.HoraFim)}.",
                        $"Atestado de {escopo.Nome}",
                        "ATESTADOS",
                        atestadoId));
                    await unidade.SalvarAsync(ct);
                    return atestadoId;
                },
                ct),
            ct);

        return MapeadorDoAtestado.Montar((await consulta.ObterAsync(id, new EscopoDeAtestados(PapelNaConsulta.PropriaPessoa, escopo.FuncionarioId, null), ct)).OuNaoEncontrado());
    }
}

/// <summary>
/// O download do arquivo do atestado, para o gestor (só a equipe dele) e para o RH (qualquer um): atestado fora do escopo e inexistente dão o mesmo 404. O arquivo é dado de saúde: o evento <c>ATESTADOS</c>/<c>BAIXOU</c> (quem, quando, qual atestado; nunca o nome do arquivo) é gravado ANTES de o
/// link existir, a cada link gerado; se o registro falhar, não há link.
/// </summary>
public sealed class BaixarAtestadoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAtestados consulta, IAuditor auditor, IArmazenamentoDeArquivos armazenamento)
{
    public async Task<LinkTemporario> DaEquipeAsync(int atestadoId, CancellationToken ct) =>
        await BaixarAsync(atestadoId, new EscopoDeAtestados(PapelNaConsulta.Gestor, escopo.FuncionarioId, await escopo.ObterEquipeAsync(ct)), ct);

    public Task<LinkTemporario> ComoRhAsync(int atestadoId, CancellationToken ct) =>
        BaixarAsync(atestadoId, new EscopoDeAtestados(PapelNaConsulta.Rh, escopo.FuncionarioId, null), ct);

    private async Task<LinkTemporario> BaixarAsync(int atestadoId, EscopoDeAtestados doEscopo, CancellationToken ct)
    {
        var atestado = (await consulta.ObterAsync(atestadoId, doEscopo, ct)).OuNaoEncontrado();

        // O registro vem PRIMEIRO (fora de transação, em contexto próprio): se ele falhar, não há link.
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Atestados, AuditoriaAcao.Baixou, $"Baixou {MapeadorDoAtestado.Descricao(atestado)}.", MapeadorDoAtestado.Registro(atestado), "ATESTADOS", atestado.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(atestado.CaminhoArquivo, atestado.NomeArquivo ?? string.Empty, ct);
    }
}

/// <summary>
/// A decisão FINAL do RH sobre um atestado: <c>validar</c> (o dia ou as horas deixam de contar como falta) ou <c>recusar</c> (com o motivo; o dia segue como falta). Ordem: 404 (a leitura não é efeito), 400 do motivo, 409 <c>ATESTADO_JA_DECIDIDO</c>; e o efeito num UPDATE condicional em
/// <c>ENVIADO</c> (duas abas: uma só vale, a outra perde com o mesmo 409). O RH pode decidir o próprio atestado. Validar manda recalcular, pela porta do banco de horas, cada dia do atestado que já passou (os de hoje e do futuro ainda nem foram consolidados). O evento
/// (<c>APROVOU</c> ou <c>RECUSOU</c> em <c>ATESTADOS</c>) não leva o nome do arquivo nem o motivo, e nenhum e-mail sai.
/// </summary>
public sealed class DecidirAtestadoCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDeAtestados consulta,
    IAtestadosRepositorio atestados,
    IRecalculoDoBancoDeHoras recalculo,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private const int TamanhoMaximoDoMotivo = 500;

    public Task<PontoAtestado> ValidarAsync(int atestadoId, CancellationToken ct) => DecidirAsync(atestadoId, SituacaoDoAtestado.Validado, null, ct);

    public Task<PontoAtestado> RecusarAsync(int atestadoId, PontoAtestadoRecusar? corpo, CancellationToken ct) => DecidirAsync(atestadoId, SituacaoDoAtestado.Recusado, corpo?.Motivo, ct);

    private async Task<PontoAtestado> DecidirAsync(int atestadoId, SituacaoDoAtestado decisao, string? motivoDigitado, CancellationToken ct)
    {
        var doRh = new EscopoDeAtestados(PapelNaConsulta.Rh, escopo.FuncionarioId, null);
        var atestado = (await consulta.ObterAsync(atestadoId, doRh, ct)).OuNaoEncontrado();

        string? motivo = null;
        if (decisao == SituacaoDoAtestado.Recusado)
        {
            motivo = (motivoDigitado ?? string.Empty).Trim();
            if (motivo.Length is < 1 or > TamanhoMaximoDoMotivo)
            {
                throw new ErroDeValidacaoException("motivo", $"Informe o motivo da recusa, de 1 a {TamanhoMaximoDoMotivo} caracteres.");
            }
        }

        if (atestado.Situacao != SituacaoDoAtestado.Enviado)
        {
            throw MensagensDoPonto.AtestadoJaDecidido.ComoProblema();
        }

        var agora = relogio.AgoraUtc;
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await atestados.DecidirAsync(atestadoId, decisao, escopo.FuncionarioId, agora, motivo, ct))
                {
                    throw MensagensDoPonto.AtestadoJaDecidido.ComoProblema(); // outra decisão chegou antes
                }

                var validado = decisao == SituacaoDoAtestado.Validado;
                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Atestados,
                    validado ? AuditoriaAcao.Aprovou : AuditoriaAcao.Recusou,
                    $"{(validado ? "Validou" : "Recusou")} {MapeadorDoAtestado.Descricao(atestado)}.",
                    MapeadorDoAtestado.Registro(atestado),
                    "ATESTADOS",
                    atestadoId));
                await unidade.SalvarAsync(ct);

                if (validado)
                {
                    await RecalcularAsync(atestado, FusoBrasil.Hoje(agora), ct);
                }

                return 0;
            },
            IsolationLevel.ReadCommitted,
            ct);

        return MapeadorDoAtestado.Montar((await consulta.ObterAsync(atestadoId, doRh, ct)).OuNaoEncontrado());
    }

    private async Task RecalcularAsync(AtestadoLido atestado, DateOnly hoje, CancellationToken ct)
    {
        for (var dia = atestado.DataInicio; dia <= atestado.DataFim && dia < hoje; dia = dia.AddDays(1))
        {
            await recalculo.RecalcularDiaAsync(atestado.Funcionario.Id, dia, ct);
        }
    }
}
