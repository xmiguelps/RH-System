using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
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

    public Task<bool> AtualizarPublicacaoAsync(FormularioLido lido, PublicacaoParaGravar novos, CancellationToken ct) => GravarPublicacaoAsync(lido, novos, new EfeitoDaPublicacao(Ativar: false, GerarToken: false, ProximaReaberturaEmUtc: null), ct);

    public Task<bool> PublicarAsync(FormularioLido lido, PublicacaoParaGravar novos, EfeitoDaPublicacao efeito, CancellationToken ct) => GravarPublicacaoAsync(lido, novos, efeito, ct);

    public async Task<bool> AtivarAgendadoAsync(int formularioId, DateTime publicacaoLidaEmUtc, DateTime? proximaReaberturaEmUtc, CancellationToken ct)
    {
        var id = formularioId;
        var instante = publicacaoLidaEmUtc;
        var proxima = proximaReaberturaEmUtc;

        // AUDITORIA-OK: a publicação do agendado é auditada à mão pela tarefa (evento Publicou do Sistema, na mesma transação); a data de publicação não muda: continua o começo do dia agendado.
        var linhas = await db.Formularios
            .Where(f => f.Id == id && !f.Ativo && f.EncerradoEmUtc == null && f.PublicadoEmUtc == instante)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Ativo, true).SetProperty(f => f.ProximaReaberturaEmUtc, proxima), ct);
        return linhas == 1;
    }

    public async Task<bool> EncerrarAsync(FormularioLido lido, DateTime encerradoEmUtc, bool cancelarReaberturas, CancellationToken ct)
    {
        var id = lido.Id;
        var anonimoLido = lido.Anonimo;
        var prazoLido = lido.Prazo;
        DateTime? encerramento = encerradoEmUtc;

        // AUDITORIA-OK: o encerramento é auditado à mão pelo caso de uso ou pela tarefa (evento Alterou, na mesma transação); ATIVO falso e a data de encerramento saem no mesmo UPDATE (o CHECK do banco os amarra).
        var linhas = await db.Formularios
            .Where(f => f.Id == id && f.Ativo && f.EncerradoEmUtc == null && f.Anonimo == anonimoLido && f.Prazo == prazoLido)
            .ExecuteUpdateAsync(s => s.SetProperty(f => f.Ativo, false).SetProperty(f => f.EncerradoEmUtc, encerramento).SetProperty(f => f.ProximaReaberturaEmUtc, f => cancelarReaberturas ? null : f.ProximaReaberturaEmUtc), ct);
        return linhas == 1;
    }

    private async Task<bool> GravarPublicacaoAsync(FormularioLido lido, PublicacaoParaGravar novos, EfeitoDaPublicacao efeito, CancellationToken ct)
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
        var ativar = efeito.Ativar;
        var proxima = efeito.ProximaReaberturaEmUtc;

        var tentativasDoCodigo = efeito.GerarToken ? 3 : 1;
        for (var i = 1; i <= tentativasDoCodigo; i++)
        {
            var token = efeito.GerarToken ? TokenDoLink.Novo() : null;
            try
            {
                // AUDITORIA-OK: a publicação é auditada à mão pelo caso de uso (evento Alterou ao guardar, Publicou ao publicar, na mesma transação); o código do link some junto se o alvo deixou de ser LINK (o CHECK do banco os amarra).
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
                            .SetProperty(f => f.Ativo, f => ativar || f.Ativo)
                            .SetProperty(f => f.ProximaReaberturaEmUtc, proxima)
                            .SetProperty(f => f.TokenDoLink, f => porLink ? (token ?? f.TokenDoLink) : null),
                        ct);
                if (linhas != 1)
                {
                    return false;
                }

                break;
            }
            catch (Exception erro) when (i < tentativasDoCodigo && (erro is DbUpdateException or MySqlException) && ErrosDoBanco.EhChaveRepetida(erro) && ErrosDoBanco.NomeDaChaveRepetida(erro) == RegrasDoBancoDosFormularios.ChaveDoTokenDoLink)
            {
                // o código do link colidiu (improvável com 32 bytes aleatórios): gera outro
            }
        }

        // AUDITORIA-OK: as linhas de destinatário não são auditadas (quem recebe o formulário não é dado de auditoria, e no anônimo nenhum rastro pode sobrar); o evento é do caso de uso.
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
