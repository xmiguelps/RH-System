using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Treinamentos.TreinamentosDeTeste;

namespace RHSystem.Api.IntegrationTests.Treinamentos;

/// <summary>
/// <c>encerrarTreinamento</c> e <c>excluirTreinamento</c>, no banco REAL com o relógio em 2026-10-07T12:42Z. Encerrar só vale em andamento, com confirmação (409 e os três números de pessoas ATIVAS) quando há pendente, e não mexe nas linhas nem nos certificados. Excluir é físico: 422 com certificado emitido (antes da confirmação e mesmo com <c>confirmar=true</c>), rascunho e agendado apagam direto,
/// em andamento ou encerrado pedem confirmação se há pendente; apaga os filhos na ordem certa, trava o treinamento contra uma conclusão no mesmo instante e só tira os PDFs do armazenamento depois do commit.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class EncerrarEExcluirTreinamentoTests(MySqlFixture mysql)
{
    private static string Rota(int id) => $"/api/rh/treinamentos/{id}";

    private async Task<ApiDeAcesso> ApiAsync()
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, nameof(EncerrarEExcluirTreinamentoTests));
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    private static async Task<HttpResponseMessage> EncerrarAsync(ApiDeAcesso api, int id, bool? confirmar = null) =>
        await (await api.ClienteDoRhAgoraAsync()).PostAsync($"{Rota(id)}/encerrar{(confirmar is null ? string.Empty : $"?confirmar={confirmar.ToString()!.ToLowerInvariant()}")}", null);

    private static async Task<HttpResponseMessage> ExcluirAsync(ApiDeAcesso api, int id, bool? confirmar = null) =>
        await (await api.ClienteDoRhAgoraAsync()).DeleteAsync($"{Rota(id)}{(confirmar is null ? string.Empty : $"?confirmar={confirmar.ToString()!.ToLowerInvariant()}")}");

    private static Task<int> PessoaAsync(ApiDeAcesso api, string nome) => ApoioDeTeste.CriarPessoaAsync(api.Conexao, Titulo(nome), Perfil.Funcionario, null, true, new DateOnly(2026, 1, 2));

    /// <summary>A linha da pessoa no treinamento, direto no banco (CONCLUIDO faz o trigger criar a linha do certificado).</summary>
    private static async Task LinhaAsync(ApiDeAcesso api, int treinamento, int pessoa, string status) =>
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento}, {pessoa}, '{status}')");

    private static async Task<int> PendenteAsync(ApiDeAcesso api, int treinamento, string status, bool ativa = true)
    {
        var pessoa = await PessoaAsync(api, $"ZEnc {status}");
        await LinhaAsync(api, treinamento, pessoa, status);
        if (!ativa)
        {
            await AdvertenciasDeTeste.InativarAsync(api, pessoa);
        }

        return pessoa;
    }

    private static async Task<long> RestoDoTreinamentoAsync(ApiDeAcesso api, int id) =>
        await ContarAsync(
            api,
            $"SELECT (SELECT COUNT(*) FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}) + (SELECT COUNT(*) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {id}) + (SELECT COUNT(*) FROM TREINAMENTOS_MODULOS_PROGRESSO p JOIN TREINAMENTOS_MODULOS m ON m.ID_MODULO = p.ID_MODULO WHERE m.ID_TREINAMENTO = {id}) + (SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id}) + (SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {id}) + (SELECT COUNT(*) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {id})");

    // ---------------------------------------------------------------- encerrar

    [Fact]
    public async Task Encerrar_em_andamento_sem_pendentes_grava_o_instante_e_mantem_linhas_e_certificados()
    {
        var api = await ApiAsync();
        var titulo = Titulo("ZEnc1");
        var id = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true);
        var pessoa = await PessoaAsync(api, "ZEnc1 Ana");
        await LinhaAsync(api, id, pessoa, "CONCLUIDO"); // o trigger cria o certificado

        var resposta = await EncerrarAsync(api, id);
        var corpo = await CorpoAsync(resposta);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("encerrarTreinamento");
        Assert.Equal(("ENCERRADO", "2026-10-07T12:42:00Z", true), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("encerradoEmUtc").GetString(), corpo.GetProperty("publicado").GetBoolean()));
        Assert.Equal("2026-10-07 09:42:00", await TextoAsync(api, $"SELECT DATE_FORMAT(ENCERRADO_EM, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}")); // o banco guarda -03:00
        Assert.Equal((1L, 1L), (await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id}"), await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {id}")));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou), (evento.Area, evento.Acao));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Encerrar_com_pendentes_sem_confirmar_devolve_409_com_os_tres_numeros_so_de_ativos_e_com_confirmar_encerra_sem_apagar_as_linhas()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZEnc2"), "EM_ANDAMENTO", obrigatorio: true);
        _ = await PendenteAsync(api, id, "EM_ANDAMENTO");
        _ = await PendenteAsync(api, id, "EM_ANDAMENTO");
        _ = await PendenteAsync(api, id, "INSCRITO");
        _ = await PendenteAsync(api, id, "INSCRITO");
        _ = await PendenteAsync(api, id, "INSCRITO");
        _ = await PendenteAsync(api, id, "INSCRITO", ativa: false); // inativa nunca conta
        _ = await PendenteAsync(api, id, "EM_ANDAMENTO", ativa: false);

        var sem = await EncerrarAsync(api, id);
        var falso = await EncerrarAsync(api, id, confirmar: false);
        var corpo = await CorpoAsync(sem);

        Assert.Equal([409, 409], new[] { sem, falso }.Select(r => (int)r.StatusCode));
        await sem.AfirmarConformeAoContratoAsync("encerrarTreinamento");
        Assert.Equal(("CONFIRMACAO_NECESSARIA", 5, 2, 3, "Confirmação necessária.", "5 pessoas ainda não concluíram este treinamento. Confirme para encerrar."), (Codigo(corpo), corpo.GetProperty("pendentes").GetInt32(), corpo.GetProperty("emAndamento").GetInt32(), corpo.GetProperty("naoIniciaram").GetInt32(), corpo.GetProperty("title").GetString(), corpo.GetProperty("detail").GetString()));
        Assert.Null(await TextoAsync(api, $"SELECT ENCERRADO_EM FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"));

        var confirmado = await EncerrarAsync(api, id, confirmar: true);

        Assert.Equal(200, (int)confirmado.StatusCode);
        Assert.Equal("ENCERRADO", (await CorpoAsync(confirmado)).GetProperty("situacao").GetString());
        Assert.Equal(7L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO = {id}")); // a linha fica no banco, também a de quem não iniciou
        Assert.Contains("(pendentes: 5)", Assert.Single(await EventosAsync(api, id)).Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task O_409_de_uma_pessoa_pendente_so_usa_o_singular()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZEnc3"), "EM_ANDAMENTO");
        _ = await PendenteAsync(api, id, "INSCRITO");

        var corpo = await CorpoAsync(await EncerrarAsync(api, id));

        Assert.Equal(("1 pessoa ainda não concluiu este treinamento. Confirme para encerrar.", 1, 0, 1), (corpo.GetProperty("detail").GetString(), corpo.GetProperty("pendentes").GetInt32(), corpo.GetProperty("emAndamento").GetInt32(), corpo.GetProperty("naoIniciaram").GetInt32()));
    }

    [Fact]
    public async Task Encerrar_fora_de_andamento_e_422_e_nao_muda_nada()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZEnc4 rascunho"));
        var agendado = await SemearAsync(api, Titulo("ZEnc4 agendado"), "AGENDADO");
        var encerrado = await SemearAsync(api, Titulo("ZEnc4 encerrado"), "ENCERRADO");

        var respostas = new[] { await EncerrarAsync(api, rascunho, true), await EncerrarAsync(api, agendado, true), await EncerrarAsync(api, encerrado, true) };

        Assert.Equal([422, 422, 422], respostas.Select(r => (int)r.StatusCode));
        await respostas[0].AfirmarConformeAoContratoAsync("encerrarTreinamento");
        await respostas[2].AfirmarConformeAoContratoAsync("encerrarTreinamento");
        Assert.Equal(["TREINAMENTO_NAO_ESTA_EM_ANDAMENTO", "TREINAMENTO_NAO_ESTA_EM_ANDAMENTO", "TREINAMENTO_ENCERRADO"], [Codigo(await CorpoAsync(respostas[0])), Codigo(await CorpoAsync(respostas[1])), Codigo(await CorpoAsync(respostas[2]))]);
        Assert.Equal("Este treinamento já foi encerrado.", (await CorpoAsync(respostas[2])).GetProperty("detail").GetString());
        Assert.Equal(0L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS WHERE ID_TREINAMENTO IN ({rascunho}, {agendado}) AND ENCERRADO_EM IS NOT NULL"));
        Assert.Equal("2026-10-01 00:00:00", await TextoAsync(api, $"SELECT DATE_FORMAT(ENCERRADO_EM, '%Y-%m-%d %H:%i:%s') FROM TREINAMENTOS WHERE ID_TREINAMENTO = {encerrado}")); // o encerramento antigo não foi regravado
    }

    [Fact]
    public async Task Encerrar_duas_vezes_ao_mesmo_tempo_da_um_200_e_um_422_e_um_so_evento()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZEnc5"), "EM_ANDAMENTO");

        var respostas = await Task.WhenAll(EncerrarAsync(api, id), EncerrarAsync(api, id), EncerrarAsync(api, id));

        Assert.Equal([200, 422, 422], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Single(await EventosAsync(api, id));
    }

    [Fact]
    public async Task Encerrar_e_excluir_inexistente_sao_404_sem_login_401_e_gestor_ou_funcionario_403()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZEnc6"), "EM_ANDAMENTO");

        var respostas = new[]
        {
            await EncerrarAsync(api, 987654), await ExcluirAsync(api, 987654),
            await api.Api.ClienteComo(api.Elenco.Gestor).PostAsync($"{Rota(id)}/encerrar", null), await api.Api.ClienteComo(api.Elenco.MembroA).DeleteAsync(Rota(id)),
            await api.Api.ClienteAnonimo().PostAsync($"{Rota(id)}/encerrar", null), await api.Api.ClienteAnonimo().DeleteAsync(Rota(id)),
        };

        Assert.Equal([404, 404, 403, 403, 401, 401], respostas.Select(r => (int)r.StatusCode));
        Assert.Equal(1L, await RestoDoTreinamentoAsync(api, id));
    }

    // ---------------------------------------------------------------- excluir

    [Fact]
    public async Task Excluir_rascunho_e_agendado_apaga_direto_tudo_e_o_pdf_do_modulo_so_depois_do_commit()
    {
        var api = await ApiAsync();
        var rascunho = await SemearAsync(api, Titulo("ZExc1 rascunho"));
        var agendado = await SemearAsync(api, Titulo("ZExc1 agendado"), "AGENDADO", obrigatorio: true);
        var caminho = $"treinamentos/{Guid.NewGuid():N}.pdf";
        api.Api.Armazenamento.Semear(caminho, [37, 80, 68, 70], ElencoDeTeste.Hoje);
        _ = await ModuloAsync(api, rascunho, 1, "PDF", caminho: caminho);
        _ = await ModuloAsync(api, agendado, 1);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_SETOR) VALUES ({agendado}, {await ApoioDeTeste.CriarSetorAsync(api.Conexao, Titulo("ZExc1 setor"))})");
        await LinhaAsync(api, agendado, await PessoaAsync(api, "ZExc1 Ana"), "INSCRITO"); // o obrigatório publicado já inscreveu gente, mas ninguém chegou a ver

        var rRascunho = await ExcluirAsync(api, rascunho);
        var rAgendado = await ExcluirAsync(api, agendado);

        Assert.Equal([204, 204], new[] { rRascunho, rAgendado }.Select(r => (int)r.StatusCode));
        await rRascunho.AfirmarConformeAoContratoAsync("excluirTreinamento");
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, rascunho) + await RestoDoTreinamentoAsync(api, agendado));
        Assert.False(api.Api.Armazenamento.Existe(caminho));
        Assert.Contains(caminho, api.Api.Armazenamento.Apagados);
        var evento = Assert.Single(await EventosAsync(api, agendado));
        Assert.Equal((AuditoriaArea.Treinamentos, AuditoriaAcao.Excluiu), (evento.Area, evento.Acao));
        Assert.Contains("(pendentes: 0)", evento.Descricao, StringComparison.Ordinal); // agendado não pede confirmação nem conta pendente
    }

    [Fact]
    public async Task Excluir_o_que_nao_emite_certificado_com_concluidos_nao_deixa_linha_em_nenhuma_das_seis_tabelas_nem_arquivo()
    {
        var api = await ApiAsync();
        var titulo = Titulo("ZExc2");
        var id = await SemearAsync(api, titulo, "EM_ANDAMENTO", obrigatorio: true, publico: "PESSOAS", emite: false);
        var caminho = $"treinamentos/{Guid.NewGuid():N}.pdf";
        api.Api.Armazenamento.Semear(caminho, [37, 80, 68, 70], ElencoDeTeste.Hoje);
        var modulo1 = await ModuloAsync(api, id, 1, "PDF", caminho: caminho);
        var modulo2 = await ModuloAsync(api, id, 2);
        var ana = await PessoaAsync(api, "ZExc2 Ana");
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_PUBLICO (ID_TREINAMENTO, ID_FUNCIONARIO) VALUES ({id}, {ana})");
        await LinhaAsync(api, id, ana, "CONCLUIDO"); // o trigger cria a linha do certificado, que não é "certificado emitido" quando o treinamento não emite
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO) VALUES ({modulo1}, {ana}), ({modulo2}, {ana})");
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {id}"));

        var resposta = await ExcluirAsync(api, id);

        Assert.Equal(204, (int)resposta.StatusCode);
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
        Assert.False(api.Api.Armazenamento.Existe(caminho));
        var evento = Assert.Single(await EventosAsync(api, id));
        Assert.Contains(titulo, evento.Descricao, StringComparison.Ordinal);
        Assert.Contains("(pendentes: 0)", evento.Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excluir_com_pendentes_pede_confirmacao_e_com_confirmar_apaga_tambem_o_andamento_de_quem_nao_terminou()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc3"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        var modulo = await ModuloAsync(api, id, 1);
        var comecou = await PendenteAsync(api, id, "EM_ANDAMENTO");
        _ = await PendenteAsync(api, id, "INSCRITO");
        _ = await PendenteAsync(api, id, "INSCRITO", ativa: false);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO TREINAMENTOS_MODULOS_PROGRESSO (ID_MODULO, ID_FUNCIONARIO) VALUES ({modulo}, {comecou})");

        var sem = await ExcluirAsync(api, id);
        var corpo = await CorpoAsync(sem);

        Assert.Equal(409, (int)sem.StatusCode);
        await sem.AfirmarConformeAoContratoAsync("excluirTreinamento");
        Assert.Equal(("CONFIRMACAO_NECESSARIA", 2, 1, 1, "2 pessoas ainda não concluíram este treinamento. Confirme para excluir."), (Codigo(corpo), corpo.GetProperty("pendentes").GetInt32(), corpo.GetProperty("emAndamento").GetInt32(), corpo.GetProperty("naoIniciaram").GetInt32(), corpo.GetProperty("detail").GetString()));
        Assert.Equal(1L, await ContarAsync(api, $"SELECT COUNT(*) FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}")); // nada foi apagado
        Assert.Empty(await EventosAsync(api, id));

        var confirmado = await ExcluirAsync(api, id, confirmar: true);

        Assert.Equal(204, (int)confirmado.StatusCode);
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
        Assert.Contains("(pendentes: 2)", Assert.Single(await EventosAsync(api, id)).Descricao, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Excluir_o_encerrado_sem_certificado_tambem_pede_confirmacao_quando_ha_pendente()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc4"), "ENCERRADO", obrigatorio: true);
        _ = await PendenteAsync(api, id, "INSCRITO");

        var sem = await ExcluirAsync(api, id);
        var com = await ExcluirAsync(api, id, confirmar: true);

        Assert.Equal([409, 204], new[] { sem, com }.Select(r => (int)r.StatusCode));
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
    }

    [Fact]
    public async Task Excluir_com_certificado_emitido_e_422_antes_da_confirmacao_e_mesmo_com_confirmar_e_nao_apaga_nada()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc5"), "ENCERRADO", obrigatorio: true, emite: true);
        _ = await ModuloAsync(api, id, 1);
        await LinhaAsync(api, id, await PessoaAsync(api, "ZExc5 Ana"), "CONCLUIDO");
        await LinhaAsync(api, id, await PessoaAsync(api, "ZExc5 Bia"), "CONCLUIDO");
        _ = await PendenteAsync(api, id, "INSCRITO"); // tem pendente, mas os certificados vêm primeiro: 422, não 409
        var antes = await RestoDoTreinamentoAsync(api, id);

        var sem = await ExcluirAsync(api, id);
        var com = await ExcluirAsync(api, id, confirmar: true);
        var corpo = await CorpoAsync(sem);

        Assert.Equal([422, 422], new[] { sem, com }.Select(r => (int)r.StatusCode));
        await sem.AfirmarConformeAoContratoAsync("excluirTreinamento");
        Assert.Equal(("TREINAMENTO_COM_CERTIFICADOS", 2, "Este treinamento já emitiu 2 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre."), (Codigo(corpo), corpo.GetProperty("certificadosEmitidos").GetInt32(), corpo.GetProperty("detail").GetString()));
        Assert.Equal(antes, await RestoDoTreinamentoAsync(api, id));
        Assert.Empty(await EventosAsync(api, id));
    }

    [Fact]
    public async Task Excluir_com_um_so_certificado_usa_o_singular()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc6"), "EM_ANDAMENTO", emite: true);
        await LinhaAsync(api, id, await PessoaAsync(api, "ZExc6 Ana"), "CONCLUIDO");

        var corpo = await CorpoAsync(await ExcluirAsync(api, id));

        Assert.Equal(("Este treinamento já emitiu 1 certificado e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.", 1), (corpo.GetProperty("detail").GetString(), corpo.GetProperty("certificadosEmitidos").GetInt32()));
    }

    [Fact]
    public async Task Falha_ao_apagar_o_pdf_depois_do_commit_nao_desfaz_a_exclusao()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc7"));
        var caminho = $"treinamentos/{Guid.NewGuid():N}.pdf";
        api.Api.Armazenamento.Semear(caminho, [37, 80, 68, 70], ElencoDeTeste.Hoje);
        _ = await ModuloAsync(api, id, 1, "PDF", caminho: caminho);
        api.Api.Armazenamento.FalharNaExclusao = new IOException("armazenamento fora do ar");

        try
        {
            var resposta = await ExcluirAsync(api, id);

            Assert.Equal(204, (int)resposta.StatusCode);
            Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
            Assert.True(api.Api.Armazenamento.Existe(caminho)); // sobra para a limpeza de sobras
        }
        finally
        {
            api.Api.Armazenamento.FalharNaExclusao = null;
        }
    }

    [Fact]
    public async Task Uma_conclusao_gravada_no_mesmo_instante_da_exclusao_nunca_vira_erro_cru_do_banco()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc8"), "EM_ANDAMENTO", obrigatorio: true, emite: false);
        _ = await ModuloAsync(api, id, 1);
        var ana = await PessoaAsync(api, "ZExc8 Ana");

        // a outra tela começa antes: uma transação aberta já tem uma linha nova de participante no treinamento (ela segura a linha do treinamento por chave estrangeira)
        await using var outra = new MySqlConnection(api.Conexao);
        await outra.OpenAsync();
        await using var transacao = await outra.BeginTransactionAsync();
        await using (var insere = new MySqlCommand($"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({id}, {ana}, 'INSCRITO')", outra, transacao))
        {
            await insere.ExecuteNonQueryAsync();
        }

        var exclusao = ExcluirAsync(api, id, confirmar: true);
        await Task.Delay(1500); // a exclusão espera a linha do treinamento
        Assert.False(exclusao.IsCompleted);
        await transacao.CommitAsync();
        var resposta = await exclusao;

        Assert.Equal(204, (int)resposta.StatusCode); // a exclusão esperou, viu a linha nova e a apagou junto: nada de 1451
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
    }

    [Fact]
    public async Task Excluir_duas_vezes_ao_mesmo_tempo_da_um_204_e_o_resto_404()
    {
        var api = await ApiAsync();
        var id = await SemearAsync(api, Titulo("ZExc9"));
        _ = await ModuloAsync(api, id, 1);

        var respostas = await Task.WhenAll(ExcluirAsync(api, id), ExcluirAsync(api, id), ExcluirAsync(api, id));

        var codigos = respostas.Select(r => (int)r.StatusCode).Order().ToList();
        Assert.Equal(204, codigos[0]);
        Assert.All(codigos.Skip(1), codigo => Assert.Equal(404, codigo));
        Assert.Equal(0L, await RestoDoTreinamentoAsync(api, id));
    }
}
