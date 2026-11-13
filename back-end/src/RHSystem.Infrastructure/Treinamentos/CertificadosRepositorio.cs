using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Emails;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// A leitura do certificado e a gravação do caminho do PDF. A linha do certificado NASCE por trigger (a aplicação nunca a insere); aqui só se lê e se atualiza o caminho. O certificado "existe" para a API só quando o treinamento EMITE certificado e a pessoa o concluiu (a linha que o trigger cria em treinamento que não emite não conta).
/// </summary>
public sealed class CertificadosRepositorio(AppDbContext db) : ICertificadosRepositorio
{
    public async Task<CertificadoParaEmitir?> LerAsync(int treinamentoId, int funcionarioId, CancellationToken ct)
    {
        var tid = treinamentoId;
        var fid = funcionarioId;
        var certificado = await db.TreinamentosCertificados.AsNoTracking()
            .Where(c => c.TreinamentoId == tid && c.FuncionarioId == fid
                && db.Treinamentos.Any(t => t.Id == tid && t.EmiteCertificado)
                && db.TreinamentosFuncionarios.Any(l => l.TreinamentoId == tid && l.FuncionarioId == fid && l.Situacao == SituacaoNoTreinamento.Concluido))
            .Select(c => new { c.Id, c.EmitidoEm, c.CodigoValidacao, c.CaminhoArquivo })
            .SingleOrDefaultAsync(ct);
        if (certificado is null)
        {
            return null;
        }

        var nome = await db.Funcionarios.AsNoTracking().Where(f => f.Id == fid).Select(f => f.Nome).SingleAsync(ct);
        var titulo = await db.Treinamentos.AsNoTracking().Where(t => t.Id == tid).Select(t => t.Titulo).SingleAsync(ct);
        var duracoes = await db.TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == tid && m.DuracaoMinutos != null).Select(m => m.DuracaoMinutos!.Value).ToListAsync(ct);
        int? carga = duracoes.Count == 0 ? null : duracoes.Sum(d => (int)d);
        return new CertificadoParaEmitir(certificado.Id, tid, fid, nome, titulo, carga, certificado.EmitidoEm, certificado.CodigoValidacao, certificado.CaminhoArquivo);
    }

    public async Task<CertificadoConferido?> ConferirPorCodigoAsync(string codigo, CancellationToken ct)
    {
        var certificado = await db.TreinamentosCertificados.AsNoTracking()
            .Where(c => c.CodigoValidacao == codigo && c.EmitidoEm != null
                && db.Treinamentos.Any(t => t.Id == c.TreinamentoId && t.EmiteCertificado)
                && db.TreinamentosFuncionarios.Any(l => l.TreinamentoId == c.TreinamentoId && l.FuncionarioId == c.FuncionarioId && l.Situacao == SituacaoNoTreinamento.Concluido))
            .Select(c => new
            {
                c.CodigoValidacao,
                c.EmitidoEm,
                Nome = db.Funcionarios.Where(f => f.Id == c.FuncionarioId).Select(f => f.Nome).First(),
                Titulo = db.Treinamentos.Where(t => t.Id == c.TreinamentoId).Select(t => t.Titulo).First(),
            })
            .SingleOrDefaultAsync(ct);
        return certificado is null ? null : new CertificadoConferido(certificado.CodigoValidacao!, certificado.Nome, certificado.Titulo, certificado.EmitidoEm!.Value);
    }

    public async Task<bool> GravarCaminhoAsync(int treinamentoId, int funcionarioId, string caminho, CancellationToken ct)
    {
        var tid = treinamentoId;
        var fid = funcionarioId;

        // AUDITORIA-OK: guardar o caminho do PDF não é um fato de negócio auditável (o download do RH tem o próprio evento); o UPDATE só vale enquanto a linha ainda está sem arquivo (dois downloads ao mesmo tempo: só um grava e o outro apaga o próprio arquivo).
        var linhas = await db.TreinamentosCertificados.Where(c => c.TreinamentoId == tid && c.FuncionarioId == fid && c.CaminhoArquivo == null).ExecuteUpdateAsync(s => s.SetProperty(c => c.CaminhoArquivo, caminho), ct);
        return linhas == 1;
    }
}

/// <summary>
/// As sobras de <c>certificados/</c> na limpeza de órfãos: o arquivo que uma linha de certificado referencia NUNCA é apagado (apagar o PDF de um certificado emitido seria perder um documento válido); o que ficou sem linha (o gerador gravou e a linha já tinha outro arquivo, ou a gravação do caminho falhou) sai depois do mínimo de horas.
/// </summary>
public sealed class FonteDeReferenciasDeCertificados(AppDbContext db) : IFonteDeReferenciasDeArquivo
{
    public string Prefixo => LimitesDoCertificado.Pasta + "/";

    public async Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct)
    {
        var lista = caminhos.ToList();
        var dosCertificados = await db.TreinamentosCertificados.AsNoTracking().Where(c => c.CaminhoArquivo != null && lista.Contains(c.CaminhoArquivo)).Select(c => c.CaminhoArquivo!).ToListAsync(ct);
        return dosCertificados.ToHashSet(StringComparer.Ordinal);
    }
}

/// <summary>O endereço da página de conferência: a base do front (<c>App:UrlDoFront</c>) quando é uma URL http ou https absoluta; sem ela o PDF não mostra onde conferir.</summary>
public sealed class EnderecoDaConferencia(OpcoesDoApp app) : IEnderecoDaConferencia
{
    public string? Endereco => Uri.TryCreate(app.UrlDoFront, UriKind.Absolute, out var url) && (url.Scheme == Uri.UriSchemeHttp || url.Scheme == Uri.UriSchemeHttps) ? app.UrlDoFront.TrimEnd('/') : null;
}

/// <summary>
/// A emissão do PDF DEPOIS do commit da conclusão: gera, guarda e grava o caminho. NUNCA lança: se o armazenamento ou o gerador falhar, a conclusão continua valendo, o erro vira um aviso no log (sem código nem nome) e o primeiro download gera o arquivo na hora.
/// </summary>
public sealed class EmissaoDoCertificado(GarantidorDoPdfDoCertificado garantidor, ILogger<EmissaoDoCertificado> log) : IEmissaoDoCertificado
{
    public async Task EmitirDepoisDoCommitAsync(int funcionarioId, int treinamentoId, CancellationToken ct)
    {
        try
        {
            _ = await garantidor.GarantirAsync(treinamentoId, funcionarioId, ct);
        }
        catch (Exception erro) when (erro is not OperationCanceledException)
        {
            log.LogWarning(erro, "O PDF do certificado do treinamento {TreinamentoId} não foi gerado agora; o primeiro download o gera.", treinamentoId);
        }
    }
}
