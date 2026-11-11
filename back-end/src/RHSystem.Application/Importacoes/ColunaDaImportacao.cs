namespace RHSystem.Application.Importacoes;

/// <summary>As colunas da aba "Colaboradores", NA ORDEM do modelo (<c>ImportacaoColuna</c> do contrato). A ordem dos valores é a ordem das colunas no Excel.</summary>
public enum ColunaDaImportacao
{
    Nome,
    Cpf,
    DataNascimento,
    Telefone,
    EmailCorporativo,
    DataAdmissao,
    TipoContrato,
    Cargo,
    Setor,
    NivelDeAcesso,
    Beneficios,
}

public static class ColunasDaImportacao
{
    public static IReadOnlyList<ColunaDaImportacao> Todas { get; } = Enum.GetValues<ColunaDaImportacao>();

    /// <summary>O código do contrato (<c>NOME</c>, <c>DATA_NASCIMENTO</c>...).</summary>
    public static string Codigo(ColunaDaImportacao coluna) => coluna switch
    {
        ColunaDaImportacao.Nome => "NOME",
        ColunaDaImportacao.Cpf => "CPF",
        ColunaDaImportacao.DataNascimento => "DATA_NASCIMENTO",
        ColunaDaImportacao.Telefone => "TELEFONE",
        ColunaDaImportacao.EmailCorporativo => "EMAIL_CORPORATIVO",
        ColunaDaImportacao.DataAdmissao => "DATA_ADMISSAO",
        ColunaDaImportacao.TipoContrato => "TIPO_CONTRATO",
        ColunaDaImportacao.Cargo => "CARGO",
        ColunaDaImportacao.Setor => "SETOR",
        ColunaDaImportacao.NivelDeAcesso => "NIVEL_DE_ACESSO",
        ColunaDaImportacao.Beneficios => "BENEFICIOS",
        _ => throw new ArgumentOutOfRangeException(nameof(coluna), coluna, "Coluna desconhecida."),
    };

    /// <summary>O título da coluna na linha 1 do modelo.</summary>
    public static string Titulo(ColunaDaImportacao coluna) => coluna switch
    {
        ColunaDaImportacao.Nome => "Nome completo",
        ColunaDaImportacao.Cpf => "CPF",
        ColunaDaImportacao.DataNascimento => "Data de nascimento",
        ColunaDaImportacao.Telefone => "Telefone",
        ColunaDaImportacao.EmailCorporativo => "E-mail corporativo",
        ColunaDaImportacao.DataAdmissao => "Data de admissão",
        ColunaDaImportacao.TipoContrato => "Tipo de contrato",
        ColunaDaImportacao.Cargo => "Cargo",
        ColunaDaImportacao.Setor => "Setor",
        ColunaDaImportacao.NivelDeAcesso => "Nível de acesso",
        ColunaDaImportacao.Beneficios => "Benefícios",
        _ => throw new ArgumentOutOfRangeException(nameof(coluna), coluna, "Coluna desconhecida."),
    };
}
