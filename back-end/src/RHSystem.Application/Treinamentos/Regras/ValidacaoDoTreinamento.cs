using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>Os campos do treinamento já conferidos (título e descrição aparados, descrição em branco como <c>null</c>, listas sem repetição e só as coerentes com o público).</summary>
public sealed record CamposDoTreinamento(
    string Titulo,
    string? Descricao,
    DateOnly? ComecaEm,
    bool Obrigatorio,
    bool Onboarding,
    bool EmiteCertificado,
    PublicoDoTreinamento? Publico,
    IReadOnlyList<int> SetorIds,
    IReadOnlyList<int> FuncionarioIds,
    bool IncluiAdmitidos);

/// <summary>
/// Confere o comando do treinamento (<c>TreinamentoComando</c>): título de 3 a 200 caracteres, descrição até 2000, setores até 100 e pessoas até 500, sem repetição e com número de 1 em diante, e a lista só do público escolhido (com <c>TODOS</c>, ou sem público, qualquer lista é de outro público). Todos os erros juntos;
/// com algum erro devolve <c>null</c>. <see cref="ConferirParaPublicar"/> acrescenta o que só o publicar exige.
/// </summary>
public static class ValidacaoDoTreinamento
{
    public const int MinimoDoTitulo = 3;
    public const int MaximoDoTitulo = 200;
    public const int MaximoDaDescricao = 2000;
    public const int MaximoDeSetores = 100;
    public const int MaximoDePessoas = 500;

    public static CamposDoTreinamento? Validar(ColetorDeErros erros, string? titulo, string? descricao, DateOnly? comecaEm, bool obrigatorio, bool onboarding, bool emiteCertificado, PublicoDoTreinamento? publico, IReadOnlyList<int>? setorIds, IReadOnlyList<int>? funcionarioIds, bool incluiAdmitidos)
    {
        var valido = true;
        var tituloAparado = (titulo ?? string.Empty).Trim();
        var tamanho = ContagemDeCaracteres.De(tituloAparado);
        if (tamanho is < MinimoDoTitulo or > MaximoDoTitulo)
        {
            erros.Adicionar("titulo", "Informe o título do treinamento, de 3 a 200 caracteres.");
            valido = false;
        }

        var descricaoAparada = string.IsNullOrWhiteSpace(descricao) ? null : descricao.Trim();
        if (descricaoAparada is not null && ContagemDeCaracteres.De(descricaoAparada) > MaximoDaDescricao)
        {
            erros.Adicionar("descricao", "A descrição pode ter até 2000 caracteres.");
            valido = false;
        }

        var setores = setorIds ?? [];
        var pessoas = funcionarioIds ?? [];
        valido &= ConferirLista(erros, "setorIds", setores, MaximoDeSetores, "setores", publico == PublicoDoTreinamento.Setores);
        valido &= ConferirLista(erros, "funcionarioIds", pessoas, MaximoDePessoas, "pessoas", publico == PublicoDoTreinamento.Pessoas);
        return valido ? new CamposDoTreinamento(tituloAparado, descricaoAparada, comecaEm, obrigatorio, onboarding, emiteCertificado, publico, setores, pessoas, incluiAdmitidos) : null;
    }

    /// <summary>O que só o publicar exige: a data em que o acesso é liberado, o público escolhido com pelo menos um setor ou uma pessoa (<c>TODOS</c> basta) e pelo menos um módulo.</summary>
    public static void ConferirParaPublicar(ColetorDeErros erros, CamposDoTreinamento campos, int totalDeModulos)
    {
        if (campos.ComecaEm is null)
        {
            erros.Adicionar("comecaEm", "Informe a data em que o acesso é liberado.");
        }

        if (campos.Publico is null)
        {
            erros.Adicionar("publico", "Escolha para quem é o treinamento.");
        }
        else if (campos.Publico == PublicoDoTreinamento.Setores && campos.SetorIds.Count == 0)
        {
            erros.Adicionar("publico", "Escolha pelo menos um setor.");
        }
        else if (campos.Publico == PublicoDoTreinamento.Pessoas && campos.FuncionarioIds.Count == 0)
        {
            erros.Adicionar("publico", "Escolha pelo menos uma pessoa.");
        }

        if (totalDeModulos < 1)
        {
            erros.Adicionar("modulos", "Inclua pelo menos um módulo.");
        }
    }

    /// <summary>Depois de publicado o treinamento sempre tem data de início: tirar a data é 400.</summary>
    public static void ConferirDataDoPublicado(ColetorDeErros erros, DateOnly? comecaEm)
    {
        if (comecaEm is null)
        {
            erros.Adicionar("comecaEm", "Informe a data em que o acesso é liberado.");
        }
    }

    private static bool ConferirLista(ColetorDeErros erros, string campo, IReadOnlyList<int> ids, int maximo, string nome, bool dopublico)
    {
        if (ids.Count == 0)
        {
            return true;
        }

        if (!dopublico)
        {
            erros.Adicionar(campo, $"Esta lista é de outro público: deixe os {nome} vazios.");
            return false;
        }

        if (ids.Count > maximo)
        {
            erros.Adicionar(campo, $"Escolha até {maximo} {nome}.");
            return false;
        }

        if (ids.Any(id => id < 1))
        {
            erros.Adicionar(campo, "Os números precisam ser de 1 em diante.");
            return false;
        }

        if (ids.Distinct().Count() != ids.Count)
        {
            erros.Adicionar(campo, $"Não repita {nome}.");
            return false;
        }

        return true;
    }
}
