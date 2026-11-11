namespace RHSystem.Api.Cadastros;

/// <summary>
/// Os parâmetros das listas dos cadastros de apoio e SÓ eles: o contrato declara <c>pagina</c>, <c>tamanhoPagina</c> e <c>busca</c> (a ordem é fixa: nome nos cargos e setores, id nos benefícios), então
/// o <c>ParametrosDeLista</c> inteiro em <c>[AsParameters]</c> acrescentaria <c>ordenarPor</c> e <c>direcao</c> ao documento da API e o contrato acusaria a divergência.
/// </summary>
public sealed record FiltrosDaListaDeCadastros(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null);
