# Ficha da fatia 3: ponto (front-end)

Contrato lido: `contrato\fontes\30-fatia-3-ponto.yaml` (a versão do contrato é o `info.version` de `contrato\openapi.yaml`; confira-a antes de começar e anote-a no `PROGRESSO.md`), `contrato\CONVENCOES.md` (seções 3, 9, 10, 11 e 12), `contrato\ELENCO.md` e `contrato\PROVISORIOS.md`. Notas de tela: `docs\plano-front-end\telas\fatia-3.md` (leia a seção "Regras comuns" primeiro). Caminhos de tela: `docs\plano-front-end\05-caminhos-de-tela.md` (tabela única; inclui o extrato completo, os atestados do RH e o modal 13 aberto por `?pedido=<id>`).

Esta ficha **não traz passo a passo fino**. Repita o padrão da fatia 1b (`fatia-1b-telas.md`): um passo pequeno por vez, cada um com um comando que prova, `PROGRESSO.md` atualizado a cada passo, commit e push por passo na sua branch.

## 1. Objetivo e entrega demonstrável

O funcionário bate o ponto, vê o espelho do mês e o banco de horas, pede ajuste com justificativa e envia atestado. O gestor vê o ponto da equipe no dia, o espelho de cada pessoa e analisa os pedidos de ajuste (primeira etapa). O RH vê o espelho de qualquer pessoa, corrige batidas direto, decide os pedidos (segunda etapa), valida atestados, cuida dos feriados municipais e exporta o mês para a folha.

Demonstração (dados simulados, "hoje" = quarta, 2026-10-07, 09:42, como em `ELENCO.md`): quem ainda não tem batida hoje registra a entrada; Carlos Ribeiro aprova o pedido 303 da Júlia Nunes; Sofia Ramos (RH) aprova o 301 e o ponto de 02/10 do Bruno Lima é corrigido; Carla Mendes envia o atestado 43 e o RH o valida.

## 2. Telas

**8 telas com print**, mais o **cartão "Seu ponto hoje" da tela 32** (o resto da tela 32 é da fatia 7d) e **telas em texto** (sem print).

| NN | Nome | Perfil | PNG (`Figma\telas\`) | Incremento |
|---|---|---|---|---|
| 04 | Ficha do colaborador, aba Ponto | RH | `04-rh-colaboradores-ficha-aba-ponto.png` | 3a |
| 12 | Ponto e feriados | RH | `12-rh-ponto-e-feriados.png` | 3a e 3c |
| 13 | Modal "Pedidos de ajuste de ponto" | RH | `13-rh-ponto-e-feriados-modal-pedidos-de-ajuste.png` | 3b |
| 32 | Início: cartão "Seu ponto hoje" e aviso de pedido em análise (o resto é da 7d) | Funcionário | `32-func-tela-inicial.png` | 3a |
| 33 | Meu ponto | Funcionário | `33-func-meu-ponto.png` | 3a e 3b |
| 47 | Meu ponto (reutiliza a 33 com o menu do gestor) | Gestor | `47-gestor-meu-ponto.png` | 3a e 3b |
| 54 | Ponto da equipe | Gestor | `54-gestor-ponto-da-equipe.png` | 3a |
| 55 | Modal "Pedido de ajuste de ponto" | Gestor | `55-gestor-ponto-da-equipe-modal-pedido-de-ajuste.png` | 3b |
| 56 | Espelho de ponto da pessoa | Gestor | `56-gestor-ponto-da-equipe-espelho-da-pessoa.png` | 3a |

Contagem: 04, 12, 13, 33, 47, 54, 55 e 56 são as 8 telas do roadmap; a 32 entra só pelo cartão.

**Telas sem print (descrição em texto em `telas\fatia-3.md`; o desenho é meu):**

| Tela | Perfil | Observação |
|---|---|---|
| Pedir ajuste de ponto (modal) | Funcionário, Gestor e RH | Abre de "Pedir ajuste" (33 e 47) e do link do cartão (32). |
| Enviar atestado e "Meus atestados" | Funcionário, Gestor e RH | O Figma só tem o botão "Enviar atestado" (32, 33 e 47). |
| Extrato completo do banco de horas | Funcionário, Gestor e RH | Abre de "Ver extrato completo" (33 e 47) e "Ver extrato" (32). |
| Corrigir batidas do dia (modal "Editar") | RH | O botão "Editar" está no print 12; o modal não. |
| Atestados (validar e recusar) | RH | Sem print. |
| Atestados da equipe e aba Ponto da ficha da equipe | Gestor | A aba existe no print 52 (fatia 1b, só a aba Dados tem print). |

## 3. Rotas do contrato por tela

34 rotas da fatia (33 têm tela; `obterBancoDeHorasDeFuncionario` fica sem tela por decisão desta ficha, última linha da tabela; ver a conferência no fim de `telas\fatia-3.md`).

| Tela | `operationId` |
|---|---|
| 32 (cartão) | `obterPontoHoje`, `registrarMarcacaoDePonto`, `listarMeusPedidosDeAjuste` (com `status=PENDENTE`, para o aviso), `obterMeuPedidoDeAjuste` ("Ver pedido"), `obterMeuBancoDeHoras` ("Ver extrato") |
| 33 e 47 | `obterPontoHoje`, `registrarMarcacaoDePonto`, `obterMeuEspelhoDePonto`, `listarMeusPedidosDeAjuste`, `obterMeuBancoDeHoras` (`tamanhoPagina=4`), `criarPedidoDeAjuste`, `listarMeusAtestados`, `enviarAtestado` |
| Extrato completo (sem print) | `obterMeuBancoDeHoras` (com `de`, `ate`, `tipo`, paginação) |
| 04 | `obterEspelhoDePontoDeFuncionario` |
| 12 | `obterEspelhoDePontoDeFuncionario`, `baixarEspelhoDePontoPdf`, `exportarPontoParaFolha`, `listarFeriados`, `criarFeriado`, `editarFeriado`, `listarPedidosDeAjusteParaRh` (só para o número do botão), `corrigirMarcacoesDoDia` (modal sem print) |
| 13 | `listarPedidosDeAjusteParaRh`, `obterPedidoDeAjusteParaRh`, `aprovarPedidoDeAjusteComoRh`, `recusarPedidoDeAjusteComoRh` |
| Atestados do RH (sem print) | `listarAtestados`, `baixarAtestado`, `validarAtestado`, `recusarAtestado` |
| 54 | `obterPontoDaEquipe`, `listarPedidosDeAjusteDaEquipe`, `listarEquipeDoGestor` (fatia 1, nome do setor e total) |
| 55 | `obterPedidoDeAjusteDaEquipe`, `aprovarPedidoDeAjusteComoGestor`, `recusarPedidoDeAjusteComoGestor` |
| 56 e aba Ponto da ficha da equipe | `obterEspelhoDePontoDaPessoaDaEquipe`, `baixarEspelhoDePontoPdfDaEquipe` |
| Atestados da equipe (sem print) | `listarAtestadosDaEquipe`, `baixarAtestadoDaEquipe` |
| **Sem tela (decisão desta ficha)** | `obterBancoDeHorasDeFuncionario`: o contrato diz que "pode ficar sem tela" (o Figma não desenha extrato para o RH). **Não construa.** O MSW já cobre a rota. |

Rotas de outras fatias que estas telas **lêem**: `listarFuncionarios` (escolher o colaborador, tela 12), `obterContadoresDoMenuDoRh`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoFuncionario` (as bolinhas do menu: a fundação já as lê com `useContadoresDoMenu`; esta fatia só chama `invalidarContadoresDoMenu` depois de decidir ou criar pedido).

## 4. Regras e cortes que valem

Do design (seção 7), do contrato e do roadmap. O contrato é lei; onde o Figma diverge, vale o contrato.

**Cortes e mudanças decididos pelo dono (2026-10-30):**
- **Sem pedir folga com o banco de horas.** O extrato (33, 47 e o extrato completo) mostra só **Crédito** e **Débito**. A linha "Compensação, folga de meio período" do PNG 33 e 47 vira um Débito; a palavra "compensação" não aparece (teste de ausência: item 13 da seção 6).
- O **"Folga" de sábado e domingo** nas tabelas de ponto **continua**.
- **Esconder "Baixar espelho do setor"** (tela 54). O espelho **de uma pessoa** em PDF segue na tela 56.
- **O RH pode recusar o que o gestor aprovou** (contrato, `CONVENCOES.md` seção 9).
- **Aprovação em duas etapas**: o gestor primeiro e depois o RH; o gestor recusa e o pedido termina; pedido de quem é gestor, de setor sem gestor ou de gestor desligado só passa pelo RH (`etapa: RH`).
- **"Lançar no banco de horas"** (PNG 04) **não existe**: só lançamentos automáticos.
- **Sem unidades**: o campo "Vale para: Todas as unidades" do formulário de feriado (PNG 12) **sai**. O texto "adicionado por Ana Souza" também sai (o banco não guarda quem cadastrou).
- **E-mail**: a pessoa recebe e-mail a cada decisão do pedido (gestor e RH), mas **não** quando cria o pedido, nem quando o RH corrige o ponto direto, nem pelo atestado. O front não manda e-mail; só diz "Você recebe um e-mail quando houver resposta" onde o PNG diz.

**Regras do contrato que mudam a tela:**
- **O horário da batida é do servidor.** O relógio do navegador nunca decide nada (o cartão usa `agoraUtc`). O botão fica desligado durante a chamada (duplo clique); 409 `PONTO_BATIDA_DUPLICADA` e 409 `PONTO_SEQUENCIA_MUDOU` recarregam o cartão.
- **O front não calcula** saldo, horas, atraso, carga nem etapa. Só **formata** o que vem (minutos, datas em `America/Sao_Paulo`, rótulos). Quem pode decidir um pedido vem em `podeDecidir` e o motivo em `motivoNaoPodeDecidir` (cinco textos fixos): habilite ou desabilite os botões por eles, **nunca adivinhe**.
- **Tela 13 diverge do Figma:** o PNG mostra "Aprovar e corrigir o ponto" e "Recusar" ativos mesmo em pedido "Com o gestor". Pelo contrato, os botões só ficam ativos quando `podeDecidir` é `true` (depois do gestor), e o texto de `motivoNaoPodeDecidir` aparece ao lado.
- **Pedido só de dia que já passou.** O PNG 13, 33, 54 e 55 mostra pedido criado no mesmo dia do ajuste; no contrato isso é 422 `PONTO_PEDIDO_DIA_NAO_PASSOU`. O campo "Dia do ponto" aceita até **ontem**.
- **Um pedido em análise por dia** (409 `PONTO_PEDIDO_JA_EM_ANALISE`). Um pedido cobre um dia e pode ter **várias batidas** (1 a 10 itens).
- **A pessoa vê "Aguardando a análise do RH"**, sem nome de quem do RH (o banco não guarda). O PNG 33 e 47 mostra "Aguardando a análise de Marina Costa" (o gestor, que **existe** no contrato na etapa `GESTOR`) e "...de Roberto Alves" (uma pessoa do RH, que **não** existe no contrato).
- **Atestado** (dado de saúde): o gestor **vê o arquivo da própria equipe** e **só o RH valida ou recusa**; o RH recusa **com motivo** obrigatório; o acesso ao arquivo fica registrado na auditoria (a tela não faz nada). O arquivo é PDF, JPG ou PNG até 5 MB. Nunca guardar nem mostrar diagnóstico.
- **Duas "situações" que não se misturam** (`CONVENCOES.md` seção 10): `SituacaoHoje` (cartões e listas de hoje, tela 54) e `PontoDiaSituacao` (a linha de um dia no espelho). A tela 54 usa as duas.
- **Datas e horas**: a API fala UTC (`...Z`); a tela mostra em `America/Sao_Paulo`. O horário que a pessoa digita ("13:05" de um dia) vira instante em UTC pelo fuso fixo `-03:00` (o contrato pede "convertido de -03:00 para UTC"). `expiraEmUtc` e os instantes de fim de dia (`...02:59:59Z`) caem no dia **anterior** em Brasília: nunca formate pela data em UTC.
- **Perfis nas rotas**: `/api/eu/...` serve a qualquer perfil; `/api/gestor/...` só ao perfil Gestor (quem é do RH e só está em `GESTORES` para decidir pedidos de gestores **não** usa essas rotas: 403); `/api/rh/...` só ao RH.

**Decisões ainda abertas (`x-provisorio`, construa normalmente):**
- `paths > /api/eu/ponto/marcacoes > post`: jornada que atravessa a meia-noite (assumido 12 horas para a próxima batida contar como saída do dia anterior). Muda só `proximaMarcacao`.
- `paths > /api/eu/ponto/pedidos-de-ajuste > post`: prazo máximo para pedir ajuste de dia antigo e ponto retroativo (hoje: qualquer dia passado serve).
- `paths > /api/eu/ponto/banco-de-horas > get`: hora extra e tolerância de atraso (todo saldo positivo vira Crédito; a tolerância só marca o atraso).
- `paths > /api/rh/ponto/folha/exportacao > get`: formato do "exportar para a folha" (CSV de uma linha por pessoa descrito no contrato).
- `components > schemas > PontoDiaSituacao` e `PontoEspelhoDia > properties > atrasoMinutos`: quando um dia com batidas é "incompleto" e como a tolerância entra. O PNG 55 e 56 mostra o chip "volta sem registro" para um dia com só entrada e saída da manhã; o contrato **não** gera esse texto (esse dia vem como `NORMAL`).

## 4b. Decisão do dono sobre o menu (nenhuma dúvida nova)

**"Meu ponto" no menu do RH. Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes a dúvida `D-menu`): o RH ganha o grupo **"Meu espaço"** no fim do menu, e **"Meu ponto"** é um dos itens, **ativo**, levando a `/eu/meu-ponto` (página para os três perfis, `05-caminhos-de-tela.md`). O gestor já tem "Meu ponto" no menu do Figma (PNG 47), em "Seu dia a dia". Não há dúvida nova nesta ficha.

## 5. Ordem de construção e dependências

**Usa da fundação (fatia 1a) e da fatia 1b:** cliente de API e dados simulados gerados do contrato; menu e rotas por perfil; ficha do colaborador com abas (tela 03; a aba Ponto entra aqui); lista de colaboradores e `listarFuncionarios` (escolher o colaborador); `listarEquipeDoGestor` (equipe do gestor); componentes de modal, painel lateral, abas, chip, cartões de número, tabela, campo de data, componente de envio de arquivo, auxiliares de download (`abrirLinkTemporario` para o PDF do espelho e o arquivo de atestado; `baixarExportacao` para o CSV da folha, `src\shared\arquivos\baixar.ts`) e formatação de datas em Brasília. **Não** depende das fatias 4 a 6. As bolinhas do menu **já vêm da fundação** (`useContadoresDoMenu`, valores simulados até a API); os cartões da tela inicial são da fatia 7.

Ordem sugerida, um passo por item (cada um com teste):
1. **Peças de apoio** (usadas em todas as telas). **Os formatadores já existem na 1a** (`src\shared\formato\`; `fatia-1a-fundacao.md`, Task 2): `formatarMinutosComoHoras(min, { sinal: true })` ("8h54", "+0h06", "−0h02" com o sinal U+2212), `formatarHora`, `formatarDataHora`, `formatarDiaMes` (instante UTC para Brasília), `formatarData` (dia de calendário, sem `Date`) e `plural`; use-os, **não os recrie**. Se a tela precisar de um formato que falta, acrescente-o **ao mesmo arquivo** e ao teste dele: o caso do **zero com `sinal`** ("0h00", sem sinal; a 1a não o fixa) e o **"horário do dia mais data" para instante UTC** (`-03:00`; nem a 1a nem a 1b o têm, então ele nasce **aqui**, em `datas.ts`, e as fatias 4 e seguintes o reaproveitam). O que é só desta fatia: a etiqueta de cada `PontoDiaSituacao` e o chip de período ("07:56 - 12:01", "em aberto", "sem saída"). Detalhe em `telas\fatia-3.md`, "Regras comuns".
2. **Cartão "Seu ponto hoje"** (`obterPontoHoje` e `registrarMarcacaoDePonto`) com relógio do servidor e os erros 409 e 422. É o primeiro fluxo de ponta a ponta.
3. **Meu ponto (33 e 47)**: cabeçalho, quatro números, "Registros de outubro" (`obterMeuEspelhoDePonto`), "Banco de horas" (últimos 4 lançamentos).
4. **Pedir ajuste (modal)** e **Meus pedidos de ajuste** (`criarPedidoDeAjuste`, `listarMeusPedidosDeAjuste`, `obterMeuPedidoDeAjuste`), com os erros 409 e 422 do contrato.
5. **Atestado do funcionário** (`enviarAtestado`, `listarMeusAtestados`) e o **extrato completo** do banco de horas.
6. **RH, espelho (tela 12 e aba Ponto da tela 04)**: escolher colaborador e período, cartões, tabela, baixar PDF.
7. **RH, feriados e exportar para a folha** (`listarFeriados`, `criarFeriado`, `editarFeriado`, `exportarPontoParaFolha`).
8. **RH, pedidos de ajuste (tela 13)**: lista, detalhe, aprovar e recusar, com `podeDecidir`. Depois, **corrigir batidas do dia** (`corrigirMarcacoesDoDia`).
9. **RH, atestados** (listar, ver arquivo, validar, recusar).
10. **Gestor, ponto da equipe (tela 54)**, **modal do pedido (55)**, **espelho da pessoa (56)** e a **aba Ponto da ficha da equipe**.
11. **Gestor, atestados da equipe** (só ver o arquivo).
12. Passe final: conferir que cada tela que cria ou decide pedido chama `invalidarContadoresDoMenu` (as bolinhas do menu já vêm da fundação; os cartões da tela inicial dependem da fatia 7) e a lista de telas para conferência visual.

## 6. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura confere as **34 operações** da seção 3, **menos** `obterBancoDeHorasDeFuncionario` (sem tela por decisão desta ficha). O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Exemplos nomeados** (`exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`; ver `03-contrato-e-mocks.md`; não copie o JSON à mão): `registrarMarcacaoDePonto` 409 `sequenciaMudou` e `duplicada`, 422 `funcionarioInativo`; `criarPedidoDeAjuste` 422 `diaNaoPassou`, `instanteNoFuturo`, `instanteForaDoDia`, `sequenciaInvalida`; `obterMeuPedidoDeAjuste` 200 `esperaOGestor`, `esperaORh`, `concluido`; `obterPedidoDeAjusteParaRh` 200 `comOGestor` (e os demais da tela 13); `aprovarPedidoDeAjusteComoRh` e `recusarPedidoDeAjusteComoRh` 409 `jaDecidido`, `etapaIncorreta`, 422 `rhForaDeGestores`, `pedidoProprio`; `corrigirMarcacoesDoDia` 409 `pedidoEmAnalise`, `marcacaoEmUso`, `batidaDuplicada`, 422 `sequenciaInvalida`; `listarAtestados` 200 `aValidar`.

Testes com MSW que precisam existir (use os `example` do contrato e as pessoas e números de `ELENCO.md`):
1. **Bater ponto**: o cartão mostra o botão de `proximaMarcacao.textoDoBotao` (o exemplo de `obterPontoHoje` tem o dia vazio: "Registrar entrada"); um clique envia `{ tipoEsperado }` **uma vez** (segundo clique durante a chamada é ignorado); 409 `PONTO_SEQUENCIA_MUDOU` e `PONTO_BATIDA_DUPLICADA` recarregam o cartão e mostram o `detail`; 422 `FUNCIONARIO_INATIVO` mostra o `detail`. Uma batida às 22:30 de Brasília (`...T01:30:00Z` do dia seguinte) aparece como "22:30" no dia certo. Para o cartão com a entrada já batida (como no PNG 32), o teste sobrescreve o exemplo com `servidor.use(...)`.
2. **Meu ponto**: as linhas do mês do exemplo de `obterMeuEspelhoDePonto` (01/10 "+0h06", 02/10 "Ajuste em análise", 03 e 04/10 "Folga", 05/10 "−0h02" com botão "Pedir ajuste", 06/10 "+0h12", 07/10 "Falta compensada": o exemplo do contrato segue `ELENCO.md`, em que o Bruno Lima tem o atestado 41 validado hoje; o PNG mostra "Hoje" para esse dia, que é dado de exemplo do Figma). O chip "Hoje" (`EM_ANDAMENTO`) é testado com `servidor.use(...)`. O saldo "+6h20" (380 minutos).
3. **Pedir ajuste**: com o dia, as batidas e a justificativa do exemplo, a tela envia `data`, `justificativa` (5 a 500) e `itens` com `instanteUtc` calculado de `-03:00`; os erros `PONTO_PEDIDO_DIA_NAO_PASSOU`, `PONTO_INSTANTE_NO_FUTURO`, `PONTO_INSTANTE_FORA_DO_DIA`, `PONTO_SEQUENCIA_INVALIDA` e 409 `PONTO_PEDIDO_JA_EM_ANALISE` mostram o `detail` junto do campo certo.
4. **Etapas na tela da pessoa**: um pedido na etapa `GESTOR` mostra "Aguardando a análise de {gestorResponsavel}"; na etapa `RH`, "Aguardando a análise do RH" (nunca um nome do RH).
5. **RH, tela 12**: trocar colaborador ou período refaz a chamada com `ano` e `mes`; "Exportar para a folha" usa o `ano` e o `mes` do período escolhido; "Baixar PDF" só liga com colaborador escolhido; o dia 06/10 mostra "Saída corrigida pelo RH em 07/10"; o dia 02/10 mostra "Revisar pedido" (e **não** "Editar"), e o clique abre o modal da tela 13 já nesse pedido (`/rh/ponto-e-feriados?pedido=<id>`; é o mesmo endereço que o painel da fatia 7 usa).
6. **RH, tela 13**: a lista abre com `status=PENDENTE`, `ordenarPor=CRIADO_EM` e `direcao=ASC`; o rodapé mostra "4 pedidos esperando o RH" (`resumo.comRh`); no pedido da etapa `GESTOR` os botões ficam desligados com o texto "Aguardando a análise do gestor."; no pedido 301 ficam ligados; aprovar e recusar enviam a `observacao` e atualizam a lista; 409 `PEDIDO_JA_DECIDIDO` e `PEDIDO_ETAPA_INCORRETA` e 422 `PEDIDO_RH_FORA_DE_GESTORES` e `PEDIDO_PROPRIO` mostram o `detail`.
7. **Corrigir batidas**: o corpo leva **todas** as batidas do dia, com o `id` das existentes (batida que não vem na lista é removida pela API); os 409 e 422 do contrato mostram o `detail`.
8. **Feriados**: a lista mostra "Nacional, importado automaticamente" sem botão "Editar" e o municipal com "Editar"; criar envia `{ nome, data, repeteTodoAno }`; 409 `FERIADO_DUPLICADO` e 422 `EMPRESA_NAO_CONFIGURADA` mostram o `detail`.
9. **Gestor, tela 54**: os quatro números (6, 1, 1, 2), a tabela "Hoje no setor" na ordem da API, "Saiu" quando `TRABALHANDO` com `periodoAberto: false`, e os cartões "Aguardando você". **Não** existe "Baixar espelho do setor" (a ausência se testa como no item 13). Dia passado (`data` no passado): `contagensHoje` é `null` e a tela mostra `situacaoDoDia`.
10. **Gestor, tela 55**: aprovar e recusar; 404 para pedido de outra equipe mostra "não encontrado"; depois de aprovar, o cartão some da lista e o número "Pedidos para analisar" diminui.
11. **Atestado**: o funcionário envia PDF, JPG ou PNG de até 5 MB (período de dias inteiros ou de horas); o RH valida e recusa com motivo; o gestor só vê o arquivo da equipe; 409 `ATESTADO_JA_DECIDIDO` mostra o `detail`.
12. **Teste de contrato** (a lacuna do `tsc`: chave extra ao lado da chave certa em `query` ou `params.path`): cada tela confere os parâmetros enviados. Os que importam: `obterMeuEspelhoDePonto` e as duas variantes de espelho (`ano`, `mes`); `obterMeuBancoDeHoras` (`tamanhoPagina=4` no bloco; `de`, `ate`, `tipo` no extrato); `listarPedidosDeAjusteParaRh` e `listarPedidosDeAjusteDaEquipe` (`status`, `etapa`, `ordenarPor`, `direcao`); `obterPontoDaEquipe` (`data`); `exportarPontoParaFolha` (`ano`, `mes`); `listarFeriados` (`ano`, `mes`).
13. **Ausência dos textos cortados** (o teste procura o texto **nos rótulos da tela** e **não** o encontra; é o que a seção 4 chama de "nunca constrói"). Método **já pronto na 1b**: `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` de `src\test\textosProibidos.ts` (**não os reescreva**) e as linhas desta fatia acrescentadas à tabela de `src\app\textosProibidos.test.tsx`; **limite de palavra** (`/\bcompensação\b/i`: "Falta compensada" é etiqueta legítima de `FALTA_COMPENSADA` e **não** casa) e **só nos rótulos de interface** dentro do contêiner da tela (`within(screen.getByRole('main'))`: títulos, botões, abas, `columnheader`, `label`, `legend`; o `rotulosDaTela` da 1b não lista `placeholder` nem `aria-label`: se a tela os usar com texto proibido, acrescente-os ao `rotulosDaTela` e ao teste dele), **nunca em células nem no texto vindo dos mocks** (a `descricao` de um lançamento do extrato é da API). Textos por tela: **33, 47 e extrato completo**: "Compensação"; **54**: "Baixar espelho do setor"; **04**: "Lançar no banco de horas"; **12** (formulário de feriado): "Vale para" e "Todas as unidades".
14. **Menu "Meu espaço"** (decisão do dono, 2026-11-02): com o perfil RH, o grupo "Meu espaço" mostra "Meu ponto" e o clique leva a `/eu/meu-ponto` (a tela abre, com o cartão "Seu ponto hoje" e o menu do RH ainda à vista); com o perfil Gestor, "Meu ponto" continua em "Seu dia a dia" e leva ao mesmo endereço. O teste usa `menuVisivel`/`todosOsCaminhosDoMenu` da 1a e `renderizarTela`.

Testes que montam `multipart` (atestado) rodam com `// @vitest-environment node` na primeira linha.

**Passos novos do `csp`:** **nenhum** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário; o PDF do espelho, o arquivo de atestado e o CSV da folha usam os dois auxiliares de download da 1a).

### Pendente de integração

As **34 rotas** estão `planejada` em `contrato\STATUS.md`. Enquanto não estiverem `pronta`, tudo roda com MSW e o `PROGRESSO.md` registra "pendente de integração", com o **roteiro pronto** (a sessão **não roda** a API real; a pessoa ou o dono roda o roteiro depois), para: bater ponto de verdade (hora do servidor), o PDF do espelho, o CSV da folha, o envio e o download de atestado, o e-mail de cada decisão, o "gestor ausente" passando pedidos para o RH, e as bolinhas do menu.

## 7. Riscos

- **Fuso**: erro clássico é formatar o dia pela data em UTC. Teste com batida às 22:30 e com `...02:59:59Z`, e rode esses testes também sob outro `TZ` (a prova "outro fuso" da fundação, `fatia-1a-fundacao.md`, Task 2, com o controle que confere o deslocamento sob `TZ`): sem isso, eles passam por acaso numa máquina que já está em São Paulo.
- **Relógio**: o cartão usa o relógio do servidor; o relógio local do navegador pode estar adiantado ou atrasado. Mantenha a diferença (servidor menos navegador) só para animar os segundos.
- **Duplo clique** e abas duplicadas ao bater ponto; e ao criar pedido e ao decidir pedido.
- **Estado dos pedidos** (`status`, `etapa`, `decisaoGestor`, `decisaoRh`, `podeDecidir`): é o ponto mais fácil de errar. Use sempre `podeDecidir` e `motivoNaoPodeDecidir`.
- **Correção de batidas pelo RH**: mandar a lista **completa** (batida que não vai na lista é apagada). Teste com o dia 06/10 do Bruno.
- **Tabelas largas** (tela 12 e 54) em 1366 px (`04-visual.md`).
- **Arquivos sob a política de segurança**: o PDF do espelho e o arquivo de atestado chegam por link temporário; o CSV da folha chega direto (bytes) com `Authorization` (`contrato\CONVENCOES.md`, seção 4). O download por `Blob` e a navegação ao link **são medidos** pela sonda do script `csp` (fundação 1a, Task 13; `04-visual.md`), que a prova padrão já roda: não há o que confirmar à parte.
- **Dados de saúde**: atestado só aparece para quem pode; nunca coloque nome do arquivo ou conteúdo em `localStorage` nem no console.
- **Telas sem print**: o desenho é meu; a pessoa confere uma vez, junto com as telas desta fatia.

## 8. Repita o padrão da fatia 1b

Mesma estrutura de pastas, mesmos hooks gerados do contrato, mesmos componentes, mesmo ciclo de cada passo (teste que falha, implementação, teste passa, lint e `format:check`, `PROGRESSO.md` com a **versão do contrato** de cada tela, commit e push). No relatório final, liste para a pessoa conferir ao lado do print: 04, 12, 13, 33, 47, 54, 55, 56 e o cartão da 32; e, **sem print**, as telas em texto desta ficha.
