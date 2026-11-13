using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Publico;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A gravação da candidatura: o candidato e as respostas dele (com <c>ID_FORMULARIO_CANDIDATO</c>, sem funcionário), com o MESMO instante. As entidades NÃO são auditadas pelo interceptor e nada aqui grava evento (nenhum dado do candidato vai a auditoria). O e-mail repetido no formulário é recusado pela chave única
/// do banco e vira 409 <c>JA_RESPONDIDO</c> (também com dois envios ao mesmo tempo); recusa de trigger vira a resposta do contrato pelo <see cref="TradutorDeRecusaDosFormularios"/>, sem copiar o valor do e-mail.
/// </summary>
public sealed class CandidatosRepositorio(AppDbContext db) : ICandidatosRepositorio
{
    public Task<bool> EmailJaRespondeuAsync(int formularioId, string email, CancellationToken ct) => db.FormulariosCandidatos.AsNoTracking().AnyAsync(c => c.FormularioId == formularioId && c.Email == email, ct);

    public async Task GravarAsync(int formularioId, CandidatoParaGravar candidato, DateTime instanteUtc, IReadOnlyList<RespostaDoCandidato> respostas, CancellationToken ct)
    {
        try
        {
            var entidade = FormularioCandidato.Criar(formularioId, candidato.Nome, candidato.Email, candidato.Telefone, candidato.CaminhoDoCurriculo, instanteUtc);
            db.FormulariosCandidatos.Add(entidade);
            await db.SaveChangesAsync(ct); // a chave única do e-mail decide aqui, antes de qualquer resposta

            foreach (var resposta in respostas)
            {
                db.FormulariosRespostas.Add(FormularioResposta.DeCandidato(formularioId, resposta.PerguntaId, entidade.Id, resposta.Resposta, instanteUtc, resposta.AlternativaId, resposta.CaminhoArquivo, resposta.NomeDoArquivo));
            }

            await db.SaveChangesAsync(ct);
        }
        catch (Exception erro) when (TradutorDeRecusaDosFormularios.Traduzir(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }
}
