# Rotas do contrato, por fatia

> GERADO por `montar.mjs`. Cada linha: o que a rota faz, em uma frase, e quem pode usá-la. `*` = depende de decisão ainda aberta (ver `PROVISORIOS.md`).

## fatia 0 autenticacao (5 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `POST /api/auth/entrar` | `entrar` | Entra com e-mail e senha e recebe a sessão. | Publico |
| `POST /api/auth/renovar` | `renovarSessao` | Troca o refresh token por uma sessão nova. | Publico |
| `POST /api/auth/sair` | `sair` | Encerra a sessão e invalida o refresh token. | Publico |
| `GET /api/auth/eu` | `obterUsuarioLogado` | Mostra quem está logado. | Funcionario, Gestor, RH |
| `GET /health` | `verificarSaude` | Diz se a API e o banco estão no ar. | Publico |

## fatia 1 colaboradores (43 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/rh/funcionarios` | `listarFuncionarios` | Lista os colaboradores, com busca e filtros por situação, setor e contrato. | RH |
| `POST /api/rh/funcionarios` | `criarFuncionario` | Cadastra um colaborador e, se marcado, envia o convite de acesso. | RH |
| `GET /api/rh/funcionarios/{funcionarioId}` | `obterFichaDoFuncionario` | Mostra a ficha do colaborador (aba Dados), com CPF e telefone completos. | RH |
| `PUT /api/rh/funcionarios/{funcionarioId}` | `editarFuncionario` | Altera os dados cadastrais do colaborador (botão "Editar dados"). | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/indicadores` | `obterIndicadoresDoFuncionario` | Mostra os cartões de resumo da ficha (banco de horas, férias, advertências, próximo pagamento). | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/desligamento` | `inativarFuncionario` | Inativa o colaborador (botão "Inativar"), a partir do último dia de trabalho. | RH |
| `DELETE /api/rh/funcionarios/{funcionarioId}/desligamento` | `cancelarDesligamentoAgendado` | Cancela um desligamento agendado que ainda não foi gravado. | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/readmissao` | `readmitirFuncionario` | Readmite um colaborador inativo (botão "Readmitir"), com nova data de admissão. | RH |
| `GET /api/gestor/equipe` | `listarEquipeDoGestor` | Lista a equipe do gestor (as pessoas do setor dele, sem ele) com a situação de hoje de cada uma. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}` | `obterFichaDaEquipe` | Mostra a ficha de uma pessoa da equipe (aba Dados), sem CPF e com telefone mascarado. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/indicadores` | `obterIndicadoresDaEquipe` | Mostra os cartões de resumo da ficha da equipe (banco de horas, férias, treinamentos, advertências). | Gestor |
| `GET /api/rh/funcionarios/{funcionarioId}/documentos` | `listarDocumentosDoFuncionario` | Lista os documentos da ficha do colaborador. | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/documentos` | `adicionarDocumentoDoFuncionario` | Anexa um documento à ficha, ou pede para a pessoa enviar. | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/documentos/{documentoId}/download` | `baixarDocumentoDoFuncionario` | Devolve um link temporário para ver o documento (botão "Ver"). | RH |
| `GET /api/eu/documentos` | `listarMeusDocumentos` | Lista os meus documentos (os já enviados e os que o RH pediu). | Funcionario, Gestor, RH |
| `POST /api/eu/documentos/{documentoId}/arquivo` | `enviarMeuDocumento` | Envia o arquivo de um documento que o RH pediu. | Funcionario, Gestor, RH |
| `GET /api/eu/documentos/{documentoId}/download` | `baixarMeuDocumento` | Devolve um link temporário para ver um documento meu. | Funcionario, Gestor, RH |
| `GET /api/rh/cargos` | `listarCargos` | Lista os cargos, com a carga semanal e quantas pessoas têm cada um. | RH |
| `POST /api/rh/cargos` | `criarCargo` | Cria um cargo (botão "Novo cargo"). | RH |
| `PUT /api/rh/cargos/{cargoId}` | `editarCargo` | Altera um cargo (botão "Editar"). | RH |
| `GET /api/rh/setores` | `listarSetores` | Lista os setores, com o gestor e quantas pessoas há em cada um. | RH |
| `POST /api/rh/setores` | `criarSetor` | Cria um setor, com o gestor opcional. | RH |
| `PUT /api/rh/setores/{setorId}` | `editarSetor` | Altera um setor e troca o gestor dele. | RH |
| `GET /api/rh/gestores` | `listarGestores` | Lista os gestores, o setor de cada um e quem está ausente. | RH |
| `POST /api/rh/gestores` | `cadastrarGestor` | Cadastra uma pessoa como gestor, sem ligá-la a um setor. | RH |
| `PUT /api/rh/gestores/{gestorId}/ausencia` | `configurarAusenciaDoGestor` | Marca ou desmarca um gestor como ausente (a etapa dele passa a ser aprovada sozinha). | RH |
| `GET /api/rh/beneficios` | `listarBeneficiosDoCatalogo` | Lista os benefícios do catálogo ("O que a empresa oferece"). | RH |
| `POST /api/rh/beneficios` | `criarBeneficioDoCatalogo` | Cria um benefício do catálogo. | RH |
| `PUT /api/rh/beneficios/{beneficioId}` | `editarBeneficioDoCatalogo` | Altera um benefício do catálogo. | RH |
| `DELETE /api/rh/beneficios/{beneficioId}` | `excluirBeneficioDoCatalogo` | Exclui um benefício do catálogo que nunca foi usado. | RH |
| `GET /api/eu/empresa` | `obterEmpresa` | Mostra a configuração da empresa (nome, UF, município, horário padrão, dia de pagamento). | Funcionario, Gestor, RH |
| `PUT /api/rh/empresa` | `salvarEmpresa` | Cria ou altera a configuração da empresa. | RH |
| `GET /api/rh/usuarios` | `listarUsuariosDoSistema` | Lista os logins de acesso ao portal, com a situação de cada um. | RH |
| `POST /api/rh/usuarios/{funcionarioId}/convite` | `enviarConviteDeAcesso` | Envia ou reenvia o convite de acesso ao portal. | RH |
| `POST /api/rh/usuarios/{funcionarioId}/ativar` | `ativarUsuarioDoSistema` | Libera o login de um colaborador (botão "Ativar"). | RH |
| `POST /api/rh/usuarios/{funcionarioId}/desativar` | `desativarUsuarioDoSistema` | Bloqueia o login de um colaborador (botão "Desativar"). | RH |
| `GET /api/rh/auditoria` | `listarEventosDeAuditoria` | Lista os eventos do log de auditoria, do mais recente ao mais antigo. | RH |
| `GET /api/rh/auditoria/exportacao` | `exportarAuditoria` | Baixa o log de auditoria filtrado em CSV (botão "Exportar CSV"). | RH |
| `GET /api/rh/auditoria/{eventoId}` | `obterEventoDeAuditoria` | Mostra os detalhes de um evento (registro afetado, origem e o que mudou). | RH |
| `POST /api/auth/senha/esqueci` | `solicitarRedefinicaoDeSenha` | Pede um link por e-mail para redefinir a senha ("Esqueci minha senha"). | Publico |
| `POST /api/auth/senha/validar-link` | `validarLinkDeSenha` | Confere se o link do convite ou da redefinição ainda vale. | Publico |
| `POST /api/auth/senha/definir` | `definirSenhaPeloLink` | Define a senha usando o link do convite ou da redefinição. | Publico |
| `POST /api/eu/senha` | `trocarMinhaSenha` | Troca a senha de quem está logado (informando a senha atual). | Funcionario, Gestor, RH |

## fatia 2 importacao (3 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/rh/importacoes/modelo` | `baixarModeloDeImportacao` | Baixa a planilha modelo para importar colaboradores. * | RH |
| `POST /api/rh/importacoes` | `validarPlanilhaDeImportacao` | Envia a planilha e recebe a prévia com os erros por linha e coluna (sem cadastrar ninguém). | RH |
| `POST /api/rh/importacoes/{importacaoId}/confirmar` | `confirmarImportacao` | Confirma a importação e cadastra todos os colaboradores da planilha (tudo ou nada). * | RH |

## fatia 3 ponto (34 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/eu/ponto/hoje` | `obterPontoHoje` | Mostra o cartão "Seu ponto hoje" (batidas do dia, próxima batida e saldo do banco de horas). | Funcionario, Gestor, RH |
| `POST /api/eu/ponto/marcacoes` | `registrarMarcacaoDePonto` | Bate o ponto agora (entrada ou saída), com a hora do servidor. * | Funcionario, Gestor, RH |
| `GET /api/eu/ponto/espelho` | `obterMeuEspelhoDePonto` | Mostra o espelho de ponto do mês da própria pessoa. | Funcionario, Gestor, RH |
| `GET /api/eu/ponto/pedidos-de-ajuste` | `listarMeusPedidosDeAjuste` | Lista os pedidos de ajuste de ponto da própria pessoa. | Funcionario, Gestor, RH |
| `POST /api/eu/ponto/pedidos-de-ajuste` | `criarPedidoDeAjuste` | Pede o ajuste das batidas de um dia (uma ou várias batidas no mesmo pedido). * | Funcionario, Gestor, RH |
| `GET /api/eu/ponto/pedidos-de-ajuste/{pedidoId}` | `obterMeuPedidoDeAjuste` | Mostra um pedido de ajuste da própria pessoa. | Funcionario, Gestor, RH |
| `GET /api/eu/ponto/banco-de-horas` | `obterMeuBancoDeHoras` | Mostra o saldo e o extrato do banco de horas da própria pessoa. * | Funcionario, Gestor, RH |
| `GET /api/eu/ponto/atestados` | `listarMeusAtestados` | Lista os atestados que a própria pessoa enviou. | Funcionario, Gestor, RH |
| `POST /api/eu/ponto/atestados` | `enviarAtestado` | Envia um atestado (arquivo mais o período coberto). | Funcionario, Gestor, RH |
| `GET /api/gestor/ponto/equipe` | `obterPontoDaEquipe` | Mostra o ponto de hoje (ou de um dia) de toda a equipe do gestor. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/ponto` | `obterEspelhoDePontoDaPessoaDaEquipe` | Mostra o espelho de ponto do mês de uma pessoa da equipe. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/ponto/espelho/download` | `baixarEspelhoDePontoPdfDaEquipe` | Gera o espelho de ponto de uma pessoa da equipe em PDF e devolve um link temporário. | Gestor |
| `GET /api/gestor/ponto/pedidos-de-ajuste` | `listarPedidosDeAjusteDaEquipe` | Lista os pedidos de ajuste de ponto da equipe do gestor. | Gestor |
| `GET /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}` | `obterPedidoDeAjusteDaEquipe` | Mostra um pedido de ajuste da equipe (modal "Pedido de ajuste de ponto"). | Gestor |
| `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | `aprovarPedidoDeAjusteComoGestor` | Aprova o pedido na etapa do gestor (depois dele, o RH confirma). | Gestor |
| `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | `recusarPedidoDeAjusteComoGestor` | Recusa o pedido na etapa do gestor (o pedido acaba aqui). | Gestor |
| `GET /api/gestor/ponto/atestados` | `listarAtestadosDaEquipe` | Lista os atestados da equipe do gestor. | Gestor |
| `GET /api/gestor/ponto/atestados/{atestadoId}/download` | `baixarAtestadoDaEquipe` | Devolve o link temporário do arquivo de um atestado da equipe. | Gestor |
| `GET /api/rh/funcionarios/{funcionarioId}/ponto` | `obterEspelhoDePontoDeFuncionario` | Mostra o espelho de ponto do mês de qualquer pessoa (tela "Ponto e feriados" e aba Ponto da ficha). | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/ponto/espelho/download` | `baixarEspelhoDePontoPdf` | Gera o espelho de ponto de uma pessoa em PDF e devolve um link temporário. | RH |
| `PUT /api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}` | `corrigirMarcacoesDoDia` | O RH corrige direto as batidas de um dia (botão "Editar" do espelho). | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas` | `obterBancoDeHorasDeFuncionario` | Mostra o saldo e o extrato do banco de horas de qualquer pessoa. | RH |
| `GET /api/rh/ponto/pedidos-de-ajuste` | `listarPedidosDeAjusteParaRh` | Lista os pedidos de ajuste de ponto de toda a empresa (modal "Pedidos de ajuste de ponto"). | RH |
| `GET /api/rh/ponto/pedidos-de-ajuste/{pedidoId}` | `obterPedidoDeAjusteParaRh` | Mostra um pedido de ajuste (painel de detalhe do modal e o botão "Revisar pedido"). | RH |
| `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | `aprovarPedidoDeAjusteComoRh` | Aprova o pedido na etapa do RH e corrige o ponto. | RH |
| `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | `recusarPedidoDeAjusteComoRh` | Recusa o pedido na etapa do RH (nenhuma batida muda). | RH |
| `GET /api/rh/ponto/feriados` | `listarFeriados` | Lista os feriados do mês (ou do ano) que valem para a empresa. | RH |
| `POST /api/rh/ponto/feriados` | `criarFeriado` | Cadastra um feriado municipal (vale para todos). | RH |
| `PUT /api/rh/ponto/feriados/{feriadoId}` | `editarFeriado` | Edita um feriado municipal (botão "Editar" da lista). | RH |
| `GET /api/rh/ponto/folha/exportacao` | `exportarPontoParaFolha` | Baixa o CSV de ponto do mês para a folha de pagamento (botão "Exportar para a folha"). * | RH |
| `GET /api/rh/ponto/atestados` | `listarAtestados` | Lista os atestados de toda a empresa para o RH validar. | RH |
| `GET /api/rh/ponto/atestados/{atestadoId}/download` | `baixarAtestado` | Devolve o link temporário do arquivo de um atestado. | RH |
| `POST /api/rh/ponto/atestados/{atestadoId}/validar` | `validarAtestado` | Valida o atestado (o dia vira "Falta compensada"). | RH |
| `POST /api/rh/ponto/atestados/{atestadoId}/recusar` | `recusarAtestado` | Recusa o atestado com um motivo (o dia segue como falta). | RH |

## fatia 4 comunicacao e documentos (35 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/rh/avisos` | `listarAvisos` | Lista os avisos do RH por situação (ativos, agendados, expirados e rascunhos). | RH |
| `POST /api/rh/avisos` | `criarAviso` | Cria um aviso, salvando como rascunho ou publicando (agora ou numa data futura). | RH |
| `GET /api/rh/avisos/resumo` | `obterResumoDosAvisos` | Mostra os números do topo da tela de avisos e a contagem de cada aba. | RH |
| `GET /api/rh/avisos/{avisoId}` | `obterAviso` | Mostra um aviso inteiro, com o conteúdo, para o painel de detalhes do RH. | RH |
| `PUT /api/rh/avisos/{avisoId}` | `editarAviso` | Edita o título, o conteúdo, a data de publicação, a expiração e o "fixar" de um aviso. | RH |
| `DELETE /api/rh/avisos/{avisoId}` | `excluirAviso` | Exclui um aviso que ainda não foi publicado (rascunho ou agendado). | RH |
| `POST /api/rh/avisos/{avisoId}/publicar` | `publicarAviso` | Publica um rascunho (agora ou na data prevista, se for futura). | RH |
| `POST /api/rh/avisos/{avisoId}/encerrar` | `encerrarAviso` | Encerra um aviso ativo agora ("Encerrar agora"). | RH |
| `GET /api/rh/avisos/{avisoId}/leituras` | `listarLeiturasDoAviso` | Mostra quem viu e quem ainda não viu um aviso ("Quem viu"). | RH |
| `POST /api/rh/avisos/{avisoId}/lembretes` | `lembrarQuemNaoViuAviso` | Manda um e-mail de lembrete a quem ainda não viu o aviso ("Lembrar quem não viu"). | RH |
| `GET /api/eu/avisos` | `listarMeusAvisos` | Lista os avisos vigentes para quem está logado (telas 37 e 50 e o cartão da tela inicial). | Funcionario, Gestor, RH |
| `GET /api/eu/avisos/{avisoId}` | `obterMeuAviso` | Mostra um aviso vigente inteiro, com o conteúdo (tela 38). | Funcionario, Gestor, RH |
| `POST /api/eu/avisos/{avisoId}/leitura` | `marcarAvisoComoLido` | Marca o aviso como lido para quem está logado (ao abrir o aviso). | Funcionario, Gestor, RH |
| `GET /api/rh/funcionarios/{funcionarioId}/holerites` | `listarHoleritesDoFuncionario` | Lista os holerites de uma pessoa, para a aba Holerites da ficha (tela 07). | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/holerites` | `enviarHolerite` | Publica ou agenda o PDF do holerite de uma pessoa (modal "Enviar holerite", tela 08). | RH |
| `POST /api/rh/holerites/lote` | `enviarHoleritesEmLote` | Publica ou agenda vários holerites de uma vez, um PDF por pessoa. * | RH |
| `POST /api/rh/holerites/{holeriteId}/versoes` | `substituirHolerite` | Corrige um holerite enviado errado, guardando uma nova versão no lugar dele. * | RH |
| `GET /api/rh/holerites/{holeriteId}/download` | `obterLinkDeDownloadDoHolerite` | Devolve um link temporário para baixar o PDF de um holerite (RH). | RH |
| `GET /api/eu/holerites` | `listarMeusHolerites` | Lista os holerites da pessoa logada (telas 36 e 49). | Funcionario, Gestor, RH |
| `GET /api/eu/holerites/{holeriteId}/download` | `obterLinkDeDownloadDoMeuHolerite` | Devolve um link temporário para baixar o PDF de um holerite da própria pessoa. * | Funcionario, Gestor, RH |
| `GET /api/rh/advertencias` | `listarAdvertencias` | Lista as advertências de todos os setores, com filtros (tela 14). | RH |
| `GET /api/rh/advertencias/resumo` | `obterResumoDasAdvertencias` | Mostra os números do topo da tela de advertências e a contagem de cada aba. | RH |
| `GET /api/rh/advertencias/{advertenciaId}` | `obterAdvertencia` | Mostra uma advertência inteira, com a contestação, para o RH ("Abrir"). | RH |
| `POST /api/rh/advertencias/{advertenciaId}/resposta` | `responderContestacaoDeAdvertencia` | O RH responde à contestação, mantendo a advertência ou acatando a contestação. * | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/advertencias` | `listarAdvertenciasDoFuncionario` | Lista as advertências de uma pessoa, para a aba Advertências da ficha (tela 11). | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/advertencias` | `registrarAdvertencia` | O RH registra uma advertência para uma pessoa (telas 11 e 15). | RH |
| `GET /api/gestor/advertencias` | `listarAdvertenciasDaEquipe` | Lista as advertências das pessoas da equipe do gestor (tela 59). | Gestor |
| `GET /api/gestor/advertencias/resumo` | `obterResumoDasAdvertenciasDaEquipe` | Mostra os números do topo da tela de advertências do setor e o quadro "Por pessoa". | Gestor |
| `GET /api/gestor/advertencias/{advertenciaId}` | `obterAdvertenciaDaEquipe` | Mostra uma advertência inteira de uma pessoa da equipe, para o gestor ("Abrir"). | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/advertencias` | `listarAdvertenciasDoFuncionarioDaEquipe` | Lista as advertências de uma pessoa da equipe, para a aba Advertências da ficha do gestor. | Gestor |
| `POST /api/gestor/equipe/{funcionarioId}/advertencias` | `registrarAdvertenciaDaEquipe` | O gestor registra uma advertência para uma pessoa da própria equipe (telas 53 e 60). | Gestor |
| `GET /api/eu/advertencias` | `listarMinhasAdvertencias` | Lista as advertências da pessoa logada, com o que precisa de ciência em primeiro lugar (tela 43). | Funcionario, Gestor, RH |
| `POST /api/eu/advertencias/{advertenciaId}/ciencia` | `confirmarCienciaDeAdvertencia` | A pessoa confirma que tomou ciência da advertência ("Confirmar ciência"). | Funcionario, Gestor, RH |
| `POST /api/eu/advertencias/{advertenciaId}/contestacao` | `contestarAdvertencia` | A pessoa contesta a advertência, explicando o motivo ("Contestar"). | Funcionario, Gestor, RH |
| `GET /api/eu/aniversariantes` | `listarAniversariantes` | Lista quem faz aniversário hoje e nos próximos 14 dias (cartão da tela inicial, fatia 7d). | Funcionario, Gestor, RH |

## fatia 5 formularios (32 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/rh/formularios` | `listarFormulariosRh` | Lista os formulários, com busca e filtros por tipo e situação. | RH |
| `POST /api/rh/formularios` | `criarFormulario` | Cria o formulário em rascunho (etapa Detalhes do assistente). * | RH |
| `GET /api/rh/formularios/{formularioId}` | `obterFormularioRh` | Mostra o formulário inteiro (detalhes, perguntas e publicação), para editar. | RH |
| `PUT /api/rh/formularios/{formularioId}` | `editarFormulario` | Altera título, descrição, tipo e anonimato do formulário. | RH |
| `DELETE /api/rh/formularios/{formularioId}` | `excluirFormulario` | Exclui o formulário, em qualquer situação, com confirmação se alguém ainda pode responder. * | RH |
| `POST /api/rh/formularios/{formularioId}/perguntas` | `adicionarPerguntaFormulario` | Acrescenta uma pergunta ao formulário (etapa Perguntas). | RH |
| `PUT /api/rh/formularios/{formularioId}/perguntas/{perguntaId}` | `editarPerguntaFormulario` | Altera uma pergunta (botão Editar da etapa Perguntas). | RH |
| `DELETE /api/rh/formularios/{formularioId}/perguntas/{perguntaId}` | `excluirPerguntaFormulario` | Apaga uma pergunta que ainda não tem resposta. | RH |
| `GET /api/rh/formularios/{formularioId}/verificacao` | `verificarFormulario` | Confere o formulário antes de publicar (etapa Verificação). * | RH |
| `PUT /api/rh/formularios/{formularioId}/publicacao` | `salvarPublicacaoFormulario` | Guarda as escolhas da etapa Publicação sem publicar ("Salvar rascunho e sair"). | RH |
| `POST /api/rh/formularios/{formularioId}/publicar` | `publicarFormulario` | Publica o formulário agora ou agenda a publicação (botão Publicar formulário). | RH |
| `POST /api/rh/formularios/{formularioId}/encerrar` | `encerrarFormulario` | Encerra o formulário: ele deixa de aceitar respostas. | RH |
| `POST /api/rh/formularios/{formularioId}/destinatarios` | `adicionarDestinatariosFormulario` | Acrescenta pessoas a um formulário que é para pessoas escolhidas. | RH |
| `GET /api/rh/formularios/{formularioId}/respondentes` | `listarRespondentesFormulario` | Mostra quem respondeu e quem falta num formulário para funcionários (tela Quem respondeu). | RH |
| `GET /api/rh/formularios/{formularioId}/candidatos` | `listarCandidatosFormulario` | Lista os candidatos que responderam um formulário por link. | RH |
| `GET /api/rh/formularios/{formularioId}/respostas/funcionarios/{funcionarioId}` | `obterRespostasDeFuncionario` | Mostra o que uma pessoa respondeu (botão Ver respostas). | RH |
| `GET /api/rh/formularios/{formularioId}/respostas/candidatos/{candidatoId}` | `obterRespostasDeCandidato` | Mostra o que um candidato respondeu. | RH |
| `GET /api/rh/formularios/{formularioId}/arquivos/{respostaId}/download` | `baixarArquivoDeResposta` | Dá um link temporário para baixar o arquivo enviado numa resposta. | RH |
| `GET /api/rh/formularios/{formularioId}/candidatos/{candidatoId}/curriculo/download` | `baixarCurriculoDeCandidato` | Dá um link temporário para baixar o currículo do candidato. | RH |
| `GET /api/rh/formularios/{formularioId}/resultado` | `obterResultadoFormulario` | Mostra o resultado geral, pergunta por pergunta (formulário anônimo: só depois de encerrado). | RH |
| `GET /api/rh/formularios/{formularioId}/avaliados` | `listarAvaliadosFormulario` | Lista as pessoas avaliadas numa avaliação, com quantas respostas cada uma tem. | RH |
| `GET /api/rh/formularios/{formularioId}/exportacao` | `exportarRespostasFormulario` | Baixa as respostas do formulário em CSV (botão Baixar respostas). | RH |
| `POST /api/rh/formularios/{formularioId}/lembretes` | `lembrarQuemFaltaFormulario` | Manda um lembrete por e-mail a quem ainda não respondeu. | RH |
| `GET /api/gestor/formularios` | `listarFormulariosDaEquipe` | Lista os pedidos do RH que a equipe do gestor tem aberto (coluna Pedidos do RH). | Gestor |
| `GET /api/gestor/formularios/{formularioId}/respondentes` | `listarRespondentesDaEquipe` | Mostra quem da equipe respondeu e quem falta num formulário (painel Quem respondeu). | Gestor |
| `POST /api/gestor/formularios/{formularioId}/lembretes` | `lembrarQuemFaltaNaEquipe` | Manda um lembrete por e-mail a quem da equipe ainda não respondeu. | Gestor |
| `GET /api/eu/formularios` | `listarMeusFormularios` | Lista os formulários que a pessoa logada deve ou pode responder, e os que já respondeu. | Funcionario, Gestor, RH |
| `GET /api/eu/formularios/{formularioId}` | `obterFormularioParaResponder` | Abre o formulário para a pessoa responder (perguntas e, nas avaliações, quem avaliar). | Funcionario, Gestor, RH |
| `POST /api/eu/formularios/{formularioId}/respostas` | `responderFormulario` | Envia as respostas da pessoa logada. | Funcionario, Gestor, RH |
| `GET /api/eu/formularios/{formularioId}/minhas-respostas` | `obterMinhasRespostas` | Mostra o que a pessoa logada respondeu (botão Ver minhas respostas). | Funcionario, Gestor, RH |
| `GET /api/publico/formularios/{token}` | `obterFormularioPublico` | Abre o formulário do candidato pelo link, sem login. * | Publico |
| `POST /api/publico/formularios/{token}/respostas` | `responderFormularioPublico` | Envia a candidatura e as respostas do candidato. * | Publico |

## fatia 6 tad (31 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/rh/treinamentos` | `listarTreinamentos` | Lista os treinamentos com o andamento de cada um, em abas por situação. | RH |
| `POST /api/rh/treinamentos` | `criarTreinamento` | Cria um treinamento como rascunho. | RH |
| `GET /api/rh/treinamentos/resumo` | `obterIndicadoresDeTreinamentos` | Mostra os quatro números do topo da tela de treinamentos. | RH |
| `GET /api/rh/treinamentos/onboarding` | `listarTreinamentosDeOnboarding` | Lista os treinamentos de onboarding em vigor, para a caixa "Incluir no treinamento" do cadastro. * | RH |
| `GET /api/rh/treinamentos/publico/contagem` | `contarPublicoDeTreinamento` | Conta quantas pessoas o público escolhido alcança. | RH |
| `GET /api/rh/treinamentos/{treinamentoId}` | `obterTreinamento` | Mostra um treinamento completo, com os módulos, para editar. | RH |
| `PUT /api/rh/treinamentos/{treinamentoId}` | `atualizarTreinamento` | Salva os dados do treinamento (detalhes, certificado e publicação). * | RH |
| `DELETE /api/rh/treinamentos/{treinamentoId}` | `excluirTreinamento` | Exclui um treinamento que ainda não emitiu certificado, com confirmação quando há gente que não terminou. | RH |
| `POST /api/rh/treinamentos/{treinamentoId}/publicar` | `publicarTreinamento` | Publica o treinamento (acesso liberado na data de início). | RH |
| `POST /api/rh/treinamentos/{treinamentoId}/encerrar` | `encerrarTreinamento` | Encerra o treinamento (ninguém novo entra; quem já começou pode terminar). | RH |
| `POST /api/rh/treinamentos/{treinamentoId}/modulos` | `adicionarModuloDeTreinamento` | Acrescenta um módulo (vídeo, texto, link ou PDF) ao treinamento. * | RH |
| `PUT /api/rh/treinamentos/{treinamentoId}/modulos/ordem` | `reordenarModulosDeTreinamento` | Muda a ordem dos módulos. * | RH |
| `PUT /api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}` | `atualizarModuloDeTreinamento` | Edita um módulo (botão "Editar" do passo Conteúdo). * | RH |
| `DELETE /api/rh/treinamentos/{treinamentoId}/modulos/{moduloId}` | `excluirModuloDeTreinamento` | Remove um módulo do rascunho. * | RH |
| `GET /api/rh/treinamentos/{treinamentoId}/participantes` | `listarParticipantesDeTreinamento` | Lista quem fez, quem está fazendo e quem não começou um treinamento. | RH |
| `GET /api/rh/treinamentos/{treinamentoId}/participantes/exportacao` | `exportarParticipantesDeTreinamento` | Baixa a lista de quem fez o treinamento em CSV. | RH |
| `GET /api/rh/treinamentos/{treinamentoId}/participantes/{funcionarioId}/certificado/download` | `baixarCertificadoDeParticipante` | Dá um link temporário para baixar o certificado em PDF de uma pessoa. * | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/treinamentos` | `listarTreinamentosDeFuncionario` | Mostra os treinamentos de uma pessoa, para a aba Treinamentos da ficha. | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/treinamentos/onboarding` | `incluirFuncionarioNoOnboarding` | Inclui uma pessoa no treinamento de onboarding (liga a caixa "Incluir no treinamento" do cadastro). * | RH |
| `GET /api/eu/treinamentos` | `listarMeusTreinamentos` | Lista os meus treinamentos, em cartões, por aba. | Funcionario, Gestor, RH |
| `GET /api/eu/treinamentos/{treinamentoId}` | `obterMeuTreinamento` | Mostra um treinamento meu, com os módulos e o andamento em cada um. | Funcionario, Gestor, RH |
| `POST /api/eu/treinamentos/{treinamentoId}/inscricao` | `inscreverMeEmTreinamento` | Inscreve a pessoa em um treinamento opcional ("Quero me inscrever"). | Funcionario, Gestor, RH |
| `POST /api/eu/treinamentos/{treinamentoId}/iniciar` | `iniciarMeuTreinamento` | Marca que a pessoa começou o treinamento ("Começar"). | Funcionario, Gestor, RH |
| `GET /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}` | `obterConteudoDeModulo` | Mostra o conteúdo de um módulo. * | Funcionario, Gestor, RH |
| `GET /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/download` | `baixarArquivoDeModulo` | Dá um link temporário para abrir o arquivo PDF de um módulo. * | Funcionario, Gestor, RH |
| `POST /api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}/concluir` | `concluirModuloDeTreinamento` | Marca um módulo como concluído; ao concluir o último, conclui o treinamento. * | Funcionario, Gestor, RH |
| `GET /api/eu/treinamentos/{treinamentoId}/certificado/download` | `baixarMeuCertificado` | Dá um link temporário para baixar o meu certificado em PDF ("Ver certificado"). * | Funcionario, Gestor, RH |
| `GET /api/gestor/treinamentos` | `listarTreinamentosDaEquipe` | Lista os treinamentos que valem para o meu setor, com o andamento da equipe. | Gestor |
| `GET /api/gestor/treinamentos/{treinamentoId}/equipe` | `listarPessoasDaEquipeNoTreinamento` | Mostra quem da minha equipe fez um treinamento. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/treinamentos` | `listarTreinamentosDePessoaDaEquipe` | Mostra os treinamentos de uma pessoa da minha equipe, para a aba Treinamentos da ficha. | Gestor |
| `GET /api/publico/certificados/{codigoValidacao}` | `validarCertificado` | Confere um certificado pelo código, sem precisar de login. * | Publico |

## fatia 7 gestao (41 rotas)

| Rota | operationId | O que faz | Quem usa |
|---|---|---|---|
| `GET /api/eu/ferias` | `obterMinhasFerias` | Mostra o saldo, os períodos aquisitivos e os pedidos de férias de quem está logado. | Funcionario, Gestor, RH |
| `POST /api/eu/ferias` | `pedirFerias` | Pede férias: primeiro o gestor analisa e depois o RH confirma. * | Funcionario, Gestor, RH |
| `POST /api/eu/ferias/conferencia` | `conferirPedidoDeFerias` | Confere as regras de um pedido de férias sem gravar nada (a lista de "vistos" do painel). * | Funcionario, Gestor, RH |
| `POST /api/eu/ferias/{feriasId}/sugestao/aceitar` | `aceitarSugestaoDeDataDasFerias` | Aceita as datas que o gestor sugeriu; o pedido segue para o RH confirmar. | Funcionario, Gestor, RH |
| `POST /api/eu/ferias/{feriasId}/sugestao/recusar` | `recusarSugestaoDeDataDasFerias` | Recusa as datas sugeridas; o pedido termina cancelado. | Funcionario, Gestor, RH |
| `GET /api/gestor/ferias` | `listarFeriasDaEquipe` | Lista os pedidos de férias da equipe do gestor (por padrão, os que esperam a análise dele). | Gestor |
| `GET /api/gestor/ferias/calendario` | `obterCalendarioDeFeriasDaEquipe` | Mostra as férias da equipe semana a semana (aprovadas e esperando o gestor). | Gestor |
| `POST /api/gestor/ferias/{feriasId}/aprovar` | `aprovarFeriasComoGestor` | O gestor aprova o pedido de férias da equipe (primeira etapa); o RH confirma depois. | Gestor |
| `POST /api/gestor/ferias/{feriasId}/recusar` | `recusarFeriasComoGestor` | O gestor recusa o pedido de férias da equipe; o pedido termina na hora. | Gestor |
| `POST /api/gestor/ferias/{feriasId}/sugerir-outra-data` | `sugerirOutraDataDasFerias` | O gestor sugere outras datas; a pessoa decide se aceita. | Gestor |
| `GET /api/gestor/equipe/{funcionarioId}/ferias` | `obterFeriasDaPessoaDaEquipe` | Mostra as férias de uma pessoa da equipe (aba Férias da ficha do gestor). | Gestor |
| `GET /api/rh/ferias` | `listarFeriasParaORh` | Lista os pedidos de férias de toda a empresa (por padrão, os que esperam a confirmação do RH). | RH |
| `POST /api/rh/ferias/{feriasId}/confirmar` | `confirmarFeriasComoRh` | O RH confirma o pedido de férias (segunda etapa); o pedido fica aprovado. | RH |
| `POST /api/rh/ferias/{feriasId}/recusar` | `recusarFeriasComoRh` | O RH recusa o pedido de férias (segunda etapa); o pedido termina. | RH |
| `GET /api/rh/funcionarios/{funcionarioId}/ferias` | `obterFeriasDaFichaDoFuncionario` | Mostra as férias de uma pessoa (aba Férias da ficha do RH). | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/ferias` | `registrarFeriasPeloRh` | O RH registra férias para uma pessoa; já nascem aprovadas. * | RH |
| `POST /api/rh/funcionarios/{funcionarioId}/ferias/conferencia` | `conferirRegistroDeFerias` | Confere as regras de um registro de férias do RH sem gravar nada (a lista de "vistos" do modal). * | RH |
| `GET /api/eu/beneficios` | `obterMeusBeneficios` | Mostra os benefícios da pessoa, os que ela pode pedir e os pedidos dela. | Funcionario, Gestor, RH |
| `POST /api/eu/beneficios-adesoes` | `pedirAdesaoABeneficio` | Pede para aderir a um benefício, com quem entra, a data de início e a autorização de desconto. | Funcionario, Gestor, RH |
| `POST /api/eu/beneficios-adesoes/{adesaoId}/dependentes` | `pedirInclusaoDeDependente` | Pede a inclusão de um dependente em um benefício que a pessoa já tem. | Funcionario, Gestor, RH |
| `POST /api/eu/beneficios-adesoes/{adesaoId}/trajetos` | `pedirAtualizacaoDeTrajeto` | Pede a atualização do trajeto do vale-transporte. | Funcionario, Gestor, RH |
| `GET /api/rh/beneficios-adesoes` | `listarPedidosDeBeneficio` | Lista as adesões a benefícios, com os pedidos de dependente e de trajeto (por padrão, os que esperam o RH). | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/aprovar` | `aprovarAdesaoABeneficio` | O RH aprova a adesão; o benefício fica ativo. | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/recusar` | `recusarAdesaoABeneficio` | O RH recusa a adesão, com o motivo. | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/dependentes/{dependenteId}/aprovar` | `aprovarDependenteDeBeneficio` | O RH aprova a inclusão de um dependente em uma adesão já ativa. | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/dependentes/{dependenteId}/recusar` | `recusarDependenteDeBeneficio` | O RH recusa a inclusão de um dependente, com o motivo. | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/trajetos/{trajetoId}/aprovar` | `aprovarTrajetoDeBeneficio` | O RH aprova a atualização do trajeto do vale-transporte. | RH |
| `POST /api/rh/beneficios-adesoes/{adesaoId}/trajetos/{trajetoId}/recusar` | `recusarTrajetoDeBeneficio` | O RH recusa a atualização do trajeto, com o motivo. | RH |
| `GET /api/eu/pagamento/proximo` | `obterProximoPagamento` | Mostra a data do próximo pagamento e o mês a que ele se refere. | Funcionario, Gestor, RH |
| `GET /api/rh/pagamento/excecoes` | `listarExcecoesDePagamento` | Lista as exceções de pagamento (a data de um mês que foge da regra do dia). | RH |
| `GET /api/rh/pagamento/calendario` | `obterCalendarioDePagamento` | Mostra a data efetiva de pagamento dos 12 meses de um ano. | RH |
| `PUT /api/rh/pagamento/excecoes/{ano}/{mes}` | `definirExcecaoDePagamento` | Cadastra (ou troca) a data de pagamento de um mês específico. | RH |
| `DELETE /api/rh/pagamento/excecoes/{ano}/{mes}` | `removerExcecaoDePagamento` | Remove a exceção de pagamento de um mês; o mês volta a seguir a regra. | RH |
| `GET /api/eu/painel` | `obterPainelDoFuncionario` | Traz os cartões da tela inicial (funcionário e gestor) que são da gestão, ou seja, férias, pagamento e benefícios. | Funcionario, Gestor, RH |
| `GET /api/eu/contadores-do-menu` | `obterContadoresDoMenuDoFuncionario` | Traz os números das bolinhas do menu (avisos não lidos, formulários pendentes e documentos pedidos pelo RH). | Funcionario, Gestor, RH |
| `GET /api/gestor/contadores-do-menu` | `obterContadoresDoMenuDoGestor` | Traz os números do menu do gestor (pendências da equipe e pedidos de ajuste de ponto). | Gestor |
| `GET /api/rh/contadores-do-menu` | `obterContadoresDoMenuDoRh` | Traz o número da bolinha "Ponto e feriados" do menu do RH. | RH |
| `GET /api/gestor/painel` | `obterPainelDaEquipe` | Traz o painel da equipe do gestor (hoje, pedidos esperando aprovação e a tabela de pessoas). | Gestor |
| `GET /api/rh/painel` | `obterPainelDoRh` | Traz o painel inicial do RH (pendências, próximos dias, treinamentos, presença agora e últimas alterações). * | RH |
| `GET /api/rh/painel/pendencias` | `listarPendenciasDoRh` | Lista, uma a uma, as pendências que esperam o RH (o link "Ver todas as pendências"). * | RH |
| `GET /api/rh/painel/presenca/sem-registro` | `listarPessoasSemRegistroHoje` | Lista as pessoas sem registro de ponto hoje (o link "Ver quem está sem registro"). | RH |
