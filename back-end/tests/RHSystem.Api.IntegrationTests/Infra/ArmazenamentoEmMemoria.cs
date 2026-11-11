using System.Runtime.CompilerServices;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Infra;

/// <summary>
/// O armazenamento de arquivos dos testes da API (o <c>ApiFactory</c> o registra no lugar do Blob): guarda os bytes e o tipo em memória, gera links <c>https://arquivos.exemplo.test/...</c> e deixa o teste provocar
/// falha (<see cref="FalharNaGravacao"/>, <see cref="FalharNaExclusao"/>), parar no meio da gravação (<see cref="AoGravar"/>, para os testes de corrida) e conferir o que aconteceu (<see cref="Gravados"/>,
/// <see cref="Apagados"/>, <see cref="GravacoesComTransacaoAberta"/>). Como o de verdade: não sobrescreve, recusa caminho inválido e apagar o que não existe não lança. Só <c>ArmazenamentoBlobTests</c> fala com o Azurite.
/// </summary>
public sealed class ArmazenamentoEmMemoria(IRelogio relogio) : IArmazenamentoDeArquivos, IListagemDeArquivos
{
    private sealed record Item(byte[] Bytes, string TipoDeConteudo, DateTime CriadoEmUtc);

    private readonly object _trava = new();
    private readonly Dictionary<string, Item> _itens = [];
    private readonly List<string> _gravados = [];
    private readonly List<string> _apagados = [];
    private int _gravacoesComTransacaoAberta;

    /// <summary>Os caminhos gravados com sucesso, na ordem.</summary>
    public IReadOnlyList<string> Gravados => Copia(_gravados);

    /// <summary>Os caminhos apagados de verdade (apagar o que não existia não entra), na ordem.</summary>
    public IReadOnlyList<string> Apagados => Copia(_apagados);

    /// <summary>Se preenchida, <see cref="GravarAsync"/> lança isto (depois de contar e de rodar o <see cref="AoGravar"/>) e não guarda nada.</summary>
    public Exception? FalharNaGravacao { get; set; }

    /// <summary>Se preenchida, <see cref="ApagarAsync"/> lança isto e não apaga nada.</summary>
    public Exception? FalharNaExclusao { get; set; }

    /// <summary>Roda no INÍCIO de <see cref="GravarAsync"/>, com o caminho: o teste segura a gravação aqui para provar o que ainda não aconteceu.</summary>
    public Func<string, Task>? AoGravar { get; set; }

    /// <summary>Quantas vezes <see cref="GravarAsync"/> rodou com uma transação aberta (<see cref="UnidadeDeTrabalhoEspia"/>). O envio de arquivo grava SEM transação: as Tasks 7 e 8 provam que é 0.</summary>
    public int GravacoesComTransacaoAberta => Volatile.Read(ref _gravacoesComTransacaoAberta);

    public int Quantidade
    {
        get
        {
            lock (_trava)
            {
                return _itens.Count;
            }
        }
    }

    public bool Existe(string caminho)
    {
        lock (_trava)
        {
            return _itens.ContainsKey(caminho);
        }
    }

    public byte[]? Conteudo(string caminho)
    {
        lock (_trava)
        {
            return _itens.TryGetValue(caminho, out var item) ? [.. item.Bytes] : null;
        }
    }

    public string? TipoDeConteudo(string caminho)
    {
        lock (_trava)
        {
            return _itens.TryGetValue(caminho, out var item) ? item.TipoDeConteudo : null;
        }
    }

    /// <summary>Põe um arquivo direto, com a data de criação escolhida (para os testes da limpeza e os que já precisam do blob no lugar). Não conta como gravação.</summary>
    public void Semear(string caminho, byte[] bytes, DateTime criadoEmUtc, string tipoDeConteudo = "application/pdf")
    {
        lock (_trava)
        {
            _itens[caminho] = new Item([.. bytes], tipoDeConteudo, criadoEmUtc);
        }
    }

    public void Limpar()
    {
        lock (_trava)
        {
            _itens.Clear();
            _gravados.Clear();
            _apagados.Clear();
        }

        FalharNaGravacao = null;
        FalharNaExclusao = null;
        AoGravar = null;
        Interlocked.Exchange(ref _gravacoesComTransacaoAberta, 0);
    }

    public async Task GravarAsync(string caminho, Stream conteudo, string tipoDeConteudo, CancellationToken ct)
    {
        if (!CaminhosDeArquivo.EhValido(caminho))
        {
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(caminho));
        }

        if (UnidadeDeTrabalhoEspia.EmTransacao)
        {
            Interlocked.Increment(ref _gravacoesComTransacaoAberta);
        }

        if (AoGravar is { } gancho)
        {
            await gancho(caminho);
        }

        if (FalharNaGravacao is { } erro)
        {
            throw erro;
        }

        using var copia = new MemoryStream();
        await conteudo.CopyToAsync(copia, ct);
        lock (_trava)
        {
            if (_itens.ContainsKey(caminho))
            {
                throw new IOException("O arquivo já existe no armazenamento.");
            }

            _itens[caminho] = new Item(copia.ToArray(), tipoDeConteudo, relogio.AgoraUtc);
            _gravados.Add(caminho);
        }
    }

    public Task<LinkTemporario> CriarLinkTemporarioAsync(string caminho, string nomeParaDownload, CancellationToken ct)
    {
        if (!CaminhosDeArquivo.EhValido(caminho))
        {
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(caminho));
        }

        var nome = NomeParaDownload.Limpar(nomeParaDownload, Path.GetExtension(caminho));
        return Task.FromResult(new LinkTemporario($"https://arquivos.exemplo.test/{caminho}?nome={Uri.EscapeDataString(nome)}", relogio.AgoraUtc.AddMinutes(5)));
    }

    public Task ApagarAsync(string caminho, CancellationToken ct)
    {
        if (!CaminhosDeArquivo.EhValido(caminho))
        {
            throw new ArgumentException("Caminho de arquivo inválido.", nameof(caminho));
        }

        if (FalharNaExclusao is { } erro)
        {
            throw erro;
        }

        lock (_trava)
        {
            if (_itens.Remove(caminho))
            {
                _apagados.Add(caminho);
            }
        }

        return Task.CompletedTask;
    }

    public async IAsyncEnumerable<ArquivoGuardado> ListarAsync(string prefixo, [EnumeratorCancellation] CancellationToken ct)
    {
        await Task.CompletedTask;
        List<ArquivoGuardado> lista;
        lock (_trava)
        {
            lista = [.. _itens.Where(i => i.Key.StartsWith(prefixo, StringComparison.Ordinal)).OrderBy(i => i.Key, StringComparer.Ordinal).Select(i => new ArquivoGuardado(i.Key, i.Value.Bytes.LongLength, i.Value.CriadoEmUtc))];
        }

        foreach (var arquivo in lista)
        {
            ct.ThrowIfCancellationRequested();
            yield return arquivo;
        }
    }

    private List<string> Copia(List<string> origem)
    {
        lock (_trava)
        {
            return [.. origem];
        }
    }
}
