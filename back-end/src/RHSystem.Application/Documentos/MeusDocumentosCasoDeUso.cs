using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Documentos;

/// <summary>
/// "Meus documentos": qualquer pessoa logada (Funcionario, Gestor, RH) vê e envia SÓ os documentos dela. O id da pessoa é sempre o do token (<see cref="IEscopoDoUsuario.FuncionarioId"/>), nunca um parâmetro: documento de
/// outra pessoa e documento inexistente dão o mesmo 404. A ordem de avaliação do envio é a do contrato: 404 (a leitura não é efeito), 400 (todos os erros do arquivo juntos), 422 (o documento não está pendente), e só então o
/// efeito, pela receita de <see cref="EnvioDeArquivoDeDocumento"/>. <b>Corrida de dois envios</b>: os dois leem o documento <c>Pendente</c> e gravam o blob; o segundo a salvar não acha a linha pendente (a <c>Situacao</c> é
/// o token de concorrência), vira 422 <c>DOCUMENTO_NAO_PEDIDO</c> DENTRO do envio, e por isso o blob dele é apagado. Baixar o próprio documento não grava evento: só o RH baixando o de outra pessoa é auditado.
/// </summary>
public sealed class MeusDocumentosCasoDeUso(
    IDocumentosRepositorio documentos,
    IArmazenamentoDeArquivos armazenamento,
    EnvioDeArquivoDeDocumento envio,
    IUnidadeDeTrabalho unidade,
    IEscopoDoUsuario escopo,
    IRelogio relogio)
{
    public async Task<DocumentoColaboradorListaResposta> ListarAsync(CancellationToken ct) =>
        new(await documentos.ListarAsync(escopo.FuncionarioId, ct));

    public async Task<DocumentoColaborador> EnviarArquivoAsync(int documentoId, ArquivoRecebido? arquivo, int arquivosRecebidos, CancellationToken ct)
    {
        var documento = await documentos.ObterParaEnvioAsync(escopo.FuncionarioId, documentoId, ct) ?? throw new NaoEncontradoException();

        var erros = new ColetorDeErros();
        var tipoDoArquivo = await ValidacaoDeArquivoDeDocumento.ValidarAsync(erros, arquivo, arquivosRecebidos, "O arquivo é obrigatório.", ct);
        erros.LancarSeHouver();

        if (documento.Situacao != SituacaoDoDocumento.Pendente)
        {
            throw MensagensDoApoio.DocumentoNaoPedido.ComoErroDeNegocio();
        }

        var nomeGuardado = NomeParaDownload.Limpar(arquivo!.NomeOriginal, tipoDoArquivo!.Extensao);
        return await envio.GravarEEfetivarAsync(
            arquivo,
            tipoDoArquivo,
            caminho => unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    documento.ReceberArquivo(caminho, nomeGuardado, relogio.AgoraUtc);
                    if (!await documentos.SalvarEnvioAsync(ct))
                    {
                        throw MensagensDoApoio.DocumentoNaoPedido.ComoErroDeNegocio(); // outro envio chegou antes
                    }

                    return DocumentoColaborador.De(documento);
                },
                ct),
            ct);
    }

    public async Task<LinkTemporario> BaixarAsync(int documentoId, CancellationToken ct)
    {
        var documento = await documentos.ObterDaPessoaAsync(escopo.FuncionarioId, documentoId, ct) ?? throw new NaoEncontradoException();
        if (documento.Situacao == SituacaoDoDocumento.Pendente || documento.CaminhoArquivo is not { } caminho)
        {
            throw MensagensDoApoio.DocumentoSemArquivo.ComoErroDeNegocio();
        }

        return await armazenamento.CriarLinkTemporarioAsync(caminho, documento.NomeArquivoOriginal ?? string.Empty, ct);
    }
}
