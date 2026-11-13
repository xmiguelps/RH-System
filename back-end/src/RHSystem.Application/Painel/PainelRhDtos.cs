using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Painel;

/// <summary>
/// O tipo de pendência do RH e o que o <c>referenciaId</c> aponta (<c>PainelPendenciaTipo</c>): o pedido de ajuste ou de férias na etapa do RH, o atestado <c>ENVIADO</c>, a pessoa em admissão (o próprio <c>funcionarioId</c>) e a adesão com pedido <c>PENDENTE</c> (da adesão, de dependente ou de trajeto). A ordem do enum é a dos cartões.
/// </summary>
public enum PainelPendenciaTipo
{
    AjusteDePonto,
    Ferias,
    Atestado,
    AdmissaoComDocumentosPendentes,
    Beneficios,
}

/// <summary>Um cartão de "Precisa da sua atenção": a quantidade do tipo e o item MAIS ANTIGO (o que o <c>resumo</c> cita e o "Revisar" abre).</summary>
public sealed record PainelPendenciaResumoDto(PainelPendenciaTipo Tipo, int Quantidade, string? Resumo, int FuncionarioId, int ReferenciaId);

/// <summary>Uma pendência, uma a uma (<c>PainelPendenciaItem</c>). <c>DesdeEmUtc</c> é desde quando espera o RH; na admissão é a data de admissão às 00:00 em -03:00 (não há instante guardado: ela só ORDENA a fila).</summary>
public sealed record PainelPendenciaItemDto(PainelPendenciaTipo Tipo, int ReferenciaId, FuncionarioResumo Funcionario, string Descricao, DateTime DesdeEmUtc);

public enum PainelTipoDeProximoDia
{
    Feriado,
    Aniversario,
}

/// <summary>Uma linha de "Próximos dias": o feriado (com o nome dele) ou o aniversário ("Nome, Setor"); o <c>Rotulo</c> é o texto pequeno acima do título.</summary>
public sealed record PainelProximoDiaDto(DateOnly Data, PainelTipoDeProximoDia Tipo, string Rotulo, string Titulo);

/// <summary>Um treinamento em andamento ("18 de 24 concluíram"): <c>Total</c> é o número de participantes da lista de treinamentos do RH.</summary>
public sealed record PainelTreinamentoAndamentoDto(int TreinamentoId, string Titulo, int Concluiram, int Total);

/// <summary>O cartão "Presença agora": a contagem por situação de hoje (a base <see cref="PresencaContagens"/>) e o <c>total</c> sem os "sem expediente".</summary>
public sealed record PainelPresencaAgoraDto(DateTime AtualizadoEmUtc, bool DiaUtil, int Total, PresencaContagens ContagensHoje);

/// <summary>Uma linha de "Últimas alterações" (do log de auditoria): <c>Autor</c> já em texto ("Ana Souza (RH)", "Carlos Ribeiro (gestor)" ou "Sistema").</summary>
public sealed record PainelAlteracaoDto(DateTime QuandoUtc, string Autor, string Descricao);

/// <summary>O painel inicial do RH (<c>PainelRh</c>).</summary>
public sealed record PainelRhDto(
    int TotalPendencias,
    IReadOnlyList<PainelPendenciaResumoDto> Pendencias,
    IReadOnlyList<PainelProximoDiaDto> ProximosDias,
    IReadOnlyList<PainelTreinamentoAndamentoDto> TreinamentosEmAndamento,
    PainelPresencaAgoraDto PresencaAgora,
    IReadOnlyList<PainelAlteracaoDto> UltimasAlteracoes);

/// <summary>Uma pessoa sem registro de ponto hoje: a ficha curta e a situação (sempre <c>SEM_REGISTRO</c> nesta lista), no mesmo nível (<c>FuncionarioResumo</c> mais <c>situacaoHoje</c>).</summary>
public sealed record PainelSemRegistroItemDto(int Id, string Nome, bool Ativo, SituacaoHoje SituacaoHoje, string? Cargo = null, string? Setor = null);
