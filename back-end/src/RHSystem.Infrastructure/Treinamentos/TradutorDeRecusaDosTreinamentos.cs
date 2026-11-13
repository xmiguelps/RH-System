using RHSystem.Application.Comum;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Treinamentos;

/// <summary>
/// Traduz o que o banco recusou numa gravação dos treinamentos para a resposta do contrato (<see cref="ProblemaDeApiException"/>), ou <c>null</c> quando não é nada que os treinamentos tratem (o chamador deixa subir e o tratador global decide). A regra de trigger (<c>SQLSTATE 45000</c>) se distingue pela
/// mensagem original e a chave única pelo NOME (a mensagem do MySQL traz o VALOR da chave e nunca é copiada). Devolvem <c>null</c> de propósito as chaves que não são erro para quem chama: a linha da pessoa repetida ("já estava"), o módulo concluído duas vezes ("já concluído"), o código do certificado
/// repetido (gera outro), o público repetido e o certificado da pessoa repetido (a aplicação nunca insere certificado).
/// </summary>
public static class TradutorDeRecusaDosTreinamentos
{
    public static ProblemaDeApiException? Traduzir(Exception erro)
    {
        var mysql = ErrosDoBanco.ErroDoMySql(erro);
        if (mysql is null)
        {
            return null;
        }

        if (mysql.SqlState == "45000")
        {
            return string.Equals(mysql.Message, RegrasDoBancoDosTreinamentos.FuncionarioInativo, StringComparison.Ordinal)
                ? MensagensDosTreinamentos.FuncionarioInativo()
                : MensagensDosTreinamentos.OperacaoRecusada(mysql.Message);
        }

        if (ErrosDoBanco.EhChaveRepetida(mysql) && string.Equals(ErrosDoBanco.NomeDaChaveRepetida(erro), RegrasDoBancoDosTreinamentos.ChaveDaOrdemDoModulo, StringComparison.OrdinalIgnoreCase))
        {
            return MensagensDosTreinamentos.ModulosMudaramEmOutraTela;
        }

        return null;
    }
}
