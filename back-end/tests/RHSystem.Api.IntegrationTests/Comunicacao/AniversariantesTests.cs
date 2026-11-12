using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Comunicacao.AdvertenciasDeTeste;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// Os aniversariantes (<c>GET /api/eu/aniversariantes</c>, qualquer perfil), no banco REAL: a empresa inteira, de hoje até 14 dias à frente pelo dia de hoje no horário do Brasil, só pessoas ativas, com a admissão já começada e com data de nascimento; 29/02 comemora em 28/02
/// nos anos que não são bissextos; nunca o ano de nascimento nem a idade. Cada teste cria as próprias pessoas com um nome único e olha só elas (as de outros testes continuam no banco).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AniversariantesTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/aniversariantes";

    private async Task<ApiDeAcesso> ApiAsync(DateTime? agora = null)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AniversariantesTests));
        api.Api.Relogio.Fixar(agora ?? ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<(int Id, string Nome)> PessoaAsync(ApiDeAcesso api, string prefixo, string? nascimento, bool ativo = true, DateOnly? admissao = null)
    {
        var nome = NomeUnico(prefixo);
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, null, ativo, admissao);
        if (nascimento is not null)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_NASCIMENTO = '{nascimento}' WHERE ID_FUNCIONARIO = {id}");
        }

        return (id, nome);
    }

    private static async Task<JsonElement> ListarAsync(ApiDeAcesso api) => await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota));

    /// <summary>Os itens da lista só das pessoas deste teste, como <c>nome|aniversarioEm|diasAte</c>, na ordem em que vieram.</summary>
    private static List<string> Meus(JsonElement lista, params string[] nomes) =>
        [.. lista.GetProperty("itens").EnumerateArray()
            .Where(i => nomes.Contains(i.GetProperty("funcionario").GetProperty("nome").GetString()))
            .Select(i => $"{i.GetProperty("funcionario").GetProperty("nome").GetString()}|{i.GetProperty("aniversarioEm").GetString()}|{i.GetProperty("diasAte").GetInt32()}")];

    [Fact]
    public async Task A_janela_vai_de_hoje_ate_14_dias_a_frente_ordenada_pela_data_e_depois_pelo_nome()
    {
        var api = await ApiAsync(); // hoje é 07/10/2026
        var hoje = await PessoaAsync(api, "Aniv Hoje", "1990-10-07");
        var ultimoDia = await PessoaAsync(api, "Aniv Dia14", "1985-10-21");
        var depois = await PessoaAsync(api, "Aniv Dia15", "1985-10-22");
        var ontem = await PessoaAsync(api, "Aniv Ontem", "1990-10-06");
        var empateB = await PessoaAsync(api, "Zeca Aniv", "1991-10-12");
        var empateA = await PessoaAsync(api, "Abel Aniv", "1992-10-12");

        var lista = await ListarAsync(api);

        Assert.Equal(14, lista.GetProperty("dias").GetInt32());
        Assert.Equal(
            [$"{hoje.Nome}|2026-10-07|0", $"{empateA.Nome}|2026-10-12|5", $"{empateB.Nome}|2026-10-12|5", $"{ultimoDia.Nome}|2026-10-21|14"],
            Meus(lista, hoje.Nome, ultimoDia.Nome, depois.Nome, ontem.Nome, empateA.Nome, empateB.Nome));
    }

    [Fact]
    public async Task O_item_traz_o_resumo_da_pessoa_a_data_e_os_dias_e_nunca_o_ano_de_nascimento_nem_a_idade()
    {
        var api = await ApiAsync();
        var (id, nome) = await PessoaAsync(api, "Aniv Campos", "1977-10-10");

        var lista = await ListarAsync(api);

        var item = lista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == id);
        Assert.Equal(["funcionario", "aniversarioEm", "diasAte"], [.. item.EnumerateObject().Select(p => p.Name)]);
        Assert.Equal((nome, "2026-10-10", 3), (item.GetProperty("funcionario").GetProperty("nome").GetString(), item.GetProperty("aniversarioEm").GetString(), item.GetProperty("diasAte").GetInt32()));
        Assert.DoesNotContain("1977", item.GetRawText(), StringComparison.Ordinal);
        Assert.DoesNotContain("idade", lista.GetRawText(), StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("nascimento", lista.GetRawText(), StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public async Task Quem_nasceu_em_29_02_comemora_em_28_02_nos_anos_que_nao_sao_bissextos_e_a_janela_atravessa_o_mes()
    {
        var api = await ApiAsync(new DateTime(2027, 2, 28, 12, 0, 0, DateTimeKind.Utc));
        var bissexto = await PessoaAsync(api, "Aniv Bissexto", "2000-02-29");
        var primeiroDeMarco = await PessoaAsync(api, "Aniv Marco", "1990-03-01");
        var ontem = await PessoaAsync(api, "Aniv Dia27", "1990-02-27");

        var lista = await ListarAsync(api);

        Assert.Equal([$"{bissexto.Nome}|2027-02-28|0", $"{primeiroDeMarco.Nome}|2027-03-01|1"], Meus(lista, bissexto.Nome, primeiroDeMarco.Nome, ontem.Nome));
    }

    [Fact]
    public async Task Em_25_12_a_janela_atravessa_a_virada_do_ano()
    {
        var api = await ApiAsync(new DateTime(2026, 12, 25, 15, 0, 0, DateTimeKind.Utc));
        var natal = await PessoaAsync(api, "Aniv Natal", "1990-12-25");
        var reveillon = await PessoaAsync(api, "Aniv Reveillon", "1990-12-31");
        var anoNovo = await PessoaAsync(api, "Aniv AnoNovo", "1990-01-05");
        var dia14 = await PessoaAsync(api, "Aniv Janeiro8", "1990-01-08");
        var dia15 = await PessoaAsync(api, "Aniv Janeiro9", "1990-01-09");

        var lista = await ListarAsync(api);

        Assert.Equal(
            [$"{natal.Nome}|2026-12-25|0", $"{reveillon.Nome}|2026-12-31|6", $"{anoNovo.Nome}|2027-01-05|11", $"{dia14.Nome}|2027-01-08|14"],
            Meus(lista, natal.Nome, reveillon.Nome, anoNovo.Nome, dia14.Nome, dia15.Nome));
    }

    [Fact]
    public async Task O_dia_de_hoje_e_o_do_horario_do_Brasil_e_nao_o_do_UTC()
    {
        var api = await ApiAsync(new DateTime(2026, 10, 8, 2, 0, 0, DateTimeKind.Utc)); // ainda é 23:00 de 07/10 no Brasil
        var pessoa = await PessoaAsync(api, "Aniv Fuso", "1990-10-07");

        var lista = await ListarAsync(api);

        Assert.Equal([$"{pessoa.Nome}|2026-10-07|0"], Meus(lista, pessoa.Nome));
    }

    [Fact]
    public async Task Inativo_admissao_que_ainda_nao_comecou_e_sem_data_de_nascimento_ficam_de_fora()
    {
        var api = await ApiAsync();
        var inativo = await PessoaAsync(api, "Aniv Inativo", "1990-10-10", ativo: false);
        var emAdmissao = await PessoaAsync(api, "Aniv EmAdmissao", "1990-10-10", admissao: new DateOnly(2026, 10, 13));
        var semData = await PessoaAsync(api, "Aniv SemData", null);
        var comecaHoje = await PessoaAsync(api, "Aniv ComecaHoje", "1990-10-10", admissao: new DateOnly(2026, 10, 7));

        var lista = await ListarAsync(api);

        Assert.Equal([$"{comecaHoje.Nome}|2026-10-10|3"], Meus(lista, inativo.Nome, emAdmissao.Nome, semData.Nome, comecaHoje.Nome));
    }

    [Fact]
    public async Task Qualquer_perfil_ve_a_empresa_inteira_e_sem_login_e_401_e_a_rota_responde_como_o_contrato_manda()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "Aniv Todos", "1990-10-09");

        foreach (var quem in new[] { api.Elenco.MembroA, api.Elenco.Gestor, api.Elenco.Rh })
        {
            var resposta = await (await api.ClienteAgoraAsync(quem.UsuarioId)).GetAsync(Rota);
            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.Equal([$"{pessoa.Nome}|2026-10-09|2"], Meus(await CorpoAsync(resposta), pessoa.Nome));
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Rota)).StatusCode);
        await (await (await api.ClienteDoRhAgoraAsync()).GetAsync(Rota)).AfirmarConformeAoContratoAsync("listarAniversariantes");
    }
}
