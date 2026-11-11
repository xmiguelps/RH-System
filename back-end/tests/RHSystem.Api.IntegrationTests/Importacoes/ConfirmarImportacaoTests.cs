using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Logging;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Documentos;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using static RHSystem.Api.IntegrationTests.Cadastros.CadastrosDeTeste;
using static RHSystem.Api.IntegrationTests.Importacoes.PlanilhasDeTeste;

namespace RHSystem.Api.IntegrationTests.Importacoes;

/// <summary>
/// <c>POST /api/rh/importacoes/{importacaoId}/confirmar</c> (<c>confirmarImportacao</c>): cadastra TODAS as pessoas da prévia como o cadastro da tela 02, ou ninguém (tudo ou nada). No banco REAL com o relógio fixo em
/// 2026-10-07T12:42Z; a prévia é criada pela API, como o RH faria. A ordem das conferências é 404, 409, 422 expirada, 422 com erros, e só então as caixas e o cadastro; toda recusa deixa a importação intacta e o arquivo lá.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConfirmarImportacaoTests(MySqlFixture mysql)
{
    private const string RotaDaPrevia = "/api/rh/importacoes";

    private const string TipoXlsx = "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet";

    private static int _proximo = 830_000_000;

    private Task<ApiDeAcesso> ApiAsync() => ApiDeAcesso.ObterAsync(mysql, nameof(ConfirmarImportacaoTests));

    private static HttpClient ComoRh(ApiDeAcesso api) => api.Api.ClienteComo(api.Elenco.Rh);

    private static string Rota(int importacaoId) => $"/api/rh/importacoes/{importacaoId}/confirmar";

    private sealed record Apoio(string Sufixo, string Cargo, string Setor, string Beneficio, string OutroBeneficio, int? GestorId, int? GestorFuncionarioId)
    {
        public string Dominio => $"c{Sufixo}.teste.com";
    }

    private static async Task<Apoio> ApoioAsync(ApiDeAcesso api, bool comGestor = false)
    {
        var s = Guid.NewGuid().ToString("N")[..8];
        int? gestorFuncionario = null;
        int? gestor = null;
        if (comGestor)
        {
            gestorFuncionario = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Gestor confirma {s}", $"gestor.{s}@gestores.teste.com", new DateOnly(2025, 1, 6), nivel: "Funcionario Gestor");
            await api.NovoGestorAsync(gestorFuncionario.Value);
            gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {gestorFuncionario}");
        }

        var apoio = new Apoio(s, $"Sr. Análise {s}", $"Operações {s}", $"Vale-refeição {s}", $"Plano de saúde {s}", gestor, gestorFuncionario);
        await SementeDoCadastro.CargoAsync(api.Conexao, apoio.Cargo, 40);
        await SementeDoCadastro.SetorAsync(api.Conexao, apoio.Setor, gestor);
        await SementeDoCadastro.BeneficioAsync(api.Conexao, apoio.Beneficio);
        await SementeDoCadastro.BeneficioAsync(api.Conexao, apoio.OutroBeneficio);
        return apoio;
    }

    private static object?[] Valida(Apoio apoio, string? cpf = null, string? email = null, string? admissao = null, string? beneficios = null, string? nome = null)
    {
        var n = Interlocked.Increment(ref _proximo);
        return Linha(
            nome: nome ?? $"Pessoa {n}",
            cpf: cpf ?? CpfsDeTeste.Valido(n),
            email: email ?? $"pessoa{n}@{apoio.Dominio}",
            admissao: admissao ?? "05/01/2026",
            cargo: apoio.Cargo,
            setor: apoio.Setor,
            beneficios: beneficios);
    }

    /// <summary>A prévia, pela API, como o RH a faria: devolve o id e o caminho do arquivo guardado.</summary>
    private static async Task<(int Id, string Caminho)> PreviaAsync(ApiDeAcesso api, params object?[][] linhas)
    {
        var resposta = await ComoRh(api).PostAsync(RotaDaPrevia, FormulariosDeTeste.Envio("novos-colaboradores.xlsx", Xlsx(linhas), TipoXlsx));
        Assert.Equal(201, (int)resposta.StatusCode);
        var id = (await CorpoAsync(resposta)).GetProperty("importacaoId").GetInt32();
        return (id, (await TextoAsync(api, id, "CAMINHO_ARQUIVO"))!);
    }

    private static Task<HttpResponseMessage> ConfirmarAsync(HttpClient cliente, int importacaoId, object? corpo = null) =>
        cliente.PostAsJsonAsync(Rota(importacaoId), corpo ?? new { enviarConvite = true });

    /// <summary>O valor da coluna como texto; <c>null</c> quando o banco guarda NULL (o <c>Convert.ToString</c> de <c>DBNull</c> daria texto vazio).</summary>
    private static async Task<string?> TextoAsync(ApiDeAcesso api, int importacaoId, string coluna) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT {coluna} FROM IMPORTACOES WHERE ID_IMPORTACAO = {importacaoId}") is { } valor and not DBNull ? Convert.ToString(valor, System.Globalization.CultureInfo.InvariantCulture) : null;

    private sealed record Antes(long Funcionarios, long Fila, long EventosDePessoas);

    private static async Task<Antes> AntesAsync(ApiDeAcesso api) =>
        new(
            await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"),
            await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"),
            await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'FUNCIONARIOS'"));

    /// <summary>A recusa não deixa rastro: nenhuma pessoa nova, nenhum convite na fila, a importação continua <c>PRONTA</c> com a prévia e o arquivo, e o arquivo continua lá.</summary>
    private static async Task AssertSemEfeitoAsync(ApiDeAcesso api, Antes antes, int importacaoId, string caminho)
    {
        Assert.Equal(antes.Funcionarios, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.Equal(antes.Fila, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
        Assert.Equal(antes.EventosDePessoas, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'FUNCIONARIOS'"));
        Assert.Equal("PRONTA", await TextoAsync(api, importacaoId, "SITUACAO"));
        Assert.NotNull(await TextoAsync(api, importacaoId, "PREVIA"));
        Assert.Null(await TextoAsync(api, importacaoId, "CONFIRMADA_EM"));
        Assert.Equal(caminho, await TextoAsync(api, importacaoId, "CAMINHO_ARQUIVO"));
        Assert.True(api.Api.Armazenamento.Existe(caminho));
        Assert.DoesNotContain(caminho, api.Api.Armazenamento.Apagados);
    }

    private static async Task<(string Codigo, string Detalhe, JsonElement Corpo)> RecusaAsync(HttpResponseMessage resposta, int status)
    {
        Assert.Equal(status, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        return (corpo.GetProperty("codigo").GetString()!, corpo.GetProperty("detail").GetString()!, corpo);
    }

    // ---------------------------------------------------------------- o caminho feliz

    [Fact]
    public async Task Confirma_e_cadastra_todos_como_o_cadastro_da_tela_02()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api, comGestor: true);
        api.Api.Armazenamento.Limpar();
        var a = Valida(apoio, nome: "Tiago Nogueira", beneficios: $"{apoio.Beneficio}; {apoio.OutroBeneficio}");
        var emailA = (string)a[4]!;
        var b = Valida(apoio, admissao: "19/10/2026"); // depois de "hoje" (07/10): EM_ADMISSAO
        var emailB = (string)b[4]!;
        var (id, _) = await PreviaAsync(api, a, b);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        var corpo = await CorpoAsync(resposta);
        var funcionarios = corpo.GetProperty("funcionarios");
        Assert.Equal(2, funcionarios.GetArrayLength());
        var idA = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{emailA}'");
        var idB = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_FUNCIONARIO FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = '{emailB}'");
        Assert.Equal([idA, idB], funcionarios.EnumerateArray().Select(f => f.GetProperty("id").GetInt32())); // na ordem da planilha
        Assert.Equal(("Tiago Nogueira", apoio.Cargo, apoio.Setor, true), (funcionarios[0].GetProperty("nome").GetString(), funcionarios[0].GetProperty("cargo").GetString(), funcionarios[0].GetProperty("setor").GetString(), funcionarios[0].GetProperty("ativo").GetBoolean()));

        // o que o cadastro da tela 02 faz por pessoa
        Assert.False(string.IsNullOrWhiteSpace((string?)await Sql.EscalarAsync(api.Conexao, $"SELECT MATRICULA FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {idA}"))); // matrícula gerada
        Assert.Equal(apoio.GestorId!.Value, (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {idA}")); // o gestor do setor
        Assert.Equal(2, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {idA}"));
        Assert.Equal(0, await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_FUNCIONARIO = {idB}"));
        Assert.Equal(("11977663344", 11), ((string)(await Sql.EscalarAsync(api.Conexao, $"SELECT TELEFONE FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {idA}"))!, ((string)(await Sql.EscalarAsync(api.Conexao, $"SELECT CPF FROM FUNCIONARIOS_DETALHES WHERE ID_FUNCIONARIO = {idA}"))!).Length));
        Assert.Equal("Funcionario", await Sql.EscalarAsync(api.Conexao, $"SELECT n.NOME_NIVEL_ACESSO FROM FUNCIONARIOS f JOIN NIVEIS_DE_ACESSO n ON n.ID_NIVEL_ACESSO = f.ID_NIVEL_ACESSO WHERE f.ID_FUNCIONARIO = {idA}")); // "Colaborador" da planilha
        var fichaA = await CorpoAsync(await ComoRh(api).GetAsync($"/api/rh/funcionarios/{idA}"));
        var fichaB = await CorpoAsync(await ComoRh(api).GetAsync($"/api/rh/funcionarios/{idB}"));
        Assert.Equal(("ATIVO", "EM_ADMISSAO"), (fichaA.GetProperty("situacao").GetString(), fichaB.GetProperty("situacao").GetString())); // pela data de admissão de cada linha
    }

    [Theory]
    [InlineData(true)]
    [InlineData(false)]
    public async Task Resultado_conta_criados_convites_onboarding_e_formulario(bool enviarConvite)
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio), Valida(apoio), Valida(apoio));
        var filaAntes = await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE'");

        var resposta = await ConfirmarAsync(ComoRh(api), id, new { enviarConvite });

        Assert.Equal(200, (int)resposta.StatusCode);
        var corpo = await CorpoAsync(resposta);
        Assert.Equal(("CONFIRMADA", "novos-colaboradores.xlsx", 3), (corpo.GetProperty("situacao").GetString(), corpo.GetProperty("nomeArquivo").GetString(), corpo.GetProperty("totalCriados").GetInt32()));
        Assert.Equal((id, enviarConvite ? 3 : 0, 0, 0, "2026-10-07T12:42:00Z"), (corpo.GetProperty("importacaoId").GetInt32(), corpo.GetProperty("convitesEnviados").GetInt32(), corpo.GetProperty("incluidosNoOnboarding").GetInt32(), corpo.GetProperty("incluidosNoFormularioDeAdmissao").GetInt32(), corpo.GetProperty("confirmadaEmUtc").GetString()));
        Assert.Equal(filaAntes + (enviarConvite ? 3 : 0), await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS WHERE TIPO = 'CONVITE'"));
        Assert.Equal(enviarConvite ? "3" : "0", await TextoAsync(api, id, "CONVITES_ENVIADOS"));
    }

    [Fact]
    public async Task Importacao_vira_CONFIRMADA_sem_previa_e_sem_caminho_com_os_totais_e_o_arquivo_e_apagado()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));

        var resposta = await ConfirmarAsync(ComoRh(api), id, new { enviarConvite = false });

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("CONFIRMADA", await TextoAsync(api, id, "SITUACAO"));
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT PREVIA FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"));
        Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"));
        Assert.Equal(("2", "2", "2", "0", "0", "0"), (await TextoAsync(api, id, "TOTAL_LINHAS"), await TextoAsync(api, id, "TOTAL_CRIADOS"), await TextoAsync(api, id, "LINHAS_VALIDAS"), await TextoAsync(api, id, "CONVITES_ENVIADOS"), await TextoAsync(api, id, "INCLUIDOS_NO_ONBOARDING"), await TextoAsync(api, id, "INCLUIDOS_NO_FORMULARIO_ADMISSAO")));
        Assert.Equal(ElencoDeTeste.Hoje, DateTime.SpecifyKind((DateTime)(await Sql.EscalarAsync(api.Conexao, $"SELECT CONFIRMADA_EM FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"))!, DateTimeKind.Utc));
        Assert.False(api.Api.Armazenamento.Existe(caminho)); // o arquivo guardado é apagado
        Assert.Equal([caminho], api.Api.Armazenamento.Apagados);
    }

    [Fact]
    public async Task Grava_um_unico_evento_Importou_com_os_totais_e_nenhum_evento_por_pessoa()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var nome = $"Zeferino Xavante {apoio.Sufixo}";
        var linha = Valida(apoio, nome: nome);
        var (id, _) = await PreviaAsync(api, linha, Valida(apoio));
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        Assert.Equal(200, (int)resposta.StatusCode);
        var eventos = await AuditoriaDeTeste.EventosAsync(api.Conexao, "IMPORTACOES", id);
        Assert.Equal(2, eventos.Count); // o da prévia e o do resultado
        var resultado = Assert.Single(eventos, e => e.Descricao.StartsWith("Confirmou", StringComparison.Ordinal));
        Assert.Equal((AuditoriaArea.Importacao, AuditoriaAcao.Importou, $"Importação {id}"), (resultado.Area, resultado.Acao, resultado.RegistroAfetado));
        Assert.Equal("Confirmou a importação (2 colaboradores cadastrados, 2 convites)", resultado.Descricao);
        Assert.Equal(api.Elenco.Rh.FuncionarioId, resultado.FuncionarioAtorId);
        Assert.Equal(antes.EventosDePessoas, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE = 'FUNCIONARIOS'")); // nenhum evento por pessoa
        // Os eventos da IMPORTAÇÃO levam só os totais: nem nome, nem CPF, nem e-mail, nem o nome do arquivo. (Cada convite tem o seu evento de ACESSO, "Login criado e convite enviado", do serviço de convite, o mesmo do cadastro da tela.)
        foreach (var evento in eventos)
        {
            foreach (var segredo in new[] { nome, (string)linha[1]!, (string)linha[4]!, "novos-colaboradores" })
            {
                Assert.DoesNotContain(segredo, evento.Descricao + '|' + evento.RegistroAfetado);
            }
        }
    }

    [Fact]
    public async Task Quem_confirma_pode_ser_outro_RH()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio));
        var outro = await PessoasDeTeste.ComLoginAsync(api, $"Outro RH {apoio.Sufixo}", Perfil.RH);

        var resposta = await ConfirmarAsync(await PessoasDeTeste.ClienteAsync(api, outro), id);

        Assert.Equal(200, (int)resposta.StatusCode);
        Assert.Equal("CONFIRMADA", await TextoAsync(api, id, "SITUACAO"));
        Assert.Equal(api.Elenco.Rh.FuncionarioId.ToString(), await TextoAsync(api, id, "ID_FUNCIONARIO_RH")); // continua o de quem enviou a planilha
        var resultado = Assert.Single((await AuditoriaDeTeste.EventosAsync(api.Conexao, "IMPORTACOES", id)), e => e.Descricao.StartsWith("Confirmou", StringComparison.Ordinal));
        Assert.Equal(outro.FuncionarioId, resultado.FuncionarioAtorId);
    }

    // ---------------------------------------------------------------- recusas sem efeito

    [Fact]
    public async Task Id_que_nao_existe_da_404()
    {
        var api = await ApiAsync();

        var resposta = await ConfirmarAsync(ComoRh(api), 987_654_321);

        Assert.Equal(404, (int)resposta.StatusCode);
        Assert.Equal(404, (await CorpoAsync(resposta)).GetProperty("status").GetInt32()); // o 404 do caso de uso (com corpo), não o de rota inexistente
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
    }

    [Fact]
    public async Task Segunda_confirmacao_da_409_IMPORTACAO_JA_CONFIRMADA_e_nao_cadastra_de_novo()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        Assert.Equal(200, (int)(await ConfirmarAsync(ComoRh(api), id)).StatusCode);
        var depois = await AntesAsync(api);

        var segunda = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(segunda, 409);
        Assert.Equal(("IMPORTACAO_JA_CONFIRMADA", "Esta importação já foi confirmada.", "Conflito."), (codigo, detalhe, corpo.GetProperty("title").GetString()));
        await segunda.AfirmarConformeAoContratoAsync("confirmarImportacao");
        Assert.Equal(depois.Funcionarios, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.Equal(depois.Fila, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
    }

    [Fact]
    public async Task Importacao_vencida_da_422_IMPORTACAO_EXPIRADA_a_partir_de_exatamente_1_hora()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, Valida(apoio));
        var antes = await AntesAsync(api);
        try
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(1)); // 13:42:00, o instante exato do vencimento: já venceu
            var resposta = await ConfirmarAsync(await api.ClienteDoRhAgoraAsync(), id);

            var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
            Assert.Equal(("IMPORTACAO_EXPIRADA", "A prévia venceu. Envie a planilha de novo.", "Operação recusada."), (codigo, detalhe, corpo.GetProperty("title").GetString()));
            await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
            await AssertSemEfeitoAsync(api, antes, id, caminho);
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Um_segundo_antes_do_vencimento_ainda_confirma()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio));
        try
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(1).AddSeconds(-1)); // 13:41:59
            var resposta = await ConfirmarAsync(await api.ClienteDoRhAgoraAsync(), id);

            Assert.Equal(200, (int)resposta.StatusCode);
            Assert.Equal("2026-10-07T13:41:59Z", (await CorpoAsync(resposta)).GetProperty("confirmadaEmUtc").GetString());
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Previa_ja_limpa_de_importacao_nao_vencida_da_422_IMPORTACAO_EXPIRADA()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE IMPORTACOES SET PREVIA = NULL WHERE ID_IMPORTACAO = {id}"); // a limpeza já passou por ela

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        Assert.Equal("IMPORTACAO_EXPIRADA", (await RecusaAsync(resposta, 422)).Codigo);
    }

    [Fact]
    public async Task Importacao_com_erros_da_422_IMPORTACAO_COM_ERROS()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio, cpf: "123.456.789-00"));
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_COM_ERROS", "A planilha ainda tem linhas com erro. Corrija e envie de novo.", "Operação recusada."), (codigo, detalhe, corpo.GetProperty("title").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        Assert.Equal("COM_ERROS", await TextoAsync(api, id, "SITUACAO"));
        Assert.Equal(antes.Funcionarios, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS"));
        Assert.True(api.Api.Armazenamento.Existe(caminho));
    }

    [Fact]
    public async Task Ordem_409_antes_de_422_expirada_antes_de_422_com_erros()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (comErros, _) = await PreviaAsync(api, Valida(apoio, cpf: "123.456.789-00"));
        var (confirmada, _) = await PreviaAsync(api, Valida(apoio));
        Assert.Equal(200, (int)(await ConfirmarAsync(ComoRh(api), confirmada)).StatusCode);
        try
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje.AddHours(2));
            var cliente = await api.ClienteDoRhAgoraAsync();

            var jaConfirmadaEVencida = await RecusaAsync(await ConfirmarAsync(cliente, confirmada), 409); // confirmada E vencida: 409
            var comErrosEVencida = await RecusaAsync(await ConfirmarAsync(cliente, comErros), 422); // com erros E vencida: vencida

            Assert.Equal("IMPORTACAO_JA_CONFIRMADA", jaConfirmadaEVencida.Codigo);
            Assert.Equal("IMPORTACAO_EXPIRADA", comErrosEVencida.Codigo);
        }
        finally
        {
            api.Api.Relogio.Fixar(ElencoDeTeste.Hoje);
        }
    }

    [Fact]
    public async Task Corpo_sem_enviarConvite_da_400()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio));
        var antes = await AntesAsync(api);

        var resposta = await ComoRh(api).PostAsJsonAsync(Rota(id), new { incluirNoOnboarding = false });

        Assert.Equal(400, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Funcionario_e_Gestor_recebem_403_e_sem_token_401()
    {
        var api = await ApiAsync();

        foreach (var quem in new[] { api.Elenco.Gestor, api.Elenco.MembroA })
        {
            Assert.Equal(403, (int)(await ConfirmarAsync(api.Api.ClienteComo(quem), 1)).StatusCode);
        }

        Assert.Equal(401, (int)(await ConfirmarAsync(api.Api.ClienteAnonimo(), 1)).StatusCode);
    }

    // ---------------------------------------------------------------- conflito depois da prévia

    [Fact]
    public async Task CPF_cadastrado_depois_da_previa_da_422_IMPORTACAO_LINHA_RECUSADA_com_a_linha_certa()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio, cpf: cpf), Valida(apoio));
        var existente = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Chegou depois {apoio.Sufixo}", $"depois.{apoio.Sufixo}@existentes.teste.com", new DateOnly(2020, 1, 6));
        await SementeDoCadastro.DetalhesAsync(api.Conexao, existente, cpf);
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal("IMPORTACAO_LINHA_RECUSADA", codigo);
        Assert.Equal("A linha 3 foi recusada: já existe um colaborador com este CPF. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", detalhe);
        Assert.Equal((3, "Operação recusada."), (corpo.GetProperty("linha").GetInt32(), corpo.GetProperty("title").GetString()));
        Assert.False(corpo.TryGetProperty("regraDoBanco", out _)); // só quando o banco recusou
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Email_cadastrado_depois_da_previa_da_422_na_linha_dele()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var email = $"repetido.{apoio.Sufixo}@{apoio.Dominio}";
        var (id, caminho) = await PreviaAsync(api, Valida(apoio, email: email), Valida(apoio));
        await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Chegou depois {apoio.Sufixo}", email.ToUpperInvariant(), new DateOnly(2020, 1, 6));
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", "A linha 2 foi recusada: já existe um colaborador com este e-mail. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", 2), (codigo, detalhe, corpo.GetProperty("linha").GetInt32()));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Cargo_apagado_depois_da_previa_da_422_na_linha_dele()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        await Sql.ExecutarAsync(api.Conexao, $"DELETE FROM CARGOS WHERE NOME_CARGO = '{apoio.Cargo}'");
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", $"A linha 2 foi recusada: o cargo \"{apoio.Cargo}\" não existe. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", 2), (codigo, detalhe, corpo.GetProperty("linha").GetInt32()));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Gestor_do_setor_ficou_inativo_depois_da_previa_da_422_na_linha_dele()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api, comGestor: true);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {apoio.GestorFuncionarioId}");
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", "A linha 2 foi recusada: o gestor do setor escolhido está inativo. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", 2), (codigo, detalhe, corpo.GetProperty("linha").GetInt32()));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Trigger_ou_banco_recusando_a_linha_500_nao_deixa_ninguem()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, [.. Enumerable.Range(0, 500).Select(_ => Valida(apoio))]);
        var antes = await AntesAsync(api);
        await using var fabrica = api.Api.ComFalhaNoCadastroDaLinha(499, "regra-simulada-do-banco-9999"); // a 500ª pessoa é a linha 501 do Excel

        var resposta = await ConfirmarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal("IMPORTACAO_LINHA_RECUSADA", codigo);
        Assert.Equal("A linha 501 foi recusada: uma regra do banco de dados recusou a linha. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", detalhe);
        Assert.Equal((501, "regra-simulada-do-banco-9999"), (corpo.GetProperty("linha").GetInt32(), corpo.GetProperty("regraDoBanco").GetString()));
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        await AssertSemEfeitoAsync(api, antes, id, caminho); // as 500 pessoas que o cadastro chegou a inserir, e a reivindicação, foram desfeitas
    }

    [Fact]
    public async Task Recusa_do_banco_por_gestor_inativo_diz_o_motivo_certo_e_a_regra_do_banco()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        var antes = await AntesAsync(api);
        await using var fabrica = api.Api.ComFalhaNoCadastroDaLinha(0, ProblemasDoCadastro.TextoDaRegraDoGestorInativo); // o trigger recusa a primeira pessoa

        var resposta = await ConfirmarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", "A linha 2 foi recusada: o gestor do setor escolhido está inativo. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", ProblemasDoCadastro.TextoDaRegraDoGestorInativo), (codigo, detalhe, corpo.GetProperty("regraDoBanco").GetString()));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Chave_unica_ocupada_no_meio_da_gravacao_diz_qual_dado_se_repetiu_na_linha_certa()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio), Valida(apoio));
        var antes = await AntesAsync(api);
        var repetido = new ProblemaDeCadastro(1, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", "Já existe um colaborador com este CPF.", new CadastroExistente(1, "Alguém", true));
        var deOutraLinha = new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", "Já existe um colaborador com este e-mail corporativo.", new CadastroExistente(2, "Outro alguém", true));
        await using var fabrica = api.Api.ComFalhaNoCadastroDaLinha(1, null, porDuplicidade: true, revalidacao: [deOutraLinha, repetido]); // o 1062 da segunda pessoa (linha 3); a linha 2 também repete um dado, mas não foi ela que o banco recusou

        var resposta = await ConfirmarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", "A linha 3 foi recusada: já existe um colaborador com este CPF. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", 3), (codigo, detalhe, corpo.GetProperty("linha").GetInt32()));
        Assert.False(corpo.TryGetProperty("regraDoBanco", out _));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Chave_unica_ocupada_que_a_revalidacao_nao_acha_da_o_motivo_generico()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        var antes = await AntesAsync(api);
        await using var fabrica = api.Api.ComFalhaNoCadastroDaLinha(0, null, porDuplicidade: true, revalidacao: []);

        var resposta = await ConfirmarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), id);

        var (_, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal("A linha 2 foi recusada: já existe um colaborador com estes dados. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", detalhe);
        Assert.False(corpo.TryGetProperty("regraDoBanco", out _));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task Com_varios_problemas_a_recusa_aponta_a_primeira_linha_e_nao_o_primeiro_tipo_de_problema()
    {
        // A linha 2 tem o gestor do setor que ficou inativo (regra); a linha 3 tem um CPF que alguém cadastrou (duplicidade). O cadastro lista as duplicidades antes das regras, mas a recusa é da linha 2.
        var api = await ApiAsync();
        var comGestor = await ApoioAsync(api, comGestor: true);
        var semGestor = await ApoioAsync(api);
        var cpf = CpfsDeTeste.Valido(Interlocked.Increment(ref _proximo));
        var (id, caminho) = await PreviaAsync(api, Valida(comGestor), Valida(semGestor, cpf: cpf));
        var existente = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Chegou depois {semGestor.Sufixo}", $"depois.{semGestor.Sufixo}@existentes.teste.com", new DateOnly(2020, 1, 6));
        await SementeDoCadastro.DetalhesAsync(api.Conexao, existente, cpf);
        await Sql.ExecutarAsync(api.Conexao, $"UPDATE FUNCIONARIOS SET ATIVO = FALSE, DATA_DESLIGAMENTO = '2026-09-30' WHERE ID_FUNCIONARIO = {comGestor.GestorFuncionarioId}");
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id);

        var (codigo, detalhe, corpo) = await RecusaAsync(resposta, 422);
        Assert.Equal(("IMPORTACAO_LINHA_RECUSADA", "A linha 2 foi recusada: o gestor do setor escolhido está inativo. Nenhum colaborador foi cadastrado. Envie a planilha de novo.", 2), (codigo, detalhe, corpo.GetProperty("linha").GetInt32()));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    // ---------------------------------------------------------------- as caixas "depois de salvar"

    [Fact]
    public async Task incluirNoOnboarding_true_da_422_SEM_TREINAMENTO_DE_ONBOARDING_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio));
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id, new { enviarConvite = true, incluirNoOnboarding = true });

        Assert.Equal("SEM_TREINAMENTO_DE_ONBOARDING", (await RecusaAsync(resposta, 422)).Codigo);
        await resposta.AfirmarConformeAoContratoAsync("confirmarImportacao");
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    [Fact]
    public async Task formularioAdmissaoId_informado_da_400_em_formularioAdmissaoId_e_nada_e_gravado()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, caminho) = await PreviaAsync(api, Valida(apoio));
        var antes = await AntesAsync(api);

        var resposta = await ConfirmarAsync(ComoRh(api), id, new { enviarConvite = true, formularioAdmissaoId = 11 });

        Assert.True((await ErrosAsync(resposta)).TryGetProperty("formularioAdmissaoId", out _));
        await AssertSemEfeitoAsync(api, antes, id, caminho);
    }

    // ---------------------------------------------------------------- concorrência e arquivo

    [Fact]
    public async Task Duas_confirmacoes_simultaneas_da_mesma_importacao_dao_um_200_e_um_409_e_exatamente_N_colaboradores()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        var (id, _) = await PreviaAsync(api, Valida(apoio), Valida(apoio), Valida(apoio));
        var antes = await AntesAsync(api);
        var cliente = ComoRh(api);

        var respostas = await EmSimultaneoAsync(2, _ => ConfirmarAsync(cliente, id));

        Assert.Equal([200, 409], respostas.Select(r => (int)r.StatusCode).Order());
        Assert.Equal(antes.Funcionarios + 3, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FUNCIONARIOS")); // exatamente N, nunca 2N
        Assert.Equal(antes.Fila + 3, await Sql.ContarAsync(api.Conexao, "SELECT COUNT(*) FROM FILA_EMAILS"));
        Assert.Equal("CONFIRMADA", await TextoAsync(api, id, "SITUACAO"));
    }

    [Fact]
    public async Task Falha_ao_apagar_o_blob_depois_do_commit_nao_desfaz_a_importacao()
    {
        var api = await ApiAsync();
        var apoio = await ApoioAsync(api);
        api.Api.Armazenamento.Limpar();
        var (id, caminho) = await PreviaAsync(api, Valida(apoio), Valida(apoio));
        var logs = new ColetorDeLogs();
        await using var fabrica = api.Api.WithWebHostBuilder(b => b.ConfigureLogging(l => l.AddProvider(logs)));
        api.Api.Armazenamento.FalharNaExclusao = new IOException("exclusao-do-blob-falhou-8888");
        try
        {
            var resposta = await ConfirmarAsync(api.Api.ClienteComo(fabrica, api.Elenco.Rh), id);

            Assert.Equal(200, (int)resposta.StatusCode); // o commit já aconteceu: o RH recebe o resultado
            Assert.Equal("CONFIRMADA", await TextoAsync(api, id, "SITUACAO"));
            Assert.True(api.Api.Armazenamento.Existe(caminho)); // sobrou: a LIMPAR_ARQUIVOS_ORFAOS o apaga (nenhuma linha o referencia mais)
            Assert.Null(await Sql.EscalarAsync(api.Conexao, $"SELECT CAMINHO_ARQUIVO FROM IMPORTACOES WHERE ID_IMPORTACAO = {id}"));
            string texto;
            lock (logs.Mensagens)
            {
                texto = string.Join('\n', logs.Mensagens);
            }

            Assert.DoesNotContain("exclusao-do-blob-falhou-8888", texto); // só o tipo da exceção vai para o log
            Assert.Contains("Não foi possível apagar o arquivo de uma importação", texto);
        }
        finally
        {
            api.Api.Armazenamento.FalharNaExclusao = null;
            api.Api.Armazenamento.Limpar();
        }
    }
}
