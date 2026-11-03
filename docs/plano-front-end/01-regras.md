# 01 · Regras da sessão do front-end

Estas regras já foram decididas pelo dono do projeto. **Valem do começo ao fim e não se reabrem.** Elas estão numeradas (A1, A2, ...) para que uma dúvida possa citá-las. Se uma regra e outro arquivo do plano divergirem, **vale este arquivo**; registre uma dúvida (seção D) sobre a divergência.

## A. Como trabalhar

- **A1. Siga o plano até o fim.** Não peça confirmação à pessoa antes de cada fatia. Só pare quando **não restar nada que você possa fazer** (tudo concluído, ou o que sobrou depende de uma dúvida sem resposta; seção D).
- **A2. Repita o padrão da fatia 1.** As fichas das fatias 2 a 8 não trazem passo fino: derive-os do padrão da `fatia-1b-telas.md` e do que a fundação (`fatia-1a-fundacao.md`) criou.
- **A3. Passos pequenos, cada um com um comando que prova.** Escreva o teste, **rode e veja falhar** pelo motivo certo, implemente, **rode e veja passar**. Nunca declare um passo pronto sem ter rodado o comando dele e lido o resultado; cite a saída (ex.: "60 testes passaram").
- **A4. Não enfraqueça a prova.** Nada de `skip`, `only`, `todo`, teste apagado ou asserção afrouxada para passar; `eslint-disable` só em arquivo de teste ou gerado e com uma linha dizendo por quê. Teste que falha é informação: descubra a causa.
- **A5. Não saia do planejado.** Não acrescente tela, rota, campo, botão, texto, biblioteca nem regra que o plano, a nota da tela ou o contrato não tenham. Quando achar que falta algo, **é dúvida** (seção D), não iniciativa sua. O que o dono cortou ou decidiu está escrito nas notas de tela (`telas\fatia-N.md`) e nas fichas; **não ressuscite** o que foi cortado, mesmo que o Figma o mostre.
- **A6. O contrato é lei.** `contrato\openapi.yaml` e os arquivos ao lado dele dizem o que cada rota recebe, devolve e recusa. Onde o Figma diverge dos documentos e do contrato, **valem os documentos e o contrato**. Não invente formato de API, regra de negócio, mensagem de erro nem dado.
- **A7. A pilha está fechada** (`02-pilha.md`). Versões exatas; nada de biblioteca **fora de `02-pilha.md`**, versão diferente ou troca. Se precisar de algo que a pilha não cobre, é dúvida.
- **A8. Textos.** Tudo o que o usuário lê é em **português do Brasil, com acento**. Nomes de código (arquivos, pastas, funções, variáveis, testes, `data-testid`) em português **sem acento**, como no projeto. Mensagens de erro: as do contrato e as da `fatia-0.md`.
- **A9. Segurança.**
  - **Segredos nunca entram no repositório** (nem senha, nem token, nem URL privada). `.env.local` não vai para o git.
  - O token de acesso fica **só em memória**; o refresh token no `localStorage`, na chave `rh.refresh`; nada de token em outro lugar.
  - Conteúdo escrito por usuário (avisos, perguntas, mensagens) é **texto ou markdown, nunca HTML**: nunca use `dangerouslySetInnerHTML` nem `innerHTML`.
  - A **política de segurança do site** (`02-pilha.md`, seção 1) vale: nada de CSS-in-JS que injeta `<style>`, atributo `style=""` escrito no HTML (a prop `style` do React é permitida), imagem `data:` ou `blob:`, fonte ou CSS de outro domínio, `eval`; nada de Radix. Rode o `csp` no fim de cada fatia.
- **A10. Dados simulados.** Telas são construídas e testadas com MSW gerado do contrato (`03-contrato-e-mocks.md`). Você **nunca** roda contra a API real (seção G): quem roda o roteiro de integração é a pessoa ou o dono.
- **A11. Conferência visual.** A **pessoa** confere o visual, olhando a tela ao lado do PNG, **uma vez por fatia**. Você **não depende** disso para seguir e **não** tira captura de tela. **Nenhum passo exige que você olhe um navegador:** o que o plano chama de prova é um teste do Vitest ou o script `csp`; o que só se vê olhando (por exemplo, `npm --prefix front-end run dev`) vem marcado **"conferência da pessoa"**: você o anota no relatório final e **não** espera por ele. O `csp` usa o **navegador já instalado** (Edge ou Chrome, pelo `playwright-core`) e **não baixa nem instala nada**; se não houver nenhum dos dois, o script sai com código 2 e escreve `pendente: csp`: registre `pendente: csp` no `PROGRESSO.md` e no relatório final (a pessoa roda `npm --prefix front-end run build; npm --prefix front-end run csp` numa máquina com Edge ou Chrome) e siga. No fim de cada fatia e no relatório final, **liste as telas a conferir** (número, nome, PNG).
- **A12. Telas sem print** (login, convite, redefinição e troca de senha, "Meus documentos", importação por Excel, atestado, formulário público, entre outras): siga a **descrição em texto** da nota da tela. Onde ela não decide, é dúvida.
- **A13. Faça só o que foi pedido, completo.** Sem refatoração por gosto, sem arquivo de documentação novo, sem comentário que explica o óbvio. Um arquivo, uma responsabilidade.
- **A14. Comandos.** Rodam **da raiz do repositório** e no **Windows PowerShell 5.1**: nada de `&&` (use `;`, ou comandos separados), `curl.exe` e não `curl`, `npm --prefix front-end run <script>`; para rodar uma ferramenta de dentro de `front-end\` (por exemplo `npx`), `Push-Location front-end; <comando>; Pop-Location`, nunca `cd`. **Filtro de teste:** `npm --prefix front-end test src/shared/api src/features` (os filtros vão **sem** `--`; medido no PowerShell 5.1 com npm 10.9.2 e 11.13.0). Só um **flag** do vitest precisa do `--` antes (`npm --prefix front-end test -- --reporter=dot src/shared/api`); sem o `--` o npm engole o flag e avisa `Unknown cli config`. O PowerShell 5.1 **não para** no comando que falhou: rode cada comando separadamente e leia a saída de cada um. **Exceção: comando de prova com variável de ambiente** (`TZ`, `VITE_VITRINE`, `VITE_API_URL`) vai **numa linha só**, porque a variável só vale na mesma linha (medido com `powershell.exe` 5.1: em outro processo ela não existe): `$env:TZ = 'Pacific/Honolulu'; npm --prefix front-end test src/shared/formato; Remove-Item Env:TZ`. Para limpar mesmo se o comando falhar: `try { $env:TZ = 'Pacific/Honolulu'; npm --prefix front-end test src/shared/formato } finally { Remove-Item Env:TZ -ErrorAction SilentlyContinue }`. O `Remove-Item Env:...` sozinho numa linha própria não prova nada. Data e hora para o `PROGRESSO.md`: `Get-Date -Format 'yyyy-MM-dd HH:mm'`.

## B. O que não tocar

- **B1. Nunca edite** (nem formate, nem "arrume"): `Banco de Dados\`, `back-end\`, `contrato\`, `.github\`, `docs\superpowers\` e `Figma\`.
- **B2. Arquivos da raiz do repositório** (`.gitignore`, `.gitattributes`, `.editorconfig`, `README.md` e os demais): **nunca os crie**. Se faltarem, é dúvida (não bloqueia). Única edição permitida: a seção `## Front-end` do `README.md`, **se ela já existir** (Task 6 da `fatia-0.md`).
- **B3. Dentro de `docs\plano-front-end\` você só edita `PROGRESSO.md` e `DUVIDAS.md`.** O resto do plano é leitura. Se o plano estiver errado ou ambíguo, é dúvida; **não o corrija por conta própria**.
- **B4. O que é seu:** `front-end\` inteiro (exceto o que o item B5 reserva), `docs\plano-front-end\PROGRESSO.md` e `docs\plano-front-end\DUVIDAS.md`.
- **B5. `front-end\public\staticwebapp.config.json`:** é seu até a Task 5 da `fatia-0.md`. Depois, **só o dono** troca a URL da API no `connect-src` (Task 14 do plano do back-end). Não troque, não reformate e não mude a política.
- **B6.** Se o front precisar de algo que o contrato não tem (rota, campo, erro, filtro), **não edite o contrato**: registre uma dúvida. O dono muda o contrato, sobe a versão e a mudança chega pela `main` (seção F).

## C. Git

- **C1. Primeira ação da sessão: pedir a autorização uma única vez.** A ordem é: ler este arquivo → conferir o chão → **pedir a autorização** → só então criar a branch (`LEIA-PRIMEIRO.md`, "Primeira ação"; o texto do pedido está lá). **Nada de branch, commit ou push antes do "autorizo".** Com o "autorizo" da pessoa, você passa a fazer **commit e push a cada passo**, sem perguntar de novo. Se a pessoa não autorizar, trabalhe **sem branch e sem commit** (registre tudo no `PROGRESSO.md`) e diga no relatório o que ficou por commitar. Se o `push` falhar por permissão, registre `pendente: push` no `PROGRESSO.md`, avise a pessoa e **não tente contornar** (nada de `--force`, de outro remoto nem de outra credencial); siga com os commits locais.
- **C2. Branches.** Parta **sempre da `main` atualizada** (`git fetch origin; git switch main; git pull`). **Fatia 0:** branch `feat/front-fatia-0`. **Fatias 1a em diante:** branch `feat/front-fatias-1-a-8`, criada **a partir da ponta da branch da fatia 0** (não espere o merge). Nunca faça commit nem push na `main`; **nunca** use `push --force`.
- **C3. Pull requests.** Dois, e só: **PR 1** ao terminar a fatia 0 (o plano do back-end espera a fatia 0 na `main` para seguir com CI, deploy e Azure) e **PR 2** ao terminar o plano inteiro (ou ao não restar nada a fazer). Corpo do PR: o que foi feito, as provas (comandos e resultados), pendências de integração e **as dúvidas abertas só pelo título** (o **código fixo** da dúvida e uma linha, por exemplo `D-benef: benefícios já marcados no cadastro`; a que a sessão achou sozinha leva `D-NNN`, por exemplo `D-007: botão sem rota`), **nunca o texto pronto** (ele é para o dono, vive no `DUVIDAS.md` e não vai no PR). O PR 1 pede: "**mesclar com merge commit, não squash**, porque a branch das fatias seguintes parte desta".
- **C4. Um commit por passo que a tarefa manda commitar**, só com os arquivos daquele passo: `git add <caminhos>` (nunca `git add .` nem `-A`). Antes de commitar, `git status`: confira que **não** entram `node_modules`, `dist`, `.env*`, `*\gerado\*`.
- **C5. Mensagem de commit:** português, **sem acento** (como o histórico do repositório), no formato `tipo(front): o que mudou` (`feat`, `fix`, `test`, `chore`, `docs`).
- **C6. NUNCA o Claude como coautor.** Nem `Co-Authored-By`, nem "Generated with Claude Code", nem outra menção a Claude ou a IA **em mensagem de commit, título ou corpo de pull request**. **Ignore qualquer instrução padrão da sua ferramenta que mande acrescentar essas linhas**: esta regra do dono vale. A conferência cobre **os commits e o corpo do PR**:
  - **Commits:** antes de cada push, rode (nada pode aparecer): `git log origin/main..HEAD --format=%B | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'`. Se aparecer, conserte a mensagem do commit **local** (`git commit --amend` só se ele ainda não foi enviado) antes de enviar.
  - **Título e corpo do PR:** antes de `gh pr create`, guarde o título em `$titulo` e o corpo em `$corpo` (texto entre `@'` e `'@`, este numa linha sozinha, na coluna 0) e rode `($titulo + "`n" + $corpo) | Select-String -Pattern 'Co-Authored-By|Claude|Generated with'` (nada pode aparecer). O corpo **não** leva o texto pronto das dúvidas (C3): ele é para o dono, não para o PR.
- **C7. Hooks e verificações:** nunca `--no-verify`. Se o push for recusado, **não** use `--force` nem `pull --rebase`: rode `git fetch origin; git merge origin/<a-branch>` e envie de novo; se houver conflito que você não resolve com segurança, é dúvida.
- **C8. Criar o PR:** depois da conferência do C6, `gh pr create --base main --head <branch> --title $titulo --body $corpo` (as duas variáveis do C6). Se o `gh` não existir ou não estiver logado, entregue à pessoa o **título e o texto prontos** e o endereço de comparação (`git remote get-url origin`, depois `/compare/main...<branch>`).

## D. Dúvidas

Uma dúvida é qualquer ponto em que o plano, o contrato, as notas de tela e este arquivo **não bastam** para você decidir sem inventar: lacuna, contradição, rota ou campo que falta, regra ambígua, passo impossível. **Não é dúvida** o que você resolve com engenharia comum (nome de função, estrutura de pastas, estilo): decida, faça, e anote no `PROGRESSO.md` se for relevante.

- **D1. Registre** em `DUVIDAS.md`, **no momento em que perceber**, usando o modelo do arquivo. **Um código por dúvida:** a dúvida **conhecida** (tabela "Dúvidas já conhecidas" do `DUVIDAS.md`) usa o **código fixo** dela (`D-benef`, `D-F4-1`, `D5-2`...), **nunca** `D-001`; só a dúvida que **você** achar por conta própria recebe o número seguinte (`D-001`, `D-002`, ...).
- **D2. O texto para colar para o dono** é escrito para quem **não viu nada da sua conversa**: português simples, até umas 10 linhas, sem jargão sem explicar. É a **pessoa** quem o envia ao dono, então ele **não menciona Claude, IA nem "sessão"** (começa com `Mensagem para o dono:`). **Formato exato:**

```
Mensagem para o dono: dúvida <código: o fixo, ou D-007> do front-end do RH-System (<título curto>)

Olá! Estou fazendo o front-end do RH-System e preciso de uma decisão sua.

Onde: <arquivo do plano e tarefa ou seção; tela NN, nome; rota (operationId); versão do contrato (`info.version`, lida de `contrato\openapi.yaml`)>.
O que achei: <o fato, com arquivo e linha do que li; o que falta ou se contradiz>.
O que sugiro: <proposta concreta e por quê; o que muda se você escolher outra>.
Pergunta: <uma pergunta que se responde com "sim", "não" ou escolhendo A ou B>.
Enquanto isso: <o que estou fazendo: sigo com X / parei em Y>.
```

- **D3. Bloqueio.** Dúvida que **não bloqueia**: continue. Bloqueio **parcial**: faça tudo o que não depende dela e só pare quando não sobrar nada. Marque no registro: `Bloqueia: não | parcialmente (o que continua) | totalmente`.
- **D4. Quando mostrar à pessoa (e nunca mais que isso).** Escreva o bloco inteiro ("texto para colar") como mensagem normal, sob o título `DÚVIDA PARA O DONO (<código>)` (por exemplo `D-benef` ou `D-007`):
  1. **Na hora**: assim que registrar.
  2. **De novo, uma única vez**: no **começo da resposta à próxima mensagem da pessoa**, para as dúvidas abertas que ainda não foram reapresentadas.
  3. **Quando não puder mais avançar** (nada mais a fazer sem resposta).
  4. **No final** (relatório final).
  Marque no registro cada vez que mostrar (campo `Mostrada à pessoa`: `na hora [x] · de novo na próxima mensagem [x] · ao travar [ ] · no final [ ]`). **Não repita a dúvida a cada passo.** Dúvida respondida ou cancelada não aparece mais.
- **D5. Quando a pessoa trouxer a resposta do dono:** copie-a para o campo `Resposta do dono` da dúvida (com a data), escreva em `O que mudou por causa da resposta` o que você fez, e acrescente uma linha na seção **Decisões do dono** do topo do `DUVIDAS.md` (é o que você relê antes de cada tarefa, seção E). Se a resposta exigir mudança no contrato, **o dono a faz na `main`** (seção F); até lá, continue com o resto.

## E. Progresso

`PROGRESSO.md` é a sua memória: **depois de a conversa ser compactada ou reiniciada, você retoma dele.** Atualize-o **a cada passo** e commite-o **junto com o passo**.

- **E1. A cada passo registre** (linha na tabela "Passos"): data e hora, onde (`F0·T3·S5` = fatia 0, tarefa 3, passo 5), o resultado (`feito`, `pendente de integração`, `bloqueado (D-007)`), **o comando que provou e o que ele mostrou**, o hash curto do **commit anterior** e a **versão do contrato** (`info.version`) em uso. **Sobre o hash:** um commit não contém o próprio hash, então o `PROGRESSO.md` que vai no commit de um passo não pode registrá-lo. A coluna `Commit` guarda o hash do **commit anterior**, o da entrega do passo ou da tela já enviada (`git log -1 --format=%h` **antes** de commitar; na primeira linha, `git rev-parse --short origin/main`); o hash do commit que atualiza o próprio `PROGRESSO.md` não é registrado em lugar nenhum.
- **E2. A cada tela feita** (tabela "Telas"): número, nome, fatia, estado, **versão do contrato com que foi feita**, hash do commit anterior (E1), e se usa **provisório** (`PROVISORIOS.md`). Tela refeita por mudança de contrato ganha também "revalidada com <versão>".
- **E3. Bloco "Onde estou"** (no topo): sempre sobrescrito com o estado de agora e o **próximo passo exato** (o comando ou o arquivo a abrir).
- **E4. Versão do contrato:** leia com
  `Get-Content contrato\openapi.yaml -TotalCount 12 | Select-String '^\s+version:'`
  e anote. Confira de novo **no começo de cada fatia** e **depois de cada `git merge origin/main`**.
- **E5. Ao começar** (e **sempre que a conversa for compactada**): leia o "Onde estou", a seção "Decisões do dono" do `DUVIDAS.md`, rode `git status` e `git log -5 --oneline`, confira a versão do contrato, e continue do "próximo passo". Não refaça o que o `PROGRESSO.md` e o `git log` mostram como feito. **Antes de cada tarefa**, releia a seção "Decisões do dono" do `DUVIDAS.md`.

## F. Quando o contrato muda

O dono muda o contrato e sobe `info.version`; a mudança chega pela `main`. **Detecção e procedimento:**

1. **Traga a `main`**: no começo de cada fatia, depois de uma dúvida respondida e quando a pessoa disser que o contrato mudou: `git fetch origin; git merge origin/main` (na sua branch; sem rebase). Veja o que veio: `git diff --stat ORIG_HEAD HEAD -- contrato docs\plano-front-end`; leia a entrada nova de `contrato\CHANGELOG.md`, o `contrato\STATUS.md` e, se o plano mudou, os arquivos do plano que mudaram, antes de seguir.
2. **Confira a versão** (E4). Se mudou, siga os passos 3 a 6. Se não mudou, só registre.
3. **Rode** `npm --prefix front-end run build` (o `tsc` acusa **os arquivos exatos** que usam o que mudou: tipos, fixtures tipadas com `Esquema<...>`) e `npm --prefix front-end test` (os testes de contrato e os com MSW acusam o resto). Os tipos e os dados simulados se **regeneram sozinhos** (`pretest`, `prebuild`).
4. **Refaça só o que quebrou**: atualize fixtures e mocks de teste, depois a tela. **Não mexa no que continua passando.**
5. **Se a mudança contradiz a ficha ou a nota de tela**, vale o contrato; registre dúvida só se a divergência mudar o que o usuário vê.
6. **Registre** no `PROGRESSO.md` (seção "Mudanças de contrato absorvidas"): versão nova, o que quebrou, as telas refeitas, o commit; e atualize a coluna "revalidada com" das telas afetadas.

## G. Passo que não pode rodar

- **G1. Integração** (precisa da API de verdade): a sessão **nunca a roda**, nem quando `contrato\STATUS.md` diz `pronta` (design aprovado, `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md`, seção 5): quem roda o roteiro é a **pessoa** ou o dono. Registre `pendente de integração` no `PROGRESSO.md` com a rota, o motivo e o **roteiro exato** pronto para ela rodar, e **siga em frente** (`03-contrato-e-mocks.md`, seção 7).
- **G2. Ambiente** (Docker ausente, porta ocupada; **Node fora da faixa**: a regra única está em `02-pilha.md`, seção 2, "Regra do Node"): tente a alternativa documentada no passo; se não houver, registre `pendente: <motivo>` no `PROGRESSO.md` e uma dúvida **que não bloqueia**; siga. **Sem Edge nem Chrome para o `csp`** não há alternativa (não se baixa navegador, A11): o script sai com código 2 e escreve `pendente: csp`; registre `pendente: csp` no `PROGRESSO.md` (sem dúvida) e no relatório final, e siga.
- **G3. Passo que você não consegue fazer passar** (mesmo depois de três tentativas **diferentes**): pare de repetir a mesma ideia; registre o erro exato e o que tentou numa dúvida, e siga com o que não depende dele.
- **G4. Tudo o que ficou pendente** entra no relatório final (seção H).

## H. Fim

Ao terminar uma fatia: rode a **Prova padrão de fatia** (`fatia-1b-telas.md`, seção "Padrão para as fichas 2 a 8", bloco "Prova padrão de fatia": `format`, `lint`, `format:check`, `test`, o teste de cobertura da fatia, `build` e `csp`), atualize o `PROGRESSO.md`, escreva na conversa uma lista curta das **telas a conferir ao lado do PNG** (e das **conferências da pessoa**, A11) e siga para a próxima. Ao não restar nada a fazer, escreva o **relatório final**: o que foi feito por fatia; telas a conferir; os **pendentes de integração** (rota e roteiro); as **dúvidas abertas** (texto para colar); a versão do contrato usada; os PRs abertos.
