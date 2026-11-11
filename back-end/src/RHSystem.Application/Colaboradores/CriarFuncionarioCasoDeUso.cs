using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>criarFuncionario</c>: abre a transação, chama o cadastro (o MESMO da importação) para uma pessoa e devolve a ficha. Os problemas viram as respostas do contrato na ordem 400 (por campo, todos
/// juntos), 409 (CPF primeiro, com o cadastro que já usa o dado) e 422. Se o banco recusar a linha depois de a validação passar (duas pessoas cadastrando o mesmo CPF ao mesmo tempo), a
/// transação desfaz tudo e a resposta é a mesma 409 de quem chegou depois.
/// </summary>
public sealed class CriarFuncionarioCasoDeUso(IUnidadeDeTrabalho unidade, ICadastroDeFuncionarios cadastro, IConsultaDeFuncionarios consulta)
{
    public async Task<FuncionarioFichaRh> ExecutarAsync(FuncionarioCriarComando comando, CancellationToken ct)
    {
        var novo = new NovoFuncionario(
            comando.Nome?.Trim() ?? string.Empty,
            Cpf.Normalizar(comando.Cpf),
            comando.DataNascimento,
            TelefoneMascarado.SoDigitos(comando.Telefone) ?? string.Empty,
            comando.EmailCorporativo?.Trim() ?? string.Empty,
            comando.DataAdmissao,
            comando.TipoContrato,
            comando.CargoId,
            comando.SetorId,
            comando.Perfil,
            comando.BeneficioIds ?? []); // o JSON pode trazer null: vale como sem benefícios
        var opcoes = new OpcoesDoCadastro(comando.EnviarConvite, comando.IncluirNoOnboarding, comando.FormularioAdmissaoId);

        ResultadoDoCadastro resultado;
        try
        {
            resultado = await unidade.ExecutarEmTransacaoAsync(() => cadastro.CadastrarAsync([novo], opcoes, ct), ct);
        }
        catch (CadastroInvalidoException invalido)
        {
            throw ProblemasDoCadastro.ParaExcecao(invalido.Problemas);
        }
        catch (LinhaRecusadaException recusada)
        {
            throw await ProblemasDoCadastro.DaRecusaAsync(recusada, () => cadastro.ValidarAsync([novo], ct));
        }

        return await consulta.ObterFichaRhAsync(resultado.Cadastrados[0].Id, ct) ?? throw new NaoEncontradoException();
    }
}

/// <summary>Traduz os problemas do cadastro (e a recusa do banco) nas exceções que a Api devolve como 400, 409 e 422. A edição (Task 8b) usa o mesmo.</summary>
public static class ProblemasDoCadastro
{
    public const string TextoDaRegraDoGestorInativo = "O gestor informado e um funcionario inativo.";

    public static Exception ParaExcecao(IReadOnlyList<ProblemaDeCadastro> problemas)
    {
        var campos = problemas.Where(p => p.Tipo == TipoDeProblemaDeCadastro.Campo).ToList();
        if (campos.Count > 0)
        {
            return new ErroDeValidacaoException(campos.GroupBy(p => p.Campo).ToDictionary(g => g.Key, g => g.Select(p => p.Mensagem).ToArray()));
        }

        if (problemas.FirstOrDefault(p => p.Tipo == TipoDeProblemaDeCadastro.Duplicado) is { } repetido)
        {
            return Conflito(repetido);
        }

        var regra = problemas.First(p => p.Tipo == TipoDeProblemaDeCadastro.Regra);
        return new ErroDeNegocioException(regra.Codigo, regra.Mensagem);
    }

    /// <summary>A recusa do banco depois da validação: duplicidade vira a 409 do contrato (conferida de novo, agora com o cadastro que ganhou); regra de trigger vira 422 com <c>regraDoBanco</c>.</summary>
    public static async Task<Exception> DaRecusaAsync(LinhaRecusadaException recusada, Func<Task<IReadOnlyList<ProblemaDeCadastro>>> validarDeNovo)
    {
        if (recusada.PorDuplicidade)
        {
            var problemas = await validarDeNovo();
            return problemas.FirstOrDefault(p => p.Tipo == TipoDeProblemaDeCadastro.Duplicado) is { } repetido
                ? Conflito(repetido)
                : new ConflitoException("Já existe um registro com estes dados.");
        }

        var regra = recusada.RegraDoBanco ?? string.Empty;
        var gestorInativo = regra == TextoDaRegraDoGestorInativo;
        return new ProblemaDeApiException(
            422,
            "Operação recusada.",
            gestorInativo ? "O gestor do setor escolhido está inativo. Troque o gestor do setor em Cadastros antes de continuar." : "A operação foi recusada por uma regra do banco de dados.",
            gestorInativo ? "GESTOR_DO_SETOR_INATIVO" : null,
            new Dictionary<string, object?> { ["regraDoBanco"] = regra });
    }

    private static ProblemaDeApiException Conflito(ProblemaDeCadastro repetido) =>
        new(
            409,
            "Conflito.",
            repetido.Mensagem,
            repetido.Codigo,
            repetido.Existente is null ? null : new Dictionary<string, object?> { ["cadastroExistente"] = repetido.Existente });
}
