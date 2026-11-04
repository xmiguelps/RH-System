using RHSystem.Application.Auditoria;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Infrastructure.Auditoria;

/// <summary>Quem consta como autor do evento: a pessoa (com o IP, no portal) ou o sistema (canal Sistema, sem IP).</summary>
internal readonly record struct DadosDoAtor(TipoDeAtor Tipo, int? FuncionarioId, string Nome, Perfil? Perfil, CanalDeOrigem Canal, string? Ip)
{
    private const string SemLogin = "Sem login não há quem registrar: informe o ator com RegistrarComo ou RegistrarEGravarComoAsync.";

    /// <summary>O ator da requisição: <c>Pessoa</c> e <c>Sistema</c> se registram; <c>Anonimo</c> lança (quem está sem login informa o ator).</summary>
    public static DadosDoAtor De(IAtorAtual ator) => ator.Tipo switch
    {
        TipoDeAtorAtual.Pessoa => new DadosDoAtor(
            TipoDeAtor.Pessoa,
            ator.FuncionarioId ?? throw new InvalidOperationException("A pessoa logada não traz o funcionário no token."),
            ator.Nome,
            ator.Perfil,
            CanalDeOrigem.Portal,
            ator.Ip),
        TipoDeAtorAtual.Sistema => new DadosDoAtor(TipoDeAtor.Sistema, null, "Sistema", null, CanalDeOrigem.Sistema, null),
        _ => throw new InvalidOperationException(SemLogin),
    };

    /// <summary>O ator informado pelo caso de uso (a requisição é anônima): pessoa no portal, com o IP da requisição.</summary>
    public static DadosDoAtor De(AtorDoEvento informado, string? ipDaRequisicao) =>
        new(TipoDeAtor.Pessoa, informado.FuncionarioId, informado.Nome, informado.Perfil, CanalDeOrigem.Portal, ipDaRequisicao);
}

internal static class ConstrutorDeEvento
{
    /// <summary>Monta o evento com as invariantes do <c>CHECK</c> do banco (o <c>Criar</c> do Domínio) e o instante recebido do <c>IRelogio</c>.</summary>
    public static EventoDeAuditoria Criar(DateTime agoraUtc, DadosDoAtor ator, EventoDeAuditoriaNovo evento)
    {
        var criado = EventoDeAuditoria.Criar(
            agoraUtc,
            ator.Tipo,
            ator.FuncionarioId,
            ator.Nome,
            ator.Perfil,
            evento.Area,
            evento.Acao,
            evento.Descricao,
            evento.RegistroAfetado,
            evento.Entidade,
            evento.IdRegistro,
            ator.Canal,
            ator.Ip);

        var ordem = 1;
        foreach (var mudanca in evento.Mudancas ?? [])
        {
            criado.AdicionarMudanca(ordem++, mudanca.Campo, mudanca.Antes, mudanca.Depois, mudanca.ValorOculto);
        }

        return criado;
    }
}
