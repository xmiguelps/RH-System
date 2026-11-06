using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Cadastros;

/// <summary>
/// Os cargos (tela 29): lista, cria e edita. Só o perfil RH. Não existe exclusão de cargo. A ordem de avaliação é a do contrato: 404 (o cargo existe?), 400 (os campos), 409 (o nome). O nome é único sem
/// diferenciar maiúsculas nem acentos; a verificação evita o erro comum e o repositório traduz o que ainda escapar numa corrida (a chave única do banco).
/// </summary>
public sealed class CargosCasoDeUso(ICargosRepositorio cargos)
{
    public async Task<CargoListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct)
    {
        var pagina = await cargos.ListarAsync(p.Validado(), ct);
        return new CargoListaResposta(pagina.Itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, pagina.TotalPaginas);
    }

    public async Task<CargoItem> CriarAsync(CargoComando comando, CancellationToken ct)
    {
        Validar(comando);
        var nome = comando.Nome.Trim();
        if (await cargos.NomeEmUsoAsync(nome, ignorarId: null, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        var cargo = Cargo.Criar(nome, comando.Descricao, comando.CargaSemanalHoras);
        await cargos.AdicionarAsync(cargo, ct);
        return Item(cargo, quantidadePessoas: 0);
    }

    public async Task<CargoItem> EditarAsync(int cargoId, CargoComando comando, CancellationToken ct)
    {
        var cargo = await cargos.ObterAsync(cargoId, ct) ?? throw new NaoEncontradoException();
        Validar(comando);
        var nome = comando.Nome.Trim();
        if (await cargos.NomeEmUsoAsync(nome, ignorarId: cargoId, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        cargo.Atualizar(nome, comando.Descricao, comando.CargaSemanalHoras);
        await cargos.SalvarAsync(ct);
        return Item(cargo, await cargos.QuantidadeDePessoasAsync(cargoId, ct));
    }

    private static void Validar(CargoComando comando)
    {
        var erros = new ColetorDeErros();
        ValidacaoDosCadastros.Nome(erros, comando.Nome);
        ValidacaoDosCadastros.Descricao(erros, comando.Descricao);
        ValidacaoDosCadastros.CargaSemanal(erros, comando.CargaSemanalHoras);
        erros.LancarSeHouver();
    }

    private static CargoItem Item(Cargo cargo, int quantidadePessoas) =>
        new(cargo.Id, cargo.Nome, cargo.Descricao, cargo.CargaSemanalHoras ?? 0, quantidadePessoas);
}
