using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>O conteúdo de uma pergunta a gravar: o texto e as alternativas já aparados e conferidos.</summary>
public sealed record PerguntaParaGravar(string Texto, TipoDePergunta Tipo, bool Obrigatoria, IReadOnlyList<AlternativaValidada> Alternativas);

/// <summary>
/// A gravação das perguntas e das alternativas. Tudo roda dentro da transação do caso de uso. A ordem é única por formulário e o banco confere linha a linha: inserir e mover passam por dois passos (<c>ORDEM + 1000</c> e depois o valor final), e a corrida de duas
/// abas na mesma posição vira <c>ProblemaDeApiException</c> 409 (<see cref="RHSystem.Application.Formularios.Mensagens.MensagensDosFormularios.PerguntasMudaramEmOutraTela"/>), que o caso de uso tenta de novo. As regras do banco (pergunta respondida) viram os 422 do contrato.
/// </summary>
public interface IPerguntasDoFormularioRepositorio
{
    /// <summary>
    /// Segura a linha do formulário se ele ainda está como foi lido (<c>ATIVO</c>, anonimato e encerramento iguais): quem publica ou encerra por último espera esta transação acabar. <c>false</c> se outra aba mudou o formulário nesse meio tempo (o chamador relê).
    /// </summary>
    Task<bool> SegurarSeIgualAoLidoAsync(FormularioLido lido, CancellationToken ct);

    /// <summary>Grava a pergunta na posição <paramref name="posicao"/> (as que estavam dali em diante descem) e as alternativas, e devolve o id da pergunta.</summary>
    Task<int> AdicionarAsync(int formularioId, PerguntaParaGravar pergunta, int posicao, CancellationToken ct);

    /// <summary>Troca o conteúdo da pergunta, move para <paramref name="novaPosicao"/> (sem valor, fica) e acerta as alternativas: com id mantém (muda o texto e a posição), sem id é nova, a que sumiu da lista é apagada.</summary>
    Task EditarAsync(int formularioId, PerguntaLida atual, PerguntaParaGravar nova, int? novaPosicao, CancellationToken ct);

    /// <summary>Apaga as alternativas e a pergunta, e as seguintes sobem uma posição.</summary>
    Task ExcluirAsync(int formularioId, PerguntaLida atual, CancellationToken ct);
}
