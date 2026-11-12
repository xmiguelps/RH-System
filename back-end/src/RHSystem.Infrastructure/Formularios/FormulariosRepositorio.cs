using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A gravação dos formulários. As mudanças de estado são <c>UPDATE</c> condicionais ao que o caso de uso leu (o banco decide a corrida entre duas abas); como o <c>ExecuteUpdate</c> não passa pelo interceptor do EF, cada chamada leva o
/// <c>AUDITORIA-OK</c> e o caso de uso grava o evento à mão, na mesma transação. <b>Nenhuma entidade dos formulários é <c>[Auditado]</c></b>: a resposta, a participação e o candidato não podem deixar rastro de quem respondeu.
/// </summary>
public sealed class FormulariosRepositorio(AppDbContext db) : IFormulariosRepositorio
{
    public async Task<int> AdicionarAsync(Formulario formulario, CancellationToken ct)
    {
        db.Formularios.Add(formulario);
        await db.SaveChangesAsync(ct);
        return formulario.Id;
    }

    public async Task<bool> AtualizarDetalhesAsync(FormularioLido lido, DetalhesParaGravar novos, CancellationToken ct)
    {
        var id = lido.Id;
        var ativoLido = lido.Ativo;
        var anonimoLido = lido.Anonimo;
        var encerradoLido = lido.EncerradoEmUtc;
        var exigirSemResposta = novos.ExigirSemResposta;
        var limpar = novos.LimparPrazoERecorrencia;
        TipoDeFormulario? tipo = novos.Tipo;

        // AUDITORIA-OK: a edição é auditada à mão pelo caso de uso (evento Alterou, na mesma transação), e o ExecuteUpdate não passa pelo interceptor do EF.
        var linhas = await db.Formularios
            .Where(f => f.Id == id && f.Ativo == ativoLido && f.Anonimo == anonimoLido && f.EncerradoEmUtc == encerradoLido && (!exigirSemResposta || !db.FormulariosRespostas.Any(r => r.FormularioId == f.Id)))
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.Titulo, novos.Titulo)
                    .SetProperty(f => f.Descricao, novos.Descricao)
                    .SetProperty(f => f.Tipo, tipo)
                    .SetProperty(f => f.SentidoAvaliacao, novos.Sentido)
                    .SetProperty(f => f.Anonimo, novos.Anonimo)
                    // virar Sugestão apaga o prazo e as quatro colunas da recorrência juntas (os CHECKs do banco as amarram)
                    .SetProperty(f => f.Prazo, f => limpar ? null : f.Prazo)
                    .SetProperty(f => f.RecorrenciaIntervalo, f => limpar ? null : f.RecorrenciaIntervalo)
                    .SetProperty(f => f.RecorrenciaUnidade, f => limpar ? null : f.RecorrenciaUnidade)
                    .SetProperty(f => f.ProximaReaberturaEmUtc, f => limpar ? null : f.ProximaReaberturaEmUtc)
                    .SetProperty(f => f.RecorrenciaAte, f => limpar ? null : f.RecorrenciaAte),
                ct);
        return linhas == 1;
    }
}
