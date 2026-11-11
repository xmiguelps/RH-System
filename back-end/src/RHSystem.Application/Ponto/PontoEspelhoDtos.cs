using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Application.Ponto;

// Os tipos do espelho do mês e do ponto da equipe (contrato/fontes/30-fatia-3-ponto.yaml, components/schemas). Os enums saem em MAIÚSCULAS_COM_SUBLINHADO (AJUSTE_EM_ANALISE, TERCA).

/// <summary>Um dia do espelho (visão do gestor e do RH).</summary>
public sealed record PontoEspelhoDia(
    DateOnly Data,
    DiaDaSemanaDoPonto DiaDaSemana,
    bool EhHoje,
    SituacaoDoDiaNoPonto Situacao,
    string? NomeFeriado,
    IReadOnlyList<PontoPeriodo> Periodos,
    int? HorasMinutos,
    int CargaMinutos,
    int? SaldoMinutos,
    int? AtrasoMinutos,
    int? PedidoEmAnaliseId);

/// <summary>Um dia do espelho da própria pessoa: os mesmos campos mais a permissão do botão "Pedir ajuste".</summary>
public sealed record PontoEspelhoDiaProprio(
    DateOnly Data,
    DiaDaSemanaDoPonto DiaDaSemana,
    bool EhHoje,
    SituacaoDoDiaNoPonto Situacao,
    string? NomeFeriado,
    IReadOnlyList<PontoPeriodo> Periodos,
    int? HorasMinutos,
    int CargaMinutos,
    int? SaldoMinutos,
    int? AtrasoMinutos,
    int? PedidoEmAnaliseId,
    bool PodePedirAjuste);

/// <summary>Os cartões do topo do espelho.</summary>
public sealed record PontoResumoMes(int HorasTrabalhadasNoMesMinutos, int SaldoBancoHorasMinutos, int PedidosEmAnalise, int Atrasos, int Faltas);

/// <summary>O espelho do mês de uma pessoa (visão do gestor e do RH).</summary>
public sealed record PontoEspelho(FuncionarioResumo Funcionario, int Ano, int Mes, PontoJornada Jornada, PontoResumoMes Resumo, IReadOnlyList<PontoEspelhoDia> Dias);

/// <summary>O espelho do mês da própria pessoa.</summary>
public sealed record PontoEspelhoProprio(int Ano, int Mes, PontoJornada Jornada, PontoResumoMes Resumo, IReadOnlyList<PontoEspelhoDiaProprio> Dias);

/// <summary>As pessoas de férias no dia consultado e o último dia das férias em curso.</summary>
public sealed record PontoEquipeFerias(FuncionarioResumo Funcionario, DateOnly Ate);

/// <summary>Os quatro cartões da tela "Ponto da equipe". <paramref name="ContagensHoje"/> só existe para hoje (<c>null</c> em dia passado).</summary>
public sealed record PontoEquipeResumo(PresencaContagens? ContagensHoje, bool DiaUtil, IReadOnlyList<FuncionarioResumo> PessoasSemRegistro, IReadOnlyList<PontoEquipeFerias> PessoasDeFerias, int PedidosParaAnalisar);

/// <summary>Uma linha de "Hoje no setor". <paramref name="SituacaoHoje"/> é <c>null</c> em dia passado (o chip vem de <paramref name="SituacaoDoDia"/>).</summary>
public sealed record PontoEquipeItem(
    FuncionarioResumo Funcionario,
    IReadOnlyList<PontoPeriodo> Periodos,
    int? HorasAteAgoraMinutos,
    int SaldoBancoHorasMinutos,
    SituacaoHoje? SituacaoHoje,
    SituacaoDoDiaNoPonto SituacaoDoDia,
    bool PeriodoAberto);

/// <summary>A tela "Ponto da equipe" de um dia.</summary>
public sealed record PontoEquipeDia(DateOnly Data, PontoEquipeResumo Resumo, IReadOnlyList<PontoEquipeItem> Itens);
