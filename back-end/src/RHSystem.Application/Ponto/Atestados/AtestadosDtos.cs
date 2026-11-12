using RHSystem.Application.Documentos;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Atestados;

/// <summary>
/// Os limites do envio de atestado (<c>enviarAtestado</c>): 5 MiB (escolha do contrato; os documentos têm 10 MiB), PDF, JPG ou PNG pelos primeiros bytes. O teto da requisição inteira é o dos documentos (16 MiB): entre 5 e 16 MiB a API lê o arquivo e devolve o
/// 400 em <c>arquivo</c> que o contrato manda, em vez do 413 do servidor.
/// </summary>
public static class LimitesDeAtestado
{
    public const long TamanhoMaximoBytes = 5L * 1024 * 1024;

    public const long TetoDaRequisicaoBytes = 16L * 1024 * 1024;

    /// <summary>A pasta dos atestados no armazenamento (e, com a barra, o prefixo da <c>IFonteDeReferenciasDeArquivo</c> deles).</summary>
    public const string PastaNoArmazenamento = "atestados";
}

/// <summary>Um atestado como o contrato o mostra (<c>Atestado</c>). Nunca leva o caminho do arquivo: o download é por link temporário.</summary>
public sealed record PontoAtestado(
    int Id,
    FuncionarioResumo Funcionario,
    DateOnly DataInicio,
    DateOnly DataFim,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    SituacaoDoAtestado Situacao,
    string? NomeArquivo,
    DateTime EnviadoEmUtc,
    DateTime? DecididoEmUtc,
    string? DecididoPorNome,
    string? MotivoRecusa);

/// <summary>O corpo de <c>recusarAtestado</c>. O motivo é texto de 1 a 500 caracteres (o banco exige que não seja vazio); o contrato o dá como obrigatório e sem <c>null</c>, e o caso de uso devolve o 400 em <c>motivo</c> também quando ele vem ausente.</summary>
public sealed record PontoAtestadoRecusar(string Motivo);

/// <summary>O formulário <c>multipart/form-data</c> do envio, como chegou: os campos de texto sem conferir e o arquivo (com a contagem de TODAS as partes de arquivo, de qualquer nome de campo).</summary>
public sealed record NovoAtestado(string? DataInicio, string? DataFim, string? HoraInicio, string? HoraFim, ArquivoRecebido? Arquivo, int ArquivosRecebidos);

/// <summary>Um atestado lido do banco para a consulta: o que o contrato mostra mais o caminho do arquivo (que só o download usa).</summary>
public sealed record AtestadoLido(
    int Id,
    FuncionarioResumo Funcionario,
    DateOnly DataInicio,
    DateOnly DataFim,
    TimeOnly? HoraInicio,
    TimeOnly? HoraFim,
    SituacaoDoAtestado Situacao,
    string? NomeArquivo,
    string CaminhoArquivo,
    DateTime EnviadoEmUtc,
    DateTime? DecididoEmUtc,
    string? DecididoPorNome,
    string? MotivoRecusa);

/// <summary>O escopo da consulta: a pessoa vê os dela, o gestor os da equipe dele (nenhum se não tem equipe) e o RH todos.</summary>
public sealed record EscopoDeAtestados(PapelNaConsulta Papel, int FuncionarioId, EquipeDoGestor? Equipe);

/// <summary>Os filtros das listas: <c>situacao</c> e <c>funcionarioId</c> (o gestor só enxerga a equipe dele). A <c>busca</c> no nome da pessoa (só o RH) vai em <see cref="Comum.Consultas.ParametrosDeLista"/>.</summary>
public sealed record FiltroDeAtestados(SituacaoDoAtestado? Situacao, int? FuncionarioId);

/// <summary>Uma página de atestados e o total do escopo com os filtros (para <c>totalPaginas</c>).</summary>
public sealed record PaginaDeAtestados(IReadOnlyList<AtestadoLido> Itens, int Total);
