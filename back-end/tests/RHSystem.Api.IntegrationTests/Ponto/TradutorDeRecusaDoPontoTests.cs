using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Comum;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;
using RHSystem.Infrastructure.Ponto;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// O tradutor de recusa do banco com exceções REAIS do MySQL: as de chave (uma por chave do ponto) vêm de gravações de verdade, e as regras de trigger também; para os textos que a aplicação só vê em condições raras, uma procedure de
/// teste levanta o mesmo <c>SIGNAL SQLSTATE '45000'</c> com a mensagem que se quer. Cada teste fixa o que o tradutor devolve por rota (o texto de "inativo" e o de "batida duplicada" mudam de uma rota para outra).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TradutorDeRecusaDoPontoTests(MySqlFixture mysql)
{
    private static readonly DateTime Instante = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static readonly DateOnly Dia = new(2026, 10, 7);
    private static int _sequencia;

    private async Task<string> ConexaoAsync()
    {
        var (conexao, _) = await BancoCompartilhado.ObterAsync(mysql, nameof(TradutorDeRecusaDoPontoTests), async nova =>
        {
            await Sql.ExecutarAsync(nova, "CREATE PROCEDURE P_SINALIZA(IN mensagem VARCHAR(200)) BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = mensagem; END");
            return 0;
        });
        return conexao;
    }

    private static async Task<(int FuncionarioId, int PontoId)> NovaPessoaComPontoAsync(string conexao, DateOnly? dia = null)
    {
        var n = Interlocked.Increment(ref _sequencia);
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa tradutor {n}", $"tradutor{n}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        var ponto = PontoDoDia.Novo(pessoa.Id, dia ?? Dia);
        db.PontosDosDias.Add(ponto);
        await db.SaveChangesAsync();
        return (pessoa.Id, ponto.Id);
    }

    private static async Task<Exception> ExcecaoDeAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception erro)
        {
            return erro;
        }

        throw new InvalidOperationException("A gravação devia ter sido recusada pelo banco.");
    }

    private static async Task<int> NovaBatidaAsync(string conexao, int pontoId, DateTime instante, TipoDeMarcacao tipo)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var batida = MarcacaoDePonto.Nova(pontoId, instante, tipo);
        db.Marcacoes.Add(batida);
        await db.SaveChangesAsync();
        return batida.Id;
    }

    private static async Task<Exception> PedidoRecusadoAsync(string conexao, int funcionarioId, int pontoId, params ItemDoPedidoDeAjuste[] itens) =>
        await ExcecaoDeAsync(async () =>
        {
            await using var db = ContextoDeTeste.Novo(conexao);
            db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(pontoId, funcionarioId, "Justificativa de teste", Instante, null, itens));
            await db.SaveChangesAsync();
        });

    private static (int Status, string? Codigo, string Detalhe, string? RegraDoBanco)? Resumo(ProblemaDeApiException? problema) =>
        problema is null ? null : (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes?["regraDoBanco"] as string);

    // ---------------------------------------------------------------- chave repetida (1062), pelo nome da chave

    [Fact]
    public async Task Batida_no_mesmo_instante_e_409_PONTO_BATIDA_DUPLICADA_com_o_texto_de_cada_rota_e_nulo_nas_outras()
    {
        var conexao = await ConexaoAsync();
        var (_, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Entrada);
        var erro = await ExcecaoDeAsync(() => NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Saida));

        Assert.Equal((409, "PONTO_BATIDA_DUPLICADA", "O seu ponto já foi registrado agora há pouco.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao)));
        Assert.Equal((409, "PONTO_BATIDA_DUPLICADA", "Há duas batidas no mesmo horário neste dia. Corrija os horários.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CorrigirDia)));
        Assert.Equal((409, "PONTO_BATIDA_DUPLICADA", "Já existe uma batida neste horário. Nada foi alterado.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido)));
        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido));
        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.Feriado));
    }

    [Fact]
    public async Task Segundo_pedido_pendente_do_mesmo_dia_e_409_PONTO_PEDIDO_JA_EM_ANALISE()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(pontoId, pessoaId, "Primeiro", Instante, null, [ItemDoPedidoDeAjuste.Novo(pontoId, null, Instante, TipoDeMarcacao.Entrada)]));
            await db.SaveChangesAsync();
        }

        var erro = await PedidoRecusadoAsync(conexao, pessoaId, pontoId, ItemDoPedidoDeAjuste.Novo(pontoId, null, Instante.AddHours(1), TipoDeMarcacao.Saida));

        Assert.Equal((409, "PONTO_PEDIDO_JA_EM_ANALISE", "Este dia já tem um pedido de ajuste em análise. Espere a resposta antes de pedir outro.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido)));
    }

    [Fact]
    public async Task Dois_itens_no_mesmo_instante_do_pedido_e_422_PONTO_SEQUENCIA_INVALIDA()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);

        var erro = await PedidoRecusadoAsync(conexao, pessoaId, pontoId, ItemDoPedidoDeAjuste.Novo(pontoId, null, Instante, TipoDeMarcacao.Entrada), ItemDoPedidoDeAjuste.Novo(pontoId, null, Instante, TipoDeMarcacao.Saida));

        Assert.Equal((422, "PONTO_SEQUENCIA_INVALIDA", "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido)));
    }

    [Fact]
    public async Task Feriado_repetido_na_mesma_data_e_409_FERIADO_DUPLICADO()
    {
        var conexao = await ConexaoAsync();
        await PontoDeTeste.NovoFeriadoAsync(conexao, "Feriado do tradutor", new DateOnly(2026, 8, 20), "EMPRESA");

        var erro = await ExcecaoDeAsync(() => PontoDeTeste.NovoFeriadoAsync(conexao, "Feriado do tradutor", new DateOnly(2026, 8, 20), "EMPRESA"));

        Assert.Equal((409, "FERIADO_DUPLICADO", "Já existe um feriado com este nome nesta data.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.Feriado)));
    }

    [Fact]
    public async Task O_valor_da_chave_com_apostrofo_e_ate_o_texto_for_key_nao_atrapalha_o_nome_da_chave()
    {
        var conexao = await ConexaoAsync();
        const string nome = "Dia d'Ouro for key 'x";
        await PontoDeTeste.NovoFeriadoAsync(conexao, nome.Replace("'", "''", StringComparison.Ordinal), new DateOnly(2026, 8, 21), "EMPRESA");

        var erro = await ExcecaoDeAsync(() => PontoDeTeste.NovoFeriadoAsync(conexao, nome.Replace("'", "''", StringComparison.Ordinal), new DateOnly(2026, 8, 21), "EMPRESA"));

        Assert.Equal("UQ_FERIADOS_DATA_NOME", ErrosDoBanco.NomeDaChaveRepetida(erro));
        Assert.Equal("FERIADO_DUPLICADO", TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.Feriado)?.Codigo);
    }

    [Fact]
    public async Task Dia_criado_em_paralelo_e_chave_que_o_ponto_nao_traduz()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, _) = await NovaPessoaComPontoAsync(conexao);

        var erro = await ExcecaoDeAsync(async () =>
        {
            await using var db = ContextoDeTeste.Novo(conexao);
            db.PontosDosDias.Add(PontoDoDia.Novo(pessoaId, Dia));
            await db.SaveChangesAsync();
        });

        Assert.Equal("UQ_PONTOS_FUNCIONARIO_DATA", ErrosDoBanco.NomeDaChaveRepetida(erro)); // o chamador relê o dia
        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao));
    }

    // ---------------------------------------------------------------- chave estrangeira (1451 e 1452)

    [Fact]
    public async Task Apagar_batida_que_um_pedido_apontou_e_409_PONTO_MARCACAO_EM_USO()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);
        var batidaId = await NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Entrada);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(pontoId, pessoaId, "Corrige", Instante, null, [ItemDoPedidoDeAjuste.Novo(pontoId, batidaId, Instante.AddMinutes(5), TipoDeMarcacao.Entrada)]));
            await db.SaveChangesAsync();
        }

        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"DELETE FROM PONTOS_MARCACOES WHERE ID_MARCACAO = {batidaId}"));

        Assert.Equal((409, "PONTO_MARCACAO_EM_USO", "Esta batida está ligada a um pedido de ajuste e não pode ser removida.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CorrigirDia)));
    }

    [Fact]
    public async Task Apagar_o_dia_que_tem_batida_e_outra_chave_estrangeira_e_nao_e_traduzido()
    {
        var conexao = await ConexaoAsync();
        var (_, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Entrada);

        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"DELETE FROM PONTOS WHERE ID_PONTO = {pontoId}"));

        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CorrigirDia));
    }

    [Fact]
    public async Task Batida_de_um_dia_que_nao_existe_e_outra_chave_estrangeira_e_nao_e_traduzida()
    {
        var conexao = await ConexaoAsync();

        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "INSERT INTO PONTOS_MARCACOES (ID_PONTO, DATA_HORA_MARCACAO, TIPO_MARCACAO) VALUES (999999, '2026-10-07 09:00:00', 'ENTRADA')"));

        Assert.True(ErrosDoBanco.EhChaveEstrangeiraSemPai(erro));
        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao));
    }

    [Fact]
    public async Task Item_que_aponta_para_batida_de_outro_dia_e_422_PONTO_SEQUENCIA_INVALIDA()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);
        var (_, outroPontoId) = await NovaPessoaComPontoAsync(conexao);
        var batidaDeOutroDia = await NovaBatidaAsync(conexao, outroPontoId, Instante, TipoDeMarcacao.Entrada);

        var erro = await PedidoRecusadoAsync(conexao, pessoaId, pontoId, ItemDoPedidoDeAjuste.Novo(pontoId, batidaDeOutroDia, Instante, TipoDeMarcacao.Entrada));

        Assert.Equal((422, "PONTO_SEQUENCIA_INVALIDA", "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários.", null), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido)));
    }

    // ---------------------------------------------------------------- regras de trigger (45000), triggers de verdade

    [Fact]
    public async Task Ponto_de_funcionario_inativo_e_422_FUNCIONARIO_INATIVO_com_o_texto_da_rota_e_a_regra_original()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, _) = await NovaPessoaComPontoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {pessoaId}");
        var erro = await ExcecaoDeAsync(async () =>
        {
            await using var db = ContextoDeTeste.Novo(conexao);
            db.PontosDosDias.Add(PontoDoDia.Novo(pessoaId, new DateOnly(2026, 9, 1)));
            await db.SaveChangesAsync();
        });
        const string regra = "Nao e possivel registrar ponto de funcionario inativo.";

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Este colaborador está inativo e não pode registrar ponto.", regra), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao)));
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Este colaborador está inativo e não pode pedir ajuste de ponto.", regra), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CriarPedido)));
        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Este colaborador está inativo e o ponto dele não pode mais ser alterado.", regra), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CorrigirDia)));
        Assert.Equal((422, (string?)null, "A operação foi recusada por uma regra do banco de dados.", regra), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido))); // sem texto próprio: a recusa genérica
    }

    [Fact]
    public async Task Alterar_o_ponto_de_inativo_tambem_e_FUNCIONARIO_INATIVO_com_a_regra_do_banco_como_veio()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-10-30' WHERE ID_FUNCIONARIO = {pessoaId}");
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE PONTOS SET CARGA_HORARIA = 6 WHERE ID_PONTO = {pontoId}"));

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Este colaborador está inativo e o ponto dele não pode mais ser alterado.", "Nao e possivel alterar ponto de funcionario inativo."), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.CorrigirDia)));
    }

    [Fact]
    public async Task Dia_anterior_a_admissao_e_422_sem_codigo_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, _) = await NovaPessoaComPontoAsync(conexao);
        var erro = await ExcecaoDeAsync(async () =>
        {
            await using var db = ContextoDeTeste.Novo(conexao);
            db.PontosDosDias.Add(PontoDoDia.Novo(pessoaId, new DateOnly(2025, 12, 31)));
            await db.SaveChangesAsync();
        });

        Assert.Equal((422, (string?)null, "A operação foi recusada por uma regra do banco de dados.", "A data do ponto e anterior a admissao."), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao)));
    }

    [Fact]
    public async Task Gestor_decidindo_o_proprio_pedido_e_422_PEDIDO_PROPRIO_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var (pessoaId, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO GESTORES (ID_FUNCIONARIO) VALUES ({pessoaId})");
        var gestorId = Convert.ToInt32(await Sql.EscalarAsync(conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoaId}"), System.Globalization.CultureInfo.InvariantCulture);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.PedidosDeAjuste.Add(PedidoDeAjuste.Criar(pontoId, pessoaId, "Próprio", Instante, null, [ItemDoPedidoDeAjuste.Novo(pontoId, null, Instante, TipoDeMarcacao.Entrada)]));
            await db.SaveChangesAsync();
        }

        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE PONTOS_MANUTENCOES SET STATUS_SOLICITACAO = 'APROVADA', ID_GESTOR = {gestorId} WHERE ID_PONTO = {pontoId}"));

        Assert.Equal((422, "PEDIDO_PROPRIO", "A operação foi recusada por uma regra do banco de dados.", "O gestor nao pode analisar a propria solicitacao."), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido)));
    }

    // ---------------------------------------------------------------- textos de trigger que só aparecem em condição rara (procedure de teste)

    [Theory]
    [InlineData("Solicitacao analisada nao pode mudar de status.", 409, "PEDIDO_JA_DECIDIDO", "Este pedido já foi respondido. Atualize a lista.", null)]
    [InlineData("Informe o gestor responsavel pela analise.", 422, "PEDIDO_RH_FORA_DE_GESTORES", "Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor.", "Informe o gestor responsavel pela analise.")]
    [InlineData("O gestor nao pode analisar a propria solicitacao.", 422, "PEDIDO_PROPRIO", "A operação foi recusada por uma regra do banco de dados.", "O gestor nao pode analisar a propria solicitacao.")]
    [InlineData("A data do ponto e posterior ao desligamento.", 422, null, "A operação foi recusada por uma regra do banco de dados.", "A data do ponto e posterior ao desligamento.")]
    [InlineData("Informe o gestor responsavel pela analise das ferias.", 422, null, "A operação foi recusada por uma regra do banco de dados.", "Informe o gestor responsavel pela analise das ferias.")] // é do trigger das férias, não do ponto
    [InlineData("O gestor nao pode analisar as proprias ferias.", 422, null, "A operação foi recusada por uma regra do banco de dados.", "O gestor nao pode analisar as proprias ferias.")]
    [InlineData("Qualquer outra regra do banco.", 422, null, "A operação foi recusada por uma regra do banco de dados.", "Qualquer outra regra do banco.")]
    public async Task Cada_texto_de_trigger_vira_a_resposta_do_contrato(string mensagem, int status, string? codigo, string detalhe, string? regraDoBanco)
    {
        var conexao = await ConexaoAsync();
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"CALL P_SINALIZA('{mensagem}')"));

        Assert.Equal((status, codigo, detalhe, regraDoBanco), Resumo(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.DecidirPedido)));
    }

    // ---------------------------------------------------------------- o que não é do ponto

    [Fact]
    public async Task Excecao_que_nao_e_do_MySQL_ou_erro_que_o_ponto_nao_conhece_volta_nulo()
    {
        var conexao = await ConexaoAsync();
        var sintaxe = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "SELECT * FROM TABELA_QUE_NAO_EXISTE"));

        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(new InvalidOperationException("outra coisa"), ContextoDaRota.RegistrarMarcacao));
        Assert.Null(TradutorDeRecusaDoPonto.Traduzir(sintaxe, ContextoDaRota.RegistrarMarcacao));
        Assert.IsType<MySqlException>(sintaxe);
    }

    [Fact]
    public async Task O_MySQL_dentro_de_DbUpdateException_tambem_e_traduzido()
    {
        var conexao = await ConexaoAsync();
        var (_, pontoId) = await NovaPessoaComPontoAsync(conexao);
        await NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Entrada);
        var erro = await ExcecaoDeAsync(() => NovaBatidaAsync(conexao, pontoId, Instante, TipoDeMarcacao.Saida));

        Assert.IsType<DbUpdateException>(erro);
        Assert.NotNull(TradutorDeRecusaDoPonto.Traduzir(erro, ContextoDaRota.RegistrarMarcacao));
        Assert.NotNull(TradutorDeRecusaDoPonto.Traduzir(erro.InnerException!, ContextoDaRota.RegistrarMarcacao)); // e o MySqlException solto, sem o embrulho
    }
}
