using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>
/// <c>POST /api/rh/treinamentos/{treinamentoId}/publicar</c>: publica o que está salvo (sem corpo). Ordem: 404, 422 <c>TREINAMENTO_JA_PUBLICADO</c> (também o encerrado), 400 com os erros por campo todos juntos (título, data de início, público com ao menos um setor ou uma pessoa, ao menos um módulo). Tudo roda numa transação que começa TRAVANDO a linha do
/// treinamento (as rotas de módulo se enfileiram nela, então o "tem módulo" conferido aqui não muda até o commit); a publicação é um <c>UPDATE</c> condicional a "ainda não publicado": duas chamadas ao mesmo tempo dão um 200 e um 422. No treinamento OBRIGATÓRIO cada pessoa ATIVA com a admissão já ocorrida do público ganha uma linha
/// <c>INSCRITO</c> (quem já tem linha é pulado); opcional não cria linha. Nenhum e-mail sai. Evento <c>PUBLICOU</c> com a quantidade e sem nome de pessoa.
/// </summary>
public sealed class PublicarTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IModulosRepositorio modulos, ITreinamentosRepositorio treinamentos, IPublicoDoTreinamento publico, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<TreinamentoRh> ExecutarAsync(int treinamentoId, CancellationToken ct)
    {
        if ((await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado().Publicado)
        {
            throw MensagensDosTreinamentos.TreinamentoJaPublicado.ComoProblema();
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var estado = await modulos.TravarTreinamentoAsync(treinamentoId, ct) ?? throw new NaoEncontradoException();
                if (estado.Publicado)
                {
                    throw MensagensDosTreinamentos.TreinamentoJaPublicado.ComoProblema();
                }

                var lido = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
                var erros = new ColetorDeErros();
                _ = ValidacaoDoTreinamento.Validar(erros, lido.Titulo, lido.Descricao, lido.ComecaEm, lido.Obrigatorio, lido.Onboarding, lido.EmiteCertificado, lido.Publico, lido.SetorIds, lido.FuncionarioIds, lido.IncluiAdmitidos);
                ValidacaoDoTreinamento.ConferirParaPublicar(erros, lido.Campos, lido.TotalDeModulos);
                erros.LancarSeHouver();

                if (!await treinamentos.PublicarAsync(treinamentoId, ct))
                {
                    throw MensagensDosTreinamentos.TreinamentoJaPublicado.ComoProblema();
                }

                var inscritos = lido.Obrigatorio ? await treinamentos.InscreverAsync(treinamentoId, await publico.PessoasAsync(treinamentoId, hoje, ct), ct) : 0;
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Publicou, DescricaoDaPublicacao(lido.Titulo, lido.Obrigatorio, inscritos), $"Treinamento \"{lido.Titulo}\"", "TREINAMENTOS", treinamentoId));
                await unidade.SalvarAsync(ct);
                return true;
            },
            ct);
        return (await consulta.ObterDetalheAsync(treinamentoId, hoje, ct)).OuNaoEncontrado();
    }

    internal static string DescricaoDaPublicacao(string titulo, bool obrigatorio, int inscritos) =>
        obrigatorio
            ? $"Publicou o treinamento \"{titulo}\" e inscreveu {inscritos} {(inscritos == 1 ? "pessoa" : "pessoas")}."
            : $"Publicou o treinamento \"{titulo}\".";
}
