# Estado das rotas

Mantido pelo dono do back-end. Diz quais rotas já existem **de verdade** na API (não só no contrato). A sessão do front consulta este arquivo para saber se um passo de integração já pode rodar; enquanto uma rota não estiver aqui como `pronta`, o front usa os dados simulados (MSW) e registra "pendente de integração".

Colunas: `fatia` (0 a 8) e `situação`: `planejada` (só no contrato), `em andamento` ou `pronta` (implementada, testada e publicada no ambiente da demo). A lista completa de rotas por fatia está em `ROTAS.md`.

| Fatia | Assunto | Situação |
|---|---|---|
| 0 | Login, sessão e saúde | planejada |
| 1 | Colaboradores e base transversal | em andamento |
| 2 | Importação por Excel | planejada |
| 3 | Ponto | planejada |
| 4 | Comunicação e documentos | planejada |
| 5 | Formulários | planejada |
| 6 | TAD (treinamentos) | planejada |
| 7 | Gestão | planejada |

## Rotas implementadas

Uma linha por rota, com a mesma chave de `ROTAS.md`. `em andamento`: implementada e testada na branch; o dono passa a `pronta` quando publicar no ambiente da demo. Quem entrega a rota acrescenta a linha dela no mesmo passo em que a implementa (o teste `STATUS_md_lista_toda_rota_implementada_e_toda_rota_listada_existe_na_API` confere nos dois sentidos).

| Rota | operationId | Situação | Observação |
|---|---|---|---|
| `POST /api/auth/entrar` | entrar | em andamento | |
| `POST /api/auth/renovar` | renovarSessao | em andamento | |
| `POST /api/auth/sair` | sair | em andamento | |
| `GET /api/auth/eu` | obterUsuarioLogado | em andamento | |
| `GET /health` | verificarSaude | em andamento | |
| `GET /api/rh/auditoria` | listarEventosDeAuditoria | em andamento | |
| `GET /api/rh/auditoria/exportacao` | exportarAuditoria | em andamento | |
| `GET /api/rh/auditoria/{eventoId}` | obterEventoDeAuditoria | em andamento | |
| `GET /api/eu/contadores-do-menu` | obterContadoresDoMenuDoFuncionario | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: 1c; avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `GET /api/gestor/contadores-do-menu` | obterContadoresDoMenuDoGestor | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: 1c; avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `GET /api/rh/contadores-do-menu` | obterContadoresDoMenuDoRh | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: 1c; avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `POST /api/auth/senha/esqueci` | solicitarRedefinicaoDeSenha | em andamento | sempre 202; um pedido por pessoa a cada 60 s (decisão 6 do plano da 1b) |
| `POST /api/auth/senha/validar-link` | validarLinkDeSenha | em andamento | |
| `POST /api/auth/senha/definir` | definirSenhaPeloLink | em andamento | sem login automático |
| `POST /api/eu/senha` | trocarMinhaSenha | em andamento | troca de senha com a sessão atual mantida; limitação do `rth` na decisão 2 do plano |
| `GET /api/rh/usuarios` | listarUsuariosDoSistema | em andamento | |
| `POST /api/rh/usuarios/{funcionarioId}/convite` | enviarConviteDeAcesso | em andamento | login desativado não recebe convite (o RH usa Ativar antes) |
| `POST /api/rh/usuarios/{funcionarioId}/ativar` | ativarUsuarioDoSistema | em andamento | |
| `POST /api/rh/usuarios/{funcionarioId}/desativar` | desativarUsuarioDoSistema | em andamento | encerra as sessões da pessoa; ninguém desativa o próprio login |

## Pendências conhecidas

- **Pedidos parados no gestor (porta vazia):** trocar, tirar ou desligar um gestor e ligar "Gestor ausente" chamam `IPedidosParadosNoGestor`, mas até as fatias 3 (ponto) e 7 (férias) implementarem a deles **nenhum pedido muda de etapa** (`CONVENCOES.md`, seção 9).
