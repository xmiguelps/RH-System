using System.Net.Http.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Empresa;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Empresa;
using RHSystem.Infrastructure.Empresa;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Empresa;

/// <summary>
/// <c>GET /api/eu/empresa</c> (qualquer perfil) e <c>PUT /api/rh/empresa</c> (só o RH), no banco REAL com o relógio fixo em 2026-10-07T12:42Z. A tabela tem UMA linha (a chave é sempre 1); cada teste a limpa ou a
/// semeia no começo (os testes da classe rodam um por vez, no banco da classe).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EmpresaTests(MySqlFixture mysql)
{
    private const string Ler = "/api/eu/empresa";
    private const string Salvar = "/api/rh/empresa";

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(EmpresaTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static object Corpo(string nomeEmpresa = "Empresa Teste", string uf = "SP", string municipio = "São Paulo", string horarioEntrada = "08:00", string horarioSaida = "17:00", int tolerancia = 10, int dia = 5) =>
        new { nomeEmpresa, uf, municipio, horarioEntrada, horarioSaida, toleranciaAtrasoMinutos = tolerancia, diaPagamento = dia };

    private static Task<long> LinhasAsync(ApiDeAcesso api) => Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM CONFIGURACOES_EMPRESA");

    [Fact]
    public async Task Sem_configuracao_obter_devolve_404_com_codigo_EMPRESA_NAO_CONFIGURADA_e_o_detail_do_contrato()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);

        var resposta = await ComoRh(api).GetAsync(Ler);

        Assert.Equal((404, "EMPRESA_NAO_CONFIGURADA", "A empresa ainda não foi configurada."), await ProblemaAsync(resposta));
    }

    [Fact]
    public async Task Qualquer_perfil_le_a_empresa_e_a_resposta_traz_os_horarios_em_HH_mm()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.InserirAsync(api.Conexao, nome: "Empresa de leitura", uf: "RJ", municipio: "Niterói", entrada: "08:00:00", saida: "17:30:00", tolerancia: 15, dia: 7);

        foreach (var quem in new[] { api.Elenco.Rh, api.Elenco.Gestor, api.Elenco.MembroA })
        {
            var corpo = await CorpoAsync(await api.Api.ClienteComo(quem).GetAsync(Ler));

            Assert.Equal(
                ("Empresa de leitura", "RJ", "Niterói", "08:00", "17:30", 15, 7),
                (corpo.GetProperty("nomeEmpresa").GetString(), corpo.GetProperty("uf").GetString(), corpo.GetProperty("municipio").GetString(), corpo.GetProperty("horarioEntrada").GetString(), corpo.GetProperty("horarioSaida").GetString(), corpo.GetProperty("toleranciaAtrasoMinutos").GetInt32(), corpo.GetProperty("diaPagamento").GetInt32()));
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().GetAsync(Ler)).StatusCode);
    }

    [Fact]
    public async Task Salvar_cria_e_depois_substitui_a_unica_linha_e_o_RH_e_o_unico_perfil_que_salva()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var cliente = ComoRh(api);

        var primeira = await cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Primeira", municipio: "Santos"));
        Assert.Equal(200, (int)primeira.StatusCode);
        Assert.Equal(1, await LinhasAsync(api));
        var segunda = await CorpoAsync(await cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Segunda", uf: "MG", municipio: "Uberlândia", horarioEntrada: "09:30", tolerancia: 0, dia: 31)));

        Assert.Equal(1, await LinhasAsync(api)); // substituiu: continua uma linha só
        Assert.Equal(("Segunda", "MG", "Uberlândia", "09:30", 0, 31), (segunda.GetProperty("nomeEmpresa").GetString(), segunda.GetProperty("uf").GetString(), segunda.GetProperty("municipio").GetString(), segunda.GetProperty("horarioEntrada").GetString(), segunda.GetProperty("toleranciaAtrasoMinutos").GetInt32(), segunda.GetProperty("diaPagamento").GetInt32()));
        Assert.Equal("Segunda", await Sql.EscalarAsync(api.Conexao, "SELECT NOME_EMPRESA FROM CONFIGURACOES_EMPRESA"));

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await api.Api.ClienteComo(quem).PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Invasora"))).StatusCode);
        }

        Assert.Equal(401, (int)(await api.Api.ClienteAnonimo().PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Invasora"))).StatusCode);
        Assert.Equal("Segunda", await Sql.EscalarAsync(api.Conexao, "SELECT NOME_EMPRESA FROM CONFIGURACOES_EMPRESA")); // nenhuma chamada proibida mexeu
    }

    [Fact]
    public async Task Salvar_valida_cada_campo_com_o_nome_do_campo_no_errors()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var cliente = ComoRh(api);
        var casos = new List<(string Campo, object Corpo)>
        {
            ("uf", Corpo(uf: "sp")),
            ("uf", Corpo(uf: "ZZ")),
            ("toleranciaAtrasoMinutos", Corpo(tolerancia: -1)),
            ("toleranciaAtrasoMinutos", Corpo(tolerancia: 121)),
            ("diaPagamento", Corpo(dia: 0)),
            ("diaPagamento", Corpo(dia: 32)),
            ("nomeEmpresa", Corpo(nomeEmpresa: string.Empty)),
            ("nomeEmpresa", Corpo(nomeEmpresa: new string('n', 151))),
            ("municipio", Corpo(municipio: string.Empty)),
            ("municipio", Corpo(municipio: new string('m', 101))),
        };
        foreach (var horario in new[] { "8:00", "24:00", "08:00:30", "08:60", string.Empty })
        {
            casos.Add(("horarioEntrada", Corpo(horarioEntrada: horario)));
            casos.Add(("horarioSaida", Corpo(horarioSaida: horario)));
        }

        foreach (var (campo, corpo) in casos)
        {
            var erros = await ErrosAsync(await cliente.PutAsJsonAsync(Salvar, corpo));
            Assert.True(erros.TryGetProperty(campo, out _), $"o erro de {campo} não veio no errors: {erros}");
        }

        Assert.Equal(0, await LinhasAsync(api)); // nenhum corpo inválido gravou
    }

    [Fact]
    public async Task Corpo_sem_campo_obrigatorio_do_contrato_devolve_400()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var cliente = ComoRh(api);
        var completo = new Dictionary<string, object> { ["nomeEmpresa"] = "Empresa", ["uf"] = "SP", ["municipio"] = "São Paulo", ["horarioEntrada"] = "08:00", ["horarioSaida"] = "17:00", ["toleranciaAtrasoMinutos"] = 10, ["diaPagamento"] = 5 };

        // toleranciaAtrasoMinutos fica de fora: o System.Text.Json não distingue "ausente" de 0 (Ruling da Task 3), e 0 é um valor válido.
        foreach (var faltando in new[] { "nomeEmpresa", "uf", "municipio", "horarioEntrada", "horarioSaida", "diaPagamento" })
        {
            var corpo = completo.Where(c => c.Key != faltando).ToDictionary(c => c.Key, c => c.Value);
            var erros = await ErrosAsync(await cliente.PutAsJsonAsync(Salvar, corpo));
            Assert.True(erros.TryGetProperty(faltando, out _), $"sem {faltando}: {erros}");
        }

        Assert.Equal(0, await LinhasAsync(api));
    }

    [Fact]
    public async Task Horario_de_saida_menor_que_o_de_entrada_e_aceito()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);

        var resposta = await ComoRh(api).PutAsJsonAsync(Salvar, Corpo(horarioEntrada: "22:00", horarioSaida: "06:00"));

        Assert.Equal(200, (int)resposta.StatusCode); // o turno que atravessa a meia-noite
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("22:00", "06:00"), (corpo.GetProperty("horarioEntrada").GetString(), corpo.GetProperty("horarioSaida").GetString()));
    }

    [Fact]
    public async Task Dois_PUT_simultaneos_com_a_tabela_vazia_terminam_200_e_com_uma_linha()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var cliente = ComoRh(api);

        var respostas = await EmSimultaneoAsync(2, i => cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: $"Simultanea {i}")));

        Assert.All(respostas, r => Assert.Equal(200, (int)r.StatusCode)); // nunca 409, nunca 500
        Assert.Equal(1, await LinhasAsync(api));
    }

    [Fact]
    public async Task Perder_a_corrida_da_primeira_gravacao_refaz_como_atualizacao_sem_erro()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var antes = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "CONFIGURACOES_EMPRESA", 1)).Count;

        // O repositório que, na primeira leitura (vazia), deixa OUTRA chamada criar a linha: o INSERT desta leva a chave única, e o caso de uso tem de refazer como atualização.
        var resultado = await api.NoEscopoComAsync(
            servicos => servicos.AddScoped<IEmpresaRepositorio>(sp => new RepositorioQueAlguemCriouAntes(ActivatorUtilities.CreateInstance<EmpresaRepositorio>(sp), api.Conexao)),
            sp => sp.GetRequiredService<EmpresaCasoDeUso>().SalvarAsync(new EmpresaConfiguracao("Veio depois", "SP", "Campinas", "08:00", "17:00", 5, 10), CancellationToken.None));

        Assert.Equal(("Veio depois", "Campinas"), (resultado.NomeEmpresa, resultado.Municipio));
        Assert.Equal(1, await LinhasAsync(api));
        Assert.Equal(("Veio depois", "Campinas"), (await Sql.EscalarAsync(api.Conexao, "SELECT NOME_EMPRESA FROM CONFIGURACOES_EMPRESA"), await Sql.EscalarAsync(api.Conexao, "SELECT MUNICIPIO FROM CONFIGURACOES_EMPRESA")));
        var evento = Assert.Single((await AuditoriaDeTeste.EventosAsync(api.Conexao, "CONFIGURACOES_EMPRESA", 1)).Skip(antes)); // a linha nasceu por SQL: só a atualização desta chamada foi auditada
        Assert.Equal(AuditoriaAcao.Alterou, evento.Acao);
    }

    [Fact]
    public async Task Salvar_grava_Criou_e_depois_Alterou_na_area_Cadastros_com_antes_e_depois_dos_campos_mudados()
    {
        var api = await ApiAsync();
        await EmpresaDeTeste.LimparAsync(api.Conexao);
        var antes = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "CONFIGURACOES_EMPRESA", 1)).Count; // a auditoria não se apaga: só os eventos novos contam
        var cliente = ComoRh(api);

        await cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Auditada", municipio: "São Paulo", horarioEntrada: "08:00"));
        await cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Auditada", municipio: "Campinas", horarioEntrada: "09:00"));

        var eventos = (await AuditoriaDeTeste.EventosAsync(api.Conexao, "CONFIGURACOES_EMPRESA", 1)).Skip(antes).ToList();
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], eventos.Select(e => e.Acao));
        Assert.All(eventos, e => Assert.Equal((AuditoriaArea.Cadastros, api.Elenco.Rh.FuncionarioId, "Configuração da empresa"), (e.Area, e.FuncionarioAtorId, e.RegistroAfetado)));
        Assert.Equal(["Horário de entrada", "Município"], eventos[1].Mudancas.Select(m => m.Campo).Order());
        Assert.Contains(eventos[1].Mudancas, m => m is { Campo: "Município", ValorAntes: "São Paulo", ValorDepois: "Campinas" });
        Assert.Contains(eventos[1].Mudancas, m => m is { Campo: "Horário de entrada", ValorAntes: "08:00", ValorDepois: "09:00" }); // o TimeOnly sai como HH:mm
    }

    [Fact]
    public async Task Respostas_batem_com_o_schema_do_contrato()
    {
        var api = await ApiAsync();
        var cliente = ComoRh(api);
        await EmpresaDeTeste.LimparAsync(api.Conexao);

        await (await cliente.GetAsync(Ler)).AfirmarConformeAoContratoAsync("obterEmpresa"); // o 404
        await (await cliente.PutAsJsonAsync(Salvar, Corpo(nomeEmpresa: "Schema"))).AfirmarConformeAoContratoAsync("salvarEmpresa");
        await (await cliente.PutAsJsonAsync(Salvar, Corpo(uf: "sp"))).AfirmarConformeAoContratoAsync("salvarEmpresa"); // o 400
        await (await cliente.GetAsync(Ler)).AfirmarConformeAoContratoAsync("obterEmpresa");
    }

    private sealed class RepositorioQueAlguemCriouAntes(IEmpresaRepositorio interno, string conexao) : IEmpresaRepositorio
    {
        private bool _criou;

        public async Task<ConfiguracaoDaEmpresa?> ObterAsync(CancellationToken ct)
        {
            var atual = await interno.ObterAsync(ct);
            if (atual is null && !_criou)
            {
                _criou = true;
                await EmpresaDeTeste.InserirAsync(conexao, nome: "Criada por outra chamada", municipio: "Santos");
            }

            return atual;
        }

        public Task<bool> TentarAdicionarAsync(ConfiguracaoDaEmpresa nova, CancellationToken ct) => interno.TentarAdicionarAsync(nova, ct);

        public Task SalvarAsync(CancellationToken ct) => interno.SalvarAsync(ct);
    }
}
