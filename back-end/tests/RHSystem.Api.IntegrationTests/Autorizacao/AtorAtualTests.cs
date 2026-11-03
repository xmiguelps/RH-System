using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Escopo;
using static RHSystem.Api.IntegrationTests.Autorizacao.ApiDeAutorizacao;

namespace RHSystem.Api.IntegrationTests.Autorizacao;

/// <summary>Quem está agindo: pessoa logada, anônimo ou o sistema (fora de requisição).</summary>
[Collection(MySqlCollection.Nome)]
public class AtorAtualTests(MySqlFixture mysql)
{
    [Fact]
    public async Task IAtorAtual_em_requisicao_autenticada_le_usuario_funcionario_perfil_nome_e_ip()
    {
        var api = await ObterAsync(mysql);
        var token = await TokenAsync(api, ApiFactory.EmailRh);

        var resposta = await GetAsync(api.CreateClient(), "/api/eu/__teste/ator", token);

        Assert.Equal(System.Net.HttpStatusCode.OK, resposta.StatusCode);
        var ator = await LerAsync(resposta);
        Assert.Equal("PESSOA", ator.GetProperty("tipo").GetString());
        Assert.True(ator.GetProperty("usuarioId").GetInt32() > 0);
        Assert.True(ator.GetProperty("funcionarioId").GetInt32() > 0);
        Assert.Equal("RH", ator.GetProperty("perfil").GetString());
        Assert.Equal("Pessoa de Teste", ator.GetProperty("nome").GetString());
        Assert.Equal("127.0.0.1", ator.GetProperty("ip").GetString()); // o middleware de teste do ApiFactory injeta o IP
        Assert.Equal("PORTAL", ator.GetProperty("canal").GetString());
    }

    [Fact]
    public async Task IAtorAtual_em_requisicao_sem_login_e_Anonimo_e_fora_de_requisicao_e_Sistema_sem_ip()
    {
        var api = await ObterAsync(mysql);

        var anonimo = await LerAsync(await GetAsync(api.CreateClient(), "/api/publico/__teste/ator"));

        Assert.Equal("ANONIMO", anonimo.GetProperty("tipo").GetString());
        Assert.Equal("Anônimo", anonimo.GetProperty("nome").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, anonimo.GetProperty("usuarioId").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, anonimo.GetProperty("funcionarioId").ValueKind);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, anonimo.GetProperty("perfil").ValueKind);
        Assert.Equal("PORTAL", anonimo.GetProperty("canal").GetString());

        // Fora de requisição (um serviço em segundo plano): não há HttpContext.
        _ = api.Server; // garante o host de pé
        using var escopo = api.Services.CreateScope();
        var sistema = escopo.ServiceProvider.GetRequiredService<IAtorAtual>();
        Assert.Equal(TipoDeAtorAtual.Sistema, sistema.Tipo);
        Assert.Equal("Sistema", sistema.Nome);
        Assert.Null(sistema.Ip);
        Assert.Null(sistema.UsuarioId);
        Assert.Null(sistema.FuncionarioId);
        Assert.Null(sistema.Perfil);
        Assert.Equal(RHSystem.Domain.Auditoria.CanalDeOrigem.Sistema, sistema.Canal);
    }

    [Fact]
    public async Task IAtorAtual_com_token_de_perfil_desconhecido_nao_inventa_perfil()
    {
        // A rota de teste do anônimo lê o ator mesmo com um token na requisição: o perfil que não é um dos três vira nulo, nunca um valor qualquer.
        var api = await ObterAsync(mysql);

        var resposta = await GetAsync(api.CreateClient(), "/api/publico/__teste/ator", TokenForjado("Diretor"));

        var ator = await LerAsync(resposta);
        Assert.Equal("PESSOA", ator.GetProperty("tipo").GetString());
        Assert.Equal(System.Text.Json.JsonValueKind.Null, ator.GetProperty("perfil").ValueKind);
    }
}
