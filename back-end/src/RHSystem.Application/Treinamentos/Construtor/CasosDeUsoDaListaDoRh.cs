using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Treinamentos.Regras;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>
/// <c>GET /api/rh/treinamentos</c>: a lista do RH (tela 22), com os rascunhos. Ordem padrão: <c>COMECA_EM</c> decrescente; <c>ordenarPor</c> aceita <c>TITULO</c> e <c>COMECA_EM</c> e a direção padrão é <c>DESC</c> com qualquer um (empate sempre pelo id). <c>busca</c> procura no título. <c>situacao</c> (a aba) e as <c>contagens</c> usam a MESMA situação por data, calculada no banco com o dia de hoje em -03:00;
/// as <c>contagens</c> ignoram <c>situacao</c> e respeitam <c>busca</c>. Os erros de campo (<c>situacao</c>, <c>ordenarPor</c>, <c>direcao</c>, página, busca) vêm juntos num 400.
/// </summary>
public sealed class ListarTreinamentosCasoDeUso(IConsultaDeTreinamentos consulta, IRelogio relogio)
{
    public static readonly string[] ValoresDeOrdenarPor = ["TITULO", "COMECA_EM"];

    public async Task<TreinamentoRhLista> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? situacao, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        SituacaoDoTreinamento? situacaoLida = null;
        DirecaoDeOrdenacao? direcaoLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => situacaoLida = EnumsDeConsulta.Ler<SituacaoDoTreinamento>(situacao, "situacao"));
        LeituraDeFiltros.Tentar(erros, () => direcaoLida = EnumsDeConsulta.Ler<DirecaoDeOrdenacao>(direcao, "direcao"));
        if (!string.IsNullOrEmpty(ordenarPor) && !ValoresDeOrdenarPor.Contains(ordenarPor, StringComparer.Ordinal))
        {
            erros.Adicionar("ordenarPor", $"Valor inválido. Use um destes: {string.Join(", ", ValoresDeOrdenarPor)}.");
        }

        erros.LancarSeHouver();

        var parametros = paginacao! with { OrdenarPor = string.IsNullOrEmpty(ordenarPor) ? null : ordenarPor, Direcao = direcaoLida ?? DirecaoDeOrdenacao.Desc };
        var (itens, total, contagens) = await consulta.ListarAsync(situacaoLida, parametros, FusoBrasil.Hoje(relogio.AgoraUtc), ct);
        return new TreinamentoRhLista(itens, parametros.Pagina, parametros.TamanhoPagina, total, (int)(((long)total + parametros.TamanhoPagina - 1) / parametros.TamanhoPagina), contagens);
    }
}

/// <summary><c>GET /api/rh/treinamentos/resumo</c>: os quatro números do topo da tela 22 (e o <c>ano</c> do rótulo), calculados com o dia de hoje em -03:00.</summary>
public sealed class ObterIndicadoresDeTreinamentosCasoDeUso(IConsultaDeTreinamentos consulta, IRelogio relogio)
{
    public async Task<TreinamentoRhIndicadores> ExecutarAsync(CancellationToken ct) => await consulta.IndicadoresAsync(FusoBrasil.Hoje(relogio.AgoraUtc), ct);
}
