using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class SetorConfiguracao : IEntityTypeConfiguration<Setor>
{
    public void Configure(EntityTypeBuilder<Setor> builder)
    {
        builder.ToTable("SETORES");
        builder.HasKey(s => s.Id);
        builder.Property(s => s.Id).HasColumnName("ID_SETOR");
        builder.Property(s => s.Nome).HasColumnName("NOME_SETOR");
    }
}
