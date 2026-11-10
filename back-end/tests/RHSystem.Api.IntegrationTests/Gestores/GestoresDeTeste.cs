using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Gestores;

/// <summary>Um gestor semeado por SQL: o <c>ID_GESTOR</c>, a pessoa, o setor que ele gerencia (se gerencia) e as pessoas ativas desse setor.</summary>
internal sealed record GestorDeTeste(int GestorId, int FuncionarioId, string Nome, int? SetorId, string? SetorNome, IReadOnlyList<int> Membros);

/// <summary>As semeaduras que os testes de gestores repetiriam. O rótulo entra em todos os nomes, para um teste não enxergar os dados do outro (o banco é um por classe).</summary>
internal static class GestoresDeTeste
{
    /// <summary>
    /// Um gestor de setor: a pessoa com o nível <c>Gestor</c>, ativa, do setor que gerencia (<c>SETORES.ID_GESTOR</c>), com <paramref name="equipe"/> pessoas ativas no setor e, se pedido, um cargo.
    /// </summary>
    public static async Task<GestorDeTeste> GestorDeSetorAsync(ApiDeAcesso api, string rotulo, int equipe = 0, string? cargo = null)
    {
        var conexao = api.Conexao;
        var nome = $"{rotulo} Gestor";
        var setorNome = $"{rotulo} Setor";
        var setorId = await ApoioDeTeste.CriarSetorAsync(conexao, setorNome);
        var funcionarioId = await ApoioDeTeste.CriarPessoaAsync(conexao, nome, Perfil.Gestor, setorId);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(conexao, funcionarioId);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestorId} WHERE ID_SETOR = {setorId}");
        if (cargo is not null)
        {
            var cargoId = await ApoioDeTeste.CriarCargoAsync(conexao, cargo, null, 44m);
            await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_CARGO = {cargoId} WHERE ID_FUNCIONARIO = {funcionarioId}");
        }

        var membros = new List<int>();
        for (var i = 1; i <= equipe; i++)
        {
            membros.Add(await ApoioDeTeste.CriarPessoaAsync(conexao, $"{rotulo} Membro {i}", Perfil.Funcionario, setorId));
        }

        return new GestorDeTeste(gestorId, funcionarioId, nome, setorId, setorNome, membros);
    }

    /// <summary>A pessoa do RH cadastrada só para decidir pedidos de gestores: linha em <c>GESTORES</c>, sem setor gerenciado. <paramref name="setorDaPessoa"/> é o setor a que ELA pertence (não o que gerencia).</summary>
    public static async Task<GestorDeTeste> RhCadastradoAsync(ApiDeAcesso api, string rotulo, int? setorDaPessoa = null)
    {
        var nome = $"{rotulo} RH";
        var funcionarioId = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.RH, setorDaPessoa);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, funcionarioId);
        return new GestorDeTeste(gestorId, funcionarioId, nome, null, null, []);
    }

    /// <summary>Quem foi gestor e voltou ao nível <c>Funcionario</c>: mantém a linha em <c>GESTORES</c> (os pedidos antigos a referenciam), não gerencia setor nenhum e não é do RH.</summary>
    public static async Task<GestorDeTeste> ExGestorRebaixadoAsync(ApiDeAcesso api, string rotulo, int? setorDaPessoa = null)
    {
        var nome = $"{rotulo} Rebaixado";
        var funcionarioId = await ApoioDeTeste.CriarPessoaAsync(api.Conexao, nome, Perfil.Funcionario, setorDaPessoa);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(api.Conexao, funcionarioId);
        return new GestorDeTeste(gestorId, funcionarioId, nome, null, null, []);
    }

    /// <summary>Um gestor de setor que foi desligado depois: continua em <c>GESTORES</c> e continua sendo o <c>ID_GESTOR</c> do setor (o desligamento não mexe no setor).</summary>
    public static async Task<GestorDeTeste> GestorInativoAsync(ApiDeAcesso api, string rotulo, int equipe = 0)
    {
        var gestor = await GestorDeSetorAsync(api, rotulo, equipe);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {gestor.FuncionarioId}");
        return gestor;
    }
}
