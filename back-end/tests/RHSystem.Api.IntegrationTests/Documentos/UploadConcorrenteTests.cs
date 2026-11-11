using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>
/// Dois envios ao mesmo documento pendente, ao mesmo tempo. A <c>Situacao</c> é o token de concorrência: os dois passam pela leitura (o documento ainda é <c>Pendente</c>) e os dois gravam o blob deles; o segundo a salvar não
/// acha a linha pendente (<c>DbUpdateConcurrencyException</c>), vira 422 <c>DOCUMENTO_NAO_PEDIDO</c> e apaga o blob dele. No fim há UM arquivo no armazenamento e a linha aponta para ele. O portão é um
/// <see cref="TaskCompletionSource"/> no gancho <c>AoGravar</c> do armazenamento (com limite de tempo: um teste que espera para sempre é um teste quebrado).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class UploadConcorrenteTests(MySqlFixture mysql)
{
    [Fact]
    public async Task Dois_envios_simultaneos_para_o_mesmo_pendente_dao_um_200_e_um_422_e_sobra_um_unico_arquivo_o_do_perdedor_e_apagado()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(UploadConcorrenteTests));
        var eu = await PessoasDeTeste.ComLoginAsync(api, "Concorrente1 pessoa");
        var pendente = await ApoioDeTeste.CriarDocumentoAsync(api.Conexao, eu.FuncionarioId, "CPF", "PENDENTE", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));
        api.Api.Armazenamento.Limpar();
        var chegadas = 0;
        var portao = new TaskCompletionSource(TaskCreationOptions.RunContinuationsAsynchronously);
        api.Api.Armazenamento.AoGravar = async _ =>
        {
            if (Interlocked.Increment(ref chegadas) == 2)
            {
                portao.SetResult(); // as duas já leram o documento (pendente) e já estão gravando: agora as duas seguem para salvar
            }

            await Task.WhenAny(portao.Task, Task.Delay(TimeSpan.FromSeconds(15)));
        };
        var cliente = await PessoasDeTeste.ClienteAsync(api, eu);

        try
        {
            var respostas = await Task.WhenAll(
                cliente.PostAsync($"/api/eu/documentos/{pendente}/arquivo", FormulariosDeTeste.Envio("envio-um.pdf", ArquivosDeTeste.Pdf(1111))),
                cliente.PostAsync($"/api/eu/documentos/{pendente}/arquivo", FormulariosDeTeste.Envio("envio-dois.pdf", ArquivosDeTeste.Pdf(2222))));

            Assert.True(portao.Task.IsCompleted, "as duas gravações deviam ter chegado ao armazenamento juntas");
            Assert.Equal([200, 422], respostas.Select(r => (int)r.StatusCode).Order());
            var perdedora = respostas.Single(r => (int)r.StatusCode == 422);
            Assert.Equal((422, "DOCUMENTO_NAO_PEDIDO", "Este documento não está aguardando envio."), await ProblemaAsync(perdedora));
            var vencedora = await CorpoAsync(respostas.Single(r => (int)r.StatusCode == 200));

            var caminhoNaLinha = (string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"))!;
            Assert.Equal(2, api.Api.Armazenamento.Gravados.Count); // os dois chegaram a gravar
            var caminhoDoPerdedor = Assert.Single(api.Api.Armazenamento.Gravados, c => c != caminhoNaLinha);
            Assert.Equal([caminhoDoPerdedor], api.Api.Armazenamento.Apagados); // o do perdedor foi apagado
            Assert.Equal(1, api.Api.Armazenamento.Quantidade); // sobrou um só arquivo
            Assert.True(api.Api.Armazenamento.Existe(caminhoNaLinha)); // e é o que a linha aponta
            Assert.Equal(vencedora.GetProperty("nomeArquivoOriginal").GetString(), await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_ARQUIVO_ORIGINAL FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}")); // o nome é do mesmo envio
            Assert.Equal("ENVIADO", await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"));
        }
        finally
        {
            api.Api.Armazenamento.Limpar();
        }
    }
}
