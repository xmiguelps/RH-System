using System.Text.RegularExpressions;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;

namespace RHSystem.Infrastructure.Arquivos.Tarefas;

/// <summary>
/// <c>LIMPAR_ARQUIVOS_ORFAOS</c>: apaga do armazenamento o arquivo que NENHUMA linha do banco referencia. Só mexe em prefixo que tem <see cref="IFonteDeReferenciasDeArquivo"/> (só a fonte sabe o que está referenciado) e só
/// no que é MAIS VELHO que <c>Armazenamento:SobrasMinimoHoras</c> (o mínimo protege o envio que já gravou o blob e ainda não inseriu a linha, e a sobra que uma queda nesse intervalo deixa é apagada depois dele). Pergunta à fonte
/// em lotes de 100. Se a fonte falha, NADA do prefixo dela é apagado e as outras seguem; no fim a tarefa lança (sem o texto do erro da fonte) para o agendador tentar de novo e a falha aparecer, em vez de ser engolida. A recuperação
/// agenda uma por dia (<c>LIMPAR_ARQUIVOS_ORFAOS:dia=AAAA-MM-DD</c>, o dia de Brasília).
/// </summary>
public sealed partial class LimparArquivosOrfaosTarefa(
    IEnumerable<IFonteDeReferenciasDeArquivo> fontes,
    IListagemDeArquivos listagem,
    IArmazenamentoDeArquivos armazenamento,
    IAgendador agendador,
    IRelogio relogio,
    OpcoesDeArmazenamento opcoes,
    ILogger<LimparArquivosOrfaosTarefa> log) : ITarefaAgendada
{
    public const string TipoDaTarefa = "LIMPAR_ARQUIVOS_ORFAOS";

    private const int TamanhoDoLote = 100;

    public string Tipo => TipoDaTarefa;

    /// <summary>Um prefixo de pasta: um ou mais segmentos <c>[a-z0-9-]</c>, cada um seguido de barra. Vazio ou sem a barra final listaria mais do que a fonte conhece.</summary>
    [GeneratedRegex("^([a-z0-9-]+/)+$")]
    private static partial Regex FormatoDoPrefixo();

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var dia = FusoBrasil.Hoje(agora);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("dia", dia)), agora, DiaReferencia: dia), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var corte = relogio.AgoraUtc.AddHours(-opcoes.SobrasMinimoHoras);
        var prefixosComFalha = 0;
        foreach (var fonte in fontes)
        {
            if (!FormatoDoPrefixo().IsMatch(fonte.Prefixo ?? string.Empty))
            {
                log.LogWarning("A fonte de referências de arquivo {Fonte} tem um prefixo inválido e foi ignorada.", fonte.GetType().Name);
                continue;
            }

            try
            {
                await LimparPrefixoAsync(fonte, corte, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                // Só o tipo da exceção vai para o log: a mensagem pode trazer dado de quem a lançou.
                prefixosComFalha++;
                log.LogError("A limpeza de arquivos órfãos falhou no prefixo {Prefixo} ({Tipo}); nada mais dele foi apagado.", fonte.Prefixo, erro.GetType().Name);
            }
        }

        if (prefixosComFalha > 0)
        {
            throw new InvalidOperationException($"A limpeza de arquivos órfãos falhou em {prefixosComFalha} prefixo(s); o que não foi apagado fica para a próxima execução.");
        }
    }

    private async Task LimparPrefixoAsync(IFonteDeReferenciasDeArquivo fonte, DateTime corte, CancellationToken ct)
    {
        var candidatos = new List<string>();
        await foreach (var arquivo in listagem.ListarAsync(fonte.Prefixo, ct))
        {
            if (arquivo.CriadoEmUtc < corte)
            {
                candidatos.Add(arquivo.Caminho);
            }
        }

        // Primeiro todas as respostas da fonte, depois as exclusões: se a fonte falha no meio, nada do prefixo é apagado.
        var orfaos = new List<string>();
        foreach (var lote in candidatos.Chunk(TamanhoDoLote))
        {
            var referenciados = await fonte.ReferenciadosAsync(lote, ct);
            orfaos.AddRange(lote.Where(caminho => !referenciados.Contains(caminho)));
        }

        var naoApagados = 0;
        foreach (var caminho in orfaos)
        {
            try
            {
                await armazenamento.ApagarAsync(caminho, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                naoApagados++;
                log.LogError("Não foi possível apagar uma sobra do prefixo {Prefixo} ({Tipo}); fica para a próxima execução.", fonte.Prefixo, erro.GetType().Name);
            }
        }

        if (naoApagados > 0)
        {
            throw new InvalidOperationException($"{naoApagados} sobra(s) não puderam ser apagadas.");
        }
    }
}
