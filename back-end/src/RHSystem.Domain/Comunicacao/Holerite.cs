namespace RHSystem.Domain.Comunicacao;

/// <summary>
/// Um holerite (<c>HOLERITES</c>): o PDF de uma competência (mês e ano) e um tipo, numa <b>versão</b> (a chave única é funcionário, ano, mês, tipo e versão; o banco nunca apaga holerite, a correção é uma versão nova).
/// <c>DataPublicacao</c> no futuro é o holerite agendado (a pessoa ainda não vê). O caminho do arquivo é segredo de armazenamento: nunca sai da API nem entra em log ou auditoria. A data de publicação vem do relógio da
/// aplicação (o trigger usaria o <c>NOW()</c> real).
/// </summary>
public class Holerite
{
    private Holerite()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    /// <summary>1 a 12 (<c>CHK_HOLERITES_MES</c>).</summary>
    public int Mes { get; private set; }

    /// <summary>A coluna é <c>YEAR</c> (1901 a 2155); o contrato aceita 2000 a 2100.</summary>
    public int Ano { get; private set; }

    public DateTime? PublicacaoEmUtc { get; private set; }

    public string? CaminhoArquivo { get; private set; }

    public TipoDeHolerite Tipo { get; private set; }

    public int Versao { get; private set; }

    public static Holerite Criar(int funcionarioId, int mes, int ano, TipoDeHolerite tipo, int versao, DateTime publicacaoEmUtc, string caminhoArquivo) =>
        new()
        {
            FuncionarioId = funcionarioId,
            Mes = mes,
            Ano = ano,
            Tipo = tipo,
            Versao = versao,
            PublicacaoEmUtc = CamposDeTexto.AoSegundo(publicacaoEmUtc),
            CaminhoArquivo = caminhoArquivo,
        };
}
