using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Formularios;

namespace RHSystem.Api.IntegrationTests.Formularios;

/// <summary>
/// O tradutor de recusa do banco com exceções REAIS do MySQL: os triggers das perguntas e das respostas, as restrições (<c>CHECK</c>) e as chaves únicas e estrangeiras dos formulários vêm de gravações de verdade; para o texto que só aparece em condições raras, o
/// teste levanta o mesmo <c>SIGNAL SQLSTATE '45000'</c> com a mensagem original. O que não é dos formulários volta <c>null</c> (o chamador deixa subir e o tratador global decide), e a mensagem do MySQL (que traz o VALOR da chave, um e-mail por exemplo) nunca sai na resposta.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class TradutorDeRecusaDosFormulariosTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 0, 0, DateTimeKind.Utc);
    private static int _sequencia;

    private async Task<string> ConexaoAsync()
    {
        var (conexao, _) = await BancoCompartilhado.ObterAsync(mysql, nameof(TradutorDeRecusaDosFormulariosTests), async nova =>
        {
            await Sql.ExecutarAsync(nova, "CREATE PROCEDURE P_SINALIZA(IN mensagem VARCHAR(200)) BEGIN SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = mensagem; END");
            return 0;
        });
        return conexao;
    }

    private static async Task<int> NovaPessoaAsync(string conexao)
    {
        var n = Interlocked.Increment(ref _sequencia);
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(x => x.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa tradutor {n}", $"tradutor.formularios{n}@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return pessoa.Id;
    }

    /// <summary>Um formulário com uma pergunta (obrigatória ou não); aberto (<c>ATIVO</c> ligado) para o trigger aceitar resposta.</summary>
    private static async Task<(int Formulario, int Pergunta)> NovoFormularioAsync(string conexao, int autor, bool anonimo = false, bool aberto = true, bool obrigatoria = false)
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        var formulario = Formulario.Criar(autor, $"Formulário {Guid.NewGuid():N}", null, TipoDeFormulario.Pesquisa, null, anonimo, Agora);
        db.Formularios.Add(formulario);
        await db.SaveChangesAsync();
        var pergunta = FormularioPergunta.Criar(formulario.Id, "Como foi?", TipoDePergunta.RespostaCurta, 1, obrigatoria);
        db.FormulariosPerguntas.Add(pergunta);
        await db.SaveChangesAsync();
        if (aberto)
        {
            await Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE WHERE ID_FORMULARIO = {formulario.Id}");
        }

        return (formulario.Id, pergunta.Id);
    }

    private static async Task ResponderAsync(string conexao, int formulario, int pergunta, int pessoa, string resposta = "Foi bom.")
    {
        await using var db = ContextoDeTeste.Novo(conexao);
        db.FormulariosRespostas.Add(FormularioResposta.DeFuncionario(formulario, pergunta, pessoa, resposta, Agora, avaliadoId: null, alternativaId: null, caminhoArquivo: null, nomeArquivoOriginal: null));
        await db.SaveChangesAsync();
    }

    private static async Task<Exception> ExcecaoDeAsync(Func<Task> acao)
    {
        try
        {
            await acao();
        }
        catch (Exception erro)
        {
            return erro;
        }

        throw new InvalidOperationException("A gravação devia ter sido recusada pelo banco.");
    }

    // ---------------------------------------------------------------- o que o trigger recusa

    [Fact]
    public async Task Editar_e_excluir_pergunta_respondida_viram_422_PERGUNTA_JA_RESPONDIDA_com_a_regra_do_banco_de_cada_um()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, pessoa);
        await ResponderAsync(conexao, formulario, pergunta, pessoa);
        var aoEditar = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS_PERGUNTAS SET TEXTO_PERGUNTA = 'Outra' WHERE ID_FORMULARIO_PERGUNTA = {pergunta}"));
        var aoExcluir = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"DELETE FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta}"));

        var daEdicao = TradutorDeRecusaDosFormularios.Traduzir(aoEditar)!;
        var daExclusao = TradutorDeRecusaDosFormularios.Traduzir(aoExcluir)!;

        Assert.Equal((422, "PERGUNTA_JA_RESPONDIDA", "Esta pergunta já foi respondida. Só dá para mudar se ela é obrigatória e a posição dela.", "Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario."), (daEdicao.Status, daEdicao.Codigo, daEdicao.Detalhe, daEdicao.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "PERGUNTA_JA_RESPONDIDA", "Esta pergunta já foi respondida e não pode ser excluída.", "Pergunta ja respondida: nao e possivel exclui-la."), (daExclusao.Status, daExclusao.Codigo, daExclusao.Detalhe, daExclusao.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Resposta_em_formulario_inativo_vira_422_FORMULARIO_ENCERRADO_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        await using var db = ContextoDeTeste.Novo(conexao);
        db.FormulariosRespostas.Add(FormularioResposta.DeFuncionario(formulario, pergunta, pessoa, "Foi bom.", Agora, null, null, null, null));
        var erro = await ExcecaoDeAsync(() => db.SaveChangesAsync());

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, "FORMULARIO_ENCERRADO", "Este formulário não está mais aceitando respostas.", "Formulario inativo nao aceita respostas."), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Resposta_de_pessoa_inativa_vira_422_FUNCIONARIO_INATIVO_com_a_regra_do_banco()
    {
        var conexao = await ConexaoAsync();
        var autor = await NovaPessoaAsync(conexao);
        var inativa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, autor);
        await Sql.ExecutarAsync(conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE WHERE ID_FUNCIONARIO = {inativa}");
        var erro = await ExcecaoDeAsync(() => ResponderAsync(conexao, formulario, pergunta, inativa));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, "FUNCIONARIO_INATIVO", "Uma pessoa inativa não pode responder formulários.", "Funcionario inativo nao pode responder formularios."), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Pergunta_obrigatoria_sem_resposta_vira_a_recusa_generica_sem_codigo_com_a_mensagem_original()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, pessoa, obrigatoria: true);
        var erro = await ExcecaoDeAsync(() => ResponderAsync(conexao, formulario, pergunta, pessoa, resposta: ""));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, (string?)null, "A operação foi recusada por uma regra do banco de dados.", "Pergunta obrigatoria: informe a resposta."), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Regra_de_trigger_que_os_formularios_nao_nomeiam_vira_a_recusa_generica_com_a_mensagem_original()
    {
        var conexao = await ConexaoAsync();
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "CALL P_SINALIZA('Uma regra que ninguem conhece.')"));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, (string?)null, "Uma regra que ninguem conhece."), (problema.Status, problema.Codigo, problema.Extensoes!["regraDoBanco"]));
        Assert.Equal("Operação recusada.", problema.Titulo);
    }

    // ---------------------------------------------------------------- as restrições (CHECK)

    [Fact]
    public async Task Anonimo_por_link_que_o_CHECK_recusa_vira_422_ANONIMO_SEM_LINK_com_o_nome_da_restricao()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET ANONIMO = TRUE, PUBLICO = 'LINK', LINK_PUBLICO_CODIGO = '{new string('b', 43)}' WHERE ID_FORMULARIO = {formulario}"));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, "ANONIMO_SEM_LINK", "Formulário anônimo não pode ser respondido por link público.", "CHK_FORMULARIOS_ANONIMO_SEM_LINK"), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    [Fact]
    public async Task Outra_restricao_CHECK_vira_a_recusa_generica_sem_codigo_com_o_nome_dela()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa); // aberto, ATIVO ligado
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET DATA_ENCERRAMENTO = '2026-10-01 00:00:00' WHERE ID_FORMULARIO = {formulario}")); // encerrado e ativo ao mesmo tempo

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, (string?)null, "A operação foi recusada por uma regra do banco de dados.", "CHK_FORMULARIOS_ENCERRADO_INATIVO"), (problema.Status, problema.Codigo, problema.Detalhe, problema.Extensoes!["regraDoBanco"]));
    }

    // ---------------------------------------------------------------- as chaves únicas (pelo nome) e a estrangeira do anonimato

    [Fact]
    public async Task O_mesmo_email_de_candidato_vira_409_JA_RESPONDIDO_do_candidato_sem_o_valor_da_chave()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.FormulariosCandidatos.Add(FormularioCandidato.Criar(formulario, "Maria", "maria.candidata@exemplo.com", null, null, Agora));
            await db.SaveChangesAsync();
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.FormulariosCandidatos.Add(FormularioCandidato.Criar(formulario, "Outra Maria", "MARIA.CANDIDATA@exemplo.com", null, null, Agora));
        var erro = await ExcecaoDeAsync(() => segundo.SaveChangesAsync());

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((409, "Conflito.", "JA_RESPONDIDO", "Já recebemos uma candidatura com este e-mail."), (problema.Status, problema.Titulo, problema.Codigo, problema.Detalhe));
        Assert.DoesNotContain("maria.candidata", problema.Detalhe, StringComparison.OrdinalIgnoreCase);
        Assert.Null(problema.Extensoes);
    }

    [Fact]
    public async Task A_mesma_participacao_no_anonimo_vira_409_JA_RESPONDIDO_da_pessoa()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa, anonimo: true);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.FormulariosParticipacoes.Add(FormularioParticipacao.Criar(formulario, pessoa, null));
            await db.SaveChangesAsync();
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.FormulariosParticipacoes.Add(FormularioParticipacao.Criar(formulario, pessoa, null));
        var erro = await ExcecaoDeAsync(() => segundo.SaveChangesAsync());

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((409, "JA_RESPONDIDO", "Você já respondeu este formulário."), (problema.Status, problema.Codigo, problema.Detalhe));
    }

    [Fact]
    public async Task A_ordem_repetida_de_pergunta_ou_de_alternativa_vira_409_sem_codigo_que_pede_para_atualizar()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.FormulariosAlternativas.Add(FormularioAlternativa.Criar(pergunta, "Ótimo", 1));
            await db.SaveChangesAsync();
        }

        await using var a = ContextoDeTeste.Novo(conexao);
        a.FormulariosPerguntas.Add(FormularioPergunta.Criar(formulario, "Outra pergunta na mesma posição", TipoDePergunta.RespostaCurta, 1, false));
        var daPergunta = await ExcecaoDeAsync(() => a.SaveChangesAsync());
        await using var b = ContextoDeTeste.Novo(conexao);
        b.FormulariosAlternativas.Add(FormularioAlternativa.Criar(pergunta, "Outra opção na mesma posição", 1));
        var daAlternativa = await ExcecaoDeAsync(() => b.SaveChangesAsync());

        var p1 = TradutorDeRecusaDosFormularios.Traduzir(daPergunta)!;
        var p2 = TradutorDeRecusaDosFormularios.Traduzir(daAlternativa)!;

        Assert.All(new[] { p1, p2 }, p => Assert.Equal((409, "Conflito.", (string?)null, "As perguntas deste formulário mudaram em outra tela. Atualize a página e tente de novo."), (p.Status, p.Titulo, p.Codigo, p.Detalhe)));
    }

    [Fact]
    public async Task Destinatario_repetido_e_token_repetido_voltam_null_porque_o_chamador_conta_ou_gera_outro()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        await using (var db = ContextoDeTeste.Novo(conexao))
        {
            db.FormulariosDestinatarios.Add(FormularioDestinatario.DaPessoa(formulario, pessoa));
            await db.SaveChangesAsync();
        }

        await using var segundo = ContextoDeTeste.Novo(conexao);
        segundo.FormulariosDestinatarios.Add(FormularioDestinatario.DaPessoa(formulario, pessoa));
        var doDestinatario = await ExcecaoDeAsync(() => segundo.SaveChangesAsync());
        var (outro, _) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        await Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET PUBLICO = 'LINK', TIPO_FORMULARIO = 'ENTREVISTA', LINK_PUBLICO_CODIGO = '{new string('c', 43)}' WHERE ID_FORMULARIO = {formulario}");
        var doToken = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET PUBLICO = 'LINK', TIPO_FORMULARIO = 'ENTREVISTA', LINK_PUBLICO_CODIGO = '{new string('c', 43)}' WHERE ID_FORMULARIO = {outro}"));

        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(doDestinatario));
        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(doToken));
    }

    [Fact]
    public async Task Trocar_o_anonimato_com_resposta_gravada_e_a_estrangeira_1451_que_vira_409_ANONIMATO_FIXO_APOS_PUBLICAR()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, pergunta) = await NovoFormularioAsync(conexao, pessoa);
        await ResponderAsync(conexao, formulario, pergunta, pessoa);
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET ANONIMO = TRUE WHERE ID_FORMULARIO = {formulario}"));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((409, "ANONIMATO_FIXO_APOS_PUBLICAR", "Depois de publicado, o formulário não muda entre anônimo e identificado."), (problema.Status, problema.Codigo, problema.Detalhe));
    }

    [Fact]
    public async Task Outra_chave_estrangeira_em_uso_nao_e_dos_formularios_e_volta_null()
    {
        var conexao = await ConexaoAsync();
        var pessoa = await NovaPessoaAsync(conexao);
        var (formulario, _) = await NovoFormularioAsync(conexao, pessoa, aberto: false);
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, $"DELETE FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario}")); // a pergunta filha segura o formulário (FK_FORMULARIOS_PERGUNTAS_FORMULARIOS)

        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(erro));
    }

    [Fact]
    public async Task O_texto_do_trigger_e_reconhecido_sem_diferenciar_caixa()
    {
        var conexao = await ConexaoAsync();
        var erro = await ExcecaoDeAsync(() => Sql.ExecutarAsync(conexao, "CALL P_SINALIZA('PERGUNTA JA RESPONDIDA: NAO E POSSIVEL EXCLUI-LA.')"));

        var problema = TradutorDeRecusaDosFormularios.Traduzir(erro)!;

        Assert.Equal((422, "PERGUNTA_JA_RESPONDIDA"), (problema.Status, problema.Codigo));
    }

    [Fact]
    public void Restricao_CHECK_com_mensagem_sem_o_nome_volta_null_porque_nada_se_pode_dizer_dela()
    {
        // o construtor com código, estado e mensagem não é público: o MySQL real sempre põe o nome entre aspas, e esta é a única forma de provar o caminho defensivo
        var construtor = typeof(MySqlConnector.MySqlException).GetConstructor(System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance, [typeof(MySqlConnector.MySqlErrorCode), typeof(string), typeof(string)])!;
        var semNome = (MySqlConnector.MySqlException)construtor.Invoke([(MySqlConnector.MySqlErrorCode)3819, "HY000", "Check constraint violada sem nome."]);

        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(semNome));
    }

    [Fact]
    public void O_que_nao_e_erro_do_MySQL_volta_null_e_a_cadeia_de_excecoes_e_percorrida()
    {
        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(new InvalidOperationException("outra coisa")));
        Assert.Null(TradutorDeRecusaDosFormularios.Traduzir(new DbUpdateException("embrulho sem erro de banco", new InvalidOperationException())));
    }
}
