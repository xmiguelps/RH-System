# Planos do back-end e do front-end: design

Data: 2026-10-30 · Status: **aprovado pelo dono em 2026-10-30** · Spec do produto (aprovado em 2026-10-29): `docs/superpowers/specs/2026-10-21-rh-system-design.md` · Roadmap: `docs/superpowers/plans/2026-10-28-roadmap-completo.md` · Plano da fatia 0: `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md`

Este documento **não é o plano**. Ele diz o que os planos precisam ser, para quem e por quê, e o que o dono já decidiu. Quem for escrever os planos parte daqui.

Convenção: o que o dono disse está escrito como decisão. O que é **escolha minha** (da sessão que escreveu) está marcado como **[minha escolha]** e deve ser revisto na leitura.

## 1. Intenção e por quê

O RH-System é um TCC (.NET 10, EF Core 9 com Pomelo, MySQL 8.4 com scripts SQL versionados, React com TypeScript, Azure). O dono faz o **back-end**. O **front-end será feito por outra pessoa do grupo, que também usa o Claude Code**, e que **não terá acesso à conversa que gerou os documentos**. Por isso o dono pediu, em 2026-10-29, dois planos separados e a fatia 0 **não** começa antes de eles ficarem prontos.

O leitor principal do plano do front-end é **a sessão do Claude Code dessa pessoa**, não a pessoa. Ela vai dizer à sessão "leia este arquivo e siga o plano", e a sessão precisa **chegar ao fim sem se perder, sem sair do planejado e sem depender do dono**.

## 2. Objetivos, não-objetivos e anti-objetivos

**Objetivos**
- Um plano do back-end e um plano do front-end, em arquivos separados, cada um seguível sem perguntar nada ao dono para o que já foi decidido.
- O front-end **nunca espera o back-end**: o contrato de todas as rotas existe antes de o front começar, e os dados simulados (MSW) seguem esse contrato.
- A sessão do front sabe, a cada momento, o que fazer em seguida, como provar que está pronto e o que fazer quando travar.

**Não-objetivos** (decididos)
- Começar a fatia 0 ou escolher branch/PR e método de execução: ficam para **depois do planejamento** (dono, 2026-10-29).
- Reproduzir o Figma ao pé da letra: ele é referência do visual; onde diverge dos documentos, **valem os documentos**.
- Passo a passo completo do back-end além das fatias 0 e 1.

**Anti-objetivos** (inferidos da conversa; o dono respondeu que o plano "está bom" quando perguntei por critérios de fracasso, então **confirmar na leitura**)
- A sessão do front inventar um formato de API, uma regra de negócio ou uma tela que foi cortada.
- A sessão do front parar de vez por uma dúvida que dava para registrar e contornar.
- Uma dúvida real ficar escondida: o dono precisa receber o texto pronto, mesmo se a pessoa do front estiver longe do computador.
- O contrato mudar sem que o front perceba.
- Um plano que só funciona se tudo der certo (sem regra para o caso de travar).

## 3. O que será produzido

Ordem de trabalho (**[minha escolha]** na ordem entre contrato e planos; o resto é do dono):

1. Este design aprovado.
2. **Escolha das bibliotecas do front** (seção 6), com conferência de versões e um teste descartável do build sob a política de segurança.
3. **Contrato de todas as rotas, das fatias 0 a 8** (seção 5), em `contrato/` (feito em 2026-10-30: 224 operações; convenções, elenco e cenário de demonstração também ali). **Mais os scripts SQL novos** (`V030` em diante: colunas novas e tabelas da aplicação que o contrato exige), **todos prontos antes de o back-end começar** (dono, 2026-10-30), escritos pelo lado da aplicação como `V006` a `V029` e conferidos depois pelo Kauan.
4. **Plano do back-end** e **plano do front-end** (seções 4 e 5), pelo `superpowers:writing-plans`.
5. Os dois planos e o contrato passam pelo `revisor-de-plano` antes de qualquer execução.
6. **Entrega** (dono, 2026-10-30), a pedido do dono: commit do planejamento, do contrato e das telas (`Figma/telas/`) em `docs/planejamento`, PR para a `main` e merge. **O back-end e o front só começam a executar depois de o contrato e os planos estarem na `main`** (dono, 2026-10-30: depois do contrato só faltam os dois planos e a execução dos dois lados), e a branch da pessoa do front parte da `main`. Se ela ainda não tiver permissão de escrita no repositório, o dono resolve antes.
7. Só depois: branch/PR da fatia 0 do back-end e método de execução.

Onde ficam os arquivos **[minha escolha]**: contrato em `contrato/` (a pasta **ainda não existe**: o plano da fatia 0 só descreve `contrato/sessao.exemplo.json`, linha 399, e nada foi criado; hoje a raiz do repositório só tem `.github/CODEOWNERS`, sem `.gitignore`, `.gitattributes`, `.editorconfig` nem README, que a Task 2 criaria); plano do back-end em `docs/superpowers/plans/` (um arquivo, com o passo a passo das fatias 0 e 1 e um roteiro do resto); plano do front-end em `docs/plano-front-end/`, uma pasta com o arquivo de entrada, a ordem de leitura, as regras, as fichas, as notas por tela, `PROGRESSO.md` e `DUVIDAS.md`. O projeto do front-end em si fica em `front-end/`, como no spec (seção 3).

## 4. Plano do back-end

- **Por etapas** (decidido): passo a passo completo só para as fatias 0 e 1; as demais ficam como roteiro (o roadmap já é esse roteiro), e o passo a passo de cada uma é escrito quando ela começar, porque depende de código e de banco que ainda não existem.
- Da fatia 0 ficam os passos 1 a 9 (back-end e banco) e 12 a 14 (CI, deploy, Azure). Os passos **10 e 11 saem** e vão para o plano do front (decidido). **Os passos 12 a 14 só começam quando os passos 10 e 11 do front estiverem na `main`** (dono, 2026-10-30): a CI tem um job do front, o deploy publica `front-end/` e o passo 14 edita `front-end/public/staticwebapp.config.json` e verifica o ambiente pelo shell do front. Se o front atrasar e isso segurar o congelamento da base (que é também o prazo do Kauan), o dono decide na hora entre esperar e fazer ele mesmo os passos 10 e 11.
- Cada fatia nasce **contra o contrato**: o back-end implementa o que o contrato diz, e um teste falha se a API (o documento OpenAPI que ela gera) divergir dele **[minha escolha]**. Mudar o contrato é decisão do dono, registrada (seção 5).
- O plano da fatia 0 precisa ser ajustado: o arquivo `contrato/sessao.exemplo.json` e os testes que o usam (linhas 399 e 494 do plano atual) passam a ser derivados do contrato.
- **Passo 0 [minha escolha]**, antes de o front começar: os arquivos da raiz que a Task 2 cria (`.gitignore`, `.gitattributes` com `eol=lf` e PNG como binário, `.editorconfig`, README) e a renormalização, num commit pequeno só com eles. O plano do front tem a regra de **nunca criá-los**: se faltarem, a sessão registra uma dúvida.
- **Integração e ponta a ponta [minha escolha]:** a integração das telas com a API real e a ferramenta de ponta a ponta (Playwright, roadmap fatia 1) ficam neste plano, com o dono, porque precisam da pilha inteira (API, banco, captador de e-mail na CI). O dono mantém `contrato/STATUS.md`, dizendo quais rotas já existem de verdade.
- Já estão decididas e **não se reabrem**: as armadilhas de MySQL e EF do roadmap (decisões 4, 5 e 12 de "Decisões que atravessam fatias"), a regra de fuso -03:00, as regras de formulário anônimo (mínimo de 3 respostas, nunca mostrar ordem de armazenamento, anonimato só contra usuários da aplicação) e as decisões da seção 7 deste documento.

## 5. Plano do front-end

**Como a sessão trabalha**
- Um **arquivo de entrada** com a ordem de leitura; a pessoa só diz à sessão para lê-lo.
- **Passos pequenos**, cada um com um **comando que prova** que passou (teste, lint, build).
- **Decisões já tomadas escritas como regras**, e uma lista do que **não se toca**: banco, back-end e contrato.
- **`PROGRESSO.md`** atualizado a cada passo, para a sessão retomar depois de a conversa ser compactada ou reiniciada. Ele registra também **com qual versão do contrato** cada tela foi feita **[minha escolha]**.
- A sessão segue **direto até o fim**, sem pedir confirmação à pessoa antes de cada fatia (dono, 2026-10-30).

**Cobertura** (decidido): passo a passo completo para as fatias 0 e 1 (a 1a inclui a fundação do front); para as fatias 2 a 8, uma **ficha** cada, com telas, ordem, onde está o contrato, regras e definição de pronto. A ficha manda repetir o padrão da fatia 1. Os passos finos das fichas dependem do que a fundação cria; a sessão os deriva do padrão.

**Passos 10 e 11 da fatia 0** vão para este plano, feitos com MSW. A verificação ponta a ponta do passo 11 (exige Docker e a API real) vira um **passo de integração** feito quando o back-end da fatia 0 estiver pronto **[minha escolha]**. Os comandos de CI, deploy e Azure ficam com o dono; o plano só diz quais comandos precisam passar.

**"Pronto" no front [minha escolha]:** em cada fatia, testes com MSW seguindo o contrato, lint, `format:check` e o build passando sob a política de segurança. Passo que exige a API real (integração) a sessão **não roda**: registra "pendente de integração" no `PROGRESSO.md`, consulta `contrato/STATUS.md` quando precisar saber se a rota existe, e segue em frente.

**Contrato primeiro, para todas as fatias** (decidido) e **sem o front esperar o back-end**:
- O contrato das fatias 0 a 8 é escrito **durante o planejamento**, a partir das telas, do spec e das tabelas dos scripts `V001` a `V029`: para cada rota, o que se envia, o que volta, os erros e as regras por perfil.
- **Formato [minha escolha]:** um único documento OpenAPI com exemplos, a partir do qual o front gera os tipos e os handlers do MSW. O roadmap (decisão 1b) já apontava que um JSON escrito à mão por rota "não escala para dezenas de endpoints". Isto **muda** o que o dono ouviu na conversa ("um arquivo de exemplo por rota"): muda o como, não o quê.
- **Só o dono muda o contrato.** Se o front precisar de algo que não está lá, a sessão **não edita**: usa o fluxo de dúvidas. Cada mudança entra em um registro de mudanças com número de versão, e os testes do front falham quando o contrato muda, apontando quais telas foram afetadas.
- Itens que dependem de decisão do dono ainda aberta (roadmap, "Decisões do dono" de cada fatia, e spec 13.6) ficam marcados como **provisórios** no contrato **[minha escolha]**. A sessão os constrói normalmente; a lista de provisórios diz quais decisões podem mudá-los.
- O dono não revisa o contrato linha por linha: recebe, por fatia, a lista de rotas com uma linha cada, em português simples, e o `revisor-de-plano` confere o contrato contra o código e os scripts.

**Dúvidas** (decidido, dono 2026-10-30)
- Toda dúvida vai para `DUVIDAS.md`, com o **texto pronto para colar para o dono**: onde no plano, o que a sessão achou, o que sugere e a pergunta exata.
- A sessão mostra a dúvida **na hora**; mostra **de novo uma única vez**, no início da resposta à próxima mensagem da pessoa; e mostra **mais uma vez** quando não puder mais avançar e no final. Nunca mais que isso.
- Dúvida que **não bloqueia**: a sessão continua. Bloqueio **parcial**: faz o resto e só para quando não sobrar nada que dê para fazer.
- Quando o dono responder, a resposta é registrada na própria dúvida e no que a sessão consulta, para não se perder.

**Telas**
- 61 telas de "Alta Complexidade" (RH 0 a 31, funcionário 32 a 45, gestor 46 a 60), exportadas para `Figma/telas/NN-perfil-tela.png` (1440 px de largura, 8,4 MB, sem commit ainda). Para cada uma, o plano traz uma **nota do que fica e do que sai** (seção 7). A pessoa não precisa de acesso ao Figma nem ao MCP dele.
- **Conferência visual** (dono, 2026-10-30): a pessoa confere o visual **olhando a tela ao lado do print, uma vez por fatia**. A sessão lista no relatório final quais telas conferir e **não depende** dessa conferência para seguir; não há captura automática de tela nem navegador extra no front.
- As telas que **não existem no Figma** (roadmap, decisão 14: login, convite, redefinição e troca de senha, "Meus documentos", importação por Excel, atestado, formulário público do candidato) e as do gestor que **reutilizam as do funcionário** precisam de **descrição em texto**, porque não há print.

## 6. Tecnologia do front-end

Já decidida pelo plano da fatia 0: Vite, React, TypeScript, Vitest, Testing Library, ESLint, Prettier, Node na LTS da Task 1, política `default-src 'self'` no Static Web Apps (Task 11).

**Falta escolher** (dono, 2026-10-30: "vamos escolher nós"; a pessoa do front não quer escolher): navegação entre páginas, conjunto de componentes visuais, formulários e validação, busca de dados e cache, geração de tipos a partir do OpenAPI, MSW. **[minha escolha]**, a registrar no plano com a versão conferida. Critérios do roadmap (decisão 1): caber em `default-src 'self'` (CSS-in-JS que injeta `<style>` e fontes externas seriam barrados), **testando o build compilado sob essa política** antes de decidir; máscaras (CPF, telefone), datas no fuso -03:00, rotas por perfil, componente de envio de arquivo; critério de pronto da fundação: menu por perfil, três telas de ponta a ponta (lista, formulário, detalhe) e o build passando sob a política. Preferência inicial: CSS estático, sem CSS-in-JS.

**Cores, medidas, fontes e ícones** (dono, 2026-10-30: "já vai pelo PNG direto"): saem **dos PNG**, não do Figma nem do MCP. As cores vêm de amostragem de pixels e as medidas, dos próprios PNG (1440 px). **[minha escolha]** Fontes e ícones **não dá para ler de pixels**: identifico olhando, marco como **palpite**, e o plano manda usar fontes de licença aberta **guardadas no repositório** (a política `default-src 'self'` barra as externas) e um conjunto de ícones aberto, escolhido junto com o resto das bibliotecas. As escolhas ficam num arquivo do plano do front.

## 7. Decisões do dono sobre as telas (2026-10-30, a consolidar por tela no plano do front)

- **Tela 39** (formulários do funcionário, "Pesquisa de clima"): tirar "O RH vê os resultados agrupados por setor". Resposta anônima não guarda funcionário nem setor (`V017`, linhas 139-140).
- **Tela 03** (ficha, RH): CPF e telefone **completos, sem máscara**. O gestor continua sem ver CPF e vê o telefone mascarado.
- **Tela 29** (Cadastros): **Níveis de acesso fica de fora**; **Gestores fica**, com a chave de aprovação automática (2026-10-29); Cargos, Setores e Benefícios como já decidido; **Usuários do sistema entra**, mínimo: lista com a situação de cada login (convite enviado, senha definida), reenviar convite, botões **"Ativar" e "Desativar"**, sem apagar. Apoio: `USUARIOS.ATIVO` (`Criacao_Tabelas:201`), trigger de inativação (`Criacao_Triggers:121-125`), reativação só com colaborador ativo (`:297-306`).
- **Sem pedir folga** usando o banco de horas. O extrato (telas 33 e 47) mostra só **Crédito e Débito** (`BANCOS_HORAS.TIPO_LANCAMENTO`, `Criacao_Tabelas:396`); "Compensação, folga de meio período" vira um Débito, sem a palavra "compensação".
- **Tela 00**, "Presença agora": tirar a linha "Folga ou compensação". O "Folga" de sábado e domingo nas tabelas de ponto continua.
- **Tela 31** (auditoria): trocar "Ninguém consegue editar nem apagar estes registros" por "Nenhuma tela do sistema edita ou apaga estes registros" (decisão de engenharia; a API não terá rota de edição nem exclusão). Trava no banco fica para a fatia 1a.
- **Esconder** "Lembrar Diego por e-mail" (tela 51) e "Baixar espelho do setor" (tela 54). O espelho de uma pessoa em PDF segue na tela 56.
- **Tela 17** (assistente de formulário): só oferece 5 tipos e não tem Pesquisa nem Sugestão, que o spec prevê: acrescentar.
- Fora das telas: sem unidades; aprovação de férias e de ajuste de ponto em duas etapas (gestor, depois RH); sem quiz, nota, prova final nem turmas presenciais (lista completa em spec 13.3 e no roadmap).

## 8. Restrições

- **Texto da interface em português do Brasil**; mensagens de erro como já escritas no plano da fatia 0 (Task 11).
- **Git.** No trabalho do dono: commit e push só quando ele pedir. Na sessão do front (dono, 2026-10-30): a primeira ação da sessão é pedir **uma vez** a autorização da pessoa; depois disso a sessão faz **commit e push a cada passo**, numa branch própria que parte da `main`, e **abre o PR no final**. Em todos os casos, **Claude nunca aparece como coautor** em commits nem em PRs; a regra vai escrita nas regras do plano do front, porque a sessão dela não lê o `CLAUDE.md` pessoal do dono e o repositório não tem um.
- Arquivos do grupo (scripts do banco, CODEOWNERS) **não se mudam sem pedir** ao dono.
- Os planos precisam funcionar **sem esta conversa e sem o Figma**.
- `Figma/telas/` pesa 8,4 MB e está sem commit; a fatia 0 (Task 2) cria um `.gitattributes` com `eol=lf`, então é preciso garantir que os PNG sejam tratados como binário. O dono decide quando versionar as imagens.

## 9. Em aberto (nenhum bloqueia o contrato nem os planos)

- **Tela 20:** as caixas "Enviar o resultado por e-mail ao RH" e "Convidar aprovados".
- **Definição de "presença agora"** (roadmap, fatia 7).
- **Largura mínima da tela:** o Figma é 1440; a demo pode ser em projetor de 1366×768 (não verificado). **[minha escolha]** provisória: projetar para 1366 e conferir na demo.
- Se o **RH pode recusar** o que o gestor aprovou na segunda etapa (suposto que sim; confirmar na fatia 3).
- Conferir a **versão atual do Pomelo** no nuget.org (a afirmação "não existe 10.x" do spec é anterior a 2026-10-21) e a **compatibilidade do MSW** com a política de segurança (só roda em desenvolvimento e nos testes, não no build de produção).
- Se o **Kauan** confere o contrato no que tocar o banco, e em que momento. A pessoa do front **não precisa de acesso ao Figma**: tudo sai dos PNG.

## 10. O que fica para quem escrever os planos

Detalhar passo a passo e comandos; escolher nomes de arquivos e pastas dentro do que a seção 3 fixa; decidir a ferramenta de geração de tipos e de handlers MSW dentro dos critérios da seção 6; escrever as fichas das fatias 2 a 8 a partir das telas e do roadmap; definir os campos do contrato que o spec e o banco não decidem (nomes, paginação, formato de erro), registrando cada escolha. Tudo o que a conversa deixou como **[minha escolha]** pode ser mudado pelo dono ao ler.
