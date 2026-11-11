namespace RHSystem.Application.Painel;

/// <summary><c>GET /api/eu/contadores-do-menu</c> (<c>PainelContadoresFuncionario</c> do contrato): as bolinhas do menu da pessoa, de qualquer perfil.</summary>
public sealed record PainelContadoresFuncionario(int AvisosNaoLidos, int FormulariosPendentes, int DocumentosPendentes);

/// <summary><c>GET /api/gestor/contadores-do-menu</c> (<c>PainelContadoresGestor</c>): o que o gestor pode decidir agora (férias <c>COM_O_GESTOR</c> mais ajustes de ponto na etapa dele) e só os ajustes de ponto.</summary>
public sealed record PainelContadoresGestor(int PendenciasDaEquipe, int PedidosDeAjusteDePonto);

/// <summary><c>GET /api/rh/contadores-do-menu</c> (<c>PainelContadoresRh</c>): os pedidos de ajuste de ponto que o RH já pode decidir.</summary>
public sealed record PainelContadoresRh(int PedidosDeAjusteDePonto);
