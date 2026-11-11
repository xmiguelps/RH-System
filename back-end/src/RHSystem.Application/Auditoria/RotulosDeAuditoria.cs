using RHSystem.Domain.Auditoria;
using RHSystem.Domain.Autenticacao;

namespace RHSystem.Application.Auditoria;

/// <summary>
/// Os rótulos da tela de auditoria, que o CSV também usa (o arquivo traz o que a pessoa lê, não os códigos). Os de área e ação são os que o contrato
/// escreve nas descrições de <c>AuditoriaArea</c> e <c>AuditoriaAcao</c> (<c>RotulosDeAuditoriaTests</c> lê o <c>openapi.yaml</c> e compara). Um valor de enum
/// novo sem rótulo falha na compilação (aviso tratado como erro) e no teste.
/// </summary>
public static class RotulosDeAuditoria
{
    public static string Area(AuditoriaArea area) => area switch
    {
        AuditoriaArea.Acesso => "Acesso",
        AuditoriaArea.Colaboradores => "Colaboradores",
        AuditoriaArea.Documentos => "Documentos",
        AuditoriaArea.Cadastros => "Cadastros",
        AuditoriaArea.Importacao => "Importação",
        AuditoriaArea.Ponto => "Ponto",
        AuditoriaArea.Feriados => "Feriados",
        AuditoriaArea.Ferias => "Férias",
        AuditoriaArea.Holerites => "Holerites",
        AuditoriaArea.Advertencias => "Advertências",
        AuditoriaArea.Avisos => "Avisos",
        AuditoriaArea.Formularios => "Formulários",
        AuditoriaArea.Treinamentos => "Treinamentos",
        AuditoriaArea.Beneficios => "Benefícios",
        AuditoriaArea.Atestados => "Atestados",
        _ => throw new ArgumentOutOfRangeException(nameof(area), area, "Área de auditoria sem rótulo."),
    };

    public static string Acao(AuditoriaAcao acao) => acao switch
    {
        AuditoriaAcao.Entrou => "Entrou",
        AuditoriaAcao.AcessoNegado => "Acesso negado",
        AuditoriaAcao.Criou => "Criou",
        AuditoriaAcao.Alterou => "Alterou",
        AuditoriaAcao.Excluiu => "Excluiu",
        AuditoriaAcao.Inativou => "Inativou",
        AuditoriaAcao.Registrou => "Registrou",
        AuditoriaAcao.PediuAjuste => "Pediu ajuste",
        AuditoriaAcao.Aprovou => "Aprovou",
        AuditoriaAcao.Recusou => "Recusou",
        AuditoriaAcao.Publicou => "Publicou",
        AuditoriaAcao.EnviouEmail => "Enviou e-mail",
        AuditoriaAcao.Importou => "Importou",
        AuditoriaAcao.Baixou => "Baixou",
        _ => throw new ArgumentOutOfRangeException(nameof(acao), acao, "Ação de auditoria sem rótulo."),
    };

    /// <summary><c>Funcionario</c> aparece como "Colaborador". Sem perfil (o sistema, ou quem não tinha nível), o texto é vazio.</summary>
    public static string Perfil(Perfil? perfil) => perfil switch
    {
        null => string.Empty,
        Domain.Autenticacao.Perfil.Funcionario => "Colaborador",
        Domain.Autenticacao.Perfil.Gestor => "Gestor",
        Domain.Autenticacao.Perfil.RH => "RH",
        _ => throw new ArgumentOutOfRangeException(nameof(perfil), perfil, "Perfil sem rótulo."),
    };

    public static string Origem(CanalDeOrigem canal) => canal switch
    {
        CanalDeOrigem.Portal => "Portal no navegador",
        CanalDeOrigem.Sistema => "Automático",
        _ => throw new ArgumentOutOfRangeException(nameof(canal), canal, "Canal de origem sem rótulo."),
    };
}
