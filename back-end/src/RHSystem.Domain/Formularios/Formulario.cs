namespace RHSystem.Domain.Formularios;

/// <summary>
/// Um formulário (<c>FORMULARIOS</c>). Nasce <b>rascunho</b>: <c>Ativo = false</c> EXPLÍCITO (o padrão do banco é <c>TRUE</c>, e um rascunho ativo aceitaria resposta) e lembrete automático ligado (o padrão do banco é <c>FALSE</c>). A situação é calculada das colunas e do relógio
/// (<c>SituacaoDoFormulario</c>). Quase toda coluna aceita <c>NULL</c> no banco (o mapeamento é conferido contra ele); a aplicação sempre preenche tipo e data de criação (o trigger usaria o <c>NOW()</c> real). As datas do grupo são UTC aqui e -03:00 no banco, cortadas ao
/// segundo. O token do link guarda-se em claro de propósito (o RH vê o link de novo): nunca vai para auditoria nem para log. Esta entidade não é auditada pelo interceptor: os atos do RH são auditados à mão.
/// </summary>
public class Formulario
{
    private Formulario()
    {
    }

    public int Id { get; private set; }

    public string Titulo { get; private set; } = string.Empty;

    public string? Descricao { get; private set; }

    public TipoDeFormulario? Tipo { get; private set; }

    public int? AutorId { get; private set; }

    public DateTime? CriadoEmUtc { get; private set; }

    /// <summary>Só o <c>true</c> aceita resposta; o prazo vencido é conferido pela aplicação (o banco não confere).</summary>
    public bool Ativo { get; private set; }

    public SentidoDaAvaliacao? SentidoAvaliacao { get; private set; }

    public AlvoDoFormulario? Alvo { get; private set; }

    /// <summary>Rascunho: nulo. Agendado: o início do dia agendado. Publicado: quando publicou.</summary>
    public DateTime? PublicadoEmUtc { get; private set; }

    public DateTime? EncerradoEmUtc { get; private set; }

    /// <summary>O último dia para responder (até 23:59 -03:00).</summary>
    public DateOnly? Prazo { get; private set; }

    /// <summary>O anonimato não muda depois de a primeira resposta (a chave estrangeira composta das respostas o garante).</summary>
    public bool Anonimo { get; private set; }

    public int? RecorrenciaIntervalo { get; private set; }

    public UnidadeDeRecorrencia? RecorrenciaUnidade { get; private set; }

    public DateTime? ProximaReaberturaEmUtc { get; private set; }

    public DateOnly? RecorrenciaAte { get; private set; }

    /// <summary>O código do link público (43 caracteres); só existe com <c>Alvo = Link</c>.</summary>
    public string? TokenDoLink { get; private set; }

    public bool LembreteAutomatico { get; private set; }

    public static Formulario Criar(int? autorId, string titulo, string? descricao, TipoDeFormulario tipo, SentidoDaAvaliacao? sentidoAvaliacao, bool anonimo, DateTime criadoEmUtc) =>
        new()
        {
            AutorId = autorId,
            Titulo = titulo,
            Descricao = descricao,
            Tipo = tipo,
            SentidoAvaliacao = sentidoAvaliacao,
            Anonimo = anonimo,
            CriadoEmUtc = CamposDeTexto.AoSegundo(criadoEmUtc),
            Ativo = false,
            LembreteAutomatico = true,
        };
}
