using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Empresa;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>As sementes do ponto para os testes de banco real (cada uma grava direto, sem passar pela API).</summary>
internal static class PontoDeTeste
{
    private static int _sequencia;

    /// <summary>Grava ou atualiza a linha única da empresa (a saída fica nove horas depois da entrada).</summary>
    public static async Task ConfigurarEmpresaAsync(string conexao, string uf = "SP", string municipio = "São Paulo", TimeOnly? entrada = null, int tolerancia = 10)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var inicio = entrada ?? new TimeOnly(8, 0);
        var existente = await db.ConfiguracoesDaEmpresa.FirstOrDefaultAsync();
        if (existente is null)
        {
            db.ConfiguracoesDaEmpresa.Add(ConfiguracaoDaEmpresa.Criar("Empresa de teste", uf, municipio, inicio, inicio.AddHours(9), tolerancia, 5));
        }
        else
        {
            existente.Atualizar("Empresa de teste", uf, municipio, inicio, inicio.AddHours(9), tolerancia, 5);
        }

        await db.SaveChangesAsync();
    }

    public static async Task NovoFeriadoAsync(string conexao, string nome, DateOnly data, string tipo, string? uf = null, string? municipio = null, bool ativo = true)
    {
        var ufSql = uf is null ? "NULL" : $"'{uf}'";
        var municipioSql = municipio is null ? "NULL" : $"'{municipio}'";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIADOS (NOME_FERIADO, DATA_FERIADO, TIPO_FERIADO, UF, MUNICIPIO, ATIVO) VALUES ('{nome}', '{data:yyyy-MM-dd}', '{tipo}', {ufSql}, {municipioSql}, {(ativo ? "TRUE" : "FALSE")})");
    }

    /// <summary>Grava as batidas do dia (hora cheia em UTC) criando o ponto do dia se faltar.</summary>
    public static async Task BaterAsync(string conexao, int funcionarioId, DateOnly dia, params (TipoDeMarcacao Tipo, int Hora)[] batidas)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var ponto = await db.PontosDosDias.FirstOrDefaultAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia);
        if (ponto is null)
        {
            ponto = PontoDoDia.Novo(funcionarioId, dia);
            db.PontosDosDias.Add(ponto);
            await db.SaveChangesAsync();
        }

        foreach (var (tipo, hora) in batidas)
        {
            db.Marcacoes.Add(MarcacaoDePonto.Nova(ponto.Id, new DateTime(dia.Year, dia.Month, dia.Day, hora, 0, 0, DateTimeKind.Utc), tipo));
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Grava as batidas do dia pelo instante exato (<c>"2026-10-01T10:56:00Z"</c>), criando o ponto do dia se faltar.</summary>
    public static async Task BaterEmAsync(string conexao, int funcionarioId, DateOnly dia, params (TipoDeMarcacao Tipo, string InstanteUtc)[] batidas)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var ponto = await PontoDoDiaAsync(db, funcionarioId, dia);
        foreach (var (tipo, instante) in batidas)
        {
            db.Marcacoes.Add(MarcacaoDePonto.Nova(ponto.Id, DateTime.Parse(instante, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal), tipo));
        }

        await db.SaveChangesAsync();
    }

    /// <summary>Uma batida que o RH corrigiu (<c>CORRIGIDA_RH</c> com a data da correção).</summary>
    public static async Task BaterCorrigidaPeloRhAsync(string conexao, int funcionarioId, DateOnly dia, TipoDeMarcacao tipo, string instanteUtc, string corrigidaEmUtc)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var ponto = await PontoDoDiaAsync(db, funcionarioId, dia);
        db.Marcacoes.Add(MarcacaoDePonto.NovaCorrigidaPeloRh(
            ponto.Id,
            DateTime.Parse(instanteUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal),
            tipo,
            DateTime.Parse(corrigidaEmUtc, CultureInfo.InvariantCulture, DateTimeStyles.AdjustToUniversal | DateTimeStyles.AssumeUniversal)));
        await db.SaveChangesAsync();
    }

    /// <summary>Um pedido de ajuste <c>PENDENTE</c> do dia, com uma batida de saída pedida; <paramref name="gestorId"/> é <c>GESTORES.ID_GESTOR</c> (etapa do gestor) ou nulo (etapa do RH).</summary>
    public static async Task<int> NovoPedidoPendenteAsync(string conexao, int funcionarioId, DateOnly dia, int? gestorId = null)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var ponto = await PontoDoDiaAsync(db, funcionarioId, dia);
        var instante = new DateTime(dia.Year, dia.Month, dia.Day, 22, 0, 0, DateTimeKind.Utc);
        var pedido = PedidoDeAjuste.Criar(ponto.Id, funcionarioId, "Esqueci de bater a saída", instante.AddDays(1), gestorId, [ItemDoPedidoDeAjuste.Novo(ponto.Id, null, instante, TipoDeMarcacao.Saida)]);
        db.PedidosDeAjuste.Add(pedido);
        await db.SaveChangesAsync();
        return pedido.Id;
    }

    /// <summary>Um lançamento do banco de horas (<c>QUANTIDADE</c> em horas com 2 casas, em texto com ponto).</summary>
    public static async Task NovoLancamentoAsync(string conexao, int funcionarioId, string diaDeReferencia, string tipo, string horas) =>
        await Sql.ExecutarAsync(conexao, $"INSERT INTO BANCOS_HORAS (ID_FUNCIONARIO, DATA_LANCAMENTO, TIPO_LANCAMENTO, QUANTIDADE, DIA_REFERENCIA) VALUES ({funcionarioId}, '{diaDeReferencia}', '{tipo}', {horas}, '{diaDeReferencia}')");

    private static async Task<PontoDoDia> PontoDoDiaAsync(AppDbContext db, int funcionarioId, DateOnly dia)
    {
        var ponto = await db.PontosDosDias.FirstOrDefaultAsync(p => p.FuncionarioId == funcionarioId && p.Dia == dia);
        if (ponto is null)
        {
            ponto = PontoDoDia.Novo(funcionarioId, dia);
            db.PontosDosDias.Add(ponto);
            await db.SaveChangesAsync();
        }

        return ponto;
    }

    /// <summary>Férias direto no banco (a fatia 7 é a dona da tabela); o período aquisitivo é 02/01 a 03/01/2026, antes de qualquer férias dos testes.</summary>
    public static async Task NovasFeriasAsync(string conexao, int funcionarioId, string inicio, string fim, string status) =>
        await Sql.ExecutarAsync(conexao, $"INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, STATUS_FERIAS) VALUES ({funcionarioId}, '2026-01-02', '2026-01-03', '{inicio}', '{fim}', '{status}')");

    /// <summary>Atestado direto no banco, no estado pedido (<c>ENVIADO</c>, <c>VALIDADO</c> ou <c>RECUSADO</c>); as decisões levam o RH e a data que o banco exige.</summary>
    public static async Task NovoAtestadoAsync(string conexao, int funcionarioId, string inicio, string fim, string status, int rhId, string? horaInicio = null, string? horaFim = null)
    {
        var n = Interlocked.Increment(ref _sequencia);
        var horas = horaInicio is null ? "NULL, NULL" : $"'{horaInicio}', '{horaFim}'";
        var decisao = status == "ENVIADO" ? "NULL, NULL" : $"{rhId}, '2026-01-10 10:00:00'";
        var motivo = status == "RECUSADO" ? "'Ilegivel'" : "NULL";
        await Sql.ExecutarAsync(conexao, $"INSERT INTO ATESTADOS (ID_FUNCIONARIO, DATA_INICIO, DATA_FIM, HORA_INICIO, HORA_FIM, CAMINHO_ARQUIVO, STATUS_ATESTADO, ID_FUNCIONARIO_RH, DATA_DECISAO, MOTIVO_RECUSA) VALUES ({funcionarioId}, '{inicio}', '{fim}', {horas}, 'atestados/teste-{n}.pdf', '{status}', {decisao}, {motivo})");
    }
}
