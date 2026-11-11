using RHSystem.Application.Comum;
using RHSystem.Application.Ponto.Mensagens;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// Traduz o que o banco recusou numa gravação do ponto para a resposta do contrato (<see cref="ProblemaDeApiException"/>), ou <c>null</c> quando não é nada que o ponto conheça (o chamador deixa subir e o tratador global decide).
/// <b>A chave repetida se distingue pelo NOME dela</b> (o ponto tem várias com significados diferentes) e a regra de trigger (<c>SQLSTATE 45000</c>) pela mensagem original; a mensagem do MySQL traz o VALOR da chave e nunca é copiada.
/// </summary>
public static class TradutorDeRecusaDoPonto
{
    private const string ChaveDaBatida = "UQ_PONTOS_MARCACOES_PONTO_DATAHORA";
    private const string ChaveDoPedidoPendente = "UQ_PONTOS_MANUTENCOES_UM_PENDENTE";
    private const string ChaveDoItemDoPedido = "UQ_PONTOS_MANUTENCOES_ITENS_PEDIDO_DATAHORA";
    private const string ChaveDoFeriado = "UQ_FERIADOS_DATA_NOME";
    private const string ChaveEstrangeiraDaBatidaDoItem = "FK_PONTOS_MANUTENCOES_ITENS_MARCACOES";

    public static ProblemaDeApiException? Traduzir(Exception erro, ContextoDaRota contexto)
    {
        var mysql = ErrosDoBanco.ErroDoMySql(erro);
        if (mysql is null)
        {
            return null;
        }

        if (mysql.SqlState == "45000")
        {
            return DaRegraDoBanco(mysql.Message, contexto);
        }

        if (ErrosDoBanco.EhChaveRepetida(mysql))
        {
            return DaChaveRepetida(ErrosDoBanco.NomeDaChaveRepetida(mysql), contexto);
        }

        if (ErrosDoBanco.EhChaveEstrangeiraEmUso(mysql))
        {
            // A batida que algum pedido já apontou (mesmo recusado) nunca se apaga: a chave estrangeira dos itens a segura.
            return Igual(ErrosDoBanco.NomeDaChaveEstrangeira(mysql), ChaveEstrangeiraDaBatidaDoItem) ? MensagensDoPonto.MarcacaoEmUso.ComoProblema() : null;
        }

        if (ErrosDoBanco.EhChaveEstrangeiraSemPai(mysql))
        {
            // O item aponta para uma batida que não é do dia (apagada entre a conferência e a gravação): as batidas pedidas já não fecham o dia.
            return Igual(ErrosDoBanco.NomeDaChaveEstrangeira(mysql), ChaveEstrangeiraDaBatidaDoItem) ? MensagensDoPonto.SequenciaInvalidaNoPedido.ComoProblema() : null;
        }

        return null;
    }

    private static ProblemaDeApiException DaRegraDoBanco(string regra, ContextoDaRota contexto)
    {
        if (regra.Contains("Solicitacao analisada nao pode mudar de status", StringComparison.OrdinalIgnoreCase))
        {
            return MensagensDoPonto.PedidoJaDecidido.ComoProblema();
        }

        if (Igual(regra, RegrasDoBancoDoPonto.PedidoSemGestor))
        {
            return MensagensDoPonto.PedidoRhForaDeGestores.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDoPonto.PedidoProprio))
        {
            return MensagensDoPonto.PedidoProprio.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDoPonto.FuncionarioInativo) || Igual(regra, RegrasDoBancoDoPonto.PontoDeInativoAlterado))
        {
            var inativo = contexto switch
            {
                ContextoDaRota.RegistrarMarcacao => MensagensDoPonto.FuncionarioInativoNoRegistro,
                ContextoDaRota.CriarPedido => MensagensDoPonto.FuncionarioInativoNoPedido,
                ContextoDaRota.CorrigirDia => MensagensDoPonto.FuncionarioInativoNaCorrecao,
                _ => null,
            };
            if (inativo is not null)
            {
                return inativo.ComoProblema(regra);
            }
        }

        return MensagensDoPonto.OperacaoRecusada.ComoProblema(regra);
    }

    private static ProblemaDeApiException? DaChaveRepetida(string? chave, ContextoDaRota contexto)
    {
        if (Igual(chave, ChaveDaBatida))
        {
            return contexto switch
            {
                ContextoDaRota.RegistrarMarcacao => MensagensDoPonto.BatidaDuplicadaNoRegistro.ComoProblema(),
                ContextoDaRota.CorrigirDia => MensagensDoPonto.BatidaDuplicadaNaCorrecao.ComoProblema(),
                ContextoDaRota.DecidirPedido => MensagensDoPonto.BatidaDuplicadaNaAprovacao.ComoProblema(),
                _ => null,
            };
        }

        if (Igual(chave, ChaveDoPedidoPendente))
        {
            return MensagensDoPonto.PedidoJaEmAnalise.ComoProblema();
        }

        if (Igual(chave, ChaveDoItemDoPedido))
        {
            return MensagensDoPonto.SequenciaInvalidaNoPedido.ComoProblema(); // dois itens no mesmo instante
        }

        return Igual(chave, ChaveDoFeriado) ? MensagensDoPonto.FeriadoDuplicado.ComoProblema() : null;
    }

    private static bool Igual(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
