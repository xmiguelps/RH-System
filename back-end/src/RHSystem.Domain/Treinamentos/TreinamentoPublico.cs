namespace RHSystem.Domain.Treinamentos;

/// <summary>Quem o treinamento alcança (<c>TREINAMENTOS_PUBLICO</c>): uma linha por setor <b>ou</b> por pessoa (<c>CHK_TREINAMENTOS_PUBLICO_SETOR_OU_PESSOA</c>); <c>TODOS</c> não guarda linha. <c>CHAVE_DESTINO</c> é gerada pelo banco e não é mapeada.</summary>
public class TreinamentoPublico
{
    private TreinamentoPublico()
    {
    }

    public int Id { get; private set; }

    public int TreinamentoId { get; private set; }

    public int? SetorId { get; private set; }

    public int? FuncionarioId { get; private set; }

    public static TreinamentoPublico DoSetor(int treinamentoId, int setorId) => new() { TreinamentoId = treinamentoId, SetorId = setorId };

    public static TreinamentoPublico DaPessoa(int treinamentoId, int funcionarioId) => new() { TreinamentoId = treinamentoId, FuncionarioId = funcionarioId };
}
