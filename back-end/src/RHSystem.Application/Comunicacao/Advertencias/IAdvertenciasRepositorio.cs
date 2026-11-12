using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>
/// A gravação das advertências. O banco nunca apaga advertência. Cada mudança de situação é <b>um UPDATE condicional na situação de partida</b> (<c>WHERE STATUS_ADVERTENCIA = ...</c>): o que o banco diz que mudou (uma linha) é a verdade, e dois cliques ao mesmo
/// tempo têm um efeito só (o outro volta <c>false</c>, que vira o 422 da rota).
/// </summary>
public interface IAdvertenciasRepositorio
{
    /// <summary>Grava e devolve o id. O que o trigger recusa (pessoa inativa, gestor que adverte a si mesmo) sai como o erro do contrato (<c>ProblemaDeApiException</c>).</summary>
    Task<int> AdicionarAsync(Advertencia advertencia, CancellationToken ct);

    /// <summary><c>PENDENTE</c> → <c>APLICADA</c>, só da pessoa dona, gravando a data da ciência. <c>false</c> se não estava pendente.</summary>
    Task<bool> ConfirmarCienciaAsync(int advertenciaId, int funcionarioId, DateTime agoraUtc, CancellationToken ct);

    /// <summary><c>PENDENTE</c> → <c>CONTESTADA</c>, só da pessoa dona, gravando o texto e a data da contestação. <c>false</c> se não estava pendente (a primeira contestação nunca é sobrescrita).</summary>
    Task<bool> ContestarAsync(int advertenciaId, int funcionarioId, string texto, DateTime agoraUtc, CancellationToken ct);

    /// <summary><c>CONTESTADA</c> → <c>APLICADA</c> (manter) ou <c>CANCELADA</c> (acatar). <c>false</c> se não estava contestada.</summary>
    Task<bool> ResponderAsync(int advertenciaId, DecisaoDaResposta decisao, CancellationToken ct);
}

/// <summary>A leitura das advertências. O escopo sai na PRIMEIRA consulta (<see cref="EscopoDeAdvertencias"/>): "não existe" e "não é seu" dão a mesma resposta.</summary>
public interface IConsultaDeAdvertencias
{
    /// <summary>A ordem é a da tela: <c>PENDENTE</c> primeiro só para a própria pessoa; para os outros, a data do ocorrido mais recente e, no empate, a mais recentemente registrada (o maior id).</summary>
    Task<PaginaDeAdvertencias> ListarAsync(EscopoDeAdvertencias escopo, FiltroDeAdvertencias filtro, ParametrosDeLista parametros, CancellationToken ct);

    Task<AdvertenciaLida?> ObterAsync(int advertenciaId, EscopoDeAdvertencias escopo, CancellationToken ct);

    /// <summary>A pessoa a advertir (<c>null</c> se não existe ou não está no escopo: o RH vê todas, inclusive inativas; o gestor, só a equipe, que é de pessoas ativas).</summary>
    Task<PessoaParaAdvertencia?> ObterPessoaAsync(int funcionarioId, EscopoDeAdvertencias escopo, CancellationToken ct);

    /// <summary>Todas as advertências do ano (qualquer situação) com o setor atual da pessoa, para o resumo do RH.</summary>
    Task<IReadOnlyList<AdvertenciaParaResumoRh>> DoAnoParaOResumoDoRhAsync(int ano, CancellationToken ct);

    /// <summary>A equipe (todas as pessoas, ativas, sem o gestor) e as advertências do ano dela, para o resumo do gestor.</summary>
    Task<(IReadOnlyList<FuncionarioResumo> Equipe, IReadOnlyList<AdvertenciaParaResumoDaEquipe> Advertencias)> DoAnoParaOResumoDaEquipeAsync(int ano, EquipeDoGestor equipe, CancellationToken ct);

    /// <summary>As pessoas do RH que recebem a cópia do e-mail de uma advertência registrada pelo gestor: ativas, com perfil RH e e-mail corporativo.</summary>
    Task<IReadOnlyList<DestinatarioDoRh>> DestinatariosDoRhAsync(CancellationToken ct);
}
