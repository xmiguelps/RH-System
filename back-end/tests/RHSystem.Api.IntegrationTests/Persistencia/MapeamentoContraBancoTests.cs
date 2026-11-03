using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Confere o mapeamento do EF contra o banco REAL criado pelos scripts. Pega o desalinhamento no dia em que o grupo muda uma tabela,
/// e não quando a API quebra em produção.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoContraBancoTests(MySqlFixture mysql)
{
    private sealed record ColunaDoBanco(string Tabela, string Nome, bool Nulavel, bool TemPadrao, string Extra);

    private static async Task<List<ColunaDoBanco>> LerColunasAsync(string conexao)
    {
        var colunas = new List<ColunaDoBanco>();
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(
            "SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE = 'YES', COLUMN_DEFAULT IS NOT NULL, EXTRA FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()",
            c);
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            colunas.Add(new ColunaDoBanco(leitor.GetString(0), leitor.GetString(1), leitor.GetBoolean(2), leitor.GetBoolean(3), leitor.GetString(4)));
        }

        return colunas;
    }

    private static IEnumerable<(string Tabela, string Coluna, bool Nulavel)> ColunasMapeadas(AppDbContext db)
    {
        foreach (var tipo in db.Model.GetEntityTypes())
        {
            var tabela = tipo.GetTableName()!;
            var objeto = StoreObjectIdentifier.Table(tabela, tipo.GetSchema());
            foreach (var propriedade in tipo.GetProperties())
            {
                yield return (tabela, propriedade.GetColumnName(objeto)!, propriedade.IsNullable);
            }
        }
    }

    [Fact]
    public async Task Toda_tabela_e_coluna_mapeada_existe_no_banco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var noBanco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        var mapeadas = ColunasMapeadas(db).ToList();

        Assert.NotEmpty(mapeadas);
        foreach (var (tabela, coluna, _) in mapeadas)
        {
            Assert.True(
                noBanco.Exists(c => string.Equals(c.Tabela, tabela, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Nome, coluna, StringComparison.OrdinalIgnoreCase)),
                $"{tabela}.{coluna} está mapeada, mas não existe no banco.");
        }
    }

    [Fact]
    public async Task Nulabilidade_das_propriedades_mapeadas_coincide_com_a_das_colunas()
    {
        // ID_NIVEL_ACESSO, EMAIL_CORPORATIVO, ID_CARGO e ID_SETOR aceitam NULL no banco.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var noBanco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var (tabela, coluna, nulavel) in ColunasMapeadas(db))
        {
            var real = noBanco.Single(c => string.Equals(c.Tabela, tabela, StringComparison.OrdinalIgnoreCase) && string.Equals(c.Nome, coluna, StringComparison.OrdinalIgnoreCase));
            Assert.True(real.Nulavel == nulavel, $"{tabela}.{coluna}: a coluna {(real.Nulavel ? "aceita" : "não aceita")} NULL, mas a propriedade {(nulavel ? "aceita" : "não aceita")}.");
        }
    }

    [Fact]
    public async Task Toda_coluna_obrigatoria_sem_valor_padrao_das_tabelas_mapeadas_esta_mapeada()
    {
        // Pega o desalinhamento quando o grupo muda um script: uma coluna nova NOT NULL sem padrão derrubaria todo INSERT da API.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var noBanco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);
        var mapeadas = ColunasMapeadas(db).ToList();

        foreach (var tabela in mapeadas.Select(m => m.Tabela).Distinct(StringComparer.OrdinalIgnoreCase))
        {
            var obrigatorias = noBanco.Where(c =>
                string.Equals(c.Tabela, tabela, StringComparison.OrdinalIgnoreCase)
                && !c.Nulavel
                && !c.TemPadrao
                && !c.Extra.Contains("GENERATED", StringComparison.OrdinalIgnoreCase));

            foreach (var coluna in obrigatorias)
            {
                Assert.True(
                    mapeadas.Exists(m => string.Equals(m.Tabela, tabela, StringComparison.OrdinalIgnoreCase) && string.Equals(m.Coluna, coluna.Nome, StringComparison.OrdinalIgnoreCase)),
                    $"{tabela}.{coluna.Nome} é obrigatória e não tem padrão no banco, mas não está mapeada.");
            }
        }
    }
}
