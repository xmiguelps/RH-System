using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Application.Ponto.Mensagens;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro do ponto vêm dos EXEMPLOS do contrato, copiados e nunca reescritos (o front mostra o <c>detail</c> como vem). Este teste lê o contrato de verdade e confere o status, o título, o texto e o código de
/// cada mensagem. A exceção declarada é <c>FERIADO_NAO_EDITAVEL</c>, que o contrato cita mas não exemplifica.
/// </summary>
public class MensagensDoPontoTests
{
    private sealed record Exemplo(int Status, string Titulo, string Detalhe, string? Codigo);

    /// <summary>Todo objeto do contrato que parece um problema (<c>title</c>, <c>status</c> e <c>detail</c>), em qualquer profundidade.</summary>
    private static List<Exemplo> Exemplos()
    {
        var achados = new List<Exemplo>();

        void Varrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject objeto:
                    // O leitor do contrato converte todo valor simples em texto: o status vem como "409".
                    if (objeto["title"]?.GetValueKind() == JsonValueKind.String && objeto["detail"]?.GetValueKind() == JsonValueKind.String
                        && int.TryParse(objeto["status"]?.GetValue<string>(), System.Globalization.CultureInfo.InvariantCulture, out var status))
                    {
                        achados.Add(new Exemplo(status, objeto["title"]!.GetValue<string>(), objeto["detail"]!.GetValue<string>(), objeto["codigo"]?.GetValue<string>()));
                    }

                    foreach (var (_, filho) in objeto)
                    {
                        Varrer(filho);
                    }

                    break;
                case JsonArray lista:
                    foreach (var filho in lista)
                    {
                        Varrer(filho);
                    }

                    break;
            }
        }

        Varrer(CarregadorDeContrato.Contrato());
        return achados;
    }

    private static readonly string[] CodigosDoPonto =
    [
        "PONTO_SEQUENCIA_MUDOU", "PONTO_BATIDA_DUPLICADA", "PONTO_PEDIDO_JA_EM_ANALISE", "PONTO_DIA_COM_PEDIDO_EM_ANALISE", "PONTO_MARCACAO_EM_USO", "PEDIDO_JA_DECIDIDO", "PEDIDO_ETAPA_INCORRETA", "FERIADO_DUPLICADO",
        "ATESTADO_JA_DECIDIDO", "FUNCIONARIO_INATIVO", "PONTO_SEQUENCIA_INVALIDA", "PONTO_PEDIDO_DIA_NAO_PASSOU", "PONTO_INSTANTE_NO_FUTURO", "PONTO_INSTANTE_FORA_DO_DIA", "PEDIDO_RH_FORA_DE_GESTORES", "PEDIDO_PROPRIO",
        "EMPRESA_NAO_CONFIGURADA", "FERIADO_NAO_EDITAVEL",
    ];

    [Fact]
    public void Cada_mensagem_tem_status_titulo_texto_e_codigo_de_um_exemplo_do_contrato()
    {
        var exemplos = Exemplos();

        foreach (var mensagem in MensagensDoPonto.Todas.Where(m => m.Codigo != "FERIADO_NAO_EDITAVEL"))
        {
            var esperado = new Exemplo(mensagem.Status, mensagem.Titulo, mensagem.Detalhe, mensagem.Codigo);
            Assert.True(exemplos.Contains(esperado), $"O contrato não tem um exemplo igual a {mensagem.Codigo ?? "a recusa genérica"}: \"{mensagem.Titulo}\" / \"{mensagem.Detalhe}\".");
        }
    }

    [Fact]
    public void Todo_exemplo_de_erro_do_ponto_do_contrato_esta_no_catalogo()
    {
        var catalogo = MensagensDoPonto.Todas.Select(m => new Exemplo(m.Status, m.Titulo, m.Detalhe, m.Codigo)).ToHashSet();
        var doPonto = Exemplos().Where(e => e.Codigo is not null && CodigosDoPonto.Contains(e.Codigo) && !IgnoradoDeOutraFatia(e)).ToList();

        Assert.NotEmpty(doPonto);
        Assert.All(doPonto, e => Assert.True(catalogo.Contains(e), $"O exemplo {e.Codigo} (\"{e.Detalhe}\") do contrato não está em MensagensDoPonto."));
    }

    /// <summary>
    /// Os exemplos de <c>FUNCIONARIO_INATIVO</c>, <c>EMPRESA_NAO_CONFIGURADA</c>, <c>PEDIDO_JA_DECIDIDO</c> e <c>PEDIDO_ETAPA_INCORRETA</c> das outras fatias têm texto próprio (o do pedido de férias da fatia 7 é outro): são do catálogo
    /// delas, não do ponto.
    /// </summary>
    private static bool IgnoradoDeOutraFatia(Exemplo e) =>
        (e.Codigo == "FUNCIONARIO_INATIVO" && !e.Detalhe.StartsWith("Este colaborador está inativo", StringComparison.Ordinal))
        || (e.Codigo == "EMPRESA_NAO_CONFIGURADA" && !e.Detalhe.StartsWith("Cadastre a UF", StringComparison.Ordinal))
        || (e.Codigo == "PEDIDO_JA_DECIDIDO" && e.Detalhe == "Este pedido já foi decidido.")
        || (e.Codigo == "PEDIDO_ETAPA_INCORRETA" && e.Detalhe == "Este pedido ainda não chegou à sua etapa ou já passou por ela.");

    [Fact]
    public void A_recusa_generica_do_banco_e_a_dos_exemplos_sem_codigo()
    {
        var exemplo = Exemplos().Where(e => e.Codigo is null && e.Titulo == "Operação recusada.").Select(e => e.Detalhe).Distinct().ToList();

        Assert.Contains(MensagensDoPonto.OperacaoRecusada.Detalhe, exemplo);
        Assert.Null(MensagensDoPonto.OperacaoRecusada.Codigo);
        Assert.Equal(422, MensagensDoPonto.OperacaoRecusada.Status);
    }

    [Fact]
    public void FERIADO_NAO_EDITAVEL_o_contrato_cita_o_codigo_mas_nao_tem_exemplo_e_o_texto_e_o_do_plano()
    {
        Assert.DoesNotContain(Exemplos(), e => e.Codigo == "FERIADO_NAO_EDITAVEL");
        Assert.Contains("FERIADO_NAO_EDITAVEL", CarregadorDeContrato.Contrato().ToJsonString());
        Assert.Equal((422, "Feriado não editável.", "Só feriados municipais podem ser editados."), (MensagensDoPonto.FeriadoNaoEditavel.Status, MensagensDoPonto.FeriadoNaoEditavel.Titulo, MensagensDoPonto.FeriadoNaoEditavel.Detalhe));
    }

    [Fact]
    public void Os_codigos_do_catalogo_sao_exatamente_os_do_contrato_e_nenhum_texto_se_repete()
    {
        Assert.Equal(CodigosDoPonto.Order(), MensagensDoPonto.Todas.Where(m => m.Codigo is not null).Select(m => m.Codigo!).Distinct().Order());
        Assert.Equal(MensagensDoPonto.Todas.Count, MensagensDoPonto.Todas.Select(m => (m.Codigo, m.Detalhe)).Distinct().Count());
    }

    [Fact]
    public void ComoProblema_leva_status_titulo_texto_codigo_e_a_regra_do_banco_so_quando_vem()
    {
        var semRegra = MensagensDoPonto.PedidoJaDecidido.ComoProblema();
        var comRegra = MensagensDoPonto.PedidoProprio.ComoProblema(RegrasDoBancoDoPonto.PedidoProprio);

        Assert.Equal((409, "Pedido já decidido.", "Este pedido já foi respondido. Atualize a lista.", "PEDIDO_JA_DECIDIDO"), (semRegra.Status, semRegra.Titulo, semRegra.Detalhe, semRegra.Codigo));
        Assert.Null(semRegra.Extensoes);
        Assert.Equal("O gestor nao pode analisar a propria solicitacao.", comRegra.Extensoes!["regraDoBanco"]);
    }
}
