using RHSystem.Domain.Formularios;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// As quatro situações do formulário como filtro de banco, as MESMAS de <c>RegraDaSituacaoDoFormulario</c> (que calcula a da linha): o prazo vencido com <c>ATIVO</c> ligado vale ENCERRADO (a tarefa de encerrar pode ainda não ter rodado), pelo dia de hoje da aplicação, nunca pelo relógio do banco.
/// </summary>
internal static class FormulariosPorSituacao
{
    public static IQueryable<Formulario> NaSituacaoCalculada(this IQueryable<Formulario> formularios, SituacaoDoFormulario? situacao, DateOnly hoje) => situacao switch
    {
        SituacaoDoFormulario.Rascunho => formularios.Where(f => !f.Ativo && f.EncerradoEmUtc == null && f.PublicadoEmUtc == null),
        SituacaoDoFormulario.Agendado => formularios.Where(f => !f.Ativo && f.EncerradoEmUtc == null && f.PublicadoEmUtc != null),
        SituacaoDoFormulario.Publicado => formularios.Where(f => f.Ativo && f.EncerradoEmUtc == null && (f.Prazo == null || f.Prazo >= hoje)),
        SituacaoDoFormulario.Encerrado => formularios.Where(f => f.EncerradoEmUtc != null || (f.Ativo && f.Prazo != null && f.Prazo < hoje)),
        _ => formularios,
    };
}
