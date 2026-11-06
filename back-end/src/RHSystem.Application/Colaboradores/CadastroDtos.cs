using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// Um colaborador a cadastrar. CPF e telefone SÓ com dígitos (quem chama usa <see cref="Cpf.Normalizar"/> e <see cref="TelefoneMascarado.SoDigitos"/>). A matrícula não vem: é gerada.
/// A importação por Excel (fatia 2) e a edição (Task 8b) reaproveitam este tipo.
/// </summary>
public sealed record NovoFuncionario(
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    string Telefone,
    string EmailCorporativo,
    DateOnly DataAdmissao,
    TipoDeContrato TipoContrato,
    int CargoId,
    int SetorId,
    Perfil Perfil,
    IReadOnlyList<int> BeneficioIds);

/// <summary>As caixas "Depois de salvar" da tela 02: <c>EnviarConvite</c> (padrão <c>true</c>), incluir no onboarding e o id do formulário de admissão (nulo = não envia nada).</summary>
public sealed record OpcoesDoCadastro(bool EnviarConvite, bool IncluirNoOnboarding, int? FormularioAdmissaoId);

/// <summary>Qual resposta o problema vira: <c>Campo</c> = 400, <c>Duplicado</c> = 409, <c>Regra</c> = 422.</summary>
public enum TipoDeProblemaDeCadastro
{
    Campo,
    Duplicado,
    Regra,
}

public sealed record CadastroExistente(int FuncionarioId, string Nome, bool Ativo);

/// <summary>
/// Um problema de uma linha (<paramref name="Indice"/>, de 0). <paramref name="Campo"/> é o nome do campo do corpo (camelCase). <paramref name="Existente"/> é o cadastro que já usa o CPF ou o
/// e-mail; <paramref name="OutroIndice"/> é a outra linha do mesmo lote que repete o dado.
/// </summary>
public sealed record ProblemaDeCadastro(
    int Indice,
    TipoDeProblemaDeCadastro Tipo,
    string Campo,
    string Codigo,
    string Mensagem,
    CadastroExistente? Existente = null,
    int? OutroIndice = null);

public sealed record FuncionarioCadastrado(int Id, string Matricula);

/// <summary><c>ConvitesEnviados</c> conta os e-mails colocados na FILA (o envio é do serviço em segundo plano).</summary>
public sealed record ResultadoDoCadastro(IReadOnlyList<FuncionarioCadastrado> Cadastrados, int ConvitesEnviados, int IncluidosNoOnboarding, int IncluidosNoFormularioDeAdmissao);

/// <summary>A validação achou problemas: NADA foi gravado.</summary>
public sealed class CadastroInvalidoException(IReadOnlyList<ProblemaDeCadastro> problemas) : Exception("O cadastro tem problemas.")
{
    public IReadOnlyList<ProblemaDeCadastro> Problemas { get; } = problemas;
}

/// <summary>O banco recusou a linha <see cref="Indice"/> durante a gravação (duplicidade ou regra de trigger). A transação do chamador deve desfazer tudo.</summary>
public sealed class LinhaRecusadaException(int indice, bool porDuplicidade, string? regraDoBanco) : Exception("O banco recusou uma linha do cadastro.")
{
    public int Indice { get; } = indice;

    public bool PorDuplicidade { get; } = porDuplicidade;

    public string? RegraDoBanco { get; } = regraDoBanco;
}

/// <summary>O corpo de <c>criarFuncionario</c> (<c>FuncionarioCriarComando</c>). Os opcionais têm o padrão do contrato: <c>beneficioIds</c> vazio, <c>enviarConvite</c> verdadeiro, <c>incluirNoOnboarding</c> falso.</summary>
public sealed record FuncionarioCriarComando(
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    string Telefone,
    string EmailCorporativo,
    DateOnly DataAdmissao,
    TipoDeContrato TipoContrato,
    int CargoId,
    int SetorId,
    Perfil Perfil,
    bool EnviarConvite = true,
    bool IncluirNoOnboarding = false,
    int? FormularioAdmissaoId = null)
{
    /// <summary>Propriedade à parte (e não parâmetro do construtor) para ter o padrão <c>[]</c> do contrato sem ser anulável no documento gerado: o contrato não aceita <c>null</c> aqui.</summary>
    public IReadOnlyList<int> BeneficioIds { get; init; } = [];
}

/// <summary>
/// O corpo de <c>editarFuncionario</c> (<c>FuncionarioEditarComando</c>): a substituição COMPLETA dos campos editáveis. Tudo vem sempre; os que podem ficar vazios aceitam <c>null</c>.
/// <c>Endereco</c> e <c>BeneficioIds</c> são obrigatórios no contrato: se faltarem (o System.Text.Json não impõe o <c>required</c>), a validação devolve 400 em vez de apagar o endereço ou todos os benefícios.
/// </summary>
public sealed record FuncionarioEditarComando(
    string Nome,
    string Cpf,
    DateOnly DataNascimento,
    string Telefone,
    string EmailCorporativo,
    string? EmailPessoal,
    string? Rg,
    string? RgOrgaoEmissor,
    FuncionarioEndereco Endereco,
    DateOnly DataAdmissao,
    TipoDeContrato TipoContrato,
    int CargoId,
    int SetorId,
    Perfil Perfil,
    IReadOnlyList<int> BeneficioIds);

/// <summary>
/// O que só a edição tem além de <see cref="NovoFuncionario"/>: e-mail pessoal, RG, órgão emissor e endereço (o cadastro não os recebe). <paramref name="Endereco"/> nulo e
/// <paramref name="BeneficiosInformados"/> falso são campos obrigatórios que o corpo não trouxe.
/// </summary>
public sealed record DadosPessoaisDaEdicao(string? EmailPessoal, string? Rg, string? RgOrgaoEmissor, FuncionarioEndereco? Endereco, bool BeneficiosInformados = true);

/// <summary>O corpo de <c>inativarFuncionario</c>: o último dia de trabalho (hoje, futuro ou passado, nunca antes da admissão). A API não recebe a palavra "desativar" da tela: a confirmação é só da interface.</summary>
public sealed record FuncionarioInativarComando(DateOnly UltimoDiaDeTrabalho);
