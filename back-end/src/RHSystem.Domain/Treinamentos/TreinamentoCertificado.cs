namespace RHSystem.Domain.Treinamentos;

/// <summary>
/// A linha do certificado (<c>TREINAMENTOS_CERTIFICADOS</c>). <b>Nunca é inserida pela aplicação</b>: o trigger a cria quando a linha da pessoa vira <c>CONCLUIDO</c>, com <c>UUID()</c> (versão 1) e <c>CURDATE()</c>; a aplicação só a LÊ e a ATUALIZA (troca o código por um UUID v4 e a data pelo relógio dela, na mesma transação da conclusão, e guarda o caminho do PDF).
/// O número que a API mostra é <c>CERT-{ano}-{id}</c>.
/// </summary>
public class TreinamentoCertificado
{
    private TreinamentoCertificado()
    {
    }

    public int Id { get; private set; }

    public int TreinamentoId { get; private set; }

    public int FuncionarioId { get; private set; }

    public DateOnly? EmitidoEm { get; private set; }

    public string? CodigoValidacao { get; private set; }

    public string? CaminhoArquivo { get; private set; }
}
