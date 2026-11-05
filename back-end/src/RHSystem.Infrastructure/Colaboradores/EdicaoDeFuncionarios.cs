using System.Globalization;
using Microsoft.EntityFrameworkCore;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Escopo;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Beneficios;
using RHSystem.Domain.Colaboradores;
using RHSystem.Infrastructure.Persistencia;

namespace RHSystem.Infrastructure.Colaboradores;

/// <summary>
/// A gravação da edição de um colaborador, dentro da transação do chamador. Valida com a MESMA validação do cadastro (<see cref="ICadastroDeFuncionarios.ValidarEdicaoAsync"/>), os campos que só a edição
/// tem (<see cref="ValidacaoDeCamposDaEdicao"/>) e as regras que dependem do estado da pessoa: a data de admissão não pode ser depois do último dia de trabalho (o <c>CHECK</c> do banco daria 500), ninguém
/// muda o PRÓPRIO nível de acesso, quem gerencia um setor continua com o nível Gestor, e benefício não se dá a quem está inativo.
/// <para>
/// Os métodos de domínio é que gravam (o interceptor registra o que mudou: cargo, setor, nome e os outros campos marcados). Os dados pessoais saem num evento à parte, com <c>ValorOculto</c>: o log diz
/// "CPF alterado", nunca o valor.
/// </para>
/// </summary>
public sealed class EdicaoDeFuncionarios(
    AppDbContext db,
    IAtorAtual ator,
    IAuditor auditor,
    IGeradorDeMatriculas matriculas,
    IGestorDoFuncionario gestores,
    ICadastroDeFuncionarios cadastro) : IEdicaoDeFuncionarios
{
    private const string CodigoDeCampoInvalido = ValidacaoDeCamposDoCadastro.CodigoDeCampoInvalido;

    public async Task EditarAsync(int funcionarioId, NovoFuncionario dados, DadosPessoaisDaEdicao pessoais, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(dados);
        ArgumentNullException.ThrowIfNull(pessoais);
        if (db.Database.CurrentTransaction is null)
        {
            throw new InvalidOperationException("Editar exige uma transação aberta (a matrícula e o tudo-ou-nada só são seguros dentro dela).");
        }

        var funcionario = await db.Funcionarios
            .Include(f => f.NivelDeAcesso)
            .Include(f => f.Detalhes)
            .SingleOrDefaultAsync(f => f.Id == funcionarioId, ct)
            ?? throw new NaoEncontradoException();
        var linhas = await db.BeneficiosDosFuncionarios.Where(b => b.FuncionarioId == funcionarioId).ToListAsync(ct);

        var problemas = new List<ProblemaDeCadastro>(await cadastro.ValidarEdicaoAsync(funcionarioId, dados, ct));
        problemas.AddRange(ValidacaoDeCamposDaEdicao.Validar(0, pessoais));
        await ConferirRegrasDaEdicaoAsync(funcionario, dados, linhas, problemas, ct);
        if (problemas.Count > 0)
        {
            throw new CadastroInvalidoException([.. problemas.OrderBy(p => p.Tipo)]);
        }

        var setorMudou = funcionario.SetorId != dados.SetorId;
        var gestorId = setorMudou ? (await gestores.ResolverAsync(dados.SetorId, funcionarioId, ct)).GestorId : funcionario.GestorId;
        var nomeDoNivel = NomesDeNivel.DoPerfil(dados.Perfil);
        var nivel = await db.NiveisDeAcesso.SingleAsync(n => n.Nome == nomeDoNivel, ct);
        funcionario.AtualizarCadastro(dados.Nome, dados.EmailCorporativo, dados.DataNascimento, dados.DataAdmissao, dados.TipoContrato, dados.CargoId, dados.SetorId, gestorId, nivel);

        if (funcionario.Matricula is null)
        {
            funcionario.AtribuirMatricula((await matriculas.ReservarAsync(1, ct))[0]);
        }

        GravarDadosPessoais(funcionario, dados, pessoais);
        AplicarBeneficios(funcionarioId, dados.BeneficioIds, linhas);
        await CadastroDeFuncionarios.SalvarLinhaAsync(db, 0, ct);
    }

    /// <summary>As regras da edição que dependem do estado atual da pessoa. A data de admissão é 400 (campo); as outras são 422.</summary>
    private async Task ConferirRegrasDaEdicaoAsync(Funcionario funcionario, NovoFuncionario dados, List<BeneficioDoFuncionario> linhas, List<ProblemaDeCadastro> problemas, CancellationToken ct)
    {
        var agendado = await db.DesligamentosAgendados.AsNoTracking()
            .Where(d => d.FuncionarioId == funcionario.Id && d.Situacao == SituacaoDoDesligamento.Agendado)
            .Select(d => (DateOnly?)d.UltimoDiaTrabalho)
            .FirstOrDefaultAsync(ct);
        var ultimoDia = new[] { agendado, funcionario.DataDesligamento }.Where(d => d is not null).Min();
        if (ultimoDia is { } ultimo && dados.DataAdmissao > ultimo)
        {
            problemas.Add(new ProblemaDeCadastro(0, TipoDeProblemaDeCadastro.Campo, "dataAdmissao", CodigoDeCampoInvalido, $"A data de admissão não pode ser depois do último dia de trabalho ({ultimo.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture)})."));
        }

        var perfilMudou = dados.Perfil != funcionario.Perfil;
        if (perfilMudou && ator.FuncionarioId == funcionario.Id)
        {
            problemas.Add(Regra("perfil", "NAO_PODE_ALTERAR_O_PROPRIO_PERFIL", "Você não pode alterar o seu próprio nível de acesso."));
        }

        if (perfilMudou && dados.Perfil != Perfil.Gestor)
        {
            var setorGerido = await (
                from s in db.Setores.AsNoTracking()
                join g in db.Gestores.AsNoTracking() on s.GestorId equals g.Id
                where g.FuncionarioId == funcionario.Id
                orderby s.Id
                select s.Nome).FirstOrDefaultAsync(ct);
            if (setorGerido is not null)
            {
                problemas.Add(Regra("perfil", "GESTOR_PRECISA_TER_PERFIL_GESTOR", $"{funcionario.Nome} gerencia o setor {setorGerido} e precisa ter o nível Gestor. Tire-o do setor antes de mudar o nível."));
            }
        }

        if (!funcionario.Ativo && dados.BeneficioIds.Distinct().Any(id => linhas.FirstOrDefault(l => l.BeneficioId == id) is not { Ativo: true }))
        {
            problemas.Add(Regra("beneficioIds", "FUNCIONARIO_INATIVO", "Não é possível dar benefício a um colaborador inativo."));
        }
    }

    private static ProblemaDeCadastro Regra(string campo, string codigo, string mensagem) => new(0, TipoDeProblemaDeCadastro.Regra, campo, codigo, mensagem);

    /// <summary>
    /// Cria os dados pessoais se faltavam (o administrador do seed) ou os atualiza, e registra o evento do que mudou com <c>ValorOculto</c> (CPF, telefone, e-mail pessoal, RG, órgão emissor e endereço).
    /// Nada mudou, nenhum evento.
    /// </summary>
    private void GravarDadosPessoais(Funcionario funcionario, NovoFuncionario dados, DadosPessoaisDaEdicao pessoais)
    {
        var e = pessoais.Endereco ?? new FuncionarioEndereco(null, null, null, null, null, null, null);
        var novos = new DadosPessoais(dados.Cpf, dados.Telefone, pessoais.EmailPessoal, pessoais.Rg, pessoais.RgOrgaoEmissor, e.Cep, e.Logradouro, e.Numero, e.Complemento, e.Bairro, e.Cidade, e.Uf);
        var antes = funcionario.Detalhes?.ParaDados();
        var mudancas = MudancasDosDadosPessoais(antes, novos);

        if (funcionario.Detalhes is null)
        {
            db.DetalhesDosFuncionarios.Add(FuncionarioDetalhes.Criar(funcionario, novos));
        }
        else
        {
            funcionario.Detalhes.Atualizar(novos);
        }

        if (mudancas.Count > 0)
        {
            auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Colaboradores, AuditoriaAcao.Alterou, $"Dados pessoais de {funcionario.Nome}", funcionario.RegistroAfetado, "FUNCIONARIOS", funcionario.Id, mudancas));
        }
    }

    private static List<MudancaDeAuditoriaNova> MudancasDosDadosPessoais(DadosPessoais? antes, DadosPessoais agora)
    {
        var mudancas = new List<MudancaDeAuditoriaNova>();

        void Se(bool mudou, string campo)
        {
            if (mudou)
            {
                mudancas.Add(new MudancaDeAuditoriaNova(campo, null, null, ValorOculto: true));
            }
        }

        Se(antes?.Cpf != agora.Cpf, "CPF");
        Se(antes?.Telefone != agora.Telefone, "Telefone");
        Se(antes?.EmailPessoal != agora.EmailPessoal, "E-mail pessoal");
        Se(antes?.Rg != agora.Rg, "RG");
        Se(antes?.RgOrgaoEmissor != agora.RgOrgaoEmissor, "Órgão emissor do RG");
        Se(
            (antes?.Cep, antes?.Logradouro, antes?.Numero, antes?.Complemento, antes?.Bairro, antes?.Cidade, antes?.Uf) != (agora.Cep, agora.Logradouro, agora.Numero, agora.Complemento, agora.Bairro, agora.Cidade, agora.Uf),
            "Endereço");
        return mudancas;
    }

    /// <summary>
    /// <paramref name="desejados"/> é o conjunto ATIVO desejado. Sem linha: insere (<c>Conceder</c>); aprovada e desativada: <c>Reativar</c>; pendente, recusada ou cancelada: <c>Reconceder</c> NA MESMA linha (a chave
    /// única <c>(ID_FUNCIONARIO, ID_BENEFICIO)</c> recusaria um segundo <c>INSERT</c>); aprovada e ativa: não muda. O que saiu do conjunto e está aprovado e ativo é desativado; linha pendente, recusada ou cancelada
    /// de benefício fora do conjunto não é tocada.
    /// </summary>
    private void AplicarBeneficios(int funcionarioId, IReadOnlyList<int> desejados, List<BeneficioDoFuncionario> linhas)
    {
        var conjunto = desejados.ToHashSet();
        foreach (var beneficioId in conjunto)
        {
            var linha = linhas.FirstOrDefault(l => l.BeneficioId == beneficioId);
            if (linha is null)
            {
                db.BeneficiosDosFuncionarios.Add(BeneficioDoFuncionario.Conceder(funcionarioId, beneficioId));
            }
            else if (linha.Status != StatusDoPedidoDeBeneficio.Aprovado)
            {
                linha.Reconceder();
            }
            else if (!linha.Ativo)
            {
                linha.Reativar();
            }
        }

        foreach (var linha in linhas.Where(l => l.Ativo && l.Status == StatusDoPedidoDeBeneficio.Aprovado && !conjunto.Contains(l.BeneficioId)))
        {
            linha.Desativar();
        }
    }
}
