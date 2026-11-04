using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Auditoria;

/// <summary>A lista do log de auditoria (tela 31 e o quadro "Últimas alterações" do painel do RH). Só leitura, só o perfil RH.</summary>
public sealed class ListarEventosDeAuditoriaCasoDeUso(IConsultaDeAuditoria consulta, IRelogio relogio)
{
    /// <exception cref="RHSystem.Application.Comum.ErroDeValidacaoException">Algum parâmetro inválido; todos os erros vêm juntos.</exception>
    public async Task<AuditoriaListaResposta> ExecutarAsync(PedidoDeAuditoria pedido, int pagina, int tamanhoPagina, CancellationToken ct)
    {
        var (filtro, paginacao) = ValidacaoDeAuditoria.Validar(pedido, new ParametrosDeLista(pagina, tamanhoPagina, pedido.Busca), relogio.AgoraUtc);
        var resultado = await consulta.ListarAsync(filtro, paginacao, ct);
        return new AuditoriaListaResposta(resultado.Itens, resultado.Pagina, resultado.TamanhoPagina, resultado.Total, resultado.TotalPaginas);
    }
}
