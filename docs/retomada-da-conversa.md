# Retomada da conversa (planejamento do RH-System)

Escrito em 2026-10-28 para continuar o trabalho em outro PC, numa sessão nova do Claude Code. A sessão antiga não passa para o outro PC; este arquivo e os documentos do repositório passam.

## Como retomar

1. Neste PC: os arquivos precisam estar no GitHub (ver a branch `docs/planejamento`). No outro PC: `git fetch`, `git switch docs/planejamento` (ou a branch indicada), e abrir o Claude Code na pasta do repositório.
2. Na primeira mensagem, dizer: **"Leia docs/retomada-da-conversa.md e continue de onde paramos."**
3. Reinstalar no outro PC o que é local de cada máquina (ver "O que NÃO passa para o outro PC").

## Onde está o trabalho

- **Spec (design aprovado):** `docs/superpowers/specs/2026-10-21-rh-system-design.md`. A seção 10 tem todas as decisões do dono.
- **Plano detalhado da fatia 0 (esqueleto, 14 tarefas):** `docs/superpowers/plans/2026-10-21-fatia-0-esqueleto.md`.
- **Roadmap completo (fatias 0 a 8):** `docs/superpowers/plans/2026-10-28-roadmap-completo.md`. Cada fatia ganha seu plano passo a passo só no começo dela, revisado pelo agente `revisor-de-plano`.
- **Lista corrida de pedidos para o Kauan (banco):** `docs/pedidos-para-o-kauan.md`. O dono vai encaminhar de uma vez quando estiver completa.
- **Scripts do grupo:** `Banco de Dados/` (tabelas, triggers, funções). Mudam quase todo dia; reler antes de afirmar algo.

## O que está pendente agora

**Parte 3 da lista para o grupo (formulários, treinamentos, férias).** O dono ia responder pelo número ("sim", "não" ou a letra). Perguntas e recomendação (⭐):

Formulários
1. Tipo "quiz" (o banco aceita entrevista, admissão, avaliação, pesquisa, outro). ⭐ sim
2. Quem deve responder e prazo (sem isso não existem "formulários pendentes"). ⭐ sim
3. Perguntas com opções, resposta certa e pontos; cortar "envio de arquivo", "eliminatória" e "nota dada pelo RH". ⭐ sim
4. Entrevista por link público (candidato que não é funcionário; a parte mais trabalhosa e de segurança). Sem recomendação; está no esboço do dono ("entrevista de emprego automática").
5. Avaliação por ciclo (gestor avalia equipe e equipe avalia gestor). ⭐ sim
6. Anonimato: ⭐ não anônimo (o RH vê quem respondeu; resultado agrupado por setor; gestor nunca vê o conteúdo).

Treinamentos
7. Módulos e progresso: (a) com módulos como no Figma; (b) sem módulos (título, descrição e link; a pessoa marca concluído e faz o quiz final). ⭐ (b)
8. Nota: o banco guarda 0 a 100 e exige 70; o Figma mostra 0 a 10 com mínima 7. ⭐ não mudar nada, mínima sempre 7. Pergunta: a mínima pode variar por treinamento?
9. Turmas presenciais: ⭐ cortar; manter "inscrever-se" nos opcionais (o status "inscrito" já existe).

Férias
10. Adiantar a 1ª parcela do 13º (uma coluna sim ou não). ⭐ sim
11. "Sugerir outra data" do gestor. ⭐ cortar (o gestor recusa com observação).
O banco já suporta férias em até 3 partes e o abono.

**Depois da Parte 3 (Parte 4):** benefícios (pedidos de adesão, dependentes, trajeto do vale-transporte), regra de pagamento, documentos do colaborador, atestados, e uma pergunta: **quem cria as tabelas novas inteiras**, o Kauan ou o Claude (tabelas da aplicação, como auditoria, fila de e-mail, tokens e importações, o Claude cria em scripts próprios).

**Outras pendências:** o dono ia verificar a conta do Azure com créditos (SENAI); o PR do CODEOWNERS está aceito e na `main`; o gestor reaproveita as telas do funcionário; o fuso `-03:00`; a escolha do método de execução (subagentes ou nativo) fica para depois do planejamento.

## Preferências do dono (importante)

- Responder em **português simples, sem jargão**, em **partes pequenas**, uma decisão de cada vez. Ele não lê textos longos.
- Mostrar o problema (com linhas do arquivo) **antes** de corrigir. Não corrigir arquivos do grupo sem pedir.
- **Nunca pôr o Claude como coautor** (nem `Co-Authored-By`, nem "Generated with Claude Code") em commits ou PRs.
- Conferir fatos no arquivo ou com busca, não de memória; separar "li no arquivo" de "suponho".

## Fatos técnicos que o Claude novo precisa saber

- Usuário do GitHub: `xmiguelps`; repositório `xmiguelps/RH-System`.
- A `main` **só aceita mudança por pull request** e exige **1 aprovação de outra pessoa** com permissão de escrita (o autor não aprova o próprio PR). Branches podem ser enviadas direto.
- No PC antigo o `gh` não estava instalado (o PR foi aberto pelo link que o GitHub mostra no push), nem Docker; Node era a versão 18 (precisa de 22 ou mais para o front-end). No outro PC, conferir.
- O Kauan Dias e o Kafly21 são a mesma pessoa; o Lucas é outro integrante. Eles sobem arquivos pela interface web.
- O Kauan está corrigindo os triggers `TRG_PONTOS_BEFORE_INSERT/UPDATE` (usam colunas removidas e copiam a carga do cargo sem dividir por 5). Não mexer nesses arquivos do grupo sem combinar; antes de baixar o trabalho dele, não deixar alteração local neles.
- O Figma é lido pelo MCP do Figma (precisa reautenticar no outro PC). Arquivo `bsZnCjHNeNgdGaLzVcaxjL`, página "Alta Complexidade" `1:2` (61 telas). Nunca pedir `get_metadata` da página inteira (2,9 milhões de caracteres); usar o id da seção de cada tela (ids no roadmap).

## O que NÃO passa para o outro PC (recriar lá)

- **Memória do Claude** (fica em `C:\Users\ead\.claude\projects\...\memory`): o essencial está neste arquivo e nos documentos acima.
- **Configuração global do Claude** em `C:\Users\ead\.claude\`: `CLAUDE.md`, `settings.json` e os agentes `agents/pesquisador.md` (Haiku 5.5) e `agents/revisor-de-plano.md` (Opus 5.5). Cópias estão em `docs/claude-config/` (leia o `LEIA-ME.md` de lá: diz o que copiar para onde e quais chaves mesclar no `settings.json`). Os agentes só são carregados quando o Claude Code inicia.
- Plugins (superpowers, frontend-design, figma) estão habilitados em `.claude/settings.json` do repositório, mas o Claude Code do outro PC pode pedir para instalá-los e autenticar.
