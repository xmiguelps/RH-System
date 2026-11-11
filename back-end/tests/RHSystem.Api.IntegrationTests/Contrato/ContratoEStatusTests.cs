using System.Text.Json.Nodes;
using Microsoft.Extensions.DependencyInjection;
using RHSystem.Api.IntegrationTests.Escopo;
using RHSystem.Api.IntegrationTests.Infra;
using RHSystem.Api.IntegrationTests.Matriz;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Regras estáticas sobre o contrato (sem banco) e a relação entre as rotas que a API tem, o <c>contrato/STATUS.md</c> e o escopo do teste de
/// divergência. O front lê o STATUS.md para saber se um passo de integração já pode rodar.
/// </summary>
[Collection(MySqlCollection.Nome)]
public class ContratoEStatusTests(MySqlFixture mysql)
{
    private static readonly string[] TresPerfis = ["Funcionario", "Gestor", "RH"];

    [Fact]
    public void X_perfis_do_contrato_segue_a_familia_da_rota()
    {
        // Falha se o dono editar uma rota para fora do padrão: /api/rh só RH, /api/gestor só Gestor, /api/eu os três, /api/publico só Publico.
        var fora = new List<string>();
        var conferidas = 0;
        foreach (var operacao in ContratoDaApi.Operacoes)
        {
            string[]? esperado = operacao.Caminho switch
            {
                var c when c.StartsWith("/api/rh/", StringComparison.Ordinal) => ["RH"],
                var c when c.StartsWith("/api/gestor/", StringComparison.Ordinal) => ["Gestor"],
                var c when c.StartsWith("/api/eu/", StringComparison.Ordinal) => TresPerfis,
                var c when c.StartsWith("/api/publico/", StringComparison.Ordinal) => ["Publico"],
                _ => null,
            };
            if (esperado is null)
            {
                continue;
            }

            conferidas++;
            if (!operacao.Perfis.Order().SequenceEqual(esperado.Order()))
            {
                fora.Add($"{operacao.Metodo} {operacao.Caminho} ({operacao.OperationId}): x-perfis é [{string.Join(", ", operacao.Perfis)}], a família pede [{string.Join(", ", esperado)}]");
            }
        }

        Assert.True(conferidas > 150, $"Só {conferidas} operações de família conferidas: o contrato foi lido errado?");
        Assert.True(fora.Count == 0, "x-perfis fora do padrão da família:\n- " + string.Join("\n- ", fora));
    }

    [Fact]
    public void Respostas_2xx_das_rotas_do_gestor_no_contrato_nao_tem_campos_do_rh()
    {
        var achados = new List<string>();
        var rotasDoGestor = ContratoDaApi.Operacoes.Where(o => o.Caminho.StartsWith("/api/gestor/", StringComparison.Ordinal)).ToList();
        var esquemasLidos = 0;
        foreach (var operacao in rotasDoGestor)
        {
            foreach (var esquema in ContratoDaApi.EsquemasDeRespostaDeSucesso(operacao.OperationId))
            {
                esquemasLidos++;
                achados.AddRange(CamposDoRh.ProibidosNoSchema(esquema).Select(c => $"{operacao.Metodo} {operacao.Caminho}: {c}"));
            }
        }

        Assert.NotEmpty(rotasDoGestor);
        Assert.True(esquemasLidos > 0, "Nenhum schema de resposta 2xx do gestor foi lido: a varredura passaria em branco.");
        Assert.True(achados.Count == 0, "Campo do RH numa resposta 2xx do gestor:\n- " + string.Join("\n- ", achados));
    }

    [Fact]
    public void Varredura_de_CamposDoRh_acha_campo_proibido_em_allOf_oneOf_items_e_propriedade_aninhada()
    {
        // Controle positivo: um schema sintético com "cpf" em cada lugar por onde a varredura precisa passar. Sem ele, "nenhum campo achado"
        // admitiria também "a varredura não chega lá".
        var emProperties = JsonNode.Parse("""{ "type": "object", "properties": { "cpf": { "type": "string" } } }""")!;
        var emAllOf = JsonNode.Parse("""{ "allOf": [ { "type": "object", "properties": { "nome": { "type": "string" } } }, { "type": "object", "properties": { "cpf": { "type": "string" } } } ] }""")!;
        var emOneOf = JsonNode.Parse("""{ "oneOf": [ { "type": "null" }, { "type": "object", "properties": { "rg": { "type": "string" } } } ] }""")!;
        var emAnyOf = JsonNode.Parse("""{ "anyOf": [ { "type": "object", "properties": { "emailPessoal": { "type": "string" } } } ] }""")!;
        var emItems = JsonNode.Parse("""{ "type": "array", "items": { "type": "object", "properties": { "matricula": { "type": "string" } } } }""")!;
        var aninhado = JsonNode.Parse("""{ "type": "object", "properties": { "ficha": { "type": "object", "properties": { "contato": { "type": "object", "properties": { "telefone": { "type": "string" } } } } } } }""")!;
        var permitido = JsonNode.Parse("""{ "type": "object", "properties": { "telefoneMascarado": { "type": "string" }, "nome": { "type": "string" } } }""")!;

        Assert.Contains(CamposDoRh.ProibidosNoSchema(emProperties), c => c.Contains("cpf"));
        Assert.Contains(CamposDoRh.ProibidosNoSchema(emAllOf), c => c.Contains("cpf"));
        Assert.Contains(CamposDoRh.ProibidosNoSchema(emOneOf), c => c.Contains("rg"));
        Assert.Contains(CamposDoRh.ProibidosNoSchema(emAnyOf), c => c.Contains("emailPessoal"));
        Assert.Contains(CamposDoRh.ProibidosNoSchema(emItems), c => c.Contains("matricula"));
        Assert.Contains(CamposDoRh.ProibidosNoSchema(aninhado), c => c.Contains("telefone"));
        Assert.Empty(CamposDoRh.ProibidosNoSchema(permitido)); // telefoneMascarado é outra chave e é permitida
    }

    [Fact]
    public void AfirmarSemCamposDoRh_acha_campo_proibido_em_qualquer_profundidade_no_JSON_real()
    {
        var limpo = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""{ "itens": [ { "nome": "Ana", "telefoneMascarado": "(11) 9****-7783" } ] }""");
        var sujo = System.Text.Json.JsonSerializer.Deserialize<System.Text.Json.JsonElement>("""{ "itens": [ { "nome": "Ana", "contato": { "cpf": "12345678909" } } ] }""");

        CamposDoRh.AfirmarSemCamposDoRh(limpo);
        Assert.ThrowsAny<Exception>(() => CamposDoRh.AfirmarSemCamposDoRh(sujo));
    }

    [Fact]
    public async Task STATUS_md_lista_toda_rota_implementada_e_toda_rota_listada_existe_na_API()
    {
        var (api, _) = await ApiComElenco.ObterAsync(mysql);
        var implementados = ContratoDaApi.OperationIdsImplementados(api.Services);
        var listados = StatusMd.OperationIdsDaSecaoRotasImplementadas();

        var semLinha = implementados.Except(listados).Order().ToList();
        var semRota = listados.Except(implementados).Order().ToList();

        Assert.NotEmpty(implementados);
        Assert.True(semLinha.Count == 0, "Rota implementada que não está no contrato/STATUS.md (seção \"Rotas implementadas\"): " + string.Join(", ", semLinha));
        Assert.True(semRota.Count == 0, "Linha do contrato/STATUS.md sem rota na API: " + string.Join(", ", semRota));
    }

    [Fact]
    public async Task Toda_rota_implementada_esta_no_conjunto_do_escopo_da_divergencia()
    {
        // Pega a tarefa que implementou a rota e esqueceu de acrescentá-la ao escopo do teste de divergência. A regra inversa (operação do escopo que a
        // API não tem) já é do comparador. É a ÚNICA prova desta regra: as fatias seguintes só acrescentam os operationId delas à lista.
        var (api, _) = await ApiComElenco.ObterAsync(mysql);

        var implementados = ContratoDaApi.OperationIdsImplementados(api.Services);

        Assert.NotEmpty(implementados);
        var fora = implementados.Where(id => !ContratoDivergenciaTests.OperationIdsNoEscopo.Contains(id)).Order().ToList();
        Assert.True(fora.Count == 0, "Rota implementada fora do escopo de ContratoDivergenciaTests.OperationIdsNoEscopo: " + string.Join(", ", fora));
    }
}
