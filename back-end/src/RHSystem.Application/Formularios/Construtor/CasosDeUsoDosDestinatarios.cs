using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Construtor;

/// <summary>
/// <c>POST /api/rh/formularios/{formularioId}/destinatarios</c>: acrescenta pessoas a um formulário POR PESSOAS (o cadastro e a importação chamam o mesmo efeito por dentro, <see cref="IEnvioDoFormularioDeAdmissao"/>). Ordem das recusas: 404, 400 (de 1 a 200 pessoas, números a partir de 1, pessoa que não existe),
/// 422 encerrado, 422 que não é por pessoas (por setores, por link, sem público ainda e avaliação, que tem os avaliados da própria regra), 409 anônimo PUBLICADO (nunca recebe pessoa nova: o público é a fotografia do dia; em rascunho e agendado ainda vale), 422 pessoa inativa (nenhuma entra).
/// Quem já estava é ignorado e conta em <c>jaEstavam</c>; a corrida de duas abas decide pela chave única do banco. <b>Nenhum e-mail.</b> Evento <c>ALTEROU</c> só quando entrou alguém, sem nomes.
/// </summary>
public sealed class AdicionarDestinatariosCasoDeUso(IConsultaDeFormularios consulta, IFormulariosRepositorio formularios, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public const int Maximo = 200;
    private const int Tentativas = 5;

    public async Task<FormularioDestinatariosResultado> ExecutarAsync(int formularioId, FormularioDestinatariosComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= Tentativas; tentativa++)
        {
            var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
            var ids = await ConferirCorpoAsync(corpo, ct);
            Conferir(lido, MapeadorDoFormulario.SituacaoDe(lido, relogio.AgoraUtc));
            if ((await consulta.PessoasAsync(ids, ct)).Any(p => !p.Ativo))
            {
                throw MensagensDosFormularios.PessoaInativaNoPublico.ComoProblema();
            }

            var adicionados = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (await formularios.IncluirDestinatariosAsync(lido, ids, ct) is not { } entraram)
                    {
                        return (int?)null;
                    }

                    if (entraram > 0)
                    {
                        auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Formularios, AuditoriaAcao.Alterou, $"Incluiu pessoas no formulário \"{lido.Titulo}\".", $"Formulário \"{lido.Titulo}\"", "FORMULARIOS", formularioId));
                        await unidade.SalvarAsync(ct);
                    }

                    return entraram;
                },
                ct);
            if (adicionados is { } n)
            {
                return new FormularioDestinatariosResultado(n, ids.Count - n);
            }
        }

        throw MensagensDosFormularios.FormularioMudouEmOutraTela;
    }

    private async Task<IReadOnlyList<int>> ConferirCorpoAsync(FormularioDestinatariosComando corpo, CancellationToken ct)
    {
        var pedidos = corpo.FuncionarioIds ?? [];
        var erros = new ColetorDeErros();
        if (pedidos.Count is < 1 or > Maximo)
        {
            erros.Adicionar("funcionarioIds", "Escolha de 1 a 200 pessoas.");
        }
        else if (pedidos.Any(id => id < 1))
        {
            erros.Adicionar("funcionarioIds", "Os números das pessoas começam em 1.");
        }

        var ids = pedidos.Where(id => id >= 1).Distinct().ToList();
        if (!erros.TemErros && (await consulta.PessoasAsync(ids, ct)).Count != ids.Count)
        {
            erros.Adicionar("funcionarioIds", "Uma das pessoas escolhidas não existe.");
        }

        erros.LancarSeHouver();
        return ids;
    }

    /// <summary>As recusas de estado e de alvo, na ordem do contrato.</summary>
    internal static void Conferir(FormularioLido lido, SituacaoDoFormulario situacao)
    {
        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioEncerradoParaEditar.ComoProblema();
        }

        if (lido.Alvo != AlvoDoFormulario.Pessoas || lido.Tipo == TipoDeFormulario.Avaliacao)
        {
            throw MensagensDosFormularios.FormularioNaoEPorPessoas.ComoProblema();
        }

        if (lido.Anonimo && situacao == SituacaoDoFormulario.Publicado)
        {
            throw MensagensDosFormularios.AnonimoSemNovosDestinatarios.ComoProblema();
        }
    }
}
