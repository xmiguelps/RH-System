using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Ferias.FeriasDeTeste;

namespace RHSystem.Api.IntegrationTests.Ferias;

/// <summary><c>GET /api/gestor/ferias/calendario</c> no banco REAL. Hoje é 2026-10-07T12:42Z (quarta): a janela padrão é de 05/10/2026 a 03/01/2027 (13 semanas).</summary>
[Collection(MySqlCollection.Nome)]
public class CalendarioDeFeriasApiTests(MySqlFixture mysql)
{
    private const string Rota = "/api/gestor/ferias/calendario";

    private Task<ApiDeAcesso> ApiAsync() => FeriasDeTeste.ApiAsync(mysql, nameof(CalendarioDeFeriasApiTests));

    private static List<int> Ids(JsonElement calendario) => [.. calendario.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("feriasId").GetInt32())];

    [Fact]
    public async Task O_quadro_traz_as_tres_cores_so_da_equipe_so_o_que_encosta_na_janela_e_na_ordem_da_data_de_inicio()
    {
        var api = await ApiAsync();
        var rh = api.Elenco.Rh.FuncionarioId;
        var (setor, gestor, gestorId, m1, m2) = await EquipeAsync(api, "Calendario");
        var m3 = await PessoaAsync(api, "Membro 3 do calendario", new DateOnly(2022, 3, 14), setor);
        var (_, _, outroGestorId, deOutroSetor, _) = await EquipeAsync(api, "Calendario alheio");

        var aprovada = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-03", "2026-11-17", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var emGozo = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-10-05", "2026-10-17", "EM_GOZO", gestorId: gestorId, gestorAprovou: true, rhId: rh);
        var comORh = await SemearAsync(api, m3.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-09", "2026-11-23", "SOLICITADA", gestorId: gestorId, gestorAprovou: true);
        var aguardando = await PedidoComOGestorAsync(api, m1.FuncionarioId, gestorId, "2026-11-30", "2026-12-11");
        var concluidaQueTermina = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-09-21", "2026-10-05", "CONCLUIDA", gestorId: gestorId, gestorAprovou: true, rhId: rh); // termina no primeiro dia da janela
        // fora do quadro
        _ = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2026-09-07", "2026-10-04", "CONCLUIDA", gestorId: gestorId, gestorAprovou: true, rhId: rh); // terminou no domingo antes da janela
        _ = await SemearAsync(api, m3.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-04", "2027-01-15", "APROVADA", gestorId: gestorId, gestorAprovou: true, rhId: rh); // começa depois do fim (03/01)
        _ = await SemearAsync(api, m2.FuncionarioId, "2025-03-14", "2026-03-13", "2026-12-14", "2026-12-23", "SOLICITADA", gestorId: gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2026-12-21", sugestaoFim: "2026-12-30"); // espera a pessoa
        _ = await SemearAsync(api, m3.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-23", "2026-12-04", "REJEITADA", gestorId: gestorId, resultadoDoGestor: "RECUSOU", motivo: "Equipe desfalcada.");
        _ = await SemearAsync(api, m1.FuncionarioId, "2025-03-14", "2026-03-13", "2026-12-07", "2026-12-18", "CANCELADA", gestorId: gestorId);
        _ = await SemearAsync(api, deOutroSetor.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-10", "2026-11-24", "APROVADA", gestorId: outroGestorId, gestorAprovou: true, rhId: rh);
        _ = await SemearAsync(api, gestor.FuncionarioId, "2021-01-02", "2022-01-01", "2026-11-16", "2026-11-30", "APROVADA"); // as do próprio gestor não são da equipe

        var resposta = await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterCalendarioDeFeriasDaEquipe");
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("2026-10-05", "2027-01-03", 13), (corpo.GetProperty("inicio").GetString(), corpo.GetProperty("fim").GetString(), corpo.GetProperty("semanas").GetArrayLength()));
        Assert.Equal("2026-10-05", corpo.GetProperty("semanas")[0].GetString());
        Assert.Equal("2026-12-28", corpo.GetProperty("semanas")[12].GetString());
        // na ordem da data de início (a que termina no primeiro dia da janela começou antes de todas)
        Assert.Equal([concluidaQueTermina, emGozo, aprovada, comORh, aguardando], Ids(corpo));
        var cores = corpo.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("feriasId").GetInt32(), i => i.GetProperty("situacao").GetString()!);
        Assert.Equal(("APROVADA", "APROVADA", "APROVADA", "COM_O_RH", "AGUARDANDO_VOCE"), (cores[concluidaQueTermina], cores[emGozo], cores[aprovada], cores[comORh], cores[aguardando]));
        var item = corpo.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("feriasId").GetInt32() == aprovada);
        Assert.Equal((m1.FuncionarioId, "2026-11-03", "2026-11-17"), (item.GetProperty("funcionario").GetProperty("id").GetInt32(), item.GetProperty("dataInicio").GetString(), item.GetProperty("dataFim").GetString()));
    }

    [Fact]
    public async Task O_inicio_vira_a_segunda_da_semana_e_as_semanas_controlam_o_fim()
    {
        var api = await ApiAsync();
        var (_, gestor, _, _, _) = await EquipeAsync(api, "Calendario janela");
        var cliente = await api.ClienteAgoraAsync(gestor.UsuarioId);

        var domingo = await CorpoAsync(await cliente.GetAsync($"{Rota}?inicio=2026-10-11&semanas=1"));
        var vinteESeis = await CorpoAsync(await cliente.GetAsync($"{Rota}?inicio=2026-11-18&semanas=26"));

        Assert.Equal(("2026-10-05", "2026-10-11", 1), (domingo.GetProperty("inicio").GetString(), domingo.GetProperty("fim").GetString(), domingo.GetProperty("semanas").GetArrayLength()));
        Assert.Equal(("2026-11-16", "2027-05-16", 26), (vinteESeis.GetProperty("inicio").GetString(), vinteESeis.GetProperty("fim").GetString(), vinteESeis.GetProperty("semanas").GetArrayLength()));
    }

    [Theory]
    [InlineData("?semanas=0")]
    [InlineData("?semanas=27")]
    [InlineData("?semanas=-1")]
    [InlineData("?inicio=amanha")]
    [InlineData("?inicio=1999-12-27")]
    public async Task Semanas_fora_de_1_a_26_e_inicio_invalido_dao_400(string consulta)
    {
        var api = await ApiAsync();
        var (_, gestor, _, _, _) = await EquipeAsync(api, "Calendario 400");

        var resposta = await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"{Rota}{consulta}");

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterCalendarioDeFeriasDaEquipe");
    }

    [Fact]
    public async Task Gestor_sem_setor_recebe_o_quadro_vazio_e_quem_nao_e_gestor_recebe_403()
    {
        var api = await ApiAsync();

        var semSetor = await CorpoAsync(await (await api.ClienteAgoraAsync(api.Elenco.GestorSemSetor.UsuarioId)).GetAsync(Rota));
        var comum = await (await api.ClienteAgoraAsync(api.Elenco.MembroA.UsuarioId)).GetAsync(Rota);

        Assert.Equal((0, 13), (semSetor.GetProperty("itens").GetArrayLength(), semSetor.GetProperty("semanas").GetArrayLength()));
        Assert.Equal(403, (int)comum.StatusCode);
    }
}
