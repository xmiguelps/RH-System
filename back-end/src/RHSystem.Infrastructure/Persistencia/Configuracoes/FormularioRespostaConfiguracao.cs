using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;
using RHSystem.Domain.Formularios;

namespace RHSystem.Infrastructure.Persistencia.Configuracoes;

/// <summary>
/// As respostas e as tabelas que as cercam. <b>Nenhuma destas entidades tem <c>[Auditado]</c> nem é auditada pelo interceptor</b> (ele gravaria autor e hora de todo INSERT): as respostas anônimas e as participações não deixam rastro de quem respondeu. As chaves estrangeiras compostas do banco
/// (pergunta e formulário; formulário e anonimato; candidato e formulário; alternativa e pergunta) não são modeladas: o banco as confere e a aplicação grava sempre os pares certos.
/// </summary>
public class FormularioRespostaConfiguracao : IEntityTypeConfiguration<FormularioResposta>
{
    public void Configure(EntityTypeBuilder<FormularioResposta> builder)
    {
        builder.ToTable("FORMULARIOS_RESPOSTAS");
        builder.HasKey(r => r.Id);
        builder.Property(r => r.Id).HasColumnName("ID_FORMULARIO_RESPOSTA");
        builder.Property(r => r.PerguntaId).HasColumnName("ID_FORMULARIO_PERGUNTA");
        builder.Property(r => r.FormularioId).HasColumnName("ID_FORMULARIO");
        builder.Property(r => r.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(r => r.AvaliadoId).HasColumnName("ID_AVALIADO");
        builder.Property(r => r.CandidatoId).HasColumnName("ID_FORMULARIO_CANDIDATO");
        builder.Property(r => r.AlternativaId).HasColumnName("ID_ALTERNATIVA");
        builder.Property(r => r.Resposta).HasColumnName("RESPOSTA");
        builder.Property(r => r.CaminhoArquivo).HasColumnName("CAMINHO_ARQUIVO");
        builder.Property(r => r.NomeArquivoOriginal).HasColumnName("NOME_ARQUIVO_ORIGINAL");

        // A data é sempre da aplicação (o trigger poria o NOW() real, com a hora: nunca no anônimo). -03:00 no banco, UTC aqui.
        builder.Property(r => r.RespondidaEmUtc).HasColumnName("DATA_RESPOSTA").HasConversion(ConversoresDeTempo.Brasil);

        // O padrão do banco (ANONIMO = FALSE) não é declarado: o EF envia sempre o valor, e ele tem de ser igual ao do formulário.
        builder.Property(r => r.Anonimo).HasColumnName("ANONIMO");

        builder.HasOne<Formulario>().WithMany().HasForeignKey(r => r.FormularioId);
        builder.HasOne<FormularioPergunta>().WithMany().HasForeignKey(r => r.PerguntaId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(r => r.FuncionarioId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(r => r.AvaliadoId);
        builder.HasOne<FormularioCandidato>().WithMany().HasForeignKey(r => r.CandidatoId);
        builder.HasOne<FormularioAlternativa>().WithMany().HasForeignKey(r => r.AlternativaId);
    }
}

public class FormularioCandidatoConfiguracao : IEntityTypeConfiguration<FormularioCandidato>
{
    public void Configure(EntityTypeBuilder<FormularioCandidato> builder)
    {
        builder.ToTable("FORMULARIOS_CANDIDATOS");
        builder.HasKey(c => c.Id);
        builder.Property(c => c.Id).HasColumnName("ID_FORMULARIO_CANDIDATO");
        builder.Property(c => c.FormularioId).HasColumnName("ID_FORMULARIO");
        builder.Property(c => c.Nome).HasColumnName("NOME");
        builder.Property(c => c.Email).HasColumnName("EMAIL");
        builder.Property(c => c.Telefone).HasColumnName("TELEFONE");
        builder.Property(c => c.CaminhoCurriculo).HasColumnName("CAMINHO_CURRICULO");

        // DEFAULT CURRENT_TIMESTAMP no banco: o EF o trata como gerado na inclusão (ConformidadeDoModeloTests), mas ENVIA o valor quando ele está preenchido, e a aplicação o preenche SEMPRE
        // com o relógio dela (o relógio real do servidor quebraria a retenção e a ordem nos testes).
        builder.Property(c => c.CadastradoEmUtc).HasColumnName("DATA_CADASTRO").HasConversion(ConversoresDeTempo.Brasil).ValueGeneratedOnAdd();

        builder.HasOne<Formulario>().WithMany().HasForeignKey(c => c.FormularioId);
    }
}

public class FormularioDestinatarioConfiguracao : IEntityTypeConfiguration<FormularioDestinatario>
{
    public void Configure(EntityTypeBuilder<FormularioDestinatario> builder)
    {
        builder.ToTable("FORMULARIOS_DESTINATARIOS");
        builder.HasKey(d => d.Id);
        builder.Property(d => d.Id).HasColumnName("ID_FORMULARIO_DESTINATARIO");
        builder.Property(d => d.FormularioId).HasColumnName("ID_FORMULARIO");
        builder.Property(d => d.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(d => d.SetorId).HasColumnName("ID_SETOR");
        builder.Property(d => d.AvaliadoId).HasColumnName("ID_AVALIADO");

        // CHAVE_DESTINO é GERADA pelo banco (STORED) e não é mapeada.
        builder.HasOne<Formulario>().WithMany().HasForeignKey(d => d.FormularioId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(d => d.FuncionarioId);
        builder.HasOne<Setor>().WithMany().HasForeignKey(d => d.SetorId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(d => d.AvaliadoId);
    }
}

public class FormularioParticipacaoConfiguracao : IEntityTypeConfiguration<FormularioParticipacao>
{
    public void Configure(EntityTypeBuilder<FormularioParticipacao> builder)
    {
        builder.ToTable("FORMULARIOS_PARTICIPACOES");
        builder.HasKey(p => p.Id);

        // O Guid v4 vem da aplicação (o UUID() do MySQL é v1 e o CHECK recusa): a chave nunca é gerada pelo EF nem pelo banco. CHAR(36), minúsculo.
        builder.Property(p => p.Id).HasColumnName("ID_PARTICIPACAO").ValueGeneratedNever();
        builder.Property(p => p.FormularioId).HasColumnName("ID_FORMULARIO");
        builder.Property(p => p.FuncionarioId).HasColumnName("ID_FUNCIONARIO");
        builder.Property(p => p.AvaliadoId).HasColumnName("ID_AVALIADO");

        // CHAVE_AVALIADO é GERADA pelo banco (STORED) e não é mapeada. Sem data, sem hora e sem ligação com as respostas, de propósito.
        builder.HasOne<Formulario>().WithMany().HasForeignKey(p => p.FormularioId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.FuncionarioId);
        builder.HasOne<Funcionario>().WithMany().HasForeignKey(p => p.AvaliadoId);
    }
}
