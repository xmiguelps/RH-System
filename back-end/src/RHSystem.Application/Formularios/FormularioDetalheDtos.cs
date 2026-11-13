using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios;

/// <summary>Uma opção de uma pergunta de múltipla escolha (<c>PerguntaAlternativa</c>).</summary>
public sealed record PerguntaAlternativa(int Id, int Ordem, string Texto);

/// <summary>Uma pergunta (<c>PerguntaFormulario</c>): <c>Alternativas</c> só tem itens na múltipla escolha.</summary>
public sealed record PerguntaFormulario(int Id, int Ordem, string Texto, TipoDePergunta Tipo, bool Obrigatoria, IReadOnlyList<PerguntaAlternativa> Alternativas);

/// <summary>A recorrência do formulário como o RH a vê (<c>FormularioRecorrencia</c>). <c>Ate</c> tem padrão porque é opcional no contrato (sem ele o gerador de OpenAPI o marcaria como obrigatório).</summary>
public sealed record FormularioRecorrencia(int Intervalo, UnidadeDeRecorrencia Unidade, DateOnly? Ate = null);

/// <summary>Um setor escolhido na publicação (<c>FormularioSetor</c>).</summary>
public sealed record FormularioSetor(int Id, string Nome);

/// <summary>
/// O que foi escolhido na etapa Publicação e o estado dela (<c>FormularioPublicacao</c>). <c>Setores</c> só tem itens com o alvo <c>SETORES</c> e <c>Pessoas</c> só com <c>PESSOAS</c>; <c>AgendarPara</c> só enquanto <c>AGENDADO</c>; <c>PublicadoEm</c> só depois de publicado;
/// <c>TokenDoLink</c> só no alvo <c>LINK</c> depois de publicar ou agendar.
/// </summary>
public sealed record FormularioPublicacao(
    AlvoDoFormulario? Alvo,
    IReadOnlyList<FormularioSetor> Setores,
    IReadOnlyList<FuncionarioResumo> Pessoas,
    DateOnly? AgendarPara,
    DateOnly? Prazo,
    FormularioRecorrencia? Recorrencia,
    bool LembreteAutomatico,
    DateTime? PublicadoEmUtc,
    DateTime? EncerradoEmUtc,
    DateTime? ProximaReaberturaEmUtc,
    string? TokenDoLink);

/// <summary>O formulário inteiro, como o RH o edita (<c>FormularioDetalheRh</c>). <c>TemRespostas</c> é <c>null</c> em formulário anônimo, em qualquer situação.</summary>
public sealed record FormularioDetalheRh(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? SentidoAvaliacao,
    bool Anonimo,
    SituacaoDoFormulario Situacao,
    FuncionarioResumo? Autor,
    DateTime CriadoEmUtc,
    bool? TemRespostas,
    IReadOnlyList<PerguntaFormulario> Perguntas,
    FormularioPublicacao Publicacao);
