using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class RefreshTokenConfiguracao : IEntityTypeConfiguration<RefreshToken>
{
    public void Configure(EntityTypeBuilder<RefreshToken> builder)
    {
        builder.ToTable("REFRESH_TOKENS");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID_REFRESH_TOKEN");
        builder.Property(r => r.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(r => r.TokenHash).HasColumnName("TOKEN_HASH").HasColumnType("char(64)");

        // CRIADO_EM tem DEFAULT (UTC_TIMESTAMP(6)) no banco; a aplicação sempre informa o valor (relógio dela). Mapeado como gerado na
        // inclusão: sem isso o EF mandaria 0001-01-01 quando o valor não fosse preenchido.
        builder.Property(r => r.CriadoEm).HasColumnName("CRIADO_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(r => r.ExpiraEm).HasColumnName("EXPIRA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(r => r.RevogadoEm).HasColumnName("REVOGADO_EM").HasConversion(ConversoresDeTempo.Utc);

        builder.HasOne<Usuario>().WithMany().HasForeignKey(r => r.UsuarioId);
        builder.HasIndex(r => r.TokenHash).IsUnique();
    }
}
