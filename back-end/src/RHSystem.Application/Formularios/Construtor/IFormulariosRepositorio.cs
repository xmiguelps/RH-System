using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>Uma alternativa como o banco a guarda.</summary>
public sealed record AlternativaLida(int Id, int Ordem, string Texto);

/// <summary>Uma pergunta como o banco a guarda, com as alternativas em ordem.</summary>
public sealed record PerguntaLida(int Id, int Ordem, string Texto, TipoDePergunta Tipo, bool Obrigatoria, IReadOnlyList<AlternativaLida> Alternativas);

/// <summary>
/// O formulário como o banco o guarda, para o RH: as colunas, o autor, as perguntas e a publicação já escolhida (setores e pessoas das linhas de destinatário). <c>TemRespostas</c> conta resposta de pessoa, de candidato e anônima (o RH nunca recebe o
/// número: o mapeador o esconde no anônimo). A situação NÃO vem pronta: quem lê a calcula com <c>RegraDaSituacaoDoFormulario</c> e o <c>agora</c> da aplicação (o banco não fecha no prazo).
/// </summary>
public sealed record FormularioLido(
    int Id,
    string Titulo,
    string? Descricao,
    TipoDeFormulario Tipo,
    SentidoDaAvaliacao? Sentido,
    bool Anonimo,
    bool Ativo,
    DateTime CriadoEmUtc,
    FuncionarioResumo? Autor,
    AlvoDoFormulario? Alvo,
    DateTime? PublicadoEmUtc,
    DateTime? EncerradoEmUtc,
    DateOnly? Prazo,
    int? RecorrenciaIntervalo,
    UnidadeDeRecorrencia? RecorrenciaUnidade,
    DateOnly? RecorrenciaAte,
    DateTime? ProximaReaberturaEmUtc,
    bool LembreteAutomatico,
    string? TokenDoLink,
    bool TemRespostas,
    IReadOnlyList<PerguntaLida> Perguntas,
    IReadOnlyList<FormularioSetor> Setores,
    IReadOnlyList<FuncionarioResumo> Pessoas);

/// <summary>A leitura dos formulários para o RH.</summary>
public interface IConsultaDeFormularios
{
    /// <summary><c>null</c> se o formulário não existe.</summary>
    Task<FormularioLido?> ObterAsync(int formularioId, CancellationToken ct);

    /// <summary>O formulário que tem este código de link público, ou <c>null</c> (nenhum é o mesmo que um formulário que perdeu o link: o chamador responde o mesmo 404).</summary>
    Task<int?> IdPorTokenAsync(string token, CancellationToken ct);

    /// <summary>A pergunta já tem resposta de pessoa, de candidato ou anônima (o trigger do banco conta as três).</summary>
    Task<bool> PerguntaTemRespostaAsync(int perguntaId, CancellationToken ct);

    /// <summary>As pessoas entre <paramref name="ids"/> que existem, com o estado de cada uma (as que não existem não voltam).</summary>
    Task<IReadOnlyList<PessoaParaPublico>> PessoasAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>Quantos dos setores de <paramref name="ids"/> existem.</summary>
    Task<int> ContarSetoresExistentesAsync(IReadOnlyList<int> ids, CancellationToken ct);

    /// <summary>Os pares (pessoa e avaliado) que já responderam o formulário com nome (as respostas de pessoa; sem as anônimas nem as de candidato): é o que "quem falta" tira dos pares que devem responder.</summary>
    Task<IReadOnlyList<ParDeResposta>> ParesQueResponderamAsync(int formularioId, CancellationToken ct);

    /// <summary>A página da lista do RH, com o total e a contagem por tipo (que respeita a busca e a situação e ignora o tipo). A situação do filtro é a CALCULADA (o prazo vencido vale encerrado), pelo <paramref name="hoje"/> da aplicação.</summary>
    Task<(IReadOnlyList<FormularioDaLista> Itens, int Total, FormularioContagemPorTipo Contagem)> ListarAsync(FiltroDeFormulariosRh filtro, ParametrosDeLista parametros, DateOnly hoje, CancellationToken ct);

    /// <summary>O que cada formulário já recebeu, de uma vez só para os ids da página (a lista do RH e a exclusão leem daqui). Formulário sem nada recebido volta com tudo zerado.</summary>
    Task<IReadOnlyDictionary<int, RespostasDoFormulario>> RespostasPorFormularioAsync(IReadOnlyList<int> ids, CancellationToken ct);
}

/// <summary>Os filtros da lista do RH: o tipo (as abas) e a situação CALCULADA.</summary>
public sealed record FiltroDeFormulariosRh(TipoDeFormulario? Tipo, SituacaoDoFormulario? Situacao);

/// <summary>Uma linha da lista como o banco a guarda: a situação NÃO vem pronta (quem lê a calcula com o relógio da aplicação).</summary>
public sealed record FormularioDaLista(int Id, string Titulo, TipoDeFormulario Tipo, SentidoDaAvaliacao? Sentido, bool Anonimo, bool Ativo, AlvoDoFormulario? Alvo, DateTime? PublicadoEmUtc, DateTime? EncerradoEmUtc, DateOnly? Prazo, DateTime CriadoEmUtc);

/// <summary>
/// O que um formulário já recebeu. <c>Pares</c>: as pessoas (e avaliados) que responderam, só nos formulários com nome. <c>Candidatos</c>: as candidaturas por link. <c>Envios</c>: os envios de pessoas (a Sugestão aceita vários da mesma pessoa; cada envio é o grupo de linhas
/// com a mesma data). <c>Participacoes</c>: as participações do anônimo (uma linha por pessoa e avaliado, sem a resposta).
/// </summary>
public sealed record RespostasDoFormulario(IReadOnlyList<ParDeResposta> Pares, int Candidatos, int Envios, int Participacoes);

/// <summary>Uma pessoa escolhida na publicação: existe, e está ativa ou não.</summary>
public sealed record PessoaParaPublico(int Id, bool Ativo);

/// <summary>
/// O que a publicação guardada grava: o alvo, as listas, o prazo, a recorrência e o lembrete. <c>NovaPublicacaoEmUtc</c> só vale para um formulário AGENDADO que ganhou outro dia (o rascunho não guarda o dia). O código do link e o <c>PROXIMA_REABERTURA</c> nunca
/// são gravados aqui: o código nasce ao publicar e a próxima reabertura, também.
/// </summary>
public sealed record PublicacaoParaGravar(AlvoDoFormulario Alvo, IReadOnlyList<int> SetorIds, IReadOnlyList<int> FuncionarioIds, DateOnly? Prazo, RecorrenciaValidada? Recorrencia, bool LembreteAutomatico, DateTime? NovaPublicacaoEmUtc);

/// <summary>A tarefa que publica o formulário agendado no começo do dia escolhido (<c>PUBLICAR_FORMULARIO:formulario={id}:em={instante}</c>). A chave leva o instante porque uma chave cancelada nunca volta: mudar o dia de um agendado cancela a antiga e agenda outra.</summary>
public interface IAgendamentoDoFormulario
{
    Task AgendarPublicacaoAsync(int formularioId, DateTime publicacaoEmUtc, CancellationToken ct);

    Task CancelarPublicacaoAsync(int formularioId, DateTime publicacaoEmUtc, CancellationToken ct);

    /// <summary>
    /// Uma NOVA TENTATIVA de publicar o agendado que já passou do dia e continua agendado (a tarefa o deixou para trás: anônimo com menos de 5 pessoas, formulário sem pergunta): a chave leva a hora (<c>PUBLICAR_FORMULARIO:formulario={id}:r={hora}:em={instante}</c>), então a recuperação horária cria uma por hora
    /// em vez de esbarrar na tarefa original, que já está concluída. A tarefa relê tudo: se o bloqueio passou, publica; senão não faz nada e a próxima hora tenta de novo.
    /// </summary>
    Task AgendarNovaTentativaDePublicacaoAsync(int formularioId, DateTime publicacaoEmUtc, DateTime agoraUtc, CancellationToken ct);

    /// <summary>A tarefa que encerra o formulário pelo prazo (<c>ENCERRAR_FORMULARIO:formulario={id}:em={instante}</c>), no instante NOMINAL do encerramento: o começo do dia seguinte ao prazo (-03:00).</summary>
    Task AgendarEncerramentoAsync(int formularioId, DateTime encerramentoNominalEmUtc, CancellationToken ct);

    Task CancelarEncerramentoAsync(int formularioId, DateTime encerramentoNominalEmUtc, CancellationToken ct);

    /// <summary>A tarefa que reabre o formulário recorrente (<c>REABRIR_FORMULARIO:formulario={id}:em={instante}</c>), no instante da próxima reabertura. Chave que já existe é sucesso (cancelada volta).</summary>
    Task AgendarReaberturaAsync(int formularioId, DateTime reaberturaEmUtc, CancellationToken ct);

    /// <summary>
    /// A tarefa do lembrete AUTOMÁTICO (<c>LEMBRETE_FORMULARIO:form={id}:dia={dia}:alcance=TODOS</c>), no começo do <paramref name="dia"/> (00:00 -03:00), 7 dias antes do prazo. A chave é a marca do dia: o lembrete manual do anônimo usa a mesma, então só um dos dois sai por dia. Chave que já existe é sucesso.
    /// </summary>
    Task AgendarLembreteAutomaticoAsync(int formularioId, DateOnly dia, CancellationToken ct);

    /// <summary>Cancela toda tarefa pendente do formulário (publicar, encerrar, reabrir, lembrete) e os e-mails de lembrete ainda na fila: a exclusão não deixa nada rodar depois.</summary>
    Task CancelarTudoAsync(int formularioId, CancellationToken ct);
}

/// <summary>O efeito de publicar sobre o formulário: se já liga o <c>ATIVO</c> (agora) ou só marca o dia (agendado), se gera o código do link e a próxima reabertura de um formulário recorrente.</summary>
public sealed record EfeitoDaPublicacao(bool Ativar, bool GerarToken, DateTime? ProximaReaberturaEmUtc);

/// <summary>Os campos que a edição dos detalhes grava. <c>LimparPrazoERecorrencia</c> vale ao virar Sugestão (sempre aberta); <c>ExigirSemResposta</c>, ao mudar tipo ou sentido de um formulário com nome publicado.</summary>
public sealed record DetalhesParaGravar(string Titulo, string? Descricao, TipoDeFormulario Tipo, SentidoDaAvaliacao? Sentido, bool Anonimo, bool LimparPrazoERecorrencia, bool ExigirSemResposta);

/// <summary>
/// A gravação dos formulários. Toda mudança de estado é um <c>UPDATE</c> condicional ao estado lido (o banco decide a corrida): devolve <c>false</c> quando outra decisão chegou antes e o chamador relê para saber qual erro dar. Quem chama registra o evento de
/// auditoria na mesma transação (as entidades dos formulários não são auditadas pelo interceptor: a resposta nunca pode deixar rastro, e o resto tem eventos à mão).
/// </summary>
public interface IFormulariosRepositorio
{
    /// <summary>Grava o formulário (rascunho: <c>ATIVO</c> falso explícito) e devolve o id.</summary>
    Task<int> AdicionarAsync(Formulario formulario, CancellationToken ct);

    /// <summary>
    /// Muda os detalhes se o formulário ainda está como foi lido (<c>ATIVO</c>, anonimato e encerramento iguais) e, com <c>ExigirSemResposta</c>, se continua sem nenhuma resposta: <c>false</c> se outra aba publicou, encerrou ou recebeu resposta nesse meio tempo.
    /// </summary>
    Task<bool> AtualizarDetalhesAsync(FormularioLido lido, DetalhesParaGravar novos, CancellationToken ct);

    /// <summary>
    /// Guarda a publicação (alvo, prazo, recorrência, lembrete, o dia de um agendado) e troca as linhas de destinatário pelas listas novas, só se o formulário ainda está como foi lido (<c>ATIVO</c>, anonimato, encerramento e data de publicação iguais), anulando o código
    /// do link se o alvo deixou de ser <c>LINK</c>. <c>false</c> se outra aba publicou, encerrou ou mudou a data nesse meio tempo.
    /// </summary>
    Task<bool> AtualizarPublicacaoAsync(FormularioLido lido, PublicacaoParaGravar novos, CancellationToken ct);

    /// <summary>
    /// Publica ou agenda: grava a publicação como <see cref="AtualizarPublicacaoAsync"/> (<c>NovaPublicacaoEmUtc</c> é a data de publicação, agora ou o começo do dia agendado), liga <c>ATIVO</c> se <see cref="EfeitoDaPublicacao.Ativar"/>, gera o código do link (43 caracteres;
    /// nova tentativa se a chave única repetir) e grava a próxima reabertura. <c>false</c> se outra aba publicou, encerrou ou mudou o formulário nesse meio tempo.
    /// </summary>
    Task<bool> PublicarAsync(FormularioLido lido, PublicacaoParaGravar novos, EfeitoDaPublicacao efeito, CancellationToken ct);

    /// <summary>A tarefa de publicar: liga <c>ATIVO</c> do formulário AGENDADO cuja data é exatamente <paramref name="publicacaoLidaEmUtc"/> (e que não foi encerrado) e grava a próxima reabertura. <c>false</c> se ele mudou, foi publicado ou apagado.</summary>
    /// <summary>
    /// Reabre o formulário recorrente SE a próxima reabertura guardada ainda é <paramref name="reaberturaPrevistaEmUtc"/>: liga o <c>ATIVO</c> e limpa o encerramento no mesmo UPDATE, grava a nova publicação, o prazo da rodada e a reabertura seguinte, e APAGA as respostas e as participações da rodada anterior.
    /// Devolve os caminhos dos arquivos das respostas apagadas (o chamador os apaga do armazenamento depois da transação), ou <c>null</c> se a data já não era essa (outra execução avançou).
    /// </summary>
    Task<IReadOnlyList<string>?> ReabrirAsync(FormularioLido lido, DateTime reaberturaPrevistaEmUtc, DateTime novaPublicacaoEmUtc, DateOnly? novoPrazo, DateTime? proximaReaberturaEmUtc, CancellationToken ct);

    /// <summary>Troca a próxima reabertura (a rodada ainda estava aberta: avança sem apagar nada; ou <c>null</c>: a recorrência acabou) SE ela ainda é <paramref name="reaberturaPrevistaEmUtc"/>.</summary>
    Task<bool> AvancarReaberturaAsync(int formularioId, DateTime reaberturaPrevistaEmUtc, DateTime? proximaReaberturaEmUtc, CancellationToken ct);

    Task<bool> AtivarAgendadoAsync(int formularioId, DateTime publicacaoLidaEmUtc, DateTime? proximaReaberturaEmUtc, CancellationToken ct);

    /// <summary>
    /// Encerra o formulário PUBLICADO como está lido: <c>ATIVO</c> falso e a data de encerramento no mesmo UPDATE (o CHECK do banco os amarra). <paramref name="cancelarReaberturas"/> tira a próxima reabertura (o encerramento à mão cancela a recorrência; o do prazo não).
    /// <c>false</c> se outra aba encerrou ou mudou o formulário nesse meio tempo.
    /// </summary>
    Task<bool> EncerrarAsync(FormularioLido lido, DateTime encerradoEmUtc, bool cancelarReaberturas, CancellationToken ct);

    /// <summary>
    /// A exclusão FÍSICA, na ordem das chaves (nenhuma tem <c>ON DELETE</c>): respostas, participações, destinatários, candidatos, alternativas, perguntas e o formulário. Devolve os caminhos dos arquivos (currículos e respostas de arquivo) para quem chama apagar do armazenamento
    /// depois de confirmar a transação, ou <c>null</c> se o formulário já não existia (outra aba o apagou).
    /// </summary>
    Task<IReadOnlyList<string>?> ExcluirAsync(int formularioId, CancellationToken ct);

    /// <summary>
    /// Inclui as pessoas como destinatárias do formulário POR PESSOAS (quem já estava é ignorado, mesmo numa corrida: a chave única decide) e devolve quantas entraram agora. Segura a linha do formulário e confere que ele continua como foi lido (<c>ATIVO</c>, anonimato, encerramento
    /// e alvo <c>PESSOAS</c>); <c>null</c> se outra aba o mudou nesse meio tempo. Roda na transação de quem chama (o caso de uso da rota, ou o cadastro do colaborador).
    /// </summary>
    Task<int?> IncluirDestinatariosAsync(FormularioLido lido, IReadOnlyList<int> funcionarioIds, CancellationToken ct);
}
