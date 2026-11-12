using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto;

/// <summary>A pessoa travada para gravar ponto: o que a conferência da aplicação precisa saber (o banco só barra pelo trigger de <c>PONTOS</c>, e num dia que já existe a batida passaria).</summary>
public sealed record PessoaDoPonto(bool Ativo, DateOnly DataAdmissao, DateOnly? DataDesligamento);

/// <summary>Uma batida lida do banco, com o instante em UTC.</summary>
public sealed record BatidaLida(int Id, DateTime InstanteUtc, TipoDeMarcacao Tipo, OrigemDaMarcacao Origem, DateTime? CorrigidaEmUtc);

/// <summary>O saldo do banco de horas: a soma dos minutos de cada linha (créditos menos débitos) e o último dia consolidado (<c>null</c> se a consolidação nunca rodou).</summary>
public sealed record SaldoDoBanco(int SaldoMinutos, DateOnly? AtualizadoAte);

public interface ISaldoDoBancoDeHoras
{
    /// <summary>Cada linha (<c>QUANTIDADE</c>, em horas com 2 casas) é convertida para minutos e SÓ DEPOIS somada; <c>FN_CALCULAR_SALDO_BANCO_HORAS</c> soma horas e erra até alguns minutos.</summary>
    Task<SaldoDoBanco> ObterAsync(int funcionarioId, CancellationToken ct);

    /// <summary>O saldo em minutos de várias pessoas de uma vez (a tela da equipe); quem não tem nenhuma linha tem 0.</summary>
    Task<IReadOnlyDictionary<int, int>> ObterSaldosAsync(IReadOnlyCollection<int> funcionarioIds, CancellationToken ct);
}

/// <summary>
/// O ponto no banco. A gravação de batida acontece dentro de uma transação <c>READ COMMITTED</c> em que a pessoa já foi travada (<see cref="TravarPessoaAsync"/>, <c>FOR UPDATE</c> em <c>FUNCIONARIOS</c>): é o que serializa o duplo clique e as duas abas,
/// porque a chave única só pega o MESMO segundo.
/// </summary>
public interface IPontoRepositorio
{
    /// <summary><c>null</c> se a pessoa não existe. Só dentro de uma transação (a trava dura até ela terminar).</summary>
    Task<PessoaDoPonto?> TravarPessoaAsync(int funcionarioId, CancellationToken ct);

    /// <summary>A carga semanal do cargo da pessoa, em horas; <c>null</c> se ela não tem cargo ou o cargo não tem carga.</summary>
    Task<decimal?> CargaSemanalAsync(int funcionarioId, CancellationToken ct);

    /// <summary>As batidas do dia do ponto, por horário e depois por id.</summary>
    Task<IReadOnlyList<BatidaLida>> BatidasDoDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct);

    /// <summary>
    /// A batida que a PRÓPRIA pessoa bate (<c>registrarMarcacaoDePonto</c>): cria o dia do ponto se faltar e grava a batida (instante cortado ao segundo); devolve o id. A recusa do banco sai traduzida
    /// (<c>ProblemaDeApiException</c>, com os textos desta rota).
    /// </summary>
    Task<int> GravarBatidaAsync(int funcionarioId, DateOnly dia, DateTime instanteUtc, TipoDeMarcacao tipo, CancellationToken ct);

    /// <summary>
    /// A correção direta do RH: apaga as <paramref name="removidas"/>, corrige as existentes que mudaram e inclui as novas (todas <c>CORRIGIDA_RH</c>, com a data da correção), criando o dia se faltar. As remoções vão primeiro (uma nova no horário de uma
    /// apagada não bate na chave única). A recusa do banco sai traduzida: batida no mesmo segundo (409) e batida ligada a um pedido (409 <c>PONTO_MARCACAO_EM_USO</c>).
    /// </summary>
    Task CorrigirDiaAsync(int funcionarioId, DateOnly dia, IReadOnlyList<RHSystem.Application.Ponto.Regras.BatidaDepoisDeAplicar> batidas, IReadOnlyList<int> removidas, DateTime agoraUtc, CancellationToken ct);
}
