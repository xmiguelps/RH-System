# Fatia 1a (fundação técnica) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido pelo dono: ele decide depois do planejamento.** Entrada do plano do back-end: `docs/superpowers/plans/2026-10-30-plano-back-end.md`.

**Goal:** Entregar a base técnica que as fatias 1b e 1c (e as 2 a 8) usam sem refazer: mapeamento do EF das tabelas novas, matrícula sem buraco, erros, listas e datas no padrão do contrato, autorização por família de rota com escopo por perfil e a matriz de perfis, auditoria, fila de e-mails, agendador, os contadores do menu e a porta dos pedidos parados no gestor, **sem tela nova para o usuário**.

**Architecture:** Monólito modular em Clean Architecture (`back-end/src/RHSystem.{Domain,Application,Infrastructure,Api}`), casos de uso como classes simples (sem biblioteca de mediação), entidades do Domain mapeadas direto pelo EF (Fluent API na Infrastructure), testes de integração com MySQL real em contêiner aplicando os mesmos scripts. Tudo aqui é **encanamento atrás de interfaces pequenas e estáveis** (seção "Para as fatias 1b e 1c"): as fatias seguintes pedem o escopo, gravam um evento, enfileiram um e-mail ou agendam uma tarefa por essas interfaces, e cada tarefa abaixo traz um teste que as fatias seguintes reaproveitam (matriz de perfis, conformidade modelo × banco, resposta conforme o contrato).

**Tech Stack:** .NET 10, EF Core 9.x sem migrations, `Pomelo.EntityFrameworkCore.MySql` 9.0.0, MySQL 8.4, xUnit, Testcontainers (MySQL e Mailpit), **MailKit** (SMTP) e **Mailpit** no `docker compose` (captador local de e-mail). Versões dos pacotes novos (MailKit; o que a Task 8 da fatia 0 escolher para ler o OpenAPI): **conferir no nuget.org na hora**, versão estável, fixada em `Directory.Packages.props`.

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seções 4 "Autorização", 5 "E-mails e auditoria", 7 "Testes"); roadmap `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (fatia 1, incremento 1a; "Decisões que atravessam fatias" 2 a 7, 12 e 13); contrato `contrato/CONVENCOES.md` e `contrato/fontes/00-base.yaml`, `10-fatia-0-autenticacao.yaml`, `20-fatia-1-colaboradores.yaml` (auditoria) e `70-fatia-7-gestao.yaml` (contadores do menu); scripts `Banco de Dados/Scripts/V030` a `V038`; design dos planos `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (seção 4).

## Global Constraints

- .NET 10 com **EF Core 9.x** e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**; **sem migrations**. Dependências: Api e Infrastructure dependem de Application; Application depende de Domain; **Domain não depende de nada** (nem `FrameworkReference`; o BCL, como `System.Text.Json`, pode).
- Casos de uso são **classes simples**, sem biblioteca de mediação. Entidades do Domain mapeadas pelo EF por Fluent API na Infrastructure.
- **Scripts `V001` a `V047` não se editam sem avisar o dono; depois da Task 14 da fatia 0 (primeiro deploy) só script novo** (o executor recusa subir com script já aplicado editado, e depois da Task 14 o guarda da CI vigia `CONGELADOS.sha256`). Se esta fatia achar que falta coluna, índice ou tabela: **para e pergunta ao dono**; a mudança é um script novo (`V048` em diante, **numerado pelo dono**), nunca edição.
- **O contrato é lei:** a API implementa `contrato/openapi.yaml` (gerado; não se edita). Não se inventa rota, campo nem `codigo` de erro: faltando algo, pergunta-se ao dono. **O escopo do `ContratoDivergenciaTests` é uma lista de `operationId` mantida no próprio teste, nunca um prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`): **cada tarefa que entrega rotas acrescenta os `operationId` dela no mesmo passo em que implementa as rotas** (nunca antes: operação do contrato que está no escopo e a API ainda não tem é divergência e falharia no meio da fatia). Toda rota declara **todos** os códigos de resposta do contrato (`WithName`, `Produces<T>`, `ProducesProblem`).
- **`contrato/STATUS.md`:** a tabela **por fatia** continua como está (é a que o front lê); abaixo dela fica a seção "Rotas implementadas" (uma linha por `operationId`). **Quem edita a seção é a própria tarefa que entrega a rota, no mesmo passo** (o dono revisa no PR); `pronta` só o dono marca, depois de publicada no ambiente da demo; aqui a rota fica `em andamento`.
- **Datas das tabelas só da aplicação** (`CRIADO_EM`, `EXPIRA_EM`, `QUANDO`, `EXECUTAR_APOS`...): a aplicação **sempre** as informa, a partir do `IRelogio` (UTC). O `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança; os testes com relógio falso esperam os valores do relógio falso.
- **Fuso:** tabelas do grupo em `-03:00`; tabelas só da aplicação (`V033` a `V038`, `REFRESH_TOKENS`) em **UTC**; a API fala UTC (`...Z`); a conversão é na fronteira e as colunas que trigger preenche ficam vazias (roadmap, decisão 4; plano da fatia 0, "Fuso horário").
- **Privacidade:** senha, hash e token nunca vão para log, auditoria, fila de e-mail depois do envio nem mensagem de erro (roadmap, decisão 13).
- **Commits e PRs: nunca pôr o Claude como coautor** (nem `Co-Authored-By`, nem "Generated with Claude Code"). Só commitar e enviar quando o dono pedir (perguntar uma vez, no início, se pode commitar por tarefa). Segredos nunca vão para o repositório.
- Nomes de domínio e de API em português sem acento; mensagens ao usuário em português do Brasil com acento. Comandos rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; `curl.exe`). `TreatWarningsAsErrors` e `dotnet format back-end --verify-no-changes` valem para tudo o que esta fatia escrever.
- **Os arquivos de `Banco de Dados/` são do grupo:** esta fatia não os toca.

## Decisões deste plano (o contrato e o roadmap não fixam)

Escolhas de engenharia da sessão que escreveu; o dono pode mudar na leitura. As cinco primeiras mudam algo que ele ou o RH veem. **Depois da revisão de 2026-10-30, as fatias 1a, 1b e 1c seguem a mesma tabela de decisões comuns** (escopo da divergência por `operationId`; `allOf` no comparador e no validador; dono de cada peça de domínio; a porta `IPedidosParadosNoGestor`; texto dos 400; datas vindas do relógio; `STATUS.md`; fila e auditoria); o que toca este plano está nos itens abaixo e nas tarefas.

1. **Rotas de leitura da auditoria (tela 31) entram na 1a** (Task 9). Nenhum incremento do roadmap as citava (1b e 1c não têm a tela 31) e elas consomem direto o que a 1a produz.
2. **Contadores do menu** (`/api/{eu,gestor,rh}/contadores-do-menu`, que o contrato põe na fatia 7) entram na 1a (Task 12), com **cada contador somando as fontes que cada fatia registrar; sem fonte, vale 0** (o contrato não diz "null" aqui, ao contrário dos indicadores da ficha).
3. **O que gera evento de auditoria no acesso:** login bem-sucedido (`ACESSO`/`ENTROU`); senha certa com acesso desativado ou sem perfil (`ACESSO`/`ACESSO_NEGADO`, como o exemplo do contrato); **senha errada de usuário que existe** (`ACESSO`/`ACESSO_NEGADO`, **sem a senha, com o IP**, e **no máximo um evento por pessoa por minuto**, para a tentativa em massa contra uma conta não inundar o log); **403 por perfil errado** (`ACESSO_NEGADO`). **Não geram evento:** **e-mail inexistente** (não há pessoa; o banco exige funcionário para ator `PESSOA`, `V037`; e assim o log não guarda e-mail digitado errado) e 404 fora do escopo (para não inundar o log).
4. **A exportação do log entra no log como `ACESSO`/`BAIXOU`** ("Exportou o log de auditoria em CSV"): o contrato diz que entra, mas não diz como; não existe área "auditoria" no enum, e `BAIXOU` já fica de fora do filtro `apenasAlteracoes`.
5. **E-mail é entregue "pelo menos uma vez":** se o processo cair entre enviar e gravar `ENVIADO`, a reserva vence e a mensagem sai de novo (um e-mail duplicado raro; o provedor não dá idempotência no SMTP). A **vazão** (500 convites não saem juntos) é do processador (lote máximo por ciclo), não do espaçamento de `PROXIMA_TENTATIVA_EM` na inclusão que o `V035` sugere.
6. **Fuso fixo `-03:00`** (`FusoBrasil`, sem `TimeZoneInfo`): o Brasil não tem horário de verão (roadmap, decisão 4) e assim Windows e Linux dão o mesmo resultado.
7. **Autorização "fechada por omissão":** a política de reserva exige login; só `/api/publico`, as rotas públicas de `/api/auth`, `/health` e **o documento OpenAPI e a UI da documentação (quando `Documentacao:Habilitada`)** aceitam anônimo, e cada rota nova de negócio cai em uma família (`/api/rh`, `/api/gestor`, `/api/eu`, `/api/publico`).
8. **Fora do escopo é 404 porque a consulta já exclui** (nunca "buscar e depois conferir"); perfil errado é 403.
9. **Auditoria "opt-in":** só entra no log a propriedade marcada com `[Auditar(...)]`; coluna nova nunca vaza por esquecimento. Requisição **anônima** não gera evento automático (quem precisa registra explicitamente, com o ator informado).
10. **Erros do banco:** só `SQLSTATE 45000` (422 com `regraDoBanco`, como a fatia 0) e `1062` (409 genérico) têm tradução. `CHECK`, chave estrangeira e `NOT NULL` violados são **500** (a aplicação devia ter validado antes) e nunca vazam nome de tabela nem SQL.
11. **Resposta real conforme o contrato:** a Task 8 da fatia 0 deixou para esta fatia decidir se generaliza a conferência do JSON real. **Decidido: sim**, com um validador pequeno de resposta contra o schema resolvido do contrato (Task 7; trata `allOf`, que o contrato usa em toda lista paginada), usado por todo teste de rota nova.
12. **`STATUS.md` mantém a tabela por fatia** (é a que o front lê) **e ganha abaixo a seção "Rotas implementadas"** (uma linha por `operationId`, com a situação), porque a fatia 1 sozinha, "em andamento", não diz ao front qual rota já pode ser integrada. **Quem edita é a própria tarefa que entrega a rota**, no mesmo passo (o dono revisa no PR); `pronta` é só do dono. Um teste confere que a seção lista toda rota implementada (Task 7).
13. **Auditoria sem trigger de imutabilidade, por ora.** O log é "só inclusão" por regra da aplicação (nenhum caminho de código atualiza ou apaga evento); o banco não o impede, como o `V037` e o `LEIA-ME.md` decidiram (sem trigger em tabela nova da aplicação). A razão: um trigger de `UPDATE`/`DELETE` também impediria a limpeza de dados vencidos (política de retenção). O dono pode acrescentar os triggers depois, por **script novo**, se quiser a garantia no banco.
14. **Pedidos parados no gestor: uma porta, implementação vazia** (Task 13). O contrato fixa a etapa do pedido na criação (`CONVENCOES.md`, seção 9); então "passar ao RH" e "aprovar a etapa do gestor" são **ações explícitas** de quem tira o gestor do caminho (1b: efetivar o desligamento e a readmissão; 1c: trocar ou tirar o gestor do setor e ligar "Gestor ausente"). As tabelas de ponto e de férias são das fatias 3 e 7, então a 1a entrega a porta `IPedidosParadosNoGestor` com um **composto** padrão, vazio (ele soma as implementações que as fatias 3 e 7 registrarem: Task 13), e a falta fica registrada como dívida (seção "Para as fatias 2 em diante" e `STATUS.md`).

## Review Focus

1. **Gestor e a equipe:** pessoa de outro setor, o próprio gestor, quem ainda está em admissão (às 22h `-03:00` o dia UTC já virou) e quem foi inativado devolvem **404** (nunca 200 nem 403); gestor sem setor tem equipe vazia, não erro (Task 6).
2. **Auditoria que vaza:** nenhum evento com senha, hash ou token; coluna sensível sem antes nem depois; falha ao gravar o evento **desfaz** a alteração; o efeito de trigger não aparece sozinho; senha errada em massa contra uma conta gera um evento por minuto, e e-mail inexistente não deixa rastro (Tasks 8.1 e 8.2).
3. **Virada do dia:** evento às 23:30 `-03:00` (02:30 UTC do dia seguinte) entra no filtro do dia local, e "hoje" às 22h `-03:00` ainda é o dia local (Tasks 4 e 9).
4. **E-mail:** a mesma mensagem enfileirada duas vezes ou por duas transações ao mesmo tempo sai uma vez; 500 convites não saem juntos; processo que cai com mensagem reservada é retomado; destinatário recusado não trava a fila; **o lembrete de formulário anônimo nunca deixa endereço, pessoa nem erro com endereço na tabela, no log ou na auditoria** (Task 10).
5. **Listas:** busca com `%` e `_` procura o caractere literal, página além do fim devolve lista vazia com o `total` certo, `tamanhoPagina` 0 ou 101 e `ordenarPor` desconhecido devolvem 400, e empate na coluna de ordenação não repete nem pula item entre páginas (Task 4).

---

## Antes de começar

**Pré-requisito:** a fatia 0 está pronta (Tasks 1 a 9 passando; o plano dela é o de `2026-10-21-fatia-0-esqueleto.md` **como editado em 2026-10-30**). A fatia 1 **não espera o front da fatia 0 nem o primeiro deploy** (as Tasks 12 a 14 da fatia 0, que só começam com o front na `main`): pode começar com as Tasks 1 a 9 da fatia 0 passando. Rode `dotnet test back-end` e `docker compose up -d --wait` e confirme verdes antes da Task 1.1.

**Numeração das tarefas.** As Tasks 1 e 8 foram divididas em duas partes (1.1 e 1.2; 8.1 e 8.2) e há uma Task 13 ao fim (a porta dos pedidos parados no gestor), para manter os números das demais, que as fatias 1b e 1c citam. **Quem cita "Task 1" ou "Task 8" fala das duas partes**; a ordem de execução é 1.1, 1.2, 2, 3, 4, 5, 6, 7, 8.1, 8.2, 9, 10, 11, 12, 13.

**O que li** (arquivo e linha de 2026-10-30; se o arquivo mudou, vale o arquivo):

- Fatia 0, Task 5 (`2026-10-21-fatia-0-esqueleto.md`, "Domínio de autenticação, mapeamento do EF e teste de fumaça do provider"; **lida em 2026-10-30 às 19h27, com o arquivo ainda em edição: por isso cito a Task, não a linha**): mapeia só `NIVEIS_DE_ACESSO`, `CARGOS` (`ID_CARGO`, `NOME_CARGO`), `SETORES` (`ID_SETOR`, `NOME_SETOR`), `FUNCIONARIOS` (com `ID_CARGO` e `ID_SETOR`), `USUARIOS` e `REFRESH_TOKENS`; as tabelas dos `V030` a `V047` só aparecem num `DbContext` descartável da **sonda** da decisão 12 (calculadas `EM_ABERTO`, `VIGENTE` e `CHAVE_PENDENTE`, `DEFAULT (UTC_TIMESTAMP(6))`, padrões `FALSE`), "e cada fatia mapeia as suas"; **o texto dela não cita conversor de fuso nem `Kind` de `DateTime`** (busca por `Kind` e `ValueConverter`: nenhuma ocorrência), então a Task 1.1 desta fatia trata disso; a matrícula por `SEQUENCIAS` é SQL cru provado pela sonda e "o código que a usa é da fatia 1b". **Esta fatia mapeia de verdade as tabelas da lista do pedido da 1a (`V030` a `V038`) e põe o gerador de matrícula pronto (Task 2) para a 1b usar**; se a sonda mostrou que alguma técnica **não** funciona, ela vale mais que o que está escrito aqui.
- Fatia 0, Tasks 6 e 8: `IUnidadeDeTrabalho`, `IRelogio`, `ISenhaHasher`, `IEmissorDeTokens` em `RHSystem.Application.Autenticacao`; `ApiFactory` com relógio falso, rota de teste sob `/__teste/` com `ExcludeFromDescription()` e um elenco semeado por SQL; `ContratoDivergenciaTests` e `ComparadorDeContrato`, que compara **só as operações do conjunto de `operationId` que o teste mantém** (nunca prefixo de caminho; o comparador recebe o conjunto, une `properties` e `required` dos membros de `allOf` antes de comparar, e acusa operação do contrato que está no escopo e a API não tem; a Task 9 dela acrescenta `verificarSaude`; "cada tarefa acrescenta os `operationId` das rotas que entrega, no passo em que as implementa"), `LocalizadorDoContrato.CaminhoDoOpenApi()` e o leitor de OpenAPI da Task 8 (pacote `Microsoft.OpenApi`); a API declara todos os códigos de resposta do contrato (`WithName`, `Produces`, `ProducesProblem`); erro de trigger vira 422 com `regraDoBanco`; os textos de cada erro são os do contrato (a tabela de erros da Task 8). A Task 8 dela deixou **para esta fatia** decidir se generaliza a conferência do JSON real (Decisão 11).
- `plano-back-end.md` (a entrada do plano do back-end): a divergência é por escopo de `operationId`, e o `STATUS.md` é do dono, que revisa no PR a edição que a tarefa de cada rota faz (decisões 1 e 11 da tabela comum de 2026-10-30; sem número de linha, porque o arquivo ainda está em edição).
- Contrato: `CONVENCOES.md:19-30` (famílias de rota, 403 e 404), `:45-53` (listas e erros), `:114` (e-mails), `20-fatia-1-colaboradores.yaml:775-830` (equipe do gestor: ativos, admissão já começada, do setor dele, sem ele; fora do escopo é 404; gestor sem setor devolve equipe vazia), `:2155-2346` (auditoria), `70-fatia-7-gestao.yaml:2197-2268` (contadores do menu).
- **Medi** com um script descartável (Node) sobre `contrato/openapi.yaml` em 2026-10-30: 224 operações; **`x-perfis` segue a família em 224 de 224** (`/api/rh` só `RH`; `/api/gestor` só `Gestor`; `/api/eu` os três; `/api/publico` só `Publico`; `/api/auth/eu` os três; as outras rotas de `/api/auth` e `/health` `Publico`); **nenhuma resposta 2xx de `/api/gestor` tem** `cpf`, `rg`, `rgOrgaoEmissor`, `emailPessoal`, `endereco`, `matricula`, `beneficios`, `documentos`, `holerites` nem `telefone`.
- `V030` a `V038` (inteiros, com os `CHECK`): `FILA_EMAILS` exige `CARGA` nula fora de `PENDENTE` e `EM_ENVIO` e proíbe `SIGILOSO` com `ID_FUNCIONARIO` ou em situação final (`V035:116-134`); `TAREFAS_AGENDADAS` (`V036`); `AUDITORIA_EVENTOS` exige funcionário para ator `PESSOA` e canal `SISTEMA` para ator `SISTEMA` (`V037:106-125`); a trigger do grupo `TRG_FUNCIONARIOS_AFTER_UPDATE` muda `USUARIOS`, `BENEFICIOS_FUNCIONARIOS` e `FERIAS` ao inativar (`Criacao_Triggers:117-140`), efeito que o interceptor do EF não vê.

**O que suponho** (não medido; o teste indicado confirma ou derruba):

- Que o Pomelo devolve `DateTime` com `Kind = Unspecified` para `datetime(6)` (Task 1.1, teste de conformidade de fuso).
- Que `SELECT ... FOR UPDATE SKIP LOCKED` roda pelo `FromSql` do Pomelo (o EF embrulha o texto numa subconsulta, e é essa a dúvida) e que um `1062` dentro de uma transação não aborta a transação (comportamento do InnoDB: só o comando falha). **A Task 10 abre com uma sonda isolada que mede os dois (Step 0), diz o que fazer se não rodar, e a fila (Task 10) e o agendador (Task 11) só dependem da técnica depois de a sonda passar.**
- Que `JsonNamingPolicy.SnakeCaseUpper` gera os valores dos enums do contrato (`SEM_REGISTRO`, `ACESSO_NEGADO`) (Task 4).
- Que o nome e a forma do `ApiFactory`, de `IUnidadeDeTrabalho` e do conjunto de `operationId` do escopo de `ContratoDivergenciaTests` (aqui chamado "o conjunto do escopo") são os da fatia 0 editada. **Antes da Task 1.1, abra esses três e anote se algum nome mudou; use o nome real e mantenha o significado.**

## Estrutura de arquivos (o que esta fatia cria; caminhos sob `back-end/`)

- `src/RHSystem.Domain/`: `Autenticacao/UsuarioTokenSenha.cs`; `Colaboradores/Gestor.cs`, `DesligamentoAgendado.cs`; `Beneficios/Beneficio.cs`; `Auditoria/` (enums, `EventoDeAuditoria`, `MudancaDeAuditoria`, atributos); `Emails/EmailNaFila.cs`; `Agendamento/TarefaAgendada.cs`; `Importacoes/Importacao.cs`.
- `src/RHSystem.Application/`: `Comum/` (exceções, `FusoBrasil`, listas e `EnumsDeConsulta`, `CsvSeguro`, `ChaveDeIdempotencia`); `Escopo/`; `Auditoria/`; `Emails/`; `Agendamento/`; `Colaboradores/IGeradorDeMatriculas.cs`, `IPedidosParadosNoGestor.cs`, `PedidosAprovados.cs`, `PedidosParadosNoGestorComposto.cs`; `Painel/`.
- `src/RHSystem.Infrastructure/`: `Persistencia/` (configurações, conversores, extensões de consulta); `Escopo/`; `Auditoria/`; `Emails/`; `Agendamento/`; `Colaboradores/GeradorDeMatriculas.cs`.
- `src/RHSystem.Api/`: `Autorizacao/`; `Configuracao/` (erros, JSON, documentação anônima); `Auditoria/AuditoriaEndpoints.cs` e os objetos de consulta da auditoria; `Painel/ContadoresDoMenuEndpoints.cs`.
- `tests/RHSystem.Api.IntegrationTests/`: `Persistencia/`, `Escopo/`, `Matriz/`, `Contrato/`, `Auditoria/`, `Emails/`, `Agendamento/`, `Erros/`, `Listas/`, `Sondas/`; `Infra/ElencoDeTeste.cs`.

---

### Task 1.1: Conversores de fuso e de enum e a conformidade do modelo com o banco

(A Task 1 foi dividida em duas partes, 1.1 e 1.2, para cada uma caber numa sessão e fechar o seu ciclo de teste. A 1.1 vem primeiro: a 1.2 usa os conversores e é vigiada pelos testes de conformidade que esta escreve.)

**Files:**
- Create (Infrastructure, em `Persistencia/`): `ConversoresDeTempo.cs`; `EnumComoTextoMaiusculo.cs`
- Modify (Infrastructure): as configurações da fatia 0 que têm `DateTime` (`Usuario`: `DATA_HORA_ULTIMO_LOGIN`; `RefreshToken`: as datas dele), para declarar o fuso de cada uma
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Persistencia/ConformidadeDoModeloTests.cs`, `ConversoresDeTempoTests.cs`, `tabelas-sem-mapeamento.txt` (copiado para a saída do teste)

**Interfaces:**
- Consumes: `AppDbContext`, `Usuario`, `RefreshToken` e `MySqlFixture.NovoBancoComScriptsAsync()` (fatia 0, Tasks 4 e 5).
- Produces (Infrastructure):

```csharp
// RHSystem.Infrastructure.Persistencia
static class ConversoresDeTempo {
  static ValueConverter<DateTime, DateTime> Utc { get; }      // tabelas só da aplicação: grava exige Kind=Utc (senão lança); lê como Kind=Utc
  static ValueConverter<DateTime, DateTime> Brasil { get; }   // tabelas do grupo (DATETIME em -03:00): grava UTC - 3 h; lê + 3 h como Kind=Utc
}
class EnumComoTextoMaiusculo<TEnum> : ValueConverter<TEnum, string> where TEnum : struct, Enum   // SemRegistro <-> "SEM_REGISTRO"; texto desconhecido na leitura lança
```

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// ConversoresDeTempoTests (os três primeiros sem banco; o último com banco)
Utc_grava_so_DateTime_com_Kind_Utc_e_lanca_para_Local_e_Unspecified()
Utc_le_como_Kind_Utc_e_Brasil_grava_tres_horas_antes_e_le_tres_horas_depois_como_Kind_Utc()
EnumComoTextoMaiusculo_converte_SemRegistro_para_SEM_REGISTRO_e_de_volta_e_lanca_para_texto_desconhecido()
DataHoraUltimoLogin_e_gravada_tres_horas_antes_e_lida_de_volta_em_UTC()                   // relógio falso COM FRAÇÃO, 2026-10-07T12:42:00.700Z (a coluna é DATETIME sem fração, Criacao_Tabelas:202, e suponho que o MySQL arredonde em vez de truncar: este teste é quem prova): o SELECT cru confere 09:42:00 em -03:00 (decisão 4 do roadmap), nunca 09:42:01, porque Usuario.RegistrarLogin trunca ao segundo (regra da fatia 0, Task 5); lido de volta em UTC como 12:42:00Z; tabela USUARIOS, que a fatia 0 já mapeia

// ConformidadeDoModeloTests (genéricos: percorrem AppDbContext.Model e INFORMATION_SCHEMA; valem para TODA tabela que as fatias seguintes mapearem)
Coluna_gerada_no_banco_esta_mapeada_como_calculada_e_fora_do_INSERT_e_do_UPDATE()   // EXTRA contém GENERATED => ValueGenerated = OnAddOrUpdate e Before/AfterSaveBehavior = Ignore
Propriedade_calculada_no_modelo_corresponde_a_uma_coluna_gerada_no_banco()
Coluna_com_DEFAULT_de_expressao_NOT_NULL_esta_mapeada_como_gerada_na_inclusao()     // EXTRA = DEFAULT_GENERATED (DEFAULT (UTC_TIMESTAMP(6))) => ValueGenerated = OnAdd
Coluna_com_DEFAULT_constante_nao_declara_o_padrao_e_o_EF_sempre_envia_o_valor()       // COLUMN_DEFAULT não nulo e não expressão => ValueGenerated.Never, GetDefaultValue() e GetDefaultValueSql() nulos
Toda_propriedade_DateTime_mapeada_declara_o_fuso_com_ConversoresDeTempo_Utc_ou_Brasil()   // nenhuma DateTime "crua": a decisão de fuso é obrigatória em cada coluna nova
Toda_tabela_do_banco_esta_mapeada_ou_listada_em_tabelas_sem_mapeamento_e_nenhuma_listada_esta_mapeada()  // o arquivo .txt tem um nome por linha; o leitor aceita "# comentário" depois do nome (SEQUENCIAS  # só por SQL cru), linha só de comentário e linha vazia; avisa quando o grupo cria tabela nova
Leitor_de_tabelas_sem_mapeamento_aceita_comentario_depois_do_nome_e_ignora_linha_so_de_comentario_e_linha_vazia()   // "SEQUENCIAS  # só por SQL cru" lê o nome SEQUENCIAS; sem isso o Step 4 gravaria uma "tabela" chamada "SEQUENCIAS  # só por SQL cru"
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia"`
Expected: FAIL (conversores não definidos; o teste do fuso acusa as colunas `DateTime` da fatia 0 sem conversor).

- [ ] **Step 3: Implementar os dois conversores** com as assinaturas acima e declarar o fuso das colunas `DateTime` que a fatia 0 já mapeia (`USUARIOS.DATA_HORA_ULTIMO_LOGIN`: `Brasil`; `REFRESH_TOKENS`: `Utc`). Se a Task 5 da fatia 0 já converte, **reaproveite** o que ela fez e só garanta o teste. O conversor `Brasil` não arredonda nem trunca: quem trunca ao segundo é `Usuario.RegistrarLogin` (fatia 0, Task 5).
- [ ] **Step 4: Gerar `tabelas-sem-mapeamento.txt`.** Rode o teste `Toda_tabela_do_banco_esta_mapeada_ou_listada...`, copie os nomes que ele aponta para o arquivo (um por linha), e acrescente `SEQUENCIAS  # só por SQL cru (V030); ver Task 2` (o leitor aceita o comentário depois do nome: é o teste `Leitor_de_tabelas_sem_mapeamento...` do Step 1). Neste ponto a lista traz todas as tabelas que a fatia 0 não mapeia, inclusive as de `V030` a `V038`; a Task 1.2 tira as que ela mapeia, e cada fatia seguinte faz o mesmo com as suas.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia"`
Expected: PASS. **Se um teste de conformidade falhar numa tabela que a fatia 0 já mapeava, o erro é do mapeamento dela: corrija o mapeamento, não o teste.**

- [ ] **Step 6: Commit** (com autorização): `git add back-end; git commit -m "feat: conversores de fuso e de enum e conformidade do modelo com o banco"`

---

### Task 1.2: Mapear as tabelas novas (V030 a V038)

**Files:**
- Create (Domain): `Autenticacao/UsuarioTokenSenha.cs`; `Colaboradores/Gestor.cs`, `Colaboradores/DesligamentoAgendado.cs`; `Beneficios/Beneficio.cs`; `Auditoria/AuditoriaArea.cs`, `AuditoriaAcao.cs`, `TipoDeAtor.cs`, `CanalDeOrigem.cs`, `EventoDeAuditoria.cs`, `MudancaDeAuditoria.cs`; `Emails/EmailNaFila.cs`; `Agendamento/TarefaAgendada.cs`; `Importacoes/Importacao.cs`
- Modify (Domain): `Autenticacao/Usuario.cs`, `Funcionario.cs`, `Setor.cs`
- Create (Infrastructure, em `Persistencia/Configuracoes/`): `UsuarioTokenSenhaConfiguracao.cs`, `GestorConfiguracao.cs`, `DesligamentoAgendadoConfiguracao.cs`, `BeneficioConfiguracao.cs`, `EventoDeAuditoriaConfiguracao.cs` (inclui `MudancaDeAuditoria`), `EmailNaFilaConfiguracao.cs`, `TarefaAgendadaConfiguracao.cs`, `ImportacaoConfiguracao.cs`
- Modify (Infrastructure): `Persistencia/AppDbContext.cs` e as configurações de `Usuario`, `Funcionario`, `Setor`
- Test: `back-end/tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoDasTabelasNovasTests.cs` e `tabelas-sem-mapeamento.txt` (tira as tabelas mapeadas); `back-end/tests/RHSystem.Domain.Tests/Autenticacao/FuncionarioMatriculaTests.cs`, `UsuarioSenhaProvisoriaTests.cs`, `UsuarioTokenSenhaTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `Funcionario`, `Usuario`, `Setor`, `Cargo`, `Perfil`, `MySqlFixture.NovoBancoComScriptsAsync()` (fatia 0, Tasks 4 e 5); `ConversoresDeTempo` e `EnumComoTextoMaiusculo<T>` (Task 1.1); os testes de conformidade da Task 1.1 vigiam tudo o que esta task mapear.
- Produces (Domain; propriedades com setter privado, o EF as preenche):

```csharp
// RHSystem.Domain.Autenticacao
enum TipoDeTokenDeSenha { Convite, Redefinicao }                    // texto no banco: CONVITE, REDEFINICAO
class UsuarioTokenSenha { int Id; int UsuarioId; TipoDeTokenDeSenha Tipo; string TokenHash; DateTime CriadoEm;
  DateTime ExpiraEm; DateTime? UsadoEm; DateTime? InvalidadoEm;
  static UsuarioTokenSenha Criar(int usuarioId, TipoDeTokenDeSenha tipo, string tokenHash, DateTime agoraUtc, TimeSpan validade); }   // CriadoEm = agoraUtc; ExpiraEm = agoraUtc + validade
// Usuario ganha:      bool SenhaProvisoria; static Usuario Criar(Funcionario f, string senhaHash, bool senhaProvisoria = false); void DefinirSenha(string novoHash);  // zera SenhaProvisoria
// Funcionario ganha:  string? Matricula; int? CargoId; int? SetorId; void AtribuirMatricula(string matricula);  // 5 dígitos, senão ArgumentException
// Setor ganha:        int? GestorId   (só a propriedade: `Setor.DefinirGestor(int? idGestor)` é da 1b)
// RHSystem.Domain.Colaboradores
class Gestor { int Id; int FuncionarioId; }
enum SituacaoDoDesligamento { Agendado, Efetivado, Cancelado, Revertido }
class DesligamentoAgendado { int Id; int FuncionarioId; DateOnly UltimoDiaTrabalho; SituacaoDoDesligamento Situacao; DateTime CriadoEm;
  DateTime? EfetivadoEm; DateTime? CanceladoEm; DateTime? RevertidoEm;
  static DesligamentoAgendado Criar(int funcionarioId, DateOnly ultimoDiaTrabalho, DateTime agoraUtc); }
// RHSystem.Domain.Beneficios
class Beneficio { int Id; string Nome; string? Descricao; bool Opcional; bool AceitaDependentes; bool AceitaTrajeto; }
// RHSystem.Domain.Auditoria
enum AuditoriaArea { Acesso, Colaboradores, Documentos, Cadastros, Importacao, Ponto, Feriados, Ferias, Holerites, Advertencias, Avisos, Formularios, Treinamentos, Beneficios, Atestados }
enum AuditoriaAcao { Entrou, AcessoNegado, Criou, Alterou, Excluiu, Inativou, Registrou, PediuAjuste, Aprovou, Recusou, Publicou, EnviouEmail, Importou, Baixou }
enum TipoDeAtor { Pessoa, Sistema }          // persistido: PESSOA, SISTEMA
enum CanalDeOrigem { Portal, Sistema }       // persistido: PORTAL, SISTEMA
```

- Produces (Domain, entidades de infraestrutura; **os nomes abaixo são os que as Tasks 8 a 11 e os testes usam**):

| Entidade (`DbSet`) | Tabela | Propriedades (C#) |
|---|---|---|
| `EmailNaFila` (`EmailsNaFila`) | `FILA_EMAILS` | `long Id`, `string Tipo`, `string ChaveDeIdempotencia`, `int? ReferenciaId`, `int? FuncionarioId`, `string DestinatarioEmail`, `bool Sigiloso`, `string? Carga` (JSON), `SituacaoDoEmail Situacao`, `int Tentativas`, `DateTime ProximaTentativaEm`, `DateTime? ReservadoAte`, `string? UltimoErro`, `DateTime CriadoEm`, `DateTime? EnviadoEm` |
| `TarefaAgendada` (`TarefasAgendadas`) | `TAREFAS_AGENDADAS` | `long Id`, `string Tipo`, `string ChaveDeIdempotencia`, `int? ReferenciaId`, `DateOnly? DiaReferencia`, `string? Parametros` (JSON), `SituacaoDaTarefa Situacao`, `DateTime ExecutarApos`, `int Tentativas`, `DateTime? ReservadaAte`, `DateTime? UltimaTentativaEm`, `DateTime? ConcluidaEm`, `string? UltimoErro`, `DateTime CriadaEm` |
| `EventoDeAuditoria` (`EventosDeAuditoria`) | `AUDITORIA_EVENTOS` | `long Id`, `DateTime Quando`, `TipoDeAtor AtorTipo`, `int? FuncionarioAtorId`, `string AtorNome`, `Perfil? AtorPerfil`, `AuditoriaArea Area`, `AuditoriaAcao Acao`, `string Descricao`, `string RegistroAfetado`, `string? Entidade`, `long? IdRegistro`, `CanalDeOrigem Canal`, `string? Ip`, `List<MudancaDeAuditoria> Mudancas` |
| `MudancaDeAuditoria` (`MudancasDeAuditoria`) | `AUDITORIA_MUDANCAS` | `long Id`, `long EventoId`, `int Ordem`, `string Campo`, `string? ValorAntes`, `string? ValorDepois`, `bool ValorOculto` |
| `Importacao` (`Importacoes`) | `IMPORTACOES` | `int Id`, `int FuncionarioRhId`, `string NomeArquivo`, `SituacaoDaImportacao Situacao`, `int TotalLinhas`, `int LinhasValidas`, `int LinhasComErro`, `DateTime CriadaEm`, `DateTime ExpiraEm`, `string? CaminhoArquivo`, `string? Previa` (JSON), `DateTime? ConfirmadaEm`, `int? TotalCriados`, `int? ConvitesEnviados`, `int? IncluidosNoOnboarding`, `int? IncluidosNoFormularioAdmissao` |

  Os enums `SituacaoDoEmail { Pendente, EmEnvio, Enviado, Falhou, Devolvido, Cancelado }`, `SituacaoDaTarefa { Pendente, EmExecucao, Concluida, Falhou, Cancelada }` e `SituacaoDaImportacao { ComErros, Pronta, Confirmada }` moram junto da entidade. `Criar(...)` de cada entidade de infraestrutura aplica as invariantes dos `CHECK` correspondentes (nomes livres; uso interno da Infrastructure e dos testes) e **recebe o instante como parâmetro** (`DateTime agoraUtc`, que vem do `IRelogio`): nenhuma entidade lê o relógio da máquina.
- Produces (Infrastructure): `DbSet`s novos em `AppDbContext`: `UsuariosTokensSenha`, `Gestores`, `DesligamentosAgendados`, `Beneficios`, `EmailsNaFila`, `TarefasAgendadas`, `EventosDeAuditoria`, `MudancasDeAuditoria`, `Importacoes`.

**Cuidados de mapeamento** (as armadilhas do `LEIA-ME.md:63-80` e da decisão 12 do roadmap; cada linha tem o seu teste, na Task 1.1 (conformidade) ou no Step 1 desta):

| Coluna | Como mapear | Por quê |
|---|---|---|
| `CRIADO_EM` (`V033`, `V034`, `V035`), `PROXIMA_TENTATIVA_EM` (`V035`), `EXECUTAR_APOS` e `CRIADA_EM` (`V036`), `QUANDO` (`V037`), `CRIADA_EM` (`V038`) | `ValueGeneratedOnAdd()`, **sem** `HasDefaultValue` nem `HasDefaultValueSql`, com `ConversoresDeTempo.Utc`. **A aplicação sempre informa o valor, a partir do `IRelogio` (UTC)**; o `DEFAULT (UTC_TIMESTAMP(6))` do banco é só rede de segurança (se o código deixar a data em branco, o banco grava a hora real) | sem o `ValueGeneratedOnAdd` o EF manda `0001-01-01`; e o padrão do banco **não** serve em teste com relógio falso: o `CHECK` `EXPIRA_EM > CRIADO_EM` (`V033`) quebra se `CRIADO_EM` for a hora real e `EXPIRA_EM`, a do relógio falso |
| `EM_ABERTO` (`V033`), `VIGENTE` (`V034`) | **propriedade de sombra** `sbyte?` (`EmAberto`, `Vigente`; o `TINYINT` sem largura é `sbyte` no Pomelo, suposição que o round-trip confirma), `ValueGeneratedOnAddOrUpdate()` e `BeforeSaveBehavior`/`AfterSaveBehavior` = `Ignore` | coluna calculada: fora do `INSERT` e do `UPDATE` (o MySQL recusa com o erro 3105) |
| `USUARIOS.SENHA_PROVISORIA` (`V032`), `BENEFICIOS.OPCIONAL`, `ACEITA_DEPENDENTES`, `ACEITA_TRAJETO` (`V031`), `FILA_EMAILS.SIGILOSO`, `AUDITORIA_MUDANCAS.VALOR_OCULTO`, `SITUACAO`, `TENTATIVAS` | propriedade comum (`ValueGenerated.Never`), **sem declarar o padrão do banco**; a entidade sempre define o valor | o padrão do banco que o EF omite é armadilha (decisão 12 do roadmap) |
| `TIPO`, `SITUACAO`, `AREA`, `ACAO`, `ATOR_TIPO`, `CANAL`, `ATOR_PERFIL` | `EnumComoTextoMaiusculo<T>`; `ATOR_PERFIL` usa o texto do `Perfil` (`Funcionario`, `Gestor`, `RH`) | os `CHECK` comparam texto |
| `CARGA`, `PARAMETROS`, `PREVIA` | `string?` com `HasColumnType("json")`; a aplicação (de)serializa | o MySQL normaliza o JSON (ordem das chaves, espaços): comparar o JSON lido, não o texto |
| `SMALLINT` (`TENTATIVAS`, `TOTAL_LINHAS`...) | `int` no C# com `HasColumnType("smallint")` | |
| todo `DateTime` das tabelas da aplicação | `ConversoresDeTempo.Utc` (o `Brasil` só vale para tabela do grupo, e a `DATA_HORA_ULTIMO_LOGIN` já foi tratada na Task 1.1) | decisão 4 do roadmap |

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Domain.Tests
Funcionario_AtribuirMatricula_aceita_5_digitos_e_recusa_outro_formato()            // "00482" ok; "482", "ABCDE", "004821" -> ArgumentException
Usuario_Criar_com_senhaProvisoria_true_e_DefinirSenha_zera_a_marca()
UsuarioTokenSenha_Criar_usa_o_instante_recebido_CriadoEm_e_ExpiraEm_e_agora_mais_a_validade()   // o relógio vem de fora, nunca de DateTime.UtcNow

// Api.IntegrationTests/Persistencia/MapeamentoDasTabelasNovasTests (ida e volta no banco real; releia com OUTRO AppDbContext)
UsuarioTokenSenha_ida_e_volta_com_as_datas_do_relogio_em_UTC_e_EM_ABERTO_calculado()      // CRIADO_EM e EXPIRA_EM lidos = os do relógio falso (2026-10-07T12:42:00Z e + validade), Kind = Utc; EM_ABERTO = 1 e, depois de usar, NULL
Data_em_branco_cai_no_padrao_do_banco_em_UTC_so_como_rede_de_seguranca()                  // token com CriadoEm default e ExpiraEm = hora real + 1 h (com data do relógio falso o CHECK EXPIRA_EM > CRIADO_EM recusaria): CRIADO_EM dentro de 5 s do UTC_TIMESTAMP(6) lido do banco, Kind = Utc. A aplicação não usa este caminho (tabela de cuidados)
Segundo_token_em_aberto_do_mesmo_usuario_e_tipo_viola_a_chave_unica_e_depois_de_invalidar_o_primeiro_passa()
Usuario_grava_SenhaProvisoria_true_e_false_como_enviados()
DesligamentoAgendado_ida_e_volta_com_VIGENTE_calculado_e_segundo_vigente_viola_a_chave_unica()
EmailNaFila_ida_e_volta_com_JSON_com_acentos_e_chave_ASCII()                              // compara o JSON interpretado, não o texto
EmailNaFila_com_a_mesma_chave_de_idempotencia_viola_a_chave_unica_com_MySqlException_1062()
EmailNaFila_ENVIADO_com_CARGA_preenchida_viola_o_CHECK()                                  // prova que o CHECK vale pelo caminho do EF
TarefaAgendada_ida_e_volta_com_DIA_REFERENCIA_e_PARAMETROS()
EventoDeAuditoria_aceita_as_15_areas_e_as_14_acoes_e_devolve_QUANDO_com_Kind_Utc()
MudancaDeAuditoria_oculta_com_antes_e_depois_viola_o_CHECK()
Importacao_ida_e_volta_com_PREVIA_json_e_smallints()
Beneficio_grava_Opcional_AceitaDependentes_e_AceitaTrajeto_como_enviados()               // V031: true e false, o que o EF mandar é o que fica
Funcionario_com_Matricula_Setor_e_Gestor_do_setor_ida_e_volta()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~MapeamentoDasTabelasNovasTests|FullyQualifiedName~FuncionarioMatriculaTests|FullyQualifiedName~UsuarioSenhaProvisoriaTests|FullyQualifiedName~UsuarioTokenSenhaTests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar as entidades e as configurações** com as assinaturas e a tabela de cuidados acima. `Funcionario.CargoId` e `SetorId` são as chaves estrangeiras das navegações `Cargo` e `Setor` que a fatia 0 já tem: se já existirem, não repita.
- [ ] **Step 4: Tirar de `tabelas-sem-mapeamento.txt`** as tabelas de `V030` a `V038` que passaram a ser mapeadas (todas, menos `SEQUENCIAS`, que fica por SQL cru).
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia|FullyQualifiedName~Domain.Tests"`
Expected: PASS, inclusive os testes de conformidade da Task 1.1 sobre as tabelas novas. Se um round-trip mostrar que uma técnica da tabela acima não funciona no Pomelo (por exemplo, `ValueGeneratedOnAdd` ainda manda `0001-01-01`), ajuste a configuração e **anote o achado** na seção "Decisões" do `README.md` do back-end.

- [ ] **Step 6: Commit** (com autorização): `git add back-end; git commit -m "feat: mapeamento das tabelas novas (V030 a V038)"`

---

### Task 2: Matrícula pelo contador (`SEQUENCIAS`)

**Files:**
- Create: `src/RHSystem.Application/Colaboradores/IGeradorDeMatriculas.cs`; `src/RHSystem.Infrastructure/Colaboradores/GeradorDeMatriculas.cs`
- Modify: `src/RHSystem.Infrastructure/DependencyInjection.cs` (registrar como escopo da requisição)
- Test: `tests/RHSystem.Api.IntegrationTests/Colaboradores/GeradorDeMatriculasTests.cs`

**Interfaces:**
- Consumes: `AppDbContext`, `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync` (fatia 0, Task 6), `Funcionario.AtribuirMatricula` (Task 1.2).
- Produces:

```csharp
// RHSystem.Application.Colaboradores
interface IGeradorDeMatriculas {
  // Reserva `quantidade` (1 a 500) matrículas consecutivas, "00001" a "99999", na transação aberta do chamador.
  // Lança InvalidOperationException se não houver transação aberta ou se o contador passar de 99999.
  Task<IReadOnlyList<string>> ReservarAsync(int quantidade, CancellationToken ct);
}
```

- [ ] **Step 1: Escrever os testes que falham** (`NovoBancoComScriptsAsync()`; a técnica já foi provada pela sonda da fatia 0, Task 5: aqui ela vira produto)

```csharp
Reservar_1_devolve_00001_e_depois_00002()                                           // a linha MATRICULA nasce com ULTIMO_VALOR = 0 (V030)
Reservar_3_devolve_tres_matriculas_consecutivas_de_5_digitos_com_zero_a_esquerda()
Reservar_fora_de_transacao_lanca_InvalidOperationException()
Rollback_da_transacao_devolve_o_numero_e_nao_deixa_buraco()
Vinte_cadastros_simultaneos_recebem_vinte_matriculas_distintas_e_consecutivas()     // 20 tarefas, cada uma com DbContext, conexão e transação próprios, liberadas juntas por uma Barrier; cada uma grava um Funcionario com a matrícula (chave única)
Contador_acima_de_99999_lanca_InvalidOperationException_e_nao_grava_matricula_de_outro_tamanho()   // o teste põe ULTIMO_VALOR = 99999 por SQL
Matricula_informada_a_mao_nao_move_o_contador()                                     // documenta a decisão 3 do V030
Quantidade_zero_ou_acima_de_500_lanca_ArgumentOutOfRangeException()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter GeradorDeMatriculasTests`
Expected: FAIL (interface não definida).

- [ ] **Step 3: Implementar `GeradorDeMatriculas`.** Dois comandos **na conexão e na transação do `AppDbContext`** (`LEIA-ME.md:67`): `UPDATE SEQUENCIAS SET ULTIMO_VALOR = LAST_INSERT_ID(ULTIMO_VALOR + {n}) WHERE NOME = 'MATRICULA'` e `SELECT LAST_INSERT_ID()`; as `n` matrículas são de `valor - n + 1` até `valor`, formatadas com 5 dígitos. Linha `MATRICULA` ausente (0 linhas afetadas) lança `InvalidOperationException`. A linha do contador fica travada até o `COMMIT`: o chamador reserva **no fim** do caso de uso, logo antes de salvar, para a transação ser curta. Comentário `// AUDITORIA-OK: contador técnico, sem registro por decisão` no `UPDATE` (ver Task 8.2, guarda de `ExecuteUpdate`).
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: gerador de matriculas pelo contador SEQUENCIAS"`

---

### Task 3: Erros padrão do contrato

**Files:**
- Create: `src/RHSystem.Application/Comum/Excecoes.cs`; `src/RHSystem.Api/Configuracao/TextosDeErro.cs`
- Modify: `src/RHSystem.Api/Configuracao/TratamentoDeErros.cs` (da fatia 0, Task 8), `src/RHSystem.Api/Program.cs`
- Test: `tests/RHSystem.Application.Tests/Comum/ColetorDeErrosTests.cs`; `tests/RHSystem.Api.IntegrationTests/Erros/ErrosPadraoTests.cs`

**Interfaces:**
- Consumes: o tratamento global de exceções e a tradução do erro de trigger da fatia 0 (Task 8); `components.responses.Erro*` de `contrato/fontes/00-base.yaml:177-262`.
- Produces:

```csharp
// RHSystem.Application.Comum  (o caso de uso lança; a Api traduz)
sealed class NaoEncontradoException : Exception { NaoEncontradoException(string? detalhe = null); }                  // 404
sealed class ConflitoException : Exception { ConflitoException(string detalhe, string? codigo = null); }             // 409
sealed class ErroDeNegocioException : Exception { ErroDeNegocioException(string codigo, string detalhe); }          // 422, com `codigo` do contrato
sealed class ErroDeValidacaoException : Exception {                                                                 // 400, com `errors` por campo
  ErroDeValidacaoException(IReadOnlyDictionary<string, string[]> erros); ErroDeValidacaoException(string campo, string mensagem); }
sealed class ColetorDeErros { void Adicionar(string campo, string mensagem); bool TemErros { get; } void LancarSeHouver(); }
static class ObjetoOuNaoEncontrado { static T OuNaoEncontrado<T>(this T? objeto) where T : class; }                  // objeto ?? throw new NaoEncontradoException()
// RHSystem.Api.Configuracao
static class TextosDeErro { static string Titulo(int status); static string DetalhePadrao(int status); }            // os textos dos exemplos de 00-base.yaml, inclusive o do 400 (Erro400Validacao, contrato 0.2.1: title "Dados inválidos." e detail "Confira os campos indicados."); o do login (Erro400Login, "Preencha o e-mail e a senha.") é só do entrar, na fatia 0
static class ProblemasExtensions { static RouteHandlerBuilder ProduzProblemas(this RouteHandlerBuilder b, params int[] status); }  // ProducesProblem(status) em application/problem+json para cada código; toda rota nova a usa com os códigos do contrato
```

**Tradução (decidida; `campo` sempre em camelCase, como no corpo da requisição):**

| Origem | Status | `title` / `detail` | Extras |
|---|---|---|---|
| `NaoEncontradoException` | 404 | `Não encontrado.` / o detalhe dado ou `O registro não foi encontrado.` | |
| `ConflitoException`; `MySqlException` 1062 | 409 | `Conflito.` / o detalhe dado ou `Já existe um registro com estes dados.` | `codigo` se informado; no 1062 nunca o nome da chave nem o valor |
| `ErroDeNegocioException` | 422 | `Operação recusada.` / o detalhe | `codigo` |
| `ErroDeValidacaoException`, corpo JSON inválido, enum desconhecido, parâmetro que não converte | 400 | `Dados inválidos.` / **`Confira os campos indicados.`** (**só o `entrar` da fatia 0 usa `Preencha o e-mail e a senha.`**; todo outro 400 usa este texto e o mapa `errors`) | `errors` |
| `SQLSTATE 45000` (trigger do grupo) | 422 | como na fatia 0 | `regraDoBanco` |
| qualquer outro erro, inclusive `CHECK`, chave estrangeira e `NOT NULL` do banco | 500 | `Erro no servidor.` / `Erro no servidor. Tente novamente em instantes.` | registrado no log com a exceção; **nada de SQL nem de nome de tabela na resposta** |
| 401, 403, 429 e a resposta vazia de rota inexistente (404) | o status | os textos de `TextosDeErro` | 429 mantém `Retry-After`. Outros status que o framework gera (405, 415) não estão no contrato: ficam como o framework os gera |

Todo erro sai com `application/problem+json`, `traceId` (o `Activity.Current.Id` ou `HttpContext.TraceIdentifier`) e **sem stack trace**.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests
ColetorDeErros_acumula_campos_e_LancarSeHouver_lanca_ErroDeValidacao_com_todos()
ColetorDeErros_sem_erros_nao_lanca()
OuNaoEncontrado_devolve_o_objeto_e_lanca_NaoEncontrado_para_nulo()

// Api.IntegrationTests/Erros/ErrosPadraoTests (rotas de teste em /__teste/erros/*, AllowAnonymous e ExcludeFromDescription)
NaoEncontrado_vira_404_problem_json_com_title_e_detail_do_contrato()
Conflito_vira_409_e_leva_o_codigo_quando_informado()
ErroDeNegocio_vira_422_com_codigo_e_sem_regraDoBanco()
Validacao_vira_400_com_errors_por_campo_em_camelCase()                                    // title "Dados inválidos." e detail "Confira os campos indicados."
Corpo_json_invalido_vira_400_problem_json_em_portugues()                                  // idem: o mesmo title e o mesmo detail
Violacao_de_chave_unica_vira_409_generico_sem_nome_de_chave_nem_valor()                   // índice único de EMAIL_CORPORATIVO
Erro_de_trigger_continua_422_com_regraDoBanco()                                            // regressão da fatia 0
Violacao_de_CHECK_vira_500_e_a_resposta_nao_tem_nome_de_constraint_nem_SQL()              // CHK_FUNCIONARIOS_DATAS
Rota_inexistente_devolve_404_problem_json_com_title_e_detail_do_contrato()
Todo_erro_tem_traceId_e_nenhum_tem_stack_trace()                                           // 400, 404, 409, 422 e 500
Titulos_e_detalhes_padrao_sao_os_dos_exemplos_do_contrato()           // lê components.responses.Erro400Validacao..Erro500Interno do contrato (LocalizadorDoContrato): title e detail de TODOS iguais a TextosDeErro, inclusive o do 400 ("Confira os campos indicados."); Erro400Login ("Preencha o e-mail e a senha.") fica de fora: é só do entrar, e o teste da fatia 0 o confere
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~ColetorDeErrosTests|FullyQualifiedName~ErrosPadraoTests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as exceções, `TextosDeErro` (o texto **copiado dos `example`**, não reescrito, inclusive o `detail` do 400), `ProduzProblemas` e a tradução acima no tratamento global da fatia 0: um `IExceptionHandler`, `AddProblemDetails` com `CustomizeProblemDetails` (para o título e o `traceId` valerem também nos erros que o próprio framework gera) e `UseStatusCodePages` para as respostas vazias. Mantenha o que a Task 8 da fatia 0 já faz (`OnChallenge` do 401, trigger, 500 em `Production`).
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter AutenticacaoEndpointsTests`; Expected: PASS (a regressão da fatia 0 continua verde, inclusive o 400 de `entrar` com o texto próprio "Preencha o e-mail e a senha.").
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: erros padrao do contrato (404, 409, 422, 400 e 500)"`

---

### Task 4: Datas, JSON e listas do contrato (fuso, paginação, ordenação, busca, CSV)

**Files:**
- Create: `src/RHSystem.Application/Comum/Tempo/FusoBrasil.cs`; `Comum/Consultas/ParametrosDeLista.cs`, `RespostaPaginada.cs`, `MapaDeOrdenacao.cs`, `EnumsDeConsulta.cs`; `Comum/Exportacao/CsvSeguro.cs`; `src/RHSystem.Infrastructure/Persistencia/ConsultasEf.cs`; `src/RHSystem.Api/Configuracao/JsonDoContrato.cs`
- Modify: `src/RHSystem.Api/Program.cs`
- Test: `tests/RHSystem.Application.Tests/Comum/FusoBrasilTests.cs`, `ParametrosDeListaTests.cs`, `MapaDeOrdenacaoTests.cs`, `EnumsDeConsultaTests.cs`, `CsvSeguroTests.cs`; `tests/RHSystem.Api.IntegrationTests/Listas/ListasEfTests.cs`, `JsonDoContratoTests.cs`

**Interfaces:**
- Consumes: `ErroDeValidacaoException` (Task 3); `CONVENCOES.md:37-48`.
- Produces:

```csharp
// RHSystem.Application.Comum.Tempo   (deslocamento fixo -03:00)
static class FusoBrasil {
  static DateTime ParaLocal(DateTime utc);                  // exige Kind=Utc (ArgumentException); devolve Kind=Unspecified
  static DateTime ParaUtc(DateTime local);                  // inverso; devolve Kind=Utc
  static DateOnly Hoje(DateTime agoraUtc);                  // o dia de calendário em -03:00
  static DateOnly DiaLocalDe(DateTime instanteUtc);
  static DateTime InicioDoDiaEmUtc(DateOnly dia);           // 00:00 de -03:00, em UTC (03:00Z); [de, ate] inclusivo = InicioDoDiaEmUtc(de) .. InicioDoDiaEmUtc(ate.AddDays(1)) exclusivo
}
// RHSystem.Application.Comum.Consultas
enum DirecaoDeOrdenacao { Asc, Desc }                       // JSON ASC / DESC
sealed record ParametrosDeLista(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, string? OrdenarPor = null, DirecaoDeOrdenacao? Direcao = null) {   // [AsParameters] SÓ na rota cujo contrato declara os cinco (pagina, tamanhoPagina, busca, ordenarPor, direcao); a que declara outros (a auditoria: sem ordenarPor e direcao) tem um objeto de consulta próprio, só com o que o contrato declara, e monta new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado() para a paginação
  ParametrosDeLista Validado();                             // 400 com errors {pagina, tamanhoPagina, busca}; Busca só com espaços vira null; Trim
  int Pular { get; } }                                      // (Pagina - 1) * TamanhoPagina
record RespostaPaginada<T>(IReadOnlyList<T> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas) {
  static RespostaPaginada<T> De(IReadOnlyList<T> itens, int total, ParametrosDeLista p); }   // as respostas com campo extra herdam: record FuncionarioListaRespostaRh(..., Totais Totais) : RespostaPaginada<Item>(...)
sealed class MapaDeOrdenacao<T> {
  MapaDeOrdenacao(string padrao, DirecaoDeOrdenacao direcaoPadrao, Expression<Func<T, object?>> desempate);  // desempate = a chave única (id): paginação estável
  MapaDeOrdenacao<T> Por(string nome, Expression<Func<T, object?>> chave);
  IQueryable<T> Aplicar(IQueryable<T> consulta, ParametrosDeLista p);   // ordenarPor desconhecido: ErroDeValidacao "ordenarPor" listando os valores aceitos
}
static class EnumsDeConsulta {                              // liga ?area=ACESSO_NEGADO pelo nome do contrato: o minimal API sozinho só entende o nome do C# (AcessoNegado), e o front manda o do contrato
  static TEnum? Ler<TEnum>(string? texto, string campo) where TEnum : struct, Enum;   // nulo ou vazio => null; o nome do contrato (a mesma regra do JsonDoContrato: SnakeCaseUpper, e Perfil continua Funcionario, Gestor, RH) => o valor;
}                                                           //   qualquer outro texto, inclusive "AcessoNegado" => ErroDeValidacaoException(campo, "Valor inválido. Use um destes: ...") com os valores aceitos
// RHSystem.Application.Comum.Exportacao
static class CsvSeguro {
  static string Texto(string? valor);                       // neutraliza o que começa com = + - @ (e TAB/CR): prefixa '
  static string Numero(long valor);                         // NÃO neutraliza (saldo negativo continua número)
  static byte[] Escrever(IReadOnlyList<string> titulos, IEnumerable<IReadOnlyList<string>> linhas);   // UTF-8 com BOM, separador ;, aspas dobradas, CRLF
}
// RHSystem.Infrastructure.Persistencia
static class ConsultasEf {
  static Task<RespostaPaginada<TDto>> ToRespostaPaginadaAsync<T, TDto>(this IQueryable<T> q, ParametrosDeLista p, Expression<Func<T, TDto>> projecao, CancellationToken ct);
}
static class BuscaLike { static string Contem(string texto); }   // "%texto%" com \ % _ escapados; usar com EF.Functions.Like(coluna, BuscaLike.Contem(busca))
// RHSystem.Api.Configuracao
static class JsonDoContrato { static void Configurar(JsonSerializerOptions o); }   // ver abaixo
```

**JSON do contrato** (`CONVENCOES.md:35-41`): `camelCase`; enums em `MAIÚSCULAS_COM_SUBLINHADO` (`JsonNamingPolicy.SnakeCaseUpper`) **menos `Perfil`**, que continua `Funcionario`, `Gestor`, `RH` (o contrato fixa); instante `DateTime` só com `Kind=Utc` e terminando em `Z` (**`Kind` diferente de `Utc` lança**: assim um `datetime` que o Pomelo devolveu sem `Kind` quebra o teste em vez de sair sem `Z`); `DateOnly` `yyyy-MM-dd`; `TimeOnly` `HH:mm`; enum desconhecido na entrada vira 400; na entrada, instante com deslocamento (`-03:00`) é convertido para UTC (o `System.Text.Json` o devolveria como `Kind=Local`).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// FusoBrasilTests
ParaLocal_subtrai_tres_horas_e_ParaUtc_soma_tres_horas()
Hoje_as_02h30_UTC_ainda_e_o_dia_anterior_no_Brasil()                // 2026-10-08T02:30Z => 2026-10-07 (a virada do dia, Review Focus 3)
Hoje_as_03h00_UTC_ja_e_o_dia_novo()
InicioDoDiaEmUtc_de_2026_10_07_e_2026_10_07T03_00Z()
ParaLocal_com_Kind_diferente_de_Utc_lanca_ArgumentException()
// ParametrosDeListaTests
Padroes_sao_pagina_1_e_tamanho_20()
Pagina_menor_que_1_e_tamanho_0_ou_101_lancam_ErroDeValidacao_com_o_nome_do_campo()
Busca_acima_de_100_caracteres_lanca_e_busca_so_com_espacos_vira_null()
// MapaDeOrdenacaoTests (em memória, AsQueryable)
Ordena_pela_coluna_e_pela_direcao_pedidas_e_usa_o_padrao_sem_parametro()
OrdenarPor_desconhecido_lanca_ErroDeValidacao_listando_os_valores_aceitos()
Empate_na_coluna_ordena_pelo_desempate_e_as_paginas_nao_repetem_nem_pulam_itens()     // Review Focus 5
// EnumsDeConsultaTests
Ler_aceita_o_nome_do_contrato_ACESSO_NEGADO_e_devolve_AcessoNegado()
Ler_texto_nulo_ou_vazio_devolve_null()
Ler_o_nome_do_CSharp_ou_texto_desconhecido_lanca_ErroDeValidacao_com_o_campo_e_os_valores_aceitos()   // "AcessoNegado", "acesso_negado", "XYZ"
Ler_Perfil_aceita_Funcionario_Gestor_RH()
// CsvSeguroTests
Texto_que_comeca_com_igual_mais_menos_ou_arroba_ganha_aspas_simples_na_frente()       // "=1+1" => "'=1+1"; também TAB e CR iniciais
Numero_negativo_nao_e_neutralizado()                                                  // Numero(-225) == "-225"
Escrever_usa_BOM_UTF8_ponto_e_virgula_aspas_dobradas_e_CRLF()
// Api.IntegrationTests/Listas/ListasEfTests (banco; Setor ou Funcionario)
ToRespostaPaginadaAsync_devolve_total_totalPaginas_e_pagina_alem_do_fim_com_itens_vazios()   // Review Focus 5
Busca_com_percentual_e_sublinhado_procura_o_caractere_literal()                       // "100%" não casa "1000"; "a_b" não casa "axb"
Busca_ignora_maiusculas_e_acentos()                                                    // "jose" acha "José" (utf8mb4_unicode_ci)
// Api.IntegrationTests/Listas/JsonDoContratoTests (rota de teste)
Instante_UTC_sai_com_Z_e_instante_sem_Kind_Utc_lanca()
DateOnly_sai_yyyy_MM_dd_e_TimeOnly_sai_HH_mm()
Enum_sai_em_MAIUSCULAS_COM_SUBLINHADO_e_Perfil_continua_Funcionario_Gestor_RH()
Enum_desconhecido_na_entrada_devolve_400_com_errors()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~FusoBrasilTests|FullyQualifiedName~ParametrosDeListaTests|FullyQualifiedName~MapaDeOrdenacaoTests|FullyQualifiedName~EnumsDeConsultaTests|FullyQualifiedName~CsvSeguroTests|FullyQualifiedName~Listas"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** com as assinaturas acima. `ToRespostaPaginadaAsync`: conta, depois `Skip`/`Take` com a ordenação que o chamador já aplicou; `TotalPaginas` é o teto de `Total / TamanhoPagina` (0 quando `Total` é 0). `JsonDoContrato.Configurar` é chamado em `ConfigureHttpJsonOptions` no `Program.cs` e mantém o que a Task 8 da fatia 0 já fixa.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter AutenticacaoEndpointsTests` (regressão: `perfil` continua texto); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: fuso, json, listas e csv do contrato"`

---

### Task 5: Famílias de rota e perfis (autorização "fechada por omissão")

**Files:**
- Create: `src/RHSystem.Application/Escopo/IAtorAtual.cs`; `src/RHSystem.Api/Autorizacao/Politicas.cs`, `FamiliasDeRota.cs`, `ManipuladorDeAutorizacao.cs`, `AtorAtualHttp.cs`
- Modify: `src/RHSystem.Api/Autenticacao/AutenticacaoEndpoints.cs`, `Configuracao/AutenticacaoExtensions.cs`, `Configuracao/DocumentacaoExtensions.cs` (documento e UI anônimos), o `/health` (Task 9 da fatia 0), `Program.cs`
- Test: `tests/RHSystem.Api.IntegrationTests/Autorizacao/FamiliasDeRotaTests.cs`, `AtorAtualTests.cs`, `DocumentacaoAnonimaTests.cs`, `SondaDaPoliticaDeReservaTests.cs`

**Interfaces:**
- Consumes: o `JwtBearer` e os claims da fatia 0 (`sub`, `fid`, `email`, `nome`, `perfil` em texto: Task 7); `TextosDeErro` (Task 3); `CONVENCOES.md:19-30`.
- Produces:

```csharp
// RHSystem.Application.Escopo
enum TipoDeAtorAtual { Pessoa, Sistema, Anonimo }
interface IAtorAtual {                       // quem está agindo agora; escopo da requisição ou da tarefa em segundo plano
  TipoDeAtorAtual Tipo { get; }
  int? UsuarioId { get; }  int? FuncionarioId { get; }  Perfil? Perfil { get; }
  string Nome { get; }                       // "Sistema" ou "Anônimo" quando não é pessoa
  string? Ip { get; }                        // só em requisição
  CanalDeOrigem Canal { get; }               // Portal (requisição) ou Sistema (segundo plano)
}
// RHSystem.Api.Autorizacao
static class Politicas { const string Rh = "Rh"; const string Gestor = "Gestor"; const string Eu = "Eu"; }   // claim perfil = RH | Gestor | qualquer dos três
static class FamiliasDeRota {
  static RouteGroupBuilder GrupoRh(this IEndpointRouteBuilder app);       // "/api/rh",      Politicas.Rh
  static RouteGroupBuilder GrupoGestor(this IEndpointRouteBuilder app);   // "/api/gestor",  Politicas.Gestor
  static RouteGroupBuilder GrupoEu(this IEndpointRouteBuilder app);       // "/api/eu",      Politicas.Eu
  static RouteGroupBuilder GrupoPublico(this IEndpointRouteBuilder app);  // "/api/publico", AllowAnonymous
}
```

**Regras:** toda rota nova de negócio nasce em um destes quatro grupos. `/api/auth/*` e `/health` ficam por rota (`AllowAnonymous` nas públicas; `GET /api/auth/eu` com `Politicas.Eu`). A **política de reserva** (`FallbackPolicy`) exige usuário autenticado: rota fora dos grupos e sem `AllowAnonymous` é 401 para anônimo. **O documento OpenAPI (`/openapi/*`) e a UI da documentação ficam fora das famílias e anônimos** quando `Documentacao:Habilitada=true` (com `false` nem existem: o 404 é o esperado, e o Step 4b mede se a regra de reserva o deixa assim): `AllowAnonymous()` em ambos, e a regra de reserva os **exclui** (sem isso o `ContratoDivergenciaTests`, que lê o documento pelo `HttpClient` do `ApiFactory` sem login, receberia 401). Eles não são rotas de negócio: `RotasImplementadas` (Task 7) só vê `/api` e `/health`, e a matriz não os examina. `MapInboundClaims = false` no `JwtBearer`, para os claims chegarem com os nomes que o emissor grava. `ManipuladorDeAutorizacao` (`IAuthorizationMiddlewareResultHandler`) devolve o 403 como `ProblemDetails` com os textos do contrato; o 401 continua o `OnChallenge` da fatia 0. `AtorAtualHttp` lê o `HttpContext`: autenticado = `Pessoa`; sem login = `Anonimo`; **sem `HttpContext` (serviço em segundo plano) = `Sistema`, canal `Sistema`, sem IP**. O evento de auditoria do 403 entra na Task 8.2.

- [ ] **Step 1: Escrever os testes que falham** (rotas de teste dentro de cada grupo, por exemplo `FamiliasDeRota.GrupoRh().MapGet("/__teste/ping", ...)`, com `ExcludeFromDescription()`, registradas pelo mesmo mecanismo que a Task 8 da fatia 0 usou para a rota do erro de trigger)

```csharp
Anonimo_em_rota_de_cada_familia_recebe_401_problem_json_e_o_publico_passa()
Funcionario_em_rota_do_rh_recebe_403_problem_json_com_title_e_detail_do_contrato()
Gestor_em_rota_do_rh_e_Rh_em_rota_do_gestor_recebem_403()
Qualquer_perfil_passa_na_familia_eu()
Token_sem_perfil_ou_com_perfil_desconhecido_recebe_403_na_familia_eu()
Rota_fora_das_familias_exige_login_por_padrao()                         // /__teste/sem-familia, sem AllowAnonymous: 401 sem token
Rotas_publicas_da_fatia_0_continuam_anonimas_e_auth_eu_exige_login()     // entrar, renovar, sair, /health
// DocumentacaoAnonimaTests (ApiFactory com Documentacao:Habilitada=true, e outro com false)
Documento_openapi_e_UI_da_documentacao_respondem_200_ao_anonimo_com_a_reserva_ligada()    // o mesmo ApiFactory prova que /__teste/sem-familia continua 401: a exclusão vale só para o documento e a UI
Documento_openapi_e_UI_da_documentacao_somem_com_Documentacao_Habilitada_false()          // 404, não 401
IAtorAtual_em_requisicao_autenticada_le_usuario_funcionario_perfil_nome_e_ip()   // o teste injeta o IP com um middleware de teste
IAtorAtual_em_requisicao_sem_login_e_Anonimo_e_fora_de_requisicao_e_Sistema_sem_ip()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Autorizacao"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** com as assinaturas e as regras acima.
- [ ] **Step 4: Manter anônimos o documento OpenAPI e a UI da documentação.** Em `DocumentacaoExtensions`, `AllowAnonymous()` no `MapOpenApi()` e na UI (só dentro do `if (Documentacao:Habilitada)`); a regra de reserva não pode fechá-los. Prova: `DocumentacaoAnonimaTests` passando e o `ContratoDivergenciaTests` da fatia 0 ainda verde (ele lê o documento sem login).
- [ ] **Step 4b: Sonda da regra de reserva sobre o que não casa com rota (hipótese não medida).** A regra de reserva fecha por omissão toda **rota** sem política, e é isso que os testes do Step 1 provam. **Suposição, não medida:** o ASP.NET pode aplicá-la também à requisição que não casou com rota nenhuma, e então um `GET` anônimo em rota inexistente sairia 401 em vez de 404. Em `SondaDaPoliticaDeReservaTests`, com a reserva ligada, escrever duas sondas que **só anotam o status e o `Content-Type`** (sem afirmar nada): `Sonda_GET_anonimo_em_nao_existe_anota_o_status()` e `Sonda_GET_anonimo_em_openapi_v1_json_com_Documentacao_Habilitada_false_anota_o_status()`; registrar os dois resultados no `README.md` do back-end (seção "Decisões"). Depois rodar os testes que esperam 404 anônimo, **que ficam como estão**: `Rota_inexistente_devolve_404_problem_json_com_title_e_detail_do_contrato` (Task 3), `Documento_openapi_e_UI_da_documentacao_somem_com_Documentacao_Habilitada_false` (Step 1 acima) e `Documentacao_so_responde_quando_Documentacao_Habilitada_e_true` (fatia 0, Task 9).
  - **Se as sondas derem 404 e os três testes passarem:** nada a mudar.
  - **Se derem 401 e os testes falharem por isso, a saída pronta é:** (a) um `app.MapFallback(...)` com `AllowAnonymous()` e `ExcludeFromDescription()` que devolve o 404 `application/problem+json` com os textos de `TextosDeErro` (Task 3: `title` "Não encontrado.", o `detail` padrão do 404 e `traceId`); (b) conferir que toda rota `/__teste/` chamada sem login tem `AllowAnonymous()` (as de `/__teste/erros/*` já têm; a de erro de trigger da fatia 0 também, Task 8 dela). O `MapFallback` casa qualquer método: anotar no `README.md` o que passa a acontecer com método errado em rota que existe (405 do framework ou 404), que o contrato não lista.
  - Não registrar nenhum dos dois comportamentos como fato antes de rodar as sondas.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Autorizacao|FullyQualifiedName~AutenticacaoEndpointsTests|FullyQualifiedName~Configuracao|FullyQualifiedName~ErrosPadraoTests|FullyQualifiedName~ContratoDivergenciaTests"`
Expected: PASS (a regressão da fatia 0 inclui `/health`, as rotas públicas de `/api/auth` e a leitura anônima do documento).

- [ ] **Step 6: Commit** (com autorização): `git add back-end; git commit -m "feat: familias de rota, politicas por perfil e ator atual"`

---

### Task 6: Escopo por perfil (linhas)

**Files:**
- Create: `src/RHSystem.Application/Escopo/IEscopoDoUsuario.cs`; `src/RHSystem.Infrastructure/Escopo/EscopoDoUsuario.cs`; `src/RHSystem.Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs`
- Modify: `src/RHSystem.Infrastructure/DependencyInjection.cs` (escopo da requisição); `tests/RHSystem.Api.IntegrationTests/Infra/ApiFactory.cs`
- Test: `tests/RHSystem.Api.IntegrationTests/Infra/ElencoDeTeste.cs`; `Escopo/EscopoDoUsuarioTests.cs`, `EscopoNaApiTests.cs`

**Interfaces:**
- Consumes: `IAtorAtual` (Task 5), `IRelogio`, `FusoBrasil` (Task 4), `NaoEncontradoException` e `OuNaoEncontrado` (Task 3), `Funcionario.SetorId`, `Setor.GestorId`, `Gestor` (Task 1.2).
- Produces:

```csharp
// RHSystem.Application.Escopo
sealed record EquipeDoGestor(int GestorId, int SetorId, string SetorNome, int GestorFuncionarioId, DateOnly Hoje);
interface IEscopoDoUsuario {                  // "um filtro de escopo único" (spec, seção 4)
  int UsuarioId { get; } int FuncionarioId { get; } Perfil Perfil { get; } string Nome { get; }   // InvalidOperationException fora de requisição autenticada
  Task<EquipeDoGestor?> ObterEquipeAsync(CancellationToken ct);   // null se a pessoa não é Gestor de um setor; lida do banco, uma vez por requisição
}
// RHSystem.Infrastructure.Persistencia
static class EscopoDeEquipeExtensions {
  static IQueryable<Funcionario> DaEquipe(this IQueryable<Funcionario> q, EquipeDoGestor e);
  // a definição única da equipe (contrato, 20-fatia-1:785-788): ativos, DataAdmissao <= e.Hoje, do setor e.SetorId, sem o Id e.GestorFuncionarioId
}
```

**Como as fatias seguintes usam** (o padrão que a rota de teste desta task demonstra): o caso de uso do gestor recebe `EquipeDoGestor?` (`null` = lista vazia, ou 404 ao pedir uma pessoa), monta a consulta sobre `DaEquipe(e)` e termina com `.OuNaoEncontrado()`: **a consulta já exclui o que está fora do escopo, então "não existe" e "é de outro setor" dão a mesma resposta**. Para tabelas com `ID_FUNCIONARIO` (ponto, férias, advertências...), filtre por `db.Funcionarios.DaEquipe(e).Select(f => f.Id).Contains(x.FuncionarioId)`. Só o RH vê todas as linhas; o funcionário lê só as linhas dele (`escopo.FuncionarioId`), **nunca um id que veio da requisição** nas rotas `/api/eu`.

**Elenco de teste** (os mesmos nomes de `contrato/ELENCO.md`; hoje = `2026-10-07T12:42:00Z`, o dia dos exemplos): `Rh` Ana Souza (nível `RH`, sem setor); `Gestor` Carlos Ribeiro (nível `Funcionario Gestor`, linha em `GESTORES`, gestor do setor Comercial); `MembroA` Diego Farias e `MembroB` Júlia Nunes (Comercial, ativos, admitidos antes de hoje); `EmAdmissao` Rafael Torres (Comercial, admissão em 2026-10-13); `Inativo` Henrique Alves (Comercial, inativado); `GestorDoFinanceiro` Marina Costa; `DeOutroSetor` Bruno Lima (Financeiro); `GestorSemSetor` Paulo Vieira (nível de gestor, linha em `GESTORES`, sem setor). Ordem de inserção por causa dos triggers e das chaves circulares: funcionários sem setor → `GESTORES` → `SETORES` com o `ID_GESTOR` → `UPDATE FUNCIONARIOS SET ID_SETOR` → `UPDATE FUNCIONARIOS SET ID_GESTOR` → `Usuario` (hash com no mínimo 40 caracteres, só para funcionário ativo) → inativar `Inativo`. **`FUNCIONARIOS.ID_GESTOR`:** cada membro de um setor com gestor (`MembroA`, `MembroB`, `EmAdmissao`, `Inativo` e `DeOutroSetor`) grava o `ID_GESTOR` de `GESTORES` do gestor do setor dele, antes de inativar `Inativo`; o gestor do setor (o trigger do grupo não o deixa ser gestor de si mesmo, `Criacao_Triggers:75-84`), quem não tem setor e quem é do RH ficam com `NULL`. A ficha da 1b tira o `gestor` dessa coluna, e o select de divergência do `validacao_consistencia.sql` (fatia 0, Task 4) acusaria um elenco que não a grava.

```csharp
// tests/RHSystem.Api.IntegrationTests/Infra
sealed record PessoaDeTeste(int FuncionarioId, int UsuarioId, string Nome, string Email, Perfil Perfil);
sealed class ElencoDeTeste { PessoaDeTeste Rh, Gestor, MembroA, MembroB, EmAdmissao, Inativo, GestorDoFinanceiro, DeOutroSetor, GestorSemSetor; int SetorComercialId, SetorFinanceiroId; }
// ApiFactory ganha:
Task<ElencoDeTeste> SemearElencoAsync();     // uma vez por banco; fixa o relógio falso em 2026-10-07T12:42:00Z
HttpClient ClienteComo(PessoaDeTeste p);     // Bearer emitido pelo IEmissorDeTokens de verdade (sem passar pelo login)
HttpClient ClienteAnonimo();
```

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// EscopoDoUsuarioTests
Gestor_com_setor_tem_equipe_com_setor_gestorId_e_o_proprio_funcionarioId()
Gestor_sem_setor_devolve_equipe_nula_e_Funcionario_e_Rh_nao_tem_equipe()
DaEquipe_inclui_so_ativos_ja_admitidos_do_setor_sem_o_proprio_gestor()            // dentro: MembroA e MembroB; fora: EmAdmissao, Inativo, DeOutroSetor, o Gestor
DaEquipe_na_virada_do_dia_usa_o_dia_de_Brasilia()                                  // relógio 2026-10-08T02:30Z (07/10 23:30 em -03:00): quem entra em 08/10 ainda não está; quem entra em 07/10 está (Review Focus 1)
Gestor_que_mudou_de_setor_passa_a_ver_a_equipe_nova_sem_novo_login()               // troca SETORES.ID_GESTOR por SQL; mesma sessão
Gestor_com_dois_setores_usa_o_de_menor_id_e_registra_aviso_no_log()                // o banco permite; o contrato fala em um setor (a 1c impede o cadastro duplo)
IEscopoDoUsuario_fora_de_requisicao_autenticada_lanca_InvalidOperationException()
Elenco_semeado_grava_o_ID_GESTOR_do_gestor_do_setor_nos_membros_e_NULL_no_gestor_e_em_quem_nao_tem_setor()   // SELECT cru: nenhum funcionário com setor tem ID_GESTOR diferente do SETORES.ID_GESTOR do setor dele (a divergência que o validacao_consistencia.sql acusa), salvo o próprio gestor do setor, que fica NULL
// EscopoNaApiTests (rotas de teste /api/gestor/__teste/equipe e /api/gestor/__teste/equipe/{id}, que usam DaEquipe e OuNaoEncontrado)
Gestor_lista_so_a_propria_equipe()
Gestor_pede_pessoa_da_equipe_e_recebe_200()
Gestor_pede_pessoa_de_outro_setor_recebe_404_problem_json_do_contrato()           // Review Focus 1
Gestor_pede_a_si_mesmo_quem_esta_em_admissao_e_quem_esta_inativo_e_recebe_404()
Gestor_pede_id_inexistente_recebe_exatamente_o_mesmo_404_de_fora_do_escopo()      // title e detail iguais: não revela existência
Gestor_sem_setor_lista_equipe_vazia_e_recebe_404_ao_pedir_alguem()
Rh_na_rota_do_gestor_recebe_403_e_nao_404()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Escopo"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** `EscopoDoUsuario` (lê `IAtorAtual`; resolve o setor por `SETORES.ID_GESTOR` ligado a `GESTORES.ID_FUNCIONARIO` = o funcionário logado; `Hoje` = `FusoBrasil.Hoje(relogio.AgoraUtc)`), `DaEquipe`, `ElencoDeTeste` e os métodos novos do `ApiFactory`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: escopo por perfil e a equipe do gestor"`

---

### Task 7: Matriz de perfis, conformidade com o contrato e `STATUS.md`

**Files:**
- Create (testes, em `tests/RHSystem.Api.IntegrationTests/`): `Contrato/CarregadorDeContrato.cs`, `ContratoDaApi.cs`, `ValidadorDeRespostaDoContrato.cs`, `RespostaConformeAoContrato.cs`; `Matriz/MatrizDePerfisTests.cs`, `CamposDoRh.cs`; `Contrato/ContratoEStatusTests.cs`, `ValidadorDeRespostaDoContratoTests.cs`
- Modify: `Contrato/ContratoDivergenciaTests.cs` (só extrair o carregamento; sem mudar o que ele prova); `contrato/STATUS.md`

**Interfaces:**
- Consumes: `LocalizadorDoContrato.CaminhoDoOpenApi()`, `ComparadorDeContrato` e o leitor de OpenAPI da fatia 0 (Task 8); `ElencoDeTeste`, `ApiFactory.ClienteComo`/`ClienteAnonimo` (Task 6); famílias (Task 5).
- Produces (testes; **as fatias seguintes só chamam isto, não o reescrevem**):

```csharp
// tests/RHSystem.Api.IntegrationTests/Contrato
static class CarregadorDeContrato {                       // extraído do ContratoDivergenciaTests; o MESMO leitor para o contrato e para o documento gerado
  static OpenApiDocument Contrato();  static Task<OpenApiDocument> GeradoAsync(HttpClient clienteDoApiFactory); }
sealed record OperacaoDoContrato(string Metodo, string Caminho, string OperationId, IReadOnlyList<string> Perfis);   // Perfis = x-perfis
static class ContratoDaApi {
  static IReadOnlyList<OperacaoDoContrato> Operacoes { get; }
  static IReadOnlyList<(string Metodo, string Caminho)> RotasImplementadas(IServiceProvider sp);   // EndpointDataSource: só /api e /health, sem segmento __teste, sem restrição de rota ({id:int} vira {id}); o documento OpenAPI e a UI da documentação não entram
  static IReadOnlySet<string> OperationIdsImplementados(IServiceProvider sp);   // RotasImplementadas casadas com Operacoes por (Metodo, Caminho); rota sem operação no contrato fica de fora (a matriz a acusa)
}
static class ValidadorDeRespostaDoContrato { static IReadOnlyList<string> Validar(OpenApiSchema schemaResolvido, JsonElement json); }   // uma frase por problema; vazia = conforme
static class RespostaConformeAoContrato { static Task AfirmarConformeAoContratoAsync(this HttpResponseMessage resposta, string operationId); }  // valida o JSON REAL contra o schema da resposta com o status real
// tests/RHSystem.Api.IntegrationTests/Matriz
static class CamposDoRh { static readonly string[] Proibidos; static void AfirmarSemCamposDoRh(JsonElement json); static IReadOnlyList<string> ProibidosNoSchema(OpenApiSchema schemaResolvido); }
   // Proibidos = cpf, rg, rgOrgaoEmissor, emailPessoal, endereco, matricula, beneficios, documentos, holerites, telefone (a chave `telefoneMascarado` é outra e é permitida); procura em qualquer profundidade
   // ProibidosNoSchema percorre properties, items, allOf, oneOf e anyOf (o contrato usa allOf com Paginacao em toda lista paginada: ficar só em properties deixaria a varredura vazia sem avisar)
```

O **validador** é pequeno de propósito (decisão 11): tipo (aceitando `null` só onde o schema aceita), `required`, chaves que o schema não declara (quando não há `additionalProperties`), `enum`, `format: date-time` terminando em `Z`, `format: date` `yyyy-MM-dd`, itens de `array`, `oneOf` (vale se um ramo bater) e **`allOf`: une `properties` e `required` dos membros (resolvendo `$ref`) antes de validar** (o contrato usa `allOf` com `Paginacao` em toda lista paginada; sem isso `total`, `pagina` e `itens` ficariam como "chaves que o schema não declara"). Não valida `minLength`, `pattern` nem outras restrições de valor.

**Algoritmo da matriz:** para cada rota de `RotasImplementadas` (ids de caminho `2147483647`, corpo `{}` JSON, sem efeito colateral: se uma rota nova executar algo com isso, ela precisa validar antes e dar 400 ou 404): (1) tem de existir no contrato, senão a falha diz "rota fora do contrato"; (2) `x-perfis` com `Publico`: anônimo e os três perfis recebem status fora de 401 e 403; (3) senão, anônimo recebe 401 `problem+json`; cada perfil **em** `x-perfis` recebe status fora de 401, 403 e 5xx; cada perfil **fora** recebe 403 `problem+json` com os textos do contrato. As falhas são **todas coletadas** e o teste termina com um único `Assert.Empty` que lista "MÉTODO caminho, perfil: esperado X, veio Y".

**Formato novo do `contrato/STATUS.md`** (**a tabela por fatia que já existe fica como está**: é a que o front lê, e a linha da fatia 1 passa a `em andamento` na primeira entrega; **abaixo dela** acrescenta-se a seção "Rotas implementadas", e a coluna `operationId` é **a chave que o `STATUS_md_lista_toda_rota_implementada` lê**: o teste lê as linhas da tabela entre este `##` e o próximo `##`, então outras seções ao fim do arquivo, como "Pendências conhecidas" (Task 13), não o atrapalham). **Quem edita:** a própria tarefa que entrega a rota, no mesmo passo em que a implementa (o dono revisa no PR); `pronta` só o dono marca, depois de publicar no ambiente da demo.

```markdown
## Rotas implementadas

Uma linha por rota, com a mesma chave de `ROTAS.md`. `em andamento`: implementada e testada na branch; o dono passa a `pronta` quando publicar no ambiente da demo.

| Rota | operationId | Situação | Observação |
|---|---|---|---|
| `POST /api/auth/entrar` | entrar | em andamento | |
```

- [ ] **Step 1: Extrair** de `ContratoDivergenciaTests` o carregamento dos dois documentos para `CarregadorDeContrato` (sem mudar o que o teste prova) e deixar o **conjunto de `operationId` do escopo** acessível aos outros testes (`internal static`; o `Toda_rota_implementada_esta_no_conjunto_do_escopo_da_divergencia` o lê), e rodar `dotnet test back-end --filter ContratoDivergenciaTests`; Expected: PASS (nada mudou).
- [ ] **Step 2: Escrever os testes que falham**

```csharp
// MatrizDePerfisTests
Toda_rota_da_API_responde_a_cada_perfil_como_o_x_perfis_do_contrato()
Rota_real_fora_do_contrato_e_acusada_e_rota_de_teste_com_segmento___teste_e_ignorada()   // controle positivo: factory à parte com duas rotas falsas
// ContratoEStatusTests (estáticos sobre o contrato; sem banco, exceto o último)
X_perfis_do_contrato_segue_a_familia_da_rota()                                  // medido em 224 de 224; falha se o dono editar uma rota para fora do padrão
Respostas_2xx_das_rotas_do_gestor_no_contrato_nao_tem_campos_do_rh()             // CamposDoRh.ProibidosNoSchema no schema resolvido, em qualquer profundidade e dentro de allOf, oneOf, anyOf e items
Varredura_de_CamposDoRh_acha_campo_proibido_em_allOf_oneOf_items_e_propriedade_aninhada()   // controle positivo: schema sintético com "cpf" em cada um desses lugares; sem ele "nenhum campo achado" admitiria "a varredura não chega lá"
STATUS_md_lista_toda_rota_implementada_e_toda_rota_listada_existe_na_API()       // compara a coluna operationId da seção com OperationIdsImplementados (nos dois sentidos)
Toda_rota_implementada_esta_no_conjunto_do_escopo_da_divergencia()                // OperationIdsImplementados ⊆ o conjunto de operationId de ContratoDivergenciaTests: pega a tarefa que implementou a rota e esqueceu de acrescentá-la (e a regra inversa, operação do escopo que a API não tem, já é do comparador); **é a única prova desta regra**: as fatias 1b e 1c só acrescentam os `operationId` delas à lista, sem repetir o teste nem extrair a lista de novo
// ValidadorDeRespostaDoContratoTests
Validador_aceita_a_resposta_real_de_entrar()                                     // controle positivo com o contrato verdadeiro (login do RH semeado)
Validador_acusa_campo_a_mais_campo_faltando_tipo_errado_enum_invalido_e_data_sem_Z()
Validador_aceita_null_onde_o_schema_permite_e_acusa_onde_nao_permite()
Validador_trata_oneOf_com_null_como_o_contrato_de_cargo_e_setor()
Validador_trata_allOf_de_Paginacao_mais_itens()                                  // schema com allOf [Paginacao, {itens, required}]: aceita {itens, pagina, tamanhoPagina, total, totalPaginas}; acusa "total" faltando e "itens" faltando (o required dos dois membros vale); e uma chave que nenhum membro declara
```

- [ ] **Step 3: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~ValidadorDeRespostaDoContratoTests"`
Expected: FAIL (tipos não definidos; `STATUS.md` sem a seção).

- [ ] **Step 4: Implementar** os helpers acima e **atualizar `contrato/STATUS.md`** (esta tarefa edita; o dono revisa no PR) com a seção e as cinco rotas da fatia 0 (`POST /api/auth/entrar` `entrar`, `POST /api/auth/renovar` `renovarSessao`, `POST /api/auth/sair` `sair`, `GET /api/auth/eu` `obterUsuarioLogado`, `GET /health` `verificarSaude`), todas `em andamento`; a linha da fatia 0 da tabela por fatia **não muda** (o dono a passa a `pronta` no deploy).
- [ ] **Step 5: Rodar e ver passar.** Mesmo comando; Expected: PASS. **Se a matriz acusar uma rota da fatia 0** (por exemplo, `/api/auth/eu` aberta a anônimos), o erro é da API: corrija a rota, não o teste.
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "test: matriz de perfis, conformidade com o contrato e STATUS por rota"`

---

### Task 8.1: Auditoria: gravação (1 de 2): marcas, interceptor do EF e o auditor

(A Task 8 foi dividida em duas partes: a 8.1 entrega o mecanismo; a 8.2 liga os eventos de acesso e fecha as guardas. As fatias 1b e 1c citam "Task 8" para as duas.)

**Files:**
- Create (Domain): `Auditoria/AuditoriaAtributos.cs`
- Modify (Domain): `Autenticacao/Funcionario.cs`, `Setor.cs`, `Cargo.cs`; `Beneficios/Beneficio.cs` (marcas abaixo)
- Create (Application): `Auditoria/IAuditor.cs`
- Create (Infrastructure): `Auditoria/AuditoriaInterceptor.cs`, `Auditor.cs`, `ContextoDeAuditoria.cs`
- Modify (Infrastructure): `DependencyInjection.cs` (interceptor no `AddDbContext`; `AddDbContextFactory` para o contexto próprio do `Auditor`)
- Test: `tests/RHSystem.Domain.Tests/Auditoria/AtributosDeAuditoriaTests.cs`; `tests/RHSystem.Api.IntegrationTests/Auditoria/AuditoriaInterceptorTests.cs`, `AuditorTests.cs`

**Interfaces:**
- Consumes: entidades e enums da Task 1.2; `EnumComoTextoMaiusculo` (Task 1.1); `IAtorAtual` (Task 5); `IUnidadeDeTrabalho`; `IRelogio`.
- Produces:

```csharp
// RHSystem.Domain.Auditoria
[AttributeUsage(AttributeTargets.Class)]    sealed class AuditadoAttribute(AuditoriaArea area, string rotulo) : Attribute { AuditoriaArea Area; string Rotulo; }
[AttributeUsage(AttributeTargets.Property)] sealed class AuditarAttribute(string campo) : Attribute { string Campo; bool Sensivel { get; init; } }
interface IAuditavel { string NomeParaAuditoria { get; } string RegistroAfetado { get; } }     // "Bruno Lima" / "Colaborador Bruno Lima, matrícula 00482"
// RHSystem.Application.Auditoria
sealed record MudancaDeAuditoriaNova(string Campo, string? Antes, string? Depois, bool ValorOculto = false);
sealed record EventoDeAuditoriaNovo(AuditoriaArea Area, AuditoriaAcao Acao, string Descricao, string RegistroAfetado,
    string? Entidade = null, long? IdRegistro = null, IReadOnlyList<MudancaDeAuditoriaNova>? Mudancas = null);
sealed record AtorDoEvento(int FuncionarioId, string Nome, Perfil? Perfil);
interface IAuditor {
  void Registrar(EventoDeAuditoriaNovo evento);        // junta ao contexto da requisição: sai no próximo SalvarAsync, na mesma transação do fato (efeito de trigger, lote, ação de negócio)
  void RegistrarComo(AtorDoEvento ator, EventoDeAuditoriaNovo evento);   // como Registrar, com o ator informado, para requisição sem login (esqueci, definir): junta ao contexto da requisição e sai no próximo SalvarAsync, na MESMA conexão e transação do fato (regra abaixo)
  Task RegistrarEGravarAsync(EventoDeAuditoriaNovo evento, CancellationToken ct);                     // grava já, em contexto próprio (login, download, exportação, e-mail): não leva mudanças pendentes do chamador; só fora de transação (regra abaixo)
  Task RegistrarEGravarComoAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, CancellationToken ct);   // idem, com o ator informado (login: a requisição ainda é anônima); também só fora de transação
  Task<bool> RegistrarEGravarComLimiteAsync(AtorDoEvento ator, EventoDeAuditoriaNovo evento, TimeSpan janela, CancellationToken ct);
     // como o anterior, mas NÃO grava se o mesmo ator já tem evento da mesma área, ação e descrição dentro da janela (contada pelo IRelogio); devolve se gravou.
     // Conferir e gravar não são atômicos: dois pedidos no mesmo instante podem gravar dois eventos (é limite de ruído, não garantia)
  IDisposable SuspenderRegistroAutomatico();           // o interceptor não grava nada até o Dispose (a importação registra um evento só)
}
```

`Registrar` e `RegistrarEGravarAsync` lançam `InvalidOperationException` se o ator atual é `Anonimo` (quem está sem login informa o ator por `RegistrarComo`, `RegistrarEGravarComoAsync` ou `RegistrarEGravarComLimiteAsync`).

**Qual conexão grava o evento (regra única; as fatias 1b e 1c a seguem).** `AUDITORIA_EVENTOS` tem chave estrangeira para `FUNCIONARIOS` (`V037`, `ID_FUNCIONARIO_ATOR`), e o InnoDB, ao inserir a linha filha, pede trava compartilhada na linha pai (comportamento **lido, não medido aqui**; o teste de concorrência do `esqueci`, Task 5 da 1b, é a sonda). Se a transação do fato segura `FOR UPDATE` na linha da pessoa e o evento sai por **outra conexão**, essa segunda conexão espera o limite do InnoDB (50 s por padrão) e a requisição vira 500. Por isso **nunca se abre segunda conexão enquanto a transação do chamador segura trava na linha da pessoa**:
- **O fato de negócio persiste** (cadastro, convite, redefinição, definir senha, desligamento...): o evento é gravado **na mesma conexão e na mesma transação do fato**, por `Registrar` (ator da requisição) ou, com a requisição anônima, por `RegistrarComo` (ator informado). Se o evento falhar, o fato desfaz.
- **O evento deve sobreviver à falha** (senha errada, acesso negado, download, exportação, e-mail enviado): é gravado **depois** do commit ou do rollback, **fora de qualquer transação**, por `RegistrarEGravar*` (contexto próprio).
- **Guarda:** os três `RegistrarEGravar*` lançam `InvalidOperationException` ("Dentro de transação use Registrar ou RegistrarComo.") se o contexto da requisição tem transação aberta (`Database.CurrentTransaction`).

**Marcas desta fatia** (a 1b e a 1c marcam as propriedades novas que mapearem): `Funcionario` `[Auditado(Colaboradores, "Colaborador")]` com `Nome`, `EmailCorporativo`, `DataAdmissao`, `Ativo` ("Situação"), `Matricula`, `CargoId` ("Cargo"), `SetorId` ("Setor"); `Setor` e `Cargo` `[Auditado(Cadastros, ...)]` com o nome (e `GestorId`, "Gestor", no setor); `Beneficio` `[Auditado(Cadastros, "Benefício")]` com nome, descrição e as três marcas. **`Usuario.SenhaHash` e qualquer `Token*` nunca levam `[Auditar]`.**

**Regras do interceptor** (`AuditoriaInterceptor : SaveChangesInterceptor`):
1. Só entidade com `[Auditado]` e só propriedade com `[Auditar]`; `Sensivel = true` grava `ValorOculto = true` e **antes e depois nulos**.
2. Added = `Criou` (mudanças com antes nulo); Modified com propriedade auditada alterada = `Alterou`, ou `Inativou` se `Ativo` passou de `true` para `false`; Deleted = `Excluiu` (depois nulo); Modified só em propriedade não marcada = nada.
3. Valores em texto: `bool` "Sim"/"Não"; `DateOnly` `dd/MM/yyyy`; chave estrangeira para entidade `IAuditavel` grava o **`NomeParaAuditoria` da referenciada** ("Analista" → "Analista pleno"; consulta por chave, e se não achar grava o id).
4. `Descricao`: `Criou` = `"{Rotulo} {Nome}"` (`Setor Compras`); `Alterou` = `"{campos} de {Nome}"` (`Cargo de Bruno Lima`; vários campos separados por vírgula); `Inativou` = `"Inativação de {Nome}"`; `Excluiu` = `"{Rotulo} {Nome} excluído"`. `RegistroAfetado` vem de `IAuditavel`; `Entidade` é o nome da tabela; `IdRegistro` é a chave.
5. Ator pelo `IAtorAtual`: `Pessoa` = evento `PESSOA` com IP; `Sistema` = `SISTEMA`, canal `Sistema`, sem IP; **`Anonimo` = não registra**.
6. **Mesma transação da alteração** (abre uma se não houver). Como o id de uma entidade nova só existe depois do `INSERT`, o evento dela é gravado no `SavedChanges`, **dentro da mesma transação**; se gravar o evento falhar, a alteração também desfaz.
7. `SuspenderRegistroAutomatico` desliga o registro até o `Dispose`.
8. **`Quando` vem do `IRelogio` (UTC)**, em todo evento (do interceptor e do `Auditor`); nunca do `DEFAULT` do banco (que é só rede de segurança: Task 1.2, tabela de cuidados).

**Efeitos de trigger e operações em lote** (roadmap, decisão 5): o interceptor não os vê. A regra é **evento explícito** (`IAuditor.Registrar`) e **nenhum caminho de negócio usa `ExecuteUpdate`, `ExecuteDelete` ou `ExecuteSql` sem registrar**: o guarda da Task 8.2 exige, em cada chamada, um comentário `// AUDITORIA-OK: <motivo>` (até 3 linhas acima) dizendo onde o evento é registrado ou por que a tabela técnica não tem registro (fila, agendador, contador, refresh token). A decisão do dono de 2026-10-28 continua: os cancelamentos automáticos da inativação não têm registro próprio.

**O banco não impede alterar nem apagar evento (decisão 13 deste plano):** o log é "só inclusão" por regra da aplicação (nenhum caminho de código atualiza ou apaga evento), **sem trigger de imutabilidade**, como o `V037` e o `LEIA-ME.md` decidiram (sem trigger em tabela nova da aplicação). A razão: um trigger de `UPDATE`/`DELETE` também impediria a limpeza de dados vencidos (política de retenção). O dono pode acrescentar os dois triggers depois, por **script novo**, se quiser a garantia no próprio banco.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Domain.Tests
Nenhuma_propriedade_com_Auditar_se_chama_Senha_Hash_Token_ou_Segredo()             // reflexão em todas as classes do Domain
Toda_entidade_Auditado_implementa_IAuditavel()
// AuditoriaInterceptorTests (banco real, DbContext do DI com o interceptor e o relógio falso)
Criar_entidade_de_teste_gera_um_evento_Criou_com_ator_ip_entidade_id_e_descricao_Rotulo_Nome()   // entidade de teste [Auditado] (DbContext derivado só do teste, como no teste do campo sensível): Added = Criou, descrição "{Rótulo} {Nome}". O mesmo com Setor ("Setor Compras") está na Task 1 da 1c, que cria Setor.Criar
Alterar_chave_estrangeira_de_entidade_de_teste_grava_o_NOME_da_referenciada_e_nao_o_id()   // entidade de teste com chave estrangeira para outra entidade de teste IAuditavel: "Analista" -> "Analista pleno". O mesmo com Funcionario e Cargo (descrição "Cargo de Bruno Lima") está na Task 1 da 1b, que cria Funcionario.AtualizarCadastro
Alterar_so_propriedade_nao_marcada_nao_gera_evento()
Ativo_de_true_para_false_gera_Inativou()
Excluir_gera_Excluiu_com_depois_nulo()                                                    // Setor inserido por SQL e removido pelo DbContext: nenhuma operação de domínio que só nasce depois
Campo_sensivel_grava_ValorOculto_sem_antes_nem_depois()                                   // entidade de teste: DbContext derivado só do teste, com tabela criada pelo próprio teste
Entidade_sem_Auditado_nao_gera_evento()                                                   // RefreshToken
Falha_ao_gravar_o_evento_desfaz_a_alteracao()                                             // entidade de teste com rótulo e nome vazios: a descrição fica vazia, o CHECK do evento recusa, e a alteração também não pode ficar (Review Focus 2)
Requisicao_anonima_nao_gera_evento_automatico()
Execucao_em_segundo_plano_grava_ator_SISTEMA_canal_SISTEMA_sem_ip()
SuspenderRegistroAutomatico_nao_grava_ate_o_Dispose()
Evento_grava_QUANDO_do_relogio_falso_e_nao_o_do_banco()                                   // relógio fixo em 2026-10-07T12:42:00Z: QUANDO lido = esse valor, Kind = Utc
Efeito_de_trigger_nao_aparece_no_interceptor()                                            // inativar funcionário com usuário: evento do Funcionario existe; USUARIOS.ATIVO ficou false SEM evento do usuário (mede o buraco que justifica o evento explícito)
// AuditorTests
Registrar_junta_o_evento_ao_SalvarAsync_da_mesma_transacao_e_some_com_o_rollback()
RegistrarComo_em_requisicao_anonima_junta_o_evento_ao_SalvarAsync_da_mesma_transacao_com_o_ator_informado_e_some_com_o_rollback()   // a mesma conexão do fato: nenhuma segunda conexão
RegistrarEGravar_dentro_de_transacao_aberta_lanca_InvalidOperationException_nas_tres_variantes_e_fora_dela_grava()                // a regra única acima
RegistrarEGravar_grava_em_contexto_proprio_sem_levar_mudancas_pendentes_do_chamador()
RegistrarEGravarComo_grava_com_o_ator_informado_e_o_ip_da_requisicao()
RegistrarEGravarComLimite_grava_o_primeiro_nao_grava_o_repetido_dentro_da_janela_e_grava_de_novo_depois_dela()   // relógio falso avança; outro ator, outra ação ou outra descrição na mesma janela grava
Registrar_com_ator_Anonimo_e_sem_ator_informado_lanca_InvalidOperationException()
Evento_com_descricao_vazia_viola_o_CHECK_e_lanca()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~AuditoriaInterceptorTests|FullyQualifiedName~AuditorTests|FullyQualifiedName~AtributosDeAuditoriaTests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** atributos, marcas, `IAuditor`, `Auditor` (o `Registrar` usa o `AppDbContext` da requisição; os `RegistrarEGravar*` usam um contexto novo da `IDbContextFactory`; ao registrar `AddDbContext` e `AddDbContextFactory` juntos, os dois ficam com o mesmo `ServiceLifetime` (`Scoped`), senão o `DbContextOptions` entra em conflito) e `AuditoriaInterceptor` (o `SaveChanges` interno do `SavedChanges` precisa de uma trava contra reentrada). Todo instante vem do `IRelogio`.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end` inteiro (o interceptor está em todos os `SaveChanges` das fatias 0 e 1a); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: auditoria (interceptor do EF e auditor)"`

---

### Task 8.2: Auditoria: gravação (2 de 2): eventos de acesso, enums do contrato e guarda de `ExecuteUpdate`

**Files:**
- Modify (Application): `Autenticacao/EntrarCasoDeUso.cs`
- Modify (Api): `Autorizacao/ManipuladorDeAutorizacao.cs` (evento do 403)
- Modify (Infrastructure): `Autenticacao/RefreshTokenRepositorio.cs` (comentário `AUDITORIA-OK`); `Colaboradores/GeradorDeMatriculas.cs` já o tem (Task 2): o guarda confere
- Test: `tests/RHSystem.Application.Tests/Autenticacao/EntrarCasoDeUsoAuditoriaTests.cs`; `tests/RHSystem.Api.IntegrationTests/Auditoria/AuditoriaDeAcessoTests.cs`, `EnumsDeAuditoriaTests.cs`, `GuardaDeExecuteUpdateTests.cs`

**Interfaces:**
- Consumes: `IAuditor`, `AtorDoEvento`, `EventoDeAuditoriaNovo` (Task 8.1); `AuditoriaArea`, `AuditoriaAcao` (Task 1.2); `IAtorAtual` e `ManipuladorDeAutorizacao` (Task 5); `CarregadorDeContrato` (Task 7).
- Produces: nenhuma assinatura nova (os eventos de acesso e o guarda são comportamento e teste).

**Eventos de acesso (explícitos; decisão 3 deste plano).** Todos saem por `RegistrarEGravar*`, **depois** de salvar o último login e o refresh token e **fora de transação** (regra da Task 8.1, "Qual conexão grava o evento"):
- Login bem-sucedido = `RegistrarEGravarComoAsync(ator, Acesso/Entrou, "Entrou no portal", "Colaborador {nome}")`.
- Senha certa com acesso desativado = `Acesso/AcessoNegado`, descrição `Tentativa de login com usuário bloqueado` (o exemplo do contrato, `20-fatia-1:2250-2251`); senha certa sem perfil configurado = `AcessoNegado`, `Tentativa de login sem perfil de acesso configurado`.
- **Senha errada de usuário que existe** = `RegistrarEGravarComLimiteAsync(ator = a pessoa dona do e-mail, Acesso/AcessoNegado, "Tentativa de login com senha incorreta", "Colaborador {nome}", janela = 1 minuto)`: **com o IP, sem a senha**, e no máximo **um evento por pessoa por minuto** (a tentativa em massa contra uma conta não inunda o log).
- **Login de quem existe mas ainda tem senha provisória** (convite não aceito, `Usuario.SenhaProvisoria`; só a 1b, Task 4, passa a tratar esse login como e-mail inexistente na verificação da senha) **gera o mesmo evento** da senha errada de usuário que existe (`RegistrarEGravarComLimiteAsync`, `Acesso/AcessoNegado`, "Tentativa de login com senha incorreta", ator = a pessoa, **no máximo um por pessoa por minuto**) e a **mesma resposta 401 genérica**. O teste fica na 1b (Task 4), onde o comportamento nasce.
- **E-mail inexistente = nenhum evento** (não há pessoa, o `V037` exige funcionário para ator `PESSOA`, e assim o log não guarda e-mail digitado errado).
- O 403 por perfil errado grava `Acesso/AcessoNegado`, descrição `Acesso negado: {MÉTODO} {rota do contrato}` (a rota com `{parametro}`, nunca o valor), ator = a pessoa logada (`RegistrarEGravarAsync`: a requisição não salva nada).

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests (falsos em memória)
Entrar_bem_sucedido_registra_Acesso_Entrou_com_o_ator_informado()
Entrar_com_acesso_desativado_ou_sem_perfil_registra_AcessoNegado()
Entrar_com_senha_errada_de_usuario_que_existe_registra_AcessoNegado_pelo_metodo_com_limite_de_1_minuto()
Entrar_com_email_inexistente_nao_registra_nada()
// AuditoriaDeAcessoTests (ApiFactory)
Login_bem_sucedido_grava_Acesso_Entrou_com_o_ator_e_o_ip()
Senha_certa_com_acesso_desativado_ou_sem_perfil_grava_Acesso_AcessoNegado()
Senha_errada_de_usuario_que_existe_grava_AcessoNegado_com_ip_sem_a_senha_e_so_um_evento_por_minuto_por_pessoa()   // 3 erros no mesmo minuto (relógio falso) = 1 evento; o relógio avança 61 s e o quarto grava o segundo; outra pessoa no mesmo minuto grava o dela (Review Focus 2)
Email_inexistente_nao_grava_evento_nem_deixa_o_email_digitado_em_lugar_nenhum_do_log()                            // varre ATOR_NOME, DESCRICAO, REGISTRO_AFETADO e as mudanças atrás do e-mail digitado
Perfil_errado_em_rota_gera_AcessoNegado_com_metodo_e_rota_do_contrato_e_404_fora_do_escopo_nao_gera()
Evento_de_login_nunca_contem_senha_hash_nem_token()                                       // inclusive o de senha errada; varre ATOR_NOME, DESCRICAO, REGISTRO_AFETADO e todas as mudanças
// EnumsDeAuditoriaTests
AuditoriaArea_e_AuditoriaAcao_tem_os_mesmos_valores_dos_enums_do_contrato()               // components.schemas.AuditoriaArea e AuditoriaAcao, lidos pelo CarregadorDeContrato, comparados em SnakeCaseUpper
// GuardaDeExecuteUpdateTests (varre os .cs de back-end/src)
Todo_ExecuteUpdate_ExecuteDelete_e_ExecuteSql_tem_o_comentario_AUDITORIA_OK_com_o_motivo()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~EntrarCasoDeUsoAuditoriaTests|FullyQualifiedName~AuditoriaDeAcessoTests|FullyQualifiedName~EnumsDeAuditoriaTests|FullyQualifiedName~GuardaDeExecuteUpdateTests"`
Expected: FAIL (sem evento nas rotas de acesso; o guarda falha nas chamadas que ainda não têm o comentário).

- [ ] **Step 3: Implementar** a mudança em `EntrarCasoDeUso` (ganha `IAuditor`), o evento no `ManipuladorDeAutorizacao` e os comentários `AUDITORIA-OK` (em `RefreshTokenRepositorio.TentarRevogarAsync`: `tabela técnica, sem registro por decisão`; em `GeradorDeMatriculas`: idem, já feito na Task 2).
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end` inteiro; Expected: PASS. As Tasks 10 e 11 acrescentam `ExecuteUpdate` e `ExecuteDelete` e cada uma já leva o comentário no passo que o escreve (o guarda roda de novo nelas).
- [ ] **Step 5: Commit** (com autorização): `git add back-end; git commit -m "feat: eventos de acesso na auditoria e guarda de ExecuteUpdate"`

---

### Task 9: Auditoria: consulta, detalhe e exportação (`GET /api/rh/auditoria`, `.../exportacao`, `.../{eventoId}`)

**Files:**
- Create (Application): `Auditoria/AuditoriaDtos.cs`, `IConsultaDeAuditoria.cs`, `ListarEventosDeAuditoriaCasoDeUso.cs`, `ObterEventoDeAuditoriaCasoDeUso.cs`, `ExportarAuditoriaCasoDeUso.cs`, `RotulosDeAuditoria.cs`
- Create (Infrastructure): `Auditoria/ConsultaDeAuditoria.cs`
- Create (Api): `Auditoria/AuditoriaEndpoints.cs`, `FiltrosDaListaDeAuditoria.cs`, `FiltrosDaExportacaoDeAuditoria.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (o conjunto de `operationId` do escopo ganha `listarEventosDeAuditoria`, `exportarAuditoria` e `obterEventoDeAuditoria`), `contrato/STATUS.md`
- Test: `tests/RHSystem.Api.IntegrationTests/Auditoria/AuditoriaConsultaTests.cs`, `RotulosDeAuditoriaTests.cs`

**Interfaces:**
- Consumes: `IAuditor` (Task 8.1); `EventoDeAuditoria` (Task 1.2); `EnumsDeConsulta`, `ErroDeValidacaoException`, `ColetorDeErros` (Tasks 4 e 3); `ParametrosDeLista`, `RespostaPaginada`, `FusoBrasil`, `CsvSeguro`, `BuscaLike`, `ToRespostaPaginadaAsync` (Task 4); `GrupoRh()` (Task 5); `ProduzProblemas` (Task 3); `AfirmarConformeAoContratoAsync` (Task 7).
- Produces (rotas; `operationId`, parâmetros e códigos **do contrato**, `20-fatia-1-colaboradores.yaml:2155-2346`; só o perfil `RH`):
  - `GET /api/rh/auditoria` (`listarEventosDeAuditoria`) → `200 AuditoriaListaResposta` | `400` | `401` | `403` | `500`; parâmetros `pagina`, `tamanhoPagina`, `busca`, `de`, `ate`, `area`, `acao`, `apenasAlteracoes` (e **só** esses: o contrato não declara `ordenarPor` nem `direcao` aqui, a ordem é fixa)
  - `GET /api/rh/auditoria/exportacao` (`exportarAuditoria`) → `200 text/csv` | `400` | `401` | `403` | `500`; os mesmos filtros, **sem** paginação e **sem** `apenasAlteracoes`
  - **Objetos de consulta próprios** (decisão 8 da tabela comum): `FiltrosDaListaDeAuditoria(int Pagina = 1, int TamanhoPagina = 20, string? Busca = null, DateOnly? De = null, DateOnly? Ate = null, string? Area = null, string? Acao = null, bool ApenasAlteracoes = false)` e `FiltrosDaExportacaoDeAuditoria(string? Busca, DateOnly? De, DateOnly? Ate, string? Area, string? Acao)`, ambos com `[AsParameters]`. **Não** se usa `[AsParameters] ParametrosDeLista` aqui (ele traria `ordenarPor` e `direcao`, que o contrato não tem). A rota monta a paginação com `new ParametrosDeLista(Pagina, TamanhoPagina, Busca).Validado()` (na exportação, só para validar `busca`), e lê `Area` e `Acao` com `EnumsDeConsulta.Ler<AuditoriaArea>(f.Area, "area")` e `Ler<AuditoriaAcao>(f.Acao, "acao")` (Task 4: `?area=ACESSO_NEGADO` pelo nome do contrato), juntando os erros de `area`, `acao` e `de`/`ate` num `ColetorDeErros`.
  - `GET /api/rh/auditoria/{eventoId}` (`obterEventoDeAuditoria`, `eventoId` `int64`) → `200 AuditoriaEventoDetalhe` | `401` | `403` | `404` | `500`
  - Tipos de resposta com **os nomes dos schemas do contrato**: `AuditoriaEvento`, `AuditoriaAtor`, `AuditoriaListaResposta` (herda `RespostaPaginada<AuditoriaEvento>`), `AuditoriaOrigem`, `AuditoriaMudanca`, `AuditoriaEventoDetalhe` (`20-fatia-1:3339-3402`).
  - `static class RotulosDeAuditoria { static string Area(AuditoriaArea a); static string Acao(AuditoriaAcao a); static string Perfil(Perfil? p); static string Origem(CanalDeOrigem c); }` com os rótulos do contrato (`:3316-3341` e `:3376`): `Funcionario` aparece como "Colaborador"; origem `Portal` = "Portal no navegador", `Sistema` = "Automático".

**Regras** (do contrato): ordem fixa do mais recente ao mais antigo (`Quando`, depois `Id`, ambos decrescentes). **Período** `de` e `ate` são **dias** em `-03:00`, inclusivos; padrão `ate` = hoje em Brasília e `de` = `ate` menos 6 dias; `de` maior que `ate` = 400 em `errors.de`; a consulta usa `FusoBrasil.InicioDoDiaEmUtc(de)` até `InicioDoDiaEmUtc(ate.AddDays(1))` (exclusivo). `busca` procura em `AtorNome` e `Descricao` (`BuscaLike`). `apenasAlteracoes` tira `Entrou`, `AcessoNegado`, `Baixou` e `Ponto`+`Registrou`. `ator.desligado` vem da ficha **de hoje** (`Funcionario.Ativo`); o sistema sai com `funcionarioId` nulo, `nome` "Sistema", `perfil` nulo, `desligado` falso. O detalhe traz `mudancas` em `Ordem`, e `valorOculto` com `antes` e `depois` nulos. **Exportação:** UTF-8 com BOM, `;`, primeira linha `Quando;Quem;Perfil;Área;Ação;O que aconteceu;Registro afetado;Origem;IP`; `Quando` em `-03:00` `dd/MM/yyyy HH:mm:ss`; rótulos da tela (não os códigos); **toda célula de texto passa por `CsvSeguro.Texto`**; `Content-Disposition: attachment; filename="auditoria-AAAA-MM-DD.csv"` (dia de hoje em Brasília). A exportação **entra no log** (decisão 4: `Acesso`/`Baixou`, "Exportou o log de auditoria em CSV"), gravada **depois** de gerar o arquivo e sem entrar nele.

- [ ] **Step 1: Escrever os testes que falham** (`ApiFactory` com o elenco; eventos inseridos por SQL com `QUANDO` controlado; relógio em `2026-10-07T12:42:00Z`)

```csharp
Lista_devolve_do_mais_recente_ao_mais_antigo_com_pagina_tamanho_total_e_totalPaginas()
Periodo_padrao_sao_os_ultimos_7_dias_ate_hoje_em_Brasilia()
Evento_as_23h30_de_07_10_em_Brasilia_entra_no_filtro_do_dia_07_e_nao_no_dia_08()   // gravado como 2026-10-08T02:30:00Z (Review Focus 3)
De_maior_que_Ate_devolve_400_com_errors_de()
Filtra_por_area_e_por_acao_pelo_nome_do_contrato_e_combina_os_filtros()             // ?area=ACESSO_NEGADO&acao=ACESSO_NEGADO (o nome do contrato, não o do C#)
Area_ou_acao_desconhecida_ou_com_o_nome_do_CSharp_devolve_400_com_errors_area_ou_acao()   // area=XYZ e area=AcessoNegado; os dois campos juntos viram dois erros no mesmo 400
Lista_so_declara_os_parametros_do_contrato()                                         // o documento gerado pela API tem para listarEventosDeAuditoria exatamente pagina, tamanhoPagina, busca, de, ate, area, acao e apenasAlteracoes (sem ordenarPor e direcao); a divergência já acusa, este nomeia o motivo
ApenasAlteracoes_tira_Entrou_AcessoNegado_Baixou_e_Ponto_Registrou()
Busca_procura_no_nome_do_ator_e_na_descricao_e_trata_percentual_como_literal()
Ator_do_sistema_sai_como_Sistema_sem_funcionarioId_e_desligado_vem_da_ficha_de_hoje()
Detalhe_traz_registroAfetado_origem_e_mudancas_em_ordem_com_valorOculto_sem_antes_nem_depois()
Detalhe_de_id_inexistente_devolve_404()
Exportacao_devolve_csv_com_BOM_titulos_rotulos_da_tela_e_attachment_com_a_data_de_hoje()
Exportacao_neutraliza_celula_que_comeca_com_igual()                                // descrição "=HYPERLINK(...)" sai "'=HYPERLINK(...)"
Exportacao_nao_pagina_e_respeita_os_mesmos_filtros_da_lista()
Exportacao_entra_no_log_como_Acesso_Baixou_e_nao_aparece_no_proprio_arquivo()
Gestor_e_Funcionario_recebem_403_nas_tres_rotas_e_anonimo_401()
Respostas_batem_com_o_schema_do_contrato()                                          // AfirmarConformeAoContratoAsync para as duas rotas JSON
// RotulosDeAuditoriaTests
Todo_valor_de_AuditoriaArea_AuditoriaAcao_Perfil_e_CanalDeOrigem_tem_rotulo_e_os_literais_do_contrato_conferem()   // Funcionario => "Colaborador", Importacao => "Importação", Ferias => "Férias", PediuAjuste => "Pediu ajuste", AcessoNegado => "Acesso negado", EnviouEmail => "Enviou e-mail", Portal => "Portal no navegador", Sistema => "Automático"
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~AuditoriaConsultaTests|FullyQualifiedName~RotulosDeAuditoriaTests"`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar** DTOs, casos de uso, `ConsultaDeAuditoria`, `RotulosDeAuditoria`, os dois objetos de consulta e as três rotas (`app.GrupoRh()`, `WithName(operationId)`, `Produces<T>(200)` e `ProduzProblemas(...)` com **exatamente** os códigos acima). **Neste mesmo passo, e nunca antes**, acrescentar `listarEventosDeAuditoria`, `exportarAuditoria` e `obterEventoDeAuditoria` ao conjunto de `operationId` do escopo de `ContratoDivergenciaTests` (nunca o prefixo `/api/rh/auditoria`).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`** (esta tarefa edita; o dono revisa no PR): uma linha por rota na seção "Rotas implementadas" (`GET /api/rh/auditoria` `listarEventosDeAuditoria`, `GET /api/rh/auditoria/exportacao` `exportarAuditoria`, `GET /api/rh/auditoria/{eventoId}` `obterEventoDeAuditoria`), `em andamento`; a linha da fatia 1 da tabela por fatia passa a `em andamento`.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Auditoria|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS, inclusive a divergência (o documento da API bate com o contrato nas três operações), a matriz (as três rotas só aceitam `RH`), o `STATUS` (as três estão na seção) e o escopo (as três estão no conjunto).
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: consulta, detalhe e exportacao do log de auditoria"`

---

### Task 10: Fila de e-mails, envio trocável e captador local (Mailpit)

**Files:**
- Create (Application): `Comum/ChaveDeIdempotencia.cs`; `Emails/IFilaDeEmails.cs`, `IEnviadorDeEmail.cs`, `IRenderizadorDeEmail.cs`, `IProcessadorDeEmails.cs`
- Create (Infrastructure): `Emails/FilaDeEmails.cs`, `ProcessadorDeEmails.cs`, `SmtpEnviadorDeEmail.cs`, `OpcoesDeEmail.cs`, `EnvioDeEmailsHostedService.cs`
- Modify: `Infrastructure/DependencyInjection.cs`, `Api/Program.cs`, `Api/appsettings.json`, `appsettings.Development.json`, `docker-compose.yml`, `.env.example`, `README.md`, `back-end/Directory.Packages.props` (MailKit), `tests/.../Infra/ApiFactory.cs` (serviço de envio desligado; `EnviadorDeEmailEmMemoria`)
- Test: `tests/RHSystem.Application.Tests/Comum/ChaveDeIdempotenciaTests.cs`; `tests/RHSystem.Api.IntegrationTests/Emails/FilaDeEmailsTests.cs`, `ProcessadorDeEmailsTests.cs`, `SmtpEnviadorDeEmailTests.cs`; `tests/RHSystem.Api.IntegrationTests/Sondas/SkipLockedPeloFromSqlTests.cs` (a sonda do Step 0; fica como teste de regressão)

**Interfaces:**
- Consumes: `EmailNaFila` e `SituacaoDoEmail` (Task 1.2); `IRelogio`; `IAuditor` (Task 8.1); `IAtorAtual` (Task 5); `IUnidadeDeTrabalho`; a técnica de reserva que a sonda do Step 0 validou.
- Produces:

```csharp
// RHSystem.Application.Comum
static class ChaveDeIdempotencia {
  static string Montar(string tipo, params (string Nome, object? Valor)[] partes);   // ("AVISO_PUBLICADO", ("aviso", 15), ("func", 21)) => "AVISO_PUBLICADO:aviso=15:func=21"
  // ArgumentException se o tipo não for MAIÚSCULAS_COM_SUBLINHADO (3 a 50, o tamanho de TAREFAS_AGENDADAS.TIPO, o maior das duas tabelas) ou a chave sair do ASCII ou passar de 190: o erro aparece no teste, não no banco.
  // A fila é mais estreita: FILA_EMAILS.TIPO é de 3 a 40 (V035), e IFilaDeEmails confere isso à parte
}
// RHSystem.Application.Emails
sealed record EmailParaEnfileirar(string Tipo, string ChaveDeIdempotencia, string DestinatarioEmail, object? Carga = null,
    int? FuncionarioId = null, int? ReferenciaId = null, bool Sigiloso = false, DateTime? EnviarApos = null);   // Sigiloso exige FuncionarioId nulo e Tipo de 3 a 40 caracteres (ArgumentException nos dois: o erro aparece no teste, não no CHECK do banco)
interface IFilaDeEmails {
  Task<bool> EnfileirarAsync(EmailParaEnfileirar email, CancellationToken ct);              // false = a chave já existe (na fila ou já enviada); vale como sucesso, nunca lança por duplicidade
  Task<int> EnfileirarLoteAsync(IReadOnlyList<EmailParaEnfileirar> emails, CancellationToken ct);   // quantos entraram (os já existentes não contam); o lote SIGILOSO é embaralhado antes de gravar
  Task<int> CancelarPendentesAsync(string tipo, int referenciaId, CancellationToken ct);    // as PENDENTE do tipo e da referência: as não sigilosas viram CANCELADO com a carga apagada (UPDATE); as SIGILOSAS são APAGADAS (DELETE: o CHECK de V035 proíbe sigilosa em situação final); devolve quantas, das duas
}
sealed record MensagemDeEmail(string Para, string Assunto, string Texto, string Html);
sealed class FalhaDeEnvioException : Exception { FalhaDeEnvioException(string mensagem, bool permanente, Exception? interna = null); bool Permanente { get; } }
interface IEnviadorDeEmail { Task EnviarAsync(MensagemDeEmail mensagem, CancellationToken ct); }   // trocável: SMTP (Mailpit local), em memória (testes), o provedor do Azure depois
sealed record ModeloDeEmail(string Assunto, string Texto, string Html, AuditoriaArea AreaDeAuditoria, string DescricaoDeAuditoria, string RegistroAfetado);
interface IRenderizadorDeEmail { string Tipo { get; } ModeloDeEmail Renderizar(JsonElement carga, string destinatario); }   // um por tipo da lista fechada (CONVENCOES.md:114); todo valor interpolado no Html passa por HtmlEncode
interface IProcessadorDeEmails { Task<int> ProcessarCicloAsync(CancellationToken ct); }     // quantas mensagens tratou; o serviço em segundo plano o chama a cada Email:IntervaloSegundos; os testes o chamam direto
```

**Configuração nova** (padrões entre parênteses): `Email:EnvioAtivo` (**`false` em `appsettings.json`, `true` em `appsettings.Development.json`**: sem provedor configurado, a fila só acumula e nada vira `FALHOU` por engano), `Email:IntervaloSegundos` (10), `Email:LoteMaximoPorCiclo` (20), `Email:MaximoDeTentativas` (5), `Email:ReservaMinutos` (5), `Email:Remetente`, `Email:NomeDoRemetente`, `Email:Smtp:Servidor`, `Email:Smtp:Porta`, `Email:Smtp:Seguranca` (`Nenhuma`, `StartTls` ou `Ssl`), `Email:Smtp:Usuario` e `Email:Smtp:Senha` (a senha só por user-secrets ou configuração do App Service), `App:UrlDoFront` (base dos links dos e-mails; quem monta o link é cada renderizador).

**Regras** (decisões do roadmap, 2, e do `V035`):
- **Enfileirar** grava com o `AppDbContext` da requisição, **na mesma transação do fato** quando o chamador está em `ExecutarEmTransacaoAsync`. Antes de inserir, **salva o que estiver pendente** no contexto (um `1062` não pode levar junto o resto do lote) e insere a mensagem sozinha; `MySqlException` 1062 = `false` e a entidade é desanexada. `CriadoEm` e `ProximaTentativaEm` vêm do `IRelogio` (`ProximaTentativaEm` = `EnviarApos` ou o instante do relógio), **nunca** do padrão do banco; a `Carga` vai em JSON `camelCase`. Dentro de uma transação que desfaz, a linha some. **Cancelar:** `CancelarPendentesAsync` faz um `UPDATE` (não sigilosas: `CANCELADO`, carga nula) e um `DELETE` (sigilosas), cada um com o comentário `// AUDITORIA-OK: tabela técnica, sem registro por decisão` (Task 8.2).
- **Processar um ciclo:** reserva até `LoteMaximoPorCiclo` mensagens com `SELECT ... FOR UPDATE SKIP LOCKED` (**pela técnica que a sonda do Step 0 validou**) numa transação curta (elegíveis: `PENDENTE` com `ProximaTentativaEm <= agora`, ou `EM_ENVIO` com `ReservadoAte < agora`; ordem `ProximaTentativaEm`, `Id`), marca `EM_ENVIO` com `ReservadoAte = agora + ReservaMinutos` e confirma; depois trata cada uma fora da transação. **A vazão é isto**: 500 convites saem em 25 ciclos de 20, e dois processadores nunca pegam a mesma. Todo instante vem do `IRelogio`, **nunca** de `NOW()` do SQL.
- **Sucesso:** `ENVIADO`, `EnviadoEm`, **`Carga = null`**, reserva limpa, **numa instrução só** (o `CHECK` exige); mensagem não sigilosa grava `ENVIOU_EMAIL` pelo `IAuditor` (ator `Sistema`, área e texto do `ModeloDeEmail`). **Falha temporária:** `Tentativas + 1`, espera de 1, 5, 30 e 120 minutos, e a mensagem **volta a `PENDENTE` com `RESERVADO_ATE` limpo**, numa instrução só (o `CHECK` `CHK_FILA_EMAILS_RESERVA` do `V035` exige `(SITUACAO = 'EM_ENVIO') = (RESERVADO_ATE IS NOT NULL)`: toda saída de `EM_ENVIO` limpa a reserva); ao chegar em `MaximoDeTentativas`, `FALHOU` com carga nula e reserva limpa. **Falha permanente** (`FalhaDeEnvioException.Permanente`, o SMTP responde 5xx): `FALHOU` na hora. Tipo sem renderizador: `FALHOU` com erro claro. `UltimoErro` guarda **só** o nome da exceção e o status SMTP, **nunca a mensagem** (ela pode trazer o endereço) e nunca token. O log traz `Id` e tipo, **nunca o endereço**.
- **Sigiloso** (lembrete de formulário anônimo, `V035:53-74`): depois de enviar, ou ao desistir, a linha é **apagada** (`DELETE`), nunca `ENVIADO`/`FALHOU` (o `CHECK` proíbe); não grava `ID_FUNCIONARIO`; **nenhum evento de auditoria** (quem registra "lembrete enviado", sem destinatário nem contagem, é o caso de uso da fatia 5); a chave leva um sufixo aleatório (`:r=<guid>`), nunca a pessoa. **"Sem destinatário legível" quer dizer:** o endereço existe na tabela só enquanto a mensagem espera para sair (o envio o exige), sem ligação com a pessoa, e some com a linha (o dono aceitou em 2026-10-29 que o anonimato vale contra quem usa a aplicação, não contra quem lê o banco).
- **At-least-once:** ver "Decisões", item 5.
- **SMTP** com MailKit; `5xx` = permanente, `4xx`, falha de rede e tempo esgotado = temporária. O **provedor em produção é decisão do dono** (a lista de provedores e o custo são dele): esta task entrega o SMTP genérico, que serve ao Mailpit e a qualquer provedor SMTP.

- [ ] **Step 0: Sonda isolada do `SELECT ... FOR UPDATE SKIP LOCKED` (premissa não medida da qual a fila e o agendador dependem).** Antes de escrever a fila, medir **num teste só dela**, sem tipo de produto: `SkipLockedPeloFromSqlTests`, com um `DbContext` só do teste (entidade `LinhaDaSonda` com `Id` e `Situacao`, tabela `SONDA_RESERVA` criada pelo próprio teste num banco de `NovoBancoComScriptsAsync()`, com 10 linhas `PENDENTE`). A forma medida é **a que a fila vai usar**: dentro de `ExecutarEmTransacaoAsync`, `db.LinhasDaSonda.FromSql($"SELECT ... WHERE SITUACAO = 'PENDENTE' ORDER BY ID LIMIT {n} FOR UPDATE SKIP LOCKED").ToListAsync(ct)`, e em seguida um `UPDATE` das linhas lidas.

```csharp
Sonda_FromSql_com_FOR_UPDATE_SKIP_LOCKED_roda_e_devolve_as_n_primeiras_linhas_na_ordem()
Sonda_controle_a_primeira_conexao_de_fato_segura_as_linhas()           // CONTROLE POSITIVO: com a transação da primeira aberta, o mesmo SELECT com NOWAIT na segunda conexão falha com o erro 3572. Sem este controle, "a segunda não recebeu as mesmas linhas" admitiria duas explicações: "o SKIP LOCKED funciona" e "a primeira nunca travou nada"
Sonda_duas_conexoes_com_transacao_aberta_recebem_linhas_diferentes()   // A pega 3 e não confirma; B pega 3 com a mesma consulta e recebe outras 3, sem esperar nem falhar
Sonda_1062_dentro_de_transacao_nao_aborta_a_transacao()                // o INSERT que viola a chave única lança MySqlException 1062; o INSERT seguinte na mesma transação e o COMMIT funcionam
```

Run: `dotnet test back-end --filter SkipLockedPeloFromSqlTests`
Expected: PASS na forma que o resto da Task 10 e a Task 11 vão usar. **Se passar:** a fila e o agendador reservam pelo `FromSql`; anote o resultado numa linha na seção "Decisões" do `README.md` do back-end. **Se o `FromSql` não rodar** (o EF embrulha o texto numa subconsulta, e o MySQL pode recusar ou perder o travamento: o controle e o teste das duas conexões separam os dois casos), **a reserva muda de forma, não de regra**: `SELECT ID ... FOR UPDATE SKIP LOCKED` por `DbCommand` na conexão e na transação do contexto (`db.Database.GetDbConnection()` e `db.Database.CurrentTransaction.GetDbTransaction()`), lendo só os ids, e depois carregar e atualizar as entidades por `Where(e => ids.Contains(e.Id))` (as linhas já estão travadas pela transação). Ajuste a sonda para provar essa forma e use-a aqui e na Task 11. **Se nem isso passar** (o `SKIP LOCKED` do MySQL 8.4 não se comportou no contêiner): **pare e avise o dono**; a alternativa é reservar por `UPDATE ... WHERE ... LIMIT n` com a marca de reserva (`ReservadoAte`) e conferir as linhas afetadas, que também impede duas instâncias de pegarem a mesma mensagem, mas muda o que a Task 11 também faz.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// ChaveDeIdempotenciaTests (Application.Tests)
Montar_junta_tipo_e_partes_em_ASCII_na_ordem_dada()
Montar_aceita_tipo_de_50_caracteres_e_recusa_o_de_51_e_o_de_2()                          // 3 a 50, o tamanho de TAREFAS_AGENDADAS.TIPO
Montar_recusa_tipo_fora_do_formato_acento_e_chave_acima_de_190_caracteres()
// FilaDeEmailsTests (banco real)
Enfileirar_grava_PENDENTE_com_carga_json_e_datas_do_relogio()                            // CriadoEm e ProximaTentativaEm = os do relógio falso (e ProximaTentativaEm = EnviarApos quando informado), nunca a hora real do banco
Enfileirar_a_mesma_chave_duas_vezes_devolve_false_e_grava_uma_linha()
Enfileirar_a_mesma_chave_em_20_chamadas_simultaneas_grava_uma_linha_e_nunca_lanca()   // 20 tarefas, DbContext e conexão próprios, Barrier (Review Focus 4)
Enfileirar_com_mudancas_pendentes_no_contexto_e_chave_repetida_nao_perde_nem_duplica_as_mudancas()
Enfileirar_dentro_de_transacao_que_desfaz_nao_deixa_linha()
Enfileirar_sigiloso_com_funcionario_lanca_ArgumentException_e_o_lote_sigiloso_nao_grava_FuncionarioId()
Enfileirar_com_tipo_acima_de_40_caracteres_lanca_ArgumentException_antes_do_banco()      // FILA_EMAILS.TIPO é de 3 a 40; ChaveDeIdempotencia aceita até 50 por causa do agendador
CancelarPendentes_marca_CANCELADO_e_apaga_a_carga_das_nao_sigilosas_e_APAGA_as_sigilosas()   // só as PENDENTE do tipo e da referência; devolve a soma; as de outro tipo, de outra referência e as já enviadas ficam; a sigilosa não vira CANCELADO (o CHECK de V035 recusaria)
// ProcessadorDeEmailsTests (relógio falso, EnviadorDeEmailEmMemoria, renderizador de teste)
Ciclo_envia_no_maximo_LoteMaximoPorCiclo_e_500_convites_saem_em_25_ciclos_cada_um_uma_vez()   // Review Focus 4
Mensagem_enviada_vira_ENVIADO_com_CARGA_nula_ENVIADO_EM_e_reserva_limpa()
Mensagem_nao_sigilosa_enviada_grava_ENVIOU_EMAIL_com_ator_SISTEMA_e_sigilosa_nao_grava_evento()
Falha_temporaria_reagenda_com_espera_crescente_e_ao_chegar_no_maximo_vira_FALHOU_com_carga_nula()   // a mensagem volta a PENDENTE com RESERVADO_ATE nulo e Tentativas + 1; o CHECK de reserva nunca é violado
Falha_permanente_vira_FALHOU_na_hora_e_UltimoErro_nao_tem_o_endereco_nem_a_mensagem_da_excecao()
Reserva_vencida_e_retomada_e_reserva_valida_nao_e_tocada()                     // processo que caiu com a mensagem EM_ENVIO (Review Focus 4)
Dois_processadores_simultaneos_nunca_pegam_a_mesma_mensagem()                  // 100 mensagens, Barrier, enviador que conta
Mensagem_com_PROXIMA_TENTATIVA_no_futuro_nao_sai_antes_da_hora()
Tipo_sem_renderizador_vira_FALHOU_com_erro_claro()
Sigiloso_enviado_ou_desistido_apaga_a_linha_e_nada_do_endereco_aparece_no_log_nem_na_auditoria()   // captura o ILogger; Review Focus 4
Log_do_processador_nunca_contem_o_endereco_de_nenhuma_mensagem()
Falha_de_uma_mensagem_nao_impede_as_outras_do_mesmo_ciclo()
// SmtpEnviadorDeEmailTests
Envia_para_o_Mailpit_e_a_mensagem_aparece_na_API_do_Mailpit_com_assunto_e_acentos()   // Testcontainers (imagem axllent/mailpit, versão fixada); GET /api/v1/messages (conferir o caminho na documentação do Mailpit)
Falha_de_conexao_vira_FalhaDeEnvioException_temporaria()                              // porta fechada
Classificacao_de_falhas_5xx_permanente_4xx_e_rede_temporaria()                          // exceções do MailKit fabricadas no teste
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~ChaveDeIdempotenciaTests|FullyQualifiedName~Emails"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as interfaces e classes acima. **Mailpit no `docker compose`:** serviço `mailpit` (`axllent/mailpit` com a **versão estável conferida na hora**, nunca `latest`), SMTP em `${MAILPIT_SMTP_PORTA:-1025}` e interface em `${MAILPIT_UI_PORTA:-8025}`, `healthcheck`; as duas portas entram em `.env.example`; `appsettings.Development.json` aponta `Email:Smtp` para `localhost:1025`, `Seguranca: Nenhuma`. O `README.md` ganha "Ver os e-mails de desenvolvimento: `http://localhost:8025`". **O `ApiFactory` desliga o serviço em segundo plano** (`Email:EnvioAtivo=false`) e expõe o `EnviadorDeEmailEmMemoria`; os testes chamam `ProcessarCicloAsync` direto.
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `docker compose up -d --wait` (o Mailpit sobe junto do MySQL); Expected: PASS.
- [ ] **Step 5: Passo do dono (não bloqueia os testes): provedor de e-mail em produção.** Dizer ao dono o que falta para a demo enviar e-mail de verdade (roadmap, decisão 2 e `Dependências cruzadas`: "Blob, Storage Account e e-mail no Azure"): escolher o provedor (por exemplo o serviço de e-mail do Azure por SMTP ou outro SMTP), **dizer o custo antes de criar o recurso**, e só então preencher `Email__Smtp__*` e ligar `Email__EnvioAtivo=true` nas configurações do App Service. Até lá a fila acumula e nada é enviado.
- [ ] **Step 6: Commit** (com autorização): `git add back-end docker-compose.yml .env.example README.md; git commit -m "feat: fila de e-mails, envio SMTP trocavel e Mailpit local"`

---

### Task 11: Agendador (serviço em segundo plano, recuperação e limpeza)

**Files:**
- Create (Application): `Agendamento/IAgendador.cs`, `ITarefaAgendada.cs`, `IExecutorDeTarefas.cs`
- Create (Infrastructure): `Agendamento/Agendador.cs`, `ExecutorDeTarefas.cs`, `AgendadorHostedService.cs`, `OpcoesDoAgendador.cs`, `Tarefas/LimparTarefasAntigasTarefa.cs`, `Tarefas/LimparEmailsAntigosTarefa.cs`
- Modify: `Infrastructure/DependencyInjection.cs`, `Api/Program.cs`, `appsettings.json`, `README.md`, `tests/.../Infra/ApiFactory.cs`
- Test: `tests/RHSystem.Api.IntegrationTests/Agendamento/AgendadorTests.cs`, `ExecutorDeTarefasTests.cs`, `TarefasDeLimpezaTests.cs`, `AgendadorHostedServiceTests.cs`

**Interfaces:**
- Consumes: `TarefaAgendada`, `SituacaoDaTarefa` (Task 1.2); `ChaveDeIdempotencia` (Task 10); a técnica de reserva que a sonda da Task 10 (Step 0) validou; `IRelogio`; `IAtorAtual` (`Sistema` fora de requisição: Task 5); `FusoBrasil` (Task 4).
- Produces:

```csharp
// RHSystem.Application.Agendamento
sealed record NovaTarefa(string Tipo, string ChaveDeIdempotencia, DateTime ExecutarApos, int? ReferenciaId = null, DateOnly? DiaReferencia = null, object? Parametros = null);   // Parametros nunca com dado pessoal (V036)
interface IAgendador {                       // o que os casos de uso chamam
  Task<bool> AgendarAsync(NovaTarefa tarefa, CancellationToken ct);                                    // false = a chave já existe; vale como sucesso
  Task<bool> RegistrarComoConcluidaAsync(string tipo, string chaveDeIdempotencia, DateOnly? diaReferencia, CancellationToken ct);   // marca idempotente ("lembrete do formulário já enviado hoje", V036:45-54): false = já existia
  Task<int> CancelarAsync(string chaveDeIdempotencia, CancellationToken ct);                            // PENDENTE -> CANCELADA; 0 se não achou ou já rodou
}
sealed record TarefaParaExecutar(long Id, string ChaveDeIdempotencia, int? ReferenciaId, DateOnly? DiaReferencia, JsonElement? Parametros, int Tentativas);
interface ITarefaAgendada {                  // um por tipo de trabalho; registrado como escopo da requisição/tarefa
  string Tipo { get; }                       // "EFETIVAR_DESLIGAMENTO": MAIÚSCULAS_COM_SUBLINHADO, 3 a 50
  Task ExecutarAsync(TarefaParaExecutar tarefa, CancellationToken ct);    // lançar = tentar de novo; voltar normalmente = concluída. IDEMPOTENTE: confere o estado real antes de agir, porque o processo pode cair entre agir e marcar CONCLUIDA
  Task RecuperarAsync(CancellationToken ct); // cria (pelo IAgendador) o que ficou para trás; idempotente; roda na subida e a cada hora. Padrão do tipo: nada a recuperar
}
interface IExecutorDeTarefas {               // o que o serviço em segundo plano chama; os testes e as fatias o chamam direto, com o relógio falso
  Task<int> RecuperarAsync(CancellationToken ct);       // chama RecuperarAsync de cada ITarefaAgendada; devolve quantos tipos recuperou (um que lança é registrado e não impede os outros)
  Task<int> ExecutarCicloAsync(CancellationToken ct);   // reserva e executa as tarefas vencidas; devolve quantas tratou
}
```

**Configuração nova:** `Agendador:Ativo` (`true`; `false` no `ApiFactory`), `Agendador:IntervaloSegundos` (30), `Agendador:RecuperacaoMinutos` (60), `Agendador:LoteMaximoPorCiclo` (10), `Agendador:ReservaMinutos` (10), `Agendador:MaximoDeTentativas` (5), `Retencao:TarefasDias` (30), `Retencao:EmailsDias` (90).

**Regras** (roadmap, decisão 3; `V036`): **cada tarefa recupera o que ficou para trás e é idempotente.** Agendar usa a mesma técnica da fila (salva as pendências antes, insere sozinha, `1062` = `false`) e grava `CriadaEm` a partir do `IRelogio` (e `ExecutarApos` como informado), **nunca** do padrão do banco. **Executar um ciclo:** reserva até `LoteMaximoPorCiclo` tarefas com `SELECT ... FOR UPDATE SKIP LOCKED` (**pela mesma técnica que a sonda da Task 10, Step 0, validou**; elegíveis: `PENDENTE` com `ExecutarApos <= agora`, ou `EM_EXECUCAO` com `ReservadaAte < agora`), marca `EM_EXECUCAO` (`ReservadaAte = agora + ReservaMinutos`, `UltimaTentativaEm`, `Tentativas + 1`), e executa cada uma **em um escopo próprio** (sem `HttpContext`, logo ator `Sistema`; os eventos de auditoria saem como `SISTEMA`). Sucesso: `CONCLUIDA` e `ConcluidaEm`. Exceção: volta a `PENDENTE` com `ExecutarApos` de 1, 5, 30 e 60 minutos e `ReservadaAte` limpo (o `CHECK` `CHK_TAREFAS_AGENDADAS_RESERVA` do `V036` exige `(SITUACAO = 'EM_EXECUCAO') = (RESERVADA_ATE IS NOT NULL)`: toda saída de `EM_EXECUCAO`, para `PENDENTE`, `CONCLUIDA` ou `FALHOU`, limpa a reserva na mesma instrução); ao chegar em `MaximoDeTentativas`, `FALHOU`; `UltimoErro` = nome da exceção e a mensagem cortada em 500 (**os handlers não põem dado pessoal em mensagens de exceção**). Tipo sem `ITarefaAgendada`: `FALHOU` com erro claro. Uma tarefa que falha **nunca** impede as outras do ciclo. Duas instâncias da API (o spec prevê uma só) não executam a mesma tarefa, por causa do `SKIP LOCKED`. **Serviço em segundo plano:** na subida chama `RecuperarAsync` (e registra no log "Agendador iniciado" e quantos tipos recuperou), depois roda `ExecutarCicloAsync` a cada `IntervaloSegundos` e `RecuperarAsync` de novo a cada `RecuperacaoMinutos`. **Exige Always On** (plano pago do App Service; Task 14 da fatia 0, Step 2): com a API dormindo nada roda, e ao acordar a recuperação refaz o que passou.

**Tarefas desta fatia** (as duas primeiras de verdade, que provam o mecanismo e cumprem as limpezas que o `V035` e o `V036` recomendam; as demais são das fatias que as usam):
- `LIMPAR_TAREFAS`: `RecuperarAsync` agenda uma por dia (`LIMPAR_TAREFAS:dia=AAAA-MM-DD`); `ExecutarAsync` apaga `CONCLUIDA` e `CANCELADA` com mais de `Retencao:TarefasDias`; **nunca** `PENDENTE`, `EM_EXECUCAO` nem `FALHOU`.
- `LIMPAR_EMAILS`: o mesmo, para `ENVIADO`, `FALHOU`, `DEVOLVIDO` e `CANCELADO` com mais de `Retencao:EmailsDias` (o endereço é dado pessoal); **nunca** `PENDENTE` nem `EM_ENVIO`.
- As chamadas de `ExecuteDelete` levam o comentário `// AUDITORIA-OK: tabela técnica, sem registro por decisão` (Task 8.2, guarda de `ExecuteUpdate`).

- [ ] **Step 1: Escrever os testes que falham** (relógio falso; tarefas de teste registradas no `ApiFactory`)

```csharp
// AgendadorTests
Agendar_grava_PENDENTE_com_os_campos_e_CriadaEm_do_relogio_falso_e_a_mesma_chave_duas_vezes_devolve_false()   // CriadaEm = o relógio falso, nunca a hora real do banco
Agendar_com_tipo_acima_de_50_caracteres_lanca_ArgumentException_antes_do_banco()                            // via ChaveDeIdempotencia.Montar (3 a 50)
Agendar_a_mesma_chave_em_20_chamadas_simultaneas_grava_uma_tarefa()
RegistrarComoConcluida_insere_CONCLUIDA_uma_vez_e_devolve_false_na_segunda()
Cancelar_pendente_vira_CANCELADA_e_cancelar_concluida_devolve_0()
// ExecutorDeTarefasTests
Tarefa_vencida_roda_uma_vez_e_fica_CONCLUIDA_com_ConcluidaEm()
Tarefa_no_futuro_nao_roda_antes_da_hora()
Reserva_vencida_e_retomada_por_outro_executor_e_reserva_valida_nao_e_tocada()
Dois_executores_simultaneos_nao_executam_a_mesma_tarefa_duas_vezes()                    // 50 tarefas, Barrier, handler que conta (Review Focus 4)
Tarefa_que_lanca_volta_a_PENDENTE_com_espera_crescente_e_ao_chegar_no_maximo_vira_FALHOU()
Tarefa_que_falha_nao_impede_as_outras_do_mesmo_ciclo()
Tipo_sem_handler_vira_FALHOU_com_erro_claro()
Tarefa_executa_como_Sistema_e_o_evento_de_auditoria_sai_com_ator_SISTEMA_sem_ip()
Parada_de_tres_dias_RecuperarAsync_cria_as_tarefas_dos_dias_parados_sem_duplicar()      // tipo de teste que cria uma tarefa por dia até ontem; relógio avança 3 dias; chamar duas vezes não duplica (Review Focus 4)
RecuperarAsync_de_um_tipo_que_lanca_nao_impede_os_outros()
// TarefasDeLimpezaTests
LIMPAR_TAREFAS_apaga_concluidas_e_canceladas_antigas_e_nunca_pendentes_em_execucao_nem_falhas()
LIMPAR_EMAILS_apaga_terminados_antigos_e_nunca_pendentes_nem_reservados()
RecuperarAsync_das_limpezas_agenda_uma_por_dia_sem_duplicar()
// AgendadorHostedServiceTests
Na_subida_chama_RecuperarAsync_de_todos_os_tipos_e_registra_o_inicio_no_log()
Com_Agendador_Ativo_false_nao_roda_nada()
Cancelar_o_servico_encerra_o_laco_sem_lancar()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Agendamento"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as interfaces e classes acima, mais os registros de DI (as `ITarefaAgendada` entram como `AddScoped<ITarefaAgendada, ...>`; cada fatia acrescenta as suas) e o `README.md`: seção "Agendador e Always On" (o que roda, a recuperação na subida, o que muda se o App Service dormir).
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando, mais `dotnet test back-end --filter GuardaDeExecuteUpdateTests` (as limpezas têm o comentário); Expected: PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end README.md; git commit -m "feat: agendador com recuperacao, idempotencia e limpezas"`

---

### Task 12: Contadores do menu, a tela inicial mínima por perfil (no que é API) e a verificação da fundação

**Files:**
- Create (Application): `Painel/ContadorDoMenu.cs`, `IFonteDeContadorDoMenu.cs`, `PainelContadoresDtos.cs`, `ContadoresDoMenuCasoDeUso.cs`
- Create (Api): `Painel/ContadoresDoMenuEndpoints.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (o conjunto de `operationId` do escopo ganha `obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoRh`), `contrato/STATUS.md`, `README.md`
- Test: `tests/RHSystem.Application.Tests/Painel/ContadoresDoMenuCasoDeUsoTests.cs`; `tests/RHSystem.Api.IntegrationTests/Painel/ContadoresDoMenuTests.cs`

**Interfaces:**
- Consumes: `IEscopoDoUsuario` (Task 6); `GrupoEu()`, `GrupoGestor()`, `GrupoRh()` (Task 5); `ProduzProblemas` (Task 3); `AfirmarConformeAoContratoAsync` (Task 7).
- Produces:

```csharp
// RHSystem.Application.Painel
enum ContadorDoMenu { AvisosNaoLidos, FormulariosPendentes, DocumentosPendentes, PendenciasDaEquipe, PedidosDeAjusteDePontoDoGestor, PedidosDeAjusteDePontoDoRh }
interface IFonteDeContadorDoMenu {           // cada fatia registra as suas (4: avisos; 5: formulários; 1c: documentos; 3 e 7: ponto e férias)
  ContadorDoMenu Contador { get; }
  Task<int> ContarAsync(IEscopoDoUsuario escopo, CancellationToken ct);
}
sealed record PainelContadoresFuncionario(int AvisosNaoLidos, int FormulariosPendentes, int DocumentosPendentes);
sealed record PainelContadoresGestor(int PendenciasDaEquipe, int PedidosDeAjusteDePonto);
sealed record PainelContadoresRh(int PedidosDeAjusteDePonto);
sealed class ContadoresDoMenuCasoDeUso {
  Task<PainelContadoresFuncionario> ObterDoFuncionarioAsync(CancellationToken ct);
  Task<PainelContadoresGestor> ObterDoGestorAsync(CancellationToken ct);      // PendenciasDaEquipe = PendenciasDaEquipe; PedidosDeAjusteDePonto = PedidosDeAjusteDePontoDoGestor
  Task<PainelContadoresRh> ObterDoRhAsync(CancellationToken ct);              // PedidosDeAjusteDePonto = PedidosDeAjusteDePontoDoRh
}
```
- Rotas (`70-fatia-7-gestao.yaml:2197-2268`; `operationId` e códigos do contrato):
  - `GET /api/eu/contadores-do-menu` (`obterContadoresDoMenuDoFuncionario`, `GrupoEu`) → `200 PainelContadoresFuncionario` | `401` | `500` (rota aberta a todos os perfis: o contrato não lista 403)
  - `GET /api/gestor/contadores-do-menu` (`obterContadoresDoMenuDoGestor`, `GrupoGestor`) → `200 PainelContadoresGestor` | `401` | `403` | `500`
  - `GET /api/rh/contadores-do-menu` (`obterContadoresDoMenuDoRh`, `GrupoRh`) → `200 PainelContadoresRh` | `401` | `403` | `500`

**Regras:** **várias fontes do mesmo contador somam; sem fonte, o contador vale 0** (decisão 2: antes de a fatia dona existir, não há nada a contar; o contrato exige inteiro, não `null`). Uma fonte que lança conta 0 e registra erro no log: **o menu não pode cair por causa de um cartão**. A rota é leve (o front a chama a cada navegação): uma consulta por fonte, sem carregar listas. **A tela inicial mínima por perfil** (saudação e menu) usa só o que já existe: o `nome`, o `perfil`, o `cargo` e o `setor` de `GET /api/auth/eu` e estes contadores; a tela é do front, e os cartões completos são da fatia 7d.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests
Sem_fonte_registrada_todos_os_contadores_valem_zero()
Fontes_do_mesmo_contador_somam_e_cada_rota_usa_so_os_contadores_dela()                   // o gestor não soma DocumentosPendentes
Fonte_que_lanca_conta_zero_e_registra_erro_sem_derrubar_o_menu()
// Api.IntegrationTests/Painel
Eu_devolve_zeros_sem_fontes_e_a_soma_das_fontes_de_teste_registradas()
Eu_aceita_os_tres_perfis_e_gestor_e_rh_so_aceitam_o_proprio_perfil()                    // 403 do contrato, texto do contrato
Respostas_das_tres_rotas_e_de_auth_eu_batem_com_o_schema_do_contrato()                  // AfirmarConformeAoContratoAsync: contadores (inteiros) e UsuarioResposta (cargo e setor texto ou null, com a chave presente)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Painel"`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar** o caso de uso, o registro das fontes por DI (`IEnumerable<IFonteDeContadorDoMenu>`), as três rotas (`WithName`, `Produces<T>(200)`, `ProduzProblemas(...)` com os códigos acima). **Neste mesmo passo, e nunca antes**, acrescentar os três `operationId` (`obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor`, `obterContadoresDoMenuDoRh`) ao conjunto do escopo de `ContratoDivergenciaTests` (nenhum prefixo de caminho).
- [ ] **Step 4: Atualizar `contrato/STATUS.md`** (esta tarefa edita; o dono revisa no PR): as três rotas na seção "Rotas implementadas", `em andamento`, com a observação **"parcial: cada contador vale 0 até a fatia dona registrar a fonte (documentos: 1c; avisos: 4; formulários: 5; ponto: 3; férias: 7)"**. Elas são do contrato da fatia 7, que continua `planejada` na tabela por fatia: a observação diz ao front que já pode integrar e que os números virão das outras fatias.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Painel|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS, inclusive a divergência nas três operações novas, a matriz, o `STATUS` e o escopo (as três estão no conjunto).
- [ ] **Step 6: Verificar a fundação inteira** (a Task 13, a última, repete esta verificação ao terminar)

Run: `dotnet build back-end; dotnet test back-end; dotnet format back-end --verify-no-changes`
Expected: build sem warnings; **todos** os testes passando (fatia 0, conformidade, matriz, escopo, auditoria, fila, agendador, painel); `dotnet format` sem alterações.

- [ ] **Step 7: Verificação manual ponta a ponta** (Docker). **Banco novo antes:** `docker compose down -v; docker compose up -d --wait` (o seed do admin só roda "se não existir usuário", fatia 0, Task 9, e o `CRIOU` do admin pelo `Sistema` só aparece se o admin nascer agora, com a auditoria já ligada; `down -v` apaga o volume do MySQL local, que se recria). API com `Database:AplicarScriptsAoIniciar` e o seed do admin por user-secrets (como na fatia 0); `dotnet run --project back-end/src/RHSystem.Api`. Conferir: (a) no log, "Agendador iniciado"; (b) `POST /api/auth/entrar` com o admin e, com o token, `GET /api/rh/auditoria`: tem o evento `ENTROU` do admin e o `CRIOU` do admin pelo `Sistema` (o seed). **O corpo JSON do `entrar` vai de arquivo** (no Windows PowerShell 5.1 as aspas duplas de um JSON passado em linha chegam quebradas ao programa nativo, suponho, não medido; mesma nota da Task 3 da fatia 0): crie `$env:TEMP\corpo.json` com `{"email":"<o do seed>","senha":"<a do seed>"}` (**fora do repositório**: leva a senha do admin; apague-o ao fim) e rode `curl.exe -X POST <url da API>/api/auth/entrar -H "Content-Type: application/json" --data-binary "@$env:TEMP\corpo.json"`, ou faça o passo no Git Bash; (c) `GET /api/eu/contadores-do-menu` devolve zeros; (d) `http://localhost:8025` abre o Mailpit vazio. Registrar o resultado no `README.md`.
- [ ] **Step 8: Commit** (com autorização): `git add back-end contrato/STATUS.md README.md; git commit -m "feat: contadores do menu e verificacao da fundacao"`

---

### Task 13: Porta dos pedidos parados no gestor (implementação vazia) e a dívida registrada

**Por que existe:** a etapa de um pedido de ponto ou de férias é **fixada na criação** (`CONVENCOES.md`, seção 9): quando o gestor sai do caminho (deixa o setor, é trocado, é desligado, está ausente), nada muda a etapa sozinho. "Passar ao RH" é uma **ação explícita**, chamada por quem tira o gestor: a **1b** a chama ao efetivar o desligamento de quem é gestor e na readmissão (que tira o gestor do setor); a **1c**, ao trocar ou tirar o gestor de um setor, e chama `AprovarEtapaDoGestorAsync` ao ligar "Gestor ausente". As tabelas de pedidos são das **fatias 3 (ponto) e 7 (férias)**, então a 1a entrega só a porta, com um **composto** padrão **vazio** (ele soma as implementações que as fatias 3 e 7 registrarem), e a falta fica registrada como dívida.

**Files:**
- Create (Application): `Colaboradores/IPedidosParadosNoGestor.cs` (a porta e `IPedidosParadosNoGestorDaFatia`), `Colaboradores/PedidosAprovados.cs` (o record), `Colaboradores/PedidosParadosNoGestorComposto.cs`
- Modify: `src/RHSystem.Infrastructure/DependencyInjection.cs` (registro); `contrato/STATUS.md` (seção "Pendências conhecidas")
- Test: `tests/RHSystem.Application.Tests/Colaboradores/PedidosParadosNoGestorCompostoTests.cs`; `tests/RHSystem.Api.IntegrationTests/Colaboradores/PedidosParadosNoGestorRegistroTests.cs`

**Interfaces:**
- Consumes: nada das outras tarefas (só o contêiner de injeção).
- Produces:

```csharp
// RHSystem.Application.Colaboradores   (gestorId = Gestor.Id, a linha de GESTORES: o mesmo id de Setor.DefinirGestor(int? idGestor))
sealed record PedidosAprovados(int AjustesDePonto, int Ferias) {         // Colaboradores/PedidosAprovados.cs; o que "Gestor ausente" aprovou agora, por tipo de pedido: vira `pedidosAprovadosAgora { ajustesDePonto, ferias }` na resposta da 1c
  static PedidosAprovados Zero { get; }                                  // 0 e 0
  PedidosAprovados Somar(PedidosAprovados outro); }                      // soma campo a campo: é como o composto junta as fatias
interface IPedidosParadosNoGestor {          // a ÚNICA porta que a 1b e a 1c injetam (o composto abaixo). As duas ações são IDEMPOTENTES e rodam no AppDbContext (e na transação) de quem chamou: o fato e a passagem ao RH desfazem juntos
  Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct);   // devolve quantos pedidos aprovou agora (o composto vazio devolve Zero), e a resposta de "Gestor ausente" repete o valor em `pedidosAprovadosAgora`. "Gestor ausente" ligado: aprova a etapa do gestor dos pedidos já parados nele e os manda ao RH (o contrato exclui férias com sugestão de data em andamento)
  Task PassarParaORhAsync(int gestorId, CancellationToken ct);          // o gestor deixou o setor, foi trocado ou foi desligado: os pedidos parados na etapa dele passam à etapa do RH, sem aprovação
}
interface IPedidosParadosNoGestorDaFatia {   // o que cada fatia dona de pedidos implementa sobre as tabelas dela: 3 (ponto), 7 (férias); cada uma a registra com AddScoped<IPedidosParadosNoGestorDaFatia, ...> e NUNCA como IPedidosParadosNoGestor
  Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct);
  Task PassarParaORhAsync(int gestorId, CancellationToken ct);
}
sealed class PedidosParadosNoGestorComposto(IEnumerable<IPedidosParadosNoGestorDaFatia> fatias) : IPedidosParadosNoGestor   // a implementação PADRÃO, registrada como escopo da requisição: chama cada fatia registrada, em ordem, e AprovarEtapaDoGestorAsync devolve a SOMA dos valores (PedidosAprovados.Somar; Zero sem nenhuma fatia); sem nenhuma, não faz nada
```

**Por que um composto e não "a fatia 3 e a 7 substituem a implementação":** duas fatias implementam a **mesma** porta; se cada uma trocasse o registro, a segunda apagaria a primeira (ponto ou férias ficaria sem passar ao RH). Cada fatia registra o **seu** `IPedidosParadosNoGestorDaFatia` e o composto os junta; a porta que a 1b e a 1c chamam não muda. **Regra de injeção:** a 1b e a 1c injetam só `IPedidosParadosNoGestor` (o composto), **nunca** `IEnumerable<IPedidosParadosNoGestor>` nem `IEnumerable<IPedidosParadosNoGestorDaFatia>`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// PedidosParadosNoGestorCompostoTests (Application.Tests; fatias falsas em memória)
Sem_nenhuma_fatia_registrada_as_duas_acoes_terminam_sem_fazer_nada_e_sem_lancar()
Cada_acao_chama_todas_as_fatias_registradas_com_o_mesmo_gestorId_na_ordem_do_registro()
Aprovar_devolve_a_soma_dos_valores_das_fatias_registradas_e_Zero_sem_nenhuma()          // fatias falsas que devolvem (1, 0) e (0, 4): o composto devolve (1, 4); sem fatia, (0, 0)
PedidosAprovados_Somar_soma_campo_a_campo_e_Zero_e_0_e_0()
Excecao_de_uma_fatia_sobe_para_quem_chamou_para_a_transacao_dele_desfazer()          // as ações são idempotentes: tentar de novo é seguro
// PedidosParadosNoGestorRegistroTests (Api.IntegrationTests)
IPedidosParadosNoGestor_esta_registrado_e_o_padrao_nao_faz_nada()                      // resolvido do contêiner do ApiFactory; as duas ações com um gestorId qualquer terminam sem lançar e Aprovar devolve Zero (0 e 0)
Fatia_registrada_por_AddScoped_e_chamada_pela_porta_unica_e_o_valor_dela_sai_em_AprovarEtapaDoGestorAsync()   // o ApiFactory registra uma IPedidosParadosNoGestorDaFatia falsa que devolve (3, 2): resolver IPedidosParadosNoGestor chama a fatia e devolve (3, 2)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~PedidosParadosNoGestor"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as duas interfaces, o record `PedidosAprovados` e o composto com as assinaturas acima, e registrar `IPedidosParadosNoGestor` → `PedidosParadosNoGestorComposto` como escopo da requisição em `DependencyInjection.cs` (o contêiner entrega uma lista vazia de `IPedidosParadosNoGestorDaFatia` até as fatias 3 e 7 registrarem as delas).
- [ ] **Step 4: Rodar e ver passar.** Mesmo comando; Expected: PASS.
- [ ] **Step 5: Registrar a dívida** (esta tarefa edita; o dono revisa no PR): em `contrato/STATUS.md`, **abaixo** da seção "Rotas implementadas", uma seção `## Pendências conhecidas` com uma linha: "**Pedidos parados no gestor (porta vazia):** trocar, tirar ou desligar um gestor e ligar "Gestor ausente" chamam `IPedidosParadosNoGestor`, mas até as fatias 3 (ponto) e 7 (férias) implementarem a deles **nenhum pedido muda de etapa** (`CONVENCOES.md`, seção 9)". A mesma dívida está na seção "Para as fatias 2 em diante" deste plano.
- [ ] **Step 6: Verificar a fundação inteira de novo**

Run: `dotnet build back-end; dotnet test back-end; dotnet format back-end --verify-no-changes`
Expected: build sem warnings; todos os testes passando (inclusive `ContratoEStatusTests`, que continua lendo só a seção "Rotas implementadas"); `dotnet format` sem alterações.

- [ ] **Step 7: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: porta dos pedidos parados no gestor (implementacao vazia) e divida registrada"`

---

## Para as fatias 1b e 1c

O que esta fatia produz e que as seguintes **consomem**, com o arquivo onde mora (caminhos sob `back-end/src/`, salvo indicação). **Os nomes e as assinaturas não mudam depois** (o `Interfaces → Produces` de cada tarefa é a fonte); se uma fatia precisar de outra coisa, ela acrescenta, não renomeia.

**Mapeamento e banco**
- `Funcionario.Matricula`, `CargoId`, `SetorId`, `AtribuirMatricula`; `Usuario.SenhaProvisoria`, `Criar(..., senhaProvisoria)`, `DefinirSenha`; `Setor.GestorId`: `RHSystem.Domain/Autenticacao/`
- `UsuarioTokenSenha`, `TipoDeTokenDeSenha`: `RHSystem.Domain/Autenticacao/UsuarioTokenSenha.cs` (1b: convite e redefinição)
- `Gestor`, `DesligamentoAgendado`, `SituacaoDoDesligamento`: `RHSystem.Domain/Colaboradores/` (1b e 1c)
- `Beneficio`: `RHSystem.Domain/Beneficios/Beneficio.cs` (1c)
- **O que a 1a NÃO cria (é da 1b, e a 1c só consome):** `NivelDeAcesso : IAuditavel` e `NomesDeNivel` (com `DoPerfil(Perfil)`), `Cargo.CargaSemanalHoras` e o mapeamento de `CARGA_HORARIA`, `Setor.DefinirGestor(int? idGestor)` (id de `GESTORES`), o record `GestorReferencia` (`Application.Colaboradores`), a entidade `BeneficioDoFuncionario` (`BENEFICIOS_FUNCIONARIOS`) e `IGestorDoFuncionario.RefazerDosMembrosAsync` (que `criarSetor` e `editarSetor`, da 1c, chamam). A 1a só põe `Setor.GestorId` (propriedade)
- `Importacao`, `SituacaoDaImportacao`: `RHSystem.Domain/Importacoes/Importacao.cs` (fatia 2)
- `ConversoresDeTempo.Utc` e `.Brasil`, `EnumComoTextoMaiusculo<T>`: `RHSystem.Infrastructure/Persistencia/` (toda coluna `DATETIME` nova diz o fuso)
- `ConformidadeDoModeloTests` e `tabelas-sem-mapeamento.txt`: `tests/RHSystem.Api.IntegrationTests/Persistencia/` (cada fatia tira daqui as tabelas que mapeia)
- `IGeradorDeMatriculas.ReservarAsync`: `RHSystem.Application/Colaboradores/IGeradorDeMatriculas.cs` (1b: cadastro; fatia 2: lote de até 500)

**Erros, listas, datas e JSON**
- `NaoEncontradoException`, `ConflitoException`, `ErroDeNegocioException`, `ErroDeValidacaoException`, `ColetorDeErros`, `OuNaoEncontrado`: `RHSystem.Application/Comum/Excecoes.cs`
- `TextosDeErro`, `ProduzProblemas`: `RHSystem.Api/Configuracao/`
- `ParametrosDeLista`, `RespostaPaginada<T>`, `MapaDeOrdenacao<T>`, `DirecaoDeOrdenacao`, `EnumsDeConsulta.Ler<TEnum>`: `RHSystem.Application/Comum/Consultas/`. **`[AsParameters] ParametrosDeLista` só na rota cujo contrato declara `pagina`, `tamanhoPagina`, `busca`, `ordenarPor` e `direcao`**; a que declara outros parâmetros tem objeto de consulta próprio (a auditoria é o exemplo) e liga enum de consulta (`?area=ACESSO_NEGADO`) por `EnumsDeConsulta.Ler`
- Texto de 400: `Dados inválidos.` / `Confira os campos indicados.` em todo 400, **menos** o `entrar` (`Preencha o e-mail e a senha.`)
- `ToRespostaPaginadaAsync`, `BuscaLike.Contem`: `RHSystem.Infrastructure/Persistencia/ConsultasEf.cs`
- `FusoBrasil`: `RHSystem.Application/Comum/Tempo/FusoBrasil.cs`
- `CsvSeguro.Texto`, `.Numero`, `.Escrever`: `RHSystem.Application/Comum/Exportacao/CsvSeguro.cs`
- `JsonDoContrato.Configurar`: `RHSystem.Api/Configuracao/JsonDoContrato.cs`

**Autorização e escopo**
- `Politicas`, `FamiliasDeRota` (`GrupoRh`, `GrupoGestor`, `GrupoEu`, `GrupoPublico`): `RHSystem.Api/Autorizacao/`. O documento OpenAPI e a UI da documentação são anônimos e ficam fora das famílias (a regra de reserva os exclui)
- `IAtorAtual`, `TipoDeAtorAtual`: `RHSystem.Application/Escopo/IAtorAtual.cs`
- `IEscopoDoUsuario`, `EquipeDoGestor`: `RHSystem.Application/Escopo/IEscopoDoUsuario.cs`
- `DaEquipe(this IQueryable<Funcionario>, EquipeDoGestor)`: `RHSystem.Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs`
- Testes: `MatrizDePerfisTests` (automática: toda rota nova entra sozinha), `ElencoDeTeste`, `ApiFactory.SemearElencoAsync/ClienteComo/ClienteAnonimo` (`tests/RHSystem.Api.IntegrationTests/Infra/` e `Matriz/`); `CamposDoRh.AfirmarSemCamposDoRh` e `AfirmarConformeAoContratoAsync` (`tests/.../Matriz/` e `Contrato/`): **toda rota nova chama os dois** (o segundo em toda resposta 2xx)

**Auditoria**
- `AuditadoAttribute`, `AuditarAttribute`, `IAuditavel`, `AuditoriaArea`, `AuditoriaAcao`: `RHSystem.Domain/Auditoria/`
- `IAuditor` (`Registrar`, `RegistrarComo`, `RegistrarEGravarAsync`, `RegistrarEGravarComoAsync`, `RegistrarEGravarComLimiteAsync`, `SuspenderRegistroAutomatico`), `EventoDeAuditoriaNovo`, `MudancaDeAuditoriaNova`, `AtorDoEvento`: `RHSystem.Application/Auditoria/IAuditor.cs`. `Quando` vem sempre do `IRelogio`
- A regra: entidade nova marca `[Auditado]` e `[Auditar]`; efeito de trigger e lote usam `IAuditor.Registrar`; **dentro de transação o evento sai por `Registrar` (ou `RegistrarComo`, sem login) na mesma conexão, e `RegistrarEGravar*` só fora de transação** (Task 8.1, "Qual conexão grava o evento"); `ExecuteUpdate`, `ExecuteDelete` e `ExecuteSql` levam `// AUDITORIA-OK: <motivo>`

**E-mail**
- `IFilaDeEmails` (`CancelarPendentesAsync` marca `CANCELADO` as não sigilosas e **apaga** as sigilosas), `EmailParaEnfileirar` (`Tipo` de 3 a 40): `RHSystem.Application/Emails/IFilaDeEmails.cs`
- `IRenderizadorDeEmail`, `ModeloDeEmail`: `RHSystem.Application/Emails/IRenderizadorDeEmail.cs` (cada e-mail da lista fechada é um renderizador, registrado por DI)
- `IEnviadorDeEmail`, `MensagemDeEmail`, `FalhaDeEnvioException`, `IProcessadorDeEmails`: `RHSystem.Application/Emails/`
- `ChaveDeIdempotencia.Montar` (tipo de 3 a 50): `RHSystem.Application/Comum/ChaveDeIdempotencia.cs`

**Agendador**
- `IAgendador`, `NovaTarefa`: `RHSystem.Application/Agendamento/IAgendador.cs`
- `ITarefaAgendada`, `TarefaParaExecutar`: `RHSystem.Application/Agendamento/ITarefaAgendada.cs` (1b: `EFETIVAR_DESLIGAMENTO` e a limpeza de tokens de senha, `V033`; fatia 2: `LIMPAR_IMPORTACOES`)
- `IExecutorDeTarefas`: `RHSystem.Application/Agendamento/IExecutorDeTarefas.cs` (os testes das fatias o chamam com o relógio falso)

**Painel**
- `IFonteDeContadorDoMenu`, `ContadorDoMenu`: `RHSystem.Application/Painel/` (1c registra `DocumentosPendentes`)

**Pedidos parados no gestor** (Task 13)
- `IPedidosParadosNoGestor` (`Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, ct)` e `Task PassarParaORhAsync(int gestorId, ct)`; `gestorId` = `Gestor.Id`), `PedidosAprovados`, `IPedidosParadosNoGestorDaFatia` e o composto padrão (vazio; soma as fatias): `RHSystem.Application/Colaboradores/IPedidosParadosNoGestor.cs`, `PedidosAprovados.cs`, `PedidosParadosNoGestorComposto.cs`. **A 1b e a 1c injetam só `IPedidosParadosNoGestor`.** **A etapa do pedido é fixada na criação** (`CONVENCOES.md`, seção 9): "passar ao RH" é ação explícita de quem tira o gestor. **1b** chama `PassarParaORhAsync` ao **efetivar o desligamento** de quem é gestor e na **readmissão** (que tira o gestor do setor). **1c** chama `PassarParaORhAsync` ao **trocar ou tirar o gestor de um setor** e `AprovarEtapaDoGestorAsync` ao ligar **"Gestor ausente"**. Cada chamada roda na transação de quem chama. As fatias 3 e 7 implementam a deles (ver "Para as fatias 2 em diante")

**Notas por fatia (para quem escrever o plano da 1b e da 1c):** 1b usa `FamiliasDeRota`, `IEscopoDoUsuario`/`DaEquipe`, `ParametrosDeLista`/`MapaDeOrdenacao`, `IGeradorDeMatriculas` dentro de `ExecutarEmTransacaoAsync` (reserva no fim), `IFilaDeEmails` na mesma transação do convite, `IAgendador` para o desligamento, `IPedidosParadosNoGestor.PassarParaORhAsync` (desligamento de gestor e readmissão), `IAuditor.RegistrarComo` (mesma transação) nos eventos de senha das rotas anônimas (`esqueci`, `definir`), e acrescenta o limitador de `POST /api/auth/senha/esqueci` ao `LimiteDeTentativas.cs` da fatia 0 (resposta uniforme). 1c registra `DocumentosPendentes`, marca as propriedades novas com `[Auditar]`, **consome** da 1b as peças listadas em "O que a 1a NÃO cria" (`criarSetor` e `editarSetor` chamam `IGestorDoFuncionario.RefazerDosMembrosAsync`) e chama `IPedidosParadosNoGestor` ao trocar ou tirar o gestor do setor e ao ligar "Gestor ausente". Nenhuma das duas reescreve a matriz, a conformidade nem o teste de divergência: só acrescentam **os `operationId` das rotas** ao conjunto do escopo de `ContratoDivergenciaTests` (no passo que implementa a rota, nunca prefixo de caminho) e as rotas à seção "Rotas implementadas" do `STATUS.md` (a própria tarefa edita; o dono revisa no PR).

---

## Para as fatias 2 em diante

Dívidas que a fatia 1 deixa, de propósito, para quem vier:

- **Pedidos parados no gestor (Task 13).** A porta `IPedidosParadosNoGestor` existe e o padrão **não faz nada**. A **fatia 3 (ponto)** e a **fatia 7 (férias)** implementam, cada uma sobre as tabelas dela, `IPedidosParadosNoGestorDaFatia` (`AprovarEtapaDoGestorAsync`: aprova a etapa do gestor dos pedidos parados nele, menos férias com sugestão de data em andamento, e devolve quantos aprovou (`PedidosAprovados`; o composto soma os das fatias); `PassarParaORhAsync`: passa à etapa do RH o que está parado na etapa do gestor) e registram a sua por DI; as ações são idempotentes e rodam na transação de quem chama. Até lá, trocar, tirar ou desligar um gestor e ligar "Gestor ausente" não movem pedido nenhum. Registrada também em `contrato/STATUS.md`, "Pendências conhecidas".
- **Contadores do menu (Task 12).** Cada fatia dona registra a sua `IFonteDeContadorDoMenu` (avisos: 4; formulários: 5; ponto: 3; férias: 7; documentos: 1c); sem fonte o contador vale 0.
- **Provedor de e-mail em produção (Task 10, Step 5).** Decisão e custo do dono; até lá a fila só acumula (`Email:EnvioAtivo=false`).
- **Garantia de "só inclusão" da auditoria no banco.** Sem trigger por ora (decisão 13); se o dono quiser, é script novo (`V048` em diante, numerado por ele).

---

## Autorrevisão (cobertura do roadmap, da fatia 1a, e do contrato)

| Pedido da 1a | Task |
|---|---|
| Mapeamento do EF das tabelas e colunas novas (V030 a V038): calculadas, `DEFAULT (UTC_TIMESTAMP(6))`, padrões `FALSE`, matrícula por `SEQUENCIAS`; teste "toda coluna obrigatória sem padrão está mapeada" e sonda da decisão 12 | 1.1 (conversores e conformidade, regras gerais permanentes; a sonda descartável é da Task 5 da fatia 0), 1.2 (as tabelas novas) e 2 (matrícula) |
| Autorização por família de rota, escopo por perfil (linhas e campos: 404 fora do escopo, 403 perfil errado), matriz de perfis reutilizável | 5, 6 e 7 |
| Auditoria: interceptor, eventos explícitos, `AUDITORIA_*`, enums, sem senha/hash/token/valor sensível, efeito de trigger e lote explícitos, `ExecuteUpdate` nunca sem registro | 8.1 e 8.2 (gravação; 8.2 inclui o login com senha errada limitado a um por minuto) e 9 (leitura, tela 31) |
| Fila de e-mails: idempotência, vazão de 500, reserva, carga apagada, lembrete anônimo sem destinatário legível, interface trocável, Mailpit | 10 (a sonda do `SKIP LOCKED` é o Step 0) |
| Agendador: `TAREFAS_AGENDADAS`, serviço em segundo plano, recuperação, idempotência, Always On | 11 |
| Erros padrão (`ProblemDetails`, `codigo`, 422 com `regraDoBanco`, 409, 404) e paginação, ordenação, filtros e enum de consulta | 3 e 4 |
| Teste de divergência API × contrato estendido a toda rota nova | 9 e 12 (cada uma acrescenta os `operationId` ao conjunto do escopo, no passo que implementa a rota); a Task 7 acrescenta a conferência do JSON real (com `allOf`) e o teste "toda rota implementada está no escopo" |
| Documento OpenAPI e UI anônimos com a reserva ligada | 5 (Step 4) |
| `contadores-do-menu` e a tela inicial mínima (só a API) | 12 |
| `STATUS.md`: tabela por fatia mantida e seção "Rotas implementadas" editada pela tarefa de cada rota | 7 (formato e fatia 0), 9 e 12 (rotas novas) e 13 (dívida), guardado por teste |
| Porta `IPedidosParadosNoGestor` com implementação vazia e a dívida registrada | 13 |

**Tipos coerentes entre tarefas:** `ConversoresDeTempo` e `EnumComoTextoMaiusculo` nascem na Task 1.1 e as entidades e enums na 1.2; `AuditoriaArea`/`AuditoriaAcao` nascem na Task 1.2 e são usados nas 8.1, 8.2, 9 e 10 (`ModeloDeEmail.AreaDeAuditoria`); `EnumsDeConsulta` nasce na 4 e é usado na 9; `IAuditor.RegistrarEGravarComLimiteAsync` nasce na 8.1 e é usado na 8.2; `IPedidosParadosNoGestor` nasce na 13 (a 1b e a 1c o consomem); `IAtorAtual` nasce na 5 e é usado nas 6, 8 e 11; `FusoBrasil` nasce na 4 e é usado nas 6, 9 e 11; `ChaveDeIdempotencia` nasce na 10 e é usada na 11; `ProduzProblemas` nasce na 3 e é usado nas 9 e 12; `AfirmarConformeAoContratoAsync` nasce na 7 e é usado nas 9 e 12.
