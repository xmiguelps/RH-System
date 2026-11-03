# Fatia 1c (apoio e documentos) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking. **O método de execução (subagentes ou na própria sessão) ainda não foi escolhido pelo dono: ele decide depois do planejamento.** Entrada do plano do back-end: `docs/superpowers/plans/2026-10-30-plano-back-end.md`.

**Goal:** Entregar as 21 operações do contrato (`contrato/fontes/20-fatia-1-colaboradores.yaml`) que são os cadastros de apoio (cargos, setores, gestores com "gestor ausente", benefícios do catálogo), a configuração da empresa e os documentos do colaborador (do RH e "Meus documentos"), com o **primeiro uso de arquivos**: Blob Storage (Azurite no `docker compose`; Storage Account no Azure, criado com o custo dito antes).

**Architecture:** Mesma arquitetura da 1a (Clean Architecture, casos de uso como classes simples, entidades mapeadas por Fluent API). Tudo o que as fatias 2 a 8 vão reutilizar sai **atrás de interfaces pequenas**: `IArmazenamentoDeArquivos` (gravar, link temporário, apagar), `IConfiguracaoDaEmpresa` (horário padrão, tolerância, dia de pagamento) e `IGestorAusente` (a regra de "gestor ausente" e de "quem decide primeiro"). **A 1a, a 1b e a 1c têm cada uma as suas peças e só consomem as das outras** (decisões de coordenação em `2026-10-30-plano-back-end.md`): a porta `IPedidosParadosNoGestor` é da 1a (o composto padrão, vazio, que soma as implementações que as fatias 3 e 7 registrarem) e esta fatia só a **chama**, sem tocar nas tabelas de ponto e de férias, que ainda não têm entidade; `BeneficioDoFuncionario`, `NomesDeNivel`, `GestorReferencia`, `Setor.DefinirGestor` e `IGestorDoFuncionario.RefazerDosMembrosAsync` são da 1b (tabela "O que conferir", em "Antes de começar").

**Tech Stack:** .NET 10, EF Core 9.x, Pomelo 9.0.0, MySQL 8.4, xUnit, Testcontainers (MySQL, Mailpit e, agora, **Azurite**), **`Azure.Storage.Blobs`** (versão estável **conferida no nuget.org na hora**, fixada em `Directory.Packages.props`) e **Azurite** no `docker compose`.

**Spec:** `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seção "Arquivos" `:74`; local `:125`; segredos `:130`); roadmap `docs/superpowers/plans/2026-10-28-roadmap-completo.md` (fatia 1, incremento 1c, `:95-125`; decisões que atravessam fatias 5, 7, 12 e 13, `:74-82`); contrato `contrato/CONVENCOES.md` (seções 6, 9, 11 e 12) e `contrato/fontes/20-fatia-1-colaboradores.yaml`; scripts `V001`; plano da 1a (`docs/superpowers/plans/2026-10-30-back-end-fatia-1a-fundacao.md`, seção "Para as fatias 1b e 1c"); plano da 1b (`docs/superpowers/plans/2026-10-30-back-end-fatia-1b-cadastro.md`, seção "Para as fatias 1c e 2") e entrada (`2026-10-30-plano-back-end.md`).

## Global Constraints

- .NET 10 com **EF Core 9.x** e `Pomelo.EntityFrameworkCore.MySql` **9.0.0**; **sem migrations**. Camadas: Api e Infrastructure dependem de Application; Application depende de Domain; **Domain não depende de nada**. Casos de uso são **classes simples**, sem biblioteca de mediação.
- **Scripts `V001` a `V005`: não se editam sem avisar o dono; depois da Task 14 da fatia 0 (primeiro deploy) só script novo.** Faltando coluna, índice ou tabela: **para e pergunta ao dono**; a mudança é script novo (`V006` em diante, numerado por ele).
- **O contrato é lei:** a API implementa `contrato/openapi.yaml`. Não se inventa rota, campo nem `codigo` de erro. **O escopo de `ContratoDivergenciaTests` é uma lista de `operationId`, nunca um prefixo de caminho** (`/api/rh/beneficios` é prefixo textual de `/api/rh/beneficios-adesoes`, da fatia 7): cada tarefa que entrega rotas acrescenta os `operationId` dela **no mesmo passo em que implementa as rotas** (nunca antes: a regra "operação do contrato no escopo que a API não tem é divergência" falharia no meio da fatia) e declara **todos** os códigos de resposta do contrato (`WithName`, `Produces<T>`, `ProduzProblemas`).
- **`contrato/STATUS.md`** (formato da 1a, Task 7): a tabela por fatia, que o front lê, **não é reescrita aqui**; **a tarefa que entrega a rota acrescenta a linha dela na seção "Rotas implementadas"** (rota, `operationId`, situação `em andamento`, observação). O dono revisa no PR e passa a `pronta` quando publicar no ambiente da demo. O `ContratoEStatusTests` da 1a confere que a seção lista toda rota implementada.
- **Fuso:** tabelas do grupo (`CARGOS`, `SETORES`, `GESTORES`, `BENEFICIOS`, `FUNCIONARIOS_DOCUMENTOS`, `CONFIGURACOES_EMPRESA`) em `-03:00` (`ConversoresDeTempo.Brasil`); a API fala UTC (`...Z`). Colunas `DATETIME` sem fração: o código **trunca ao segundo** antes de gravar (o MySQL arredondaria, suposição que a Task 1 mede; a mesma regra vale para `USUARIOS.DATA_HORA_ULTIMO_LOGIN`, que `Usuario.RegistrarLogin` trunca desde a fatia 0, Task 5, e a 1a testa com relógio de fração, Task 1.1). **Quem preenche as datas:** `DATA_PEDIDO` e `DATA_ENVIO` a aplicação grava, sempre a partir de `IRelogio`; `DATA_CADASTRO` é do banco (`DEFAULT CURRENT_TIMESTAMP` do `V001`, tabela do grupo), então os testes com relógio falso **não** a esperam igual ao relógio falso.
- **Privacidade:** senha, hash e token nunca vão para log, auditoria nem erro. **A connection string do Storage (traz a chave da conta) nunca vai para o repositório, o log nem a resposta**; no Azure o dono a cola direto nas configurações do App Service.
- **E-mails: nenhum e-mail novo.** A lista é fechada (`CONVENCOES.md`, seção 12): **não há e-mail de pedido de documento nem lembrete de documento**. Conteúdo escrito por usuário (descrições, nomes) é texto: a API não o altera nem o interpreta como HTML.
- **Arquivos** (`CONVENCOES.md`, seção 6; roadmap, decisão 7): envio por `multipart/form-data`; o **tipo vem do conteúdo, não da extensão**; **o banco valida (leitura) antes de qualquer envio e nenhuma transação fica aberta durante o envio**: validar → gravar no Blob → uma transação curta que insere a linha; se ela falhar, apagar o blob; **download sempre por link temporário e como anexo**, nunca pelos bytes na API de negócio; sobras são limpas por tarefa agendada.
- **Azure:** o Storage Account é criado **na Task 9, com o custo dito antes e pelo dono**, que nunca usou Azure: o assistente **não cria nada** e nunca vê as chaves.
- **Commits e PRs: nunca pôr o Claude como coautor** (nem `Co-Authored-By`, nem "Generated with Claude Code"). Só commitar e enviar quando o dono pedir. Segredos nunca vão para o repositório.
- Nomes de domínio e de API em português sem acento; mensagens ao usuário em português do Brasil com acento. Comandos rodam **da raiz do repositório** e funcionam no Windows PowerShell 5.1 (sem `&&`; `curl.exe`). `TreatWarningsAsErrors` e `dotnet format back-end --verify-no-changes` valem para tudo o que esta fatia escrever.
- **Os arquivos de `Banco de Dados/` são do grupo:** esta fatia não os toca.

## Decisões deste plano (o contrato e o roadmap não fixam)

Escolhas de engenharia de quem escreveu; o dono pode mudar na leitura. As oito primeiras mudam algo que ele ou o RH veem:

1. **Escolher como gestor de um setor alguém que pertence a outro setor não muda o setor dessa pessoa.** O contrato não tem erro para isso nem diz que o gestor precisa ser do setor. Consequência: ele gerencia o setor sem fazer parte dele (`quantidadePessoas` do setor não o conta; `quantidadeEquipe` dele é a do setor que gerencia). **Pergunta ao dono** (Task 5, Step 6).
2. **"Gestor ausente": `pedidosAprovadosAgora` repete o que a porta devolve, e só passa de 0 e 0 quando as fatias 3 e 7 registrarem a implementação delas.** Esta fatia só **chama** a porta `IPedidosParadosNoGestor` (da 1a; o composto padrão é vazio e as fatias 3 e 7 registram a implementação delas): `AprovarEtapaDoGestorAsync(gestorId, ct)` ao ligar a ausência, que devolve `PedidosAprovados(AjustesDePonto, Ferias)` (a soma do que cada fatia aprovou). Hoje não há pedido algum, porque ponto e férias ainda não existem. **O contrato pede em `pedidosAprovadosAgora { ajustesDePonto, ferias }` o número real de pedidos aprovados nesta chamada (`20-fatia-1:3216-3224`):** a 1c o põe na resposta exatamente como a porta o devolveu, `{ ajustesDePonto: 0, ferias: 0 }` com o composto vazio e os números reais depois que as fatias 3 e 7 registrarem a implementação delas. O teste da Task 4 prova a repetição com um espião que devolve números diferentes de zero. Nenhuma pergunta ao dono: está decidido. Não há tarefa agendada de ausência: a API calcula `ausenteHoje` na hora (nota «(antigo V011)» do `V001`).
3. **Promover a `Gestor` e rebaixar a `Funcionario` valem a partir do próximo login ou renovação de sessão** (o token de acesso dura 15 minutos e carrega o perfil; a mesma consequência já aceita para inativação, plano da fatia 0, "Decisões").
4. **Nome único de cargo, setor e benefício também ignora acentos**, não só maiúsculas: a chave única do banco usa `utf8mb4_unicode_ci` (`Criacao_Tabelas:68`, `:119`, `:541`), então "Estagiario" e "Estagiário" são o mesmo nome (o contrato diz só "sem diferenciar maiúsculas").
5. **O download do próprio documento ("Meus documentos") não entra no log**; o do RH entra (`Documentos`/`Baixou`), como o contrato pede só para ele.
6. **O nome do arquivo enviado aparece no log como "valor não registrado"** (`ValorOculto`): o nome pode revelar conteúdo (roadmap, decisão 13). O log mostra que o arquivo mudou, não qual.
7. **Ordem da lista de documentos: mais recentes primeiro** (descrição de `listarDocumentosDoFuncionario`, por data de cadastro e depois por id); o exemplo do contrato da ficha do RH está na ordem contrária e o de "Meus documentos" na certa. Vale o texto.
8. **10 MB são 10 MiB** (`10 * 1024 * 1024` bytes). Acima de 16 MiB o próprio servidor corta com 413, que o contrato não lista; entre 10 e 16 MiB a API devolve o 400 em `arquivo` que o contrato manda.
9. **Refazer o gestor das pessoas do setor** atinge as **ativas** (inclusive quem está em admissão; é a regra de `IGestorDoFuncionario.RefazerDosMembrosAsync`, da 1b); o `ID_GESTOR` de **inativos** fica como estava (histórico de quem era o gestor deles).
10. **Cargo sem carga horária** (o banco aceita `NULL`, `Criacao_Tabelas:66`; o contrato não) só existiria por SQL direto: a lista devolve `0` e registra um aviso no log.
11. **O envio do arquivo acontece fora de qualquer transação:** (1) valida, inclusive o que se lê do banco (pessoa existe, documento está `Pendente`); (2) grava no Blob; (3) **uma transação curta** insere (ou atualiza) a linha e o evento de auditoria; (4) se o passo 2 ou o 3 falhar, a API apaga o blob. A API cair entre o 2 e o 3 deixa no máximo uma sobra, que a tarefa de limpeza apaga depois de 24 h. Custo: uma sobra possível; ganho: nenhuma transação (e nenhum bloqueio de linha) aberta durante o envio de até 10 MiB. Isto lê "só depois de a regra do banco aprovar" (`20-fatia-1:969-971`, roadmap decisão 7) como "depois de o banco validar por leitura": em `FUNCIONARIOS_DOCUMENTOS` não há trigger que só o `INSERT` acione (`V001`: só a chave estrangeira e dois `CHECK`, que a aplicação cumpre por construção).
12. **`GET /api/eu/empresa` não usa cache**: é uma linha só.

## Review Focus

1. **Nome repetido em corrida e sem diferenciar maiúsculas nem acentos** (cargo, setor, benefício): 20 criações simultâneas do mesmo nome dão um 201 e dezenove 409 `NOME_JA_CADASTRADO`, **nunca 500** (Tasks 2 e 5).
2. **Troca de gestor do setor:** quem não tem o nível `Gestor` nunca gerencia setor; um gestor só responde por um setor; trocar ou tirar refaz o `ID_GESTOR` das pessoas **sem o gestor ser gestor de si mesmo** (o trigger recusaria), rebaixa o antigo, chama a porta dos pedidos parados, e **uma falha no meio desfaz tudo** (Task 5).
3. **"Gestor ausente":** vale **até o dia anterior** à data de volta, na virada do dia usa o dia de Brasília (23:30 `-03:00` ainda é o dia local), a data de volta tem de ser depois de hoje, quem só decide pedidos de gestores recebe 422 `GESTOR_SEM_EQUIPE`, e uma porta que falha desfaz a marcação (Task 4).
4. **Arquivo:** o tipo vem do conteúdo (um `.exe` chamado `.pdf` é recusado; um PDF chamado `.jpg` é aceito como PDF), 10 MiB + 1 byte, arquivo vazio e dois arquivos dão 400 em `arquivo`; o Blob só recebe depois de as validações (inclusive as lidas do banco) passarem e **nenhuma transação fica aberta durante o envio**; uma falha no envio não deixa linha e uma falha do `INSERT` apaga o blob; dois envios simultâneos para o mesmo pedido dão um 200 e um 422 **e sobra um só arquivo** (o do perdedor é apagado); nome com aspas ou quebra de linha não vira cabeçalho (Tasks 6, 7 e 8).
5. **Documento de outra pessoa:** `/api/eu/documentos/{id}` de outra pessoa e o id de um documento usado na ficha errada dão o **mesmo 404** do id inexistente; o gestor recebe 403 em todas as rotas de documentos e o contrato não tem rota de documentos para ele; a bolinha `documentosPendentes` conta só os pendentes da própria pessoa (Tasks 7 e 8).

---

## Antes de começar

**Pré-requisito:** a 1a e a **1b** estão prontas (`dotnet test back-end` e `docker compose up -d --wait` verdes). A 1c é a última da fatia 1. A 1a, a 1b e a 1c seguem as mesmas decisões de coordenação (entrada, `2026-10-30-plano-back-end.md`): **cada peça tem um dono e quem não é o dono só a consome**. Antes da Task 1, confira a tabela abaixo contra os arquivos reais da 1a e da 1b e use o nome real, mantendo o significado; **nunca crie uma segunda versão de uma peça que a tabela dá a outra fatia**.

**O que li** (arquivo e linha de 2026-10-30; se o arquivo mudou, vale o arquivo):

- Contrato `20-fatia-1-colaboradores.yaml`: documentos `:914-1176`; cargos `:1181-1273`; setores `:1278-1440`; gestores e ausência `:1445-1681`; benefícios `:1686-1822`; empresa `:1827-1919`; schemas `:3043-3279`; respostas de erro `:2570-2637`; parâmetros `:2481-2516`. Das 43 operações do arquivo, **21 são desta fatia** (tabela da autorrevisão); as outras são da 1a (auditoria: 3) e da 1b (colaboradores, equipe, usuários do sistema, senha: 19).
- `CONVENCOES.md`: seção 9 `:70-86` ("Gestor ausente", "setor que fica sem gestor ativo", "só quem tem perfil `Gestor` é gestor de setor"); seção 6 `:56-59`; seção 11 `:103-110`; seção 12 `:112-114`.
- Banco (`V001`): `CONFIGURACOES_EMPRESA` (linha única; UF e tolerância com `CHECK`; tabela nasce vazia); `GESTORES.APROVACAO_AUTOMATICA` e `_ATE` ("a APLICAÇÃO decide se a chave vale", sem agendador; nota «(antigo V011)»); `FUNCIONARIOS_DOCUMENTOS` (`DATA_CADASTRO DEFAULT CURRENT_TIMESTAMP`; `ENVIADO` exige arquivo e data de envio); `BENEFICIOS` (três marcas, padrão `FALSE`); `BENEFICIOS_FUNCIONARIOS` (ganha `STATUS_PEDIDO` **sem padrão**; pedido `PENDENTE` tem `BENEFICIO_ATIVO = FALSE`; nota «(antigo V021)»).
- Tabelas e triggers do grupo: `CARGOS` `Criacao_Tabelas:62-70`, `FUNCIONARIOS` `:81-105`, `SETORES` `:113-120`, `GESTORES` `:129-138`, `BENEFICIOS` `:536-542`, `BENEFICIOS_FUNCIONARIOS` `:548-563`; `TRG_FUNCIONARIOS_BEFORE_INSERT/UPDATE` recusam "gestor de si mesmo" e gestor inativo (`Criacao_Triggers:75-84`, `:105-113`); `TRG_GESTORES_BEFORE_INSERT` recusa funcionário inativo (`:154-168`); `TRG_PONTOS_MANUTENCOES_BEFORE_UPDATE` e `TRG_FERIAS_BEFORE_UPDATE` exigem `ID_GESTOR` na decisão final (`:535-567`, `:950-990`); `TRG_FUNCIONARIOS_AFTER_UPDATE` (`:117-140`).
- Plano da 1a: as interfaces da seção "Para as fatias 1b e 1c" (inclusive a porta `IPedidosParadosNoGestor`, o composto padrão vazio e `PedidosAprovados`, da Task 13), os testes automáticos que valem para a 1c (`ConformidadeDoModeloTests`, `MatrizDePerfisTests`, `ContratoEStatusTests`, `GuardaDeExecuteUpdateTests`) e o formato do `STATUS.md` (Task 7: tabela por fatia mais a seção "Rotas implementadas").
- Plano da 1b: a seção "Para as fatias 1c e 2" e as peças que a tabela "O que conferir" lista (Task 1: `BeneficioDoFuncionario`, `NomesDeNivel`, `NivelDeAcesso`, `Cargo.CargaSemanalHoras`, `Setor.DefinirGestor`; Task 3: `ProblemaDeApiException`; lista e ficha: `GestorReferencia`, `SetorReferencia`; cadastro e edição: `IGestorDoFuncionario`).
- Fatia 0: `Cargo { Id, Nome }` e `Setor { Id, Nome }` "só leitura, a 1c as estende" (Task 5, "Interfaces → Produces"); `docker compose` só com MySQL e "Azurite entra na fatia que o usa" (Task 3, e "Decisões deste plano", o item riscado "Sem biblioteca de rotas nem de UI nesta fatia", que diz que Mailpit e Azurite entram só nas fatias que os usam); fuso `-03:00` ("Decisões deste plano", o item "Fuso horário").
- `ELENCO.md:7` e `:51` (setor sem gestor; a lista de gestores tem 5 linhas: 3 de setor e 2 do RH só para decidir pedidos de gestores).

**O que suponho** (não medido; o teste indicado confirma ou derruba):

- Pomelo mapeia `TimeOnly`↔`TIME`, `DateOnly`↔`DATE`, `decimal(5,2)` e `TINYINT` como `int` com `HasColumnType("tinyint")` (Task 1, ida e volta).
- `DEFAULT CURRENT_TIMESTAMP` aparece como `DEFAULT_GENERATED` e é tratado como o `DEFAULT (UTC_TIMESTAMP(6))` da 1a: mapeado como gerado na inclusão (os testes de conformidade da 1a o provam).
- O MySQL **arredonda** (não trunca) a fração de segundo ao gravar em `DATETIME` sem fração (Task 1 mede).
- `.DisableAntiforgery()` é necessário nas rotas de upload e uma rota de formulário que recebe JSON responde **415**, não 500 (Task 7; a matriz da 1a prova).
- O SDK novo do Blob fala uma versão de API que o Azurite aceita (Task 6; se recusar, subir a versão do Azurite ou, só então, `--skipApiVersionCheck`).
- **A etapa do pedido é decidida na criação e só muda por ação explícita** (`CONVENCOES.md`, seção 9): `IGestorAusente.EtapaInicialDoPedidoAsync` diz quem decide primeiro quando o pedido nasce; **"passar ao RH" é uma chamada explícita de quem tira o gestor** (`IPedidosParadosNoGestor.PassarParaORhAsync`): a 1b a chama ao efetivar o desligamento de um gestor e na readmissão, **a 1c ao trocar ou tirar o gestor de um setor**; e "Gestor ausente" ligado chama `AprovarEtapaDoGestorAsync`. Como as fatias 3 e 7 guardam e derivam a etapa nas colunas do `V001` (`RESULTADO_GESTOR`, `ETAPA_GESTOR_AUTOMATICA`, `ID_GESTOR`) é decisão delas (seção "Para as fatias 2 em diante"); **a 1c não supõe nenhum modelo de colunas**, só o significado das duas portas.

**O que conferir do que a 1a e a 1b entregaram** (abra o arquivo e anote o nome real. **A 1c só consome: não duplique nenhuma peça desta tabela.** Se o nome real diferir, use o real mantendo o significado e **não** crie uma segunda versão):

| Peça (assinatura esperada) | Dono | O que a 1c faz com ela | Se faltar ou diferir |
|---|---|---|---|
| `NivelDeAcesso : IAuditavel` (`NomeParaAuditoria`: "Colaborador", "Gestor" ou "RH") e `NomesDeNivel.DoPerfil(Perfil)` (`Rh` → "RH"; `Funcionario` → "Funcionario"; `Gestor` → "Funcionario Gestor") | 1b, Task 1 | a Task 5 usa `DoPerfil` para achar o nível a dar ou tirar e para reconhecer o nível `RH`; **não** cria `NomesDeNivel` nem mexe em `NivelDeAcesso` | nome diferente: use o real; **não** crie outra classe |
| `Cargo.CargaSemanalHoras` (`decimal?`, `CARGA_HORARIA`, `HasPrecision(5, 2)`) e o mapeamento da coluna | 1b, Task 1 | **não remapeia**; acrescenta `Descricao`, `Criar` e `Atualizar` (que gravam a carga) | a 1b a declarou `{ get; }` (só leitura): trocar por `{ get; private set; }`, edição mecânica na linha dela, sem novo mapeamento |
| `Setor.GestorId` (1a) e `Setor.DefinirGestor(int? idGestor)` (o id é o de `GESTORES`) | 1a e 1b, Task 1 | chama `DefinirGestor(gestorNovo?.Id)`; **não** define outro `DefinirGestor`; acrescenta `Descricao`, `Criar`, `Atualizar` e, se ainda não existir, a navegação `Gestor? Gestor` | nome do parâmetro ou do método diferente: use o real |
| `GestorReferencia(int GestorId, int FuncionarioId, string Nome)`, `CargoReferencia(int Id, string Nome)` e `SetorReferencia(int Id, string Nome)` em `RHSystem.Application.Colaboradores` | 1b (lista e ficha) | usa (`SetorItem.Gestor`, `GestorItem.Setor`); **não** cria | **`FuncionarioResumo` a 1b não criou** (o `FuncionarioEquipeItem` dela tem os campos soltos): a Task 4 o cria em `Application/Colaboradores/FuncionarioResumo.cs` |
| `BeneficioDoFuncionario` (`RHSystem.Domain.Beneficios`: `Id`, `FuncionarioId`, `BeneficioId`, `Ativo`, `Status` do tipo `StatusDoPedidoDeBeneficio`), `DbSet BeneficiosDosFuncionarios`, já mapeada e fora de `tabelas-sem-mapeamento.txt` | 1b, Task 1 | **substitui o antigo `VinculoDeBeneficio`**: a Task 2 lê `Ativo` e `BeneficioId`; **não** cria entidade, `DbSet` nem configuração e **não** mexe em `BENEFICIOS_FUNCIONARIOS` no `tabelas-sem-mapeamento.txt` | a fatia 7 a estende, não cria outra |
| `IGestorDoFuncionario.RefazerDosMembrosAsync(int setorId, CancellationToken ct)` → `Task<int>` (`RHSystem.Application.Colaboradores`) | 1b (cadastro e edição) | `criarSetor` e `editarSetor` a chamam **na mesma transação, depois de salvar o setor**, sempre que o gestor do setor muda; **não** reimplementa a regra | o `422 GESTOR_DO_SETOR_INATIVO` dela nunca chega à 1c, que recusa gestor inativo antes (`FUNCIONARIO_INATIVO`) |
| `Funcionario.DefinirNivel(NivelDeAcesso)` | **ninguém definiu** (a 1b grava `NivelDeAcessoId` dentro de `AtualizarCadastro`) | a 1c cria `void DefinirNivel(NivelDeAcesso nivel)` (Task 1) | a 1b marcou `NivelDeAcessoId` com `[Auditar]`: a promoção também aparece em `Colaboradores` (aceitável; os testes da Task 5 filtram por área) |
| `IPedidosParadosNoGestor` com `Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct)` e `Task PassarParaORhAsync(int gestorId, CancellationToken ct)`, o record `PedidosAprovados` (`Application.Colaboradores`) e o composto padrão (**vazio**) registrado como `IPedidosParadosNoGestor` | 1a | só **chama**: "Gestor ausente" ligado → `AprovarEtapaDoGestorAsync` (Task 4); trocar ou tirar o gestor do setor → `PassarParaORhAsync` (Task 5). As fatias 3 e 7 registram a implementação delas (`IPedidosParadosNoGestorDaFatia`) | a 1c **não** cria interface, record nem implementação, e injeta só `IPedidosParadosNoGestor`; os testes usam o `PedidosParadosEspiao` (Task 4) |
| Escopo de `ContratoDivergenciaTests` por **lista de `operationId`** (o comparador recebe o conjunto de `operationId`, não um predicado de caminho) | fatia 0, Task 8 | só **acrescenta** os `operationId` dela, no passo que implementa as rotas (Tasks 2, 3, 4, 5, 7 e 8) | escopo ainda por prefixo de caminho: **pare e avise o dono** (erro da fatia 0: `/api/rh/beneficios` pega `/api/rh/beneficios-adesoes`); a 1c não o conserta por conta própria |
| O comparador (fatia 0, Task 8) e o validador de resposta (1a, Task 7) tratam `allOf` (unem `properties` e `required` dos membros, resolvendo `$ref`) | fatia 0 e 1a | as listas paginadas e `GestorAusenciaResposta` usam `allOf` no contrato | se acusarem "campo a mais" ou "campo faltando" nelas, o erro é do comparador ou do validador, não do contrato nem da 1c |
| O comparador (fatia 0, Task 8) compara `multipart/form-data` (nomes dos campos e `required`; arquivo = `format: binary`), com teste próprio | fatia 0, Task 8 | as rotas de envio de arquivo (Tasks 7 e 8) têm corpo `multipart/form-data` | se o comparador só olhar JSON, **pare e avise o dono**: o envio ficaria fora da divergência sem ninguém notar; a 1c não o conserta por conta própria |
| `ProblemaDeApiException(int status, string titulo, string detalhe, string? codigo = null, extensoes = null)` | 1b, Task 3 | a Task 3 a usa para o 404 `EMPRESA_NAO_CONFIGURADA`; a 1c **não** acrescenta `codigo` a `NaoEncontradoException` | |
| Detector de `1062` (chave repetida) | ninguém expõe: a 1a traduz o `1062` para 409 no tratamento global, sem classe de apoio | a 1c cria `ErrosDoBanco` (`1062` e `1451`) na Task 1 | se a 1b criou um detector de `1062`, reuse-o e só acrescente o `1451` |
| `SementeDoCadastro` (`CargoAsync(nome, cargaSemanal)`, `BeneficioAsync(nome)`, ...) e `CpfsDeTeste` | 1b, Task 1 | usa no caso simples; o `ApoioDeTeste` da 1c fica **só** com o que a 1b não tem (Task 1, Step 3) | |
| `ParametrosDeLista`, `MapaDeOrdenacao<T>`, `BuscaLike`, `ToRespostaPaginadaAsync` | 1a, Task 4 | as listas desta fatia declaram só `pagina`, `tamanhoPagina` e `busca` | `[AsParameters] ParametrosDeLista` só onde o contrato declara os cinco parâmetros (Task 2) |
| Seção "Rotas implementadas" do `STATUS.md` e o teste `STATUS_md_lista_toda_rota_implementada_e_toda_rota_listada_existe_na_API` | 1a, Task 7 | cada tarefa que entrega rotas acrescenta as linhas delas | |
| `GESTOR_PRECISA_TER_PERFIL_GESTOR` | 1b (editar o nível de quem gerencia setor) **e** 1c (escolher alguém do RH como gestor de setor) | cada uma usa o `detail` do exemplo **dela** no contrato (textos diferentes, mesmo `codigo`); o da 1c é `MensagensDoApoio.GestorPrecisaTerPerfilGestor` | |
| `IGeradorDeMatriculas`, `IFilaDeEmails` | 1a | a 1c **não** os usa (documentos não geram e-mail) | |

## Estrutura de arquivos (o que esta fatia cria; caminhos sob `back-end/`)

- `src/RHSystem.Domain/`: `Empresa/ConfiguracaoDaEmpresa.cs`; `Documentos/DocumentoDoColaborador.cs`, `TipoDeDocumento.cs`, `SituacaoDoDocumento.cs`. Modifica `Cargo`, `Setor`, `Gestor`, `Beneficio` e, se faltar, `Funcionario` (`DefinirNivel`). **Não** cria nem altera `NivelDeAcesso`, `NomesDeNivel` nem `BeneficioDoFuncionario` (são da 1b).
- `src/RHSystem.Application/`: `Cadastros/` (casos de uso e DTOs de cargos, setores, benefícios; `MensagensDoApoio`); `Gestores/` (casos de uso, `IGestorAusente`; **a porta `IPedidosParadosNoGestor` é da 1a**); `Colaboradores/FuncionarioResumo.cs`; `Empresa/`; `Arquivos/`; `Documentos/`.
- `src/RHSystem.Infrastructure/`: `Cadastros/`, `Gestores/`, `Empresa/`, `Documentos/` (repositórios e consultas); `Arquivos/` (`ArmazenamentoBlob`, `OpcoesDeArmazenamento`, `Tarefas/LimparArquivosOrfaosTarefa`); `Persistencia/ErrosDoBanco.cs`.
- `src/RHSystem.Api/`: `Cadastros/CargosEndpoints.cs`, `SetoresEndpoints.cs`, `GestoresEndpoints.cs`, `BeneficiosEndpoints.cs`; `Empresa/EmpresaEndpoints.cs`; `Documentos/DocumentosEndpoints.cs`.
- `tests/RHSystem.Api.IntegrationTests/`: `Cadastros/`, `Gestores/`, `Empresa/`, `Documentos/`, `Arquivos/`; `Infra/ArmazenamentoEmMemoria.cs`, `UnidadeDeTrabalhoEspia.cs`, `PedidosParadosEspiao.cs`, `GestorDoFuncionarioEspiao.cs`, `AzuriteFixture.cs`, `ArquivosDeTeste.cs`, `ApoioDeTeste.cs` (o que a `SementeDoCadastro` da 1b não tem).

**Rotas e códigos** (todos de `20-fatia-1-colaboradores.yaml`; `x-perfis` segue a família: `/api/rh` só `RH`, `/api/eu` os três e **sem 403**):

| operationId | Rota | Sucesso | Erros declarados |
|---|---|---|---|
| `listarCargos` / `criarCargo` / `editarCargo` | `GET`, `POST /api/rh/cargos`; `PUT .../{cargoId}` | 200 / 201 / 200 | 400 401 403 500; criar +409; editar +404 +409 |
| `listarSetores` / `criarSetor` / `editarSetor` | `GET`, `POST /api/rh/setores`; `PUT .../{setorId}` | 200 / 201 / 200 | 400 401 403 500; criar +409 +422; editar +404 +409 +422 |
| `listarGestores` / `cadastrarGestor` / `configurarAusenciaDoGestor` | `GET`, `POST /api/rh/gestores`; `PUT .../{gestorId}/ausencia` | 200 / 201 / 200 | listar: 401 403 500; cadastrar: 400 401 403 409 422 500; ausência: 400 401 403 404 422 500 |
| `listarBeneficiosDoCatalogo` / `criarBeneficioDoCatalogo` / `editarBeneficioDoCatalogo` / `excluirBeneficioDoCatalogo` | `GET`, `POST /api/rh/beneficios`; `PUT`, `DELETE .../{beneficioId}` | 200 / 201 / 200 / 204 | listar: 400 401 403 500; criar +409; editar +404 +409; excluir: 401 403 404 409 500 |
| `obterEmpresa` / `salvarEmpresa` | `GET /api/eu/empresa`; `PUT /api/rh/empresa` | 200 / 200 | obter: 401 404 500 (**sem 403**); salvar: 400 401 403 500 |
| `listarDocumentosDoFuncionario` / `adicionarDocumentoDoFuncionario` / `baixarDocumentoDoFuncionario` | `GET`, `POST /api/rh/funcionarios/{funcionarioId}/documentos`; `GET .../{documentoId}/download` | 200 / 201 / 200 | listar: 401 403 404 500; adicionar: 400 401 403 404 500; baixar: 401 403 404 422 500 |
| `listarMeusDocumentos` / `enviarMeuDocumento` / `baixarMeuDocumento` | `GET /api/eu/documentos`; `POST .../{documentoId}/arquivo`; `GET .../{documentoId}/download` | 200 / 200 / 200 | listar: 401 500; enviar: 400 401 404 422 500; baixar: 401 404 422 500 |

---

### Task 1: Mapear o apoio, a empresa e os documentos, e as mensagens do contrato

**Files:**
- Create (Domain): `Empresa/ConfiguracaoDaEmpresa.cs`; `Documentos/DocumentoDoColaborador.cs`, `TipoDeDocumento.cs`, `SituacaoDoDocumento.cs`
- Modify (Domain): `Autenticacao/Cargo.cs` (`Descricao`, `Criar`, `Atualizar`; `CargaSemanalHoras` com `private set`), `Setor.cs` (`Descricao`, `Criar`, `Atualizar`, navegação `Gestor`), `Funcionario.cs` (só `DefinirNivel`, se faltar); `Colaboradores/Gestor.cs`; `Beneficios/Beneficio.cs`
- Create (Application): `Cadastros/MensagensDoApoio.cs`
- Create (Infrastructure, em `Persistencia/`): `Configuracoes/ConfiguracaoDaEmpresaConfiguracao.cs`, `DocumentoDoColaboradorConfiguracao.cs`; `ErrosDoBanco.cs`
- Modify (Infrastructure): `Persistencia/AppDbContext.cs` (DbSets `ConfiguracoesDaEmpresa` e `Documentos`); as configurações de `Cargo` (só `Descricao`), `Setor`, `Gestor` e `Beneficio`
- Test: `tests/RHSystem.Domain.Tests/Apoio/` (`CargoTests.cs`, `SetorTests.cs`, `GestorTests.cs`, `BeneficioTests.cs`, `ConfiguracaoDaEmpresaTests.cs`, `DocumentoDoColaboradorTests.cs`); `tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoDoApoioTests.cs`; `Auditoria/AuditoriaDoApoioTests.cs`; `Contrato/MensagensDoApoioTests.cs`; `Infra/ApoioDeTeste.cs`; `Persistencia/tabelas-sem-mapeamento.txt`

**Interfaces:**
- Consumes: `AppDbContext`, `Funcionario`, `Perfil`, `Cargo`, `Setor` (fatia 0); `Gestor`, `Beneficio`, `Setor.GestorId`, `ConversoresDeTempo.Utc/.Brasil`, `EnumComoTextoMaiusculo<T>`, `[Auditado]`, `[Auditar]`, `IAuditavel`, `AuditoriaArea` (1a, Tasks 1 e 8); `CarregadorDeContrato` (1a, Task 7); **da 1b (só consome, tabela "O que conferir")**: `NivelDeAcesso`, `NomesDeNivel.DoPerfil(Perfil)`, `Cargo.CargaSemanalHoras` e o mapeamento de `CARGA_HORARIA`, `Setor.DefinirGestor(int? idGestor)`, `BeneficioDoFuncionario`.
- Produces (Domain; setters privados, o EF preenche; todo `Criar`/`Atualizar` recusa com `ArgumentException` o que viola as regras abaixo):

```csharp
// RHSystem.Domain.Autenticacao   (a 1c ACRESCENTA; NivelDeAcesso : IAuditavel, NomesDeNivel, CargaSemanalHoras e Setor.DefinirGestor(int?) são da 1b)
// Cargo ganha:   string? Descricao;   // CargaSemanalHoras já existe (1b; passa a { get; private set; }): nome 1..100 (Trim), descrição até 255, carga > 0 e <= 168 com até 2 casas
//                static Cargo Criar(string nome, string? descricao, decimal cargaSemanalHoras); void Atualizar(string nome, string? descricao, decimal cargaSemanalHoras);
// Setor ganha:   string? Descricao; Gestor? Gestor;   // GestorId (1a) e DefinirGestor(int? idGestor) (1b) já existem; a navegação Gestor só se ainda não existir
//                static Setor Criar(string nome, string? descricao); void Atualizar(string nome, string? descricao);
// Funcionario ganha (só se a 1b não tiver):  void DefinirNivel(NivelDeAcesso nivel);   // nível nulo: ArgumentNullException; grava NivelDeAcessoId
// RHSystem.Domain.Colaboradores
// Gestor ganha:  Funcionario Funcionario; bool Ausente; DateOnly? AusenteAte;   // colunas APROVACAO_AUTOMATICA e APROVACAO_AUTOMATICA_ATE (V001); implementa IAuditavel
//                static Gestor Criar(Funcionario funcionario); bool AusenteEm(DateOnly dia);   // Ausente && (AusenteAte == null || dia < AusenteAte)
//                void MarcarAusente(DateOnly? ateDia, DateOnly hoje);   // ateDia <= hoje: ArgumentException
//                void DesmarcarAusencia();                               // zera a data (o CHECK do V001 exige)
// RHSystem.Domain.Beneficios
// Beneficio ganha: static Beneficio Criar(string nome, string? descricao, bool opcional, bool aceitaDependentes, bool aceitaTrajeto); void Atualizar(<os mesmos cinco>);
// RHSystem.Domain.Empresa
class ConfiguracaoDaEmpresa { const byte IdUnico = 1; byte Id; string NomeEmpresa; string Uf; string Municipio; TimeOnly HorarioEntrada; TimeOnly HorarioSaida;
  int ToleranciaAtrasoMinutos; int DiaPagamento;   // nome 1..150, município 1..100, UF das 27 siglas em maiúsculas, tolerância 0..120, dia 1..31; saída < entrada é válido (turno que atravessa a meia-noite)
  static IReadOnlySet<string> UfsValidas { get; }
  static ConfiguracaoDaEmpresa Criar(string nomeEmpresa, string uf, string municipio, TimeOnly entrada, TimeOnly saida, int toleranciaAtrasoMinutos, int diaPagamento);
  void Atualizar(<os mesmos sete>); }
// RHSystem.Domain.Documentos
enum TipoDeDocumento { Identidade, Cpf, CarteiraDeTrabalho, ComprovanteDeResidencia, ContratoAssinado, ExameAdmissional, Outro }   // texto no banco: IDENTIDADE, CPF, CARTEIRA_DE_TRABALHO...
enum SituacaoDoDocumento { Pendente, Enviado }
class DocumentoDoColaborador : IAuditavel { int Id; int FuncionarioId; Funcionario Funcionario; TipoDeDocumento Tipo; string? Descricao; DateOnly? DataValidade;
  SituacaoDoDocumento Situacao; string? CaminhoArquivo; string? NomeArquivoOriginal; DateTime? PedidoEmUtc; DateTime? EnviadoEmUtc; DateTime CadastradoEm;
  static DocumentoDoColaborador Anexar(Funcionario f, TipoDeDocumento tipo, string? descricao, DateOnly? dataValidade, string caminho, string nomeOriginal, DateTime agoraUtc);   // nasce Enviado
  static DocumentoDoColaborador Pedir(Funcionario f, TipoDeDocumento tipo, string? descricao, DateOnly? dataValidade, DateTime agoraUtc);                                       // nasce Pendente, PedidoEmUtc = agora
  void ReceberArquivo(string caminho, string nomeOriginal, DateTime agoraUtc); }   // só Pendente (senão InvalidOperationException); descrição até 255, nome do arquivo até 255; instantes truncados ao segundo
// BENEFICIOS_FUNCIONARIOS: a entidade é a BeneficioDoFuncionario da 1b (a 1c não cria outra; a fatia 7 a estende)
```

- Produces (Application): `static class MensagensDoApoio` com os **textos e códigos copiados dos exemplos do contrato** (nunca reescritos):

| Constante ou método | Código | Texto (`detail`) |
|---|---|---|
| `NomeJaCadastrado` | `NOME_JA_CADASTRADO` | `Já existe um cadastro com este nome.` |
| `GestorJaCadastrado` | `GESTOR_JA_CADASTRADO` | `Esta pessoa já está cadastrada como gestor.` |
| `GestorSemSetorSoParaRh` | `GESTOR_SEM_SETOR_SO_PARA_RH` | `Só quem é do RH é cadastrado como gestor sem setor. Para gerenciar um setor, escolha a pessoa no cadastro de Setores.` |
| `GestorJaRespondePorOutroSetor(nomeGestor, nomeSetor)` | `GESTOR_JA_RESPONDE_POR_OUTRO_SETOR` | `{nomeGestor} já é o gestor do setor {nomeSetor}. Tire-o de lá antes de escolhê-lo aqui.` |
| `GestorPrecisaTerPerfilGestor(nome)` | `GESTOR_PRECISA_TER_PERFIL_GESTOR` | `{nome} tem o nível RH. Só quem tem o nível Gestor pode gerenciar um setor.` |
| `GestorSemEquipe(nome)` | `GESTOR_SEM_EQUIPE` | `{nome} só decide pedidos de gestores e não tem equipe. Não há etapa do gestor para aprovar sozinha.` |
| `FuncionarioInativo` | `FUNCIONARIO_INATIVO` | `A pessoa escolhida está inativa.` |
| `CadastroEmUso` | `CADASTRO_EM_USO` | `Este benefício já foi dado a colaboradores e não pode ser excluído.` |
| `EmpresaNaoConfigurada` | `EMPRESA_NAO_CONFIGURADA` | `A empresa ainda não foi configurada.` |
| `DocumentoSemArquivo` | `DOCUMENTO_SEM_ARQUIVO` | `Este documento ainda não foi enviado.` |
| `DocumentoNaoPedido` | `DOCUMENTO_NAO_PEDIDO` | `Este documento não está aguardando envio.` |

- Produces (Infrastructure): `DbSet<ConfiguracaoDaEmpresa> ConfiguracoesDaEmpresa`, `DbSet<DocumentoDoColaborador> Documentos`; `static class ErrosDoBanco { static bool EhChaveRepetida(Exception e); static bool EhChaveEstrangeiraEmUso(Exception e); }` (`MySqlException` 1062 e 1451, na exceção ou em qualquer `InnerException`; **se a 1b já criou um detector de 1062, reuse-o** e só acrescente o 1451).

**Cuidados de mapeamento** (as regras da 1a, Task 1, "Cuidados de mapeamento", aplicadas às colunas desta fatia; o `ConformidadeDoModeloTests` da 1a prova cada linha):

| Coluna | Como mapear |
|---|---|
| `GESTORES.APROVACAO_AUTOMATICA` (`DEFAULT FALSE`) | propriedade comum, **sem declarar o padrão** (o EF sempre envia o valor); `APROVACAO_AUTOMATICA_ATE` `DateOnly?` |
| `FUNCIONARIOS_DOCUMENTOS.STATUS_DOCUMENTO` (`DEFAULT 'PENDENTE'`) | `EnumComoTextoMaiusculo<SituacaoDoDocumento>`, **sem padrão**, **`IsConcurrencyToken()`** (o `UPDATE` leva `WHERE STATUS_DOCUMENTO = 'PENDENTE'` e fecha a corrida da Task 8) |
| `FUNCIONARIOS_DOCUMENTOS.DATA_CADASTRO` (`DEFAULT CURRENT_TIMESTAMP`) | `ValueGeneratedOnAdd()`, sem padrão, `ConversoresDeTempo.Brasil` (é tabela do `V001`: `-03:00`) |
| `DATA_PEDIDO`, `DATA_ENVIO` | `ConversoresDeTempo.Brasil` (a aplicação grava; nenhum trigger as preenche) |
| `TIPO_DOCUMENTO` | `EnumComoTextoMaiusculo<TipoDeDocumento>` (texto livre no banco, sem `CHECK`: valor desconhecido lido do banco lança; só a aplicação grava) |
| `CONFIGURACOES_EMPRESA.ID_CONFIGURACAO_EMPRESA` (`TINYINT DEFAULT 1`) | chave com `ValueGeneratedNever()`, `byte`, sem padrão; a entidade define `Id = 1` |
| `TOLERANCIA_ATRASO_MINUTOS` (`SMALLINT`, `DEFAULT 0`), `DIA_PAGAMENTO` (`TINYINT`) | `int` com `HasColumnType("smallint")` e `("tinyint")`; **sem padrão** |
| `HORARIO_ENTRADA`, `HORARIO_SAIDA` (`TIME`) | `TimeOnly` |
| `CARGOS.CARGA_HORARIA` (`DECIMAL(5,2)`, aceita `NULL`) | **da 1b** (`decimal?`, `HasPrecision(5, 2)`): a 1c não remapeia; só troca o `get` por `private set` |
| `BENEFICIOS_FUNCIONARIOS` | **da 1b** (`BeneficioDoFuncionario`, já mapeada, com `STATUS_PEDIDO` sem padrão): a 1c não mapeia nada aqui |

**Marcas de auditoria** (a 1a marcou nome, `GestorId`, e as três marcas do benefício; aqui entram as que faltam; a 1a fixou o texto de cada evento): `Cargo`: `Descricao` ("Descrição"), `CargaSemanalHoras` ("Carga semanal"; a propriedade é da 1b, a marca é da 1c); `Setor`: `Descricao`; **`Gestor`** `[Auditado(Cadastros, "Gestor")]`: `Ausente` ("Gestor ausente"), `AusenteAte` ("Volta em"); **`ConfiguracaoDaEmpresa`** `[Auditado(Cadastros, "Empresa")]`: `NomeEmpresa` ("Nome da empresa"), `Uf` ("UF"), `Municipio` ("Município"), `HorarioEntrada` ("Horário de entrada"), `HorarioSaida` ("Horário de saída"), `ToleranciaAtrasoMinutos` ("Tolerância de atraso"), `DiaPagamento` ("Dia de pagamento"); **`DocumentoDoColaborador`** `[Auditado(Documentos, "Documento")]`: `Tipo`, `Descricao` ("Descrição"), `DataValidade` ("Validade"), `Situacao` ("Situação"), `NomeArquivoOriginal` (`[Auditar("Arquivo", Sensivel = true)]`: decisão 6). **`CaminhoArquivo` nunca leva `[Auditar]`.** `NomeParaAuditoria` e `RegistroAfetado`: `Gestor` = nome da pessoa (`Funcionario.Nome`, ou `Gestor {Id}` se a navegação não veio) e `Gestor {nome}`; `Documento` = o rótulo do tipo e `Documento {rótulo} de {nome da pessoa}`; `Empresa` = o nome da empresa e `Configuração da empresa`.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Domain.Tests/Apoio (as regras de cada Criar/Atualizar; valores do contrato)
Cargo_aceita_carga_ate_168_com_duas_casas_e_recusa_zero_negativa_acima_de_168_e_tres_casas()   // 44, 44.5, 168 ok; 0, -1, 168.01, 44.555 -> ArgumentException
Cargo_e_Setor_normalizam_o_nome_com_Trim_e_recusam_nome_vazio_ou_acima_de_100()
Gestor_AusenteEm_vale_ate_o_dia_anterior_a_volta_e_sem_data_vale_enquanto_marcado()   // volta 2026-10-19: dia 18 true, dia 19 false; sem data: true; nao marcado: false
Gestor_MarcarAusente_com_volta_hoje_ou_antes_lanca_e_DesmarcarAusencia_zera_a_data()
Beneficio_Criar_e_Atualizar_guardam_as_tres_marcas_como_informadas()
ConfiguracaoDaEmpresa_valida_UF_da_lista_de_27_e_recusa_sp_ZZ_tolerancia_121_e_dia_0_e_32_e_aceita_saida_menor_que_entrada()
DocumentoDoColaborador_Anexar_nasce_Enviado_com_arquivo_e_Pedir_nasce_Pendente_sem_arquivo_com_PedidoEmUtc()
DocumentoDoColaborador_ReceberArquivo_so_vale_para_Pendente_e_preserva_PedidoEmUtc()
DocumentoDoColaborador_trunca_os_instantes_ao_segundo()                    // 12:42:00.700Z -> 12:42:00Z
Funcionario_DefinirNivel_troca_o_nivel_e_recusa_nulo()                      // só se a 1c criou o método (tabela "O que conferir")

// Api.IntegrationTests/Persistencia/MapeamentoDoApoioTests (banco real; releia com OUTRO AppDbContext)
Cargo_ida_e_volta_com_carga_decimal_e_com_carga_nula_lida_por_SQL()
Setor_ida_e_volta_com_descricao_e_gestor()
Gestor_grava_Ausente_true_e_false_como_enviados_e_a_data_de_volta_e_o_CHECK_do_V001_recusa_data_sem_marca()
ConfiguracaoDaEmpresa_ida_e_volta_com_TIME_smallint_e_tinyint_e_so_aceita_o_Id_1()   // segundo INSERT com Id 1: 1062; Id 2: CHECK
ConfiguracaoDaEmpresa_UF_minuscula_viola_o_CHECK_do_banco_e_horario_de_virada_da_noite_grava()
Documento_ida_e_volta_com_DATA_CADASTRO_do_banco_e_datas_gravadas_tres_horas_antes()   // SELECT cru: DATA_ENVIO = relógio - 3 h; DATA_CADASTRO dentro de 5 s do NOW() do banco; lidas em Kind=Utc
Documento_Pendente_e_Enviado_gravam_STATUS_como_enviados()                           // o padrão do banco não é declarado
Documento_ENVIADO_sem_arquivo_viola_o_CHECK_do_V001()
Instante_com_fracao_de_segundo_e_gravado_truncado_e_o_MySQL_arredondaria_o_que_nao_for_truncado()   // mede a suposição: grava 12:42:00.700 por SQL cru e lê 12:42:01
ErrosDoBanco_reconhece_1062_e_1451_dentro_de_DbUpdateException()                       // viola UQ_CARGOS_NOME e apaga benefício com BeneficioDoFuncionario (1b) apontando para ele

// Auditoria/AuditoriaDoApoioTests (DbContext do DI com o interceptor e o relógio falso; veio da Task 8.1 da 1a, que ainda não tem Setor.Criar)
Criar_setor_gera_um_evento_Criou_com_ator_ip_entidade_id_e_descricao_Setor_Compras()
// Contrato/MensagensDoApoioTests
Textos_e_codigos_de_MensagensDoApoio_sao_os_dos_exemplos_do_contrato()   // lê os `example` de Erro409CadastroNomeRepetido, Erro409Setor, Erro422Setor, da 409/422 de gestores, da 409 de benefícios, da 404 da empresa e das 422 de documentos (CarregadorDeContrato) e compara, trocando os nomes dos exemplos (Carlos Ribeiro, Comercial, Ana Souza)
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Domain.Tests.Apoio|FullyQualifiedName~MapeamentoDoApoioTests|FullyQualifiedName~AuditoriaDoApoioTests|FullyQualifiedName~MensagensDoApoioTests"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as entidades, as configurações, `MensagensDoApoio`, `ErrosDoBanco` e o que faltar em `Funcionario` (`DefinirNivel`; tabela "O que conferir"), com as assinaturas e a tabela de cuidados acima. **Não** criar `NomesDeNivel`, `NivelDeAcesso : IAuditavel`, `DefinirGestor` nem entidade de `BENEFICIOS_FUNCIONARIOS` (são da 1b). Acrescentar ao `ApoioDeTeste` os ajudantes de semente por SQL **que a `SementeDoCadastro` da 1b não tem** (a 1b já tem `CargoAsync(nome, cargaSemanal)` e `BeneficioAsync(nome)`: use-os no caso simples; mesma técnica do `SemearElencoAsync` da 1a, Task 6): `CriarCargoAsync(nome, descricao, cargaSemanalOuNull)`, `CriarBeneficioAsync(nome, descricao, opcional, aceitaDependentes, aceitaTrajeto)`, `DarBeneficioAsync(funcionarioId, beneficioId, ativo, statusDoPedido)` (`STATUS_PEDIDO` **sempre explícito**, sem padrão), `CriarSetorAsync(nome, gestorFuncionarioId?)`, `CriarPessoaAsync(nome, perfil, setorId?, ativo, admissao)` e `CriarDocumentoAsync(...)`.
- [ ] **Step 4: Atualizar `tabelas-sem-mapeamento.txt`:** tirar `CONFIGURACOES_EMPRESA` e `FUNCIONARIOS_DOCUMENTOS` (`BENEFICIOS_FUNCIONARIOS` a 1b já tirou).
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Persistencia|FullyQualifiedName~Domain.Tests|FullyQualifiedName~MensagensDoApoioTests|FullyQualifiedName~Auditoria"`
Expected: PASS, **inclusive** os testes genéricos da 1a (`ConformidadeDoModeloTests`, `AtributosDeAuditoriaTests`: toda entidade `[Auditado]` implementa `IAuditavel` e nenhuma propriedade `[Auditar]` se chama senha, hash ou token). Se um teste de conformidade falhar numa coluna desta fatia, o erro é do mapeamento: corrija o mapeamento, não o teste. Se o MySQL **não** arredondar (o teste do arredondamento falhar ao contrário), a truncagem fica por segurança e a decisão registrada no `README.md` do back-end.
- [ ] **Step 6: Commit** (com autorização): `git add back-end; git commit -m "feat: mapeamento do apoio, da empresa e dos documentos do colaborador"`

---

### Task 2: Cargos e benefícios do catálogo (`/api/rh/cargos` e `/api/rh/beneficios`)

**Files:**
- Create (Application): `Cadastros/CargosCasoDeUso.cs`, `BeneficiosCasoDeUso.cs`, `CadastrosDtos.cs`, `ICargosRepositorio.cs`, `IBeneficiosRepositorio.cs`
- Create (Infrastructure): `Cadastros/CargosRepositorio.cs`, `BeneficiosRepositorio.cs`; modify `DependencyInjection.cs`
- Create (Api): `Cadastros/CargosEndpoints.cs`, `BeneficiosEndpoints.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha os `operationId` `listarCargos`, `criarCargo`, `editarCargo`, `listarBeneficiosDoCatalogo`, `criarBeneficioDoCatalogo`, `editarBeneficioDoCatalogo` e `excluirBeneficioDoCatalogo`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Test: `tests/RHSystem.Api.IntegrationTests/Cadastros/CargosTests.cs`, `BeneficiosDoCatalogoTests.cs`

**Interfaces:**
- Consumes: `Cargo`, `Beneficio`, `MensagensDoApoio`, `ErrosDoBanco` (Task 1); `BeneficioDoFuncionario` e `DbSet BeneficiosDosFuncionarios` (**da 1b**: `Ativo`, `BeneficioId`); `ParametrosDeLista`, `MapaDeOrdenacao<T>`, `ToRespostaPaginadaAsync`, `BuscaLike` (1a, Task 4); `GrupoRh()`, `ProduzProblemas` (1a, Tasks 3 e 5); `ConflitoException`, `NaoEncontradoException`, `ColetorDeErros`, `OuNaoEncontrado` (1a, Task 3); `AfirmarConformeAoContratoAsync` (1a, Task 7).
- Produces (nomes dos schemas do contrato; JSON `camelCase`):

```csharp
// RHSystem.Application.Cadastros
sealed record CargoItem(int Id, string Nome, string? Descricao, decimal CargaSemanalHoras, int QuantidadePessoas);
sealed record CargoComando(string Nome, string? Descricao, decimal CargaSemanalHoras);          // os três obrigatórios; descricao pode ser null
sealed record CargoListaResposta(IReadOnlyList<CargoItem> Itens, int Pagina, int TamanhoPagina, int Total, int TotalPaginas) : RespostaPaginada<CargoItem>(...);
sealed record BeneficioCatalogoItem(int Id, string Nome, string? Descricao, bool Opcional, bool AceitaDependentes, bool AceitaTrajeto, int QuantidadePessoas);
sealed record BeneficioCatalogoComando(string Nome, string? Descricao, bool Opcional, bool AceitaDependentes, bool AceitaTrajeto);   // os cinco obrigatórios
sealed record BeneficioCatalogoListaResposta(...) : RespostaPaginada<BeneficioCatalogoItem>(...);
sealed class CargosCasoDeUso { Task<CargoListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct);
  Task<CargoItem> CriarAsync(CargoComando c, CancellationToken ct); Task<CargoItem> EditarAsync(int cargoId, CargoComando c, CancellationToken ct); }
sealed class BeneficiosCasoDeUso { Task<BeneficioCatalogoListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct);
  Task<BeneficioCatalogoItem> CriarAsync(BeneficioCatalogoComando c, CancellationToken ct); Task<BeneficioCatalogoItem> EditarAsync(int beneficioId, BeneficioCatalogoComando c, CancellationToken ct);
  Task ExcluirAsync(int beneficioId, CancellationToken ct); }
```

**Regras** (do contrato; ordem de avaliação: 404, 400, 409):
- **Listas:** só os parâmetros `pagina`, `tamanhoPagina` e `busca` (a rota **não** tem `ordenarPor` nem `direcao`: receba os três e monte `new ParametrosDeLista(pagina, tamanhoPagina, busca).Validado()`; **`[AsParameters] ParametrosDeLista` só vale onde o contrato declara os cinco parâmetros (`pagina`, `tamanhoPagina`, `busca`, `ordenarPor`, `direcao`): aqui acrescentaria os dois que faltam ao documento da API e a divergência acusaria**). `busca` procura em nome **e** descrição (`BuscaLike`). Cargos: ordem por nome (`MapaDeOrdenacao` com desempate `Id`); `quantidadePessoas` = pessoas **ativas** com o cargo (sem a condição de admissão: o texto do contrato difere do do setor; siga-o). Benefícios: ordem por `Id`; `quantidadePessoas` = linhas de `BeneficioDoFuncionario` com `Ativo = true` (pedido `PENDENTE` ou inativo não conta).
- **Cargo:** nome 1..100, descrição até 255 (ou `null`; vazia vira `null`), `cargaSemanalHoras` em `(0, 168]` com até 2 casas (400 em `cargaSemanalHoras`). **Não existe exclusão de cargo.** A nova carga vale só para os dias de ponto criados depois: nada a fazer aqui (o trigger copia no `INSERT` do ponto).
- **Nome único** (cargo e benefício; sem diferenciar maiúsculas **nem acentos**, decisão 4): verifica antes (`NomeEmUso`, ignorando o próprio registro na edição) e **também** traduz o `1062` do `SaveChanges` no repositório: ambos viram `ConflitoException(MensagensDoApoio.NomeJaCadastrado, codigo: NOME_JA_CADASTRADO)`. Editar mantendo o próprio nome não é conflito.
- **Benefício:** as três marcas são gravadas **como o RH enviou** (`true` e `false`; o padrão `FALSE` do `V001` não é declarado). **Excluir** só se **nenhuma** linha de `BeneficioDoFuncionario` (`BENEFICIOS_FUNCIONARIOS`) aponta para ele (qualquer `Status`, ativa ou não): senão 409 `CADASTRO_EM_USO`. A exclusão usa `Remove` rastreado (o interceptor grava `Excluiu`, "Benefício Vale-cultura excluído"), e um vínculo criado no meio da exclusão (`1451`) também vira 409 `CADASTRO_EM_USO`.
- Descrições são texto: `<b>x</b>` é guardado e devolvido como está.

- [ ] **Step 1: Escrever os testes que falham** (`ApiFactory` com o elenco; `SementeDoCadastro` (1b) e `ApoioDeTeste` para cargos, benefícios e pedidos de benefício)

```csharp
// CargosTests
Lista_devolve_nome_descricao_carga_e_quantidadePessoas_so_de_ativos_ordenada_por_nome()      // cargo com 2 ativos, 1 inativo e 1 em admissão: quantidade = 3 (admissão conta; o contrato só diz "ativas")
Busca_procura_no_nome_e_na_descricao_e_trata_percentual_como_literal()
Total_serve_ao_numero_do_menu_com_tamanhoPagina_1_e_tamanhoPagina_100_e_aceito_e_101_devolve_400()
Criar_devolve_201_com_quantidadePessoas_0_e_grava_Criou_na_area_Cadastros_com_ator_e_ip()
Criar_com_nome_repetido_sem_diferenciar_maiusculas_nem_acentos_devolve_409_NOME_JA_CADASTRADO_com_o_texto_do_contrato()   // "estagiario" x "Estagiário" (Review Focus 1)
Vinte_criacoes_simultaneas_do_mesmo_nome_dao_um_201_e_dezenove_409_e_nenhum_500()            // Barrier (Review Focus 1)
Carga_zero_negativa_acima_de_168_ou_com_tres_casas_devolve_400_em_cargaSemanalHoras()
Nome_vazio_so_espacos_ou_acima_de_100_devolve_400_em_nome_e_descricao_acima_de_255_em_descricao()
Corpo_sem_campo_obrigatorio_do_contrato_devolve_400()                                       // [Theory]: sem nome, sem cargaSemanalHoras
Descricao_com_marcacao_html_e_guardada_e_devolvida_como_texto_literal()
Editar_substitui_os_tres_campos_e_grava_Alterou_com_antes_e_depois()
Editar_cargo_inexistente_devolve_404_nome_de_outro_cargo_409_e_o_proprio_nome_nao_da_409()
Cargo_sem_carga_no_banco_sai_com_carga_0_e_a_lista_nao_quebra()                              // decisão 10: SQL cru com CARGA_HORARIA NULL
Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
Respostas_batem_com_o_schema_do_contrato()                                                   // AfirmarConformeAoContratoAsync em listarCargos, criarCargo e editarCargo
// BeneficiosDoCatalogoTests
Lista_ordenada_por_id_com_as_tres_marcas_e_quantidadePessoas_so_dos_vinculos_ativos()        // vínculo inativo e pedido PENDENTE não contam
Criar_e_editar_gravam_true_e_false_das_marcas_como_enviados()                                // V001: o EF não pode omitir o false
Nome_repetido_409_e_vinte_criacoes_simultaneas_dao_um_201_e_nenhum_500()
Excluir_beneficio_sem_vinculo_devolve_204_e_grava_Excluiu_com_a_descricao_Beneficio_Vale_cultura_excluido()
Excluir_com_qualquer_vinculo_ativo_inativo_ou_pedido_pendente_devolve_409_CADASTRO_EM_USO_e_nao_exclui()
Excluir_com_vinculo_criado_depois_de_carregar_o_beneficio_devolve_409_e_nunca_500()          // teste no repositório: carrega, insere o vínculo por SQL, exclui (FK 1451)
Editar_e_excluir_inexistente_devolvem_404()
Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_quatro_rotas()
Respostas_batem_com_o_schema_do_contrato()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~CargosTests|FullyQualifiedName~BeneficiosDoCatalogoTests"`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar** DTOs, casos de uso, repositórios (a tradução do `1062` mora no repositório, em `AdicionarAsync` e `SalvarAsync`) e as sete rotas em `app.GrupoRh()`, com `WithName(operationId)`, `Produces<T>` e `ProduzProblemas(...)` com **exatamente** os códigos da tabela "Rotas e códigos". **Neste passo, junto com as rotas** (nunca antes), acrescentar à lista de escopo de `ContratoDivergenciaTests` os sete `operationId`: `listarCargos`, `criarCargo`, `editarCargo`, `listarBeneficiosDoCatalogo`, `criarBeneficioDoCatalogo`, `editarBeneficioDoCatalogo` e `excluirBeneficioDoCatalogo`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** uma linha por rota (as sete) na seção "Rotas implementadas", `em andamento`; a observação de `listarBeneficiosDoCatalogo`: "as marcas já são gravadas; `quantidadePessoas` conta os pedidos de benefício ativos (`BeneficioDoFuncionario`: 1b e 7)".
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Cadastros|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~GuardaDeExecuteUpdateTests"`
Expected: PASS, inclusive a divergência (as sete rotas batem com o contrato), a matriz (só `RH`) e o `STATUS`.
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: cargos e beneficios do catalogo"`

---

### Task 3: Configuração da empresa (`GET /api/eu/empresa`, `PUT /api/rh/empresa`) e `IConfiguracaoDaEmpresa`

**Files:**
- Create (Application): `Empresa/EmpresaCasoDeUso.cs`, `EmpresaDtos.cs`, `IConfiguracaoDaEmpresa.cs`, `IEmpresaRepositorio.cs`
- Create (Infrastructure): `Empresa/EmpresaRepositorio.cs` (implementa as duas interfaces); modify `DependencyInjection.cs`
- Create (Api): `Empresa/EmpresaEndpoints.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha `obterEmpresa` e `salvarEmpresa`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Test: `tests/RHSystem.Api.IntegrationTests/Empresa/EmpresaTests.cs`, `ConfiguracaoDaEmpresaLeituraTests.cs`

**Interfaces:**
- Consumes: `ConfiguracaoDaEmpresa`, `MensagensDoApoio`, `ErrosDoBanco` (Task 1); `IRelogio`; `GrupoEu()`, `GrupoRh()`, `ProduzProblemas`; exceções (1a, Task 3); **`ProblemaDeApiException(int status, string titulo, string detalhe, string? codigo = null, ...)` (1b, Task 3)**, que dá o 404 com `codigo`: a 1c **não** acrescenta `codigo` a `NaoEncontradoException` nem mexe no tratamento global de erros.
- Produces:

```csharp
// RHSystem.Application.Empresa      (o que as fatias 3 e 7 usam)
sealed record ConfiguracaoDaEmpresaLida(string NomeEmpresa, string Uf, string Municipio, TimeOnly HorarioEntrada, TimeOnly HorarioSaida, int ToleranciaAtrasoMinutos, int DiaPagamento);
interface IConfiguracaoDaEmpresa {
  Task<ConfiguracaoDaEmpresaLida?> ObterAsync(CancellationToken ct);          // null = ainda não configurada (quem pode seguir sem ela usa este)
  Task<ConfiguracaoDaEmpresaLida> ObterOuFalharAsync(CancellationToken ct);   // lança ErroDeNegocioException("EMPRESA_NAO_CONFIGURADA", MensagensDoApoio.EmpresaNaoConfigurada): vira 422 (a operação que precisa da empresa, CONVENCOES seção 11)
}
// DTOs do contrato (os horários são texto "HH:mm")
sealed record EmpresaConfiguracao(string NomeEmpresa, string Uf, string Municipio, string HorarioEntrada, string HorarioSaida, int ToleranciaAtrasoMinutos, int DiaPagamento);   // entrada e resposta; os sete obrigatórios
sealed class EmpresaCasoDeUso { Task<EmpresaConfiguracao> ObterAsync(CancellationToken ct); Task<EmpresaConfiguracao> SalvarAsync(EmpresaConfiguracao c, CancellationToken ct); }
```

**Regras:** `obterEmpresa` sem linha = **404** `ProblemaDeApiException(404, "Não encontrado.", MensagensDoApoio.EmpresaNaoConfigurada, "EMPRESA_NAO_CONFIGURADA")` (1b, Task 3). `salvarEmpresa`: a primeira chamada cria a linha, as outras a substituem; **os horários chegam como texto e são validados por regex** `^([01][0-9]|2[0-3]):[0-5][0-9]$` no caso de uso (nunca confiar no conversor de `TimeOnly` para recusar `08:00:30` ou `24:00`), com erro por campo; **UF só maiúscula e da lista de 27 siglas** (`sp` é 400, o enum do contrato é estrito); município e nome com `Trim`; saída menor que entrada é aceita. Duas primeiras chamadas simultâneas: a segunda leva `1062` na chave única e **refaz como atualização** (nunca 409, nunca 500: o contrato não declara 409). `IConfiguracaoDaEmpresa` **não tem cache**.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// EmpresaTests
Sem_configuracao_obter_devolve_404_com_codigo_EMPRESA_NAO_CONFIGURADA_e_o_detail_do_contrato()
Qualquer_perfil_le_a_empresa_e_a_resposta_traz_os_horarios_em_HH_mm()
Salvar_cria_e_depois_substitui_a_unica_linha_e_o_RH_e_o_unico_perfil_que_salva()               // SELECT COUNT(*) = 1; Gestor e Funcionario: 403; anônimo: 401
Salvar_valida_cada_campo_com_o_nome_do_campo_no_errors()                                       // uf "sp" e "ZZ"; tolerância -1 e 121; dia 0 e 32; horário "8:00", "24:00", "08:00:30"; nome vazio e 151; município vazio e 101
Corpo_sem_campo_obrigatorio_do_contrato_devolve_400()
Horario_de_saida_menor_que_o_de_entrada_e_aceito()                                             // 22:00 a 06:00
Dois_PUT_simultaneos_com_a_tabela_vazia_terminam_200_e_com_uma_linha()                         // Barrier
Salvar_grava_Criou_e_depois_Alterou_na_area_Cadastros_com_antes_e_depois_dos_campos_mudados()   // se o interceptor da 1a não formatar TimeOnly como HH:mm, acrescentar essa formatação à regra 3 dele
Respostas_batem_com_o_schema_do_contrato()
// ConfiguracaoDaEmpresaLeituraTests (a interface que as fatias 3 e 7 usam)
ObterAsync_devolve_null_sem_configuracao_e_os_valores_salvos_depois()
ObterOuFalharAsync_sem_configuracao_lanca_e_a_rota_de_teste_responde_422_com_codigo_EMPRESA_NAO_CONFIGURADA()   // rota de teste /__teste/empresa-obrigatoria
ObterAsync_enxerga_a_mudanca_logo_depois_de_salvar_sem_cache()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~EmpresaTests|FullyQualifiedName~ConfiguracaoDaEmpresaLeituraTests"`
Expected: FAIL.

- [ ] **Step 3: Implementar** as interfaces, o caso de uso, o repositório e as duas rotas (`GrupoEu()` para o `GET`, **`ProduzProblemas(401, 404, 500)` sem 403**; `GrupoRh()` para o `PUT`, `ProduzProblemas(400, 401, 403, 500)`); o 404 sai por `ProblemaDeApiException` (1b). **Neste passo, junto com as rotas**, acrescentar `obterEmpresa` e `salvarEmpresa` à lista de escopo de `ContratoDivergenciaTests`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** as duas rotas na seção "Rotas implementadas", `em andamento`.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Empresa|FullyQualifiedName~ErrosPadraoTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS.
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: configuracao da empresa e IConfiguracaoDaEmpresa"`

---

### Task 4: Gestores e "gestor ausente" (`/api/rh/gestores`, `IGestorAusente`; chama a porta `IPedidosParadosNoGestor` da 1a)

**Files:**
- Create (Application): `Gestores/GestoresCasoDeUso.cs`, `GestoresDtos.cs`, `IGestoresRepositorio.cs`, `IGestorAusente.cs`; `Colaboradores/FuncionarioResumo.cs`
- Create (Infrastructure): `Gestores/GestoresRepositorio.cs`, `GestorAusente.cs`; modify `DependencyInjection.cs`
- Create (Api): `Cadastros/GestoresEndpoints.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha `listarGestores`, `cadastrarGestor` e `configurarAusenciaDoGestor`), `Infra/ApiFactory.cs` (`ComPedidosParados(...)`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Create (testes): `Infra/PedidosParadosEspiao.cs` (implementa a porta da 1a: registra `(método, gestorId)` de cada chamada, pode ser mandado lançar e devolve o `PedidosAprovados` que o teste configurar (padrão `Zero`); **não** é a implementação das fatias 3 e 7)
- Test: `tests/RHSystem.Api.IntegrationTests/Gestores/GestoresTests.cs`, `AusenciaDoGestorTests.cs`, `GestorAusenteTests.cs`; `tests/RHSystem.Application.Tests/Gestores/` (se o caso de uso tiver regra pura)

**Interfaces:**
- Consumes: `Gestor`, `Setor`, `Funcionario`, `MensagensDoApoio` (Task 1); `SetorReferencia` (1b); **`IPedidosParadosNoGestor` (1a)**: a 1c só chama `AprovarEtapaDoGestorAsync`; `EquipeDoGestor`, `DaEquipe` (1a, Task 6: **a definição única de equipe**); `FusoBrasil.Hoje`, `IRelogio`, `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`; `GrupoRh()`, `ProduzProblemas`; exceções; `IAuditor` indiretamente (o interceptor grava as marcas do `Gestor`).
- Produces (DTOs do contrato):

```csharp
// RHSystem.Application.Gestores
sealed record GestorItem(int GestorId, FuncionarioResumo Funcionario, SetorReferencia? Setor, bool SoDecidePedidosDeGestores, int QuantidadeEquipe, bool Ausente, DateOnly? AusenteAte, bool AusenteHoje);
sealed record GestorListaResposta(IReadOnlyList<GestorItem> Itens);                      // sem paginação, ordem de gestorId
sealed record GestorCadastrarComando(int FuncionarioId);                                  // obrigatório
sealed record GestorAusenciaComando(bool Ausente, DateOnly? AusenteAte);                  // os dois obrigatórios (ausenteAte pode ser null)
// PedidosAprovados(int AjustesDePonto, int Ferias) é da 1a (Application.Colaboradores): a 1c NÃO cria outro record; `pedidosAprovadosAgora` é só o nome do campo da resposta
sealed record GestorAusenciaResposta(...campos de GestorItem..., PedidosAprovados PedidosAprovadosAgora);   // repete o que AprovarEtapaDoGestorAsync devolveu (decisão 2)
sealed class GestoresCasoDeUso { Task<GestorListaResposta> ListarAsync(CancellationToken ct); Task<GestorItem> CadastrarAsync(GestorCadastrarComando c, CancellationToken ct);
  Task<GestorAusenciaResposta> ConfigurarAusenciaAsync(int gestorId, GestorAusenciaComando c, CancellationToken ct); }
```
  (`SetorReferencia(int Id, string Nome)` é da 1b, em `RHSystem.Application.Colaboradores`: reuse. **`FuncionarioResumo(int Id, string Nome, string? Cargo, string? Setor, bool Ativo)`** é o schema de `00-base.yaml:119`; a 1b **não** o criou (o `FuncionarioEquipeItem` dela tem os campos soltos): crie-o em `Application/Colaboradores/FuncionarioResumo.cs`; as fatias 3 e 7 o reaproveitam.)
- Produces (**para as fatias 3 e 7**; nomes e assinaturas não mudam depois):

```csharp
// RHSystem.Application.Gestores
enum EtapaInicial { Gestor, Rh }
sealed record EtapaInicialDoPedido(EtapaInicial Etapa, int? GestorId, bool EtapaDoGestorAprovadaPorAusencia);
interface IGestorAusente {      // as fatias 3 (ajuste de ponto) e 7 (férias) chamam ao CRIAR um pedido
  // Quem decide primeiro o pedido novo desta pessoa (CONVENCOES, seção 9):
  //  - a pessoa é gestor (linha em GESTORES, ativa, e gerencia um setor ou é do RH cadastrado), ou não tem setor, ou o setor não tem gestor, ou o gestor está inativo:
  //      Etapa = Rh, GestorId = null, aprovada = false  (o RH decide; quem decide precisa estar em GESTORES: PEDIDO_RH_FORA_DE_GESTORES é da fatia 3 e 7)
  //  - o gestor do setor está ausente HOJE: Etapa = Rh, GestorId = o do gestor, EtapaDoGestorAprovadaPorAusencia = true (a etapa dele já nasce aprovada, "automática")
  //  - senão: Etapa = Gestor, GestorId = o do gestor, aprovada = false
  Task<EtapaInicialDoPedido> EtapaInicialDoPedidoAsync(int funcionarioId, CancellationToken ct);
  Task<bool> EstaAusenteHojeAsync(int gestorId, CancellationToken ct);   // ausente e (sem data de volta ou hoje antes dela), com "hoje" em Brasília
}
```

**A porta que esta tarefa chama** (**não é desta fatia**: a 1a a cria, Task 13, com o composto padrão **vazio** registrado como `IPedidosParadosNoGestor`; as fatias 3 e 7 registram a implementação delas, `IPedidosParadosNoGestorDaFatia`, e o composto as soma; namespace e arquivo: os que a 1a definiu). Assinatura da 1a (decisão 5 da seção 12 da entrada), **com a contagem em `PedidosAprovados` e sem `Tipo`**; a 1c injeta só esta porta:

```csharp
interface IPedidosParadosNoGestor {
  // "Gestor ausente" ligado: aprova a etapa do gestor dos pedidos em análise que esperam por este gestor (CONVENCOES, seção 9; férias com sugestão de data em andamento NÃO entram).
  Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken ct);   // PedidosAprovados(int AjustesDePonto, int Ferias): vira `pedidosAprovadosAgora` na resposta de "Gestor ausente"
  // O gestor deixou o setor, foi trocado ou foi desligado: passa à etapa do RH os pedidos em análise que esperavam por ele. Chamada EXPLÍCITA de quem tira o gestor.
  Task PassarParaORhAsync(int gestorId, CancellationToken ct);
}
```

**Regras do contrato** (ordem de avaliação: 404, 400, 409, 422):
- **Quem é gestor** (lista e 404): linha em `GESTORES`, pessoa **ativa**, e (**gerencia um setor** ou tem o nível **`RH`**). Quem tem o nível `Gestor` sem setor (o `GestorSemSetor` do elenco) e quem foi rebaixado a `Funcionario` **não** aparecem e dão 404 em `.../ausencia`. `setor` = o setor que ele gerencia (`SETORES.ID_GESTOR`) ou `null`; `soDecidePedidosDeGestores` = não gerencia setor; `quantidadeEquipe` = `DaEquipe(new EquipeDoGestor(...)).Count()` (a definição da 1a: ativos, admissão já começada, do setor, sem ele; 0 sem setor); `ausenteHoje` = `Gestor.AusenteEm(FusoBrasil.Hoje(relogio.AgoraUtc))`; `funcionario` = `FuncionarioResumo` (id, nome, cargo, setor da **pessoa**, ativo).
- **`cadastrarGestor`:** `funcionarioId` inexistente = **400** em `funcionarioId` (o contrato não declara 404 aqui); pessoa inativa = 422 `FUNCIONARIO_INATIVO`; nível diferente de `RH` = 422 `GESTOR_SEM_SETOR_SO_PARA_RH`; já tem linha em `GESTORES` = 409 `GESTOR_JA_CADASTRADO`. O nível da pessoa não muda. A linha nasce com `soDecidePedidosDeGestores = true`, sem setor. Nesta ordem: 400, 422 inativa, 422 não RH, 409.
- **`configurarAusenciaDoGestor`:** `ausente = true`: `ausenteAte` `null` ou **depois de hoje** (senão 400 em `ausenteAte`); `ausente = false`: `ausenteAte` precisa ser `null` (senão 400). **Marcar para quem só decide pedidos de gestores = 422 `GESTOR_SEM_EQUIPE`** (desmarcar é permitido: limpa marca antiga). Marcar roda **na mesma transação**: grava a marca e chama `IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync(gestorId, ct)` (1a); a resposta traz em `pedidosAprovadosAgora` **o que a porta devolveu** (`PedidosAprovados`: `{ ajustesDePonto: 0, ferias: 0 }` com o composto vazio da 1a; os números reais depois que as fatias 3 e 7 registrarem a implementação delas; decisão 2). **Desmarcar não chama a porta** (só os pedidos novos voltam ao normal; o aprovado automaticamente continua aprovado). Marcar de novo (idempotente) chama a porta de novo. A marca e a data entram na auditoria pelas marcas do `Gestor`.
- Nenhuma tarefa agendada: passada a data de volta, `ausente` continua `true` e `ausenteHoje` vira `false` (nota «(antigo V011)» do `V001`).

- [ ] **Step 1: Escrever os testes que falham** (relógio em `2026-10-07T12:42:00Z` quando não dito; `ApoioDeTeste.CriarPessoaAsync` para a pessoa do RH sem linha e para o ex-gestor; o `PedidosParadosEspiao` pelo `ApiFactory.ComPedidosParados(...)`)

```csharp
// GestoresTests
Lista_traz_so_quem_gerencia_setor_ativo_ou_e_do_RH_cadastrado_na_ordem_de_gestorId()      // controles que NÃO aparecem: GestorSemSetor (nível Gestor sem setor), gestor inativo, ex-gestor rebaixado
Linha_de_gestor_de_setor_tem_setor_equipe_igual_a_definicao_da_1a_e_soDecide_false()      // Carlos: setor Comercial, equipe = DaEquipe(...).Count() (MembroA e MembroB; fora: EmAdmissao, Inativo, o próprio)
Linha_do_RH_cadastrado_tem_setor_null_equipe_0_e_soDecide_true()
Funcionario_da_linha_traz_o_cargo_e_o_setor_da_pessoa_e_ativo()
Cadastrar_RH_ativo_devolve_201_com_setor_null_soDecide_true_ausente_false_e_grava_Criou_na_area_Cadastros()
Cadastrar_inexistente_devolve_400_em_funcionarioId_inativo_422_FUNCIONARIO_INATIVO_nao_RH_422_GESTOR_SEM_SETOR_SO_PARA_RH_e_repetido_409_GESTOR_JA_CADASTRADO()
Cadastrar_nao_muda_o_nivel_da_pessoa()
Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
Respostas_batem_com_o_schema_do_contrato()
// AusenciaDoGestorTests
Marcar_com_volta_futura_devolve_200_ausenteHoje_true_e_pedidosAprovadosAgora_zeros_com_a_porta_padrao_vazia()   // Paulo Vieira, volta 2026-10-19 (o exemplo do contrato)
Marcar_sem_data_vale_ate_desmarcar_e_desmarcar_devolve_ausente_false_data_null_e_zeros()
ausenteHoje_vale_ate_o_dia_anterior_a_volta_e_depois_continua_ausente_true_com_ausenteHoje_false()   // relógio 2026-10-18: true; 2026-10-19: false (Review Focus 3)
ausenteHoje_na_virada_do_dia_usa_o_dia_de_Brasilia()                                      // volta 2026-10-19, relógio 2026-10-19T02:30Z (18/10 23:30 em -03:00): ainda true
Marcar_com_volta_hoje_ou_no_passado_devolve_400_em_ausenteAte()
Desmarcar_com_data_devolve_400_em_ausenteAte()
Marcar_o_RH_cadastrado_sem_setor_devolve_422_GESTOR_SEM_EQUIPE_com_o_nome_no_detail_e_desmarcar_e_permitido()
Gestor_inexistente_sem_setor_com_nivel_Gestor_rebaixado_ou_inativo_devolvem_404()
Marcar_chama_AprovarEtapaDoGestorAsync_com_o_gestorId_na_mesma_transacao()   // PedidosParadosEspiao registra (AprovarEtapaDoGestorAsync, gestorId); a chamada roda dentro de ExecutarEmTransacaoAsync
Marcar_repete_na_resposta_o_que_a_porta_devolveu_com_numeros_diferentes_de_zero()   // o PedidosParadosEspiao devolve PedidosAprovados(3, 2): pedidosAprovadosAgora { ajustesDePonto: 3, ferias: 2 } (decisão 2); repetir com (0, 4) e esperar { 0, 4 }, para o teste não passar com os dois campos trocados
Marcar_de_novo_chama_a_porta_de_novo_e_desmarcar_nao_chama()
Porta_que_lanca_desfaz_a_marcacao_e_devolve_500_sem_vazar_detalhe()                       // Review Focus 3
Marcar_e_desmarcar_gravam_Alterou_na_area_Cadastros_com_Gestor_ausente_e_Volta_em_antes_e_depois_e_o_nome_da_pessoa()
Corpo_sem_ausente_ou_sem_ausenteAte_devolve_400()
Gestor_e_Funcionario_recebem_403_e_anonimo_401()
Respostas_batem_com_o_schema_do_contrato()
// GestorAusenteTests (a interface que as fatias 3 e 7 usam)
EtapaInicial_de_quem_e_gestor_ou_do_RH_cadastrado_e_Rh_sem_gestor()
EtapaInicial_de_pessoa_de_setor_sem_gestor_ou_sem_setor_e_Rh_sem_gestor()                 // Compras sem gestor; Roberto Alves (RH, sem linha, setor Recursos Humanos)
EtapaInicial_com_gestor_do_setor_inativo_e_Rh_sem_gestor()
EtapaInicial_com_gestor_presente_e_Gestor_com_o_GestorId_dele()
EtapaInicial_com_gestor_ausente_hoje_e_Rh_com_GestorId_e_aprovada_por_ausencia()
EtapaInicial_passada_a_data_de_volta_volta_a_ser_Gestor()
EtapaInicial_de_ex_gestor_rebaixado_segue_o_gestor_do_setor_dele()
EstaAusenteHojeAsync_segue_a_mesma_regra_da_lista()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Gestores"`
Expected: FAIL.

- [ ] **Step 3: Implementar** DTOs, casos de uso, repositório, `GestorAusente` e as três rotas em `app.GrupoRh()` com `ProduzProblemas` dos códigos da tabela. A lista conta a equipe uma vez por gestor (poucos gestores: sem agrupamento). O `ApiFactory.ComPedidosParados(IPedidosParadosNoGestor)` troca, só nos testes, o composto padrão da 1a (registrado como `IPedidosParadosNoGestor`) pelo `PedidosParadosEspiao`. Registrar `IGestorAusente` como escopo da requisição. **Neste passo, junto com as rotas**, acrescentar `listarGestores`, `cadastrarGestor` e `configurarAusenciaDoGestor` à lista de escopo de `ContratoDivergenciaTests`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** as três rotas na seção "Rotas implementadas", `em andamento`; observação de `configurarAusenciaDoGestor`: **"parcial: `pedidosAprovadosAgora` vale 0 e 0 até as fatias 3 e 7 registrarem a implementação delas da porta `IPedidosParadosNoGestor` (o composto padrão é vazio)"**.
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Gestores|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS.
- [ ] **Step 6: Registrar no `README.md` do back-end** (decisão 2, já tomada: nada a perguntar ao dono) que `pedidosAprovadosAgora` repete o `PedidosAprovados` que `IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync` devolve, e que vale `{ 0, 0 }` enquanto a porta for o composto vazio da 1a; as fatias 3 e 7 passam a preencher os números ao registrarem a implementação delas.
- [ ] **Step 7: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: gestores e gestor ausente (chama a porta dos pedidos parados)"`

---

### Task 5: Setores e a troca de gestor (`/api/rh/setores`)

**Files:**
- Create (Application): `Cadastros/SetoresCasoDeUso.cs`, `ISetoresRepositorio.cs`; DTOs em `CadastrosDtos.cs`
- Create (Infrastructure): `Cadastros/SetoresRepositorio.cs`; modify `DependencyInjection.cs`
- Create (Api): `Cadastros/SetoresEndpoints.cs`; modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha `listarSetores`, `criarSetor` e `editarSetor`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Create (testes): `Infra/GestorDoFuncionarioEspiao.cs` (decorador de `IGestorDoFuncionario` que delega à implementação real da 1b e registra os `setorId` recebidos; o `ApiFactory` o registra por `ComGestorDoFuncionarioEspiao()`)
- Test: `tests/RHSystem.Api.IntegrationTests/Cadastros/SetoresTests.cs`, `TrocaDeGestorDoSetorTests.cs`

**Interfaces:**
- Consumes: `Setor` (`Descricao`, `Criar`, `Atualizar`: Task 1; **`DefinirGestor(int? idGestor)`: 1b**), `Gestor`, `Funcionario` (`DefinirNivel`: Task 1), `MensagensDoApoio`, `ErrosDoBanco` (Task 1); **da 1b:** `NomesDeNivel.DoPerfil(Perfil)`, `GestorReferencia(int GestorId, int FuncionarioId, string Nome)` (`RHSystem.Application.Colaboradores`) e **`IGestorDoFuncionario.RefazerDosMembrosAsync(int setorId, CancellationToken ct)`**; **`IPedidosParadosNoGestor.PassarParaORhAsync(int gestorId, CancellationToken ct)` (1a)**; `DaEquipe` e `EquipeDoGestor`; `ParametrosDeLista`, `MapaDeOrdenacao`, `BuscaLike`; `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`; exceções (1a, Task 3).
- Produces:

```csharp
// RHSystem.Application.Cadastros      (GestorReferencia é da 1b: não se redefine aqui)
sealed record SetorItem(int Id, string Nome, string? Descricao, GestorReferencia? Gestor, int QuantidadePessoas);
sealed record SetorComando(string Nome, string? Descricao, int? GestorFuncionarioId);          // os três obrigatórios; os dois últimos podem ser null
sealed record SetorListaResposta(...) : RespostaPaginada<SetorItem>(...);
sealed class SetoresCasoDeUso { Task<SetorListaResposta> ListarAsync(ParametrosDeLista p, CancellationToken ct);
  Task<SetorItem> CriarAsync(SetorComando c, CancellationToken ct); Task<SetorItem> EditarAsync(int setorId, SetorComando c, CancellationToken ct); }
```

**Regras** (`CONVENCOES.md:83` e `editarSetor`, `:1383-1440`; ordem: 404, 400, 409 nome, 422 inativo, 422 RH, 409 outro setor):
- **Lista:** só `pagina`, `tamanhoPagina`, `busca` (mesma regra da Task 2: não use `[AsParameters]`); nome e descrição; ordem por nome. `quantidadePessoas` = pessoas **ativas com `DataAdmissao <= hoje`** do setor, **inclusive o gestor se ele for do setor**. `gestor` = `GestorReferencia` ou `null` (mostra o gestor mesmo inativo).
- **Nome único:** igual à Task 2 (ignora maiúsculas e acentos; corrida vira 409).
- **Criar e editar com `gestorFuncionarioId` não nulo:** pessoa inexistente = **400** em `gestorFuncionarioId`; inativa = 422 `FUNCIONARIO_INATIVO`; nível `RH` = 422 `GESTOR_PRECISA_TER_PERFIL_GESTOR`; já é gestor de **outro** setor = 409 `GESTOR_JA_RESPONDE_POR_OUTRO_SETOR` (o texto traz o nome dele e o do setor onde está). Quem tem o nível `Funcionario` é **promovido** a `Gestor`; se não tem linha em `GESTORES`, a API a cria.
- **Trocar o gestor** (só quando `gestorFuncionarioId` difere do atual; **tudo na mesma transação**, e qualquer falha desfaz tudo):
  1. Se o gestor novo não tem a linha, cria; se o nível não é `Gestor`, `DefinirNivel` com o `NivelDeAcesso` de `NomesDeNivel.DoPerfil(Perfil.Gestor)` (1b).
  2. Se havia gestor antigo (e é outra pessoa), `DefinirNivel` com o de `NomesDeNivel.DoPerfil(Perfil.Funcionario)` **se o nível dele é `Gestor`**; a linha em `GESTORES` **fica** (os pedidos antigos a referenciam). Ele continua no setor como membro.
  3. `setor.DefinirGestor(gestorNovo?.Id)` (1b; o id é o de `GESTORES`, ou `null`) e `SalvarAsync` do setor.
  4. **`IGestorDoFuncionario.RefazerDosMembrosAsync(setor.Id, ct)` (1b), depois de salvar o setor e ainda na mesma transação.** Ela refaz o `ID_GESTOR` dos membros **ativos** do setor (o gestor novo fica `null`, porque o trigger recusa "gestor de si mesmo", `Criacao_Triggers:75-84`; com `null` no setor, todos ficam `null`; inativos ficam como estão, decisão 9). **A 1c não reimplementa essa regra.** `criarSetor` **também** a chama quando nasce com gestor (o setor novo ainda não tem membros: o efeito é nenhum, mas é o mesmo caminho de código de `editarSetor`). O `422 GESTOR_DO_SETOR_INATIVO` dela nunca chega aqui, porque o gestor inativo já foi recusado com `FUNCIONARIO_INATIVO`. **Não mexe no `ID_SETOR` do gestor novo** (decisão 1).
  5. Se havia gestor antigo, chama `IPedidosParadosNoGestor.PassarParaORhAsync(gestorAntigo.GestorId, ct)` (1a), **uma vez** (os pedidos dele passam ao RH, tanto ao tirar quanto ao trocar). Sem gestor antigo, ou com o mesmo gestor, **não chama**.
- O interceptor grava o evento do setor (`Alterou`, "Gestor" com o **nome** antes e depois: carregue o `Gestor` antigo e o novo com `Include(g => g.Funcionario)` **antes** de salvar, senão o interceptor da 1a grava o id) e, como a 1b marcou o nível (`NivelDeAcessoId`), também o do colaborador promovido ou rebaixado.
- Não existe exclusão de setor.

- [ ] **Step 1: Escrever os testes que falham** (elenco da 1a: Comercial com Carlos, `MembroA`, `MembroB`, `EmAdmissao`, `Inativo`; Financeiro com Marina; `DeOutroSetor`; `ApoioDeTeste` para Compras sem gestor e para uma segunda pessoa de nível `Funcionario`; portas falsas)

```csharp
// SetoresTests
Lista_traz_gestor_ou_null_e_quantidadePessoas_de_ativos_ja_admitidos_inclusive_o_gestor()   // Comercial = 3 (Carlos, MembroA, MembroB); EmAdmissao e Inativo fora
Busca_no_nome_e_na_descricao_ordem_por_nome_e_setor_com_gestor_inativo_continua_mostrando_o_gestor()
Criar_sem_gestor_devolve_201_com_gestor_null_e_quantidadePessoas_0_e_grava_Criou()          // Compras, o exemplo do contrato
Criar_com_gestor_Funcionario_promove_a_Gestor_cria_a_linha_em_Gestores_e_define_o_gestor_do_setor()
Criar_com_gestor_RH_devolve_422_GESTOR_PRECISA_TER_PERFIL_GESTOR_com_o_texto_do_contrato_e_nada_e_gravado()
Criar_ou_editar_com_gestor_inativo_devolve_422_FUNCIONARIO_INATIVO_e_com_inexistente_400_em_gestorFuncionarioId()
Criar_com_gestor_que_ja_responde_por_outro_setor_devolve_409_GESTOR_JA_RESPONDE_POR_OUTRO_SETOR_com_o_nome_dele_e_do_setor()
Nome_repetido_409_NOME_JA_CADASTRADO_e_vinte_criacoes_simultaneas_dao_um_201_e_nenhum_500()  // Review Focus 1
Corpo_sem_campo_obrigatorio_devolve_400_e_nome_ou_descricao_fora_dos_limites_400()
Setor_inexistente_devolve_404_na_edicao()
Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas()
Respostas_batem_com_o_schema_do_contrato()
// TrocaDeGestorDoSetorTests
Editar_sem_mudar_o_gestor_so_atualiza_nome_e_descricao_sem_efeito_nenhum_e_sem_chamar_a_porta_nem_RefazerDosMembros()
Trocar_o_gestor_refaz_o_ID_GESTOR_dos_ativos_do_setor_o_novo_fica_sem_gestor_e_o_antigo_passa_a_ter_o_novo()   // efeito de RefazerDosMembrosAsync (1b) visto por SELECT: MembroA e MembroB = gestor novo; o novo = NULL; o antigo = gestor novo; Inativo e DeOutroSetor intactos
Criar_e_editar_com_mudanca_de_gestor_chamam_RefazerDosMembrosAsync_do_setor_na_mesma_transacao()   // GestorDoFuncionarioEspiao: recebe o setorId certo, depois de o setor salvo; criar com gestor também a chama (efeito nenhum: setor sem membros)
Trocar_o_gestor_promove_o_novo_a_Gestor_e_rebaixa_o_antigo_a_Funcionario_que_continua_no_setor_e_em_Gestores()
Tirar_o_gestor_com_null_zera_o_ID_GESTOR_dos_membros_rebaixa_o_antigo_e_deixa_o_setor_sem_gestor()
Gestor_novo_que_ja_e_membro_do_setor_recebe_ID_GESTOR_nulo_e_nunca_o_proprio()             // o trigger "de si mesmo" não dispara
Gestor_novo_de_outro_setor_continua_no_setor_dele_e_nao_e_movido()                          // decisão 1
Trocar_ou_tirar_o_gestor_chama_PassarParaORhAsync_do_gestor_antigo_uma_vez_e_nao_chama_sem_gestor_antigo_nem_sem_mudanca()   // PedidosParadosEspiao: (PassarParaORhAsync, gestorId do antigo)
Falha_no_meio_da_troca_desfaz_o_setor_os_niveis_os_ID_GESTOR_e_o_evento()                   // PedidosParadosEspiao lança em PassarParaORhAsync, o último passo: tudo o que veio antes volta (Review Focus 2)
Erros_da_troca_nao_deixam_nada_pela_metade()                                                // 422 e 409 antes de qualquer gravação: nenhuma mudança
Trocar_o_gestor_grava_Alterou_na_area_Cadastros_com_Gestor_antes_e_depois_pelo_NOME()
Novo_gestor_ve_a_equipe_do_setor_na_rota_de_teste_do_escopo_da_1a_depois_de_novo_token()    // ClienteComo(pessoa com Perfil.Gestor): /api/gestor/__teste/equipe devolve MembroA e MembroB
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~SetoresTests|FullyQualifiedName~TrocaDeGestorDoSetorTests"`
Expected: FAIL.

- [ ] **Step 3: Implementar** DTOs, caso de uso, repositório e as três rotas em `app.GrupoRh()`, `ProduzProblemas` dos códigos da tabela. O caso de uso roda em `ExecutarEmTransacaoAsync`, **valida tudo antes de gravar** (400, 409, 422) e só então aplica os cinco passos; os passos 4 e 5 **chamam** `IGestorDoFuncionario` (1b) e `IPedidosParadosNoGestor` (1a), **sem reimplementá-los**; o repositório devolve o setor, o gestor antigo e o novo rastreados. O `ApiFactory.ComGestorDoFuncionarioEspiao()` registra o decorador de teste. **Neste passo, junto com as rotas**, acrescentar `listarSetores`, `criarSetor` e `editarSetor` à lista de escopo de `ContratoDivergenciaTests`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** as três rotas na seção "Rotas implementadas", `em andamento`; observação de `editarSetor`: "os pedidos parados do gestor antigo passam ao RH só quando as fatias 3 e 7 registrarem a implementação delas da porta `IPedidosParadosNoGestor` (o composto padrão é vazio)".
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Setores|FullyQualifiedName~TrocaDeGestorDoSetorTests|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~Escopo"`
Expected: PASS (inclusive os testes de escopo da 1a: a equipe de quem acabou de ser promovido, e os da 1b: `GestorDoFuncionarioTests` continua verde).
- [ ] **Step 6: Perguntar ao dono (decisão 1)** e registrar a resposta no `README.md` do back-end: ao escolher como gestor de um setor uma pessoa **de outro setor**, a API (a) deixa como está (**implementado**), (b) recusa, ou (c) muda o setor dela. Só (b) ou (c) pedem contrato novo (um erro novo, ou efeito novo descrito).
- [ ] **Step 7: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: setores e troca de gestor do setor"`

---

### Task 6: Armazenamento de arquivos (`IArmazenamentoDeArquivos`), Azurite e a limpeza de sobras

**Files:**
- Create (Application): `Arquivos/IArmazenamentoDeArquivos.cs` (com `IListagemDeArquivos`), `CaminhosDeArquivo.cs`, `IdentificadorDeTipoDeArquivo.cs`, `NomeParaDownload.cs`, `IFonteDeReferenciasDeArquivo.cs`
- Create (Infrastructure): `Arquivos/ArmazenamentoBlob.cs`, `OpcoesDeArmazenamento.cs`, `Tarefas/LimparArquivosOrfaosTarefa.cs`; modify `DependencyInjection.cs`
- Modify: `docker-compose.yml`, `.env.example`, `README.md`, `Api/appsettings.json`, `appsettings.Development.json`, `back-end/Directory.Packages.props` (`Azure.Storage.Blobs`; `Testcontainers.Azurite` se existir para a versão do Testcontainers do projeto, senão o construtor genérico), `tests/.../Infra/ApiFactory.cs` (**troca o armazenamento pelo em memória** e decora `IUnidadeDeTrabalho` com o `UnidadeDeTrabalhoEspia`)
- Test: `tests/RHSystem.Application.Tests/Arquivos/IdentificadorDeTipoDeArquivoTests.cs`, `CaminhosDeArquivoTests.cs`, `NomeParaDownloadTests.cs`; `tests/RHSystem.Api.IntegrationTests/Arquivos/ArmazenamentoBlobTests.cs`, `LimparArquivosOrfaosTests.cs`; `Infra/ArmazenamentoEmMemoria.cs`, `UnidadeDeTrabalhoEspia.cs`, `AzuriteFixture.cs`, `ArquivosDeTeste.cs`

**Interfaces:**
- Consumes: `IRelogio`; `ITarefaAgendada`, `IAgendador`, `NovaTarefa`, `TarefaParaExecutar`, `ChaveDeIdempotencia.Montar` (1a, Tasks 10 e 11); `FusoBrasil`.
- Produces (**reaproveitável por holerites, atestados, currículos, respostas de formulário e módulos; nomes e assinaturas não mudam depois**):

```csharp
// RHSystem.Application.Arquivos
sealed record LinkTemporario(string Url, DateTime ExpiraEmUtc);                    // o schema LinkTemporario do contrato
sealed record ArquivoGuardado(string Caminho, long Tamanho, DateTime CriadoEmUtc);
interface IArmazenamentoDeArquivos {
  Task GravarAsync(string caminho, Stream conteudo, string tipoDeConteudo, CancellationToken ct);              // não sobrescreve: caminho já existente lança; caminho inválido lança ArgumentException
  Task<LinkTemporario> CriarLinkTemporarioAsync(string caminho, string nomeParaDownload, CancellationToken ct);   // só leitura, vale Armazenamento:LinkValidoMinutos a partir do IRelogio, baixa como anexo com este nome
  Task ApagarAsync(string caminho, CancellationToken ct);                                                        // idempotente: apagar o que não existe não lança
}
interface IListagemDeArquivos { IAsyncEnumerable<ArquivoGuardado> ListarAsync(string prefixo, CancellationToken ct); }   // só a limpeza usa
interface IFonteDeReferenciasDeArquivo {                    // cada fatia que guarda caminho de arquivo registra UMA; a limpeza só toca prefixo que tem fonte
  string Prefixo { get; }                                   // "documentos/", "holerites/"...
  Task<IReadOnlySet<string>> ReferenciadosAsync(IReadOnlyCollection<string> caminhos, CancellationToken ct);   // quais destes caminhos têm linha no banco
}
static class CaminhosDeArquivo {
  static string Novo(string pasta, string extensao);        // "documentos/9f1c…(32 hex).pdf"; pasta e extensão em minúsculas; o nome é um GUID, nunca vem do usuário
  static bool EhValido(string caminho);                     // segmentos [a-z0-9-], termina em 32 hex + extensão; recusa "..", barra inicial, maiúscula e barra invertida
}
sealed record TipoDeArquivo(string TipoDeConteudo, string Extensao);
static class IdentificadorDeTipoDeArquivo { static TipoDeArquivo? Identificar(ReadOnlySpan<byte> inicio); }   // PDF ("%PDF-" → application/pdf, ".pdf"), JPEG (FF D8 FF → image/jpeg, ".jpg"), PNG (assinatura de 8 bytes → image/png, ".png"); o resto: null. Quem precisar de outro tipo (xlsx na fatia 2) ACRESCENTA, não renomeia
static class NomeParaDownload { static string Limpar(string? nomeOriginal, string extensao); }                // sem diretório (/ e \), sem aspas nem caracteres de controle nem quebra de linha, até 100 caracteres; vazio vira "documento" + extensão
```

**Configuração nova** (padrões entre parênteses): `Armazenamento:ConexaoBlob` (vazio; **só por user-secrets ou configuração do App Service**; em Development: `UseDevelopmentStorage=true`, que dispensa chave escrita), `Armazenamento:Container` (`arquivos`, privado, criado se não existir), `Armazenamento:LinkValidoMinutos` (5), `Armazenamento:SomenteHttps` (`true`; `false` em Development), `Armazenamento:SobrasMinimoHoras` (24). Sem `ConexaoBlob` o app **sobe** e a primeira rota que usa arquivo responde 500 (erro claro **no log**, nunca com a connection string).

**Regras:** `ArmazenamentoBlob` usa `Azure.Storage.Blobs` e gera o link por **SAS** com a chave da conta (`BlobSasBuilder`: permissão só de leitura; `StartsOn` 1 minuto antes por causa de relógio; `ExpiresOn` = `IRelogio.AgoraUtc` + validade; `ContentDisposition` = `attachment` com `filename` ASCII e `filename*` UTF-8; protocolo HTTPS conforme `SomenteHttps`). `Gravar` define o `Content-Type` e **não sobrescreve**. Sem acesso anônimo. **A tarefa `LIMPAR_ARQUIVOS_ORFAOS`** (um `ITarefaAgendada`, registrado como as da 1a): `RecuperarAsync` agenda uma por dia (`LIMPAR_ARQUIVOS_ORFAOS:dia=AAAA-MM-DD`, `ChaveDeIdempotencia.Montar`; o tipo tem 22 caracteres, dentro dos 50 que `ChaveDeIdempotencia` e `TAREFAS_AGENDADAS.TIPO` aceitam); `ExecutarAsync` percorre **só os prefixos que têm `IFonteDeReferenciasDeArquivo`**, lista os blobs **mais velhos que `SobrasMinimoHoras`** (o mínimo protege o envio que já gravou o blob e ainda não inseriu a linha: a janela entre o Blob e a transação curta, e a sobra deixada por uma queda nesse intervalo), pergunta à fonte, em lotes de 100, quais são referenciados e apaga os outros. Uma fonte que lança não impede as demais e nada do prefixo dela é apagado.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// Application.Tests
Identificar_reconhece_PDF_JPEG_e_PNG_pelo_conteudo_e_devolve_null_para_exe_texto_html_gif_e_vazio()
CaminhosDeArquivo_Novo_gera_pasta_barra_guid_ponto_extensao_e_EhValido_recusa_traversal_barra_inicial_maiuscula_e_barra_invertida()
NomeParaDownload_Limpar_tira_diretorio_aspas_barras_controle_e_quebra_de_linha_corta_em_100_e_cai_em_documento_com_extensao_se_ficar_vazio()
// Api.IntegrationTests/Arquivos/ArmazenamentoBlobTests (Azurite em contêiner pelo AzuriteFixture; imagem com versão fixada)
Gravar_e_baixar_pelo_link_devolve_os_mesmos_bytes_como_anexo_com_o_nome_dado()           // HttpClient no Url: 200, bytes iguais, Content-Disposition attachment e filename
Link_expira_no_instante_do_relogio_mais_a_validade_e_um_link_vencido_e_recusado_pelo_servico()   // relógio falso 10 min no passado
Link_nao_contem_a_chave_da_conta_nem_a_connection_string()
Gravar_em_caminho_ja_existente_falha_sem_sobrescrever_e_caminho_invalido_lanca_ArgumentException_antes_de_chamar_o_servico()
Apagar_e_idempotente()
Nome_com_aspas_e_quebra_de_linha_nao_injeta_cabecalho_nem_quebra_o_link()                 // Review Focus 4
ListarAsync_devolve_caminho_tamanho_e_data_de_criacao_dos_blobs_do_prefixo()
// LimparArquivosOrfaosTests (armazenamento em memória e fontes falsas)
Apaga_blob_antigo_sem_referencia_e_mantem_o_referenciado()
Nunca_apaga_blob_mais_novo_que_o_minimo()                                                  // o envio que já gravou o blob e ainda não inseriu a linha
Nunca_apaga_blob_de_prefixo_sem_fonte_registrada()
Fonte_que_lanca_nao_impede_as_outras_e_nada_do_prefixo_dela_e_apagado()
Mais_de_cem_blobs_sao_tratados_em_lotes()
RecuperarAsync_agenda_uma_por_dia_sem_duplicar_e_o_tipo_esta_registrado()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Arquivos"`
Expected: FAIL (tipos não definidos).

- [ ] **Step 3: Implementar** as interfaces e classes. **Azurite no `docker compose`:** serviço `azurite` (`mcr.microsoft.com/azure-storage/azurite` com a **versão estável conferida na hora**, nunca `latest`), comando `azurite-blob --blobHost 0.0.0.0 --blobPort 10000 --location /data`, porta `${AZURITE_BLOB_PORTA:-10000}:10000`, volume nomeado, `healthcheck` (a imagem é Alpine com Node: `nc -z 127.0.0.1 10000` ou `node -e` servem; o Step 4 prova). A porta entra em `.env.example`. `appsettings.Development.json` ganha `Armazenamento:ConexaoBlob = "UseDevelopmentStorage=true"` e `SomenteHttps = false`. O `README.md` ganha "Arquivos em desenvolvimento: Azurite em `http://127.0.0.1:10000`; os arquivos somem com `docker compose down -v`". **O `ApiFactory` registra `ArmazenamentoEmMemoria`** (guarda os bytes e o tipo; expõe `Gravados`, `Apagados`, `FalharNaGravacao`, `FalharNaExclusao`, o gancho `AoGravar` (`Func<string, Task>?`, chamado no início de `GravarAsync`, para os testes de corrida) e `GravacoesComTransacaoAberta` (quantas vezes `GravarAsync` rodou com uma transação aberta); gera links `https://arquivos.exemplo.test/...`) no lugar do Blob. **O `UnidadeDeTrabalhoEspia`** decora `IUnidadeDeTrabalho` no `ApiFactory` e marca, num `AsyncLocal<bool>`, quando `ExecutarEmTransacaoAsync` está em curso; o `ArmazenamentoEmMemoria` o lê para contar `GravacoesComTransacaoAberta` (as Tasks 7 e 8 provam que é 0). **Só `ArmazenamentoBlobTests` fala com o Azurite**. Se o SDK recusar a versão de API no Azurite, subir a versão do Azurite e, **só se continuar**, acrescentar `--skipApiVersionCheck` ao comando e anotar no `README.md`.
- [ ] **Step 4: Rodar e ver passar**

Run: `docker compose up -d --wait; dotnet test back-end --filter "FullyQualifiedName~Arquivos"`
Expected: o serviço `azurite` `healthy` e PASS.
- [ ] **Step 5: Commit** (com autorização): `git add back-end docker-compose.yml .env.example README.md; git commit -m "feat: armazenamento de arquivos no Blob com Azurite local e limpeza de sobras"`

---

### Task 7: Documentos do colaborador, pelo RH (`/api/rh/funcionarios/{funcionarioId}/documentos`)

**Files:**
- Create (Application): `Documentos/DocumentosDoColaboradorCasoDeUso.cs`, `DocumentosDtos.cs`, `IDocumentosRepositorio.cs`, `LimitesDeDocumento.cs`, `RotulosDeDocumento.cs`
- Create (Infrastructure): `Documentos/DocumentosRepositorio.cs`, `FonteDeReferenciasDosDocumentos.cs`; modify `DependencyInjection.cs`
- Create (Api): `Documentos/DocumentosEndpoints.cs` (as três rotas do RH; a Task 8 acrescenta as de `/api/eu`); modify `Program.cs`
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha `listarDocumentosDoFuncionario`, `adicionarDocumentoDoFuncionario` e `baixarDocumentoDoFuncionario`: **nunca** o prefixo `/api/rh/funcionarios`, que pegaria rotas das fatias 3, 4, 5, 6 e 7), `Infra/ApiFactory.cs` (`ComFalhaNoBancoAoSalvarDocumento()`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Test: `tests/RHSystem.Api.IntegrationTests/Documentos/DocumentosDoRhTests.cs`, `UploadDeDocumentoTests.cs`; `tests/RHSystem.Application.Tests/Documentos/RotulosDeDocumentoTests.cs`

**Interfaces:**
- Consumes: `DocumentoDoColaborador` e enums (Task 1); `IArmazenamentoDeArquivos`, `CaminhosDeArquivo`, `IdentificadorDeTipoDeArquivo`, `NomeParaDownload`, `IFonteDeReferenciasDeArquivo`, `LinkTemporario`, `ArmazenamentoEmMemoria` e `UnidadeDeTrabalhoEspia` (Task 6); `IAuditor.RegistrarEGravarAsync` (1a, Task 8); `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`; `IRelogio`; exceções; `GrupoRh()`, `ProduzProblemas`.
- Produces (nomes dos schemas do contrato):

```csharp
// RHSystem.Application.Documentos
static class LimitesDeDocumento { const long TamanhoMaximoBytes = 10 * 1024 * 1024; const long TetoDaRequisicaoBytes = 16 * 1024 * 1024; const string PastaNoArmazenamento = "documentos"; }
enum ModoDeDocumento { AnexarAgora, PedirParaEnviar }                                  // JSON: ANEXAR_AGORA, PEDIR_PARA_ENVIAR
sealed record DocumentoColaborador(int Id, TipoDeDocumento Tipo, string? Descricao, DateOnly? DataValidade, SituacaoDoDocumento Situacao,
    string? NomeArquivoOriginal, DateTime? PedidoEmUtc, DateTime? EnviadoEmUtc);          // nunca o caminho do arquivo
sealed record DocumentoColaboradorListaResposta(IReadOnlyList<DocumentoColaborador> Itens);
sealed record NovoDocumentoDoRh(string? Modo, string? Tipo, string? Descricao, string? DataValidade, ArquivoRecebido? Arquivo, int ArquivosRecebidos);   // texto cru do formulário; o caso de uso valida
sealed record ArquivoRecebido(string NomeOriginal, long Tamanho, Func<Stream> Abrir);   // a Api embrulha o IFormFile; o Application não depende do ASP.NET
static class RotulosDeDocumento { static string Tipo(TipoDeDocumento t); }              // "Documento de identidade (RG ou CNH)", "CPF", "Carteira de trabalho digital", "Comprovante de residência", "Contrato assinado", "Exame admissional (ASO)", "Outro"
sealed class DocumentosDoColaboradorCasoDeUso {
  Task<DocumentoColaboradorListaResposta> ListarAsync(int funcionarioId, CancellationToken ct);
  Task<DocumentoColaborador> AdicionarAsync(int funcionarioId, NovoDocumentoDoRh novo, CancellationToken ct);
  Task<LinkTemporario> BaixarAsync(int funcionarioId, int documentoId, CancellationToken ct); }
```

**Regras** (`:914-1048` do contrato):
- **Listar:** `404` se a pessoa não existe; vale também para **inativos**; ordem: **mais recentes primeiro** (`DataCadastro` desc, depois `Id` desc; decisão 7).
- **Adicionar** (`multipart/form-data`; campos `modo`, `tipo`, `descricao`, `dataValidade`, `arquivo`): valida **antes de qualquer efeito** com `ColetorDeErros`: `modo` e `tipo` do enum (400 no campo), `descricao` até 255, `dataValidade` `yyyy-MM-dd` válida (data passada é aceita); **`arquivo`** (400 em `arquivo`): `ANEXAR_AGORA` exige **um** arquivo, `PEDIR_PARA_ENVIAR` proíbe, mais de um arquivo (qualquer parte) é "sobrando", tamanho `0` ou acima de `TamanhoMaximoBytes` recusados, e **o tipo vem dos primeiros bytes** (`IdentificadorDeTipoDeArquivo`; fora de PDF, JPG e PNG é recusado). **Depois, nesta ordem (decisão 11): nenhuma transação fica aberta até o passo 3.**
  1. **Leitura:** carrega a pessoa (404 se não existe).
  2. **Blob** (só em `ANEXAR_AGORA`): `GravarAsync` do arquivo no caminho novo, `CaminhosDeArquivo.Novo("documentos", extensão do conteúdo)`, com o tipo de conteúdo vindo dos bytes.
  3. **Uma transação curta** (`ExecutarEmTransacaoAsync`): cria o documento (`Anexar` com o caminho e `NomeParaDownload.Limpar` no nome guardado, ou `Pedir`), `SalvarAsync` (a linha e o evento `Criou` do interceptor) e `COMMIT`.

  Se o passo 2 ou o 3 falhar, `ApagarAsync` do caminho (idempotente; se a limpeza também falhar, o erro **original** é o que sai e a sobra fica para a `LIMPAR_ARQUIVOS_ORFAOS`). Uma queda entre o 2 e o 3 deixa só a sobra, sem linha. **Cada chamada cria um documento novo.** `PEDIR_PARA_ENVIAR` **não enfileira e-mail nenhum** (`CONVENCOES.md`, seção 12). A auditoria é a do interceptor (`Criou` em `Documentos`, sem o nome do arquivo nem o caminho).
- **Baixar:** o documento tem de ser **da pessoa da rota** (senão 404, igual ao inexistente); `Pendente` = 422 `DOCUMENTO_SEM_ARQUIVO`; senão `RegistrarEGravarAsync` **primeiro** (`Documentos`/`Baixou`, "Baixou o documento {rótulo} de {nome}", `RegistroAfetado` "Documento {rótulo} de {nome}", entidade `FUNCIONARIOS_DOCUMENTOS`, id do documento) e **depois** `CriarLinkTemporarioAsync` (sem registro, sem link).
- A rota de upload leva `.DisableAntiforgery()` (autenticação por cabeçalho, sem cookie), `RequestSizeLimit` e `RequestFormLimits` de `TetoDaRequisicaoBytes`, `Accepts` de `multipart/form-data`, e corpo que **não** é formulário responde **415**.

- [ ] **Step 1: Escrever os testes que falham** (`ApiFactory` com `ArmazenamentoEmMemoria`; `ArquivosDeTeste.Pdf(n)`, `.Jpeg()`, `.Png()`, `.Exe()`, `.Gif()` com os cabeçalhos certos; relógio `2026-10-07T12:42:00Z`)

```csharp
// DocumentosDoRhTests
Lista_devolve_os_documentos_da_pessoa_mais_recentes_primeiro_com_os_campos_do_contrato_e_sem_caminho()
Funcionario_inexistente_devolve_404_nas_tres_rotas_e_listar_funciona_para_inativo()
Anexar_agora_com_PDF_devolve_201_ENVIADO_com_arquivo_no_armazenamento_nome_original_enviadoEmUtc_do_relogio_e_pedidoEmUtc_null()
Anexar_agora_aceita_JPG_e_PNG_e_o_tipo_vem_do_conteudo_nao_da_extensao_nem_do_Content_Type()     // PDF enviado como "x.jpg" é aceito e gravado como application/pdf
Anexar_agora_recusa_exe_chamado_pdf_e_gif_com_400_em_arquivo_e_nada_no_armazenamento()           // Review Focus 4
Pedir_para_enviar_devolve_201_PENDENTE_com_pedidoEmUtc_sem_arquivo_e_sem_nada_no_armazenamento()
Pedir_para_enviar_nao_enfileira_nenhum_email()                                                 // FILA_EMAILS inalterada
Erros_de_arquivo_voltam_400_em_arquivo()                                                        // [Theory]: anexar sem arquivo; pedir com arquivo; dois arquivos; 0 bytes; 10 MiB + 1 byte; (10 MiB exatos é aceito)
Erros_de_campo_voltam_400_com_o_nome_do_campo()                                                 // [Theory]: modo e tipo fora do enum, descricao com 256, dataValidade "2027-13-01"
Corpo_json_em_vez_de_formulario_devolve_415()
Dois_anexos_do_mesmo_tipo_criam_dois_documentos()
O_arquivo_so_e_gravado_depois_de_as_validacoes_passarem()                                       // pessoa inexistente (404) e cada 400 de campo ou de arquivo: Gravados vazio
Nenhuma_transacao_fica_aberta_durante_o_envio_ao_armazenamento()                                // UnidadeDeTrabalhoEspia: GravacoesComTransacaoAberta == 0 (Review Focus 4)
Falha_do_banco_ao_inserir_apaga_o_blob_e_nao_deixa_linha_nem_evento()                           // ComFalhaNoBancoAoSalvarDocumento: 500; Gravados tem 1 e Apagados o mesmo caminho; o armazenamento termina vazio; sem linha, sem evento Criou
Falha_do_armazenamento_nao_deixa_linha_nem_evento_e_tenta_apagar_o_caminho()                    // FalharNaGravacao: 500; nenhuma transação foi aberta; sem linha em FUNCIONARIOS_DOCUMENTOS e sem evento Criou; ApagarAsync chamado com o caminho tentado
Falha_ao_apagar_depois_da_falha_do_banco_devolve_o_erro_original_e_deixa_a_sobra_para_a_tarefa()   // FalharNaExclusao + ComFalhaNoBancoAoSalvarDocumento: o 500 vem do banco; a sobra fica; a LIMPAR_ARQUIVOS_ORFAOS (Task 6) a apaga depois do mínimo
Descricao_com_marcacao_html_e_guardada_e_devolvida_como_texto_literal()
Anexar_e_pedir_gravam_Criou_na_area_Documentos_sem_o_nome_do_arquivo_e_sem_o_caminho()         // a mudança "Arquivo" sai com ValorOculto e antes e depois nulos
Download_devolve_o_link_com_nome_para_download_e_grava_Baixou_antes_de_devolver()
Download_de_documento_pendente_devolve_422_DOCUMENTO_SEM_ARQUIVO_com_o_texto_do_contrato()
Download_de_documento_de_outra_pessoa_pela_ficha_errada_devolve_404_igual_ao_inexistente()      // Review Focus 5: title e detail iguais
Falha_ao_gravar_o_evento_de_download_nao_devolve_link()
Gestor_e_Funcionario_recebem_403_e_anonimo_401_nas_tres_rotas_e_nenhuma_rota_do_gestor_no_contrato_trata_de_documentos()
Respostas_batem_com_o_schema_do_contrato()                                                      // listar, adicionar (201) e baixar (200)
FonteDeReferenciasDosDocumentos_marca_como_referenciados_so_os_caminhos_com_linha()
RotulosDeDocumento_tem_o_rotulo_do_contrato_para_cada_tipo()
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~DocumentosDoRhTests|FullyQualifiedName~UploadDeDocumentoTests|FullyQualifiedName~RotulosDeDocumentoTests"`
Expected: FAIL (404 nas rotas).

- [ ] **Step 3: Implementar** DTOs, caso de uso, repositório, `RotulosDeDocumento`, a fonte de referências (registrada com `AddScoped<IFonteDeReferenciasDeArquivo, ...>`, prefixo `documentos/`) e as três rotas em `app.GrupoRh()`, com os códigos da tabela. O `POST` recebe `[FromForm]` (ou `HttpRequest`) e embrulha o `IFormFile` em `ArquivoRecebido`; `tipo` e `modo` chegam como texto. **O comparador de divergência da fatia 0 (Task 8) compara `multipart/form-data`** pelos nomes dos campos e pelo `required` do schema do formulário (campo de arquivo = `format: binary`), e esta task depende disso: as três rotas só entram no escopo com esse teste verde. **Se ele acusar diferença no schema do formulário** (por exemplo `tipo` e `modo` como `string` contra o enum do contrato), o comparador é a autoridade: declare o schema com o transformador de OpenAPI do .NET e **não mude o contrato**. O caso de uso segue a ordem das **Regras** (leitura → Blob → transação curta → apagar o blob se falhar); **nenhuma chamada a `GravarAsync` fica dentro de `ExecutarEmTransacaoAsync`**. **Neste passo, junto com as rotas**, acrescentar `listarDocumentosDoFuncionario`, `adicionarDocumentoDoFuncionario` e `baixarDocumentoDoFuncionario` à lista de escopo de `ContratoDivergenciaTests`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** as três rotas na seção "Rotas implementadas", `em andamento`, observação "o arquivo vai para o Azurite em desenvolvimento; em produção depende do Storage Account (Task 9)".
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Documentos|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests|FullyQualifiedName~Auditoria"`
Expected: PASS. **Se a matriz acusar 5xx na rota de upload** (corpo JSON `{}`), ela deve responder 415: o erro é da rota, não do teste.
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: documentos do colaborador pelo RH, com envio ao Blob e link temporario"`

---

### Task 8: Meus documentos (`/api/eu/documentos`) e a bolinha `documentosPendentes`

**Files:**
- Create (Application): `Documentos/MeusDocumentosCasoDeUso.cs`
- Create (Infrastructure): `Documentos/FonteDeDocumentosPendentes.cs`; modify `DocumentosRepositorio.cs`, `DependencyInjection.cs`
- Modify (Api): `Documentos/DocumentosEndpoints.cs` (as três rotas de `/api/eu`)
- Modify: `tests/.../Contrato/ContratoDivergenciaTests.cs` (a lista de escopo ganha `listarMeusDocumentos`, `enviarMeuDocumento` e `baixarMeuDocumento`), `contrato/STATUS.md` (seção "Rotas implementadas")
- Test: `tests/RHSystem.Api.IntegrationTests/Documentos/MeusDocumentosTests.cs`, `UploadConcorrenteTests.cs`; `Painel/DocumentosPendentesTests.cs`

**Interfaces:**
- Consumes: tudo da Task 7; `IEscopoDoUsuario` (1a, Task 6); `IFonteDeContadorDoMenu`, `ContadorDoMenu.DocumentosPendentes` (1a, Task 12); `GrupoEu()`.
- Produces:

```csharp
// RHSystem.Application.Documentos
sealed class MeusDocumentosCasoDeUso {
  Task<DocumentoColaboradorListaResposta> ListarAsync(CancellationToken ct);
  Task<DocumentoColaborador> EnviarArquivoAsync(int documentoId, ArquivoRecebido? arquivo, int arquivosRecebidos, CancellationToken ct);
  Task<LinkTemporario> BaixarAsync(int documentoId, CancellationToken ct); }
// RHSystem.Infrastructure.Documentos
sealed class FonteDeDocumentosPendentes : IFonteDeContadorDoMenu { ContadorDoMenu Contador => DocumentosPendentes; /* uma consulta: COUNT dos Pendentes de escopo.FuncionarioId */ }
```

**Regras** (`:1053-1176`): **só os documentos da pessoa logada**, o id sempre de `escopo.FuncionarioId` e **nunca** de um parâmetro; documento de outra pessoa = 404 (a consulta já filtra, como na 1a: "não existe" e "é de outro" dão o mesmo 404). Mesmo schema e mesma ordem da ficha do RH. **Enviar:** só `Pendente` (senão 422 `DOCUMENTO_NAO_PEDIDO`); os erros de arquivo são os da Task 7. **Ordem (decisão 11), sem transação aberta até o passo 3:** (1) **leitura:** carrega o documento da pessoa logada (404 se não é dela ou não existe; 422 se não está `Pendente`) e valida o arquivo; (2) **Blob:** `GravarAsync` no caminho novo (`CaminhosDeArquivo.Novo("documentos", extensão do conteúdo)`); (3) **transação curta** (`ExecutarEmTransacaoAsync`): `ReceberArquivo` e `SalvarAsync` (a linha e o evento `Alterou`); (4) falha no 2 ou no 3: `ApagarAsync` do caminho tentado (o erro original é o que sai). O **`Situacao` é token de concorrência**: dois envios simultâneos para o mesmo pendente **passam ambos pela leitura e ambos gravam o seu blob**; o segundo falha no `SalvarAsync` (a `DbUpdateConcurrencyException` vira 422 `DOCUMENTO_NAO_PEDIDO`) e **apaga o blob dele**: no fim há **um** arquivo no armazenamento e a linha aponta para ele. A pessoa não cria documento por conta própria (não há rota). **Baixar:** `Pendente` = 422 `DOCUMENTO_SEM_ARQUIVO`; **não grava evento** (decisão 5). **A bolinha:** a fonte conta só os `Pendente` da própria pessoa.

- [ ] **Step 1: Escrever os testes que falham**

```csharp
// MeusDocumentosTests
Lista_devolve_so_os_documentos_da_pessoa_logada_para_Funcionario_Gestor_e_RH()
Enviar_arquivo_de_documento_pendente_devolve_200_ENVIADO_com_nome_enviadoEmUtc_do_relogio_pedidoEmUtc_preservado_e_arquivo_gravado()
Enviar_para_documento_ja_enviado_devolve_422_DOCUMENTO_NAO_PEDIDO_com_o_texto_do_contrato_e_nada_e_gravado_no_armazenamento()
Enviar_ou_baixar_documento_de_outra_pessoa_devolve_404_igual_ao_do_inexistente()               // Review Focus 5: mesmo title e detail
Erros_de_arquivo_voltam_400_em_arquivo()                                                       // [Theory]: sem arquivo, gif, exe chamado pdf, 10 MiB + 1, dois arquivos
Falha_do_armazenamento_ou_do_banco_nao_muda_a_situacao_do_documento_e_nao_deixa_blob()       // FalharNaGravacao e ComFalhaNoBancoAoSalvarDocumento: 500; o documento segue Pendente; o armazenamento termina vazio
Nenhuma_transacao_fica_aberta_durante_o_envio_ao_armazenamento()                              // UnidadeDeTrabalhoEspia: GravacoesComTransacaoAberta == 0
Download_do_meu_documento_devolve_o_link_e_nao_grava_evento_e_pendente_devolve_422_DOCUMENTO_SEM_ARQUIVO()
Envio_grava_Alterou_na_area_Documentos_com_Situacao_de_Pendente_para_Enviado_e_Arquivo_sem_valor()   // o texto do enum sem diferenciar maiúsculas (formato do interceptor da 1a)
Anonimo_recebe_401_nas_tres_rotas_e_as_rotas_nao_listam_403()
Respostas_batem_com_o_schema_do_contrato()
// UploadConcorrenteTests
Dois_envios_simultaneos_para_o_mesmo_pendente_dao_um_200_e_um_422_e_sobra_um_unico_arquivo_o_do_perdedor_e_apagado()   // Barrier(2) no gancho AoGravar do ArmazenamentoEmMemoria: as duas passam pela leitura antes de qualquer uma salvar; Gravados tem 2, Apagados tem o do perdedor, e a linha aponta para o que ficou (Review Focus 4)
// Painel/DocumentosPendentesTests
Bolinha_documentosPendentes_conta_so_os_pendentes_da_propria_pessoa_e_zera_ao_enviar()          // GET /api/eu/contadores-do-menu como Funcionario, Gestor e RH
Pedido_do_RH_aparece_na_lista_e_na_bolinha_da_pessoa_e_nao_na_de_outra()                        // fluxo: o RH pede, a pessoa vê, envia, o RH baixa
Gestor_e_RH_somam_a_bolinha_so_na_rota_deles_do_funcionario_e_as_rotas_do_gestor_e_do_RH_nao_a_trazem()   // PainelContadoresGestor e PainelContadoresRh não têm o campo
```

- [ ] **Step 2: Rodar e ver falhar**

Run: `dotnet test back-end --filter "FullyQualifiedName~MeusDocumentosTests|FullyQualifiedName~UploadConcorrenteTests|FullyQualifiedName~DocumentosPendentesTests"`
Expected: FAIL.

- [ ] **Step 3: Implementar** o caso de uso, a fonte do contador (`AddScoped<IFonteDeContadorDoMenu, FonteDeDocumentosPendentes>`) e as três rotas em `app.GrupoEu()` (`ProduzProblemas` **sem 403**, com os códigos da tabela). `EnviarArquivoAsync` segue a ordem das **Regras** (leitura → Blob → transação curta → apagar o blob se falhar), reaproveitando a validação e o envio da Task 7 em vez de repeti-los. **Neste passo, junto com as rotas**, acrescentar `listarMeusDocumentos`, `enviarMeuDocumento` e `baixarMeuDocumento` à lista de escopo de `ContratoDivergenciaTests`.
- [ ] **Step 4: Atualizar `contrato/STATUS.md`:** as três rotas na seção "Rotas implementadas", `em andamento`; e, nas três rotas de `contadores-do-menu` (1a), a observação passa a "**documentos: ok (1c)**; avisos: 4; formulários: 5; ponto: 3; férias: 7".
- [ ] **Step 5: Rodar e ver passar**

Run: `dotnet test back-end --filter "FullyQualifiedName~Documentos|FullyQualifiedName~Painel|FullyQualifiedName~ContratoDivergenciaTests|FullyQualifiedName~Matriz|FullyQualifiedName~ContratoEStatusTests"`
Expected: PASS.
- [ ] **Step 6: Commit** (com autorização): `git add back-end contrato/STATUS.md; git commit -m "feat: meus documentos e a bolinha de documentos pendentes"`

---

### Task 9: Passo guiado do dono: criar o Storage Account no Azure (custo dito antes; o assistente não cria nada)

**Files:** Create: `docs/azure/passo-a-passo-fatia-1c.md` (roteiro com os **nomes e valores reais**, **sem segredos**); modify `README.md` (link). **Nenhum teste automatizado:** este passo **não bloqueia** as Tasks 1 a 8 nem o `dotnet test`; ele bloqueia **publicar a fatia 1c no ambiente da demo** (sem ele as rotas de arquivo devolvem 500 no Azure).

O dono **nunca usou Azure**: cada passo diz **onde clicar**, **o que escolher** e **por quê**. Os nomes das telas e opções abaixo são **de memória do portal e podem ter mudado** (não conferi com o portal aberto): se uma opção não existir com este nome, **parar e perguntar**, não adivinhar.

- [ ] **Step 1: Confirmar o ponto de partida.** A fatia 0 (Task 14) está concluída: a conta com os créditos, a **região** escolhida, o **grupo de recursos** do projeto, o **alerta de orçamento** criado e o App Service publicado. **Parar se algum faltar** (o Storage Account vai nesse mesmo grupo e região).
- [ ] **Step 2: Dizer o custo antes de criar** (roadmap, decisão 7). Abrir a calculadora de preços do Azure, escolher *Storage Accounts*, tipo *Blob Storage / StorageV2*, desempenho *Standard*, redundância *LRS*, camada *Hot*, a região escolhida, **1 GB** e o número de operações mais baixo; **anotar o valor mensal que a calculadora mostrar** e dizer ao dono. **Suposição minha, a confirmar na calculadora:** a conta paga por GB guardado e por operação, e para uma demo com alguns MB é da ordem de centavos por mês, debitados dos créditos. **Pedir o "sim" do dono antes de seguir.** Dizer também o que **não** custa nada de útil (parar o App Service não altera o Storage) e o que o desliga (apagar o Storage Account).
- [ ] **Step 3: O dono cria o Storage Account no portal** (assistente guia, dono clica). *Storage accounts* → *Create*: grupo de recursos **o do projeto**; nome `rhsystem` + sufixo (**3 a 24 caracteres, só letras minúsculas e números, único em todo o Azure**: se já existir, o portal avisa); região **a mesma do App Service**; *Performance* **Standard**; *Redundancy* **LRS** (a mais barata: a demo não precisa de cópia em outra região). Na aba *Advanced*: *Require secure transfer* **ligado**; *Allow enabling anonymous access on individual containers* **desligado**; *Minimum TLS version* **1.2**; *Enable hierarchical namespace* **desligado**; *Access tier* **Hot**. Na aba *Networking*: deixar o acesso público à rede (o App Service do plano barato não tem IP fixo; endurecer fica como melhoria futura). *Review + create* → *Create*.
- [ ] **Step 4: Criar o contêiner `arquivos`.** No Storage Account: *Data storage* → *Containers* → *+ Container*, nome `arquivos`, nível de acesso **Private (no anonymous access)**. (Os arquivos nunca são públicos: o link que a API devolve é assinado e vale poucos minutos.)
- [ ] **Step 5: Pôr a chave nas configurações do App Service, sem o assistente ver.** *Security + networking* → *Access keys* → *Show* → copiar a **Connection string** da *key1*. No App Service da API: *Settings* → *Environment variables* (ou *Configuration*) → *+ Add*: `Armazenamento__ConexaoBlob` = a connection string (**o dono cola direto; não manda pelo chat, não escreve em arquivo nenhum**), `Armazenamento__Container` = `arquivos`, `Armazenamento__SomenteHttps` = `true` → *Apply/Save* → deixar reiniciar. A connection string traz a **chave da conta**: quem a tem lê e apaga tudo; girar a chave (*Access keys* → *Rotate*) invalida esta configuração **e todos os links já emitidos**.
- [ ] **Step 6: Publicar e verificar** (o dono dispara `deploy-api` pelo GitHub Actions, como na fatia 0). Com o login do RH: pedir um documento a alguém e anexar um PDF na ficha; abrir o link de "Ver" (deve **baixar** o arquivo, não abrir uma página); no portal, *Containers* → `arquivos` mostra o arquivo `documentos/...`; o link, depois de alguns minutos, **recusa** (o portal do Storage devolve erro de assinatura expirada).
- [ ] **Step 7: Deixar o ambiente seguro para o custo.** Apagar os arquivos de teste; registrar no roteiro **como ver o custo** (*Cost Management* → filtro pelo grupo de recursos) e **como parar de gastar** (apagar o Storage Account, ou o grupo de recursos inteiro); conferir que o alerta de orçamento da fatia 0 cobre o grupo.
- [ ] **Step 8: Registrar e commitar** (com autorização): escrever `docs/azure/passo-a-passo-fatia-1c.md` com o nome real do Storage Account, a região, o custo que a calculadora mostrou e os passos acima **sem nenhuma chave**; acrescentar o link ao `README.md`; `git add docs/azure README.md; git commit -m "docs: roteiro do Storage Account da fatia 1c"`.

---

### Task 10: Verificar a fatia inteira

**Files:**
- Create: `tests/RHSystem.Api.IntegrationTests/Contrato/EscopoDaDivergenciaTests.cs`
- Modify: `contrato/STATUS.md`, `README.md` (a lista de escopo de `ContratoDivergenciaTests` já é visível aos testes desde a 1a, Task 7, Step 1: a 1c não a extrai de novo)

- [ ] **Step 1: Conferir que o escopo tem as 21 operações.** O escopo é a **lista de `operationId`** de `ContratoDivergenciaTests` (nunca prefixo de caminho; por isso `/api/rh/beneficios` não pega `/api/rh/beneficios-adesoes` e `/api/rh/funcionarios` não pega as rotas das outras fatias). Cada tarefa já acrescentou os `operationId` dela no passo que implementou as rotas; a prova de que nenhuma rota implementada ficou fora da lista é **uma só**, da 1a (Task 7, `Toda_rota_implementada_esta_no_conjunto_do_escopo_da_divergencia`); este passo confere apenas que os 21 da 1c estão na lista. Escrever:

```csharp
Escopo_inclui_os_21_operationId_da_fatia_1c()                               // lista literal da tabela "Rotas e códigos"; o escopo pode ter mais (fatias 0, 1a e 1b), nunca menos
// "toda rota implementada está no escopo" não se repete aqui: é a prova única da 1a (Task 7); esta task só confere os 21 da 1c
```

Run: `dotnet test back-end --filter "FullyQualifiedName~EscopoDaDivergenciaTests|FullyQualifiedName~ContratoDivergenciaTests"`
Expected: PASS.
- [ ] **Step 2: Conferir as 21 operações.** A seção "Rotas implementadas" do `STATUS.md` tem uma linha por `operationId` da tabela "Rotas e códigos" (21), todas `em andamento`; a observação de `contadores-do-menu` foi atualizada (Task 8). O `ContratoEStatusTests` da 1a prova que o `STATUS` e a API concordam. A tabela por fatia não é tocada.
- [ ] **Step 3: Verificar tudo**

Run: `dotnet build back-end; dotnet test back-end; dotnet format back-end --verify-no-changes`
Expected: build sem warnings; **todos** os testes passando (fatia 0, 1a, 1b e 1c: conformidade, matriz, escopo, auditoria, fila, agendador, painel, cadastros, empresa, gestores, armazenamento, documentos); `dotnet format` sem alterações.
- [ ] **Step 4: Verificação manual ponta a ponta** (Docker): `docker compose up -d --wait` (MySQL, Mailpit e Azurite `healthy`); API como na 1a (`Database:AplicarScriptsAoIniciar` e o seed do admin por user-secrets). Com `curl.exe` e o token do admin (a URL da API é a do `README.md`; **todo corpo JSON vai de arquivo**: no Windows PowerShell 5.1 as aspas duplas de um JSON passado em linha chegam quebradas ao programa nativo, suponho, não medido, como na nota da Task 3 da fatia 0; crie `$env:TEMP\corpo.json` **fora do repositório** a cada chamada, com o JSON do exemplo do contrato, e passe `-H "Content-Type: application/json" --data-binary "@$env:TEMP\corpo.json"`; ou faça o passo no Git Bash): (a) `PUT /api/rh/empresa` com o corpo do exemplo do contrato e `GET /api/eu/empresa`; (b) `POST /api/rh/cargos` e `POST /api/rh/setores` do exemplo, e `GET` de cada um; (c) `POST /api/rh/funcionarios/{id do admin}/documentos` com `curl.exe -F "modo=ANEXAR_AGORA" -F "tipo=IDENTIDADE" -F "arquivo=@<um PDF pequeno>;type=application/pdf"`; (d) `GET .../documentos/{id}/download` e baixar o `url` com `curl.exe -L -D - -o saida.pdf`: **`Content-Disposition: attachment`** e bytes iguais; (e) `GET /api/rh/auditoria`: eventos `CRIOU` (empresa, cargo, setor, documento) e `BAIXOU`; (f) `http://localhost:8025` (Mailpit) **vazio**: nada disso manda e-mail. Registrar o resultado no `README.md`.
- [ ] **Step 5: Commit** (com autorização): `git add back-end contrato/STATUS.md README.md; git commit -m "test: conferencia do escopo da divergencia e verificacao da fatia 1c"`

---

## Para as fatias 2 em diante

O que a 1c produz e que as seguintes **consomem** (caminhos sob `back-end/src/`). **Nomes e assinaturas não mudam depois**; quem precisar de outra coisa acrescenta, não renomeia.

**Arquivos (Blob)**
- `IArmazenamentoDeArquivos` (`GravarAsync`, `CriarLinkTemporarioAsync`, `ApagarAsync`), `LinkTemporario`, `IListagemDeArquivos`, `ArquivoGuardado`, `CaminhosDeArquivo`, `IdentificadorDeTipoDeArquivo`, `NomeParaDownload`, `IFonteDeReferenciasDeArquivo`: `RHSystem.Application/Arquivos/`. Implementação `ArmazenamentoBlob` em `RHSystem.Infrastructure/Arquivos/`; nos testes, `ArmazenamentoEmMemoria` (já registrado no `ApiFactory`), `UnidadeDeTrabalhoEspia`, `ArquivosDeTeste` e `AzuriteFixture`.
- **A receita do envio** (holerite na 4b, atestado na 3b, certificado na 6b, módulo de treinamento na 6a, currículo e resposta de formulário na 5, planilha na 2), **a mesma das Tasks 7 e 8 (decisão 11)**: (1) validar tudo, inclusive o que se lê do banco; (2) `GravarAsync` no Blob, **sem transação aberta**; (3) **uma transação curta** (`ExecutarEmTransacaoAsync`) que insere ou atualiza a linha e o evento; (4) se o passo 2 ou o 3 falhar, `ApagarAsync` do caminho (idempotente; a falha da limpeza não esconde o erro original). A sobra de uma queda entre o 2 e o 3 é apagada pela `LIMPAR_ARQUIVOS_ORFAOS` depois de 24 h. Teste de cada fatia: `GravacoesComTransacaoAberta == 0`. Caminho **sempre** de `CaminhosDeArquivo.Novo(pasta, extensão)` (GUID, nunca o nome do usuário); o nome do usuário vai só para a coluna `NOME_ARQUIVO_ORIGINAL` e para `NomeParaDownload`; o tipo vem de `IdentificadorDeTipoDeArquivo` (acrescente tipos, não renomeie: xlsx na fatia 2). Cada limite de tamanho (holerite 5 MB) é do chamador; o teto da requisição vem de `RequestSizeLimit` na rota.
- **Cada fatia que guarda caminho registra uma `IFonteDeReferenciasDeArquivo`** (prefixo próprio: `holerites/`, `atestados/`...). Sem a fonte, a limpeza `LIMPAR_ARQUIVOS_ORFAOS` **nunca toca** o prefixo (seguro, mas as sobras ficam).
- **Download:** a rota devolve `LinkTemporario` e **grava o evento `Baixou` antes** (`IAuditor.RegistrarEGravarAsync`), se o contrato pedir. O **front abre o link por navegação** (`<a href>`/`window.location`), **nunca por `fetch`**: a política do site (`connect-src 'self' <API>`) barra o domínio do Storage e o Storage não tem CORS (suposição, a conferir no script da política do front).
- Configuração `Armazenamento:*` e o Azurite no `docker compose`; o Storage Account do Azure é o da Task 9 (já existe quando a 2 começar).

**Empresa**
- `IConfiguracaoDaEmpresa` (`ObterAsync`, `ObterOuFalharAsync`) e `ConfiguracaoDaEmpresaLida` (`HorarioEntrada`, `HorarioSaida`, `ToleranciaAtrasoMinutos`, `DiaPagamento`, `Uf`, `Municipio`, `NomeEmpresa`): `RHSystem.Application/Empresa/`. **Fatia 3** (situação de hoje: `SEM_REGISTRO` depois da entrada **mais** a tolerância; feriados pela UF e município) e **fatia 7c** (próximo pagamento) usam `ObterOuFalharAsync`: a operação que precisa da empresa responde **422 `EMPRESA_NAO_CONFIGURADA`** (`CONVENCOES.md`, seção 11). A fatia 8 semeia a empresa por SQL (`CONFIGURACOES_EMPRESA` tem uma linha só, `Id = 1`).
- O 404 com `codigo` (`EMPRESA_NAO_CONFIGURADA`) sai por `ProblemaDeApiException` (1b); a 1c não alterou `NaoEncontradoException`.

**Gestor ausente e pedidos parados** (CONVENCOES, seção 9)
- `IGestorAusente` (`EtapaInicialDoPedidoAsync`, `EstaAusenteHojeAsync`), `EtapaInicial`, `EtapaInicialDoPedido`: `RHSystem.Application/Gestores/`. **As fatias 3 (pedido de ajuste de ponto) e 7a (férias) a chamam ao criar o pedido** e gravam o que ela devolve (`GestorId`, e a etapa do gestor já aprovada, automática, quando `EtapaDoGestorAprovadaPorAusencia`). Ela **não** decide `PEDIDO_RH_FORA_DE_GESTORES` nem `PEDIDO_PROPRIO` (são da 3 e da 7).
- `IPedidosParadosNoGestor` (`Task<PedidosAprovados> AprovarEtapaDoGestorAsync(int gestorId, CancellationToken)`, `Task PassarParaORhAsync(int gestorId, CancellationToken)`) **é da 1a**, com o composto padrão **vazio** registrado; **as fatias 3 e 7 não trocam a porta**: cada uma implementa `IPedidosParadosNoGestorDaFatia` sobre as tabelas dela e a registra, e o composto as soma. A etapa do pedido é decidida na criação (`IGestorAusente`) e só muda por **ação explícita** de quem tira o gestor: a **1c** chama `AprovarEtapaDoGestorAsync` ao ligar "Gestor ausente" e `PassarParaORhAsync` ao trocar ou tirar o gestor de um setor; a **1b** chama `PassarParaORhAsync` ao efetivar o desligamento de um gestor e na readmissão (que tira o gestor do setor).
- **Dívidas registradas para as fatias 3 e 7 (e no `STATUS.md`, nas observações de `configurarAusenciaDoGestor` e `editarSetor`):** (a) implementar as duas ações sobre as colunas do `V001` (`RESULTADO_GESTOR`, `ETAPA_GESTOR_AUTOMATICA`, `ID_GESTOR`, `ID_FUNCIONARIO_RH`), cujo modelo é delas: a 1c não supõe nenhum; a decisão final do RH exige `ID_GESTOR` preenchido e diferente da pessoa (`Criacao_Triggers:549-552` e `:967-970`); (b) **`pedidosAprovadosAgora`**: o contrato pede o número real (`20-fatia-1:3216-3224`) e a porta o devolve em `PedidosAprovados`: cada fatia devolve quantos aprovou do tipo dela (a 3, `AjustesDePonto`; a 7, `Ferias`; o outro campo, 0), o composto da 1a soma e a 1c o repete na resposta; (c) a 3 e a 7 **não** substituem a porta: cada uma registra a sua `IPedidosParadosNoGestorDaFatia`, e o composto da 1a as junta.

**Painel**
- `DocumentosPendentes` já tem fonte (`FonteDeDocumentosPendentes`). A observação de `STATUS.md` dos contadores diz o que falta (avisos: 4; formulários: 5; ponto: 3; férias: 7).

**Cadastros e testes**
- `Cargo.Descricao`/`Criar`/`Atualizar` (a `CargaSemanalHoras` é da 1b), `Setor.Descricao`/`Gestor`/`Criar`/`Atualizar`, `Gestor.Ausente`/`AusenteAte`/`AusenteEm`, `Beneficio.Criar`/`Atualizar` (as três marcas são da 1a), `Funcionario.DefinirNivel` (se a 1c o criou), `ErrosDoBanco` (`EhChaveRepetida`, `EhChaveEstrangeiraEmUso`), `FuncionarioResumo` (`Application/Colaboradores/`; as fatias 3 e 7 o reaproveitam). **`BeneficioDoFuncionario`, `NomesDeNivel`, `GestorReferencia` e `Setor.DefinirGestor` são da 1b**: a fatia 7 estende `BeneficioDoFuncionario` com os campos do pedido de adesão, não cria outra entidade para `BENEFICIOS_FUNCIONARIOS`.
- `MensagensDoApoio` (códigos e textos do apoio, conferidos contra o contrato).
- Testes: `ApoioDeTeste` (sementes por SQL do que a `SementeDoCadastro` da 1b não tem: cargos com descrição ou sem carga, benefícios com as marcas, pedidos de benefício, setores, pessoas e documentos), `ArquivosDeTeste`, `ApiFactory.ComPedidosParados(IPedidosParadosNoGestor)` e `PedidosParadosEspiao` (as fatias 3 e 7 os usam para testar a 1c com a implementação delas), `ComGestorDoFuncionarioEspiao()` e `ComFalhaNoBancoAoSalvarDocumento()`; a **lista de `operationId` do escopo** de `ContratoDivergenciaTests` (a prova "toda rota implementada está no escopo" é da 1a, Task 7, e vale para as fatias seguintes) e `EscopoDaDivergenciaTests` (só os 21 da 1c): as fatias seguintes só acrescentam os `operationId` delas, no passo que implementa as rotas.

---

## Autorrevisão (cobertura do contrato, da 1c, e das regras)

**Cobertura: cada operação do contrato que é desta fatia → tarefa**

| operationId | Tarefa | | operationId | Tarefa |
|---|---|---|---|---|
| `listarCargos` | 2 | | `listarBeneficiosDoCatalogo` | 2 |
| `criarCargo` | 2 | | `criarBeneficioDoCatalogo` | 2 |
| `editarCargo` | 2 | | `editarBeneficioDoCatalogo` | 2 |
| `listarSetores` | 5 | | `excluirBeneficioDoCatalogo` | 2 |
| `criarSetor` | 5 | | `obterEmpresa` | 3 |
| `editarSetor` | 5 | | `salvarEmpresa` | 3 |
| `listarGestores` | 4 | | `listarDocumentosDoFuncionario` | 7 |
| `cadastrarGestor` | 4 | | `adicionarDocumentoDoFuncionario` | 7 |
| `configurarAusenciaDoGestor` | 4 | | `baixarDocumentoDoFuncionario` | 7 |
| `listarMeusDocumentos` | 8 | | `enviarMeuDocumento` | 8 |
| `baixarMeuDocumento` | 8 | | | |

**21 de 21** (contrato `20-`: 43 operações; 3 são da 1a, auditoria; 19 da 1b: colaboradores, equipe, usuários do sistema e senha). Os outros pedidos da fatia:

| Pedido | Onde |
|---|---|
| Nome único; um gestor por setor; só `Gestor` gerencia setor (`GESTOR_PRECISA_TER_PERFIL_GESTOR`); trocar ou tirar passa os pedidos ao RH; quem deixa de ser gestor volta a `Funcionario` | Task 5 (regras e testes): chama `IGestorDoFuncionario.RefazerDosMembrosAsync` (1b) e `IPedidosParadosNoGestor.PassarParaORhAsync` (1a) |
| Catálogo com `OPCIONAL`, `ACEITA_DEPENDENTES`, `ACEITA_TRAJETO`; excluir só se nunca usado | Task 2 |
| "Gestor ausente" e o efeito sobre pedidos já parados de ponto e de férias, como serviço que 3 e 7 chamam | Task 4: `IGestorAusente` (pedidos novos) e a chamada a `IPedidosParadosNoGestor.AprovarEtapaDoGestorAsync` (pedidos parados; porta da 1a); só tem efeito depois das fatias 3 e 7, e `pedidosAprovadosAgora` repete o que a porta devolve (`PedidosAprovados`: 0 e 0 enquanto o composto for o vazio; decisão 2) |
| Configuração da empresa, `EMPRESA_NAO_CONFIGURADA`, leitura para 3 e 7 | Task 3: `IConfiguracaoDaEmpresa` |
| Documentos: pedir ou anexar, multipart, download por link temporário como anexo, Blob com Azurite, envio fora de transação (validar, gravar no Blob, transação curta, apagar o blob se falhar), limpeza de sobras, tipos e tamanhos, bolinha | Tasks 6, 7 e 8 |
| Matriz de perfis das rotas | automática (1a, Task 7) rodada em cada task; testes de 401 e 403 por grupo de rotas; Task 10 prova o escopo |
| Storage Account com o custo dito antes, guiado, sem criar nada | Task 9 |
| Nenhum e-mail novo; auditoria dos eventos; conteúdo sem HTML | `Pedir_para_enviar_nao_enfileira_nenhum_email` (7); marcas de auditoria (1) e testes de evento em cada task; `Descricao_com_marcacao_html...` (2 e 7) |

**Tipos coerentes entre tarefas:** `MensagensDoApoio` e `ErrosDoBanco` nascem na Task 1 e são usados nas 2 a 8; `GestorReferencia`, `SetorReferencia`, `NomesDeNivel`, `BeneficioDoFuncionario`, `Setor.DefinirGestor(int?)` e `IGestorDoFuncionario.RefazerDosMembrosAsync` são da 1b e `IPedidosParadosNoGestor` é da 1a (a 1c só os consome: Tasks 2, 4 e 5); `FuncionarioResumo` nasce na 4 (a 1b não o criou); `Funcionario.DefinirNivel` nasce na 1 e a 5 o usa; `IArmazenamentoDeArquivos`, `LinkTemporario`, `CaminhosDeArquivo`, `IdentificadorDeTipoDeArquivo` e `NomeParaDownload` nascem na 6 e são usados na 7 e na 8; `ArquivoRecebido` nasce na 7 e a 8 o usa; `LimitesDeDocumento` na 7, usado na 8; `DocumentoColaborador` (DTO) e `RotulosDeDocumento` na 7, usados na 8; `UnidadeDeTrabalhoEspia` e o `ArmazenamentoEmMemoria` (com `AoGravar`, `FalharNaExclusao` e `GravacoesComTransacaoAberta`) na 6, usados na 7 e na 8; `ApiFactory.ComPedidosParados` e `PedidosParadosEspiao` na 4, usados na 5; `GestorDoFuncionarioEspiao` na 5.

**Review Focus → testes:** 1 → `Vinte_criacoes_simultaneas...` (2 e 5) e `...sem_diferenciar_maiusculas_nem_acentos...` (2); 2 → `TrocaDeGestorDoSetorTests` (5, inclusive `Falha_no_meio_da_troca_desfaz...` e `Gestor_novo_que_ja_e_membro...`); 3 → `AusenciaDoGestorTests` (4: dia anterior à volta, virada do dia, `GESTOR_SEM_EQUIPE`, porta que lança); 4 → `UploadDeDocumentoTests`, `UploadConcorrenteTests` (um 200, um 422 e um só arquivo), `Nenhuma_transacao_fica_aberta_durante_o_envio...` (7 e 8), `Falha_do_banco_ao_inserir_apaga_o_blob...` (7), `NomeParaDownloadTests` e `Nome_com_aspas...` (6, 7 e 8); 5 → `...de_outra_pessoa...404_igual...` (7 e 8), `Gestor_e_Funcionario_recebem_403...` e a bolinha (8).

**Proporção:** o plano tem dez tarefas (a 1a tem quinze: 1.1, 1.2, 2 a 7, 8.1, 8.2 e 9 a 13) para 21 operações e uma interface de arquivos que as fatias 2 a 8 reaproveitam; o código aparece só em assinaturas, nomes de teste e nos valores do contrato (textos de erro, limites, rótulos). O que **não** foi feito aqui: nenhuma tabela de ponto ou de férias é mapeada (a porta `IPedidosParadosNoGestor` espera as fatias 3 e 7); nenhum e-mail; nenhuma varredura de vírus nos arquivos (fora do contrato; suposição: aceitável para a demo).
