using System.Text;
using System.Text.RegularExpressions;

namespace RHSystem.Infrastructure.Esquema;

/// <summary>
/// Divide o texto de um script SQL em comandos, como o cliente <c>mysql</c> faz: pelo delimitador corrente (<c>;</c> até uma
/// linha <c>DELIMITER x</c> trocá-lo). Um delimitador dentro de comentário (<c>-- </c>, <c>#</c>, <c>/* */</c>) ou de literal entre aspas
/// (<c>'...'</c>, <c>"..."</c>, crase) não divide. Os comentários ficam no texto do comando (o servidor os ignora); trecho vazio ou só de
/// comentário não vira comando.
/// </summary>
public static partial class DivisorDeScript
{
    private enum Estado
    {
        Normal,
        ComentarioDeLinha,
        ComentarioDeBloco,
        AspasSimples,
        AspasDuplas,
        Crase,
    }

    [GeneratedRegex(@"\G[ \t]*delimiter[ \t]+(?<novo>\S+)[ \t]*(?:\n|$)", RegexOptions.IgnoreCase)]
    private static partial Regex LinhaDelimiter();

    public static IReadOnlyList<string> Dividir(string conteudo)
    {
        var texto = conteudo.Replace("\r\n", "\n").Replace('\r', '\n');
        var comandos = new List<string>();
        var atual = new StringBuilder();
        var temCodigo = false;
        var delimitador = ";";
        var estado = Estado.Normal;
        var inicioDeLinha = true;
        var i = 0;

        void Emitir()
        {
            if (temCodigo)
            {
                comandos.Add(atual.ToString().Trim());
            }

            atual.Clear();
            temCodigo = false;
        }

        while (i < texto.Length)
        {
            var c = texto[i];
            var proximo = i + 1 < texto.Length ? texto[i + 1] : '\0';

            switch (estado)
            {
                case Estado.Normal:
                    if (inicioDeLinha)
                    {
                        var linha = LinhaDelimiter().Match(texto, i);
                        if (linha.Success)
                        {
                            Emitir();
                            delimitador = linha.Groups["novo"].Value;
                            i += linha.Length;
                            continue;
                        }
                    }

                    if (string.CompareOrdinal(texto, i, delimitador, 0, delimitador.Length) == 0)
                    {
                        Emitir();
                        i += delimitador.Length;
                        inicioDeLinha = false;
                        continue;
                    }

                    if (c == '-' && proximo == '-' && (i + 2 >= texto.Length || char.IsWhiteSpace(texto[i + 2])))
                    {
                        estado = Estado.ComentarioDeLinha;
                        atual.Append("--");
                        i += 2;
                        inicioDeLinha = false;
                        continue;
                    }

                    if (c == '#')
                    {
                        estado = Estado.ComentarioDeLinha;
                        atual.Append(c);
                        i++;
                        inicioDeLinha = false;
                        continue;
                    }

                    if (c == '/' && proximo == '*')
                    {
                        estado = Estado.ComentarioDeBloco;
                        // /*! ... */ (comentário executável) e /*+ ... */ (dica do otimizador) contam como código.
                        var terceiro = i + 2 < texto.Length ? texto[i + 2] : '\0';
                        temCodigo |= terceiro is '!' or '+';
                        atual.Append("/*");
                        i += 2;
                        inicioDeLinha = false;
                        continue;
                    }

                    if (c is '\'' or '"' or '`')
                    {
                        estado = c == '\'' ? Estado.AspasSimples : c == '"' ? Estado.AspasDuplas : Estado.Crase;
                        temCodigo = true;
                    }
                    else if (!char.IsWhiteSpace(c))
                    {
                        temCodigo = true;
                    }

                    atual.Append(c);
                    i++;
                    inicioDeLinha = c == '\n';
                    break;

                case Estado.ComentarioDeLinha:
                    atual.Append(c);
                    i++;
                    if (c == '\n')
                    {
                        estado = Estado.Normal;
                        inicioDeLinha = true;
                    }

                    break;

                case Estado.ComentarioDeBloco:
                    if (c == '*' && proximo == '/')
                    {
                        atual.Append("*/");
                        i += 2;
                        estado = Estado.Normal;
                    }
                    else
                    {
                        atual.Append(c);
                        i++;
                    }

                    break;

                default:
                    var aspas = estado switch
                    {
                        Estado.AspasSimples => '\'',
                        Estado.AspasDuplas => '"',
                        _ => '`',
                    };

                    if (c == '\\' && estado != Estado.Crase && proximo != '\0')
                    {
                        atual.Append(c).Append(proximo);
                        i += 2;
                    }
                    else if (c == aspas && proximo == aspas)
                    {
                        atual.Append(c).Append(proximo);
                        i += 2;
                    }
                    else
                    {
                        atual.Append(c);
                        i++;
                        if (c == aspas)
                        {
                            estado = Estado.Normal;
                        }
                    }

                    break;
            }
        }

        Emitir();
        return comandos;
    }
}
