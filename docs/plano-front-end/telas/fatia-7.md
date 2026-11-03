# Notas de tela da fatia 7: gestão (front-end)

Contrato lido: `contrato\fontes\70-fatia-7-gestao.yaml` (41 rotas; a versão do contrato é o `info.version` de `contrato\openapi.yaml`: confira-a antes de começar e anote-a no `PROGRESSO.md`, tela por tela). Ficha da fatia: `docs\plano-front-end\fichas\fatia-7.md`. Onde o Figma diverge dos documentos e do contrato, **valem os documentos e o contrato**.

**Como ler.** "Li" é o que está escrito no PNG ou no arquivo citado. "Suponho" é escolha minha onde nada decide: confira na conferência visual e, se discordar, registre em `DUVIDAS.md`. As telas **com print** têm o número do PNG (`Figma\telas\NN-...png`). As telas **sem print** (o Figma não as desenha, mas o contrato tem rota para elas) ganham aqui o número **7.1 a 7.6**: é número desta nota, não do Figma. Textos entre aspas são os que aparecem na tela.

**Telas desta fatia:** 11 com print (00, 05, 06, 32, 34, 35, 44, 45, 46, 48, 51) e 6 sem print (7.1 a 7.6).

**Caminhos de tela** (onde cada tela mora, o que é painel e o que é modal, de onde cada uma abre): `05-caminhos-de-tela.md`, a tabela única. Os pontos desta fatia: painel do RH `/rh/painel`, pendências 7.5 `/rh/painel/pendencias`, sem registro 7.6 `/rh/painel/sem-registro`; férias do RH 7.2 `/rh/pedidos-de-ferias` (`?pedido=<id>` destaca um pedido); benefícios do RH 7.3 `/rh/pedidos-de-beneficios`; pagamento 7.4 `/rh/cadastros/pagamento`; início `/eu/inicio`; férias `/eu/ferias` e o painel "Pedir férias" `/eu/ferias/pedir`; benefícios `/eu/beneficios` e o painel "Aderir" `/eu/beneficios/aderir/:beneficioId`; Painel da equipe `/gestor/equipe`, com a ficha da equipe (tela 52 e a aba 7.1, `?aba=ferias`) como **painel sobre ele** em `/gestor/equipe/:funcionarioId`.

## Mapa tela → rotas

| Tela | Perfil | Rotas que a tela chama (`operationId`) |
|---|---|---|
| 00 Painel inicial | RH | `obterPainelDoRh`, `obterContadoresDoMenuDoRh`; de outras fatias: `obterUsuarioLogado` (saudação), `listarFuncionarios` (busca) |
| 05 Ficha, aba Férias | RH | `obterFeriasDaFichaDoFuncionario` |
| 06 Modal "Registrar férias" | RH | `conferirRegistroDeFerias`, `registrarFeriasPeloRh` |
| 32 Tela inicial | Funcionário | `obterPainelDoFuncionario`, `obterContadoresDoMenuDoFuncionario`; de outras fatias: `obterUsuarioLogado`, `obterPontoHoje`, `listarMeusPedidosDeAjuste`, `listarMeusAvisos`, `listarAniversariantes` |
| 34 Férias | Funcionário | `obterMinhasFerias`, `aceitarSugestaoDeDataDasFerias`, `recusarSugestaoDeDataDasFerias` |
| 35 Painel "Pedir férias" | Funcionário | `conferirPedidoDeFerias`, `pedirFerias` (os números do topo já vêm de `obterMinhasFerias`) |
| 44 Benefícios | Funcionário (e Gestor) | `obterMeusBeneficios`, `pedirInclusaoDeDependente`, `pedirAtualizacaoDeTrajeto` |
| 45 Painel "Aderir ao ..." | Funcionário (e Gestor) | `pedirAdesaoABeneficio` |
| 46 Tela inicial | Gestor | as da tela 32, mais `obterContadoresDoMenuDoGestor` |
| 48 Férias | Gestor | as das telas 34 e 35 (a mesma tela, com o menu do gestor) |
| 51 Painel da equipe | Gestor | `obterPainelDaEquipe`, `obterCalendarioDeFeriasDaEquipe`, `aprovarFeriasComoGestor`, `recusarFeriasComoGestor`, `sugerirOutraDataDasFerias`, `obterContadoresDoMenuDoGestor`; de outras fatias: `listarEquipeDoGestor` (quadro "Hoje"), `aprovarPedidoDeAjusteComoGestor`, `recusarPedidoDeAjusteComoGestor` |
| 7.1 Aba Férias da ficha da equipe (sem print) | Gestor | `obterFeriasDaPessoaDaEquipe` |
| 7.2 Pedidos de férias para o RH (sem print) | RH | `listarFeriasParaORh`, `confirmarFeriasComoRh`, `recusarFeriasComoRh`; de outra fatia: `listarSetores` (filtro) |
| 7.3 Pedidos de benefícios para o RH (sem print) | RH | `listarPedidosDeBeneficio`, `aprovarAdesaoABeneficio`, `recusarAdesaoABeneficio`, `aprovarDependenteDeBeneficio`, `recusarDependenteDeBeneficio`, `aprovarTrajetoDeBeneficio`, `recusarTrajetoDeBeneficio`; de outra fatia: `listarBeneficiosDoCatalogo` (filtro) |
| 7.4 Pagamento (sem print) | RH | `obterCalendarioDePagamento`, `listarExcecoesDePagamento`, `definirExcecaoDePagamento`, `removerExcecaoDePagamento`; de outra fatia: `obterEmpresa` (o dia da regra) |
| 7.5 Pendências do RH (sem print) | RH | `listarPendenciasDoRh` |
| 7.6 Pessoas sem registro hoje (sem print) | RH | `listarPessoasSemRegistroHoje` |
| Cartão "Próximo pagamento" (um componente só, props `{ data, mesReferencia }`) | todos | telas **32 e 46**: `obterPainelDoFuncionario.proximoPagamento` (o painel já o traz; **não** chamam `obterProximoPagamento`); telas **36 e 49** (fatia 4): `obterProximoPagamento` (não têm painel) |

Das **41 rotas** da fatia, 40 aparecem acima como rota de tela; a 41ª, `listarFeriasDaEquipe`, **não tem tela** (ver a tela 51, "Observações"). Na tabela acima, **`obterProximoPagamento` é chamada só pelas telas 36 e 49 (fatia 4)**: as telas 32 e 46 leem o mesmo dado do painel. O teste de cobertura da ficha 7 conta, além das 40 com tela, `listarEquipeDoGestor` (tela 51) e `listarAniversariantes` (telas 32 e 46), que são de outras fatias (ver a ficha, seções 3 e 6).

## Regras comuns a todas as telas desta fatia

**Datas.** `date` (`2026-11-16`) é o dia de calendário no fuso de São Paulo: mostre `16/11/2026` **sem converter fuso**. Nunca faça `new Date('2026-11-16')` (o JavaScript lê como meia-noite UTC e o dia aparece como 15). `date-time` (`...Z`) é um instante em UTC: mostre em `America/Sao_Paulo` ("Hoje, 09:38", "Ontem, 17:55"; para dia mais antigo, `dd/mm, HH:mm`: suponho). "Quarta-feira, 7 de outubro" (li, PNG 00 e 32) é a data de hoje por extenso, dia sem zero à esquerda.

**Dias e horas.** "1 dia", "15 dias" (singular só no 1). Banco de horas: `bancoDeHorasMinutos` vira `+2h10`, `−1h30` (sinal de menos tipográfico, como no PNG 51) e `0h00`; use `formatarMinutosComoHoras(min, { sinal: true })` da 1a (`src\shared\formato\numeros.ts`; `fatia-1a-fundacao.md`, Task 2) e `plural` (`texto.ts`): **já existem, não os recrie**; se faltar um formato que a tela precisa (a 1a não fixa o zero com `sinal`: aqui ele é `0h00`, sem sinal), acrescente-o **ao mesmo arquivo** e ao teste dele.

**Rótulo da situação de um pedido de férias** (o campo é `situacao`; o `status` do banco e a `etapa` vêm junto e **não** se mostram como texto):

| `situacao` | Rótulo |
|---|---|
| `COM_O_GESTOR` | Aguardando o gestor (suponho) |
| `SUGESTAO_DE_DATA` | Outra data sugerida (suponho) |
| `COM_O_RH` | Aguardando o RH (suponho) |
| `APROVADA` | Aprovada (suponho) |
| `EM_GOZO` | Em gozo (suponho) |
| `CONCLUIDA` | Concluída (o Figma escreve "Concluídas", no plural, nos PNG 05, 34 e 48; suponho que seja descuido) |
| `REJEITADA` | Recusada (suponho; o valor do banco é `REJEITADA`, o texto da tela é "Recusada") |
| `CANCELADA` | Cancelada (suponho) |

**Como mostrar a decisão de uma etapa** (`decisaoGestor` e `decisaoRh`, schema `DecisaoDeEtapa`; frases minhas, suponho; o **motivo da recusa é obrigatório mostrar**, o contrato diz que a pessoa o vê):
- `APROVADA` e `automatica: false`: "Aprovado pelo gestor {decididaPor.nome} em {dd/mm}" (RH: "Confirmado pelo RH ({nome}) em {dd/mm}").
- `APROVADA` e `automatica: true` (o RH ligou "Gestor ausente", `CONVENCOES.md` seção 9; `decididaPor` vem `null`): "Aprovado automaticamente (gestor ausente)".
- `REJEITADA`: "Recusado por {nome} em {dd/mm}: {observacao}".
- `decisaoGestor` `null` com `gestorResponsavel` `null` e etapa do RH: "Este pedido não passa pelo gestor" (pedido de quem é gestor, de setor sem gestor ou de gestor desligado).
- `situacao: SUGESTAO_DE_DATA`: a sugestão está em `sugestao` (não em `decisaoGestor`): "{sugeridaPor.nome} sugeriu outras datas ({dd/mm} a {dd/mm}) e espera a resposta de {nome}".
- `CANCELADA` com `sugestao.respostaDaPessoa` `RECUSOU` ou `EXPIROU`: "Cancelado: a pessoa recusou as datas sugeridas" ou "Cancelado: a sugestão de datas expirou".

**Botões que decidem** (gestor e RH): habilitados só com `podeDecidir: true`. Com `false`, ficam desabilitados e mostram `motivoNaoPodeDecidir` (texto já pronto; use-o como veio). Os textos possíveis estão em `70-fatia-7-gestao.yaml`, schema `FeriasPedido`, propriedade `motivoNaoPodeDecidir`: "Aguardando a análise do gestor.", "Aguardando a análise do RH.", "Este pedido já foi decidido.", "Você não pode decidir o seu próprio pedido.", "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH." e, só nas férias, "Esperando a resposta de {nome} à sugestão de outra data."

**Erros que se repetem** (todo erro é `application/problem+json`: mostre o `detail`, já em português; o `codigo` serve para decidir o que fazer):

| Código HTTP e `codigo` | O que a tela faz |
|---|---|
| 400 (campos em `errors`) | Marca cada campo com a mensagem dele. |
| 401 | A fundação trata (renovar a sessão ou voltar ao login). |
| 403 "Você não tem permissão para esta ação." | Mostra a mensagem; não deve acontecer com o menu certo. |
| 404 "O registro não foi encontrado." | Para pedido ou pessoa: some da lista e atualiza a tela. |
| 409 `PEDIDO_ETAPA_INCORRETA`, `PEDIDO_JA_DECIDIDO` (férias) | Mostra o `detail` ("Este pedido ainda não chegou à sua etapa ou já passou por ela." / "Este pedido já foi decidido.") e **recarrega** a lista: outra pessoa decidiu antes. |
| 409 `BENEFICIO_JA_ATIVO_OU_PENDENTE`, `BENEFICIO_ADESAO_NAO_ATIVA`, `BENEFICIO_PEDIDO_JA_DECIDIDO` | Mostra o `detail` e recarrega. |
| 422 `FERIAS_REGRAS_NAO_ATENDIDAS` | Mostra a lista `regras` que vier (no exemplo do contrato vêm só as regras citadas, não as oito) e mantém o formulário aberto. |
| 422 `FERIAS_SEM_PERIODO_AQUISITIVO`, `FUNCIONARIO_INATIVO` | Mostra o `detail`. |
| 422 `PEDIDO_PROPRIO`, `PEDIDO_RH_FORA_DE_GESTORES` (RH decidindo férias) | Mostra o `title` ("Você não pode decidir o próprio pedido." / "Quem decide precisa ser gestor também.") e, abaixo, o `detail` (no primeiro caso o `detail` é genérico: "A operação foi recusada por uma regra do banco de dados."). |
| 422 `BENEFICIO_NAO_OPCIONAL`, `BENEFICIO_NAO_ACEITA_DEPENDENTES`, `BENEFICIO_NAO_ACEITA_TRAJETO` | Mostra o `detail`. |
| 500 | "Erro no servidor. Tente novamente em instantes." |

**E-mails.** A lista de e-mails do sistema é **fechada** (`CONVENCOES.md`, seção 12). Nesta fatia a pessoa recebe e-mail **a cada decisão de férias** (gestor, RH, sugestão de outra data); **não** recebe e-mail de pedido criado, de férias registradas pelo RH, nem de resposta de benefício. Todo texto do Figma que promete e-mail fora dessa lista **muda** (cada tela abaixo diz qual).

**Dados sensíveis.** O trajeto do vale-transporte (`origem` costuma ser o endereço de casa) e os dependentes só aparecem para a própria pessoa e para o RH. Não os escreva no console, em `localStorage` nem na URL.

---

## Tela 00: Painel inicial (RH)

PNG: `Figma\telas\00-rh-painel-inicial.png`. É o "Painel" do menu do RH. Incremento 7d.

**O que fica (li no PNG 00)**
- Cabeçalho: "Bom dia, Ana" (primeiro nome de `obterUsuarioLogado`; suponho que a saudação siga a hora: Bom dia, Boa tarde, Boa noite) e a linha "Quarta-feira, 7 de outubro. Há 10 pedidos esperando revisão." (`totalPendencias`; com 1: "Há 1 pedido esperando revisão"; com 0: "Nenhum pedido esperando revisão", suponho).
- Ações do topo: campo "Buscar colaborador" (lupa) e os botões "Publicar aviso" (contorno) e "Adicionar colaborador" (cheio). Os três usam telas das fatias 1 e 4: a busca leva à lista de colaboradores com o texto em `busca` de `listarFuncionarios` (suponho: ao teclar Enter); "Publicar aviso" abre o novo aviso (tela 28, fatia 4); "Adicionar colaborador" abre o cadastro (tela 02, fatia 1).
- **"Precisa da sua atenção"** com o link "Ver todas as pendências" (abre a tela 7.5). Um cartão por tipo de `pendencias`, **só os de `quantidade` maior que zero**, na ordem que a API manda: o número grande (`quantidade`), o título do tipo, a frase `resumo` (pronta; se vier `null`, não mostre a linha) e o botão "Revisar". Títulos (li): `AJUSTE_DE_PONTO` "Pedidos de ajuste de ponto"; `FERIAS` "Pedidos de férias"; `ATESTADO` "Atestados para validar"; `ADMISSAO_COM_DOCUMENTOS_PENDENTES` "Admissão com documentos pendentes"; `BENEFICIOS` "Pedidos de benefícios" (suponho: o Figma não desenha; ver "O que sai ou muda").
- Para onde vai "Revisar" (`PainelPendenciaTipo`, no contrato): ajuste de ponto, à tela 12 com o modal da tela 13 (fatia 3), destacando o pedido `referenciaId`; férias, à tela 7.2; atestado, à lista de atestados do RH (fatia 3); admissão, à ficha da pessoa `funcionarioId` (fatia 1, tela 03); benefícios, à tela 7.3. Os endereços exatos de cada destino estão na seção "De onde cada tela abre" de `05-caminhos-de-tela.md` (por exemplo, ajuste de ponto: `/rh/ponto-e-feriados?pedido=<referenciaId>`).
- **"Próximos dias"**: cada linha tem o dia grande e o mês abreviado ("12" e "out"), o `rotulo` pequeno ("Feriado nacional", "Aniversário") e o `titulo` ("Nossa Senhora Aparecida", "Marina Costa, Financeiro"). A API manda até 8, em ordem de data, nos próximos 21 dias.
- **"Treinamentos em andamento"**: por treinamento, o `titulo`, o texto "18 de 24 concluíram" (`concluiram` de `total`) e uma barra proporcional (`concluiram` ÷ `total`; com `total` 0, barra vazia). Até 5. Link "Gerenciar treinamentos" (tela 22, fatia 6).
- **"Presença agora"** (cartão de fundo verde-claro): ver "Presença agora" abaixo.
- **"Últimas alterações"** com o link "Abrir log de auditoria" (tela 31, fatia 1): tabela com as colunas Quando, Quem e O que aconteceu, 4 linhas de `ultimasAlteracoes` (`quandoUtc`, `autor`, `descricao`; os dois textos já vêm prontos, mostre-os como texto, nunca como HTML).
- No menu lateral, a bolinha de "Ponto e feriados" (4 no PNG) vem de `obterContadoresDoMenuDoRh.pedidosDeAjusteDePonto`: é o mesmo número do cartão de ajuste de ponto.

**"Presença agora" (regra do contrato, `CONVENCOES.md` seção 10)**
- Título "Presença agora" e, à direita, "Atualizado às 9:42" (`atualizadoEmUtc` em São Paulo, formato `H:mm`).
- Número grande = `contagensHoje.trabalhando`; ao lado, "de {total} colaboradores registraram entrada hoje". `total` é a soma de trabalhando, férias, atestado, sem registro e ainda não começou.
- Barra com um trecho por situação, na ordem: Trabalhando, Férias, Atestado, Sem registro, e Ainda não começou (este último **só se `aindaNaoComecou` for maior que zero**; o Figma não tem esse rótulo). Legenda com o nome e o número de cada um. Com `total` 0, não desenhe a barra.
- Link "Ver quem está sem registro" (abre a tela 7.6); suponho que ele só apareça com `semRegistro` maior que zero.
- **Dia sem expediente** (`diaUtil: false`): o cartão mostra só "Hoje não há expediente" e, se `trabalhando` for maior que zero, uma linha com esse número ("{n} colaboradores registraram entrada hoje", suponho).

**O que sai ou muda (e por quê)**
- **Sai a linha "Folga ou compensação"** (li no PNG 00, 3 pessoas): decisão do dono em 2026-10-30 (design `2026-10-30-planos-backend-frontend-design.md`, seção 7) e `obterPainelDoRh`. A contagem muda junto: o Figma mostra "Sem registro 4" mais "Folga 3"; o contrato (e `ELENCO.md`) mostra **Sem registro 7**, e a soma continua 101. Não é erro: mostre o que a API manda.
- **Saem "Fim do período de experiência" (20/10, Lucas Prado) e "Treinamento presencial" (23/10, turma 2)** em "Próximos dias" (li no PNG 00): cortados (sem período de experiência, decisão de 2026-10-28; sem turmas presenciais, 2026-10-29; spec seção 13.3). A API não os envia; o front **não** os recria. O contrato traz só `FERIADO` e `ANIVERSARIO`, e o exemplo tem 5 linhas, não 4.
- **"Presença agora"** segue `SituacaoHoje` (definição única do contrato). O roadmap lista a definição como decisão do dono ainda aberta (fatia 7, "Decisões do dono"); o contrato adotou esta por escolha de engenharia (`CONVENCOES.md` seção 10). Construa pela do contrato.
- **Tipo `BENEFICIOS`** em "Precisa da sua atenção": acrescentado pelo contrato (o Figma desenha quatro tipos). É `x-provisorio` (ver abaixo).
- **Frases das linhas de "Últimas alterações"** são do log de auditoria (rota da fatia 1, `apenasAlteracoes`); o front não escolhe quais eventos entram nem reescreve a frase. Uma linha do Figma ("Sistema: Enviou por e-mail um lembrete do formulário de admissão...") é só exemplo: mostre o que vier.

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterPainelDoRh` | ao abrir a tela | `PainelRh` (tudo acima, exceto a bolinha do menu e a saudação) |
| `obterContadoresDoMenuDoRh` | no menu, a cada navegação | `PainelContadoresRh` |
| `obterUsuarioLogado` (fatia 0) | saudação | nome |
| `listarFuncionarios` (fatia 1) | busca do topo | lista com `busca` |

**Estados**
- Carregando: esqueleto por bloco (suponho: um bloco que falha não derruba os outros). Vazio: sem pendências, mostre "Nada esperando revisão" no lugar do bloco; sem treinamentos, alterações ou próximos dias, uma frase curta em cada (suponho). Erro: mensagem por bloco com "Tentar de novo" (suponho). 403: só se o perfil estiver errado (rota só do RH).

**Observações para quem monta**
- Os dados não têm polling, e a fundação desliga `refetchOnWindowFocus` (`criarQueryClient`, 1a): **não** recarregue ao voltar o foco para a janela. Peça os dados de novo com `invalidarRota(queryClient, '/api/rh/painel')` (e `invalidarContadoresDoMenu`) depois de qualquer decisão feita nesta ou em outra tela.
- O cartão "Presença agora" é o mais denso em 1366 px; veja a ficha, "Riscos".

**Perguntas abertas (do `PROVISORIOS.md`)**: `paths > /api/rh/painel > get` e `/api/rh/painel/pendencias`: pedidos de benefício contam como pendência do RH? Construa com o tipo `BENEFICIOS`; se o dono disser não, ele sai de `pendencias` e a tela não muda.

---

## Tela 05: Ficha do colaborador, aba Férias (RH)

PNG: `Figma\telas\05-rh-colaboradores-ficha-aba-ferias.png`. A ficha é o painel lateral da fatia 1 (cabeçalho com nome, cargo, setor, matrícula, situação e botões "Editar dados", "Inativar" e "..."; abas Dados, Ponto, Férias, Holerites, Documentos, Advertências). **Esta nota cobre só o conteúdo da aba Férias.** Incremento 7a.

**O que fica (li no PNG 05)**
- Três cartões de resumo (de `resumo`): "Dias disponíveis" (`diasDisponiveis`, "18 dias"); "Período aquisitivo atual" (`periodoAquisitivoAtual`, "14/03/2026 a 13/03/2027"; `null`, mostre "—"); "Prazo para tirar o saldo" (`prazoParaTirarSaldo`, "13/03/2027"; `null`, "—").
- Título "Períodos de férias" e, à direita, o botão "+ Registrar férias" (abre a tela 06).
- Tabela com as colunas **Período** ("05/01/2026 a 16/01/2026"), **Dias** ("12 dias") e **Situação** (etiqueta; rótulos na seção "Regras comuns"). Uma linha por pedido de `ferias`, do mais novo para o mais antigo (a ordem da API).
- Rodapé: "Pedidos feitos pelo colaborador aparecem aqui depois que o gestor analisa."

**O que sai ou muda (e por quê)**
- A lista **não traz** os pedidos ainda `COM_O_GESTOR` ou em `SUGESTAO_DE_DATA` (é o que o rodapé diz; contrato). Por isso `resumo.pedidosEmAnalise` pode ser maior que o número de linhas de pedidos em análise: o Figma só desenha três números, **não** mostre `pedidosEmAnalise`, `proximasFerias` nem `periodosAquisitivos` nesta aba (suponho).
- O Figma tem 3 colunas; o contrato traz mais (decisões, abono, 13º). Fique nas 3 colunas. Opcional (suponho): clicar na linha abre os detalhes com a frase de cada decisão (seção "Regras comuns"), com o **motivo da recusa** quando houver.
- Pessoa **inativa**: o botão "Registrar férias" fica escondido (a API recusaria com 422 `FUNCIONARIO_INATIVO`; suponho).

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterFeriasDaFichaDoFuncionario` | ao abrir a aba | `FeriasFicha` (`resumo`, `periodosAquisitivos`, `ferias`) |

**Estados**: carregando; sem nenhum pedido: "Nenhum período de férias registrado ainda." (suponho); erro e 404 (a pessoa não existe); 403 (só RH).

**Observações**: depois de registrar férias (tela 06), recarregue esta aba e os indicadores da ficha (`obterIndicadoresDoFuncionario`, fatia 1, cartão "Férias disponíveis").

---

## Tela 06: Modal "Registrar férias de {nome}" (RH)

PNG: `Figma\telas\06-rh-colaboradores-ficha-modal-registrar-ferias.png`. Incremento 7a. Ação direta do RH: o pedido **não** passa pelo gestor nem pela confirmação e já nasce `APROVADA` (contrato, `registrarFeriasPeloRh`; decisão do dono, 2026-10-29).

**O que fica (li no PNG 06)**
- Título "Registrar férias de Bruno" (primeiro nome).
- Três números: "Saldo disponível" ("18 dias"), "Período aquisitivo" e "Prazo para tirar" ("13/03/2027"), tirados do `resumo` que a aba já carregou (sem chamada nova).
- Campos "Primeiro dia" e "Último dia" (datas) e "Total" ("18 dias corridos": `totalDias` da conferência; nesta tela **não** se mostra "Sobram").
- Caixas "Vender parte das férias (abono de até 10 dias)" e "Adiantar a 1ª parcela do 13º salário".
- Lista de verificações com visto (as `regras` da conferência; ver "O que muda").
- Aviso laranja "O pagamento das férias precisa sair até 2 dias antes do início, ou seja, até 05/12": a data vem de `pagamentoAte` (`null`, esconda o aviso).
- "Observação (opcional)" (até 500 caracteres; texto livre, nunca HTML), com o exemplo "Ex.: combinado com a gestora em 06/10".
- Rodapé "As férias aparecem na aba Férias e no ponto como dias de férias." e os botões "Cancelar" e "Registrar férias" (este só habilita com `podeEnviar: true`).

**O que sai ou muda (e por quê)**
- **Sai a caixa "Avisar Bruno e o gestor por e-mail"** (li no PNG 06): não existe e-mail de férias registradas pelo RH (`CONVENCOES.md` seção 12; contrato `registrarFeriasPeloRh`). A decisão de 2026-10-30 vale mais que a linha 313 do spec, que ainda lista esse e-mail.
- **Aparece o campo "Quantos dias" (1 a 10)** quando "Vender parte..." está marcada: o Figma desenha só a caixa, o banco exige o número. `x-provisorio` de `FeriasConferenciaComando.diasAbono`: o dono pode preferir um valor fixo (10); se mudar, o campo some.
- **O segundo número deve ser `resumo.periodoDoSaldo`, não o período em andamento.** O PNG mostra "14/03/2026 a 13/03/2027" (o período em andamento, o mesmo de `periodoAquisitivoAtual` da tela 05), mas o saldo de 18 dias é do período mais antigo com saldo (tela 34: "Do período 2025 a 2026"), e as datas do próprio print (07/12 a 24/12/2026) cairiam **antes** do fim do período em andamento, o que o banco proíbe ("início depois do fim do período aquisitivo", contrato `pedirFerias`). O servidor também escolhe sozinho o período (o mais antigo com saldo; não há campo para escolher). Mostre `periodoDoSaldo` (formato "14/03/2025 a 13/03/2026"); o rótulo "Período aquisitivo" fica (suponho). Registre a conferência visual como "contradição do Figma resolvida pelo contrato".
- **A lista de verificações é a que a API devolve**, não as três do PNG ("Cabe no saldo disponível", "Tem pelo menos 14 dias seguidos", "Não começa nos 2 dias antes de um feriado ou do descanso semanal"). O contrato manda mostrar todas as `regras` com o texto pronto (`mensagem`) e `atendida` (visto ou aviso). Aqui **não** há a de antecedência de 30 dias (o RH pode registrar férias já combinadas, até no passado).
- O campo de data do Figma aparece no formato dos EUA (mm/dd/aaaa) por ser o navegador de quem fez o print; use o controle de data do navegador em pt-BR (dd/mm/aaaa).

**Rotas**

| Rota | Quando | Corpo e resposta |
|---|---|---|
| `conferirRegistroDeFerias` (`POST /api/rh/funcionarios/{funcionarioId}/ferias/conferencia`) | a cada mudança de data ou de caixa, **só com as duas datas preenchidas** e fim não anterior ao início | corpo `{ dataInicio, dataFim, abonoPecuniario, diasAbono }`; 200 `FeriasConferencia` (sempre 200, mesmo com regra não atendida) |
| `registrarFeriasPeloRh` (`POST /api/rh/funcionarios/{funcionarioId}/ferias`) | botão "Registrar férias" | corpo `{ dataInicio, dataFim, abonoPecuniario, diasAbono, adiantar13Salario, observacao }`; 201 `FeriasPedido`; 400; 404; 422 `FERIAS_REGRAS_NAO_ATENDIDAS`, `FERIAS_SEM_PERIODO_AQUISITIVO`, `FUNCIONARIO_INATIVO` |

**Estados**: conferência em andamento (botão desabilitado, lista com carregando); sem datas (lista vazia, botão desabilitado); 422 (mostra as regras que vierem e o `detail`); sucesso (fecha o modal, aba recarrega, aviso "Férias registradas." suponho).

**Observações**
- **O front não reimplementa nenhuma regra de férias**: valida só que há duas datas e que o fim não vem antes do início, e deixa o resto com a API. Descarte respostas antigas (se o RH muda a data duas vezes seguidas, vale a última resposta) e espere uns 300 ms depois de digitar (suponho).
- **Altura**: o modal tem uns 900 px no PNG (li: do topo em 24 px até 929 px). Num projetor de 1366×768 não cabe: o corpo precisa rolar por dentro e o rodapé com os botões deve ficar sempre visível.

**Perguntas abertas (`PROVISORIOS.md`)**: `/api/rh/funcionarios/{funcionarioId}/ferias > post`, `.../ferias/conferencia > post`, `FeriasRegraCodigo` e `FeriasConferencia`: as regras de férias do protótipo (partes, abono, 13º, antecedência, 2 dias antes de feriado) ainda dependem do dono. Se mudarem, mudam os códigos e as mensagens; a tela não muda. `FeriasConferenciaComando.diasAbono`: ver acima.

---

## Tela 32: Tela inicial (funcionário)

PNG: `Figma\telas\32-func-tela-inicial.png`. É o "Início". Incremento 7d (completa o cartão "Seu ponto hoje", que nasceu solto na fatia 3: `roadmap`, "Dependências cruzadas").

**O que fica (li no PNG 32)**
- "Olá, Bruno" (`obterUsuarioLogado`) e "Quarta-feira, 7 de outubro".
- **"Seu ponto hoje"** (cartão grande, "Jornada das 08:00 às 17:48", relógio, quatro caixas Entrada, Saída almoço, Volta e Saída, botão de registrar, "Pedir ajuste de ponto", "Enviar atestado") e o cartão **"Banco de horas"** ("+6h20", "Atualizado até ontem", "Ver extrato"): **são da fatia 3** (`obterPontoHoje`); aqui só se encaixa o componente que a fatia 3 já fez.
- **"Férias disponíveis"** ("18 dias", "Vencem em {data}", link "Pedir férias" que abre a tela 35): `obterPainelDoFuncionario.ferias` (`diasDisponiveis`, `prazoParaTirarSaldo`; com `null`, esconda "Vencem em").
- **"Próximo pagamento"** ("6 de novembro", "Holerite de outubro", link "Ver holerites" para a tela 36): `obterPainelDoFuncionario.proximoPagamento` (`PagamentoProximo`: `pagamento.data` e `competencia`, o mês **anterior** ao do pagamento). Com `pagamento` `null` (empresa sem dia de pagamento), **esconda o cartão**. É **o mesmo componente** das telas 36 e 49 (fatia 4), com props `{ data, mesReferencia }` (`pagamento.data` e `competencia.mes`); aqui ele é alimentado pelo painel, e **a tela 32 não chama `obterProximoPagamento`**. A data vai por `formatarDiaEMes` (1b; "6 de novembro").
- Faixa laranja "Seu pedido de ajuste da saída de 02/10 está em análise pelo RH. Você recebe um e-mail quando houver resposta." com "Ver pedido": vem de `listarMeusPedidosDeAjuste` (fatia 3; suponho que seja o pedido em análise mais recente); o texto exato é o da nota de tela da fatia 3. Sem pedido em análise, a faixa não aparece.
- **"Seus benefícios"**: uma linha por item de `obterPainelDoFuncionario.beneficios` (`nome` e `detalhe`), na ordem do catálogo, e o link "Ver detalhes dos benefícios" (tela 44).
- **"Avisos"**: os dois mais recentes de `listarMeusAvisos` (título, trecho, "Hoje, publicado pelo RH", "Ler aviso completo", que abre a tela 38; fatia 4).
- **"Aniversariantes"**: "Ninguém faz aniversário hoje. Nos próximos 14 dias:" e a lista (iniciais, nome, setor, `dd/mm`), de `listarAniversariantes` (fatia 4).
- Menu "Seu dia a dia" (Início, Meu ponto, Férias, Holerites), "Comunicação" (Avisos, Formulários) e "Você na empresa" (Treinamentos, Advertências, Benefícios), com as bolinhas de `obterContadoresDoMenuDoFuncionario`: Avisos 2 (`avisosNaoLidos`), Formulários 1 (`formulariosPendentes`) e, no item "Meus documentos" (`documentosPendentes`; item que o Figma não desenha, decisão 14 do roadmap).

**O que sai ou muda (e por quê)**
- "Crédito todo dia 1º", "Ida e volta" e "Ativo desde 2022" (li no PNG 32) **não existem no contrato**: `detalhe` só traz "Você e 1 dependente" (dependentes aprovados) e as linhas do trajeto do vale-transporte ("Metrô Linha 1 e Linha 2" no exemplo); para os demais é `null` e a linha mostra só o nome.
- "Vencem em 14/03/2027" no PNG 32 e 46, mas 13/03/2027 nos PNG 05, 06, 34 e 35: o contrato diz 13/03 (12 meses depois do fim do período) e anota a divergência (`PainelFeriasResumo`). Mostre o que a API manda.
- "Você recebe um e-mail quando houver resposta" está certo (o pedido de ajuste manda e-mail a cada decisão).
- Contador do menu: o PNG mostra Avisos 2 e Formulários 1; o valor real vem da API.

**Rotas**

| Rota | Quando |
|---|---|
| `obterPainelDoFuncionario` | cartões "Férias disponíveis", "Próximo pagamento" e "Seus benefícios" |
| `obterContadoresDoMenuDoFuncionario` | bolinhas do menu (leve; a cada navegação) |
| `obterUsuarioLogado`, `obterPontoHoje`, `listarMeusPedidosDeAjuste`, `listarMeusAvisos`, `listarAniversariantes` (fatias 0, 3 e 4) | as demais partes, **em paralelo** |

**Estados**: cada cartão carrega e falha sozinho (suponho). Sem benefícios ativos: "Você ainda não tem benefícios ativos." (suponho). Sem avisos: "Nenhum aviso por enquanto." (suponho).

**Observações**: o contrato já separa os cartões por assunto de propósito (cada fatia é dona da sua rota); **não** espere uma rota única. O menu e as bolinhas são do shell da fundação (1a): ela já traz `useContadoresDoMenu`, que lê `contadores-do-menu`; os valores reais chegam quando a API existir. Esta tela não cria nem troca nada nas bolinhas.

---

## Tela 34: Férias (funcionário)

PNG: `Figma\telas\34-func-ferias.png`. Incremento 7a. O gestor e o RH usam **a mesma rota** (`obterMinhasFerias`) para as férias **deles**.

**O que fica (li no PNG 34)**
- Título "Férias", subtítulo "Seu saldo, seus períodos e seus pedidos" e botão "+ Pedir férias" (abre a tela 35).
- **Quatro números** (de `resumo`): "Dias disponíveis" (`diasDisponiveis`; embaixo "Do período 2025 a 2026", dos anos de `periodoDoSaldo`; `null`, sem a linha); "Prazo para tirar esse saldo" (`prazoParaTirarSaldo`); "Próximas férias" (`proximasFerias`: "dd/mm a dd/mm" e "N dias"; `null`: "Nenhuma" e "Nada agendado ainda"); "Pedidos em análise" (`pedidosEmAnalise`).
- **"Seus períodos aquisitivos"** (`periodosAquisitivos`, do mais antigo ao mais novo): por período, "14/03/2025 a 13/03/2026" e:
  - `COM_SALDO` ou `SEM_SALDO`: barra com os dias tirados (escuro) e disponíveis (claro), legenda "12 dias tirados" e "18 dias disponíveis", e a etiqueta "Tire até 13/03/2027" (`prazoParaTirar`; suponho que a etiqueta só apareça quando há dias disponíveis).
  - `EM_AQUISICAO`: etiqueta "Em andamento", barra listrada proporcional a `mesesTrabalhados` de 12 e o texto "7 de 12 meses trabalhados. Os 30 dias desse período ficam disponíveis a partir de 14/03/2027." (`disponivelAPartirDe`).
- **"Histórico"**: tabela **Período**, **Dias**, **Pedido em** (`solicitadaEmUtc`, só a data) e **Situação** (etiqueta), uma linha por pedido de `historico` (do mais novo para o mais antigo, sem paginação).
- **"Como funciona"** (cartão fixo): "Você pode dividir as férias em até 3 partes: uma de pelo menos 14 dias e as outras de pelo menos 5 dias."; "Dá para vender até 10 dias (abono) e pedir o adiantamento da 1ª parcela do 13º."; "As férias não podem começar nos 2 dias antes de um feriado ou do fim de semana."; e "Peça com pelo menos 30 dias de antecedência. {nome} analisa e depois o RH confirma." Guarde esse texto num lugar só: as regras de férias são `x-provisorio` e podem mudar.

**O que sai ou muda (e por quê)**
- **Última linha de "Como funciona"**: `gestorQueAnalisa` traz o nome de quem faz a primeira etapa; quando vem `null` (a pessoa é gestora, não tem gestor ativo ou tem o gestor desligado) o texto é **"O RH analisa o pedido."** (contrato, `obterMinhasFerias`). O PNG 48 (gestor Carlos Ribeiro) escreve "Roberto Alves analisa e depois o RH confirma": isso contraria a regra (pedido de gestor só o RH decide) e **não vale**.
- **O motivo de uma recusa precisa aparecer** (o Figma não tem onde): na linha do histórico, uma segunda linha com a frase da decisão (ver "Regras comuns"), por exemplo "Recusado pelo gestor Carlos Ribeiro em 08/10: {motivo}".
- **Pedido com `situacao: SUGESTAO_DE_DATA`** (o gestor sugeriu outras datas; o Figma não desenha): mostre, acima do Histórico, um bloco destacado "{nome do gestor} sugeriu outras datas: {dd/mm} a {dd/mm} ({dias} dias). Responda até {expiraEm}." (suponho a frase) com os botões **"Aceitar as novas datas"** (`aceitarSugestaoDeDataDasFerias`) e **"Recusar"** (`recusarSugestaoDeDataDasFerias`). Depois de recusar, mostre exatamente: "Seu pedido foi encerrado. Converse com o seu gestor antes de abrir outro pedido de férias." (texto do contrato). Aceitar pode dar 422 (as datas novas não cabem no saldo): mostre o `detail` e as `regras`. Esses dias **não** estão reservados enquanto a pessoa não aceita.
- **Dias reservados**: os dias de pedidos que esperam resposta (`COM_O_GESTOR` e `COM_O_RH`) continuam dentro de `diasDisponiveis`, mas não podem ser pedidos de novo. O Figma não desenha isso; sugestão (suponho): na legenda do período, acrescente "(N reservados em pedido)" quando `diasEmPedido` for maior que zero.
- "Concluídas": ver "Regras comuns" (rótulos).

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterMinhasFerias` | ao abrir a tela e depois de cada ação | `FeriasEuResposta` (`resumo`, `gestorQueAnalisa`, `periodosAquisitivos`, `historico`) |
| `aceitarSugestaoDeDataDasFerias` (`POST /api/eu/ferias/{feriasId}/sugestao/aceitar`) | botão "Aceitar as novas datas" | 200 `FeriasPedido`; 404; 409 `PEDIDO_ETAPA_INCORRETA` ou `PEDIDO_JA_DECIDIDO`; 422 |
| `recusarSugestaoDeDataDasFerias` (`POST /api/eu/ferias/{feriasId}/sugestao/recusar`) | botão "Recusar" | 200 `FeriasPedido` (vira `CANCELADA`); 404; 409 |

**Estados**: carregando; **sem pedidos**: o Histórico mostra "Você ainda não pediu férias." (suponho); **sem período fechado** (primeiro ano de casa): "Dias disponíveis" 0 e o texto de `EM_AQUISICAO`; o botão "Pedir férias" continua habilitado e a API responde 422 `FERIAS_SEM_PERIODO_AQUISITIVO` (não reimplemente a regra); erro.

**Perguntas abertas (`PROVISORIOS.md`)**: `FeriasSugestao.expiraEm` (prazo da sugestão sem resposta; proposta: 5 dias corridos ou 2 dias antes da data sugerida): a tela mostra a data que vier. As regras do cartão "Como funciona": ver tela 06.

---

## Tela 35: Painel lateral "Pedir férias" (funcionário)

PNG: `Figma\telas\35-func-ferias-pedir-ferias.png`. Painel que abre da direita sobre a tela 34. Incremento 7a. Fluxo: o pedido nasce com o gestor, depois o RH confirma (`pedirFerias`).

**O que fica (li no PNG 35)**
- Título "Pedir férias" e o aviso "Você tem 18 dias disponíveis para tirar até 13/03/2027." (de `resumo.diasDisponiveis` e `prazoParaTirarSaldo` já carregados pela tela 34).
- Campos "Primeiro dia" e "Último dia" (datas) e "Total": "15 dias corridos. Sobram 3 dias." (`totalDias` e `diasQueSobram` da conferência; se `diasQueSobram` for negativo, suponho "Passa 4 dias do saldo.").
- Caixas "Vender parte das férias (até 10 dias)" (com o campo "Quantos dias", 1 a 10, quando marcada) e "Adiantar a 1ª parcela do 13º salário".
- Lista de verificações com visto (`regras`).
- "Mensagem para Marina Costa (opcional)" (até 500 caracteres; exemplo "Ex.: queria emendar com o feriado de 20/11"): o corpo leva em `mensagem`. Sem gestor na primeira etapa (`gestorQueAnalisa` `null`), o rótulo é "Mensagem para o RH (opcional)" (suponho).
- Rodapé "Marina Costa analisa primeiro e depois o RH confirma. Você recebe um e-mail em cada etapa." e os botões "Cancelar" e "Enviar pedido" (habilitado só com `podeEnviar: true`).

**O que sai ou muda (e por quê)**
- **Rodapé**: criar o pedido **não** manda e-mail; a pessoa recebe e-mail a **cada decisão** (gestor, RH, sugestão de outra data); a aprovação automática do "Gestor ausente" não manda (`CONVENCOES.md` seção 12). Texto sugerido (suponho): "{gestor} analisa primeiro e depois o RH confirma. Você recebe um e-mail a cada decisão." Sem gestor na primeira etapa: "O RH analisa o pedido. Você recebe um e-mail a cada decisão."
- **Lista de verificações**: o PNG mostra 4 ("Cabe no seu saldo", "Tem pelo menos 14 dias seguidos", "Começa daqui a mais de 30 dias", "Não começa nos 2 dias antes de um feriado ou do fim de semana"). O contrato devolve até 8 regras (`FeriasRegraCodigo`); mostre **todas as que vierem**, com o mesmo visual (visto quando `atendida`, aviso quando não). O front não decide quais regras existem.
- Os campos de data do print estão no formato dos EUA; use dd/mm/aaaa.
- O campo "Quantos dias" e a conferência: ver tela 06 (`x-provisorio`).

**Rotas**

| Rota | Quando | Corpo e resposta |
|---|---|---|
| `conferirPedidoDeFerias` (`POST /api/eu/ferias/conferencia`) | a cada mudança de data ou de caixa, só com as duas datas | `{ dataInicio, dataFim, abonoPecuniario, diasAbono }`; 200 `FeriasConferencia` |
| `pedirFerias` (`POST /api/eu/ferias`) | botão "Enviar pedido" | `{ dataInicio, dataFim, abonoPecuniario, diasAbono, adiantar13Salario, mensagem }`; 201 `FeriasPedido`; 400; 422 `FERIAS_REGRAS_NAO_ATENDIDAS` (com `regras`), `FUNCIONARIO_INATIVO`, `FERIAS_SEM_PERIODO_AQUISITIVO` |

**Estados**: como na tela 06. Sucesso: fecha o painel, a tela 34 recarrega (o pedido aparece "Aguardando o gestor" ou "Aguardando o RH") e o cartão da tela inicial também.

**Observações**: **não reimplemente** nenhuma regra (a API é a única dona delas). Use o mesmo componente de conferência da tela 06 (muda só a rota). O gestor e o RH pedem as **próprias** férias por este mesmo painel (tela 48 para o gestor).

---

## Tela 44: Benefícios (funcionário; o gestor usa a mesma tela)

PNG: `Figma\telas\44-func-beneficios.png`. Incremento 7b. **O gestor reutiliza esta tela com o menu dele** (roadmap, decisão 14; contrato `obterMeusBeneficios`); o gestor **não** vê benefícios, dependentes nem trajeto da equipe.

**O que fica (li no PNG 44)**
- Título "Benefícios", subtítulo "O que a empresa oferece para você e como usar".
- **"Seus benefícios"**: um cartão por item de `beneficios` (adesões ativas e aprovadas), em grade de 4 por linha no PNG: ícone, etiqueta "Ativo", nome (`beneficio.nome`) e descrição (`beneficio.descricao`).
- **"Disponíveis para você"** com o texto de apoio abaixo: cartões de borda tracejada, um por item de `disponiveis` (opcionais do catálogo que a pessoa ainda não tem nem pediu), com ícone, nome, descrição e o botão que abre a tela 45.
- **"Seus pedidos"**: tabela **Pedido** (`titulo`: "Inclusão de dependente no plano de saúde", "Atualização do trajeto do vale-transporte"), **Data** (`pedidoEmUtc`) e **Situação** (etiqueta "Aprovado"), de `pedidos`, do mais novo para o mais antigo.
- Rodapé "Dúvida sobre algum benefício? Fale com o RH".

**O que sai ou muda (e por quê)**
- **Detalhes que o banco não tem ficam de fora** (contrato `obterMeusBeneficios`): "Crédito: Todo dia 1º", "Cartão: [Operadora]", "Operadora", "Desconto: Até 6% do salário", "Desde 14/03/2022", "Beneficiários: 2 cadastrados". O cartão mostra só nome e descrição do catálogo, mais duas linhas que o contrato **tem**: em benefício com `aceitaDependentes`, "Quem está no plano: Você e N dependente(s)" (a lista `dependentes`, só os aprovados); em benefício com `aceitaTrajeto`, "Trajeto: {`trajetoAtual.linhasMeios`}" (suponho: **só as linhas ou meios**, não a origem, porque a origem é o endereço de casa e a demo pode ser num projetor).
- **Saem os botões "Ver regras de uso", "Ver carteirinha" e "Ver beneficiários"** (falta dado). "Atualizar trajeto" fica (só com `aceitaTrajeto`). **Entra o botão "Incluir dependente"** (só com `aceitaDependentes`): o Figma não o desenha, mas a rota `pedirInclusaoDeDependente` precisa de um ponto de partida e o pedido aparece em "Seus pedidos" (suponho o rótulo e o lugar).
- **"O RH analisa e responde por e-mail" muda**: não há e-mail de resposta de benefício (`CONVENCOES.md` seção 12). Texto sugerido (suponho): "Benefícios que você ainda não tem e pode pedir. O RH analisa e responde em até 5 dias úteis; você vê a resposta em Seus pedidos."
- **Texto do cartão disponível**: "Opcional. A mensalidade é descontada em folha." e "Para quem tem filhos de até 6 anos." (PNG) **não** vêm do contrato (o catálogo não marca desconto em folha): mostre só a `descricao`. O botão é "Quero aderir" em todos (o catálogo não tem texto de botão; o "Pedir auxílio" do Figma fica como exemplo, suponho).
- `ultimoPedido` (recusado ou cancelado antes): no cartão disponível, uma linha "Seu último pedido foi recusado: {motivoRecusa}" (suponho a frase); a pessoa pode pedir de novo.
- Em "Seus pedidos", `PENDENTE` mostra "Resposta até {respostaPrevistaAte}" e `RECUSADO` mostra o `motivoRecusa` (a pessoa precisa vê-lo). Rótulos das situações (suponho): Pendente, Aprovado, Recusado, Cancelado.
- "Fale com o RH" não tem destino no contrato: deixe como texto, sem link (suponho).
- O ícone de cada benefício não vem da API: escolha-o pelo nome do catálogo (ids e nomes em `ELENCO.md`: Vale-refeição, Plano de saúde, Plano odontológico, Vale-transporte, Seguro de vida, Auxílio-creche, Vale-cultura) com um ícone genérico para o que não estiver na lista.

**Modais sem print** (suponho o desenho; o contrato fixa os campos)
- **"Incluir dependente"**: campos Nome (1 a 150), Parentesco (1 a 30, texto livre) e Data de nascimento (não pode ser futura); botões "Cancelar" e "Enviar pedido". Rota `pedirInclusaoDeDependente` (`POST /api/eu/beneficios-adesoes/{adesaoId}/dependentes`, corpo `{ nome, parentesco, dataNascimento }`; 201 `BeneficioAdesaoPedidoResumo`; 404; 409 `BENEFICIO_ADESAO_NAO_ATIVA`; 422 `BENEFICIO_NAO_ACEITA_DEPENDENTES`, `FUNCIONARIO_INATIVO`). Texto de apoio: "Enquanto o RH não aprovar, o dependente não tem cobertura." (do contrato).
- **"Atualizar trajeto"**: campos Origem (1 a 200), Destino (1 a 200) e Linhas ou meios (1 a 255; exemplo "Ônibus 175T-10 e Metrô Linha 2"), com os valores do trajeto atual preenchidos. Rota `pedirAtualizacaoDeTrajeto` (`POST /api/eu/beneficios-adesoes/{adesaoId}/trajetos`, corpo `{ origem, destino, linhasMeios }`; 201; 404; 409 `BENEFICIO_ADESAO_NAO_ATIVA`; 422 `BENEFICIO_NAO_ACEITA_TRAJETO`, `FUNCIONARIO_INATIVO`). Aviso: "O trajeto atual continua valendo até o RH aprovar o novo." (do contrato).

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterMeusBeneficios` | ao abrir a tela e depois de cada pedido | `BeneficioAdesaoEuResposta` (`beneficios`, `disponiveis`, `pedidos`, `dependentesCadastrados`; listas curtas, sem paginação) |
| `pedirInclusaoDeDependente`, `pedirAtualizacaoDeTrajeto` | os dois modais acima | `BeneficioAdesaoPedidoResumo` |

**Estados**: sem benefícios ativos: "Você ainda não tem benefícios ativos." (suponho); sem disponíveis: esconda a seção; sem pedidos: "Você ainda não fez pedidos." (suponho); carregando; erro.

**Perguntas abertas (`PROVISORIOS.md`)**: `BeneficioAdesaoDependenteComando.parentesco`: a lista de parentescos não foi decidida (texto livre por enquanto); se virar lista fechada, o campo vira seletor. Os exemplos do contrato usam FILHO, FILHA e CONJUGE.

---

## Tela 45: Painel lateral "Aderir ao {benefício}" (funcionário; também gestor)

PNG: `Figma\telas\45-func-beneficios-aderir-a-beneficio.png`. Abre da direita sobre a tela 44, por "Quero aderir" (ou "Pedir auxílio" no Figma). Incremento 7b.

**O que fica (li no PNG 45)**
- Título "Aderir ao plano odontológico" (nome do `disponiveis[].beneficio.nome`).
- Faixa de apoio com o nome e a descrição do benefício.
- **"Quem vai entrar no plano"** (só em benefício com `aceitaDependentes`): "Você" (sempre marcado e sem poder desmarcar), uma caixa por item de `dependentesCadastrados` (dependentes já aprovados, com o nome; o front reenvia os dados dele no pedido) e "Incluir outro dependente" (abre os campos nome, parentesco e data de nascimento; suponho que se possa incluir mais de um).
- **"Começar em"**: data ("1º de novembro"; `dataInicioDesejada`, **não pode ser anterior a hoje**; sugestão: o primeiro dia do próximo mês, como no PNG, suponho).
- Caixa "Autorizo o desconto mensal em folha" (`autorizaDescontoEmFolha`; fica guardada como prova de consentimento; a rota aceita `false`, então a caixa **não é obrigatória**).
- Texto "O RH confere o pedido e responde por e-mail em até 5 dias úteis." e os botões "Cancelar" e "Enviar pedido".
- Trajeto: em benefício com `aceitaTrajeto` (vale-transporte), três campos a mais (origem, destino, linhas ou meios), como no modal de "Atualizar trajeto" (suponho a posição; o PNG é de um plano de saúde).

**O que sai ou muda (e por quê)**
- "[nome da operadora]" e "[valor] por pessoa" (li no PNG 45) **não existem no contrato**: a faixa mostra só nome e descrição.
- **"responde por e-mail" muda** (não há e-mail de resposta de benefício): "O RH confere o pedido e responde em até 5 dias úteis. Você vê a resposta em Seus pedidos." (suponho a frase; o prazo é `respostaPrevistaAte`).

**Rotas**

| Rota | Quando | Corpo e resposta |
|---|---|---|
| `pedirAdesaoABeneficio` (`POST /api/eu/beneficios-adesoes`) | botão "Enviar pedido" | `{ beneficioId, dataInicioDesejada, autorizaDescontoEmFolha, dependentes[], trajeto }`; 201 `BeneficioAdesaoPedidoResumo`; 400; 404; 409 `BENEFICIO_JA_ATIVO_OU_PENDENTE` ("Você já pediu este benefício."); 422 `BENEFICIO_NAO_OPCIONAL`, `BENEFICIO_NAO_ACEITA_DEPENDENTES`, `BENEFICIO_NAO_ACEITA_TRAJETO`, `FUNCIONARIO_INATIVO` |

**Estados**: enviando (botão desabilitado); sucesso (fecha, a tela 44 recarrega: o benefício sai de "Disponíveis" e o pedido entra em "Seus pedidos" como Pendente); erros 409 e 422 com o `detail`.

**Observações**: se a pessoa já teve um pedido recusado ou cancelado deste benefício, o novo pedido **reabre a mesma adesão** (efeito do banco; a tela não faz nada de diferente). Limites: nome 1 a 150, parentesco 1 a 30, origem e destino 1 a 200, linhas ou meios 1 a 255.

---

## Tela 46: Tela inicial (gestor)

PNG: `Figma\telas\46-gestor-tela-inicial.png`. Igual à tela 32, com o menu do gestor e uma faixa a mais. Incremento 7d. **Todo o conteúdo e as rotas da tela 32 valem aqui** (o cartão "Seu ponto hoje", "Banco de horas", "Férias disponíveis", "Próximo pagamento", "Seus benefícios", "Avisos" e "Aniversariantes" são os mesmos).

**O que muda em relação à tela 32 (li no PNG 46)**
- Menu: "Seu dia a dia" (Início, Meu ponto, Férias, Holerites), **"Seu setor"** (Painel da equipe com bolinha, Ponto da equipe, Formulários, Treinamentos, Advertências), "Comunicação" (Avisos com bolinha). Rodapé "Carlos Ribeiro, Perfil gestor, setor Comercial" (`perfil`, `cargo` e `setor` de `obterUsuarioLogado`).
- A bolinha de "Painel da equipe" vem de `obterContadoresDoMenuDoGestor.pendenciasDaEquipe` (pedidos de férias com o gestor mais pedidos de ajuste de ponto na etapa dele; o pedido em `SUGESTAO_DE_DATA` **não** conta, porque espera a pessoa).
- Faixa laranja "Você tem 2 pedidos de ajuste de ponto do setor esperando sua análise." com o link "Analisar agora": o número é `obterContadoresDoMenuDoGestor.pedidosDeAjusteDePonto`; sem pedidos, a faixa não aparece. O link vai ao Painel da equipe, tela 51, onde estão os cartões de decisão (suponho; o Figma não diz o destino).

**O que sai ou muda (e por quê)**
- O menu do gestor do Figma **não tem "Benefícios"** (nem "Meus documentos"), mas o cartão "Seus benefícios" e a decisão 14 do roadmap dizem que o gestor **reutiliza** a tela de benefícios com o menu dele: o menu do gestor já traz "Benefícios" (grupo "Seu dia a dia") e "Meus documentos" (1a e fatia 1). **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu; no RH ele traz "Férias" (`/eu/ferias`) e "Benefícios" (`/eu/beneficios`), entre os itens pessoais (os do gestor que não são desta fatia são "Meus formulários", "Meus treinamentos" e "Minhas advertências"). O gestor continua sem ver benefícios da equipe.
- O número da bolinha de "Painel da equipe" é **2** no PNG; o exemplo do contrato dá **3** (`pendenciasDaEquipe: 3`, `pedidosDeAjusteDePonto: 2`: dois ajustes mais o pedido de férias 345 do Pedro Santos). Mostre o que a API manda; o PNG é só exemplo.
- Demais divergências de dados do cartão "Seus benefícios" e de "Vencem em 14/03/2027": como na tela 32.

**Rotas**: as da tela 32, mais `obterContadoresDoMenuDoGestor`. O gestor continua chamando também `obterContadoresDoMenuDoFuncionario`, porque as bolinhas de Avisos e Formulários são da rota `/api/eu/contadores-do-menu`. Estados e observações: como na tela 32. Gestor **sem setor** (perfil Gestor que ainda não foi escolhido gestor de um setor): `pendenciasDaEquipe` é 0 e a tela inicial funciona normalmente.

---

## Tela 48: Férias (gestor)

PNG: `Figma\telas\48-gestor-ferias.png`. É a **mesma tela 34** (mesma rota `obterMinhasFerias`) dentro do menu do gestor, e o painel "Pedir férias" é o da tela 35. O gestor vê aqui as férias **dele**, não as da equipe (essas ficam na tela 51).

**O que muda em relação às telas 34 e 35 (li no PNG 48)**
- Só o menu (o do gestor, com "Painel da equipe" 2 e "Avisos" 2) e o rodapé do usuário ("Carlos Ribeiro, Perfil gestor, setor Comercial").
- **Última linha de "Como funciona"**: o PNG diz "Roberto Alves analisa e depois o RH confirma". Pela regra do contrato o pedido de quem é gestor **não tem etapa do gestor** (só o RH decide), então `gestorQueAnalisa` vem `null` e o texto é **"O RH analisa o pedido."** (ver tela 34). O mesmo vale para o rodapé e para o rótulo da mensagem do painel "Pedir férias" (tela 35).
- Os pedidos do próprio gestor nunca aparecem na tela 51 dele; ele acompanha aqui, em "Histórico".

**Rotas**: as das telas 34 e 35. Estados e observações: os mesmos.

---

## Tela 51: Painel da equipe (gestor)

PNG: `Figma\telas\51-gestor-painel-da-equipe.png`. Incremento 7a (cartões de férias e calendário) e 7d (tela completa). Só o perfil Gestor, sobre a **equipe dele**: as pessoas ativas do setor de que ele é gestor, **sem ele mesmo** (um gestor, um setor; "8 pessoas").

**O que fica (li no PNG 51)**
- Título "Equipe Comercial" (`setor.nome`) e subtítulo "8 pessoas. Você vê o ponto, as férias, os treinamentos e as advertências de quem está no seu setor." (`totalPessoas`). Botão **"Avaliar a equipe (ciclo 2026.2)"** (`avaliacao.ciclo`): abre a avaliação `avaliacao.formularioId` (fatia 5); com `avaliacao` `null` (nenhum ciclo aberto para o gestor), o botão **e** a coluna "Avaliação" **somem**.
- **Faixa "Hoje"**: "6 trabalhando", "1 de férias", "1 sem registro: Diego Farias". Os números são `contagensHoje` de `listarEquipeDoGestor` (**não** vêm de `obterPainelDaEquipe`); os nomes de quem está sem registro são os `itens` com `situacaoHoje` `SEM_REGISTRO`. Suponho: mostre também "N com atestado" e "N ainda não começaram" só se forem maiores que zero; sem "sem registro", omita o trecho; em dia sem expediente (`diaUtil` falso) a faixa diz "Hoje não há expediente".
- **"Esperando sua aprovação"** (`aguardandoAprovacao`), cartões, primeiro os de férias e depois os de ajuste de ponto, cada grupo na ordem em que a API manda (a lista de férias é do pedido mais antigo para o mais novo):
  - **Férias**: etiqueta "Férias" e a frase "Pedro Santos pediu 15 dias, de 10/11 a 24/11." (`funcionario.nome`, `dias`, `dataInicio`, `dataFim`). Se `abonoPecuniario`, acrescente "Quer vender {diasAbono} dias." e, se `adiantar13Salario`, "Quer adiantar a 1ª parcela do 13º." (suponho as frases). Se houver `observacao` (a mensagem da pessoa), mostre-a entre aspas. Por cada item de `conflitos`, aviso laranja "Coincide 8 dias com as férias já aprovadas de Júlia Nunes (03/11 a 17/11). A equipe fica com 6 pessoas nesse período." (`diasEmComum`, `funcionario.nome`, datas, `equipeDisponivel`). Botões **"Aprovar"**, **"Sugerir outra data"** e **"Recusar"**.
  - **Ajuste de ponto** (fatia 3): etiqueta "Ajuste de ponto", frase "{nome} pediu: {resumoDaCorrecao}" (do contrato; o Figma escreve "Diego Farias pediu para incluir a volta do almoço de 05/10, às 13:05"), a data do dia a corrigir (`data`), a `justificativa` entre aspas e os botões "Aprovar" e "Recusar".
- **"Férias da equipe"** com o texto à direita "Outubro a dezembro, por semana" (de `inicio` a `fim` do calendário): quadro com uma linha por pessoa da equipe e uma coluna por semana (`semanas`: a segunda-feira de cada coluna, rotulada pelo dia; os meses aparecem como cabeçalho acima), 13 semanas a partir da segunda-feira da semana atual. Cada período é uma barra: **sólida** para `APROVADA`; **tracejada laranja** para `AGUARDANDO_VOCE` (legenda "Aguardando você"); `COM_O_RH` (o gestor já aprovou, falta o RH; o Figma não desenha) com a cor das aprovadas e borda tracejada (sugestão do contrato; legenda "Aguardando o RH", suponho). Texto da barra: "03/11 a 17/11"; "Até 17/10" quando começa antes da janela; "A partir de {dd/mm}" quando passa do fim da janela (suponho). Legenda "Aprovadas" e "Aguardando você".
- **"Pessoas da equipe"**: tabela com **Pessoa** (iniciais, nome e cargo), **Ponto hoje**, **Banco de horas**, **Férias disponíveis**, **Avaliação {ciclo}**, **Advertências** e **Ações** (botão "Abrir ficha", que abre a ficha da equipe, tela 52, fatia 1).
  - Ponto hoje, por `pontoHoje.situacaoHoje`: `TRABALHANDO` "Entrada 08:01" (`horario`); `FERIAS` "De férias até 17/10" (`feriasAte`); `SEM_REGISTRO` "Sem registro" (em destaque laranja); e, que o Figma não mostra (suponho): `ATESTADO` "Atestado", `AINDA_NAO_COMECOU` "Ainda não começou", `SEM_EXPEDIENTE` "Sem expediente".
  - Banco de horas: `bancoDeHorasMinutos` formatado ("+2h10", "−1h30", "0h00").
  - Férias disponíveis: "15 dias" (`feriasDisponiveisDias`).
  - Avaliação: etiqueta "Respondida" (`RESPONDIDA`), "Pendente" (`PENDENTE`) ou "Não se aplica" (`NAO_SE_APLICA`); a coluna some com `avaliacaoSituacao` `null`.
  - Advertências: "Nenhuma" quando 0; o número em vermelho quando maior que zero (já descontadas as canceladas e só do ano corrente).

**Modais sem print** (suponho o desenho; o contrato fixa os campos)
- **"Recusar pedido de férias"**: campo "Motivo" obrigatório, **5 a 500 caracteres** (a pessoa o vê, no histórico e no e-mail); botões "Cancelar" e "Recusar". Rota `recusarFeriasComoGestor` (`POST /api/gestor/ferias/{feriasId}/recusar`, corpo `{ motivo }`). O pedido termina na hora (`REJEITADA`) e **não** vai ao RH.
- **"Sugerir outra data"**: campos "Primeiro dia" e "Último dia" (5 a 30 dias corridos, depois do fim do período aquisitivo; quem confere é a API). Rota `sugerirOutraDataDasFerias` (`POST /api/gestor/ferias/{feriasId}/sugerir-outra-data`, corpo `{ dataInicio, dataFim }`; 200 `FeriasPedidoAnalise`; 422 `FERIAS_REGRAS_NAO_ATENDIDAS` com `regras`). Depois, o cartão sai da lista (o pedido espera a resposta da pessoa).
- **"Aprovar"** (`aprovarFeriasComoGestor`, `POST /api/gestor/ferias/{feriasId}/aprovar`, sem corpo): sem janela de confirmação, como no Figma; o pedido passa ao RH. Aviso curto depois (suponho): "Pedido aprovado. Agora o RH confirma."

**O que sai ou muda (e por quê)**
- **Sai o botão "Lembrar Diego por e-mail"** (li no PNG 51): decisão do dono em 2026-10-30; não existe e-mail de lembrete de ponto (lista fechada, `CONVENCOES.md` seção 12) nem rota.
- **Entra o botão "Recusar" no cartão de férias** (o Figma só tem "Aprovar" e "Sugerir outra data"): os documentos dizem que o gestor pode recusar, e valem (`recusarFeriasComoGestor`; decisão do dono, 2026-10-29: "se o gestor recusa, o pedido acaba na hora").
- **Ordem das pessoas**: o PNG não segue ordem alguma (Júlia, Pedro, Larissa, Diego...); o contrato manda **ordem alfabética do nome** (`pessoas`). Use a do contrato, tanto na tabela quanto no quadro de férias.
- **Dados do exemplo**: o PNG mostra o ajuste de ponto do Diego Farias; no contrato os pedidos esperando o Carlos são da Júlia Nunes (303) e da Helena Prates (304). É só exemplo.
- **"Baixar espelho do setor"** (tela 54) também sai por decisão do dono, mas é da fatia 3; não aparece aqui.
- O gestor **não recebe** CPF, endereço, holerite, documentos nem a origem do trajeto nesta tela (contrato).

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterPainelDaEquipe` | ao abrir | `PainelGestor` (`setor`, `totalPessoas`, `aguardandoAprovacao`, `avaliacao`, `pessoas`) |
| `listarEquipeDoGestor` (fatia 1) | ao abrir, em paralelo | `contagensHoje`, `diaUtil` e a lista de pessoas (também serve de linhas do quadro de férias) |
| `obterCalendarioDeFeriasDaEquipe` (`GET /api/gestor/ferias/calendario`, parâmetros opcionais `inicio` e `semanas`) | ao abrir (sem parâmetros: 13 semanas) | `FeriasCalendario` (`inicio`, `fim`, `semanas`, `itens`); pessoas sem férias na janela **não vêm** em `itens`: desenhe a linha sem barra |
| `aprovarFeriasComoGestor`, `recusarFeriasComoGestor`, `sugerirOutraDataDasFerias` | botões dos cartões de férias | `FeriasPedidoAnalise`; 404 (pedido fora da equipe, do próprio gestor ou de outro gestor), 409 `PEDIDO_ETAPA_INCORRETA` ou `PEDIDO_JA_DECIDIDO` |
| `aprovarPedidoDeAjusteComoGestor`, `recusarPedidoDeAjusteComoGestor` (fatia 3, corpo `PontoPedidoDecisao`, observação opcional) | botões dos cartões de ajuste | `PontoPedidoAjuste`; 409 |
| `obterContadoresDoMenuDoGestor` | bolinha do menu | `pendenciasDaEquipe`, `pedidosDeAjusteDePonto` |

Depois de qualquer decisão, recarregue `obterPainelDaEquipe`, `obterCalendarioDeFeriasDaEquipe` e `obterContadoresDoMenuDoGestor`.

**Estados**
- Carregando (esqueleto por bloco, suponho).
- **Gestor sem setor**: `setor` `null`, `totalPessoas` 0 e tudo vazio: mostre "Você ainda não é gestor de um setor." (suponho) e esconda os blocos.
- Equipe sem ninguém ativo: "Seu setor ainda não tem pessoas ativas." (suponho). Nada esperando: "Nada esperando sua aprovação." no lugar do bloco (suponho).
- 403 (perfil que não é Gestor); 409 e 404 ao decidir: mostre o `detail` e recarregue (alguém decidiu antes, ou o RH ligou "Gestor ausente" e o pedido já passou ao RH).

**Observações**
- `podeDecidir` vem `true` em todos os itens de `aguardandoAprovacao`; mesmo assim, ligue os botões a ele (se vier `false`, mostre `motivoNaoPodeDecidir`).
- A coluna "Férias disponíveis" usa o número que vem em `pessoas` (os dias em pedido ficam dentro dele); não chame a rota de férias de cada pessoa.
- **`listarFeriasDaEquipe` não tem tela** (a lista do bloco "Esperando sua aprovação" já vem em `obterPainelDaEquipe`, os mesmos itens). Não crie tela para ela: registre no `PROGRESSO.md` "sem uso em tela". Consequência: o gestor só acompanha as sugestões de outra data que fez (pedidos em `SUGESTAO_DE_DATA`, que não entram no painel nem no calendário) pela aba Férias da ficha da pessoa (tela 7.1).
- **Largura**: a tabela tem 7 colunas e o quadro de férias 13 semanas; veja a ficha, "Riscos" (1366 px).

**Perguntas abertas (`PROVISORIOS.md`)**: `PainelAvaliacaoDoCiclo` (como o ciclo de avaliação, "2026.2", é definido: fatia 5); a tela mostra o rótulo que vier.

---

## Tela 7.1 (sem print): Aba Férias da ficha da equipe (gestor)

Sem PNG. A ficha da equipe é a tela 52 (`Figma\telas\52-gestor-painel-da-equipe-ficha.png`, li: abas **Dados**, Ponto, **Férias**, Treinamentos e Advertências); o Figma só desenha a aba Dados. O contrato tem a rota da aba Férias (`obterFeriasDaPessoaDaEquipe`: "mostra os mesmos dados da aba do RH"). O roadmap diz que as outras abas da ficha da equipe chegam nas fatias 3, 4 e 6; **a aba Férias chega aqui, na 7a**. Incremento 7a.

**Descrição em texto**
- Os mesmos três cartões e a mesma tabela da tela 05 (Dias disponíveis, Período aquisitivo atual, Prazo para tirar o saldo; tabela Período, Dias e Situação), **sem** o botão "Registrar férias" (o gestor não registra) e **com todos os pedidos**, inclusive os que ainda esperam o gestor ou estão em `SUGESTAO_DE_DATA` (diferente da aba do RH).
- Em cada linha, a frase da decisão (seção "Regras comuns") e, em `SUGESTAO_DE_DATA`, "Esperando a resposta de {nome} à sugestão de outra data." (o `motivoNaoPodeDecidir`).
- Sem botões de decisão: o gestor decide no painel da equipe (tela 51).
- Rodapé sugerido (suponho): "Os pedidos esperando sua análise também aparecem no Painel da equipe."

**Rotas**: `obterFeriasDaPessoaDaEquipe` (`GET /api/gestor/equipe/{funcionarioId}/ferias`; `FeriasFicha`). 404 para pessoa de outro setor e para quem é gestor (mesmo de outro setor); 403 para outro perfil.

**Estados**: carregando; sem pedidos ("Nenhum pedido de férias ainda.", suponho); 404 (volta à lista da equipe).

**Observações**: reutilize o componente da aba da tela 05 passando um parâmetro que esconde o botão.

---

## Tela 7.2 (sem print): Pedidos de férias para o RH

Sem PNG. O Figma não desenha esta lista; o contrato diz que o front monta **uma lista simples com o mesmo cartão da tela 51** (`listarFeriasParaORh`). É o destino do botão "Revisar" do cartão "Pedidos de férias" da tela 00 (e do tipo `FERIAS` em "Ver todas as pendências"). Incremento 7a. Não está no menu do RH do Figma: acesse pelo painel, em `/rh/pedidos-de-ferias` (`05-caminhos-de-tela.md`; `?pedido=<referenciaId>` destaca o pedido).

**Descrição em texto**
- Título "Pedidos de férias" e subtítulo "Os que esperam a confirmação do RH" (suponho).
- Filtros: **Situação** (`situacao`; padrão `COM_O_RH`; demais valores de `FeriasSituacao`: Aguardando o gestor, Outra data sugerida, Aprovada, Em gozo, Concluída, Recusada, Cancelada), **Setor** (`setorId`, lista de `listarSetores`) e busca por nome (`busca`). Paginação padrão (`pagina`, `tamanhoPagina`; rodapé "Mostrando 1 a 8 de 101", como no PNG 05).
- Uma lista de cartões, do pedido mais antigo para o mais novo. Cada cartão: etiqueta "Férias"; "{nome} ({cargo}, {setor}) pediu {dias} dias, de {início} a {fim}." e as frases extras (abono, 13º, mensagem) como no cartão da tela 51; o aviso laranja de coincidência (`conflitos`, `equipeDisponivel`) da tela 51; a **decisão do gestor** em uma frase ("Aprovado pelo gestor Carlos Ribeiro em 08/10", "Aprovado automaticamente (gestor ausente)" ou "Este pedido não passa pelo gestor"); o rótulo da situação.
- Botões **"Confirmar"** (`confirmarFeriasComoRh`) e **"Recusar"** (`recusarFeriasComoRh`, abre o modal de motivo de 5 a 500 caracteres, o mesmo da tela 51), habilitados por `podeDecidir`; desabilitados mostram `motivoNaoPodeDecidir`.
- O RH **pode recusar o que o gestor já aprovou** (decisão do dono, 2026-10-30). **Não existe "o RH aprova direto"**: com o pedido ainda com o gestor, `podeDecidir` é falso e a API responde 409 `PEDIDO_ETAPA_INCORRETA`.
- Quem do RH decide pedido **sem etapa do gestor** (pedido de gestor, de setor sem gestor ou de gestor desligado) precisa ter linha em `GESTORES`; senão `podeDecidir` é falso e a API devolve 422 `PEDIDO_RH_FORA_DE_GESTORES`. Decidir o **próprio** pedido sem etapa do gestor: 422 `PEDIDO_PROPRIO`. Se a etapa do gestor foi feita por outra pessoa, o RH pode confirmar o próprio pedido (decisão do dono, 2026-10-29).
- Ao abrir vindo do painel, destaque o pedido `referenciaId` (o mais antigo).

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `listarFeriasParaORh` (`GET /api/rh/ferias`; `pagina`, `tamanhoPagina`, `busca`, `situacao`, `setorId`) | ao abrir e ao mudar filtro | `FeriasListaAnalise` |
| `confirmarFeriasComoRh` (`POST /api/rh/ferias/{feriasId}/confirmar`, sem corpo) | "Confirmar" | `FeriasPedidoAnalise`; 409; 422 `PEDIDO_PROPRIO`, `PEDIDO_RH_FORA_DE_GESTORES` |
| `recusarFeriasComoRh` (`POST /api/rh/ferias/{feriasId}/recusar`, corpo `{ motivo }`) | "Recusar" | `FeriasPedidoAnalise`; 400 (motivo fora de 5 a 500); 409; 422 |
| `listarSetores` (fatia 1) | filtro de setor | lista |

**Estados**: carregando; vazio ("Nenhum pedido de férias esperando o RH.", suponho); erro; depois de decidir, o cartão sai da lista e o painel (tela 00) e a bolinha atualizam.

---

## Tela 7.3 (sem print): Pedidos de benefícios para o RH

Sem PNG. O Figma não desenha; o contrato diz "o front monta uma lista simples, de preferência com o mesmo visual das pendências do painel" (`listarPedidosDeBeneficio`). Destino do "Revisar" do tipo `BENEFICIOS` do painel (tela 00). Incremento 7b.

**Descrição em texto**
- Título "Pedidos de benefícios" e subtítulo "O RH responde em até 5 dias úteis" (suponho).
- Filtros: **Situação** da adesão (`situacao`: Pendente (padrão, que **inclui** adesões já aprovadas com dependente ou trajeto pendente), Aprovado, Recusado, Cancelado), **Benefício** (`beneficioId`, lista de `listarBeneficiosDoCatalogo`) e busca (`busca`). Paginação padrão.
- Uma linha (cartão) **por adesão**, do pedido mais antigo para o mais novo: nome da pessoa (cargo e setor), nome do benefício, "Pedido em {data}", "Responder até {`respostaPrevistaAte`}" (destaque se já passou, suponho), "Começar em {`dataInicioDesejada`}", "Autoriza o desconto em folha: sim ou não". Quando `pedidoEmUtc` é `null`, o RH deu o benefício direto (sem pedido).
- Dentro do cartão, a lista de **dependentes** (nome, parentesco, data de nascimento, situação) e de **trajetos** (origem, destino, linhas ou meios, situação): dado pessoal sensível (endereço de casa), só o RH vê.
- Ações: na adesão `PENDENTE`, **"Aprovar"** (`aprovarAdesaoABeneficio`: aprova **junto** os dependentes e o trajeto que vieram no mesmo pedido e ainda estão pendentes) e **"Recusar"** (`recusarAdesaoABeneficio`, motivo obrigatório, até 500 caracteres; os dependentes e o trajeto pendentes viram cancelados). Em dependente ou trajeto `PENDENTE` de adesão **já aprovada**, **"Aprovar"** e **"Recusar"** próprios (`aprovarDependenteDeBeneficio`, `recusarDependenteDeBeneficio`, `aprovarTrajetoDeBeneficio`, `recusarTrajetoDeBeneficio`). Para recusar só um dependente de um pedido novo, o RH aprova a adesão e depois recusa o dependente (as rotas de dependente e de trajeto só funcionam com a adesão aprovada): ponha uma dica curta no cartão (suponho).
- A pessoa vê o resultado em "Seus pedidos" (tela 44), **sem e-mail**.

**Rotas**

| Rota | Quando | Resposta e erros |
|---|---|---|
| `listarPedidosDeBeneficio` (`GET /api/rh/beneficios-adesoes`; `pagina`, `tamanhoPagina`, `busca`, `situacao`, `beneficioId`) | ao abrir e ao filtrar | `BeneficioAdesaoRhLista` |
| `aprovarAdesaoABeneficio` (`POST .../{adesaoId}/aprovar`) | "Aprovar" da adesão | `BeneficioAdesaoRh`; 409 `BENEFICIO_PEDIDO_JA_DECIDIDO`; 422 `FUNCIONARIO_INATIVO` |
| `recusarAdesaoABeneficio` (`POST .../{adesaoId}/recusar`, corpo `{ motivo }`) | "Recusar" da adesão | `BeneficioAdesaoRh`; 400; 409 |
| `aprovarDependenteDeBeneficio`, `recusarDependenteDeBeneficio` (`.../dependentes/{dependenteId}/aprovar` e `/recusar`) | ações do dependente | `BeneficioAdesaoRh`; 409 `BENEFICIO_ADESAO_NAO_ATIVA` ou `BENEFICIO_PEDIDO_JA_DECIDIDO`; 404 |
| `aprovarTrajetoDeBeneficio`, `recusarTrajetoDeBeneficio` (`.../trajetos/{trajetoId}/aprovar` e `/recusar`) | ações do trajeto | idem |
| `listarBeneficiosDoCatalogo` (fatia 1) | filtro | lista |

**Estados**: carregando; vazio ("Nenhum pedido de benefício esperando o RH.", suponho); erro; 409 mostra o `detail` e recarrega. O motivo de recusa não pode ficar vazio.

**Perguntas abertas (`PROVISORIOS.md`)**: o tipo `BENEFICIOS` na lista de pendências (ver tela 00).

---

## Tela 7.4 (sem print): Pagamento (RH)

Sem PNG. O Figma não desenha. O contrato sugere **uma seção "Pagamento" dentro de Cadastros** (tela 29, fatia 1), ao lado de "Empresa" (menu lateral de Cadastros, só RH): acrescente o item "Pagamento". Incremento 7c.

**Descrição em texto**
- **Regra padrão** (só leitura nesta tela): "Todo dia {`diaPagamento`}" (de `obterEmpresa`). A regra é **única para a empresa toda** e se edita em Cadastros, **Empresa** (`salvarEmpresa`, fatia 1): ponha um link "Alterar o dia em Empresa". Sem `diaPagamento`: "O dia de pagamento ainda não foi definido." e o mesmo link.
- **Calendário do ano** (`obterCalendarioDePagamento`): seletor de ano (`ano`; padrão o ano atual) e uma tabela com os 12 meses (só os que a API manda): **Mês**, **Data do pagamento** (`data`), **Origem** ("Regra" ou "Exceção") e **Ajustes**, em frases (suponho): `ULTIMO_DIA_DO_MES` "O mês não tem o dia {n}: vale o último dia"; `ANTECIPADO_FIM_DE_SEMANA` "Antecipado: caiu no fim de semana"; `ANTECIPADO_FERIADO` "Antecipado: caiu em feriado" (podem vir dois). Mês sem `diaPagamento` e sem exceção **não vem** na lista.
- **Exceções** (`listarExcecoesDePagamento`; sem `ano`, do mês atual em diante): lista **Mês e ano**, **Data do pagamento** e o botão "Remover" (volta a seguir a regra). Botão **"Nova exceção"**: campos mês, ano e data de pagamento (a data tem de cair **dentro daquele mês e ano**, senão 400); salva com `definirExcecaoDePagamento` (`PUT /api/rh/pagamento/excecoes/{ano}/{mes}`, corpo `{ dataPagamento }`; **substitui** a que já existir). A data da exceção vale **como está**: não antecipa por fim de semana ou feriado (quem escolhe é o RH); diga isso no formulário (texto do contrato).
- `removerExcecaoDePagamento` (`DELETE .../{ano}/{mes}`): 204; 404 se não existir. Remover pede confirmação (suponho).
- Depois de salvar ou remover, recarregue calendário e exceções, e o cartão "Próximo pagamento" das telas iniciais.

**Rotas**

| Rota | Quando | Resposta |
|---|---|---|
| `obterCalendarioDePagamento` (`GET /api/rh/pagamento/calendario`; `ano`) | ao abrir e ao trocar o ano | `PagamentoCalendario` |
| `listarExcecoesDePagamento` (`GET /api/rh/pagamento/excecoes`; `ano`) | ao abrir | `PagamentoExcecaoLista` (curta, sem paginação) |
| `definirExcecaoDePagamento` | "Nova exceção" | `PagamentoExcecao`; 400 |
| `removerExcecaoDePagamento` | "Remover" | 204; 404 |
| `obterEmpresa` (fatia 1) | regra padrão | `diaPagamento` |

**Estados**: carregando; sem exceções ("Nenhuma exceção cadastrada.", suponho); erro; 403 (só RH).

**Observações**: o "Próximo pagamento" das quatro telas (32 e 46, aqui; 36 e 49, da fatia 4) é o `PagamentoProximo` ("definição única do dado"): **uma só** implementação do cartão, que recebe `{ data, mesReferencia }` por props. Cada tela o alimenta **pela rota do próprio painel quando ela traz o campo**: as telas **32 e 46** por `obterPainelDoFuncionario.proximoPagamento` (`GET /api/eu/painel`, lido em `70-fatia-7-gestao.yaml`); as telas **36 e 49**, que não têm painel, por `obterProximoPagamento`. O componente nasce na fatia 4; se ela já o criou, reaproveite. Regras do dia (aceitas pelo dono em 2026-10-29): mês sem o dia, vale o último; cai em fim de semana ou feriado, antecipa para o dia útil anterior. **Quem calcula é a API**; a tela só mostra.

---

## Tela 7.5 (sem print): Pendências do RH ("Ver todas as pendências")

Sem PNG. O Figma só tem o link "Ver todas as pendências" na tela 00; o contrato diz "o front monta uma lista simples" (`listarPendenciasDoRh`). Incremento 7d.

**Descrição em texto**
- Título "Pendências" e subtítulo "O que está esperando o RH".
- Filtro **Tipo** (`tipo`: os cinco de `PainelPendenciaTipo`) e paginação.
- Uma linha por pendência, **da mais antiga para a mais nova**: o tipo (rótulo da tela 00), a pessoa (`funcionario`), a frase `descricao` ("Saída de 02/10 sem registro"), "Desde {`desdeEmUtc`}" e o botão **"Revisar"**, com os mesmos destinos do cartão da tela 00 (ajuste de ponto: tela 12 com o modal 13, destacando `referenciaId`; atestado: lista de atestados do RH; férias: tela 7.2; benefícios: tela 7.3; admissão: ficha da pessoa).
- **Exceção em `ADMISSAO_COM_DOCUMENTOS_PENDENTES`**: nesse tipo `desdeEmUtc` **não** diz desde quando a pessoa está pendente (o banco não guarda esse instante); é a data da admissão, só para ordenar a fila (regra do contrato, schema `PainelPendenciaItem`). Para esse tipo escreva **"Começa em {dd/mm}"** (a data de `desdeEmUtc` em São Paulo) em vez de "Desde".

**Rotas**: `listarPendenciasDoRh` (`GET /api/rh/painel/pendencias`; `pagina`, `tamanhoPagina`, `tipo`; `PainelPendenciaLista`). **Estados**: carregando; vazio ("Nada esperando o RH.", suponho); erro; 403.

**Perguntas abertas (`PROVISORIOS.md`)**: o tipo `BENEFICIOS` (ver tela 00).

---

## Tela 7.6 (sem print): Pessoas sem registro hoje ("Ver quem está sem registro")

Sem PNG. O Figma só tem o link na tela 00 (`listarPessoasSemRegistroHoje`). Incremento 7d. Desenho simples, sem dado novo: página `/rh/painel/sem-registro` (`05-caminhos-de-tela.md`).

**Descrição em texto**
- Título "Sem registro de ponto hoje" e a data de hoje.
- Lista em **ordem alfabética**, paginada: iniciais, nome, cargo e setor (`FuncionarioResumo`); todas com `situacaoHoje` `SEM_REGISTRO` (dia com expediente, sem entrada, depois do horário padrão mais a tolerância). Quem **ainda não começou** (`AINDA_NAO_COMECOU`) não entra. Em dia sem expediente a lista vem **vazia**.
- Sem botão de lembrete: não existe e-mail de lembrete de ponto (decisão do dono, 2026-10-30).

**Rotas**: `listarPessoasSemRegistroHoje` (`GET /api/rh/painel/presenca/sem-registro`; `pagina`, `tamanhoPagina`; `PainelSemRegistroLista`). **Estados**: carregando; vazio ("Ninguém sem registro agora.", ou "Hoje não há expediente." quando `diaUtil` da tela 00 for falso; suponho); erro; 403.

---

## Perguntas abertas desta fatia, juntas

Os `x-provisorio` do contrato que tocam estas telas (construa normalmente; `PROVISORIOS.md` é a fonte):

| Onde no contrato | O que a decisão pendente mudaria na tela |
|---|---|
| `/api/eu/ferias > post`, `/api/eu/ferias/conferencia > post`, `/api/rh/funcionarios/{funcionarioId}/ferias > post` e `.../ferias/conferencia > post`; schemas `FeriasRegraCodigo` e `FeriasConferencia` | As regras de férias (partes, abono, 13º, antecedência de 30 dias, 2 dias antes de feriado): mudam os códigos e as mensagens da lista de verificações e o texto fixo de "Como funciona" (telas 34 e 48). A forma das telas não muda. |
| `FeriasConferenciaComando > diasAbono` | Se o dono preferir um valor fixo (10), o campo "Quantos dias" some (telas 06 e 35). |
| `FeriasSugestao > expiraEm` | O prazo da sugestão sem resposta: só muda o valor mostrado (tela 34). |
| `BeneficioAdesaoDependenteComando > parentesco` | Se virar lista fechada, o campo vira seletor (telas 44 e 45). |
| `/api/rh/painel > get`, `/api/rh/painel/pendencias > get` e `PainelPendenciaTipo` | O tipo `BENEFICIOS` pode sair das pendências (telas 00, 7.3 e 7.5). |
| `PainelAvaliacaoDoCiclo` (fatia 5) | Só a origem do rótulo do ciclo (tela 51). |

Sem `x-provisorio` mas abertas no roadmap: a **definição de "presença agora"** (o contrato adotou `SituacaoHoje`, tela 00) e **o catálogo de benefícios e as regras de adesão** (fatia 1 trouxe o catálogo; telas 44, 45 e 7.3).
