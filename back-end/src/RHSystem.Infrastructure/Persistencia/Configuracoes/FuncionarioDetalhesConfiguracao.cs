using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FuncionarioDetalhesConfiguracao : IEntityTypeConfiguration<FuncionarioDetalhes>
{
    public void Configure(EntityTypeBuilder<FuncionarioDetalhes> builder)
    {
        builder.ToTable("FUNCIONARIOS_DETALHES");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID_FUNCIONARIO_DETALHES");
        builder.Property(d => d.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // CPF, CEP e UF são CHAR no banco (tamanho fixo): o tipo vai explícito para o EF não mandar o parâmetro como VARCHAR/TEXT. O trigger do banco
        // confere o CPF e põe a UF em maiúsculas.
        builder.Property(d => d.Cpf).HasColumnName("CPF").HasColumnType("char(11)");
        builder.Property(d => d.Rg).HasColumnName("RG");
        builder.Property(d => d.RgOrgaoEmissor).HasColumnName("RG_ORGAO_EMISSOR");
        builder.Property(d => d.EmailPessoal).HasColumnName("EMAIL_PESSOAL");
        builder.Property(d => d.Telefone).HasColumnName("TELEFONE");
        builder.Property(d => d.Cep).HasColumnName("CEP").HasColumnType("char(8)");
        builder.Property(d => d.Logradouro).HasColumnName("LOGRADOURO");
        builder.Property(d => d.Numero).HasColumnName("NUMERO");
        builder.Property(d => d.Complemento).HasColumnName("COMPLEMENTO");
        builder.Property(d => d.Bairro).HasColumnName("BAIRRO");
        builder.Property(d => d.Cidade).HasColumnName("CIDADE");
        builder.Property(d => d.Uf).HasColumnName("UF").HasColumnType("char(2)");

        // Um para um: a chave única de ID_FUNCIONARIO no banco (UQ_FUNCIONARIOS_DETALHES_FUNCIONARIO).
        builder.HasOne(d => d.Funcionario).WithOne(f => f.Detalhes).HasForeignKey<FuncionarioDetalhes>(d => d.FuncionarioId);
    }
}
