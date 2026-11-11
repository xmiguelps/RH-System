using System.Collections.Concurrent;
using System.Net;
using System.Text.Json;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Emails;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Api.IntegrationTests.Emails;

/// <summary>O <see cref="IEnviadorDeEmail"/> dos testes: guarda o que "enviou" e falha como o teste mandar. Seguro para vários processadores ao mesmo tempo.</summary>
public sealed class EnviadorDeEmailEmMemoria : IEnviadorDeEmail
{
    private readonly ConcurrentQueue<MensagemDeEmail> _enviadas = new();

    /// <summary>Devolve a exceção que o envio daquela mensagem deve lançar, ou <c>null</c> para enviar.</summary>
    public Func<MensagemDeEmail, Exception?> Comportamento { get; set; } = _ => null;

    /// <summary>Uma pausa por mensagem, para dois processadores de verdade se sobreporem no tempo.</summary>
    public TimeSpan Pausa { get; set; } = TimeSpan.Zero;

    public IReadOnlyList<MensagemDeEmail> Enviadas => [.. _enviadas];

    public void Limpar()
    {
        _enviadas.Clear();
        Comportamento = _ => null;
        Pausa = TimeSpan.Zero;
    }

    public async Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken ct)
    {
        if (Pausa > TimeSpan.Zero)
        {
            await Task.Delay(Pausa, ct);
        }

        if (Comportamento(mensagem) is { } erro)
        {
            throw erro;
        }

        _enviadas.Enqueue(mensagem);
    }
}

/// <summary>Um renderizador de teste: assunto e texto com o <c>nome</c> da carga, HTML com ele codificado, e o evento de auditoria sem destinatário.</summary>
public sealed class RenderizadorDeTeste(string tipo = "TESTE_ENVIO", string descricaoDeAuditoria = "Aviso de teste enviado") : IRenderizadorDeEmail
{
    public string Tipo => tipo;

    public ModeloDeEmail Renderizar(JsonElement carga, string destinatario)
    {
        var nome = carga.TryGetProperty("nome", out var n) ? n.GetString() ?? string.Empty : string.Empty;
        return new ModeloDeEmail($"Olá, {nome}", $"Texto para {nome}", $"<p>{WebUtility.HtmlEncode(nome)}</p>", AuditoriaArea.Avisos, descricaoDeAuditoria, "Aviso de teste");
    }
}

/// <summary>
/// Uma API (e um banco) só para os testes de e-mail: o serviço em segundo plano fica desligado e os testes chamam <c>ProcessarCicloAsync</c> direto. Cada teste começa
/// com <see cref="ComecarAsync"/>: fila vazia, enviador limpo e o relógio falso em 07/10/2026 12:42 UTC.
/// </summary>
internal static class ApiDeEmails
{
    private static readonly SemaphoreSlim Trava = new(1, 1);
    private static ApiFactory? _api;

    public static async Task<ApiFactory> ComecarAsync(MySqlFixture mysql)
    {
        await Trava.WaitAsync();
        try
        {
            _api ??= await ApiFactory.CriarAsync(mysql);
        }
        finally
        {
            Trava.Release();
        }

        await Auditoria.AuditoriaDeTeste.ExecutarAsync(_api.Conexao, "DELETE FROM FILA_EMAILS");
        _api.Enviador.Limpar();
        _api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return _api;
    }
}

/// <summary>O Mailpit (captador de e-mail de desenvolvimento) em contêiner, uma vez por classe de teste: SMTP na 1025 e a API/interface na 8025, em portas livres da máquina.</summary>
public sealed class MailpitFixture : IAsyncLifetime
{
    // Versão estável conferida em 2026-11-04 (v1.31.6, de 15/10/2026); nunca "latest". Mesma do docker-compose.yml.
    public const string Imagem = "axllent/mailpit:v1.31.6";

    private readonly IContainer _container = new ContainerBuilder(Imagem)
        .WithPortBinding(1025, assignRandomHostPort: true)
        .WithPortBinding(8025, assignRandomHostPort: true)
        .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPort(8025).ForPath("/api/v1/info")))
        .Build();

    public string Servidor => _container.Hostname;

    public int PortaSmtp => _container.GetMappedPublicPort(1025);

    public string UrlDaApi => $"http://{_container.Hostname}:{_container.GetMappedPublicPort(8025)}";

    public Task InitializeAsync() => _container.StartAsync();

    public Task DisposeAsync() => _container.DisposeAsync().AsTask();
}
