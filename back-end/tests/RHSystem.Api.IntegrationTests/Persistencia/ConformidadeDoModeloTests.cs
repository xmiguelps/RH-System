using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Testes genéricos: percorrem o modelo do EF e o <c>INFORMATION_SCHEMA</c> do banco criado pelos scripts, e por isso valem para TODA
/// tabela que as fatias seguintes mapearem (nenhuma delas precisa repetir estas regras).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConformidadeDoModeloTests(MySqlFixture mysql)
{
    private sealed record ColunaDoBanco(string Tabela, string Nome, bool Nulavel, string? Padrao, string Extra)
    {
        /// <summary><c>STORED GENERATED</c> ou <c>VIRTUAL GENERATED</c>. (<c>DEFAULT_GENERATED</c> é outra coisa: um padrão que é expressão.)</summary>
        public bool EhCalculada =>
            Extra.Contains("STORED GENERATED", StringComparison.OrdinalIgnoreCase) || Extra.Contains("VIRTUAL GENERATED", StringComparison.OrdinalIgnoreCase);

        public bool PadraoEhExpressao => Extra.Contains("DEFAULT_GENERATED", StringComparison.OrdinalIgnoreCase);
    }

    private sealed record PropriedadeMapeada(string Tabela, string Coluna, IProperty Propriedade);

    private static bool Mesma(string a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);

    private static async Task<List<ColunaDoBanco>> LerColunasAsync(string conexao)
    {
        var colunas = new List<ColunaDoBanco>();
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(
            "SELECT TABLE_NAME, COLUMN_NAME, IS_NULLABLE = 'YES', COLUMN_DEFAULT, EXTRA FROM information_schema.COLUMNS WHERE TABLE_SCHEMA = DATABASE()",
            c);
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            colunas.Add(new ColunaDoBanco(
                leitor.GetString(0),
                leitor.GetString(1),
                leitor.GetBoolean(2),
                leitor.IsDBNull(3) ? null : leitor.GetString(3),
                leitor.GetString(4)));
        }

        return colunas;
    }

    private static async Task<List<string>> LerTabelasAsync(string conexao)
    {
        var tabelas = new List<string>();
        await using var c = new MySqlConnection(conexao);
        await c.OpenAsync();
        await using var comando = new MySqlCommand(
            "SELECT TABLE_NAME FROM information_schema.TABLES WHERE TABLE_SCHEMA = DATABASE() AND TABLE_TYPE = 'BASE TABLE'",
            c);
        await using var leitor = await comando.ExecuteReaderAsync();
        while (await leitor.ReadAsync())
        {
            tabelas.Add(leitor.GetString(0));
        }

        return tabelas;
    }

    private static List<PropriedadeMapeada> Mapeadas(AppDbContext db)
    {
        var lista = new List<PropriedadeMapeada>();
        foreach (var tipo in db.Model.GetEntityTypes())
        {
            var tabela = tipo.GetTableName()!;
            var objeto = StoreObjectIdentifier.Table(tabela, tipo.GetSchema());
            foreach (var propriedade in tipo.GetProperties())
            {
                lista.Add(new PropriedadeMapeada(tabela, propriedade.GetColumnName(objeto)!, propriedade));
            }
        }

        return lista;
    }

    private static ColunaDoBanco? ColunaDe(List<ColunaDoBanco> banco, PropriedadeMapeada mapeada) =>
        banco.Find(c => Mesma(c.Tabela, mapeada.Tabela) && Mesma(c.Nome, mapeada.Coluna));

    [Fact]
    public async Task Coluna_gerada_no_banco_esta_mapeada_como_calculada_e_fora_do_INSERT_e_do_UPDATE()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var banco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var mapeada in Mapeadas(db))
        {
            if (ColunaDe(banco, mapeada) is not { EhCalculada: true })
            {
                continue;
            }

            var p = mapeada.Propriedade;
            var nome = $"{mapeada.Tabela}.{mapeada.Coluna}";
            Assert.True(p.ValueGenerated == ValueGenerated.OnAddOrUpdate, $"{nome} é calculada no banco e deve ser ValueGenerated.OnAddOrUpdate (é {p.ValueGenerated}).");
            Assert.True(p.GetBeforeSaveBehavior() == PropertySaveBehavior.Ignore, $"{nome} é calculada no banco e não pode entrar no INSERT nem no UPDATE (BeforeSaveBehavior).");
            Assert.True(p.GetAfterSaveBehavior() == PropertySaveBehavior.Ignore, $"{nome} é calculada no banco e não pode entrar no INSERT nem no UPDATE (AfterSaveBehavior).");
        }
    }

    [Fact]
    public async Task Propriedade_calculada_no_modelo_corresponde_a_uma_coluna_gerada_no_banco()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var banco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var mapeada in Mapeadas(db).Where(m => m.Propriedade.ValueGenerated == ValueGenerated.OnAddOrUpdate))
        {
            Assert.True(
                ColunaDe(banco, mapeada) is { EhCalculada: true },
                $"{mapeada.Tabela}.{mapeada.Coluna} está mapeada como calculada, mas a coluna do banco não é gerada.");
        }
    }

    [Fact]
    public async Task Coluna_com_DEFAULT_de_expressao_NOT_NULL_esta_mapeada_como_gerada_na_inclusao()
    {
        // DEFAULT (UTC_TIMESTAMP(6)): sem ValueGenerated.OnAdd o EF mandaria 0001-01-01 quando o valor não fosse preenchido.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var banco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var mapeada in Mapeadas(db))
        {
            if (ColunaDe(banco, mapeada) is not { PadraoEhExpressao: true, Nulavel: false })
            {
                continue;
            }

            Assert.True(
                mapeada.Propriedade.ValueGenerated == ValueGenerated.OnAdd,
                $"{mapeada.Tabela}.{mapeada.Coluna} tem DEFAULT de expressão e NOT NULL: deve ser ValueGenerated.OnAdd (é {mapeada.Propriedade.ValueGenerated}).");
        }
    }

    [Fact]
    public async Task Coluna_com_DEFAULT_constante_nao_declara_o_padrao_e_o_EF_sempre_envia_o_valor()
    {
        // O padrão do banco que o EF omite é armadilha: com padrão TRUE no banco, um false que o EF considerasse "o valor do CLR" seria
        // omitido e o banco gravaria TRUE sem erro.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var banco = await LerColunasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var mapeada in Mapeadas(db))
        {
            if (ColunaDe(banco, mapeada) is not { Padrao: not null, PadraoEhExpressao: false, EhCalculada: false })
            {
                continue;
            }

            var p = mapeada.Propriedade;
            var nome = $"{mapeada.Tabela}.{mapeada.Coluna}";
            Assert.True(p.ValueGenerated == ValueGenerated.Never, $"{nome} tem DEFAULT constante: o EF deve sempre enviar o valor (ValueGenerated.Never, é {p.ValueGenerated}).");
            // Medido (sonda de 2026-11-03): sem declaração, GetDefaultValue() devolve o padrão do TIPO (False, 0, 0001-01-01), não null; o que
            // diz "declarado" é a presença da anotação.
            Assert.True(p.FindAnnotation(RelationalAnnotationNames.DefaultValue) is null, $"{nome}: não declare HasDefaultValue.");
            Assert.True(p.FindAnnotation(RelationalAnnotationNames.DefaultValueSql) is null, $"{nome}: não declare HasDefaultValueSql.");
        }
    }

    [Fact]
    public async Task Toda_propriedade_DateTime_mapeada_declara_o_fuso_com_ConversoresDeTempo_Utc_ou_Brasil()
    {
        // Nenhuma DateTime "crua": a decisão de fuso é obrigatória em cada coluna nova.
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);

        foreach (var mapeada in Mapeadas(db))
        {
            var tipo = Nullable.GetUnderlyingType(mapeada.Propriedade.ClrType) ?? mapeada.Propriedade.ClrType;
            if (tipo != typeof(DateTime))
            {
                continue;
            }

            var conversor = mapeada.Propriedade.GetValueConverter();
            Assert.True(
                ReferenceEquals(conversor, ConversoresDeTempo.Utc) || ReferenceEquals(conversor, ConversoresDeTempo.Brasil),
                $"{mapeada.Tabela}.{mapeada.Coluna} é DateTime sem fuso declarado: use HasConversion(ConversoresDeTempo.Utc) (tabela só da aplicação) ou .Brasil (tabela do grupo, em -03:00).");
        }
    }

    [Fact]
    public async Task Toda_tabela_do_banco_esta_mapeada_ou_listada_em_tabelas_sem_mapeamento_e_nenhuma_listada_esta_mapeada()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var noBanco = await LerTabelasAsync(conexao);
        await using var db = ContextoDeTeste.Novo(conexao);
        var mapeadas = db.Model.GetEntityTypes().Select(t => t.GetTableName()!).ToList();
        var listadas = TabelasSemMapeamento.Ler();

        var semDestino = noBanco
            .Where(t => !mapeadas.Contains(t, StringComparer.OrdinalIgnoreCase) && !listadas.Contains(t, StringComparer.OrdinalIgnoreCase))
            .Order()
            .ToList();
        var listadasEMapeadas = listadas.Where(t => mapeadas.Contains(t, StringComparer.OrdinalIgnoreCase)).Order().ToList();
        var listadasQueNaoExistem = listadas.Where(t => !noBanco.Contains(t, StringComparer.OrdinalIgnoreCase)).Order().ToList();

        Assert.True(
            semDestino.Count == 0,
            $"Tabelas do banco que não estão mapeadas nem listadas em {TabelasSemMapeamento.NomeDoArquivo} (o grupo criou tabela nova? mapeie ou liste): {string.Join(", ", semDestino)}");
        Assert.True(
            listadasEMapeadas.Count == 0,
            $"Tabelas listadas em {TabelasSemMapeamento.NomeDoArquivo} que já estão mapeadas (tire da lista): {string.Join(", ", listadasEMapeadas)}");
        Assert.True(
            listadasQueNaoExistem.Count == 0,
            $"Tabelas listadas em {TabelasSemMapeamento.NomeDoArquivo} que não existem no banco: {string.Join(", ", listadasQueNaoExistem)}");
    }

    [Fact]
    public void Leitor_de_tabelas_sem_mapeamento_aceita_comentario_depois_do_nome_e_ignora_linha_so_de_comentario_e_linha_vazia()
    {
        var lidas = TabelasSemMapeamento.Interpretar(
        [
            "# só comentário",
            string.Empty,
            "   ",
            "NIVEIS",
            "SEQUENCIAS  # só por SQL cru",
            "OUTRA#sem espaço antes",
            "  COM_ESPACOS  ",
        ]);

        // Sem isto o Step 4 gravaria uma "tabela" chamada "SEQUENCIAS  # só por SQL cru".
        Assert.Equal(["NIVEIS", "SEQUENCIAS", "OUTRA", "COM_ESPACOS"], lidas);
    }
}
