using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Autenticacao;

/// <summary>Uma linha de <c>CARGOS</c>. Só leitura na fatia 0: nome e id bastam para o <c>cargo</c> de <c>UsuarioResposta</c>.</summary>
[Auditado(AuditoriaArea.Cadastros, "Cargo")]
public class Cargo : IAuditavel
{
    private Cargo()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    /// <summary>A carga semanal em horas (<c>CARGOS.CARGA_HORARIA</c>); nula quando o cargo não a define. Só leitura aqui: quem a escreve é a fatia 1c.</summary>
    public decimal? CargaSemanalHoras { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Cargo {Nome}";
}
