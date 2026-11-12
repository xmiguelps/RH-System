using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>
/// <c>GET /api/gestor/advertencias</c> (só o gestor, só a EQUIPE dele): as advertências das pessoas da equipe (a mesma de <c>GET /api/gestor/equipe</c>), registradas por qualquer um (o gestor ou o RH), de UM ano (<c>ano</c>, padrão o atual) pela data do ocorrido, com filtro de
/// <c>situacao</c>. O gestor sem setor (sem equipe) recebe a lista vazia. Ordem: data do ocorrido mais recente e, no empate, a registrada por último.
/// </summary>
public sealed class ListarAdvertenciasDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IRelogio relogio)
{
    public async Task<RespostaPaginada<AdvertenciaItem>> ExecutarAsync(int pagina, int tamanhoPagina, int? ano, string? situacao, CancellationToken ct)
    {
        var situacaoLida = EnumsDeConsulta.Ler<SituacaoDaAdvertencia>(situacao, "situacao");
        AnoDaLista.Conferir(ano);
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var anoUsado = ano ?? FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        var lista = await consulta.ListarAsync(EscoposDeAdvertencia.DoGestor(escopo, await escopo.ObterEquipeAsync(ct)), new FiltroDeAdvertencias(anoUsado, situacaoLida, null, null, null), parametros, ct);
        return RespostaPaginada<AdvertenciaItem>.De([.. lista.Itens.Select(MapeadorDaAdvertencia.Item)], lista.Total, parametros);
    }
}

/// <summary>
/// <c>GET /api/gestor/advertencias/resumo</c> (só o gestor): os números do ano para a equipe, todos pela regra única das advertências NÃO canceladas do ano (a mesma do indicador da ficha). <c>porPessoa</c> traz TODAS as pessoas da equipe (inclusive com 0), da que tem mais para a
/// que tem menos e, no empate, por nome; a soma das quantidades é <c>registradas</c>. O gestor sem equipe recebe tudo zerado.
/// </summary>
public sealed class ObterResumoDasAdvertenciasDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, IRelogio relogio)
{
    public async Task<AdvertenciaResumoGestor> ExecutarAsync(int? ano, CancellationToken ct)
    {
        AnoDaLista.Conferir(ano);
        var anoUsado = ano ?? FusoBrasil.Hoje(relogio.AgoraUtc).Year;
        if (await escopo.ObterEquipeAsync(ct) is not { } equipe)
        {
            return ResumosDeAdvertencia.DaEquipe(anoUsado, [], []);
        }

        var (pessoas, advertencias) = await consulta.DoAnoParaOResumoDaEquipeAsync(anoUsado, equipe, ct);
        return ResumosDeAdvertencia.DaEquipe(anoUsado, pessoas, advertencias);
    }
}

/// <summary><c>GET /api/gestor/advertencias/{advertenciaId}</c> (só o gestor): a advertência inteira de uma pessoa da EQUIPE, sem o texto da contestação nem o desfecho. De fora da equipe (ou que não existe): 404.</summary>
public sealed class ObterAdvertenciaDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta)
{
    public async Task<AdvertenciaDetalheGestor> ExecutarAsync(int advertenciaId, CancellationToken ct) =>
        MapeadorDaAdvertencia.DetalheGestor((await consulta.ObterAsync(advertenciaId, EscoposDeAdvertencia.DoGestor(escopo, await escopo.ObterEquipeAsync(ct)), ct)).OuNaoEncontrado());
}

/// <summary>
/// <c>GET /api/gestor/equipe/{funcionarioId}/advertencias</c> (só o gestor): TODAS as advertências de uma pessoa da equipe, de todos os anos e inclusive as canceladas, data do ocorrido mais recente primeiro. Pessoa de outro setor (ou o próprio gestor, ou quem não é da equipe):
/// 404.
/// </summary>
public sealed class ListarAdvertenciasDoFuncionarioDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta)
{
    public async Task<RespostaPaginada<AdvertenciaItem>> ExecutarAsync(int funcionarioId, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina).Validado();
        var doGestor = EscoposDeAdvertencia.DoGestor(escopo, await escopo.ObterEquipeAsync(ct));
        _ = (await consulta.ObterPessoaAsync(funcionarioId, doGestor, ct)).OuNaoEncontrado();
        var lista = await consulta.ListarAsync(doGestor, new FiltroDeAdvertencias(null, null, null, null, funcionarioId), parametros, ct);
        return RespostaPaginada<AdvertenciaItem>.De([.. lista.Itens.Select(MapeadorDaAdvertencia.Item)], lista.Total, parametros);
    }
}

/// <summary>
/// <c>POST /api/gestor/equipe/{funcionarioId}/advertencias</c> (só o gestor): registra uma advertência para uma pessoa da EQUIPE dele, <b>aplicada por ele</b> (o gestor logado). Ordem: 404 (fora da equipe, o próprio gestor ou inativo: a equipe só tem pessoas ativas), 400 (campos),
/// 422 <c>FUNCIONARIO_INATIVO</c> (a corrida de uma inativação entre a leitura e a gravação: é o trigger do banco). A pessoa recebe SEMPRE o e-mail e o RH recebe uma cópia (cada pessoa ativa do RH com e-mail); o gestor não recebe cópia.
/// </summary>
public sealed class RegistrarAdvertenciaDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAdvertencias consulta, RegistradorDeAdvertencia registrador, IRelogio relogio)
{
    public async Task<AdvertenciaDetalheGestor> ExecutarAsync(int funcionarioId, AdvertenciaRegistroGestorComando corpo, CancellationToken ct)
    {
        var equipe = await escopo.ObterEquipeAsync(ct);
        var doGestor = EscoposDeAdvertencia.DoGestor(escopo, equipe);
        var pessoa = (await consulta.ObterPessoaAsync(funcionarioId, doGestor, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        var campos = ValidacaoDaAdvertencia.ValidarRegistro(erros, corpo.DataDoOcorrido, FusoBrasil.Hoje(relogio.AgoraUtc), corpo.Motivo, corpo.Descricao);
        erros.LancarSeHouver();

        if (!pessoa.Ativo)
        {
            throw MensagensDaComunicacao.AdvertenciaDeInativo.ComoProblema(RegrasDoBancoDaComunicacao.AdvertenciaDeInativo);
        }

        var id = await registrador.RegistrarAsync(pessoa, equipe!.GestorId, escopo.Nome, corpo.DataDoOcorrido, corpo.Tipo, campos!, true, ct);
        return MapeadorDaAdvertencia.DetalheGestor((await consulta.ObterAsync(id, doGestor, ct)).OuNaoEncontrado());
    }
}
