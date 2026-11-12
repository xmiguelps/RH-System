using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Holerites;

/// <summary>A gravação dos holerites. O banco nunca apaga holerite: a correção é uma versão nova.</summary>
public interface IHoleritesRepositorio
{
    /// <summary>Grava e devolve o id. O que o banco recusa (a chave única da competência, tipo e versão, ou uma regra de trigger) sai como o erro do contrato (<c>ProblemaDeApiException</c>).</summary>
    Task<int> AdicionarAsync(Holerite holerite, OperacaoDeHolerite operacao, CancellationToken ct);
}

/// <summary>A leitura dos holerites para o envio e a nova versão.</summary>
public interface IConsultaDeHolerites
{
    /// <summary><c>null</c> se a pessoa não existe (inclusive inativa: o RH envia e lista holerite de inativo).</summary>
    Task<PessoaDoHolerite?> ObterPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>A maior versão da competência e do tipo da pessoa, ou <c>null</c> se não há holerite.</summary>
    Task<int?> MaiorVersaoAsync(int funcionarioId, int ano, int mes, TipoDeHolerite tipo, CancellationToken ct);

    Task<HoleriteLido?> ObterAsync(int holeriteId, CancellationToken ct);
}

/// <summary>A tarefa que manda o e-mail do holerite na hora em que ele fica visível (<c>PUBLICAR_HOLERITE:holerite={id}</c>; a data de um holerite nunca muda, então a chave não leva o instante).</summary>
public interface IAgendamentoDoHolerite
{
    Task AgendarAsync(int holeriteId, DateTime publicacaoEmUtc, CancellationToken ct);
}
