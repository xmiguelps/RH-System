using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Comunicacao.Advertencias;

/// <summary>Monta as respostas de advertência a partir da linha lida do banco: o gestor vira "quem aplicou" e o desfecho é deduzido da situação e do texto da contestação.</summary>
internal static class MapeadorDaAdvertencia
{
    public static AdvertenciaItem Item(AdvertenciaLida l) => new(l.Id, l.Data, l.Funcionario, l.Tipo, l.Motivo, RegrasDaAdvertencia.AplicadaPor(l.GestorNome), l.Situacao);

    public static AdvertenciaDetalheRh DetalheRh(AdvertenciaLida l) =>
        new(l.Id, l.Data, l.Funcionario, l.Tipo, l.Motivo, RegrasDaAdvertencia.AplicadaPor(l.GestorNome), l.Situacao, l.Descricao, l.CienciaEmUtc, l.TextoContestacao, l.ContestacaoEmUtc, RegrasDaAdvertencia.Desfecho(l.Situacao, l.TextoContestacao));

    public static AdvertenciaDetalheGestor DetalheGestor(AdvertenciaLida l) =>
        new(l.Id, l.Data, l.Funcionario, l.Tipo, l.Motivo, RegrasDaAdvertencia.AplicadaPor(l.GestorNome), l.Situacao, l.Descricao, l.CienciaEmUtc);

    public static AdvertenciaEu Eu(AdvertenciaLida l) =>
        new(l.Id, l.Data, l.Tipo, l.Motivo, l.Descricao, RegrasDaAdvertencia.AplicadaPor(l.GestorNome), l.Situacao, l.CienciaEmUtc, l.TextoContestacao, l.ContestacaoEmUtc, RegrasDaAdvertencia.Desfecho(l.Situacao, l.TextoContestacao));
}
