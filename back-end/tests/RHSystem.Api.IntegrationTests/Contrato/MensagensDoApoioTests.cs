using System.Text.Json.Nodes;
using System.Text.RegularExpressions;
using RHSystem.Application.Cadastros;

namespace RHSystem.Api.IntegrationTests.Contrato;

/// <summary>
/// Os textos e os códigos de erro do apoio vêm dos EXEMPLOS do contrato (<c>example</c> e <c>examples</c> das respostas), copiados e nunca reescritos: o front pode mostrar o <c>detail</c> como está. Este teste lê o
/// contrato de verdade e compara cada mensagem, trocando os nomes que o exemplo usa (Carlos Ribeiro, Comercial, Ana Souza).
/// </summary>
public class MensagensDoApoioTests
{
    /// <summary>Todo objeto do contrato que tem <c>codigo</c> e <c>detail</c> em texto (os exemplos de erro), em qualquer profundidade: código para os textos que ele tem.</summary>
    private static Dictionary<string, List<string>> ExemplosDeErro()
    {
        var achados = new Dictionary<string, List<string>>(StringComparer.Ordinal);

        void Varrer(JsonNode? no)
        {
            switch (no)
            {
                case JsonObject objeto:
                    if (objeto["codigo"]?.GetValueKind() == System.Text.Json.JsonValueKind.String && objeto["detail"]?.GetValueKind() == System.Text.Json.JsonValueKind.String)
                    {
                        var codigo = objeto["codigo"]!.GetValue<string>();
                        if (!achados.TryGetValue(codigo, out var textos))
                        {
                            achados[codigo] = textos = [];
                        }

                        textos.Add(objeto["detail"]!.GetValue<string>());
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

    private static void ConfereTexto(Dictionary<string, List<string>> exemplos, MensagemDoApoio mensagem)
    {
        Assert.True(exemplos.TryGetValue(mensagem.Codigo, out var textos), $"O contrato não tem nenhum exemplo com o código {mensagem.Codigo}.");
        Assert.Contains(mensagem.Detalhe, textos!); // o texto do código, igualzinho ao de algum exemplo
    }

    [Fact]
    public void Textos_e_codigos_de_MensagensDoApoio_sao_os_dos_exemplos_do_contrato()
    {
        var exemplos = ExemplosDeErro();

        // Sem parâmetro: o texto igual ao do exemplo.
        ConfereTexto(exemplos, MensagensDoApoio.NomeJaCadastrado);
        ConfereTexto(exemplos, MensagensDoApoio.GestorJaCadastrado);
        ConfereTexto(exemplos, MensagensDoApoio.GestorSemSetorSoParaRh);
        ConfereTexto(exemplos, MensagensDoApoio.FuncionarioInativo);
        ConfereTexto(exemplos, MensagensDoApoio.CadastroEmUso);
        ConfereTexto(exemplos, MensagensDoApoio.EmpresaNaoConfigurada);
        ConfereTexto(exemplos, MensagensDoApoio.DocumentoSemArquivo);
        ConfereTexto(exemplos, MensagensDoApoio.DocumentoNaoPedido);

        // Com parâmetro: os nomes dos exemplos do contrato entram no lugar dos marcadores.
        ConfereTexto(exemplos, MensagensDoApoio.GestorJaRespondePorOutroSetor("Carlos Ribeiro", "Comercial"));
        ConfereTexto(exemplos, MensagensDoApoio.GestorPrecisaTerPerfilGestor("Ana Souza"));
        var semEquipe = exemplos["GESTOR_SEM_EQUIPE"].Single();
        var nome = Regex.Match(semEquipe, @"^(?<nome>.+?) só decide pedidos de gestores").Groups["nome"].Value;
        Assert.NotEmpty(nome);
        ConfereTexto(exemplos, MensagensDoApoio.GestorSemEquipe(nome)); // o nome do exemplo, seja qual for, no texto do contrato
    }

    [Fact]
    public void Cada_mensagem_traz_o_codigo_exato_do_contrato()
    {
        (string Esperado, string Real)[] pares =
        [
            ("NOME_JA_CADASTRADO", MensagensDoApoio.NomeJaCadastrado.Codigo),
            ("GESTOR_JA_CADASTRADO", MensagensDoApoio.GestorJaCadastrado.Codigo),
            ("GESTOR_SEM_SETOR_SO_PARA_RH", MensagensDoApoio.GestorSemSetorSoParaRh.Codigo),
            ("GESTOR_JA_RESPONDE_POR_OUTRO_SETOR", MensagensDoApoio.GestorJaRespondePorOutroSetor("a", "b").Codigo),
            ("GESTOR_PRECISA_TER_PERFIL_GESTOR", MensagensDoApoio.GestorPrecisaTerPerfilGestor("a").Codigo),
            ("GESTOR_SEM_EQUIPE", MensagensDoApoio.GestorSemEquipe("a").Codigo),
            ("FUNCIONARIO_INATIVO", MensagensDoApoio.FuncionarioInativo.Codigo),
            ("CADASTRO_EM_USO", MensagensDoApoio.CadastroEmUso.Codigo),
            ("EMPRESA_NAO_CONFIGURADA", MensagensDoApoio.EmpresaNaoConfigurada.Codigo),
            ("DOCUMENTO_SEM_ARQUIVO", MensagensDoApoio.DocumentoSemArquivo.Codigo),
            ("DOCUMENTO_NAO_PEDIDO", MensagensDoApoio.DocumentoNaoPedido.Codigo),
        ];

        Assert.All(pares, par => Assert.Equal(par.Esperado, par.Real));
        Assert.Equal(pares.Length, pares.Select(p => p.Real).Distinct().Count()); // nenhum código repetido entre as mensagens
    }
}
