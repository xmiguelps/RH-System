using RHSystem.Application.Acesso;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// A parte PURA da validação do cadastro (sem banco): os campos de uma linha. Todos os problemas de campo da linha voltam juntos, com o nome do campo em camelCase. A existência de cargo, setor e
/// benefícios, as duplicidades e o gestor do setor são conferidos pelo serviço, que consulta o banco.
/// </summary>
public static class ValidacaoDeCamposDoCadastro
{
    public const string CodigoDeCampoInvalido = "CAMPO_INVALIDO";

    public const int TamanhoMaximoDoNome = 150;

    public static IReadOnlyList<ProblemaDeCadastro> Validar(int indice, NovoFuncionario novo, DateOnly hoje)
    {
        var problemas = new List<ProblemaDeCadastro>();

        void Problema(string campo, string mensagem) =>
            problemas.Add(new ProblemaDeCadastro(indice, TipoDeProblemaDeCadastro.Campo, campo, CodigoDeCampoInvalido, mensagem));

        var nome = novo.Nome?.Trim() ?? string.Empty;
        if (nome.Length == 0)
        {
            Problema("nome", "O nome é obrigatório.");
        }
        else if (nome.Length > TamanhoMaximoDoNome)
        {
            Problema("nome", $"O nome pode ter até {TamanhoMaximoDoNome} caracteres.");
        }

        if (!Cpf.EhValido(novo.Cpf ?? string.Empty))
        {
            Problema("cpf", "CPF inválido.");
        }

        if (novo.DataNascimento == DateOnly.MinValue)
        {
            Problema("dataNascimento", "A data de nascimento é obrigatória.");
        }
        else if (novo.DataNascimento > hoje)
        {
            Problema("dataNascimento", "A data de nascimento não pode ser no futuro.");
        }

        var telefone = novo.Telefone ?? string.Empty;
        if (telefone.Length is not (10 or 11) || telefone.Any(c => c is < '0' or > '9'))
        {
            Problema("telefone", "Informe o telefone com DDD.");
        }

        var email = novo.EmailCorporativo?.Trim() ?? string.Empty;
        if (email.Length == 0)
        {
            Problema("emailCorporativo", "O e-mail corporativo é obrigatório.");
        }
        else if (!RegrasDeSenha.EmailComFormatoValido(email))
        {
            Problema("emailCorporativo", "Informe um e-mail corporativo válido, com até 150 caracteres.");
        }

        if (novo.DataAdmissao == DateOnly.MinValue)
        {
            Problema("dataAdmissao", "A data de admissão é obrigatória.");
        }

        if (!Enum.IsDefined(novo.TipoContrato))
        {
            Problema("tipoContrato", "Informe o tipo de contrato.");
        }

        if (!Enum.IsDefined(novo.Perfil))
        {
            Problema("perfil", "O perfil é inválido.");
        }

        if (novo.BeneficioIds?.Distinct().Count() != novo.BeneficioIds?.Count)
        {
            Problema("beneficioIds", "Há benefícios repetidos.");
        }

        return problemas;
    }
}
