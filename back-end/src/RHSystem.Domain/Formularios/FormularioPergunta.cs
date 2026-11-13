namespace RHSystem.Domain.Formularios;

/// <summary>Uma pergunta do formulário (<c>FORMULARIOS_PERGUNTAS</c>). <c>Tipo</c> e <c>Ordem</c> aceitam <c>NULL</c> no banco, mas a aplicação sempre os preenche. A ordem é única por formulário (<c>UQ_FORMULARIOS_PERGUNTAS_FORMULARIO_ORDEM</c>).</summary>
public class FormularioPergunta
{
    private FormularioPergunta()
    {
    }

    public int Id { get; private set; }

    public int FormularioId { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public TipoDePergunta? Tipo { get; private set; }

    public int? Ordem { get; private set; }

    public bool Obrigatoria { get; private set; }

    public static FormularioPergunta Criar(int formularioId, string texto, TipoDePergunta tipo, int ordem, bool obrigatoria) =>
        new() { FormularioId = formularioId, Texto = texto, Tipo = tipo, Ordem = ordem, Obrigatoria = obrigatoria };
}
