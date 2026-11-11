using Microsoft.AspNetCore.Mvc.Testing;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Api.IntegrationTests.Documentos;

/// <summary>Pessoas novas COM login e o perfil pedido, para os testes de "Meus documentos" (cada um cria a sua: a lista e a bolinha são da pessoa logada, e o banco é um por classe).</summary>
internal static class PessoasDeTeste
{
    /// <summary>Uma pessoa com senha definida e o nível do <paramref name="perfil"/> (a troca de nível é por SQL; o token que o teste pede depois já sai com o nível novo).</summary>
    public static async Task<PessoaDoTeste> ComLoginAsync(ApiDeAcesso api, string sufixo, Perfil perfil = Perfil.Funcionario)
    {
        var pessoa = await api.NovaPessoaAsync(sufixo, login: TipoDeLogin.SenhaDefinida);
        if (perfil != Perfil.Funcionario)
        {
            await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ID_NIVEL_ACESSO = (SELECT ID_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO WHERE NOME_NIVEL_ACESSO = '{NomesDeNivel.DoPerfil(perfil)}') WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}");
        }

        return pessoa;
    }

    /// <summary>Um cliente da pessoa, com token emitido AGORA (no relógio falso) e o nível que ela tem neste momento.</summary>
    public static Task<HttpClient> ClienteAsync(ApiDeAcesso api, PessoaDoTeste pessoa) => api.ClienteAgoraAsync(pessoa.UsuarioId!.Value);

    /// <summary>O mesmo token, falando com uma API derivada (a que troca o repositório, por exemplo).</summary>
    public static async Task<HttpClient> ClienteDaFabricaAsync(ApiDeAcesso api, WebApplicationFactory<Program> fabrica, PessoaDoTeste pessoa)
    {
        var cliente = fabrica.CreateClient();
        cliente.DefaultRequestHeaders.Authorization = (await ClienteAsync(api, pessoa)).DefaultRequestHeaders.Authorization;
        return cliente;
    }
}
