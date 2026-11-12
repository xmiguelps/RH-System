namespace RHSystem.Application.Ponto.Regras;

/// <summary>
/// Os feriados estaduais de <b>data fixa</b> de cada UF (a data cívica do estado). Os móveis (como o de Nossa Senhora da Penha, no Espírito Santo) e os que repetem um nacional não entram, e os estados sem feriado fixo conhecido (ES, GO, MG e MT) ficam sem
/// linha. É uma primeira tabela: o RH acrescenta o que faltar como feriado municipal ou da empresa.
/// </summary>
public static class TabelaDeFeriadosEstaduais
{
    private static readonly Dictionary<string, (string Nome, int Mes, int Dia)[]> PorUf = new(StringComparer.OrdinalIgnoreCase)
    {
        ["AC"] = [("Dia do Evangélico", 1, 23), ("Aniversário do Acre", 6, 15)],
        ["AL"] = [("Emancipação Política de Alagoas", 9, 16)],
        ["AM"] = [("Elevação do Amazonas à Categoria de Província", 9, 5)],
        ["AP"] = [("Dia de São José, padroeiro do Amapá", 3, 19), ("Criação do Território Federal do Amapá", 9, 13)],
        ["BA"] = [("Independência da Bahia", 7, 2)],
        ["CE"] = [("Data Magna do Ceará", 3, 25)],
        ["DF"] = [("Dia do Evangélico", 11, 30)],
        ["MA"] = [("Adesão do Maranhão à Independência do Brasil", 7, 28)],
        ["MS"] = [("Criação do Estado de Mato Grosso do Sul", 10, 11)],
        ["PA"] = [("Adesão do Grão-Pará à Independência do Brasil", 8, 15)],
        ["PB"] = [("Fundação do Estado da Paraíba", 8, 5)],
        ["PE"] = [("Revolução Pernambucana", 3, 6)],
        ["PI"] = [("Dia do Piauí", 10, 19)],
        ["PR"] = [("Emancipação Política do Paraná", 12, 19)],
        ["RJ"] = [("Dia de São Jorge", 4, 23)],
        ["RN"] = [("Mártires de Cunhaú e Uruaçu", 10, 3)],
        ["RO"] = [("Criação do Estado de Rondônia", 1, 4), ("Dia do Evangélico", 6, 18)],
        ["RR"] = [("Criação do Estado de Roraima", 10, 5)],
        ["RS"] = [("Revolução Farroupilha", 9, 20)],
        ["SC"] = [("Dia de Santa Catarina", 8, 11)],
        ["SE"] = [("Autonomia Política de Sergipe", 7, 8)],
        ["SP"] = [("Revolução Constitucionalista", 7, 9)],
        ["TO"] = [("Autonomia do Estado do Tocantins", 3, 18), ("Nossa Senhora da Natividade", 9, 8), ("Criação do Estado do Tocantins", 10, 5)],
    };

    /// <summary>Os feriados estaduais do ano, na ordem da data. UF vazia ou sem linha na tabela: lista vazia.</summary>
    public static IReadOnlyList<FeriadoCalculado> Da(string? uf, int ano) =>
        uf is not null && PorUf.TryGetValue(uf.Trim(), out var feriados)
            ? [.. feriados.Select(f => new FeriadoCalculado(f.Nome, new DateOnly(ano, f.Mes, f.Dia))).OrderBy(f => f.Data)]
            : [];
}
