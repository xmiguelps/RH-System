using System.Net.Http.Json;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Auditoria;
using RHSystem.Api.IntegrationTests.Colaboradores;
using RHSystem.Api.IntegrationTests.Contrato;
using RHSystem.Api.IntegrationTests.Ferias;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Domain.Auditoria;
using static RHSystem.Api.IntegrationTests.Acesso.AcessoDeTeste;
using static RHSystem.Api.IntegrationTests.Beneficios.BeneficiosDeTeste;

namespace RHSystem.Api.IntegrationTests.Beneficios;

/// <summary>
/// As pontes dos benefícios com o resto do sistema, no banco REAL: desligar cancela os pedidos pendentes (a adesão, os dependentes e os trajetos) sem evento próprio, e a ficha do RH conta os dependentes aprovados de cada benefício ativo. Hoje é 2026-10-07T12:42Z (quarta, 09:42 em Brasília). O banco de horas
/// já está fechado até 2030 (o que o desligamento da fatia 1 espera).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class PontesDosBeneficiosTests(MySqlFixture mysql)
{
    private Task<ApiDeAcesso> ApiAsync() => DesligamentoDeTeste.ComecarAsync(mysql, nameof(PontesDosBeneficiosTests));

    private static Task<PessoaDeFerias> NovaAsync(ApiDeAcesso api, string nome) => FeriasDeTeste.PessoaAsync(api, nome, new DateOnly(2024, 1, 2));

    private static string Estado(ApiDeAcesso api, string tabela, string chave, int id) =>
        TextoAsync(api, $"SELECT CONCAT_WS('|', STATUS_PEDIDO, IFNULL(ID_FUNCIONARIO_RH, '-'), IFNULL(DATE_FORMAT(DATA_RESPOSTA, '%Y-%m-%d %H:%i:%s'), '-')) FROM {tabela} WHERE {chave} = {id}").GetAwaiter().GetResult()!;

    private static string Adesao(ApiDeAcesso api, int id) =>
        TextoAsync(api, $"SELECT CONCAT_WS('|', STATUS_PEDIDO, BENEFICIO_ATIVO, IFNULL(ID_FUNCIONARIO_RH, '-'), IFNULL(DATE_FORMAT(DATA_PEDIDO, '%Y-%m-%d %H:%i:%s'), '-')) FROM BENEFICIOS_FUNCIONARIOS WHERE ID_BENEFICIO_FUNCIONARIOS = {id}").GetAwaiter().GetResult()!;

    private static string Dependente(ApiDeAcesso api, int id) => Estado(api, "DEPENDENTES", "ID_DEPENDENTE", id);

    private static string Trajeto(ApiDeAcesso api, int id) => Estado(api, "TRAJETOS_VALE_TRANSPORTE", "ID_TRAJETO", id);

    // ---------------------------------------------------------------- desligar cancela os pedidos pendentes

    [Fact]
    public async Task Inativar_cancela_a_adesao_os_dependentes_e_os_trajetos_pendentes_sem_evento_proprio_e_eles_somem_da_lista_do_RH()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Vai embora");
        var outra = await NovaAsync(api, "Fica");
        var pedido = await ItemAsync(api, dependentes: true, trajeto: true);
        var ativo = await ItemAsync(api, dependentes: true, trajeto: true, opcional: false);
        var daOutra = await ItemAsync(api, dependentes: true);
        var adesaoPendente = await AdesaoAsync(api, pessoa.FuncionarioId, pedido, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00", inicio: "2026-11-01", autoriza: true);
        var depDaPendente = await DependenteAsync(api, adesaoPendente, "Zyx Pendente", pedidoEm: "2026-10-05 08:00:00");
        var trajDaPendente = await TrajetoAsync(api, adesaoPendente, pedidoEm: "2026-10-05 08:00:00");
        var adesaoAtiva = await AdesaoAsync(api, pessoa.FuncionarioId, ativo);
        var depPendenteDaAtiva = await DependenteAsync(api, adesaoAtiva, "Zyx Esperando", pedidoEm: "2026-10-06 08:00:00");
        var depAprovado = await DependenteAsync(api, adesaoAtiva, "Zyx Aprovado", status: "APROVADO", pedidoEm: "2026-08-01 09:00:00", respondidoEm: "2026-08-02 10:00:00");
        var depRecusado = await DependenteAsync(api, adesaoAtiva, "Zyx Recusado", status: "RECUSADO", pedidoEm: "2026-08-01 09:00:00", respondidoEm: "2026-08-02 10:00:00");
        var trajPendenteDaAtiva = await TrajetoAsync(api, adesaoAtiva, pedidoEm: "2026-10-06 08:00:00");
        var trajAprovado = await TrajetoAsync(api, adesaoAtiva, status: "APROVADO", pedidoEm: "2025-08-03 09:00:00", respondidoEm: "2025-08-05 12:00:00");
        var adesaoDaOutra = await AdesaoAsync(api, outra.FuncionarioId, daOutra, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00");
        var depDaOutra = await DependenteAsync(api, adesaoDaOutra, "Zyx Da Outra", pedidoEm: "2026-10-05 08:00:00");
        var eventosAntes = Contar(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE IN ('BENEFICIOS_FUNCIONARIOS', 'DEPENDENTES', 'TRAJETOS_VALE_TRANSPORTE')");
        var aprovadoAntes = Dependente(api, depAprovado);
        var recusadoAntes = Dependente(api, depRecusado);
        var trajAprovadoAntes = Trajeto(api, trajAprovado);

        await DesligamentoDeTeste.AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 10, 5)); // já passou: efetiva na hora

        Assert.Equal("False", await TextoAsync(api, $"SELECT ATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {pessoa.FuncionarioId}"));
        Assert.Equal("CANCELADO|0|-|2026-10-05 08:00:00", Adesao(api, adesaoPendente)); // a data do pedido fica; sem resposta
        Assert.Equal(("CANCELADO|-|-", "CANCELADO|-|-"), (Dependente(api, depDaPendente), Trajeto(api, trajDaPendente)));
        Assert.StartsWith("APROVADO|0|", Adesao(api, adesaoAtiva), StringComparison.Ordinal); // o trigger só a desativou
        Assert.Equal(("CANCELADO|-|-", "CANCELADO|-|-"), (Dependente(api, depPendenteDaAtiva), Trajeto(api, trajPendenteDaAtiva)));
        Assert.Equal((aprovadoAntes, recusadoAntes, trajAprovadoAntes), (Dependente(api, depAprovado), Dependente(api, depRecusado), Trajeto(api, trajAprovado))); // o que já foi respondido não muda
        Assert.Equal(("PENDENTE|0|-|2026-10-05 08:00:00", "PENDENTE|-|-"), (Adesao(api, adesaoDaOutra), Dependente(api, depDaOutra))); // quem fica não é tocado
        Assert.Equal(eventosAntes, Contar(api, "SELECT COUNT(*) FROM AUDITORIA_EVENTOS WHERE ENTIDADE IN ('BENEFICIOS_FUNCIONARIOS', 'DEPENDENTES', 'TRAJETOS_VALE_TRANSPORTE')")); // nenhum evento próprio

        var rhCliente = await api.ClienteDoRhAgoraAsync();
        Assert.Empty(Ids(await CorpoAsync(await rhCliente.GetAsync($"/api/rh/beneficios-adesoes?beneficioId={pedido}"))));
        Assert.Empty(Ids(await CorpoAsync(await rhCliente.GetAsync($"/api/rh/beneficios-adesoes?beneficioId={ativo}"))));
        Assert.Equal([adesaoDaOutra], Ids(await CorpoAsync(await rhCliente.GetAsync($"/api/rh/beneficios-adesoes?beneficioId={daOutra}"))));
        Assert.DoesNotContain("Zyx", (await AuditoriaDeTeste.TudoDoLogAsync(api.Conexao)).Texto, StringComparison.Ordinal);
    }

    [Fact]
    public async Task Desligamento_agendado_so_cancela_os_pedidos_quando_efetiva()
    {
        var api = await ApiAsync();
        var pessoa = await NovaAsync(api, "Sai depois");
        var item = await ItemAsync(api, dependentes: true);
        var adesao = await AdesaoAsync(api, pessoa.FuncionarioId, item, "PENDENTE", ativo: false, pedidoEm: "2026-10-05 08:00:00");
        var dependente = await DependenteAsync(api, adesao, pedidoEm: "2026-10-05 08:00:00");

        await DesligamentoDeTeste.AgendarAsync(api, pessoa.FuncionarioId, new DateOnly(2026, 11, 13));

        Assert.Equal(("PENDENTE|0|-|2026-10-05 08:00:00", "PENDENTE|-|-"), (Adesao(api, adesao), Dependente(api, dependente))); // ainda ativa: nada mudou

        api.Api.Relogio.Fixar(new DateTime(2026, 11, 16, 12, 0, 0, DateTimeKind.Utc));
        await DesligamentoDeTeste.EfetivarComoSistemaAsync(api, await DesligamentoDeTeste.DesligamentoIdAsync(api, pessoa.FuncionarioId));

        Assert.Equal(("CANCELADO|0|-|2026-10-05 08:00:00", "CANCELADO|-|-"), (Adesao(api, adesao), Dependente(api, dependente)));
        api.Api.Relogio.Fixar(DesligamentoDeTeste.Agora);
    }

    private static List<int> Ids(System.Text.Json.JsonElement lista) => [.. lista.GetProperty("itens").EnumerateArray().Select(i => i.GetProperty("adesaoId").GetInt32())];

    // ---------------------------------------------------------------- a ficha do RH

    [Fact]
    public async Task A_ficha_do_RH_conta_os_dependentes_APROVADO_de_cada_beneficio_ativo_e_a_do_gestor_nao_tem_o_numero()
    {
        var api = await ApiAsync();
        var (_, gestor, _, membro, _) = await FeriasDeTeste.EquipeAsync(api, "Ficha dos beneficios");
        var saude = await ItemAsync(api, "Plano de saúde ficha", opcional: false, dependentes: true);
        var vr = await ItemAsync(api, "Vale-refeição ficha", opcional: false);
        var odonto = await ItemAsync(api, "Plano odontológico ficha", opcional: false, dependentes: true);
        var adesaoSaude = await AdesaoAsync(api, membro.FuncionarioId, saude);
        _ = await AdesaoAsync(api, membro.FuncionarioId, vr);
        var adesaoOdonto = await AdesaoAsync(api, membro.FuncionarioId, odonto, "APROVADO", ativo: false);
        await DependenteAsync(api, adesaoSaude, "Zyx Um", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");
        await DependenteAsync(api, adesaoSaude, "Zyx Dois", status: "APROVADO", respondidoEm: "2026-01-03 10:00:00");
        await DependenteAsync(api, adesaoSaude, "Zyx Pendente");
        await DependenteAsync(api, adesaoSaude, "Zyx Recusado", status: "RECUSADO", respondidoEm: "2026-01-04 10:00:00");
        await DependenteAsync(api, adesaoSaude, "Zyx Cancelado", status: "CANCELADO");
        await DependenteAsync(api, adesaoOdonto, "Zyx Do Inativo", status: "APROVADO", respondidoEm: "2026-01-02 10:00:00");

        var resposta = await (await api.ClienteDoRhAgoraAsync()).GetAsync($"/api/rh/funcionarios/{membro.FuncionarioId}");

        Assert.Equal(200, (int)resposta.StatusCode);
        await resposta.AfirmarConformeAoContratoAsync("obterFichaDoFuncionario");
        var beneficios = (await CorpoAsync(resposta)).GetProperty("beneficios").EnumerateArray().ToDictionary(b => b.GetProperty("beneficioId").GetInt32(), b => b.GetProperty("quantidadeDependentes").GetInt32());
        Assert.Equal(((2, 0), 2), ((beneficios[saude], beneficios[vr]), beneficios.Count));

        var daEquipe = await (await api.ClienteAgoraAsync(gestor.UsuarioId)).GetAsync($"/api/gestor/equipe/{membro.FuncionarioId}");
        Assert.Equal(200, (int)daEquipe.StatusCode);
        Assert.DoesNotContain("quantidadeDependentes", await daEquipe.Content.ReadAsStringAsync(), StringComparison.OrdinalIgnoreCase);
    }
}
