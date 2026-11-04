using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class CargoConfiguracao : IEntityTypeConfiguration<Cargo>
{
    public void Configure(EntityTypeBuilder<Cargo> builder)
    {
        builder.ToTable("CARGOS");
        builder.Ignore(c => c.NomeParaAuditoria);
        builder.Ignore(c => c.RegistroAfetado);
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID_CARGO");
        builder.Property(c => c.Nome).HasColumnName("NOME_CARGO");
    }
}
