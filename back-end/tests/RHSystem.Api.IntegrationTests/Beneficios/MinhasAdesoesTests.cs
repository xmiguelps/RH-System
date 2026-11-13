using System.Net.Http.Json;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Beneficios.BeneficiosDeTeste;

namespace RHSystem.Api.IntegrationTests.Beneficios;

/// <summary>
/// <c>GET /api/eu/beneficios</c> e os três pedidos (<c>POST /api/eu/beneficios-adesoes</c>, <c>.../{adesaoId}/dependentes</c> e <c>.../{adesaoId}/trajetos</c>) no banco REAL. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília); 12/10 e 07/09 são feriados nacionais do banco de teste. Cada teste cria as próprias pessoas e
/// os próprios itens de catálogo (o banco é um por classe; as listas de "disponíveis" trazem o catálogo inteiro, por isso os testes olham só os itens deles).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MinhasAdesoesTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => BeneficiosDeTeste.ApiAsync(mysql, nameof(MinhasAdesoesTests));

    private static Task<PessoaDeFerias> NovaAsync(ApiDeAcesso api, string nome = "Pessoa", Perfil perfil = Perfil.Funcionario, bool ativa = true) =>
        FeriasDeTeste.PessoaAsync(api, nome, new DateOnly(2024, 1, 2), perfil: perfil, ativa: ativa);

    private static async Task<HttpResponseMessage> PostAsync(ApiDeAcesso api, PessoaDeFerias pessoa, string rota, object corpo) => await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).PostAsJsonAsync(rota, corpo);

    private static async Task<JsonElement> MinhaTelaAsync(ApiDeAcesso api, PessoaDeFerias pessoa)
    {
        var resposta = await (await api.ClienteAgoraAsync(pessoa.UsuarioId)).GetAsync(RotaEu);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static string Codigo(JsonElement problema) => problema.TryGetProperty("codigo", out var c) ? c.GetString()! : "(sem código)";

    private static string Linha(ApiDeAcesso api, int adesaoId) =>
        TextoAsync(api, $"SELECT CONCAT_WS('|', STATUS_PEDIDO, BENEFICIO_ATIVO, IFNULL(DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s'), '-'), IFNULL(DATE_FORMAT(DATA_INICIO_DESEJADA, '%Y-%m-%d'), '-'), AUTORIZA_DESCONTO_FOLHA, IFNULL(ID_FUNCIONARIO_RH, '-'), IFNULL(DATA_RESPOSTA, '-'), IFNULL(MOTIVO_RECUSA, '-')) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}").GetAwaiter().GetResult()!;

    private static string SituacaoDosFilhos(ApiDeAcesso api, string tabela, string chave, int adesaoId) =>
        TextoAsync(api, $"SELECT IFNULL(GROUP_CONCAT(STATUS_PEDIDO ORDER BY {chave}), '') FROM {tabela} WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}").GetAwaiter().GetResult()!;

    private static long TotalDeEmails(ApiDeAcesso api) => Contar(api, "SELECT COUNT(*) FROM FILA_EMAILS");

    // ---------------------------------------------------------------- obterMeusBeneficios

    [Fact]
    public async Task Meus_beneficios_montam_cartoes_disponiveis_pedidos_e_dependentes_cadastrados()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Marina");
        var vr = await ItemAsync(api, "Vale-refeição T1", opcional: false, descricao: "Crédito no cartão");
        var saude = await ItemAsync(api, "Plano de saúde T1", opcional: false, dependentes: true);
        var vt = await ItemAsync(api, "Vale-transporte T1", opcional: false, trajeto: true);
        var odonto = await ItemAsync(api, "Plano odontológico T1", dependentes: true);
        var creche = await ItemAsync(api, "Auxílio-creche T1", dependentes: true);
        await AdesaoAsync(api, eu.FuncionarioId, vr);
        var adSaude = await AdesaoAsync(api, eu.FuncionarioId, saude);
        var dependente = await DependenteAsync(api, adSaude, status: "APROVADO", pedidoEm: "2026-01-12 10:00:00", respondidoEm: "2026-01-14 11:00:00");
        var adVt = await AdesaoAsync(api, eu.FuncionarioId, vt);
        var trajetoEmVigor = await TrajetoAsync(api, adVt, status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");
        var trajetoNovo = await TrajetoAsync(api, adVt, origem: "Rua Nova, 10", pedidoEm: "2026-10-01 10:00:00");
        var adCreche = await AdesaoAsync(api, eu.FuncionarioId, creche, "RECUSADO", ativo: false, pedidoEm: "2026-09-01 08:00:00", respondidoEm: "2026-09-02 15:00:00", motivo: "Sem vaga neste mês.");

        var resposta = await (await api.ClienteAgoraAsync(eu.UsuarioId)).GetAsync(RotaEu);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterMeusBeneficios");
        var corpo = await CorpoAsync(resposta);

        var cartoes = corpo.GetProperty("beneficios").EnumerateArray().ToList();
        Assert.Equal([vr, saude, vt], cartoes.Select(c => c.GetProperty("beneficio").GetProperty("id").GetInt32()));
        Assert.Equal(("Vale-refeição T1", "Crédito no cartão", false, false, JsonValueKind.Null, false), (
            cartoes[0].GetProperty("beneficio").GetProperty("nome").GetString()!,
            cartoes[0].GetProperty("beneficio").GetProperty("descricao").GetString()!,
            cartoes[0].GetProperty("aceitaDependentes").GetBoolean(),
            cartoes[0].GetProperty("aceitaTrajeto").GetBoolean(),
            cartoes[0].GetProperty("inicioEm").ValueKind,
            cartoes[0].GetProperty("autorizaDescontoEmFolha").GetBoolean()));
        Assert.Equal((true, false), (cartoes[1].GetProperty("aceitaDependentes").GetBoolean(), cartoes[1].GetProperty("aceitaTrajeto").GetBoolean()));
        var dependentes = cartoes[1].GetProperty("dependentes").EnumerateArray().ToList();
        var helena = Assert.Single(dependentes);
        Assert.Equal((dependente, "Helena Lima", "FILHA", "2019-05-21", "APROVADO", "2026-01-12T13:00:00Z", "2026-01-14T14:00:00Z"), (
            helena.GetProperty("id").GetInt32(),
            helena.GetProperty("nome").GetString()!,
            helena.GetProperty("parentesco").GetString()!,
            helena.GetProperty("dataNascimento").GetString()!,
            helena.GetProperty("situacao").GetString()!,
            Utc(helena.GetProperty("pedidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            Utc(helena.GetProperty("respondidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ")));
        Assert.Equal(JsonValueKind.Null, cartoes[1].GetProperty("trajetoAtual").ValueKind);
        Assert.Empty(cartoes[0].GetProperty("dependentes").EnumerateArray());
        var atual = cartoes[2].GetProperty("trajetoAtual");
        Assert.Equal((trajetoEmVigor, "Rua das Acácias, 120, Vila Mariana, São Paulo", "Av. Paulista, 1000, São Paulo", "Ônibus 175T-10 e Metrô Linha 2", "APROVADO"), (
            atual.GetProperty("id").GetInt32(),
            atual.GetProperty("origem").GetString()!,
            atual.GetProperty("destino").GetString()!,
            atual.GetProperty("linhasMeios").GetString()!,
            atual.GetProperty("situacao").GetString()!));

        var meus = new[] { odonto, creche };
        var disponiveis = corpo.GetProperty("disponiveis").EnumerateArray().Where(d => meus.Contains(d.GetProperty("beneficio").GetProperty("id").GetInt32())).ToList();
        Assert.Equal([odonto, creche], disponiveis.Select(d => d.GetProperty("beneficio").GetProperty("id").GetInt32()));
        Assert.Equal((true, false, JsonValueKind.Null), (disponiveis[0].GetProperty("aceitaDependentes").GetBoolean(), disponiveis[0].GetProperty("aceitaTrajeto").GetBoolean(), disponiveis[0].GetProperty("ultimoPedido").ValueKind));
        var ultimo = disponiveis[1].GetProperty("ultimoPedido");
        Assert.Equal(("RECUSADO", "Sem vaga neste mês.", "2026-09-02T18:00:00Z"), (ultimo.GetProperty("situacao").GetString()!, ultimo.GetProperty("motivoRecusa").GetString()!, Utc(ultimo.GetProperty("respondidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ")));

        Assert.Equal(
            [
                ("TRAJETO", trajetoNovo, adVt, "Atualização do trajeto do vale-transporte T1", "PENDENTE", null, "2026-10-08"),
                ("ADESAO", adCreche, adCreche, "Adesão ao auxílio-creche T1", "RECUSADO", "Sem vaga neste mês.", "2026-09-09"),
                ("DEPENDENTE", dependente, adSaude, "Inclusão de dependente no plano de saúde T1", "APROVADO", null, "2026-01-19"),
                ("TRAJETO", trajetoEmVigor, adVt, "Atualização do trajeto do vale-transporte T1", "APROVADO", null, "2025-08-08"),
            ],
            corpo.GetProperty("pedidos").EnumerateArray().Select(p => (
                p.GetProperty("tipo").GetString()!,
                p.GetProperty("id").GetInt32(),
                p.GetProperty("adesaoId").GetInt32(),
                p.GetProperty("titulo").GetString()!,
                p.GetProperty("situacao").GetString()!,
                p.GetProperty("motivoRecusa").GetString(),
                p.GetProperty("respostaPrevistaAte").GetString()!)));

        var cadastrado = Assert.Single(corpo.GetProperty("dependentesCadastrados").EnumerateArray());
        Assert.Equal(("Helena Lima", "FILHA", "2019-05-21"), (cadastrado.GetProperty("nome").GetString()!, cadastrado.GetProperty("parentesco").GetString()!, cadastrado.GetProperty("dataNascimento").GetString()!));
    }

    [Fact]
    public async Task Quem_nao_tem_nada_ve_os_opcionais_do_catalogo_e_as_outras_listas_vazias()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Sem nada");
        var opcional = await ItemAsync(api);
        var direto = await ItemAsync(api, opcional: false);

        var corpo = await MinhaTelaAsync(api, eu);

        var ids = corpo.GetProperty("disponiveis").EnumerateArray().Select(d => d.GetProperty("beneficio").GetProperty("id").GetInt32()).ToList();
        Assert.Contains(opcional, ids);
        Assert.DoesNotContain(direto, ids);
        Assert.Empty(corpo.GetProperty("beneficios").EnumerateArray());
        Assert.Empty(corpo.GetProperty("pedidos").EnumerateArray());
        Assert.Empty(corpo.GetProperty("dependentesCadastrados").EnumerateArray());
    }

    [Fact]
    public async Task Dependente_aprovado_em_dois_beneficios_aparece_uma_vez_e_o_de_outra_pessoa_nunca()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Eu");
        var outra = await NovaAsync(api, "Outra");
        var saude = await ItemAsync(api, dependentes: true, opcional: false);
        var odonto = await ItemAsync(api, dependentes: true, opcional: false);
        var adSaude = await AdesaoAsync(api, eu.FuncionarioId, saude);
        var adOdonto = await AdesaoAsync(api, eu.FuncionarioId, odonto);
        await DependenteAsync(api, adSaude, "Helena Lima", status: "APROVADO", respondidoEm: "2026-01-14 11:00:00");
        await DependenteAsync(api, adOdonto, "HELENA LIMA", "filha", status: "APROVADO", respondidoEm: "2026-01-15 11:00:00");
        await DependenteAsync(api, adOdonto, "Pedro Lima", "FILHO", "2021-09-03", status: "PENDENTE");
        var adDaOutra = await AdesaoAsync(api, outra.FuncionarioId, saude);
        await DependenteAsync(api, adDaOutra, "Segredo da Outra", status: "APROVADO", respondidoEm: "2026-01-14 11:00:00");

        var corpo = await MinhaTelaAsync(api, eu);

        var nomes = corpo.GetProperty("dependentesCadastrados").EnumerateArray().Select(d => d.GetProperty("nome").GetString()).ToList();
        Assert.Equal(["Helena Lima"], nomes);
        Assert.DoesNotContain("Segredo", await (await api.ClienteAgoraAsync(eu.UsuarioId)).GetStringAsync(RotaEu), StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task O_gestor_e_o_RH_usam_a_mesma_rota_para_os_proprios_beneficios(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Chefe", perfil);
        var item = await ItemAsync(api, opcional: false);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item);

        var corpo = await MinhaTelaAsync(api, pessoa);

        Assert.Equal([adesao], corpo.GetProperty("beneficios").EnumerateArray().Select(c => c.GetProperty("adesaoId").GetInt32()));
    }

    [Fact]
    public async Task O_gestor_nao_ve_dependente_nem_trajeto_da_equipe_na_ficha_do_membro()
    {
        var api = await ApiAsync();
        var (_, gestor, _, membro, _) = await FeriasDeTeste.EquipeAsync(api, "Beneficios");
        var saude = await ItemAsync(api, "Plano de saúde equipe", opcional: false, dependentes: true);
        var adesao = await AdesaoAsync(api, membro.FuncionarioId, saude);
        await DependenteAsync(api, adesao, "Criança Secreta", status: "APROVADO", respondidoEm: "2026-01-14 11:00:00");

        var resposta = await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{membro.FuncionarioId}");

        Assert.Equal(200, (int)resposta.StatusCode);
        var texto = await resposta.Content.ReadAsStringAsync();
        Assert.DoesNotContain("Secreta", texto, StringComparison.Ordinal);
        Assert.DoesNotContain("dependente", texto, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("trajeto", texto, StringComparison.OrdinalIgnoreCase);
    }

    // ---------------------------------------------------------------- pedirAdesaoABeneficio

    [Fact]
    public async Task O_pedido_de_adesao_nasce_PENDENTE_e_inativo_com_a_data_em_menos_3_horas_sem_e_mail_e_o_log_sem_o_dependente()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Pedinte");
        var odonto = await ItemAsync(api, "Plano odontológico H", dependentes: true);
        var emails = TotalDeEmails(api);

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(odonto, "2026-11-01", true, [Helena]));

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAdesaoABeneficio");
        var corpo = await CorpoAsync(resposta);
        var adesaoId = corpo.GetProperty("id").GetInt32();
        Assert.Equal(("ADESAO", adesaoId, "Adesão ao plano odontológico H", "PENDENTE", JsonValueKind.Null, "2026-10-07T12:42:00Z", "2026-10-15"), (
            corpo.GetProperty("tipo").GetString()!,
            corpo.GetProperty("adesaoId").GetInt32(),
            corpo.GetProperty("titulo").GetString()!,
            corpo.GetProperty("situacao").GetString()!,
            corpo.GetProperty("motivoRecusa").ValueKind,
            Utc(corpo.GetProperty("pedidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            corpo.GetProperty("respostaPrevistaAte").GetString()!));
        Assert.Equal("PENDENTE|0|2026-10-07 09:42:00|2026-11-01|1|-|-|-", Linha(api, adesaoId));
        Assert.Equal(
            "Helena Lima|FILHA|2019-05-21|PENDENTE|2026-10-07 09:42:00",
            await TextoAsync(api, $"SELECT CONCAT_WS('|', NOME, PARENTESCO, DATE_FORMAT(DATA_NASCIMENTO, '%Y-%m-%d'), STATUS_PEDIDO, DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s')) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}"));
        Assert.Equal(emails, TotalDeEmails(api));

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesaoId));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Criou, "Pediu adesão ao benefício Plano odontológico H."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.Equal(
            [("Situação", null, "Pendente", false), ("Autorização de desconto em folha", null, "Sim", false), ("Início desejado", null, "01/11/2026", false), ("Dependentes", null, null, true)],
            evento.Mudancas.OrderBy(m => m.Ordem).Select(m => (m.Campo, m.ValorAntes, m.ValorDepois, m.ValorOculto)));
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Helena", log, StringComparison.Ordinal);
        Assert.DoesNotContain("2019", log, StringComparison.Ordinal);
        Assert.DoesNotContain("FILHA", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_pedido_sem_dependentes_e_sem_trajeto_e_sem_autorizacao_tambem_vale()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Simples");
        var item = await ItemAsync(api);

        var resposta = await PostAsync(api, eu, RotaDosPedidos, new { beneficioId = item, dataInicioDesejada = "2026-10-07", autorizaDescontoEmFolha = false });

        Assert.Equal(201, (int)resposta.StatusCode);
        var adesaoId = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        Assert.Equal("PENDENTE|0|2026-10-07 09:42:00|2026-10-07|0|-|-|-", Linha(api, adesaoId));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}") + Contar(api, $"SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesaoId));
        Assert.Equal(["Situação", "Autorização de desconto em folha", "Início desejado"], evento.Mudancas.OrderBy(m => m.Ordem).Select(m => m.Campo));
    }

    [Fact]
    public async Task O_pedido_com_trajeto_grava_o_trajeto_pendente_e_o_log_so_diz_que_mudou()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Viajante");
        var vt = await ItemAsync(api, "Vale-transporte H", trajeto: true);

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(vt, trajeto: Trajeto));

        Assert.Equal(201, (int)resposta.StatusCode);
        var adesaoId = (await CorpoAsync(resposta)).GetProperty("id").GetInt32();
        Assert.Equal(
            "Rua das Acácias, 120, Vila Mariana, São Paulo|Av. Paulista, 1000, São Paulo|Metrô Linha 1 e Linha 2|PENDENTE",
            await TextoAsync(api, $"SELECT CONCAT_WS('|', ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}"));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesaoId));
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Trajeto", ValorOculto: true, ValorAntes: null, ValorDepois: null });
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Acácias", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Paulista", log, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData("2026-10-06", 0, "dataInicioDesejada")]
    [InlineData("2101-01-01", 0, "dataInicioDesejada")]
    [InlineData("2026-11-01", 11, "dependentes")]
    public async Task O_corpo_invalido_da_adesao_responde_400_com_o_campo_e_nao_grava(string inicio, int quantos, string campo)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Quatrocentos");
        var item = await ItemAsync(api, dependentes: true);
        var dependentes = Enumerable.Repeat(Helena, quantos).ToArray();

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item, inicio, true, dependentes));

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAdesaoABeneficio");
        Assert.True((await CorpoAsync(resposta)).GetProperty("errors").TryGetProperty(campo, out _));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {eu.FuncionarioId}"));
    }

    [Fact]
    public async Task Os_erros_do_dependente_e_do_trajeto_da_adesao_vem_juntos_com_o_caminho_do_campo()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Varios erros");
        var item = await ItemAsync(api, dependentes: true, trajeto: true);
        var ruim = new { nome = " ", parentesco = "FILHA", dataNascimento = "2027-01-01" };

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item, "2026-10-01", true, [Helena, ruim], new { origem = "", destino = "x", linhasMeios = "y" }));

        Assert.Equal(400, (int)resposta.StatusCode);
        var erros = (await CorpoAsync(resposta)).GetProperty("errors");
        Assert.Equal(["dataInicioDesejada", "dependentes[1].dataNascimento", "dependentes[1].nome", "trajeto.origem"], erros.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal));
    }

    [Fact]
    public async Task Pedir_um_beneficio_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Sem item");

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(999_999));

        Assert.Equal(404, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAdesaoABeneficio");
    }

    [Fact]
    public async Task Os_tres_422_do_catalogo_e_o_da_pessoa_inativa_levam_o_codigo_do_contrato()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Regras");
        var inativa = await NovaAsync(api, "Inativa", ativa: false);
        var direto = await ItemAsync(api, opcional: false);
        var semDependentes = await ItemAsync(api);
        var semTrajeto = await ItemAsync(api, dependentes: true);
        var comum = await ItemAsync(api);

        var naoOpcional = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(direto));
        var dependentes = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(semDependentes, dependentes: [Helena]));
        var trajeto = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(semTrajeto, trajeto: Trajeto));
        var pessoaInativa = await PostAsync(api, inativa, RotaDosPedidos, PedidoDeAdesao(comum));

        foreach (var (resposta, codigo) in new[] { (naoOpcional, "BENEFICIO_NAO_OPCIONAL"), (dependentes, "BENEFICIO_NAO_ACEITA_DEPENDENTES"), (trajeto, "BENEFICIO_NAO_ACEITA_TRAJETO"), (pessoaInativa, "FUNCIONARIO_INATIVO") })
        {
            Assert.Equal(422, (int)resposta.StatusCode);
            await resposta.AfirmarConformeAoContratoAsync("pedirAdesaoABeneficio");
            Assert.Equal(codigo, Codigo(await CorpoAsync(resposta)));
        }

        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO IN ({eu.FuncionarioId}, {inativa.FuncionarioId})"));
    }

    [Theory]
    [InlineData("PENDENTE", false)]
    [InlineData("APROVADO", true)]
    public async Task Quem_ja_tem_o_beneficio_ou_ja_pediu_leva_409(string status, bool ativo)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Ja tem");
        var item = await ItemAsync(api);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, item, status, ativo, pedidoEm: status == "PENDENTE" ? "2026-10-05 10:00:00" : null);
        var antes = Linha(api, adesao);

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item));

        Assert.Equal(409, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAdesaoABeneficio");
        Assert.Equal("BENEFICIO_JA_ATIVO_OU_PENDENTE", Codigo(await CorpoAsync(resposta)));
        Assert.Equal(antes, Linha(api, adesao));
    }

    [Fact]
    public async Task Pedido_recusado_reabre_a_mesma_linha_limpa_a_resposta_cancela_os_filhos_pendentes_e_deixa_a_autorizacao_anterior_so_no_log()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Reabre");
        var item = await ItemAsync(api, dependentes: true, trajeto: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, item, "RECUSADO", ativo: false, pedidoEm: "2026-09-01 08:00:00", inicio: "2026-09-15", autoriza: true, respondidoEm: "2026-09-02 15:00:00", motivo: "Sem vaga neste mês.");
        await DependenteAsync(api, adesao, "Antigo Pendente", status: "PENDENTE");
        await DependenteAsync(api, adesao, "Antigo Recusado", status: "RECUSADO", respondidoEm: "2026-09-02 15:00:00");
        await TrajetoAsync(api, adesao, status: "PENDENTE");

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item, "2026-11-10", autoriza: false, dependentes: [Helena]));

        Assert.Equal(201, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((adesao, adesao, "PENDENTE"), (corpo.GetProperty("id").GetInt32(), corpo.GetProperty("adesaoId").GetInt32(), corpo.GetProperty("situacao").GetString()!));
        Assert.Equal("PENDENTE|0|2026-10-07 09:42:00|2026-11-10|0|-|-|-", Linha(api, adesao));
        Assert.Equal("CANCELADO,RECUSADO,PENDENTE", SituacaoDosFilhos(api, "DEPENDENTES", "ID_DEPENDENTE", adesao));
        Assert.Equal("CANCELADO", SituacaoDosFilhos(api, "TRAJETOS_VALE_TRANSPORTE", "ID_TRAJETO", adesao));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao} AND STATUS_PEDIDO = 'CANCELADO' AND (ID_FUNCIONARIO_RH IS NOT NULL OR DATA_RESPOSTA IS NOT NULL)"));
        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {eu.FuncionarioId} AND ID_BENEFICIO = {item}"));

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesao));
        Assert.Equal((AuditoriaAcao.Alterou, "Pediu adesão ao benefício " + await TextoAsync(api, $"SELECT NOME_BENEFICIO FROM BENEFICIOS WHERE ID_BENEFICIO = {item}") + "."), (evento.Acao, evento.Descricao));
        Assert.Equal(
            [("Situação", "Recusado", "Pendente"), ("Autorização de desconto em folha", "Sim", "Não"), ("Início desejado", "15/09/2026", "10/11/2026")],
            evento.Mudancas.OrderBy(m => m.Ordem).Where(m => !m.ValorOculto).Select(m => (m.Campo, m.ValorAntes!, m.ValorDepois!)));
    }

    [Fact]
    public async Task Pedir_de_novo_o_que_o_RH_tirou_cancela_tambem_o_dependente_e_o_trajeto_aprovados_da_adesao_antiga()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Reabre com aprovados");
        var item = await ItemAsync(api, dependentes: true, trajeto: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, item, "APROVADO", ativo: false); // o RH tirou o benefício: a linha continua aprovada e desativada
        await DependenteAsync(api, adesao, "Antiga Aprovada", status: "APROVADO", respondidoEm: "2026-02-01 09:00:00");
        await TrajetoAsync(api, adesao, status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item, "2026-11-10", autoriza: true, dependentes: [Helena]));

        Assert.Equal(201, (int)resposta.StatusCode);
        // o filho antigo só volta se o RH aprovar de novo: o que não faz parte do pedido novo é cancelado (V001, "ao refazer um pedido na mesma linha")
        Assert.Equal("CANCELADO,PENDENTE", SituacaoDosFilhos(api, "DEPENDENTES", "ID_DEPENDENTE", adesao));
        Assert.Equal("CANCELADO", SituacaoDosFilhos(api, "TRAJETOS_VALE_TRANSPORTE", "ID_TRAJETO", adesao));
        var meus = await CorpoAsync(await (await api.ClienteAgoraAsync(eu.UsuarioId)).GetAsync(RotaEu));
        Assert.Empty(meus.GetProperty("dependentesCadastrados").EnumerateArray());
    }

    [Theory]
    [InlineData("CANCELADO", false, "Cancelado")]
    [InlineData("APROVADO", false, "Aprovado")]
    public async Task Pedido_cancelado_ou_aprovado_que_o_RH_desativou_tambem_reabre_a_mesma_linha(string status, bool ativo, string rotuloAnterior)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Reabre " + status);
        var item = await ItemAsync(api);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, item, status, ativo, pedidoEm: status == "CANCELADO" ? "2026-09-01 08:00:00" : null);

        var resposta = await PostAsync(api, eu, RotaDosPedidos, PedidoDeAdesao(item, "2026-12-01", true));

        Assert.Equal(201, (int)resposta.StatusCode);
        Assert.Equal(adesao, (await CorpoAsync(resposta)).GetProperty("id").GetInt32());
        Assert.Equal("PENDENTE|0|2026-10-07 09:42:00|2026-12-01|1|-|-|-", Linha(api, adesao));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "BENEFICIOS_FUNCIONARIOS", adesao));
        Assert.Equal((rotuloAnterior, "Pendente"), evento.Mudancas.Where(m => m.Campo == "Situação").Select(m => (m.ValorAntes!, m.ValorDepois!)).Single());
        Assert.Equal(("Não", "Sim"), evento.Mudancas.Where(m => m.Campo == "Autorização de desconto em folha").Select(m => (m.ValorAntes!, m.ValorDepois!)).Single());
    }

    [Fact]
    public async Task Dois_pedidos_iguais_ao_mesmo_tempo_dao_um_201_e_um_409()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Clique duplo");
        var item = await ItemAsync(api);
        var cliente = await api.ClienteAgoraAsync(eu.UsuarioId);

        var respostas = await Task.WhenAll(cliente.PostAsJsonAsync(RotaDosPedidos, PedidoDeAdesao(item)), cliente.PostAsJsonAsync(RotaDosPedidos, PedidoDeAdesao(item)));

        Assert.Equal([201, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(1, Contar(api, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {eu.FuncionarioId} AND ID_BENEFICIO = {item}"));
    }

    [Fact]
    public async Task O_gestor_e_o_RH_tambem_pedem_para_si()
    {
        var api = await ApiAsync();
        var gestor = await NovaAsync(api, "Gestor pede", Perfil.Gestor);
        var rh = await NovaAsync(api, "RH pede", Perfil.RH);
        var item = await ItemAsync(api);

        var doGestor = await PostAsync(api, gestor, RotaDosPedidos, PedidoDeAdesao(item));
        var doRh = await PostAsync(api, rh, RotaDosPedidos, PedidoDeAdesao(item));

        Assert.Equal([201, 201], new[] { (int)doGestor.StatusCode, (int)doRh.StatusCode });
        Assert.Equal(2, Contar(api, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO = {item} AND STATUS_PEDIDO = 'PENDENTE'"));
    }

    // ---------------------------------------------------------------- pedirInclusaoDeDependente

    private static string Dependentes(int adesaoId) => $"{RotaDosPedidos}/{adesaoId}/dependentes";

    private static string Trajetos(int adesaoId) => $"{RotaDosPedidos}/{adesaoId}/trajetos";

    [Fact]
    public async Task O_pedido_de_dependente_nasce_PENDENTE_com_o_titulo_do_contrato_sem_e_mail_e_o_log_sem_os_dados()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Mae");
        var saude = await ItemAsync(api, "Plano de saúde D", opcional: false, dependentes: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, saude);
        var emails = TotalDeEmails(api);

        var resposta = await PostAsync(api, eu, Dependentes(adesao), new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" });

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirInclusaoDeDependente");
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(("DEPENDENTE", adesao, "Inclusão de dependente no plano de saúde D", "PENDENTE", "2026-10-07T12:42:00Z", "2026-10-15"), (
            corpo.GetProperty("tipo").GetString()!,
            corpo.GetProperty("adesaoId").GetInt32(),
            corpo.GetProperty("titulo").GetString()!,
            corpo.GetProperty("situacao").GetString()!,
            Utc(corpo.GetProperty("pedidoEmUtc")).ToString("yyyy-MM-ddTHH:mm:ssZ"),
            corpo.GetProperty("respostaPrevistaAte").GetString()!));
        Assert.Equal(
            "Pedro Lima|FILHO|2021-09-03|PENDENTE|2026-10-07 09:42:00",
            await TextoAsync(api, $"SELECT CONCAT_WS('|', NOME, PARENTESCO, DATE_FORMAT(DATA_NASCIMENTO, '%Y-%m-%d'), STATUS_PEDIDO, DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s')) FROM DEPENDENTES WHERE ID_DEPENDENTE = {id}"));
        Assert.Equal(emails, TotalDeEmails(api));

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "DEPENDENTES", id));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Criou, "Pediu a inclusão de um dependente em Plano de saúde D."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.Equal([("Dependente", null, null, true)], evento.Mudancas.Select(m => (m.Campo, m.ValorAntes, m.ValorDepois, m.ValorOculto)));
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Pedro Lima", log, StringComparison.Ordinal);
        Assert.DoesNotContain("2021-09-03", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_pedido_de_dependente_aparece_nos_pedidos_mas_nao_nos_cadastrados_ate_o_RH_aprovar()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Mae 2");
        var saude = await ItemAsync(api, opcional: false, dependentes: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, saude);
        await PostAsync(api, eu, Dependentes(adesao), new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" });

        var corpo = await MinhaTelaAsync(api, eu);

        Assert.Equal(["DEPENDENTE"], corpo.GetProperty("pedidos").EnumerateArray().Select(p => p.GetProperty("tipo").GetString()));
        Assert.Empty(corpo.GetProperty("dependentesCadastrados").EnumerateArray());
        Assert.Empty(corpo.GetProperty("beneficios")[0].GetProperty("dependentes").EnumerateArray());
    }

    [Fact]
    public async Task Pedir_dependente_em_adesao_de_outra_pessoa_ou_que_nao_existe_e_404()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Eu 404");
        var outra = await NovaAsync(api, "Outra 404");
        var saude = await ItemAsync(api, opcional: false, dependentes: true);
        var daOutra = await AdesaoAsync(api, outra.FuncionarioId, saude);
        var corpo = new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" };

        var alheia = await PostAsync(api, eu, Dependentes(daOutra), corpo);
        var inexistente = await PostAsync(api, eu, Dependentes(999_999), corpo);

        Assert.Equal([404, 404], new[] { (int)alheia.StatusCode, (int)inexistente.StatusCode });
        await alheia.AfirmarConformeAoContratoAsync("pedirInclusaoDeDependente");
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {daOutra}"));
    }

    [Theory]
    [InlineData("PENDENTE", false)]
    [InlineData("RECUSADO", false)]
    [InlineData("CANCELADO", false)]
    [InlineData("APROVADO", false)]
    public async Task Pedir_dependente_em_adesao_que_nao_esta_ativa_e_409(string status, bool ativo)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Nao ativa " + status);
        var saude = await ItemAsync(api, dependentes: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, saude, status, ativo, pedidoEm: status == "APROVADO" ? null : "2026-09-01 08:00:00");

        var resposta = await PostAsync(api, eu, Dependentes(adesao), new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" });

        Assert.Equal(409, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirInclusaoDeDependente");
        Assert.Equal("BENEFICIO_ADESAO_NAO_ATIVA", Codigo(await CorpoAsync(resposta)));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao}"));
    }

    [Fact]
    public async Task Pedir_dependente_em_beneficio_que_nao_aceita_dependentes_e_422()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Sem dependentes");
        var vr = await ItemAsync(api, opcional: false, dependentes: false);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, vr);

        var resposta = await PostAsync(api, eu, Dependentes(adesao), new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" });

        Assert.Equal(422, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirInclusaoDeDependente");
        Assert.Equal("BENEFICIO_NAO_ACEITA_DEPENDENTES", Codigo(await CorpoAsync(resposta)));
    }

    [Fact]
    public async Task Pedir_dependente_sendo_pessoa_inativa_com_adesao_ainda_ativa_e_422()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Inativa com adesao");
        var saude = await ItemAsync(api, opcional: false, dependentes: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, saude);
        await AdvertenciasDeTeste.InativarAsync(api, eu.FuncionarioId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET BENEFICIO_ATIVO = TRUE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao}");

        var resposta = await PostAsync(api, eu, Dependentes(adesao), new { nome = "Pedro Lima", parentesco = "FILHO", dataNascimento = "2021-09-03" });

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(resposta)));
    }

    [Theory]
    [InlineData("", "FILHO", "2021-09-03", "nome")]
    [InlineData("Pedro", " ", "2021-09-03", "parentesco")]
    [InlineData("Pedro", "FILHO", "2027-01-01", "dataNascimento")]
    [InlineData("Pedro", "FILHO", "1899-12-31", "dataNascimento")]
    public async Task O_corpo_invalido_do_dependente_responde_400_com_o_campo(string nome, string parentesco, string nascimento, string campo)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Dep 400 " + campo);
        var saude = await ItemAsync(api, opcional: false, dependentes: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, saude);

        var resposta = await PostAsync(api, eu, Dependentes(adesao), new { nome, parentesco, dataNascimento = nascimento });

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirInclusaoDeDependente");
        Assert.Equal([campo], (await CorpoAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao}"));
    }

    // ---------------------------------------------------------------- pedirAtualizacaoDeTrajeto

    [Fact]
    public async Task O_pedido_de_trajeto_nasce_PENDENTE_o_em_vigor_continua_e_o_log_so_diz_que_mudou()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Viaja");
        var vt = await ItemAsync(api, "Vale-transporte T", opcional: false, trajeto: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, vt);
        var emVigor = await TrajetoAsync(api, adesao, status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");
        var emails = TotalDeEmails(api);

        var resposta = await PostAsync(api, eu, Trajetos(adesao), Trajeto);

        Assert.Equal(201, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAtualizacaoDeTrajeto");
        var corpo = await CorpoAsync(resposta);
        var id = corpo.GetProperty("id").GetInt32();
        Assert.Equal(("TRAJETO", adesao, "Atualização do trajeto do vale-transporte T", "PENDENTE", "2026-10-15"), (
            corpo.GetProperty("tipo").GetString()!,
            corpo.GetProperty("adesaoId").GetInt32(),
            corpo.GetProperty("titulo").GetString()!,
            corpo.GetProperty("situacao").GetString()!,
            corpo.GetProperty("respostaPrevistaAte").GetString()!));
        Assert.Equal(
            "Rua das Acácias, 120, Vila Mariana, São Paulo|Av. Paulista, 1000, São Paulo|Metrô Linha 1 e Linha 2|PENDENTE|2026-10-07 09:42:00",
            await TextoAsync(api, $"SELECT CONCAT_WS('|', ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s')) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_TRAJETO = {id}"));
        Assert.Equal(emails, TotalDeEmails(api));

        var tela = await MinhaTelaAsync(api, eu);
        Assert.Equal(emVigor, tela.GetProperty("beneficios")[0].GetProperty("trajetoAtual").GetProperty("id").GetInt32());

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "TRAJETOS_VALE_TRANSPORTE", id));
        Assert.Equal((AuditoriaArea.Beneficios, AuditoriaAcao.Criou, "Pediu a atualização do trajeto de Vale-transporte T."), (evento.Area, evento.Acao, evento.Descricao));
        Assert.Equal([("Trajeto", null, null, true)], evento.Mudancas.Select(m => (m.Campo, m.ValorAntes, m.ValorDepois, m.ValorOculto)));
        var log = (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto;
        Assert.DoesNotContain("Acácias", log, StringComparison.Ordinal);
        Assert.DoesNotContain("Metrô", log, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Pedir_trajeto_em_adesao_de_outra_pessoa_e_404_e_em_adesao_nao_ativa_e_409()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Trajeto erros");
        var outra = await NovaAsync(api, "Trajeto outra");
        var vt = await ItemAsync(api, trajeto: true);
        var daOutra = await AdesaoAsync(api, outra.FuncionarioId, vt, "APROVADO");
        var minhaPendente = await AdesaoAsync(api, eu.FuncionarioId, vt, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 10:00:00");

        var alheia = await PostAsync(api, eu, Trajetos(daOutra), Trajeto);
        var pendente = await PostAsync(api, eu, Trajetos(minhaPendente), Trajeto);

        Assert.Equal(404, (int)alheia.StatusCode);
        Assert.Equal(409, (int)pendente.StatusCode);
        await pendente.AfirmarConformeAoContratoAsync("pedirAtualizacaoDeTrajeto");
        Assert.Equal("BENEFICIO_ADESAO_NAO_ATIVA", Codigo(await CorpoAsync(pendente)));
        Assert.Equal(0, Contar(api, "SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE WHERE STATUS_PEDIDO = 'PENDENTE' AND ORIGEM LIKE 'Rua das Acácias%' AND ID_BENEFICIO_FUNCIONARIOS IN (" + daOutra + ", " + minhaPendente + ")"));
    }

    [Fact]
    public async Task Pedir_trajeto_em_beneficio_sem_trajeto_e_422_e_com_pessoa_inativa_tambem()
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Sem trajeto");
        var inativa = await NovaAsync(api, "Inativa trajeto");
        var vr = await ItemAsync(api, opcional: false, trajeto: false);
        var vt = await ItemAsync(api, opcional: false, trajeto: true);
        var semTrajeto = await AdesaoAsync(api, eu.FuncionarioId, vr);
        var daInativa = await AdesaoAsync(api, inativa.FuncionarioId, vt);
        await AdvertenciasDeTeste.InativarAsync(api, inativa.FuncionarioId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE BENEFICIOS_FUNCIONARIOS SET BENEFICIO_ATIVO = TRUE WHERE ID_BENEFICIO_FUNCIONARIOS = {daInativa}");

        var beneficio = await PostAsync(api, eu, Trajetos(semTrajeto), Trajeto);
        var pessoa = await PostAsync(api, inativa, Trajetos(daInativa), Trajeto);

        Assert.Equal(422, (int)beneficio.StatusCode);
        await beneficio.AfirmarConformeAoContratoAsync("pedirAtualizacaoDeTrajeto");
        Assert.Equal("BENEFICIO_NAO_ACEITA_TRAJETO", Codigo(await CorpoAsync(beneficio)));
        Assert.Equal(422, (int)pessoa.StatusCode);
        Assert.Equal("FUNCIONARIO_INATIVO", Codigo(await CorpoAsync(pessoa)));
    }

    [Theory]
    [InlineData("", "d", "l", "origem")]
    [InlineData("o", " ", "l", "destino")]
    [InlineData("o", "d", "", "linhasMeios")]
    public async Task O_corpo_invalido_do_trajeto_responde_400_com_o_campo(string origem, string destino, string linhas, string campo)
    {
        var api = await ApiAsync();
        var eu = await NovaAsync(api, "Traj 400 " + campo);
        var vt = await ItemAsync(api, opcional: false, trajeto: true);
        var adesao = await AdesaoAsync(api, eu.FuncionarioId, vt);

        var resposta = await PostAsync(api, eu, Trajetos(adesao), new { origem, destino, linhasMeios = linhas });

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("pedirAtualizacaoDeTrajeto");
        Assert.Equal([campo], (await CorpoAsync(resposta)).GetProperty("errors").EnumerateObject().Select(p => p.Name));
        Assert.Equal(0, Contar(api, $"SELECT COUNT(*) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesao}"));
    }
}
