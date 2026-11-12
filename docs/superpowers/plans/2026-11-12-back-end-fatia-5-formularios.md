# Back-end, fatia 5 (formulários) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar as 32 rotas de formulários (`contrato/fontes/50-fatia-5-formularios.yaml`): o RH monta (entrevista, admissão, avaliação, pesquisa, sugestão), publica para pessoas, setores ou link público e acompanha; o funcionário responde; o gestor vê só quem respondeu; o candidato responde pelo link; a avaliação tem avaliado; o anônimo não deixa rastro de quem respondeu.

**Architecture:** Mesma receita das fatias 3 e 4: regras puras em `Application/Formularios/Regras`, casos de uso, consultas e repositórios por porta, mensagens do contrato em um catálogo com teste contra o `openapi.yaml`, tarefas agendadas com chave que leva o instante (`PUBLICAR_FORMULARIO`, `ENCERRAR_FORMULARIO`, `REABRIR_FORMULARIO`, `LEMBRETE_FORMULARIO`, retenção de candidatos), e-mail pela fila (sigiloso no anônimo), auditoria **à mão** (nenhuma entidade de resposta com `[Auditado]`). A situação do formulário é uma função única que considera o prazo pelo `IRelogio` (o banco não fecha no prazo).

**Tech Stack:** .NET 10, EF Core 9 + Pomelo 9.0.0, MySQL 8.4 (Testcontainers), xUnit; PDFsharp não entra nesta fatia.

**Spec:** `contrato/fontes/50-fatia-5-formularios.yaml` (autoridade) e o levantamento `docs/superpowers/plans/2026-11-12-back-end-fatia-5-formularios-levantamento.md` (citado `L:A.n`, `L:B.n`, `L:D.n`, `L:E.n`, `L:G` T..., `L:H`); o plano argumenta a partir deles: **os dois viajam com o plano e o executor lê os dois**.

## Global Constraints

- O contrato vence o roadmap. Sem e-mail "formulário atribuído" nem "formulário publicado" (CONVENCOES:114); só o lembrete manual do RH, o manual do gestor e o automático de 7 dias.
- Todo instante gravado vai cortado ao segundo, em UTC na aplicação e -03:00 no banco (`ConversoresDeTempo.Brasil`); "hoje", `prazo` (até 23:59 -03:00) e `agendarPara` (00:00 -03:00) pelo dia de Brasília (`FusoBrasil`); datas de criação, resposta e cadastro **sempre** pelo `IRelogio` (os padrões `NOW()` do banco usam o relógio real).
- `ATIVO = FALSE` explícito ao criar (o padrão do banco é TRUE); `LEMBRETE_AUTOMATICO = TRUE` gravado pela API; `ANONIMO` da resposta sempre igual ao do formulário; `ID_PARTICIPACAO` com `Guid.NewGuid()` em minúsculas (nunca o `UUID()` do MySQL).
- **Anonimato:** `FormularioResposta`, `FormularioParticipacao` e `FormularioCandidato` **sem `[Auditado]`**; nenhum `auditor.Registrar` ao responder; nenhuma rota devolve ou ordena pelo id de resposta anônima; resposta sem autor, sem arquivo e com a data do dia da publicação da rodada; textos em ordem alfabética (sem diferenciar caixa e acento), nunca na de gravação; mínimos 5 (destinatários), 3 (por pergunta) e 3 (por avaliado); toda rota de escrita em anônimo publicado decide **antes** de olhar resposta.
- Escopo único: o gestor só a equipe (setor dele, ativos, sem ele, admissão começada) e nunca o conteúdo; a pessoa só o que é dela; o candidato só nas rotas públicas e nas do RH.
- Arquivos: PDF ou Word (`.doc`, `.docx`) até 5 MB, tipo conferido pelo conteúdo; servidos só como anexo por link temporário; o caminho nunca sai da API; receita "validar tudo → gravar o blob → transação curta → apagar o blob se falhar".
- Nada de valor de resposta, texto de pergunta de anônimo, e-mail ou nome de candidato em log, auditoria (`VALOR_ANTES/DEPOIS`) ou mensagem de erro; o token do link nunca entra em auditoria nem em log.
- Todo `ExecuteUpdate`/`ExecuteDelete` tem `// AUDITORIA-OK: motivo` até 3 linhas acima (`GuardaDeExecuteUpdateTests`). Colunas anuláveis do banco ficam anuláveis na entidade (`MapeamentoContraBancoTests`).
- Cada rota: o `operationId` em `ContratoDivergenciaTests.OperationIdsNoEscopo`, a linha em `contrato/STATUS.md`, o par de enum de consulta em `EnumsDeParametrosDoContrato`, o multipart em `FormulariosDoContrato`, `AfirmarConformeAoContratoAsync` nas respostas e a matriz de perfis verde.
- Commit e push por tarefa; **sem** `Co-Authored-By`, `Claude-Session` nem "Generated with Claude Code" em commit ou PR (conferir `git log origin/main..HEAD --format=%B | grep -ciE "Co-Authored-By|Claude|Generated with"` = 0 antes de cada push).

## Decisões do plano (Rulings; as perguntas H do levantamento, decididas sem esperar o dono, cada uma com o custo se errada)

1. **Link público (H1):** só limite por IP (30 consultas/min e 10 envios/h, configuráveis), sem captcha; retenção de candidato de **180 dias** configurável. *Custo se errado:* captcha muda o corpo da rota pública e exige nova fatia pequena.
2. **Recorrente (H2):** a reabertura **só reabre formulário encerrado** (com ou sem nome); se na data ainda está `PUBLICADO` não apaga nada e a tarefa tenta de novo na próxima data (próxima reabertura + intervalo). Formulário **por link não pode ser recorrente** (400 em `errors.recorrencia`). *Custo:* quem quiser reabrir no meio da rodada não consegue.
3. **Depois de agendar/publicar (H3):** sem rota nova (o contrato não tem). `salvarPublicacaoFormulario` em `AGENDADO` com `agendarPara` preenchido **muda a data** (reagenda a tarefa); vazio mantém a data guardada. Não há como mudar o prazo de um publicado nem devolver um agendado a rascunho (o RH exclui e refaz). *Custo:* pedido do dono vira rota nova no contrato.
4. **Avaliação da equipe anônima (H4):** **proibida** (400 em `errors.anonimo` ao criar/editar, como a Sugestão anônima), porque cada pessoa recebe uma só avaliação e o resultado nunca apareceria. A avaliação do gestor (`EQUIPE_AVALIA_GESTOR`) anônima é permitida. *Custo:* se o dono quiser a combinação, tira-se a validação e o resultado fica sempre oculto.
5. **Pares de avaliação com nome (H5):** calculados **na hora** (equipe e gestor de hoje), como o contrato diz para quem deve responder; só o anônimo congela (fotografia de pessoa × avaliado). *Custo:* quem troca de setor no meio pode ficar com avaliação fora da lista de hoje.
6. **Setores e quem está em admissão (H6):** o destinatário de um setor é a pessoa **ativa com admissão já começada** (a mesma definição da equipe); quem ainda não começou recebe só por `PESSOAS` (é o formulário de admissão).
7. **Data da resposta anônima (H7):** o **dia da publicação da rodada** (00:00 -03:00), igual para todos, nunca o dia da resposta. *Custo:* nenhum relatório "respostas por dia" do anônimo.
8. **Candidato (H8):** baixar currículo e arquivo de resposta grava evento `BAIXOU` (`FORMULARIOS`, sem o nome do arquivo); o aceite do aviso de privacidade é exigido (`true`) mas **não fica registrado** (não há coluna): pedir ao Kauan. *Custo:* sem prova do aceite.
9. **Arquivo Word:** `.docx` = ZIP com `word/document.xml`; `.doc` = assinatura OLE2 (`D0 CF 11 E0 A1 B1 1A E1`) (aceita também `.xls`/`.ppt` renomeados; só sai como anexo). *Custo:* um OLE2 não-Word passa.
10. **Textos de erro sem exemplo** (L:E.4.18): o executor escreve, registra `Ruling:` e o teste de mensagens tem lista de exceções.
11. **Mínimo de 5 do anônimo conta pessoas da fotografia** (setor sem gestor na avaliação do gestor fica fora); **`esperados` do anônimo = a fotografia inteira** (número fixo, mesmo para quem sair depois).
12. **Auditoria:** `CRIOU` (criar), `ALTEROU` (editar, perguntas, publicação guardada, encerrar, destinatários), `PUBLICOU` (publicar), `EXCLUIU`, `ENVIOU_EMAIL` (lembrete), `BAIXOU` (arquivo de candidato/resposta); o agendador age como Sistema; responder com nome grava **nada**; anônimo nunca.

## Review Focus

1. **Vazamento por contraste no anônimo:** o mesmo 409 com e sem resposta, o evento do lembrete **sempre** (com ou sem destinatário), `temRespostas`/`pendentes`/`respondidos`/`faltam` nulos antes de encerrar, o tempo da resposta do lembrete (a tarefa enfileira, a rota só marca), nenhuma linha de auditoria ao responder (T4, T5, T8, T13, T14, T16, T17).
2. **Prazo e agendador atrasado:** resposta depois do prazo com a tarefa de encerrar parada = 422 e nada gravado; resultado do anônimo só abre depois de encerrado de verdade; reabrir e publicar rodando duas vezes ou sobre estado que já mudou (T2, T8, T12, T15, T18).
3. **Link público:** token inexistente/rascunho/fora do padrão = o mesmo 404; 429 antes de tudo; currículo grande, tipo errado e `.zip` renomeado; mesmo e-mail várias vezes (e em paralelo); nada pessoal em log ou auditoria; matriz de perfis não gasta o balde do IP (T19, T20, T21, T22).
4. **Duas abas e duplo clique:** responder duas vezes com nome (o banco aceita), publicar, encerrar contra responder, reordenar perguntas (1062), dois envios de Sugestão no mesmo segundo (T5, T8, T12, T13, T20).
5. **Quem vê o quê:** o gestor nunca vê conteúdo (404 nas avaliações e na Sugestão); o funcionário só o dele; candidato de outro formulário = 404; arquivo de resposta de anônimo = 404; a lista do RH nunca devolve caminho de arquivo (T14, T17, T21).

---

## Task 1: Mapeamento das sete tabelas (5a)

**Files:** Create `Domain/Formularios/{Formulario,FormularioPergunta,FormularioAlternativa,FormularioResposta,FormularioCandidato,FormularioDestinatario,FormularioParticipacao}.cs`, enums (`TipoDeFormulario`, `SentidoDaAvaliacao`, `AlvoDoFormulario`, `TipoDePergunta`, `UnidadeDeRecorrencia`), configurações em `Infrastructure/Persistencia/Configuracoes/*Configuracao.cs`, `DbSet`s no `AppDbContext`; Modify `tests/.../Persistencia/tabelas-sem-mapeamento.txt` (tirar as sete tabelas). Test `Persistencia/MapeamentoDosFormulariosTests.cs`.

**Interfaces:** Produces as entidades com fábricas por "dono" da resposta (`FormularioResposta.DeFuncionario/DeCandidato/Anonima`), `Formulario.Criar` (rascunho, `Ativo=false`, `LembreteAutomatico=true`), `FormularioParticipacao.Criar(Guid v4)`; colunas geradas (`CHAVE_DESTINO`, `CHAVE_AVALIADO`) mapeadas fora do INSERT; `ID_PARTICIPACAO` sem geração de valor.

- [ ] **Step 1: Testes (RED, compile-RED aceito):** `MapeamentoContraBancoTests`/`ConformidadeDoModeloTests` verdes com as tabelas fora do arquivo de exceções; sonda que grava formulário (`Ativo=false` relido), resposta anônima (sem autor, `ANONIMO` igual ao do formulário), participação v4, candidato com data do relógio falso, destinatário por pessoa e por setor, e relê tudo.
- [ ] **Step 2 a 4:** RED, implementar (colunas anuláveis do banco anuláveis; datas do grupo com `ConversoresDeTempo.Brasil`; nenhuma das três entidades de resposta com `[Auditado]`), GREEN. **Step 5: Commit.** `feat: entidades e mapeamento dos formularios (fatia 5)`.

## Task 2: Regras puras do formulário (5a)

**Files:** Create `Application/Formularios/Regras/{SituacaoDoFormulario,MatrizDeEdicao,ValidacaoDoFormulario,ValidacaoDaPergunta,ValidacaoDaPublicacao,InstantesDoFormulario,TokenDoLink,MinutosEstimados,QuemRespondeResumo}.cs`. Test `Application.Tests/Formularios/*`.

**Interfaces:** Produces `SituacaoDoFormulario.De(ativo, publicacao, encerramento, prazo, agora)` (**uma função só**: `ATIVO` ligado com prazo vencido = `ENCERRADO`; `RASCUNHO` = inativo sem datas; `AGENDADO` = inativo com `publicacao`), `MatrizDeEdicao.Decidir(situacao, anonimo, temResposta, corpo)` (D.6 do levantamento: erro 409/422 ou o que muda), validações com todos os erros juntos (`errors` por campo: `titulo`, `tipo`, `anonimo`, `sentidoAvaliacao`, `alternativas`, `alvo`, `setorIds`, `funcionarioIds`, `prazo`, `agendarPara`, `recorrencia`), `InstantesDoFormulario` (agendar = `InicioDoDiaEmUtc(dia)`; prazo = fim do dia -03:00; encerramento nominal = `InicioDoDiaEmUtc(prazo+1)`), `TokenDoLink.Novo()` (32 bytes base64url = 43 caracteres) e `Valido(texto)` (`^[A-Za-z0-9_-]{22,64}$`), `MinutosEstimados`, `QuemRespondeResumo.Texto(...)` (os seis textos do contrato; plural/singular).

- [ ] **Step 1: Testes (RED):** as quatro situações e o prazo vencido (23:59 -03:00 do dia, meia-noite UTC e 21:00 locais); cada linha da matriz de edição; Sugestão anônima e avaliação do gestor anônima = 400 em `anonimo`; recorrência em formulário por link = 400 em `recorrencia`; `LINK` com listas não vazias, `SETORES` sem `setorIds`; `prazo` < hoje ou < `agendarPara`; token com 43 caracteres sem `+/=`; as frases de `quemRespondeResumo`.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, mutação das regras. **Step 5: Commit.** `feat: regras puras dos formularios (situacao com prazo, matriz de edicao, validacoes e instantes)`.

## Task 3: Erros da fatia 5 (5a)

**Files:** Create `Application/Formularios/Mensagens/{MensagensDosFormularios,RegrasDoBancoDosFormularios}.cs`, `Infrastructure/Formularios/TradutorDeRecusaDosFormularios.cs`; Modify `Api/Configuracao/TextosDeErro.cs` (título "Confirmação necessária."). Test `Contrato/MensagensDosFormulariosTests.cs`, `Formularios/TradutorDeRecusaDosFormulariosTests.cs`.

**Interfaces:** Produces uma propriedade por código de L:B.1 (texto exato do contrato; os "sem exemplo" escritos pelo executor), a fábrica `ConfirmacaoNecessaria(pendentes: int?)` (`pendentes: null` **sai no JSON**), o tradutor (45000 pela mensagem; 1062 pelo nome da chave; 1451 da FK do anônimo → `ANONIMATO_FIXO_APOS_PUBLICAR`; CHECKs → `regraDoBanco` fixo).

- [ ] **Step 1: Testes (RED):** cada mensagem confere com o `openapi.yaml` montado (lista de exceções para os textos sem exemplo); o 409 de confirmação tem título próprio e `pendentes` presente também como `null`; o tradutor mapeia cada recusa de L:B.2. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: erros dos formularios (catalogo contra o contrato e tradutor de recusa do banco)`.

## Task 4: Detalhes do formulário (#2, #3, #4) (5a)

**Files:** Create `Application/Formularios/Construtor/{CasosDeUsoDosDetalhes,FormulariosDtos,IFormulariosRepositorio,IConsultaDeFormularios}.cs`, `Infrastructure/Formularios/{FormulariosRepositorio,ConsultaDeFormularios}.cs`, `Api/Formularios/FormulariosRhEndpoints.cs`; Modify `Program.cs`, `DependencyInjection.cs`. Test `Formularios/DetalhesDoFormularioTests.cs`.

**Interfaces:** Produces `criarFormulario` (201, rascunho, autor, data do relógio, `ATIVO` falso, `lembreteAutomatico` true), `obterFormularioRh` (`temRespostas` **nulo no anônimo em qualquer situação**; `tokenDoLink` só depois de publicar/agendar por link; `publicacao` completa), `editarFormulario` pela matriz (decide o anônimo publicado **antes** de olhar respostas; troca de `anonimo` com resposta = 409 antes do 1451; formulário por link não sai de `ENTREVISTA`).

- [ ] **Step 1: Testes (RED):** rascunho relido do banco com `ATIVO=0` e lembrete ligado; Sugestão anônima e avaliação do gestor sobre a equipe anônima = 400; matriz de edição por situação/anônimo/resposta (inclusive **o mesmo 409** com e sem resposta no anônimo publicado); passar a anônimo com pergunta `ARQUIVO` = 422 e com link = 422; mudar `tipo` anula `sentidoAvaliacao`; `temRespostas` nulo em rascunho anônimo; evento `CRIOU`/`ALTEROU` sem o conteúdo das perguntas de anônimo; conformidade ao contrato; matriz de perfis.
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: detalhes do formulario (criar, obter e editar pela matriz de situacao e anonimato)`.

## Task 5: Perguntas e alternativas (#6, #7, #8) (5a)

**Files:** Create `Application/Formularios/Construtor/CasosDeUsoDasPerguntas.cs`; Modify repositório e endpoints. Test `Formularios/PerguntasDoFormularioTests.cs`.

**Interfaces:** Produces `adicionarPerguntaFormulario` (sem `ordem` vai para o fim; com `ordem` as seguintes descem; com nome publicado aceita), `editarPerguntaFormulario` (alternativa com `id` mantém, sem `id` é nova, a ausente é apagada; respondida: só `obrigatoria` e `ordem` — 422 `PERGUNTA_JA_RESPONDIDA` com `regraDoBanco`; anônimo publicado: **sempre 409 `ANONIMO_PERGUNTAS_FIXAS`**), `excluirPerguntaFormulario` (as seguintes sobem). **Reordenar em dois passos** (`ORDEM + 1000` e depois o valor final) dentro da transação; a corrida de duas abas na mesma posição vira nova tentativa curta e depois 409, nunca 500.

- [ ] **Step 1: Testes (RED):** mover a pergunta 3 para a posição 1 sem 1062 e sem buraco; duas abas acrescentando no mesmo lugar; alternativas 2 a 10 (400 em `errors.alternativas`); `ARQUIVO` em anônimo = 422 `ANONIMO_SEM_ARQUIVO`; respondida (editar texto/tipo/alternativas, apagar) = 422, só `obrigatoria`/`ordem` passam; alternativa respondida não muda; anônimo publicado com e sem resposta = o mesmo 409; encerrado 422; evento `ALTEROU` sem o texto das perguntas de anônimo.
- [ ] **Step 2 a 4, 5:** RED, implementar, GREEN, **Commit** `feat: perguntas e alternativas do formulario (ordem em dois passos, respondida e anonimo publicado)`.

## Task 6: Quem deve responder (porta única) (5a)

**Files:** Create `Application/Formularios/Destinatarios/{IDestinatariosDoFormulario,QuemDeveResponder,ParesDeAvaliacao}.cs`, `Infrastructure/Formularios/DestinatariosDoFormulario.cs`. Test `Formularios/QuemDeveResponderTests.cs` (+ unidade dos pares).

**Interfaces:** Produces `ObterAsync(formulario, ct)` → pares `(pessoaId, avaliadoId?)` conforme D.8 do levantamento (pessoas; setores **na hora** com ativos de admissão começada; fotografia do anônimo; `GESTOR_AVALIA_EQUIPE` = cada gestor escolhido × cada pessoa da equipe dele menos ele; `EQUIPE_AVALIA_GESTOR` = as pessoas/setores escolhidos × o gestor do setor de cada uma, sem gestor = fora), `Esperados`, `QuemFalta(participacoes)`, a fotografia (`FotografarAsync`, uma linha por pessoa **e** mantendo as de setor).

- [ ] **Step 1: Testes (RED):** setor sem gestor, gestor dentro do setor, pessoa inativa, em admissão (fora de `SETORES`, dentro de `PESSOAS`), quem entra no setor depois, número de avaliações ("3 gestores (24 avaliações)"), fotografia do anônimo congelada.
- [ ] **Step 2 a 5:** **Commit** `feat: quem deve responder o formulario (pessoas, setores, fotografia e pares de avaliacao)`.

## Task 7: Publicação guardada e verificação (#10, #9) (5a)

**Files:** Create `Application/Formularios/Construtor/{CasosDeUsoDaPublicacaoGuardada,VerificacaoDoFormulario}.cs`; endpoints. Test `Formularios/VerificacaoEPublicacaoGuardadaTests.cs`.

**Interfaces:** Produces `salvarPublicacaoFormulario` (só guarda; continua rascunho/agendado; **não** roda verificação nem o mínimo; `agendarPara` de rascunho aceito e **não guardado** (volta `null`); em `AGENDADO` com `agendarPara` muda a data e reagenda a tarefa (Decisão 3); pessoa inativa = 422 `FUNCIONARIO_INATIVO` e nada entra; trocar o alvo de agendado por link anula o token no mesmo UPDATE; `PUBLICADO`/`ENCERRADO` = 422), `verificarFormulario` (os sete itens na ordem de D.7 do levantamento; `podePublicar`; lista de palavras do dado sensível só `AVISO`).

- [ ] **Step 1: Testes (RED):** os dois exemplos de `yaml:716-736`; alvo vs listas (400); link em anônimo = 422 `ANONIMO_SEM_LINK`; link em tipo diferente de entrevista = 422 `LINK_SO_PARA_ENTREVISTA`; `agendarPara` no passado = 400; recorrência com link = 400; **sem** e-mail nem token ao só guardar.
- [ ] **Step 2 a 5:** **Commit** `feat: publicacao guardada e verificacao do formulario`.

## Task 8: Publicar, agendar e encerrar (#11, #12) (5a)

**Files:** Create `Application/Formularios/Construtor/{CasosDeUsoDePublicar,CasosDeUsoDeEncerrar}.cs`, `Infrastructure/Formularios/{AgendamentoDoFormulario,PublicarFormularioTarefa,EncerrarFormularioTarefa}.cs`; Modify DI (tarefas). Test `Formularios/PublicarEEncerrarTests.cs`.

**Interfaces:** Produces `publicarFormulario` (ordem: mínimo de 5 do anônimo → verificação com o público do corpo → publica agora (`ATIVO` + `publicadoEmUtc`) ou agenda (tarefa no início do dia -03:00); `LINK` gera o token mesmo agendando; fotografia do anônimo; **sem e-mail**), `encerrarFormulario` (UPDATE condicional; `ATIVO` falso + `DATA_ENCERRAMENTO` no mesmo UPDATE; confirmação **sempre** no anônimo, com `pendentes` só no com nome; cancela as próximas reaberturas; sem "reabrir"), tarefas com **chave que leva o instante** que **relêem** o formulário antes de agir (publicar uma vez; anônimo abaixo de 5 não publica e continua `AGENDADO`; encerrar grava o encerramento nominal do prazo), `RecuperarAsync` de cada uma. Reusa o padrão de `AgendamentoDoAviso` (inclusive `ReativarAsync` quando a data volta a um instante já usado).

- [ ] **Step 1: Testes (RED):** agendado publica uma vez (tarefa rodada duas vezes e depois da recuperação); anônimo agendado que caiu para 4 não publica; publicar anônimo para 4 = 422 `FORMULARIO_ANONIMO_PUBLICO_PEQUENO` **antes** da verificação; verificação com bloqueio = 422; `FORMULARIO_JA_PUBLICADO`; duas abas publicando e duas encerrando (um 200, um 4xx); encerrar com pendentes pede `CONFIRMACAO_NECESSARIA` (e no anônimo com `pendentes: null`, mesmo com todos respondidos); encerramento nominal `2026-10-01T03:00:00Z` para prazo `2026-09-30`; encerrar × responder ao mesmo tempo; eventos `PUBLICOU` e `ALTEROU` (encerrou) sem contagem no anônimo.
- [ ] **Step 2 a 5:** **Commit** `feat: publicar, agendar e encerrar formulario (tarefas com chave que leva o instante)`.

## Task 9: Lista do RH e exclusão (#1, #5) (5a)

**Files:** Modify `ConsultaDeFormularios`, casos de uso, endpoints. Test `Formularios/ListaEExclusaoTests.cs`.

**Interfaces:** Produces `listarFormulariosRh` (busca por título, `tipo`, `situacao`, `ordenarPor` `criadoEm|titulo|prazo` + `direcao`, `contagemPorTipo` que respeita busca e situação e **ignora** tipo; `respondidos`/`esperados`/taxa por alvo e anonimato: link e Sugestão `null`, anônimo aberto só `esperados`; taxa inteira com `MidpointRounding.AwayFromZero`), `excluirFormulario` (`confirmar`; 409 `CONFIRMACAO_NECESSARIA`; **exclusão física** na ordem respostas → participações → destinatários → candidatos → alternativas → perguntas → formulário em uma transação, depois apaga os arquivos; cancela tarefas e e-mails pendentes; evento `EXCLUIU` com título e a quantidade de respostas, **sem a quantidade** no anônimo aberto).

- [ ] **Step 1: Testes (RED):** os quatro `ordenarPor` e a ordem padrão; contagem por tipo; `quemRespondeResumo` pelo alvo; 5 de 8 = 63%; anônimo aberto sem contagem; excluir com respostas, candidatos, arquivos e tarefas (nada sobra no banco nem no armazenamento; nenhuma tarefa roda depois); evento com e sem contagem. **Step 2 a 5:** **Commit** `feat: lista do RH e exclusao fisica do formulario`.

## Task 10: Destinatários e a caixa de admissão do cadastro (#13) (5a)

**Files:** Create `Application/Formularios/Construtor/CasosDeUsoDosDestinatarios.cs`, `Infrastructure/Formularios/EnvioDoFormularioDeAdmissao.cs`; Modify DI (registrar **depois** da implementação padrão `CaixasIndisponiveis`), endpoints. Test `Formularios/DestinatariosTests.cs`; atualizar `CadastroDeFuncionariosTests`, `CriarFuncionarioTests`, `ReadmissaoDeFuncionariosTests`, `ConfirmarImportacaoTests` (os 400 da caixa indisponível viram caminho feliz e os erros 400/422) e `ContadoresDoMenuTests` (T11).

**Interfaces:** Produces `adicionarDestinatariosFormulario` (só `alvo = PESSOAS`; `adicionados`/`jaEstavam`; inativo = 422 e **ninguém** entra; anônimo publicado = 409 `ANONIMO_SEM_NOVOS_DESTINATARIOS`; avaliação = 422; `FORMULARIO_NAO_E_POR_PESSOAS`; `FORMULARIO_ENCERRADO`; sem e-mail), `IEnvioDoFormularioDeAdmissao` real (`ConferirAsync`: inexistente/não é de admissão/rascunho-agendado = 400 em `formularioAdmissaoId`, encerrado = 422; `AdicionarDestinatariosAsync` na mesma transação do cadastro, da readmissão e da importação).

- [ ] **Step 1: Testes (RED):** a matriz de L:H.2 (publicado por pessoas inclui; rascunho 400; por setores 422; encerrado 422; inexistente 400; nada cadastrado nos erros); `incluidosNoFormularioDeAdmissao` conta certo; incluir inativo + dois ativos = ninguém entra. **Step 2 a 5:** **Commit** `feat: destinatarios do formulario e a caixa de formulario de admissao do cadastro`.

- [ ] **Mutação do incremento 5a (T2 a T10):** gerar `gerar-spec-f5-5a.mjs` (padrão de `gerar-spec-f4-4b.mjs`), `node mutar-em-paralelo.mjs <spec> <saida> 10` em segundo plano com Monitor, um teste novo para cada sobrevivente real, equivalentes no ledger; **Commit** `test: mutantes do incremento 5a`.

## Task 11: Meus formulários, abrir e a bolinha do menu (#27, #28) (5b)

**Files:** Create `Application/Formularios/Responder/{CasosDeUsoDosMeusFormularios,IConsultaDeMeusFormularios}.cs`, `Infrastructure/Formularios/{ConsultaDeMeusFormularios,FonteDeContadorDeFormularios}.cs`, `Api/Formularios/FormulariosEuEndpoints.cs`. Test `Formularios/MeusFormulariosTests.cs`; Modify `ContadoresDoMenuTests`.

**Interfaces:** Produces `listarMeusFormularios` (`situacao` ausente = `PARA_RESPONDER` + `DISPONIVEL`; `contagens` respeitam busca e tipo e ignoram situação; entram os publicados em que a pessoa deve responder **e todos os que já respondeu**; por link nunca; `ADMISSAO`/`AVALIACAO` = para responder, o resto disponível; Sugestão sempre `DISPONIVEL`; `minutosEstimados`), `obterFormularioParaResponder` (404 se não deve responder; agendado/encerrado = 422; avaliação traz `avaliados` com `respondido`), `FonteDeContadorDeFormularios` (`contagens.paraResponder` = o contador `FormulariosPendentes`).

- [ ] **Step 1: Testes (RED):** o contador do menu = `contagens.paraResponder`; respondido some de "Todos"; prazo vencido com tarefa parada = 422; 404 para quem não deve responder; conformidade; perfis (`/api/eu`). **Step 2 a 5:** **Commit** `feat: meus formularios, abrir para responder e a bolinha do menu`.

## Task 12: Responder com nome e minhas respostas (#29 com nome, #30) (5b)

**Files:** Create `Application/Formularios/Responder/{CasosDeUsoDeResponder,ValidacaoDaResposta,IFormulariosRespostasRepositorio}.cs`, `Application/Arquivos/{TipoWord,ValidacaoDeArquivoDeFormulario}.cs`, `Infrastructure/Formularios/{FormulariosRespostasRepositorio,FonteDeReferenciasDosFormularios}.cs`; Modify `Api/Configuracao/FormulariosDoContrato.cs` (multipart `RespostaFormularioEnvio`), `IdentificadorDeTipoDeArquivo` (Word). Test `Formularios/ResponderComNomeTests.cs`.

**Interfaces:** Produces `responderFormulario` com nome (multipart `respostas` JSON + `arquivos[]`; ordem: 404 → situação (agendado/encerrado/prazo) → 400 de campo (um só tipo por item, alternativa da própria pergunta, escala 1 a 5, texto ≤200/≤4000, `arquivoIndice` válido, arquivo não citado, obrigatória faltando, ao menos uma resposta) → 422 `FUNCIONARIO_INATIVO` → 409 `JA_RESPONDIDO` (uma resposta por pessoa e por avaliado; **trava** + conferência na mesma transação; Sugestão aceita várias) → grava tudo ou nada com **o mesmo instante** em todas as linhas (cortado ao segundo; na Sugestão, se já houver envio naquele segundo, o seguinte); recibo `participacaoId` `null`, `restantes`), `obterMinhasRespostas` (só com nome; 404 se não respondeu; arquivo só com o nome; 422 `FORMULARIO_ANONIMO`/`FORMULARIO_SUGESTAO`), validação "PDF ou Word até 5 MB" por conteúdo, `FonteDeReferenciasDosFormularios` (prefixo `formularios/`, lê `CAMINHO_ARQUIVO` e `CAMINHO_CURRICULO`).

- [ ] **Step 1: Testes (RED):** cada erro de campo; duas abas = um 201 e um 409 sem linha duplicada; arquivo de 6 MB, `.png`, `.zip` renomeado `.docx` = 400 em `arquivos` e nenhum blob; recusa do banco depois do upload apaga o blob; encerrar × responder = 422 com `regraDoBanco`; fora do prazo com a tarefa atrasada = 422; Sugestão duas vezes (inclusive no mesmo segundo) = duas linhas; resposta de pergunta `ARQUIVO` grava o nome em `RESPOSTA`; múltipla escolha grava o texto da alternativa; opcional em branco não gera linha. **Step 2 a 5:** **Commit** `feat: responder formulario com nome e minhas respostas`.

## Task 13: Responder anônimo (#29, o resto) (5b)

**Files:** Modify `CasosDeUsoDeResponder`, repositório. Test `Formularios/ResponderAnonimoTests.cs`.

**Interfaces:** Produces o caminho anônimo de `responderFormulario`: resposta sem funcionário, sem candidato, sem arquivo, `ANONIMO = TRUE`, `DATA_RESPOSTA` = dia da publicação da rodada (Decisão 7), participação `Guid.NewGuid()` minúsculo devolvida no recibo, 409 pela chave única (inclusive em corrida), pessoa inativa barrada pela aplicação (o trigger não confere), **nenhum evento de auditoria**, nenhuma ligação entre participação e resposta.

- [ ] **Step 1: Testes (RED):** depois de responder **nenhuma linha nova** em `AUDITORIA_EVENTOS`/`AUDITORIA_MUDANCAS`; a resposta sem autor e com a data do dia da publicação; a participação sem data e com UUID v4 minúsculo; duas abas = um 201 e um 409; inativo = 422 `FUNCIONARIO_INATIVO`; avaliação anônima uma participação por (pessoa, avaliado); o mesmo corpo de 422/409 para quem já respondeu e para quem não (sem vazamento por contraste).
- [ ] **Step 2 a 5:** **Commit** `feat: responder formulario anonimo (sem autor, sem hora, sem auditoria)`.

## Task 14: Quem respondeu (RH) e ver respostas (#14, #16, #18) (5b)

**Files:** Create `Application/Formularios/Acompanhar/{CasosDeUsoDeAcompanhar,IConsultaDeAcompanhamento}.cs`, `Infrastructure/Formularios/ConsultaDeAcompanhamento.cs`. Test `Formularios/QuemRespondeuTests.cs`.

**Interfaces:** Produces `listarRespondentesFormulario` (calculado na hora com `QuemDeveResponder`; `resumo` não muda com filtros; ordem nome; anônimo: `itens: []`, `total: 0`, filtros ignorados, contagem só depois de `ENCERRADO`; avaliação uma linha por avaliação; Sugestão uma linha por envio, o mais recente primeiro; 422 `FORMULARIO_POR_LINK`), `obterRespostasDeFuncionario` (`avaliadoId`/`respondidoEmUtc` obrigatórios ou proibidos conforme o tipo; 404 se não respondeu; 422 `FORMULARIO_ANONIMO`; a lista sai das **perguntas**, não das linhas), `baixarArquivoDeResposta` (link temporário como anexo; anônimo = 404; evento `BAIXOU` sem o nome do arquivo).

- [ ] **Step 1: Testes (RED):** anônimo aberto e encerrado; Sugestão por envio; `podeVerRespostas`; ver respostas de outro formulário = 404; pergunta opcional em branco vem com tudo vazio; o gestor e o funcionário = 403 (matriz); arquivo sem caminho na resposta. **Step 2 a 5:** **Commit** `feat: quem respondeu o formulario e ver respostas pelo RH`.

## Task 15: Resultado e exportação (#20, #22) (5b)

**Files:** Create `Application/Formularios/Acompanhar/{ResultadoDoFormulario,ExportacaoDoFormulario}.cs` (+ `Regras/AgregacaoDeResultado.cs`). Test `Formularios/ResultadoEExportacaoTests.cs` (+ unidade da agregação).

**Interfaces:** Produces `obterResultadoFormulario` (por tipo: múltipla escolha com **todas** as alternativas e percentual de 1 casa; escala com média e os cinco valores; textos em ordem alfabética sem caixa/acento; arquivo só `totalRespostas`; anônimo: só `ENCERRADO` de verdade (função de situação), mínimo 3 por pergunta com `perguntasOcultas`; avaliado anônimo < 3 = 422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES`; com nome: sem mínimo), `exportarRespostasFormulario` (`text/csv`, `;`, BOM, cabeçalho, `CsvSeguro`, datas -03:00 `2026-10-02 11:10`, `formulario-{id}-respostas.csv`; com nome/link uma linha por resposta; anônimo só o agregado; 409 antes de encerrar; 422 sem nenhuma pergunta com 3).

- [ ] **Step 1: Testes (RED):** os exemplos de `yaml:1513-1621`; textos Z, A, M saem A, M, Z; pergunta com 2 respostas some e conta; resposta começando por `=`, `+`, `-`, `@` neutralizada; com o prazo vencido e a tarefa de encerrar parada o formulário já conta como `ENCERRADO` e o resultado do anônimo abre (prova a função única de situação); 409 `RESULTADO_ANONIMO_INDISPONIVEL`. **Step 2 a 5:** **Commit** `feat: resultado e exportacao do formulario (minimo de 3 e csv seguro)`.

## Task 16: Lembretes (#23) e o automático (5b)

**Files:** Create `Application/Formularios/Lembretes/{CasosDeUsoDeLembrar,LembreteDeFormularioTarefa}.cs`, `Infrastructure/Formularios/LembreteDeFormularioTarefa.cs`, `Infrastructure/Emails/FormularioLembreteRenderizador.cs`. Test `Formularios/LembretesDoFormularioTests.cs`.

**Interfaces:** Produces `lembrarQuemFaltaFormulario` (só `PUBLICADO`; não vale por link nem Sugestão; **200**; com nome: e-mail por pessoa com chave `FORMULARIO_LEMBRETE:form=:func=:dia=` (a repetida vai para `ignorados`), `enviados`/`ignorados`; anônimo: só lista vazia (senão 422 `FORMULARIO_ANONIMO`), a rota grava a marca `LEMBRETE_FORMULARIO:form=:dia=:alcance=TODOS` com `RegistrarComoConcluidaAsync` e **uma tarefa** que resolve os destinatários e enfileira e-mails **sigilosos** (linha apagada ao enviar), resposta **sempre igual** (`enviados`/`ignorados` `null`), limite por formulário/dia, **evento sempre** e sem número nem destinatário), `LembreteDeFormularioTarefa` (automático 7 dias antes do prazo, ligado por padrão; não agenda se o prazo está a 7 dias ou menos; uma por rodada; relê antes de agir), renderizador `FORMULARIO_LEMBRETE`.

- [ ] **Step 1: Testes (RED):** lembrete com nome duas vezes no dia (RH e gestor) = a segunda em `ignorados`; anônimo com muitos, um, nenhum e repetido no dia = a **mesma resposta**; linhas sigilosas apagadas depois do envio e sem `ENVIOU_EMAIL`; evento sem número; prazo a 8 dias sai no dia certo uma vez, a 7 dias não; caixa desligada; por link. **Step 2 a 5:** **Commit** `feat: lembretes do formulario (manual, automatico e sigiloso no anonimo)`.

## Task 17: Gestor (#24, #25, #26) (5b)

**Files:** Create `Application/Formularios/Equipe/*`, `Infrastructure/Formularios/ConsultaDeFormulariosDaEquipe.cs`, `Api/Formularios/FormulariosGestorEndpoints.cs`. Test `Formularios/FormulariosDaEquipeTests.cs`.

**Interfaces:** Produces `listarFormulariosDaEquipe` (só `PUBLICADO` com gente do setor para responder; contagens só da equipe; **fora**: link, Sugestão, as duas avaliações, encerrado; sem paginação; ordem prazo e título), `listarRespondentesDaEquipe` (só quem, nunca conteúdo; outro setor/Sugestão/avaliações = 404; `observacao` = férias aprovadas que cobrem hoje via `IFeriasDoPonto`; anônimo `itens: []` e contagem só depois de encerrado), `lembrarQuemFaltaNaEquipe` (alcance do setor; 404 fora da equipe; mesmas regras de T16). Gestor sem setor = lista vazia.

- [ ] **Step 1: Testes (RED):** outro setor, Sugestão, avaliação do gestor e da equipe = 404; gestor sem setor; nenhuma rota do gestor devolve conteúdo; anônimo aberto só `esperados`; `observacao` "de férias até 17/10"; perfis (RH e funcionário = 403). **Step 2 a 5:** **Commit** `feat: formularios da equipe para o gestor (so quem respondeu, nunca o conteudo)`.

## Task 18: Recorrência (5b)

**Files:** Create `Infrastructure/Formularios/ReabrirFormularioTarefa.cs`; Modify `CasosDeUsoDePublicar` (agenda a primeira reabertura). Test `Formularios/RecorrenciaTests.cs`.

**Interfaces:** Produces `ReabrirFormularioTarefa` (chave com o instante; relê; **só reabre formulário `ENCERRADO`** (Decisão 2); se ainda `PUBLICADO` avança a próxima data sem apagar; reabre: apaga respostas, participações e **arquivos** da rodada anterior, `ATIVO` + `DATA_ENCERRAMENTO` nulo juntos, nova `publicadoEmUtc`, prazo avança o mesmo número de dias, recalcula `proximaReaberturaEmUtc` (até `recorrencia.ate`), agenda o encerramento e o lembrete da rodada; anônimo reabre só com 5 ou mais ativos da fotografia, senão fica `ENCERRADO` e a recorrência é cancelada; evento do Sistema sem contagem no anônimo), `encerrarFormulario` à mão cancela as próximas reaberturas.

- [ ] **Step 1: Testes (RED):** com nome e anônimo reabrindo (respostas, participações e arquivos somem; a pessoa volta a "para responder"; a pergunta volta a ser editável); aberto na data não reabre e tenta na próxima; anônimo com menos de 5 fica encerrado sem recorrência; encerrar à mão cancela (a tarefa antiga não reabre); a tarefa rodada duas vezes reabre uma vez; por link 400 ao agendar. **Step 2 a 5:** **Commit** `feat: formulario recorrente (reabertura de rodada que apaga as respostas anteriores)`.

- [ ] **Mutação do incremento 5b (T11 a T18):** igual à 5a; **Commit** `test: mutantes do incremento 5b`.

## Task 19: Página pública do candidato (#31) (5c)

**Files:** Create `Application/Formularios/Publico/{CasosDeUsoDaPaginaPublica,OpcoesDoFormularioPublico}.cs`, `Api/Formularios/FormulariosPublicoEndpoints.cs`; Modify `Api/Configuracao/LimiteDeTentativas.cs` (políticas `formulario-consulta` 30/min e `formulario-envio` 10/h, configuráveis), `Infra/ApiFactory.cs` (limites altos nos testes). Test `Formularios/PaginaPublicaTests.cs`.

**Interfaces:** Produces `obterFormularioPublico` em `GrupoPublico` (429 antes de tudo com `Retry-After` e o texto do contrato; token inexistente, de rascunho e **fora do padrão** = o mesmo 404 (nunca 400); agendado 422 `FORMULARIO_AINDA_NAO_ABERTO`; encerrado ou prazo vencido 422 `FORMULARIO_ENCERRADO`; sem empresa 422 `EMPRESA_NAO_CONFIGURADA` com o texto desta fatia; `empresaNome`, perguntas sem `id` do formulário, `avisoDePrivacidade` com `retencaoDias`), sem registrar o caminho (token) em log.

- [ ] **Step 1: Testes (RED):** o mesmo 404 para inexistente/rascunho/`../x`/`2147483647`; 31ª consulta no minuto = 429 e outro IP passa; agendado, encerrado, prazo vencido com tarefa parada, sem empresa; a matriz de perfis nunca 401/403 nas públicas. **Step 2 a 5:** **Commit** `feat: pagina publica do formulario (limite por IP, mesmo 404 e estados)`.

## Task 20: Candidatura (#32) (5c)

**Files:** Create `Application/Formularios/Publico/CasosDeUsoDaCandidatura.cs`, `Infrastructure/Formularios/CandidatosRepositorio.cs`; Modify `FormulariosDoContrato.cs` (multipart `CandidatoEnvio`). Test `Formularios/CandidaturaTests.cs`.

**Interfaces:** Produces `responderFormularioPublico` (limite 10/h por IP; validação de `nome`, `email` (normalizado: aparado e minúsculas), `telefone`, `aceitouAvisoDePrivacidade = true`, `respostas`, currículo e arquivos PDF ou Word até 5 MB por conteúdo, tudo ou nada com a receita do blob; 409 `JA_RESPONDIDO` pela chave única do e-mail (inclusive em corrida); data pelo `IRelogio`; resposta sem funcionário e com `ID_FORMULARIO_CANDIDATO`; **auditoria sem nenhum dado pessoal** (ator Sistema, só "candidatura recebida" sem nome nem e-mail; ou nenhuma)).

- [ ] **Step 1: Testes (RED):** currículo de 6 MB, `.png` e `.zip` renomeado = 400 em `curriculo` e nenhum blob; arquivo de pergunta ruim = 400 em `arquivos`; o mesmo e-mail com caixa diferente e em paralelo = um 201 e um 409; 11º envio na hora = 429; prazo vencido e agendado = 422; nada de nome/e-mail em log, auditoria ou erro; o blob apagado se o banco recusar. **Step 2 a 5:** **Commit** `feat: candidatura pelo link publico (arquivos validados, e-mail unico, sem dado pessoal em auditoria)`.

## Task 21: Candidatos para o RH (#15, #17, #19) (5c)

**Files:** Modify `ConsultaDeAcompanhamento`, `CasosDeUsoDeAcompanhar`, endpoints. Test `Formularios/CandidatosDoRhTests.cs`.

**Interfaces:** Produces `listarCandidatosFormulario` (só `alvo = LINK`; mais recentes primeiro; busca em nome e e-mail; 422 `FORMULARIO_SEM_LINK`), `obterRespostasDeCandidato` (candidato de outro formulário = 404), `baixarCurriculoDeCandidato` (anexo com nome gerado `curriculo-<nome>.<ext>`; sem currículo = 404; evento `BAIXOU` sem o nome), e `exportarRespostasFormulario` para formulário por link (uma linha por candidato).

- [ ] **Step 1: Testes (RED):** candidato de outro formulário = 404; sem currículo = 404; o gestor e o funcionário = 403; dado pessoal só no RH; o CSV do link com `=` neutralizado. **Step 2 a 5:** **Commit** `feat: candidatos do formulario para o RH (lista, respostas e curriculo)`.

## Task 22: Retenção de candidatos (5c)

**Files:** Create `Infrastructure/Formularios/ApagarCandidatosVencidosTarefa.cs` (diária, recuperável). Test `Formularios/RetencaoDeCandidatosTests.cs`.

**Interfaces:** Produces a tarefa que apaga candidatos com mais de `retencaoDias` (180 por padrão): respostas, candidato e, depois da transação, os arquivos (currículo e respostas de arquivo); evento do Sistema sem dado pessoal.

- [ ] **Step 1: Testes (RED):** com o relógio 181 dias à frente o candidato, as respostas e os blobs somem; o de 179 dias fica; rodar duas vezes é inofensivo. **Step 2 a 5:** **Commit** `feat: retencao de candidatos (apaga os vencidos com respostas e arquivos)`.

- [ ] **Mutação do incremento 5c (T19 a T22):** igual; **Commit** `test: mutantes do incremento 5c`.

## Task 23: Responder avaliação (5d)

**Files:** Modify `CasosDeUsoDeResponder`, `ConsultaDeMeusFormularios`, `ConsultaDeAcompanhamento`. Test `Formularios/AvaliacoesTests.cs`.

**Interfaces:** Produces `avaliados` em #28 (com `respondido`), `avaliadoId` em #29 (422 `AVALIADO_INVALIDO`; uma resposta por avaliado, com nome e anônima; 400 em formulário que não é avaliação), `restantes` no recibo e `total`/`respondidos` do cartão, uma linha por avaliação em #14, `avaliadoId` em #16 e #30.

- [ ] **Step 1: Testes (RED):** avaliar quem não está na lista; a mesma pessoa duas vezes (409); `avaliadoId` em formulário que não é avaliação (400); avaliação do gestor com setor sem gestor (essas pessoas não veem o formulário e não contam); anônima com participação por (pessoa, avaliado). **Step 2 a 5:** **Commit** `feat: responder avaliacao (avaliados, restantes e uma resposta por avaliado)`.

## Task 24: Resultado por avaliado (#21) (5d)

**Files:** Modify `ResultadoDoFormulario`, `ExportacaoDoFormulario`; endpoints. Test `Formularios/ResultadoPorAvaliadoTests.cs`.

**Interfaces:** Produces `listarAvaliadosFormulario` (sem paginação, por nome; `esperadas` exato; anônimo só depois de encerrado e `totalRespostas` nulo para quem tem < 3; `resultadoLiberado`; 422 `FORMULARIO_NAO_E_AVALIACAO`; 409 antes de encerrar), `obterResultadoFormulario` com `avaliadoId` (anônimo com < 3 = 422), seções por avaliado no CSV anônimo.

- [ ] **Step 1: Testes (RED):** o exemplo de `yaml:1657-1662`; avaliado com 2 respostas some do resultado e do CSV; o mínimo conta **participações**. **Step 2 a 5:** **Commit** `feat: resultado por avaliado nas avaliacoes`.

- [ ] **Mutação do incremento 5d (T23 e T24):** igual; **Commit** `test: mutantes do incremento 5d`.

## Task 25: Verificação da fatia 5

**Files:** Modify `contrato/STATUS.md` (32 linhas e as observações das linhas 32-34 (contadores), 46 (`criarFuncionario`) e 77 (`confirmarImportacao`)), `README.md` (seção "Formulários (fatia 5)" com as perguntas em aberto e as Decisões acima).

- [ ] **Step 1:** os 32 `operationId` em `OperationIdsNoEscopo`; matriz de perfis verde; `dotnet build back-end --no-incremental` sem avisos; `dotnet format back-end --verify-no-changes`.
- [ ] **Step 2:** criar `ferramentas-rh/suite-formularios.sh` (grupos do namespace `Formularios`) e tirar o namespace do que a `suite-paralela.sh` já roda; suíte inteira em paralelo (paralela + comunicacao + formularios, depois ponto); revisor `fable` só leitura sobre `feab6b5..HEAD` (com o Review Focus deste plano) e **um** passe de correção RED→GREEN; ledger com `Final:`.
- [ ] **Step 3:** PR e `gh pr merge N --squash --admin`, sem trailer de Claude; sincronizar a `main`.

## Autorrevisão

- **Cobertura do contrato:** #2, #3, #4 (T4), #6-#8 (T5), #9 e #10 (T7), #11 e #12 (T8), #1 e #5 (T9), #13 (T10), #27 e #28 (T11), #29 (T12, T13, T23), #30 (T12, T23), #14, #16, #18 (T14), #20 e #22 (T15, T24), #23 (T16), #24-#26 (T17), #31 (T19), #32 (T20), #15, #17, #19 (T21), #21 (T24). Contagem: 13 + 13 + 5 + 1 = 32. T1-T3 e T6 são a base; T18 e T22 são tarefas agendadas.
- **Perguntas ao dono para o relatório final:** as oito de L:H.1 (decididas nas Decisões 1 a 8) e a retenção de 180 dias, a ausência do registro do aceite do aviso, a proibição da avaliação da equipe anônima, o Word por assinatura.
