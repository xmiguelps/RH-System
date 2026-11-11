using System.Runtime.CompilerServices;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;

namespace RHSystem.Infrastructure.Arquivos;

/// <summary>
/// Os arquivos no Azure Blob Storage (e, em desenvolvimento, no Azurite). O contêiner é privado e criado se faltar; a gravação <b>não sobrescreve</b> (<c>If-None-Match: *</c>); o link de download é um SAS de LEITURA para
/// aquele blob, gerado com a chave da conta, que vale <see cref="OpcoesDeArmazenamento.LinkValidoMinutos"/> a partir do <see cref="IRelogio"/> (começa 1 minuto antes, por causa de diferença de relógio) e baixa como ANEXO. O caminho é
/// validado ANTES de qualquer chamada ao serviço. A connection string nunca vai para o log, para a exceção nem para o link; sem ela o app sobe e a primeira operação falha com mensagem clara.
/// </summary>
public sealed class ArmazenamentoBlob(OpcoesDeArmazenamento opcoes, IRelogio relogio, ILogger<ArmazenamentoBlob> log) : IArmazenamentoDeArquivos, IListagemDeArquivos
{
    private readonly SemaphoreSlim _trava = new(1, 1);
    private BlobContainerClient? _container;

    public async Task GravarAsync(string caminho, Stream conteudo, string tipoDeConteudo, CancellationToken ct)
    {
        ValidarCaminho(caminho);
        ArgumentNullException.ThrowIfNull(conteudo);
        ArgumentException.ThrowIfNullOrWhiteSpace(tipoDeConteudo);
        var blob = (await ContainerAsync(ct)).GetBlobClient(caminho);
        await blob.UploadAsync(
            conteudo,
            new BlobUploadOptions
            {
                HttpHeaders = new BlobHttpHeaders { ContentType = tipoDeConteudo },
                Conditions = new BlobRequestConditions { IfNoneMatch = ETag.All }, // não sobrescreve: se já existe, o serviço recusa
            },
            ct);
    }

    public async Task<LinkTemporario> CriarLinkTemporarioAsync(string caminho, string nomeParaDownload, CancellationToken ct)
    {
        ValidarCaminho(caminho);
        var container = await ContainerAsync(ct);
        var blob = container.GetBlobClient(caminho);
        if (!blob.CanGenerateSasUri)
        {
            throw new InvalidOperationException("O armazenamento não consegue gerar o link temporário: a connection string precisa trazer a chave da conta (Armazenamento:ConexaoBlob).");
        }

        var agora = DateTime.SpecifyKind(relogio.AgoraUtc, DateTimeKind.Utc);
        var expira = agora.AddMinutes(opcoes.LinkValidoMinutos);
        var nome = NomeParaDownload.Limpar(nomeParaDownload, Path.GetExtension(caminho));
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, new DateTimeOffset(expira))
        {
            BlobContainerName = container.Name,
            BlobName = caminho,
            Resource = "b",
            StartsOn = new DateTimeOffset(agora.AddMinutes(-1)),
            Protocol = opcoes.SomenteHttps ? SasProtocol.Https : SasProtocol.HttpsAndHttp,
            ContentDisposition = DisposicaoComoAnexo(nome),
        };
        return new LinkTemporario(blob.GenerateSasUri(sas).ToString(), expira);
    }

    public async Task ApagarAsync(string caminho, CancellationToken ct)
    {
        ValidarCaminho(caminho);
        var blob = (await ContainerAsync(ct)).GetBlobClient(caminho);
        await blob.DeleteIfExistsAsync(DeleteSnapshotsOption.IncludeSnapshots, cancellationToken: ct);
    }

    public async IAsyncEnumerable<ArquivoGuardado> ListarAsync(string prefixo, [EnumeratorCancellation] CancellationToken ct)
    {
        var container = await ContainerAsync(ct);
        await foreach (var item in container.GetBlobsAsync(BlobTraits.None, BlobStates.None, prefixo, ct))
        {
            var criadoEm = item.Properties.CreatedOn ?? item.Properties.LastModified ?? DateTimeOffset.UtcNow;
            yield return new ArquivoGuardado(item.Name, item.Properties.ContentLength ?? 0, criadoEm.UtcDateTime);
        }
    }

    /// <summary>O cabeçalho de anexo: o nome só em ASCII seguro (para quem não lê o <c>filename*</c>) e o nome inteiro em UTF-8 codificado. O nome já vem limpo, sem aspas nem quebra de linha.</summary>
    private static string DisposicaoComoAnexo(string nomeLimpo)
    {
        var ascii = new string([.. nomeLimpo.Select(letra => letra is >= ' ' and <= '~' && letra != '"' && letra != '\\' && letra != '%' && letra != ';' ? letra : '_')]);
        return $"attachment; filename=\"{ascii}\"; filename*=UTF-8''{Uri.EscapeDataString(nomeLimpo)}";
    }

    private static void ValidarCaminho(string caminho)
    {
        if (!CaminhosDeArquivo.EhValido(caminho))
        {
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(caminho));
        }
    }

    private async Task<BlobContainerClient> ContainerAsync(CancellationToken ct)
    {
        if (_container is { } pronto)
        {
            return pronto;
        }

        await _trava.WaitAsync(ct);
        try
        {
            if (_container is null)
            {
                if (string.IsNullOrWhiteSpace(opcoes.ConexaoBlob))
                {
                    log.LogError("O armazenamento de arquivos não está configurado: falta Armazenamento:ConexaoBlob (user-secrets em desenvolvimento, configuração do App Service no Azure).");
                    throw new InvalidOperationException("O armazenamento de arquivos não está configurado (Armazenamento:ConexaoBlob).");
                }

                BlobContainerClient cliente;
                try
                {
                    cliente = new BlobServiceClient(opcoes.ConexaoBlob).GetBlobContainerClient(opcoes.Container);
                }
                catch (Exception erro) when (erro is ArgumentException or FormatException)
                {
                    // A mensagem do SDK pode repetir pedaços da connection string, que tem a chave da conta: nem ela nem a exceção interna sobem.
                    log.LogError("A connection string do armazenamento de arquivos é inválida ({Tipo}).", erro.GetType().Name);
                    throw new InvalidOperationException("A connection string do armazenamento de arquivos é inválida (Armazenamento:ConexaoBlob).");
                }

                await cliente.CreateIfNotExistsAsync(PublicAccessType.None, cancellationToken: ct);
                _container = cliente;
            }

            return _container;
        }
        finally
        {
            _trava.Release();
        }
    }
}
