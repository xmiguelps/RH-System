using RHSystem.Application.Colaboradores;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// As linhas da prévia: cada linha é convertida (<see cref="ConversorDaPlanilha"/>) e o lote inteiro é validado UMA vez pelo mesmo cadastro que grava (<see cref="ICadastroDeFuncionarios.ValidarAsync"/>), com as mesmas
/// regras da tela. Os problemas do cadastro voltam para a linha e a coluna do Excel; o que já falhou na conversão não se repete.
/// </summary>
public sealed class MontadorDaPrevia(ICadastroDeFuncionarios cadastro)
{
    public async Task<IReadOnlyList<ImportacaoLinha>> MontarAsync(IReadOnlyList<(int NumeroLinha, ImportacaoLinhaValores Valores)> linhas, CatalogoDaImportacao catalogo, CancellationToken ct)
    {
        var conversoes = linhas.Select(l => ConversorDaPlanilha.Converter(l.Valores, catalogo)).ToList();
        var problemas = (await cadastro.ValidarAsync(conversoes.Select(c => c.Novo).ToList(), ct)).ToLookup(p => p.Indice);

        var resultado = new List<ImportacaoLinha>(linhas.Count);
        for (var i = 0; i < linhas.Count; i++)
        {
            var erros = new List<ImportacaoLinhaErro>(conversoes[i].Erros);
            foreach (var problema in problemas[i])
            {
                var coluna = ConversorDaPlanilha.ColunaDoCampo(problema.Campo);
                if (!conversoes[i].Erros.Any(e => e.Coluna == coluna))
                {
                    erros.Add(new ImportacaoLinhaErro(coluna, Mensagem(problema, coluna, linhas)));
                }
            }

            // OrderBy é estável: dentro da mesma coluna, o erro de conversão vem antes do do cadastro.
            var ordenados = erros.OrderBy(e => e.Coluna).ToList();
            resultado.Add(new ImportacaoLinha(linhas[i].NumeroLinha, ordenados.Count == 0 ? SituacaoDaLinha.Ok : SituacaoDaLinha.ComErro, linhas[i].Valores, ordenados));
        }

        return resultado;
    }

    /// <summary>
    /// CPF e e-mail repetidos dizem em que linha do EXCEL está a outra ocorrência (o cadastro conta linhas de 1 a partir do lote), e o cadastro existente não diz o nome de quem é: a prévia fica guardada e só deve levar o que
    /// o RH digitou. Qualquer outro problema sai com a mensagem do cadastro.
    /// </summary>
    private static string Mensagem(ProblemaDeCadastro problema, ColunaDaImportacao coluna, IReadOnlyList<(int NumeroLinha, ImportacaoLinhaValores Valores)> linhas)
    {
        var dado = coluna switch
        {
            ColunaDaImportacao.Cpf => "CPF",
            ColunaDaImportacao.EmailCorporativo => "e-mail",
            _ => null,
        };
        if (dado is null)
        {
            return problema.Mensagem;
        }

        if (problema.OutroIndice is { } outro)
        {
            return $"Este {dado} aparece também na linha {linhas[outro].NumeroLinha}.";
        }

        if (problema.Existente is { } existente)
        {
            return existente.Ativo
                ? $"Já existe um colaborador com este {dado}."
                : $"Este {dado} pertence a um cadastro inativo. Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.";
        }

        return problema.Mensagem;
    }
}
