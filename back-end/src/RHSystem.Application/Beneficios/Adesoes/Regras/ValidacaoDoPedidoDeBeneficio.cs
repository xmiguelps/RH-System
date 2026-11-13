using RHSystem.Application.Comum;

namespace RHSystem.Application.Beneficios.Adesoes.Regras;

/// <summary>
/// O que vale para o corpo dos pedidos de benefício e para o motivo da recusa. Os tamanhos são os do contrato (e do banco): nome 150, parentesco 30, origem e destino 200, linhas e meios 255, motivo 500, até 10 dependentes por adesão. Os textos contam sem os espaços das pontas
/// (quem grava usa <see cref="DadosDoDependente.Aparado"/>). A data de nascimento não pode ser futura nem antes de 1900; o início desejado não pode ser anterior a hoje (hoje vale) nem depois de 2100.
/// </summary>
public static class ValidacaoDoPedidoDeBeneficio
{
    public const int MaximoDeDependentes = 10;

    public const int MaximoDoNome = 150;

    public const int MaximoDoParentesco = 30;

    public const int MaximoDoLocal = 200;

    public const int MaximoDasLinhas = 255;

    public const int MaximoDoMotivo = 500;

    public const int AnoMinimoDeNascimento = 1900;

    public const int AnoMaximoDoInicio = 2100;

    /// <summary>O corpo de <c>pedirAdesaoABeneficio</c>; os dependentes e o trajeto entram com o nome do campo no corpo (<c>dependentes[0].nome</c>, <c>trajeto.origem</c>).</summary>
    public static void Adesao(DateOnly dataInicioDesejada, IReadOnlyList<DadosDoDependente>? dependentes, DadosDoTrajeto? trajeto, DateOnly hoje)
    {
        var erros = new ColetorDeErros();
        if (dataInicioDesejada < hoje)
        {
            erros.Adicionar("dataInicioDesejada", "A data de início não pode ser anterior a hoje.");
        }
        else if (dataInicioDesejada.Year > AnoMaximoDoInicio)
        {
            erros.Adicionar("dataInicioDesejada", "Informe uma data de início até 2100.");
        }

        if (dependentes is not null)
        {
            if (dependentes.Count > MaximoDeDependentes)
            {
                erros.Adicionar("dependentes", "Informe no máximo 10 dependentes.");
            }

            for (var i = 0; i < dependentes.Count; i++)
            {
                ValidarDependente(erros, dependentes[i], $"dependentes[{i}].", hoje);
            }
        }

        if (trajeto is not null)
        {
            ValidarTrajeto(erros, trajeto, "trajeto.");
        }

        erros.LancarSeHouver();
    }

    /// <summary>O corpo de <c>pedirInclusaoDeDependente</c>.</summary>
    public static void Dependente(DadosDoDependente dependente, DateOnly hoje)
    {
        var erros = new ColetorDeErros();
        ValidarDependente(erros, dependente, string.Empty, hoje);
        erros.LancarSeHouver();
    }

    /// <summary>O corpo de <c>pedirAtualizacaoDeTrajeto</c>.</summary>
    public static void Trajeto(DadosDoTrajeto trajeto)
    {
        var erros = new ColetorDeErros();
        ValidarTrajeto(erros, trajeto, string.Empty);
        erros.LancarSeHouver();
    }

    /// <summary>O motivo da recusa como é gravado: sem espaços nas pontas, de 1 a 500 caracteres.</summary>
    public static string Motivo(string? motivo)
    {
        var limpo = motivo?.Trim() ?? string.Empty;
        if (limpo.Length == 0)
        {
            throw new ErroDeValidacaoException("motivo", "Informe o motivo da recusa.");
        }

        if (limpo.Length > MaximoDoMotivo)
        {
            throw new ErroDeValidacaoException("motivo", "O motivo pode ter até 500 caracteres.");
        }

        return limpo;
    }

    private static void ValidarDependente(ColetorDeErros erros, DadosDoDependente dependente, string prefixo, DateOnly hoje)
    {
        Texto(erros, prefixo + "nome", dependente.Nome, MaximoDoNome, "Informe o nome do dependente.", "O nome pode ter até 150 caracteres.");
        Texto(erros, prefixo + "parentesco", dependente.Parentesco, MaximoDoParentesco, "Informe o parentesco do dependente.", "O parentesco pode ter até 30 caracteres.");
        if (dependente.DataNascimento > hoje)
        {
            erros.Adicionar(prefixo + "dataNascimento", "A data de nascimento não pode ser futura.");
        }
        else if (dependente.DataNascimento.Year < AnoMinimoDeNascimento)
        {
            erros.Adicionar(prefixo + "dataNascimento", "Informe uma data de nascimento a partir de 1900.");
        }
    }

    private static void ValidarTrajeto(ColetorDeErros erros, DadosDoTrajeto trajeto, string prefixo)
    {
        Texto(erros, prefixo + "origem", trajeto.Origem, MaximoDoLocal, "Informe a origem do trajeto.", "A origem pode ter até 200 caracteres.");
        Texto(erros, prefixo + "destino", trajeto.Destino, MaximoDoLocal, "Informe o destino do trajeto.", "O destino pode ter até 200 caracteres.");
        Texto(erros, prefixo + "linhasMeios", trajeto.LinhasMeios, MaximoDasLinhas, "Informe as linhas ou os meios de transporte.", "As linhas ou os meios podem ter até 255 caracteres.");
    }

    private static void Texto(ColetorDeErros erros, string campo, string? valor, int maximo, string mensagemDeVazio, string mensagemDeExcesso)
    {
        var tamanho = valor?.Trim().Length ?? 0;
        if (tamanho == 0)
        {
            erros.Adicionar(campo, mensagemDeVazio);
        }
        else if (tamanho > maximo)
        {
            erros.Adicionar(campo, mensagemDeExcesso);
        }
    }
}
