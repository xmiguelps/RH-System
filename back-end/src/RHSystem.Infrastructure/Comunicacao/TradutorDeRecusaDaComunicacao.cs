using RHSystem.Application.Comum;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Comunicacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Comunicacao;

/// <summary>A operação que gravava quando o banco recusou: o mesmo erro do banco tem resposta diferente por rota (a chave repetida do holerite).</summary>
public enum OperacaoDaComunicacao
{
    Outra,
    EnviarHolerite,
    SubstituirHolerite,
    RegistrarAdvertencia,
}

/// <summary>A operação e, no holerite, a competência que se gravava (o texto do <c>HOLERITE_JA_EXISTE</c> diz o tipo e o mês).</summary>
public readonly record struct ContextoDaComunicacao(OperacaoDaComunicacao Operacao, TipoDeHolerite Tipo = TipoDeHolerite.Mensal, int Mes = 1, int Ano = 2000);

/// <summary>
/// Traduz o que o banco recusou numa gravação da comunicação para a resposta do contrato (<see cref="ProblemaDeApiException"/>), ou <c>null</c> quando não é nada que a comunicação conheça (o chamador deixa subir e o tratador global decide).
/// <b>A chave repetida se distingue pelo NOME dela</b> e a regra de trigger (<c>SQLSTATE 45000</c>) pela mensagem original; a mensagem do MySQL traz o VALOR da chave e nunca é copiada para a resposta. A leitura repetida do aviso
/// (<c>UQ_AVISOS_VISUALIZACOES_AVISO_FUNCIONARIO</c>) <b>não é erro</b>: o caso de uso a trata como sucesso e por isso ela não passa por aqui.
/// </summary>
public static class TradutorDeRecusaDaComunicacao
{
    private const string ChaveDoHolerite = "UQ_HOLERITES_COMPETENCIA_TIPO_VERSAO";

    public static ProblemaDeApiException? Traduzir(Exception erro, ContextoDaComunicacao contexto)
    {
        var mysql = ErrosDoBanco.ErroDoMySql(erro);
        if (mysql is null)
        {
            return null;
        }

        if (mysql.SqlState == "45000")
        {
            return DaRegraDoBanco(mysql.Message);
        }

        if (ErrosDoBanco.EhChaveRepetida(mysql) && Igual(ErrosDoBanco.NomeDaChaveRepetida(mysql), ChaveDoHolerite))
        {
            return contexto.Operacao switch
            {
                OperacaoDaComunicacao.EnviarHolerite => MensagensDaComunicacao.HoleriteJaExiste(contexto.Tipo, contexto.Mes, contexto.Ano).ComoProblema(),
                OperacaoDaComunicacao.SubstituirHolerite => MensagensDaComunicacao.HoleriteVersaoDesatualizada.ComoProblema(),
                _ => null,
            };
        }

        return null;
    }

    private static ProblemaDeApiException DaRegraDoBanco(string regra)
    {
        if (Igual(regra, RegrasDoBancoDaComunicacao.HoleriteAnteriorAAdmissao))
        {
            return MensagensDaComunicacao.HoleriteCompetenciaAnteriorAAdmissao.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDaComunicacao.HoleritePosteriorAoDesligamento))
        {
            return MensagensDaComunicacao.HoleriteCompetenciaPosteriorAoDesligamento.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDaComunicacao.HoleriteCompetenciaFutura))
        {
            return MensagensDaComunicacao.HoleriteCompetenciaFutura.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDaComunicacao.AdvertenciaDeInativo))
        {
            return MensagensDaComunicacao.AdvertenciaDeInativo.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDaComunicacao.GestorAdvertePorSiMesmo))
        {
            return MensagensDaComunicacao.AdvertenciaGestorAdvertePorSiMesmo.ComoProblema(regra);
        }

        return MensagensDaComunicacao.OperacaoRecusada(regra);
    }

    private static bool Igual(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
