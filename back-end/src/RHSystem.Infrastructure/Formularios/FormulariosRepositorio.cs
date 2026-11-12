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

    public async Task<bool> AtualizarPublicacaoAsync(FormularioLido lido, PublicacaoParaGravar novos, CancellationToken ct)
    {
        var id = lido.Id;
        var ativoLido = lido.Ativo;
        var anonimoLido = lido.Anonimo;
        var encerradoLido = lido.EncerradoEmUtc;
        var publicadoLido = lido.PublicadoEmUtc;
        AlvoDoFormulario? alvo = novos.Alvo;
        var porLink = novos.Alvo == AlvoDoFormulario.Link;
        var prazo = novos.Prazo;
        var intervalo = novos.Recorrencia?.Intervalo;
        UnidadeDeRecorrencia? unidade = novos.Recorrencia?.Unidade;
        var ate = novos.Recorrencia?.Ate;
        var lembrete = novos.LembreteAutomatico;
        var novoDia = novos.NovaPublicacaoEmUtc;

        // AUDITORIA-OK: a publicação guardada é auditada à mão pelo caso de uso (evento Alterou, na mesma transação); o código do link some junto se o alvo deixou de ser LINK (o CHECK do banco os amarra).
        var linhas = await db.Formularios
            .Where(f => f.Id == id && f.Ativo == ativoLido && f.Anonimo == anonimoLido && f.EncerradoEmUtc == encerradoLido && f.PublicadoEmUtc == publicadoLido)
            .ExecuteUpdateAsync(
                s => s.SetProperty(f => f.Alvo, alvo)
                    .SetProperty(f => f.Prazo, prazo)
                    .SetProperty(f => f.RecorrenciaIntervalo, intervalo)
                    .SetProperty(f => f.RecorrenciaUnidade, unidade)
                    .SetProperty(f => f.RecorrenciaAte, ate)
                    .SetProperty(f => f.LembreteAutomatico, lembrete)
                    .SetProperty(f => f.PublicadoEmUtc, f => novoDia ?? f.PublicadoEmUtc)
                    .SetProperty(f => f.TokenDoLink, f => porLink ? f.TokenDoLink : null),
                ct);
        if (linhas != 1)
        {
            return false;
        }

        // AUDITORIA-OK: as linhas de destinatário não são auditadas (quem recebe o formulário não é dado de auditoria, e no anônimo nenhum rastro pode sobrar); o evento Alterou é do caso de uso.
        await db.FormulariosDestinatarios.Where(d => d.FormularioId == id).ExecuteDeleteAsync(ct);
        db.FormulariosDestinatarios.AddRange(novos.Alvo switch
        {
            AlvoDoFormulario.Pessoas => novos.FuncionarioIds.Select(p => FormularioDestinatario.DaPessoa(id, p)),
            AlvoDoFormulario.Setores => novos.SetorIds.Select(s => FormularioDestinatario.DoSetor(id, s)),
            _ => [],
        });
        await db.SaveChangesAsync(ct);
        return true;
    }
}
