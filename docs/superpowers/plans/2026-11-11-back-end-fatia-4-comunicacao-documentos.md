# Back-end fatia 4 (comunicação e documentos) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) or superpowers:subagent-driven-development to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** o RH publica avisos (rascunho, agendado, ativo, encerrado) e acompanha quem leu; envia holerites em PDF (um a um, em lote, com nova versão) e o funcionário baixa os dele; o RH e o gestor registram advertências e a pessoa dá ciência ou contesta; a API lista os aniversariantes dos próximos 14 dias.

**Architecture:** mesmo molde da fatia 3: regras puras em `Application/Comunicacao/Regras` (situação do aviso, prévia, próximo pagamento, desfecho da advertência, janela de aniversário), casos de uso por família de rota (`/api/eu`, `/api/gestor`, `/api/rh`) sobre repositórios EF, escritas críticas por UPDATE condicional (o banco decide a corrida), e-mails pela fila (`IFilaDeEmails`, chave de idempotência por mensagem) e publicação agendada por tarefas do agendador com recuperação. Holerite segue a receita da 1c (validar tudo, subir o PDF, transação curta, apagar o PDF se falhar), porque o banco nunca apaga holerite. O contrato é lei.

**Tech Stack:** .NET 10 / EF Core 9 / Pomelo 9.0.0 / MySQL 8.4; nenhuma biblioteca nova (PDFsharp só em teste para gerar PDF de prova; PdfPig só em teste).

**Spec:** `contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml` (35 operações), `contrato/CONVENCOES.md`, e o anexo `docs/superpowers/plans/2026-11-11-back-end-fatia-4-comunicacao-documentos-levantamento.md` (**leia as seções A a F dele antes de cada tarefa: tem os códigos de erro com os textos exatos, as colunas, os triggers e as armadilhas**). Roadmap: `docs/superpowers/plans/2026-10-28-roadmap-completo.md`, "Fatia 4".

## Global Constraints

- Mesmas da fatia 3 (`2026-11-11-back-end-fatia-3-ponto.md`, "Global Constraints"): .NET 10, EF Core 9 sem migrations, Domain sem dependência, casos de uso como classes simples, **`V001` a `V005` não se editam** (faltando coluna, índice ou tabela: para e pergunta; `V006` em diante é do dono), **contrato é lei** (rota, campo e `codigo` não se inventam), escopo de `ContratoDivergenciaTests` = lista de `operationId` acrescentada no passo que implementa a rota, toda rota declara **todos** os códigos do contrato, `contrato/STATUS.md` ganha a linha da rota, **e-mail novo só da lista fechada do contrato** (`AVISO_PUBLICADO`, `AVISO_LEMBRETE`, `HOLERITE_PUBLICADO`, `ADVERTENCIA_REGISTRADA`, `ADVERTENCIA_RESPONDIDA`), `front-end/` é do Lucas, código em português sem acento e mensagem ao usuário em português com acento, `TreatWarningsAsErrors` e `dotnet format back-end --verify-no-changes`.
- **Commits e PRs:** mensagens em português (`feat:`, `test:`, `docs:`); **nunca** `Co-Authored-By`, `Claude-Session` nem "Generated with Claude Code"; `git push` a cada tarefa; no fim, PR e `gh pr merge N --squash --admin` (autorizado pelo dono).
- **Tempo:** todo instante na aplicação é UTC (`IRelogio`); as tabelas do grupo (`AVISOS`, `AVISOS_VISUALIZACOES`, `HOLERITES`, `ADVERTENCIAS`) guardam -03:00 (`ConversoresDeTempo.Brasil`); `FILA_EMAILS` e `TAREFAS_AGENDADAS` são UTC; "hoje" é `FusoBrasil.Hoje(relogio.AgoraUtc)`. **Nunca deixar o trigger preencher** `DATA_PUBLICACAO`, `DATA_VISUALIZACAO`, `DATA_ADVERTENCIA` nem `STATUS_ADVERTENCIA` (eles usam o `NOW()` real; os testes fixam 2026-10-07).
- **Privacidade:** holerite (salário) e advertência não vão para log; auditoria sem conteúdo do arquivo nem caminho; `PARAMETROS` de tarefa sem dado pessoal; download de holerite (RH e da própria pessoa) grava o evento **antes** de existir o link; o caminho do arquivo nunca sai da API.
- **Conteúdo de usuário** (aviso, advertência, contestação) é texto: a API guarda e devolve texto, o renderizador de e-mail faz `HtmlEncode`; nada de HTML cru.
- **Comandos longos** em segundo plano com `timeout` e vigia; mutação e suíte em paralelo (`C:\Users\User\dev\ferramentas-rh\mutar-em-paralelo.mjs`, `suite-paralela.sh`, `suite-ponto.sh` (adaptar para a fatia 4), até 10 trabalhadores); **não recompilar o repositório principal enquanto grupos de teste `--no-build` rodam**.
- **Controle de mutação:** nas tarefas de regra pura, por tarefa e no projeto `Application.Tests`; nas de rota, ao fim de cada incremento (4a, 4b, 4c), sobre os arquivos novos do incremento.

## Decisões deste plano (o contrato e o levantamento deixam em aberto)

Escolhas de engenharia; o dono pode mudar. Cada uma vira uma linha `Ruling:` no livro ao ser executada.

1. **Situação do aviso** é derivada das colunas e do `IRelogio` (levantamento C.4): `RASCUNHO` = `ENVIADO=FALSE`; `AGENDADO` = enviado e publicação no futuro; `ATIVO` = publicado e sem expiração vencida; `EXPIRADO` = expiração ≤ agora. **Não existe estado "encerrado" nem "excluído"** (encerrar = expiração agora; excluir = apagar). A visibilidade não depende do agendador: o aviso agendado aparece sozinho na hora; a tarefa só manda o e-mail e completa as linhas de leitura.
2. **Linhas de leitura:** o trigger cria a linha dos ativos no INSERT do aviso; a aplicação **completa na publicação efetiva** (`INSERT ... SELECT` dos ativos sem linha, idempotente pela chave única) e cria a linha de quem entrou depois ao marcar lido. `viram`, `aindaNaoViram` e `total` contam só pessoas **ativas hoje** (linhas de quem saiu ficam no banco e fora da conta); o lembrete vai para o mesmo conjunto (os exemplos do contrato dão `quantidadeDeEmails = aindaNaoViram`). Custo se errado: o percentual de leitura difere um pouco.
3. **Rascunho sem data:** o trigger sempre grava `NOW()`; criar com `publicacaoEmUtc: null` faz `UPDATE DATA_PUBLICACAO = NULL` na mesma transação. Datas de `publicarEmUtc` e de `expiraEm` (fim do dia -03:00, `23:59:59`) são truncadas ao segundo; encerrar grava expiração = agora truncada e nunca antes da publicação (senão 3819).
4. **E-mail do aviso sai pela tarefa**, nunca dentro do pedido HTTP: `PUBLICAR_AVISO:aviso={id}:em={instanteUtc}` com `ExecutarApos` = publicação (também para "agora"); a tarefa relê o aviso e **só manda se ele ainda está publicado com aquela data**; editar a data de um agendado agenda outra chave (a chave de tarefa cancelada nunca volta). Destinatários: ativos com `DATA_ADMISSAO ≤ hoje` e e-mail corporativo; quem não tem e-mail é pulado e registrado. A recuperação agenda só o que ainda é futuro ou publicado há menos de 7 dias sem e-mail enfileirado (a fila apaga em 90 dias, então reenfileirar tudo reenviaria).
5. **Lembrete:** chave por clique (`AVISO_LEMBRETE:aviso={id}:func={id}:em={instanteUtc}`), 202 com `quantidadeDeEmails`; duplo clique em menos de 1 minuto devolve o mesmo resultado sem enfileirar de novo (a janela é uma escolha nossa; o contrato não limita).
6. **Holerite:** só PDF (variante da validação da 1c com tipos aceitos por parâmetro), ≤ 5 MiB, conferido pelo conteúdo (`%PDF-`); competência julgada pelo `IRelogio` com o `regraDoBanco` fixo do contrato; pessoa inexistente = 404 antes de qualquer efeito; versão = `MAX+1` e a chave única decide a corrida (1062 → `HOLERITE_JA_EXISTE` no envio, `HOLERITE_VERSAO_DESATUALIZADA` na nova versão); arquivo órfão é apagado quando o banco recusa; pasta `holerites/` com a fonte de referências. A pessoa vê a **maior versão já publicada**; agendado e versão substituída = 404 para ela.
7. **"No dia do pagamento":** a 4 implementa a **regra pura** do próximo pagamento (dia do mês; mês sem o dia usa o último; fim de semana ou feriado antecipa para o dia útil anterior; exceção de `PAGAMENTOS_EXCECOES` vale como está; de hoje em diante, hoje = publica agora) e a porta `IProximoPagamento`; a 7c reaproveita. **Não** liga o `proximoPagamento` da ficha (o contrato o dá à fatia 7). Sem a linha de `CONFIGURACOES_EMPRESA` = 422 `EMPRESA_NAO_CONFIGURADA` com o texto da 4 (não o de `ObterOuFalharAsync`). Pergunta ao dono.
8. **Lote:** item a item (o contrato diz; formato é `x-provisorio`), cada item com a receita da T9 em transação própria; erros do lote todo (400/422) antes de qualquer efeito; `ARQUIVO_INVALIDO` e `FUNCIONARIO_NAO_ENCONTRADO` só em `codigoDoErro`, com texto nosso; arquivo acima de 5 MiB vira item com erro, não 400 do lote; um evento de auditoria de resumo do lote mais um por holerite criado. O teto da requisição é 100 MiB mais folga dos campos.
9. **Advertência:** o desfecho da contestação não tem coluna e é **derivado** (`CANCELADA` ⇒ `ACATADA`; `APLICADA` com texto de contestação ⇒ `MANTIDA`; senão nulo); toda transição é UPDATE condicional (`WHERE STATUS_ADVERTENCIA = 'PENDENTE'` ou `'CONTESTADA'`), porque o trigger permite transições que o contrato proíbe; a **resposta do RH não vai para o banco**: só no e-mail e na auditoria (`VALOR_DEPOIS`, que é TEXT). "Mais recente registrado" = `ID_ADVERTENCIA` desc.
10. **Quem é "o RH" da cópia** (gestor registra advertência): todo usuário ativo de perfil RH com e-mail corporativo, uma mensagem por pessoa (a chave leva o destinatário). Pergunta ao dono.
11. **`ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO`** (sem texto no contrato, quase inalcançável porque o gestor do setor fica sem gestor): tradução defensiva do 45000, título "Operação recusada.", detalhe "O gestor não pode aplicar uma advertência a si mesmo.". `FUNCIONARIO_INATIVO` na rota do gestor só sai na corrida (inativado entre a leitura e o INSERT); no resto é 404 pela equipe.
12. **Aniversariantes:** ativos com `DATA_NASCIMENTO` e `DATA_ADMISSAO ≤ hoje`, hoje até +14 dias em -03:00, `29/02` vira `28/02` em ano comum, virada de ano (25/12 + 14), só `FuncionarioResumo` + `aniversarioEm` + `diasAte`; nunca o ano nem a idade.
13. **Auditoria:** avisos `Criou`/`Alterou`/`Excluiu`/`Publicou` (encerrar = `Alterou`); holerites `Criou` (envio, versão e cada item do lote) e `Baixou`; advertências `Criou`, ciência e contestação `Alterou`, resposta `Alterou` com o texto em `VALOR_DEPOIS`. Excluir aviso usa `ExecuteDelete` nas leituras com `// AUDITORIA-OK` e o evento explícito.
14. **Listas:** nenhuma lista da fatia 4 tem `ordenarPor`; a ordem é a do contrato. Gestor sem setor: listas vazias, resumo zerado, `porPessoa: []`, registrar = 404.
15. **Indicadores de outras fatias** que a 4 liga: `ContadorDoMenu.AvisosNaoLidos` (= `contagens.naoLidos`) e `IndicadorDaFicha.AdvertenciasQuantidade` (não canceladas do ano corrente).

## Review Focus

1. **Duplo clique e duas abas:** publicar aviso, enviar holerite, dar ciência, contestar, responder contestação e lembrar quem não viu, em paralelo: um efeito só, o outro 422/409 com o código certo, nunca 500 nem e-mail em dobro (T4, T6, T9, T12, T14).
2. **Dois RHs no mesmo aviso:** um muda a data do agendado enquanto o outro exclui ou publica; a tarefa antiga não pode mandar e-mail de aviso que não existe ou fora da hora (T4, T5).
3. **PDF ruim:** 6 MiB, PNG com extensão `.pdf`, arquivo vazio, banco recusando depois do upload (não fica arquivo nem linha órfã), lote com 200 PDFs e uma queda no meio (T9, T10).
4. **Fuso:** `publicarEmUtc` e "hoje" às 22:30 locais, `expiraEm` no fim do dia -03:00, agendado às 00:00 -03:00, aniversário em 28/02/2027 e 25/12 (T2, T7, T9, T15).
5. **Quem vê o quê:** holerite de outra pessoa, agendado ou substituído = 404; gestor só vê advertência da equipe e sem o texto da contestação; funcionário só as dele; aviso rascunho/agendado nunca aparece em `/api/eu` (T7, T11, T13, T14).

---

## Estrutura de arquivos (sob `back-end/`)

- `src/RHSystem.Domain/Comunicacao/`: `Aviso`, `LeituraDeAviso`, `Holerite`, `Advertencia` e enums (`TipoDeAviso`, `SituacaoDoAviso`, `TipoDeHolerite`, `SituacaoDoHolerite`, `TipoDeAdvertencia`, `SituacaoDaAdvertencia`, `DesfechoDaContestacao`).
- `src/RHSystem.Application/Comunicacao/`: `Regras/` (puras), `Mensagens/MensagensDaComunicacao.cs`, casos de uso por assunto (`Avisos/`, `Holerites/`, `Advertencias/`, `Aniversariantes/`), interfaces de repositório e de consulta, DTOs do contrato.
- `src/RHSystem.Infrastructure/Comunicacao/`: repositórios, consultas, `TradutorDeRecusaDaComunicacao`, tarefas (`PublicarAvisoTarefa`, `PublicarHoleriteTarefa`), renderizadores de e-mail, `FonteDeReferenciasDosHolerites`, `ProximoPagamento`; `Persistencia/Configuracoes/*Configuracao.cs`.
- `src/RHSystem.Api/Comunicacao/`: `AvisosRhEndpoints`, `AvisosEuEndpoints`, `HoleritesRhEndpoints`, `HoleritesEuEndpoints`, `AdvertenciasRhEndpoints`, `AdvertenciasGestorEndpoints`, `AdvertenciasEuEndpoints`, `AniversariantesEndpoints` (restrição `{id:int}` para `/resumo` e `/lote` não colidirem).
- `tests/RHSystem.Application.Tests/Comunicacao/` (regras) e `tests/RHSystem.Api.IntegrationTests/Comunicacao/` (rotas, banco real).

---

# Incremento 4a: avisos

## Task 1: Entidades e mapeamento das quatro tabelas

**Files:** Create `Domain/Comunicacao/*`, `Infrastructure/Persistencia/Configuracoes/{Aviso,LeituraDeAviso,Holerite,Advertencia}Configuracao.cs`; modify `AppDbContext`. Test: `Comunicacao/MapeamentoDaComunicacaoTests.cs`.

**Interfaces:** Produces as entidades com setters privados e fábricas; colunas anuláveis do banco declaradas anuláveis (`ADVERTENCIAS` quase toda); `FIXADO`, `ENVIADO`, `TIPO_HOLERITE`, `VERSAO`, `VISUALIZADO` **sem declarar o padrão do banco**; `HOLERITES.ANO` (`YEAR`) mapeado como inteiro com a sonda; `-03:00` com `ConversoresDeTempo.Brasil`.

- [ ] **Step 1: Testes (RED).** `MapeamentoContraBancoTests` passa a cobrir as 4 tabelas; sonda que grava `FIXADO=false`/`ENVIADO=false`, rascunho com `DATA_PUBLICACAO` NULL depois do `UPDATE`, holerite com `ANO` 2026 e `VERSAO` 2, advertência com todas as colunas anuláveis nulas, e relê.
- [ ] **Step 2: RED.** `dotnet test back-end/tests/RHSystem.Api.IntegrationTests -m:2 --filter "FullyQualifiedName~Persistencia"`. Expected: falha por entidade ausente.
- [ ] **Step 3: Implementar** como as entidades do ponto.
- [ ] **Step 4: GREEN + formato.** Mesmo filtro; `dotnet build` sem avisos.
- [ ] **Step 5: Commit.** `feat: entidades e mapeamento da comunicacao (fatia 4)`, `git push`.

## Task 2: Regras puras dos avisos

**Files:** Create `Application/Comunicacao/Regras/{SituacaoDoAviso,ExpiracaoDoAviso,PreviaDoAviso,ValidacaoDoAviso}.cs`. Test: `Application.Tests/Comunicacao/*`.

**Interfaces:** Produces `SituacaoDoAviso.De(bool enviado, DateTime? publicacaoUtc, DateTime? expiracaoUtc, DateTime agoraUtc)`; `ExpiracaoDoAviso.FimDoDia(DateOnly dia)` (23:59:59 -03:00 em UTC); `PreviaDoAviso.Montar(TipoDeAviso tipo, string conteudo)` (simples = a mensagem inteira; detalhado = texto puro sem markdown, parágrafos inteiros enquanto couber em 200, se o primeiro passar corta em 200, sem reticências; os exemplos 398 e 409 do contrato são casos de teste); `ValidacaoDoAviso` (simples ≤ 280, título 1-200, conteúdo 1-10000, `expiraEm` ≥ dia da publicação, todos os erros juntos).

- [ ] **Step 1: Testes (RED)** de cada regra com as bordas (agora igual à publicação; expiração igual a agora = `EXPIRADO`; título só de `##`; 280 e 281 caracteres; `expiraEm` no dia da publicação).
- [ ] **Step 2: RED**, `dotnet test back-end/tests/RHSystem.Application.Tests --filter "FullyQualifiedName~Comunicacao"`.
- [ ] **Step 3: Implementar.**
- [ ] **Step 4: GREEN + mutação** do projeto de regras (`gerar-spec`/`mutar-em-paralelo.mjs`).
- [ ] **Step 5: Commit.** `feat: regras puras dos avisos`.

## Task 3: Erros da fatia 4

**Files:** Create `Application/Comunicacao/Mensagens/MensagensDaComunicacao.cs`, `Infrastructure/Comunicacao/TradutorDeRecusaDaComunicacao.cs`. Test: `Comunicacao/MensagensDaComunicacaoTests.cs`.

**Interfaces:** Produces um catálogo com todos os `codigo`/`detail` da tabela A do levantamento (inclusive `regraDoBanco` fixo das competências e de `FUNCIONARIO_INATIVO`); tradutor do 45000 por mensagem e do 1062 de holerite pela operação; 1451/1452 → 404 ou falha consciente.

- [ ] **Step 1: Teste (RED)** que lê o yaml e compara cada código e texto do catálogo (como `MensagensDoPontoTests`).
- [ ] **Step 2: RED.** **Step 3: Implementar.** **Step 4: GREEN.**
- [ ] **Step 5: Commit.** `feat: erros da comunicacao`.

## Task 4: Gestão do aviso (#1 a #8)

**Files:** Create casos de uso em `Application/Comunicacao/Avisos/`, `AvisosRepositorio`, `ConsultaDeAvisos`, `AvisosRhEndpoints`; modify `ContratoDivergenciaTests` (escopo), `STATUS.md`. Test: `Comunicacao/Avisos*Tests.cs`.

**Interfaces:** Consumes T1 a T3. Produces `listarAvisos`, `criarAviso`, `obterResumoDosAvisos`, `obterAviso`, `editarAviso`, `excluirAviso`, `publicarAviso`, `encerrarAviso`. Criar rascunho faz o `UPDATE` da data para NULL; **publicar** é `UPDATE ... WHERE ENVIADO = FALSE` (segundo clique = 422 `AVISO_JA_PUBLICADO`) e grava a data efetiva; encerrar grava expiração agora truncada (≥ publicação); editar segue o contrato (data só em rascunho ou agendado; expiração vazia tira a expiração; expirado com expiração futura volta a ativo; não reenvia e-mail); excluir apaga leituras e aviso na mesma transação, cancela a tarefa e os e-mails pendentes.

- [ ] **Step 1: Testes (RED)** por rota: cada código do contrato, as quatro ordens da lista, `leitura` só em ativo e expirado, resumo (média inteira, `null` sem ativo, expiram em 7 dias), 400 de expiração anterior, 422 ao excluir publicado, duplo clique em publicar (dois pedidos em paralelo: um 200 e um 422), editar a data de agendado reagenda (chave nova), conformidade ao contrato e matriz de perfis.
- [ ] **Step 2: RED**, filtro `FullyQualifiedName~Comunicacao.Avisos`.
- [ ] **Step 3: Implementar** (casos de uso, repositório, endpoints, `// AUDITORIA-OK` no `ExecuteDelete`).
- [ ] **Step 4: GREEN + formato.**
- [ ] **Step 5: Commit.** `feat: gestao de avisos do RH`.

## Task 5: Publicação e e-mail do aviso

**Files:** Create `Infrastructure/Comunicacao/PublicarAvisoTarefa.cs`, `AvisoPublicadoRenderizador.cs`. Test: `Comunicacao/PublicarAvisoTests.cs`.

**Interfaces:** Produces `PublicarAvisoTarefa` (`PUBLICAR_AVISO:aviso={id}:em={instanteUtc}`, `RecuperarAsync` agenda o futuro e o publicado há menos de 7 dias sem e-mail); a tarefa completa as linhas de leitura dos ativos sem linha e enfileira `AVISO_PUBLICADO:aviso={id}:func={id}` (pula quem não tem e-mail); `ModeloDeEmail` com `HtmlEncode`.

- [ ] **Step 1: Testes (RED):** agendado dispara uma vez; rodar duas vezes não duplica; editar a data reagenda; excluir cancela; aviso editado ou excluído antes da tarefa não manda e-mail; 300 pessoas sem segurar o pedido HTTP; conteúdo com `<script>` sai escapado.
- [ ] **Step 2: RED. Step 3: Implementar. Step 4: GREEN.**
- [ ] **Step 5: Commit.** `feat: publicacao e e-mail do aviso`.

## Task 6: Leituras e lembrete (#9, #10)

**Files:** Create casos de uso, `AvisoLembreteRenderizador`. Test: `Comunicacao/AvisoLeiturasTests.cs`.

**Interfaces:** Produces `listarLeiturasDoAviso` (`viram`/`aindaNaoViram` independem do filtro; ordem por nome; 422 `AVISO_AINDA_NAO_PUBLICADO`) e `lembrarQuemNaoViuAviso` (202; só ativos não lidos com e-mail; chave por clique; janela de 1 minuto da decisão 5).

- [ ] **Step 1: Testes (RED):** os totais dos exemplos do contrato; `NAO_LIDO` com `tamanhoPagina=5`; lembrete duas vezes seguidas; 422 em aviso não ativo; ninguém sem ver devolve `quantidadeDeEmails: 0`.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: leituras e lembrete de aviso`.

## Task 7: Avisos da própria pessoa (#11 a #13) e a bolinha do menu

**Files:** Create casos de uso, `AvisosEuEndpoints`, `FonteDeContadorDeAvisos`. Test: `Comunicacao/MeusAvisosTests.cs`.

**Interfaces:** Produces `listarMeusAvisos`, `obterMeuAviso` (não marca lido), `marcarAvisoComoLido` (UPDATE condicional; sem linha = INSERT; 1062 = sucesso; data do `IRelogio`); registra `ContadorDoMenu.AvisosNaoLidos` = `contagens.naoLidos`.

- [ ] **Step 1: Testes (RED):** admitido depois vê e marca; rascunho, agendado e expirado = 404; fixados primeiro com `fixadosPrimeiro`; `contagens` não dependem de busca; duas marcações em paralelo mantêm a primeira data; contador do menu igual ao `naoLidos`.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, **mutação do incremento 4a** (arquivos novos). **Step 5: Commit.** `feat: avisos da propria pessoa` e `test: mutantes do incremento 4a`.

---

# Incremento 4b: holerites

## Task 8: Próximo pagamento

**Files:** Create `Application/Comunicacao/Regras/RegraDoProximoPagamento.cs`, `Application/Comunicacao/IProximoPagamento.cs`, `Infrastructure/Comunicacao/ProximoPagamento.cs`. Test: `Application.Tests/Comunicacao/RegraDoProximoPagamentoTests.cs` e `Comunicacao/ProximoPagamentoTests.cs`.

**Interfaces:** Produces `RegraDoProximoPagamento.Calcular(DateOnly hoje, int diaDoPagamento, IReadOnlyDictionary<(int Ano, int Mes), DateOnly> excecoes, Func<DateOnly, bool> ehDiaUtil)` e `IProximoPagamento.ObterAsync(DateOnly hoje, CancellationToken) : DateOnly?` (nulo sem `CONFIGURACOES_EMPRESA`); usa `ICalendarioDoPonto` para feriados e `Database.SqlQuery` em `PAGAMENTOS_EXCECOES`.

- [ ] **Step 1: Testes (RED):** fevereiro com dia 30 usa o último dia; dia em sábado, domingo e feriado antecipa; hoje é o dia = hoje; exceção do mês vale como está (sem antecipar); passou o dia vai para o mês seguinte; virada de ano.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, mutação da regra. **Step 5: Commit.** `feat: proximo pagamento`.

## Task 9: Envio individual e nova versão de holerite (#15, #17)

**Files:** Create casos de uso, `HoleritesRepositorio`, `PublicarHoleriteTarefa`, `HoleritePublicadoRenderizador`, `FonteDeReferenciasDosHolerites`, `HoleritesRhEndpoints`; modify `FormulariosDoContrato`, `ValidacaoDeArquivoDeDocumento` (tipos aceitos por parâmetro). Test: `Comunicacao/EnviarHoleriteTests.cs`.

**Interfaces:** Produces `enviarHolerite` e `substituirHolerite`: 404 antes de qualquer efeito, validação só PDF ≤ 5 MiB, `publicarEm` × `noDiaDoPagamento` exclusivos (400; dia passado 400), competência pelo `IRelogio` (`HOLERITE_COMPETENCIA_FUTURA` só vale fora de férias; anterior à admissão; posterior ao desligamento), versão `MAX+1`, blob → transação → apagar blob; publicação agora enfileira `HOLERITE_PUBLICADO:holerite={id}`; agendada cria `PUBLICAR_HOLERITE:holerite={id}` com `ExecutarApos` = 00:00 -03:00, que **só manda se a versão ainda é a maior publicável**.

- [ ] **Step 1: Testes (RED):** 6 MiB e PNG com extensão `.pdf` = 400; PDF vazio = 400; arquivo órfão apagado quando o banco recusa; dois envios paralelos = um 201 e um 409 `HOLERITE_JA_EXISTE` (texto montado com tipo e mês por extenso); duas novas versões paralelas = um 201 e um 409 `HOLERITE_VERSAO_DESATUALIZADA`; `noDiaDoPagamento` com e sem configuração; competência de 2100 chega ao trigger e devolve o `regraDoBanco`; agendado invisível até o dia; evento `HOLERITES`/`Criou`; nenhum caminho no log.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: envio de holerite e nova versao`.

## Task 10: Lote (#16)

**Files:** Create `EnviarHoleritesEmLoteCasoDeUso`. Test: `Comunicacao/HoleritesEmLoteTests.cs`.

**Interfaces:** Produces `enviarHoleritesEmLote`: sempre 200 com resultado por item; leitura de `itens` (JSON) e `arquivos`; 400 do lote para nome repetido, arquivo sem item, item sem arquivo, mais de 200 ou mais de 100 MiB, `itens` inválido; 422 `EMPRESA_NAO_CONFIGURADA` do lote todo; cada item usa a receita da T9 em transação própria (não cria versão: `HOLERITE_JA_EXISTE` vira erro do item).

- [ ] **Step 1: Testes (RED):** lote com uma linha inválida entre três; nome repetido; arquivo sem item; 200 PDFs; pessoa inexistente vira `FUNCIONARIO_NAO_ENCONTRADO`; PDF de 6 MiB vira item `ARQUIVO_INVALIDO`; retentativa do mesmo lote devolve `HOLERITE_JA_EXISTE` nos que já entraram.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: envio de holerites em lote`.

## Task 11: Listas e downloads de holerite (#14, #18, #19, #20)

**Files:** Create casos de uso, `ConsultaDeHolerites`, `HoleritesEuEndpoints`. Test: `Comunicacao/ListasEDownloadsDeHoleriteTests.cs`.

**Interfaces:** Produces `listarHoleritesDoFuncionario` (versão atual ou `todasAsVersoes`, `ultimoPublicado` ignora filtros), `obterLinkDeDownloadDoHolerite` (RH, qualquer versão), `listarMeusHolerites` (só publicados, maior versão publicada, `contagens` do ano, `anosDisponiveis`) e `obterLinkDeDownloadDoMeuHolerite`; o evento `HOLERITES`/`Baixou` é gravado **antes** do link e a falha dele devolve erro sem link.

- [ ] **Step 1: Testes (RED):** A não baixa o de B (404); agendado e versão substituída = 404 para a pessoa; com v2 agendada a v1 segue visível; RH baixa qualquer versão; auditoria antes do link; `ano` fora de 2000-2100 = 400; vale para inativo.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, **mutação do incremento 4b**. **Step 5: Commit.** `feat: listas e downloads de holerite` e `test: mutantes do incremento 4b`.

---

# Incremento 4c: advertências e aniversariantes

## Task 12: Advertências do RH (#21 a #26)

**Files:** Create casos de uso, `AdvertenciasRepositorio`, `ConsultaDeAdvertencias`, `AdvertenciaRegistradaRenderizador`, `AdvertenciaRespondidaRenderizador`, `AdvertenciasRhEndpoints`, `FonteDoIndicadorDeAdvertencias`. Test: `Comunicacao/AdvertenciasRhTests.cs`.

**Interfaces:** Produces `listarAdvertencias`, `obterResumoDasAdvertencias`, `obterAdvertencia`, `responderContestacaoDeAdvertencia` (`UPDATE ... WHERE STATUS_ADVERTENCIA = 'CONTESTADA'`; e-mail com a resposta na carga; resposta só na auditoria), `listarAdvertenciasDoFuncionario`, `registrarAdvertencia` (inativo 422 com `regraDoBanco`; gestor do colaborador ativo ou `ADVERTENCIA_SEM_GESTOR`; data futura 400; nasce PENDENTE com `STATUS` e `DATA_ADVERTENCIA` preenchidos pela aplicação; e-mail só à pessoa); registra `IndicadorDaFicha.AdvertenciasQuantidade`.

- [ ] **Step 1: Testes (RED):** cada código; desfecho derivado nos três casos; resumo com `setorComMaisRegistros` (empate alfabético, só não canceladas); `ano` pela data do ocorrido; resposta duas vezes = 422; pessoa sem setor fora do resumo; nada de conteúdo no log; conformidade ao contrato.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: advertencias do RH`.

## Task 13: Advertências do gestor (#27 a #31)

**Files:** Create casos de uso, `AdvertenciasGestorEndpoints`. Test: `Comunicacao/AdvertenciasGestorTests.cs`.

**Interfaces:** Produces `listarAdvertenciasDaEquipe`, `obterResumoDasAdvertenciasDaEquipe`, `obterAdvertenciaDaEquipe` (sem contestação nem desfecho), `listarAdvertenciasDoFuncionarioDaEquipe`, `registrarAdvertenciaDaEquipe` (`DaEquipe` em tudo; `ID_GESTOR` = gestor logado; e-mail à pessoa e cópia a cada RH ativo com e-mail, decisão 10).

- [ ] **Step 1: Testes (RED):** outra equipe, o próprio id e inativo = 404; corrida de inativação = 422 `FUNCIONARIO_INATIVO`; gestor sem setor = listas vazias; `porPessoa` com todos (inclusive zero); a cópia ao RH sai e o gestor não recebe cópia.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: advertencias do gestor`.

## Task 14: Advertências da própria pessoa (#32 a #34)

**Files:** Create casos de uso, `AdvertenciasEuEndpoints`. Test: `Comunicacao/MinhasAdvertenciasTests.cs`.

**Interfaces:** Produces `listarMinhasAdvertencias` (PENDENTE primeiro, depois data desc), `confirmarCienciaDeAdvertencia` e `contestarAdvertencia` (`UPDATE ... WHERE STATUS_ADVERTENCIA = 'PENDENTE'`; ciência vira APLICADA com `DATA_CIENCIA` do `IRelogio`; contestação 10-2000, nunca sobrescreve, sem e-mail).

- [ ] **Step 1: Testes (RED):** ciência duas vezes, contestar depois da ciência e ciência depois de contestar = 422 `ADVERTENCIA_NAO_ESTA_PENDENTE`; de outra pessoa = 404; ciência e contestação em paralelo = um efeito só; gestor e RH usam a própria família.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: advertencias da propria pessoa`.

## Task 15: Aniversariantes (#35)

**Files:** Create `Application/Comunicacao/Regras/JanelaDeAniversarios.cs`, caso de uso, consulta, `AniversariantesEndpoints`. Test: `Application.Tests/Comunicacao/JanelaDeAniversariosTests.cs` e `Comunicacao/AniversariantesTests.cs`.

**Interfaces:** Produces `JanelaDeAniversarios.ProximoAniversario(DateOnly nascimento, DateOnly hoje)` (29/02 em ano comum = 28/02; virada de ano) e `listarAniversariantes` (decisão 12).

- [ ] **Step 1: Testes (RED):** relógio em 28/02/2027 e em 25/12; hoje = aniversário (`diasAte` 0); dia 14 entra e 15 não; sem data de nascimento some; inativo e em admissão fora; resposta nunca traz ano nem idade.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, mutação da regra. **Step 5: Commit.** `feat: aniversariantes`.

## Task 16: Verificação da fatia 4

**Files:** Modify `contrato/STATUS.md` (35 linhas e as observações das linhas 32-34, 43-45 e 51-53), `back-end/README.md` (seção "Comunicação e documentos (fatia 4)" com as perguntas em aberto).

- [ ] **Step 1:** os 35 `operationId` no escopo de `ContratoDivergenciaTests`; matriz de perfis verde; `dotnet build back-end --no-incremental` sem avisos; `dotnet format back-end --verify-no-changes`.
- [ ] **Step 2:** mutação do incremento 4c (arquivos novos) e testes para cada sobrevivente.
- [ ] **Step 3:** suíte inteira em paralelo; revisor `fable` só leitura sobre o intervalo da fatia e **um** passe de correção RED→GREEN; ledger com `Final:`.
- [ ] **Step 4:** PR e `gh pr merge N --squash --admin`, sem trailer de Claude; sincronizar a `main`.

## Autorrevisão

- **Cobertura do contrato:** #1 a #8 (T4), #9 e #10 (T6), #11 a #13 (T7), #14 e #18 a #20 (T11), #15 e #17 (T9), #16 (T10), #21 a #26 (T12), #27 a #31 (T13), #32 a #34 (T14), #35 (T15); T8 sustenta `noDiaDoPagamento`; T1 a T3 são a base. Contagem: 8 + 2 + 3 + 4 + 2 + 1 + 6 + 5 + 3 + 1 = 35.
- **Perguntas ao dono para o relatório final:** decisões 2, 7, 8, 9 e 10, o texto do `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO`, a retenção do holerite de quem saiu, e se a fatia 4 já liga o `proximoPagamento` da ficha.
