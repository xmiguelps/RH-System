using RHSystem.Application.Autenticacao;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Gestores;

/// <summary>
/// Os gestores (tela 29, "Quem gerencia cada setor"): lista, cadastra quem do RH decide os pedidos de gestores e liga e desliga "Gestor ausente". Só o perfil RH. A ordem de avaliação é a do contrato: 404, 400, 422 (e 409
/// no cadastro: 400, 422 da pessoa inativa, 422 do nível, 409). "Hoje" é o dia em -03:00. Marcar a ausência grava a marca e chama a porta dos pedidos parados (<see cref="IPedidosParadosNoGestor"/>, da 1a) NA MESMA transação:
/// se a porta falha, a marcação é desfeita.
/// </summary>
public sealed class GestoresCasoDeUso(IGestoresRepositorio gestores, IPedidosParadosNoGestor pedidosParados, IUnidadeDeTrabalho unidade, IRelogio relogio)
{
    public async Task<GestorListaResposta> ListarAsync(CancellationToken ct) => new(await gestores.ListarAsync(Hoje(), ct));

    public async Task<GestorItem> CadastrarAsync(GestorCadastrarComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var pessoa = comando.FuncionarioId > 0 ? await gestores.ObterPessoaAsync(comando.FuncionarioId, ct) : null;
        if (pessoa is null)
        {
            throw new ErroDeValidacaoException("funcionarioId", "A pessoa escolhida não existe.");
        }

        if (!pessoa.Ativo)
        {
            throw MensagensDoApoio.FuncionarioInativo.ComoErroDeNegocio();
        }

        if (pessoa.Perfil != Perfil.RH)
        {
            throw MensagensDoApoio.GestorSemSetorSoParaRh.ComoErroDeNegocio();
        }

        if (await gestores.JaECadastradoAsync(pessoa.Id, ct))
        {
            throw MensagensDoApoio.GestorJaCadastrado.ComoConflito();
        }

        var gestor = await gestores.AdicionarAsync(pessoa, ct);
        return await gestores.ObterAsync(gestor.Id, Hoje(), ct) ?? throw new InvalidOperationException("O gestor recém-cadastrado não foi encontrado.");
    }

    public async Task<GestorAusenciaResposta> ConfigurarAusenciaAsync(int gestorId, GestorAusenciaComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var hoje = Hoje();
        var item = await gestores.ObterAsync(gestorId, hoje, ct) ?? throw new NaoEncontradoException();
        Validar(comando, hoje);
        if (comando.Ausente && item.SoDecidePedidosDeGestores)
        {
            throw MensagensDoApoio.GestorSemEquipe(item.Funcionario.Nome).ComoErroDeNegocio();
        }

        // A marca e a chamada à porta valem juntas ou nenhuma vale. Desmarcar não chama a porta: só os pedidos novos voltam ao normal, o que já foi aprovado automaticamente continua aprovado.
        var (gestor, aprovados) = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var linha = await gestores.ObterParaAusenciaAsync(gestorId, ct) ?? throw new NaoEncontradoException();
                if (comando.Ausente)
                {
                    linha.MarcarAusente(comando.AusenteAte, hoje);
                }
                else
                {
                    linha.DesmarcarAusencia();
                }

                await gestores.SalvarAsync(ct);
                var aprovadosAgora = comando.Ausente ? await pedidosParados.AprovarEtapaDoGestorAsync(gestorId, ct) : PedidosAprovados.Zero;
                return (linha, aprovadosAgora);
            },
            ct);

        return new GestorAusenciaResposta(
            item.GestorId,
            item.Funcionario,
            item.Setor,
            item.SoDecidePedidosDeGestores,
            item.QuantidadeEquipe,
            gestor.Ausente,
            gestor.AusenteAte,
            gestor.AusenteEm(hoje),
            aprovados);
    }

    private static void Validar(GestorAusenciaComando comando, DateOnly hoje)
    {
        var erros = new ColetorDeErros();
        if (comando.Ausente && comando.AusenteAte is { } volta && volta <= hoje)
        {
            erros.Adicionar("ausenteAte", "A data de volta precisa ser depois de hoje.");
        }

        if (!comando.Ausente && comando.AusenteAte is not null)
        {
            erros.Adicionar("ausenteAte", "Ao desmarcar a ausência, a data de volta precisa ficar em branco.");
        }

        erros.LancarSeHouver();
    }

    private DateOnly Hoje() => FusoBrasil.Hoje(relogio.AgoraUtc);
}
