using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Feriados;

/// <summary>Um feriado como o contrato o mostra (<c>Feriado</c>). <c>PodeEditar</c> é <c>true</c> só no municipal.</summary>
public sealed record PontoFeriado(int Id, string Nome, DateOnly Data, TipoDeFeriado Tipo, string? Uf, string? Municipio, bool RepeteTodoAno, bool Ativo, bool PodeEditar);

/// <summary>A lista curta, sem paginação.</summary>
public sealed record PontoFeriadoLista(IReadOnlyList<PontoFeriado> Itens);

/// <summary>O corpo de <c>criarFeriado</c>: o tipo é sempre municipal e a UF e o município são os da empresa. <c>RepeteTodoAno</c> é opcional (falso se omitido).</summary>
public sealed record PontoFeriadoCriar
{
    public required string Nome { get; init; }

    public required DateOnly Data { get; init; }

    public bool RepeteTodoAno { get; init; }
}

/// <summary>O corpo de <c>editarFeriado</c>: os quatro campos vêm sempre.</summary>
public sealed record PontoFeriadoEditar
{
    public required string Nome { get; init; }

    public required DateOnly Data { get; init; }

    public required bool RepeteTodoAno { get; init; }

    public required bool Ativo { get; init; }
}

/// <summary>O recorte da lista: o ano, o mês (sem ele, o ano todo) e se vêm os desativados.</summary>
public sealed record FiltroDeFeriados(int Ano, int? Mes, bool IncluirInativos);
