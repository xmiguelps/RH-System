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
| `GET /api/eu/contadores-do-menu` | obterContadoresDoMenuDoFuncionario | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: ok (1c); avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `GET /api/gestor/contadores-do-menu` | obterContadoresDoMenuDoGestor | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: ok (1c); avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `GET /api/rh/contadores-do-menu` | obterContadoresDoMenuDoRh | em andamento | parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: ok (1c); avisos: 4; formulários: 5; ponto: 3; férias: 7) |
| `POST /api/auth/senha/esqueci` | solicitarRedefinicaoDeSenha | em andamento | sempre 202; um pedido por pessoa a cada 60 s (decisão 6 do plano da 1b) |
| `POST /api/auth/senha/validar-link` | validarLinkDeSenha | em andamento | |
| `POST /api/auth/senha/definir` | definirSenhaPeloLink | em andamento | sem login automático |
| `POST /api/eu/senha` | trocarMinhaSenha | em andamento | troca de senha com a sessão atual mantida; limitação do `rth` na decisão 2 do plano |
| `GET /api/rh/usuarios` | listarUsuariosDoSistema | em andamento | |
| `POST /api/rh/usuarios/{funcionarioId}/convite` | enviarConviteDeAcesso | em andamento | login desativado não recebe convite (o RH usa Ativar antes) |
| `POST /api/rh/usuarios/{funcionarioId}/ativar` | ativarUsuarioDoSistema | em andamento | |
| `POST /api/rh/usuarios/{funcionarioId}/desativar` | desativarUsuarioDoSistema | em andamento | encerra as sessões da pessoa; ninguém desativa o próprio login |
| `GET /api/rh/funcionarios` | listarFuncionarios | em andamento | parcial: `situacaoHoje` já conhece batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4 e 7; sem benefícios de dependentes até a 7 |
| `GET /api/rh/funcionarios/{funcionarioId}` | obterFichaDoFuncionario | em andamento | parcial: `situacaoHoje` já conhece batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4 e 7; sem benefícios de dependentes até a 7 |
| `GET /api/rh/funcionarios/{funcionarioId}/indicadores` | obterIndicadoresDoFuncionario | em andamento | parcial: `situacaoHoje` já conhece batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4 e 7; sem benefícios de dependentes até a 7 |
| `POST /api/rh/funcionarios` | criarFuncionario | em andamento | as caixas "incluir no onboarding" e "formulário de admissão" recusam com o erro do contrato até as fatias 6 e 5 |
| `PUT /api/rh/funcionarios/{funcionarioId}` | editarFuncionario | em andamento | substituição completa; benefício dado a quem já tem linha PENDENTE, RECUSADO ou CANCELADO atualiza a mesma linha (aprova o pedido, decisão 10 do plano) |
| `POST /api/rh/funcionarios/{funcionarioId}/desligamento` | inativarFuncionario | em andamento | a condição "depois de a consolidação do banco de horas" entra com a fatia 3; os pedidos de ajuste de ponto parados num gestor desligado passam ao RH (fatia 3, `PedidosParadosNoGestorDoPonto`); os de férias só quando a fatia 7 registrar a implementação dela da porta `IPedidosParadosNoGestor` |
| `DELETE /api/rh/funcionarios/{funcionarioId}/desligamento` | cancelarDesligamentoAgendado | em andamento | só enquanto o desligamento está AGENDADO; depois de EFETIVADO responde 422 `SEM_DESLIGAMENTO_AGENDADO` |
| `POST /api/rh/funcionarios/{funcionarioId}/readmissao` | readmitirFuncionario | em andamento | os pedidos de ajuste de ponto parados no gestor readmitido passam ao RH (fatia 3); os de férias só quando a fatia 7 registrar a implementação dela da porta `IPedidosParadosNoGestor` |
| `GET /api/gestor/equipe` | listarEquipeDoGestor | em andamento | parcial: `situacaoHoje` e `contagensHoje` já conhecem batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4, 6 e 7 |
| `GET /api/gestor/equipe/{funcionarioId}` | obterFichaDaEquipe | em andamento | parcial: `situacaoHoje` e `contagensHoje` já conhecem batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4, 6 e 7 |
| `GET /api/gestor/equipe/{funcionarioId}/indicadores` | obterIndicadoresDaEquipe | em andamento | parcial: `situacaoHoje` e `contagensHoje` já conhecem batida, feriado, férias e atestado (fatia 3); indicador do banco de horas já é o saldo do extrato (fatia 3), os outros `null` até as fatias 4, 6 e 7 |
| `GET /api/rh/cargos` | listarCargos | em andamento | |
| `POST /api/rh/cargos` | criarCargo | em andamento | |
| `PUT /api/rh/cargos/{cargoId}` | editarCargo | em andamento | a nova carga vale só para os dias de ponto criados depois; não existe exclusão de cargo |
| `GET /api/rh/beneficios` | listarBeneficiosDoCatalogo | em andamento | as marcas já são gravadas; `quantidadePessoas` conta os pedidos de benefício ativos (`BeneficioDoFuncionario`: 1b e 7) |
| `POST /api/rh/beneficios` | criarBeneficioDoCatalogo | em andamento | |
| `PUT /api/rh/beneficios/{beneficioId}` | editarBeneficioDoCatalogo | em andamento | |
| `DELETE /api/rh/beneficios/{beneficioId}` | excluirBeneficioDoCatalogo | em andamento | só se nenhum colaborador recebeu o benefício (ativo, inativo ou pedido pendente); senão 409 `CADASTRO_EM_USO` |
| `GET /api/rh/setores` | listarSetores | em andamento | |
| `POST /api/rh/setores` | criarSetor | em andamento | escolher como gestor alguém de outro setor não muda o setor dessa pessoa (decisão 1 do plano da 1c, aguardando o dono) |
| `PUT /api/rh/setores/{setorId}` | editarSetor | em andamento | os pedidos de ajuste de ponto parados do gestor antigo passam ao RH (fatia 3); os de férias só quando a fatia 7 registrar a implementação dela da porta `IPedidosParadosNoGestor` |
| `GET /api/rh/gestores` | listarGestores | em andamento | |
| `POST /api/rh/gestores` | cadastrarGestor | em andamento | só quem tem o nível `RH`; a linha nasce sem setor |
| `PUT /api/rh/gestores/{gestorId}/ausencia` | configurarAusenciaDoGestor | em andamento | parcial: `pedidosAprovadosAgora` já conta os pedidos de ajuste de ponto aprovados sozinhos (fatia 3); os de férias valem 0 até a fatia 7 registrar a implementação dela da porta `IPedidosParadosNoGestor` |
| `GET /api/rh/funcionarios/{funcionarioId}/documentos` | listarDocumentosDoFuncionario | em andamento | |
| `POST /api/rh/funcionarios/{funcionarioId}/documentos` | adicionarDocumentoDoFuncionario | em andamento | o arquivo vai para o Azurite em desenvolvimento; em produção depende do Storage Account (Task 9, do dono); "pedir para enviar" não manda e-mail |
| `GET /api/rh/funcionarios/{funcionarioId}/documentos/{documentoId}/download` | baixarDocumentoDoFuncionario | em andamento | o link vem do Azurite em desenvolvimento; em produção depende do Storage Account (Task 9, do dono) |
| `GET /api/eu/documentos` | listarMeusDocumentos | em andamento | qualquer perfil logado (sem 403): só os documentos da própria pessoa |
| `POST /api/eu/documentos/{documentoId}/arquivo` | enviarMeuDocumento | em andamento | só documento `PENDENTE` da própria pessoa (de outra: 404; já enviado: 422 `DOCUMENTO_NAO_PEDIDO`); o arquivo vai para o Azurite em desenvolvimento e, em produção, depende do Storage Account (Task 9, do dono) |
| `GET /api/eu/documentos/{documentoId}/download` | baixarMeuDocumento | em andamento | o link vem do Azurite em desenvolvimento; em produção depende do Storage Account (Task 9, do dono); baixar o próprio documento não grava evento |
| `GET /api/eu/empresa` | obterEmpresa | em andamento | qualquer perfil logado (sem 403); a tabela nasce vazia: sem configuração responde 404 `EMPRESA_NAO_CONFIGURADA` |
| `PUT /api/rh/empresa` | salvarEmpresa | em andamento | a primeira chamada cria a linha única e as outras a substituem; os horários são `HH:mm` |
| `GET /api/rh/importacoes/modelo` | baixarModeloDeImportacao | em andamento | só RH; o arquivo `.xlsx` é gerado na hora com os cargos, setores e benefícios de hoje (aba "Valores válidos"); CPF e telefone saem em formato texto |
| `POST /api/rh/importacoes` | validarPlanilhaDeImportacao | em andamento | só RH; `multipart/form-data` (campo `arquivo`, `.xlsx` até 5 MiB e 500 linhas); devolve a prévia (201, sem `Location`) com erros por linha e coluna; nada é cadastrado; o arquivo e a prévia ficam 1 hora |
| `POST /api/rh/importacoes/{importacaoId}/confirmar` | confirmarImportacao | em andamento | só RH; tudo ou nada numa transação (reivindica a importação, cadastra pelo mesmo cadastro da tela 02, um evento `Importou`); 409 se já confirmada, 422 se venceu, tem erro ou uma linha foi recusada (com `linha`); as caixas de onboarding e de formulário de admissão usam as versões "indisponível" até as fatias 5 e 6 |
| `GET /api/eu/ponto/hoje` | obterPontoHoje | em andamento | qualquer perfil logado (sem 403): o cartão da própria pessoa; `dataDoPonto` é ontem só quando a ENTRADA de ontem está aberta há menos de 12 h e ainda não há batida hoje; `bancoHoras.atualizadoAte` é `null` até a consolidação (fatia 3c) |
| `POST /api/eu/ponto/marcacoes` | registrarMarcacaoDePonto | em andamento | hora do servidor; transação com a pessoa travada (duplo clique e duas abas: uma só entra); 409 `PONTO_BATIDA_DUPLICADA` (mesmo segundo) e `PONTO_SEQUENCIA_MUDOU`; 422 `FUNCIONARIO_INATIVO` e, sem código, dia anterior à admissão (ambos com `regraDoBanco`); auditoria `PONTO`/`REGISTROU` sem a hora; após fechar a jornada da madrugada o cartão devolvido é o do dia novo |
| `GET /api/eu/ponto/espelho` | obterMeuEspelhoDePonto | em andamento | qualquer perfil logado (sem 403): do dia 1 (ou da admissão) até hoje; mês futuro `dias: []`; `ano`/`mes` fora da faixa = 400 e só um dos dois completa com o mês atual; pedidos pendentes aparecem em `pedidoEmAnaliseId` mesmo antes de a rota que cria o pedido existir |
| `GET /api/gestor/ponto/equipe` | obterPontoDaEquipe | em andamento | só Gestor; a equipe é a de hoje (sem o gestor; quem entra depois do dia consultado fica de fora); `data` futura ou inválida = 400; dia passado: `situacaoHoje` e `contagensHoje` `null`; `pedidosParaAnalisar` conta só os pedidos da etapa deste gestor |
| `GET /api/gestor/equipe/{funcionarioId}/ponto` | obterEspelhoDePontoDaPessoaDaEquipe | em andamento | só Gestor, só a equipe dele: outra equipe, o próprio, outro gestor, inativo, em admissão ou inexistente = o mesmo 404; o mês inválido (400) vem antes do 404 |
| `GET /api/rh/funcionarios/{funcionarioId}/ponto` | obterEspelhoDePontoDeFuncionario | em andamento | só RH; qualquer pessoa, inclusive inativa (mostra os dias até o desligamento) |
| `GET /api/eu/ponto/pedidos-de-ajuste` | listarMeusPedidosDeAjuste | em andamento | qualquer perfil logado (sem 403): só os pedidos da própria pessoa; `etapa` sai do que está gravado (gestor pendente = GESTOR, depois dele ou sem gestor = RH) |
| `POST /api/eu/ponto/pedidos-de-ajuste` | criarPedidoDeAjuste | em andamento | só dia passado (422 `PONTO_PEDIDO_DIA_NAO_PASSOU`); um pedido PENDENTE por dia (409 `PONTO_PEDIDO_JA_EM_ANALISE`); o dia inteiro pedido tem de dar uma sequência válida (422 `PONTO_SEQUENCIA_INVALIDA`); pessoa sem gestor ou com gestor ausente nasce na etapa do RH (a etapa do gestor já aprovada, automática); o gestor é o do setor HOJE |
| `GET /api/eu/ponto/pedidos-de-ajuste/{pedidoId}` | obterMeuPedidoDeAjuste | em andamento | pedido de outra pessoa = 404 |
| `GET /api/gestor/ponto/pedidos-de-ajuste` | listarPedidosDeAjusteDaEquipe | em andamento | só Gestor; só pedidos de quem é da equipe e está na etapa dele (ou já decididos por ele); filtros do contrato |
| `GET /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}` | obterPedidoDeAjusteDaEquipe | em andamento | pedido de fora da equipe dele = 404 |
| `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | aprovarPedidoDeAjusteComoGestor | em andamento | só a etapa do gestor: o pedido passa ao RH (sem aplicar nada) e o `ID_GESTOR` fica; 409 `PEDIDO_JA_DECIDIDO` (decisão condicional, duas abas: uma só vale) e 422 `PEDIDO_ETAPA_INCORRETA` e `PEDIDO_PROPRIO` |
| `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | recusarPedidoDeAjusteComoGestor | em andamento | encerra o pedido (RECUSADO) com a observação obrigatória; e-mail `RESULTADO_AJUSTE_PONTO` à pessoa (sem e-mail corporativo, sem e-mail); mesmos 409/422 de aprovar |
| `GET /api/rh/ponto/pedidos-de-ajuste` | listarPedidosDeAjusteParaRh | em andamento | só RH; a fila da empresa toda, filtrável por `etapa` |
| `GET /api/rh/ponto/pedidos-de-ajuste/{pedidoId}` | obterPedidoDeAjusteParaRh | em andamento | só RH |
| `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | aprovarPedidoDeAjusteComoRh | em andamento | decisão FINAL: aplica as batidas do pedido, tudo ou nada, na transação com a pessoa travada; pedido no gestor que está ativo = 422 `PEDIDO_RH_FORA_DE_GESTORES`; o recálculo do banco de horas entra com a consolidação (3c) |
| `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | recusarPedidoDeAjusteComoRh | em andamento | só RH; a observação é obrigatória; RH não decide pedido próprio (422 `PEDIDO_PROPRIO`, vence `PEDIDO_RH_FORA_DE_GESTORES`) |
| `PUT /api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}` | corrigirMarcacoesDoDia | em andamento | só RH; a lista substitui as batidas do dia (lista vazia limpa o dia); a sequência tem de ser válida (422 `PONTO_SEQUENCIA_INVALIDA`); dia com pedido em análise recusa (409 `PONTO_DIA_COM_PEDIDO_EM_ANALISE`); um evento de auditoria com `Mudancas` |
| `GET /api/eu/ponto/atestados` | listarMeusAtestados | em andamento | qualquer perfil logado (sem 403): só os atestados da própria pessoa, o mais recente primeiro; `situacao` filtra; nunca traz o caminho do arquivo |
| `POST /api/eu/ponto/atestados` | enviarAtestado | em andamento | `multipart/form-data` (PDF, JPG ou PNG pelos primeiros bytes, até 5 MiB, o limite do contrato; os documentos têm 10 MiB); dias inteiros ou algumas horas de um dia, com todos os erros de campo e de arquivo juntos num 400; nasce `ENVIADO` (201, sem `Location`); o arquivo vai para o Azurite em desenvolvimento e, em produção, depende do Storage Account (Task 9, do dono); auditoria `ATESTADOS`/`CRIOU` sem o nome do arquivo |
| `GET /api/gestor/ponto/atestados` | listarAtestadosDaEquipe | em andamento | só Gestor; só a equipe dele (sem o próprio gestor); pessoa de outra equipe em `funcionarioId` devolve a lista vazia; gestor sem setor, lista vazia |
| `GET /api/gestor/ponto/atestados/{atestadoId}/download` | baixarAtestadoDaEquipe | em andamento | só Gestor, só a equipe dele (outro: o mesmo 404 do inexistente); o evento `ATESTADOS`/`BAIXOU` é gravado ANTES do link (sem registro, sem link); o link vem do Azurite em desenvolvimento |
| `GET /api/rh/ponto/atestados` | listarAtestados | em andamento | só RH; a empresa toda, o mais recente primeiro; `busca` procura no nome da pessoa |
| `GET /api/rh/ponto/atestados/{atestadoId}/download` | baixarAtestado | em andamento | só RH; o evento `ATESTADOS`/`BAIXOU` é gravado ANTES do link; o link vem do Azurite em desenvolvimento |
| `POST /api/rh/ponto/atestados/{atestadoId}/validar` | validarAtestado | em andamento | só RH; `ENVIADO` vira `VALIDADO` (o dia fica `FALTA_COMPENSADA`); decisão final por UPDATE condicional: 409 `ATESTADO_JA_DECIDIDO` (duas abas, uma só vale); o RH valida o próprio atestado; manda recalcular cada dia já passado pela porta do banco de horas (a consolidação real entra na 3c); sem e-mail |
| `POST /api/rh/ponto/atestados/{atestadoId}/recusar` | recusarAtestado | em andamento | só RH; `motivo` de 1 a 500 caracteres (aparado); o dia segue como falta e nada é recalculado; mesmos 404 e 409 de validar; o motivo não vai para a auditoria |
| `GET /api/rh/ponto/feriados` | listarFeriados | em andamento | só RH; `ano` obrigatório (2000 a 2100), `mes` opcional (sem ele, o ano todo); os feriados que valem para a empresa (nacionais, estaduais da UF, municipais do município e os da empresa), só ativos, a menos de `incluirInativos`; por data, sem paginação; sem a empresa configurada não dá erro (só nacionais e da empresa); `podeEditar` só no municipal |
| `POST /api/rh/ponto/feriados` | criarFeriado | em andamento | só RH; sempre `MUNICIPAL` com a UF e o município da empresa; nome de 1 a 100 (aparado); 409 `FERIADO_DUPLICADO` na mesma data e nome (a chave do banco não diferencia caixa nem acento); 422 `EMPRESA_NAO_CONFIGURADA`; o evento `FERIADOS`/`CRIOU` é do interceptor |
| `PUT /api/rh/ponto/feriados/{feriadoId}` | editarFeriado | em andamento | só RH; só `MUNICIPAL` (422 `FERIADO_NAO_EDITAVEL` nos outros); `ativo: false` desativa sem apagar; mesmos 400 e 409 da criação; não pede a empresa; o log traz só o que mudou |
| `GET /api/eu/ponto/banco-de-horas` | obterMeuBancoDeHoras | em andamento | qualquer perfil logado (sem 403): só o da própria pessoa; `saldoMinutos` é a soma dos minutos de CADA linha (convertida de horas com 2 casas, arredondando) de TODOS os lançamentos, nunca a função do banco nem só os filtrados; do dia de referência mais recente para o mais antigo; `de` e `ate` inclusivos, `tipo` e paginação; `atualizadoAte` é o último dia consolidado (global, `null` se a consolidação nunca rodou) |
| `GET /api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas` | obterBancoDeHorasDeFuncionario | em andamento | só RH; o mesmo extrato de outra pessoa; pessoa que não existe devolve 404 |

## Pendências conhecidas

- **Pedidos parados no gestor (só o ponto implementa):** trocar, tirar ou desligar um gestor e ligar "Gestor ausente" chamam `IPedidosParadosNoGestor`; a fatia 3 (ponto) já move os pedidos de ajuste, mas até a fatia 7 (férias) implementar a dela **nenhum pedido de férias muda de etapa** (`CONVENCOES.md`, seção 9). Um pedido de ponto parado quando a pessoa muda de setor continua com o gestor antigo (dívida, pergunta ao dono).
