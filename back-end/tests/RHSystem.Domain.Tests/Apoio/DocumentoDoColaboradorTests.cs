using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Documentos;

namespace RHSystem.Domain.Tests.Apoio;

public class DocumentoDoColaboradorTests
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static Funcionario Pessoa() => Funcionario.Criar("Bruno Lima", "bruno.lima@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso("Funcionario"));

    [Fact]
    public void DocumentoDoColaborador_Anexar_nasce_Enviado_com_arquivo_e_Pedir_nasce_Pendente_sem_arquivo_com_PedidoEmUtc()
    {
        var pessoa = Pessoa();

        var anexado = DocumentoDoColaborador.Anexar(pessoa, TipoDeDocumento.Cpf, "Frente e verso", new DateOnly(2030, 1, 1), "documentos/1/abc.pdf", "cpf.pdf", Agora);
        var pedido = DocumentoDoColaborador.Pedir(pessoa, TipoDeDocumento.Identidade, null, null, Agora);

        Assert.Equal((SituacaoDoDocumento.Enviado, "documentos/1/abc.pdf", "cpf.pdf", (DateTime?)Agora, (DateTime?)null), (anexado.Situacao, anexado.CaminhoArquivo, anexado.NomeArquivoOriginal, anexado.EnviadoEmUtc, anexado.PedidoEmUtc));
        Assert.Equal((TipoDeDocumento.Cpf, "Frente e verso", (DateOnly?)new DateOnly(2030, 1, 1)), (anexado.Tipo, anexado.Descricao, anexado.DataValidade));
        Assert.Equal((SituacaoDoDocumento.Pendente, (string?)null, (string?)null, (DateTime?)null, (DateTime?)Agora), (pedido.Situacao, pedido.CaminhoArquivo, pedido.NomeArquivoOriginal, pedido.EnviadoEmUtc, pedido.PedidoEmUtc));
        Assert.Same(pessoa, pedido.Funcionario);
    }

    [Fact]
    public void DocumentoDoColaborador_ReceberArquivo_so_vale_para_Pendente_e_preserva_PedidoEmUtc()
    {
        var pessoa = Pessoa();
        var pedido = DocumentoDoColaborador.Pedir(pessoa, TipoDeDocumento.ComprovanteDeResidencia, "Conta de luz", null, Agora);
        var depois = Agora.AddHours(5);

        pedido.ReceberArquivo("documentos/1/xyz.jpg", "luz.jpg", depois);

        Assert.Equal((SituacaoDoDocumento.Enviado, "documentos/1/xyz.jpg", "luz.jpg", (DateTime?)depois, (DateTime?)Agora), (pedido.Situacao, pedido.CaminhoArquivo, pedido.NomeArquivoOriginal, pedido.EnviadoEmUtc, pedido.PedidoEmUtc));
        Assert.Throws<InvalidOperationException>(() => pedido.ReceberArquivo("documentos/1/outro.pdf", "outro.pdf", depois)); // já enviado: não se reenvia
        var anexado = DocumentoDoColaborador.Anexar(pessoa, TipoDeDocumento.Outro, "Diploma", null, "documentos/1/d.pdf", "diploma.pdf", Agora);
        Assert.Throws<InvalidOperationException>(() => anexado.ReceberArquivo("documentos/1/e.pdf", "e.pdf", depois)); // o que nasceu Enviado também não
        Assert.Equal("documentos/1/xyz.jpg", pedido.CaminhoArquivo);
    }

    [Fact]
    public void DocumentoDoColaborador_trunca_os_instantes_ao_segundo()
    {
        var comFracao = Agora.AddMilliseconds(700); // 12:42:00.700Z
        var pessoa = Pessoa();

        var anexado = DocumentoDoColaborador.Anexar(pessoa, TipoDeDocumento.Cpf, null, null, "d/1.pdf", "1.pdf", comFracao);
        var pedido = DocumentoDoColaborador.Pedir(pessoa, TipoDeDocumento.Cpf, null, null, comFracao);
        pedido.ReceberArquivo("d/2.pdf", "2.pdf", comFracao.AddHours(1));

        Assert.Equal(Agora, anexado.EnviadoEmUtc);
        Assert.Equal(Agora, pedido.PedidoEmUtc);
        Assert.Equal(Agora.AddHours(1), pedido.EnviadoEmUtc);
        Assert.Equal(DateTimeKind.Utc, anexado.EnviadoEmUtc!.Value.Kind);
    }

    [Fact]
    public void DocumentoDoColaborador_recusa_descricao_e_nome_de_arquivo_acima_de_255_e_pessoa_nula()
    {
        var pessoa = Pessoa();
        var longo = new string('x', 256);

        Assert.Throws<ArgumentException>(() => DocumentoDoColaborador.Pedir(pessoa, TipoDeDocumento.Outro, longo, null, Agora));
        Assert.Throws<ArgumentException>(() => DocumentoDoColaborador.Anexar(pessoa, TipoDeDocumento.Outro, null, null, "d/1.pdf", longo, Agora));
        Assert.Throws<ArgumentException>(() => DocumentoDoColaborador.Anexar(pessoa, TipoDeDocumento.Outro, null, null, longo, "1.pdf", Agora));
        Assert.Throws<ArgumentNullException>(() => DocumentoDoColaborador.Pedir(null!, TipoDeDocumento.Outro, null, null, Agora));
        Assert.Equal(new string('x', 255), DocumentoDoColaborador.Pedir(pessoa, TipoDeDocumento.Outro, new string('x', 255), null, Agora).Descricao);
    }

    [Theory]
    [InlineData(TipoDeDocumento.Identidade, "Documento de identidade (RG ou CNH)")]
    [InlineData(TipoDeDocumento.Cpf, "CPF")]
    [InlineData(TipoDeDocumento.CarteiraDeTrabalho, "Carteira de trabalho digital")]
    [InlineData(TipoDeDocumento.ComprovanteDeResidencia, "Comprovante de residência")]
    [InlineData(TipoDeDocumento.ContratoAssinado, "Contrato assinado")]
    [InlineData(TipoDeDocumento.ExameAdmissional, "Exame admissional (ASO)")]
    [InlineData(TipoDeDocumento.Outro, "Outro")]
    public void DocumentoDoColaborador_diz_o_rotulo_do_tipo_e_o_registro_afetado_com_o_nome_da_pessoa(TipoDeDocumento tipo, string rotulo)
    {
        var documento = DocumentoDoColaborador.Pedir(Pessoa(), tipo, null, null, Agora);

        Assert.Equal(rotulo, documento.NomeParaAuditoria); // os rótulos da tela, copiados do contrato (DocumentoColaboradorTipo)
        Assert.Equal($"Documento {rotulo} de Bruno Lima", documento.RegistroAfetado);
    }
}
