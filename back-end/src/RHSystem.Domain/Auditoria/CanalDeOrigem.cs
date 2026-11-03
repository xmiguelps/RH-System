namespace RHSystem.Domain.Auditoria;

/// <summary>De onde veio o evento: o portal (<c>PORTAL</c>, com IP) ou o sistema (<c>SISTEMA</c>, tarefas agendadas; sem IP).</summary>
public enum CanalDeOrigem
{
    Portal,
    Sistema,
}
