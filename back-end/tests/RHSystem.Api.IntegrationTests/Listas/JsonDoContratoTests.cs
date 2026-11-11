using System.Text;
using System.Text.Json;
using RHSystem.Api.Configuracao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Listas;

/// <summary>
/// O JSON do contrato (<c>CONVENCOES.md</c>, seção 3): instante em UTC com Z, data <c>yyyy-MM-dd</c>, hora <c>HH:mm</c>, enum em
/// MAIÚSCULAS_COM_SUBLINHADO (menos o <c>Perfil</c>). As rotas de teste ficam em <c>/__teste/json/*</c>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class JsonDoContratoTests(MySqlFixture mysql)
{
    private async Task<HttpClient> ClienteAsync() => (await ApiCompartilhada.ObterAsync(mysql)).CreateClient();

    private static JsonSerializerOptions Opcoes()
    {
        var opcoes = new JsonSerializerOptions(JsonSerializerDefaults.Web);
        JsonDoContrato.Configurar(opcoes);
        return opcoes;
    }

    private static StringContent Json(string texto) => new(texto, Encoding.UTF8, "application/json");

    [Fact]
    public async Task Instante_UTC_sai_com_Z_e_instante_sem_Kind_Utc_lanca()
    {
        var cliente = await ClienteAsync();

        Assert.Equal("{\"quando\":\"2026-10-07T12:42:00Z\"}", await cliente.GetStringAsync("/__teste/json/instante"));

        // Um datetime que o provedor devolveu sem Kind (ou com Kind Local) sairia sem Z, e o front o leria no fuso dele: melhor quebrar.
        foreach (var tipo in new[] { DateTimeKind.Unspecified, DateTimeKind.Local })
        {
            var semUtc = new DateTime(2026, 10, 7, 12, 42, 0, tipo);
            Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new { quando = semUtc }, Opcoes()));
        }

        var resposta = await cliente.GetAsync("/__teste/json/instante-sem-kind");
        Assert.Equal(System.Net.HttpStatusCode.InternalServerError, resposta.StatusCode);
    }

    [Fact]
    public async Task Instante_com_deslocamento_na_entrada_vira_UTC_e_sem_deslocamento_e_recusado()
    {
        var cliente = await ClienteAsync();

        var comDeslocamento = await cliente.PostAsync("/__teste/json/instante", Json("{\"quando\":\"2026-10-07T09:42:00-03:00\"}"));
        var comZ = await cliente.PostAsync("/__teste/json/instante", Json("{\"quando\":\"2026-10-07T12:42:00Z\"}"));
        var semDeslocamento = await cliente.PostAsync("/__teste/json/instante", Json("{\"quando\":\"2026-10-07T12:42:00\"}"));

        Assert.Equal("{\"quando\":\"2026-10-07T12:42:00Z\"}", await comDeslocamento.Content.ReadAsStringAsync());
        Assert.Equal("{\"quando\":\"2026-10-07T12:42:00Z\"}", await comZ.Content.ReadAsStringAsync());
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, semDeslocamento.StatusCode);
    }

    [Fact]
    public async Task DateOnly_sai_yyyy_MM_dd_e_TimeOnly_sai_HH_mm()
    {
        var cliente = await ClienteAsync();

        Assert.Equal("{\"dia\":\"2026-10-07\",\"horario\":\"08:05\"}", await cliente.GetStringAsync("/__teste/json/data-e-hora"));

        var entrada = await cliente.PostAsync("/__teste/json/data-e-hora", Json("{\"dia\":\"2026-02-03\",\"horario\":\"17:30\"}"));
        Assert.Equal("{\"dia\":\"2026-02-03\",\"horario\":\"17:30\"}", await entrada.Content.ReadAsStringAsync());

        var horarioComSegundos = await cliente.PostAsync("/__teste/json/data-e-hora", Json("{\"dia\":\"2026-02-03\",\"horario\":\"17:30:15\"}"));
        Assert.Equal(System.Net.HttpStatusCode.BadRequest, horarioComSegundos.StatusCode);
    }

    [Fact]
    public void Horario_com_segundos_na_saida_lanca_em_vez_de_perder_os_segundos()
    {
        Assert.Throws<InvalidOperationException>(() => JsonSerializer.Serialize(new TimeOnly(8, 5, 30), Opcoes()));
        Assert.Equal("\"08:05\"", JsonSerializer.Serialize(new TimeOnly(8, 5), Opcoes()));
    }

    [Fact]
    public async Task Enum_sai_em_MAIUSCULAS_COM_SUBLINHADO_e_Perfil_continua_Funcionario_Gestor_RH()
    {
        var cliente = await ClienteAsync();

        var texto = await cliente.GetStringAsync("/__teste/json/enums");

        Assert.Equal("{\"acao\":\"ACESSO_NEGADO\",\"area\":\"COLABORADORES\",\"perfil\":\"RH\",\"perfis\":[\"Funcionario\",\"Gestor\",\"RH\"]}", texto);
    }

    [Fact]
    public async Task Enum_desconhecido_na_entrada_devolve_400_com_errors()
    {
        var cliente = await ClienteAsync();

        var conhecido = await cliente.PostAsync("/__teste/json/enums", Json("{\"acao\":\"ACESSO_NEGADO\",\"perfil\":\"Gestor\"}"));
        var desconhecido = await cliente.PostAsync("/__teste/json/enums", Json("{\"acao\":\"INEXISTENTE\"}"));
        var numero = await cliente.PostAsync("/__teste/json/enums", Json("{\"acao\":1}"));

        Assert.Equal(System.Net.HttpStatusCode.OK, conhecido.StatusCode);
        foreach (var resposta in new[] { desconhecido, numero })
        {
            Assert.Equal(System.Net.HttpStatusCode.BadRequest, resposta.StatusCode);
            Assert.Equal("application/problem+json", resposta.Content.Headers.ContentType?.MediaType);
            Assert.True(JsonSerializer.Deserialize<JsonElement>(await resposta.Content.ReadAsStringAsync()).TryGetProperty("errors", out _));
        }
    }

    [Fact]
    public void O_nome_do_enum_no_JSON_e_o_mesmo_que_a_consulta_aceita()
    {
        // A mesma regra nos dois lados: o que o front lê no JSON é o que ele manda em ?acao=...
        foreach (var valor in Enum.GetValues<AuditoriaAcao>())
        {
            Assert.Equal($"\"{NomesDoContrato.Enum(valor)}\"", JsonSerializer.Serialize(valor, Opcoes()));
        }

        foreach (var valor in Enum.GetValues<AuditoriaArea>())
        {
            Assert.Equal($"\"{NomesDoContrato.Enum(valor)}\"", JsonSerializer.Serialize(valor, Opcoes()));
        }

        foreach (var valor in Enum.GetValues<Perfil>())
        {
            Assert.Equal($"\"{NomesDoContrato.Enum(valor)}\"", JsonSerializer.Serialize(valor, Opcoes()));
        }
    }

    [Fact]
    public void Numero_como_texto_e_recusado_e_propriedades_saem_em_camelCase()
    {
        // O que a fatia 0 já fixava: números só como número, nomes em camelCase.
        Assert.Equal("{\"totalDeLinhas\":3}", JsonSerializer.Serialize(new { TotalDeLinhas = 3 }, Opcoes()));
        Assert.Throws<JsonException>(() => JsonSerializer.Deserialize<int>("\"3\"", Opcoes()));
    }
}
