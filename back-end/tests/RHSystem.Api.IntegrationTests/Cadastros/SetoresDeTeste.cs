using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Gestores;
using RHSystem.Api.IntegrationTests.Infra;

namespace RHSystem.Api.IntegrationTests.Cadastros;

/// <summary>Um setor semeado por SQL: o gestor dele, as pessoas ativas (com <c>ID_GESTOR</c> do gestor) e uma pessoa inativa (idem, como o histórico de quem era o gestor dela).</summary>
internal sealed record SetorDeTeste(int SetorId, string Nome, GestorDeTeste Gestor, IReadOnlyList<int> Ativos, int Inativo);

/// <summary>O que os testes de setores repetiriam: semear um setor com equipe e ler do banco o que a troca de gestor mexe.</summary>
internal static class SetoresDeTeste
{
    /// <summary>
    /// Um setor com gestor (<c>Gestor</c> de nível <c>Funcionario Gestor</c>, linha em <c>GESTORES</c>), <paramref name="ativos"/> membros ativos e um inativo; todos com o <c>ID_GESTOR</c> do gestor,
    /// que ele mesmo não tem (o trigger do banco não deixa ninguém ser gestor de si mesmo).
    /// </summary>
    public static async Task<SetorDeTeste> ComEquipeAsync(ApiDeAcesso api, string rotulo, int ativos = 2)
    {
        var gestor = await GestoresDeTeste.GestorDeSetorAsync(api, rotulo);
        var membros = new List<int>();
        for (var i = 1; i <= ativos; i++)
        {
            membros.Add(await MembroAsync(api, $"{rotulo} Membro {i}", gestor, ativo: true));
        }

        var inativo = await MembroAsync(api, $"{rotulo} Inativo", gestor, ativo: false);
        return new SetorDeTeste(gestor.SetorId!.Value, gestor.SetorNome!, gestor, membros, inativo);
    }

    private static Task<int> MembroAsync(ApiDeAcesso api, string nome, GestorDeTeste gestor, bool ativo) =>
        SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, $"{Guid.NewGuid():N}@setores.com.br", new DateOnly(2026, 1, 2), ativo, "Funcionario", gestor.SetorId, gestorId: gestor.GestorId);

    public static async Task<int?> IdGestorDaPessoaAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}") is { } valor ? Convert.ToInt32(valor) : null;

    public static async Task<int?> SetorDaPessoaAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT ID_SETOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}") is { } valor ? Convert.ToInt32(valor) : null;

    public static async Task<int?> GestorDoSetorAsync(ApiDeAcesso api, int setorId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM SETORES WHERE ID_SETOR = {setorId}") is { } valor ? Convert.ToInt32(valor) : null;

    public static async Task<string?> NivelAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT n.NOME_NIVEL_ACESSO FROM FUNCIONARIOS f JOIN NIVEIS_DE_ACESSO n ON n.ID_NIVEL_ACESSO = f.ID_NIVEL_ACESSO WHERE f.ID_FUNCIONARIO = {funcionarioId}") as string;

    public static async Task<long> LinhasEmGestoresAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_FUNCIONARIO = {funcionarioId}");
}
