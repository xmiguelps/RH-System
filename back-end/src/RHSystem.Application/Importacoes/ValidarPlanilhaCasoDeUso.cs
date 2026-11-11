using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Documentos;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Importacoes;

namespace RHSystem.Application.Importacoes;

/// <summary>
/// <c>validarPlanilhaDeImportacao</c>: lê a planilha, valida TODAS as linhas sem cadastrar ninguém e guarda a prévia por 1 hora. Ordem: o corpo (400 em <c>arquivo</c>), o tamanho (422 <c>ARQUIVO_MUITO_GRANDE</c>) e o leitor
/// (422 do arquivo todo) vêm ANTES de qualquer escrita; só então o blob vai para o armazenamento, FORA de qualquer transação, e a linha e o evento vão numa transação curta (receita de <see cref="EnvioDeArquivoDeDocumento"/>:
/// se algo falhar, o blob é apagado). A prévia tem dado pessoal: o evento de auditoria leva só os totais e nada disso vai para log.
/// </summary>
public sealed class ValidarPlanilhaCasoDeUso(
    ILeitorDePlanilha leitor,
    IConsultaDoCatalogoDaImportacao catalogo,
    MontadorDaPrevia montador,
    EnvioDeArquivoDeDocumento envio,
    IImportacoesRepositorio importacoes,
    IUnidadeDeTrabalho unidade,
    IAuditor auditor,
    IEscopoDoUsuario escopo,
    IRelogio relogio)
{
    private static readonly TipoDeArquivo Xlsx = new("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", ".xlsx");

    /// <param name="arquivosRecebidos">Conta TODAS as partes de arquivo do formulário, de qualquer nome de campo.</param>
    public async Task<ImportacaoPrevia> ExecutarAsync(ArquivoRecebido? arquivo, int arquivosRecebidos, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        if (arquivosRecebidos > 1)
        {
            erros.Adicionar("arquivo", "Envie um arquivo só.");
        }
        else if (arquivo is null)
        {
            erros.Adicionar("arquivo", "O arquivo é obrigatório.");
        }
        else if (arquivo.Tamanho <= 0)
        {
            erros.Adicionar("arquivo", "O arquivo está vazio.");
        }

        erros.LancarSeHouver();

        if (arquivo!.Tamanho > LimitesDeImportacao.TamanhoMaximoBytes)
        {
            throw ErrosDePlanilha.MuitoGrande();
        }

        // O arquivo cabe em memória (até 5 MiB): o leitor não escreve nada, então um arquivo ruim não deixa rastro.
        byte[] bytes;
        await using (var conteudo = arquivo.Abrir())
        using (var memoria = new MemoryStream())
        {
            await conteudo.CopyToAsync(memoria, ct);
            bytes = memoria.ToArray();
        }

        var planilha = leitor.Ler(new MemoryStream(bytes));
        var linhas = await montador.MontarAsync(
            [.. planilha.Linhas.Select(l => (l.NumeroLinha, ImportacaoLinhaValores.De(l)))],
            await catalogo.ObterAsync(ct),
            ct);

        var comErro = linhas.Count(l => l.Situacao == SituacaoDaLinha.ComErro);
        var nomeGuardado = NomeParaDownload.Limpar(arquivo.NomeOriginal, Xlsx.Extensao);
        var previa = SerializadorDaPrevia.Serializar(linhas);
        var agora = relogio.AgoraUtc;
        var guardado = new ArquivoRecebido(nomeGuardado, bytes.Length, () => new MemoryStream(bytes));

        return await envio.GravarEEfetivarAsync(
            guardado,
            LimitesDeImportacao.PastaNoArmazenamento,
            Xlsx,
            caminho => unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var importacao = Importacao.Criar(escopo.FuncionarioId, nomeGuardado, linhas.Count, linhas.Count - comErro, comErro, agora, LimitesDeImportacao.Validade, caminho, previa);
                    await importacoes.AdicionarAsync(importacao, ct);

                    // O evento só leva os totais, nunca o nome do arquivo nem as linhas. Precisa do Id: sai no SalvarAsync seguinte, na mesma transação.
                    auditor.Registrar(
                        new EventoDeAuditoriaNovo(
                            AuditoriaArea.Importacao,
                            AuditoriaAcao.Importou,
                            $"Enviou uma planilha de importação ({linhas.Count} linhas, {comErro} com erro)",
                            $"Importação {importacao.Id}",
                            "IMPORTACOES",
                            importacao.Id));
                    await unidade.SalvarAsync(ct);

                    return new ImportacaoPrevia(
                        importacao.Id,
                        importacao.NomeArquivo,
                        importacao.Situacao,
                        importacao.TotalLinhas,
                        importacao.LinhasValidas,
                        importacao.LinhasComErro,
                        comErro == 0,
                        importacao.ExpiraEm,
                        linhas);
                },
                ct),
            ct);
    }
}
