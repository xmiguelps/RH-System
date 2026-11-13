using RHSystem.Application.Colaboradores;
using RHSystem.Application.Ferias.Regras;

namespace RHSystem.Application.Tests.Ferias;

/// <summary>
/// Os conflitos de um pedido (férias já aprovadas de OUTRAS pessoas do mesmo setor que coincidem com as datas, com os dias em comum) e quantas pessoas do setor ficam trabalhando (sem quem pede e sem quem tem férias aprovadas no período; o gestor do setor conta).
/// </summary>
public class ConflitosDeFeriasTests
{
    private static DateOnly D(int ano, int mes, int dia) => new(ano, mes, dia);

    private static MembroDoSetor Membro(int id, DateOnly? admissao = null) => new(id, new FuncionarioResumo(id, $"Pessoa {id}", true, "Cargo", "Setor"), admissao ?? D(2022, 3, 14));

    private static FeriasAprovadaDoSetor Aprovada(int pessoa, DateOnly inicio, DateOnly fim) => new(pessoa, inicio, fim);

    private static readonly IReadOnlyList<MembroDoSetor> Setor = [Membro(1), Membro(2), Membro(3), Membro(4), Membro(5)];

    [Fact]
    public void Sem_setor_nao_ha_conflito_nem_equipe_disponivel()
    {
        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), pedidoEmAberto: true, setorId: null, Setor, [Aprovada(2, D(2026, 11, 16), D(2026, 11, 30))]);

        Assert.Empty(r.Conflitos);
        Assert.Null(r.EquipeDisponivel);
    }

    [Fact]
    public void O_conflito_conta_os_dias_em_comum_contando_o_primeiro_e_o_ultimo_e_ordena_por_data()
    {
        var aprovadas = new[]
        {
            Aprovada(3, D(2026, 11, 20), D(2026, 12, 4)), // 11 dias em comum (20 a 30)
            Aprovada(2, D(2026, 11, 9), D(2026, 11, 23)), // 8 dias (16 a 23)
            Aprovada(4, D(2026, 11, 30), D(2026, 12, 6)), // 1 dia: o último
            Aprovada(5, D(2026, 11, 1), D(2026, 11, 15)), // termina na véspera: não coincide
        };

        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, Setor, aprovadas);

        Assert.Equal([(2, 8), (3, 11), (4, 1)], r.Conflitos.Select(c => (c.Funcionario.Id, c.DiasEmComum)));
        Assert.Equal([(D(2026, 11, 9), D(2026, 11, 23)), (D(2026, 11, 20), D(2026, 12, 4)), (D(2026, 11, 30), D(2026, 12, 6))], r.Conflitos.Select(c => (c.DataInicio, c.DataFim)));
    }

    [Fact]
    public void As_ferias_de_quem_pede_e_de_quem_nao_e_do_setor_nao_sao_conflito()
    {
        var aprovadas = new[] { Aprovada(1, D(2026, 11, 16), D(2026, 11, 30)), Aprovada(99, D(2026, 11, 16), D(2026, 11, 30)) };

        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, Setor, aprovadas);

        Assert.Empty(r.Conflitos);
        Assert.Equal(4, r.EquipeDisponivel);
    }

    [Fact]
    public void Quem_pede_e_quem_tem_ferias_no_periodo_nao_ficam_trabalhando()
    {
        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, Setor, [Aprovada(2, D(2026, 11, 30), D(2026, 12, 4)), Aprovada(3, D(2026, 10, 1), D(2026, 11, 15))]);

        Assert.Equal(3, r.EquipeDisponivel); // saem 1 (quem pede) e 2 (coincide em um dia); 3 terminou na véspera
    }

    [Fact]
    public void Quem_so_e_admitido_depois_do_fim_do_periodo_nao_conta_como_trabalhando()
    {
        IReadOnlyList<MembroDoSetor> membros = [Membro(1), Membro(2), Membro(3, D(2026, 11, 30)), Membro(4, D(2026, 12, 1))];

        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, membros, []);

        Assert.Equal(2, r.EquipeDisponivel); // 2 e 3 (admitido no último dia); o 4 entra depois
    }

    [Fact]
    public void Pedido_ja_decidido_nao_lista_conflito_mas_a_equipe_disponivel_continua()
    {
        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), pedidoEmAberto: false, 10, Setor, [Aprovada(2, D(2026, 11, 16), D(2026, 11, 30))]);

        Assert.Empty(r.Conflitos);
        Assert.Equal(3, r.EquipeDisponivel);
    }

    [Fact]
    public void Duas_ferias_aprovadas_da_mesma_pessoa_no_periodo_aparecem_as_duas_e_ela_so_sai_uma_vez_da_equipe()
    {
        var r = ConflitosDeFerias.Calcular(1, D(2026, 11, 16), D(2026, 11, 30), true, 10, Setor, [Aprovada(2, D(2026, 11, 16), D(2026, 11, 18)), Aprovada(2, D(2026, 11, 25), D(2026, 11, 27))]);

        Assert.Equal([(2, 3), (2, 3)], r.Conflitos.Select(c => (c.Funcionario.Id, c.DiasEmComum)));
        Assert.Equal(3, r.EquipeDisponivel);
    }
}
