using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// A conferência dos arquivos de uma resposta (PDF ou Word até 5 MB, o tipo pelo CONTEÚDO) para as duas portas que os recebem (o funcionário e o candidato): cada arquivo ruim vira um erro no campo dado (<c>arquivos</c> ou <c>curriculo</c>) e quem passa leva o tipo e o nome LIMPO (sem diretório,
/// sem aspas nem controle) que a linha da resposta guarda.
/// </summary>
public static class ConferenciaDeArquivos
{
    public static async Task<IReadOnlyList<ArquivoDeRespostaConferido>> ConferirAsync(ColetorDeErros erros, string campo, IReadOnlyList<ArquivoRecebido> arquivos, CancellationToken ct)
    {
        var conferidos = new List<ArquivoDeRespostaConferido>(arquivos.Count);
        foreach (var arquivo in arquivos)
        {
            if (await ValidacaoDeArquivoDeFormulario.ValidarAsync(erros, campo, arquivo, ct) is { } tipo)
            {
                conferidos.Add(new ArquivoDeRespostaConferido(arquivo, tipo, NomeParaDownload.Limpar(arquivo.NomeOriginal, tipo.Extensao)));
            }
        }

        return conferidos;
    }
}
