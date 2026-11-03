# Ficha da fatia 4: comunicação e documentos (front-end)

Contrato lido: `contrato\fontes\40-fatia-4-comunicacao-e-documentos.yaml` (a versão do contrato é o `info.version` de `contrato\openapi.yaml`; confira-a antes de começar e anote-a no `PROGRESSO.md`), `contrato\CONVENCOES.md` (seções 3, 5, 6, 11 e 12), `contrato\ELENCO.md` e `contrato\PROVISORIOS.md`. Notas de tela: `docs\plano-front-end\telas\fatia-4.md` (leia a seção "Regras comuns" primeiro). Caminhos de tela: `docs\plano-front-end\05-caminhos-de-tela.md` (tabela única).

Esta ficha **não traz passo a passo fino**. Repita o padrão da fatia 1b (`fatia-1b-telas.md`): um passo pequeno por vez, cada um com um comando que prova, `PROGRESSO.md` atualizado a cada passo, commit e push por passo na sua branch.

## 1. Objetivo e entrega demonstrável

O RH publica **avisos** (simples e detalhados), acompanha quem leu e lembra quem não viu; publica o **holerite** de cada pessoa (um a um ou em lote), corrige um holerite errado por nova versão; registra **advertências**, que a pessoa confirma ou contesta e o RH responde. Todos veem os avisos e os holerites que são seus; cada pessoa vê as próprias advertências; o gestor registra e acompanha as advertências da equipe.

Demonstração (dados simulados, "hoje" = quarta, 2026-10-07, como em `ELENCO.md`): Ana Souza publica o aviso "Inventário de estoque no sábado, 17/10"; Bruno Lima abre o aviso e o contador do menu cai; Ana envia o holerite de outubro do Bruno agendado para o dia do pagamento; Carlos Ribeiro registra a advertência do Diego Farias; Diego contesta; Ana mantém a advertência.

## 2. Telas

**16 telas com print.**

| NN | Nome | Perfil | PNG (`Figma\telas\`) | Incremento |
|---|---|---|---|---|
| 27 | Avisos (lista, números e detalhe) | RH | `27-rh-avisos.png` | 4a |
| 28 | Novo aviso (painel lateral) | RH | `28-rh-avisos-novo-aviso.png` | 4a |
| 37 | Avisos | Funcionário | `37-func-avisos.png` | 4a |
| 38 | Aviso aberto (modal) | Funcionário | `38-func-avisos-aviso-aberto.png` | 4a |
| 50 | Avisos (reutiliza a 37 com o menu do gestor) | Gestor | `50-gestor-avisos.png` | 4a |
| 07 | Ficha do colaborador, aba Holerites | RH | `07-rh-colaboradores-ficha-aba-holerites.png` | 4b |
| 08 | Modal "Enviar holerite" | RH | `08-rh-colaboradores-ficha-modal-enviar-holerite.png` | 4b |
| 36 | Holerites | Funcionário | `36-func-holerites.png` | 4b |
| 49 | Holerites (reutiliza a 36 com o menu do gestor) | Gestor | `49-gestor-holerites.png` | 4b |
| 14 | Advertências (lista e números) | RH | `14-rh-advertencias.png` | 4c |
| 15 | Nova advertência (painel lateral) | RH | `15-rh-advertencias-nova-advertencia.png` | 4c |
| 11 | Ficha do colaborador, aba Advertências | RH | `11-rh-colaboradores-ficha-aba-advertencias.png` | 4c |
| 43 | Minhas advertências | Funcionário | `43-func-advertencias.png` | 4c |
| 59 | Advertências do setor | Gestor | `59-gestor-advertencias-da-equipe.png` | 4c |
| 53 | Registrar advertência (a partir da ficha da equipe) | Gestor | `53-gestor-painel-da-equipe-ficha-registrar-advertencia.png` | 4c |
| 60 | Registrar advertência (a partir de "Advertências do setor") | Gestor | `60-gestor-advertencias-da-equipe-registrar-advertencia.png` | 4c |

**Telas em texto (sem print; o desenho é meu), em `telas\fatia-4.md`:**

| Tela | Perfil | Observação |
|---|---|---|
| Substituir holerite (modal) | RH | A API tem a nova versão; o Figma não tem o botão. |
| Enviar holerites em lote | RH | O contrato tem a rota; o Figma não desenha o lote. |
| Detalhe da advertência e "Responder contestação" | RH | O botão "Abrir" está nos PNG 14; o detalhe não. |
| "Contestar" (modal) e "Ver detalhes" | Funcionário | Os botões estão no PNG 43; os modais não. |
| Detalhe da advertência ("Abrir") | Gestor | O botão está no PNG 59; o detalhe não (nenhum PNG o desenha). **Painel lateral** sobre a lista 59, como o do RH: `/gestor/advertencias/:advertenciaId` (`05-caminhos-de-tela.md`). |
| Aba Advertências da ficha da equipe | Gestor | A aba existe no PNG 52 (fatia 1b); sem print do conteúdo. |
| Ações de rascunho e agendado nos avisos ("Publicar", "Excluir") | RH | Dentro da tela 27; o Figma não as desenha. |

**Fora desta fatia:** o cartão "Aniversariantes" da tela inicial (`listarAniversariantes`) e os cartões "Avisos" e "Próximo pagamento" da tela inicial são da fatia 7d (o componente do cartão "Próximo pagamento", porém, nasce aqui, nas telas 36 e 49: ver a seção 5); aqui só existe a rota (aniversariantes) e a leitura dos avisos. `listarAniversariantes` entra na conta do teste de cobertura da **ficha 7**, não desta.

## 3. Rotas do contrato por tela

35 rotas da fatia (todas têm tela, com uma exceção declarada; conferência no fim de `telas\fatia-4.md`).

| Tela | `operationId` |
|---|---|
| 27 | `listarAvisos`, `obterResumoDosAvisos`, `obterAviso`, `encerrarAviso`, `listarLeiturasDoAviso`, `lembrarQuemNaoViuAviso`, `publicarAviso` e `excluirAviso` (rascunho e agendado, sem print) |
| 28 | `criarAviso`, `editarAviso` (o mesmo painel, em modo de edição) |
| 37 e 50 | `listarMeusAvisos` |
| 38 | `obterMeuAviso`, `marcarAvisoComoLido` |
| 07 | `listarHoleritesDoFuncionario`, `obterLinkDeDownloadDoHolerite`, `substituirHolerite` (modal sem print) |
| 08 | `enviarHolerite`, `listarHoleritesDoFuncionario` (só `ultimoPublicado`) |
| Lote (sem print) | `enviarHoleritesEmLote` |
| 36 e 49 | `listarMeusHolerites`, `obterLinkDeDownloadDoMeuHolerite`, `obterProximoPagamento` (rota da fatia 7; alimenta o cartão "Próximo pagamento", componente único `{ data, mesReferencia }`; as telas 36 e 49 não têm painel, por isso chamam a rota) |
| 14 | `listarAdvertencias`, `obterResumoDasAdvertencias`, `obterAdvertencia`, `responderContestacaoDeAdvertencia`, `listarSetores` (fatia 1, o filtro de setor) |
| 15 | `registrarAdvertencia`, `listarFuncionarios` (fatia 1, escolher o colaborador) |
| 11 | `listarAdvertenciasDoFuncionario`, `obterAdvertencia`, `registrarAdvertencia` |
| 43 | `listarMinhasAdvertencias`, `confirmarCienciaDeAdvertencia`, `contestarAdvertencia` |
| 59 | `listarAdvertenciasDaEquipe`, `obterResumoDasAdvertenciasDaEquipe`, `obterAdvertenciaDaEquipe`, `listarEquipeDoGestor` (fatia 1, nome do setor) |
| 53 e 60 | `registrarAdvertenciaDaEquipe`, `listarEquipeDoGestor` (a lista da tela 60) |
| Aba Advertências da ficha da equipe (sem print) | `listarAdvertenciasDoFuncionarioDaEquipe`, `obterAdvertenciaDaEquipe` |
| **Só API (tela é da 7d)** | `listarAniversariantes` |

Rotas de outras fatias que estas telas **lêem**: `listarFuncionarios`, `listarSetores`, `listarEquipeDoGestor` (fatia 1), `obterProximoPagamento` (fatia 7, o cartão "Próximo pagamento") e `obterContadoresDoMenu...` (as bolinhas do menu: a fundação já as lê com `useContadoresDoMenu`; esta fatia só chama `invalidarContadoresDoMenu`).

## 4. Regras e cortes que valem

Do roadmap (fatia 4), do spec (13.3), de `CONVENCOES.md` (seção 12, e-mails) e do contrato. O contrato é lei; onde o Figma diverge, vale o contrato.

**Avisos**
- **Sempre para todos.** Não existe público por setor: o texto "ou para setores" do PNG 27, o campo "Para quem" do PNG 28 e os rótulos "Para: Todos", "Para: Logística", "Para: Financeiro" e "Para: Comercial" dos PNG 27, 37, 38 e 50 **saem**.
- **Sem anexo PDF.** O botão "Baixar a política completa (PDF)" dos PNG 27 e 38 e o "e anexo" do tipo "Detalhado" no PNG 28 **saem**.
- **E-mail sempre**: o e-mail para todos sai na publicação (agora ou na data agendada). A caixa "Enviar por e-mail também" do PNG 28 **sai**. Editar não reenvia e-mail.
- **Simples**: texto puro, até 280 caracteres. **Detalhado**: markdown (seções com `##`), até 10.000 caracteres. **HTML nunca é interpretado** (roadmap, decisão 6): mostre texto; nunca use `dangerouslySetInnerHTML`. O aviso **detalhado** se mostra com o componente **`Markdown`** da 1a (`src\shared\ui\Markdown.tsx`, `react-markdown` 10.1.0; `fatia-1a-fundacao.md`, Task 7; `##` vira título `<h3>`): **use-o, não escreva outro renderizador**. O aviso **simples** é texto puro (`<p className="whitespace-pre-line">`), **sem** passar pelo `Markdown`, como a 1a manda.
- **Aviso publicado não se exclui, só se encerra** ("Encerrar agora"). Rascunho e agendado se **excluem** (botão que o Figma não desenha).
- **Abrir o aviso o marca como lido** (`marcarAvisoComoLido`, idempotente). "Quem viu" do RH conta quem abriu.

**Holerites**
- **Com tipo** (Mensal, 13º salário, Férias) e **sem marca "Novo" nem "visto"**: o chip "Novo" dos PNG 36 e 49 **sai**.
- **PDF de até 5 MB** (5 × 1024 × 1024 bytes), só PDF. O RH publica **agora**, **no dia do pagamento** (o sistema calcula a data) ou **numa data** (ver a pergunta abaixo).
- **E-mail sempre** na publicação: a caixa "Avisar Bruno por e-mail quando publicar" do PNG 08 **sai**.
- **Competência futura só no tipo Férias.** Holerite errado **não se apaga**: nova versão (`substituirHolerite`). A pessoa vê só a maior versão **já publicada**; o RH vê o histórico.
- **O gestor não vê holerite de ninguém da equipe.** As telas 36 e 49 mostram só os **próprios** holerites.
- **Envio em lote entra** (decisão do dono, 2026-10-30), sem tela no Figma.

**Advertências**
- Verbal ou Escrita. Nasce `PENDENTE` ("Aguardando ciência"); a pessoa **confirma a ciência** (`APLICADA`, "Ciente") ou **contesta** (`CONTESTADA`); o RH **mantém** (volta a `APLICADA`) ou **acata** (`CANCELADA`) e responde **por e-mail**.
- **E-mail**: só a pessoa recebe quando a advertência é registrada. Quando o **gestor** registra, o **RH** recebe uma cópia. Quando o **RH** registra, o **gestor não** recebe cópia (decisão do dono, 2026-10-30). Por isso as caixas "Avisar o colaborador e o gestor por e-mail" (PNG 15) e "Avisar o colaborador por e-mail" (PNG 53 e 60) **saem**.
- **O gestor só registra e vê a equipe dele** (outro setor: 404). O gestor **não vê o texto da contestação nem a resposta do RH** (`x-provisorio` de `AdvertenciaDetalheGestor`).
- **A resposta do RH vai por e-mail e não fica guardada**; a pessoa não a vê na tela, só a nova situação.
- **O número "Registradas em 2026" do RH inclui as canceladas** (`contagens.todas`); o do **gestor não** (conta só as não canceladas). Mostre cada número como o contrato o define.

**Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-F4-5`): o botão **"Lembrar quem não viu"** do aviso (tela 27, `lembrarQuemNaoViuAviso`) **fica**, **sem limite** de lembretes por aviso; o e-mail entrou na lista fechada (`contrato\CONVENCOES.md`, seção 12, contrato 0.2.1). **Não é mais provisório** (`contrato\PROVISORIOS.md` já não o lista): construa normalmente e **não** anote "usa provisório".

**Decisões ainda abertas (`x-provisorio`, construa normalmente):**
- `paths > /api/rh/holerites/lote > post`: só o **formato** do lote está em aberto; **não há tela desenhada**.
- `paths > /api/rh/holerites/{holeriteId}/versoes > post`: a correção por nova versão é decisão da sessão por delegação do dono; ele pode discordar.
- `paths > /api/eu/holerites/{holeriteId}/download > get`: "Ver" e "Baixar PDF" (PNG 36 e 49) usam o **mesmo** link e o PDF **sempre baixa** como anexo.
- `paths > /api/rh/advertencias/{advertenciaId}/resposta > post`: o desfecho da contestação (hoje: manter ou acatar, resposta só por e-mail).
- `components > schemas > AdvertenciaTipo` (só Verbal e Escrita), `AdvertenciaAplicadaPor` (quando o RH registra, o nome vem `null` e a tela mostra "RH"; o PNG 14 mostra nomes de pessoas do RH), `AdvertenciaDetalheGestor` (o gestor não vê a contestação) e `AdvertenciaRegistroRhComando > properties > aplicadaPor` (as duas opções: "Gestor do colaborador" e "RH").

## 4b. Dúvidas já conhecidas (registre em `DUVIDAS.md` quando chegar ao passo, com o modelo de lá; o texto abaixo é o conteúdo) e a decisão do dono sobre o menu

**D-F4-1, modal "Enviar holerite" (tela 08).** Onde: `telas\fatia-4.md`, tela 08. O que achei: o PNG 08 só tem "Publicar agora" e "Agendar para o dia do pagamento"; o contrato (`enviarHolerite`) também aceita agendar numa data (`publicarEm`) e a própria mensagem de erro da API (422 `EMPRESA_NAO_CONFIGURADA`) diz "ou escolha uma data". O que sugiro: construir a terceira opção, "Agendar para uma data". Pergunta: "Posso manter a opção 'Agendar para uma data' no modal de enviar holerite, que o Figma não desenha mas o contrato permite?" **Não bloqueia: construa a opção e siga.**

**Onde o RH e o gestor veem as próprias advertências, holerites e avisos. Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-menu`): o RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu, com os itens pessoais **ativos**. Nesta fatia: no RH, **"Holerites"** (`/eu/holerites`), **"Minhas advertências"** (`/eu/advertencias`, o mesmo nome do item do gestor) e, pelo grupo, **"Meu ponto"**, **"Férias"**, **"Meus formulários"**, **"Meus treinamentos"** e **"Benefícios"** (cada um é da fatia da sua tela); no gestor, **"Minhas advertências"** (`/eu/advertencias`; "Holerites" e "Avisos" ele já tem no menu do Figma). As telas pessoais (`/eu/holerites`, `/eu/avisos`, `/eu/advertencias`) continuam sendo páginas para os três perfis (`05-caminhos-de-tela.md`, regra 6). O Figma não desenha o grupo; o que a pessoa confere é o desenho, não a existência dos itens.

**D-F4-3, link "Fale com o RH" (telas 36, 49 e 43).** O que achei: o PNG mostra "Dúvida sobre um valor? Fale com o RH" e "Dúvidas sobre uma advertência? Fale com o RH" como links, mas o contrato não tem rota nem endereço para isso. O que sugiro: mostrar como texto simples, sem link, até o dono dizer o destino (por exemplo um e-mail do RH). Pergunta: "O que acontece ao clicar em 'Fale com o RH'?" **Não bloqueia: texto simples.**

**D-F4-4, envio de holerites em lote (sem tela).** O que achei: o contrato tem `enviarHoleritesEmLote` (`x-provisorio`), mas o Figma não desenha o lote nem diz de onde ele se abre. O que sugiro: um botão "Enviar holerites em lote" ao lado de "Adicionar colaborador" na lista de colaboradores (tela 01) que abre a tela descrita em `telas\fatia-4.md`. Pergunta: "Posso abrir o envio em lote por um botão na lista de colaboradores?" **Não bloqueia: construa assim e siga.**

## 5. Ordem de construção e dependências

**Usa da fundação (fatia 1a) e da fatia 1b:** cliente de API e dados simulados gerados do contrato; menu e rotas por perfil; ficha do colaborador com abas (tela 03; as abas Holerites e Advertências entram aqui); `listarFuncionarios` e o **seletor de colaborador** (busca por nome; reaproveite o da fatia 3, tela 12, se já existir); `listarSetores`; `listarEquipeDoGestor`; `GET /api/auth/eu` (traz `setor` e `cargo`); componentes de painel lateral, modal, abas, chip, cartões de número, tabela, campo de data, **componente de envio de arquivo**, auxiliar de download por link temporário (`abrirLinkTemporario`, `src\shared\arquivos\baixar.ts`) e formatação de datas em Brasília. **Não** depende das fatias 2, 3, 5 e 6. As bolinhas do menu **já vêm da fundação** (`useContadoresDoMenu`); o cartão "Próximo pagamento" das telas 36 e 49 é **desta** fatia: é o **componente único** `{ data, mesReferencia }` (`pagamento.data` e `competencia.mes`; sem `data`, não desenha nada), que a fatia 7 reaproveita nas telas 32 e 46; aqui ele é alimentado por `obterProximoPagamento` (rota da fatia 7: dados simulados do contrato enquanto isso). **Formatadores:** use os da 1a (`fatia-1a-fundacao.md`, Task 2: `formatarData`, `formatarDataHora`, `formatarHora`, `formatarDiaMes`, `plural`) e os da 1b (`formatarDiaEMes`, `formatarDataDoInstante`; "Para as fichas 2 a 8") e **não os recrie**; o **nome do mês** ("outubro" de `mesReferencia`; "Setembro de 2026" no rótulo do holerite) não existe em nenhum dos dois: acrescente-o ao mesmo `datas.ts`, com teste e sem `Date`.

Ordem sugerida, um passo por item (cada um com teste):
1. **4a, avisos do funcionário e do gestor** (telas 37, 38 e 50): lista com abas "Não lidos" e "Todos" (**"Todos" é a escolhida ao abrir**, como no PNG 37 e no 50), busca, abrir o aviso (marca como lido), contador. É a leitura mais simples e já usa o `Markdown` da 1a para o aviso detalhado.
2. **4a, avisos do RH** (telas 27 e 28): lista por abas, números, detalhe, "Quem viu", criar, editar, publicar, encerrar, excluir e lembrar.
3. **4b, holerites do funcionário e do gestor** (telas 36 e 49) com o cartão "Próximo pagamento".
4. **4b, holerites do RH**: aba da ficha (07), envio (08), substituir e **lote** (tela em texto).
5. **4c, advertências do funcionário** (tela 43): ciência, "Contestar" e "Ver detalhes".
6. **4c, registrar advertência do RH** (telas 15 e 11, o mesmo painel) e a **lista do RH** (14) com o detalhe e "Responder contestação".
7. **4c, advertências do gestor** (telas 59, 53 e 60) e a aba Advertências da ficha da equipe.
8. Passe final: invalidações das bolinhas do menu e a lista de telas para conferência visual.

## 6. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura confere as **35 operações** da seção 3, **menos** `listarAniversariantes` (a tela é da fatia 7d; a **ficha 7** a conta no teste de cobertura dela, então nenhuma operação com tela fica sem teste). `coberturaDaFatia4.test.ts` guarda essa exclusão escrita, com o motivo. O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Passos novos do `csp`:** **nenhum** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário; o envio de arquivo, o markdown e o link temporário já são da 1a e da 1b: não há recurso de primeira vez nesta fatia).

**Exemplos nomeados** (`exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`; ver `03-contrato-e-mocks.md`): `enviarHolerite` 422 `empresaSemDiaDePagamento`, `competenciaFutura`, `anteriorAAdmissao`; `registrarAdvertencia` 422 `inativo`, `semGestor`.

Testes com MSW que precisam existir (use os `example` do contrato e as pessoas e números de `ELENCO.md`):
1. **Avisos do funcionário**: a lista mostra os 4 avisos do exemplo, "Não lidos 2" e "Todos 4" (`contagens`); abrir o aviso de id 409 ("Política de trabalho remoto atualizada") chama `obterMeuAviso` **e** `marcarAvisoComoLido`, depois invalida a lista e as contagens; ao abrir sem `?aba=`, a aba "Todos" vem escolhida (PNG 37 e 50); sem "Para: ..." e sem botão de PDF (a ausência se testa como no item 12).
2. **Avisos do RH**: as quatro abas (Ativos 4, Agendados 1, Expirados 12, Rascunhos 1); o aviso 412 mostra "41 de 101 viram"; "Encerrar agora" chama `encerrarAviso` (e 422 `AVISO_NAO_ESTA_ATIVO` mostra o `detail`); rascunho e agendado mostram "Excluir" (`excluirAviso`) e **não** "Encerrar agora"; "Lembrar quem não viu" chama `lembrarQuemNaoViuAviso` e mostra a confirmação com o `quantidadeDeEmails` devolvido (28 no exemplo; 0 mostra "Todos já viram este aviso.").
3. **Novo aviso**: simples com mais de 280 caracteres não envia (e 400 do servidor aparece no campo); "Salvar rascunho" envia `publicar: false`, "Publicar aviso" envia `publicar: true`; **sem** campo "Para quem" nem "Enviar por e-mail também" (a ausência se testa como no item 12); o aviso detalhado com `<script>` aparece **como texto** (nunca vira HTML: é o `Markdown` da 1a, que já tem esse teste; aqui o teste confere o aviso desta tela).
4. **Holerites do funcionário**: 10 holerites em 2026 (`total`), abas "Todos 10, Mensais 9, Férias 1, 13º salário 0", seletor de ano de `anosDisponiveis`, sem chip "Novo" (item 12), "Ver" e "Baixar PDF" abrem o link de `obterLinkDeDownloadDoMeuHolerite`, cartão "Próximo pagamento" com "6 de novembro" de `obterProximoPagamento` (some se `pagamento` for `null`); o teste de contrato desta tela inclui `capturar('obterProximoPagamento').conforme()` (sem parâmetros): é nele que a cobertura da ficha 7 acha essa operação.
5. **Enviar holerite**: só PDF até 5 MB (aviso antes de enviar; 400 do servidor no campo `arquivo`); "Publicar agora" não envia `publicarEm` nem `noDiaDoPagamento`; "Agendar para o dia do pagamento" envia `noDiaDoPagamento: true`; "Agendar para uma data" envia `publicarEm`; nunca os dois; 409 `HOLERITE_JA_EXISTE` oferece "Substituir"; 422 `HOLERITE_COMPETENCIA_FUTURA`, `..._ANTERIOR_A_ADMISSAO`, `..._POSTERIOR_AO_DESLIGAMENTO` e `EMPRESA_NAO_CONFIGURADA` mostram o `detail`; sem a caixa "Avisar ... por e-mail" (item 12). Teste com `// @vitest-environment node` (multipart).
6. **Lote de holerites**: um PDF por pessoa com `itens` (`funcionarioId`, `nomeDoArquivo`); resultado por item (2 enviados, 1 com erro `HOLERITE_JA_EXISTE`).
7. **Advertências do funcionário**: o cartão em destaque só para `PENDENTE`; "Confirmar ciência" chama `confirmarCienciaDeAdvertencia`; "Contestar" pede o texto (10 a 2000 caracteres) e chama `contestarAdvertencia`; 422 `ADVERTENCIA_NAO_ESTA_PENDENTE` ("Esta advertência já teve ciência ou contestação.") recarrega a lista.
8. **Advertências do RH**: números "Registradas em 2026: 9", "Aguardando ciência: 2", "Contestadas: 1", "Setor com mais registros: Logística, 4"; abas Todas 9, Aguardando ciência 2, Ciente 6, Contestada 1; busca, setor e tipo enviam os parâmetros certos; "Responder contestação" envia `{ decisao, resposta }` e 422 `ADVERTENCIA_NAO_ESTA_CONTESTADA` mostra o `detail`.
9. **Registrar advertência (RH e gestor)**: sem caixa de e-mail (item 12); RH envia `aplicadaPor` (`GESTOR_DO_COLABORADOR` ou `RH`), gestor **não** envia; 422 `ADVERTENCIA_SEM_GESTOR`, `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO` e `FUNCIONARIO_INATIVO` mostram o `detail`; depois de salvar, as listas e os números são atualizados.
10. **Advertências do gestor**: "Registradas em 2026: 3", "Aguardando ciência: 1 (Diego Farias)", "Pessoas com advertência: 2 de 8", "Por pessoa" na ordem da API; o detalhe **não** mostra o texto da contestação; 404 para pessoa de outro setor.
11. **Teste de contrato** (a lacuna do `tsc`: chave extra ao lado da chave certa em `query` ou `params.path`): cada tela confere os parâmetros enviados. Os que importam: `listarAvisos` (`situacao`, `tipo`, `busca`, paginação), `listarLeiturasDoAviso` (`situacao=NAO_LIDO`, `tamanhoPagina=5`), `listarMeusAvisos` (`apenasNaoLidos`, `busca`), `listarMeusHolerites` (`ano`, `tipo`), `listarHoleritesDoFuncionario` (`ano`, `tipo`, `todasAsVersoes`), `listarAdvertencias` (`situacao`, `setorId`, `tipo`, `busca`, `ano`) e `listarAdvertenciasDaEquipe` (`situacao`, `ano`).
12. **Ausência dos textos cortados** (o teste procura o texto **nos rótulos da tela** e **não** o encontra). Método **já pronto na 1b**: `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` de `src\test\textosProibidos.ts` (**não os reescreva**) e as linhas desta fatia acrescentadas à tabela de `src\app\textosProibidos.test.tsx`; **limite de palavra** (`/\bAvisar\b/i`; nas frases, a frase inteira) e **só nos rótulos de interface** dentro do contêiner da tela (`within(screen.getByRole('main'))`: títulos, botões, abas, `columnheader`, `label`, `legend`; o `rotulosDaTela` da 1b não lista `placeholder` nem `aria-label`: se a tela os usar com texto proibido, acrescente-os ao `rotulosDaTela` e ao teste dele), **nunca em células nem no texto vindo dos mocks** (o título de um aviso de exemplo pode dizer "Para: ..."). Textos por tela: **27, 37, 38 e 50**: "Para:", "Baixar a política completa", "ou para setores", "e para o seu setor"; **28**: "Para quem", "Enviar por e-mail também", "e anexo"; **36 e 49**: "Novo" (o chip; só nessas telas, onde "Novo" não é rótulo de nada); **08**: "Avisar ... por e-mail quando publicar"; **15, 53 e 60**: "Avisar o colaborador"; **gestor (59, aba Advertências da ficha da equipe e o detalhe)**: "Contestação" e "Resposta do RH" (o gestor vê a situação "Contestada", não o texto).
13. **Menu "Meu espaço"** (decisão do dono, 2026-11-02): com o perfil RH, o menu mostra no grupo "Meu espaço" os itens "Holerites" e "Minhas advertências", e o clique leva a `/eu/holerites` e `/eu/advertencias` (a tela abre, com o menu do RH ainda à vista); com o perfil Gestor, o grupo mostra "Minhas advertências" e o clique leva a `/eu/advertencias`, e "Holerites" e "Avisos" continuam no grupo do Figma. O teste usa `menuVisivel`/`todosOsCaminhosDoMenu` da 1a e `renderizarTela`; o do funcionário **não** ganha o grupo.

### Pendente de integração

As **35 rotas** estão `planejada` em `contrato\STATUS.md`. Enquanto não estiverem `pronta`, tudo roda com MSW e o `PROGRESSO.md` registra "pendente de integração", com o **roteiro pronto** (a sessão **não roda** a API real; a pessoa ou o dono roda o roteiro depois), para: o upload real de PDF de holerite (e do lote), o download real por link temporário, o e-mail de aviso, de holerite e de advertência, a publicação agendada de aviso e de holerite, as contagens "viram", e as bolinhas do menu.

## 7. Riscos

- **Conteúdo escrito por usuário**: aviso e advertência trazem texto livre. **Nunca** HTML cru. O teste com `<script>` e `<img onerror=...>` precisa existir.
- **Fuso**: `expiraEmUtc` (`...02:59:59Z`) é o fim do dia anterior em UTC e o dia certo em Brasília; formate sempre pelo fuso. `publicacaoEmUtc` de holerite agendado é o começo do dia (`...T03:00:00Z`).
- **Arquivos**: PDF de holerite sob a política de segurança. O envio é `multipart`; o download é por link temporário (nunca monte a URL). A navegação ao link temporário e o download por `Blob` **são medidos** pela sonda do script `csp` (fundação 1a, Task 13; `04-visual.md`), que a prova padrão já roda: não há o que confirmar à parte.
- **Lote**: até 200 PDFs e 100 MB; a tela é grande e sem desenho. Mantenha simples (uma tabela de arquivo e pessoa) e teste com poucos arquivos.
- **Visibilidade por perfil**: o gestor não vê holerite nem contestação da equipe; o RH que não é gestor não usa `/api/gestor/...`.
- **Telas sem print**: o desenho é meu; a pessoa confere uma vez, junto com as telas desta fatia.
- **Menus**: o Figma não desenha "Holerites" nem "Minhas advertências" pessoais no menu do **RH** nem "Minhas advertências" no do gestor; **o dono decidiu em 2026-11-02 que eles existem no grupo "Meu espaço"** (seção 4b; `DUVIDAS.md`, "Decisões do dono"), com o item "Minhas advertências" igual nos dois perfis e levando a `/eu/advertencias`.
- **Tabelas largas** (a tela 14 corta o botão "Abrir" no PNG): planeje a largura para 1366 px (`04-visual.md`).

## 8. Repita o padrão da fatia 1b

Mesma estrutura de pastas, mesmos hooks gerados do contrato, mesmos componentes, mesmo ciclo de cada passo (teste que falha, implementação, teste passa, lint e `format:check`, `PROGRESSO.md` com a **versão do contrato** de cada tela, commit e push). No relatório final, liste para a pessoa conferir ao lado do print: 07, 08, 11, 14, 15, 27, 28, 36, 37, 38, 43, 49, 50, 53, 59 e 60; e, **sem print**, as telas em texto desta ficha.
