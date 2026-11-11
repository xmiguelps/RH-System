namespace RHSystem.Application.Importacoes;

/// <summary>
/// Uma linha de dados da planilha. <paramref name="NumeroLinha"/> é o número que o RH vê no Excel (a linha 1 é o cabeçalho). <paramref name="Celulas"/> tem uma posição por coluna do modelo
/// (<see cref="ColunasDaImportacao.Todas"/>), com o texto aparado; <c>null</c> é célula vazia. Célula de data vem como <c>dd/mm/aaaa</c>.
/// </summary>
public sealed record LinhaLida(int NumeroLinha, IReadOnlyList<string?> Celulas);

/// <summary>As linhas NÃO vazias da aba "Colaboradores" (de 1 a <see cref="LimitesDeImportacao.LinhasMaximas"/>).</summary>
public sealed record PlanilhaLida(IReadOnlyList<LinhaLida> Linhas);
