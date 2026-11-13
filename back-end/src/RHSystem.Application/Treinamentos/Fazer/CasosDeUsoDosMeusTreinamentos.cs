using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Treinamentos.Regras;

namespace RHSystem.Application.Treinamentos.Fazer;

/// <summary>Monta o cartão e o detalhe da pessoa a partir do que a consulta leu: o progresso, a situação dela, o instante da conclusão (o do último módulo) e o certificado (só com a linha <c>CONCLUIDO</c>, treinamento que emite e código versão 4).</summary>
internal static class MapeadorDoMeuTreinamento
{
    public static TreinamentoMeuCartao Cartao(MeuTreinamentoLido t)
    {
        var progresso = ProgressoDoTreinamento.Calcular([.. t.Modulos.Select(m => new ModuloParaProgresso(m.Ordem, m.DuracaoMinutos, m.ConcluidoEmUtc is not null))]);
        var situacao = MinhaSituacao.De(t.Linha);
        var concluido = situacao == MinhaSituacaoNoTreinamento.Concluido;
        DateTime? concluidoEm = concluido ? t.Modulos.Where(m => m.ConcluidoEmUtc is not null).Select(m => m.ConcluidoEmUtc).Max() : null;
        var certificado = concluido && t.EmiteCertificado && t.Certificado is { EmitidoEm: { } emitidoEm, CodigoValidacao: { } codigo } emitido && CodigoDoCertificado.EhVersao4(codigo)
            ? new CertificadoResumo(NumeroDoCertificado.De(emitidoEm.Year, emitido.Id), codigo, emitidoEm)
            : null;
        return new TreinamentoMeuCartao(t.Id, t.Titulo, t.Descricao, t.Obrigatorio, t.Onboarding, situacao, t.EncerradoEmUtc is not null, t.ComecaEm, progresso.Total, progresso.DuracaoTotalMinutos, progresso.Concluidos, progresso.Percentual, progresso.ModuloAtual, t.IniciadoEm, concluidoEm, certificado);
    }

    public static TreinamentoMeuDetalhe Detalhe(MeuTreinamentoLido t)
    {
        var c = Cartao(t);
        var modulos = t.Modulos.OrderBy(m => m.Ordem).Select(m => new TreinamentoModuloEu(m.Id, m.Ordem, m.Titulo, m.Tipo, m.DuracaoMinutos, m.ConcluidoEmUtc is not null, m.ConcluidoEmUtc)).ToList();
        return new TreinamentoMeuDetalhe(c.Id, c.Titulo, c.Descricao, c.Obrigatorio, c.Onboarding, c.Situacao, c.Encerrado, c.ComecaEm, c.TotalModulos, c.DuracaoTotalMinutos, c.ModulosConcluidos, c.ProgressoPercentual, c.ModuloAtual, c.IniciadoEm, c.ConcluidoEmUtc, c.Certificado, modulos);
    }
}

/// <summary>
/// <c>GET /api/eu/treinamentos</c>: os cartões da pessoa logada (qualquer perfil, sempre a PRÓPRIA linha). Só publicados já liberados; entram os treinamentos com linha <c>INSCRITO</c>, <c>EM_ANDAMENTO</c> ou <c>CONCLUIDO</c> e os opcionais do público dela que ela ainda não escolheu (<c>DISPONIVEL</c>); o encerrado só para quem começou ou concluiu; linha <c>REPROVADO</c>, <c>CANCELADO</c> ou sem situação não aparece. <c>aba</c> padrão
/// <c>TODOS</c> (tudo que não foi concluído); as <c>contagens</c> ignoram <c>aba</c> e respeitam <c>busca</c> e <c>obrigatorio</c>. Ordem fixa: obrigatórios primeiro, <c>comecaEm</c> mais antigo (sem data por último), título e id. Os erros de campo (<c>aba</c>, página, busca) vêm juntos num 400.
/// </summary>
public sealed class ListarMeusTreinamentosCasoDeUso(IConsultaDeMeusTreinamentos consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<TreinamentoMeuLista> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? aba, bool? obrigatorio, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        AbaDosMeusTreinamentos? abaLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => abaLida = EnumsDeConsulta.Ler<AbaDosMeusTreinamentos>(aba, "aba"));
        erros.LancarSeHouver();

        var visiveis = await consulta.ListarAsync(escopo.FuncionarioId, FusoBrasil.Hoje(relogio.AgoraUtc), paginacao!.Busca, obrigatorio, ct);
        var cartoes = visiveis.Select(MapeadorDoMeuTreinamento.Cartao).ToList();
        var contagens = AbasDosMeusTreinamentos.Contar(cartoes.Select(c => c.Situacao));
        var daAba = cartoes
            .Where(c => AbasDosMeusTreinamentos.Pertence(abaLida ?? AbaDosMeusTreinamentos.Todos, c.Situacao))
            .OrderByDescending(c => c.Obrigatorio).ThenBy(c => c.ComecaEm is null).ThenBy(c => c.ComecaEm).ThenBy(c => c.Titulo, StringComparer.OrdinalIgnoreCase).ThenBy(c => c.Id)
            .ToList();
        var itens = daAba.Skip(paginacao.Pular).Take(paginacao.TamanhoPagina).ToList();
        return new TreinamentoMeuLista(itens, paginacao.Pagina, paginacao.TamanhoPagina, daAba.Count, (int)(((long)daAba.Count + paginacao.TamanhoPagina - 1) / paginacao.TamanhoPagina), contagens);
    }
}

/// <summary><c>GET /api/eu/treinamentos/{treinamentoId}</c>: o cartão e a lista de módulos (sem conteúdo) com o andamento da pessoa. 404 para rascunho, não liberado, sem linha e fora do público, e encerrado em que ela não começou nem concluiu; o opcional do público dela que ainda não escolheu vem como <c>DISPONIVEL</c>.</summary>
public sealed class ObterMeuTreinamentoCasoDeUso(IConsultaDeMeusTreinamentos consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<TreinamentoMeuDetalhe> ExecutarAsync(int treinamentoId, CancellationToken ct) =>
        MapeadorDoMeuTreinamento.Detalhe((await consulta.ObterAsync(escopo.FuncionarioId, treinamentoId, FusoBrasil.Hoje(relogio.AgoraUtc), ct)).OuNaoEncontrado());
}
