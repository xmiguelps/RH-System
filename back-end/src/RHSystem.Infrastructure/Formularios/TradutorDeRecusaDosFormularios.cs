using RHSystem.Application.Comum;
using RHSystem.Application.Formularios.Mensagens;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Formularios;

/// <summary>
/// Traduz o que o banco recusou numa gravação dos formulários para a resposta do contrato (<see cref="ProblemaDeApiException"/>), ou <c>null</c> quando não é nada que os formulários conheçam (o chamador deixa subir e o tratador global decide).
/// <b>A restrição, a chave única e a chave estrangeira se distinguem pelo NOME</b> e a regra de trigger (<c>SQLSTATE 45000</c>) pela mensagem original; a mensagem do MySQL traz o VALOR da chave (um e-mail, por exemplo) e nunca é copiada para a resposta.
/// Devolvem <c>null</c> de propósito o destinatário repetido (o chamador conta em <c>jaEstavam</c>) e o token repetido (o chamador gera outro): não são erro para quem chama.
/// </summary>
public static class TradutorDeRecusaDosFormularios
{
    /// <summary>O número do MySQL para "a restrição CHECK foi violada".</summary>
    private const int ViolacaoDeCheck = 3819;

    public static ProblemaDeApiException? Traduzir(Exception erro)
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

        if (mysql.Number == ViolacaoDeCheck)
        {
            return DaRestricao(NomeDaRestricao(mysql.Message));
        }

        if (ErrosDoBanco.EhChaveRepetida(mysql))
        {
            return DaChaveRepetida(ErrosDoBanco.NomeDaChaveRepetida(erro));
        }

        if (ErrosDoBanco.EhChaveEstrangeiraEmUso(mysql) && Igual(ErrosDoBanco.NomeDaChaveEstrangeira(erro), RegrasDoBancoDosFormularios.ChaveEstrangeiraDoAnonimato))
        {
            return MensagensDosFormularios.AnonimatoFixoAposPublicar.ComoProblema();
        }

        return null;
    }

    private static ProblemaDeApiException DaRegraDoBanco(string regra)
    {
        if (Igual(regra, RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoAlterar))
        {
            return MensagensDosFormularios.PerguntaJaRespondidaParaEditar.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDosFormularios.PerguntaJaRespondidaAoExcluir))
        {
            return MensagensDosFormularios.PerguntaJaRespondidaParaExcluir.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDosFormularios.FormularioInativoNaoAceitaRespostas))
        {
            return MensagensDosFormularios.FormularioEncerradoParaResponder.ComoProblema(regra);
        }

        if (Igual(regra, RegrasDoBancoDosFormularios.FuncionarioInativoNaoRespondeFormularios))
        {
            return MensagensDosFormularios.PessoaInativaParaResponder.ComoProblema(regra);
        }

        return MensagensDosFormularios.OperacaoRecusada(regra);
    }

    private static ProblemaDeApiException? DaRestricao(string? nome)
    {
        if (nome is null)
        {
            return null;
        }

        return Igual(nome, RegrasDoBancoDosFormularios.AnonimoSemLink)
            ? MensagensDosFormularios.AnonimoSemLink.ComoProblema(nome)
            : MensagensDosFormularios.OperacaoRecusada(nome);
    }

    private static ProblemaDeApiException? DaChaveRepetida(string? chave)
    {
        if (Igual(chave, RegrasDoBancoDosFormularios.ChaveDoEmailDoCandidato))
        {
            return MensagensDosFormularios.JaRespondidoPeloCandidato.ComoProblema();
        }

        if (Igual(chave, RegrasDoBancoDosFormularios.ChaveDaParticipacao))
        {
            return MensagensDosFormularios.JaRespondidoPeloFuncionario.ComoProblema();
        }

        if (Igual(chave, RegrasDoBancoDosFormularios.ChaveDaOrdemDaPergunta) || Igual(chave, RegrasDoBancoDosFormularios.ChaveDaOrdemDaAlternativa))
        {
            return MensagensDosFormularios.PerguntasMudaramEmOutraTela;
        }

        return null;
    }

    /// <summary>O nome da restrição na mensagem do MySQL (<c>Check constraint 'CHK_X' is violated.</c>), ou <c>null</c>. Só o nome sai: ele não traz valor.</summary>
    private static string? NomeDaRestricao(string mensagem)
    {
        var inicio = mensagem.IndexOf('\'', StringComparison.Ordinal);
        var fim = inicio < 0 ? -1 : mensagem.IndexOf('\'', inicio + 1);
        return fim < 0 ? null : mensagem[(inicio + 1)..fim];
    }

    private static bool Igual(string? a, string b) => string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
}
