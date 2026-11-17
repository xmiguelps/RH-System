using RHSystem.Demo.Exemplos;

namespace RHSystem.Api.IntegrationTests.Demo;

/// <summary>A linha de comando da ferramenta que o dono roda contra a demo hospedada: só o endereço da API é obrigatório e qualquer erro de digitação vira uma mensagem que diz o que consertar, em vez de uma exceção.</summary>
public class OpcoesDaFerramentaTests
{
    [Fact]
    public void So_com_o_endereco_os_outros_valores_sao_os_da_demo()
    {
        var (opcoes, mensagem) = OpcoesDaFerramenta.Ler(["--url", "https://rh-demo.azurewebsites.net"]);

        Assert.Null(mensagem);
        Assert.NotNull(opcoes);
        Assert.Equal(new Uri("https://rh-demo.azurewebsites.net"), opcoes.Url);
        Assert.Equal(("rhsystem-demo.example", "Demo#RH2026", null, null), (opcoes.Dominio, opcoes.Senha, opcoes.EmailDoRh, opcoes.Hoje));
    }

    [Fact]
    public void Todas_as_opcoes_sao_lidas()
    {
        var (opcoes, mensagem) = OpcoesDaFerramenta.Ler(["--url", "http://localhost:5080", "--dominio", "minha-demo.com.br", "--senha", "Outra#Senha1", "--email-do-rh", "rh@minha-demo.com.br", "--hoje", "2026-10-07"]);

        Assert.Null(mensagem);
        Assert.NotNull(opcoes);
        Assert.Equal(("minha-demo.com.br", "Outra#Senha1", "rh@minha-demo.com.br", new DateOnly(2026, 10, 7)), (opcoes.Dominio, opcoes.Senha, opcoes.EmailDoRh, opcoes.Hoje));
        Assert.Equal("localhost", opcoes.Url.Host);
    }

    [Theory]
    [InlineData("sem endereço", new string[0], "--url")]
    [InlineData("endereço relativo", new[] { "--url", "api/rh" }, "--url")]
    [InlineData("endereço de outro protocolo", new[] { "--url", "ftp://servidor" }, "--url")]
    [InlineData("opção desconhecida", new[] { "--url", "https://a.b", "--sendha", "x" }, "--sendha")]
    [InlineData("opção sem valor", new[] { "--url", "https://a.b", "--senha" }, "--senha")]
    [InlineData("data inválida", new[] { "--url", "https://a.b", "--hoje", "07/10/2026" }, "--hoje")]
    [InlineData("valor onde devia haver opção", new[] { "https://a.b" }, "https://a.b")]
    public void Erro_de_digitacao_vira_mensagem_que_cita_o_que_estava_errado(string caso, string[] argumentos, string citado)
    {
        var (opcoes, mensagem) = OpcoesDaFerramenta.Ler(argumentos);

        Assert.True(opcoes is null, caso);
        Assert.Contains(citado, mensagem);
    }

    [Theory]
    [InlineData("--ajuda")]
    [InlineData("-h")]
    public void Pedir_ajuda_mostra_o_uso_com_todas_as_opcoes(string pedido)
    {
        var (opcoes, mensagem) = OpcoesDaFerramenta.Ler([pedido]);

        Assert.Null(opcoes);
        Assert.True(OpcoesDaFerramenta.PediuAjuda([pedido]));
        foreach (var opcao in new[] { "--url", "--dominio", "--senha", "--email-do-rh", "--hoje" })
        {
            Assert.Contains(opcao, mensagem);
        }
    }

    [Fact]
    public void O_dia_de_hoje_da_demo_e_o_de_Brasilia_mesmo_perto_da_meia_noite_em_UTC()
    {
        Assert.Equal(new DateOnly(2026, 10, 6), OpcoesDaFerramenta.HojeEmBrasilia(new DateTime(2026, 10, 7, 2, 59, 0, DateTimeKind.Utc))); // 23:59 de terça em Brasília
        Assert.Equal(new DateOnly(2026, 10, 7), OpcoesDaFerramenta.HojeEmBrasilia(new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc))); // 00:00 de quarta em Brasília
    }
}
