using RHSystem.Application.Autenticacao;
using RHSystem.Application.Colaboradores;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Autenticacao;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Application.Cadastros;

/// <summary>
/// Os setores (tela 29): lista, cria e edita (não há exclusão), com a troca de gestor. Só o perfil RH. A ordem de avaliação é a do contrato: 404, 400, 409 (nome), 422 (pessoa inativa), 422 (nível RH), 409 (o gestor
/// já responde por outro setor). Tudo é validado ANTES de gravar. Trocar o gestor (só quando <c>gestorFuncionarioId</c> difere do atual) roda numa transação só: promove o novo a <c>Gestor</c> (e cria a linha em
/// <c>GESTORES</c> se faltar), rebaixa o antigo a <c>Funcionario</c> (a linha dele FICA: os pedidos antigos a referenciam), grava o setor, <b>chama</b> <see cref="IGestorDoFuncionario.RefazerDosMembrosAsync"/> (1b) e,
/// com gestor antigo, <b>chama</b> <see cref="IPedidosParadosNoGestor.PassarParaORhAsync"/> (1a); qualquer falha desfaz tudo. A 1c não reimplementa nenhuma das duas regras. Escolher como gestor alguém de OUTRO
/// setor não o muda de setor (decisão 1 do plano).
/// </summary>
public sealed class SetoresCasoDeUso(ISetoresRepositorio setores, IGestorDoFuncionario gestorDoFuncionario, IPedidosParadosNoGestor pedidosParados, IUnidadeDeTrabalho unidade, IRelogio relogio)
{
    public async Task<SetorListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct)
    {
        var pagina = await setores.ListarAsync(p.Validado(), Hoje(), ct);
        return new SetorListaResposta(pagina.Itens, pagina.Pagina, pagina.TamanhoPagina, pagina.Total, pagina.TotalPaginas);
    }

    public async Task<SetorItem> CriarAsync(SetorComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var pessoa = await ValidarAsync(comando, ct);
        var nome = comando.Nome.Trim();
        if (await setores.NomeEmUsoAsync(nome, ignorarId: null, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        if (pessoa is not null)
        {
            await ConferirOGestorAsync(pessoa, setorAtualId: null, ct);
        }

        var setor = Setor.Criar(nome, comando.Descricao);
        if (pessoa is null)
        {
            setores.AdicionarSetor(setor);
            await setores.SalvarAsync(ct);
        }
        else
        {
            await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var gestor = await PrepararNovoGestorAsync(pessoa, ct);
                    await setores.SalvarGestorEOsNiveisAsync(ct); // a linha nova e o nível: só então o setor pode apontar para o gestor (o id sai da gravação)
                    setor.DefinirGestor(gestor.Id);
                    setores.AdicionarSetor(setor);
                    await setores.SalvarAsync(ct);
                    await gestorDoFuncionario.RefazerDosMembrosAsync(setor.Id, ct); // o setor novo não tem membros: o efeito é nenhum, mas é o mesmo caminho da edição
                    return setor.Id;
                },
                ct);
        }

        return await ItemAsync(setor.Id, ct);
    }

    public async Task<SetorItem> EditarAsync(int setorId, SetorComando comando, CancellationToken ct)
    {
        ArgumentNullException.ThrowIfNull(comando);
        var setor = await setores.ObterAsync(setorId, ct) ?? throw new NaoEncontradoException();
        var pessoa = await ValidarAsync(comando, ct);
        var nome = comando.Nome.Trim();
        if (await setores.NomeEmUsoAsync(nome, setorId, ct))
        {
            throw MensagensDoApoio.NomeJaCadastrado.ComoConflito();
        }

        var gestorAntigo = setor.Gestor;
        if (comando.GestorFuncionarioId == gestorAntigo?.FuncionarioId)
        {
            // O gestor não mudou: só nome e descrição, sem rebaixar, promover, refazer nem passar pedido nenhum (e sem reconferir o gestor, que pode ter sido desligado depois).
            setor.Atualizar(nome, comando.Descricao);
            await setores.SalvarAsync(ct);
            return await ItemAsync(setorId, ct);
        }

        if (pessoa is not null)
        {
            await ConferirOGestorAsync(pessoa, setorId, ct);
        }

        await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var gestorNovo = pessoa is null ? null : await PrepararNovoGestorAsync(pessoa, ct);
                if (gestorAntigo is not null)
                {
                    await RebaixarSeForGestorAsync(gestorAntigo.Funcionario, ct);
                }

                await setores.SalvarGestorEOsNiveisAsync(ct);
                setor.Atualizar(nome, comando.Descricao);
                setor.DefinirGestor(gestorNovo?.Id);
                await setores.SalvarAsync(ct);
                await gestorDoFuncionario.RefazerDosMembrosAsync(setor.Id, ct); // lê o gestor do setor NO BANCO: por isso depois de salvar o setor, na mesma transação
                if (gestorAntigo is not null)
                {
                    await pedidosParados.PassarParaORhAsync(gestorAntigo.Id, ct); // os pedidos dele passam ao RH, ao tirar e ao trocar; sem gestor antigo, não há de quem passar
                }

                return setor.Id;
            },
            ct);

        return await ItemAsync(setorId, ct);
    }

    /// <summary>Os campos e a pessoa do gestor (400 junto: nome, descrição e "a pessoa não existe"). Devolve a pessoa rastreada, ou <c>null</c> quando o setor fica sem gestor.</summary>
    private async Task<Funcionario?> ValidarAsync(SetorComando comando, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        ValidacaoDosCadastros.Nome(erros, comando.Nome);
        ValidacaoDosCadastros.Descricao(erros, comando.Descricao);
        Funcionario? pessoa = null;
        if (comando.GestorFuncionarioId is { } id)
        {
            pessoa = await setores.ObterPessoaAsync(id, ct);
            if (pessoa is null)
            {
                erros.Adicionar("gestorFuncionarioId", "A pessoa escolhida não existe.");
            }
        }

        erros.LancarSeHouver();
        return pessoa;
    }

    private async Task ConferirOGestorAsync(Funcionario pessoa, int? setorAtualId, CancellationToken ct)
    {
        if (!pessoa.Ativo)
        {
            throw MensagensDoApoio.FuncionarioInativo.ComoErroDeNegocio();
        }

        if (pessoa.Perfil == Perfil.RH)
        {
            throw MensagensDoApoio.GestorPrecisaTerPerfilGestor(pessoa.Nome).ComoErroDeNegocio();
        }

        if (await setores.NomeDoSetorQueGerenciaAsync(pessoa.Id, setorAtualId, ct) is { } nomeDoOutroSetor)
        {
            throw MensagensDoApoio.GestorJaRespondePorOutroSetor(pessoa.Nome, nomeDoOutroSetor).ComoConflito();
        }
    }

    /// <summary>A linha de <c>GESTORES</c> da pessoa (criada se faltar) e o nível <c>Gestor</c> (quem tem <c>Funcionario</c> é promovido). Não grava: quem chama grava junto com o resto.</summary>
    private async Task<Gestor> PrepararNovoGestorAsync(Funcionario pessoa, CancellationToken ct)
    {
        var gestor = await setores.ObterGestorDaPessoaAsync(pessoa.Id, ct);
        if (gestor is null)
        {
            gestor = Gestor.Criar(pessoa);
            setores.AdicionarGestor(gestor);
        }

        if (pessoa.Perfil != Perfil.Gestor)
        {
            pessoa.DefinirNivel(await setores.ObterNivelAsync(Perfil.Gestor, ct));
        }

        return gestor;
    }

    private async Task RebaixarSeForGestorAsync(Funcionario pessoa, CancellationToken ct)
    {
        if (pessoa.Perfil == Perfil.Gestor)
        {
            pessoa.DefinirNivel(await setores.ObterNivelAsync(Perfil.Funcionario, ct));
        }
    }

    private async Task<SetorItem> ItemAsync(int setorId, CancellationToken ct) =>
        await setores.ObterItemAsync(setorId, Hoje(), ct) ?? throw new InvalidOperationException("O setor gravado não foi encontrado.");

    private DateOnly Hoje() => FusoBrasil.Hoje(relogio.AgoraUtc);
}
