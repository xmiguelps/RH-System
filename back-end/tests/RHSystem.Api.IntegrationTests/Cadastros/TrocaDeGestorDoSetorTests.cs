using System.Net.Http.Json;
using Microsoft.AspNetCore.Mvc.Testing;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>
/// A troca de gestor do setor (<c>PUT /api/rh/setores/{setorId}</c> e <c>POST /api/rh/setores</c> com gestor): promove e rebaixa, refaz o <c>ID_GESTOR</c> dos membros (pela
/// <c>IGestorDoFuncionario.RefazerDosMembrosAsync</c> da 1b, que aqui roda DE VERDADE por baixo do espião), passa os pedidos parados do gestor antigo ao RH (a porta <c>IPedidosParadosNoGestor</c> da 1a)
/// e desfaz tudo se qualquer passo falhar. Cada teste monta o setor dele (o rótulo vai nos nomes): o banco é um por classe.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TrocaDeGestorDoSetorTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(TrocaDeGestorDoSetorTests));

    private static string Rota(int setorId) => $"/api/rh/setores/{setorId}";

    private static HttpClient ComoRh(ApiDeAcesso api, WebApplicationFactory<Program>? fabrica) =>
        fabrica is null ? api.Api.ClienteComo(api.Elenco.Rh) : api.Api.ClienteComo(fabrica, api.Elenco.Rh);

    private static Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, WebApplicationFactory<Program>? fabrica, int setorId, string nome, int? gestorFuncionarioId, string? descricao = null) =>
        ComoRh(api, fabrica).PutAsJsonAsync(Rota(setorId), new { nome, descricao, gestorFuncionarioId });

    private static Task<HttpResponseMessage> CriarAsync(ApiDeAcesso api, WebApplicationFactory<Program>? fabrica, string nome, int? gestorFuncionarioId) =>
        ComoRh(api, fabrica).PostAsJsonAsync("/api/rh/setores", new { nome, descricao = (string?)null, gestorFuncionarioId });

    /// <summary>Uma pessoa do nível <c>Funcionario</c>, ativa, no setor e com o gestor dados (ou fora de qualquer setor).</summary>
    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setorId = null, int? gestorId = null) =>
        SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, $"{Guid.NewGuid():N}@troca.com.br", new DateOnly(2026, 1, 2), true, "Funcionario", setorId, gestorId: gestorId);

    private static async Task<int> GestorIdDaRespostaAsync(HttpResponseMessage resposta)
    {
        Assert.Equal(200, (int)resposta.StatusCode);
        return (await CorpoAsync(resposta)).GetProperty("gestor").GetProperty("gestorId").GetInt32();
    }

    private static async Task<int> EventosAsync(ApiDeAcesso api, string entidade, int id) => (await AuditoriaDeTeste.EventosAsync(api.Conexao, entidade, id)).Count;

    // ---------------------------------------------------------------- sem mudança

    [Fact]
    public async Task Editar_sem_mudar_o_gestor_so_atualiza_nome_e_descricao_sem_efeito_nenhum_e_sem_chamar_a_porta_nem_RefazerDosMembros()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Sem1");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = NULL WHERE ID_FUNCIONARIO = {setor.Ativos[0]}"); // torto de propósito: se algo refizesse o ID_GESTOR, ele mudaria
        var pedidos = new PedidosParadosEspiao();
        var chamadas = new ChamadasDoGestorDoFuncionario();
        var fabrica = api.Api.ComGestorDoFuncionarioEspiao(chamadas, pedidos);

        var resposta = await EditarAsync(api, fabrica, setor.SetorId, "Sem1 Setor renomeado", setor.Gestor.FuncionarioId, "nova descrição");

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("Sem1 Setor renomeado", "nova descrição", setor.Gestor.GestorId, 3), (corpo.GetProperty("nome").GetString(), corpo.GetProperty("descricao").GetString(), corpo.GetProperty("gestor").GetProperty("gestorId").GetInt32(), corpo.GetProperty("quantidadePessoas").GetInt32())); // o gestor e os dois membros ativos
        Assert.Empty(pedidos.Chamadas);
        Assert.Empty(chamadas.Chamadas);
        Assert.Null(await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Ativos[0])); // ninguém refez o ID_GESTOR
        Assert.Equal("Funcionario Gestor", await SetoresDeTeste.NivelAsync(api, setor.Gestor.FuncionarioId));
        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "SETORES", setor.SetorId));
        Assert.Equal(["Descrição", "Nome"], evento.Mudancas.Select(m => m.Campo).Order()); // o gestor não mudou, então não aparece
    }

    // ---------------------------------------------------------------- o efeito da troca

    [Fact]
    public async Task Trocar_o_gestor_refaz_o_ID_GESTOR_dos_ativos_do_setor_o_novo_fica_sem_gestor_e_o_antigo_passa_a_ter_o_novo()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Troca2");
        var novo = await PessoaAsync(api, "Troca2 Novo", setor.SetorId, setor.Gestor.GestorId); // membro do setor, hoje com o gestor antigo
        var deOutroSetor = api.Elenco.DeOutroSetor.FuncionarioId;
        var gestorDele = await SetoresDeTeste.IdGestorDaPessoaAsync(api, deOutroSetor);

        var novoGestorId = await GestorIdDaRespostaAsync(await EditarAsync(api, null, setor.SetorId, "Troca2 Setor", novo));

        Assert.Equal(novoGestorId, await SetoresDeTeste.GestorDoSetorAsync(api, setor.SetorId));
        foreach (var membro in setor.Ativos)
        {
            Assert.Equal(novoGestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, membro));
        }

        Assert.Null(await SetoresDeTeste.IdGestorDaPessoaAsync(api, novo)); // o gestor novo não é gestor de si mesmo (o trigger recusaria)
        Assert.Equal(novoGestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Gestor.FuncionarioId)); // o antigo continua no setor e passa a ter o novo gestor
        Assert.Equal(setor.Gestor.GestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Inativo)); // histórico de quem era o gestor dele
        Assert.Equal(gestorDele, await SetoresDeTeste.IdGestorDaPessoaAsync(api, deOutroSetor)); // outro setor intacto
    }

    [Fact]
    public async Task Criar_e_editar_com_mudanca_de_gestor_chamam_RefazerDosMembrosAsync_do_setor_na_mesma_transacao()
    {
        var api = await ApiAsync();
        var chamadas = new ChamadasDoGestorDoFuncionario();
        var fabrica = api.Api.ComGestorDoFuncionarioEspiao(chamadas);
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Refaz3");
        var novo = await PessoaAsync(api, "Refaz3 Novo");

        var novoGestorId = await GestorIdDaRespostaAsync(await EditarAsync(api, fabrica, setor.SetorId, "Refaz3 Setor", novo));

        // Chamada na transação, DEPOIS de o setor salvo: o espião lê o ID_GESTOR do setor pela mesma conexão e já vê o gestor novo (ainda não confirmado).
        Assert.Equal([new ChamadaDeRefazerOsMembros(setor.SetorId, EmTransacao: true, novoGestorId)], chamadas.Chamadas.ToArray());

        var criador = await PessoaAsync(api, "Refaz3 Criador");
        var criada = await CriarAsync(api, fabrica, "Refaz3 Setor criado", criador);
        Assert.Equal(201, (int)criada.StatusCode);
        var corpo = await CorpoAsync(criada);
        var idCriado = corpo.GetProperty("id").GetInt32();
        Assert.Equal(2, chamadas.Chamadas.Count); // criar com gestor também passa por aqui (o setor novo não tem membros: o efeito é nenhum)
        Assert.Equal(new ChamadaDeRefazerOsMembros(idCriado, EmTransacao: true, corpo.GetProperty("gestor").GetProperty("gestorId").GetInt32()), chamadas.Chamadas[1]);

        Assert.Equal(201, (int)(await CriarAsync(api, fabrica, "Refaz3 Sem gestor", null)).StatusCode);
        Assert.Equal(2, chamadas.Chamadas.Count); // criar sem gestor não chama
    }

    [Fact]
    public async Task Trocar_o_gestor_promove_o_novo_a_Gestor_e_rebaixa_o_antigo_a_Funcionario_que_continua_no_setor_e_em_Gestores()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Promove4");
        var novo = await PessoaAsync(api, "Promove4 Novo");

        Assert.Equal(200, (int)(await EditarAsync(api, null, setor.SetorId, "Promove4 Setor", novo)).StatusCode);

        Assert.Equal("Funcionario Gestor", await SetoresDeTeste.NivelAsync(api, novo));
        Assert.Equal(1, await SetoresDeTeste.LinhasEmGestoresAsync(api, novo));
        Assert.Equal("Funcionario", await SetoresDeTeste.NivelAsync(api, setor.Gestor.FuncionarioId));
        Assert.Equal(setor.SetorId, await SetoresDeTeste.SetorDaPessoaAsync(api, setor.Gestor.FuncionarioId)); // continua no setor, como membro
        Assert.Equal(1, await SetoresDeTeste.LinhasEmGestoresAsync(api, setor.Gestor.FuncionarioId)); // a linha fica: os pedidos antigos a referenciam
        Assert.Null(await SetoresDeTeste.SetorDaPessoaAsync(api, novo)); // e o novo não foi movido para o setor (decisão 1)
    }

    [Fact]
    public async Task Tirar_o_gestor_com_null_zera_o_ID_GESTOR_dos_membros_rebaixa_o_antigo_e_deixa_o_setor_sem_gestor()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Tira5");

        var resposta = await EditarAsync(api, null, setor.SetorId, "Tira5 Setor", null);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(System.Text.Json.JsonValueKind.Null, (await CorpoAsync(resposta)).GetProperty("gestor").ValueKind);
        Assert.Null(await SetoresDeTeste.GestorDoSetorAsync(api, setor.SetorId));
        foreach (var membro in setor.Ativos.Append(setor.Gestor.FuncionarioId))
        {
            Assert.Null(await SetoresDeTeste.IdGestorDaPessoaAsync(api, membro));
        }

        Assert.Equal(setor.Gestor.GestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Inativo)); // inativo fica como estava
        Assert.Equal("Funcionario", await SetoresDeTeste.NivelAsync(api, setor.Gestor.FuncionarioId));
        Assert.Equal(1, await SetoresDeTeste.LinhasEmGestoresAsync(api, setor.Gestor.FuncionarioId));
    }

    [Fact]
    public async Task Gestor_novo_que_ja_e_membro_do_setor_recebe_ID_GESTOR_nulo_e_nunca_o_proprio()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Membro6", ativos: 2);
        var novo = setor.Ativos[0];

        var novoGestorId = await GestorIdDaRespostaAsync(await EditarAsync(api, null, setor.SetorId, "Membro6 Setor", novo)); // o trigger "gestor de si mesmo" não pode disparar

        Assert.Null(await SetoresDeTeste.IdGestorDaPessoaAsync(api, novo));
        Assert.Equal(novoGestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Ativos[1]));
        Assert.Equal(novoGestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Gestor.FuncionarioId));
    }

    [Fact]
    public async Task Gestor_novo_de_outro_setor_continua_no_setor_dele_e_nao_e_movido()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Decisao7");
        var outro = await SetoresDeTeste.ComEquipeAsync(api, "Decisao7 Outro", ativos: 1);
        var novo = outro.Ativos[0]; // do outro setor, nível Funcionario

        var resposta = await EditarAsync(api, null, setor.SetorId, "Decisao7 Setor", novo);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(outro.SetorId, await SetoresDeTeste.SetorDaPessoaAsync(api, novo)); // não mudou de setor
        Assert.Equal("Funcionario Gestor", await SetoresDeTeste.NivelAsync(api, novo));
        var corpo = await CorpoAsync(resposta);
        Assert.Equal((novo, 3), (corpo.GetProperty("gestor").GetProperty("funcionarioId").GetInt32(), corpo.GetProperty("quantidadePessoas").GetInt32())); // ele gerencia o setor sem fazer parte dele: não é contado
        Assert.Equal(outro.Gestor.GestorId, await SetoresDeTeste.GestorDoSetorAsync(api, outro.SetorId)); // o outro setor continua com o gestor dele
    }

    // ---------------------------------------------------------------- a porta dos pedidos parados

    [Fact]
    public async Task Trocar_ou_tirar_o_gestor_chama_PassarParaORhAsync_do_gestor_antigo_uma_vez_e_nao_chama_sem_gestor_antigo_nem_sem_mudanca()
    {
        var api = await ApiAsync();
        var pedidos = new PedidosParadosEspiao();
        var fabrica = api.Api.ComPedidosParados(pedidos);
        var trocado = await SetoresDeTeste.ComEquipeAsync(api, "Passa8a");
        var tirado = await SetoresDeTeste.ComEquipeAsync(api, "Passa8b");

        Assert.Equal(200, (int)(await EditarAsync(api, fabrica, trocado.SetorId, "Passa8a Setor", await PessoaAsync(api, "Passa8a Novo"))).StatusCode);
        Assert.Equal([(PedidosParadosEspiao.PassarParaORh, trocado.Gestor.GestorId)], pedidos.Chamadas.ToArray()); // uma vez, com o gestor ANTIGO

        Assert.Equal(200, (int)(await EditarAsync(api, fabrica, tirado.SetorId, "Passa8b Setor", null)).StatusCode);
        Assert.Equal([(PedidosParadosEspiao.PassarParaORh, trocado.Gestor.GestorId), (PedidosParadosEspiao.PassarParaORh, tirado.Gestor.GestorId)], pedidos.Chamadas.ToArray()); // tirar também passa ao RH

        var semGestor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Passa8c Setor sem gestor");
        var gestorNovo = await PessoaAsync(api, "Passa8c Novo");
        Assert.Equal(200, (int)(await EditarAsync(api, fabrica, semGestor, "Passa8c Setor sem gestor", gestorNovo)).StatusCode); // sem gestor antigo: ninguém para passar
        Assert.Equal(200, (int)(await EditarAsync(api, fabrica, semGestor, "Passa8c Setor sem gestor", gestorNovo, "só a descrição")).StatusCode); // mesmo gestor: sem mudança
        Assert.Equal(200, (int)(await EditarAsync(api, fabrica, await ApoioDeTeste.CriarSetorAsync(api.Conexao, "Passa8d Outro sem gestor"), "Passa8d Outro sem gestor", null)).StatusCode); // sem gestor antes e depois

        Assert.Equal(2, pedidos.Chamadas.Count);
    }

    [Fact]
    public async Task Falha_no_meio_da_troca_desfaz_o_setor_os_niveis_os_ID_GESTOR_e_o_evento()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Falha9");
        var novo = await PessoaAsync(api, "Falha9 Novo", setor.SetorId, setor.Gestor.GestorId);
        var pedidos = new PedidosParadosEspiao { Lanca = new InvalidOperationException("detalhe-interno-da-porta-888") };
        var chamadas = new ChamadasDoGestorDoFuncionario();
        var fabrica = api.Api.ComGestorDoFuncionarioEspiao(chamadas, pedidos);

        var resposta = await EditarAsync(api, fabrica, setor.SetorId, "Falha9 Setor renomeado", novo, "descrição que não pode ficar");

        Assert.Equal(500, (int)resposta.StatusCode);
        Assert.DoesNotContain("detalhe-interno-da-porta-888", await resposta.Content.ReadAsStringAsync());
        Assert.Single(chamadas.Chamadas); // o passo 4 (refazer os membros) chegou a rodar; o 5 (a porta) é o que lançou: tudo o que veio antes tem de ter voltado
        Assert.Equal(("Falha9 Setor", (int?)setor.Gestor.GestorId), (await Sql.EscalarAsync(api.Conexao, $"SELECT NOME_SETOR FROM SETORES WHERE ID_SETOR = {setor.SetorId}") as string, await SetoresDeTeste.GestorDoSetorAsync(api, setor.SetorId)));
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT DESCRICAO FROM SETORES WHERE ID_SETOR = {setor.SetorId}"));
        Assert.Equal(("Funcionario", "Funcionario Gestor"), (await SetoresDeTeste.NivelAsync(api, novo), await SetoresDeTeste.NivelAsync(api, setor.Gestor.FuncionarioId)));
        Assert.Equal(0, await SetoresDeTeste.LinhasEmGestoresAsync(api, novo));
        foreach (var membro in setor.Ativos.Append(novo))
        {
            Assert.Equal(setor.Gestor.GestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, membro)); // o ID_GESTOR refeito voltou
        }

        Assert.Equal(0, await EventosAsync(api, "SETORES", setor.SetorId));
        Assert.Equal(0, await EventosAsync(api, "FUNCIONARIOS", novo));
        Assert.Equal(0, await EventosAsync(api, "FUNCIONARIOS", setor.Gestor.FuncionarioId));
    }

    [Fact]
    public async Task Erros_da_troca_nao_deixam_nada_pela_metade()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Erros10");
        var outro = await SetoresDeTeste.ComEquipeAsync(api, "Erros10 Outro");
        var rh = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Erros10 RH", Perfil.RH);
        var inativo = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, "Erros10 Inativo", Perfil.Funcionario, ativo: false);
        var pedidos = new PedidosParadosEspiao();
        var chamadas = new ChamadasDoGestorDoFuncionario();
        var fabrica = api.Api.ComGestorDoFuncionarioEspiao(chamadas, pedidos);

        Assert.Equal(422, (int)(await EditarAsync(api, fabrica, setor.SetorId, "Erros10 Setor", rh)).StatusCode);
        Assert.Equal(422, (int)(await EditarAsync(api, fabrica, setor.SetorId, "Erros10 Setor", inativo)).StatusCode);
        Assert.Equal(409, (int)(await EditarAsync(api, fabrica, setor.SetorId, "Erros10 Setor", outro.Gestor.FuncionarioId)).StatusCode);
        Assert.Equal(409, (int)(await EditarAsync(api, fabrica, setor.SetorId, outro.Nome, await PessoaAsync(api, "Erros10 Candidato"))).StatusCode); // o nome repetido

        Assert.Equal(setor.Gestor.GestorId, await SetoresDeTeste.GestorDoSetorAsync(api, setor.SetorId));
        Assert.Equal("Funcionario Gestor", await SetoresDeTeste.NivelAsync(api, setor.Gestor.FuncionarioId));
        Assert.Equal(("RH", "Funcionario"), (await SetoresDeTeste.NivelAsync(api, rh), await SetoresDeTeste.NivelAsync(api, inativo)));
        Assert.Equal(0, await SetoresDeTeste.LinhasEmGestoresAsync(api, rh) + await SetoresDeTeste.LinhasEmGestoresAsync(api, inativo));
        Assert.Equal(setor.Gestor.GestorId, await SetoresDeTeste.IdGestorDaPessoaAsync(api, setor.Ativos[0]));
        Assert.Empty(chamadas.Chamadas);
        Assert.Empty(pedidos.Chamadas);
        Assert.Equal(0, await EventosAsync(api, "SETORES", setor.SetorId));
    }

    // ---------------------------------------------------------------- auditoria e escopo

    [Fact]
    public async Task Trocar_o_gestor_grava_Alterou_na_area_Cadastros_com_Gestor_antes_e_depois_pelo_NOME()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Auditoria11");
        var novo = await PessoaAsync(api, "Auditoria11 Novo");

        Assert.Equal(200, (int)(await EditarAsync(api, null, setor.SetorId, "Auditoria11 Setor", novo)).StatusCode);

        var evento = Assert.Single(await AuditoriaDeTeste.EventosAsync(api.Conexao, "SETORES", setor.SetorId));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Alterou, api.Elenco.Rh.FuncionarioId), (evento.Area, evento.Acao, evento.FuncionarioAtorId));
        Assert.Contains(evento.Mudancas, m => m is { Campo: "Gestor", ValorAntes: "Auditoria11 Gestor", ValorDepois: "Auditoria11 Novo" }); // o nome da pessoa, não o id
        // O nível de acesso é auditado no colaborador (a 1b o marcou): quem foi promovido e quem foi rebaixado.
        Assert.Contains((await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", novo)).SelectMany(e => e.Mudancas), m => m is { Campo: "Nível de acesso", ValorAntes: "Colaborador", ValorDepois: "Gestor" });
        Assert.Contains((await AuditoriaDeTeste.EventosAsync(api.Conexao, "FUNCIONARIOS", setor.Gestor.FuncionarioId)).SelectMany(e => e.Mudancas), m => m is { Campo: "Nível de acesso", ValorAntes: "Gestor", ValorDepois: "Colaborador" });
    }

    [Fact]
    public async Task Novo_gestor_ve_a_equipe_do_setor_na_rota_de_teste_do_escopo_da_1a_depois_de_novo_token()
    {
        var api = await ApiAsync();
        var setor = await SetoresDeTeste.ComEquipeAsync(api, "Escopo12");
        var pessoa = await api.NovaPessoaAsync("Escopo12", login: TipoDeLogin.SenhaDefinida);
        var usuarioId = pessoa.UsuarioId!.Value;
        Assert.Equal(403, (int)(await (await api.ClienteAgoraAsync(usuarioId)).GetAsync("/api/gestor/__teste/equipe")).StatusCode); // ainda é Funcionario

        Assert.Equal(200, (int)(await EditarAsync(api, null, setor.SetorId, "Escopo12 Setor", pessoa.FuncionarioId)).StatusCode);

        var resposta = await (await api.ClienteAgoraAsync(usuarioId)).GetAsync("/api/gestor/__teste/equipe"); // o token novo carrega o nível novo
        Assert.Equal(200, (int)resposta.StatusCode);
        var nomes = (await CorpoAsync(resposta)).EnumerateArray().Select(p => p.GetProperty("nome").GetString());
        Assert.Equal(["Escopo12 Gestor", "Escopo12 Membro 1", "Escopo12 Membro 2"], nomes); // o antigo gestor, agora membro, e os dois ativos; o inativo fica de fora
    }
}
