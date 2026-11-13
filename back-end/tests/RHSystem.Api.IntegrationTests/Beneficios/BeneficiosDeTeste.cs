using System.Globalization;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Beneficios;

/// <summary>
/// O apoio dos testes de benefícios: itens novos no catálogo (nome único no banco) e linhas de adesão, dependente e trajeto semeadas direto, já no estado pedido (os <c>CHECK</c> do banco exigem resposta e data juntas, recusa com motivo e RH, ativo só com aprovado). Os instantes semeados são o relógio de -03:00.
/// Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília).
/// </summary>
internal static class BeneficiosDeTeste
{
    public const string RotaEu = "/api/eu/beneficios";

    public const string RotaDosPedidos = "/api/eu/beneficios-adesoes";

    private static int _sequencia;

    public static async Task<ApiDeAcesso> ApiAsync(MySqlFixture mysql, string chave)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, chave);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    /// <summary>Um item novo no catálogo (o nome é único no banco; sem nome, ganha um numerado). Devolve o id.</summary>
    public static async Task<int> ItemAsync(ApiDeAcesso api, string? nome = null, bool opcional = true, bool dependentes = false, bool trajeto = false, string? descricao = null)
    {
        nome ??= $"Beneficio de teste {Interlocked.Increment(ref _sequencia)}";
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO BENEFICIOS (NOME_BENEFICIO, DESCRICAO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO) VALUES (@nome, @descricao, @opcional, @dependentes, @trajeto)",
            ("@nome", nome),
            ("@descricao", descricao),
            ("@opcional", opcional),
            ("@dependentes", dependentes),
            ("@trajeto", trajeto));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_BENEFICIO FROM BENEFICIOS WHERE NOME_BENEFICIO = '{nome}'");
    }

    /// <summary>
    /// Uma linha de <c>BENEFICIOS_FUNCIONARIOS</c>. <c>APROVADO</c> e ativo sem <paramref name="pedidoEm"/> é o benefício que o RH deu direto. <c>RECUSADO</c> ganha o RH e o motivo que o banco exige; <paramref name="respondidoEm"/> sem <paramref name="rhId"/> usa a Ana (RH do elenco).
    /// </summary>
    public static async Task<int> AdesaoAsync(
        ApiDeAcesso api,
        int pessoaId,
        int beneficioId,
        string status = "APROVADO",
        bool ativo = true,
        string? pedidoEm = null,
        string? inicio = null,
        bool autoriza = false,
        string? respondidoEm = null,
        string? motivo = null)
    {
        var respondeu = status is "RECUSADO" || respondidoEm is not null;
        var rh = respondeu ? api.Elenco.Rh.FuncionarioId : (int?)null;
        var resposta = respondeu ? Data(respondidoEm ?? "2026-09-01 14:00:00") : (DateTime?)null;
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO BENEFICIOS_FUNCIONARIOS (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO, DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA, ID_FUNCIONARIO_RH, DATA_RESPOSTA, MOTIVO_RECUSA) VALUES (@pessoa, @beneficio, @ativo, @status, @pedido, @inicio, @autoriza, @rh, @resposta, @motivo)",
            ("@pessoa", pessoaId),
            ("@beneficio", beneficioId),
            ("@ativo", ativo),
            ("@status", status),
            ("@pedido", pedidoEm is null ? null : Data(pedidoEm)),
            ("@inicio", inicio),
            ("@autoriza", autoriza),
            ("@rh", rh),
            ("@resposta", resposta),
            ("@motivo", status == "RECUSADO" ? motivo ?? "Sem vaga neste mês." : motivo));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_BENEFICIO_FUNCIONARIOS FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoaId} AND ID_BENEFICIO = {beneficioId}");
    }

    public static async Task<int> DependenteAsync(
        ApiDeAcesso api,
        int adesaoId,
        string nome = "Helena Lima",
        string parentesco = "FILHA",
        string nascimento = "2019-05-21",
        string status = "PENDENTE",
        string pedidoEm = "2026-01-12 10:00:00",
        string? respondidoEm = null,
        string? motivo = null)
    {
        var respondeu = respondidoEm is not null;
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO DEPENDENTES (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA, MOTIVO_RECUSA) VALUES (@adesao, @nome, @parentesco, @nascimento, @status, @pedido, @rh, @resposta, @motivo)",
            ("@adesao", adesaoId),
            ("@nome", nome),
            ("@parentesco", parentesco),
            ("@nascimento", nascimento),
            ("@status", status),
            ("@pedido", Data(pedidoEm)),
            ("@rh", respondeu ? api.Elenco.Rh.FuncionarioId : (int?)null),
            ("@resposta", respondeu ? Data(respondidoEm!) : (DateTime?)null),
            ("@motivo", status == "RECUSADO" ? motivo ?? "Falta a certidão." : motivo));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT MAX(ID_DEPENDENTE) FROM DEPENDENTES WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");
    }

    public static async Task<int> TrajetoAsync(
        ApiDeAcesso api,
        int adesaoId,
        string origem = "Rua das Acácias, 120, Vila Mariana, São Paulo",
        string destino = "Av. Paulista, 1000, São Paulo",
        string linhas = "Ônibus 175T-10 e Metrô Linha 2",
        string status = "PENDENTE",
        string pedidoEm = "2025-08-03 09:00:00",
        string? respondidoEm = null,
        string? motivo = null)
    {
        var respondeu = respondidoEm is not null;
        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO TRAJETOS_VALE_TRANSPORTE (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATA_PEDIDO, ID_FUNCIONARIO_RH, DATA_RESPOSTA, MOTIVO_RECUSA) VALUES (@adesao, @origem, @destino, @linhas, @status, @pedido, @rh, @resposta, @motivo)",
            ("@adesao", adesaoId),
            ("@origem", origem),
            ("@destino", destino),
            ("@linhas", linhas),
            ("@status", status),
            ("@pedido", Data(pedidoEm)),
            ("@rh", respondeu ? api.Elenco.Rh.FuncionarioId : (int?)null),
            ("@resposta", respondeu ? Data(respondidoEm!) : (DateTime?)null),
            ("@motivo", status == "RECUSADO" ? motivo ?? "Endereço incompleto." : motivo));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT MAX(ID_TRAJETO) FROM TRAJETOS_VALE_TRANSPORTE WHERE ID_BENEFICIO_FUNCIONARIOS = {adesaoId}");
    }

    public static object Helena { get; } = new { nome = "Helena Lima", parentesco = "FILHA", dataNascimento = "2019-05-21" };

    public static object Trajeto { get; } = new { origem = "Rua das Acácias, 120, Vila Mariana, São Paulo", destino = "Av. Paulista, 1000, São Paulo", linhasMeios = "Metrô Linha 1 e Linha 2" };

    public static object PedidoDeAdesao(int beneficioId, string inicio = "2026-11-01", bool autoriza = true, object[]? dependentes = null, object? trajeto = null) =>
        new { beneficioId, dataInicioDesejada = inicio, autorizaDescontoEmFolha = autoriza, dependentes, trajeto };

    public static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) =>
        (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    public static long Contar(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql).GetAwaiter().GetResult();

    public static DateTime Utc(JsonElement texto) => texto.GetDateTime().ToUniversalTime();

    private static DateTime Data(string texto) => DateTime.ParseExact(texto, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
}
