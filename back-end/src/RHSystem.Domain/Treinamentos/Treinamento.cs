namespace RHSystem.Domain.Treinamentos;

/// <summary>
/// Um treinamento (<c>TREINAMENTOS</c>). Nasce rascunho (<c>PUBLICADO</c> falso); a situação (rascunho, agendado, em andamento, encerrado) é CALCULADA pelas datas, sem agendador. <c>TIPO_TREINAMENTO</c> é <c>'ONBOARDING'</c> ou <c>NULL</c>. <c>EMITE_CERTIFICADO</c> e <c>INCLUI_ADMITIDOS</c> têm padrão
/// <c>TRUE</c> no banco e por isso a aplicação os grava SEMPRE (o "falso" também). <c>DATA_FIM</c>, <c>AVISA_POR_EMAIL</c> e <c>LEMBRA_ANTES_DO_FIM</c> não são mapeadas (sem uso: sem prazo, sem lembrete, sem e-mail). A entidade não é auditada pelo interceptor (publicar cria linhas em lote; o evento é à mão).
/// </summary>
public class Treinamento
{
    public const string TipoOnboarding = "ONBOARDING";

    private Treinamento()
    {
    }

    public int Id { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public string? TipoTreinamento { get; private set; }

    public DateOnly? ComecaEm { get; private set; }

    public bool Obrigatorio { get; private set; }

    public bool Publicado { get; private set; }

    public PublicoDoTreinamento? Publico { get; private set; }

    public bool EmiteCertificado { get; private set; }

    public bool IncluiAdmitidos { get; private set; }

    public DateTime? EncerradoEmUtc { get; private set; }

    public bool Onboarding => TipoTreinamento == TipoOnboarding;

    public static Treinamento Criar(string titulo, string? descricao, bool onboarding, bool obrigatorio, PublicoDoTreinamento? publico, bool emiteCertificado, bool incluiAdmitidos, DateOnly? comecaEm) =>
        new()
        {
            Titulo = titulo,
            Descricao = descricao,
            TipoTreinamento = onboarding ? TipoOnboarding : null,
            Obrigatorio = obrigatorio,
            Publico = publico,
            EmiteCertificado = emiteCertificado,
            IncluiAdmitidos = incluiAdmitidos,
            ComecaEm = comecaEm,
            Publicado = false,
        };
}
