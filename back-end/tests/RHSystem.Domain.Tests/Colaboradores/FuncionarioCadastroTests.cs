using System.Reflection;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Tests.Colaboradores;

public class FuncionarioCadastroTests
{
    private static Funcionario Nova() =>
        Funcionario.Criar("Bruno Lima", "bruno.lima@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso("Funcionario"));

    [Fact]
    public void Funcionario_AtualizarCadastro_normaliza_o_email_e_grava_cargo_setor_gestor_e_nivel()
    {
        var pessoa = Nova();

        pessoa.AtualizarCadastro(
            "Bruno Lima Souza",
            "  Bruno.Souza@Empresa.com.br ",
            new DateOnly(1990, 5, 17),
            new DateOnly(2026, 2, 3),
            TipoDeContrato.Estagio,
            cargoId: 4,
            setorId: 5,
            gestorId: 6,
            new NivelDeAcesso("Funcionario Gestor"));

        Assert.Equal("Bruno Lima Souza", pessoa.Nome);
        Assert.Equal("bruno.souza@empresa.com.br", pessoa.EmailCorporativo);
        Assert.Equal(new DateOnly(1990, 5, 17), pessoa.DataNascimento);
        Assert.Equal(new DateOnly(2026, 2, 3), pessoa.DataAdmissao);
        Assert.Equal("ESTAGIO", pessoa.TipoContrato);
        Assert.Equal((4, 5, 6), (pessoa.CargoId, pessoa.SetorId, pessoa.GestorId));
        Assert.Equal(Perfil.Gestor, pessoa.Perfil);

        // Sem gestor (o do setor sai, o setor fica sem ninguém): o campo limpa em vez de guardar o antigo.
        pessoa.AtualizarCadastro("Bruno Lima Souza", "bruno.souza@empresa.com.br", new DateOnly(1990, 5, 17), new DateOnly(2026, 2, 3), TipoDeContrato.Clt, 4, 5, null, new NivelDeAcesso("RH"));
        Assert.Null(pessoa.GestorId);
        Assert.Equal("CLT", pessoa.TipoContrato);
        Assert.Equal(Perfil.RH, pessoa.Perfil);
    }

    [Fact]
    public void Funcionario_Desligar_grava_a_data_e_inativa_e_Readmitir_ativa_zera_a_data_e_troca_o_vinculo()
    {
        var pessoa = Nova();

        pessoa.Desligar(new DateOnly(2026, 10, 7));

        Assert.False(pessoa.Ativo);
        Assert.Equal(new DateOnly(2026, 10, 7), pessoa.DataDesligamento);

        pessoa.Readmitir(new DateOnly(2026, 11, 1), cargoId: 8, setorId: 9, TipoDeContrato.Clt, gestorId: 10, new NivelDeAcesso("RH"));

        Assert.True(pessoa.Ativo);
        Assert.Null(pessoa.DataDesligamento);
        Assert.Equal(new DateOnly(2026, 11, 1), pessoa.DataAdmissao);
        Assert.Equal((8, 9, 10), (pessoa.CargoId, pessoa.SetorId, pessoa.GestorId));
        Assert.Equal("CLT", pessoa.TipoContrato);
        Assert.Equal(Perfil.RH, pessoa.Perfil);
    }

    [Fact]
    public void Funcionario_Readmitir_de_quem_esta_ativo_lanca_InvalidOperationException()
    {
        var pessoa = Nova();

        Assert.Throws<InvalidOperationException>(() =>
            pessoa.Readmitir(new DateOnly(2026, 11, 1), 1, 1, TipoDeContrato.Clt, null, new NivelDeAcesso("Funcionario")));

        Assert.Equal(new DateOnly(2026, 1, 2), pessoa.DataAdmissao); // nada mudou antes de recusar
    }

    [Fact]
    public void Funcionario_Situacao_e_Ativo_EmAdmissao_ou_Inativo_pelo_dia()
    {
        var pessoa = Nova();
        pessoa.AtualizarCadastro("Bruno Lima", "bruno.lima@empresa.com.br", new DateOnly(1990, 1, 1), new DateOnly(2026, 10, 8), TipoDeContrato.Clt, 1, 1, null, new NivelDeAcesso("Funcionario"));

        Assert.Equal(SituacaoCadastral.EmAdmissao, pessoa.Situacao(new DateOnly(2026, 10, 7)));
        Assert.Equal(SituacaoCadastral.Ativo, pessoa.Situacao(new DateOnly(2026, 10, 8)));
        Assert.Equal(SituacaoCadastral.Ativo, pessoa.Situacao(new DateOnly(2026, 10, 9)));

        pessoa.Desligar(new DateOnly(2026, 10, 9));
        Assert.Equal(SituacaoCadastral.Inativo, pessoa.Situacao(new DateOnly(2026, 10, 7))); // inativo vence "em admissão"
        Assert.Equal(SituacaoCadastral.Inativo, pessoa.Situacao(new DateOnly(2026, 10, 20)));
    }

    [Theory]
    [InlineData(TipoDeContrato.Clt, "CLT")]
    [InlineData(TipoDeContrato.Estagio, "ESTAGIO")]
    public void TiposDeContrato_ParaTexto_e_DoTexto_sao_inversos(TipoDeContrato tipo, string texto)
    {
        Assert.Equal(texto, TiposDeContrato.ParaTexto(tipo));
        Assert.Equal(tipo, TiposDeContrato.DoTexto(texto));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("PJ")]
    [InlineData("clt")]
    public void TiposDeContrato_DoTexto_de_valor_desconhecido_ou_nulo_e_null(string? texto) =>
        Assert.Null(TiposDeContrato.DoTexto(texto)); // o banco guarda texto livre: valor fora de CLT e ESTAGIO não derruba a leitura (decisão 9)

    [Fact]
    public void FuncionarioDetalhes_Criar_Atualizar_e_ParaDados_guardam_os_dados_pessoais()
    {
        var pessoa = Nova();
        var dados = new DadosPessoais("52998224725", "11987654410", "bruno@pessoal.com", "123456789", "SSP", "01310100", "Av. Paulista", "1000", "Sala 2", "Bela Vista", "São Paulo", "SP");

        var detalhes = FuncionarioDetalhes.Criar(pessoa, dados);

        Assert.Same(pessoa, detalhes.Funcionario);
        Assert.Equal(dados, detalhes.ParaDados());

        var novos = dados with { Telefone = null, Cidade = "Campinas", Cpf = "11144477735" };
        detalhes.Atualizar(novos);
        Assert.Equal(novos, detalhes.ParaDados());
    }

    [Fact]
    public void Setor_DefinirGestor_grava_e_limpa_o_gestor()
    {
        var setor = (Setor)Activator.CreateInstance(typeof(Setor), nonPublic: true)!;

        setor.DefinirGestor(12);
        Assert.Equal(12, setor.GestorId);

        setor.DefinirGestor(null);
        Assert.Null(setor.GestorId);
    }

    [Fact]
    public void Marcas_de_auditoria_da_1b()
    {
        static AuditarAttribute? Marca(string propriedade) =>
            typeof(Funcionario).GetProperty(propriedade, BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)!.GetCustomAttribute<AuditarAttribute>();

        Assert.Equal("Último dia de trabalho", Marca(nameof(Funcionario.DataDesligamento))!.Campo);
        Assert.False(Marca(nameof(Funcionario.DataDesligamento))!.Sensivel);
        Assert.Equal("Data de nascimento", Marca(nameof(Funcionario.DataNascimento))!.Campo);
        Assert.True(Marca(nameof(Funcionario.DataNascimento))!.Sensivel); // o log diz "Data de nascimento alterada", nunca o valor
        Assert.Equal("Tipo de contrato", Marca(nameof(Funcionario.TipoContrato))!.Campo);
        Assert.Equal("Nível de acesso", Marca(nameof(Funcionario.NivelDeAcessoId))!.Campo);

        // O gestor do colaborador é derivado do setor: auditar os dois duplicaria o evento.
        Assert.Null(Marca(nameof(Funcionario.GestorId)));

        // Os dados pessoais entram no log pela ficha, não por uma entidade auditada à parte: FuncionarioDetalhes não leva [Auditado].
        Assert.Null(typeof(FuncionarioDetalhes).GetCustomAttribute<AuditadoAttribute>());
        Assert.DoesNotContain(
            typeof(FuncionarioDetalhes).GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance),
            p => p.GetCustomAttribute<AuditarAttribute>() is not null);

        // O nível de acesso é só rótulo para a chave estrangeira: implementa IAuditavel, mas não é auditado por si.
        Assert.True(typeof(IAuditavel).IsAssignableFrom(typeof(NivelDeAcesso)));
        Assert.Null(typeof(NivelDeAcesso).GetCustomAttribute<AuditadoAttribute>());
    }
}
