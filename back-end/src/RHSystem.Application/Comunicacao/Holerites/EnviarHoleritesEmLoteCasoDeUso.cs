using System.Text.Json;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Comunicacao.Holerites;

/// <summary>
/// <c>POST /api/rh/holerites/lote</c> (multipart): vários PDFs de uma vez, um por pessoa, ligados pela lista <c>itens</c> (<c>funcionarioId</c> e <c>nomeDoArquivo</c>). <b>Cada item é independente</b> (sem tudo ou nada): a resposta é sempre 200 com o resultado de
/// cada um, na ordem dos itens (publicado, agendado, ou o <c>codigoDoErro</c> da operação individual: <c>HOLERITE_JA_EXISTE</c> (o lote não cria versão), as três competências, <c>ARQUIVO_INVALIDO</c> e <c>FUNCIONARIO_NAO_ENCONTRADO</c>). O que vale para o lote
/// todo recusa o envio INTEIRO antes de qualquer efeito: 400 (campos, <c>itens</c> que não é uma lista de 1 a 200 linhas, nome repetido, arquivo sem item e item sem arquivo, mais de 200 PDFs, mais de 100 MiB no total, PDF fora do campo <c>arquivos</c>) e 422
/// <c>EMPRESA_NAO_CONFIGURADA</c> (no dia do pagamento sem dia configurado). Cada item usa a receita da T9 (arquivo fora de transação, transação curta, arquivo apagado se falhar) e grava o seu evento; o lote ganha um evento de resumo. Um PDF acima de 5 MiB é erro do
/// item (<c>ARQUIVO_INVALIDO</c>), não do lote.
/// </summary>
public sealed class EnviarHoleritesEmLoteCasoDeUso(IConsultaDeHolerites consulta, PublicadorDeHolerite publicador, IAuditor auditor, IRelogio relogio)
{
    public async Task<HoleriteLoteResultado> ExecutarAsync(NovoLoteDeHoleritesBruto lote, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);

        var erros = new ColetorDeErros();
        var competencia = LeitorDosCamposDoHolerite.LerCompetencia(erros, lote.Mes, lote.Ano, lote.Tipo);
        var quando = LeitorDosCamposDoHolerite.LerQuandoPublicar(erros, lote.PublicarEm, lote.NoDiaDoPagamento, hoje);
        var itens = LerItens(erros, lote.Itens);
        ConferirOsArquivos(erros, lote, itens);
        erros.LancarSeHouver();

        var publicacao = await publicador.InstanteDaPublicacaoAsync(quando!, hoje, agora, ct);
        var porNome = lote.Arquivos.ToDictionary(a => a.NomeOriginal, StringComparer.Ordinal);

        var resultados = new List<HoleriteLoteItemResultado>(itens!.Count);
        foreach (var item in itens)
        {
            resultados.Add(await ProcessarAsync(item, porNome[item.NomeDoArquivo], competencia!, publicacao, hoje, agora, ct));
        }

        var enviados = resultados.Count(r => r.Sucesso);
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(
                AuditoriaArea.Holerites,
                AuditoriaAcao.Criou,
                $"Enviou um lote de {resultados.Count} holerites {MensagensDaComunicacao.TextoDaCompetencia(competencia!.Tipo, competencia.Mes, competencia.Ano)}: {enviados} enviados e {resultados.Count - enviados} com erro.",
                "Lote de holerites"),
            ct);
        return new HoleriteLoteResultado(resultados.Count, enviados, resultados.Count - enviados, resultados);
    }

    private async Task<HoleriteLoteItemResultado> ProcessarAsync(HoleriteLoteItemComando item, ArquivoRecebido arquivo, CompetenciaDoHolerite competencia, DateTime publicacao, DateOnly hoje, DateTime agora, CancellationToken ct)
    {
        var pessoa = await consulta.ObterPessoaAsync(item.FuncionarioId, ct);
        if (pessoa is null)
        {
            return Falha(item, MensagensDaComunicacao.CodigoDaPessoaNaoEncontradaNoLote, MensagensDaComunicacao.TextoDaPessoaNaoEncontradaNoLote);
        }

        var errosDoArquivo = new ColetorDeErros();
        var tipoDoArquivo = await PublicadorDeHolerite.ValidarPdfAsync(errosDoArquivo, arquivo, 1, ct);
        if (errosDoArquivo.TemErros)
        {
            return Falha(item, MensagensDaComunicacao.CodigoDoArquivoInvalidoNoLote, PrimeiraMensagem(errosDoArquivo, "arquivo"));
        }

        if (RegraDaCompetenciaDoHolerite.Conferir(competencia, hoje, pessoa.DataAdmissao, pessoa.DataDesligamento) is { } recusa)
        {
            return Falha(item, recusa.Mensagem.Codigo, recusa.Mensagem.Detalhe);
        }

        var jaExiste = MensagensDaComunicacao.HoleriteJaExisteNoLote(competencia.Tipo, competencia.Mes, competencia.Ano);
        if (await consulta.MaiorVersaoAsync(pessoa.Id, competencia.Ano, competencia.Mes, competencia.Tipo, ct) is not null)
        {
            return Falha(item, "HOLERITE_JA_EXISTE", jaExiste);
        }

        try
        {
            var holerite = await publicador.GravarAsync(pessoa, competencia, 1, publicacao, agora, arquivo, tipoDoArquivo!, OperacaoDeHolerite.Envio, ct);
            return new HoleriteLoteItemResultado(item.FuncionarioId, item.NomeDoArquivo, true, holerite, null, null);
        }
        catch (ProblemaDeApiException problema)
        {
            // A corrida (outro envio da mesma competência entre a conferência e a gravação) e as regras do trigger chegam como o erro do contrato; o do lote não leva a sugestão de nova versão.
            return Falha(item, problema.Codigo ?? "OPERACAO_RECUSADA", problema.Codigo == "HOLERITE_JA_EXISTE" ? jaExiste : problema.Detalhe);
        }
    }

    private static HoleriteLoteItemResultado Falha(HoleriteLoteItemComando item, string codigo, string mensagem) => new(item.FuncionarioId, item.NomeDoArquivo, false, null, codigo, mensagem);

    private static string PrimeiraMensagem(ColetorDeErros erros, string campo)
    {
        try
        {
            erros.LancarSeHouver();
        }
        catch (ErroDeValidacaoException erro)
        {
            return erro.Erros[campo][0];
        }

        return string.Empty;
    }

    /// <summary>Lê <c>itens</c> (JSON): uma lista de 1 a 200 objetos com <c>funcionarioId</c> (inteiro) e <c>nomeDoArquivo</c> (texto não vazio), sem nome repetido. Erro de formato ou de contagem vai em <c>itens</c>; <c>null</c> se não deu para ler.</summary>
    private static List<HoleriteLoteItemComando>? LerItens(ColetorDeErros erros, string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            erros.Adicionar("itens", "Informe a lista de itens, um por arquivo.");
            return null;
        }

        JsonDocument documento;
        try
        {
            documento = JsonDocument.Parse(json);
        }
        catch (JsonException)
        {
            erros.Adicionar("itens", "Os itens precisam ser uma lista em JSON.");
            return null;
        }

        using (documento)
        {
            if (documento.RootElement.ValueKind != JsonValueKind.Array)
            {
                erros.Adicionar("itens", "Os itens precisam ser uma lista em JSON.");
                return null;
            }

            var itens = new List<HoleriteLoteItemComando>();
            foreach (var elemento in documento.RootElement.EnumerateArray())
            {
                if (elemento.ValueKind == JsonValueKind.Object
                    && elemento.TryGetProperty("funcionarioId", out var pessoa) && pessoa.ValueKind == JsonValueKind.Number && pessoa.TryGetInt32(out var funcionarioId)
                    && elemento.TryGetProperty("nomeDoArquivo", out var nome) && nome.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(nome.GetString()))
                {
                    itens.Add(new HoleriteLoteItemComando(funcionarioId, nome.GetString()!));
                }
                else
                {
                    erros.Adicionar("itens", "Cada item precisa de funcionarioId (inteiro) e nomeDoArquivo (texto).");
                    return null;
                }
            }

            if (itens.Count is < 1 or > LimitesDoLoteDeHolerites.MaximoDeItens)
            {
                erros.Adicionar("itens", $"Envie de 1 a {LimitesDoLoteDeHolerites.MaximoDeItens} itens.");
                return null;
            }

            if (itens.Select(i => i.NomeDoArquivo).Distinct(StringComparer.Ordinal).Count() != itens.Count)
            {
                erros.Adicionar("itens", "Há nomes de arquivo repetidos nos itens.");
                return null;
            }

            return itens;
        }
    }

    /// <summary>Os arquivos do lote: de 1 a 200, só no campo <c>arquivos</c>, sem nome repetido, até 100 MiB no total, e cada um com o seu item (e cada item com o seu arquivo).</summary>
    private static void ConferirOsArquivos(ColetorDeErros erros, NovoLoteDeHoleritesBruto lote, List<HoleriteLoteItemComando>? itens)
    {
        if (lote.ArquivosRecebidos != lote.Arquivos.Count)
        {
            erros.Adicionar("arquivos", "Envie os PDFs só no campo arquivos.");
            return;
        }

        if (lote.Arquivos.Count < 1)
        {
            erros.Adicionar("arquivos", "Envie pelo menos um PDF no campo arquivos.");
            return;
        }

        if (lote.Arquivos.Count > LimitesDoLoteDeHolerites.MaximoDeItens)
        {
            erros.Adicionar("arquivos", $"Envie até {LimitesDoLoteDeHolerites.MaximoDeItens} PDFs por vez.");
            return;
        }

        if (lote.Arquivos.Select(a => a.NomeOriginal).Distinct(StringComparer.Ordinal).Count() != lote.Arquivos.Count)
        {
            erros.Adicionar("arquivos", "Há arquivos com o mesmo nome.");
            return;
        }

        if (lote.Arquivos.Sum(a => a.Tamanho) > LimitesDoLoteDeHolerites.TamanhoTotalMaximoBytes)
        {
            erros.Adicionar("arquivos", "O total dos arquivos pode ter até 100 MB.");
            return;
        }

        if (itens is null)
        {
            return;
        }

        var nomesDosArquivos = lote.Arquivos.Select(a => a.NomeOriginal).ToHashSet(StringComparer.Ordinal);
        var nomesDosItens = itens.Select(i => i.NomeDoArquivo).ToHashSet(StringComparer.Ordinal);
        var semArquivo = nomesDosItens.Where(n => !nomesDosArquivos.Contains(n)).Take(5).ToList();
        var semItem = nomesDosArquivos.Where(n => !nomesDosItens.Contains(n)).Take(5).ToList();
        if (semArquivo.Count > 0)
        {
            erros.Adicionar("itens", $"Há itens sem arquivo: {string.Join(", ", semArquivo)}.");
        }

        if (semItem.Count > 0)
        {
            erros.Adicionar("arquivos", $"Há arquivos sem item: {string.Join(", ", semItem)}.");
        }
    }
}
