using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class GestorConfiguracao : IEntityTypeConfiguration<Gestor>
{
    public void Configure(EntityTypeBuilder<Gestor> builder)
    {
        builder.ToTable("GESTORES");
        builder.HasKey(g => g.Id);
        builder.Property(g => g.Id).HasColumnName("ID_GESTOR");
        builder.Property(g => g.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // APROVACAO_AUTOMATICA e APROVACAO_AUTOMATICA_ATE entram com a fatia 1c: até lá o EF não as envia e vale o padrão do banco (FALSE e NULL).
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(g => g.FuncionarioId);
    }
}
