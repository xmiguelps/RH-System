namespace RHSystem.Domain.Auditoria;

/// <summary>
/// Marca uma entidade que a auditoria registra sozinha (o interceptor do EF vê criar, alterar, inativar e excluir). Só entidade com esta marca e só
/// propriedade com <see cref="AuditarAttribute"/> entram no log: o que não é marcado nunca é registrado.
/// </summary>
/// <param name="area">A área do evento (<c>COLABORADORES</c>, <c>CADASTROS</c>...).</param>
/// <param name="rotulo">O nome da coisa para a tela ("Colaborador", "Setor"): a descrição do evento é "{rotulo} {nome}".</param>
[AttributeUsage(AttributeTargets.Class)]
public sealed class AuditadoAttribute(AuditoriaArea area, string rotulo) : Attribute
{
    public AuditoriaArea Area { get; } = area;

    public string Rotulo { get; } = rotulo;
}

/// <summary>
/// Marca uma propriedade que entra no log (o nome do campo na tela, como "Situação"). <see cref="Sensivel"/> grava só "este campo mudou", sem o valor
/// antes nem depois. Nunca marque senha, hash, token ou segredo: um teste do Domain barra isso.
/// </summary>
/// <param name="campo">O nome do campo como a tela mostra.</param>
[AttributeUsage(AttributeTargets.Property)]
public sealed class AuditarAttribute(string campo) : Attribute
{
    public string Campo { get; } = campo;

    public bool Sensivel { get; init; }
}

/// <summary>O que uma entidade auditada diz de si para o evento de auditoria.</summary>
public interface IAuditavel
{
    /// <summary>O nome curto ("Bruno Lima"): vai na descrição do evento e substitui o id quando outra entidade aponta para esta ("Analista pleno").</summary>
    string NomeParaAuditoria { get; }

    /// <summary>O registro afetado como a tela de auditoria mostra ("Colaborador Bruno Lima, matrícula 00482").</summary>
    string RegistroAfetado { get; }
}
