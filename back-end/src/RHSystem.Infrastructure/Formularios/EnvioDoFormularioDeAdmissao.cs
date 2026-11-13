using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// A caixa "Enviar o formulário de admissão" do cadastro, da readmissão e da importação (fatias 1 e 2): o MESMO efeito de <c>adicionarDestinatariosFormulario</c>, por dentro e na transação de quem cadastra (tudo ou nada). <see cref="ConferirAsync"/> roda ANTES de gravar:
/// 400 em <c>formularioAdmissaoId</c> se o formulário não existe, não é de admissão, ainda não está publicado (rascunho ou agendado) ou é anônimo (o anônimo publicado não recebe pessoa nova); 422 <c>FORMULARIO_ENCERRADO</c> (com o texto do cadastro) se encerrou; 422 <c>FORMULARIO_NAO_E_POR_PESSOAS</c>
/// se o público não é por pessoas escolhidas. <b>Nenhum e-mail sai.</b>
/// </summary>
public sealed class EnvioDoFormularioDeAdmissao(IConsultaDeFormularios consulta, IFormulariosRepositorio formularios, IRelogio relogio) : IEnvioDoFormularioDeAdmissao
{
    private static ErroDeValidacaoException NaoExisteOuNaoPublicado() => new("formularioAdmissaoId", "O formulário de admissão escolhido não existe ou não está publicado.");

    public async Task ConferirAsync(int formularioId, CancellationToken ct)
    {
        var lido = await consulta.ObterAsync(formularioId, ct);
        if (lido is null || lido.Tipo != TipoDeFormulario.Admissao)
        {
            throw NaoExisteOuNaoPublicado();
        }

        var situacao = RegraDaSituacaoDoFormulario.De(lido.Ativo, lido.PublicadoEmUtc, lido.EncerradoEmUtc, lido.Prazo, relogio.AgoraUtc);
        if (situacao == SituacaoDoFormulario.Encerrado)
        {
            throw MensagensDosFormularios.FormularioDeAdmissaoEncerrado.ComoProblema();
        }

        if (situacao != SituacaoDoFormulario.Publicado || (lido.Anonimo && lido.Alvo == AlvoDoFormulario.Pessoas))
        {
            throw NaoExisteOuNaoPublicado();
        }

        if (lido.Alvo != AlvoDoFormulario.Pessoas)
        {
            throw MensagensDosFormularios.FormularioNaoEPorPessoas.ComoProblema();
        }
    }

    public async Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct)
    {
        var lido = (await consulta.ObterAsync(formularioId, ct)).OuNaoEncontrado();
        return await formularios.IncluirDestinatariosAsync(lido, funcionarioIds, ct) ?? throw MensagensDosFormularios.FormularioMudouEmOutraTela;
    }
}
