# Notas de tela da fatia 5: formulários

Contrato lido: `contrato/fontes/50-fatia-5-formularios.yaml` (a versão do contrato é o `info.version` de `contrato/openapi.yaml`). Fatia 5 tem 32 rotas (`contrato/ROTAS.md`). Ficha da fatia: `docs/plano-front-end/fichas/fatia-5.md`.

## Como ler estas notas

- **"Li no print"** é o que está escrito no PNG indicado (`Figma/telas/NN-...png`, 1440 px de largura). **"Contrato"** é o que o `operationId` citado diz. O que não tem nenhuma das duas marcas e vem com **(suponho)** é inferência minha: confirme na conferência visual ou registre em `DUVIDAS.md`.
- **Onde o Figma e o contrato divergem, vale o contrato** (e a seção 7 do design `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md`). Cada nota diz o que o PNG mostra e o front não terá, com a razão.
- Nomes de pessoas e números dos PNG são só ilustração. Os dados simulados (MSW) vêm dos exemplos do contrato e do elenco (`contrato/ELENCO.md`): por exemplo, o formulário 11 "Admissão: outubro de 2026" tem 3 pessoas esperadas e **ninguém respondeu**, enquanto o PNG 21 mostra 2 de 3.
- **Caminhos de tela:** a tabela única é `05-caminhos-de-tela.md` (vale sobre qualquer caminho escrito nesta nota). Os desta fatia: lista `/rh/formularios`; assistente `/rh/formularios/novo` e `/rh/formularios/:formularioId/editar` (modal sobre a lista, etapa em `?etapa=detalhes`, `perguntas`, `verificacao` ou `publicacao`); quem respondeu `/rh/formularios/:formularioId/respondentes`; resultado `/rh/formularios/:formularioId/resultado`; respostas de uma pessoa, modal sobre a tela 21 (`.../respondentes/funcionarios/:funcionarioId` ou `.../candidatos/:candidatoId`); telas 39 e 40 `/eu/formularios` (`?aba=todos`, `para-responder`, `disponiveis` ou `respondidos`); responder `/eu/formularios/:formularioId/responder`; minhas respostas `/eu/formularios/:formularioId/minhas-respostas`; gestor `/gestor/formularios`; página pública do candidato `/formulario/:token` (fora da área logada e sem menu).

## Regras do anonimato (resumo; o texto completo está na descrição da tag Formulários do contrato)

Cada nota abaixo repete o efeito que a regra tem **naquela tela**; aqui só os números para referência.

- **R1** Formulário anônimo só publica para 5 ou mais destinatários (422 `FORMULARIO_ANONIMO_PUBLICO_PEQUENO`); o recorrente anônimo confere de novo a cada reabertura.
- **R2** Depois de publicado, nada muda: anonimato (409 `ANONIMATO_FIXO_APOS_PUBLICAR`), perguntas (409 `ANONIMO_PERGUNTAS_FIXAS`, sem olhar se há resposta) e destinatários (409 `ANONIMO_SEM_NOVOS_DESTINATARIOS`). Só título e descrição mudam.
- **R3** O resultado só existe depois de encerrado (409 `RESULTADO_ANONIMO_INDISPONIVEL` em resultado, avaliados e exportação).
- **R4** Mínimo de 3 respostas por pergunta (a que tem menos some e só se conta quantas sumiram); na avaliação anônima, também 3 por avaliado.
- **R5** Antes de encerrar não há contagem de respostas, nem exata, nem em faixa: `respondidos`, `faltam` e `taxaDeRespostaPercentual` vêm vazios e só `esperados` vem exato; `temRespostas` não existe.
- **R6** A auditoria não grava número, nome, autor nem hora de resposta anônima (assunto do back-end; o front só não deve contradizer).
- **R7** A API nunca devolve: quem respondeu ou falta, data, hora ou ordem de resposta, resposta por pessoa, setor ou data, pergunta com menos de 3, resultado de avaliado com menos de 3.
- **R8** O que não está coberto (escrito para ninguém prometer demais na tela): quem lê o banco ou o backup, o conluio de duas pessoas do RH, a subtração de totais, o e-mail do lembrete e o fato de o gestor saber, depois de encerrar, quantos da equipe participaram. **A tela não deve escrever frases como "100% anônimo" ou "ninguém jamais saberá"**: diga só o que o contrato diz ("o RH e o gestor veem só o resultado geral de cada pergunta, nunca quem respondeu").

---

## Tela 16: Formulários, lista (RH)

PNG: `Figma/telas/16-rh-formularios.png`.

**Li no print**
- Título "Formulários", subtítulo "Entrevistas, admissão, avaliações e quizzes de treinamento", botão "+ Novo formulário" no alto à direita.
- Abas com número: Todos 6, Entrevistas 2, Admissão 1, Avaliações 2, Quizzes 1. Busca "Buscar formulário" e seletor "Todas as situações".
- Tabela com colunas Formulário (título e, embaixo, o tipo), Quem responde, Respostas, Prazo, Situação e Ações. Exemplos de "Quem responde": "Candidatos, por link", "Novos colaboradores (3)", "Todos os colaboradores", "Gestores (12)", "Turma LGPD 2026". Exemplos de "Respostas": "Nenhuma ainda", barra com "2 de 3", "58 de 101", "7 de 12", "63 de 101", e "41 respostas". Prazo: "Até 31/10", "Sem prazo", "Encerrou em 30/09". Situação: selos Rascunho, Publicado, Encerrado.
- Ações: botão "Editar" em toda linha; botão escuro "Quem respond..." **cortado na borda direita** (a tabela passa de 1440 px) nas linhas publicadas e encerrada; "..." visível só na linha do rascunho (nas outras a borda corta a tabela, então não dá para ver se há). Rodapé "6 formulários".

**O que fica**
- Abas por tipo, busca no título, filtro de situação, as seis colunas, "Novo formulário", "Editar", "Quem respondeu" (o texto do botão cortado é, suponho, "Quem respondeu", como o título da tela 21) e o menu "...".

**O que sai ou muda (e por quê)**
- **Aba "Quizzes" e tipo Quiz saem** (sem quiz, decisão de 2026-10-29). **Entram "Pesquisas" e "Sugestões"** (contrato `listarFormulariosRh`; `contagemPorTipo` traz `todos`, `entrevistas`, `admissoes`, `avaliacoes`, `pesquisas`, `sugestoes`). "Sugestões" é `tipo=OUTRO`; "Avaliações" são os dois sentidos juntos. Os números das abas ignoram a aba escolhida e respeitam busca e situação.
- Subtítulo novo: "Entrevistas, admissão, avaliações, pesquisas e sugestões".
- "Quem responde" vem pronto em `quemRespondeResumo` ("3 pessoas escolhidas", "Todos os setores (101 pessoas)", "3 gestores (24 avaliações)", "Candidatos, por link", "Ainda não definido"): mostre o texto como vier. Os textos do PNG ("Turma LGPD 2026") são de quiz e não existem.
- **"Respostas" depende do tipo** (campos `respondidos`, `esperados`, `taxaDeRespostaPercentual`):
  - com nome e publicado: barra "respondidos de esperados" e a taxa;
  - rascunho sem resposta: "Nenhuma ainda";
  - por link (`alvo = LINK`): só "N respostas" (esperados e taxa vêm vazios; `respondidos` é o número de candidatos);
  - Sugestão (`OUTRO`): só a contagem de sugestões recebidas, sem barra (texto "N sugestões", suponho);
  - avaliação: a unidade é a **avaliação** (uma pessoa avaliando outra): "17 de 24", não "de 12 gestores". Na avaliação do gestor, `esperados` conta só quem tem gestor (setor sem gestor não entra);
  - **anônimo ainda não encerrado: sem número e sem barra**. A linha mostra "N esperados" e a situação, com o texto "resultado ao encerrar". O PNG mostra "58 de 101" na "Avaliação do gestor" (que é anônima no exemplo do contrato): isso **não** pode aparecer. Depois de `ENCERRADO` os números voltam exatos.
- **Prazo:** `prazo` vazio é "Sem prazo". Formulário encerrado mostra "Encerrou em dd/mm" com o dia de `encerradoEmUtc` em `America/Sao_Paulo`. Observação: o exemplo do contrato (formulário 6, prazo 30/09, `encerradoEmUtc` `2026-10-01T03:00:00Z`) dá **01/10** nessa conversão, e o PNG diz 30/09; o contrato não diz qual das duas datas a lista mostra. Use a de `encerradoEmUtc` e anote em `DUVIDAS.md` (só muda um dia na tela).
- **Menu "..." (o PNG só o mostra fechado):** o contrato dá **Encerrar** (só em `PUBLICADO`), **Excluir** (em qualquer situação) e **Ver resultado** (abre a página de resultado; em anônimo fica desabilitado até `ENCERRADO`).
- **"Editar" em formulário `ENCERRADO`:** o PNG mostra o botão, mas o contrato recusa (422 `FORMULARIO_ENCERRADO`). Deixe desabilitado com a dica "Formulário encerrado não muda" (suponho a dica). Em `PUBLICADO` anônimo o assistente abre com tudo travado menos título e descrição (ver tela 17).
- "Quem respondeu" não existe em rascunho (nada a mostrar) e, em formulário por link, abre a **lista de candidatos** no lugar da lista de pessoas (ver tela 21).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista, abas, busca e filtros (`tipo`, `situacao`, `busca`, `pagina`, `tamanhoPagina`, `ordenarPor` = `criadoEm`, `titulo` ou `prazo`, `direcao`) | `listarFormulariosRh` |
| "Novo formulário" abre o assistente (a rota só é chamada ao sair da etapa Detalhes) | `criarFormulario` |
| "Editar" carrega o formulário inteiro | `obterFormularioRh` |
| "Encerrar" (com `confirmar=true` na repetição) | `encerrarFormulario` |
| "Excluir" (com `confirmar=true` na repetição) | `excluirFormulario` |

**Estados**
- Carregando: esqueleto da tabela. Vazio: "Nenhum formulário ainda" com o botão "Novo formulário"; busca ou filtro sem resultado: "Nenhum formulário encontrado". Erro: mensagem do problema (`detail`) e "Tentar de novo". Sem permissão: 403 mostra a tela de acesso negado da fundação (rota só do RH).

**Textos e mensagens**
- Confirmar encerrar (409 `CONFIRMACAO_NECESSARIA` com `pendentes`): "N pessoas ainda não responderam. Encerrar mesmo assim?". Confirmar excluir: "N pessoas ainda não responderam. Excluir apaga as respostas. Excluir mesmo assim?" Ao confirmar, repita a chamada com `confirmar=true`.
- **Anônimo publicado: a confirmação vem sempre, sem número** (`pendentes` vazio, mesmo se todos já participaram). Escreva sem número: "Este formulário é anônimo: o sistema não diz quantas pessoas ainda não responderam. Encerrar mesmo assim?" (e o equivalente para excluir; o texto é meu, a regra é do contrato). Em exclusão, pergunte também em formulário que não pede `confirmar` (o contrato diz que apagar é sem volta).
- 422 `FORMULARIO_NAO_PUBLICADO` (encerrar o que não está publicado) e 422 `FORMULARIO_ENCERRADO`: mostre o `detail`.

**Anonimato nesta tela**
- R5: linha de formulário anônimo aberto mostra só `esperados`, sem número de respostas, sem faixa, sem barra. R3: "Ver resultado" desabilitado até encerrar. Encerrar e excluir pedem confirmação sempre e sem número. Nenhum texto desta tela diz quantas pessoas já participaram de um anônimo aberto.

**Observações para quem monta**
- A tabela passa de 1440 px no PNG; no front use rolagem horizontal só na tabela ou reduza larguras (o PNG é referência, a demo pode ser 1366 px).
- O gestor e o funcionário nunca veem esta tela (rota `/api/rh`).

**Perguntas abertas (`x-provisorio`)**
- `excluirFormulario`: o dono falou só de excluir formulário publicado; o contrato supõe que o encerrado também se exclui, sem confirmação. Se o dono proteger o encerrado, o item "Excluir" some da linha encerrada.
- `criarFormulario`: Sugestão anônima não existe (hipótese). Ver tela 17.

---

## Tela 17: Novo formulário, etapa Detalhes (RH)

PNG: `Figma/telas/17-rh-formularios-novo-formulario-etapa-detalhes.png`.

**Li no print**
- Janela "Novo formulário" com "X". Etapas no alto: **Detalhes** (ativa), Perguntas, Verificação, Publicação.
- Esquerda: "Detalhes"; "Título" (exemplo "Entrevista: auxiliar de logística"); "Descrição para quem vai responder" (texto de várias linhas); "Tipo de formulário" com **cinco cartões de escolha única**: Entrevista automática ("Candidatos de uma vaga"), Formulário de admissão ("Quem acabou de ser contratado"), Avaliação da equipe ("O gestor avalia cada pessoa"), Avaliação do gestor ("A equipe avalia o gestor"), **Quiz de treinamento** ("Fim de um treinamento").
- Direita: "Como o candidato vai ver" com o selo "Prévia": cartão com cabeçalho "[Nome da empresa]", selo do tipo, título, descrição, "Pergunta 1 de 4 25%", a primeira pergunta com as opções "Sim" e "Não" e o botão "Próxima"; nota "A prévia muda conforme você preenche."
- Rodapé: link "Salvar rascunho e sair" e botão "Próximo".

**O que fica**
- As quatro etapas, os campos Título (até 200 caracteres) e Descrição (até 1000, texto ou markdown, **nunca HTML**), os cartões de tipo, a prévia montada pelo front com o que a pessoa digitou (não há rota para ela), "Salvar rascunho e sair" e "Próximo".

**O que sai ou muda (e por quê)**
- **Sai "Quiz de treinamento"** (sem quiz). **Entram "Pesquisa" (`PESQUISA`) e "Sugestão" (`OUTRO`)**, que o spec prevê e o Figma não desenha (design, seção 7, "Tela 17"; contrato `criarFormulario`). São seis cartões. Os textos de apoio dos dois novos não existem nos documentos: use "Clima, satisfação ou outro tema" e "Caixa sempre aberta para ideias" (suponho; conferir na conferência visual).
- Os dois cartões de avaliação mandam `tipo = AVALIACAO` e `sentidoAvaliacao` = `GESTOR_AVALIA_EQUIPE` ("Avaliação da equipe") ou `EQUIPE_AVALIA_GESTOR` ("Avaliação do gestor"). `sentidoAvaliacao` é obrigatório na avaliação e proibido nos outros tipos.
- **Entra a caixa "Respostas anônimas"** (`anonimo`), que o Figma não tem (decisão de 2026-10-29: o RH marca ao criar). Texto de apoio do contrato: "o RH e o gestor veem só o resultado geral de cada pergunta, nunca quem respondeu". Ao lado, a tela pode listar as regras (público mínimo de 5 pessoas; anonimato, perguntas e destinatários não mudam depois de publicar; resultado só depois de encerrar; mínimo de 3 respostas por pergunta e, na avaliação, por avaliado). **Em Sugestão a caixa fica escondida** (ou desligada e desabilitada): Sugestão nunca é anônima (400 em `errors.anonimo`).
- O rótulo da prévia "Como o candidato vai ver" só serve à Entrevista; nos outros tipos use "Como quem responde vai ver" (suponho).
- O assistente é um só para criar e editar: a primeira saída da etapa 1 chama `criarFormulario` (cria **rascunho**; a API grava `ATIVO = falso` de propósito) e guarda o `id`; as seguintes chamam `editarFormulario`. O `id` é preciso para cadastrar as perguntas.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Primeira vez que sai da etapa (Próximo ou Salvar rascunho e sair) | `criarFormulario` |
| Saídas seguintes e edição de um formulário existente | `editarFormulario` |
| Abrir um formulário para editar | `obterFormularioRh` |

**Estados e regras de edição (contrato `editarFormulario`)**
- `RASCUNHO` e `AGENDADO`: tudo muda, inclusive `anonimo`.
- `PUBLICADO` **anônimo**: só título e descrição mudam, sempre, com ou sem resposta. Tipo, sentido e anonimato ficam travados (409 `ANONIMATO_FIXO_APOS_PUBLICAR`); mostre os campos desabilitados com a dica "Depois de publicado, o formulário não muda entre anônimo e identificado."
- `PUBLICADO` **com nome**: `anonimo` nunca muda. Sem resposta, o resto muda (menos tirar o tipo `ENTREVISTA` de quem está publicado por link: 422 `LINK_SO_PARA_ENTREVISTA`); com resposta (`temRespostas` verdadeiro), só título e descrição.
- `ENCERRADO`: nada muda (422 `FORMULARIO_ENCERRADO`); abra o assistente só para leitura.
- Passar para anônimo com pergunta de envio de arquivo ou com link público: 422 `ANONIMO_SEM_ARQUIVO` ou `ANONIMO_SEM_LINK`.
- Erros de campo (400, mapa `errors` por campo): mostre cada mensagem junto do campo (`errors.titulo`, `errors.sentidoAvaliacao`, `errors.anonimo`).

**Anonimato nesta tela**
- R2: depois de publicar, o anonimato e o tipo ficam travados; em anônimo publicado a etapa abre quase toda desabilitada. R1: a regra do público mínimo de 5 aparece como texto de apoio. R3 e R4: o texto de apoio avisa que o resultado só sai depois de encerrar e só das perguntas com 3 respostas ou mais. Sugestão não oferece a caixa. Anônimo não tem envio de arquivo nem link público (as recusas vêm das etapas 2 e 4).
- Não consulte `temRespostas` em anônimo: ele vem vazio de propósito (saber se há resposta revelaria o andamento).

**Observações para quem monta**
- O modal tem a mesma moldura nas quatro etapas (cabeçalho, indicador de etapas, corpo em duas colunas, prévia à direita, rodapé). Faça um componente só. Nas telas 18 e 19 o PNG empilha "Voltar" sobre "Próximo"; na tela 20 estão lado a lado: use **lado a lado** nas quatro.
- Data e hora sempre em `America/Sao_Paulo`; a API fala UTC.

**Perguntas abertas (`x-provisorio`)**
- `criarFormulario`: Sugestão anônima (hipótese: não pode). `FormularioTipo`: como as respostas do formulário de admissão chegam aos campos do cadastro (hipótese: não chegam; o RH lê as respostas e copia o que quiser). A tela não muda nos dois casos, exceto a caixa "Respostas anônimas" na Sugestão.

---

## Tela 18: Novo formulário, etapa Perguntas (RH)

PNG: `Figma/telas/18-rh-formularios-novo-formulario-etapa-perguntas.png`.

**Li no print**
- Etapa **Perguntas** ativa (Detalhes já marcada). Lista numerada de cartões, cada um com tipo, texto e botão "Editar":
  1. "Múltipla escolha" com o selo **"Eliminatória"**, "Você tem disponibilidade para trabalhar aos sábados?" e as opções "Sim" e "Não".
  2. "Resposta longa" com o selo **"Nota dada pelo RH"**, "Conte sobre uma vez em que você precisou organizar um estoque com prazo curto."
  3. **"Quiz, 2 pontos"**, "Qual é a forma correta de levantar uma caixa pesada do chão?", com **"Correta: Dobrar os joelhos e manter a coluna reta"** destacada e outras duas opções.
  4. "Envio de arquivo", "Envie seu currículo (PDF ou Word, até 5 MB)."
- Caixa tracejada "Adicionar pergunta:" com os botões Resposta curta, Resposta longa, Múltipla escolha, **Quiz**, Escala de 1 a 5 e Envio de arquivo. Prévia à direita, rodapé com "Salvar rascunho e sair", "Voltar" e "Próximo".

**O que fica**
- Lista numerada de perguntas, tipo e texto em cada cartão, opções como etiquetas, "Editar", os cinco botões de adicionar (curta, longa, múltipla escolha, escala, arquivo) e a prévia.

**O que sai ou muda (e por quê)**
- **Saem** o selo "Eliminatória", o selo "Nota dada pelo RH", o cartão "Quiz, 2 pontos" com "Correta:" e o botão "Quiz" (cortados em 2026-10-29: sem quiz, sem resposta certa, sem pontos, sem eliminatória, sem nota). O contrato (`PerguntaComando`) não tem campo para nada disso.
- Tipos (`PerguntaTipo`): `RESPOSTA_CURTA` (até 200 caracteres), `RESPOSTA_LONGA` (até 4000), `MULTIPLA_ESCOLHA` (de 2 a 10 alternativas, **escolha de uma só**), `ESCALA` (1 a 5) e `ARQUIVO` (PDF ou Word, até 5 MB). Texto da pergunta até 500 caracteres, texto ou markdown, nunca HTML.
- **O editor de pergunta não está desenhado** ("Editar" só aparece como botão). Descrição em texto: painel ou formulário no próprio modal com o texto da pergunta, o tipo (troque só em pergunta nova; editar troca o conteúdo inteiro), a caixa "Obrigatória", a lista de alternativas (adicionar, remover, as que vêm com `id` mantêm o `id`, as sem `id` são novas) e, em edição, a posição (`ordem`). Também falta o "Apagar pergunta": ponha dentro do editor.
- **Mover pergunta** não está desenhado; o contrato move pela `ordem` (as outras se ajustam). Ofereça "Subir" e "Descer" (suponho).
- Em formulário **anônimo**, o botão "Envio de arquivo" fica desabilitado com a dica "Formulário anônimo não tem envio de arquivo" (422 `ANONIMO_SEM_ARQUIVO`).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Adicionar pergunta (com `ordem` opcional) | `adicionarPerguntaFormulario` |
| Salvar o editor / mover | `editarPerguntaFormulario` |
| Apagar | `excluirPerguntaFormulario` |
| Recarregar a lista depois de cada mudança | `obterFormularioRh` |

**Estados e regras por situação**
- `RASCUNHO` e `AGENDADO`: tudo livre.
- `PUBLICADO` **com nome**: pergunta nova ainda entra; pergunta que **já tem resposta** só muda `obrigatoria` e `ordem` (422 `PERGUNTA_JA_RESPONDIDA`; mostre o `detail`: "Esta pergunta já foi respondida. Só dá para mudar se ela é obrigatória e a posição dela.").
- `PUBLICADO` **anônimo**: **tudo congelado**, inclusive `obrigatoria` e `ordem` (409 `ANONIMO_PERGUNTAS_FIXAS`). Mostre a etapa só para leitura com "As perguntas de um formulário anônimo publicado não mudam." O erro `PERGUNTA_JA_RESPONDIDA` **nunca** sai num anônimo.
- `ENCERRADO`: só leitura (422 `FORMULARIO_ENCERRADO`).
- Erros de campo (400): `errors.alternativas` (menos de 2 em múltipla escolha, ou alternativas em outro tipo), `errors.texto`.

**Anonimato nesta tela**
- R2: em anônimo publicado nenhuma mudança de pergunta é possível, e a tela **não pode tentar descobrir** se há resposta (nem pelo erro: a API só responde 409 `ANONIMO_PERGUNTAS_FIXAS`). Sem pergunta de arquivo. Sem nenhuma dica do tipo "esta pergunta já foi respondida" em anônimo.

**Observações para quem monta**
- A prévia mostra só a primeira pergunta e o "Pergunta 1 de N", com a barra em N passos; é montada no front.
- Múltipla escolha é de **uma opção** (`RespostaFormularioItem.alternativaIds` com `maxItems` 1): desenhe como botões de opção (radio) na prévia.

**Perguntas abertas (`x-provisorio`)**
- `RespostaFormularioItem.alternativaIds`: se a múltipla escolha aceita mais de uma opção (hipótese: uma só). Se mudar, a pergunta ganha o campo `permiteVariasOpcoes` e a prévia muda para caixas de marcar.

---

## Tela 19: Novo formulário, etapa Verificação (RH)

PNG: `Figma/telas/19-rh-formularios-novo-formulario-etapa-verificacao.png`.

**Li no print**
- Etapa **Verificação** ativa. Texto: "O sistema confere o formulário antes de publicar. Os itens em laranja não impedem a publicação."
- Quatro linhas verdes com "check": "Título e descrição preenchidos", "4 perguntas, 3 obrigatórias", **"A pergunta de quiz tem a resposta certa marcada e vale 2 pontos"**, "Nenhuma pergunta pede dado sensível, como saúde, religião ou orientação sexual". Uma linha laranja com "!": **"A pergunta 2 é de resposta longa: alguém do RH vai precisar dar a nota de cada candidato"**. Prévia, rodapé "Salvar rascunho e sair", "Voltar", "Próximo".

**O que fica**
- A frase de abertura, a lista de conferências com ícone verde (OK) e laranja (AVISO), a prévia e os botões.

**O que sai ou muda (e por quê)**
- **Saem as duas linhas de quiz e de nota** (sem quiz e sem nota). O resto da lista vem da API (`verificarFormulario`), na ordem recebida, com estes `codigo`: `TITULO_E_DESCRICAO`, `PERGUNTAS`, `ALTERNATIVAS`, `ANONIMO_SEM_ARQUIVO`, `ANONIMO_RESULTADO_MINIMO`, `ANONIMO_PUBLICO_MINIMO`, `DADO_SENSIVEL`. Mostre a `mensagem` como vier.
- **Três situações, o Figma só tem duas:** `OK` (verde), `AVISO` (laranja, não impede) e `BLOQUEIO` (impede: `podePublicar` falso e a etapa seguinte não deixa publicar; 422 `VERIFICACAO_COM_BLOQUEIO`). Use a cor de erro da paleta para o bloqueio (suponho). Com `podePublicar` falso, "Próximo" fica desabilitado e a lista diz o que corrigir. Item com `perguntaId` pode levar à pergunta na etapa 2 (suponho).
- **Ordem das etapas:** a Verificação vem **antes** da Publicação, mas o público mínimo do anônimo depende do público escolhido na etapa 4. O contrato confere o público **já guardado** por `salvarPublicacaoFormulario`: `OK` com "N pessoas", `BLOQUEIO` abaixo de 5, ou `AVISO` "escolha pelo menos 5 pessoas na etapa Publicação" se ainda não foi escolhido. Quem volta da etapa 4 para a 3 deve guardar a publicação antes (chame `salvarPublicacaoFormulario`; suponho, para a conferência enxergar o público).
- `DADO_SENSIVEL` é um `AVISO` (nunca bloqueio): a API procura palavras-chave no texto das perguntas (hipótese do contrato).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Carrega a conferência ao entrar na etapa e de novo a cada volta | `verificarFormulario` |
| Guarda a publicação antes de voltar da etapa 4 (suponho) | `salvarPublicacaoFormulario` |

**Estados**
- Carregando: quatro linhas de esqueleto. Erro: 404 (formulário não existe) e 500 com `detail`. Sem permissão: 403.

**Anonimato nesta tela**
- Em formulário anônimo aparecem três itens a mais: `ANONIMO_SEM_ARQUIVO` (BLOQUEIO se há pergunta de arquivo), `ANONIMO_RESULTADO_MINIMO` (AVISO fixo: "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais", e na avaliação só dos avaliados com 3 ou mais) e `ANONIMO_PUBLICO_MINIMO` (R1). Mostre-os como a API manda; não os esconda.

**Perguntas abertas (`x-provisorio`)**
- `verificarFormulario`: como a verificação de dado sensível funciona (hipótese: palavras-chave, sempre AVISO). Só muda a lista de itens, não o formato.

---

## Tela 20: Novo formulário, etapa Publicação (RH)

PNG: `Figma/telas/20-rh-formularios-novo-formulario-etapa-publicacao.png`.

**Li no print**
- Etapa **Publicação** ativa. "Quem responde" com três cartões: **Candidatos, por link** ("Qualquer pessoa com o link responde", escolhido), **Setores** ("Todos de um ou mais setores"), **Pessoas escolhidas** ("Você marca quem responde").
- "Quando publicar": "Publicar agora" (escolhido) e "Agendar para uma data".
- "Aceitar respostas até" com data (exibida como 10/31/2026) e, ao lado, **"Nota mínima para seguir"** com "7 de 10 pontos".
- Duas caixas marcadas: **"Enviar o resultado por e-mail ao RH"** e **"Convidar aprovados para a entrevista"**.
- Prévia à direita com a legenda "Aceita respostas até 31/10. O link fica disponível depois de publicar." Rodapé: "Salvar rascunho e sair", "Voltar", "Publicar formulário".

**O que fica**
- Os três cartões de "Quem responde", "Publicar agora" e "Agendar para uma data", o campo "Aceitar respostas até", a legenda da prévia, "Salvar rascunho e sair", "Voltar" e "Publicar formulário".

**O que sai ou muda (e por quê)**
- **Saem as três coisas abaixo** (decisão do dono, 2026-10-30; contrato `publicarFormulario` e `FormularioPublicacaoComando`): **"Nota mínima para seguir"** (cortada, sem nota); **"Enviar o resultado por e-mail ao RH"** e **"Convidar aprovados para a entrevista"** (não existem; a lista de e-mails do sistema é fechada, `contrato/CONVENCOES.md`, seção 12). O design (seção 9) ainda lista as duas caixas como "em aberto": **vale o contrato**, que registra a decisão. Também não existe aviso de "formulário publicado".
- **Entram duas coisas que o Figma não desenha:**
  - Caixa **"Lembrar quem não respondeu 7 dias antes do prazo"** (`lembreteAutomatico`), **ligada por padrão**. Só tem efeito com prazo e com `SETORES` ou `PESSOAS`: esconda-a nos outros casos. Se o prazo está a 7 dias ou menos do dia da publicação, não há lembrete (sem erro).
  - Bloco opcional **"Repetir"** (`recorrencia`: intervalo de 1 a 365, unidade dia, semana ou mês, e "até" opcional), com o aviso "Ao reabrir, as respostas da rodada anterior são apagadas. Baixe as respostas antes." e um atalho para "Baixar respostas". Em Sugestão fica escondido (Sugestão é sempre aberta).
- **Escolha de setores e de pessoas não está desenhada.** Descrição em texto: com "Setores", lista de setores com caixa de marcar (de `listarSetores`); com "Pessoas escolhidas", busca de pessoas (de `listarFuncionarios`) com as escolhidas como etiquetas removíveis. Na **avaliação da equipe** (`GESTOR_AVALIA_EQUIPE`) as pessoas escolhidas são os **gestores** (lista de `listarGestores`; suponho que é a fonte certa). Na **avaliação do gestor** (`EQUIPE_AVALIA_GESTOR`) as pessoas ou o setor escolhidos são **quem avalia**.
- **Quem pode ter link:** `LINK` só para `ENTREVISTA` (422 `LINK_SO_PARA_ENTREVISTA`) e **nunca** em anônimo (422 `ANONIMO_SEM_LINK`). Desabilite o cartão "Candidatos, por link" nos outros casos, com a dica do motivo.
- **Sugestão (`OUTRO`) é sempre aberta:** esconda "Aceitar respostas até" e "Repetir" (a API recusa `prazo` e `recorrencia` com 400).
- **Datas em pt-BR (dd/mm/aaaa):** o PNG mostra 10/31/2026 por causa do navegador. `prazo` não pode ser anterior a hoje nem a `agendarPara`; `agendarPara` não pode ser no passado.
- **"Agendar" e "Salvar rascunho e sair":** num rascunho o `agendarPara` **não é guardado** (o banco só o guarda em `AGENDADO`): `salvarPublicacaoFormulario` devolve `agendarPara` vazio. Depois de salvar com data de agendamento digitada, avise "A data de agendamento só é guardada ao publicar. Escolha de novo ao voltar." (texto meu) e peça a data de novo ao publicar.
- **Link depois de publicar:** com `alvo = LINK` o servidor gera `publicacao.tokenDoLink`, mesmo no agendamento. Monte o link completo com o endereço do site e o caminho da página pública (`/formulario/:token`) e mostre-o com o botão "Copiar link" na confirmação de publicação e no cabeçalho da lista de candidatos (a segunda é suposição).
- **Publicar não manda e-mail** (setores ou pessoas): as pessoas passam a ver o formulário em "Formulários". Escreva "Ninguém recebe e-mail ao publicar. Se quiser avisar, publique um Aviso." (texto meu, a regra é do contrato).
- **Contagem de destinatários:** não há rota que conte o público de um formulário. Não calcule o número no front; o mínimo de 5 do anônimo vem da API (verificação e 422).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| "Salvar rascunho e sair" e guardar ao voltar (mesmo corpo de publicar, sem publicar) | `salvarPublicacaoFormulario` |
| "Publicar formulário" (publica agora ou agenda) | `publicarFormulario` |
| Conferir bloqueios ao publicar | `verificarFormulario` (a API repete por dentro) |
| Escolher setores, pessoas e gestores | `listarSetores`, `listarFuncionarios`, `listarGestores` (fatia 1) |
| Abrir o formulário salvo | `obterFormularioRh` |

**Estados e erros**
- 400 por campo (`setorIds`, `funcionarioIds` conforme o `alvo`, `prazo`, `agendarPara`, `recorrencia`). 422 (`FormularioErro422Publicacao`): `FORMULARIO_ANONIMO_PUBLICO_PEQUENO` ("Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para N."), `VERIFICACAO_COM_BLOQUEIO` ("O formulário tem itens a corrigir antes de publicar. Veja a etapa Verificação."), `LINK_SO_PARA_ENTREVISTA`, `ANONIMO_SEM_LINK`, `FORMULARIO_JA_PUBLICADO`, `FORMULARIO_ENCERRADO`, `ANONIMO_SEM_ARQUIVO`, `FUNCIONARIO_INATIVO`. Mostre o `detail`.

**Anonimato nesta tela**
- R1: o anônimo precisa de 5 ou mais pessoas ao publicar (e na reabertura). R2: o público é uma fotografia do dia da publicação (quem for admitido depois não entra; diga isso em texto de apoio quando `anonimo` for verdadeiro). Sem link público. Recorrente anônimo só reabre **depois de encerrado** e reconfere o mínimo de 5 a cada vez; se ficarem menos de 5, não reabre e a recorrência é cancelada. O lembrete automático de um anônimo vai a quem ainda não participou, sem guardar destinatário e sem número.

**Perguntas abertas (`x-provisorio`)**
- `FormularioRecorrencia`: o prazo da rodada nova ao reabrir (hipótese: avança o mesmo número de dias da rodada anterior) e o controle na tela. `obterFormularioPublico` e `responderFormularioPublico`: proteção do link (hipótese: só limite por IP) e retenção dos dados de candidato (180 dias). A tela desta etapa não muda nos dois casos.

---

## Tela 21: Quem respondeu (RH)

PNG: `Figma/telas/21-rh-formularios-quem-respondeu.png`.

**Li no print**
- Trilha "Formulários / Quem respondeu" e botão "X". Título "Admissão: outubro de 2026"; linha "Formulário de admissão, criado por Sofia Ramos em 25/09/2026"; selo "Publicado". Botões "Baixar respostas" e "Lembrar quem falta por e-mail".
- Três cartões: Responderam 2, Faltam responder 1, Taxa de resposta 67%.
- Abas Todos, Responderam, Faltam; busca "Buscar pessoa"; seletor "Todos os setores".
- Tabela: Pessoa (iniciais, nome), Setor, Situação (selos "Respondido" e "Não respondeu"), Respondido em ("01/10", "02/10", "—") e a ação: "Ver respostas" em quem respondeu, "Lembrar" em quem não respondeu. Rodapé "Mostrando 3 de 3".

**O que fica**
- Trilha, cabeçalho, os dois botões do alto, os três cartões (que **não mudam com os filtros**), as abas, a busca, o filtro de setor, as colunas e as ações por linha, "Mostrando N de M".

**O que sai ou muda (e por quê)**
- O cabeçalho (título, "criado por ... em ...", situação) vem de `obterFormularioRh`. O PNG mostra pessoas e números de exemplo (2 de 3); nos dados simulados o formulário 11 tem 3 esperados e ninguém respondeu.
- **Avaliações:** cada linha é uma avaliação ("fulano avaliou beltrano"): acrescente a coluna "Avaliado" (`avaliado`; suponho que a coluna é a forma de mostrar).
- **"Ver resultado" (botão novo)**, ao lado de "Baixar respostas": leva à página de resultado (ver "Tela sem print: Resultado"). Em anônimo fica desabilitado até `ENCERRADO`.
- **Formulário por link (`alvo = LINK`)**: a página mostra a **lista de candidatos** (`listarCandidatosFormulario`) no lugar desta: colunas nome, e-mail, telefone, "Currículo" (ícone ou "Baixar currículo" se `temCurriculo`), "Respondido em" (mais recentes primeiro), ação "Ver respostas". **Sem "faltam", sem taxa e sem "Lembrar"** (qualquer pessoa com o link pode responder). Busca no nome e no e-mail. Dado de candidato é pessoal (LGPD): só o RH, e some no prazo de retenção. Chamar a lista errada dá 422 `FORMULARIO_POR_LINK` ou `FORMULARIO_SEM_LINK`: decida pela `publicacao.alvo` de `obterFormularioRh` antes.
- **Sugestão (`OUTRO`)**: cada linha é **um envio**; a mesma pessoa pode aparecer mais de uma vez, e o `respondidoEmUtc` (mostre **data e hora**) é o que separa um envio do outro. Só um cartão ("Sugestões recebidas" = `resumo.respondidos`); sem "Faltam", sem taxa, sem "Lembrar", sem linha "Não respondeu". "Ver respostas" manda o `respondidoEmUtc` da linha.
- **"Lembrar" por linha** só aparece em quem não respondeu; "Ver respostas" só quando `podeVerRespostas`.
- **"Lembrar quem falta por e-mail"** é o lembrete manual do RH (um dos e-mails da lista fechada). Só em formulário `PUBLICADO` (422 `FORMULARIO_NAO_PUBLICADO` nos outros). Resposta com nome: "Lembrete enviado a N pessoas" e, se `ignorados` for maior que zero, "M já tinham respondido ou receberam um lembrete hoje" (no máximo um por pessoa, por formulário, por dia).
- "Respondido em" mostra só dia e mês (como no PNG); acrescente o ano se a pessoa respondeu em ano diferente do atual (suponho).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Cabeçalho | `obterFormularioRh` |
| Cartões, abas (`situacao` = `RESPONDIDO` ou `NAO_RESPONDEU`), busca, setor (`setorId`), páginas | `listarRespondentesFormulario` |
| Lista de candidatos (formulário por link) | `listarCandidatosFormulario` |
| "Baixar respostas" (CSV) | `exportarRespostasFormulario` |
| "Lembrar quem falta" e "Lembrar" de uma linha | `lembrarQuemFaltaFormulario` |
| "Ver respostas" de uma pessoa ou de um candidato | `obterRespostasDeFuncionario`, `obterRespostasDeCandidato` |
| Arquivo e currículo | `baixarArquivoDeResposta`, `baixarCurriculoDeCandidato` |
| Filtro "Todos os setores" | `listarSetores` (fatia 1) |

**Estados**
- Carregando, lista vazia na aba ("Ninguém respondeu ainda" / "Ninguém falta responder"), erro com `detail`, 403 sem permissão. Cartões com valor vazio mostram "—" ou o texto "disponível ao encerrar" (anônimo).

**Anonimato nesta tela** (a mais afetada)
- **Anônimo: só os três cartões.** `anonimo` vem verdadeiro e `itens` vem sempre vazio: **esconda abas, busca, filtro de setor, tabela e "Ver respostas"** (a API ignora `busca`, `setorId` e `situacao`; a contagem é sempre geral, nunca por setor).
- **Antes de `ENCERRADO` os cartões não têm contagem** (R5): `resumo.respondidos`, `faltam` e `taxaDeRespostaPercentual` vêm vazios; só `esperados` é exato. Mostre um cartão "Esperados" com o número e escreva "disponível ao encerrar" nos cartões Responderam, Faltam e Taxa. Depois de encerrado os três vêm exatos.
- **"Lembrar quem falta por e-mail" continua** no anônimo, mas só sem escolher pessoas (`funcionarioIds` vazio; mandar uma pessoa é 422 `FORMULARIO_ANONIMO`). A resposta **só diz que o lembrete saiu** (`anonimo` verdadeiro, `enviados` e `ignorados` vazios): escreva "Lembrete enviado." e **nunca** número nem "ninguém precisava". É a mesma resposta com muita gente, pouca, nenhuma ou se o de hoje já tinha saído. Limite de um lembrete do formulário por dia.
- "Baixar respostas" em anônimo só depois de encerrado (409 `RESULTADO_ANONIMO_INDISPONIVEL`; 422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES` se nenhuma pergunta chega a 3). Desabilite o botão com a dica "Só depois de encerrar".

**Observações para quem monta**
- Para downloads de arquivo (`baixarArquivoDeResposta`, `baixarCurriculoDeCandidato`) a API devolve `LinkTemporario` (`url`, `expiraEmUtc`); abra o link em nova aba com `abrirLinkTemporario(link)` (fundação 1a), nunca incorpore.
- O CSV vem direto da API (os bytes) com `Content-Disposition: attachment` (separador `;`, UTF-8 com BOM; `contrato/CONVENCOES.md`, seção 4); use `baixarExportacao(chamada, nomePadrao)` da fundação.

**Perguntas abertas (`x-provisorio`)**
- `listarCandidatosFormulario` e `obterFormularioPublico`: retenção dos dados do candidato (180 dias, valor de partida). `lembrarQuemFaltaFormulario` não tem marca; o lembrete do gestor (tela 57) tem.

---

## Tela 39: Meus formulários, abas Todos, Para responder e Disponíveis (funcionário; também gestor e RH)

PNG: `Figma/telas/39-func-formularios.png`.

**Li no print**
- Título "Formulários"; subtítulo "Pedidos do RH e formulários abertos para você responder". Abas com número: **Todos 3**, Para responder 1, Disponíveis 2, Respondidos 4. Busca "Buscar formulário" e seletor "Todos os tipos".
- Três cartões com faixa colorida, ícone e selo do tipo: **"Avaliação do gestor"** (ícone de pessoas; etiqueta "Para responder"; título "Avaliação da sua gestora, ciclo 2026.2"; descrição "Conte como foi trabalhar com Marina Costa neste semestre."; "12 perguntas", "10 min", "Até 16/10" em laranja; barra 0%; botão "Responder →"); **"Pesquisa"** (ícone de balão; "Disponível"; "Pesquisa de clima 2026"; descrição "Como você se sente trabalhando aqui. **O RH vê os resultados agrupados por setor.**"; "20 perguntas", "8 min", "Aberta até 31/10"); **"Sugestão"** (ícone de lâmpada; "Disponível"; "Sugestões para o RH"; "Mande uma ideia, uma dúvida ou um pedido para o RH quando quiser."; "3 perguntas", "2 min", "Sempre aberto"). Nota no pé: "Os formulários que você já respondeu ficam na aba Respondidos."
- Menu do funcionário com a bolinha "Formulários 1".

**O que fica**
- Abas, busca, filtro de tipo, o desenho dos cartões (faixa, ícone, selo do tipo, etiqueta de situação, título, descrição, "N perguntas", "N min", prazo, barra, botão), a nota do pé. O desenho do cartão (ícone e cor) é decorativo: escolha por tipo (suposição de ícones: pessoas, balão, lâmpada, documento).

**O que sai ou muda (e por quê)**
- **Sai a frase "O RH vê os resultados agrupados por setor"** (decisão do dono, 2026-10-30; design, seção 7, "Tela 39"): a resposta anônima não guarda funcionário nem setor (`CHK_FORMULARIOS_RESPOSTAS_ANONIMA_SEM_AUTOR`, tabela `FORMULARIOS_RESPOSTAS` do `Banco de Dados/Scripts/V001`), então agrupar por setor é impossível. No formulário anônimo a descrição que o RH escreveu diz que ele vê só o resultado geral ("O RH vê só o resultado geral, sem saber quem respondeu."). **Mostre `descricao` como vier**; não escreva nenhuma frase fixa sobre o que o RH vê.
- Sem "Quiz de treinamento", sem nota. O filtro "Todos os tipos" oferece Formulário de admissão, Avaliação, Pesquisa e Sugestão (`tipo`; sem Entrevista, porque formulário por link nunca aparece aqui; suponho que o filtro omite a Entrevista).
- **Abas e contagens:** "Todos" não manda `situacao` e traz `PARA_RESPONDER` e `DISPONIVEL` (os respondidos ficam de fora); os quatro números vêm de `contagens` e respeitam `busca` e `tipo`. No PNG "Respondidos 4" tem dois quizzes; no contrato de exemplo são 2.
- **Prazo no cartão:** `prazo` vazio é "Sempre aberto"; com prazo, "Até dd/mm" (laranja quando a etiqueta é "Para responder") ou "Aberta até dd/mm". Não existe "atrasado".
- **Barra e botão:** `respondidos` de `total`. `total` é 1, exceto em `GESTOR_AVALIA_EQUIPE`, em que é o número de pessoas que o gestor deve avaliar: o botão vira "Continuar" e o cartão alimenta a faixa "Sua parte" da tela 57.
- **O que separa "Para responder" de "Disponíveis" (`FormularioMinhaSituacao`, `x-provisorio`):** hipótese do contrato, só pelo tipo: `ADMISSAO` e `AVALIACAO` são "Para responder"; `PESQUISA` e `OUTRO` (Sugestão) são "Disponíveis". A API já manda a etiqueta pronta em `situacao`; o front só a mostra.
- **Sugestão** fica **sempre** em "Disponível": nunca vai para Respondidos, mostra 0 de 1, "Sempre aberto", e o botão "Responder" continua valendo (a pessoa pode mandar outra).
- Etiquetas de tipo: `ENTREVISTA` "Entrevista automática", `ADMISSAO` "Formulário de admissão", `AVALIACAO` + `GESTOR_AVALIA_EQUIPE` "Avaliação da equipe", `AVALIACAO` + `EQUIPE_AVALIA_GESTOR` "Avaliação do gestor", `PESQUISA` "Pesquisa", `OUTRO` "Sugestão".

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Cartões, abas (`situacao`), busca (`busca`), `tipo`, páginas, contagens | `listarMeusFormularios` |
| "Responder" ou "Continuar" abre a tela de responder | `obterFormularioParaResponder` (ver "Tela sem print: Responder") |
| Bolinha "Formulários" do menu (`formulariosPendentes`, igual a `contagens.paraResponder`) | `obterContadoresDoMenuDoFuncionario` (a fundação 1a já a lê); chame `invalidarContadoresDoMenu` depois de responder |

**Estados**
- Carregando (três cartões de esqueleto). Vazio por aba: "Você não tem formulários para responder agora" (Para responder), "Nenhum formulário aberto no momento" (Disponíveis), "Você ainda não respondeu nenhum formulário" (Respondidos) (textos meus). Erro com `detail`. A rota vale para os três perfis (o gestor e o RH também respondem): a API não devolve 403.

**Anonimato nesta tela**
- Cartão de formulário anônimo: `respondidoEmUtc` vem vazio e `podeVerMinhasRespostas` falso (sem data, sem "Ver minhas respostas"; ver tela 40). Não acrescente selo, texto fixo nem contagem sobre quem já respondeu. A descrição vem do RH; o aviso "o RH e o gestor não veem quem respondeu o quê" fica na tela de responder.

**Observações para quem monta**
- **Menu do RH e do gestor:** os menus do RH (PNG 16) e do gestor (PNGs 57 e 58) **não têm** o item "Formulários" do funcionário, mas a rota serve aos três perfis e o contrato diz que gestor e RH também respondem. **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Meus formulários"** existe no grupo **"Meu espaço"** do RH e do gestor e leva a `/eu/formularios` (a página serve aos três perfis; o gestor chega também pela faixa "Sua parte" da tela 57).

---

## Tela 40: Meus formulários, aba Respondidos (funcionário; também gestor e RH)

PNG: `Figma/telas/40-func-formularios-aba-respondidos.png`.

**Li no print**
- Mesma tela, aba **Respondidos 4** ativa. Quatro cartões: "Quiz: LGPD na prática" ("Sua nota foi 9,0"; "Respondido em 15/09/2026"), "Avaliação da sua gestora, ciclo 2026.1" ("Respondido em 22/04/2026"), "Quiz: código de conduta 2025" ("Sua nota foi 10"), "Admissão" ("Dados e documentos enviados quando você entrou na empresa."; "18 perguntas", "15 min", "Respondido em 10/03/2022"). Cada um com etiqueta "Respondido", barra em 100% e botão "Ver minhas respostas". Nota no pé: "Mostrando os formulários que você já respondeu."

**O que fica**
- A aba, os cartões como na tela 39 com etiqueta "Respondido", "Respondido em dd/mm/aaaa" (dia de `respondidoEmUtc` em `America/Sao_Paulo`), barra 100%, "Ver minhas respostas" e a nota do pé.

**O que sai ou muda (e por quê)**
- **Saem os dois quizzes e "Sua nota foi ...".**
- **Formulário anônimo:** o cartão **não mostra data e não tem "Ver minhas respostas"** (a participação anônima não guarda data e a resposta não é da pessoa para o sistema). O PNG mostra "Respondido em 22/04/2026" e o botão na avaliação do gestor 2026.1, que no exemplo do contrato é anônima: **os dois somem**. O cartão fica só com a etiqueta "Respondido" e a barra.
- **Sugestão** nunca aparece aqui.
- `podeVerMinhasRespostas` decide o botão; no contrato de exemplo só a Admissão o tem. Contagem da aba: `contagens.respondidos` (2 no exemplo, 4 no PNG por causa dos quizzes).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista da aba (`situacao=RESPONDIDO`) | `listarMeusFormularios` |
| "Ver minhas respostas" | `obterMinhasRespostas` (ver "Tela sem print: Minhas respostas") |

**Estados**
- Vazio: "Você ainda não respondeu nenhum formulário". Carregando e erro como na tela 39.

**Anonimato nesta tela**
- Sem data e sem botão em formulário anônimo, como acima. Nada de "Respondido em" que pudesse cruzar com outro dado.

**Perguntas abertas (`x-provisorio`)**
- Mesma de `FormularioMinhaSituacao` (tela 39).

---

## Tela 57: Formulários do setor (gestor)

PNG: `Figma/telas/57-gestor-formularios-da-equipe.png`.

**Li no print**
- Título "Formulários do setor"; subtítulo "Veja quem do setor Comercial já respondeu o que o RH pediu". Menu do gestor com "Seu dia a dia" (Início, Meu ponto, Férias, Holerites), "Seu setor" (Painel da equipe 2, Ponto da equipe, **Formulários** ativo, Treinamentos, Advertências) e "Comunicação" (Avisos 2).
- Faixa laranja **"Sua parte: avaliação da equipe, ciclo 2026.2"**, "Você avaliou 3 de 8 pessoas. O prazo é 23/10." e botão "Continuar avaliação".
- Coluna esquerda "Pedidos do RH" com três cartões: "Avaliação do gestor" / "Avaliação do gestor, ciclo 2026.2" ("5 de 8 responderam", "Até 16/10", **selecionado**); "Quiz de treinamento" / "Quiz: LGPD na prática" ("6 de 8 responderam", "Sem prazo"); "Quiz de treinamento" / "Quiz: segurança da informação" ("3 de 8 responderam", "Até 15/11"). Cada um com barra.
- Painel direito "Quem respondeu" com o título do formulário selecionado, a frase "Você vê só quem já respondeu. As respostas ficam com o RH.", botão "Lembrar quem falta por e-mail", cartões "Responderam 5" e "Faltam 3" e a lista: avatar, nome, cargo (e "Vendedora, de férias até 17/10"), "em 02/10" e selo "Respondido" ou "Não respondeu".

**O que fica**
- Título, faixa "Sua parte" (quando existir), a coluna "Pedidos do RH" com seleção, o painel "Quem respondeu" com os dois cartões e a lista, a frase "Você vê só quem já respondeu. As respostas ficam com o RH.".

**O que sai ou muda (e por quê)**
- **Saem os dois quizzes** (sem quiz).
- **A "Avaliação do gestor, ciclo 2026.2" não aparece para o gestor** (contrato, descrição da tag e de `listarFormulariosDaEquipe`): na avaliação do gestor (`EQUIPE_AVALIA_GESTOR`) ele é o **avaliado**, e quem é avaliado pela equipe **não acompanha o andamento nem lembra ninguém**; as rotas `/api/gestor/formularios...` devolvem 404 para ele. O PNG a mostra selecionada: isso diverge do contrato e **vale o contrato**.
- Não entram na lista: formulário por link, **Sugestão**, avaliação da equipe (`GESTOR_AVALIA_EQUIPE`; nela quem responde é o próprio gestor, e ela vira a faixa "Sua parte") e formulário encerrado. Lista curta, sem paginação, pelo prazo mais próximo e depois pelo título; `prazo` vazio é "Sem prazo".
- **Faixa "Sua parte"** é montada pelo front com o item `AVALIACAO` + `GESTOR_AVALIA_EQUIPE` de `listarMeusFormularios`: "Você avaliou `respondidos` de `total` pessoas. O prazo é dd/mm." e o botão "Continuar avaliação" leva à tela de responder. Se não há esse item, a faixa não aparece. Um formulário desse tipo por ciclo (se houver dois, uma faixa para cada; suponho).
- **Lista de pessoas:** só quem é do setor do gestor (sem o gestor), em ordem de nome (a ordem do PNG não é a do contrato). A observação "de férias até 17/10" vem em `observacao` (de férias aprovadas que cobrem hoje; `null` se não há). Só **quem**, nunca o conteúdo.
- **"Lembrar quem falta por e-mail": decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D5-4`): o botão **fica**; o e-mail entrou na lista fechada (`CONVENCOES.md`, seção 12, contrato 0.2.1) e a rota `lembrarQuemFaltaNaEquipe` **não é mais provisória**. Construa normalmente. O botão não manda corpo e vale só para pessoas do setor do gestor. O "Lembrar Diego por e-mail" da tela 51 é outro botão e está cortado.
- Os cartões têm **Responderam** e **Faltam**; a taxa vem em `taxaDeRespostaPercentual` e a barra do cartão da esquerda usa `respondidos` de `esperados`.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Coluna "Pedidos do RH" | `listarFormulariosDaEquipe` |
| Painel "Quem respondeu" do cartão selecionado | `listarRespondentesDaEquipe` |
| "Lembrar quem falta por e-mail" | `lembrarQuemFaltaNaEquipe` |
| Faixa "Sua parte" e botão "Continuar avaliação" | `listarMeusFormularios`, depois `obterFormularioParaResponder` |
| Nome do setor no subtítulo | `obterUsuarioLogado` (traz `setor`; fatia 0) |

**Estados**
- Carregando. Lista vazia: "Nenhum pedido do RH aberto para o seu setor". Painel sem cartão escolhido: selecione o primeiro automaticamente. 404 do painel (formulário fora da equipe, avaliação do gestor): volte para a lista com o aviso do `detail`. 403: só gestor.

**Textos e mensagens**
- Lembrete com nome: "Lembrete enviado a N pessoas da sua equipe" (`enviados`; `ignorados` já responderam ou já receberam hoje). Anônimo: só "Lembrete enviado." Limite de um por pessoa, por formulário, por dia (no anônimo, um do formulário por dia e por alcance, o setor do gestor).

**Anonimato nesta tela**
- **Anônimo ainda aberto:** o gestor vê só `esperados` (R5): no cartão da esquerda escreva "N esperados" e "resultado ao encerrar", sem barra e sem número de respostas; no painel da direita só os cartões ("Esperados"; "Responderam" e "Faltam" como "disponível ao encerrar"), **sem lista**. Numa equipe de 8 pessoas até uma faixa identificaria quem faltou.
- Depois de `ENCERRADO` o gestor sabe quantos da equipe participaram, sem saber quem (R8). Mas a lista da esquerda **não traz formulário encerrado**, então o front não oferece caminho até esse estado: só trate a resposta se ela chegar (contradição dentro do contrato; ver `fichas\fatia-5.md`, seção 9, "Contradição **dentro do contrato**"). **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D5-3`): o gestor **não** vê formulário encerrado na lista da tela 57.
- O anônimo nunca mostra quem respondeu nem quem falta; "Lembrar" no anônimo nunca diz quantas pessoas da equipe faltam.

**Perguntas abertas (`x-provisorio`)**
- Nenhuma nesta tela: o lembrete manual do gestor (`lembrarQuemFaltaNaEquipe`) foi decidido em 2026-11-02 (o botão fica, só para a equipe dele, nunca na avaliação do gestor e sem contagem no anônimo) e saiu do `PROVISORIOS.md`.

---

## Tela sem print: Resultado do formulário (RH)

**Descrição em texto** (o Figma não tem esta tela; contrato `obterResultadoFormulario`: "O front monta uma página simples 'Resultado'"). Caminho `/rh/formularios/:formularioId/resultado`, aberta pelo botão "Ver resultado" da tela 21 e pelo item do menu "..." da tela 16.

- **Cabeçalho:** título do formulário, selo do tipo e da situação (de `obterFormularioRh`), "N respostas" (`totalRespostas`) e o botão "Baixar respostas" (`exportarRespostasFormulario`).
- **Avaliações:** acima do resultado, a escolha de **quem foi avaliado** (lista de `listarAvaliadosFormulario`: nome, cargo, setor, "N de M avaliações", e para o anônimo com menos de 3 respostas o texto "menos de 3 respostas", sem número). `avaliadoId` é obrigatório na avaliação e proibido nos outros tipos (400). O avaliado com `resultadoLiberado` falso fica desabilitado.
- **Uma seção por pergunta**, na ordem de `ordem`, com o texto e "N respostas" (`totalRespostas`):
  - **Múltipla escolha:** uma barra por alternativa com o texto, `total` e `percentual` (uma casa decimal).
  - **Escala de 1 a 5:** a `media` (uma casa decimal) e a `distribuicao` com os cinco valores, 1 a 5, sempre os cinco.
  - **Resposta curta e longa:** lista de textos **na ordem em que vierem** (alfabética no anônimo), sem numerar e sem reordenar.
  - **Envio de arquivo:** só a quantidade de respostas.
- **Rodapé do anônimo:** quando `perguntasOcultas` é maior que zero, "N perguntas não aparecem porque tiveram menos de 3 respostas" (singular: "1 pergunta não aparece porque teve menos de 3 respostas").
- **Formulário com nome:** sem mínimo, em qualquer situação (`minimoPorPergunta` vazio, `perguntasOcultas` 0). Se ainda está `PUBLICADO`, escreva a etiqueta "Resultado parcial: o formulário ainda está aberto" (suponho). As respostas individuais estão em "Ver respostas" da tela 21, não aqui.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Resultado (com `avaliadoId` nas avaliações) | `obterResultadoFormulario` |
| Lista de avaliados | `listarAvaliadosFormulario` |
| "Baixar respostas" | `exportarRespostasFormulario` |
| Cabeçalho | `obterFormularioRh` |

**Estados e erros**
- Carregando, erro genérico com `detail`.
- **409 `RESULTADO_ANONIMO_INDISPONIVEL`** (anônimo não encerrado, também em avaliados e exportação): mostre "O resultado de um formulário anônimo só aparece depois que ele é encerrado." e nenhum número. O botão "Ver resultado" já vem desabilitado até `ENCERRADO`; a página trata o 409 mesmo assim (link direto).
- **422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES`** (avaliado com menos de 3, ou exportação em que nenhuma pergunta chega a 3): mostre "Esta pessoa recebeu menos de 3 avaliações, então o resultado dela não aparece." (ou o `detail`). A API nunca diz quantas respostas houve.
- 422 `FORMULARIO_NAO_E_AVALIACAO` (lista de avaliados em outro tipo).

**Anonimato nesta tela**
- R3: só depois de encerrado. R4: pergunta com menos de 3 respostas some (só o contador de ocultas aparece); avaliado com menos de 3 não tem resultado e **não mostra o total dele**. R7: sem resultado por pessoa, por setor ou por data; sem filtro por setor; **textos em ordem alfabética e nunca na ordem de gravação**: o front não reordena, não numera e não mostra "resposta 1, 2, 3". Não faça a conta "total do formulário menos avaliados exibidos" na tela (R8). Não guarde o resultado em cache fora do cache normal de consulta e não registre no console o corpo da resposta.

**Perguntas abertas**
- Nenhuma `x-provisorio` própria. O contrato registra ainda "falta o dono responder o mínimo de respostas por avaliado" (`contrato/CHANGELOG.md`, 0.2.0): o número 3 pode mudar; leia-o de `minimoPorPergunta` e `minimoPorAvaliado`, **não o escreva fixo** na tela, exceto no texto do contrato que já o traz.

---

## Tela sem print: Respostas de uma pessoa ou de um candidato (RH)

**Descrição em texto** (o botão "Ver respostas" está na tela 21; o conteúdo não está desenhado). Modal grande sobre a tela 21.

- **Cabeçalho:** pessoa (nome, cargo, setor) ou candidato (nome, e-mail, telefone), título do formulário e "Respondido em dd/mm/aaaa às hh:mm". Nas avaliações, "Avaliando: nome" (`avaliado`).
- **Corpo:** as respostas na ordem de `ordem`, cada uma com o texto da pergunta e a resposta: texto; a alternativa escolhida; a escala como "4 de 5"; arquivo como nome com botão "Baixar" (`baixarArquivoDeResposta`) e, no candidato, "Baixar currículo" (`baixarCurriculoDeCandidato`, só com `temCurriculo`). Pergunta opcional em branco: "Sem resposta".
- Nas **avaliações** a chamada leva `avaliadoId` (o da linha); na **Sugestão** leva `respondidoEmUtc` (o da linha); nos outros tipos nenhum dos dois (400 se vier).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Respostas de funcionário | `obterRespostasDeFuncionario` |
| Respostas de candidato | `obterRespostasDeCandidato` |
| Arquivo | `baixarArquivoDeResposta` |
| Currículo | `baixarCurriculoDeCandidato` |

**Estados e erros**
- 404 (pessoa não respondeu, candidato de outro formulário, ou Sugestão sem envio naquele instante), 422 `FORMULARIO_SEM_LINK` (candidato em formulário que não é por link), 422 `FORMULARIO_ANONIMO` (ver abaixo), 400 por parâmetro errado.

**Anonimato nesta tela**
- Em formulário anônimo **esta tela não existe**: a tela 21 não mostra "Ver respostas" e a rota devolve 422 `FORMULARIO_ANONIMO` ("Este formulário é anônimo: o sistema não guarda quem respondeu o quê."). Não ofereça atalho nem link direto.

---

## Tela sem print: Responder um formulário (todos os perfis)

**Descrição em texto** (contrato `obterFormularioParaResponder`: "Tela de responder (sem print; o botão 'Responder' do cartão leva a ela). Uma pergunta por vez, com barra de 'Pergunta 1 de 4' e botão 'Próxima', como a prévia do assistente (tela 17)"). Caminho `/eu/formularios/:formularioId/responder`. Serve ao funcionário, ao gestor e ao RH.

- **Cabeçalho:** selo do tipo, título, descrição, "N min" (`minutosEstimados`), prazo ("Até dd/mm" ou "Sempre aberto").
- **Corpo:** uma pergunta por vez com "Pergunta N de M" e a barra de progresso (como na prévia da tela 17: título da pergunta, o campo, botão "Próxima"; na última, "Enviar respostas"; "Voltar" nas seguintes). Campos por tipo: curta (linha, até 200 caracteres), longa (área, até 4000), múltipla escolha (**escolha de uma**), escala de 1 a 5 (cinco botões numerados), envio de arquivo (PDF ou Word, até 5 MB; confira tipo e tamanho no navegador antes de enviar e mostre o erro junto do campo). Pergunta obrigatória sem resposta bloqueia "Próxima"; opcional pode ficar em branco.
- **Avaliações:** antes das perguntas, a lista de **quem avaliar** (`avaliados`: nome, cargo, "Já avaliado" ou "Avaliar"/"Continuar avaliação"). Escolhida a pessoa, responde-se as perguntas e envia-se **uma resposta por avaliado** (`avaliadoId` no envio). O recibo traz `restantes` (avaliações que ainda faltam). Na avaliação do gestor (`EQUIPE_AVALIA_GESTOR`) `avaliados` tem uma só pessoa (o gestor do setor): pule a lista e mostre "Você vai avaliar nome".
- **Depois de enviar:** confirmação "Respostas enviadas", botão de voltar à lista (tela 39); em avaliação com `restantes` maior que zero, "Avaliar a próxima pessoa". Atualize a lista e a bolinha do menu. **Sugestão:** volta à lista e o formulário continua disponível.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Abrir (perguntas, avaliados) | `obterFormularioParaResponder` |
| Enviar (`multipart/form-data`: parte `respostas` em JSON, partes `arquivos`, `avaliadoId`) | `responderFormulario` |

**Estados e erros**
- 404 (formulário que a pessoa não deve responder). 422 `FORMULARIO_AINDA_NAO_ABERTO` ("Este formulário ainda não está aberto. Tente de novo a partir da data de publicação."), 422 `FORMULARIO_ENCERRADO` ("Este formulário não está mais aceitando respostas."), 422 `AVALIADO_INVALIDO` ("Você não deve avaliar esta pessoa neste formulário."), 422 `FUNCIONARIO_INATIVO`. Pode vir `regraDoBanco` ("Pergunta obrigatoria: informe a resposta."; mostre o `detail`, nunca a mensagem técnica sozinha).
- **409 `JA_RESPONDIDO`** ("Você já respondeu este formulário."): leve de volta à lista. Não vale na Sugestão.
- 400 com `errors` por campo (`respostas[2].texto`, `arquivos`): mostre junto da pergunta. Tudo ou nada: se uma resposta é inválida, nada é gravado.
- Tipos: cada resposta usa **só o campo do tipo da pergunta** (`texto`, `alternativaIds` com um item, `valorEscala` de 1 a 5, `arquivoIndice`).

**Anonimato nesta tela**
- Em formulário anônimo mostre, antes da primeira pergunta e perto do botão de enviar, "Suas respostas são anônimas: o RH e o gestor não veem quem respondeu o quê." (o contrato diz que o aviso é sempre verdadeiro até o envio, porque anonimato e perguntas não mudam depois de publicado; por isso o envio **não** manda um campo de conferência). Não escreva promessas além disso (R8).
- A resposta é gravada sem funcionário e com a hora grosseira (só o dia); o recibo traz `participacaoId` (UUID): mostre apenas "Participação registrada" e **não use o `participacaoId` para mais nada**.
- **Não guarde rascunho local** (localStorage ou similar) de respostas de formulário anônimo, não registre as respostas no console e não envie hora do navegador. Em anônimo não há pergunta de arquivo.

**Observações para quem monta**
- O envio é multipart: o teste desta tela usa `// @vitest-environment node` (jsdom não monta multipart; ver `02-pilha.md`).
- Texto de pergunta e de descrição é texto ou markdown, **nunca HTML**: renderize com o componente seguro da fundação (ou como texto puro).

---

## Tela sem print: Minhas respostas (todos os perfis)

**Descrição em texto** (botão "Ver minhas respostas" das telas 40). Página `/eu/formularios/:formularioId/minhas-respostas` (`05-caminhos-de-tela.md`).

- Título do formulário, "Respondido em dd/mm/aaaa" e as respostas na ordem de `ordem`, como na tela de respostas do RH (arquivo só com o **nome**, sem baixar: "não há download de volta"). Nas avaliações com nome, a pessoa escolhe qual avaliação ver (`avaliadoId`; use os `avaliados` já respondidos de `obterFormularioParaResponder`).

**Rotas:** `obterMinhasRespostas`.

**Estados e erros:** 404 (não respondeu); 422 `FORMULARIO_ANONIMO` e 422 `FORMULARIO_SUGESTAO` (o cartão já vem com `podeVerMinhasRespostas` falso: o botão nem aparece); 400 com `avaliadoId` nos tipos errados.

**Anonimato nesta tela:** não existe para formulário anônimo (a resposta não é da pessoa para o sistema). Nada de atalho por link.

---

## Tela sem print: Formulário público do candidato

**Descrição em texto** (roadmap, decisão 14; contrato `obterFormularioPublico` e `responderFormularioPublico`: "o front a desenha em estilo simples, como a prévia 'Como o candidato vai ver' do assistente"). Caminho `/formulario/:token`, **fora da área logada e sem menu**.

- **Cabeçalho:** quadrado com a inicial e o nome da empresa (`empresaNome`), como na prévia da tela 17.
- **Etapa de identificação, antes das perguntas:** título e descrição do formulário (e prazo, se houver), campos **Nome** (obrigatório, até 150 caracteres), **E-mail** (obrigatório), **Telefone** (opcional; só dígitos, espaço, parênteses, `+` e `-`, de 8 a 30 caracteres), **Currículo** (opcional; PDF ou Word, até 5 MB), o **aviso de privacidade** (`avisoDePrivacidade`, texto ou markdown; diz o que é guardado, quem vê e por quanto tempo: `retencaoDias`) e a **caixa de aceite** ("Li e concordo com o aviso de privacidade"; o botão "Começar" só habilita marcada; vai no envio como `aceitouAvisoDePrivacidade`, que precisa ser verdadeiro).
- **Perguntas:** uma por vez, "Pergunta N de M", "Próxima" e na última "Enviar candidatura"; mesmos tipos e mesma regra de obrigatórias de `responderFormulario` (curta, longa, múltipla escolha de uma opção, escala, envio de arquivo de PDF ou Word até 5 MB).
- **Sucesso:** mostra `mensagem` ("Recebemos as suas respostas. Obrigado por se candidatar.").
- **Layout:** esta é a única página provavelmente aberta em celular. Faça coluna única fluida que funcione de 360 px até o desktop (o Figma só tem 1440 px; suponho a necessidade, o contrato não a pede).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Abrir pelo link (sem login) | `obterFormularioPublico` |
| Enviar a candidatura (`multipart/form-data`: `nome`, `email`, `telefone`, `curriculo`, `aceitouAvisoDePrivacidade`, `respostas` em JSON, `arquivos`) | `responderFormularioPublico` |

**Estados e erros (mostre o `detail` ou o texto abaixo)**
- 404 (token que não existe ou formulário em rascunho; a mesma resposta): "Este link não existe ou não está mais disponível." (texto meu).
- 422 `FORMULARIO_AINDA_NAO_ABERTO` ("Este formulário ainda não está aberto. Tente de novo a partir da data de publicação."), 422 `FORMULARIO_ENCERRADO` ("Este formulário não está mais aceitando respostas."), 422 `EMPRESA_NAO_CONFIGURADA` ("Este formulário ainda não está disponível. Fale com o RH.").
- **429** (limite de 30 consultas por minuto e 10 envios por hora por IP; cabeçalho `Retry-After`): "Muitas tentativas. Tente de novo em alguns minutos." (texto meu).
- 409 `JA_RESPONDIDO` ("Já recebemos uma candidatura com este e-mail."): o mesmo e-mail responde uma vez por formulário.
- 400 por campo (`curriculo`, `arquivos`, `email`, `telefone`): mostre junto do campo.

**Anonimato e segurança nesta tela**
- **Formulário anônimo nunca é público**: a página nunca abre um. O candidato não é anônimo (dá nome e e-mail); o aviso de privacidade diz isso.
- Esta página **não usa a sessão do site**: chame a API **sem** `Authorization` e **sem** o ciclo de renovação de sessão (rotas `Publico` têm `security: []`). Não guarde nada no navegador (o refresh token do site fica em `localStorage`; esta página nunca o toca). Não use nenhum recurso externo (política `default-src 'self'`): fonte e ícones do próprio site.
- O candidato nunca vê o `id` interno do formulário nem dado de outro candidato.

**Perguntas abertas (`x-provisorio`)**
- `obterFormularioPublico` e `responderFormularioPublico`: proteção do link (hipótese: só limite por IP; se for captcha, o envio ganha um campo de prova do captcha; se o dono quiser limite de respostas por link, a publicação ganha `limiteDeRespostas`) e retenção (180 dias). `FormularioPaginaPublica.retencaoDias`: leia o valor da API, não o escreva fixo.

---

## Ligação com outras telas (caixa "Enviar o formulário de admissão")

Tela 02 (adicionar colaborador, fatia 1b) e a confirmação da importação (fatia 2) têm a caixa "Enviar o formulário de admissão", que o roadmap manda deixar **desligada até esta fatia** (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, "Dependências cruzadas" da fatia 1): nas duas ela está **visível, desmarcada e desabilitada**, com a dica "Indisponível por enquanto", e envia `formularioAdmissaoId: null`. **A ficha 5 (bloco 9) liga as duas telas de uma vez** (e o diálogo "Readmitir" da ficha, tela 03, que a 1b deixou igual), no fim da fatia:

- A lista de formulários de admissão vem de `listarFormulariosRh` com `tipo=ADMISSAO` e `situacao=PUBLICADO`. Se não houver nenhum, a caixa fica desligada com a dica "Crie e publique um formulário de admissão em Formulários".
- Marcada, o cadastro (`criarFuncionario`), a importação (`confirmarImportacao`) e a readmissão (`readmitirFuncionario`, diálogo "Readmitir" da ficha) mandam `formularioAdmissaoId` (na importação, o resultado passa a mostrar "Incluídos no formulário de admissão: N", `incluidosNoFormularioDeAdmissao`); a API inclui a pessoa como destinatária **por dentro**, com o mesmo efeito de `adicionarDestinatariosFormulario`. **Nenhuma tela chama essa rota diretamente.** Nenhum e-mail sai; a pessoa vê o formulário ao entrar no portal.
- Erros que voltam do cadastro: 400 em `formularioAdmissaoId` (não existe, não é de admissão ou não está publicado), 422 `FORMULARIO_NAO_E_POR_PESSOAS`, 422 `FORMULARIO_ENCERRADO`, 409 `ANONIMO_SEM_NOVOS_DESTINATARIOS` (só se o formulário de admissão escolhido for anônimo e já estiver publicado; o RH marca o anonimato em qualquer tipo menos Sugestão, então a lista pode oferecer esse caso: mostre o `detail`).
