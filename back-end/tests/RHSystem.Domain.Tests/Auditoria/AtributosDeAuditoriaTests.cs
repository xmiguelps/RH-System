using System.Reflection;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Domain.Tests.Auditoria;

public class AtributosDeAuditoriaTests
{
    private static readonly Assembly Dominio = typeof(Funcionario).Assembly;

    private static IEnumerable<(Type Tipo, PropertyInfo Propriedade, AuditarAttribute Marca)> PropriedadesAuditadas() =>
        Dominio.GetTypes()
            .SelectMany(t => t.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance), (t, p) => (Tipo: t, Propriedade: p, Marca: p.GetCustomAttribute<AuditarAttribute>()))
            .Where(x => x.Marca is not null)
            .Select(x => (x.Tipo, x.Propriedade, x.Marca!));

    [Fact]
    public void Nenhuma_propriedade_com_Auditar_se_chama_Senha_Hash_Token_ou_Segredo()
    {
        var marcadas = PropriedadesAuditadas().ToList();
        var proibidas = new[] { "Senha", "Hash", "Token", "Segredo" };

        // Controle positivo: a varredura achou as marcas da fatia (sem isto, "nenhuma proibida" admitiria "não achou nada").
        Assert.True(marcadas.Count >= 15, $"Só {marcadas.Count} propriedades marcadas: a reflexão não está lendo o Domain?");
        var achadas = marcadas
            .Where(m => proibidas.Any(p => m.Propriedade.Name.Contains(p, StringComparison.OrdinalIgnoreCase) || m.Marca.Campo.Contains(p, StringComparison.OrdinalIgnoreCase) && !m.Marca.Sensivel))
            .Select(m => $"{m.Tipo.Name}.{m.Propriedade.Name}")
            .ToList();
        Assert.True(achadas.Count == 0, "Propriedade de segredo marcada com [Auditar]: " + string.Join(", ", achadas));

        // E a senha do usuário, em particular, nunca leva a marca.
        Assert.Null(typeof(Usuario).GetProperty(nameof(Usuario.SenhaHash))!.GetCustomAttribute<AuditarAttribute>());
        Assert.Null(typeof(UsuarioTokenSenha).GetProperty(nameof(UsuarioTokenSenha.TokenHash))!.GetCustomAttribute<AuditarAttribute>());
        Assert.Null(typeof(RefreshToken).GetProperty(nameof(RefreshToken.TokenHash))!.GetCustomAttribute<AuditarAttribute>());
    }

    [Fact]
    public void Toda_entidade_Auditado_implementa_IAuditavel()
    {
        var auditadas = Dominio.GetTypes().Where(t => t.GetCustomAttribute<AuditadoAttribute>() is not null).ToList();

        Assert.True(auditadas.Count >= 4, "Funcionario, Setor, Cargo e Beneficio são auditados nesta fatia.");
        Assert.All(auditadas, t => Assert.True(typeof(IAuditavel).IsAssignableFrom(t), $"{t.Name} é [Auditado] e não implementa IAuditavel."));
        Assert.All(auditadas, t => Assert.False(string.IsNullOrWhiteSpace(t.GetCustomAttribute<AuditadoAttribute>()!.Rotulo), $"{t.Name} tem rótulo vazio."));
    }

    [Fact]
    public void Funcionario_Setor_Cargo_e_Beneficio_levam_as_marcas_da_fatia()
    {
        static string[] Campos(Type tipo) =>
            [.. tipo.GetProperties(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance)
                .Select(p => p.GetCustomAttribute<AuditarAttribute>()?.Campo)
                .OfType<string>()
                .Order()];

        Assert.Equal(AuditoriaArea.Colaboradores, typeof(Funcionario).GetCustomAttribute<AuditadoAttribute>()!.Area);
        Assert.Equal("Colaborador", typeof(Funcionario).GetCustomAttribute<AuditadoAttribute>()!.Rotulo);
        // A 1b acrescentou: Data de nascimento (sensível), Nível de acesso, Tipo de contrato e Último dia de trabalho (FuncionarioCadastroTests.Marcas_de_auditoria_da_1b os detalha).
        Assert.Equal(
            new[] { "Cargo", "Data de admissão", "Data de nascimento", "E-mail corporativo", "Matrícula", "Nível de acesso", "Nome", "Setor", "Situação", "Tipo de contrato", "Último dia de trabalho" }.Order(),
            Campos(typeof(Funcionario)));

        Assert.Equal(AuditoriaArea.Cadastros, typeof(Setor).GetCustomAttribute<AuditadoAttribute>()!.Area);
        Assert.Equal(new[] { "Gestor", "Nome" }.Order(), Campos(typeof(Setor)));
        Assert.Equal(AuditoriaArea.Cadastros, typeof(Cargo).GetCustomAttribute<AuditadoAttribute>()!.Area);
        Assert.Equal(["Nome"], Campos(typeof(Cargo)));
        Assert.Equal(AuditoriaArea.Cadastros, typeof(Beneficio).GetCustomAttribute<AuditadoAttribute>()!.Area);
        Assert.Equal(new[] { "Aceita dependentes", "Aceita trajeto", "Descrição", "Nome", "Opcional" }.Order(), Campos(typeof(Beneficio)));
    }

    [Fact]
    public void Funcionario_diz_o_nome_e_o_registro_afetado_com_e_sem_matricula()
    {
        var pessoa = Funcionario.Criar("Bruno Lima", "bruno.lima@empresa.com.br", new DateOnly(2026, 1, 2), new NivelDeAcesso("Funcionario"));

        Assert.Equal("Bruno Lima", pessoa.NomeParaAuditoria);
        Assert.Equal("Colaborador Bruno Lima", pessoa.RegistroAfetado);

        pessoa.AtribuirMatricula("00482");

        Assert.Equal("Colaborador Bruno Lima, matrícula 00482", pessoa.RegistroAfetado);
    }
}
