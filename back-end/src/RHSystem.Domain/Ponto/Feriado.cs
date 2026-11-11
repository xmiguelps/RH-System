using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Ponto;

/// <summary>
/// Um feriado (<c>FERIADOS</c>). A abrangência é regra do banco: nacional e da empresa sem UF nem município; estadual com UF; municipal com os dois. Só o <see cref="TipoDeFeriado.Municipal"/> se edita pela API. A auditoria é do interceptor: o RH que cria ou
/// edita sai como ator do evento, e o que o agendador importa ou repete sai como <c>SISTEMA</c>.
/// </summary>
[Auditado(AuditoriaArea.Feriados, "Feriado")]
public class Feriado : IAuditavel
{
    private Feriado()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    [Auditar("Data")]
    public DateOnly Data { get; private set; }

    public TipoDeFeriado Tipo { get; private set; }

    public string? Uf { get; private set; }

    public string? Municipio { get; private set; }

    [Auditar("Ativo")]
    public bool Ativo { get; private set; }

    public string? Descricao { get; private set; }

    [Auditar("Repete todo ano")]
    public bool RepeteTodoAno { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Feriado {Nome}";

    /// <summary>Muda o que a tela edita. O tipo, a UF e o município não mudam, e só o feriado municipal se edita.</summary>
    /// <exception cref="InvalidOperationException">O feriado não é municipal.</exception>
    public void Editar(string nome, DateOnly data, bool repeteTodoAno, bool ativo)
    {
        if (Tipo != TipoDeFeriado.Municipal)
        {
            throw new InvalidOperationException("Só feriados municipais podem ser editados.");
        }

        Nome = nome;
        Data = data;
        RepeteTodoAno = repeteTodoAno;
        Ativo = ativo;
    }

    public static Feriado Criar(string nome, DateOnly data, TipoDeFeriado tipo, string? uf, string? municipio, bool repeteTodoAno) =>
        new() { Nome = nome, Data = data, Tipo = tipo, Uf = uf, Municipio = municipio, Ativo = true, RepeteTodoAno = repeteTodoAno };
}
