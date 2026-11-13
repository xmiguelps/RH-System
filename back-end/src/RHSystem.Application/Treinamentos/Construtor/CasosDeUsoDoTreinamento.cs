using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Treinamentos.Mensagens;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Construtor;

/// <summary>As conferências do público que dependem do banco: setor ou pessoa que não existe é 400 no campo (e não um 1452 cru da chave estrangeira).</summary>
public static class ConferenciaDoPublicoDoTreinamento
{
    public static async Task ConferirAsync(ColetorDeErros erros, IConsultaDeTreinamentos consulta, CamposDoTreinamento campos, CancellationToken ct)
    {
        if (campos.SetorIds.Count > 0 && (await consulta.SetoresInexistentesAsync(campos.SetorIds, ct)).Count > 0)
        {
            erros.Adicionar("setorIds", "Escolha só setores que existem.");
        }

        if (campos.FuncionarioIds.Count > 0 && (await consulta.FuncionariosInexistentesAsync(campos.FuncionarioIds, ct)).Count > 0)
        {
            erros.Adicionar("funcionarioIds", "Escolha só pessoas que existem.");
        }
    }
}

/// <summary>
/// <c>POST /api/rh/treinamentos</c>: cria o treinamento como RASCUNHO. Só o título é obrigatório; as duas caixas que o banco liga por padrão (<c>emiteCertificado</c> e <c>incluiAdmitidos</c>) são gravadas como vieram; nenhum e-mail, nenhum prazo; <c>DATA_FIM</c> e as duas colunas sem uso ficam como o banco as cria. 400 com todos os
/// erros juntos (título, descrição, listas do outro público, setor ou pessoa que não existe). Evento <c>CRIOU</c> com o título.
/// </summary>
public sealed class CriarTreinamentoCasoDeUso(ITreinamentosRepositorio treinamentos, IConsultaDeTreinamentos consulta, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    public async Task<TreinamentoRh> ExecutarAsync(TreinamentoComando corpo, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        var campos = ValidacaoDoTreinamento.Validar(erros, corpo.Titulo, corpo.Descricao, corpo.ComecaEm, corpo.Obrigatorio, corpo.Onboarding, corpo.EmiteCertificado, corpo.Publico, corpo.SetorIds, corpo.FuncionarioIds, corpo.IncluiAdmitidos);
        if (campos is not null)
        {
            await ConferenciaDoPublicoDoTreinamento.ConferirAsync(erros, consulta, campos, ct);
        }

        erros.LancarSeHouver();

        var id = await unidade.ExecutarEmTransacaoAsync(
            async () =>
            {
                var criado = await treinamentos.CriarAsync(campos!, ct);
                auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Criou, $"Criou o treinamento \"{campos!.Titulo}\".", $"Treinamento \"{campos.Titulo}\"", "TREINAMENTOS", criado));
                await unidade.SalvarAsync(ct);
                return criado;
            },
            ct);
        return (await consulta.ObterDetalheAsync(id, FusoBrasil.Hoje(relogio.AgoraUtc), ct)).OuNaoEncontrado();
    }
}

/// <summary><c>GET /api/rh/treinamentos/{treinamentoId}</c>: o treinamento completo, em qualquer situação, com os módulos em ordem (texto e link; o PDF só como nome). 404 se não existe.</summary>
public sealed class ObterTreinamentoRhCasoDeUso(IConsultaDeTreinamentos consulta, IRelogio relogio)
{
    public async Task<TreinamentoRh> ExecutarAsync(int treinamentoId, CancellationToken ct) =>
        (await consulta.ObterDetalheAsync(treinamentoId, FusoBrasil.Hoje(relogio.AgoraUtc), ct)).OuNaoEncontrado();
}

/// <summary>
/// <c>PUT /api/rh/treinamentos/{treinamentoId}</c>: substitui TODOS os campos. Ordem: 404, 400 (todos os campos juntos; publicado sem data de início também é 400), 422 <c>TREINAMENTO_ENCERRADO</c> (encerrado não muda nada, nem com o corpo igual ao atual) e 422 <c>TREINAMENTO_CAMPO_TRAVADO</c> (depois de publicado só mudam título,
/// descrição, a caixa de admitidos e, enquanto agendado, a data; as listas se comparam como conjuntos). A gravação é um <c>UPDATE</c> condicional ao estado lido: se outra aba publicou ou encerrou no meio, o caso de uso relê e dá o erro certo (nunca grava por cima). Evento <c>ALTEROU</c>.
/// </summary>
public sealed class AtualizarTreinamentoCasoDeUso(ITreinamentosRepositorio treinamentos, IConsultaDeTreinamentos consulta, IUnidadeDeTrabalho unidade, IAuditor auditor, IRelogio relogio)
{
    private const int TentativasNaCorrida = 5;

    public async Task<TreinamentoRh> ExecutarAsync(int treinamentoId, TreinamentoComando corpo, CancellationToken ct)
    {
        for (var tentativa = 1; tentativa <= TentativasNaCorrida; tentativa++)
        {
            var lido = (await consulta.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
            var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
            var situacao = lido.Situacao(hoje);

            var erros = new ColetorDeErros();
            var campos = ValidacaoDoTreinamento.Validar(erros, corpo.Titulo, corpo.Descricao, corpo.ComecaEm, corpo.Obrigatorio, corpo.Onboarding, corpo.EmiteCertificado, corpo.Publico, corpo.SetorIds, corpo.FuncionarioIds, corpo.IncluiAdmitidos);
            if (situacao != SituacaoDoTreinamento.Rascunho)
            {
                ValidacaoDoTreinamento.ConferirDataDoPublicado(erros, corpo.ComecaEm);
            }

            if (campos is not null)
            {
                await ConferenciaDoPublicoDoTreinamento.ConferirAsync(erros, consulta, campos, ct);
            }

            erros.LancarSeHouver();

            switch (CamposTravados.Decidir(situacao, lido.Campos, campos!))
            {
                case ResultadoDaEdicao.Encerrado:
                    throw MensagensDosTreinamentos.EncerradoParaEditar.ComoProblema();
                case ResultadoDaEdicao.CampoTravado:
                    throw MensagensDosTreinamentos.CampoTravadoDoTreinamento.ComoProblema();
            }

            var gravou = await unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    if (!await treinamentos.AtualizarAsync(lido, campos!, ct))
                    {
                        return false;
                    }

                    auditor.Registrar(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Alterou, $"Alterou o treinamento \"{campos!.Titulo}\".", $"Treinamento \"{campos.Titulo}\"", "TREINAMENTOS", lido.Id));
                    await unidade.SalvarAsync(ct);
                    return true;
                },
                ct);
            if (gravou)
            {
                return (await consulta.ObterDetalheAsync(treinamentoId, hoje, ct)).OuNaoEncontrado();
            }
        }

        throw MensagensDosTreinamentos.TreinamentoMudouEmOutraTela;
    }
}

/// <summary>
/// <c>GET /api/rh/treinamentos/publico/contagem</c>: quantas pessoas o público alcança (só ativas com a admissão já ocorrida). <c>publico</c> é obrigatório; <c>TODOS</c> ignora as listas, <c>SETORES</c> usa <c>setorIds</c> (até 100) e <c>PESSOAS</c> usa <c>funcionarioIds</c> (até 500). 400 com os erros juntos.
/// </summary>
public sealed class ContarPublicoDoTreinamentoCasoDeUso(IConsultaDeTreinamentos consulta, IRelogio relogio)
{
    public async Task<TreinamentoPublicoContagem> ExecutarAsync(string? publico, IReadOnlyList<int>? setorIds, IReadOnlyList<int>? funcionarioIds, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        PublicoDoTreinamento? lido = null;
        LeituraDeFiltros.Tentar(erros, () => lido = EnumsDeConsulta.Ler<PublicoDoTreinamento>(publico, "publico"));
        if (string.IsNullOrEmpty(publico))
        {
            erros.Adicionar("publico", "Informe o público: TODOS, SETORES ou PESSOAS.");
        }

        var setores = setorIds ?? [];
        var pessoas = funcionarioIds ?? [];
        if (setores.Count > ValidacaoDoTreinamento.MaximoDeSetores)
        {
            erros.Adicionar("setorIds", $"Escolha até {ValidacaoDoTreinamento.MaximoDeSetores} setores.");
        }

        if (pessoas.Count > ValidacaoDoTreinamento.MaximoDePessoas)
        {
            erros.Adicionar("funcionarioIds", $"Escolha até {ValidacaoDoTreinamento.MaximoDePessoas} pessoas.");
        }

        erros.LancarSeHouver();
        return new TreinamentoPublicoContagem(await consulta.ContarPublicoAsync(lido!.Value, setores, pessoas, FusoBrasil.Hoje(relogio.AgoraUtc), ct));
    }
}
