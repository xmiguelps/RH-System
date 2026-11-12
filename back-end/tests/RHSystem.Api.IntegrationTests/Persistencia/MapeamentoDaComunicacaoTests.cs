using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta das tabelas da comunicação (avisos, leituras, holerites e advertências) no banco REAL, relendo com OUTRO contexto. Cada teste prova um cuidado do plano da fatia 4: as datas cortadas ao segundo e em -03:00, o rascunho
/// que volta a ter data nula depois do trigger, as colunas com padrão no banco (<c>FIXADO</c>, <c>ENVIADO</c>, <c>VISUALIZADO</c>, <c>TIPO_HOLERITE</c>, <c>VERSAO</c>) que o EF sempre envia, a linha de leitura que o trigger cria,
/// a chave única da leitura e o <c>ANO</c> do holerite (<c>YEAR</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDaComunicacaoTests(MySqlFixture mysql)
{
    // 12:42 UTC com fração: a coluna não tem fração e a entidade corta ao segundo.
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddMilliseconds(600);

    private static readonly DateTime AgoraAoSegundo = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static async Task<(AppDbContext Db, Funcionario Pessoa)> NovaPessoaAsync(string conexao, string email)
    {
        var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa {email}", email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return (db, pessoa);
    }

    private static async Task<object?> EscalarAsync(string conexao, string sql) => await Sql.EscalarAsync(conexao, sql);

    private static string Texto(object? valor) => Convert.ToDateTime(valor, System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", System.Globalization.CultureInfo.InvariantCulture);

    [Fact]
    public async Task Aviso_grava_as_datas_cortadas_ao_segundo_em_menos_3_horas_e_o_rascunho_volta_a_ter_data_nula_depois_do_trigger()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "aviso1@teste.com");
        var agendado = Aviso.Criar(autor.Id, TipoDeAviso.Detalhado, "Recesso de fim de ano", "## Datas\n\nO escritório fecha.", fixado: true, enviado: true, Agora, Agora.AddDays(7));
        var rascunho = Aviso.Criar(autor.Id, TipoDeAviso.Simples, "Rascunho", "Texto curto", fixado: false, enviado: false, null, null);
        db.Avisos.AddRange(agendado, rascunho);

        await db.SaveChangesAsync();

        Assert.Equal(("2026-10-07 09:42:00", "2026-10-14 09:42:00", "DETALHADO", 1, 1), (Texto(await EscalarAsync(conexao, $"SELECT DATA_PUBLICACAO FROM AVISOS WHERE ID_AVISO = {agendado.Id}")), Texto(await EscalarAsync(conexao, $"SELECT DATA_EXPIRACAO FROM AVISOS WHERE ID_AVISO = {agendado.Id}")), (await EscalarAsync(conexao, $"SELECT TIPO_AVISO FROM AVISOS WHERE ID_AVISO = {agendado.Id}")) as string, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT FIXADO FROM AVISOS WHERE ID_AVISO = {agendado.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ENVIADO FROM AVISOS WHERE ID_AVISO = {agendado.Id}"))));
        Assert.Equal((0, 0, "SIMPLES"), (Convert.ToInt32(await EscalarAsync(conexao, $"SELECT FIXADO FROM AVISOS WHERE ID_AVISO = {rascunho.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ENVIADO FROM AVISOS WHERE ID_AVISO = {rascunho.Id}")), (await EscalarAsync(conexao, $"SELECT TIPO_AVISO FROM AVISOS WHERE ID_AVISO = {rascunho.Id}")) as string));

        // O trigger de INSERT põe o NOW() do servidor onde a data veio nula, inclusive no rascunho: a aplicação volta a data para NULL logo depois.
        Assert.NotNull(await EscalarAsync(conexao, $"SELECT DATA_PUBLICACAO FROM AVISOS WHERE ID_AVISO = {rascunho.Id}") as DateTime?);
        await Sql.ExecutarAsync(conexao, $"UPDATE AVISOS SET DATA_PUBLICACAO = NULL WHERE ID_AVISO = {rascunho.Id}");

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lidos = await outro.Avisos.AsNoTracking().Where(a => a.Id == agendado.Id || a.Id == rascunho.Id).OrderBy(a => a.Id).ToListAsync();
        Assert.Equal(
            [(autor.Id, TipoDeAviso.Detalhado, "Recesso de fim de ano", true, true, (DateTime?)AgoraAoSegundo, (DateTime?)AgoraAoSegundo.AddDays(7)), (autor.Id, TipoDeAviso.Simples, "Rascunho", false, false, null, null)],
            lidos.Select(a => (a.AutorId!.Value, a.Tipo!.Value, a.Titulo, a.Fixado, a.Enviado, a.PublicacaoEmUtc, a.ExpiracaoEmUtc)));
        Assert.Equal("## Datas\n\nO escritório fecha.", lidos[0].Conteudo);
    }

    [Fact]
    public async Task Leitura_do_aviso_grava_a_data_em_menos_3_horas_o_trigger_cria_a_linha_de_quem_ja_era_ativo_e_a_chave_unica_recusa_a_segunda()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, quemJaEra) = await NovaPessoaAsync(conexao, "leitura.a@teste.com");
        var aviso = Aviso.Criar(quemJaEra.Id, TipoDeAviso.Simples, "Aviso", "Texto", fixado: false, enviado: true, Agora, null);
        db.Avisos.Add(aviso);
        await db.SaveChangesAsync();
        var (db2, entrouDepois) = await NovaPessoaAsync(conexao, "leitura.b@teste.com"); // admitido depois do aviso: o trigger não criou a linha dele

        var daqueleQueJaEra = await db.LeiturasDeAviso.AsNoTracking().SingleAsync(l => l.AvisoId == aviso.Id && l.FuncionarioId == quemJaEra.Id);
        var lida = LeituraDeAviso.Criar(aviso.Id, entrouDepois.Id, visualizado: true, Agora);
        db2.LeiturasDeAviso.Add(lida);
        await db2.SaveChangesAsync();

        Assert.Equal((false, null), (daqueleQueJaEra.Visualizado, daqueleQueJaEra.VisualizadoEmUtc));
        Assert.Equal(("2026-10-07 09:42:00", 1), (Texto(await EscalarAsync(conexao, $"SELECT DATA_VISUALIZACAO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO_VISUALIZACAO = {lida.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT VISUALIZADO FROM AVISOS_VISUALIZACOES WHERE ID_AVISO_VISUALIZACAO = {lida.Id}"))));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var relida = await outro.LeiturasDeAviso.AsNoTracking().SingleAsync(l => l.Id == lida.Id);
        Assert.Equal((aviso.Id, entrouDepois.Id, true, (DateTime?)AgoraAoSegundo), (relida.AvisoId, relida.FuncionarioId, relida.Visualizado, relida.VisualizadoEmUtc));

        await using var terceiro = ContextoDeTeste.Novo(conexao);
        terceiro.LeiturasDeAviso.Add(LeituraDeAviso.Criar(aviso.Id, entrouDepois.Id, visualizado: true, Agora));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => terceiro.SaveChangesAsync());
        Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
    }

    [Fact]
    public async Task Holerite_grava_o_ano_o_tipo_a_versao_e_a_publicacao_em_menos_3_horas_com_o_caminho_do_arquivo()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "holerite1@teste.com");
        var holerite = Holerite.Criar(pessoa.Id, 1, 2026, TipoDeHolerite.DecimoTerceiro, 2, Agora, "holerites/abc.pdf");
        db.Holerites.Add(holerite);

        await db.SaveChangesAsync();

        Assert.Equal((1, 2026, "DECIMO_TERCEIRO", 2, "2026-10-07 09:42:00", "holerites/abc.pdf"), (Convert.ToInt32(await EscalarAsync(conexao, $"SELECT MES FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ANO FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")), (await EscalarAsync(conexao, $"SELECT TIPO_HOLERITE FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")) as string, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT VERSAO FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")), Texto(await EscalarAsync(conexao, $"SELECT DATA_PUBLICACAO FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")), (await EscalarAsync(conexao, $"SELECT CAMINHO_ARQUIVO FROM HOLERITES WHERE ID_HOLERITE = {holerite.Id}")) as string));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.Holerites.AsNoTracking().SingleAsync(h => h.Id == holerite.Id);
        Assert.Equal((pessoa.Id, 1, 2026, TipoDeHolerite.DecimoTerceiro, 2, (DateTime?)AgoraAoSegundo, "holerites/abc.pdf"), (lido.FuncionarioId, lido.Mes, lido.Ano, lido.Tipo, lido.Versao, lido.PublicacaoEmUtc, lido.CaminhoArquivo));
    }

    [Fact]
    public async Task Advertencia_nasce_pendente_com_o_dia_do_ocorrido_e_as_colunas_de_ciencia_e_contestacao_nulas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "advertencia1@teste.com");
        var advertencia = Advertencia.Registrar(pessoa.Id, null, new DateOnly(2026, 10, 5), TipoDeAdvertencia.Escrita, "Atrasos repetidos", "Três atrasos de mais de 20 minutos na mesma semana.");
        db.Advertencias.Add(advertencia);

        await db.SaveChangesAsync();

        Assert.Equal(("PENDENTE", "ESCRITA", "2026-10-05"), ((await EscalarAsync(conexao, $"SELECT STATUS_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {advertencia.Id}")) as string, (await EscalarAsync(conexao, $"SELECT TIPO_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {advertencia.Id}")) as string, Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_ADVERTENCIA FROM ADVERTENCIAS WHERE ID_ADVERTENCIA = {advertencia.Id}"), System.Globalization.CultureInfo.InvariantCulture).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture)));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.Advertencias.AsNoTracking().SingleAsync(a => a.Id == advertencia.Id);
        Assert.Equal((pessoa.Id, (int?)null, (DateOnly?)new DateOnly(2026, 10, 5), (TipoDeAdvertencia?)TipoDeAdvertencia.Escrita, "Atrasos repetidos", "Três atrasos de mais de 20 minutos na mesma semana.", (SituacaoDaAdvertencia?)SituacaoDaAdvertencia.Pendente, (DateTime?)null, (string?)null, (DateTime?)null), (lida.FuncionarioId, lida.GestorId, lida.Data, lida.Tipo, lida.Motivo, lida.Descricao, lida.Situacao, lida.CienciaEmUtc, lida.TextoContestacao, lida.ContestacaoEmUtc));
    }
}
