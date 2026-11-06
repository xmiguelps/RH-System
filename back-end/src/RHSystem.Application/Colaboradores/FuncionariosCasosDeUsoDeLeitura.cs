using RHSystem.Application.Acesso;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>A lista de colaboradores (tela 01). Só o perfil RH. Todos os erros de parâmetro voltam juntos num 400 só.</summary>
public sealed class ListarFuncionariosCasoDeUso(IConsultaDeFuncionarios consulta)
{
    /// <exception cref="ErroDeValidacaoException">Algum parâmetro inválido; todos os erros vêm juntos.</exception>
    public async Task<FuncionarioListaRespostaRh> ExecutarAsync(
        int pagina,
        int tamanhoPagina,
        string? busca,
        string? situacao,
        int? setorId,
        string? tipoContrato,
        string? ordenarPor,
        string? direcao,
        CancellationToken ct)
    {
        var erros = new Dictionary<string, string[]>();
        ParametrosDeLista? paginacao = null;
        FuncionarioSituacao? aba = null;
        TipoDeContrato? contrato = null;
        FuncionarioOrdenarPor? coluna = null;

        DirecaoDeOrdenacao? sentido = null;
        Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        Tentar(erros, () => sentido = EnumsDeConsulta.Ler<DirecaoDeOrdenacao>(direcao, "direcao"));
        Tentar(erros, () => aba = EnumsDeConsulta.Ler<FuncionarioSituacao>(situacao, "situacao"));
        Tentar(erros, () => contrato = EnumsDeConsulta.Ler<TipoDeContrato>(tipoContrato, "tipoContrato"));
        Tentar(erros, () => coluna = EnumsDeConsulta.Ler<FuncionarioOrdenarPor>(ordenarPor, "ordenarPor"));
        if (erros.Count > 0)
        {
            throw new ErroDeValidacaoException(erros);
        }

        var parametros = paginacao! with { OrdenarPor = coluna is { } c ? NomesDoContrato.Enum(c) : null, Direcao = sentido };
        return await consulta.ListarAsync(parametros, aba ?? FuncionarioSituacao.Ativo, setorId, contrato, ct);
    }

    private static void Tentar(Dictionary<string, string[]> erros, Action acao)
    {
        try
        {
            acao();
        }
        catch (ErroDeValidacaoException erro)
        {
            foreach (var (campo, mensagens) in erro.Erros)
            {
                erros[campo] = mensagens;
            }
        }
    }
}

/// <summary>A ficha completa de um colaborador (qualquer um, ativo ou não). 404 se não existe.</summary>
public sealed class ObterFichaDoFuncionarioCasoDeUso(IConsultaDeFuncionarios consulta)
{
    public async Task<FuncionarioFichaRh> ExecutarAsync(int funcionarioId, CancellationToken ct) =>
        await consulta.ObterFichaRhAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
}

/// <summary>
/// Os indicadores da ficha: cada campo é o PRIMEIRO valor das fontes registradas para ele (banco de horas: fatia 3; férias e advertências: 7) e o dia do próximo pagamento vem de
/// <see cref="IFonteDoProximoPagamento"/>. Sem fonte, <c>null</c>. 404 se o colaborador não existe.
/// </summary>
public sealed class ObterIndicadoresDoFuncionarioCasoDeUso(
    IConsultaDeUsuariosDoSistema existencia,
    IEnumerable<IFonteDeIndicadorDaFicha> fontes,
    IEnumerable<IFonteDoProximoPagamento> proximoPagamento)
{
    public async Task<FuncionarioIndicadoresRh> ExecutarAsync(int funcionarioId, CancellationToken ct)
    {
        // "Existe" é ter linha de colaborador, ativo ou não (a mesma leitura de "Usuários do sistema").
        _ = await existencia.ObterAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
        var lista = fontes.ToList();

        DateOnly? dia = null;
        foreach (var fonte in proximoPagamento)
        {
            if (await fonte.ObterAsync(ct) is { } encontrado)
            {
                dia = encontrado;
                break;
            }
        }

        return new FuncionarioIndicadoresRh(
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.BancoDeHorasMinutos, funcionarioId, ct),
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.FeriasDisponiveisDias, funcionarioId, ct),
            await FontesDeIndicador.PrimeiroAsync(lista, IndicadorDaFicha.AdvertenciasQuantidade, funcionarioId, ct),
            dia);
    }
}
