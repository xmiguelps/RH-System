using System.Text.Json;
using System.Text.Json.Nodes;
using RHSystem.Application.Treinamentos.Mensagens;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro dos treinamentos vêm dos EXEMPLOS do contrato (<c>60-fatia-6-tad.yaml</c>), copiados e nunca reescritos (o front mostra o <c>detail</c> como vem). Este teste lê o contrato de verdade e confere o status, o título, o texto e o código de cada mensagem; os textos com número (os dois 409 e o
/// 422 de certificados) se conferem com os números do exemplo. As exceções declaradas são as que o contrato cita e não exemplifica (o executor escreve o texto).
/// </summary>
public class MensagensDosTreinamentosTests
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

    /// <summary>Os textos que o contrato NÃO traz em exemplo (o código é citado na descrição, mas sem <c>detail</c>), escritos pelo executor.</summary>
    private static readonly string[] SemExemplo = [];

    [Fact]
    public void Cada_mensagem_de_texto_fixo_tem_status_titulo_texto_e_codigo_de_um_exemplo_do_contrato()
    {
        var exemplos = Exemplos();

        foreach (var mensagem in MensagensDosTreinamentos.Todas.Where(m => !SemExemplo.Contains(m.Detalhe)))
        {
            var esperado = new Exemplo(mensagem.Status, mensagem.Titulo, mensagem.Detalhe, mensagem.Codigo);
            Assert.True(exemplos.Contains(esperado), $"O contrato não tem um exemplo igual a {mensagem.Codigo}: \"{mensagem.Titulo}\" / \"{mensagem.Detalhe}\".");
        }
    }

    [Fact]
    public void Os_textos_com_numero_saem_iguais_aos_exemplos_do_contrato_e_o_singular_e_o_plural_seguem_o_numero()
    {
        var exemplos = Exemplos();
        var encerrar = MensagensDosTreinamentos.ConfirmacaoNecessaria(21, 17, AcaoQueExigeConfirmacao.Encerrar);
        var excluir = MensagensDosTreinamentos.ConfirmacaoNecessaria(4, 5, AcaoQueExigeConfirmacao.Excluir);
        var certificados = MensagensDosTreinamentos.ComCertificados(63);

        Assert.Contains(new Exemplo(409, "Confirmação necessária.", "38 pessoas ainda não concluíram este treinamento. Confirme para encerrar.", "CONFIRMACAO_NECESSARIA"), exemplos);
        Assert.Contains(new Exemplo(409, "Confirmação necessária.", "9 pessoas ainda não concluíram este treinamento. Confirme para excluir.", "CONFIRMACAO_NECESSARIA"), exemplos);
        Assert.Contains(new Exemplo(422, "Operação recusada.", "Este treinamento já emitiu 63 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.", "TREINAMENTO_COM_CERTIFICADOS"), exemplos);
        Assert.Equal(("38 pessoas ainda não concluíram este treinamento. Confirme para encerrar.", 38, 21, 17), (encerrar.Detalhe, encerrar.Extensoes!["pendentes"], encerrar.Extensoes["emAndamento"], encerrar.Extensoes["naoIniciaram"]));
        Assert.Equal(("9 pessoas ainda não concluíram este treinamento. Confirme para excluir.", 9), (excluir.Detalhe, excluir.Extensoes!["pendentes"]));
        Assert.Equal(("Este treinamento já emitiu 63 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.", 63), (certificados.Detalhe, certificados.Extensoes!["certificadosEmitidos"]));
        Assert.Equal("1 pessoa ainda não concluiu este treinamento. Confirme para encerrar.", MensagensDosTreinamentos.ConfirmacaoNecessaria(1, 0, AcaoQueExigeConfirmacao.Encerrar).Detalhe); // sem exemplo no contrato: texto do executor
        Assert.Equal("Este treinamento já emitiu 1 certificado e não pode ser excluído. Encerre o treinamento para que ninguém novo entre.", MensagensDosTreinamentos.ComCertificados(1).Detalhe); // idem
    }

    [Fact]
    public void O_404_da_conferencia_e_o_formulario_de_inativo_saem_como_o_contrato_mostra()
    {
        var exemplos = Exemplos();
        var naoEncontrado = MensagensDosTreinamentos.CertificadoNaoEncontrado;
        var inativo = MensagensDosTreinamentos.FuncionarioInativo();

        Assert.Contains(new Exemplo(404, "Certificado não encontrado.", "Não encontramos um certificado com este código.", null), exemplos);
        Assert.Equal((404, "Certificado não encontrado.", "Não encontramos um certificado com este código.", (string?)null), (naoEncontrado.Status, naoEncontrado.Titulo, naoEncontrado.Detalhe, naoEncontrado.Codigo));
        Assert.Equal(("FUNCIONARIO_INATIVO", "Não é possível incluir uma pessoa inativa em treinamento.", "Nao e possivel inscrever funcionario inativo em treinamento."), (inativo.Codigo, inativo.Detalhe, inativo.Extensoes!["regraDoBanco"]));
        Assert.Contains(exemplos, e => e.Codigo == "FUNCIONARIO_INATIVO" && e.Detalhe == inativo.Detalhe);
    }

    [Fact]
    public void Os_codigos_do_catalogo_sao_exatamente_os_da_fatia_e_nenhum_texto_se_repete()
    {
        string[] esperados =
        [
            "TREINAMENTO_JA_PUBLICADO", "TREINAMENTO_CAMPO_TRAVADO", "TREINAMENTO_MODULOS_TRAVADOS", "TREINAMENTO_ENCERRADO", "TREINAMENTO_NAO_ESTA_EM_ANDAMENTO", "TREINAMENTO_NAO_E_OPCIONAL", "INSCRICAO_PENDENTE", "SEM_TREINAMENTO_DE_ONBOARDING",
        ];

        Assert.Equal(esperados.Order(), MensagensDosTreinamentos.Todas.Select(m => m.Codigo!).Distinct().Order());
        Assert.Equal(MensagensDosTreinamentos.Todas.Count, MensagensDosTreinamentos.Todas.Select(m => (m.Codigo, m.Detalhe)).Distinct().Count());
    }
}
