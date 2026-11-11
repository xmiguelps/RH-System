using System.Text.RegularExpressions;

namespace RHSystem.Api.IntegrationTests.Auditoria;

/// <summary>
/// O interceptor do EF só vê o que passa pelo <c>SaveChanges</c>: <c>ExecuteUpdate</c>, <c>ExecuteDelete</c> e <c>ExecuteSql*</c> escrevem sem que ele
/// perceba. Por isso toda chamada dessas, em <c>back-end/src</c>, leva um comentário <c>AUDITORIA-OK: motivo</c> até 3 linhas acima: quem a escreve
/// decidiu de propósito que ela não vira evento (ou registra o evento à mão, com <c>IAuditor.Registrar</c>) e disse por quê.
/// </summary>
public partial class GuardaDeExecuteUpdateTests
{
    [GeneratedRegex(@"\.\s*(ExecuteUpdate|ExecuteDelete|ExecuteSql(Raw|Interpolated)?)(Async)?\s*[(<]")]
    private static partial Regex ChamadaEscritora();

    [GeneratedRegex(@"AUDITORIA-OK:\s*\S")]
    private static partial Regex Comentario();

    /// <summary>As linhas (1 a N) com a chamada que não tem o comentário com motivo na própria linha nem nas 3 de cima. Linha só de comentário não conta como chamada.</summary>
    internal static List<int> LinhasSemComentario(IReadOnlyList<string> linhas)
    {
        var sem = new List<int>();
        for (var i = 0; i < linhas.Count; i++)
        {
            var linha = linhas[i].TrimStart();
            if (linha.StartsWith("//", StringComparison.Ordinal) || !ChamadaEscritora().IsMatch(linhas[i]))
            {
                continue;
            }

            var janela = linhas.Skip(Math.Max(0, i - 3)).Take(Math.Min(i, 3) + 1);
            if (!janela.Any(l => Comentario().IsMatch(l)))
            {
                sem.Add(i + 1);
            }
        }

        return sem;
    }

    private static string PastaDoCodigoDoBackEnd()
    {
        for (var pasta = new DirectoryInfo(AppContext.BaseDirectory); pasta is not null; pasta = pasta.Parent)
        {
            var candidato = Path.Combine(pasta.FullName, "back-end", "src");
            if (Directory.Exists(candidato))
            {
                return candidato;
            }
        }

        throw new DirectoryNotFoundException("back-end/src não foi encontrado subindo de " + AppContext.BaseDirectory);
    }

    [Fact]
    public void Todo_ExecuteUpdate_ExecuteDelete_e_ExecuteSql_tem_o_comentario_AUDITORIA_OK_com_o_motivo()
    {
        var raiz = PastaDoCodigoDoBackEnd();
        var arquivos = Directory.EnumerateFiles(raiz, "*.cs", SearchOption.AllDirectories)
            .Where(a => !a.Contains($"{Path.DirectorySeparatorChar}obj{Path.DirectorySeparatorChar}") && !a.Contains($"{Path.DirectorySeparatorChar}bin{Path.DirectorySeparatorChar}"))
            .ToList();
        var chamadas = 0;
        var faltando = new List<string>();
        foreach (var arquivo in arquivos)
        {
            var linhas = File.ReadAllLines(arquivo);
            chamadas += linhas.Count(l => !l.TrimStart().StartsWith("//", StringComparison.Ordinal) && ChamadaEscritora().IsMatch(l));
            faltando.AddRange(LinhasSemComentario(linhas).Select(n => $"{Path.GetRelativePath(raiz, arquivo)}:{n}"));
        }

        Assert.True(chamadas >= 4, $"controle positivo: o guarda achou só {chamadas} chamada(s) em {arquivos.Count} arquivo(s); esperava ao menos as 4 que existem hoje");
        Assert.True(faltando.Count == 0, "Chamadas sem AUDITORIA-OK com motivo (até 3 linhas acima): " + string.Join("; ", faltando));
    }

    [Fact]
    public void O_detector_acusa_chamada_sem_comentario_e_aceita_com_motivo()
    {
        // Controle do próprio guarda (sem ele, "nenhuma chamada sem comentário" poderia ser só um detector que não detecta).
        string[] semComentario = ["var n = await db.Itens.Where(i => i.X).ExecuteUpdateAsync(s => s.SetProperty(i => i.X, 1), ct);"];
        string[] comMotivo = ["// AUDITORIA-OK: tabela técnica, sem registro por decisão", "var n = await db.Itens.ExecuteDeleteAsync(ct);"];
        string[] semMotivo = ["// AUDITORIA-OK:", "var n = await db.Itens.ExecuteDeleteAsync(ct);"];
        string[] longe = ["// AUDITORIA-OK: motivo", "var a = 1;", "var b = 2;", "var c = 3;", "await db.Database.ExecuteSqlRawAsync(\"x\", ct);"];
        string[] soComentario = ["// usa .ExecuteUpdate( para isso, mas aqui é só texto"];

        Assert.Equal([1], LinhasSemComentario(semComentario));
        Assert.Empty(LinhasSemComentario(comMotivo));
        Assert.Equal([2], LinhasSemComentario(semMotivo));
        Assert.Equal([5], LinhasSemComentario(longe)); // 4 linhas acima já é longe demais
        Assert.Empty(LinhasSemComentario(soComentario));
    }
}
