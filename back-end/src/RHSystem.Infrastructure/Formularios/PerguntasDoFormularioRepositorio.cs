using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Domain.Formularios;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A gravação das perguntas e das alternativas. A ordem é única por formulário (<c>UQ_FORMULARIOS_PERGUNTAS_FORMULARIO_ORDEM</c>) e o banco a confere linha a linha: toda mudança de posição passa por dois passos (<c>ORDEM + 1000</c> e depois o valor final), e a pergunta que
/// muda de lugar vai antes para um valor só dela. Como o <c>ExecuteUpdate</c> não passa pelo interceptor do EF, cada chamada leva o <c>AUDITORIA-OK</c> e o caso de uso grava o evento à mão, na mesma transação. O texto da pergunta nunca vai a evento: o formulário
/// anônimo não pode vazá-lo.
/// </summary>
public sealed class PerguntasDoFormularioRepositorio(AppDbContext db) : IPerguntasDoFormularioRepositorio
{
    private const int Salto = 1000;
    private const int PosicaoTemporariaBase = 1_000_000;

    public async Task<bool> SegurarSeIgualAoLidoAsync(FormularioLido lido, CancellationToken ct)
    {
        var id = lido.Id;
        var ativo = lido.Ativo;
        var anonimo = lido.Anonimo;
        var encerrado = lido.EncerradoEmUtc;

        // AUDITORIA-OK: não muda valor nenhum (TITULO = TITULO): só segura a linha do formulário até o fim da transação, para publicar e encerrar esperarem; nada a auditar.
        var linhas = await db.Formularios.Where(f => f.Id == id && f.Ativo == ativo && f.Anonimo == anonimo && f.EncerradoEmUtc == encerrado).ExecuteUpdateAsync(s => s.SetProperty(f => f.Titulo, f => f.Titulo), ct);
        return linhas == 1;
    }

    public async Task<int> AdicionarAsync(int formularioId, PerguntaParaGravar pergunta, int posicao, CancellationToken ct)
    {
        try
        {
            await AbrirEspacoAsync(formularioId, posicao, ct);
            var entidade = FormularioPergunta.Criar(formularioId, pergunta.Texto, pergunta.Tipo, posicao, pergunta.Obrigatoria);
            db.FormulariosPerguntas.Add(entidade);
            await db.SaveChangesAsync(ct);
            for (var i = 0; i < pergunta.Alternativas.Count; i++)
            {
                db.FormulariosAlternativas.Add(FormularioAlternativa.Criar(entidade.Id, pergunta.Alternativas[i].Texto, i + 1));
            }

            await db.SaveChangesAsync(ct);
            return entidade.Id;
        }
        catch (Exception erro) when (Traduzida(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }

    public async Task EditarAsync(int formularioId, PerguntaLida atual, PerguntaParaGravar nova, int? novaPosicao, CancellationToken ct)
    {
        try
        {
            var id = atual.Id;
            if (novaPosicao is { } destino && destino != atual.Ordem)
            {
                await MoverAsync(formularioId, id, atual.Ordem, destino, ct);
            }

            var texto = nova.Texto;
            TipoDePergunta? tipo = nova.Tipo;
            var obrigatoria = nova.Obrigatoria;

            // AUDITORIA-OK: a edição da pergunta é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação); o trigger recusa texto ou tipo novos em pergunta respondida.
            await db.FormulariosPerguntas.Where(p => p.Id == id).ExecuteUpdateAsync(s => s.SetProperty(p => p.Texto, texto).SetProperty(p => p.Tipo, tipo).SetProperty(p => p.Obrigatoria, obrigatoria), ct);
            await AcertarAlternativasAsync(atual, nova, ct);
        }
        catch (Exception erro) when (Traduzida(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }

    public async Task ExcluirAsync(int formularioId, PerguntaLida atual, CancellationToken ct)
    {
        try
        {
            var id = atual.Id;
            var posicao = atual.Ordem;

            // AUDITORIA-OK: a exclusão é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação).
            await db.FormulariosAlternativas.Where(a => a.PerguntaId == id).ExecuteDeleteAsync(ct);

            // AUDITORIA-OK: a exclusão é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação); o trigger recusa apagar pergunta respondida.
            await db.FormulariosPerguntas.Where(p => p.Id == id).ExecuteDeleteAsync(ct);
            await DeslocarAsync(formularioId, de: posicao + 1, ate: int.MaxValue - Salto, delta: -1, ct);
        }
        catch (Exception erro) when (Traduzida(erro) is { } problema)
        {
            db.ChangeTracker.Clear();
            throw problema;
        }
    }

    /// <summary>As perguntas da posição <paramref name="posicao"/> em diante descem uma casa, para a nova entrar ali.</summary>
    private Task AbrirEspacoAsync(int formularioId, int posicao, CancellationToken ct) => DeslocarAsync(formularioId, posicao, int.MaxValue - Salto, 1, ct);

    /// <summary>Move a pergunta para <paramref name="destino"/>: ela sai para uma posição só dela, as do meio andam uma casa e ela entra no lugar.</summary>
    private async Task MoverAsync(int formularioId, int perguntaId, int origem, int destino, CancellationToken ct)
    {
        var temporaria = PosicaoTemporariaBase + perguntaId;

        // AUDITORIA-OK: a mudança de posição é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação).
        await db.FormulariosPerguntas.Where(p => p.Id == perguntaId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Ordem, (int?)temporaria), ct);
        if (destino > origem)
        {
            await DeslocarAsync(formularioId, origem + 1, destino, -1, ct); // quem estava entre a origem e o destino sobe uma casa
        }
        else
        {
            await DeslocarAsync(formularioId, destino, origem - 1, 1, ct); // quem estava entre o destino e a origem desce uma casa
        }

        // AUDITORIA-OK: a mudança de posição é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação).
        await db.FormulariosPerguntas.Where(p => p.Id == perguntaId).ExecuteUpdateAsync(s => s.SetProperty(p => p.Ordem, (int?)destino), ct);
    }

    /// <summary>Soma <paramref name="delta"/> (+1 ou -1) às posições de <paramref name="de"/> até <paramref name="ate"/>, em dois passos para a chave única nunca ver duas iguais.</summary>
    private async Task DeslocarAsync(int formularioId, int de, int ate, int delta, CancellationToken ct)
    {
        if (de > ate)
        {
            return;
        }

        // AUDITORIA-OK: o reordenamento é auditado à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação); primeiro passo: sai da frente para a faixa de 1000 em diante.
        await db.FormulariosPerguntas.Where(p => p.FormularioId == formularioId && p.Ordem >= de && p.Ordem <= ate).ExecuteUpdateAsync(s => s.SetProperty(p => p.Ordem, p => p.Ordem + Salto), ct);

        // AUDITORIA-OK: o reordenamento é auditado à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação); segundo passo: volta já deslocada.
        await db.FormulariosPerguntas.Where(p => p.FormularioId == formularioId && p.Ordem >= de + Salto && p.Ordem <= ate + Salto).ExecuteUpdateAsync(s => s.SetProperty(p => p.Ordem, p => p.Ordem - Salto + delta), ct);
    }

    /// <summary>
    /// Deixa as alternativas como a lista pede: a que sumiu é apagada, as que ficam (com id) mudam de texto e de posição, as sem id entram. Posição pelo lugar na lista (1, 2, 3...). Em dois passos: as que ficam vão para a faixa de 1000 em diante e voltam
    /// já no lugar final, então a chave única da posição nunca vê duas iguais.
    /// </summary>
    private async Task AcertarAlternativasAsync(PerguntaLida atual, PerguntaParaGravar nova, CancellationToken ct)
    {
        var perguntaId = atual.Id;
        var iguais = atual.Alternativas.Count == nova.Alternativas.Count && atual.Alternativas.Zip(nova.Alternativas).All(par => par.First.Id == par.Second.Id && par.First.Texto == par.Second.Texto);
        if (iguais)
        {
            return;
        }

        var ficam = nova.Alternativas.Where(a => a.Id is not null).Select(a => a.Id!.Value).ToList();
        var somem = atual.Alternativas.Select(a => a.Id).Where(id => !ficam.Contains(id)).ToList();
        if (somem.Count > 0)
        {
            // AUDITORIA-OK: a edição da pergunta é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação).
            await db.FormulariosAlternativas.Where(a => a.PerguntaId == perguntaId && somem.Contains(a.Id)).ExecuteDeleteAsync(ct);
        }

        // AUDITORIA-OK: a edição da pergunta é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação); as que ficam saem para a faixa de 1000 em diante.
        await db.FormulariosAlternativas.Where(a => a.PerguntaId == perguntaId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Ordem, a => a.Ordem + Salto), ct);
        for (var i = 0; i < nova.Alternativas.Count; i++)
        {
            var alternativa = nova.Alternativas[i];
            var ordem = i + 1;
            var texto = alternativa.Texto;
            if (alternativa.Id is { } id)
            {
                // AUDITORIA-OK: a edição da pergunta é auditada à mão pelo caso de uso (evento Alterou só com o título do formulário, na mesma transação).
                await db.FormulariosAlternativas.Where(a => a.Id == id && a.PerguntaId == perguntaId).ExecuteUpdateAsync(s => s.SetProperty(a => a.Texto, texto).SetProperty(a => a.Ordem, ordem), ct);
            }
            else
            {
                db.FormulariosAlternativas.Add(FormularioAlternativa.Criar(perguntaId, texto, ordem));
                await db.SaveChangesAsync(ct);
            }
        }
    }

    private static Application.Comum.ProblemaDeApiException? Traduzida(Exception erro) =>
        erro is DbUpdateException or MySqlException ? TradutorDeRecusaDosFormularios.Traduzir(erro) : null;
}
