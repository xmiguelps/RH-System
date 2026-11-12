using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Ponto;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O recálculo do banco de horas de UM dia de UMA pessoa: monta o dia como o espelho o monta (<see cref="MontadorDoEspelho"/>), decide o que lançar (<see cref="RegraDoLancamentoDoDia"/>) e cria, reescreve ou apaga a linha do dia (uma por pessoa e dia de referência).
/// <b>Só dia que já passou e que é da vida da pessoa</b> (da admissão ao desligamento): hoje, o futuro e os dias fora do vínculo não mexem em nada. <b>Inativo</b> só atualiza ou apaga a linha que já existe (o trigger do banco recusa a linha nova de quem está inativo).
/// Duas gravações do mesmo dia ao mesmo tempo (a tarefa e uma aprovação) chocam na chave única: a perdedora relê e refaz uma vez. Não há evento de auditoria: é o cálculo do sistema, e a causa (o pedido, a correção, o atestado) já tem o dela.
/// </summary>
public sealed class RecalculoDoBancoDeHoras(AppDbContext db, IConsultaDoPonto consulta, MontadorDoEspelho montador, IRelogio relogio) : IRecalculoDoBancoDeHoras
{
    private const int TentativasNaCorrida = 2;

    public async Task RecalcularDiaAsync(int funcionarioId, DateOnly dia, CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var pessoa = await consulta.PessoaAsync(funcionarioId, ct);
        if (pessoa is null || dia >= hoje || dia < pessoa.DataAdmissao || (pessoa.DataDesligamento is { } desligamento && dia > desligamento))
        {
            return;
        }

        for (var tentativa = 1; ; tentativa++)
        {
            var decisao = RegraDoLancamentoDoDia.Decidir(await montador.MontarDiaAsync(pessoa, dia, ct));
            if (decisao.Acao == AcaoDoLancamento.Manter)
            {
                return;
            }

            try
            {
                await GravarAsync(pessoa, dia, decisao, hoje, ct);
                return;
            }
            catch (DbUpdateException erro) when (tentativa < TentativasNaCorrida && ErrosDoBanco.EhChaveRepetida(erro))
            {
                db.ChangeTracker.Clear(); // outra gravação do mesmo dia chegou primeiro: relê e refaz
            }
        }
    }

    private async Task GravarAsync(PessoaDoEspelho pessoa, DateOnly dia, DecisaoDoLancamento decisao, DateOnly hoje, CancellationToken ct)
    {
        var existente = await db.LancamentosDoBancoDeHoras.FirstOrDefaultAsync(l => l.FuncionarioId == pessoa.Id && l.DiaDeReferencia == dia, ct);
        if (decisao.Acao == AcaoDoLancamento.Apagar)
        {
            if (existente is null)
            {
                return;
            }

            db.LancamentosDoBancoDeHoras.Remove(existente);
        }
        else if (existente is not null)
        {
            existente.Refazer(decisao.Tipo!.Value, decisao.Minutos, decisao.Observacao!);
        }
        else if (pessoa.Ativo)
        {
            db.LancamentosDoBancoDeHoras.Add(LancamentoDoBancoDeHoras.Criar(pessoa.Id, dia, decisao.Tipo!.Value, decisao.Minutos, decisao.Observacao!, hoje));
        }
        else
        {
            return; // inativo sem linha: o banco recusaria a linha nova
        }

        await db.SaveChangesAsync(ct);
    }
}
