using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Colaboradores;

/// <summary>
/// <c>editarFuncionario</c>: abre a transação, chama a edição (a MESMA validação do cadastro, mais as regras que só a edição tem) e devolve a ficha atualizada. Os problemas viram as respostas do
/// contrato na ordem 400 (por campo, todos juntos), 409 (CPF primeiro, com o cadastro que já usa o dado) e 422, pela mesma tradução do <c>criarFuncionario</c> (<see cref="ProblemasDoCadastro"/>). Se o banco
/// recusar a gravação depois de a validação passar (duas pessoas usando o mesmo CPF ao mesmo tempo), a transação desfaz tudo e a resposta é a 409 de quem chegou depois.
/// </summary>
public sealed class EditarFuncionarioCasoDeUso(IUnidadeDeTrabalho unidade, IEdicaoDeFuncionarios edicao, ICadastroDeFuncionarios cadastro, IConsultaDeFuncionarios consulta)
{
    public async Task<FuncionarioFichaRh> ExecutarAsync(int funcionarioId, FuncionarioEditarComando comando, CancellationToken ct)
    {
        var dados = new NovoFuncionario(
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
            comando.BeneficioIds ?? []); // sem a lista o corpo é inválido (400 em beneficioIds): ela nunca vale como "tirar todos"
        var pessoais = new DadosPessoaisDaEdicao(comando.EmailPessoal?.Trim(), comando.Rg?.Trim(), comando.RgOrgaoEmissor?.Trim(), comando.Endereco, comando.BeneficioIds is not null);

        try
        {
            await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    await edicao.EditarAsync(funcionarioId, dados, pessoais, ct);
                    return true;
                },
                ct);
        }
        catch (CadastroInvalidoException invalido)
        {
            throw ProblemasDoCadastro.ParaExcecao(invalido.Problemas);
        }
        catch (LinhaRecusadaException recusada)
        {
            throw await ProblemasDoCadastro.DaRecusaAsync(recusada, () => cadastro.ValidarEdicaoAsync(funcionarioId, dados, ct));
        }

        return await consulta.ObterFichaRhAsync(funcionarioId, ct) ?? throw new NaoEncontradoException();
    }
}
