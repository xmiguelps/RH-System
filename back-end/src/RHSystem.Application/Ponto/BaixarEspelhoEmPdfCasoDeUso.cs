using System.Globalization;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Empresa;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Ponto;

/// <summary>O que o PDF precisa: o espelho do mês, a empresa (<c>null</c> se ainda não foi configurada) e o instante de geração (no rodapé, em -03:00).</summary>
public sealed record EspelhoParaPdf(EspelhoDoMes Espelho, ConfiguracaoDaEmpresaLida? Empresa, DateTime GeradoEmUtc);

/// <summary>Desenha o espelho do mês em PDF, com fonte que acentua (a do sistema nunca é usada).</summary>
public interface IGeradorDePdfDoEspelho
{
    byte[] Gerar(EspelhoParaPdf espelho);
}

/// <summary>
/// <c>GET /api/rh/funcionarios/{id}/ponto/espelho/download</c> (qualquer pessoa, só o RH) e <c>GET /api/gestor/equipe/{id}/ponto/espelho/download</c> (só quem é da equipe do gestor; outro setor, o próprio gestor, quem não existe e gestor sem setor dão o mesmo 404): o
/// PDF do espelho do mês (<c>ano</c> e <c>mes</c>, padrão o mês atual; fora da faixa é 400, antes do 404). A API de negócio nunca devolve o arquivo: grava o PDF em <c>espelhos/</c> (nome com GUID, sem transação aberta), registra o evento <c>PONTO</c>/<c>BAIXOU</c> (com
/// o nome e o mês) ANTES de existir o link, e devolve um link temporário que baixa como anexo. Se o registro falha, não há link. O PDF é de uso único: nada o referencia e a limpeza de sobras o apaga depois das horas mínimas.
/// </summary>
public sealed class BaixarEspelhoEmPdfCasoDeUso(
    IEscopoDoUsuario escopo,
    IConsultaDoPonto consulta,
    MontadorDoEspelho montador,
    IConfiguracaoDaEmpresa empresa,
    IGeradorDePdfDoEspelho gerador,
    IArmazenamentoDeArquivos armazenamento,
    IAuditor auditor,
    IRelogio relogio)
{
    public const string PastaNoArmazenamento = "espelhos";

    public async Task<LinkTemporario> ParaRhAsync(int funcionarioId, int? ano, int? mes, CancellationToken ct)
    {
        MontadorDoEspelho.Periodo(ano, mes, DateOnly.MinValue);
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        return await GerarAsync(pessoa, ano, mes, ct);
    }

    public async Task<LinkTemporario> DaEquipeAsync(int funcionarioId, int? ano, int? mes, CancellationToken ct)
    {
        var equipe = await escopo.ObterEquipeAsync(ct);
        MontadorDoEspelho.Periodo(ano, mes, DateOnly.MinValue);
        var pessoa = equipe is null ? null : await consulta.PessoaDaEquipeAsync(equipe, funcionarioId, ct);
        return await GerarAsync(pessoa ?? throw new NaoEncontradoException(), ano, mes, ct);
    }

    private async Task<LinkTemporario> GerarAsync(PessoaDoEspelho pessoa, int? ano, int? mes, CancellationToken ct)
    {
        var espelho = await montador.MontarAsync(pessoa, ano, mes, ct);
        var pdf = gerador.Gerar(new EspelhoParaPdf(espelho, await empresa.ObterAsync(ct), relogio.AgoraUtc));

        var caminho = CaminhosDeArquivo.Novo(PastaNoArmazenamento, "pdf");
        using (var conteudo = new MemoryStream(pdf, writable: false))
        {
            await armazenamento.GravarAsync(caminho, conteudo, "application/pdf", ct);
        }

        var competencia = string.Create(CultureInfo.InvariantCulture, $"{espelho.Mes:00}/{espelho.Ano:0000}");
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(
                AuditoriaArea.Ponto,
                AuditoriaAcao.Baixou,
                $"Baixou o espelho de ponto de {pessoa.Nome} ({competencia}) em PDF.",
                $"Espelho de ponto de {pessoa.Nome} ({competencia})",
                "FUNCIONARIOS",
                pessoa.Id),
            ct);
        return await armazenamento.CriarLinkTemporarioAsync(caminho, string.Create(CultureInfo.InvariantCulture, $"espelho-de-ponto-{pessoa.Nome}-{espelho.Ano:0000}-{espelho.Mes:00}.pdf"), ct);
    }
}
