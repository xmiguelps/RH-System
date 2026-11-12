using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Domain.Ponto;

namespace RHSystem.Application.Ponto.Feriados;

internal static class MapeadorDoFeriado
{
    public static PontoFeriado Montar(Feriado f) => new(f.Id, f.Nome, f.Data, f.Tipo, f.Uf, f.Municipio, f.RepeteTodoAno, f.Ativo, f.Tipo == TipoDeFeriado.Municipal);

    /// <summary>O nome de 1 a 100 caracteres, aparado (o erro vai em <c>nome</c>).</summary>
    public static string LerNome(ColetorDeErros erros, string? nome)
    {
        var aparado = (nome ?? string.Empty).Trim();
        if (aparado.Length is < 1 or > Limites.TamanhoMaximoDoNome)
        {
            erros.Adicionar("nome", "O nome do feriado tem de 1 a 100 caracteres.");
        }

        return aparado;
    }

    public static class Limites
    {
        public const int TamanhoMaximoDoNome = 100;
    }
}

/// <summary>
/// <c>GET /api/rh/ponto/feriados</c>: os feriados do ano (ou do mês) que valem para a empresa, por data, sem paginação. Sem a empresa configurada a lista <b>não dá erro</b> (a tela abre numa instalação nova): vêm só os nacionais e os da empresa. Só os ativos, a menos
/// que <c>incluirInativos</c>.
/// </summary>
public sealed class ListarFeriadosCasoDeUso(IConfiguracaoDaEmpresa empresa, IFeriadosRepositorio feriados)
{
    public const int PrimeiroAno = 2000;
    public const int UltimoAno = 2100;

    public async Task<PontoFeriadoLista> ExecutarAsync(int? ano, int? mes, bool incluirInativos, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        if (ano is null or < PrimeiroAno or > UltimoAno)
        {
            erros.Adicionar("ano", $"Informe o ano, de {PrimeiroAno} a {UltimoAno}.");
        }

        if (mes is < 1 or > 12)
        {
            erros.Adicionar("mes", "O mês vai de 1 a 12.");
        }

        erros.LancarSeHouver();
        var lidos = await feriados.ListarAsync(new FiltroDeFeriados(ano!.Value, mes, incluirInativos), await empresa.ObterAsync(ct), ct);
        return new PontoFeriadoLista([.. lidos.Select(MapeadorDoFeriado.Montar)]);
    }
}

/// <summary>
/// <c>POST /api/rh/ponto/feriados</c>: cadastra um feriado MUNICIPAL com a UF e o município da empresa. Ordem: 400 do corpo, 422 <c>EMPRESA_NAO_CONFIGURADA</c> (o texto do ponto), 409 <c>FERIADO_DUPLICADO</c> (a chave única do banco, que não diferencia caixa
/// nem acento). O evento de auditoria é do interceptor.
/// </summary>
public sealed class CriarFeriadoCasoDeUso(IConfiguracaoDaEmpresa empresa, IFeriadosRepositorio feriados)
{
    public async Task<PontoFeriado> ExecutarAsync(PontoFeriadoCriar corpo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var nome = MapeadorDoFeriado.LerNome(erros, corpo.Nome);
        erros.LancarSeHouver();

        var configuracao = await empresa.ObterAsync(ct) ?? throw MensagensDoPonto.EmpresaNaoConfigurada.ComoProblema();
        var feriado = Feriado.Criar(nome, corpo.Data, TipoDeFeriado.Municipal, configuracao.Uf, configuracao.Municipio, corpo.RepeteTodoAno);
        await feriados.AdicionarAsync(feriado, ct);
        return MapeadorDoFeriado.Montar(feriado);
    }
}

/// <summary>
/// <c>PUT /api/rh/ponto/feriados/{feriadoId}</c>: edita um feriado MUNICIPAL (nome, data, repetição e <c>ativo</c>: desativar não apaga). Ordem: 404, 400 do corpo, 422 <c>FERIADO_NAO_EDITAVEL</c> (nacional, estadual e da empresa), 409 <c>FERIADO_DUPLICADO</c>. Editar
/// não pede a empresa (a UF e o município do feriado não mudam). Feriado em data já consolidada não recalcula o banco de horas (o contrato não manda).
/// </summary>
public sealed class EditarFeriadoCasoDeUso(IFeriadosRepositorio feriados)
{
    public async Task<PontoFeriado> ExecutarAsync(int feriadoId, PontoFeriadoEditar corpo, CancellationToken ct)
    {
        var feriado = (await feriados.ObterParaEditarAsync(feriadoId, ct)).OuNaoEncontrado();
        var erros = new ColetorDeErros();
        var nome = MapeadorDoFeriado.LerNome(erros, corpo.Nome);
        erros.LancarSeHouver();

        if (feriado.Tipo != TipoDeFeriado.Municipal)
        {
            throw MensagensDoPonto.FeriadoNaoEditavel.ComoProblema();
        }

        feriado.Editar(nome, corpo.Data, corpo.RepeteTodoAno, corpo.Ativo);
        await feriados.SalvarEdicaoAsync(ct);
        return MapeadorDoFeriado.Montar(feriado);
    }
}
