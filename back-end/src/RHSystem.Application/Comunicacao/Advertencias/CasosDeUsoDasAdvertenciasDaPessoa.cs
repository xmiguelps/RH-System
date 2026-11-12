using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>
/// <c>GET /api/eu/advertencias</c> (qualquer perfil): as advertências DA PESSOA LOGADA (um gestor ou alguém do RH também pode receber advertência), de todos os anos. As <c>PENDENTE</c> primeiro (a tela as destaca), depois as demais da data do ocorrido mais recente para a mais antiga;
/// cada item já traz a descrição e a contestação. <c>situacao</c> filtra. Nunca mostra a advertência de outra pessoa.
/// </summary>
public sealed class ListarMinhasAdvertenciasCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta)
{
    public async Task<RespostaPaginada<AdvertenciaEu>> ExecutarAsync(int pagina, int tamanhoPagina, string? situacao, CancellationToken ct)
    {
        var situacaoLida = EnumsDeConsulta.Ler<SituacaoDaAdvertencia>(situacao, "situacao");
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var lista = await consulta.ListarAsync(EscoposDeAdvertencia.DaPessoa(escopo), new FiltroDeAdvertencias(null, situacaoLida, null, null, null), parametros, ct);
        return RespostaPaginada<AdvertenciaEu>.De([.. lista.Itens.Select(MapeadorDaAdvertencia.Eu)], lista.Total, parametros);
    }
}

/// <summary>
/// <c>POST /api/eu/advertencias/{advertenciaId}/ciencia</c> (qualquer perfil): a pessoa confirma que tomou ciência (não é concordar). <c>PENDENTE</c> → <c>APLICADA</c>, com a data da ciência do <c>IRelogio</c>, num UPDATE condicional em <c>PENDENTE</c> e na pessoa dona (a
/// ciência e a contestação ao mesmo tempo têm um efeito só). Ordem: 404 (de outra pessoa ou inexistente: o mesmo), 422 <c>ADVERTENCIA_NAO_ESTA_PENDENTE</c> (inclusive repetir o clique). Sem e-mail. O evento (<c>ALTEROU</c>) não leva o texto da advertência.
/// </summary>
public sealed class ConfirmarCienciaDeAdvertenciaCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IAdvertenciasRepositorio advertencias, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<AdvertenciaEu> ExecutarAsync(int advertenciaId, CancellationToken ct)
    {
        var daPessoa = EscoposDeAdvertencia.DaPessoa(escopo);
        var advertencia = (await consulta.ObterAsync(advertenciaId, daPessoa, ct)).OuNaoEncontrado();

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await advertencias.ConfirmarCienciaAsync(advertenciaId, escopo.FuncionarioId, relogio.AgoraUtc, ct))
                {
                    throw MensagensDaComunicacao.AdvertenciaNaoEstaPendente.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Advertencias,
                    AuditoriaAcao.Alterou,
                    string.Create(CultureInfo.InvariantCulture, $"Confirmou a ciência da advertência de {advertencia.Data:dd/MM/yyyy}."),
                    $"Advertência de {advertencia.Funcionario.Nome}",
                    "ADVERTENCIAS",
                    advertenciaId,
                    [new MudancaDeAuditoriaNova("situacao", NomesDoContrato.Enum(SituacaoDaAdvertencia.Pendente), NomesDoContrato.Enum(SituacaoDaAdvertencia.Aplicada))]));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);

        return MapeadorDaAdvertencia.Eu((await consulta.ObterAsync(advertenciaId, daPessoa, ct)).OuNaoEncontrado());
    }
}

/// <summary>
/// <c>POST /api/eu/advertencias/{advertenciaId}/contestacao</c> (qualquer perfil): a pessoa contesta, explicando (10 a 2000 caracteres). <c>PENDENTE</c> → <c>CONTESTADA</c> com o texto e a data da contestação, num UPDATE condicional em <c>PENDENTE</c> e na pessoa dona: a primeira
/// contestação nunca é sobrescrita e contestar depois da ciência (ou a ciência depois de contestar) é 422 <c>ADVERTENCIA_NAO_ESTA_PENDENTE</c>. Ordem: 404, 400 (o texto), 422. NÃO manda e-mail ao RH (o RH a encontra na aba "Contestada"); o evento não leva o texto.
/// </summary>
public sealed class ContestarAdvertenciaCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IAdvertenciasRepositorio advertencias, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<AdvertenciaEu> ExecutarAsync(int advertenciaId, AdvertenciaContestacaoComando? corpo, CancellationToken ct)
    {
        var daPessoa = EscoposDeAdvertencia.DaPessoa(escopo);
        var advertencia = (await consulta.ObterAsync(advertenciaId, daPessoa, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        var texto = ValidacaoDaAdvertencia.ValidarContestacao(erros, corpo?.Texto);
        erros.LancarSeHouver();

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await advertencias.ContestarAsync(advertenciaId, escopo.FuncionarioId, texto!, relogio.AgoraUtc, ct))
                {
                    throw MensagensDaComunicacao.AdvertenciaNaoEstaPendente.ComoProblema();
                }

                auditor.Registrar(new EventoDeAuditoriaNovo(
                    AuditoriaArea.Advertencias,
                    AuditoriaAcao.Alterou,
                    string.Create(CultureInfo.InvariantCulture, $"Contestou a advertência de {advertencia.Data:dd/MM/yyyy}."),
                    $"Advertência de {advertencia.Funcionario.Nome}",
                    "ADVERTENCIAS",
                    advertenciaId,
                    [new MudancaDeAuditoriaNova("situacao", NomesDoContrato.Enum(SituacaoDaAdvertencia.Pendente), NomesDoContrato.Enum(SituacaoDaAdvertencia.Contestada))]));
                await unidade.SalvarAsync(ct);
                return 0;
            },
            ct);

        return MapeadorDaAdvertencia.Eu((await consulta.ObterAsync(advertenciaId, daPessoa, ct)).OuNaoEncontrado());
    }
}
