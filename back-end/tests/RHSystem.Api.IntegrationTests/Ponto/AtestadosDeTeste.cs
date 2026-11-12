using System.Net.Http.Headers;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Auditoria;
using RHSystem.Domain.Auditoria;
using RHSystem.Infrastructure.Auditoria;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>Os formulários <c>multipart/form-data</c> do atestado, como o front os mandaria (e como ele nunca mandaria), e a semente de atestados com a data de envio que o teste escolhe.</summary>
internal static class AtestadosDeTeste
{
    /// <summary>Uma parte de arquivo; <paramref name="Campo"/> é o nome do campo do formulário.</summary>
    public sealed record Parte(string Campo, string NomeDoArquivo, byte[] Bytes);

    /// <summary>Um formulário de envio. <c>null</c> num campo de texto não manda o campo; o arquivo, quando vem, é o do campo <c>arquivo</c> (mais partes entram em <paramref name="arquivos"/>).</summary>
    public static MultipartFormDataContent Envio(string? dataInicio, string? dataFim, string? horaInicio = null, string? horaFim = null, params Parte[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        Texto(formulario, "dataInicio", dataInicio);
        Texto(formulario, "dataFim", dataFim);
        Texto(formulario, "horaInicio", horaInicio);
        Texto(formulario, "horaFim", horaFim);
        foreach (var parte in arquivos)
        {
            var conteudo = new ByteArrayContent(parte.Bytes);
            conteudo.Headers.ContentType = new MediaTypeHeaderValue("application/octet-stream"); // o tipo de verdade vem dos primeiros bytes
            formulario.Add(conteudo, parte.Campo, parte.NomeDoArquivo);
        }

        return formulario;
    }

    /// <summary>Um atestado de dias inteiros com um PDF no campo <c>arquivo</c>.</summary>
    public static MultipartFormDataContent Dias(string dataInicio, string dataFim, string nome = "atestado.pdf", byte[]? bytes = null) =>
        Envio(dataInicio, dataFim, null, null, new Parte("arquivo", nome, bytes ?? ArquivosDeTeste.Pdf(2048)));

    /// <summary>Um atestado de horas de um dia só com um PDF no campo <c>arquivo</c>.</summary>
    public static MultipartFormDataContent Horas(string dia, string horaInicio, string horaFim, string nome = "declaracao.pdf") =>
        Envio(dia, dia, horaInicio, horaFim, new Parte("arquivo", nome, ArquivosDeTeste.Pdf(2048)));

    /// <summary>
    /// Insere um atestado direto no banco, com o instante de envio que o teste quer (o padrão do banco é o relógio do servidor, que os testes não controlam) e, se não for <c>ENVIADO</c>, a decisão do RH. As datas e instantes são UTC.
    /// </summary>
    public static async Task<int> SemearAsync(string conexao, int funcionarioId, string inicio, string fim, string situacao = "ENVIADO", int? rhId = null, string enviadoEm = "2026-10-05 12:00:00", string nome = "atestado.pdf", string? horaInicio = null, string? horaFim = null, string? motivo = null)
    {
        var caminho = $"atestados/{Guid.NewGuid():N}.pdf";
        var horas = horaInicio is null ? "NULL, NULL" : $"'{horaInicio}', '{horaFim}'";
        var decisao = situacao == "ENVIADO" ? "NULL, NULL" : $"{rhId ?? throw new ArgumentException("A decisão precisa do RH.")}, '2026-10-06 10:00:00'";
        var recusa = situacao == "RECUSADO" ? $"'{motivo ?? "Documento ilegível."}'" : "NULL";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO ATESTADOS (ID_FUNCIONARIO, DATA_INICIO, DATA_FIM, HORA_INICIO, HORA_FIM, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL, STATUS_ATESTADO, ID_FUNCIONARIO_RH, DATA_DECISAO, MOTIVO_RECUSA, DATA_ENVIO) VALUES ({funcionarioId}, '{inicio}', '{fim}', {horas}, '{caminho}', '{nome}', '{situacao}', {decisao}, {recusa}, '{enviadoEm}')");
        return (int)await Sql.ContarAsync(conexao, $"SELECT ID_ATESTADO FROM ATESTADOS WHERE CAMINHO_ARQUIVO = '{caminho}'");
    }

    /// <summary>Uma segunda API sobre o MESMO banco em que o <see cref="IAuditor"/> de verdade falha só ao gravar o evento <c>Baixou</c>: a rota de download tem de falhar junto, sem devolver link.</summary>
    public static WebApplicationFactory<Program> ComAuditorQueFalhaNoDownload(WebApplicationFactory<Program> api) =>
        api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IAuditor>();
            servicos.AddScoped<IAuditor>(sp => new AuditorQueFalhaNoDownload(ActivatorUtilities.CreateInstance<Auditor>(sp)));
        }));

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

    private static void Texto(MultipartFormDataContent formulario, string campo, string? valor)
    {
        if (valor is not null)
        {
            formulario.Add(new StringContent(valor), campo);
        }
    }
}
