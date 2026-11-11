using RHSystem.Domain.Auditoria;

namespace RHSystem.Domain.Autenticacao;

/// <summary>
/// Uma linha de <c>CARGOS</c>: nome (único sem diferenciar maiúsculas nem acentos), descrição e a carga semanal em horas. <see cref="Criar"/> e <see cref="Atualizar"/> recusam com
/// <see cref="ArgumentException"/> o que o banco ou o contrato recusariam: nome de 1 a 100 (com <c>Trim</c>), descrição até 255 e carga maior que zero, até 168 e com no máximo duas casas.
/// </summary>
[Auditado(AuditoriaArea.Cadastros, "Cargo")]
public class Cargo : IAuditavel
{
    private Cargo()
    {
    }

    public int Id { get; private set; }

    [Auditar("Nome")]
    public string Nome { get; private set; } = string.Empty;

    [Auditar("Descrição")]
    public string? Descricao { get; private set; }

    /// <summary>A carga semanal em horas (<c>CARGOS.CARGA_HORARIA</c>); nula só se alguém a pôs por SQL (o banco aceita <c>NULL</c>, o contrato não).</summary>
    [Auditar("Carga semanal")]
    public decimal? CargaSemanalHoras { get; private set; }

    public string NomeParaAuditoria => Nome;

    public string RegistroAfetado => $"Cargo {Nome}";

    public static Cargo Criar(string nome, string? descricao, decimal cargaSemanalHoras)
    {
        var cargo = new Cargo();
        cargo.Aplicar(nome, descricao, cargaSemanalHoras);
        return cargo;
    }

    public void Atualizar(string nome, string? descricao, decimal cargaSemanalHoras) => Aplicar(nome, descricao, cargaSemanalHoras);

    /// <summary>Valida TUDO primeiro e só então grava: o que for recusado não deixa o cargo pela metade.</summary>
    private void Aplicar(string nome, string? descricao, decimal cargaSemanalHoras)
    {
        var nomeValido = CamposDeTexto.Obrigatorio(nome, nameof(nome), 100);
        var descricaoValida = CamposDeTexto.Opcional(descricao, nameof(descricao), 255);
        if (cargaSemanalHoras <= 0 || cargaSemanalHoras > 168 || decimal.Round(cargaSemanalHoras, 2) != cargaSemanalHoras)
        {
            throw new ArgumentException("A carga semanal é maior que zero, de no máximo 168 horas, com até duas casas decimais.", nameof(cargaSemanalHoras));
        }

        Nome = nomeValido;
        Descricao = descricaoValida;
        CargaSemanalHoras = cargaSemanalHoras;
    }
}
