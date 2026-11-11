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
        await AgendarDaHoraAsync(InicioDaHora(agora), agora, ct);
    }

    public async Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;

        // Encadeia a da hora seguinte, para ela só rodar a partir do começo da hora. A recuperação roda a cada 60 minutos contados do FIM da anterior, então o espaçamento deriva e pode pular uma hora (11:59 e depois 13:00): sem este
        // encadeamento a prévia que vence às 12:05 ficaria até as 13:00. A chave é estável, então repetir não duplica.
        var proxima = InicioDaHora(agora).AddHours(1);
        await AgendarDaHoraAsync(proxima, proxima, ct);

        var naoApagados = 0;
        foreach (var vencida in await importacoes.ListarVencidasAsync(agora, TamanhoDoLote, ct))
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

    private static DateTime InicioDaHora(DateTime agoraUtc) => new(agoraUtc.Year, agoraUtc.Month, agoraUtc.Day, agoraUtc.Hour, 0, 0, DateTimeKind.Utc);

    private async Task AgendarDaHoraAsync(DateTime inicioDaHora, DateTime executarApos, CancellationToken ct) =>
        await agendador.AgendarAsync(new NovaTarefa(Tipo, ChaveDeIdempotencia.Montar(Tipo, ("hora", inicioDaHora.ToString("yyyy-MM-dd'T'HH", CultureInfo.InvariantCulture))), executarApos), ct);
}
