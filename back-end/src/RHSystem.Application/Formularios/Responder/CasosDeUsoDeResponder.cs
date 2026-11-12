using System.Globalization;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// <c>POST /api/eu/formularios/{formularioId}/respostas</c> (multipart: <c>respostas</c> em JSON, <c>arquivos</c> e <c>avaliadoId</c>), a pessoa logada respondendo, COM NOME ou em formulário ANÔNIMO. Ordem das recusas: 404 (não existe, rascunho, por link ou a pessoa não deve responder), 422 situação (agendado, encerrado, inclusive o
/// prazo vencido com a tarefa de encerrar parada), 400 (todos os campos e arquivos juntos), 422 <c>FUNCIONARIO_INATIVO</c>, 422 <c>AVALIADO_INVALIDO</c>, 409 <c>JA_RESPONDIDO</c> (uma resposta por pessoa e por avaliado; a Sugestão aceita várias). Tudo ou nada: os arquivos vão para o armazenamento fora de
/// transação, UMA transação curta segura a linha do formulário, confere de novo se a pessoa já respondeu e grava todas as linhas com o MESMO instante (cortado ao segundo; na Sugestão, se já houve envio naquele segundo, o seguinte); falhou, os blobs são apagados.
/// <b>No anônimo</b> não há arquivo (400 em <c>arquivos</c> se vier) e as linhas saem SEM autor e com a data do dia da publicação da rodada, mais a participação (UUID v4 no recibo, a chave única dá o 409 mesmo numa corrida). Nem com nome nem anônimo este caso de uso grava auditoria.
/// </summary>
public sealed class ResponderFormularioCasoDeUso(
    IConsultaDeFormularios consulta,
    IConsultaDeMeusFormularios meus,
    IFormulariosRespostasRepositorio respostas,
    EnvioDeArquivosDeFormulario envio,
    IUnidadeDeTrabalho unidade,
    IEscopoDoUsuario escopo,
    IRelogio relogio)
{
    public async Task<RespostaFormularioRecibo> ExecutarAsync(int formularioId, NovaRespostaDeFormulario novo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var eu = escopo.FuncionarioId;
        var acesso = await AcessoAoFormularioParaResponder.ConferirAsync(consulta, meus, formularioId, eu, agora, ct);
        var lido = acesso.Lido;
        var erros = new ColetorDeErros();
        var avaliadoId = LerAvaliado(erros, lido, novo.AvaliadoId);
        var linhas = ValidacaoDaResposta.Validar(erros, lido.Perguntas, novo.Respostas, novo.Arquivos.Count);
        var conferidos = lido.Anonimo ? SemArquivos(erros, novo) : await ConferirArquivosAsync(erros, novo, ct);
        erros.LancarSeHouver();

        await ConferirPessoaEParAsync(lido, acesso.Posicao, eu, avaliadoId, ct);

        var participacao = await envio.GravarEEfetivarAsync(conferidos, caminhos => GravarAsync(lido, eu, avaliadoId, linhas, conferidos, caminhos, agora, ct), ct);
        return new RespostaFormularioRecibo(lido.Id, participacao?.ToString("D"), Restantes(lido, acesso.Posicao, eu, avaliadoId));
    }

    /// <summary>A transação curta: segura o formulário, confere de novo, grava. Se o formulário já não estava publicado (outra aba o encerrou), lança DEPOIS da transação, o que faz o envio apagar os arquivos. Devolve a participação (só no anônimo).</summary>
    private async Task<Guid?> GravarAsync(FormularioLido lido, int eu, int? avaliadoId, IReadOnlyList<RespostaValidada> linhas, IReadOnlyList<ArquivoDeRespostaConferido> conferidos, IReadOnlyList<string> caminhos, DateTime agora, CancellationToken ct)
    {
        var sugestao = lido.Tipo == TipoDeFormulario.Outro;
        var gravacao = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await respostas.SegurarAbertoAsync(lido.Id, lido.Anonimo, ct))
                {
                    return new Gravacao(false, null);
                }

                if (lido.Anonimo)
                {
                    if (await respostas.JaParticipouAsync(lido.Id, eu, avaliadoId, ct))
                    {
                        throw MensagensDosFormularios.JaRespondidoPeloFuncionario.ComoProblema();
                    }

                    var dia = InstantesDoFormulario.DiaDaRodada(lido.PublicadoEmUtc!.Value); // a data grossa: o dia da publicação da rodada, igual para todos
                    return new Gravacao(true, await respostas.GravarAnonimaAsync(lido.Id, eu, avaliadoId, dia, [.. linhas.Select(l => ParaGravar(l, caminhos, conferidos))], ct));
                }

                if (!sugestao && await respostas.JaRespondeuAsync(lido.Id, eu, avaliadoId, ct))
                {
                    throw MensagensDosFormularios.JaRespondidoPeloFuncionario.ComoProblema();
                }

                var momento = InstanteAoSegundo.De(agora);
                if (sugestao && await respostas.UltimoEnvioAsync(lido.Id, eu, ct) is { } ultimo && ultimo >= momento)
                {
                    momento = ultimo.AddSeconds(1); // dois envios da Sugestão no mesmo segundo: o instante separa um do outro
                }

                await respostas.GravarDeFuncionarioAsync(lido.Id, eu, avaliadoId, momento, [.. linhas.Select(l => ParaGravar(l, caminhos, conferidos))], ct);
                return new Gravacao(true, null);
            },
            ct);
        if (!gravacao.Gravou)
        {
            await RecusarFormularioQueMudouAsync(lido.Id, agora, ct);
        }

        return gravacao.Participacao;
    }

    private sealed record Gravacao(bool Gravou, Guid? Participacao);

    private static LinhaParaGravar ParaGravar(RespostaValidada linha, IReadOnlyList<string> caminhos, IReadOnlyList<ArquivoDeRespostaConferido> conferidos) =>
        linha.ArquivoIndice is { } indice
            ? new LinhaParaGravar(linha.Pergunta.Id, conferidos[indice].Nome, null, caminhos[indice], conferidos[indice].Nome)
            : new LinhaParaGravar(linha.Pergunta.Id, linha.Resposta, linha.AlternativaId, null, null);

    /// <summary>O <c>avaliadoId</c>: obrigatório (um inteiro de 1 em diante) nas avaliações e proibido nos outros tipos.</summary>
    private static int? LerAvaliado(ColetorDeErros erros, FormularioLido lido, string? texto)
    {
        var informado = !string.IsNullOrWhiteSpace(texto);
        if (lido.Tipo != TipoDeFormulario.Avaliacao)
        {
            if (informado)
            {
                erros.Adicionar("avaliadoId", "Este formulário não tem avaliado.");
            }

            return null;
        }

        if (!informado)
        {
            erros.Adicionar("avaliadoId", "Informe quem está sendo avaliado.");
            return null;
        }

        if (!int.TryParse(texto, NumberStyles.None, CultureInfo.InvariantCulture, out var id) || id < 1)
        {
            erros.Adicionar("avaliadoId", "O avaliado precisa ser um número de 1 em diante.");
            return null;
        }

        return id;
    }

    /// <summary>O formulário anônimo nunca tem arquivo (nem pergunta de arquivo, nem parte <c>arquivos</c>): qualquer parte de arquivo é 400 em <c>arquivos</c>.</summary>
    private static IReadOnlyList<ArquivoDeRespostaConferido> SemArquivos(ColetorDeErros erros, NovaRespostaDeFormulario novo)
    {
        if (novo.ArquivosRecebidos > 0)
        {
            erros.Adicionar("arquivos", "Este formulário é anônimo e não recebe arquivos.");
        }

        return [];
    }

    private static async Task<IReadOnlyList<ArquivoDeRespostaConferido>> ConferirArquivosAsync(ColetorDeErros erros, NovaRespostaDeFormulario novo, CancellationToken ct)
    {
        if (novo.ArquivosRecebidos != novo.Arquivos.Count)
        {
            erros.Adicionar("arquivos", "Envie os arquivos na parte \"arquivos\".");
        }

        var conferidos = new List<ArquivoDeRespostaConferido>(novo.Arquivos.Count);
        foreach (var arquivo in novo.Arquivos)
        {
            if (await ValidacaoDeArquivoDeFormulario.ValidarAsync(erros, "arquivos", arquivo, ct) is { } tipo)
            {
                conferidos.Add(new ArquivoDeRespostaConferido(arquivo, tipo, NomeParaDownload.Limpar(arquivo.NomeOriginal, tipo.Extensao)));
            }
        }

        return conferidos;
    }

    /// <summary>A pessoa está ativa, o avaliado é um dos dela e ela ainda não respondeu (conferência rápida antes de gravar arquivo; a que vale é a da transação).</summary>
    private async Task ConferirPessoaEParAsync(FormularioLido lido, PosicaoDaPessoaNoFormulario posicao, int eu, int? avaliadoId, CancellationToken ct)
    {
        if ((await consulta.PessoasAsync([eu], ct)).FirstOrDefault() is { Ativo: false })
        {
            throw MensagensDosFormularios.PessoaInativaParaResponder.ComoProblema();
        }

        var par = new ParDeResposta(eu, avaliadoId);
        if (lido.Tipo == TipoDeFormulario.Outro)
        {
            if (!posicao.MeusPares.Contains(par))
            {
                throw new NaoEncontradoException();
            }

            return;
        }

        if (posicao.Feitos.Contains(par))
        {
            throw MensagensDosFormularios.JaRespondidoPeloFuncionario.ComoProblema();
        }

        if (!posicao.MeusPares.Contains(par))
        {
            throw lido.Tipo == TipoDeFormulario.Avaliacao ? MensagensDosFormularios.AvaliadoInvalido.ComoProblema() : new NaoEncontradoException();
        }
    }

    /// <summary>A linha do formulário não estava mais publicada na hora de gravar (outra aba o encerrou): relê e dá o erro certo.</summary>
    private async Task RecusarFormularioQueMudouAsync(int formularioId, DateTime agora, CancellationToken ct)
    {
        var depois = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        throw RegraDaSituacaoDoFormulario.De(depois.Ativo, depois.PublicadoEmUtc, depois.EncerradoEmUtc, depois.Prazo, agora) == SituacaoDoFormulario.Encerrado
            ? MensagensDosFormularios.FormularioEncerradoParaResponder.ComoProblema()
            : MensagensDosFormularios.FormularioMudouEmOutraTela;
    }

    /// <summary>As avaliações que ainda faltam à pessoa depois deste envio (0 fora das avaliações).</summary>
    private static int Restantes(FormularioLido lido, PosicaoDaPessoaNoFormulario posicao, int eu, int? avaliadoId) =>
        lido.Tipo == TipoDeFormulario.Avaliacao
            ? posicao.MeusPares.Count(p => p != new ParDeResposta(eu, avaliadoId) && !posicao.Feitos.Contains(p))
            : 0;
}

/// <summary>
/// <c>GET /api/eu/formularios/{formularioId}/minhas-respostas</c>: o que a pessoa logada respondeu (só COM NOME). 404 se o formulário não existe, é rascunho ou por link, ou a pessoa não respondeu (nas avaliações, para aquele avaliado); 422 <c>FORMULARIO_ANONIMO</c> no anônimo e 422
/// <c>FORMULARIO_SUGESTAO</c> na Sugestão; <c>avaliadoId</c> é obrigatório nas avaliações e proibido nos outros tipos (400). Vem TODA pergunta do formulário, na ordem; a opcional em branco vem com tudo vazio. O arquivo aparece só com o nome (não há download de volta).
/// </summary>
public sealed class ObterMinhasRespostasCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeMeusFormularios meus, IFormulariosRespostasRepositorio respostas, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<RespostaFormularioMinhas> ExecutarAsync(int formularioId, int? avaliadoIdInformado, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        if (lido.Alvo is not (AlvoDoFormulario.Pessoas or AlvoDoFormulario.Setores) || RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, relogio.AgoraUtc) == SituacaoDoFormulario.Rascunho)
        {
            throw new NaoEncontradoException();
        }

        if (lido.Anonimo)
        {
            throw MensagensDosFormularios.FormularioAnonimo.ComoProblema();
        }

        if (lido.Tipo == TipoDeFormulario.Outro)
        {
            throw MensagensDosFormularios.SugestaoNasMinhasRespostas.ComoProblema();
        }

        var erros = new ColetorDeErros();
        var avaliadoId = LerAvaliadoDaConsulta(erros, lido, avaliadoIdInformado);
        erros.LancarSeHouver();

        var eu = escopo.FuncionarioId;
        var lidas = await respostas.DaPessoaAsync(lido.Id, eu, avaliadoId, ct);
        if (lidas.Count == 0)
        {
            throw new NaoEncontradoException();
        }

        var avaliado = avaliadoId is { } id ? (await meus.ResumosAsync([id], ct)).FirstOrDefault() : null;
        return new RespostaFormularioMinhas(lido.Id, lido.Titulo, avaliado, lidas.Max(l => l.RespondidaEmUtc) ?? default, MontadorDeRespostasDadas.Montar(lido, lidas));
    }

    private static int? LerAvaliadoDaConsulta(ColetorDeErros erros, FormularioLido lido, int? informado)
    {
        if (lido.Tipo != TipoDeFormulario.Avaliacao)
        {
            if (informado is not null)
            {
                erros.Adicionar("avaliadoId", "Este formulário não tem avaliado.");
            }

            return null;
        }

        if (informado is not { } id || id < 1)
        {
            erros.Adicionar("avaliadoId", "Informe de qual avaliação se fala (o avaliado, um número de 1 em diante).");
            return null;
        }

        return id;
    }
}
