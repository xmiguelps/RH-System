using System.Text.Json.Serialization;
using RHSystem.Application.Comum;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios;

/// <summary>
/// O corpo de <c>criarFormulario</c> e <c>editarFormulario</c> (<c>FormularioDetalhesComando</c>). O tipo e o anonimato são OBRIGATÓRIOS no JSON (<c>JsonRequired</c>): sem isso o que falta virava o primeiro tipo e "com nome". <c>SentidoAvaliacao</c> é obrigatório na avaliação e
/// proibido nos outros tipos (400 em <c>sentidoAvaliacao</c>).
/// </summary>
public sealed record FormularioDetalhesComando(string Titulo, [property: JsonRequired] TipoDeFormulario Tipo, [property: JsonRequired] bool Anonimo)
{
    public string? Descricao { get; init; }

    public SentidoDaAvaliacao? SentidoAvaliacao { get; init; }
}

/// <summary>Uma alternativa no corpo da pergunta (<c>PerguntaAlternativaComando</c>): com <c>Id</c> mantém a alternativa, sem <c>Id</c> é nova, a que sumiu da lista é apagada.</summary>
public sealed record PerguntaAlternativaComando(string Texto)
{
    [JsonConverter(typeof(InteiroOpcionalSemNullConversor))]
    public int? Id { get; init; }
}

/// <summary>O corpo de <c>adicionarPerguntaFormulario</c> e <c>editarPerguntaFormulario</c> (<c>PerguntaComando</c>): de 2 a 10 alternativas na múltipla escolha e nenhuma nos outros tipos; sem <c>Ordem</c> a pergunta vai para o fim.</summary>
public sealed record PerguntaComando(string Texto, [property: JsonRequired] TipoDePergunta Tipo, [property: JsonRequired] bool Obrigatoria)
{
    public IReadOnlyList<PerguntaAlternativaComando> Alternativas { get; init; } = [];

    [JsonConverter(typeof(InteiroOpcionalSemNullConversor))]
    public int? Ordem { get; init; }
}

/// <summary>A recorrência do formulário (<c>FormularioRecorrencia</c>): reabre a cada <c>Intervalo</c> dias, semanas ou meses, até <c>Ate</c> (sem data, sem fim).</summary>
public sealed record FormularioRecorrenciaComando([property: JsonRequired] int Intervalo, [property: JsonRequired] UnidadeDeRecorrencia Unidade)
{
    public DateOnly? Ate { get; init; }
}

/// <summary>O corpo de <c>salvarPublicacaoFormulario</c> e <c>publicarFormulario</c> (<c>FormularioPublicacaoComando</c>): o alvo e as listas coerentes com ele, o dia agendado, o prazo, a recorrência e o lembrete automático (padrão ligado).</summary>
public sealed record FormularioPublicacaoComando([property: JsonRequired] AlvoDoFormulario Alvo)
{
    public IReadOnlyList<int> SetorIds { get; init; } = [];

    public IReadOnlyList<int> FuncionarioIds { get; init; } = [];

    public DateOnly? AgendarPara { get; init; }

    public DateOnly? Prazo { get; init; }

    public FormularioRecorrenciaComando? Recorrencia { get; init; }

    public bool LembreteAutomatico { get; init; } = true;
}
