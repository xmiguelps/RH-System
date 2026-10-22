# Fatia 0 (esqueleto) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar o esqueleto do RH-System, com API em camadas, banco MySQL, login com JWT, shell do React, CI e deploy no Azure, tudo rodando de ponta a ponta.

**Architecture:** Monólito modular em Clean Architecture (`back-end/`) e React + TypeScript (`front-end/`), no mesmo repositório. O MySQL é code-first com migrations do EF. A autenticação usa JWT de vida curta e refresh token com rotação, guardado só como hash no banco.

**Tech Stack:** .NET 10, EF Core 9.x, `Pomelo.EntityFrameworkCore.MySql` 9.0.0, MySQL 8.4, xUnit, Testcontainers, React + TypeScript + Vite, Vitest + Testing Library + Prettier, GitHub Actions, Azure (App Service Linux, Static Web Apps, MySQL Flexible Server).

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seções 2, 3, 5, 7 e 8; a fatia 0 é a linha 0 da tabela da seção 8; a seção 4 traz o modelo de dados e perfis).

## Global Constraints

- .NET 10 com **EF Core 9.x** (nenhum `Microsoft.EntityFrameworkCore.*` em 10.x) e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**.
- Dependências: Api e Infrastructure dependem de Application; Application depende de Domain; **Domain não depende de nada**.
- Casos de uso são **classes simples**, sem biblioteca de mediação. Entidades do Domain mapeadas direto pelo EF (Fluent API na Infrastructure).
- Banco **code-first**; migrations na **inicialização da API**, controladas por configuração.
- Login por e-mail e senha, senha só como hash. JWT de vida curta; refresh token no banco, **revogável, com rotação e só como hash**. Tokens por **cabeçalho**, não cookie. Há limite de tentativas de login. Usuário inativo não entra e perde o refresh token. O front tem **cabeçalhos de segurança** (spec, seção 5).
- Segredos **nunca** vão para o repositório: user-secrets, `.env` ignorado, configurações do App Service e secrets do GitHub. **O assistente nunca vê os valores dos segredos de produção:** o dono os gera e cola direto no portal.
- Testes de integração usam **MySQL real em contêiner** (Docker). Verificação de formatação no back-end e no front-end.
- Pastas `back-end/` e `front-end/`. Deploy no Azure **manual ou por tag**, nunca a cada push.
- O ambiente Azure só é criado **depois de o dono confirmar qual conta tem os créditos**. O custo de cada recurso é dito antes de criá-lo, e o dono nunca usou Azure: cada passo é guiado.
- **Commit e push só com autorização do dono** (perguntar uma vez, no início da execução, se pode commitar por tarefa).
- Nomes de domínio e API em português sem acento (`Usuario`, `Entrar`); mensagens ao usuário em português do Brasil com acento.
- Comandos deste plano rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; no PowerShell o `curl` é alias, use `curl.exe`).

## Decisões deste plano (o spec não fixa)

Escolhidas aqui; o dono pode mudar na revisão:
- Token de acesso **15 min**; refresh **7 dias**; limite de **5 tentativas de login por minuto por IP** (configurável); senha de no máximo **128 caracteres**.
- Refresh token no **`sessionStorage`** (ao fechar a aba, ou abrir uma aba nova, o usuário entra de novo; só duplicar a aba copia a sessão).
- Usuário inativo que digita a senha certa vê a mesma mensagem genérica de credencial inválida.
- Refresh já usado (ou duas renovações simultâneas): a segunda recebe 401, **sem revogar os outros tokens** do usuário. O spec não exige detecção de reuso, e revogar em massa derrubava a sessão por corrida. Consequência aceita e documentada: um token de acesso continua válido por até 15 minutos depois de o usuário ser inativado.
- Nomes do banco como no script atual quando existem: banco `rh_system`, tabela `USUARIOS`, `DATA_HORA_ULTIMO_LOGIN`; onde o código diverge do script (`SENHA_HASH` em vez de `SENHA`), o DER regenerado na fatia 8 é quem vale.
- **Sem biblioteca de rotas** nesta fatia (o spec adia rotas, UI e formulários para depois de ver o Figma): o front mostra a tela de login ou o shell conforme a sessão. Sem biblioteca de UI.
- Imagem local `mysql:8.4`; Mailpit (e-mail) e Azurite (Blob) entram no `docker compose` só nas fatias que os usam.
- `Usuario` não tem vínculo com `Funcionario` nesta fatia. **A fatia 1 deve criar `Funcionario` e ligar o admin semeado a um** (o script atual exige `ID_FUNCIONARIO NOT NULL UNIQUE` em `USUARIOS`).
- App Service em **Linux**; Static Web Apps com origem de deploy "Other" (sem workflow criado pelo portal); a API conecta ao MySQL com **usuário só do banco**, não com o administrador do servidor.

## Review Focus

1. **E-mail com maiúsculas ou espaços** autentica o mesmo usuário (Task 5).
2. **Senha acima de 128 caracteres** dá 400; **senha com acentos e emoji** funciona (Tasks 6 e 7).
3. **Refresh reaproveitado e renovações simultâneas** (duas abas, `StrictMode`): nunca 500, no máximo um 200, e o token novo do vencedor continua válido (Tasks 6, 7 e 9).
4. **E-mail inexistente, senha errada e usuário inativo** respondem com o mesmo status e o mesmo `title`/`detail` (Task 7).
5. **Token malformado, expirado ou ausente e erro inesperado em `Production`** devolvem `ProblemDetails` sem stack trace; `/health` dá 503 com o banco fora do ar (Tasks 7 e 8).

---

### Task 1: Pré-requisitos da máquina e conferências de documentação

**Files:** nenhum.

Feita pelo dono, com o assistente conferindo. Sem Docker não dá para rodar os testes que usam o banco (Tasks 4, 6, 7 e 8) nem a verificação manual da Task 10; as demais rodam sem ele.

- [ ] **Step 1: Instalar o Docker Desktop** (pode ser na máquina de casa, se aqui não for permitido) e abrir até o daemon subir.
- [ ] **Step 2: Atualizar o Node para a LTS ativa** (22 ou mais nova; hoje a máquina tem v18.16.1).
- [ ] **Step 3: Verificar**

Run: `docker version --format '{{.Server.Version}}'; node -v; dotnet --list-sdks; $PSVersionTable.PSVersion; Get-NetTCPConnection -LocalPort 3306 -ErrorAction SilentlyContinue`
Expected: versão do Docker; Node 22+; SDK 10.0.x; versão do PowerShell; **nenhuma linha** na última consulta (porta 3306 livre; se houver MySQL local, a Task 3 usa outra porta).

- [ ] **Step 4 (opcional): instalar Azure CLI e GitHub CLI.** O portal e a aba Actions também servem. Se usadas, encerrar as sessões ao fim (`az logout`, `gh auth logout`): a máquina pode ser compartilhada.
- [ ] **Step 5: Conferir na documentação do Azure (Microsoft Learn), sem precisar de conta,** e registrar no spec, seção 12: (a) o App Service Linux oferece o runtime .NET 10; (b) o MySQL Flexible Server oferece a versão 8.4; (c) em qual nível de plano o **Always On** existe; (d) em quais regiões o Static Web Apps existe. **Se (a) ou (b) falhar, avisar o dono antes da Task 2** (plano B: .NET 9 com EF Core 9, ou MySQL 8.0).

---

### Task 2: Solution em camadas e testes de arquitetura

**Files:**
- Create: `global.json`, `.gitattributes`, `.config/dotnet-tools.json`, `.gitignore`, `.editorconfig` (todos na raiz), `back-end/Directory.Build.props`, `back-end/Directory.Packages.props`
- Create: `back-end/src/RHSystem.Domain/`, `RHSystem.Application/`, `RHSystem.Infrastructure/`, `RHSystem.Api/` e `back-end/tests/RHSystem.Domain.Tests/`, `RHSystem.Application.Tests/`, `RHSystem.Api.IntegrationTests/`
- Test: `back-end/tests/RHSystem.Domain.Tests/ArquiteturaTests.cs`

**Interfaces:**
- Produces: a solution `back-end/RHSystem` com 7 projetos; versionamento central de pacotes (`Directory.Packages.props`, com `Microsoft.EntityFrameworkCore.*` todos em 9.0.x e Pomelo 9.0.0); `global.json` na **raiz** fixando o SDK 10 (`rollForward: latestFeature`); ferramenta local `dotnet-ef` 9.0.x registrada em `.config/dotnet-tools.json`.

- [ ] **Step 1: Criar a solution, o projeto `RHSystem.Domain.Tests` (xUnit) e os arquivos de configuração.** `.gitattributes` com `* text=auto eol=lf`; `.editorconfig` com `end_of_line = lf` e `generated_code = true` para `**/Migrations/**`; `.gitignore` cobrindo `bin/`, `obj/`, `node_modules/`, `.env`, `*.user`. `Directory.Build.props`: `net10.0`, `Nullable` e `ImplicitUsings` habilitados, `TreatWarningsAsErrors` verdadeiro, **`WarningsNotAsErrors` com `NU1901;NU1902;NU1903;NU1904`** (avisos de vulnerabilidade não quebram o build) e `NU1608` em `WarningsAsErrors`. Os projetos gerados por `dotnet new` trazem `Version=` nos `PackageReference`: remova, pois o versionamento é central (senão dá `NU1008`).
- [ ] **Step 2: Escrever os testes de arquitetura que falham.** Eles leem os `.csproj` como XML, subindo de `AppContext.BaseDirectory` até a pasta `back-end`:

```csharp
[Fact] void Domain_csproj_nao_tem_ProjectReference_PackageReference_nem_FrameworkReference() { }
[Fact] void Assembly_do_Domain_nao_referencia_EntityFrameworkCore_nem_AspNetCore() { }   // typeof(Usuario).Assembly.GetReferencedAssemblies(); a partir da Task 4
[Fact] void Application_referencia_apenas_Domain() { }
[Fact] void Infrastructure_referencia_Application_e_nao_referencia_Api() { }
[Fact] void Api_referencia_Application_e_Infrastructure() { }
[Fact] void Directory_Build_props_e_Directory_Packages_props_nao_tem_ProjectReference() { }
```

O segundo teste só compila quando existir uma classe do Domain; ele nasce na Task 4 e fica fora desta.

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test back-end --filter ArquiteturaTests`
Expected: FAIL (os `.csproj` de `src/` não existem).

- [ ] **Step 4: Criar os quatro projetos de `src/` e os outros dois de teste** com as referências da regra de dependência. `RHSystem.Api` é web; os demais são bibliotecas. `RHSystem.Api.IntegrationTests` referencia Api, Infrastructure, Application e Domain.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet build back-end; dotnet test back-end --filter ArquiteturaTests`
Expected: build sem warnings; 5 testes passando.

- [ ] **Step 6: Commit** (com autorização), incluindo o spec e o plano, que hoje estão fora do git: `git add back-end global.json .gitattributes .config .gitignore .editorconfig docs; git commit -m "feat: solution em camadas e testes de arquitetura"`

---

### Task 3: `docker compose` local com MySQL e README

**Files:** Create: `docker-compose.yml`, `.env.example`, `README.md` (raiz).

**Interfaces:**
- Produces: MySQL 8.4 em `localhost:${MYSQL_PORTA:-3306}`, banco `rh_system`, `utf8mb4` e `utf8mb4_unicode_ci`; credenciais vindas de `.env` (copiado de `.env.example`, que só traz valores de desenvolvimento local).

- [ ] **Step 1: Escrever `docker-compose.yml`:** serviço `mysql` (`mysql:8.4`, `--character-set-server=utf8mb4 --collation-server=utf8mb4_unicode_ci`, volume nomeado, porta `${MYSQL_PORTA:-3306}:3306`) e `healthcheck` com `mysqladmin ping -h 127.0.0.1 -u root -p"$$MYSQL_ROOT_PASSWORD"`. O `-h 127.0.0.1` evita o servidor temporário de inicialização, que só atende por socket e daria `healthy` cedo demais.
- [ ] **Step 2: Subir e verificar**

Run: `docker compose up -d --wait; docker compose exec mysql sh -c 'mysql -uroot -p"$MYSQL_ROOT_PASSWORD" -e "SELECT @@character_set_server, @@collation_server, @@version"'`
Expected: serviço `healthy`; `utf8mb4`, `utf8mb4_unicode_ci` e `8.4.x`.

- [ ] **Step 3: Escrever o `README.md`** com o que é o projeto, os pré-requisitos (Task 1) e as seções "Banco", "Back-end" e "Front-end", preenchidas nas Tasks 8 e 10.
- [ ] **Step 4: Commit** (com autorização): `git add docker-compose.yml .env.example README.md; git commit -m "feat: docker compose local com MySQL"`

---

### Task 4: Domínio de autenticação, persistência e teste de fumaça do provider

**É a tarefa que confirma o maior risco do spec. Incompatibilidade de provider significa: `NU1608`, `NU1107`, `MissingMethodException` ou `TypeLoadException` ao usar `UseMySql` ou ao aplicar migrations. Se aparecer, PARE e avise o dono. Qualquer outro erro (por exemplo, índice único em coluna `longtext`) é bug de mapeamento nosso: corrija.**

**Files:**
- Create: `back-end/src/RHSystem.Domain/Autenticacao/Perfil.cs`, `Usuario.cs`, `RefreshToken.cs`
- Create: `back-end/src/RHSystem.Infrastructure/Persistencia/AppDbContext.cs`, `AppDbContextFactory.cs`, `Configuracoes/UsuarioConfiguracao.cs`, `RefreshTokenConfiguracao.cs`, `Migrations/` (gerada)
- Modify: `back-end/src/RHSystem.Api/RHSystem.Api.csproj` (`Microsoft.EntityFrameworkCore.Design` 9.0.x com `PrivateAssets=all`, exigido pelo `dotnet ef`)
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Infra/MySqlFixture.cs`, `ProviderMySqlSmokeTests.cs`; `back-end/tests/RHSystem.Domain.Tests/Autenticacao/UsuarioTests.cs`, `RefreshTokenTests.cs`; incluir o 2º teste de arquitetura da Task 2

**Interfaces:**
- Produces (Domain, `RHSystem.Domain.Autenticacao`):
  - `enum Perfil { Funcionario = 1, Gestor = 2, RH = 3 }`
  - `class Usuario { int Id; string Nome; string Email; string SenhaHash; Perfil Perfil; bool Ativo; DateTime? UltimoLoginEm; static Usuario Criar(string nome, string email, string senhaHash, Perfil perfil); void RegistrarLogin(DateTime agoraUtc); void Inativar(); }` (`Criar` normaliza o e-mail com `Trim()` e `ToLowerInvariant()`)
  - `class RefreshToken { int Id; int UsuarioId; string TokenHash; DateTime CriadoEm; DateTime ExpiraEm; DateTime? RevogadoEm; bool EstaAtivo(DateTime agoraUtc); static RefreshToken Criar(int usuarioId, string tokenHash, DateTime agoraUtc, TimeSpan validade); }`
- Produces (Infrastructure): `AppDbContext : DbContext` com `DbSet<Usuario> Usuarios` e `DbSet<RefreshToken> RefreshTokens`. Mapeamento **explícito** (`ToTable` e `HasColumnName` em cada propriedade) e charset fixado no modelo (`utf8mb4`, `utf8mb4_unicode_ci`):
  - `USUARIOS`: `ID_USUARIO`, `NOME` (150), `EMAIL` (150, índice único), `SENHA_HASH` (255), `PERFIL` (texto, 20), `ATIVO`, `DATA_HORA_ULTIMO_LOGIN`.
  - `REFRESH_TOKENS`: `ID_REFRESH_TOKEN`, `ID_USUARIO` (FK, sem cascata), `TOKEN_HASH` (64, índice único), `CRIADO_EM`, `EXPIRA_EM`, `REVOGADO_EM`.
  - Toda `string` tem `HasMaxLength`: sem tamanho o Pomelo cria `longtext`, e o MySQL não indexa `longtext`.
- Produces (testes): `MySqlFixture` (`IAsyncLifetime`; `new MySqlBuilder("mysql:8.4")` com a imagem no construtor, pois o construtor sem imagem é obsoleto; usuário com permissão `CREATE DATABASE`, ver a documentação do módulo) com `Task<string> NovoBancoAsync()` (banco vazio e exclusivo, devolve a connection string) e `Task<string> NovoBancoMigradoAsync()` (idem, já com as migrations). **Cada classe de teste de integração pede o seu próprio banco**, para não haver dependência de ordem.

- [ ] **Step 1: Escrever os testes de unidade do Domain que falham**

```csharp
[Fact] void Criar_normaliza_o_email() => Assert.Equal("ana@empresa.com", Usuario.Criar("Ana", "  Ana@Empresa.com ", "h", Perfil.RH).Email);
[Fact] void Inativar_marca_Ativo_false() { }
[Fact] void RefreshToken_expirado_ou_revogado_nao_esta_ativo() { }
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Domain.Tests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar as entidades** (sem pacote). Adicionar o 2º teste de arquitetura da Task 2.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS (inclui os testes de arquitetura).
- [ ] **Step 5: Escrever o teste de fumaça que falha**, em `ProviderMySqlSmokeTests.cs`, usando `NovoBancoMigradoAsync()`:

```csharp
[Fact] async Task Usuario_faz_ida_e_volta_com_unicode_e_enum_como_texto()
  // grava Nome = "José Ação 😀", Perfil.RH; relê com outro AppDbContext; confere o Nome e o Perfil
[Fact] async Task Email_duplicado_viola_o_indice_unico() { }
[Fact] async Task Tabelas_e_colunas_tem_os_nomes_mapeados_e_o_banco_usa_utf8mb4_unicode_ci() { }
  // consulta information_schema.COLUMNS e information_schema.SCHEMATA
```

- [ ] **Step 6: Rodar e ver falhar**

Run: `dotnet test back-end --filter ProviderMySqlSmokeTests`
Expected: FAIL (sem `AppDbContext`).

- [ ] **Step 7: Implementar `AppDbContext` e as configurações.** `UseMySql(connectionString, new MySqlServerVersion(new Version(8, 4, 0)))`, versão fixa (o `dotnet ef` não abre conexão em tempo de design). `AppDbContextFactory : IDesignTimeDbContextFactory<AppDbContext>` lê a connection string de `RH_CONNECTION_STRING` (com padrão local), pois a API só registra o `DbContext` na Task 6. Instalar e conferir a ferramenta, depois gerar a migration inicial:

Run: `dotnet tool restore; dotnet ef --version`
Expected: `9.0.x`. Se a ferramenta 9.x se recusar a rodar só com o runtime 10, defina `DOTNET_ROLL_FORWARD=Major` para o comando e registre no README.

Run: `dotnet ef migrations add Inicial --project back-end/src/RHSystem.Infrastructure --startup-project back-end/src/RHSystem.Api --output-dir Migrations`
Expected: migration criada, sem warnings de versão.

- [ ] **Step 8: Rodar e ver passar**

Run: `dotnet test back-end --filter ProviderMySqlSmokeTests`
Expected: PASS, sem `NU1608`/`NU1107` e sem `MissingMethodException`. **Se falhar por incompatibilidade de provider (definição acima), PARE e reporte.**

- [ ] **Step 9: Commit** (com autorização): `git add back-end; git commit -m "feat: dominio de autenticacao, persistencia e teste de fumaca do provider"`

---

### Task 5: Casos de uso de autenticação (Application)

**Files:**
- Create: `back-end/src/RHSystem.Application/Autenticacao/Contratos.cs`, `Dtos.cs`, `EntrarCasoDeUso.cs`, `RenovarTokenCasoDeUso.cs`, `SairCasoDeUso.cs`
- Test: `back-end/tests/RHSystem.Application.Tests/Autenticacao/EntrarCasoDeUsoTests.cs`, `RenovarTokenCasoDeUsoTests.cs`, `SairCasoDeUsoTests.cs`, `Falsos/` (repositórios, hasher, relógio e unidade de trabalho em memória)

**Interfaces:**
- Consumes: `Usuario`, `RefreshToken`, `Perfil` (Task 4).
- Produces (`RHSystem.Application.Autenticacao`):
  - `IUsuarioRepositorio { Task<Usuario?> ObterPorEmailAsync(string emailNormalizado, CancellationToken ct); Task<Usuario?> ObterPorIdAsync(int id, CancellationToken ct); Task AdicionarAsync(Usuario u, CancellationToken ct); }`
  - `IRefreshTokenRepositorio { Task AdicionarAsync(RefreshToken t, CancellationToken ct); Task<RefreshToken?> ObterPorHashAsync(string tokenHash, CancellationToken ct); Task<bool> TentarRevogarAsync(string tokenHash, DateTime agoraUtc, CancellationToken ct); Task RevogarTodosDoUsuarioAsync(int usuarioId, DateTime agoraUtc, CancellationToken ct); }` — `TentarRevogarAsync` é atômico e devolve `true` só para quem de fato revogou.
  - `IUnidadeDeTrabalho { Task SalvarAsync(CancellationToken ct); Task<T> ExecutarEmTransacaoAsync<T>(Func<Task<T>> acao, CancellationToken ct); }`
  - `ISenhaHasher { string Gerar(string senha); bool Verificar(string hash, string senha); }`
  - `IEmissorDeTokens { AcessoEmitido EmitirAcesso(Usuario u); RefreshGerado NovoRefresh(); string HashDoRefresh(string token); }`, `record AcessoEmitido(string Token, DateTime ExpiraEmUtc)`, `record RefreshGerado(string Token, string Hash)`
  - `IRelogio { DateTime AgoraUtc { get; } }`
  - DTOs: `record EntrarComando(string Email, string Senha)`, `record RenovarComando(string RefreshToken)`, `record UsuarioResposta(int Id, string Nome, string Email, Perfil Perfil)`, `record SessaoResposta(string TokenDeAcesso, DateTime AcessoExpiraEmUtc, string RefreshToken, UsuarioResposta Usuario)`
  - `EntrarCasoDeUso.ExecutarAsync(EntrarComando, CancellationToken) -> Task<SessaoResposta?>` (`null` = inválido), `RenovarTokenCasoDeUso.ExecutarAsync(RenovarComando, CancellationToken) -> Task<SessaoResposta?>`, `SairCasoDeUso.ExecutarAsync(RenovarComando, CancellationToken) -> Task`

- [ ] **Step 1: Escrever os testes que falham**, com falsos em memória (todos `async Task`):

```csharp
Entrar_com_email_em_maiusculas_e_espacos_autentica_o_mesmo_usuario        // Review Focus 1
Entrar_com_senha_errada_ou_email_inexistente_devolve_null
Entrar_com_email_inexistente_ainda_chama_Verificar_do_hasher              // contra enumeração por tempo
Entrar_com_usuario_inativo_devolve_null
Entrar_registra_UltimoLoginEm_e_guarda_apenas_o_hash_do_refresh
Renovar_rotaciona_revoga_o_antigo_e_emite_um_novo_na_mesma_transacao
Renovar_com_token_ja_revogado_devolve_null_e_nao_revoga_os_outros_tokens_do_usuario
Renovar_para_usuario_inativo_devolve_null_e_revoga_todos_os_tokens_do_usuario
Renovar_com_token_expirado_devolve_null
Sair_revoga_o_refresh_informado
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Application.Tests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar contratos, DTOs e os três casos de uso.** `Entrar` normaliza o e-mail (`Trim().ToLowerInvariant()`) e, quando o usuário não existe, verifica a senha contra um hash fictício gerado **uma vez pelo mesmo `ISenhaHasher`** (mesmo custo), para o tempo de resposta não revelar quais e-mails existem. `Renovar` faz a revogação do antigo e a criação do novo dentro de `ExecutarEmTransacaoAsync`; se `TentarRevogarAsync` devolver `false`, devolve `null`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: casos de uso de autenticacao"`

---

### Task 6: Infraestrutura de autenticação (hash, JWT, repositórios)

**Files:**
- Create: `back-end/src/RHSystem.Infrastructure/Autenticacao/SenhaHasher.cs`, `EmissorDeTokensJwt.cs`, `OpcoesJwt.cs`, `UsuarioRepositorio.cs`, `RefreshTokenRepositorio.cs`, `UnidadeDeTrabalho.cs`, `RelogioDoSistema.cs`; `back-end/src/RHSystem.Infrastructure/DependencyInjection.cs`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Autenticacao/SenhaHasherTests.cs`, `EmissorDeTokensJwtTests.cs`, `RefreshTokenRepositorioTests.cs`

**Interfaces:**
- Consumes: interfaces da Task 5; `AppDbContext` da Task 4.
- Produces:
  - **Configuração lida por `AdicionarInfraestrutura`:** `ConnectionStrings:Padrao`, `Database:ServerVersion` (padrão `8.4.0`), `Jwt:Chave` (mínimo 32 bytes), `Jwt:Emissor`, `Jwt:Audiencia`, `Jwt:AcessoMinutos` (15), `Jwt:RefreshDias` (7). `RH_CONNECTION_STRING` é só da fábrica de tempo de design.
  - `class OpcoesJwt { string Chave; string Emissor; string Audiencia; int AcessoMinutos = 15; int RefreshDias = 7; }`
  - `static IServiceCollection AdicionarInfraestrutura(this IServiceCollection s, IConfiguration c)`: registra `AppDbContext`, repositórios, hasher, emissor, relógio e unidade de trabalho.
  - `SenhaHasher` usa `PasswordHasher<Usuario>` (pacote `Microsoft.Extensions.Identity.Core`; se o restore avisar `NU1510`, siga a mensagem) e lança `ArgumentException` para senha acima de 128 caracteres.
  - `EmissorDeTokensJwt.NovoRefresh()` gera 32 bytes aleatórios em Base64Url; `HashDoRefresh` é SHA-256 em hexadecimal (64 caracteres). O JWT leva `sub` (Id), `email`, `nome` e `perfil` (texto).
  - `TentarRevogarAsync` usa `ExecuteUpdateAsync` com `WHERE TOKEN_HASH = @h AND REVOGADO_EM IS NULL`, na transação corrente, e devolve `linhasAfetadas == 1`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Hash_verifica_senha_correta_e_rejeita_errada
Senha_com_acentos_e_emoji_gera_e_verifica                       // Review Focus 2
Senha_com_mais_de_128_caracteres_lanca_ArgumentException        // Review Focus 2
Jwt_emitido_contem_sub_email_nome_perfil_e_expira_em_15_minutos
TentarRevogar_com_20_chamadas_simultaneas_devolve_true_para_exatamente_uma   // 20 tarefas, cada uma com seu DbContext e conexão, liberadas juntas por uma Barrier, repetido 5 vezes; no MySQL real (NovoBancoMigradoAsync)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Api.IntegrationTests.Autenticacao"`
Expected: FAIL.

- [ ] **Step 3: Implementar as classes.**
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: infraestrutura de autenticacao"`

---

### Task 7: API de autenticação (endpoints e JWT)

**Files:**
- Create: `back-end/src/RHSystem.Api/Autenticacao/AutenticacaoEndpoints.cs`, `Configuracao/AutenticacaoExtensions.cs`, `Configuracao/TratamentoDeErros.cs`; `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs`; `contrato/sessao.exemplo.json` (raiz)
- Modify: `back-end/src/RHSystem.Api/Program.cs`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Autenticacao/AutenticacaoEndpointsTests.cs`

**Interfaces:**
- Consumes: casos de uso (Task 5), `AdicionarInfraestrutura` (Task 6), `MySqlFixture.NovoBancoMigradoAsync()` (Task 4).
- Produces (rotas sob `/api/auth`):
  - `POST /api/auth/entrar` corpo `EntrarComando` → `200 SessaoResposta` | `401` | `400` (campo vazio ou senha acima de 128 caracteres)
  - `POST /api/auth/renovar` corpo `RenovarComando` → `200 SessaoResposta` | `401`
  - `POST /api/auth/sair` corpo `RenovarComando` → `204`
  - `GET /api/auth/eu` (`Authorization: Bearer`) → `200 UsuarioResposta` | `401`
- Erros sempre `ProblemDetails` (`application/problem+json`) em português, **sem stack trace**, inclusive o 401 do `JwtBearer` (que sozinho vem sem corpo: tratar em `OnChallenge`). O 401 de login usa sempre o mesmo `title` e `detail` (`"E-mail ou senha inválidos."`).
- Enums saem como **texto** (`JsonStringEnumConverter`). `ClockSkew` do JWT = 30 s.
- `ApiFactory : WebApplicationFactory<Program>`: ambiente `Testing`; **toda a configuração em memória** (inclusive `Jwt:Chave`, para o teste não depender de user-secrets nem do CI); cada classe de teste recebe um banco novo e migrado; semeia um RH ativo e um usuário inativo; permite trocar o `IRelogio` por um falso e o ambiente por `Production`.
- `contrato/sessao.exemplo.json`: exemplo de `SessaoResposta` com as chaves `tokenDeAcesso`, `acessoExpiraEmUtc` (terminando em `Z`), `refreshToken` e `usuario` (`id`, `nome`, `email`, `perfil`). A API e o front-end são testados contra o mesmo arquivo (Task 9).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Entrar_com_credenciais_validas_devolve_200_com_tokens
Resposta_de_entrar_tem_exatamente_as_chaves_de_contrato_sessao_exemplo_json
Entrar_com_email_inexistente_senha_errada_e_usuario_inativo_devolvem_o_mesmo_status_title_e_detail   // Review Focus 4; NÃO comparar traceId
Entrar_com_senha_acima_de_128_caracteres_devolve_400                                                // Review Focus 2
Entrar_com_senha_unicode_funciona                                                                   // Review Focus 2
Renovar_rotaciona_e_o_token_antigo_passa_a_devolver_401
Renovar_simultaneo_com_o_mesmo_token_nunca_devolve_500_no_maximo_um_200_e_o_token_do_vencedor_continua_valido  // Review Focus 3
Eu_sem_token_ou_com_token_malformado_devolve_401_ProblemDetails_sem_stack_trace                      // Review Focus 5
Eu_com_token_expirado_devolve_401   // relógio falso avançado 20 minutos
Erro_inesperado_em_Production_devolve_500_ProblemDetails_sem_stack_trace   // rota de teste que lança exceção, registrada só no ApiFactory; ambiente Production
Eu_com_token_valido_devolve_o_usuario_com_perfil_como_texto               // Assert.Equal("RH", ...)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter AutenticacaoEndpointsTests`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar os endpoints** (minimal API), `JwtBearer` lendo `OpcoesJwt`, `OnChallenge` com `ProblemDetails`, tratamento global de exceções e os registros em `Program.cs`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end contrato; git commit -m "feat: endpoints de autenticacao"`

---

### Task 8: Limite de tentativas, proxy, CORS, saúde, documentação da API, migração e seed

**Files:**
- Create: `back-end/src/RHSystem.Api/Configuracao/LimiteDeTentativas.cs`, `Cors.cs`, `ProxyReverso.cs`, `InicializacaoDoBanco.cs`, `Saude/BancoHealthCheck.cs`
- Modify: `back-end/src/RHSystem.Api/Program.cs`, `appsettings.json`, `appsettings.Development.json`, `back-end/tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs` (limite de login 1000 por padrão), `README.md`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Configuracao/LimiteDeTentativasTests.cs`, `SaudeTests.cs`, `InicializacaoTests.cs`, `CorsTests.cs`

**Interfaces:**
- Produces (configuração; as da Task 6 já existem): `Database:MigrarAoIniciar` (bool), `Autenticacao:LimiteLoginPorMinuto` (padrão 5), `Cors:OrigensPermitidas` (lista), `ProxyReverso:Habilitado` (bool), `Documentacao:Habilitada` (bool), `Seed:AdminNome|AdminEmail|AdminSenha`.
- Limitador: **5 requisições por minuto por IP** em `POST /api/auth/entrar`; a sexta devolve **`429`** com `ProblemDetails` e `Retry-After` (o limitador do ASP.NET rejeita por padrão com 503 e sem corpo: configure `RejectionStatusCode` e `OnRejected`). O IP vem de `RemoteIpAddress`; com `ProxyReverso:Habilitado` o `UseForwardedHeaders` (`X-Forwarded-For` e `-Proto`) o ajusta, aceitando só o proxy da plataforma. Cada tentativa de login registra um log `Information` com o IP.
- `GET /health` → `200 {"status":"ok"}` com o banco acessível, `503` se ele cair **depois da subida**. Com o banco parado na partida e `MigrarAoIniciar` ligado, a API falha ao iniciar (comportamento aceito e documentado no README).
- `Documentacao:Habilitada` publica o documento OpenAPI nativo e uma UI (escolher e conferir compatibilidade com o .NET 10 na hora: `Swashbuckle.AspNetCore` ou `Scalar.AspNetCore`). Ligada em `Development`; no Azure o dono decide.
- Seed: com `MigrarAoIniciar=true` aplica as migrations; se `Seed:AdminEmail` e `Seed:AdminSenha` estiverem preenchidos **e não existir usuário**, cria um `Usuario` com `Perfil.RH`; nunca registra a senha em log.
- `appsettings.Development.json` traz só valores locais, sem segredo real (`ConnectionStrings:Padrao` para o compose, com `AllowPublicKeyRetrieval=True` **só em desenvolvimento**). A chave do JWT local vem de user-secrets: `dotnet user-secrets init --project back-end/src/RHSystem.Api` e depois `dotnet user-secrets set "Jwt:Chave" "<32+ bytes>" --project back-end/src/RHSystem.Api`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
Sexta_tentativa_de_login_no_mesmo_minuto_devolve_429_ProblemDetails_com_Retry_After   // limite 5 só neste teste
Health_devolve_200_com_banco_e_503_com_connection_string_para_porta_fechada            // Review Focus 5; MigrarAoIniciar=false; não parar o contêiner compartilhado
Seed_cria_o_admin_uma_unica_vez_e_nao_cria_sem_configuracao                           // banco novo (NovoBancoMigradoAsync)
Inicializacao_aplica_migrations_quando_MigrarAoIniciar_e_true                         // banco novo e vazio (NovoBancoAsync)
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

- [ ] **Step 6: Preencher no `README.md`** os comandos reais: `docker compose up -d --wait`, `dotnet run --project back-end/src/RHSystem.Api`, e a nota sobre a API não subir sem banco.
- [ ] **Step 7: Commit** (com autorização): `git add back-end README.md; git commit -m "feat: limite de tentativas, proxy, cors, saude, migracao e seed"`

---

### Task 9: Front-end: projeto, cliente de API e contexto de autenticação

**Files:**
- Create: `front-end/` (Vite + React + TypeScript), `front-end/.prettierrc`, `.env.example`, `src/shared/api/clienteApi.ts`, `src/shared/api/tipos.ts`, `src/features/autenticacao/AuthProvider.tsx`, `useAuth.ts`, `armazenamentoDaSessao.ts`
- Test: `src/shared/api/clienteApi.test.ts`, `src/shared/api/contrato.test.ts`, `src/features/autenticacao/AuthProvider.test.tsx`

**Interfaces:**
- Produces:
  - `type Usuario = { id: number; nome: string; email: string; perfil: 'Funcionario' | 'Gestor' | 'RH' }`; `type Sessao = { tokenDeAcesso: string; acessoExpiraEmUtc: string; refreshToken: string; usuario: Usuario }`; `function ehSessao(x: unknown): x is Sessao`
  - `type ClienteApi = { entrar(email: string, senha: string): Promise<Sessao>; renovar(refreshToken: string): Promise<Sessao>; sair(refreshToken: string): Promise<void>; eu(tokenDeAcesso: string): Promise<Usuario> }` e `criarClienteApi(urlBase: string, fetchImpl?: typeof fetch): ClienteApi`. Resposta HTTP de erro vira `class ErroDeApi extends Error { status: number }`.
  - `useAuth(): { usuario: Usuario | null; carregando: boolean; entrar(email: string, senha: string): Promise<void>; sair(): Promise<void> }`. Token de acesso **em memória**; refresh em `sessionStorage` (chave `rh.refresh`). Ao montar, o `AuthProvider` tenta renovar com o refresh guardado, **com uma única renovação em andamento por vez** (as chamadas concorrentes compartilham a mesma promessa). `AuthProvider` recebe o `ClienteApi` por prop.
- URL da API: `VITE_API_URL`.

- [ ] **Step 1: Criar o projeto** com o template React + TypeScript do Vite **sem perguntas interativas** (confira as flags em `npm create vite@latest -- --help`), instalar Vitest, Testing Library (`@testing-library/react`, `@testing-library/user-event`, `jsdom`), ESLint e **Prettier**, e criar os scripts `lint`, `format:check`, `test` e `build`. Conferir que o Node atende o `engines` do Vite.
- [ ] **Step 2: Escrever os testes que falham**

```ts
clienteApi.entrar envia POST /api/auth/entrar e devolve a sessao
clienteApi transforma resposta 401, 400, 429 e 500 em ErroDeApi com o status
ehSessao aceita contrato/sessao.exemplo.json e rejeita um objeto sem usuario    // lê o mesmo arquivo do back-end
AuthProvider renova a sessao ao montar quando ha refresh no sessionStorage
AuthProvider em StrictMode (montagem dupla) chama renovar uma unica vez          // Review Focus 3
AuthProvider com renovacao que falha ao montar limpa o sessionStorage e fica sem usuario
AuthProvider.sair limpa o sessionStorage e o usuario
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

### Task 10: Front-end: tela de login, shell e cabeçalhos de segurança

**Files:**
- Create: `front-end/src/app/App.tsx`, `ShellDoPortal.tsx`, `rotulosDePerfil.ts`, `front-end/src/features/autenticacao/PaginaEntrar.tsx`, `PortaDeAcesso.tsx`, `front-end/public/staticwebapp.config.json`
- Test: `front-end/src/features/autenticacao/PaginaEntrar.test.tsx`, `PortaDeAcesso.test.tsx`, `front-end/src/app/ShellDoPortal.test.tsx`, `front-end/src/app/staticwebapp.config.test.ts`

**Interfaces:**
- Consumes: `useAuth` e `ClienteApi` (Task 9).
- Produces: `PortaDeAcesso` mostra um indicador enquanto `carregando`, `PaginaEntrar` sem usuário e `ShellDoPortal` com usuário (**sem biblioteca de rotas**). `ShellDoPortal` mostra "Portal de RH" (o nome do menu no Figma), o nome do usuário, o rótulo do perfil (`rotulosDePerfil`: `Funcionario` → "Funcionário", `Gestor` → "Gestor", `RH` → "RH") e o botão "Sair".
- Textos: rótulos "E-mail" e "Senha", botão "Entrar"; 401 `"E-mail ou senha inválidos."`; 400 `"Preencha o e-mail e a senha."`; 429 `"Muitas tentativas. Aguarde um minuto e tente de novo."`; 500 `"Erro no servidor. Tente novamente em instantes."`; falha de rede `"Não foi possível conectar. Tente novamente."`.
- `staticwebapp.config.json`: `navigationFallback.rewrite = "/index.html"` e `globalHeaders` com `Content-Security-Policy` (`default-src 'self'`; `connect-src 'self'` mais a URL da API, preenchida na Task 13), `X-Content-Type-Options: nosniff`, `X-Frame-Options: DENY`, `Referrer-Policy: no-referrer` e `Permissions-Policy`.

- [ ] **Step 1: Escrever os testes que falham**

```tsx
PaginaEntrar mostra a mensagem certa para 401, 400, 429, 500 e falha de rede
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

- [ ] **Step 5: Verificação manual ponta a ponta** (exige Docker): `docker compose up -d --wait`, API com `Seed:AdminEmail` e `Seed:AdminSenha` definidos por user-secrets, `dotnet run --project back-end/src/RHSystem.Api` e `npm --prefix front-end run dev`. Entrar com o admin, ver o shell, sair, e confirmar que **F5 não desloga**. Registrar o resultado.
- [ ] **Step 6: Preencher no `README.md`** os comandos do front-end.
- [ ] **Step 7: Commit** (com autorização): `git add front-end README.md; git commit -m "feat: login, shell do portal e cabecalhos de seguranca"`

---

### Task 11: Integração contínua (GitHub Actions)

**Files:** Create: `.github/workflows/ci.yml`.

**Interfaces:**
- Produces: workflow `CI` em `push` e `pull_request`, com dois jobs independentes. `back-end` (`ubuntu-latest`, que traz Docker para o Testcontainers): setup-dotnet com `global-json-file: global.json`, `dotnet tool restore`, `dotnet restore`, `dotnet build --no-restore`, `dotnet format back-end --verify-no-changes`, `dotnet test --no-build`. `front-end`: setup-node na LTS da Task 1, `npm ci`, `lint`, `format:check`, `test -- --run`, `build`.

- [ ] **Step 1: Escrever o workflow.** Fixar cada ação por versão maior estável conferida na documentação da ação na hora, não de memória.
- [ ] **Step 2: Validar localmente**

Run: `dotnet format back-end --verify-no-changes; npm --prefix front-end ci; npm --prefix front-end run format:check`
Expected: sem diferenças de formatação (as migrations ficam fora, por `generated_code`); instalação limpa.

- [ ] **Step 3: Com autorização do dono, dar push em uma branch** e conferir os dois jobs.

Run: `gh run watch` (ou abrir a aba Actions)
Expected: os dois jobs verdes.

- [ ] **Step 4: Commit** (com autorização): `git add .github; git commit -m "ci: build e testes do back-end e do front-end"`

---

### Task 12: Workflows de deploy manual

**Files:** Create: `.github/workflows/deploy-api.yml`, `.github/workflows/deploy-front.yml`.

**Interfaces:**
- Produces: dois workflows com `workflow_dispatch` e disparo por tag `v*`, **nunca por push em branch**. `deploy-api` roda os testes e, se passarem, publica `RHSystem.Api` (`dotnet publish -c Release`) no App Service usando o **publish profile** (decisão: é o método mais simples para quem nunca usou Azure; a Task 13 liga a autenticação básica do SCM, que vem desligada em App Services novos) guardado no secret `AZURE_WEBAPP_PUBLISH_PROFILE`. `deploy-front` gera o build com `VITE_API_URL` (variável do repositório) e publica no Static Web Apps com o token do secret `AZURE_STATIC_WEB_APPS_API_TOKEN`.
- **O botão "Run workflow" só aparece quando o workflow está na branch padrão (`main`):** é preciso fazer merge para `main` (com autorização do dono) antes da Task 13, Step 6.

- [ ] **Step 1: Escrever os dois workflows**, conferindo a sintaxe das ações oficiais de deploy do Azure na documentação atual.
- [ ] **Step 2: Validar a sintaxe** (por exemplo com `actionlint`, se instalado) e, depois do merge para `main`, conferir que os dois aparecem em Actions.

Expected: workflows listados, sem erro de sintaxe. **O deploy em si só é verificado na Task 13.**

- [ ] **Step 3: Commit** (com autorização): `git add .github; git commit -m "ci: workflows de deploy manual para o Azure"`

---

### Task 13: Azure guiado: provisionar, publicar e verificar

**Feita junto com o dono, que nunca usou o Azure. Antes de começar, o dono deve ter confirmado qual conta tem os créditos do SENAI. Cada passo é conferido na documentação atual (Microsoft Learn), não de memória. O assistente nunca vê os valores dos segredos: o dono os gera e cola direto no portal e no GitHub.**

**Files:** Create: `docs/azure/passo-a-passo-fatia-0.md` (roteiro com os nomes e valores reais, sem segredos). Modify: `README.md` (link), `front-end/public/staticwebapp.config.json` (URL da API no `connect-src`).

**Interfaces:**
- Produces: grupo de recursos único; App Service **Linux**; Static Web Apps; MySQL Flexible Server; um banco `rh_system` com **usuário próprio da API** (`ALL` só nesse banco, criado por um script SQL curto que o roteiro traz); configurações do App Service `ConnectionStrings__Padrao`, `Jwt__Chave|Emissor|Audiencia`, `Cors__OrigensPermitidas__0`, `Database__MigrarAoIniciar=true`, `Database__ServerVersion`, `ProxyReverso__Habilitado=true`, `Documentacao__Habilitada`, `Seed__AdminNome|AdminEmail|AdminSenha`; secrets e variável do GitHub da Task 12.

- [ ] **Step 1: Confirmar a conta com os créditos e a região permitida** (que ofereça o MySQL e o Static Web Apps, conforme a Task 1, Step 5). **Parar se não estiver confirmada.**
- [ ] **Step 2: Conferir, na conta, o plano e o orçamento:** o plano do App Service escolhido tem Always On? Criar um **alerta de orçamento** no Cost Management antes de qualquer recurso. **Perguntar ao dono** se aceita um plano pago do App Service (o Always On, de que o agendador das fatias seguintes depende, exige nível pago) e, se o crédito acabar, que a demo cai.
- [ ] **Step 3: Para cada recurso, dizer o SKU exato e o custo estimado e esperar o OK do dono** (os padrões do portal costumam ser caros, como General Purpose, alta disponibilidade e Premium; o assistente diz qual valor escolher em cada tela). Ordem: grupo de recursos, **App Service**, MySQL Flexible Server (firewall: **perguntar ao dono** entre liberar só os IPs de saída do App Service, mais seguro e mais trabalhoso, ou "permitir serviços do Azure", mais simples; mais o IP do dono quando preciso), Static Web Apps **com origem de deploy "Other"** e sem o Deployment Center do App Service (os dois criariam workflows que publicam a cada push, contra o spec).
- [ ] **Step 4: Segredos.** O dono gera a chave do JWT e a senha do admin **localmente** (o roteiro traz o comando) e cola direto nas configurações do App Service e nos secrets do GitHub. Ligar **HTTPS Only** e a autenticação básica do SCM (para o publish profile). Criar o banco e o usuário da API com o script SQL do roteiro.
- [ ] **Step 5: Atualizar o `connect-src`** do `staticwebapp.config.json` com a URL real da API e conferir que `Cors__OrigensPermitidas__0` é a URL real do front.
- [ ] **Step 6: Disparar `deploy-api` e `deploy-front`** pelo GitHub Actions (os workflows já estão em `main`).

Expected: os dois verdes.

- [ ] **Step 7: Verificar no ambiente real**

Run: `curl.exe -s -o NUL -w "%{http_code}" https://<url-da-api>/health` e abrir a URL do front.
Expected: `200`; no navegador, o dono entra com o admin, vê o shell, sai, e **F5 não desloga**. Depois conferir no log do App Service o IP registrado em uma tentativa de login: **não pode ser o IP do proxy** (se for, o limite de login virou global; corrigir `ProxyReverso`).

- [ ] **Step 8: Remover a configuração `Seed__AdminSenha`** do App Service (o admin já existe; a senha não precisa continuar guardada) e registrar no roteiro. A troca de senha chega na fatia 1.
- [ ] **Step 9: Deixar o ambiente seguro para o custo.** Mostrar ao dono como parar ou apagar o que gasta crédito, registrando que **parar o app não zera o custo do plano do App Service** (só apagar o plano ou rebaixá-lo) e que o MySQL parado pode religar sozinho depois de um tempo (conferir o prazo na documentação). Encerrar as sessões de `az` e `gh`.
- [ ] **Step 10: Commit** (com autorização): `git add docs/azure README.md front-end; git commit -m "docs: roteiro de deploy no Azure da fatia 0"`

---

## Self-review (cobertura do spec, linha 0 da seção 8)

| Item da fatia 0 | Task |
|---|---|
| Solution em camadas | 2 |
| `docker compose` | 3 (só MySQL; Mailpit e Azurite entram nas fatias que os usam) |
| Teste de fumaça do provider | 4 |
| Login: JWT, refresh com rotação, inativo bloqueado, limite de tentativas | 4 a 8 |
| Endpoint de saúde, migração na inicialização, segredos | 8 e 13 |
| Cabeçalhos de segurança no front (spec, seção 5) | 10 e 13 |
| Shell do React | 9 e 10 |
| CI com verificação de formatação nos dois lados | 11 |
| Deploy no Azure (manual; custo dito antes; conta confirmada; Always On e runtime conferidos) | 1, 12 e 13 |
