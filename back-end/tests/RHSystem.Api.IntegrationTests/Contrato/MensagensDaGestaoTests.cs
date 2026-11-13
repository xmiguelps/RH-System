using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Application.Ferias.Regras;
using RHSystem.Application.Gestao.Mensagens;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro da gestão vêm dos EXEMPLOS do contrato (<c>70-fatia-7-gestao.yaml</c>), copiados e nunca reescritos (o front mostra o <c>detail</c> como vem). Este teste lê o contrato de verdade e confere o status, o título, o texto e o código de cada mensagem; as que o contrato cita e não
/// exemplifica (a pessoa inativa de férias e a falta de período aquisitivo) estão na lista de exceções (o executor escreve o texto). Os textos dos 409 de pedido são os DESTA fatia, diferentes dos do ponto.
/// </summary>
public class MensagensDaGestaoTests
{
    private sealed record Exemplo(int Status, string Titulo, string Detalhe, string? Codigo);

    private static List<Exemplo> Exemplos()
    {
        var achados = new List<Exemplo>();

        void Varrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject objeto:
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

    /// <summary>Os textos que o contrato NÃO traz em exemplo, escritos pelo executor.</summary>
    private static readonly string[] SemExemplo = ["FERIAS_SEM_PERIODO_AQUISITIVO"];

    [Fact]
    public void Cada_mensagem_de_texto_fixo_tem_status_titulo_texto_e_codigo_de_um_exemplo_do_contrato()
    {
        var exemplos = Exemplos();

        foreach (var mensagem in MensagensDaGestao.Todas.Where(m => !SemExemplo.Contains(m.Codigo)))
        {
            var esperado = new Exemplo(mensagem.Status, mensagem.Titulo, mensagem.Detalhe, mensagem.Codigo);
            Assert.True(exemplos.Contains(esperado), $"O contrato não tem um exemplo igual a {mensagem.Codigo}: \"{mensagem.Titulo}\" / \"{mensagem.Detalhe}\".");
        }
    }

    [Fact]
    public void Os_dois_409_de_pedido_sao_os_textos_da_fatia_7_e_nao_os_do_ponto()
    {
        var etapa = MensagensDaGestao.PedidoNaEtapaErrada;
        var decidido = MensagensDaGestao.PedidoJaDecidido;

        Assert.Equal((409, "Pedido na etapa errada.", "Este pedido ainda não chegou à sua etapa ou já passou por ela.", "PEDIDO_ETAPA_INCORRETA"), (etapa.Status, etapa.Titulo, etapa.Detalhe, etapa.Codigo));
        Assert.Equal((409, "Pedido já decidido.", "Este pedido já foi decidido.", "PEDIDO_JA_DECIDIDO"), (decidido.Status, decidido.Titulo, decidido.Detalhe, decidido.Codigo));
    }

    [Fact]
    public void As_tres_formas_do_422_de_regras_de_ferias_saem_como_o_contrato_mostra_com_a_lista_de_regras()
    {
        var exemplos = Exemplos();
        var regras = new List<ResultadoDaRegra> { new(RegraDeFerias.CabeNoSaldo, false, "Passa do saldo disponível: faltam 4 dias."), new(RegraDeFerias.AntecedenciaDe30Dias, true, "Começa daqui a mais de 30 dias.") };

        var pedir = MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.PedirOuAceitar, regras);
        var sugerir = MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.Sugerir, regras);
        var registrar = MensagensDaGestao.FeriasRegrasNaoAtendidas(VarianteDasRegras.Registrar, regras);

        Assert.Contains(new Exemplo(422, "Pedido de férias recusado.", "O pedido não cumpre uma ou mais regras de férias.", "FERIAS_REGRAS_NAO_ATENDIDAS"), exemplos);
        Assert.Contains(new Exemplo(422, "Sugestão recusada pelas regras.", "As datas sugeridas não cumprem uma ou mais regras de férias.", "FERIAS_REGRAS_NAO_ATENDIDAS"), exemplos);
        Assert.Contains(new Exemplo(422, "Registro de férias recusado.", "O registro não cumpre uma ou mais regras de férias.", "FERIAS_REGRAS_NAO_ATENDIDAS"), exemplos);
        Assert.Equal(("Pedido de férias recusado.", "O pedido não cumpre uma ou mais regras de férias."), (pedir.Titulo, pedir.Detalhe));
        Assert.Equal(("Sugestão recusada pelas regras.", "As datas sugeridas não cumprem uma ou mais regras de férias."), (sugerir.Titulo, sugerir.Detalhe));
        Assert.Equal(("Registro de férias recusado.", "O registro não cumpre uma ou mais regras de férias."), (registrar.Titulo, registrar.Detalhe));
        Assert.All(new[] { pedir, sugerir, registrar }, p => Assert.Equal((422, "FERIAS_REGRAS_NAO_ATENDIDAS"), (p.Status, p.Codigo)));
        var lista = Assert.IsAssignableFrom<IReadOnlyList<IReadOnlyDictionary<string, object?>>>(pedir.Extensoes!["regras"]);
        Assert.Equal(
            [("CABE_NO_SALDO", false, "Passa do saldo disponível: faltam 4 dias."), ("ANTECEDENCIA_DE_30_DIAS", true, "Começa daqui a mais de 30 dias.")],
            lista.Select(r => ((string)r["codigo"]!, (bool)r["atendida"]!, (string)r["mensagem"]!)));
    }

    [Fact]
    public void Os_codigos_das_oito_regras_sao_os_do_enum_do_contrato_na_ordem()
    {
        var doContrato = CarregadorDeContrato.Contrato()["components"]!["schemas"]!["FeriasRegraCodigo"]!["enum"]!.AsArray().Select(n => n!.GetValue<string>()).ToList();

        Assert.Equal(doContrato, Enum.GetValues<RegraDeFerias>().Select(CodigosDasRegrasDeFerias.Texto));
    }

    [Fact]
    public void A_pessoa_inativa_e_o_pedido_proprio_e_o_RH_fora_de_gestores_levam_a_regra_do_banco_como_o_contrato_mostra()
    {
        var inativoDeFerias = MensagensDaGestao.FuncionarioInativoDeFerias();
        var proprio = MensagensDaGestao.PedidoProprio();
        var foraDeGestores = MensagensDaGestao.PedidoRhForaDeGestores();

        Assert.Equal(("FUNCIONARIO_INATIVO", "Funcionario inativo nao pode solicitar ferias."), (inativoDeFerias.Codigo, inativoDeFerias.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "PEDIDO_PROPRIO", "Você não pode decidir o próprio pedido.", "A operação foi recusada por uma regra do banco de dados.", "O gestor nao pode analisar as proprias ferias."), (proprio.Status, proprio.Codigo, proprio.Titulo, proprio.Detalhe, proprio.Extensoes!["regraDoBanco"]));
        Assert.Equal((422, "PEDIDO_RH_FORA_DE_GESTORES", "Quem decide precisa ser gestor também.", "Informe o gestor responsavel pela analise das ferias."), (foraDeGestores.Status, foraDeGestores.Codigo, foraDeGestores.Titulo, foraDeGestores.Extensoes!["regraDoBanco"]));
        var exemplos = Exemplos();
        Assert.Contains(exemplos, e => e is { Codigo: "PEDIDO_PROPRIO" } && e.Titulo == proprio.Titulo && e.Detalhe == proprio.Detalhe);
        Assert.Contains(exemplos, e => e is { Codigo: "PEDIDO_RH_FORA_DE_GESTORES" } && e.Titulo == foraDeGestores.Titulo && e.Detalhe == foraDeGestores.Detalhe);
    }

    [Fact]
    public void A_pessoa_inativa_nos_beneficios_tem_dois_textos_o_de_pedir_e_o_de_aprovar()
    {
        var exemplos = Exemplos();

        Assert.Contains(new Exemplo(422, "Operação recusada.", "Não é possível pedir nem ativar benefício para uma pessoa inativa.", "FUNCIONARIO_INATIVO"), exemplos);
        Assert.Contains(new Exemplo(422, "Operação recusada.", "Não é possível ativar benefício para uma pessoa inativa.", "FUNCIONARIO_INATIVO"), exemplos);
        Assert.Equal("Não é possível pedir nem ativar benefício para uma pessoa inativa.", MensagensDaGestao.FuncionarioInativoParaPedirBeneficio.Detalhe);
        Assert.Equal("Não é possível ativar benefício para uma pessoa inativa.", MensagensDaGestao.FuncionarioInativoParaAprovarBeneficio.Detalhe);
    }

    [Fact]
    public void Os_codigos_do_catalogo_sao_exatamente_os_da_fatia_e_nenhum_texto_se_repete()
    {
        string[] esperados =
        [
            "PEDIDO_ETAPA_INCORRETA", "PEDIDO_JA_DECIDIDO", "BENEFICIO_JA_ATIVO_OU_PENDENTE", "BENEFICIO_ADESAO_NAO_ATIVA", "BENEFICIO_PEDIDO_JA_DECIDIDO",
            "BENEFICIO_NAO_OPCIONAL", "BENEFICIO_NAO_ACEITA_DEPENDENTES", "BENEFICIO_NAO_ACEITA_TRAJETO", "FUNCIONARIO_INATIVO", "FERIAS_SEM_PERIODO_AQUISITIVO",
        ];

        Assert.Equal(esperados.Order(), MensagensDaGestao.Todas.Select(m => m.Codigo!).Distinct().Order());
        Assert.Equal(MensagensDaGestao.Todas.Count, MensagensDaGestao.Todas.Select(m => (m.Codigo, m.Detalhe)).Distinct().Count());
    }

    [Fact]
    public void A_operacao_recusada_generica_carrega_a_regra_do_banco_e_nenhum_valor()
    {
        var recusada = MensagensDaGestao.OperacaoRecusada("Transicao de status de ferias invalida.");

        Assert.Equal((422, "Operação recusada.", "A operação foi recusada por uma regra do banco de dados.", (string?)null, "Transicao de status de ferias invalida."), (recusada.Status, recusada.Titulo, recusada.Detalhe, recusada.Codigo, recusada.Extensoes!["regraDoBanco"]));
    }
}
