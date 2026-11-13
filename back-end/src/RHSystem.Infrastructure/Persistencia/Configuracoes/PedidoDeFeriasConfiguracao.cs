using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Ferias;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// <c>FERIAS</c>. Tabela do grupo: -03:00 no banco, UTC na aplicação; a aplicação grava TODAS as datas pelo relógio dela (o trigger só preenche <c>DATA_SOLICITACAO</c> e <c>DATA_ANALISE</c> quando vêm nulas, e com data de outro relógio o <c>CHECK DATA_ANALISE &gt;= DATA_SOLICITACAO</c> recusaria a decisão).
/// Nenhum padrão do banco é declarado (<c>STATUS_FERIAS = 'SOLICITADA'</c>, abono, automática, 13º): o registro direto do RH grava <c>APROVADA</c> e o EF sempre envia o valor. Sem <c>[Auditado]</c>: os eventos são escritos à mão, um por fato (as decisões são <c>ExecuteUpdate</c>).
/// </summary>
public class PedidoDeFeriasConfiguracao : IEntityTypeConfiguration<PedidoDeFerias>
{
    public void Configure(EntityTypeBuilder<PedidoDeFerias> builder)
    {
        builder.ToTable("FERIAS");
        builder.HasKey(f => f.Id);
        builder.Property(f => f.Id).HasColumnName("ID_FERIAS");
        builder.Property(f => f.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(f => f.InicioDoPeriodoAquisitivo).HasColumnName("DATA_INICIO_PERIODO_AQUISITIVO");
        builder.Property(f => f.FimDoPeriodoAquisitivo).HasColumnName("DATA_FIM_PERIODO_AQUISITIVO");
        builder.Property(f => f.DataInicio).HasColumnName("DATA_INICIO");
        builder.Property(f => f.DataFim).HasColumnName("DATA_FIM");
        builder.Property(f => f.AbonoPecuniario).HasColumnName("ABONO_PECUNIARIO");
        builder.Property(f => f.DiasAbono).HasColumnName("DIAS_ABONO");
        builder.Property(f => f.Status).HasColumnName("STATUS_FERIAS").HasConversion(new EnumComoTextoMaiusculo<StatusDasFerias>());
        builder.Property(f => f.SolicitadaEmUtc).HasColumnName("DATA_SOLICITACAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.AnalisadaEmUtc).HasColumnName("DATA_ANALISE").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.GestorId).HasColumnName("ID_GESTOR");
        builder.Property(f => f.Observacao).HasColumnName("OBSERVACAO");
        builder.Property(f => f.ResultadoDoGestor).HasColumnName("RESULTADO_GESTOR").HasConversion(new EnumComoTextoMaiusculo<ResultadoDoGestorNasFerias>());
        builder.Property(f => f.EtapaDoGestorEmUtc).HasColumnName("DATA_ETAPA_GESTOR").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.EtapaDoGestorAutomatica).HasColumnName("ETAPA_GESTOR_AUTOMATICA");
        builder.Property(f => f.FuncionarioRhId).HasColumnName("ID_FUNCIONARIO_RH");
        builder.Property(f => f.ConfirmadaPeloRhEmUtc).HasColumnName("DATA_CONFIRMACAO_RH").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.Adiantar13Salario).HasColumnName("ADIANTAR_13_SALARIO");
        builder.Property(f => f.SugestaoDataInicio).HasColumnName("SUGESTAO_DATA_INICIO");
        builder.Property(f => f.SugestaoDataFim).HasColumnName("SUGESTAO_DATA_FIM");
        builder.Property(f => f.RespostaDaSugestao).HasColumnName("RESPOSTA_SUGESTAO").HasConversion(new EnumComoTextoMaiusculo<RespostaDaSugestao>());
        builder.Property(f => f.RespostaDaSugestaoEmUtc).HasColumnName("DATA_RESPOSTA_SUGESTAO").HasConversion(ConversoresDeTempo.Brasil);
        builder.Property(f => f.MotivoRecusa).HasColumnName("MOTIVO_RECUSA");

        builder.HasOne<Funcionario>().WithMany().HasForeignKey(f => f.FuncionarioId);
        builder.HasOne<Gestor>().WithMany().HasForeignKey(f => f.GestorId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(f => f.FuncionarioRhId);
    }
}
