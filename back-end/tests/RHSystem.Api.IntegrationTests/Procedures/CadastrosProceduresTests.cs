using System.Globalization;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Infrastructure.Autenticacao;
using static RHSystem.Api.IntegrationTests.Procedures.ProceduresDeTeste;

namespace RHSystem.Api.IntegrationTests.Procedures;

/// <summary>
/// As procedures de cadastro do <c>V007</c> (seção E): <c>SP_CADASTRAR_CARGO</c>, <c>SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO</c>, <c>SP_CRIAR_USUARIO</c> e <c>SP_CADASTRAR_FUNCIONARIO</c>, mais a prova de que
/// <c>SP_CADASTRAR_NIVEL_ACESSO</c> não existe (a API não cria níveis). Cada uma faz as regras e os efeitos no banco do caso de uso equivalente da API (<c>criarCargo</c>, o perfil em <c>editarFuncionario</c>, o login de
/// <c>enviarConviteDeAcesso</c> e <c>criarFuncionario</c>).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class CadastrosProceduresTests(MySqlFixture mysql)
{
    private const string CpfDaAna = "52998224725"; // um CPF com os dígitos verificadores certos

    private static readonly TimeSpan EsperaPelaTrava = TimeSpan.FromMilliseconds(800);

    // ================================================================ SP_CADASTRAR_CARGO

    [Fact]
    public async Task Cargo_e_criado_com_o_nome_aparado_a_descricao_e_a_carga_semanal()
    {
        var conexao = await NovoBancoAsync(mysql);

        var saidas = await ChamarAsync(conexao, "SP_CADASTRAR_CARGO", "  Analista de compras \t", " Cotações e pedidos ", 44m, new Saida("id"));
        var semDescricao = await ChamarAsync(conexao, "SP_CADASTRAR_CARGO", "Estagiário", "   ", 30.5m, new Saida("id"));

        var id = Inteiro(saidas["id"]);
        Assert.Equal("Analista de compras|Cotações e pedidos|44.00", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', NOME_CARGO, DESCRICAO, CARGA_HORARIA) FROM CARGOS WHERE ID_CARGO = {id}"));
        Assert.Equal("Estagiário|<nulo>|30.50", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', NOME_CARGO, COALESCE(DESCRICAO, '<nulo>'), CARGA_HORARIA) FROM CARGOS WHERE ID_CARGO = {Inteiro(semDescricao["id"])}"));
        Assert.Equal(2L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS"));
    }

    [Fact]
    public async Task Cargo_aceita_os_limites_do_contrato_nome_de_100_descricao_de_255_e_carga_de_um_centesimo_a_168()
    {
        var conexao = await NovoBancoAsync(mysql);

        await ChamarAsync(conexao, "SP_CADASTRAR_CARGO", new string('a', 100), new string('d', 255), 168m, new Saida("id"));
        await ChamarAsync(conexao, "SP_CADASTRAR_CARGO", "Minimo", null, 0.01m, new Saida("id"));

        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS WHERE CHAR_LENGTH(NOME_CARGO) = 100 AND CHAR_LENGTH(DESCRICAO) = 255 AND CARGA_HORARIA = 168.00"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO = 'Minimo' AND DESCRICAO IS NULL AND CARGA_HORARIA = 0.01"));
    }

    [Fact]
    public async Task Cargo_recusa_nome_descricao_e_carga_fora_da_regra_e_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        const string carga = "Informe a carga semanal em horas: maior que zero, ate 168, com ate duas casas decimais.";
        (object? Nome, object? Descricao, object? Carga, string Mensagem)[] casos =
        [
            (null, null, 44m, "Informe o nome do cargo."),
            (string.Empty, null, 44m, "Informe o nome do cargo."),
            (" \t ", null, 44m, "Informe o nome do cargo."),
            (new string('a', 101), null, 44m, "O nome do cargo pode ter ate 100 caracteres."),
            ("Vendedor", new string('d', 256), 44m, "A descricao do cargo pode ter ate 255 caracteres."),
            ("Vendedor", null, null, carga), // a antiga aceitava cargo sem carga; o contrato não
            ("Vendedor", null, 0m, carga),
            ("Vendedor", null, -8m, carga),
            ("Vendedor", null, 168.01m, carga),
            ("Vendedor", null, 40.005m, carga), // a antiga arredondava em silêncio
        ];

        foreach (var caso in casos)
        {
            Assert.Equal(caso.Mensagem, await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_CARGO", caso.Nome, caso.Descricao, caso.Carga, new Saida("id")));
        }

        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS"));
    }

    [Fact]
    public async Task Cargo_com_nome_repetido_e_recusado_sem_diferenciar_maiusculas_nem_acentos()
    {
        var conexao = await NovoBancoAsync(mysql);
        await ChamarAsync(conexao, "SP_CADASTRAR_CARGO", "Técnico", null, 40m, new Saida("id"));

        var igual = await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_CARGO", "Técnico", "outra descrição", 44m, new Saida("id"));
        var semAcentoEmMaiusculas = await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_CARGO", "  TECNICO ", null, 44m, new Saida("id"));

        Assert.Equal("Ja existe um cargo com este nome.", igual);
        Assert.Equal("Ja existe um cargo com este nome.", semAcentoEmMaiusculas);
        Assert.Equal("Técnico|40.00", await TextoAsync(conexao, "SELECT GROUP_CONCAT(CONCAT_WS('|', NOME_CARGO, CARGA_HORARIA)) FROM CARGOS"));
    }

    [Fact]
    public async Task Cargo_pedido_varias_vezes_ao_mesmo_tempo_com_o_mesmo_nome_so_e_criado_uma_vez()
    {
        var conexao = await NovoBancoAsync(mysql);

        var respostas = await Task.WhenAll(Enumerable.Range(0, 6).Select(_ => TentarAsync(conexao, "SP_CADASTRAR_CARGO", "Operador", null, 44m, new Saida("id"))));

        Assert.Single(respostas, r => r is null);
        Assert.All(respostas.Where(r => r is not null), r => Assert.Equal("Ja existe um cargo com este nome.", r));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM CARGOS WHERE NOME_CARGO = 'Operador'"));
    }

    // ================================================================ SP_CADASTRAR_NIVEL_ACESSO (removida)

    [Fact]
    public async Task Nao_existe_procedure_para_criar_nivel_de_acesso_e_os_tres_niveis_do_V005_estao_la()
    {
        var conexao = await NovoBancoAsync(mysql);

        var procedures = await Sql.ColunaAsync(conexao, "SELECT ROUTINE_NAME FROM INFORMATION_SCHEMA.ROUTINES WHERE ROUTINE_SCHEMA = DATABASE() AND ROUTINE_TYPE = 'PROCEDURE' ORDER BY ROUTINE_NAME");

        Assert.DoesNotContain("SP_CADASTRAR_NIVEL_ACESSO", procedures);
        Assert.Contains("SP_CADASTRAR_CARGO", procedures);
        Assert.Contains("SP_CADASTRAR_FUNCIONARIO", procedures);
        Assert.Contains("SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", procedures);
        Assert.Contains("SP_CRIAR_USUARIO", procedures);
        Assert.Equal(["Funcionario", "Funcionario Gestor", "RH"], await Sql.ColunaAsync(conexao, "SELECT NOME_NIVEL_ACESSO FROM NIVEIS_DE_ACESSO ORDER BY NOME_NIVEL_ACESSO"));
    }

    // ================================================================ SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO

    [Fact]
    public async Task Nivel_e_trocado_e_promover_a_gestor_nao_cria_linha_em_GESTORES_nem_mexe_em_outra_pessoa()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var outra = await FuncionarioAsync(conexao);

        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, await NivelAsync(conexao, "Funcionario Gestor"));
        Assert.Equal("Funcionario Gestor", await NivelDeAsync(conexao, pessoa));

        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, await NivelAsync(conexao, "RH"));
        Assert.Equal("RH", await NivelDeAsync(conexao, pessoa));

        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, await NivelAsync(conexao, "Funcionario"));
        Assert.Equal("Funcionario", await NivelDeAsync(conexao, pessoa));

        Assert.Equal("Funcionario", await NivelDeAsync(conexao, outra));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM GESTORES"));
    }

    [Fact]
    public async Task Nivel_igual_ao_atual_nao_e_erro_e_nao_muda_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao, nivel: "RH");
        var rh = await NivelAsync(conexao, "RH");

        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, rh); // a antiga recusava: "nivel ja era o mesmo"
        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, rh);

        Assert.Equal("RH", await NivelDeAsync(conexao, pessoa));
    }

    [Fact]
    public async Task Nivel_de_quem_esta_inativo_ou_sem_nivel_tambem_pode_ser_trocado()
    {
        var conexao = await NovoBancoAsync(mysql);
        var inativo = await FuncionarioAsync(conexao, ativo: false);
        var semNivel = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ID_NIVEL_ACESSO = NULL WHERE ID_FUNCIONARIO = {semNivel}");

        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", inativo, await NivelAsync(conexao, "RH"));
        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", semNivel, await NivelAsync(conexao, "Funcionario"));

        Assert.Equal("RH", await NivelDeAsync(conexao, inativo));
        Assert.Equal("Funcionario", await NivelDeAsync(conexao, semNivel));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {inativo} AND ATIVO"));
    }

    [Fact]
    public async Task Nivel_recusa_o_que_falta_a_pessoa_e_o_nivel_que_nao_existem_e_o_nivel_que_a_API_nao_conhece()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var rh = await NivelAsync(conexao, "RH");
        await Sql.ExecutarAsync(conexao, "INSERT INTO NIVEIS_DE_ACESSO (NOME_NIVEL_ACESSO) VALUES ('Diretoria')");
        var diretoria = await NivelAsync(conexao, "Diretoria");

        Assert.Equal("Informe o funcionario e o nivel de acesso.", await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", null, rh));
        Assert.Equal("Informe o funcionario e o nivel de acesso.", await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, null));
        Assert.Equal("Funcionario nao encontrado.", await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", 987654, rh));
        Assert.Equal("O nivel de acesso nao existe.", await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, 987654));
        Assert.Equal("Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.", await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, diretoria));
        Assert.Equal("Funcionario", await NivelDeAsync(conexao, pessoa));
    }

    [Fact]
    public async Task Nivel_de_quem_gerencia_um_setor_nao_sai_de_gestor_ate_a_pessoa_deixar_o_setor()
    {
        var conexao = await NovoBancoAsync(mysql);
        const string recusa = "O funcionario gerencia um setor e precisa ter o nivel Funcionario Gestor. Tire-o do setor antes de mudar o nivel.";
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor");
        var gestor = await GestorAsync(conexao, chefe);
        var setor = await SetorAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {setor}");

        Assert.Equal(recusa, await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", chefe, await NivelAsync(conexao, "Funcionario")));
        Assert.Equal(recusa, await RecusaDeNegocioAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", chefe, await NivelAsync(conexao, "RH")));
        Assert.Equal("Funcionario Gestor", await NivelDeAsync(conexao, chefe));
        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", chefe, await NivelAsync(conexao, "Funcionario Gestor")); // continuar gestor não é recusado

        // Fora do setor (a linha em GESTORES fica, como na API), o nível pode mudar.
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = NULL WHERE ID_SETOR = {setor}");
        await ChamarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", chefe, await NivelAsync(conexao, "Funcionario"));

        Assert.Equal("Funcionario", await NivelDeAsync(conexao, chefe));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM GESTORES WHERE ID_FUNCIONARIO = {chefe}"));
    }

    [Fact]
    public async Task Nivel_espera_a_troca_de_gestor_de_setor_em_andamento_e_recusa_tirar_o_nivel_de_quem_acabou_de_virar_gestor()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var gestor = await GestorAsync(conexao, pessoa);
        var setor = await SetorAsync(conexao);
        var nivelGestor = await NivelAsync(conexao, "Funcionario Gestor");

        // A API está pondo a pessoa como gestora de um setor (promove a pessoa e grava o setor, na mesma transação) e ainda não confirmou.
        await using var outraSessao = new MySqlConnection(conexao);
        await outraSessao.OpenAsync();
        await ExecutarNaSessaoAsync(outraSessao, "START TRANSACTION");
        await ExecutarNaSessaoAsync(outraSessao, $"UPDATE FUNCIONARIOS SET ID_NIVEL_ACESSO = {nivelGestor} WHERE ID_FUNCIONARIO = {pessoa}");
        await ExecutarNaSessaoAsync(outraSessao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {setor}");

        var chamada = TentarAsync(conexao, "SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO", pessoa, await NivelAsync(conexao, "RH"));
        await Task.Delay(EsperaPelaTrava);
        var terminouComALinhaTravada = chamada.IsCompleted;
        await ExecutarNaSessaoAsync(outraSessao, "COMMIT");
        var mensagem = await chamada;

        // Sem a trava na linha da pessoa, a procedure conferiria o setor ANTES de a troca aparecer e deixaria um gestor de setor com o nível RH.
        Assert.False(terminouComALinhaTravada);
        Assert.Equal("O funcionario gerencia um setor e precisa ter o nivel Funcionario Gestor. Tire-o do setor antes de mudar o nivel.", mensagem);
        Assert.Equal("Funcionario Gestor", await NivelDeAsync(conexao, pessoa));
    }

    // ================================================================ SP_CRIAR_USUARIO

    [Fact]
    public async Task Usuario_nasce_provisorio_ativo_e_com_uma_senha_que_nao_confere_com_nenhuma()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ana = await FuncionarioAsync(conexao);
        var bruno = await FuncionarioAsync(conexao);

        var idDaAna = Inteiro((await ChamarAsync(conexao, "SP_CRIAR_USUARIO", ana, new Saida("id")))["id"]);
        var idDoBruno = Inteiro((await ChamarAsync(conexao, "SP_CRIAR_USUARIO", bruno, new Saida("id")))["id"]);

        Assert.Equal($"{ana}|1|1|<nulo>", await TextoAsync(conexao, $"SELECT CONCAT_WS('|', ID_FUNCIONARIO, ATIVO, SENHA_PROVISORIA, COALESCE(DATA_HORA_ULTIMO_LOGIN, '<nulo>')) FROM USUARIOS WHERE ID_USUARIO = {idDaAna}"));
        var senhaDaAna = await TextoAsync(conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {idDaAna}");
        var senhaDoBruno = await TextoAsync(conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {idDoBruno}");

        // O formato do HashProvisorio da API: Base64 de 48 bytes (64 caracteres), o primeiro 0xFF; o SenhaHasher da API nunca o aceita, e nem cai.
        Assert.Equal(64, senhaDaAna.Length);
        var bytes = Convert.FromBase64String(senhaDaAna);
        Assert.Equal(48, bytes.Length);
        Assert.Equal(0xFF, bytes[0]);
        Assert.NotEqual(senhaDaAna, senhaDoBruno);
        var hasher = new SenhaHasher();
        Assert.False(hasher.Verificar(senhaDaAna, senhaDaAna));
        Assert.False(hasher.Verificar(senhaDaAna, string.Empty));
        Assert.False(hasher.Verificar(senhaDaAna, "Senha@123"));

        Assert.Equal(2L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA")); // o link do convite é da API
    }

    [Fact]
    public async Task Usuario_provisorio_que_ja_existe_e_devolvido_sem_criar_outro_nem_trocar_a_senha()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);
        var primeiro = Inteiro((await ChamarAsync(conexao, "SP_CRIAR_USUARIO", pessoa, new Saida("id")))["id"]);
        var senha = await TextoAsync(conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {primeiro}");

        var segundo = Inteiro((await ChamarAsync(conexao, "SP_CRIAR_USUARIO", pessoa, new Saida("id")))["id"]);

        Assert.Equal(primeiro, segundo);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
        Assert.Equal(senha, await TextoAsync(conexao, $"SELECT SENHA FROM USUARIOS WHERE ID_USUARIO = {primeiro}"));
    }

    [Fact]
    public async Task Usuario_recusa_pessoa_que_falta_que_nao_existe_inativa_e_sem_e_mail_corporativo_nessa_ordem()
    {
        var conexao = await NovoBancoAsync(mysql);
        var inativa = await FuncionarioAsync(conexao, ativo: false);
        var inativaSemEmail = await FuncionarioAsync(conexao, ativo: false);
        var semEmail = await FuncionarioAsync(conexao);
        var emailEmBranco = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = NULL WHERE ID_FUNCIONARIO IN ({inativaSemEmail}, {semEmail})");
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET EMAIL_CORPORATIVO = '  ' WHERE ID_FUNCIONARIO = {emailEmBranco}");

        Assert.Equal("Informe o funcionario.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", null, new Saida("id")));
        Assert.Equal("Funcionario nao encontrado.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", 987654, new Saida("id")));
        Assert.Equal("Nao e possivel criar usuario para funcionario inativo.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", inativa, new Saida("id")));
        Assert.Equal("Nao e possivel criar usuario para funcionario inativo.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", inativaSemEmail, new Saida("id")));
        Assert.Equal("O funcionario precisa ter e-mail corporativo para criar o usuario.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", semEmail, new Saida("id")));
        Assert.Equal("O funcionario precisa ter e-mail corporativo para criar o usuario.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", emailEmBranco, new Saida("id")));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
    }

    [Fact]
    public async Task Usuario_recusa_quem_ja_definiu_a_senha_e_quem_tem_o_login_desativado_sem_tocar_no_login()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hash = new string('h', 60);
        var comSenha = await FuncionarioAsync(conexao);
        var comSenhaDesativado = await FuncionarioAsync(conexao);
        var provisorioDesativado = await FuncionarioAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA, ATIVO, SENHA_PROVISORIA) VALUES ({comSenha}, '{hash}', TRUE, FALSE), ({comSenhaDesativado}, '{hash}', FALSE, FALSE), ({provisorioDesativado}, '{hash}', FALSE, TRUE)");

        Assert.Equal("O funcionario ja tem usuario com a senha definida.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", comSenha, new Saida("id")));
        Assert.Equal("O funcionario ja tem usuario com a senha definida.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", comSenhaDesativado, new Saida("id")));
        Assert.Equal("O usuario do funcionario esta desativado. Ative o usuario em vez de criar outro.", await RecusaDeNegocioAsync(conexao, "SP_CRIAR_USUARIO", provisorioDesativado, new Saida("id")));
        Assert.Equal(3L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE SENHA = '{hash}'"));
        Assert.Equal("1|0,0|0,0|1", await TextoAsync(conexao, "SELECT GROUP_CONCAT(CONCAT_WS('|', ATIVO, SENHA_PROVISORIA) ORDER BY ID_USUARIO) FROM USUARIOS"));
    }

    [Fact]
    public async Task Usuario_pedido_varias_vezes_ao_mesmo_tempo_para_a_mesma_pessoa_e_um_so()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        var ids = await Task.WhenAll(Enumerable.Range(0, 6).Select(async _ => Inteiro((await ChamarAsync(conexao, "SP_CRIAR_USUARIO", pessoa, new Saida("id")))["id"])));

        Assert.Single(ids.Distinct());
        Assert.Equal(1L, await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ID_FUNCIONARIO = {pessoa}"));
    }

    [Fact]
    public async Task Usuarios_de_pessoas_diferentes_pedidos_ao_mesmo_tempo_sao_todos_criados_sem_uma_chamada_derrubar_a_outra()
    {
        var conexao = await NovoBancoAsync(mysql);

        // Pessoas sem login, com ids maiores que os de todo login existente: uma leitura de USUARIOS com trava (FOR UPDATE) que não acha linha travaria o MESMO intervalo do índice para todas,
        // e os INSERT esperariam um pelo outro (erro 1213, deadlock). Várias rodadas, com as conexões já abertas, para as chamadas partirem juntas.
        for (var rodada = 0; rodada < 6; rodada++)
        {
            var pessoas = new List<int>();
            for (var i = 0; i < 8; i++)
            {
                pessoas.Add(await FuncionarioAsync(conexao));
            }

            var sessoes = new List<MySqlConnection>();
            try
            {
                foreach (var _ in pessoas)
                {
                    var sessao = new MySqlConnection(conexao);
                    await sessao.OpenAsync();
                    sessoes.Add(sessao);
                }

                var ids = await Task.WhenAll(pessoas.Select((pessoa, i) => Task.Run(async () => Inteiro((await ChamarNaConexaoAsync(sessoes[i], "SP_CRIAR_USUARIO", pessoa, new Saida("id")))["id"]))));

                Assert.Equal(pessoas.Count, ids.Distinct().Count());
            }
            finally
            {
                foreach (var sessao in sessoes)
                {
                    await sessao.DisposeAsync();
                }
            }

            Assert.Equal(pessoas.Count, (int)await Sql.ContarAsync(conexao, $"SELECT COUNT(*) FROM USUARIOS WHERE ATIVO AND SENHA_PROVISORIA AND ID_FUNCIONARIO IN ({string.Join(", ", pessoas)})"));
        }

        Assert.Equal(48L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
    }

    [Fact]
    public async Task Usuario_espera_o_desligamento_em_andamento_e_nao_cria_login_para_quem_acabou_de_ficar_inativo()
    {
        var conexao = await NovoBancoAsync(mysql);
        var pessoa = await FuncionarioAsync(conexao);

        // Outro programa está inativando a pessoa e ainda não confirmou.
        await using var outraSessao = new MySqlConnection(conexao);
        await outraSessao.OpenAsync();
        await ExecutarNaSessaoAsync(outraSessao, "START TRANSACTION");
        await ExecutarNaSessaoAsync(outraSessao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {pessoa}");

        var chamada = TentarAsync(conexao, "SP_CRIAR_USUARIO", pessoa, new Saida("id"));
        await Task.Delay(EsperaPelaTrava);
        var terminouComALinhaTravada = chamada.IsCompleted;
        await ExecutarNaSessaoAsync(outraSessao, "COMMIT");
        var mensagem = await chamada;

        // Sem a trava na linha da pessoa, a procedure (e o trigger, na mesma leitura) ainda a veria ativa e criaria o login de quem já saiu.
        Assert.False(terminouComALinhaTravada);
        Assert.Equal("Nao e possivel criar usuario para funcionario inativo.", mensagem);
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM USUARIOS"));
    }

    // ================================================================ SP_CADASTRAR_FUNCIONARIO

    [Fact]
    public async Task Funcionario_e_cadastrado_com_a_matricula_gerada_os_textos_normalizados_e_so_o_que_a_API_tambem_grava()
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao) with
        {
            Nome = "  Ana Souza \t",
            Email = "  Ana.Souza@Empresa.com.br ",
            Cpf = "529.982.247-25",
            Telefone = "(11) 98765-4410",
            Contrato = "CLT",
        };

        var saidas = await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos());

        var id = Inteiro(saidas["id"]);
        Assert.Equal("00001", saidas["matricula"]);
        Assert.Equal(
            $"Ana Souza|1990-06-18|2025-03-10|<nulo>|1|ana.souza@empresa.com.br|{cadastro.Cargo}|{cadastro.Setor}|<nulo>|{cadastro.Nivel}|00001|CLT",
            await TextoAsync(conexao, $"SELECT CONCAT_WS('|', NOME, DATA_NASCIMENTO, DATA_ADMISSAO, COALESCE(DATA_DESLIGAMENTO, '<nulo>'), ATIVO, EMAIL_CORPORATIVO, ID_CARGO, ID_SETOR, COALESCE(ID_GESTOR, '<nulo>'), ID_NIVEL_ACESSO, MATRICULA, TIPO_CONTRATO) FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {id}"));
        Assert.Equal(
            "52998224725|11987654410|-|-|-|-|-|-|-|-|-|-",
            await TextoAsync(conexao, $"SELECT CONCAT_WS('|', CPF, TELEFONE, COALESCE(RG, '-'), COALESCE(RG_ORGAO_EMISSOR, '-'), COALESCE(EMAIL_PESSOAL, '-'), COALESCE(CEP, '-'), COALESCE(LOGRADOURO, '-'), COALESCE(NUMERO, '-'), COALESCE(COMPLEMENTO, '-'), COALESCE(BAIRRO, '-'), COALESCE(CIDADE, '-'), COALESCE(UF, '-')) FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {id}"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES"));

        // O login, os benefícios e a linha de gestor são de outras operações.
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT (SELECT COUNT(*) FROM USUARIOS) + (SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS) + (SELECT COUNT(*) FROM GESTORES)"));
    }

    [Fact]
    public async Task Funcionario_guarda_os_dados_pessoais_opcionais_aparados_a_UF_em_maiusculas_e_o_contrato_no_texto_da_API()
    {
        var conexao = await NovoBancoAsync(mysql);
        var hoje = DateOnly.FromDateTime((DateTime)(await Sql.EscalarAsync(conexao, "SELECT CURDATE()"))!);
        var cadastro = await CadastroPadraoAsync(conexao) with
        {
            Nome = new string('n', 150),
            Email = $"{new string('e', 138)}@empresa.com", // 150 caracteres
            Nascimento = hoje, // hoje não é futuro
            Admissao = hoje.AddDays(40), // em admissão: a data pode ser futura
            Contrato = " estagio ",
            Nivel = await NivelAsync(conexao, "Funcionario Gestor"),
            Telefone = "1133447783",
            Rg = " 45.678.912-3 ",
            Orgao = "SSP-SP",
            EmailPessoal = " Ana@Email.com ",
            Cep = "01310100",
            Logradouro = " Avenida Paulista ",
            Numero = "1578",
            Complemento = "apto 42",
            Bairro = "Bela Vista",
            Cidade = "São Paulo",
            Uf = "sp",
        };

        var id = Inteiro((await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos()))["id"]);

        Assert.Equal(
            "1133447783|45.678.912-3|SSP-SP|Ana@Email.com|01310100|Avenida Paulista|1578|apto 42|Bela Vista|São Paulo|SP",
            await TextoAsync(conexao, $"SELECT CONCAT_WS('|', TELEFONE, RG, RG_ORGAO_EMISSOR, EMAIL_PESSOAL, CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, UF) FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {id}"));
        Assert.Equal(
            $"150|150|{hoje:yyyy-MM-dd}|{hoje.AddDays(40):yyyy-MM-dd}|ESTAGIO|Funcionario Gestor|1",
            await TextoAsync(conexao, $"SELECT CONCAT_WS('|', CHAR_LENGTH(F.NOME), CHAR_LENGTH(F.EMAIL_CORPORATIVO), F.DATA_NASCIMENTO, F.DATA_ADMISSAO, F.TIPO_CONTRATO, N.NOME_NIVEL_ACESSO, F.ATIVO) FROM FUNCIONARIOS F JOIN NIVEIS_DE_ACESSO N ON N.ID_NIVEL_ACESSO = F.ID_NIVEL_ACESSO WHERE F.ID_FUNCIONARIO = {id}"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM GESTORES")); // o nível Gestor não cria a linha de gestor (a API também não)
    }

    [Fact]
    public async Task Funcionario_recebe_o_gestor_do_setor_escolhido()
    {
        var conexao = await NovoBancoAsync(mysql);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor");
        var gestor = await GestorAsync(conexao, chefe);
        var cadastro = await CadastroPadraoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {cadastro.Setor}");

        var id = Inteiro((await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos()))["id"]);

        Assert.Equal(gestor, (int)await Sql.ContarAsync(conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {id}"));
    }

    [Fact]
    public async Task Funcionario_nao_e_cadastrado_em_setor_cujo_gestor_esta_inativo_e_a_matricula_volta()
    {
        var conexao = await NovoBancoAsync(mysql);
        var chefe = await FuncionarioAsync(conexao, nivel: "Funcionario Gestor");
        var gestor = await GestorAsync(conexao, chefe);
        var cadastro = await CadastroPadraoAsync(conexao);
        await Sql.ExecutarAsync(conexao, $"UPDATE SETORES SET ID_GESTOR = {gestor} WHERE ID_SETOR = {cadastro.Setor}");
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {chefe}");
        var pessoasAntes = await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS");

        var mensagem = await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos());

        Assert.Equal("O gestor do setor escolhido esta inativo. Troque o gestor do setor antes de continuar.", mensagem);
        await NadaFoiGravadoAsync(conexao, pessoasAntes, matriculas: 0);
    }

    [Fact]
    public async Task Funcionario_recusa_cada_entrada_fora_da_regra_na_ordem_dos_campos_e_nao_grava_nada()
    {
        var conexao = await NovoBancoAsync(mysql);
        var certo = await CadastroPadraoAsync(conexao);
        var amanha = DateOnly.FromDateTime((DateTime)(await Sql.EscalarAsync(conexao, "SELECT CURDATE() + INTERVAL 1 DAY"))!);
        const string telefone = "Informe o telefone com DDD (10 ou 11 digitos).";
        const string email = "Informe um e-mail corporativo valido, com ate 150 caracteres.";
        const string emailPessoal = "Informe um e-mail pessoal valido, com ate 150 caracteres.";
        const string contrato = "Informe o tipo de contrato: CLT ou ESTAGIO.";
        const string cep = "O CEP tem 8 digitos, sem pontuacao.";
        const string uf = "Informe a sigla do estado (por exemplo SP).";
        (Cadastro Cadastro, string Mensagem)[] casos =
        [
            (certo with { Nome = null }, "Informe o nome do funcionario."),
            (certo with { Nome = " \t " }, "Informe o nome do funcionario."),
            (certo with { Nome = new string('n', 151) }, "O nome pode ter ate 150 caracteres."),
            (certo with { Cpf = null }, "CPF invalido."),
            (certo with { Cpf = "5299822472" }, "CPF invalido."), // 10 dígitos
            (certo with { Cpf = "529982247250" }, "CPF invalido."), // 12 dígitos
            (certo with { Cpf = "11111111111" }, "CPF invalido."), // todos iguais
            (certo with { Cpf = "52998224724" }, "CPF invalido."), // segundo dígito verificador errado (a antiga só conferia o tamanho)
            (certo with { Cpf = "52998224735" }, "CPF invalido."), // primeiro dígito verificador errado
            (certo with { Nascimento = null }, "Informe a data de nascimento."),
            (certo with { Nascimento = amanha }, "A data de nascimento nao pode ser no futuro."),
            (certo with { Telefone = null }, telefone),
            (certo with { Telefone = "987654410" }, telefone), // sem DDD
            (certo with { Telefone = "5511987654410" }, telefone), // com o código do país
            (certo with { Email = null }, "Informe o e-mail corporativo."),
            (certo with { Email = "   " }, "Informe o e-mail corporativo."),
            (certo with { Email = "ana.empresa.com.br" }, email),
            (certo with { Email = "ana@empresa" }, email),
            (certo with { Email = "ana souza@empresa.com.br" }, email),
            (certo with { Email = "ana@@empresa.com.br" }, email),
            (certo with { Email = $"{new string('e', 139)}@empresa.com" }, email), // 151 caracteres
            (certo with { Admissao = null }, "Informe a data de admissao."),
            (certo with { Contrato = null }, contrato),
            (certo with { Contrato = "PJ" }, contrato),
            (certo with { Contrato = "ESTÁGIO" }, contrato), // o texto gravado é o que a API lê, sem acento
            (certo with { EmailPessoal = "ana.email.com" }, emailPessoal),
            (certo with { Rg = new string('1', 21) }, "O RG pode ter ate 20 caracteres."),
            (certo with { Orgao = new string('S', 21) }, "O orgao emissor do RG pode ter ate 20 caracteres."),
            (certo with { Cep = "01310-100" }, cep),
            (certo with { Cep = "0131010" }, cep),
            (certo with { Uf = "XX" }, uf),
            (certo with { Uf = "S" }, uf),
            (certo with { Logradouro = new string('l', 151) }, "O logradouro pode ter ate 150 caracteres."),
            (certo with { Numero = new string('1', 11) }, "O numero do endereco pode ter ate 10 caracteres."),
            (certo with { Complemento = new string('c', 101) }, "O complemento pode ter ate 100 caracteres."),
            (certo with { Bairro = new string('b', 101) }, "O bairro pode ter ate 100 caracteres."),
            (certo with { Cidade = new string('c', 101) }, "A cidade pode ter ate 100 caracteres."),
            (certo with { Nome = null, Cpf = "1", Email = "x" }, "Informe o nome do funcionario."), // vários erros: sai o primeiro, na ordem dos campos
            (certo with { Cpf = "52998224724", Cargo = 987654 }, "CPF invalido."), // a entrada (inclusive o dígito verificador) é conferida antes do estado, sem esperar o trigger
        ];

        foreach (var caso in casos)
        {
            Assert.Equal(caso.Mensagem, await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", caso.Cadastro.Argumentos()));
        }

        await NadaFoiGravadoAsync(conexao, pessoas: 0, matriculas: 0);
    }

    [Fact]
    public async Task Funcionario_recusa_cargo_setor_e_nivel_que_nao_existem_e_o_nivel_que_a_API_nao_conhece()
    {
        var conexao = await NovoBancoAsync(mysql);
        var certo = await CadastroPadraoAsync(conexao);
        await Sql.ExecutarAsync(conexao, "INSERT INTO NIVEIS_DE_ACESSO (NOME_NIVEL_ACESSO) VALUES ('Diretoria')");
        var diretoria = await NivelAsync(conexao, "Diretoria");

        Assert.Equal("O cargo nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Cargo = 987654 }).Argumentos()));
        Assert.Equal("O cargo nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Cargo = null }).Argumentos()));
        Assert.Equal("O setor nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Setor = 987654 }).Argumentos()));
        Assert.Equal("O setor nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Setor = null }).Argumentos()));
        Assert.Equal("O nivel de acesso nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Nivel = 987654 }).Argumentos()));
        Assert.Equal("O nivel de acesso nao existe.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Nivel = null }).Argumentos()));
        Assert.Equal("Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (certo with { Nivel = diretoria }).Argumentos()));
        await NadaFoiGravadoAsync(conexao, pessoas: 0, matriculas: 0);
    }

    [Fact]
    public async Task Funcionario_com_CPF_ou_e_mail_de_outro_cadastro_e_recusado_e_o_cadastro_inativo_manda_readmitir()
    {
        var conexao = await NovoBancoAsync(mysql);
        var ativa = await CadastroPadraoAsync(conexao) with { Cpf = CpfDaAna, Email = "josé.lima@empresa.com.br" };
        var inativa = ativa with { Cpf = CpfValido(2), Email = "saiu@empresa.com.br" };
        await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", ativa.Argumentos());
        var idDaInativa = Inteiro((await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", inativa.Argumentos()))["id"]);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {idDaInativa}");
        var nova = ativa with { Cpf = CpfValido(3), Email = "nova@empresa.com.br" };

        Assert.Equal("Ja existe um colaborador com este CPF.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (nova with { Cpf = "529.982.247-25" }).Argumentos()));
        Assert.Equal("Ja existe um colaborador com este e-mail corporativo.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (nova with { Email = " JOSE.LIMA@Empresa.com.br" }).Argumentos())); // sem maiúsculas e sem acento, como a chave única
        Assert.Equal("Ja existe um colaborador com este CPF.", await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (nova with { Cpf = CpfDaAna, Email = "saiu@empresa.com.br" }).Argumentos())); // os dois repetidos: o CPF primeiro
        Assert.Equal(
            "Este CPF pertence a um cadastro inativo. Se a pessoa esta voltando, readmita em vez de cadastrar de novo.",
            await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (nova with { Cpf = CpfValido(2) }).Argumentos()));
        Assert.Equal(
            "Este e-mail corporativo pertence a um cadastro inativo. Se a pessoa esta voltando, readmita.",
            await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (nova with { Email = "saiu@empresa.com.br" }).Argumentos()));
        await NadaFoiGravadoAsync(conexao, pessoas: 2, matriculas: 2);

        // Sem repetir nada, a mesma pessoa entra, com a matrícula seguinte (as recusas não gastaram número).
        Assert.Equal("00003", (await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", nova.Argumentos()))["matricula"]);
    }

    [Fact]
    public async Task Funcionario_nao_e_cadastrado_quando_as_matriculas_acabaram_ou_o_contador_nao_existe()
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao);
        await Sql.ExecutarAsync(conexao, "UPDATE SEQUENCIAS SET ULTIMO_VALOR = 99998 WHERE NOME = 'MATRICULA'");

        var ultima = await ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos());
        var acabaram = await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (cadastro with { Cpf = CpfValido(2), Email = "outra@empresa.com.br" }).Argumentos());

        Assert.Equal("99999", ultima["matricula"]);
        Assert.Equal("As matriculas de 5 digitos acabaram: o contador passaria de 99999.", acabaram);
        await NadaFoiGravadoAsync(conexao, pessoas: 1, matriculas: 99999);

        await Sql.ExecutarAsync(conexao, "DELETE FROM SEQUENCIAS WHERE NOME = 'MATRICULA'");
        var semContador = await RecusaDeNegocioAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (cadastro with { Cpf = CpfValido(2), Email = "outra@empresa.com.br" }).Argumentos());

        Assert.Equal("A linha MATRICULA nao existe na tabela SEQUENCIAS (o script V001 a cria).", semContador);
        Assert.Equal(1L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
    }

    [Fact]
    public async Task Funcionarios_cadastrados_ao_mesmo_tempo_recebem_matriculas_seguidas_sem_repetir_e_sem_buraco()
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao);

        var saidas = await Task.WhenAll(Enumerable.Range(1, 8).Select(i =>
            ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (cadastro with { Cpf = CpfValido(i), Email = $"pessoa{i.ToString(CultureInfo.InvariantCulture)}@empresa.com.br" }).Argumentos())));

        Assert.Equal(
            ["00001", "00002", "00003", "00004", "00005", "00006", "00007", "00008"],
            saidas.Select(s => (string)s["matricula"]!).Order(StringComparer.Ordinal).ToArray());
        Assert.Equal("00001,00002,00003,00004,00005,00006,00007,00008", await TextoAsync(conexao, "SELECT GROUP_CONCAT(MATRICULA ORDER BY MATRICULA) FROM FUNCIONARIOS"));
        Assert.Equal(8L, await Sql.ContarAsync(conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
    }

    [Fact]
    public async Task Funcionario_pedido_varias_vezes_ao_mesmo_tempo_com_o_mesmo_CPF_so_e_cadastrado_uma_vez()
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao);

        var respostas = await Task.WhenAll(Enumerable.Range(1, 5).Select(i =>
            TentarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", (cadastro with { Email = $"tentativa{i.ToString(CultureInfo.InvariantCulture)}@empresa.com.br" }).Argumentos())));

        Assert.Single(respostas, r => r is null);
        Assert.All(respostas.Where(r => r is not null), r => Assert.Equal("Ja existe um colaborador com este CPF.", r));
        await NadaFoiGravadoAsync(conexao, pessoas: 1, matriculas: 1);
    }

    [Fact]
    public async Task Funcionario_espera_o_cadastro_que_esta_com_o_contador_de_matriculas_e_recebe_o_numero_seguinte()
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao);
        await using var outraSessao = new MySqlConnection(conexao);
        await outraSessao.OpenAsync();
        await ExecutarNaSessaoAsync(outraSessao, "START TRANSACTION");
        await ExecutarNaSessaoAsync(outraSessao, "UPDATE SEQUENCIAS SET ULTIMO_VALOR = ULTIMO_VALOR + 1 WHERE NOME = 'MATRICULA'"); // o que a API faz ao reservar a matrícula

        var chamada = ChamarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos());
        await Task.Delay(EsperaPelaTrava);
        var terminouComOContadorTravado = chamada.IsCompleted;
        await ExecutarNaSessaoAsync(outraSessao, "COMMIT");
        var saidas = await chamada;

        Assert.False(terminouComOContadorTravado);
        Assert.Equal("00002", saidas["matricula"]);
    }

    [Theory]
    [InlineData(true, "Ja existe um colaborador com este CPF.")]
    [InlineData(false, "Ja existe um colaborador com este e-mail corporativo.")]
    public async Task Funcionario_que_perde_a_corrida_para_outro_programa_e_recusado_pela_chave_unica_com_a_mensagem_da_regra(bool mesmoCpf, string mensagemEsperada)
    {
        var conexao = await NovoBancoAsync(mysql);
        var cadastro = await CadastroPadraoAsync(conexao) with { Cpf = CpfDaAna, Email = "ana@empresa.com.br" };

        // Outro programa (a API, por exemplo) grava a mesma pessoa e ainda não confirmou: a procedure não a enxerga na conferência e para na chave única.
        await using var outraSessao = new MySqlConnection(conexao);
        await outraSessao.OpenAsync();
        await ExecutarNaSessaoAsync(outraSessao, "START TRANSACTION");
        await ExecutarNaSessaoAsync(outraSessao, $"INSERT INTO FUNCIONARIOS (NOME, DATA_ADMISSAO, EMAIL_CORPORATIVO) VALUES ('Quem chegou antes', '2025-01-06', '{(mesmoCpf ? "antes@empresa.com.br" : "ana@empresa.com.br")}')");
        await ExecutarNaSessaoAsync(outraSessao, $"INSERT INTO FUNCIONARIOS_DETALHES (ID_FUNCIONARIO, CPF) VALUES (LAST_INSERT_ID(), '{(mesmoCpf ? CpfDaAna : CpfValido(9))}')");

        var chamada = TentarAsync(conexao, "SP_CADASTRAR_FUNCIONARIO", cadastro.Argumentos());
        await Task.Delay(EsperaPelaTrava);
        var terminouAntesDoOutroConfirmar = chamada.IsCompleted;
        await ExecutarNaSessaoAsync(outraSessao, "COMMIT");
        var mensagem = await chamada;

        Assert.False(terminouAntesDoOutroConfirmar);
        Assert.Equal(mensagemEsperada, mensagem);
        await NadaFoiGravadoAsync(conexao, pessoas: 1, matriculas: 0); // só quem chegou antes; o número da matrícula voltou
    }

    // ================================================================ apoio

    /// <summary>Os 20 parâmetros de entrada de <c>SP_CADASTRAR_FUNCIONARIO</c>, na ordem da procedure, mais as duas saídas (<c>id</c> e <c>matricula</c>).</summary>
    private sealed record Cadastro(
        string? Nome,
        DateOnly? Nascimento,
        DateOnly? Admissao,
        string? Email,
        int? Cargo,
        int? Setor,
        int? Nivel,
        string? Contrato,
        string? Cpf,
        string? Telefone,
        string? Rg = null,
        string? Orgao = null,
        string? EmailPessoal = null,
        string? Cep = null,
        string? Logradouro = null,
        string? Numero = null,
        string? Complemento = null,
        string? Bairro = null,
        string? Cidade = null,
        string? Uf = null)
    {
        public object?[] Argumentos() =>
        [
            Nome, Nascimento, Admissao, Email, Cargo, Setor, Nivel, Contrato, Cpf, Rg, Orgao, EmailPessoal, Telefone, Cep, Logradouro, Numero, Complemento, Bairro, Cidade, Uf,
            new Saida("id"), new Saida("matricula"),
        ];
    }

    /// <summary>Um cadastro que passa: cargo e setor (sem gestor) criados na hora, nível <c>Funcionario</c> e só os campos que a API também exige.</summary>
    private static async Task<Cadastro> CadastroPadraoAsync(string conexao) =>
        new(
            "Ana Souza",
            new DateOnly(1990, 6, 18),
            new DateOnly(2025, 3, 10),
            "ana.souza@empresa.com.br",
            await CargoAsync(conexao),
            await SetorAsync(conexao),
            await NivelAsync(conexao, "Funcionario"),
            "CLT",
            CpfDaAna,
            "11987654410");

    /// <summary>Um CPF com os dígitos verificadores certos, diferente para cada <paramref name="semente"/>.</summary>
    private static string CpfValido(int semente)
    {
        var digitos = (100200300 + semente).ToString(CultureInfo.InvariantCulture).Select(c => c - '0').ToList();
        for (var quantidade = 9; quantidade <= 10; quantidade++)
        {
            var soma = 0;
            for (var i = 0; i < quantidade; i++)
            {
                soma += digitos[i] * (quantidade + 1 - i);
            }

            var digito = soma * 10 % 11;
            digitos.Add(digito == 10 ? 0 : digito);
        }

        return string.Concat(digitos);
    }

    /// <summary>A recusa não deixou nada: o número de pessoas (com os dados pessoais de cada uma que a procedure cadastrou) e o contador de matrículas continuam os de antes.</summary>
    private static async Task NadaFoiGravadoAsync(string conexao, long pessoas, long matriculas)
    {
        Assert.Equal(pessoas, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS_DETALHES D WHERE NOT EXISTS (SELECT 1 FROM FUNCIONARIOS F WHERE F.ID_FUNCIONARIO = D.ID_FUNCIONARIO)"));
        Assert.Equal(0L, await Sql.ContarAsync(conexao, "SELECT COUNT(*) FROM FUNCIONARIOS F WHERE F.MATRICULA IS NOT NULL AND NOT EXISTS (SELECT 1 FROM FUNCIONARIOS_DETALHES D WHERE D.ID_FUNCIONARIO = F.ID_FUNCIONARIO)"));
        Assert.Equal(matriculas, await Sql.ContarAsync(conexao, "SELECT ULTIMO_VALOR FROM SEQUENCIAS WHERE NOME = 'MATRICULA'"));
    }

    private static async Task<string> NivelDeAsync(string conexao, int funcionarioId) =>
        await TextoAsync(conexao, $"SELECT N.NOME_NIVEL_ACESSO FROM FUNCIONARIOS F JOIN NIVEIS_DE_ACESSO N ON N.ID_NIVEL_ACESSO = F.ID_NIVEL_ACESSO WHERE F.ID_FUNCIONARIO = {funcionarioId}");

    private static async Task<string> TextoAsync(string conexao, string sql) =>
        Convert.ToString(await Sql.EscalarAsync(conexao, sql), CultureInfo.InvariantCulture) ?? "<sem linha>";

    /// <summary>Chama a procedure e devolve <c>null</c> se ela aceitou, ou a mensagem da recusa de negócio (qualquer outro erro derruba o teste).</summary>
    private static async Task<string?> TentarAsync(string conexao, string procedure, params object?[] argumentos)
    {
        try
        {
            await ChamarAsync(conexao, procedure, argumentos);
            return null;
        }
        catch (MySqlException erro) when (erro.SqlState == "45000")
        {
            return erro.Message;
        }
    }

    private static async Task ExecutarNaSessaoAsync(MySqlConnection sessao, string sql)
    {
        await using var comando = new MySqlCommand(sql, sessao);
        await comando.ExecuteNonQueryAsync();
    }
}
