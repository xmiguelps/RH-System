namespace RHSystem.Infrastructure.Esquema;

/// <summary>Um script que já consta como aplicado teve o conteúdo mudado (o hash não confere).</summary>
public sealed class ScriptJaAplicadoFoiAlteradoException(int versao)
    : Exception($"O script V{versao:D3} já foi aplicado e foi alterado. Não edite scripts aplicados: crie um script novo.")
{
    public int Versao { get; } = versao;
}

/// <summary>Um comando de um script falhou. O banco pode ter ficado parcialmente alterado.</summary>
public sealed class FalhaNoScriptException(int versao, int indiceDoComando, string detalhe, Exception? interna = null)
    : Exception(
        $"O script V{versao:D3} falhou no comando {indiceDoComando}: {detalhe}. O banco pode ter ficado parcialmente alterado: confira o que já foi criado antes de tentar de novo (em desenvolvimento, recrie o banco com 'docker compose down -v').",
        interna)
{
    public int Versao { get; } = versao;

    /// <summary>Posição do comando que falhou dentro do script, contando a partir de 1.</summary>
    public int IndiceDoComando { get; } = indiceDoComando;

    public string Detalhe { get; } = detalhe;
}

/// <summary>Há um script pendente com número menor que o de um script já aplicado.</summary>
public sealed class VersaoForaDeOrdemException(int versaoPendente, int maiorVersaoAplicada)
    : Exception($"O script V{versaoPendente:D3} está pendente, mas o banco já tem aplicada a versão V{maiorVersaoAplicada:D3}. Não se cria script com número menor que o de um script já aplicado: renumere para uma versão maior.")
{
    public int VersaoPendente { get; } = versaoPendente;

    public int MaiorVersaoAplicada { get; } = maiorVersaoAplicada;
}

/// <summary>Uma versão consta como aplicada, mas o arquivo dela não existe mais entre os scripts.</summary>
public sealed class ScriptAplicadoSumiuException(int versao)
    : Exception($"O script V{versao:D3} consta como aplicado no banco, mas o arquivo dele não existe mais. Não apague scripts aplicados.")
{
    public int Versao { get; } = versao;
}

/// <summary>O banco já tem tabelas, mas não tem histórico de scripts (SCHEMA_VERSOES vazia ou ausente).</summary>
public sealed class BancoSemHistoricoDeScriptsException(int quantidadeDeTabelas)
    : Exception($"O banco já tem {quantidadeDeTabelas} tabela(s), mas não tem histórico de scripts aplicados (a SCHEMA_VERSOES está vazia ou não existe). Para não sobrescrever nada, o executor recusa: use um banco vazio (em desenvolvimento, 'docker compose down -v').")
{
    public int QuantidadeDeTabelas { get; } = quantidadeDeTabelas;
}
