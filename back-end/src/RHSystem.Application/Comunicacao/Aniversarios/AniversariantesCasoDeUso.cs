using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Regras;

namespace RHSystem.Application.Comunicacao.Aniversarios;

/// <summary>Um aniversariante da lista (<c>AniversarianteItem</c>): a pessoa, a data da próxima comemoração e os dias até ela. Nunca o ano de nascimento nem a idade.</summary>
public sealed record AniversarianteItem(FuncionarioResumo Funcionario, DateOnly AniversarioEm, int DiasAte);

/// <summary>A resposta de <c>listarAniversariantes</c>: a janela em dias (14) e os itens. A lista é curta e não é paginada.</summary>
public sealed record AniversariantesResposta(int Dias, IReadOnlyList<AniversarianteItem> Itens);

/// <summary>Quem pode aparecer: a pessoa e a data de nascimento (que fica aqui dentro: só a conta da janela a lê).</summary>
public sealed record CandidatoAAniversario(FuncionarioResumo Funcionario, DateOnly Nascimento);

public interface IConsultaDeAniversariantes
{
    /// <summary>As pessoas ATIVAS, com a admissão já começada (<c>DataAdmissao &lt;= hoje</c>) e com data de nascimento, da empresa inteira (não só de um setor).</summary>
    Task<IReadOnlyList<CandidatoAAniversario>> CandidatosAsync(DateOnly hoje, CancellationToken ct);
}

/// <summary>
/// <c>GET /api/eu/aniversariantes</c> (qualquer perfil): quem faz aniversário de hoje até 14 dias à frente, da empresa inteira, pelo dia de hoje no horário do Brasil. Só pessoas ativas com data de nascimento (e com a admissão já começada). Ordem: a data do aniversário e, no empate, o
/// nome. <c>diasAte</c> 0 é hoje. A lista não traz o ano de nascimento nem a idade.
/// </summary>
public sealed class ListarAniversariantesCasoDeUso(IConsultaDeAniversariantes consulta, IRelogio relogio)
{
    public async Task<AniversariantesResposta> ExecutarAsync(CancellationToken ct)
    {
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var candidatos = await consulta.CandidatosAsync(hoje, ct);
        var itens = candidatos
            .Where(c => JanelaDeAniversarios.EstaNaJanela(c.Nascimento, hoje))
            .Select(c => new AniversarianteItem(c.Funcionario, JanelaDeAniversarios.ProximoAniversario(c.Nascimento, hoje), JanelaDeAniversarios.DiasAte(c.Nascimento, hoje)))
            .OrderBy(a => a.AniversarioEm)
            .ThenBy(a => a.Funcionario.Nome, StringComparer.Create(System.Globalization.CultureInfo.InvariantCulture, ignoreCase: true))
            .ThenBy(a => a.Funcionario.Id)
            .ToList();
        return new AniversariantesResposta(JanelaDeAniversarios.DiasAFrente, itens);
    }
}
