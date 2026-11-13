using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Tests.Beneficios;

/// <summary>As duas listas de "Meus benefícios": cartões (ativos e aprovados) e disponíveis (opcionais que a pessoa não tem nem pede).</summary>
public class ListasDoBeneficioTests
{
    private static readonly DateTime Resposta = new(2026, 9, 1, 14, 0, 0, DateTimeKind.Utc);

    private static readonly ItemDoCatalogo ValeRefeicao = new(1, "Vale-refeição", "Crédito", false, false, false);

    private static readonly ItemDoCatalogo PlanoDeSaude = new(2, "Plano de saúde", null, false, true, false);

    private static readonly ItemDoCatalogo Odonto = new(3, "Plano odontológico", "Dentistas", true, true, false);

    private static readonly ItemDoCatalogo Creche = new(6, "Auxílio-creche", null, true, true, false);

    private static readonly ItemDoCatalogo Gympass = new(7, "Gympass", null, true, false, false);

    private static readonly ItemDoCatalogo[] Catalogo = [ValeRefeicao, PlanoDeSaude, Odonto, Creche, Gympass];

    private static AdesaoDaPessoa Linha(ItemDoCatalogo item, StatusDoPedidoDeBeneficio status, bool ativo, string? motivo = null, DateTime? respondidoEm = null) =>
        new(100 + item.Id, item.Id, status, ativo, null, false, motivo, respondidoEm);

    [Fact]
    public void Sem_nenhuma_linha_so_os_opcionais_estao_disponiveis_sem_ultimo_pedido()
    {
        var listas = ListasDoBeneficio.Montar(Catalogo, []);

        Assert.Empty(listas.Cartoes);
        Assert.Equal([Odonto, Creche, Gympass], listas.Disponiveis.Select(d => d.Item));
        Assert.All(listas.Disponiveis, d => Assert.Null(d.UltimoPedido));
    }

    [Fact]
    public void Aprovado_e_ativo_e_cartao_inclusive_o_que_nao_e_opcional_e_sai_dos_disponiveis()
    {
        var vr = Linha(ValeRefeicao, StatusDoPedidoDeBeneficio.Aprovado, true);
        var odonto = Linha(Odonto, StatusDoPedidoDeBeneficio.Aprovado, true);

        var listas = ListasDoBeneficio.Montar(Catalogo, [odonto, vr]);

        Assert.Equal([ValeRefeicao, Odonto], listas.Cartoes.Select(c => c.Item));
        Assert.Equal(vr, listas.Cartoes[0].Adesao);
        Assert.Equal([Creche, Gympass], listas.Disponiveis.Select(d => d.Item));
    }

    [Fact]
    public void Pedido_pendente_nao_e_cartao_nem_disponivel()
    {
        var listas = ListasDoBeneficio.Montar(Catalogo, [Linha(Creche, StatusDoPedidoDeBeneficio.Pendente, false)]);

        Assert.Empty(listas.Cartoes);
        Assert.Equal([Odonto, Gympass], listas.Disponiveis.Select(d => d.Item));
    }

    [Fact]
    public void Recusado_volta_para_disponiveis_com_o_motivo_e_a_data_da_resposta()
    {
        var listas = ListasDoBeneficio.Montar(Catalogo, [Linha(Odonto, StatusDoPedidoDeBeneficio.Recusado, false, "Fora da carência", Resposta)]);

        var odonto = Assert.Single(listas.Disponiveis, d => d.Item.Id == Odonto.Id);
        Assert.Equal(new UltimoPedidoDoBeneficio(StatusDoPedidoDeBeneficio.Recusado, "Fora da carência", Resposta), odonto.UltimoPedido);
    }

    [Fact]
    public void Cancelado_volta_para_disponiveis_como_cancelado_sem_motivo()
    {
        var listas = ListasDoBeneficio.Montar(Catalogo, [Linha(Gympass, StatusDoPedidoDeBeneficio.Cancelado, false)]);

        var gympass = Assert.Single(listas.Disponiveis, d => d.Item.Id == Gympass.Id);
        Assert.Equal(new UltimoPedidoDoBeneficio(StatusDoPedidoDeBeneficio.Cancelado, null, null), gympass.UltimoPedido);
    }

    [Fact]
    public void Aprovado_que_o_RH_desativou_volta_para_disponiveis_sem_ultimo_pedido_e_nao_e_cartao()
    {
        var listas = ListasDoBeneficio.Montar(Catalogo, [Linha(Creche, StatusDoPedidoDeBeneficio.Aprovado, false, null, Resposta)]);

        Assert.Empty(listas.Cartoes);
        var creche = Assert.Single(listas.Disponiveis, d => d.Item.Id == Creche.Id);
        Assert.Null(creche.UltimoPedido);
    }

    [Fact]
    public void Item_que_nao_e_opcional_nunca_esta_disponivel_nem_com_pedido_recusado_ou_desativado()
    {
        var listas = ListasDoBeneficio.Montar(
            Catalogo,
            [
                Linha(ValeRefeicao, StatusDoPedidoDeBeneficio.Recusado, false, "x", Resposta),
                Linha(PlanoDeSaude, StatusDoPedidoDeBeneficio.Aprovado, false, null, Resposta),
            ]);

        Assert.Empty(listas.Cartoes);
        Assert.Equal([Odonto, Creche, Gympass], listas.Disponiveis.Select(d => d.Item));
    }

    [Fact]
    public void As_duas_listas_saem_em_ordem_de_id_do_catalogo_mesmo_com_a_entrada_embaralhada()
    {
        ItemDoCatalogo[] embaralhado = [Gympass, ValeRefeicao, Creche, PlanoDeSaude, Odonto];

        var listas = ListasDoBeneficio.Montar(
            embaralhado,
            [Linha(PlanoDeSaude, StatusDoPedidoDeBeneficio.Aprovado, true), Linha(ValeRefeicao, StatusDoPedidoDeBeneficio.Aprovado, true)]);

        Assert.Equal([ValeRefeicao, PlanoDeSaude], listas.Cartoes.Select(c => c.Item));
        Assert.Equal([Odonto, Creche, Gympass], listas.Disponiveis.Select(d => d.Item));
    }

    [Fact]
    public void Linha_de_um_beneficio_que_nao_esta_no_catalogo_e_ignorada()
    {
        var orfa = new AdesaoDaPessoa(900, 99, StatusDoPedidoDeBeneficio.Aprovado, true, null, false, null, Resposta);

        var listas = ListasDoBeneficio.Montar(Catalogo, [orfa]);

        Assert.Empty(listas.Cartoes);
    }
}

/// <summary>O trajeto em vigor: o aprovado que o RH respondeu por último.</summary>
public class TrajetoEmVigorTests
{
    private static DateTime Em(int dia) => new(2026, 9, dia, 14, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void Sem_trajeto_aprovado_nao_ha_trajeto_em_vigor()
    {
        Assert.Null(TrajetoEmVigor.IdDe([]));
        Assert.Null(TrajetoEmVigor.IdDe(
        [
            new TrajetoParaEscolher(1, StatusDoPedidoDeBeneficio.Pendente, null),
            new TrajetoParaEscolher(2, StatusDoPedidoDeBeneficio.Recusado, Em(3)),
            new TrajetoParaEscolher(3, StatusDoPedidoDeBeneficio.Cancelado, null),
        ]));
    }

    [Fact]
    public void Vale_o_aprovado_respondido_por_ultimo_e_um_pedido_pendente_novo_nao_o_tira()
    {
        var id = TrajetoEmVigor.IdDe(
        [
            new TrajetoParaEscolher(5, StatusDoPedidoDeBeneficio.Aprovado, Em(2)),
            new TrajetoParaEscolher(6, StatusDoPedidoDeBeneficio.Pendente, null),
            new TrajetoParaEscolher(4, StatusDoPedidoDeBeneficio.Aprovado, Em(9)),
        ]);

        Assert.Equal(4, id);
    }

    [Fact]
    public void No_mesmo_instante_vale_o_de_id_maior()
    {
        var id = TrajetoEmVigor.IdDe(
        [
            new TrajetoParaEscolher(7, StatusDoPedidoDeBeneficio.Aprovado, Em(2)),
            new TrajetoParaEscolher(8, StatusDoPedidoDeBeneficio.Aprovado, Em(2)),
            new TrajetoParaEscolher(6, StatusDoPedidoDeBeneficio.Aprovado, Em(2)),
        ]);

        Assert.Equal(8, id);
    }
}
