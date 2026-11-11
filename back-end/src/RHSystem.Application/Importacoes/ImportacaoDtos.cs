using RHSystem.Domain.Importacoes;

namespace RHSystem.Application.Importacoes;

/// <summary>Situação de uma linha da prévia (<c>OK</c> ou <c>COM_ERRO</c>, pelo nome do contrato).</summary>
public enum SituacaoDaLinha
{
    Ok,
    ComErro,
}

/// <summary>Um problema numa célula da linha (<c>ImportacaoLinhaErro</c> do contrato): a coluna e o texto pronto para mostrar.</summary>
public sealed record ImportacaoLinhaErro(ColunaDaImportacao Coluna, string Mensagem);

/// <summary>Os valores da linha COMO FORAM DIGITADOS (texto; data de célula vem <c>dd/mm/aaaa</c>); <c>null</c> é célula vazia.</summary>
public sealed record ImportacaoLinhaValores(
    string? Nome,
    string? Cpf,
    string? DataNascimento,
    string? Telefone,
    string? EmailCorporativo,
    string? DataAdmissao,
    string? TipoContrato,
    string? Cargo,
    string? Setor,
    string? NivelDeAcesso,
    string? Beneficios)
{
    /// <summary>Cada célula vai para o campo da sua coluna (a posição na linha é a ordem de <see cref="ColunaDaImportacao"/>).</summary>
    public static ImportacaoLinhaValores De(LinhaLida linha)
    {
        var celulas = linha.Celulas;
        return new ImportacaoLinhaValores(
            celulas[(int)ColunaDaImportacao.Nome],
            celulas[(int)ColunaDaImportacao.Cpf],
            celulas[(int)ColunaDaImportacao.DataNascimento],
            celulas[(int)ColunaDaImportacao.Telefone],
            celulas[(int)ColunaDaImportacao.EmailCorporativo],
            celulas[(int)ColunaDaImportacao.DataAdmissao],
            celulas[(int)ColunaDaImportacao.TipoContrato],
            celulas[(int)ColunaDaImportacao.Cargo],
            celulas[(int)ColunaDaImportacao.Setor],
            celulas[(int)ColunaDaImportacao.NivelDeAcesso],
            celulas[(int)ColunaDaImportacao.Beneficios]);
    }
}

/// <summary>Uma linha de dados da planilha com o resultado da validação. <paramref name="NumeroLinha"/> é o que o RH vê no Excel (a linha 1 é o cabeçalho).</summary>
public sealed record ImportacaoLinha(int NumeroLinha, SituacaoDaLinha Situacao, ImportacaoLinhaValores Valores, IReadOnlyList<ImportacaoLinhaErro> Erros);

/// <summary>A prévia da importação (<c>ImportacaoPrevia</c> do contrato).</summary>
public sealed record ImportacaoPrevia(
    int ImportacaoId,
    string NomeArquivo,
    SituacaoDaImportacao Situacao,
    int TotalLinhas,
    int LinhasValidas,
    int LinhasComErro,
    bool PodeConfirmar,
    DateTime ExpiraEmUtc,
    IReadOnlyList<ImportacaoLinha> Linhas);
