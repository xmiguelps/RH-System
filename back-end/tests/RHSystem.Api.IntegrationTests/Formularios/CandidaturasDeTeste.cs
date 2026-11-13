using System.Text;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Application.Formularios.Regras;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>Uma entrevista por link já montada: o formulário, o código do link e as quatro perguntas (múltipla escolha obrigatória, resposta longa obrigatória, arquivo opcional e a alternativa "Sim").</summary>
internal sealed record Entrevista(int Id, string Token, int Multipla, int Longa, int Anexo, int Sim);

/// <summary>O que os testes de candidatura (envio pelo link e telas do RH) compartilham: a entrevista, o corpo <c>multipart</c> e o envio.</summary>
internal static class CandidaturasDeTeste
{
    public static async Task<Entrevista> EntrevistaAsync(ApiDeAcesso api, string prefixo, string situacao = "PUBLICADO", string? prazo = null)
    {
        var token = TokenDoLink.Novo();
        var id = await SemearAsync(api, Titulo(prefixo), situacao == "ENCERRADO" ? "PUBLICADO" : situacao, tipo: "ENTREVISTA", alvo: "LINK", prazo: prazo, token: token);
        var multipla = await PerguntaAsync(api, id, "Você tem disponibilidade aos sábados?", "MULTIPLA_ESCOLHA", 1, true, "Sim", "Não");
        var longa = await PerguntaAsync(api, id, "Conte uma experiência.", "RESPOSTA_LONGA", 2, true);
        var anexo = await PerguntaAsync(api, id, "Envie um comprovante.", "ARQUIVO", 3);
        var sim = (int)await ContarAsync(api, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {multipla} AND ORDEM = 1");
        if (situacao == "ENCERRADO")
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = FALSE, DATA_ENCERRAMENTO = '2026-10-06 00:00:00' WHERE ID_FORMULARIO = {id}");
        }

        return new Entrevista(id, token, multipla, longa, anexo, sim);
    }

    public static string RespostasOk(Entrevista e, bool comArquivo = false) => comArquivo
        ? Json(new { perguntaId = e.Multipla, alternativaIds = new[] { e.Sim } }, new { perguntaId = e.Longa, texto = "Reorganizei um estoque em dois dias." }, new { perguntaId = e.Anexo, arquivoIndice = 0 })
        : Json(new { perguntaId = e.Multipla, alternativaIds = new[] { e.Sim } }, new { perguntaId = e.Longa, texto = "Reorganizei um estoque em dois dias." });

    public static MultipartFormDataContent Candidatura(string? respostas, string? nome = "Mariana Teixeira", string? email = "mariana.teixeira@email.com.br", string? telefone = "(11) 98877-1204", string? aceitou = "true", (string Nome, byte[] Bytes)? curriculo = null, params (string Nome, byte[] Bytes)[] arquivos)
    {
        var formulario = new MultipartFormDataContent();
        void Campo(string nomeDoCampo, string? valor)
        {
            if (valor is not null)
            {
                formulario.Add(new StringContent(valor, Encoding.UTF8), nomeDoCampo);
            }
        }

        Campo("nome", nome);
        Campo("email", email);
        Campo("telefone", telefone);
        Campo("aceitouAvisoDePrivacidade", aceitou);
        Campo("respostas", respostas);
        if (curriculo is { } c)
        {
            formulario.Add(new ByteArrayContent(c.Bytes), "curriculo", c.Nome);
        }

        foreach (var (arquivoNome, bytes) in arquivos)
        {
            formulario.Add(new ByteArrayContent(bytes), "arquivos", arquivoNome);
        }

        return formulario;
    }

    public static Task<HttpResponseMessage> EnviarAsync(HttpClient cliente, string token, HttpContent corpo) => cliente.PostAsync($"/api/publico/formularios/{token}/respostas", corpo);

    /// <summary>Um candidato que se candidatou de verdade (pela rota pública) e o número dele em <c>FORMULARIOS_CANDIDATOS</c>.</summary>
    public static async Task<int> CandidatarAsync(ApiDeAcesso api, Entrevista e, string nome, string email, string? telefone = "(11) 98877-1204", (string Nome, byte[] Bytes)? curriculo = null, bool comArquivo = false, params (string Nome, byte[] Bytes)[] arquivos)
    {
        using var corpo = Candidatura(RespostasOk(e, comArquivo), nome, email, telefone, "true", curriculo, arquivos);
        var resposta = await EnviarAsync(api.Api.ClienteAnonimo(), e.Token, corpo);
        Assert.Equal(201, (int)resposta.StatusCode);
        return (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_CANDIDATO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO = {e.Id} AND EMAIL = '{email.Trim().ToLowerInvariant()}'");
    }
}
