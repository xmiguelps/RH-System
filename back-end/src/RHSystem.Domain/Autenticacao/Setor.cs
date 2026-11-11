using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Colaboradores;

namespace RHSystem.Domain.Autenticacao;

/// <summary>
/// Uma linha de <c>SETORES</c>: nome (único sem diferenciar maiúsculas nem acentos), descrição e o gestor (<c>ID_GESTOR</c>, que aceita <c>NULL</c>: um setor pode estar sem gestor). O gestor muda só por
/// <see cref="DefinirGestor"/>; <see cref="Atualizar"/> troca nome e descrição.
/// </summary>
[Auditado(AuditoriaArea.Cadastros, "Setor")]
public class Setor : IAuditavel
{
    private Setor()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    [Auditar("Descrição")]
    public string? Descricao { get; private set; }

    /// <summary><c>GESTORES.ID_GESTOR</c> do gestor responsável pelo setor; um setor pode estar sem gestor.</summary>
    [Auditar("Gestor")]
    public int? GestorId { get; private set; }

    /// <summary>O gestor, quando a consulta o traz (<c>Include</c>); o log mostra o nome da pessoa pelo <see cref="GestorId"/>.</summary>
    public Gestor? Gestor { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Setor {Nome}";

    public static Setor Criar(string nome, string? descricao)
    {
        var setor = new Setor();
        setor.Atualizar(nome, descricao);
        return setor;
    }

    /// <summary>Valida TUDO primeiro e só então grava: o que for recusado não deixa o setor pela metade.</summary>
    public void Atualizar(string nome, string? descricao)
    {
        var nomeValido = CamposDeTexto.Obrigatorio(nome, nameof(nome), 100);
        var descricaoValida = CamposDeTexto.Opcional(descricao, nameof(descricao), 255);
        Nome = nomeValido;
        Descricao = descricaoValida;
    }

    /// <summary><paramref name="idGestor"/> é o <c>ID_GESTOR</c> de <c>GESTORES</c> (não o id do colaborador); nulo deixa o setor sem gestor.</summary>
    public void DefinirGestor(int? idGestor) => GestorId = idGestor;
}
