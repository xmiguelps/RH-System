using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Formularios;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// "Quem deve responder" no banco REAL, com o dia de hoje em 2026-10-07 (-03:00): quem está ativo, com a admissão começada e no setor (calculado na hora), quem está em admissão (só por <c>PESSOAS</c>), setor sem gestor, gestor dentro do próprio setor, o número de avaliações e a
/// fotografia do anônimo, que não muda. Cada teste cria os seus setores e pessoas: os números não dependem dos outros testes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class QuemDeveResponderNoBancoTests(MySqlFixture mysql)
{
    private static readonly DateOnly Hoje = new(2026, 10, 7);

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(QuemDeveResponderNoBancoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<T> ComAsync<T>(ApiDeAcesso api, Func<IDestinatariosDoFormulario, Task<T>> acao)
    {
        using var escopo = api.Api.Services.CreateScope();
        return await acao(escopo.ServiceProvider.GetRequiredService<IDestinatariosDoFormulario>());
    }

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome, int? setorId = null, bool ativa = true, DateOnly? admissao = null) =>
        ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico(nome), Perfil.Funcionario, setorId, ativa, admissao);

    private static async Task<int> SetorSemGestorAsync(ApiDeAcesso api) => await ApoioDeTeste.CriarSetorAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("Setor sem gestor"));

    private static Task EscolherPessoasAsync(ApiDeAcesso api, int formularioId, params int[] pessoas) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO) VALUES {string.Join(", ", pessoas.Select(p => $"({formularioId}, {p})"))}");

    private static Task EscolherSetoresAsync(ApiDeAcesso api, int formularioId, params int[] setores) =>
        Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES {string.Join(", ", setores.Select(s => $"({formularioId}, {s})"))}");

    private static ParDeResposta P(int pessoa, int? avaliado = null) => new(pessoa, avaliado);

    // ---------------------------------------------------------------- formulário comum

    [Fact]
    public async Task Por_pessoas_so_as_ativas_respondem_e_quem_esta_em_admissao_entra()
    {
        var api = await ApiAsync();
        var ativa = await PessoaAsync(api, "Ativa");
        var inativa = await PessoaAsync(api, "Inativa", ativa: false);
        var emAdmissao = await PessoaAsync(api, "Em admissão", admissao: new DateOnly(2026, 11, 1));
        var id = await SemearAsync(api, Titulo("Por pessoas"), alvo: "PESSOAS");
        await EscolherPessoasAsync(api, id, ativa, inativa, emAdmissao);

        var quem = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal([P(ativa), P(emAdmissao)], quem.Pares.OrderBy(p => p.PessoaId));
        Assert.Equal((2, 2, false), (quem.Esperados, quem.Pessoas, quem.EhAvaliacao));
        Assert.Equal("2 pessoas escolhidas", QuemRespondeResumo.Texto(quem.Resumo));
    }

    [Fact]
    public async Task Por_setores_responde_o_setor_inteiro_com_o_gestor_so_ativos_e_com_a_admissao_comecada()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.NovoMembroAsync(api, setor, ativo: false);
        var emAdmissao = await PessoaAsync(api, "Admissão futura", setor.SetorId, admissao: new DateOnly(2026, 10, 8));
        var entraHoje = await PessoaAsync(api, "Entra hoje", setor.SetorId, admissao: Hoje);
        var id = await SemearAsync(api, Titulo("Por setores"), "PUBLICADO", alvo: "SETORES");
        await EscolherSetoresAsync(api, id, setor.SetorId);

        var quem = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal(new[] { setor.GestorFuncionarioId, a.Id, b.Id, entraHoje }.Order(), quem.Pares.Select(p => p.PessoaId)); // o gestor também responde; quem entra hoje já responde; quem entra amanhã ainda não
        Assert.DoesNotContain(quem.Pares, p => p.PessoaId == emAdmissao);
        Assert.Equal((4, 1, false), (quem.Esperados, quem.SetoresEscolhidos, quem.TodosOsSetores));
        Assert.Equal("1 setor (4 pessoas)", QuemRespondeResumo.Texto(quem.Resumo));
    }

    [Fact]
    public async Task Quem_entra_no_setor_depois_tambem_responde_e_quem_sai_deixa_de_responder()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var antigo = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("Setor que muda"), "PUBLICADO", alvo: "SETORES");
        await EscolherSetoresAsync(api, id, setor.SetorId);
        var antes = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!.Esperados;

        var novo = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var depoisDeEntrar = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;
        await AdvertenciasDeTeste.InativarAsync(api, antigo.Id);
        var depoisDeSair = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal((2, 3, 2), (antes, depoisDeEntrar.Esperados, depoisDeSair.Esperados)); // o gestor + antigo; + novo; sem o antigo
        Assert.Contains(depoisDeEntrar.Pares, p => p.PessoaId == novo.Id);
        Assert.DoesNotContain(depoisDeSair.Pares, p => p.PessoaId == antigo.Id);
    }

    [Fact]
    public async Task Todos_os_setores_e_quando_a_escolha_cobre_todos_os_setores_do_cadastro()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var todosOsSetores = (await Sql.ColunaAsync(api.Conexao, "SELECT ID_SETOR FROM SETORES")).Select(int.Parse).ToArray();
        var todos = await SemearAsync(api, Titulo("Todos os setores"), "PUBLICADO", alvo: "SETORES");
        await EscolherSetoresAsync(api, todos, todosOsSetores);
        var um = await SemearAsync(api, Titulo("Só um setor"), "PUBLICADO", alvo: "SETORES");
        await EscolherSetoresAsync(api, um, setor.SetorId);

        var a = (await ComAsync(api, d => d.ObterAsync(todos, Hoje, CancellationToken.None)))!;
        var b = (await ComAsync(api, d => d.ObterAsync(um, Hoje, CancellationToken.None)))!;

        Assert.Equal((true, false), (a.TodosOsSetores, b.TodosOsSetores));
        Assert.StartsWith("Todos os setores (", QuemRespondeResumo.Texto(a.Resumo), StringComparison.Ordinal);
    }

    [Fact]
    public async Task Por_link_e_sem_alvo_nao_tem_quem_deva_responder_e_formulario_inexistente_e_null()
    {
        var api = await ApiAsync();
        var porLink = await SemearAsync(api, Titulo("Entrevista"), "PUBLICADO", tipo: "ENTREVISTA", alvo: "LINK", token: new string('q', 43));
        var semAlvo = await SemearAsync(api, Titulo("Sem alvo"));

        var link = (await ComAsync(api, d => d.ObterAsync(porLink, Hoje, CancellationToken.None)))!;
        var vazio = (await ComAsync(api, d => d.ObterAsync(semAlvo, Hoje, CancellationToken.None)))!;

        Assert.Equal((0, 0, "Candidatos, por link", "Ainda não definido"), (link.Esperados, vazio.Esperados, QuemRespondeResumo.Texto(link.Resumo), QuemRespondeResumo.Texto(vazio.Resumo)));
        Assert.Null(await ComAsync(api, d => d.ObterAsync(2147483000, Hoje, CancellationToken.None)));
    }

    [Fact]
    public async Task Calcular_com_o_publico_do_corpo_nao_precisa_de_nada_gravado()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);

        var quem = await ComAsync(api, d => d.CalcularAsync(new PublicoDoFormulario(AlvoDoFormulario.Setores, TipoDeFormulario.Pesquisa, null, [setor.SetorId, setor.SetorId], []), Hoje, CancellationToken.None));

        Assert.Equal(new[] { setor.GestorFuncionarioId, a.Id }.Order(), quem.Pares.Select(p => p.PessoaId));
        Assert.Equal(1, quem.SetoresEscolhidos); // o setor repetido conta uma vez
    }

    // ---------------------------------------------------------------- avaliações

    [Fact]
    public async Task O_gestor_avalia_cada_pessoa_da_equipe_ativa_e_com_admissao_comecada_menos_ele_e_so_gestor_de_setor_conta_como_gestor()
    {
        var api = await ApiAsync();
        var s1 = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var s2 = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, s1);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, s1);
        await AdvertenciasDeTeste.NovoMembroAsync(api, s1, ativo: false);
        await PessoaAsync(api, "Em admissão", s1.SetorId, admissao: new DateOnly(2026, 12, 1));
        var m3 = await AdvertenciasDeTeste.NovoMembroAsync(api, s2);
        var comum = await PessoaAsync(api, "Escolhida por engano", s1.SetorId);
        var id = await SemearAsync(api, Titulo("Gestor avalia"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        await EscolherPessoasAsync(api, id, s1.GestorFuncionarioId, s2.GestorFuncionarioId, comum);

        var quem = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal(new[] { P(s1.GestorFuncionarioId, m1.Id), P(s1.GestorFuncionarioId, m2.Id), P(s1.GestorFuncionarioId, comum), P(s2.GestorFuncionarioId, m3.Id) }.OrderBy(p => p.PessoaId).ThenBy(p => p.AvaliadoId), quem.Pares);
        Assert.Equal((4, 2, true), (quem.Esperados, quem.Gestores, quem.EhAvaliacao)); // "2 gestores (4 avaliações)": a pessoa comum não é gestor, não conta, e não avalia ninguém
        Assert.Equal("2 gestores (4 avaliações)", QuemRespondeResumo.Texto(quem.Resumo));
    }

    [Fact]
    public async Task A_equipe_avalia_o_gestor_do_proprio_setor_setor_sem_gestor_fica_fora_e_o_gestor_nao_se_avalia()
    {
        var api = await ApiAsync();
        var comGestor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, comGestor);
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, comGestor);
        var semGestor = await SetorSemGestorAsync(api);
        await PessoaAsync(api, "Sem gestor", semGestor);
        var id = await SemearAsync(api, Titulo("Equipe avalia"), "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        await EscolherSetoresAsync(api, id, comGestor.SetorId, semGestor);

        var quem = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal(new[] { P(a.Id, comGestor.GestorFuncionarioId), P(b.Id, comGestor.GestorFuncionarioId) }.OrderBy(p => p.PessoaId), quem.Pares);
        Assert.Equal((2, 2), (quem.Esperados, quem.SetoresEscolhidos));
        Assert.Equal("2 setores (2 avaliações)", QuemRespondeResumo.Texto(quem.Resumo));
    }

    [Fact]
    public async Task A_equipe_avalia_o_gestor_por_pessoas_escolhidas_e_gestor_inativo_vale_como_setor_sem_gestor()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var semGestor = await SetorSemGestorAsync(api);
        var solta = await PessoaAsync(api, "De setor sem gestor", semGestor);
        var comGestorInativo = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var c = await AdvertenciasDeTeste.NovoMembroAsync(api, comGestorInativo);
        await AdvertenciasDeTeste.InativarAsync(api, comGestorInativo.GestorFuncionarioId);
        var id = await SemearAsync(api, Titulo("Por pessoas"), "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "EQUIPE_AVALIA_GESTOR");
        await EscolherPessoasAsync(api, id, a.Id, solta, c.Id, setor.GestorFuncionarioId);

        var quem = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal([P(a.Id, setor.GestorFuncionarioId)], quem.Pares); // o gestor escolhido não se avalia, e os outros dois não têm gestor ativo
        Assert.Equal(4, quem.PessoasEscolhidas); // as quatro pessoas escolhidas estão ativas: contam todas, tenham ou não gestor
    }

    // ---------------------------------------------------------------- a fotografia do anônimo

    [Fact]
    public async Task A_fotografia_congela_os_pares_do_anonimo_e_o_mesmo_formulario_com_nome_continua_calculado_na_hora()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var anonimo = await SemearAsync(api, Titulo("Anônimo por setor"), anonimo: true, alvo: "SETORES");
        var comNome = await SemearAsync(api, Titulo("Com nome por setor"), "PUBLICADO", alvo: "SETORES");
        await EscolherSetoresAsync(api, anonimo, setor.SetorId);
        await EscolherSetoresAsync(api, comNome, setor.SetorId);

        var fotografia = await ComAsync(api, d => d.FotografarAsync(anonimo, Hoje, CancellationToken.None));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE, DATA_PUBLICACAO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {anonimo}");
        var novo = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        await AdvertenciasDeTeste.InativarAsync(api, b.Id);

        var congelado = (await ComAsync(api, d => d.ObterAsync(anonimo, Hoje, CancellationToken.None)))!;
        var aoVivo = (await ComAsync(api, d => d.ObterAsync(comNome, Hoje, CancellationToken.None)))!;

        Assert.Equal(new[] { setor.GestorFuncionarioId, a.Id, b.Id }.Order(), fotografia.Pares.Select(p => p.PessoaId));
        Assert.Equal(fotografia.Pares, congelado.Pares); // quem entrou depois não entra, e quem saiu continua na fotografia: o número é fixo
        Assert.DoesNotContain(congelado.Pares, p => p.PessoaId == novo.Id);
        Assert.Equal((3, 1), (congelado.Esperados, congelado.SetoresEscolhidos));
        Assert.Equal(new[] { setor.GestorFuncionarioId, a.Id, novo.Id }.Order(), aoVivo.Pares.Select(p => p.PessoaId)); // com nome: b saiu e o novo entrou
        Assert.Equal(1, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {anonimo} AND ID_SETOR = {setor.SetorId}")); // a linha do setor ficou
        Assert.Equal(3, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {anonimo} AND ID_FUNCIONARIO IS NOT NULL"));
    }

    [Fact]
    public async Task Antes_de_publicar_o_anonimo_ainda_e_calculado_na_hora_e_a_fotografia_nao_se_repete_nem_deixa_linha_velha()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("Anônimo em rascunho"), anonimo: true, alvo: "SETORES");
        await EscolherSetoresAsync(api, id, setor.SetorId);
        var antes = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);

        var aoVivo = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!; // ainda não foi fotografado: vê o b
        await ComAsync(api, d => d.FotografarAsync(id, Hoje, CancellationToken.None));
        var outraFotografia = await ComAsync(api, d => d.FotografarAsync(id, Hoje, CancellationToken.None)); // a segunda fotografia substitui a primeira

        Assert.Equal((2, 3), (antes.Esperados, aoVivo.Esperados));
        Assert.Equal(new[] { setor.GestorFuncionarioId, a.Id, b.Id }.Order(), outraFotografia.Pares.Select(p => p.PessoaId));
        Assert.Equal(3, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_FUNCIONARIO IS NOT NULL"));
    }

    [Fact]
    public async Task A_fotografia_da_avaliacao_anonima_guarda_o_par_pessoa_e_gestor_e_as_pessoas_escolhidas_continuam_as_mesmas()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var a = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var b = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("Avaliação anônima"), anonimo: true, tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "EQUIPE_AVALIA_GESTOR");
        await EscolherPessoasAsync(api, id, a.Id, b.Id);

        var fotografia = await ComAsync(api, d => d.FotografarAsync(id, Hoje, CancellationToken.None));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE, DATA_PUBLICACAO = '2026-10-07 09:00:00' WHERE ID_FORMULARIO = {id}");
        var guardado = (await ComAsync(api, d => d.ObterAsync(id, Hoje, CancellationToken.None)))!;

        Assert.Equal(new[] { P(a.Id, setor.GestorFuncionarioId), P(b.Id, setor.GestorFuncionarioId) }.OrderBy(p => p.PessoaId), fotografia.Pares);
        Assert.Equal(fotografia.Pares, guardado.Pares);
        Assert.Equal((2, 2), (guardado.Esperados, guardado.PessoasEscolhidas));
        Assert.Equal(2, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO = {id} AND ID_AVALIADO = {setor.GestorFuncionarioId}"));
    }
}
