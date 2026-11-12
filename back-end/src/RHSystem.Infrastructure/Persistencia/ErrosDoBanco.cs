using MySqlConnector;

namespace RHSystem.Infrastructure.Persistencia;

/// <summary>
/// Reconhece, na exceção ou em qualquer <see cref="Exception.InnerException"/> dela, os dois erros do MySQL que os cadastros traduzem em resposta do contrato: chave repetida (<c>1062</c>: nome já cadastrado, 409) e
/// chave estrangeira em uso (<c>1451</c>: não se apaga o que tem dependente, 409). A mensagem do MySQL traz o VALOR da chave e nunca é mostrada nem registrada por quem chama.
/// </summary>
public static class ErrosDoBanco
{
    private const int ChaveRepetida = 1062;
    private const int ChaveEstrangeiraEmUso = 1451;
    private const int ChaveEstrangeiraSemPai = 1452;

    public static bool EhChaveRepetida(Exception erro) => TemErro(erro, ChaveRepetida);

    public static bool EhChaveEstrangeiraEmUso(Exception erro) => TemErro(erro, ChaveEstrangeiraEmUso);

    /// <summary>1452: a linha filha aponta para um pai que não existe (por exemplo, a batida de outro dia).</summary>
    public static bool EhChaveEstrangeiraSemPai(Exception erro) => TemErro(erro, ChaveEstrangeiraSemPai);

    /// <summary>O primeiro erro do MySQL na cadeia de exceções (o EF embrulha o do provedor em <c>DbUpdateException</c>), ou <c>null</c>.</summary>
    public static MySqlException? ErroDoMySql(Exception? erro)
    {
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            if (atual is MySqlException mysql)
            {
                return mysql;
            }
        }

        return null;
    }

    /// <summary>
    /// O NOME da chave única que o 1062 violou (sem a tabela: <c>UQ_PONTOS_MARCACOES_PONTO_DATAHORA</c>), ou <c>null</c>. A mensagem do MySQL é <c>Duplicate entry '&lt;valor&gt;' for key '&lt;tabela&gt;.&lt;chave&gt;'</c>: só o fim dela é lido, porque o VALOR
    /// (que pode ter dado pessoal e apóstrofo) vem antes e nunca sai daqui.
    /// </summary>
    public static string? NomeDaChaveRepetida(Exception erro)
    {
        var mensagem = ErroDoMySql(erro) is { Number: ChaveRepetida } mysql ? mysql.Message : null;
        const string marcador = "for key '";
        var inicio = mensagem?.LastIndexOf(marcador, StringComparison.Ordinal) ?? -1;
        if (mensagem is null || inicio < 0)
        {
            return null;
        }

        var chave = mensagem[(inicio + marcador.Length)..].TrimEnd('\'');
        return chave[(chave.LastIndexOf('.') + 1)..];
    }

    /// <summary>O nome da chave estrangeira que o 1451 ou o 1452 violou (<c>CONSTRAINT `FK_...`</c> na mensagem do MySQL), ou <c>null</c>.</summary>
    public static string? NomeDaChaveEstrangeira(Exception erro)
    {
        var mensagem = ErroDoMySql(erro)?.Message;
        const string marcador = "CONSTRAINT `";
        var inicio = mensagem?.IndexOf(marcador, StringComparison.Ordinal) ?? -1;
        if (mensagem is null || inicio < 0)
        {
            return null;
        }

        var resto = mensagem[(inicio + marcador.Length)..];
        var fim = resto.IndexOf('`', StringComparison.Ordinal);
        return fim < 0 ? null : resto[..fim];
    }

    private static bool TemErro(Exception? erro, int numero)
    {
        for (var atual = erro; atual is not null; atual = atual.InnerException)
        {
            if (atual is MySqlException mysql && mysql.Number == numero)
            {
                return true;
            }
        }

        return false;
    }
}
