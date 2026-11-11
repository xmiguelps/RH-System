using System.Globalization;
using MigraDoc.DocumentObjectModel;
using MigraDoc.DocumentObjectModel.Tables;
using MigraDoc.Rendering;
using RHSystem.Application.Comum.Tempo;
using RHSystem.Application.Ponto;
using RHSystem.Application.Ponto.Regras;

namespace RHSystem.Infrastructure.Ponto;

/// <summary>
/// O espelho do mês em PDF (PDFsharp e MigraDoc, licença MIT), com o mesmo conteúdo da tela: o cabeçalho (empresa, colaborador, cargo, setor, jornada), o resumo do mês e a tabela dos dias (situação, períodos de entrada e saída em -03:00, horas, carga, saldo e atraso). A fonte é a
/// embutida (<see cref="FonteDoPdf"/>), então os acentos saem certos em qualquer máquina; o PDF é montado na memória e a tabela se parte em páginas sozinha (o cabeçalho dela se repete).
/// </summary>
public sealed class GeradorDePdfDoEspelho : IGeradorDePdfDoEspelho
{
    private const string SemValor = "–";

    private static readonly string[] Meses = ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"];

    public byte[] Gerar(EspelhoParaPdf entrada)
    {
        FonteDoPdf.Registrar();
        var renderizador = new PdfDocumentRenderer { Document = Montar(entrada) };
        renderizador.RenderDocument();
        using var saida = new MemoryStream();
        renderizador.PdfDocument.Save(saida, false);
        return saida.ToArray();
    }

    private static Document Montar(EspelhoParaPdf entrada)
    {
        var espelho = entrada.Espelho;
        var competencia = string.Create(CultureInfo.InvariantCulture, $"{Meses[espelho.Mes - 1]} de {espelho.Ano:0000}");
        var documento = new Document();
        documento.Info.Title = $"Espelho de ponto de {espelho.Pessoa.Nome} - {competencia}";
        documento.Info.Author = "RH System";

        var normal = documento.Styles[StyleNames.Normal]!;
        normal.Font.Name = FonteDoPdf.Familia;
        normal.Font.Size = 9;

        var secao = documento.AddSection();
        secao.PageSetup.PageFormat = PageFormat.A4;
        secao.PageSetup.Orientation = Orientation.Portrait;
        secao.PageSetup.LeftMargin = Unit.FromCentimeter(1.5);
        secao.PageSetup.RightMargin = Unit.FromCentimeter(1.5);
        secao.PageSetup.TopMargin = Unit.FromCentimeter(1.5);
        secao.PageSetup.BottomMargin = Unit.FromCentimeter(1.8);

        var rodape = secao.Footers.Primary.AddParagraph();
        rodape.Format.Font.Size = 8;
        rodape.AddText(string.Create(CultureInfo.InvariantCulture, $"Gerado em {FusoBrasil.ParaLocal(entrada.GeradoEmUtc):dd/MM/yyyy HH:mm} (horário de Brasília) · Página "));
        rodape.AddPageField();
        rodape.AddText(" de ");
        rodape.AddNumPagesField();

        var titulo = secao.AddParagraph("Espelho de ponto");
        titulo.Format.Font.Size = 16;
        titulo.Format.Font.Bold = true;
        var mes = secao.AddParagraph(competencia);
        mes.Format.Font.Size = 11;
        mes.Format.SpaceAfter = Unit.FromPoint(8);

        if (entrada.Empresa is { } empresa)
        {
            Linha(secao, "Empresa", $"{empresa.NomeEmpresa} · {empresa.Municipio}/{empresa.Uf}");
        }

        Linha(secao, "Colaborador", espelho.Pessoa.Nome);
        Linha(secao, "Cargo", espelho.Pessoa.Cargo ?? SemValor);
        Linha(secao, "Setor", espelho.Pessoa.Setor ?? SemValor);
        Linha(secao, "Jornada", Jornada(espelho.Jornada));
        Linha(secao, "Resumo do mês", string.Create(CultureInfo.InvariantCulture, $"{Duracao(espelho.Resumo.HorasTrabalhadasNoMesMinutos)} trabalhadas · saldo do banco de horas {Saldo(espelho.Resumo.SaldoBancoHorasMinutos)} · {espelho.Resumo.Atrasos} atraso(s) · {espelho.Resumo.Faltas} falta(s)"));

        var espaco = secao.AddParagraph();
        espaco.Format.SpaceAfter = Unit.FromPoint(6);

        var tabela = secao.AddTable();
        tabela.Borders.Width = 0.25;
        tabela.Borders.Color = Colors.Gray;
        tabela.Rows.LeftIndent = 0;
        foreach (var largura in new[] { 2.6, 3.8, 5.0, 1.6, 1.6, 1.7, 1.7 })
        {
            tabela.AddColumn(Unit.FromCentimeter(largura));
        }

        var cabecalho = tabela.AddRow();
        cabecalho.HeadingFormat = true;
        cabecalho.Shading.Color = Colors.LightGray;
        cabecalho.Format.Font.Bold = true;
        string[] titulos = ["Dia", "Situação", "Entradas e saídas", "Horas", "Carga", "Saldo", "Atraso"];
        for (var i = 0; i < titulos.Length; i++)
        {
            cabecalho.Cells[i].AddParagraph(titulos[i]);
        }

        foreach (var dia in espelho.Dias)
        {
            var linha = tabela.AddRow();
            linha.Cells[0].AddParagraph(string.Create(CultureInfo.InvariantCulture, $"{dia.Data:dd/MM} {DiaDaSemana(dia.DiaDaSemana)}"));
            linha.Cells[1].AddParagraph(Situacao(dia));
            linha.Cells[2].AddParagraph(Periodos(dia));
            linha.Cells[3].AddParagraph(Duracao(dia.HorasMinutos));
            linha.Cells[4].AddParagraph(Duracao(dia.CargaMinutos));
            linha.Cells[5].AddParagraph(Saldo(dia.SaldoMinutos));
            linha.Cells[6].AddParagraph(dia.AtrasoMinutos is { } atraso ? $"{atraso} min" : SemValor);
            if (dia.EhHoje)
            {
                linha.Shading.Color = Color.FromRgb(232, 240, 254);
            }
        }

        return documento;
    }

    private static void Linha(Section secao, string rotulo, string valor)
    {
        var paragrafo = secao.AddParagraph();
        paragrafo.Format.SpaceAfter = Unit.FromPoint(2);
        var negrito = paragrafo.AddFormattedText($"{rotulo}: ");
        negrito.Bold = true;
        paragrafo.AddText(valor);
    }

    private static string Jornada(PontoJornada jornada)
    {
        if (jornada.CargaDiariaMinutos is null)
        {
            return "não configurada";
        }

        var horario = jornada.HorarioEntrada is { } entrada && jornada.HorarioSaida is { } saida ? $" ({entrada} às {saida})" : string.Empty;
        return $"{Duracao(jornada.CargaDiariaMinutos)} por dia{horario}";
    }

    private static string Periodos(DiaDoEspelho dia) =>
        dia.Periodos.Count == 0
            ? SemValor
            : string.Join("  ·  ", dia.Periodos.Select(p => string.Create(CultureInfo.InvariantCulture, $"{Hora(p.Entrada?.InstanteUtc)} – {Hora(p.Saida?.InstanteUtc)}")));

    private static string Hora(DateTime? instanteUtc) => instanteUtc is { } instante ? FusoBrasil.ParaLocal(instante).ToString("HH:mm", CultureInfo.InvariantCulture) : "?";

    private static string Duracao(int? minutos) => minutos is { } m ? string.Create(CultureInfo.InvariantCulture, $"{m / 60}:{m % 60:00}") : SemValor;

    private static string Saldo(int? minutos) =>
        minutos is not { } m ? SemValor : m == 0 ? "0:00" : string.Create(CultureInfo.InvariantCulture, $"{(m > 0 ? "+" : "-")}{Math.Abs(m) / 60}:{Math.Abs(m) % 60:00}");

    private static string DiaDaSemana(DiaDaSemanaDoPonto dia) => dia switch
    {
        DiaDaSemanaDoPonto.Segunda => "Seg",
        DiaDaSemanaDoPonto.Terca => "Ter",
        DiaDaSemanaDoPonto.Quarta => "Qua",
        DiaDaSemanaDoPonto.Quinta => "Qui",
        DiaDaSemanaDoPonto.Sexta => "Sex",
        DiaDaSemanaDoPonto.Sabado => "Sáb",
        _ => "Dom",
    };

    private static string Situacao(DiaDoEspelho dia) => dia.Situacao switch
    {
        SituacaoDoDiaNoPonto.Normal => "Normal",
        SituacaoDoDiaNoPonto.Atraso => "Atraso",
        SituacaoDoDiaNoPonto.Incompleto => "Incompleto",
        SituacaoDoDiaNoPonto.AjusteEmAnalise => "Ajuste em análise",
        SituacaoDoDiaNoPonto.EmAndamento => "Em andamento",
        SituacaoDoDiaNoPonto.SemRegistro => "Sem registro",
        SituacaoDoDiaNoPonto.Folga => "Folga",
        SituacaoDoDiaNoPonto.Feriado => dia.NomeFeriado is { } nome ? $"Feriado: {nome}" : "Feriado",
        SituacaoDoDiaNoPonto.Ferias => "Férias",
        _ => "Falta compensada",
    };
}
