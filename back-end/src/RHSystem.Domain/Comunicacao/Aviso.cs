namespace RHSystem.Domain.Comunicacao;

/// <summary>
/// Um aviso (<c>AVISOS</c>). As datas são UTC dentro da aplicação, <b>cortadas ao segundo</b> (a coluna não tem fração e o MySQL arredondaria: encerrar no mesmo segundo da publicação poderia gravar a expiração antes
/// dela e o <c>CHK_AVISOS_DATAS</c> recusaria). <c>ENVIADO = false</c> é o rascunho; a situação (agendado, ativo, expirado) é calculada das datas e do relógio e nunca gravada. A data de publicação é sempre preenchida
/// pela APLICAÇÃO (o trigger usaria o <c>NOW()</c> do servidor, inclusive no rascunho: o rascunho sem data prevista faz um <c>UPDATE</c> para <c>NULL</c> logo depois do <c>INSERT</c>).
/// </summary>
public class Aviso
{
    private Aviso()
    {
    }

    public int Id { get; private set; }

    /// <summary>Quem criou (<c>null</c> se a pessoa foi apagada do cadastro; o contrato mostra <c>autorNome: null</c>).</summary>
    public int? AutorId { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string Conteudo { get; private set; } = string.Empty;

    public DateTime? PublicacaoEmUtc { get; private set; }

    public DateTime? ExpiracaoEmUtc { get; private set; }

    public TipoDeAviso? Tipo { get; private set; }

    public bool Fixado { get; private set; }

    public bool Enviado { get; private set; }

    public static Aviso Criar(int? autorId, TipoDeAviso tipo, string titulo, string conteudo, bool fixado, bool enviado, DateTime? publicacaoEmUtc, DateTime? expiracaoEmUtc) =>
        new()
        {
            AutorId = autorId,
            Tipo = tipo,
            Titulo = titulo,
            Conteudo = conteudo,
            Fixado = fixado,
            Enviado = enviado,
            PublicacaoEmUtc = publicacaoEmUtc is { } publicacao ? CamposDeTexto.AoSegundo(publicacao) : null,
            ExpiracaoEmUtc = expiracaoEmUtc is { } expiracao ? CamposDeTexto.AoSegundo(expiracao) : null,
        };
}
