using RHSystem.Application.Arquivos;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Comunicacao.Mensagens;
using RHSystem.Application.Comunicacao.Regras;
using RHSystem.Application.Documentos;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Comunicacao;

namespace RHSystem.Application.Comunicacao.Holerites;

/// <summary>
/// O que o envio individual e a nova versão têm em comum: conferir o PDF (só PDF, até 5 MiB, pelo conteúdo), calcular quando o holerite fica visível (inclusive no dia do pagamento) e gravar com a receita da 1c: o arquivo vai para o armazenamento
/// FORA de transação, depois uma transação curta grava a linha, a tarefa do e-mail e o evento, e se qualquer passo falhar o arquivo é apagado (o banco nunca apaga holerite: uma linha sem arquivo ficaria para sempre e ainda bloquearia a versão 1).
/// </summary>
public sealed class PublicadorDeHolerite(EnvioDeArquivoDeDocumento envio, IHoleritesRepositorio repositorio, IAgendamentoDoHolerite agendamento, IProximoPagamento proximoPagamento, IUnidadeDeTrabalho unidade, IAuditor auditor)
{
    public static async Task<TipoDeArquivo?> ValidarPdfAsync(ColetorDeErros erros, ArquivoRecebido? arquivo, int arquivosRecebidos, CancellationToken ct) =>
        await ValidacaoDeArquivoDeDocumento.ValidarPdfAsync(erros, arquivo, arquivosRecebidos, "O arquivo é obrigatório.", LimitesDeHolerite.TamanhoMaximoBytes, "O arquivo pode ter até 5 MB.", ct);

    /// <summary>O instante da publicação. Com <c>noDiaDoPagamento</c> e a empresa sem dia de pagamento (e sem exceção), 422 <c>EMPRESA_NAO_CONFIGURADA</c> com o texto da fatia 4.</summary>
    public async Task<DateTime> InstanteDaPublicacaoAsync(QuandoPublicarHolerite quando, DateOnly hoje, DateTime agoraUtc, CancellationToken ct)
    {
        DateOnly? dataDoPagamento = null;
        if (quando.NoDiaDoPagamento)
        {
            dataDoPagamento = (await proximoPagamento.ObterAsync(hoje, ct))?.Data ?? throw MensagensDaComunicacao.EmpresaSemDiaDePagamento.ComoProblema();
        }

        return PublicacaoDoHolerite.Instante(quando.PublicarEm, quando.NoDiaDoPagamento, hoje, agoraUtc, dataDoPagamento);
    }

    public async Task<HoleriteItemRh> GravarAsync(PessoaDoHolerite pessoa, CompetenciaDoHolerite competencia, int versao, DateTime publicacaoUtc, DateTime agoraUtc, ArquivoRecebido arquivo, TipoDeArquivo tipoDoArquivo, OperacaoDeHolerite operacao, CancellationToken ct)
    {
        var id = await envio.GravarEEfetivarAsync(
            arquivo,
            LimitesDeHolerite.PastaNoArmazenamento,
            tipoDoArquivo,
            caminho => unidade.ExecutarEmTransacaoAsync(
                async () =>
                {
                    var holerite = Holerite.Criar(pessoa.Id, competencia.Mes, competencia.Ano, competencia.Tipo, versao, publicacaoUtc, caminho);
                    var holeriteId = await repositorio.AdicionarAsync(holerite, operacao, ct);
                    await agendamento.AgendarAsync(holeriteId, publicacaoUtc, ct);
                    auditor.Registrar(new EventoDeAuditoriaNovo(
                        AuditoriaArea.Holerites,
                        AuditoriaAcao.Criou,
                        $"Enviou o holerite {MensagensDaComunicacao.TextoDaCompetencia(competencia.Tipo, competencia.Mes, competencia.Ano)} (versão {versao}) de {pessoa.Nome}.",
                        $"Holerite de {pessoa.Nome}",
                        "HOLERITES",
                        holeriteId));
                    await unidade.SalvarAsync(ct);
                    return holeriteId;
                },
                ct),
            ct);

        return new HoleriteItemRh(id, pessoa.Id, competencia.Tipo, competencia.Mes, competencia.Ano, versao, true, publicacaoUtc > agoraUtc ? SituacaoDoHolerite.Agendado : SituacaoDoHolerite.Publicado, publicacaoUtc);
    }
}

/// <summary>
/// <c>POST /api/rh/funcionarios/{funcionarioId}/holerites</c> (multipart): o RH publica ou agenda o PDF do holerite de uma pessoa (versão 1). Ordem dos erros: 404 (a pessoa, antes de qualquer efeito), 400 (campos e arquivo, todos juntos), 422 da competência
/// (anterior à admissão, posterior ao desligamento, futura fora de férias: conferidas pelo <c>IRelogio</c>, com o <c>regraDoBanco</c> do trigger), 422 <c>EMPRESA_NAO_CONFIGURADA</c> (no dia do pagamento sem dia configurado) e 409 <c>HOLERITE_JA_EXISTE</c>
/// (a conferência e, na corrida, a chave única). O arquivo só é guardado depois de tudo isso passar; o e-mail sai pela tarefa <c>PUBLICAR_HOLERITE</c> na hora em que o holerite fica visível.
/// </summary>
public sealed class EnviarHoleriteCasoDeUso(IConsultaDeHolerites consulta, PublicadorDeHolerite publicador, IRelogio relogio)
{
    public async Task<HoleriteItemRh> ExecutarAsync(int funcionarioId, NovoHolerite novo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var pessoa = (await consulta.ObterPessoaAsync(funcionarioId, ct)).OuNaoEncontrado();

        var erros = new ColetorDeErros();
        var competencia = LeitorDosCamposDoHolerite.LerCompetencia(erros, novo.Mes, novo.Ano, novo.Tipo);
        var quando = LeitorDosCamposDoHolerite.LerQuandoPublicar(erros, novo.PublicarEm, novo.NoDiaDoPagamento, hoje);
        var tipoDoArquivo = await PublicadorDeHolerite.ValidarPdfAsync(erros, novo.Arquivo, novo.ArquivosRecebidos, ct);
        erros.LancarSeHouver();

        if (RegraDaCompetenciaDoHolerite.Conferir(competencia!, hoje, pessoa.DataAdmissao, pessoa.DataDesligamento) is { } recusa)
        {
            throw recusa.Mensagem.ComoProblema(recusa.RegraDoBanco);
        }

        var publicacao = await publicador.InstanteDaPublicacaoAsync(quando!, hoje, agora, ct);
        if (await consulta.MaiorVersaoAsync(funcionarioId, competencia!.Ano, competencia.Mes, competencia.Tipo, ct) is not null)
        {
            throw MensagensDaComunicacao.HoleriteJaExiste(competencia.Tipo, competencia.Mes, competencia.Ano).ComoProblema();
        }

        return await publicador.GravarAsync(pessoa, competencia, 1, publicacao, agora, novo.Arquivo!, tipoDoArquivo!, OperacaoDeHolerite.Envio, ct);
    }
}

/// <summary>
/// <c>POST /api/rh/holerites/{holeriteId}/versoes</c> (multipart): corrige um holerite enviado errado guardando a <b>versão seguinte</b> (maior versão + 1) da mesma pessoa, competência e tipo; o banco não deixa apagar. <c>{holeriteId}</c> tem de ser a versão
/// atual: se já existe uma mais nova, 409 <c>HOLERITE_VERSAO_DESATUALIZADA</c> (a conferência e, na corrida, a chave única). Ordem: 404, 400, 409, 422 (a competência contra a pessoa e o dia do pagamento sem configuração). A pessoa passa a ver só a maior versão
/// já publicada; se a nova é agendada, a antiga segue valendo até o dia. Um novo e-mail sai quando a nova versão fica visível.
/// </summary>
public sealed class SubstituirHoleriteCasoDeUso(IConsultaDeHolerites consulta, PublicadorDeHolerite publicador, IRelogio relogio)
{
    public async Task<HoleriteItemRh> ExecutarAsync(int holeriteId, NovaVersaoDeHolerite novo, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var hoje = FusoBrasil.Hoje(agora);
        var atual = (await consulta.ObterAsync(holeriteId, ct)).OuNaoEncontrado();
        var pessoa = (await consulta.ObterPessoaAsync(atual.FuncionarioId, ct)).OuNaoEncontrado();

        var erros = new ColetorDeErros();
        var quando = LeitorDosCamposDoHolerite.LerQuandoPublicar(erros, novo.PublicarEm, novo.NoDiaDoPagamento, hoje);
        var tipoDoArquivo = await PublicadorDeHolerite.ValidarPdfAsync(erros, novo.Arquivo, novo.ArquivosRecebidos, ct);
        erros.LancarSeHouver();

        if (!atual.VersaoAtual)
        {
            throw MensagensDaComunicacao.HoleriteVersaoDesatualizada.ComoProblema();
        }

        var competencia = new CompetenciaDoHolerite(atual.Mes, atual.Ano, atual.Tipo);
        if (RegraDaCompetenciaDoHolerite.Conferir(competencia, hoje, pessoa.DataAdmissao, pessoa.DataDesligamento) is { } recusa)
        {
            throw recusa.Mensagem.ComoProblema(recusa.RegraDoBanco);
        }

        var publicacao = await publicador.InstanteDaPublicacaoAsync(quando!, hoje, agora, ct);
        return await publicador.GravarAsync(pessoa, competencia, atual.Versao + 1, publicacao, agora, novo.Arquivo!, tipoDoArquivo!, OperacaoDeHolerite.NovaVersao, ct);
    }
}
