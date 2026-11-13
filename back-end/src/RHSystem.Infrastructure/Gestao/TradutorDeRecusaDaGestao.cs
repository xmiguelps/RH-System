using RHSystem.Application.Comum;
using RHSystem.Application.Gestao.Mensagens;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Gestao;

/// <summary>
/// Traduz o que o banco recusou numa gravação da gestão (férias, benefícios, pagamento) para a resposta do contrato (<see cref="ProblemaDeApiException"/>), ou <c>null</c> quando não é nada que a gestão trate (o chamador deixa subir e o tratador global decide). A regra de trigger
/// (<c>SQLSTATE 45000</c>) se distingue pela mensagem original e a chave única pelo NOME (a mensagem do MySQL traz o VALOR da chave e nunca é copiada). Devolve <c>null</c> de propósito a chave única da exceção de pagamento (o caso de uso a trata como "já existe, atualizar") e os <c>CHECK</c>: a validação
/// vem ANTES (um <c>CHECK</c> recusado é defeito, não erro de quem usa, e vira o 500 do tratador global).
/// </summary>
public static class TradutorDeRecusaDaGestao
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
            return mysql.Message switch
            {
                RegrasDoBancoDaGestao.FeriasDeInativo => MensagensDaGestao.FuncionarioInativoDeFerias(),
                RegrasDoBancoDaGestao.FeriasProprias => MensagensDaGestao.PedidoProprio(),
                RegrasDoBancoDaGestao.FeriasSemGestor => MensagensDaGestao.PedidoRhForaDeGestores(),
                RegrasDoBancoDaGestao.BeneficioDeInativo => MensagensDaGestao.FuncionarioInativoParaPedirBeneficio.ComoProblema(),
                _ => MensagensDaGestao.OperacaoRecusada(mysql.Message),
            };
        }

        if (ErrosDoBanco.EhChaveRepetida(mysql) && string.Equals(ErrosDoBanco.NomeDaChaveRepetida(erro), RegrasDoBancoDaGestao.ChaveDaAdesao, StringComparison.OrdinalIgnoreCase))
        {
            return MensagensDaGestao.BeneficioJaAtivoOuPendente.ComoProblema();
        }

        return null;
    }
}
