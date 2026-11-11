namespace RHSystem.Domain.Auditoria;

/// <summary>Quem fez: uma pessoa (<c>PESSOA</c>, com funcionário) ou o próprio sistema (<c>SISTEMA</c>, sem funcionário nem perfil).</summary>
public enum TipoDeAtor
{
    Pessoa,
    Sistema,
}
