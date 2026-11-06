using RHSystem.Application.Acesso;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A parte PURA da validação da edição (sem banco): os campos que só a edição tem. Os outros (nome, CPF, telefone, e-mail corporativo, datas, perfil, benefícios repetidos) são os de
/// <see cref="ValidacaoDeCamposDoCadastro"/>. Os problemas levam o nome do campo do contrato; os do endereço levam <c>endereco.</c> na frente.
/// </summary>
public static class ValidacaoDeCamposDaEdicao
{
    public const int TamanhoMaximoDoEmail = 150;

    public const int TamanhoMaximoDoRg = 20;

    private static readonly HashSet<string> Ufs = new(StringComparer.Ordinal)
    {
        "AC", "AL", "AP", "AM", "BA", "CE", "DF", "ES", "GO", "MA", "MT", "MS", "MG", "PA", "PB", "PR", "PE", "PI", "RJ", "RN", "RS", "RO", "RR", "SC", "SP", "SE", "TO",
    };

    public static IReadOnlyList<ProblemaDeCadastro> Validar(int indice, DadosPessoaisDaEdicao dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        var problemas = new List<ProblemaDeCadastro>();

        void Problema(string campo, string mensagem) =>
            problemas.Add(new ProblemaDeCadastro(indice, TipoDeProblemaDeCadastro.Campo, campo, ValidacaoDeCamposDoCadastro.CodigoDeCampoInvalido, mensagem));

        if (dados.EmailPessoal is { } emailPessoal && !RegrasDeSenha.EmailComFormatoValido(emailPessoal))
        {
            Problema("emailPessoal", $"Informe um e-mail pessoal válido, com até {TamanhoMaximoDoEmail} caracteres.");
        }

        if (dados.Rg is { Length: > TamanhoMaximoDoRg })
        {
            Problema("rg", $"O RG pode ter até {TamanhoMaximoDoRg} caracteres.");
        }

        if (dados.RgOrgaoEmissor is { Length: > TamanhoMaximoDoRg })
        {
            Problema("rgOrgaoEmissor", $"O órgão emissor pode ter até {TamanhoMaximoDoRg} caracteres.");
        }

        if (dados.Endereco is null)
        {
            Problema("endereco", "O endereço é obrigatório (os campos dele podem ser null).");
        }
        else
        {
            if (dados.Endereco.Cep is { } cep && !(cep.Length == 8 && cep.All(c => c is >= '0' and <= '9')))
            {
                Problema("endereco.cep", "O CEP tem 8 dígitos, sem pontuação.");
            }

            if (dados.Endereco.Uf is { } uf && !Ufs.Contains(uf))
            {
                Problema("endereco.uf", "Informe a sigla do estado em maiúsculas (por exemplo SP).");
            }

            foreach (var (campo, rotulo, valor, maximo) in CamposDeTexto(dados.Endereco))
            {
                if (valor is { } texto && texto.Length > maximo)
                {
                    Problema($"endereco.{campo}", $"O {rotulo} pode ter até {maximo} caracteres.");
                }
            }
        }

        if (!dados.BeneficiosInformados)
        {
            Problema("beneficioIds", "Informe os benefícios do colaborador (pode ser uma lista vazia).");
        }

        return problemas;
    }

    /// <summary>Os campos de texto do endereço e o tamanho que o contrato (<c>FuncionarioEndereco</c>) e as colunas do banco aceitam: um a mais seria um erro de gravação (500), e o contrato manda 400.</summary>
    private static IEnumerable<(string Campo, string Rotulo, string? Valor, int Maximo)> CamposDeTexto(FuncionarioEndereco e) =>
    [
        ("logradouro", "logradouro", e.Logradouro, 150),
        ("numero", "número", e.Numero, 10),
        ("complemento", "complemento", e.Complemento, 100),
        ("bairro", "bairro", e.Bairro, 100),
        ("cidade", "cidade", e.Cidade, 100),
    ];
}
