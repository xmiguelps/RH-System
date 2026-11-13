using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Fazer;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;

namespace RHSystem.Application.Treinamentos.Publico;

/// <summary>A resposta da conferência pública (<c>CertificadoValidacaoResposta</c>): SÓ quatro campos (nenhum id, número, setor, e-mail nem situação da pessoa).</summary>
public sealed record CertificadoValidacaoResposta(string CodigoValidacao, string TitularNome, string TreinamentoTitulo, DateOnly EmitidoEm);

/// <summary>
/// <c>GET /api/publico/certificados/{codigoValidacao}</c>: confere um certificado pelo código, sem login. Ordem: 400 (o texto não é um UUID: <c>errors.codigoValidacao</c>); 404 com o MESMO corpo (título "Certificado não encontrado.") para código que não existe, de treinamento que não emite certificado, de pessoa que não concluiu e de código que NÃO é UUID versão 4 (um v1 do trigger
/// nunca confere: o código previsível não pode ser usado para adivinhar o vizinho). O nome de quem saiu da empresa continua aparecendo (o código continua válido). Sem auditoria e sem registrar o código em log; o limite de consultas por IP (429) roda antes de tudo.
/// </summary>
public sealed class ValidarCertificadoCasoDeUso(ICertificadosRepositorio certificados)
{
    public async Task<CertificadoValidacaoResposta> ExecutarAsync(string? codigoValidacao, CancellationToken ct)
    {
        if (!Guid.TryParseExact(codigoValidacao?.Trim(), "D", out var uuid))
        {
            throw new ErroDeValidacaoException("codigoValidacao", "Informe o código de validação do certificado (um UUID).");
        }

        var codigo = uuid.ToString("D");
        if (!CodigoDoCertificado.EhVersao4(codigo))
        {
            throw MensagensDosTreinamentos.CertificadoNaoEncontrado;
        }

        var conferido = await certificados.ConferirPorCodigoAsync(codigo, ct) ?? throw MensagensDosTreinamentos.CertificadoNaoEncontrado;
        return new CertificadoValidacaoResposta(conferido.Codigo, conferido.TitularNome, conferido.TreinamentoTitulo, conferido.EmitidoEm);
    }
}
