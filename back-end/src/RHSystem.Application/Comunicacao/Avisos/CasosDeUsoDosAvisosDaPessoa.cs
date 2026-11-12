using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Avisos;

internal static class MapeadorDoAvisoDaPessoa
{
    public static AvisoItemEu Item(AvisoDaPessoaLido l)
    {
        var tipo = l.Tipo ?? TipoDeAviso.Simples;
        return new AvisoItemEu(l.Id, tipo, l.Titulo, PreviaDoAviso.Montar(tipo, l.Conteudo), l.Fixado, l.PublicadoEmUtc, l.ExpiracaoEmUtc, l.AutorNome, l.Lido);
    }

    public static AvisoDetalheEu Detalhe(AvisoDaPessoaLido l)
    {
        var item = Item(l);
        return new AvisoDetalheEu(item.Id, item.Tipo, item.Titulo, item.Previa, item.Fixado, item.PublicadoEmUtc, item.ExpiraEmUtc, item.AutorNome, item.Lido, l.LidoEmUtc, l.Conteudo);
    }
}

/// <summary>
/// <c>GET /api/eu/avisos</c>: os avisos <b>vigentes</b> para quem está logado (qualquer perfil vê os mesmos: aviso é sempre para todos). Rascunho, agendado e expirado nunca aparecem; quem foi admitido depois da publicação também vê. Ordem: publicação mais
/// recente primeiro (com <c>fixadosPrimeiro</c>, os fixados antes, cada grupo do mais recente para o mais antigo). <c>contagens</c> (não lidos e total, e também o número do menu) não dependem de <c>busca</c> nem de <c>apenasNaoLidos</c>.
/// </summary>
public sealed class ListarMeusAvisosCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAvisosDaPessoa consulta, IRelogio relogio)
{
    public async Task<AvisoListaEu> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, bool apenasNaoLidos, bool fixadosPrimeiro, CancellationToken ct)
    {
        var parametros = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado();
        var (itens, total, contagens) = await consulta.ListarAsync(escopo.FuncionarioId, new FiltroDeMeusAvisos(apenasNaoLidos, fixadosPrimeiro), parametros, relogio.AgoraUtc, ct);
        var resposta = RespostaPaginada<AvisoItemEu>.De([.. itens.Select(MapeadorDoAvisoDaPessoa.Item)], total, parametros);
        return new AvisoListaEu(resposta.Itens, resposta.Pagina, resposta.TamanhoPagina, resposta.Total, resposta.TotalPaginas, contagens);
    }
}

/// <summary><c>GET /api/eu/avisos/{avisoId}</c>: o aviso vigente inteiro. <b>Não marca como lido</b> (quem abre chama também a rota de leitura). 404 se não existe ou não está vigente.</summary>
public sealed class ObterMeuAvisoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAvisosDaPessoa consulta, IRelogio relogio)
{
    public async Task<AvisoDetalheEu> ExecutarAsync(int avisoId, CancellationToken ct) =>
        MapeadorDoAvisoDaPessoa.Detalhe((await consulta.ObterAsync(escopo.FuncionarioId, avisoId, relogio.AgoraUtc, ct)).OuNaoEncontrado());
}

/// <summary>
/// <c>POST /api/eu/avisos/{avisoId}/leitura</c>: a pessoa abriu o aviso. Idempotente (a data da primeira leitura fica), cria a linha de quem foi admitido depois e responde 404 se o aviso não existe ou não está vigente. Não grava evento de auditoria
/// (é dado de controle da própria pessoa, de volume alto e sem efeito sobre ninguém).
/// </summary>
public sealed class MarcarAvisoComoLidoCasoDeUso(IEscopoDoUsuario escopo, IConsultaDeAvisosDaPessoa consulta, IAvisosRepositorio avisos, IRelogio relogio)
{
    public async Task ExecutarAsync(int avisoId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        _ = (await consulta.ObterAsync(escopo.FuncionarioId, avisoId, agora, ct)).OuNaoEncontrado();
        await avisos.MarcarComoLidoAsync(avisoId, escopo.FuncionarioId, InstanteAoSegundo.De(agora), ct);
    }
}
