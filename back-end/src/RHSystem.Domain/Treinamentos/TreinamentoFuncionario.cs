namespace RHSystem.Domain.Treinamentos;

/// <summary>
/// A linha da pessoa no treinamento (<c>TREINAMENTOS_FUNCIONARIOS</c>), uma por (treinamento, pessoa). <c>STATUS_TREINAMENTO</c> aceita <c>NULL</c> no banco, mas a aplicação sempre o grava. As datas são DIAS (<c>DATE</c>) em -03:00 e saem do relógio da aplicação (o trigger usaria o <c>CURDATE()</c> real). Uma linha <c>CONCLUIDO</c> não muda mais (trigger) e
/// ao virar <c>CONCLUIDO</c> o banco cria a linha do certificado. <c>NOTA</c> não é mapeada: a API nunca grava nota.
/// </summary>
public class TreinamentoFuncionario
{
    private TreinamentoFuncionario()
    {
    }

    public int Id { get; private set; }

    public int TreinamentoId { get; private set; }

    public int FuncionarioId { get; private set; }

    public SituacaoNoTreinamento? Situacao { get; private set; }

    public DateOnly? IniciadoEm { get; private set; }

    public DateOnly? ConcluidoEm { get; private set; }

    public static TreinamentoFuncionario Criar(int treinamentoId, int funcionarioId, SituacaoNoTreinamento situacao, DateOnly? iniciadoEm = null, DateOnly? concluidoEm = null) =>
        new() { TreinamentoId = treinamentoId, FuncionarioId = funcionarioId, Situacao = situacao, IniciadoEm = iniciadoEm, ConcluidoEm = concluidoEm };

    public void Concluir(DateOnly dia)
    {
        Situacao = SituacaoNoTreinamento.Concluido;
        ConcluidoEm = dia;
    }
}
