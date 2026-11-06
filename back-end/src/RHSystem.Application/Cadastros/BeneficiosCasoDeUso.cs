using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Beneficios;

namespace RHSystem.Application.Cadastros;

/// <summary>
/// O catálogo de benefícios (tela 29): lista, cria, edita e exclui. Só o perfil RH. A ordem de avaliação é a do contrato: 404 (o benefício existe?), 400 (os campos), 409 (o nome, ou o benefício em uso).
/// Excluir só vale se nenhuma linha de <c>BeneficioDoFuncionario</c> aponta para o benefício, de qualquer status: a verificação evita o caso comum e o repositório traduz o vínculo que nasce no meio da exclusão.
/// </summary>
public sealed class BeneficiosCasoDeUso(IBeneficiosRepositorio beneficios)
{
    public async Task<BeneficioCatalogoListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct)
    {
        var pagina = await beneficios.ListarAsync(p.Validado(), ct);
        return new BeneficioCatalogoListaResposta(pagina.Itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, pagina.TotalPaginas);
    }

    public async Task<BeneficioCatalogoItem> CriarAsync(BeneficioCatalogoComando comando, CancellationToken ct)
    {
        Validar(comando);
        var nome = comando.Nome.Trim();
        if (await beneficios.NomeEmUsoAsync(nome, ignorarId: null, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        var beneficio = Beneficio.Criar(nome, comando.Descricao, comando.Opcional, comando.AceitaDependentes, comando.AceitaTrajeto);
        await beneficios.AdicionarAsync(beneficio, ct);
        return Item(beneficio, quantidadePessoas: 0);
    }

    public async Task<BeneficioCatalogoItem> EditarAsync(int beneficioId, BeneficioCatalogoComando comando, CancellationToken ct)
    {
        var beneficio = await beneficios.ObterAsync(beneficioId, ct) ?? throw new NaoEncontradoException();
        Validar(comando);
        var nome = comando.Nome.Trim();
        if (await beneficios.NomeEmUsoAsync(nome, ignorarId: beneficioId, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        beneficio.Atualizar(nome, comando.Descricao, comando.Opcional, comando.AceitaDependentes, comando.AceitaTrajeto);
        await beneficios.SalvarAsync(ct);
        return Item(beneficio, await beneficios.QuantidadeDePessoasAsync(beneficioId, ct));
    }

    public async Task ExcluirAsync(int beneficioId, CancellationToken ct)
    {
        var beneficio = await beneficios.ObterAsync(beneficioId, ct) ?? throw new NaoEncontradoException();
        if (await beneficios.TemVinculoAsync(beneficioId, ct))
        {
            throw MensagensDoApoio.CadastroEmUso.ComoConflito();
        }

        await beneficios.ExcluirAsync(beneficio, ct);
    }

    private static void Validar(BeneficioCatalogoComando comando)
    {
        var erros = new ColetorDeErros();
        ValidacaoDosCadastros.Nome(erros, comando.Nome);
        ValidacaoDosCadastros.Descricao(erros, comando.Descricao);
        erros.LancarSeHouver();
    }

    private static BeneficioCatalogoItem Item(Beneficio beneficio, int quantidadePessoas) =>
        new(beneficio.Id, beneficio.Nome, beneficio.Descricao, beneficio.Opcional, beneficio.AceitaDependentes, beneficio.AceitaTrajeto, quantidadePessoas);
}
