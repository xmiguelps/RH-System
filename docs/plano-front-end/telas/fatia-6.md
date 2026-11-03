# Notas de tela da fatia 6: TAD (treinamentos)

Contrato lido: `contrato/fontes/60-fatia-6-tad.yaml` (a versão do contrato é o `info.version` de `contrato/openapi.yaml`). Fatia 6 tem 31 rotas (`contrato/ROTAS.md`). Ficha da fatia: `docs/plano-front-end/fichas/fatia-6.md`.

## Como ler estas notas

- **"Li no print"** é o que está escrito no PNG indicado (`Figma/telas/NN-...png`, 1440 px de largura). **"Contrato"** é o que o `operationId` citado diz. O que vem com **(suponho)** é inferência minha: confirme na conferência visual ou registre em `DUVIDAS.md`.
- **Onde o Figma e o contrato divergem, vale o contrato** (e a seção 7 do design `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md`). Cada nota diz o que o PNG mostra e o front não terá, com a razão.
- Nomes e números dos PNG são só ilustração. Os dados simulados (MSW) vêm dos exemplos do contrato e de `contrato/ELENCO.md` (treinamentos 1, 2 e 3 são Onboarding 2026, LGPD na prática e Segurança da informação; os exemplos acrescentam 4 "Atendimento ao cliente" agendado, 5 "Armazenagem segura (NR-11)" encerrado, 6 "Prevenção de assédio no trabalho" rascunho e 7 "Excel para relatórios financeiros").
- **Caminhos de tela:** a tabela única é `05-caminhos-de-tela.md` (vale sobre qualquer caminho escrito nesta nota). Os desta fatia: tela 22 `/rh/treinamentos` (com `?quemFez=<id>` quando um treinamento está aberto na seção de baixo); assistente `/rh/treinamentos/novo` e `/rh/treinamentos/:treinamentoId/editar` (modal sobre a lista, etapa em `?etapa=detalhes`, `conteudo`, `certificado` ou `publicacao`); telas 41 e 42 `/eu/treinamentos` (`?aba=todos`, `disponiveis`, `em-andamento` ou `concluidos`); fazer o treinamento `/eu/treinamentos/:treinamentoId`; gestor `/gestor/treinamentos`; aba Treinamentos da ficha da equipe (tela 52) `/gestor/equipe/:funcionarioId?aba=treinamentos`; conferir certificado (público, fora da área logada e sem menu) `/certificado` e `/certificado/:codigo`.

## Regras que valem em todas as telas desta fatia

Decididas pelo dono em 2026-10-29 e 2026-10-30 (`contrato/CONVENCOES.md`, seções 12 e 13, e a descrição da tag Treinamentos do contrato).

- **T1. Treinamento não tem prazo.** Fica disponível para quem entrar na empresa. Não existem: data final, "Termina em", "Período 01/01 a 31/12", "Prazos até 31/10", "Até 15/11", "De 20/10 a 20/11", "faltam 24 dias", "atrasado", nem "O prazo é 15/11". Existem: publicar, a data de início (só libera o acesso) e **encerrar** ou **excluir** a qualquer momento.
- **T2. Sem quiz, nota, prova final, turma presencial nem modalidade.** Não existem: "Terminar com um quiz", "Nota mínima para concluir", a coluna Nota, "Concluído com nota 9,0", módulo "Aula presencial", "Híbrido", "Presencial", o campo "Tipo" do passo Detalhes e o filtro "Todos os tipos". Todo treinamento é online.
- **T3. Esta fatia não envia e-mail nenhum.** Não existem: "Avisar por e-mail ao publicar", "Lembrar quem não começou 7 dias antes do fim", "Lembrar quem não terminou" (tela 22) e "Lembrar quem falta por e-mail" (tela 58). Quem quiser avisar usa um Aviso (fatia 4), à mão.
- **T4. Encerrar e excluir pedem confirmação `CONFIRMACAO_NECESSARIA`** (409, com `pendentes`, `emAndamento` e `naoIniciaram`) quando há gente que ainda não concluiu; a tela repete a chamada com `confirmar=true`. **Treinamento que já emitiu certificado não se exclui** (422 `TREINAMENTO_COM_CERTIFICADOS`, mesmo com `confirmar=true`): só se encerra. Não há rota para reabrir nem para despublicar.
- **T5. Situações.** Do treinamento (`TreinamentoSituacao`): `RASCUNHO`, `AGENDADO` (publicado, `comecaEm` ainda não chegou), `EM_ANDAMENTO`, `ENCERRADO` (o RH encerrou). Da pessoa (`TreinamentoParticipanteSituacao`): `INSCRITO` (o Figma escreve "Não iniciado"), `EM_ANDAMENTO`, `CONCLUIDO`. Na tela "Meus treinamentos" (`TreinamentoMinhaSituacao`): `DISPONIVEL`, `NAO_INICIADO`, `EM_ANDAMENTO`, `CONCLUIDO`.
- **T6. "Pendente"** (indicador da tela 22, ficha, faixa "Sua parte"): treinamento **obrigatório** `EM_ANDAMENTO` em que a pessoa tem linha `INSCRITO` ou `EM_ANDAMENTO`. Opcional, agendado e encerrado nunca são pendência. A API já faz a conta; o front não recalcula.
- **T7. Quem tem linha:** obrigatório cria uma linha `INSCRITO` para cada pessoa ativa do público ao **publicar**; opcional só tem linha de quem se inscreveu.
- **T8. Datas:** a API fala UTC (`...Z`); instantes (`concluidoEmUtc`, `encerradoEmUtc`) mostram só o dia em `America/Sao_Paulo`; datas de calendário (`comecaEm`, `iniciadoEm`, `emitidoEm`) são dias sem fuso. Formato dd/mm/aaaa (dd/mm onde o PNG só tem dia e mês).

---

## Tela 22: Treinamentos, lista e "Quem fez" (RH)

PNG: `Figma/telas/22-rh-treinamentos.png` (1440 × 2015; é uma página só com duas tabelas, uma sobre a outra).

**Li no print**
- Título "Treinamentos"; subtítulo "Onboarding, treinamentos obrigatórios e opcionais"; botão "+ Novo treinamento".
- Faixa de quatro indicadores: "Em andamento 3", "Obrigatórios 4", "Certificados emitidos em 2026 186", "Pessoas com obrigatório pendente 17" com a legenda "Prazos até 31/10".
- Abas com número (quebradas em duas linhas): Todos 5, Em andamento 3, Agendados 1, Encerrados 1. Busca "Buscar treinamento" e seletor "Todos os tipos".
- Tabela de cima, colunas Treinamento (título e, embaixo, "Online", "Híbrido, turma presencial em 23/10" ou "Presencial"), Período ("01/01 a 31/12/2026"...), Obrigatoriedade (selos "Obrigatório" e "Opcional"), Conclusão ("18 de 24 concluíram" com barra; "18 inscritos" sem barra), Situação ("Em andamento", "Agendado", "Encerrado") e dois controles: botão "Ver quem fez" e "...". Uma linha (LGPD na prática) está realçada.
- **Seção de baixo "Quem fez"** do treinamento escolhido: pequeno título "Quem fez", "LGPD na prática", legenda "Obrigatório para todos, com quiz final e certificado. 01/08 a 31/10/2026."; botões "Baixar lista" e "Lembrar quem não terminou"; três cartões Concluíram 63, Em andamento 21, Não iniciaram 17; abas Todos, Concluíram, Em andamento, Não iniciaram e seletor "Todos os setores"; tabela com Pessoa (iniciais e nome), Setor, Situação ("Concluído", "Em andamento", "Não iniciado"), Início, Conclusão, **Nota**, Certificado (botão "Baixar" ou "—"); rodapé "Mostrando 10 de 101".

**O que fica**
- A faixa de indicadores (com os números vindos da API), as abas, a busca, as colunas Treinamento, Obrigatoriedade, Conclusão e Situação, "Novo treinamento", "Ver quem fez", o menu "...", a seção "Quem fez" com "Baixar lista", os três cartões, as abas, o filtro de setor, as colunas Pessoa, Setor, Situação, Início, Conclusão e Certificado, e "Mostrando N de M".

**O que sai ou muda (e por quê)**
- **Sai a legenda "Prazos até 31/10"** do indicador (T1): o cartão mostra só o número. Os rótulos vêm de `obterIndicadoresDeTreinamentos`: `emAndamento`, `obrigatorios` (publicados, em qualquer situação, **inclusive agendado e encerrado**), `certificadosEmitidosNoAno` (rótulo "Certificados emitidos em {`ano`}", só de treinamento que emite certificado) e `pessoasComObrigatorioPendente` (T6). Os valores do PNG (186 e 17) são ilustração; o exemplo do contrato diz 134 e 72.
- **Sai o filtro "Todos os tipos"** e a segunda linha do título ("Online", "Híbrido...", "Presencial") (T2). No lugar da segunda linha mostre "N módulos" (`totalModulos`; suponho).
- **A coluna "Período" vira "Começa em"** (`comecaEm`; vazio em rascunho sem data) e, no encerrado, o texto "Encerrado em dd/mm" com o dia de `encerradoEmUtc` (T1: não há fim).
- **Entra a aba "Rascunhos"** (o PNG não a desenha, mas o RH precisa dela para retomar o que salvou; contrato `listarTreinamentos`). As abas vêm de `situacao` (`RASCUNHO`, `AGENDADO`, `EM_ANDAMENTO`, `ENCERRADO`) e "Todos" (sem `situacao`) **inclui os rascunhos**. Números em `contagens` (`todos`, `rascunhos`, `agendados`, `emAndamento`, `encerrados`); ignoram a aba e respeitam a busca. Ordem das abas: Todos, Em andamento, Agendados, Rascunhos, Encerrados (suponho a posição de "Rascunhos").
- **Coluna "Conclusão":** obrigatório mostra "`concluidos` de `totalParticipantes` concluíram" com barra; opcional mostra "`totalParticipantes` inscritos" sem barra; rascunho tem zeros (mostre "Sem participantes ainda"; suponho). O PNG mostra "18 inscritos" num opcional **agendado**: isso não acontece, porque opcional só tem linha de quem se inscreveu e ninguém vê o treinamento antes da data de início (no exemplo do contrato, 0).
- **Ações da linha:** "Ver quem fez" abre a seção de baixo para aquele treinamento (rascunho não tem participantes: ponha "Editar" no lugar; suponho). **Menu "..." com Editar, Encerrar e Excluir** (o PNG só mostra o "..." fechado; o conteúdo é do contrato):
  - **Editar** abre o assistente (`obterTreinamento`).
  - **Encerrar** só em `EM_ANDAMENTO` (em rascunho e agendado se exclui; 422 `TREINAMENTO_NAO_ESTA_EM_ANDAMENTO`).
  - **Excluir:** com `certificadosEmitidos` maior que zero, **fica desabilitado** com a explicação "Treinamento com certificado emitido só pode ser encerrado" (a API recusaria com 422 `TREINAMENTO_COM_CERTIFICADOS`); nos demais casos abre a confirmação. Não há "Duplicar" nem "Lembrar".
- **Seção "Quem fez":**
  - A legenda perde o fim e o quiz: "Obrigatório para todos, com certificado, desde 01/08" (só "com certificado" quando `emiteCertificado`; e, se encerrado, "encerrado em dd/mm"). "Todos", "Setores" ou "Pessoas escolhidas" vem de `publico`.
  - **Sai "Lembrar quem não terminou"** (T3). **Sai a coluna "Nota"** e a legenda "quiz final" (T2).
  - Cartões "Concluíram", "Em andamento" e "Não iniciaram" (`contagens.concluidos`, `emAndamento`, `naoIniciados`; "Não iniciaram" é `INSCRITO`). Abas = `situacao` (`CONCLUIDO`, `EM_ANDAMENTO`, `INSCRITO`); setor = `setorId`; a API também aceita `busca` por nome e `ordenarPor` (`NOME`, `SETOR`, `SITUACAO`, `INICIO`, `CONCLUSAO`), mas o PNG não tem busca nesta tabela: não precisa construí-la.
  - "Início" é `iniciadoEm` (dia); "Conclusão" é o dia de `concluidoEmUtc`; ambos "—" quando vazios. "Baixar" só aparece quando `certificadoDisponivel` é verdadeiro (concluiu **e** o treinamento emite certificado).
  - Quem foi inativado continua na lista (`funcionario.ativo`); marque "Inativo" ao lado do nome (suponho). Treinamento encerrado continua consultável: quem não começou aparece "Não iniciado" e quem terminou depois do encerramento aparece "Concluído".
  - O PNG mostra 10 linhas ("Mostrando 10 de 101"): envie `tamanhoPagina=10` (suponho) e mostre "Mostrando N de `total`".
  - "Baixar lista" baixa o CSV com os mesmos filtros (`situacao`, `setorId`, `busca`), todas as linhas, colunas `Pessoa;Setor;Situação;Início;Conclusão` (sem Nota).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Faixa de indicadores | `obterIndicadoresDeTreinamentos` |
| Abas, busca, tabela de cima (`situacao`, `busca`, `ordenarPor` = `TITULO` ou `COMECA_EM`, `direcao`, páginas) | `listarTreinamentos` |
| "Editar" abre o assistente | `obterTreinamento` |
| "Encerrar" (repete com `confirmar=true`) | `encerrarTreinamento` |
| "Excluir" (repete com `confirmar=true`) | `excluirTreinamento` |
| "Ver quem fez": cartões, abas, tabela de baixo | `listarParticipantesDeTreinamento` |
| "Baixar lista" (CSV) | `exportarParticipantesDeTreinamento` |
| "Baixar" do certificado de uma pessoa (link temporário) | `baixarCertificadoDeParticipante` |
| Filtro "Todos os setores" | `listarSetores` (fatia 1) |

**Estados**
- Carregando (indicadores e linhas em esqueleto). Vazio: "Nenhum treinamento ainda" com "Novo treinamento"; sem resultado de busca: "Nenhum treinamento encontrado". Seção "Quem fez" sem treinamento escolhido: "Escolha um treinamento em 'Ver quem fez'" (suponho). Lista de participantes vazia na aba: "Ninguém nesta situação". Erro com `detail`. 403: só RH.

**Textos e mensagens**
- Confirmar encerrar (409 `CONFIRMACAO_NECESSARIA`; `detail` de exemplo: "38 pessoas ainda não concluíram este treinamento. Confirme para encerrar."): "N pessoas ainda não concluíram este treinamento (X em andamento, Y não começaram). Quem já começou continua e termina; quem não começou deixa de poder começar. Encerrar mesmo assim?" (texto meu, os fatos são do contrato).
- Confirmar excluir (409; só sem certificado emitido): "N pessoas ainda não concluíram este treinamento (X em andamento, Y não começaram). Excluir apaga o treinamento e o andamento de todos. Excluir mesmo assim?"
- Excluir rascunho ou agendado: ninguém o viu, apaga direto na API (204); peça só uma confirmação simples "Excluir 'título'? Isso não pode ser desfeito." (suponho).
- 422 `TREINAMENTO_COM_CERTIFICADOS`: "Este treinamento já emitiu N certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre." (o `detail` do contrato). 422 `TREINAMENTO_ENCERRADO` e `TREINAMENTO_NAO_ESTA_EM_ANDAMENTO`: mostre o `detail`.
- Depois de encerrar: "Treinamento encerrado. Quem já começou ainda pode terminar." (suponho o texto).

**Observações para quem monta**
- A página é longa (duas tabelas); mantenha a seção "Quem fez" abaixo da lista e role até ela ao clicar "Ver quem fez".
- `certificadosEmitidos` na linha é a fonte do "Excluir" desabilitado; não conte conclusões no front.
- O download do certificado (arquivo guardado) abre o `LinkTemporario` em nova aba com `abrirLinkTemporario(link)`; o CSV de "Quem fez" vem direto da API (os bytes, `contrato/CONVENCOES.md`, seção 4) com `Content-Disposition: attachment` e se baixa com `baixarExportacao(chamada, nomePadrao)`; os dois auxiliares são da fundação 1a (`src\shared\arquivos\baixar.ts`).

**Perguntas abertas (`x-provisorio`)**
- `baixarCertificadoDeParticipante`: modelo visual do certificado e formato do número (o PDF segue o modelo simples do back-end). `listarTreinamentosDeOnboarding` e `TreinamentoComando.onboarding`: como o RH marca o treinamento de onboarding. `atualizarTreinamento`: o que o RH pode mudar depois de publicar.

---

## Tela 23: Novo treinamento, etapa Detalhes (RH)

PNG: `Figma/telas/23-rh-treinamentos-novo-treinamento-etapa-detalhes.png`.

**Li no print**
- Janela "Novo treinamento" com "X"; etapas **Detalhes** (ativa), Conteúdo, **Avaliação**, Publicação.
- Campos: Título ("Prevenção de assédio no trabalho"), Descrição (texto de várias linhas), **Tipo** (seletor "Online"), **Começa em** (15/10/2026 no formato do navegador), **Termina em** (15/12/2026); caixa marcada **"Obrigatório. Quem não concluir até o fim aparece como pendente para o RH e para o gestor."**
- Prévia "Como o colaborador vai ver" (selo "Prévia"): cabeçalho "[Nome da empresa]", selos "Obrigatório" e "De 15/10 a 15/12", título, descrição, "Online. 3 módulos e um quiz final.", barra de progresso, lista numerada dos três módulos e "Quiz final e certificado", botão "Começar". Legenda "A prévia muda conforme você preenche." Rodapé: "Salvar rascunho e sair" e "Próximo".

**O que fica**
- Título (de 3 a 200 caracteres), Descrição (até 2000, texto ou markdown, nunca HTML), "Começa em", a caixa "Obrigatório", a prévia montada no front (não há rota), "Salvar rascunho e sair" e "Próximo".

**O que sai ou muda (e por quê)**
- **Saem o campo "Tipo" e o campo "Termina em"** (T1 e T2). "Começa em" (`comecaEm`) só **libera o acesso**; data futura deixa o treinamento `AGENDADO` até lá.
- **Texto da caixa:** "Obrigatório. Quem não concluir aparece como pendente para o RH e para o gestor enquanto o treinamento estiver em andamento." (o "até o fim" do PNG não existe).
- **Prévia:** o selo "De 15/10 a 15/12" vira "Começa em 15/10" (suponho); "Online. 3 módulos e um quiz final." vira "3 módulos" (e a soma das durações, se todos têm; suponho); a última linha "Quiz final e certificado" vira "Certificado" e só aparece se "Emitir certificado" estiver marcado (passo seguinte).
- **Entra a caixa do onboarding**, que o Figma não desenha (contrato `TreinamentoComando.onboarding`, `x-provisorio`): "É o treinamento de onboarding (quem for cadastrado pode ser incluído nele)" (texto meu).
- **Primeira saída da etapa = `criarTreinamento`** (só o título é obrigatório; nasce **rascunho**; ninguém o vê antes de publicar); as seguintes = `atualizarTreinamento`. O `PUT` **substitui todos os campos**: envie sempre o comando inteiro (campo omitido volta ao padrão).
- **Depois de publicado** só mudam título, descrição, "incluir quem for admitido" e, enquanto `AGENDADO`, "Começa em". O resto abre desabilitado com a dica "Depois de publicado, só dá para mudar o título, a descrição e a opção de incluir quem for admitido." (422 `TREINAMENTO_CAMPO_TRAVADO`). `ENCERRADO`: só leitura (422 `TREINAMENTO_ENCERRADO`: "Este treinamento foi encerrado e não pode mais ser alterado.").

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Primeira vez que sai da etapa | `criarTreinamento` |
| Saídas seguintes e edição | `atualizarTreinamento` |
| Abrir um existente (Editar da lista; retomar rascunho) | `obterTreinamento` |

**Estados**
- 400 por campo (`errors.titulo`...). 404, 403 e 500 como nas outras telas do RH.

**Perguntas abertas (`x-provisorio`)**
- `TreinamentoComando.onboarding` e `listarTreinamentosDeOnboarding`: como o RH escolhe o treinamento de onboarding (hipótese: a caixa guardada em `TREINAMENTOS.TIPO_TREINAMENTO`). `atualizarTreinamento`: o que se pode mudar depois de publicar (hipótese: só título, descrição, incluir admitidos e a data de início enquanto agendado).

---

## Tela 24: Novo treinamento, etapa Conteúdo (RH)

PNG: `Figma/telas/24-rh-treinamentos-novo-treinamento-etapa-conteudo.png`.

**Li no print**
- Etapa **Conteúdo** ativa. Três módulos numerados, cada um com título, tipo e botão "Editar": 1 "O que é assédio moral e sexual" ("Vídeo, 15 minutos"), 2 "Situações do dia a dia e como agir" ("Texto com exemplos"), 3 "Canais de denúncia da empresa" ("Arquivo PDF").
- Caixa tracejada **"Adicionar módulo:"** com os botões Vídeo, Arquivo PDF, Texto, Link e **Aula presencial**. Prévia à direita, rodapé com "Salvar rascunho e sair", "Voltar" e "Próximo" ("Voltar" sobre "Próximo": ponha lado a lado).

**O que fica**
- A lista numerada de módulos com "Editar", os botões Vídeo, Arquivo PDF, Texto e Link, a prévia (lista dos módulos) e os botões do rodapé.

**O que sai ou muda (e por quê)**
- **Sai "Aula presencial"** (T2; `TreinamentoModuloTipo` é `VIDEO`, `TEXTO`, `LINK`, `PDF`).
- **A segunda linha do módulo** ("Texto com exemplos", "Arquivo PDF") não vem da API (o módulo não tem subtítulo): mostre o tipo e a duração, como "Vídeo, 15 minutos", "Texto, 10 minutos", "PDF, 5 minutos" (e o `nomeArquivo` quando for PDF; suponho).
- **O editor de módulo não está desenhado.** Descrição em texto: formulário no modal com Título (até 200), Duração em minutos (opcional, de 1 a 32767) e o campo do tipo: **Vídeo** pede o endereço (`urlExterna`), **Link** pede o endereço, **Texto** pede o texto (`conteudoTexto`, até 50000, texto ou markdown, **nunca HTML**), **PDF** pede o arquivo (PDF de até 10 MB; confira tipo e tamanho no navegador, a API confere pelo conteúdo). `urlExterna` precisa começar com `http://` ou `https://`; campo que não é do tipo é recusado (400). Envio em `multipart/form-data`. Sem arquivo novo num PDF em edição, o arquivo atual fica.
- **Vídeo é só link externo** (hipótese do contrato, `x-provisorio`): o botão "Vídeo" pede um endereço, não um arquivo.
- **Mover e apagar módulo** não estão desenhados: ofereça "Subir", "Descer" (`reordenarModulosDeTreinamento` recebe **todos** os ids na nova ordem) e "Remover" dentro do editor (suponho a forma). Os módulos que sobram são renumerados pela API.
- **Depois de publicado a lista de módulos não muda:** acrescentar, reordenar e excluir dão 422 `TREINAMENTO_MODULOS_TRAVADOS`; só se **corrige o conteúdo** de um módulo existente (título, duração, endereço, texto, arquivo), e o `tipo` não muda (422 `TREINAMENTO_CAMPO_TRAVADO`). Em treinamento publicado ou encerrado, desabilite os botões de adicionar, mover e remover com a dica "Este treinamento já foi publicado. Não dá mais para acrescentar, reordenar nem excluir módulos; só corrigir o conteúdo deles."
- Precisa de pelo menos um módulo para publicar (a etapa Publicação mostra o erro `errors.modulos`).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Acrescentar módulo (multipart) | `adicionarModuloDeTreinamento` |
| Salvar o editor de um módulo (multipart) | `atualizarModuloDeTreinamento` |
| Reordenar | `reordenarModulosDeTreinamento` |
| Remover | `excluirModuloDeTreinamento` |
| Recarregar a lista de módulos | `obterTreinamento` |

**Estados**
- Sem módulos: "Nenhum módulo ainda. Adicione o primeiro." 400 por campo (`errors.urlExterna`, `errors.conteudoTexto`, `errors.arquivo`).

**Observações para quem monta**
- O teste de envio com arquivo usa `// @vitest-environment node` (jsdom não monta multipart; ver `02-pilha.md`).
- Em módulo `PDF` a API devolve só o **nome** do arquivo (`nomeArquivo`), nunca o caminho interno.

**Perguntas abertas (`x-provisorio`)**
- `adicionarModuloDeTreinamento`, `atualizarModuloDeTreinamento`, `reordenarModulosDeTreinamento`, `excluirModuloDeTreinamento`: onde ficam os vídeos (hipótese: só link externo; se for arquivo no Blob, "Vídeo" passa a aceitar `arquivo`) e se se pode acrescentar módulo depois de publicado (hoje proibido).

---

## Tela 25: Novo treinamento, etapa "Avaliação e certificado" (RH)

PNG: `Figma/telas/25-rh-treinamentos-novo-treinamento-etapa-avaliacao.png`.

**Li no print**
- Etapa **Avaliação** ativa; título "Avaliação e certificado". Caixa marcada **"Terminar com um quiz"**; "Quiz" (seletor "Criar um quiz novo") e **"Nota mínima para concluir"** (7 de 10); caixa marcada **"Emitir certificado com código de validação ao concluir"**; linha verde "3 módulos com conteúdo"; **aviso laranja** "O quiz ainda não foi criado. Ele aparece em Formulários como rascunho depois que você publicar." Prévia e rodapé como nas outras etapas.

**O que fica**
- A caixa **"Emitir certificado com código de validação ao concluir"** (decisão do dono, 2026-10-29: fica; marcada por padrão) e a linha verde com o número de módulos.

**O que sai ou muda (e por quê)**
- **Saem "Terminar com um quiz", o seletor "Quiz", "Nota mínima para concluir" e o aviso laranja do quiz** (T2; quiz e nota foram cortados em 2026-10-29). Também sai o vínculo com "Formulários" (não há quiz de treinamento).
- **O passo sobra só com a caixa do certificado** e o contrato deixa o front chamá-lo de **"Certificado"** (`contrato/fontes/60-fatia-6-tad.yaml`, tag Treinamentos): troque o rótulo da etapa no indicador de etapas.
- **A linha verde "N módulos com conteúdo"** é calculada no front com a lista de módulos; se não há nenhum, mostre o aviso laranja "Inclua pelo menos um módulo para poder publicar" (a publicação exige).
- **`emiteCertificado` não muda depois de publicado** (desligar faria certificados já entregues responderem "não encontrado" na validação; ligar exporia conclusões antigas): em treinamento publicado a caixa abre desabilitada com a dica. Em treinamento sem certificado, o RH e a pessoa não veem certificado nem número.
- Esta etapa não tem rota própria: grava `emiteCertificado` no `PUT` do comando inteiro.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Guardar a caixa (comando inteiro) | `atualizarTreinamento` |
| Conferir quantos módulos há | `obterTreinamento` |

**Perguntas abertas (`x-provisorio`)**
- `CertificadoResumo.numero`: o formato do número do certificado é **decisão do dono de 2026-11-02**: fica `CERT-{ano}-{id com 5 dígitos}`, o que a API devolve (o Figma mostra `LGPD-2026-00482`, com sigla por treinamento, que o banco não guarda; `DUVIDAS.md`, "Decisões do dono"). Não muda nada nesta etapa.

---

## Tela 26: Novo treinamento, etapa Publicação (RH)

PNG: `Figma/telas/26-rh-treinamentos-novo-treinamento-etapa-publicacao.png`.

**Li no print**
- Etapa **Publicação** ativa. "Quem deve fazer": três cartões, **Todos** ("101 pessoas", escolhido), **Setores** ("Um ou mais setores"), **Pessoas escolhidas** ("Você marca quem faz"). "Quando publicar": "Publicar agora, com acesso liberado em 15/10" (escolhido) e "Salvar como rascunho".
- Três caixas marcadas: **"Incluir quem for admitido até o fim"**, **"Avisar por e-mail ao publicar"**, **"Lembrar quem não começou 7 dias antes do fim"**. Legenda da prévia: "Aparece em Meus treinamentos de 101 pessoas a partir de 15/10." Rodapé: "Salvar rascunho e sair", "Voltar", "Publicar treinamento".

**O que fica**
- Os três cartões de público (sem "por cargo"), as duas opções de "Quando publicar", a caixa de incluir quem for admitido, a legenda da prévia, "Salvar rascunho e sair", "Voltar" e "Publicar treinamento".

**O que sai ou muda (e por quê)**
- **Saem "Avisar por e-mail ao publicar" e "Lembrar quem não começou 7 dias antes do fim"** (T1 e T3: esta fatia não manda e-mail e não há fim; o contrato deixa `AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM` sem uso). O roadmap e o spec ainda falam desse lembrete: **vale o contrato**.
- **"Incluir quem for admitido até o fim"** vira **"Incluir quem for admitido depois"** (`incluiAdmitidos`; sem prazo, vale enquanto o treinamento não for encerrado). Só tem efeito em **obrigatório**: desabilite a caixa quando o treinamento é opcional.
- **"Publicar agora, com acesso liberado em 15/10"** depende de "Começa em" (etapa 1): com a data de hoje ou passada, "Publicar agora, com acesso liberado agora"; com data futura, "Publicar agora, com acesso liberado em dd/mm" (o treinamento fica `AGENDADO`). **"Salvar como rascunho"** não chama a rota de publicar (o `PUT` já guardou tudo): quando escolhido, o botão principal vira "Salvar rascunho" e só chama `atualizarTreinamento` (suponho).
- **Escolha de setores e de pessoas não está desenhada.** Descrição em texto: com "Setores", lista de setores com caixa de marcar (de `listarSetores`); com "Pessoas escolhidas", busca de pessoas (de `listarFuncionarios`) com as escolhidas como etiquetas removíveis (até 500). Em treinamento opcional, troque o rótulo "Quem deve fazer" por "Quem pode se inscrever" (suponho).
- **Contagem "Todos: 101 pessoas" e a legenda:** vêm de `contarPublicoDeTreinamento` (`publico`, `setorIds`, `funcionarioIds`; os arrays vão **repetindo o parâmetro**, `setorIds=1&setorIds=2`). Conta só pessoas **ativas cuja admissão já ocorreu**; quem está "Em admissão" entra depois pela caixa de incluir admitidos. Em opcional, escreva "Fica disponível em Meus treinamentos de N pessoas a partir de dd/mm, para quem quiser se inscrever" (suponho).
- **Para publicar o contrato exige** (400, `errors` por campo): `titulo`, `comecaEm`, `publico` escolhido (setor com pelo menos um, pessoas com pelo menos uma) e pelo menos um módulo (`errors.modulos`: "Inclua pelo menos um módulo."; `errors.comecaEm`: "Informe a data em que o acesso é liberado."). Mostre cada mensagem junto da etapa que a corrige.
- **Efeitos ao publicar:** `PUBLICADO` e, em obrigatório, uma linha `INSCRITO` para cada pessoa ativa do público. **Nada é enviado por e-mail.** Escreva na confirmação: "Treinamento publicado. Ninguém foi avisado por e-mail; para avisar, publique um Aviso." (texto meu). Com `AGENDADO`: "aparece em Meus treinamentos a partir de dd/mm".
- Publicar de novo: 422 `TREINAMENTO_JA_PUBLICADO` ("Este treinamento já foi publicado."). Não existe "despublicar".

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Contar o público escolhido | `contarPublicoDeTreinamento` |
| Escolher setores e pessoas | `listarSetores`, `listarFuncionarios` (fatia 1) |
| "Salvar rascunho e sair" e "Salvar rascunho" | `atualizarTreinamento` |
| "Publicar treinamento" | `publicarTreinamento` |
| Abrir o treinamento salvo | `obterTreinamento` |

**Estados e erros**
- 400 com `errors` (acima); 422 `TREINAMENTO_JA_PUBLICADO` e `FUNCIONARIO_INATIVO` (pessoa escolhida inativa); 404. Mostre o `detail`.

**Perguntas abertas (`x-provisorio`)**
- `atualizarTreinamento`: o que o RH muda depois de publicar (público, obrigatoriedade e certificado ficam travados porque já há linhas). `publicarTreinamento` não tem marca própria.

---

## Tela 41: Meus treinamentos, abas Todos, Disponíveis e Em andamento (funcionário; também gestor e RH)

PNG: `Figma/telas/41-func-treinamentos.png`.

**Li no print**
- Título "Meus treinamentos"; subtítulo "Os obrigatórios aparecem primeiro. Ao concluir, o certificado fica guardado aqui." Abas com número: **Todos 3**, Disponíveis 2, Em andamento 1, Concluídos 3. Busca "Buscar treinamento" e dois seletores: "Obrigatórios e opcionais" e "Todos os tipos".
- Três cartões com faixa colorida e ícone: (1) selo "Obrigatório", etiqueta "Em andamento", "Segurança da informação", descrição "Senhas, golpes por e-mail e cuidado com dados. Turma presencial em 23/10, às 14:00.", "5 módulos", "1h40", **"Até 15/11"** em laranja, barra 60%, botão "Continuar →"; (2) "Obrigatório", etiqueta **"Em breve"**, "Prevenção de assédio no trabalho", "3 módulos", "45 min", "Abre em 15/10", barra 0%, botão **desabilitado "Abre em 15/10"**; (3) selo "Opcional", etiqueta "Disponível", "Excel para relatórios financeiros", "**8 aulas**", "3h", **"De 20/10 a 20/11"**, barra 0%, botão "Quero me inscrever →". Nota no pé: "Os treinamentos que você já concluiu ficam na aba Concluídos."

**O que fica**
- Abas, busca, o seletor "Obrigatórios e opcionais" (`obrigatorio`), cartões (faixa, ícone decorativo, selo, etiqueta, título, descrição, "N módulos", duração, barra e botão), a nota do pé.

**O que sai ou muda (e por quê)**
- **Sai o cartão "Em breve / Abre em 15/10"** (e o botão "Abre em ..."): o treinamento só aparece quando a data de início chega (`listarMeusTreinamentos`; rascunho e agendado não vêm). **Saem "Até 15/11" e "De 20/10 a 20/11"** (T1; o selo do cartão é só "Obrigatório", "Opcional" ou "Onboarding" quando `onboarding` é verdadeiro). **Sai o filtro "Todos os tipos"** (T2). "aulas" vira **"módulos"** (`totalModulos`). A turma presencial do texto da descrição é dado de exemplo do Figma e não existe nos dados do contrato.
- **Abas** (`aba` = `TODOS`, `DISPONIVEIS`, `EM_ANDAMENTO`, `CONCLUIDOS`): "Todos" traz **tudo que ainda não foi concluído**; "Disponíveis" traz `DISPONIVEL` (opcional ainda não escolhido) e `NAO_INICIADO` (obrigatório com linha `INSCRITO`); contagens em `contagens` (respeitam busca e `obrigatorio`, ignoram a aba). Ordem fixa: obrigatórios primeiro, depois `comecaEm` mais antigo, depois título.
- **Botão por situação** (`TreinamentoMinhaSituacao`): `DISPONIVEL` → **"Quero me inscrever →"** (`inscreverMeEmTreinamento`; depois o cartão vira `NAO_INICIADO`); `NAO_INICIADO` → **"Começar →"** (`iniciarMeuTreinamento` e abre a tela de fazer); `EM_ANDAMENTO` → **"Continuar →"** (abre a tela de fazer).
- **Barra e duração:** `progressoPercentual` (módulos concluídos sobre o total, arredondado para baixo) e `duracaoTotalMinutos` ("1h40", "45 min"; sem duração em nenhum módulo, esconda).
- **Encerrado pelo RH:** cartão `EM_ANDAMENTO` com `encerrado` verdadeiro traz o aviso "Encerrado pelo RH; você ainda pode terminar" (texto do contrato). Quem estava `NAO_INICIADO` e o opcional não escolhido **somem** da lista. O cartão encerrado não conta como pendência.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Cartões, abas, busca, `obrigatorio`, páginas, contagens | `listarMeusTreinamentos` |
| "Quero me inscrever" | `inscreverMeEmTreinamento` |
| "Começar" | `iniciarMeuTreinamento` |
| "Continuar" abre a tela de fazer | `obterMeuTreinamento` (ver "Tela sem print: Fazer o treinamento") |

**Estados**
- Carregando (três cartões de esqueleto). Vazio por aba: "Nenhum treinamento por aqui" (Todos e Disponíveis), "Você não tem treinamento em andamento" (Em andamento), "Você ainda não concluiu nenhum treinamento" (Concluídos) (textos meus). Erro com `detail`. 422 `TREINAMENTO_ENCERRADO` ao começar ("O RH encerrou este treinamento. Fale com o RH se precisar dele."), 422 `TREINAMENTO_NAO_E_OPCIONAL`, 404.

**Observações para quem monta**
- A rota serve a qualquer perfil (gestor e RH também fazem treinamentos). O menu do RH (PNG 22) e o do gestor (PNG 58) **não têm** "Treinamentos" do funcionário (o item "Treinamentos" deles é a tela de gestão). **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Meus treinamentos"** existe no grupo **"Meu espaço"** do RH e do gestor e leva a `/eu/treinamentos`; o gestor chega à tela de fazer também pela faixa "Sua parte" da tela 58.

**Perguntas abertas (`x-provisorio`)**
- `listarMeusTreinamentos` não tem marca. `CertificadoResumo.numero` (tela 42) e a tela de execução (vídeo só por link) valem aqui só pela tela de fazer.

---

## Tela 42: Meus treinamentos, aba Concluídos (funcionário; também gestor e RH)

PNG: `Figma/telas/42-func-treinamentos-aba-concluidos.png`.

**Li no print**
- Aba **Concluídos 3** ativa. Três cartões: "Obrigatório" "LGPD na prática" (etiqueta "Concluído"; "Concluído com nota 9,0. Código do certificado: LGPD-2026-00482."; "4 módulos", "1h10"; "Concluído em 15/09/2026"; barra 100%); "Obrigatório" "Código de conduta 2025" ("Concluído com nota 10. Código do certificado: CDC-2025-00482."); selo **"Onboarding"** "Onboarding 2022" ("Boas-vindas, cultura da empresa e primeiros passos. Código: ONB-2022-00482."). Cada um com botões **"Ver certificado"** (contorno) e **"Rever conteúdo"** (cheio). Nota no pé: "Mostrando os treinamentos que você já concluiu."

**O que fica**
- A aba, os cartões como na tela 41 com etiqueta "Concluído", "Concluído em dd/mm/aaaa" (dia de `concluidoEmUtc`), barra 100%, selo "Onboarding" quando `onboarding` é verdadeiro, os botões "Ver certificado" e "Rever conteúdo".

**O que sai ou muda (e por quê)**
- **Sai a nota** ("Concluído com nota 9,0", "com nota 10"): sem nota (T2). O texto do cartão vira "Código do certificado: {`certificado.numero`}" e **mostra o número como vier** (hoje `CERT-2026-00482`; o PNG usa siglas por treinamento, que o banco não guarda: **decidido pelo dono em 2026-11-02** manter o número `CERT-{ano}-{id}` que a API devolve, sem sigla; `DUVIDAS.md`, "Decisões do dono").
- **Treinamento que não emite certificado** (`emiteCertificado` falso): `certificado` vem vazio; o cartão **não mostra o código nem "Ver certificado"**, só "Rever conteúdo".
- "Ver certificado" baixa o PDF por link temporário (`baixarMeuCertificado`; o PDF traz o código de validação). "Rever conteúdo" abre a tela de fazer em modo de revisão (`obterMeuTreinamento`; os módulos abrem por `obterConteudoDeModulo`, mesmo com o treinamento encerrado).
- Contagem da aba: `contagens.concluidos` (3).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Lista da aba (`aba=CONCLUIDOS`) | `listarMeusTreinamentos` |
| "Ver certificado" (link temporário) | `baixarMeuCertificado` |
| "Rever conteúdo" | `obterMeuTreinamento` |

**Estados**
- Vazio: "Você ainda não concluiu nenhum treinamento". Carregando e erro como na tela 41. 404 em `baixarMeuCertificado` (sem certificado): trate sem quebrar a tela.

**Perguntas abertas (`x-provisorio`)**
- `baixarMeuCertificado` e `CertificadoResumo.numero`: modelo visual do certificado e formato do número.

---

## Tela 58: Treinamentos do setor (gestor)

PNG: `Figma/telas/58-gestor-treinamentos-da-equipe.png`.

**Li no print**
- Título "Treinamentos do setor"; subtítulo "Veja quem do setor Comercial já fez cada treinamento". Menu do gestor (mesmo da tela 57; "Treinamentos" ativo).
- Faixa laranja **"Sua parte: segurança da informação"**, "Você está no módulo 2 de 5. O prazo é 15/11." e botão "Continuar treinamento".
- Coluna esquerda "Treinamentos do setor" com três cartões: "Obrigatório, online" "LGPD na prática" ("6 de 8 concluíram", "Até 31/10", selecionado); "Obrigatório, híbrido" "Segurança da informação" ("3 de 8 concluíram", "Até 15/11"); "Opcional, presencial" "Atendimento ao cliente" ("6 de 8 inscritos", "Começa em 03/11"). Cada um com barra.
- Painel direito: legenda pequena "Quem fez", título "LGPD na prática", **"Termina com quiz e certificado."**, botão **"Lembrar quem falta por e-mail"**; cartões "Concluíram 6" e "Faltam concluir 2"; lista: iniciais, nome, cargo (e "Vendedora, de férias até 17/10"), à direita "em 20/08" + selo "Concluído", "faltam 24 dias" + selo "Não iniciado", "módulo 3 de 4" + selo "Em andamento".

**O que fica**
- Título, faixa "Sua parte" (quando existir), a coluna "Treinamentos do setor" com seleção, o painel com os dois cartões e a lista, as marcas "em dd/mm", "módulo X de Y", "de férias até dd/mm".

**O que sai ou muda (e por quê)**
- **Sai "Lembrar quem falta por e-mail"** (T3: esta fatia não envia e-mail; o contrato diz "Não há botão"). Ao contrário da tela 57, aqui **não há** hipótese a confirmar: o botão não existe.
- **Saem "Até 31/10", "Até 15/11", "O prazo é 15/11" e "faltam 24 dias"** (T1). Linha "Não iniciado" mostra só o selo. O agendado mostra **"Começa em dd/mm"** (`comecaEm`).
- **Saem "online", "híbrido" e "presencial"** das linhas pequenas (T2): fica só "Obrigatório" ou "Opcional". O PNG mostra "6 de 8 inscritos" num opcional **agendado**: não existe (opcional só tem linha de quem se inscreveu e ninguém o vê antes da data; no exemplo do contrato, 0 de 8).
- **Sai "Termina com quiz e certificado."** A lista da equipe (`TreinamentoGestorResumo`) **não traz `emiteCertificado`**, então a frase não tem como ser montada: deixe só "Obrigatório" ou "Opcional" sob o título (suponho). O gestor **não vê nota nem certificado e não baixa certificado**.
- **Faixa "Sua parte"** é montada com o primeiro item **obrigatório**, com `encerrado` falso e `situacao` `NAO_INICIADO` ou `EM_ANDAMENTO`, de `listarMeusTreinamentos`: "Você está no módulo `moduloAtual` de `totalModulos`." e o botão "Continuar treinamento" leva à tela de fazer. Sem esse item, a faixa não aparece. Se `NAO_INICIADO`, o botão pode dizer "Começar treinamento" (suponho).
- **Lista da esquerda:** treinamentos **publicados** (agendados, em andamento e encerrados) que alcançam pelo menos uma pessoa do setor; rascunho não aparece. A equipe **não inclui o próprio gestor** (o que é dele vem de `listarMeusTreinamentos`). Obrigatórios primeiro, depois `comecaEm` mais antigo; sem paginação. Obrigatório: "`concluiram` de `totalNaEquipe` concluíram"; opcional: "`inscritos` de `totalNaEquipe` inscritos".
- **Lista de pessoas** (`listarPessoasDaEquipeNoTreinamento`): só pessoas do setor **com linha** no treinamento, em ordem de nome (a ordem do PNG não é a do contrato). `INSCRITO` → selo "Não iniciado"; `EM_ANDAMENTO` → "módulo `moduloAtual` de `totalModulos`" e selo "Em andamento"; `CONCLUIDO` → "em dd/mm" (dia de `concluidoEmUtc`) e selo "Concluído"; `emFeriasAte` vira ", de férias até dd/mm" ao lado do cargo.
- Os dois cartões do topo do painel são `concluiram` e `faltamConcluir` (com linha e ainda não concluíram).

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Coluna "Treinamentos do setor" | `listarTreinamentosDaEquipe` |
| Painel "Quem fez" do cartão selecionado | `listarPessoasDaEquipeNoTreinamento` |
| Faixa "Sua parte" e botão "Continuar treinamento" | `listarMeusTreinamentos`, depois `obterMeuTreinamento` |
| Nome do setor no subtítulo | `obterUsuarioLogado` (traz `setor`; fatia 0) |

**Estados**
- Carregando. Lista vazia: "Nenhum treinamento alcança o seu setor". Painel sem cartão escolhido: selecione o primeiro automaticamente. Opcional agendado sem inscritos: "Ninguém da equipe tem este treinamento ainda" (suponho). 404 do painel (rascunho ou que não alcança o setor): volte à lista com o `detail`. 403: só gestor.

**Observações para quem monta**
- Esta tela e a do funcionário usam o mesmo cartão de treinamento; reaproveite o componente de cartão e de barra.
- Não há hipótese pendente nesta tela (sem `x-provisorio` nas duas rotas do gestor).

---

## Tela sem print: Fazer o treinamento (todos os perfis)

**Descrição em texto** (contrato: "A tela de execução do treinamento não existe no Figma ... O contrato supõe uma tela simples: lista de módulos com o andamento, o conteúdo do módulo escolhido e o botão 'Concluir módulo'"). Caminho `/eu/treinamentos/:treinamentoId`. É o que abre em "Começar", "Continuar" e "Rever conteúdo".

- **Cabeçalho:** selo ("Obrigatório", "Opcional" ou "Onboarding"), título, descrição, "N de M módulos concluídos" com barra (`progressoPercentual`), duração total e, se `encerrado`, a faixa "Encerrado pelo RH; você ainda pode terminar".
- **Coluna dos módulos:** lista numerada (`modulos`): título, tipo (vídeo, texto, link, PDF), duração e uma marca de concluído com o dia (`concluidoEmUtc`). **Os módulos podem ser feitos em qualquer ordem** (não trave nenhum).
- **Área do conteúdo** do módulo escolhido (`obterConteudoDeModulo`):
  - **Texto:** mostra `conteudoTexto` (texto ou markdown, nunca HTML; componente seguro da fundação).
  - **Vídeo e link:** um botão "Abrir vídeo" ou "Abrir link" que abre `urlExterna` em **nova aba, com `rel="noopener noreferrer"`**. A política de segurança do site (`default-src 'self'`) **impede incorporar** vídeo ou página externa: nunca use `iframe` nem `video` com endereço de fora. Escreva "O vídeo abre em outra aba." (texto meu).
  - **PDF:** botão "Abrir PDF" que chama `baixarArquivoDeModulo` e abre o link temporário em nova aba (vem como anexo).
- **"Concluir módulo":** botão que chama `concluirModuloDeTreinamento`; repetir é inofensivo (módulo já concluído devolve o estado atual, sem erro). Concluído: no lugar do botão, "Módulo concluído em dd/mm". Quando era o último que faltava, a resposta traz `treinamentoConcluido` verdadeiro: mostre o painel "Você concluiu {título}!" com, se houver `certificado`, o número, "Ver certificado" (`baixarMeuCertificado`) e o aviso de que ele fica guardado em "Meus treinamentos". **Não existe "concluir treinamento"** nem tela final de quiz ou nota.
- **Começar:** "Começar" da lista chama `iniciarMeuTreinamento` e abre esta tela; concluir o primeiro módulo também inicia quem estava `INSCRITO`. **Rever conteúdo** (treinamento concluído ou encerrado, para quem começou ou concluiu): mesma tela sem o botão de concluir nos módulos já concluídos.

**Rotas**

| O que a tela faz | `operationId` |
|---|---|
| Abrir (cartão e lista de módulos) | `obterMeuTreinamento` |
| Inscrever-se (opcional) e começar | `inscreverMeEmTreinamento`, `iniciarMeuTreinamento` |
| Conteúdo de um módulo | `obterConteudoDeModulo` |
| PDF do módulo | `baixarArquivoDeModulo` |
| "Concluir módulo" | `concluirModuloDeTreinamento` |
| "Ver certificado" | `baixarMeuCertificado` |

**Estados e erros**
- 404 (sem linha e fora do público, não publicado, ainda não começou, ou encerrado em que a pessoa não começou nem concluiu): "Treinamento não encontrado." (texto meu).
- 422 `INSCRICAO_PENDENTE` ("Inscreva-se no treinamento antes de começar."): mostre o botão "Quero me inscrever" ali mesmo.
- 422 `TREINAMENTO_ENCERRADO` ao começar ou concluir quem ainda não tinha começado ("O RH encerrou este treinamento. Fale com o RH se precisar dele.").
- Depois de concluir um módulo, atualize o cartão e a lista de módulos com a resposta (`treinamento`), e invalide a lista "Meus treinamentos".

**Perguntas abertas (`x-provisorio`)**
- `obterConteudoDeModulo`, `baixarArquivoDeModulo`: onde ficam os vídeos (hoje só link externo). `concluirModuloDeTreinamento`, `baixarMeuCertificado`: formato do número e modelo do PDF.

---

## Tela sem print: Conferir um certificado (pública)

**Descrição em texto** (contrato `validarCertificado`: "Não existe tela para isso no Figma; sugere-se uma página pública simples com um campo de código e o resultado"). Caminhos `/certificado` (campo de código) e `/certificado/:codigo` (resultado direto, para o endereço que o PDF traz). **Fora da área logada e sem menu.**

- **Cabeçalho:** "Conferir certificado" e uma linha "Digite o código impresso no certificado." Campo "Código de validação" (UUID) e botão "Conferir". Valide o formato UUID no navegador antes de chamar (400 se mal formado: "Digite o código completo, como está no certificado.", texto meu).
- **Resultado válido (200):** cartão "Certificado válido" com **o mínimo que a API devolve**: nome de quem concluiu (`titularNome`), o treinamento (`treinamentoTitulo`), a data de emissão (`emitidoEm`, dd/mm/aaaa) e o código (`codigoValidacao`). **Nenhum outro dado** (sem CPF, setor, nota, número sequencial).
- **Não encontrado (404):** "Certificado não encontrado" com o `detail` do contrato: "Não encontramos um certificado com este código." A mesma resposta vale para código inexistente, de treinamento que não emite certificado e de conclusão sem certificado. **Não existe "certificado apagado"**: um código válido continua válido, mesmo com o treinamento encerrado.
- **429** (limite de 10 consultas por minuto por IP; cabeçalho `Retry-After`): "Muitas consultas. Tente de novo em alguns minutos." (texto meu).

**Rotas:** `validarCertificado` (pública, `security: []`).

**Segurança**
- Chame a API **sem** `Authorization` e sem o ciclo de renovação de sessão; não leia nem grave nada da sessão do site. Sem recurso externo (fonte e ícones do próprio site). O código é um UUID v4 aleatório (não enumerável); não ofereça "próximo código" nem lista.

**Perguntas abertas (`x-provisorio`)**
- `validarCertificado`: modelo visual do certificado e o formato do número; o PDF deve trazer o código e o endereço desta página (o texto exato é do back-end).

---

## Tela 52 (aba Treinamentos da ficha da equipe, gestor)

PNG: `Figma/telas/52-gestor-painel-da-equipe-ficha.png` (mostra a ficha na aba **Dados**; as abas Dados, Ponto, Férias, **Treinamentos** e Advertências aparecem no alto, mas só a primeira está desenhada).

**Li no print:** na aba Dados, o cartão **"Treinamentos pendentes 2"** ao lado de Banco de horas, Férias disponíveis e Advertências. A aba **Treinamentos** não tem print. O roadmap diz que "as outras abas chegam nas fatias 3, 4 e 6" (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, fatia 1, linha das telas): **esta aba é peça da fatia 6**.

**Descrição em texto** (contrato `listarTreinamentosDePessoaDaEquipe`):
- Três números no topo (`resumo`: `pendentes`, `emAndamento`, `concluidos`) e a lista de treinamentos da pessoa, sem paginação: obrigatórios primeiro, depois `comecaEm` mais antigo. Cada linha: título, selo "Obrigatório", "Opcional" ou "Onboarding", situação ("Concluído", "Em andamento", "Não iniciado", de `situacao`), "módulos `modulosConcluidos` de `totalModulos`", início (`iniciadoEm`) e conclusão (dia de `concluidoEmUtc`).
- **Sem certificado** (o gestor não baixa), sem nota, sem data final. Pessoa de outro setor ou o próprio gestor: 404.
- O cartão "Treinamentos pendentes" da aba Dados (fatia 1) **continua lendo `treinamentosPendentes` de `obterIndicadoresDaEquipe`**, como a 1b o fez (`telas\fatia-1.md`, tela 52: "o front lê só esta rota"). O contrato já calcula lá, e diz que o número sai do `resumo.pendentes` desta rota ("é desta rota que a fatia 1 tira `treinamentosPendentes`"): isso é **assunto do back-end**. **A fatia 6 não troca a rota** e o front não soma nem copia o número de uma rota para a outra. Nos dados simulados os dois exemplos trazem 2 (lidos em `20-fatia-1-colaboradores.yaml` e `60-fatia-6-tad.yaml`); se um dia divergirem, é do exemplo do contrato, não do front.

**Rotas:** `listarTreinamentosDePessoaDaEquipe`.

**Estados:** lista vazia: "Esta pessoa ainda não tem treinamentos"; 404 volta à lista da equipe; carregando e erro como nas outras abas.

---

## Ligações com outras telas (sem tela própria nesta fatia)

- **Caixa "Incluir no treinamento Onboarding 2026"** (tela 02, confirmação da importação e diálogo "Readmitir" da ficha, fatias 1b e 2; o roadmap a manda deixar **desligada até esta fatia**: nas três ela está visível, desmarcada e desabilitada, com a dica "Indisponível por enquanto", e envia `incluirNoOnboarding: false`). **A ficha 6 (bloco 7) liga as três telas de uma vez** (cadastro, importação **e** `readmitirFuncionario`, o diálogo "Readmitir" da ficha, tela 03, que a 1b deixou igual), no fim da fatia (na importação, o resultado passa a mostrar "Incluídos no onboarding: N", `incluidosNoOnboarding`): a lista vem de `listarTreinamentosDeOnboarding` (treinamentos de onboarding em vigor: publicados e não encerrados; podem estar agendados). Um item: "Incluir no treinamento {título}". Mais de um: "Incluir nos treinamentos de onboarding" (inclui em todos). Lista vazia: caixa desligada e desabilitada com a dica "Nenhum treinamento de onboarding publicado". Marcada, o cadastro (`criarFuncionario`), a importação (`confirmarImportacao`) e a readmissão (`readmitirFuncionario`) mandam `incluirNoOnboarding: true`; a API inscreve a pessoa **por dentro**, com o mesmo efeito de `incluirFuncionarioNoOnboarding`. **Nenhuma tela chama essa rota diretamente**, **sem botão** na ficha (o Figma não o desenha; o contrato diz que a rota serve para incluir depois uma pessoa já cadastrada, "por exemplo, a partir da ficha": é a dúvida `D6-4` da ficha 6, padrão sem botão) e nenhum e-mail sai. Sem onboarding em vigor, o cadastro inteiro é recusado com 422 `SEM_TREINAMENTO_DE_ONBOARDING` (tudo ou nada). Pessoa inativa: 422 `FUNCIONARIO_INATIVO`. Pergunta aberta: `x-provisorio` de `listarTreinamentosDeOnboarding` e `incluirFuncionarioNoOnboarding` (como o RH marca qual é o treinamento de onboarding).
- **Aba Treinamentos da ficha do RH** (`listarTreinamentosDeFuncionario`): o contrato a oferece, mas o Figma **não a desenha** para o RH e o spec (seção 13.3) lista as abas da ficha do RH sem ela (Dados, Ponto, Férias, Holerites, Documentos e Advertências). **Não construa**: **decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D6-2`) que a ficha do RH **não** ganha a aba Treinamentos. Registre "rota sem tela (decisão do dono)" em `PROGRESSO.md`; a rota fica na exclusão da cobertura, com esse motivo.
- **Painel do RH** (fatia 7, tela 00): o cartão "Treinamentos em andamento" usa `/api/rh/painel` e o link "Gerenciar treinamentos" leva à tela 22; nada a construir aqui.
