using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Empresa;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>
/// A tela "Ponto da equipe" de um dia (hoje, ou um dia passado: nunca futuro, senão 400). A equipe é a de HOJE (ativos do setor, sem o gestor); quem ainda não tinha sido admitido no dia consultado fica de fora. Cada linha usa a mesma conta do espelho
/// (<see cref="ConstrutorDoDiaDoEspelho"/>) para <c>situacaoDoDia</c>; <c>situacaoHoje</c> (e <c>contagensHoje</c>) só existem para hoje e vêm do mesmo <see cref="ISituacaoDeHoje"/> da lista da equipe. Gestor sem setor: lista vazia, sem erro.
/// </summary>
public sealed class MontadorDoPontoDaEquipe(
    IConsultaDoPonto consulta,
    ICalendarioDoPonto calendario,
    IFeriasDoPonto ferias,
    ISaldoDoBancoDeHoras saldo,
    ISituacaoDeHoje situacaoDeHoje,
    IConfiguracaoDaEmpresa empresa,
    IRelogio relogio)
{
    public async Task<PontoEquipeDia> MontarAsync(EquipeDoGestor? equipe, DateOnly? data, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var dia = data ?? hoje;
        if (dia > hoje)
        {
            throw new ErroDeValidacaoException("data", "A data não pode ser futura.");
        }

        var ehHoje = dia == hoje;
        var doCalendario = await calendario.DiaAsync(dia, ct);
        IReadOnlyList<PessoaDoEspelho> pessoas = equipe is null ? [] : [.. (await consulta.PessoasDaEquipeAsync(equipe, ct)).Where(p => p.DataAdmissao <= dia)];
        var ids = pessoas.Select(p => p.Id).ToList();

        var configuracao = await empresa.ObterAsync(ct);
        var batidas = (await consulta.BatidasAsync(ids, dia, dia, ct)).ToLookup(b => b.FuncionarioId, b => b.Batida);
        var atestados = await consulta.AtestadosValidadosAsync(ids, dia, dia, ct);
        var deFerias = (await ferias.NoPeriodoAsync(dia, dia, ct)).Where(f => ids.Contains(f.FuncionarioId)).ToList();
        var pendentes = await consulta.PedidosPendentesAsync(ids, ct);
        var saldos = await saldo.ObterSaldosAsync(ids, ct);
        var situacoesDeHoje = ehHoje ? (await situacaoDeHoje.ObterAsync(ids, ct)).Situacoes : null;

        var itens = new List<PontoEquipeItem>();
        foreach (var pessoa in pessoas)
        {
            var doDia = batidas[pessoa.Id].ToList();
            var fatosDoDia = ConstrutorDoDiaDoEspelho.Construir(new DadosDoDiaDoEspelho(
                dia,
                hoje,
                doDia,
                doCalendario.NomeDoFeriado,
                deFerias.Any(f => f.FuncionarioId == pessoa.Id),
                atestados.Any(a => a.FuncionarioId == pessoa.Id && a.HoraInicio is null && a.Inicio <= dia && dia <= a.Fim),
                MontadorDoEspelho.MinutosCobertosPorAtestadoDeHoras([.. atestados.Where(a => a.FuncionarioId == pessoa.Id)], dia),
                pendentes.Where(p => p.FuncionarioId == pessoa.Id && p.Dia == dia).Select(p => (int?)p.PedidoId).FirstOrDefault(),
                MontadorDoCartaoDeHoje.Jornada(pessoa.CargaSemanalHoras, configuracao).CargaDiariaMinutos ?? 0,
                configuracao?.HorarioEntrada,
                configuracao?.ToleranciaAtrasoMinutos ?? 0));

            var marcacoes = doDia.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo)).OrderBy(m => m.InstanteUtc).ThenBy(m => m.Id).ToList();
            itens.Add(new PontoEquipeItem(
                new FuncionarioResumo(pessoa.Id, pessoa.Nome, pessoa.Ativo, pessoa.Cargo, pessoa.Setor),
                fatosDoDia.Periodos,
                marcacoes.Count == 0 ? null : CalculoDoDia.HorasAteAgoraEmMinutos(SequenciaDoDia.Periodos(marcacoes), agora, ehHoje),
                saldos.GetValueOrDefault(pessoa.Id),
                situacoesDeHoje?[pessoa.Id],
                fatosDoDia.Situacao,
                marcacoes.Count > 0 && marcacoes[^1].Tipo == TipoDeMarcacao.Entrada));
        }

        bool Esta(PontoEquipeItem item, SituacaoHoje deHoje, SituacaoDoDiaNoPonto doDia) => ehHoje ? item.SituacaoHoje == deHoje : item.SituacaoDoDia == doDia;

        var resumo = new PontoEquipeResumo(
            ehHoje ? PresencaContagens.De(itens.Select(i => i.SituacaoHoje!.Value)) : null,
            doCalendario.EhDiaUtil,
            [.. itens.Where(i => Esta(i, SituacaoHoje.SemRegistro, SituacaoDoDiaNoPonto.SemRegistro)).Select(i => i.Funcionario)],
            [.. itens.Where(i => Esta(i, SituacaoHoje.Ferias, SituacaoDoDiaNoPonto.Ferias)).Select(i => new PontoEquipeFerias(i.Funcionario, deFerias.Where(f => f.FuncionarioId == i.Funcionario.Id).Max(f => f.Fim)))],
            equipe is null ? 0 : pendentes.Count(p => p.NaEtapaDoGestor && p.GestorId == equipe.GestorId));
        return new PontoEquipeDia(dia, resumo, itens);
    }
}
