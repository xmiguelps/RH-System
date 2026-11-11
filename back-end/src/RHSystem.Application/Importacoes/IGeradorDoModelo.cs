namespace RHSystem.Application.Importacoes;

/// <summary>Gera o <c>.xlsx</c> do modelo de importação (<c>baixarModeloDeImportacao</c>): a aba "Colaboradores" (só o cabeçalho) e a aba "Valores válidos".</summary>
public interface IGeradorDoModelo
{
    byte[] Gerar(ValoresValidosDoModelo valores);
}
