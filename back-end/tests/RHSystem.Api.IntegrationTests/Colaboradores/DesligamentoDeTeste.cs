using System.Globalization;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Colaboradores;

namespace RHSystem.Api.IntegrationTests.Colaboradores;

/// <summary>A condição que a fatia 3 vai registrar, falsa: o teste liga e desliga <see cref="Libera"/>.</summary>
internal sealed class CondicaoDeTeste(bool libera) : ICondicaoParaEfetivarDesligamento
{
    public bool Libera { get; set; } = libera;

    public Task<bool> PodeEfetivarAsync(int funcionarioId, DateOnly ultimoDiaDeTrabalho, CancellationToken ct) => Task.FromResult(Libera);
}

/// <summary>A fatia dona de pedidos (3 ou 7), falsa: anota as chamadas da porta única e pode lançar.</summary>
internal sealed class FatiaDeTeste(List<string> chamadas, bool lanca = false) : IPedidosParadosNoGestorDaFatia
{
    public Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)
    {
        chamadas.Add($"Aprovar:{gestorId}");
        return Task.FromResult(PedidosAprovados.Zero);
    }

    public Task PassarParaORhAsync(int gestorId, CancellationToken ct)
    {
        chamadas.Add($"Passar:{gestorId}");
        return lanca ? throw new InvalidOperationException("a fatia quebrou (de propósito)") : Task.CompletedTask;
    }
}

/// <summary>O que os testes de desligamento têm em comum: o relógio de 2026-10-07T12:42Z, o serviço chamado como o RH ou como o Sistema e as leituras do que o banco guardou.</summary>
internal static class DesligamentoDeTeste
{
    public static readonly DateTime Agora = new(2026, 10, 7, 12, 42, 0, DateTimeKind.Utc);

    /// <summary>Um banco por classe de teste; o relógio volta para <see cref="Agora"/> e as tarefas anteriores saem (o ciclo do executor roda tudo o que está vencido).</summary>
    public static async Task<ApiDeAcesso> ComecarAsync(MySqlFixture mysql, string chave)
    {
        var api = await ApiDeAcesso.ObterAsync(mysql, chave);
        api.Api.Relogio.Fixar(Agora);
        await Sql.ExecutarAsync(api.Conexao, "DELETE FROM TAREFAS_AGENDADAS");
        return api;
    }

    public static Task<ResultadoDoAgendamento> AgendarAsync(ApiDeAcesso api, int funcionarioId, DateOnly ultimoDia) =>
        api.NoEscopoAsync(sp => sp.GetRequiredService<IDesligamentoDeFuncionarios>().AgendarAsync(funcionarioId, ultimoDia, CancellationToken.None));

    public static Task CancelarAsync(ApiDeAcesso api, int funcionarioId) =>
        api.NoEscopoAsync(async sp =>
        {
            await sp.GetRequiredService<IDesligamentoDeFuncionarios>().CancelarAsync(funcionarioId, CancellationToken.None);
            return true;
        });

    /// <summary>Como a tarefa e a recuperação chamam: sem requisição, logo o ator é o Sistema.</summary>
    public static async Task<ResultadoDaEfetivacao> EfetivarComoSistemaAsync(ApiDeAcesso api, int desligamentoId)
    {
        await using var escopo = api.Api.Services.CreateAsyncScope();
        return await escopo.ServiceProvider.GetRequiredService<IDesligamentoDeFuncionarios>().EfetivarAsync(desligamentoId, CancellationToken.None);
    }

    /// <summary>Um desligamento AGENDADO por SQL (sem tarefa), e o id dele.</summary>
    public static async Task<int> AgendadoPorSqlAsync(ApiDeAcesso api, int funcionarioId, DateOnly ultimoDia)
    {
        await SementeDoCadastro.DesligamentoAgendadoAsync(api.Conexao, funcionarioId, ultimoDia);
        return await DesligamentoIdAsync(api, funcionarioId);
    }

    public static async Task<int> DesligamentoIdAsync(ApiDeAcesso api, int funcionarioId) =>
        (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_DESLIGAMENTO_AGENDADO FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {funcionarioId} ORDER BY ID_DESLIGAMENTO_AGENDADO DESC LIMIT 1");

    public static async Task<string?> SituacaoAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT SITUACAO FROM DESLIGAMENTOS_AGENDADOS WHERE ID_FUNCIONARIO = {funcionarioId} ORDER BY ID_DESLIGAMENTO_AGENDADO DESC LIMIT 1") as string;

    public static async Task<bool> AtivoAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.ContarAsync(api.Conexao, $"SELECT ATIVO FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}") == 1;

    public static async Task<string?> DataDeDesligamentoAsync(ApiDeAcesso api, int funcionarioId) =>
        await Sql.EscalarAsync(api.Conexao, $"SELECT DATE_FORMAT(DATA_DESLIGAMENTO, '%Y-%m-%d') FROM FUNCIONARIOS WHERE ID_FUNCIONARIO = {funcionarioId}") as string;

    public static async Task<int> TarefasDoDesligamentoAsync(ApiDeAcesso api, int desligamentoId) =>
        (int)await Sql.ContarAsync(api.Conexao, $"SELECT COUNT(*) FROM TAREFAS_AGENDADAS WHERE TIPO = 'EFETIVAR_DESLIGAMENTO' AND REFERENCIA_ID = {desligamentoId}");

    /// <summary>Um colaborador gestor (linha em <c>GESTORES</c>, nível <c>Funcionario Gestor</c>) que gerencia um setor novo; devolve o id da pessoa, o <c>ID_GESTOR</c> e o id do setor.</summary>
    public static async Task<(int PessoaId, int GestorId, int SetorId)> GestorDeSetorAsync(ApiDeAcesso api, string sufixo)
    {
        var cargo = await SementeDoCadastro.CargoAsync(api.Conexao, $"Cargo deslig {sufixo}", 44m);
        var pessoa = await SementeDoCadastro.ColaboradorAsync(api.Conexao, $"Gestor deslig {sufixo}", $"gestor.deslig.{sufixo.Replace(' ', '.')}.{Guid.NewGuid():N}@deslig.com.br", new DateOnly(2026, 1, 5), nivel: "Funcionario Gestor", cargoId: cargo);
        await api.NovoGestorAsync(pessoa);
        var gestor = (int)await Sql.ContarAsync(api.Conexao, $"SELECT ID_GESTOR FROM GESTORES WHERE ID_FUNCIONARIO = {pessoa}");
        var setor = await SementeDoCadastro.SetorAsync(api.Conexao, $"Setor deslig {sufixo}", gestor);
        return (pessoa, gestor, setor);
    }

    public static string Dia(int ano, int mes, int dia) => new DateOnly(ano, mes, dia).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
}
