using System.Text.Json.Serialization;
using RHSystem.Application.Colaboradores;
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

/// <summary>
/// Um setor na lista. <c>Gestor</c> é o gestor (mesmo inativo) ou <c>null</c>; <c>QuantidadePessoas</c> conta as pessoas ativas com a admissão já começada, inclusive o gestor se ele for do setor.
/// <c>Descricao</c> e <c>Gestor</c> são obrigatórios e aceitam <c>null</c> no contrato, então não têm padrão.
/// </summary>
public sealed record SetorItem(int Id, string Nome, string? Descricao, GestorReferencia? Gestor, int QuantidadePessoas);

/// <summary>
/// O corpo de criar e de editar um setor: os três campos são obrigatórios; <c>descricao</c> e <c>gestorFuncionarioId</c> podem ser <c>null</c> (sem gestor). Como a edição SUBSTITUI, o campo que falta valeria "apagar
/// a descrição" ou "tirar o gestor" (que passa os pedidos dele ao RH), então os dois levam <see cref="JsonRequiredAttribute"/>: sem eles o corpo é 400. O nome faltando já vira 400 em <c>nome</c> no caso de uso.
/// </summary>
public sealed record SetorComando(string Nome, [property: JsonRequired] string? Descricao, [property: JsonRequired] int? GestorFuncionarioId);

/// <summary>A página de setores.</summary>
public sealed record SetorListaResposta(IReadOnlyList<SetorItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas)
    : RespostaPaginada<SetorItem>(Itens, Pagina, TamanhoPagina, Total, TotalPaginas);
