# Ficha da fatia 5: formulários

Notas de cada tela: `docs/plano-front-end/telas/fatia-5.md` (leia a nota da tela antes de montá-la). Caminhos de tela: `docs/plano-front-end/05-caminhos-de-tela.md` (tabela única). Contrato: `contrato/fontes/50-fatia-5-formularios.yaml` (a versão do contrato é o `info.version` de `contrato/openapi.yaml`; anote-a em `PROGRESSO.md` a cada tela). Esta ficha **não** traz passo a passo fino: a sessão o deriva do padrão da fatia 1b (última seção).

## 1. Objetivo e entrega demonstrável

**Objetivo.** O RH monta um formulário (assistente de 4 etapas com prévia), publica para pessoas, setores ou por link público, acompanha quem respondeu e lê o resultado; o funcionário responde o que está pendente; o gestor vê só quem da equipe respondeu; o candidato responde pelo link, sem login. Há formulário **anônimo**, **avaliação** (equipe avalia o gestor e gestor avalia a equipe), **pesquisa** e **sugestão**. **Sem quiz, nota nem pontuação.**

**Entrega demonstrável** (com dados simulados; **contra a API real só a pessoa ou o dono o faz**, quando `contrato/STATUS.md` disser que a fatia 5 está `pronta`: a sessão não roda a API real):
1. O RH cria uma **pesquisa anônima**, passa pelas quatro etapas e a publica para 5 ou mais pessoas; a lista mostra "N esperados" e **nenhuma contagem de respostas**.
2. Um funcionário responde (a tela avisa que é anônima); o RH só vê o **resultado depois de encerrar**, pergunta por pergunta.
3. O RH publica uma **entrevista por link**; o candidato abre a página pública, aceita o aviso de privacidade e envia; o RH vê o candidato e baixa o currículo.
4. O gestor abre "Formulários do setor" e vê quem da equipe respondeu o que o RH pediu (sem conteúdo).
5. Uma **avaliação da equipe**: o gestor avalia as pessoas uma a uma e a faixa "Sua parte" mostra "3 de 8".

Incrementos do roadmap: 5a construtor, 5b responder e acompanhar, 5c link público e entrevista (superfície de segurança à parte), 5d avaliações por ciclo (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, fatia 5). Os blocos da seção 6 seguem essa divisão.

## 2. Telas (9 com print, mais as sem print)

| Tela | Nome | Perfil | PNG (`Figma/telas/`) | Nota |
|---|---|---|---|---|
| 16 | Formulários, lista | RH | `16-rh-formularios.png` | Tela 16 |
| 17 | Novo formulário, etapa Detalhes | RH | `17-rh-formularios-novo-formulario-etapa-detalhes.png` | Tela 17 |
| 18 | Etapa Perguntas | RH | `18-rh-formularios-novo-formulario-etapa-perguntas.png` | Tela 18 |
| 19 | Etapa Verificação | RH | `19-rh-formularios-novo-formulario-etapa-verificacao.png` | Tela 19 |
| 20 | Etapa Publicação | RH | `20-rh-formularios-novo-formulario-etapa-publicacao.png` | Tela 20 |
| 21 | Quem respondeu | RH | `21-rh-formularios-quem-respondeu.png` | Tela 21 |
| 39 | Meus formulários (Todos, Para responder, Disponíveis) | funcionário (também gestor e RH) | `39-func-formularios.png` | Tela 39 |
| 40 | Meus formulários, Respondidos | funcionário (também gestor e RH) | `40-func-formularios-aba-respondidos.png` | Tela 40 |
| 57 | Formulários do setor | gestor | `57-gestor-formularios-da-equipe.png` | Tela 57 |
| sem print | Resultado do formulário | RH | descrição em texto | "Tela sem print: Resultado" |
| sem print | Respostas de uma pessoa ou candidato | RH | descrição em texto | "Tela sem print: Respostas..." |
| sem print | Responder um formulário | todos | descrição em texto | "Tela sem print: Responder" |
| sem print | Minhas respostas | todos | descrição em texto | "Tela sem print: Minhas respostas" |
| sem print | Formulário público do candidato | público | descrição em texto | "Tela sem print: Formulário público" |

Também é desta fatia **ligar a caixa "Enviar o formulário de admissão"** da tela 02 (cadastro, 1b) **e** da importação (ficha 2): as duas a deixaram **visível, desmarcada e desabilitada** ("Indisponível por enquanto"), e **esta ficha liga as duas telas de uma vez** (nota "Ligação com outras telas"; bloco 9).

## 3. Rotas do contrato por tela

32 rotas na fatia (`contrato/ROTAS.md`). Cada uma aparece abaixo; `listarSetores`, `listarFuncionarios` e `listarGestores` (fatia 1), `obterUsuarioLogado` (fatia 0) e `obterContadoresDoMenuDoFuncionario` (fatia 7) são consumidas, não desta fatia.

| Tela | `operationId` |
|---|---|
| 16 lista | `listarFormulariosRh`, `encerrarFormulario`, `excluirFormulario`, `obterFormularioRh` (Editar) |
| 17 Detalhes | `criarFormulario`, `editarFormulario`, `obterFormularioRh` |
| 18 Perguntas | `adicionarPerguntaFormulario`, `editarPerguntaFormulario`, `excluirPerguntaFormulario`, `obterFormularioRh` |
| 19 Verificação | `verificarFormulario`, `salvarPublicacaoFormulario` |
| 20 Publicação | `salvarPublicacaoFormulario`, `publicarFormulario`, `verificarFormulario`, `obterFormularioRh` (+ `listarSetores`, `listarFuncionarios`, `listarGestores`) |
| 21 Quem respondeu | `obterFormularioRh`, `listarRespondentesFormulario`, `listarCandidatosFormulario`, `exportarRespostasFormulario`, `lembrarQuemFaltaFormulario`, `obterRespostasDeFuncionario`, `obterRespostasDeCandidato`, `baixarArquivoDeResposta`, `baixarCurriculoDeCandidato` |
| Resultado (sem print) | `obterResultadoFormulario`, `listarAvaliadosFormulario`, `exportarRespostasFormulario` |
| 39 e 40 | `listarMeusFormularios`, `obterMinhasRespostas` |
| Responder (sem print) | `obterFormularioParaResponder`, `responderFormulario` |
| Minhas respostas (sem print) | `obterMinhasRespostas` |
| 57 gestor | `listarFormulariosDaEquipe`, `listarRespondentesDaEquipe`, `lembrarQuemFaltaNaEquipe`, `listarMeusFormularios` (faixa "Sua parte") |
| Pública (sem print) | `obterFormularioPublico`, `responderFormularioPublico` |
| Sem tela própria | `adicionarDestinatariosFormulario` (só por dentro de `criarFuncionario`, `confirmarImportacao` e `readmitirFuncionario`, que mandam `formularioAdmissaoId`; nenhuma tela a chama) |

Conferência: 32 operações no contrato, 32 acima.

## 4. Regras e cortes que valem

**Cortes do dono (design, seção 7, e contrato).**
- **Tela 17:** só tem cinco tipos e não tem Pesquisa nem Sugestão: **acrescentar** (`PESQUISA` e `OUTRO`). **Sem Quiz.**
- **Tela 18:** sem "Quiz", sem selo "Eliminatória", sem "Nota dada pelo RH", sem "Correta:" e sem pontos.
- **Tela 19:** somem as linhas de quiz e de nota; entram os itens do anônimo. Há três situações (OK, AVISO, BLOQUEIO).
- **Tela 20:** **não tem** "Nota mínima para seguir", **"Enviar o resultado por e-mail ao RH"** nem **"Convidar aprovados para a entrevista"** (decisão do dono, 2026-10-30; o design, seção 9, ainda as lista como "em aberto": vale o contrato). Entram "Lembrar quem não respondeu 7 dias antes do prazo" (ligada por padrão) e o bloco "Repetir". Nenhum e-mail de "formulário publicado" nem de "formulário atribuído" (`contrato/CONVENCOES.md`, seção 12).
- **Tela 39:** **sem a frase "O RH vê os resultados agrupados por setor"** (a resposta anônima não guarda funcionário nem setor: `Banco de Dados/Scripts/V017`, linhas 139 e 140). Sem quiz, sem nota. Mostre `descricao` como vier.
- **Tela 40:** sem os dois quizzes; no formulário anônimo, sem data e sem "Ver minhas respostas".
- **Tela 57:** sem quizzes; **a avaliação do gestor não aparece para o gestor** (ele é o avaliado e não acompanha). Esconder "Lembrar Diego por e-mail" é da tela 51, não desta.
- **Sugestão:** caixa sempre aberta (`prazo` e `recorrencia` vazios), **nunca anônima**, aceita várias respostas da mesma pessoa (sem 409 `JA_RESPONDIDO`), o cartão fica sempre em "Disponível" e a pessoa não relê o que mandou.
- **Perguntas:** curta, longa, múltipla escolha (uma opção), escala de 1 a 5 e envio de arquivo (PDF ou Word, até 5 MB). Texto e markdown, **nunca HTML**. Anônimo não tem arquivo nem link público.

**Regras do anonimato** (valem em toda tela que mostra um formulário anônimo; o texto completo está em `telas/fatia-5.md`, "Regras do anonimato", e cada nota diz o efeito na sua tela):
- R1 só publica para 5 ou mais destinatários. R2 depois de publicado nada muda (anonimato, perguntas, destinatários; só título e descrição). R3 **resultado só depois de encerrar**. R4 mínimo de 3 respostas por pergunta (e por avaliado). R5 **sem contagem de respostas antes de encerrar** (só `esperados`; sem faixa; `temRespostas` não existe). R6 a auditoria não grava nome, número nem hora. R7 a API nunca devolve quem respondeu, data, hora, ordem, resposta por pessoa ou por setor. R8 o que a proteção não cobre: **a tela nunca promete mais do que o contrato**.
- Regra de ouro para quem monta: **se um número, uma data, uma ordem ou uma lista de pessoas não está no JSON de um formulário anônimo, não invente nem calcule**.

**Outras regras que atravessam fatias.** Datas: a API fala UTC e a tela mostra `America/Sao_Paulo`. Download de **arquivo guardado** (anexo de resposta, currículo) sempre por `LinkTemporario`, em nova aba (`abrirLinkTemporario`). Erros em `application/problem+json`: mostre o `detail` em português. **Exportação CSV devolve os bytes direto** com `Content-Disposition: attachment` (`contrato/CONVENCOES.md`, seção 4): use `baixarExportacao(chamada, nomePadrao)`; não é link temporário.

## 5. Decisões do dono ainda abertas (`x-provisorio`) e o que construir mesmo assim

Construa **o que o contrato diz**; a coluna da direita é o que muda se o dono decidir diferente (fonte: `contrato/PROVISORIOS.md`).

| Onde no contrato | O que a tela faz hoje | Se o dono mudar |
|---|---|---|
| `criarFormulario` | Sugestão nunca é anônima (a caixa "Respostas anônimas" some) | A caixa volta na Sugestão e o resultado fica preso ao encerramento manual |
| `excluirFormulario` | Formulário encerrado também se exclui | "Excluir" some da linha encerrada |
| `verificarFormulario` | Dado sensível é um AVISO por palavras-chave | Só muda a lista de itens |
| `obterFormularioPublico`, `responderFormularioPublico` | Só limite por IP (429); retenção de 180 dias | Com captcha, o envio ganha um campo de prova; o prazo de retenção só muda de valor |
| `FormularioTipo` | Admissão é um formulário como os outros; as respostas não vão sozinhas ao cadastro | Cópia automática exigiria um mapeamento pergunta-campo |
| `FormularioMinhaSituacao` | Admissão e Avaliação são "Para responder"; Pesquisa e Sugestão são "Disponíveis" | Pode ganhar o campo "obrigatório" |
| `FormularioRecorrencia` | Ao reabrir, o prazo avança o mesmo número de dias da rodada anterior | Só muda a descrição |
| `RespostaFormularioItem.alternativaIds` | Múltipla escolha de **uma** opção | Passa a aceitar várias; a pergunta ganha `permiteVariasOpcoes` |
| `FormularioPaginaPublica.retencaoDias` | 180 dias (leia o valor da API) | Só o valor muda |

## 6. Ordem de construção e dependências

**Da fundação (fatia 1a) esta fatia usa:** shell com menu por perfil e rotas protegidas por perfil; cliente de API tipado e a variante **sem sessão** para as rotas públicas; dados simulados (MSW) gerados do contrato; formatadores de data e de CPF/telefone; e os componentes-base: **janela em etapas** (cabeçalho, indicador de etapas, duas colunas, rodapé; a fatia 6 usa o mesmo), abas, tabela com paginação, selo, barra de progresso, cartão, caixa de marcar, escolha em cartões, campo de data em pt-BR, janela de confirmação, aviso temporário, envio de arquivo, helper de download por `LinkTemporario` e o componente seguro de texto/markdown. Se algum faltar, crie-o no lugar dos componentes compartilhados que a fundação definiu e anote em `PROGRESSO.md`.
**Da fatia 1b:** o padrão de lista com filtros (tela 01), de gaveta ou modal com formulário (tela 02), de detalhe com abas (tela 03), e as telas de cadastro que listam setores e gestores. **Das bolinhas do menu:** a fundação já lê `obterContadoresDoMenuDoFuncionario` (`useContadoresDoMenu`); esta fatia só chama `invalidarContadoresDoMenu` depois de responder (a bolinha "Formulários"). **Esta fatia não depende de nenhuma outra de 2 a 4**; a fatia 6 não depende desta.

**Blocos** (cada bloco termina com os testes verdes, `PROGRESSO.md` atualizado, commit e push; um bloco = vários passos pequenos como na fatia 1b):
1. **Lista e etapa Detalhes** (telas 16 e 17): tabela, abas com `contagemPorTipo`, filtros, menu "..." (Encerrar, Excluir, confirmações), assistente com a primeira etapa (`criarFormulario`, `editarFormulario`). *5a.*
2. **Perguntas e Verificação** (telas 18 e 19): adicionar, editar, mover, apagar; as regras por situação; verificação com três situações. *5a.*
3. **Publicação** (tela 20): quem responde, agendar, prazo, lembrete automático, "Repetir", escolha de setores e pessoas, publicar, link gerado, erros 422. *5a.*
4. **Responder, interno** (telas 39 e 40, "Responder", "Minhas respostas"): formulários com nome, perguntas de todos os tipos, envio multipart, `JA_RESPONDIDO`; Sugestão. *5b.*
5. **Acompanhar** (tela 21 e "Respostas de uma pessoa", tela 57): cartões, lista, "Ver respostas", "Lembrar", "Baixar respostas". *5b.*
6. **Anonimato** (todas as telas): revise cada tela contra R1 a R8, escreva os testes de anonimato (seção 7) e construa a página **Resultado**. *5b.*
7. **Avaliações:** lista de avaliados na tela de responder, recibo com `restantes`, faixa "Sua parte" (tela 57), resultado por avaliado e `listarAvaliadosFormulario`. *5d.*
8. **Link público** (superfície de segurança à parte): lista de candidatos (variante da tela 21), respostas e currículo, e a **página pública** sem sessão. *5c.*
9. **Ligações:** liga a caixa "Enviar o formulário de admissão" **na tela 02 (cadastro) e na importação de uma vez** (e no diálogo "Readmitir" da ficha, tela 03, que a 1b deixou com as mesmas caixas desligadas: `telas/fatia-1.md`, "Ligação com outras telas"; `telas/fatia-5.md`, "Ligação com outras telas"; `listarFormulariosRh` com `tipo=ADMISSAO` e `situacao=PUBLICADO`; sem nenhum publicado, a caixa fica desligada com a dica da nota); a chamada a `invalidarContadoresDoMenu` depois de responder; relatório final com as telas a conferir.

## 7. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura confere as **32 operações** da seção 3, **menos** `adicionarDestinatariosFormulario` (sem tela própria: só por dentro de `criarFuncionario`, `confirmarImportacao` e `readmitirFuncionario`; a exclusão fica escrita, com o motivo, no `coberturaDaFatia5.test.ts`). O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Passos novos do `csp`:** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário), **um passo novo**, porque a página pública do candidato é a primeira página pública com formulário, envio de arquivo e markdown (o aviso de privacidade) **sem sessão**: o passo abre `/formulario/<token de teste>` sem login (como a 1b abre `/esqueci-senha` e `/definir-senha`) e conta violações. Aqui só se descreve o passo, sem código de `.mjs`: o script e o ponto de acréscimo (`ACRESCENTE AQUI`) são os da 1b (`fatia-1b-telas.md`, Task 10, passo 4). O envio de arquivo em si usa o `SeletorDeArquivo` da 1a, que não é recurso novo.

**Testes que precisam existir** (nomes descritivos; a sessão escolhe os nomes finais):
- **Contrato de parâmetros por tela:** cada tela confere os `query` e `path` enviados (o `tsc` não pega chave extra). Em especial: a aba Sugestões manda `tipo=OUTRO`; "Avaliações" manda `tipo=AVALIACAO`; `confirmar=true` só na repetição; `avaliadoId` só nas avaliações; `respondidoEmUtc` só na Sugestão.
- **Texto proibido:** um teste renderiza cada tela da fatia e **falha** se encontrar, **nos rótulos da tela**, algum destes termos: "Quiz", "nota", "Eliminatória", "Nota dada pelo RH", "Nota mínima", "Enviar o resultado por e-mail", "Convidar aprovados", "agrupados por setor". Duas regras de método, porque `nota` em minúsculo aparece dentro de outras palavras ("anotação", "denota") e nos dados:
  - **Já pronto na 1b:** `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` de `src\test\textosProibidos.ts` (**não os reescreva**) e as linhas desta fatia acrescentadas à tabela de `src\app\textosProibidos.test.tsx`. O `rotulosDaTela` da 1b não lista `placeholder` nem `aria-label`: se uma tela os usar com texto proibido, acrescente-os ao `rotulosDaTela` e ao teste dele.
  - **Limite de palavra:** procure com expressão regular `\b...\b` e sem diferença de maiúsculas (`/\bnota\b/i`, `/\bquiz\b/i`; nas frases, a frase inteira). (O `acharTextoProibido` da 1b usa limite de palavra que também vale para palavra acentuada; o `\b` do JavaScript falha quando a palavra começa com letra acentuada.)
  - **Só nos rótulos, não nos dados da API:** a asserção é sobre o texto visível **dentro do contêiner da tela** (`within(screen.getByRole('main'))`) e olha só os elementos de interface: títulos (`heading`), botões, abas, cabeçalhos de coluna (`columnheader`), `label`, `legend`, `placeholder` e `aria-label`. **Não** olha células (`cell`) nem o texto de itens que vêm dos mocks (título, descrição e nome de um formulário de exemplo podem ter "nota"). Se um rótulo de interface é montado com texto da API, o teste usa a resposta do exemplo com o texto livre trocado por "Xx".
- **Ligação das caixas** (cadastro, importação **e readmissão**, de uma vez): a caixa "Enviar o formulário de admissão" **deixa de estar desabilitada** (nas três telas: a tela 02, a importação e o diálogo "Readmitir" da ficha, tela 03) e lista os formulários de `listarFormulariosRh` com `tipo=ADMISSAO` e `situacao=PUBLICADO` (o formulário 11, "Admissão: outubro de 2026"); sem nenhum, fica desligada com a dica da nota; o cadastro (`criarFuncionario`) e a readmissão (`readmitirFuncionario`) enviam `formularioAdmissaoId`; a importação confirma com `formularioAdmissaoId: 11` e mostra "2 no formulário de admissão" (`incluidosNoFormularioDeAdmissao`); 422 `FORMULARIO_ENCERRADO` (`confirmarImportacao` 422 `formularioEncerrado`, `criarFuncionario` 422 `formularioEncerrado`; `readmitirFuncionario` tem os "mesmos erros do cadastro" no contrato, mas **sem exemplo nomeado** para este: monte a resposta com `problema(...)` de `src\test\capturar.ts`) e `FORMULARIO_NAO_E_POR_PESSOAS` mostram o `detail`. (Os testes da ficha 2 e da 1b que conferem esta caixa **desabilitada** passam a conferir a caixa **habilitada**, **inclusive** o de `DialogoReadmitir.test.tsx` da 1b ("as caixas de onboarding e de formulario ficam desligadas...": o teste que a 1b deixou sem dono, `fatia-1b-telas.md`, Task 3, parte D); com a caixa desmarcada, `formularioAdmissaoId: null` continua valendo. O que confere a caixa do onboarding desabilitada fica até a ficha 6.)
- **Anonimato** (usando os exemplos nomeados do contrato, por `exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`; ver `03-contrato-e-mocks.md`: `listarRespondentesFormulario` 200 `identificado`, `anonimoAberto`, `anonimoEncerrado`, `sugestao`; `listarRespondentesDaEquipe` 200 `identificado`, `anonimoAberto`; `obterResultadoFormulario` 200 `anonimoEncerrado`, `avaliadoComRespostasSuficientes`, `comNome` e 422 `nenhumaPergunta`, `avaliadoAbaixoDoMinimo`; `listarMeusFormularios` 200 `abertos`, `respondidos`; `lembrarQuemFaltaFormulario` 200 `identificado`, `anonimo`; `responderFormulario` 201 `identificado`, `anonimo`, 409 `jaRespondido`, `candidato`):
  - lista: anônimo aberto não mostra número de respostas nem barra; mostra "N esperados"; anônimo encerrado mostra números exatos;
  - tela 21: anônimo mostra só os três cartões, sem abas, busca, tabela nem "Ver respostas"; "Lembrar quem falta" mostra só "Lembrete enviado." (nenhum número); Sugestão mostra data e hora por linha;
  - tela 57: não lista `EQUIPE_AVALIA_GESTOR`; anônimo aberto não mostra respondidos nem lista; **não oferece formulário encerrado** (o gestor não vê encerrados, decisão de 2026-11-02); o botão "Lembrar quem falta por e-mail" existe (decidido, sem "usa provisório") e chama `lembrarQuemFaltaNaEquipe`;
  - telas 39 e 40: cartão anônimo sem data e sem "Ver minhas respostas"; Sugestão sempre "Disponível";
  - assistente: anônimo desabilita "Envio de arquivo" e "Candidatos, por link"; anônimo publicado abre travado; Sugestão esconde "Respostas anônimas";
  - resultado: 409 mostra o texto sem número; `perguntasOcultas` vira a frase; os textos aparecem **na ordem recebida**; 422 do avaliado mostra "menos de 3";
  - responder: o aviso de anonimato aparece; nada é salvo em `localStorage`.
- **Menu "Meu espaço"** (decisão do dono, 2026-11-02): com o perfil RH e com o perfil Gestor, o grupo "Meu espaço" mostra "Meus formulários" e o clique leva a `/eu/formularios` (a tela abre com o menu do perfil ainda à vista); o perfil Funcionário **não** ganha o grupo e já tem "Formulários" em "Comunicação". O teste usa `menuVisivel`/`todosOsCaminhosDoMenu` da 1a e `renderizarTela`.
- **Tela 20:** não existem as três caixas cortadas; "Lembrar quem não respondeu" vem ligada e some sem prazo ou com `alvo = LINK`; `agendarPara` num rascunho avisa que não é guardado; o link usa o `tokenDoLink`.
- **Envio multipart** (com `// @vitest-environment node`): responder com arquivo; candidato com currículo; 400 com `errors` por campo; 409 `JA_RESPONDIDO`.
- **Página pública:** nenhuma chamada leva `Authorization`; 404, 422 (`FORMULARIO_AINDA_NAO_ABERTO`, `FORMULARIO_ENCERRADO`, `EMPRESA_NAO_CONFIGURADA`), 409 e 429 mostram o texto da nota; "Começar" só habilita com o aceite.
- Cada tela cobre **carregando, vazio, erro e sem permissão**.

**Pendente de integração** (a sessão **não roda** contra a API real: registra "pendente de integração" em `PROGRESSO.md` **com o roteiro pronto** e consulta `contrato/STATUS.md` só para saber se a rota existe; **a pessoa ou o dono** roda o roteiro depois, com a pilha inteira):
- Todas as 32 rotas, até a fatia 5 estar `pronta` em `contrato/STATUS.md`.
- Em especial o que só a API real prova: envio multipart com arquivo de verdade; links temporários e CSV reais; **concorrência** (dois envios ao mesmo tempo, 409 `JA_RESPONDIDO`); **limites 429** da página pública; **agendamento e encerramento por prazo** feitos pelo agendador; **recorrência**; **lembrete por e-mail** (captador); e as regras de anonimato do lado do servidor (o front só garante que não pede nem mostra o que o contrato proíbe).
- Conferência visual (a pessoa, uma vez, ao lado do PNG): telas **16, 17, 18, 19, 20, 21, 39, 40 e 57** e a página pública (no celular).

## 8. Riscos

1. **Vazar anonimato por detalhe de tela:** um número, uma frase ("100% anônimo"), uma data, uma ordem, uma contagem em faixa. Defesa: seção 4 (regra de ouro), os testes de anonimato e a revisão de cada tela contra R1 a R8 no bloco 6.
2. **O assistente é grande** (quatro etapas, regras por situação, tipos, anônimo). Defesa: a API é a fonte da verdade; **recarregue `obterFormularioRh` depois de cada gravação** e não replique regra no front além de habilitar e desabilitar o que a nota manda.
3. **`agendarPara` num rascunho não é guardado** (o banco só o guarda em `AGENDADO`): a tela precisa avisar e pedir a data de novo.
4. **Página pública e a sessão do site:** usar o cliente com sessão por engano mandaria `Authorization` e acionaria a renovação. Defesa: variante de cliente sem sessão e o teste "nenhuma chamada leva `Authorization`".
5. **Upload multipart nos testes:** o jsdom não o monta; use `// @vitest-environment node` e o gerador de dados simulados da fundação.
6. **Exemplos nomeados do contrato:** os estados do anônimo (aberto, encerrado, Sugestão) são `examples` com nome; o handler gerado pega um por padrão. Os testes carregam os outros pelo nome com `exemploDoContrato(operationId, status?, nome?)` (`src\shared\teste\`; ver `03-contrato-e-mocks.md` e a lista da seção 7).
7. **Gestor e RH respondem pelo menu "Meu espaço":** decidido pelo dono em 2026-11-02 (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Meus formulários"** existe no grupo "Meu espaço" do RH e do gestor e leva a `/eu/formularios` (a página abre para os três perfis); o gestor chega também pela faixa "Sua parte" da tela 57.
8. **Largura:** o Figma é 1440 px e a tabela da tela 16 passa disso; a demo pode ser 1366 px. A página pública deve funcionar em celular.
9. **Dado de candidato é pessoal (LGPD):** não registre no console nem guarde em armazenamento local; só o RH o vê.
10. **Texto de usuário:** perguntas, descrições e respostas são texto ou markdown, **nunca HTML** (um XSS vira tomada de conta, porque o refresh token está no `localStorage`).

## 9. Onde os documentos se contradizem (vale o contrato; não corrija os documentos)

- `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md` (seção 9, "Tela 20") e `docs/superpowers/specs/2026-10-21-rh-system-design.md` (seção 13.3, "Resultado por e-mail ao RH e convite automático dos aprovados") **ainda tratam** as caixas da tela 20 como previstas ou em aberto; o contrato (`publicarFormulario`, `FormularioPublicacaoComando`) registra a decisão do dono de 2026-10-30 de que **não existem**.
- Spec (linha 96) e roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, seção "Fatia 5: formulários", subseção "Regras", item do e-mail "formulário atribuído") citam o e-mail "formulário atribuído"; `contrato/CONVENCOES.md`, seção 12, diz que **não existe** (o RH usa um Aviso).
- O roadmap (seção "Fatia 5: formulários", subseção "Regras", item "Anonimato", onde diz que o tamanho mínimo de grupo "começa em 3") fala só no mínimo de 3 respostas; o contrato acrescenta o **público mínimo de 5**, **perguntas congeladas** e **nenhuma contagem antes de encerrar**.
- Contradição **dentro do contrato**: `listarFormulariosDaEquipe` não traz formulário encerrado, mas a regra 8 da tag Formulários e `listarRespondentesDaEquipe` dizem que o gestor, depois de encerrar, vê quantos da equipe participaram. Na tela 57 o estado "anônimo encerrado" **não tem caminho**: só trate a resposta se ela chegar (decidido pelo dono em 2026-11-02: o gestor **não** vê formulário encerrado na lista; antes a dúvida `D5-3`; `DUVIDAS.md`, "Decisões do dono").
- Figma e contrato: ver "O que sai ou muda" de cada nota (tipos, selos, caixas, nota, "agrupados por setor", datas, avaliação do gestor na tela 57).

## 10. Decisões do dono de 2026-11-02 e dúvidas já conhecidas (as dúvidas se registram em `DUVIDAS.md` com o modelo de lá; o texto abaixo é o conteúdo; **não bloqueiam**)

**Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; **não se registram como dúvida**):
- **Onde gestor e RH respondem formulários** (antes `D-menu`): o RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu, e o item **"Meus formulários"** é um deles, **ativo**, levando a `/eu/formularios`; o gestor chega também pela faixa "Sua parte" da tela 57.
- **Gestor e formulário anônimo encerrado, tela 57** (antes `D5-3`): o gestor **não** vê formulário encerrado na lista; o estado "anônimo encerrado" não tem caminho nessa tela, e só se trata a resposta se ela chegar.
- **"Lembrar quem falta por e-mail" do gestor, tela 57** (antes `D5-4`): o botão **fica**; o e-mail entrou na lista fechada (`contrato/CONVENCOES.md`, seção 12, contrato 0.2.1). **Não é mais provisório** (`contrato/PROVISORIOS.md` já não o lista): construa normalmente e **não** anote "usa provisório".

**Dúvida ainda aberta:**
- **D5-2. "Encerrou em ..." na lista (tela 16).** O PNG diz "Encerrou em 30/09" e o exemplo do contrato (prazo 30/09, `encerradoEmUtc` 2026-10-01T03:00Z) dá 01/10 se mostrarmos o dia de `encerradoEmUtc`. Sugiro mostrar o dia de `encerradoEmUtc`. **Pergunta:** a lista mostra o último dia do prazo (30/09) ou o dia em que o sistema encerrou (01/10)? (Muda um dia na tela.)

## 11. Repita o padrão da fatia 1b

`docs/plano-front-end/fatia-1b-telas.md` é o modelo. Para **cada tela** desta ficha, nesta ordem:
1. Leia a nota da tela em `telas/fatia-5.md`, abra o PNG correspondente (quando houver) e anote em `PROGRESSO.md` a versão do contrato.
2. Escreva o **teste de contrato** (parâmetros enviados) e o **teste da tela** com dados simulados (estados: carregando, vazio, erro, sem permissão, mais as regras da nota e do anonimato), **rode e veja falhar**.
3. Implemente o mínimo para passar: página, componentes da tela, ligação com a rota tipada.
4. Rode teste, `lint` e `format:check`; no fim do bloco, o `build` sob a política de segurança.
5. Atualize `PROGRESSO.md` (tela feita, versão do contrato, "pendente de integração" onde couber) e faça **commit e push** na branch da sessão, **sem coautor Claude**.
6. Dúvida nova: `DUVIDAS.md`, com o texto pronto; **não edite o contrato, o banco nem o back-end**.
