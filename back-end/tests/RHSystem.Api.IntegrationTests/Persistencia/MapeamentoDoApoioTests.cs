using System.Globalization;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Documentos;
using RHSystem.Domain.Empresa;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Api.IntegrationTests.Persistencia;

/// <summary>
/// Ida e volta do que a fatia 1c mapeia (cargo com descrição, setor, gestor com ausência, configuração da empresa e documento), no banco REAL (MySQL em contêiner com os scripts), relendo com OUTRO contexto.
/// Um banco para a classe inteira; cada teste usa nomes próprios. A configuração da empresa é linha única: quem a usa a apaga antes.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class MapeamentoDoApoioTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private Task<string> BancoAsync() => BancoCompartilhado.ObterAsync(mysql, nameof(MapeamentoDoApoioTests));

    private static Task<int> PessoaAsync(string conexao, string nome, Perfil perfil = Perfil.Funcionario) => ApoioDeTeste.CriarPessoaAsync(conexao, nome, perfil);

    // NULL do banco volta nulo (Convert.ToString(null) devolveria ""): os testes distinguem coluna vazia de coluna nula.
    private static async Task<string?> TextoAsync(string conexao, string sql) =>
        await ApoioDeTeste.EscalarAsync(conexao, sql) is { } valor ? Convert.ToString(valor, CultureInfo.InvariantCulture) : null;

    private static async Task<MySqlException> ErroDoMySqlAsync(string conexao, string sql)
    {
        var erro = await Assert.ThrowsAnyAsync<MySqlException>(() => ApoioDeTeste.ExecutarAsync(conexao, sql));
        return erro;
    }

    [Fact]
    public async Task Cargo_ida_e_volta_com_carga_decimal_e_com_carga_nula_lida_por_SQL()
    {
        var conexao = await BancoAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.Cargos.Add(Cargo.Criar("Analista de apoio", "Cuida do apoio", 44.5m));
            await escrita.SaveChangesAsync();
        }

        var semCarga = await ApoioDeTeste.CriarCargoAsync(conexao, "Cargo sem carga apoio", null, null); // o banco aceita NULL; o contrato não: só por SQL direto

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = await leitura.Cargos.AsNoTracking().SingleAsync(c => c.Nome == "Analista de apoio");
        Assert.Equal(("Cuida do apoio", (decimal?)44.5m), (lido.Descricao, lido.CargaSemanalHoras));
        var nulo = await leitura.Cargos.AsNoTracking().SingleAsync(c => c.Id == semCarga);
        Assert.Null(nulo.Descricao);
        Assert.Null(nulo.CargaSemanalHoras);
    }

    [Fact]
    public async Task Setor_ida_e_volta_com_descricao_e_gestor()
    {
        var conexao = await BancoAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(conexao, "Gestor do setor apoio", Perfil.Gestor);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(conexao, pessoa);
        int setorId;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var setor = Setor.Criar("Compras apoio", "Compras e suprimentos");
            escrita.Setores.Add(setor);
            await escrita.SaveChangesAsync();
            setor.DefinirGestor(gestorId);
            await escrita.SaveChangesAsync();
            setorId = setor.Id;
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = await leitura.Setores.AsNoTracking().Include(s => s.Gestor).ThenInclude(g => g!.Funcionario).SingleAsync(s => s.Id == setorId);
        Assert.Equal(("Compras apoio", "Compras e suprimentos", (int?)gestorId, "Gestor do setor apoio"), (lido.Nome, lido.Descricao, lido.GestorId, lido.Gestor!.Funcionario.Nome));
    }

    [Fact]
    public async Task Gestor_grava_Ausente_true_e_false_como_enviados_e_a_data_de_volta_e_o_CHECK_do_V001_recusa_data_sem_marca()
    {
        var conexao = await BancoAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(conexao, "Gestora ausente apoio", Perfil.Gestor);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(conexao, pessoa);

        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var gestor = await escrita.Gestores.SingleAsync(g => g.Id == gestorId);
            gestor.MarcarAusente(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 7));
            await escrita.SaveChangesAsync();
        }

        Assert.Equal(("1", "2026-10-19"), (await TextoAsync(conexao, $"SELECT CAST(APROVACAO_AUTOMATICA AS SIGNED) FROM GESTORES WHERE ID_GESTOR = {gestorId}"), await TextoAsync(conexao, $"SELECT DATE_FORMAT(APROVACAO_AUTOMATICA_ATE, '%Y-%m-%d') FROM GESTORES WHERE ID_GESTOR = {gestorId}")));

        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var gestor = await escrita.Gestores.SingleAsync(g => g.Id == gestorId);
            gestor.DesmarcarAusencia();
            await escrita.SaveChangesAsync();
        }

        Assert.Equal(("0", (string?)null), (await TextoAsync(conexao, $"SELECT CAST(APROVACAO_AUTOMATICA AS SIGNED) FROM GESTORES WHERE ID_GESTOR = {gestorId}"), await TextoAsync(conexao, $"SELECT APROVACAO_AUTOMATICA_ATE FROM GESTORES WHERE ID_GESTOR = {gestorId}")));

        // Um gestor novo, gravado pelo EF, nasce com a marca DESLIGADA enviada (o padrão do banco não é declarado).
        var outra = await ApoioDeTeste.CriarPessoaAsync(conexao, "Gestor novo apoio", Perfil.Gestor);
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var funcionario = await escrita.Funcionarios.SingleAsync(f => f.Id == outra);
            escrita.Gestores.Add(Gestor.Criar(funcionario));
            await escrita.SaveChangesAsync();
        }

        Assert.Equal("0", await TextoAsync(conexao, $"SELECT CAST(APROVACAO_AUTOMATICA AS SIGNED) FROM GESTORES WHERE ID_FUNCIONARIO = {outra}"));

        // O CHECK do V001: data de volta sem a marca ligada é recusada pelo banco.
        var erro = await ErroDoMySqlAsync(conexao, $"UPDATE GESTORES SET APROVACAO_AUTOMATICA_ATE = '2026-10-19' WHERE ID_GESTOR = {gestorId}");
        Assert.Contains("CHK_GESTORES_APROVACAO_AUTOMATICA_ATE", erro.Message);
    }

    [Fact]
    public async Task ConfiguracaoDaEmpresa_ida_e_volta_com_TIME_smallint_e_tinyint_e_so_aceita_o_Id_1()
    {
        var conexao = await BancoAsync();
        await ApoioDeTeste.ExecutarAsync(conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.ConfiguracoesDaEmpresa.Add(ConfiguracaoDaEmpresa.Criar("Empresa Exemplo", "SP", "São Paulo", new TimeOnly(8, 30), new TimeOnly(17, 45), 15, 5));
            await escrita.SaveChangesAsync();
        }

        await using (var leitura = ContextoDeTeste.Novo(conexao))
        {
            var lida = await leitura.ConfiguracoesDaEmpresa.AsNoTracking().SingleAsync();
            Assert.Equal(((byte)1, "Empresa Exemplo", "SP", "São Paulo", new TimeOnly(8, 30), new TimeOnly(17, 45), 15, 5), (lida.Id, lida.NomeEmpresa, lida.Uf, lida.Municipio, lida.HorarioEntrada, lida.HorarioSaida, lida.ToleranciaAtrasoMinutos, lida.DiaPagamento));
        }

        var segundoComId1 = await ErroDoMySqlAsync(conexao, "INSERT INTO CONFIGURACOES_EMPRESA (ID_CONFIGURACAO_EMPRESA, NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, TOLERANCIA_ATRASO_MINUTOS, DIA_PAGAMENTO) VALUES (1, 'Outra', 'SP', 'Cidade', '08:00', '17:00', 0, 5)");
        Assert.Equal(1062, segundoComId1.Number); // a linha única: um segundo INSERT com o mesmo id é chave repetida
        var comId2 = await ErroDoMySqlAsync(conexao, "INSERT INTO CONFIGURACOES_EMPRESA (ID_CONFIGURACAO_EMPRESA, NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, TOLERANCIA_ATRASO_MINUTOS, DIA_PAGAMENTO) VALUES (2, 'Outra', 'SP', 'Cidade', '08:00', '17:00', 0, 5)");
        Assert.Contains("CHK_CONFIGURACOES_EMPRESA_LINHA_UNICA", comId2.Message);
    }

    [Fact]
    public async Task ConfiguracaoDaEmpresa_UF_minuscula_viola_o_CHECK_do_banco_e_horario_de_virada_da_noite_grava()
    {
        var conexao = await BancoAsync();
        await ApoioDeTeste.ExecutarAsync(conexao, "DELETE FROM CONFIGURACOES_EMPRESA");

        var erro = await ErroDoMySqlAsync(conexao, "INSERT INTO CONFIGURACOES_EMPRESA (NOME_EMPRESA, UF, MUNICIPIO, HORARIO_ENTRADA, HORARIO_SAIDA, TOLERANCIA_ATRASO_MINUTOS, DIA_PAGAMENTO) VALUES ('Empresa', 'sp', 'Cidade', '08:00', '17:00', 0, 5)");
        Assert.Contains("CHK_CONFIGURACOES_EMPRESA_UF", erro.Message);

        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.ConfiguracoesDaEmpresa.Add(ConfiguracaoDaEmpresa.Criar("Empresa noturna", "SP", "Cidade", new TimeOnly(22, 0), new TimeOnly(6, 0), 10, 5));
            await escrita.SaveChangesAsync();
        }

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lida = await leitura.ConfiguracoesDaEmpresa.AsNoTracking().SingleAsync();
        Assert.Equal((new TimeOnly(22, 0), new TimeOnly(6, 0)), (lida.HorarioEntrada, lida.HorarioSaida)); // o turno que atravessa a meia-noite grava e volta igual
    }

    [Fact]
    public async Task Documento_ida_e_volta_com_DATA_CADASTRO_do_banco_e_datas_gravadas_tres_horas_antes()
    {
        var conexao = await BancoAsync();
        var pessoa = await PessoaAsync(conexao, "Pessoa documento ida e volta");
        int id;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var funcionario = await escrita.Funcionarios.SingleAsync(f => f.Id == pessoa);
            var documento = DocumentoDoColaborador.Anexar(funcionario, TipoDeDocumento.CarteiraDeTrabalho, "Digital", new DateOnly(2030, 1, 1), "documentos/ida-e-volta/a.pdf", "carteira.pdf", Agora);
            escrita.Documentos.Add(documento);
            await escrita.SaveChangesAsync();
            id = documento.Id;
        }

        Assert.Equal(("CARTEIRA_DE_TRABALHO", "ENVIADO", "2026-10-07 09:42:00"), (await TextoAsync(conexao, $"SELECT TIPO_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}"), await TextoAsync(conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}"), await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_ENVIO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}"))); // a coluna do grupo guarda -03:00
        Assert.Null(await TextoAsync(conexao, $"SELECT DATA_PEDIDO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}"));
        Assert.Equal("1", await TextoAsync(conexao, $"SELECT TIMESTAMPDIFF(SECOND, DATA_CADASTRO, NOW()) BETWEEN -5 AND 5 FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {id}")); // DATA_CADASTRO é do banco

        await using var leitura = ContextoDeTeste.Novo(conexao);
        var lido = await leitura.Documentos.AsNoTracking().SingleAsync(d => d.Id == id);
        Assert.Equal((TipoDeDocumento.CarteiraDeTrabalho, "Digital", (DateOnly?)new DateOnly(2030, 1, 1), SituacaoDoDocumento.Enviado, "documentos/ida-e-volta/a.pdf", "carteira.pdf"), (lido.Tipo, lido.Descricao, lido.DataValidade, lido.Situacao, lido.CaminhoArquivo, lido.NomeArquivoOriginal));
        Assert.Equal((Agora, DateTimeKind.Utc), (lido.EnviadoEmUtc!.Value, lido.EnviadoEmUtc.Value.Kind)); // lido de volta em UTC
        Assert.Equal(DateTimeKind.Utc, lido.CadastradoEm.Kind);
        Assert.InRange(Math.Abs((lido.CadastradoEm - DateTime.UtcNow).TotalSeconds), 0, 60 * 60 * 3 + 30); // DATA_CADASTRO é do relógio do banco, não do relógio falso
    }

    [Fact]
    public async Task Documento_Pendente_e_Enviado_gravam_STATUS_como_enviados()
    {
        var conexao = await BancoAsync();
        var pessoa = await PessoaAsync(conexao, "Pessoa documento status");
        int pendente;
        int enviado;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var funcionario = await escrita.Funcionarios.SingleAsync(f => f.Id == pessoa);
            var pedido = DocumentoDoColaborador.Pedir(funcionario, TipoDeDocumento.Identidade, null, null, Agora);
            var anexado = DocumentoDoColaborador.Anexar(funcionario, TipoDeDocumento.Cpf, null, null, "documentos/status/b.pdf", "b.pdf", Agora);
            escrita.Documentos.AddRange(pedido, anexado);
            await escrita.SaveChangesAsync();
            (pendente, enviado) = (pedido.Id, anexado.Id);
        }

        Assert.Equal(("PENDENTE", "2026-10-07 09:42:00"), (await TextoAsync(conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"), await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}")));
        Assert.Equal("ENVIADO", await TextoAsync(conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {enviado}"));

        // O pedido recebe o arquivo: o UPDATE leva o STATUS como token de concorrência (a Task 8 conta com isso).
        await using var segunda = ContextoDeTeste.Novo(conexao);
        var doBanco = await segunda.Documentos.SingleAsync(d => d.Id == pendente);
        doBanco.ReceberArquivo("documentos/status/c.pdf", "c.pdf", Agora.AddHours(2));
        await segunda.SaveChangesAsync();
        Assert.Equal(("ENVIADO", "2026-10-07 11:42:00", "2026-10-07 09:42:00"), (await TextoAsync(conexao, $"SELECT STATUS_DOCUMENTO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"), await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_ENVIO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}"), await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pendente}")));
    }

    [Fact]
    public async Task Documento_so_recebe_o_arquivo_uma_vez_quando_dois_contextos_disputam_o_mesmo_pedido()
    {
        var conexao = await BancoAsync();
        var pessoa = await PessoaAsync(conexao, "Pessoa documento disputa");
        var pedido = await ApoioDeTeste.CriarDocumentoAsync(conexao, pessoa, "OUTRO", "PENDENTE", "Diploma", pedidoEm: new DateTime(2026, 10, 1, 9, 0, 0));

        await using var primeiro = ContextoDeTeste.Novo(conexao);
        await using var segundo = ContextoDeTeste.Novo(conexao);
        var a = await primeiro.Documentos.SingleAsync(d => d.Id == pedido);
        var b = await segundo.Documentos.SingleAsync(d => d.Id == pedido);
        a.ReceberArquivo("documentos/disputa/a.pdf", "a.pdf", Agora);
        b.ReceberArquivo("documentos/disputa/b.pdf", "b.pdf", Agora);

        await primeiro.SaveChangesAsync();
        await Assert.ThrowsAsync<DbUpdateConcurrencyException>(() => segundo.SaveChangesAsync()); // quem chegou depois: o UPDATE leva WHERE STATUS_DOCUMENTO = 'PENDENTE' e não acha a linha
        Assert.Equal("documentos/disputa/a.pdf", await TextoAsync(conexao, $"SELECT CAMINHO_ARQUIVO FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {pedido}"));
    }

    [Fact]
    public async Task Documento_ENVIADO_sem_arquivo_viola_o_CHECK_do_V001()
    {
        var conexao = await BancoAsync();
        var pessoa = await PessoaAsync(conexao, "Pessoa documento check");

        var erro = await Assert.ThrowsAnyAsync<MySqlException>(() => ApoioDeTeste.CriarDocumentoAsync(conexao, pessoa, "CPF", "ENVIADO"));

        Assert.Contains("CHK_FUNCIONARIOS_DOCUMENTOS_ENVIADO_COM_ARQUIVO", erro.Message);
    }

    [Fact]
    public async Task Instante_com_fracao_de_segundo_e_gravado_truncado_e_o_MySQL_arredondaria_o_que_nao_for_truncado()
    {
        var conexao = await BancoAsync();
        var pessoa = await PessoaAsync(conexao, "Pessoa documento fracao");

        // O que a entidade grava é truncado ao segundo antes de chegar ao banco.
        int truncado;
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            var funcionario = await escrita.Funcionarios.SingleAsync(f => f.Id == pessoa);
            var documento = DocumentoDoColaborador.Pedir(funcionario, TipoDeDocumento.Outro, null, null, Agora.AddMilliseconds(700));
            escrita.Documentos.Add(documento);
            await escrita.SaveChangesAsync();
            truncado = documento.Id;
        }

        Assert.Equal("2026-10-07 09:42:00", await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {truncado}"));

        // E a suposição que justifica truncar: o MySQL ARREDONDA a fração quando a coluna não tem fração (grava .700 por SQL cru e lê o segundo seguinte).
        var cru = await ApoioDeTeste.CriarDocumentoAsync(conexao, pessoa, "OUTRO", "PENDENTE", pedidoEm: DateTime.Parse("2026-10-07 09:42:00.700", CultureInfo.InvariantCulture));
        Assert.Equal("2026-10-07 09:42:01", await TextoAsync(conexao, $"SELECT DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s') FROM FUNCIONARIOS_DOCUMENTOS WHERE ID_FUNCIONARIO_DOCUMENTO = {cru}"));
    }

    [Fact]
    public async Task ErrosDoBanco_reconhece_1062_e_1451_dentro_de_DbUpdateException()
    {
        var conexao = await BancoAsync();
        await using (var escrita = ContextoDeTeste.Novo(conexao))
        {
            escrita.Cargos.Add(Cargo.Criar("Cargo repetido apoio", null, 40m));
            await escrita.SaveChangesAsync();
        }

        await using (var repetido = ContextoDeTeste.Novo(conexao))
        {
            repetido.Cargos.Add(Cargo.Criar("cargo REPETIDO apoio", null, 40m)); // sem diferenciar maiúsculas: a chave única do banco recusa
            var erro = await Assert.ThrowsAsync<DbUpdateException>(() => repetido.SaveChangesAsync());
            Assert.True(ErrosDoBanco.EhChaveRepetida(erro));
            Assert.False(ErrosDoBanco.EhChaveEstrangeiraEmUso(erro));
        }

        var pessoa = await PessoaAsync(conexao, "Pessoa beneficio em uso");
        var beneficio = await ApoioDeTeste.CriarBeneficioAsync(conexao, "Beneficio em uso apoio", null, false, false, false);
        await ApoioDeTeste.DarBeneficioAsync(conexao, pessoa, beneficio, ativo: true, statusDoPedido: "APROVADO");
        await using var exclusao = ContextoDeTeste.Novo(conexao);
        exclusao.Beneficios.Remove(await exclusao.Beneficios.SingleAsync(b => b.Id == beneficio));
        var emUso = await Assert.ThrowsAsync<DbUpdateException>(() => exclusao.SaveChangesAsync());
        Assert.True(ErrosDoBanco.EhChaveEstrangeiraEmUso(emUso));
        Assert.False(ErrosDoBanco.EhChaveRepetida(emUso));
        Assert.False(ErrosDoBanco.EhChaveRepetida(new InvalidOperationException("outra coisa")));
    }
}
