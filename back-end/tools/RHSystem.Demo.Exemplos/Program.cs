using System.Text;
using RHSystem.Demo.Exemplos;

Console.OutputEncoding = Encoding.UTF8;

var (opcoes, mensagem) = OpcoesDaFerramenta.Ler(args);
if (opcoes is null)
{
    Console.WriteLine(mensagem);
    return OpcoesDaFerramenta.PediuAjuda(args) ? 0 : 2;
}

using var http = new HttpClient { BaseAddress = opcoes.Url, Timeout = TimeSpan.FromMinutes(2) };
var hoje = opcoes.Hoje ?? OpcoesDaFerramenta.HojeEmBrasilia(DateTime.UtcNow);
Console.WriteLine($"Enviando os exemplos para {opcoes.Url} (hoje = {hoje:dd/MM/yyyy})...");
try
{
    var relatorio = await new EnviadorDeExemplos(http, hoje, opcoes.Dominio, opcoes.Senha, opcoes.EmailDoRh).EnviarAsync();
    Console.WriteLine($"Holerites: {relatorio.HoleritesEnviados} enviados, {relatorio.HoleritesQueJaExistiam} já existiam, {relatorio.HoleritesRecusados} recusados pela API.");
    Console.WriteLine($"Atestados: {relatorio.AtestadosEnviados} enviado(s), {relatorio.AtestadosValidados} validado(s) pelo RH.");
    foreach (var aviso in relatorio.Avisos)
    {
        Console.WriteLine($"  aviso: {aviso}");
    }

    return 0;
}
catch (Exception erro) when (erro is InvalidOperationException or HttpRequestException or TaskCanceledException)
{
    Console.Error.WriteLine($"Não deu certo: {erro.Message}");
    return 1;
}
