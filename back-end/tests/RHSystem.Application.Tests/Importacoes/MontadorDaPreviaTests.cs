using RHSystem.Application.Colaboradores;
using RHSystem.Application.Importacoes;

namespace RHSystem.Application.Tests.Importacoes;

public class MontadorDaPreviaTests
{
    private static readonly CatalogoDaImportacao Catalogo = new(
        Cargos: [new ItemDoCatalogo(1, "Estagiário"), new ItemDoCatalogo(2, "Analista de RH")],
        Setores: [new ItemDoCatalogo(10, "Tecnologia"), new ItemDoCatalogo(11, "Recursos Humanos")],
        Beneficios: [new ItemDoCatalogo(100, "Vale-transporte"), new ItemDoCatalogo(101, "Vale-refeição")]);

    private static ImportacaoLinhaValores Valida(string nome = "Tiago Nogueira") =>
        new(
            Nome: nome,
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

    private static ProblemaDeCadastro Campo(int indice, string campo, string mensagem) =>
        new(indice, TipoDeProblemaDeCadastro.Campo, campo, "CAMPO_INVALIDO", mensagem);

    private static async Task<IReadOnlyList<ImportacaoLinha>> MontarAsync(CadastroFalso cadastro, params (int Numero, ImportacaoLinhaValores Valores)[] linhas) =>
        await new MontadorDaPrevia(cadastro).MontarAsync(linhas, Catalogo, CancellationToken.None);

    [Fact]
    public async Task Linha_sem_problema_e_OK_com_erros_vazios()
    {
        var linhas = await MontarAsync(new CadastroFalso(), (2, Valida("Ana")), (3, Valida("Bia")));

        Assert.Equal([2, 3], linhas.Select(l => l.NumeroLinha));
        Assert.All(linhas, l =>
        {
            Assert.Equal(SituacaoDaLinha.Ok, l.Situacao);
            Assert.Empty(l.Erros);
        });
    }

    [Fact]
    public async Task Linha_com_problema_do_cadastro_e_COM_ERRO_e_as_outras_continuam_OK()
    {
        var cadastro = new CadastroFalso(Campo(1, "cpf", "CPF inválido."));

        var linhas = await MontarAsync(cadastro, (2, Valida("Ana")), (3, Valida("Bia")));

        Assert.Equal(SituacaoDaLinha.Ok, linhas[0].Situacao);
        Assert.Equal(SituacaoDaLinha.ComErro, linhas[1].Situacao);
        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "CPF inválido.")], linhas[1].Erros);
    }

    [Fact]
    public async Task Erro_de_conversao_e_erro_do_cadastro_aparecem_juntos_na_mesma_linha()
    {
        // O exemplo "comErros" do contrato, linha 5: e-mail repetido da linha 2 + cargo e setor que não existem.
        var linha5 = Valida("Elisa Prado") with { Cargo = "Analista de compras (novo)", Setor = "Marketing" };
        var cadastro = new CadastroFalso(
            new ProblemaDeCadastro(1, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Este e-mail corporativo se repete na linha 1.", OutroIndice: 0),
            Campo(1, "cargoId", "O cargo não existe."),
            Campo(1, "setorId", "O setor não existe."));

        var linhas = await MontarAsync(cadastro, (2, Valida()), (5, linha5));

        Assert.Equal(SituacaoDaLinha.ComErro, linhas[1].Situacao);
        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.EmailCorporativo, "Este e-mail aparece também na linha 2."),
                new ImportacaoLinhaErro(ColunaDaImportacao.Cargo, "O cargo \"Analista de compras (novo)\" não existe. Use um cargo da aba Valores válidos."),
                new ImportacaoLinhaErro(ColunaDaImportacao.Setor, "O setor \"Marketing\" não existe. Use um setor da aba Valores válidos."),
            ],
            linhas[1].Erros);
    }

    [Fact]
    public async Task Erro_do_cadastro_na_coluna_que_ja_falhou_na_conversao_nao_se_repete()
    {
        var cadastro = new CadastroFalso(Campo(0, "cargoId", "O cargo não existe."), Campo(0, "cpf", "CPF inválido."));

        var linhas = await MontarAsync(cadastro, (2, Valida() with { Cargo = "Inexistente" }));

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "CPF inválido."),
                new ImportacaoLinhaErro(ColunaDaImportacao.Cargo, "O cargo \"Inexistente\" não existe. Use um cargo da aba Valores válidos."),
            ],
            linhas[0].Erros);
    }

    [Fact]
    public async Task O_filtro_de_coluna_repetida_e_por_linha_e_nao_apaga_a_mesma_coluna_de_outra_linha()
    {
        // Só a linha 0 falhou na conversão do cargo; o problema de cargo da linha 1 é de outra linha e fica.
        var cadastro = new CadastroFalso(Campo(0, "cargoId", "O cargo não existe."), Campo(1, "cargoId", "O cargo não existe."));

        var linhas = await MontarAsync(cadastro, (2, Valida() with { Cargo = "Inexistente" }), (3, Valida()));

        Assert.Single(linhas[0].Erros);
        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.Cargo, "O cargo não existe.")], linhas[1].Erros);
    }

    [Fact]
    public async Task Email_ou_cpf_repetido_no_arquivo_diz_que_aparece_tambem_na_linha_N()
    {
        var cadastro = new CadastroFalso(
            new ProblemaDeCadastro(2, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Este CPF se repete na linha 1.", OutroIndice: 0),
            new ProblemaDeCadastro(2, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Este e-mail corporativo se repete na linha 2.", OutroIndice: 1));

        var linhas = await MontarAsync(cadastro, (2, Valida("Ana")), (4, Valida("Bia")), (9, Valida("Caio")));

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "Este CPF aparece também na linha 2."),
                new ImportacaoLinhaErro(ColunaDaImportacao.EmailCorporativo, "Este e-mail aparece também na linha 4."),
            ],
            linhas[2].Erros);
    }

    [Fact]
    public async Task Cadastro_existente_ativo_diz_que_ja_existe()
    {
        var existente = new CadastroExistente(7, "Maria Souza", Ativo: true);
        var cadastro = new CadastroFalso(
            new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Já existe um colaborador com este CPF.", existente),
            new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Já existe um colaborador com este e-mail corporativo.", existente));

        var linhas = await MontarAsync(cadastro, (2, Valida()));

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "Já existe um colaborador com este CPF."),
                new ImportacaoLinhaErro(ColunaDaImportacao.EmailCorporativo, "Já existe um colaborador com este e-mail."),
            ],
            linhas[0].Erros);
    }

    [Fact]
    public async Task Cadastro_existente_inativo_manda_usar_Readmitir_sem_dizer_o_nome_de_quem_e()
    {
        var existente = new CadastroExistente(7, "Maria Souza", Ativo: false);
        var cadastro = new CadastroFalso(
            new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Este CPF pertence a um cadastro inativo (Maria Souza). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.", existente),
            new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Este e-mail corporativo pertence a um cadastro inativo (Maria Souza). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.", existente));

        var linhas = await MontarAsync(cadastro, (2, Valida()));

        Assert.Equal(
            [
                new ImportacaoLinhaErro(ColunaDaImportacao.Cpf, "Este CPF pertence a um cadastro inativo. Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela."),
                new ImportacaoLinhaErro(ColunaDaImportacao.EmailCorporativo, "Este e-mail pertence a um cadastro inativo. Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela."),
            ],
            linhas[0].Erros);
    }

    [Fact]
    public async Task Problema_de_regra_sem_reescrita_sai_com_a_mensagem_do_cadastro_na_coluna_do_campo()
    {
        var cadastro = new CadastroFalso(new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Regra, "setorId", "GESTOR_DO_SETOR_INATIVO", "O gestor do setor escolhido está inativo."));

        var linhas = await MontarAsync(cadastro, (2, Valida()));

        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.Setor, "O gestor do setor escolhido está inativo.")], linhas[0].Erros);
    }

    [Fact]
    public async Task Os_indices_do_cadastro_voltam_para_o_numero_da_linha_do_Excel_com_linhas_em_branco_no_meio()
    {
        var cadastro = new CadastroFalso(Campo(2, "telefone", "Informe o telefone com DDD."));

        var linhas = await MontarAsync(cadastro, (2, Valida("Ana")), (3, Valida("Bia")), (7, Valida("Caio")));

        Assert.Equal([2, 3, 7], linhas.Select(l => l.NumeroLinha));
        Assert.Equal([SituacaoDaLinha.Ok, SituacaoDaLinha.Ok, SituacaoDaLinha.ComErro], linhas.Select(l => l.Situacao));
        Assert.Equal([new ImportacaoLinhaErro(ColunaDaImportacao.Telefone, "Informe o telefone com DDD.")], linhas[2].Erros);
    }

    [Fact]
    public async Task Valores_saem_como_digitados_para_a_resposta()
    {
        var digitados = Valida() with { Cpf = " 402.817.593-32 ", Telefone = "(11) 97766-3344", DataAdmissao = "5/3/2026", Beneficios = "vale-transporte;  VALE-REFEIÇÃO" };

        var linhas = await MontarAsync(new CadastroFalso(), (2, digitados));

        Assert.Equal(digitados, linhas[0].Valores);
    }

    [Fact]
    public async Task Chama_ValidarAsync_uma_vez_so_com_todas_as_linhas_na_ordem()
    {
        var cadastro = new CadastroFalso();

        await MontarAsync(cadastro, (2, Valida("Ana")), (3, Valida("Bia")), (4, Valida("Caio")));

        Assert.Equal(1, cadastro.ChamadasDeValidar);
        Assert.Equal(["Ana", "Bia", "Caio"], cadastro.UltimoLote.Select(n => n.Nome));
    }

    [Fact]
    public async Task O_lote_leva_os_valores_convertidos()
    {
        var cadastro = new CadastroFalso();

        await MontarAsync(cadastro, (2, Valida()));

        var novo = Assert.Single(cadastro.UltimoLote);
        Assert.Equal("40281759332", novo.Cpf);
        Assert.Equal(1, novo.CargoId);
        Assert.Equal(10, novo.SetorId);
        Assert.Equal([100], novo.BeneficioIds);
    }

    [Fact]
    public async Task Os_erros_de_cada_linha_saem_na_ordem_das_colunas_e_nao_na_ordem_em_que_foram_achados()
    {
        var cadastro = new CadastroFalso(
            Campo(0, "setorId", "O gestor do setor escolhido está inativo."),
            Campo(0, "emailCorporativo", "Informe um e-mail corporativo válido, com até 150 caracteres."),
            Campo(0, "cpf", "CPF inválido."));

        var linhas = await MontarAsync(cadastro, (2, Valida() with { Cargo = "Inexistente" }));

        Assert.Equal(
            [ColunaDaImportacao.Cpf, ColunaDaImportacao.EmailCorporativo, ColunaDaImportacao.Cargo, ColunaDaImportacao.Setor],
            linhas[0].Erros.Select(e => e.Coluna));
    }

    private sealed class CadastroFalso(params ProblemaDeCadastro[] problemas) : ICadastroDeFuncionarios
    {
        public int ChamadasDeValidar { get; private set; }

        public IReadOnlyList<NovoFuncionario> UltimoLote { get; private set; } = [];

        public Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(IReadOnlyList<NovoFuncionario> novos, CancellationToken ct)
        {
            ChamadasDeValidar++;
            UltimoLote = novos;
            return Task.FromResult<IReadOnlyList<ProblemaDeCadastro>>(problemas);
        }

        public Task<ResultadoDoCadastro> CadastrarAsync(IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro opcoes, CancellationToken ct) => throw new NotSupportedException();

        public Task<IReadOnlyList<ProblemaDeCadastro>> ValidarEdicaoAsync(int funcionarioId, NovoFuncionario dados, CancellationToken ct) => throw new NotSupportedException();
    }
}
