using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>
/// "Meus documentos" (<c>/api/eu/documentos</c>), para qualquer pessoa logada (Funcionario, Gestor e RH; sem 403), no banco REAL com o relógio fixo em 2026-10-07T12:42Z. O id da pessoa é SEMPRE o do token, nunca um
/// parâmetro: documento de outra pessoa dá o mesmo 404 do inexistente. Enviar só vale para documento <c>Pendente</c> e segue a receita do arquivo da Task 7 (blob fora da transação, linha e evento numa transação curta, blob apagado
/// se falhar). Baixar o próprio documento não grava evento (decisão 5).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MeusDocumentosTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(MeusDocumentosTests));

    private const string Lista = "/api/eu/documentos";

    private static string Envio(int documentoId) => $"/api/eu/documentos/{documentoId}/arquivo";

    private static string Download(int documentoId) => $"/api/eu/documentos/{documentoId}/download";

    private static DateTime Pedido => new(2026, 10, 1, 9, 0, 0); // 12:00Z

    private static Task<int> PendenteAsync(ApiDeAcesso api, PessoaDoTeste pessoa, string tipo = "COMPROVANTE_DE_RESIDENCIA") =>
        ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa.FuncionarioId, tipo, "PENDENTE", "Comprovante atualizado", pedidoEm: Pedido);

    private static Task<int> EnviadoAsync(ApiDeAcesso api, PessoaDoTeste pessoa, string tipo = "IDENTIDADE") =>
        ApoioDeTeste.CriarDocumentoAsync(api.Conexao, pessoa.FuncionarioId, tipo, "ENVIADO", null, CaminhosDeArquivo.Novo("documentos", ".pdf"), "rg.pdf", enviadoEm: new DateTime(2022, 3, 14, 10, 20, 0));

    private static async Task<string?> SituacaoNoBancoAsync(ApiDeAcesso api, int documentoId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {documentoId}") as string;

    private static async Task<string?> CaminhoNoBancoAsync(ApiDeAcesso api, int documentoId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {documentoId}") as string;

    private static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int documentoId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS_DOCUMENTOS", documentoId);

    // ---------------------------------------------------------------- listar

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task Lista_devolve_so_os_documentos_da_pessoa_logada_para_Funcionario_Gestor_e_RH(Perfil perfil)
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, $"Lista1 {perfil}", perfil);
        var outra = await PessoasDeTeste.ComLoginAsync(api, $"Lista1 outra {perfil}");
        var antigo = await EnviadoAsync(api, eu);
        var pendente = await PendenteAsync(api, eu);
        await EnviadoAsync(api, outra);
        await PendenteAsync(api, outra);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_CADASTRO = '2026-01-10 10:00:00' WHERE ID_FUNCIONARIO_DOCUMENTO = {antigo}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS_DOCUMENTOS SET DATA_CADASTRO = '2026-10-01 09:00:00' WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}");

        var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).GetAsync(Lista);

        var itens = await ItensAsync(resposta);
        Assert.Equal([pendente, antigo], itens.Select(i => i.GetProperty("id").GetInt32())); // só os dela, mais recentes primeiro
        Assert.Equal(["id", "tipo", "descricao", "dataValidade", "situacao", "nomeArquivoOriginal", "pedidoEmUtc", "enviadoEmUtc"], itens[0].EnumerateObject().Select(p => p.Name)); // o mesmo schema da ficha do RH
        Assert.Equal(("PENDENTE", "2026-10-01T12:00:00Z"), (itens[0].GetProperty("situacao").GetString(), itens[0].GetProperty("pedidoEmUtc").GetString()));
    }

    // ---------------------------------------------------------------- enviar

    [Fact]
    public async Task Enviar_arquivo_de_documento_pendente_devolve_200_ENVIADO_com_nome_enviadoEmUtc_do_relogio_pedidoEmUtc_preservado_e_arquivo_gravado()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Enviar2 pessoa");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();
        var bytes = ArquivosDeTeste.Pdf(3000);

        var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("comprovante de residencia.pdf", bytes));

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((pendente, "COMPROVANTE_DE_RESIDENCIA", "ENVIADO", "comprovante de residencia.pdf", "2026-10-07T12:42:00Z", "2026-10-01T12:00:00Z"), (corpo.GetProperty("id").GetInt32(), corpo.GetProperty("tipo").GetString(), corpo.GetProperty("situacao").GetString(), corpo.GetProperty("nomeArquivoOriginal").GetString(), corpo.GetProperty("enviadoEmUtc").GetString(), corpo.GetProperty("pedidoEmUtc").GetString()));
        Assert.Equal("Comprovante atualizado", corpo.GetProperty("descricao").GetString()); // o resto do pedido fica
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Matches(@"^documentos/[0-9a-f]{32}\.pdf$", caminho);
        Assert.Equal(bytes, api.Api.Armazenamento.Conteudo(caminho));
        Assert.Equal(("ENVIADO", caminho), (await SituacaoNoBancoAsync(api, pendente), await CaminhoNoBancoAsync(api, pendente)));
    }

    [Fact]
    public async Task Enviar_para_documento_ja_enviado_devolve_422_DOCUMENTO_NAO_PEDIDO_com_o_texto_do_contrato_e_nada_e_gravado_no_armazenamento()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Enviado3 pessoa");
        var enviado = await EnviadoAsync(api, eu);
        var caminhoAntes = await CaminhoNoBancoAsync(api, enviado);
        api.Api.Armazenamento.Limpar();

        var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(enviado), FormulariosDeTeste.Envio("outro.pdf", ArquivosDeTeste.Pdf()));

        Assert.Equal((422, "DOCUMENTO_NAO_PEDIDO", "Este documento não está aguardando envio."), await ProblemaAsync(resposta));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal(caminhoAntes, await CaminhoNoBancoAsync(api, enviado)); // o arquivo que já estava lá não foi trocado
    }

    [Fact]
    public async Task Enviar_ou_baixar_documento_de_outra_pessoa_devolve_404_igual_ao_do_inexistente()
    {
        var api = await ApiAsync();
        var dona = await PessoasDeTeste.ComLoginAsync(api, "Dona4 pessoa");
        var intrusa = await PessoasDeTeste.ComLoginAsync(api, "Intrusa4 pessoa");
        var pendenteDaDona = await PendenteAsync(api, dona);
        var enviadoDaDona = await EnviadoAsync(api, dona);
        var cliente = await PessoasDeTeste.ClienteAsync(api, intrusa);
        api.Api.Armazenamento.Limpar();

        var envioErrado = await cliente.PostAsync(Envio(pendenteDaDona), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()));
        var envioInexistente = await cliente.PostAsync(Envio(int.MaxValue), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()));
        var downloadErrado = await cliente.GetAsync(Download(enviadoDaDona));
        var downloadInexistente = await cliente.GetAsync(Download(int.MaxValue));

        foreach (var (errado, inexistente) in new[] { (envioErrado, envioInexistente), (downloadErrado, downloadInexistente) })
        {
            Assert.Equal(404, (int)errado.StatusCode);
            var (a, b) = (await CorpoAsync(errado), await CorpoAsync(inexistente));
            Assert.Equal((404, b.GetProperty("title").GetString(), b.GetProperty("detail").GetString()), (404, a.GetProperty("title").GetString(), a.GetProperty("detail").GetString())); // nada diz que o documento existe
        }

        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal("PENDENTE", await SituacaoNoBancoAsync(api, pendenteDaDona));

        // O controle: a dona envia o MESMO documento e dá 200 (sem ele, o 404 de uma rota que não existe passaria).
        Assert.Equal(200, (int)(await (await PessoasDeTeste.ClienteAsync(api, dona)).PostAsync(Envio(pendenteDaDona), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
    }

    [Theory]
    [InlineData("sem-arquivo", "O arquivo é obrigatório.")]
    [InlineData("gif", "Envie um arquivo PDF, JPG ou PNG.")]
    [InlineData("exe-chamado-pdf", "Envie um arquivo PDF, JPG ou PNG.")]
    [InlineData("10-MiB-mais-1", "O arquivo pode ter até 10 MB.")]
    [InlineData("dois-arquivos", "Envie um arquivo só.")]
    [InlineData("zero-bytes", "O arquivo está vazio.")]
    public async Task Erros_de_arquivo_voltam_400_em_arquivo_dizendo_o_que_fazer(string cenario, string mensagem)
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, $"Erros5 {cenario}");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();
        var pdf = ArquivosDeTeste.Pdf();
        var formulario = cenario switch
        {
            "sem-arquivo" => new MultipartFormDataContent { { new StringContent("x"), "outro" } },
            "gif" => FormulariosDeTeste.Envio("imagem.gif", ArquivosDeTeste.Gif()),
            "exe-chamado-pdf" => FormulariosDeTeste.Envio("mentira.pdf", ArquivosDeTeste.Exe(), "application/pdf"),
            "10-MiB-mais-1" => FormulariosDeTeste.Envio("enorme.pdf", ArquivosDeTeste.Pdf((10 * 1024 * 1024) + 1)),
            "dois-arquivos" => FormulariosDeTeste.Documento(null, null, null, null, new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf), new FormulariosDeTeste.Parte("arquivo", "b.pdf", pdf)),
            _ => FormulariosDeTeste.Envio("vazio.pdf", []),
        };

        var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), formulario);

        var erros = await ErrosAsync(resposta);
        Assert.Equal([mensagem], erros.GetProperty("arquivo").EnumerateArray().Select(m => m.GetString()));
        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Equal("PENDENTE", await SituacaoNoBancoAsync(api, pendente)); // o pedido continua de pé
    }

    [Fact]
    public async Task O_nome_guardado_perde_a_pasta_que_o_navegador_ou_o_atacante_mandou()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Nome5c pessoa");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();

        var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("../../pasta/secreta/meu rg.pdf", ArquivosDeTeste.Pdf()));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("meu rg.pdf", (await CorpoAsync(resposta)).GetProperty("nomeArquivoOriginal").GetString());
        Assert.Equal("meu rg.pdf", await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"));
    }

    [Fact]
    public async Task O_id_que_vale_e_o_do_funcionario_do_token_e_nao_o_do_usuario_nas_tres_rotas()
    {
        var api = await ApiAsync();
        await api.NovaPessoaAsync("Ids5d sem login um"); // pessoas sem login empurram o id de funcionário à frente do id de usuário
        await api.NovaPessoaAsync("Ids5d sem login dois");
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Ids5d pessoa");
        Assert.NotEqual(eu.FuncionarioId, eu.UsuarioId); // sem isto os dois ids coincidem e o teste não prova nada
        var pendente = await PendenteAsync(api, eu);
        var enviado = await EnviadoAsync(api, eu);
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);
        api.Api.Armazenamento.Limpar();

        var lista = await ItensAsync(await cliente.GetAsync(Lista));
        var link = await cliente.GetAsync(Download(enviado));
        var envio = await cliente.PostAsync(Envio(pendente), FormulariosDeTeste.Envio("ids.pdf", ArquivosDeTeste.Pdf()));

        Assert.Equal(new[] { pendente, enviado }.Order(), lista.Select(i => i.GetProperty("id").GetInt32()).Order());
        Assert.Equal(200, (int)link.StatusCode);
        Assert.Equal(200, (int)envio.StatusCode);
    }

    [Fact]
    public async Task O_400_do_arquivo_vem_antes_do_422_de_documento_ja_enviado_e_o_404_antes_dos_dois()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Ordem5b pessoa");
        var enviado = await EnviadoAsync(api, eu);
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);

        var arquivoRuim = await cliente.PostAsync(Envio(enviado), FormulariosDeTeste.Envio("mentira.pdf", ArquivosDeTeste.Exe()));
        var inexistenteComArquivoRuim = await cliente.PostAsync(Envio(int.MaxValue), FormulariosDeTeste.Envio("mentira.pdf", ArquivosDeTeste.Exe()));

        Assert.Equal(400, (int)arquivoRuim.StatusCode); // a ordem do contrato: 404, 400, 422
        Assert.Equal(404, (int)inexistenteComArquivoRuim.StatusCode);
    }

    [Fact]
    public async Task Falha_do_armazenamento_ou_do_banco_nao_muda_a_situacao_do_documento_e_nao_deixa_blob()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Falha6 pessoa");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();

        // O armazenamento falha: 500, o documento segue pendente e a limpeza do caminho tentado foi pedida.
        api.Api.Armazenamento.FalharNaGravacao = new IOException("armazenamento-fora-do-ar-6666");
        try
        {
            var semArmazenamento = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()));
            Assert.Equal(500, (int)semArmazenamento.StatusCode);
            Assert.DoesNotContain("armazenamento-fora-do-ar-6666", await semArmazenamento.Content.ReadAsStringAsync());
            Assert.Single(api.Api.Armazenamento.ExclusoesPedidas);
        }
        finally
        {
            api.Api.Armazenamento.Limpar();
        }

        Assert.Equal(("PENDENTE", (string?)null), (await SituacaoNoBancoAsync(api, pendente), await CaminhoNoBancoAsync(api, pendente)));

        // O banco falha DEPOIS de gravar a linha e o evento: a transação os desfaz e o blob é apagado.
        var fabrica = api.Api.ComFalhaNoBancoAoSalvarDocumento();
        var semBanco = await (await PessoasDeTeste.ClienteDaFabricaAsync(api, fabrica, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()));

        Assert.Equal(500, (int)semBanco.StatusCode);
        Assert.DoesNotContain(DocumentosRepositorioComFalha.TextoDaFalha, await semBanco.Content.ReadAsStringAsync());
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal([caminho], api.Api.Armazenamento.Apagados);
        Assert.Equal(0, api.Api.Armazenamento.Quantidade); // o armazenamento termina vazio
        Assert.Equal(("PENDENTE", (string?)null), (await SituacaoNoBancoAsync(api, pendente), await CaminhoNoBancoAsync(api, pendente)));
        Assert.Empty(await EventosAsync(api, pendente)); // nem o evento Alterou
    }

    [Fact]
    public async Task Nenhuma_transacao_fica_aberta_durante_o_envio_ao_armazenamento()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Transacao7 pessoa");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();
        string? situacaoNoInstanteDaGravacao = null;
        api.Api.Armazenamento.AoGravar = async _ => situacaoNoInstanteDaGravacao = await SituacaoNoBancoAsync(api, pendente);

        try
        {
            var resposta = await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()));

            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.Single(api.Api.Armazenamento.Gravados);
            Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta);
            Assert.Equal("PENDENTE", situacaoNoInstanteDaGravacao); // a linha só muda DEPOIS do blob
        }
        finally
        {
            api.Api.Armazenamento.Limpar();
        }
    }

    // ---------------------------------------------------------------- baixar

    [Fact]
    public async Task Download_do_meu_documento_devolve_o_link_e_nao_grava_evento_e_pendente_devolve_422_DOCUMENTO_SEM_ARQUIVO()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Baixar8 pessoa");
        var caminho = CaminhosDeArquivo.Novo("documentos", ".pdf");
        var enviado = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, eu.FuncionarioId, "CPF", "ENVIADO", null, caminho, "Meu CPF.pdf", enviadoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        var pendente = await PendenteAsync(api, eu);
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);

        var link = await cliente.GetAsync(Download(enviado));
        var semArquivo = await cliente.GetAsync(Download(pendente));

        Assert.Equal(200, (int)link.StatusCode);
        var corpo = await CorpoAsync(link);
        Assert.Equal(($"https://arquivos.exemplo.test/{caminho}?nome=Meu%20CPF.pdf", "2026-10-07T12:47:00Z"), (corpo.GetProperty("url").GetString(), corpo.GetProperty("expiraEmUtc").GetString()));
        Assert.Empty(await EventosAsync(api, enviado)); // o download do próprio documento não entra no log (decisão 5)
        Assert.Equal((422, "DOCUMENTO_SEM_ARQUIVO", "Este documento ainda não foi enviado."), await ProblemaAsync(semArquivo));
    }

    // ---------------------------------------------------------------- auditoria, perfis e contrato

    [Fact]
    public async Task Envio_grava_Alterou_na_area_Documentos_com_Situacao_de_Pendente_para_Enviado_e_Arquivo_sem_valor()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Auditoria9 pessoa");
        var pendente = await PendenteAsync(api, eu);
        api.Api.Armazenamento.Limpar();

        Assert.Equal(200, (int)(await (await PessoasDeTeste.ClienteAsync(api, eu)).PostAsync(Envio(pendente), FormulariosDeTeste.Envio("segredo-da-maria.pdf", ArquivosDeTeste.Pdf()))).StatusCode);

        var evento = Assert.Single(await EventosAsync(api, pendente));
        Assert.Equal((AuditoriaArea.Documentos, AuditoriaAcao.Alterou, eu.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId)); // quem agiu foi a própria pessoa
        Assert.Equal($"Documento Comprovante de residência de {eu.Nome}", evento.RegistroAfetado); // o log diz de quem é o documento
        var situacao = Assert.Single(evento.Mudancas, m => m.Campo == "Situação");
        Assert.Equal(("pendente", "enviado"), (situacao.ValorAntes?.ToLowerInvariant(), situacao.ValorDepois?.ToLowerInvariant())); // o texto do enum, sem diferenciar maiúsculas (o formato é do interceptor)
        var arquivo = Assert.Single(evento.Mudancas, m => m.Campo == "Arquivo");
        Assert.Equal((true, (string?)null, (string?)null), (arquivo.ValorOculto, arquivo.ValorAntes, arquivo.ValorDepois));
        var tudo = await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_EVENTOS") + await Sql.TextoDaTabelaAsync(api.Conexao, "AUDITORIA_MUDANCAS");
        Assert.DoesNotContain("segredo-da-maria", tudo);
        Assert.DoesNotContain(Assert.Single(api.Api.Armazenamento.Gravados)[("documentos/".Length)..^4], tudo);
    }

    [Fact]
    public async Task Anonimo_recebe_401_nas_tres_rotas_e_as_rotas_nao_listam_403()
    {
        var api = await ApiAsync();
        var anonimo = api.Api.ClienteAnonimo();

        Assert.Equal(401, (int)(await anonimo.GetAsync(Lista)).StatusCode);
        Assert.Equal(401, (int)(await anonimo.PostAsync(Envio(1), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf()))).StatusCode);
        Assert.Equal(401, (int)(await anonimo.GetAsync(Download(1))).StatusCode);

        // "Qualquer pessoa logada": nem o contrato nem a API declaram 403 nestas rotas (o gerador também não: nenhum perfil é recusado).
        var gerado = await CarregadorDeContrato.GeradoAsync(api.Api.CreateClient());
        foreach (var documento in new[] { CarregadorDeContrato.Contrato(), gerado })
        {
            foreach (var rota in new[] { "/api/eu/documentos", "/api/eu/documentos/{documentoId}/arquivo", "/api/eu/documentos/{documentoId}/download" })
            {
                var operacoes = documento["paths"]![rota]!.AsObject();
                Assert.NotEmpty(operacoes);
                foreach (var operacao in operacoes)
                {
                    Assert.False(operacao.Value!["responses"]!.AsObject().ContainsKey("403"), $"{operacao.Key} {rota} não devia declarar 403");
                }
            }
        }
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Schema10 pessoa");
        var pendente = await PendenteAsync(api, eu);
        var pendente2 = await PendenteAsync(api, eu, "CPF");
        var enviado = await EnviadoAsync(api, eu);
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);

        // O status de cada resposta é conferido antes do schema: o 404 de uma rota que não existe também "bate" com o 404 declarado.
        var (lista, enviou, semArquivo, jaEnviado, inexistente, link, semLink, linkInexistente) = (
            await cliente.GetAsync(Lista),
            await cliente.PostAsync(Envio(pendente), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf())),
            await cliente.PostAsync(Envio(pendente2), new MultipartFormDataContent { { new StringContent("x"), "outro" } }),
            await cliente.PostAsync(Envio(enviado), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf())),
            await cliente.PostAsync(Envio(int.MaxValue), FormulariosDeTeste.Envio("a.pdf", ArquivosDeTeste.Pdf())),
            await cliente.GetAsync(Download(enviado)),
            await cliente.GetAsync(Download(pendente2)),
            await cliente.GetAsync(Download(int.MaxValue)));
        Assert.Equal([200, 200, 400, 422, 404, 200, 422, 404], new[] { lista, enviou, semArquivo, jaEnviado, inexistente, link, semLink, linkInexistente }.Select(r => (int)r.StatusCode));

        await lista.AfirmarConformeAoContratoAsync("listarMeusDocumentos");
        foreach (var resposta in new[] { enviou, semArquivo, jaEnviado, inexistente })
        {
            await resposta.AfirmarConformeAoContratoAsync("enviarMeuDocumento");
        }

        foreach (var resposta in new[] { link, semLink, linkInexistente })
        {
            await resposta.AfirmarConformeAoContratoAsync("baixarMeuDocumento");
        }
    }
}
