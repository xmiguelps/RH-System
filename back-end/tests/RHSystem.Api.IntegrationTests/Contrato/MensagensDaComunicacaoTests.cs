using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro da comunicação vêm dos EXEMPLOS do contrato (<c>40-fatia-4-comunicacao-e-documentos.yaml</c>), copiados e nunca reescritos (o front mostra o <c>detail</c> como vem). Este teste lê o contrato de verdade e confere o
/// status, o título, o texto e o código de cada mensagem. As exceções declaradas: <c>ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO</c> (o contrato cita e não exemplifica) e os códigos que só aparecem no resultado do lote.
/// </summary>
public class MensagensDaComunicacaoTests
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

    private static readonly string[] CodigosDaComunicacao =
    [
        "AVISO_PUBLICACAO_NAO_EDITAVEL", "AVISO_NAO_PODE_SER_EXCLUIDO", "AVISO_JA_PUBLICADO", "AVISO_NAO_ESTA_ATIVO", "AVISO_AINDA_NAO_PUBLICADO", "HOLERITE_JA_EXISTE", "HOLERITE_VERSAO_DESATUALIZADA", "EMPRESA_NAO_CONFIGURADA",
        "HOLERITE_COMPETENCIA_FUTURA", "HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO", "HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO", "ADVERTENCIA_NAO_ESTA_CONTESTADA", "FUNCIONARIO_INATIVO", "ADVERTENCIA_SEM_GESTOR",
        "ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO", "ADVERTENCIA_NAO_ESTA_PENDENTE",
    ];

    [Fact]
    public void Cada_mensagem_tem_status_titulo_texto_e_codigo_de_um_exemplo_do_contrato()
    {
        var exemplos = Exemplos();

        foreach (var mensagem in MensagensDaComunicacao.Todas.Where(m => m.Codigo != "ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO"))
        {
            var esperado = new Exemplo(mensagem.Status, mensagem.Titulo, mensagem.Detalhe, mensagem.Codigo);
            Assert.True(exemplos.Contains(esperado), $"O contrato não tem um exemplo igual a {mensagem.Codigo}: \"{mensagem.Titulo}\" / \"{mensagem.Detalhe}\".");
        }
    }

    [Fact]
    public void Todo_exemplo_de_erro_da_comunicacao_do_contrato_esta_no_catalogo()
    {
        var catalogo = MensagensDaComunicacao.Todas.Select(m => new Exemplo(m.Status, m.Titulo, m.Detalhe, m.Codigo)).ToHashSet();
        var daComunicacao = Exemplos().Where(e => e.Codigo is not null && CodigosDaComunicacao.Contains(e.Codigo) && !IgnoradoDeOutraFatia(e)).ToList();

        Assert.NotEmpty(daComunicacao);
        Assert.All(daComunicacao, e => Assert.True(catalogo.Contains(e), $"O exemplo {e.Codigo} (\"{e.Detalhe}\") do contrato não está em MensagensDaComunicacao."));
    }

    /// <summary>
    /// <c>FUNCIONARIO_INATIVO</c> e <c>EMPRESA_NAO_CONFIGURADA</c> têm textos de outras fatias (o ponto, a 1c, o pedido de férias da 7): são do catálogo delas. <c>HOLERITE_JA_EXISTE</c> tem o texto montado com o tipo e o mês: o catálogo guarda o do exemplo
    /// (mensal, outubro de 2026) e a montagem é testada à parte.
    /// </summary>
    private static bool IgnoradoDeOutraFatia(Exemplo e) =>
        (e.Codigo == "FUNCIONARIO_INATIVO" && !e.Detalhe.StartsWith("Não é possível registrar advertência", StringComparison.Ordinal))
        || (e.Codigo == "EMPRESA_NAO_CONFIGURADA" && !e.Detalhe.StartsWith("A empresa ainda não tem o dia do pagamento", StringComparison.Ordinal));

    [Fact]
    public void Os_codigos_do_catalogo_sao_exatamente_os_do_contrato_e_nenhum_texto_se_repete()
    {
        Assert.Equal(CodigosDaComunicacao.Order(), MensagensDaComunicacao.Todas.Select(m => m.Codigo).Distinct().Order());
        Assert.Equal(MensagensDaComunicacao.Todas.Count, MensagensDaComunicacao.Todas.Select(m => (m.Codigo, m.Detalhe)).Distinct().Count());
    }

    [Fact]
    public void ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO_o_contrato_cita_o_codigo_mas_nao_tem_exemplo_e_o_texto_e_o_do_plano()
    {
        Assert.DoesNotContain(Exemplos(), e => e.Codigo == "ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO");
        Assert.Contains("ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO", CarregadorDeContrato.Contrato().ToJsonString());
        Assert.Equal((422, "Operação recusada.", "O gestor não pode aplicar uma advertência a si mesmo."), (MensagensDaComunicacao.AdvertenciaGestorAdvertePorSiMesmo.Status, MensagensDaComunicacao.AdvertenciaGestorAdvertePorSiMesmo.Titulo, MensagensDaComunicacao.AdvertenciaGestorAdvertePorSiMesmo.Detalhe));
    }

    [Fact]
    public void O_aviso_que_nao_esta_ativo_tem_um_texto_para_encerrar_e_outro_para_lembrar_com_o_mesmo_codigo()
    {
        Assert.Equal(("AVISO_NAO_ESTA_ATIVO", "Só um aviso ativo pode ser encerrado."), (MensagensDaComunicacao.AvisoNaoEstaAtivoParaEncerrar.Codigo, MensagensDaComunicacao.AvisoNaoEstaAtivoParaEncerrar.Detalhe));
        Assert.Equal(("AVISO_NAO_ESTA_ATIVO", "Só é possível lembrar sobre um aviso ativo."), (MensagensDaComunicacao.AvisoNaoEstaAtivoParaLembrar.Codigo, MensagensDaComunicacao.AvisoNaoEstaAtivoParaLembrar.Detalhe));
    }

    [Fact]
    public void O_holerite_que_ja_existe_diz_o_tipo_e_o_mes_por_extenso_e_o_lote_nao_leva_a_sugestao()
    {
        Assert.Equal("Já existe um holerite mensal de outubro de 2026 para esta pessoa. Para corrigir, envie uma nova versão.", MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Mensal, 10, 2026).Detalhe); // o exemplo do contrato
        Assert.Equal("Já existe um holerite do 13º salário de dezembro de 2026 para esta pessoa. Para corrigir, envie uma nova versão.", MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.DecimoTerceiro, 12, 2026).Detalhe);
        Assert.Equal("Já existe um holerite de férias de março de 2027 para esta pessoa. Para corrigir, envie uma nova versão.", MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Ferias, 3, 2027).Detalhe);
        Assert.Equal("Já existe um holerite mensal de outubro de 2026 para esta pessoa.", MensagensDaComunicacao.HoleriteJaExisteNoLote(TipoDeHolerite.Mensal, 10, 2026)); // yaml: mensagemDoErro do lote
        Assert.Equal(("HOLERITE_JA_EXISTE", 409, "Conflito."), (MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Mensal, 10, 2026).Codigo, MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Mensal, 10, 2026).Status, MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Mensal, 10, 2026).Titulo));
    }

    [Theory]
    [InlineData(1, "janeiro")]
    [InlineData(2, "fevereiro")]
    [InlineData(3, "março")]
    [InlineData(4, "abril")]
    [InlineData(5, "maio")]
    [InlineData(6, "junho")]
    [InlineData(7, "julho")]
    [InlineData(8, "agosto")]
    [InlineData(9, "setembro")]
    [InlineData(10, "outubro")]
    [InlineData(11, "novembro")]
    [InlineData(12, "dezembro")]
    public void O_mes_sai_por_extenso_em_portugues(int mes, string nome)
    {
        Assert.Contains($"holerite mensal de {nome} de 2026", MensagensDaComunicacao.HoleriteJaExiste(TipoDeHolerite.Mensal, mes, 2026).Detalhe, StringComparison.Ordinal);
    }

    [Fact]
    public void ComoProblema_leva_status_titulo_texto_codigo_e_a_regra_do_banco_so_quando_vem()
    {
        var semRegra = MensagensDaComunicacao.AvisoJaPublicado.ComoProblema();
        var comRegra = MensagensDaComunicacao.HoleriteCompetenciaFutura.ComoProblema(RegrasDoBancoDaComunicacao.HoleriteCompetenciaFutura);

        Assert.Equal((422, "Operação recusada.", "Este aviso já foi publicado.", "AVISO_JA_PUBLICADO"), (semRegra.Status, semRegra.Titulo, semRegra.Detalhe, semRegra.Codigo));
        Assert.Null(semRegra.Extensoes);
        Assert.Equal("Nao e possivel cadastrar holerite de competencia futura.", comRegra.Extensoes!["regraDoBanco"]);
    }

    [Fact]
    public void As_regras_do_banco_sao_as_mensagens_originais_dos_triggers_sem_acento()
    {
        Assert.Equal("A competencia do holerite e anterior a admissao.", RegrasDoBancoDaComunicacao.HoleriteAnteriorAAdmissao);
        Assert.Equal("A competencia do holerite e posterior ao desligamento.", RegrasDoBancoDaComunicacao.HoleritePosteriorAoDesligamento);
        Assert.Equal("Nao e possivel cadastrar holerite de competencia futura.", RegrasDoBancoDaComunicacao.HoleriteCompetenciaFutura);
        Assert.Equal("Nao e possivel registrar advertencia para funcionario inativo.", RegrasDoBancoDaComunicacao.AdvertenciaDeInativo);
        Assert.Equal("O gestor nao pode advertir a si mesmo.", RegrasDoBancoDaComunicacao.GestorAdvertePorSiMesmo);
        Assert.Equal("A expiracao do aviso nao pode ser anterior a publicacao.", RegrasDoBancoDaComunicacao.AvisoComExpiracaoAnterior);
    }
}
