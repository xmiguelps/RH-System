# Notas de tela da fatia 4: comunicação e documentos

Fontes lidas: `contrato\fontes\40-fatia-4-comunicacao-e-documentos.yaml` (todas as 35 operações e os schemas), `contrato\CONVENCOES.md` (seções 3, 5, 6, 11 e 12), `contrato\ELENCO.md`, `contrato\PROVISORIOS.md`, o design dos planos (seção 7), o roadmap (seção "Fatia 4: comunicação e documentos"), o spec (13.3), os 16 PNG da fatia em `Figma\telas\` e, só para ver menu e abas, os PNG 46 e 52.

Marcas usadas: **(li)** = li no PNG ou no arquivo; **(contrato)** = está no contrato; **(desenho meu)** = a tela não existe no Figma e a descrição é a proposta deste plano, em estilo simples; **(suponho)** = inferência minha, a confirmar na conferência visual.

**Caminhos de tela** (onde cada tela mora, o que é painel e o que é modal, de onde cada uma abre): `05-caminhos-de-tela.md`, a tabela única. Os pontos desta fatia: avisos do RH `/rh/avisos` (aviso selecionado em `?aviso=<id>`; painel "Novo aviso" em `/rh/avisos/novo` e `/rh/avisos/:avisoId/editar`); avisos da pessoa `/eu/avisos` e o aviso aberto em `/eu/avisos/:avisoId`; holerites da pessoa `/eu/holerites`; advertências do RH `/rh/advertencias`, `/rh/advertencias/nova` e `/rh/advertencias/:advertenciaId`; da pessoa `/eu/advertencias`; do gestor `/gestor/advertencias`, `/gestor/advertencias/nova` e `/gestor/advertencias/:advertenciaId`; as abas Holerites e Advertências da ficha do RH são `?aba=holerites` e `?aba=advertencias`.

Cobertura: **16 telas com print**, **7 telas em texto**, **35 de 35 rotas** listadas (34 com tela nesta fatia e 1, `listarAniversariantes`, com a tela na fatia 7d; conferência no fim).

---

## Regras comuns da fatia 4

Valem para todas as telas abaixo. Os componentes devem nascer **uma vez**, nesta fatia, e ser reutilizados (a 36 e a 49, a 37 e a 50 e a 53 e a 60 são as mesmas telas).

### Datas e horas

- A API fala UTC com `Z`; a tela mostra em `America/Sao_Paulo`. Formatos: `dd/MM` ("07/10"), `dd/MM/yyyy` ("05/10/2026"), `dd/MM, HH:mm` ("Publicado em 07/10, 08:00").
- `data` e `dataDoOcorrido` (tipo `date`) são o dia como veio: **não** passam por conversão de fuso.
- `expiraEmUtc` de aviso é o **fim** do último dia (`...T02:59:59Z` = 23:59:59 do dia anterior em UTC): formate pelo fuso, senão "expira em 20/10" vira 21/10. `publicacaoEmUtc` de holerite agendado é o começo do dia (`...T03:00:00Z`).
- O que a pessoa digita em um campo de data e hora ("Publicar em") vira UTC pelo fuso fixo `-03:00`: `2026-10-09T09:00:00-03:00` vira `2026-10-09T12:00:00Z`. Use o auxiliar de "horário do dia mais data para instante" que a **fatia 3 cria** (`src\shared\formato\datas.ts`; ficha 3, passo 1: a 1a e a 1b não o têm): **não o recrie**; se a fatia 3 ainda não foi feita, crie-o **uma vez** nesse arquivo, com teste, e a 3 o reaproveita.
- Mês por extenso em português, com inicial maiúscula: "Setembro de 2026".

### Rótulos e chips

| Valor da API | Texto na tela |
|---|---|
| `AvisoTipo` `SIMPLES` e `DETALHADO` | "Simples" (chip azul-claro cheio) e "Detalhado" (chip só com borda) (li nos PNG 27 e 37) |
| `AvisoSituacao` | abas: "Ativos", "Agendados", "Expirados", "Rascunhos" (li no PNG 27) |
| `HoleriteTipo` `MENSAL`, `FERIAS`, `DECIMO_TERCEIRO` | "Mensal", "Férias", "13º salário" (li nos PNG 36 e 49) |
| `AdvertenciaTipo` `VERBAL`, `ESCRITA` | "Verbal", "Escrita" (li nos PNG 11, 14 e 43) |
| `AdvertenciaSituacao` `PENDENTE` | "Aguardando ciência" (âmbar); no cartão da pessoa, "Aguardando sua ciência" |
| `AdvertenciaSituacao` `APLICADA` | "Ciente" (verde-azulado) |
| `AdvertenciaSituacao` `CONTESTADA` | "Contestada" (vermelho claro) |
| `AdvertenciaSituacao` `CANCELADA` | "Cancelada" (cinza; suponho: não há no Figma) |
| `AdvertenciaAplicadaPor` `tipo: GESTOR` | o `nome` do gestor ("Carlos Ribeiro"); no cartão da pessoa, "Carlos Ribeiro, seu gestor" (li no PNG 43) |
| `AdvertenciaAplicadaPor` `tipo: RH` | "RH" (o `nome` vem `null`) |

### Rótulo da referência de um holerite

- `MENSAL`: "{Mês} de {ano}" ("Setembro de 2026").
- `FERIAS`: "Férias de {mês} de {ano}" ("Férias de janeiro de 2026"; li no PNG 36).
- `DECIMO_TERCEIRO`: "13º salário de {ano}" (suponho; nenhum PNG mostra).

### Conteúdo escrito por pessoas (avisos e advertências)

Aviso simples é texto puro; aviso detalhado é markdown com seções (`##`); descrição e motivo de advertência são texto. **HTML nunca é interpretado.** Mostre sempre como texto do React (que escapa); nunca `dangerouslySetInnerHTML`. O aviso **detalhado** (markdown) se mostra com o componente **`Markdown`** da 1a (`src\shared\ui\Markdown.tsx`, `react-markdown` 10.1.0; `fatia-1a-fundacao.md`, Task 7): ele só aceita os elementos da lista segura, **não** gera HTML a partir do texto e rebaixa `##` para título `<h3>` (os exemplos do contrato usam só `##` e parágrafos). **Use-o; não escreva outro renderizador.** O aviso **simples** é texto puro: `<p className="whitespace-pre-line">`, sem passar pelo `Markdown`, e as quebras de linha são preservadas.

### Estados de qualquer tela desta fatia

- **Carregando**: esqueleto da lista ou do cartão; ações desligadas.
- **Vazio**: texto curto no lugar da lista (cada tela diz o seu).
- **Erro** (rede ou 500): "Não foi possível carregar. Tente de novo." com botão "Tentar de novo" e o `traceId` em letra pequena.
- **Sem permissão**: 401 leva ao login (fundação); 403 mostra "Você não tem permissão para ver esta tela."; 404 mostra "Não encontrado." (vale também para "fora do escopo", como o gestor pedindo alguém de outro setor).

### Depois de uma ação

Invalide a lista e o número do topo da própria tela e, quando mexe em aviso lido ou publicado, as bolinhas do menu com `invalidarContadoresDoMenu` (a fundação 1a já lê `obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoRh`; os valores reais chegam quando a API existir). O número "Avisos 2" do menu é `contagens.naoLidos` de `listarMeusAvisos`.

### Arquivos

- **Envio** de holerite: `multipart/form-data`, só PDF, até 5 MB (5 × 1024 × 1024 bytes). Antes de enviar, a tela confere extensão e tamanho **só por conforto**; a API confere o conteúdo e decide.
- **Download** (`LinkTemporario`; só **arquivo guardado**, como o PDF de holerite, vem por link): chame a rota, receba `{ url, expiraEmUtc }` e abra a `url` na hora, como anexo, com `abrirLinkTemporario(link)` da fundação (`src\shared\arquivos\baixar.ts`, o mesmo auxiliar do "Ver" documento da 1b). Nunca monte nem guarde a `url`; ela vence em minutos.

### O que esta fatia nunca constrói

Público por setor ou "Para quem" em aviso; "Para: ..." nas listas; anexo PDF de aviso; "Enviar por e-mail também"; chip "Novo" ou marca de "visto" em holerite; "Avisar ... por e-mail" em holerite e em advertência; texto da contestação ou resposta do RH na tela do gestor; holerites da equipe para o gestor; excluir aviso já publicado.

---

## Tela 27: Avisos (RH)

**PNG:** `Figma\telas\27-rh-avisos.png` · **Incremento:** 4a · **Rotas:** `listarAvisos`, `obterResumoDosAvisos`, `obterAviso`, `encerrarAviso`, `listarLeiturasDoAviso`, `lembrarQuemNaoViuAviso`; sem print: `publicarAviso`, `excluirAviso`.

**O que fica (li no PNG 27)**
- Título "Avisos" e botão principal **"+ Novo aviso"** (abre a tela 28).
- **Quatro números** (`obterResumoDosAvisos`): "Ativos" (`contagens.ativos`, 4), "Agendados" (`contagens.agendados`, 1), "Leitura média dos ativos" (`leituraMediaDosAtivosPercentual`, "75%"; `null` vira "—") e "Expiram nesta semana" (`expiramNosProximos7Dias`, 1, âmbar, com o aviso de `proximoAExpirar` embaixo: "{título}, em {dd/MM}"; o PNG mostra "Vacinação, em 15/10").
- **Abas** "Ativos 4", "Agendados 1", "Expirados 12" e "Rascunhos 1" (os números são `contagens` e **não** mudam com a busca; cada aba é um valor de `situacao`; padrão "Ativos"), campo **"Buscar aviso"** (`busca`: título e conteúdo) e seletor **"Todos os tipos"** (`tipo`: Simples, Detalhado).
- **Lista de avisos** (cartões, na ordem da API): chip do tipo, título em negrito, a prévia (`previa`, cortada), a linha "Publicado em 07/10, 08:00, expira em 20/10" (`publicacaoEmUtc` e `expiraEmUtc`; sem expiração: "não expira"), à direita "41 de 101 viram" (`leitura.viram` de `leitura.total`) e uma barra de progresso. O cartão selecionado tem borda e fundo destacados; o primeiro vem selecionado.
- **Painel de detalhe** à direita do aviso selecionado (`obterAviso`, que traz o `conteudo` inteiro): chip do tipo; botões **"Editar"** (contornado) e **"Encerrar agora"** (contornado em vermelho); título grande; "Publicado por Ana Souza em 06/10, 09:00. Expira em 06/11." (`autorNome`, `publicacaoEmUtc`, `expiraEmUtc`); o conteúdo (no detalhado, as seções com título em negrito).
- **"Quem viu"** (só em aviso `ATIVO` ou `EXPIRADO`): dois números "Viram" (`viram`, 73) e "Ainda não viram" (`aindaNaoViram`, 28, âmbar); os cinco primeiros que **ainda não viram** (avatar, nome e setor), de `listarLeiturasDoAviso` com `situacao=NAO_LIDO` e `tamanhoPagina=5`; a frase "E mais 23 pessoas." (`total` menos 5, só se for maior que zero); e o botão principal **"Lembrar quem não viu"**.

**O que sai ou muda (com a razão)**
- **"ou para setores" sai do subtítulo** (PNG: "Comunicados para a empresa toda ou para setores"): todo aviso é **para todos** (decisão do dono, 2026-10-28; roadmap, fatia 4). Use "Comunicados para a empresa toda".
- **"Para: Todos" e "Para: Logística" saem** dos cartões e do painel (não há público por setor; o contrato não tem o campo).
- **"Baixar a política completa (PDF)" sai** do painel: não há anexo PDF (decisão do dono, 2026-10-28).
- **Ações que o Figma não desenha e a tela precisa (contrato):**
  - Aviso `RASCUNHO`: botões **"Editar"**, **"Publicar"** (`publicarAviso`; sem corpo; usa a data prevista guardada: no passado ou vazia publica agora, no futuro fica agendado) e **"Excluir"** (`excluirAviso`).
  - Aviso `AGENDADO`: **"Editar"** e **"Excluir"**.
  - Aviso `ATIVO`: **"Editar"** e **"Encerrar agora"**. Aviso `EXPIRADO`: **"Editar"** (nova data de expiração futura volta a deixá-lo `ATIVO`).
  - **Aviso publicado não se exclui**, só se encerra (422 `AVISO_NAO_PODE_SER_EXCLUIDO`).
- **Diálogos de confirmação** (desenho meu): "Encerrar este aviso agora? Ele sai do ar para todos. As leituras já feitas continuam na contagem." e "Excluir este aviso? Ninguém o viu ainda. Esta ação não pode ser desfeita."
- **Rascunho e agendado** não têm "viram" nem "Quem viu" (`leitura: null`; `listarLeiturasDoAviso` daria 422 `AVISO_AINDA_NAO_PUBLICADO`): mostre no lugar o texto "Este aviso ainda não foi publicado." ou, no agendado, "Será publicado em {dd/MM, HH:mm}." Na lista, no lugar de "viram": rascunho "Rascunho" (com "Publicação prevista dd/MM, HH:mm" se houver data); agendado "Publica em dd/MM, HH:mm". Aviso expirado: "Publicado em ..., expirou em ...".
- **"Lembrar quem não viu"** só em aviso `ATIVO` (422 `AVISO_NAO_ESTA_ATIVO` nos outros); depois de enviar, "Lembrete na fila para {quantidadeDeEmails} pessoas." (202; 0 vira "Todos já viram este aviso."). **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-F4-5`): o botão **fica**, **sem limite** de lembretes por aviso; o e-mail entrou na lista fechada (`CONVENCOES.md`, seção 12, contrato 0.2.1) e a rota **não é mais provisória**. Construa o botão normalmente.
- **Aviso fixado** (`fixado`): o PNG não mostra marca; mostre um rótulo pequeno "Fixado" no cartão (suponho) para o RH saber o que está no topo da tela inicial.
- **"Leitura média 75%"** do PNG é dado de exemplo (o exemplo do contrato tem 74): o número vem da API.
- O PNG mostra "Publicado em 07/10, 08:00, expira em 20/10" para o aviso 412 e o exemplo do contrato o publica às 11:00 UTC (08:00 em Brasília): confere.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Quatro números e contagem das abas | `obterResumoDosAvisos` | não depende de busca nem de filtro |
| Lista, abas, busca e tipo | `listarAvisos` | `situacao` (padrão `ATIVO`), `tipo`, `busca`, `pagina`, `tamanhoPagina` |
| Painel de detalhe | `obterAviso` | qualquer situação, inclusive rascunho |
| "Editar" | `editarAviso` | abre o painel da tela 28 em modo de edição |
| "Encerrar agora" | `encerrarAviso` | sem corpo; 422 `AVISO_NAO_ESTA_ATIVO` |
| "Publicar" (rascunho) | `publicarAviso` | sem corpo; 422 `AVISO_JA_PUBLICADO`; 400 se a expiração ficou antes da publicação |
| "Excluir" (rascunho e agendado) | `excluirAviso` | 204; 422 `AVISO_NAO_PODE_SER_EXCLUIDO` |
| "Quem viu" | `listarLeiturasDoAviso` | `situacao=NAO_LIDO`, `tamanhoPagina=5`; 422 `AVISO_AINDA_NAO_PUBLICADO` |
| "Lembrar quem não viu" | `lembrarQuemNaoViuAviso` | 202 com `quantidadeDeEmails` |

**Estados**
- Carregando: esqueleto nos números, na lista e no painel.
- Vazio: aba sem avisos "Nenhum aviso aqui." (com a busca: "Nenhum aviso encontrado para esta busca."); painel sem seleção "Escolha um aviso para ver os detalhes."
- Erro: nas regras comuns. 404 ao abrir um aviso que sumiu: "Este aviso não existe mais." e recarregue a lista.
- Sucesso: toasts "Aviso encerrado.", "Aviso publicado." (ou "Aviso agendado para dd/MM, HH:mm."), "Aviso excluído."

**Textos e mensagens (contrato)**
- 422 `AVISO_NAO_ESTA_ATIVO` "Só um aviso ativo pode ser encerrado." (no lembrete: "Só é possível lembrar sobre um aviso ativo."); 422 `AVISO_NAO_PODE_SER_EXCLUIDO` "Só é possível excluir um aviso que ainda não foi publicado. Para tirar um aviso do ar, encerre-o."; 422 `AVISO_JA_PUBLICADO` "Este aviso já foi publicado."; 422 `AVISO_AINDA_NAO_PUBLICADO` "Este aviso ainda não foi publicado."

**Observações para quem monta**
- Os números e as barras **vêm prontos**; não calcule porcentagem no front.
- A lista e o painel se atualizam depois de qualquer ação; o aviso selecionado continua selecionado.
- A contagem de "viram" considera os ativos na criação do aviso mais quem entrou depois e abriu (contrato); o front só mostra.

**Perguntas abertas (`PROVISORIOS.md`)**
- Nenhuma nesta tela: o lembrete manual de aviso foi decidido em 2026-11-02 (o botão fica, sem limite) e saiu do `PROVISORIOS.md`.

---

## Tela 28: Novo aviso (RH, painel lateral)

**PNG:** `Figma\telas\28-rh-avisos-novo-aviso.png` · **Incremento:** 4a · **Rotas:** `criarAviso`, `editarAviso`.

**O que fica (li no PNG 28)**
- Painel lateral "Novo aviso" com botão fechar.
- **Tipo de aviso**: duas opções de rádio em cartões: **"Simples"** ("Título e uma mensagem curta"; selecionada) e **"Detalhado"** ("Texto longo, com seções").
- **Título** (placeholder "Ex.: inventário de estoque no sábado"; até 200 caracteres).
- **Mensagem** (caixa de texto; no simples o placeholder diz "Uma mensagem curta, de até 280 caracteres"; mostre um contador "0/280").
- **Publicar em** (data e hora; opcional: **vazio publica agora**, no futuro agenda) e **Expira em** (data; opcional: vazio não expira; é o **último dia** em que o aviso vale).
- Caixa **"Fixar no topo da tela inicial até expirar"** (`fixado`).
- Botões **"Salvar rascunho"** (contornado; `publicar: false`) e **"Publicar aviso"** (principal; `publicar: true`).

**O que sai ou muda (com a razão)**
- **"e anexo"** sai do texto do tipo Detalhado (PNG: "Texto longo, com seções e anexo"): não há anexo PDF (decisão do dono, 2026-10-28).
- **O campo "Para quem" (Todos os colaboradores) sai**: todo aviso é para todos (não há público por setor).
- **A caixa "Enviar por e-mail também" sai**: o e-mail para todos os ativos sai **sempre** na publicação, agora ou na data agendada (decisão do dono, 2026-10-28; contrato). Para o RH não ficar sem saber, mostre ao lado dos botões o texto "Ao publicar, todos os colaboradores ativos recebem um e-mail." (desenho meu).
- **Detalhado** (o PNG só mostra o estado Simples; descrição minha apoiada em `AvisoCriacaoComando`): o campo da mensagem vira **"Conteúdo"**, mais alto, até 10.000 caracteres, com a dica "Use ## no começo de uma linha para criar uma seção." e uma **prévia** do aviso renderizado ao lado ou abaixo (opcional, suponho). Sem HTML.
- **Editar** (mesmo painel, título "Editar aviso", vindo do botão "Editar" da tela 27): o **tipo não muda** (opções desligadas); "Publicar em" só se edita em rascunho e agendado (nos outros, desligado); aviso `EXPIRADO` com nova data de expiração futura volta a `ATIVO` (dica "Ao escolher uma nova data de expiração, o aviso volta a ficar ativo."); texto "Editar não envia o e-mail de novo." (contrato). Botões: em rascunho, **"Salvar rascunho"** (`editarAviso`) e **"Publicar aviso"** (`editarAviso` e depois `publicarAviso`); nos demais, **"Salvar alterações"** (`editarAviso`).
- "Publicar em" no PNG mostra "10/07/2026, 09:00 AM" no formato americano: use o formato do Brasil (dd/mm/aaaa, 24 horas).

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| "Salvar rascunho" e "Publicar aviso" (novo) | `criarAviso` | corpo `{ tipo, titulo, conteudo, publicarEmUtc, expiraEm, fixado, publicar }` |
| Modo editar | `editarAviso` | corpo `{ titulo, conteudo, publicarEmUtc, expiraEm, fixado }` |
| "Publicar aviso" em rascunho editado | `publicarAviso` | sem corpo |

**Estados**
- Salvando: botões desligados com "Salvando..."; sucesso fecha o painel, seleciona o aviso na tela 27 e mostra "Rascunho salvo.", "Aviso publicado." ou "Aviso agendado para dd/MM, HH:mm."
- Erros de campo (400, `errors`): simples com mais de 280 caracteres; `expiraEm` antes do dia da publicação (a API valida; o trigger do banco também recusa); título vazio.
- 422 `AVISO_PUBLICACAO_NAO_EDITAVEL` "Este aviso já foi publicado; a data de publicação não pode mudar." (editar um aviso já publicado enviando uma data de publicação diferente: a tela não deixa mudar, então só aparece se a lista estiver velha).

**Observações para quem monta**
- Converta "Publicar em" para UTC pelo fuso `-03:00` (regras comuns); `expiraEm` vai como data (`yyyy-MM-dd`).
- **Nunca** interprete HTML. Teste com `<script>` e `<img onerror=...>` no conteúdo e no título.
- Reaproveite o mesmo painel para criar e editar.

---

## Tela 37: Avisos (funcionário)

**PNG:** `Figma\telas\37-func-avisos.png` · **Incremento:** 4a · **Rotas:** `listarMeusAvisos` (abre a tela 38: `obterMeuAviso`, `marcarAvisoComoLido`).

**O que fica (li no PNG 37)**
- Título "Avisos". Abas **"Não lidos 2"** e **"Todos 5"** (os números são `contagens.naoLidos` e `contagens.total`, que **não** mudam com a busca nem com a aba; a aba **"Todos" é a escolhida ao abrir** (li nos PNG 37 e 50: é a aba clara e em relevo; sem `?aba=` na URL, vale "Todos"; `?aba=nao-lidos` escolhe a outra, `05-caminhos-de-tela.md`)) e campo **"Buscar aviso"** (`busca`).
- **Lista**, um cartão por aviso, do mais recente ao mais antigo: um **ponto laranja** à esquerda nos não lidos, o **título** em negrito, o chip do tipo, o rótulo **"Não lido"** (laranja), a **prévia** em uma linha (`previa`, cortada), à direita a **data** da publicação ("07/10", de `publicadoEmUtc`) e uma seta. Os não lidos têm o fundo levemente mais quente. Clicar abre o aviso (tela 38).
- Menu com "Avisos 2" e rodapé com nome e cargo (da fundação).

**O que sai ou muda (com a razão)**
- **"e para o seu setor" sai do subtítulo** (PNG: "Comunicados do RH para você e para o seu setor"): todo aviso é para todos. Use "Comunicados do RH para você".
- **"Para: Todos" e "Para: Financeiro" saem** (o contrato não tem público por setor).
- A aba "Não lidos" usa `apenasNaoLidos=true`.
- Aviso fixado: o PNG não mostra marca; a lista segue só a ordem da API (publicação mais recente primeiro). `fixadosPrimeiro` é do cartão da tela inicial (fatia 7d).
- O número "Todos 5" do PNG é exemplo; o exemplo do contrato tem 4.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Abas, contagens, busca e lista | `listarMeusAvisos` | `apenasNaoLidos`, `busca`, `pagina`, `tamanhoPagina` |
| Clicar em um aviso | `obterMeuAviso` e `marcarAvisoComoLido` | ver a tela 38 |

**Estados**
- Carregando: esqueleto da lista.
- Vazio: "Nenhum aviso por enquanto." (aba "Não lidos" vazia: "Você leu todos os avisos."; busca sem resultado: "Nenhum aviso encontrado para esta busca.").
- Erro e sem permissão: nas regras comuns.

**Observações para quem monta**
- Só aparecem avisos **vigentes** (publicados, não expirados): rascunho, agendado e expirado nunca chegam.
- Depois de ler, invalide a lista, as contagens e a bolinha "Avisos" do menu.
- Paginação "carregar mais" se passar de uma página (padrão 20).

---

## Tela 38: Aviso aberto (funcionário, modal)

**PNG:** `Figma\telas\38-func-avisos-aviso-aberto.png` · **Incremento:** 4a · **Rotas:** `obterMeuAviso`, `marcarAvisoComoLido`.

**O que fica (li no PNG 38)**
- Modal sobre a lista, título do aviso ("Política de trabalho remoto atualizada") e botão fechar.
- Chip do tipo e a linha "Publicado por Ana Souza em 06/10/2026, 09:00." (`autorNome`, `publicadoEmUtc`).
- O **conteúdo**: no detalhado, seções com título em negrito ("O que mudou", "Como pedir", "Quem continua presencial") e o texto de cada uma.
- Rodapé: "Ao abrir, o aviso fica marcado como lido para o RH." e botão principal **"Fechar"**.
- No fundo, a lista já mostra "Não lidos 1" (a contagem caiu ao abrir).

**O que sai ou muda (com a razão)**
- **"Para: Todos." sai** da linha de publicação (sem público por setor).
- **"Baixar a política completa (PDF)" sai** (sem anexo PDF).
- **Marcar como lido**: o `GET` do aviso **não marca**; ao abrir, chame também `marcarAvisoComoLido` (`POST`, sem corpo, 204, idempotente) e depois invalide a lista, as contagens e a bolinha do menu.
- Deep link: o cartão "Avisos" da tela inicial (fatia 7d) tem "Ler aviso completo"; o modal abre também por uma rota com o `avisoId`: `/eu/avisos/:avisoId` (`05-caminhos-de-tela.md`).

**Estados**
- Carregando: esqueleto do conteúdo.
- Erro: 404 (aviso não existe ou **não está mais vigente**) "Este aviso não está mais disponível." e recarregue a lista.
- Se `marcarAvisoComoLido` falhar, não bloqueie a leitura: tente de novo ao fechar e mostre o erro só se persistir.

**Observações para quem monta**
- Conteúdo como texto (regras comuns). Um aviso simples é texto puro; o detalhado passa pelo `Markdown` da 1a.
- Quem foi admitido depois da publicação também vê o aviso vigente (a API cria a linha de leitura na hora).

---

## Tela 50: Avisos (gestor)

**PNG:** `Figma\telas\50-gestor-avisos.png` · **Incremento:** 4a · **Rotas:** as mesmas da tela 37 (`/api/eu/...`).

**O que fica (li no PNG 50):** é **a mesma tela 37** (e abre o mesmo modal da 38), com o **menu do gestor** (Seu dia a dia, Seu setor com "Painel da equipe 2", Comunicação com "Avisos 2", rodapé "Carlos Ribeiro, Perfil gestor, setor Comercial"). Monte **uma só tela**, escolhida pelo perfil só no menu.

**O que sai ou muda:** os mesmos cortes da tela 37 ("e para o seu setor" e "Para: Comercial" saem: o PNG 50 mostra "Metas do quarto trimestre, Para: Comercial", que **não existe** sem público por setor). O gestor **lê** avisos aqui; **não publica** (publicar é do RH, tela 27).

**Estados e textos:** os da tela 37.

---

## Tela 07: Ficha do colaborador, aba Holerites (RH)

**PNG:** `Figma\telas\07-rh-colaboradores-ficha-aba-holerites.png` · **Incremento:** 4b · **Rotas:** `listarHoleritesDoFuncionario`, `obterLinkDeDownloadDoHolerite`; sem print: `substituirHolerite`.

A moldura da ficha (cabeçalho "Bruno Lima", abas) é da **fatia 1b** (tela 03). Esta fatia entrega o **conteúdo da aba Holerites**.

**O que fica (li no PNG 07)**
- Texto de apoio "O colaborador recebe um e-mail sempre que um holerite novo é publicado." e o botão principal **"+ Enviar holerite"** (abre o modal da tela 08).
- **Tabela**: "Referência" (rótulo do holerite, ver "Regras comuns"), "Publicado em" (`publicacaoEmUtc`, `dd/MM/yyyy`) e o botão **"Baixar PDF"** (`obterLinkDeDownloadDoHolerite`; abre o link temporário). Ordem da API (competência mais recente primeiro).

**O que sai ou muda (com a razão)**
- **Coluna "Tipo"** (acréscimo do contrato; o PNG 07 não a tem, mas a 36 tem): mostre "Mensal", "Férias" ou "13º salário", porque a aba mostra os três tipos.
- **Holerite agendado** (`situacao: AGENDADO`; a pessoa ainda não o vê): na coluna "Publicado em" mostre "Agendado para {dd/MM/yyyy}" com um chip "Agendado". O PNG só tem publicados.
- **Versões** (`versao`, `versaoAtual`): por padrão só a **versão atual** de cada competência e tipo. Na linha com `versao` maior que 1, mostre "Versão {n}". Um link **"Ver versões anteriores"** refaz a lista com `todasAsVersoes=true` (as antigas aparecem esmaecidas, sem botão "Substituir").
- **Botão "Substituir"** em cada linha da versão atual (o Figma não o desenha; o contrato tem `substituirHolerite`): abre o modal descrito abaixo. É a única forma de corrigir um holerite (o banco não deixa apagar).
- **Paginação** (`pagina`, `tamanhoPagina`) quando passar de uma página; o PNG mostra 5 linhas e nenhuma navegação.
- Funciona também para pessoa **inativa** (o RH guarda os holerites de quem saiu).

**Estados**
- Carregando: esqueleto da tabela. Vazio: "Nenhum holerite enviado ainda." Erro e sem permissão: nas regras comuns.

**Observações para quem monta**
- O botão "+ Enviar holerite" abre o modal com `funcionarioId` e `ultimoPublicado` (vem desta mesma lista).
- Só o RH vê esta aba (o gestor não vê holerite de ninguém).

### Modal "Substituir holerite" (sem print; desenho meu)

- Título "Substituir holerite de {Mês} de {ano}" para {nome}. Texto: "O holerite anterior continua guardado no histórico. A pessoa passa a ver a nova versão assim que ela for publicada e recebe um e-mail."
- Campos: **arquivo PDF** (mesma área de envio do modal 08: "Arraste o PDF do holerite aqui", "Só PDF, até 5 MB", "Escolher arquivo") e **"Quando publicar"** (os mesmos três controles do modal 08).
- Botões "Cancelar" e "Substituir holerite". `multipart/form-data` com `arquivo` e, se escolhido, `publicarEm` **ou** `noDiaDoPagamento`.
- **Mensagens (contrato):** 409 `HOLERITE_VERSAO_DESATUALIZADA` "Este holerite já foi substituído por uma versão mais nova. Atualize a lista." (recarregue); 422 `HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO` e `HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO` e `EMPRESA_NAO_CONFIGURADA` (mostre o `detail`).
- **Pergunta aberta (`PROVISORIOS.md`):** `paths > /api/rh/holerites/{holeriteId}/versoes > post`: a correção por nova versão é decisão da sessão por delegação do dono; ele pode discordar.

---

## Tela 08: Modal "Enviar holerite" (RH)

**PNG:** `Figma\telas\08-rh-colaboradores-ficha-modal-enviar-holerite.png` · **Incremento:** 4b · **Rotas:** `enviarHolerite`, `listarHoleritesDoFuncionario` (só `ultimoPublicado`), `obterProximoPagamento` (opcional, para mostrar a data).

**O que fica (li no PNG 08)**
- Título "Enviar holerite de Bruno" (primeiro nome) e botão fechar.
- Três campos: **"Mês"** (lista: Janeiro a Dezembro; começa no mês atual), **"Ano"** (lista; "2026") e **"Tipo"** (lista: Mensal, 13º salário, Férias).
- **Área de envio** com borda tracejada: ícone, "Arraste o PDF do holerite aqui", "Só PDF, até 5 MB" e o botão **"Escolher arquivo"**. Mostre o nome do arquivo escolhido e um "Trocar".
- **"Quando publicar"**: rádio **"Publicar agora"** (selecionado) e **"Agendar para o dia do pagamento"**.
- Linha "Último holerite enviado: Setembro de 2026, publicado em 05/10/2026." (de `ultimoPublicado`; `null`: esconda a linha).
- Rodapé: "O arquivo fica guardado na ficha e na área de holerites do colaborador." e os botões **"Cancelar"** e **"Publicar holerite"** (principal).

**O que sai ou muda (com a razão)**
- **A caixa "Avisar Bruno por e-mail quando publicar" sai:** o e-mail para a pessoa sai **sempre** ao publicar (na hora ou no dia agendado), sem opção para não avisar (decisão do dono, 2026-10-28; `CONVENCOES.md`, seção 12). Para o RH saber, o texto de apoio da aba (tela 07) já diz "O colaborador recebe um e-mail sempre que um holerite novo é publicado."
- **Terceira opção "Agendar para uma data"** (acréscimo do contrato; **dúvida D-F4-1**): um rádio com um campo de data ao lado (de hoje em diante; o holerite aparece e o e-mail sai no começo do dia escolhido). `publicarEm` e `noDiaDoPagamento` **nunca** vão juntos (400).
- **"Agendar para o dia do pagamento"**: o sistema calcula a data (a de `pagamento.data` em `obterProximoPagamento`; hoje, se hoje é o dia). Mostre a data ao lado do texto quando `obterProximoPagamento` a trouxer ("Agendar para o dia do pagamento (06/11/2026)"). Se `pagamento` vier `null` (empresa sem dia de pagamento configurado), **desligue** essa opção e mostre "Configure o dia do pagamento em Cadastros, Empresa."
- **O botão** passa a "Agendar holerite" quando a escolha não for "Publicar agora" (suponho a redação; o PNG só tem "Publicar holerite").
- **Competência futura** só vale no tipo Férias (o holerite de férias é pago antes). A tela pode avisar "Só o holerite de Férias pode ter competência futura." ao escolher mês e ano futuros com outro tipo, mas **quem decide é a API** (422 `HOLERITE_COMPETENCIA_FUTURA`).
- Resposta: o `HoleriteItemRh` criado (versão 1) traz `situacao` e `publicacaoEmUtc`; mostre "Holerite publicado." ou "Holerite agendado para {dd/MM/yyyy}."

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| "Último holerite enviado" | `listarHoleritesDoFuncionario` | só o campo `ultimoPublicado` |
| Data do "dia do pagamento" | `obterProximoPagamento` (fatia 7) | opcional |
| "Publicar holerite" | `enviarHolerite` | `multipart`: `mes`, `ano`, `tipo`, `arquivo` e, se houver, `publicarEm` ou `noDiaDoPagamento` |

**Estados**
- Enviando: botão "Enviando..." e tudo desligado. Sucesso fecha o modal, atualiza a tabela da tela 07 e o "Último holerite enviado".
- Erros de campo (400, `errors`): arquivo que não é PDF ou passa de 5 MB aparece no campo `arquivo`; `publicarEm` no passado.
- 409 `HOLERITE_JA_EXISTE` "Já existe um holerite mensal de outubro de 2026 para esta pessoa. Para corrigir, envie uma nova versão." com um botão **"Substituir"** que abre o modal "Substituir holerite" daquele holerite.
- 422: `HOLERITE_COMPETENCIA_FUTURA` "Só o holerite de férias pode ter competência futura."; `HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO` "A competência do holerite é anterior à admissão da pessoa."; `HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO` "A competência do holerite é posterior ao desligamento da pessoa."; `EMPRESA_NAO_CONFIGURADA` "A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data." (com a opção de data, o "escolha uma data" fica verdadeiro).

**Observações para quem monta**
- Teste com `// @vitest-environment node` (multipart).
- O arquivo só é guardado depois de a regra do banco aprovar (a API); a tela não precisa limpar nada.

**Perguntas abertas:** D-F4-1 (opção "Agendar para uma data"). O `x-provisorio` de lote e de versão ficam nas telas abaixo.

---

## Tela 36: Holerites (funcionário)

**PNG:** `Figma\telas\36-func-holerites.png` · **Incremento:** 4b · **Rotas:** `listarMeusHolerites`, `obterLinkDeDownloadDoMeuHolerite`, `obterProximoPagamento` (fatia 7).

**O que fica (li no PNG 36)**
- Título "Holerites" e subtítulo "Todos os seus holerites ficam guardados aqui".
- **Cartão escuro "Próximo pagamento"**: "Até 6 de novembro" (de `obterProximoPagamento`: `pagamento.data`), o texto "O holerite de outubro aparece aqui quando o RH publicar. Você também recebe um e-mail." (o mês é `competencia.mes`) e a linha "Dúvida sobre um valor? Fale com o RH".
- **Abas** "Todos 10", "Mensais 9", "Férias 1" e "13º salário 0" (`contagens`: `todos`, `mensais`, `ferias`, `decimoTerceiro`; valem para o `ano` escolhido e **não** mudam com a aba) e o **seletor de ano** ("2026"; as opções são `anosDisponiveis`; padrão `ano`, que é o ano atual).
- **Tabela**: "Referência" (rótulo, ver "Regras comuns"), "Tipo" ("Mensal", "Férias"), "Publicado em" (`publicadoEmUtc`, `dd/MM/yyyy`) e dois botões, **"Ver"** (contornado) e **"Baixar PDF"** (principal). Rodapé "10 holerites em 2026" (`total` e `ano`).

**O que sai ou muda (com a razão)**
- **O chip "Novo" sai** (setembro de 2026 no PNG): não há marca de "novo" nem de "visto" em holerite (decisão do dono, 2026-10-28; contrato).
- **"Ver" e "Baixar PDF" usam o mesmo link** (`x-provisorio`): a regra de arquivos (roadmap, decisão 7) manda o PDF **sempre baixar como anexo**, então o contrato tem uma só rota. Mantenha os dois botões como no Figma; "Ver" abre o link em outra aba e "Baixar PDF" navega para ele (o efeito para o usuário é o mesmo). Se o dono quiser que "Ver" abra o PDF na tela do navegador, a rota ganha um parâmetro.
- **"Fale com o RH"**: o contrato não tem rota nem destino para esse link. Mostre como **texto simples**, sem link, até o dono dizer o destino (dúvida D-F4-3).
- **Cartão "Próximo pagamento"**: se a empresa não tem o dia de pagamento configurado, `obterProximoPagamento` devolve `pagamento` e `competencia` `null` e o **cartão não aparece** (contrato).
- **Só os próprios holerites**, publicados e na versão atual (agendados e versões substituídas não aparecem).
- Rótulo "13º salário 0": mostre a aba mesmo com zero.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Cartão "Próximo pagamento" | `obterProximoPagamento` (fatia 7) | dados simulados do contrato enquanto a 7 não existe. O cartão é o **componente único** `{ data, mesReferencia }` (`pagamento.data` e `competencia.mes`), **criado aqui**; a fatia 7 o reaproveita nas telas 32 e 46, alimentado por `obterPainelDoFuncionario.proximoPagamento` (estas duas telas **não** chamam `obterProximoPagamento`) |
| Abas, ano, tabela e rodapé | `listarMeusHolerites` | `ano`, `tipo`, `pagina`, `tamanhoPagina` |
| "Ver" e "Baixar PDF" | `obterLinkDeDownloadDoMeuHolerite` | 404 para holerite de outra pessoa, agendado ou substituído |

**Estados**
- Carregando: esqueleto do cartão e da tabela.
- Vazio: "Você ainda não tem holerites em {ano}." (de `contagens.todos` igual a 0).
- Erro: nas regras comuns; 404 ao baixar "Este holerite não está mais disponível." e recarregue a lista.

**Observações para quem monta**
- O link vence em minutos: chame a rota **no clique**, não antes. Nunca guarde a `url`.
- Mensagem sob o cartão quando não há pagamento: nenhuma (o cartão some).

**Perguntas abertas (`PROVISORIOS.md`)**
- `paths > /api/eu/holerites/{holeriteId}/download > get`: "Ver" abre o PDF na tela do navegador ou baixa como os dois botões fazem hoje?

---

## Tela 49: Holerites (gestor)

**PNG:** `Figma\telas\49-gestor-holerites.png` · **Incremento:** 4b · **Rotas:** as mesmas da tela 36 (`/api/eu/...`).

**O que fica (li no PNG 49):** é **a mesma tela 36**, com o **menu do gestor**. Monte **uma só tela**. O gestor vê **só os próprios holerites**; **não existe** rota para ele ver os da equipe (contrato). Mesmos cortes e estados da tela 36 (chip "Novo" sai).

---

## Tela sem print: Enviar holerites em lote (RH)

**PNG:** nenhum · **Incremento:** 4b · **Rota:** `enviarHoleritesEmLote` (`POST /api/rh/holerites/lote`) · **Em aberto:** `x-provisorio` do formato do lote.

**Descrição (desenho meu, apoiada no contrato)**
- **Onde:** botão secundário **"Enviar holerites em lote"** no cabeçalho da lista de colaboradores (tela 01, fatia 1b), ao lado de "Adicionar colaborador". Abre um modal grande (dúvida D-F4-4).
- **Campos comuns** (valem para todos os arquivos): **Mês**, **Ano**, **Tipo** e **"Quando publicar"** (os mesmos controles e regras do modal 08, inclusive o desligar do "dia do pagamento" sem dia configurado).
- **Arquivos**: área de envio que aceita **vários PDFs** ("Arraste os PDFs aqui", "Só PDF, até 5 MB cada, até 200 arquivos e 100 MB no total"). Para cada arquivo, uma linha em uma tabela: **"Arquivo"** (nome e tamanho), **"Colaborador"** (campo de busca; **obrigatório**; o RH escolhe a pessoa de cada arquivo: a API **não adivinha** a pessoa pelo conteúdo nem pelo nome do arquivo) e **"Remover"**. O seletor de colaborador usa `listarFuncionarios`.
- Avisos no cliente (só por conforto): arquivo que não é PDF, arquivo acima de 5 MB, nome de arquivo repetido (a API recusa), mais de 200 arquivos, arquivo sem colaborador (o botão fica desligado).
- Botão principal **"Publicar {N} holerites"** (ou "Agendar {N} holerites") e "Cancelar". Corpo `multipart/form-data`: `mes`, `ano`, `tipo`, `publicarEm` **ou** `noDiaDoPagamento`, `itens` (lista em JSON com `funcionarioId` e `nomeDoArquivo` de cada arquivo) e `arquivos` (um campo repetido, um PDF por parte).
- **Resultado** (sempre 200, com **um resultado por item**; **não é tudo ou nada**): totais "{enviados} enviados, {comErro} com erro" e uma tabela "Arquivo", "Colaborador" e "Resultado": "Publicado" ou "Agendado para dd/MM/yyyy" (de `holerite.situacao` e `publicacaoEmUtc`) ou o erro do item (`mensagemDoErro`, com o `codigoDoErro`: `HOLERITE_JA_EXISTE`, `HOLERITE_COMPETENCIA_FUTURA`, `HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO`, `HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO`, `ARQUIVO_INVALIDO`, `FUNCIONARIO_NAO_ENCONTRADO`). O lote **não cria versões**: para `HOLERITE_JA_EXISTE`, ofereça "Abrir a ficha" e usar "Substituir". Botões "Fechar" e "Enviar mais".
- **Erros do lote inteiro** (nenhum item é processado): 400 (`publicarEm` e `noDiaDoPagamento` juntos, `publicarEm` no passado, nome de arquivo repetido, arquivo sem item, item sem arquivo, passa de 200 arquivos ou de 100 MB) e 422 `EMPRESA_NAO_CONFIGURADA` ("A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data.").
- Cada pessoa recebe um e-mail na publicação (como no envio individual).

**Estados:** enviando ("Enviando {N} arquivos..." e tudo desligado), resultado, erro. Lista vazia: "Adicione os PDFs para começar."

**Observações:** a tela é grande e sem desenho: mantenha simples e teste com poucos arquivos. O teste de `multipart` roda com `// @vitest-environment node`.

**Pergunta aberta (`PROVISORIOS.md`):** `paths > /api/rh/holerites/lote > post`: o **formato** do lote (hoje: vários PDFs mais a lista `itens`, cada item validado sozinho). Se o dono quiser tudo ou nada, ou a matrícula no nome do arquivo, o corpo e a resposta mudam.

---

## Tela 14: Advertências (RH)

**PNG:** `Figma\telas\14-rh-advertencias.png` · **Incremento:** 4c · **Rotas:** `listarAdvertencias`, `obterResumoDasAdvertencias`, `obterAdvertencia`, `responderContestacaoDeAdvertencia`, `listarSetores` (fatia 1).

**O que fica (li no PNG 14)**
- Título "Advertências", subtítulo "Registros disciplinares de todos os setores" e botão principal **"+ Nova advertência"** (abre a tela 15).
- **Quatro números** (`obterResumoDasAdvertencias`, do **ano**; o PNG usa 2026): "Registradas em 2026" (`contagens.todas`, 9; **inclui as canceladas**), "Aguardando ciência" (`contagens.pendentes`, 2, âmbar), "Contestadas" (`contagens.contestadas`, 1, vermelho) e "Setor com mais registros" (`setorComMaisRegistros.setor`, "Logística", com "{quantidade} advertências" embaixo; `null` vira "—").
- **Abas** "Todas 9" (sem `situacao`), "Aguardando ciência 2" (`PENDENTE`), "Ciente 6" (`APLICADA`) e "Contestada 1" (`CONTESTADA`) (os números são `contagens` e **não** mudam com a busca nem com os filtros), campo **"Buscar colaborador ou motivo"** (`busca`: nome e motivo), seletores **"Todos os setores"** (`setorId`; opções de `listarSetores`) e **"Todos os tipos"** (`tipo`: Verbal, Escrita).
- **Tabela**: "Data" (`data`, `dd/MM/yyyy`; é a data do ocorrido), "Colaborador" (avatar, nome e setor), "Tipo", "Motivo" (`motivo`, pode quebrar linha), "Aplicada por" (ver "Regras comuns"), "Situação" (chip) e o botão **"Abrir"**. Rodapé "Mostrando 6 de 9 em 2026" (`itens.length`, `total` e o ano).

**O que sai ou muda (com a razão)**
- **"Aplicada por" quando o RH registra:** o PNG mostra nomes de pessoas do RH ("Patrícia Gomes", "Renata Dias"); o banco só guarda o gestor, então o contrato devolve `tipo: RH` e `nome: null` e a tela mostra **"RH"** (`x-provisorio` de `AdvertenciaAplicadaPor`).
- **A aba "Cancelada" não existe** no Figma: as canceladas aparecem em "Todas" com o chip "Cancelada".
- **Filtro de ano:** o PNG não tem; a tela usa o **ano atual** (padrão da API). Se quiser ver outro ano, o contrato aceita `ano` (acréscimo opcional, desenho meu: um seletor pequeno ao lado do rodapé). Sem ele, a lista é sempre do ano corrente.
- **O botão "Abrir" aparece cortado** na borda direita do PNG (a tabela é mais larga que a moldura): planeje a largura para caber (1366 px).
- **"Abrir"** abre o detalhe (ver "Detalhe da advertência", abaixo); o PNG não o desenha.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Quatro números e contagem das abas | `obterResumoDasAdvertencias` | `ano` opcional; não depende de busca nem de filtro |
| Lista, abas, busca, setor, tipo | `listarAdvertencias` | `situacao`, `busca`, `setorId`, `tipo`, `ano`, `pagina`, `tamanhoPagina` |
| Filtro "Todos os setores" | `listarSetores` (fatia 1) | lista curta, sem paginação |
| "Abrir" | `obterAdvertencia` | detalhe com a contestação |
| "Responder contestação" | `responderContestacaoDeAdvertencia` | só em `CONTESTADA` |

**Estados**
- Carregando: esqueleto nos números e na tabela.
- Vazio: "Nenhuma advertência encontrada." (aba sem registros: "Nenhuma advertência nesta situação."; com a busca: "Nenhuma advertência encontrada para esta busca.").
- Erro e sem permissão: nas regras comuns.

**Observações para quem monta**
- A lista é **de um ano** pela data do ocorrido; os números do topo também.
- "Registradas em 2026" **soma tudo** (inclusive canceladas); "Setor com mais registros" **não** conta as canceladas (contrato). Não tente conciliar os dois no front.

**Perguntas abertas (`PROVISORIOS.md`)**
- `components > schemas > AdvertenciaAplicadaPor`: o nome da pessoa do RH na coluna "Aplicada por" exigiria uma coluna nova no banco.
- `components > schemas > AdvertenciaTipo`: só Verbal e Escrita.

### Detalhe da advertência e "Responder contestação" (RH; sem print; desenho meu)

- **Onde:** botão "Abrir" da tela 14 e da aba da tela 11. Painel lateral "Advertência de {dd/MM/yyyy}" (`obterAdvertencia`): da tela 14 é uma rota filha da lista, `/rh/advertencias/:advertenciaId`; da aba da tela 11 o mesmo painel abre sobre a ficha, sem caminho (`05-caminhos-de-tela.md`).
- **Conteúdo:** colaborador (avatar, nome, setor), "Data do ocorrido", "Tipo", "Aplicada por", "Motivo", "Descrição" (texto, quebras de linha preservadas), "Situação" (chip), "Ciência em {dd/MM/yyyy, HH:mm}" (`dataCienciaUtc`; vazio: "Ainda sem ciência"), e, se houve contestação, o bloco **"Contestação"** com a data (`dataContestacaoUtc`) e o texto (`textoContestacao`).
- **Responder contestação** (só em `CONTESTADA`): escolha entre **"Manter a advertência"** (`MANTER`: volta a `APLICADA`) e **"Acatar a contestação"** (`ACATAR`: a advertência é **cancelada** e não muda mais); campo **"Resposta para {nome}"** (obrigatório, até 2000 caracteres) e o botão **"Enviar resposta"**. Texto fixo junto do campo: "A resposta vai por e-mail para {nome}. Ela não fica guardada no sistema e a pessoa não a vê na tela, só a nova situação." (contrato: a resposta **não fica guardada**; `x-provisorio`).
- **Depois de responder:** o detalhe mostra o desfecho (`desfechoDaContestacao`): "Contestação acatada: advertência cancelada." ou "Contestação analisada: advertência mantida." (suponho a redação).
- **Mensagens:** 422 `ADVERTENCIA_NAO_ESTA_CONTESTADA` "Só uma advertência contestada pode receber resposta." (recarregue).
- **Pergunta aberta:** `paths > /api/rh/advertencias/{advertenciaId}/resposta > post` (o que acontece depois da contestação).

---

## Tela 15: Nova advertência (RH, painel lateral)

**PNG:** `Figma\telas\15-rh-advertencias-nova-advertencia.png` · **Incremento:** 4c · **Rotas:** `registrarAdvertencia`, `listarFuncionarios` (fatia 1).

**O que fica (li no PNG 15)**
- Painel lateral "Nova advertência" com botão fechar.
- **Colaborador** (lista com busca; "Escolha um colaborador"; de `listarFuncionarios`, só ativos: pessoa inativa não recebe advertência).
- **Data do ocorrido** (`dataDoOcorrido`; hoje ou antes; o padrão é hoje) e **Tipo** (Verbal ou Escrita).
- **Aplicada por** (lista: "Gestor do colaborador" e "RH"; `aplicadaPor` `GESTOR_DO_COLABORADOR` ou `RH`).
- **Motivo** (placeholder "Resumo em uma linha, ex.: atrasos repetidos sem justificativa"; até 255 caracteres) e **Descrição** (placeholder "O que aconteceu, quando e qual regra foi descumprida"; até 5000).
- Caixa informativa: "O colaborador vê a advertência no portal e confirma que tomou ciência. Se discordar, ele pode contestar e explicar o motivo."
- Botões **"Cancelar"** e **"Registrar advertência"** (principal).

**O que sai ou muda (com a razão)**
- **A caixa "Avisar o colaborador e o gestor por e-mail" sai:** a pessoa recebe **sempre** o e-mail de "advertência registrada", e o **gestor dela não recebe cópia** quando o RH registra (decisão do dono, 2026-10-30; `CONVENCOES.md`, seção 12). Sem opção para não avisar.
- **O PNG mostra só "Gestor do colaborador"** no campo "Aplicada por"; as duas opções ("Gestor do colaborador" e "RH") são suposição do contrato (`x-provisorio`). Quando o RH registra "em nome do gestor", a lista mostra o nome do gestor; "RH" aparece como "RH".
- **Colaborador fixo** quando o painel abre da ficha (tela 11): o campo vem preenchido e desligado.
- A data do PNG está em formato americano ("10/07/2026"); use dd/mm/aaaa.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Campo "Colaborador" | `listarFuncionarios` (fatia 1b) | busca por nome |
| "Registrar advertência" | `registrarAdvertencia` | `POST /api/rh/funcionarios/{funcionarioId}/advertencias`; corpo `{ dataDoOcorrido, tipo, aplicadaPor, motivo, descricao }` |

**Estados**
- Salvando: botões desligados. Sucesso fecha o painel, mostra "Advertência registrada. {nome} foi avisado por e-mail." e atualiza a tela 14 (lista, números, abas) ou a aba da ficha.
- Erros de campo (400): data no futuro, motivo ou descrição vazios.
- 422 `ADVERTENCIA_SEM_GESTOR` "Esta pessoa não tem gestor ativo. Escolha "RH" em "Aplicada por"."; 422 `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO` (a pessoa é o próprio gestor; mostre o `detail`); 422 `FUNCIONARIO_INATIVO` "Não é possível registrar advertência para uma pessoa inativa."; 404 pessoa não encontrada.

**Observações para quem monta:** o mesmo painel serve às telas 14 e 11; o gestor usa outra rota e outro corpo (telas 53 e 60).

**Perguntas abertas (`PROVISORIOS.md`)**
- `components > schemas > AdvertenciaRegistroRhComando > properties > aplicadaPor`: quais são as opções do campo "Aplicada por".

---

## Tela 11: Ficha do colaborador, aba Advertências (RH)

**PNG:** `Figma\telas\11-rh-colaboradores-ficha-aba-advertencias.png` · **Incremento:** 4c · **Rotas:** `listarAdvertenciasDoFuncionario`, `obterAdvertencia`, `registrarAdvertencia`.

A moldura da ficha (cabeçalho "Pedro Santos", abas) é da **fatia 1b**. Esta fatia entrega o **conteúdo da aba Advertências**.

**O que fica (li no PNG 11)**
- **Tabela** com "Data" (`data`, `dd/MM/yyyy`), "Tipo" ("Verbal"), "Motivo", "Aplicada por" ("Carlos Ribeiro") e "Situação" (chip "Ciente"); ordem da API (data do ocorrido mais recente primeiro).
- Botão **"Registrar advertência"** (contornado) abaixo da tabela: abre o painel da tela 15 com o colaborador já escolhido, **sobre a ficha e sem caminho próprio** (o painel da tela 15 a partir da lista 14 é `/rh/advertencias/nova`).

**O que sai ou muda (com a razão)**
- **Botão "Abrir" por linha** (o PNG não o desenha, mas o contrato o prevê: `listarAdvertenciasDoFuncionario` diz "O botão 'Abrir' usa `GET /api/rh/advertencias/{advertenciaId}`"): acrescente-o, como na tela 14, para o RH ler a descrição e a contestação e responder.
- **A lista inclui todas as advertências da pessoa, de todos os anos, inclusive as canceladas** (diferente do número "Advertências" do resumo da ficha, que conta só as não canceladas do ano corrente: por isso pode ser menor que o `total` desta lista). Funciona também para pessoa inativa.
- Paginação quando passar de uma página.

**Estados:** carregando (esqueleto); vazio "Nenhuma advertência registrada."; erro nas regras comuns.

---

## Tela 43: Minhas advertências (funcionário)

**PNG:** `Figma\telas\43-func-advertencias.png` · **Incremento:** 4c · **Rotas:** `listarMinhasAdvertencias`, `confirmarCienciaDeAdvertencia`, `contestarAdvertencia`.

**O que fica (li no PNG 43)**
- Título "Minhas advertências" e subtítulo "Registros disciplinares feitos pelo seu gestor ou pelo RH".
- **Cartão em destaque** (borda laranja) para a advertência `PENDENTE`: chip "Aguardando sua ciência", título "Advertência escrita de 05/10/2026" ("Advertência {tipo em minúsculas} de {data}"), três campos "Tipo" ("Escrita"), "Data" ("05/10/2026") e "Aplicada por" ("Carlos Ribeiro, seu gestor"), "Motivo" e "Descrição" (texto), uma caixa explicativa "Confirmar a ciência não quer dizer que você concorda. Se discordar, escolha Contestar e explique o que aconteceu. O RH analisa a contestação e responde por e-mail." e os botões **"Confirmar ciência"** (principal) e **"Contestar"** (contornado).
- **"Histórico"** (tabela das demais): "Data", "Tipo", "Motivo", "Aplicada por", "Situação" (chip "Ciente") e o botão **"Ver detalhes"**.
- Rodapé "Dúvidas sobre uma advertência? Fale com o RH".

**O que sai ou muda (com a razão)**
- **Mais de uma advertência pendente:** a API põe as `PENDENTE` primeiro; mostre **um cartão em destaque para cada uma** (o PNG só tem uma).
- **"Fale com o RH"** sem rota nem destino no contrato: texto simples, sem link (dúvida D-F4-3).
- **"Aplicada por"** quando é o RH: "RH" (sem nome de pessoa).
- **Cada item já traz** descrição e contestação: "Ver detalhes" **não** chama outra rota.
- **Contestar** e **Ver detalhes** não têm print: ver abaixo.
- **Onde o gestor e o RH veem as suas. Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Minhas advertências"** existe no grupo **"Meu espaço"** do menu, com o mesmo nome no RH e no gestor, e leva a `/eu/advertencias` (a página é a mesma para os três perfis e se intitula "Minhas advertências"). O RH também tem "Holerites" (`/eu/holerites`) no grupo; o gestor já tem "Holerites" e "Avisos" no menu do Figma.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Cartões e histórico | `listarMinhasAdvertencias` | `situacao` opcional; as `PENDENTE` vêm primeiro |
| "Confirmar ciência" | `confirmarCienciaDeAdvertencia` | sem corpo; passa a `APLICADA` ("Ciente") |
| "Contestar" | `contestarAdvertencia` | `{ texto }`; passa a `CONTESTADA` |

**Estados**
- Carregando: esqueleto. Vazio: "Você não tem advertências." (sem cartão em destaque e sem histórico).
- 422 `ADVERTENCIA_NAO_ESTA_PENDENTE` "Esta advertência já teve ciência ou contestação." (clique repetido ou outra aba): recarregue a lista.
- Sucesso: "Ciência registrada." e "Contestação enviada. O RH vai analisar e responder por e-mail."

**Observações para quem monta**
- Confirmar a ciência é uma ação **sem volta**; peça confirmação? O PNG não pede. Não peça (desabilite o botão durante a chamada e mostre o resultado).
- Não há e-mail ao RH no momento da contestação (contrato): o RH a encontra na aba "Contestada".

### "Contestar" (modal; sem print; desenho meu)

- Título "Contestar advertência". Texto: "Explique o que aconteceu. O RH analisa e responde por e-mail."
- Campo **"O que você quer explicar"** (obrigatório, **10 a 2000 caracteres**, com contador).
- Botões "Cancelar" e "Enviar contestação". Depois de enviada, **não** se pode contestar de novo nem trocar o texto (a primeira contestação nunca é sobrescrita).

### "Ver detalhes" (modal; sem print; desenho meu)

- Título "Advertência de {dd/MM/yyyy}". Mostra tudo o que o item já traz: tipo, data, aplicada por, motivo, descrição, situação, "Ciência em {dd/MM/yyyy, HH:mm}" (`dataCienciaUtc`), e se houve contestação, "Você contestou em {data}" com o seu texto, e o desfecho (`desfechoDaContestacao`): "O RH acatou a sua contestação: a advertência foi cancelada." (`ACATADA`) ou "O RH analisou a sua contestação e manteve a advertência." (`MANTIDA`) (suponho a redação). **Não** mostra a resposta do RH (ela vai só por e-mail e não fica guardada).

---

## Tela 59: Advertências do setor (gestor)

**PNG:** `Figma\telas\59-gestor-advertencias-da-equipe.png` · **Incremento:** 4c · **Rotas:** `listarAdvertenciasDaEquipe`, `obterResumoDasAdvertenciasDaEquipe`, `obterAdvertenciaDaEquipe`, `listarEquipeDoGestor` (fatia 1).

**O que fica (li no PNG 59)**
- Título "Advertências do setor", subtítulo "Setor Comercial. O RH acompanha todas as advertências registradas." (o nome do setor vem de `listarEquipeDoGestor`, `setor.nome`) e botão principal **"+ Registrar advertência"** (abre a tela 60).
- **Três números** (`obterResumoDasAdvertenciasDaEquipe`, do ano): "Registradas em 2026" (`registradas`, 3), "Aguardando ciência" (`aguardandoCiencia.quantidade`, 1, âmbar, com o nome da pessoa embaixo: "Diego Farias"; havendo mais, o primeiro nome e "e mais N") e "Pessoas com advertência" (`pessoasComAdvertencia` de `totalDaEquipe`: "2 de 8").
- **"Registros"** (tabela): "Data", "Pessoa" (avatar e nome), "Tipo", "Motivo", "Situação" (chip) e o botão **"Abrir"**. Mostra as advertências da equipe registradas por **qualquer pessoa** (o gestor ou o RH).
- **"Por pessoa"**: uma grade de cartões, um por pessoa da equipe (avatar, nome e a quantidade, em âmbar quando maior que zero), **da que tem mais para a que tem menos e, no empate, por nome** (a ordem de `porPessoa`).

**O que sai ou muda (com a razão)**
- **Os números do gestor não contam canceladas** (`registradas` é "advertência não cancelada do ano", e a soma de `porPessoa` é `registradas`); a **lista** mostra também as canceladas (`total` pode ser maior). Mostre cada número como o contrato o define.
- **O gestor não vê o texto da contestação nem a resposta do RH** (`x-provisorio` de `AdvertenciaDetalheGestor`): vê a `situacao`, inclusive "Contestada".
- **Sem a coluna "Aplicada por"** (como no PNG, li).
- **Filtro de ano:** o PNG não tem; a tela usa o ano atual.
- **"Abrir"** abre o detalhe (ver abaixo).
- **Equipe vazia** (gestor sem setor): `totalDaEquipe` 0; mostre "Você ainda não é gestor de um setor."

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Subtítulo (setor) | `listarEquipeDoGestor` (fatia 1) | `setor.nome` |
| Três números e "Por pessoa" | `obterResumoDasAdvertenciasDaEquipe` | `ano` opcional |
| "Registros" | `listarAdvertenciasDaEquipe` | `situacao`, `ano`, `pagina`, `tamanhoPagina` |
| "Abrir" | `obterAdvertenciaDaEquipe` | 404 para pessoa de outro setor |
| "+ Registrar advertência" | `registrarAdvertenciaDaEquipe` (tela 60) | |

**Estados:** carregando (esqueleto nos números, na tabela e na grade); vazio "Nenhuma advertência registrada neste ano."; erro nas regras comuns; 403 para quem não é gestor.

**Observações para quem monta:** depois de registrar, invalide a lista, os números e a grade.

**Perguntas abertas (`PROVISORIOS.md`)**
- `components > schemas > AdvertenciaDetalheGestor`: o gestor vê o texto da contestação e o desfecho? (Hipótese: não.)

### Detalhe da advertência da equipe ("Abrir"; gestor; sem print; desenho meu)

- **Painel lateral** "Advertência de {dd/MM/yyyy}" (`obterAdvertenciaDaEquipe`), como o detalhe do RH: sobre a lista 59 é uma rota filha, `/gestor/advertencias/:advertenciaId`; aberto pela aba Advertências da ficha da equipe (que já é um painel), abre sobre a ficha **sem caminho** (`05-caminhos-de-tela.md`). **Nenhum PNG o desenha** (o PNG 59 só tem o botão "Abrir"; li): o tipo de superfície segue o detalhe do RH, para as duas telas serem iguais. Conteúdo: pessoa, "Data do ocorrido", "Tipo", "Aplicada por", "Motivo", "Descrição", "Situação" (chip, inclusive "Contestada") e "Ciência em {dd/MM/yyyy, HH:mm}" (`dataCienciaUtc`; vazio: "Ainda sem ciência"). **Sem** o texto da contestação, a data dela e a resposta do RH. Só leitura.

### Aba Advertências da ficha da equipe (gestor; sem print; desenho meu)

- A moldura da ficha (PNG 52, li: abas Dados, Ponto, Férias, Treinamentos e Advertências, e o botão "Registrar advertência" no cabeçalho) é da **fatia 1b**; só a aba Dados tem print.
- **Conteúdo:** a mesma tabela da tela 11 (Data, Tipo, Motivo, Aplicada por, Situação, "Abrir"), de `listarAdvertenciasDoFuncionarioDaEquipe` (todas as advertências da pessoa, de todos os anos, **inclusive canceladas**; pessoa de outro setor: 404) e o botão "Abrir" (`obterAdvertenciaDaEquipe`). O botão "Registrar advertência" do cabeçalho da ficha abre a tela 53.

---

## Tela 53: Registrar advertência, a partir da ficha da equipe (gestor)

**PNG:** `Figma\telas\53-gestor-painel-da-equipe-ficha-registrar-advertencia.png` · **Incremento:** 4c · **Rotas:** `registrarAdvertenciaDaEquipe`.

**O que fica (li no PNG 53)**
- Painel lateral "Registrar advertência", aberto sobre a ficha de Diego Farias (que fica ao fundo).
- **Colaborador** (preenchido e desligado: "Diego Farias"), **Data do ocorrido**, **Tipo** (Verbal), **Motivo** (placeholder "Resumo em uma linha, ex.: atrasos repetidos sem justificativa") e **Descrição** (placeholder "O que aconteceu, quando e qual regra foi descumprida").
- Caixa informativa: "A advertência fica registrada como aplicada por você. O RH recebe uma cópia e o colaborador confirma a ciência pelo portal, podendo contestar."
- Botões **"Cancelar"** e **"Registrar advertência"**.

**O que sai ou muda (com a razão)**
- **A caixa "Avisar o colaborador por e-mail" sai:** a pessoa recebe **sempre** o e-mail e o **RH recebe uma cópia** (decisão do dono, 2026-10-30; `CONVENCOES.md`, seção 12; contrato). A frase da caixa informativa ("O RH recebe uma cópia") **continua verdadeira**.
- **Sem campo "Aplicada por"**: quem aplica é sempre o gestor logado (`AdvertenciaRegistroGestorComando`).
- O gestor **só registra para a própria equipe** (outro setor ou o próprio id dá 404; ele não faz parte da equipe dele).

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| "Registrar advertência" | `registrarAdvertenciaDaEquipe` | `POST /api/gestor/equipe/{funcionarioId}/advertencias`; corpo `{ dataDoOcorrido, tipo, motivo, descricao }` |

**Estados:** salvando; sucesso "Advertência registrada. {nome} foi avisado por e-mail e o RH recebeu uma cópia." e atualiza a aba Advertências da ficha, o número "Advertências" do resumo da ficha e a tela 59; erros de campo (400); 422 `FUNCIONARIO_INATIVO` "Não é possível registrar advertência para uma pessoa inativa."; 404 pessoa fora da equipe.

**Observações:** é o **mesmo painel** da tela 60 (que escolhe a pessoa).

---

## Tela 60: Registrar advertência, a partir de "Advertências do setor" (gestor)

**PNG:** `Figma\telas\60-gestor-advertencias-da-equipe-registrar-advertencia.png` · **Incremento:** 4c · **Rotas:** `registrarAdvertenciaDaEquipe`, `listarEquipeDoGestor` (fatia 1).

**O que fica (li no PNG 60):** é o **mesmo painel da tela 53**, com a diferença de que o campo **"Colaborador"** é uma lista **"Escolha um colaborador"** (as pessoas de `listarEquipeDoGestor`; o gestor escolhe), aberto sobre a tela 59 (`/gestor/advertencias/nova`; o da tela 53 abre sobre a ficha, sem caminho).

**O que sai ou muda:** os mesmos cortes da tela 53 (sem a caixa de e-mail e sem "Aplicada por"). A lista de pessoas é **a equipe do gestor** (a mesma rota da fatia 1), sem paginação.

**Rotas por elemento:** campo "Colaborador" `listarEquipeDoGestor`; "Registrar advertência" `registrarAdvertenciaDaEquipe`.

**Estados e mensagens:** os da tela 53. Depois de salvar, atualize a lista, os números e a grade "Por pessoa" da tela 59.

---

## Conferência: toda rota da fatia 4 tem tela

| `operationId` | Rota | Tela |
|---|---|---|
| `listarAvisos` | `GET /api/rh/avisos` | 27 |
| `criarAviso` | `POST /api/rh/avisos` | 28 |
| `obterResumoDosAvisos` | `GET /api/rh/avisos/resumo` | 27 |
| `obterAviso` | `GET /api/rh/avisos/{avisoId}` | 27 |
| `editarAviso` | `PUT /api/rh/avisos/{avisoId}` | 28 (modo editar) |
| `excluirAviso` | `DELETE /api/rh/avisos/{avisoId}` | 27 (rascunho e agendado; sem print) |
| `publicarAviso` | `POST /api/rh/avisos/{avisoId}/publicar` | 27 (rascunho; sem print) e 28 |
| `encerrarAviso` | `POST /api/rh/avisos/{avisoId}/encerrar` | 27 |
| `listarLeiturasDoAviso` | `GET /api/rh/avisos/{avisoId}/leituras` | 27 |
| `lembrarQuemNaoViuAviso` | `POST /api/rh/avisos/{avisoId}/lembretes` | 27 |
| `listarMeusAvisos` | `GET /api/eu/avisos` | 37, 50 |
| `obterMeuAviso` | `GET /api/eu/avisos/{avisoId}` | 38 |
| `marcarAvisoComoLido` | `POST /api/eu/avisos/{avisoId}/leitura` | 38 |
| `listarHoleritesDoFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}/holerites` | 07, 08 |
| `enviarHolerite` | `POST /api/rh/funcionarios/{funcionarioId}/holerites` | 08 |
| `enviarHoleritesEmLote` | `POST /api/rh/holerites/lote` | Lote (sem print) |
| `substituirHolerite` | `POST /api/rh/holerites/{holeriteId}/versoes` | 07 ("Substituir"; modal sem print) |
| `obterLinkDeDownloadDoHolerite` | `GET /api/rh/holerites/{holeriteId}/download` | 07 |
| `listarMeusHolerites` | `GET /api/eu/holerites` | 36, 49 |
| `obterLinkDeDownloadDoMeuHolerite` | `GET /api/eu/holerites/{holeriteId}/download` | 36, 49 |
| `listarAdvertencias` | `GET /api/rh/advertencias` | 14 |
| `obterResumoDasAdvertencias` | `GET /api/rh/advertencias/resumo` | 14 |
| `obterAdvertencia` | `GET /api/rh/advertencias/{advertenciaId}` | 14 e 11 ("Abrir"; detalhe sem print) |
| `responderContestacaoDeAdvertencia` | `POST /api/rh/advertencias/{advertenciaId}/resposta` | 14 (detalhe; sem print) |
| `listarAdvertenciasDoFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}/advertencias` | 11 |
| `registrarAdvertencia` | `POST /api/rh/funcionarios/{funcionarioId}/advertencias` | 15, 11 |
| `listarAdvertenciasDaEquipe` | `GET /api/gestor/advertencias` | 59 |
| `obterResumoDasAdvertenciasDaEquipe` | `GET /api/gestor/advertencias/resumo` | 59 |
| `obterAdvertenciaDaEquipe` | `GET /api/gestor/advertencias/{advertenciaId}` | 59 ("Abrir"; detalhe sem print) |
| `listarAdvertenciasDoFuncionarioDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}/advertencias` | aba Advertências da ficha da equipe (sem print) |
| `registrarAdvertenciaDaEquipe` | `POST /api/gestor/equipe/{funcionarioId}/advertencias` | 53, 60 |
| `listarMinhasAdvertencias` | `GET /api/eu/advertencias` | 43 |
| `confirmarCienciaDeAdvertencia` | `POST /api/eu/advertencias/{advertenciaId}/ciencia` | 43 |
| `contestarAdvertencia` | `POST /api/eu/advertencias/{advertenciaId}/contestacao` | 43 ("Contestar"; modal sem print) |
| `listarAniversariantes` | `GET /api/eu/aniversariantes` | **sem tela nesta fatia: o cartão "Aniversariantes" da tela inicial é da fatia 7d (PNG 32 e 46)** |

35 de 35 rotas listadas; 34 com tela nesta fatia e 1 (`listarAniversariantes`) com a tela na 7d, como diz o roadmap.
