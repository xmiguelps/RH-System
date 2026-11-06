using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Empresa;
using static RHSystem.Api.IntegrationTests.Auditoria.AuditoriaDeTeste;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// O que a auditoria registra do apoio (setor, gestor, empresa), com o interceptor de verdade e o relógio falso, no banco REAL. O teste do setor veio da Task 8.1 da 1a, que ainda não tinha <c>Setor.Criar</c>.
/// Um banco para a classe; cada teste cria o que usa.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class AuditoriaDoApoioTests(MySqlFixture mysql)
{
    private static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    private Task<(string Conexao, PessoaDeTeste Ator)> BancoAsync() =>
        BancoCompartilhado.ObterAsync(mysql, nameof(AuditoriaDoApoioTests), PrepararBancoAsync);

    [Fact]
    public async Task Criar_setor_gera_um_evento_Criou_com_ator_ip_entidade_id_e_descricao_Setor_Compras()
    {
        var (conexao, ator) = await BancoAsync();
        await using var db = NovoContexto(conexao, PessoaComIp(ator), RelogioEm(Agora));
        var setor = Setor.Criar("Compras auditoria", "Compras e suprimentos");

        db.Setores.Add(setor);
        await db.SaveChangesAsync();

        var evento = Assert.Single(await EventosAsync(conexao, "SETORES", setor.Id));
        Assert.Equal((AuditoriaArea.Cadastros, AuditoriaAcao.Criou, "Setor Compras auditoria", "Setor Compras auditoria", "SETORES", (long?)setor.Id), (evento.Area, evento.Acao, evento.Descricao, evento.RegistroAfetado, evento.Entidade, evento.IdRegistro));
        Assert.Equal((TipoDeAtor.Pessoa, ator.FuncionarioId, Ip, CanalDeOrigem.Portal), (evento.AtorTipo, evento.FuncionarioAtorId, evento.Ip, evento.Canal));
        Assert.Contains(evento.Mudancas, m => m.Campo == "Nome" && m.ValorDepois == "Compras auditoria");
        Assert.Contains(evento.Mudancas, m => m.Campo == "Descrição" && m.ValorDepois == "Compras e suprimentos");
    }

    [Fact]
    public async Task Marcar_e_desmarcar_a_ausencia_do_gestor_gera_Alterou_com_a_marca_Gestor_ausente_e_a_data_Volta_em()
    {
        var (conexao, ator) = await BancoAsync();
        var pessoa = await ApoioDeTeste.CriarPessoaAsync(conexao, "Gestora auditoria ausente", Perfil.Gestor);
        var gestorId = await ApoioDeTeste.GarantirGestorAsync(conexao, pessoa);
        await using var db = NovoContexto(conexao, PessoaComIp(ator), RelogioEm(Agora));
        var gestor = await db.Gestores.Include(g => g.Funcionario).SingleAsync(g => g.Id == gestorId);

        gestor.MarcarAusente(new DateOnly(2026, 10, 19), new DateOnly(2026, 10, 7));
        await db.SaveChangesAsync();
        gestor.DesmarcarAusencia();
        await db.SaveChangesAsync();

        var eventos = (await EventosAsync(conexao, "GESTORES", gestorId)).Where(e => e.Acao == AuditoriaAcao.Alterou).OrderBy(e => e.Id).ToList();
        Assert.Equal(2, eventos.Count);
        Assert.Equal("Gestora auditoria ausente", eventos[0].RegistroAfetado.Replace("Gestor ", string.Empty, StringComparison.Ordinal));
        Assert.Equal(("Gestor ausente", "Não", "Sim"), (eventos[0].Mudancas[0].Campo, eventos[0].Mudancas[0].ValorAntes, eventos[0].Mudancas[0].ValorDepois));
        Assert.Contains(eventos[0].Mudancas, m => m.Campo == "Volta em" && m.ValorAntes is null && m.ValorDepois == "19/10/2026");
        Assert.Contains(eventos[1].Mudancas, m => m.Campo == "Volta em" && m.ValorAntes == "19/10/2026" && m.ValorDepois is null);
    }

    [Fact]
    public async Task Salvar_a_empresa_gera_Criou_e_depois_Alterou_com_os_campos_que_mudaram_pelos_nomes_da_tela()
    {
        var (conexao, ator) = await BancoAsync();
        await ApoioDeTeste.ExecutarAsync(conexao, "DELETE FROM CONFIGURACOES_EMPRESA");
        await using var db = NovoContexto(conexao, PessoaComIp(ator), RelogioEm(Agora));
        var empresa = ConfiguracaoDaEmpresa.Criar("Empresa auditoria", "SP", "São Paulo", new TimeOnly(8, 0), new TimeOnly(17, 0), 10, 5);
        db.ConfiguracoesDaEmpresa.Add(empresa);
        await db.SaveChangesAsync();

        empresa.Atualizar("Empresa auditoria", "SP", "Campinas", new TimeOnly(9, 0), new TimeOnly(17, 0), 10, 5);
        await db.SaveChangesAsync();

        var eventos = (await EventosAsync(conexao, "CONFIGURACOES_EMPRESA", empresa.Id)).OrderBy(e => e.Id).ToList();
        Assert.Equal([AuditoriaAcao.Criou, AuditoriaAcao.Alterou], eventos.Select(e => e.Acao));
        Assert.Equal("Configuração da empresa", eventos[1].RegistroAfetado);
        Assert.Equal(["Horário de entrada", "Município"], eventos[1].Mudancas.Select(m => m.Campo).Order());
        var municipio = Assert.Single(eventos[1].Mudancas, m => m.Campo == "Município");
        Assert.Equal(("São Paulo", "Campinas"), (municipio.ValorAntes, municipio.ValorDepois));
    }
}
