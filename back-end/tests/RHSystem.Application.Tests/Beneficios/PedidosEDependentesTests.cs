using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Tests.Beneficios;

/// <summary>"Seus pedidos" (ordem, título e prazo) e "Quem vai entrar no plano" (dependentes sem repetir).</summary>
public class PedidosEDependentesTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static DateTime Utc(int ano, int mes, int dia, int hora, int minuto) => new(ano, mes, dia, hora, minuto, 0, DateTimeKind.Utc);

    private static readonly IReadOnlySet<DateOnly> SemFeriados = new HashSet<DateOnly>();

    private static PedidoParaResumir Pedido(TipoDoPedidoDeBeneficio tipo, int id, DateTime em, StatusDoPedidoDeBeneficio situacao = StatusDoPedidoDeBeneficio.Pendente, string? motivo = null) =>
        new(tipo, id, 42, "Plano de saúde", em, situacao, motivo);

    [Fact]
    public void Resumir_monta_o_titulo_o_prazo_e_repassa_a_situacao_e_o_motivo()
    {
        var resumo = PedidosDaPessoa.Resumir(
            new PedidoParaResumir(TipoDoPedidoDeBeneficio.Dependente, 8, 42, "Plano de saúde", Utc(2026, 1, 12, 13, 0), StatusDoPedidoDeBeneficio.Recusado, "Falta certidão"),
            SemFeriados);

        Assert.Equal(
            new ResumoDoPedido(TipoDoPedidoDeBeneficio.Dependente, 8, 42, "Inclusão de dependente no plano de saúde", Utc(2026, 1, 12, 13, 0), StatusDoPedidoDeBeneficio.Recusado, "Falta certidão", D(2026, 1, 19)),
            resumo);
    }

    [Fact]
    public void Os_pedidos_saem_do_mais_novo_para_o_mais_antigo()
    {
        var resumos = PedidosDaPessoa.Montar(
            [
                Pedido(TipoDoPedidoDeBeneficio.Trajeto, 5, Utc(2025, 8, 3, 12, 0)),
                Pedido(TipoDoPedidoDeBeneficio.Dependente, 8, Utc(2026, 1, 12, 13, 0)),
                Pedido(TipoDoPedidoDeBeneficio.Adesao, 44, Utc(2025, 12, 1, 9, 0)),
            ],
            SemFeriados);

        Assert.Equal([8, 44, 5], resumos.Select(r => r.Id));
    }

    [Fact]
    public void No_mesmo_instante_a_adesao_vem_antes_do_dependente_e_do_trajeto_e_o_id_maior_antes()
    {
        var em = Utc(2026, 10, 7, 12, 41);

        var resumos = PedidosDaPessoa.Montar(
            [
                Pedido(TipoDoPedidoDeBeneficio.Trajeto, 3, em),
                Pedido(TipoDoPedidoDeBeneficio.Dependente, 10, em),
                Pedido(TipoDoPedidoDeBeneficio.Adesao, 44, em),
                Pedido(TipoDoPedidoDeBeneficio.Dependente, 11, em),
            ],
            SemFeriados);

        Assert.Equal([(TipoDoPedidoDeBeneficio.Adesao, 44), (TipoDoPedidoDeBeneficio.Dependente, 11), (TipoDoPedidoDeBeneficio.Dependente, 10), (TipoDoPedidoDeBeneficio.Trajeto, 3)], resumos.Select(r => (r.Tipo, r.Id)));
    }

    [Fact]
    public void Sem_pedidos_a_lista_e_vazia()
    {
        Assert.Empty(PedidosDaPessoa.Montar([], SemFeriados));
    }

    [Fact]
    public void Dependentes_repetidos_so_na_caixa_ou_nos_espacos_valem_um_so_e_vence_o_primeiro()
    {
        var lista = DependentesCadastrados.SemRepetir(
        [
            new DadosDoDependente("Helena Lima", "FILHA", D(2019, 5, 21)),
            new DadosDoDependente("  helena lima ", "filha", D(2019, 5, 21)),
            new DadosDoDependente("HELENA LIMA", "Filha", D(2019, 5, 21)),
        ]);

        Assert.Equal([new DadosDoDependente("Helena Lima", "FILHA", D(2019, 5, 21))], lista);
    }

    [Fact]
    public void Muda_o_parentesco_ou_o_nascimento_e_sao_dependentes_diferentes()
    {
        var lista = DependentesCadastrados.SemRepetir(
        [
            new DadosDoDependente("Helena Lima", "FILHA", D(2019, 5, 21)),
            new DadosDoDependente("Helena Lima", "ENTEADA", D(2019, 5, 21)),
            new DadosDoDependente("Helena Lima", "FILHA", D(2020, 5, 21)),
        ]);

        Assert.Equal(3, lista.Count);
    }

    [Fact]
    public void Os_dependentes_saem_em_ordem_de_nome_sem_diferenciar_caixa_e_depois_de_nascimento()
    {
        var lista = DependentesCadastrados.SemRepetir(
        [
            new DadosDoDependente("pedro Lima", "FILHO", D(2021, 9, 3)),
            new DadosDoDependente("Ana Lima", "FILHA", D(2022, 1, 1)),
            new DadosDoDependente("Ana Lima", "ENTEADA", D(2018, 1, 1)),
            new DadosDoDependente("Bia Lima", "FILHA", D(2019, 5, 21)),
        ]);

        Assert.Equal([D(2018, 1, 1), D(2022, 1, 1), D(2019, 5, 21), D(2021, 9, 3)], lista.Select(d => d.DataNascimento));
    }

    [Fact]
    public void Sem_dependentes_a_lista_e_vazia()
    {
        Assert.Empty(DependentesCadastrados.SemRepetir([]));
    }

    [Fact]
    public void Os_dados_aparados_perdem_os_espacos_das_pontas_e_nulo_continua_nulo()
    {
        Assert.Equal(new DadosDoDependente("Ana", "FILHA", D(2020, 1, 1)), new DadosDoDependente(" Ana ", " FILHA", D(2020, 1, 1)).Aparado());
        Assert.Equal(new DadosDoDependente(null, null, D(2020, 1, 1)), new DadosDoDependente(null, null, D(2020, 1, 1)).Aparado());
        Assert.Equal(new DadosDoTrajeto("Rua A, 1", "Av. B, 2", "Ônibus 10"), new DadosDoTrajeto(" Rua A, 1 ", "Av. B, 2 ", " Ônibus 10").Aparado());
        Assert.Equal(new DadosDoTrajeto(null, null, null), new DadosDoTrajeto(null, null, null).Aparado());
    }
}
