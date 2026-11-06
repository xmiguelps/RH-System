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

    public static bool EhChaveRepetida(Exception erro) => TemErro(erro, ChaveRepetida);

    public static bool EhChaveEstrangeiraEmUso(Exception erro) => TemErro(erro, ChaveEstrangeiraEmUso);

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
