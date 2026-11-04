using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Auditoria;

/// <summary>Um campo que mudou num evento. Campo sensível leva <c>ValorOculto = true</c> e antes e depois nulos.</summary>
public sealed record MudancaDeAuditoriaNova(string Campo, string? Antes, string? Depois, bool ValorOculto = false);

/// <summary>Um evento a registrar. <c>Entidade</c> e <c>IdRegistro</c> vêm juntos ou ficam os dois vazios.</summary>
public sealed record EventoDeAuditoriaNovo(
    AuditoriaArea Area,
    AuditoriaAcao Acao,
    string Descricao,
    string RegistroAfetado,
    string? Entidade = null,
    long? IdRegistro = null,
    IReadOnlyList<MudancaDeAuditoriaNova>? Mudancas = null);

/// <summary>Quem fez, quando a requisição ainda é anônima (o login, o "esqueci minha senha"): o ator vem do que o caso de uso sabe.</summary>
public sealed record AtorDoEvento(int FuncionarioId, string Nome, Perfil? Perfil);

/// <summary>
/// O log de auditoria. <b>Qual conexão grava</b> (regra única): se o fato de negócio persiste, o evento sai NA MESMA conexão e transação dele
/// (<see cref="Registrar"/>, <see cref="RegistrarComo"/>); se o evento tem de sobreviver à falha (senha errada, acesso negado, download, exportação), sai DEPOIS,
/// fora de qualquer transação, em contexto próprio (<c>RegistrarEGravar*</c>). Nunca se abre segunda conexão enquanto a transação do chamador segura trava
/// na linha da pessoa: o evento tem chave estrangeira para <c>FUNCIONARIOS</c> e a segunda conexão esperaria a trava do InnoDB.
/// </summary>
public interface IAuditor
{
    /// <summary>Junta o evento ao contexto da requisição: sai no próximo <c>SalvarAsync</c>, na mesma transação do fato (efeito de trigger, lote, ação de negócio).</summary>
    /// <exception cref="InvalidOperationException">O ator atual é anônimo (use <see cref="RegistrarComo"/>).</exception>
    void Registrar(EventoDeAuditoriaNovo evento);

    /// <summary>Como <see cref="Registrar"/>, com o ator informado, para a requisição sem login (esqueci, definir senha).</summary>
    void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento);

    /// <summary>Grava já, em contexto próprio (login, download, exportação, e-mail): não leva mudanças pendentes do chamador.</summary>
    /// <exception cref="InvalidOperationException">Há transação aberta ("Dentro de transação use Registrar ou RegistrarComo."), ou o ator atual é anônimo.</exception>
    Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct);

    /// <summary>Como <see cref="RegistrarEGravarAsync"/>, com o ator informado (o login: a requisição ainda é anônima). Também só fora de transação.</summary>
    Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct);

    /// <summary>
    /// Como o anterior, mas NÃO grava se o mesmo ator já tem evento da mesma área, ação e descrição dentro da <paramref name="janela"/> (contada pelo
    /// <c>IRelogio</c>); devolve se gravou. Conferir e gravar não são atômicos: dois pedidos no mesmo instante podem gravar dois eventos (é limite de
    /// ruído, não garantia).
    /// </summary>
    Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct);

    /// <summary>O interceptor do EF não grava nada até o <c>Dispose</c> (a importação registra um evento só).</summary>
    IDisposable SuspenderRegistroAutomatico();
}
