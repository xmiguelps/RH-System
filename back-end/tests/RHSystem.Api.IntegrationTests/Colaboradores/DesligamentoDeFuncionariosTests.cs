using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Domain.Agendamento;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Colaboradores;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;
using static RHSystem.Api.IntegrationTests.Colaboradores.DesligamentoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// <c>IDesligamentoDeFuncionarios</c> (agendar, cancelar e efetivar) chamado direto, no banco REAL, com o relógio falso em 2026-10-07T12:42Z (09:42 em Brasília). O serviço abre a própria transação;
/// o ator é o RH (<c>NoEscopoAsync</c>) ou o Sistema (a tarefa e a recuperação).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class DesligamentoDeFuncionariosTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => ComecarAsync(mysql, nameof(DesligamentoDeFuncionariosTests));

    private static Task<PessoaDoTeste> PessoaAsync(ApiDeAcesso api, string sufixo, bool ativa = true, TipoDeLogin login = TipoDeLogin.SenhaDefinida) => api.NovaPessoaAsync(sufixo, ativa, login: login);

    [Fact]
    public async Task Agendar_para_13_11_deixa_AGENDADO_a_pessoa_ativa_e_cria_a_tarefa_para_2026_11_14T03_00Z()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "agendar1");

        var resultado = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 13));

        Assert.False(resultado.EfetivadoNaHora);
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var linha = await db.DesligamentosAgendados.AsNoTracking().SingleAsync(d => d.FuncionarioId == pessoa.FuncionarioId);
        Assert.Equal(resultado.DesligamentoId, linha.Id);
        Assert.Equal((SituacaoDoDesligamento.Agendado, new DateOnly(2026, 11, 13), (DateTime?)null), (linha.Situacao, linha.UltimoDiaTrabalho, linha.EfetivadoEm));
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Null(await DataDeDesligamentoAsync(api, pessoa.FuncionarioId));

        var tarefa = await db.TarefasAgendadas.AsNoTracking().SingleAsync(t => t.ReferenciaId == resultado.DesligamentoId && t.Tipo == "EFETIVAR_DESLIGAMENTO");
        Assert.Equal(($"EFETIVAR_DESLIGAMENTO:desligamento={resultado.DesligamentoId}", new DateTime(2026, 11, 14, 3, 0, 0, DateTimeKind.Utc), SituacaoDaTarefa.Pendente), (tarefa.ChaveDeIdempotencia, tarefa.ExecutarApos, tarefa.Situacao));
    }

    [Fact]
    public async Task Ultimo_dia_hoje_nao_efetiva_porque_o_dia_seguinte_so_comeca_em_2026_10_08T03_00Z()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "hoje1");

        var resultado = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 7)); // hoje em Brasília

        Assert.False(resultado.EfetivadoNaHora);
        Assert.Equal("AGENDADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var tarefa = await db.TarefasAgendadas.AsNoTracking().SingleAsync(t => t.ReferenciaId == resultado.DesligamentoId);
        Assert.Equal(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc), tarefa.ExecutarApos);
    }

    [Fact]
    public async Task Ultimo_dia_no_passado_efetiva_na_hora_com_DATA_DESLIGAMENTO_do_passado_e_o_trigger_desativa_login_beneficios_e_ferias_futuras()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "passado1");
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Beneficio deslig passado1");
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.FuncionarioId, beneficio);
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS) VALUES ({pessoa.FuncionarioId}, '2026-01-02', '2027-01-01', '2027-03-01', '2027-03-15', 'APROVADA')");

        var resultado = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 5));

        Assert.True(resultado.EfetivadoNaHora);
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        Assert.False(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal("2026-10-05", await DataDeDesligamentoAsync(api, pessoa.FuncionarioId)); // o ÚLTIMO DIA de trabalho, que é do passado
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM USUARIOS WHERE ID_USUARIO = {pessoa.UsuarioId}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT BENEFICIO_ATIVO FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Equal("CANCELADA", await Sql.EscalarAsync(api.Conexao, $"SELECT STATUS_FERIAS FROM FERIAS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Equal(0, await TarefasDoDesligamentoAsync(api, resultado.DesligamentoId)); // efetivou na hora: não sobra tarefa
    }

    [Fact]
    public async Task Virada_do_dia_ultimo_dia_07_10_as_23h30_de_Brasilia_nao_efetiva_e_as_00h00_de_08_10_sim()
    {
        var api = await ApiAsync();
        var antes = await PessoaAsync(api, "virada1");
        var depois = await PessoaAsync(api, "virada2");

        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 2, 30, 0, DateTimeKind.Utc)); // 23:30 de 07/10 em Brasília
        var naoEfetivou = await AgendarAsync(api, antes.FuncionarioId, new DateOnly(2026, 10, 7));
        api.Api.Relogio.Fixar(new DateTime(2026, 10, 8, 3, 0, 0, DateTimeKind.Utc)); // 00:00 de 08/10
        var efetivou = await AgendarAsync(api, depois.FuncionarioId, new DateOnly(2026, 10, 7));

        Assert.False(naoEfetivou.EfetivadoNaHora);
        Assert.True(await AtivoAsync(api, antes.FuncionarioId));
        Assert.True(efetivou.EfetivadoNaHora);
        Assert.False(await AtivoAsync(api, depois.FuncionarioId));
    }

    [Fact]
    public async Task Efetivar_gera_um_unico_evento_Inativou_com_o_ultimo_dia_e_nenhum_evento_do_login_dos_beneficios_nem_das_ferias()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "evento1");
        var beneficio = await SementeDoCadastro.BeneficioAsync(api.Conexao, "Beneficio deslig evento1");
        await SementeDoCadastro.BeneficioDoColaboradorAsync(api.Conexao, pessoa.FuncionarioId, beneficio);

        await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 5));

        var eventos = await EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.FuncionarioId);
        var inativou = Assert.Single(eventos); // um clique, um evento
        Assert.Equal((AuditoriaArea.Colaboradores, AuditoriaAcao.Inativou, api.Elenco.Rh.FuncionarioId), (inativou.Area, inativou.Acao, inativou.FuncionarioAtorId));
        Assert.Equal($"Inativação de {pessoa.Nome}", inativou.Descricao);
        var ultimoDia = Assert.Single(inativou.Mudancas, m => m.Campo == "Último dia de trabalho");
        Assert.Equal(((string?)null, "05/10/2026"), (ultimoDia.ValorAntes, ultimoDia.ValorDepois));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO LIKE '%{pessoa.Nome}%' AND DESCRICAO <> 'Inativação de {pessoa.Nome}'")); // nada do login, dos benefícios nem das férias
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE IN ('USUARIOS', 'BENEFICIOS_FUNCIONARIOS', 'FERIAS')"));
    }

    [Fact]
    public async Task Agendar_sem_efetivar_grava_so_o_evento_explicito_Desligamento_agendado_e_nenhum_Inativou()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "evento2");

        await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 13));

        var evento = Assert.Single(await EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.FuncionarioId));
        Assert.Equal((AuditoriaArea.Colaboradores, AuditoriaAcao.Inativou, $"Desligamento de {pessoa.Nome} agendado"), (evento.Area, evento.Acao, evento.Descricao));
        var mudanca = Assert.Single(evento.Mudancas);
        Assert.Equal(("Último dia de trabalho", (string?)null, "13/11/2026"), (mudanca.Campo, mudanca.ValorAntes, mudanca.ValorDepois));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE DESCRICAO = 'Inativação de {pessoa.Nome}'"));
    }

    [Fact]
    public async Task Ultimo_dia_antes_da_admissao_ou_9999_12_31_devolve_400_em_ultimoDiaDeTrabalho_e_nao_500()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "invalido1"); // admitida em 2026-01-02

        foreach (var dia in new[] { new DateOnly(2026, 1, 1), DateOnly.MaxValue, new DateOnly(2036, 10, 8), default })
        {
            var erro = await Assert.ThrowsAsync<ErroDeValidacaoException>(() => AgendarAsync(api, pessoa.FuncionarioId, dia));
            Assert.Equal(new[] { "ultimoDiaDeTrabalho" }, erro.Erros.Keys);
        }

        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));

        // As bordas que valem: o dia da admissão e dez anos à frente (07/10/2036).
        Assert.NotNull(await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 1, 2)));
        var outra = await PessoaAsync(api, "invalido2");
        Assert.NotNull(await AgendarAsync(api, outra.FuncionarioId, new DateOnly(2036, 10, 7)));
    }

    [Fact]
    public async Task Agendar_de_inativo_ou_de_quem_ja_tem_desligamento_vigente_devolve_409_DESLIGAMENTO_JA_REGISTRADO()
    {
        var api = await ApiAsync();
        var inativo = await PessoaAsync(api, "conflito1", ativa: false);
        var agendada = await PessoaAsync(api, "conflito2");
        var efetivada = await PessoaAsync(api, "conflito3");
        await AgendarAsync(api, agendada.FuncionarioId, new DateOnly(2026, 11, 13));
        await AgendarAsync(api, efetivada.FuncionarioId, new DateOnly(2026, 10, 5));

        foreach (var id in new[] { inativo.FuncionarioId, agendada.FuncionarioId, efetivada.FuncionarioId })
        {
            var conflito = await Assert.ThrowsAsync<ConflitoException>(() => AgendarAsync(api, id, new DateOnly(2026, 12, 1)));
            Assert.Equal("DESLIGAMENTO_JA_REGISTRADO", conflito.Codigo);
        }

        await Assert.ThrowsAsync<NaoEncontradoException>(() => AgendarAsync(api, 987_654, new DateOnly(2026, 12, 1)));
    }

    [Fact]
    public async Task Dois_cliques_em_Inativar_dao_um_200_e_um_409_com_o_codigo_e_nenhum_500()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "cliques1");
        using var barreira = new Barrier(20);

        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ => EmParalelo(
            barreira,
            async () =>
            {
                try
                {
                    await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 13));
                    return (Exception?)null;
                }
                catch (Exception erro)
                {
                    return erro;
                }
            })));

        Assert.Equal(1, resultados.Count(r => r is null)); // um clique ganha
        var perdedores = resultados.Where(r => r is not null).ToList();
        Assert.Equal(19, perdedores.Count);
        Assert.All(perdedores, r => Assert.Equal("DESLIGAMENTO_JA_REGISTRADO", Assert.IsType<ConflitoException>(r).Codigo)); // os outros 19 recebem o 409, nunca um 500
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
    }

    [Fact]
    public async Task Cancelar_AGENDADO_vira_CANCELADO_cancela_a_tarefa_e_a_pessoa_continua_ativa_e_pode_agendar_de_novo()
    {
        var api = await ApiAsync();
        var pessoa = await PessoaAsync(api, "cancelar1");
        var primeiro = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 13));

        await CancelarAsync(api, pessoa.FuncionarioId);

        Assert.Equal("CANCELADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        await using var db = ContextoDeTeste.Novo(api.Conexao);
        Assert.Equal(SituacaoDaTarefa.Cancelada, (await db.TarefasAgendadas.AsNoTracking().SingleAsync(t => t.ReferenciaId == primeiro.DesligamentoId)).Situacao);
        var linha = await db.DesligamentosAgendados.AsNoTracking().SingleAsync(d => d.Id == primeiro.DesligamentoId);
        Assert.Equal((DateTime?)Agora, linha.CanceladoEm); // o instante do relógio falso, não o UTC do banco

        var segundo = await AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 20)); // depois de CANCELADO a pessoa pode ter outro
        Assert.NotEqual(primeiro.DesligamentoId, segundo.DesligamentoId);
        Assert.Equal("AGENDADO", await SituacaoAsync(api, pessoa.FuncionarioId));

        var evento = Assert.Single(await EventosAsync(api.Conexao, "FUNCIONARIOS", pessoa.FuncionarioId), e => e.Descricao == $"Cancelamento do desligamento de {pessoa.Nome}");
        Assert.Equal((AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou), (evento.Area, evento.Acao));
    }

    [Fact]
    public async Task Cancelar_EFETIVADO_ou_inexistente_devolve_422_SEM_DESLIGAMENTO_AGENDADO()
    {
        var api = await ApiAsync();
        var efetivada = await PessoaAsync(api, "cancelar2");
        var semDesligamento = await PessoaAsync(api, "cancelar3");
        await AgendarAsync(api, efetivada.FuncionarioId, new DateOnly(2026, 10, 5));

        foreach (var id in new[] { efetivada.FuncionarioId, semDesligamento.FuncionarioId })
        {
            var erro = await Assert.ThrowsAsync<ErroDeNegocioException>(() => CancelarAsync(api, id));
            Assert.Equal("SEM_DESLIGAMENTO_AGENDADO", erro.Codigo);
        }

        Assert.Equal("EFETIVADO", await SituacaoAsync(api, efetivada.FuncionarioId)); // o que já foi gravado fica
        await Assert.ThrowsAsync<NaoEncontradoException>(() => CancelarAsync(api, 987_654));
    }

    [Fact]
    public async Task Cancelar_e_efetivar_ao_mesmo_tempo_terminam_coerentes_nunca_CANCELADO_com_a_pessoa_inativa()
    {
        var api = await ApiAsync();
        var cancelou = 0;
        var efetivou = 0;

        for (var repeticao = 0; repeticao < 20; repeticao++)
        {
            var pessoa = await PessoaAsync(api, $"corrida{repeticao}");
            var desligamento = await AgendadoPorSqlAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 5)); // vencido: a efetivação pode acontecer
            using var barreira = new Barrier(2);

            var cancelar = EmParalelo(
                barreira,
                async () =>
                {
                    try
                    {
                        await CancelarAsync(api, pessoa.FuncionarioId);
                        return (Exception?)null;
                    }
                    catch (Exception erro)
                    {
                        return erro;
                    }
                });
            var efetivar = EmParalelo(barreira, () => EfetivarComoSistemaAsync(api, desligamento));
            var erroDoCancelamento = await cancelar;
            var resultado = await efetivar;

            var situacao = await SituacaoAsync(api, pessoa.FuncionarioId);
            var ativa = await AtivoAsync(api, pessoa.FuncionarioId);
            if (situacao == "CANCELADO")
            {
                cancelou++;
                Assert.Null(erroDoCancelamento);
                Assert.Equal(ResultadoDaEfetivacao.NaoEstavaAgendado, resultado);
                Assert.True(ativa); // cancelado: a pessoa continua trabalhando
            }
            else
            {
                efetivou++;
                Assert.Equal("EFETIVADO", situacao);
                Assert.Equal(ResultadoDaEfetivacao.Efetivado, resultado);
                Assert.False(ativa);
                Assert.Equal("SEM_DESLIGAMENTO_AGENDADO", Assert.IsType<ErroDeNegocioException>(erroDoCancelamento).Codigo); // quem chegou depois recebe o 422, nunca um 500
            }
        }

        Assert.Equal(20, cancelou + efetivou);
    }

    [Fact]
    public async Task Condicao_que_recusa_mantem_AGENDADO_e_a_pessoa_ativa_e_quando_libera_efetiva()
    {
        var api = await ApiAsync();
        var condicao = new CondicaoDeTeste(libera: false);
        void Configurar(IServiceCollection s) => s.AddScoped<ICondicaoParaEfetivarDesligamento>(_ => condicao);
        var pessoa = await PessoaAsync(api, "condicao1");
        var outra = await PessoaAsync(api, "condicao2");

        // No clique: o último dia é passado, mas a condição recusa; fica AGENDADO e com a tarefa.
        var agendou = await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(pessoa.FuncionarioId, new DateOnly(2026, 10, 5), CancellationToken.None));
        Assert.False(agendou.EfetivadoNaHora);
        Assert.Equal("AGENDADO", await SituacaoAsync(api, pessoa.FuncionarioId));
        Assert.True(await AtivoAsync(api, pessoa.FuncionarioId));
        Assert.Equal(1, await TarefasDoDesligamentoAsync(api, agendou.DesligamentoId));

        // Na efetivação: a condição ainda recusa (AindaNaoPode) e o estado não muda.
        var desligamento = await AgendadoPorSqlAsync(api, outra.FuncionarioId, new DateOnly(2026, 10, 5));
        Assert.Equal(ResultadoDaEfetivacao.AindaNaoPode, await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().EfetivarAsync(desligamento, CancellationToken.None)));
        Assert.Equal("AGENDADO", await SituacaoAsync(api, outra.FuncionarioId));
        Assert.True(await AtivoAsync(api, outra.FuncionarioId));

        // A condição libera (a fatia 3 fechou o banco de horas do dia): agora efetiva.
        condicao.Libera = true;
        Assert.Equal(ResultadoDaEfetivacao.Efetivado, await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().EfetivarAsync(desligamento, CancellationToken.None)));
        Assert.Equal("EFETIVADO", await SituacaoAsync(api, outra.FuncionarioId));
        Assert.False(await AtivoAsync(api, outra.FuncionarioId));
        Assert.Equal(ResultadoDaEfetivacao.JaEstavaEfetivado, await EfetivarComoSistemaAsync(api, desligamento)); // idempotente
    }

    [Fact]
    public async Task Efetivar_o_gestor_de_um_setor_chama_PassarParaORhAsync_com_o_id_do_gestor_uma_vez_e_nao_mexe_no_setor()
    {
        var api = await ApiAsync();
        var (pessoa, gestor, setor) = await GestorDeSetorAsync(api, "gestor1");
        var chamadas = new List<string>();

        var resultado = await api.NoEscopoComAsync(
            s => s.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas)),
            sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(pessoa, new DateOnly(2026, 10, 5), CancellationToken.None));

        Assert.True(resultado.EfetivadoNaHora);
        Assert.Equal([$"Passar:{gestor}"], chamadas); // uma vez, com o ID_GESTOR, e a fatia nunca aprova a etapa
        Assert.Equal(gestor, await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {setor}")); // o setor continua apontando para ele (só a 1c troca)
        Assert.Equal(1, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_GESTOR = {gestor}"));
    }

    [Fact]
    public async Task Efetivar_quem_nao_e_gestor_e_agendar_sem_efetivar_nao_chamam_PassarParaORhAsync()
    {
        var api = await ApiAsync();
        var (gestora, _, _) = await GestorDeSetorAsync(api, "gestor2");
        var comum = await PessoaAsync(api, "naogestor1");
        var chamadas = new List<string>();
        void Configurar(IServiceCollection s) => s.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas));

        await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(comum.FuncionarioId, new DateOnly(2026, 10, 5), CancellationToken.None)); // efetiva, mas não é gestor
        var agendou = await api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(gestora, new DateOnly(2026, 11, 13), CancellationToken.None)); // é gestora, mas só agenda

        Assert.False(agendou.EfetivadoNaHora);
        Assert.Empty(chamadas);
    }

    [Fact]
    public async Task Se_PassarParaORhAsync_lanca_a_efetivacao_inteira_desfaz_e_a_pessoa_continua_ativa_e_AGENDADO()
    {
        var api = await ApiAsync();
        var (gestora, _, _) = await GestorDeSetorAsync(api, "gestor3");
        var (outra, _, _) = await GestorDeSetorAsync(api, "gestor4");
        var desligamento = await AgendadoPorSqlAsync(api, gestora, new DateOnly(2026, 10, 5));
        var chamadas = new List<string>();
        void Configurar(IServiceCollection s) => s.AddScoped<IPedidosParadosNoGestorDaFatia>(_ => new FatiaDeTeste(chamadas, lanca: true));

        // Pela efetivação de um AGENDADO: tudo desfaz, e ele continua AGENDADO.
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().EfetivarAsync(desligamento, CancellationToken.None)));
        Assert.Equal("AGENDADO", await SituacaoAsync(api, gestora));
        Assert.True(await AtivoAsync(api, gestora));
        Assert.Null(await DataDeDesligamentoAsync(api, gestora));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'FUNCIONARIOS' AND ID_REGISTRO = {gestora}")); // nem o evento do que desfez

        // Pelo clique com data passada: nem a linha do desligamento sobra.
        await Assert.ThrowsAsync<InvalidOperationException>(() => api.NoEscopoComAsync(Configurar, sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(outra, new DateOnly(2026, 10, 5), CancellationToken.None)));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {outra}"));
        Assert.True(await AtivoAsync(api, outra));
        Assert.Equal(2, chamadas.Count); // a fatia foi chamada nas duas tentativas, e as duas desfizeram
    }

    [Fact]
    public async Task Agendar_e_efetivar_gravam_CriadoEm_e_EfetivadoEm_do_relogio_falso()
    {
        var api = await ApiAsync();
        var naHora = await PessoaAsync(api, "relogio1");
        var depois = await PessoaAsync(api, "relogio2");

        var resultado = await AgendarAsync(api, naHora.FuncionarioId, new DateOnly(2026, 10, 5));
        var desligamento = await AgendadoPorSqlAsync(api, depois.FuncionarioId, new DateOnly(2026, 10, 5));
        var maisTarde = new DateTime(2026, 10, 9, 5, 0, 0, DateTimeKind.Utc);
        api.Api.Relogio.Fixar(maisTarde);
        await EfetivarComoSistemaAsync(api, desligamento);

        await using var db = ContextoDeTeste.Novo(api.Conexao);
        var primeiro = await db.DesligamentosAgendados.AsNoTracking().SingleAsync(d => d.Id == resultado.DesligamentoId);
        Assert.Equal((Agora, (DateTime?)Agora), (primeiro.CriadoEm, primeiro.EfetivadoEm)); // 2026-10-07T12:42:00Z, e não o UTC do banco
        var segundo = await db.DesligamentosAgendados.AsNoTracking().SingleAsync(d => d.Id == desligamento);
        Assert.Equal((DateTime?)maisTarde, segundo.EfetivadoEm);
    }

    /// <summary>Roda a ação numa thread própria que espera a barreira: é o que faz os cliques e as chamadas começarem juntos (o pool de threads demoraria a ter 20 livres).</summary>
    private static Task<T> EmParalelo<T>(Barrier barreira, Func<Task<T>> acao) =>
        Task.Factory.StartNew(
            () =>
            {
                barreira.SignalAndWait();
                return acao();
            },
            CancellationToken.None,
            TaskCreationOptions.LongRunning,
            TaskScheduler.Default).Unwrap();
}
