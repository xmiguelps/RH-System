# Fatia 0 (esqueleto) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar o esqueleto do RH-System, com API em camadas, banco MySQL mantido por scripts SQL versionados, login com JWT, shell do React, CI e deploy no Azure, tudo rodando de ponta a ponta.

**Architecture:** Monólito modular em Clean Architecture (`back-end/`) e React + TypeScript (`front-end/`), no mesmo repositório. O esquema do MySQL vem dos scripts SQL do grupo (`Banco de Dados/Scripts/`), aplicados por um executor próprio com hash de integridade; o EF Core só mapeia e consulta. A autenticação usa JWT de vida curta e refresh token com rotação, guardado só como hash.

**Tech Stack:** .NET 10, EF Core 9.x (sem migrations), `Pomelo.EntityFrameworkCore.MySql` 9.0.0, MySqlConnector, MySQL 8.4, xUnit, Testcontainers, React + TypeScript + Vite, Vitest + Testing Library + Prettier, GitHub Actions, Azure (App Service Linux, Static Web Apps, MySQL Flexible Server).

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seções 2, 3, 4, 5, 7 e 8; a fatia 0 é a linha 0 da tabela da seção 8).

## Global Constraints

- .NET 10 com **EF Core 9.x** (nenhum `Microsoft.EntityFrameworkCore.*` em 10.x) e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**. **Não há migrations do EF** nem `dotnet-ef`.
- Dependências: Api e Infrastructure dependem de Application; Application depende de Domain; **Domain não depende de nada**.
- Casos de uso são **classes simples**, sem biblioteca de mediação. Entidades do Domain mapeadas direto pelo EF (Fluent API na Infrastructure).
- **Banco por scripts SQL versionados** em `Banco de Dados/Scripts/` (`Vnnn__descricao.sql`). O executor aplica na inicialização, controlado por configuração, registra o hash de cada script e **recusa subir se um script já aplicado foi editado**. Os scripts podem ser corrigidos e os bancos locais recriados **até o primeiro deploy no Azure (Task 14); depois todo script aplicado fica congelado** e toda mudança é script novo (`ALTER` para tabelas; `DROP` e `CREATE` para trigger e função). A base do grupo **não está 100% pronta** (dono, 2026-10-28).
- Login por e-mail e senha, senha só como hash. JWT de vida curta; refresh token no banco, **revogável, com rotação e só como hash**. Tokens por **cabeçalho**, não cookie. Há limite de tentativas de login. O front tem **cabeçalhos de segurança**.
- Segredos **nunca** vão para o repositório: user-secrets, `.env` ignorado, configurações do App Service e secrets do GitHub. **O assistente nunca vê os valores dos segredos de produção:** o dono os gera e cola direto no portal.
- Testes de integração usam **MySQL real em contêiner** (Docker), aplicando os **mesmos scripts**. Verificação de formatação no back-end e no front-end.
- Pastas `back-end/` e `front-end/`. Deploy no Azure **manual ou por tag**, nunca a cada push.
- O ambiente Azure só é criado **depois de o dono confirmar qual conta tem os créditos**. O custo de cada recurso é dito antes de criá-lo, e o dono nunca usou Azure: cada passo é guiado.
- **Os arquivos de `Banco de Dados/` são do grupo:** só mover ou editar com autorização do dono e aviso ao grupo. **Commit e push só com autorização do dono** (perguntar uma vez, no início, se pode commitar por tarefa).
- Nomes de domínio e API em português sem acento (`Usuario`, `Entrar`); mensagens ao usuário em português do Brasil com acento.
- Comandos deste plano rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; no PowerShell o `curl` é alias, use `curl.exe`).

## Decisões deste plano (o spec não fixa)

Escolhidas aqui; o dono pode mudar na revisão:
- Token de acesso **15 min**; refresh **7 dias**, renovados a cada uso; limite de **5 tentativas de login por minuto por IP** (configurável); senha de no máximo **128 caracteres**.
- Refresh token no **`localStorage`** (decisão do dono: não perder o login). A renovação é serializada entre abas (`navigator.locks`), a aba que perde a corrida relê o armazenamento e tenta uma vez com o token novo, e o logout em uma aba vale para todas. O custo é que um XSS lê o refresh token; mitigado por CSP, acesso curto e rotação.
- Usuário inativo com a **senha certa** recebe 403 "Seu acesso está desativado. Fale com o RH." (decisão do dono); com a senha errada, a mesma resposta genérica.
- Refresh já usado (ou duas renovações simultâneas): a segunda recebe 401, **sem revogar os outros tokens** do usuário. Consequência aceita: um token de acesso vale por até 15 minutos depois de o usuário ser inativado.
- **Esquema:** as 25 tabelas (`V001`), os 36 triggers (`V002`) e as 8 funções (`V003`) vêm dos três arquivos do grupo (versão de 2026-10-28); a fatia 0 **mapeia só** `NIVEIS_DE_ACESSO`, `FUNCIONARIOS` (colunas `ID_FUNCIONARIO`, `NOME`, `DATA_ADMISSAO`, `ATIVO`, `EMAIL_CORPORATIVO`, `ID_NIVEL_ACESSO`), `USUARIOS` e a nova `REFRESH_TOKENS` (`V004`). O login é pelo `EMAIL_CORPORATIVO` do funcionário, e `USUARIOS` não tem nome nem e-mail. `V005` semeia (com `INSERT IGNORE`) os níveis `RH`, `Funcionario` e `Funcionario Gestor` (**nomes confirmados pelo dono em 2026-10-28**), mapeados pelo nome para `Perfil.RH`, `Perfil.Funcionario` e `Perfil.Gestor`. Funcionário **sem nível ou com nível desconhecido não entra**: 403 "Seu perfil de acesso não está configurado. Fale com o RH." e um log de aviso (o banco aceita `ID_NIVEL_ACESSO` e `EMAIL_CORPORATIVO` nulos).
- **Conflito nos scripts do grupo (2026-10-28), em correção pelo Kauan:** `TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE` usam `HORA_ENTRADA` e `HORA_SAIDA`, colunas removidas de `PONTOS` (as batidas foram para `PONTOS_MARCACOES`), e copiam a carga do cargo sem dividir (o cargo passa a ser semanal). A correção esperada remove o bloco de `HORA_*` (validação de saída e entrada e cálculo de `HORAS_TRABALHADAS`) e divide a carga por 5 ao copiar. **Eu fiz uma correção local e a desfiz, a pedido do dono, para não conflitar com a do Kauan** (o patch ficou guardado). `PONTOS.HORAS_TRABALHADAS` deixa de ser preenchida por trigger (as funções do grupo calculam pelas marcações). **Até a correção dele chegar, a `V002` provavelmente não aplica**; o teste da Task 4 é a prova.
- **Erros de trigger na API:** viram 422 com `detail` genérico em português com acento ("A operação foi recusada por uma regra do banco de dados.") e a mensagem original do trigger (sem acento, técnica) numa extensão `regraDoBanco`; a tradução para textos amigáveis fica para as fatias que expõem cada regra.
- O admin do seed é um `Funcionario` (nome e e-mail da configuração, admissão = hoje, nível `RH`) mais o `Usuario`.
- **Executor de scripts próprio** (pequeno): a busca não confirmou como DbUp ou Flyway tratam `DELIMITER` em triggers, e a regra de hash de integridade é parte do requisito. O teste contra o arquivo real de triggers do grupo é quem prova.
- Os três arquivos do grupo viram `V001`, `V002` e `V003` **movendo-os para `Banco de Dados/Scripts/`** (com os ajustes descritos na Task 4). **Autorizado pelo dono em 2026-10-28**, que também avisa o grupo. Como o grupo envia arquivos pela interface web, a CI recusa `.sql` solto em `Banco de Dados/` fora de `Scripts/` e `Validacao/` (Task 12). **Os números de versão são do dono:** o grupo envia o conteúdo e o dono numera, para não haver duas `V003`.
- Sem biblioteca de rotas nem de UI nesta fatia (o spec adia a escolha para depois do Figma). Imagem local `mysql:8.4`; Mailpit e Azurite entram no `docker compose` só nas fatias que os usam.
- App Service em **Linux**; Static Web Apps com origem de deploy "Other"; a API conecta ao MySQL com **usuário só do banco**.
- **Fuso horário:** instantes (tokens, auditoria) em UTC; **datas de negócio e o `time_zone` do servidor MySQL em `-03:00`** (os triggers do grupo usam `NOW()` e `CURDATE()` no fuso do servidor; o Brasil não tem horário de verão). Sem isso, o admin criado às 22h receberia "admissão amanhã". O padrão do Flexible Server não foi verificado.
- **Documentação da API (OpenAPI/Swagger):** gerada pela própria API, sem nada para subir no Azure; ligada na demo (`Documentacao__Habilitada=true`).

## Review Focus

1. **E-mail com maiúsculas ou espaços** autentica o mesmo usuário (Task 6).
2. **Script já aplicado que foi editado** e **dois executores ao mesmo tempo**: o executor recusa e aplica cada script uma vez só (Tasks 4 e 9).
3. **Refresh reaproveitado e renovações simultâneas** (duas abas no `localStorage`, `StrictMode`): nunca 500, no máximo um 200, o token novo do vencedor continua válido, e a aba que perde tenta de novo em vez de deslogar (Tasks 7, 8 e 10).
4. **E-mail inexistente, senha errada e inativo com senha errada** respondem com o mesmo status e o mesmo `title`/`detail`; só o inativo com senha certa recebe o 403 informativo (Tasks 6 e 8).
5. **Token malformado, expirado ou ausente, erro inesperado em `Production`, erro de trigger do banco e banco fora do ar** devolvem `ProblemDetails` sem stack trace; `/health` dá 503 (Tasks 8 e 9).

---

### Task 1: Pré-requisitos da máquina e conferências de documentação

**Files:** nenhum.

Feita pelo dono, com o assistente conferindo. Sem Docker não dá para rodar os testes que usam o banco (Tasks 4 a 9) nem a verificação manual da Task 11; as demais rodam sem ele.

- [ ] **Step 1: Instalar o Docker Desktop** (pode ser na máquina de casa) e abrir até o daemon subir.
- [ ] **Step 2: Atualizar o Node para a LTS ativa** (22 ou mais nova; hoje a máquina tem v18.16.1).
- [ ] **Step 3: Verificar**

Run: `docker version --format '{{.Server.Version}}'; node -v; dotnet --list-sdks; $PSVersionTable.PSVersion; Get-NetTCPConnection -LocalPort 3306 -ErrorAction SilentlyContinue`
Expected: versão do Docker; Node 22+; SDK 10.0.x; versão do PowerShell; **nenhuma linha** na última consulta (porta 3306 livre; se houver MySQL local, a Task 3 usa outra porta).

- [ ] **Step 4 (opcional): instalar Azure CLI e GitHub CLI.** O portal e a aba Actions também servem. Se usadas, encerrar as sessões ao fim (`az logout`, `gh auth logout`): a máquina pode ser compartilhada.
- [ ] **Step 5: Conferir na documentação (Microsoft Learn e do MySQL), sem precisar de conta, e registrar no spec, seção 12:** (a) o App Service Linux oferece o runtime .NET 10; (b) o MySQL Flexible Server oferece a versão 8.4; (c) em qual nível de plano o **Always On** existe; (d) em quais regiões o Static Web Apps existe; (e) **criar triggers e funções no Flexible Server exige algum parâmetro do servidor ou privilégio** (`log_bin_trust_function_creators`, `TRIGGER`, `CREATE ROUTINE`, `SUPER`)? Vale também fora do Azure: o MySQL 8 liga o log binário por padrão. **Se (a) ou (b) falhar, avisar o dono antes da Task 2.**

---

### Task 2: Solution em camadas e testes de arquitetura

**Files:**
- Create: `global.json`, `.gitattributes`, `.gitignore`, `.editorconfig` (todos na raiz), `back-end/Directory.Build.props`, `back-end/Directory.Packages.props`
- Create: `back-end/src/RHSystem.Domain/`, `RHSystem.Application/`, `RHSystem.Infrastructure/`, `RHSystem.Api/` e `back-end/tests/RHSystem.Domain.Tests/`, `RHSystem.Application.Tests/`, `RHSystem.Api.IntegrationTests/`
- Test: `back-end/tests/RHSystem.Domain.Tests/ArquiteturaTests.cs`

**Interfaces:**
- Produces: a solution `back-end/RHSystem` com 7 projetos; versionamento central de pacotes (`Directory.Packages.props`, com `Microsoft.EntityFrameworkCore.*` todos em 9.0.x e Pomelo 9.0.0); `global.json` na **raiz** fixando o SDK 10 (`rollForward: latestFeature`).

- [ ] **Step 1: Criar a solution, o projeto `RHSystem.Domain.Tests` (xUnit) e os arquivos de configuração.** `.gitattributes` com `* text=auto eol=lf`; `.editorconfig` com `end_of_line = lf`; `.gitignore` cobrindo `bin/`, `obj/`, `node_modules/`, `.env`, `*.user`. `Directory.Build.props`: `net10.0`, `Nullable` e `ImplicitUsings` habilitados, `TreatWarningsAsErrors` verdadeiro, **`WarningsNotAsErrors` com `NU1901;NU1902;NU1903;NU1904`** e `NU1608` em `WarningsAsErrors`. Os projetos gerados por `dotnet new` trazem `Version=` nos `PackageReference`: remova (o versionamento é central; senão dá `NU1008`).
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

Run: `dotnet build back-end; dotnet test back-end --filter ArquiteturaTests`
Expected: build sem warnings; 5 testes passando.

- [ ] **Step 6: Commit** (com autorização), incluindo o spec e o plano: `git add back-end global.json .gitattributes .gitignore .editorconfig docs; git commit -m "feat: solution em camadas e testes de arquitetura"`

---

### Task 3: `docker compose` local com MySQL e README

**Files:** Create: `docker-compose.yml`, `.env.example`, `README.md` (raiz).

**Interfaces:**
- Produces: MySQL 8.4 em `localhost:${MYSQL_PORTA:-3306}`, banco **vazio** `rh_system` (`MYSQL_DATABASE`), `utf8mb4` e `utf8mb4_unicode_ci`; credenciais de `.env` (copiado de `.env.example`, só com valores de desenvolvimento local). Quem cria as tabelas é o executor de scripts da Task 4, não o compose.

- [ ] **Step 1: Escrever `docker-compose.yml`:** serviço `mysql` (`mysql:8.4`, `--character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci --default-time-zone=-03:00`, volume nomeado, porta `${MYSQL_PORTA:-3306}:3306`) e `healthcheck` com `mysqladmin ping -h 127.0.0.1 -u root -p"$$MYSQL_ROOT_PASSWORD"` (o `-h 127.0.0.1` evita o servidor temporário de inicialização, que só atende por socket).
- [ ] **Step 2: Subir e verificar**

Run: `docker compose up -d --wait; docker compose exec mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "SELECT @@character_set_server, @@collation_server, @@version"'`
Expected: serviço `healthy`; `utf8mb4`, `utf8mb4_unicode_ci` e `8.4.x`.

- [ ] **Step 3: Escrever o `README.md`** com o que é o projeto, os pré-requisitos (Task 1), as regras dos scripts SQL (resumo da seção 4 do spec: nunca editar script aplicado, congelamento no primeiro deploy, recriar o banco local com `docker compose down -v`) e seções "Back-end" e "Front-end", preenchidas nas Tasks 9 e 11.
- [ ] **Step 4: Commit** (com autorização): `git add docker-compose.yml .env.example README.md; git commit -m "feat: docker compose local com MySQL"`

---

### Task 4: Scripts SQL versionados e executor

**Contexto:** os triggers de `PONTOS` do grupo estão sendo corrigidos pelo Kauan (ver "Decisões deste plano"); até a correção chegar, o Step 9 falha por causa deles. Depois de baixar a versão dele, rode o teste; se ainda falhar, siga o ramo de decisão do passo. Esta task **não bloqueia** as Tasks 2, 3, 6, 10 e 11 (que não usam o banco), e o resultado vai ao dono e ao grupo.

**Files:**
- Move/Create (autorizado pelo dono em 2026-10-28): `Banco de Dados/Scripts/V001__esquema_inicial.sql`, `V002__triggers.sql`, `V003__funcoes.sql`, `V004__refresh_tokens.sql`, `V005__niveis_de_acesso_iniciais.sql`; `Banco de Dados/Validacao/validacao_consistencia.sql`
- Create: `back-end/src/RHSystem.Infrastructure/Esquema/ScriptSql.cs`, `IFonteDeScripts.cs`, `ScriptsEmbutidos.cs`, `DivisorDeScript.cs`, `ExecutorDeScripts.cs`, `Excecoes.cs`
- Modify: `back-end/src/RHSystem.Infrastructure/RHSystem.Infrastructure.csproj` (`EmbeddedResource` de `..\..\..\Banco de Dados\Scripts\*.sql`, com `LogicalName="Scripts/%(Filename)%(Extension)"`; pacote `MySqlConnector`)
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Infra/MySqlFixture.cs`, `Esquema/DivisorDeScriptTests.cs`, `ExecutorDeScriptsTests.cs`, `ScriptsDoGrupoTests.cs`

**Interfaces:**
- Produces (`RHSystem.Infrastructure.Esquema`):
  - `record ScriptSql(int Versao, string Nome, string Conteudo)`
  - `interface IFonteDeScripts { IReadOnlyList<ScriptSql> Carregar(); }` e `class ScriptsEmbutidos : IFonteDeScripts` — lê os recursos `Scripts/V{nnn}__{nome}.sql`, ordena por versão e lança se houver versão repetida ou nome fora do padrão. A interface existe para os testes trocarem a fonte.
  - `static class DivisorDeScript { static IReadOnlyList<string> Dividir(string conteudo); }` — divide pelo delimitador corrente, **ignorando delimitador dentro de comentário** (`-- `, `#`, `/* */`) **e de literal entre aspas** (`'...'`, `"..."`, crase, com aspas dobradas e escape por barra); linhas `DELIMITER x` (em qualquer caixa) trocam o delimitador e **não** viram comando; o último comando sem delimitador final também é emitido; normaliza CRLF para LF; descarta trechos vazios ou só de comentário. Os scripts reais têm `;` dentro de comentários no modo `;`.
  - `class ExecutorDeScripts { ExecutorDeScripts(string connectionString, ILogger<ExecutorDeScripts> log); Task AplicarPendentesAsync(IReadOnlyList<ScriptSql> scripts, CancellationToken ct); }`. Cria `SCHEMA_VERSOES (VERSAO INT PRIMARY KEY, NOME VARCHAR(200) NOT NULL, HASH_SHA256 CHAR(64) NOT NULL, APLICADO_EM DATETIME(6) NOT NULL)`; usa **uma conexão só para a trava** e **uma conexão nova por script** (estado de sessão, como `SET FOREIGN_KEY_CHECKS`, não vaza entre scripts); adquire `GET_LOCK('rhsystem_esquema:{nome do banco}', 60)` na conexão da trava, trata o resultado `1`, `0` e `NULL` (só `1` segue) e a libera no fim; confere o hash dos já aplicados; **recusa** uma versão pendente menor que a maior já aplicada, uma versão aplicada cujo arquivo sumiu, e um banco que já tem tabelas mas está sem histórico em `SCHEMA_VERSOES`; aplica os pendentes em ordem, um comando por vez, e **só depois do script inteiro** grava a linha em `SCHEMA_VERSOES`. O hash é SHA-256 do conteúdo sem BOM e com fins de linha normalizados.
  - `class ScriptJaAplicadoFoiAlteradoException(int versao)` com mensagem "O script V001 já foi aplicado e foi alterado. Não edite scripts aplicados: crie um script novo." e `class FalhaNoScriptException(int versao, int indiceDoComando, string detalhe)` (avisa que o banco pode ter ficado parcialmente alterado), mais `VersaoForaDeOrdemException`, `ScriptAplicadoSumiuException` e `BancoSemHistoricoDeScriptsException`, todas com mensagem clara em português.
  - `MySqlFixture` (testes): `IAsyncLifetime`; `new MySqlBuilder("mysql:8.4")` com a imagem no construtor (o construtor sem imagem é obsoleto); administração com o usuário que pode `CREATE DATABASE` (ver a documentação do módulo); `Task<string> NovoBancoAsync()` cria um banco vazio e exclusivo com `CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci` **e um usuário restrito a esse banco**, com os mesmos privilégios da Task 14 (`SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES, TRIGGER, CREATE ROUTINE, ALTER ROUTINE, EXECUTE`), e devolve a connection string **desse usuário** (assim os testes não escondem um problema de privilégio que apareceria no Azure); `Task<string> NovoBancoComScriptsAsync()` (idem, com todos os scripts embutidos aplicados). **Cada classe de teste pede o seu próprio banco, e os testes não apagam funcionários** (o trigger do grupo bloqueia `DELETE` em `FUNCIONARIOS`): usam e-mails únicos.
- Produces (scripts):
  - `V001`: o script de tabelas do grupo **sem** `CREATE DATABASE`, `USE`, o bloco "LIMPEZA" (os `DROP TABLE` e os dois `SET FOREIGN_KEY_CHECKS`) e os selects de validação do fim; mantém tabelas, chaves, índices e o `INSERT` dos feriados de 2026.
  - `V002`: o script de triggers do grupo **sem** a linha `USE` (os `DROP TRIGGER IF EXISTS` ficam).
  - `V003`: o arquivo de funções do grupo (`RHSystem_Functions_MySQL_Unificadas.sql`, 8 funções) **sem** a linha `USE`.
  - `V004`: `REFRESH_TOKENS (ID_REFRESH_TOKEN INT AUTO_INCREMENT PRIMARY KEY, ID_USUARIO INT NOT NULL, TOKEN_HASH CHAR(64) NOT NULL, CRIADO_EM DATETIME(6) NOT NULL, EXPIRA_EM DATETIME(6) NOT NULL, REVOGADO_EM DATETIME(6) NULL)`, `FK_REFRESH_TOKENS_USUARIOS` para `USUARIOS(ID_USUARIO)` e `UQ_REFRESH_TOKENS_HASH` em `TOKEN_HASH`; `utf8mb4_unicode_ci`.
  - `V005`: `INSERT IGNORE` em `NIVEIS_DE_ACESSO` dos três níveis `RH`, `Funcionario` e `Funcionario Gestor`, com descrição (o `IGNORE` evita derrubar a versão se o grupo semear os mesmos níveis antes).
  - `validacao_consistencia.sql`: os selects de validação do grupo, mais um que lista funcionários cujo `ID_GESTOR` diverge do gestor do setor.

- [ ] **Step 1: Mover os arquivos do grupo** (autorizado pelo dono em 2026-10-28): `git mv` de `RHSystem_Criacao_Tabelas_MySQL.sql` para `Banco de Dados/Scripts/V001__esquema_inicial.sql`, do de triggers para `V002__triggers.sql` e do de funções para `V003__funcoes.sql`; aplicar os ajustes acima; criar `V004`, `V005` e o arquivo de validação. Antes de mover, `git pull` e conferir de novo o que o grupo mudou, porque os arquivos ainda estão em evolução.
- [ ] **Step 2: Escrever os testes do divisor que falham**

```csharp
Divide_por_ponto_e_virgula_e_ignora_comentarios_e_vazios
Respeita_DELIMITER_e_nao_emite_a_linha_DELIMITER
Trigger_com_ponto_e_virgula_no_corpo_vira_um_unico_comando
Normaliza_CRLF_para_LF
Texto_so_com_comentarios_nao_gera_comando
Ponto_e_virgula_dentro_de_comentario_no_modo_ponto_e_virgula_nao_divide      // os scripts reais têm 4 casos
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
Todos_os_scripts_reais_aplicam_com_o_usuario_restrito_ao_banco_e_as_tabelas_usadas_pela_fatia_0_existem   // NIVEIS_DE_ACESSO, FUNCIONARIOS, USUARIOS, REFRESH_TOKENS, SCHEMA_VERSOES
Quantidade_de_triggers_no_banco_e_igual_ao_numero_de_CREATE_TRIGGER_do_V002
Quantidade_de_funcoes_no_banco_e_igual_ao_numero_de_CREATE_FUNCTION_do_V003  // conta no texto do arquivo, para não quebrar quando o grupo mudar a base
Niveis_de_acesso_semeados_sao_RH_Funcionario_e_Funcionario_Gestor_e_aplicar_de_novo_nao_duplica
Trigger_do_grupo_dispara: usuario_para_funcionario_inativo_e_recusado        // mensagem "Nao e possivel criar usuario para funcionario inativo."
```

- [ ] **Step 9: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Esquema|FullyQualifiedName~ScriptsDoGrupo"`
Expected: PASS. **Se o servidor recusar um script real do grupo, PARE: não altere o arquivo do grupo nem dobre o divisor para passar.** Diagnostique: (a) bug do divisor (por exemplo `;` em comentário ou literal): corrigir o divisor e acrescentar o caso aos testes; (b) incompatibilidade entre os scripts do grupo (por exemplo `TRG_PONTOS_BEFORE_INSERT/UPDATE` usando `HORA_ENTRADA` e `HORA_SAIDA`, removidas de `PONTOS`): levar ao dono e ao grupo, que decidem quem corrige; (c) erro 1419 (privilégio `SUPER` com log binário): registrar no README e na Task 14 que o servidor precisa de `log_bin_trust_function_creators`, e definir o mesmo parâmetro no contêiner de teste e no compose, espelhando o Azure.

- [ ] **Step 10: Commit** (com autorização): `git add "Banco de Dados/Scripts" "Banco de Dados/Validacao" back-end; git commit -m "feat: scripts sql versionados e executor"` (caminhos explícitos: não levar junto outros arquivos do grupo, como o `.drawio` e os `.png`)

---

### Task 5: Domínio de autenticação, mapeamento do EF e teste de fumaça do provider

**É a tarefa que confirma o maior risco do spec. Incompatibilidade de provider significa: `NU1608`, `NU1107`, `MissingMethodException` ou `TypeLoadException` ao usar `UseMySql` ou ao consultar. Se aparecer, PARE e avise o dono. Qualquer outro erro (por exemplo, coluna não mapeada) é bug nosso: corrija.**

**Files:**
- Create: `back-end/src/RHSystem.Domain/Autenticacao/Perfil.cs`, `NivelDeAcesso.cs`, `Funcionario.cs`, `Usuario.cs`, `RefreshToken.cs`
- Create: `back-end/src/RHSystem.Infrastructure/Persistencia/AppDbContext.cs`, `Configuracoes/NivelDeAcessoConfiguracao.cs`, `FuncionarioConfiguracao.cs`, `UsuarioConfiguracao.cs`, `RefreshTokenConfiguracao.cs`
- Test: `back-end/tests/RHSystem.Domain.Tests/Autenticacao/FuncionarioTests.cs`, `UsuarioTests.cs`, `PerfilTests.cs`, `RefreshTokenTests.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoContraBancoTests.cs`, `ProviderMySqlSmokeTests.cs`

**Interfaces:**
- Consumes: `MySqlFixture.NovoBancoComScriptsAsync()` e os scripts (Task 4).
- Produces (Domain, `RHSystem.Domain.Autenticacao`):
  - `enum Perfil { Funcionario = 1, Gestor = 2, RH = 3 }` e `static Perfil? PerfilPorNome(string? nomeDoNivel)`: `"RH"` → `Perfil.RH`, `"Funcionario"` → `Perfil.Funcionario`, `"Funcionario Gestor"` → `Perfil.Gestor`; `null` ou qualquer outro nome devolve `null`.
  - `class NivelDeAcesso { int Id; string Nome; }`
  - `class Funcionario { int Id; string Nome; string? EmailCorporativo; DateOnly DataAdmissao; bool Ativo; NivelDeAcesso? NivelDeAcesso; Perfil? Perfil { get; }; static Funcionario Criar(string nome, string emailCorporativo, DateOnly dataAdmissao, NivelDeAcesso nivel); void Inativar(); }` (`Criar` aplica `Trim()` e `ToLowerInvariant()` ao e-mail).
  - `class Usuario { int Id; int FuncionarioId; Funcionario Funcionario; string SenhaHash; bool Ativo; DateTime? UltimoLoginEm; bool PodeEntrar { get; }; static Usuario Criar(Funcionario f, string senhaHash); void RegistrarLogin(DateTime agoraUtc); void Inativar(); }` (`PodeEntrar` = `Ativo && Funcionario.Ativo`). Os triggers do grupo recusam criar usuário para funcionário inativo e podem reagir à inativação de um funcionário; os testes criam ativos e inativam depois.
  - `class RefreshToken { int Id; int UsuarioId; string TokenHash; DateTime CriadoEm; DateTime ExpiraEm; DateTime? RevogadoEm; bool EstaAtivo(DateTime agoraUtc); static RefreshToken Criar(int usuarioId, string tokenHash, DateTime agoraUtc, TimeSpan validade); }`
- Produces (Infrastructure): `AppDbContext : DbContext` com `DbSet<NivelDeAcesso> NiveisDeAcesso`, `DbSet<Funcionario> Funcionarios`, `DbSet<Usuario> Usuarios`, `DbSet<RefreshToken> RefreshTokens`. **Mapeamento explícito** (`ToTable` e `HasColumnName` por propriedade) para as tabelas dos scripts: `NIVEIS_DE_ACESSO` (`ID_NIVEL_ACESSO`, `NOME_NIVEL_ACESSO`), `FUNCIONARIOS` (`ID_FUNCIONARIO`, `NOME`, `DATA_ADMISSAO`, `ATIVO`, `EMAIL_CORPORATIVO`, `ID_NIVEL_ACESSO`), `USUARIOS` (`ID_USUARIO`, `ID_FUNCIONARIO`, `SENHA`, `ATIVO`, `DATA_HORA_ULTIMO_LOGIN`), `REFRESH_TOKENS` (`ID_REFRESH_TOKEN`, `ID_USUARIO`, `TOKEN_HASH`, `CRIADO_EM`, `EXPIRA_EM`, `REVOGADO_EM`). Sem `EnsureCreated` nem migrations. `UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))`.

- [ ] **Step 1: Escrever os testes de unidade do Domain que falham**

```csharp
Funcionario_Criar_normaliza_o_email()                       // "  Ana@Empresa.com " -> "ana@empresa.com"
Usuario_PodeEntrar_e_false_se_o_usuario_ou_o_funcionario_esta_inativo()
PerfilPorNome_mapeia_RH_Funcionario_e_Funcionario_Gestor_e_devolve_null_para_nulo_ou_outro_nome()
RefreshToken_expirado_ou_revogado_nao_esta_ativo()
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
Nulabilidade_das_propriedades_mapeadas_coincide_com_a_das_colunas()     // ID_NIVEL_ACESSO e EMAIL_CORPORATIVO aceitam NULL no banco
Toda_coluna_obrigatoria_sem_valor_padrao_das_tabelas_mapeadas_esta_mapeada()   // pega o desalinhamento quando o grupo muda o script
// ProviderMySqlSmokeTests
Funcionario_e_usuario_fazem_ida_e_volta_com_unicode_e_perfil_pelo_nivel()      // Nome = "José Ação 😀", nível RH; relê com outro AppDbContext
Email_corporativo_duplicado_viola_a_chave_unica()
Erro_de_trigger_chega_como_DbUpdateException_com_SQLSTATE_45000()              // usuário para funcionário inativo
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
  - `IUsuarioRepositorio { Task<Usuario?> ObterPorEmailCorporativoAsync(string emailNormalizado, CancellationToken ct); Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct); }` (devolvem o `Usuario` com `Funcionario` e `NivelDeAcesso` carregados)
  - `IRefreshTokenRepositorio { Task AdicionarAsync(RefreshToken t, CancellationToken ct); Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct); Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct); Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct); }` — `TentarRevogarAsync` é atômico e devolve `true` só para quem de fato revogou.
  - `IUnidadeDeTrabalho { Task SalvarAsync(CancellationToken ct); Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct); }`
  - `ISenhaHasher { string Gerar(string senha); bool Verificar(string hash, string senha); }`
  - `IEmissorDeTokens { AcessoEmitido EmitirAcesso(Usuario u); RefreshGerado NovoRefresh(); string HashDoRefresh(string token); }`, `record AcessoEmitido(string Token, DateTime ExpiraEmUtc)`, `record RefreshGerado(string Token, string Hash)`
  - `IRelogio { DateTime AgoraUtc { get; } }`
  - DTOs: `record EntrarComando(string Email, string Senha)`, `record RenovarComando(string RefreshToken)`, `record UsuarioResposta(int Id, int FuncionarioId, string Nome, string Email, Perfil Perfil)`, `record SessaoResposta(string TokenDeAcesso, DateTime AcessoExpiraEmUtc, string RefreshToken, UsuarioResposta Usuario)`
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
Entrar_registra_UltimoLoginEm_e_guarda_apenas_o_hash_do_refresh
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
  - `UsuarioRepositorio` busca por `EmailCorporativo` com `Include(Funcionario).ThenInclude(NivelDeAcesso)`.

- [ ] **Step 1: Escrever os testes que falham** (com `NovoBancoComScriptsAsync()` onde houver banco)

```csharp
Hash_verifica_senha_correta_e_rejeita_errada
Hash_gerado_tem_pelo_menos_40_caracteres
Senha_com_acentos_e_emoji_gera_e_verifica
Senha_com_mais_de_128_caracteres_lanca_ArgumentException
Jwt_emitido_contem_sub_fid_email_nome_perfil_e_expira_em_15_minutos
UsuarioRepositorio_acha_pelo_email_corporativo_ignorando_maiusculas_e_traz_o_perfil
UsuarioRepositorio_traz_o_usuario_mesmo_quando_o_funcionario_nao_tem_nivel      // a navegação é opcional: nada de INNER JOIN que esconda o usuário
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
- Create: `back-end/src/RHSystem.Api/Autenticacao/AutenticacaoEndpoints.cs`, `Configuracao/AutenticacaoExtensions.cs`, `Configuracao/TratamentoDeErros.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs`; `contrato/sessao.exemplo.json` (raiz)
- Modify: `back-end/src/RHSystem.Api/Program.cs`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Autenticacao/AutenticacaoEndpointsTests.cs`

**Interfaces:**
- Consumes: casos de uso (Task 6), `AdicionarInfraestrutura` (Task 7), `MySqlFixture.NovoBancoComScriptsAsync()` (Task 4).
- Produces (rotas sob `/api/auth`):
  - `POST /api/auth/entrar` corpo `EntrarComando` → `200 SessaoResposta` | `401` | `403` (inativo com a senha certa, ou sem perfil de acesso configurado) | `400` (campo vazio ou senha acima de 128 caracteres)
  - `POST /api/auth/renovar` corpo `RenovarComando` → `200 SessaoResposta` | `401`
  - `POST /api/auth/sair` corpo `RenovarComando` → `204`
  - `GET /api/auth/eu` (`Authorization: Bearer`) → `200 UsuarioResposta` | `401`
- Erros sempre `ProblemDetails` (`application/problem+json`) em português, **sem stack trace**, inclusive o 401 do `JwtBearer` (que sozinho vem sem corpo: tratar em `OnChallenge`). O 401 de login usa sempre o mesmo `title` e `detail` (`"E-mail ou senha inválidos."`); o 403 usa o `detail` `"Seu acesso está desativado. Fale com o RH."` (inativo) ou `"Seu perfil de acesso não está configurado. Fale com o RH."` (sem perfil). **Erro de trigger do banco** (`SQLSTATE 45000`) vira `422` com `ProblemDetails` de `detail` genérico em português com acento (`"A operação foi recusada por uma regra do banco de dados."`) e a mensagem original do trigger numa extensão `regraDoBanco`.
- Enums saem como **texto** (`JsonStringEnumConverter`). `ClockSkew` do JWT = 30 s.
- `ApiFactory : WebApplicationFactory<Program>`: ambiente `Testing`; **toda a configuração em memória** (inclusive `Jwt:Chave`); cada classe de teste recebe um banco novo com os scripts aplicados; semeia (com e-mails únicos, pois não se apaga funcionário) um RH ativo, um usuário com só `USUARIOS.ATIVO = false`, um cujo funcionário foi inativado por `UPDATE` (o trigger do grupo inativa o usuário junto) e um funcionário sem nível de acesso; o caso "funcionário inativo com usuário ativo" não existe no banco e só o teste do Application, com falsos, o cobre; permite trocar o `IRelogio` por um falso e o ambiente por `Production`; registra uma rota de teste que provoca um erro de trigger.
- `contrato/sessao.exemplo.json`: exemplo de `SessaoResposta` com `tokenDeAcesso`, `acessoExpiraEmUtc` (terminando em `Z`), `refreshToken` e `usuario` (`id`, `funcionarioId`, `nome`, `email`, `perfil`). A API e o front-end são testados contra o mesmo arquivo (Task 10).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Entrar_com_credenciais_validas_devolve_200_com_tokens
Resposta_de_entrar_tem_exatamente_as_chaves_de_contrato_sessao_exemplo_json
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
Eu_com_token_valido_devolve_o_usuario_com_perfil_como_texto                                          // Assert.Equal("RH", ...)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter AutenticacaoEndpointsTests`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar os endpoints** (minimal API), `JwtBearer` lendo `OpcoesJwt`, `OnChallenge` com `ProblemDetails`, tratamento global de exceções (inclusive a tradução do erro de trigger) e os registros em `Program.cs`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end contrato; git commit -m "feat: endpoints de autenticacao"`

---

### Task 9: Limite de tentativas, proxy, CORS, saúde, documentação da API, scripts na inicialização e seed

**Files:**
- Create: `back-end/src/RHSystem.Api/Configuracao/LimiteDeTentativas.cs`, `Cors.cs`, `ProxyReverso.cs`, `InicializacaoDoBanco.cs`, `Saude/BancoHealthCheck.cs`
- Modify: `back-end/src/RHSystem.Api/Program.cs`, `appsettings.json`, `appsettings.Development.json`, `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs` (limite de login 1000 por padrão), `README.md`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Configuracao/LimiteDeTentativasTests.cs`, `SaudeTests.cs`, `InicializacaoTests.cs`, `CorsTests.cs`

**Interfaces:**
- Produces (configuração; as da Task 7 já existem): `Database:AplicarScriptsAoIniciar` (bool), `Autenticacao:LimiteLoginPorMinuto` (padrão 5), `Cors:OrigensPermitidas` (lista), `ProxyReverso:Habilitado` (bool), `Documentacao:Habilitada` (bool), `Seed:AdminNome|AdminEmail|AdminSenha`.
- Limitador: **5 requisições por minuto por IP** em `POST /api/auth/entrar`; a sexta devolve **`429`** com `ProblemDetails` e `Retry-After` (o limitador do ASP.NET rejeita por padrão com 503 e sem corpo: configure `RejectionStatusCode` e `OnRejected`). O IP vem de `RemoteIpAddress`; com `ProxyReverso:Habilitado` o `UseForwardedHeaders` (`X-Forwarded-For` e `-Proto`) o ajusta, aceitando só o proxy da plataforma. Cada tentativa de login registra um log `Information` com o IP.
- `GET /health` → `200 {"status":"ok"}` com o banco acessível, `503` se ele cair **depois da subida**. Com o banco parado na partida e `AplicarScriptsAoIniciar` ligado, a API falha ao iniciar (aceito e documentado no README).
- `Documentacao:Habilitada` publica o documento OpenAPI nativo e uma UI (escolher e conferir compatibilidade com o .NET 10: `Swashbuckle.AspNetCore` ou `Scalar.AspNetCore`). Ligada em `Development` e na demo.
- Inicialização: com `AplicarScriptsAoIniciar=true`, `IFonteDeScripts.Carregar()` (registrada como `ScriptsEmbutidos`; os testes a substituem) + `ExecutorDeScripts.AplicarPendentesAsync`. Se um script aplicado foi alterado, **a API não sobe**, com a mensagem da exceção. Seed: se `Seed:AdminEmail` e `Seed:AdminSenha` estiverem preenchidos **e não existir usuário**, cria `Funcionario` (nome da configuração, e-mail normalizado, admissão = hoje no fuso `-03:00`, nível `RH` lido de `NIVEIS_DE_ACESSO`) e o `Usuario` com hash; nunca registra a senha em log; se o nível `RH` não existir, falha com mensagem clara.
- `appsettings.Development.json` traz só valores locais, sem segredo (`ConnectionStrings:Padrao` para o compose, com `AllowPublicKeyRetrieval=True` **só em desenvolvimento**). A chave do JWT local vem de user-secrets: `dotnet user-secrets init --project back-end/src/RHSystem.Api` e `dotnet user-secrets set "Jwt:Chave" "<32+ bytes>" --project back-end/src/RHSystem.Api`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Sexta_tentativa_de_login_no_mesmo_minuto_devolve_429_ProblemDetails_com_Retry_After   // limite 5 só neste teste
Health_devolve_200_com_banco_e_503_com_connection_string_para_porta_fechada            // Review Focus 5; AplicarScriptsAoIniciar=false; não parar o contêiner compartilhado
Seed_cria_funcionario_e_usuario_admin_uma_unica_vez_e_nao_cria_sem_configuracao        // banco novo com scripts
Inicializacao_aplica_os_scripts_quando_AplicarScriptsAoIniciar_e_true                   // banco novo e vazio (NovoBancoAsync)
Inicializacao_recusa_subir_se_um_script_aplicado_foi_alterado_com_IFonteDeScripts_falsa                           // Review Focus 2
Cors_so_permite_as_origens_configuradas
Com_ProxyReverso_habilitado_o_limite_e_por_IP_do_cabecalho_X_Forwarded_For            // dois IPs distintos não dividem o balde
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Api.IntegrationTests.Configuracao"`
Expected: FAIL.

- [ ] **Step 3: Implementar** limitador, proxy, CORS, health check, documentação, inicialização e seed.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Rodar tudo do back-end**

Run: `dotnet test back-end`
Expected: todos passando.

- [ ] **Step 6: Preencher no `README.md`** os comandos reais: `docker compose up -d --wait`, `dotnet run --project back-end/src/RHSystem.Api`, como recriar o banco local (`docker compose down -v`) e a nota sobre a API não subir sem banco.
- [ ] **Step 7: Commit** (com autorização): `git add back-end README.md; git commit -m "feat: limite de tentativas, proxy, cors, saude, scripts na inicializacao e seed"`

---

### Task 10: Front-end: projeto, cliente de API e contexto de autenticação

**Files:**
- Create: `front-end/` (Vite + React + TypeScript), `front-end/.prettierrc`, `.env.example`, `src/shared/api/clienteApi.ts`, `src/shared/api/tipos.ts`, `src/features/autenticacao/AuthProvider.tsx`, `useAuth.ts`, `armazenamentoDaSessao.ts`
- Test: `src/shared/api/clienteApi.test.ts`, `src/shared/api/contrato.test.ts`, `src/features/autenticacao/AuthProvider.test.tsx`

**Interfaces:**
- Produces:
  - `type Usuario = { id: number; funcionarioId: number; nome: string; email: string; perfil: 'Funcionario' | 'Gestor' | 'RH' }`; `type Sessao = { tokenDeAcesso: string; acessoExpiraEmUtc: string; refreshToken: string; usuario: Usuario }`; `function ehSessao(x: unknown): x is Sessao`
  - `type ClienteApi = { entrar(email: string, senha: string): Promise<Sessao>; renovar(refreshToken: string): Promise<Sessao>; sair(refreshToken: string): Promise<void>; eu(tokenDeAcesso: string): Promise<Usuario> }` e `criarClienteApi(urlBase: string, fetchImpl?: typeof fetch): ClienteApi`. Resposta HTTP de erro vira `class ErroDeApi extends Error { status: number }`.
  - `useAuth(): { usuario: Usuario | null; carregando: boolean; entrar(email: string, senha: string): Promise<void>; sair(): Promise<void> }`. Token de acesso **em memória**; refresh em **`localStorage`** (chave `rh.refresh`), para a sessão sobreviver ao fechar a aba ou o navegador. Ao montar, o `AuthProvider` tenta renovar com o refresh guardado, com **uma única renovação em andamento por vez** (as chamadas concorrentes na mesma aba compartilham a mesma promessa) e **serializada entre abas** com `navigator.locks.request` (quando o navegador não oferece, segue sem o lock). Se a renovação receber 401, o provider **relê o `localStorage`**: se outra aba já gravou um refresh diferente do que foi usado, tenta **uma vez** com o novo; se o refresh não mudou, limpa a sessão. O evento `storage` desloga a aba quando outra aba remove `rh.refresh`. `AuthProvider` recebe o `ClienteApi` por prop.
- URL da API: `VITE_API_URL`.

- [ ] **Step 1: Criar o projeto** com o template React + TypeScript do Vite **sem perguntas interativas** (confira as flags em `npm create vite@latest -- --help`), instalar Vitest, Testing Library (`@testing-library/react`, `@testing-library/user-event`, `jsdom`), ESLint e **Prettier**, e criar os scripts `lint`, `format:check`, `test` e `build`. Conferir que o Node atende o `engines` do Vite.
- [ ] **Step 2: Escrever os testes que falham**

```ts
clienteApi.entrar envia POST /api/auth/entrar e devolve a sessao
clienteApi transforma resposta 401, 403, 400, 429 e 500 em ErroDeApi com o status
ehSessao aceita contrato/sessao.exemplo.json e rejeita um objeto sem usuario    // lê o mesmo arquivo do back-end
AuthProvider renova a sessao ao montar quando ha refresh no localStorage (inclusive depois de "fechar e reabrir")
AuthProvider em StrictMode (montagem dupla) chama renovar uma unica vez          // Review Focus 3
AuthProvider com renovacao que falha ao montar (e o refresh nao mudou) limpa o localStorage e fica sem usuario
AuthProvider com 401 na renovacao, quando outra aba ja gravou um refresh novo, tenta uma vez com o novo e continua logado   // Review Focus 3
AuthProvider.sair limpa o localStorage e o usuario
AuthProvider desloga quando outra aba remove rh.refresh (evento storage)
AuthProvider.entrar com erro nao guarda nada e propaga o erro
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `npm --prefix front-end test -- --run`
Expected: FAIL (módulos não existem).

- [ ] **Step 4: Implementar** `criarClienteApi`, `ehSessao`, `AuthProvider` e `useAuth`.
- [ ] **Step 5: Rodar e ver passar**

Run: `npm --prefix front-end test -- --run; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build`
Expected: tudo passando e sem erros.

- [ ] **Step 6: Commit** (com autorização): `git add front-end; git commit -m "feat: front-end base com cliente de api e contexto de autenticacao"`

---

### Task 11: Front-end: tela de login, shell e cabeçalhos de segurança

**Files:**
- Create: `front-end/src/app/App.tsx`, `ShellDoPortal.tsx`, `rotulosDePerfil.ts`, `front-end/src/features/autenticacao/PaginaEntrar.tsx`, `PortaDeAcesso.tsx`, `front-end/public/staticwebapp.config.json`
- Test: `front-end/src/features/autenticacao/PaginaEntrar.test.tsx`, `PortaDeAcesso.test.tsx`, `front-end/src/app/ShellDoPortal.test.tsx`, `front-end/src/app/staticwebapp.config.test.ts`

**Interfaces:**
- Consumes: `useAuth` e `ClienteApi` (Task 10).
- Produces: `PortaDeAcesso` mostra um indicador enquanto `carregando`, `PaginaEntrar` sem usuário e `ShellDoPortal` com usuário (**sem biblioteca de rotas**). `ShellDoPortal` mostra "Portal de RH" (o nome do menu no Figma), o nome do usuário, o rótulo do perfil (`rotulosDePerfil`: `Funcionario` → "Funcionário", `Gestor` → "Gestor", `RH` → "RH") e o botão "Sair".
- Textos: rótulos "E-mail" e "Senha", botão "Entrar"; 401 `"E-mail ou senha inválidos."`; 403 `"Seu acesso está desativado. Fale com o RH."`; 400 `"Preencha o e-mail e a senha."`; 429 `"Muitas tentativas. Aguarde um minuto e tente de novo."`; 500 `"Erro no servidor. Tente novamente em instantes."`; falha de rede `"Não foi possível conectar. Tente novamente."`.
- `staticwebapp.config.json`: `navigationFallback.rewrite = "/index.html"` e `globalHeaders` com `Content-Security-Policy` (`default-src 'self'`; `connect-src 'self'` mais a URL da API, preenchida na Task 14), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` e `Permissions-Policy`.

- [ ] **Step 1: Escrever os testes que falham**

```tsx
PaginaEntrar mostra a mensagem certa para 401, 403, 400, 429, 500 e falha de rede
PaginaEntrar desabilita o botao enquanto envia
PortaDeAcesso mostra o login sem usuario e o shell com usuario
Shell mostra nome do usuario, "Funcionário" para o perfil Funcionario e o botao Sair
Sair volta para o login e limpa a sessao
staticwebapp.config.json e JSON valido, tem navigationFallback e os cabecalhos de seguranca
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `npm --prefix front-end test -- --run`
Expected: FAIL.

- [ ] **Step 3: Implementar** as páginas, a porta de acesso, o shell e o arquivo de configuração.
- [ ] **Step 4: Rodar e ver passar**

Run: `npm --prefix front-end test -- --run; npm --prefix front-end run lint; npm --prefix front-end run format:check; npm --prefix front-end run build`
Expected: tudo passando.

- [ ] **Step 5: Verificação manual ponta a ponta** (exige Docker): `docker compose up -d --wait`, API com `Database:AplicarScriptsAoIniciar` e `Seed:AdminNome`/`Seed:AdminEmail`/`Seed:AdminSenha` definidos por user-secrets, `dotnet run --project back-end/src/RHSystem.Api` e `npm --prefix front-end run dev`. Entrar com o admin, ver o shell, sair, e confirmar que **F5 e fechar e reabrir o navegador não deslogam**, e que sair em uma aba desloga a outra. Registrar o resultado.
- [ ] **Step 6: Preencher no `README.md`** os comandos do front-end.
- [ ] **Step 7: Commit** (com autorização): `git add front-end README.md; git commit -m "feat: login, shell do portal e cabecalhos de seguranca"`

---

### Task 12: Integração contínua (GitHub Actions)

**Files:** Create: `.github/workflows/ci.yml`.

**Interfaces:**
- Produces: workflow `CI` em `push` e `pull_request`, com dois jobs independentes. `back-end` (`ubuntu-latest`, que traz Docker para o Testcontainers): setup-dotnet com `global-json-file: global.json`, `dotnet restore`, `dotnet build --no-restore`, `dotnet format back-end --verify-no-changes`, `dotnet test --no-build` e dois guardas de scripts: (1) **nenhum `.sql` solto em `Banco de Dados/`** fora de `Scripts/` e `Validacao/` (impede que um envio pela web recrie o arquivo antigo); (2) se existir `Banco de Dados/Scripts/CONGELADOS.sha256` (criado na Task 14), os hashes das versões listadas não mudaram (um PR que edita script congelado passaria nos testes, que criam banco novo, e só quebraria no Azure). `front-end`: setup-node na LTS da Task 1, `npm ci`, `lint`, `format:check`, `test -- --run`, `build`.

- [ ] **Step 1: Escrever o workflow.** Fixar cada ação por versão maior estável conferida na documentação da ação na hora, não de memória.
- [ ] **Step 2: Validar localmente**

Run: `dotnet format back-end --verify-no-changes; npm --prefix front-end ci; npm --prefix front-end run format:check`
Expected: sem diferenças de formatação; instalação limpa.

- [ ] **Step 3: Com autorização do dono, dar push em uma branch** e conferir os dois jobs.

Run: `gh run watch` (ou abrir a aba Actions)
Expected: os dois jobs verdes.

- [ ] **Step 4: Commit** (com autorização): `git add .github; git commit -m "ci: build e testes do back-end e do front-end"`

---

### Task 13: Workflows de deploy manual

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

**Feita junto com o dono, que nunca usou o Azure. Antes de começar, o dono deve ter confirmado qual conta tem os créditos do SENAI. Cada passo é conferido na documentação atual (Microsoft Learn), não de memória. O assistente nunca vê os valores dos segredos: o dono os gera e cola direto no portal e no GitHub.**

**Files:** Create: `docs/azure/passo-a-passo-fatia-0.md` (roteiro com os nomes e valores reais, sem segredos). Modify: `README.md` (link), `front-end/public/staticwebapp.config.json` (URL da API no `connect-src`).

**Interfaces:**
- Produces: grupo de recursos único; App Service **Linux**; Static Web Apps; MySQL Flexible Server; um banco `rh_system` com **usuário próprio da API** (privilégios `SELECT, INSERT, UPDATE, DELETE, CREATE, ALTER, DROP, INDEX, REFERENCES, TRIGGER, CREATE ROUTINE, ALTER ROUTINE, EXECUTE` só nesse banco, criado por um script SQL curto que o roteiro traz; `DROP` no banco também permite apagar o próprio banco, risco aceito e documentado); configurações do App Service `ConnectionStrings__Padrao`, `Jwt__Chave|Emissor|Audiencia`, `Cors__OrigensPermitidas__0`, `Database__AplicarScriptsAoIniciar=true`, `Database__ServerVersion`, `ProxyReverso__Habilitado=true`, `Documentacao__Habilitada`, `Seed__AdminNome|AdminEmail|AdminSenha`; secrets e variável do GitHub da Task 13.

- [ ] **Step 1: Confirmar a conta com os créditos e a região permitida** (que ofereça o MySQL e o Static Web Apps, conforme a Task 1, Step 5). **Parar se não estiver confirmada.**
- [ ] **Step 2: Conferir, na conta, o plano e o orçamento:** o plano do App Service escolhido tem Always On? Criar um **alerta de orçamento** no Cost Management antes de qualquer recurso. **Perguntar ao dono** se aceita um plano pago do App Service (o Always On, de que o agendador das fatias seguintes depende, exige nível pago) e, se o crédito acabar, que a demo cai.
- [ ] **Step 3: Para cada recurso, dizer o SKU exato e o custo estimado e esperar o OK do dono** (os padrões do portal costumam ser caros: General Purpose, alta disponibilidade, Premium; o assistente diz qual valor escolher em cada tela). Ordem: grupo de recursos, **App Service**, MySQL Flexible Server (firewall: **perguntar ao dono** entre liberar só os IPs de saída do App Service, mais seguro e mais trabalhoso, ou "permitir serviços do Azure", mais simples; mais o IP do dono quando preciso), Static Web Apps **com origem de deploy "Other"** e sem o Deployment Center do App Service (os dois criariam workflows que publicam a cada push, contra o spec).
- [ ] **Step 4: Segredos e banco.** O dono gera a chave do JWT e a senha do admin **localmente** (o roteiro traz o comando) e cola direto nas configurações do App Service e nos secrets do GitHub. Ligar **HTTPS Only** e a autenticação básica do SCM (para o publish profile). Criar o banco (`CHARACTER SET utf8mb4 COLLATE utf8mb4_unicode_ci`) e o usuário da API com o script SQL do roteiro, que também traz a **receita de recuperação**: se a `V001` falhar no meio no primeiro deploy, apagar e recriar o banco (aceitável enquanto não há dados); depois do primeiro deploy bem-sucedido, nunca recriar. Os triggers e funções ficam com o usuário da API como `DEFINER`: **não trocar esse usuário sem recriar os triggers e as funções**. **Conferir que o usuário da API consegue criar triggers** (resultado da Task 1, Step 5e); se o servidor exigir um parâmetro (por exemplo `log_bin_trust_function_creators`), ajustá-lo conforme a documentação. Ajustar também o parâmetro `time_zone` do servidor para `-03:00` e conferir com `SELECT @@global.time_zone, NOW(), UTC_TIMESTAMP()`.
- [ ] **Step 5: Atualizar o `connect-src`** do `staticwebapp.config.json` com a URL real da API e conferir que `Cors__OrigensPermitidas__0` é a URL real do front.
- [ ] **Step 6: Disparar `deploy-api` e `deploy-front`** pelo GitHub Actions (os workflows já estão em `main`).

Expected: os dois verdes, e a API sobe aplicando `V001` a `V005` (ver o log do App Service).

- [ ] **Step 7: Verificar no ambiente real**

Run: `curl.exe -s -o NUL -w "%{http_code}" https://<url-da-api>/health` e abrir a URL do front.
Expected: `200`; no navegador, o dono entra com o admin, vê o shell, sai, e **F5 e fechar e reabrir o navegador não deslogam**. Depois conferir no log do App Service o IP registrado em uma tentativa de login: **não pode ser o IP do proxy** (se for, o limite de login virou global; corrigir `ProxyReverso`).

- [ ] **Step 8: Remover a configuração `Seed__AdminSenha`** do App Service (o admin já existe) e registrar no roteiro. A troca de senha chega na fatia 1.
- [ ] **Step 9: Congelar a base de scripts.** Criar `Banco de Dados/Scripts/CONGELADOS.sha256` com a versão e o hash de cada script aplicado (o guarda da CI, Task 12, passa a vigiá-lo). Registrar no README e no roteiro que **nenhum script aplicado pode mais ser editado** (a API deixa de subir se for), e **avisar o grupo**: toda mudança daqui em diante é um script novo, numerado pelo dono (`ALTER` para tabelas; `DROP` e `CREATE` para trigger e função).
- [ ] **Step 10: Deixar o ambiente seguro para o custo.** Mostrar ao dono como parar ou apagar o que gasta crédito, registrando que **parar o app não zera o custo do plano do App Service** (só apagar o plano ou rebaixá-lo) e que o MySQL parado pode religar sozinho depois de um tempo (conferir o prazo na documentação). Encerrar as sessões de `az` e `gh`.
- [ ] **Step 11: Commit** (com autorização): `git add docs/azure README.md front-end; git commit -m "docs: roteiro de deploy no Azure da fatia 0"`

---

## Self-review (cobertura do spec, linha 0 da seção 8)

| Item da fatia 0 | Task |
|---|---|
| Solution em camadas | 2 |
| `docker compose` | 3 (só MySQL; Mailpit e Azurite entram nas fatias que os usam) |
| Scripts SQL versionados com o executor (hash, trava, congelamento) | 4 e 14 |
| Teste de fumaça do provider | 5 |
| Login: JWT, refresh com rotação, inativo bloqueado, limite de tentativas | 5 a 9 |
| Endpoint de saúde, scripts na inicialização, segredos | 9 e 14 |
| Cabeçalhos de segurança no front (spec, seção 5) | 11 e 14 |
| Shell do React | 10 e 11 |
| CI com verificação de formatação nos dois lados | 12 |
| Deploy no Azure (manual; custo dito antes; conta confirmada; Always On, runtime e triggers conferidos; base congela) | 1, 13 e 14 |
