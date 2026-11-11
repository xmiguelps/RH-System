using Microsoft.Extensions.Logging;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Comum;

namespace RHSystem.Application.Documentos;

/// <summary>
/// O arquivo de um documento, no que o RH (Task 7) e a própria pessoa (Task 8) compartilham: a validação (400 em <c>arquivo</c>, o tipo vem dos primeiros bytes, até 10 MiB) e o envio (blob fora de qualquer transação, o resto
/// numa transação curta que o chamador abre, e o blob apagado se qualquer passo falhar). A regra existe num lugar só.
/// </summary>
public static class ValidacaoDeArquivoDeDocumento
{
    /// <summary>
    /// Confere o arquivo recebido e devolve o tipo que o CONTEÚDO revelou (<c>null</c> quando há erro, que vai para <paramref name="erros"/> em <c>arquivo</c>). <paramref name="arquivosRecebidos"/> conta TODAS as partes de
    /// arquivo do formulário, de qualquer nome de campo.
    /// </summary>
    public static async Task<TipoDeArquivo?> ValidarAsync(ColetorDeErros erros, ArquivoRecebido? arquivo, int arquivosRecebidos, string mensagemSemArquivo, CancellationToken ct)
    {
        if (arquivosRecebidos > 1)
        {
            erros.Adicionar("arquivo", "Envie um arquivo só.");
            return null;
        }

        if (arquivo is null)
        {
            erros.Adicionar("arquivo", mensagemSemArquivo);
            return null;
        }

        if (arquivo.Tamanho <= 0)
        {
            erros.Adicionar("arquivo", "O arquivo está vazio.");
            return null;
        }

        if (arquivo.Tamanho > LimitesDeDocumento.TamanhoMaximoBytes)
        {
            erros.Adicionar("arquivo", "O arquivo pode ter até 10 MB.");
            return null;
        }

        // O tipo vem do CONTEÚDO, nunca do nome nem do Content-Type que o cliente mandou.
        var inicio = new byte[8];
        int lidos;
        await using (var conteudo = arquivo.Abrir())
        {
            lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, ct);
        }

        var tipo = IdentificadorDeTipoDeArquivo.Identificar(inicio.AsSpan(0, lidos));
        if (tipo is null)
        {
            erros.Adicionar("arquivo", "Envie um arquivo PDF, JPG ou PNG.");
        }

        return tipo;
    }
}

/// <summary>
/// O envio do arquivo de um documento (decisão 11 do plano da 1c): grava no armazenamento SEM transação aberta, deixa o chamador efetivar (uma transação curta que grava a linha e o evento) e, se o blob ou a efetivação falhar,
/// apaga o blob. O erro ORIGINAL é o que sai; se a limpeza também falhar, a sobra fica para a <c>LIMPAR_ARQUIVOS_ORFAOS</c>.
/// </summary>
public sealed class EnvioDeArquivoDeDocumento(IArmazenamentoDeArquivos armazenamento, ILogger<EnvioDeArquivoDeDocumento> log)
{
    /// <summary>Grava o arquivo no caminho novo, na pasta de documentos, e chama <paramref name="efetivar"/> com ele (a transação curta é do chamador). Qualquer falha, de um ou de outro, apaga o blob e relança.</summary>
    public Task<T> GravarEEfetivarAsync<T>(ArquivoRecebido arquivo, TipoDeArquivo tipo, Func<string, Task<T>> efetivar, CancellationToken ct) =>
        GravarEEfetivarAsync(arquivo, LimitesDeDocumento.PastaNoArmazenamento, tipo, efetivar, ct);

    /// <summary>A mesma receita para qualquer pasta do armazenamento (a importação por planilha guarda em <c>importacoes/</c>).</summary>
    public async Task<T> GravarEEfetivarAsync<T>(ArquivoRecebido arquivo, string pasta, TipoDeArquivo tipo, Func<string, Task<T>> efetivar, CancellationToken ct)
    {
        var caminho = CaminhosDeArquivo.Novo(pasta, tipo.Extensao);
        try
        {
            await using (var conteudo = arquivo.Abrir())
            {
                await armazenamento.GravarAsync(caminho, conteudo, tipo.TipoDeConteudo, ct); // fora de qualquer transação
            }

            return await efetivar(caminho);
        }
        catch
        {
            await TentarApagarAsync(caminho);
            throw;
        }
    }

    private async Task TentarApagarAsync(string caminho)
    {
        try
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
        }
        catch (Exception erro)
        {
            // O erro ORIGINAL é o que sai; a sobra fica para a LIMPAR_ARQUIVOS_ORFAOS. Só o tipo da exceção vai para o log.
            log.LogWarning("Não foi possível apagar o arquivo de um envio que falhou ({Tipo}); a limpeza de sobras o apaga depois.", erro.GetType().Name);
        }
    }
}
