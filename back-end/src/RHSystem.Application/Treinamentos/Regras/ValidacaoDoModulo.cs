using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Regras;

/// <summary>O módulo conferido: só o campo do tipo vem preenchido (os outros ficam <c>null</c>, o que a coluna do banco espera) e o título aparado. O arquivo do PDF é conferido à parte (pelo conteúdo).</summary>
public sealed record CamposDoModulo(string Titulo, TipoDeModulo Tipo, short? DuracaoMinutos, string? UrlExterna, string? ConteudoTexto);

/// <summary>
/// Confere o módulo (<c>TreinamentoModuloComando</c>): título de 1 a 200, duração de 1 a 32767 minutos, <c>urlExterna</c> até 500 caracteres começando por <c>http://</c> ou <c>https://</c>, <c>conteudoTexto</c> até 50000. Cada tipo exige o seu campo e recusa os dos outros: <c>VIDEO</c> e <c>LINK</c> o endereço
/// (o vídeo aceita um endereço OU um arquivo MP4/WebM, nunca os dois nem nenhum; decisão do dono de 2026-11-17), <c>TEXTO</c> o texto, <c>PDF</c> o arquivo (na edição, sem arquivo novo o atual fica, e o vídeo que já tem arquivo fica sem nada novo ou vira link com um endereço). Texto vazio num campo de outro tipo vale como ausente (o formulário multipart costuma mandar <c>""</c>). Todos os erros juntos; com algum erro devolve <c>null</c>.
/// </summary>
public static class ValidacaoDoModulo
{
    public const int MaximoDoTitulo = 200;
    public const int MaximoDoEndereco = 500;
    public const int MaximoDoTexto = 50000;

    public static CamposDoModulo? Validar(ColetorDeErros erros, string? titulo, TipoDeModulo? tipo, int? duracaoMinutos, string? urlExterna, string? conteudoTexto, bool temArquivoNovo, bool jaTemArquivo)
    {
        var valido = true;
        var tituloAparado = (titulo ?? string.Empty).Trim();
        if (tituloAparado.Length == 0 || ContagemDeCaracteres.De(tituloAparado) > MaximoDoTitulo)
        {
            erros.Adicionar("titulo", "Informe o título do módulo, com até 200 caracteres.");
            valido = false;
        }

        if (duracaoMinutos is < 1 or > short.MaxValue)
        {
            erros.Adicionar("duracaoMinutos", "A duração vai de 1 a 32767 minutos.");
            valido = false;
        }

        if (tipo is null)
        {
            erros.Adicionar("tipo", "Escolha o tipo do módulo: vídeo, texto, link ou PDF.");
            return null;
        }

        var endereco = string.IsNullOrWhiteSpace(urlExterna) ? null : urlExterna.Trim();
        var texto = string.IsNullOrWhiteSpace(conteudoTexto) ? null : conteudoTexto;
        var textoVazioMasPreenchido = conteudoTexto is not null && texto is null && tipo == TipoDeModulo.Texto;
        if (endereco is not null && (endereco.Length > MaximoDoEndereco || !(endereco.StartsWith("http://", StringComparison.OrdinalIgnoreCase) || endereco.StartsWith("https://", StringComparison.OrdinalIgnoreCase))))
        {
            erros.Adicionar("urlExterna", "Use um endereço que comece com http:// ou https://, com até 500 caracteres.");
            valido = false;
        }
        else if (endereco is null && tipo == TipoDeModulo.Link)
        {
            erros.Adicionar("urlExterna", "Informe o endereço do módulo.");
            valido = false;
        }
        else if (endereco is null && tipo == TipoDeModulo.Video && !temArquivoNovo && !jaTemArquivo)
        {
            erros.Adicionar("urlExterna", "Informe o endereço do vídeo ou envie o arquivo.");
            valido = false;
        }
        else if (endereco is not null && tipo is TipoDeModulo.Texto or TipoDeModulo.Pdf)
        {
            erros.Adicionar("urlExterna", "Este tipo de módulo não tem endereço.");
            valido = false;
        }

        if (texto is not null && ContagemDeCaracteres.De(texto) > MaximoDoTexto)
        {
            erros.Adicionar("conteudoTexto", "O texto pode ter até 50000 caracteres.");
            valido = false;
        }
        else if (texto is null && tipo == TipoDeModulo.Texto)
        {
            erros.Adicionar("conteudoTexto", textoVazioMasPreenchido ? "O texto do módulo não pode ficar em branco." : "Informe o texto do módulo.");
            valido = false;
        }
        else if (texto is not null && tipo != TipoDeModulo.Texto)
        {
            erros.Adicionar("conteudoTexto", "Este tipo de módulo não tem texto.");
            valido = false;
        }

        if (tipo == TipoDeModulo.Pdf && !temArquivoNovo && !jaTemArquivo)
        {
            erros.Adicionar("arquivo", "Envie o arquivo PDF do módulo.");
            valido = false;
        }
        else if (tipo == TipoDeModulo.Video && temArquivoNovo && endereco is not null)
        {
            erros.Adicionar("arquivo", "Escolha um só: o link ou o arquivo de vídeo.");
            valido = false;
        }
        else if (tipo is not (TipoDeModulo.Pdf or TipoDeModulo.Video) && temArquivoNovo)
        {
            erros.Adicionar("arquivo", "Este tipo de módulo não tem arquivo.");
            valido = false;
        }

        return valido ? new CamposDoModulo(tituloAparado, tipo.Value, (short?)duracaoMinutos, tipo is TipoDeModulo.Video or TipoDeModulo.Link ? endereco : null, tipo == TipoDeModulo.Texto ? texto : null) : null;
    }
}
