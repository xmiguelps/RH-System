using System.Globalization;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Agendamento;
using static RHSystem.Api.IntegrationTests.Colaboradores.DesligamentoDeTeste;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>
/// <c>LIMPAR_TOKENS_DE_SENHA</c> no banco REAL, com o relógio em 2026-10-07T12:42Z: o corte é de 30 dias da criação (2026-09-07T12:42Z). O hash do token é de teste (64 hexadecimais por linha).
/// </summary>
[Collection(MySqlCollection.Nome)]
public class LimparTokensDeSenhaTarefaTests(MySqlFixture mysql)
{
    private const string Tipo = "LIMPAR_TOKENS_DE_SENHA";

    private static int _hash;

    private Task<ApiDeAcesso> ApiAsync() => ComecarAsync(mysql, nameof(LimparTokensDeSenhaTarefaTests));

    /// <summary>Um token de senha por SQL (o que o convite e a redefinição gravam). Devolve o hash, que identifica a linha.</summary>
    private static async Task<string> TokenAsync(ApiDeAcesso api, int usuarioId, string tipo, string criadoEm, string expiraEm, string? usadoEm = null, string? invalidadoEm = null)
    {
        var hash = Interlocked.Increment(ref _hash).ToString("x64", CultureInfo.InvariantCulture);
        await Sql.ExecutarAsync(
            api.Conexao,
            $"INSERT INTO USUARIOS_TOKENS_SENHA (ID_USUARIO, TIPO, TOKEN_HASH, CRIADO_EM, EXPIRA_EM, USADO_EM, INVALIDADO_EM) VALUES ({usuarioId}, '{tipo}', '{hash}', '{criadoEm}', '{expiraEm}', {Texto(usadoEm)}, {Texto(invalidadoEm)})");
        return hash;
    }

    private static string Texto(string? valor) => valor is null ? "NULL" : $"'{valor}'";

    private static async Task<bool> ExisteAsync(ApiDeAcesso api, string hash) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM USUARIOS_TOKENS_SENHA WHERE TOKEN_HASH = '{hash}'") == 1;

    private static async Task ExecutarAsync(ApiDeAcesso api)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        var tarefa = escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tipo);
        await tarefa.ExecutarAsync(new TarefaParaExecutar(0, $"{Tipo}:dia=2026-10-07", null, new DateOnly(2026, 10, 7), null, 1), CancellationToken.None);
    }

    [Fact]
    public async Task Apaga_tokens_velhos_usados_ou_invalidados_e_o_convite_antigo_que_tem_outro_mais_novo_e_nunca_o_ultimo_convite_nem_token_em_aberto_valido()
    {
        var api = await ApiAsync();
        var ana = await api.NovaPessoaAsync("tokens1", login: TipoDeLogin.SenhaDefinida);
        var bia = await api.NovaPessoaAsync("tokens2", login: TipoDeLogin.SenhaDefinida);
        var usuarioDaAna = ana.UsuarioId!.Value;
        var usuarioDaBia = bia.UsuarioId!.Value;

        // Saem: velhos (mais de 30 dias) que já terminaram.
        var redefinicaoUsada = await TokenAsync(api, usuarioDaAna, "REDEFINICAO", "2026-08-01 12:00:00", "2026-08-01 13:00:00", usadoEm: "2026-08-01 12:30:00");
        var redefinicaoInvalidada = await TokenAsync(api, usuarioDaAna, "REDEFINICAO", "2026-08-02 12:00:00", "2026-08-02 13:00:00", invalidadoEm: "2026-08-02 12:10:00");
        var redefinicaoVencida = await TokenAsync(api, usuarioDaAna, "REDEFINICAO", "2026-08-03 12:00:00", "2026-08-03 13:00:00"); // em aberto, mas já venceu: não vale mais
        var conviteAntigoComNovo = await TokenAsync(api, usuarioDaAna, "CONVITE", "2026-08-04 12:00:00", "2026-08-11 12:00:00", invalidadoEm: "2026-08-10 12:00:00");
        var umSegundoAntesDoCorte = await TokenAsync(api, usuarioDaBia, "REDEFINICAO", "2026-09-07 12:41:59", "2026-09-07 13:41:59", usadoEm: "2026-09-07 12:50:00");

        // Ficam: o último convite de cada pessoa, o token em aberto ainda válido e o que não passou do prazo.
        var ultimoConviteDaAna = await TokenAsync(api, usuarioDaAna, "CONVITE", "2026-09-20 12:00:00", "2026-09-27 12:00:00", usadoEm: "2026-09-21 12:00:00");
        var ultimoConviteDaBia = await TokenAsync(api, usuarioDaBia, "CONVITE", "2026-08-05 12:00:00", "2026-08-12 12:00:00", usadoEm: "2026-08-06 12:00:00"); // velho e usado, mas é o único convite dela
        var emAbertoAindaValido = await TokenAsync(api, usuarioDaBia, "REDEFINICAO", "2026-08-06 12:00:00", "2026-12-01 12:00:00");
        var noCorteExato = await TokenAsync(api, usuarioDaAna, "REDEFINICAO", "2026-09-07 12:42:00", "2026-09-07 13:42:00", usadoEm: "2026-09-07 12:45:00"); // "mais de 30 dias": o dia 30 inteiro ainda fica
        var recente = await TokenAsync(api, usuarioDaAna, "REDEFINICAO", "2026-10-01 12:00:00", "2026-10-01 13:00:00", usadoEm: "2026-10-01 12:30:00");

        await ExecutarAsync(api);

        foreach (var saiu in new[] { redefinicaoUsada, redefinicaoInvalidada, redefinicaoVencida, conviteAntigoComNovo, umSegundoAntesDoCorte })
        {
            Assert.False(await ExisteAsync(api, saiu));
        }

        foreach (var ficou in new[] { ultimoConviteDaAna, ultimoConviteDaBia, emAbertoAindaValido, noCorteExato, recente })
        {
            Assert.True(await ExisteAsync(api, ficou));
        }
    }

    [Fact]
    public async Task RecuperarAsync_agenda_uma_limpeza_por_dia_sem_duplicar()
    {
        var api = await ApiAsync();
        async Task RecuperarAsync()
        {
            await using var escopo = api.Api.Services.CreateAsyncScope();
            await escopo.ServiceProvider.GetServices<ITarefaAgendada>().Single(t => t.Tipo == Tipo).RecuperarAsync(CancellationToken.None);
        }

        await RecuperarAsync();
        await RecuperarAsync(); // no mesmo dia: não duplica
        await using (var db = ContextoDeTeste.Novo(api.Conexao))
        {
            var tarefa = Assert.Single(await db.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == Tipo).ToListAsync());
            Assert.Equal(($"{Tipo}:dia=2026-10-07", new DateOnly(2026, 10, 7)), (tarefa.ChaveDeIdempotencia, tarefa.DiaReferencia));
        }

        api.Api.Relogio.Avancar(TimeSpan.FromDays(1));
        await RecuperarAsync();
        await using var db2 = ContextoDeTeste.Novo(api.Conexao);
        var chaves = await db2.TarefasAgendadas.AsNoTracking().Where(t => t.Tipo == Tipo).Select(t => t.ChaveDeIdempotencia).OrderBy(c => c).ToListAsync();
        Assert.Equal([$"{Tipo}:dia=2026-10-07", $"{Tipo}:dia=2026-10-08"], chaves);
    }
}
