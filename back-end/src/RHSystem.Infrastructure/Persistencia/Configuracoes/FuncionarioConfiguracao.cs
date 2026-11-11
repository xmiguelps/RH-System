using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FuncionarioConfiguracao : IEntityTypeConfiguration<Funcionario>
{
    public void Configure(EntityTypeBuilder<Funcionario> builder)
    {
        builder.ToTable("FUNCIONARIOS");
        builder.Ignore(f => f.NomeParaAuditoria);
        builder.Ignore(f => f.RegistroAfetado);
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID_FUNCIONARIO");
        builder.Property(f => f.Nome).HasColumnName("NOME");
        builder.Property(f => f.EmailCorporativo).HasColumnName("EMAIL_CORPORATIVO");
        builder.Property(f => f.DataAdmissao).HasColumnName("DATA_ADMISSAO");
        builder.Property(f => f.Ativo).HasColumnName("ATIVO");
        builder.Property(f => f.Matricula).HasColumnName("MATRICULA");
        builder.Ignore(f => f.Perfil);

        // Datas de calendário (DATE): DateOnly sem conversor de fuso. O tipo de contrato é texto livre no banco, sem CHECK (nota do V001): TiposDeContrato converte.
        builder.Property(f => f.DataNascimento).HasColumnName("DATA_NASCIMENTO");
        builder.Property(f => f.DataDesligamento).HasColumnName("DATA_DESLIGAMENTO");
        builder.Property(f => f.TipoContrato).HasColumnName("TIPO_CONTRATO");

        // O gestor do colaborador (ID_GESTOR, de GESTORES); o trigger do banco recusa gestor igual à pessoa ou inativo.
        builder.Property(f => f.GestorId).HasColumnName("ID_GESTOR");
        builder.HasOne<Gestor>().WithMany().HasForeignKey(f => f.GestorId).IsRequired(false);

        // Nível, cargo e setor aceitam NULL no banco: a chave estrangeira é opcional. As três têm a chave como propriedade (NivelDeAcessoId, CargoId e SetorId), que
        // o cadastro de colaboradores usa e a auditoria lê para mostrar o NOME do apontado.
        builder.Property(f => f.NivelDeAcessoId).HasColumnName("ID_NIVEL_ACESSO");
        builder.HasOne(f => f.NivelDeAcesso).WithMany().HasForeignKey(f => f.NivelDeAcessoId).IsRequired(false);
        builder.Property(f => f.CargoId).HasColumnName("ID_CARGO");
        builder.HasOne(f => f.Cargo).WithMany().HasForeignKey(f => f.CargoId).IsRequired(false);
        builder.Property(f => f.SetorId).HasColumnName("ID_SETOR");
        builder.HasOne(f => f.Setor).WithMany().HasForeignKey(f => f.SetorId).IsRequired(false);
    }
}
