# Levantamento da fatia 3 (ponto) do back-end (anexo do plano 2026-11-11-back-end-fatia-3-ponto.md)

Feito em 2026-11-11, na branch `feat/fatia-3-ponto` (igual à `main`, último commit `882242d`). Fontes lidas: roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, 68-84 e 151-178), `contrato/fontes/30-fatia-3-ponto.yaml` inteiro, `contrato/CONVENCOES.md`, `PROVISORIOS.md`, `ELENCO.md`, trechos de `00-base.yaml`, `20-fatia-1` e `70-fatia-7`, `V001`/`V002`/`V003`, `Banco de Dados/Scripts/LEIA-ME.md`, `back-end/src` e os apoios de `back-end/tests/RHSystem.Api.IntegrationTests`. Caminhos de código são relativos a `back-end/src/` (ou `back-end/tests/...`). **INFERIDO** = conclusão minha, não lida nem medida.

---

## A. As 34 operações

Perfis: F = Funcionario, G = Gestor, RH. `[F,G,RH]` = família `/api/eu` (a própria pessoa; nunca lista 403). Paginação = `pagina` (≥1, padrão 1) e `tamanhoPagina` (1-100, padrão 20). Toda operação tem 401 e 500; não repito.

| # | Rota | operationId | Perfil | Entrada | 2xx | Erros (além de 401/500) | Efeito declarado |
|---|---|---|---|---|---|---|---|
| 1 | GET `/api/eu/ponto/hoje` | obterPontoHoje | F,G,RH | — | 200 `PontoHoje` | — | lê; `agoraUtc` é o relógio do servidor; `dataDoPonto` pode ser ontem (jornada após meia-noite) |
| 2 | POST `/api/eu/ponto/marcacoes` | registrarMarcacaoDePonto | F,G,RH | `{tipoEsperado: ENTRADA\|SAIDA}` (obrig.) | 201 `PontoHoje` | 400; 409 `PONTO_SEQUENCIA_MUDOU`, 409 `PONTO_BATIDA_DUPLICADA`; 422 `FUNCIONARIO_INATIVO` (+`regraDoBanco`); 422 sem código (`regraDoBanco` "A data do ponto e anterior a admissao." ou "...posterior ao desligamento.") | hora do servidor; cria o `PONTOS` do dia se faltar (carga = cargo ÷ 5); bater em feriado/folga/férias é aceito (vira hora extra); auditoria com IP e origem; x-provisorio 12 h |
| 3 | GET `/api/eu/ponto/espelho` | obterMeuEspelhoDePonto | F,G,RH | `ano` (2000-2100), `mes` (1-12), opcionais | 200 `PontoEspelhoProprio` | 400 | dias 1→hoje (mês corrente) ou →fim; mês futuro `dias: []`; sem dias antes da admissão/depois do desligamento |
| 4 | GET `/api/eu/ponto/pedidos-de-ajuste` | listarMeusPedidosDeAjuste | F,G,RH | paginação, `status`, `etapa`, `ordenarPor` (CRIADO_EM\|DIA_DO_PONTO), `direcao` | 200 `PontoPedidoAjustePagina` | 400 | `podeDecidir` sempre false; sem filtro: mais recente primeiro |
| 5 | POST `/api/eu/ponto/pedidos-de-ajuste` | criarPedidoDeAjuste | F,G,RH | `PontoPedidoAjusteCriar` | 201 `PontoPedidoAjuste` | 400; 409 `PONTO_PEDIDO_JA_EM_ANALISE` (inclusive quando a chave `CHAVE_PENDENTE` do banco recusa, nunca 500); 422 `PONTO_PEDIDO_DIA_NAO_PASSOU`, `PONTO_INSTANTE_NO_FUTURO`, `PONTO_INSTANTE_FORA_DO_DIA`, `PONTO_SEQUENCIA_INVALIDA`, `FUNCIONARIO_INATIVO` (+`regraDoBanco`); 422 sem código ("anterior a admissao") | cria o dia se faltar; etapa decidida na criação; nada é aplicado às batidas; **sem e-mail**; x-provisorio prazo |
| 6 | GET `/api/eu/ponto/pedidos-de-ajuste/{pedidoId}` | obterMeuPedidoDeAjuste | F,G,RH | — | 200 `PontoPedidoAjuste` | 404 (de outra pessoa também) | `podeDecidir` false |
| 7 | GET `/api/eu/ponto/banco-de-horas` | obterMeuBancoDeHoras | F,G,RH | paginação, `de`, `ate` (date, inclusivos, sobre `diaReferencia`), `tipo` (CREDITO\|DEBITO) | 200 `BancoHorasExtrato` | 400 | ordem fixa `diaReferencia` desc; `saldoMinutos` de TODAS as linhas; x-provisorio hora extra/tolerância |
| 8 | GET `/api/eu/ponto/atestados` | listarMeusAtestados | F,G,RH | paginação, `situacao` | 200 `AtestadoPagina` | 400 | mais recente primeiro |
| 9 | POST `/api/eu/ponto/atestados` | enviarAtestado | F,G,RH | multipart `AtestadoEnviar` | 201 `Atestado` | 400 (`errors`: fim antes do início, horas sem par, horas com mais de um dia, tipo/tamanho do arquivo) | PDF/JPG/PNG ≤ 5 MB; arquivo só é guardado depois de a regra do banco aprovar; sobras por job; nasce `ENVIADO`; sem CID; **sem 422 nem 409 no contrato** |
| 10 | GET `/api/gestor/ponto/equipe` | obterPontoDaEquipe | G | `data` (query, date, opcional, não futura) | 200 `PontoEquipeDia` | 400, 403 | equipe = ativos do setor, sem o gestor (= `GET /api/gestor/equipe`); sem paginação, por nome; dia passado: `situacaoHoje` e `contagensHoje` `null`; sem "baixar espelho do setor" |
| 11 | GET `/api/gestor/equipe/{funcionarioId}/ponto` | obterEspelhoDePontoDaPessoaDaEquipe | G | `ano`, `mes` | 200 `PontoEspelho` | 400, 403, 404 (fora da equipe) | só leitura |
| 12 | GET `/api/gestor/equipe/{funcionarioId}/ponto/espelho/download` | baixarEspelhoDePontoPdfDaEquipe | G | `ano`, `mes` | 200 `LinkTemporario` | 400, 403, 404 | PDF com fonte que acentua, por link temporário, como anexo |
| 13 | GET `/api/gestor/ponto/pedidos-de-ajuste` | listarPedidosDeAjusteDaEquipe | G | paginação, `status`, `etapa`, `funcionarioId`, `ordenarPor`, `direcao` | 200 `PontoPedidoAjusteFilaPagina` | 400, 403 | só a equipe (pedido do gestor, de outro gestor e de outro setor não aparecem); `resumo` conta etapa GESTOR e RH da equipe |
| 14 | GET `/api/gestor/ponto/pedidos-de-ajuste/{pedidoId}` | obterPedidoDeAjusteDaEquipe | G | — | 200 `PontoPedidoAjuste` | 403, 404 (outra equipe, outro gestor, o próprio) | — |
| 15 | POST `.../gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | aprovarPedidoDeAjusteComoGestor | G | `PontoPedidoDecisao` (corpo obrigatório; `observacao` opcional ≤500) | 200 `PontoPedidoAjuste` | 400, 403, 404; 409 `PEDIDO_JA_DECIDIDO`, `PEDIDO_ETAPA_INCORRETA` | grava `decisaoGestor` APROVADA; segue `PENDENTE`, etapa RH; nenhuma batida muda; **e-mail à pessoa** pela fila |
| 16 | POST `.../gestor/.../{pedidoId}/recusar` | recusarPedidoDeAjusteComoGestor | G | idem | 200 | 400, 403, 404; 409 idem | `REJEITADA`, `CONCLUIDA`, `decisaoRh: null`; **e-mail** |
| 17 | GET `/api/gestor/ponto/atestados` | listarAtestadosDaEquipe | G | paginação, `situacao`, `funcionarioId` | 200 `AtestadoPagina` | 400, 403 | pessoa de outra equipe = lista vazia; gestor não valida |
| 18 | GET `/api/gestor/ponto/atestados/{atestadoId}/download` | baixarAtestadoDaEquipe | G | — | 200 `LinkTemporario` | 403, 404 | **auditoria a cada link** (área `ATESTADOS`, ação `BAIXOU`, IP, origem) |
| 19 | GET `/api/rh/funcionarios/{funcionarioId}/ponto` | obterEspelhoDePontoDeFuncionario | RH | `ano`, `mes` | 200 `PontoEspelho` | 400, 403, 404 | sem botão "Lançar no banco de horas" |
| 20 | GET `/api/rh/funcionarios/{funcionarioId}/ponto/espelho/download` | baixarEspelhoDePontoPdf | RH | `ano`, `mes` | 200 `LinkTemporario` | 400, 403, 404 | PDF por link |
| 21 | PUT `/api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}` | corrigirMarcacoesDoDia | RH | `PontoDiaCorrigir` | 200 `PontoEspelhoDia` | 400, 403, 404; 409 `PONTO_DIA_COM_PEDIDO_EM_ANALISE`, `PONTO_BATIDA_DUPLICADA`, `PONTO_MARCACAO_EM_USO`; 422 `PONTO_SEQUENCIA_INVALIDA`, `PONTO_INSTANTE_FORA_DO_DIA`, `PONTO_INSTANTE_NO_FUTURO`, `FUNCIONARIO_INATIVO` (+`regraDoBanco`), sem código ("anterior a admissao") | substitui a lista inteira: com `id` e mudou → `CORRIGIDA_RH`; sem `id` → nova `CORRIGIDA_RH`; ausente → apagada; `DATA_CORRECAO` junto; recalcula o banco do dia; auditoria (batida antiga no log); **sem segunda etapa e sem e-mail** |
| 22 | GET `/api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas` | obterBancoDeHorasDeFuncionario | RH | como #7 | 200 `BancoHorasExtrato` | 400, 403, 404 | sem tela no Figma ("escolha minha") |
| 23 | GET `/api/rh/ponto/pedidos-de-ajuste` | listarPedidosDeAjusteParaRh | RH | paginação, `status`, `etapa`, `funcionarioId`, `ordenarPor`, `direcao` | 200 `PontoPedidoAjusteFilaPagina` | 400, 403 | toda a empresa; `resumo.comRh` = fila do RH (= bolinha do menu e painel da f7) |
| 24 | GET `/api/rh/ponto/pedidos-de-ajuste/{pedidoId}` | obterPedidoDeAjusteParaRh | RH | — | 200 | 403, 404 | `podeDecidir` por quem consulta |
| 25 | POST `.../rh/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | aprovarPedidoDeAjusteComoRh | RH | `PontoPedidoDecisao` | 200 | 400, 403, 404; 409 `PEDIDO_ETAPA_INCORRETA`, `PEDIDO_JA_DECIDIDO`, `PONTO_BATIDA_DUPLICADA`; 422 `PEDIDO_RH_FORA_DE_GESTORES` (+`regraDoBanco` "Informe o gestor responsavel pela analise."), `PEDIDO_PROPRIO` (+`regraDoBanco` "O gestor nao pode analisar a propria solicitacao."; vence quando os dois valem), `PONTO_SEQUENCIA_INVALIDA` | **tudo ou nada**: aplica todos os itens (`CORRIGIDA_RH` + `DATA_CORRECAO`), `APROVADA`/`CONCLUIDA`, `decisaoRh` (observação substitui a do gestor), recalcula o banco do dia, auditoria, **e-mail** |
| 26 | POST `.../rh/.../{pedidoId}/recusar` | recusarPedidoDeAjusteComoRh | RH | idem | 200 | 400, 403, 404; 409 (`PontoErro409Decisao`); 422 (`PontoErro422DecisaoRh`: os dois códigos acima) | `REJEITADA`; RH pode recusar o que o gestor aprovou; **e-mail** |
| 27 | GET `/api/rh/ponto/feriados` | listarFeriados | RH | `ano` (obrig., 2000-2100), `mes` (opc.), `incluirInativos` (bool, false) | 200 `FeriadoLista` (sem paginação, por data) | 400, 403 | nacionais + estaduais da UF + municipais do município + `EMPRESA`; sem empresa configurada **não dá erro** (só nacionais e `EMPRESA`) |
| 28 | POST `/api/rh/ponto/feriados` | criarFeriado | RH | `FeriadoCriar` | 201 `Feriado` | 400, 403; 409 `FERIADO_DUPLICADO`; 422 `EMPRESA_NAO_CONFIGURADA` | sempre `MUNICIPAL`, UF/município da empresa; "repetir" → agendador cria o do ano seguinte |
| 29 | PUT `/api/rh/ponto/feriados/{feriadoId}` | editarFeriado | RH | `FeriadoEditar` | 200 `Feriado` | 400, 403, 404; 409 (ref. genérica `Erro409Conflito`, código pela descrição = `FERIADO_DUPLICADO`); 422 (ref. genérica; código `FERIADO_NAO_EDITAVEL`, sem exemplo de texto) | só `MUNICIPAL`; `ativo: false` desativa |
| 30 | GET `/api/rh/ponto/folha/exportacao` | exportarPontoParaFolha | RH | `ano`, `mes` (obrig.) | 200 `text/csv` | 400, 403 | UTF-8 com BOM, `;`, `attachment; filename="ponto-AAAA-MM.csv"`, uma linha por pessoa ativa no mês; x-provisorio formato |
| 31 | GET `/api/rh/ponto/atestados` | listarAtestados | RH | paginação, `busca` (nome), `situacao`, `funcionarioId` | 200 `AtestadoPagina` | 400, 403 | fila = `situacao=ENVIADO` |
| 32 | GET `/api/rh/ponto/atestados/{atestadoId}/download` | baixarAtestado | RH | — | 200 `LinkTemporario` | 403, 404 | auditoria `ATESTADOS`/`BAIXOU` a cada link |
| 33 | POST `/api/rh/ponto/atestados/{atestadoId}/validar` | validarAtestado | RH | sem corpo | 200 `Atestado` | 403, 404; 409 `ATESTADO_JA_DECIDIDO` | `ENVIADO`→`VALIDADO`, quem/quando; recalcula os dias do atestado; RH pode validar o próprio; **sem e-mail** |
| 34 | POST `/api/rh/ponto/atestados/{atestadoId}/recusar` | recusarAtestado | RH | `{motivo}` 1-500 | 200 `Atestado` | 400, 403, 404; 409 `ATESTADO_JA_DECIDIDO` | `RECUSADO` com motivo; dia segue falta |

**Textos exatos dos erros com código** (copiar, como `MensagensDoApoio` faz na 1c; o `TratadorGlobalDeErros` dá título fixo "Operação recusada."/"Conflito." às exceções simples, então estes precisam de `ProblemaDeApiException`):

| codigo (status) | title | detail (variações por rota) |
|---|---|---|
| PONTO_SEQUENCIA_MUDOU (409) | A sequência do ponto mudou. | O seu ponto mudou desde a última vez que você abriu a tela. Confira e tente de novo. |
| PONTO_BATIDA_DUPLICADA (409) | Batida duplicada. | #2: "O seu ponto já foi registrado agora há pouco." · #21: "Há duas batidas no mesmo horário neste dia. Corrija os horários." · #25: "Já existe uma batida neste horário. Nada foi alterado." |
| FUNCIONARIO_INATIVO (422) | Colaborador inativo. | #2: "Este colaborador está inativo e não pode registrar ponto." · #5: "...e não pode pedir ajuste de ponto." · #21: "Este colaborador está inativo e o ponto dele não pode mais ser alterado." Sempre `regraDoBanco: Nao e possivel registrar ponto de funcionario inativo.` |
| (sem código, 422) | Operação recusada. | A operação foi recusada por uma regra do banco de dados. (`regraDoBanco` = mensagem do trigger) |
| PONTO_PEDIDO_JA_EM_ANALISE (409) | Já existe um pedido para este dia. | Este dia já tem um pedido de ajuste em análise. Espere a resposta antes de pedir outro. |
| PONTO_SEQUENCIA_INVALIDA (422) | Batidas inconsistentes. | #5/#21: "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários." · #25: "Com as batidas do pedido o dia fica com uma entrada sem saída. Peça à pessoa um novo pedido." |
| PONTO_PEDIDO_DIA_NAO_PASSOU (422) | O dia ainda não passou. | Só dá para pedir ajuste de um dia que já passou. Para hoje, use o cartão "Seu ponto hoje". |
| PONTO_INSTANTE_NO_FUTURO (422) | Horário no futuro. | Uma das batidas pedidas tem horário que ainda não chegou. Confira os horários. |
| PONTO_INSTANTE_FORA_DO_DIA (422) | Horário fora do dia. | Cada batida precisa cair no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite). |
| PONTO_DIA_COM_PEDIDO_EM_ANALISE (409) | O dia tem um pedido em análise. | Responda o pedido de ajuste deste dia antes de editar as batidas. |
| PONTO_MARCACAO_EM_USO (409) | Batida em uso. | Esta batida está ligada a um pedido de ajuste e não pode ser removida. |
| PEDIDO_JA_DECIDIDO (409) | Pedido já decidido. | Este pedido já foi respondido. Atualize a lista. |
| PEDIDO_ETAPA_INCORRETA (409) | Pedido fora da vez. | genérico (#15, #16, #26): "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista." · #25: "Este pedido ainda está na etapa do gestor. Espere a análise dele." |
| PEDIDO_RH_FORA_DE_GESTORES (422) | Quem decide precisa ser gestor também. | Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor. (+`regraDoBanco`) |
| PEDIDO_PROPRIO (422) | Você não pode decidir o próprio pedido. | A operação foi recusada por uma regra do banco de dados. (+`regraDoBanco`) |
| FERIADO_DUPLICADO (409) | Feriado repetido. | Já existe um feriado com este nome nesta data. |
| EMPRESA_NAO_CONFIGURADA (422, #28) | Empresa não configurada. | Cadastre a UF e o município da empresa antes de criar feriados municipais. |
| ATESTADO_JA_DECIDIDO (409) | Atestado já decidido. | Este atestado já foi respondido. Atualize a lista. |
| FERIADO_NAO_EDITAVEL (422) | — sem exemplo — | — sem exemplo — (o executor escolhe e registra) |

---

## B. Schemas que importam

- **Convenção de tempo** (`CONVENCOES.md:37-38`): instante = `date-time` UTC com `Z`; dia = `date` no fuso -03:00; horário = `HH:mm` (`^([01][0-9]|2[0-3]):[0-5][0-9]$`); duração = minutos inteiros (`...Minutos`).
- **PontoMarcacao** (todos obrig.): `id`, `ordem` (≥1, por horário), `tipo` (`ENTRADA|SAIDA`), `rotulo` (ordem1 ENTRADA "Entrada", 2 SAIDA "Saída almoço", 3 ENTRADA "Volta", 4 SAIDA "Saída"; 5ª em diante ou tipo que não combina: "Entrada"/"Saída"), `instanteUtc`, `origem` (`SISTEMA|CORRIGIDA_RH`), `corrigidaEmUtc` (null se `SISTEMA`; vem da coluna `DATA_CORRECAO`, não do log).
- **PontoPeriodo** `{entrada, saida}` (cada um `PontoMarcacao|null`): ENTRADA abre, SAIDA seguinte fecha; ENTRADA seguida de ENTRADA deixa a primeira sem saída; SAIDA sem entrada = período com `entrada: null`.
- **PontoProximaMarcacao** `{tipo, rotulo, textoDoBotao}`; textos: "Registrar entrada", "Registrar saída para almoço", "Registrar volta", "Registrar saída".
- **PontoJornada** (campos obrig., anuláveis): `cargaSemanalMinutos` (≥1), `cargaDiariaMinutos` (= semanal ÷ 5), `horarioEntrada`, `horarioSaida`, `toleranciaAtrasoMinutos` (0-120). Tudo `null` sem carga no cargo / sem empresa; sem horário não há atraso.
- **PontoBancoHoras** `{saldoMinutos, atualizadoAte (date|null)}`. `atualizadoAte` é **global** (último dia consolidado, da `TAREFAS_AGENDADAS`); `null` só se nunca rodou.
- **PontoHoje** (obrig.): `dataDoPonto`, `agoraUtc`, `jornada`, `periodos`, `proximaMarcacao`, `bancoHoras`. **PontoMarcacaoRegistrar** `{tipoEsperado}` obrig.
- **PontoDiaSituacao** = `NORMAL, ATRASO, INCOMPLETO, AJUSTE_EM_ANALISE, EM_ANDAMENTO, SEM_REGISTRO, FOLGA, FERIADO, FERIAS, FALTA_COMPENSADA`. Precedência (`yaml:3005-3013`): (1) pedido `PENDENTE` do dia → `AJUSTE_EM_ANALISE` (mesmo sem batida); (2) com batidas: hoje `EM_ANDAMENTO`; passado com sequência quebrada `INCOMPLETO`; com atraso `ATRASO`; senão `NORMAL`; (3) sem batida: `FOLGA` (sáb/dom), `FERIADO`, `FERIAS` (férias `APROVADA|EM_GOZO|CONCLUIDA`), `FALTA_COMPENSADA` (atestado `VALIDADO` de dia inteiro), senão `SEM_REGISTRO` (inclusive hoje, a qualquer hora). Dia com 2 batidas fechadas = `NORMAL` (x-provisorio).
- **PontoEspelhoDia** (todos obrig.): `data`, `diaDaSemana` (`SEGUNDA..DOMINGO`), `ehHoje`, `situacao`, `nomeFeriado` (preenchido mesmo com batida), `periodos`, `horasMinutos` (soma dos períodos fechados, segundos descartados; `null` hoje, `INCOMPLETO` e dia sem expediente sem batida; `0` em `SEM_REGISTRO` passado), `cargaMinutos` (carga do dia; `0` em folga, feriado, férias, falta compensada e cargo sem carga; menos as horas de atestado de horas validado), `saldoMinutos` (`horas - carga`, `null` se horas `null`), `atrasoMinutos` (≥1; primeira ENTRADA − horário padrão, **só se passa da tolerância, conta inteiro**: 08:14 → 14; `null` sem atraso/batida/expediente/horário; vem mesmo com outra situação), `pedidoEmAnaliseId`. **Próprio** acrescenta `podePedirAjuste` (dia passado, sem pedido em análise, com expediente ou com batidas).
- **PontoResumoMes**: `horasTrabalhadasNoMesMinutos` (soma dos não-null), `saldoBancoHorasMinutos` (saldo total, não do mês), `pedidosEmAnalise` (PENDENTE de qualquer mês), `atrasos` (dias com atraso), `faltas` (dias passados `SEM_REGISTRO`, sem hoje). **PontoEspelho** = `{funcionario, ano, mes, jornada, resumo, dias}`; **PontoEspelhoProprio** sem `funcionario`.
- **PontoDiaCorrigir** `{marcacoes: [{id?: int|null, tipo, instanteUtc}]}` com `maxItems: 20`.
- **PontoPedidoAjusteCriar**: `data` (date), `justificativa` (5-500), `itens` 1-10 de `{tipo, instanteUtc, marcacaoId?}`.
- **PontoPedidoAjuste** (todos obrig.): `id, funcionario (FuncionarioResumo), data, criadoEmUtc ("o banco preenche"), justificativa, status (PENDENTE|APROVADA|REJEITADA), etapa (GESTOR|RH|CONCLUIDA), gestorResponsavel (FuncionarioResumo|null; id = funcionarioId), decisaoGestor, decisaoRh (DecisaoDeEtapa|null), itens [{id, marcacaoId|null, tipo, instanteUtc}], resumoDaCorrecao, periodosDoDia, podeDecidir, motivoNaoPodeDecidir`.
  - `resumoDaCorrecao` (horário em -03:00): "Incluir a entrada às HH:mm" / "Incluir a saída às HH:mm"; entrada+saída novas: "Incluir o período HH:mm - HH:mm"; existente: "Corrigir a entrada para HH:mm" / "Corrigir a saída para HH:mm"; várias partes com "; ".
  - **Cinco textos fixos** de `motivoNaoPodeDecidir` (`yaml:3240-3248`): "Aguardando a análise do gestor." · "Aguardando a análise do RH." · "Este pedido já foi decidido." · "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH." · "Você não pode decidir o seu próprio pedido." (este vence o anterior).
- **DecisaoDeEtapa** (base): `decisao` (`APROVADA|REJEITADA`), `decididaPor` (null se automática), `decididaEmUtc`, `automatica`, `observacao` (≤500). **EtapaDoPedido** `GESTOR|RH|CONCLUIDA`. **PontoPedidoDecisao** `{observacao?: string|null ≤500}`. **PontoPedidoFilaResumo** `{comGestor, comRh}` (independe da página e dos filtros, respeita o escopo).
- **PontoEquipeDia** `{data, resumo, itens}`; **PontoEquipeResumo** `{contagensHoje (PresencaContagens|null), diaUtil, pessoasSemRegistro [FuncionarioResumo], pessoasDeFerias [{funcionario, ate}], pedidosParaAnalisar}`; **PontoEquipeItem** `{funcionario, periodos, horasAteAgoraMinutos (int|null), saldoBancoHorasMinutos, situacaoHoje (SituacaoHoje|null), situacaoDoDia, periodoAberto}`.
- **SituacaoHoje** (base) `SEM_EXPEDIENTE, FERIAS, ATESTADO, TRABALHANDO, SEM_REGISTRO, AINDA_NAO_COMECOU`; precedência `CONVENCOES.md:97` (batida vence; atestado só validado de dia inteiro; `SEM_REGISTRO` só **depois** de entrada + tolerância).
- **BancoHorasLancamento** `{id, diaReferencia, tipo (CREDITO|DEBITO), quantidadeMinutos (≥1), descricao}`; descrições: "Hora extra", "Saldo negativo do dia", "Falta". **BancoHorasExtrato** = Paginação + `saldoMinutos`, `atualizadoAte`, `itens`.
- **Feriado** (todos obrig.): `id, nome, data, tipo (NACIONAL|ESTADUAL|MUNICIPAL|EMPRESA), uf, municipio, repeteTodoAno, ativo, podeEditar` (true só `MUNICIPAL`). **FeriadoCriar** `{nome 1-100, data, repeteTodoAno=false}` (29/02 não se repete); **FeriadoEditar** `{nome, data, repeteTodoAno, ativo}` todos obrig.
- **Atestado** (todos obrig.): `id, funcionario, dataInicio, dataFim, horaInicio|null, horaFim|null, situacao (ENVIADO|VALIDADO|RECUSADO), nomeArquivo|null, enviadoEmUtc, decididoEmUtc|null, decididoPorNome|null, motivoRecusa|null`. **AtestadoEnviar** obrig. `dataInicio, dataFim, arquivo`; `horaInicio/horaFim` juntas, só com um dia, fim > início. **AtestadoRecusar** `{motivo 1-500}`.
- **Exemplos que ensinam regra**: 07/10 da tela 33 com `FALTA_COMPENSADA` e `cargaMinutos: 0`; 02/10 `AJUSTE_EM_ANALISE` com volta sem saída, `horasMinutos: null`; pedido 297 (gestor ausente: `decisaoGestor.automatica: true`, `decididaPor: null`, `decididaEmUtc` = criação, `gestorResponsavel` = Paulo); pedido 306 visto por Sofia (`podeDecidir: true`) e por Roberto (texto 4); 302 com `periodosDoDia` começando por `entrada: null`; CSV de exemplo com `matricula` "00482".

---

## C. O banco

### C.1 Tabelas

| Tabela (`V001`) | Colunas | Restrições |
|---|---|---|
| `PONTOS` (254-270) | `ID_PONTO`, `ID_FUNCIONARIO` NN, `DATA` DATE NN, `HORAS_TRABALHADAS` DEC(5,2), `CARGA_HORARIA` DEC(5,2) (horas/dia) | UQ `(ID_FUNCIONARIO, DATA)`; CHECK horas 0-24; CHECK carga > 0 ou NULL; índices `(DATA)` e `(ID_FUNCIONARIO, DATA)` |
| `PONTOS_MARCACOES` (278-300) | `ID_MARCACAO`, `ID_PONTO` NN, `DATA_HORA_MARCACAO` DATETIME NN (sem fração, -03:00), `TIPO_MARCACAO` NN, `ORIGEM` NN default `'SISTEMA'`, `DATA_CORRECAO` DATETIME NULL | UQ `(ID_MARCACAO, ID_PONTO)` (alvo de FK composta); **UQ `(ID_PONTO, DATA_HORA_MARCACAO)`** (duplo clique); CHECK tipo; CHECK origem; **CHECK `(ORIGEM='CORRIGIDA_RH') = (DATA_CORRECAO IS NOT NULL)`**; **sem trigger** |
| `PONTOS_MANUTENCOES` (306-369) | `ID_MANUTENCAO_PONTO`, `ID_PONTO` NN, `ID_MARCACAO` (obsoleta, "pode ficar vazia"), `ID_FUNCIONARIO` NN, `JUSTIFICATIVA` TEXT NN, `DATA_SOLICITACAO`, `STATUS_SOLICITACAO` NN default PENDENTE, `DATA_ANALISE`, `ID_GESTOR` (FK GESTORES), `OBSERVACAO` TEXT (**uma só**), `RESULTADO_GESTOR`, `DATA_ETAPA_GESTOR`, `ETAPA_GESTOR_AUTOMATICA` NN default FALSE, `ID_FUNCIONARIO_RH`, `DATA_CONFIRMACAO_RH`, `CHAVE_PENDENTE` **gerada** `IF(STATUS='PENDENTE', ID_PONTO, NULL) STORED` | CHECK status `PENDENTE|APROVADA|REJEITADA`; CHECK `DATA_ANALISE >= DATA_SOLICITACAO`; UQ `(ID_MANUTENCAO_PONTO, ID_PONTO)`; CHECK resultado `APROVOU|RECUSOU`; CHECK resultado⇔data da etapa; CHECK resultado exige `ID_GESTOR`; CHECK automática ⇒ `APROVOU`; CHECK `RECUSOU` ⇒ `REJEITADA`; CHECK `ID_FUNCIONARIO_RH`⇔`DATA_CONFIRMACAO_RH`; CHECK RH só se gestor não recusou; **UQ `(CHAVE_PENDENTE)`** (um pendente por dia); índice `(STATUS, DATA_SOLICITACAO)`; FK composta `(ID_MARCACAO, ID_PONTO)` |
| `PONTOS_MANUTENCOES_ITENS` (945-963) | `ID_MANUTENCAO_PONTO_ITEM`, `ID_MANUTENCAO_PONTO` NN, `ID_PONTO` NN, `ID_MARCACAO` NULL, `DATA_HORA_PROPOSTA` DATETIME NN, `TIPO_MARCACAO` NN | FK `(pedido, ID_PONTO)` → mesmo dia do pedido; FK `(ID_MARCACAO, ID_PONTO)` → batida do mesmo dia; **UQ `(pedido, DATA_HORA_PROPOSTA)`**; CHECK tipo; sem trigger |
| `BANCOS_HORAS` (437-455) | `ID_BANCO_HORAS`, `ID_FUNCIONARIO` NN, `DATA_LANCAMENTO` DATE, `TIPO_LANCAMENTO`, `QUANTIDADE` DEC(8,2) **em horas**, `OBSERVACAO` TEXT, `DIA_REFERENCIA` DATE **NULL** | CHECK quantidade ≥ 0; CHECK tipo `CREDITO|DEBITO`; UQ `(ID_FUNCIONARIO, DIA_REFERENCIA)` (não protege NULL) |
| `FERIADOS` (863-882) | `ID_FERIADO`, `NOME_FERIADO` NN (100), `DATA_FERIADO` NN, `TIPO_FERIADO` NN, `UF` CHAR(2), `MUNICIPIO`, `ATIVO` NN default TRUE, `DESCRICAO`, `REPETE_TODO_ANO` NN default FALSE | **UQ `(DATA_FERIADO, NOME_FERIADO)`**; CHECK tipo; CHECK abrangência (NACIONAL/EMPRESA sem UF e município; ESTADUAL com UF sem município; MUNICIPAL com os dois). Carga anual: 10 nacionais de 2026 **sem acento** (906-916), sem Carnaval e Corpus Christi |
| `ATESTADOS` (1108-1144) | `ID_ATESTADO`, `ID_FUNCIONARIO` NN, `DATA_INICIO`/`DATA_FIM` NN, `HORA_INICIO`/`HORA_FIM` TIME NULL, `CAMINHO_ARQUIVO` NN, `NOME_ARQUIVO_ORIGINAL`, `STATUS_ATESTADO` NN default ENVIADO, `ID_FUNCIONARIO_RH`, `DATA_DECISAO`, `MOTIVO_RECUSA`, `DATA_ENVIO` NN **default CURRENT_TIMESTAMP** (fuso do servidor) | CHECK fim ≥ início; horas juntas; horas ⇒ mesmo dia e fim > início; status; ENVIADO sem decisão / outros com quem e quando; RECUSADO com motivo não vazio; índices `(STATUS, DATA_ENVIO)` e `(STATUS, DATA_FIM, DATA_INICIO)`; **sem trigger** |
| `CONFIGURACOES_EMPRESA` (923-938) | linha única (`ID=1`), `NOME_EMPRESA`, `UF`, `MUNICIPIO`, `HORARIO_ENTRADA`/`HORARIO_SAIDA` TIME, `TOLERANCIA_ATRASO_MINUTOS` (0-120), `DIA_PAGAMENTO` | nasce **vazia**; sem CHECK saída > entrada |
| `GESTORES` (112-127) | `ID_GESTOR`, `ID_FUNCIONARIO` (UQ), `APROVACAO_AUTOMATICA`, `APROVACAO_AUTOMATICA_ATE` | CHECK data só com a marca; vale até o dia **anterior** à data |
| `FERIAS` (754-848), só leitura na 3 | `ID_FUNCIONARIO`, `DATA_INICIO`, `DATA_FIM`, `STATUS_FERIAS` (`SOLICITADA, APROVADA, REJEITADA, EM_GOZO, CONCLUIDA, CANCELADA`) | índice `(STATUS, DATA_FIM, DATA_INICIO)` |
| `CARGOS.CARGA_HORARIA` DEC(5,2) | **semanal** (44) | — |
| `FUNCIONARIOS` (56-88) | `DATA_ADMISSAO` NN, `DATA_DESLIGAMENTO`, `ATIVO`, `MATRICULA` (5 dígitos, NULL possível), `EMAIL_CORPORATIVO` (NULL possível) | **CHECK `ATIVO = FALSE OR DATA_DESLIGAMENTO IS NULL`** |
| Tabelas da aplicação (UTC): `TAREFAS_AGENDADAS` (1466-1494; chave `TIPO:...` única; exemplos `CONSOLIDAR_BANCO_HORAS:dia=2026-10-06`, `REPETIR_FERIADO`), `FILA_EMAILS` (1412-1455; tipo `^[A-Z][A-Z0-9_]{2,39}$`, destinatário `%_@_%`), `AUDITORIA_EVENTOS` (1507-1551; áreas `PONTO`, `FERIADOS`, `ATESTADOS`; ações `REGISTROU`, `PEDIU_AJUSTE`, `APROVOU`, `RECUSOU`, `BAIXOU`...), `DESLIGAMENTOS_AGENDADOS` (1377-1405) | | |

### C.2 Triggers (`V002`)

| Trigger | Regra |
|---|---|
| `TRG_PONTOS_BEFORE_INSERT` (431-468) | Recusa "Nao e possivel registrar ponto de funcionario inativo." / "A data do ponto e anterior a admissao." / "A data do ponto e posterior ao desligamento."; se `CARGA_HORARIA` vem NULL, grava `ROUND(cargo/5, 2)` (NULL se o cargo não tem carga) |
| `TRG_PONTOS_BEFORE_UPDATE` (470-507) | Igual, com "Nao e possivel **alterar** ponto de funcionario inativo." (qualquer UPDATE em `PONTOS` de inativo) |
| `TRG_PONTOS_MANUTENCOES_BEFORE_INSERT` (513-533) | Recusa "O ponto informado pertence a outro funcionario."; força `STATUS='PENDENTE'`; `DATA_SOLICITACAO = NOW()` se NULL. **Não confere funcionário ativo** |
| `TRG_PONTOS_MANUTENCOES_BEFORE_UPDATE` (535-567) | Status ≠ PENDENTE é final ("Solicitacao analisada nao pode mudar de status."); PENDENTE→APROVADA/REJEITADA exige `ID_GESTOR` ("Informe o gestor responsavel pela analise.") e recusa `GESTORES.ID_FUNCIONARIO = ID_FUNCIONARIO` do pedido ("O gestor nao pode analisar a propria solicitacao."); `DATA_ANALISE = NOW()` se NULL. **Não olha `ID_FUNCIONARIO_RH`** |
| `TRG_BANCOS_HORAS_BEFORE_INSERT` (687-713) | Recusa "Nao e possivel lancar banco de horas para funcionario inativo.", "Informe o tipo e a quantidade do lancamento.", "A data do lancamento nao pode ser futura." (vs `CURDATE()`); `DATA_LANCAMENTO = CURDATE()` se NULL. **Só INSERT** (UPDATE e DELETE livres) |
| `TRG_FERIADOS_BEFORE_INSERT/UPDATE` (998-1044) | `UPPER(UF)`; NACIONAL/EMPRESA zeram UF e município; "Feriado estadual exige UF."; "Feriado municipal exige UF e municipio." |
| `TRG_FUNCIONARIOS_BEFORE_INSERT/UPDATE` (57-115) | `DATA_DESLIGAMENTO` preenchida ⇒ `ATIVO = FALSE` (mesmo futura) |
| `TRG_FUNCIONARIOS_AFTER_UPDATE` (117-140) | Inativar desativa usuário e benefícios e cancela férias `SOLICITADA|APROVADA` com início > `CURDATE()` |
| `TRG_GESTORES_BEFORE_INSERT` (154-168) | Recusa gestor inativo |
| `TRG_FERIAS_*` (910-992) | Transições; decisão exige `ID_GESTOR` ≠ a pessoa (fatia 7) |
| `PONTOS_MARCACOES`, `PONTOS_MANUTENCOES_ITENS`, `ATESTADOS` | **sem trigger** |

### C.3 Funções (`V003`)

| Função | Regra | Usar? |
|---|---|---|
| `FN_VERIFICAR_CONSISTENCIA_PONTO(id_ponto)` (123-179) | Ordena por `(DATA_HORA, ID)`, exige alternância começando em ENTRADA e terminando em SAIDA; devolve `CONSISTENTE`, `PONTO INCONSISTENTE`, `SEM MARCACOES`, `PONTO INEXISTENTE` | Reimplementar em C# (o pedido e a correção validam o dia **depois de aplicar os itens**, antes de gravar); a função serve de **oráculo em teste** (é a definição de `INCOMPLETO` do contrato) |
| `FN_CALCULAR_HORAS_PONTO(id_ponto)` (183-226) | NULL se inconsistente; soma segundos e devolve `ROUND(s/3600, 2)` horas | **Reimplementar**: 2 casas de hora erram até ~0,3 min por dia; o contrato quer minutos "com os segundos descartados" |
| `FN_CALCULAR_HORAS_TRABALHADAS(func, ini, fim)` (230-273) | Soma a anterior nos dias consistentes | Não usar (mesmo erro de arredondamento) |
| `FN_CALCULAR_SALDO_BANCO_HORAS(func)` (29-47) | Soma horas com sinal | **Proibido pelo contrato** para `saldoMinutos` (`yaml:161-165`, `:868-872`; `LEIA-ME.md:92`): converter **cada linha** para minutos e depois somar |

"Uso das funções do grupo" é decisão aberta do dono (roadmap:176); o roadmap pede que a aplicação valide e calcule "apoiada nas funções" (roadmap:164).

### C.4 Como as duas etapas ficam nas colunas (INFERIDO, apoiado no `V001` 1864-1934 e em `IGestorAusente`)

Não há coluna de etapa. Com `PENDENTE`: `RESULTADO_GESTOR` NULL e `ID_GESTOR` preenchido = **GESTOR** (o gestor gravado na criação, o `GestorId` de `EtapaInicialDoPedido`); `RESULTADO_GESTOR` NULL e `ID_GESTOR` NULL = **RH sem etapa do gestor**; `RESULTADO_GESTOR='APROVOU'` = **RH** (automática se `ETAPA_GESTOR_AUTOMATICA`). Final: gestor recusou = `REJEITADA` + `RECUSOU`; RH decidiu = `ID_FUNCIONARIO_RH` + `DATA_CONFIRMACAO_RH`. No pedido sem etapa do gestor, a decisão final precisa gravar em `ID_GESTOR` a linha de `GESTORES` **de quem do RH decide** (o trigger exige), e o pedido continua sem `decisaoGestor` porque `RESULTADO_GESTOR` fica NULL. "Passar ao RH" = `ID_GESTOR = NULL` nos pendentes sem resultado daquele gestor. `decisaoGestor.decididaEmUtc` = `DATA_ETAPA_GESTOR`; `decisaoRh.decididaEmUtc` = `DATA_CONFIRMACAO_RH`; `DATA_ANALISE` = decisão final (trigger). Observação: a do RH sobrescreve a do gestor (`yaml:112-116`).

### C.5 Armadilhas

1. **Fuso e relógio.** Tabelas do grupo em -03:00, da aplicação em UTC (`LEIA-ME.md:80`); converter com `ConversoresDeTempo.Brasil`. Os triggers usam `NOW()`/`CURDATE()` **reais** do servidor, e os testes fixam o relógio em 2026-10-07: se `DATA_SOLICITACAO` vier do trigger e `DATA_ETAPA_GESTOR`/`DATA_CONFIRMACAO_RH` do `IRelogio`, o pedido nasce "depois" de ser decidido e `criadoEmUtc` sai em novembro. O `CHECK DATA_ANALISE >= DATA_SOLICITACAO` só passa se as duas vierem da mesma fonte. Mesmo problema em `ATESTADOS.DATA_ENVIO` (default `CURRENT_TIMESTAMP`). Ver F.3.
2. **Segundos.** `DATA_HORA_MARCACAO` é `DATETIME` sem fração; o MySQL **arredonda** a fração ao gravar (comportamento documentado; INFERIDO para este provedor, medir): dois cliques a 0,4 s e 0,6 s caem em segundos diferentes e **os dois entram**. Truncar ao segundo na entidade (o conversor não trunca: `Persistencia/ConversoresDeTempo.cs`).
3. **Corrida da sequência.** A UQ só pega o mesmo segundo; duas abas em segundos diferentes passam no `PONTO_SEQUENCIA_MUDOU` e gravam duas ENTRADAs. Precisa de trava por pessoa (padrão `OperacoesComTrava`: `FOR UPDATE` em `FUNCIONARIOS`, `READ COMMITTED`). Criar o dia também corre (UQ `(ID_FUNCIONARIO, DATA)` → 1062: reler).
4. **Inativo.** `PONTOS_MARCACOES` e `PONTOS_MANUTENCOES` não conferem atividade: se o dia já existe, inserir batida ou pedido de inativo **passa** no banco. O `FUNCIONARIO_INATIVO` do contrato precisa de checagem da aplicação (o token de acesso dura 15 min e não reconfere o usuário: `Infrastructure/Autenticacao/OpcoesJwt.cs`, sem `OnTokenValidated`).
5. **"Posterior ao desligamento" é inalcançável**: a data preenchida já inativa (`LEIA-ME.md:116`); vem sempre a mensagem de inativo.
6. **Último dia de quem sai.** `BANCOS_HORAS` recusa INSERT de inativo, mas aceita UPDATE/DELETE; recalcular depois da efetivação (ajuste aprovado tarde, atestado validado tarde) funciona se a linha existe e falha se precisa criar. `PONTOS` UPDATE de inativo é recusado. A efetivação já tem gancho: `ICondicaoParaEfetivarDesligamento` (`Application/Colaboradores/IDesligamentoDeFuncionarios.cs`).
7. **Batida em uso para sempre.** A FK `(ID_MARCACAO, ID_PONTO)` dos itens continua depois da decisão: batida que já foi alvo de qualquer pedido (inclusive recusado) **nunca** pode ser apagada → 1451 → `PONTO_MARCACAO_EM_USO`. A correção aprovada é UPDATE no lugar (o horário original só fica no log).
8. **FKs que viram 500.** `marcacaoId` de outro dia/pessoa (FK composta, erro 1452) e dois itens no mesmo instante (UQ do item, 1062) não têm código no contrato; o tratador global transforma 1452 em 500 e 1062 em 409 genérico. Validar antes.
9. **1062 ambíguo.** `ErrosDoBanco.EhChaveRepetida` não diz qual chave; o ponto tem seis UQs com significados distintos (`PONTO_BATIDA_DUPLICADA`, `PONTO_PEDIDO_JA_EM_ANALISE`, `FERIADO_DUPLICADO`, dia criado em paralelo, item repetido, banco de horas repetido). Distinguir pelo nome da chave na mensagem do MySQL (INFERIDO: a mensagem traz `TABELA.CHAVE`).
10. **Horas no banco de horas.** `QUANTIDADE` em horas com 2 casas: converter minuto→hora com `Math.Round(m/60, 2)` e hora→minuto com arredondamento ao inteiro é sem perda (erro máximo 0,3 min); truncar perde.
11. **Carga congelada.** `PONTOS.CARGA_HORARIA` é copiada na criação do dia ("a nova carga vale para os dias criados depois", STATUS.md:56); dia sem `PONTOS` (falta) não tem carga gravada e teria de usar a do cargo atual. `ROUND(x/5,2)` pode diferir em 1 min de "semanal em minutos ÷ 5" para cargas quebradas.
12. **`HORAS_TRABALHADAS` não é mais preenchida** por trigger (V002:394-400); nada a lê.
13. **Feriados e collation.** UQ `(DATA, NOME)` sob `utf8mb4_unicode_ci` é, pelo que sei, insensível a acento e maiúscula (INFERIDO, não medido): importar "Independência do Brasil" sobre "Independencia do Brasil" dá 1062 em vez de duplicar; nome diferente ("Dia de Tiradentes") duplica a data. Renomear um feriado que repete faz o agendador recriar o nome velho se copiar de registro antigo (`V001` 2744-2746).
14. **`CHAVE_PENDENTE` e `CHAVE_...`** são calculadas: mapear com `CalculadaNoBanco()` (`Persistencia/ColunaCalculadaNoBanco.cs`). `ORIGEM`, `ETAPA_GESTOR_AUTOMATICA`, `ATIVO`, `REPETE_TODO_ANO` têm padrão no banco: não declarar o padrão no EF (decisão 12a do roadmap). `DIA_REFERENCIA` aceita NULL: sempre preencher.
15. **Mapeamento × banco.** `MapeamentoContraBancoTests` exige nulabilidade igual à coluna e toda coluna NN sem padrão mapeada. Ler `FERIAS` por entidade obriga mapear `DATA_INICIO_PERIODO_AQUISITIVO` etc.; a fatia 7 será dona de `FERIAS` (ver E).
16. **Jornada após a meia-noite.** A batida das 01:00 pertence ao `PONTOS` do dia anterior se fecha uma ENTRADA aberta há < 12 h; a consolidação de "ontem" às 00:00 pegaria esse dia `INCOMPLETO` e não lançaria; quando a SAIDA chegar, alguém precisa recalcular.
17. **Retenção do agendador.** `LIMPAR_TAREFAS` apaga `CONCLUIDA` com mais de 30 dias (`Agendamento/Tarefas/LimparTarefasAntigasTarefa.cs`): `atualizadoAte` e o "o que falta consolidar" não podem depender só desse histórico depois de uma parada longa.
18. **Pedido de quem tem o mesmo RH como único em `GESTORES`**: ninguém decide (`V001` 1910-1912). No seed de teste (`ElencoDeTeste`) **ninguém do RH está em `GESTORES`**, não há cargo com carga nem empresa configurada.

---

## D. Regras de negócio que o código tem de cumprir

| Regra | Fonte |
|---|---|
| Horário da batida é o do servidor; o corpo só diz o tipo esperado | yaml:192-197; roadmap:166 |
| Sequência ENTRADA/SAIDA e cálculo de horas ficam com a aplicação | roadmap:164; yaml:528-529 |
| Batida duplicada: chave por segundo, resposta amigável, front desabilita | roadmap:166; yaml:198-200 |
| Jornada que atravessa a meia-noite: próxima é a SAIDA do dia anterior se a ENTRADA tem < 12 h (x-provisorio) | yaml:156-158, 210 |
| Carga do dia = semanal ÷ 5; horário padrão único da empresa; atraso contra ele com tolerância | roadmap:172, 40; V002:402-412 |
| Duas etapas (gestor, depois RH); status do banco só muda na decisão final; etapa fixada na criação e só avança | CONVENCOES:70-86; roadmap:52; yaml:85-90 |
| Sem etapa do gestor: pedido de quem é gestor, de setor sem gestor ativo, de gestor desligado; só o RH decide e precisa estar em `GESTORES` | CONVENCOES:78; yaml:59-65 |
| Gestor recusa → acaba; RH pode recusar o que o gestor aprovou | CONVENCOES:79 (dono, 2026-10-30) |
| Sem "RH decide direto" (botões só depois do gestor; divergência da tela 13) | roadmap:165, 176; yaml:1870-1872 |
| Gestor ausente: pedido novo nasce com etapa do gestor aprovada (`automatica`); ao ligar, aprova os parados; ao desligar, só os novos voltam; vale até o dia anterior à volta | CONVENCOES:80; yaml:67-78; f1 `configurarAusenciaDoGestor` |
| Setor fica sem gestor ativo / gestor trocado → pedidos parados passam ao RH | CONVENCOES:82; yaml:80-83; f1:1402-1404, :528 |
| `PEDIDO_PROPRIO` só onde o trigger recusa; RH confirma o próprio pedido se passou pelo gestor | CONVENCOES:81; yaml:99-107; LEIA-ME:167 |
| Nas rotas do gestor, pedido de gestor, do próprio e de outra equipe = 404 | CONVENCOES:85; yaml:95-96 |
| E-mail a cada decisão (gestor e RH); não na criação, não na correção do RH, não na aprovação automática; falha de envio não desfaz | CONVENCOES:114; roadmap:169; yaml:118-124 |
| Pedido: um dia por pedido, várias batidas; dia já passado; instante não futuro, no dia ou no seguinte; um pendente por dia (também no banco) | yaml:516-535; roadmap:41 |
| Aprovação do RH aplica todos os itens ou nenhum, com `CORRIGIDA_RH` + `DATA_CORRECAO` | yaml:2156-2176; roadmap:174 |
| Gestor só vê o espelho; só o RH altera marcações; a correção fica marcada | roadmap:170; yaml:1148 |
| Correção do RH sem segunda etapa e sem e-mail | yaml:1696-1697 (dono, 2026-10-29) |
| Atestado: gestor vê o arquivo só da equipe; só o RH valida/recusa (com motivo); acesso auditado; validado = "Falta Compensada", não desconta; reduz o esperado pelas horas cobertas | roadmap:168; yaml:129-139; V001:2526-2564 |
| Banco de horas só automático, só CREDITO/DEBITO, sem botão manual | roadmap:42, 161; yaml:126, 853-854 |
| Consolidação: idempotente (uma linha por pessoa e dia), recupera dias parados, recalcula após ajuste aprovado/correção/atestado, considera férias aprovadas e atestados validados, não lança `INCOMPLETO`/`AJUSTE_EM_ANALISE`, saldo zero não tem linha (some se zerar), roda o último dia antes de gravar o desligamento | roadmap:167, 115; yaml:855-866 |
| Saldo = soma dos minutos de cada linha | yaml:868-872; LEIA-ME:92 |
| Feriados: nacionais e estaduais (UF da empresa) entram sozinhos; municipais o RH cadastra, valem para todos (sem unidades); repetir todo ano pelo agendador | roadmap:171; yaml:2360-2369, 2398-2400 |
| Exportação para a folha: CSV, `;`, BOM, células que começam com `=`,`+`,`-`,`@` neutralizadas, nenhum número negativo | roadmap:78; CONVENCOES:48; yaml:2483-2496 |
| PDF do espelho: biblioteca de licença conferida, fontes com acento, Linux, pouca memória; por link temporário | roadmap:77, 178; CONVENCOES:59, 105 |
| Auditoria: batida (`PONTO`/`REGISTROU`, com IP), correção do RH (batida antiga no log), decisões, acesso ao atestado (`ATESTADOS`/`BAIXOU`); `ExecuteUpdate` só com evento explícito ou `AUDITORIA-OK` | roadmap:74; yaml:29-31, 204, 1713-1714; f1 `AuditoriaArea` |
| Privacidade: auditoria não grava o valor do arquivo de atestado | roadmap:82 |
| Mensagens de trigger traduzidas (texto genérico + `regraDoBanco`) | roadmap:79; CONVENCOES:53 |

---

## E. O que existe e o que falta

### E.1 Reaproveitar

| Peça | Onde | O que faz |
|---|---|---|
| `GrupoEu/GrupoGestor/GrupoRh` | Api/Autorizacao/FamiliasDeRota.cs | grupos com a política do perfil (403 automático) |
| `IEscopoDoUsuario` / `EquipeDoGestor` | Application/Escopo/IEscopoDoUsuario.cs; Infrastructure/Escopo/EscopoDoUsuario.cs | quem está logado; `ObterEquipeAsync` lê o setor do gestor no banco (null se não é gestor de setor) |
| `DaEquipe(equipe)` | Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs | definição única da equipe (ativos, admitidos até hoje, do setor, sem o gestor); filtrar ponto/atestado/pedido por `DaEquipe(e).Select(f=>f.Id).Contains(x.FuncionarioId)` + `OuNaoEncontrado()` |
| `IGestorAusente.EtapaInicialDoPedidoAsync` / `EstaAusenteHojeAsync` | Application/Gestores/IGestorAusente.cs; Infrastructure/Gestores/GestorAusente.cs | decide a etapa de nascimento, o `GestorId` e a aprovação por ausência |
| `IPedidosParadosNoGestorDaFatia` + composto | Application/Colaboradores/IPedidosParadosNoGestor.cs, PedidosParadosNoGestorComposto.cs, PedidosAprovados.cs | a 3 **implementa e registra** a da fatia (`AddScoped<IPedidosParadosNoGestorDaFatia, ...>`); a 1b/1c já chamam |
| `ICondicaoParaEfetivarDesligamento` | Application/Colaboradores/IDesligamentoDeFuncionarios.cs | a 3 registra "banco do último dia fechado" |
| `IFonteDeFatosDoDia`, `ISituacaoDeHoje`, `RegraDeSituacaoDeHoje`, `PresencaContagens`, `SituacaoHoje` | Application/Colaboradores/SituacaoDeHoje.cs; Infrastructure/Colaboradores/SituacaoDeHojeServico.cs | a 3 registra uma fonte que substitui `FonteDeFatosDoDiaSemRegistros` (registro posterior vence) |
| `IFonteDeIndicadorDaFicha` (`BancoDeHorasMinutos`) | Application/Colaboradores/IFonteDeIndicadorDaFicha.cs | a 3 registra a fonte do saldo |
| `IFonteDeContadorDoMenu` / `ContadorDoMenu` | Application/Painel/ | enum já tem `PedidosDeAjusteDePontoDoGestor`, `PedidosDeAjusteDePontoDoRh`, `PendenciasDaEquipe`; a 3 registra as fontes (COUNT) |
| `IConfiguracaoDaEmpresa` | Application/Empresa/IConfiguracaoDaEmpresa.cs | horário, tolerância, UF, município (`ObterAsync` null sem empresa) |
| `IAgendador`, `ITarefaAgendada` (`ExecutarAsync`, `RecuperarAsync`), `NovaTarefa`, `TarefaParaExecutar`, `ChaveDeIdempotencia.Montar` | Application/Agendamento/; Application/Comum/ChaveDeIdempotencia.cs; Infrastructure/Agendamento/ | agenda na transação do chamador, chave única = idempotência; executor com reserva, novas tentativas (1/5/30/60 min) e escopo Sistema; modelo: `EfetivarDesligamentoTarefa` |
| `IFilaDeEmails`, `EmailParaEnfileirar`, `IRenderizadorDeEmail`, `ModeloDeEmail` | Application/Emails/; Infrastructure/Emails/ | enfileirar na transação do fato; novo tipo = novo renderizador `AddSingleton<IRenderizadorDeEmail, ...>` (modelo: `RenderizadoresDeSenha.cs`, `HtmlEncode` em tudo) |
| `IArmazenamentoDeArquivos`, `CaminhosDeArquivo.Novo`, `IdentificadorDeTipoDeArquivo`, `NomeParaDownload`, `LinkTemporario`, `IFonteDeReferenciasDeArquivo`, `LimparArquivosOrfaosTarefa` | Application/Arquivos/; Infrastructure/Arquivos/ | blob com caminho GUID; tipo pelos bytes (PDF/JPEG/PNG); link de 5 min como anexo; limpeza de sobras por prefixo (>24 h) |
| `EnvioDeArquivoDeDocumento.GravarEEfetivarAsync(arquivo, pasta, tipo, efetivar)` e `ValidacaoDeArquivoDeDocumento` | Application/Documentos/ArquivoDeDocumento.cs | receita: blob fora de transação → transação curta → apaga o blob se falhar. **A validação tem 10 MiB e textos fixos**; o atestado (5 MB) precisa parametrizar |
| `LeituraDeFormulario`, `FormulariosDoContrato` | Api/Comum/LeituraDeFormulario.cs; Api/Configuracao/FormulariosDoContrato.cs | multipart lido de `HttpRequest` (415/400 certos); acrescentar `enviarAtestado` |
| `IAuditor` (`Registrar` na transação; `RegistrarEGravarAsync` fora, para download), `[Auditado]`/`[Auditar(Sensivel)]`, `AuditoriaInterceptor`, `GuardaDeExecuteUpdateTests` | Application/Auditoria/IAuditor.cs; Domain/Auditoria/; Infrastructure/Auditoria/ | modelo do download auditado: `DocumentosDoColaboradorCasoDeUso.BaixarAsync` |
| `ProblemaDeApiException`, `ErroDeNegocioException`, `ConflitoException`, `ErroDeValidacaoException`, `ColetorDeErros`, `NaoEncontradoException`/`OuNaoEncontrado` | Application/Comum/ | ver o padrão de tradução de trigger em `CriarFuncionarioCasoDeUso.DaRecusaAsync` + `LinhaRecusadaException` |
| `ErrosDoBanco` | Infrastructure/Persistencia/ErrosDoBanco.cs | reconhece 1062 e 1451 (sem dizer a chave) |
| `ParametrosDeLista`, `MapaDeOrdenacao<T>`, `EnumsDeConsulta.Ler`, `RespostaPaginada<T>`, `ToRespostaPaginadaAsync`, `BuscaLike` | Application/Comum/Consultas/; Infrastructure/Persistencia/ConsultasEf.cs | paginação e filtros pelo nome do contrato |
| `EnumsDeParametrosDoContrato`, `NomesDeParametrosDoContrato` | Api/Configuracao/ | acrescentar os pares (operationId, parâmetro) de `status`, `etapa`, `ordenarPor`, `direcao`, `tipo`, `situacao` |
| `CsvSeguro`, `ArquivoExportado` | Application/Comum/Exportacao/ | CSV `;`, BOM, CRLF, neutraliza `= + - @ TAB CR`; servido como `Results.Bytes(..., "text/csv; charset=utf-8")` (modelo: AuditoriaEndpoints) |
| `IRelogio`, `FusoBrasil` (`Hoje`, `ParaLocal`, `ParaUtc`, `DiaLocalDe`, `InicioDoDiaEmUtc`), `ConversoresDeTempo.Brasil/Utc` | Application/Autenticacao/Contratos.cs; Application/Comum/Tempo/FusoBrasil.cs; Infrastructure/Persistencia/ConversoresDeTempo.cs | todo instante UTC dentro da aplicação |
| `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync` (com isolamento), `OperacoesComTrava` | Application/Autenticacao/Contratos.cs; Infrastructure/Colaboradores/OperacoesComTrava.cs | transação e trava por pessoa |
| `FuncionarioResumo` | Application/Colaboradores/FuncionarioResumo.cs | o schema da base |
| Testes: `ApiFactory` (`RelogioDeTeste.Fixar`, `Armazenamento`, `Enviador`, `ComPedidosParados`...), `ElencoDeTeste` (relógio em 2026-10-07T12:42Z; Ana, Carlos, Diego, Júlia, Rafael, Henrique inativo, Marina, Bruno, Paulo sem setor), `ApiDeAcesso.ObterAsync(mysql, chave)` (um banco por classe + hospedeiro com ator RH), `SementeDoCadastro` (cargo, setor, colaborador, desligamento agendado), `PessoasDeTeste.ComLoginAsync`, `ArmazenamentoEmMemoria`, `ContratoDivergenciaTests.OperationIdsNoEscopo`, `RespostaConformeAoContrato.AfirmarConformeAoContratoAsync(operationId)`, `MatrizDePerfis` (chama toda rota com id `int.MaxValue` e corpo `{}`: a rota tem de dar 400/404 **antes** de qualquer efeito), `ContratoEStatusTests` (todo endpoint implementado no `STATUS.md`), `MapeamentoContraBancoTests` | back-end/tests/RHSystem.Api.IntegrationTests/ | |

### E.2 Não existe (criar)

- **Entidades e mapeamentos**: `Ponto`, `MarcacaoDePonto`, `PedidoDeAjuste`, `ItemDoPedidoDeAjuste`, `LancamentoDoBancoDeHoras`, `Feriado`, `Atestado`; leitura de `FERIAS` (sem dono ainda: a 7 será a dona; sugestão: consulta SQL/projeção sem entidade, ou criar já a entidade `Ferias` mínima com todas as colunas NN mapeadas).
- **Catálogo de erros do ponto** (`MensagensDoPonto`, com status, título, detalhe e código copiados do contrato) e o teste que o confere contra o yaml, nos moldes de `MensagensDoApoioTests`.
- **Tradutor de recusa do banco do ponto**: 45000 → código por mensagem; 1062 → código pelo nome da chave; 1451 → `PONTO_MARCACAO_EM_USO`.
- **Regras puras**: pareamento/rótulos/próxima batida, consistência, horas em minutos, atraso, situação do dia, `podeDecidir`/`motivoNaoPodeDecidir`, `resumoDaCorrecao`.
- **Calendário da empresa** (dia útil = seg-sex e não feriado que vale para a UF/município) e a fonte de fatos do dia do ponto (substitui a da 1b, que "só conhece o fim de semana": `Infrastructure/Colaboradores/FonteDeFatosDoDiaSemRegistros.cs`).
- **`IPedidosParadosNoGestorDaFatia` do ponto** (hoje o composto é vazio; STATUS.md:48, 50, 63, 66, 81 registram a dívida).
- **Fontes do menu**: `PedidosDeAjusteDePontoDoGestor` (= `resumo.comGestor` do gestor), `PedidosDeAjusteDePontoDoRh` (= `comRh`), e a parte de ajustes em `PendenciasDaEquipe`.
- **Fonte do indicador** `BancoDeHorasMinutos` e a **condição de desligamento** do ponto.
- **Tarefas**: `CONSOLIDAR_BANCO_HORAS`, importação anual de feriados nacionais/estaduais, `REPETIR_FERIADO`.
- **Renderizador de e-mail** do resultado do pedido (tipo novo, por exemplo `RESULTADO_AJUSTE_PONTO`, chave `...:pedido={id}:etapa=GESTOR|RH`).
- **Biblioteca de PDF**: nenhuma no `Directory.Packages.props` (há OpenXml, MailKit, Blobs, Pomelo). Candidatas a conferir na hora (INFERIDO): PDFsharp/MigraDoc 6 (MIT, exige resolvedor de fonte com fonte embutida no Linux) e QuestPDF (licença Community com limite de receita). Gerar, gravar no blob (pasta própria, por exemplo `espelhos/`) e devolver o link; registrar uma `IFonteDeReferenciasDeArquivo` que não referencia nada, para a limpeza apagar depois de 24 h.
- **Fonte de feriados estaduais por UF** (dado a manter; a nacional sai de cálculo com Páscoa para a Sexta-feira Santa).
- **Gerador de CSV**: **já existe** (`CsvSeguro`); falta só o caso de uso.

---

## F. Lacunas, contradições e perguntas

### F.1 Decisões do dono já registradas (não reabrir)

Duas etapas, com a do gestor em colunas e sem reescrever triggers (2026-10-29); sem "RH decide direto" (2026-10-29); "Gestor ausente" em qualquer momento e motivo (2026-10-30); RH pode recusar o que o gestor aprovou (2026-10-30); e-mail a cada decisão, lista fechada (2026-10-30); RH confirma o próprio pedido (2026-10-29); quem decide pedido de gestor é o RH que está em `GESTORES` (2026-10-29); várias batidas, um dia por pedido (2026-10-29); correção direta do RH sem segunda etapa nem e-mail; banco de horas só automático, só Crédito/Débito (2026-10-28 e design seção 7); sem unidades; carga semanal ÷ 5 e horário padrão único (2026-10-28); atestado: gestor vê, só RH valida, Falta Compensada, recusa com motivo (2026-10-29); `DATA_CORRECAO` como fonte de `corrigidaEmUtc` (2026-10-30); gestor não baixa o espelho do setor; desligamento agendado gravado no dia seguinte depois da consolidação (2026-10-29).

**Escolhas de engenharia do contrato que o dono ainda pode vetar** ("escolha minha"): bater em feriado/folga aceito; ao ligar a ausência, aprovar os parados; setor sem gestor ativo → RH; aprovação automática sem e-mail; não lançar `INCOMPLETO`/`AJUSTE_EM_ANALISE`; extrato do RH sem tela; `listarFeriados` sem empresa não dá erro; `ativo:false` no feriado; atestado PDF/JPG/PNG até 5 MB.

### F.2 Decisões do dono abertas (x-provisorio e roadmap:176)

1. Janela da jornada após a meia-noite (12 h assumidas).
2. Prazo para pedir ajuste de dia antigo e quem registra ponto retroativo (sem prazo assumido).
3. Hora extra e tolerância: todo saldo positivo vira Crédito sem adicional; tolerância só marca atraso.
4. Formato da folha (CSV por pessoa assumido).
5. Quando um dia é `INCOMPLETO` (só sequência quebrada; o Figma das telas 55/56 sugere 4 batidas).
6. Regra do atraso (`atrasoMinutos` conta desde o horário padrão quando passa da tolerância).
7. Fonte dos feriados (cálculo ou biblioteca) e nomes (os de 2026 sem acento).
8. Uso das funções do grupo (chamar ou reimplementar).

### F.3 Contradições

- **Figma tela 13** mostra "Aprovar e corrigir o ponto" e "Recusar" no pedido "Com o gestor" e diz "O gestor do setor também pode analisar"; pela decisão do dono os botões só valem depois do gestor (`podeDecidir`), e o rodapé é só informação (roadmap:165; yaml:1865-1872). A tela 55 já diz "Depois de você, o RH confirma".
- **Fuso dos campos que o trigger preenche**: roadmap:73 manda "deixar vazias as colunas que os triggers preenchem"; a decisão 7 da seção 12 do plano do back-end manda a aplicação preencher pelo `IRelogio` (para as tabelas da aplicação). Para `DATA_SOLICITACAO`, `DATA_ANALISE` e `ATESTADOS.DATA_ENVIO` (tabelas do grupo) as duas regras se chocam com o relógio falso dos testes (C.5.1). **O executor decide e registra**; recomendo gravar as três pelo `IRelogio` convertido para -03:00 (o trigger só preenche se vier NULL).
- **`EMPRESA_NAO_CONFIGURADA` ao bater ponto**: a fatia 1 (`obterEmpresa`, f1:1840) diz que registrar ponto responde 422 sem empresa; o contrato do ponto não lista esse erro e prevê `PontoJornada` toda `null`. Seguir o contrato do ponto (sem 422) e registrar.
- **Texto do `EMPRESA_NAO_CONFIGURADA`**: `IConfiguracaoDaEmpresa.ObterOuFalharAsync` lança `ErroDeNegocioException` com o texto da 1c ("A empresa ainda não foi configurada.", título "Operação recusada."); o exemplo de `criarFeriado` pede título "Empresa não configurada." e outro detalhe. Não reusar o método como está.
- **Direção padrão**: `PontoPedidoOrdenarPor` diz "padrão `DESC`"; `MapaDeOrdenacao` usa `ASC` quando vem `ordenarPor` sem `direcao`. A tela 13 manda `direcao` explícito, mas a regra difere.
- **Janela do instante**: o pedido aceita qualquer hora do dia seguinte (`PONTO_INSTANTE_FORA_DO_DIA`), a batida usa 12 h.
- **Funções do grupo** (roadmap:164 "apoiados nas funções") × contrato que proíbe `FN_CALCULAR_SALDO_BANCO_HORAS` e precisão em minutos.
- **`editarFeriado`** referencia 409/422 genéricos, mas a descrição fixa `FERIADO_DUPLICADO` e `FERIADO_NAO_EDITAVEL` (este sem texto de exemplo).
- **Quem é gestor**: o contrato diz "quem tem linha em `GESTORES`" (yaml:51); `GestorAusente` exige linha **e** (gerenciar setor **ou** nível RH) (Infrastructure/Gestores/GestorAusente.cs). Quem foi rebaixado a Funcionario mantém a linha mas tem etapa do gestor. Usar a regra do código (consistente com f1:1466-1467).
- **"Arquivo só depois de a regra do banco aprovar"** (roadmap:76; yaml:968) × `ATESTADOS.CAMINHO_ARQUIVO NOT NULL` (a linha não nasce sem o caminho): seguir a receita da 1c (validar tudo, blob fora de transação, transação curta, apagar o blob se falhar), que cumpre a intenção.
- **Ordem do roadmap**: 3a promete "faltas" no espelho, mas feriados (que tiram a falta) estão em 3c. Proponho trazer a leitura de feriados para a 3a (G).

### F.4 Lacunas que o executor decide e registra

1. **Pedido parado quando a pessoa muda de setor** (ou vira gestor): nada passa o pedido ao RH; o gestor antigo deixa de vê-lo (não está mais na equipe) e ninguém decide a etapa do gestor. Sugestão: tratar na edição do colaborador (porta da 1b) ou deixar o gestor antigo decidir pelo `ID_GESTOR`; perguntar ao dono.
2. **Inativo em aprovação/correção/atestado**: o contrato não lista `FUNCIONARIO_INATIVO` na aprovação do RH nem no envio de atestado; o banco não impede batida em dia já existente.
3. **Atestado**: sobreposição, datas futuras, limite de dias; "horas cobertas" = duração bruta ou interseção com o expediente; vários atestados no mesmo dia; atestado de dia inteiro validado em dia com batidas (carga 0 → tudo vira crédito?); auditoria do envio (`CRIOU`?) e da validação (`APROVOU`/`RECUSOU` em `ATESTADOS`).
4. **Feriado criado/editado/desativado em data já consolidada**: o contrato não manda recalcular.
5. **Férias canceladas depois da consolidação** (fatia 7): recalcular.
6. **Consolidação**: quando roda o dia D (00:00 de D+1 ou D+1 12:00 por causa da meia-noite); horizonte da recuperação (último dia concluído, data mínima configurável); como o `atualizadoAte` sobrevive à limpeza de 30 dias; dia `INCOMPLETO` para sempre não lança nada.
7. **Gestor sem setor** em `obterPontoDaEquipe` e listas do gestor: lista vazia? (o contrato não lista 404).
8. **Dia passado em `obterPontoDaEquipe`**: equipe de hoje ou da data (admitido depois aparece?).
9. **`corrigirMarcacoesDoDia`** em dia futuro, em dia de quem ainda não foi admitido; `id` de batida de outro dia/pessoa (400? 404?).
10. **Destinatário do e-mail**: `EMAIL_CORPORATIVO` pode ser NULL (CHECK da fila recusa); pular e registrar?
11. **Auditoria da aprovação automática** (ator Sistema? sem evento?) e do "passar ao RH" (`ExecuteUpdate` exige evento ou `AUDITORIA-OK`).
12. **Exportação**: "pessoa ativa no mês" inclui quem saiu no meio do mês?; `matricula` NULL.
13. **Equipe e pedidos do gestor**: filtrar por `DaEquipe` (pessoa) e `podeDecidir` por `ID_GESTOR = gestor logado` (INFERIDO).

---

## G. Proposta de divisão em tarefas

Cada tarefa entrega rotas com os `operationId` acrescentados a `OperationIdsNoEscopo`, linha no `STATUS.md`, `AfirmarConformeAoContratoAsync` nas respostas e a matriz de perfis passando. Assinaturas são sugestões.

### 3a. Bater ponto e espelho

**T1. Mapeamento das tabelas do ponto.** `Ponto`, `MarcacaoDePonto` (`Origem`, `CorrigidaEmUtc`, instante truncado ao segundo), `PedidoDeAjuste`/`ItemDoPedidoDeAjuste` (`CHAVE_PENDENTE` calculada), `LancamentoDoBancoDeHoras`, `Feriado`, `Atestado`; leitura de férias. Entrega: `MapeamentoContraBancoTests` verde e uma sonda que grava e relê `ORIGEM`/`DATA_CORRECAO`, `ETAPA_GESTOR_AUTOMATICA=false` e um instante com fração (prova o truncamento). Arquivos: Domain/Ponto/*, Infrastructure/Persistencia/Configuracoes/*.

**T2. Regras puras do dia.** `SequenciaDoDia` (`Periodos(...)`, `EhConsistente(...)`, `Rotulo(ordem, tipo)`, `ProximaMarcacao(diaAtual, diaAnterior, agora)` com a janela de 12 h), `CalculoDoDia` (`HorasEmMinutos`, `Atraso(primeiraEntrada, horario, tolerancia)`, `Saldo`), `SituacaoDoDiaNoEspelho.Calcular(...)` com a precedência do contrato. Entrega: testes de unidade (meia-noite, 5+ batidas, sequência quebrada, segundos, atraso 08:10 vs 08:11) e um teste de integração comparando `EhConsistente` com `FN_VERIFICAR_CONSISTENCIA_PONTO`. Arquivos: Application/Ponto/Regras/*.

**T3. Calendário da empresa e fonte de fatos do dia.** `ICalendarioDoPonto` (`ObterDiaAsync(dia)`, `FeriadosQueValemAsync(de, ate)`: nacionais, estaduais da UF, municipais do município, `EMPRESA`, `ATIVO`) e `FonteDeFatosDoDiaDoPonto : IFonteDeFatosDoDia` (batida de entrada hoje, férias `APROVADA|EM_GOZO|CONCLUIDA`, atestado validado de dia inteiro, limite = entrada + tolerância). Entrega: `situacaoHoje` real em `GET /api/gestor/equipe` e `GET /api/rh/funcionarios` (ajustar STATUS.md:43-53). Depende de T1.

**T4. Erros do ponto.** `MensagensDoPonto` (todos os pares da tabela de A) com teste contra o yaml; `TradutorDeRecusaDoPonto` (`Traduzir(Exception, ContextoDaRota)`: 45000 → `FUNCIONARIO_INATIVO`/genérico/`PEDIDO_PROPRIO`/`PEDIDO_RH_FORA_DE_GESTORES`/`PEDIDO_JA_DECIDIDO`; 1062 por chave; 1451 → `PONTO_MARCACAO_EM_USO`); `ErrosDoBanco.ChaveRepetida(Exception) : string?`. Entrega: testes de unidade.

**T5. Bater ponto e cartão de hoje** (#1, #2). `ObterPontoHojeCasoDeUso`, `RegistrarMarcacaoCasoDeUso` (trava por pessoa, cria o dia, confere inativo antes, `PONTO_SEQUENCIA_MUDOU`, 1062 → `PONTO_BATIDA_DUPLICADA`, evento `PONTO`/`REGISTROU`), `ISaldoDoBancoDeHoras.ObterAsync(funcionarioId)` (soma por linha) e `atualizadoAte` (null até a 3c). Entrega: testes de duplo clique paralelo, duas abas, batida às 22:30 local, meia-noite, inativo com token válido, antes da admissão. Depende de T1-T4.

**T6. Espelho** (#3, #11, #19). `IMontadorDoEspelho.MontarAsync(funcionarioId, ano, mes, comPermissaoDePedido)`; casos de uso por família (o do gestor por `DaEquipe` → 404). Lê pedidos pendentes e atestados (vazios até a 3b). Entrega: espelho de outubro do elenco igual ao exemplo do contrato (situações, resumo, atraso 14, 02/10 em análise quando houver). Depende de T2, T3.

**T7. Ponto da equipe** (#10). `ObterPontoDaEquipeCasoDeUso` reusando `ISituacaoDeHoje` e o montador do dia. Entrega: os números do elenco (6 trabalhando, Diego sem registro, Larissa de férias); dia passado com `situacaoHoje: null`.

**T8. Porta de recálculo.** `IRecalculoDoBancoDeHoras.RecalcularDiaAsync(funcionarioId, dia, ct)` com implementação que não faz nada até a 3c (as tarefas da 3b já chamam). Pequena; pode ir junto da T5.

### 3b. Ajustes em duas etapas e atestado

**T9. Modelo do pedido.** `EtapaDoPedido` derivada das colunas (C.4), `RegrasDeDecisaoDoPedido.PodeDecidir(pedido, quemConsulta)` com os cinco textos, `ResumoDaCorrecao.Montar(itens, marcacoes)`, `AplicacaoDosItens.Aplicar(diaAtual, itens)` (resultado + validação de sequência/duplicidade). Entrega: testes de unidade (Sofia × Roberto, pedido do próprio RH, automática).

**T10. Pedir e ver os próprios** (#4, #5, #6). `CriarPedidoDeAjusteCasoDeUso` (usa `IGestorAusente.EtapaInicialDoPedidoAsync`; grava `ID_GESTOR`/`RESULTADO_GESTOR`/automática; valida dia passado, futuro, fora do dia, sequência, `marcacaoId` do dia, itens repetidos; 1062 da `CHAVE_PENDENTE` → 409; evento `PEDIU_AJUSTE`), `ListarMeusPedidosCasoDeUso`, `ObterMeuPedidoCasoDeUso`. Entrega: os quatro exemplos de criação (303, 302, 296, 297) e o duplo envio.

**T11. Etapa do gestor** (#13-#16). `ConsultaDePedidosDoGestor`, `DecidirComoGestorCasoDeUso.AprovarAsync/RecusarAsync` (409s; e-mail), `ResultadoAjustePontoRenderizador : IRenderizadorDeEmail`, fontes do menu do gestor e de `PendenciasDaEquipe`. Entrega: e-mail na fila com chave única; pedido de outra equipe/gestor/próprio = 404; corrida gestor × ausência = 409.

**T12. Etapa do RH** (#23-#26). `ConsultaDePedidosDoRh` (com `resumo`), `DecidirComoRhCasoDeUso` (`PEDIDO_PROPRIO` antes de `PEDIDO_RH_FORA_DE_GESTORES`; `ID_GESTOR` = linha do RH no pedido sem etapa do gestor; aplica tudo ou nada com `CORRIGIDA_RH` + `DATA_CORRECAO`; recalcula o dia; e-mail), fonte do menu do RH. Entrega: aprovado pelo gestor e recusado pelo RH e o contrário; várias batidas, uma corrigida e uma nova; dia que mudou → 422; duplicada → 409 sem nada aplicado.

**T13. Pedidos parados no gestor.** `PedidosParadosNoGestorDoPonto : IPedidosParadosNoGestorDaFatia` (`AprovarEtapaDoGestorAsync` com `automatica`, `PassarParaORhAsync` com `ID_GESTOR = NULL`, eventos explícitos). Entrega: `pedidosAprovadosAgora.ajustesDePonto` real em `configurarAusenciaDoGestor`; trocar/tirar/desligar gestor passa ao RH; STATUS.md:48, 50, 63, 66, 81.

**T14. Correção direta do RH** (#21). `CorrigirMarcacoesDoDiaCasoDeUso` (substituição da lista, 409 com pedido em análise, `PONTO_MARCACAO_EM_USO`, evento com a batida antiga, recálculo). Depende de T10.

**T15. Atestados** (#8, #9, #17, #18, #31-#34). `EnviarAtestadoCasoDeUso` (validação de arquivo parametrizada para 5 MB, pasta `atestados/`), `ConsultaDeAtestados` por família, `BaixarAtestadoCasoDeUso` (evento `ATESTADOS`/`BAIXOU` antes do link), `DecidirAtestadoCasoDeUso.ValidarAsync/RecusarAsync` (409 `ATESTADO_JA_DECIDIDO` com concorrência pela situação; recálculo dos dias), `FonteDeReferenciasDosAtestados`, `FormulariosDoContrato.EnviarAtestado`. Entrega: gestor de outra equipe 404; atestado visto por quem não pode; arquivo órfão apagado se o banco recusar; `FALTA_COMPENSADA` no espelho e `ATESTADO` em `situacaoHoje` só depois de validado.

### 3c. Feriados, banco de horas, extrato, folha e PDF

**T16. Feriados** (#27-#29). `ListarFeriadosCasoDeUso`, `CriarFeriadoCasoDeUso`/`EditarFeriadoCasoDeUso` (`EMPRESA_NAO_CONFIGURADA` com o texto do ponto, `FERIADO_DUPLICADO`, `FERIADO_NAO_EDITAVEL`), `ImportarFeriadosTarefa` (`IMPORTAR_FERIADOS:ano=AAAA`; nacionais por cálculo, estaduais pela UF; dedup por data+tipo) e `RepetirFeriadoTarefa` (`REPETIR_FERIADO:ano=AAAA`, copia do registro mais recente, pula 29/02). Entrega: ponto em feriado; importar duas vezes não duplica; nomes com e sem acento.

**T17. Consolidação do banco de horas.** `ConsolidarBancoDeHorasTarefa` (`CONSOLIDAR_BANCO_HORAS:dia=AAAA-MM-DD`; recuperação com horizonte), `RecalculoDoBancoDeHoras : IRecalculoDoBancoDeHoras` (insere, atualiza ou apaga a linha do dia; esperado sem férias, feriado, folga e horas de atestado; não lança `INCOMPLETO`/`AJUSTE_EM_ANALISE`; "Falta" de dia sem batida), `IUltimoDiaConsolidado.ObterAsync()`, `CondicaoDoBancoDeHorasParaDesligamento : ICondicaoParaEfetivarDesligamento`. Entrega: rodar duas vezes sem duplicar; parada de vários dias; recalcular após ajuste e atestado; desligamento só depois de fechar o último dia; inativo com linha existente. Depende de T6, T15, T16.

**T18. Extrato e indicador** (#7, #22). `ObterExtratoDoBancoDeHorasCasoDeUso`, `FonteDoBancoDeHorasNaFicha : IFonteDeIndicadorDaFicha`. Entrega: saldo por linha igual ao exemplo (380); indicador da ficha deixa de ser `null`.

**T19. Exportação para a folha** (#30). `ExportarPontoParaFolhaCasoDeUso` reusando o montador do espelho e `CsvSeguro`. Entrega: CSV com acentos, BOM, nome com `=` neutralizado, nenhum número negativo.

**T20. Espelho em PDF** (#12, #20). Escolha e registro da biblioteca (licença, fonte com acento embutida, Linux), `IGeradorDePdfDoEspelho.Gerar(PontoEspelho) : byte[]`, `BaixarEspelhoEmPdfCasoDeUso` (grava em `espelhos/`, devolve `LinkTemporario`), fonte de referências vazia para a limpeza. Entrega: PDF com "São Paulo" e "Júlia" legíveis em contêiner Linux; gestor de outra equipe 404.

**T21. Fechamento.** Matriz de perfis completa, os 34 `operationId` no escopo, STATUS.md sem pendência do ponto, revisão.

**Ordem e dependências:** T1 → T2 → T3 → T4 → T5/T8 → T6 → T7 (fim da 3a) → T9 → T10 → T11 → T12 → T13 → T14 → T15 (fim da 3b) → T16 → T17 → T18 → T19 → T20 → T21. T16 pode ser antecipada para depois da T3 se o espelho da 3a precisar dos feriados importados; T20 depende só de T6 e pode correr em paralelo com a 3b se a biblioteca for escolhida cedo.
