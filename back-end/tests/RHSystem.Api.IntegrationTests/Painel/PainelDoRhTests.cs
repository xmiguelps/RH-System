using System.Text.Json;
using Microsoft.AspNetCore.TestHost;
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
using RHSystem.Api.IntegrationTests.Treinamentos;
using RHSystem.Application.Painel;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;

namespace RHSystem.Api.IntegrationTests.Painel;

/// <summary>
/// <c>GET /api/rh/painel</c>, <c>/api/rh/painel/pendencias</c> e <c>/api/rh/painel/presenca/sem-registro</c> no banco REAL. Cada teste tem o PRÓPRIO banco (as contas de pendência e de presença são da empresa inteira e não podem depender dos outros testes). Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília); a empresa abre às 08:00 com 10 minutos
/// de tolerância; 12/10 e 07/09 são feriados nacionais do banco de teste.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PainelDoRhTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/painel";

    private async Task<ApiDeAcesso> ApiAsync(string teste, bool empresa = true)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, $"{nameof(PainelDoRhTests)}-{teste}");
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        if (empresa)
        {
            await PontoDeTeste.ConfigurarEmpresaAsync(api.Conexao, entrada: new TimeOnly(8, 0), tolerancia: 10);
        }

        return api;
    }

    private static async Task<JsonElement> GetAsync(ApiDeAcesso api, string rota)
    {
        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync(rota);
        Assert.Equal(200, (int)resposta.StatusCode);
        return await CorpoAsync(resposta);
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setor = null, bool ativo = true, DateOnly? admissao = null) =>
        ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico(nome), Perfil.Funcionario, setor, ativo, admissao);

    private static string Texto(JsonElement e, string campo) => e.GetProperty(campo).GetString()!;

    // ---------------------------------------------------------------- pendências

    [Fact]
    public async Task Sem_nenhuma_pendencia_o_painel_vem_zerado_e_conforme_ao_contrato()
    {
        var api = await ApiAsync(nameof(Sem_nenhuma_pendencia_o_painel_vem_zerado_e_conforme_ao_contrato));
        var rh = await api.ClienteDoRhAgoraAsync();

        var resposta = await rh.GetAsync(Rota);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterPainelDoRh");
        var painel = await CorpoAsync(resposta);
        Assert.Equal(0, painel.GetProperty("totalPendencias").GetInt32());
        Assert.Empty(painel.GetProperty("pendencias").EnumerateArray());
        Assert.Empty(painel.GetProperty("treinamentosEmAndamento").EnumerateArray());
        Assert.Equal("2026-10-07T12:42:00Z", painel.GetProperty("presencaAgora").GetProperty("atualizadoEmUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    [Fact]
    public async Task As_cinco_filas_somam_so_os_tipos_com_quantidade_entram_e_a_mesma_conta_vale_na_lista_e_no_contador_do_menu()
    {
        var api = await ApiAsync(nameof(As_cinco_filas_somam_so_os_tipos_com_quantidade_entram_e_a_mesma_conta_vale_na_lista_e_no_contador_do_menu));
        var rhId = api.Elenco.Rh.FuncionarioId;
        var (setor, _, gestorId) = await FeriasDeTeste.SetorComGestorAsync(api, "Pendencias");
        var bruno = await FeriasDeTeste.PessoaAsync(api, "Bruno das pendencias", new DateOnly(2022, 3, 14), setor);
        var carla = await FeriasDeTeste.PessoaAsync(api, "Carla das pendencias", new DateOnly(2022, 3, 14), setor);

        // ajuste de ponto: dois na etapa do RH (um sem gestor, um que o gestor já aprovou) e um ainda com o gestor (não conta)
        var ajusteMaisAntigo = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, bruno.FuncionarioId, new DateOnly(2026, 10, 2), gestorId: null);
        var ajusteDoGestor = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, carla.FuncionarioId, new DateOnly(2026, 10, 5), gestorId, gestorJaAprovou: true);
        _ = await PontoDeTeste.NovoPedidoPendenteAsync(api.Conexao, carla.FuncionarioId, new DateOnly(2026, 10, 6), gestorId); // com o gestor: não é do RH
        // férias: uma COM_O_RH (aprovada pelo gestor), uma sem etapa do gestor e uma ainda com o gestor (não conta)
        var feriasAprovadas = await FeriasDeTeste.SemearAsync(api, bruno.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-09", "2026-11-23", "SOLICITADA", "2026-09-29 09:00:00", gestorId, gestorAprovou: true);
        _ = await FeriasDeTeste.SemearAsync(api, carla.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", "2026-10-03 09:00:00");
        _ = await FeriasDeTeste.PedidoComOGestorAsync(api, carla.FuncionarioId, gestorId, "2027-02-08", "2027-02-22");
        // atestados enviados (e um já validado, que não conta)
        await PontoDeTeste.NovoAtestadoAsync(api.Conexao, bruno.FuncionarioId, "2026-10-06", "2026-10-06", "ENVIADO", rhId);
        await PontoDeTeste.NovoAtestadoAsync(api.Conexao, carla.FuncionarioId, "2026-10-01", "2026-10-02", "ENVIADO", rhId);
        await PontoDeTeste.NovoAtestadoAsync(api.Conexao, carla.FuncionarioId, "2026-09-01", "2026-09-02", "VALIDADO", rhId);
        // admissão: começa em até 7 dias, com o formulário de admissão atribuído e sem resposta
        var admissao = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("Admissão do painel"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        await FormulariosDeTeste.PerguntaAsync(api, admissao, "Dados");
        var rafael = await PessoaAsync(api, "Rafael em admissao", admissao: new DateOnly(2026, 10, 13));
        var tarde = await PessoaAsync(api, "Tiago em admissao longe", admissao: new DateOnly(2026, 10, 19)); // começa depois de 7 dias: ainda não conta
        var semFormulario = await PessoaAsync(api, "Sem formulario", admissao: new DateOnly(2026, 10, 10));
        await RespostasDeTeste.EscolherPessoasAsync(api, admissao, rafael, tarde);
        // benefícios: adesão pendente, adesão aprovada com dependente pendente e a de quem foi inativado (não conta)
        var item = await BeneficiosDeTeste.ItemAsync(api, "Plano odontológico do painel", dependentes: true);
        var adesaoPendente = await BeneficiosDeTeste.AdesaoAsync(api, bruno.FuncionarioId, item, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00");
        var item2 = await BeneficiosDeTeste.ItemAsync(api, "Plano de saúde do painel", opcional: false, dependentes: true);
        var adesaoComFilho = await BeneficiosDeTeste.AdesaoAsync(api, carla.FuncionarioId, item2);
        await BeneficiosDeTeste.DependenteAsync(api, adesaoComFilho, "Zyx Filho", pedidoEm: "2026-10-04 09:00:00");
        var inativa = await FeriasDeTeste.PessoaAsync(api, "Inativa das pendencias", new DateOnly(2022, 3, 14), setor);
        var item3 = await BeneficiosDeTeste.ItemAsync(api, "Creche do painel");
        _ = await BeneficiosDeTeste.AdesaoAsync(api, inativa.FuncionarioId, item3, "PENDENTE", ativo: false, pedidoEm: "2026-10-01 08:00:00");
        await AdvertenciasDeTeste.InativarAsync(api, inativa.FuncionarioId);

        var painel = await GetAsync(api, Rota);

        Assert.Equal(
            [("AJUSTE_DE_PONTO", 2), ("FERIAS", 2), ("ATESTADO", 2), ("ADMISSAO_COM_DOCUMENTOS_PENDENTES", 1), ("BENEFICIOS", 2)],
            painel.GetProperty("pendencias").EnumerateArray().Select(p => (Texto(p, "tipo"), p.GetProperty("quantidade").GetInt32())));
        Assert.Equal(9, painel.GetProperty("totalPendencias").GetInt32());
        var cartoes = painel.GetProperty("pendencias").EnumerateArray().ToDictionary(p => Texto(p, "tipo"));
        Assert.Equal((bruno.FuncionarioId, ajusteMaisAntigo), (cartoes["AJUSTE_DE_PONTO"].GetProperty("funcionarioId").GetInt32(), cartoes["AJUSTE_DE_PONTO"].GetProperty("referenciaId").GetInt32()));
        Assert.Equal((bruno.FuncionarioId, feriasAprovadas), (cartoes["FERIAS"].GetProperty("funcionarioId").GetInt32(), cartoes["FERIAS"].GetProperty("referenciaId").GetInt32()));
        Assert.Equal((rafael, rafael), (cartoes["ADMISSAO_COM_DOCUMENTOS_PENDENTES"].GetProperty("funcionarioId").GetInt32(), cartoes["ADMISSAO_COM_DOCUMENTOS_PENDENTES"].GetProperty("referenciaId").GetInt32()));
        Assert.Equal(adesaoComFilho, cartoes["BENEFICIOS"].GetProperty("referenciaId").GetInt32()); // o filho pendente (04/10) é mais antigo que a adesão pendente (05/10)
        Assert.Contains("começa em 13/10 e ainda não enviou o formulário de admissão", Texto(cartoes["ADMISSAO_COM_DOCUMENTOS_PENDENTES"], "resumo"), StringComparison.Ordinal);
        Assert.StartsWith("O mais antigo é de ", Texto(cartoes["FERIAS"], "resumo"), StringComparison.Ordinal);

        // a lista uma a uma: as mesmas nove, do mais antigo ao mais novo
        var lista = await GetAsync(api, $"{Rota}/pendencias?tamanhoPagina=100");
        Assert.Equal(9, lista.GetProperty("total").GetInt32());
        var itens = lista.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(itens.Select(i => i.GetProperty("desdeEmUtc").GetDateTime()).OrderBy(d => d), itens.Select(i => i.GetProperty("desdeEmUtc").GetDateTime()));
        Assert.Equal(painel.GetProperty("pendencias").EnumerateArray().ToDictionary(p => Texto(p, "tipo"), p => p.GetProperty("quantidade").GetInt32()), itens.GroupBy(i => Texto(i, "tipo")).ToDictionary(g => g.Key, g => g.Count()));
        var daAdmissao = itens.Single(i => Texto(i, "tipo") == "ADMISSAO_COM_DOCUMENTOS_PENDENTES");
        Assert.Equal(("2026-10-13T03:00:00Z", "Começa em 13/10 e ainda não enviou o formulário de admissão"), (daAdmissao.GetProperty("desdeEmUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"), Texto(daAdmissao, "descricao")));
        Assert.Equal(
            ["Atestado de 01/10 a 02/10", "Atestado de 06/10"],
            itens.Where(i => Texto(i, "tipo") == "ATESTADO").Select(i => Texto(i, "descricao")).Order(StringComparer.Ordinal));
        Assert.DoesNotContain(itens, i => i.GetProperty("funcionario").GetProperty("id").GetInt32() == tarde || i.GetProperty("funcionario").GetProperty("id").GetInt32() == semFormulario || i.GetProperty("funcionario").GetProperty("id").GetInt32() == inativa.FuncionarioId);
        Assert.Contains(itens, i => Texto(i, "tipo") == "BENEFICIOS" && i.GetProperty("referenciaId").GetInt32() == adesaoPendente && Texto(i, "descricao") == "Adesão ao plano odontológico do painel");
        Assert.Contains(itens, i => Texto(i, "tipo") == "BENEFICIOS" && i.GetProperty("referenciaId").GetInt32() == adesaoComFilho && Texto(i, "descricao") == "Inclusão de dependente no plano de saúde do painel");
        Assert.Contains(itens, i => Texto(i, "tipo") == "AJUSTE_DE_PONTO" && i.GetProperty("referenciaId").GetInt32() == ajusteDoGestor);

        // o contador do menu do RH (ponto) é o mesmo número dos ajustes
        var menu = await GetAsync(api, "/api/rh/contadores-do-menu");
        Assert.Equal(cartoes["AJUSTE_DE_PONTO"].GetProperty("quantidade").GetInt32(), menu.GetProperty("pedidosDeAjusteDePonto").GetInt32());
    }

    [Fact]
    public async Task A_lista_de_pendencias_pagina_filtra_por_tipo_e_o_tipo_invalido_e_400()
    {
        var api = await ApiAsync(nameof(A_lista_de_pendencias_pagina_filtra_por_tipo_e_o_tipo_invalido_e_400));
        var rhId = api.Elenco.Rh.FuncionarioId;
        var pessoa = await PessoaAsync(api, "Do atestado");
        foreach (var dia in Enumerable.Range(1, 5))
        {
            await PontoDeTeste.NovoAtestadoAsync(api.Conexao, pessoa, $"2026-10-0{dia}", $"2026-10-0{dia}", "ENVIADO", rhId);
        }

        var item = await BeneficiosDeTeste.ItemAsync(api);
        var adesao = await BeneficiosDeTeste.AdesaoAsync(api, pessoa, item, "PENDENTE", ativo: false, pedidoEm: "2026-10-03 08:00:00");
        var rh = await api.ClienteDoRhAgoraAsync();

        var primeira = await GetAsync(api, $"{Rota}/pendencias?tamanhoPagina=2");
        var terceira = await GetAsync(api, $"{Rota}/pendencias?pagina=3&tamanhoPagina=2");
        var soAtestados = await GetAsync(api, $"{Rota}/pendencias?tipo=ATESTADO&tamanhoPagina=100");
        var soBeneficios = await GetAsync(api, $"{Rota}/pendencias?tipo=BENEFICIOS");
        var invalida = await rh.GetAsync($"{Rota}/pendencias?tipo=XYZ");
        var resposta = await rh.GetAsync($"{Rota}/pendencias");

        Assert.Equal((6, 2, 3, 2, 2), (primeira.GetProperty("total").GetInt32(), primeira.GetProperty("tamanhoPagina").GetInt32(), primeira.GetProperty("totalPaginas").GetInt32(), primeira.GetProperty("itens").GetArrayLength(), terceira.GetProperty("itens").GetArrayLength()));
        Assert.All(soAtestados.GetProperty("itens").EnumerateArray(), i => Assert.Equal("ATESTADO", Texto(i, "tipo")));
        Assert.Equal(5, soAtestados.GetProperty("total").GetInt32());
        Assert.Equal([adesao], soBeneficios.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("referenciaId").GetInt32()));
        Assert.Equal(400, (int)invalida.StatusCode);
        await invalida.AfirmarConformeAoContratoAsync("listarPendenciasDoRh");
        await resposta.AfirmarConformeAoContratoAsync("listarPendenciasDoRh");
    }

    // ---------------------------------------------------------------- próximos dias e treinamentos

    [Fact]
    public async Task Os_proximos_dias_juntam_feriados_e_aniversarios_dos_proximos_21_dias_em_ordem_de_data_no_maximo_8()
    {
        var api = await ApiAsync(nameof(Os_proximos_dias_juntam_feriados_e_aniversarios_dos_proximos_21_dias_em_ordem_de_data_no_maximo_8));
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Feriado da empresa", new DateOnly(2026, 10, 20), "EMPRESA");
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Feriado municipal", new DateOnly(2026, 10, 28), "MUNICIPAL", uf: "SP", municipio: "São Paulo"); // 21 dias: entra
        await PontoDeTeste.NovoFeriadoAsync(api.Conexao, "Feriado depois da janela", new DateOnly(2026, 10, 29), "EMPRESA"); // 22 dias: fica de fora
        var nomeDoSetor = $"Setor dos aniversarios {Guid.NewGuid():N}";
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, nomeDoSetor);
        async Task<string> NascerAsync(string nome, string nascimento, int? deSetor, bool ativo = true)
        {
            var id = await PessoaAsync(api, nome, deSetor, ativo);
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_NASCIMENTO = '{nascimento}' WHERE ID_FUNCIONARIO = {id}");
            return await TextoDoNomeAsync(api, id);
        }

        var hoje = await NascerAsync("Zelia aniversariante hoje", "1990-10-07", setor);
        var mesmoDia = await NascerAsync("Ana no dia do feriado nacional", "1985-10-12", null);
        var daSemana = await NascerAsync("Bia da semana seguinte", "1992-10-15", setor);
        var ultimoDia = await NascerAsync("Caio no ultimo dia", "1980-10-28", setor);
        _ = await NascerAsync("Dora depois da janela", "1980-10-29", setor);
        _ = await NascerAsync("Eva inativa", "1980-10-10", setor, ativo: false);
        var dias = (await GetAsync(api, Rota)).GetProperty("proximosDias").EnumerateArray().ToList();

        Assert.Equal(
            [
                ("2026-10-07", "ANIVERSARIO", "Aniversário", $"{hoje}, {nomeDoSetor}"),
                ("2026-10-12", "FERIADO", "Feriado nacional", "Nossa Senhora Aparecida"),
                ("2026-10-12", "ANIVERSARIO", "Aniversário", mesmoDia),
                ("2026-10-15", "ANIVERSARIO", "Aniversário", $"{daSemana}, {nomeDoSetor}"),
                ("2026-10-20", "FERIADO", "Feriado da empresa", "Feriado da empresa"),
                ("2026-10-28", "FERIADO", "Feriado municipal", "Feriado municipal"),
                ("2026-10-28", "ANIVERSARIO", "Aniversário", $"{ultimoDia}, {nomeDoSetor}"),
            ],
            dias.Select(d => (Texto(d, "data"), Texto(d, "tipo"), Texto(d, "rotulo"), Texto(d, "titulo"))));
    }

    private static async Task<string> TextoDoNomeAsync(ApiDeAcesso api, int id) => (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {id}"))!.ToString()!;

    [Fact]
    public async Task Com_mais_de_8_dias_so_os_8_primeiros_saem()
    {
        var api = await ApiAsync(nameof(Com_mais_de_8_dias_so_os_8_primeiros_saem));
        foreach (var dia in Enumerable.Range(14, 10))
        {
            await PontoDeTeste.NovoFeriadoAsync(api.Conexao, $"Feriado {dia}", new DateOnly(2026, 10, dia), "EMPRESA");
        }

        var dias = (await GetAsync(api, Rota)).GetProperty("proximosDias").EnumerateArray().ToList();

        Assert.Equal(8, dias.Count);
        Assert.Equal(dias.Select(d => Texto(d, "data")).Order(), dias.Select(d => Texto(d, "data")));
    }

    [Fact]
    public async Task Os_treinamentos_em_andamento_saem_do_mais_antigo_ao_mais_novo_no_maximo_5_so_os_liberados_e_nao_encerrados_com_o_total_da_lista_do_RH()
    {
        var api = await ApiAsync(nameof(Os_treinamentos_em_andamento_saem_do_mais_antigo_ao_mais_novo_no_maximo_5_so_os_liberados_e_nao_encerrados_com_o_total_da_lista_do_RH));
        var ids = new List<int>();
        foreach (var (dia, indice) in new[] { "2026-09-20", "2026-09-25", "2026-09-10", "2026-10-01", "2026-09-15", "2026-10-05", "2026-10-07" }.Select((d, i) => (d, i)))
        {
            var id = await TreinamentosDeTeste.SemearAsync(api, TreinamentosDeTeste.Titulo("Em andamento"), "EM_ANDAMENTO");
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE TREINAMENTOS SET DATA_INICIO = '{dia}' WHERE ID_TREINAMENTO = {id}");
            ids.Add(id);
            _ = indice;
        }

        _ = await TreinamentosDeTeste.SemearAsync(api, TreinamentosDeTeste.Titulo("Agendado"), "AGENDADO");
        _ = await TreinamentosDeTeste.SemearAsync(api, TreinamentosDeTeste.Titulo("Encerrado"), "ENCERRADO");
        _ = await TreinamentosDeTeste.SemearAsync(api, TreinamentosDeTeste.Titulo("Rascunho"));

        var painel = await GetAsync(api, Rota);
        var daLista = await GetAsync(api, "/api/rh/treinamentos?situacao=EM_ANDAMENTO&tamanhoPagina=100");

        // 10/09, 15/09, 20/09, 25/09 e 01/10: os cinco mais antigos
        Assert.Equal([ids[2], ids[4], ids[0], ids[1], ids[3]], painel.GetProperty("treinamentosEmAndamento").EnumerateArray().Select(t => t.GetProperty("treinamentoId").GetInt32()));
        foreach (var t in painel.GetProperty("treinamentosEmAndamento").EnumerateArray())
        {
            var original = daLista.GetProperty("itens").EnumerateArray().Single(i => i.GetProperty("id").GetInt32() == t.GetProperty("treinamentoId").GetInt32());
            Assert.Equal((original.GetProperty("totalParticipantes").GetInt32(), original.GetProperty("concluidos").GetInt32(), Texto(original, "titulo")), (t.GetProperty("total").GetInt32(), t.GetProperty("concluiram").GetInt32(), Texto(t, "titulo")));
        }
    }

    // ---------------------------------------------------------------- presença e últimas alterações

    [Fact]
    public async Task A_presenca_conta_cada_pessoa_ativa_e_admitida_em_uma_situacao_de_hoje_e_o_total_nao_inclui_sem_expediente()
    {
        var api = await ApiAsync(nameof(A_presenca_conta_cada_pessoa_ativa_e_admitida_em_uma_situacao_de_hoje_e_o_total_nao_inclui_sem_expediente));
        var base0 = await ContarAtivosAdmitidosAsync(api);
        var trabalhando = await PessoaAsync(api, "Trabalhando agora");
        var deFerias = await PessoaAsync(api, "De ferias agora");
        var comAtestado = await PessoaAsync(api, "Com atestado agora");
        _ = await PessoaAsync(api, "Sem registro agora");
        _ = await PessoaAsync(api, "Inativa agora", ativo: false);
        _ = await PessoaAsync(api, "Admitida amanha", admissao: new DateOnly(2026, 10, 8));
        _ = await PessoaAsync(api, "Admitida hoje", admissao: new DateOnly(2026, 10, 7)); // o primeiro dia de casa já conta: está sem registro depois do limite
        await PontoDeTeste.BaterEmAsync(api.Conexao, trabalhando, new DateOnly(2026, 10, 7), (TipoDeMarcacao.Entrada, "2026-10-07 11:00:00"));
        await PontoDeTeste.NovasFeriasAsync(api.Conexao, deFerias, "2026-10-05", "2026-10-09", "APROVADA");
        await PontoDeTeste.NovoAtestadoAsync(api.Conexao, comAtestado, "2026-10-07", "2026-10-07", "VALIDADO", api.Elenco.Rh.FuncionarioId);

        var presenca = (await GetAsync(api, Rota)).GetProperty("presencaAgora");

        var c = presenca.GetProperty("contagensHoje");
        Assert.Equal((1, 1, 1, base0 + 2, 0, 0), (c.GetProperty("trabalhando").GetInt32(), c.GetProperty("ferias").GetInt32(), c.GetProperty("atestado").GetInt32(), c.GetProperty("semRegistro").GetInt32(), c.GetProperty("aindaNaoComecou").GetInt32(), c.GetProperty("semExpediente").GetInt32()));
        Assert.Equal((true, base0 + 5), (presenca.GetProperty("diaUtil").GetBoolean(), presenca.GetProperty("total").GetInt32()));
    }

    [Fact]
    public async Task Antes_do_limite_de_entrada_ninguem_esta_sem_registro_e_em_dia_sem_expediente_o_total_vale_so_quem_trabalha()
    {
        var api = await ApiAsync(nameof(Antes_do_limite_de_entrada_ninguem_esta_sem_registro_e_em_dia_sem_expediente_o_total_vale_so_quem_trabalha));
        var base0 = await ContarAtivosAdmitidosAsync(api);
        var trabalha = await PessoaAsync(api, "Trabalha no sabado");
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 7, 10, 30, 0, DateTimeKind.Utc)); // 07:30 em Brasília, antes das 08:10

        var cedo = (await GetAsync(api, Rota)).GetProperty("presencaAgora");
        var listaCedo = await GetAsync(api, $"{Rota}/presenca/sem-registro");
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 10, 12, 42, 0, DateTimeKind.Utc)); // sábado
        await PontoDeTeste.BaterEmAsync(api.Conexao, trabalha, new DateOnly(2026, 10, 10), (TipoDeMarcacao.Entrada, "2026-10-10 11:00:00"));
        var sabado = (await GetAsync(api, Rota)).GetProperty("presencaAgora");
        var listaDoSabado = await GetAsync(api, $"{Rota}/presenca/sem-registro");

        Assert.Equal((0, base0 + 1, true), (cedo.GetProperty("contagensHoje").GetProperty("semRegistro").GetInt32(), cedo.GetProperty("contagensHoje").GetProperty("aindaNaoComecou").GetInt32(), cedo.GetProperty("diaUtil").GetBoolean()));
        Assert.Equal(base0 + 1, cedo.GetProperty("total").GetInt32());
        Assert.Empty(listaCedo.GetProperty("itens").EnumerateArray()); // quem ainda não começou não entra
        Assert.Equal((false, 1, 1, base0), (sabado.GetProperty("diaUtil").GetBoolean(), sabado.GetProperty("total").GetInt32(), sabado.GetProperty("contagensHoje").GetProperty("trabalhando").GetInt32(), sabado.GetProperty("contagensHoje").GetProperty("semExpediente").GetInt32()));
        Assert.Empty(listaDoSabado.GetProperty("itens").EnumerateArray());
    }

    private static async Task<int> ContarAtivosAdmitidosAsync(ApiDeAcesso api) =>
        (int)await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS WHERE ATIVO = TRUE AND DATA_ADMISSAO <= '2026-10-07'");

    [Fact]
    public async Task A_lista_de_sem_registro_traz_so_SEM_REGISTRO_em_ordem_alfabetica_pagina_e_bate_com_o_numero_do_painel()
    {
        var api = await ApiAsync(nameof(A_lista_de_sem_registro_traz_so_SEM_REGISTRO_em_ordem_alfabetica_pagina_e_bate_com_o_numero_do_painel));
        var elenco = await ContarAtivosAdmitidosAsync(api);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, $"Setor sem registro {Guid.NewGuid():N}");
        var zeca = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Zeca sem registro", Perfil.Funcionario, setor);
        var anaB = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana sem registro", Perfil.Funcionario, setor);
        var anaA = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Ana sem registro", Perfil.Funcionario, setor);
        var anaMinuscula = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "ana sem registro", Perfil.Funcionario, setor); // só a caixa difere: a ordem alfabética não liga para ela e o empate vai pelo id
        var presente = await PessoaAsync(api, "Presente");
        await PontoDeTeste.BaterEmAsync(api.Conexao, presente, new DateOnly(2026, 10, 7), (TipoDeMarcacao.Entrada, "2026-10-07 11:00:00"));

        var tudo = await GetAsync(api, $"{Rota}/presenca/sem-registro?tamanhoPagina=100");
        var painel = (await GetAsync(api, Rota)).GetProperty("presencaAgora").GetProperty("contagensHoje").GetProperty("semRegistro").GetInt32();
        var pagina2 = await GetAsync(api, $"{Rota}/presenca/sem-registro?pagina=2&tamanhoPagina=2");

        var itens = tudo.GetProperty("itens").EnumerateArray().ToList();
        Assert.Equal(painel, tudo.GetProperty("total").GetInt32());
        Assert.Equal(elenco + 4, painel);
        Assert.All(itens, i => Assert.Equal("SEM_REGISTRO", Texto(i, "situacaoHoje")));
        Assert.DoesNotContain(itens, i => i.GetProperty("id").GetInt32() == presente);
        Assert.Equal(itens.Select(i => Texto(i, "nome")).OrderBy(n => n, StringComparer.Create(System.Globalization.CultureInfo.GetCultureInfo("pt-BR"), ignoreCase: true)), itens.Select(i => Texto(i, "nome")));
        var daSetor = itens.Where(i => new[] { zeca, anaA, anaB, anaMinuscula }.Contains(i.GetProperty("id").GetInt32())).Select(i => i.GetProperty("id").GetInt32()).ToList();
        Assert.Equal([Math.Min(anaA, anaB), Math.Max(anaA, anaB), anaMinuscula, zeca], daSetor);
        Assert.Equal((2, painel), (pagina2.GetProperty("itens").GetArrayLength(), pagina2.GetProperty("total").GetInt32()));
        var primeiro = itens[0];
        Assert.True(primeiro.TryGetProperty("cargo", out _) && primeiro.TryGetProperty("setor", out _) && primeiro.TryGetProperty("ativo", out _));
    }

    [Fact]
    public async Task As_ultimas_alteracoes_sao_as_4_mais_novas_so_de_alteracao_com_o_autor_em_texto_pronto()
    {
        var api = await ApiAsync(nameof(As_ultimas_alteracoes_sao_as_4_mais_novas_so_de_alteracao_com_o_autor_em_texto_pronto));
        var rh = api.Elenco.Rh.FuncionarioId;
        var gestor = api.Elenco.Gestor.FuncionarioId;
        async Task EventoAsync(string quando, string tipo, int? ator, string nome, string? perfil, string area, string acao, string descricao)
        {
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO AUDITORIA_EVENTOS (QUANDO, ATOR_TIPO, ID_FUNCIONARIO_ATOR, ATOR_NOME, ATOR_PERFIL, AREA, ACAO, DESCRICAO, REGISTRO_AFETADO, CANAL, IP) VALUES ('{quando}', '{tipo}', {(ator is null ? "NULL" : ator.Value.ToString(System.Globalization.CultureInfo.InvariantCulture))}, '{nome}', {(perfil is null ? "NULL" : $"'{perfil}'")}, '{area}', '{acao}', '{descricao}', 'Registro do painel', '{(tipo == "PESSOA" ? "PORTAL" : "SISTEMA")}', {(tipo == "PESSOA" ? "'10.0.0.7'" : "NULL")})");
        }

        await EventoAsync("2026-10-08 06:00:00", "PESSOA", rh, "Ana Souza", "RH", "COLABORADORES", "ALTEROU", "Alterou o cargo de Bruno Lima de Analista para Analista pleno");
        await EventoAsync("2026-10-08 06:01:00", "PESSOA", gestor, "Carlos Ribeiro", "Gestor", "FERIAS", "APROVOU", "Aprovou as férias de Júlia Nunes");
        await EventoAsync("2026-10-08 06:02:00", "SISTEMA", null, "Sistema", null, "FERIAS", "ALTEROU", "O sistema concluiu 2 férias que já terminaram");
        await EventoAsync("2026-10-08 06:03:00", "PESSOA", api.Elenco.MembroA.FuncionarioId, "Bruno Lima", "Funcionario", "PONTO", "PEDIU_AJUSTE", "Pediu ajuste do ponto de 05/10");
        await EventoAsync("2026-10-08 06:04:00", "PESSOA", rh, "Ana Souza", "RH", "ACESSO", "ENTROU", "Entrou no sistema"); // login: não é alteração
        await EventoAsync("2026-10-08 06:05:00", "PESSOA", api.Elenco.MembroB.FuncionarioId, "Pedro Santos", "Funcionario", "PONTO", "REGISTROU", "Registrou a entrada"); // batida: não é alteração
        await EventoAsync("2026-10-08 06:06:00", "PESSOA", rh, "Ana Souza", "RH", "DOCUMENTOS", "BAIXOU", "Baixou um documento"); // download: não é alteração
        await EventoAsync("2026-10-08 06:07:00", "PESSOA", rh, "Ana Souza", "RH", "ACESSO", "ACESSO_NEGADO", "Acesso negado"); // acesso negado: não é alteração
        await EventoAsync("2026-10-09 00:00:00", "PESSOA", rh, "Ana Souza", "RH", "COLABORADORES", "ALTEROU", "Evento do futuro além de um dia"); // depois de "agora + 1 dia": fora

        var alteracoes = (await GetAsync(api, Rota)).GetProperty("ultimasAlteracoes").EnumerateArray().ToList();

        Assert.Equal(
            [
                ("Pediu ajuste do ponto de 05/10", "Bruno Lima (funcionário)"),
                ("O sistema concluiu 2 férias que já terminaram", "Sistema"),
                ("Aprovou as férias de Júlia Nunes", "Carlos Ribeiro (gestor)"),
                ("Alterou o cargo de Bruno Lima de Analista para Analista pleno", "Ana Souza (RH)"),
            ],
            alteracoes.Select(a => (Texto(a, "descricao"), Texto(a, "autor"))));
        Assert.Equal("2026-10-08T06:03:00Z", alteracoes[0].GetProperty("quandoUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"));
    }

    // ---------------------------------------------------------------- desde, admissão e benefícios (os números das bordas)

    [Fact]
    public async Task O_desde_das_ferias_e_a_aprovacao_do_gestor_a_resposta_que_aceitou_a_sugestao_ou_a_solicitacao()
    {
        var api = await ApiAsync(nameof(O_desde_das_ferias_e_a_aprovacao_do_gestor_a_resposta_que_aceitou_a_sugestao_ou_a_solicitacao));
        var (setor, _, gestorId) = await FeriasDeTeste.SetorComGestorAsync(api, "Desde das ferias");
        var ana = await FeriasDeTeste.PessoaAsync(api, "Ana do desde", new DateOnly(2022, 3, 14), setor);
        var bia = await FeriasDeTeste.PessoaAsync(api, "Bia do desde", new DateOnly(2022, 3, 14), setor);
        var caio = await FeriasDeTeste.PessoaAsync(api, "Caio do desde", new DateOnly(2022, 3, 14), setor);
        var aprovadaPeloGestor = await FeriasDeTeste.SemearAsync(api, ana.FuncionarioId, "2025-03-14", "2026-03-13", "2026-11-09", "2026-11-23", "SOLICITADA", "2026-09-29 09:00:00", gestorId, gestorAprovou: true);
        var sugestaoAceita = await FeriasDeTeste.SemearAsync(api, bia.FuncionarioId, "2025-03-14", "2026-03-13", "2026-12-07", "2026-12-21", "SOLICITADA", "2026-09-20 09:00:00", gestorId, resultadoDoGestor: "SUGERIU_DATA", sugestaoInicio: "2026-12-14", sugestaoFim: "2026-12-28", respostaDaSugestao: "ACEITOU");
        var semEtapaDoGestor = await FeriasDeTeste.SemearAsync(api, caio.FuncionarioId, "2025-03-14", "2026-03-13", "2027-01-11", "2027-01-25", "SOLICITADA", "2026-10-03 09:00:00");

        var itens = (await GetAsync(api, $"{Rota}/pendencias?tipo=FERIAS")).GetProperty("itens").EnumerateArray().ToList();

        // 09:00 em Brasília = 12:00 UTC; a aprovação é um dia depois da solicitação e a resposta à sugestão, dois dias depois
        Assert.Equal(
            [(sugestaoAceita, "2026-09-22T12:00:00Z"), (aprovadaPeloGestor, "2026-09-30T12:00:00Z"), (semEtapaDoGestor, "2026-10-03T12:00:00Z")],
            itens.Select(i => (i.GetProperty("referenciaId").GetInt32(), i.GetProperty("desdeEmUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ"))));
    }

    [Fact]
    public async Task A_admissao_entra_de_hoje_a_7_dias_so_com_formulario_de_admissao_publicado_atribuido_e_ainda_sem_resposta()
    {
        var api = await ApiAsync(nameof(A_admissao_entra_de_hoje_a_7_dias_so_com_formulario_de_admissao_publicado_atribuido_e_ainda_sem_resposta));
        var admissao = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("Admissão das bordas"), "PUBLICADO", tipo: "ADMISSAO", alvo: "PESSOAS");
        var pergunta = await FormulariosDeTeste.PerguntaAsync(api, admissao, "Dados");
        var pesquisa = await FormulariosDeTeste.SemearAsync(api, FormulariosDeTeste.Titulo("Pesquisa das bordas"), "PUBLICADO", tipo: "PESQUISA", alvo: "PESSOAS");
        _ = await FormulariosDeTeste.PerguntaAsync(api, pesquisa, "Como foi?");
        var hoje = await PessoaAsync(api, "Admitida hoje com formulario", admissao: new DateOnly(2026, 10, 7));
        var noLimite = await PessoaAsync(api, "Admitida em 7 dias", admissao: new DateOnly(2026, 10, 14));
        var depoisDoLimite = await PessoaAsync(api, "Admitida em 8 dias", admissao: new DateOnly(2026, 10, 15));
        var ontem = await PessoaAsync(api, "Admitida ontem", admissao: new DateOnly(2026, 10, 6));
        var jaRespondeu = await PessoaAsync(api, "Admitida que ja respondeu", admissao: new DateOnly(2026, 10, 10));
        var soPesquisa = await PessoaAsync(api, "Admitida so com pesquisa", admissao: new DateOnly(2026, 10, 9));
        await RespostasDeTeste.EscolherPessoasAsync(api, admissao, hoje, noLimite, depoisDoLimite, ontem, jaRespondeu);
        await RespostasDeTeste.EscolherPessoasAsync(api, pesquisa, soPesquisa);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO, RESPOSTA, DATA_RESPOSTA, ANONIMO) VALUES ({pergunta}, {admissao}, {jaRespondeu}, NULL, 'Enviado', '2026-10-06 10:00:00', 0)");

        var itens = (await GetAsync(api, $"{Rota}/pendencias?tipo=ADMISSAO_COM_DOCUMENTOS_PENDENTES")).GetProperty("itens").EnumerateArray().ToList();

        Assert.Equal([hoje, noLimite], itens.Select(i => i.GetProperty("funcionario").GetProperty("id").GetInt32())); // por data de início: a de hoje e a de 7 dias
    }

    [Fact]
    public async Task Beneficio_so_conta_adesao_pendente_ou_aprovada_com_filho_pendente_e_o_desde_e_o_do_pedido_pendente_mais_antigo()
    {
        var api = await ApiAsync(nameof(Beneficio_so_conta_adesao_pendente_ou_aprovada_com_filho_pendente_e_o_desde_e_o_do_pedido_pendente_mais_antigo));
        var comDependentes = await BeneficiosDeTeste.ItemAsync(api, "Plano de saúde das bordas", opcional: false, dependentes: true);
        var comTrajeto = await BeneficiosDeTeste.ItemAsync(api, "Vale-transporte das bordas", trajeto: true);
        var simples = await BeneficiosDeTeste.ItemAsync(api, "Seguro das bordas");
        var semNada = await PessoaAsync(api, "Aprovada sem nada pendente");
        _ = await BeneficiosDeTeste.AdesaoAsync(api, semNada, simples); // aprovada e ativa, sem filho pendente: não é pendência
        var comFilhos = await PessoaAsync(api, "Aprovada com dependente pendente");
        var adesaoDosFilhos = await BeneficiosDeTeste.AdesaoAsync(api, comFilhos, comDependentes);
        _ = await BeneficiosDeTeste.DependenteAsync(api, adesaoDosFilhos, "Ana Filha", status: "APROVADO", pedidoEm: "2026-01-10 08:00:00", respondidoEm: "2026-01-11 08:00:00"); // o pedido antigo já decidido não conta
        _ = await BeneficiosDeTeste.DependenteAsync(api, adesaoDosFilhos, "Bia Filha", pedidoEm: "2026-10-02 08:00:00");
        var comViagem = await PessoaAsync(api, "Aprovada com trajeto pendente");
        var adesaoDaViagem = await BeneficiosDeTeste.AdesaoAsync(api, comViagem, comTrajeto);
        _ = await BeneficiosDeTeste.TrajetoAsync(api, adesaoDaViagem, status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-04 09:00:00");
        _ = await BeneficiosDeTeste.TrajetoAsync(api, adesaoDaViagem, origem: "Rua Nova, 10, São Paulo", pedidoEm: "2026-10-01 08:00:00");

        var itens = (await GetAsync(api, $"{Rota}/pendencias?tipo=BENEFICIOS")).GetProperty("itens").EnumerateArray().ToList();

        Assert.Equal(
            [(adesaoDaViagem, "Atualização do trajeto do vale-transporte das bordas", "2026-10-01"), (adesaoDosFilhos, "Inclusão de dependente no plano de saúde das bordas", "2026-10-02")],
            itens.Select(i => (i.GetProperty("referenciaId").GetInt32(), Texto(i, "descricao"), i.GetProperty("desdeEmUtc").GetDateTime().ToUniversalTime().ToString("yyyy-MM-dd"))));
    }

    [Fact]
    public async Task Aniversarios_no_mesmo_dia_saem_em_ordem_alfabetica_sem_ligar_para_a_caixa_e_o_empate_vai_pela_ordem_de_cadastro()
    {
        var api = await ApiAsync(nameof(Aniversarios_no_mesmo_dia_saem_em_ordem_alfabetica_sem_ligar_para_a_caixa_e_o_empate_vai_pela_ordem_de_cadastro));
        var maiuscula = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Duda do mesmo dia", Perfil.Funcionario, null, true, null);
        var minuscula = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "duda do mesmo dia", Perfil.Funcionario, null, true, null);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET DATA_NASCIMENTO = '1991-10-16' WHERE ID_FUNCIONARIO IN ({maiuscula}, {minuscula})");

        var dias = (await GetAsync(api, Rota)).GetProperty("proximosDias").EnumerateArray().ToList();

        Assert.Equal(["Duda do mesmo dia", "duda do mesmo dia"], dias.Where(d => Texto(d, "tipo") == "ANIVERSARIO").Select(d => Texto(d, "titulo")));
    }

    // ---------------------------------------------------------------- falhas e permissão

    [Fact]
    public async Task Uma_fila_que_lanca_sai_vazia_com_Warning_sem_a_mensagem_e_nunca_da_500()
    {
        var api = await ApiAsync(nameof(Uma_fila_que_lanca_sai_vazia_com_Warning_sem_a_mensagem_e_nunca_da_500));
        var pessoa = await PessoaAsync(api, "Do atestado com falha");
        await PontoDeTeste.NovoAtestadoAsync(api.Conexao, pessoa, "2026-10-06", "2026-10-06", "ENVIADO", api.Elenco.Rh.FuncionarioId);
        var coletor = new ColetorDeLogs();
        using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureTestServices(servicos =>
        {
            servicos.RemoveAll<IConsultaDoPainelDoRh>();
            servicos.AddScoped(_ => LancadorDeFalhas<IConsultaDoPainelDoRh>.Criar());
            servicos.AddSingleton<ILoggerProvider>(coletor);
        }));
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await api.ClienteDoRhAgoraAsync()).DefaultRequestHeaders.Authorization;

        var painel = await CorpoAsync(await cliente.GetAsync(Rota));
        var lista = await cliente.GetAsync($"{Rota}/pendencias");
        var semRegistro = await cliente.GetAsync($"{Rota}/presenca/sem-registro");

        Assert.Equal(0, painel.GetProperty("totalPendencias").GetInt32());
        Assert.Equal((0, true), (painel.GetProperty("presencaAgora").GetProperty("total").GetInt32(), painel.GetProperty("presencaAgora").GetProperty("diaUtil").GetBoolean()));
        Assert.Equal(200, (int)lista.StatusCode);
        Assert.Equal(500, (int)semRegistro.StatusCode); // a lista de nomes não tem o que mostrar sem a fonte: erro de servidor honesto, nunca uma lista errada
        var avisos = coletor.Mensagens.Where(m => m.StartsWith("[Warning]", StringComparison.Ordinal) && m.Contains("falhou", StringComparison.Ordinal)).ToList();
        Assert.Contains(avisos, a => a.Contains("atestados", StringComparison.Ordinal));
        Assert.Contains(avisos, a => a.Contains("presença agora", StringComparison.Ordinal));
        Assert.All(avisos, a => Assert.DoesNotContain(LancadorDeFalhas<IConsultaDoPainelDoRh>.Segredo, a, StringComparison.Ordinal));
    }

    [Theory]
    [InlineData(Perfil.Funcionario)]
    [InlineData(Perfil.Gestor)]
    public async Task So_o_RH_usa_o_painel_e_as_duas_listas(Perfil perfil)
    {
        var api = await ApiAsync(nameof(So_o_RH_usa_o_painel_e_as_duas_listas) + perfil);
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("Sem permissao do painel do RH"), perfil);
        var cliente = await AdvertenciasDeTeste.ClienteDeAsync(api, pessoa);

        var respostas = new[] { await cliente.GetAsync(Rota), await cliente.GetAsync($"{Rota}/pendencias"), await cliente.GetAsync($"{Rota}/presenca/sem-registro") };

        Assert.All(respostas, r => Assert.Equal(403, (int)r.StatusCode));
    }
}
