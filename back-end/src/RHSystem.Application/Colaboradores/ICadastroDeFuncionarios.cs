namespace RHSystem.Application.Colaboradores;

/// <summary>
/// O ÚNICO caminho para criar uma ou 500 pessoas: o cadastro pela tela e a importação por Excel (fatia 2) chamam este. A validação é uma só e serve às duas operações.
/// </summary>
public interface ICadastroDeFuncionarios
{
    /// <summary>
    /// SEM gravar. As MESMAS regras do cadastro, em lote (a prévia da importação as usa): campos, existência de cargo, setor e benefícios, CPF e e-mail repetidos (no banco, com o
    /// <see cref="CadastroExistente"/>; e dentro do lote, com o <c>OutroIndice</c>) e gestor do setor inativo. Os problemas saem por linha, nesta ordem: campos, duplicidades, regras.
    /// </summary>
    Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(IReadOnlyList<NovoFuncionario> novos, CancellationToken ct);

    /// <summary>
    /// <b>Exige transação aberta</b> (como <c>IGeradorDeMatriculas</c>); de 1 a 500; NÃO abre nem confirma transação. Valida tudo (<see cref="CadastroInvalidoException"/>) e confere as caixas
    /// ANTES de gravar; reserva as matrículas em bloco, grava linha por linha (assim o banco aponta a linha: <see cref="LinhaRecusadaException"/>), depois os convites, o onboarding e o formulário.
    /// A importação envolve a chamada com <c>IAuditor.SuspenderRegistroAutomatico()</c>.
    /// </summary>
    Task<ResultadoDoCadastro> CadastrarAsync(IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro opcoes, CancellationToken ct);
}

/// <summary>Quem o gestor de uma pessoa deve ser. <c>GestorId</c> é o de <c>GESTORES</c>; <c>GestorInativo</c> diz que o gestor do setor está inativo (o banco recusaria a gravação).</summary>
public sealed record ResolucaoDoGestor(int? GestorId, bool GestorInativo, string? NomeDoGestor);

/// <summary>
/// O gestor do colaborador é DERIVADO do setor. ESTA FATIA É A DONA desta interface; a fatia 1c só chama <see cref="RefazerDosMembrosAsync"/> (ao criar e editar setor) e não a reimplementa.
/// </summary>
public interface IGestorDoFuncionario
{
    /// <summary>O <c>ID_GESTOR</c> que a pessoa deve ter: o gestor do setor; nulo se o setor não tem gestor OU se o gestor é a própria pessoa (<paramref name="funcionarioId"/>).</summary>
    Task<ResolucaoDoGestor> ResolverAsync(int setorId, int? funcionarioId, CancellationToken ct);

    /// <summary>
    /// Para a fatia 1c: refaz o <c>ID_GESTOR</c> dos membros ATIVOS do setor (o próprio gestor fica nulo); devolve quantos mudaram. 422 <c>GESTOR_DO_SETOR_INATIVO</c> se o gestor novo está inativo. Lê o
    /// gestor do setor NO BANCO: quem chama grava o setor antes (<c>SaveChanges</c>), na mesma transação.
    /// </summary>
    Task<int> RefazerDosMembrosAsync(int setorId, CancellationToken ct);
}

/// <summary>Passo "incluir no onboarding" das caixas "Depois de salvar". Depende da fatia 6; a versão padrão é "indisponível" (decisão 8 do plano) e a fatia dona a substitui.</summary>
public interface IInclusaoNoOnboarding
{
    /// <summary>Sem treinamento em vigor: 422 <c>SEM_TREINAMENTO_DE_ONBOARDING</c>.</summary>
    Task ConferirAsync(CancellationToken ct);

    /// <summary>Quantos foram incluídos (o mesmo efeito de <c>incluirFuncionarioNoOnboarding</c>, fatia 6).</summary>
    Task<int> IncluirAsync(IReadOnlyList<int> funcionarioIds, CancellationToken ct);
}

/// <summary>Passo "enviar o formulário de admissão". Depende da fatia 5; a versão padrão é "indisponível" e a fatia dona a substitui.</summary>
public interface IEnvioDoFormularioDeAdmissao
{
    /// <summary>400 em <c>formularioAdmissaoId</c> (não existe, não é de admissão ou não está publicado); 422 <c>FORMULARIO_NAO_E_POR_PESSOAS</c> ou <c>FORMULARIO_ENCERRADO</c>.</summary>
    Task ConferirAsync(int formularioId, CancellationToken ct);

    /// <summary>Quantos foram adicionados (o mesmo efeito de <c>adicionarDestinatariosFormulario</c>, fatia 5).</summary>
    Task<int> AdicionarDestinatariosAsync(int formularioId, IReadOnlyList<int> funcionarioIds, CancellationToken ct);
}
