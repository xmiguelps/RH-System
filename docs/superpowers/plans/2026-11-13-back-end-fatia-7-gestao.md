# Back-end, fatia 7 (gestão: férias, benefícios, pagamento e painéis) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar as 41 rotas de gestão (`contrato/fontes/70-fatia-7-gestao.yaml`; 3 delas, os contadores do menu, já existem e só ganham a fonte de férias): férias em duas etapas (pedir, conferir, sugestão de data, gestor, RH, registro direto, calendário, tarefa diária), benefícios com dependentes e trajeto (pedidos da pessoa e decisões do RH), próximo pagamento com exceções e calendário, e os painéis do funcionário, do gestor e do RH.

**Architecture:** Mesma receita das fatias 3 a 6: regras puras em `Application/<assunto>/Regras` (alvo de mutação), casos de uso por porta, repositórios e consultas em `Infrastructure`, endpoints finos registrados por `operationId`, catálogo de mensagens do contrato com teste contra o `openapi.yaml`, auditoria à mão nas escritas por `ExecuteUpdate`. Férias copia o desenho do ponto (etapa deduzida das colunas, decisão como UPDATE condicional, e-mail e evento na mesma transação) **sem** copiar o que o ponto faz de errado para férias (sobrescrever `OBSERVACAO`); o saldo é calculado em **um só lugar**, em lote, e usado pelas seis telas e pelo indicador da ficha. Nenhum script SQL novo (tudo já está no `V001`).

**Tech Stack:** .NET 10, EF Core 9 + Pomelo 9.0.0, MySQL 8.4 (Testcontainers), xUnit.

**Spec:** `contrato/fontes/70-fatia-7-gestao.yaml` (autoridade; `base`, `CONVENCOES.md`, `PROVISORIOS.md`, `ELENCO.md`) e o levantamento `docs/superpowers/plans/2026-11-13-back-end-fatia-7-gestao-levantamento.md` (citado `L:A.n`, `L:B`, `L:C`, `L:D.n`, `L:E.n`, `L:F`, `L:G`, `L:H`); o plano argumenta a partir deles: **os dois viajam com o plano e o executor lê os dois**.

## Global Constraints

- O contrato vence o roadmap e os comentários do `V001` (L:E.3). Nenhum script novo; o `V006` é do dono. **E-mails: lista fechada** (`CONVENCOES:114`): só férias, um por decisão (gestor aprova, recusa, sugere; RH confirma, recusa), chave de idempotência por pedido **e** decisão, todo valor com `HtmlEncode`; **nenhum** ao criar, aceitar, recusar a sugestão, aprovação automática, registro do RH, tarefa, nem nada de benefício; pessoa sem e-mail corporativo: a decisão funciona e nada entra na fila.
- Datas e instantes: `FERIAS`, `BENEFICIOS_FUNCIONARIOS`, `DEPENDENTES` e `TRAJETOS_VALE_TRANSPORTE` guardam `DATETIME` em **-03:00** (`ConversoresDeTempo.Brasil`), cortados ao segundo; "hoje" pelo dia de Brasília (`FusoBrasil`); **toda** data gravada vem do `IRelogio` (os `NOW()`/`CURDATE()`/`DEFAULT CURRENT_TIMESTAMP` do banco usam o relógio real): `DATA_SOLICITACAO`, `DATA_ANALISE`, `DATA_ETAPA_GESTOR`, `DATA_CONFIRMACAO_RH`, `DATA_RESPOSTA_SUGESTAO`, `DATA_PEDIDO` e `DATA_RESPOSTA` saem da aplicação; `DATA_ANALISE >= DATA_SOLICITACAO` (CHECK) é o motivo de nunca gravar UTC.
- Colunas com padrão do banco mapeadas **sem** padrão (gravar sempre): `STATUS_FERIAS`, `ABONO_PECUNIARIO`, `DIAS_ABONO`, `ETAPA_GESTOR_AUTOMATICA`, `ADIANTAR_13_SALARIO`, `BENEFICIO_ATIVO` (o pedido grava `FALSE`), `STATUS_PEDIDO` de dependente e trajeto, `AUTORIZA_DESCONTO_FOLHA`.
- **Cada mudança de estado de `FERIAS` é UM `UPDATE` condicional** ao estado de partida que grava juntas todas as colunas que os `CHECK` em cadeia exigem (L:B.2: resultado + data da etapa + status + motivo; `ID_FUNCIONARIO_RH` + `DATA_CONFIRMACAO_RH`; resposta + data; sugestão: duas datas + resultado). **`OBSERVACAO` (a mensagem da pessoa) nunca é sobrescrita** por decisão; o sufixo " | Cancelada automaticamente: funcionario inativado" que o trigger acrescenta é tirado na leitura. Ids de quem decide vêm do servidor, nunca do corpo.
- **Saldo de férias: um só cálculo** (`SaldoDeFerias`, com forma em lote: várias pessoas, uma consulta) usado por #1, #11, #15, #34, a coluna de #38 e o indicador `feriasDisponiveisDias` da ficha; nenhum painel chama o cálculo pessoa a pessoa (sem N+1). Pedir, registrar e aceitar sugestão **travam a pessoa** (`OperacoesComTrava`, `FOR UPDATE` em `FUNCIONARIOS`) antes de conferir, na mesma transação da gravação.
- Quem vê o quê: a pessoa só o que é dela (`IEscopoDoUsuario.FuncionarioId`); o gestor só a equipe de hoje (a de `GET /api/gestor/equipe`: sem ele e sem outros gestores) e **o mesmo 404** para pedido inexistente, próprio, de outro gestor e de outro setor; **dependentes, trajeto, origem/destino, endereço e CPF nunca** em rota de gestor, resposta de painel, mensagem de erro ou log de auditoria (origem e destino e nascimento do dependente como campo sensível: "campo mudou" sem valor).
- Todo `ExecuteUpdate`/`ExecuteDelete` tem `// AUDITORIA-OK: motivo` até 3 linhas acima (`GuardaDeExecuteUpdateTests`); eventos de auditoria **à mão** (áreas `FERIAS` e `BENEFICIOS`; pagamento usa `CADASTROS`); a tarefa agendada audita como Sistema. Colunas anuláveis do banco ficam anuláveis na entidade (`MapeamentoContraBancoTests`); as quatro tabelas saem de `tabelas-sem-mapeamento.txt` no mesmo passo em que são mapeadas.
- Cada rota: o `operationId` em `ContratoDivergenciaTests.OperationIdsNoEscopo`, a linha em `contrato/STATUS.md`, enums de consulta (`FeriasSituacao`, `BeneficioAdesaoSituacao`, `PainelPendenciaTipo`) em `EnumsDeParametrosDoContrato`, `AfirmarConformeAoContratoAsync` nas respostas, matriz de perfis verde. **Rotas registradas por `operationId`, nunca por prefixo** (`/api/gestor/equipe/{funcionarioId}/ferias`, `/api/rh/funcionarios/{funcionarioId}/ferias` dividem prefixo com as fatias 1, 3, 4 e 6; ids `{x:int}`); os três `contadores-do-menu` **não** são registrados de novo.
- **TDD de verdade:** o teste vermelho vem antes do código e o vermelho é visto; depois de cada tarefa `dotnet format back-end --verify-no-changes` e os transversais (`GuardaDeExecuteUpdate|.Contrato.|Matriz|Persistencia|Configuracao|Agendamento`). Commit e push por tarefa; **sem** `Co-Authored-By`, `Claude-Session` nem "Generated with Claude Code" em commit ou PR (`git log origin/main..HEAD --format=%B | grep -ciE "Co-Authored-By|Claude|Generated with"` = 0 antes de todo push).

## Decisões do plano (Rulings; as perguntas H do levantamento e as lacunas de L:E.4, decididas sem esperar o dono, cada uma com o custo se errada)

1. **Regras de férias (H1):** as oito de `FeriasRegraCodigo` como escritas: duração de cada parte 5 a 30; no máximo 3 partes por período; abono de 1 a 10 dias escolhido pela pessoa; 30 dias de antecedência (`início >= hoje + 30`); saldo **não vence** (`prazoParaTirar` é só informação), 30 dias sempre (sem desconto por falta, sem regra de estagiário); `adiantar13Salario` só é guardado; o pedido **não** se divide entre períodos; pode sobrar um resto de 1 a 4 dias que nunca poderá ser tirado (o exemplo do contrato aceita). *Custo:* médio (T3 e mensagens); alto se entrar perda de saldo por prazo ou falta.
2. **Parte de 14 dias (L:E.4.4):** fórmula escrita aqui: para o período escolhido, com `P` = partes já contadas, `M` = a maior delas, `g` = gozo do pedido e `R = disponíveis - g - abono`, a regra vale quando `max(M, g) >= 14` **ou** (`P + 1 < 3` e `R >= 14`). Confere com o exemplo do Bruno (12 tirados, saldo 18: só aceita 14 a 18). **Contam** como partes e como reserva `APROVADA`, `EM_GOZO`, `CONCLUIDA`, `COM_O_GESTOR`, `COM_O_RH`; `SUGESTAO_DE_DATA`, `REJEITADA` e `CANCELADA` não contam. O abono entra no saldo e não é parte. *Custo:* baixo no código, **alto na demo se o dono tiver outra conta**: é a regra que ele mais vai testar (README, pergunta 1).
3. **Feriado e descanso (H2):** proibido **começar** em feriado (os do `ICalendarioDoPonto`), sábado ou domingo, e nos 2 dias corridos antes de feriado ou de sábado. *Custo:* uma regra e os testes dela.
4. **Período aquisitivo (H3):** só são candidatos os períodos fechados que **terminam antes do início**; escolhe o mais antigo em que o pedido cabe inteiro; sem nenhum fechado: 422 `FERIAS_SEM_PERIODO_AQUISITIVO`; com fechados mas nenhum com saldo: regra `CABE_NO_SALDO` não atendida, e `diasQueSobram` se refere ao `periodoDoSaldo` (o fechado mais antigo com saldo; sem nenhum, o mais antigo). Sugestão do gestor com início **depois** do fim do período do pedido exige 400 no campo `dataInicio` ("A sugestão precisa começar depois do fim do período aquisitivo do pedido.") em vez de código de regra novo; sugestão com início antes de hoje: 400. *Custo:* baixo; pode pedir um código novo no contrato.
5. **Abono incoerente (L:B.1):** `abonoPecuniario` falso com `diasAbono > 0`, ou verdadeiro com 0, é a regra `ABONO_ATE_10_DIAS` **não atendida** (a conferência responde 200); só o número fora de 0 a 10 é 400 no campo.
6. **Sugestão do gestor (H4):** confere **todas as regras menos a antecedência** (e desconta o próprio pedido da sobreposição), com `regras[]` no 422; senão a pessoa nunca conseguiria aceitar. Aceitar reconfere tudo menos a antecedência, inclusive `CABE_NO_SALDO`. *Custo:* médio, diverge do texto "só pelas regras do banco" do contrato.
7. **Pedido vencido e prazo (H5):** a tarefa cancela `SOLICITADA` com `DATA_INICIO < hoje` (menor, não menor ou igual) **exceto** sugestão pendente (`SUGERIU_DATA` sem resposta), que só cai pela expiração: `expiraEm = min(dia local da sugestão + 5, início sugerido - 2)` e vence quando `hoje > expiraEm`. Aceitar ou recusar depois de `expiraEm` e antes da tarefa: o caso de uso expira o pedido (mesma função da tarefa) e responde 409 `PEDIDO_JA_DECIDIDO`. *Custo:* baixo.
8. **Troca de setor com pedido parado (H6):** a pessoa que muda de setor tem os pedidos parados no gestor antigo (férias e ponto) passados ao RH, pela mesma porta `IPedidosParadosNoGestorDaFatia` (método novo se preciso, com as duas implementações). *Custo:* médio (mexe na edição da ficha da fatia 1 e fecha a dívida do ponto, `STATUS:213`); se não couber num método novo da porta, `Ruling:` e a dívida fica no README.
9. **Benefício direto e pedir de novo (H7):** o RH que concede direto passa a gravar `ID_FUNCIONARIO_RH` e `DATA_RESPOSTA` (código da fatia 1, T18); adesão aprovada e depois inativa (o RH tirou, ou readmissão) aparece em `disponiveis` com `ultimoPedido: null` e o pedido reabre a mesma linha. Reabrir cancela os dependentes e trajetos antigos que não vêm no novo pedido (`V001:2466-2469`) e guarda a autorização de desconto anterior no log. Dependente repetido: aceito (o RH recusa); trajeto novo cancela o `PENDENTE` anterior da adesão; trajeto é opcional; `dependentesCadastrados` sem repetir por (nome sem caixa, parentesco, nascimento), inclui aprovados de adesão inativa. *Custo:* baixo.
10. **Holerite agendado (H8):** não acompanha exceção de pagamento criada, trocada ou removida depois (fica a data calculada no envio); fica escrito no README. *Custo:* médio se o dono quiser reagendar.
11. **Registro no passado (H9):** o registro do RH recalcula o banco de horas dos dias **já passados** do período (`IRecalculoDoBancoDeHoras.RecalcularDiaAsync`, dia a dia, depois do commit, falha isolada e logada). *Custo:* médio; sem isso o saldo fica errado em silêncio.
12. **Benefícios como pendência (H10):** contam em `PainelPendenciaTipo.BENEFICIOS` (provisório do contrato). **Painel do gestor (H11):** `NAO_SE_APLICA` = a pessoa não é avaliada pelo gestor naquele formulário (e quem está de férias hoje, como o exemplo); `ciclo` = o que vem depois de "ciclo " no título do formulário aberto, senão o título; com dois abertos, o de prazo mais próximo. *Custo:* baixo.
13. **Inativação (L:E.4.8):** o trigger cancela férias futuras `SOLICITADA`/`APROVADA` sem aviso nem evento; `EM_GOZO` e as que começam hoje seguem e a tarefa as conclui; saldo, filas e painéis ignoram pedido de pessoa inativa ou o tratam sem quebrar. A **aplicação** cancela adesões, dependentes e trajetos `PENDENTE` na efetivação do desligamento (ponto de extensão novo, T18), sem evento próprio; `decisaoRh` de férias aprovadas pelo RH e depois canceladas é mantida. Readmissão: o histórico mostra as férias do vínculo anterior; o saldo casa períodos e férias pelas datas exatas.
14. **Pagamento e painéis (L:E.4.11, E.4.12):** DELETE com `ano` ou `mes` fora da faixa = 404; PUT com `ano` fora de 2020 a 2100 ou data fora do mês = 400; a exceção em fim de semana vale como está; o calendário aceita o que `DoMes` devolve (um só "antecipado"); data de ano anterior no calendário é coerente. `resumo` de cada pendência: uma frase simples por tipo sobre o item mais antigo (aceita `null`); `desdeEmUtc` de férias = aprovação do gestor quando há, senão a solicitação (sugestão aceita: a resposta da pessoa; passou ao RH porque o gestor saiu: a solicitação); admissão conta só quem tem o formulário de admissão atribuído e não respondeu e começa em até 7 dias; `treinamentosEmAndamento.total` = o número da lista de treinamentos do RH; fila de pendências montada **em memória** e paginada (volume de dezenas; limite escrito); `autor` das alterações novo ("Nome (RH)", "Nome (gestor)", "Sistema"); `total` de presença do painel soma as cinco situações do contrato (não usa `PresencaContagens.Total`, que soma seis). Os painéis isolam a falha **por fonte** (como os contadores): uma fonte que falha vira lista vazia e `Warning` no log, nunca 500 do painel.
15. **Textos de erro sem exemplo** (L:B.1: `FUNCIONARIO_INATIVO` e `FERIAS_SEM_PERIODO_AQUISITIVO` de férias, mensagens por regra): o executor escreve, registra `Ruling:` e o teste de mensagens tem lista de exceções. **Texto dos 409 de pedido da fatia 7 é o do contrato** (não o do ponto): catálogo `MensagensDaGestao` próprio. Ordem dos erros do RH: etapa (409) antes de quem decide (422), como no ponto.

## Review Focus

1. **Saldo em corrida:** dois pedidos simultâneos, pedido × registro do RH e aceite × pedido nunca reservam mais de 30 dias e nunca dão 500 (T6, T8, T10).
2. **Pontes silenciosas:** ligar "gestor ausente", trocar, tirar ou desligar o gestor e trocar a pessoa de setor movem os pedidos de férias (e só os certos: `SUGESTAO_DE_DATA` não muda); o indicador da ficha é igual ao saldo (T12).
3. **Privacidade:** dependente (menor), trajeto e endereço nunca em rota de gestor, painel, log de auditoria nem mensagem de erro; o mesmo 404 para o gestor em pedido próprio, alheio e de outro setor (T7, T10, T16, T17, T23).
4. **Decisão dupla e `CHECK` em cadeia:** dois cliques, gestor × ausência, confirmar × recusar, aceitar × expirar = um efeito, um e-mail, o outro 409; nenhum caminho de estado cai no `CHECK` do banco como 500; `OBSERVACAO` preservada (T7, T8, T9, T13).
5. **Painéis:** uma fonte que falha não derruba o painel; número de consultas constante (sem N+1); nada que o gestor não veja em `GET /api/gestor/equipe` (T22, T23, T24).

---

## Task 1: Mapeamento de `FERIAS` e sonda dos triggers (7a)

**Files:** Create `Domain/Ferias/{Ferias,StatusDasFerias,ResultadoDoGestor,RespostaDaSugestao}.cs`, `Infrastructure/Persistencia/Configuracoes/FeriasConfiguracao.cs`, `DbSet<Ferias>` no `AppDbContext`; Modify `tests/.../Persistencia/tabelas-sem-mapeamento.txt` (sai `FERIAS`). Test `tests/Persistencia/MapeamentoDasFeriasTests.cs`.

**Interfaces:** Produces a entidade (sem `[Auditado]`: eventos à mão) com as 24 colunas de L:D.1, nulabilidade igual ao banco, instantes com `ConversoresDeTempo.Brasil`, `DATA_SOLICITACAO`/`DATA_ANALISE` anuláveis, nenhum padrão do banco declarado; enums lidos em texto `SNAKE_UPPER` tolerante (`EnumComoTextoMaiusculoTolerante`).

- [ ] **Step 1: Testes (RED):** `MapeamentoContraBancoTests` e `ConformidadeDoModeloTests` verdes com `FERIAS` fora do arquivo; sonda que grava pelo EF um registro `APROVADA` sem gestor (o padrão `SOLICITADA` não vence), prova as recusas de trigger de L:D.2 com o texto exato (inativo; início antes do fim do período; transição inválida; sem gestor ao aprovar; gestor analisando as próprias férias), as transições aceitas, o cancelamento pela inativação (e o sufixo na `OBSERVACAO`; `EM_GOZO` não) com datas escolhidas longe de "hoje" real, e que datas gravadas em -03:00 passam no `CHECK` de análise.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: entidade e mapeamento das ferias (fatia 7)`.

## Task 2: Regras puras, períodos aquisitivos e saldo (7a)

**Files:** Create `Application/Ferias/Regras/{PeriodosAquisitivos,SaldoDeFerias}.cs`. Test `Application.Tests/Ferias/*`.

**Interfaces:** Produces `PeriodosAquisitivos.Calcular(DateOnly admissao, DateOnly hoje, DateOnly? desligamento)` → períodos (`inicio` = admissão + n anos, `fim` = próximo início menos um dia, `prazoParaTirar` = fim + 12 meses, situação `EM_AQUISICAO`/`COM_SALDO`/`SEM_SALDO`, `mesesTrabalhados`/`disponivelAPartirDe` só em aquisição); `SaldoDeFerias.De(IReadOnlyList<PeriodoAquisitivo>, IReadOnlyList<FeriasParaSaldo>)` com `diasDeDireito` 30, `diasTirados` (gozo + abono de `APROVADA`/`EM_GOZO`/`CONCLUIDA`), `diasEmPedido` (`COM_O_GESTOR`/`COM_O_RH`), `diasDisponiveis` (0 em aquisição), `periodoDoSaldo`, `proximasFerias`, `pedidosEmAnalise` (conta as três situações em análise), e a forma em lote `SaldoDeFerias.DeVarias(...)`.

- [ ] **Step 1: Testes (RED, alvo de mutação):** os exemplos do contrato (Bruno `yaml:56-114`, Diego `:853-899`), admissão em 29/02, menos de um ano, pessoa desligada, virada de ano, readmissão (férias antigas sem período casado não entram no saldo), `SUGESTAO_DE_DATA` não reserva, `REJEITADA`/`CANCELADA` não contam, o lote dá o mesmo que o cálculo individual para cada pessoa.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: periodos aquisitivos e saldo de ferias (regras puras)`.

## Task 3: Regras puras, as oito regras e a conferência (7a)

**Files:** Create `Application/Ferias/Regras/{ConferenciaDeFerias,MensagensDasRegrasDeFerias,EscolhaDoPeriodo}.cs`. Test `Application.Tests/Ferias/*`.

**Interfaces:** Consumes T2. Produces `ConferenciaDeFerias.Conferir(ComandoDeFerias comando, ContextoDaConferencia contexto, ModoDaConferencia modo)` → `FeriasConferencia` (`totalDias`, `diasQueSobram` (pode ser negativo), `podeEnviar`, `pagamentoAte` = início menos 2 dias corridos, `null` com datas inválidas, `regras[]` com as oito, cada uma `atendida` + `mensagem`); `ModoDaConferencia` = `Pessoa` (todas), `Rh` e `AceiteDaSugestao` e `SugestaoDoGestor` (sem `ANTECEDENCIA_DE_30_DIAS`); `EscolhaDoPeriodo.Escolher(...)` (Rulings 2 e 4); `MensagensDasRegrasDeFerias` (texto por regra e por modo: pessoa × RH).

- [ ] **Step 1: Testes (RED, tabela por regra com as bordas):** 4, 5, 30 e 31 dias; abono 0, 1, 10, 11 e a combinação incoerente; antecedência 29 e 30 dias; véspera, antevéspera e 3 dias antes de feriado e de sábado, início em feriado/sábado/domingo; três e quatro partes; o caso do Bruno para a parte de 14 dias (e mais três linhas: 2 partes de 6 e saldo 18; 1 parte de 14 e saldo 16; saldo todo usado sem parte de 14); sobreposição de um dia com cada status que conta e que não conta; `diasQueSobram` negativo; `pagamentoAte`; a lista sem antecedência nos três modos; os exemplos de mensagem do contrato (`:385-386`, `:1476-1477`).
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: as oito regras de ferias e a conferencia (regras puras)`.

## Task 4: Regras puras, situação, etapa, decisões e `podeDecidir` (7a)

**Files:** Create `Application/Ferias/Regras/{SituacaoDoPedidoDeFerias,MapeadorDoPedidoDeFerias,RegrasDeDecisaoDeFerias}.cs`. Test `Application.Tests/Ferias/*`.

**Interfaces:** Produces `SituacaoDoPedidoDeFerias.De(linha, hoje)` (tabela L:D.3: as oito `FeriasSituacao` e a `etapa`), o mapeamento para `decisaoGestor`/`decisaoRh`/`sugestao` (L:D.3, inclusive `expiraEm` do Ruling 7 e `decididaPor` nulo na etapa automática), `RegrasDeDecisaoDeFerias.PodeDecidir(...)` + `MotivoNaoPodeDecidir` com os cinco textos já em `RegrasDeDecisaoDoPedido` e o sexto "Esperando a resposta de {nome} à sugestão de outra data." (decidir se os tipos sobem para `App/Comum` ou se copia: ledger).

- [ ] **Step 1: Testes (RED):** cada linha da tabela de L:D.3; as quatro combinações de `podeDecidir` por perfil; os seis textos; `expiraEm` nas duas pontas (5 dias e 2 dias antes); `decisaoRh` mantida em `CANCELADA` com `ID_FUNCIONARIO_RH`; sufixo do trigger tirado da `observacao`.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: situacao, etapa e decisoes das ferias (regras puras)`.

## Task 5: Erros da fatia 7 (7a)

**Files:** Create `Application/Gestao/Mensagens/{MensagensDaGestao,RegrasDoBancoDaGestao}.cs`, `Infrastructure/Gestao/TradutorDeRecusaDaGestao.cs`; Modify `Api/Configuracao/TextosDeErro.cs`. Test `Contrato/MensagensDaGestaoTests.cs`, `Gestao/TradutorDeRecusaDaGestaoTests.cs`.

**Interfaces:** Produces uma propriedade por código de L:B.1 (texto exato do contrato; os "sem exemplo" escritos pelo executor, Ruling 15), fábrica `FeriasRegrasNaoAtendidas(variante, regras)` com `regras[]` como extensão (três variantes de título: pedir/aceitar, sugerir, registrar), `FuncionarioInativoDeFerias` (com `regraDoBanco`), `PedidoProprio` e `PedidoRhForaDeGestores` (com `regraDoBanco`), o tradutor (45000 pela mensagem; 1062 por chave: adesão por pessoa e benefício, exceção por ano e mês; CHECKs → 400 ou o 422 certo sem copiar valor).

- [ ] **Step 1: Testes (RED):** cada mensagem confere com o `openapi.yaml` montado (lista de exceções para os textos sem exemplo); `regras[]` e `regraDoBanco` saem no JSON; o tradutor mapeia cada recusa de L:B.2. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: erros da gestao (catalogo contra o contrato e tradutor de recusa do banco)`.

## Task 6: Minhas férias, conferência e pedir (#1, #3, #2) (7a)

**Files:** Create `Application/Ferias/{IFeriasRepositorio,IConsultaDeFerias,CasosDeUsoDasMinhasFerias}.cs`, `Infrastructure/Ferias/{FeriasRepositorio,ConsultaDeFerias}.cs`, `Api/Ferias/{FeriasEuEndpoints,FeriasDtos}.cs`; registro em `DependencyInjection`. Test `tests/Ferias/MinhasFeriasTests.cs`.

**Interfaces:** Consumes T2 a T5, `IGestorAusente.EtapaInicialDoPedidoAsync`, `IOperacoesComTrava`. Produces `obterMinhasFerias`, `conferirPedidoDeFerias`, `pedirFerias`; `IFeriasRepositorio.CriarAsync` (INSERT com as colunas da etapa de nascimento: com gestor `ID_GESTOR`; gestor ausente `ID_GESTOR` + `APROVOU` + data + `ETAPA_GESTOR_AUTOMATICA`; sem gestor tudo nulo), `IConsultaDeFerias.PeriodosEPedidosDaPessoaAsync(ids)` em lote (usado também por T10, T12, T22, T23).

- [ ] **Step 1: Testes (RED):** `resumo`/`periodosAquisitivos`/`historico` dos exemplos do contrato (histórico inclui cancelados e recusados); `gestorQueAnalisa` `null` só com o RH; nasce com gestor, sem gestor (setor sem gestor, gestor desligado, pessoa gestora) e com gestor ausente (automática, **sem e-mail**); nenhum e-mail ao criar; 422 `FERIAS_REGRAS_NAO_ATENDIDAS` com a lista, 422 `FUNCIONARIO_INATIVO`, 422 `FERIAS_SEM_PERIODO_AQUISITIVO`; a conferência é sempre 200 e **não grava**; **dois pedidos em paralelo** (decorador com barreira) nunca estouram o saldo e nenhum dá 500; evento de auditoria `FERIAS` (pedido, período, datas); a mensagem fica em `observacao` e nunca é reescrita.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: minhas ferias, conferencia e pedir (fatia 7)`.

## Task 7: Gestor, lista, aprovar, recusar e sugerir (#6, #8, #9, #10) (7a)

**Files:** Create `Application/Ferias/CasosDeUsoDasFeriasDaEquipe.cs`, `Api/Ferias/FeriasGestorEndpoints.cs`, `Infrastructure/Emails/ResultadoFeriasRenderizador.cs` (tipo `FERIAS_DECIDIDAS`); Modify `FeriasRepositorio` (decisões). Test `tests/Ferias/FeriasDoGestorTests.cs`.

**Interfaces:** Produces `listarFeriasDaEquipe` (padrão `COM_O_GESTOR`; ordem do mais antigo ao mais novo com desempate por id; `conflitos` e `equipeDisponivel` em lote), `aprovarFeriasComoGestor` (UPDATE condicional: `SOLICITADA`, `ID_GESTOR` = o dele, resultado nulo → `APROVOU` + data; **não toca `OBSERVACAO`**), `recusarFeriasComoGestor` (um UPDATE: `REJEITADA` + `RECUSOU` + data + `MOTIVO_RECUSA`, motivo com `Trim` de 5 a 500), `sugerirOutraDataDasFerias` (UPDATE: `SUGERIU_DATA` + data + as duas datas); e-mail por decisão na mesma transação com chave `FERIAS_DECIDIDAS:{id}:{decisao}`.

- [ ] **Step 1: Testes (RED):** o **mesmo 404** para pedido inexistente, próprio do gestor, de outro gestor e de outro setor; 409 `PEDIDO_ETAPA_INCORRETA` (já no RH, em sugestão) e `PEDIDO_JA_DECIDIDO` com os textos do contrato; motivo de 4, 5, 500 e 501 caracteres e só de espaços (400, nunca o CHECK); sugestão com as regras do Ruling 6 (422 com `regras[]`, 400 sem datas coerentes/passado/antes do fim do período); `conflitos` e `equipeDisponivel` dos exemplos (`null` sem setor; vazio quando já decidido); um e-mail por decisão com a chave certa e `HtmlEncode` (motivo com `<`); nada na fila sem e-mail corporativo; **decisão dupla** (dois cliques; aprovar × sugerir) = um efeito, um e-mail, o outro 409; `OBSERVACAO` preservada após aprovar.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: ferias na etapa do gestor (lista, aprovar, recusar, sugerir)`.

## Task 8: Resposta à sugestão de data (#4, #5) (7a)

**Files:** Modify `CasosDeUsoDasMinhasFerias`, `FeriasEuEndpoints`, `FeriasRepositorio`. Test `tests/Ferias/RespostaASugestaoTests.cs`.

**Interfaces:** Produces `aceitarSugestaoDeDataDasFerias` (trava a pessoa; reconfere em modo `AceiteDaSugestao`; UPDATE condicional copia as datas sugeridas para `DATA_INICIO`/`DATA_FIM`, grava `ACEITOU` + data, **mantém** as sugeridas; a data original fica só no evento de auditoria), `recusarSugestaoDeDataDasFerias` (`CANCELADA` + `RECUSOU` + data), a função compartilhada `ExpirarSePreciso` (Ruling 7) usada também pela tarefa (T13).

- [ ] **Step 1: Testes (RED):** aceitar troca as datas, vira `COM_O_RH` com `decisaoGestor` `APROVADA` (`decididaPor` = quem sugeriu, `decididaEmUtc` = a resposta) e a reserva passa a contar; 422 quando já não cabe (outro pedido ocupou o saldo); recusar cancela com `respostaDaPessoa: RECUSOU` e as datas originais; pedido de outra pessoa = 404; fora de `SUGESTAO_DE_DATA` = 409 `PEDIDO_ETAPA_INCORRETA`; terminado = 409 `PEDIDO_JA_DECIDIDO`; depois de `expiraEm` = expira e 409 `PEDIDO_JA_DECIDIDO`; **sem e-mail** nos dois; aceitar × expirar e aceitar × pedido novo em paralelo sem 500 nem estouro de saldo.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: resposta da pessoa a sugestao de data das ferias`.

## Task 9: RH, lista, confirmar e recusar (#12, #13, #14) (7a)

**Files:** Create `Application/Ferias/CasosDeUsoDasFeriasParaRh.cs`, `Api/Ferias/FeriasRhEndpoints.cs`; Modify `FeriasRepositorio`. Test `tests/Ferias/FeriasDoRhTests.cs`.

**Interfaces:** Consumes `RegrasDeDecisaoDeFerias`, o molde `CasosDeUsoDosPedidosParaRh` do ponto. Produces `listarFeriasParaORh` (padrão `COM_O_RH`; `busca` pelo nome de quem pediu, `setorId`, ordem do mais antigo), `confirmarFeriasComoRh` (só em `COM_O_RH`; vira `APROVADA` + `ID_FUNCIONARIO_RH` + `DATA_CONFIRMACAO_RH` + `ID_GESTOR = COALESCE(ID_GESTOR, linha de GESTORES de quem decide)`), `recusarFeriasComoRh` (`REJEITADA`, inclusive o que o gestor aprovou; motivo em `decisaoRh.observacao`); e-mail por decisão.

- [ ] **Step 1: Testes (RED):** confirmar com o pedido no gestor ou em `SUGESTAO_DE_DATA` = 409 `PEDIDO_ETAPA_INCORRETA` (antes de qualquer 422); já decidido = 409 `PEDIDO_JA_DECIDIDO`; pedido sem etapa: dono da conta decide = 422 `PEDIDO_PROPRIO` (vence), sem linha em `GESTORES` = 422 `PEDIDO_RH_FORA_DE_GESTORES`, com linha = 200 com `ID_GESTOR` gravado; o RH confirma o próprio pedido que o gestor aprovou (200); recusa do que o gestor aprovou (`decisaoGestor` `APROVADA` mantida); `busca`/`setorId`/filtro de situação; `conflitos`/`equipeDisponivel` pelo setor de quem pede; e-mail por decisão; confirmar × recusar em paralelo = um efeito.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: ferias na etapa do RH (lista, confirmar, recusar)`.

## Task 10: Fichas, registro direto e conferência do RH (#11, #15, #16, #17) (7a)

**Files:** Create `Api/Ferias/FeriasFichaEndpoints.cs` (registro por `operationId` nos prefixos compartilhados); Modify `CasosDeUsoDasFeriasDaEquipe`, `CasosDeUsoDasFeriasParaRh`, `FeriasRepositorio`. Test `tests/Ferias/FichaERegistroDeFeriasTests.cs`.

**Interfaces:** Produces `obterFeriasDaPessoaDaEquipe` (gestor: tudo, 404 para outro setor e para quem é gestor), `obterFeriasDaFichaDoFuncionario` (RH: a lista **sem** `COM_O_GESTOR` e `SUGESTAO_DE_DATA`, mas `resumo.pedidosEmAnalise` os conta), `registrarFeriasPeloRh` (trava a pessoa; INSERT `APROVADA`, `etapa: CONCLUIDA`, sem gestor e sem RH; modo `Rh`; pode ser no passado; recalcula o banco de horas dos dias passados, Ruling 11), `conferirRegistroDeFerias` (sem antecedência).

- [ ] **Step 1: Testes (RED):** as duas fichas dos exemplos; registro no passado, sem antecedência, sem e-mail, com `decisaoGestor` e `decisaoRh` `null`; inativo = 422 `FUNCIONARIO_INATIVO`; sem período que termine antes do início = 422 (Ruling 4); registro × pedido em paralelo sem estourar o saldo; o dia passado já consolidado como falta é recalculado (sonda com o banco de horas) e a falha do recálculo não desfaz o registro; abono incoerente = regra, não 400; nunca dependente, trajeto ou endereço na ficha do gestor.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: fichas de ferias, registro direto e conferencia do RH`.

## Task 11: Calendário da equipe (#7) (7a)

**Files:** Create `Application/Ferias/Regras/CalendarioDeFerias.cs`, `Api/Ferias/FeriasCalendarioEndpoint.cs`. Test `Application.Tests/Ferias/CalendarioDeFeriasTests.cs`, `tests/Ferias/CalendarioDeFeriasApiTests.cs`.

**Interfaces:** Produces `obterCalendarioDeFeriasDaEquipe` (`inicio` ajustado para a segunda da semana, `semanas` 1 a 26 padrão 13, só pessoas com férias na janela; itens `APROVADA` (inclui em gozo e concluída), `COM_O_RH` e `AGUARDANDO_VOCE` = `COM_O_GESTOR`; `SUGESTAO_DE_DATA` fora).

- [ ] **Step 1: Testes (RED):** segunda da semana de um domingo e de uma segunda; 1 e 26 semanas (0 e 27 = 400); itens que encostam na borda (último dia = primeiro da janela); as três situações e o fora; pessoa de outro setor e o próprio gestor não aparecem; janela sem férias = lista vazia. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: calendario de ferias da equipe`.

## Task 12: Pontes das férias (7a)

**Files:** Create `Infrastructure/Ferias/{PedidosParadosNoGestorDasFerias,FonteDoIndicadorDeFerias,FonteDeFeriasParaOGestor}.cs`; Modify registro em `DependencyInjection`, `Infrastructure/Colaboradores/*` (troca de setor, Ruling 8), `tests/Colaboradores/PedidosParadosNoGestorRegistroTests.cs`, `IndicadoresDaFichaTests`, `IndicadoresDaEquipeTests`, `ContadoresDoMenuTests`. Test `tests/Ferias/PontesDasFeriasTests.cs`.

**Interfaces:** Produces `IPedidosParadosNoGestorDaFatia` de férias (molde `PedidosParadosNoGestorDoPonto`: filtra `RESULTADO_GESTOR IS NULL`, `SUGESTAO_DE_DATA` não muda; aprovar os parados da ausência com `ETAPA_GESTOR_AUTOMATICA`, passar ao RH nos outros casos), `IFonteDeIndicadorDaFicha` de `FeriasDisponiveisDias` (mesmo cálculo de `resumo.diasDisponiveis`, em lote), `IFonteDeContadorDoMenu` de `PendenciasDaEquipe` (somam com a do ponto; `SUGESTAO_DE_DATA` não conta).

- [ ] **Step 1: Testes (RED):** ligar a ausência aprova os parados e devolve a contagem em `pedidosAprovadosAgora.ferias`; trocar, tirar e desligar o gestor passa ao RH; `SUGESTAO_DE_DATA` não muda; trocar a pessoa de setor passa os parados dela ao RH (férias e ponto); setor que ganha gestor depois: o que nasceu no RH continua no RH; o indicador da ficha (RH e gestor) é igual a `resumo.diasDisponiveis`; a bolinha do gestor soma férias e ponto; **atualizar** os testes antigos de L:H.3 (`Assert.Single` vira as duas portas; `IndicadoresDaFichaTests` remove as fontes reais onde afirma "sem fonte"; `IndicadoresDaEquipeTests` ganha o caso com a fonte real).
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: pontes das ferias (pedidos parados, indicador da ficha e contador do gestor)`.

## Task 13: Tarefa agendada de férias (7a)

**Files:** Create `Infrastructure/Ferias/AtualizarFeriasTarefa.cs` (molde `IncluirAdmitidosNoTreinamentoTarefa`: chave `ATUALIZAR_FERIAS:dia=AAAA-MM-DD`, com `RecuperarAsync`); Modify registro. Test `tests/Ferias/AtualizarFeriasTarefaTests.cs`.

**Interfaces:** Produces quatro ações, cada uma um UPDATE condicional na transição que o trigger aceita: `APROVADA` com `DATA_INICIO <= hoje <= DATA_FIM` → `EM_GOZO`; `APROVADA`/`EM_GOZO` com `DATA_FIM < hoje` → `CONCLUIDA` (inclusive `APROVADA` direto, para recuperar dias parados); `SOLICITADA` com `DATA_INICIO < hoje` e sem sugestão pendente → `CANCELADA`; sugestão pendente vencida (`ExpirarSePreciso`, T8) → `CANCELADA` + `EXPIROU`; eventos como Sistema; nenhum e-mail.

- [ ] **Step 1: Testes (RED):** as quatro ações; rodar duas vezes não muda nada; API parada por dias recupera tudo numa execução; só transições aceitas (nada em `CANCELADA`/`REJEITADA`/`CONCLUIDA`); pedido em `SUGESTAO_DE_DATA` cujo início original passou **não** é cancelado pela regra do vencido; a contagem de `ExecutorDeTarefasTests` sobe para 21 tarefas; fila de e-mails vazia. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: tarefa diaria de ferias (entrar em gozo, concluir, cancelar vencidas e expirar sugestoes)`.

**Fim do 7a: mutação das tarefas T2 a T13** (spec `gerar-spec-f7-7a.mjs`, 8 workers; um teste por sobrevivente real; equivalentes como `Ruling:`), `dotnet format`, README seção "Férias (fatia 7)" com as perguntas ao dono.

## Task 14: Mapeamento de adesão, dependentes e trajetos (7b)

**Files:** Modify `Domain/Beneficios/BeneficioDoFuncionario.cs` (seis colunas: `DATA_PEDIDO`, `DATA_INICIO_DESEJADA`, `AUTORIZA_DESCONTO_FOLHA`, `ID_FUNCIONARIO_RH`, `DATA_RESPOSTA`, `MOTIVO_RECUSA`); Create `Domain/Beneficios/{Dependente,TrajetoDeValeTransporte}.cs`, configurações, `DbSet`s; Modify `tabelas-sem-mapeamento.txt` (saem `DEPENDENTES` e `TRAJETOS_VALE_TRANSPORTE`). Test `tests/Persistencia/MapeamentoDosBeneficiosTests.cs`.

**Interfaces:** Produces as entidades com `ORIGEM`/`DESTINO`/`LINHAS_MEIOS` e o nascimento marcados sensíveis (`AuditoriaAtributos`), `DATA_PEDIDO` gravada pela aplicação, `BENEFICIO_ATIVO` e `STATUS_PEDIDO` sem padrão declarado.

- [ ] **Step 1: Testes (RED):** conformidade verde; o pedido nasce com `BENEFICIO_ATIVO = FALSE`; as recusas dos `CHECK` de L:B.2 (reabrir sem limpar resposta e data juntas, recusa sem motivo, resposta juntas); a ficha da fatia 1 continua gravando como antes (suíte de colaboradores). **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: mapeamento da adesao, dependentes e trajetos de beneficios`.

## Task 15: Regras puras dos benefícios (7b)

**Files:** Create `Application/Beneficios/Adesoes/Regras/{ListasDoBeneficio,TituloDoPedido,RespostaPrevista,DependentesCadastrados,ValidacaoDoPedidoDeBeneficio}.cs`. Test `Application.Tests/Beneficios/*`.

**Interfaces:** Produces `ListasDoBeneficio.Montar(...)` (`beneficios` ativos e aprovados, `disponiveis` opcionais que a pessoa não tem e não pede, com `ultimoPedido` só `RECUSADO`/`CANCELADO`; aprovado inativo volta com `ultimoPedido` `null`), `TituloDoPedido.De(tipo, nomeDoBeneficio)` ("Adesão ao plano odontológico": minúscula só na primeira letra e as três preposições fixas do contrato), `RespostaPrevista.De(pedidoEm, calendario)` (5 dias úteis, feriados da empresa), `DependentesCadastrados.SemRepetir(...)`, validações de tamanho (nome 150, parentesco 30, origem/destino 200, linhas 255, até 10 dependentes, nascimento não futuro, início desejado não passado, motivo `Trim` 1 a 500).

- [ ] **Step 1: Testes (RED, alvo de mutação):** os três exemplos de `respostaPrevistaAte` (`yaml:1567-1568`, `:1619`); fim de semana e feriado no meio; o título para cada tipo; as quatro listas com recusado, cancelado, aprovado inativo e pendente; "sem repetir" ignorando caixa. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: regras puras dos beneficios (listas, titulo, resposta prevista, validacao)`.

## Task 16: Meus benefícios e os três pedidos (#18 a #21) (7b)

**Files:** Create `Application/Beneficios/Adesoes/{IAdesoesRepositorio,CasosDeUsoDasMinhasAdesoes}.cs`, `Infrastructure/Beneficios/{AdesoesRepositorio,ConsultaDeAdesoes}.cs`, `Api/Beneficios/BeneficiosEuEndpoints.cs`. Test `tests/Beneficios/MinhasAdesoesTests.cs`.

**Interfaces:** Produces `obterMeusBeneficios`, `pedirAdesaoABeneficio` (INSERT ou UPDATE que reabre a mesma linha limpando resposta e motivo juntos e cancelando filhos antigos; a corrida de dois pedidos novos dá 1062 → 409 `BENEFICIO_JA_ATIVO_OU_PENDENTE`), `pedirInclusaoDeDependente`, `pedirAtualizacaoDeTrajeto` (adesão ativa e aprovada da própria pessoa; a de outra pessoa = 404).

- [ ] **Step 1: Testes (RED):** os quatro 422 (`BENEFICIO_NAO_OPCIONAL`, sem dependentes, sem trajeto, inativo) e os dois 409 (já ativo ou pendente; adesão não ativa); reabrir linha recusada (autorização anterior no log, filhos antigos `CANCELADO`, trajeto pendente anterior cancelado); adesão de outra pessoa = 404; `dependentesCadastrados`; **nenhum e-mail**; **origem, destino e nascimento fora do log** (evento "campo mudou" sem valor); nada de dependente em rota de gestor (`GET /api/gestor/equipe/{id}` não ganha campo); dois pedidos iguais em paralelo = um 201 e um 409.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: meus beneficios e pedidos de adesao, dependente e trajeto`.

## Task 17: RH, lista e as seis decisões (#22 a #28) (7b)

**Files:** Create `Application/Beneficios/Adesoes/CasosDeUsoDasAdesoesParaRh.cs`, `Api/Beneficios/BeneficiosRhEndpoints.cs`; Modify `AdesoesRepositorio`. Test `tests/Beneficios/AdesoesParaRhTests.cs`.

**Interfaces:** Produces `listarPedidosDeBeneficio` (uma linha por adesão; `situacao` padrão `PENDENTE` inclui aprovada com filho pendente; `busca` por nome, `beneficioId`; ordem pela menor data pendente), `aprovarAdesaoABeneficio` (aprova **junto** os `PENDENTE` da adesão: `APROVADO` + `BENEFICIO_ATIVO = TRUE` + quem e quando; inativo = 422 pela aplicação, o banco não barra), `recusarAdesaoABeneficio` (`RECUSADO`; filhos `PENDENTE` viram `CANCELADO` sem resposta), aprovar/recusar dependente e trajeto (adesão tem de estar `APROVADO`; de outra adesão = 404; o trajeto aprovado passa a ser o em vigor).

- [ ] **Step 1: Testes (RED):** aprovar junto (exemplos `:1785-1789`); recusar cancela filhos (`:1833-1835`); dependente e trajeto de outra adesão = 404; os dois 409 na ordem do contrato (`BENEFICIO_ADESAO_NAO_ATIVA` antes de `BENEFICIO_PEDIDO_JA_DECIDIDO`); inativo = 422; motivo só de espaços = 400; decidir duas vezes e em paralelo = um efeito; o RH decide o próprio pedido (200); nenhum e-mail; a lista com adesão dada direto (situação `APROVADO`) e o pedido que só tem filho pendente.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: decisoes do RH sobre adesoes, dependentes e trajetos`.

## Task 18: Pontes dos benefícios (7b)

**Files:** Modify `Infrastructure/Colaboradores/DesligamentoDeFuncionarios.cs` (ponto de extensão novo `IAoEfetivarDesligamento`, ou o nome que o vizinho pedir), `App/Colaboradores/FuncionarioDtos.cs` + a consulta da ficha do RH (`quantidadeDependentes`), `Domain/Beneficios/BeneficioDoFuncionario.cs` (quem e quando na concessão direta); Create `Infrastructure/Beneficios/CancelaPedidosDeBeneficioAoDesligar.cs`. Test `tests/Beneficios/PontesDosBeneficiosTests.cs`.

- [ ] **Step 1: Testes (RED):** inativar cancela adesão, dependente e trajeto `PENDENTE` (sem evento próprio) e eles somem da lista do RH; os testes de desligamento da fatia 1b continuam verdes; a ficha do RH traz `quantidadeDependentes` = dependentes `APROVADO` de todas as adesões ativas (0 sem eles); a concessão direta pela ficha grava `ID_FUNCIONARIO_RH` e `DATA_RESPOSTA` (e continua sem `DATA_PEDIDO`, por isso fora de `pedidos`); editar a ficha com pedido `PENDENTE` na lista de `beneficioIds` e fora dela (L:E.4.10).
- [ ] **Step 2 a 4:** RED, implementar, GREEN, suíte de colaboradores. **Step 5: Commit.** `feat: pontes dos beneficios (cancelar pendentes ao desligar, dependentes na ficha, quem concedeu)`.

**Fim do 7b: mutação das tarefas T15 a T18** (`gerar-spec-f7-7b.mjs`), `dotnet format`, README "Benefícios (fatia 7)".

## Task 19: Mapeamento de `PAGAMENTOS_EXCECOES` (7c)

**Files:** Create `Domain/Pagamento/ExcecaoDePagamento.cs`, configuração, `DbSet`; Modify `Infrastructure/Comunicacao/ProximoPagamento.cs` (troca o SQL cru por entidade) e `tabelas-sem-mapeamento.txt` (sai a tabela). Test `tests/Persistencia/MapeamentoDoPagamentoTests.cs`, `tests/Comunicacao/ProximoPagamentoTests.cs`.

- [ ] **Step 1: Testes (RED):** sonda do tipo `YEAR` do Pomelo (leitura e gravação; 2020 e 2100); `UQ (ANO, MES)` dá 1062; `CHK_DATA_NO_MES`; `ProximoPagamentoTests` continua verde com a leitura nova. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: mapeamento das excecoes de pagamento`.

## Task 20: Exceções e calendário (#30 a #33) (7c)

**Files:** Create `Application/Pagamento/{CasosDeUsoDoPagamentoRh,IExcecoesDePagamentoRepositorio}.cs`, `Infrastructure/Pagamento/ExcecoesDePagamentoRepositorio.cs`, `Api/Pagamento/PagamentoRhEndpoints.cs`. Test `tests/Pagamento/PagamentoRhTests.cs`.

**Interfaces:** Consumes `RegraDoPagamento.DoMes`. Produces `listarExcecoesDePagamento` (sem `ano`: do mês atual em diante; ordem de data; `ano` 2020 a 2100), `obterCalendarioDePagamento` (12 meses; mês sem regra e sem exceção não vem; `ajustes` podem vir dois), `definirExcecaoDePagamento` (PUT que substitui; dois PUT juntos: o 1062 vira "já existe, atualizar"; auditoria `CADASTROS`), `removerExcecaoDePagamento` (204; inexistente e fora da faixa = 404).

- [ ] **Step 1: Testes (RED):** PUT cria e substitui; data fora do mês = 400, `ano` fora da faixa = 400, `mes` fora = 400; dois PUT em paralelo sem 500; DELETE inexistente = 404; calendário com fevereiro (dia 30 e 31, ano bissexto), dois ajustes, exceção em fim de semana (vale como está), data do ano anterior, empresa sem a linha de configuração (só os meses com exceção); evento de auditoria. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: excecoes e calendario de pagamento (RH)`.

## Task 21: Próximo pagamento e a fonte do indicador (#29) (7c)

**Files:** Create `Api/Pagamento/PagamentoEuEndpoints.cs`, `Infrastructure/Pagamento/FonteDoProximoPagamento.cs`; Modify registro, `IndicadoresDaFichaTests`. Test `tests/Pagamento/ProximoPagamentoApiTests.cs`.

**Interfaces:** Produces `obterProximoPagamento` (de hoje em diante, hoje inclusive; exceção vale mais que a regra; sem dia configurado: os dois campos `null`; competência sempre o mês anterior ao do pagamento) e a `IFonteDoProximoPagamento` real (adaptador sobre `IProximoPagamento`).

- [ ] **Step 1: Testes (RED):** hoje é o dia; competência em janeiro (dezembro do ano anterior); sem configuração = `null`; a exceção do mês vence; o indicador `proximoPagamento` da ficha (RH e gestor) é igual a `pagamento.data` da rota; os testes antigos de L:H.3 atualizados. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: proximo pagamento e fonte do indicador da ficha`.

## Task 22: Painel do funcionário (#34) (7d)

**Files:** Create `Application/Painel/{CasoDeUsoDoPainelDoFuncionario,PainelFuncionarioDtos}.cs`, `Api/Painel/PainelFuncionarioEndpoints.cs`. Test `tests/Painel/PainelDoFuncionarioTests.cs`.

**Interfaces:** Consumes T6, T16, T21 (as mesmas consultas, nunca cálculo paralelo). Produces `obterPainelDoFuncionario` (cartões de férias, próximo pagamento e benefícios na ordem do catálogo; `detalhe` com dependentes e trajeto; falha de uma fonte = cartão vazio e `Warning`, nunca 500).

- [ ] **Step 1: Testes (RED):** os três cartões iguais às rotas de origem; ordem do catálogo; `detalhe` com dependentes aprovados e com trajeto em vigor; só o próprio; uma fonte que lança não derruba o painel. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: painel do funcionario`.

## Task 23: Painel do gestor e o contador de férias (#38, #36) (7d)

**Files:** Create `Application/Painel/{CasoDeUsoDoPainelDoGestor,PainelGestorDtos}.cs`, `Infrastructure/Painel/ConsultaDoPainelDoGestor.cs`, `Api/Painel/PainelGestorEndpoints.cs`. Test `tests/Painel/PainelDoGestorTests.cs`.

**Interfaces:** Consumes `IConsultaDaEquipe`, `ISituacaoDeHoje`, `ISaldoDoBancoDeHoras`, T2 em lote, a fila de ajustes do ponto. Produces `obterPainelDaEquipe` (`setor`, `totalPessoas`, `aguardandoAprovacao` (férias = os itens de #6, todos, sem paginação; ajustes = resumo da fatia 3), `avaliacao` (Ruling 12), `pessoas` em ordem alfabética com situação de hoje, banco de horas, saldo de férias e advertências do ano; gestor sem setor: `setor` `null`, zeros e listas vazias).

- [ ] **Step 1: Testes (RED):** gestor sem setor e com equipe vazia; a equipe é a de `GET /api/gestor/equipe`; nenhum CPF, endereço, dependente nem trajeto em nenhum campo; **número de consultas constante** com 3 e com 30 pessoas (contador de comandos do `DbContext`); ordem alfabética com desempate por id; avaliação `NAO_SE_APLICA`/`PENDENTE`/`RESPONDIDA`/`null`; falha de uma fonte isola. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: painel do gestor`.

## Task 24: Painel do RH e as duas listas (#39, #40, #41) (7d)

**Files:** Create `Application/Painel/{CasoDeUsoDoPainelDoRh,FilaDePendenciasDoRh,PainelRhDtos}.cs`, `Infrastructure/Painel/*`, `Api/Painel/PainelRhEndpoints.cs`. Test `tests/Painel/PainelDoRhTests.cs`.

**Interfaces:** Consumes as filas de ponto, férias, atestados, admissão e benefícios (as assinaturas de atestados e treinamentos **não foram lidas no levantamento: ler ao começar**). Produces `obterPainelDoRh` (`totalPendencias` = soma; `pendencias` só com quantidade > 0; `proximosDias` 21 dias, máximo 8; `treinamentosEmAndamento` máximo 5; `presencaAgora`; `ultimasAlteracoes` 4 linhas, `apenasAlteracoes=true`, janela larga), `listarPendenciasDoRh` (uma a uma, do mais antigo ao mais novo, cinco origens, em memória e paginada, filtro `tipo`), `listarPessoasSemRegistroHoje` (só `SEM_REGISTRO`, alfabética; dia sem expediente: vazia).

- [ ] **Step 1: Testes (RED):** soma das pendências e tipos com zero fora; a mesma conta nas três saídas (`quantidade` do #39, itens do #40, contador #37 do ponto); admissão só em até 7 dias e só com formulário atribuído; próximos dias (21 dias, máximo 8, ordem, feriado e aniversário); treinamentos (máximo 5, ordem de liberação); presença em dia útil antes e depois do limite e em dia sem expediente (`total` = cinco); as 4 últimas alterações sem login nem batida e com o `autor` pronto; paginação da fila unida e filtro por tipo; uma fonte que falha vira lista vazia com `Warning`; benefícios contam.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: painel do RH, pendencias e pessoas sem registro`.

## Task 25: Fechamento da fatia 7 (7d)

- [ ] **Step 1:** os 38 `operationId` novos em `OperationIdsNoEscopo`; `STATUS.md` (38 linhas novas, a correção das três de contadores `:32-34` e das observações `:43-45`, `:48`, `:50`, `:63`, `:66`, `:213`) por `inserir_status.py`; matriz de perfis das 41 rotas; README seção "Gestão (fatia 7)" com as perguntas ao dono (as 11 de L:H.1, as hipóteses construídas e o custo se erradas); mutação do 7c+7d (`gerar-spec-f7-7cd.mjs`); `tabelas-sem-mapeamento.txt` sem as quatro tabelas.
- [ ] **Step 2:** `dotnet build back-end -nologo -v q` limpo (`--no-incremental`), `dotnet format back-end --verify-no-changes`, suíte inteira em três ondas (`suite-paralela.sh`+`suite-treinamentos.sh`, depois `suite-formularios.sh`+`suite-comunicacao.sh`, depois `suite-ponto.sh`; criar `suite-gestao.sh` se o 7 ganhar grupos próprios), tudo verde.
- [ ] **Step 3:** revisor fresco (Agent `fable`, só leitura; `prompt-revisor-f6.md` adaptado com o Review Focus acima), **uma** rodada de correção só dos Critical/Important (RED→GREEN), Minors no ledger como `Final: minor (deferred)`; PR + `gh pr merge N --squash --admin`; sincronizar a `main`; atualizar as memórias.
