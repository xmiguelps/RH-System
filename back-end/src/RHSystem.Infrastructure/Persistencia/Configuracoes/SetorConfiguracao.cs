using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class SetorConfiguracao : IEntityTypeConfiguration<Setor>
{
    public void Configure(EntityTypeBuilder<Setor> builder)
    {
        builder.ToTable("SETORES");
        builder.Ignore(s => s.NomeParaAuditoria);
        builder.Ignore(s => s.RegistroAfetado);
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID_SETOR");
        builder.Property(s => s.Nome).HasColumnName("NOME_SETOR");
        builder.Property(s => s.GestorId).HasColumnName("ID_GESTOR");

        // Um setor pode estar sem gestor (ID_GESTOR aceita NULL).
        builder.HasOne<Gestor>().WithMany().HasForeignKey(s => s.GestorId).IsRequired(false);
    }
}
