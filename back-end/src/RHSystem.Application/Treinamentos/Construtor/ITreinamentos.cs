using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>O treinamento como o banco o guarda, com as listas do público e a quantidade de módulos (o que a edição precisa saber para decidir).</summary>
public sealed record TreinamentoLido(
    int Id,
    string Titulo,
    string? Descricao,
    bool Onboarding,
    DateOnly? ComecaEm,
    bool Obrigatorio,
    bool Publicado,
    PublicoDoTreinamento? Publico,
    bool EmiteCertificado,
    bool IncluiAdmitidos,
    DateTime? EncerradoEmUtc,
    IReadOnlyList<int> SetorIds,
    IReadOnlyList<int> FuncionarioIds,
    int TotalDeModulos)
{
    public SituacaoDoTreinamento Situacao(DateOnly hoje) => RegraDaSituacaoDoTreinamento.De(Publicado, ComecaEm, EncerradoEmUtc, hoje);

    public CamposDoTreinamento Campos => new(Titulo, Descricao, ComecaEm, Obrigatorio, Onboarding, EmiteCertificado, Publico, SetorIds, FuncionarioIds, IncluiAdmitidos);
}

/// <summary>As pessoas ATIVAS que ainda não concluíram o treinamento: as que começaram (<c>EM_ANDAMENTO</c>) e as que ainda não iniciaram (<c>INSCRITO</c>).</summary>
public sealed record PendentesDoTreinamento(int EmAndamento, int NaoIniciaram)
{
    public int Total => EmAndamento + NaoIniciaram;
}

/// <summary>A leitura dos treinamentos para o RH.</summary>
public interface IConsultaDeTreinamentos
{
    Task<TreinamentoLido?> ObterAsync(int treinamentoId, CancellationToken ct);

    /// <summary>O treinamento completo (<c>TreinamentoRh</c>): setores com nome, pessoas com cargo e setor, módulos em ordem. <c>null</c> se não existe.</summary>
    Task<TreinamentoRh?> ObterDetalheAsync(int treinamentoId, DateOnly hoje, CancellationToken ct);

    /// <summary>Quantas pessoas o público alcança: só <b>ativas com a admissão já ocorrida</b> (<paramref name="hoje"/> inclusive); <c>TODOS</c> ignora as listas, <c>SETORES</c> usa os setores e <c>PESSOAS</c> as pessoas (número que não existe não conta).</summary>
    Task<int> ContarPublicoAsync(PublicoDoTreinamento publico, IReadOnlyList<int> setorIds, IReadOnlyList<int> funcionarioIds, DateOnly hoje, CancellationToken ct);

    /// <summary>As pessoas ATIVAS com linha <c>EM_ANDAMENTO</c> ou <c>INSCRITO</c> no treinamento (inativa nunca conta como pendência).</summary>
    Task<PendentesDoTreinamento> ContarPendentesAsync(int treinamentoId, CancellationToken ct);

    /// <summary>Os certificados emitidos: se o treinamento EMITE certificado, quantas pessoas o concluíram (o trigger cria a linha de certificado a cada conclusão, mesmo sem emitir, e essa não conta); 0 se não emite ou não existe.</summary>
    Task<int> ContarCertificadosEmitidosAsync(int treinamentoId, CancellationToken ct);

    /// <summary>Dos números dados, os que NÃO são setor.</summary>
    Task<IReadOnlyList<int>> SetoresInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>Dos números dados, os que NÃO são pessoa.</summary>
    Task<IReadOnlyList<int>> FuncionariosInexistentesAsync(IReadOnlyList<int> ids, CancellationToken ct);
}

/// <summary>A escrita dos treinamentos. Tudo roda na transação do chamador.</summary>
public interface ITreinamentosRepositorio
{
    /// <summary>Insere o rascunho (e as linhas do público) e devolve o número.</summary>
    Task<int> CriarAsync(CamposDoTreinamento campos, CancellationToken ct);

    /// <summary>
    /// Grava os campos por <c>UPDATE</c> condicional ao estado lido (publicação e encerramento iguais; a data de início igual quando é ela que muda): <c>false</c> se outra aba mudou o treinamento no meio. Rascunho: todos os campos e o público trocado (apaga as linhas antigas antes de inserir as novas).
    /// Publicado: só título, descrição, a caixa de admitidos e a data de início.
    /// </summary>
    Task<bool> AtualizarAsync(TreinamentoLido lido, CamposDoTreinamento novos, CancellationToken ct);

    /// <summary>Marca o treinamento como publicado por <c>UPDATE</c> condicional a "ainda não publicado nem encerrado": <c>false</c> se outra chamada publicou (ou encerrou) antes.</summary>
    Task<bool> PublicarAsync(int treinamentoId, CancellationToken ct);

    /// <summary>Cria a linha <c>INSCRITO</c> de cada pessoa que ainda não tem linha no treinamento (as datas ficam vazias) e devolve quantas criou. O banco recusa inativo: quem chama passa só pessoas ativas.</summary>
    Task<int> InscreverAsync(int treinamentoId, IReadOnlyList<int> funcionarioIds, CancellationToken ct);

    /// <summary>Grava <c>ENCERRADO_EM</c> (<paramref name="agoraUtc"/>) por <c>UPDATE</c> condicional ao estado lido (publicado, ainda não encerrado, mesma data de início): <c>false</c> se outra aba mudou o treinamento no meio.</summary>
    Task<bool> EncerrarAsync(TreinamentoLido lido, DateTime agoraUtc, CancellationToken ct);

    /// <summary>Apaga o treinamento e tudo que depende dele, nesta ordem: progresso dos módulos, módulos, certificados, participantes, público e o treinamento (o banco recusa apagar o pai com filho). O chamador já travou a linha do treinamento.</summary>
    Task ExcluirAsync(int treinamentoId, CancellationToken ct);
}

/// <summary>A porta do público: QUEM é do público do treinamento agora. Só pessoas ATIVAS com a admissão já ocorrida (<c>TODOS</c> = todas elas, <c>SETORES</c> = as dos setores escolhidos, <c>PESSOAS</c> = as escolhidas); treinamento sem público não alcança ninguém.</summary>
public interface IPublicoDoTreinamento
{
    Task<IReadOnlyList<int>> PessoasAsync(int treinamentoId, DateOnly hoje, CancellationToken ct);
}
