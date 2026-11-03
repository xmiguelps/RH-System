# LEIA PRIMEIRO: plano do front-end do RH-System

Este é o **arquivo de entrada** do plano do front-end. Ele foi escrito para uma **sessão de IA que não tem nenhum outro contexto**: tudo o que você precisa saber está neste repositório. Leia este arquivo inteiro, **faça a primeira ação** (abaixo) e siga a ordem de leitura.

## O que é o projeto

O **RH-System** é um sistema de recursos humanos web (um TCC): o RH, os gestores e os funcionários usam um portal para ponto, férias, holerites, avisos, formulários, treinamentos e gestão de colaboradores. O **back-end** (.NET 10, banco MySQL) é de outra pessoa, "o dono". O **front-end** (React, TypeScript, pasta `front-end\`) é o que **você** faz, com a pessoa do grupo que abriu esta sessão. O que une as duas partes é o **contrato da API** (`contrato\openapi.yaml`, 224 operações, já escrito): **você nunca espera o back-end**, porque trabalha com dados simulados (MSW) gerados do contrato. As telas estão desenhadas em 61 imagens (`Figma\telas\`); onde o desenho diverge dos documentos e do contrato, valem os documentos e o contrato.

## O que dizer à sessão (a pessoa cola isto)

> Você vai fazer o front-end do RH-System. Leia `docs\plano-front-end\LEIA-PRIMEIRO.md` e siga o plano até o fim, sem pedir confirmação a cada fatia. Dúvidas vão para `docs\plano-front-end\DUVIDAS.md`, com o texto pronto para eu mandar ao dono.

## Antes de começar (para a pessoa)

O repositório está clonado e atualizado na `main` (`git pull`), e a `main` **já tem** `docs\plano-front-end\` **completo**, `contrato\openapi.yaml` e `Figma\telas\` (61 PNG); o Git consegue dar `push` ao GitHub com a sua conta; Node instalado na faixa da regra abaixo (`node -v`); Edge ou Chrome instalados (o teste da política de segurança usa um deles: ele **não baixa** navegador). O `gh` (GitHub CLI) é opcional. A sessão **confere isto** na primeira ação (abaixo); o que faltar ela **não tolera**.

**Regra do Node** (o mesmo texto está em `02-pilha.md`, seção 2, e em `fatia-0.md`, Task 1, passo 1; vale para a sessão toda). Aceite `^22.22.2 || ^24.15.0` (`node -v`): a pilha foi medida com a 22.22.2 ou mais nova da série 22 e com a 24.15.0 ou mais nova da série 24 (o plano foi medido na 24.17.0). Versão **26 ou mais nova**: siga, mas registre `Node 26: não medido` no `PROGRESSO.md`; se alguma prova falhar **só** nela, peça à pessoa que instale o **Node 24 LTS** (nodejs.org, página de versões anteriores) e registre `pendente: Node`. **Depois de instalar:** feche e reabra o terminal, confira que `node -v` escreve `v24.15.0` ou mais nova, rode `Remove-Item -Recurse -Force front-end\node_modules; npm --prefix front-end ci`, refaça a prova que falhou e troque `pendente: Node` por `feito` no `PROGRESSO.md`. Versão abaixo da faixa (22 anterior à 22.22.2, 24 anterior à 24.15.0, 23, 25 ou 20 e mais antigas): peça a mesma instalação (Node 24 LTS), registre `pendente: Node` e não siga sem ela. **Escopo:** esta regra é do front. O plano do back-end (`docs\superpowers\plans\2026-10-21-fatia-0-esqueleto.md`, Task 1, passo 2) **fixa o Node 24** (`^24.15.0`) e o CI usa `node-version: 24`; o Node 24 LTS atende os dois planos, e as duas regras não se contradizem (a do back-end é mais estreita; a do front aceita também a série 22 e deixa a 26 seguir, registrada).

## Primeira ação (antes de ler o resto)

A ordem é esta, e **nada de branch, commit ou push antes do passo 3 (o "autorizo")**: (1) ler `01-regras.md`; (2) conferir o chão; (3) pedir a autorização; (4) criar a branch; (5) criar o `PROGRESSO.md` e o `DUVIDAS.md` de trabalho; (6) ler o resto.

1. **Leia `docs\plano-front-end\01-regras.md` inteiro** (as regras valem do começo ao fim; a seção C explica por que a autorização vem antes da branch).
2. **Confira o chão** (os pré-requisitos). Da raiz do repositório, no PowerShell:

   ```powershell
   node -v; git fetch origin; git status; git branch --show-current
   Test-Path contrato\openapi.yaml
   (Get-ChildItem Figma\telas\*.png).Count
   $daRaiz = @('docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md', 'contrato\CONVENCOES.md', 'contrato\ELENCO.md', 'contrato\ROTAS.md', 'contrato\PROVISORIOS.md', 'contrato\STATUS.md')
   $daRaiz | Where-Object { -not (Test-Path $_) }
   $esperados = @('LEIA-PRIMEIRO.md', '01-regras.md', '02-pilha.md', '03-contrato-e-mocks.md', '04-visual.md', '05-caminhos-de-tela.md', 'fatia-0.md', 'fatia-1a-fundacao.md', 'fatia-1b-telas.md', 'PROGRESSO.md', 'DUVIDAS.md', 'ferramentas\gerar-mocks.mjs', 'ferramentas\servidor-de-teste.mjs', 'ferramentas\violacoes-de-politica.mjs') + (2..8 | ForEach-Object { "fichas\fatia-$_.md" }) + (0..8 | ForEach-Object { "telas\fatia-$_.md" })
   $esperados | Where-Object { -not (Test-Path "docs\plano-front-end\$_") }
   ```

   Esperado: `node -v` na faixa da regra do Node (acima); `Test-Path` escreve `True`; a contagem de PNG escreve `61`; **as duas últimas consultas não escrevem nada** (os seis arquivos da raiz, que a ordem de leitura exige, e o plano completo existem). **Se faltar algo, não tolere:** registre uma dúvida que **bloqueia aquela parte** (`01-regras.md`, D3) e avise a pessoa; **pare só aquela parte** e siga com o que não depende do que falta. Faltando `contrato\openapi.yaml`, `01-regras.md` ou `fatia-0.md`, **nada** pode seguir: pare e avise. Faltando o design, um arquivo do contrato, uma ficha, uma nota ou um PNG, a dúvida bloqueia só aquela parte (aquela fatia ou aquela tela).
3. **Peça a autorização de Git, uma única vez, com estas palavras** (e **espere a resposta**: é o único ponto em que você espera a pessoa):

   > Para seguir o plano sem parar a cada passo, preciso da sua autorização, **uma vez**: posso (1) criar a branch `feat/front-fatia-0` a partir da `main`, (2) fazer `git commit` e `git push` dessa branch a cada passo, e (3) abrir os pull requests no GitHub quando o plano mandar? Nunca vou colocar o Claude como coautor, nem em commit nem em pull request, nunca vou mexer na `main` e nunca vou forçar um push. Responda "autorizo" ou diga o que mudar.

   Se a pessoa **não** autorizar, trabalhe **sem branch e sem commit** (`01-regras.md`, C1): pule o passo 4 e o commit do passo 5.
4. **Crie a branch** (**só depois do "autorizo"**): `git fetch origin; git switch main; git pull; git switch -c feat/front-fatia-0`. **Confira que ela parte da `main`:** `git merge-base --is-ancestor origin/main HEAD; $LASTEXITCODE` deve escrever `0`; senão, volte à `main`, `git pull` e crie a branch de novo.
5. **Crie o `PROGRESSO.md` e o `DUVIDAS.md` de trabalho** a partir dos modelos que já estão em `docs\plano-front-end\` (edite os próprios arquivos: apague os blocos marcados **EXEMPLO**, **mas não** as linhas de "Decisões do dono" do `DUVIDAS.md` (são decisões reais do dono, de 2026-11-02); preencha o cabeçalho do `PROGRESSO.md`: data, branch, autorização, versão do contrato, versão do Node). Leia a versão do contrato com `Get-Content contrato\openapi.yaml -TotalCount 12 | Select-String '^\s+version:'`. Commit e push (só com o "autorizo"): `git add docs\plano-front-end\PROGRESSO.md docs\plano-front-end\DUVIDAS.md; git commit -m "docs(front): inicia PROGRESSO e DUVIDAS do front-end"; git push -u origin feat/front-fatia-0`. **Se o `push` falhar por permissão**, registre `pendente: push` no `PROGRESSO.md`, avise a pessoa e **não tente contornar** (nada de `--force`, de outro remoto nem de outra credencial); siga com os commits locais (`01-regras.md`, C1).
6. Só então leia o resto, na ordem abaixo.

## Ordem de leitura

**Leia agora, nesta ordem:**

1. `docs\plano-front-end\01-regras.md`: as regras (como trabalhar, o que não tocar, Git, dúvidas, progresso, contrato que muda, passo que não roda). **Valem do começo ao fim.** Você já o leu no passo 1 da primeira ação; volte à seção de que precisar.
2. `docs\plano-front-end\02-pilha.md`: a pilha, com versões exatas e configurações medidas. Não se reabre.
3. `docs\plano-front-end\03-contrato-e-mocks.md`: como usar o contrato, os tipos, o cliente, os dados simulados e o teste de contrato.
4. `docs\plano-front-end\04-visual.md`: cores, medidas, fontes, ícones e componentes-base, tirados dos PNG.
5. `docs\plano-front-end\05-caminhos-de-tela.md`: a **tabela única** de caminhos (endereços) de **todas** as telas. Vale sobre qualquer caminho que uma nota de tela ou a fundação proponha.
6. `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md`: o design aprovado deste plano. Leia as **seções 5** (plano do front), **6** (tecnologia), **7** (decisões do dono sobre as telas) e **8** (restrições). O resto é do back-end.
7. Os arquivos do contrato: `contrato\CONVENCOES.md` (leia **inteiro**), `contrato\ELENCO.md`, `contrato\ROTAS.md` (é o índice: uma linha por rota), `contrato\PROVISORIOS.md` e `contrato\STATUS.md`. **Não leia** `contrato\openapi.yaml` inteiro (1,3 MB): procure a operação pelo `operationId`. `contrato\CHANGELOG.md`: quando a versão mudar.

**Leia quando o plano mandar ou quando precisar:**

- **Só se lê o que a ficha ou a tarefa mandar.** `docs\superpowers\specs\2026-10-21-rh-system-design.md` (spec do produto) e `docs\superpowers\plans\2026-10-28-roadmap-completo.md` (fatias 0 a 8, decisões que atravessam fatias) **têm trechos desatualizados** (prazo e lembretes de treinamento, as duas caixas da tela 20, os e-mails): onde divergirem, **vale o contrato** (`contrato\CONVENCOES.md`, seções 12 e 13) **e o design aprovado do plano, seção 7** (item 6 acima). Não "corrija" o contrato por causa deles: se achar divergência que muda o que a pessoa vê, é dúvida.
- `docs\superpowers\plans\2026-10-21-fatia-0-esqueleto.md`: plano da fatia 0 **do back-end**. As Tasks 10 e 11 (front) **saíram de lá e estão em `fatia-0.md`**; o resto não é seu.
- `Figma\telas\NN-perfil-tela.png`: 61 imagens de 1440 px (numeração 0 a 60: **RH 0 a 31, funcionário 32 a 45, gestor 46 a 60**). Abra com a ferramenta **Read** (ela mostra a imagem). Não precisa de acesso ao Figma.
- `docs\retomada-da-conversa.md`: decisões e regras de trabalho do dono; **consulte por palavra-chave** se uma nota de tela remeter a ela.

**Não precisa ler** (é do back-end e do banco; o contrato já traz o que o front precisa): `Banco de Dados\`, `docs\pedidos-para-o-kauan.md`, `back-end\`.

## O que fazer em cada fase

Cada fatia segue o mesmo ritmo: **leia o arquivo da fase, execute as tarefas na ordem, rode as provas, atualize o `PROGRESSO.md`**. No **começo de cada fatia**, traga a `main` (`git fetch origin; git merge origin/main`) e confira a versão do contrato (`01-regras.md`, seção F).

| Fase | O que ler | O que fazer |
|---|---|---|
| **Fatia 0** | `fatia-0.md` | Criar o projeto `front-end\` com a pilha; gerar tipos, dados simulados e exemplos do contrato; cliente de API; tela de entrar, `AuthProvider` (refresh serializado entre abas) e shell; política de segurança (e a sonda de download) provadas por um navegador sem janela. **Seis tarefas.** No fim: **PR 1** e a branch `feat/front-fatias-1-a-8`. A integração ponta a ponta com a API real fica **pendente de integração** (quem a roda é a pessoa ou o dono, nunca a sessão: `01-regras.md`, G1). |
| **Fatia 1a (fundação)** | `fatia-1a-fundacao.md` | A base que todas as telas usam: navegação e menu por perfil, componentes-base, formulários, formatações, e as três telas de ponta a ponta (lista, formulário, detalhe) com o build passando sob a política. |
| **Fatia 1b (telas)** | `fatia-1b-telas.md` e `telas\fatia-1.md` | As telas da fatia 1 (colaboradores e base transversal). **É o padrão que as fatias 2 a 8 repetem.** |
| **Fatias 2 a 8** | `fichas\fatia-N.md` e `telas\fatia-N.md` | A ficha dá o objetivo, as telas, as rotas do contrato, os cortes, a ordem e a definição de pronto; **não** traz passo fino: derive os passos do padrão da 1b e da fundação. Cada tela tem uma **nota** (o que fica, o que sai, rotas, estados, textos). |

O mapa dos arquivos do plano (**todos** existem: a primeira ação conferiu):

```
docs\plano-front-end\
  LEIA-PRIMEIRO.md          este arquivo
  01-regras.md              regras
  02-pilha.md               pilha e configurações
  03-contrato-e-mocks.md    contrato, tipos, cliente, dados simulados
  04-visual.md              cores, medidas, fontes, ícones, componentes-base
  05-caminhos-de-tela.md    a tabela única de caminhos de todas as telas
  fatia-0.md                fatia 0 (passo a passo)
  fatia-1a-fundacao.md      fundação (passo a passo)
  fatia-1b-telas.md         telas da fatia 1 (passo a passo)
  fichas\fatia-2.md ... fatia-8.md
  telas\fatia-0.md ... fatia-8.md    uma nota por tela
  ferramentas\              arquivos que o plano manda copiar para front-end\scripts\
  PROGRESSO.md              sua memória (você atualiza)
  DUVIDAS.md                suas dúvidas para o dono (você atualiza)
```

## Em uma frase cada: o que mais importa

- **Siga até o fim**, sem pedir confirmação a cada fatia; **passos pequenos, cada um com um comando que prova**.
- **Não toque** em `Banco de Dados\`, `back-end\`, `contrato\`, `.github\`, `docs\superpowers\` nem em arquivos da raiz; **nunca crie** arquivos da raiz.
- **O contrato é lei**: falta algo? Não edite: registre uma **dúvida** com o texto pronto para o dono.
- **`PROGRESSO.md` a cada passo**, com a versão do contrato; é dele que você retoma depois de a conversa ser compactada.
- **Git**: **só depois do "autorizo"** (nada de branch, commit ou push antes), commit e push a cada passo, na sua branch, **sem o Claude como coautor** (nem nos commits, nem no título e no corpo do PR).
- **Você não olha navegador nenhum**: prova é teste do Vitest ou o script `csp`; o que só se vê olhando está marcado **"conferência da pessoa"** e vai no relatório final, sem bloquear (`01-regras.md`, A11). O `csp` usa o Edge ou o Chrome já instalados; sem eles, `pendente: csp`.
- **O visual** quem confere é a pessoa, ao lado do PNG, uma vez por fatia; você lista as telas a conferir e **não depende** disso.

## Como terminar

Quando não restar nada que você possa fazer, escreva o **relatório final** (`01-regras.md`, seção H): o que foi feito por fatia, as telas a conferir, as **conferências da pessoa**, os **pendentes de integração** (e `pendente: csp`, se houve), as **dúvidas abertas** (texto para colar), a versão do contrato usada e os PRs abertos.
