using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// O corpo de <c>readmitirFuncionario</c> (<c>FuncionarioReadmitirComando</c>): o vínculo novo de quem está inativo. O CPF, o e-mail, a matrícula e os dados pessoais continuam os do cadastro; o nível de acesso, os
/// benefícios e as férias não vêm no corpo. Os três últimos são opcionais, com o padrão do contrato (<c>false</c>, <c>null</c>, <c>false</c>).
/// </summary>
public sealed record FuncionarioReadmitirComando(
    DateOnly DataAdmissao,
    int CargoId,
    int SetorId,
    TipoDeContrato TipoContrato,
    bool IncluirNoOnboarding = false,
    int? FormularioAdmissaoId = null,
    bool EnviarConvite = false);

/// <summary>O que a readmissão recebe, já sem o que é só do JSON. As caixas valem como no cadastro: mesmo efeito, mesmos erros, nenhum e-mail por elas (o único e-mail é o convite).</summary>
public sealed record DadosDaReadmissao(DateOnly DataAdmissao, int CargoId, int SetorId, TipoDeContrato TipoContrato, bool IncluirNoOnboarding, int? FormularioAdmissaoId, bool EnviarConvite);

/// <summary>
/// A nova admissão de quem está inativo, sobre o mesmo cadastro (mesmo id, CPF, e-mail e matrícula). <b>Abre a própria transação e é tudo ou nada.</b> 404 (a pessoa não existe); 409 <c>FUNCIONARIO_JA_ATIVO</c> (quem está
/// ativo, inclusive com desligamento só agendado); 400 em <c>dataAdmissao</c> (anterior ao último dia de trabalho do vínculo anterior), <c>cargoId</c>, <c>setorId</c>, <c>tipoContrato</c> ou <c>formularioAdmissaoId</c>;
/// 422 <c>GESTOR_DO_SETOR_INATIVO</c>, <c>SEM_TREINAMENTO_DE_ONBOARDING</c> (e os do formulário) e <c>SENHA_JA_DEFINIDA</c> (convite para quem já definiu a senha).
/// </summary>
public interface IReadmissaoDeFuncionarios
{
    Task ReadmitirAsync(int funcionarioId, DadosDaReadmissao dados, CancellationToken ct);
}
