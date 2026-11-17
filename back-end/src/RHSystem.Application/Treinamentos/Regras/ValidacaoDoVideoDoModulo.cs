using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;

namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>
/// O arquivo de vídeo de um módulo (decisão do dono, 2026-11-17): MP4 ou WebM, pelo CONTEÚDO (os primeiros bytes), até 100 MiB. Erro vai para <c>arquivo</c>: "Envie um arquivo só.", "O arquivo está vazio.", "O vídeo pode ter até 100 MB." e "Envie um vídeo MP4 ou WebM.". O tamanho é conferido
/// ANTES de abrir o arquivo.
/// </summary>
public static class ValidacaoDoVideoDoModulo
{
    public const long TamanhoMaximoBytes = 100L * 1024 * 1024;

    public static async Task<TipoDeArquivo?> ValidarAsync(ColetorDeErros erros, ArquivoRecebido arquivo, int arquivosRecebidos, CancellationToken ct)
    {
        if (arquivosRecebidos > 1)
        {
            erros.Adicionar("arquivo", "Envie um arquivo só.");
            return null;
        }

        if (arquivo.Tamanho <= 0)
        {
            erros.Adicionar("arquivo", "O arquivo está vazio.");
            return null;
        }

        if (arquivo.Tamanho > TamanhoMaximoBytes)
        {
            erros.Adicionar("arquivo", "O vídeo pode ter até 100 MB.");
            return null;
        }

        // O tipo vem do CONTEÚDO, nunca do nome nem do Content-Type que o cliente mandou.
        var inicio = new byte[IdentificadorDeVideo.BytesLidos];
        int lidos;
        await using (var conteudo = arquivo.Abrir())
        {
            lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, ct);
        }

        var tipo = IdentificadorDeVideo.Identificar(inicio.AsSpan(0, lidos));
        if (tipo is null)
        {
            erros.Adicionar("arquivo", "Envie um vídeo MP4 ou WebM.");
        }

        return tipo;
    }
}
