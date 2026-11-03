# PROGRESSO do front-end

> **Este arquivo é a sua memória.** Atualize-o **a cada passo** e commite-o **junto com o passo** (`01-regras.md`, seção E). Depois que a conversa for compactada ou reiniciada, **retome daqui**: leia "Onde estou", rode `git status` e `git log -5 --oneline`, confira a versão do contrato e siga o "próximo passo".
>
> As linhas marcadas **EXEMPLO** mostram o formato e **são inventadas**: **apague-as** quando preencher as suas. Data e hora: `Get-Date -Format 'yyyy-MM-dd HH:mm'`. Versão do contrato: `Get-Content contrato\openapi.yaml -TotalCount 12 | Select-String '^\s+version:'`.

## Onde estou

(Sobrescreva este bloco a cada passo.)

- **Fatia · tarefa · passo:** ainda não comecei (primeira ação de `LEIA-PRIMEIRO.md`)
- **Próximo passo exato:** pedir a autorização de Git
- **Branch:** (nenhuma ainda)
- **Autorização de Git:** ainda não pedida (quando dada: "dada por <nome> em <data>"; sem autorização: "não autorizou: sem branch e sem commit")
- **Push:** (ok | `pendente: push`, se falhou por permissão: `01-regras.md`, C1)
- **Node (`node -v`):** (a saída; se for 26 ou mais nova, escreva `Node 26: não medido`; se foi preciso instalar o Node 24 LTS, `pendente: Node` até refazer a prova que falhou: `02-pilha.md`, seção 2)
- **Versão do contrato em uso (`info.version`):** (anote a lida agora)
- **`STATUS.md` visto em:** (data); fatia 0: (planejada | em andamento | pronta)
- **Última prova:** (comando e resultado)
- **Último `csp`:** (`PASSOU` em <data>, ou `pendente: csp` se não há Edge nem Chrome: `01-regras.md`, A11 e G2)

## Passos

Uma linha por passo (os mais novos embaixo). Resultado: `feito`, `pendente de integração`, `pendente: <motivo>` ou `bloqueado (<código da dúvida>)`. Onde: `F0·T3·S5` = fatia 0, tarefa 3, passo 5. **A coluna Commit guarda o hash do commit anterior** (um commit não contém o próprio hash: `01-regras.md`, E1): `git log -1 --format=%h` antes de commitar; o hash do commit que leva este arquivo não é registrado.

| Data | Onde | Resultado | Comando que provou e o que ele mostrou | Commit | Contrato |
|---|---|---|---|---|---|
| EXEMPLO 2026-11-03 14:20 | F0·T1·S8 | feito | `npm --prefix front-end test`: 3 arquivos, 7 testes passaram | a1b2c3d | 0.2.0 |
| EXEMPLO 2026-11-03 16:05 | F0·T6·S3 | pendente de integração | (não rodou: `STATUS.md` diz fatia 0 `planejada`) | a1b2c3d | 0.2.0 |

## Telas

Uma linha por tela (as notas de tela em `telas\fatia-N.md` dizem quais são). Estado: `não começada`, `em andamento`, `feita`, `pendente de integração`, `bloqueada (<código da dúvida>)`. A coluna **Commit** é o hash do commit anterior (E1). **Feita com contrato** é a versão com que a tela foi construída; **Revalidada com** é a versão em que você a conferiu de novo depois de uma mudança do contrato (`01-regras.md`, seção F). **Provisório:** a rota usada em `PROVISORIOS.md`, se houver.

| Tela | Nome | Perfil | Fatia | Estado | Feita com contrato | Revalidada com | Usa provisório | Commit | Conferir ao lado do PNG |
|---|---|---|---|---|---|---|---|---|---|
| EXEMPLO 99 | Lista de exemplo | RH | 1b | feita | 0.2.0 | 0.3.0 | usa provisório: `listarExemplos` | a1b2c3d | sim |

## Pendente de integração

Cada passo que precisa da API de verdade (`03-contrato-e-mocks.md`, seção 7). **A sessão nunca o roda**, nem quando `contrato\STATUS.md` diz `pronta` para a fatia: ela registra a linha com o roteiro pronto (o motivo muda: "a rota ainda não existe" ou "a rota já existe, mas quem roda é a pessoa ou o dono") e a pessoa traz o resultado, que vai para "Conferências da pessoa".

| O que | Rota (`operationId`) | Por que não rodou | Roteiro para quando puder |
|---|---|---|---|
| EXEMPLO entrar com o administrador e ver o shell | `entrar` | `STATUS.md`: fatia 0 `planejada` | `.env.local` com `VITE_API_URL` e `VITE_MOCKS=nao`; `npm --prefix front-end run dev`; entrar, F5, fechar e reabrir, sair numa aba e ver a outra |

## Conferências da pessoa (não bloqueiam)

Passos que só se veem olhando um navegador (por exemplo, `npm --prefix front-end run dev`). **Você não os executa** e não espera por eles (`01-regras.md`, A11): anote aqui o roteiro; a pessoa o roda e traz o resultado, que você registra. Tudo isto vai no relatório final.

| O que | Onde no plano | Roteiro para a pessoa | Resultado (a pessoa traz) |
|---|---|---|---|
| EXEMPLO ver o MSW ligado no desenvolvimento | `fatia-0.md`, Task 4, passo 6 | `npm --prefix front-end run dev`; abrir o endereço do Vite; o console mostra `[MSW] Mocking enabled`; entrar com qualquer e-mail e senha abre o shell | (vazio) |

## Mudanças de contrato absorvidas

| Versão | O que quebrou (arquivos que o `tsc` e os testes apontaram) | Telas refeitas | Commit |
|---|---|---|---|
| EXEMPLO 0.3.0 | `FuncionarioListaItemRh.cargo` virou obrigatório: 2 fixtures e a tela 02 | 02 | a1b2c3d |

## Conferência visual (lista para a pessoa)

As telas que a pessoa deve conferir **ao lado do PNG**, uma vez por fatia. Acrescente ao fechar cada fatia (número, nome, PNG, e o que olhar: textos, colunas, estados vazio e de erro).

- (nenhuma ainda)

## Roteiro da demo (fatia 8)

(Preencha na fatia 8: o passo a passo da demonstração, tela a tela, **a 1366 px de largura**, com as pessoas e os dados de `contrato\ELENCO.md`. O roteiro vive **aqui**: não crie arquivo de documentação à parte, A13 de `01-regras.md`.)

## Relatório final

(Preencha ao não restar nada a fazer: `01-regras.md`, seção H.)
