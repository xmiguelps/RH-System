using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Tests.Apoio;

public class SetorTests
{
    [Fact]
    public void Setor_Atualizar_troca_nome_e_descricao_e_nao_mexe_no_gestor()
    {
        var setor = Setor.Criar("Compras", "antes");
        setor.DefinirGestor(7);

        setor.Atualizar("Suprimentos", "depois");

        Assert.Equal(("Suprimentos", "depois", (int?)7), (setor.Nome, setor.Descricao, setor.GestorId)); // o gestor muda por DefinirGestor, nunca por Atualizar
    }

    [Fact]
    public void Funcionario_DefinirNivel_troca_o_nivel_e_recusa_nulo()
    {
        var comum = new NivelDeAcesso("Funcionario");
        var gestor = new NivelDeAcesso("Funcionario Gestor");
        var pessoa = Funcionario.Criar("Bruno Lima", "bruno.lima@empresa.com.br", new DateOnly(2026, 1, 2), comum);

        pessoa.DefinirNivel(gestor);

        Assert.Same(gestor, pessoa.NivelDeAcesso);
        Assert.Equal(Perfil.Gestor, pessoa.Perfil);
        Assert.Throws<ArgumentNullException>(() => pessoa.DefinirNivel(null!));
        Assert.Same(gestor, pessoa.NivelDeAcesso); // o nulo recusado não apaga o nível
    }
}
