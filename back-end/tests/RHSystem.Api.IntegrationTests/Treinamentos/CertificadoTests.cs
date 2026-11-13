using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using UglyToad.PdfPig;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>baixarMeuCertificado</c> e <c>baixarCertificadoDeParticipante</c> e a emissão do PDF, no banco REAL com o relógio em 2026-10-07T12:42Z. O PDF sai DEPOIS do commit da conclusão (uma página, com o nome com acento, o título, o número e o código versão 4); se o armazenamento estiver fora do ar a conclusão continua valendo e o primeiro download gera o arquivo, UMA vez (dois downloads ao mesmo
/// tempo deixam um caminho e um arquivo). 404 se a pessoa não concluiu, se é de outra pessoa ou se o treinamento não emite; o RH baixando o de outra pessoa grava um evento sem o nome; a limpeza de sobras nunca apaga o PDF de um certificado emitido.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CertificadoTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(CertificadoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, HttpClient Cliente, string Nome)> PessoaAsync(ApiDeAcesso api, string nome, Perfil perfil = Perfil.Funcionario)
    {
        var completo = Titulo(nome);
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, completo, perfil, null, true, new DateOnly(2026, 1, 2));
        return (id, await api.ClienteAgoraAsync(await AdvertenciasDeTeste.ComLoginAsync(api, id)), completo);
    }

    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status, string? inicio = null) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO, DATA_INICIO) VALUES ({treinamento}, {pessoa}, '{status}', {(inicio is null ? "NULL" : $"'{inicio}'")})");

    /// <summary>Um certificado já emitido SEM arquivo: a pessoa concluída direto no banco (o trigger cria a linha) e o código trocado por um v4 (como a conclusão pela API faz).</summary>
    private static async Task<string> ConcluidaSemArquivoAsync(ApiDeAcesso api, int treinamento, int pessoa)
    {
        await LinhaAsync(api, treinamento, pessoa, "CONCLUIDO");
        var codigo = CodigoDoCertificado.Novo();
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS_CERTIFICADOS SET CODIGO_VALIDACAO = '{codigo}', DATA_EMISSAO = '2026-10-07' WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");
        return codigo;
    }

    private static Task<string?> CaminhoDoCertificadoAsync(ApiDeAcesso api, int treinamento, int pessoa) =>
        TextoAsync(api, $"SELECT CAMINHO_ARQUIVO FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento} AND ID_FUNCIONARIO = {pessoa}");

    private static string TextoDoPdf(byte[] bytes)
    {
        using var documento = PdfDocument.Open(bytes);
        return new string([.. string.Join(' ', documento.GetPages().Select(p => p.Text)).Where(c => !char.IsWhiteSpace(c))]);
    }

    private static string Compacto(string texto) => new([.. texto.Where(c => !char.IsWhiteSpace(c))]);

    private static string RotaDaPessoa(int treinamento) => $"/api/eu/treinamentos/{treinamento}/certificado/download";

    private static string RotaDoRh(int treinamento, int pessoa) => $"/api/rh/treinamentos/{treinamento}/participantes/{pessoa}/certificado/download";

    [Fact]
    public async Task Concluir_o_ultimo_modulo_gera_o_pdf_depois_do_commit_com_o_nome_com_acento_o_numero_a_carga_horaria_e_o_codigo_v4()
    {
        var api = await ApiAsync();
        var (eu, cliente, nome) = await PessoaAsync(api, "José Conceição Ávila");
        var titulo = Titulo("ZCer1 Segurança da informação");
        var t = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true);
        var m1 = await ModuloAsync(api, t, 1, "TEXTO", duracao: 60);
        var m2 = await ModuloAsync(api, t, 2, "TEXTO", duracao: 40);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        _ = await cliente.PostAsync($"/api/eu/treinamentos/{t}/modulos/{m1}/concluir", null);

        var conclusao = await cliente.PostAsync($"/api/eu/treinamentos/{t}/modulos/{m2}/concluir", null);
        var certificado = (await CorpoAsync(conclusao)).GetProperty("treinamento").GetProperty("certificado");
        var caminho = await CaminhoDoCertificadoAsync(api, t, eu);

        Assert.Equal(200, (int)conclusao.StatusCode);
        Assert.NotNull(caminho);
        Assert.StartsWith("certificados/", caminho, StringComparison.Ordinal);
        var bytes = api.Api.Armazenamento.Conteudo(caminho!)!;
        var texto = TextoDoPdf(bytes);
        Assert.Contains(Compacto(nome), texto, StringComparison.Ordinal); // o nome com acento
        Assert.Contains(Compacto(titulo), texto, StringComparison.Ordinal);
        Assert.Contains(certificado.GetProperty("numero").GetString()!, texto, StringComparison.Ordinal); // CERT-2026-NNNNN
        Assert.Contains(certificado.GetProperty("codigoValidacao").GetString()!, texto, StringComparison.Ordinal);
        Assert.Contains("cargahoráriade1h40", texto, StringComparison.Ordinal);
        Assert.Contains("07/10/2026", texto, StringComparison.Ordinal);
        using (var documento = PdfDocument.Open(bytes))
        {
            Assert.Equal(1, documento.NumberOfPages);
            Assert.True(documento.GetPage(1).Width > documento.GetPage(1).Height); // paisagem
        }

        Assert.Equal("application/pdf", api.Api.Armazenamento.TipoDeConteudo(caminho!));
    }

    [Fact]
    public async Task O_download_do_proprio_certificado_devolve_o_link_temporario_sem_o_caminho_e_sem_evento()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer2 Ana");
        var t = await SemearAsync(api, Titulo("ZCer2"), "EM_ANDAMENTO", obrigatorio: true);
        var m = await ModuloAsync(api, t, 1);
        await LinhaAsync(api, t, eu, "INSCRITO");
        _ = await cliente.PostAsync($"/api/eu/treinamentos/{t}/modulos/{m}/concluir", null);
        var caminho = (await CaminhoDoCertificadoAsync(api, t, eu))!;
        var eventosAntes = (await EventosAsync(api, t)).Count;

        var resposta = await cliente.GetAsync(RotaDaPessoa(t));
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("baixarMeuCertificado");
        Assert.StartsWith("https://", corpo.GetProperty("url").GetString(), StringComparison.Ordinal);
        Assert.Equal(["expiraEmUtc", "url"], corpo.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal)); // nenhum campo com o caminho do armazenamento
        Assert.Equal(eventosAntes, (await EventosAsync(api, t)).Count); // baixar o próprio certificado não gera evento
    }

    [Fact]
    public async Task A_conclusao_com_o_armazenamento_fora_do_ar_continua_concluida_e_o_primeiro_download_gera_o_arquivo()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer3 Ana");
        var t = await SemearAsync(api, Titulo("ZCer3"), "EM_ANDAMENTO", obrigatorio: true);
        var m = await ModuloAsync(api, t, 1);
        await LinhaAsync(api, t, eu, "EM_ANDAMENTO", "2026-10-05");
        api.Api.Armazenamento.FalharNaGravacao = new IOException("armazenamento fora do ar");
        HttpResponseMessage conclusao;
        try
        {
            conclusao = await cliente.PostAsync($"/api/eu/treinamentos/{t}/modulos/{m}/concluir", null);
        }
        finally
        {
            api.Api.Armazenamento.FalharNaGravacao = null;
        }

        Assert.Equal(200, (int)conclusao.StatusCode);
        Assert.True((await CorpoAsync(conclusao)).GetProperty("treinamentoConcluido").GetBoolean());
        Assert.Equal("CONCLUIDO", await TextoAsync(api, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}"));
        Assert.Null(await CaminhoDoCertificadoAsync(api, t, eu)); // sem PDF ainda

        var download = await cliente.GetAsync(RotaDaPessoa(t));

        Assert.Equal(200, (int)download.StatusCode);
        var caminho = await CaminhoDoCertificadoAsync(api, t, eu);
        Assert.NotNull(caminho);
        Assert.True(api.Api.Armazenamento.Existe(caminho!));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {t} AND ID_FUNCIONARIO = {eu}")); // nunca cria outra linha
    }

    [Fact]
    public async Task Dois_downloads_ao_mesmo_tempo_sem_arquivo_deixam_um_caminho_so_e_um_arquivo()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer4 Ana");
        var t = await SemearAsync(api, Titulo("ZCer4"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, t, 1);
        _ = await ConcluidaSemArquivoAsync(api, t, eu);
        var antes = api.Api.Armazenamento.Gravados.Count;

        var respostas = await Task.WhenAll(cliente.GetAsync(RotaDaPessoa(t)), cliente.GetAsync(RotaDaPessoa(t)), cliente.GetAsync(RotaDaPessoa(t)), cliente.GetAsync(RotaDaPessoa(t)));

        Assert.Equal([200, 200, 200, 200], respostas.Select(r => (int)r.StatusCode));
        var caminho = (await CaminhoDoCertificadoAsync(api, t, eu))!;
        var novos = api.Api.Armazenamento.Gravados.Skip(antes).Where(c => c.StartsWith("certificados/", StringComparison.Ordinal)).ToList();
        Assert.True(api.Api.Armazenamento.Existe(caminho));
        Assert.Equal([caminho], novos.Where(api.Api.Armazenamento.Existe)); // quem perdeu a corrida apagou o próprio arquivo
    }

    [Fact]
    public async Task Sem_certificado_e_404_em_nao_concluiu_nao_emite_outra_pessoa_e_inexistente_e_o_RH_e_403_para_quem_nao_e_RH()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer5 Ana");
        var (outra, clienteDaOutra, _) = await PessoaAsync(api, "ZCer5 Bia");
        var concluido = await SemearAsync(api, Titulo("ZCer5 concluido"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, concluido, 1);
        _ = await ConcluidaSemArquivoAsync(api, concluido, eu);
        var naoConcluiu = await SemearAsync(api, Titulo("ZCer5 nao concluiu"), "EM_ANDAMENTO", obrigatorio: true);
        await LinhaAsync(api, naoConcluiu, eu, "EM_ANDAMENTO", "2026-10-05");
        var naoEmite = await SemearAsync(api, Titulo("ZCer5 nao emite"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        await LinhaAsync(api, naoEmite, eu, "CONCLUIDO");
        var rh = await api.ClienteDoRhAgoraAsync();

        var respostas = new[]
        {
            await clienteDaOutra.GetAsync(RotaDaPessoa(concluido)), await cliente.GetAsync(RotaDaPessoa(naoConcluiu)), await cliente.GetAsync(RotaDaPessoa(naoEmite)), await cliente.GetAsync(RotaDaPessoa(987654)),
            await rh.GetAsync(RotaDoRh(naoConcluiu, eu)), await rh.GetAsync(RotaDoRh(naoEmite, eu)), await rh.GetAsync(RotaDoRh(concluido, outra)), await rh.GetAsync(RotaDoRh(987654, eu)),
            await cliente.GetAsync(RotaDoRh(concluido, eu)), await api.Api.ClienteAnonimo().GetAsync(RotaDaPessoa(concluido)), await api.Api.ClienteAnonimo().GetAsync(RotaDoRh(concluido, eu)),
        };

        Assert.Equal([404, 404, 404, 404, 404, 404, 404, 404, 403, 401, 401], respostas.Select(r => (int)r.StatusCode));
        Assert.Null(await CaminhoDoCertificadoAsync(api, concluido, eu)); // nenhum dos 404 gerou arquivo
        Assert.Null(await CaminhoDoCertificadoAsync(api, naoEmite, eu));
    }

    [Fact]
    public async Task O_RH_baixando_o_certificado_de_outra_pessoa_grava_o_evento_Baixou_sem_o_nome_nem_o_codigo()
    {
        var api = await ApiAsync();
        var (eu, _, nome) = await PessoaAsync(api, "ZCer6 Ana Souza");
        var titulo = Titulo("ZCer6");
        var t = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, t, 1);
        var codigo = await ConcluidaSemArquivoAsync(api, t, eu);
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync(RotaDoRh(t, eu));

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("baixarCertificadoDeParticipante");
        var evento = Assert.Single(await EventosAsync(api, t));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Baixou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain(nome, evento.Descricao, StringComparison.Ordinal);
        Assert.DoesNotContain(codigo, evento.Descricao, StringComparison.Ordinal);
        Assert.NotNull(await CaminhoDoCertificadoAsync(api, t, eu));
    }

    [Fact]
    public async Task A_limpeza_de_orfaos_conhece_o_pdf_do_certificado_e_nao_o_apaga_mas_apaga_a_sobra()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer7 Ana");
        var t = await SemearAsync(api, Titulo("ZCer7"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, t, 1);
        _ = await ConcluidaSemArquivoAsync(api, t, eu);
        _ = await cliente.GetAsync(RotaDaPessoa(t));
        var referenciado = (await CaminhoDoCertificadoAsync(api, t, eu))!;
        var sobra = CaminhosDeArquivo.Novo("certificados", ".pdf");
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var fonte = escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>().Single(f => f.Prefixo == "certificados/");

        var referenciados = await fonte.ReferenciadosAsync([referenciado, sobra], CancellationToken.None);

        Assert.Equal([referenciado], referenciados);
    }

    [Fact]
    public void O_titulo_do_treinamento_vira_o_nome_do_download_sem_diretorio_nem_aspas()
    {
        Assert.Equal("certificado-Segurança da informação.pdf", NomesDeArquivoDoTreinamento.PdfDoCertificado("Segurança da informação"));
        Assert.DoesNotContain("/", NomesDeArquivoDoTreinamento.PdfDoCertificado("../Segredo\"\r\nX"), StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_codigo_que_nao_e_v4_nunca_vai_para_o_pdf_e_o_download_e_404()
    {
        var api = await ApiAsync();
        var (eu, cliente, _) = await PessoaAsync(api, "ZCer8 Ana");
        var t = await SemearAsync(api, Titulo("ZCer8"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await ModuloAsync(api, t, 1);
        await LinhaAsync(api, t, eu, "CONCLUIDO"); // o código é o v1 do trigger e ninguém o trocou

        var resposta = await cliente.GetAsync(RotaDaPessoa(t));

        Assert.Equal(404, (int)resposta.StatusCode);
        Assert.Null(await CaminhoDoCertificadoAsync(api, t, eu));
    }
}
