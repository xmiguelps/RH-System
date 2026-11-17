using System.Text.Json;
using System.Text.RegularExpressions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Demo.Exemplos;
using UglyToad.PdfPig;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Demo.DemoDeTeste;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>
/// O passo extra da demo (<c>Banco de Dados/Demo/LEIA-ME.md</c>): depois do seed, a ferramenta <c>RHSystem.Demo.Exemplos</c> entra na API como o RH e manda holerites e atestados de exemplo, de verdade, pelas mesmas rotas que o front usa. Aqui ela roda contra o banco que o seed montou, com o relógio no
/// dia do seed (quarta, 2026-10-07): os testes conferem o que ficou no banco, o que a pessoa vê e o texto dos PDFs que foram guardados.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ExemplosDaDemoTests(MySqlFixture mysql)
{
    private static string Compacto(string texto) => new([.. texto.Where(c => !char.IsWhiteSpace(c))]);

    private static string TextoDoPdf(byte[] bytes)
    {
        using var documento = PdfDocument.Open(bytes);
        return Compacto(string.Join(' ', documento.GetPages().Select(p => p.Text)));
    }

    private static string CaminhoDoLink(JsonElement link) => new Uri(link.GetProperty("url").GetString()!).AbsolutePath.TrimStart('/');

    private static EnviadorDeExemplos Enviador(ApiFactory api, string senha = Senha) => new(api.CreateClient(), Hoje, Dominio, senha);

    [Fact]
    public async Task Os_holerites_de_exemplo_chegam_para_as_doze_pessoas_ativas_nos_dois_meses_anteriores()
    {
        var api = await ApiAsync(mysql);

        var relatorio = await Enviador(api).EnviarAsync();

        Assert.Equal(24, relatorio.HoleritesEnviados); // doze pessoas ativas, agosto e setembro de 2026
        Assert.Equal((0, 0), (relatorio.HoleritesQueJaExistiam, relatorio.HoleritesRecusados));
        Assert.Empty(relatorio.Avisos);
        Assert.Equal(24L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM HOLERITES"));
        Assert.Equal(12L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(DISTINCT ID_FUNCIONARIO) FROM HOLERITES WHERE ANO = 2026 AND MES = 8 AND TIPO_HOLERITE = 'MENSAL'"));
        Assert.Equal(12L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(DISTINCT ID_FUNCIONARIO) FROM HOLERITES WHERE ANO = 2026 AND MES = 9 AND TIPO_HOLERITE = 'MENSAL'"));
        Assert.Equal(0L, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM HOLERITES h JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = h.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = '{Email("julia.andrade")}'")); // quem ainda não começou não recebe
    }

    [Fact]
    public async Task O_holerite_guardado_e_um_PDF_de_verdade_com_o_nome_o_cargo_e_a_competencia_da_pessoa()
    {
        var api = await ApiAsync(mysql);
        await Enviador(api).EnviarAsync();

        var lucas = await EntrarComoAsync(api, "lucas.pereira");
        var lista = await GetAsync(lucas, "/api/eu/holerites");
        var setembro = lista.GetProperty("itens").EnumerateArray().Single(h => h.GetProperty("mes").GetInt32() == 9);
        var link = await GetAsync(lucas, $"/api/eu/holerites/{setembro.GetProperty("id").GetInt32()}/download");
        var bytes = api.Armazenamento.Conteudo(CaminhoDoLink(link))!;

        var texto = TextoDoPdf(bytes);
        Assert.Equal(2, lista.GetProperty("itens").GetArrayLength());
        Assert.Contains("LucasPereira", texto);
        Assert.Contains("Desenvolvedor", texto);
        Assert.Contains("Tecnologia", texto);
        Assert.Contains("Setembro/2026", texto);
    }

    [Fact]
    public async Task O_atestado_de_exemplo_cobre_a_falta_do_Thiago_e_o_RH_ja_validou()
    {
        var api = await ApiAsync(mysql);

        var relatorio = await Enviador(api).EnviarAsync();

        Assert.Equal((1, 1), (relatorio.AtestadosEnviados, relatorio.AtestadosValidados));
        var thiago = await EntrarComoAsync(api, "thiago.mello");
        var espelho = await GetAsync(thiago, "/api/eu/ponto/espelho?ano=2026&mes=9");
        var dia = espelho.GetProperty("dias").EnumerateArray().Single(d => d.GetProperty("data").GetString() == "2026-09-30");
        Assert.Equal("FALTA_COMPENSADA", dia.GetProperty("situacao").GetString());
        Assert.Equal(1L, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM ATESTADOS a JOIN FUNCIONARIOS f ON f.ID_FUNCIONARIO = a.ID_FUNCIONARIO WHERE f.EMAIL_CORPORATIVO = '{Email("thiago.mello")}' AND a.STATUS_ATESTADO = 'VALIDADO' AND a.DATA_INICIO = '2026-09-30' AND a.DATA_FIM = '2026-09-30'"));
        var caminho = (string)(await Sql.EscalarAsync(api.Conexao, "SELECT CAMINHO_ARQUIVO FROM ATESTADOS"))!;
        var texto = TextoDoPdf(api.Armazenamento.Conteudo(caminho)!);
        Assert.Contains("ThiagoMello", texto);
        Assert.Contains("30/09/2026", texto);
    }

    [Fact]
    public async Task Rodar_de_novo_nao_duplica_nem_falha()
    {
        var api = await ApiAsync(mysql);
        await Enviador(api).EnviarAsync();

        var segunda = await Enviador(api).EnviarAsync();

        Assert.Equal((0, 24, 0, 0, 0), (segunda.HoleritesEnviados, segunda.HoleritesQueJaExistiam, segunda.HoleritesRecusados, segunda.AtestadosEnviados, segunda.AtestadosValidados));
        Assert.Equal(24L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM HOLERITES"));
        Assert.Equal(1L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM ATESTADOS"));
    }

    [Fact]
    public async Task Com_a_senha_errada_o_envio_para_antes_de_mandar_qualquer_coisa_e_diz_que_foi_o_login()
    {
        var api = await ApiAsync(mysql);

        var erro = await Assert.ThrowsAsync<InvalidOperationException>(() => Enviador(api, "senha-errada").EnviarAsync());

        Assert.Contains("login", erro.Message, StringComparison.OrdinalIgnoreCase);
        Assert.Equal(0L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM HOLERITES"));
    }

    [Fact]
    public async Task Uma_competencia_que_a_API_recusa_vai_para_os_avisos_e_nao_derruba_o_resto()
    {
        var api = await ApiAsync(mysql);
        // a Bruno foi admitido há 6 meses (abril); movendo a admissão dele para setembro, agosto passa a ser anterior à admissão e a API recusa só esse
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_ADMISSAO = '2026-09-01' WHERE EMAIL_CORPORATIVO = '{Email("bruno.azevedo")}'");

        var relatorio = await Enviador(api).EnviarAsync();

        Assert.Equal((23, 1), (relatorio.HoleritesEnviados, relatorio.HoleritesRecusados));
        var aviso = Assert.Single(relatorio.Avisos);
        Assert.Contains("Bruno Azevedo", aviso);
        Assert.Contains("08/2026", aviso);
    }

    [Fact]
    public async Task Rodar_de_novo_em_outro_dia_nao_manda_outro_atestado_para_a_pessoa_que_ja_tem_um_validado()
    {
        var api = await ApiAsync(mysql);
        await Enviador(api).EnviarAsync(); // no dia do seed: o atestado da falta de 30/09
        api.Relogio.Fixar(new DateTime(2026, 10, 9, 12, 42, 0, DateTimeKind.Utc)); // dois dias depois, 7 e 8/10 também aparecem como dias sem registro no espelho do Thiago

        var outroDia = await new EnviadorDeExemplos(api.CreateClient(), new DateOnly(2026, 10, 9), Dominio, Senha).EnviarAsync();

        Assert.Equal((0, 0), (outroDia.AtestadosEnviados, outroDia.AtestadosValidados));
        Assert.Equal(1L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM ATESTADOS"));
    }

    [Fact]
    public async Task Um_atestado_que_ficou_so_enviado_porque_a_rodada_anterior_parou_antes_de_validar_e_validado_em_vez_de_mandar_outro()
    {
        var api = await ApiAsync(mysql);
        var thiago = await EntrarComoAsync(api, "thiago.mello");
        Assert.Equal(201, (int)(await thiago.PostAsync("/api/eu/ponto/atestados", Ponto.AtestadosDeTeste.Dias("2026-09-30", "2026-09-30"))).StatusCode); // o envio que a ferramenta fez antes de cair

        var relatorio = await Enviador(api).EnviarAsync();

        Assert.Equal((0, 1), (relatorio.AtestadosEnviados, relatorio.AtestadosValidados));
        Assert.Equal(1L, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM ATESTADOS"));
        Assert.Equal("VALIDADO", await Sql.EscalarAsync(api.Conexao, "SELECT STATUS_ATESTADO FROM ATESTADOS") as string);
    }

    [Fact]
    public async Task O_UPDATE_que_o_LEIA_ME_manda_rodar_cancela_so_os_emails_pendentes_do_dominio_ficticio()
    {
        var api = await ApiAsync(mysql);
        var leiaMe = File.ReadAllText(Path.Combine(Path.GetDirectoryName(ArquivoDoSeed())!, "LEIA-ME.md"));
        var comando = Regex.Match(leiaMe, @"UPDATE FILA_EMAILS[^;]*;").Value; // o comando está na seção do passo 4, e é o que o dono vai copiar
        Assert.NotEmpty(comando);
        // o sigiloso (convite ou redefinição de senha) não pode virar CANCELADO: o banco recusa (CHK_FILA_EMAILS_SIGILOSO_NAO_FICA) e derrubaria o comando inteiro
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FILA_EMAILS (TIPO, CHAVE_IDEMPOTENCIA, DESTINATARIO_EMAIL, CARGA, SIGILOSO) VALUES ('TESTE_DEMO', 'TESTE_DEMO:ficticio', 'lucas.pereira@{Dominio}', JSON_OBJECT('a', 1), FALSE), ('TESTE_DEMO', 'TESTE_DEMO:real', 'alguem@exemplo.com.br', JSON_OBJECT('a', 1), FALSE), ('TESTE_DEMO', 'TESTE_DEMO:sigiloso', 'lucas.pereira@{Dominio}', JSON_OBJECT('a', 1), TRUE)");

        await Sql.ExecutarAsync(api.Conexao, comando);

        Assert.Equal("CANCELADO", await Sql.EscalarAsync(api.Conexao, "SELECT SITUACAO FROM FILA_EMAILS WHERE CHAVE_IDEMPOTENCIA = 'TESTE_DEMO:ficticio'") as string);
        Assert.Equal("PENDENTE", await Sql.EscalarAsync(api.Conexao, "SELECT SITUACAO FROM FILA_EMAILS WHERE CHAVE_IDEMPOTENCIA = 'TESTE_DEMO:real'") as string);
        Assert.Equal("PENDENTE", await Sql.EscalarAsync(api.Conexao, "SELECT SITUACAO FROM FILA_EMAILS WHERE CHAVE_IDEMPOTENCIA = 'TESTE_DEMO:sigiloso'") as string);
    }
}
