using System.Text.RegularExpressions;

namespace RHSystem.Domain.Emails;

/// <summary>Texto no banco: <c>PENDENTE</c>, <c>EM_ENVIO</c>, <c>ENVIADO</c>, <c>FALHOU</c>, <c>DEVOLVIDO</c>, <c>CANCELADO</c>.</summary>
public enum SituacaoDoEmail
{
    Pendente,
    EmEnvio,
    Enviado,
    Falhou,
    Devolvido,
    Cancelado,
}

/// <summary>
/// Uma linha de <c>FILA_EMAILS</c>. A chave de idempotência é única e começa por <c>TIPO:</c> (ASCII): a mesma mensagem enfileirada duas
/// vezes dá a violação <c>1062</c>. <see cref="Criar"/> aplica as invariantes dos <c>CHECK</c> e recebe o instante de fora.
/// </summary>
public partial class EmailNaFila
{
    private EmailNaFila()
    {
    }

    public long Id { get; private set; }

    public string Tipo { get; private set; } = string.Empty;

    public string ChaveDeIdempotencia { get; private set; } = string.Empty;

    public int? ReferenciaId { get; private set; }

    public int? FuncionarioId { get; private set; }

    public string DestinatarioEmail { get; private set; } = string.Empty;

    /// <summary>Lembrete de formulário anônimo: não pode ter pessoa e a linha é apagada ao terminar.</summary>
    public bool Sigiloso { get; private set; }

    /// <summary>JSON da mensagem; só existe enquanto a mensagem espera (nula depois de enviada).</summary>
    public string? Carga { get; private set; }

    public SituacaoDoEmail Situacao { get; private set; }

    public int Tentativas { get; private set; }

    public DateTime ProximaTentativaEm { get; private set; }

    public DateTime? ReservadoAte { get; private set; }

    public string? UltimoErro { get; private set; }

    public DateTime CriadoEm { get; private set; }

    public DateTime? EnviadoEm { get; private set; }

    public static EmailNaFila Criar(
        string tipo,
        string chaveDeIdempotencia,
        string destinatarioEmail,
        DateTime agoraUtc,
        int? referenciaId = null,
        int? funcionarioId = null,
        bool sigiloso = false,
        string? carga = null)
    {
        // CHK_FILA_EMAILS_TIPO
        if (tipo is null || !NomeDeTipo().IsMatch(tipo))
        {
            throw new ArgumentException("O tipo é um nome em maiúsculas de 3 a 40 caracteres (por exemplo CONVITE).", nameof(tipo));
        }

        // CHK_FILA_EMAILS_CHAVE_COM_TIPO, e a coluna é ASCII (varchar(190))
        if (chaveDeIdempotencia is null || chaveDeIdempotencia.Length > 190 || !chaveDeIdempotencia.StartsWith(tipo + ":", StringComparison.Ordinal) || chaveDeIdempotencia.Any(c => c > 127))
        {
            throw new ArgumentException("A chave de idempotência é ASCII, tem até 190 caracteres e começa por \"TIPO:\".", nameof(chaveDeIdempotencia));
        }

        // CHK_FILA_EMAILS_DESTINATARIO: LIKE '%_@_%' (algo antes e algo depois do @)
        if (!TemArrobaComAlgoAoRedor(destinatarioEmail))
        {
            throw new ArgumentException("O destinatário é um e-mail (algo antes e algo depois do @).", nameof(destinatarioEmail));
        }

        // CHK_FILA_EMAILS_SIGILOSO_SEM_PESSOA
        if (sigiloso && funcionarioId is not null)
        {
            throw new ArgumentException("Uma mensagem sigilosa não aponta para uma pessoa.", nameof(funcionarioId));
        }

        return new EmailNaFila
        {
            Tipo = tipo,
            ChaveDeIdempotencia = chaveDeIdempotencia,
            ReferenciaId = referenciaId,
            FuncionarioId = funcionarioId,
            DestinatarioEmail = destinatarioEmail,
            Sigiloso = sigiloso,
            Carga = carga,
            Situacao = SituacaoDoEmail.Pendente,
            Tentativas = 0,
            ProximaTentativaEm = agoraUtc,
            CriadoEm = agoraUtc,
        };
    }

    /// <summary>O mesmo que <c>LIKE '%_@_%'</c>: um <c>@</c> com pelo menos um caractere antes e um depois.</summary>
    private static bool TemArrobaComAlgoAoRedor(string? valor)
    {
        if (valor is null)
        {
            return false;
        }

        for (var i = 1; i < valor.Length - 1; i++)
        {
            if (valor[i] == '@')
            {
                return true;
            }
        }

        return false;
    }

    [GeneratedRegex(@"^[A-Z][A-Z0-9_]{2,39}\z")]
    private static partial Regex NomeDeTipo();
}
