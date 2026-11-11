using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Cadastros;
using RHSystem.Application.Comum;
using RHSystem.Application.Empresa;
using RHSystem.Domain.Empresa;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Empresa;

/// <summary>
/// A linha única da configuração da empresa no banco (a chave é sempre <see cref="ConfiguracaoDaEmpresa.IdUnico"/>). Serve ao cadastro (<see cref="IEmpresaRepositorio"/>, com a entidade rastreada) e às outras
/// fatias (<see cref="IConfiguracaoDaEmpresa"/>, leitura sem rastreamento e <b>sem cache</b>: a mudança salva agora é a que a chamada seguinte vê).
/// </summary>
public sealed class EmpresaRepositorio(AppDbContext db) : IEmpresaRepositorio, IConfiguracaoDaEmpresa
{
    public async Task<ConfiguracaoDaEmpresa?> ObterAsync(CancellationToken ct) =>
        await db.ConfiguracoesDaEmpresa.FirstOrDefaultAsync(c => c.Id == ConfiguracaoDaEmpresa.IdUnico, ct);

    public async Task<bool> TentarAdicionarAsync(ConfiguracaoDaEmpresa nova, CancellationToken ct)
    {
        db.ConfiguracoesDaEmpresa.Add(nova);
        try
        {
            await db.SaveChangesAsync(ct);
            return true;
        }
        catch (DbUpdateException erro) when (ErrosDoBanco.EhChaveRepetida(erro))
        {
            // Outra chamada criou a linha antes. O contexto guarda a entidade nova e o evento de auditoria que não foram gravados: limpa tudo, e quem chamou lê a linha que existe e a atualiza.
            db.ChangeTracker.Clear();
            return false;
        }
    }

    public async Task SalvarAsync(CancellationToken ct) => await db.SaveChangesAsync(ct);

    async Task<ConfiguracaoDaEmpresaLida?> IConfiguracaoDaEmpresa.ObterAsync(CancellationToken ct) =>
        await db.ConfiguracoesDaEmpresa
            .AsNoTracking()
            .Where(c => c.Id == ConfiguracaoDaEmpresa.IdUnico)
            .Select(c => new ConfiguracaoDaEmpresaLida(c.NomeEmpresa, c.Uf, c.Municipio, c.HorarioEntrada, c.HorarioSaida, c.ToleranciaAtrasoMinutos, c.DiaPagamento))
            .FirstOrDefaultAsync(ct);

    public async Task<ConfiguracaoDaEmpresaLida> ObterOuFalharAsync(CancellationToken ct) =>
        await ((IConfiguracaoDaEmpresa)this).ObterAsync(ct)
        ?? throw new ErroDeNegocioException(MensagensDoApoio.EmpresaNaoConfigurada.Codigo, MensagensDoApoio.EmpresaNaoConfigurada.Detalhe);
}
