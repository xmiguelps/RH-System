# Fatia 0 (esqueleto) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar o esqueleto do RH-System, com API em camadas, banco MySQL mantido por scripts SQL versionados, login com JWT, shell do React, CI e deploy no Azure, tudo rodando de ponta a ponta.

**Architecture:** Monólito modular em Clean Architecture (`back-end/`) e React + TypeScript (`front-end/`), no mesmo repositório. O esquema do MySQL vem dos scripts SQL do grupo (`Banco de Dados/Scripts/`), aplicados por um executor próprio com hash de integridade; o EF Core só mapeia e consulta. A autenticação usa JWT de vida curta e refresh token com rotação, guardado só como hash.

**Tech Stack:** .NET 10, EF Core 9.x (sem migrations), `Pomelo.EntityFrameworkCore.MySql` 9.0.0, MySqlConnector, MySQL 8.4, xUnit, Testcontainers, React + TypeScript + Vite, Vitest + Testing Library + Prettier, GitHub Actions, Azure (App Service Linux, Static Web Apps, MySQL Flexible Server).

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seções 2, 3, 4, 5, 7 e 8; a fatia 0 é a linha 0 da tabela da seção 8); design dos planos do back-end e do front-end, `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (seções 3 e 4); contrato da API, `contrato/openapi.yaml` (gerado de `contrato/fontes/*.yaml`; o que vale para a fatia 0 está em `contrato/fontes/10-fatia-0-autenticacao.yaml`).

> **Nota de 2026-11-03:** os scripts `V006` a `V047` citados nas Tasks já executadas (0 a 11) e nas medições datadas deste plano foram incorporados ao `V001` e ao `V002` (ver `docs/superpowers/plans/2026-11-03-base-do-esquema.md`); o cabeçalho e as Tasks 12 a 14, que ainda não rodaram, foram atualizados.
>
> **ALERTA (2026-10-30), para o dono:** o Kauan tem **até a Task 14** para conferir o `V001` e o `V002` (as mudanças que a aplicação precisava, 42 scripts escritos pelo lado da aplicação, foram juntadas neles em 2026-11-03; `Banco de Dados/Scripts/LEIA-ME.md` tem o mapa, e as notas dos pedidos estão no fim do `V001`). **Até a Task 14, um script só se edita avisando o dono** (os bancos locais se recriam); a Task 14 **congela** `V001` a `V005` e **depois dela só script novo**. Avise o Kauan do prazo e de que o `V001` e o `V002` mudaram de forma (o assistente não fala com ele). **A fatia 1 (1a, 1b e 1c) pode começar com as Tasks 1 a 9 passando**, sem esperar o front nem as Tasks 12 a 14, e **as três seguem a tabela de decisões da revisão de 2026-10-30** (`docs/superpowers/plans/2026-10-30-plano-back-end.md`, seção 12); onde ela fala de peça desta fatia (teste de divergência, `allOf`, documento anônimo, CORS, hash de congelamento), vale o que está **neste** plano.
>
> **Mudanças de 2026-10-30:** as Tasks 10 e 11 (projeto do front, cliente de API, login, shell e cabeçalhos de segurança) **saíram deste plano** e estão em `docs/plano-front-end/fatia-0.md`; **as Tasks 12 a 14 só começam quando o front da fatia 0 estiver na `main`** (se o front atrasar e isso segurar o congelamento da base, o dono decide na hora entre esperar e fazer ele mesmo a parte do front; design, seção 4); há uma **Task 0** (Passo 0) antes da Task 1; o contrato da API é `contrato/openapi.yaml` (não existe mais `contrato/sessao.exemplo.json`). A entrada do plano do back-end é `docs/superpowers/plans/2026-10-30-plano-back-end.md`.
>
> **Correções da revisão conjunta (2026-10-30), feitas depois de o `revisor-de-plano` ler os cinco planos juntos:** o escopo do teste de divergência é uma **lista de `operationId`**, e não prefixo de caminho (Task 8); o comparador trata `allOf` e `oneOf` com `null` (Task 8); uma **sonda mede** o documento OpenAPI nativo antes do comparador (Task 8, Step 4); o documento e a UI da documentação ficam **sem login** e o CORS **expõe** `Content-Disposition` e `Retry-After` (Task 9); o 400 de `renovar` e `sair` usa o texto genérico (Task 8); o hash de congelamento **normaliza CRLF para LF** e dá o mesmo valor no PowerShell e no Linux (Tasks 12 e 14); o commit da Task 2 não varre `docs`.

## Global Constraints

- .NET 10 com **EF Core 9.x** (nenhum `Microsoft.EntityFrameworkCore.*` em 10.x) e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**. **Não há migrations do EF** nem `dotnet-ef`.
- Dependências: Api e Infrastructure dependem de Application; Application depende de Domain; **Domain não depende de nada**.
- Casos de uso são **classes simples**, sem biblioteca de mediação. Entidades do Domain mapeadas direto pelo EF (Fluent API na Infrastructure).
- **Banco por scripts SQL versionados** em `Banco de Dados/Scripts/` (`Vnnn__descricao.sql`). O executor aplica na inicialização, controlado por configuração, registra o hash de cada script e **recusa subir se um script já aplicado foi editado**. **Até o primeiro deploy no Azure (Task 14), um script só se edita avisando o dono** (e os bancos locais são recriados); **depois da Task 14 todo script aplicado fica congelado** e toda mudança é script novo (`ALTER` para tabelas; `DROP` e `CREATE` para trigger e função). A base do grupo **não está 100% pronta** (dono, 2026-10-28).
- Login por e-mail e senha, senha só como hash. JWT de vida curta; refresh token no banco, **revogável, com rotação e só como hash**. Tokens por **cabeçalho**, não cookie. Há limite de tentativas de login. O front tem **cabeçalhos de segurança** (feitos no plano do front, `docs/plano-front-end/fatia-0.md`; esta decisão vale para os dois lados).
- **Commits e PRs: nunca pôr o Claude como coautor** (nem `Co-Authored-By`, nem "Generated with Claude Code"; regra do dono, em `docs/retomada-da-conversa.md`). O ambiente de execução pode acrescentar essas linhas por padrão: removê-las de todo commit e descrição de PR. Só commitar e enviar quando o dono pedir.
- Segredos **nunca** vão para o repositório: user-secrets, `.env` ignorado, configurações do App Service e secrets do GitHub. **O assistente nunca vê os valores dos segredos de produção:** o dono os gera e cola direto no portal.
- Testes de integração usam **MySQL real em contêiner** (Docker), aplicando os **mesmos scripts**. Verificação de formatação no back-end e no front-end.
- Pastas `back-end/` e `front-end/`. Deploy no Azure **manual ou por tag**, nunca a cada push.
- O ambiente Azure só é criado **depois de o dono confirmar qual conta tem os créditos**. O custo de cada recurso é dito antes de criá-lo, e o dono nunca usou Azure: cada passo é guiado.
- **Os arquivos de `Banco de Dados/` são do grupo:** só mover ou editar com autorização do dono e aviso ao grupo. **Commit e push só com autorização do dono** (perguntar uma vez, no início, se pode commitar por tarefa).
- Nomes de domínio e API em português sem acento (`Usuario`, `Entrar`); mensagens ao usuário em português do Brasil com acento.
- Comandos deste plano rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; no PowerShell o `curl` é alias, use `curl.exe`).
- **O contrato é lei:** a API implementa `contrato/openapi.yaml` e um teste de divergência (Task 8) falha se o documento OpenAPI que ela gera divergir dele. Só o dono muda o contrato; cada mudança entra em `contrato/CHANGELOG.md` com versão. Faltando algo no contrato, **não se inventa rota nem campo**: pergunta-se ao dono.
- **`contrato/STATUS.md` é mantido pelo dono** e diz quais rotas existem de verdade; o front só liga na API real quando ele disser `pronta`.
- **Ordem entre back-end e front:** nada começa antes de contrato, planos e telas estarem na `main`; o **Passo 0 (Task 0)** vai para a `main` antes de o front começar; e as **Tasks 12 a 14 só começam quando o front da fatia 0 estiver na `main`**.

## Decisões deste plano (o spec não fixa)

Escolhidas aqui; o dono pode mudar na revisão:
- Token de acesso **15 min**; refresh **7 dias**, renovados a cada uso; limite de **5 tentativas de login por minuto por IP** (configurável); senha de no máximo **128 caracteres**.
- Refresh token no **`localStorage`** (decisão do dono: não perder o login). A renovação é serializada entre abas (`navigator.locks`), a aba que perde a corrida relê o armazenamento e tenta uma vez com o token novo, e o logout em uma aba vale para todas. O custo é que um XSS lê o refresh token; mitigado por CSP, acesso curto e rotação. **(2026-10-30)** O lado do navegador (`localStorage`, `navigator.locks`, evento `storage`) agora é feito no plano do front, `docs/plano-front-end/fatia-0.md`; o back-end entrega o que o contrato descreve em `entrar`, `renovar` e `sair`.
- Usuário inativo com a **senha certa** recebe 403 "Seu acesso está desativado. Fale com o RH." (decisão do dono); com a senha errada, a mesma resposta genérica.
- Refresh já usado (ou duas renovações simultâneas): a segunda recebe 401, **sem revogar os outros tokens** do usuário. Consequência aceita: um token de acesso vale por até 15 minutos depois de o usuário ser inativado.
- **Esquema:** as 25 tabelas (`V001`), os 36 triggers (`V002`) e as 8 funções (`V003`) vêm dos três arquivos do grupo (versão de 2026-10-28); a fatia 0 **mapeia só** `NIVEIS_DE_ACESSO`, `FUNCIONARIOS` (colunas `ID_FUNCIONARIO`, `NOME`, `DATA_ADMISSAO`, `ATIVO`, `EMAIL_CORPORATIVO`, `ID_NIVEL_ACESSO`, `ID_CARGO`, `ID_SETOR`), `CARGOS` (`ID_CARGO`, `NOME_CARGO`), `SETORES` (`ID_SETOR`, `NOME_SETOR`), `USUARIOS` e a nova `REFRESH_TOKENS` (`V004`); **`CARGOS` e `SETORES` entram (2026-10-30) porque `UsuarioResposta` traz `cargo` e `setor`** (`contrato/fontes/10-fatia-0-autenticacao.yaml:196-204`). O login é pelo `EMAIL_CORPORATIVO` do funcionário, e `USUARIOS` não tem nome nem e-mail. `V005` semeia (com `INSERT IGNORE`) os níveis `RH`, `Funcionario` e `Funcionario Gestor` (**nomes confirmados pelo dono em 2026-10-28**), mapeados pelo nome para `Perfil.RH`, `Perfil.Funcionario` e `Perfil.Gestor`. Funcionário **sem nível ou com nível desconhecido não entra**: 403 "Seu perfil de acesso não está configurado. Fale com o RH." e um log de aviso (o banco aceita `ID_NIVEL_ACESSO` e `EMAIL_CORPORATIVO` nulos).
- **ATUALIZAÇÃO 2026-10-29:** o dono autorizou a correção **direto no arquivo de triggers do grupo**, com comentário para o Kauan validar (remove os blocos de `HORA_*` e copia `ROUND(v_carga / 5, 2)`); o arquivo corrigido aplicou os 36 triggers num MySQL 8.4.13. O texto abaixo é o registro de 2026-10-28, quando a correção era do Kauan e eu a tinha desfeito a pedido do dono; ao chegar na Task 4, conferir se o Kauan subiu uma versão própria.
- **Scripts da aplicação (escritos em 2026-10-29 e 2026-10-30; juntados em 2026-11-03):** a aplicação escreveu 42 scripts (`V006` a `V029`, 24, e `V030` a `V047`, 18). Em 2026-11-03 o dono decidiu juntá-los dentro do `V001` e do `V002` antes do primeiro deploy (plano `docs/superpowers/plans/2026-11-03-base-do-esquema.md`). `Banco de Dados/Scripts/` tem hoje `V001` a `V005` e o `LEIA-ME.md` com o mapa (os números são do dono, que numera; o Kauan confere). O `ScriptsEmbutidos` embute **a pasta inteira**, então `NovoBancoComScriptsAsync()`, os testes da Task 4, o deploy (Task 14) e o congelamento incluem **os 5** (`V001` a `V005`). O prazo para o Kauan conferir o `V001` e o `V002` é, portanto, a Task 14. O ramo de decisão do Step 9 da Task 4 vale também para falha no `V001` (em 2026-11-03 o executor aplicou o `V001`, com 68 comandos, e o `V002`, com 72 e o trigger de `DELIMITER`, sem erro, com usuário restrito).
- **Conflito nos scripts do grupo (2026-10-28), registro histórico:** `TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE` usam `HORA_ENTRADA` e `HORA_SAIDA`, colunas removidas de `PONTOS` (as batidas foram para `PONTOS_MARCACOES`), e copiam a carga do cargo sem dividir (o cargo passa a ser semanal). A correção esperada remove o bloco de `HORA_*` (validação de saída e entrada e cálculo de `HORAS_TRABALHADAS`) e divide a carga por 5 ao copiar. **Eu fiz uma correção local e a desfiz, a pedido do dono, para não conflitar com a do Kauan** (o patch ficou guardado). `PONTOS.HORAS_TRABALHADAS` deixa de ser preenchida por trigger (as funções do grupo calculam pelas marcações). **Superado em 2026-10-29** (ver a atualização acima): o arquivo corrigido cria os 36 triggers (o original criava 13 e parava na linha 374, medido). O teste da Task 4 continua sendo a prova.
- **Erros de trigger na API:** viram 422 com `detail` genérico em português com acento ("A operação foi recusada por uma regra do banco de dados.") e a mensagem original do trigger (sem acento, técnica) numa extensão `regraDoBanco`; a tradução para textos amigáveis fica para as fatias que expõem cada regra.
- O admin do seed é um `Funcionario` (nome e e-mail da configuração, admissão = hoje, nível `RH`) mais o `Usuario`. **Sem cargo, sem setor e sem matrícula** (2026-10-30; decisão de engenharia): li que `FUNCIONARIOS.ID_CARGO` e `ID_SETOR` aceitam `NULL` (`Criacao_Tabelas:89-90`), que nenhum arquivo de `Banco de Dados/` semeia `CARGOS` nem `SETORES` (busca por `INSERT INTO` nos `.sql`: só feriados, `SEQUENCIAS`, `TREINAMENTOS_PUBLICO` e os que os triggers fazem), que o contrato aceita `cargo` e `setor` `null` (`10-fatia-0-autenticacao.yaml:203-204`) e que o `V030` (linhas 45-49) deixa o admin do seed sem matrícula. Então o `UsuarioResposta` do admin sai com `cargo: null` e `setor: null`, e o rodapé do menu do front não mostra cargo nem setor até a fatia 1c cadastrar cargos e setores e a ficha dele ser completada.
- **Executor de scripts próprio** (pequeno): a busca não confirmou como DbUp ou Flyway tratam `DELIMITER` em triggers, e a regra de hash de integridade é parte do requisito. O teste contra o arquivo real de triggers do grupo é quem prova.
- Os três arquivos do grupo viram `V001`, `V002` e `V003` **movendo-os para `Banco de Dados/Scripts/`** (com os ajustes descritos na Task 4). **Autorizado pelo dono em 2026-10-28**, que também avisa o grupo. Como o grupo envia arquivos pela interface web, a CI recusa `.sql` solto em `Banco de Dados/` fora de `Scripts/` e `Validacao/` (Task 12). **Os números de versão são do dono:** o grupo envia o conteúdo e o dono numera, para não haver duas `V003`.
- ~~Sem biblioteca de rotas nem de UI nesta fatia~~ **Superado em 2026-10-30:** o front deixou este plano e as bibliotecas dele (rotas, componentes, formulários, dados) foram escolhidas e estão em `docs/plano-front-end/02-pilha.md`. Imagem local `mysql:8.4`; Mailpit e Azurite entram no `docker compose` só nas fatias que os usam.
- App Service em **Linux**; Static Web Apps com origem de deploy "Other"; a API conecta ao MySQL com **usuário só do banco**.
- **Fuso horário (regra revista em 2026-10-29, com sonda):** o `time_zone` do servidor MySQL é `-03:00` (o Brasil não tem horário de verão) e **todo `DATETIME` das tabelas do grupo e dos `V006` a `V029` (e das colunas que o `V039` e o `V042` acrescentam a tabelas do grupo) fica em `-03:00`**, seja preenchido pelo banco (`NOW()` e `DEFAULT CURRENT_TIMESTAMP`) ou pela aplicação. A aplicação **converte na fronteira** (grava convertendo de UTC para `-03:00`, lê convertendo de volta, e a API fala UTC em ISO 8601) e, nas colunas que o trigger do grupo preenche (`DATA_SOLICITACAO`, `DATA_ANALISE`, `DATA_PUBLICACAO`, `DATA_CRIACAO`, `DATA_RESPOSTA` etc.), **deixa vazio** e deixa o banco preencher. **Medido** num MySQL 8.4 com `--default-time-zone=-03:00`: gravar `DATA_SOLICITACAO = UTC_TIMESTAMP()` e depois aprovar o pedido faz o `CHECK` `CHK_PONTOS_MANUTENCOES_DATAS` recusar (a data da análise, em `-03:00`, fica 3 horas antes da solicitação em UTC); deixando o banco preencher, ou gravando em `-03:00`, o pedido aprova. Tabelas criadas só para a aplicação (`REFRESH_TOKENS`, `SCHEMA_VERSOES` e as de `V033` a `V038`: tokens de senha, desligamentos agendados, fila de e-mail, tarefas agendadas, auditoria e importações, todas em UTC) podem guardar UTC, desde que **nenhum `CHECK` ou trigger compare** essas colunas com as do primeiro grupo; cada script novo diz o fuso de cada `DATETIME`. **Nessas tabelas a aplicação preenche sempre as datas**, a partir de `IRelogio` (UTC): `CRIADO_EM`, `EXPIRA_EM`, `QUANDO` etc.; o `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança (o `CHECK` `EXPIRA_EM > CRIADO_EM` quebraria com relógio falso se o padrão do banco gravasse a data real), e os testes com relógio falso esperam os valores do relógio falso. Sem o `time_zone` em `-03:00`, o admin criado às 22h receberia "admissão amanhã". O padrão do Flexible Server não foi verificado.
- **Documentação da API (OpenAPI):** o documento **nativo do .NET 10**, gerado pela própria API, sem nada para subir no Azure; ligada na demo (`Documentacao__Habilitada=true`). **(2026-10-30)** Ele é a **fonte do teste de divergência** contra `contrato/openapi.yaml` (Task 8); o contrato, e não o documento gerado, é a fonte de verdade.
- **Contrato OpenAPI (2026-10-30):** o contrato de todas as fatias existe em `contrato/` (OpenAPI 3.1.0, 224 operações, 5 delas na fatia 0). **Não existe mais `contrato/sessao.exemplo.json`**: a Task 8 não o cria; o que ele fixava (`SessaoResposta`) está em `contrato/fontes/10-fatia-0-autenticacao.yaml`. `UsuarioResposta` ganhou `cargo` e `setor`. Os textos de erro e os códigos são os do contrato. As **Tasks 10 e 11 foram para o plano do front**.

## Review Focus

1. **E-mail com maiúsculas ou espaços** autentica o mesmo usuário (Task 6).
2. **Script já aplicado que foi editado** e **dois executores ao mesmo tempo**: o executor recusa e aplica cada script uma vez só (Tasks 4 e 9).
3. **Refresh reaproveitado e renovações simultâneas** (duas abas no `localStorage`, `StrictMode`): nunca 500, no máximo um 200, o token novo do vencedor continua válido, e a aba que perde tenta de novo em vez de deslogar (Tasks 7 e 8 no back-end; a parte do navegador, a aba que perde a corrida, está no plano do front, `docs/plano-front-end/fatia-0.md`).
4. **E-mail inexistente, senha errada e inativo com senha errada** respondem com o mesmo status e o mesmo `title`/`detail`; só o inativo com senha certa recebe o 403 informativo (Tasks 6 e 8).
5. **Token malformado, expirado ou ausente, erro inesperado em `Production`, erro de trigger do banco e banco fora do ar** devolvem `ProblemDetails` sem stack trace; `/health` dá 503 (Tasks 8 e 9).

---

### Task 0: Passo 0: arquivos da raiz e renormalização

**Novo em 2026-10-30. Feita antes de o front começar:** um commit pequeno, **só** com os arquivos da raiz abaixo e a renormalização, que vai para a `main` antes de a pessoa do front criar a branch dela. O plano do front tem a regra de **nunca criar** estes arquivos (se faltarem, a sessão do front registra uma dúvida). Pré-condição: o planejamento (contrato, planos, telas e scripts `V030` a `V047`) já está commitado, para que o commit do Passo 0 não leve nada junto. Branch e PR: decisão do dono (`docs/superpowers/plans/2026-10-30-plano-back-end.md`, seção 10). Hoje a raiz não tem `.gitignore`, `.gitattributes`, `.editorconfig` nem `README.md` (li em 2026-10-30: os únicos arquivos de configuração versionados são `.github/CODEOWNERS` e `.claude/settings.json`).

**Files:**
- Create (raiz): `.gitattributes`, `.gitignore`, `.editorconfig`, `README.md`

**Interfaces:**
- Produces: os quatro arquivos na `main`. A **Task 2** não os cria de novo (só acrescenta regras de C# ao `.editorconfig`, se o `dotnet format` pedir); a **Task 3** completa o `README.md`; o hash de congelamento (Task 12, bullet "Hash dos scripts") normaliza CRLF para LF por conta própria, e o `eol=lf` só mantém a árvore igual nos dois sistemas; o plano do front parte da `main` já com eles.

- [ ] **Step 1: Conferir a pré-condição e o fim de linha dos arquivos versionados**

Run: `git status --short; git ls-files --eol | Select-String "i/crlf"`
Expected: `git status --short` **sem** nenhuma linha `M` (arquivo modificado). A segunda consulta lista arquivos que estão em CRLF **no índice**: li em 2026-10-30 que, neste PC, os 41 arquivos de texto versionados já estão em LF no índice (nenhuma linha `i/crlf`), embora 16 estejam em CRLF na árvore de trabalho (`w/crlf`); então a renormalização **provavelmente não muda nada**. Se aparecer `i/crlf`, a renormalização vai mudar esses arquivos, **só no fim de linha**, e isso é o esperado.

- [ ] **Step 2: Criar os quatro arquivos**

`.gitattributes` (os PNG de `Figma/telas/` e o PDF do repositório são binários e nunca recebem conversão de fim de linha):

```
* text=auto eol=lf
*.png binary
*.pdf binary
```

`.gitignore`:

```
bin/
obj/
node_modules/
dist/
.vs/
TestResults/
*.user
.env
```

`.editorconfig`:

```
root = true

[*]
charset = utf-8
end_of_line = lf
insert_final_newline = true
```

`README.md` mínimo: título `# RH-System`; uma frase (sistema de RH, TCC: API em .NET 10 em `back-end/`, front-end em React em `front-end/`, banco MySQL por scripts SQL em `Banco de Dados/Scripts/`); três links (`docs/superpowers/plans/2026-10-30-plano-back-end.md`, `docs/plano-front-end/LEIA-PRIMEIRO.md` e `contrato/`); e a frase "Como rodar cada parte será descrito aqui à medida que ela existir." (A Task 3 completa o resto.)

- [ ] **Step 3: Verificar os atributos**

Run: `git check-attr text eol binary -- Figma/telas/00-rh-painel-inicial.png "Banco de Dados/Scripts/V006__funcionarios_matricula_contrato.sql"`
Expected: para o PNG, `binary: set` (e `text: unset`); para o `.sql`, `text: auto` e `eol: lf` (medido em um repositório descartável, 2026-10-30).

- [ ] **Step 4: Renormalizar e conferir que o commit só leva os quatro arquivos**

Run: `git add .gitattributes .gitignore .editorconfig README.md; git add --renormalize .; git diff --cached --name-status`
Expected: **exatamente quatro** linhas, todas `A` (`--renormalize` implica `-u`: só mexe em arquivos já versionados e **não adiciona o que ainda não foi versionado**; medido em um repositório descartável em 2026-10-30, onde um arquivo não versionado ficou de fora e um índice já em LF não gerou nenhuma linha `M`). Se aparecer uma linha `M`: confira `git diff --cached --ignore-space-at-eol --stat -- <arquivo>`; vazio quer dizer que só o fim de linha mudou (aceitável); com conteúdo, é alteração pendente que não pertence a este commit, então `git restore --staged <arquivo>`.

- [ ] **Step 5: Commit** (com autorização): `git commit -m "chore: arquivos da raiz e renormalizacao (passo 0)"`
- [ ] **Step 6: Levar para a `main` e avisar a pessoa do front** (PR, aprovação e branch conforme a decisão do dono). Depois do merge:

Run: `git fetch origin; git ls-tree --name-only origin/main -- .gitattributes .gitignore .editorconfig README.md`
Expected: os quatro nomes. **Só então** a pessoa do front cria a branch dela a partir da `main`.

---

### Task 1: Pré-requisitos da máquina e conferências de documentação

**Files:** nenhum.

Feita pelo dono, com o assistente conferindo. Sem Docker não dá para rodar os testes que usam o banco (Tasks 4 a 9) nem o passo de integração do plano do front (`docs/plano-front-end/fatia-0.md`); as demais rodam sem ele.

- [ ] **Step 1: Instalar o Docker Desktop** (pode ser na máquina de casa) e abrir até o daemon subir.
- [ ] **Step 2: Atualizar o Node para o Node 24 LTS** (**`^24.15.0`**: uma 24 anterior à 24.15.0 não serve, é o mínimo medido para a pilha do front, `docs/plano-front-end/02-pilha.md`; o `engines` do front também aceita `^22.22.2`, mas **este plano fixa a 24**, porque uma versão mais nova, como a 26, não foi medida com a pilha; hoje a máquina tem v18.16.1).
- [ ] **Step 3: Verificar**

Run: `docker version --format '{{.Server.Version}}'; node -v; dotnet --list-sdks; $PSVersionTable.PSVersion; Get-NetTCPConnection -LocalPort 3306 -ErrorAction SilentlyContinue`
Expected: versão do Docker; Node 24.15.0 ou mais nova da série 24 (`^24.15.0`); SDK 10.0.x; versão do PowerShell; **nenhuma linha** na última consulta (porta 3306 livre; se houver MySQL local, a Task 3 usa outra porta).

- [ ] **Step 4 (opcional): instalar Azure CLI e GitHub CLI.** O portal e a aba Actions também servem. Se usadas, encerrar as sessões ao fim (`az logout`, `gh auth logout`): a máquina pode ser compartilhada.
- [ ] **Step 5: Conferir na documentação (Microsoft Learn e do MySQL), sem precisar de conta, e registrar no spec, seção 12:** (a) o App Service Linux oferece o runtime .NET 10; (b) o MySQL Flexible Server oferece a versão 8.4; (c) em qual nível de plano o **Always On** existe; (d) em quais regiões o Static Web Apps existe; (e) **criar triggers e funções no Flexible Server exige algum parâmetro do servidor ou privilégio** (`log_bin_trust_function_creators`, `TRIGGER`, `CREATE ROUTINE`, `SUPER`)? Vale também fora do Azure: o MySQL 8 liga o log binário por padrão. **Se (a) ou (b) falhar, avisar o dono antes da Task 2.**

---

### Task 2: Solution em camadas e testes de arquitetura

**Files:**
- Create: `global.json` (raiz), `back-end/Directory.Build.props`, `back-end/Directory.Packages.props`. **`.gitattributes`, `.gitignore` e `.editorconfig` da raiz já existem desde a Task 0 (Passo 0): não os crie de novo.**
- Create: `back-end/src/RHSystem.Domain/`, `RHSystem.Application/`, `RHSystem.Infrastructure/`, `RHSystem.Api/` e `back-end/tests/RHSystem.Domain.Tests/`, `RHSystem.Application.Tests/`, `RHSystem.Api.IntegrationTests/`
- Test: `back-end/tests/RHSystem.Domain.Tests/ArquiteturaTests.cs`

**Interfaces:**
- Produces: a solution `back-end/RHSystem` com 7 projetos; versionamento central de pacotes (`Directory.Packages.props`, com `Microsoft.EntityFrameworkCore.*` todos em 9.0.x e Pomelo 9.0.0); `global.json` na **raiz** fixando o SDK 10 (`rollForward: latestFeature`).

- [ ] **Step 0: Conferir o provider no nuget.org** (achado da revisão de 2026-10-29): a versão estável atual de `Pomelo.EntityFrameworkCore.MySql` e se já existe uma 10.x. A afirmação "não existe 10.x" do spec é de antes de 2026-10-21; este plano fixa a 9.0.0 com EF Core 9. Se existir uma 10.x estável, avisar o dono antes de seguir (a escolha muda o `Directory.Packages.props` e as restrições globais).
- [ ] **Step 1: Criar a solution, o projeto `RHSystem.Domain.Tests` (xUnit) e os arquivos de configuração** (`global.json` e os dois `Directory.*.props`; os arquivos da raiz já vieram do Passo 0: se o `dotnet format` pedir regras de C#, **acrescente-as ao `.editorconfig` existente**, sem recriá-lo). `Directory.Build.props`: `net10.0`, `Nullable` e `ImplicitUsings` habilitados, `TreatWarningsAsErrors` verdadeiro, **`WarningsNotAsErrors` com `NU1901;NU1902;NU1903;NU1904`** e `NU1608` em `WarningsAsErrors`. Os projetos gerados por `dotnet new` trazem `Version=` nos `PackageReference`: remova (o versionamento é central; senão dá `NU1008`).
- [ ] **Step 2: Escrever os testes de arquitetura que falham.** Eles leem os `.csproj` como XML, subindo de `AppContext.BaseDirectory` até a pasta `back-end`:

```csharp
[Fact] void Domain_csproj_nao_tem_ProjectReference_PackageReference_nem_FrameworkReference() { }
[Fact] void Application_referencia_apenas_Domain() { }
[Fact] void Infrastructure_referencia_Application_e_nao_referencia_Api() { }
[Fact] void Api_referencia_Application_e_Infrastructure() { }
[Fact] void Directory_Build_props_e_Directory_Packages_props_nao_tem_ProjectReference() { }
```

O teste "Assembly do Domain não referencia EntityFrameworkCore nem AspNetCore" nasce na Task 5, quando houver uma classe do Domain.

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test back-end --filter ArquiteturaTests`
Expected: FAIL (os `.csproj` de `src/` não existem).

- [ ] **Step 4: Criar os quatro projetos de `src/` e os outros dois de teste** com as referências da regra de dependência. `RHSystem.Api` é web; os demais são bibliotecas. `RHSystem.Api.IntegrationTests` referencia Api, Infrastructure, Application e Domain.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet build back-end; dotnet test back-end --filter ArquiteturaTests; dotnet format back-end --verify-no-changes`
Expected: build sem warnings; 5 testes passando; `dotnet format` sem alterações. Rodar o `dotnet format` já aqui (e não só na Task 12) confirma cedo que ele funciona com o formato de solution que o SDK 10 cria (suponho que seja `.slnx`; não medido).

- [ ] **Step 6: Commit** (com autorização), **só com os caminhos que esta Task cria**. Nunca `docs`: o spec e os planos já foram na entrega do planejamento, e varrer `docs` levaria junto o que outra sessão estiver mexendo.

Run: `git add back-end global.json .editorconfig; git diff --cached --name-only`
Expected: só caminhos sob `back-end/`, mais `global.json` (e `.editorconfig`, só se o Step 1 acrescentou regras de C#). Se aparecer outro, `git restore --staged <arquivo>`. Depois: `git commit -m "feat: solution em camadas e testes de arquitetura"`

---

### Task 3: `docker compose` local com MySQL e README

**Files:** Create: `docker-compose.yml`, `.env.example` (raiz). Modify: `README.md` (raiz; já existe, mínimo, desde a Task 0).

**Interfaces:**
- Produces: MySQL 8.4 em `localhost:${MYSQL_PORTA:-3306}`, banco **vazio** `rh_system` (`MYSQL_DATABASE`), `utf8mb4` e `utf8mb4_unicode_ci`; credenciais de `.env` (copiado de `.env.example`, só com valores de desenvolvimento local). Quem cria as tabelas é o executor de scripts da Task 4, não o compose.

- [ ] **Step 1: Escrever `docker-compose.yml`:** serviço `mysql` (`mysql:8.4`, `--character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci --default-time-zone=-03:00 --log-bin-trust-function-creators=1` (o último **medido em 2026-10-29**: com o log binário ligado, que é o padrão do `mysql:8.4`, o usuário restrito da Task 14 recebe o erro 1419 ao criar o primeiro trigger; com o parâmetro, os 36 triggers, as 8 funções e os `V006` a `V029` aplicam), volume nomeado, porta `${MYSQL_PORTA:-3306}:3306`) e `healthcheck` com `mysqladmin ping -h 127.0.0.1 -u root -p"$$MYSQL_ROOT_PASSWORD"` (o `-h 127.0.0.1` evita o servidor temporário de inicialização, que só atende por socket).
- [ ] **Step 2: Subir e verificar**

Run (no Git Bash; no PowerShell 5.1 as aspas duplas dentro de aspas simples podem chegar quebradas ao programa nativo, suponho, não medido): `docker compose up -d --wait; docker compose exec mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "SELECT @@character_set_server, @@collation_server, @@version"'`
Expected: serviço `healthy`; `utf8mb4`, `utf8mb4_unicode_ci` e `8.4.x`.

- [ ] **Step 3: Completar o `README.md`** (que o Passo 0 criou mínimo, sem reescrevê-lo do zero) com o que é o projeto, os pré-requisitos (Task 1), as regras dos scripts SQL (resumo da seção 4 do spec: nunca editar script aplicado, congelamento no primeiro deploy, recriar o banco local com `docker compose down -v`) e seções "Back-end" e "Front-end": a do back-end é preenchida na Task 9; a do front-end, pelo plano do front (`docs/plano-front-end/fatia-0.md`).
- [ ] **Step 4: Commit** (com autorização): `git add docker-compose.yml .env.example README.md; git commit -m "feat: docker compose local com MySQL"`

---

### Task 4: Scripts SQL versionados e executor

**Contexto:** os triggers de `PONTOS` do grupo foram corrigidos em 2026-10-29 direto no arquivo, com autorização do dono, para o Kauan validar (ver "Decisões deste plano"). Antes de mover o arquivo, rode `git pull` e confira se o Kauan subiu uma versão própria; se subiu, compare as duas. Se o Step 9 falhar, siga o ramo de decisão do passo. A pasta `Scripts/` já tem `V006` a `V047` (42 scripts; ver acima), e esta task cria `V001` a `V005`: o Step 9 testa **os 47** (`V001` a `V047`). Esta task **não bloqueia** o código e os testes das Tasks 2, 3 e 6 (que não usam o banco), **exceto o passo de integração do plano do front** (`docs/plano-front-end/fatia-0.md`; é a verificação manual ponta a ponta que era o Step 5 da antiga Task 11), que precisa do banco (Task 4) e da API (Task 9). O resultado vai ao dono e ao grupo.

**Files:**
- Move/Create (autorizado pelo dono em 2026-10-28): `Banco de Dados/Scripts/V001__esquema_inicial.sql`, `V002__triggers.sql`, `V003__funcoes.sql`, `V004__refresh_tokens.sql`, `V005__niveis_de_acesso_iniciais.sql`; `Banco de Dados/Validacao/validacao_consistencia.sql`. **Os `V006` a `V047` já existem na pasta (42 scripts, escritos em 2026-10-29 e 2026-10-30): esta task não os cria nem os altera; só os embute e os testa.**
- Create: `back-end/src/RHSystem.Infrastructure/Esquema/ScriptSql.cs`, `IFonteDeScripts.cs`, `ScriptsEmbutidos.cs`, `DivisorDeScript.cs`, `ExecutorDeScripts.cs`, `Excecoes.cs`
- Modify: `back-end/src/RHSystem.Infrastructure/RHSystem.Infrastructure.csproj` (`EmbeddedResource` de `..\..\..\Banco de Dados\Scripts\*.sql`, com `LogicalName="Scripts/%(Filename)%(Extension)"`; pacote `MySqlConnector`)
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Infra/MySqlFixture.cs`, `Esquema/DivisorDeScriptTests.cs`, `ExecutorDeScriptsTests.cs`, `ScriptsDoGrupoTests.cs`

**Interfaces:**
- Produces (`RHSystem.Infrastructure.Esquema`):
  - `record ScriptSql(int Versao, string Nome, string Conteudo)`
  - `interface IFonteDeScripts { IReadOnlyList<ScriptSql> Carregar(); }` e `class ScriptsEmbutidos : IFonteDeScripts` — lê os recursos `Scripts/V{nnn}__{nome}.sql`, ordena por versão e lança se houver versão repetida ou nome fora do padrão. A interface existe para os testes trocarem a fonte.
  - `static class DivisorDeScript { static IReadOnlyList<string> Dividir(string conteudo); }` — divide pelo delimitador corrente, **ignorando delimitador dentro de comentário** (`-- `, `#`, `/* */`) **e de literal entre aspas** (`'...'`, `"..."`, crase, com aspas dobradas e escape por barra); linhas `DELIMITER x` (em qualquer caixa) trocam o delimitador e **não** viram comando; o último comando sem delimitador final também é emitido; normaliza CRLF para LF; descarta trechos vazios ou só de comentário. Os arquivos do grupo têm **12** casos de `;` dentro de comentário no modo `;` (medido em 2026-10-29: 4 em `Criacao_Tabelas` e 8 linhas `-- SELECT ...;` no fim de `Functions`, depois de `DELIMITER ;`), mais 3 comentários com `;` dentro do bloco `DELIMITER $$` das funções; os `V006` a `V029` não têm nenhum, e li em 2026-10-30 que os `V030` a `V047` também não têm linha de comentário `--` com `;` (só procurei linhas `--`; o divisor ignora `;` em qualquer comentário e em literal entre aspas, e os testes do Step 2 provam isso).
  - `class ExecutorDeScripts { ExecutorDeScripts(string connectionString, ILogger<ExecutorDeScripts> log); Task AplicarPendentesAsync(IReadOnlyList<ScriptSql> scripts, CancellationToken ct); }`. Cria `SCHEMA_VERSOES (VERSAO INT PRIMARY KEY, NOME VARCHAR(200) NOT NULL, HASH_SHA256 CHAR(64) NOT NULL, APLICADO_EM DATETIME(6) NOT NULL)`; usa **uma conexão só para a trava** e **uma conexão nova por script** (estado de sessão, como `SET FOREIGN_KEY_CHECKS`, não vaza entre scripts); adquire `GET_LOCK('rhsystem_esquema:{nome do banco}', 60)` na conexão da trava, trata o resultado `1`, `0` e `NULL` (só `1` segue) e a libera no fim; confere o hash dos já aplicados; **recusa** uma versão pendente menor que a maior já aplicada, uma versão aplicada cujo arquivo sumiu, e um banco que já tem tabelas mas está sem histórico em `SCHEMA_VERSOES`; aplica os pendentes em ordem, um comando por vez, e **só depois do script inteiro** grava a linha em `SCHEMA_VERSOES`. O hash é SHA-256 do conteúdo sem BOM e com fins de linha normalizados.
  - `class ScriptJaAplicadoFoiAlteradoException(int versao)` com mensagem "O script V001 já foi aplicado e foi alterado. Não edite scripts aplicados: crie um script novo." e `class FalhaNoScriptException(int versao, int indiceDoComando, string detalhe)` (avisa que o banco pode ter ficado parcialmente alterado), mais `VersaoForaDeOrdemException`, `ScriptAplicadoSumiuException` e `BancoSemHistoricoDeScriptsException`, todas com mensagem clara em português.
  - `MySqlFixture` (testes): `IAsyncLifetime`; `new MySqlBuilder("mysql:8.4")` com a imagem no construtor (o construtor sem imagem é obsoleto) **e com os mesmos parâmetros do servidor do `docker-compose.yml` e do Azure** (`--character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci --default-time-zone=-03:00 --log-bin-trust-function-creators=1`, por `WithCommand`; conferir o método na documentação do módulo): sem o fuso, os testes que dependem de `CURDATE()` e `NOW()` (os triggers do grupo usam as duas funções) rodariam em UTC e esconderiam diferenças que só apareceriam no deploy; administração com o usuário que pode `CREATE DATABASE` (ver a documentação do módulo); `Task<string> NovoBancoAsync()` cria um banco vazio e exclusivo com `CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci` **e um usuário restrito a esse banco**, com os mesmos privilégios da Task 14 (`SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES, TRIGGER, CREATE ROUTINE, ALTER ROUTINE, EXECUTE`), e devolve a connection string **desse usuário** (assim os testes não escondem um problema de privilégio que apareceria no Azure); `Task<string> NovoBancoComScriptsAsync()` (idem, com todos os scripts embutidos aplicados). **Cada classe de teste pede o seu próprio banco, e os testes não apagam funcionários** (o trigger do grupo bloqueia `DELETE` em `FUNCIONARIOS`): usam e-mails únicos.
- Produces (scripts):
  - `V001`: o script de tabelas do grupo **sem** `CREATE DATABASE`, `USE`, o bloco "LIMPEZA" (os `DROP TABLE` e os dois `SET FOREIGN_KEY_CHECKS`) e os selects de validação do fim; mantém tabelas, chaves, índices e o `INSERT` dos feriados de 2026.
  - `V002`: o script de triggers do grupo **sem** a linha `USE` (os `DROP TRIGGER IF EXISTS` ficam).
  - `V003`: o arquivo de funções do grupo (`RHSystem_Functions_MySQL_Unificadas.sql`, 8 funções) **sem** a linha `USE`.
  - `V004`: `REFRESH_TOKENS (ID_REFRESH_TOKEN INT AUTO_INCREMENT PRIMARY KEY, ID_USUARIO INT NOT NULL, TOKEN_HASH CHAR(64) NOT NULL, CRIADO_EM DATETIME(6) NOT NULL, EXPIRA_EM DATETIME(6) NOT NULL, REVOGADO_EM DATETIME(6) NULL)`, `FK_REFRESH_TOKENS_USUARIOS` para `USUARIOS(ID_USUARIO)` e `UQ_REFRESH_TOKENS_HASH` em `TOKEN_HASH`; `utf8mb4_unicode_ci`.
  - `V005`: `INSERT IGNORE` em `NIVEIS_DE_ACESSO` dos três níveis `RH`, `Funcionario` e `Funcionario Gestor`, com descrição (o `IGNORE` evita derrubar a versão se o grupo semear os mesmos níveis antes).
  - `V006` a `V047`: **já existem** (42 scripts; `Banco de Dados/Scripts/LEIA-ME.md` diz o que cada um faz). O `EmbeddedResource` os leva junto, e o `ExecutorDeScripts` os aplica em ordem depois de `V005`.
  - `validacao_consistencia.sql`: os selects de validação do grupo, mais um que lista funcionários cujo `ID_GESTOR` diverge do gestor do setor.

- [ ] **Step 1: Mover os arquivos do grupo** (autorizado pelo dono em 2026-10-28): `git mv` de `RHSystem_Criacao_Tabelas_MySQL.sql` para `Banco de Dados/Scripts/V001__esquema_inicial.sql`, do de triggers para `V002__triggers.sql` e do de funções para `V003__funcoes.sql`; aplicar os ajustes acima; criar `V004`, `V005` e o arquivo de validação. Antes de mover, `git pull` e conferir de novo o que o grupo mudou, porque os arquivos ainda estão em evolução.
- [ ] **Step 2: Escrever os testes do divisor que falham**

```csharp
Divide_por_ponto_e_virgula_e_ignora_comentarios_e_vazios
Respeita_DELIMITER_e_nao_emite_a_linha_DELIMITER
Trigger_com_ponto_e_virgula_no_corpo_vira_um_unico_comando
Normaliza_CRLF_para_LF
Texto_so_com_comentarios_nao_gera_comando
Ponto_e_virgula_dentro_de_comentario_no_modo_ponto_e_virgula_nao_divide      // os arquivos do grupo têm 12 casos no modo `;` (4 nas tabelas, 8 nas funções)
Ponto_e_virgula_dentro_de_literal_entre_aspas_nao_divide
Ultimo_comando_sem_delimitador_final_e_emitido
DELIMITER_em_minusculas_e_reconhecido
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test back-end --filter DivisorDeScriptTests`
Expected: FAIL (tipos não definidos).

- [ ] **Step 4: Implementar `ScriptSql`, `ScriptsEmbutidos` e `DivisorDeScript`** com as assinaturas acima. Rodar de novo; Expected: PASS.
- [ ] **Step 5: Escrever os testes do executor que falham**, com scripts sintéticos e `NovoBancoAsync()`:

```csharp
Aplica_em_ordem_e_registra_o_hash
Segunda_execucao_nao_reaplica_nenhum_script
Script_aplicado_e_depois_editado_faz_o_executor_recusar_com_mensagem_clara       // Review Focus 2
Duas_execucoes_simultaneas_aplicam_cada_script_uma_unica_vez                      // Review Focus 2; script NÃO idempotente com DO SLEEP(1) para forçar a sobreposição; dois executores liberados juntos por uma Barrier
Script_que_falha_no_meio_lanca_FalhaNoScript_e_nao_registra_a_versao
Hash_ignora_a_diferenca_entre_CRLF_e_LF
Versao_pendente_menor_que_a_maior_aplicada_e_recusada
Script_aplicado_cujo_arquivo_sumiu_e_recusado
Banco_com_tabelas_mas_sem_historico_em_SCHEMA_VERSOES_e_recusado
Trava_distinta_por_banco_nao_bloqueia_outro_banco_no_mesmo_servidor
```

- [ ] **Step 6: Rodar e ver falhar**

Run: `dotnet test back-end --filter ExecutorDeScriptsTests`
Expected: FAIL.

- [ ] **Step 7: Implementar `ExecutorDeScripts` e as exceções.** Rodar de novo; Expected: PASS.
- [ ] **Step 8: Escrever os testes contra os scripts reais do grupo** (`NovoBancoAsync()` + `ScriptsEmbutidos.Carregar()`):

```csharp
Todos_os_scripts_reais_aplicam_com_o_usuario_restrito_ao_banco_e_as_tabelas_usadas_pela_fatia_0_existem   // NIVEIS_DE_ACESSO, FUNCIONARIOS, CARGOS, SETORES, USUARIOS, REFRESH_TOKENS, SCHEMA_VERSOES
Quantidade_de_triggers_no_banco_e_igual_ao_numero_de_CREATE_TRIGGER_do_V002
Quantidade_de_funcoes_no_banco_e_igual_ao_numero_de_CREATE_FUNCTION_do_V003  // conta no texto do arquivo, para não quebrar quando o grupo mudar a base
Quantidade_de_tabelas_no_banco_e_igual_aos_nomes_distintos_de_CREATE_TABLE_nos_scripts_mais_SCHEMA_VERSOES  // BASE TABLE em information_schema; nomes distintos, não linhas; hoje 49 (ver o Step 9)
Niveis_de_acesso_semeados_sao_RH_Funcionario_e_Funcionario_Gestor_e_aplicar_de_novo_nao_duplica
Executor_aplica_os_47_scripts_com_lower_case_table_names_1()   // contêiner extra com --lower-case-table-names=1 (suposto no Flexible Server, não verificado); medido em 2026-10-29 que os `V006` a `V029` e os 204 testes de comportamento passam assim, falta o executor; para `V030` a `V047` isso ainda não foi medido
Scripts_de_negocio_V006_a_V047_aplicam_depois_de_V001_a_V005_pelo_usuario_restrito()   // o V012 usa DELIMITER
Trigger_do_grupo_dispara: usuario_para_funcionario_inativo_e_recusado        // mensagem "Nao e possivel criar usuario para funcionario inativo."
```

- [ ] **Step 9: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Esquema|FullyQualifiedName~ScriptsDoGrupo"`
Expected: PASS, com **49 tabelas, 36 triggers e 8 funções** nos scripts de hoje (`V001` a `V047`). **Como o 49 foi calculado, e o que li (2026-10-30):** `grep` de `CREATE TABLE` no início de linha dá 25 em `RHSystem_Criacao_Tabelas_MySQL.sql` (o futuro `V001`) e 22 nos scripts `V006` a `V047` (21 arquivos; `V026` e `V037` criam duas cada), total 47, que é o número que o `LEIA-ME.md:40` mediu num MySQL 8.4.13; **mais** `REFRESH_TOKENS` (`V004`, que ainda não existe) **e** `SCHEMA_VERSOES` (criada pelo executor, não por um script) dão 49. Triggers: `CREATE TRIGGER` aparece 36 vezes no arquivo de triggers; o `V012` apenas apaga e recria `TRG_HOLERITES_BEFORE_INSERT` (por isso a contagem é por nome distinto) e nenhum outro script cria trigger. Funções: 8 `CREATE FUNCTION` no arquivo de funções. O que **não** medi: a execução do executor (ele não existe) e o usuário restrito para `V030` a `V047`; o teste é quem prova. Se o número do grupo mudar, os testes baseados em texto acompanham; não fixe 49 no código. **Se o servidor recusar um script real do grupo, PARE: não altere o arquivo do grupo nem dobre o divisor para passar.** Diagnostique: (a) bug do divisor (por exemplo `;` em comentário ou literal): corrigir o divisor e acrescentar o caso aos testes; (b) incompatibilidade entre os scripts do grupo (por exemplo `TRG_PONTOS_BEFORE_INSERT/UPDATE` usando `HORA_ENTRADA` e `HORA_SAIDA`, removidas de `PONTOS`): levar ao dono e ao grupo, que decidem quem corrige; (c) erro 1419 (privilégio `SUPER` com log binário): **medido localmente em 2026-10-29** (sem o parâmetro o usuário restrito recebe 1419 no primeiro `CREATE TRIGGER`; com `log_bin_trust_function_creators=1` tudo aplica), por isso o parâmetro já está no compose e na fixture; registrar no README e na Task 14 que o servidor precisa dele, e conferir no Azure com `SHOW VARIABLES LIKE 'log_bin%'` (o valor padrão do Flexible Server não foi verificado).

- [ ] **Step 10: Commit** (com autorização): `git add "Banco de Dados/Scripts" "Banco de Dados/Validacao" back-end; git commit -m "feat: scripts sql versionados e executor"` (caminhos explícitos: não levar junto outros arquivos do grupo, como o `.drawio` e os `.png`)

---

### Task 5: Domínio de autenticação, mapeamento do EF e teste de fumaça do provider

**É a tarefa que confirma o maior risco do spec. Incompatibilidade de provider significa: `NU1608`, `NU1107`, `MissingMethodException` ou `TypeLoadException` ao usar `UseMySql` ou ao consultar. Se aparecer, PARE e avise o dono. Qualquer outro erro (por exemplo, coluna não mapeada) é bug nosso: corrija.**

**Files:**
- Create: `back-end/src/RHSystem.Domain/Autenticacao/Perfil.cs`, `NivelDeAcesso.cs`, `Cargo.cs`, `Setor.cs`, `Funcionario.cs`, `Usuario.cs`, `RefreshToken.cs`
- Create: `back-end/src/RHSystem.Infrastructure/Persistencia/AppDbContext.cs`, `Configuracoes/NivelDeAcessoConfiguracao.cs`, `CargoConfiguracao.cs`, `SetorConfiguracao.cs`, `FuncionarioConfiguracao.cs`, `UsuarioConfiguracao.cs`, `RefreshTokenConfiguracao.cs`
- Test: `back-end/tests/RHSystem.Domain.Tests/Autenticacao/FuncionarioTests.cs`, `UsuarioTests.cs`, `PerfilTests.cs`, `RefreshTokenTests.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoContraBancoTests.cs`, `ProviderMySqlSmokeTests.cs`

**Interfaces:**
- Consumes: `MySqlFixture.NovoBancoComScriptsAsync()` e os scripts (Task 4).
- Produces (Domain, `RHSystem.Domain.Autenticacao`):
  - `enum Perfil { Funcionario = 1, Gestor = 2, RH = 3 }` e `static Perfil? PerfilPorNome(string? nomeDoNivel)`: `"RH"` → `Perfil.RH`, `"Funcionario"` → `Perfil.Funcionario`, `"Funcionario Gestor"` → `Perfil.Gestor`; `null` ou qualquer outro nome devolve `null`.
  - `class NivelDeAcesso { int Id; string Nome; }`
  - `class Cargo { int Id; string Nome; }` e `class Setor { int Id; string Nome; }` (**novas em 2026-10-30**, só leitura nesta fatia: nome e id bastam para o `cargo` e o `setor` de `UsuarioResposta`; **a fatia 1b as estende** com `Cargo.CargaSemanalHoras` e `Setor.DefinirGestor(int? idGestor)` (decisão 4 da revisão), e a 1c só as consome no cadastro de apoio).
  - `class Funcionario { int Id; string Nome; string? EmailCorporativo; DateOnly DataAdmissao; bool Ativo; NivelDeAcesso? NivelDeAcesso; Cargo? Cargo; Setor? Setor; Perfil? Perfil { get; }; static Funcionario Criar(string nome, string emailCorporativo, DateOnly dataAdmissao, NivelDeAcesso nivel); void Inativar(); }` (`Criar` aplica `Trim()` e `ToLowerInvariant()` ao e-mail).
  - `class Usuario { int Id; int FuncionarioId; Funcionario Funcionario; string SenhaHash; bool Ativo; DateTime? UltimoLoginEm; bool PodeEntrar { get; }; static Usuario Criar(Funcionario f, string senhaHash); void RegistrarLogin(DateTime agoraUtc); void Inativar(); }` (`PodeEntrar` = `Ativo && Funcionario.Ativo`). **`RegistrarLogin` grava `UltimoLoginEm` truncado ao segundo**: `USUARIOS.DATA_HORA_ULTIMO_LOGIN` é `DATETIME` sem fração (`Criacao_Tabelas:202`) e suponho que o MySQL **arredonde** a fração em vez de truncá-la (a 1c o mede, Task 1 dela); truncar antes de gravar vale nos dois casos, e o relógio falso dos testes de login tem fração de propósito. Os triggers do grupo recusam criar usuário para funcionário inativo e podem reagir à inativação de um funcionário; os testes criam ativos e inativam depois.
  - `class RefreshToken { int Id; int UsuarioId; string TokenHash; DateTime CriadoEm; DateTime ExpiraEm; DateTime? RevogadoEm; bool EstaAtivo(DateTime agoraUtc); static RefreshToken Criar(int usuarioId, string tokenHash, DateTime agoraUtc, TimeSpan validade); }`
- Produces (Infrastructure): `AppDbContext : DbContext` com `DbSet<NivelDeAcesso> NiveisDeAcesso`, `DbSet<Cargo> Cargos`, `DbSet<Setor> Setores`, `DbSet<Funcionario> Funcionarios`, `DbSet<Usuario> Usuarios`, `DbSet<RefreshToken> RefreshTokens`. **Mapeamento explícito** (`ToTable` e `HasColumnName` por propriedade) para as tabelas dos scripts: `NIVEIS_DE_ACESSO` (`ID_NIVEL_ACESSO`, `NOME_NIVEL_ACESSO`), `CARGOS` (`ID_CARGO`, `NOME_CARGO`), `SETORES` (`ID_SETOR`, `NOME_SETOR`), `FUNCIONARIOS` (`ID_FUNCIONARIO`, `NOME`, `DATA_ADMISSAO`, `ATIVO`, `EMAIL_CORPORATIVO`, `ID_NIVEL_ACESSO`, `ID_CARGO`, `ID_SETOR`), `USUARIOS` (`ID_USUARIO`, `ID_FUNCIONARIO`, `SENHA`, `ATIVO`, `DATA_HORA_ULTIMO_LOGIN`), `REFRESH_TOKENS` (`ID_REFRESH_TOKEN`, `ID_USUARIO`, `TOKEN_HASH`, `CRIADO_EM`, `EXPIRA_EM`, `REVOGADO_EM`). Sem `EnsureCreated` nem migrations. `UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))`.
- **Armadilhas de mapeamento dos `V030` a `V047`** (`Banco de Dados/Scripts/LEIA-ME.md`, "Armadilhas novas"; inferidas, **não medidas**: a sonda abaixo as mede, e vale para toda fatia que mapear essas tabelas):
  - **Colunas calculadas** ficam fora do `INSERT` e do `UPDATE` (mapear como calculadas): `USUARIOS_TOKENS_SENHA.EM_ABERTO` (`V033`), `DESLIGAMENTOS_AGENDADOS.VIGENTE` (`V034`) e `PONTOS_MANUTENCOES.CHAVE_PENDENTE` (`V040`; o `LEIA-ME` a cita ao lado de `VIGENTE`, mas li em `V040:47` que ela é de `PONTOS_MANUTENCOES`), além de `CHAVE_DESTINO` e `CHAVE_AVALIADO` (`V018`, `V029`, `V019`).
  - **`DEFAULT (UTC_TIMESTAMP(6))`** (tabelas só da aplicação, `V033` a `V038`, em UTC) é mapeado como **gerado na inclusão**; senão o EF manda `0001-01-01`. **A aplicação não depende dele**: preenche sempre a data a partir de `IRelogio` (decisão 7 da revisão); o padrão do banco é rede de segurança.
  - **Padrões `FALSE` de propósito** (`FORMULARIOS.LEMBRETE_AUTOMATICO`, `TREINAMENTOS.AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM`): **não declarar o padrão do banco**; a API grava `TRUE` onde o contrato pede ligado, e o EF tem de enviar o valor sempre.
  - **Matrícula por `SEQUENCIAS`** (`V030`): `UPDATE ... LAST_INSERT_ID(ULTIMO_VALOR + 1)` e `SELECT LAST_INSERT_ID()` na **mesma conexão e transação** do cadastro (SQL cru, sem entidade); o código que a usa é da fatia 1b, e a sonda só prova que o provider faz isso sem buraco.
  - **O que a Task 5 mapeia de verdade** continua sendo só seis tabelas (`NIVEIS_DE_ACESSO`, `CARGOS`, `SETORES`, `FUNCIONARIOS`, `USUARIOS` e `REFRESH_TOKENS`); as outras só aparecem no `DbContext` descartável da sonda, e cada fatia mapeia as suas.

- [ ] **Step 1: Escrever os testes de unidade do Domain que falham**

```csharp
Funcionario_Criar_normaliza_o_email()                       // "  Ana@Empresa.com " -> "ana@empresa.com"
Usuario_PodeEntrar_e_false_se_o_usuario_ou_o_funcionario_esta_inativo()
PerfilPorNome_mapeia_RH_Funcionario_e_Funcionario_Gestor_e_devolve_null_para_nulo_ou_outro_nome()
RefreshToken_expirado_ou_revogado_nao_esta_ativo()
Usuario_RegistrarLogin_trunca_o_instante_ao_segundo()       // 2026-10-07T12:42:00.700Z e 12:42:00.999Z => UltimoLoginEm 12:42:00Z (nunca arredonda para 12:42:01Z)
Assembly_do_Domain_nao_referencia_EntityFrameworkCore_nem_AspNetCore()     // da Task 2
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Domain.Tests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar as entidades** (sem pacote). **Step 4: rodar de novo;** Expected: PASS.
- [ ] **Step 5: Escrever os testes de integração que falham**, com `NovoBancoComScriptsAsync()`:

```csharp
// MapeamentoContraBancoTests
Toda_tabela_e_coluna_mapeada_existe_no_banco()
Nulabilidade_das_propriedades_mapeadas_coincide_com_a_das_colunas()     // ID_NIVEL_ACESSO, EMAIL_CORPORATIVO, ID_CARGO e ID_SETOR aceitam NULL no banco
Toda_coluna_obrigatoria_sem_valor_padrao_das_tabelas_mapeadas_esta_mapeada()   // pega o desalinhamento quando o grupo muda o script
// ProviderMySqlSmokeTests
Funcionario_e_usuario_fazem_ida_e_volta_com_unicode_e_perfil_pelo_nivel()      // Nome = "José Ação 😀", nível RH; relê com outro AppDbContext
Email_corporativo_duplicado_viola_a_chave_unica()
Erro_de_trigger_chega_como_DbUpdateException_com_SQLSTATE_45000()              // usuário para funcionário inativo
Funcionario_com_cargo_e_setor_carrega_os_nomes_e_sem_cargo_nem_setor_carrega_null()   // Cargo e Setor inseridos por SQL e ID_CARGO/ID_SETOR atribuídos por UPDATE; relê com Include
// SondaMapeamentoDosScriptsTests: DbContext DESCARTÁVEL, só do teste (não entra no produto), com FORMULARIOS, TREINAMENTOS,
// FORMULARIOS_DESTINATARIOS, FORMULARIOS_PARTICIPACOES e, desde 2026-10-30, USUARIOS_TOKENS_SENHA, DESLIGAMENTOS_AGENDADOS e PONTOS_MANUTENCOES
// (com o PONTOS do dia). Roadmap, "Decisões que atravessam fatias", item 12, e `Banco de Dados/Scripts/LEIA-ME.md`, "Armadilhas novas"
// (armadilhas inferidas, ainda não medidas)
Formulario_com_Ativo_false_grava_false_e_nao_o_padrao_TRUE()
Treinamento_com_EmiteCertificado_false_grava_false()
Participacao_com_Guid_v4_gerado_pela_aplicacao_passa_no_CHECK_e_o_valor_nao_e_regerado()
Colunas_CHAVE_DESTINO_e_CHAVE_AVALIADO_mapeadas_como_calculadas_ficam_fora_do_INSERT()
Coluna_com_DEFAULT_CURRENT_TIMESTAMP_mapeada_como_gerada_na_inclusao_nao_grava_0001_01_01()
// Armadilhas dos V030 a V047:
Colunas_EM_ABERTO_VIGENTE_e_CHAVE_PENDENTE_mapeadas_como_calculadas_ficam_fora_do_INSERT_e_do_UPDATE()   // EM_ABERTO (V033), VIGENTE (V034), CHAVE_PENDENTE (V040); sem isso o MySQL recusa com o erro 3105
Segundo_pedido_de_ajuste_pendente_do_mesmo_dia_viola_UQ_PONTOS_MANUTENCOES_UM_PENDENTE_e_depois_de_decidir_o_primeiro_o_segundo_passa()   // CHAVE_PENDENTE vale ID_PONTO só enquanto PENDENTE
Coluna_com_DEFAULT_UTC_TIMESTAMP_mapeada_como_gerada_na_inclusao_grava_o_instante_UTC_do_banco()   // USUARIOS_TOKENS_SENHA.CRIADO_EM (V033): dentro de poucos segundos do UTC_TIMESTAMP(6) lido do próprio banco, nunca 0001-01-01
Padroes_FALSE_de_proposito_gravam_true_e_false_como_enviados()   // FORMULARIOS.LEMBRETE_AUTOMATICO (V045), TREINAMENTOS.AVISA_POR_EMAIL e LEMBRA_ANTES_DO_FIM (V043): o padrão do banco NÃO é declarado no mapeamento e o EF sempre envia o valor
Matricula_pela_SEQUENCIAS_devolve_numeros_sem_buraco_na_mesma_transacao_e_o_rollback_devolve_o_numero()   // V030: UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + 1) WHERE NOME = 'MATRICULA' e SELECT LAST_INSERT_ID() na mesma conexão e transação; formato LPAD(numero, 5, '0'); SQL cru, sem entidade
```

- [ ] **Step 6: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia"`
Expected: FAIL (sem `AppDbContext`).

- [ ] **Step 7: Implementar `AppDbContext` e as configurações.**
- [ ] **Step 8: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia"`
Expected: PASS, sem `NU1608`/`NU1107` e sem `MissingMethodException`. **Se falhar por incompatibilidade de provider (definição acima), PARE e reporte.**

- [ ] **Step 9: Commit** (com autorização): `git add back-end; git commit -m "feat: dominio de autenticacao, mapeamento e teste de fumaca do provider"`

---

### Task 6: Casos de uso de autenticação (Application)

**Files:**
- Create: `back-end/src/RHSystem.Application/Autenticacao/Contratos.cs`, `Dtos.cs`, `EntrarCasoDeUso.cs`, `RenovarTokenCasoDeUso.cs`, `SairCasoDeUso.cs`
- Test: `back-end/tests/RHSystem.Application.Tests/Autenticacao/EntrarCasoDeUsoTests.cs`, `RenovarTokenCasoDeUsoTests.cs`, `SairCasoDeUsoTests.cs`, `Falsos/` (repositórios, hasher, relógio e unidade de trabalho em memória)

**Interfaces:**
- Consumes: `Usuario`, `Funcionario`, `RefreshToken`, `Perfil` (Task 5).
- Produces (`RHSystem.Application.Autenticacao`):
  - `IUsuarioRepositorio { Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct); Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct); }` (devolvem o `Usuario` com `Funcionario`, `NivelDeAcesso`, `Cargo` e `Setor` carregados; `Cargo` e `Setor` podem ser `null`)
  - `IRefreshTokenRepositorio { Task AdicionarAsync(RefreshToken t, CancellationToken ct); Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct); Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct); Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct); }` — `TentarRevogarAsync` é atômico e devolve `true` só para quem de fato revogou.
  - `IUnidadeDeTrabalho { Task SalvarAsync(CancellationToken ct); Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct); }`
  - `ISenhaHasher { string Gerar(string senha); bool Verificar(string hash, string senha); }`
  - `IEmissorDeTokens { AcessoEmitido EmitirAcesso(Usuario u); RefreshGerado NovoRefresh(); string HashDoRefresh(string token); }`, `record AcessoEmitido(string Token, DateTime ExpiraEmUtc)`, `record RefreshGerado(string Token, string Hash)`
  - `IRelogio { DateTime AgoraUtc { get; } }`
  - DTOs: `record EntrarComando(string Email, string Senha)`, `record RenovarComando(string RefreshToken)`, `record UsuarioResposta(int Id, int FuncionarioId, string Nome, string Email, Perfil Perfil, string? Cargo, string? Setor)` (**`Cargo` e `Setor` novos em 2026-10-30**: o nome do cargo e do setor da pessoa, ou `null` se ela não tem; `contrato/fontes/10-fatia-0-autenticacao.yaml:196-204`), `record SessaoResposta(string TokenDeAcesso, DateTime AcessoExpiraEmUtc, string RefreshToken, UsuarioResposta Usuario)`
  - `enum FalhaDeEntrada { CredenciaisInvalidas, UsuarioInativo, PerfilNaoConfigurado }` (`PerfilNaoConfigurado`: senha certa, conta ativa, mas o funcionário não tem nível ou o nível é desconhecido; o caso de uso registra um log de aviso) e `record ResultadoDeEntrada(SessaoResposta? Sessao, FalhaDeEntrada? Falha)`
  - `EntrarCasoDeUso.ExecutarAsync(EntrarComando, CancellationToken) -> Task<ResultadoDeEntrada>`, `RenovarTokenCasoDeUso.ExecutarAsync(RenovarComando, CancellationToken) -> Task<SessaoResposta?>` (`null` = inválido), `SairCasoDeUso.ExecutarAsync(RenovarComando, CancellationToken) -> Task`

- [ ] **Step 1: Escrever os testes que falham**, com falsos em memória (todos `async Task`):

```csharp
Entrar_com_email_em_maiusculas_e_espacos_autentica_o_mesmo_usuario        // Review Focus 1
Entrar_com_senha_errada_ou_email_inexistente_devolve_falha_CredenciaisInvalidas
Entrar_com_email_inexistente_ainda_chama_Verificar_do_hasher              // contra enumeração por tempo
Entrar_com_usuario_inativo_e_senha_correta_devolve_falha_UsuarioInativo
Entrar_com_funcionario_inativo_e_senha_correta_devolve_falha_UsuarioInativo
Entrar_com_usuario_inativo_e_senha_errada_devolve_falha_CredenciaisInvalidas
Entrar_com_funcionario_sem_nivel_ou_com_nivel_desconhecido_e_senha_correta_devolve_falha_PerfilNaoConfigurado
Entrar_registra_UltimoLoginEm_e_guarda_apenas_o_hash_do_refresh   // relógio falso com fração, 2026-10-07T12:42:00.700Z: UltimoLoginEm = 12:42:00Z (truncado ao segundo)
Entrar_devolve_o_nome_do_cargo_e_do_setor_e_null_quando_o_funcionario_nao_tem   // UsuarioResposta.Cargo e .Setor
Renovar_devolve_o_nome_do_cargo_e_do_setor_do_funcionario
Renovar_rotaciona_revoga_o_antigo_e_emite_um_novo_na_mesma_transacao
Renovar_com_token_ja_revogado_devolve_null_e_nao_revoga_os_outros_tokens_do_usuario
Renovar_para_usuario_ou_funcionario_inativo_devolve_null_e_revoga_todos_os_tokens_do_usuario
Renovar_com_token_expirado_devolve_null
Sair_revoga_o_refresh_informado
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Application.Tests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar contratos, DTOs e os três casos de uso.** `Entrar` normaliza o e-mail (`Trim().ToLowerInvariant()`), verifica a senha **antes** de olhar se a conta está ativa (inativo com senha errada é credencial inválida; só inativo com senha certa devolve `UsuarioInativo`) e, quando o usuário não existe, verifica a senha contra um hash fictício gerado **uma vez pelo mesmo `ISenhaHasher`** (mesmo custo). `Renovar` faz a revogação do antigo e a criação do novo dentro de `ExecutarEmTransacaoAsync`; se `TentarRevogarAsync` devolver `false`, devolve `null`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: casos de uso de autenticacao"`

---

### Task 7: Infraestrutura de autenticação (hash, JWT, repositórios)

**Files:**
- Create: `back-end/src/RHSystem.Infrastructure/Autenticacao/SenhaHasher.cs`, `EmissorDeTokensJwt.cs`, `OpcoesJwt.cs`, `UsuarioRepositorio.cs`, `RefreshTokenRepositorio.cs`, `UnidadeDeTrabalho.cs`, `RelogioDoSistema.cs`; `back-end/src/RHSystem.Infrastructure/DependencyInjection.cs`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Autenticacao/SenhaHasherTests.cs`, `EmissorDeTokensJwtTests.cs`, `RefreshTokenRepositorioTests.cs`, `UsuarioRepositorioTests.cs`

**Interfaces:**
- Consumes: interfaces da Task 6; `AppDbContext` (Task 5); `ExecutorDeScripts` (Task 4).
- Produces:
  - **Configuração lida por `AdicionarInfraestrutura`:** `ConnectionStrings:Padrao`, `Database:ServerVersion` (padrão `8.4.0`), `Jwt:Chave` (mínimo 32 bytes), `Jwt:Emissor`, `Jwt:Audiencia`, `Jwt:AcessoMinutos` (15), `Jwt:RefreshDias` (7).
  - `class OpcoesJwt { string Chave; string Emissor; string Audiencia; int AcessoMinutos = 15; int RefreshDias = 7; }`
  - `static IServiceCollection AdicionarInfraestrutura(this IServiceCollection s, IConfiguration c)`: registra `AppDbContext`, repositórios, hasher, emissor, relógio, unidade de trabalho e `ExecutorDeScripts`.
  - `SenhaHasher` usa `PasswordHasher<Usuario>` (pacote `Microsoft.Extensions.Identity.Core`; se o restore avisar `NU1510`, siga a mensagem) e lança `ArgumentException` para senha acima de 128 caracteres. **O hash gerado tem no mínimo 40 caracteres**, porque o trigger do grupo recusa senha com menos que isso.
  - `EmissorDeTokensJwt.NovoRefresh()` gera 32 bytes aleatórios em Base64Url; `HashDoRefresh` é SHA-256 em hexadecimal (64 caracteres). O JWT leva `sub` (Id do usuário), `fid` (Id do funcionário), `email`, `nome` e `perfil` (texto).
  - `TentarRevogarAsync` usa `ExecuteUpdateAsync` com `WHERE TOKEN_HASH = @h AND REVOGADO_EM IS NULL`, na transação corrente, e devolve `linhasAfetadas == 1`.
  - `UsuarioRepositorio` busca por `EmailCorporativo` com `Include(Funcionario).ThenInclude(NivelDeAcesso)` e também `ThenInclude(Cargo)` e `ThenInclude(Setor)` (o mesmo em `ObterPorIdAsync`).

- [ ] **Step 1: Escrever os testes que falham** (com `NovoBancoComScriptsAsync()` onde houver banco)

```csharp
Hash_verifica_senha_correta_e_rejeita_errada
Hash_gerado_tem_pelo_menos_40_caracteres
Senha_com_acentos_e_emoji_gera_e_verifica
Senha_com_mais_de_128_caracteres_lanca_ArgumentException
Jwt_emitido_contem_sub_fid_email_nome_perfil_e_expira_em_15_minutos
UsuarioRepositorio_acha_pelo_email_corporativo_ignorando_maiusculas_e_traz_o_perfil
UsuarioRepositorio_traz_o_usuario_mesmo_quando_o_funcionario_nao_tem_nivel      // a navegação é opcional: nada de INNER JOIN que esconda o usuário
UsuarioRepositorio_traz_cargo_e_setor_quando_existem_e_null_quando_o_funcionario_nao_tem   // Cargo e Setor também são opcionais (ID_CARGO e ID_SETOR aceitam NULL); sem INNER JOIN
TentarRevogar_com_20_chamadas_simultaneas_devolve_true_para_exatamente_uma   // Review Focus 3: 20 tarefas, cada uma com seu DbContext e conexão, liberadas juntas por uma Barrier, repetido 5 vezes
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Api.IntegrationTests.Autenticacao"`
Expected: FAIL.

- [ ] **Step 3: Implementar as classes.**
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: infraestrutura de autenticacao"`

---

### Task 8: API de autenticação (endpoints e JWT)

**Files:**
- Create: `back-end/src/RHSystem.Api/Autenticacao/AutenticacaoEndpoints.cs`, `Configuracao/AutenticacaoExtensions.cs`, `Configuracao/TratamentoDeErros.cs`, `Configuracao/DocumentacaoExtensions.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Contrato/LocalizadorDoContrato.cs`, `ComparadorDeContrato.cs`, `ContratoDivergenciaTests.cs`, `ComparadorDeContratoTests.cs`, `SondaDoOpenApiNativoTests.cs`
- Modify: `back-end/src/RHSystem.Api/Program.cs`, `back-end/Directory.Packages.props` (`Microsoft.AspNetCore.OpenApi` 10.x na Api; `Microsoft.OpenApi` e o leitor de YAML dele nos testes: **versões a conferir na hora**)
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Autenticacao/AutenticacaoEndpointsTests.cs`
- **Não cria `contrato/sessao.exemplo.json`** (2026-10-30): o contrato é `contrato/openapi.yaml`, que já existe; esta task **não altera `contrato/`**. As cinco rotas da fatia 0 entram na seção "Rotas implementadas" de `contrato/STATUS.md` quando a fatia 1a a criar (decisão 11 da revisão); a linha da fatia 0 da tabela por fatia o dono passa a `pronta` no Step 7 da Task 14.

**Interfaces:**
- Consumes: casos de uso (Task 6), `AdicionarInfraestrutura` (Task 7), `MySqlFixture.NovoBancoComScriptsAsync()` (Task 4); `contrato/openapi.yaml` e `contrato/fontes/10-fatia-0-autenticacao.yaml`.
- Produces (rotas sob `/api/auth`; os `operationId` e os códigos de resposta são **os do contrato**, e a API **declara todos** com `WithName(...)`, `Produces<T>(...)` e `ProducesProblem(...)`, porque o gerador só descreve o que se declara):
  - `POST /api/auth/entrar` (`operationId` `entrar`), corpo `EntrarComando` → `200 SessaoResposta` | `400` (campo vazio ou senha acima de 128 caracteres) | `401` | `403` (inativo com a senha certa, ou sem perfil de acesso configurado) | `429` (declarado aqui; o limitador é da Task 9) | `500`
  - `POST /api/auth/renovar` (`renovarSessao`), corpo `RenovarComando` → `200 SessaoResposta` | `400` (refresh vazio) | `401` | `500`
  - `POST /api/auth/sair` (`sair`), corpo `RenovarComando` → `204` | `400` (refresh vazio) | `500`
  - `GET /api/auth/eu` (`obterUsuarioLogado`; `Authorization: Bearer`) → `200 UsuarioResposta` | `401` | `500`
- **Corpos e textos do contrato** (`contrato/fontes/10-fatia-0-autenticacao.yaml` e `00-base.yaml`, schemas e respostas `Erro4xx`). `SessaoResposta`: `tokenDeAcesso`, `acessoExpiraEmUtc` (`date-time`, terminando em `Z`), `refreshToken`, `usuario`. `UsuarioResposta`: `id`, `funcionarioId`, `nome`, `email`, `perfil` (texto: `Funcionario`, `Gestor` ou `RH`), **`cargo` e `setor` (texto ou `null`; os dois obrigatórios no JSON, com `null` quando a pessoa não tem)**. O `perfil` vem do mapeamento da Task 5 (`"RH"` → `RH`, `"Funcionario"` → `Funcionario`, `"Funcionario Gestor"` → `Gestor`).
- Erros sempre `ProblemDetails` (`application/problem+json`) em português, **sem stack trace**, inclusive o 401 do `JwtBearer` (que sozinho vem sem corpo: tratar em `OnChallenge`). Os textos são os do contrato (`status`, `title`, `detail`):

  | Situação | status | `title` | `detail` |
  |---|---|---|---|
  | Campo vazio ou inválido | 400 | `Dados inválidos.` | em `entrar`: `Preencha o e-mail e a senha.`; **todo outro 400** (aqui `renovar` e `sair`, que no contrato usam `Erro400Validacao`) usa `Confira os campos indicados.` (decisão 6 da revisão; contrato 0.2.1); mais `errors` por campo (ex.: `email: ["O e-mail é obrigatório."]`) |
  | Login recusado (e-mail inexistente, senha errada, inativo com senha errada) | 401 | `E-mail ou senha inválidos.` | `E-mail ou senha inválidos.` (**sempre os mesmos**) |
  | Sem token, token inválido ou vencido, refresh inválido | 401 | `Não autenticado.` | `Entre novamente para continuar.` |
  | Inativo com a senha certa | 403 | `Acesso desativado.` | `Seu acesso está desativado. Fale com o RH.` |
  | Sem nível de acesso ou nível desconhecido | 403 | `Perfil não configurado.` | `Seu perfil de acesso não está configurado. Fale com o RH.` |
  | **Erro de trigger do banco** (`SQLSTATE 45000`) | 422 | `Operação recusada.` | `A operação foi recusada por uma regra do banco de dados.`, mais a mensagem original do trigger na extensão `regraDoBanco` |
  | Muitas tentativas (Task 9) | 429 | `Muitas tentativas.` | `Muitas tentativas. Aguarde um minuto e tente de novo.`, mais o cabeçalho `Retry-After` |
  | Erro inesperado | 500 | `Erro no servidor.` | `Erro no servidor. Tente novamente em instantes.` |

  Todo erro traz `traceId`.
- Enums saem como **texto** (`JsonStringEnumConverter`). `ClockSkew` do JWT = 30 s.
- **Documento OpenAPI nativo** (`Microsoft.AspNetCore.OpenApi`, `AddOpenApi()` e `MapOpenApi()`), servido só com `Documentacao:Habilitada=true` **e sem login** (a rota fica fora de qualquer família e sem `RequireAuthorization`; a UI, os valores por ambiente e o teste que o prova são da Task 9). O `ApiFactory` liga a configuração. **Nada disto foi medido: o Step 4 (sonda) mede, antes de o comparador existir, e o resultado vai para o `README.md`:** a versão de OpenAPI que o gerador produz (o contrato é 3.1.0, `contrato/CONVENCOES.md`, seção 1); a rota padrão do documento (suponho `/openapi/v1.json`); se ele marca como `required` as propriedades não anuláveis dos `record` (se não marcar, um transformador de schema as marca, para bater com o contrato); que o enum em texto sai como `enum` de strings; que `ProducesProblem` gera `application/problem+json`; e qual pacote lê o YAML do contrato (`Microsoft.OpenApi` com o leitor de YAML, ou `YamlDotNet` convertendo para a mesma árvore). **Se o gerador não der conta de algum ponto do contrato, não relaxe o teste nem mude o contrato:** faça o que o Step 4 manda (diferença só de forma: normalizar no comparador; diferença de significado: configurar o gerador; se ainda não bastar, dúvida para o dono).
- **Teste de divergência** (`ContratoDivergenciaTests`, `ComparadorDeContrato`). Interfaces:
  - `static class LocalizadorDoContrato { static string CaminhoDoOpenApi(); }`: sobe de `AppContext.BaseDirectory` até a pasta que tem `contrato/openapi.yaml` e **falha com o caminho procurado** se não achar.
  - `static class ComparadorDeContrato { static IReadOnlyList<string> Comparar(OpenApiDocument contrato, OpenApiDocument gerado, IReadOnlySet<string> operationIdsNoEscopo); }`: uma frase em português por divergência; **lista vazia quando bate**. O conjunto traz **`operationId`, nunca caminho** (decisão 1 da revisão).
  - **O escopo** é `ContratoDivergenciaTests.OperationIdsNoEscopo` (`static readonly HashSet<string>`, mantido no próprio teste): uma lista de **`operationId`, nunca prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`, e `/api/auth/senha/*` é da fatia 1b). **Cada tarefa que entrega rotas, de qualquer fatia, acrescenta os `operationId` dela à lista no mesmo passo em que implementa as rotas, nunca antes** (uma operação do contrato no escopo que a API ainda não tem é divergência, e o teste falharia no meio da fatia) **e nunca depois** (`Toda_operacao_do_documento_gerado_esta_no_escopo` falha). O plano não escreve contagem de caminhos: a lista é a fonte.
  - Estratégia: lê `contrato/openapi.yaml` e o documento que a API gera (`GET` do documento pelo `ApiFactory`) com o **mesmo leitor**, para os `$ref` chegarem resolvidos, e compara **só as operações que a fatia já implementa**, listadas por **`operationId`** no próprio teste (**nunca por prefixo de caminho**: `/api/auth/senha/*` é da fatia 1b e `/api/rh/funcionarios/...` e `/api/gestor/equipe/...` têm rotas de várias fatias); na fatia 0 o escopo é `entrar`, `renovarSessao`, `sair` e `obterUsuarioLogado` (a Task 9 acrescenta `verificarSaude`, no mesmo passo em que implementa o `/health`); **cada tarefa que entrega rotas, de qualquer fatia, acrescenta os `operationId` dela à lista no mesmo passo em que implementa as rotas**. Para cada operação, compara: (1) se existe nos dois lados (operação do contrato no escopo que a API não tem, ou da API no escopo que o contrato não tem, é divergência); (2) `operationId`; (3) o **conjunto de códigos de resposta**; (4) os **tipos de mídia** de cada resposta (`application/json`, `application/problem+json`); (5) para o corpo da requisição e o corpo das respostas 2xx em JSON, **e para o corpo da requisição em `multipart/form-data`** (o envio de arquivo, fatia 1c em diante: compara **os nomes dos campos e o `required`** do schema do formulário; campo de arquivo é `type: string` com `format: binary`, ou `array` de `items` assim; o tipo de mídia do corpo, JSON ou `multipart/form-data`, também é comparado), o **schema resolvido**, descendo por `properties`, `items` e **`allOf`** (com guarda contra referência circular; **antes de comparar um schema, une `properties` e `required` dos membros do `allOf`**, resolvendo `$ref` de forma recursiva, e um `allOf` de um membro só vira o próprio membro: o contrato usa `allOf` com `Paginacao` mais `{ itens }` em toda lista paginada, e para pôr `description` ao lado de um `$ref`): tipo (`string`, `integer`, `number`, `boolean`, `array`, `object`), **aceita `null` ou não** (normalizando `type: [x, 'null']`, `nullable` e `oneOf` de dois membros em que um é `{ type: 'null' }`, em qualquer ordem: é assim que o contrato escreve o `null` de um `$ref`), **conjunto de nomes de propriedades**, **conjunto `required`**, `format` quando é `date-time`, `date` ou `binary`, e valores de `enum`; (6) parâmetros de caminho e de consulta: nome, onde está, se é obrigatório e o tipo.
  - **O que não compara** (de propósito): `summary`, `description`, `tags`, `example` e `examples` (os exemplos são validados contra os schemas pelo job `contrato` da CI, não contra a API); `minLength`, `maxLength` e outras restrições de valor (a regra "senha acima de 128 caracteres devolve 400" é do teste de comportamento); extensões `x-*` (inclusive `x-perfis`); `security` (o 401 sem token é do teste de comportamento; a matriz de perfis por rota é da fatia 1); cabeçalhos de resposta (como `Retry-After`; o teste da Task 9 confere); o corpo dos erros além do tipo de mídia; `info`, `servers` e a versão do contrato. **O teste de divergência não prova que o JSON real é o descrito** (a serialização é do `System.Text.Json`): por isso o teste de comportamento da Task 8 confere as chaves reais da resposta de `entrar`, e a fatia 1a o generaliza com um validador de resposta contra os schemas do contrato (que também trata `allOf`, decisão 2 da revisão).
- `ApiFactory : WebApplicationFactory<Program>`: ambiente `Testing`; **toda a configuração em memória** (inclusive `Jwt:Chave` e `Documentacao:Habilitada=true`); cada classe de teste recebe um banco novo com os scripts aplicados; semeia (com e-mails únicos, pois não se apaga funcionário) um RH ativo **sem cargo e sem setor** (como o admin do seed da Task 9), um funcionário ativo **com cargo e setor** (linhas em `CARGOS` e `SETORES` inseridas por SQL), um usuário com só `USUARIOS.ATIVO = false`, um cujo funcionário foi inativado por `UPDATE` (o trigger do grupo inativa o usuário junto) e um funcionário sem nível de acesso; o caso "funcionário inativo com usuário ativo" não existe no banco e só o teste do Application, com falsos, o cobre; permite trocar o `IRelogio` por um falso e o ambiente por `Production`; registra uma rota de teste que provoca um erro de trigger, **fora do escopo do teste de divergência** (caminho `/__teste/...`, `ExcludeFromDescription()` e **`AllowAnonymous()`**: a fatia 1a passa a fechar por omissão toda rota sem política, e sem isso o teste do erro de trigger, que chama sem login, receberia 401; a Task 5 da 1a o confere, no Step 4b).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// AutenticacaoEndpointsTests
Entrar_com_credenciais_validas_devolve_200_com_tokens
Resposta_de_entrar_tem_exatamente_as_chaves_do_contrato_em_camelCase_e_perfil_como_texto   // substitui o teste que lia contrato/sessao.exemplo.json; chaves literais do contrato: tokenDeAcesso, acessoExpiraEmUtc (termina em Z), refreshToken, usuario{id, funcionarioId, nome, email, perfil, cargo, setor}; o teste de divergência não prova a serialização real
Entrar_devolve_cargo_e_setor_quando_o_funcionario_tem_e_null_nos_dois_quando_nao_tem            // o RH semeado sem cargo nem setor traz "cargo": null e "setor": null (a chave existe)
Entrar_renovar_e_sair_com_campo_vazio_devolvem_400_com_errors_por_campo                         // senha vazia, e-mail vazio, refreshToken vazio
Textos_dos_erros_sao_os_do_contrato   // [Theory] com a tabela de erros acima: status, title e detail (400 de entrar com "Preencha o e-mail e a senha.", 400 de renovar e de sair com "Confira os campos indicados.", 401 de login, 401 sem token, 403 dos dois casos, 422, 500); 429 é da Task 9
Entrar_com_usuario_inativo_e_senha_correta_devolve_403_com_a_mensagem_de_acesso_desativado
Entrar_com_email_inexistente_senha_errada_e_inativo_com_senha_errada_devolvem_o_mesmo_status_title_e_detail   // Review Focus 4; NÃO comparar traceId
Entrar_com_senha_acima_de_128_caracteres_devolve_400
Entrar_com_senha_unicode_funciona
Renovar_rotaciona_e_o_token_antigo_passa_a_devolver_401
Renovar_simultaneo_com_o_mesmo_token_nunca_devolve_500_no_maximo_um_200_e_o_token_do_vencedor_continua_valido   // Review Focus 3
Eu_sem_token_ou_com_token_malformado_devolve_401_ProblemDetails_sem_stack_trace                      // Review Focus 5
Eu_com_token_expirado_devolve_401                                                                    // relógio falso avançado 20 minutos
Erro_inesperado_em_Production_devolve_500_ProblemDetails_sem_stack_trace
Entrar_com_funcionario_sem_nivel_devolve_403_perfil_nao_configurado
Erro_de_trigger_do_banco_devolve_422_ProblemDetails_com_detail_generico_e_a_mensagem_do_trigger_na_extensao_regraDoBanco
Eu_com_token_valido_devolve_o_usuario_com_perfil_como_texto_cargo_e_setor                            // Assert.Equal("RH", ...); cargo e setor do funcionário semeado com os dois
```

```csharp
// ContratoDivergenciaTests (Review Focus: API e contrato divergem)
Operacoes_da_API_no_escopo_batem_com_o_contrato   // escopo = OperationIdsNoEscopo: entrar, renovarSessao, sair, obterUsuarioLogado; a lista de divergências vem vazia
Toda_operacao_do_documento_gerado_esta_no_escopo  // nenhuma rota implementada fica fora da comparação por esquecimento; as rotas /__teste/ têm ExcludeFromDescription e não aparecem no documento
// ComparadorDeContratoTests (o comparador não pode passar em branco; documentos pequenos montados no próprio teste)
Comparador_acusa_operacao_que_so_existe_de_um_lado
Comparador_acusa_codigo_de_resposta_a_mais_ou_a_menos_e_tipo_de_midia_diferente
Comparador_acusa_campo_a_mais_campo_a_menos_tipo_diferente_required_diferente_e_nulabilidade_diferente
Comparador_acusa_operationId_diferente
Comparador_ignora_summary_description_example_minLength_e_extensoes_x
Comparador_trata_type_com_null_nullable_e_oneOf_com_null_como_a_mesma_coisa   // oneOf [{$ref}, {type: 'null'}] e [{type: 'null'}, {$ref}]
Comparador_une_properties_e_required_de_allOf_e_acusa_campo_que_falta   // contrato: allOf [Paginacao, { required: [itens], properties: { itens } }] × gerado: o mesmo schema achatado = bate; sem `total` no gerado = acusa; allOf de um membro só com description ao lado do $ref = o próprio membro
Comparador_compara_so_os_operationId_do_escopo_e_nao_confunde_caminho_que_e_prefixo_de_outro   // /api/rh/beneficios (no escopo) e /api/rh/beneficios-adesoes (fora): só a primeira é comparada
Comparador_resolve_ref_e_nao_trava_em_referencia_circular
Comparador_compara_multipart_form_data_pelos_nomes_dos_campos_e_required_e_o_arquivo_como_binary   // contrato: multipart com { modo, tipo, descricao, dataValidade, arquivo (string binary) } e required [modo, tipo] × gerado igual = bate; acusa campo a menos, required diferente, arquivo como string sem format binary e corpo application/json no lugar de multipart/form-data (a 1c depende deste teste)
Comparador_acusa_quando_se_tira_o_campo_cargo_de_UsuarioResposta_do_contrato_real_em_memoria   // controle positivo com o contrato verdadeiro
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~AutenticacaoEndpointsTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~ComparadorDeContratoTests"`
Expected: FAIL (404 nas rotas; tipos do comparador não definidos).

- [ ] **Step 3: Implementar os endpoints** (minimal API, com `WithName`, `Produces` e `ProducesProblem` de todos os códigos do contrato), `JwtBearer` lendo `OpcoesJwt`, `OnChallenge` com `ProblemDetails`, tratamento global de exceções (inclusive a tradução do erro de trigger), o documento OpenAPI nativo sob `Documentacao:Habilitada` (`DocumentacaoExtensions.cs`) e os registros em `Program.cs`. **Ainda não o comparador.**
- [ ] **Step 4: Sonda do documento OpenAPI nativo (antes do comparador).** O comparador e o teste de divergência se apoiam em suposições sobre o gerador do .NET 10 que ninguém mediu. Escrever `SondaDoOpenApiNativoTests` (lê o documento pelo `ApiFactory`, só com os endpoints do Step 3):

```csharp
Documento_declara_openapi_3_1                                      // o contrato é 3.1.0 (CONVENCOES.md, seção 1)
Propriedades_nao_anulaveis_dos_records_saem_em_required            // SessaoResposta: tokenDeAcesso, acessoExpiraEmUtc, refreshToken, usuario
Propriedade_anulavel_sai_como_type_com_null_ou_oneOf_com_null      // UsuarioResposta.cargo e .setor; o teste anota QUAL das duas formas saiu
Enum_em_texto_sai_como_enum_de_strings                             // UsuarioResposta.perfil: Funcionario, Gestor, RH
Respostas_de_erro_saem_como_application_problem_json               // entrar: 400, 401, 403, 429 e 500
Rota_do_documento_e_openapi_v1_json                                // suposta; se for outra, anotar qual
```

Run: `dotnet test back-end --filter SondaDoOpenApiNativoTests`
Expected: cada teste passa ou falha **mostrando o que o gerador escreveu**; anotar o resultado dos seis (vai para o `README.md`, Task 9, Step 6). **O que fazer com cada falha** (nunca relaxar o teste de divergência nem mudar o contrato): diferença só de **forma** (a mesma informação escrita de outro jeito: `nullable` ou `oneOf` com `null` no lugar de `type: [x, 'null']`; `allOf` de um membro só): **normalizar no comparador**, que já o prevê no Step 1 (acrescente o que faltar, com teste). Diferença de **significado** (sem `required`, enum como número, sem `application/problem+json`, versão 3.0): **configurar o gerador** (transformadores de schema e de operação do `AddOpenApi`; a versão pela opção de versão do `OpenApiOptions`: conferir o nome na documentação do pacote); se ainda assim não bastar, **dúvida para o dono** (versão diferente da 3.1: a linha da seção 1 de `contrato/CONVENCOES.md`). Depois do ajuste os seis testes passam e ficam como regressão do gerador.

- [ ] **Step 5: Implementar `LocalizadorDoContrato`, `ComparadorDeContrato` (com `allOf`, `oneOf` com `null`, `$ref` e referência circular) e o carregamento dos dois documentos** com as assinaturas acima, **aplicando o que a sonda decidiu** (normalização no comparador ou configuração do gerador).
- [ ] **Step 6: Rodar e ver passar.** Mesmo comando do Step 2, mais `|FullyQualifiedName~SondaDoOpenApiNativoTests`; Expected: PASS. **Se `Operacoes_da_API_no_escopo_batem_com_o_contrato` falhar, a divergência é lida como erro da API, não do teste**; só o dono muda o contrato (regra do contrato, `docs/superpowers/plans/2026-10-30-plano-back-end.md`, seção 5).
- [ ] **Step 7: Commit** (com autorização): `git add back-end; git commit -m "feat: endpoints de autenticacao e teste de divergencia com o contrato"`

---

### Task 9: Limite de tentativas, proxy, CORS, saúde, documentação da API, scripts na inicialização e seed

**Files:**
- Create: `back-end/src/RHSystem.Api/Configuracao/LimiteDeTentativas.cs`, `Cors.cs`, `ProxyReverso.cs`, `InicializacaoDoBanco.cs`, `Saude/BancoHealthCheck.cs`
- Modify: `back-end/src/RHSystem.Api/Program.cs`, `appsettings.json`, `appsettings.Development.json`, `back-end/src/RHSystem.Api/Configuracao/DocumentacaoExtensions.cs` (da Task 8: aqui entra a UI), `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs` (limite de login 1000 por padrão), `back-end/tests/RHSystem.Api.IntegrationTests/Contrato/ContratoDivergenciaTests.cs` (o escopo ganha `verificarSaude`, no mesmo passo em que a rota é implementada), `README.md`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Configuracao/LimiteDeTentativasTests.cs`, `SaudeTests.cs`, `InicializacaoTests.cs`, `CorsTests.cs`, `DocumentacaoTests.cs`

**Interfaces:**
- Produces (configuração; as da Task 7 já existem): `Database:AplicarScriptsAoIniciar` (bool), `Autenticacao:LimiteLoginPorMinuto` (padrão 5), `Cors:OrigensPermitidas` (lista), `ProxyReverso:Habilitado` (bool), `Documentacao:Habilitada` (bool), `Seed:AdminNome|AdminEmail|AdminSenha`.
- Limitador: **5 requisições por minuto por IP** em `POST /api/auth/entrar`; a sexta devolve **`429`** com `ProblemDetails` e `Retry-After` (o limitador do ASP.NET rejeita por padrão com 503 e sem corpo: configure `RejectionStatusCode` e `OnRejected`). O IP vem de `RemoteIpAddress`; com `ProxyReverso:Habilitado` o `UseForwardedHeaders` (`X-Forwarded-For` e `-Proto`) o ajusta, aceitando só o proxy da plataforma. Cada tentativa de login registra um log `Information` com o IP.
- **CORS:** só as `Cors:OrigensPermitidas`, e **expõe os cabeçalhos `Content-Disposition` e `Retry-After`** (`WithExposedHeaders`): o navegador esconde do JavaScript do front os cabeçalhos que não são "seguros" para CORS, e sem isso o nome do arquivo baixado e a espera do 429 não chegariam à tela (decisão 19 da revisão).
- `GET /health` (`operationId` `verificarSaude`, acrescentado a `OperationIdsNoEscopo` neste passo) → `200 {"status":"ok"}` (o corpo é `SaudeResposta`, `contrato/fontes/10-fatia-0-autenticacao.yaml:160-175`) com o banco acessível, `503` se ele cair **depois da subida** (o contrato **não define o corpo do 503**: `10-fatia-0-autenticacao.yaml:174-175`; o teste de divergência compara só o código). Com o banco parado na partida e `AplicarScriptsAoIniciar` ligado, a API falha ao iniciar (aceito e documentado no README). **Para o `/health` aparecer no documento OpenAPI** (suponho que a rota de `MapHealthChecks` não apareça; **não medido**, conferir): expor `/health` como um `MapGet` que consulta o `HealthCheckService` e declara `WithName("verificarSaude")`, `Produces<SaudeResposta>(200)` e `Produces(503)`.
- `Documentacao:Habilitada` publica o **documento OpenAPI nativo do .NET 10** (`Microsoft.AspNetCore.OpenApi`, **versão do documento 3.1, medida pela sonda da Task 8 (Step 4) e registrada no README**; a Task 8 já o liga) e **uma UI que lê esse documento nativo** (`Scalar.AspNetCore`, ou o Swagger UI apontado para ele; **não** deixe a UI gerar um segundo documento por conta própria, como o `Swashbuckle.AspNetCore` faz, para não haver dois documentos diferentes; conferir a compatibilidade com o .NET 10). **O documento nativo é a fonte do teste de divergência** (Task 8). **O documento e a UI ficam anônimos** (fora de qualquer família de rota e sem `RequireAuthorization`) quando `Documentacao:Habilitada` é verdadeiro: a fatia 1a passa a fechar por omissão toda rota fora das famílias e **exclui `/openapi/*` e a UI** dessa regra (decisão 9 da revisão), e o teste `Documento_openapi_e_UI_respondem_sem_login_quando_habilitados` é quem o prova nas duas fatias. Ligada em `Development` e na demo; desligada por padrão em produção. **Se a versão do documento nativo não for a 3.1**, avisar o dono para ajustar a linha do `contrato/CONVENCOES.md` (seção 1) que pede esta conferência: o contrato é do dono.
- Inicialização: com `AplicarScriptsAoIniciar=true`, `IFonteDeScripts.Carregar()` (registrada como `ScriptsEmbutidos`; os testes a substituem) + `ExecutorDeScripts.AplicarPendentesAsync`. Se um script aplicado foi alterado, **a API não sobe**, com a mensagem da exceção. Seed: se `Seed:AdminEmail` e `Seed:AdminSenha` estiverem preenchidos **e não existir usuário**, cria `Funcionario` (nome da configuração, e-mail normalizado, admissão = hoje no fuso `-03:00`, nível `RH` lido de `NIVEIS_DE_ACESSO`, **sem cargo, sem setor e sem matrícula**: ver "Decisões deste plano") e o `Usuario` com hash (`SENHA_PROVISORIA` fica no padrão `FALSE` do banco, `V032`: o admin tem senha de verdade); nunca registra a senha em log; se o nível `RH` não existir, falha com mensagem clara.
- `appsettings.Development.json` traz só valores locais, sem segredo (`ConnectionStrings:Padrao` para o compose, com `AllowPublicKeyRetrieval=True` **só em desenvolvimento**). A chave do JWT local vem de user-secrets: `dotnet user-secrets init --project back-end/src/RHSystem.Api` e `dotnet user-secrets set "Jwt:Chave" "<32+ bytes>" --project back-end/src/RHSystem.Api`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Sexta_tentativa_de_login_no_mesmo_minuto_devolve_429_ProblemDetails_com_Retry_After   // limite 5 só neste teste; title "Muitas tentativas." e detail "Muitas tentativas. Aguarde um minuto e tente de novo." (contrato)
Health_devolve_200_com_banco_e_503_com_connection_string_para_porta_fechada            // Review Focus 5; AplicarScriptsAoIniciar=false; não parar o contêiner compartilhado; o corpo do 200 é exatamente {"status":"ok"}
Operacoes_da_API_no_escopo_batem_com_o_contrato   // (Task 8) agora com os cinco operationId da fatia 0: entrar, renovarSessao, sair, obterUsuarioLogado e verificarSaude (200 e 503); o último entra na lista neste passo, junto com a rota
Documentacao_so_responde_quando_Documentacao_Habilitada_e_true   // documento OpenAPI e UI; com false, 404 (a fatia 1a, ao ligar a política de reserva, roda este teste de novo e mede, Task 5 Step 4b, se o 404 continua)
Documento_openapi_e_UI_respondem_sem_login_quando_habilitados    // decisão 9: GET sem Authorization devolve 200 no documento e na UI; vale também depois que a 1a fechar o resto por omissão
Seed_cria_funcionario_e_usuario_admin_uma_unica_vez_e_nao_cria_sem_configuracao        // banco novo com scripts; o admin criado sai de GET /api/auth/eu com cargo e setor null
Inicializacao_aplica_os_scripts_quando_AplicarScriptsAoIniciar_e_true                   // banco novo e vazio (NovoBancoAsync)
Inicializacao_recusa_subir_se_um_script_aplicado_foi_alterado_com_IFonteDeScripts_falsa                           // Review Focus 2
Cors_so_permite_as_origens_configuradas
Cors_expoe_Content_Disposition_e_Retry_After                      // requisição com Origin permitido: Access-Control-Expose-Headers lista os dois (decisão 19)
Com_ProxyReverso_habilitado_o_limite_e_por_IP_do_cabecalho_X_Forwarded_For            // dois IPs distintos não dividem o balde
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Api.IntegrationTests.Configuracao|FullyQualifiedName~ContratoDivergenciaTests"`
Expected: FAIL.

- [ ] **Step 3: Implementar** limitador (o `429` com o `title` e o `detail` do contrato), proxy, CORS (**com `WithExposedHeaders("Content-Disposition", "Retry-After")`**), health check (como `MapGet` se o `/health` não aparecer no documento, ver acima; **`verificarSaude` entra em `OperationIdsNoEscopo` neste passo**), a UI da documentação (**e o documento e a UI sem login**, ver acima), inicialização e seed.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Rodar tudo do back-end**

Run: `dotnet test back-end`
Expected: todos passando.

- [ ] **Step 6: Preencher no `README.md`** os comandos reais: `docker compose up -d --wait`, `dotnet run --project back-end/src/RHSystem.Api`, como recriar o banco local (`docker compose down -v`) e a nota sobre a API não subir sem banco; mais o resultado da sonda do documento OpenAPI nativo (Task 8, Step 4: versão do documento, rota, `required`, `null`, enum e `problem+json`, e o que foi normalizado ou configurado) e a UI escolhida.
- [ ] **Step 7: Commit** (com autorização): `git add back-end README.md; git commit -m "feat: limite de tentativas, proxy, cors, saude, scripts na inicializacao e seed"`

---

### Task 10: Front-end: projeto, cliente de API e contexto de autenticação

**Saiu deste plano (2026-10-30).** Esta tarefa agora está em `docs/plano-front-end/fatia-0.md` (projeto do front, cliente de API gerado do contrato e contexto de autenticação), feita pela pessoa do front com a sessão dela e com dados simulados (MSW) do contrato. **As Tasks 12 a 14 só começam quando ela estiver na `main`.** A numeração das outras tarefas não mudou, para não quebrar referências.

O que o back-end precisa saber dela: o front fala com a API só pelo que o contrato descreve (`contrato/openapi.yaml`), e o `contrato/sessao.exemplo.json` que esta tarefa lia **não existe mais** (a Task 8 o substituiu pelo teste de divergência).

---

### Task 11: Front-end: tela de login, shell e cabeçalhos de segurança

**Saiu deste plano (2026-10-30).** Esta tarefa agora está em `docs/plano-front-end/fatia-0.md` (tela de login, shell do portal e `front-end/public/staticwebapp.config.json` com a política de segurança). **As Tasks 12 a 14 só começam quando ela estiver na `main`.**

Onde foi cada parte que não é só do front:
- A **verificação manual ponta a ponta** (que era o Step 5 desta tarefa: banco, API e front no ar, entrar com o admin, F5, fechar e reabrir, sair em uma aba e a outra deslogar) virou **passo de integração do plano do front**, que só roda quando `contrato/STATUS.md` disser que as rotas existem (`pronta`). O que cabe ao back-end: Tasks 4 e 9 prontas e, no ambiente real, o Step 7 da Task 14.
- A **política de segurança do site** (`Content-Security-Policy` com `default-src 'self'` e `connect-src 'self'` mais a URL da API, `X-Content-Type-Options`, `X-Frame-Options`, `Referrer-Policy`, `Permissions-Policy`) é do plano do front; **a Task 14 só preenche nela a URL real da API** no `connect-src`.
- Os textos de erro da tela de login vêm do contrato (os `detail` da tabela de erros da Task 8).

---

### Task 12: Integração contínua (GitHub Actions)

**Só começa quando o front da fatia 0 estiver na `main`** (regra do dono, 2026-10-30; vale também para as Tasks 13 e 14).

**Files:** Create: `.github/workflows/ci.yml`.

**Interfaces:**
- Produces: workflow `CI` em `push` e `pull_request`, com **três jobs independentes**: `contrato`, `back-end` e `front-end`. `back-end` (`ubuntu-latest`, que traz Docker para o Testcontainers): setup-dotnet com `global-json-file: global.json`, `dotnet restore`, `dotnet build --no-restore`, `dotnet format back-end --verify-no-changes`, `dotnet test --no-build` e dois guardas de scripts, **agora sobre `V001` a `V005`**: (1) **nenhum `.sql` solto em `Banco de Dados/`** fora de `Scripts/` e `Validacao/` (impede que um envio pela web recrie o arquivo antigo); (2) se existir `Banco de Dados/Scripts/CONGELADOS.sha256` (criado na Task 14, com `V001` a `V005`), os hashes das versões listadas não mudaram, **calculados pelo comando bash do bullet "Hash dos scripts"** abaixo (um PR que edita script congelado passaria nos testes, que criam banco novo, e só quebraria no Azure); o passo **imprime sempre os hashes** (a prova do Step 3, no Linux).
- **Job `contrato` (novo, 2026-10-30):** `ubuntu-latest`, setup-node com `node-version: 24` (o Node 24 LTS da Task 1), `npm --prefix contrato ci`, `npm --prefix contrato run validar`. Li em `contrato/package.json` que o `validar` roda `node montar.mjs && redocly lint openapi.yaml && node validar-exemplos.mjs openapi.yaml`: junta `contrato/fontes/*.yaml` em `openapi.yaml`, roda o lint do Redocly e valida **cada exemplo** contra o schema dele (medi em 2026-10-30: `1471 exemplos validados, 0 inválidos`; o número muda quando o contrato muda, e o job falha se algum exemplo for inválido ou o lint reclamar). Mais um guarda: depois do `validar`, `git diff --exit-code -- contrato/openapi.yaml contrato/ROTAS.md contrato/PROVISORIOS.md` (o `montar` regenera os três; se o que está commitado ficou para trás das fontes, o job falha; li em `contrato/montar.mjs` que ele não grava data, então a saída é estável).
- **Job `front-end`:** só passa a existir quando o front da fatia 0 estiver na `main` (por isso esta task só começa então). Setup-node com `node-version: 24` (o Node 24 LTS da Task 1), `npm ci`, `lint`, `format:check`, `test -- --run`, `build` e o que mais o plano do front (`docs/plano-front-end/fatia-0.md`) listar como prova de "pronto" (por exemplo a verificação do build sob a política de segurança): **confira a lista no arquivo**.

- **Hash dos scripts (decisão 20 da revisão, 2026-10-30).** O hash do congelamento é o SHA-256 do conteúdo **sem BOM e com CRLF normalizado para LF**, em hexadecimal minúsculo: **o mesmo algoritmo do executor** (Task 4) e **o mesmo valor no PowerShell e no Linux**. `CONGELADOS.sha256` tem uma linha por script, o hash e o nome do arquivo separados por **dois espaços**, em ordem de nome e com fim de linha LF. O comando que **gera** (Windows PowerShell 5.1, da raiz; é o que a Task 14, Step 9, usa):

  ```powershell
  $utf8 = New-Object System.Text.UTF8Encoding($false)
  Get-ChildItem "Banco de Dados\Scripts" -Filter "V*.sql" | Sort-Object Name | ForEach-Object {
    $texto = [System.IO.File]::ReadAllText($_.FullName, $utf8).Replace("`r`n", "`n")
    $hash = [System.BitConverter]::ToString([System.Security.Cryptography.SHA256]::Create().ComputeHash($utf8.GetBytes($texto))).Replace("-", "").ToLowerInvariant()
    "$hash  $($_.Name)"
  }
  ```

  O comando que **confere** (bash: o Linux da CI e o Git Bash; da raiz). É o corpo do passo "Hashes dos scripts" do job `back-end`: imprime sempre as linhas e, se `CONGELADOS.sha256` existir, falha quando alguma linha dele não bate com o arquivo de hoje (script editado ou sumido); script novo, que o arquivo não lista, não falha:

  ```bash
  cd "Banco de Dados/Scripts"
  atuais=$(for f in $(LC_ALL=C ls V*.sql); do printf '%s  %s\n' "$(sed '1s/^\xEF\xBB\xBF//; s/\r$//' "$f" | sha256sum | cut -d' ' -f1)" "$f"; done)
  echo "$atuais"
  if [ -f CONGELADOS.sha256 ]; then
    while read -r hash nome; do
      echo "$atuais" | grep -qxF "$hash  $nome" || { echo "Script congelado alterado ou sumiu: $nome"; exit 1; }
    done < CONGELADOS.sha256
  fi
  ```

  **Medido em 2026-10-30:** nos 42 scripts `V006` a `V047` que existiam então (hoje juntados no `V001`), e em cópias de um deles em LF, em CRLF e com BOM mais CRLF, o PowerShell 5.1, o PowerShell 7 e o `sed` com `sha256sum` do Git Bash dão o mesmo valor. **Não medido:** o Linux da CI (o Step 3 e o Step 4 o medem).

- [ ] **Step 1: Escrever o workflow.** Fixar cada ação por versão maior estável conferida na documentação da ação na hora, não de memória.
- [ ] **Step 2: Validar localmente**

Run: `npm --prefix contrato ci; npm --prefix contrato run validar; dotnet format back-end --verify-no-changes; npm --prefix front-end ci; npm --prefix front-end run format:check`
Expected: o contrato valida (`0 inválidos`); sem diferenças de formatação; instalações limpas.

- [ ] **Step 3: Provar que o hash é o mesmo no PowerShell e no Linux (decisão 20).** (a) Na raiz, gravar a saída do comando PowerShell em `$env:TEMP\hashes-ps.txt` (`... | Set-Content -Encoding ASCII $env:TEMP\hashes-ps.txt`) e, no Git Bash, a do comando bash (até o `echo "$atuais"`, sem o `if`; redirecionar para `"$TEMP/hashes-bash.txt"`). (b) Repetir os dois comandos com cópias de **um** script em LF, em CRLF e com BOM mais CRLF, numa pasta temporária **fora do repositório** com a mesma estrutura (`Banco de Dados\Scripts`).

Run: `Compare-Object (Get-Content $env:TEMP\hashes-ps.txt) (Get-Content $env:TEMP\hashes-bash.txt)`
Expected: nenhuma diferença, **5 linhas** de cada lado; em (b), as três cópias dão o mesmo hash nos dois comandos.

- [ ] **Step 4: Com autorização do dono, dar push em uma branch** e conferir os três jobs.

Run: `gh run watch` (ou abrir a aba Actions)
Expected: os três jobs verdes (`contrato`, `back-end` e `front-end`); no log do job `back-end`, o passo "Hashes dos scripts" imprime **as mesmas 5 linhas** que o PowerShell deu no Step 3 (a prova no Linux).

- [ ] **Step 5: Commit** (com autorização): `git add .github; git commit -m "ci: contrato, build e testes do back-end e do front-end"`

---

### Task 13: Workflows de deploy manual

**Só começa quando o front da fatia 0 estiver na `main`:** o `deploy-front` publica `front-end/`, que vem do plano do front.

**Files:** Create: `.github/workflows/deploy-api.yml`, `.github/workflows/deploy-front.yml`.

**Interfaces:**
- Produces: dois workflows com `workflow_dispatch` e disparo por tag `v*`, **nunca por push em branch**. `deploy-api` roda os testes e, se passarem, publica `RHSystem.Api` (`dotnet publish -c Release`) no App Service e **só termina verde depois que `GET /health` responde 200** (com novas tentativas por alguns minutos; a URL vem da variável `API_URL`), usando o **publish profile** (o método mais simples para quem nunca usou Azure; a Task 14 liga a autenticação básica do SCM, desligada em App Services novos) guardado no secret `AZURE_WEBAPP_PUBLISH_PROFILE`. `deploy-front` gera o build com `VITE_API_URL` (variável do repositório) e publica no Static Web Apps com o token do secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.
- **O botão "Run workflow" só aparece quando o workflow está na branch padrão (`main`):** é preciso fazer merge para `main` (com autorização do dono) antes da Task 14, Step 6.

- [ ] **Step 1: Escrever os dois workflows**, conferindo a sintaxe das ações oficiais de deploy do Azure na documentação atual.
- [ ] **Step 2: Validar a sintaxe** (por exemplo com `actionlint`, se instalado) e, depois do merge para `main`, conferir que os dois aparecem em Actions.

Expected: workflows listados, sem erro de sintaxe. **O deploy em si só é verificado na Task 14.**

- [ ] **Step 3: Commit** (com autorização): `git add .github; git commit -m "ci: workflows de deploy manual para o Azure"`

---

### Task 14: Azure guiado: provisionar, publicar e verificar

**Só começa quando o front da fatia 0 estiver na `main`** (ela edita `front-end/public/staticwebapp.config.json` e confere o ambiente pelo shell do front). **Antes do Step 9 (congelamento), o Kauan precisa ter conferido o `V001` e o `V002`** (ver o alerta no começo deste plano).

**Feita junto com o dono, que nunca usou o Azure. Antes de começar, o dono deve ter confirmado qual conta tem os créditos do SENAI. Cada passo é conferido na documentação atual (Microsoft Learn), não de memória. O assistente nunca vê os valores dos segredos: o dono os gera e cola direto no portal e no GitHub.**

**Files:** Create: `docs/azure/passo-a-passo-fatia-0.md` (roteiro com os nomes e valores reais, sem segredos). Modify: `README.md` (link), `front-end/public/staticwebapp.config.json` (**arquivo do plano do front**; aqui só se preenche a URL da API no `connect-src`).

**Interfaces:**
- Produces: grupo de recursos único; App Service **Linux**; Static Web Apps; MySQL Flexible Server; um banco `rh_system` com **usuário próprio da API** (privilégios `SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES, TRIGGER, CREATE ROUTINE, ALTER ROUTINE, EXECUTE` só nesse banco, criado por um script SQL curto que o roteiro traz; `DROP` no banco também permite apagar o próprio banco, risco aceito e documentado); configurações do App Service `ConnectionStrings__Padrao`, `Jwt__Chave|Emissor|Audiencia`, `Cors__OrigensPermitidas__0`, `Database__AplicarScriptsAoIniciar=true`, `Database__ServerVersion`, `ProxyReverso__Habilitado=true`, `Documentacao__Habilitada`, `Seed__AdminNome|AdminEmail|AdminSenha`; secrets e variável do GitHub da Task 13.

- [ ] **Step 1: Confirmar a conta com os créditos e a região permitida** (que ofereça o MySQL e o Static Web Apps, conforme a Task 1, Step 5). **Parar se não estiver confirmada.**
- [ ] **Step 2: Conferir, na conta, o plano e o orçamento:** o plano do App Service escolhido tem Always On? Criar um **alerta de orçamento** no Cost Management antes de qualquer recurso. **Perguntar ao dono** se aceita um plano pago do App Service (o Always On, de que o agendador das fatias seguintes depende, exige nível pago) e, se o crédito acabar, que a demo cai.
- [ ] **Step 3: Para cada recurso, dizer o SKU exato e o custo estimado e esperar o OK do dono** (os padrões do portal costumam ser caros: General Purpose, alta disponibilidade, Premium; o assistente diz qual valor escolher em cada tela). Ordem: grupo de recursos, **App Service**, MySQL Flexible Server (firewall: **perguntar ao dono** entre liberar só os IPs de saída do App Service, mais seguro e mais trabalhoso, ou "permitir serviços do Azure", mais simples; mais o IP do dono quando preciso), Static Web Apps **com origem de deploy "Other"** e sem o Deployment Center do App Service (os dois criariam workflows que publicam a cada push, contra o spec).
- [ ] **Step 4: Segredos e banco.** O dono gera a chave do JWT e a senha do admin **localmente** (o roteiro traz o comando) e cola direto nas configurações do App Service e nos secrets do GitHub. Ligar **HTTPS Only** e a autenticação básica do SCM (para o publish profile). Criar o banco (`CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`) e o usuário da API com o script SQL do roteiro, que também traz a **receita de recuperação**: se a `V001` falhar no meio no primeiro deploy, apagar e recriar o banco (aceitável enquanto não há dados); depois do primeiro deploy bem-sucedido, nunca recriar. Os triggers e funções ficam com o usuário da API como `DEFINER`: **não trocar esse usuário sem recriar os triggers e as funções**. **Conferir que o usuário da API consegue criar triggers** (resultado da Task 1, Step 5e); se o servidor exigir um parâmetro (por exemplo `log_bin_trust_function_creators`), ajustá-lo conforme a documentação. Ajustar também o parâmetro `time_zone` do servidor para `-03:00` e conferir com `SELECT @@global.time_zone, NOW(), UTC_TIMESTAMP()`.
- [ ] **Step 5: Atualizar o `connect-src`** do `front-end/public/staticwebapp.config.json` com a URL real da API (**só isso**: o resto da política é do plano do front), rodar `npm --prefix front-end test -- --run` e `npm --prefix front-end run build` para conferir que nada do front quebrou, e conferir que `Cors__OrigensPermitidas__0` é a URL real do front.
- [ ] **Step 6: Disparar `deploy-api` e `deploy-front`** pelo GitHub Actions (os workflows já estão em `main`).

Expected: os dois verdes, e a API sobe aplicando `V001` a `V005` (ver o log do App Service).

- [ ] **Step 7: Verificar no ambiente real**

Run: `curl.exe -s -o NUL -w "%{http_code}" https://<url-da-api>/health` e abrir a URL do front.
Expected: `200`; no navegador, o dono entra com o admin, vê o shell (do plano do front), sai, e **F5 e fechar e reabrir o navegador não deslogam**. Depois conferir no log do App Service o IP registrado em uma tentativa de login: **não pode ser o IP do proxy** (se for, o limite de login virou global; corrigir `ProxyReverso`). **Com tudo isso verificado, o dono marca a fatia 0 como `pronta` em `contrato/STATUS.md`** (é o que libera o passo de integração do plano do front; `pronta` quer dizer implementada, testada e publicada no ambiente da demo).

- [ ] **Step 8: Remover a configuração `Seed__AdminSenha`** do App Service (o admin já existe) e registrar no roteiro. A troca de senha chega na fatia 1.
- [ ] **Step 9: Congelar a base de scripts.** Criar `Banco de Dados/Scripts/CONGELADOS.sha256` com o hash e o nome de cada script aplicado (uma linha `<hash>  <nome>` por script), **`V001` a `V005`** (o guarda da CI, Task 12, passa a vigiá-lo). **O hash é o do executor** (SHA-256 do conteúdo sem BOM e com CRLF normalizado para LF; ver Task 4), calculado **pelo comando PowerShell da Task 12 (bullet "Hash dos scripts")**, que dá o mesmo valor que o `sed` com `sha256sum` do guarda da CI (provado no Step 3 da Task 12). Gravar o arquivo com `[System.IO.File]::WriteAllText(...)` em UTF-8 sem BOM e fins de linha LF (**não** com `>` nem `Out-File`, que no PowerShell 5.1 gravam UTF-16 ou CRLF) e **nunca** com `Get-FileHash`, que hasheia os bytes como estão e dá outro valor numa cópia em CRLF: o guarda da CI no Linux quebraria. **Confira** o resultado contra o próprio executor: `SELECT VERSAO, HASH_SHA256 FROM SCHEMA_VERSOES ORDER BY VERSAO` no banco em que ele acabou de aplicar tudo (o do compose local ou o do Azure) traz **os 5 mesmos hashes**, versão a versão; e, no PR, o passo "Hashes dos scripts" da CI fica verde. A renormalização de CRLF para LF (`git add --renormalize .`) já foi feita no mesmo commit do `.gitattributes`, no Passo 0. Registrar no README e no roteiro que **nenhum script aplicado pode mais ser editado** (a API deixa de subir se for), e **avisar o grupo e o Kauan**: toda mudança daqui em diante, inclusive correção que ele ache no `V001` ou no `V002`, é um script novo, numerado pelo dono (`ALTER` para tabelas; `DROP` e `CREATE` para trigger e função).
- [ ] **Step 10: Deixar o ambiente seguro para o custo.** Mostrar ao dono como parar ou apagar o que gasta crédito, registrando que **parar o app não zera o custo do plano do App Service** (só apagar o plano ou rebaixá-lo) e que o MySQL parado pode religar sozinho depois de um tempo (conferir o prazo na documentação). Encerrar as sessões de `az` e `gh`.
- [ ] **Step 11: Commit** (com autorização), **só com os caminhos que esta Task cria ou altera** (inclusive o `CONGELADOS.sha256` do Step 9, que o guarda da CI passa a vigiar; nada de `front-end` inteiro, que é de outra pessoa): `git add docs/azure README.md front-end/public/staticwebapp.config.json "Banco de Dados/Scripts/CONGELADOS.sha256"; git diff --cached --name-only` (Expected: só caminhos sob `docs/azure/`, mais `README.md`, `front-end/public/staticwebapp.config.json` e `Banco de Dados/Scripts/CONGELADOS.sha256`); depois `git commit -m "docs: roteiro de deploy no Azure da fatia 0 e congelamento dos scripts"`

---

## Self-review (cobertura do spec, linha 0 da seção 8)

| Item da fatia 0 | Task |
|---|---|
| Solution em camadas | 2 |
| `docker compose` | 3 (só MySQL; Mailpit e Azurite entram nas fatias que os usam) |
| Scripts SQL versionados com o executor (hash, trava, congelamento) | 4, 12 e 14 |
| Teste de fumaça do provider | 5 |
| Login: JWT, refresh com rotação, inativo bloqueado, limite de tentativas | 5 a 9 |
| Endpoint de saúde, scripts na inicialização, segredos | 9 e 14 |
| Cabeçalhos de segurança no front (spec, seção 5) | plano do front, `docs/plano-front-end/fatia-0.md` (antiga Task 11), e 14 (URL da API) |
| Shell do React | plano do front (antigas Tasks 10 e 11) |
| CI com verificação de formatação nos dois lados, e o contrato | 12 (jobs `contrato`, `back-end` e `front-end`) |
| Arquivos da raiz e renormalização (Passo 0) | 0 |
| Contrato OpenAPI implementado e teste de divergência | 8 e 9 |
| Deploy no Azure (manual; custo dito antes; conta confirmada; Always On, runtime e triggers conferidos; base congela) | 1, 13 e 14 |
