using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using static RHSystem.Api.IntegrationTests.Procedures.ApoioDaParteD;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de advertência do <c>V007</c> (parte D): <c>SP_GERAR_ADVERTENCIA</c>, <c>SP_REGISTRAR_CIENCIA_ADVERTENCIA</c>, <c>SP_CONTESTAR_ADVERTENCIA</c> e <c>SP_RESPONDER_CONTESTACAO_ADVERTENCIA</c>, num MySQL 8.4 de verdade. As outras da parte D estão em <c>TreinamentoProceduresTests</c>,
/// <c>FormularioProceduresTests</c> e <c>DesligamentoProceduresTests</c>.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AdvertenciasEOutrosProceduresTests(MySqlFixture mysql)
{
    private const string Gerar = "SP_GERAR_ADVERTENCIA";
    private const string Ciencia = "SP_REGISTRAR_CIENCIA_ADVERTENCIA";
    private const string Contestar = "SP_CONTESTAR_ADVERTENCIA";
    private const string Responder = "SP_RESPONDER_CONTESTACAO_ADVERTENCIA";
    private const string NaoEncontrada = "Advertencia nao encontrada para o funcionario informado.";
    private const string CienciaSoDePendente = "A advertencia nao esta pendente: a ciencia so vale para advertencia PENDENTE.";
    private const string ContestarSoDePendente = "A advertencia nao esta pendente: so a advertencia PENDENTE pode ser contestada.";
    private const string NaoEstaContestada = "A advertencia nao esta contestada: so a contestacao em aberto pode ser respondida.";

    /// <summary>Alguém do RH, um setor com o gestor dele e uma pessoa da equipe (que tem esse gestor na ficha).</summary>
    private sealed record Elenco(string Conexao, int Rh, int Chefe, int Gestor, int Setor, int Pessoa, DateOnly Hoje);

    private async Task<Elenco> ElencoAsync()
    {
        var conexao = await NovoBancoAsync(mysql);
        var rh = await FuncionarioAsync(conexao, nivel: "RH");
        var setor = await SetorAsync(conexao);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor", setorId: setor);
        var gestor = await GestorDoSetorAsync(conexao, chefe, setor);
        var pessoa = await FuncionarioAsync(conexao, setorId: setor, gestorId: gestor);
        return new Elenco(conexao, rh, chefe, gestor, setor, pessoa, await DiaAsync(conexao));
    }

    /// <summary>Uma advertência <c>PENDENTE</c> registrada pelo RH, pela própria procedure.</summary>
    private static async Task<int> PendenteAsync(Elenco e) =>
        Inteiro((await ChamarAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "ESCRITA", "Atraso", "Chegou atrasado tres vezes.", new Saida("id")))["id"]);

    private static Task<string?> LinhaAsync(string conexao, int id) =>
        TextoAsync(conexao, $"SELECT CONCAT_WS('|', STATUS_ADVERTENCIA, COALESCE(DATE_FORMAT(DATA_CIENCIA, '%Y-%m-%d'), 'sem ciencia'), COALESCE(TEXTO_CONTESTACAO, 'sem texto'), COALESCE(DATE_FORMAT(DATA_CONTESTACAO, '%Y-%m-%d'), 'sem contestacao')) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}");

    private static Task<long> TotalAsync(string conexao) => Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM ADVERTENCIAS");

    // ---------------------------------------------------------------- SP_GERAR_ADVERTENCIA

    [Fact]
    public async Task O_RH_registra_e_a_advertencia_nasce_pendente_com_o_dia_do_ocorrido_e_quem_do_RH_registrou()
    {
        var e = await ElencoAsync();
        var ocorrido = e.Hoje.AddDays(-3);

        var saidas = await ChamarAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, ocorrido, " escrita ", "  Atraso reiterado \n", "\t Chegou atrasado tres vezes na semana.  ", new Saida("id"));

        var id = Inteiro(saidas["id"]);
        Assert.Equal(
            $"{e.Pessoa}|sem gestor|{e.Rh}|{Iso(ocorrido)}|ESCRITA|Atraso reiterado|Chegou atrasado tres vezes na semana.|PENDENTE|sem ciencia|sem texto|sem contestacao",
            await TextoAsync(e.Conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, COALESCE(ID_GESTOR, 'sem gestor'), COALESCE(ID_FUNCIONARIO_RH, 'sem rh'), DATE_FORMAT(DATA_ADVERTENCIA, '%Y-%m-%d'), TIPO_ADVERTENCIA, MOTIVO, DESCRICAO, STATUS_ADVERTENCIA, COALESCE(DATA_CIENCIA, 'sem ciencia'), COALESCE(TEXTO_CONTESTACAO, 'sem texto'), COALESCE(DATA_CONTESTACAO, 'sem contestacao')) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        Assert.Equal(1L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task O_RH_registra_em_nome_do_gestor_da_pessoa_e_ficam_gravados_o_gestor_e_quem_do_RH_registrou()
    {
        var e = await ElencoAsync();

        var saidas = await ChamarAsync(e.Conexao, Gerar, e.Pessoa, e.Gestor, e.Rh, e.Hoje, "VERBAL", "Conduta", "Discussao com um colega.", new Saida("id"));

        Assert.Equal($"{e.Gestor}|{e.Rh}|VERBAL|PENDENTE", await TextoAsync(e.Conexao, $"SELECT CONCAT_WS('|', ID_GESTOR, ID_FUNCIONARIO_RH, TIPO_ADVERTENCIA, STATUS_ADVERTENCIA) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {Inteiro(saidas["id"])}"));
    }

    [Fact]
    public async Task O_gestor_registra_para_a_propria_equipe_e_a_coluna_do_RH_fica_vazia()
    {
        var e = await ElencoAsync();

        var saidas = await ChamarAsync(e.Conexao, Gerar, e.Pessoa, e.Gestor, null, e.Hoje, "VERBAL", "Conduta", "Discussao com um colega.", new Saida("id"));

        Assert.Equal($"{e.Gestor}|sem rh|PENDENTE", await TextoAsync(e.Conexao, $"SELECT CONCAT_WS('|', ID_GESTOR, COALESCE(ID_FUNCIONARIO_RH, 'sem rh'), STATUS_ADVERTENCIA) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {Inteiro(saidas["id"])}"));
    }

    [Fact]
    public async Task Cada_entrada_invalida_e_recusada_com_a_mensagem_que_diz_o_que_consertar_e_nada_fica_gravado()
    {
        var e = await ElencoAsync();
        var amanha = e.Hoje.AddDays(1);
        const string motivo = "Atraso";
        const string descricao = "Chegou atrasado.";

        Assert.Equal("Informe o funcionario advertido.", await RecusaDeNegocioAsync(e.Conexao, Gerar, null, null, e.Rh, e.Hoje, "VERBAL", motivo, descricao, new Saida("id")));
        Assert.Equal("Informe quem registra a advertencia: o gestor, o responsavel do RH ou os dois.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, null, e.Hoje, "VERBAL", motivo, descricao, new Saida("id")));
        Assert.Equal("Informe a data do ocorrido.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, null, "VERBAL", motivo, descricao, new Saida("id")));
        Assert.Equal("A data do ocorrido nao pode ser futura.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, amanha, "VERBAL", motivo, descricao, new Saida("id")));
        Assert.Equal("Tipo de advertencia invalido: use VERBAL ou ESCRITA.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "SUSPENSAO", motivo, descricao, new Saida("id")));
        Assert.Equal("Tipo de advertencia invalido: use VERBAL ou ESCRITA.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, null, motivo, descricao, new Saida("id")));
        Assert.Equal("Informe o motivo, com ate 255 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", " \n\t ", descricao, new Saida("id")));
        Assert.Equal("Informe o motivo, com ate 255 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", null, descricao, new Saida("id")));
        Assert.Equal("Informe o motivo, com ate 255 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", new string('m', 256), descricao, new Saida("id")));
        Assert.Equal("Informe a descricao, com ate 5000 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", motivo, null, new Saida("id")));
        Assert.Equal("Informe a descricao, com ate 5000 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", motivo, "   ", new Saida("id")));
        Assert.Equal("Informe a descricao, com ate 5000 caracteres.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", motivo, new string('d', 5001), new Saida("id")));

        Assert.Equal(0L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task Os_limites_sao_aceitos_motivo_de_255_e_descricao_de_5000_caracteres_contados_depois_de_aparar_e_o_dia_da_admissao()
    {
        var e = await ElencoAsync();
        var motivo = string.Concat(Enumerable.Repeat("😀", 255)); // 255 caracteres para o banco, 510 pedaços UTF-16
        var descricao = new string('d', 5000);

        var saidas = await ChamarAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, new DateOnly(2025, 1, 6), "verbal", $"  {motivo}\n", $"\n{descricao}  ", new Saida("id"));

        Assert.Equal("255|5000|2025-01-06|VERBAL", await TextoAsync(e.Conexao, $"SELECT CONCAT_WS('|', CHAR_LENGTH(MOTIVO), CHAR_LENGTH(DESCRICAO), DATE_FORMAT(DATA_ADVERTENCIA, '%Y-%m-%d'), TIPO_ADVERTENCIA) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {Inteiro(saidas["id"])}"));
    }

    [Fact]
    public async Task Pessoa_que_nao_existe_inativa_ou_com_o_ocorrido_antes_da_admissao_e_recusada()
    {
        var e = await ElencoAsync();
        var inativa = await FuncionarioAsync(e.Conexao, ativo: false);

        Assert.Equal("Funcionario nao encontrado.", await RecusaDeNegocioAsync(e.Conexao, Gerar, 999_999, null, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("Nao e possivel registrar advertencia para funcionario inativo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, inativa, null, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("A data do ocorrido nao pode ser anterior a admissao do funcionario.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, new DateOnly(2025, 1, 5), "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));

        Assert.Equal(0L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task Quem_do_RH_registra_tem_de_existir_e_estar_ativo()
    {
        var e = await ElencoAsync();
        var rhInativo = await FuncionarioAsync(e.Conexao, nivel: "RH", ativo: false);

        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, 999_999, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("Responsavel do RH inexistente ou inativo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, null, rhInativo, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));

        Assert.Equal(0L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task Em_nome_do_gestor_o_RH_so_registra_com_o_gestor_ativo_da_propria_pessoa()
    {
        var e = await ElencoAsync();
        const string semGestor = "O gestor informado nao e o gestor ativo do funcionario.";
        var outroSetor = await SetorAsync(e.Conexao);
        var outroChefe = await FuncionarioAsync(e.Conexao, nivel: "Funcionario Gestor", setorId: outroSetor);
        var outroGestor = await GestorDoSetorAsync(e.Conexao, outroChefe, outroSetor);
        var semChefe = await FuncionarioAsync(e.Conexao, setorId: e.Setor);

        Assert.Equal(semGestor, await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, outroGestor, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id"))); // gestor de outra pessoa
        Assert.Equal(semGestor, await RecusaDeNegocioAsync(e.Conexao, Gerar, semChefe, e.Gestor, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id"))); // a pessoa não tem gestor na ficha
        Assert.Equal(semGestor, await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, 999_999, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id"))); // gestor que não existe

        await Sql.ExecutarAsync(e.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {e.Chefe}");
        Assert.Equal(semGestor, await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, e.Gestor, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id"))); // o gestor dela foi inativado

        Assert.Equal(0L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task Pela_tela_do_gestor_so_vale_gestor_ativo_advertindo_alguem_da_propria_equipe_e_nunca_a_si_mesmo()
    {
        var e = await ElencoAsync();
        var outroSetor = await SetorAsync(e.Conexao);
        var deOutroSetor = await FuncionarioAsync(e.Conexao, setorId: outroSetor, gestorId: e.Gestor); // tem o gestor na ficha, mas é de outro setor: a equipe é o setor
        var gestorSemSetor = await GestorAsync(e.Conexao, await FuncionarioAsync(e.Conexao, nivel: "Funcionario Gestor"));

        Assert.Equal("Gestor inexistente ou inativo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, 999_999, null, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("O gestor nao pode advertir a si mesmo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Chefe, e.Gestor, null, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("O funcionario nao e da equipe do gestor informado.", await RecusaDeNegocioAsync(e.Conexao, Gerar, deOutroSetor, e.Gestor, null, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));
        Assert.Equal("O funcionario nao e da equipe do gestor informado.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, gestorSemSetor, null, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));

        await Sql.ExecutarAsync(e.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {e.Chefe}");
        Assert.Equal("Gestor inexistente ou inativo.", await RecusaDeNegocioAsync(e.Conexao, Gerar, e.Pessoa, e.Gestor, null, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));

        Assert.Equal(0L, await TotalAsync(e.Conexao));
    }

    [Fact]
    public async Task Duas_chamadas_iguais_ao_mesmo_tempo_registram_duas_advertencias_como_dois_envios_na_API()
    {
        var e = await ElencoAsync();

        var desfechos = await AoMesmoTempoAsync(
            () => TentarAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")),
            () => TentarAsync(e.Conexao, Gerar, e.Pessoa, null, e.Rh, e.Hoje, "VERBAL", "Atraso", "Chegou atrasado.", new Saida("id")));

        Assert.All(desfechos, d => Assert.True(d.Passou, d.Mensagem));
        Assert.Equal(2, desfechos.Select(d => Inteiro(d.Saidas!["id"])).Distinct().Count());
        Assert.Equal(2L, await TotalAsync(e.Conexao));
    }

    // ---------------------------------------------------------------- SP_REGISTRAR_CIENCIA_ADVERTENCIA

    [Fact]
    public async Task A_ciencia_leva_a_advertencia_pendente_a_aplicada_com_a_data_de_agora()
    {
        var e = await ElencoAsync();
        var id = await PendenteAsync(e);

        await ChamarAsync(e.Conexao, Ciencia, id, e.Pessoa);

        Assert.Equal($"APLICADA|{Iso(e.Hoje)}|sem texto|sem contestacao", await LinhaAsync(e.Conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(e.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id} AND DATA_CIENCIA BETWEEN NOW() - INTERVAL 2 MINUTE AND NOW()")); // horário de Brasília, como NOW() no banco
    }

    [Fact]
    public async Task A_ciencia_de_advertencia_de_outra_pessoa_ou_que_nao_existe_tem_a_mesma_recusa_e_nao_muda_nada()
    {
        var e = await ElencoAsync();
        var id = await PendenteAsync(e);

        Assert.Equal(NaoEncontrada, await RecusaDeNegocioAsync(e.Conexao, Ciencia, id, e.Chefe));
        Assert.Equal(NaoEncontrada, await RecusaDeNegocioAsync(e.Conexao, Ciencia, 999_999, e.Pessoa));
        Assert.Equal("Informe a advertencia e o funcionario.", await RecusaDeNegocioAsync(e.Conexao, Ciencia, null, e.Pessoa));
        Assert.Equal("Informe a advertencia e o funcionario.", await RecusaDeNegocioAsync(e.Conexao, Ciencia, id, null));

        Assert.Equal("PENDENTE|sem ciencia|sem texto|sem contestacao", await LinhaAsync(e.Conexao, id));
    }

    [Fact]
    public async Task Repetir_a_ciencia_e_recusado_e_a_primeira_data_fica()
    {
        var e = await ElencoAsync();
        var id = await PendenteAsync(e);
        await ChamarAsync(e.Conexao, Ciencia, id, e.Pessoa);
        await Sql.ExecutarAsync(e.Conexao, $"UPDATE ADVERTENCIAS SET DATA_CIENCIA = '2026-01-02 03:04:05' WHERE ID_ADVERTENCIA = {id}");

        Assert.Equal(CienciaSoDePendente, await RecusaDeNegocioAsync(e.Conexao, Ciencia, id, e.Pessoa));

        Assert.Equal("2026-01-02 03:04:05", await TextoAsync(e.Conexao, $"SELECT DATE_FORMAT(DATA_CIENCIA, '%Y-%m-%d %H:%i:%s') FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
    }

    [Theory]
    [InlineData("STATUS_ADVERTENCIA = 'CONTESTADA', TEXTO_CONTESTACAO = 'Nao foi assim.', DATA_CONTESTACAO = NOW()", "CONTESTADA")]
    [InlineData("STATUS_ADVERTENCIA = 'CANCELADA'", "CANCELADA")]
    [InlineData("STATUS_ADVERTENCIA = NULL", null)]
    public async Task A_ciencia_so_vale_a_partir_de_pendente(string mudanca, string? situacao)
    {
        var e = await ElencoAsync();
        var id = await PendenteAsync(e);
        await Sql.ExecutarAsync(e.Conexao, $"UPDATE ADVERTENCIAS SET {mudanca} WHERE ID_ADVERTENCIA = {id}"); // a antiga aceitava a ciência em qualquer situação que ainda não tivesse a data

        Assert.Equal(CienciaSoDePendente, await RecusaDeNegocioAsync(e.Conexao, Ciencia, id, e.Pessoa));

        Assert.Equal(situacao ?? "nula", await TextoAsync(e.Conexao, $"SELECT CONCAT(COALESCE(STATUS_ADVERTENCIA, 'nula'), COALESCE(DATA_CIENCIA, '')) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
    }

    [Fact]
    public async Task Ciencia_e_contestacao_ao_mesmo_tempo_tem_um_efeito_so()
    {
        var e = await ElencoAsync();
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var id = await PendenteAsync(e);

            var desfechos = await AoMesmoTempoAsync(
                () => TentarAsync(e.Conexao, Ciencia, id, e.Pessoa),
                () => TentarAsync(e.Conexao, Contestar, id, e.Pessoa, "Nao concordo com o registro."));

            Assert.Single(desfechos, d => d.Passou);
            var recusa = Assert.Single(desfechos, d => !d.Passou);
            Assert.Equal("45000", recusa.SqlState);
            var linha = await LinhaAsync(e.Conexao, id);
            if (desfechos[0].Passou)
            {
                Assert.Equal(ContestarSoDePendente, recusa.Mensagem);
                Assert.Equal($"APLICADA|{Iso(e.Hoje)}|sem texto|sem contestacao", linha);
            }
            else
            {
                Assert.Equal(CienciaSoDePendente, recusa.Mensagem);
                Assert.Equal($"CONTESTADA|sem ciencia|Nao concordo com o registro.|{Iso(e.Hoje)}", linha);
            }
        }
    }

    // ---------------------------------------------------------------- SP_CONTESTAR_ADVERTENCIA

    [Fact]
    public async Task A_contestacao_leva_a_advertencia_pendente_a_contestada_com_o_texto_aparado_e_a_data_de_agora()
    {
        var e = await ElencoAsync();
        var id = await PendenteAsync(e);

        await ChamarAsync(e.Conexao, Contestar, id, e.Pessoa, "\n  Eu estava em visita a um cliente.  \t");

        Assert.Equal($"CONTESTADA|sem ciencia|Eu estava em visita a um cliente.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, id));
        Assert.Equal(1L, await Sql.ContarAsync(e.Conexao, $"SELECT COUNT(*) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id} AND DATA_CONTESTACAO BETWEEN NOW() - INTERVAL 2 MINUTE AND NOW()"));
    }

    [Fact]
    public async Task O_texto_da_contestacao_tem_de_10_a_2000_caracteres_depois_de_aparado()
    {
        var e = await ElencoAsync();
        const string texto = "Explique o que aconteceu no texto da contestacao, com de 10 a 2000 caracteres.";
        var curto = await PendenteAsync(e);
        var longo = await PendenteAsync(e);

        Assert.Equal(texto, await RecusaDeNegocioAsync(e.Conexao, Contestar, curto, e.Pessoa, null));
        Assert.Equal(texto, await RecusaDeNegocioAsync(e.Conexao, Contestar, curto, e.Pessoa, "   123456789   ")); // 9 caracteres: os espaços das pontas não contam
        Assert.Equal(texto, await RecusaDeNegocioAsync(e.Conexao, Contestar, longo, e.Pessoa, new string('c', 2001)));
        Assert.Equal("PENDENTE|sem ciencia|sem texto|sem contestacao", await LinhaAsync(e.Conexao, curto));

        await ChamarAsync(e.Conexao, Contestar, curto, e.Pessoa, " 1234567890 ");
        await ChamarAsync(e.Conexao, Contestar, longo, e.Pessoa, new string('c', 2000));

        Assert.Equal("10", await TextoAsync(e.Conexao, $"SELECT CHAR_LENGTH(TEXTO_CONTESTACAO) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {curto}"));
        Assert.Equal("2000", await TextoAsync(e.Conexao, $"SELECT CHAR_LENGTH(TEXTO_CONTESTACAO) FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {longo}"));
    }

    [Fact]
    public async Task So_a_pessoa_dona_contesta_e_so_a_partir_de_pendente_a_primeira_contestacao_nunca_e_sobrescrita()
    {
        var e = await ElencoAsync();
        var comCiencia = await PendenteAsync(e);
        var contestada = await PendenteAsync(e);
        await ChamarAsync(e.Conexao, Ciencia, comCiencia, e.Pessoa);
        await ChamarAsync(e.Conexao, Contestar, contestada, e.Pessoa, "A primeira explicacao.");

        Assert.Equal(NaoEncontrada, await RecusaDeNegocioAsync(e.Conexao, Contestar, contestada, e.Chefe, "Uma explicacao de outra pessoa."));
        Assert.Equal(NaoEncontrada, await RecusaDeNegocioAsync(e.Conexao, Contestar, 999_999, e.Pessoa, "Uma explicacao qualquer."));
        Assert.Equal("Informe a advertencia e o funcionario.", await RecusaDeNegocioAsync(e.Conexao, Contestar, null, e.Pessoa, "Uma explicacao qualquer."));
        Assert.Equal(ContestarSoDePendente, await RecusaDeNegocioAsync(e.Conexao, Contestar, comCiencia, e.Pessoa, "Contestar depois da ciencia.")); // a antiga deixava
        Assert.Equal(ContestarSoDePendente, await RecusaDeNegocioAsync(e.Conexao, Contestar, contestada, e.Pessoa, "A segunda explicacao."));

        Assert.Equal($"APLICADA|{Iso(e.Hoje)}|sem texto|sem contestacao", await LinhaAsync(e.Conexao, comCiencia));
        Assert.Equal($"CONTESTADA|sem ciencia|A primeira explicacao.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, contestada));
    }

    // ---------------------------------------------------------------- SP_RESPONDER_CONTESTACAO_ADVERTENCIA

    [Fact]
    public async Task Manter_devolve_a_contestada_a_aplicada_e_acatar_a_cancela_guardando_o_texto_da_contestacao()
    {
        var e = await ElencoAsync();
        var mantida = await PendenteAsync(e);
        var acatada = await PendenteAsync(e);
        await ChamarAsync(e.Conexao, Contestar, mantida, e.Pessoa, "A primeira explicacao.");
        await ChamarAsync(e.Conexao, Contestar, acatada, e.Pessoa, "A segunda explicacao.");

        await ChamarAsync(e.Conexao, Responder, mantida, "MANTER");
        await ChamarAsync(e.Conexao, Responder, acatada, " acatar ");

        Assert.Equal($"APLICADA|sem ciencia|A primeira explicacao.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, mantida)); // aplicada COM texto de contestação: é assim que a API deduz "mantida"
        Assert.Equal($"CANCELADA|sem ciencia|A segunda explicacao.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, acatada));
    }

    [Fact]
    public async Task So_a_advertencia_contestada_e_respondida_e_uma_vez_so()
    {
        var e = await ElencoAsync();
        var pendente = await PendenteAsync(e);
        var respondida = await PendenteAsync(e);
        await ChamarAsync(e.Conexao, Contestar, respondida, e.Pessoa, "A primeira explicacao.");

        Assert.Equal("Informe a advertencia.", await RecusaDeNegocioAsync(e.Conexao, Responder, null, "MANTER"));
        Assert.Equal("Decisao invalida: use MANTER ou ACATAR.", await RecusaDeNegocioAsync(e.Conexao, Responder, respondida, "APROVAR"));
        Assert.Equal("Decisao invalida: use MANTER ou ACATAR.", await RecusaDeNegocioAsync(e.Conexao, Responder, respondida, null));
        Assert.Equal("Advertencia nao encontrada.", await RecusaDeNegocioAsync(e.Conexao, Responder, 999_999, "MANTER"));
        Assert.Equal(NaoEstaContestada, await RecusaDeNegocioAsync(e.Conexao, Responder, pendente, "ACATAR"));
        Assert.Equal($"CONTESTADA|sem ciencia|A primeira explicacao.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, respondida));

        await ChamarAsync(e.Conexao, Responder, respondida, "ACATAR");

        Assert.Equal(NaoEstaContestada, await RecusaDeNegocioAsync(e.Conexao, Responder, respondida, "MANTER")); // cancelada não muda mais
        Assert.Equal(CienciaSoDePendente, await RecusaDeNegocioAsync(e.Conexao, Ciencia, respondida, e.Pessoa));
        Assert.Equal("PENDENTE|sem ciencia|sem texto|sem contestacao", await LinhaAsync(e.Conexao, pendente));
        Assert.Equal($"CANCELADA|sem ciencia|A primeira explicacao.|{Iso(e.Hoje)}", await LinhaAsync(e.Conexao, respondida));
    }

    [Fact]
    public async Task Duas_respostas_ao_mesmo_tempo_a_mesma_contestacao_so_uma_vale()
    {
        var e = await ElencoAsync();
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var id = await PendenteAsync(e);
            await ChamarAsync(e.Conexao, Contestar, id, e.Pessoa, "Nao concordo com o registro.");

            var desfechos = await AoMesmoTempoAsync(() => TentarAsync(e.Conexao, Responder, id, "MANTER"), () => TentarAsync(e.Conexao, Responder, id, "ACATAR"));

            Assert.Single(desfechos, d => d.Passou);
            Assert.Equal(NaoEstaContestada, Assert.Single(desfechos, d => !d.Passou).Mensagem);
            Assert.Equal(desfechos[0].Passou ? "APLICADA" : "CANCELADA", await TextoAsync(e.Conexao, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {id}"));
        }
    }
}
