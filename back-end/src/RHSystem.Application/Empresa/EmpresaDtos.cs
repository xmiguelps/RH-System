namespace RHSystem.Application.Empresa;

/// <summary>
/// A configuração da empresa no contrato (<c>EmpresaConfiguracao</c>): corpo do <c>PUT</c> e resposta do <c>GET</c> e do <c>PUT</c>. Os horários são texto <c>HH:mm</c>, sem fuso, e a UF é a sigla em maiúsculas:
/// o caso de uso confere os dois (o conversor de <see cref="TimeOnly"/> aceitaria <c>08:00:30</c>, e o texto livre aceitaria <c>sp</c>). Os sete campos são obrigatórios e ficam sem padrão.
/// </summary>
public sealed record EmpresaConfiguracao(
    string NomeEmpresa,
    string Uf,
    string Municipio,
    string HorarioEntrada,
    string HorarioSaida,
    int ToleranciaAtrasoMinutos,
    int DiaPagamento);
