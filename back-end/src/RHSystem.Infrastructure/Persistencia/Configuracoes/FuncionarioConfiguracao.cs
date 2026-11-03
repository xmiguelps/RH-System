using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FuncionarioConfiguracao : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> builder)
    {
        builder.ToTable("FUNCIONARIOS");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID_FUNCIONARIO");
        builder.Property(f => f.Nome).HasColumnName("NOME");
        builder.Property(f => f.EmailCorporativo).HasColumnName("EMAIL_CORPORATIVO");
        builder.Property(f => f.DataAdmissao).HasColumnName("DATA_ADMISSAO");
        builder.Property(f => f.Ativo).HasColumnName("ATIVO");
        builder.Property(f => f.Matricula).HasColumnName("MATRICULA");
        builder.Ignore(f => f.Perfil);

        // Nível, cargo e setor aceitam NULL no banco: a chave estrangeira é opcional. O nível fica como propriedade de sombra (o
        // domínio só usa a navegação); cargo e setor têm a chave como propriedade (CargoId e SetorId), que o cadastro de colaboradores usa.
        builder.Property<int?>("IdNivelDeAcesso").HasColumnName("ID_NIVEL_ACESSO");
        builder.HasOne(f => f.NivelDeAcesso).WithMany().HasForeignKey("IdNivelDeAcesso").IsRequired(false);
        builder.Property(f => f.CargoId).HasColumnName("ID_CARGO");
        builder.HasOne(f => f.Cargo).WithMany().HasForeignKey(f => f.CargoId).IsRequired(false);
        builder.Property(f => f.SetorId).HasColumnName("ID_SETOR");
        builder.HasOne(f => f.Setor).WithMany().HasForeignKey(f => f.SetorId).IsRequired(false);
    }
}
