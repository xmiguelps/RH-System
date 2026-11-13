using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;

namespace RHSystem.Application.Arquivos;

/// <summary>
/// O arquivo de uma resposta de formulário (a pergunta de envio de arquivo e o currículo do candidato): PDF ou Word (<c>.doc</c>, <c>.docx</c>) de até 5 MiB, o tipo vem do CONTEÚDO (nunca do nome nem do <c>Content-Type</c>): um PNG, um ZIP qualquer renomeado <c>.docx</c> e um arquivo vazio dão 400.
/// A regra existe num lugar só para as rotas do funcionário e as do candidato.
/// </summary>
public static class ValidacaoDeArquivoDeFormulario
{
    public const long TamanhoMaximoBytes = 5L * 1024 * 1024;

    /// <summary>O teto do corpo de uma resposta: até 10 arquivos de 5 MiB e mais 1 MiB do resto (o texto das respostas). Acima disso a requisição nem é lida (413).</summary>
    public const long TetoDaRequisicaoBytes = (10 * TamanhoMaximoBytes) + (1024 * 1024);

    /// <summary>O teto do corpo de uma candidatura: o currículo e até 10 arquivos de resposta de 5 MiB e mais 1 MiB do resto.</summary>
    public const long TetoDaCandidaturaBytes = (11 * TamanhoMaximoBytes) + (1024 * 1024);

    public const string MensagemDoTipo = "Envie um arquivo PDF ou Word (.doc ou .docx).";

    public const string MensagemDoTamanho = "Cada arquivo pode ter até 5 MB.";

    public const string MensagemVazio = "O arquivo está vazio.";

    /// <summary>O tipo que o conteúdo revelou, ou <c>null</c> (e o erro vai para <paramref name="erros"/> em <paramref name="campo"/>).</summary>
    public static async Task<TipoDeArquivo?> ValidarAsync(ColetorDeErros erros, string campo, ArquivoRecebido arquivo, CancellationToken ct)
    {
        if (arquivo.Tamanho <= 0)
        {
            erros.Adicionar(campo, MensagemVazio);
            return null;
        }

        if (arquivo.Tamanho > TamanhoMaximoBytes)
        {
            erros.Adicionar(campo, MensagemDoTamanho);
            return null;
        }

        var inicio = new byte[8];
        int lidos;
        await using (var conteudo = arquivo.Abrir())
        {
            lidos = await conteudo.ReadAtLeastAsync(inicio, inicio.Length, throwOnEndOfStream: false, ct);
        }

        var tipo = Identificar(inicio.AsSpan(0, lidos), arquivo);
        if (tipo is null)
        {
            erros.Adicionar(campo, MensagemDoTipo);
        }

        return tipo;
    }

    private static TipoDeArquivo? Identificar(ReadOnlySpan<byte> inicio, ArquivoRecebido arquivo)
    {
        if (IdentificadorDeTipoDeArquivo.Identificar(inicio) is { TipoDeConteudo: "application/pdf" } pdf)
        {
            return pdf;
        }

        if (TipoWord.EhOle2(inicio))
        {
            return TipoWord.Doc;
        }

        if (TipoWord.ComecaComoZip(inicio))
        {
            using var conteudo = arquivo.Abrir();
            return TipoWord.TemODocumentoDoWord(conteudo) ? TipoWord.Docx : null;
        }

        return null;
    }
}
