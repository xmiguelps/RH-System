using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>
/// A receita do envio de arquivo (decisão 11 do plano da 1c): (1) valida tudo, inclusive o que se lê do banco; (2) grava no armazenamento, SEM transação aberta; (3) uma transação curta que insere a linha e o evento;
/// (4) se o passo 2 ou o 3 falhar, apaga o blob (e o erro que sai é o original). No banco REAL, com o armazenamento em memória que conta as gravações com transação aberta. O armazenamento é compartilhado pela API da classe:
/// todo teste o limpa antes e desfaz o que configurou.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class UploadDeDocumentoTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(UploadDeDocumentoTests));

    private static string Rota(int funcionarioId) => $"/api/rh/funcionarios/{funcionarioId}/documentos";

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario);

    private static Task<HttpResponseMessage> AnexarAsync(ApiDeAcesso api, int funcionarioId, string nome = "arquivo.pdf", WebApplicationFactory<Program>? fabrica = null) =>
        (fabrica is null ? api.Api.ClienteComo(api.Elenco.Rh) : api.Api.ClienteComo(fabrica, api.Elenco.Rh)).PostAsync(Rota(funcionarioId), FormulariosDeTeste.Anexo("CPF", nome, ArquivosDeTeste.Pdf()));

    private static async Task<long> LinhasDaPessoaAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO = {funcionarioId}");

    private static async Task<int> EventosDoRhAsync(ApiDeAcesso api) => (await AuditoriaDeTeste.EventosDoAtorAsync(api.Conexao, api.Elenco.Rh.FuncionarioId)).Count;

    // ---------------------------------------------------------------- a ordem

    [Fact]
    public async Task O_arquivo_so_e_gravado_depois_de_as_validacoes_passarem()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Ordem1 Pessoa");
        api.Api.Armazenamento.Limpar();
        var cliente = api.Api.ClienteComo(api.Elenco.Rh);
        var pdf = ArquivosDeTeste.Pdf();
        var arquivo = new FormulariosDeTeste.Parte("arquivo", "a.pdf", pdf);

        // Pessoa inexistente: o 404 vem antes (a leitura do banco é do passo 1) e nada é gravado.
        Assert.Equal(404, (int)(await cliente.PostAsync(Rota(int.MaxValue), FormulariosDeTeste.Anexo("CPF", "a.pdf", pdf))).StatusCode);
        // Cada 400, COM um arquivo bom no formulário: o arquivo não chega ao armazenamento.
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("TALVEZ", "CPF", null, null, arquivo))).StatusCode);
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("ANEXAR_AGORA", "INEXISTENTE", null, null, arquivo))).StatusCode);
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", new string('d', 256), null, arquivo))).StatusCode);
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, "2027-13-01", arquivo))).StatusCode);
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, arquivo, arquivo with { NomeDoArquivo = "b.pdf" }))).StatusCode); // dois arquivos
        Assert.Equal(400, (int)(await cliente.PostAsync(Rota(pessoa), FormulariosDeTeste.Documento("ANEXAR_AGORA", "CPF", null, null, new FormulariosDeTeste.Parte("arquivo", "mentira.pdf", ArquivosDeTeste.Exe())))).StatusCode);

        Assert.Empty(api.Api.Armazenamento.Gravados);
        Assert.Empty(api.Api.Armazenamento.ExclusoesPedidas); // nem houve o que desfazer
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
    }

    [Fact]
    public async Task Nenhuma_transacao_fica_aberta_durante_o_envio_ao_armazenamento_e_o_blob_vem_antes_da_linha()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Transacao2 Pessoa");
        api.Api.Armazenamento.Limpar();
        long? linhasNoInstanteDaGravacao = null;
        api.Api.Armazenamento.AoGravar = async _ => linhasNoInstanteDaGravacao = await LinhasDaPessoaAsync(api, pessoa);

        try
        {
            var resposta = await AnexarAsync(api, pessoa);

            Assert.Equal(201, (int)resposta.StatusCode);
            Assert.Single(api.Api.Armazenamento.Gravados); // o espião foi exercitado
            Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta); // e nenhuma gravação rodou com transação aberta
            Assert.Equal(0L, linhasNoInstanteDaGravacao); // a linha só nasce DEPOIS do blob
            Assert.Equal(1, await LinhasDaPessoaAsync(api, pessoa));
        }
        finally
        {
            api.Api.Armazenamento.Limpar();
        }
    }

    [Fact]
    public async Task O_espiao_de_transacao_enxerga_uma_gravacao_feita_dentro_de_ExecutarEmTransacaoAsync()
    {
        // O controle do teste acima: sem ele, "0 gravações com transação aberta" poderia ser só um espião que nunca vê nada.
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();

        await api.NaTransacaoAsync(async sp =>
        {
            var armazenamento = sp.GetRequiredService<IArmazenamentoDeArquivos>();
            await armazenamento.GravarAsync(CaminhosDeArquivo.Novo("documentos", ".pdf"), new MemoryStream(ArquivosDeTeste.Pdf()), "application/pdf", CancellationToken.None);
            return 0;
        });

        Assert.Equal(1, api.Api.Armazenamento.GravacoesComTransacaoAberta);
        api.Api.Armazenamento.Limpar();
    }

    // ---------------------------------------------------------------- as falhas

    [Fact]
    public async Task Falha_do_banco_ao_inserir_apaga_o_blob_e_nao_deixa_linha_nem_evento()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "FalhaBanco3 Pessoa");
        api.Api.Armazenamento.Limpar();
        var eventosAntes = await EventosDoRhAsync(api);
        var fabrica = api.Api.ComFalhaNoBancoAoSalvarDocumento();

        var resposta = await AnexarAsync(api, pessoa, fabrica: fabrica);

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain(DocumentosRepositorioComFalha.TextoDaFalha, await resposta.Content.ReadAsStringAsync());
        var caminho = Assert.Single(api.Api.Armazenamento.Gravados);
        Assert.Equal([caminho], api.Api.Armazenamento.Apagados); // o blob gravado foi apagado
        Assert.Equal(0, api.Api.Armazenamento.Quantidade); // o armazenamento termina vazio
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa)); // a transação desfez a linha
        Assert.Equal(eventosAntes, await EventosDoRhAsync(api)); // e o evento Criou que o interceptor escreveu na mesma transação
        Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta);
    }

    [Fact]
    public async Task Falha_do_banco_ao_pedir_para_enviar_nao_deixa_linha_e_nao_tenta_apagar_nada()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "FalhaPedido4 Pessoa");
        api.Api.Armazenamento.Limpar();
        var fabrica = api.Api.ComFalhaNoBancoAoSalvarDocumento();

        var resposta = await api.Api.ClienteComo(fabrica, api.Elenco.Rh).PostAsync(Rota(pessoa), FormulariosDeTeste.Pedido("CPF"));

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
        Assert.Empty(api.Api.Armazenamento.ExclusoesPedidas); // pedir não grava blob: não há o que apagar
    }

    [Fact]
    public async Task Falha_do_armazenamento_nao_deixa_linha_nem_evento_e_tenta_apagar_o_caminho()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "FalhaArmazenamento5 Pessoa");
        api.Api.Armazenamento.Limpar();
        var eventosAntes = await EventosDoRhAsync(api);
        api.Api.Armazenamento.FalharNaGravacao = new IOException("armazenamento-fora-do-ar-5555");

        try
        {
            var resposta = await AnexarAsync(api, pessoa);

            Assert.Equal(500, (int)resposta.StatusCode);
            Assert.DoesNotContain("armazenamento-fora-do-ar-5555", await resposta.Content.ReadAsStringAsync());
            Assert.Equal(0, api.Api.Armazenamento.GravacoesComTransacaoAberta); // a gravação foi tentada fora de qualquer transação
            Assert.Empty(api.Api.Armazenamento.Gravados);
            var tentado = Assert.Single(api.Api.Armazenamento.ExclusoesPedidas); // o caso de uso apagou (idempotente) o caminho que tentou
            Assert.Matches(@"^documentos/[0-9a-f]{32}\.pdf$", tentado);
            Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa));
            Assert.Equal(eventosAntes, await EventosDoRhAsync(api));
        }
        finally
        {
            api.Api.Armazenamento.Limpar();
        }
    }

    [Fact]
    public async Task Falha_ao_apagar_depois_da_falha_do_banco_devolve_o_erro_original_e_deixa_a_sobra_para_a_tarefa()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Sobra6 Pessoa");
        api.Api.Armazenamento.Limpar();
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        api.Api.Armazenamento.FalharNaExclusao = new IOException("exclusao-recusada-9999");
        var logs = new ColetorDeLogs();
        var fabrica = api.Api.ComFalhaNoBancoAoSalvarDocumento().WithWebHostBuilder(construtor => construtor.ConfigureLogging(l => l.AddProvider(logs)));

        try
        {
            var resposta = await AnexarAsync(api, pessoa, fabrica: fabrica);

            Assert.Equal(500, (int)resposta.StatusCode);
            var texto = string.Join('\n', logs.Mensagens);
            Assert.Contains(DocumentosRepositorioComFalha.TextoDaFalha, texto); // o erro que saiu (e foi logado) é o ORIGINAL, o do banco
            Assert.DoesNotContain("exclusao-recusada-9999", texto); // o da limpeza não o esconde nem aparece com a mensagem
            var sobra = Assert.Single(api.Api.Armazenamento.ExclusoesPedidas);
            Assert.True(api.Api.Armazenamento.Existe(sobra)); // a sobra ficou
            Assert.Equal(0, await LinhasDaPessoaAsync(api, pessoa)); // sem linha

            // A tarefa de limpeza a apaga, mas só depois do mínimo de 24 horas.
            api.Api.Armazenamento.FalharNaExclusao = null;
            await ExecutarLimpezaAsync(api);
            Assert.True(api.Api.Armazenamento.Existe(sobra)); // ainda dentro do mínimo: protege o envio que está em andamento
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(25));
            await ExecutarLimpezaAsync(api);
            Assert.False(api.Api.Armazenamento.Existe(sobra));
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
            api.Api.Armazenamento.Limpar();
        }
    }

    private static async Task ExecutarLimpezaAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var limpeza = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == "LIMPAR_ARQUIVOS_ORFAOS");
        await limpeza.ExecutarAsync(new TarefaParaExecutar(0, "LIMPAR_ARQUIVOS_ORFAOS:dia=2026-10-07", null, new DateOnly(2026, 10, 7), null, 1), CancellationToken.None);
    }
}
