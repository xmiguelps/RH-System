namespace RHSystem.Domain.Formularios;

/// <summary>Uma alternativa de pergunta de múltipla escolha (<c>FORMULARIOS_PERGUNTAS_ALTERNATIVAS</c>). A ordem é única por pergunta; uma alternativa já respondida não muda (o trigger da pergunta não enxerga esta tabela: quem confere é a aplicação).</summary>
public class FormularioAlternativa
{
    private FormularioAlternativa()
    {
    }

    public int Id { get; private set; }

    public int PerguntaId { get; private set; }

    public string Texto { get; private set; } = string.Empty;

    public int Ordem { get; private set; }

    public static FormularioAlternativa Criar(int perguntaId, string texto, int ordem) => new() { PerguntaId = perguntaId, Texto = texto, Ordem = ordem };
}
