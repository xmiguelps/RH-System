using System.Text.RegularExpressions;

namespace RHSystem.Infrastructure.Arquivos;

/// <summary>
/// A seção <c>Armazenamento</c>. <see cref="ConexaoBlob"/> traz a chave da conta e <b>só entra por user-secrets ou pela configuração do App Service</b> (em Development, <c>UseDevelopmentStorage=true</c> dispensa chave escrita): nunca vai
/// para o repositório, o log nem a resposta. Vazia, o app sobe e a primeira operação que usa arquivo falha com erro claro no log.
/// </summary>
public sealed partial class OpcoesDeArmazenamento
{
    public const string Secao = "Armazenamento";

    /// <summary>A connection string do Storage Account. Vazia por padrão.</summary>
    public string ConexaoBlob { get; set; } = string.Empty;

    /// <summary>O contêiner de blobs (privado, sem acesso anônimo): criado se não existir.</summary>
    public string Container { get; set; } = "arquivos";

    /// <summary>Por quantos minutos vale o link temporário de download.</summary>
    public int LinkValidoMinutos { get; set; } = 5;

    /// <summary><c>true</c>: o link só funciona por HTTPS. <c>false</c> só em Development (o Azurite fala HTTP).</summary>
    public bool SomenteHttps { get; set; } = true;

    /// <summary>A limpeza de sobras só apaga arquivo mais velho que isto: protege o envio que já gravou o blob e ainda não inseriu a linha.</summary>
    public int SobrasMinimoHoras { get; set; } = 24;

    /// <summary>Nome de contêiner do Azure: de 3 a 63 caracteres, letras minúsculas, números e hífen (sem hífen duplo, nem no começo ou no fim).</summary>
    [GeneratedRegex("^[a-z0-9](?:[a-z0-9]|-(?=[a-z0-9])){2,62}$")]
    private static partial Regex FormatoDoContainer();

    /// <summary>Configuração errada falha na inicialização, com mensagem clara. A connection string vazia NÃO é erro (o app sobe sem Blob).</summary>
    public void Validar()
    {
        if (LinkValidoMinutos < 1 || SobrasMinimoHoras < 1)
        {
            throw new InvalidOperationException("Armazenamento:LinkValidoMinutos e Armazenamento:SobrasMinimoHoras precisam ser pelo menos 1.");
        }

        if (!FormatoDoContainer().IsMatch(Container ?? string.Empty))
        {
            throw new InvalidOperationException("Armazenamento:Container precisa ter de 3 a 63 caracteres: letras minúsculas, números e hífen.");
        }
    }
}
