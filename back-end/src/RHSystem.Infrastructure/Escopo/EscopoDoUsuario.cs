using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Escopo;

/// <summary>
/// O escopo da pessoa logada, lido do <see cref="IAtorAtual"/>. A equipe vem do banco (o setor de que ela é o gestor), e não do token: se o
/// gestor muda de setor, a próxima requisição já enxerga o setor novo, sem novo login. Uma instância vive uma requisição e lê a equipe uma vez.
/// </summary>
public sealed class EscopoDoUsuario(IAtorAtual ator, AppDbContext db, IRelogio relogio, ILogger<EscopoDoUsuario> log) : IEscopoDoUsuario
{
    private Task<EquipeDoGestor?>? _equipe;

    public int UsuarioId => ator.Tipo == TipoDeAtorAtual.Pessoa && ator.UsuarioId is { } id ? id : ForaDeRequisicaoAutenticada<int>();

    public int FuncionarioId => ator.Tipo == TipoDeAtorAtual.Pessoa && ator.FuncionarioId is { } id ? id : ForaDeRequisicaoAutenticada<int>();

    public Perfil Perfil => ator.Tipo == TipoDeAtorAtual.Pessoa && ator.Perfil is { } perfil ? perfil : ForaDeRequisicaoAutenticada<Perfil>();

    public string Nome => ator.Tipo == TipoDeAtorAtual.Pessoa ? ator.Nome : ForaDeRequisicaoAutenticada<string>();

    public Task<EquipeDoGestor?> ObterEquipeAsync(CancellationToken ct) => _equipe ??= LerEquipeAsync(ct);

    private static T ForaDeRequisicaoAutenticada<T>() =>
        throw new InvalidOperationException("Não há pessoa logada (fora de requisição autenticada): não há escopo a consultar.");

    private async Task<EquipeDoGestor?> LerEquipeAsync(CancellationToken ct)
    {
        var funcionarioId = FuncionarioId; // lança fora de requisição autenticada
        if (Perfil != Perfil.Gestor)
        {
            return null;
        }

        var setores = await (
                from gestor in db.Gestores
                join setor in db.Setores on gestor.Id equals setor.GestorId
                where gestor.FuncionarioId == funcionarioId
                orderby setor.Id
                select new { GestorId = gestor.Id, SetorId = setor.Id, SetorNome = setor.Nome })
            .ToListAsync(ct);
        if (setores.Count == 0)
        {
            return null;
        }

        if (setores.Count > 1)
        {
            log.LogWarning(
                "O gestor (funcionário {FuncionarioId}) é gestor de {Quantidade} setores; vale o de menor id ({SetorId}). O contrato fala em um setor por gestor.",
                funcionarioId,
                setores.Count,
                setores[0].SetorId);
        }

        var primeiro = setores[0];
        return new EquipeDoGestor(primeiro.GestorId, primeiro.SetorId, primeiro.SetorNome, funcionarioId, FusoBrasil.Hoje(relogio.AgoraUtc));
    }
}
