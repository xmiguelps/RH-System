using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>
/// O treinamento e a linha da PESSOA nele, lidos nesta ordem (primeiro o treinamento, depois a linha dela): <c>TemLinha</c> diz se existe linha (mesmo <c>REPROVADO</c> ou de situação nula) e <c>Situacao</c> é a dela; <c>DoMeuPublico</c> diz se ela é do público do treinamento hoje (<c>TODOS</c>, o setor dela ou ela entre as pessoas escolhidas).
/// </summary>
public sealed record TreinamentoParaParticipar(int Id, bool Publicado, DateOnly? ComecaEm, bool Obrigatorio, DateTime? EncerradoEmUtc, bool DoMeuPublico, bool TemLinha, SituacaoNoTreinamento? Situacao);

/// <summary>A escrita da participação da pessoa logada (inscrever e iniciar): só a PRÓPRIA linha, sem transação do chamador (cada operação é uma instrução).</summary>
public interface IMinhaParticipacaoRepositorio
{
    /// <summary>O treinamento e a linha da pessoa; <c>null</c> se o treinamento não existe.</summary>
    Task<TreinamentoParaParticipar?> LerAsync(int funcionarioId, int treinamentoId, CancellationToken ct);

    /// <summary>Cria a linha <c>INSCRITO</c> da pessoa (datas vazias). <c>false</c> = a linha já existia (duas telas ao mesmo tempo: a chave única recusou); o banco recusa inativo (a recusa sobe traduzida).</summary>
    Task<bool> InscreverAsync(int funcionarioId, int treinamentoId, CancellationToken ct);

    /// <summary>
    /// <c>INSCRITO</c> para <c>EM_ANDAMENTO</c> com <c>DATA_INICIO</c> = <paramref name="hoje"/> (do relógio da aplicação), por <c>UPDATE</c> condicional: só vale se a linha ainda é <c>INSCRITO</c> e o treinamento ainda não foi encerrado. <c>false</c> = nada mudou (outra tela já iniciou, ou o RH encerrou antes).
    /// </summary>
    Task<bool> IniciarAsync(int funcionarioId, int treinamentoId, DateOnly hoje, CancellationToken ct);
}
