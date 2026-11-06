using RHSystem.Application.Comum.Consultas;

namespace RHSystem.Application.Cadastros;

// Os corpos dos cadastros de apoio. Os nomes são os dos schemas de contrato/fontes/20-fatia-1-colaboradores.yaml e o JSON é camelCase. O gerador de OpenAPI marca "required" todo parâmetro de
// construtor sem padrão (mesmo anulável): os campos que o contrato declara obrigatórios (todos os de cargo e de benefício, a descrição inclusive, que só pode ser null) ficam sem padrão.

/// <summary>Um cargo na lista. <c>QuantidadePessoas</c> conta as pessoas ativas com o cargo.</summary>
public sealed record CargoItem(int Id, string Nome, string? Descricao, decimal CargaSemanalHoras, int QuantidadePessoas);

/// <summary>O corpo de criar e de editar um cargo: os três campos são obrigatórios (a descrição pode ser <c>null</c>).</summary>
public sealed record CargoComando(string Nome, string? Descricao, decimal CargaSemanalHoras);

/// <summary>A página de cargos.</summary>
public sealed record CargoListaResposta(IReadOnlyList<CargoItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas)
    : RespostaPaginada<CargoItem>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);

/// <summary>Um benefício do catálogo na lista. <c>QuantidadePessoas</c> conta as linhas de <c>BeneficioDoFuncionario</c> com <c>Ativo = true</c>.</summary>
public sealed record BeneficioCatalogoItem(int Id, string Nome, string? Descricao, bool Opcional, bool AceitaDependentes, bool AceitaTrajeto, int QuantidadePessoas);

/// <summary>O corpo de criar e de editar um benefício: os cinco campos são obrigatórios (a descrição pode ser <c>null</c>).</summary>
public sealed record BeneficioCatalogoComando(string Nome, string? Descricao, bool Opcional, bool AceitaDependentes, bool AceitaTrajeto);

/// <summary>A página de benefícios do catálogo.</summary>
public sealed record BeneficioCatalogoListaResposta(IReadOnlyList<BeneficioCatalogoItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas)
    : RespostaPaginada<BeneficioCatalogoItem>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);
