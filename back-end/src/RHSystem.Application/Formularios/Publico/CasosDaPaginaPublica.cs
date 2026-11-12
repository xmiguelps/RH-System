using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Publico;

/// <summary>
/// Os valores de partida do formulário público (<c>Formularios:...</c> em <c>appsettings.json</c>): quantos dias os dados de um candidato ficam guardados antes do job apagá-los (180: hipótese do contrato, <c>x-provisorio</c>; só o valor muda se o dono decidir outro).
/// </summary>
public sealed class OpcoesDoFormularioPublico
{
    public const string Secao = "Formularios";

    public int RetencaoDeCandidatosDias { get; set; } = 180;
}

/// <summary>O que a página pública do candidato mostra (<c>FormularioPaginaPublica</c>): o nome da empresa, o formulário, o aviso de privacidade com o prazo de retenção e as perguntas. <b>Sem o <c>id</c> do formulário</b>, sem o código do link e sem nada do RH.</summary>
public sealed record FormularioPaginaPublica(string EmpresaNome, string Titulo, string? Descricao, DateOnly? Prazo, string AvisoDePrivacidade, int RetencaoDias, IReadOnlyList<PerguntaFormulario> Perguntas);

/// <summary>
/// <c>GET /api/publico/formularios/{token}</c> (sem login; o limite de 30 consultas por minuto por IP roda ANTES de tudo): abre o formulário do candidato pelo código do link. Código fora do padrão, inexistente, de rascunho ou de um formulário que não é por link: o MESMO 404 (nunca 400, para ninguém varrer o formato).
/// Agendado: 422 <c>FORMULARIO_AINDA_NAO_ABERTO</c>; encerrado, inclusive o prazo vencido com a tarefa de encerrar parada: 422 <c>FORMULARIO_ENCERRADO</c>; empresa ainda sem configuração: 422 <c>EMPRESA_NAO_CONFIGURADA</c> (o nome dela vai no cabeçalho). O código do link nunca vai a log nem a auditoria.
/// </summary>
public sealed class ObterFormularioPublicoCasoDeUso(IConsultaDeFormularios consulta, IConfiguracaoDaEmpresa empresa, OpcoesDoFormularioPublico opcoes, IRelogio relogio)
{
    public async Task<FormularioPaginaPublica> ExecutarAsync(string token, CancellationToken ct)
    {
        var lido = await AcessoPublico.ObterAsync(consulta, token, relogio.AgoraUtc, ct);
        var configuracao = await empresa.ObterAsync(ct) ?? throw MensagensDosFormularios.EmpresaNaoConfigurada.ComoProblema();
        var retencao = opcoes.RetencaoDeCandidatosDias;
        return new FormularioPaginaPublica(configuracao.NomeEmpresa, lido.Titulo, lido.Descricao, lido.Prazo, AvisoDePrivacidade(retencao), retencao, [.. lido.Perguntas.OrderBy(p => p.Ordem).Select(MapeadorDoFormulario.Pergunta)]);
    }

    /// <summary>O texto (markdown) que o candidato aceita antes de enviar: o que é guardado, quem vê e por quanto tempo.</summary>
    public static string AvisoDePrivacidade(int retencaoDias) =>
        $"Usamos seu nome, e-mail, telefone, currículo e respostas só para avaliar a sua candidatura. Só o RH da empresa acessa esses dados. Guardamos por até {retencaoDias} dias e depois apagamos. Se quiser que apaguemos antes, fale com o RH.";
}

/// <summary>A porta de entrada do link público (a mesma da página e do envio): o formulário PUBLICADO que o código abre, ou o 404 único / o 422 do estado.</summary>
public static class AcessoPublico
{
    public static async Task<FormularioLido> ObterAsync(IConsultaDeFormularios consulta, string? token, DateTime agoraUtc, CancellationToken ct)
    {
        if (!TokenDoLink.Valido(token) || await consulta.IdPorTokenAsync(token!, ct) is not { } id)
        {
            throw new NaoEncontradoException();
        }

        var lido = (await consulta.ObterAsync(id, ct)).OuNaoEncontrado();
        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, agoraUtc);
        if (situacao == SituacaoDoFormulario.Rascunho || lido.Alvo != AlvoDoFormulario.Link)
        {
            throw new NaoEncontradoException();
        }

        return situacao switch
        {
            SituacaoDoFormulario.Agendado => throw MensagensDosFormularios.FormularioAindaNaoAberto.ComoProblema(),
            SituacaoDoFormulario.Encerrado => throw MensagensDosFormularios.FormularioEncerradoParaResponder.ComoProblema(),
            _ => lido,
        };
    }
}
