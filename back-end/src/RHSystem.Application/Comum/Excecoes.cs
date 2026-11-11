namespace RHSystem.Application.Comum;

// O caso de uso lança; a Api traduz em resposta (404, 409, 422, 400) com os textos do contrato. Nenhuma delas leva dado interno.

/// <summary>404: não existe, ou está fora do escopo da pessoa (a API não revela qual dos dois).</summary>
public sealed class NaoEncontradoException(string? detalhe = null) : Exception(detalhe ?? "O registro não foi encontrado.")
{
    /// <summary>O detalhe dado por quem lançou; <c>null</c> deixa valer o texto padrão do contrato.</summary>
    public string? Detalhe { get; } = detalhe;
}

/// <summary>409: conflito com o que já existe (por exemplo CPF repetido ou registro alterado por outra pessoa).</summary>
public sealed class ConflitoException(string detalhe, string? codigo = null) : Exception(detalhe)
{
    public string Detalhe { get; } = detalhe;

    /// <summary>Código estável da regra violada, quando o front precisa decidir algo por ela.</summary>
    public string? Codigo { get; } = codigo;
}

/// <summary>422: a operação foi recusada por uma regra de negócio. O <see cref="Codigo"/> é o do contrato.</summary>
public sealed class ErroDeNegocioException(string codigo, string detalhe) : Exception(detalhe)
{
    public string Codigo { get; } = codigo;

    public string Detalhe { get; } = detalhe;
}

/// <summary>400: algum campo está inválido ou faltando. O nome do campo é o do corpo da requisição, em camelCase.</summary>
public sealed class ErroDeValidacaoException : Exception
{
    public ErroDeValidacaoException(IReadOnlyDictionary<string, string[]> erros)
        : base("Há campos inválidos.")
    {
        Erros = erros;
    }

    public ErroDeValidacaoException(string campo, string mensagem)
        : this(new Dictionary<string, string[]> { [campo] = [mensagem] })
    {
    }

    /// <summary>O nome do campo e as mensagens dele.</summary>
    public IReadOnlyDictionary<string, string[]> Erros { get; }
}

/// <summary>Junta as mensagens de todos os campos inválidos e lança uma só <see cref="ErroDeValidacaoException"/>, para a pessoa corrigir tudo de uma vez.</summary>
public sealed class ColetorDeErros
{
    private readonly Dictionary<string, List<string>> _erros = [];

    public bool TemErros => _erros.Count > 0;

    public void Adicionar(string campo, string mensagem)
    {
        if (!_erros.TryGetValue(campo, out var mensagens))
        {
            _erros[campo] = mensagens = [];
        }

        mensagens.Add(mensagem);
    }

    public void LancarSeHouver()
    {
        if (TemErros)
        {
            throw new ErroDeValidacaoException(_erros.ToDictionary(e => e.Key, e => e.Value.ToArray()));
        }
    }
}

public static class ObjetoOuNaoEncontrado
{
    /// <summary>Devolve o objeto, ou lança <see cref="NaoEncontradoException"/> se for nulo.</summary>
    public static T OuNaoEncontrado<T>(this T? objeto)
        where T : class =>
        objeto ?? throw new NaoEncontradoException();
}
