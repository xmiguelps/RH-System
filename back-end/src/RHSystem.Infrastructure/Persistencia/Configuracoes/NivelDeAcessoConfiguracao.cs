using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class NivelDeAcessoConfiguracao : IEntityTypeConfiguration<NivelDeAcesso>
{
    public void Configure(EntityTypeBuilder<NivelDeAcesso> builder)
    {
        builder.ToTable("NIVEIS_DE_ACESSO");
        builder.HasKey(n => n.Id);
        builder.Property(n => n.Id).HasColumnName("ID_NIVEL_ACESSO");
        builder.Property(n => n.Nome).HasColumnName("NOME_NIVEL_ACESSO");
    }
}
