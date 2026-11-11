namespace RHSystem.Application.Importacoes;

/// <summary>Um cargo, setor ou benefício que existe hoje: o id para gravar e o nome que o RH escreve na planilha.</summary>
public sealed record ItemDoCatalogo(int Id, string Nome);

/// <summary>O que a aba "Valores válidos" do modelo mostra: os nomes, em ordem de nome, dos cadastros que existem hoje; os tipos de contrato e os níveis de acesso são fixos.</summary>
public sealed record ValoresValidosDoModelo(IReadOnlyList<string> Cargos, IReadOnlyList<string> Setores, IReadOnlyList<string> Beneficios)
{
    public static IReadOnlyList<string> TiposDeContrato { get; } = ["CLT", "Estágio"];

    public static IReadOnlyList<string> NiveisDeAcesso { get; } = ["Colaborador", "Gestor", "RH"];
}

/// <summary>Os cadastros de apoio de hoje, para montar o modelo e (nas tarefas seguintes) trocar os nomes da planilha pelos ids.</summary>
public sealed record CatalogoDaImportacao(IReadOnlyList<ItemDoCatalogo> Cargos, IReadOnlyList<ItemDoCatalogo> Setores, IReadOnlyList<ItemDoCatalogo> Beneficios)
{
    public ValoresValidosDoModelo ValoresValidos() =>
        new(Cargos.Select(c => c.Nome).ToList(), Setores.Select(s => s.Nome).ToList(), Beneficios.Select(b => b.Nome).ToList());
}

public interface IConsultaDoCatalogoDaImportacao
{
    /// <summary>Todos os cargos, setores e benefícios, cada lista em ordem de nome.</summary>
    Task<CatalogoDaImportacao> ObterAsync(CancellationToken ct);
}
