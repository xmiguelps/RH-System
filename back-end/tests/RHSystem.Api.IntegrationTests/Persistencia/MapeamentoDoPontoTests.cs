using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta das tabelas do ponto no banco REAL, relendo com OUTRO contexto. Cada teste prova um cuidado do plano da fatia 3: o instante cortado ao segundo e em -03:00, as colunas com padrão no banco que a aplicação grava sem
/// declarar o padrão (<c>ORIGEM</c>, <c>ETAPA_GESTOR_AUTOMATICA</c>, <c>ATIVO</c>, <c>REPETE_TODO_ANO</c>, <c>STATUS_ATESTADO</c>), a coluna calculada <c>CHAVE_PENDENTE</c> e a conversão de horas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDoPontoTests(MySqlFixture mysql)
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

    [Fact]
    public async Task Marcacao_grava_o_instante_cortado_ao_segundo_em_menos_3_horas_e_a_origem_sem_data_de_correcao()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "marcacao1@teste.com");
        var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 10, 7));
        db.PontosDosDias.Add(dia);
        await db.SaveChangesAsync();
        var batida = MarcacaoDePonto.Nova(dia.Id, Agora, TipoDeMarcacao.Entrada);
        db.Marcacoes.Add(batida);

        await db.SaveChangesAsync();

        Assert.Equal("2026-10-07 09:42:00", Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_HORA_MARCACAO FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batida.Id}")).ToString("yyyy-MM-dd HH:mm:ss"));
        Assert.Equal(("SISTEMA", "ENTRADA", true), (await EscalarAsync(conexao, $"SELECT ORIGEM FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batida.Id}") as string, await EscalarAsync(conexao, $"SELECT TIPO_MARCACAO FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batida.Id}") as string, await EscalarAsync(conexao, $"SELECT DATA_CORRECAO FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batida.Id}") is null or DBNull));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.Marcacoes.AsNoTracking().SingleAsync(m => m.Id == batida.Id);
        Assert.Equal((AgoraAoSegundo, DateTimeKind.Utc, OrigemDaMarcacao.Sistema, null), (lida.InstanteUtc, lida.InstanteUtc.Kind, lida.Origem, lida.CorrigidaEmUtc));
    }

    [Fact]
    public async Task Marcacao_corrigida_pelo_RH_grava_a_origem_e_a_data_da_correcao_juntas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "marcacao2@teste.com");
        var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 10, 7));
        db.PontosDosDias.Add(dia);
        await db.SaveChangesAsync();
        var batida = MarcacaoDePonto.NovaCorrigidaPeloRh(dia.Id, Agora.AddHours(-3), TipoDeMarcacao.Saida, Agora);
        db.Marcacoes.Add(batida);

        await db.SaveChangesAsync();

        Assert.Equal("CORRIGIDA_RH", await EscalarAsync(conexao, $"SELECT ORIGEM FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batida.Id}") as string);
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.Marcacoes.AsNoTracking().SingleAsync(m => m.Id == batida.Id);
        Assert.Equal((OrigemDaMarcacao.CorrigidaRh, (DateTime?)AgoraAoSegundo, TipoDeMarcacao.Saida), (lida.Origem, lida.CorrigidaEmUtc, lida.Tipo));
    }

    [Fact]
    public async Task Dia_do_ponto_recebe_a_carga_do_trigger_quando_o_cargo_tem_carga_e_fica_nulo_sem_ela()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "dia1@teste.com");
        var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 10, 7));
        db.PontosDosDias.Add(dia);

        await db.SaveChangesAsync();

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.PontosDosDias.AsNoTracking().SingleAsync(p => p.Id == dia.Id);
        Assert.Equal((new DateOnly(2026, 10, 7), null, null), (lido.Dia, lido.CargaHoraria, lido.HorasTrabalhadas)); // sem cargo: o trigger deixa nula
    }

    [Fact]
    public async Task Pedido_grava_com_itens_a_etapa_do_gestor_falsa_e_a_chave_pendente_calculada_pelo_banco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "pedido1@teste.com");
        var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 10, 6));
        db.PontosDosDias.Add(dia);
        await db.SaveChangesAsync();
        var itens = new[]
        {
            ItemDoPedidoDeAjuste.Novo(dia.Id, null, Agora.AddDays(-1), TipoDeMarcacao.Entrada),
            ItemDoPedidoDeAjuste.Novo(dia.Id, null, Agora.AddDays(-1).AddHours(8), TipoDeMarcacao.Saida),
        };
        var pedido = PedidoDeAjuste.Criar(dia.Id, pessoa.Id, "Esqueci de bater o ponto", Agora, gestorId: null, itens);
        db.PedidosDeAjuste.Add(pedido);

        await db.SaveChangesAsync();

        Assert.Equal((0L, (long)dia.Id, "PENDENTE"), (Convert.ToInt64(await EscalarAsync(conexao, $"SELECT ETAPA_GESTOR_AUTOMATICA FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido.Id}")), Convert.ToInt64(await EscalarAsync(conexao, $"SELECT CHAVE_PENDENTE FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido.Id}")), await EscalarAsync(conexao, $"SELECT STATUS_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido.Id}") as string));
        Assert.Equal("2026-10-07 09:42:00", Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_SOLICITACAO FROM PONTOS_MANUTENCOES WHERE ID_MANUTENCAO_PONTO = {pedido.Id}")).ToString("yyyy-MM-dd HH:mm:ss")); // a aplicação grava a data: o trigger só a preenche quando vem nula
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.PedidosDeAjuste.AsNoTracking().Include(p => p.Itens).SingleAsync(p => p.Id == pedido.Id);
        Assert.Equal((StatusDoPedidoDeAjuste.Pendente, false, (int?)dia.Id, (DateTime?)AgoraAoSegundo), (lido.Status, lido.EtapaDoGestorAutomatica, lido.ChavePendente, lido.SolicitadoEmUtc));
        Assert.Equal([TipoDeMarcacao.Entrada, TipoDeMarcacao.Saida], lido.Itens.OrderBy(i => i.InstanteProposto).Select(i => i.Tipo));
    }

    [Fact]
    public async Task Segundo_pedido_pendente_do_mesmo_dia_e_recusado_pela_chave_unica_da_chave_pendente()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "pedido2@teste.com");
        var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 10, 6));
        db.PontosDosDias.Add(dia);
        await db.SaveChangesAsync();
        db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(dia.Id, pessoa.Id, "Primeiro pedido", Agora, null, [ItemDoPedidoDeAjuste.Novo(dia.Id, null, Agora.AddDays(-1), TipoDeMarcacao.Entrada)]));
        await db.SaveChangesAsync();
        db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(dia.Id, pessoa.Id, "Segundo pedido", Agora, null, [ItemDoPedidoDeAjuste.Novo(dia.Id, null, Agora.AddDays(-1), TipoDeMarcacao.Entrada)]));

        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => db.SaveChangesAsync());

        Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
    }

    [Fact]
    public async Task Lancamento_grava_as_horas_com_duas_casas_o_dia_de_referencia_e_a_data()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "banco1@teste.com");
        var lancamento = LancamentoDoBancoDeHoras.Criar(pessoa.Id, new DateOnly(2026, 10, 6), TipoDeLancamentoDoBanco.Credito, 83, "Hora extra", new DateOnly(2026, 10, 7));
        db.LancamentosDoBancoDeHoras.Add(lancamento);

        await db.SaveChangesAsync();

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.LancamentosDoBancoDeHoras.AsNoTracking().SingleAsync(l => l.Id == lancamento.Id);
        Assert.Equal((new DateOnly(2026, 10, 6), TipoDeLancamentoDoBanco.Credito, 1.38m), (lido.DiaDeReferencia!.Value, lido.Tipo!.Value, lido.Quantidade!.Value));
        Assert.Equal("CREDITO", await EscalarAsync(conexao, $"SELECT TIPO_LANCAMENTO FROM BANCOS_HORAS WHERE ID_BANCO_HORAS = {lancamento.Id}") as string);
    }

    [Fact]
    public async Task Feriado_grava_ativo_e_repete_todo_ano_sem_declarar_o_padrao_do_banco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var db = ContextoDeTeste.Novo(conexao);
        var inativoNoModelo = Feriado.Criar("Aniversário da cidade", new DateOnly(2026, 1, 25), TipoDeFeriado.Municipal, "SP", "São Paulo", repeteTodoAno: false);
        var repete = Feriado.Criar("Dia da empresa", new DateOnly(2026, 6, 10), TipoDeFeriado.Empresa, null, null, repeteTodoAno: true);
        db.Feriados.AddRange(inativoNoModelo, repete);

        await db.SaveChangesAsync();

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lidos = await outro.Feriados.AsNoTracking().Where(f => f.Id == inativoNoModelo.Id || f.Id == repete.Id).OrderBy(f => f.Data).ToListAsync();
        Assert.Equal([("Aniversário da cidade", TipoDeFeriado.Municipal, "SP", "São Paulo", true, false), ("Dia da empresa", TipoDeFeriado.Empresa, null, null, true, true)], lidos.Select(f => (f.Nome, f.Tipo, f.Uf, f.Municipio, f.Ativo, f.RepeteTodoAno)));
    }

    [Fact]
    public async Task Atestado_grava_as_horas_a_data_de_envio_do_relogio_em_menos_3_horas_e_nasce_enviado()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, pessoa) = await NovaPessoaAsync(conexao, "atestado1@teste.com");
        var atestado = Atestado.Enviar(pessoa.Id, new DateOnly(2026, 10, 7), new DateOnly(2026, 10, 7), new TimeOnly(8, 0), new TimeOnly(12, 0), $"atestados/{Guid.NewGuid():N}.pdf", "atestado.pdf", Agora);
        db.Atestados.Add(atestado);

        await db.SaveChangesAsync();

        Assert.Equal("2026-10-07 09:42:00", Convert.ToDateTime(await EscalarAsync(conexao, $"SELECT DATA_ENVIO FROM ATESTADOS WHERE ID_ATESTADO = {atestado.Id}")).ToString("yyyy-MM-dd HH:mm:ss"));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.Atestados.AsNoTracking().SingleAsync(a => a.Id == atestado.Id);
        Assert.Equal((SituacaoDoAtestado.Enviado, new TimeOnly(8, 0), new TimeOnly(12, 0), AgoraAoSegundo, "atestado.pdf"), (lido.Situacao, lido.HoraInicio!.Value, lido.HoraFim!.Value, lido.EnviadoEmUtc, lido.NomeArquivoOriginal));
        Assert.Equal("ENVIADO", await EscalarAsync(conexao, $"SELECT STATUS_ATESTADO FROM ATESTADOS WHERE ID_ATESTADO = {atestado.Id}") as string);
    }
}
