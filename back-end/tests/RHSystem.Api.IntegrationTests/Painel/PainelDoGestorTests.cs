using System.Data.Common;
using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Beneficios;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Formularios;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Ponto;
using RHSystem.Application.Ponto;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Painel;

/// <summary>Conta os comandos SQL que o banco recebe: o painel da equipe tem de fazer o MESMO número de consultas com 3 ou com 30 pessoas.</summary>
public sealed class ContadorDeComandos : DbCommandInterceptor
{
    private int _total;

    public int Total => Volatile.Read(ref _total);

    public override ValueTask<InterceptionResult<DbDataReader>> ReaderExecutingAsync(DbCommand comando, CommandEventData dados, InterceptionResult<DbDataReader> resultado, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _total);
        return base.ReaderExecutingAsync(comando, dados, resultado, ct);
    }

    public override ValueTask<InterceptionResult<int>> NonQueryExecutingAsync(DbCommand comando, CommandEventData dados, InterceptionResult<int> resultado, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _total);
        return base.NonQueryExecutingAsync(comando, dados, resultado, ct);
    }

    public override ValueTask<InterceptionResult<object>> ScalarExecutingAsync(DbCommand comando, CommandEventData dados, InterceptionResult<object> resultado, CancellationToken ct = default)
    {
        Interlocked.Increment(ref _total);
        return base.ScalarExecutingAsync(comando, dados, resultado, ct);
    }
}

/// <summary>
/// <c>GET /api/gestor/painel</c> no banco REAL: a tabela de pessoas e os cartões de "Esperando sua aprovação" da equipe do gestor, cada parte com a MESMA conta da rota de origem, em número constante de consultas e sem dado pessoal. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília); a empresa abre às 08:00 com 10
/// minutos de tolerância.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PainelDoGestorTests(MySqlFixture mysql)
{
    private const string Rota = "/api/gestor/painel";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(PainelDoGestorTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        return api;
    }

    private static async Task<(SetorDeAdvertencia Setor, HttpClient Gestor)> SetorAsync(ApiDeAcesso api) =>
        await NovoSetorDoPainelAsync(api);

    private static async Task<(SetorDeAdvertencia Setor, HttpClient Gestor)> NovoSetorDoPainelAsync(ApiDeAcesso api)
    {
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api, "Setor do painel");
        return (setor, await AdvertenciasDeTeste.ClienteDeAsync(api, setor.GestorFuncionarioId));
    }

    private static async Task<JsonElement> PainelAsync(HttpClient gestor)
    {
        var resposta = await gestor.GetAsync(Rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static JsonElement DaPessoa(JsonElement painel, int id) => painel.GetProperty("pessoas").EnumerateArray().Single(p => p.GetProperty("funcionario").GetProperty("id").GetInt32() == id);

    // ---------------------------------------------------------------- gestor sem setor e equipe vazia

    [Fact]
    public async Task Gestor_sem_setor_recebe_setor_nulo_zero_pessoas_e_as_listas_vazias_e_o_painel_esta_conforme_ao_contrato()
    {
        var api = await ApiAsync();
        var gestor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("Gestor sem setor do painel"), Perfil.Gestor);
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, gestor);

        var resposta = await cliente.GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterPainelDaEquipe");
        var painel = await CorpoAsync(resposta);
        Assert.Equal((JsonValueKind.Null, 0, JsonValueKind.Null), (painel.GetProperty("setor").ValueKind, painel.GetProperty("totalPessoas").GetInt32(), painel.GetProperty("avaliacao").ValueKind));
        Assert.Empty(painel.GetProperty("pessoas").EnumerateArray());
        Assert.Empty(painel.GetProperty("aguardandoAprovacao").GetProperty("ferias").EnumerateArray());
        Assert.Empty(painel.GetProperty("aguardandoAprovacao").GetProperty("ajustesDePonto").EnumerateArray());
    }

    [Fact]
    public async Task Setor_sem_ninguem_alem_do_gestor_tem_total_0_e_a_lista_vazia_mas_o_setor_aparece()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);

        var painel = await PainelAsync(gestor);

        Assert.Equal((setor.SetorId, setor.SetorNome, 0), (painel.GetProperty("setor").GetProperty("id").GetInt32(), painel.GetProperty("setor").GetProperty("nome").GetString(), painel.GetProperty("totalPessoas").GetInt32()));
        Assert.Empty(painel.GetProperty("pessoas").EnumerateArray());
    }

    // ---------------------------------------------------------------- a equipe e a ordem

    [Fact]
    public async Task A_equipe_e_a_de_GET_gestor_equipe_em_ordem_alfabetica_com_desempate_por_id_e_so_ativos_sem_o_gestor()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var zeca = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Zeca do painel", Perfil.Funcionario, setor.SetorId);
        var anaB = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana do painel", Perfil.Funcionario, setor.SetorId);
        var anaA = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana do painel", Perfil.Funcionario, setor.SetorId);
        var bruno = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "bruno do painel", Perfil.Funcionario, setor.SetorId);
        var anaMinuscula = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "ana do painel", Perfil.Funcionario, setor.SetorId); // só a caixa difere: empata com as outras Ana e vai pelo id
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Inativo do painel", Perfil.Funcionario, setor.SetorId, ativo: false);
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "De outro setor do painel", Perfil.Funcionario, (await AdvertenciasDeTeste.NovoSetorAsync(api)).SetorId);

        var painel = await PainelAsync(gestor);
        var equipe = await CorpoAsync(await gestor.GetAsync("/api/gestor/equipe"));

        Assert.Equal(equipe.GetProperty("total").GetInt32(), painel.GetProperty("totalPessoas").GetInt32());
        Assert.Equal(equipe.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("id").GetInt32()).Order(), painel.GetProperty("pessoas").EnumerateArray().Select(p => p.GetProperty("funcionario").GetProperty("id").GetInt32()).Order());
        var ordem = painel.GetProperty("pessoas").EnumerateArray().Select(p => (p.GetProperty("funcionario").GetProperty("nome").GetString()!, p.GetProperty("funcionario").GetProperty("id").GetInt32())).ToList();
        Assert.Equal([("Ana do painel", Math.Min(anaA, anaB)), ("Ana do painel", Math.Max(anaA, anaB)), ("ana do painel", anaMinuscula), ("bruno do painel", bruno), ("Zeca do painel", zeca)], ordem);
    }

    // ---------------------------------------------------------------- as colunas

    [Fact]
    public async Task As_colunas_do_ponto_do_banco_das_ferias_e_das_advertencias_saem_das_fontes_de_cada_uma()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var rh = api.Elenco.Rh.FuncionarioId;
        var trabalhando = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana trabalhando", Perfil.Funcionario, setor.SetorId, admissao: new DateOnly(2022, 3, 14));
        var deFerias = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Bia de ferias", Perfil.Funcionario, setor.SetorId);
        var semRegistro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Caio sem registro", Perfil.Funcionario, setor.SetorId);
        foreach (var id in new[] { trabalhando, deFerias, semRegistro })
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {setor.GestorId} WHERE ID_FUNCIONARIO = {id}");
        }

        await FeriasDeTeste.SemearBrunoAsync(api, trabalhando, setor.GestorId, rh, com340: false); // 18 dias de saldo
        await PontoDeTeste.BaterEmAsync(api.Conexao, trabalhando, new DateOnly(2026, 10, 7), (TipoDeMarcacao.Entrada, "2026-10-07 11:01:00"), (TipoDeMarcacao.Saida, "2026-10-07 12:00:00")); // 08:01 em Brasília
        await PontoDeTeste.NovoLancamentoAsync(api.Conexao, trabalhando, "2026-10-06", "CREDITO", "2.17"); // +130 minutos
        await PontoDeTeste.NovoLancamentoAsync(api.Conexao, semRegistro, "2026-10-06", "DEBITO", "1.50"); // -90 minutos
        await PontoDeTeste.NovasFeriasAsync(api.Conexao, deFerias, "2026-10-05", "2026-10-17", "APROVADA");
        await AdvertenciasDeTeste.SemearAsync(api, semRegistro, "2026-03-10");
        await AdvertenciasDeTeste.SemearAsync(api, semRegistro, "2026-04-10");
        await AdvertenciasDeTeste.SemearAsync(api, semRegistro, "2025-12-20"); // do ano passado: não conta
        await AdvertenciasDeTeste.SemearAsync(api, semRegistro, "2026-05-10", situacao: "CANCELADA", textoContestacao: "Foi um engano.", contestacaoLocal: "2026-05-12 10:00:00"); // cancelada: não conta
        var cliente = gestor;

        var painel = await PainelAsync(cliente);

        var ana = DaPessoa(painel, trabalhando);
        Assert.Equal(("TRABALHANDO", "08:01", JsonValueKind.Null, 130, 18, 0), (ana.GetProperty("pontoHoje").GetProperty("situacaoHoje").GetString()!, ana.GetProperty("pontoHoje").GetProperty("horario").GetString()!, ana.GetProperty("pontoHoje").GetProperty("feriasAte").ValueKind, ana.GetProperty("bancoDeHorasMinutos").GetInt32(), ana.GetProperty("feriasDisponiveisDias").GetInt32(), ana.GetProperty("advertencias").GetInt32()));
        var bia = DaPessoa(painel, deFerias);
        Assert.Equal(("FERIAS", JsonValueKind.Null, "2026-10-17", 0, 0), (bia.GetProperty("pontoHoje").GetProperty("situacaoHoje").GetString()!, bia.GetProperty("pontoHoje").GetProperty("horario").ValueKind, bia.GetProperty("pontoHoje").GetProperty("feriasAte").GetString()!, bia.GetProperty("bancoDeHorasMinutos").GetInt32(), bia.GetProperty("advertencias").GetInt32()));
        var caio = DaPessoa(painel, semRegistro);
        Assert.Equal(("SEM_REGISTRO", JsonValueKind.Null, JsonValueKind.Null, -90, 2), (caio.GetProperty("pontoHoje").GetProperty("situacaoHoje").GetString()!, caio.GetProperty("pontoHoje").GetProperty("horario").ValueKind, caio.GetProperty("pontoHoje").GetProperty("feriasAte").ValueKind, caio.GetProperty("bancoDeHorasMinutos").GetInt32(), caio.GetProperty("advertencias").GetInt32()));
        // as mesmas contas das rotas de origem
        var feriasDaAna = (await CorpoAsync(await cliente.GetAsync($"/api/gestor/equipe/{trabalhando}/ferias"))).GetProperty("resumo").GetProperty("diasDisponiveis").GetInt32();
        Assert.Equal(feriasDaAna, ana.GetProperty("feriasDisponiveisDias").GetInt32());
        var daEquipe = await CorpoAsync(await cliente.GetAsync("/api/gestor/equipe"));
        Assert.Equal(
            daEquipe.GetProperty("itens").EnumerateArray().ToDictionary(i => i.GetProperty("id").GetInt32(), i => i.GetProperty("situacaoHoje").GetString()),
            painel.GetProperty("pessoas").EnumerateArray().ToDictionary(p => p.GetProperty("funcionario").GetProperty("id").GetInt32(), p => p.GetProperty("pontoHoje").GetProperty("situacaoHoje").GetString()));
    }

    // ---------------------------------------------------------------- esperando a aprovação

    [Fact]
    public async Task Esperando_sua_aprovacao_traz_os_itens_de_ferias_e_os_ajustes_de_ponto_que_so_o_gestor_pode_decidir()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var membro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro com pedidos", Perfil.Funcionario, setor.SetorId, admissao: new DateOnly(2022, 3, 14));
        var outro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Outro membro com pedidos", Perfil.Funcionario, setor.SetorId, admissao: new DateOnly(2022, 3, 14));
        foreach (var id in new[] { membro, outro })
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {setor.GestorId} WHERE ID_FUNCIONARIO = {id}");
        }

        var comOGestor = await FeriasDeTeste.PedidoComOGestorAsync(api, membro, setor.GestorId);
        _ = await FeriasDeTeste.SemearAsync(api, outro, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", gestorId: setor.GestorId, gestorAprovou: true); // já com o RH: não é do gestor
        var ajuste = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, membro, new DateOnly(2026, 10, 5), setor.GestorId);
        _ = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, outro, new DateOnly(2026, 10, 6), gestorId: null); // etapa do RH: não é do gestor
        _ = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, outro, new DateOnly(2026, 10, 2), setor.GestorId, gestorJaAprovou: true); // o gestor já aprovou

        var painel = await PainelAsync(gestor);

        var daLista = await CorpoAsync(await gestor.GetAsync("/api/gestor/ferias?tamanhoPagina=100"));
        var itens = painel.GetProperty("aguardandoAprovacao").GetProperty("ferias").EnumerateArray().ToList();
        Assert.Equal([comOGestor], itens.Select(i => i.GetProperty("id").GetInt32()));
        Assert.Equal(daLista.GetProperty("itens").EnumerateArray().Select(i => i.GetRawText()), itens.Select(i => i.GetRawText())); // os mesmos itens, byte a byte
        var daFilaDoPonto = await CorpoAsync(await gestor.GetAsync("/api/gestor/ponto/pedidos-de-ajuste?etapa=GESTOR&status=PENDENTE&tamanhoPagina=100"));
        var pedido = Assert.Single(painel.GetProperty("aguardandoAprovacao").GetProperty("ajustesDePonto").EnumerateArray());
        var original = daFilaDoPonto.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == ajuste);
        Assert.Equal(
            (ajuste, membro, "2026-10-05", original.GetProperty("resumoDaCorrecao").GetString(), "Esqueci de bater a saída", original.GetProperty("criadoEmUtc").GetString()),
            (pedido.GetProperty("pedidoId").GetInt32(), pedido.GetProperty("funcionario").GetProperty("id").GetInt32(), pedido.GetProperty("data").GetString(), pedido.GetProperty("resumoDaCorrecao").GetString(), pedido.GetProperty("justificativa").GetString(), pedido.GetProperty("criadoEmUtc").GetString()));
    }

    // ---------------------------------------------------------------- avaliação

    [Fact]
    public async Task Sem_formulario_de_avaliacao_aberto_o_botao_e_a_coluna_somem()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var membro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro sem avaliacao", Perfil.Funcionario, setor.SetorId);

        var painel = await PainelAsync(gestor);

        Assert.Equal(JsonValueKind.Null, painel.GetProperty("avaliacao").ValueKind);
        Assert.Equal(JsonValueKind.Null, DaPessoa(painel, membro).GetProperty("avaliacaoSituacao").ValueKind);
    }

    [Fact]
    public async Task A_avaliacao_aberta_da_o_ciclo_do_titulo_e_a_situacao_de_cada_pessoa_RESPONDIDA_PENDENTE_ou_NAO_SE_APLICA()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var respondida = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana respondida", Perfil.Funcionario, setor.SetorId);
        var pendente = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Bia pendente", Perfil.Funcionario, setor.SetorId);
        var deFerias = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Caio de ferias", Perfil.Funcionario, setor.SetorId);
        await PontoDeTeste.NovasFeriasAsync(api.Conexao, deFerias, "2026-10-05", "2026-10-17", "APROVADA");
        var titulo = $"Zpainel{FormulariosDeTeste.Titulo("A")} Avaliação da equipe, ciclo 2026.2"; // o número único vai ANTES da palavra ciclo
        var formulario = await FormulariosDeTeste.SemearAsync(api, titulo, "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE", prazo: "2026-12-15");
        var pergunta = await FormulariosDeTeste.PerguntaAsync(api, formulario, "Nota");
        await RespostasDeTeste.EscolherPessoasAsync(api, formulario, setor.GestorFuncionarioId);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {formulario}, {setor.GestorFuncionarioId}, {respondida}, 'Boa', '2026-10-06 10:00:00', 0)");

        var painel = await PainelAsync(gestor);

        Assert.Equal(("2026.2", formulario), (painel.GetProperty("avaliacao").GetProperty("ciclo").GetString()!, painel.GetProperty("avaliacao").GetProperty("formularioId").GetInt32()));
        Assert.Equal(
            ("RESPONDIDA", "PENDENTE", "NAO_SE_APLICA"),
            (DaPessoa(painel, respondida).GetProperty("avaliacaoSituacao").GetString()!, DaPessoa(painel, pendente).GetProperty("avaliacaoSituacao").GetString()!, DaPessoa(painel, deFerias).GetProperty("avaliacaoSituacao").GetString()!));
        var cartao = (await CorpoAsync(await gestor.GetAsync("/api/eu/formularios?busca=Zpainel"))).GetProperty("itens").EnumerateArray().Single();
        Assert.Equal((formulario, "PARA_RESPONDER"), (cartao.GetProperty("id").GetInt32(), cartao.GetProperty("situacao").GetString()!)); // o mesmo item "Sua parte" de GET /api/eu/formularios
    }

    [Fact]
    public async Task Titulo_sem_a_palavra_ciclo_vira_o_ciclo_inteiro_e_com_dois_abertos_vale_o_de_prazo_mais_proximo()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro dos ciclos", Perfil.Funcionario, setor.SetorId);
        var longe = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("Avaliação de fim de ano, ciclo 2027.1"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE", prazo: "2027-06-30");
        var perto = await FormulariosDeTeste.SemearAsync(api, "Avaliação do semestre " + longe, "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE", prazo: "2026-11-30");
        var semPrazo = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("Avaliação sem prazo, ciclo 2025.2"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        foreach (var id in new[] { longe, perto, semPrazo })
        {
            await FormulariosDeTeste.PerguntaAsync(api, id, "Nota");
            await RespostasDeTeste.EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        }

        var painel = await PainelAsync(gestor);

        Assert.Equal((perto, "Avaliação do semestre " + longe), (painel.GetProperty("avaliacao").GetProperty("formularioId").GetInt32(), painel.GetProperty("avaliacao").GetProperty("ciclo").GetString()!));
    }

    [Fact]
    public async Task Titulo_que_comeca_com_a_palavra_ciclo_leva_so_o_que_vem_depois_dela()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro do ciclo no inicio", Perfil.Funcionario, setor.SetorId);
        var unico = FormulariosDeTeste.Titulo("2028.1");
        var formulario = await FormulariosDeTeste.SemearAsync(api, $"Ciclo {unico}", "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        await FormulariosDeTeste.PerguntaAsync(api, formulario, "Nota");
        await RespostasDeTeste.EscolherPessoasAsync(api, formulario, setor.GestorFuncionarioId);

        var painel = await PainelAsync(gestor);

        Assert.Equal((formulario, unico), (painel.GetProperty("avaliacao").GetProperty("formularioId").GetInt32(), painel.GetProperty("avaliacao").GetProperty("ciclo").GetString()!));
    }

    [Fact]
    public async Task So_a_avaliacao_publicada_em_que_o_gestor_avalia_a_equipe_vira_o_botao_mesmo_com_prazo_mais_perto_em_outras()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        _ = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro dos sentidos", Perfil.Funcionario, setor.SetorId);
        var certa = await FormulariosDeTeste.SemearAsync(api, $"{FormulariosDeTeste.Titulo("Avaliação da equipe")}, ciclo 2027.1", "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE", prazo: "2027-01-31");
        var deOutroSentido = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("A equipe avalia o gestor"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "EQUIPE_AVALIA_GESTOR", prazo: "2026-10-20");
        var encerrada = await FormulariosDeTeste.SemearAsync(api, $"{FormulariosDeTeste.Titulo("Avaliação encerrada")}, ciclo 2025.1", "ENCERRADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE", prazo: "2026-10-10");
        foreach (var id in new[] { certa, deOutroSentido, encerrada })
        {
            await FormulariosDeTeste.PerguntaAsync(api, id, "Nota");
            await RespostasDeTeste.EscolherPessoasAsync(api, id, setor.GestorFuncionarioId);
        }

        var painel = await PainelAsync(gestor);

        Assert.Equal((certa, "2027.1"), (painel.GetProperty("avaliacao").GetProperty("formularioId").GetInt32(), painel.GetProperty("avaliacao").GetProperty("ciclo").GetString()!));
    }

    // ---------------------------------------------------------------- privacidade, consultas e falhas

    [Fact]
    public async Task Nenhum_cpf_endereco_dependente_ou_trajeto_sai_em_campo_nenhum_do_painel()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var membro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro privado", Perfil.Funcionario, setor.SetorId);
        var saude = await BeneficiosDeTeste.ItemAsync(api, "Plano de saúde do painel privado", opcional: false, dependentes: true, trajeto: true);
        var adesao = await BeneficiosDeTeste.AdesaoAsync(api, membro, saude);
        await BeneficiosDeTeste.DependenteAsync(api, adesao, "Zyx Dependente Secreto", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");
        await BeneficiosDeTeste.TrajetoAsync(api, adesao, origem: "Zyx Rua Secreta, 1", status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");

        var texto = await (await gestor.GetAsync(Rota)).Content.ReadAsStringAsync();

        foreach (var proibido in new[] { "cpf", "endereco", "dependente", "trajeto", "origem", "Zyx", "telefone", "email", "holerite" })
        {
            Assert.DoesNotContain(proibido, texto, StringComparison.OrdinalIgnoreCase);
        }
    }

    [Fact]
    public async Task O_numero_de_consultas_ao_banco_e_o_mesmo_com_3_e_com_30_pessoas_na_equipe()
    {
        var api = await ApiAsync();
        var contador = new ContadorDeComandos();
        var (pequeno, gestorPequeno) = await SetorAsync(api);
        var (grande, gestorGrande) = await SetorAsync(api);
        foreach (var i in Enumerable.Range(0, 3))
        {
            await ApoioDeTeste.CriarPessoaAsync(api.Conexao, $"Pequena {i}", Perfil.Funcionario, pequeno.SetorId);
        }

        foreach (var i in Enumerable.Range(0, 30))
        {
            var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, $"Grande {i}", Perfil.Funcionario, grande.SetorId);
            await PontoDeTeste.NovoLancamentoAsync(api.Conexao, id, "2026-10-06", "CREDITO", "1.00");
        }

        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(servicos => servicos.ConfigureDbContext<AppDbContext>(o => o.AddInterceptors(contador))));
        HttpClient Cliente(HttpClient origem)
        {
            var c = fabrica.CreateClient();
            c.DefaultRequestHeaders.Authorization = origem.DefaultRequestHeaders.Authorization;
            return c;
        }

        var pequena = Cliente(gestorPequeno);
        var grandeCliente = Cliente(gestorGrande);
        _ = await PainelAsync(pequena); // aquece (a primeira chamada abre conexões e monta o modelo)
        var antesPequena = contador.Total;
        var painelPequeno = await PainelAsync(pequena);
        var comandosPequena = contador.Total - antesPequena;
        var antesGrande = contador.Total;
        var painelGrande = await PainelAsync(grandeCliente);
        var comandosGrande = contador.Total - antesGrande;

        Assert.Equal((3, 30), (painelPequeno.GetProperty("pessoas").GetArrayLength(), painelGrande.GetProperty("pessoas").GetArrayLength()));
        Assert.True(comandosPequena > 0);
        Assert.Equal(comandosPequena, comandosGrande);
    }

    [Fact]
    public async Task Uma_parte_que_lanca_sai_vazia_so_ela_registra_Warning_sem_a_mensagem_e_nunca_da_500()
    {
        var api = await ApiAsync();
        var (setor, gestor) = await SetorAsync(api);
        var membro = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Membro com falha", Perfil.Funcionario, setor.SetorId);
        await PontoDeTeste.NovoLancamentoAsync(api.Conexao, membro, "2026-10-06", "CREDITO", "1.00");
        var coletor = new ColetorDeLogs();
        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<ISaldoDoBancoDeHoras>();
            servicos.AddScoped(_ => LancadorDeFalhas<ISaldoDoBancoDeHoras>.Criar());
            servicos.AddSingleton<ILoggerProvider>(coletor);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = gestor.DefaultRequestHeaders.Authorization;

        var painel = await PainelAsync(cliente);

        var pessoa = DaPessoa(painel, membro);
        Assert.Equal(0, pessoa.GetProperty("bancoDeHorasMinutos").GetInt32()); // a parte do banco de horas saiu vazia (zero)
        Assert.Equal(1, painel.GetProperty("totalPessoas").GetInt32());
        var avisos = coletor.Mensagens.Where(m => m.StartsWith("[Warning]", StringComparison.Ordinal) && m.Contains("do painel da equipe falhou", StringComparison.Ordinal)).ToList();
        var aviso = Assert.Single(avisos);
        Assert.Contains("banco de horas", aviso, StringComparison.Ordinal);
        Assert.DoesNotContain(LancadorDeFalhas<ISaldoDoBancoDeHoras>.Segredo, aviso, StringComparison.Ordinal);
    }

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.RH)]
    public async Task So_o_gestor_ve_o_painel_da_equipe(Perfil perfil)
    {
        var api = await ApiAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("Sem permissao do painel " + perfil), perfil);
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, pessoa);

        Assert.Equal(403, (int)(await cliente.GetAsync(Rota)).StatusCode);
    }
}
