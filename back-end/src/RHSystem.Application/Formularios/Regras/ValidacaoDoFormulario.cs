using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Regras;

/// <summary>O título e a descrição já aparados (descrição em branco vira <c>null</c>). O tipo, o anonimato e o sentido vêm do próprio comando.</summary>
public sealed record DetalhesValidados(string Titulo, string? Descricao);

/// <summary>Uma alternativa conferida: com <paramref name="Id"/> é a que já existe, sem ele é nova.</summary>
public sealed record AlternativaValidada(int? Id, string Texto);

/// <summary>A pergunta conferida: texto aparado, alternativas aparadas e a posição pedida (sem posição, vai para o fim).</summary>
public sealed record PerguntaValidada(string Texto, int? Ordem, bool Obrigatoria, IReadOnlyList<AlternativaValidada> Alternativas);

public sealed record RecorrenciaValidada(int Intervalo, UnidadeDeRecorrencia Unidade, DateOnly? Ate);

/// <summary>A publicação conferida: listas sem repetição, só as coerentes com o alvo.</summary>
public sealed record PublicacaoValidada(
    AlvoDoFormulario Alvo,
    IReadOnlyList<int> SetorIds,
    IReadOnlyList<int> FuncionarioIds,
    DateOnly? AgendarPara,
    DateOnly? Prazo,
    RecorrenciaValidada? Recorrencia,
    bool LembreteAutomatico);

/// <summary>Confere o título (1 a 200 caracteres), a descrição (até 1000), o anonimato e o sentido da avaliação. Todos os erros juntos; com algum erro devolve <c>null</c>.</summary>
public static class ValidacaoDoFormulario
{
    public const int MaximoDoTitulo = 200;
    public const int MaximoDaDescricao = 1000;

    public static DetalhesValidados? Validar(ColetorDeErros erros, FormularioDetalhesComando comando)
    {
        var valido = true;
        var titulo = (comando.Titulo ?? string.Empty).Trim();
        if (titulo.Length == 0 || ContagemDeCaracteres.De(titulo) > MaximoDoTitulo)
        {
            erros.Adicionar("titulo", "Informe o título do formulário, com até 200 caracteres.");
            valido = false;
        }

        var descricao = string.IsNullOrWhiteSpace(comando.Descricao) ? null : comando.Descricao.Trim();
        if (descricao is not null && ContagemDeCaracteres.De(descricao) > MaximoDaDescricao)
        {
            erros.Adicionar("descricao", "A descrição pode ter até 1000 caracteres.");
            valido = false;
        }

        if (comando.Anonimo && comando.Tipo == TipoDeFormulario.Outro)
        {
            erros.Adicionar("anonimo", "A caixa de sugestões não pode ser anônima.");
            valido = false;
        }

        if (comando.Anonimo && comando.Tipo == TipoDeFormulario.Avaliacao && comando.SentidoAvaliacao == SentidoDaAvaliacao.GestorAvaliaEquipe)
        {
            erros.Adicionar("anonimo", "A avaliação do gestor sobre a equipe não pode ser anônima: cada pessoa recebe uma só avaliação e o resultado nunca apareceria.");
            valido = false;
        }

        if (comando.Tipo == TipoDeFormulario.Avaliacao && comando.SentidoAvaliacao is null)
        {
            erros.Adicionar("sentidoAvaliacao", "Informe quem avalia quem: GESTOR_AVALIA_EQUIPE ou EQUIPE_AVALIA_GESTOR.");
            valido = false;
        }

        if (comando.Tipo != TipoDeFormulario.Avaliacao && comando.SentidoAvaliacao is not null)
        {
            erros.Adicionar("sentidoAvaliacao", "O sentido só vale para a avaliação.");
            valido = false;
        }

        return valido ? new DetalhesValidados(titulo, descricao) : null;
    }
}

/// <summary>Confere a pergunta: texto de 1 a 500 caracteres, de 2 a 10 alternativas de 1 a 255 só na múltipla escolha e a posição a partir de 1.</summary>
public static class ValidacaoDaPergunta
{
    public const int MaximoDoTexto = 500;
    public const int MaximoDaAlternativa = 255;
    public const int MinimoDeAlternativas = 2;
    public const int MaximoDeAlternativas = 10;

    public static PerguntaValidada? Validar(ColetorDeErros erros, PerguntaComando comando)
    {
        var valido = true;
        var texto = (comando.Texto ?? string.Empty).Trim();
        if (texto.Length == 0 || ContagemDeCaracteres.De(texto) > MaximoDoTexto)
        {
            erros.Adicionar("texto", "Informe o texto da pergunta, com até 500 caracteres.");
            valido = false;
        }

        if (comando.Ordem is < 1)
        {
            erros.Adicionar("ordem", "A posição começa em 1.");
            valido = false;
        }

        var alternativas = (comando.Alternativas ?? []).Select(a => new AlternativaValidada(a.Id, (a.Texto ?? string.Empty).Trim())).ToList();
        if (comando.Tipo != TipoDePergunta.MultiplaEscolha)
        {
            if (alternativas.Count > 0)
            {
                erros.Adicionar("alternativas", "Só a múltipla escolha tem alternativas.");
                valido = false;
            }
        }
        else
        {
            if (alternativas.Count is < MinimoDeAlternativas or > MaximoDeAlternativas)
            {
                erros.Adicionar("alternativas", "A múltipla escolha precisa de 2 a 10 alternativas.");
                valido = false;
            }

            if (alternativas.Any(a => a.Texto.Length == 0 || ContagemDeCaracteres.De(a.Texto) > MaximoDaAlternativa))
            {
                erros.Adicionar("alternativas", "Cada alternativa tem de 1 a 255 caracteres.");
                valido = false;
            }
        }

        return valido ? new PerguntaValidada(texto, comando.Ordem, comando.Obrigatoria, alternativas) : null;
    }
}

/// <summary>
/// Confere a publicação (<c>salvarPublicacaoFormulario</c> e <c>publicarFormulario</c>): as listas certas para cada alvo, o dia agendado e o prazo contra <paramref name="hoje"/> (dia no Brasil), a Sugestão sem prazo nem recorrência e a recorrência
/// (intervalo de 1 a 365, exige prazo, nunca por link, fim não anterior a hoje). Todos os erros juntos.
/// </summary>
public static class ValidacaoDaPublicacao
{
    public const int MaximoDoIntervalo = 365;

    public static PublicacaoValidada? Validar(ColetorDeErros erros, FormularioPublicacaoComando comando, DateOnly hoje, TipoDeFormulario tipo)
    {
        var valido = true;
        var setores = (comando.SetorIds ?? []).Distinct().ToList();
        var pessoas = (comando.FuncionarioIds ?? []).Distinct().ToList();

        void Recusar(string campo, string mensagem)
        {
            erros.Adicionar(campo, mensagem);
            valido = false;
        }

        switch (comando.Alvo)
        {
            case AlvoDoFormulario.Link:
                if (setores.Count > 0)
                {
                    Recusar("setorIds", "Por link, não se escolhem setores nem pessoas.");
                }

                if (pessoas.Count > 0)
                {
                    Recusar("funcionarioIds", "Por link, não se escolhem setores nem pessoas.");
                }

                break;
            case AlvoDoFormulario.Setores:
                if (setores.Count == 0)
                {
                    Recusar("setorIds", "Escolha pelo menos um setor.");
                }

                if (pessoas.Count > 0)
                {
                    Recusar("funcionarioIds", "Para setores, não se escolhem pessoas.");
                }

                break;
            default:
                if (pessoas.Count == 0)
                {
                    Recusar("funcionarioIds", "Escolha pelo menos uma pessoa.");
                }

                if (setores.Count > 0)
                {
                    Recusar("setorIds", "Para pessoas, não se escolhem setores.");
                }

                break;
        }

        if (comando.AgendarPara is { } dia && dia < hoje)
        {
            Recusar("agendarPara", "O dia agendado não pode ser anterior a hoje.");
        }

        var sugestao = tipo == TipoDeFormulario.Outro;
        if (sugestao && comando.Prazo is not null)
        {
            Recusar("prazo", "A caixa de sugestões é sempre aberta: não tem prazo.");
        }
        else if (comando.Prazo is { } prazo)
        {
            if (prazo < hoje)
            {
                Recusar("prazo", "O prazo não pode ser anterior a hoje.");
            }
            else if (comando.AgendarPara is { } agendada && prazo < agendada)
            {
                Recusar("prazo", "O prazo não pode ser anterior ao dia agendado.");
            }
        }

        RecorrenciaValidada? recorrencia = null;
        if (comando.Recorrencia is { } pedida)
        {
            if (sugestao)
            {
                Recusar("recorrencia", "A caixa de sugestões é sempre aberta: não se repete.");
            }
            else if (comando.Alvo == AlvoDoFormulario.Link)
            {
                Recusar("recorrencia", "O formulário por link não pode ser recorrente: reabrir apagaria as candidaturas.");
            }
            else
            {
                if (pedida.Intervalo is < 1 or > MaximoDoIntervalo)
                {
                    Recusar("recorrencia", "O intervalo vai de 1 a 365.");
                }

                if (comando.Prazo is null)
                {
                    Recusar("recorrencia", "Informe o prazo: a rodada nova avança o mesmo número de dias da anterior.");
                }

                if (pedida.Ate is { } fim && fim < hoje)
                {
                    Recusar("recorrencia", "A data final da recorrência não pode ser anterior a hoje.");
                }

                recorrencia = new RecorrenciaValidada(pedida.Intervalo, pedida.Unidade, pedida.Ate);
            }
        }

        return valido ? new PublicacaoValidada(comando.Alvo, setores, pessoas, comando.AgendarPara, comando.Prazo, recorrencia, comando.LembreteAutomatico) : null;
    }
}
