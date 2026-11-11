using RHSystem.Application.Importacoes;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Tests.Importacoes;

public class ConversorDaPlanilhaTests
{
    private static readonly CatalogoDaImportacao Catalogo = new(
        Cargos: [new ItemDoCatalogo(1, "Estagiário"), new ItemDoCatalogo(2, "Analista de RH")],
        Setores: [new ItemDoCatalogo(10, "Tecnologia"), new ItemDoCatalogo(11, "Recursos Humanos")],
        Beneficios: [new ItemDoCatalogo(100, "Vale-transporte"), new ItemDoCatalogo(101, "Vale-refeição"), new ItemDoCatalogo(102, "Plano de saúde")]);

    private static ImportacaoLinhaValores Valida() =>
        new(
            Nome: "Tiago Nogueira",
            Cpf: "402.817.593-32",
            DataNascimento: "14/03/1989",
            Telefone: "(11) 97766-3344",
            EmailCorporativo: "tiago.nogueira@empresa.com.br",
            DataAdmissao: "19/10/2026",
            TipoContrato: "Estágio",
            Cargo: "Estagiário",
            Setor: "Tecnologia",
            NivelDeAcesso: "Colaborador",
            Beneficios: "Vale-transporte");

    private static ImportacaoLinhaValores Esvaziar(ImportacaoLinhaValores valores, ColunaDaImportacao coluna) =>
        coluna switch
        {
            ColunaDaImportacao.Nome => valores with { Nome = null },
            ColunaDaImportacao.Cpf => valores with { Cpf = null },
            ColunaDaImportacao.DataNascimento => valores with { DataNascimento = null },
            ColunaDaImportacao.Telefone => valores with { Telefone = null },
            ColunaDaImportacao.EmailCorporativo => valores with { EmailCorporativo = null },
            ColunaDaImportacao.DataAdmissao => valores with { DataAdmissao = null },
            ColunaDaImportacao.TipoContrato => valores with { TipoContrato = null },
            ColunaDaImportacao.Cargo => valores with { Cargo = null },
            ColunaDaImportacao.Setor => valores with { Setor = null },
            ColunaDaImportacao.NivelDeAcesso => valores with { NivelDeAcesso = null },
            ColunaDaImportacao.Beneficios => valores with { Beneficios = null },
            _ => throw new ArgumentOutOfRangeException(nameof(coluna)),
        };

    [Fact]
    public void Linha_valida_vira_NovoFuncionario_com_cpf_e_telefone_so_digitos_ids_e_perfil()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida(), Catalogo);

        Assert.Empty(conversao.Erros);
        var novo = conversao.Novo;
        Assert.Equal("Tiago Nogueira", novo.Nome);
        Assert.Equal("40281759332", novo.Cpf);
        Assert.Equal(new DateOnly(1989, 3, 14), novo.DataNascimento);
        Assert.Equal("11977663344", novo.Telefone);
        Assert.Equal("tiago.nogueira@empresa.com.br", novo.EmailCorporativo);
        Assert.Equal(new DateOnly(2026, 10, 19), novo.DataAdmissao);
        Assert.Equal(TipoDeContrato.Estagio, novo.TipoContrato);
        Assert.Equal(1, novo.CargoId);
        Assert.Equal(10, novo.SetorId);
        Assert.Equal(Perfil.Funcionario, novo.Perfil);
        Assert.Equal([100], novo.BeneficioIds);
    }

    [Fact]
    public void De_mapeia_cada_posicao_da_linha_lida_para_o_campo_da_coluna()
    {
        var celulas = ColunasDaImportacao.Todas.Select(c => $"valor-{(int)c}").ToList<string?>();

        var valores = ImportacaoLinhaValores.De(new LinhaLida(7, celulas));

        Assert.Equal(
            new ImportacaoLinhaValores("valor-0", "valor-1", "valor-2", "valor-3", "valor-4", "valor-5", "valor-6", "valor-7", "valor-8", "valor-9", "valor-10"),
            valores);
    }

    [Fact]
    public void De_mantem_a_celula_vazia_como_nula()
    {
        var celulas = ColunasDaImportacao.Todas.Select(_ => (string?)null).ToList();

        Assert.Equal(new ImportacaoLinhaValores(null, null, null, null, null, null, null, null, null, null, null), ImportacaoLinhaValores.De(new LinhaLida(2, celulas)));
    }

    [Theory]
    [InlineData("14/03/1989", 1989, 3, 14)]
    [InlineData("5/3/2026", 2026, 3, 5)]
    [InlineData("29/02/2028", 2028, 2, 29)]
    public void Data_aceita_dd_mm_aaaa(string texto, int ano, int mes, int dia)
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { DataNascimento = texto, DataAdmissao = texto }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal(new DateOnly(ano, mes, dia), conversao.Novo.DataNascimento);
        Assert.Equal(new DateOnly(ano, mes, dia), conversao.Novo.DataAdmissao);
    }

    [Theory]
    [InlineData("1989-03-14")]
    [InlineData("14-03-1989")]
    [InlineData("31/02/2026")]
    [InlineData("14/13/2026")]
    [InlineData("14/03/89")]
    [InlineData("ontem")]
    [InlineData("29/02/2027")]
    public void Data_em_outro_formato_ou_que_nao_existe_recusa_com_a_mensagem_do_contrato(string texto)
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { DataNascimento = texto, DataAdmissao = texto }, Catalogo);

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.DataNascimento, $"A data \"{texto}\" não é válida. Use dd/mm/aaaa."),
                new ImportacaoLinhaErro(ColunaDaImportacao.DataAdmissao, $"A data \"{texto}\" não é válida. Use dd/mm/aaaa."),
            ],
            conversao.Erros);
    }

    [Theory]
    [InlineData("estagio", TipoDeContrato.Estagio)]
    [InlineData("Estágio", TipoDeContrato.Estagio)]
    [InlineData("ESTÁGIO", TipoDeContrato.Estagio)]
    [InlineData("CLT", TipoDeContrato.Clt)]
    [InlineData(" clt ", TipoDeContrato.Clt)]
    public void Tipo_de_contrato_casa_sem_diferenciar_maiusculas_nem_acentos(string texto, TipoDeContrato esperado)
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { TipoContrato = texto }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal(esperado, conversao.Novo.TipoContrato);
    }

    [Theory]
    [InlineData("Colaborador", Perfil.Funcionario)]
    [InlineData("colaborador", Perfil.Funcionario)]
    [InlineData("Gestor", Perfil.Gestor)]
    [InlineData("gestor", Perfil.Gestor)]
    [InlineData("RH", Perfil.RH)]
    [InlineData(" rh ", Perfil.RH)]
    public void Nivel_de_acesso_casa_sem_diferenciar_maiusculas_e_vira_o_perfil(string texto, Perfil esperado)
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { NivelDeAcesso = texto }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal(esperado, conversao.Novo.Perfil);
    }

    [Fact]
    public void Cargo_setor_e_beneficio_casam_pelo_nome_sem_diferenciar_maiusculas_acentos_nem_espacos_nas_pontas()
    {
        var valores = Valida() with { Cargo = "  ANALISTA DE rh ", Setor = "recursos humanos", Beneficios = "VALE-REFEICAO" };

        var conversao = ConversorDaPlanilha.Converter(valores, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal(2, conversao.Novo.CargoId);
        Assert.Equal(11, conversao.Novo.SetorId);
        Assert.Equal([101], conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Cargo_inexistente_da_o_erro_do_contrato()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Cargo = "Analista de compras (novo)" }, Catalogo);

        Assert.Equal(
            [new ImportacaoLinhaErro(ColunaDaImportacao.Cargo, "O cargo \"Analista de compras (novo)\" não existe. Use um cargo da aba Valores válidos.")],
            conversao.Erros);
    }

    [Fact]
    public void Setor_inexistente_da_o_erro_do_contrato()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Setor = "Marketing" }, Catalogo);

        Assert.Equal(
            [new ImportacaoLinhaErro(ColunaDaImportacao.Setor, "O setor \"Marketing\" não existe. Use um setor da aba Valores válidos.")],
            conversao.Erros);
    }

    [Fact]
    public void Tipo_de_contrato_inexistente_da_o_erro_do_contrato()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { TipoContrato = "PJ" }, Catalogo);

        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.TipoContrato, "O tipo de contrato \"PJ\" não existe. Use CLT ou Estágio.")], conversao.Erros);
    }

    [Fact]
    public void Nivel_de_acesso_inexistente_da_o_erro_do_contrato()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { NivelDeAcesso = "Diretor" }, Catalogo);

        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.NivelDeAcesso, "O nível de acesso \"Diretor\" não existe. Use Colaborador, Gestor ou RH.")], conversao.Erros);
    }

    [Theory]
    [InlineData(ColunaDaImportacao.Nome)]
    [InlineData(ColunaDaImportacao.Cpf)]
    [InlineData(ColunaDaImportacao.DataNascimento)]
    [InlineData(ColunaDaImportacao.Telefone)]
    [InlineData(ColunaDaImportacao.EmailCorporativo)]
    [InlineData(ColunaDaImportacao.DataAdmissao)]
    [InlineData(ColunaDaImportacao.TipoContrato)]
    [InlineData(ColunaDaImportacao.Cargo)]
    [InlineData(ColunaDaImportacao.Setor)]
    [InlineData(ColunaDaImportacao.NivelDeAcesso)]
    public void Coluna_obrigatoria_vazia_da_Preencha_esta_coluna(ColunaDaImportacao coluna)
    {
        var conversao = ConversorDaPlanilha.Converter(Esvaziar(Valida(), coluna), Catalogo);

        Assert.Equal([new ImportacaoLinhaErro(coluna, "Preencha esta coluna.")], conversao.Erros);
    }

    [Theory]
    [InlineData(ColunaDaImportacao.Nome)]
    [InlineData(ColunaDaImportacao.DataAdmissao)]
    [InlineData(ColunaDaImportacao.Cargo)]
    public void Coluna_obrigatoria_so_com_espacos_tambem_da_Preencha_esta_coluna(ColunaDaImportacao coluna)
    {
        var valores = coluna switch
        {
            ColunaDaImportacao.Nome => Valida() with { Nome = "   " },
            ColunaDaImportacao.DataAdmissao => Valida() with { DataAdmissao = "   " },
            _ => Valida() with { Cargo = "   " },
        };

        var conversao = ConversorDaPlanilha.Converter(valores, Catalogo);

        Assert.Equal([new ImportacaoLinhaErro(coluna, "Preencha esta coluna.")], conversao.Erros);
    }

    [Fact]
    public void Beneficios_e_opcional_e_vazio_vira_lista_vazia_sem_erro()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = null }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Empty(conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Beneficios_separados_por_ponto_e_virgula_na_ordem_em_que_foram_escritos()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = "Plano de saúde; Vale-transporte" }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal([102, 100], conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Beneficio_repetido_conta_uma_vez_so()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = "Vale-transporte;vale-transporte; VALE-TRANSPORTE" }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal([100], conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Item_vazio_entre_os_pontos_e_virgulas_e_ignorado()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = ";Vale-transporte;; ;" }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal([100], conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Beneficio_inexistente_da_erro_e_nao_derruba_os_outros()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = "Vale-transporte; Seguro de vida; Plano de saúde" }, Catalogo);

        Assert.Equal(
            [new ImportacaoLinhaErro(ColunaDaImportacao.Beneficios, "O benefício \"Seguro de vida\" não existe. Use um benefício da aba Valores válidos.")],
            conversao.Erros);
        Assert.Equal([100, 102], conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Cada_beneficio_inexistente_diferente_da_o_seu_erro_e_o_repetido_da_um_so()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Beneficios = "Seguro de vida; Gympass; seguro de vida" }, Catalogo);

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Beneficios, "O benefício \"Seguro de vida\" não existe. Use um benefício da aba Valores válidos."),
                new ImportacaoLinhaErro(ColunaDaImportacao.Beneficios, "O benefício \"Gympass\" não existe. Use um benefício da aba Valores válidos."),
            ],
            conversao.Erros);
        Assert.Empty(conversao.Novo.BeneficioIds);
    }

    [Fact]
    public void Coluna_que_nao_converteu_vira_valor_sentinela_zero()
    {
        var valores = Valida() with
        {
            DataNascimento = "ontem",
            DataAdmissao = null,
            TipoContrato = "PJ",
            Cargo = "Inexistente",
            Setor = null,
            NivelDeAcesso = "Diretor",
        };

        var novo = ConversorDaPlanilha.Converter(valores, Catalogo).Novo;

        Assert.Equal(default, novo.DataNascimento);
        Assert.Equal(default, novo.DataAdmissao);
        Assert.Equal(default, novo.TipoContrato);
        Assert.Equal(0, novo.CargoId);
        Assert.Equal(0, novo.SetorId);
        Assert.Equal(default, novo.Perfil);
    }

    [Fact]
    public void Texto_vazio_vira_texto_vazio_no_NovoFuncionario()
    {
        var valores = Valida() with { Nome = null, Cpf = null, Telefone = null, EmailCorporativo = null };

        var novo = ConversorDaPlanilha.Converter(valores, Catalogo).Novo;

        Assert.Equal(string.Empty, novo.Nome);
        Assert.Equal(string.Empty, novo.Cpf);
        Assert.Equal(string.Empty, novo.Telefone);
        Assert.Equal(string.Empty, novo.EmailCorporativo);
    }

    [Fact]
    public void Cpf_so_com_letras_nao_e_erro_de_conversao_quem_recusa_e_o_cadastro()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { Cpf = "abc" }, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal(string.Empty, conversao.Novo.Cpf);
    }

    [Fact]
    public void Os_erros_saem_na_ordem_das_colunas()
    {
        var valores = Valida() with { Setor = "Marketing", Nome = null, TipoContrato = "PJ", Beneficios = "Gympass", DataNascimento = "ontem" };

        var colunas = ConversorDaPlanilha.Converter(valores, Catalogo).Erros.Select(e => e.Coluna).ToList();

        Assert.Equal(
            [ColunaDaImportacao.Nome, ColunaDaImportacao.DataNascimento, ColunaDaImportacao.TipoContrato, ColunaDaImportacao.Setor, ColunaDaImportacao.Beneficios],
            colunas);
    }

    [Fact]
    public void Todas_as_colunas_com_problema_dao_um_erro_cada_na_ordem_exata_das_colunas()
    {
        var valores = new ImportacaoLinhaValores(null, null, "x", null, null, "y", "PJ", "cargo-que-nao-existe", "setor-que-nao-existe", "Diretor", "beneficio-que-nao-existe");

        var colunas = ConversorDaPlanilha.Converter(valores, Catalogo).Erros.Select(e => e.Coluna).ToList();

        Assert.Equal(ColunasDaImportacao.Todas, colunas);
    }

    [Fact]
    public void Texto_com_espacos_nas_pontas_sai_aparado()
    {
        var valores = Valida() with { Nome = "  Tiago  ", EmailCorporativo = " tiago@empresa.com.br ", DataNascimento = " 14/03/1989 ", DataAdmissao = "\t19/10/2026\t", TipoContrato = " CLT " };

        var conversao = ConversorDaPlanilha.Converter(valores, Catalogo);

        Assert.Empty(conversao.Erros);
        Assert.Equal("Tiago", conversao.Novo.Nome);
        Assert.Equal("tiago@empresa.com.br", conversao.Novo.EmailCorporativo);
        Assert.Equal(new DateOnly(1989, 3, 14), conversao.Novo.DataNascimento);
        Assert.Equal(new DateOnly(2026, 10, 19), conversao.Novo.DataAdmissao);
    }

    [Fact]
    public void A_mensagem_de_data_invalida_traz_o_valor_aparado()
    {
        var conversao = ConversorDaPlanilha.Converter(Valida() with { DataNascimento = "  ontem  " }, Catalogo);

        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.DataNascimento, "A data \"ontem\" não é válida. Use dd/mm/aaaa.")], conversao.Erros);
    }

    [Theory]
    [InlineData("nome", ColunaDaImportacao.Nome)]
    [InlineData("cpf", ColunaDaImportacao.Cpf)]
    [InlineData("dataNascimento", ColunaDaImportacao.DataNascimento)]
    [InlineData("telefone", ColunaDaImportacao.Telefone)]
    [InlineData("emailCorporativo", ColunaDaImportacao.EmailCorporativo)]
    [InlineData("dataAdmissao", ColunaDaImportacao.DataAdmissao)]
    [InlineData("tipoContrato", ColunaDaImportacao.TipoContrato)]
    [InlineData("cargoId", ColunaDaImportacao.Cargo)]
    [InlineData("setorId", ColunaDaImportacao.Setor)]
    [InlineData("perfil", ColunaDaImportacao.NivelDeAcesso)]
    [InlineData("beneficioIds", ColunaDaImportacao.Beneficios)]
    public void ColunaDoCampo_mapeia_todos_os_campos_do_cadastro(string campo, ColunaDaImportacao esperada) =>
        Assert.Equal(esperada, ConversorDaPlanilha.ColunaDoCampo(campo));

    [Theory]
    [InlineData("endereco.cep")]
    [InlineData("Nome")]
    [InlineData("")]
    public void ColunaDoCampo_de_campo_desconhecido_lanca(string campo) =>
        Assert.Throws<InvalidOperationException>(() => ConversorDaPlanilha.ColunaDoCampo(campo));
}
