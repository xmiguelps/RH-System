using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class FeriadoConfiguracao : IEntityTypeConfiguration<Feriado>
{
    public void Configure(EntityTypeBuilder<Feriado> builder)
    {
        builder.ToTable("FERIADOS");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID_FERIADO");
        builder.Property(f => f.Nome).HasColumnName("NOME_FERIADO");
        builder.Property(f => f.Data).HasColumnName("DATA_FERIADO");
        builder.Property(f => f.Tipo).HasColumnName("TIPO_FERIADO").HasConversion(new EnumComoTextoMaiusculo<TipoDeFeriado>());
        builder.Property(f => f.Uf).HasColumnName("UF").HasColumnType("char(2)");
        builder.Property(f => f.Municipio).HasColumnName("MUNICIPIO");

        // Os padrões do banco (ATIVO = TRUE, REPETE_TODO_ANO = FALSE) não são declarados: o EF sempre envia o valor, inclusive o "falso".
        builder.Property(f => f.Ativo).HasColumnName("ATIVO");
        builder.Property(f => f.Descricao).HasColumnName("DESCRICAO");
        builder.Property(f => f.RepeteTodoAno).HasColumnName("REPETE_TODO_ANO");
    }
}
