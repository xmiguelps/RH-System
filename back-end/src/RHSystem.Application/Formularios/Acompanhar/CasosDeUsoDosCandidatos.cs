using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Acompanhar;

/// <summary>
/// O nome com que o RH baixa o currículo: <c>curriculo-&lt;nome do candidato&gt;&lt;extensão&gt;</c>, gerado aqui (o nome do arquivo que o candidato mandou não é guardado nem usado). Barra e contrabarra do nome viram <c>-</c> (não viram diretório) e o resto passa pela mesma limpeza dos outros downloads;
/// o nome do candidato é cortado ANTES de juntar a extensão, para a extensão nunca se perder no limite de 100 caracteres.
/// </summary>
public static class NomeDoCurriculo
{
    private const string Prefixo = "curriculo-";

    public static string De(string nomeDoCandidato, string extensao)
    {
        var nome = nomeDoCandidato.Replace('/', '-').Replace('\\', '-');
        var limpo = NomeParaDownload.Limpar(nome, string.Empty);
        var cabe = Math.Max(1, NomeParaDownload.TamanhoMaximo - Prefixo.Length - extensao.Length);
        if (limpo.Length > cabe)
        {
            limpo = limpo[..cabe];
            if (char.IsHighSurrogate(limpo[^1]))
            {
                limpo = limpo[..^1]; // não deixa um par substituto cortado ao meio
            }

            limpo = limpo.TrimEnd();
        }

        return Prefixo + limpo + extensao;
    }
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/candidatos</c>: a lista dos candidatos de um formulário por link (só o RH). 404 (não existe), 400 (<c>pagina</c>, <c>tamanhoPagina</c>, <c>busca</c>, juntos), 422 <c>FORMULARIO_SEM_LINK</c> (a lista dos outros é a de funcionários). Os mais recentes primeiro
/// (empate pelo número, o maior primeiro); <c>busca</c> procura no nome e no e-mail, sem diferenciar caixa nem acento. Dado pessoal (LGPD): só o RH; o caminho do currículo nunca sai (só <c>temCurriculo</c>).
/// </summary>
public sealed class ListarCandidatosDoFormularioCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeAcompanhamento acompanhamento)
{
    public async Task<CandidatoLista> ExecutarAsync(int formularioId, int pagina, int tamanhoPagina, string? busca, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        var paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        if (lido.Alvo != AlvoDoFormulario.Link)
        {
            throw MensagensDosFormularios.FormularioSemLink.ComoProblema();
        }

        var (itens, total) = await acompanhamento.CandidatosAsync(lido.Id, paginacao.Busca, paginacao.Pular, paginacao.TamanhoPagina, ct);
        var lista = RespostaPaginada<CandidatoResumo>.De([.. itens.Select(Resumo)], total, paginacao);
        return new CandidatoLista(lista.Itens, lista.Pagina, lista.TamanhoPagina, lista.Total, lista.TotalPaginas);
    }

    internal static CandidatoResumo Resumo(CandidatoLido c) => new(c.Id, c.Nome, c.Email, c.Telefone, c.CaminhoDoCurriculo is not null, c.CadastradoEmUtc);
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/respostas/candidatos/{candidatoId}</c>: o que um candidato respondeu (só o RH). 404 (formulário, candidato que não existe ou é de OUTRO formulário), 422 <c>FORMULARIO_SEM_LINK</c>. Vem TODA pergunta do formulário, na ordem (a opcional em branco vem vazia); o arquivo só com
/// <c>respostaId</c> e nome (o link sai por <c>baixarArquivoDeResposta</c>).
/// </summary>
public sealed class ObterRespostasDeCandidatoCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeAcompanhamento acompanhamento)
{
    public async Task<RespostaFormularioCandidato> ExecutarAsync(int formularioId, int candidatoId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Alvo != AlvoDoFormulario.Link)
        {
            throw MensagensDosFormularios.FormularioSemLink.ComoProblema();
        }

        var candidato = (await acompanhamento.CandidatoAsync(lido.Id, candidatoId, ct)).OuNaoEncontrado();
        var lidas = await acompanhamento.DoCandidatoAsync(lido.Id, candidato.Id, ct);
        return new RespostaFormularioCandidato(lido.Id, lido.Titulo, ListarCandidatosDoFormularioCasoDeUso.Resumo(candidato), MontadorDeRespostasDadas.Montar(lido, lidas));
    }
}

/// <summary>
/// <c>GET /api/rh/formularios/{formularioId}/candidatos/{candidatoId}/curriculo/download</c>: o link temporário do currículo (só o RH), que baixa como anexo <c>curriculo-&lt;nome&gt;.&lt;ext&gt;</c>; o caminho do armazenamento nunca aparece. 404 (formulário, candidato de outro formulário e candidato SEM currículo), 422
/// <c>FORMULARIO_SEM_LINK</c>. Cada link pedido grava o evento <c>BAIXOU</c> só com o título do formulário (nunca o nome nem o e-mail do candidato); o 404 não grava.
/// </summary>
public sealed class BaixarCurriculoDeCandidatoCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeAcompanhamento acompanhamento, IAuditor auditor, IArmazenamentoDeArquivos armazenamento)
{
    public async Task<LinkTemporario> ExecutarAsync(int formularioId, int candidatoId, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Alvo != AlvoDoFormulario.Link)
        {
            throw MensagensDosFormularios.FormularioSemLink.ComoProblema();
        }

        var candidato = (await acompanhamento.CandidatoAsync(lido.Id, candidatoId, ct)).OuNaoEncontrado();
        var caminho = candidato.CaminhoDoCurriculo ?? throw new NaoEncontradoException();
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Baixou, $"Baixou um currículo do formulário \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", lido.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(caminho, NomeDoCurriculo.De(candidato.Nome, Path.GetExtension(caminho)), ct);
    }
}
