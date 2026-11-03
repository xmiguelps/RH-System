using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class UsuarioConfiguracao : IEntityTypeConfiguration<Usuario>
{
    public void Configure(EntityTypeBuilder<Usuario> builder)
    {
        builder.ToTable("USUARIOS");
        builder.HasKey(u => u.Id);
        builder.Property(u => u.Id).HasColumnName("ID_USUARIO");
        builder.Property(u => u.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(u => u.SenhaHash).HasColumnName("SENHA");
        builder.Property(u => u.Ativo).HasColumnName("ATIVO");

        // DEFAULT FALSE no banco, não declarado: o EF sempre envia a marca que a entidade tem.
        builder.Property(u => u.SenhaProvisoria).HasColumnName("SENHA_PROVISORIA");

        // Coluna do grupo (DATETIME em -03:00): a aplicação guarda UTC e o conversor faz a conta.
        builder.Property(u => u.UltimoLoginEm).HasColumnName("DATA_HORA_ULTIMO_LOGIN").HasConversion(ConversoresDeTempo.Brasil);
        builder.Ignore(u => u.PodeEntrar);

        builder.HasOne(u => u.Funcionario).WithMany().HasForeignKey(u => u.FuncionarioId);
        builder.HasIndex(u => u.FuncionarioId).IsUnique();
    }
}
