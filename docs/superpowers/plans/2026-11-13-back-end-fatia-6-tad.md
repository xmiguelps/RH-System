# Back-end, fatia 6 (TAD, treinamentos) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:executing-plans (inline) to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Entregar as 31 rotas de treinamentos (`contrato/fontes/60-fatia-6-tad.yaml`): o RH monta (treinamento, módulos de texto, vídeo por link e PDF), publica para todos, setores ou pessoas e acompanha ("quem fez" e CSV); a pessoa vê o que é dela, se inscreve nos opcionais, faz os módulos em qualquer ordem e, ao concluir o último, recebe o certificado em PDF; o gestor vê só números da equipe; qualquer um confere um certificado pelo código; o onboarding inclui a pessoa nos treinamentos de onboarding e a caixa do cadastro, da readmissão e da importação passa a funcionar.

**Architecture:** Mesma receita das fatias 3 a 5: regras puras em `Application/Treinamentos/Regras`, casos de uso, consultas e repositórios por porta, mensagens do contrato num catálogo com teste contra o `openapi.yaml`, auditoria **à mão** (entidades sem `[Auditado]`: lote de publicação, trigger do certificado), arquivos pela receita "validar → gravar o blob → transação curta → apagar se falhar", PDF do certificado com PDFsharp-MigraDoc e a fonte embutida que o espelho de ponto já usa. A situação do treinamento é **calculada** pela data (sem agendador para liberar nem encerrar); a única tarefa agendada é a de incluir quem for admitido.

**Tech Stack:** .NET 10, EF Core 9 + Pomelo 9.0.0, MySQL 8.4 (Testcontainers), xUnit, PDFsharp-MigraDoc 6.2.4 (já no projeto), PdfPig só nos testes.

**Spec:** `contrato/fontes/60-fatia-6-tad.yaml` (autoridade) e o levantamento `docs/superpowers/plans/2026-11-13-back-end-fatia-6-tad-levantamento.md` (citado `L:A`, `L:B.n`, `L:D.n`, `L:E.n`, `L:F`, `L:G`, `L:H`); o plano argumenta a partir deles: **os dois viajam com o plano e o executor lê os dois**.

## Global Constraints

- O contrato vence o roadmap e os comentários do `V001` (L:E.3). **Sem e-mail** nesta fatia, sem prazo, sem lembrete, sem nota, sem quiz; **não** criar a tarefa `LIBERAR_TREINAMENTO`; `DATA_FIM`, `AVISA_POR_EMAIL`, `LEMBRA_ANTES_DO_FIM` e `NOTA` ficam fora do modelo.
- Instantes gravados cortados ao segundo, UTC na aplicação e -03:00 no banco (`ConversoresDeTempo.Brasil`); "hoje" pelo dia de Brasília (`FusoBrasil`); **toda** data gravada pela aplicação vem do `IRelogio` (os `CURDATE()`/`CURRENT_TIMESTAMP` do banco e do trigger usam o relógio real): `DATA_INICIO`, `DATA_CONCLUSAO`, `DATA_EMISSAO` e a do progresso saem da aplicação.
- `EMITE_CERTIFICADO` e `INCLUI_ADMITIDOS` mapeados **sem** padrão do banco (gravar sempre, os dois padrões são `TRUE`); `TIPO_TREINAMENTO` = `'ONBOARDING'` ou `NULL`; `STATUS_TREINAMENTO` sempre explícito; `NOTA` nunca gravada.
- **Certificado:** a aplicação **nunca insere** em `TREINAMENTOS_CERTIFICADOS` (o trigger cria a linha com UUID v1); na mesma transação em que a linha vira `CONCLUIDO` ela faz UPDATE da linha do certificado trocando `CODIGO_VALIDACAO` por UUID **v4** (minúsculas, 36 caracteres) e `DATA_EMISSAO` pelo relógio; um código v1 nunca sai em resposta nem em PDF; o `numero` é `CERT-{ano de DATA_EMISSAO}-{ID_CERTIFICADO:00000}`; o PDF é gerado **depois** do commit, nunca dentro da transação.
- A pessoa só mexe na **própria** linha (`IEscopoDoUsuario.FuncionarioId`); gestor só a equipe de hoje (ativos, admissão começada, do setor, sem ele) e **nunca** certificado, código ou conteúdo de módulo; progresso só de módulo do treinamento da rota (outro = 404).
- Arquivos: só `PDF` tem arquivo (módulo, até 10 MiB, tipo pelo conteúdo, nome limpo sem diretório); vídeo só por link `http(s)://`; texto e descrição nunca como HTML; servido só como anexo por link temporário; o caminho de armazenamento **nunca** sai da API; arquivo gravado fora de transação e apagado se a gravação falha; blobs de excluir/trocar apagados **depois** do commit; as pastas `treinamentos/` e `certificados/` têm fonte de referências para a limpeza de órfãos (e a fonte errada apagaria certificado válido: testar os dois lados).
- O código do certificado é segredo de quem o tem: nenhum log, auditoria ou mensagem de erro o traz; o tratador global de erros loga a rota pública como `/api/publico/certificados/{codigo}` (o mesmo cuidado do código do link da fatia 5).
- Todo `ExecuteUpdate`/`ExecuteDelete` tem `// AUDITORIA-OK: motivo` até 3 linhas acima (`GuardaDeExecuteUpdateTests`). Colunas anuláveis do banco ficam anuláveis na entidade (`MapeamentoContraBancoTests`); as seis tabelas saem de `tabelas-sem-mapeamento.txt` no mesmo passo em que são mapeadas.
- Toda escrita de `/api/eu` e toda exclusão leem o treinamento **na mesma ordem** (treinamento, depois a linha da pessoa) para não dar deadlock nem 1451/1452 cru; UPDATE de situação sempre condicional na situação de partida; nenhum UPDATE em linha `CONCLUIDO` (o trigger recusa).
- Cada rota: o `operationId` em `ContratoDivergenciaTests.OperationIdsNoEscopo`, a linha em `contrato/STATUS.md`, o enum de consulta em `EnumsDeParametrosDoContrato`, o multipart (#11, #13) em `FormulariosDoContrato`, `AfirmarConformeAoContratoAsync` nas respostas e a matriz de perfis verde (`/api/gestor/equipe/{funcionarioId}/` é de várias fatias: registrar por `operationId`, nunca por prefixo).
- **TDD de verdade:** o teste vermelho vem antes do código e o vermelho é visto; depois de cada tarefa `dotnet format back-end --verify-no-changes` e os testes transversais (`GuardaDeExecuteUpdate|.Contrato.|Matriz|.Treinamentos.|Configuracao|Agendamento`). Commit e push por tarefa; **sem** `Co-Authored-By`, `Claude-Session` nem "Generated with Claude Code" em commit ou PR (`git log origin/main..HEAD --format=%B | grep -ciE "Co-Authored-By|Claude|Generated with"` = 0 antes de cada push).

## Decisões do plano (Rulings; as perguntas H do levantamento, decididas sem esperar o dono, cada uma com o custo se errada)

1. **Vídeo (H1):** só link externo; `VIDEO` não aceita arquivo. *Custo:* upload de vídeo é fatia nova (tipo de arquivo, limite, custo de armazenamento).
2. **Onboarding (H2):** a caixa `onboarding` marca `TIPO_TREINAMENTO = 'ONBOARDING'`; pode haver vários em vigor (publicado, não encerrado, agendado entra) e a pessoa entra em todos, qualquer que seja o público. *Custo:* muda #4, #19 e a ponte.
3. **Admitidos (H3):** tarefa diária `INCLUIR_ADMITIDOS_NO_TREINAMENTO` (chave `...:dia=AAAA-MM-DD`, com `RecuperarAsync`): para todo treinamento **obrigatório, publicado, não encerrado, com `INCLUI_ADMITIDOS`**, insere `INSCRITO` para toda pessoa ativa, com admissão já ocorrida, no público e **sem linha** (cobre o admitido novo, o "Em admissão" que começou, o readmitido e quem entrou num setor do público). Ator Sistema, um evento por treinamento e dia com a contagem, sem nomes. *Custo:* gente a mais ou a menos como pendente.
4. **Certificado (H4):** PDF paisagem de uma página: nome da empresa (se configurada), nome da pessoa, título **do dia da emissão**, carga horária (se há duração), `numero`, data, código e o endereço da página de conferência (só se `App:UrlDoFront` está preenchida). O título do treinamento continua editável. *Custo:* layout (troca o gerador); o PDF e a conferência podem mostrar títulos diferentes se o RH renomear.
5. **Pendência (H5):** pessoa **inativa** não conta em `pessoasComObrigatorioPendente`, nos números da confirmação de encerrar/excluir nem nos `pendentes` das fichas (a lista "Quem fez" mostra todos); quem está "Em admissão" conta. *Custo:* um filtro nas contagens.
6. **Conferência pública (H6):** o nome de quem saiu continua aparecendo (o contrato diz que o código continua válido); código que não é v4 (inclusive um v1 antigo) = o **mesmo 404**; formato que não é UUID = 400; sem auditoria da consulta. *Custo:* decisão de privacidade, mexe só em T14.
7. **Depois de publicado (H7):** como no contrato (só título, descrição, `incluiAdmitidos` e `comecaEm` enquanto agendado; módulos só com correção de conteúdo), e a correção de conteúdo vale também no **encerrado**; `comecaEm` nulo em publicado = 400. *Custo:* alto se liberar estrutura (módulo novo não reabre quem concluiu).
8. **Auditoria (H8):** eventos à mão: criar (`Criou`), alterar e mexer em módulos (`Alterou`), publicar (`Publicou`, com a quantidade de linhas criadas), encerrar (`Alterou`), excluir (`Excluiu`, título e `pendentes`), a **conclusão do treinamento** (`Alterou`, porque a linha do certificado nasce por trigger) e o RH baixando certificado de outra pessoa (`Baixou`); nenhum evento para inscrever, iniciar, concluir módulo, baixar o próprio certificado nem arquivo de módulo. *Custo:* falta rastro de download.
9. **Conclusão defensiva:** "concluir" um módulo já concluído devolve o estado atual sem `UPDATE`, **exceto** se todos os módulos estão concluídos e a linha não é `CONCLUIDO` (corrida que prendeu a pessoa em 100%): aí fecha o treinamento. Desvio consciente de "repetir não toca no banco" (`yaml:1380`) só no caso quebrado. *Custo:* um `UPDATE` a mais num caso raro.
10. **Linhas `REPROVADO`, `CANCELADO` ou de situação nula** (dado antigo) não aparecem em lista nem em contagem. **Campo de texto vazio** de outro tipo num multipart vale como ausente. **Encerrado e linha `INSCRITO` que nunca começou:** #24 e #25 = 404, igual ao #21. **Pessoa opcional sem inscrição:** vê o cartão e a lista de módulos (#21), o conteúdo e o arquivo dão 422 `INSCRICAO_PENDENTE`; com linha `INSCRITO` o conteúdo abre sem "Começar".
11. **Nomes:** download do certificado `certificado-<título>.pdf` (`NomeParaDownload.Limpar`); CSV `treinamento-<título em minúsculas, sem acento, hífens>.csv`; `ordenarPor=SITUACAO` na ordem lógica (não iniciado, em andamento, concluído); posição do nulo ao ordenar por `COMECA_EM`: por último nos dois sentidos; `contarPublicoDeTreinamento` ignora id que não existe.
12. **Textos de erro sem exemplo** (L:B.1): o executor escreve, registra `Ruling:` e o teste de mensagens tem lista de exceções.

## Review Focus

1. **Conclusão em corrida:** duas abas concluindo os dois últimos módulos = um `CONCLUIDO`, uma linha de certificado, código v4; concluir × excluir × encerrar sem deadlock e sem 1451 cru; pessoa nunca presa em 100% (T12).
2. **Certificado:** nenhum código v1 em lugar nenhum; PDF fora da transação e a conclusão sobrevive a falha do PDF; só o dono e o RH baixam; o gestor nunca vê certificado nem código; dois downloads simultâneos deixam um caminho só (T12, T13).
3. **Rota pública do certificado:** limite por IP antes de tudo, o **mesmo** 404 para inexistente, de treinamento que não emite e de conclusão sem certificado (mesmo corpo), só quatro campos, código nunca em log (T14).
4. **Quem vê o quê:** a pessoa só a própria linha; gestor só a equipe de hoje e sem o gestor; treinamento rascunho, não liberado ou encerrado sem ter começado = 404; opcional sem inscrição não lê conteúdo (T9, T11, T16).
5. **Arquivos e exclusão:** PDF do módulo pelo conteúdo, caminho nunca sai, blobs apagados depois do commit, a limpeza de órfãos não apaga certificado nem módulo válido; excluir apaga os filhos na ordem certa com o treinamento travado (T5, T7, T13).
6. **Onboarding:** a caixa do cadastro, da readmissão e da importação inclui na mesma transação; encerrar entre conferir e incluir desfaz o cadastro (nunca "0 em silêncio"); `incluidosNoOnboarding` conta pessoas (T17).

---

## Task 1: Mapeamento das seis tabelas e sonda dos triggers (6a)

**Files:** Create `Domain/Treinamentos/{Treinamento,TreinamentoModulo,TreinamentoModuloProgresso,TreinamentoPublico,TreinamentoFuncionario,TreinamentoCertificado}.cs` e enums (`PublicoDoTreinamento`, `TipoDeModulo`, `SituacaoNoTreinamento`), configurações em `Infrastructure/Persistencia/Configuracoes/*Configuracao.cs`, `DbSet`s no `AppDbContext`; Modify `tests/.../Persistencia/tabelas-sem-mapeamento.txt` (tirar as seis). Test `Persistencia/MapeamentoDosTreinamentosTests.cs`.

**Interfaces:** Produces as entidades sem `[Auditado]` (L:D.1): `ENCERRADO_EM` e `TREINAMENTOS_MODULOS_PROGRESSO.DATA_CONCLUSAO` com `ConversoresDeTempo.Brasil` (a segunda gravada pela aplicação), `CHAVE_DESTINO` calculada (`CalculadaNoBanco`), `EMITE_CERTIFICADO`/`INCLUI_ADMITIDOS` sem padrão, `NOTA`/`DATA_FIM`/caixas mortas fora do modelo, a entidade do certificado só para **ler e atualizar** (sem fábrica de inserção).

- [ ] **Step 1: Testes (RED):** `MapeamentoContraBancoTests` e `ConformidadeDoModeloTests` verdes com as tabelas fora do arquivo de exceções; sonda que grava pelo EF (`EMITE_CERTIFICADO=false` relido como `false`, módulo de cada tipo, público por setor e por pessoa), passa uma linha `INSCRITO` a `CONCLUIDO` e lê a linha de certificado criada pelo trigger (código v1, `DATA_EMISSAO` do banco), e prova as três recusas de trigger de L:D.2 (inativo, `CONCLUIDO` não muda, `CONCLUIDO` já concluído).
- [ ] **Step 2 a 4:** RED, implementar, GREEN. **Step 5: Commit.** `feat: entidades e mapeamento dos treinamentos (fatia 6)`.

## Task 2: Regras puras (6a)

**Files:** Create `Application/Treinamentos/Regras/{SituacaoDoTreinamento,ValidacaoDoTreinamento,ValidacaoDoModulo,CamposTravados,ProgressoDoTreinamento,VisibilidadeParaAPessoa,MinhaSituacao,NumeroDoCertificado,NomesDeArquivo}.cs`. Test `Application.Tests/Treinamentos/*`.

**Interfaces:** Produces `SituacaoDoTreinamento.De(publicado, comecaEm, encerradoEm, hoje)` (rascunho, agendado, em andamento, encerrado; publicado sem data = já liberado), `ValidacaoDoTreinamento` (comando completo com todos os erros juntos: `titulo` 3 a 200, `descricao` até 2000, `publico` e listas sem repetição, rascunho × publicar com `errors.comecaEm`/`errors.modulos`/`publico`), `CamposTravados.Decidir(situacao, atual, novo)` (conjuntos para listas; 422 `TREINAMENTO_CAMPO_TRAVADO`/`TREINAMENTO_ENCERRADO`), `ValidacaoDoModulo` (cada tipo exige o seu campo e recusa os dos outros; `urlExterna` `^https?://` até 500; texto até 50000; duração 1 a 32767), `ProgressoDoTreinamento` (concluídos, piso do percentual, módulo atual, duração total), `VisibilidadeParaAPessoa` (liberado, encerrado só para quem começou ou concluiu, opcional do público = `DISPONIVEL`), `MinhaSituacao`, aba/contagens, `NumeroDoCertificado.De(ano, id)`, nomes de CSV e de download.

- [ ] **Step 1: Testes (RED):** as quatro situações e a fronteira de `comecaEm` = hoje; matriz de campos travados por situação; cada tipo de módulo aceito/recusado; lista de módulos vazia ao publicar; `todos = disponiveis + paraResponder` do exemplo do contrato (2, 1, 1, 3); `CERT-2026-00012`; nome do CSV sem acento e com hífens.
- [ ] **Step 2 a 4:** RED, implementar, GREEN, mutação das regras. **Step 5: Commit.** `feat: regras puras dos treinamentos (situacao por data, campos travados, modulos, progresso e visibilidade)`.

## Task 3: Erros da fatia 6 (6a)

**Files:** Create `Application/Treinamentos/Mensagens/{MensagensDosTreinamentos,RegrasDoBancoDosTreinamentos}.cs`, `Infrastructure/Treinamentos/TradutorDeRecusaDosTreinamentos.cs`; Modify `Api/Configuracao/TextosDeErro.cs` (títulos "Treinamento encerrado.", "Confirmação necessária.", "Certificado não encontrado."). Test `Contrato/MensagensDosTreinamentosTests.cs`, `Treinamentos/TradutorDeRecusaDosTreinamentosTests.cs`.

**Interfaces:** Produces uma propriedade por código de L:B.1 (texto exato do contrato; os "sem exemplo" escritos pelo executor), as fábricas `ConfirmacaoNecessaria(pendentes, emAndamento, naoIniciaram)` (os três extras sempre no JSON), `TreinamentoComCertificados(n)` (`certificadosEmitidos`), `FuncionarioInativo` (com `regraDoBanco` fixa), o tradutor (45000 pela mensagem; 1062 pelo nome da chave: par do treinamento, módulo concluído duas vezes, ordem do módulo, público repetido; CHECKs → 400 ou o 422 certo sem copiar valor).

- [ ] **Step 1: Testes (RED):** cada mensagem confere com o `openapi.yaml` montado (lista de exceções para os textos sem exemplo: singulares, 400 de campo); o 409 de confirmação e o 422 de certificados saem com os extras; o tradutor mapeia cada recusa de L:B.2. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: erros dos treinamentos (catalogo contra o contrato e tradutor de recusa do banco)`.

## Task 4: Criar, obter, atualizar e contar o público (#2, #6, #7, #5) (6a)

**Files:** Create `Application/Treinamentos/Construtor/{CasosDeUsoDoTreinamento,TreinamentosDtos,ITreinamentosRepositorio,IConsultaDeTreinamentos,IPublicoDoTreinamento}.cs`, `Infrastructure/Treinamentos/{TreinamentosRepositorio,ConsultaDeTreinamentos,PublicoDoTreinamento}.cs`, `Api/Treinamentos/TreinamentosRhEndpoints.cs`; Modify `Program.cs`, `DependencyInjection.cs`. Test `Treinamentos/ConstrutorDoTreinamentoTests.cs`.

**Interfaces:** Produces `criarTreinamento` (201, rascunho só com o título, `incluiAdmitidos`/`emiteCertificado` sempre gravados, sem módulos, evento `Criou`), `obterTreinamento` (qualquer situação, módulos em ordem com texto e link, PDF só como `nomeArquivo`), `atualizarTreinamento` (substitui **todos** os campos; rascunho: tudo muda e público trocado na mesma transação apagando antes de inserir; publicado/encerrado pelos campos travados; lista do outro público = 400 antes do 422), `contarPublicoDeTreinamento` (só ativos com admissão ocorrida; `TODOS` ignora as listas), e a **porta do público** (`IPublicoDoTreinamento`: quem é do público agora) usada por #5, #9, #20, #21, #22, #28 e pela tarefa.

- [ ] **Step 1: Testes (RED):** rascunho relido do banco com a caixa `TIPO_TREINAMENTO` certa e as duas colunas `TRUE` gravadas explicitamente; `PUT` omitindo campo volta ao padrão; trocar `SETORES` por `PESSOAS` no rascunho apaga as linhas antigas; publicado muda só título/descrição/`incluiAdmitidos` e a data enquanto agendado; encerrado 422 `TREINAMENTO_ENCERRADO`; `setorIds` com id que não existe = 400 no campo; público contado só com ativos admitidos; inexistente 404; sem login 401, gestor/funcionário 403. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: treinamentos do RH (criar, obter, atualizar e contar o publico)`.

## Task 5: Módulos (#11, #12, #13, #14) (6a)

**Files:** Create `Application/Treinamentos/Construtor/CasosDeUsoDosModulos.cs`, `Infrastructure/Treinamentos/FonteDeReferenciasDeModulos.cs`; Modify repositório, endpoints, `Api/Configuracao/FormulariosDoContrato.cs` (os dois `operationId` multipart), DI. Test `Treinamentos/ModulosDoTreinamentoTests.cs`.

**Interfaces:** Produces `adicionarModuloDeTreinamento` (fim da lista; só rascunho; PDF até 10 MiB pelo conteúdo com a receita do arquivo na pasta `treinamentos/`; campos de outro tipo = 400), `reordenarModulosDeTreinamento` (todos os ids, repetido/faltando/de outro = 400; **duas fases** numa transação com valores positivos), `atualizarModuloDeTreinamento` (sem `arquivo` num `PDF` mantém o atual; rascunho troca o tipo e anula os campos dos outros **no mesmo UPDATE**, apagando o arquivo antigo depois do commit; publicado/encerrado só o conteúdo, tipo travado), `excluirModuloDeTreinamento` (só rascunho; renumera; apaga o blob depois), a fonte de referências de `treinamentos/`, evento `Alterou`.

- [ ] **Step 1: Testes (RED):** mover o módulo 3 para a posição 1 sem 1062; `.exe` renomeado para `.pdf`, PDF de 10,5 MB e arquivo vazio = 400 e nenhum blob; `urlExterna` num `TEXTO` e `javascript:` = 400; trocar `PDF` por `TEXTO` apaga o blob e anula `CAMINHO_ARQUIVO` e o nome juntos; excluir módulo PDF apaga o blob; publicado e encerrado: acrescentar/reordenar/excluir = 422 `TREINAMENTO_MODULOS_TRAVADOS`, corrigir conteúdo passa e trocar o tipo = 422 `TREINAMENTO_CAMPO_TRAVADO`; duas abas acrescentando módulo (um 201 e o outro refaz ou 409 sem 500); a limpeza de órfãos não apaga o blob de um módulo válido. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: modulos do treinamento (texto, video por link e PDF, ordem em duas fases e travas)`.

## Task 6: Publicar (#9) (6a)

**Files:** Create `Application/Treinamentos/Construtor/CasosDeUsoDePublicar.cs`; Modify repositório, endpoints. Test `Treinamentos/PublicarTreinamentoTests.cs`.

**Interfaces:** Produces `publicarTreinamento` (400 com `errors` por campo: `titulo`, `comecaEm`, `publico` com ao menos um setor ou pessoa, `modulos`; 422 `TREINAMENTO_JA_PUBLICADO` também no encerrado; UPDATE condicional em `PUBLICADO = FALSE`; obrigatório cria linha `INSCRITO` para cada pessoa **ativa com admissão ocorrida** do público numa transação, pulando quem já tem linha; opcional e `TODOS` não criam nada; **nenhum e-mail**; evento `Publicou` com a quantidade).

- [ ] **Step 1: Testes (RED):** publicar sem módulo/sem data/sem público = 400 com todos os campos juntos; obrigatório para um setor cria só as linhas dos ativos admitidos (inativo e "Em admissão" não); opcional não cria linha; publicar duas vezes em paralelo = um 200 e um 422 sem linha duplicada; `comecaEm` futuro = `AGENDADO` e hoje = `EM_ANDAMENTO`; evento sem nomes. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: publicar treinamento (validacao por campo, linhas do obrigatorio em lote e evento)`.

## Task 7: Encerrar e excluir (#10, #8) (6a)

**Files:** Create `Application/Treinamentos/Construtor/CasosDeUsoDeEncerrarEExcluir.cs`; Modify repositório, endpoints. Test `Treinamentos/EncerrarEExcluirTreinamentoTests.cs`.

**Interfaces:** Produces `encerrarTreinamento` (só `EM_ANDAMENTO`; 409 `CONFIRMACAO_NECESSARIA` com `pendentes`/`emAndamento`/`naoIniciaram` de pessoas **ativas** quando há pendente e `confirmar` falso; grava `ENCERRADO_EM` agora com UPDATE condicional; evento `Alterou`), `excluirTreinamento` (exclusão física; 422 `TREINAMENTO_COM_CERTIFICADOS` com `certificadosEmitidos` **antes** da confirmação e também com `confirmar=true`; rascunho e agendado apagam direto; em andamento ou encerrado sem certificado: 409 se há `INSCRITO`/`EM_ANDAMENTO`; transação com o treinamento **travado**, ordem progresso → módulos → certificados (as linhas do trigger) → participantes → público → treinamento; os PDFs dos módulos apagados **depois** do commit; evento `Excluiu` com título e `pendentes`).

- [ ] **Step 1: Testes (RED):** encerrar em rascunho/agendado/encerrado = 422 certos; os três números do 409 (e o singular); encerrar tira a pendência e mantém os certificados; excluir treinamento que não emite, com concluídos, não deixa linha em nenhuma das seis tabelas nem blob; com certificado emitido = 422 mesmo com `confirmar=true`; conclusão no mesmo instante da exclusão não deixa 1451 cru (uma das duas ganha); inexistente 404. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: encerrar e excluir treinamento (confirmacao, certificados emitidos e ordem dos filhos)`.

## Task 8: Lista e indicadores do RH (#1, #3) (6a)

**Files:** Modify `ConsultaDeTreinamentos`, `Api/Configuracao/EnumsDeParametrosDoContrato.cs`, endpoints. Test `Treinamentos/ListaDeTreinamentosDoRhTests.cs`.

**Interfaces:** Produces `listarTreinamentos` (situação por data **no SQL**, o mesmo predicado no filtro e nas `contagens`; rascunhos incluídos; `totalParticipantes`, `concluidos` e `certificadosEmitidos` por linha (`concluidos` só se emite, senão 0; rascunho zerado); ordenação `TITULO`/`COMECA_EM`, direção, desempate por id; `contagens` ignora `situacao` e respeita `busca`) e `obterIndicadoresDeTreinamentos` (`emAndamento`, `obrigatorios` publicados em qualquer situação, `certificadosEmitidosNoAno` + `ano`, `pessoasComObrigatorioPendente` só de pessoas ativas).

- [ ] **Step 1: Testes (RED):** o exemplo do contrato; cada situação na fronteira (`comecaEm` hoje e amanhã, encerrado); `contagens` com `busca`; rascunho com participantes zerados; inativo fora de `pessoasComObrigatorioPendente`; ano pelo relógio fixo. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: lista e indicadores de treinamentos do RH`.

- [ ] **Mutação do incremento 6a (T2 a T8):** spec por `ferramentas-rh/gerar-spec-f6-6a.mjs` (copiar o `gerar-spec-f5-5b.mjs`), `mutar-em-paralelo.mjs` com 8 workers em segundo plano e vigiado; um teste por sobrevivente real (classe `MutantesDoIncremento6aTests`), equivalentes no ledger; **Commit** `test: mutantes do incremento 6a`.

## Task 9: Meus treinamentos e detalhe (#20, #21) (6b)

**Files:** Create `Application/Treinamentos/Fazer/{CasosDeUsoDosMeusTreinamentos,MeusTreinamentosDtos,IConsultaDeMeusTreinamentos}.cs`, `Infrastructure/Treinamentos/ConsultaDeMeusTreinamentos.cs`, `Api/Treinamentos/TreinamentosEuEndpoints.cs`. Test `Treinamentos/MeusTreinamentosTests.cs`.

**Interfaces:** Produces `listarMeusTreinamentos` (só publicados liberados; as linhas da pessoa + opcionais do público dela de hoje ainda não escolhidos = `DISPONIVEL`; encerrado só para quem começou ou concluiu; `aba` padrão `TODOS` = tudo que não foi concluído; `contagens` ignora `aba` e respeita `busca` e `obrigatorio`; ordem fixa: obrigatórios, `comecaEm` mais antigo, título) e `obterMeuTreinamento` (módulos com `concluido` e **sem** conteúdo; 404 em rascunho, não liberado, sem linha e fora do público, encerrado sem ter começado; concluído traz `certificado` só se emite).

- [ ] **Step 1: Testes (RED):** o exemplo do contrato (2, 1, 1, 3); cada abá; opcional do setor da pessoa = `DISPONIVEL`, de outro setor = 404; encerrado some para quem não começou e continua para quem concluiu; linha `REPROVADO` não aparece; o gestor e o RH usam a mesma rota (nunca 403); a pessoa nunca vê linha de outra. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: meus treinamentos (lista por aba e detalhe com a visibilidade da pessoa)`.

## Task 10: Inscrever e iniciar (#22, #23) (6b)

**Files:** Create `Application/Treinamentos/Fazer/CasosDeUsoDeInscreverEIniciar.cs`; Modify repositório, endpoints. Test `Treinamentos/InscreverEIniciarTests.cs`.

**Interfaces:** Produces `inscreverMeEmTreinamento` (só opcional, do público, publicado e liberado; cria `INSCRITO`; repetir devolve o detalhe; 1062 = "já estava"; 422 `TREINAMENTO_NAO_E_OPCIONAL`/`TREINAMENTO_ENCERRADO`), `iniciarMeuTreinamento` (`INSCRITO` → `EM_ANDAMENTO` com `DATA_INICIO` do relógio por UPDATE condicional; repetir devolve o estado atual **sem UPDATE**; 422 `INSCRICAO_PENDENTE` e `TREINAMENTO_ENCERRADO`; nada em linha `CONCLUIDO`). As duas leem o treinamento antes da linha da pessoa.

- [ ] **Step 1: Testes (RED):** inscrever em obrigatório = 422; em outro setor = 404; duas abas inscrevendo = uma linha; iniciar duas vezes = um `UPDATE` (data não muda); iniciar opcional sem inscrição = 422; encerrado = 422; inativa não consegue (trigger traduzido); linha `CONCLUIDO` nunca é tocada. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: inscrever e iniciar o treinamento (idempotentes e sem tocar no concluido)`.

## Task 11: Conteúdo e arquivo do módulo (#24, #25) (6b)

**Files:** Create `Application/Treinamentos/Fazer/CasosDeUsoDoConteudo.cs`; Modify endpoints. Test `Treinamentos/ConteudoDoModuloTests.cs`.

**Interfaces:** Produces `obterConteudoDeModulo` (módulos em qualquer ordem; vale reler concluído e com o treinamento encerrado para quem começou ou concluiu; sem linha = 404; opcional sem inscrição = 422 `INSCRICAO_PENDENTE`; módulo de outro treinamento = 404; texto como veio, nunca HTML) e `baixarArquivoDeModulo` (só módulo `PDF`, outro = 404, mesmas regras de acesso, `LinkTemporario` como anexo, caminho nunca aparece).

- [ ] **Step 1: Testes (RED):** cada ramo 404 × 422; `INSCRITO` lê sem "Começar"; encerrado com linha `INSCRITO` que nunca começou = 404; o link é `https://` assinado sem o caminho de armazenamento e o nome de download limpo; módulo de vídeo/texto no download = 404. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: conteudo e arquivo do modulo (404 x inscricao pendente, link temporario)`.

## Task 12: Concluir módulo (#26), sem o PDF (6b)

**Files:** Create `Application/Treinamentos/Fazer/{CasosDeUsoDeConcluirModulo,IEmissaoDoCertificado}.cs`, `Infrastructure/Treinamentos/{ConclusaoDoTreinamento,CertificadosRepositorio}.cs`; Modify endpoints, DI. Test `Treinamentos/ConcluirModuloTests.cs`.

**Interfaces:** Produces `concluirModuloDeTreinamento` (transação com **trava na linha do treinamento e depois na da pessoa**: grava o progresso (1062 = já concluído), `INSCRITO` → `EM_ANDAMENTO` (início implícito), se era o último módulo passa a linha a `CONCLUIDO` com `DATA_CONCLUSAO` do relógio, **troca no mesmo commit o código do certificado por UUID v4 e a `DATA_EMISSAO`** (UPDATE na linha criada pelo trigger, que o EF não rastreia), evento da conclusão; repetir um módulo já concluído devolve o estado atual sem `UPDATE`, exceto a conclusão defensiva da Decisão 9; resposta `TreinamentoModuloConclusaoResposta` com `concluidoEmUtc` do progresso e `certificado` só se emite; 404 de módulo de outro treinamento; 422 `INSCRICAO_PENDENTE`/`TREINAMENTO_ENCERRADO`). Define a porta `IEmissaoDoCertificado` (gerar o PDF e guardar o caminho) para a T13.

- [ ] **Step 1: Testes (RED):** o exemplo do contrato; os dois últimos módulos concluídos **em paralelo** = um `CONCLUIDO`, uma linha de certificado, código v4 (nunca v1), `DATA_EMISSAO` do relógio fixo; repetir não muda nada; a corrida que deixou todos concluídos e a linha em `EM_ANDAMENTO` fecha na chamada seguinte; treinamento que não emite conclui sem `certificado` na resposta e a linha de certificado do trigger continua escondida; concluir × excluir/encerrar sem deadlock; progresso de módulo de outro treinamento = 404; a conclusão não grava `NOTA`. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: concluir modulo (um CONCLUIDO por pessoa, codigo v4 na mesma transacao e inicio implicito)`.

## Task 13: PDF e downloads do certificado (#27, #17) (6b)

**Files:** Create `Infrastructure/Treinamentos/{GeradorDePdfDoCertificado,EmissaoDoCertificado,FonteDeReferenciasDeCertificados}.cs`, `Application/Treinamentos/Fazer/CasosDeUsoDoCertificado.cs`; Modify `FonteDoPdf` (sai de `Ponto` para um lugar comum, se couber), endpoints. Test `Treinamentos/CertificadoTests.cs`.

**Interfaces:** Produces o gerador (MigraDoc, paisagem, Noto Sans embutida; conteúdo da Decisão 4), a emissão **depois do commit** (gerar, gravar o blob, UPDATE condicional `CAMINHO_ARQUIVO IS NULL`; falha só vira log e o download gera depois), `baixarMeuCertificado` e `baixarCertificadoDeParticipante` (404 se não concluiu, se é de outra pessoa ou se o treinamento não emite; gera na hora, **uma vez**, se faltar o arquivo; o perdedor da corrida apaga o próprio blob; o RH baixando o de outra pessoa grava `Baixou` antes do link; nome `certificado-<título>.pdf`) e a fonte de referências de `certificados/`.

- [ ] **Step 1: Testes (RED):** PdfPig lê no PDF o nome com acento, o `numero` `CERT-2026-NNNNN` e o código v4; a conclusão com o armazenamento fora do ar **continua concluída** e o primeiro download gera; dois downloads simultâneos sem arquivo deixam um caminho só e um blob; gestor e outra pessoa = 404 (gestor sem rota); treinamento que não emite = 404; a limpeza de órfãos não apaga o certificado emitido e apaga a sobra; o RH baixando de outra pessoa grava evento sem o nome. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: certificado em PDF (emissao depois da conclusao, download sob demanda e uma vez)`.

## Task 14: Conferência pública do certificado (#31) (6b)

**Files:** Create `Application/Treinamentos/Publico/CasosDaConferencia.cs`, `Api/Treinamentos/CertificadosPublicoEndpoints.cs`; Modify `Api/Configuracao/LimiteDeTentativas.cs` (política `certificado-conferencia`, 10 por minuto por IP, configurável), `Api/Configuracao/TratamentoDeErros.cs` (`CaminhoParaLog`: `/api/publico/certificados/{codigo}`), `Infra/ApiFactory.cs` (limite alto nos testes). Test `Treinamentos/ConferirCertificadoTests.cs`.

**Interfaces:** Produces `validarCertificado` em `GrupoPublico` (429 antes de tudo com `Retry-After` e o texto do contrato; formato que não é UUID = 400; **o mesmo 404** com título próprio para inexistente, de treinamento que não emite, de conclusão sem certificado e de código v1/que não é v4; resposta só com `codigoValidacao`, nome, título e data), sem auditoria e sem registrar o código em log.

- [ ] **Step 1: Testes (RED):** 11ª consulta no minuto = 429 e outro IP passa; os três 404 com o mesmo corpo e o mesmo status; um v1 escrito à mão no banco = 404; a resposta tem exatamente quatro campos (nenhum id, `numero`, setor, e-mail, ativo); 500 forçado (armazenamento/banco falhando) não escreve o código no log; a matriz de perfis nunca 401/403 na pública. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: conferencia publica do certificado (limite por IP, mesmo 404 e codigo so v4)`.

- [ ] **Mutação do incremento 6b (T9 a T14):** igual à 6a; classe `MutantesDoIncremento6bTests`; **Commit** `test: mutantes do incremento 6b`.

## Task 15: Quem fez e exportação (#15, #16) (6c)

**Files:** Create `Application/Treinamentos/Acompanhar/{CasosDeUsoDeParticipantes,ParticipantesDtos,IConsultaDeParticipantes}.cs`, `Infrastructure/Treinamentos/ConsultaDeParticipantes.cs`, `Api/Treinamentos/TreinamentosRhAcompanhamentoEndpoints.cs`. Test `Treinamentos/ParticipantesDoTreinamentoTests.cs`.

**Interfaces:** Produces `listarParticipantesDeTreinamento` (só quem tem linha; inativos continuam; rascunho = lista vazia; `contagens` ignora `situacao` e respeita `setorId` e `busca`; `ordenarPor` `NOME`/`SETOR`/`SITUACAO`/`INICIO`/`CONCLUSAO`; `concluidoEmUtc` vem do progresso) e `exportarParticipantesDeTreinamento` (UTF-8 com BOM, `;`, `Pessoa;Setor;Situação;Início;Conclusão`, `dd/MM/aaaa`, situação em português, `CsvSeguro`, nome `treinamento-<título>.csv`, evento `Baixou` à mão como a exportação de formulário).

- [ ] **Step 1: Testes (RED):** os exemplos do contrato; cada filtro/ordenação (nulos de `CONCLUSAO`); inativo aparece; rascunho vazio; encerrado consultável; célula `=SOMA(1;1)` neutralizada; 404 de treinamento inexistente. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: quem fez o treinamento e exportacao em CSV`.

## Task 16: Ficha do RH, equipe do gestor e o indicador (#18, #28, #29, #30) (6c)

**Files:** Create `Application/Treinamentos/Equipe/{CasosDeUsoDaEquipe,EquipeDtos}.cs`, `Infrastructure/Treinamentos/{ConsultaDaEquipe,FonteDeTreinamentosPendentes}.cs`, `Api/Treinamentos/TreinamentosGestorEndpoints.cs`; Modify DI (a fonte de `IndicadorDaFicha.TreinamentosPendentes`). Test `Treinamentos/EquipeETreinamentosTests.cs`.

**Interfaces:** Produces `listarTreinamentosDeFuncionario` (as linhas da pessoa em treinamentos publicados já liberados; encerrado só se começou ou concluiu; obrigatórios primeiro; `resumo` pendentes/emAndamento/concluidos; com certificado para o RH), `listarTreinamentosDaEquipe` (publicados que alcançam ao menos uma pessoa do setor; sem o gestor; gestor sem setor = lista vazia), `listarPessoasDaEquipeNoTreinamento` (só pessoas do setor **com linha**, ordem por nome, `emFeriasAte` por `IFeriasDoPonto`, sem nota nem certificado; rascunho ou que não alcança o setor = 404), `listarTreinamentosDePessoaDaEquipe` (a lista do #18 sem certificado; outro setor ou o próprio gestor = 404) e a **fonte do indicador `treinamentosPendentes`** = `resumo.pendentes` do #30 (só pessoas ativas).

- [ ] **Step 1: Testes (RED):** o `resumo.pendentes` do #30 igual ao indicador da ficha da equipe; outro setor e o próprio gestor = 404 nas duas rotas de pessoa; **nenhum campo de certificado, código ou conteúdo** nas respostas do gestor; férias do `FERIAS`; inativo some para o gestor e aparece para o RH; gestor sem setor = lista vazia no #28 e 404 no #29 e #30; rota de equipe registrada por `operationId` sem colidir com as outras fatias. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: treinamentos da ficha, da equipe do gestor e o indicador de pendentes`.

## Task 17: Onboarding e a ponte com o cadastro (#4, #19) (6c)

**Files:** Create `Application/Treinamentos/Onboarding/CasosDeUsoDoOnboarding.cs`, `Infrastructure/Treinamentos/InclusaoNoOnboarding.cs`; Modify DI (registrar **depois** de `OnboardingIndisponivel`), endpoints, `STATUS.md` (linhas 46 e 77). Test `Treinamentos/OnboardingTests.cs`; Modify os quatro testes que hoje afirmam o 422 (cadastro, `criarFuncionario`, readmissão, importação) para ganharem o par do caminho feliz.

**Interfaces:** Produces `listarTreinamentosDeOnboarding` (em vigor = publicado, marcado, **não encerrado**, agendado entra; sem paginação; lista vazia desliga a caixa), `incluirFuncionarioNoOnboarding` (cria `INSCRITO` em **cada** treinamento em vigor qualquer que seja o público, quem já tem linha não muda e vem `jaEstavaIncluida`; 422 `SEM_TREINAMENTO_DE_ONBOARDING` e `FUNCIONARIO_INATIVO`; evento `Alterou`) e a implementação real de `IInclusaoNoOnboarding` (`ConferirAsync` com o texto longo do cadastro; `IncluirAsync` **dentro da transação do chamador**, devolve **quantas pessoas** entraram e lança o mesmo 422 se nenhum treinamento está em vigor, nunca 0 em silêncio).

- [ ] **Step 1: Testes (RED):** sem treinamento de onboarding o cadastro/readmissão/importação continuam recusando com o texto de hoje; com um publicado a caixa inclui na mesma transação e `incluidosNoOnboarding` conta pessoas (3 pessoas e 2 treinamentos = 3); encerrar entre conferir e incluir desfaz o cadastro; readmitido que já tinha linha `CONCLUIDO` não muda; inativa = 422; duas chamadas do #19 = `jaEstavaIncluida`; "Em admissão" é incluída. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: onboarding (lista em vigor, incluir a pessoa e a caixa do cadastro, da readmissao e da importacao)`.

## Task 18: Tarefa de incluir quem for admitido (6c)

**Files:** Create `Infrastructure/Treinamentos/IncluirAdmitidosNoTreinamentoTarefa.cs`; Modify DI e `ExecutorDeTarefasTests` (`Assert.Equal(20, recuperados)`). Test `Treinamentos/AdmitidosNoTreinamentoTests.cs`.

**Interfaces:** Produces a tarefa da Decisão 3 (`Tipo = "INCLUIR_ADMITIDOS_NO_TREINAMENTO"`, uma por dia com `DiaReferencia`, `RecuperarAsync` agenda a do dia, idempotente: 1062 = já estava, o inativo é pulado no próprio `SELECT`; ator Sistema; um evento `Alterou` por treinamento e dia só com a contagem).

- [ ] **Step 1: Testes (RED):** pessoa admitida ontem e a que mudou para um setor do público entram; rodar duas vezes não duplica; treinamento encerrado, opcional, sem a caixa ou rascunho não recebe ninguém; ligar a caixa depois de publicado inclui todo o atraso de uma vez; inativo não entra; o evento não tem nomes; a recuperação agenda uma por dia. **Step 2 a 5:** RED, implementar, GREEN, **Commit** `feat: tarefa que inclui no treinamento obrigatorio quem for admitido`.

## Task 19: Fechamento da fatia 6 (6c)

**Files:** Modify `contrato/STATUS.md` (os 31 `operationId`, a linha da fatia 6 em "em andamento", observações das linhas 46 e 77), `README.md` (seção "Treinamentos (fatia 6)": rotas, certificado, onboarding, tarefa, perguntas em aberto), `ferramentas-rh/suite-treinamentos.sh`.

- [ ] **Step 1:** os 31 `operationId` em `OperationIdsNoEscopo`; matriz de perfis verde; `dotnet build back-end --no-incremental` sem avisos; `dotnet format back-end --verify-no-changes`.
- [ ] **Mutação do incremento 6c (T15 a T18):** igual; classe `MutantesDoIncremento6cTests`; **Commit** `test: mutantes do incremento 6c`.
- [ ] **Step 2:** `suite-treinamentos.sh` (grupos do namespace `Treinamentos`, soma conferida contra `--list-tests`); suíte inteira (paralela + formularios + treinamentos, depois comunicacao + ponto); revisor `fable` só leitura sobre `ac63500..HEAD` (com o Review Focus deste plano) e **um** passe de correção RED→GREEN; ledger com `Final:`.
- [ ] **Step 3:** PR e `gh pr merge N --squash --admin`, sem trailer de Claude; sincronizar a `main`.

## Autorrevisão

- **Cobertura do contrato:** #1 e #3 (T8), #2, #5, #6, #7 (T4), #8 e #10 (T7), #9 (T6), #11 a #14 (T5), #15 e #16 (T15), #17 (T13), #18, #28, #29, #30 (T16), #19 e #4 (T17), #20 e #21 (T9), #22 e #23 (T10), #24 e #25 (T11), #26 (T12), #27 (T13), #31 (T14). Contagem: 13 (6a: #1, #2, #3, #5 a #14) + 10 (6b: #17, #20 a #27, #31) + 8 (6c: #4, #15, #16, #18, #19, #28, #29, #30) = 31 rotas. T1 a T3 são a base; T18 é tarefa agendada (sem rota).
- **Perguntas ao dono para o relatório final:** as oito de L:H.1 (decididas nas Decisões 1 a 8), o título do treinamento editável depois de emitido, o nome de quem saiu continuar na conferência pública, a conclusão defensiva, o evento de download.
