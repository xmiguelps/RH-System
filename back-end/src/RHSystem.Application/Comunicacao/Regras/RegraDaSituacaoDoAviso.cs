using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Regras;

/// <summary>
/// A situação do aviso, <b>calculada</b> das colunas e do relógio (nunca gravada): <c>ENVIADO = false</c> é rascunho; enviado com publicação depois de agora é agendado; com expiração até agora é expirado (&lt;=, para o "encerrar agora" sair já
/// expirado); senão ativo. Enviado sem data de publicação não existe (publicar sempre grava a data): vale como ativo. A visibilidade não depende do agendador: o aviso agendado aparece sozinho na hora.
/// </summary>
public static class RegraDaSituacaoDoAviso
{
    public static SituacaoDoAviso De(bool enviado, DateTime? publicacaoUtc, DateTime? expiracaoUtc, DateTime agoraUtc)
    {
        if (!enviado)
        {
            return SituacaoDoAviso.Rascunho;
        }

        if (publicacaoUtc is { } publicacao && publicacao > agoraUtc)
        {
            return SituacaoDoAviso.Agendado;
        }

        if (expiracaoUtc is { } expiracao && expiracao <= agoraUtc)
        {
            return SituacaoDoAviso.Expirado;
        }

        return SituacaoDoAviso.Ativo;
    }
}
