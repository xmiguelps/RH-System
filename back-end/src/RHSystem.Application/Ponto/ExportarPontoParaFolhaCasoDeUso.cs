using System.Globalization;
using RHSystem.Application.Auditoria;
using RHSystem.Application.Autenticacao;
using RHSystem.Application.Comum;
using RHSystem.Application.Comum.Exportacao;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ponto.Regras;
using RHSystem.Domain.Auditoria;

namespace RHSystem.Application.Ponto;

/// <summary>Uma pessoa da folha: a matrícula (que o espelho não traz; <c>null</c> se não tem) e a pessoa como o espelho a conhece.</summary>
public sealed record PessoaDaFolha(string? Matricula, PessoaDoEspelho Pessoa);

/// <summary>Os crédito e débito do banco de horas de uma pessoa dentro do mês, em minutos inteiros (cada linha é convertida e depois somada).</summary>
public sealed record BancoDoMesDaPessoa(int CreditoMinutos, int DebitoMinutos);

/// <summary>O que a exportação para a folha lê do banco além do espelho.</summary>
public interface IConsultaDaFolha
{
    /// <summary>Quem esteve ativo em algum dia do período: admitido até o último dia e não desligado antes do primeiro, por nome.</summary>
    Task<IReadOnlyList<PessoaDaFolha>> PessoasDoPeriodoAsync(DateOnly primeiroDia, DateOnly ultimoDia, CancellationToken ct);

    /// <summary>Os lançamentos do banco de horas com <c>diaReferencia</c> dentro do período, por pessoa.</summary>
    Task<IReadOnlyDictionary<int, BancoDoMesDaPessoa>> BancoDoPeriodoAsync(DateOnly primeiroDia, DateOnly ultimoDia, CancellationToken ct);
}

/// <summary>
/// <c>GET /api/rh/ponto/folha/exportacao</c>: o CSV do mês para a folha, uma linha por pessoa que esteve ativa em algum dia do mês (admitida até o fim dele e não desligada antes do começo). Cada número sai do mesmo espelho que a tela mostra: <c>dias_trabalhados</c> são os
/// dias com período fechado, <c>horas_trabalhadas_min</c> a soma das horas, <c>horas_previstas_min</c> a das cargas, <c>atrasos</c> os dias com atraso, <c>faltas</c> os dias <c>SEM_REGISTRO</c> já passados, <c>faltas_compensadas</c> os de atestado de dia inteiro validado; o banco do
/// mês soma os lançamentos cujo dia de referência cai nele, crédito e débito em colunas separadas (nenhum número é negativo). Só o texto passa pela neutralização de <c>= + - @</c>. Cada exportação deixa um evento sem nome nem hora de ninguém.
/// </summary>
public sealed class ExportarPontoParaFolhaCasoDeUso(IConsultaDaFolha consulta, MontadorDoEspelho montador, IAuditor auditor, IRelogio relogio)
{
    public const int PrimeiroAno = 2000;
    public const int UltimoAno = 2100;

    public async Task<ArquivoExportado> ExecutarAsync(int? ano, int? mes, string? colunasPedidas, string? formatoPedido, string? separadorPedido, CancellationToken ct)
    {
        var erros = new ColetorDeErros();
        if (ano is null or < PrimeiroAno or > UltimoAno)
        {
            erros.Adicionar("ano", $"Informe o ano, de {PrimeiroAno} a {UltimoAno}.");
        }

        if (mes is null or < 1 or > 12)
        {
            erros.Adicionar("mes", "Informe o mês, de 1 a 12.");
        }

        var colunas = OpcoesDaFolha.LerColunas(erros, colunasPedidas);
        var formato = OpcoesDaFolha.LerFormato(erros, formatoPedido);
        var separador = OpcoesDaFolha.LerSeparador(erros, separadorPedido);
        erros.LancarSeHouver();

        var primeiroDia = new DateOnly(ano!.Value, mes!.Value, 1);
        var ultimoDia = primeiroDia.AddMonths(1).AddDays(-1);
        var hoje = FusoBrasil.Hoje(relogio.AgoraUtc);
        var pessoas = await consulta.PessoasDoPeriodoAsync(primeiroDia, ultimoDia, ct);
        var banco = await consulta.BancoDoPeriodoAsync(primeiroDia, ultimoDia, ct);

        var linhas = new List<IReadOnlyList<string>>();
        foreach (var pessoa in pessoas)
        {
            var dias = (await montador.MontarAsync(pessoa.Pessoa, ano, mes, ct)).Dias;
            var doBanco = banco.GetValueOrDefault(pessoa.Pessoa.Id) ?? new BancoDoMesDaPessoa(0, 0);

            // Todas as células, na ordem de ColunaDaFolha; a linha leva só as colunas pedidas, na ordem pedida.
            string[] celulas =
            [
                CsvSeguro.Texto(pessoa.Matricula),
                CsvSeguro.Texto(pessoa.Pessoa.Nome),
                CsvSeguro.Texto(pessoa.Pessoa.Setor),
                CsvSeguro.Texto(pessoa.Pessoa.Cargo),
                CsvSeguro.Numero(dias.Count(d => d.Periodos.Any(p => p.Entrada is not null && p.Saida is not null))),
                OpcoesDaFolha.Horas(dias.Sum(d => d.HorasMinutos ?? 0), formato, separador),
                OpcoesDaFolha.Horas(dias.Sum(d => d.CargaMinutos), formato, separador),
                CsvSeguro.Numero(dias.Count(d => d.AtrasoMinutos is not null)),
                CsvSeguro.Numero(dias.Count(d => d.Data < hoje && d.Situacao == SituacaoDoDiaNoPonto.SemRegistro)),
                CsvSeguro.Numero(dias.Count(d => d.Situacao == SituacaoDoDiaNoPonto.FaltaCompensada)),
                OpcoesDaFolha.Horas(doBanco.CreditoMinutos, formato, separador),
                OpcoesDaFolha.Horas(doBanco.DebitoMinutos, formato, separador),
            ];
            linhas.Add([.. colunas.Select(c => celulas[(int)c])]);
        }

        var competencia = string.Create(CultureInfo.InvariantCulture, $"{mes:00}/{ano:0000}");
        await auditor.RegistrarEGravarAsync(
            new EventoDeAuditoriaNovo(AuditoriaArea.Ponto, AuditoriaAcao.Baixou, $"Exportou o ponto de {competencia} para a folha (CSV).", $"Ponto de {competencia} (folha)"),
            ct);
        return new ArquivoExportado(CsvSeguro.Escrever([.. colunas.Select(c => OpcoesDaFolha.Titulo(c, formato))], linhas, OpcoesDaFolha.Caractere(separador)), string.Create(CultureInfo.InvariantCulture, $"ponto-{ano:0000}-{mes:00}.csv"));
    }
}
