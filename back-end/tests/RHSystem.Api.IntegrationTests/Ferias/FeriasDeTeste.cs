using System.Globalization;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Comunicacao;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Ferias;

internal sealed record PessoaDeFerias(int FuncionarioId, int UsuarioId, string Nome);

/// <summary>
/// O apoio dos testes de férias: pessoas novas COM login e a data de admissão pedida (o elenco foi admitido em 02/01/2026 e ainda não tem período fechado), setores com gestor, e linhas de <c>FERIAS</c> semeadas direto no banco já no estado pedido (o histórico do Bruno do contrato).
/// Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). As datas semeadas já seguem os <c>CHECK</c> do banco (decisão e data da etapa juntas, RH e data juntos).
/// </summary>
internal static class FeriasDeTeste
{
    public const string Rota = "/api/eu/ferias";

    private static int _sequencia;

    public static async Task<ApiDeAcesso> ApiAsync(MySqlFixture mysql, string chave)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, chave);
        api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        return api;
    }

    public static async Task<PessoaDeFerias> PessoaAsync(ApiDeAcesso api, string nome, DateOnly admissao, int? setorId = null, Perfil perfil = Perfil.Funcionario, bool ativa = true)
    {
        var n = Interlocked.Increment(ref _sequencia);
        var rotulo = $"{nome} {n}";
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, rotulo, perfil, setorId, ativo: true, admissao);
        var usuario = await AdvertenciasDeTeste.ComLoginAsync(api, id);
        if (!ativa)
        {
            await AdvertenciasDeTeste.InativarAsync(api, id);
        }

        return new PessoaDeFerias(id, usuario, rotulo);
    }

    /// <summary>Um setor com gestor novo (perfil Gestor, com login, já no próprio setor), mais a linha de <c>GESTORES</c> dele.</summary>
    public static async Task<(int SetorId, PessoaDeFerias Gestor, int GestorId)> SetorComGestorAsync(ApiDeAcesso api, string nome)
    {
        var gestor = await PessoaAsync(api, $"Gestor {nome}", new DateOnly(2020, 1, 2), perfil: Perfil.Gestor);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, $"{nome} {Interlocked.Increment(ref _sequencia)}", gestor.FuncionarioId);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor} WHERE ID_FUNCIONARIO = {gestor.FuncionarioId}");
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, gestor.FuncionarioId);
        return (setor, gestor, gestorId);
    }

    /// <summary>
    /// Uma linha de férias semeada. <paramref name="gestorId"/> é <c>GESTORES.ID_GESTOR</c>; <paramref name="gestorAprovou"/> grava a etapa do gestor aprovada (um dia depois da solicitação) e <paramref name="rhId"/> a confirmação do RH (dois dias depois, com a data da análise).
    /// </summary>
    public static async Task<int> SemearAsync(
        ApiDeAcesso api,
        int pessoaId,
        string periodoInicio,
        string periodoFim,
        string inicio,
        string fim,
        string status,
        string solicitadaEm = "2026-10-07 09:20:00",
        int? gestorId = null,
        bool gestorAprovou = false,
        int? rhId = null,
        string? observacao = null,
        string? motivo = null,
        string? resultadoDoGestor = null,
        string? sugestaoInicio = null,
        string? sugestaoFim = null,
        string? respostaDaSugestao = null,
        bool automatica = false,
        int abono = 0)
    {
        var solicitada = DateTime.ParseExact(solicitadaEm, "yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);
        var resultado = resultadoDoGestor ?? (gestorAprovou ? "APROVOU" : null);
        DateTime? etapa = resultado is null ? null : solicitada.AddDays(1);
        DateTime? confirmacao = rhId is null ? null : solicitada.AddDays(2);
        DateTime? analise = status is "APROVADA" or "REJEITADA" or "EM_GOZO" or "CONCLUIDA" ? solicitada.AddDays(2) : null;
        DateTime? respostaEm = respostaDaSugestao is null ? null : solicitada.AddDays(2);

        await ApoioDeTeste.ExecutarAsync(
            api.Conexao,
            "INSERT INTO FERIAS (ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO, DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO, STATUS_FERIAS, DATA_SOLICITACAO, DATA_ANALISE, ID_GESTOR, OBSERVACAO, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ETAPA_GESTOR_AUTOMATICA, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH, SUGESTAO_DATA_INICIO, SUGESTAO_DATA_FIM, RESPOSTA_SUGESTAO, DATA_RESPOSTA_SUGESTAO, MOTIVO_RECUSA) VALUES (@pessoa, @pi, @pf, @i, @f, @abonoMarcado, @abono, @status, @solicitada, @analise, @gestor, @obs, @resultado, @etapa, @auto, @rh, @confirmacao, @si, @sf, @resposta, @respostaEm, @motivo)",
            ("@pessoa", pessoaId),
            ("@pi", periodoInicio),
            ("@pf", periodoFim),
            ("@i", inicio),
            ("@f", fim),
            ("@abonoMarcado", abono > 0),
            ("@abono", abono),
            ("@status", status),
            ("@solicitada", solicitada),
            ("@analise", analise),
            ("@gestor", gestorId),
            ("@obs", observacao),
            ("@resultado", resultado),
            ("@etapa", etapa),
            ("@auto", automatica),
            ("@rh", rhId),
            ("@confirmacao", confirmacao),
            ("@si", sugestaoInicio),
            ("@sf", sugestaoFim),
            ("@resposta", respostaDaSugestao),
            ("@respostaEm", respostaEm),
            ("@motivo", motivo));
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT MAX(ID_FERIAS) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoaId}");
    }

    /// <summary>O histórico do Bruno do contrato: três períodos inteiros tirados, 12 dias de 2025 a 2026 e, se <paramref name="com340"/>, o pedido de 15 dias esperando o gestor. Devolve o id do pedido 340 (ou 0).</summary>
    public static async Task<int> SemearBrunoAsync(ApiDeAcesso api, int pessoaId, int gestorId, int rhId, bool com340)
    {
        await SemearAsync(api, pessoaId, "2022-03-14", "2023-03-13", "2023-04-17", "2023-05-16", "CONCLUIDA", "2023-02-20 08:00:00", gestorId, true, rhId);
        await SemearAsync(api, pessoaId, "2023-03-14", "2024-03-13", "2024-04-15", "2024-05-14", "CONCLUIDA", "2024-02-19 08:00:00", gestorId, true, rhId);
        await SemearAsync(api, pessoaId, "2024-03-14", "2025-03-13", "2025-04-14", "2025-05-13", "CONCLUIDA", "2025-02-17 08:30:00", gestorId, true, rhId);
        await SemearAsync(api, pessoaId, "2025-03-14", "2026-03-13", "2026-04-13", "2026-04-24", "CONCLUIDA", "2026-02-20 10:10:00", gestorId, true, rhId);
        return com340
            ? await SemearAsync(api, pessoaId, "2025-03-14", "2026-03-13", "2026-11-16", "2026-11-30", "SOLICITADA", "2026-10-07 09:20:00", gestorId, observacao: "Queria emendar com o feriado de 20/11.")
            : 0;
    }

    public static long Contar(ApiDeAcesso api, string sql) => Sql.ContarAsync(api.Conexao, sql).GetAwaiter().GetResult();

    public static long LinhasDe(ApiDeAcesso api, int pessoaId) => Contar(api, $"SELECT COUNT(*) FROM FERIAS WHERE ID_FUNCIONARIO = {pessoaId}");

    public static long EmailsDe(ApiDeAcesso api, int pessoaId) => Contar(api, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE ID_FUNCIONARIO = {pessoaId}");

    public static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) =>
        (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    public static object Pedido(string inicio, string fim, bool abono = false, int diasAbono = 0, bool adiantar13 = false, string? mensagem = null) =>
        new { dataInicio = inicio, dataFim = fim, abonoPecuniario = abono, diasAbono, adiantar13Salario = adiantar13, mensagem };
}
