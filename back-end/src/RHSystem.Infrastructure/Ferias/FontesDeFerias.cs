using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Escopo;
using RHSystem.Application.Ferias;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Painel;
using RHSystem.Domain.Ferias;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ferias;

/// <summary>
/// O indicador "férias disponíveis" da ficha (RH e gestor): o MESMO saldo da rota de férias (<see cref="SaldoDeFerias"/>, a soma dos dias disponíveis dos períodos fechados), calculado das linhas da pessoa. Pessoa que não existe: <c>null</c> (a ficha já deu 404 antes).
/// </summary>
public sealed class FonteDoIndicadorDeFerias(IConsultaDeFerias consulta, IRelogio relogio) : IFonteDeIndicadorDaFicha
{
    public IndicadorDaFicha Indicador => IndicadorDaFicha.FeriasDisponiveisDias;

    public async Task<int?> ObterAsync(int funcionarioId, CancellationToken ct)
    {
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct);
        if (pessoa is null)
        {
            return null;
        }

        var linhas = await consulta.DaPessoaAsync(funcionarioId, ct);
        return SaldoDeFerias.De(pessoa.DataAdmissao, pessoa.DataDesligamento, FusoBrasil.Hoje(relogio.AgoraUtc), [.. linhas.Select(l => l.ParaSaldo())]).DiasDisponiveis;
    }
}

/// <summary>
/// As férias que aguardam o GESTOR, para a bolinha "pendências da equipe": <c>COM_O_GESTOR</c> (o <c>ID_GESTOR</c> é o dele e sem resultado dele), de gente da equipe de hoje. <c>SUGESTAO_DE_DATA</c> espera a pessoa e não conta; o que já está com o RH também não. Soma com a fonte dos ajustes de ponto.
/// </summary>
public sealed class FonteDeFeriasParaOGestor(AppDbContext db) : IFonteDeContadorDoMenu
{
    public ContadorDoMenu Contador => ContadorDoMenu.PendenciasDaEquipe;

    public async Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct)
    {
        if (await escopo.ObterEquipeAsync(ct) is not { } equipe)
        {
            return 0;
        }

        var gestorId = equipe.GestorId;
        return await db.PedidosDeFerias.AsNoTracking()
            .Where(p => p.Status == StatusDasFerias.Solicitada && p.GestorId == gestorId && p.ResultadoDoGestor == null && db.Funcionarios.DaEquipe(equipe).Select(f => f.Id).Contains(p.FuncionarioId))
            .CountAsync(ct);
    }
}
