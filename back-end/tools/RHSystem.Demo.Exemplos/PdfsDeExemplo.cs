using System.Globalization;
using System.Text;

namespace RHSystem.Demo.Exemplos;

/// <summary>
/// Os PDFs de exemplo da demo, escritos à mão (cabeçalho, objetos, tabela de referências cruzadas): só texto, em Helvetica padrão do PDF e codificação Latin-1, que cobre todos os acentos do português. Sem biblioteca, sem fonte do sistema e sem nada que mude entre uma máquina e outra: o mesmo pedido gera os mesmos
/// bytes. <b>Tudo é inventado</b> e cada folha diz isso; os valores do holerite são só ilustrativos.
/// </summary>
public static class PdfsDeExemplo
{
    private static readonly string[] Meses = ["Janeiro", "Fevereiro", "Março", "Abril", "Maio", "Junho", "Julho", "Agosto", "Setembro", "Outubro", "Novembro", "Dezembro"];

    /// <summary>Um holerite mensal de uma página, com proventos, descontos e líquido.</summary>
    public static byte[] Holerite(string nome, string cargo, string setor, int mes, int ano)
    {
        var (bruto, inss, irrf, liquido) = ValoresDoHolerite(nome);
        var folha = new Folha();
        folha.Linha("HOLERITE (EXEMPLO)", 18, negrito: true);
        folha.Linha("Empresa Demonstração Ltda. - CNPJ fictício", 11);
        folha.Espaco(6);
        folha.Linha($"Competência: {Meses[mes - 1]}/{ano} - folha mensal", 12, negrito: true);
        folha.Regua();
        folha.Linha($"Funcionário: {nome}", 12);
        folha.Linha($"Cargo: {cargo}", 12);
        folha.Linha($"Setor: {setor}", 12);
        folha.Regua();
        folha.Par("Proventos", Reais(bruto), negrito: true);
        folha.Par("   Salário base", Reais(bruto));
        folha.Espaco(4);
        folha.Par("Descontos", Reais(inss + irrf), negrito: true);
        folha.Par("   INSS (valor ilustrativo)", Reais(inss));
        folha.Par("   IRRF (valor ilustrativo)", Reais(irrf));
        folha.Regua();
        folha.Par("Líquido a receber", Reais(liquido), negrito: true);
        folha.Espaco(24);
        folha.Linha("Documento fictício, gerado para a demonstração do RH System.", 9);
        folha.Linha("Os valores são ilustrativos e não têm validade fiscal nem trabalhista.", 9);
        return folha.Montar();
    }

    /// <summary>Um atestado de uma página para dias inteiros. Não traz diagnóstico: o sistema não guarda CID.</summary>
    public static byte[] Atestado(string nome, DateOnly inicio, DateOnly fim)
    {
        var dias = fim.DayNumber - inicio.DayNumber + 1;
        var folha = new Folha();
        folha.Linha("ATESTADO MÉDICO (MODELO FICTÍCIO)", 18, negrito: true);
        folha.Espaco(10);
        folha.Linha("Atesto, para os devidos fins, que a pessoa abaixo precisa se afastar", 12);
        folha.Linha("das atividades profissionais no período indicado.", 12);
        folha.Espaco(8);
        folha.Linha($"Nome: {nome}", 12, negrito: true);
        folha.Linha($"Período: {Data(inicio)} a {Data(fim)} ({dias} {(dias == 1 ? "dia" : "dias")})", 12);
        folha.Linha("CID não informado.", 12);
        folha.Regua();
        folha.Linha($"São Paulo, {Data(inicio)}", 12);
        folha.Espaco(20);
        folha.Linha("Dra. Maria Exemplo - CRM 000000/SP (fictício)", 12);
        folha.Espaco(24);
        folha.Linha("Documento fictício, gerado para a demonstração do RH System, sem valor médico nem legal.", 9);
        return folha.Montar();
    }

    /// <summary>Salário bruto, INSS e IRRF ilustrativos e o líquido. O bruto vem do nome (soma dos caracteres, igual em qualquer máquina), entre R$ 3.000 e R$ 8.900; os descontos são 9% e 7% do bruto.</summary>
    public static (decimal Bruto, decimal Inss, decimal Irrf, decimal Liquido) ValoresDoHolerite(string nome)
    {
        var soma = 0;
        foreach (var letra in nome)
        {
            soma += letra;
        }

        var bruto = 3000m + (soma % 60) * 100m;
        var inss = decimal.Round(bruto * 0.09m, 2);
        var irrf = decimal.Round(bruto * 0.07m, 2);
        return (bruto, inss, irrf, bruto - inss - irrf);
    }

    /// <summary>Valor em reais no formato brasileiro (<c>R$ 1.234,50</c>), sem depender da cultura da máquina.</summary>
    public static string Reais(decimal valor)
    {
        var texto = valor.ToString("N2", CultureInfo.InvariantCulture); // 1,234.50
        return "R$ " + texto.Replace(',', '#').Replace('.', ',').Replace('#', '.');
    }

    private static string Data(DateOnly dia) => dia.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Uma folha A4 que vai sendo preenchida de cima para baixo e vira o arquivo PDF.</summary>
    private sealed class Folha
    {
        private const double Margem = 56;
        private const double Largura = 595;
        private const double Altura = 842;

        private readonly StringBuilder _conteudo = new();
        private double _y = Altura - 72;

        public void Linha(string texto, double tamanho, bool negrito = false)
        {
            Texto(Margem, texto, tamanho, negrito);
            _y -= tamanho + 7;
        }

        /// <summary>Um rótulo à esquerda e o valor numa coluna fixa à direita (a Helvetica não tem largura fixa, então os valores começam todos na mesma coluna, sem alinhar pelo fim).</summary>
        public void Par(string rotulo, string valor, bool negrito = false)
        {
            Texto(Margem, rotulo, 12, negrito);
            Texto(Largura - Margem - 110, valor, 12, negrito);
            _y -= 19;
        }

        public void Espaco(double pontos) => _y -= pontos;

        public void Regua()
        {
            _y -= 4;
            _conteudo.Append(CultureInfo.InvariantCulture, $"{Margem:0.##} {_y:0.##} {Largura - 2 * Margem:0.##} 0.6 re f\n");
            _y -= 16;
        }

        public byte[] Montar()
        {
            var fluxo = Encoding.Latin1.GetBytes(_conteudo.ToString());
            byte[] objetoDoConteudo = [.. Encoding.Latin1.GetBytes($"<< /Length {fluxo.Length} >>\nstream\n"), .. fluxo, .. Encoding.Latin1.GetBytes("\nendstream")];
            var objetos = new List<byte[]>
            {
                Encoding.Latin1.GetBytes("<< /Type /Catalog /Pages 2 0 R >>"),
                Encoding.Latin1.GetBytes("<< /Type /Pages /Kids [3 0 R] /Count 1 >>"),
                Encoding.Latin1.GetBytes($"<< /Type /Page /Parent 2 0 R /MediaBox [0 0 {Largura} {Altura}] /Resources << /Font << /F1 5 0 R /F2 6 0 R >> >> /Contents 4 0 R >>"),
                objetoDoConteudo,
                Encoding.Latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica /Encoding /WinAnsiEncoding >>"),
                Encoding.Latin1.GetBytes("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica-Bold /Encoding /WinAnsiEncoding >>"),
            };

            using var saida = new MemoryStream();
            Escrever(saida, "%PDF-1.4\n");
            var posicoes = new List<long>();
            for (var i = 0; i < objetos.Count; i++)
            {
                posicoes.Add(saida.Position);
                Escrever(saida, $"{i + 1} 0 obj\n");
                saida.Write(objetos[i]);
                Escrever(saida, "\nendobj\n");
            }

            var inicioDaTabela = saida.Position;
            Escrever(saida, $"xref\n0 {objetos.Count + 1}\n0000000000 65535 f \n");
            foreach (var posicao in posicoes)
            {
                Escrever(saida, $"{posicao:D10} 00000 n \n");
            }

            Escrever(saida, $"trailer\n<< /Size {objetos.Count + 1} /Root 1 0 R >>\nstartxref\n{inicioDaTabela}\n%%EOF\n");
            return saida.ToArray();
        }

        private void Texto(double x, string texto, double tamanho, bool negrito) =>
            _conteudo.Append(CultureInfo.InvariantCulture, $"BT /{(negrito ? "F2" : "F1")} {tamanho:0.##} Tf {x:0.##} {_y:0.##} Td ({Escapar(texto)}) Tj ET\n");

        private static void Escrever(Stream saida, string texto) => saida.Write(Encoding.Latin1.GetBytes(texto));

        /// <summary>Barra invertida e parênteses viram escape. O que o Latin-1 não tem, o <see cref="Encoding.Latin1"/> do .NET troca pelo parente mais próximo (aspas e travessões tipográficos viram os retos) ou por <c>?</c>; o teste do PDF prova os dois casos.</summary>
        private static string Escapar(string texto) => texto.Replace("\\", "\\\\", StringComparison.Ordinal).Replace("(", "\\(", StringComparison.Ordinal).Replace(")", "\\)", StringComparison.Ordinal);
    }
}
