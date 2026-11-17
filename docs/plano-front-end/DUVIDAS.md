# DÚVIDAS do front-end

> Aqui ficam as dúvidas que **o plano, o contrato e as notas de tela não resolvem**. Cada uma tem um **texto pronto para a pessoa colar** e mandar ao dono; o texto **não menciona Claude nem IA** (começa com `Mensagem para o dono:`) e **nunca vai no corpo de um PR** (o PR só lista o código e uma linha de cada dúvida: `01-regras.md`, C3). As regras estão em `01-regras.md`, **seção D**: registre na hora; mostre **na hora**, **de novo uma única vez** na próxima mensagem da pessoa, **quando não puder mais avançar** e **no final**; nunca mais que isso. Dúvida que não bloqueia: continue; bloqueio parcial: faça o resto. Algumas dúvidas **já são conhecidas** e estão na tabela "Dúvidas já conhecidas", mais abaixo, cada uma com o **seu código fixo**.
>

## Decisões do dono

(Leia esta seção **antes de cada tarefa**: ela é o que você consulta. Uma linha por dúvida respondida. As de 2026-11-02 já estão aqui: **mandam sobre o texto antigo** de qualquer ficha, nota ou tarefa.)

| Data | Dúvida | Decisão do dono | Onde afeta |
|---|---|---|---|
| 2026-11-02 | `D-menu` | **A: sim.** O RH e o gestor ganham um grupo **"Meu espaço"** no fim do menu com os itens pessoais. **RH:** Meu ponto, Férias, Holerites, Minhas advertências, Meus formulários, Meus treinamentos, Benefícios. **Gestor** (que já tem Meu ponto, Férias, Holerites e Benefícios): Meus formulários, Meus treinamentos, Minhas advertências. (Ajuste de 2026-11-02: o item do RH se chama "Minhas advertências", para não repetir o "Advertências" do grupo Pessoas; leva à mesma rota, `/eu/advertencias`.) "Meus documentos" continua nos três menus. | `fatia-1a-fundacao.md`, Task 12 (`menu.ts`: linhas **ativas**); fichas 3, 4, 5, 6 e 7; `05-caminhos-de-tela.md`; os itens abrem as telas pessoais `/eu/...` e só aparecem quando a rota existe |
| 2026-11-02 | `D-rodape` | **A:** dois botões de texto pequenos no **rodapé do menu**, abaixo do nome e do cargo: "Trocar senha" (chega na 1b) e "Sair". | `fatia-1a-fundacao.md`, Task 12 (`RodapeDoMenu.tsx`); `fatia-1b-telas.md`, Task 7 |
| 2026-11-02 | `D-fonte` | **Manter a Inter** (trocar para a Hanken Grotesk só se o dono pedir depois). | `02-pilha.md`, `04-visual.md` (seção 4), `fatia-1a-fundacao.md`, Task 1; a pessoa ainda confere a letra ao lado do PNG |
| 2026-11-02 | `D6-3` | **Manter `CERT-{ano}-{id}`** (o que a API devolve); sem sigla por treinamento. | `fichas\fatia-6.md`; a tela do certificado mostra o `numero` como vem |
| 2026-11-02 | `D6-2` | **Não** criar a aba Treinamentos na ficha do colaborador do RH. | `fichas\fatia-6.md`; `listarTreinamentosDeFuncionario` fica fora da cobertura, com o motivo "decisão do dono, 2026-11-02" |
| 2026-11-02 | `D5-3` | **Deixar como está:** o gestor **não** vê formulário encerrado na lista da tela 57. | `fichas\fatia-5.md`, tela 57 (`listarFormulariosDaEquipe`); sem caminho para o estado "anônimo encerrado" do gestor, só trate a resposta se ela chegar |
| 2026-11-02 | `D-F4-5` | **Manter** o botão "Lembrar quem não viu" do aviso (tela 27); o e-mail entrou na lista fechada do contrato (`CONVENCOES.md`, seção 12, versão 0.2.1); **sem limite** de lembretes por aviso. **Não é mais provisório.** | `fichas\fatia-4.md`, tela 27 (`lembrarQuemNaoViuAviso`): construa normalmente e **não** anote "usa provisório" |
| 2026-11-02 | `D5-4` | **Manter** o botão "Lembrar quem falta" do gestor (tela 57). **Não é mais provisório.** | `fichas\fatia-5.md`, tela 57 (`lembrarQuemFaltaNaEquipe`): construa normalmente e **não** anote "usa provisório" |

## Modelo de uma dúvida

Copie o bloco abaixo para a seção "Dúvidas" e preencha. O que está entre `<...>` é para você preencher. **O código:** a dúvida **conhecida** (tabela "Dúvidas já conhecidas") usa o **código fixo** dela, **nunca** `D-001`; só a dúvida que **você** achar por conta própria recebe o número seguinte (`D-001`, `D-002`, ...).

````markdown
### <código> · <título curto> · [aberta | respondida | cancelada]

- **Registrada em:** <data e hora> (<fatia · tarefa · passo>)
- **Bloqueia:** <não | parcialmente (o que continua: ...) | totalmente>
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [ ]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida <código> do front-end do RH-System (<título curto>)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: <arquivo do plano e tarefa ou seção; tela NN, nome; rota (operationId); versão do contrato (`info.version`, lida de `contrato\openapi.yaml`)>.
  O que achei: <o fato, com arquivo e linha do que li; o que falta ou se contradiz>.
  O que sugiro: <proposta concreta e por quê; o que muda se você escolher outra>.
  Pergunta: <uma pergunta que se responde com "sim", "não" ou escolhendo A ou B>.
  Enquanto isso: <o que estou fazendo: sigo com X / parei em Y>.
  ```

- **Resposta do dono:** (vazio até a pessoa trazer; com a data)
- **O que mudou por causa da resposta:** (vazio; o que você fez; e a linha acrescentada em "Decisões do dono")
````

## Dúvidas já conhecidas (registre quando chegar ao passo que o plano indica)

Só estão aqui as que **continuam abertas** (as respondidas estão em "Decisões do dono", acima). **Cada dúvida tem um código só**, o da primeira coluna: ao registrar, copie o bloco do modelo para a seção "Dúvidas" **com esse código** (nunca `D-001`) e preencha os campos. O **conteúdo** de cada uma (o que achei, o que sugiro, a pergunta) mora no arquivo da coluna "Nasce em"; o texto para colar sai dele e do modelo acima. **Nenhuma bloqueia.**

| Código | Nasce em | O que fazer enquanto o dono não responder (o padrão) |
|---|---|---|
| `D-benef` | `fatia-1b-telas.md`, Task 2, passo 6 (tela 02, benefícios já marcados no cadastro); o conteúdo está em "Dúvidas conhecidas desta fatia" do mesmo arquivo | Todos os benefícios **desmarcados** (o que o contrato diz): quem cadastra marca os que valem. |
| `D-editar-inativo` | `fatia-1b-telas.md`, Task 3, parte D, passo D5 (tela 03, "Editar dados" para quem está inativo); o conteúdo está no mesmo arquivo | "Editar dados" aparece **também para quem está inativo**. |
| `D-F4-1` | `fichas\fatia-4.md`, seção "4b. Dúvidas já conhecidas" (tela 08, modal "Enviar holerite") | Construa a terceira opção, "Agendar para uma data" (o contrato a permite; o Figma não a desenha). |
| `D-F4-3` | `fichas\fatia-4.md`, seção 4b (telas 36, 49 e 43: link "Fale com o RH") | Mostre como texto simples, **sem link** (o contrato não tem rota nem endereço para isso). |
| `D-F4-4` | `fichas\fatia-4.md`, seção 4b (envio de holerites em lote, `enviarHoleritesEmLote`, sem tela) | Um botão "Enviar holerites em lote" ao lado de "Adicionar colaborador" na lista de colaboradores (tela 01), que abre a tela descrita em `telas\fatia-4.md`. |
| `D5-2` | `fichas\fatia-5.md`, seção "10. Decisões do dono de 2026-11-02 e dúvidas já conhecidas" (tela 16, "Encerrou em ...") | Mostre o dia de `encerradoEmUtc` (pode dar um dia depois do prazo que o PNG mostra). |
| `D6-4` | `fichas\fatia-6.md`, seção 10 (incluir no onboarding uma pessoa já cadastrada, a partir da ficha da tela 03; `incluirFuncionarioNoOnboarding`) | **Sem botão**: a rota fica na exclusão da cobertura. |
| `D-F7-1` | `fichas\fatia-7.md`, seção "9. Decisão do dono sobre o menu e dúvidas que a sessão já pode registrar" (tela 51, sugestões de outra data) | Deixe assim: o gestor só vê as sugestões que aguardam resposta na aba Férias da ficha da pessoa. |
| `D-F7-2` | `fichas\fatia-7.md`, seção 9 (tela 7.4, "Pagamento") | Siga o contrato: monte "Pagamento" dentro de Cadastros, ao lado de "Empresa". |
| `D-V-contraste` | vistoria do front-end (2026-11-17): cores do Figma abaixo de 4,5:1 (`primaria-media` sobre fundo claro, `borda-campo` 1,47:1, número da bolinha do menu 4,2:1) | **Seguir o Figma** e registrar; não mexo em `index.css`. |
| `D-V-avaliacao-encerrada` | vistoria (2026-11-17): "Ver minhas respostas" de uma avaliação já encerrada não tem de onde tirar o `avaliadoId` | Sem o atalho; a pessoa vê o que a lista dela mostra. |
| `D-V-token-na-url` | vistoria (2026-11-17): o link de definir senha deixa o `token` no endereço | **Mantido**: o site manda `Referrer-Policy: no-referrer`, o link vale uma vez e recarregar a página quebraria o formulário. |
| `D-F8-1` | `fichas\fatia-8.md`, seção "9. Dúvidas que a sessão pode registrar" (roteiro da demo) | Descreva os quatro fluxos no roteiro; o dono corta. |
| `D-F8-2` | `fichas\fatia-8.md`, seção 9 (plano B da demo) | Se a API cair, mostre as telas pelo servidor de desenvolvimento com os dados de `ELENCO.md`, avisando a banca. |
| `D-F8-3` | `fichas\fatia-8.md`, seção 9 (resolução do projetor) | Projete para 1366 px, com piso de 1280 px (`04-visual.md`, seção 9). |

**Não é dúvida:** CPF e telefone completos do RH aparecem **com pontuação** (`846.392.710-69`, `(11) 98765-4410`); é o padrão adotado.

## Dúvidas

### D-benef · Benefícios já marcados no cadastro de colaborador · aberta

- **Registrada em:** 2026-11-03 14:20 (F1b·T2·S6)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [x] · de novo na próxima mensagem [x] · ao travar [ ] · no final [ ]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-benef do front-end do RH-System (benefícios já marcados no cadastro de colaborador)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 1b, Task 2 (tela 02, "Adicionar colaborador"); PNG 02; contrato 0.2.2, criarFuncionario (beneficioIds, padrão []); telas\fatia-1.md, "Perguntas abertas", item 1.
  O que achei: o desenho mostra três benefícios já marcados no cadastro (Vale-refeição, Plano de saúde e Seguro de vida). O contrato diz que a lista começa vazia, e nenhum documento diz que existem benefícios que já vêm marcados.
  O que sugiro: deixar todos desmarcados (o que o contrato diz); quem cadastra marca os que valem. Se você quiser três já marcados, preciso saber quais e se são sempre os mesmos.
  Pergunta: todos desmarcados (A) ou alguns já marcados (B, e diga quais)?
  Enquanto isso: sigo com todos desmarcados.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-editar-inativo · "Editar dados" para quem está inativo · aberta

- **Registrada em:** 2026-11-03 14:55 (F1b·T3·D5)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [x] · de novo na próxima mensagem [x] · ao travar [ ] · no final [ ]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-editar-inativo do front-end do RH-System ("Editar dados" para quem está inativo)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 1b, Task 3 (tela 03, ficha do colaborador); telas\fatia-1.md, seção "Tela 03", item "Botões do cabeçalho conforme situacao"; contrato 0.2.2, editarFuncionario.
  O que achei: o contrato não proíbe editar os dados de quem está inativo, e o desenho só mostra o botão "Editar dados" de quem está ativo. Para quem está inativo, o contrato manda "Readmitir" no lugar de "Inativar".
  O que sugiro: mostrar "Editar dados" também para quem está inativo (por exemplo, para corrigir um CPF ou um endereço antes de readmitir). Se não puder, o botão some quando a pessoa está inativa e nada mais muda.
  Pergunta: quem está inativo pode ter os dados editados (A) ou só ser readmitido (B)?
  Enquanto isso: sigo com "Editar dados" aparecendo também para inativo.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F4-1 · Opção "Agendar para uma data" no modal Enviar holerite · aberta

- **Registrada em:** 2026-11-04 02:30 (F4, passo 4)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F4-1 do front-end do RH-System (opção "Agendar para uma data" no envio de holerite)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: telas\fatia-4.md, tela 08 (modal "Enviar holerite"); rota enviarHolerite; contrato 0.2.2.
  O que achei: o PNG 08 só tem "Publicar agora" e "Agendar para o dia do pagamento". O contrato também aceita agendar numa data (publicarEm), e a própria mensagem de erro da API (422 EMPRESA_NAO_CONFIGURADA) diz "ou escolha uma data".
  O que sugiro: manter a terceira opção, "Agendar para uma data" (já construída, também no "Substituir" e no lote). Se você não quiser, ela some e a mensagem de erro deixa de citar a data.
  Pergunta: posso manter a opção "Agendar para uma data", que o Figma não desenha mas o contrato permite?
  Enquanto isso: a opção está construída e testada.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F4-3 · Destino do link "Fale com o RH" · aberta

- **Registrada em:** 2026-11-04 02:30 (F4, passos 3 e 5)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F4-3 do front-end do RH-System (destino do "Fale com o RH")

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: telas\fatia-4.md, telas 36, 49 e 43; contrato 0.2.2 (nenhuma rota).
  O que achei: os PNG mostram "Dúvida sobre um valor? Fale com o RH" e "Dúvidas sobre uma advertência? Fale com o RH" como links, mas o contrato não tem rota nem endereço para isso.
  O que sugiro: deixar como texto simples, sem link, até você dizer o destino (por exemplo um e-mail do RH, que entraria na configuração da empresa).
  Pergunta: o que acontece ao clicar em "Fale com o RH"?
  Enquanto isso: o texto aparece sem link.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F4-4 · De onde se abre o envio de holerites em lote · aberta

- **Registrada em:** 2026-11-04 02:30 (F4, passo 4)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F4-4 do front-end do RH-System (onde fica o envio de holerites em lote)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: telas\fatia-4.md, "Enviar holerites em lote" (sem print); rota enviarHoleritesEmLote (x-provisorio); contrato 0.2.2.
  O que achei: o contrato tem o envio em lote, mas o Figma não desenha o lote nem diz de onde ele se abre.
  O que sugiro: um botão "Enviar holerites em lote" ao lado de "Adicionar colaborador" na lista de colaboradores (tela 01), que abre um modal: mês, ano, tipo, quando publicar, os PDFs e, para cada um, a pessoa.
  Pergunta: posso abrir o envio em lote por esse botão na lista de colaboradores?
  Enquanto isso: está construído assim e testado.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D5-2 · "Encerrou em ..." na lista de formulários · aberta

- **Registrada em:** 2026-11-04 (F5, bloco 1)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D5-2 do front-end do RH-System ("Encerrou em" na lista de formulários)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: telas\fatia-5.md, tela 16 (lista de formulários do RH); rota listarFormulariosRh; contrato 0.2.2.
  O que achei: o PNG 16 diz "Encerrou em 30/09". No exemplo do contrato o prazo é 30/09 e o encerramento automático (encerradoEmUtc) foi em 2026-10-01T03:00Z, que em Brasília é 01/10. O contrato não diz qual das duas datas a lista mostra.
  O que sugiro: mostrar o dia em que o sistema encerrou (01/10), que vale também para quem encerra à mão antes do prazo. Se preferir o último dia do prazo, a lista passa a usar o prazo quando ele existir.
  Pergunta: a lista mostra o dia em que o sistema encerrou (A) ou o último dia do prazo (B)?
  Enquanto isso: mostro o dia de encerradoEmUtc (A). Muda um dia na tela.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D6-4 · Incluir no onboarding uma pessoa já cadastrada, a partir da ficha · aberta

- **Registrada em:** 2026-11-04 (F6, bloco 7)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D6-4 do front-end do RH-System (incluir no onboarding uma pessoa já cadastrada)

  Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 6 (treinamentos), ficha do colaborador (tela 03, fatia 1); rota incluirFuncionarioNoOnboarding; contrato 0.2.2.
  O que achei: o contrato tem uma rota para incluir no treinamento de onboarding uma pessoa que já está cadastrada (por exemplo, a partir da ficha), mas o Figma não desenha nenhum botão para isso. Hoje a inclusão só acontece pela caixa "Incluir no treinamento de onboarding" ao cadastrar, importar ou readmitir.
  O que sugiro: não criar o botão; quem já está cadastrado e ficou de fora do onboarding não teria como entrar depois. Se você quiser, o botão "Incluir no onboarding" entra na ficha, só para o RH.
  Pergunta: o RH deve poder incluir no onboarding uma pessoa já cadastrada, a partir da ficha? (A) não, só pela caixa do cadastro, da importação e da readmissão; (B) sim, com um botão na ficha.
  Enquanto isso: sigo sem o botão.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F7-1 · O gestor não tem lista das sugestões de outra data que ainda esperam resposta · aberta

- **Registrada em:** 2026-11-17 (F7, bloco 7a)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F7-1 do front-end do RH-System (sugestões de outra data de férias)

  Olá! Estou fechando o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 7 (gestão), Painel da equipe (tela 51); rota listarFeriasDaEquipe; contrato 0.3.0.
  O que achei: quando o gestor sugere outra data para as férias de alguém, o pedido espera a resposta da pessoa e deixa o painel e o calendário. O gestor só volta a ver essa sugestão na aba Férias da ficha da pessoa. A rota listarFeriasDaEquipe (com situacao=SUGESTAO_DE_DATA) listaria todas, mas não há tela para ela.
  O que sugiro: deixar como está.
  Pergunta: o gestor precisa de uma lista das sugestões de data que aguardam resposta? (A) não, a aba Férias da ficha basta; (B) sim, um bloco no Painel da equipe.
  Enquanto isso: sigo sem o bloco.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F7-2 · "Pagamento" dentro de Cadastros · aberta

- **Registrada em:** 2026-11-17 (F7, bloco 7c)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F7-2 do front-end do RH-System (tela de Pagamento)

  Olá! Estou fechando o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 7, tela 7.4 (calendário de pagamento e exceções por mês); contrato 0.3.0.
  O que achei: o Figma não desenha essa tela. O contrato sugere um item "Pagamento" em Cadastros, ao lado de "Empresa".
  O que sugiro: seguir o contrato (foi o que fiz).
  Pergunta: posso manter "Pagamento" dentro de Cadastros? (A) sim; (B) não, quero em outro lugar.
  Enquanto isso: ficou em Cadastros.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-V-contraste · Cores do Figma abaixo do contraste recomendado · aberta

- **Registrada em:** 2026-11-17 (vistoria do front-end)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-V-contraste do front-end do RH-System (contraste das cores)

  Olá! Conferi o front-end inteiro e preciso de uma decisão sua.

  Onde: cores do desenho (04-visual.md e index.css).
  O que achei: alguns pares de cor do Figma ficam abaixo de 4,5:1 de contraste (WCAG): a cor de destaque sobre fundo claro, a borda dos campos (1,47:1) e o número da bolinha do menu (4,2:1). O desenho foi seguido à risca.
  O que sugiro: manter o desenho agora; se a avaliação de acessibilidade pesar, escurecer essas três cores.
  Pergunta: manter as cores do Figma (A) ou escurecer as que ficam abaixo de 4,5:1 (B)?
  Enquanto isso: sigo o Figma.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-V-avaliacao-encerrada · "Ver minhas respostas" de uma avaliação encerrada · aberta

- **Registrada em:** 2026-11-17 (vistoria do front-end)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-V-avaliacao-encerrada do front-end do RH-System (minhas respostas)

  Olá! Conferi o front-end inteiro e preciso de uma decisão sua.

  Onde: formulários da pessoa (Meus formulários), avaliação já encerrada.
  O que achei: para abrir "Minhas respostas" de uma avaliação, a API pede qual pessoa foi avaliada, e a lista de formulários encerrados não informa isso. Hoje a tela não oferece o atalho nesse caso.
  O que sugiro: a API incluir o avaliado na lista de "Meus formulários".
  Pergunta: quer que a lista passe a trazer o avaliado (A) ou fica sem esse atalho (B)?
  Enquanto isso: sigo sem o atalho.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-V-token-na-url · O link de definir senha deixa o token no endereço · decidida (mantido)

- **Registrada em:** 2026-11-17 (vistoria do front-end)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-V-token-na-url do front-end do RH-System (segurança do link de senha)

  Olá! Conferi o front-end inteiro e deixo um registro para você.

  Onde: tela de definir senha (/definir-senha?token=...).
  O que achei: o código do link fica no endereço da página, então aparece no histórico do navegador. Tirar o código do endereço depois de ler faria a página quebrar se a pessoa recarregar.
  O que fiz: mantive como está. O site manda Referrer-Policy: no-referrer (o código não sai para outros sites) e o link vale uma única vez.
  Pergunta: tudo bem manter assim (A) ou prefere tirar o código do endereço, aceitando que recarregar a página peça um link novo (B)?
  Enquanto isso: mantido.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F8-1 · Quais fluxos entram no roteiro da demo · aberta

- **Registrada em:** 2026-11-17 (F8)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F8-1 do front-end do RH-System (roteiro da demo)

  Olá! Estou fechando o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 8, roteiro da demo (PROGRESSO.md, seção "Roteiro da demo").
  O que achei: o roteiro pede "entrar e sair", "bater ponto", "importar planilha" e "pedir e aprovar férias". Deixei os quatro, mais a exportação do ponto, rodando contra a API real com o seed fictício.
  O que sugiro: manter os quatro e você corta o que não couber no tempo.
  Pergunta: quais fluxos entram na demo e em que ordem?
  Enquanto isso: o roteiro traz todos, numerados.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F8-2 · Plano B da demo · aberta

- **Registrada em:** 2026-11-17 (F8)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F8-2 do front-end do RH-System (plano B da demo)

  Olá! Estou fechando o front-end do RH-System e preciso de uma decisão sua.

  Onde: fatia 8, roteiro da demo.
  O que achei: se a API cair durante a demo, o front ainda roda com dados simulados (o servidor de desenvolvimento, sempre como Ana Souza, do RH, com a data fixa de 07/10/2026). Não é a demo real.
  O que sugiro: usar só se a API cair, avisando a banca.
  Pergunta: esse plano B é aceitável (A) ou a demo só vale contra a API real (B)?
  Enquanto isso: o roteiro descreve o plano B com o aviso.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)

### D-F8-3 · Resolução do projetor · aberta

- **Registrada em:** 2026-11-17 (F8)
- **Bloqueia:** não
- **Mostrada à pessoa:** na hora [ ] · de novo na próxima mensagem [ ] · ao travar [ ] · no final [x]
- **Texto para colar para o dono:**

  ```
  Mensagem para o dono: dúvida D-F8-3 do front-end do RH-System (resolução da apresentação)

  Olá! Estou fechando o front-end do RH-System e preciso de uma informação sua.

  Onde: fatia 8, conferência de largura.
  O que achei: o desenho é de 1440 px de largura. Conferi as telas a 1366×768 (um projetor comum) contra a API real e nenhuma rola na horizontal, mas a 768 px de altura o menu do RH rola por dentro.
  O que sugiro: apresentar a 1366×768 ou maior, com o zoom do navegador em 100%.
  Pergunta: qual é a resolução real do projetor da banca e do notebook de quem apresenta?
  Enquanto isso: sigo com 1366×768 como referência.
  ```

- **Resposta do dono:** (vazio)
- **O que mudou por causa da resposta:** (vazio)
