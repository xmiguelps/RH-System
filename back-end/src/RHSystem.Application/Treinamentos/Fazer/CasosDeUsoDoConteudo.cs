using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>
/// O acesso ao CONTEÚDO de um módulo (e ao arquivo dele), a mesma regra para as duas rotas: 404 (treinamento que não existe, rascunho, não liberado, sem linha e fora do público; obrigatório sem linha; linha <c>REPROVADO</c>; encerrado em que a pessoa nunca começou, tenha ela linha <c>INSCRITO</c> ou nenhuma); 422 <c>INSCRICAO_PENDENTE</c> (opcional do público dela
/// sem inscrição). Quem tem linha <c>INSCRITO</c> (e o treinamento não foi encerrado) lê SEM "Começar"; quem começou ou concluiu continua lendo com o treinamento encerrado ("Rever conteúdo").
/// </summary>
internal static class AcessoAoConteudo
{
    public static TreinamentoParaParticipar Conferir(TreinamentoParaParticipar? treinamento, DateOnly hoje)
    {
        var t = AcessoDaParticipacao.Conferir(treinamento, hoje);
        if (!t.TemLinha)
        {
            if (t.Obrigatorio || t.EncerradoEmUtc is not null)
            {
                throw new NaoEncontradoException();
            }

            throw MensagensDosTreinamentos.InscricaoPendente.ComoProblema();
        }

        if (!AcessoDaParticipacao.LinhaVisivel(t) || (t.EncerradoEmUtc is not null && t.Situacao == SituacaoNoTreinamento.Inscrito))
        {
            throw new NaoEncontradoException();
        }

        return t;
    }
}

/// <summary>
/// <c>GET /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}</c>: o módulo aberto (texto como veio, nunca HTML; link de vídeo e de página; <c>temArquivo</c> no PDF, sem o caminho), em QUALQUER ordem e também depois de concluído. Ordem: o acesso de <see cref="AcessoAoConteudo"/>, depois 404 se o módulo não é deste treinamento. <c>concluido</c> e <c>concluidoEmUtc</c> são do progresso da PRÓPRIA pessoa.
/// </summary>
public sealed class ObterConteudoDeModuloCasoDeUso(IMinhaParticipacaoRepositorio participacao, IModulosRepositorio modulos, IConsultaDeMeusTreinamentos consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<TreinamentoModuloConteudo> ExecutarAsync(int treinamentoId, int moduloId, CancellationToken ct)
    {
        var eu = escopo.FuncionarioId;
        _ = AcessoAoConteudo.Conferir(await participacao.LerAsync(eu, treinamentoId, ct), FusoBrasil.Hoje(relogio.AgoraUtc));
        var modulo = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();
        var concluidoEm = await consulta.InstanteDaConclusaoDoModuloAsync(eu, moduloId, ct);
        return new TreinamentoModuloConteudo(modulo.Id, modulo.Ordem, modulo.Titulo, modulo.Tipo, modulo.DuracaoMinutos, modulo.ConteudoTexto, modulo.UrlExterna, modulo.CaminhoArquivo is not null, concluidoEm is not null, concluidoEm);
    }
}

/// <summary>
/// <c>GET /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/download</c>: o link temporário do PDF do módulo, como anexo. Mesmas regras de acesso do conteúdo; módulo que não é PDF (sem arquivo) = 404. O caminho do armazenamento nunca aparece; o nome do download é o nome original já limpo. Sem evento de auditoria.
/// </summary>
public sealed class BaixarArquivoDeModuloCasoDeUso(IMinhaParticipacaoRepositorio participacao, IModulosRepositorio modulos, IArmazenamentoDeArquivos armazenamento, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<LinkTemporario> ExecutarAsync(int treinamentoId, int moduloId, CancellationToken ct)
    {
        _ = AcessoAoConteudo.Conferir(await participacao.LerAsync(escopo.FuncionarioId, treinamentoId, ct), FusoBrasil.Hoje(relogio.AgoraUtc));
        var modulo = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();
        if (modulo.Tipo != TipoDeModulo.Pdf || modulo.CaminhoArquivo is null)
        {
            throw new NaoEncontradoException();
        }

        return await armazenamento.CriarLinkTemporarioAsync(modulo.CaminhoArquivo, modulo.NomeArquivoOriginal ?? "modulo.pdf", ct);
    }
}
