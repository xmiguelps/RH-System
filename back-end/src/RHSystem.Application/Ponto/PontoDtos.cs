using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

// Os tipos do cartão "Seu ponto hoje" (contrato/fontes/30-fatia-3-ponto.yaml, components/schemas). O JSON é camelCase e os enums saem em MAIÚSCULAS_COM_SUBLINHADO (ENTRADA, SAIDA, SISTEMA, CORRIGIDA_RH).

/// <summary>Uma batida de ponto. <paramref name="Ordem"/> é a posição no dia, por horário (a primeira é 1); <paramref name="Rotulo"/> vem da posição e do tipo.</summary>
public sealed record PontoMarcacao(int Id, int Ordem, TipoDeMarcacao Tipo, string Rotulo, DateTime InstanteUtc, OrigemDaMarcacao Origem, DateTime? CorrigidaEmUtc);

/// <summary>Uma ENTRADA e a SAÍDA seguinte; os dois lados podem faltar (entrada sem saída, saída sem entrada).</summary>
public sealed record PontoPeriodo(PontoMarcacao? Entrada, PontoMarcacao? Saida);

/// <summary>A batida que o botão do cartão vai fazer agora.</summary>
public sealed record PontoProximaMarcacao(TipoDeMarcacao Tipo, string Rotulo, string TextoDoBotao);

/// <summary>A jornada da pessoa: a carga do cargo e o horário padrão da empresa. Cada grupo é <c>null</c> por inteiro quando o cargo não tem carga ou a empresa não configurou o horário.</summary>
public sealed record PontoJornada(int? CargaSemanalMinutos, int? CargaDiariaMinutos, string? HorarioEntrada, string? HorarioSaida, int? ToleranciaAtrasoMinutos);

/// <summary>O saldo do banco de horas (soma dos minutos de cada linha) e o último dia consolidado.</summary>
public sealed record PontoBancoHoras(int SaldoMinutos, DateOnly? AtualizadoAte);

/// <summary>O cartão: as batidas do dia do ponto (hoje, ou ontem na jornada que atravessa a meia-noite), a próxima batida, a jornada e o banco de horas.</summary>
public sealed record PontoHoje(DateOnly DataDoPonto, DateTime AgoraUtc, PontoJornada Jornada, IReadOnlyList<PontoPeriodo> Periodos, PontoProximaMarcacao ProximaMarcacao, PontoBancoHoras BancoHoras);

/// <summary>O corpo de <c>POST /api/eu/ponto/marcacoes</c>: só diz que batida a pessoa acha que está fazendo; a hora é do servidor.</summary>
public sealed record PontoMarcacaoRegistrar
{
    /// <summary><c>required</c>: sem o campo o corpo nem é lido (400). Um enum sem <c>required</c> viraria ENTRADA em silêncio.</summary>
    public required TipoDeMarcacao TipoEsperado { get; init; }
}
