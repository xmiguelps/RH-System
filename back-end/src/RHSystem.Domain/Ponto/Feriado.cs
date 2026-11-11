namespace RHSystem.Domain.Ponto;

/// <summary>
/// Um feriado (<c>FERIADOS</c>). A abrangência é regra do banco: nacional e da empresa sem UF nem município; estadual com UF; municipal com os dois. Só o <see cref="TipoDeFeriado.Municipal"/> se edita pela API.
/// </summary>
public class Feriado
{
    private Feriado()
    {
    }

    public int Id { get; private set; }

    public string Nome { get; private set; } = string.Empty;

    public DateOnly Data { get; private set; }

    public TipoDeFeriado Tipo { get; private set; }

    public string? Uf { get; private set; }

    public string? Municipio { get; private set; }

    public bool Ativo { get; private set; }

    public string? Descricao { get; private set; }

    public bool RepeteTodoAno { get; private set; }

    public static Feriado Criar(string nome, DateOnly data, TipoDeFeriado tipo, string? uf, string? municipio, bool repeteTodoAno) =>
        new() { Nome = nome, Data = data, Tipo = tipo, Uf = uf, Municipio = municipio, Ativo = true, RepeteTodoAno = repeteTodoAno };
}
