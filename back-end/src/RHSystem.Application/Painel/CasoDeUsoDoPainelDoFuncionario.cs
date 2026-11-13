using Microsoft.Extensions.Logging;
using RHSystem.Application.Beneficios.Adesoes;
using RHSystem.Application.Ferias;
using RHSystem.Application.Pagamento;

namespace RHSystem.Application.Painel;

/// <summary>
/// <c>GET /api/eu/painel</c>: os cartões da tela inicial do funcionário e do gestor que são da gestão (férias, próximo pagamento e benefícios), SEMPRE os da própria pessoa. Cada cartão sai da MESMA consulta da rota de origem (<c>obterMinhasFerias</c>, <c>obterProximoPagamento</c>, <c>obterMeusBeneficios</c>): nunca uma conta
/// paralela. <b>Uma fonte que falha deixa o cartão vazio</b> (zero dias, pagamento nulo, nenhum benefício) e registra um <c>Warning</c> só com o nome do cartão e o tipo da exceção (a mensagem pode trazer dado pessoal): a tela inicial não cai por causa de um cartão. O cancelamento do pedido sobe. Os benefícios seguem a ordem
/// do catálogo; o <c>detalhe</c> junta "Você e N dependente(s)" (só com dependentes aprovados) e as linhas do trajeto em vigor, separados por " · ".
/// </summary>
public sealed class ObterPainelDoFuncionarioCasoDeUso(
    ObterMinhasFeriasCasoDeUso ferias,
    ObterProximoPagamentoCasoDeUso pagamento,
    ObterMeusBeneficiosCasoDeUso beneficios,
    ILogger<ObterPainelDoFuncionarioCasoDeUso> log)
{
    public async Task<PainelFuncionarioDto> ExecutarAsync(CancellationToken ct)
    {
        var cartaoDeFerias = await IsolarAsync("férias", async () =>
        {
            var resumo = (await ferias.ExecutarAsync(ct)).Resumo;
            return new PainelFeriasResumoDto(resumo.DiasDisponiveis, resumo.PrazoParaTirarSaldo);
        }, new PainelFeriasResumoDto(0, null));
        var proximo = await IsolarAsync("próximo pagamento", () => pagamento.ExecutarAsync(ct), new PagamentoProximoDto(null, null));
        var cartoes = await IsolarAsync("benefícios", async () => (await beneficios.ExecutarAsync(ct)).Beneficios.Select(Resumir).ToList(), []);
        return new PainelFuncionarioDto(cartaoDeFerias, proximo, cartoes);
    }

    private static PainelBeneficioResumoDto Resumir(CartaoDeBeneficioDto cartao)
    {
        var partes = new List<string>();
        if (cartao.Dependentes.Count > 0)
        {
            partes.Add(cartao.Dependentes.Count == 1 ? "Você e 1 dependente" : $"Você e {cartao.Dependentes.Count} dependentes");
        }

        if (cartao.TrajetoAtual is { } trajeto)
        {
            partes.Add(trajeto.LinhasMeios);
        }

        return new PainelBeneficioResumoDto(cartao.AdesaoId, cartao.Beneficio.Nome, partes.Count == 0 ? null : string.Join(" · ", partes));
    }

    private async Task<T> IsolarAsync<T>(string cartao, Func<Task<T>> consulta, T vazio)
    {
        try
        {
            return await consulta();
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception erro)
        {
            log.LogWarning("O cartão {Cartao} do painel falhou ({Erro}); ele sai vazio.", cartao, erro.GetType().Name);
            return vazio;
        }
    }
}
