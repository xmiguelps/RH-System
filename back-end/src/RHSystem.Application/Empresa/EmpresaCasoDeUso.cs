using System.Globalization;
using System.Text.RegularExpressions;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum;
using RHSystem.Domain.Empresa;

namespace RHSystem.Application.Empresa;

/// <summary>
/// A configuração da empresa: ler (qualquer perfil) e salvar (só o RH). Sem linha, ler é 404 <c>EMPRESA_NAO_CONFIGURADA</c>. Salvar cria a linha na primeira vez e a substitui nas outras; se duas primeiras chamadas
/// chegam juntas, a que perde a corrida leva a chave única do banco e refaz como atualização (o contrato não declara 409). Os horários chegam como texto e são conferidos aqui: o conversor de
/// <see cref="TimeOnly"/> aceitaria <c>08:00:30</c>, e <c>24:00</c> não existe.
/// </summary>
public sealed partial class EmpresaCasoDeUso(IEmpresaRepositorio empresa)
{
    private const string FormatoDoHorario = "HH:mm";

    public async Task<EmpresaConfiguracao> ObterAsync(CancellationToken ct)
    {
        var atual = await empresa.ObterAsync(ct) ?? throw new ProblemaDeApiException(
            404,
            "Não encontrado.",
            MensagensDoApoio.EmpresaNaoConfigurada.Detalhe,
            MensagensDoApoio.EmpresaNaoConfigurada.Codigo);
        return Dto(atual);
    }

    public async Task<EmpresaConfiguracao> SalvarAsync(EmpresaConfiguracao configuracao, CancellationToken ct)
    {
        var (entrada, saida) = Validar(configuracao);
        var nome = configuracao.NomeEmpresa.Trim();
        var municipio = configuracao.Municipio.Trim();

        var atual = await empresa.ObterAsync(ct);
        if (atual is null)
        {
            var nova = ConfiguracaoDaEmpresa.Criar(nome, configuracao.Uf, municipio, entrada, saida, configuracao.ToleranciaAtrasoMinutos, configuracao.DiaPagamento);
            if (await empresa.TentarAdicionarAsync(nova, ct))
            {
                return Dto(nova);
            }

            // Outra chamada criou a linha entre a leitura e o INSERT: esta vira a atualização.
            atual = await empresa.ObterAsync(ct) ?? throw new InvalidOperationException("A linha da empresa não existe, e o banco recusou criá-la por já existir.");
        }

        atual.Atualizar(nome, configuracao.Uf, municipio, entrada, saida, configuracao.ToleranciaAtrasoMinutos, configuracao.DiaPagamento);
        await empresa.SalvarAsync(ct);
        return Dto(atual);
    }

    [GeneratedRegex(@"^([01][0-9]|2[0-3]):[0-5][0-9]\z")]
    private static partial Regex PadraoDoHorario();

    private static (TimeOnly Entrada, TimeOnly Saida) Validar(EmpresaConfiguracao c)
    {
        var erros = new ColetorDeErros();
        var nome = c.NomeEmpresa?.Trim();
        if (string.IsNullOrEmpty(nome))
        {
            erros.Adicionar("nomeEmpresa", "O nome da empresa é obrigatório.");
        }
        else if (nome.Length > 150)
        {
            erros.Adicionar("nomeEmpresa", "O nome da empresa pode ter até 150 caracteres.");
        }

        var municipio = c.Municipio?.Trim();
        if (string.IsNullOrEmpty(municipio))
        {
            erros.Adicionar("municipio", "O município é obrigatório.");
        }
        else if (municipio.Length > 100)
        {
            erros.Adicionar("municipio", "O município pode ter até 100 caracteres.");
        }

        if (c.Uf is null || !ConfiguracaoDaEmpresa.UfsValidas.Contains(c.Uf))
        {
            erros.Adicionar("uf", "A UF é uma das 27 siglas, em maiúsculas (por exemplo SP).");
        }

        var entrada = Horario(erros, "horarioEntrada", c.HorarioEntrada);
        var saida = Horario(erros, "horarioSaida", c.HorarioSaida);
        if (c.ToleranciaAtrasoMinutos is < 0 or > 120)
        {
            erros.Adicionar("toleranciaAtrasoMinutos", "A tolerância de atraso vai de 0 a 120 minutos.");
        }

        if (c.DiaPagamento is < 1 or > 31)
        {
            erros.Adicionar("diaPagamento", "O dia de pagamento vai de 1 a 31.");
        }

        erros.LancarSeHouver();
        return (entrada, saida);
    }

    private static TimeOnly Horario(ColetorDeErros erros, string campo, string? texto)
    {
        if (texto is not null && PadraoDoHorario().IsMatch(texto))
        {
            return TimeOnly.ParseExact(texto, FormatoDoHorario, CultureInfo.InvariantCulture);
        }

        erros.Adicionar(campo, "O horário é no formato HH:mm, de 00:00 a 23:59.");
        return default;
    }

    private static EmpresaConfiguracao Dto(ConfiguracaoDaEmpresa c) =>
        new(
            c.NomeEmpresa,
            c.Uf,
            c.Municipio,
            c.HorarioEntrada.ToString(FormatoDoHorario, CultureInfo.InvariantCulture),
            c.HorarioSaida.ToString(FormatoDoHorario, CultureInfo.InvariantCulture),
            c.ToleranciaAtrasoMinutos,
            c.DiaPagamento);
}
