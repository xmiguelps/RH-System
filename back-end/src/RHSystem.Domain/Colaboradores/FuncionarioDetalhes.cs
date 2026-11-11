using RHSystem.Domain.Autenticacao;

namespace RHSystem.Domain.Colaboradores;

public enum SituacaoCadastral
{
    Ativo,
    EmAdmissao,
    Inativo,
}

/// <summary>Os dados pessoais de uma pessoa (CPF sempre só com dígitos). A tela de cadastro e a de edição mandam e recebem este conjunto.</summary>
public sealed record DadosPessoais(
    string Cpf,
    string? Telefone,
    string? EmailPessoal,
    string? Rg,
    string? RgOrgaoEmissor,
    string? Cep,
    string? Logradouro,
    string? Numero,
    string? Complemento,
    string? Bairro,
    string? Cidade,
    string? Uf);

/// <summary>
/// Uma linha de <c>FUNCIONARIOS_DETALHES</c> (um para um com <see cref="Funcionario"/>). <b>Não é <c>[Auditado]</c> de propósito</b>: o log de
/// dados pessoais sai pelo caso de uso (CPF, RG, e-mail pessoal, telefone e endereço com <c>valorOculto</c>), nunca o valor.
/// </summary>
public class FuncionarioDetalhes
{
    private FuncionarioDetalhes()
    {
    }

    public int Id { get; private set; }

    public int FuncionarioId { get; private set; }

    public Funcionario? Funcionario { get; private set; }

    public string Cpf { get; private set; } = string.Empty;

    public string? Telefone { get; private set; }

    public string? EmailPessoal { get; private set; }

    public string? Rg { get; private set; }

    public string? RgOrgaoEmissor { get; private set; }

    public string? Cep { get; private set; }

    public string? Logradouro { get; private set; }

    public string? Numero { get; private set; }

    public string? Complemento { get; private set; }

    public string? Bairro { get; private set; }

    public string? Cidade { get; private set; }

    public string? Uf { get; private set; }

    public static FuncionarioDetalhes Criar(Funcionario funcionario, DadosPessoais dados)
    {
        ArgumentNullException.ThrowIfNull(funcionario);
        var detalhes = new FuncionarioDetalhes { Funcionario = funcionario, FuncionarioId = funcionario.Id };
        detalhes.Atualizar(dados);
        return detalhes;
    }

    public void Atualizar(DadosPessoais dados)
    {
        ArgumentNullException.ThrowIfNull(dados);
        Cpf = dados.Cpf;
        Telefone = dados.Telefone;
        EmailPessoal = dados.EmailPessoal;
        Rg = dados.Rg;
        RgOrgaoEmissor = dados.RgOrgaoEmissor;
        Cep = dados.Cep;
        Logradouro = dados.Logradouro;
        Numero = dados.Numero;
        Complemento = dados.Complemento;
        Bairro = dados.Bairro;
        Cidade = dados.Cidade;
        Uf = dados.Uf;
    }

    public DadosPessoais ParaDados() =>
        new(Cpf, Telefone, EmailPessoal, Rg, RgOrgaoEmissor, Cep, Logradouro, Numero, Complemento, Bairro, Cidade, Uf);
}
