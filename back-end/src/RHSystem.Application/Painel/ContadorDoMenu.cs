namespace RHSystem.Application.Painel;

/// <summary>
/// Os números das bolinhas do menu. Cada fatia dona registra a <see cref="IFonteDeContadorDoMenu"/> do seu (documentos: 1c; avisos: 4; formulários: 5;
/// ponto: 3; férias: 7). O contador do gestor e o do RH para o ajuste de ponto são dois: o gestor conta a etapa dele e o RH, a etapa do RH.
/// </summary>
public enum ContadorDoMenu
{
    AvisosNaoLidos,
    FormulariosPendentes,
    DocumentosPendentes,
    PendenciasDaEquipe,
    PedidosDeAjusteDePontoDoGestor,
    PedidosDeAjusteDePontoDoRh,
}
