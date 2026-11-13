using System.Reflection;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Beneficios;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Comunicacao;
using RHSystem.Application.Ferias;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Beneficios.BeneficiosDeTeste;

namespace RHSystem.Api.IntegrationTests.Painel;

/// <summary>Uma porta cujo uso SEMPRE falha (com uma mensagem "secreta" que o log nunca pode levar): para provar que um cartão com defeito não derruba o painel.</summary>
public class LancadorDeFalhas<T> : DispatchProxy
    where T : class
{
    public const string Segredo = "SEGREDO-DA-FALHA";

    protected override object? Invoke(MethodInfo? metodo, object?[]? argumentos) => throw new InvalidOperationException(Segredo);

    public static T Criar() => Create<T, LancadorDeFalhas<T>>();
}

/// <summary>
/// <c>GET /api/eu/painel</c> no banco REAL: os três cartões da gestão (férias, próximo pagamento e benefícios) saem das MESMAS consultas das rotas de origem, só da própria pessoa, e uma fonte que falha deixa o cartão vazio sem derrubar o painel. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília), dia de pagamento 6.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PainelDoFuncionarioTests(MySqlFixture mysql)
{
    private const string Rota = "/api/eu/painel";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await BeneficiosDeTeste.ApiAsync(mysql, nameof(PainelDoFuncionarioTests));
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM PAGAMENTOS_EXCECOES");
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao);
        await Sql.ExecutarAsync(api.Conexao, "UPDATE CONFIGURACOES_EMPRESA SET DIA_PAGAMENTO = 6");
        return api;
    }

    private static async Task<(PessoaDeFerias Bruno, int GestorId)> BrunoAsync(ApiDeAcesso api, string nome = "Bruno do painel")
    {
        var (setor, _, gestorId) = await FeriasDeTeste.SetorComGestorAsync(api, "Painel " + nome);
        var bruno = await FeriasDeTeste.PessoaAsync(api, nome, new DateOnly(2022, 3, 14), setor);
        await FeriasDeTeste.SemearBrunoAsync(api, bruno.FuncionarioId, gestorId, api.Elenco.Rh.FuncionarioId, com340: false);
        return (bruno, gestorId);
    }

    private static async Task<JsonElement> GetAsync(HttpClient cliente, string rota)
    {
        var resposta = await cliente.GetAsync(rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    [Fact]
    public async Task Os_tres_cartoes_sao_iguais_aos_das_rotas_de_origem_e_o_painel_esta_conforme_ao_contrato()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api);
        var vr = await ItemAsync(api, "Vale-refeição painel", opcional: false);
        var adesaoVr = await AdesaoAsync(api, bruno.FuncionarioId, vr);
        var cliente = await api.ClienteAgoraAsync(bruno.UsuarioId);

        var resposta = await cliente.GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterPainelDoFuncionario");
        var painel = await CorpoAsync(resposta);
        var ferias = (await GetAsync(cliente, "/api/eu/ferias")).GetProperty("resumo");
        Assert.Equal((18, "2027-03-13"), (painel.GetProperty("ferias").GetProperty("diasDisponiveis").GetInt32(), painel.GetProperty("ferias").GetProperty("prazoParaTirarSaldo").GetString()));
        Assert.Equal((ferias.GetProperty("diasDisponiveis").GetInt32(), ferias.GetProperty("prazoParaTirarSaldo").GetString()), (painel.GetProperty("ferias").GetProperty("diasDisponiveis").GetInt32(), painel.GetProperty("ferias").GetProperty("prazoParaTirarSaldo").GetString()));
        Assert.Equal((await GetAsync(cliente, "/api/eu/pagamento/proximo")).GetRawText(), painel.GetProperty("proximoPagamento").GetRawText());
        Assert.Equal("2026-11-06", painel.GetProperty("proximoPagamento").GetProperty("pagamento").GetProperty("data").GetString());
        var cartao = Assert.Single(painel.GetProperty("beneficios").EnumerateArray());
        Assert.Equal((adesaoVr, "Vale-refeição painel", JsonValueKind.Null), (cartao.GetProperty("adesaoId").GetInt32(), cartao.GetProperty("nome").GetString()!, cartao.GetProperty("detalhe").ValueKind));
    }

    [Fact]
    public async Task Os_beneficios_seguem_a_ordem_do_catalogo_e_o_detalhe_diz_os_dependentes_aprovados_e_o_trajeto_em_vigor()
    {
        var api = await ApiAsync();
        var pessoa = await FeriasDeTeste.PessoaAsync(api, "Detalhes do painel", new DateOnly(2024, 1, 2));
        var seguro = await ItemAsync(api, "Seguro de vida painel", opcional: false);
        var saude = await ItemAsync(api, "Plano de saúde painel", opcional: false, dependentes: true);
        var vt = await ItemAsync(api, "Vale-transporte painel", opcional: false, trajeto: true);
        var odonto = await ItemAsync(api, "Plano odontológico painel", opcional: false, dependentes: true, trajeto: true);
        var opcional = await ItemAsync(api, "Opcional painel");
        // criadas fora da ordem do catálogo: o painel segue o catálogo (o id do benefício), não o id da adesão
        var adOdonto = await AdesaoAsync(api, pessoa.FuncionarioId, odonto);
        var adVt = await AdesaoAsync(api, pessoa.FuncionarioId, vt);
        var adSaude = await AdesaoAsync(api, pessoa.FuncionarioId, saude);
        var adSeguro = await AdesaoAsync(api, pessoa.FuncionarioId, seguro);
        _ = await AdesaoAsync(api, pessoa.FuncionarioId, opcional, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00"); // só pedida: não é cartão
        await DependenteAsync(api, adSaude, "Helena", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");
        await DependenteAsync(api, adSaude, "Pendente", status: "PENDENTE");
        await DependenteAsync(api, adSaude, "Recusado", status: "RECUSADO", respondidoEm: "2026-01-03 10:00:00");
        await TrajetoAsync(api, adVt, linhas: "Metrô Linha 1 e Linha 2", status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");
        await TrajetoAsync(api, adVt, linhas: "Trajeto pendente que não vale", pedidoEm: "2026-10-01 09:00:00");
        await DependenteAsync(api, adOdonto, "Um", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");
        await DependenteAsync(api, adOdonto, "Dois", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");
        await TrajetoAsync(api, adOdonto, linhas: "Ônibus 175T-10", status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");

        var painel = await GetAsync(await api.ClienteAgoraAsync(pessoa.UsuarioId), Rota);

        Assert.Equal(
            [
                (adSeguro, "Seguro de vida painel", (string?)null),
                (adSaude, "Plano de saúde painel", "Você e 1 dependente"),
                (adVt, "Vale-transporte painel", "Metrô Linha 1 e Linha 2"),
                (adOdonto, "Plano odontológico painel", "Você e 2 dependentes · Ônibus 175T-10"),
            ],
            painel.GetProperty("beneficios").EnumerateArray().Select(b => (b.GetProperty("adesaoId").GetInt32(), b.GetProperty("nome").GetString()!, b.GetProperty("detalhe").GetString())));
    }

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    [InlineData(Perfil.RH)]
    public async Task Todos_os_perfis_veem_so_os_dados_da_propria_pessoa(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = await FeriasDeTeste.PessoaAsync(api, "Painel proprio " + perfil, new DateOnly(2024, 1, 2), perfil: perfil);
        var outra = await FeriasDeTeste.PessoaAsync(api, "Outra do painel " + perfil, new DateOnly(2024, 1, 2));
        var meu = await ItemAsync(api, "Meu beneficio painel " + perfil, opcional: false);
        var dela = await ItemAsync(api, "Beneficio da outra painel " + perfil, opcional: false);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, meu);
        _ = await AdesaoAsync(api, outra.FuncionarioId, dela);

        var painel = await GetAsync(await api.ClienteAgoraAsync(pessoa.UsuarioId), Rota);

        Assert.Equal([adesao], painel.GetProperty("beneficios").EnumerateArray().Select(b => b.GetProperty("adesaoId").GetInt32()));
    }

    [Fact]
    public async Task Pessoa_sem_nada_tem_os_cartoes_vazios_com_200()
    {
        var api = await ApiAsync();
        var pessoa = await FeriasDeTeste.PessoaAsync(api, "Sem nada no painel", new DateOnly(2026, 6, 1));
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM CONFIGURACOES_EMPRESA");

        var painel = await GetAsync(await api.ClienteAgoraAsync(pessoa.UsuarioId), Rota);

        Assert.Equal((0, JsonValueKind.Null), (painel.GetProperty("ferias").GetProperty("diasDisponiveis").GetInt32(), painel.GetProperty("ferias").GetProperty("prazoParaTirarSaldo").ValueKind));
        Assert.Equal((JsonValueKind.Null, JsonValueKind.Null), (painel.GetProperty("proximoPagamento").GetProperty("pagamento").ValueKind, painel.GetProperty("proximoPagamento").GetProperty("competencia").ValueKind));
        Assert.Empty(painel.GetProperty("beneficios").EnumerateArray());
    }

    [Fact]
    public async Task Uma_fonte_que_lanca_deixa_so_o_cartao_dela_vazio_registra_um_Warning_sem_a_mensagem_e_nunca_da_500()
    {
        var api = await ApiAsync();
        var (bruno, _) = await BrunoAsync(api, "Bruno com falhas");
        var vr = await ItemAsync(api, "Vale-refeição falha", opcional: false);
        await AdesaoAsync(api, bruno.FuncionarioId, vr);
        var coletor = new ColetorDeLogs();
        HttpClient ComFalhaEm(Action<IServiceCollection> falha)
        {
            var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
            {
                falha(servicos);
                servicos.AddSingleton<ILoggerProvider>(coletor);
            }));
            var cliente = fabrica.CreateClient();
            return cliente;
        }

        var semBeneficios = ComFalhaEm(s =>
        {
            s.RemoveAll<IConsultaDeAdesoes>();
            s.AddScoped(_ => LancadorDeFalhas<IConsultaDeAdesoes>.Criar());
        });
        semBeneficios.DefaultRequestHeaders.Authorization = (await api.ClienteAgoraAsync(bruno.UsuarioId)).DefaultRequestHeaders.Authorization;
        var semPagamento = ComFalhaEm(s =>
        {
            s.RemoveAll<IProximoPagamento>();
            s.AddScoped(_ => LancadorDeFalhas<IProximoPagamento>.Criar());
        });
        semPagamento.DefaultRequestHeaders.Authorization = semBeneficios.DefaultRequestHeaders.Authorization;
        var semFerias = ComFalhaEm(s =>
        {
            s.RemoveAll<IConsultaDeFerias>();
            s.AddScoped(_ => LancadorDeFalhas<IConsultaDeFerias>.Criar());
        });
        semFerias.DefaultRequestHeaders.Authorization = semBeneficios.DefaultRequestHeaders.Authorization;

        var painelSemBeneficios = await GetAsync(semBeneficios, Rota);
        var painelSemPagamento = await GetAsync(semPagamento, Rota);
        var painelSemFerias = await GetAsync(semFerias, Rota);

        Assert.Empty(painelSemBeneficios.GetProperty("beneficios").EnumerateArray());
        Assert.Equal(18, painelSemBeneficios.GetProperty("ferias").GetProperty("diasDisponiveis").GetInt32());
        Assert.Equal("2026-11-06", painelSemBeneficios.GetProperty("proximoPagamento").GetProperty("pagamento").GetProperty("data").GetString());
        Assert.Equal(JsonValueKind.Null, painelSemPagamento.GetProperty("proximoPagamento").GetProperty("pagamento").ValueKind);
        Assert.Single(painelSemPagamento.GetProperty("beneficios").EnumerateArray());
        Assert.Equal(0, painelSemFerias.GetProperty("ferias").GetProperty("diasDisponiveis").GetInt32());
        Assert.Single(painelSemFerias.GetProperty("beneficios").EnumerateArray());
        var avisos = coletor.Mensagens.Where(m => m.StartsWith("[Warning]", StringComparison.Ordinal) && m.Contains("do painel falhou", StringComparison.Ordinal)).ToList();
        Assert.Equal(3, avisos.Count);
        Assert.All(avisos, a => Assert.DoesNotContain(LancadorDeFalhas<IProximoPagamento>.Segredo, a, StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Contains("benefícios", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Contains("próximo pagamento", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Contains("férias", StringComparison.Ordinal));
    }
}
