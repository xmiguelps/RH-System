using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Documentos;

/// <summary>
/// Os rótulos que a tela mostra para cada tipo de documento (copiados do contrato, <c>DocumentoColaboradorTipo</c>). É o texto do log de download ("Baixou o documento {rótulo} de {nome}"). O domínio escreve o mesmo rótulo no
/// log de criação (<c>DocumentoDoColaborador.NomeParaAuditoria</c>); <c>RotulosDeDocumentoTests</c> confere que os dois não se afastam.
/// </summary>
public static class RotulosDeDocumento
{
    public static string Tipo(TipoDeDocumento tipo) => tipo switch
    {
        TipoDeDocumento.Identidade => "Documento de identidade (RG ou CNH)",
        TipoDeDocumento.Cpf => "CPF",
        TipoDeDocumento.CarteiraDeTrabalho => "Carteira de trabalho digital",
        TipoDeDocumento.ComprovanteDeResidencia => "Comprovante de residência",
        TipoDeDocumento.ContratoAssinado => "Contrato assinado",
        TipoDeDocumento.ExameAdmissional => "Exame admissional (ASO)",
        _ => "Outro",
    };
}
