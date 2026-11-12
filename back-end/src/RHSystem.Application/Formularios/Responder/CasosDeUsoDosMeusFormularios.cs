using System.Globalization;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Formularios.Construtor;
using RHSystem.Application.Formularios.Destinatarios;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Domain.Formularios;

namespace RHSystem.Application.Formularios.Responder;

/// <summary>
/// <c>GET /api/eu/formularios</c>: os formulários da pessoa logada (qualquer perfil), em cartões. Sem <c>situacao</c> traz PARA_RESPONDER e DISPONIVEL (os já respondidos ficam de fora; é a aba "Todos"); <c>contagens</c> respeita <c>busca</c> (no título, sem diferenciar caixa nem acento) e <c>tipo</c> e IGNORA a
/// situação; <c>contagens.paraResponder</c> é a bolinha do menu. Erros de campo (<c>tipo</c>, <c>situacao</c>, página, busca) vêm juntos num 400. Só olha a pessoa logada: nunca um id da requisição.
/// </summary>
public sealed class ListarMeusFormulariosCasoDeUso(IConsultaDeMeusFormularios consulta, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<FormularioMeusLista> ExecutarAsync(int pagina, int tamanhoPagina, string? busca, string? tipo, string? situacao, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        TipoDeFormulario? tipoLido = null;
        SituacaoMinhaDoFormulario? situacaoLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => tipoLido = EnumsDeConsulta.Ler<TipoDeFormulario>(tipo, "tipo"));
        LeituraDeFiltros.Tentar(erros, () => situacaoLida = EnumsDeConsulta.Ler<SituacaoMinhaDoFormulario>(situacao, "situacao"));
        erros.LancarSeHouver();

        var agora = relogio.AgoraUtc;
        var cartoes = await consulta.CartoesAsync(escopo.FuncionarioId, FusoBrasil.Hoje(agora), agora, soParaResponder: false, ct);
        var base2 = cartoes.Select(c => RegrasDosMeusFormularios.Montar(c, agora)).OfType<FormularioMeuItem>()
            .Where(i => tipoLido is null || i.Tipo == tipoLido)
            .Where(i => paginacao!.Busca is null || CultureInfo.InvariantCulture.CompareInfo.IndexOf(i.Titulo, paginacao.Busca, CompareOptions.IgnoreCase | CompareOptions.IgnoreNonSpace) >= 0)
            .ToList();
        var daAba = base2.Where(i => situacaoLida is null ? i.Situacao != SituacaoMinhaDoFormulario.Respondido : i.Situacao == situacaoLida).ToList();
        var pagina2 = RegrasDosMeusFormularios.Ordenar(daAba).Skip(paginacao!.Pular).Take(paginacao.TamanhoPagina).ToList();
        return new FormularioMeusLista(pagina2, paginacao.Pagina, paginacao.TamanhoPagina, daAba.Count, (int)(((long)daAba.Count + paginacao.TamanhoPagina - 1) / paginacao.TamanhoPagina), RegrasDosMeusFormularios.Contar(base2));
    }
}

/// <summary>
/// <c>GET /api/eu/formularios/{formularioId}</c>: abre o formulário para a pessoa responder (as perguntas em ordem e, nas avaliações, quem avaliar com o "já avaliou" de cada uma). 404 se ele não existe, é rascunho, é por link ou a pessoa NÃO deve respondê-lo (e não o respondeu): nunca diz qual dos casos.
/// Agendado: 422 <c>FORMULARIO_AINDA_NAO_ABERTO</c>; encerrado (inclusive o prazo vencido com a tarefa de encerrar parada): 422 <c>FORMULARIO_ENCERRADO</c>. A Sugestão abre sempre (enquanto publicada). Nada do que é do RH sai: nem o público, nem o código do link, nem se há respostas.
/// </summary>
public sealed class ObterFormularioParaResponderCasoDeUso(IConsultaDeFormularios consulta, IConsultaDeMeusFormularios meus, IEscopoDoUsuario escopo, IRelogio relogio)
{
    public async Task<FormularioParaResponder> ExecutarAsync(int formularioId, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var (lido, posicao) = await AcessoAoFormularioParaResponder.ConferirAsync(consulta, meus, formularioId, escopo.FuncionarioId, agora, ct);
        var avaliados = lido.Tipo == TipoDeFormulario.Avaliacao ? await AvaliadosAsync(posicao, ct) : [];
        var minutos = MinutosEstimados.De(lido.Perguntas.Select(p => p.Tipo), lido.Tipo == TipoDeFormulario.Avaliacao ? Math.Max(avaliados.Count, 1) : 1);
        return new FormularioParaResponder(lido.Id, lido.Titulo, lido.Descricao, lido.Tipo, lido.Sentido, lido.Anonimo, lido.Tipo == TipoDeFormulario.Outro ? null : lido.Prazo, minutos, [.. lido.Perguntas.Select(MapeadorDoFormulario.Pergunta)], avaliados);
    }

    /// <summary>Quem a pessoa deve avaliar (e já avaliou), primeiro os que faltam, depois os feitos, cada grupo por nome.</summary>
    private async Task<IReadOnlyList<AvaliadoParaResponder>> AvaliadosAsync(PosicaoDaPessoaNoFormulario posicao, CancellationToken ct)
    {
        var pares = posicao.MeusPares.Union(posicao.Feitos).Where(p => p.AvaliadoId is not null).ToList();
        var feitos = posicao.Feitos.Select(p => p.AvaliadoId).ToHashSet();
        var resumos = await meus.ResumosAsync([.. pares.Select(p => p.AvaliadoId!.Value).Distinct()], ct);
        return [.. resumos.Select(r => new AvaliadoParaResponder(r, feitos.Contains(r.Id))).OrderBy(a => a.Respondido).ThenBy(a => a.Funcionario.Nome, StringComparer.CurrentCultureIgnoreCase).ThenBy(a => a.Funcionario.Id)];
    }
}
