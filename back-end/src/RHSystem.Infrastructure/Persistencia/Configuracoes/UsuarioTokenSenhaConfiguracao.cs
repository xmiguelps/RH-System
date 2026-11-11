using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class UsuarioTokenSenhaConfiguracao : IEntityTypeConfiguration<UsuarioTokenSenha>
{
    public void Configure(EntityTypeBuilder<UsuarioTokenSenha> builder)
    {
        builder.ToTable("USUARIOS_TOKENS_SENHA");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID_USUARIO_TOKEN_SENHA");
        builder.Property(t => t.UsuarioId).HasColumnName("ID_USUARIO");
        builder.Property(t => t.Tipo).HasColumnName("TIPO").HasConversion(new EnumComoTextoMaiusculo<TipoDeTokenDeSenha>());
        builder.Property(t => t.TokenHash).HasColumnName("TOKEN_HASH").HasColumnType("char(64)");

        // Tabela só da aplicação: UTC. A aplicação sempre informa CRIADO_EM (relógio dela); o DEFAULT do banco é rede de segurança.
        builder.Property(t => t.CriadoEm).HasColumnName("CRIADO_EM").HasConversion(ConversoresDeTempo.Utc).ValueGeneratedOnAdd();
        builder.Property(t => t.ExpiraEm).HasColumnName("EXPIRA_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(t => t.UsadoEm).HasColumnName("USADO_EM").HasConversion(ConversoresDeTempo.Utc);
        builder.Property(t => t.InvalidadoEm).HasColumnName("INVALIDADO_EM").HasConversion(ConversoresDeTempo.Utc);

        // 1 enquanto o link não foi usado nem invalidado, NULL depois: é o que faz a chave única valer só para o link em aberto.
        builder.Property<sbyte?>("EmAberto").HasColumnName("EM_ABERTO").CalculadaNoBanco();

        builder.HasOne<Usuario>().WithMany().HasForeignKey(t => t.UsuarioId);
    }
}
