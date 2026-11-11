namespace RHSystem.Application.Importacoes;

/// <summary>
/// Lê a aba "Colaboradores" de um <c>.xlsx</c>. Síncrono de propósito: o arquivo já está em memória (até 5 MiB). Problema no arquivo como um todo lança <see cref="Comum.ProblemaDeApiException"/> (422, de
/// <see cref="ErrosDePlanilha"/>); nenhum outro tipo de exceção sai por causa do CONTEÚDO do arquivo.
/// </summary>
public interface ILeitorDePlanilha
{
    PlanilhaLida Ler(Stream xlsx);
}
