using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Acompanhar;

/// <summary>O treinamento aberto em "Quem fez" (<c>TreinamentoParticipantesCabecalho</c>).</summary>
public sealed record TreinamentoParticipantesCabecalho(int Id, string Titulo, bool Obrigatorio, bool Onboarding, bool EmiteCertificado, SituacaoDoTreinamento Situacao, DateOnly? ComecaEm, DateTime? EncerradoEmUtc, PublicoDoTreinamento? Publico, int TotalModulos);

/// <summary>Os números do topo de "Quem fez" (<c>TreinamentoParticipantesContagens</c>): <c>NaoIniciados</c> são as linhas <c>INSCRITO</c>.</summary>
public sealed record TreinamentoParticipantesContagens(int Total, int Concluidos, int EmAndamento, int NaoIniciados);

/// <summary>Uma linha de "Quem fez" (<c>TreinamentoParticipante</c>). <c>ConcluidoEmUtc</c> é o instante do último módulo concluído (sem progresso: o dia de <c>DATA_CONCLUSAO</c> às 00:00 em -03:00); <c>CertificadoDisponivel</c> só com a linha <c>CONCLUIDO</c> num treinamento que emite.</summary>
public sealed record TreinamentoParticipante(FuncionarioResumo Funcionario, SituacaoNoTreinamento Situacao, DateOnly? IniciadoEm, DateTime? ConcluidoEmUtc, int ModulosConcluidos, bool CertificadoDisponivel);

/// <summary>A página de "Quem fez" (<c>TreinamentoParticipantesLista</c>).</summary>
public sealed record TreinamentoParticipantesLista(TreinamentoParticipantesCabecalho Treinamento, TreinamentoParticipantesContagens Contagens, IReadOnlyList<TreinamentoParticipante> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas);

/// <summary>Os filtros de "Quem fez" (aba, setor); a busca (no nome) vem em <see cref="ParametrosDeLista"/>.</summary>
public sealed record FiltroDeParticipantes(SituacaoNoTreinamento? Situacao, int? SetorId);

/// <summary>A leitura de quem tem linha num treinamento: só <c>INSCRITO</c>, <c>EM_ANDAMENTO</c> e <c>CONCLUIDO</c> (inativos continuam; <c>REPROVADO</c> e <c>CANCELADO</c> nunca aparecem).</summary>
public interface IConsultaDeParticipantes
{
    /// <summary>
    /// A página (<paramref name="paginar"/> falso devolve TODAS as linhas, para o CSV), o total já filtrado e as contagens, que IGNORAM a aba e respeitam o setor e a busca. <c>ordenarPor</c> (<c>NOME</c>, <c>SETOR</c>, <c>SITUACAO</c> na ordem lógica não iniciado, em andamento, concluído, <c>INICIO</c>, <c>CONCLUSAO</c>) e <c>direcao</c> vêm em <paramref name="parametros"/>;
    /// o nulo de <c>INICIO</c> e <c>CONCLUSAO</c> fica por último nos dois sentidos; o empate é sempre pelo id da linha.
    /// </summary>
    Task<(IReadOnlyList<TreinamentoParticipante> Itens, int Total, TreinamentoParticipantesContagens Contagens)> ListarAsync(int treinamentoId, bool emiteCertificado, FiltroDeParticipantes filtro, ParametrosDeLista parametros, bool paginar, CancellationToken ct);
}
