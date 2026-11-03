# Base do esquema (juntar V006–V047 em V001–V003) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans. Steps use checkbox (`- [ ]`) syntax.

**Goal:** trocar os 47 scripts de `Banco de Dados/Scripts/` por uma base de 5 (`V001` criação, `V002` triggers, `V003` funções, `V004` refresh tokens, `V005` níveis iniciais) que produz **o mesmo banco** (no sentido lógico: colunas, índices, restrições, triggers, funções e dados), e reescrever as citações de `V006`–`V048` no repositório.

**Decisão do dono (2026-11-03):** opção B, **renumerar tudo e reescrever as citações**; o front ainda não começou, então mexer no texto do contrato não atrapalha ninguém. O dono escolheu isto depois de ver a conta: 27 arquivos citam `V0xx` fora da pasta de scripts (cerca de 800 ocorrências; `contrato/openapi.yaml` sozinho tem 152).

**Architecture:** a prova é por comparação: o MySQL 8.4 do compose recebe os 47 scripts atuais (banco `cmp_ref`, o "retrato de referência A") e a base nova (banco `cmp_novo`, retrato B); os dois têm de sair iguais. Controles com defeitos injetados provam que o comparador acusa. A base nova é montada por uma ferramenta de junção (rascunho fora do repo), revisada à mão, e só depois se apagam os antigos e se reescrevem as citações. O texto de cada `V006`–`V047` continua no git (commit `10fe79a`; conferido em 2026-11-03: nenhum deles mudou depois dele).

**Tech Stack:** MySQL 8.4 do `docker-compose.yml` (contêiner `rh-system-mysql-1`, só com bancos descartáveis `cmp_*`, nunca o `rh_system`; não se sobe um segundo servidor porque a memória livre está em cerca de 3,7 GB), cliente `mysql` e `mysqldump` dentro do contêiner, bash, Node (junção e reescrita), `npm run validar` do contrato, `dotnet test` **por filtro** (a suíte inteira não se roda sem o dono pedir: o sistema já a matou uma vez por falta de memória).

**Spec:** `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md` (Task 4, regras dos scripts) e `Banco de Dados/Scripts/LEIA-ME.md`. Revisado por `revisor-de-plano` em 2026-11-03 (24 achados; 2 bloqueavam e foram tratados abaixo).

## Global Constraints

- Branch `chore/base-do-esquema`, criada a partir de `feat/fatia-0-esqueleto`. **Sem commit e sem push até o dono mandar.** Nunca Claude como coautor, nem "Generated with Claude Code".
- Ferramentas, retratos e caderno ficam numa pasta temporária **fora do repositório** (`%LOCALAPPDATA%\Temp\rh-base-esquema`). O contêiner `rh-system-mysql-1` **nunca** é parado, removido nem recriado por este plano sem o dono saber; só se criam e apagam bancos `cmp_*`.
- "Idêntico" vale no sentido **lógico**: no banco antigo as colunas acrescentadas por `ALTER` entram como `INSTANT` (versões de linha do InnoDB) e no novo nascem no `CREATE`; isso não aparece no retrato e não muda comportamento.
- `V004` e `V005` **não mudam de número nem de nome**. Do `V004` só o texto do comentário já corrigido.
- Próximo número livre depois da base: **`V006`** (os planos diziam `V048`).
- **Texto do `V001` e do `V002` passa a ser da aplicação e do grupo juntos:** o dono avisa o grupo e o Kauan (o assistente não fala com ele). O `V002` só ganha a exceção de FÉRIAS do trigger de holerite.

## Regras da reescrita de citações (Task 5)

1. **Padrão de busca:** `(?<![A-Za-z0-9])V0\d\d(?!\d)`. O `\b` não serve: `_` conta como letra e deixava passar nome de arquivo (`V006__x.sql`) e nome de teste (`_do_V034_`).
2. **Uma passada só, com função:** `V048` → `V006` e `V006`–`V047` → `V001` na **mesma** passada. Nunca em duas passadas (a segunda reescreveria o `V006` que a primeira acabou de escrever). Intervalos ("V030–V047", "V006 a V029", "V001 a V047", "V006 ao V047"): listados antes, resolvidos à mão e protegidos por marcador durante a passada.
3. **Trigger:** citações do ajuste do trigger de holerite (`V012`, parte do trigger) → `V002`; o resto do `V012` → `V001`.
4. **Quem é protegido (não se reescreve):** `Banco de Dados/Scripts/V*.sql`; `docs/superpowers/plans/2026-11-03-base-do-esquema.md` (este plano); o mapa histórico do `LEIA-ME.md` (escrito à mão); `contrato/CHANGELOG.md` (entradas antigas); **registros datados e medições**: `docs/retomada-da-conversa.md`, as medições datadas do plano da fatia 0 (linhas "Medido em 2026-10-30…"), a seção "Como foi testado" do `LEIA-ME.md`, as notas de histórico da memória do assistente. Em cada um entra **uma nota datada** dizendo que os scripts `V006`–`V047` citados ali foram incorporados ao `V001`/`V002` em 2026-11-03.
5. **Citações com número de linha** (`Vnnn:NN`, `LEIA-ME.md:NN`, `openapi.yaml:NN`, "linhas 139-140"): apontam para a linha errada depois da junção. Uma ferramenta lista cada uma com o texto da linha antiga e a tabela a que pertencia, e se reescreve trocando a linha por **tabela/coluna** ("`V001`, tabela `FORMULARIOS_RESPOSTAS`"). Citações a `LEIA-ME.md:NN` passam a citar a seção pelo título.
6. **Contagens que não são número de versão** ("47 linhas" em `CONGELADOS.sha256`, "os 47 mesmos hashes", "42 scripts", o nome `Executor_aplica_os_47_scripts…`): no plano da fatia 0 (Tasks 12–14) viram **5**; as de tabelas (49), triggers (36) e funções (8) continuam certas.
7. **Tabelas organizadas por script** (`plano-back-end.md`, seção 7 e a coluna de scripts por fatia): a coluna "script" vira "objeto" (tabela ou coluna), para não virar uma coluna inteira de `V001`.
8. **Frases cujo sentido muda** ("scripts já escritos em `Banco de Dados/Scripts` (`V042`)", "o que é novo"): reescritas à mão.
9. **Exemplos de script FUTURO** (`V030__telefone_emergencia.sql` no design) viram `V006…`, nunca `V001`.
10. Depois da passada: colapsar duplicatas (`V001 e V001`, `V001, V001`, `V001/V001`) e revisar o diff arquivo por arquivo.

## Review Focus

1. **Ordem das chaves estrangeiras.** FK para tabela criada *depois* quebra o `CREATE TABLE`. Duas FKs de `FORMULARIOS_RESPOSTAS` (para `FORMULARIOS_CANDIDATOS` e `FORMULARIOS_PERGUNTAS_ALTERNATIVAS`) ficam numa seção final de `ALTER TABLE`. (O grupo já tinha uma seção assim no meio do arquivo, a "seção 6"; a nova fica no fim.)
2. **Scripts que mudam definições existentes:** `V017` (`MODIFY COLUMN`), `V012` (`DROP INDEX` + trigger), `V014` (`DROP CHECK` + recria `CHK_ADVERTENCIAS_STATUS`), `V021` (`ALTER COLUMN … DROP DEFAULT`), `V043` (`SET DEFAULT FALSE`); `V011` só acrescenta.
3. **Exceção de FÉRIAS no trigger de holerite:** passa a morar no arquivo do grupo; se ele trocar o `V002`, ela some. Um teste de comportamento a protege (Task 4).
4. **Dados:** só a linha de `SEQUENCIAS` do `V030` tem efeito em banco vazio; `UPDATE` de `V013`, `V029`, `V043` e o `INSERT … SELECT` de `V029` são recarga de dados antigos.
5. **A prova roda por outro caminho** (cliente `mysql` como root) **que a aplicação** (executor com usuário restrito): uma sonda aplica a base nova pelo executor da API e compara (Task 2, Step 7).
6. **Efeito no desenvolvimento local:** com o `V001` alterado, o executor recusa subir em banco que já tem a versão antiga; quem tem banco local recria (`docker compose down -v`), e isto vai no relatório e no README (já diz).

---

### Task 1: Retrato de referência e comparador — **FEITA**

- [x] Ferramentas em `%LOCALAPPDATA%\Temp\rh-base-esquema` (`lib.sh`, `aplicar.sh`, `retrato.sh`, `comparar.sh`, `merge.mjs`). `aplicar.sh` normaliza CRLF→LF e tira BOM (como o executor), e só aceita bancos `cmp_*`.
- [x] Banco A: os 47 scripts atuais em `cmp_ref` = **48 tabelas** (a 49ª, `SCHEMA_VERSOES`, só o executor cria), 426 colunas, 241 linhas de índice, 769 de restrições (inclui `KEY_COLUMN_USAGE`, as colunas de cada FK), 36 triggers, 27 de rotinas, 3 de dados.
- [x] Retrato: `colunas.txt` (com `COLUMN_TYPE`, padrão, extra, expressão gerada, charset/collation, comentário), `indices.txt`, `restricoes.txt` (tabela, CHECK, FK com regras, colunas de FK), `triggers.txt`, `rotinas.txt`, `tabelas.txt` (motor, collation, comentário), `estrutura.sql` (`mysqldump --no-data --routines --triggers`, sem `AUTO_INCREMENT=n`), `dados.sql`.
- [x] Controle (9 defeitos injetados antes da perda da pasta, e 7 de novo com as ferramentas refeitas): cada defeito foi acusado no arquivo certo (coluna/tipo → `colunas`/`estrutura`; CHECK → `restricoes`; índice → `indices`; dado → `dados`; comentário de tabela → `tabelas`; função → `rotinas`; trigger → `triggers`; regra de FK → `restricoes`).
- [x] **Controles que faltavam (achado 15 do revisor), feitos:** A × A′ (os 47 originais do git aplicados em dois bancos separados): tudo igual, inclusive a estrutura exata; defeitos novos acusados no arquivo certo: ordem das colunas dentro de um índice (`indices`), coluna de origem de uma FK (`restricoes`; linha `KCU` mudou de `ID_FUNCIONARIO_RH` para `ID_FUNCIONARIO`), valor de `SEQUENCIAS` (`dados`), `DEFAULT` (`colunas`).

### Task 2: Base nova — **FEITA**

- [x] `merge.mjs` lê o `V001` do grupo e os `V006`–`V047` em ordem e monta o `V001` novo: tabelas novas no fim (seções 26 a 47, numeradas e com o título do pedido de origem, sem número `Vnnn`); `ADD COLUMN`, `ADD CONSTRAINT`, `MODIFY`, `DROP INDEX/CHECK`, `SET/DROP DEFAULT` aplicados dentro do `CREATE TABLE`; um comentário com o título do pedido antes de cada grupo acrescentado; `CREATE INDEX` dos antigos `V035`–`V038` **soltos** logo depois da tabela (ver Ruling no caderno: dentro do `CREATE TABLE` o MySQL guarda a constante de 3 CHECKs como `_utf8mb4` e não `_ascii`; medido); `INSERT` de `SEQUENCIAS` com `ULTIMO_VALOR` 0; os `UPDATE` de recarga de dados antigos ignorados e registrados.
- [x] Resultado: **47 `CREATE TABLE`** (25 do grupo + 22 novos).
- [x] Trigger no `V002`: única mudança é a condição `AND NEW.TIPO_HOLERITE <> 'FERIAS'` e um comentário de 2 linhas.
- [x] **Comparação A × B (repositório):** `colunas` (426), `indices` (241), `restricoes` (769), `rotinas` (27), `tabelas` (48) e `dados` (3) **iguais**; `triggers`: igual **exceto o texto do comentário** reescrito no trigger de holerite (com os comentários removidos: igual, 1.117 bytes, e os outros 35 triggers iguais no texto exato); `estrutura.sql`: mesmo conjunto de linhas, só a **ordem** de 3 linhas `KEY` difere (`IDX_FERIAS_FUNCIONARIO_INICIO`, 2 chaves de FK de `FORMULARIOS_RESPOSTAS`, `IDX_PONTOS_FUNCIONARIO_DATA`).
- [x] Cabeçalho do `V001` explicando a junção, com a referência ao commit `10fe79a`.
- [x] **Step 7 (sonda do caminho da aplicação, achado 16), feito duas vezes (base sem notas e base final com notas):** a API de desenvolvimento, apontada para um banco `cmp_exec` com um usuário restrito criado só para isto (senha e chave aleatórias geradas na hora; nenhuma senha do projeto lida), aplicou `V001` (68 comandos), `V002` (72), `V003` (16), `V004` e `V005`; o retrato (sem `SCHEMA_VERSOES`, com o `DEFINER` normalizado) saiu **igual** ao do cliente `mysql`: colunas, índices, restrições, tabelas, triggers (texto exato), rotinas, dados e estrutura.

### Task 3: Remover os antigos, escrever o mapa e o `LEIA-ME` — **FEITA**

- [x] Os 47 originais foram guardados (`git archive HEAD`, na pasta temporária) e os 42 `V006`–`V047` saíram da pasta (`git rm`; continuam no commit `10fe79a`). A pasta tem `LEIA-ME.md` e `V001`–`V005`.
- [x] `LEIA-ME.md` reescrito à mão: topo com o estado atual e o que cada `V00x` tem; o mapa histórico (pedido → antigo script) fica, com a nota de que hoje está tudo no `V001`/`V002`; as frases que ficaram falsas foram corrigidas (numeração, prazo, "como foi testado" marcado como medição antiga); nova seção **"Como a base nova foi conferida"**; "Armadilhas novas" manteve o título.
- [x] **Ruling:** os comentários de abertura dos 42 scripts antigos foram copiados, **sem mudança**, para uma seção de "notas dos pedidos" no fim do `V001` (42 notas), e os comentários de dentro dos `CREATE TABLE` das tabelas novas foram preservados. Motivo: é a documentação que o dono pediu em 2026-10-29 para o Kauan conferir, e vários planos a citam. Só comentário: o banco não muda (conferido: código SQL idêntico e retrato igual).

### Task 4: Testes e código — **FEITA**

- [x] O teste obsoleto `Scripts_de_negocio_V006_em_diante_aplicam_depois_de_V001_a_V005_pelo_usuario_restrito` saiu (com 5 scripts passaria sem provar nada). Entrou `Trigger_de_holerite_aceita_competencia_futura_so_para_ferias`: contra o `V002` **original** falha (`Nao e possivel cadastrar holerite de competencia futura.` no `INSERT` de FÉRIAS futuro); contra o `V002` novo passa.
- [x] Comentários do código atualizados (`ScriptsDoGrupoTests.cs:33`, `SondaMapeamentoDosScriptsTests.cs:249,272`); os demais citam `V001`–`V005`, que continuam certos.
- [x] Por grupo, **com o `V001` final** (notas incluídas) e o contrato `0.2.2`, o contêiner do compose parado durante os testes: `Esquema` 27, `Persistencia` 17, `Configuracao` 18, `Autenticacao` 58, `Contrato` 39 (**159 de integração, 0 falhas**); `Domain` 21 e `Application` 28 (**0 falhas**). `dotnet format back-end --verify-no-changes`: código de saída 0. A suíte inteira (`dotnet test back-end`) **não** foi rodada de uma vez.

### Task 5: Citações (documentos e contrato) — **FEITA**

- [x] Inventário antes: 780 ocorrências de `V006`–`V048` em 22 arquivos. Contrato antes: 224 operações, 1472 exemplos válidos.
- [x] 32 citações com número de linha de script e 15 ao `LEIA-ME.md:NN` trocadas à mão: linha de DDL → "tabela X do `V001`"; linha do comentário de abertura → "nota «(antigo Vnnn)» do `V001`"; `LEIA-ME.md:NN` → a seção. Depois, 272 linhas pela ferramenta de reescrita (uma passada só: `V048`→`V006`, `V006`–`V047`→`V001`, repetições colapsadas) e cerca de 40 trocas manuais para frases que ficaram falsas ou vagas; seção 7 e a tabela por fatia do plano do back-end reescritas por objeto.
- [x] Registros datados **não reescritos**, com nota datada no topo: `docs/retomada-da-conversa.md`, `docs/pedidos-para-o-kauan.md`; no plano da fatia 0 as Tasks 0 a 11 (já executadas) e as medições datadas ficam, com nota; o cabeçalho e as Tasks 12 a 14 foram atualizados (47→5 scripts).
- [x] Contrato: só as fontes foram editadas; `npm run validar` regenerou `openapi.yaml`, `ROTAS.md` e `PROVISORIOS.md`: 224 operações e 1472 exemplos válidos. **Prova de "só texto":** o `openapi.yaml` de antes e o de agora, como estruturas, são idênticos fora de `description`, `summary`, `x-provisorio` e `info.version` (97 textos mudaram). Versão `0.2.1` → `0.2.2`, com entrada no `CHANGELOG.md`.
- [x] Inventário depois: sobram `V006`–`V048` só em registros protegidos (plano da fatia 0, Tasks 0–11: 68; `retomada`: 18; `pedidos-para-o-kauan`: 12), no `CHANGELOG` e este plano, e como "próximo script livre" (`V006`: 10 linhas, conferidas uma a uma). Os ponteiros `(antigo Vnnn)` (notas do `V001`) são intencionais.
- [x] Memória do assistente: `estado-dos-planos.md`, `decisoes-contrato.md`, `decisoes-telas-figma.md` e `MEMORY.md`.

### Task 6: Fechamento — **FEITA, falta o que é do dono**

- [x] Bancos `cmp_*` apagados; o contêiner `rh-system-mysql-1` foi **parado durante os testes (duas janelas) e religado**; o banco de desenvolvimento `rh_system` continua lá. Nada foi commitado nem enviado.
- [ ] **Do dono:** avisar o grupo e o Kauan (o `V001` e o `V002` mudaram de forma; o assistente não fala com ele); recriar o banco local (`docker compose down -v`, porque o executor recusa um banco cujo `V001` ou `V002` mudou); decidir commit e push, e a ordem dos PRs (esta branch sai da `feat/fatia-0-esqueleto`, que ainda não foi mergeada).
