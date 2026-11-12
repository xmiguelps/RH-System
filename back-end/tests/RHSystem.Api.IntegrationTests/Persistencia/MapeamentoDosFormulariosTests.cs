using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta das sete tabelas dos formulários no banco REAL, relendo com OUTRO contexto. Cada teste prova um cuidado do plano da fatia 5: o rascunho nasce INATIVO (o padrão do banco é TRUE), as datas do grupo vão em -03:00 e cortadas ao segundo, a resposta anônima não tem autor
/// nem arquivo, a data do candidato vem do relógio da aplicação (não do <c>CURRENT_TIMESTAMP</c> do servidor), as colunas geradas não são enviadas e a participação guarda o Guid v4 em minúsculas. As três entidades que não podem deixar rastro de quem respondeu não são auditadas.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDosFormulariosTests(MySqlFixture mysql)
{
    // 12:42 UTC com fração: as colunas não têm fração e as entidades cortam ao segundo.
    private static readonly DateTime Agora = new DateTime(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc).AddMilliseconds(600);

    private static readonly DateTime AgoraAoSegundo = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private static async Task<(AppDbContext Db, Funcionario Pessoa)> NovaPessoaAsync(string conexao, string email)
    {
        var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar($"Pessoa {email}", email, new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();
        return (db, pessoa);
    }

    private static async Task<object?> EscalarAsync(string conexao, string sql) => await Sql.EscalarAsync(conexao, sql);

    private static string Texto(object? valor) => Convert.ToDateTime(valor, CultureInfo.InvariantCulture).ToString("yyyy-MM-dd HH:mm:ss", CultureInfo.InvariantCulture);

    private static async Task<(Formulario Formulario, FormularioPergunta Pergunta)> FormularioComPerguntaAsync(AppDbContext db, int autor, bool anonimo, TipoDePergunta tipo = TipoDePergunta.RespostaCurta)
    {
        var formulario = Formulario.Criar(autor, $"Formulário {Guid.NewGuid():N}", null, TipoDeFormulario.Pesquisa, null, anonimo, Agora);
        db.Formularios.Add(formulario);
        await db.SaveChangesAsync();
        var pergunta = FormularioPergunta.Criar(formulario.Id, "Como foi?", tipo, 1, obrigatoria: false);
        db.FormulariosPerguntas.Add(pergunta);
        await db.SaveChangesAsync();
        return (formulario, pergunta);
    }

    [Fact]
    public async Task Formulario_nasce_inativo_com_lembrete_ligado_e_a_data_de_criacao_em_menos_3_horas_cortada_ao_segundo()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form1@teste.com");
        var formulario = Formulario.Criar(autor.Id, "Pesquisa de clima", "Conte como foi o trimestre.", TipoDeFormulario.Avaliacao, SentidoDaAvaliacao.EquipeAvaliaGestor, anonimo: true, Agora);
        db.Formularios.Add(formulario);

        await db.SaveChangesAsync();

        // o padrão do banco é ATIVO = TRUE: o rascunho só nasce inativo porque a entidade envia o valor
        Assert.Equal((0, 1, 1, "2026-10-07 09:42:00", "AVALIACAO", "EQUIPE_AVALIA_GESTOR"), (Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ATIVO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT LEMBRETE_AUTOMATICO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ANONIMO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}")), Texto(await EscalarAsync(conexao, $"SELECT DATA_CRIACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}")), (string)(await EscalarAsync(conexao, $"SELECT TIPO_FORMULARIO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}"))!, (string)(await EscalarAsync(conexao, $"SELECT SENTIDO_AVALIACAO FROM FORMULARIOS WHERE ID_FORMULARIO = {formulario.Id}"))!));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.Formularios.AsNoTracking().SingleAsync(f => f.Id == formulario.Id);
        Assert.Equal((autor.Id, "Pesquisa de clima", "Conte como foi o trimestre.", TipoDeFormulario.Avaliacao, SentidoDaAvaliacao.EquipeAvaliaGestor, true, false, true, (DateTime?)AgoraAoSegundo), (lido.AutorId, lido.Titulo, lido.Descricao, lido.Tipo, lido.SentidoAvaliacao, lido.Anonimo, lido.Ativo, lido.LembreteAutomatico, lido.CriadoEmUtc));
        Assert.Null(lido.Alvo);
        Assert.Null(lido.PublicadoEmUtc);
        Assert.Null(lido.EncerradoEmUtc);
    }

    [Fact]
    public async Task Formulario_publicado_por_link_e_recorrente_lido_do_banco_traz_cada_coluna_com_as_datas_em_utc()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form2@teste.com");
        await Sql.ExecutarAsync(
            conexao,
            $"INSERT INTO FORMULARIOS (TITULO, DESCRICAO, TIPO_FORMULARIO, ID_FUNCIONARIO_AUTOR, DATA_CRIACAO, ATIVO, PUBLICO, DATA_PUBLICACAO, DATA_PRAZO, ANONIMO, RECORRENCIA_INTERVALO, RECORRENCIA_UNIDADE, PROXIMA_REABERTURA, RECORRENCIA_ATE, LINK_PUBLICO_CODIGO, LEMBRETE_AUTOMATICO) VALUES ('Entrevista', 'Descrição', 'ENTREVISTA', {autor.Id}, '2026-10-01 08:00:00', TRUE, 'LINK', '2026-10-02 00:00:00', '2026-10-30', FALSE, 3, 'SEMANA', '2026-11-01 00:00:00', '2027-01-31', '{new string('a', 43)}', TRUE)");
        var id = (int)await Sql.ContarAsync(conexao, "SELECT ID_FORMULARIO FROM FORMULARIOS WHERE TITULO = 'Entrevista'");

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.Formularios.AsNoTracking().SingleAsync(f => f.Id == id);

        Assert.Equal(
            (TipoDeFormulario.Entrevista, AlvoDoFormulario.Link, true, (DateTime?)new DateTime(2026, 10, 1, 11, 0, 0, DateTimeKind.Utc), (DateTime?)new DateTime(2026, 10, 2, 3, 0, 0, DateTimeKind.Utc), (DateOnly?)new DateOnly(2026, 10, 30)),
            (lido.Tipo!.Value, lido.Alvo!.Value, lido.Ativo, lido.CriadoEmUtc, lido.PublicadoEmUtc, lido.Prazo));
        Assert.Equal(
            ((int?)3, (UnidadeDeRecorrencia?)UnidadeDeRecorrencia.Semana, (DateTime?)new DateTime(2026, 11, 1, 3, 0, 0, DateTimeKind.Utc), (DateOnly?)new DateOnly(2027, 1, 31), new string('a', 43), false),
            (lido.RecorrenciaIntervalo, lido.RecorrenciaUnidade, lido.ProximaReaberturaEmUtc, lido.RecorrenciaAte, lido.TokenDoLink, lido.Anonimo));
        Assert.Null(lido.EncerradoEmUtc);
        Assert.Null(lido.SentidoAvaliacao);
    }

    [Fact]
    public async Task Pergunta_e_alternativas_gravam_o_tipo_em_maiusculas_a_ordem_e_o_obrigatoria_falso_e_voltam_iguais()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form3@teste.com");
        var (formulario, pergunta) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: false, TipoDePergunta.MultiplaEscolha);
        var a = FormularioAlternativa.Criar(pergunta.Id, "Ótimo", 1);
        var b = FormularioAlternativa.Criar(pergunta.Id, "Bom", 2);
        db.FormulariosAlternativas.AddRange(a, b);
        await db.SaveChangesAsync();

        Assert.Equal(("MULTIPLA_ESCOLHA", 1, 0), ((string)(await EscalarAsync(conexao, $"SELECT TIPO_RESPOSTA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta.Id}"))!, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ORDEM FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta.Id}")), Convert.ToInt32(await EscalarAsync(conexao, $"SELECT OBRIGATORIA FROM FORMULARIOS_PERGUNTAS WHERE ID_FORMULARIO_PERGUNTA = {pergunta.Id}"))));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.FormulariosPerguntas.AsNoTracking().SingleAsync(p => p.Id == pergunta.Id);
        var alternativas = await outro.FormulariosAlternativas.AsNoTracking().Where(x => x.PerguntaId == pergunta.Id).OrderBy(x => x.Ordem).ToListAsync();
        Assert.Equal((formulario.Id, "Como foi?", (TipoDePergunta?)TipoDePergunta.MultiplaEscolha, (int?)1, false), (lida.FormularioId, lida.Texto, lida.Tipo, lida.Ordem, lida.Obrigatoria));
        Assert.Equal([("Ótimo", 1), ("Bom", 2)], alternativas.Select(x => (x.Texto, x.Ordem)));
    }

    [Fact]
    public async Task Resposta_de_funcionario_e_de_candidato_gravam_o_anonimo_falso_e_a_anonima_nao_tem_autor_nem_arquivo()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form4@teste.com");
        var (comNome, perguntaComNome) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: false, TipoDePergunta.Arquivo);
        var (anonimo, perguntaAnonima) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: true);
        await Sql.ExecutarAsync(conexao, $"UPDATE FORMULARIOS SET ATIVO = TRUE WHERE ID_FORMULARIO IN ({comNome.Id}, {anonimo.Id})"); // o trigger recusa resposta em formulário inativo
        var candidato = FormularioCandidato.Criar(comNome.Id, "Maria Candidata", "maria@exemplo.com", "(27) 99999-0000", "formularios/cv.pdf", Agora);
        db.FormulariosCandidatos.Add(candidato);
        await db.SaveChangesAsync();
        var doFuncionario = FormularioResposta.DeFuncionario(comNome.Id, perguntaComNome.Id, autor.Id, "comprovante.pdf", Agora, avaliadoId: null, alternativaId: null, "formularios/a.pdf", "comprovante.pdf");
        var doCandidato = FormularioResposta.DeCandidato(comNome.Id, perguntaComNome.Id, candidato.Id, "portfolio.pdf", Agora, alternativaId: null, "formularios/b.pdf", "portfolio.pdf");
        var anonima = FormularioResposta.Anonima(anonimo.Id, perguntaAnonima.Id, "Foi bom.", new DateTime(2026, 10, 7, 3, 0, 0, DateTimeKind.Utc), avaliadoId: null, alternativaId: null);
        db.FormulariosRespostas.AddRange(doFuncionario, doCandidato, anonima);

        await db.SaveChangesAsync();

        await using var outro = ContextoDeTeste.Novo(conexao);
        var lidas = await outro.FormulariosRespostas.AsNoTracking().Where(r => r.Id == doFuncionario.Id || r.Id == doCandidato.Id || r.Id == anonima.Id).OrderBy(r => r.Id).ToListAsync();
        Assert.Equal(
            [(autor.Id, (int?)null, false, "comprovante.pdf", "formularios/a.pdf"), ((int?)null, candidato.Id, false, "portfolio.pdf", "formularios/b.pdf"), ((int?)null, (int?)null, true, "Foi bom.", (string?)null)],
            lidas.Select(r => ((int?)r.FuncionarioId, r.CandidatoId, r.Anonimo, r.Resposta, r.CaminhoArquivo)));
        Assert.Equal(["2026-10-07 09:42:00", "2026-10-07 09:42:00", "2026-10-07 00:00:00"], lidas.Select(r => Texto(r.RespondidaEmUtc!.Value.AddHours(-3))));
        Assert.Equal(1, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ANONIMO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_RESPOSTA = {anonima.Id}")));
        Assert.Equal(0, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT ANONIMO FROM FORMULARIOS_RESPOSTAS WHERE ID_FORMULARIO_RESPOSTA = {doFuncionario.Id}")));
    }

    [Fact]
    public async Task Candidato_grava_a_data_do_relogio_da_aplicacao_e_nao_a_do_servidor_e_a_chave_unica_recusa_o_mesmo_email()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form5@teste.com");
        var (formulario, _) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: false);
        var candidato = FormularioCandidato.Criar(formulario.Id, "Maria", "maria@exemplo.com", null, null, Agora);
        db.FormulariosCandidatos.Add(candidato);

        await db.SaveChangesAsync();

        // o servidor usaria o CURRENT_TIMESTAMP real (outro dia): a data tem de ser a do relógio falso
        Assert.Equal("2026-10-07 09:42:00", Texto(await EscalarAsync(conexao, $"SELECT DATA_CADASTRO FROM FORMULARIOS_CANDIDATOS WHERE ID_FORMULARIO_CANDIDATO = {candidato.Id}")));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lido = await outro.FormulariosCandidatos.AsNoTracking().SingleAsync(c => c.Id == candidato.Id);
        Assert.Equal((formulario.Id, "Maria", "maria@exemplo.com", (string?)null, (string?)null, AgoraAoSegundo), (lido.FormularioId, lido.Nome, lido.Email, lido.Telefone, lido.CaminhoCurriculo, lido.CadastradoEmUtc));

        await using var terceiro = ContextoDeTeste.Novo(conexao);
        terceiro.FormulariosCandidatos.Add(FormularioCandidato.Criar(formulario.Id, "Outra Maria", "MARIA@exemplo.com", null, null, Agora)); // o banco não diferencia a caixa
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => terceiro.SaveChangesAsync());
        Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
    }

    [Fact]
    public async Task Destinatario_por_pessoa_e_por_setor_gravam_sem_a_coluna_gerada_e_o_banco_calcula_a_chave()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form6@teste.com");
        var (formulario, _) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: false);
        var setorId = (int)await Sql.ContarAsync(conexao, "SELECT COALESCE(MIN(ID_SETOR), 0) FROM SETORES");
        if (setorId == 0)
        {
            await Sql.ExecutarAsync(conexao, "INSERT INTO SETORES (NOME_SETOR) VALUES ('Setor do formulário')");
            setorId = (int)await Sql.ContarAsync(conexao, "SELECT MIN(ID_SETOR) FROM SETORES");
        }

        var daPessoa = FormularioDestinatario.DaPessoa(formulario.Id, autor.Id);
        var doSetor = FormularioDestinatario.DoSetor(formulario.Id, setorId);
        db.FormulariosDestinatarios.AddRange(daPessoa, doSetor);

        await db.SaveChangesAsync();

        Assert.Equal([$"{autor.Id}:0:0", $"0:{setorId}:0"], new[] { daPessoa.Id, doSetor.Id }.Select(id => (string)EscalarAsync(conexao, $"SELECT CHAVE_DESTINO FROM FORMULARIOS_DESTINATARIOS WHERE ID_FORMULARIO_DESTINATARIO = {id}").GetAwaiter().GetResult()!));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lidos = await outro.FormulariosDestinatarios.AsNoTracking().Where(d => d.FormularioId == formulario.Id).OrderBy(d => d.Id).ToListAsync();
        Assert.Equal([((int?)autor.Id, (int?)null), ((int?)null, (int?)setorId)], lidos.Select(d => (d.FuncionarioId, d.SetorId)));

        await using var terceiro = ContextoDeTeste.Novo(conexao);
        terceiro.FormulariosDestinatarios.Add(FormularioDestinatario.DaPessoa(formulario.Id, autor.Id)); // a mesma pessoa de novo: a chave gerada é única
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => terceiro.SaveChangesAsync());
        Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
    }

    [Fact]
    public async Task Participacao_guarda_o_guid_v4_em_minusculas_sem_data_e_a_chave_unica_recusa_a_mesma_pessoa()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        var (db, autor) = await NovaPessoaAsync(conexao, "form7@teste.com");
        var (formulario, _) = await FormularioComPerguntaAsync(db, autor.Id, anonimo: true);
        var participacao = FormularioParticipacao.Criar(formulario.Id, autor.Id, null);

        db.FormulariosParticipacoes.Add(participacao);
        await db.SaveChangesAsync();

        var guardado = (string)(await EscalarAsync(conexao, $"SELECT CONCAT(ID_PARTICIPACAO, '') FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {formulario.Id}"))!; // o texto como o banco guarda
        Assert.Equal((participacao.Id.ToString("D"), '4', 36), (guardado, guardado[14], guardado.Length));
        Assert.Equal(guardado, guardado.ToLowerInvariant());
        Assert.Equal(0, Convert.ToInt32(await EscalarAsync(conexao, $"SELECT CHAVE_AVALIADO FROM FORMULARIOS_PARTICIPACOES WHERE ID_FORMULARIO = {formulario.Id}")));
        await using var outro = ContextoDeTeste.Novo(conexao);
        var lida = await outro.FormulariosParticipacoes.AsNoTracking().SingleAsync(p => p.FormularioId == formulario.Id);
        Assert.Equal((participacao.Id, autor.Id, (int?)null), (lida.Id, lida.FuncionarioId, lida.AvaliadoId));

        await using var terceiro = ContextoDeTeste.Novo(conexao);
        terceiro.FormulariosParticipacoes.Add(FormularioParticipacao.Criar(formulario.Id, autor.Id, null));
        var erro = await Assert.ThrowsAsync<DbUpdateException>(() => terceiro.SaveChangesAsync());
        Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
    }

    [Fact]
    public void As_entidades_que_nao_podem_deixar_rastro_de_quem_respondeu_nao_sao_auditadas_pelo_interceptor()
    {
        foreach (var tipo in new[] { typeof(FormularioResposta), typeof(FormularioParticipacao), typeof(FormularioCandidato), typeof(Formulario), typeof(FormularioPergunta), typeof(FormularioAlternativa), typeof(FormularioDestinatario) })
        {
            Assert.Empty(tipo.GetCustomAttributes(typeof(AuditadoAttribute), inherit: true));
        }
    }
}
