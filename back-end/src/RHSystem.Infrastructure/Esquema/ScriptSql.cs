namespace RHSystem.Infrastructure.Esquema;

/// <summary>Um script SQL versionado: <c>Banco de Dados/Scripts/V{Versao}__{Nome}.sql</c>.</summary>
/// <param name="Versao">Número da versão (o <c>nnn</c> de <c>Vnnn</c>).</param>
/// <param name="Nome">A descrição do nome do arquivo, sem o <c>Vnnn__</c> e sem a extensão.</param>
/// <param name="Conteudo">O texto do script, sem BOM.</param>
public sealed record ScriptSql(int Versao, string Nome, string Conteudo);
