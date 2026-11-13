using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os casos de borda de RESPONDER uma avaliação de ponta a ponta, no banco REAL com o relógio em 2026-10-07T12:42Z (o resto da avaliação, o avaliado obrigatório, o que não é da equipe, a mesma pessoa duas vezes, o <c>restantes</c>, o cartão e "ver respostas", já tem teste nas classes de cada rota): na avaliação do
/// gestor, o setor SEM gestor não tem quem avaliar (a equipe dele não vê o formulário, não consegue responder e não entra na conta do RH); na avaliação ANÔNIMA cada pessoa participa uma vez por avaliado, as respostas ficam sem autor e a repetida é 409.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AvaliacoesTests(MySqlFixture mysql)
{
    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(AvaliacoesTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    [Fact]
    public async Task Na_avaliacao_do_gestor_o_setor_sem_gestor_nao_ve_nao_responde_e_nao_conta()
    {
        var api = await ApiAsync();
        var comGestor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var membroA = await AdvertenciasDeTeste.NovoMembroAsync(api, comGestor);
        var semGestor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("ZAv1 setor sem gestor"));
        var membroB = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, AdvertenciasDeTeste.NomeUnico("ZAv1 membro sem gestor"), Perfil.Funcionario, semGestor);
        var titulo = Titulo("ZAv1");
        var id = await SemearAsync(api, titulo, "PUBLICADO", tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {comGestor.SetorId}), ({id}, {semGestor})");
        var clienteA = await AdvertenciasDeTeste.ClienteDeAsync(api, membroA.Id);
        var clienteB = await AdvertenciasDeTeste.ClienteDeAsync(api, membroB);
        var corpo = Json(new { perguntaId = nota, valorEscala = 4 });

        var listaA = await CorpoAsync(await clienteA.GetAsync($"/api/eu/formularios?busca={Uri.EscapeDataString(titulo)}"));
        var listaB = await CorpoAsync(await clienteB.GetAsync($"/api/eu/formularios?busca={Uri.EscapeDataString(titulo)}"));
        var abrirB = await clienteB.GetAsync($"/api/eu/formularios/{id}");
        var responderB = await ResponderAsync(clienteB, id, Envio(corpo, comGestor.GestorFuncionarioId));
        var quemRespondeu = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/formularios/{id}/respondentes"));

        Assert.Equal((1, 0), (listaA.GetProperty("itens").GetArrayLength(), listaB.GetProperty("itens").GetArrayLength()));
        Assert.Equal((404, 404), ((int)abrirB.StatusCode, (int)responderB.StatusCode));
        Assert.Equal(0L, await LinhasAsync(api, id));
        Assert.Equal((1, 1), (quemRespondeu.GetProperty("total").GetInt32(), quemRespondeu.GetProperty("resumo").GetProperty("esperados").GetInt32())); // só o par (membro do setor com gestor, gestor)
    }

    [Fact]
    public async Task Na_avaliacao_anonima_cada_pessoa_participa_uma_vez_as_respostas_ficam_sem_autor_e_a_repetida_e_409()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var id = await SemearAsync(api, Titulo("ZAv2"), "PUBLICADO", anonimo: true, tipo: "AVALIACAO", alvo: "SETORES", sentido: "EQUIPE_AVALIA_GESTOR");
        var nota = await PerguntaAsync(api, id, "Nota", "ESCALA", 1, true);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor.SetorId})");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO) VALUES ({id}, {m1.Id}, {setor.GestorFuncionarioId}), ({id}, {m2.Id}, {setor.GestorFuncionarioId})"); // a fotografia
        var corpo = Json(new { perguntaId = nota, valorEscala = 5 });
        var cliente1 = await AdvertenciasDeTeste.ClienteDeAsync(api, m1.Id);
        var cliente2 = await AdvertenciasDeTeste.ClienteDeAsync(api, m2.Id);

        var primeira = await ResponderAsync(cliente1, id, Envio(corpo, setor.GestorFuncionarioId));
        var segunda = await ResponderAsync(cliente2, id, Envio(corpo, setor.GestorFuncionarioId));
        var repetida = await ResponderAsync(cliente1, id, Envio(corpo, setor.GestorFuncionarioId));

        Assert.Equal([201, 201, 409], new[] { primeira, segunda, repetida }.Select(r => (int)r.StatusCode));
        Assert.Equal("2,2", await TextoAsync(api, $"SELECT CONCAT(COUNT(*), ',', COUNT(DISTINCT ID_FUNCIONARIO)) FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {id}")); // uma participação por pessoa
        Assert.Equal("2,2,0", await TextoAsync(api, $"SELECT CONCAT(COUNT(*), ',', SUM(ANONIMO), ',', COUNT(ID_FUNCIONARIO)) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {id}")); // as respostas: anônimas e sem autor
    }
}
