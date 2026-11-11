using Microsoft.Extensions.Logging;
using RHSystem.Application.Escopo;

namespace RHSystem.Application.Painel;

/// <summary>
/// Os números das bolinhas do menu. <b>Várias fontes do mesmo contador somam; sem fonte, o contador vale 0</b> (antes de a fatia dona existir não há o que contar, e o
/// contrato exige inteiro, nunca <c>null</c>). <b>Uma fonte que lança conta 0 e registra o erro no log: o menu não pode cair por causa de uma bolinha.</b> O log leva o
/// nome do contador e o tipo da exceção, nunca a mensagem (pode trazer dado pessoal). Cada rota consulta só as fontes dos contadores dela, uma de cada vez
/// (o contexto do banco da requisição não é paralelo). O cancelamento do pedido não vira zero: sobe.
/// </summary>
public sealed class ContadoresDoMenuCasoDeUso(IEnumerable<IFonteDeContadorDoMenu> fontes, IEscopoDoUsuario escopo, ILogger<ContadoresDoMenuCasoDeUso> log)
{
    public async Task<PainelContadoresFuncionario> ObterDoFuncionarioAsync(CancellationToken ct) =>
        new(
            await SomarAsync(ContadorDoMenu.AvisosNaoLidos, ct),
            await SomarAsync(ContadorDoMenu.FormulariosPendentes, ct),
            await SomarAsync(ContadorDoMenu.DocumentosPendentes, ct));

    public async Task<PainelContadoresGestor> ObterDoGestorAsync(CancellationToken ct) =>
        new(
            await SomarAsync(ContadorDoMenu.PendenciasDaEquipe, ct),
            await SomarAsync(ContadorDoMenu.PedidosDeAjusteDePontoDoGestor, ct));

    public async Task<PainelContadoresRh> ObterDoRhAsync(CancellationToken ct) =>
        new(await SomarAsync(ContadorDoMenu.PedidosDeAjusteDePontoDoRh, ct));

    private async Task<int> SomarAsync(ContadorDoMenu contador, CancellationToken ct)
    {
        var total = 0;
        foreach (var fonte in fontes.Where(f => f.Contador == contador))
        {
            try
            {
                total += await fonte.ContarAsync(escopo, ct);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested)
            {
                throw;
            }
            catch (Exception erro)
            {
                log.LogError("A fonte do contador {Contador} falhou ({Erro}); ela conta 0.", contador, erro.GetType().Name);
            }
        }

        return total;
    }
}
