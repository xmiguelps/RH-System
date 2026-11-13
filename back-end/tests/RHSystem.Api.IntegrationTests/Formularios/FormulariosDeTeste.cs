using System.Reflection;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Autenticacao;
using System.Text.Json;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// O que os testes dos formulários têm em comum: nomes únicos, o formulário semeado direto no banco em cada situação (datas LOCAIS, -03:00), a pergunta, a resposta (de pessoa ou anônima: o trigger exige o formulário ATIVO) e o gancho que faz "outra aba" mudar o banco
/// logo depois da primeira leitura do formulário (a prova das corridas entre a leitura e a gravação).
/// </summary>
internal static class FormulariosDeTeste
{
    private static int _sequencia;

    public static string Titulo(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    public static string Valor(string? v) => v is null ? "NULL" : $"'{v.Replace("'", "''", StringComparison.Ordinal)}'";

    public static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    public static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) => (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, System.Globalization.CultureInfo.InvariantCulture) : null;

    public static Task<long> ContarAsync(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql);

    public static Task<List<EventoLido>> EventosAsync(ApiDeAcesso api, int formularioId) => AuditoriaDeTeste.EventosAsync(api.Conexao, "FORMULARIOS", formularioId);

    /// <summary>Um formulário direto no banco, na situação pedida: rascunho (inativo, sem datas), agendado (inativo com a data de publicação), publicado (ativo) ou encerrado (inativo com a data de encerramento).</summary>
    public static async Task<int> SemearAsync(ApiDeAcesso api, string titulo, string situacao = "RASCUNHO", bool anonimo = false, string tipo = "PESQUISA", string? alvo = null, string? prazo = null, string? token = null, string? sentido = null, string? descricao = null)
    {
        var (ativo, publicacao, encerramento) = situacao switch
        {
            "AGENDADO" => (0, "'2026-10-20 00:00:00'", "NULL"),
            "PUBLICADO" => (1, "'2026-10-01 08:00:00'", "NULL"),
            "ENCERRADO" => (0, "'2026-09-01 08:00:00'", "'2026-10-01 00:00:00'"),
            _ => (0, "NULL", "NULL"),
        };
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO FORMULARIOS (TITULO, DESCRICAO, TIPO_FORMULARIO, SENTIDO_AVALIACAO, ID_FUNCIONARIO_AUTOR, DATA_CRIACAO, ATIVO, PUBLICO, DATA_PUBLICACAO, DATA_ENCERRAMENTO, DATA_PRAZO, ANONIMO, LINK_PUBLICO_CODIGO, LEMBRETE_AUTOMATICO) VALUES ({Valor(titulo)}, {Valor(descricao)}, '{tipo}', {Valor(sentido)}, {api.Elenco.Rh.FuncionarioId}, '2026-10-01 08:00:00', {ativo}, {Valor(alvo)}, {publicacao}, {encerramento}, {Valor(prazo)}, {(anonimo ? 1 : 0)}, {Valor(token)}, TRUE)");
        return (int)await ContarAsync(api, $"SELECT ID_FORMULARIO FROM FORMULARIOS WHERE TITULO = {Valor(titulo)}");
    }

    public static async Task<int> PerguntaAsync(ApiDeAcesso api, int formularioId, string texto, string tipo = "RESPOSTA_CURTA", int ordem = 1, bool obrigatoria = false, params string[] alternativas)
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PERGUNTAS (ID_FORMULARIO, TEXTO_PERGUNTA, TIPO_RESPOSTA, ORDEM, OBRIGATORIA) VALUES ({formularioId}, {Valor(texto)}, '{tipo}', {ordem}, {(obrigatoria ? 1 : 0)})");
        var id = (int)await ContarAsync(api, $"SELECT ID_FORMULARIO_PERGUNTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO = {formularioId} AND ORDEM = {ordem}");
        for (var i = 0; i < alternativas.Length; i++)
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_PERGUNTAS_ALTERNATIVAS (ID_FORMULARIO_PERGUNTA, TEXTO_ALTERNATIVA, ORDEM) VALUES ({id}, {Valor(alternativas[i])}, {i + 1})");
        }

        return id;
    }

    /// <summary>Uma resposta direto no banco: o formulário tem de estar ATIVO (o trigger recusa o inativo). Anônima = sem autor.</summary>
    public static async Task ResponderAsync(ApiDeAcesso api, int formularioId, int perguntaId, int? pessoaId, bool anonimo = false) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({perguntaId}, {formularioId}, {(pessoaId is null ? "NULL" : pessoaId.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))}, 'Foi bom.', '2026-10-05 10:00:00', {(anonimo ? 1 : 0)})");

    /// <summary>Um formulário publicado, com uma pergunta já respondida (por uma pessoa, ou sem autor se anônimo).</summary>
    public static async Task<int> PublicadoRespondidoAsync(ApiDeAcesso api, string titulo, bool anonimo, string tipo = "PESQUISA", string? alvo = "PESSOAS")
    {
        var id = await SemearAsync(api, titulo, "PUBLICADO", anonimo, tipo, alvo);
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        var pessoa = anonimo ? (int?)null : await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("Respondente"), Perfil.Funcionario);
        await ResponderAsync(api, id, pergunta, pessoa, anonimo);
        return id;
    }

    /// <summary>
    /// Passa tudo para a consulta de verdade, mas logo depois da PRIMEIRA leitura de um formulário roda <see cref="Depois"/> (outra aba que publica, responde ou encerra). A gravação condicionada ao estado lido tem de recusar e o caso de uso reler e dar o erro certo.
    /// </summary>
    public class ConsultaComGancho : DispatchProxy
    {
        public IConsultaDeFormularios Real { get; set; } = null!;

        public Func<Task> Depois { get; set; } = () => Task.CompletedTask;

        private int _leituras;

        protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos)
        {
            var retorno = metodo!.Invoke(Real, argumentos);
            if (metodo.Name != nameof(IConsultaDeFormularios.ObterAsync) || retorno is not Task<FormularioLido?> leitura)
            {
                return retorno;
            }

            return DepoisDeLerAsync(leitura);
        }

        private async Task<FormularioLido?> DepoisDeLerAsync(Task<FormularioLido?> leitura)
        {
            var lido = await leitura;
            if (Interlocked.Increment(ref _leituras) == 1)
            {
                await Depois();
            }

            return lido;
        }
    }

    /// <summary>Um cliente do RH num hospedeiro em que a consulta dos formulários roda <paramref name="outraAba"/> depois da primeira leitura.</summary>
    public static async Task<HttpClient> ClienteComOutraAbaAsync(ApiDeAcesso api, Func<Task> outraAba)
    {
        var fabrica = api.Api.WithWebHostBuilder(construtor => construtor.ConfigureServices(servicos =>
        {
            servicos.RemoveAll<IConsultaDeFormularios>();
            servicos.AddScoped<IConsultaDeFormularios>(sp =>
            {
                var proxy = DispatchProxy.Create<IConsultaDeFormularios, ConsultaComGancho>();
                var configurado = (ConsultaComGancho)(object)proxy;
                configurado.Real = ActivatorUtilities.CreateInstance<RHSystem.Infrastructure.Formularios.ConsultaDeFormularios>(sp);
                configurado.Depois = outraAba;
                return proxy;
            });
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;
        return cliente;
    }
}
