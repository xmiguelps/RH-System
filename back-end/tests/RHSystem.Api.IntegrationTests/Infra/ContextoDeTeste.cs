using Microsoft.EntityFrameworkCore;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Infra;

public static class ContextoDeTeste
{
    /// <summary>O mesmo <c>UseMySql</c> da API: provider Pomelo com a versão do servidor fixada (nada de AutoDetect, que abre conexão).</summary>
    public static AppDbContext Novo(string conexao) =>
        new(new DbContextOptionsBuilder<AppDbContext>()
            .UseMySql(conexao, new MySqlServerVersion(new Version(8, 4, 0)))
            .Options);
}
