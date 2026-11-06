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

        builder.Ignore(g => g.NomeParaAuditoria);
        builder.Ignore(g => g.RegistroAfetado);

        // A ausência ("Gestor ausente"): a chave e a data de volta. O padrão do banco (FALSE) NÃO é declarado, para o EF sempre enviar o valor que a entidade tem; a data só existe com a chave ligada (CHECK do V001).
        builder.Property(g => g.Ausente).HasColumnName("APROVACAO_AUTOMATICA");
        builder.Property(g => g.AusenteAte).HasColumnName("APROVACAO_AUTOMATICA_ATE");
        builder.HasOne(g => g.Funcionario).WithMany().HasForeignKey(g => g.FuncionarioId);
    }
}
