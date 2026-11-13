using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Formularios.Construtor;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.FormulariosDeTeste;
using static RHSystem.Api.IntegrationTests.Formularios.RespostasDeTeste;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// Os casos que o controle de mutação do incremento 5a (tarefas 4 a 10) mostrou sem teste: editar detalhes de uma pesquisa não mexe no prazo e a Sugestão limpa o prazo mesmo sem recorrência; mudar o tipo de um publicado que recebeu resposta em outra aba é recusado; o prazo no dia da publicação vale; a Sugestão não pede
/// confirmação para excluir; a taxa é nula sem ninguém esperado; 200 pessoas valem e 201 não; a pessoa de número 1 não é "menor que 1"; editar as alternativas de uma pergunta não mexe nas de outra; excluir um formulário não cancela as tarefas de outro.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MutantesDoIncremento5aTests(MySqlFixture mysql)
{
    private const string Rota = "/api/rh/formularios";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(MutantesDoIncremento5aTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static object Detalhes(string titulo, string tipo = "PESQUISA") => new { titulo, tipo, anonimo = false };

    private static async Task<HttpResponseMessage> EditarAsync(ApiDeAcesso api, int id, object corpo) => await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}", corpo);

    // ---------------------------------------------------------------- detalhes

    [Fact]
    public async Task Editar_o_titulo_de_uma_pesquisa_nao_apaga_o_prazo_nem_a_recorrencia()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Pesquisa com prazo"), alvo: "PESSOAS", prazo: "2026-10-30");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FORMULARIOS SET RECORRENCIA_INTERVALO = 1, RECORRENCIA_UNIDADE = 'MES' WHERE ID_FORMULARIO = {id}");

        var resposta = await EditarAsync(api, id, Detalhes(Titulo("Pesquisa renomeada")));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal(("2026-10-30", "1", "MES"), (await TextoAsync(api, $"SELECT DATE_FORMAT(DATA_PRAZO, '%Y-%m-%d') FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT RECORRENCIA_INTERVALO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"), await TextoAsync(api, $"SELECT RECORRENCIA_UNIDADE FROM FORMULARIOS WHERE ID_FORMULARIO = {id}")));
    }

    [Fact]
    public async Task Virar_Sugestao_limpa_o_prazo_mesmo_quando_nao_ha_recorrencia()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Só com prazo"), alvo: "PESSOAS", prazo: "2026-10-30");

        var resposta = await EditarAsync(api, id, Detalhes(Titulo("Sugestões"), "OUTRO"));

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Null(await TextoAsync(api, $"SELECT DATA_PRAZO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Mudar_o_tipo_de_um_publicado_que_recebe_resposta_em_outra_aba_e_recusado_e_o_tipo_nao_muda()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Publicado"), "PUBLICADO", alvo: "PESSOAS");
        var pergunta = await PerguntaAsync(api, id, "Como foi?");
        var cliente = await ClienteComOutraAbaAsync(api, () => FormulariosDeTeste.ResponderAsync(api, id, pergunta, api.Elenco.MembroA.FuncionarioId));

        var resposta = await cliente.PutAsJsonAsync($"{Rota}/{id}", Detalhes(Titulo("Virou admissão"), "ADMISSAO"));

        Assert.Equal(422, (int)resposta.StatusCode);
        Assert.Equal("FORMULARIO_JA_RESPONDIDO", Codigo(await CorpoAsync(resposta)));
        Assert.Equal("PESQUISA", await TextoAsync(api, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    // ---------------------------------------------------------------- publicação guardada

    [Fact]
    public async Task O_prazo_no_proprio_dia_da_publicacao_de_um_agendado_vale_e_o_dia_anterior_nao()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Agendado"), "AGENDADO", alvo: "PESSOAS");
        await EscolherPessoasAsync(api, id, api.Elenco.MembroA.FuncionarioId);
        object Corpo(string prazo) => new { alvo = "PESSOAS", setorIds = Array.Empty<int>(), funcionarioIds = new[] { api.Elenco.MembroA.FuncionarioId }, prazo, lembreteAutomatico = true };

        var mesmoDia = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/publicacao", Corpo("2026-10-20")); // o agendado publica em 20/10
        var diaAntes = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/publicacao", Corpo("2026-10-19"));

        Assert.Equal((200, 400), ((int)mesmoDia.StatusCode, (int)diaAntes.StatusCode));
    }

    // ---------------------------------------------------------------- destinatários

    [Fact]
    public async Task Duzentas_pessoas_valem_e_duzentas_e_uma_e_400()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Rascunho"), alvo: "PESSOAS");
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        var rh = await api.ClienteDoRhAgoraAsync();

        var duzentas = await rh.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = Enumerable.Repeat(pessoa, 200) });
        var duzentasEUma = await rh.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = Enumerable.Repeat(pessoa, 201) });

        Assert.Equal((200, 400), ((int)duzentas.StatusCode, (int)duzentasEUma.StatusCode));
        Assert.Equal(["Escolha de 1 a 200 pessoas."], (await CorpoAsync(duzentasEUma)).GetProperty("errors").GetProperty("funcionarioIds").EnumerateArray().Select(e => e.GetString()));
    }

    [Fact]
    public async Task A_pessoa_de_numero_1_nao_e_numero_invalido_e_o_zero_e()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Rascunho"), alvo: "PESSOAS");
        var rh = await api.ClienteDoRhAgoraAsync();
        var primeiraAtiva = await TextoAsync(api, "SELECT ATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = 1");

        var zero = await rh.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = new[] { 0 } });
        var um = await rh.PostAsJsonAsync($"{Rota}/{id}/destinatarios", new { funcionarioIds = new[] { 1 } });

        Assert.Equal(400, (int)zero.StatusCode);
        Assert.Equal(["Os números das pessoas começam em 1."], (await CorpoAsync(zero)).GetProperty("errors").GetProperty("funcionarioIds").EnumerateArray().Select(e => e.GetString()));
        Assert.Equal(primeiraAtiva == "True" ? 200 : (primeiraAtiva is null ? 400 : 422), (int)um.StatusCode); // a pessoa 1 entra, ou é recusada pelo que ela é (inativa, inexistente), nunca por "número inválido"
        if (primeiraAtiva == "True")
        {
            Assert.Equal(1, (await CorpoAsync(um)).GetProperty("adicionados").GetInt32());
        }
        else
        {
            Assert.DoesNotContain("começam em 1", await um.Content.ReadAsStringAsync(), StringComparison.Ordinal);
        }
    }

    // ---------------------------------------------------------------- lista e exclusão

    [Fact]
    public async Task A_taxa_da_lista_e_nula_quando_nao_ha_ninguem_esperado()
    {
        var api = await ApiAsync();
        var setorVazio = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("Setor sem ninguém"));
        var titulo = Titulo("ZMut Vazio");
        var id = await SemearAsync(api, titulo, "PUBLICADO", alvo: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setorVazio})");

        var lista = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?busca={Uri.EscapeDataString(titulo)}"));

        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray());
        Assert.Equal((0, 0, JsonValueKind.Null), (item.GetProperty("respondidos").GetInt32(), item.GetProperty("esperados").GetInt32(), item.GetProperty("taxaDeRespostaPercentual").ValueKind));
    }

    [Fact]
    public async Task Excluir_uma_Sugestao_publicada_para_pessoas_nao_pede_confirmacao_porque_ninguem_e_obrigado_a_responder()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Sugestões"), "PUBLICADO", tipo: "OUTRO", alvo: "PESSOAS");
        await PerguntaAsync(api, id, "Sua ideia");
        await EscolherPessoasAsync(api, id, api.Elenco.MembroA.FuncionarioId, api.Elenco.MembroB.FuncionarioId);

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{id}");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS WHERE ID_FORMULARIO = {id}"));
    }

    [Fact]
    public async Task Excluir_um_formulario_nao_cancela_as_tarefas_de_outro()
    {
        var api = await ApiAsync();
        var excluido = await SemearAsync(api, Titulo("Vai embora"), "AGENDADO", alvo: "PESSOAS", prazo: "2026-11-17");
        var fica = await SemearAsync(api, Titulo("Fica"), "AGENDADO", alvo: "PESSOAS", prazo: "2026-11-17");
        using (var escopo = api.Api.Services.CreateScope())
        {
            var agendamento = escopo.ServiceProvider.GetRequiredService<IAgendamentoDoFormulario>();
            foreach (var formulario in new[] { excluido, fica })
            {
                await agendamento.AgendarPublicacaoAsync(formulario, new DateTime(2026, 10, 20, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None);
                await agendamento.AgendarEncerramentoAsync(formulario, new DateTime(2026, 11, 18, 3, 0, 0, DateTimeKind.Utc), CancellationToken.None);
            }
        }

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{excluido}");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal((2L, 0L), (await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {fica} AND SITUACAO = 'PENDENTE'"), await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {fica} AND SITUACAO = 'CANCELADA'")));
        Assert.Equal((0L, 2L), (await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {excluido} AND SITUACAO = 'PENDENTE'"), await ContarAsync(api, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE REFERENCIA_ID = {excluido} AND SITUACAO = 'CANCELADA'")));
    }

    [Fact]
    public async Task Excluir_um_formulario_nao_apaga_os_arquivos_as_respostas_nem_as_alternativas_de_outro()
    {
        var api = await ApiAsync();
        var pessoa = api.Elenco.MembroA.FuncionarioId;
        async Task<(int Id, string Caminho, int Pergunta)> ComArquivoAsync(string prefixo)
        {
            var formulario = await SemearAsync(api, Titulo(prefixo), "PUBLICADO", alvo: "PESSOAS");
            var multipla = await PerguntaAsync(api, formulario, "Gostou?", "MULTIPLA_ESCOLHA", 1, false, "Sim", "Não");
            var anexo = await PerguntaAsync(api, formulario, "Comprovante", "ARQUIVO", 2);
            var caminho = RHSystem.Application.Arquivos.CaminhosDeArquivo.Novo("formularios", "pdf");
            await api.Api.Armazenamento.GravarAsync(caminho, new MemoryStream(ArquivosDeTeste.Pdf()), "application/pdf", CancellationToken.None);
            await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_RESPOSTAS (ID_FORMULARIO_PERGUNTA, ID_FORMULARIO, ID_FUNCIONARIO, RESPOSTA, DATA_RESPOSTA, ANONIMO, CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL) VALUES ({anexo}, {formulario}, {pessoa}, 'a.pdf', '2026-10-05 10:00:00', 0, '{caminho}', 'a.pdf')");
            return (formulario, caminho, multipla);
        }

        var vai = await ComArquivoAsync("Vai embora");
        var fica = await ComArquivoAsync("Fica");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota}/{vai.Id}?confirmar=true");

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal((false, true), (api.Api.Armazenamento.Existe(vai.Caminho), api.Api.Armazenamento.Existe(fica.Caminho))); // só o arquivo do excluído sai do armazenamento
        Assert.Equal((0L, 1L), (await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {vai.Id}"), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO = {fica.Id}")));
        Assert.Equal((0L, 2L), (await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {vai.Pergunta}"), await ContarAsync(api, $"SELECT COUNT(*) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {fica.Pergunta}"))); // as alternativas do outro ficam
    }

    // ---------------------------------------------------------------- quem deve responder

    [Fact]
    public async Task O_resumo_de_quem_responde_da_avaliacao_do_gestor_por_pessoas_conta_avaliacoes()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        var m1 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var m2 = await AdvertenciasDeTeste.NovoMembroAsync(api, setor);
        var titulo = Titulo("ZMut avaliação do gestor");
        var id = await SemearAsync(api, titulo, "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "EQUIPE_AVALIA_GESTOR");
        await EscolherPessoasAsync(api, id, m1.Id, m2.Id);

        var lista = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?busca={Uri.EscapeDataString(titulo)}"));

        var item = Assert.Single(lista.GetProperty("itens").EnumerateArray());
        Assert.Equal(("2 pessoas escolhidas (2 avaliações)", 2), (item.GetProperty("quemRespondeResumo").GetString(), item.GetProperty("esperados").GetInt32()));
    }

    [Fact]
    public async Task Quem_e_admitido_hoje_ja_e_do_setor_para_responder_e_quem_so_entra_amanha_nao()
    {
        var api = await ApiAsync();
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZMut setor"));
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut ontem"), RHSystem.Domain.Autenticacao.Perfil.Funcionario, setor, admissao: new DateOnly(2026, 10, 6));
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut hoje"), RHSystem.Domain.Autenticacao.Perfil.Funcionario, setor, admissao: new DateOnly(2026, 10, 7));
        await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo("ZMut amanhã"), RHSystem.Domain.Autenticacao.Perfil.Funcionario, setor, admissao: new DateOnly(2026, 10, 8));
        var titulo = Titulo("ZMut por setor");
        var id = await SemearAsync(api, titulo, "PUBLICADO", alvo: "SETORES");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FORMULARIOS_DESTINATARIOS (ID_FORMULARIO, ID_SETOR) VALUES ({id}, {setor})");

        var lista = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?busca={Uri.EscapeDataString(titulo)}"));

        Assert.Equal(2, Assert.Single(lista.GetProperty("itens").EnumerateArray()).GetProperty("esperados").GetInt32()); // quem entra amanhã ainda não
    }

    [Fact]
    public async Task O_gestor_avalia_quem_e_admitido_hoje_e_nao_quem_so_entra_amanha()
    {
        var api = await ApiAsync();
        var setor = await AdvertenciasDeTeste.NovoSetorAsync(api);
        foreach (var (nome, dia) in new[] { ("ontem", new DateOnly(2026, 10, 6)), ("hoje", new DateOnly(2026, 10, 7)), ("amanhã", new DateOnly(2026, 10, 8)) })
        {
            var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo($"ZMut equipe {nome}"), RHSystem.Domain.Autenticacao.Perfil.Funcionario, setor.SetorId, admissao: dia);
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {setor.GestorId} WHERE ID_FUNCIONARIO = {id}");
        }

        var titulo = Titulo("ZMut avaliação da equipe");
        var avaliacao = await SemearAsync(api, titulo, "PUBLICADO", tipo: "AVALIACAO", alvo: "PESSOAS", sentido: "GESTOR_AVALIA_EQUIPE");
        await EscolherPessoasAsync(api, avaliacao, setor.GestorFuncionarioId);

        var lista = await CorpoAsync(await (await api.ClienteDoRhAgoraAsync()).GetAsync($"{Rota}?busca={Uri.EscapeDataString(titulo)}"));

        Assert.Equal(2, Assert.Single(lista.GetProperty("itens").EnumerateArray()).GetProperty("esperados").GetInt32()); // ontem e hoje; amanhã ainda não
    }

    // ---------------------------------------------------------------- perguntas

    [Fact]
    public async Task Editar_as_alternativas_de_uma_pergunta_nao_mexe_na_ordem_das_alternativas_de_outra()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("Duas múltiplas"), alvo: "PESSOAS");
        var a = await PerguntaAsync(api, id, "Pergunta A", "MULTIPLA_ESCOLHA", 1, false, "a1", "a2", "a3");
        var b = await PerguntaAsync(api, id, "Pergunta B", "MULTIPLA_ESCOLHA", 2, false, "b1", "b2");
        var idsDeA = (await Sql.ColunaAsync(api.Conexao, $"SELECT ID_ALTERNATIVA FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {a} ORDER BY ORDEM")).Select(int.Parse).ToList();
        var corpo = new
        {
            texto = "Pergunta A",
            tipo = "MULTIPLA_ESCOLHA",
            obrigatoria = false,
            alternativas = new object[] { new { id = idsDeA[2], texto = "a3" }, new { id = idsDeA[0], texto = "a1" }, new { id = idsDeA[1], texto = "a2" } },
        };

        var resposta = await (await api.ClienteDoRhAgoraAsync()).PutAsJsonAsync($"{Rota}/{id}/perguntas/{a}", corpo);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("a3,a1,a2", await TextoAsync(api, $"SELECT GROUP_CONCAT(TEXTO_ALTERNATIVA ORDER BY ORDEM) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {a}"));
        Assert.Equal("1,2", await TextoAsync(api, $"SELECT GROUP_CONCAT(ORDEM ORDER BY ORDEM) FROM FORMULARIOS_PERGUNTAS_ALTERNATIVAS WHERE ID_FORMULARIO_PERGUNTA = {b}")); // a outra pergunta não mexeu
    }
}
