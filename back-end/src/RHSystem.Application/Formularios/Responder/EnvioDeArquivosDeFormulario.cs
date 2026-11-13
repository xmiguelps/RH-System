using Microsoft.Extensions.Logging;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Documentos;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>Um arquivo de resposta já conferido: o que chegou, o tipo que o conteúdo revelou e o nome limpo que a pessoa deu (o que vai para <c>NOME_ARQUIVO_ORIGINAL</c> e para o "ver respostas").</summary>
public sealed record ArquivoDeRespostaConferido(ArquivoRecebido Arquivo, TipoDeArquivo Tipo, string Nome);

/// <summary>
/// O envio dos arquivos de uma resposta (a receita da decisão 11 para VÁRIOS arquivos): grava cada um no armazenamento SEM transação aberta, deixa o chamador efetivar (a transação curta que grava as linhas) e, se qualquer arquivo ou a efetivação falhar, apaga TODOS os que já foram
/// gravados. O erro ORIGINAL é o que sai; se a limpeza também falhar, a sobra fica para a <c>LIMPAR_ARQUIVOS_ORFAOS</c> (prefixo <c>formularios/</c>, com a fonte de referências dos formulários).
/// </summary>
public sealed class EnvioDeArquivosDeFormulario(IArmazenamentoDeArquivos armazenamento, ILogger<EnvioDeArquivosDeFormulario> log)
{
    public const string Pasta = "formularios";

    /// <summary>Grava os arquivos em caminhos novos, na mesma ordem, e chama <paramref name="efetivar"/> com os caminhos. Sem arquivos, só efetiva.</summary>
    public async Task<T> GravarEEfetivarAsync<T>(IReadOnlyList<ArquivoDeRespostaConferido> arquivos, Func<IReadOnlyList<string>, Task<T>> efetivar, CancellationToken ct)
    {
        var caminhos = new List<string>(arquivos.Count);
        try
        {
            foreach (var conferido in arquivos)
            {
                var caminho = CaminhosDeArquivo.Novo(Pasta, conferido.Tipo.Extensao);
                caminhos.Add(caminho); // antes de gravar: um arquivo gravado pela metade também é apagado
                await using var conteudo = conferido.Arquivo.Abrir();
                await armazenamento.GravarAsync(caminho, conteudo, conferido.Tipo.TipoDeConteudo, ct); // fora de qualquer transação
            }

            return await efetivar(caminhos);
        }
        catch
        {
            foreach (var caminho in caminhos)
            {
                await TentarApagarAsync(caminho);
            }

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
