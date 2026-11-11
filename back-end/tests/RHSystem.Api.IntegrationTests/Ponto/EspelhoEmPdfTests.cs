using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using PdfSharp.Fonts;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Arquivos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Ponto;
using UglyToad.PdfPig;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ponto.PontoDeTeste;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O espelho de ponto em PDF (<c>/api/rh/funcionarios/{id}/ponto/espelho/download</c> e <c>/api/gestor/equipe/{id}/ponto/espelho/download</c>), com o mesmo conteúdo do espelho do mês. A API nunca devolve o arquivo: grava o PDF em <c>espelhos/</c> e devolve um link temporário
/// (como anexo). A fonte que acentua vai embutida (nenhuma do sistema é usada); o gestor só baixa o de quem é da equipe dele. O relógio está em 2026-10-07T12:42Z. O texto do PDF é lido de volta com o PdfPig.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EspelhoEmPdfTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EspelhoEmPdfTests));
        await ConfigurarEmpresaAsync(api.Conexao, "SP", "São Paulo", new TimeOnly(8, 0), 10);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo do PDF {Guid.NewGuid():N}", 44m);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargo} WHERE ID_FUNCIONARIO IN ({api.Elenco.MembroA.FuncionarioId}, {api.Elenco.MembroB.FuncionarioId})");
        return api;
    }

    private static async Task<HttpResponseMessage> ComoRhAsync(ApiDeAcesso api, int funcionarioId, string consulta = "ano=2026&mes=10") =>
        await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{funcionarioId}/ponto/espelho/download" + (consulta.Length == 0 ? string.Empty : $"?{consulta}"));

    private static async Task<HttpResponseMessage> ComoGestorAsync(ApiDeAcesso api, int funcionarioId, string consulta = "ano=2026&mes=10") =>
        await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{funcionarioId}/ponto/espelho/download" + (consulta.Length == 0 ? string.Empty : $"?{consulta}"));

    private static string CaminhoDoLink(JsonElement link) => new Uri(link.GetProperty("url").GetString()!).AbsolutePath.TrimStart('/');

    private static string Compacto(string texto) => new([.. texto.Where(c => !char.IsWhiteSpace(c))]);

    private static string TextoDoPdf(byte[] bytes)
    {
        using var documento = PdfDocument.Open(bytes);
        return Compacto(string.Join(' ', documento.GetPages().Select(p => p.Text)));
    }

    private static int PaginasDoPdf(byte[] bytes)
    {
        using var documento = PdfDocument.Open(bytes);
        return documento.NumberOfPages;
    }

    private static string Utc(DateOnly dia, int hora, int minuto = 0) => $"{dia:yyyy-MM-dd}T{hora:00}:{minuto:00}:00Z";

    // ---------------------------------------------------------------- o RH

    [Fact]
    public async Task O_RH_recebe_o_link_o_PDF_vai_para_espelhos_e_traz_o_nome_o_mes_a_empresa_e_as_batidas_com_acento()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;
        var dia = new DateOnly(2026, 10, 5);
        await BaterEmAsync(api.Conexao, julia.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 15)), (TipoDeMarcacao.Entrada, Utc(dia, 16)), (TipoDeMarcacao.Saida, Utc(dia, 20, 48))); // 528: saldo zero
        var comAtraso = new DateOnly(2026, 10, 6);
        await BaterEmAsync(api.Conexao, julia.FuncionarioId, comAtraso, (TipoDeMarcacao.Entrada, Utc(comAtraso, 11, 15)), (TipoDeMarcacao.Saida, Utc(comAtraso, 15)), (TipoDeMarcacao.Entrada, Utc(comAtraso, 16)), (TipoDeMarcacao.Saida, Utc(comAtraso, 21, 50))); // 225 + 350 = 575: +0:47 e 15 min de atraso

        var resposta = await ComoRhAsync(api, julia.FuncionarioId);
        var link = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("2026-10-07T12:47:00Z", link.GetProperty("expiraEmUtc").GetString()); // o link vale pouco tempo
        var caminho = CaminhoDoLink(link);
        Assert.StartsWith("espelhos/", caminho, StringComparison.Ordinal);
        Assert.EndsWith(".pdf", caminho, StringComparison.Ordinal);
        Assert.Contains("2026-10", Uri.UnescapeDataString(link.GetProperty("url").GetString()!), StringComparison.Ordinal); // o nome do anexo diz o mês
        Assert.Equal("application/pdf", api.Api.Armazenamento.TipoDeConteudo(caminho));
        var bytes = api.Api.Armazenamento.Conteudo(caminho)!;
        Assert.Equal("%PDF", System.Text.Encoding.ASCII.GetString(bytes, 0, 4));
        var texto = TextoDoPdf(bytes);
        foreach (var esperado in new[]
        {
            "Espelhodeponto", "Geradoem07/10/202609:42(horáriodeBrasília)", "Colaborador:JúliaNunes", "Setor:Comercial", "Cargo:CargodoPDF", "Outubrode2026", "SãoPaulo", "05/10Seg", "08:00–12:00", "13:00–17:48", "8:48", "0:00", "+0:47", "15min", "Atraso", "Normal", "03/10Sáb", "04/10Dom", "Folga", "Semregistro",
        })
        {
            Assert.Contains(esperado, texto, StringComparison.Ordinal);
        }
    }

    [Fact]
    public async Task Sem_ano_e_mes_vale_o_mes_atual_e_ano_ou_mes_fora_da_faixa_da_400_sem_gerar_arquivo()
    {
        var api = await ApiAsync();
        api.Api.Armazenamento.Limpar();

        var atual = await ComoRhAsync(api, api.Elenco.MembroA.FuncionarioId, string.Empty);
        var anoRuim = await ComoRhAsync(api, api.Elenco.MembroA.FuncionarioId, "ano=1999&mes=5");
        var antesDo404 = await ComoRhAsync(api, int.MaxValue, "ano=2026&mes=13"); // o mês ruim vem antes do 404
        var antesDo404DoGestor = await ComoGestorAsync(api, api.Elenco.DeOutroSetor.FuncionarioId, "ano=2026&mes=13");
        var mesRuim = await ComoRhAsync(api, api.Elenco.MembroA.FuncionarioId, "ano=2026&mes=13");

        Assert.Equal((200, 400, 400, 400, 400), ((int)atual.StatusCode, (int)anoRuim.StatusCode, (int)mesRuim.StatusCode, (int)antesDo404.StatusCode, (int)antesDo404DoGestor.StatusCode));
        Assert.Contains("Outubrode2026", TextoDoPdf(api.Api.Armazenamento.Conteudo(CaminhoDoLink(await CorpoAsync(atual)))!), StringComparison.Ordinal);
        Assert.Equal(1, api.Api.Armazenamento.Quantidade); // só o do mês atual
    }

    [Fact]
    public async Task Pessoa_que_nao_existe_e_404_e_um_mes_de_31_dias_cabe_em_poucas_paginas_e_o_arquivo_fica_pequeno()
    {
        var api = await ApiAsync();
        var diego = api.Elenco.MembroA;
        for (var dia = new DateOnly(2026, 8, 3); dia.Month == 8; dia = dia.AddDays(1))
        {
            if (dia.DayOfWeek is not (DayOfWeek.Saturday or DayOfWeek.Sunday))
            {
                await BaterEmAsync(api.Conexao, diego.FuncionarioId, dia, (TipoDeMarcacao.Entrada, Utc(dia, 11)), (TipoDeMarcacao.Saida, Utc(dia, 15)), (TipoDeMarcacao.Entrada, Utc(dia, 16)), (TipoDeMarcacao.Saida, Utc(dia, 20, 48)));
            }
        }

        var inexistente = await ComoRhAsync(api, int.MaxValue);
        var cheio = await ComoRhAsync(api, diego.FuncionarioId, "ano=2026&mes=8");

        Assert.Equal(404, (int)inexistente.StatusCode);
        var bytes = api.Api.Armazenamento.Conteudo(CaminhoDoLink(await CorpoAsync(cheio)))!;
        Assert.InRange(PaginasDoPdf(bytes), 1, 3);
        Assert.True(bytes.Length < 150_000, $"O PDF de 31 dias pesa {bytes.Length} bytes: a fonte tem de ir recortada, não inteira por página.");
        Assert.Contains("Agostode2026", TextoDoPdf(bytes), StringComparison.Ordinal);
    }

    // ---------------------------------------------------------------- o gestor

    [Fact]
    public async Task O_gestor_baixa_o_da_equipe_e_o_de_outro_setor_ou_de_quem_nao_existe_e_404()
    {
        var api = await ApiAsync();

        var daEquipe = await ComoGestorAsync(api, api.Elenco.MembroB.FuncionarioId);
        var outroSetor = await ComoGestorAsync(api, api.Elenco.DeOutroSetor.FuncionarioId);
        var inexistente = await ComoGestorAsync(api, int.MaxValue);
        var oProprio = await ComoGestorAsync(api, api.Elenco.Gestor.FuncionarioId);
        var semSetor = await (await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId)).GetAsync($"/api/gestor/equipe/{api.Elenco.MembroB.FuncionarioId}/ponto/espelho/download?ano=2026&mes=10");

        Assert.Equal((200, 404, 404, 404, 404), ((int)daEquipe.StatusCode, (int)outroSetor.StatusCode, (int)inexistente.StatusCode, (int)oProprio.StatusCode, (int)semSetor.StatusCode));
        Assert.Contains("JúliaNunes", TextoDoPdf(api.Api.Armazenamento.Conteudo(CaminhoDoLink(await CorpoAsync(daEquipe)))!), StringComparison.Ordinal);
    }

    [Fact]
    public async Task So_o_RH_usa_a_rota_do_RH_so_o_gestor_a_da_equipe_e_sem_login_e_401()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB.FuncionarioId;

        Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(api.Elenco.Gestor.UsuarioId)).GetAsync($"/api/rh/funcionarios/{julia}/ponto/espelho/download?ano=2026&mes=10")).StatusCode);
        Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync($"/api/rh/funcionarios/{julia}/ponto/espelho/download?ano=2026&mes=10")).StatusCode);
        Assert.Equal(403, (int)(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/gestor/equipe/{julia}/ponto/espelho/download?ano=2026&mes=10")).StatusCode);
        Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync($"/api/gestor/equipe/{julia}/ponto/espelho/download?ano=2026&mes=10")).StatusCode);
        var anonimo = api.Api.ClienteAnonimo();
        Assert.Equal((401, 401), ((int)(await anonimo.GetAsync($"/api/rh/funcionarios/{julia}/ponto/espelho/download")).StatusCode, (int)(await anonimo.GetAsync($"/api/gestor/equipe/{julia}/ponto/espelho/download")).StatusCode));
    }

    // ---------------------------------------------------------------- auditoria, fonte e arquivos

    [Fact]
    public async Task Cada_download_deixa_o_evento_Baixou_com_o_nome_e_o_mes_e_se_o_registro_falha_nao_ha_link()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB;

        var antes = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", julia.FuncionarioId)).Count(e => e.Acao == AuditoriaAcao.Baixou);
        var ok = await ComoRhAsync(api, julia.FuncionarioId);
        var depoisDoOk = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", julia.FuncionarioId)).Where(e => e.Acao == AuditoriaAcao.Baixou).ToList();
        var fabrica = AtestadosDeTeste.ComAuditorQueFalhaNoDownload(api.Api);
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        var semRegistro = await cliente.GetAsync($"/api/rh/funcionarios/{julia.FuncionarioId}/ponto/espelho/download?ano=2026&mes=10");

        Assert.Equal(200, (int)ok.StatusCode);
        Assert.Equal(antes + 1, depoisDoOk.Count);
        var evento = depoisDoOk[^1];
        Assert.Equal((AuditoriaArea.Ponto, "Baixou o espelho de ponto de Júlia Nunes (10/2026) em PDF."), (evento.Area, evento.Descricao));
        Assert.Equal(depoisDoOk.Count, (await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", julia.FuncionarioId)).Count(e => e.Acao == AuditoriaAcao.Baixou)); // a que falhou não deixou evento
        Assert.Equal(500, (int)semRegistro.StatusCode); // o evento vem ANTES do link: sem ele, não há link
        Assert.DoesNotContain("url", await semRegistro.Content.ReadAsStringAsync(), StringComparison.Ordinal);
    }

    [Fact]
    public void A_fonte_do_PDF_e_so_a_embutida_com_acento_para_qualquer_nome_de_fonte_pedido()
    {
        var resolvedor = new FonteDoPdf();

        foreach (var familia in new[] { "Noto Sans", "Arial", "Segoe UI", "DejaVu Sans" })
        {
            var regular = resolvedor.ResolveTypeface(familia, isBold: false, isItalic: false);
            var negrito = resolvedor.ResolveTypeface(familia, isBold: true, isItalic: false);
            Assert.NotNull(regular);
            Assert.NotNull(negrito);
            Assert.NotEqual(regular!.FaceName, negrito!.FaceName);
            Assert.Equal([0x00, 0x01, 0x00, 0x00], resolvedor.GetFont(regular.FaceName)!.Take(4)); // um TTF de verdade, do assembly
            Assert.True(resolvedor.GetFont(negrito.FaceName)!.Length > 100_000);
            Assert.Equal(TamanhoDoRecurso("Fontes/NotoSans-Regular.ttf"), resolvedor.GetFont(regular.FaceName)!.Length); // cada face tem os bytes da sua fonte
            Assert.Equal(TamanhoDoRecurso("Fontes/NotoSans-Bold.ttf"), resolvedor.GetFont(negrito.FaceName)!.Length);
        }
    }

    private static long TamanhoDoRecurso(string nome)
    {
        using var fluxo = typeof(FonteDoPdf).Assembly.GetManifestResourceStream(nome)!;
        return fluxo.Length;
    }

    [Fact]
    public async Task A_limpeza_de_sobras_conhece_o_prefixo_espelhos_e_nada_o_referencia()
    {
        var api = await ApiAsync();
        await using var escopo = api.Api.Services.CreateAsyncScope();

        var fonte = Assert.Single(escopo.ServiceProvider.GetServices<IFonteDeReferenciasDeArquivo>(), f => f.Prefixo == "espelhos/");
        var referenciados = await fonte.ReferenciadosAsync(["espelhos/a.pdf", "espelhos/b.pdf"], CancellationToken.None);

        Assert.Empty(referenciados); // o PDF é de uso único: a limpeza apaga depois das horas mínimas
    }

    [Fact]
    public async Task As_respostas_seguem_o_contrato()
    {
        var api = await ApiAsync();
        var julia = api.Elenco.MembroB.FuncionarioId;

        await (await ComoRhAsync(api, julia)).AfirmarConformeAoContratoAsync("baixarEspelhoDePontoPdf");
        await (await ComoRhAsync(api, int.MaxValue)).AfirmarConformeAoContratoAsync("baixarEspelhoDePontoPdf"); // 404
        await (await ComoRhAsync(api, julia, "ano=2026&mes=13")).AfirmarConformeAoContratoAsync("baixarEspelhoDePontoPdf"); // 400
        await (await ComoGestorAsync(api, julia)).AfirmarConformeAoContratoAsync("baixarEspelhoDePontoPdfDaEquipe");
        await (await ComoGestorAsync(api, api.Elenco.DeOutroSetor.FuncionarioId)).AfirmarConformeAoContratoAsync("baixarEspelhoDePontoPdfDaEquipe"); // 404
    }
}
