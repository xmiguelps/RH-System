using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>
/// <c>POST /api/rh/treinamentos/{treinamentoId}/encerrar</c>: só vale para treinamento EM ANDAMENTO (rascunho e agendado = 422 <c>TREINAMENTO_NAO_ESTA_EM_ANDAMENTO</c>, encerrado = 422 <c>TREINAMENTO_ENCERRADO</c>). Com pessoas ATIVAS ainda pendentes (<c>INSCRITO</c> ou <c>EM_ANDAMENTO</c>) e sem <c>confirmar=true</c>: 409 <c>CONFIRMACAO_NECESSARIA</c> com os três números, sem encerrar.
/// Grava <c>ENCERRADO_EM</c> (agora) por <c>UPDATE</c> condicional ao estado lido: se outra aba mudou o treinamento no meio, relê e dá o erro certo. As linhas dos participantes e os certificados não mudam. Evento <c>ALTEROU</c>.
/// </summary>
public sealed class EncerrarTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, ITreinamentosRepositorio treinamentos, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const int TentativasNaCorrida = 5;

    public async Task<TreinamentoRh> ExecutarAsync(int treinamentoId, bool confirmar, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= TentativasNaCorrida; tentativa++)
        {
            var lido = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
            var agora = relogio.AgoraUtc;
            var hoje = FusoBrasil.Hoje(agora);
            switch (lido.Situacao(hoje))
            {
                case SituacaoDoTreinamento.Encerrado:
                    throw MensagensDosTreinamentos.EncerradoParaEncerrar.ComoProblema();
                case SituacaoDoTreinamento.Rascunho or SituacaoDoTreinamento.Agendado:
                    throw MensagensDosTreinamentos.NaoEstaEmAndamento.ComoProblema();
            }

            var pendentes = await consulta.ContarPendentesAsync(treinamentoId, ct);
            if (pendentes.Total > 0 && !confirmar)
            {
                throw MensagensDosTreinamentos.ConfirmacaoNecessaria(pendentes.EmAndamento, pendentes.NaoIniciaram, AcaoQueExigeConfirmacao.Encerrar);
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await treinamentos.EncerrarAsync(lido, agora, ct))
                    {
                        return false;
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, DescricaoDoEncerramento(lido.Titulo, pendentes.Total), $"Treinamento \"{lido.Titulo}\"", "TREINAMENTOS", lido.Id));
                    await unidade.SalvarAsync(ct);
                    return true;
                },
                ct);
            if (gravou)
            {
                return (await consulta.ObterDetalheAsync(treinamentoId, hoje, ct)).OuNaoEncontrado();
            }
        }

        throw MensagensDosTreinamentos.TreinamentoMudouEmOutraTela;
    }

    private static string DescricaoDoEncerramento(string titulo, int pendentes) =>
        pendentes == 0 ? $"Encerrou o treinamento \"{titulo}\"." : $"Encerrou o treinamento \"{titulo}\" (pendentes: {pendentes}).";
}

/// <summary>
/// <c>DELETE /api/rh/treinamentos/{treinamentoId}</c>: exclusão FÍSICA, só enquanto nenhum certificado foi emitido. Ordem: 404; 422 <c>TREINAMENTO_COM_CERTIFICADOS</c> (o treinamento emite certificado e alguém o concluiu; vale também com <c>confirmar=true</c> e vem ANTES da confirmação); em andamento ou encerrado com pessoas ATIVAS pendentes e sem <c>confirmar=true</c>: 409
/// <c>CONFIRMACAO_NECESSARIA</c>; rascunho e agendado apagam direto (ninguém chegou a ver). Tudo numa transação que começa TRAVANDO a linha do treinamento (uma conclusão no mesmo instante espera e é conferida; as conferências são refeitas com a linha travada): progresso dos módulos, módulos, certificados (as linhas do trigger), participantes, público e o treinamento,
/// nessa ordem. Os PDFs dos módulos saem do armazenamento DEPOIS do commit (o que falhar fica para a limpeza de sobras). Evento <c>EXCLUIU</c> com o título e o número de pendentes.
/// </summary>
public sealed class ExcluirTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, ITreinamentosRepositorio treinamentos, IArmazenamentoDeArquivos armazenamento, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task ExecutarAsync(int treinamentoId, bool confirmar, CancellationToken ct)
    {
        _ = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);

        var arquivos = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                _ = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                var lido = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();

                var certificados = await consulta.ContarCertificadosEmitidosAsync(treinamentoId, ct);
                if (certificados > 0)
                {
                    throw MensagensDosTreinamentos.ComCertificados(certificados);
                }

                var pendentes = new PendentesDoTreinamento(0, 0);
                if (lido.Situacao(hoje) is SituacaoDoTreinamento.EmAndamento or SituacaoDoTreinamento.Encerrado)
                {
                    pendentes = await consulta.ContarPendentesAsync(treinamentoId, ct);
                    if (pendentes.Total > 0 && !confirmar)
                    {
                        throw MensagensDosTreinamentos.ConfirmacaoNecessaria(pendentes.EmAndamento, pendentes.NaoIniciaram, AcaoQueExigeConfirmacao.Excluir);
                    }
                }

                var caminhos = (await modulos.ListarAsync(treinamentoId, ct)).Select(m => m.CaminhoArquivo).OfType<string>().ToList();
                await treinamentos.ExcluirAsync(treinamentoId, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Excluiu, $"Excluiu o treinamento \"{lido.Titulo}\" (pendentes: {pendentes.Total}).", $"Treinamento \"{lido.Titulo}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return caminhos;
            },
            ct);

        foreach (var caminho in arquivos)
        {
            await ArquivosDoTreinamento.ApagarDepoisDoCommitAsync(armazenamento, caminho);
        }
    }
}
