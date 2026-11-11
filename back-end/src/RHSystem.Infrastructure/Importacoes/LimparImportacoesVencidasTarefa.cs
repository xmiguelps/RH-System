using System.Globalization;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Importacoes;

namespace RHSystem.Infrastructure.Importacoes;

/// <summary>
/// <c>LIMPAR_IMPORTACOES_VENCIDAS</c>: a prévia de uma importação tem dado pessoal (nome, CPF, nascimento, telefone e e-mail de gente que talvez nem seja cadastrada) e o arquivo guardado tem o mesmo dado. Quando a importação vence
/// (1 hora) os dois somem, sem esperar o dia seguinte: a tarefa é POR HORA (a <see cref="RecuperarAsync"/> da subida e da hora agenda a da hora de agora; a chave é estável dentro da hora) e limpa até
/// <c>100</c> importações por execução, as mais antigas primeiro. A linha fica, com os totais. O arquivo é apagado ANTES de a linha ser limpa; se ele não puder ser apagado a PRÉVIA some mesmo assim e o caminho fica, para a próxima
/// execução tentar de novo (a exceção sai só pelo tipo, no log e no erro da tarefa).
/// </summary>
public sealed class LimparImportacoesVencidasTarefa(
    IImportacoesRepositorio importacoes,
    IArmazenamentoDeArquivos armazenamento,
    IAgendador agendador,
    IRelogio relogio,
    ILogger<LimparImportacoesVencidasTarefa> log) : ITarefaAgendada
{
    public const string TipoDaTarefa = "LIMPAR_IMPORTACOES_VENCIDAS";

    private const int TamanhoDoLote = 100;

    public string Tipo => TipoDaTarefa;

    public async Task RecuperarAsync(CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hora = agora.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture);
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("hora", hora)), agora), ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var naoApagados = 0;
        foreach (var vencida in await importacoes.ListarVencidasAsync(relogio.AgoraUtc, TamanhoDoLote, ct))
        {
            var arquivoApagado = true;
            if (vencida.CaminhoArquivo is { } caminho)
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
                    // Só o tipo da exceção vai para o log: a mensagem pode trazer dado de quem a lançou.
                    arquivoApagado = false;
                    naoApagados++;
                    log.LogError("Não foi possível apagar o arquivo de uma importação vencida ({Tipo}); fica para a próxima execução.", erro.GetType().Name);
                }
            }

            await importacoes.LimparVencidaAsync(vencida.Id, arquivoApagado, ct);
        }

        if (naoApagados > 0)
        {
            throw new InvalidOperationException($"{naoApagados} arquivo(s) de importação não puderam ser apagados; ficam para a próxima execução.");
        }
    }
}
