using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

public class PedidoDeAjusteConfiguracao : IEntityTypeConfiguration<PedidoDeAjuste>
{
    public void Configure(EntityTypeBuilder<PedidoDeAjuste> builder)
    {
        builder.ToTable("PONTOS_MANUTENCOES");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID_MANUTENCAO_PONTO");
        builder.Property(p => p.PontoId).HasColumnName("ID_PONTO");
        builder.Property(p => p.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(p => p.Justificativa).HasColumnName("JUSTIFICATIVA");

        // Tabela do grupo: -03:00 no banco, UTC na aplicação. A aplicação grava TODAS as datas pelo relógio dela (o trigger só preenche DATA_SOLICITACAO e DATA_ANALISE quando vêm nulas: com o relógio falso dos testes o pedido
        // nasceria "depois" de decidido e o CHECK DATA_ANALISE >= DATA_SOLICITACAO o recusaria).
        builder.Property(p => p.SolicitadoEmUtc).HasColumnName("DATA_SOLICITACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(p => p.Status).HasColumnName("STATUS_SOLICITACAO").HasConversion(new EnumComoTextoMaiusculo<StatusDoPedidoDeAjuste>());
        builder.Property(p => p.AnalisadoEmUtc).HasColumnName("DATA_ANALISE").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(p => p.GestorId).HasColumnName("ID_GESTOR");
        builder.Property(p => p.Observacao).HasColumnName("OBSERVACAO");
        builder.Property(p => p.ResultadoDoGestor).HasColumnName("RESULTADO_GESTOR").HasConversion(new EnumComoTextoMaiusculo<ResultadoDaEtapaDoGestor>());
        builder.Property(p => p.EtapaDoGestorEmUtc).HasColumnName("DATA_ETAPA_GESTOR").HasConversion(ConversoresDeTempo.Brasil);

        // O padrão do banco (FALSE) não é declarado: o EF sempre envia o valor.
        builder.Property(p => p.EtapaDoGestorAutomatica).HasColumnName("ETAPA_GESTOR_AUTOMATICA");
        builder.Property(p => p.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");
        builder.Property(p => p.ConfirmadoPeloRhEmUtc).HasColumnName("DATA_CONFIRMACAO_RH").HasConversion(ConversoresDeTempo.Brasil);

        // Coluna GERADA (= ID_PONTO enquanto PENDENTE): fora do INSERT e do UPDATE. A chave única dela é o "um pedido pendente por dia".
        builder.Property(p => p.ChavePendente).HasColumnName("CHAVE_PENDENTE").CalculadaNoBanco();

        builder.HasMany(p => p.Itens).WithOne().HasForeignKey(i => i.PedidoId);
        builder.Navigation(p => p.Itens).UsePropertyAccessMode(PropertyAccessMode.Field);
        builder.HasOne<PontoDoDia>().WithMany().HasForeignKey(p => p.PontoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.FuncionarioId);
    }
}

public class ItemDoPedidoDeAjusteConfiguracao : IEntityTypeConfiguration<ItemDoPedidoDeAjuste>
{
    public void Configure(EntityTypeBuilder<ItemDoPedidoDeAjuste> builder)
    {
        builder.ToTable("PONTOS_MANUTENCOES_ITENS");
        builder.HasKey(i => i.Id);
        builder.Property(i => i.Id).HasColumnName("ID_MANUTENCAO_PONTO_ITEM");
        builder.Property(i => i.PedidoId).HasColumnName("ID_MANUTENCAO_PONTO");
        builder.Property(i => i.PontoId).HasColumnName("ID_PONTO");
        builder.Property(i => i.MarcacaoId).HasColumnName("ID_MARCACAO");
        builder.Property(i => i.InstanteProposto).HasColumnName("DATA_HORA_PROPOSTA").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(i => i.Tipo).HasColumnName("TIPO_MARCACAO").HasConversion(new EnumComoTextoMaiusculo<TipoDeMarcacao>());
    }
}
