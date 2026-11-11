using Microsoft.EntityFrameworkCore;
using RHSystem.Api.IntegrationTests.Acesso;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Ponto;

namespace RHSystem.Api.IntegrationTests.Ponto;

/// <summary>
/// <c>FN_VERIFICAR_CONSISTENCIA_PONTO</c> (a definição do grupo de "ponto consistente") serve de ORÁCULO: a aplicação reimplementa a regra em C# (<see cref="SequenciaDoDia.EhConsistente"/>) e este teste compara as duas em
/// 300 sequências sorteadas (semente fixa, de 0 a 8 batidas, tipos e horários aleatórios), gravadas de verdade no banco.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ConsistenciaContraAFuncaoTests(MySqlFixture mysql)
{
    [Fact]
    public async Task EhConsistente_concorda_com_a_funcao_do_banco_em_300_sequencias_sorteadas()
    {
        var conexao = await mysql.NovoBancoComScriptsAsync();
        await using var db = ContextoDeTeste.Novo(conexao);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == "RH");
        var pessoa = Funcionario.Criar("Pessoa do oraculo", "oraculo@teste.com", new DateOnly(2026, 1, 2), nivel);
        db.Funcionarios.Add(pessoa);
        await db.SaveChangesAsync();

        var sorteio = new Random(20261111);
        var casos = new List<(int PontoId, bool Esperado)>();
        for (var caso = 0; caso < 300; caso++)
        {
            var dia = PontoDoDia.Novo(pessoa.Id, new DateOnly(2026, 1, 3).AddDays(caso));
            db.PontosDosDias.Add(dia);
            await db.SaveChangesAsync();

            var inicio = new DateTime(2026, 1, 3, 6, 0, 0, DateTimeKind.Utc).AddDays(caso);
            var quantidade = sorteio.Next(0, 9);
            var batidas = new List<MarcacaoDePonto>();
            for (var i = 0; i < quantidade; i++)
            {
                var tipo = sorteio.Next(2) == 0 ? TipoDeMarcacao.Entrada : TipoDeMarcacao.Saida;
                // Metade dos casos alterna direito (para termos consistentes de verdade); a outra metade é sorteio livre.
                if (caso % 2 == 0)
                {
                    tipo = i % 2 == 0 ? TipoDeMarcacao.Entrada : TipoDeMarcacao.Saida;
                }

                batidas.Add(MarcacaoDePonto.Nova(dia.Id, inicio.AddMinutes((i * 30) + sorteio.Next(0, 20)), tipo));
            }

            db.Marcacoes.AddRange(batidas.OrderBy(_ => sorteio.Next())); // a ordem de gravação (os ids) não é a do horário
            await db.SaveChangesAsync();

            var marcacoes = batidas.Select(b => new MarcacaoDoDia(b.Id, b.InstanteUtc, b.Tipo)).ToList();
            casos.Add((dia.Id, SequenciaDoDia.EhConsistente(marcacoes)));
        }

        var diferentes = new List<int>();
        foreach (var (pontoId, esperado) in casos)
        {
            var funcao = (string)(await Sql.EscalarAsync(conexao, $"SELECT FN_VERIFICAR_CONSISTENCIA_PONTO({pontoId})"))!;
            if ((funcao == "CONSISTENTE") != esperado)
            {
                diferentes.Add(pontoId);
            }
        }

        Assert.Empty(diferentes);
        Assert.True(casos.Count(c => c.Esperado) >= 30, "o sorteio precisa ter consistentes de verdade, senão o teste não prova nada");
        Assert.True(casos.Count(c => !c.Esperado) >= 30, "e inconsistentes também");
    }
}
