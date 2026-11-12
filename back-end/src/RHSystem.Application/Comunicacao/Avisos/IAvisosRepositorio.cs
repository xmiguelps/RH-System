using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Avisos;

/// <summary>
/// A gravação dos avisos. Toda mudança de estado é um <c>UPDATE</c> condicional ao estado lido (o banco decide a corrida): devolve <c>false</c> quando outra decisão chegou antes e o chamador relê para saber qual erro dar. Quem chama
/// registra o evento de auditoria na mesma transação.
/// </summary>
public interface IAvisosRepositorio
{
    /// <summary>Grava o aviso e devolve o id. O rascunho sem data prevista volta a ter <c>DATA_PUBLICACAO</c> nula (o trigger de INSERT põe o <c>NOW()</c> do servidor).</summary>
    Task<int> AdicionarAsync(Aviso aviso, CancellationToken ct);

    /// <summary>Muda o aviso se ele ainda está como foi lido (<paramref name="enviadoLido"/>): <c>false</c> se foi publicado, excluído ou mudou de estado nesse meio tempo.</summary>
    Task<bool> AtualizarAsync(int avisoId, bool enviadoLido, string titulo, string conteudo, bool fixado, DateTime? publicacaoEmUtc, DateTime? expiracaoEmUtc, CancellationToken ct);

    /// <summary>Rascunho vira publicado com a data efetiva (<c>WHERE ENVIADO = FALSE</c>): o segundo clique devolve <c>false</c>.</summary>
    Task<bool> PublicarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct);

    /// <summary>A expiração vira <paramref name="agoraUtc"/> (já cortado ao segundo) só se o aviso está ativo.</summary>
    Task<bool> EncerrarAsync(int avisoId, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Apaga as leituras e o aviso, só se ele ainda não está publicado (rascunho, ou agendado para depois de <paramref name="agoraUtc"/>).</summary>
    Task<bool> ExcluirAsync(int avisoId, DateTime agoraUtc, CancellationToken ct);

    /// <summary>
    /// A pessoa abriu o aviso: a linha de leitura dela vira lida com a data de <paramref name="agoraUtc"/> (<c>WHERE VISUALIZADO = FALSE</c>: a segunda leitura não muda nada e a data da primeira fica). Sem linha (admitida depois), a linha é criada
    /// já lida; a chave única repetida de uma corrida vale como sucesso.
    /// </summary>
    Task MarcarComoLidoAsync(int avisoId, int funcionarioId, DateTime agoraUtc, CancellationToken ct);
}

/// <summary>A leitura dos avisos para o RH. A lista e o resumo recebem o <c>agora</c> da aplicação (as situações são calculadas dele, nunca do relógio do banco).</summary>
public interface IConsultaDeAvisos
{
    Task<(IReadOnlyList<AvisoLido> Itens, int Total)> ListarAsync(FiltroDeAvisosRh filtro, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct);

    Task<AvisoLido?> ObterAsync(int avisoId, CancellationToken ct);

    Task<ResumoDosAvisos> ResumoAsync(DateTime agoraUtc, CancellationToken ct);

    /// <summary>
    /// A lista "Quem viu": as linhas de leitura de pessoas ATIVAS hoje, por nome. <c>Viram</c> e <c>AindaNaoViram</c> são os totais do aviso e não mudam com o <paramref name="filtro"/>; <c>Total</c> é o da lista pedida (para a paginação).
    /// </summary>
    Task<(IReadOnlyList<AvisoLeituraItem> Itens, int Total, int Viram, int AindaNaoViram)> ListarLeiturasAsync(int avisoId, FiltroDeLeitura? filtro, ParametrosDeLista parametros, CancellationToken ct);

    /// <summary>Quem ainda não abriu o aviso e pode receber e-mail: ativo, admitido até <paramref name="hoje"/> e com e-mail corporativo.</summary>
    Task<IReadOnlyList<DestinatarioDeAviso>> NaoLidosParaLembreteAsync(int avisoId, DateOnly hoje, CancellationToken ct);
}

/// <summary>A tarefa que manda o e-mail na hora da publicação (<c>PUBLICAR_AVISO:aviso={id}:em={instante}</c>). A chave leva o instante porque uma chave de tarefa cancelada nunca volta: mudar a data agenda outra.</summary>
public interface IAgendamentoDoAviso
{
    Task AgendarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct);

    Task CancelarAsync(int avisoId, DateTime publicacaoEmUtc, CancellationToken ct);
}

/// <summary>A leitura dos avisos <b>vigentes</b> (ativos: publicados, já no ar e sem expirar) para quem está logado. O escopo é a própria pessoa: o lido é o dela.</summary>
public interface IConsultaDeAvisosDaPessoa
{
    Task<(IReadOnlyList<AvisoDaPessoaLido> Itens, int Total, AvisoContagensEu Contagens)> ListarAsync(int funcionarioId, FiltroDeMeusAvisos filtro, ParametrosDeLista parametros, DateTime agoraUtc, CancellationToken ct);

    /// <summary><c>null</c> se o aviso não existe ou não está vigente (rascunho, agendado ou expirado).</summary>
    Task<AvisoDaPessoaLido?> ObterAsync(int funcionarioId, int avisoId, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Os avisos vigentes que a pessoa ainda não abriu: a bolinha do menu.</summary>
    Task<int> ContarNaoLidosAsync(int funcionarioId, DateTime agoraUtc, CancellationToken ct);
}
