using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Auditoria;

/// <summary>
/// Valida o pedido da lista e o da exportação do mesmo jeito e devolve TODOS os erros de uma vez (<c>pagina</c>, <c>tamanhoPagina</c>, <c>busca</c>,
/// <c>area</c>, <c>acao</c>, <c>de</c>, <c>ate</c>): quem manda <c>area=XYZ&amp;acao=ZZZ</c> recebe os dois erros no mesmo 400, e não um de cada vez.
/// </summary>
internal static class ValidacaoDeAuditoria
{
    /// <summary>Os extremos aceitos para <c>de</c> e <c>ate</c>: <c>0001-01-01</c> e <c>9999-12-31</c> estourariam a conversão de fuso e virariam 500.</summary>
    internal static readonly DateOnly MenorDia = new(2000, 1, 1);

    internal static readonly DateOnly MaiorDia = new(2999, 12, 31);

    /// <summary>
    /// Período padrão: <c>ate</c> = hoje em Brasília e <c>de</c> = <c>ate</c> menos 6 dias ("Últimos 7 dias"). Os dois são dias INCLUSIVOS em -03:00, então em
    /// instantes é <c>[InicioDoDia(de), InicioDoDia(ate + 1 dia))</c>.
    /// </summary>
    internal static (FiltroDeAuditoria Filtro, ParametrosDeLista Paginacao) Validar(PedidoDeAuditoria pedido, ParametrosDeLista paginacao, DateTime agoraUtc)
    {
        var erros = new ColetorDeErros();
        var validada = Coletar(erros, paginacao.Validado);
        var area = Coletar(erros, () => EnumsDeConsulta.Ler<AuditoriaArea>(pedido.Area, "area"));
        var acao = Coletar(erros, () => EnumsDeConsulta.Ler<AuditoriaAcao>(pedido.Acao, "acao"));
        var datasNoIntervalo = true;
        var foraDoIntervalo = $"A data precisa estar entre {MenorDia:dd/MM/yyyy} e {MaiorDia:dd/MM/yyyy}.";
        if (pedido.De is { } deInformado && (deInformado < MenorDia || deInformado > MaiorDia))
        {
            erros.Adicionar("de", foraDoIntervalo);
            datasNoIntervalo = false;
        }

        if (pedido.Ate is { } ateInformado && (ateInformado < MenorDia || ateInformado > MaiorDia))
        {
            erros.Adicionar("ate", foraDoIntervalo);
            datasNoIntervalo = false;
        }

        var ate = pedido.Ate ?? FusoBrasil.Hoje(agoraUtc);
        var de = pedido.De ?? ate.AddDays(-6);
        if (datasNoIntervalo && de > ate)
        {
            erros.Adicionar("de", "A data inicial não pode ser depois da data final.");
        }

        erros.LancarSeHouver();
        var filtro = new FiltroDeAuditoria(FusoBrasil.InicioDoDiaEmUtc(de), FusoBrasil.InicioDoDiaEmUtc(ate.AddDays(1)), validada!.Busca, area, acao, pedido.ApenasAlteracoes);
        return (filtro, validada);
    }

    /// <summary>Roda a validação de um campo; se ela lançar, passa os erros para o coletor (e segue, para os outros campos também serem conferidos).</summary>
    private static T? Coletar<T>(ColetorDeErros erros, Func<T> validacao)
    {
        try
        {
            return validacao();
        }
        catch (ErroDeValidacaoException erro)
        {
            foreach (var (campo, mensagens) in erro.Erros)
            {
                foreach (var mensagem in mensagens)
                {
                    erros.Adicionar(campo, mensagem);
                }
            }

            return default;
        }
    }
}
