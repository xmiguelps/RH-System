using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Documentos;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>A conferência do módulo recebido (compartilhada por acrescentar e atualizar): tipo e duração lidos do texto, as regras do tipo, o PDF pelo CONTEÚDO (até 10 MiB, tipo pelos primeiros bytes) e o vídeo por arquivo (MP4 ou WebM pelo conteúdo, até 100 MiB). Devolve os campos e o tipo de arquivo (<c>null</c> sem arquivo novo); com erro, os dois <c>null</c> e os erros em <paramref name="erros"/>.</summary>
internal static class ConferenciaDoModulo
{
    /// <param name="tipoAtual">O tipo que o módulo tem hoje (<c>null</c> ao acrescentar).</param>
    /// <param name="atualTemArquivo">O módulo de hoje já guarda um arquivo. Só vale como "já tem" se o tipo novo é o mesmo (um PDF não serve de vídeo).</param>
    public static async Task<(CamposDoModulo? Campos, TipoDeArquivo? Arquivo)> ConferirAsync(ColetorDeErros erros, NovoModulo novo, TipoDeModulo? tipoAtual, bool atualTemArquivo, CancellationToken ct)
    {
        TipoDeModulo? tipo = null;
        LeituraDeFiltros.Tentar(erros, () => tipo = EnumsDeConsulta.Ler<TipoDeModulo>(novo.Tipo, "tipo"));
        int? duracao = null;
        if (!string.IsNullOrWhiteSpace(novo.DuracaoMinutos))
        {
            if (int.TryParse(novo.DuracaoMinutos.Trim(), System.Globalization.NumberStyles.None, System.Globalization.CultureInfo.InvariantCulture, out var minutos))
            {
                duracao = minutos;
            }
            else
            {
                erros.Adicionar("duracaoMinutos", "A duração vai de 1 a 32767 minutos.");
            }
        }

        var temArquivoNovo = novo.Arquivo is not null;
        var tipoLidoSemErro = tipo is not null || string.IsNullOrEmpty(novo.Tipo);
        var jaTemArquivo = atualTemArquivo && tipoAtual == tipo;
        var campos = tipoLidoSemErro ? ValidacaoDoModulo.Validar(erros, novo.Titulo, tipo, duracao, novo.UrlExterna, novo.ConteudoTexto, temArquivoNovo, jaTemArquivo) : null;
        TipoDeArquivo? arquivo = null;
        if (tipo == TipoDeModulo.Pdf && temArquivoNovo)
        {
            arquivo = await ValidacaoDeArquivoDeDocumento.ValidarPdfAsync(erros, novo.Arquivo, novo.ArquivosRecebidos, "Envie o arquivo PDF do módulo.", LimitesDoModulo.TamanhoMaximoBytes, "O arquivo pode ter até 10 MB.", ct);
        }
        else if (tipo == TipoDeModulo.Video && temArquivoNovo)
        {
            arquivo = await ValidacaoDoVideoDoModulo.ValidarAsync(erros, novo.Arquivo!, novo.ArquivosRecebidos, ct);
        }

        return erros.TemErros ? (null, null) : (campos, arquivo);
    }
}

/// <summary>
/// <c>POST /api/rh/treinamentos/{treinamentoId}/modulos</c>: acrescenta um módulo no fim (só em RASCUNHO; publicado ou encerrado = 422 <c>TREINAMENTO_MODULOS_TRAVADOS</c>, que vale mais que os erros do corpo). Ordem: 404, 422, 400 (todos os erros juntos). O PDF (até 10 MiB, tipo pelo CONTEÚDO) vai para o armazenamento FORA de transação e a transação curta trava o
/// treinamento, confere de novo que ainda é rascunho e grava a linha (o <c>CHECK</c> do banco exige o caminho junto com o nome); qualquer falha apaga o blob. Evento <c>ALTEROU</c>.
/// </summary>
public sealed class AdicionarModuloDeTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, EnvioDeArquivoDeDocumento envio, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<TreinamentoModuloRh> ExecutarAsync(int treinamentoId, NovoModulo novo, CancellationToken ct)
    {
        var treinamento = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        if (treinamento.Publicado)
        {
            throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
        }

        var erros = new ColetorDeErros();
        var (campos, arquivo) = await ConferenciaDoModulo.ConferirAsync(erros, novo, tipoAtual: null, atualTemArquivo: false, ct);
        erros.LancarSeHouver();

        async Task<TreinamentoModuloRh> EfetivarAsync(string? caminho)
        {
            var nome = caminho is null ? null : NomeParaDownload.Limpar(novo.Arquivo!.NomeOriginal, arquivo!.Extensao);
            return await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var estado = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                    if (estado.Publicado)
                    {
                        throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
                    }

                    var criado = await modulos.AdicionarAsync(treinamentoId, campos!, caminho, nome, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Acrescentou o módulo \"{criado.Titulo}\" ao treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamentoId));
                    await unidade.SalvarAsync(ct);
                    return criado.ParaOContrato;
                },
                ct);
        }

        return arquivo is null
            ? await EfetivarAsync(null)
            : await envio.GravarEEfetivarAsync(novo.Arquivo!, LimitesDoModulo.Pasta, arquivo, EfetivarAsync, ct);
    }
}

/// <summary>
/// <c>PUT /api/rh/treinamentos/{treinamentoId}/modulos/ordem</c>: reordena (só em RASCUNHO, senão 422 <c>TREINAMENTO_MODULOS_TRAVADOS</c>). O corpo traz TODOS os ids do treinamento, cada um uma vez, na nova ordem (faltando, repetido ou de outro treinamento = 400 em <c>moduloIds</c>). A troca é em duas fases numa transação, com o
/// treinamento travado. Evento <c>ALTEROU</c>.
/// </summary>
public sealed class ReordenarModulosDeTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<TreinamentoModulosRh> ExecutarAsync(int treinamentoId, TreinamentoModulosOrdemComando corpo, CancellationToken ct)
    {
        var treinamento = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        if (treinamento.Publicado)
        {
            throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
        }

        var ids = corpo.ModuloIds ?? [];
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var estado = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                if (estado.Publicado)
                {
                    throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
                }

                var atuais = await modulos.ListarAsync(treinamentoId, ct);
                if (ids.Count == 0 || ids.Distinct().Count() != ids.Count || !atuais.Select(m => m.Id).ToHashSet().SetEquals(ids))
                {
                    throw new ErroDeValidacaoException("moduloIds", "Envie todos os módulos do treinamento, cada um uma vez, na nova ordem.");
                }

                await modulos.ReordenarAsync(treinamentoId, ids, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Reordenou os módulos do treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);
        return new TreinamentoModulosRh([.. (await modulos.ListarAsync(treinamentoId, ct)).Select(m => m.ParaOContrato)]);
    }
}

/// <summary>
/// <c>PUT /api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}</c>: substitui o módulo sem mudar a ordem. 404 (treinamento, ou módulo que não é dele), 400 (todos os erros juntos), 422 <c>TREINAMENTO_CAMPO_TRAVADO</c> (publicado ou encerrado: o tipo não muda; corrigir título, duração e conteúdo vale também no encerrado). No rascunho o tipo muda e o arquivo que deixa
/// de valer é apagado DEPOIS do commit; num <c>PDF</c> sem arquivo novo o atual fica (trocar PARA pdf exige o arquivo). Evento <c>ALTEROU</c>.
/// </summary>
public sealed class AtualizarModuloDeTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, EnvioDeArquivoDeDocumento envio, IArmazenamentoDeArquivos armazenamento, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<TreinamentoModuloRh> ExecutarAsync(int treinamentoId, int moduloId, NovoModulo novo, CancellationToken ct)
    {
        var treinamento = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        var atual = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();

        var erros = new ColetorDeErros();
        var (campos, arquivo) = await ConferenciaDoModulo.ConferirAsync(erros, novo, atual.Tipo, atual.CaminhoArquivo is not null, ct);
        erros.LancarSeHouver();
        if (treinamento.Publicado && campos!.Tipo != atual.Tipo)
        {
            throw MensagensDosTreinamentos.CampoTravadoDoModulo.ComoProblema();
        }

        async Task<(TreinamentoModuloRh Modulo, string? Antigo)> EfetivarAsync(string? caminhoNovo)
        {
            return await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var estado = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                    var dentro = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();
                    if (estado.Publicado && campos!.Tipo != dentro.Tipo)
                    {
                        throw MensagensDosTreinamentos.CampoTravadoDoModulo.ComoProblema();
                    }

                    string? caminho;
                    string? nome;
                    if (campos!.Tipo is not (TipoDeModulo.Pdf or TipoDeModulo.Video))
                    {
                        caminho = null;
                        nome = null;
                    }
                    else if (caminhoNovo is not null)
                    {
                        caminho = caminhoNovo;
                        nome = NomeParaDownload.Limpar(novo.Arquivo!.NomeOriginal, arquivo!.Extensao);
                    }
                    else if (campos.Tipo == TipoDeModulo.Video && campos.UrlExterna is not null)
                    {
                        caminho = null; // o vídeo de arquivo virou link: o arquivo velho sai depois do commit
                        nome = null;
                    }
                    else if (dentro.Tipo == campos.Tipo && dentro.CaminhoArquivo is not null)
                    {
                        caminho = dentro.CaminhoArquivo; // sem arquivo novo, do mesmo tipo: o atual fica (o caso de uso já exigiu que exista)
                        nome = dentro.NomeArquivoOriginal;
                    }
                    else
                    {
                        throw campos.Tipo == TipoDeModulo.Pdf
                            ? new ErroDeValidacaoException("arquivo", "Envie o arquivo PDF do módulo.")
                            : new ErroDeValidacaoException("urlExterna", "Informe o endereço do vídeo ou envie o arquivo.");
                    }

                    await modulos.SubstituirAsync(moduloId, campos, caminho, nome, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Alterou o módulo \"{campos.Titulo}\" do treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamentoId));
                    await unidade.SalvarAsync(ct);
                    var depois = dentro with { Titulo = campos.Titulo, Tipo = campos.Tipo, DuracaoMinutos = campos.DuracaoMinutos, UrlExterna = campos.UrlExterna, ConteudoTexto = campos.ConteudoTexto, CaminhoArquivo = caminho, NomeArquivoOriginal = nome };
                    return (depois.ParaOContrato, dentro.CaminhoArquivo is { } velho && velho != caminho ? velho : null);
                },
                ct);
        }

        (TreinamentoModuloRh Modulo, string? Antigo) resultado = arquivo is null
            ? await EfetivarAsync(null)
            : await envio.GravarEEfetivarAsync(novo.Arquivo!, LimitesDoModulo.Pasta, arquivo, EfetivarAsync, ct);
        await ArquivosDoTreinamento.ApagarDepoisDoCommitAsync(armazenamento, resultado.Antigo);
        return resultado.Modulo;
    }
}

/// <summary>
/// <c>DELETE /api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}</c>: só em RASCUNHO (senão 422 <c>TREINAMENTO_MODULOS_TRAVADOS</c>, e no publicado o módulo pode ter progresso de gente). Apaga o módulo e renumera os que sobram; o arquivo sai do armazenamento DEPOIS do commit (o que falhar fica para a limpeza de sobras). 404 (treinamento, ou módulo
/// que não é dele). Evento <c>ALTEROU</c>.
/// </summary>
public sealed class ExcluirModuloDeTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, IArmazenamentoDeArquivos armazenamento, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task ExecutarAsync(int treinamentoId, int moduloId, CancellationToken ct)
    {
        var treinamento = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        var modulo = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();
        if (treinamento.Publicado)
        {
            throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
        }

        var arquivo = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var estado = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                if (estado.Publicado)
                {
                    throw MensagensDosTreinamentos.ModulosTravados.ComoProblema();
                }

                var dentro = (await modulos.ObterAsync(treinamentoId, moduloId, ct)).OuNaoEncontrado();
                await modulos.ExcluirAsync(treinamentoId, moduloId, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Excluiu o módulo \"{modulo.Titulo}\" do treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return dentro.CaminhoArquivo;
            },
            ct);
        await ArquivosDoTreinamento.ApagarDepoisDoCommitAsync(armazenamento, arquivo);
    }
}

/// <summary>Apagar o arquivo que deixou de valer DEPOIS do commit: se o armazenamento falhar a linha já mudou e a sobra fica para a limpeza de sobras (nunca derruba a operação).</summary>
internal static class ArquivosDoTreinamento
{
    public static async Task ApagarDepoisDoCommitAsync(IArmazenamentoDeArquivos armazenamento, string? caminho)
    {
        if (caminho is null)
        {
            return;
        }

        try
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
        }
        catch (Exception)
        {
            // a linha já mudou: o arquivo que não saiu agora sai na limpeza de sobras
        }
    }
}
