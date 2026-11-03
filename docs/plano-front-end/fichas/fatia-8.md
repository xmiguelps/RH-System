# Ficha da fatia 8: fechamento (front-end)

Contrato lido: nenhum arquivo novo. **A fatia 8 não tem tela nem rota** (`contrato\ROTAS.md` vai da fatia 0 à 7; `contrato\STATUS.md` também). Ela fecha o que as fatias 0 a 7 entregaram. Notas de tela: `docs\plano-front-end\telas\fatia-8.md` (curta: a lista de conferência final). Fontes: roadmap, fatia 8 (`docs\superpowers\plans\2026-10-28-roadmap-completo.md`, "Fatia 8: fechamento"), spec seção 7 (testes) e seção 9 (limitações), design `2026-10-30-planos-backend-frontend-design.md` (seções 4, 5 e 9), `contrato\ELENCO.md` e `contrato\STATUS.md`.

Esta ficha **não traz passo a passo fino**. Repita o padrão da fatia 1b (`fatia-1b-telas.md`): um passo pequeno por vez, cada um com um comando que prova, `PROGRESSO.md` atualizado a cada passo, commit e push por passo na sua branch (regras em `01-regras.md`). **Só comece quando as fichas 2 a 7 estiverem feitas** (a fatia 8 depende de todas).

## 1. Objetivo e entrega demonstrável

O front-end **pronto para a banca**: todas as telas conferidas ao lado do print, o layout sem quebrar num projetor de **1366×768** (as duas conferências são da pessoa), o trecho do roteiro da demo que cabe ao front e as limitações do front escritas. **Os testes de ponta a ponta contra a API real (Playwright, pasta `e2e\`) são do dono**, no plano do back-end: o front **não** os escreve, **não** instala ferramenta para eles e **não** cria pasta de testes de ponta a ponta.

O que se mostra no fim: o relatório de conferência visual de **todas** as telas, a lista de rotas a conferir a 1366 px (sem rolagem horizontal da página) e a seção "Roteiro da demo" do `PROGRESSO.md`, com a ordem das telas, o perfil e o que esperar em cada uma.

## 2. Telas e rotas

**Nenhuma tela nova, nenhuma rota nova.** O que existe de tela está nas fichas 0 a 7 e nas notas `telas\fatia-0.md` a `telas\fatia-7.md`; a lista de conferência final está em `telas\fatia-8.md`.

## 3. O que o front faz nesta fatia

### 3.1 Fluxos da demo (insumo do roteiro; a automação de ponta a ponta é do dono)

O roadmap (fatia 8) pede os fluxos da demo; o spec (seção 7) falava em 2 ou 3 e o roadmap acrescentou o de férias. **Quais entram no roteiro é decisão do dono** (roadmap, fatia 8, "Decisões do dono"): descreva os quatro no roteiro e deixe a escolha para ele. A tabela abaixo é o que o front sabe de cada fluxo, e serve também de insumo ao `e2e\` do dono.

| Fluxo | Perfil (pessoa de `ELENCO.md`) | O que prova | Rotas (`operationId`) | Depende de (`STATUS.md` `pronta`) |
|---|---|---|---|---|
| Entrar e sair | RH (Ana Souza), Gestor (Carlos Ribeiro), Funcionário (Bruno Lima) | login, menu do perfil certo, F5 não desloga, sair | `entrar`, `obterUsuarioLogado`, `renovarSessao`, `sair` | fatia 0 |
| Bater ponto | Funcionário | o cartão "Seu ponto hoje" mostra a batida nova | `obterPontoHoje`, `registrarMarcacaoDePonto` | fatias 0 e 3 |
| Importar planilha | RH | baixar o modelo, enviar, ver a prévia com erros, enviar a corrigida, confirmar, ver as pessoas na lista | `baixarModeloDeImportacao`, `validarPlanilhaDeImportacao`, `confirmarImportacao`, `listarFuncionarios` | fatias 1 e 2 |
| Pedir e aprovar férias | Funcionário pede, Gestor aprova, RH confirma, Funcionário vê "Aprovada" | as duas etapas de ponta a ponta | `obterMinhasFerias`, `conferirPedidoDeFerias`, `pedirFerias`, `obterPainelDaEquipe`, `aprovarFeriasComoGestor`, `listarFeriasParaORh`, `confirmarFeriasComoRh` | fatias 1 e 7 |

**De quem é cada coisa:**
- **A ferramenta e o pipeline de ponta a ponta são do dono** (design, seção 5: Playwright, API, banco e captador de e-mail na CI): ficam no diretório **`e2e\`** na raiz, criado por ele como passo de integração do plano do back-end (`docs\superpowers\plans\2026-10-30-plano-back-end.md`, tabela de entradas, item "Ponta a ponta (Playwright)"), depois de o front da fatia 1 estar na `main`. O front **não** o escreve, **não** o instala e **não** cria pasta de testes de ponta a ponta (o `csp` da fatia 0 prova só a política de segurança e fica onde está).
- O **seed** que volta ao começo entre execuções e o ambiente no Azure também são do dono (seção 4).

**O que o roteiro do front registra sobre estes fluxos** (cuidados que o dono precisa ao automatizar, ou ao ensaiar):
- Credenciais do seed vêm de **variáveis de ambiente**, nunca do repositório (regra de segredos).
- **Não suponha a data de `ELENCO.md`** (2026-10-07): o ambiente roda na data de verdade; as datas se calculam a partir de hoje. Nas férias, `conferirPedidoDeFerias` é o árbitro (não se reimplementa a regra): avance o início até `podeEnviar: true`.
- Cada fluxo **muda o estado** (a batida, o pedido de férias): sem o seed voltar ao começo, a segunda execução falha ou, pior, passa por acaso.
- Nomes e perfis: só as pessoas de `ELENCO.md`.

### 3.2 Conferência visual final

Cada fatia já listou, no relatório final dela, as telas a conferir. Aqui se **consolida**: `telas\fatia-8.md` traz a lista das **61 telas com print** (por fatia e número de PNG) e manda ler as seções "sem print" das notas das outras fatias. A pessoa confere ao lado do print; **a sessão não depende disso** para terminar e **não faz captura automática de tela**.

### 3.3 Largura de projetor (1366×768, provisório): conferência da pessoa

O Figma é de 1440 px; a demo pode ser num projetor de 1366×768 (design, seção 9; não verificado). O padrão de partida é **projetar para 1366** (`04-visual.md`, seção 9; já vem das fichas 1 a 7).
- A largura de conferência é **1366×768, fixa**: a mesma de `04-visual.md` e da fundação (1a). Não há largura a escolher. O piso do layout é o `min-width` de 1280 px que a fundação já definiu (`PortalLayout`; `04-visual.md`, seção 9): abaixo dele a página rola na horizontal; **não crie layout móvel** (limitação documentada, spec seção 9).
- **Conferência da pessoa, no relatório final, não bloqueia:** a pessoa abre **cada rota** a 1366×768 e confere: sem rolagem horizontal da página, nada cortado, e todo **modal e painel lateral** com o corpo rolando por dentro e os botões de rodapé sempre visíveis. A sessão entrega a **lista das rotas** (de `05-caminhos-de-tela.md`) e os casos de alerta que li: modal "Registrar férias" (tela 06: cerca de 900 px de altura), Painel da equipe (tela 51) e painel inicial do RH (tela 00); o guia completo está em `telas\fatia-8.md`, seção 3. **Nenhum passo desta seção exige que a sessão olhe um navegador**, e a sessão **não** escreve script de geometria nem de ponta a ponta (A11, e a seção 3.1).
- Se a pessoa devolver uma rota com rolagem horizontal da página ou um botão de modal fora da janela, a sessão corrige no código e registra no `PROGRESSO.md`.
- Ao ajustar, **não quebre** o desenho de 1440: a conferência visual continua sendo contra o print.

### 3.4 Roteiro da demo (o trecho do front) e lista do ensaio

O **roteiro** e o **ensaio** são do dono (roadmap, fatia 8); o front entrega o **rascunho do trecho de telas**: para cada passo, o perfil, a pessoa de `ELENCO.md`, a tela (número do PNG), o que clicar, o que deve aparecer e as rotas envolvidas. Proposta de ordem, para o dono cortar ou reordenar: Ana Souza no painel (00); importar planilha; Bruno Lima bate ponto e pede férias (32, 35); Carlos Ribeiro aprova no Painel da equipe (51); Ana Souza confirma (tela 7.2 da fatia 7); e o que mais o dono quiser (formulário anônimo, treinamento com certificado, holerite).
- Guarde o rascunho numa seção **`## Roteiro da demo` do `PROGRESSO.md`** (crie a seção se o modelo ainda não a trouxer). **Não crie** `docs\demo\...` nem nenhum outro arquivo de documentação: em `docs\plano-front-end\` só `PROGRESSO.md` e `DUVIDAS.md` são seus (`01-regras.md`, A13 e B3).
- **Lista de verificação do ensaio** (curta, na mesma seção): uma conta por perfil (as senhas ficam fora do repositório), navegador e zoom a 100%, resolução do projetor, ambiente aquecido (a primeira chamada de uma API parada pode demorar; suponho, não medido), e o que fazer se a API não responder.
- **Plano B** (para o dono decidir): se a API cair durante a demo, o servidor de desenvolvimento com MSW mostra as telas com os dados de `ELENCO.md` (data fixa 2026-10-07). Não é a demo real; diga isso na hora.

### 3.5 Limitações do front, escritas

Para o roteiro e para a banca (spec, seções 9 e 13.5): **só desktop** (sem layout móvel; o Figma só tem desktop, 1440 px, tema claro); largura mínima de 1280 px (3.3); navegador usado na verificação do `csp` (Edge ou Chrome, o instalado; suponho que a demo use o mesmo); a conformidade legal completa do ponto não é objetivo, o horário da batida vem sempre do servidor (isso é do back-end, mas aparece na tela de ponto); **sem tela de notificações** (o Figma não a desenha; spec, seção 13.1); o front não guarda dado de negócio no navegador (só a sessão, como a fundação definiu; ver `fatia-0.md`).

### 3.6 Fechamento do plano

1. **Versão do contrato**: compare a versão atual de `contrato\openapi.yaml` (`info.version`) com a anotada no `PROGRESSO.md` por tela. Se mudou, regenere os tipos e os dados simulados e rode **tudo**: os testes falham apontando as telas afetadas (regra de `03-contrato-e-mocks.md`). A versão **1.0.0** é a da publicação na `main`.
2. **Pendências de integração**: percorra o `PROGRESSO.md` e releia `contrato\STATUS.md` **só para saber se a rota existe**. A sessão **não roda** passo que exige a API real (design, seção 5, parágrafo "Pronto no front"): cada item continua `pendente de integração`, com o **roteiro pronto** no `PROGRESSO.md`, e, para cada rota que passou a `pronta`, acrescente a nota "rota `pronta` em `STATUS.md`: a pessoa ou o dono roda o roteiro". No relatório final, liste os roteiros que já podem ser rodados e por quem.
3. **Provisórios**: releia `contrato\PROVISORIOS.md` e liste no relatório final os que **continuam abertos** e quais telas dependem deles.
4. **Dúvidas**: mostre de novo, no relatório final, as de `DUVIDAS.md` ainda sem resposta (mais uma vez, como manda `01-regras.md`).
5. **Build final**: confirme que o `mockServiceWorker.js` **não** está em `dist` (o `postbuild` o apaga) e que a política de segurança passa (o `csp` da prova padrão).
6. **Relatório final** e **PR** (regras de git de `01-regras.md`; sem coautor Claude). **O corpo do PR lista só os títulos das dúvidas abertas** (o código e uma linha de cada), nunca o texto pronto para o dono; a conferência de C6 (sem coautor nem menção a Claude) cobre **os commits e o corpo do PR**.

## 4. O que não é do front (não toque)

DER/MER e revisão dos scripts com o grupo; seed completo e fictício da demo (e a forma de voltá-lo ao começo); o pipeline e o ambiente no Azure; **os testes de ponta a ponta contra a API real (Playwright, diretório `e2e\`)**; custos e como desligar o ambiente; o ensaio em si; o contrato e `STATUS.md`. Se o front precisar de algo disso, registre uma dúvida.

## 5. Regras e cortes que valem

- O contrato é lei; o front não edita banco, back-end, contrato nem scripts.
- **Dado fictício sempre**: nenhum dado real em teste, seed ou roteiro; e-mails do seed que não geram devolução (do back-end).
- **Segredos nunca no repositório** (senhas de seed, URL privada).
- Sem captura automática de tela; a conferência visual é da pessoa, uma vez.
- O que as fichas 2 a 7 cortaram (o que o Figma mostra e o projeto não terá) **continua cortado**: não reabra na reta final.

## 6. Ordem de construção e dependências

Depende de **todas** as fatias (0 a 7). Ordem sugerida, um passo por item, cada um com a verificação dita:
1. A **lista de conferência visual consolidada** (`telas\fatia-8.md`, seções 1 e 2) para o relatório final.
2. A **lista de rotas a conferir a 1366×768** (3.3) e as correções de largura que a pessoa pedir.
3. A seção **`## Roteiro da demo`** do `PROGRESSO.md` e a lista do ensaio (3.4).
4. O **fechamento** (3.6).

## 7. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações), rodado da raiz do repositório (`01-regras.md`, A14). Esta fatia não tem operação nova e **não existe `coberturaDaFatia8`**: no lugar do passo `coberturaDaFatiaN`, rode **todos** os testes de cobertura de uma vez, com o comando do bloco e o `N` tirado, no PowerShell 5.1 e **sem `--`**: `npm --prefix front-end test src/app/coberturaDaFatia` (o filtro do vitest é por trecho do caminho, o mesmo mecanismo do `npm --prefix front-end test src/app` da 1b: casa `coberturaDaFatia1` a `coberturaDaFatia7`; suponho, não medido, porque a pasta `front-end` ainda não existe). **Todos passam.** Cada um falha listando as operações da sua fatia sem `capturar('<operationId>')` ou `esperarRequisicaoConforme`, e **nunca se afrouxa uma lista**. **As exclusões das fichas 3 a 7** (a operação sem tela que cada uma tira da conta da sua fatia) **cada ficha já lista as suas, na seção "Definição de pronto", e a lista fixa do `coberturaDaFatiaN.test.ts` dela as reflete**: a 8 **não as repete e não as muda**. As duas operações que a ficha 1b e a ficha 4 deixam para a tela da 7 (`listarEquipeDoGestor` e `listarAniversariantes`) entram na conta da **ficha 7**, então nenhuma operação com tela fica fora de todos os testes. Operação que continua sem teste e sem exclusão escrita numa ficha é defeito daquela fatia (corrija lá, com o motivo), não desta. O que esta ficha acrescenta (nada disto é teste de navegador):
- a **lista de conferência visual** de todas as telas e a **lista de rotas a conferir a 1366×768** no relatório final, marcadas como "conferência da pessoa, não bloqueia" (3.2 e 3.3);
- `PROGRESSO.md` com a versão do contrato e o estado de cada tela, **e a seção `## Roteiro da demo`** escrita (3.4);
- o fechamento (3.6) feito; relatório final com a lista de conferência, as dúvidas e os provisórios abertos.

### Pendente de integração

Os 4 fluxos da demo dependem das fatias 0, 1, 2, 3 e 7 estarem `pronta` em `contrato\STATUS.md` (hoje todas `planejada`); a automação ponta a ponta deles é do dono (`e2e\`). Sem isso, o roteiro marca cada fluxo como "pendente de integração" (com o motivo: rota que não está `pronta`); **com** a rota `pronta`, o fluxo continua pendente com o roteiro pronto, para a pessoa ou o dono rodar. O front **não** roda nada contra a API real nem escreve teste de ponta a ponta.

## 8. Riscos

- **Nada pronto no back-end** quando a fatia 8 chegar: os fluxos ficam "pendentes" e a entrega vira o roteiro, a conferência e a largura. É um resultado aceitável, desde que dito.
- **Roteiro que ignora a data de verdade:** regra de antecedência de férias, batida de ponto e "hoje" mudam; o roteiro diz que as datas se calculam a partir de hoje (3.1). Primeira chamada lenta depois de o ambiente ficar parado (a API pode dormir; suponho, não medido): o roteiro manda ensaiar com o ambiente aquecido.
- **1366×768:** modais e painéis mais altos que a tela; tabelas largas (Painel da equipe, Ponto da equipe, prévia da importação). O ajuste não pode estragar 1440.
- **Reabrir decisão na reta final:** os cortes das fichas 2 a 7 não se reabrem sem o dono.

## 9. Dúvidas que a sessão pode registrar (o conteúdo abaixo; o texto para colar sai do modelo de `DUVIDAS.md`)

1. **D-F8-1, roteiro.** Achei: o roadmap pede "pedir e aprovar férias" entre os fluxos da demo e o spec fala só em login, bater ponto e importar planilha; o roadmap deixa a escolha dos fluxos ao dono. Sugiro: descrever os quatro no roteiro e o dono corta. Pergunta: "Quais fluxos entram no roteiro da demo?"
2. **D-F8-2, plano B da demo.** Achei: o MSW roda em desenvolvimento e nos testes, não no `dist`. Sugiro: se a API cair, mostrar as telas pelo servidor de desenvolvimento com os dados de `ELENCO.md`, avisando a banca. Pergunta: "Esse plano B é aceitável, ou a demo só vale contra a API real?"
3. **D-F8-3, resolução do projetor.** Achei: 1366×768 é hipótese (design, seção 9). Sugiro: projetar para 1366, com piso de 1280 px (`04-visual.md`, seção 9) e rolagem horizontal abaixo disso. Pergunta: "Qual é a resolução real do projetor da banca e do notebook de quem apresenta?"

## 10. Repita o padrão da fatia 1b

Mesma estrutura de pasta, mesmos hooks, mesmo ciclo de cada passo (teste que falha, implementação, teste passa, lint e `format:check`, `PROGRESSO.md` com a versão do contrato, commit e push) e a mesma conferência visual no fim, agora para **todas** as telas.
