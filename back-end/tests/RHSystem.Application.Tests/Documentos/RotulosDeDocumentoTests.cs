using RHSystem.Application.Documentos;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;

namespace RHSystem.Application.Tests.Documentos;

public class RotulosDeDocumentoTests
{
    [Fact]
    public void RotulosDeDocumento_tem_o_rotulo_do_contrato_para_cada_tipo()
    {
        Assert.Equal("Documento de identidade (RG ou CNH)", RotulosDeDocumento.Tipo(TipoDeDocumento.Identidade));
        Assert.Equal("CPF", RotulosDeDocumento.Tipo(TipoDeDocumento.Cpf));
        Assert.Equal("Carteira de trabalho digital", RotulosDeDocumento.Tipo(TipoDeDocumento.CarteiraDeTrabalho));
        Assert.Equal("Comprovante de residência", RotulosDeDocumento.Tipo(TipoDeDocumento.ComprovanteDeResidencia));
        Assert.Equal("Contrato assinado", RotulosDeDocumento.Tipo(TipoDeDocumento.ContratoAssinado));
        Assert.Equal("Exame admissional (ASO)", RotulosDeDocumento.Tipo(TipoDeDocumento.ExameAdmissional));
        Assert.Equal("Outro", RotulosDeDocumento.Tipo(TipoDeDocumento.Outro));
    }

    [Fact]
    public void Todo_tipo_do_enum_tem_rotulo_e_e_o_mesmo_que_o_log_do_dominio_mostra()
    {
        // Um tipo novo no enum sem rótulo (ou com rótulo diferente no domínio, que escreve o log) falha aqui: o log e a tela falam igual.
        var pessoa = Funcionario.Criar("Ana Souza", "ana.souza@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso("RH"));
        foreach (var tipo in Enum.GetValues<TipoDeDocumento>())
        {
            var rotulo = RotulosDeDocumento.Tipo(tipo);

            Assert.False(string.IsNullOrWhiteSpace(rotulo), $"{tipo} está sem rótulo");
            Assert.Equal(rotulo, DocumentoDoColaborador.Pedir(pessoa, tipo, null, null, new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)).NomeParaAuditoria);
        }
    }
}
