namespace RHSystem.Infrastructure.Esquema;

/// <summary>De onde vêm os scripts. Existe para os testes trocarem a fonte por scripts sintéticos.</summary>
public interface IFonteDeScripts
{
    /// <summary>Todos os scripts, ordenados por versão.</summary>
    IReadOnlyList<ScriptSql> Carregar();
}
