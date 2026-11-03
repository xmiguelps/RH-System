# Plano do back-end do RH-System: entrada, ordem de trabalho e regras

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **Este arquivo é a entrada: ele não tem tarefas.** As tarefas estão nos planos de cada fatia, citados na seção 3. O método de execução (subagentes ou na própria sessão) **ainda não foi escolhido pelo dono**: ele decide depois do planejamento.

**Goal:** Entregar o back-end do RH-System (API, banco por scripts SQL, autenticação, CI e deploy no Azure) fatia por fatia, implementando o contrato `contrato/openapi.yaml`, sem esperar o front-end e sem que o front-end espere o back-end.

**Architecture:** Monólito modular em Clean Architecture (`back-end/`), esquema do MySQL por scripts SQL versionados (`Banco de Dados/Scripts/`, `V001` a `V005` prontos antes de o back-end começar) e **contrato primeiro**: o contrato OpenAPI é escrito antes, a API o implementa e um teste falha quando a API gerada diverge dele.

**Tech Stack:** .NET 10, EF Core 9.x sem migrations, `Pomelo.EntityFrameworkCore.MySql` 9.0.0, MySQL 8.4, xUnit, Testcontainers, GitHub Actions, Azure; contrato em OpenAPI 3.1 com ferramentas Node (`contrato/package.json`).

**Spec:** `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (design destes planos, seções 3, 4 e 7); `docs/superpowers/specs/2026-10-21-rh-system-design.md` (spec do produto); `contrato/openapi.yaml` (o contrato, gerado de `contrato/fontes/*.yaml`); `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (roteiro das fatias 0 a 8).

## Global Constraints

Valem as `Global Constraints` do plano da fatia 0 (`docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md`) em todas as fatias. Para este arquivo, em resumo:

- Comandos rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; `curl.exe`). Commit e push **só com autorização do dono**; **nunca com o Claude como coautor**. Segredos nunca vão para o repositório.
- **O contrato é lei.** A API implementa `contrato/openapi.yaml`; só o dono o muda; toda mudança entra em `contrato/CHANGELOG.md` com número de versão (`contrato/CONVENCOES.md`, seção 14).
- Nomes de domínio e de API em português sem acento; mensagens ao usuário em português com acento.
- Os arquivos do grupo em `Banco de Dados/` (as tabelas, os triggers e as funções) só se mudam com autorização do dono. **Os números dos scripts são do dono.**
- Tarefas de CI, deploy e Azure da fatia 0 (Tasks 12 a 14) **só começam quando o front da fatia 0 estiver na `main`** (seção 4). **A fatia 1 não espera por elas:** começa com as Tasks 1 a 9 da fatia 0 passando (seção 3).
- **As fatias 1a, 1b e 1c seguem a tabela de decisões da seção 12** (revisão conjunta dos planos, 2026-10-30). Onde o plano de uma fatia a contradiz, vale a seção 12 e o plano da fatia é corrigido.

## Review Focus

Os riscos deste plano são de **processo**; o teste de cada um está no plano que o executa.

1. **API e contrato divergem sem ninguém notar** (um campo a mais, um código de resposta esquecido, uma rota fora do contrato): o teste de divergência acusa nos dois sentidos (Task 8 da fatia 0; o escopo é uma lista de `operationId`, e cada tarefa que entrega rotas acrescenta os dela no mesmo passo).
2. **Script já aplicado é editado** (antes da Task 14, sem avisar o dono; depois dela, no congelamento): o executor recusa subir e a CI confere `CONGELADOS.sha256` (Tasks 4, 12 e 14 da fatia 0).
3. **Duas versões com o mesmo número de script** (o grupo envia pela web, o dono numera): o executor recusa versão repetida (Task 4).
4. **O front acredita que uma rota existe e ela ainda não existe**: `contrato/STATUS.md` só marca `pronta` o que está implementado, testado e publicado (seção 5).
5. **Back-end e front começam antes de contrato, planos e telas estarem na `main`**, e a branch do front parte de um ponto sem os arquivos da raiz: ordem da seção 4 e Passo 0 (Task 0 da fatia 0).

---

## 1. O que é este plano e para quem

É o plano do **back-end**. Quem o segue é o dono, com o Claude Code, na máquina dele. O **front-end é outro plano**, em outra pasta (`docs/plano-front-end/`, entrada `LEIA-PRIMEIRO.md`), seguido por outra pessoa do grupo com a sessão do Claude Code dela, que **não tem a conversa** que gerou estes documentos: só o repositório. Os dois planos se encontram **só no contrato**:

- o back-end **implementa** o que o contrato diz;
- o front gera tipos e dados simulados do contrato e só liga na API real quando `contrato/STATUS.md` disser que a rota existe.

O plano é **por etapas** (decisão do dono): passo a passo completo só para as fatias 0 e 1. As fatias 2 a 8 têm aqui um roteiro (seção 9), e **o passo a passo de cada uma é escrito no começo dela** (skill `superpowers:writing-plans`, revisado pelo agente `revisor-de-plano`), porque depende de código e de banco que ainda não existem.

## 2. Ordem de leitura

1. `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (o design aprovado: o que é do back-end e o que é do front; seção 7: decisões do dono sobre as telas).
2. `contrato/CONVENCOES.md` e, para a fatia em andamento, o arquivo dela em `contrato/fontes/` (`ROTAS.md` resume as 224 rotas; `ELENCO.md` traz o cenário dos exemplos; `PROVISORIOS.md` lista o que depende de decisão aberta; `STATUS.md` diz o que existe de verdade).
3. Este arquivo.
4. O plano da fatia em andamento (seção 3).
5. `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (decisões que atravessam fatias, dependências) e `docs/superpowers/specs/2026-10-21-rh-system-design.md`.
6. `Banco de Dados/Scripts/LEIA-ME.md` e o `V001` (as notas dos pedidos, no fim do arquivo, dizem por que cada mudança existe), mais os três arquivos do grupo em `Banco de Dados/`.
7. `docs/retomada-da-conversa.md` (regras de trabalho do dono) e `docs/pedidos-para-o-kauan.md`.

## 3. Ordem de trabalho

| Etapa | O que é | Onde está | Começa quando |
|---|---|---|---|
| Entrega do planejamento | Commit do contrato, dos planos, das telas (`Figma/telas/`) e dos scripts SQL; PR para a `main`; merge | design, seção 3, item 6 | agora (decisão do dono sobre a branch: seção 10) |
| **Passo 0** | Arquivos da raiz (`.gitignore`, `.gitattributes`, `.editorconfig`, `README.md` mínimo) e renormalização, num commit só deles | fatia 0, **Task 0** | antes de o front começar |
| **Fatia 0** | Esqueleto: solution em camadas, MySQL em contêiner, scripts e executor, login com JWT, `/health`, CI, deploy e Azure | `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md` (Tasks 1 a 9; 10 e 11 saíram; 12 a 14 esperam o front) | planejamento e Passo 0 na `main` |
| **Fatia 1a** | Fundação técnica: escopo por perfil, auditoria, fila de e-mail, agendador | `docs/superpowers/plans/2026-10-30-back-end-fatia-1a-fundacao.md` (escrito por outra sessão) | Tasks 1 a 9 da fatia 0 passando (não espera o front nem as Tasks 12 a 14) |
| **Fatia 1b** | Cadastro, ficha e acesso: convite, redefinição e troca de senha, desligamento | `docs/superpowers/plans/2026-10-30-back-end-fatia-1b-cadastro.md` (idem) | 1a pronta |
| **Fatia 1c** | Cadastros de apoio, configuração da empresa, documentos do colaborador (primeiro uso do Blob) | `docs/superpowers/plans/2026-10-30-back-end-fatia-1c-apoio-e-documentos.md` (idem) | 1b pronta |
| **Fatias 2 a 8** | Roteiro na seção 9; **plano escrito no começo de cada uma** | `docs/superpowers/plans/` (ao chegar) | a anterior pronta, pelas dependências do roadmap |

Uma fatia só termina com: testes passando (unidade, integração e matriz de perfis), CI verde, deploy no Azure, `contrato/STATUS.md` atualizado pelo dono e as mudanças de banco registradas em scripts novos (roadmap, "Como usar este documento").

**Dois sentidos de "pronta", para não confundir.** Na coluna "Começa quando", "pronta" quer dizer **testes passando e código na `main`**: o próximo incremento não espera o deploy. No `contrato/STATUS.md`, `pronta` quer dizer **implementada, testada e publicada no ambiente da demo**: é o que o front espera. Quem publica e marca `pronta` é o dono, quando o deploy existir (Tasks 12 a 14 da fatia 0).

## 4. Quando back-end e front começam

**Nenhum dos dois começa a executar antes de o contrato, os planos e as telas estarem na `main`** (dono, 2026-10-30; design, seção 3, item 6). A branch da pessoa do front parte da `main`, e a pessoa precisa de permissão de escrita no repositório (se faltar, o dono resolve antes).

Depois disso os dois lados andam **em paralelo**: o front trabalha com dados simulados do contrato; o back-end implementa o contrato e atualiza o `STATUS.md`. O que amarra os dois:

- **Passo 0** na `main` **antes** de o front começar: o plano do front tem a regra de nunca criar os arquivos da raiz; se faltarem, a sessão do front registra uma dúvida.
- **Tasks 12 a 14 da fatia 0** (CI, deploy, Azure) **só começam quando o front da fatia 0 estiver na `main`**: a CI tem um job do front, o deploy publica `front-end/` e a Task 14 edita `front-end/public/staticwebapp.config.json` e confere o ambiente pelo shell do front. Se o front atrasar e isso segurar o congelamento da base (que é também o prazo do Kauan), **o dono decide na hora** entre esperar e fazer ele mesmo o que seria do front na fatia 0 (design, seção 4). A fatia 1 **não** espera: pode andar com as Tasks 1 a 9; só o deploy dela espera.

## 5. Regras do contrato (contract-first)

1. **A API implementa `contrato/openapi.yaml`.** Esse arquivo é **gerado**: não se edita; muda-se `contrato/fontes/*.yaml` e roda-se `npm --prefix contrato run validar`.
2. **Teste de divergência.** Um teste de integração lê `contrato/openapi.yaml` e o documento OpenAPI que a API gera (o nativo do .NET 10) e falha quando as operações no escopo diferem (caminho, método, `operationId`, códigos de resposta, tipos de mídia, nomes e tipos de campo, `required`, e também o corpo `multipart/form-data` pelos nomes dos campos e `required`, com o campo de arquivo como `format: binary`; o comparador trata `allOf`, `oneOf` com `null` e `$ref`). Nasce na Task 8 da fatia 0, com o escopo `entrar`, `renovarSessao`, `sair` e `obterUsuarioLogado` (a Task 9 acrescenta `verificarSaude`). **O escopo é uma lista de `operationId` mantida no próprio teste, nunca prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`, e `/api/auth/senha/*` é da 1b); **cada tarefa que entrega rotas acrescenta os `operationId` dela no mesmo passo em que implementa as rotas, nunca antes** (uma operação do contrato no escopo que a API ainda não tem é divergência). Um segundo teste confere que toda operação que a API publica está no escopo. Estratégia e limites do teste: Task 8 da fatia 0.
3. **A API não inventa rota nem campo.** Se o back-end precisar de algo que o contrato não tem, ele **para e pergunta ao dono** (onde, o que falta, o que sugere); o dono muda o contrato, o `CHANGELOG.md` registra e a versão sobe (maior: tirar ou renomear rota ou campo, ou tornar um campo obrigatório; menor: acrescentar rota ou campo opcional; correção: texto ou exemplo). Só depois se implementa.
4. **`contrato/STATUS.md` é do dono** e tem **duas partes**: a **tabela por fatia que já existe** (é a que o front lê para saber se a fatia está publicada) e, **abaixo dela**, uma seção **"Rotas implementadas"** (`operationId` → situação: `em andamento` ou `pronta`), criada na fatia 1a. **A própria tarefa que entrega uma rota a acrescenta na seção**, no mesmo passo em que a implementa (o dono revisa no PR), e **um teste confere que a seção lista toda rota que a API implementa**. `pronta` quer dizer implementada, testada e publicada no ambiente da demo (`contrato/STATUS.md:5`): quem passa a `pronta` é o dono, depois do deploy. O front consulta o arquivo para saber se um passo de integração já pode rodar.
5. **A CI vigia o contrato** (job `contrato`, Task 12 da fatia 0): monta, roda o lint do Redocly e valida os exemplos contra os schemas (hoje 1.471 exemplos, medidos em 2026-10-30 com `node validar-exemplos.mjs openapi.yaml`).
6. **Itens provisórios** (`contrato/PROVISORIOS.md`) dependem de decisão do dono ainda aberta. O back-end implementa o que está escrito; ao mudar a decisão, o contrato muda (e a versão sobe).
7. **Erros e códigos são os do contrato**: `application/problem+json` em português com acento, sem stack trace, os mesmos `codigo` em todas as fatias (`contrato/CONVENCOES.md`, seções 5 e 11). Perfil errado na rota é 403; objeto fora do escopo da pessoa é 404.

## 6. Fatia 0: quem faz o quê

| Task (plano antigo, numeração mantida) | O que é | Agora |
|---|---|---|
| 0 (nova) | Passo 0: arquivos da raiz e renormalização | este plano; antes de o front começar |
| 1 | Pré-requisitos da máquina e conferências de documentação | dono, com o assistente conferindo |
| 2 | Solution em camadas e testes de arquitetura | este plano (não cria mais os arquivos da raiz) |
| 3 | `docker compose` e README | este plano (o README já nasce no Passo 0) |
| 4 | Scripts SQL (`V001` a `V005`) e executor | este plano; o executor embute a pasta inteira |
| 5 a 7 | Domínio, mapeamento do EF, casos de uso, infraestrutura de autenticação | este plano (agora com `cargo` e `setor`) |
| 8 | API de autenticação | este plano; implementa o contrato; sonda do documento OpenAPI nativo e teste de divergência (escopo por `operationId`) |
| 9 | Limite de tentativas, proxy, CORS, saúde, documentação, seed | este plano |
| **10 e 11** | Projeto do front, cliente de API, login, shell, cabeçalhos de segurança | **saíram: estão em `docs/plano-front-end/fatia-0.md`** |
| 12 a 14 | CI, deploy manual, Azure guiado e congelamento da base | dono, guiado; **só depois do front da fatia 0 na `main`** |

## 7. O esquema do banco (V001 a V005) e a regra de numeração

**Atualização de 2026-11-03.** As mudanças que a aplicação precisava no banco (24 pedidas ao Kauan, em `docs/pedidos-para-o-kauan.md`, e 18 escritas em 2026-10-30 a partir do contrato, para **todo o esquema ficar pronto antes de o back-end começar**) estavam em 42 scripts (`V006` a `V047`). O dono decidiu juntá-las antes do primeiro deploy: hoje estão **dentro do `V001`** (tabelas, colunas, regras e índices) e do **`V002`** (a exceção de férias no trigger de holerite). A pasta tem `V001` a `V005`; o próximo script é o **`V006`**. O texto original de cada script antigo está no commit `10fe79a` e, copiado sem mudança, nas **notas dos pedidos** no fim do `V001` (procure "(antigo V0nn)"). A conferência (os 47 scripts antigos contra a base nova, num MySQL 8.4) mostrou os dois bancos iguais: ver `Banco de Dados/Scripts/LEIA-ME.md`, "Como a base nova foi conferida". Resultado: **48 tabelas** (mais `SCHEMA_VERSOES`, que o executor cria), **36 triggers e 8 funções**. **A aplicação não criou trigger novo.**

| Pedido (antigo script) | O que faz | Fatia que usa (minha leitura) |
|---|---|---|
| (antigo V030) | matrícula gerada pelo sistema (tabela `SEQUENCIAS` e `CHECK` de 5 dígitos em `FUNCIONARIOS.MATRICULA`) | 1b e 2 |
| (antigo V031) | `BENEFICIOS`: `OPCIONAL`, `ACEITA_DEPENDENTES`, `ACEITA_TRAJETO` | 1c e 7 |
| (antigo V032) | `USUARIOS.SENHA_PROVISORIA` (convite ainda não aceito) | 1b |
| (antigo V033) | `USUARIOS_TOKENS_SENHA` (convite e redefinição: só o hash, uso único) | 1b |
| (antigo V034) | `DESLIGAMENTOS_AGENDADOS` (agendar, efetivar, cancelar, readmitir) | 1b |
| (antigo V035) | `FILA_EMAILS` (idempotência, vazão, carga apagada ao terminar, lembrete anônimo sem destinatário) | 1a |
| (antigo V036) | `TAREFAS_AGENDADAS` (estado idempotente) | 1a |
| (antigo V037) | `AUDITORIA_EVENTOS` e `AUDITORIA_MUDANCAS` (só inclusão, `VALOR_OCULTO`) | 1a |
| (antigo V038) | `IMPORTACOES` (prévia em JSON, validade de 1 hora, tudo ou nada) | 2 |
| (antigo V039) | `PONTOS_MARCACOES.DATA_CORRECAO` (quando a batida foi corrigida) | 3 |
| (antigo V040) | um pedido de ajuste de ponto pendente por dia (coluna calculada e chave única) | 3 |
| (antigo V041) | `FERIAS.MOTIVO_RECUSA` (obrigatório ao recusar) | 7 |
| (antigo V042) | `TREINAMENTOS.ENCERRADO_EM` | 6 |
| (antigo V043) | `AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM` do treinamento nascem `FALSE` | 6 |
| (antigo V044) | `TREINAMENTOS_MODULOS.NOME_ARQUIVO_ORIGINAL` | 6 |
| (antigo V045) | `FORMULARIOS.LEMBRETE_AUTOMATICO` | 5 |
| (antigo V046) | arquivo na resposta do formulário (`CAMINHO_ARQUIVO` e `NOME_ARQUIVO_ORIGINAL`; anônimo nunca tem arquivo) | 5 |
| (antigo V047) | 17 índices para os filtros do contrato | todas |

(As descrições vêm de `Banco de Dados/Scripts/LEIA-ME.md`. A coluna da direita é minha leitura dos nomes e do roadmap; o `LEIA-ME` não a traz.)

**Regras de numeração e de conferência:**

- **O dono numera.** O grupo envia o conteúdo; o dono dá o número, para não haver dois `V006`. O nome precisa seguir `V{nnn}__{nome}.sql`, que é o que o executor aceita.
- **O Kauan confere até o congelamento** (Task 14 da fatia 0): o `V001` e o `V002`, comparando com os arquivos dele (cada grupo de colunas acrescentado tem um comentário com o título do pedido, e as notas no fim do `V001` explicam cada um). O `LEIA-ME` ("O que o Kauan deve conferir") diz o que pede a atenção dele. O dono disse que avisa o Kauan; **o `V001` e o `V002` mudaram de forma em 2026-11-03, então o aviso tem de incluir isso** (o assistente não fala com o Kauan).
- **Até o congelamento (Task 14 da fatia 0, o primeiro deploy)**, o `V001` e o `V002` podem ser corrigidos no lugar, sem script novo (dono, 2026-10-29), **mas só avisando o dono** (que avisa o Kauan); os bancos locais se recriam (`docker compose down -v`). **Depois da Task 14**, todo script aplicado fica congelado (`CONGELADOS.sha256`, Task 14, Step 9; o hash normaliza CRLF para LF e dá o mesmo valor no PowerShell e no Linux, Task 12) e **só script novo vale**: `ALTER` para tabela; `DROP` e `CREATE` para trigger e função. **`V001` a `V005` não estão congelados antes disso**: a fatia 1 começa com as Tasks 1 a 9 da fatia 0 passando e, se achar que falta coluna, índice ou tabela, **para e pergunta ao dono** em vez de editar.

## 8. Decisões de engenharia que atravessam fatias (já tomadas; não reabrir)

Todas já estão escritas nos documentos de origem; aqui só o lembrete de onde.

- **Fuso:** `time_zone` do servidor `-03:00`; as tabelas do grupo e as colunas que os scripts acrescentam a elas ficam em `-03:00`; as tabelas só da aplicação (`REFRESH_TOKENS`, `SCHEMA_VERSOES` e as tabelas da aplicação do `V001`: tokens de senha, desligamentos, fila de e-mails, tarefas agendadas, auditoria e importações) ficam em **UTC**, e a nota de cada pedido, no fim do `V001`, diz o fuso das suas colunas. A aplicação converte na fronteira e **deixa vazias** as colunas que os triggers preenchem. A API fala UTC (`...Z`) (plano da fatia 0, "Fuso horário"; roadmap, decisão 4; `contrato/CONVENCOES.md`, seção 3; `LEIA-ME.md`, seção «Armadilhas novas»). **Nas tabelas só da aplicação, quem preenche as datas é a aplicação, sempre, a partir de `IRelogio` (UTC)** (`CRIADO_EM`, `EXPIRA_EM`, `QUANDO`...): o `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança, porque o `CHECK` `EXPIRA_EM > CRIADO_EM` quebra com relógio falso se o padrão do banco gravar a data real.
- **`CHECK` e `NULL`:** no MySQL um `CHECK` que dá `NULL` passa; os `CHECK` novos usam `COALESCE` quando precisa (`LEIA-ME.md`, seção «O que eu gostaria que você questionasse»).
- **DDL não faz rollback:** um script com várias instruções que falha no meio deixa o banco parcial; por isso `ALTER` numa instrução só quando possível (`LEIA-ME.md`, seção «Numeração»; `FalhaNoScriptException`, Task 4).
- **UUID:** o do banco é a versão 1; a aplicação troca por v4 onde o contrato ou o `CHECK` pedem (participação anônima, `V001`; certificado, `LEIA-ME.md`, seção «Armadilhas novas»).
- **Mapeamento do EF** para colunas calculadas, geradas na inclusão e com padrão no banco (roadmap, decisão 12; `LEIA-ME.md`, "Armadilhas novas"; sonda na Task 5 da fatia 0). **Nunca declarar o padrão do banco** onde a aplicação grava o valor "falso".
- **Auditoria** por interceptor mais **eventos explícitos** para efeito de trigger e operação em lote (roadmap, decisão 5); a tabela só aceita inclusão **por regra da aplicação, sem trigger agora** (`V001`): um trigger de imutabilidade impediria a limpeza de dados vencidos, e o dono pode acrescentar um depois, por script novo (`LEIA-ME.md`); senha, hash, token e valor sensível ficam fora do log. **No login**, a senha errada de um usuário que existe registra um evento `ACESSO_NEGADO` (sem a senha, com o IP), no máximo **um por pessoa por minuto** (vale também para quem ainda tem senha provisória); e-mail inexistente **não** é registrado (não poluir o log nem guardar e-mail digitado errado).
- **E-mail:** fila no banco com chave de idempotência (ASCII, começa por `TIPO:`, única; o erro 1062 é "já existe" e vale como sucesso) e **lista fechada** de e-mails (`contrato/CONVENCOES.md`, seção 12). Nenhum outro e-mail existe.
- **Agendador** em segundo plano, recuperando o que ficou para trás e idempotente; exige Always On (roadmap, decisão 3; `V001`).
- **Conteúdo escrito por usuário** é texto ou markdown, nunca HTML; **CSV** neutraliza células que começam com `=`, `+`, `-` ou `@` (roadmap, decisões 6 e 9).
- **Arquivos:** envio por `multipart/form-data`; download sempre por link temporário e como anexo, nunca pela API de negócio (`contrato/CONVENCOES.md`, seção 6).
- **Matrícula:** `UPDATE SEQUENCIAS ... LAST_INSERT_ID(ULTIMO_VALOR + 1)` na **mesma transação e conexão** do cadastro, sem buraco (`V001`).
- **Aprovação em duas etapas** (ponto e férias): um só modelo, `etapa`, `podeDecidir`, `decisaoGestor` e `decisaoRh` (`contrato/CONVENCOES.md`, seção 9).
- **Banco de horas:** converter **cada linha** para minutos e depois somar; somar horas e converter no fim erra (`LEIA-ME.md`, seção «Armadilhas novas»).
- **Permutar `ORDEM`** (perguntas, alternativas, módulos): somar 1000 e depois gravar o valor final, porque o `CHECK` recusa negativo (`LEIA-ME.md`, seção «Armadilhas novas»).
- **Sem triggers novos** nas tabelas da aplicação (`LEIA-ME.md`, "Padrões usados"). Mensagem de trigger do grupo vira 422 com `detail` genérico e a mensagem original em `regraDoBanco`; cada fatia decide quais traduz.
- **Anonimato** (formulários): vale só contra quem usa a aplicação; mínimo de respostas por grupo; nunca mostrar a ordem de gravação (roadmap, fatia 5).

## 9. Roteiro das fatias 1 a 8

O detalhe de cada fatia está no roadmap; o contrato, em `contrato/fontes/`. **Antes de começar cada fatia: escrever o plano dela** com a skill `superpowers:writing-plans`, passar pelo `revisor-de-plano`, e conferir as "Decisões do dono" daquela seção do roadmap.

| Fatia | Contrato (rotas) | Objetos do banco (no `V001`) | Incrementos e pontos de atenção |
|---|---|---|---|
| **1** colaboradores e base | `20-fatia-1-colaboradores.yaml` (43), mais 3 rotas `contadores-do-menu` do `70-fatia-7-gestao.yaml` (46 no total) | `FUNCIONARIOS.MATRICULA` e `TIPO_CONTRATO`, `CONFIGURACOES_EMPRESA`, `FUNCIONARIOS_DOCUMENTOS`, `SEQUENCIAS`, `BENEFICIOS` (marcas), `USUARIOS.SENHA_PROVISORIA`, `USUARIOS_TOKENS_SENHA`, `DESLIGAMENTOS_AGENDADOS`, `FILA_EMAILS`, `TAREFAS_AGENDADAS`, `AUDITORIA_*` | **1a** fundação (escopo por perfil, auditoria, fila de e-mail, agendador); **1b** cadastro, ficha, convite, redefinição e troca de senha, desligamento agendado (o banco inativa a pessoa assim que há data de desligamento); **1c** cargos, setores, benefícios, empresa e documentos (primeiro Blob). Matriz de perfis por rota. **As três rotas `GET /api/eu/contadores-do-menu`, `GET /api/gestor/contadores-do-menu` e `GET /api/rh/contadores-do-menu`** (`obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoRh`; o contrato as põe na fatia 7) **entram na 1a retornando 0** até a fatia dona de cada contador registrar a fonte; no `STATUS.md` ficam como parciais. **Ponta a ponta (Playwright):** é um **passo de integração da fatia 1**, feito **depois de o front da fatia 1 estar na `main`**, num diretório `e2e/` na raiz **criado pelo dono**; o detalhe só é escrito nessa hora (os fluxos que o front espera estão na ficha 8, `docs/plano-front-end/fichas/fatia-8.md`, seção 3.1). |
| **2** importação por Excel | `25-fatia-2-importacao.yaml` (3) | `IMPORTACOES` | Transação única (até 500 linhas), limite de tamanho descompactado do `.xlsx`, prévia com dado pessoal e validade de 1 hora, matrícula reservada em bloco. Biblioteca de Excel e colunas do modelo: decisões do dono. |
| **3** ponto | `30-fatia-3-ponto.yaml` (34) | `PONTOS_MARCACOES` (origem, data da correção), `PONTOS_MANUTENCOES` e `PONTOS_MANUTENCOES_ITENS` (itens, duas etapas, um pendente por dia), `BANCOS_HORAS`, `GESTORES` (aprovação automática), `ATESTADOS`, `FERIADOS` | **3a** bater ponto e espelho; **3b** ajustes em duas etapas e atestado; **3c** feriados, banco de horas (consolidação idempotente, recupera dias parados, considera férias e atestado), exportação para a folha, espelho em PDF. Sequência de batidas e cálculo de horas ficam na aplicação, apoiados nas funções do grupo. Implementa, para os pedidos de ponto, `IPedidosParadosNoGestorDaFatia` e a registra (o composto da 1a a soma com a das férias; a 1b e a 1c chamam a porta `IPedidosParadosNoGestor`; decisão 5 da seção 12). |
| **4** comunicação e documentos | `40-fatia-4-comunicacao-e-documentos.yaml` (35) | `HOLERITES` (tipo e versão; a exceção de férias do trigger está no `V002`), `AVISOS` (`FIXADO`, `ENVIADO`), `ADVERTENCIAS` (ciência, contestação) | **4a** avisos (rascunho: o trigger grava `NOW()` em `DATA_PUBLICACAO`; `LEIA-ME.md`, seção «Armadilhas novas»); **4b** holerites (o banco proíbe apagar: nova versão; PDF no Blob; lote); **4c** advertências com ciência e contestação, aniversariantes. |
| **5** formulários | `50-fatia-5-formularios.yaml` (32) | `FORMULARIOS*` (publicação, prazo, recorrência, anônimo, alternativas, respostas de candidato, anônimas e com arquivo, destinatários, participações), `LEMBRETE_AUTOMATICO` | **5a** construtor; **5b** responder e acompanhar; **5c** link público e entrevista (superfície de segurança à parte); **5d** avaliações por ciclo. Anonimato, recorrência, excluir filhos na ordem do contrato. |
| **6** TAD (treinamentos) | `60-fatia-6-tad.yaml` (31) | `TREINAMENTOS*` (módulos e progresso, publicação, público, certificado, `ENCERRADO_EM`, `NOME_ARQUIVO_ORIGINAL`, marcas com padrão `FALSE`) | **6a** treinamentos e módulos; **6b** progresso e certificado (o trigger cria o certificado com UUID v1: a aplicação troca por v4; PDF); o roadmap previa **6c** "lembretes", mas pelo contrato não há lembrete nem prazo de treinamento (`contrato/CONVENCOES.md`, seções 12 e 13; sobra encerrar e excluir com `confirmar=true`): o plano da fatia 6 reagrupa os incrementos. Sem nota. |
| **7** gestão | `70-fatia-7-gestao.yaml` (41; **3 delas, as `contadores-do-menu`, já entram na 1a**, restando 38) | `FERIAS` (etapa do gestor, 13º, sugestão de data, motivo da recusa), `BENEFICIOS_FUNCIONARIOS`, `DEPENDENTES`, `TRAJETOS_VALE_TRANSPORTE`, `PAGAMENTOS_EXCECOES`, `GESTORES` | **7a** férias; **7b** benefícios e dependentes; **7c** pagamento e calendário; **7d** painéis (dependem das fatias 3 a 6). O agendador leva `APROVADA` a `EM_GOZO` e `CONCLUIDA`. Implementa, para os pedidos de férias, `IPedidosParadosNoGestorDaFatia` e a registra (decisão 5 da seção 12); registra também a fonte de férias nos contadores do menu. |
| **8** fechamento | (sem rotas novas) | | DER, seed fictício da demo, testes de ponta a ponta, roteiro e ensaio da banca. |

**Contadores do menu.** O menu precisa deles desde a fatia 1, embora o contrato os ponha na fatia 7: por isso as três rotas entram na **1a retornando 0**, e **cada fatia dona de um contador registra a fonte dele** (documentos pendentes: 1c; ponto: 3; avisos: 4; formulários: 5; férias: 7). No `contrato/STATUS.md` as rotas aparecem como parciais enquanto faltar alguma fonte.

## 10. O que ainda é decisão do dono

Só o que muda o que ele decide. O que for de engenharia a sessão resolve e conta depois.

1. **Branch e PR da fatia 0:** de qual branch parte e **quem aprova**. A `main` só aceita PR com 1 aprovação de outra pessoa com permissão de escrita, e o `CODEOWNERS` só tem o dono (`docs/retomada-da-conversa.md:79`; `.github/CODEOWNERS`). Vale também para o Passo 0: sugestão da sessão, **a decidir**, é fazê-lo como o último commit do PR do planejamento (poupa uma aprovação) em vez de um PR próprio.
2. **Método de execução:** subagentes ou na própria sessão (vale para todos os planos de fatia).
3. **Conta do Azure com os créditos do SENAI** e se aceita um plano pago do App Service (o Always On, de que o agendador depende, exige nível pago); decidido na Task 14.
4. **O Kauan conferir o `V001` e o `V002` antes da Task 14**, e o dono avisá-lo do prazo. Depois da Task 14 qualquer correção vira script novo.
5. **Se o front da fatia 0 atrasar** e segurar as Tasks 12 a 14: esperar ou fazer ele mesmo o que seria do front (seção 4).
6. **Contrato:** o dono ainda vai vetar ou aceitar as escolhas de engenharia do contrato e responder o mínimo de respostas por avaliado (`contrato/CHANGELOG.md`, versão 0.2.0); a versão **1.0.0** é a da publicação na `main`.
7. **Decisões abertas do roadmap**, uma a uma, no começo da fatia que as usa (lista em `contrato/PROVISORIOS.md`).

## 11. Cobertura do design (seção 4) por este plano

| Ponto do design | Onde está |
|---|---|
| Por etapas: fatias 0 e 1 completas, o resto roteiro | seções 1, 3 e 9 |
| Passos 1 a 9 da fatia 0 ficam; 10 e 11 saem; 12 a 14 esperam o front | seção 6 e plano da fatia 0 |
| Contrato primeiro e teste de divergência | seção 5; Task 8 da fatia 0 |
| Plano da fatia 0 sem `contrato/sessao.exemplo.json` | Tasks 8 e 10 do plano da fatia 0 (a 8 usa o contrato; a 10 saiu) |
| Passo 0 | Task 0 da fatia 0 |
| Integração e ponta a ponta ficam com o dono; `STATUS.md` do dono | seção 5, regra 4; fatia 1 (ponta a ponta, depois de o front da fatia 1 estar na `main`) e fatia 8 |
| Decisões que não se reabrem | seção 8 |
| Decisões da revisão conjunta dos planos (as três fatias 1a, 1b e 1c as seguem) | seção 12 |

## 12. Decisões da revisão conjunta dos planos (2026-10-30)

O agente `revisor-de-plano` leu os cinco planos juntos (esta entrada, a fatia 0, a 1a, a 1b e a 1c) e achou contradições entre eles, porque a 1b e a 1c foram escritas em paralelo, sem se ver. As decisões abaixo as resolvem. **As três fatias 1a, 1b e 1c seguem esta tabela**, e a fatia 0 e esta entrada também, onde são donas da peça. Quando um plano disser outra coisa, **vale esta seção** e o plano é corrigido. Cite como "decisão N da seção 12". A coluna "Quem" diz qual plano **define** a peça; os outros só a consomem.

| N | Decisão | Quem |
|---|---|---|
| 1 | **Escopo do teste de divergência = lista de `operationId`** mantida no próprio teste, **nunca prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`; `/api/auth/senha/*` é da 1b). **Cada tarefa que entrega rotas acrescenta os `operationId` dela no mesmo passo em que implementa as rotas**, nunca antes (a regra "operação do contrato no escopo que a API não tem é divergência" falharia no meio da fatia). O comparador recebe o conjunto de `operationId`, não um `Func<string,bool>` de caminho. Nenhum plano escreve contagem de caminhos. | fatia 0 (Task 8) |
| 2 | O **comparador e o validador de JSON** tratam **`allOf`**: unem `properties` e `required` dos membros (resolvendo `$ref`) antes de comparar. O contrato usa `allOf` com `Paginacao` em toda lista paginada. Teste com um `allOf` de `Paginacao` mais `itens`. | fatia 0 (comparador) e 1a (validador) |
| 3 | **`POST /api/eu/senha` mantém o 429** do contrato: 5 tentativas de senha atual errada por minuto por pessoa, com `Retry-After`. | 1b |
| 4 | **Dono de cada peça de domínio**: a **1b** cria `NivelDeAcesso : IAuditavel` e `NomesDeNivel` (com `DoPerfil(Perfil)`), `Cargo.CargaSemanalHoras` e o mapeamento de `CARGA_HORARIA`, **`Setor.DefinirGestor(int? idGestor)`** (id de `GESTORES`), o record **`GestorReferencia`** (em `Application.Colaboradores`), a entidade **`BeneficioDoFuncionario`** (`BENEFICIOS_FUNCIONARIOS`) e **`IGestorDoFuncionario.RefazerDosMembrosAsync`**. A **1c só consome**: `VinculoDeBeneficio` vira `BeneficioDoFuncionario`; **`criarSetor` e `editarSetor` chamam `RefazerDosMembrosAsync`**; usa `GestorReferencia` e `NomesDeNivel.DoPerfil`. A **1a** cria as portas e os ajudantes. | 1b (dona), 1c (consome) |
| 5 | **Porta `IPedidosParadosNoGestor`** (dono: **1a**, Task 13): `Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken)` (ligar "Gestor ausente"; devolve `PedidosAprovados(AjustesDePonto, Ferias)`, a **soma** do que cada fatia aprovou agora, que a 1c repete em `pedidosAprovadosAgora`; sem fatia registrada devolve 0 e 0) e `Task PassarParaORhAsync(int gestorId, CancellationToken)` (o gestor deixa o setor, é trocado ou é desligado). **Uma só porta e um composto:** a 1a registra `PedidosParadosNoGestorComposto` como `IPedidosParadosNoGestor` (vazio por padrão); as fatias 3 e 7 **não trocam a porta**: cada uma implementa `IPedidosParadosNoGestorDaFatia` e a registra, e o composto as junta. **Quem chama (1b e 1c) injeta só `IPedidosParadosNoGestor`**, nunca `IEnumerable<...>`. **A etapa do pedido é fixada na criação** (`contrato/CONVENCOES.md`, seção 9): "passar ao RH" é uma **ação explícita** de quem tira o gestor. A **1b** a chama ao **efetivar o desligamento** de um gestor e na **readmissão**; a **1c** ao **trocar ou tirar o gestor de um setor** e ao ligar "Gestor ausente" (`AprovarEtapaDoGestorAsync`). As fatias 3 e 7 implementam a delas sobre as tabelas de pedido; a dívida fica registrada na seção "Para as fatias 2 em diante" e no `STATUS.md`. | 1a (porta, composto e `PedidosAprovados`), 1b e 1c (chamam) |
| 6 | **Texto de 400:** só o `entrar` usa "Preencha o e-mail e a senha."; todo outro 400 usa `title` "Dados inválidos." e `detail` "Confira os campos indicados." (mais o mapa `errors`). O contrato (0.2.1) traz esse texto no exemplo `Erro400Validacao` e o do login só em `Erro400Login` (rota `entrar`): o teste da 1a confere `title` e `detail` iguais aos do contrato também no 400 (`Erro400Validacao`), e o da fatia 0 confere o `Erro400Login` no `entrar`. | 1a (`TextosDeErro`) e fatia 0 (`renovar`, `sair`) |
| 7 | **Datas das tabelas só da aplicação:** a aplicação as preenche **sempre**, a partir de `IRelogio` (UTC): `CRIADO_EM`, `EXPIRA_EM`, `QUANDO` etc. O `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança (o `CHECK` `EXPIRA_EM > CRIADO_EM` quebra com relógio falso se o padrão do banco gravar a data real). Os testes com relógio falso esperam os valores do relógio falso. | todas |
| 8 | **Enums de consulta:** `EnumsDeConsulta` (ligar `?area=ACESSO_NEGADO` pelo nome do contrato) é **da 1a** (Task 4). As rotas de lista da auditoria usam **um objeto de consulta próprio**, só com os parâmetros que o contrato declara (sem `ordenarPor` e `direcao`); `[AsParameters]` de `ParametrosDeLista` só onde o contrato declara `pagina`, `tamanhoPagina`, `busca`, `ordenarPor` e `direcao`. | 1a |
| 9 | **O documento OpenAPI e a UI da documentação ficam anônimos** (fora das famílias, sem login) quando `Documentacao:Habilitada`; a regra de reserva da 1a, que fecha por omissão, **exclui** `/openapi/*` e a UI. Há um passo e um teste que dizem isso. | fatia 0 (Task 9) e 1a |
| 10 | **Scripts** `V001` a `V005`: "**não se editam sem avisar o dono; depois da Task 14 (primeiro deploy) só script novo**" (e não "congelados" já). A fatia 1 pode começar com as Tasks 1 a 9 da fatia 0 passando, sem esperar o front nem o deploy. | entrada e fatia 0 |
| 11 | **`STATUS.md`**: mantém a **tabela por fatia que já existe** (a que o front lê) e ganha **abaixo** uma seção "Rotas implementadas" (`operationId` → situação); o teste confere que a seção lista toda rota implementada. **Quem edita** é a própria tarefa que entrega a rota (o dono revisa no PR). As três rotas `contadores-do-menu` (contrato fatia 7) entram na **1a** retornando 0 até a fatia dona registrar a fonte, e aparecem na tabela da fatia 1 da seção 9 com essa observação. | 1a (seção e teste), entrada (tabela) |
| 12 | **Fila de e-mails:** `CancelarPendentesAsync` **apaga** (`DELETE`) as linhas **sigilosas** (lembrete de formulário anônimo) em vez de marcá-las `CANCELADO` (o `CHECK` de `V001` recusa). | 1a |
| 13 | **Auditoria no banco:** **sem trigger agora** (um trigger de imutabilidade impediria a limpeza de dados vencidos; o dono pode acrescentar depois, por script novo, `LEIA-ME.md`). **Login:** o evento de **senha errada de usuário que existe** (`ACESSO_NEGADO`, sem a senha, com IP) é registrado, com **limite** de um evento por pessoa por minuto (o login de quem existe mas ainda tem **senha provisória**, convite não aceito, gera o mesmo evento e a mesma resposta 401 genérica: 1b, Task 4); e-mail inexistente **não** é registrado. | 1a (e seção 8 desta entrada) |
| 14 | **Convite a login desativado:** 422 `USUARIO_DESATIVADO` (o contrato já tem; o RH usa "Ativar" antes). O convite não reativa o login. | 1b |
| 15 | **Benefício que já tem linha** (`PENDENTE`, `RECUSADO` ou `CANCELADO`) quando o cadastro ou a edição concede: a API **atualiza a linha existente** para ativa (chave única `(ID_FUNCIONARIO, ID_BENEFICIO)`, `Criacao_Tabelas:562`) em vez de inserir; com teste. | 1b |
| 16 | **Upload de documento:** valida as regras no banco (leitura) → **grava no Blob** → **uma transação curta** que insere a linha; se o `INSERT` falhar, apaga o blob; sobras limpas por tarefa agendada. Nenhuma transação fica aberta durante o envio de arquivo. | 1c |
| 17 | **Troca de senha e `rth`:** a claim `rth` (hash do refresh da sessão) mantém a sessão atual; **limitação documentada e testada**: se outra aba renovou o refresh antes, o token de acesso ainda carrega o `rth` antigo e a sessão dessa aba cai na próxima renovação (raro; aceito). | 1b |
| 18 | **Tamanho da chave de idempotência:** `ChaveDeIdempotencia` aceita até **50** caracteres de tipo (igual a `TAREFAS_AGENDADAS.TIPO`). | 1a |
| 19 | **CORS** expõe `Content-Disposition` e `Retry-After` (`WithExposedHeaders`). | fatia 0 (Task 9) |
| 20 | **Hash de congelamento** (`CONGELADOS.sha256`): **normaliza CRLF para LF antes de hashear** e usa o **mesmo algoritmo no PowerShell e no Linux da CI**; o plano traz os dois comandos e um passo que prova que dão o mesmo valor. | fatia 0 (Tasks 12 e 14) |
| 21 | **Ponta a ponta (Playwright):** **passo de integração da fatia 1**, feito **depois de o front da fatia 1 estar na `main`**, num diretório `e2e/` na raiz criado pelo dono; **detalhe só nessa hora** (ficha 8 do front, `docs/plano-front-end/fichas/fatia-8.md`). | entrada (seção 9) |
