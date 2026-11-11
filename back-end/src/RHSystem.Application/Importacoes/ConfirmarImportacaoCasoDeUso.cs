using Microsoft.Extensions.Logging;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Importacoes;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// <c>confirmarImportacao</c>: cadastra TODAS as pessoas da prévia guardada, ou ninguém (tudo ou nada). Ordem das conferências (decisão 12): 404, 409 (já confirmada), 422 <c>IMPORTACAO_EXPIRADA</c>, 422
/// <c>IMPORTACAO_COM_ERROS</c>, e só então a conversão de novo das linhas e a transação. A transação REIVINDICA a importação primeiro (uma instrução que só muda a linha se ela ainda está <c>PRONTA</c> e dentro da validade: duas
/// confirmações juntas dão um 200 e um 409, sem a segunda chegar a inserir gente) e DEPOIS cadastra, pelo mesmo <see cref="ICadastroDeFuncionarios"/> do cadastro da tela 02, com o registro automático da auditoria suspenso: o
/// resultado é UM evento <c>Importou</c>, com os totais. Qualquer recusa desfaz a transação inteira, inclusive a reivindicação. O arquivo é apagado DEPOIS do commit.
/// </summary>
public sealed class ConfirmarImportacaoCasoDeUso(
    IImportacoesRepositorio importacoes,
    ICadastroDeFuncionarios cadastro,
    IConsultaDoCatalogoDaImportacao catalogo,
    IArmazenamentoDeArquivos armazenamento,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IRelogio relogio,
    ILogger<ConfirmarImportacaoCasoDeUso> log)
{
    private sealed record Confirmacao(ResultadoDoCadastro Resultado, IReadOnlyList<FuncionarioResumo> Funcionarios);

    public async Task<ImportacaoResultado> ExecutarAsync(int importacaoId, ImportacaoConfirmarComando comando, CancellationToken ct)
    {
        var importacao = await importacoes.ObterAsync(importacaoId, ct) ?? throw new NaoEncontradoException();
        var agora = relogio.AgoraUtc;

        if (importacao.Situacao == SituacaoDaImportacao.Confirmada)
        {
            throw JaConfirmada();
        }

        // Sem prévia numa importação ainda no prazo: a limpeza já passou por ela. Para o RH é o mesmo: venceu.
        if (importacao.Vencida(agora) || importacao.Previa is null)
        {
            throw new ErroDeNegocioException("IMPORTACAO_EXPIRADA", "A prévia venceu. Envie a planilha de novo.");
        }

        if (importacao.Situacao == SituacaoDaImportacao.ComErros)
        {
            throw new ErroDeNegocioException("IMPORTACAO_COM_ERROS", "A planilha ainda tem linhas com erro. Corrija e envie de novo.");
        }

        // Converte de novo com o catálogo de AGORA: um cargo apagado depois da prévia vira recusa da linha dele.
        var linhas = SerializadorDaPrevia.Ler(importacao.Previa);
        var numeros = linhas.Select(l => l.NumeroLinha).ToList();
        var catalogoAtual = await catalogo.ObterAsync(ct);
        var conversoes = linhas.Select(l => ConversorDaPlanilha.Converter(l.Valores, catalogoAtual)).ToList();
        for (var i = 0; i < conversoes.Count; i++)
        {
            if (conversoes[i].Erros.Count > 0)
            {
                throw LinhaRecusada(numeros[i], Motivo(conversoes[i].Erros[0].Mensagem), regraDoBanco: null);
            }
        }

        var novos = conversoes.Select(c => c.Novo).ToList();
        var opcoes = new OpcoesDoCadastro(comando.EnviarConvite, comando.IncluirNoOnboarding, comando.FormularioAdmissaoId);

        Confirmacao feita;
        try
        {
            feita = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    // O banco exige os quatro números do resultado junto com CONFIRMADA: a reivindicação grava zeros e os reais entram logo depois do cadastro.
                    if (!await importacoes.ReivindicarAsync(importacaoId, agora, ct))
                    {
                        throw JaConfirmada(); // outra confirmação chegou antes
                    }

                    ResultadoDoCadastro resultado;
                    using (auditor.SuspenderRegistroAutomatico())
                    {
                        resultado = await cadastro.CadastrarAsync(novos, opcoes, ct);
                    }

                    await importacoes.RegistrarResultadoAsync(importacaoId, resultado.ConvitesEnviados, resultado.IncluidosNoOnboarding, resultado.IncluidosNoFormularioDeAdmissao, ct);
                    auditor.Registrar(
                        new EventoDeAuditoriaNovo(
                            AuditoriaArea.Importacao,
                            AuditoriaAcao.Importou,
                            $"Confirmou a importação ({resultado.Cadastrados.Count} colaboradores cadastrados, {resultado.ConvitesEnviados} convites)",
                            $"Importação {importacaoId}",
                            "IMPORTACOES",
                            importacaoId));
                    await unidade.SalvarAsync(ct);

                    return new Confirmacao(resultado, await importacoes.ResumirAsync([.. resultado.Cadastrados.Select(c => c.Id)], ct));
                },
                ct);
        }
        catch (CadastroInvalidoException invalido)
        {
            var primeiro = invalido.Problemas[0]; // ValidarAsync devolve os problemas por linha: o primeiro é o da primeira linha com problema
            throw LinhaRecusada(numeros[primeiro.Indice], Motivo(MontadorDaPrevia.Mensagem(primeiro, numeros)), regraDoBanco: null);
        }
        catch (LinhaRecusadaException recusada)
        {
            throw await DaRecusaDoBancoAsync(recusada, novos, numeros, ct);
        }

        await TentarApagarAsync(importacao.CaminhoArquivo);
        var cadastrados = feita.Resultado;
        return new ImportacaoResultado(
            importacaoId,
            SituacaoDaImportacao.Confirmada,
            importacao.NomeArquivo,
            cadastrados.Cadastrados.Count,
            cadastrados.ConvitesEnviados,
            cadastrados.IncluidosNoOnboarding,
            cadastrados.IncluidosNoFormularioDeAdmissao,
            agora,
            feita.Funcionarios);
    }

    private static ConflitoException JaConfirmada() => new("Esta importação já foi confirmada.", "IMPORTACAO_JA_CONFIRMADA");

    /// <summary>O banco recusou uma linha DURANTE a gravação (a transação já foi desfeita): diz qual e, se foi duplicidade, de qual dado (relendo as linhas com a mesma validação da prévia).</summary>
    private async Task<ProblemaDeApiException> DaRecusaDoBancoAsync(LinhaRecusadaException recusada, IReadOnlyList<NovoFuncionario> novos, IReadOnlyList<int> numeros, CancellationToken ct)
    {
        if (recusada.PorDuplicidade)
        {
            var problemas = await cadastro.ValidarAsync(novos, ct);
            var repetido = problemas.FirstOrDefault(p => p.Indice == recusada.Indice && p.Tipo == TipoDeProblemaDeCadastro.Duplicado);
            return LinhaRecusada(numeros[recusada.Indice], repetido is null ? "já existe um colaborador com estes dados" : Motivo(MontadorDaPrevia.Mensagem(repetido, numeros)), regraDoBanco: null);
        }

        var motivo = recusada.RegraDoBanco == ProblemasDoCadastro.TextoDaRegraDoGestorInativo ? "o gestor do setor escolhido está inativo" : "uma regra do banco de dados recusou a linha";
        return LinhaRecusada(numeros[recusada.Indice], motivo, recusada.RegraDoBanco);
    }

    private static ProblemaDeApiException LinhaRecusada(int linha, string motivo, string? regraDoBanco)
    {
        var extensoes = new Dictionary<string, object?> { ["linha"] = linha };
        if (regraDoBanco is not null)
        {
            extensoes["regraDoBanco"] = regraDoBanco;
        }

        return new ProblemaDeApiException(
            422,
            "Operação recusada.",
            $"A linha {linha} foi recusada: {motivo}. Nenhum colaborador foi cadastrado. Envie a planilha de novo.",
            "IMPORTACAO_LINHA_RECUSADA",
            extensoes);
    }

    /// <summary>
    /// A primeira frase da mensagem, sem o ponto final e com a inicial em minúscula (menos em sigla: <c>CPF inválido</c> continua assim). O ponto dentro de aspas (um cargo chamado <c>Sr. Analista</c>) não termina a frase.
    /// </summary>
    public static string Motivo(string mensagem)
    {
        var dentroDeAspas = false;
        var fim = mensagem.Length;
        for (var i = 0; i < mensagem.Length - 1; i++)
        {
            if (mensagem[i] == '"')
            {
                dentroDeAspas = !dentroDeAspas;
            }
            else if (!dentroDeAspas && mensagem[i] == '.' && mensagem[i + 1] == ' ')
            {
                fim = i;
                break;
            }
        }

        var frase = mensagem[..fim].TrimEnd('.');
        return frase.Length > 1 && char.IsUpper(frase[0]) && !char.IsUpper(frase[1]) ? char.ToLowerInvariant(frase[0]) + frase[1..] : frase;
    }

    /// <summary>Depois do commit: se o arquivo não for apagado a importação continua confirmada, e a <c>LIMPAR_ARQUIVOS_ORFAOS</c> o apaga (nenhuma linha o referencia mais). Só o tipo da exceção vai para o log.</summary>
    private async Task TentarApagarAsync(string? caminho)
    {
        if (caminho is null)
        {
            return;
        }

        try
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
        }
        catch (Exception erro)
        {
            log.LogWarning("Não foi possível apagar o arquivo de uma importação confirmada ({Tipo}); a limpeza de sobras o apaga depois.", erro.GetType().Name);
        }
    }
}
