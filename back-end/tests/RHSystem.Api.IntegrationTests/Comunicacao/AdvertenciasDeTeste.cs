using System.Globalization;
using System.Text.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Comunicacao;

/// <summary>
/// O que os testes de advertência têm em comum: um setor novo por teste (gestor, membros e a data do ocorrido ficam só dele, então as listas e os números não dependem dos outros testes) e a advertência semeada direto no banco (<c>ADVERTENCIAS</c> nunca
/// apaga linha: o trigger recusa o DELETE).
/// </summary>
internal sealed record SetorDeAdvertencia(int SetorId, string SetorNome, int GestorFuncionarioId, int GestorId, string GestorNome);

internal static class AdvertenciasDeTeste
{
    private static int _sequencia;

    public static string NomeUnico(string prefixo) => $"{prefixo} {Interlocked.Increment(ref _sequencia)}";

    /// <summary>Um setor com o seu gestor (pessoa ativa com perfil de gestor).</summary>
    public static async Task<SetorDeAdvertencia> NovoSetorAsync(ApiDeAcesso api, string prefixo = "Setor")
    {
        var nomeDoGestor = NomeUnico("Gestor Adv");
        var gestor = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nomeDoGestor, Perfil.Gestor);
        var nomeDoSetor = NomeUnico(prefixo);
        var setor = await ApoioDeTeste.CriarSetorAsync(api.Conexao, nomeDoSetor, gestor);
        var gestorId = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {gestor}");
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor} WHERE ID_FUNCIONARIO = {gestor}");
        return new SetorDeAdvertencia(setor, nomeDoSetor, gestor, gestorId, nomeDoGestor);
    }

    /// <summary>Um membro do setor (ativo por padrão), com o gestor do setor como <c>ID_GESTOR</c> (o "gestor do colaborador").</summary>
    public static async Task<(int Id, string Nome)> NovoMembroAsync(ApiDeAcesso api, SetorDeAdvertencia setor, bool ativo = true, string prefixo = "Membro Adv")
    {
        var nome = NomeUnico(prefixo);
        var id = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, setor.SetorId, ativo);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_GESTOR = {setor.GestorId} WHERE ID_FUNCIONARIO = {id}");
        return (id, nome);
    }

    /// <summary>
    /// Uma advertência direto no banco (datas LOCAIS). <paramref name="situacao"/> é o texto do banco; <c>CONTESTADA</c> exige o texto e a data da contestação (os <c>CHECK</c>s do banco). Devolve o id.
    /// </summary>
    public static async Task<int> SemearAsync(
        ApiDeAcesso api,
        int funcionarioId,
        string data,
        string tipo = "VERBAL",
        string motivo = "Atraso na abertura",
        string descricao = "Chegou depois do horário.",
        string situacao = "PENDENTE",
        int? gestorId = null,
        string? cienciaLocal = null,
        string? textoContestacao = null,
        string? contestacaoLocal = null)
    {
        static string Valor(string? v) => v is null ? "NULL" : $"'{v.Replace("'", "''", StringComparison.Ordinal)}'";
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO ADVERTENCIAS (ID_FUNCIONARIO, ID_GESTOR, DATA_ADVERTENCIA, TIPO_ADVERTENCIA, MOTIVO, DESCRICAO, STATUS_ADVERTENCIA, DATA_CIENCIA, TEXTO_CONTESTACAO, DATA_CONTESTACAO) VALUES ({funcionarioId}, {(gestorId is null ? "NULL" : gestorId.Value.ToString(CultureInfo.InvariantCulture))}, '{data}', '{tipo}', {Valor(motivo)}, {Valor(descricao)}, '{situacao}', {Valor(cienciaLocal)}, {Valor(textoContestacao)}, {Valor(contestacaoLocal)})");
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT MAX(ID_ADVERTENCIA) FROM ADVERTENCIAS WHERE ID_FUNCIONARIO = {funcionarioId}");
    }

    /// <summary>Desliga a pessoa (o trigger do banco inativa o login junto). O INSERT de advertência para inativo é recusado, então se semeia ANTES.</summary>
    public static async Task InativarAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionarioId}");

    /// <summary>Dá um login (usuário ativo) à pessoa criada por SQL e devolve o <c>ID_USUARIO</c>.</summary>
    public static async Task<int> ComLoginAsync(ApiDeAcesso api, int funcionarioId)
    {
        await Sql.ExecutarAsync(api.Conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA) VALUES ({funcionarioId}, '{new string('x', 60)}')");
        return (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionarioId}");
    }

    /// <summary>Um cliente HTTP da pessoa (com login criado na hora, se ainda não tem), com o token emitido AGORA no relógio falso.</summary>
    public static async Task<HttpClient> ClienteDeAsync(ApiDeAcesso api, int funcionarioId)
    {
        var existente = await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionarioId}");
        var usuario = existente > 0 ? (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_USUARIO FROM USUARIOS WHERE ID_FUNCIONARIO = {funcionarioId}") : await ComLoginAsync(api, funcionarioId);
        return await api.ClienteAgoraAsync(usuario);
    }

    public static async Task<string?> TextoAsync(ApiDeAcesso api, string sql) =>
        (await Sql.EscalarAsync(api.Conexao, sql)) is { } v and not DBNull ? Convert.ToString(v, CultureInfo.InvariantCulture) : null;

    public static List<int> Ids(JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(a => a.GetProperty("id").GetInt32())];

    public static long EmailsDa(ApiDeAcesso api, string tipo, int referencia) => Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = '{tipo}' AND REFERENCIA_ID = {referencia}").GetAwaiter().GetResult();
}
