using RHSystem.Application.Beneficios.Adesoes.Regras;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Painel;

/// <summary>Uma pendência como a fonte a leu: o tipo, o que o <c>referenciaId</c> aponta, a pessoa, desde quando espera e a frase pronta.</summary>
public sealed record PendenciaLida(PainelPendenciaTipo Tipo, int ReferenciaId, FuncionarioResumo Funcionario, DateTime DesdeEmUtc, string Descricao);

/// <summary>As frases das pendências: uma frase simples por tipo, sobre o item (<c>descricao</c>) e sobre o item mais antigo (<c>resumo</c> do cartão).</summary>
public static class TextosDasPendencias
{
    private static string Dia(DateOnly dia) => dia.ToString("dd/MM", System.Globalization.CultureInfo.InvariantCulture);

    public static string DoAjuste(DateOnly dia) => $"Pediu ajuste do ponto de {Dia(dia)}";

    public static string DasFerias(DateOnly inicio, DateOnly fim) => $"Pediu {fim.DayNumber - inicio.DayNumber + 1} dias, de {Dia(inicio)} a {Dia(fim)}";

    public static string DoAtestado(DateOnly inicio, DateOnly fim) => inicio == fim ? $"Atestado de {Dia(inicio)}" : $"Atestado de {Dia(inicio)} a {Dia(fim)}";

    public static string DaAdmissao(DateOnly admissao) => $"Começa em {Dia(admissao)} e ainda não enviou o formulário de admissão";

    /// <summary>O mesmo título de "Seus pedidos" da pessoa (Adesão ao..., Inclusão de dependente no..., Atualização do trajeto do...).</summary>
    public static string DoBeneficio(TipoDoPedidoDeBeneficio tipo, string nomeDoBeneficio) => TituloDoPedido.De(tipo, nomeDoBeneficio);

    /// <summary>
    /// A frase do cartão, sobre o item mais antigo: "O mais antigo é de Bruno Lima: Pediu ajuste do ponto de 02/10"; na admissão a frase já é sobre a pessoa ("Rafael Torres começa em 13/10 e ainda não enviou o formulário de admissão").
    /// </summary>
    public static string Resumo(PainelPendenciaTipo tipo, string nome, string descricao) =>
        tipo == PainelPendenciaTipo.AdmissaoComDocumentosPendentes
            ? $"{nome} {char.ToLowerInvariant(descricao[0])}{descricao[1..]}"
            : $"O mais antigo é de {nome}: {char.ToLowerInvariant(descricao[0])}{descricao[1..]}";
}

/// <summary>
/// A fila das pendências do RH: as cinco origens numa lista só, do MAIS ANTIGO para o mais novo (<c>desdeEmUtc</c>; empate: a ordem dos tipos e o id da referência). Os três lugares que contam pendências (o <c>quantidade</c> do painel, os itens da lista e o contador do menu do ponto) saem desta mesma conta.
/// </summary>
public static class FilaDePendenciasDoRh
{
    public static IReadOnlyList<PendenciaLida> Ordenar(IEnumerable<PendenciaLida> pendencias) =>
        [.. pendencias.OrderBy(p => p.DesdeEmUtc).ThenBy(p => p.Tipo).ThenBy(p => p.ReferenciaId)];

    /// <summary>Um cartão por tipo que tem pendência (quantidade maior que zero), na ordem dos tipos; cada um com o item mais antigo.</summary>
    public static IReadOnlyList<PainelPendenciaResumoDto> Resumos(IEnumerable<PendenciaLida> pendencias) =>
        [.. Ordenar(pendencias)
            .GroupBy(p => p.Tipo)
            .OrderBy(g => g.Key)
            .Select(g =>
            {
                var maisAntiga = g.First();
                return new PainelPendenciaResumoDto(g.Key, g.Count(), TextosDasPendencias.Resumo(g.Key, maisAntiga.Funcionario.Nome, maisAntiga.Descricao), maisAntiga.Funcionario.Id, maisAntiga.ReferenciaId);
            })];

    public static RespostaPaginada<PainelPendenciaItemDto> Pagina(IEnumerable<PendenciaLida> pendencias, PainelPendenciaTipo? tipo, ParametrosDeLista parametros)
    {
        var fila = Ordenar(pendencias.Where(p => tipo is null || p.Tipo == tipo)).ToList();
        var itens = fila.Skip(parametros.Pular).Take(parametros.TamanhoPagina).Select(p => new PainelPendenciaItemDto(p.Tipo, p.ReferenciaId, p.Funcionario, p.Descricao, p.DesdeEmUtc)).ToList();
        return RespostaPaginada<PainelPendenciaItemDto>.De(itens, fila.Count, parametros);
    }
}
