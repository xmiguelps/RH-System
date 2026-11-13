using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// As seis tabelas do TAD. <b>Nenhuma destas entidades tem <c>[Auditado]</c> nem é auditada pelo interceptor</b>: publicar cria uma linha por pessoa em lote e a conclusão cria o certificado por trigger; os eventos são escritos à mão (um por fato). Não mapeadas, de propósito: <c>TREINAMENTOS.DATA_FIM</c>, <c>AVISA_POR_EMAIL</c> e
/// <c>LEMBRA_ANTES_DO_FIM</c> (sem prazo, sem lembrete, sem e-mail), <c>TREINAMENTOS_FUNCIONARIOS.NOTA</c> (sem nota) e a coluna gerada <c>CHAVE_DESTINO</c>. Os padrões <c>TRUE</c> do banco em <c>EMITE_CERTIFICADO</c> e <c>INCLUI_ADMITIDOS</c> NÃO são declarados: o "falso" também é enviado.
/// </summary>
public class TreinamentoConfiguracao : IEntityTypeConfiguration<Treinamento>
{
    public void Configure(EntityTypeBuilder<Treinamento> builder)
    {
        builder.ToTable("TREINAMENTOS");
        builder.HasKey(t => t.Id);
        builder.Property(t => t.Id).HasColumnName("ID_TREINAMENTO");
        builder.Property(t => t.Titulo).HasColumnName("TITULO");
        builder.Property(t => t.Descricao).HasColumnName("DESCRICAO");
        builder.Property(t => t.TipoTreinamento).HasColumnName("TIPO_TREINAMENTO");
        builder.Property(t => t.ComecaEm).HasColumnName("DATA_INICIO");
        builder.Property(t => t.Obrigatorio).HasColumnName("OBRIGATORIO");
        builder.Property(t => t.Publicado).HasColumnName("PUBLICADO");
        builder.Property(t => t.Publico).HasColumnName("PUBLICO").HasConversion(new EnumComoTextoMaiusculo<PublicoDoTreinamento>());
        builder.Property(t => t.EmiteCertificado).HasColumnName("EMITE_CERTIFICADO");
        builder.Property(t => t.IncluiAdmitidos).HasColumnName("INCLUI_ADMITIDOS");
        builder.Property(t => t.EncerradoEmUtc).HasColumnName("ENCERRADO_EM").HasConversion(ConversoresDeTempo.Brasil);
        builder.Ignore(t => t.Onboarding);
    }
}

public class TreinamentoModuloConfiguracao : IEntityTypeConfiguration<TreinamentoModulo>
{
    public void Configure(EntityTypeBuilder<TreinamentoModulo> builder)
    {
        builder.ToTable("TREINAMENTOS_MODULOS");
        builder.HasKey(m => m.Id);
        builder.Property(m => m.Id).HasColumnName("ID_MODULO");
        builder.Property(m => m.TreinamentoId).HasColumnName("ID_TREINAMENTO");
        builder.Property(m => m.Ordem).HasColumnName("ORDEM");
        builder.Property(m => m.Titulo).HasColumnName("TITULO");
        builder.Property(m => m.Tipo).HasColumnName("TIPO_MODULO").HasConversion(new EnumComoTextoMaiusculo<TipoDeModulo>());
        builder.Property(m => m.DuracaoMinutos).HasColumnName("DURACAO_MINUTOS");
        builder.Property(m => m.UrlExterna).HasColumnName("URL_EXTERNA");
        builder.Property(m => m.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");
        builder.Property(m => m.ConteudoTexto).HasColumnName("CONTEUDO_TEXTO");
        builder.Property(m => m.NomeArquivoOriginal).HasColumnName("NOME_ARQUIVO_ORIGINAL");

        builder.HasOne<Treinamento>().WithMany().HasForeignKey(m => m.TreinamentoId);
    }
}

public class TreinamentoModuloProgressoConfiguracao : IEntityTypeConfiguration<TreinamentoModuloProgresso>
{
    public void Configure(EntityTypeBuilder<TreinamentoModuloProgresso> builder)
    {
        builder.ToTable("TREINAMENTOS_MODULOS_PROGRESSO");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID_MODULO_PROGRESSO");
        builder.Property(p => p.ModuloId).HasColumnName("ID_MODULO");
        builder.Property(p => p.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // DEFAULT CURRENT_TIMESTAMP no banco: o EF o trata como gerado na inclusão, mas ENVIA o valor quando preenchido, e a aplicação o preenche SEMPRE com o relógio dela.
        builder.Property(p => p.ConcluidoEmUtc).HasColumnName("DATA_CONCLUSAO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd();

        builder.HasOne<TreinamentoModulo>().WithMany().HasForeignKey(p => p.ModuloId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.FuncionarioId);
    }
}

public class TreinamentoPublicoConfiguracao : IEntityTypeConfiguration<TreinamentoPublico>
{
    public void Configure(EntityTypeBuilder<TreinamentoPublico> builder)
    {
        builder.ToTable("TREINAMENTOS_PUBLICO");
        builder.HasKey(p => p.Id);
        builder.Property(p => p.Id).HasColumnName("ID_TREINAMENTO_PUBLICO");
        builder.Property(p => p.TreinamentoId).HasColumnName("ID_TREINAMENTO");
        builder.Property(p => p.SetorId).HasColumnName("ID_SETOR");
        builder.Property(p => p.FuncionarioId).HasColumnName("ID_FUNCIONARIO");

        // CHAVE_DESTINO é GERADA pelo banco (STORED) e não é mapeada.
        builder.HasOne<Treinamento>().WithMany().HasForeignKey(p => p.TreinamentoId);
        builder.HasOne<Setor>().WithMany().HasForeignKey(p => p.SetorId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.FuncionarioId);
    }
}

public class TreinamentoFuncionarioConfiguracao : IEntityTypeConfiguration<TreinamentoFuncionario>
{
    public void Configure(EntityTypeBuilder<TreinamentoFuncionario> builder)
    {
        builder.ToTable("TREINAMENTOS_FUNCIONARIOS");
        builder.HasKey(l => l.Id);
        builder.Property(l => l.Id).HasColumnName("ID_TREINAMENTO_FUNCIONARIO");
        builder.Property(l => l.TreinamentoId).HasColumnName("ID_TREINAMENTO");
        builder.Property(l => l.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(l => l.Situacao).HasColumnName("STATUS_TREINAMENTO").HasConversion(new EnumComoTextoMaiusculoTolerante<SituacaoNoTreinamento>()); // REPROVADO e CANCELADO (o banco aceita, a API não usa) leem como null
        builder.Property(l => l.IniciadoEm).HasColumnName("DATA_INICIO");
        builder.Property(l => l.ConcluidoEm).HasColumnName("DATA_CONCLUSAO");

        builder.HasOne<Treinamento>().WithMany().HasForeignKey(l => l.TreinamentoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(l => l.FuncionarioId);
    }
}

public class TreinamentoCertificadoConfiguracao : IEntityTypeConfiguration<TreinamentoCertificado>
{
    public void Configure(EntityTypeBuilder<TreinamentoCertificado> builder)
    {
        builder.ToTable("TREINAMENTOS_CERTIFICADOS");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID_CERTIFICADO");
        builder.Property(c => c.TreinamentoId).HasColumnName("ID_TREINAMENTO");
        builder.Property(c => c.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(c => c.EmitidoEm).HasColumnName("DATA_EMISSAO");
        builder.Property(c => c.CodigoValidacao).HasColumnName("CODIGO_VALIDACAO");
        builder.Property(c => c.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");

        builder.HasOne<Treinamento>().WithMany().HasForeignKey(c => c.TreinamentoId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(c => c.FuncionarioId);
    }
}
