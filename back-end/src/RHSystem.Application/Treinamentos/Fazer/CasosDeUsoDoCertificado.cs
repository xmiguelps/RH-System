using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using RHSystem.Application.Escopo;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>
/// Garante que o PDF do certificado existe e devolve o caminho dele: se a linha já tem arquivo, devolve; senão gera, grava SEM transação aberta e guarda o caminho por <c>UPDATE</c> condicional a "ainda sem arquivo" (nunca cria outra linha de certificado). Quem perde a corrida (dois downloads ou a emissão e um download ao mesmo tempo) apaga o PRÓPRIO arquivo e devolve o do vencedor: sobra um caminho e um arquivo só.
/// <c>null</c> = não há certificado (treinamento que não emite, pessoa que não concluiu, ou código que não é versão 4, que nunca vai para um PDF).
/// </summary>
public sealed class GarantidorDoPdfDoCertificado(ICertificadosRepositorio certificados, IGeradorDePdfDoCertificado gerador, IArmazenamentoDeArquivos armazenamento, IConfiguracaoDaEmpresa empresa, IEnderecoDaConferencia conferencia)
{
    public async Task<string?> GarantirAsync(int treinamentoId, int funcionarioId, CancellationToken ct)
    {
        var certificado = await certificados.LerAsync(treinamentoId, funcionarioId, ct);
        if (certificado is not { EmitidoEm: { } emitidoEm, CodigoValidacao: { } codigo } || !CodigoDoCertificado.EhVersao4(codigo))
        {
            return null;
        }

        if (certificado.CaminhoArquivo is not null)
        {
            return certificado.CaminhoArquivo;
        }

        var nomeDaEmpresa = (await empresa.ObterAsync(ct))?.NomeEmpresa;
        var pdf = gerador.Gerar(new CertificadoParaPdf(certificado.NomeDaPessoa, certificado.TituloDoTreinamento, certificado.CargaHorariaMinutos, NumeroDoCertificado.De(emitidoEm.Year, certificado.Id), emitidoEm, codigo, nomeDaEmpresa, conferencia.Endereco));
        var caminho = CaminhosDeArquivo.Novo(LimitesDoCertificado.Pasta, ".pdf");
        await using (var fluxo = new MemoryStream(pdf))
        {
            await armazenamento.GravarAsync(caminho, fluxo, "application/pdf", ct);
        }

        bool gravou;
        try
        {
            gravou = await certificados.GravarCaminhoAsync(treinamentoId, funcionarioId, caminho, ct);
        }
        catch (Exception)
        {
            await armazenamento.ApagarAsync(caminho, CancellationToken.None);
            throw;
        }

        if (gravou)
        {
            return caminho;
        }

        await armazenamento.ApagarAsync(caminho, CancellationToken.None); // perdeu a corrida: o arquivo do vencedor é o que vale
        return (await certificados.LerAsync(treinamentoId, funcionarioId, ct))?.CaminhoArquivo;
    }
}

/// <summary>O link do certificado e o nome do download (<c>certificado-&lt;título&gt;.pdf</c>, limpo); 404 se não há certificado.</summary>
internal static class LinkDoCertificado
{
    public static async Task<LinkTemporario> CriarAsync(GarantidorDoPdfDoCertificado garantidor, ICertificadosRepositorio certificados, IArmazenamentoDeArquivos armazenamento, int treinamentoId, int funcionarioId, CancellationToken ct)
    {
        var caminho = await garantidor.GarantirAsync(treinamentoId, funcionarioId, ct) ?? throw new NaoEncontradoException();
        var titulo = (await certificados.LerAsync(treinamentoId, funcionarioId, ct))?.TituloDoTreinamento ?? string.Empty;
        return await armazenamento.CriarLinkTemporarioAsync(caminho, NomesDeArquivoDoTreinamento.PdfDoCertificado(titulo), ct);
    }
}

/// <summary>
/// <c>GET /api/eu/treinamentos/{treinamentoId}/certificado/download</c>: o link temporário do PDF do PRÓPRIO certificado. 404 se a pessoa não concluiu ou o treinamento não emite certificado (nunca o de outra pessoa: a rota só olha a linha dela). Se a linha ainda está sem arquivo (a emissão depois da conclusão falhou), gera na hora, uma vez. Sem evento de auditoria.
/// </summary>
public sealed class BaixarMeuCertificadoCasoDeUso(GarantidorDoPdfDoCertificado garantidor, ICertificadosRepositorio certificados, IArmazenamentoDeArquivos armazenamento, IEscopoDoUsuario escopo)
{
    public async Task<LinkTemporario> ExecutarAsync(int treinamentoId, CancellationToken ct) => await LinkDoCertificado.CriarAsync(garantidor, certificados, armazenamento, treinamentoId, escopo.FuncionarioId, ct);
}

/// <summary>
/// <c>GET /api/rh/treinamentos/{treinamentoId}/participantes/{funcionarioId}/certificado/download</c>: o RH baixa o certificado de outra pessoa. 404 como na rota da pessoa. Grava o evento <c>BAIXOU</c> ANTES de entregar o link, com o título e SEM o nome da pessoa nem o código.
/// </summary>
public sealed class BaixarCertificadoDeParticipanteCasoDeUso(GarantidorDoPdfDoCertificado garantidor, ICertificadosRepositorio certificados, IArmazenamentoDeArquivos armazenamento, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public async Task<LinkTemporario> ExecutarAsync(int treinamentoId, int funcionarioId, CancellationToken ct)
    {
        var certificado = (await certificados.LerAsync(treinamentoId, funcionarioId, ct)).OuNaoEncontrado();
        _ = await garantidor.GarantirAsync(treinamentoId, funcionarioId, ct) ?? throw new NaoEncontradoException();
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Baixou, $"Baixou o certificado do treinamento \"{certificado.TituloDoTreinamento}\" de uma pessoa.", $"Treinamento \"{certificado.TituloDoTreinamento}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);
        return await LinkDoCertificado.CriarAsync(garantidor, certificados, armazenamento, treinamentoId, funcionarioId, ct);
    }
}
