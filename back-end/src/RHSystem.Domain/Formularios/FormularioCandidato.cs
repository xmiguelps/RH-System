namespace RHSystem.Domain.Formularios;

/// <summary>
/// Um candidato de fora que respondeu um formulário por link público (<c>FORMULARIOS_CANDIDATOS</c>). Dado pessoal: só o RH o vê, nunca vai para auditoria (o valor), log nem erro, e a retenção (180 dias por padrão) o apaga com as respostas e os arquivos. O e-mail se guarda aparado e em
/// minúsculas (a chave única <c>(formulário, e-mail)</c> não diferencia caixa nem acento) e a data vem SEMPRE do relógio da aplicação (o padrão do banco usa o relógio real). A entidade não é auditada pelo interceptor. O nome original do currículo não é guardado (o download gera o nome).
/// </summary>
public class FormularioCandidato
{
    private FormularioCandidato()
    {
    }

    public int Id { get; private set; }

    public int FormularioId { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public string Email { get; private set; } = string.Empty;

    public string? Telefone { get; private set; }

    public string? CaminhoCurriculo { get; private set; }

    public DateTime CadastradoEmUtc { get; private set; }

    public static FormularioCandidato Criar(int formularioId, string nome, string email, string? telefone, string? caminhoCurriculo, DateTime cadastradoEmUtc) =>
        new()
        {
            FormularioId = formularioId,
            Nome = nome,
            Email = email,
            Telefone = telefone,
            CaminhoCurriculo = caminhoCurriculo,
            CadastradoEmUtc = CamposDeTexto.AoSegundo(cadastradoEmUtc),
        };
}
