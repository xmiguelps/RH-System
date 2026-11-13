using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Treinamentos;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta das seis tabelas do TAD (treinamentos) no banco REAL, relendo com OUTRO contexto. Cada teste prova um cuidado do plano da fatia 6: <c>EMITE_CERTIFICADO</c> e <c>INCLUI_ADMITIDOS</c> gravados mesmo como "falso" (o padrão do banco é TRUE), <c>TIPO_TREINAMENTO</c> só <c>'ONBOARDING'</c> ou nulo, as colunas sem uso ficam
/// com o padrão, <c>ENCERRADO_EM</c> e o progresso em -03:00 com o relógio da aplicação, a coluna <c>CHAVE_DESTINO</c> do público é gerada, e o trigger cria a linha do certificado com UUID v1 quando a linha da pessoa vira <c>CONCLUIDO</c> (a aplicação a lê). As recusas dos triggers saem na sonda.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDosTreinamentosTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddMilliseconds(600);

    private static async Task<(AppDbContext Db, Funcionario Pessoa)> NovaPessoaAsync(string conexao, string email, bool ativa = true)
    {
        var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa {email}", email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        if (!ativa)
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-01' WHERE ID_FUNCIONARIO = {pessoa.Id}");
        }

        return (db, pessoa);
    }

    private static async Task<object?> EscalarAsync(string conexao, string sql) => await Sql.EscalarAsync(conexao, sql);

    private static async Task<string?> TextoAsync(string conexao, string sql) => (await Sql.EscalarAsync(conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    private static string Texto(object? valor) => Convert.ToDateTime(valor, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static async Task<Treinamento> TreinamentoAsync(AppDbContext db, bool emite = true, bool admitidos = true, bool onboarding = false)
    {
        var treinamento = Treinamento.Criar($"Treinamento {Guid.NewGuid():N}", null, onboarding, obrigatorio: false, publico: null, emiteCertificado: emite, incluiAdmitidos: admitidos, comecaEm: null);
        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();
        return treinamento;
    }

    [Fact]
    public async Task O_treinamento_nasce_rascunho_e_grava_as_duas_caixas_mesmo_falsas_sem_tocar_nas_colunas_sem_uso()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var db = ContextoDeTeste.Novo(conexao);
        var treinamento = Treinamento.Criar("LGPD na prática", "Conceitos.", onboarding: false, obrigatorio: true, publico: PublicoDoTreinamento.Setores, emiteCertificado: false, incluiAdmitidos: false, comecaEm: new DateOnly(2026, 10, 20));

        db.Treinamentos.Add(treinamento);
        await db.SaveChangesAsync();

        // os padrões do banco são EMITE_CERTIFICADO = TRUE e INCLUI_ADMITIDOS = TRUE: o "falso" só chega porque a entidade o envia
        var id = treinamento.Id;
        Assert.Equal((0, 0, 0, 1, 0, 0, "SETORES"), (
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT EMITE_CERTIFICADO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT INCLUI_ADMITIDOS FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT PUBLICADO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT OBRIGATORIO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT AVISA_POR_EMAIL FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToInt32(await EscalarAsync(conexao, $"SELECT LEMBRA_ANTES_DO_FIM FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture),
            Convert.ToString(await EscalarAsync(conexao, $"SELECT PUBLICO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}"), CultureInfo.InvariantCulture)));
        Assert.Equal((true, true), (await EscalarAsync(conexao, $"SELECT DATA_FIM FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}") is DBNull or null, await EscalarAsync(conexao, $"SELECT TIPO_TREINAMENTO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {id}") is DBNull or null));
        var relido = await ContextoDeTeste.Novo(conexao).Treinamentos.AsNoTracking().SingleAsync(t => t.Id == id);
        Assert.Equal((false, false, false, new DateOnly(2026, 10, 20), PublicoDoTreinamento.Setores), (relido.EmiteCertificado, relido.IncluiAdmitidos, relido.Onboarding, relido.ComecaEm, relido.Publico));
    }

    [Fact]
    public async Task O_onboarding_grava_o_tipo_e_o_encerramento_lido_vira_UTC_vindo_de_menos_3_horas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var db = ContextoDeTeste.Novo(conexao);
        var onboarding = await TreinamentoAsync(db, onboarding: true);
        var comum = await TreinamentoAsync(db);
        await Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS SET PUBLICADO = TRUE, ENCERRADO_EM = '2026-10-07 09:42:00' WHERE ID_TREINAMENTO = {onboarding.Id}");

        var relido = await ContextoDeTeste.Novo(conexao).Treinamentos.AsNoTracking().SingleAsync(t => t.Id == onboarding.Id);

        Assert.Equal(("ONBOARDING", true, true, new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc)), (Convert.ToString(await EscalarAsync(conexao, $"SELECT TIPO_TREINAMENTO FROM TREINAMENTOS WHERE ID_TREINAMENTO = {onboarding.Id}"), CultureInfo.InvariantCulture), relido.Onboarding, relido.Publicado, relido.EncerradoEmUtc));
        Assert.False((await ContextoDeTeste.Novo(conexao).Treinamentos.AsNoTracking().SingleAsync(t => t.Id == comum.Id)).Onboarding);
    }

    [Fact]
    public async Task Cada_tipo_de_modulo_faz_a_ida_e_volta_com_o_seu_campo_e_o_nome_do_arquivo_junto_do_caminho()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var db = ContextoDeTeste.Novo(conexao);
        var treinamento = await TreinamentoAsync(db);
        db.TreinamentosModulos.AddRange(
            TreinamentoModulo.Criar(treinamento.Id, 1, "Abertura", TipoDeModulo.Video, 12, "https://video.exemplo.com.br/abertura", null, null, null),
            TreinamentoModulo.Criar(treinamento.Id, 2, "Leitura", TipoDeModulo.Texto, null, null, null, "Texto do módulo.", null),
            TreinamentoModulo.Criar(treinamento.Id, 3, "Artigo", TipoDeModulo.Link, 5, "https://artigo.exemplo.com.br", null, null, null),
            TreinamentoModulo.Criar(treinamento.Id, 4, "Apostila", TipoDeModulo.Pdf, 30, null, "treinamentos/apostila.pdf", null, "Apostila.pdf"));

        await db.SaveChangesAsync();

        var modulos = await ContextoDeTeste.Novo(conexao).TreinamentosModulos.AsNoTracking().Where(m => m.TreinamentoId == treinamento.Id).OrderBy(m => m.Ordem).ToListAsync();
        Assert.Equal([TipoDeModulo.Video, TipoDeModulo.Texto, TipoDeModulo.Link, TipoDeModulo.Pdf], modulos.Select(m => m.Tipo));
        Assert.Equal(("https://video.exemplo.com.br/abertura", (short)12, "Texto do módulo.", ("treinamentos/apostila.pdf", "Apostila.pdf")), (modulos[0].UrlExterna, modulos[0].DuracaoMinutos!.Value, modulos[1].ConteudoTexto, (modulos[3].CaminhoArquivo, modulos[3].NomeArquivoOriginal)));
        Assert.Null(modulos[1].DuracaoMinutos);
        Assert.Equal("VIDEO,TEXTO,LINK,PDF", await TextoAsync(conexao, $"SELECT GROUP_CONCAT(TIPO_MODULO ORDER BY ORDEM) FROM TREINAMENTOS_MODULOS WHERE ID_TREINAMENTO = {treinamento.Id}"));
    }

    [Fact]
    public async Task O_publico_por_setor_e_por_pessoa_le_a_chave_gerada_e_o_progresso_grava_o_instante_da_aplicacao_em_menos_3_horas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "tad1@teste.com");
        var treinamento = await TreinamentoAsync(db);
        var setor = await ApoioDeTeste.CriarSetorAsync(conexao, "Setor do treinamento");
        db.TreinamentosPublico.AddRange(TreinamentoPublico.DoSetor(treinamento.Id, setor), TreinamentoPublico.DaPessoa(treinamento.Id, pessoa.Id));
        var modulo = TreinamentoModulo.Criar(treinamento.Id, 1, "Único", TipoDeModulo.Texto, null, null, null, "Texto.", null);
        db.TreinamentosModulos.Add(modulo);
        await db.SaveChangesAsync();
        db.TreinamentosModulosProgresso.Add(TreinamentoModuloProgresso.Criar(modulo.Id, pessoa.Id, Agora));

        await db.SaveChangesAsync();

        Assert.Equal($"0:{setor},{pessoa.Id}:0", await TextoAsync(conexao, $"SELECT GROUP_CONCAT(CHAVE_DESTINO ORDER BY ID_TREINAMENTO_PUBLICO) FROM TREINAMENTOS_PUBLICO WHERE ID_TREINAMENTO = {treinamento.Id}"));
        Assert.Equal("2026-10-07 09:42:00", Texto(await EscalarAsync(conexao, $"SELECT DATA_CONCLUSAO FROM TREINAMENTOS_MODULOS_PROGRESSO WHERE ID_MODULO = {modulo.Id}")));
        var relido = await ContextoDeTeste.Novo(conexao).TreinamentosModulosProgresso.AsNoTracking().SingleAsync(p => p.ModuloId == modulo.Id);
        Assert.Equal(new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc), relido.ConcluidoEmUtc);
    }

    [Fact]
    public async Task A_linha_da_pessoa_grava_a_situacao_e_os_dias_e_o_trigger_cria_o_certificado_com_UUID_v1_quando_ela_conclui()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "tad2@teste.com");
        var treinamento = await TreinamentoAsync(db);
        var linha = TreinamentoFuncionario.Criar(treinamento.Id, pessoa.Id, SituacaoNoTreinamento.EmAndamento, iniciadoEm: new DateOnly(2026, 10, 5));
        db.TreinamentosFuncionarios.Add(linha);
        await db.SaveChangesAsync();
        var antes = Convert.ToInt32(await EscalarAsync(conexao, $"SELECT COUNT(*) FROM TREINAMENTOS_CERTIFICADOS WHERE ID_TREINAMENTO = {treinamento.Id}"), CultureInfo.InvariantCulture);

        linha.Concluir(new DateOnly(2026, 10, 7));
        await db.SaveChangesAsync();

        Assert.Equal((0, "CONCLUIDO", "2026-10-05", "2026-10-07"), (antes, Convert.ToString(await EscalarAsync(conexao, $"SELECT STATUS_TREINAMENTO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO_FUNCIONARIO = {linha.Id}"), CultureInfo.InvariantCulture), Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_INICIO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO_FUNCIONARIO = {linha.Id}"), CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_CONCLUSAO FROM TREINAMENTOS_FUNCIONARIOS WHERE ID_TREINAMENTO_FUNCIONARIO = {linha.Id}"), CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)));
        var certificado = await ContextoDeTeste.Novo(conexao).TreinamentosCertificados.AsNoTracking().SingleAsync(c => c.TreinamentoId == treinamento.Id && c.FuncionarioId == pessoa.Id);
        Assert.Equal('1', certificado.CodigoValidacao![14]); // o UUID() do MySQL é versão 1: a aplicação o troca por um v4
        Assert.Null(certificado.CaminhoArquivo);
        Assert.NotNull(certificado.EmitidoEm);
    }

    [Fact]
    public async Task Os_triggers_recusam_pessoa_inativa_e_mudar_a_situacao_de_quem_ja_concluiu()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, ativa) = await NovaPessoaAsync(conexao, "tad3@teste.com");
        var (_, inativa) = await NovaPessoaAsync(conexao, "tad4@teste.com", ativa: false);
        var treinamento = await TreinamentoAsync(db);
        var linha = TreinamentoFuncionario.Criar(treinamento.Id, ativa.Id, SituacaoNoTreinamento.Concluido, concluidoEm: new DateOnly(2026, 10, 7));
        db.TreinamentosFuncionarios.Add(linha);
        await db.SaveChangesAsync();

        var inativaRecusada = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"INSERT INTO TREINAMENTOS_FUNCIONARIOS (ID_TREINAMENTO, ID_FUNCIONARIO, STATUS_TREINAMENTO) VALUES ({treinamento.Id}, {inativa.Id}, 'INSCRITO')"));
        var concluidoRecusado = await Assert.ThrowsAnyAsync<Exception>(() => Sql.ExecutarAsync(conexao, $"UPDATE TREINAMENTOS_FUNCIONARIOS SET STATUS_TREINAMENTO = 'EM_ANDAMENTO' WHERE ID_TREINAMENTO_FUNCIONARIO = {linha.Id}"));

        Assert.Contains("Nao e possivel inscrever funcionario inativo em treinamento.", inativaRecusada.ToString(), StringComparison.Ordinal);
        Assert.Contains("Treinamento concluido nao pode mudar de status.", concluidoRecusado.ToString(), StringComparison.Ordinal);
    }
}
