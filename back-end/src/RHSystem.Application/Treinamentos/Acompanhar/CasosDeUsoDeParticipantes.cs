using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Exportacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Formularios.Regras;
using RHSystem.Application.Treinamentos.Construtor;
using RHSystem.Application.Treinamentos.Regras;
using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Treinamentos;

namespace RHSystem.Application.Treinamentos.Acompanhar;

/// <summary>O que as duas rotas de "Quem fez" leem do pedido: os filtros e a ordem, com os erros juntos num 400.</summary>
internal static class PedidoDeParticipantes
{
    public static readonly string[] ValoresDeOrdenarPor = ["NOME", "SETOR", "SITUACAO", "INICIO", "CONCLUSAO"];

    public static (ParametrosDeLista Parametros, FiltroDeParticipantes Filtro) Ler(int pagina, int tamanhoPagina, string? busca, string? situacao, int? setorId, string? ordenarPor, string? direcao, bool comOrdem)
    {
        var erros = new ColetorDeErros();
        ParametrosDeLista? paginacao = null;
        SituacaoNoTreinamento? situacaoLida = null;
        DirecaoDeOrdenacao? direcaoLida = null;
        LeituraDeFiltros.Tentar(erros, () => paginacao = new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado());
        LeituraDeFiltros.Tentar(erros, () => situacaoLida = EnumsDeConsulta.Ler<SituacaoNoTreinamento>(situacao, "situacao"));
        if (comOrdem)
        {
            LeituraDeFiltros.Tentar(erros, () => direcaoLida = EnumsDeConsulta.Ler<DirecaoDeOrdenacao>(direcao, "direcao"));
            if (!string.IsNullOrEmpty(ordenarPor) && !ValoresDeOrdenarPor.Contains(ordenarPor, StringComparer.Ordinal))
            {
                erros.Adicionar("ordenarPor", $"Valor inválido. Use um destes: {string.Join(", ", ValoresDeOrdenarPor)}.");
            }
        }

        erros.LancarSeHouver();
        return (paginacao! with { OrdenarPor = comOrdem && !string.IsNullOrEmpty(ordenarPor) ? ordenarPor : null, Direcao = direcaoLida ?? DirecaoDeOrdenacao.Asc }, new FiltroDeParticipantes(situacaoLida, setorId));
    }
}

/// <summary>
/// <c>GET /api/rh/treinamentos/{treinamentoId}/participantes</c>: "Quem fez" (só o RH). Só quem tem linha (obrigatório: o público inteiro; opcional: quem se inscreveu); inativos continuam; rascunho = lista vazia; encerrado continua consultável. <c>situacao</c> é a aba, <c>setorId</c> o filtro de setor, <c>busca</c> procura no nome; as <c>contagens</c> ignoram a aba e respeitam setor e busca. <c>ordenarPor</c> (<c>NOME</c> padrão,
/// <c>SETOR</c>, <c>SITUACAO</c>, <c>INICIO</c>, <c>CONCLUSAO</c>) e <c>direcao</c> (padrão <c>ASC</c>). 404 se o treinamento não existe; 400 com todos os erros juntos.
/// </summary>
public sealed class ListarParticipantesDeTreinamentoCasoDeUso(IConsultaDeTreinamentos treinamentos, IConsultaDeParticipantes consulta, IRelogio relogio)
{
    public async Task<TreinamentoParticipantesLista> ExecutarAsync(int treinamentoId, int pagina, int tamanhoPagina, string? busca, string? situacao, int? setorId, string? ordenarPor, string? direcao, CancellationToken ct)
    {
        var (parametros, filtro) = PedidoDeParticipantes.Ler(pagina, tamanhoPagina, busca, situacao, setorId, ordenarPor, direcao, comOrdem: true);
        var treinamento = (await treinamentos.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        var cabecalho = new TreinamentoParticipantesCabecalho(treinamento.Id, treinamento.Titulo, treinamento.Obrigatorio, treinamento.Onboarding, treinamento.EmiteCertificado, treinamento.Situacao(FusoBrasil.Hoje(relogio.AgoraUtc)), treinamento.ComecaEm, treinamento.EncerradoEmUtc, treinamento.Publico, treinamento.TotalDeModulos);
        if (!treinamento.Publicado)
        {
            return new TreinamentoParticipantesLista(cabecalho, new TreinamentoParticipantesContagens(0, 0, 0, 0), [], parametros.Pagina, parametros.TamanhoPagina, 0, 0);
        }

        var (itens, total, contagens) = await consulta.ListarAsync(treinamentoId, treinamento.EmiteCertificado, filtro, parametros, paginar: true, ct);
        return new TreinamentoParticipantesLista(cabecalho, contagens, itens, parametros.Pagina, parametros.TamanhoPagina, total, (int)(((long)total + parametros.TamanhoPagina - 1) / parametros.TamanhoPagina));
    }
}

/// <summary>
/// <c>GET /api/rh/treinamentos/{treinamentoId}/participantes/exportacao</c>: a lista de "Quem fez" em CSV (UTF-8 com BOM, <c>;</c>, <c>Pessoa;Setor;Situação;Início;Conclusão</c>, datas <c>dd/MM/aaaa</c> em -03:00, situação em português: Concluído, Em andamento, Não iniciado), TODAS as linhas dos mesmos filtros (<c>situacao</c>, <c>setorId</c>, <c>busca</c>), por nome. Toda célula de texto passa
/// por <see cref="CsvSeguro.Texto"/> (a que começa com <c>=</c>, <c>+</c>, <c>-</c> ou <c>@</c> é neutralizada). Nome do arquivo <c>treinamento-&lt;título&gt;.csv</c>. Evento <c>BAIXOU</c> com o título, sem nomes; se gravar o evento falhar o arquivo não sai.
/// </summary>
public sealed class ExportarParticipantesDeTreinamentoCasoDeUso(IConsultaDeTreinamentos treinamentos, IConsultaDeParticipantes consulta, IAuditor auditor)
{
    private static readonly string[] Titulos = ["Pessoa", "Setor", "Situação", "Início", "Conclusão"];

    public async Task<ArquivoExportado> ExecutarAsync(int treinamentoId, string? busca, string? situacao, int? setorId, CancellationToken ct)
    {
        var (parametros, filtro) = PedidoDeParticipantes.Ler(1, ParametrosDeLista.TamanhoMaximoDaPagina, busca, situacao, setorId, null, null, comOrdem: false);
        var treinamento = (await treinamentos.ObterAsync(treinamentoId, ct)).OuNaoEncontrado();
        IReadOnlyList<TreinamentoParticipante> itens = treinamento.Publicado ? (await consulta.ListarAsync(treinamentoId, treinamento.EmiteCertificado, filtro, parametros with { OrdenarPor = "NOME", Direcao = DirecaoDeOrdenacao.Asc }, paginar: false, ct)).Itens : [];
        var conteudo = CsvSeguro.Escrever(Titulos, itens.Select(Celulas));
        await auditor.RegistrarEGravarAsync(new EventoDeAuditoriaNovo(AuditoriaArea.Treinamentos, AuditoriaAcao.Baixou, $"Exportou em CSV quem fez o treinamento \"{treinamento.Titulo}\".", $"Treinamento \"{treinamento.Titulo}\"", "TREINAMENTOS", treinamento.Id), ct);
        return new ArquivoExportado(conteudo, NomesDeArquivoDoTreinamento.CsvDosParticipantes(treinamento.Titulo));
    }

    private static IReadOnlyList<string> Celulas(TreinamentoParticipante p) =>
    [
        CsvSeguro.Texto(p.Funcionario.Nome),
        CsvSeguro.Texto(p.Funcionario.Setor),
        CsvSeguro.Texto(Rotulo(p.Situacao)),
        p.IniciadoEm is { } inicio ? inicio.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty,
        p.ConcluidoEmUtc is { } conclusao ? FusoBrasil.ParaLocal(conclusao).ToString("dd/MM/yyyy", CultureInfo.InvariantCulture) : string.Empty,
    ];

    public static string Rotulo(SituacaoNoTreinamento situacao) => situacao switch
    {
        SituacaoNoTreinamento.Concluido => "Concluído",
        SituacaoNoTreinamento.EmAndamento => "Em andamento",
        _ => "Não iniciado",
    };
}
