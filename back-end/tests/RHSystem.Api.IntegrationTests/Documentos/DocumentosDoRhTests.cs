using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Auditoria;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>
/// Os documentos da ficha (<c>/api/rh/funcionarios/{funcionarioId}/documentos</c>), só para o RH, no banco REAL com o relógio fixo em 2026-10-07T12:42Z e o armazenamento em memória. O tipo do arquivo vem do
/// CONTEÚDO; o arquivo só é gravado depois de todas as validações; "Pedir para enviar" não manda e-mail. Cada teste cria a pessoa dele. O fluxo do arquivo (transação, falhas, limpeza) é de <see cref="UploadDeDocumentoTests"/>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DocumentosDoRhTests(MySqlFixture mysql)
{
    private const string ContratoAssinado = "CONTRATO_ASSINADO";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(DocumentosDoRhTests));

    private static string Rota(int funcionarioId) => $"/api/rh/funcionarios/{funcionarioId}/documentos";

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, bool ativa = true) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, ativo: ativa);

    private static Task<HttpResponseMessage> PostarAsync(ApiDeAcesso api, int funcionarioId, MultipartFormDataContent formulario) => ComoRh(api).PostAsync(Rota(funcionarioId), formulario);

    private static async Task<long> LinhasDaPessoaAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO = {funcionarioId}");

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int documentoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS_DOCUMENTOS", documentoId);

    private static string NovoCaminho() => CaminhosDeArquivo.Novo("documentos", ".pdf");

    // ---------------------------------------------------------------- listar

    [Fact]
    public async Task Lista_devolve_os_documentos_da_pessoa_mais_recentes_primeiro_com_os_campos_do_contrato_e_sem_caminho()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Lista1 Pessoa");
        var caminho = NovoCaminho();
        var identidade = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "IDENTIDADE", "ENVIADO", null, caminho, "rg-lista1.pdf", enviadoEm: new DateTime(2022, 3, 14, 10, 20, 0));
        var comprovante = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "COMPROVANTE_DE_RESIDENCIA", "PENDENTE", "Comprovante atualizado", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        var cpfA = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 5, 8, 0, 0));
        var cpfB = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 5, 8, 0, 0));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_VALIDADE = '2027-10-01' WHERE ID_FUNCIONARIO_DOCUMENTO = {comprovante}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_CADASTRO = '2026-01-10 10:00:00' WHERE ID_FUNCIONARIO_DOCUMENTO = {identidade}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_CADASTRO = '2026-10-01 09:00:00' WHERE ID_FUNCIONARIO_DOCUMENTO = {comprovante}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_CADASTRO = '2026-10-05 08:00:00' WHERE ID_FUNCIONARIO_DOCUMENTO IN ({cpfA}, {cpfB})");

        var resposta = await ComoRh(api).GetAsync(Rota(pessoa));

        var itens = await ItensAsync(resposta);
        Assert.Equal([cpfB, cpfA, comprovante, identidade], itens.Select(i => i.GetProperty("id").GetInt32())); // mais recentes primeiro; no empate, o id maior
        var enviado = itens[3];
        Assert.Equal(["id", "tipo", "descricao", "dataValidade", "situacao", "nomeArquivoOriginal", "pedidoEmUtc", "enviadoEmUtc"], enviado.EnumerateObject().Select(p => p.Name)); // os oito campos do contrato e nada mais (nunca o caminho)
        Assert.Equal(("IDENTIDADE", "ENVIADO", "rg-lista1.pdf", "2022-03-14T13:20:00Z"), (enviado.GetProperty("tipo").GetString(), enviado.GetProperty("situacao").GetString(), enviado.GetProperty("nomeArquivoOriginal").GetString(), enviado.GetProperty("enviadoEmUtc").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null, JsonValueKind.Null), (enviado.GetProperty("descricao").ValueKind, enviado.GetProperty("dataValidade").ValueKind, enviado.GetProperty("pedidoEmUtc").ValueKind));
        var pendente = itens[2];
        Assert.Equal(("COMPROVANTE_DE_RESIDENCIA", "Comprovante atualizado", "2027-10-01", "PENDENTE", "2026-10-01T12:00:00Z"), (pendente.GetProperty("tipo").GetString(), pendente.GetProperty("descricao").GetString(), pendente.GetProperty("dataValidade").GetString(), pendente.GetProperty("situacao").GetString(), pendente.GetProperty("pedidoEmUtc").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (pendente.GetProperty("nomeArquivoOriginal").ValueKind, pendente.GetProperty("enviadoEmUtc").ValueKind));
        Assert.DoesNotContain(caminho, await resposta.Content.ReadAsStringAsync());

        var semNenhum = await PessoaAsync(api, "Lista1 Sem documentos");
        Assert.Empty(await ItensAsync(await ComoRh(api).GetAsync(Rota(semNenhum))));
    }

    [Fact]
    public async Task Lista_so_traz_os_documentos_da_pessoa_da_rota()
    {
        var api = await ApiAsync();
        var a = await PessoaAsync(api, "Lista2 A");
        var b = await PessoaAsync(api, "Lista2 B");
        var deA = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, a, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, b, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        Assert.Equal([deA], (await ItensAsync(await ComoRh(api).GetAsync(Rota(a)))).Select(i => i.GetProperty("id").GetInt32()));
    }

    [Fact]
    public async Task Funcionario_inexistente_devolve_404_nas_tres_rotas_e_listar_funciona_para_inativo()
    {
        var api = await ApiAsync();
        var inativa = await PessoaAsync(api, "Inexistente3 inativa", ativa: false);
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, inativa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        api.Api.Armazenamento.Limpar();

        Assert.Equal(404, (int)(await ComoRh(api).GetAsync(Rota(int.MaxValue))).StatusCode);
        Assert.Equal(404, (int)(await PostarAsync(api, int.MaxValue, FormulariosDeTeste.Pedido("CPF"))).StatusCode);
        Assert.Equal(404, (int)(await PostarAsync(api, int.MaxValue, FormulariosDeTeste.Anexo("CPF", "a.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
        Assert.Equal(404, (int)(await ComoRh(api).GetAsync($"{Rota(int.MaxValue)}/1/download")).StatusCode);
        Assert.Empty(api.Api.Armazenamento.Gravados); // o 404 vem antes de gravar qualquer coisa

        Assert.Single(await ItensAsync(await ComoRh(api).GetAsync(Rota(inativa)))); // inativo também lista
    }

    // ---------------------------------------------------------------- adicionar: anexar agora

    [Fact]
    public async Task Anexar_agora_com_PDF_devolve_201_ENVIADO_com_arquivo_no_armazenamento_nome_original_enviadoEmUtc_do_relogio_e_pedidoEmUtc_null()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Anexar4 Pessoa");
        api.Api.Armazenamento.Limpar();
        var bytes = ArquivosDeTeste.Pdf(2048);

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo(ContratoAssinado, "contrato assinado.pdf", bytes, "Via do RH", "2027-10-01"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(["id", "tipo", "descricao", "dataValidade", "situacao", "nomeArquivoOriginal", "pedidoEmUtc", "enviadoEmUtc"], corpo.EnumerateObject().Select(p => p.Name));
        Assert.Equal((ContratoAssinado, "Via do RH", "2027-10-01", "ENVIADO", "contrato assinado.pdf", "2026-10-07T12:42:00Z"), (corpo.GetProperty("tipo").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("dataValidade").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("nomeArquivoOriginal").GetString(), corpo.GetProperty("enviadoEmUtc").GetString()));
        Assert.Equal(JsonValueKind.Null, corpo.GetProperty("pedidoEmUtc").ValueKind);
        Assert.EndsWith($"/documentos/{id}/download", resposta.Headers.Location!.ToString());
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Matches(@"^documentos/[0-9a-f]{32}\.pdf$", caminho);
        Assert.Equal(bytes, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal("application/pdf", api.Api.Armazenamento.TipoDeConteudo(caminho));
        Assert.Equal(caminho, await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}"));
        Assert.DoesNotContain(caminho, await resposta.Content.ReadAsStringAsync());
    }

    [Theory]
    [InlineData("pdf", "x.jpg", "image/jpeg", ".pdf", "application/pdf")] // um PDF chamado .jpg (e declarado como JPEG) é um PDF
    [InlineData("jpeg", "foto.pdf", "application/pdf", ".jpg", "image/jpeg")] // um JPEG chamado .pdf é um JPEG
    [InlineData("png", "imagem.txt", "text/plain", ".png", "image/png")]
    public async Task Anexar_agora_aceita_PDF_JPG_e_PNG_e_o_tipo_vem_do_conteudo_nao_da_extensao_nem_do_Content_Type(string conteudo, string nomeEnviado, string tipoDeclarado, string extensaoEsperada, string tipoEsperado)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"Tipo5 {conteudo}");
        api.Api.Armazenamento.Limpar();
        var bytes = conteudo switch { "pdf" => ArquivosDeTeste.Pdf(), "jpeg" => ArquivosDeTeste.Jpeg(), _ => ArquivosDeTeste.Png() };

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("OUTRO", nomeEnviado, bytes, tipoDeConteudo: tipoDeclarado));

        Assert.Equal(201, (int)resposta.StatusCode);
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.EndsWith(extensaoEsperada, caminho);
        Assert.Equal(tipoEsperado, api.Api.Armazenamento.TipoDeConteudo(caminho));
        Assert.Equal(nomeEnviado, (await CorpoAsync(resposta)).GetProperty("nomeArquivoOriginal").GetString()); // o nome que a pessoa deu fica como está (limpo)
    }

    [Theory]
    [InlineData("exe")]
    [InlineData("gif")]
    [InlineData("texto")]
    public async Task Anexar_agora_recusa_exe_chamado_pdf_gif_e_texto_com_400_em_arquivo_e_nada_no_armazenamento(string conteudo)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"Recusa6 {conteudo}");
        api.Api.Armazenamento.Limpar();
        var bytes = conteudo switch { "exe" => ArquivosDeTeste.Exe(), "gif" => ArquivosDeTeste.Gif(), _ => ArquivosDeTeste.Texto() };

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("OUTRO", "documento.pdf", bytes, tipoDeConteudo: "application/pdf")); // o nome e o Content-Type mentem

        Assert.True((await ErrosAsync(resposta)).TryGetProperty("arquivo", out _));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Fact]
    public async Task Arquivo_de_exatamente_10_MiB_e_aceito_e_o_de_10_MiB_mais_1_byte_e_recusado()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Limite7 Pessoa");
        api.Api.Armazenamento.Limpar();

        var aceito = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("OUTRO", "no-limite.pdf", ArquivosDeTeste.Pdf(10 * 1024 * 1024)));
        var recusado = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("OUTRO", "passou.pdf", ArquivosDeTeste.Pdf((10 * 1024 * 1024) + 1)));

        Assert.Equal(201, (int)aceito.StatusCode);
        Assert.True((await ErrosAsync(recusado)).TryGetProperty("arquivo", out _));
        Assert.Single(api.Api.Armazenamento.Gravados); // só o de 10 MiB exatos
        Assert.Equal(1, await LinhasDaPessoaAsync(api, pessoa));
    }

    // ---------------------------------------------------------------- adicionar: pedir para enviar

    [Fact]
    public async Task Pedir_para_enviar_devolve_201_PENDENTE_com_pedidoEmUtc_sem_arquivo_e_sem_nada_no_armazenamento()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Pedir8 Pessoa");
        api.Api.Armazenamento.Limpar();

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("COMPROVANTE_DE_RESIDENCIA", "Comprovante atualizado", "2027-10-01"));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("COMPROVANTE_DE_RESIDENCIA", "Comprovante atualizado", "2027-10-01", "PENDENTE", "2026-10-07T12:42:00Z"), (corpo.GetProperty("tipo").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("dataValidade").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("pedidoEmUtc").GetString()));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (corpo.GetProperty("nomeArquivoOriginal").ValueKind, corpo.GetProperty("enviadoEmUtc").ValueKind));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, api.Api.Armazenamento.Quantidade);
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {corpo.GetProperty("id").GetInt32()}"));
    }

    [Fact]
    public async Task Pedir_para_enviar_nao_enfileira_nenhum_email()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Email9 Pessoa");
        var antes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS");

        Assert.Equal(201, (int)(await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("CPF"))).StatusCode);
        Assert.Equal(201, (int)(await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("CPF", "cpf.pdf", ArquivosDeTeste.Pdf()))).StatusCode);

        Assert.Equal(antes, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS")); // a lista de e-mails é fechada: não há e-mail de pedido de documento
    }

    // ---------------------------------------------------------------- adicionar: erros

    [Theory]
    [InlineData("anexar-sem-arquivo")]
    [InlineData("pedir-com-arquivo")]
    [InlineData("dois-arquivos")]
    [InlineData("arquivo-so-em-outro-campo")]
    [InlineData("arquivo-e-outro-arquivo-em-outro-campo")]
    [InlineData("zero-bytes")]
    [InlineData("grande-demais")]
    public async Task Erros_de_arquivo_voltam_400_em_arquivo(string cenario)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"Arquivo10 {cenario}");
        api.Api.Armazenamento.Limpar();
        var pdf = ArquivosDeTeste.Pdf();
        var formulario = cenario switch
        {
            "anexar-sem-arquivo" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF"),
            "pedir-com-arquivo" => FormulariosDeTeste.Documento("PEDIR_PARA_ENVIAR", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf)),
            "dois-arquivos" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf), new FormulariosDeTeste.Parte("arquivo", "b.pdf", pdf)),
            "arquivo-so-em-outro-campo" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, new FormulariosDeTeste.Parte("outro", "a.pdf", pdf)),
            "arquivo-e-outro-arquivo-em-outro-campo" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf), new FormulariosDeTeste.Parte("extra", "b.pdf", pdf)),
            "zero-bytes" => FormulariosDeTeste.Anexo("CPF", "vazio.pdf", []),
            _ => FormulariosDeTeste.Anexo("CPF", "enorme.pdf", ArquivosDeTeste.Pdf((10 * 1024 * 1024) + 1)),
        };

        var resposta = await PostarAsync(api, pessoa, formulario);

        Assert.True((await ErrosAsync(resposta)).TryGetProperty("arquivo", out _));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Theory]
    [InlineData("anexar-sem-arquivo", "O arquivo é obrigatório neste modo.")]
    [InlineData("pedir-com-arquivo", "Neste modo não se envia arquivo: a pessoa o envia pelo portal.")]
    [InlineData("dois-arquivos", "Envie um arquivo só.")]
    [InlineData("zero-bytes", "O arquivo está vazio.")]
    [InlineData("grande-demais", "O arquivo pode ter até 10 MB.")]
    [InlineData("tipo-nao-aceito", "Envie um arquivo PDF, JPG ou PNG.")]
    public async Task Cada_erro_de_arquivo_diz_o_que_fazer_em_portugues(string cenario, string mensagem)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"Mensagem10b {cenario}");
        var pdf = ArquivosDeTeste.Pdf();
        var formulario = cenario switch
        {
            "anexar-sem-arquivo" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF"),
            "pedir-com-arquivo" => FormulariosDeTeste.Documento("PEDIR_PARA_ENVIAR", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf)),
            "dois-arquivos" => FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf), new FormulariosDeTeste.Parte("arquivo", "b.pdf", pdf)),
            "zero-bytes" => FormulariosDeTeste.Anexo("CPF", "vazio.pdf", []),
            "grande-demais" => FormulariosDeTeste.Anexo("CPF", "enorme.pdf", ArquivosDeTeste.Pdf((10 * 1024 * 1024) + 1)),
            _ => FormulariosDeTeste.Anexo("CPF", "mentira.pdf", ArquivosDeTeste.Exe()),
        };

        var erros = await ErrosAsync(await PostarAsync(api, pessoa, formulario));

        Assert.Equal([mensagem], erros.GetProperty("arquivo").EnumerateArray().Select(m => m.GetString()));
    }

    [Theory]
    [InlineData("TALVEZ", "CPF", null, null, "modo")]
    [InlineData("pedir_para_enviar", "CPF", null, null, "modo")] // só o nome do contrato vale
    [InlineData(null, "CPF", null, null, "modo")]
    [InlineData("PEDIR_PARA_ENVIAR", "INEXISTENTE", null, null, "tipo")]
    [InlineData("PEDIR_PARA_ENVIAR", "Cpf", null, null, "tipo")]
    [InlineData("PEDIR_PARA_ENVIAR", null, null, null, "tipo")]
    [InlineData("PEDIR_PARA_ENVIAR", "CPF", "LONGA", null, "descricao")]
    [InlineData("PEDIR_PARA_ENVIAR", "CPF", null, "2027-13-01", "dataValidade")]
    [InlineData("PEDIR_PARA_ENVIAR", "CPF", null, "01/10/2027", "dataValidade")]
    [InlineData("PEDIR_PARA_ENVIAR", "CPF", null, "2027-02-30", "dataValidade")]
    [InlineData("PEDIR_PARA_ENVIAR", "CPF", null, "2027-1-5", "dataValidade")] // só yyyy-MM-dd, com os zeros
    public async Task Erros_de_campo_voltam_400_com_o_nome_do_campo(string? modo, string? tipo, string? descricao, string? dataValidade, string campo)
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, $"Campo11 {campo} {Guid.NewGuid():N}"[..40]);
        api.Api.Armazenamento.Limpar();
        var texto = descricao == "LONGA" ? new string('d', 256) : descricao;

        var erros = await ErrosAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Documento(modo, tipo, texto, dataValidade)));

        Assert.True(erros.TryGetProperty(campo, out _), $"o 400 devia acusar {campo}");
        Assert.Single(erros.EnumerateObject()); // só o campo errado é acusado
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Fact]
    public async Task Campos_no_limite_e_data_passada_sao_aceitos_e_erros_de_campo_e_de_arquivo_vem_juntos()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Limites12 Pessoa");

        var aceito = await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("OUTRO", new string('d', 255), "2020-01-01")); // 255 é o limite; data passada vale
        var juntos = await ErrosAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Documento("ANEXAR_AGORA", "XYZ", new string('d', 256), "ontem")));

        Assert.Equal(201, (int)aceito.StatusCode);
        Assert.Equal(["arquivo", "dataValidade", "descricao", "tipo"], juntos.EnumerateObject().Select(p => p.Name).Order()); // todos os erros de uma vez
    }

    [Fact]
    public async Task Corpo_json_em_vez_de_formulario_devolve_415()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Json13 Pessoa");

        var resposta = await ComoRh(api).PostAsJsonAsync(Rota(pessoa), new { modo = "PEDIR_PARA_ENVIAR", tipo = "CPF" });

        Assert.Equal(415, (int)resposta.StatusCode);
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Fact]
    public async Task Nome_do_arquivo_que_veio_com_caminho_e_limpo_antes_de_guardar_e_devolver()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Nome13a Pessoa");
        api.Api.Armazenamento.Limpar();

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("OUTRO", "C:/fakepath/../segredos/contrato final.pdf", ArquivosDeTeste.Pdf())); // um caminho de diretório no nome (barra normal: a invertida é escape no cabeçalho do multipart)

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal("contrato final.pdf", corpo.GetProperty("nomeArquivoOriginal").GetString());
        Assert.Equal("contrato final.pdf", await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {corpo.GetProperty("id").GetInt32()}"));
    }

    [Fact]
    public async Task Formulario_urlencoded_devolve_415_e_multipart_malformado_devolve_400_nunca_500()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Formato13b Pessoa");
        var cliente = ComoRh(api);

        // O contrato só aceita multipart/form-data: o formulário comum de navegador (urlencoded) não.
        var comum = await cliente.PostAsync(Rota(pessoa), new FormUrlEncodedContent([new("modo", "PEDIR_PARA_ENVIAR"), new("tipo", "CPF")]));
        // Um corpo que diz ser multipart e não é: dado inválido (400), não erro do servidor.
        var quebrado = new StringContent("isto nao e um corpo multipart", System.Text.Encoding.UTF8, "multipart/form-data");
        quebrado.Headers.ContentType = new System.Net.Http.Headers.MediaTypeHeaderValue("multipart/form-data") { Parameters = { new System.Net.Http.Headers.NameValueHeaderValue("boundary", "limite-que-nao-aparece") } };
        var malformado = await cliente.PostAsync(Rota(pessoa), quebrado);

        Assert.Equal(415, (int)comum.StatusCode);
        Assert.Equal(400, (int)malformado.StatusCode);
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Fact]
    public async Task Dois_anexos_do_mesmo_tipo_criam_dois_documentos()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Dois14 Pessoa");
        api.Api.Armazenamento.Limpar();

        var primeiro = await CorpoAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("CPF", "cpf-1.pdf", ArquivosDeTeste.Pdf())));
        var segundo = await CorpoAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("CPF", "cpf-2.pdf", ArquivosDeTeste.Pdf())));
        var pendente = await CorpoAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("CPF"))); // nem um pendente do mesmo tipo é substituído

        Assert.Equal(3, new[] { primeiro, segundo, pendente }.Select(c => c.GetProperty("id").GetInt32()).Distinct().Count());
        Assert.Equal(3, await LinhasDaPessoaAsync(api, pessoa));
        Assert.Equal(2, api.Api.Armazenamento.Gravados.Distinct().Count()); // dois arquivos, dois caminhos
    }

    [Fact]
    public async Task Descricao_com_marcacao_html_e_guardada_e_devolvida_como_texto_literal()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Html15 Pessoa");
        const string Marcacao = "<b>negrito</b> <script>alert('x')</script> & mais";

        var resposta = await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("OUTRO", Marcacao));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(Marcacao, (await CorpoAsync(resposta)).GetProperty("descricao").GetString());
        Assert.Equal(Marcacao, (await ItensAsync(await ComoRh(api).GetAsync(Rota(pessoa))))[0].GetProperty("descricao").GetString());
    }

    // ---------------------------------------------------------------- auditoria

    [Fact]
    public async Task Anexar_e_pedir_gravam_Criou_na_area_Documentos_sem_o_nome_do_arquivo_e_sem_o_caminho()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Auditoria16 Pessoa");
        api.Api.Armazenamento.Limpar();

        var anexado = await CorpoAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo(ContratoAssinado, "segredo-do-joao.pdf", ArquivosDeTeste.Pdf())));
        var pedido = await CorpoAsync(await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("CPF")));

        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        var eventoDoAnexo = Assert.Single(await EventosAsync(api, anexado.GetProperty("id").GetInt32()));
        Assert.Equal((AuditoriaArea.Documentos, AuditoriaAcao.Criou, "Documento Contrato assinado de Auditoria16 Pessoa", api.Elenco.Rh.FuncionarioId), (eventoDoAnexo.Area, eventoDoAnexo.Acao, eventoDoAnexo.RegistroAfetado, eventoDoAnexo.FuncionarioAtorId));
        var arquivo = Assert.Single(eventoDoAnexo.Mudancas, m => m.Campo == "Arquivo");
        Assert.Equal((true, (string?)null, (string?)null), (arquivo.ValorOculto, arquivo.ValorAntes, arquivo.ValorDepois)); // mudou, mas o nome não é registrado
        var eventoDoPedido = Assert.Single(await EventosAsync(api, pedido.GetProperty("id").GetInt32()));
        Assert.Equal((AuditoriaArea.Documentos, AuditoriaAcao.Criou), (eventoDoPedido.Area, eventoDoPedido.Acao));

        // Nem o nome do arquivo nem o caminho aparecem em lugar nenhum do log de auditoria.
        var tudo = await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS") + await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_MUDANCAS");
        Assert.DoesNotContain("segredo-do-joao", tudo);
        Assert.DoesNotContain(caminho, tudo);
        Assert.DoesNotContain(caminho[("documentos/".Length)..^4], tudo);
    }

    // ---------------------------------------------------------------- baixar

    [Fact]
    public async Task Download_devolve_o_link_com_nome_para_download_e_grava_Baixou_antes_de_devolver()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Baixar17 Pessoa");
        var caminho = NovoCaminho();
        var documento = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, ContratoAssinado, "ENVIADO", null, caminho, "Contrato do João.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        var resposta = await ComoRh(api).GetAsync($"{Rota(pessoa)}/{documento}/download");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(($"https://arquivos.exemplo.test/{caminho}?nome=Contrato%20do%20Jo%C3%A3o.pdf", "2026-10-07T12:47:00Z"), (corpo.GetProperty("url").GetString(), corpo.GetProperty("expiraEmUtc").GetString()));
        var evento = Assert.Single(await EventosAsync(api, documento));
        Assert.Equal((AuditoriaArea.Documentos, AuditoriaAcao.Baixou, "Baixou o documento Contrato assinado de Baixar17 Pessoa", "Documento Contrato assinado de Baixar17 Pessoa", api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.FuncionarioAtorId));
    }

    [Fact]
    public async Task Download_de_documento_pendente_devolve_422_DOCUMENTO_SEM_ARQUIVO_com_o_texto_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Pendente18 Pessoa");
        var pendente = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        var resposta = await ComoRh(api).GetAsync($"{Rota(pessoa)}/{pendente}/download");

        Assert.Equal((422, "DOCUMENTO_SEM_ARQUIVO", "Este documento ainda não foi enviado."), await ProblemaAsync(resposta));
        Assert.Empty(await EventosAsync(api, pendente)); // sem arquivo, não houve download para registrar
    }

    [Fact]
    public async Task Download_de_documento_de_outra_pessoa_pela_ficha_errada_devolve_404_igual_ao_inexistente()
    {
        var api = await ApiAsync();
        var dona = await PessoaAsync(api, "Dona19 Pessoa");
        var outra = await PessoaAsync(api, "Outra19 Pessoa");
        var documento = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, dona, "CPF", "ENVIADO", null, NovoCaminho(), "cpf.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        var pelaFichaErrada = await ComoRh(api).GetAsync($"{Rota(outra)}/{documento}/download");
        var inexistente = await ComoRh(api).GetAsync($"{Rota(outra)}/{int.MaxValue}/download");

        Assert.Equal(404, (int)pelaFichaErrada.StatusCode);
        var (a, b) = (await CorpoAsync(pelaFichaErrada), await CorpoAsync(inexistente));
        Assert.Equal((404, b.GetProperty("title").GetString(), b.GetProperty("detail").GetString()), (404, a.GetProperty("title").GetString(), a.GetProperty("detail").GetString())); // nada diz que o documento existe
        Assert.Empty(await EventosAsync(api, documento)); // e nada foi registrado como baixado

        // O controle: pela ficha da dona o MESMO documento baixa (sem ele, o 404 de uma rota que não existe passaria).
        Assert.Equal(200, (int)(await ComoRh(api).GetAsync($"{Rota(dona)}/{documento}/download")).StatusCode);
    }

    [Fact]
    public async Task Falha_ao_gravar_o_evento_de_download_nao_devolve_link()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Falha20 Pessoa");
        var documento = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "ENVIADO", null, NovoCaminho(), "cpf.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAuditor>();
            servicos.AddScoped<IAuditor>(sp => new AuditorQueFalhaNoDownload(ActivatorUtilities.CreateInstance<Auditor>(sp)));
        }));

        var resposta = await api.Api.ClienteComo(fabrica, api.Elenco.Rh).GetAsync($"{Rota(pessoa)}/{documento}/download");

        Assert.Equal(500, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("arquivos.exemplo.test", texto); // sem registro, sem link
        Assert.DoesNotContain("falha-simulada-da-auditoria", texto);
        Assert.Empty(await EventosAsync(api, documento));
    }

    // ---------------------------------------------------------------- perfis, contrato e peças

    [Fact]
    public async Task Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas_e_nenhuma_rota_do_gestor_no_contrato_trata_de_documentos()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Permissao21 Pessoa");
        var documento = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "ENVIADO", null, NovoCaminho(), "cpf.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        api.Api.Armazenamento.Limpar();

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var cliente = api.Api.ClienteComo(quem);
            Assert.Equal(403, (int)(await cliente.GetAsync(Rota(pessoa))).StatusCode);
            Assert.Equal(403, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Anexo("CPF", "invasor.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
            Assert.Equal(403, (int)(await cliente.GetAsync($"{Rota(pessoa)}/{documento}/download")).StatusCode);
        }

        var anonimo = api.Api.ClienteAnonimo();
        Assert.Equal(401, (int)(await anonimo.GetAsync(Rota(pessoa))).StatusCode);
        Assert.Equal(401, (int)(await anonimo.PostAsync(Rota(pessoa), FormulariosDeTeste.Pedido("CPF"))).StatusCode);
        Assert.Equal(401, (int)(await anonimo.GetAsync($"{Rota(pessoa)}/{documento}/download")).StatusCode);
        Assert.Empty(api.Api.Armazenamento.Gravados); // o perfil errado nunca chega a gravar
        Assert.Equal(1, await LinhasDaPessoaAsync(api, pessoa));

        // O contrato não tem rota de documentos para o gestor (documento é do RH e do próprio colaborador).
        var rotasDoGestor = CarregadorDeContrato.Contrato()["paths"]!.AsObject().Select(p => p.Key).Where(c => c.StartsWith("/api/gestor", StringComparison.Ordinal)).ToList();
        Assert.NotEmpty(rotasDoGestor);
        Assert.DoesNotContain(rotasDoGestor, c => c.Contains("documento", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Schema22 Pessoa");
        var pendente = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        var enviado = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "ENVIADO", null, NovoCaminho(), "cpf.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        // O status de cada resposta é conferido antes do schema: o 404 de uma rota que não existe também "bate" com o 404 declarado.
        var (lista, anexado, pedido, semModo, semPessoa, link, semArquivo, outroDocumento) = (
            await ComoRh(api).GetAsync(Rota(pessoa)),
            await PostarAsync(api, pessoa, FormulariosDeTeste.Anexo("CPF", "cpf.pdf", ArquivosDeTeste.Pdf())),
            await PostarAsync(api, pessoa, FormulariosDeTeste.Pedido("OUTRO", "descrição")),
            await PostarAsync(api, pessoa, FormulariosDeTeste.Documento("TALVEZ", "CPF")),
            await PostarAsync(api, int.MaxValue, FormulariosDeTeste.Pedido("CPF")),
            await ComoRh(api).GetAsync($"{Rota(pessoa)}/{enviado}/download"),
            await ComoRh(api).GetAsync($"{Rota(pessoa)}/{pendente}/download"),
            await ComoRh(api).GetAsync($"{Rota(pessoa)}/{int.MaxValue}/download"));
        Assert.Equal([200, 201, 201, 400, 404, 200, 422, 404], new[] { lista, anexado, pedido, semModo, semPessoa, link, semArquivo, outroDocumento }.Select(r => (int)r.StatusCode));

        await lista.AfirmarConformeAoContratoAsync("listarDocumentosDoFuncionario");
        foreach (var resposta in new[] { anexado, pedido, semModo, semPessoa })
        {
            await resposta.AfirmarConformeAoContratoAsync("adicionarDocumentoDoFuncionario");
        }

        foreach (var resposta in new[] { link, semArquivo, outroDocumento })
        {
            await resposta.AfirmarConformeAoContratoAsync("baixarDocumentoDoFuncionario");
        }
    }

    [Fact]
    public async Task FonteDeReferenciasDosDocumentos_marca_como_referenciados_so_os_caminhos_com_linha()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Fonte23 Pessoa");
        var comLinha1 = NovoCaminho();
        var comLinha2 = NovoCaminho();
        var semLinha = NovoCaminho();
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "ENVIADO", null, comLinha1, "a.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "ENVIADO", null, comLinha2, "b.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0)); // pendente: sem caminho, não referencia nada

        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = Assert.Single(escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>(), f => f.Prefixo == "documentos/");
        var referenciados = await fonte.ReferenciadosAsync([comLinha1, comLinha2, semLinha], CancellationToken.None);

        Assert.Equal(new[] { comLinha1, comLinha2 }.Order(), referenciados.Order());
        Assert.Empty(await fonte.ReferenciadosAsync([semLinha], CancellationToken.None));
    }

    /// <summary>O <see cref="IAuditor"/> de verdade, que falha só ao gravar o evento <c>Baixou</c>: a rota de download tem de falhar junto, sem devolver link.</summary>
    private sealed class AuditorQueFalhaNoDownload(IAuditor real) : IAuditor
    {
        public void Registrar(EventoDeAuditoriaNovo evento) => real.Registrar(evento);

        public void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento) => real.RegistrarComo(ator, evento);

        public Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct) =>
            evento.Acao == AuditoriaAcao.Baixou ? throw new InvalidOperationException("falha-simulada-da-auditoria") : real.RegistrarEGravarAsync(evento, ct);

        public Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct) => real.RegistrarEGravarComoAsync(ator, evento, ct);

        public Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct) => real.RegistrarEGravarComLimiteAsync(ator, evento, janela, ct);

        public IDisposable SuspenderRegistroAutomatico() => real.SuspenderRegistroAutomatico();
    }
}
