using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Application.Autenticacao;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Equipe;

/// <summary>Monta a ficha da pessoa (as mesmas linhas para o RH e para o gestor, mesma visibilidade e mesma ordem): obrigatórios primeiro, depois <c>comecaEm</c> (o mais antigo primeiro; sem data por último), título e id.</summary>
internal static class MontagemDaFicha
{
    public static IReadOnlyList<MeuTreinamentoLido> Linhas(IEnumerable<MeuTreinamentoLido> todos) =>
        [.. todos.Where(t => t.Linha is not null).OrderByDescending(t => t.Obrigatorio).ThenBy(t => t.ComecaEm is null).ThenBy(t => t.ComecaEm).ThenBy(t => t.Titulo, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id)];

    public static TreinamentoFichaResumo Resumo(IReadOnlyList<MeuTreinamentoLido> linhas, int pendentes) =>
        new(pendentes, linhas.Count(t => t.Linha == SituacaoNoTreinamento.EmAndamento), linhas.Count(t => t.Linha == SituacaoNoTreinamento.Concluido));

    public static TreinamentoFichaItemRh ItemRh(MeuTreinamentoLido t)
    {
        var cartao = MapeadorDoMeuTreinamento.Cartao(t);
        return new TreinamentoFichaItemRh(t.Id, t.Titulo, t.Obrigatorio, t.Onboarding, t.Linha!.Value, t.IniciadoEm, cartao.ConcluidoEmUtc, cartao.ModulosConcluidos, cartao.TotalModulos, t.Linha == SituacaoNoTreinamento.Concluido && t.EmiteCertificado);
    }

    public static TreinamentoFichaItemGestor ItemGestor(MeuTreinamentoLido t)
    {
        var cartao = MapeadorDoMeuTreinamento.Cartao(t);
        return new TreinamentoFichaItemGestor(t.Id, t.Titulo, t.Obrigatorio, t.Onboarding, t.Linha!.Value, t.IniciadoEm, cartao.ConcluidoEmUtc, cartao.ModulosConcluidos, cartao.TotalModulos);
    }
}

/// <summary>
/// <c>GET /api/rh/funcionarios/{funcionarioId}/treinamentos</c>: as linhas da pessoa em treinamentos publicados já liberados (o encerrado só se ela começou ou concluiu; <c>REPROVADO</c> e <c>CANCELADO</c> fora), obrigatórios primeiro, com o resumo e o certificado disponível para o RH baixar. 404 se a pessoa não existe. Pessoa inativa continua visível para o RH, com <c>resumo.pendentes</c> 0.
/// </summary>
public sealed class ListarTreinamentosDeFuncionarioCasoDeUso(IConsultaDeTreinamentos existencia, IConsultaDeMeusTreinamentos linhas, IConsultaDaEquipeNosTreinamentos pendencias, IRelogio relogio)
{
    public async Task<TreinamentoFichaRhResposta> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        if ((await existencia.FuncionariosInexistentesAsync([funcionarioId], ct)).Count > 0)
        {
            throw new NaoEncontradoException();
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var lista = MontagemDaFicha.Linhas(await linhas.ListarAsync(funcionarioId, hoje, null, null, ct));
        return new TreinamentoFichaRhResposta(MontagemDaFicha.Resumo(lista, await pendencias.PendenciasAsync(funcionarioId, hoje, ct)), [.. lista.Select(MontagemDaFicha.ItemRh)]);
    }
}

/// <summary>
/// <c>GET /api/gestor/equipe/{funcionarioId}/treinamentos</c>: a mesma lista da ficha do RH, SEM certificado, de uma pessoa da equipe de hoje (ativa, admissão começada, do setor do gestor, sem o próprio gestor). Pessoa de outro setor, o próprio gestor, em admissão, inativa ou inexistente: 404 igual para todos. <c>resumo.pendentes</c> é o indicador <c>treinamentosPendentes</c> da ficha.
/// </summary>
public sealed class ListarTreinamentosDePessoaDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe equipe, IConsultaDeMeusTreinamentos linhas, IConsultaDaEquipeNosTreinamentos pendencias, IRelogio relogio)
{
    public async Task<TreinamentoFichaGestorResposta> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        _ = (await equipe.ObterFichaAsync(await escopo.ObterEquipeAsync(ct), funcionarioId, ct)).OuNaoEncontrado();
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var lista = MontagemDaFicha.Linhas(await linhas.ListarAsync(funcionarioId, hoje, null, null, ct));
        return new TreinamentoFichaGestorResposta(MontagemDaFicha.Resumo(lista, await pendencias.PendenciasAsync(funcionarioId, hoje, ct)), [.. lista.Select(MontagemDaFicha.ItemGestor)]);
    }
}

/// <summary>Os números da equipe por treinamento (compartilhados pelas duas rotas do gestor).</summary>
internal static class MontagemDaEquipe
{
    public static TreinamentoGestorResumo Resumo(TreinamentoDaEquipeLido t, DateOnly hoje) =>
        new(t.Id, t.Titulo, t.Obrigatorio, t.Onboarding, RegraDaSituacaoDoTreinamento.De(true, t.ComecaEm, t.EncerradoEmUtc, hoje), t.ComecaEm, t.TotalNaEquipe, t.Concluiram, t.Inscritos);

    public static async Task<IReadOnlyList<int>?> IdsDaEquipeAsync(IEscopoDoUsuario escopo, IConsultaDaEquipe equipe, CancellationToken ct)
    {
        var dele = await escopo.ObterEquipeAsync(ct);
        return dele is null ? null : [.. (await equipe.ListarAsync(dele, ct)).Itens.Select(i => i.Id)];
    }
}

/// <summary>
/// <c>GET /api/gestor/treinamentos</c>: os treinamentos PUBLICADOS (agendados, em andamento e encerrados; rascunho não) que alcançam pelo menos uma pessoa da equipe do gestor (sem ele), com os números da equipe; obrigatórios primeiro, depois <c>comecaEm</c> (o mais antigo primeiro), título e id. Sem paginação. Gestor sem setor: lista vazia.
/// </summary>
public sealed class ListarTreinamentosDaEquipeCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe equipe, IConsultaDaEquipeNosTreinamentos consulta, IRelogio relogio)
{
    public async Task<TreinamentoGestorLista> ExecutarAsync(CancellationToken ct)
    {
        var dele = await escopo.ObterEquipeAsync(ct);
        var ids = await MontagemDaEquipe.IdsDaEquipeAsync(escopo, equipe, ct);
        if (dele is null || ids is null)
        {
            return new TreinamentoGestorLista([]);
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var lidos = await consulta.TreinamentosDaEquipeAsync(ids, dele.SetorId, ct);
        return new TreinamentoGestorLista([.. lidos.OrderByDescending(t => t.Obrigatorio).ThenBy(t => t.ComecaEm is null).ThenBy(t => t.ComecaEm).ThenBy(t => t.Titulo, StringComparer.OrdinalIgnoreCase).ThenBy(t => t.Id).Select(t => MontagemDaEquipe.Resumo(t, hoje))]);
    }
}

/// <summary>
/// <c>GET /api/gestor/treinamentos/{treinamentoId}/equipe</c>: quem da equipe tem linha no treinamento (sem o gestor), por nome, sem paginação, sem nota nem certificado; <c>concluiram</c> e <c>faltamConcluir</c> (com linha e ainda não concluíram); <c>moduloAtual</c> e <c>emFeriasAte</c> (férias de hoje, lidas de <c>FERIAS</c>). 404: gestor sem setor, treinamento que não existe, rascunho ou que não alcança a equipe.
/// </summary>
public sealed class ListarPessoasDaEquipeNoTreinamentoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDaEquipe equipe, IConsultaDaEquipeNosTreinamentos consulta, IConsultaDeTreinamentos treinamentos, IFeriasDoPonto ferias, IRelogio relogio)
{
    public async Task<TreinamentoGestorEquipeResposta> ExecutarAsync(int treinamentoId, CancellationToken ct)
    {
        var dele = await escopo.ObterEquipeAsync(ct) ?? throw new NaoEncontradoException();
        var ids = await MontagemDaEquipe.IdsDaEquipeAsync(escopo, equipe, ct) ?? throw new NaoEncontradoException();
        var treinamento = (await treinamentos.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        if (!treinamento.Publicado)
        {
            throw new NaoEncontradoException();
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var resumo = (await consulta.TreinamentosDaEquipeAsync(ids, dele.SetorId, ct)).FirstOrDefault(t => t.Id == treinamentoId).OuNaoEncontrado();
        var feriasDeHoje = (await ferias.NoPeriodoAsync(hoje, hoje, ct)).GroupBy(f => f.FuncionarioId).ToDictionary(g => g.Key, g => g.Max(f => f.Fim));
        var pessoas = await consulta.PessoasDaEquipeAsync(treinamentoId, ids, ct);
        var itens = pessoas.Select(p =>
        {
            var progresso = ProgressoDoTreinamento.Calcular(p.Modulos);
            return new TreinamentoGestorPessoa(new FuncionarioResumo(p.FuncionarioId, p.Nome, p.Ativo, p.Cargo, p.Setor), p.Situacao, p.ConcluidoEmUtc, progresso.Concluidos, progresso.Total, progresso.ModuloAtual, feriasDeHoje.TryGetValue(p.FuncionarioId, out var ate) ? ate : null);
        }).ToList();
        return new TreinamentoGestorEquipeResposta(MontagemDaEquipe.Resumo(resumo, hoje), itens.Count(i => i.Situacao == SituacaoNoTreinamento.Concluido), itens.Count(i => i.Situacao != SituacaoNoTreinamento.Concluido), itens);
    }
}
