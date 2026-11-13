using System.Text.RegularExpressions;
using RHSystem.Application.Arquivos;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Documentos;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Responder;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Publico;

/// <summary>A candidatura como chegou (<c>multipart/form-data</c>), em texto cru: o caso de uso valida tudo e devolve os erros juntos num 400 só.</summary>
public sealed record NovaCandidatura(string? Nome, string? Email, string? Telefone, string? Aceitou, string? Respostas, ArquivoRecebido? Curriculo, IReadOnlyList<ArquivoRecebido> Arquivos, int ArquivosRecebidos);

/// <summary>O agradecimento da tela de confirmação (<c>CandidatoRecibo</c>).</summary>
public sealed record CandidatoRecibo(string Mensagem);

/// <summary>O candidato já conferido, pronto para gravar: nome aparado, e-mail aparado e em minúsculas e o telefone como veio (ou nulo).</summary>
public sealed record CandidatoParaGravar(string Nome, string Email, string? Telefone, string? CaminhoDoCurriculo);

/// <summary>Uma resposta do candidato: a pergunta, o texto guardado, a alternativa e, na pergunta de arquivo, o caminho guardado e o nome limpo do arquivo.</summary>
public sealed record RespostaDoCandidato(int PerguntaId, string Resposta, int? AlternativaId, string? CaminhoArquivo, string? NomeDoArquivo);

/// <summary>A gravação da candidatura. O e-mail é único POR formulário (chave única do banco, sem diferenciar maiúscula): a repetida é 409 <c>JA_RESPONDIDO</c>, também em corrida.</summary>
public interface ICandidatosRepositorio
{
    /// <summary>O e-mail já respondeu este formulário (conferência rápida antes de guardar arquivo; a que vale é a chave única).</summary>
    Task<bool> EmailJaRespondeuAsync(int formularioId, string email, CancellationToken ct);

    /// <summary>Grava o candidato e todas as respostas dele com o MESMO instante (cortado ao segundo), numa transação do chamador. Chave repetida vira <c>JA_RESPONDIDO</c>.</summary>
    Task GravarAsync(int formularioId, CandidatoParaGravar candidato, DateTime instanteUtc, IReadOnlyList<RespostaDoCandidato> respostas, CancellationToken ct);
}

/// <summary>
/// <c>POST /api/publico/formularios/{token}/respostas</c> (sem login; o limite de 10 envios por hora por IP roda ANTES de tudo; <c>multipart/form-data</c>): a candidatura pelo link. Ordem das recusas: 404 e 422 do estado (as mesmas da página: código que não vale, agendado, encerrado, inclusive o prazo vencido),
/// 400 (nome, e-mail, telefone, aceite do aviso, respostas, currículo e arquivos, todos juntos), 409 <c>JA_RESPONDIDO</c> (o mesmo e-mail uma vez só por formulário, em qualquer caixa e em corrida). Tudo ou nada: os arquivos (PDF ou Word até 5 MB, o tipo pelo CONTEÚDO) vão para o armazenamento fora de transação,
/// UMA transação curta segura a linha do formulário e grava o candidato e as respostas com o mesmo instante; falhou, os blobs são apagados. <b>Nenhum dado do candidato</b> (nome, e-mail, telefone, currículo, respostas) vai a log, auditoria ou mensagem de erro: o caso de uso não grava evento nenhum.
/// </summary>
public sealed partial class ResponderFormularioPublicoCasoDeUso(
    IConsultaDeFormularios consulta,
    IFormulariosRespostasRepositorio formularios,
    ICandidatosRepositorio candidatos,
    EnvioDeArquivosDeFormulario envio,
    IUnidadeDeTrabalho unidade,
    IRelogio relogio)
{
    public const string Agradecimento = "Recebemos as suas respostas. Obrigado por se candidatar.";

    public async Task<CandidatoRecibo> ExecutarAsync(string token, NovaCandidatura novo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var lido = await AcessoPublico.ObterAsync(consulta, token, agora, ct);
        var erros = new ColetorDeErros();
        var nome = LerNome(erros, novo.Nome);
        var email = LerEmail(erros, novo.Email);
        var telefone = LerTelefone(erros, novo.Telefone);
        LerAceite(erros, novo.Aceitou);
        var linhas = ValidacaoDaResposta.Validar(erros, lido.Perguntas, novo.Respostas, novo.Arquivos.Count);
        IReadOnlyList<ArquivoDeRespostaConferido> curriculo = novo.Curriculo is { } enviado ? await ConferenciaDeArquivos.ConferirAsync(erros, "curriculo", [enviado], ct) : [];
        var arquivos = await ConferenciaDeArquivos.ConferirAsync(erros, "arquivos", novo.Arquivos, ct);
        if (novo.ArquivosRecebidos != novo.Arquivos.Count + (novo.Curriculo is null ? 0 : 1))
        {
            erros.Adicionar("arquivos", "Envie os arquivos nas partes \"curriculo\" e \"arquivos\".");
        }

        erros.LancarSeHouver();

        if (await candidatos.EmailJaRespondeuAsync(lido.Id, email!, ct))
        {
            throw MensagensDosFormularios.JaRespondidoPeloCandidato.ComoProblema();
        }

        var todos = new List<ArquivoDeRespostaConferido>([.. curriculo, .. arquivos]);
        await envio.GravarEEfetivarAsync(todos, caminhos => GravarAsync(lido, new CandidatoParaGravar(nome!, email!, telefone, curriculo.Count > 0 ? caminhos[0] : null), linhas, arquivos, caminhos.Skip(curriculo.Count).ToList(), agora, ct), ct);
        return new CandidatoRecibo(Agradecimento);
    }

    private async Task<bool> GravarAsync(FormularioLido lido, CandidatoParaGravar candidato, IReadOnlyList<RespostaValidada> linhas, IReadOnlyList<ArquivoDeRespostaConferido> arquivos, IReadOnlyList<string> caminhos, DateTime agora, CancellationToken ct)
    {
        var gravou = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                if (!await formularios.SegurarAbertoAsync(lido.Id, false, ct))
                {
                    return false;
                }

                var respostas = linhas.Select(l => l.ArquivoIndice is { } indice
                    ? new RespostaDoCandidato(l.Pergunta.Id, arquivos[indice].Nome, null, caminhos[indice], arquivos[indice].Nome)
                    : new RespostaDoCandidato(l.Pergunta.Id, l.Resposta, l.AlternativaId, null, null)).ToList();
                await candidatos.GravarAsync(lido.Id, candidato, InstanteAoSegundo.De(agora), respostas, ct);
                return true;
            },
            ct);
        if (!gravou)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaResponder.ComoProblema(); // outra aba (ou o agendador) encerrou o formulário depois da leitura
        }

        return gravou;
    }

    private static string? LerNome(ColetorDeErros erros, string? texto)
    {
        var nome = texto?.Trim();
        if (string.IsNullOrEmpty(nome))
        {
            erros.Adicionar("nome", "Informe o seu nome.");
            return null;
        }

        if (nome.Length > 150)
        {
            erros.Adicionar("nome", "O nome pode ter até 150 caracteres.");
            return null;
        }

        return nome;
    }

    private static string? LerEmail(ColetorDeErros erros, string? texto)
    {
        var email = texto?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(email))
        {
            erros.Adicionar("email", "Informe o seu e-mail.");
            return null;
        }

        if (email.Length > 150 || !FormatoDoEmail().IsMatch(email))
        {
            erros.Adicionar("email", "Informe um e-mail válido, com até 150 caracteres.");
            return null;
        }

        return email;
    }

    private static string? LerTelefone(ColetorDeErros erros, string? texto)
    {
        var telefone = texto?.Trim();
        if (string.IsNullOrEmpty(telefone))
        {
            return null;
        }

        if (!FormatoDoTelefone().IsMatch(telefone))
        {
            erros.Adicionar("telefone", "Use só números, espaço, parênteses, + e -, de 8 a 30 caracteres.");
            return null;
        }

        return telefone;
    }

    private static void LerAceite(ColetorDeErros erros, string? texto)
    {
        if (!bool.TryParse(texto?.Trim(), out var aceitou) || !aceitou)
        {
            erros.Adicionar("aceitouAvisoDePrivacidade", "Para enviar é preciso aceitar o aviso de privacidade.");
        }
    }

    [GeneratedRegex(@"^[^@\s]+@[^@\s]+\.[^@\s]+$")]
    private static partial Regex FormatoDoEmail();

    [GeneratedRegex(@"^[0-9()+\- ]{8,30}$")]
    private static partial Regex FormatoDoTelefone();
}
