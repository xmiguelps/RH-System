using System.Globalization;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Documentos;

/// <summary>
/// Os documentos da ficha do colaborador, pelo RH: lista, anexa (ou pede para a pessoa enviar) e baixa. A ordem de avaliação é a do contrato: 404 (a pessoa existe? é leitura, não efeito), 400 (todos os erros juntos, antes de
/// qualquer efeito). <b>A receita do envio de arquivo</b> (decisão 11): (1) a pessoa já foi lida; (2) <see cref="IArmazenamentoDeArquivos.GravarAsync"/> SEM transação aberta; (3) uma transação curta que insere a linha e o evento
/// <c>Criou</c>; (4) se o passo 2 ou o 3 falhar, <see cref="IArmazenamentoDeArquivos.ApagarAsync"/> do caminho (idempotente; se a limpeza também falhar, o erro ORIGINAL é o que sai e a sobra fica para a
/// <c>LIMPAR_ARQUIVOS_ORFAOS</c>). Nenhuma transação fica aberta durante o envio de até 10 MiB. O tipo do arquivo vem dos primeiros bytes. Nenhum e-mail sai: "pedir para enviar" só cria a linha pendente.
/// </summary>
public sealed class DocumentosDoColaboradorCasoDeUso(
    IDocumentosRepositorio documentos,
    IArmazenamentoDeArquivos armazenamento,
    EnvioDeArquivoDeDocumento envio,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio)
{
    private sealed record Campos(ModoDeDocumento Modo, TipoDeDocumento Tipo, DateOnly? DataValidade, TipoDeArquivo? TipoDoArquivo);

    public async Task<DocumentoColaboradorListaResposta> ListarAsync(int funcionarioId, CancellationToken ct)
    {
        _ = await documentos.ObterPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        return new DocumentoColaboradorListaResposta(await documentos.ListarAsync(funcionarioId, ct));
    }

    public async Task<DocumentoColaborador> AdicionarAsync(int funcionarioId, NovoDocumentoDoRh novo, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(novo);
        var pessoa = await documentos.ObterPessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var campos = await ValidarAsync(novo, ct);

        if (campos.Modo == ModoDeDocumento.PedirParaEnviar)
        {
            return await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var pedido = DocumentoDoColaborador.Pedir(pessoa, campos.Tipo, novo.Descricao, campos.DataValidade, relogio.AgoraUtc);
                    await documentos.AdicionarAsync(pedido, ct);
                    return DocumentoColaborador.De(pedido);
                },
                ct);
        }

        var arquivo = novo.Arquivo!;
        var tipoDoArquivo = campos.TipoDoArquivo!;
        var nomeGuardado = NomeParaDownload.Limpar(arquivo.NomeOriginal, tipoDoArquivo.Extensao);
        return await envio.GravarEEfetivarAsync(
            arquivo,
            tipoDoArquivo,
            caminho => unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var documento = DocumentoDoColaborador.Anexar(pessoa, campos.Tipo, novo.Descricao, campos.DataValidade, caminho, nomeGuardado, relogio.AgoraUtc);
                    await documentos.AdicionarAsync(documento, ct);
                    return DocumentoColaborador.De(documento);
                },
                ct),
            ct);
    }

    public async Task<LinkTemporario> BaixarAsync(int funcionarioId, int documentoId, CancellationToken ct)
    {
        // De outra pessoa ou inexistente: o mesmo 404 (o repositório só devolve o documento que é da pessoa da rota).
        var documento = await documentos.ObterDaPessoaAsync(funcionarioId, documentoId, ct) ?? throw new NaoEncontradoException();
        if (documento.Situacao == SituacaoDoDocumento.Pendente || documento.CaminhoArquivo is not { } caminho)
        {
            throw MensagensDoApoio.DocumentoSemArquivo.ComoErroDeNegocio();
        }

        // O registro vem PRIMEIRO (fora de transação, em contexto próprio): se ele falhar, não há link.
        var rotulo = RotulosDeDocumento.Tipo(documento.Tipo);
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(
                AuditoriaArea.Documentos,
                AuditoriaAcao.Baixou,
                $"Baixou o documento {rotulo} de {documento.Funcionario.Nome}",
                documento.RegistroAfetado,
                "FUNCIONARIOS_DOCUMENTOS",
                documento.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(caminho, documento.NomeArquivoOriginal ?? string.Empty, ct);
    }

    /// <summary>Todos os erros de campo e de arquivo de uma vez (400, com o nome do campo do contrato), antes de qualquer efeito.</summary>
    private static async Task<Campos> ValidarAsync(NovoDocumentoDoRh novo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var modo = LerEnum<ModoDeDocumento>(erros, novo.Modo, "modo");
        var tipo = LerEnum<TipoDeDocumento>(erros, novo.Tipo, "tipo");
        ValidacaoDosCadastros.Descricao(erros, novo.Descricao);
        var validade = LerData(erros, novo.DataValidade);

        TipoDeArquivo? tipoDoArquivo = null;
        if (modo == ModoDeDocumento.PedirParaEnviar)
        {
            if (novo.ArquivosRecebidos > 0)
            {
                erros.Adicionar("arquivo", "Neste modo não se envia arquivo: a pessoa o envia pelo portal.");
            }
        }
        else if (modo == ModoDeDocumento.AnexarAgora)
        {
            tipoDoArquivo = await ValidacaoDeArquivoDeDocumento.ValidarAsync(erros, novo.Arquivo, novo.ArquivosRecebidos, "O arquivo é obrigatório neste modo.", ct);
        }

        erros.LancarSeHouver();
        return new Campos(modo!.Value, tipo!.Value, validade, tipoDoArquivo);
    }

    private static TEnum? LerEnum<TEnum>(ColetorDeErros erros, string? texto, string campo)
        where TEnum : struct, Enum
    {
        var valores = Enum.GetValues<TEnum>();
        if (!string.IsNullOrEmpty(texto))
        {
            foreach (var valor in valores)
            {
                if (string.Equals(NomesDoContrato.Enum(valor), texto, StringComparison.Ordinal))
                {
                    return valor;
                }
            }
        }

        erros.Adicionar(campo, $"{(string.IsNullOrEmpty(texto) ? "O campo é obrigatório." : "Valor inválido.")} Use um destes: {string.Join(", ", valores.Select(NomesDoContrato.Enum))}.");
        return null;
    }

    private static DateOnly? LerData(ColetorDeErros erros, string? texto)
    {
        if (string.IsNullOrWhiteSpace(texto))
        {
            return null;
        }

        if (DateOnly.TryParseExact(texto, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var data))
        {
            return data;
        }

        erros.Adicionar("dataValidade", "A data de validade precisa estar no formato AAAA-MM-DD (por exemplo 2027-10-01).");
        return null;
    }
}
