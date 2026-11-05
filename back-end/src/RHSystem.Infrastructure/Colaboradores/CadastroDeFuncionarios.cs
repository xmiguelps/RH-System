using System.Globalization;
using System.Text;
using Microsoft.EntityFrameworkCore;
using MySqlConnector;
using RHSystem.Application.Acesso;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// O caminho único de criação (uma ou 500 pessoas). A validação é UMA só e serve à prévia (<see cref="ValidarAsync"/>) e à gravação (<see cref="CadastrarAsync"/>): campos (a parte pura, em
/// <see cref="ValidacaoDeCamposDoCadastro"/>), existência de cargo, setor e benefícios, CPF e e-mail repetidos (no banco e dentro do lote) e gestor do setor inativo.
/// <para>
/// A gravação roda DENTRO da transação do chamador: valida tudo e confere as caixas antes de gravar; reserva as matrículas em bloco (um <c>UPDATE</c> no contador, que serializa dois cadastros e
/// devolve o número se a transação desfaz: sem buraco); grava linha por linha (o banco aponta a linha que recusou); e só então convites, onboarding e formulário. Falhou qualquer passo, a
/// transação do chamador desfaz o colaborador, os dados pessoais, os benefícios, o login, o link e o e-mail.
/// </para>
/// </summary>
public sealed class CadastroDeFuncionarios(
    AppDbContext db,
    IRelogio relogio,
    IGeradorDeMatriculas matriculas,
    IServicoDeConvite convites,
    IGestorDoFuncionario gestores,
    IInclusaoNoOnboarding onboarding,
    IEnvioDoFormularioDeAdmissao formulario) : ICadastroDeFuncionarios
{
    private const int MaximoPorChamada = 500;

    public async Task<IReadOnlyList<ProblemaDeCadastro>> ValidarAsync(IReadOnlyList<NovoFuncionario> novos, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(novos);
        if (novos.Count > MaximoPorChamada)
        {
            throw new ArgumentOutOfRangeException(nameof(novos), novos.Count, $"Um cadastro vale até {MaximoPorChamada} pessoas por chamada.");
        }

        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var problemas = new List<ProblemaDeCadastro>();
        for (var i = 0; i < novos.Count; i++)
        {
            problemas.AddRange(ValidacaoDeCamposDoCadastro.Validar(i, novos[i], hoje));
        }

        await ConferirExistenciaAsync(novos, problemas, ct);
        await ConferirDuplicidadeNoBancoAsync(novos, problemas, semOFuncionario: null, ct);
        ConferirDuplicidadeNoLote(novos, problemas);
        await ConferirGestoresAsync(novos, problemas, ct);

        // Por linha, e dentro dela: campos (400), duplicidades (409), regras (422). O sort é estável: o CPF continua antes do e-mail.
        return [.. problemas.OrderBy(p => p.Indice).ThenBy(p => p.Tipo)];
    }

    public async Task<ResultadoDoCadastro> CadastrarAsync(IReadOnlyList<NovoFuncionario> novos, OpcoesDoCadastro opcoes, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(novos);
        ArgumentNullException.ThrowIfNull(opcoes);
        if (novos.Count is < 1 or > MaximoPorChamada)
        {
            throw new ArgumentOutOfRangeException(nameof(novos), novos.Count, $"Um cadastro vale de 1 a {MaximoPorChamada} pessoas por chamada.");
        }

        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Cadastrar exige uma transação aberta (a reserva das matrículas e o tudo-ou-nada só são seguros dentro dela).");
        }

        var problemas = await ValidarAsync(novos, ct);
        if (problemas.Count > 0)
        {
            throw new CadastroInvalidoException(problemas);
        }

        // As caixas são conferidas ANTES de gravar (decisão 8): sem fonte, elas recusam com o erro do contrato e nada fica gravado.
        if (opcoes.IncluirNoOnboarding)
        {
            await onboarding.ConferirAsync(ct);
        }

        if (opcoes.FormularioAdmissaoId is { } formularioId)
        {
            await formulario.ConferirAsync(formularioId, ct);
        }

        var numeros = await matriculas.ReservarAsync(novos.Count, ct);
        var niveis = await db.NiveisDeAcesso.ToDictionaryAsync(n => n.Nome, ct);
        var gestorPorSetor = new Dictionary<int, int?>();
        var cadastrados = new List<FuncionarioCadastrado>(novos.Count);

        for (var i = 0; i < novos.Count; i++)
        {
            var novo = novos[i];
            if (!gestorPorSetor.TryGetValue(novo.SetorId, out var gestorId))
            {
                gestorId = (await gestores.ResolverAsync(novo.SetorId, null, ct)).GestorId;
                gestorPorSetor[novo.SetorId] = gestorId;
            }

            var nivel = niveis[NomesDeNivel.DoPerfil(novo.Perfil)];
            var funcionario = Funcionario.Criar(novo.Nome, novo.EmailCorporativo, novo.DataAdmissao, nivel);
            funcionario.AtualizarCadastro(novo.Nome, novo.EmailCorporativo, novo.DataNascimento, novo.DataAdmissao, novo.TipoContrato, novo.CargoId, novo.SetorId, gestorId, nivel);
            funcionario.AtribuirMatricula(numeros[i]);
            db.Funcionarios.Add(funcionario);
            db.DetalhesDosFuncionarios.Add(FuncionarioDetalhes.Criar(funcionario, new DadosPessoais(novo.Cpf, novo.Telefone, null, null, null, null, null, null, null, null, null, null)));
            await SalvarLinhaAsync(i, ct);

            if (novo.BeneficioIds.Count > 0)
            {
                foreach (var beneficioId in novo.BeneficioIds)
                {
                    db.BeneficiosDosFuncionarios.Add(BeneficioDoFuncionario.Conceder(funcionario.Id, beneficioId));
                }

                await SalvarLinhaAsync(i, ct);
            }

            cadastrados.Add(new FuncionarioCadastrado(funcionario.Id, numeros[i]));
        }

        var ids = cadastrados.Select(c => c.Id).ToList();
        var emitidos = opcoes.EnviarConvite ? await convites.EmitirAsync(ids, ct) : [];
        var noOnboarding = opcoes.IncluirNoOnboarding ? await onboarding.IncluirAsync(ids, ct) : 0;
        var noFormulario = opcoes.FormularioAdmissaoId is { } idDoFormulario ? await formulario.AdicionarDestinatariosAsync(idDoFormulario, ids, ct) : 0;
        return new ResultadoDoCadastro(cadastrados, emitidos.Count, noOnboarding, noFormulario);
    }

    /// <summary>Grava o que está pendente; se o banco recusa, diz de QUAL linha (duplicidade de CPF, e-mail ou matrícula, ou regra de trigger).</summary>
    private async Task SalvarLinhaAsync(int indice, CancellationToken ct)
    {
        try
        {
            await db.SaveChangesAsync(ct);
        }
        catch (DbUpdateException erro) when (erro.InnerException is MySqlException { Number: 1062 })
        {
            throw new LinhaRecusadaException(indice, porDuplicidade: true, regraDoBanco: null);
        }
        catch (DbUpdateException erro) when (erro.InnerException is MySqlException { SqlState: "45000" } regra)
        {
            throw new LinhaRecusadaException(indice, porDuplicidade: false, regra.Message);
        }
    }

    private async Task ConferirExistenciaAsync(IReadOnlyList<NovoFuncionario> novos, List<ProblemaDeCadastro> problemas, CancellationToken ct)
    {
        var cargoIds = novos.Select(n => n.CargoId).Distinct().ToList();
        var setorIds = novos.Select(n => n.SetorId).Distinct().ToList();
        var beneficioIds = novos.SelectMany(n => n.BeneficioIds ?? []).Distinct().ToList();
        var cargos = (await db.Cargos.AsNoTracking().Where(c => cargoIds.Contains(c.Id)).Select(c => c.Id).ToListAsync(ct)).ToHashSet();
        var setores = (await db.Setores.AsNoTracking().Where(s => setorIds.Contains(s.Id)).Select(s => s.Id).ToListAsync(ct)).ToHashSet();
        var beneficios = (await db.Beneficios.AsNoTracking().Where(b => beneficioIds.Contains(b.Id)).Select(b => b.Id).ToListAsync(ct)).ToHashSet();

        for (var i = 0; i < novos.Count; i++)
        {
            var novo = novos[i];
            if (!cargos.Contains(novo.CargoId))
            {
                problemas.Add(Campo(i, "cargoId", "O cargo não existe."));
            }

            if (!setores.Contains(novo.SetorId))
            {
                problemas.Add(Campo(i, "setorId", "O setor não existe."));
            }

            foreach (var beneficioId in (novo.BeneficioIds ?? []).Where(id => !beneficios.Contains(id)).Distinct())
            {
                problemas.Add(Campo(i, "beneficioIds", $"O benefício {beneficioId.ToString(CultureInfo.InvariantCulture)} não existe."));
            }
        }
    }

    /// <summary>
    /// CPF e e-mail já cadastrados (inclusive de INATIVOS: o banco exige os dois únicos entre todos os cadastros). A comparação do e-mail é a do banco (sem maiúsculas e sem acentos).
    /// <paramref name="semOFuncionario"/> é a edição: o próprio colaborador não conta.
    /// </summary>
    private async Task ConferirDuplicidadeNoBancoAsync(IReadOnlyList<NovoFuncionario> novos, List<ProblemaDeCadastro> problemas, int? semOFuncionario, CancellationToken ct)
    {
        var cpfs = novos.Select(n => n.Cpf ?? string.Empty).Where(c => c.Length == 11).Distinct().ToList();
        var emails = novos.Select(n => (n.EmailCorporativo ?? string.Empty).Trim()).Where(e => e.Length > 0).Distinct().ToList();

        var cpfExistente = (await (
            from d in db.DetalhesDosFuncionarios.AsNoTracking()
            where cpfs.Contains(d.Cpf)
            join f in db.Funcionarios.AsNoTracking() on d.FuncionarioId equals f.Id
            where semOFuncionario == null || f.Id != semOFuncionario
            select new { d.Cpf, f.Id, f.Nome, f.Ativo }).ToListAsync(ct))
            .GroupBy(x => x.Cpf)
            .ToDictionary(g => g.Key, g => g.First());
        var emailExistente = (await db.Funcionarios.AsNoTracking()
            .Where(f => emails.Contains(f.EmailCorporativo!) && (semOFuncionario == null || f.Id != semOFuncionario))
            .Select(f => new { f.EmailCorporativo, f.Id, f.Nome, f.Ativo })
            .ToListAsync(ct))
            .GroupBy(x => ChaveDoEmail(x.EmailCorporativo!))
            .ToDictionary(g => g.Key, g => g.First());

        for (var i = 0; i < novos.Count; i++)
        {
            if (cpfExistente.TryGetValue(novos[i].Cpf ?? string.Empty, out var porCpf))
            {
                problemas.Add(Repetido(i, "cpf", "CPF_JA_CADASTRADO", "este CPF", "Este CPF", porCpf.Id, porCpf.Nome, porCpf.Ativo));
            }
        }

        for (var i = 0; i < novos.Count; i++)
        {
            if (emailExistente.TryGetValue(ChaveDoEmail(novos[i].EmailCorporativo ?? string.Empty), out var porEmail))
            {
                problemas.Add(Repetido(i, "emailCorporativo", "EMAIL_JA_CADASTRADO", "este e-mail corporativo", "Este e-mail corporativo", porEmail.Id, porEmail.Nome, porEmail.Ativo));
            }
        }
    }

    private static ProblemaDeCadastro Repetido(int indice, string campo, string codigo, string dado, string dadoNoInicio, int funcionarioId, string nome, bool ativo) =>
        new(
            indice,
            TipoDeProblemaDeCadastro.Duplicado,
            campo,
            codigo,
            ativo
                ? $"Já existe um colaborador com {dado}."
                : $"{dadoNoInicio} pertence a um cadastro inativo ({nome}). Se a pessoa está voltando à empresa, use \"Readmitir\" na ficha dela.",
            new CadastroExistente(funcionarioId, nome, ativo));

    /// <summary>CPF e e-mail que se repetem DENTRO do lote: a linha de baixo aponta a de cima (<c>OutroIndice</c>).</summary>
    private static void ConferirDuplicidadeNoLote(IReadOnlyList<NovoFuncionario> novos, List<ProblemaDeCadastro> problemas)
    {
        var cpfs = new Dictionary<string, int>(StringComparer.Ordinal);
        var emails = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < novos.Count; i++)
        {
            var cpf = novos[i].Cpf ?? string.Empty;
            if (cpf.Length == 11 && !cpfs.TryAdd(cpf, i))
            {
                problemas.Add(new ProblemaDeCadastro(i, TipoDeProblemaDeCadastro.Duplicado, "cpf", "CPF_JA_CADASTRADO", $"Este CPF se repete na linha {cpfs[cpf] + 1}.", OutroIndice: cpfs[cpf]));
            }
        }

        for (var i = 0; i < novos.Count; i++)
        {
            var email = ChaveDoEmail(novos[i].EmailCorporativo ?? string.Empty);
            if (email.Length > 0 && !emails.TryAdd(email, i))
            {
                problemas.Add(new ProblemaDeCadastro(i, TipoDeProblemaDeCadastro.Duplicado, "emailCorporativo", "EMAIL_JA_CADASTRADO", $"Este e-mail corporativo se repete na linha {emails[email] + 1}.", OutroIndice: emails[email]));
            }
        }
    }

    private async Task ConferirGestoresAsync(IReadOnlyList<NovoFuncionario> novos, List<ProblemaDeCadastro> problemas, CancellationToken ct)
    {
        foreach (var setorId in novos.Select(n => n.SetorId).Distinct())
        {
            if (!(await gestores.ResolverAsync(setorId, null, ct)).GestorInativo)
            {
                continue;
            }

            for (var i = 0; i < novos.Count; i++)
            {
                if (novos[i].SetorId == setorId)
                {
                    problemas.Add(new ProblemaDeCadastro(
                        i,
                        TipoDeProblemaDeCadastro.Regra,
                        "setorId",
                        "GESTOR_DO_SETOR_INATIVO",
                        "O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar."));
                }
            }
        }
    }

    private static ProblemaDeCadastro Campo(int indice, string campo, string mensagem) =>
        new(indice, TipoDeProblemaDeCadastro.Campo, campo, ValidacaoDeCamposDoCadastro.CodigoDeCampoInvalido, mensagem);

    /// <summary>A chave de comparação de e-mail: sem espaços, sem maiúsculas e sem acentos (a collation do banco, <c>utf8mb4_unicode_ci</c>, faz o mesmo).</summary>
    private static string ChaveDoEmail(string email)
    {
        var decomposto = email.Trim().Normalize(NormalizationForm.FormD);
        var semAcento = new StringBuilder(decomposto.Length);
        foreach (var letra in decomposto.Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark))
        {
            semAcento.Append(letra);
        }

        return semAcento.ToString().ToLowerInvariant();
    }
}
