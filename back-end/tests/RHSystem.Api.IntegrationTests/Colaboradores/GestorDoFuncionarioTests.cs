using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>O gestor do colaborador, derivado do setor: quem resolve (cadastro e edição) e quem refaz os membros (a fatia 1c chama ao criar e editar setor).</summary>
[Collection(MySqlCollection.Nome)]
public class GestorDoFuncionarioTests(MySqlFixture mysql)
{
    private static readonly CancellationToken Ct = CancellationToken.None;

    private static int _contador;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(GestorDoFuncionarioTests));

    private static string Email() => $"gestor.{Interlocked.Increment(ref _contador)}@gestor.com.br";

    private static Task<T> ComOGestor<T>(ApiDeAcesso api, Func<IGestorDoFuncionario, Task<T>> acao) =>
        api.NoEscopoAsync(sp => acao(sp.GetRequiredService<IGestorDoFuncionario>()));

    /// <summary>Uma pessoa que é gestora (linha em <c>GESTORES</c>) e o <c>ID_GESTOR</c> dela.</summary>
    private static async Task<(int FuncionarioId, int GestorId)> NovoGestorAsync(ApiDeAcesso api, string nome, bool ativo = true)
    {
        var funcionario = await SementeDoCadastro.ColaboradorAsync(api.Conexao, nome, Email(), new DateOnly(2025, 1, 6), nivel: "Funcionario Gestor");
        await api.NovoGestorAsync(funcionario);
        var gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {funcionario}");
        if (!ativo)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {funcionario}");
        }

        return (funcionario, gestor);
    }

    private static async Task<int?> GestorDeAsync(ApiDeAcesso api, int funcionarioId) =>
        (await Sql.EscalarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}")) is { } valor ? Convert.ToInt32(valor, System.Globalization.CultureInfo.InvariantCulture) : null;

    [Fact]
    public async Task Resolver_devolve_o_gestor_do_setor_e_null_sem_gestor_e_null_quando_o_gestor_e_a_propria_pessoa_e_GestorInativo_true_quando_inativo()
    {
        var api = await ApiAsync();
        var (gestorFunc, gestor) = await NovoGestorAsync(api, "Gestor resolver");
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor resolver", gestor);
        var setorSemGestor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor resolver sem gestor");
        var (_, gestorInativo) = await NovoGestorAsync(api, "Gestor inativo resolver", ativo: false);
        var setorComInativo = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor resolver inativo", gestorInativo);

        var comGestor = await ComOGestor(api, g => g.ResolverAsync(setor, null, Ct));
        var semGestor = await ComOGestor(api, g => g.ResolverAsync(setorSemGestor, null, Ct));
        var oProprio = await ComOGestor(api, g => g.ResolverAsync(setor, gestorFunc, Ct)); // o gestor do setor não tem gestor
        var outraPessoa = await ComOGestor(api, g => g.ResolverAsync(setor, 987_654, Ct));
        var inativo = await ComOGestor(api, g => g.ResolverAsync(setorComInativo, null, Ct));
        var inexistente = await ComOGestor(api, g => g.ResolverAsync(987_654, null, Ct));

        Assert.Equal(new ResolucaoDoGestor(gestor, false, "Gestor resolver"), comGestor);
        Assert.Equal(new ResolucaoDoGestor(null, false, null), semGestor);
        Assert.Equal(new ResolucaoDoGestor(null, false, "Gestor resolver"), oProprio);
        Assert.Equal(new ResolucaoDoGestor(gestor, false, "Gestor resolver"), outraPessoa);
        Assert.Equal((gestorInativo, true), (inativo.GestorId, inativo.GestorInativo));
        Assert.Equal(new ResolucaoDoGestor(null, false, null), inexistente);
    }

    [Fact]
    public async Task RefazerDosMembros_atualiza_so_os_ativos_do_setor_o_gestor_novo_fica_com_null_e_inativos_nao_mudam()
    {
        var api = await ApiAsync();
        var (_, antigoGestor) = await NovoGestorAsync(api, "Gestor antigo refazer");
        var (novoFunc, novoGestor) = await NovoGestorAsync(api, "Gestor novo refazer");
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor refazer", antigoGestor);
        var membro1 = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Membro 1 refazer", Email(), new DateOnly(2026, 1, 5), setorId: setor, gestorId: antigoGestor);
        var membro2 = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Membro 2 refazer", Email(), new DateOnly(2026, 1, 5), setorId: setor, gestorId: antigoGestor);
        var inativo = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Inativo refazer", Email(), new DateOnly(2026, 1, 5), ativo: false, setorId: setor, gestorId: null);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_SETOR = {setor}, ID_GESTOR = {antigoGestor} WHERE ID_FUNCIONARIO = {novoFunc}"); // o novo gestor é do setor e ainda aponta para o antigo
        var deOutroSetor = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "De outro setor refazer", Email(), new DateOnly(2026, 1, 5), gestorId: antigoGestor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = {novoGestor} WHERE ID_SETOR = {setor}"); // o setor trocou de gestor

        var mudaram = await ComOGestor(api, g => g.RefazerDosMembrosAsync(setor, Ct));

        Assert.Equal(3, mudaram); // os dois membros ativos e o próprio gestor novo (que sai de baixo do antigo)
        Assert.Equal(novoGestor, await GestorDeAsync(api, membro1));
        Assert.Equal(novoGestor, await GestorDeAsync(api, membro2));
        Assert.Null(await GestorDeAsync(api, novoFunc)); // o gestor do setor não tem gestor
        Assert.Null(await GestorDeAsync(api, inativo)); // inativo não muda
        Assert.Equal(antigoGestor, await GestorDeAsync(api, deOutroSetor)); // outro setor não muda
        Assert.Equal(0, await ComOGestor(api, g => g.RefazerDosMembrosAsync(setor, Ct))); // refazer de novo não muda mais nada
    }

    [Fact]
    public async Task RefazerDosMembros_sem_gestor_no_setor_tira_o_gestor_dos_membros_e_com_gestor_inativo_devolve_422()
    {
        var api = await ApiAsync();
        var (_, gestor) = await NovoGestorAsync(api, "Gestor some refazer2");
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor refazer2", gestor);
        var membro = await SementeDoCadastro.ColaboradorAsync(api.Conexao, "Membro refazer2", Email(), new DateOnly(2026, 1, 5), setorId: setor, gestorId: gestor);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE SETORES SET ID_GESTOR = NULL WHERE ID_SETOR = {setor}"); // o setor ficou sem gestor

        var mudaram = await ComOGestor(api, g => g.RefazerDosMembrosAsync(setor, Ct));

        Assert.Equal(1, mudaram);
        Assert.Null(await GestorDeAsync(api, membro));

        var (_, inativo) = await NovoGestorAsync(api, "Gestor inativo refazer2", ativo: false);
        var setorComInativo = await SementeDoCadastro.SetorAsync(api.Conexao, "Setor refazer2 inativo", inativo);
        var erro = await Assert.ThrowsAsync<ErroDeNegocioException>(() => ComOGestor(api, g => g.RefazerDosMembrosAsync(setorComInativo, Ct)));
        Assert.Equal("GESTOR_DO_SETOR_INATIVO", erro.Codigo);
    }
}
