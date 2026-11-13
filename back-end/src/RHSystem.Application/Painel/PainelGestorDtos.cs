using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ferias;

namespace RHSystem.Application.Painel;

/// <summary>O setor do gestor (<c>PainelSetor</c>). Um só: um gestor responde por um setor.</summary>
public sealed record PainelSetorDto(int Id, string Nome);

/// <summary>Um pedido de ajuste de ponto que espera o gestor (<c>PainelAjusteDePontoPendente</c>): os mesmos dados de um item de <c>GET /api/gestor/ponto/pedidos-de-ajuste</c>; a decisão usa as rotas do ponto com o <c>pedidoId</c>.</summary>
public sealed record PainelAjusteDePontoPendenteDto(int PedidoId, FuncionarioResumo Funcionario, DateOnly Data, string ResumoDaCorrecao, string Justificativa, DateTime CriadoEmUtc);

/// <summary>"Esperando sua aprovação": as férias (os itens de <c>listarFeriasDaEquipe</c>, todos, sem paginação) e os ajustes de ponto na etapa do gestor.</summary>
public sealed record PainelGestorAprovacoesDto(IReadOnlyList<FeriasPedidoAnalise> Ferias, IReadOnlyList<PainelAjusteDePontoPendenteDto> AjustesDePonto);

/// <summary>O botão "Avaliar a equipe (ciclo ...)": o <c>ciclo</c> é o que vem depois de "ciclo " no título do formulário aberto (senão o título inteiro) e o <c>formularioId</c> abre a avaliação <c>GESTOR_AVALIA_EQUIPE</c> (o mesmo item "Sua parte" de <c>GET /api/eu/formularios</c>).</summary>
public sealed record PainelAvaliacaoDoCicloDto(string Ciclo, int FormularioId);

/// <summary>A coluna "Ponto hoje": a situação de hoje e, de apoio, o horário da primeira entrada (só <c>TRABALHANDO</c>, <c>HH:mm</c> em Brasília) e o último dia das férias (só <c>FERIAS</c>).</summary>
public sealed record PainelPontoHojeDto(SituacaoHoje SituacaoHoje, string? Horario, DateOnly? FeriasAte);

/// <summary>A coluna "Avaliação": <c>RESPONDIDA</c>, <c>PENDENTE</c> ou <c>NAO_SE_APLICA</c> (a pessoa não é avaliada pelo gestor naquele formulário). O nome do C# é o do contrato em maiúsculas com sublinhado.</summary>
public enum SituacaoDaAvaliacaoNoPainel
{
    Respondida,
    Pendente,
    NaoSeAplica,
}

/// <summary>Uma linha de "Pessoas da equipe". Sem CPF, endereço, holerite, documentos, dependente nem trajeto.</summary>
public sealed record PainelPessoaDaEquipeDto(
    FuncionarioResumo Funcionario,
    PainelPontoHojeDto PontoHoje,
    int BancoDeHorasMinutos,
    int FeriasDisponiveisDias,
    SituacaoDaAvaliacaoNoPainel? AvaliacaoSituacao,
    int Advertencias);

/// <summary>O painel da equipe do gestor (<c>PainelGestor</c>). Gestor sem setor: <c>setor</c> nulo, <c>totalPessoas</c> 0 e as listas vazias.</summary>
public sealed record PainelGestorDto(PainelSetorDto? Setor, int TotalPessoas, PainelGestorAprovacoesDto AguardandoAprovacao, PainelAvaliacaoDoCicloDto? Avaliacao, IReadOnlyList<PainelPessoaDaEquipeDto> Pessoas);
