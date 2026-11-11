using System.Globalization;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum.Consultas;
using RHSystem.Application.Comum.Exportacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Auditoria;

/// <summary>
/// O CSV do log filtrado (botão "Exportar CSV"): os mesmos filtros da lista, sem paginação. UTF-8 com BOM, separador <c>;</c>, <c>Quando</c> em -03:00
/// (<c>dd/MM/yyyy HH:mm:ss</c>), rótulos da tela no lugar dos códigos, e TODA célula de texto passa por <see cref="CsvSeguro.Texto"/> (célula que começa
/// com <c>=</c>, <c>+</c>, <c>-</c> ou <c>@</c> ganha um apóstrofo). A exportação entra no log (<c>Acesso/Baixou</c>), gravada DEPOIS de gerar o arquivo, então
/// não aparece nele. Se gravar o evento falhar, o arquivo NÃO sai: levar o log inteiro sem deixar rastro derrotaria o motivo do log (diferente do login e do
/// 403, onde o evento é só registro).
/// </summary>
public sealed class ExportarAuditoriaCasoDeUso(IConsultaDeAuditoria consulta, IAuditor auditor, IRelogio relogio)
{
    private static readonly string[] Titulos = ["Quando", "Quem", "Perfil", "Área", "Ação", "O que aconteceu", "Registro afetado", "Origem", "IP"];

    public async Task<ArquivoExportado> ExecutarAsync(PedidoDeAuditoria pedido, CancellationToken ct)
    {
        var agora = relogio.AgoraUtc;
        var (filtro, _) = ValidacaoDeAuditoria.Validar(pedido, new ParametrosDeLista(Busca: pedido.Busca), agora);
        var linhas = await consulta.ListarParaExportacaoAsync(filtro, ct);
        var conteudo = CsvSeguro.Escrever(Titulos, linhas.Select(Celulas));

        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Acesso, AuditoriaAcao.Baixou, "Exportou o log de auditoria em CSV", "Log de auditoria"),
            ct);
        return new ArquivoExportado(conteudo, $"auditoria-{FusoBrasil.Hoje(agora).ToString("yyyy-MM-dd", CultureInfo.InvariantCulture)}.csv");
    }

    private static IReadOnlyList<string> Celulas(LinhaDeExportacaoDeAuditoria linha) =>
    [
        CsvSeguro.Texto(FusoBrasil.ParaLocal(linha.QuandoUtc).ToString("dd/MM/yyyy HH:mm:ss", CultureInfo.InvariantCulture)),
        CsvSeguro.Texto(linha.Quem),
        CsvSeguro.Texto(linha.Tipo == TipoDeAtor.Sistema ? RotulosDeAuditoria.Origem(CanalDeOrigem.Sistema) : RotulosDeAuditoria.Perfil(linha.Perfil)),
        CsvSeguro.Texto(RotulosDeAuditoria.Area(linha.Area)),
        CsvSeguro.Texto(RotulosDeAuditoria.Acao(linha.Acao)),
        CsvSeguro.Texto(linha.Descricao),
        CsvSeguro.Texto(linha.RegistroAfetado),
        CsvSeguro.Texto(RotulosDeAuditoria.Origem(linha.Canal)),
        CsvSeguro.Texto(linha.Ip),
    ];
}
