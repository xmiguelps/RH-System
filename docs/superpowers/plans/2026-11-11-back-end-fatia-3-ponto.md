# Back-end fatia 3 (ponto) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** o funcionário bate o ponto e vê o espelho e o banco de horas; pede ajuste com justificativa (gestor e depois RH decidem); entrega atestado (só o RH valida); o RH corrige batidas, cadastra feriados municipais, exporta para a folha e baixa o espelho em PDF; o banco de horas é consolidado todo dia.

**Architecture:** regras puras (sequência, horas, atraso, situação do dia, etapa do pedido) em `Application/Ponto/Regras`, sem banco e testadas por unidade; casos de uso por família de rota (`/api/eu`, `/api/gestor`, `/api/rh`) sobre repositórios EF; a gravação de batida e a decisão do pedido são transações curtas com trava por pessoa; consolidação e importação de feriados são tarefas do agendador; PDF e CSV saem pelo link temporário e pelo `CsvSeguro` que já existem. O contrato é lei.

**Tech Stack:** .NET 10 / EF Core 9 / Pomelo 9.0.0 / MySQL 8.4; **PDFsharp-MigraDoc 6.2.4** (MIT; a 6.5.0 passou a **AGPL-3.0**: nunca subir a versão) com fonte embutida; sem biblioteca nova para CSV.

**Spec:** `contrato/fontes/30-fatia-3-ponto.yaml` (34 operações), `contrato/CONVENCOES.md`, e o anexo `docs/superpowers/plans/2026-11-11-back-end-fatia-3-ponto-levantamento.md` (**leia as seções A a F dele antes de cada tarefa: ele traz os códigos de erro com os textos exatos, as colunas, os triggers e as armadilhas**). Roadmap: `docs/superpowers/plans/2026-10-28-roadmap-completo.md`, "Fatia 3".

## Global Constraints

- Mesmas da fatia 2 (`2026-11-11-back-end-fatia-2-importacao.md`, "Global Constraints"): .NET 10, EF Core 9 sem migrations, Domain sem dependência, casos de uso como classes simples, **`V001` a `V005` não se editam** (faltando coluna, índice ou tabela: para e pergunta; `V006` em diante é do dono), **contrato é lei** (rota, campo e `codigo` não se inventam), escopo de `ContratoDivergenciaTests` = lista de `operationId` acrescentada no passo que implementa a rota, toda rota declara **todos** os códigos do contrato, `contrato/STATUS.md` ganha a linha da rota, **sem e-mail novo** além do que o contrato manda (o resultado do pedido de ajuste), `front-end/` é do Lucas, código em português sem acento e mensagem ao usuário em português com acento, `TreatWarningsAsErrors` e `dotnet format back-end --verify-no-changes`.
- **Commits e PRs:** mensagens em português (`feat:`, `test:`, `docs:`); **nunca** `Co-Authored-By` nem "Generated with Claude Code"; `git push` a cada tarefa; no fim, PR e `gh pr merge N --squash --admin` (autorizado pelo dono).
- **Tempo:** todo instante dentro da aplicação é UTC (`IRelogio`); as tabelas do grupo guardam -03:00 e o EF converte com `ConversoresDeTempo.Brasil`; o dia do ponto é `date` em -03:00 (`FusoBrasil`). Nada de `DateTime.Now` nem `NOW()` preenchendo coluna que a aplicação conhece (ver decisão 1).
- **Privacidade:** atestado é dado de saúde: o log e a auditoria nunca levam o conteúdo nem o nome do arquivo; cada link de download do atestado grava evento `ATESTADOS`/`BAIXOU` **antes** de existir. Valor de batida, justificativa e nome de pessoa não vão para log.
- **Comandos longos** em segundo plano com `timeout` e vigia; mutação e suíte em paralelo (`C:\Users\User\dev\ferramentas-rh\mutar-em-paralelo.mjs` e `suite-paralela.sh`, até 10 trabalhadores); **não recompilar o repositório principal enquanto grupos de teste rodam**.
- **Controle de mutação:** nas tarefas de regra pura, por tarefa e no projeto `Application.Tests` (rápido); nas de rota, ao fim de cada incremento (3a, 3b, 3c), sobre os arquivos novos do incremento.

## Decisões deste plano (o contrato e o levantamento deixam em aberto)

Escolhas de engenharia; o dono pode mudar. Cada uma vira uma linha `Ruling:` no livro ao ser executada (as marcadas "contrato" só são confirmadas).

1. **Quem preenche as datas.** `DATA_SOLICITACAO`, `DATA_ANALISE`, `DATA_ETAPA_GESTOR`, `DATA_CONFIRMACAO_RH` e `ATESTADOS.DATA_ENVIO` vêm do `IRelogio` (convertido para -03:00 pelo mapeamento), **não** do `NOW()` do trigger nem do `DEFAULT CURRENT_TIMESTAMP`: o relógio falso dos testes (2026-10-07) faria o pedido nascer "depois" de decidido e o `CHECK DATA_ANALISE >= DATA_SOLICITACAO` recusaria. Custo se errado: nenhum em produção (os dois relógios coincidem).
2. **Batida:** o instante é truncado ao segundo **na entidade** (o conversor não trunca); a sequência é conferida **dentro de uma transação com trava por pessoa** (`OperacoesComTrava`, `FOR UPDATE` em `FUNCIONARIOS`, `READ COMMITTED`), porque a chave única só pega o mesmo segundo e duas abas passariam. Criar o dia também corre (1062 em `(ID_FUNCIONARIO, DATA)`): reler. Custo se errado: duas ENTRADAs seguidas gravadas.
3. **Inativo** é conferido pela **aplicação** em batida, pedido e correção (o banco só barra pelo `PONTOS`, e num dia que já existe passa); o token dura 15 min e não reconfere o usuário. Aprovação do RH e atestado de inativo **são permitidos** (o contrato não os recusa); o recálculo do banco de horas **só atualiza ou apaga** a linha existente de um inativo (o trigger recusa INSERT dele).
4. **Janela da jornada que atravessa a meia-noite: 12 horas** (contrato, `x-provisorio`): a próxima batida é SAÍDA do dia anterior se a ENTRADA aberta tem menos de 12 h; o dia do ponto é o da ENTRADA.
5. **Funções do grupo: reimplementadas em C#** (minutos inteiros, segundos descartados); `FN_VERIFICAR_CONSISTENCIA_PONTO` serve de **oráculo em teste** (é a definição de `INCOMPLETO`); `FN_CALCULAR_SALDO_BANCO_HORAS` é **proibida** (o saldo é a soma dos minutos de cada linha: `QUANTIDADE` em horas com 2 casas, convertida com arredondamento ao inteiro).
6. **Quem é gestor** é a regra do código (`IGestorAusente`: linha em `GESTORES` **e** gerencia setor ou é RH), não só "ter linha". **Equipe** é `DaEquipe(equipe)` (ativos do setor, sem o gestor): pedido, atestado e espelho de quem não é da equipe = 404 (atestado em lista = lista vazia). Gestor sem setor: listas vazias, sem erro.
7. **Etapa do pedido** deduzida das colunas (levantamento C.4, **inferido**): `PENDENTE` com `ID_GESTOR` e sem resultado = GESTOR; `PENDENTE` com `ID_GESTOR` nulo = RH sem etapa do gestor; `RESULTADO_GESTOR = 'APROVOU'` = RH. No pedido sem etapa do gestor a decisão final grava em `ID_GESTOR` a linha de `GESTORES` **de quem do RH decide** (o trigger exige) e `PEDIDO_RH_FORA_DE_GESTORES` vale quando ela não existe; `PEDIDO_PROPRIO` vence quando os dois valem.
8. **Atestado:** PDF, JPG ou PNG até **5 MiB**; sem checagem de sobreposição nem de data futura (o contrato não pede); "horas cobertas" de atestado de horas = fim − início, limitadas à carga do dia; vários atestados validados no mesmo dia somam, limitados à carga; atestado de dia inteiro validado em dia com batidas zera a carga e as horas viram crédito. Auditoria: envio `Criou`, validação `Aprovou`, recusa `Recusou`, download `Baixou`, todos em `ATESTADOS`. Custo se errado: o número de horas compensado.
9. **Pedido parado quando a pessoa muda de setor** fica como **dívida e pergunta ao dono** (o gestor antigo deixa de ver o pedido e ninguém decide a etapa dele): não há porta para isso na edição do colaborador e inventar uma excede a fatia. Registrar no relatório final.
10. **Consolidação:** a tarefa `CONSOLIDAR_BANCO_HORAS:dia=AAAA-MM-DD` do dia D roda a partir das **12:00 (-03:00) de D+1** (cobre a janela de 12 h da jornada que atravessa a meia-noite); a recuperação consolida do dia seguinte ao último consolidado até D-1, com horizonte máximo de **60 dias**; `atualizadoAte` é o último dia com tarefa `CONCLUIDA` (a limpeza de 30 dias não atrapalha porque a tarefa é diária); dia `INCOMPLETO` ou com pedido `PENDENTE` **não lança** (e é recalculado quando resolver); saldo zero não tem linha (a linha some se o dia passar a zerar). Falta (dia útil sem batida, sem férias, feriado, folga nem atestado) = DÉBITO "Falta" com a carga do dia.
11. **E-mail do resultado do pedido:** tipo novo `RESULTADO_AJUSTE_PONTO` (chave `...:pedido={id}:etapa=GESTOR|RH`), enfileirado na transação da decisão; **não** na criação, **não** na aprovação automática, **não** na correção do RH; destinatário sem e-mail corporativo é pulado (a fila recusa nulo) e registrado; falha de envio nunca desfaz a decisão (contrato).
12. **Feriados:** a leitura (nacionais, estaduais da UF, municipais do município, `EMPRESA`, só `ATIVO`) entra já na 3a (T3), porque o espelho precisa deles; a importação anual (nacionais por cálculo com Páscoa, estaduais por tabela da UF, sem acento duplicado: dedup por **data + tipo**) e o `REPETIR_FERIADO` ficam na 3c. `EMPRESA_NAO_CONFIGURADA` de `criarFeriado` usa **o texto do ponto** (título "Empresa não configurada."), não o de `IConfiguracaoDaEmpresa.ObterOuFalharAsync`. `FERIADO_NAO_EDITAVEL` (sem texto no contrato): título "Feriado não editável." e detalhe "Só feriados municipais podem ser editados.". Feriado criado, editado ou desativado em data já consolidada **não** recalcula o banco (o contrato não manda).
13. **Bater ponto sem empresa configurada não dá 422** (o contrato do ponto prevê a jornada toda `null`; a 1c dizia o contrário): seguir o ponto. Sem horário não há atraso.
14. **Listas:** `ordenarPor` omitido = mais recente primeiro; com `ordenarPor` e sem `direcao`, **`DESC`** (o contrato do ponto diz DESC; o `MapaDeOrdenacao` da 1c usa ASC: o ponto passa o padrão explícito). `podeDecidir` do gestor = `ID_GESTOR` é o dele e a etapa é a dele.
15. **Exportação para a folha:** uma linha por pessoa **ativa em algum dia do mês** (admitida até o fim do mês e não desligada antes do começo dele), `matrícula` nula vira vazia, nenhum número negativo; usa o `CsvSeguro` (BOM, `;`, CRLF, neutraliza `= + - @`).
16. **PDF:** `PDFsharp-MigraDoc` **6.2.4** com um `IFontResolver` próprio e **uma fonte TTF embutida** (com acento, licença livre; se faltar a de Windows no contêiner Linux, o PDF ainda sai); gravado em `espelhos/` e entregue por link temporário; a fonte de referências de `espelhos/` não referencia nada, então a limpeza de sobras apaga depois de 24 h.
17. **`FERIAS` não ganha entidade** (a fatia 7 é a dona): a consolidação e o espelho leem as férias `APROVADA|EM_GOZO|CONCLUIDA` por `Database.SqlQuery<FeriasDoPeriodo>` (projeção sem entidade).
18. **Aprovação automática** (gestor ausente) não gera evento próprio (o ligar/desligar da ausência já é auditado na 1c); "passar ao RH" (`ID_GESTOR = NULL`) e "aprovar etapa do gestor" por `ExecuteUpdate` levam `// AUDITORIA-OK: motivo` e um evento resumo por chamada.
19. **Dia que o espelho mostra:** de 1º do mês (ou da admissão) até hoje (mês corrente) ou até o fim (mês passado); mês futuro `dias: []`; nenhum dia antes da admissão nem depois do desligamento. **Dia passado em `obterPontoDaEquipe`** usa a equipe de hoje.

## Review Focus

1. **Duplo clique e duas abas:** duas batidas do mesmo tipo em paralelo, a 0,4 s e a 0,6 s, e em abas diferentes: no máximo uma entra, a outra dá 409 com o código certo, nunca 500 nem duas ENTRADAs (T5).
2. **Fuso e meia-noite:** batida às 22:30 e às 00:10 locais caem no dia certo; jornada 22:00 a 06:00; relógio do navegador não conta (o corpo só leva o tipo) (T2, T5, T6).
3. **Tudo ou nada na decisão do RH:** pedido com várias batidas (uma corrigida e uma nova) aplica todas ou nenhuma; sequência inválida depois de aplicar, batida duplicada e pedido já decidido deixam o dia e o pedido intactos (T12).
4. **Quem vê o quê:** gestor tentando pedido, espelho ou atestado de outra equipe, do próprio ou de outro gestor = 404; atestado visto por quem não pode; todo download de atestado auditado antes do link (T11, T15).
5. **Consolidação:** rodar duas vezes não duplica; parada de vários dias recupera; ajuste, correção e atestado recalculam o dia; dia `INCOMPLETO` não lança; o último dia de quem sai fecha antes do desligamento (T17).

---

## Estrutura de arquivos (sob `back-end/`)

- `src/RHSystem.Domain/Ponto/`: `Ponto`, `MarcacaoDePonto`, `PedidoDeAjuste`, `ItemDoPedidoDeAjuste`, `LancamentoDoBancoDeHoras`, `Feriado`, `Atestado` e os enums (`TipoDeMarcacao`, `OrigemDaMarcacao`, `StatusDoPedido`, `EtapaDoPedido`, `SituacaoDoAtestado`, `TipoDeFeriado`).
- `src/RHSystem.Application/Ponto/`: `Regras/` (puras), `Mensagens/MensagensDoPonto.cs`, casos de uso, interfaces de repositório e de consulta, DTOs do contrato.
- `src/RHSystem.Infrastructure/Ponto/`: repositórios, consultas, `TradutorDeRecusaDoPonto`, `RecalculoDoBancoDeHoras`, tarefas, gerador de PDF, renderizador de e-mail; `Persistencia/Configuracoes/*Configuracao.cs`.
- `src/RHSystem.Api/Ponto/`: `PontoEuEndpoints`, `PontoGestorEndpoints`, `PontoRhEndpoints`.
- `tests/RHSystem.Application.Tests/Ponto/` (regras) e `tests/RHSystem.Api.IntegrationTests/Ponto/` (rotas, banco real).

---

# Incremento 3a: bater ponto e espelho

## Task 1: Entidades e mapeamento das tabelas do ponto

**Files:** Create `Domain/Ponto/*`, `Infrastructure/Persistencia/Configuracoes/{Ponto,MarcacaoDePonto,PedidoDeAjuste,ItemDoPedidoDeAjuste,LancamentoDoBancoDeHoras,Feriado,Atestado}Configuracao.cs`; modify `AppDbContext`. Test: `Ponto/MapeamentoDoPontoTests.cs`.

**Interfaces:** Produces as entidades com setters privados e fábricas (`MarcacaoDePonto.Nova(pontoId, instanteUtc, tipo, origem, corrigidaEmUtc?)` **trunca ao segundo**); `PedidoDeAjuste.ChavePendente` e `CHAVE_PENDENTE` mapeadas com `CalculadaNoBanco()`; `ORIGEM`, `ETAPA_GESTOR_AUTOMATICA`, `ATIVO`, `REPETE_TODO_ANO` **sem declarar o padrão do banco**; `DIA_REFERENCIA` sempre preenchido.

- [ ] **Step 1: Testes (RED).** `MapeamentoContraBancoTests` (já existe) passa a cobrir as 7 tabelas (nulabilidade igual à coluna, toda coluna NN sem padrão mapeada); sonda `Grava_e_relê_origem_data_correcao_etapa_automatica_false_e_instante_com_fração` (a fração some: 12:42:00.600 grava 12:42:00 ou 12:42:01? **medir e ledgrar**; a entidade trunca, então grava 12:42:00); `Chave_pendente_nao_e_enviada_no_INSERT`.
- [ ] **Step 2: RED.** `dotnet test back-end/tests/RHSystem.Api.IntegrationTests -m:2 --filter "FullyQualifiedName~Persistencia"`. Expected: falha por entidade ausente.
- [ ] **Step 3: Implementar.** Mapear como o resto (`ConversoresDeTempo.Brasil` nas colunas do grupo; `Utc` nas da aplicação).
- [ ] **Step 4: GREEN + formato.** Mesmo filtro; `dotnet build` sem avisos.
- [ ] **Step 5: Commit.** `feat: entidades e mapeamento das tabelas do ponto (fatia 3)`, `git push`.

## Task 2: Regras puras do dia

**Files:** Create `Application/Ponto/Regras/{SequenciaDoDia,CalculoDoDia,SituacaoDoDiaNoEspelho}.cs`. Test: `tests/RHSystem.Application.Tests/Ponto/*Tests.cs`, `Ponto/ConsistenciaContraAFuncaoTests.cs` (integração, oráculo).

**Interfaces:** Produces (puras, sem banco): `SequenciaDoDia.Periodos(IReadOnlyList<MarcacaoDoDia>) → IReadOnlyList<PeriodoDoDia>` (ENTRADA abre, SAÍDA seguinte fecha; ENTRADA seguida de ENTRADA deixa a primeira sem saída; SAÍDA sem entrada = período com `Entrada: null`); `EhConsistente(...)`; `Rotulo(ordem, tipo)` ("Entrada", "Saída almoço", "Volta", "Saída"; 5ª em diante e tipo que não combina: "Entrada"/"Saída"); `ProximaMarcacao(diaAtual, diaAnterior, agoraUtc)` com a janela de 12 h; `CalculoDoDia.HorasEmMinutos(periodos)` (soma dos fechados, segundos descartados), `Atraso(primeiraEntradaLocal, horarioPadrao, toleranciaMin)` (**só se passa da tolerância; conta inteiro**: 08:14 com horário 08:00 e tolerância 10 → 14; 08:10 → `null`), `Saldo(horas, carga)`; `SituacaoDoDiaNoEspelho.Calcular(entrada)` com a **precedência do contrato** (levantamento B, `PontoDiaSituacao`).

- [ ] **Step 1: Testes (RED).** `Periodos`: 2, 4 e 5 batidas; ENTRADA, ENTRADA; SAÍDA solta; ordem por horário e depois id. `ProximaMarcacao`: dia vazio → ENTRADA; 4 batidas → ENTRADA; meia-noite (ENTRADA 22:00 aberta, agora 01:00 → SAÍDA no dia anterior; aberta há 12 h exatas ou mais → ENTRADA do dia novo). Horas: segundos descartados por período e não no total (09:00:50 → 09:10:10 = 9 min, não 10). Atraso: 08:10 sem atraso, 08:11 com 11. Situação: cada linha da precedência (pedido pendente vence tudo; hoje `EM_ANDAMENTO`; passado quebrado `INCOMPLETO`; folga; feriado; férias; falta compensada; `SEM_REGISTRO` inclusive hoje). Oráculo: gravar sequências ao acaso no banco e comparar `EhConsistente` com `FN_VERIFICAR_CONSISTENCIA_PONTO` (200 casos, semente fixa).
- [ ] **Step 2: RED.** `dotnet test back-end/tests/RHSystem.Application.Tests --filter "FullyQualifiedName~Ponto"`. Expected: não compila.
- [ ] **Step 3: Implementar** as três classes estáticas. **Step 4: GREEN.**
- [ ] **Step 5: Controle de mutação** (~30 mutantes: `<` vs `<=` na janela de 12 h, na tolerância e na consistência; troca de tipo; ordem da precedência; arredondamento). Sobreviventes viram teste ou INFERIDO.
- [ ] **Step 6: Commit.** `feat: regras puras do dia de ponto (fatia 3)`, `git push`.

## Task 3: Calendário da empresa e fonte de fatos do dia

**Files:** Create `Application/Ponto/{ICalendarioDoPonto,IFonteDeFeriados}.cs`, `Infrastructure/Ponto/{CalendarioDoPonto,FonteDeFatosDoDiaDoPonto}.cs`; modify `DependencyInjection`. Test: `Ponto/CalendarioDoPontoTests.cs`, ajuste dos testes da 1b que assumiam "só fim de semana".

**Interfaces:** `ICalendarioDoPonto.DiaAsync(DateOnly) → DiaDoCalendario(EhDiaUtil, NomeDoFeriado?)`, `FeriadosQueValemAsync(de, ate)` (nacional + estadual da UF + municipal do município + `EMPRESA`, `ATIVO`; sem empresa: só nacionais e `EMPRESA`); `FonteDeFatosDoDiaDoPonto : IFonteDeFatosDoDia` substitui `FonteDeFatosDoDiaSemRegistros` (registro posterior vence): batida de entrada hoje, férias `APROVADA|EM_GOZO|CONCLUIDA` (`SqlQuery`), atestado `VALIDADO` de dia inteiro, `SEM_REGISTRO` só depois de entrada + tolerância.

- [ ] **Step 1: Testes (RED).** Dia útil/feriado nacional (07/09/2026 da carga do `V001`), estadual só com a UF da empresa, municipal só com o município, `ATIVO = false` ignorado, sem empresa; `situacaoHoje` de `GET /api/gestor/equipe` e `GET /api/rh/funcionarios` passa a refletir a batida (6 trabalhando, Diego sem registro depois de entrada + tolerância, Larissa de férias, ajustes no `STATUS.md`).
- [ ] **Step 2-4: RED, implementar, GREEN** (`...Ponto`, `...Colaboradores`, `...Equipe`). **Step 5: Commit** `feat: calendario do ponto e fonte de fatos do dia`.

## Task 4: Erros do ponto

**Files:** Create `Application/Ponto/Mensagens/MensagensDoPonto.cs`, `Infrastructure/Ponto/TradutorDeRecusaDoPonto.cs`; modify `ErrosDoBanco`. Test: `Ponto/MensagensDoPontoTests.cs` (contra o yaml), `Ponto/TradutorDeRecusaDoPontoTests.cs`.

**Interfaces:** `MensagensDoPonto` com **todos** os pares (status, título, detalhe, código) da tabela "Textos exatos" do levantamento (A), com `ComoProblema(regraDoBanco?)`; `TradutorDeRecusaDoPonto.Traduzir(Exception, ContextoDaRota) → ProblemaDeApiException?`: 45000 por texto (inativo, "anterior a admissao", `PEDIDO_PROPRIO`, `PEDIDO_RH_FORA_DE_GESTORES`, "Solicitacao analisada nao pode mudar de status" → `PEDIDO_JA_DECIDIDO`) e genérico "Operação recusada." com `regraDoBanco`; 1062 **pelo nome da chave** na mensagem (`UQ_PONTOS_MARCACOES_PONTO_DATAHORA` → `PONTO_BATIDA_DUPLICADA`, `UQ_PONTOS_MANUTENCOES_CHAVE_PENDENTE` → `PONTO_PEDIDO_JA_EM_ANALISE`, `UQ_FERIADOS_*` → `FERIADO_DUPLICADO`, item do pedido → 422 de sequência; **medir** o texto do MySQL e ledgrar); 1451 → `PONTO_MARCACAO_EM_USO`; 1452 → o código de dia/batida inválida.

- [ ] **Step 1: Testes (RED).** `MensagensDoPontoTests` lê `contrato/openapi.yaml` e confere cada `codigo` e `title` (modelo: `MensagensDoApoioTests`); tradutor com exceções reais do MySQL (provocadas no banco de teste, uma por chave). **Step 2-4: RED, implementar, GREEN. Step 5: mutação** (~15, nos mapas de texto e de chave). **Step 6: Commit** `feat: catalogo de erros do ponto e tradutor de recusa do banco`.

## Task 5: Bater ponto e cartão de hoje (#1, #2)

**Files:** Create `Application/Ponto/{ObterPontoHojeCasoDeUso,RegistrarMarcacaoCasoDeUso,IPontoRepositorio,ISaldoDoBancoDeHoras,IRecalculoDoBancoDeHoras}.cs`, `Infrastructure/Ponto/{PontoRepositorio,SaldoDoBancoDeHoras,RecalculoDoBancoDeHorasNulo}.cs`, `Api/Ponto/PontoEuEndpoints.cs`; modify `FormulariosDoContrato` (nenhum), `ContratoDivergenciaTests`, `STATUS.md`, `DependencyInjection`, `Program`. Test: `Ponto/BaterPontoTests.cs`.

**Interfaces:** `RegistrarMarcacaoCasoDeUso.ExecutarAsync(MarcacaoRegistrar corpo) → PontoHoje`: transação com trava por pessoa; confere inativo antes (decisão 3); cria o `PONTOS` do dia se faltar (a carga vem do trigger); calcula o próximo tipo (`SequenciaDoDia.ProximaMarcacao`) e responde 409 `PONTO_SEQUENCIA_MUDOU` se `tipoEsperado` difere; 1062 → `PONTO_BATIDA_DUPLICADA`; evento `PONTO`/`REGISTROU` com IP e origem. `ISaldoDoBancoDeHoras.ObterAsync(funcionarioId)` soma **por linha** (decisão 5); `atualizadoAte` é `null` até a T17. `IRecalculoDoBancoDeHoras.RecalcularDiaAsync(funcionarioId, dia, ct)` (porta; a implementação da T17 substitui a nula).

- [ ] **Step 1: Testes (RED)** (`BaterPontoTests`, banco real, relógio fixo): `Primeira_batida_cria_o_dia_com_a_carga_e_devolve_o_cartao`; `Hora_vem_do_servidor_e_o_corpo_so_diz_o_tipo`; `tipoEsperado_errado_da_409_PONTO_SEQUENCIA_MUDOU`; **`Duas_batidas_iguais_em_paralelo_gravam_uma_so`** (portão `TaskCompletionSource`; 0,4 s e 0,6 s de diferença: no máximo uma, a outra 409 com código; nunca 500); `Duas_abas_com_o_mesmo_tipoEsperado_gravam_uma_so`; `Batida_as_22h30_locais_cai_no_dia_certo` e `As_00h10_fecha_a_entrada_das_22h_do_dia_anterior`; `Funcionario_inativo_com_token_valido_da_422_FUNCIONARIO_INATIVO_com_regraDoBanco`; `Antes_da_admissao_da_422_sem_codigo`; `Feriado_e_fim_de_semana_aceitam_a_batida`; `Sem_empresa_a_jornada_vem_toda_null_e_nao_da_422`; `Auditoria_registra_PONTO_REGISTROU_com_IP_e_sem_o_corpo`; `As_respostas_seguem_o_schema_do_contrato`.
- [ ] **Step 2: RED** (`...Ponto.BaterPontoTests`). **Step 3: Implementar** (decisões 1 a 4, 13). **Step 4: Contrato:** `obterPontoHoje` e `registrarMarcacaoDePonto` no escopo, linhas no `STATUS.md`; rodar `...Contrato` e `...Matriz`. **Step 5: GREEN. Step 6: Commit** `feat: bater ponto e cartao de hoje (registrarMarcacaoDePonto, obterPontoHoje)`.

## Task 6: Espelho do mês (#3, #11, #19) e ponto da equipe (#10)

**Files:** Create `Application/Ponto/{MontadorDoEspelho,ObterMeuEspelhoCasoDeUso,ObterEspelhoDaEquipeCasoDeUso,ObterEspelhoParaRhCasoDeUso,ObterPontoDaEquipeCasoDeUso}.cs`, DTOs (`PontoEspelho*`, `PontoEquipe*`), `Infrastructure/Ponto/ConsultaDoPonto.cs`, `Api/Ponto/PontoGestorEndpoints.cs`, `Api/Ponto/PontoRhEndpoints.cs` (só leitura por ora). Test: `Ponto/EspelhoDoPontoTests.cs`, `Ponto/PontoDaEquipeTests.cs`.

**Interfaces:** `MontadorDoEspelho.MontarAsync(funcionarioId, ano, mes, comPodePedirAjuste) → PontoEspelho` (um dia por data, `SituacaoDoDiaNoEspelho`, `atrasoMinutos`, `cargaMinutos` menos horas de atestado validado, `pedidoEmAnaliseId`; `resumo` com `faltas` = dias passados `SEM_REGISTRO`; pedidos e atestados vazios até a 3b); a família do gestor passa por `DaEquipe` (fora = 404); `obterPontoDaEquipe` reusa `ISituacaoDeHoje` e o montador do dia (`contagensHoje`/`situacaoHoje` `null` em dia passado).

- [ ] **Step 1: Testes (RED):** o espelho de outubro/2026 do elenco igual ao exemplo do contrato (situações, `resumo`, atraso 14); mês corrente termina hoje, mês futuro `dias: []`, nada antes da admissão nem depois do desligamento; feriado com batida mantém `nomeFeriado`; `horasMinutos` `null` em `INCOMPLETO`; gestor pede espelho de outra equipe/de si/ de gestor = 404; `ano`/`mes` fora da faixa = 400; `obterPontoDaEquipe`: 6 trabalhando, Diego sem registro, Larissa de férias, `data` futura = 400, dia passado com `situacaoHoje: null`; gestor sem setor = lista vazia.
- [ ] **Step 2-5: RED, implementar, contrato** (`obterMeuEspelhoDePonto`, `obterEspelhoDePontoDaPessoaDaEquipe`, `obterEspelhoDePontoDeFuncionario`, `obterPontoDaEquipe` no escopo e no `STATUS.md`), **GREEN**. **Step 6: Mutação do incremento 3a** (T2 já tem a sua; aqui ~25 mutantes em `MontadorDoEspelho`, `ConsultaDoPonto` e nos casos de uso), suíte do incremento, **commit** `feat: espelho de ponto e ponto da equipe`.

# Incremento 3b: ajustes em duas etapas e atestado

## Task 7: Modelo do pedido (regras puras)

**Files:** Create `Application/Ponto/Regras/{EtapaDoPedidoDeAjuste,RegrasDeDecisaoDoPedido,ResumoDaCorrecao,AplicacaoDosItens}.cs`. Test: unidade.

**Interfaces:** `EtapaDoPedidoDeAjuste.De(pedido)` (decisão 7); `RegrasDeDecisaoDoPedido.PodeDecidir(pedido, QuemConsulta) → (bool, string? motivo)` com os **cinco textos fixos** do contrato (Sofia × Roberto, pedido do próprio RH, automática); `ResumoDaCorrecao.Montar(itens, marcacoes)` ("Incluir a entrada às HH:mm", "Incluir o período HH:mm - HH:mm", "Corrigir a entrada para HH:mm", partes com "; ", horário em -03:00); `AplicacaoDosItens.Aplicar(marcacoesDoDia, itens) → (novoDia, ErroDeAplicacao?)` (sequência inválida, duplicidade; nada é gravado).

- [ ] Testes (RED) com os exemplos do contrato (pedidos 297, 302, 306), mutação (~25) e commit `feat: regras do pedido de ajuste de ponto`.

## Task 8: Pedir e ver os próprios pedidos (#4, #5, #6)

**Files:** Create `CriarPedidoDeAjusteCasoDeUso`, `ListarMeusPedidosCasoDeUso`, `ObterMeuPedidoCasoDeUso`, `IPedidosDeAjusteRepositorio` e a implementação, endpoints. Test: `Ponto/PedirAjusteTests.cs`.

**Interfaces:** `CriarPedidoDeAjusteCasoDeUso.ExecutarAsync(PedidoCriar)`: usa `IGestorAusente.EtapaInicialDoPedidoAsync`; grava `ID_GESTOR`, `RESULTADO_GESTOR`/`DATA_ETAPA_GESTOR`/`ETAPA_GESTOR_AUTOMATICA` quando o gestor está ausente (aprovação automática: `decididaPor: null`, `decididaEmUtc` = criação); valida **antes de gravar**: dia passado (`PONTO_PEDIDO_DIA_NAO_PASSOU`), instante não futuro, no dia ou no seguinte, sequência do dia **depois de aplicar** os itens, `marcacaoId` do mesmo dia e da mesma pessoa, instantes repetidos; cria o `PONTOS` do dia se faltar; `CHAVE_PENDENTE` (1062) → 409 `PONTO_PEDIDO_JA_EM_ANALISE`; evento `PEDIU_AJUSTE`; **sem e-mail**.

- [ ] Testes (RED): os quatro exemplos de criação do contrato (303, 302, 296, 297); duplo envio paralelo (um 201 e um 409, nunca 500); cada um dos códigos 422; `FUNCIONARIO_INATIVO`; justificativa 5-500; listas com filtros `status`, `etapa`, ordenação e `direcao` padrão DESC (decisão 14); pedido de outra pessoa = 404. Contrato (`listarMeusPedidosDeAjuste`, `criarPedidoDeAjuste`, `obterMeuPedidoDeAjuste`). Commit `feat: pedido de ajuste de ponto (criar e ver os proprios)`.

## Task 9: Etapa do gestor (#13 a #16)

**Files:** Create `ConsultaDePedidosDoGestor`, `DecidirComoGestorCasoDeUso`, `Infrastructure/Emails/ResultadoAjustePontoRenderizador.cs`, fontes do menu. Test: `Ponto/DecidirComoGestorTests.cs`.

**Interfaces:** `AprovarAsync/RecusarAsync(pedidoId, PontoPedidoDecisao)` na equipe do gestor (`DaEquipe`; pedido do gestor, do próprio e de outra equipe = 404); UPDATE condicional por situação (concorrência → 409 `PEDIDO_JA_DECIDIDO`/`PEDIDO_ETAPA_INCORRETA`); aprovar grava `RESULTADO_GESTOR = 'APROVOU'`, segue `PENDENTE`; recusar grava `RECUSOU` + `REJEITADA` (`etapa: CONCLUIDA`); e-mail pela fila (decisão 11); `IFonteDeContadorDoMenu` `PedidosDeAjusteDePontoDoGestor` e a parte de ajustes de `PendenciasDaEquipe`.

- [ ] Testes (RED): aprovar não muda batida; e-mail na fila com chave única e sem `EMAIL_CORPORATIVO` pulado; corrida gestor × gestor ausente = 409; `resumo` independe da página e do filtro. Contrato (4 operações). Commit `feat: etapa do gestor no pedido de ajuste`.

## Task 10: Etapa do RH (#23 a #26)

**Files:** Create `ConsultaDePedidosDoRh`, `DecidirComoRhCasoDeUso`, fonte do menu do RH. Test: `Ponto/DecidirComoRhTests.cs`.

**Interfaces:** `AprovarAsync/RecusarAsync`: **uma transação** que aplica todos os itens (`CORRIGIDA_RH` + `DATA_CORRECAO` = relógio; `AplicacaoDosItens` revalida), grava `APROVADA`/`CONCLUIDA`, `ID_FUNCIONARIO_RH`, `DATA_CONFIRMACAO_RH`, `ID_GESTOR` = linha do RH quando o pedido não tinha etapa do gestor (`PEDIDO_RH_FORA_DE_GESTORES` se ela não existe; `PEDIDO_PROPRIO` vence), observação do RH substitui a do gestor, chama `IRecalculoDoBancoDeHoras`, evento, e-mail; pedido ainda na etapa do gestor = 409 `PEDIDO_ETAPA_INCORRETA` ("ainda está na etapa do gestor").

- [ ] Testes (RED): aprovado pelo gestor e recusado pelo RH e o contrário; pedido com várias batidas (uma corrigida e uma nova) aplica todas; **o dia mudou depois da criação e a sequência ficou inválida = 422 sem nada aplicado** (Review Focus 3); duplicada = 409 sem nada aplicado; RH confirma o próprio pedido que passou pelo gestor; sem linha em `GESTORES` = 422. Contrato (`listarPedidosDeAjusteParaRh`, `obterPedidoDeAjusteParaRh`, `aprovar...ComoRh`, `recusar...ComoRh`). Commit `feat: etapa do RH no pedido de ajuste`.

## Task 11: Pedidos parados no gestor e correção direta do RH

**Files:** Create `Infrastructure/Ponto/PedidosParadosNoGestorDoPonto.cs` (`IPedidosParadosNoGestorDaFatia`), `CorrigirMarcacoesDoDiaCasoDeUso`. Test: `Ponto/PedidosParadosTests.cs`, `Ponto/CorrigirDiaTests.cs`.

**Interfaces:** `AprovarEtapaDoGestorAsync(gestorId)` aprova a etapa dos pedidos sem resultado daquele gestor (`automatica = true`, sem e-mail, evento resumo, `AUDITORIA-OK`) e devolve `PedidosAprovados.AjustesDePonto`; `PassarParaORhAsync(gestorId)` põe `ID_GESTOR = NULL` nos pendentes sem resultado. `CorrigirMarcacoesDoDiaCasoDeUso.ExecutarAsync(funcionarioId, dia, PontoDiaCorrigir)`: substitui a lista (id com mudança → `CORRIGIDA_RH`; sem id → nova; ausente → apagada, e a batida que já foi alvo de pedido dá 409 `PONTO_MARCACAO_EM_USO`), 409 `PONTO_DIA_COM_PEDIDO_EM_ANALISE`, 422 de sequência/instante/inativo, recálculo, evento com a batida antiga (log), **sem e-mail e sem segunda etapa**.

- [ ] Testes (RED): `pedidosAprovadosAgora.ajustesDePonto` real em `configurarAusenciaDoGestor`; trocar, tirar e desligar gestor passam os pedidos ao RH (ajustar `STATUS.md:48,50,63,66,81`); correção: os quatro casos da lista, dia futuro e de quem não foi admitido = 422, `id` de outro dia = 400, a batida original só fica no log. Contrato (`corrigirMarcacoesDoDia`). Commit `feat: pedidos parados no gestor e correcao direta do RH`.

## Task 12: Atestados (#8, #9, #17, #18, #31 a #34)

**Files:** Create `EnviarAtestadoCasoDeUso`, `ConsultaDeAtestados`, `BaixarAtestadoCasoDeUso`, `DecidirAtestadoCasoDeUso`, `Infrastructure/Ponto/FonteDeReferenciasDosAtestados.cs` (prefixo `atestados/`), endpoints; modify `ValidacaoDeArquivoDeDocumento` para aceitar o teto e as mensagens de quem chama (ou criar a variante do atestado, **sem duplicar a regra do tipo pelos bytes**), `FormulariosDoContrato` (`enviarAtestado`). Test: `Ponto/AtestadosTests.cs`.

**Interfaces:** envio pela receita da 1c (valida tudo antes, blob fora de transação, transação curta, apaga o blob se falhar; `DATA_ENVIO` do relógio); gestor vê só os da equipe (outra equipe = lista vazia e download 404); download grava `ATESTADOS`/`BAIXOU` **antes** do link (nunca o nome do arquivo); validar/recusar só RH, UPDATE condicional por situação (409 `ATESTADO_JA_DECIDIDO`), recusar com `motivo` 1-500, recálculo dos dias do atestado pela porta.

- [ ] Testes (RED): fim antes do início, horas sem par, horas em mais de um dia, tipo e tamanho do arquivo (400 com `errors`); atestado visto por quem não pode; arquivo órfão apagado se o banco recusar; auditoria sem o conteúdo; `FALTA_COMPENSADA` no espelho e `ATESTADO` em `situacaoHoje` **só depois de validado**; RH valida o próprio. Contrato (8 operações). **Mutação do incremento 3b** sobre os arquivos novos, suíte do incremento, commit `feat: atestados de ponto`.

# Incremento 3c: feriados, banco de horas, extrato, folha e PDF

## Task 13: Feriados (#27 a #29) e importação

**Files:** Create `ListarFeriadosCasoDeUso`, `CriarFeriadoCasoDeUso`, `EditarFeriadoCasoDeUso`, `ImportarFeriadosTarefa` (`IMPORTAR_FERIADOS:ano=AAAA`), `RepetirFeriadoTarefa` (`REPETIR_FERIADO:ano=AAAA`), `Application/Ponto/Regras/FeriadosNacionais.cs` (cálculo com Páscoa), `TabelaDeFeriadosEstaduais.cs`. Test: `Ponto/FeriadosTests.cs`, unidade de `FeriadosNacionais`.

**Interfaces:** decisão 12. `FeriadosNacionais.Do(ano)` (Confraternização, Sexta-feira Santa, Tiradentes, Dia do Trabalho, Independência, Nossa Senhora Aparecida, Finados, Proclamação da República, Consciência Negra, Natal; Carnaval e Corpus Christi **não** entram: eram facultativos na carga do `V001`); criar sempre `MUNICIPAL` com UF e município da empresa, 409 `FERIADO_DUPLICADO` (1062 da chave `(DATA, NOME)`; collation `utf8mb4_unicode_ci` ignora acento e caixa: **medir**), `repeteTodoAno` não repete 29/02; editar só `MUNICIPAL`.

- [ ] Testes (RED): importar duas vezes não duplica; "Independência do Brasil" sobre "Independencia do Brasil" não duplica (dedup por data + tipo); ponto em feriado importado; `incluirInativos`; sem empresa não dá erro na listagem e dá 422 `EMPRESA_NAO_CONFIGURADA` ao criar. Contrato (3 operações). Commit `feat: feriados do ponto`.

## Task 14: Consolidação do banco de horas

**Files:** Create `ConsolidarBancoDeHorasTarefa`, `RecalculoDoBancoDeHoras : IRecalculoDoBancoDeHoras` (substitui a nula), `IUltimoDiaConsolidado`, `CondicaoDoBancoDeHorasParaDesligamento : ICondicaoParaEfetivarDesligamento`. Test: `Ponto/BancoDeHorasTests.cs`, `Ponto/ConsolidacaoTests.cs`; ajuste de `ExecutorDeTarefasTests` (mais um tipo recuperável).

**Interfaces:** decisão 10. `RecalcularDiaAsync(funcionarioId, dia)` insere, atualiza ou apaga a linha do dia (decisão 3 para inativo): esperado = carga do dia menos férias, feriado, folga e horas de atestado; horas = períodos fechados; `INCOMPLETO` e pedido `PENDENTE` não lançam; CRÉDITO "Hora extra" / DÉBITO "Saldo negativo do dia" ou "Falta"; `QUANTIDADE` em horas arredondada a 2 casas, **a partir de minutos inteiros**; `DIA_REFERENCIA` e `DATA_LANCAMENTO` do relógio. A condição do desligamento exige o dia consolidado antes de gravar.

- [ ] Testes (RED): rodar duas vezes não duplica; parada de vários dias recupera (horizonte 60); ajuste aprovado, correção do RH e atestado validado recalculam o dia; dia `INCOMPLETO` não lança e lança depois de resolvido; saldo zero apaga a linha; jornada 22:00 a 06:00 consolida só às 12:00 do dia seguinte; inativo com linha existente atualiza, sem linha não cria; desligamento só depois de fechar o último dia. Commit `feat: consolidacao do banco de horas`.

## Task 15: Extrato e indicador (#7, #22)

**Files:** Create `ObterExtratoDoBancoDeHorasCasoDeUso`, `FonteDoBancoDeHorasNaFicha : IFonteDeIndicadorDaFicha`. Test: `Ponto/ExtratoTests.cs`.

- [ ] Testes (RED): o exemplo do contrato (saldo 380 = soma **das linhas em minutos**, nunca `FN_CALCULAR_SALDO_BANCO_HORAS`), filtros `de`/`ate`/`tipo`, `atualizadoAte` do último dia consolidado, ordem fixa `diaReferencia` desc, indicador da ficha deixa de ser `null`. Contrato (2). Commit `feat: extrato e indicador do banco de horas`.

## Task 16: Exportação para a folha (#30)

**Files:** Create `ExportarPontoParaFolhaCasoDeUso`. Test: `Ponto/ExportacaoDaFolhaTests.cs`.

- [ ] Testes (RED): CSV com BOM, `;`, CRLF, acentos, nome que começa com `=` neutralizado, matrícula nula vazia, nenhum número negativo, decisão 15 (quem saiu no meio do mês entra, quem saiu antes do mês não). Contrato (1). Commit `feat: exportacao do ponto para a folha`.

## Task 17: Espelho em PDF (#12, #20)

**Files:** Create `Application/Ponto/IGeradorDePdfDoEspelho.cs`, `Infrastructure/Ponto/{GeradorDePdfDoEspelho,FonteDoPdf}.cs`, a fonte TTF como recurso embutido, `BaixarEspelhoEmPdfCasoDeUso`, `FonteDeReferenciasDosEspelhos`; modify `Directory.Packages.props` (**PDFsharp-MigraDoc 6.2.4**, com o comentário da licença e do aviso sobre a 6.5.0). Test: `Ponto/EspelhoEmPdfTests.cs`.

- [ ] Testes (RED): o PDF abre (cabeçalho `%PDF`), tem as páginas esperadas e o texto "São Paulo" e "Júlia" extraível (a fonte embutida tem acento); gestor de outra equipe = 404; o link é temporário e como anexo; o arquivo vai para `espelhos/`; sem a fonte do sistema o PDF ainda sai; uso de memória não cresce com 31 dias. Contrato (2). Commit `feat: espelho de ponto em pdf`.

## Task 18: Verificação da fatia 3

- [ ] Os 34 `operationId` no escopo (`EscopoDaDivergenciaDaFatia3`), `STATUS.md` sem pendência do ponto (as linhas da 1b e 1c que citavam "porta vazia" atualizadas), README (seção "Ponto (fatia 3)": decisões 1 a 19, a pergunta do pedido parado na mudança de setor, o aviso da licença do PDFsharp), `dotnet build back-end --no-incremental` sem avisos, formato, **suíte inteira em grupos paralelos**, **mutação do incremento 3c**, revisão geral (revisor de contexto novo, modelo mais capaz, só leitura, sobre `882242d..HEAD`) com um passe de correção RED→GREEN, PR e `gh pr merge --squash --admin`, sincronizar a `main` e começar o plano da fatia 4.

---

## Autorrevisão

- **Cobertura das 34 operações:** #1 e #2 (T5), #3 e #11 e #19 (T6), #10 (T6), #4-#6 (T8), #7 e #22 (T15), #8 e #9 e #17 e #18 e #31-#34 (T12), #12 e #20 (T17), #13-#16 (T9), #21 (T11), #23-#26 (T10), #27-#29 (T13), #30 (T16).
- **Comparativo com o levantamento (seção G):** T8 do levantamento (porta de recálculo) foi para a T5; T13 do levantamento (pedidos parados) virou a T11 junto da correção do RH; T16 (feriados) virou T13; a **leitura** de feriados foi antecipada para a T3 (decisão 12).
- **Nomes:** `MensagensDoPonto`, `TradutorDeRecusaDoPonto`, `RegistrarMarcacaoCasoDeUso`, `IRecalculoDoBancoDeHoras`, `ISaldoDoBancoDeHoras`, `IPedidosDeAjusteRepositorio` aparecem nas tarefas que os produzem e nas que os consomem com a mesma assinatura.
- **Proporção:** o plano é o contrato das decisões; o texto exato dos erros e as colunas estão no levantamento, **não** repetidos aqui.
