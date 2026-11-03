# Notas de tela da fatia 3: ponto

Fontes lidas: `contrato\fontes\30-fatia-3-ponto.yaml` (todas as 34 operações e os schemas), `contrato\CONVENCOES.md` (seções 3, 9, 10, 11 e 12), `contrato\ELENCO.md`, `contrato\PROVISORIOS.md`, o design dos planos (seção 7), o roadmap (seção "Fatia 3: ponto"), o spec (13.3) e os PNG 04, 12, 13, 32, 33, 47, 54, 55 e 56 em `Figma\telas\`, mais os PNG 46 e 52 só para ver o menu e as abas do gestor.

Marcas usadas: **(li)** = li no PNG ou no arquivo; **(contrato)** = está no contrato; **(desenho meu)** = a tela não existe no Figma e a descrição é a proposta deste plano, em estilo simples; **(suponho)** = inferência minha, a confirmar na conferência visual.

**Caminhos de tela** (onde cada tela mora, o que é painel e o que é modal, de onde cada uma abre): `05-caminhos-de-tela.md`, a tabela única. Os pontos desta fatia: Meu ponto `/eu/meu-ponto`; extrato completo `/eu/meu-ponto/extrato`; Ponto e feriados `/rh/ponto-e-feriados`; o modal da tela 13 abre por `?pedido=<id>` (ou `?pedidos=abertos`) nessa mesma rota; atestados do RH `/rh/ponto-e-feriados/atestados`; Ponto da equipe `/gestor/ponto-da-equipe`, com a tela 55 em `/gestor/ponto-da-equipe/pedidos/:pedidoId` e a 56 em `/gestor/ponto-da-equipe/espelho/:funcionarioId`.

Cobertura: 8 telas com print (04, 12, 13, 33, 47, 54, 55, 56), o cartão da 32, **6 telas em texto** (a última descreve duas peças do gestor: a aba Ponto da ficha da equipe e os atestados da equipe) e **34 de 34 rotas** listadas (33 com tela; conferência no fim, com uma exceção declarada: `obterBancoDeHorasDeFuncionario` fica sem tela por decisão da ficha).

---

## Regras comuns da fatia 3

Valem para todas as telas abaixo. Os componentes devem nascer **uma vez**, nesta fatia, e ser reutilizados.

### Formatar duração (a API fala em minutos inteiros)

- **Já existe na 1a:** `formatarMinutosComoHoras(min, { sinal: true })` (`src\shared\formato\numeros.ts`; `fatia-1a-fundacao.md`, Task 2), com o sinal de menos U+2212, além de `formatarHora`, `formatarDataHora`, `formatarDiaMes`, `formatarData` e `plural`. **Use-os; não os recrie.** O que a 1a não fixa e esta fatia precisa (o zero com `sinal`, "0h00"; e o "horário do dia mais data" para instante UTC) se acrescenta **ao mesmo arquivo** e ao teste dele (ficha 3, passo 1).
- Mostre `{horas}h{minutos com 2 dígitos}`: 534 vira "8h54"; 540, "9h00"; 1600, "26h40"; 380, "6h20"; 6, "0h06". Confira nos PNG: 8h54, 9h00, 26h40, 6h20 (li).
- **Saldo** leva sinal: "+0h06", "−0h02" (use o caractere de menos "−", como nos PNG, e o mesmo no código e nos testes). **Zero** é "0h00", sem sinal (PNG 54 e 56, li). **`null`** é "—" (travessão).
- Cor: saldo positivo em verde-azulado, negativo em marrom/âmbar, como nos PNG (as cores exatas estão em `04-visual.md`).

### Datas e horas (a API fala UTC com `Z`; a tela mostra Brasília)

- Instante para hora: `HH:mm` em `America/Sao_Paulo`. "Pedido em 02/10, 18:05" é `dd/MM, HH:mm`.
- Dia de calendário (`date`, ex.: `2026-10-05`) **não** passa por conversão de fuso: é o dia como veio. Formate sem criar `Date` em UTC (risco de cair no dia anterior).
- `expiraEmUtc` e outros "fim de dia" (`...T02:59:59Z`) caem no dia **anterior** em Brasília: formate sempre pelo fuso.
- Dia da semana: `diaDaSemana` vem sem acento (`SEGUNDA`, `TERCA`, `QUARTA`, `QUINTA`, `SEXTA`, `SABADO`, `DOMINGO`). Mostre "segunda", "terça", "quarta", "quinta", "sexta", "sábado", "domingo" (li nos PNG 04, 12, 33). Na tela 13 aparece capitalizado ("Sexta, 02/10").
- **O que a pessoa digita** ("13:05" do dia 05/10) vira `instanteUtc` com o fuso fixo `-03:00` (o contrato diz "convertido de -03:00 para UTC", `PontoPedidoItemEnviar`): `2026-10-05T13:05:00-03:00` vira `2026-10-05T16:05:00Z`. Para batida "no dia seguinte" (jornada que atravessa a meia-noite), some um dia à data antes de converter.

### Chip de período (as batidas do dia)

- `PontoPeriodo` = uma entrada e a saída seguinte. Mostre `HH:mm - HH:mm` (PNG 04, 33, 56: "07:56 - 12:01").
- Saída vazia: **hoje** (`ehHoje`), "07:58 - em aberto" (cinza, PNG 33, li); **dia passado**, "13:02 - sem saída" (âmbar, PNG 33 e 04, li).
- Entrada vazia (saída sem entrada): "sem entrada - HH:mm" (suponho; o contrato prevê `entrada: null` e o PNG não mostra).
- Sem batida nenhuma: texto "Sem expediente" em `FOLGA` (PNG 33, li), "Nenhum registro" nas demais (PNG 54, li) e "—" nas colunas da tela 12.

### Etiqueta da situação do dia (`PontoDiaSituacao`)

| Valor da API | Etiqueta | Onde aparece no PNG |
|---|---|---|
| `NORMAL` | "Normal" (verde-azulado) | 04, 12, 56 |
| `ATRASO` | "Atraso de {atrasoMinutos} min" (âmbar). A tela 04 mostra só "Atraso" (li); use o texto com minutos | 04, 12 |
| `AJUSTE_EM_ANALISE` | "Ajuste em análise" (âmbar). O PNG 12 diz "Ajuste pendente"; vale o contrato | 04, 12, 33, 56 |
| `EM_ANDAMENTO` | "Hoje" (azul claro) em 04, 33 e 47; "Em andamento" na tela 12 (li) | 04, 12, 33, 47 |
| `FOLGA` | "Folga" (cinza) | 12, 33, 56 |
| `SEM_REGISTRO` | "Sem registro" (âmbar) | 56 |
| `INCOMPLETO` | "Incompleto" (âmbar) | sem etiqueta no Figma (contrato) |
| `FERIADO` | o `nomeFeriado` (ou "Feriado") | sem etiqueta no Figma (contrato) |
| `FERIAS` | "Férias" | sem etiqueta no Figma (contrato) |
| `FALTA_COMPENSADA` | "Falta compensada" | sem etiqueta no Figma (contrato) |

`atrasoMinutos` pode vir preenchido junto de outra situação; o chip segue **só** a `situacao`.

### Estados de qualquer tela desta fatia

- **Carregando**: esqueleto da tabela ou do cartão; botões de ação desligados.
- **Vazio**: texto curto no lugar da tabela (cada tela diz o seu).
- **Erro** (rede ou 500): "Não foi possível carregar. Tente de novo." com botão "Tentar de novo" e o `traceId` em letra pequena.
- **Sem permissão**: 401 leva ao login (fundação); 403 mostra "Você não tem permissão para ver esta tela."; 404 mostra "Não encontrado." (vale também para "fora do escopo").

### Downloads

- **Link temporário** (`LinkTemporario`: PDF do espelho, arquivo de atestado; só **arquivo guardado** vem assim): chame a rota, receba `{ url, expiraEmUtc }` e abra a `url` na hora com `abrirLinkTemporario(link)` da fundação (`src\shared\arquivos\baixar.ts`, o mesmo auxiliar do "Ver" documento da 1b). Nunca monte nem guarde a `url`.
- **Arquivo direto** (CSV da folha, `exportarPontoParaFolha`: **exportação CSV devolve os bytes**, `contrato\CONVENCOES.md`, seção 4): a resposta é o arquivo; use `baixarExportacao(chamada, nomePadrao)` da fundação, o mesmo auxiliar do "Exportar CSV" do log de auditoria.

### Depois de criar ou decidir um pedido

Invalide: a lista e o espelho afetados, `listarMeusPedidosDeAjuste`, e as bolinhas do menu com `invalidarContadoresDoMenu` (a fundação 1a já lê `obterContadoresDoMenuDoFuncionario`, `obterContadoresDoMenuDoGestor` e `obterContadoresDoMenuDoRh`; os valores reais chegam quando a API existir). O número "Ponto e feriados 4" do RH é `resumo.comRh`; o "Painel da equipe 2" do gestor inclui `resumo.comGestor`.

### Etiqueta do pedido de ajuste (RH e gestor, tela 13 e 54)

| Situação | Chip |
|---|---|
| `etapa: GESTOR` | "Com o gestor" (âmbar) |
| `etapa: RH` e `decisaoGestor.decisao: APROVADA` | "Aprovado pelo gestor" (verde-azulado); se `decisaoGestor.automatica`, "Aprovado automaticamente" |
| `etapa: RH` e `decisaoGestor: null` (pedido de gestor, de setor sem gestor ou de gestor desligado) | "Com o RH" |
| `etapa: CONCLUIDA` | "Aprovado" (`status: APROVADA`) ou "Recusado" (`REJEITADA`) |
| Na lista do gestor, pedido que ele pode decidir (`podeDecidir: true`) | "Aguardando você" (âmbar, PNG 54, li) |

### O que esta fatia nunca constrói

"Lançar no banco de horas"; "Baixar espelho do setor"; "Vale para: Todas as unidades" no feriado; "adicionado por {nome}" no feriado; a palavra "Compensação" no extrato; nome de pessoa do RH em "Aguardando a análise de ..."; botão de extrato do banco de horas para o RH (`obterBancoDeHorasDeFuncionario`).

---

## Tela 33: Meu ponto (funcionário)

**PNG:** `Figma\telas\33-func-meu-ponto.png` · **Incremento:** 3a e 3b · **Rotas:** `obterPontoHoje`, `registrarMarcacaoDePonto`, `obterMeuEspelhoDePonto`, `listarMeusPedidosDeAjuste`, `obterMeuBancoDeHoras`, `criarPedidoDeAjuste`, `listarMeusAtestados`, `enviarAtestado`.

**O que fica (li no PNG 33)**
- Título "Meu ponto" e subtítulo "Outubro de 2026. Jornada de 44 horas semanais, das 08:00 às 17:48." Vem de `obterMeuEspelhoDePonto` (`ano`, `mes`) e de `jornada` (`cargaSemanalMinutos` ÷ 60 = 44 horas; `horarioEntrada` e `horarioSaida`). Se `jornada` vier com campos `null`, omita a parte que faltar.
- Botões no topo, à direita: "Enviar atestado" e "Pedir ajuste" (contornados).
- **Cartão escuro** com "Hoje, quarta-feira 7/10", o relógio grande "09:42", dois chips "Entrada 07:58" e "Saída: aguardando registro" e o botão principal "Registrar saída". É a versão **reduzida** do cartão da tela 32 (o contrato: "a tela 33 mostra uma versão reduzida (só entrada e saída) com o mesmo dado"). Suponho: o chip de entrada é a primeira entrada do dia (ou "Entrada —"); o chip de saída mostra a última saída se o dia já fechou a 4ª batida, e "Saída: aguardando registro" nos outros casos. O texto do botão vem de `proximaMarcacao.textoDoBotao` (o PNG diz "Registrar saída"; com o dado do exemplo seria "Registrar saída para almoço": vale o contrato).
- **Quatro números**: "Banco de horas" (`resumo.saldoBancoHorasMinutos`, "+6h20"), "Horas trabalhadas no mês" (`resumo.horasTrabalhadasNoMesMinutos`, "26h40"), "Carga horária por dia" (`jornada.cargaDiariaMinutos`, "8h48") e "Pedidos em análise" (`resumo.pedidosEmAnalise`, "1", em âmbar).
- **"Registros de outubro"** (tabela; uma linha por `dias[]`, em ordem crescente, do dia 1 até hoje): colunas Dia ("01/10" e "quinta" embaixo), "Períodos registrados" (chips), "Horas trabalhadas" (`horasMinutos`, em negrito, "—" se `null`), "Carga horária" (`cargaMinutos`, "—" quando 0, **só aparece o valor nos dias com expediente**), "Saldo do dia" (`saldoMinutos` com sinal) e a coluna final: botão **"Pedir ajuste"** quando `podePedirAjuste: true`; senão o chip da `situacao` ("Ajuste em análise", "Folga", "Hoje"). Linhas de fim de semana têm fundo cinza claro e "Sem expediente" no lugar dos períodos.
- **"Meus pedidos de ajuste"** (tabela, à esquerda): colunas "Pedido em" (`criadoEmUtc`, "02/10, 18:05"), "Dia" (`data`, "02/10", em negrito), "Justificativa e resposta" e "Situação". A célula de justificativa mostra o texto e, em caixa cinza embaixo, a resposta (ver abaixo). Chips de situação: "Em análise" (âmbar, `PENDENTE`), "Aprovado" (verde-azulado, `APROVADA`), "Recusado" (vermelho, `REJEITADA`).
- **"Banco de horas"** (bloco à direita, com o saldo "+6h20" no topo): as quatro últimas linhas do extrato (`obterMeuBancoDeHoras` com `tamanhoPagina=4`): tipo ("Crédito" ou "Débito") em negrito, "06/10, hora extra" embaixo e o valor à direita ("+0h12", "−0h02"). Link **"Ver extrato completo"**.

**Resposta na célula "Justificativa e resposta" (contrato e PNG)**
- Etapa `GESTOR`: "Aguardando a análise de {gestorResponsavel.nome}" (PNG: "Aguardando a análise de Marina Costa", li).
- Etapa `RH`: "Aguardando a análise do RH" (sem nome de pessoa; ver abaixo).
- Etapa `CONCLUIDA`: "{nome de quem decidiu por último}: {observação}" (PNG: "Marina Costa: conferido com o controle da portaria.", li). Quem decidiu e a observação vêm de `decisaoRh` (ou de `decisaoGestor`, se o gestor recusou); sem observação, mostre "{nome} aprovou." ou "{nome} recusou." (suponho). Lembre que, quando o RH decide, a observação dele **substitui** a do gestor (contrato).

**O que sai ou muda (com a razão)**
- **Extrato só com "Crédito" e "Débito".** A linha "Compensação, 25/09, folga de meio período, −4h00" do PNG vira **"Débito"**, com a descrição que a API trouxer ("Saldo negativo do dia") e **sem** a palavra "compensação": decisão do dono (design, seção 7) porque o banco só tem `CREDITO` e `DEBITO` e não haverá pedido de folga com o banco de horas.
- **"Aguardando a análise do RH" em vez do nome de uma pessoa do RH** (PNG 47 mostra "Roberto Alves"; o PNG 33 mostra o gestor, que continua valendo na etapa `GESTOR`). O banco não guarda qual pessoa do RH vai decidir (contrato, `listarMeusPedidosDeAjuste`).
- **Pedido no mesmo dia** (PNG: "Pedido em 02/10, 18:05" para o "Dia 02/10"): no contrato todo pedido é criado **depois** do dia a que se refere. O campo "Dia do ponto" do modal vai só até ontem (ver "Pedir ajuste").
- **"Carga horária" em dia de folga** vem 0: mostre "—" (PNG, li).
- **Sem seletor de mês** no PNG. O contrato aceita `ano` e `mes`; como acréscimo mínimo **opcional** (desenho meu), reuse o campo "Período" da tela 12 ao lado do título. Se faltar tempo, deixe fora: o padrão é o mês atual.
- O atraso (`atrasoMinutos`) **não** aparece aqui (como no PNG); a tela 12 o mostra.
- **Meus atestados** (sem print): uma seção "Meus atestados" abaixo de "Meus pedidos de ajuste" (ver "Tela sem print: Enviar atestado").

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Cartão e botão "Registrar ..." | `obterPontoHoje`, `registrarMarcacaoDePonto` | `tipoEsperado` = `proximaMarcacao.tipo` |
| Subtítulo, quatro números e tabela do mês | `obterMeuEspelhoDePonto` | `ano` e `mes` opcionais |
| "Pedir ajuste" (topo e linha) | `criarPedidoDeAjuste` | ver o modal sem print |
| "Meus pedidos de ajuste" | `listarMeusPedidosDeAjuste` | do mais recente ao mais antigo; paginação |
| Bloco "Banco de horas" e "Ver extrato completo" | `obterMeuBancoDeHoras` | `tamanhoPagina=4` no bloco |
| "Enviar atestado" e "Meus atestados" | `enviarAtestado`, `listarMeusAtestados` | ver a tela sem print |

**Estados**
- Carregando: esqueleto nos números, na tabela e nos dois blocos de baixo.
- Vazio: mês sem dias ainda (`dias: []`, mês futuro): "Nada registrado neste mês." "Meus pedidos de ajuste" vazio: "Você ainda não pediu ajustes." Extrato vazio: "Nenhum lançamento ainda."
- Erro e sem permissão: ver "Estados de qualquer tela". Erro ao bater ponto: ver o cartão (tela 32).
- Banco de horas: se `atualizadoAte` for `null`, mostre "Banco de horas ainda não atualizado." no lugar do saldo atualizado.

**Textos e mensagens (contrato)**
- Batida: 409 `PONTO_SEQUENCIA_MUDOU` "O seu ponto mudou desde a última vez que você abriu a tela. Confira e tente de novo." (recarregue o cartão); 409 `PONTO_BATIDA_DUPLICADA` "O seu ponto já foi registrado agora há pouco."; 422 `FUNCIONARIO_INATIVO` "Este colaborador está inativo e não pode registrar ponto."; 422 do banco "A data do ponto e anterior a admissao." (o `detail` vem genérico: "A operação foi recusada por uma regra do banco de dados.").

**Observações para quem monta**
- Os números do topo e as linhas vêm **prontos**: não some nem divida nada no front.
- "Registrar ..." fica desligado durante a chamada.
- Ao registrar com sucesso, a resposta já traz o cartão atualizado (`PontoHoje`): use-a no cache e invalide o espelho do mês.
- O gestor e o RH usam esta tela com as mesmas rotas `/api/eu/...`.

**Perguntas abertas (`PROVISORIOS.md`)**
- `paths > /api/eu/ponto/marcacoes > post`: jornada que atravessa a meia-noite (assumido 12 horas).
- `paths > /api/eu/ponto/banco-de-horas > get`: hora extra e tolerância de atraso.
- `components > schemas > PontoDiaSituacao`: quando um dia com batidas é "incompleto".

---

## Tela 32: Início, cartão "Seu ponto hoje" e aviso de pedido em análise (funcionário)

**PNG:** `Figma\telas\32-func-tela-inicial.png` · **Incremento:** 3a · **Rotas:** `obterPontoHoje`, `registrarMarcacaoDePonto`, `listarMeusPedidosDeAjuste`, `obterMeuPedidoDeAjuste`, `obterMeuBancoDeHoras`.

**Só estes blocos são desta fatia.** O resto da tela inicial (cartões "Férias disponíveis" e "Próximo pagamento", "Seus benefícios", "Avisos", "Aniversariantes") é da fatia 7d (e os avisos e aniversariantes têm rota na fatia 4). Esta fatia entrega os blocos como **componentes** que a 7d só monta.

**O que fica (li no PNG 32)**
- Cartão escuro grande "Seu ponto hoje", com "Jornada das 08:00 às 17:48" à direita do título (de `jornada`), o relógio "09:42" e **quatro quadros**: "Entrada", "Saída almoço", "Volta" e "Saída". Cada quadro mostra a hora da batida de mesma `ordem` (1 a 4) ou "—". O quadro da **próxima** batida (`proximaMarcacao`) fica com borda clara (no PNG, "Saída almoço").
- Botão principal com o texto de `proximaMarcacao.textoDoBotao` ("Registrar saída para almoço"), link **"Pedir ajuste de ponto"** (abre o modal de pedido) e link **"Enviar atestado"** (abre o envio de atestado).
- Cartão branco "Banco de horas": "+6h20", "Atualizado até ontem" (de `bancoHoras.atualizadoAte`: "ontem" se for o dia anterior, senão "Atualizado até dd/MM") e link "Ver extrato" (abre o extrato completo).
- Faixa âmbar abaixo: "Seu pedido de ajuste da saída de 02/10 está em análise pelo RH. Você recebe um e-mail quando houver resposta." e o link "Ver pedido". Vem de `listarMeusPedidosDeAjuste` com `status=PENDENTE`. Texto sugerido a partir do contrato (suponho): "Seu pedido de ajuste do dia {dd/MM} está em análise {pelo gestor | pelo RH}. Você recebe um e-mail quando houver resposta." (`etapa`); se houver mais de um pedido, "Você tem {N} pedidos de ajuste em análise." "Ver pedido" abre `obterMeuPedidoDeAjuste` (detalhe, somente leitura) num **modal sem caminho**, aberto só por esta faixa (`05-caminhos-de-tela.md`, regra 4); ao fechar, a pessoa continua na tela inicial.

**O que sai ou muda (com a razão)**
- O PNG diz "da saída de 02/10" e "pelo RH" enquanto a tela 33 diz "de Marina Costa" (o gestor): é dado de exemplo do Figma; o texto segue a `etapa` do contrato.
- Os dados do PNG (entrada às 07:58 do Bruno Lima) **não** são os do exemplo do contrato (`obterPontoHoje` vem com o dia vazio e `ELENCO.md` põe o Bruno com atestado hoje). Os testes usam o exemplo e sobrescrevem com `servidor.use(...)` para ver o cartão com batidas.

**Estados**
- Carregando: o relógio mostra "--:--" até a primeira resposta; botão desligado.
- Erro ao bater: ver abaixo. Erro de leitura: "Não foi possível carregar o seu ponto." com "Tentar de novo".
- Dia sem expediente (sábado, domingo ou feriado): o cartão continua mostrando o botão (bater em feriado é aceito, contrato).

**Textos e mensagens (contrato)**
- Os mesmos da tela 33 para `registrarMarcacaoDePonto`.

**Observações para quem monta**
- **O relógio grande usa `agoraUtc`**: guarde a diferença entre o relógio do servidor e o do navegador no momento da resposta e use-a só para animar os segundos. Nenhuma regra (qual batida, se pode bater) depende do relógio do navegador.
- A cada clique em "Registrar ...": desligue o botão, envie `{ tipoEsperado }`, use o `PontoHoje` devolvido para atualizar o cartão.
- `dataDoPonto` pode ser o dia anterior (jornada que atravessa a meia-noite): mostre as batidas daquele dia no cartão.
- O mesmo cartão aparece na tela inicial do gestor (PNG 46, li) com o aviso "Você tem 2 pedidos de ajuste de ponto do setor esperando sua análise" (que é da 7d, via `obterContadoresDoMenuDoGestor`).

**Perguntas abertas**
- As mesmas `x-provisorio` da tela 33.

---

## Tela 47: Meu ponto (gestor)

**PNG:** `Figma\telas\47-gestor-meu-ponto.png` · **Incremento:** 3a e 3b · **Rotas:** as mesmas da tela 33 (rotas `/api/eu/...`).

**O que fica (li no PNG 47):** é **a mesma tela 33**, em tudo (cartão, quatro números, tabela do mês, pedidos, banco de horas), com o **menu do gestor** (Início, Meu ponto, Férias, Holerites; "Seu setor": Painel da equipe (2), Ponto da equipe, Formulários, Treinamentos, Advertências; "Comunicação": Avisos (2); rodapé "Carlos Ribeiro, Perfil gestor, setor Comercial"). Monte **uma só tela**, escolhida pelo perfil só no menu; não copie o código.

**O que sai ou muda (com a razão)**
- O PNG 47 mostra "Aguardando a análise de Roberto Alves" (pessoa do RH) e as respostas "Roberto Alves: conferido com o controle da portaria." Como o gestor **não tem etapa do gestor** (pedido de quem é gestor só passa pelo RH, contrato), o pedido dele fica em `etapa: RH` e a tela diz "Aguardando a análise do RH". O nome de uma pessoa do RH só aparece **depois** de decidido, em `decisaoRh.decididaPor`.
- Mesmos cortes e mudanças da tela 33 (extrato só com Crédito e Débito).

**Estados e textos:** os da tela 33.

**Observações:** o gestor usa `/api/eu/...` para o próprio ponto e `/api/gestor/...` só para a equipe; nunca misture.

**Perguntas abertas:** as da tela 33. O Figma não mostra "Meu ponto" no menu do RH; **decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o item **"Meu ponto"** existe no grupo **"Meu espaço"** do RH e leva a `/eu/meu-ponto`.

---

## Tela sem print: Pedir ajuste de ponto (modal; funcionário, gestor e RH)

**PNG:** nenhum (os PNG 32, 33 e 47 só têm os botões "Pedir ajuste" e o link "Pedir ajuste de ponto") · **Incremento:** 3b · **Rota:** `criarPedidoDeAjuste` (`POST /api/eu/ponto/pedidos-de-ajuste`).

**Descrição (desenho meu, apoiada em `criarPedidoDeAjuste`)** — modal "Pedir ajuste de ponto":
- **Dia do ponto**: campo de data obrigatório, **máximo ontem** (fuso de Brasília). Vem preenchido e fixo quando se abre pela linha do dia ("Pedir ajuste" na tabela); vem vazio quando se abre pelo botão do topo ou pelo link do cartão.
- **Registro atual do dia**: os chips de período do dia escolhido (de `obterMeuEspelhoDePonto`; se o dia for de outro mês, carregue aquele mês). Dia sem batida: "Nenhum registro neste dia."
- **Correção pedida**: lista de batidas (mínimo 1, máximo 10). Cada linha: **Tipo** (Entrada ou Saída), **Hora** (`HH:mm`), caixa pequena "No dia seguinte" (para jornada que atravessa a meia-noite) e, opcional, **"Corrigir uma batida deste dia"** (escolhe uma das batidas atuais: vira `marcacaoId`; nesse caso o tipo fica igual ao da batida, suposição). Dois botões: "Incluir uma batida" e "Incluir um período (entrada e saída)" (acrescenta duas linhas).
- **Justificativa**: caixa de texto, 5 a 500 caracteres, com contador.
- Texto de apoio: "O pedido passa pela análise do gestor (se a sua equipe tem gestor) e depois pelo RH. Você recebe um e-mail a cada resposta." (suponho a redação; o fato vem de `CONVENCOES.md`, seções 9 e 12).
- Botões "Cancelar" e "Enviar pedido".
- Ao enviar: converta cada hora digitada para `instanteUtc` (regra comum), envie `{ data, justificativa, itens: [{ tipo, instanteUtc, marcacaoId }] }`.

**O que sai ou muda:** a tela **não** valida a sequência de entrada e saída (a regra é da aplicação; `PONTO_SEQUENCIA_INVALIDA` vem do servidor) e **não** calcula horas.

**Mensagens (contrato, `detail` da API; mostre-as junto do campo certo)**

| Resposta | `codigo` | Texto |
|---|---|---|
| 409 | `PONTO_PEDIDO_JA_EM_ANALISE` | "Este dia já tem um pedido de ajuste em análise. Espere a resposta antes de pedir outro." |
| 422 | `PONTO_PEDIDO_DIA_NAO_PASSOU` | "Só dá para pedir ajuste de um dia que já passou. Para hoje, use o cartão "Seu ponto hoje"." |
| 422 | `PONTO_INSTANTE_NO_FUTURO` | "Uma das batidas pedidas tem horário que ainda não chegou. Confira os horários." |
| 422 | `PONTO_INSTANTE_FORA_DO_DIA` | "Cada batida precisa cair no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite)." |
| 422 | `PONTO_SEQUENCIA_INVALIDA` | "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários." |
| 422 | `FUNCIONARIO_INATIVO` | "Este colaborador está inativo e não pode pedir ajuste de ponto." |
| 422 | (regra do banco) | "A operação foi recusada por uma regra do banco de dados." (o texto técnico vem em `regraDoBanco`; não o mostre ao usuário) |
| 400 | (por campo) | `errors` por campo |

**Estados:** enviando (botão "Enviando..."); sucesso fecha o modal, mostra "Pedido enviado. Você recebe um e-mail a cada resposta." e atualiza espelho, "Meus pedidos de ajuste", o número "Pedidos em análise", o aviso da tela 32 e as bolinhas do menu.

**Observações:** o pedido nasce na etapa certa sem a tela saber (`GESTOR` ou `RH`; contrato); o resultado já mostra "Aguardando a análise de ..." ou "do RH".

**Pergunta aberta:** `paths > /api/eu/ponto/pedidos-de-ajuste > post`: prazo máximo para pedir ajuste de dia antigo e ponto retroativo (hoje: qualquer dia passado serve).

---

## Tela sem print: Enviar atestado e "Meus atestados" (funcionário, gestor e RH)

**PNG:** nenhum (os PNG 32, 33 e 47 só têm o botão e o link "Enviar atestado") · **Incremento:** 3b · **Rotas:** `enviarAtestado` (`POST /api/eu/ponto/atestados`), `listarMeusAtestados` (`GET /api/eu/ponto/atestados`).

**Descrição (desenho meu, apoiada no contrato)**
- **Onde:** botão "Enviar atestado" no topo de "Meu ponto" (33 e 47), link no cartão da tela 32 e uma seção **"Meus atestados"** abaixo de "Meus pedidos de ajuste" na tela 33 e 47.
- **Modal "Enviar atestado"**:
  - Escolha "Dia(s) inteiro(s)" ou "Algumas horas".
  - Dia(s) inteiro(s): "De" e "Até" (datas; `dataFim` não pode ser antes de `dataInicio`; um só dia = datas iguais).
  - Algumas horas: uma "Data" (`dataInicio` = `dataFim`), "Das" e "Até" (`horaInicio` e `horaFim`, `HH:mm`, a segunda depois da primeira). Só esse período é compensado.
  - **Arquivo**: área de envio no estilo do holerite (PNG 08, li): "Arraste o arquivo aqui", "PDF, JPG ou PNG, até 5 MB", "Escolher arquivo".
  - Texto de apoio: "O RH e o seu gestor veem o arquivo. O sistema não guarda diagnóstico." (o gestor vê o arquivo **da própria equipe**; contrato).
  - Botões "Cancelar" e "Enviar atestado".
- **Lista "Meus atestados"** (mais recente primeiro; filtro opcional por situação): colunas "Período" ("07/10 a 08/10" ou "25/09, das 13:00 às 17:48"), "Arquivo" (`nomeArquivo`), "Enviado em" e "Situação": **"Aguardando o RH"** (`ENVIADO`), **"Validado"** (`VALIDADO`, com a dica "O dia não conta como falta") e **"Recusado"** (`RECUSADO`, com o `motivoRecusa` embaixo e a frase "O dia segue como falta"). **Nunca** link para o arquivo aqui.

**O que sai ou muda:** não há campo de diagnóstico (CID). Não há e-mail para o atestado (lista fechada, `CONVENCOES.md`, seção 12).

**Estados:** lista vazia "Você ainda não enviou atestados."; erros de campo (400, `errors`): fim antes do início, horas sem o par, horas com mais de um dia, arquivo de tipo ou tamanho não aceito.

**Observações:** `multipart/form-data`; o teste roda com `// @vitest-environment node`. O atestado de dias inteiros validado faz o espelho mostrar "Falta compensada".

---

## Tela sem print: Extrato completo do banco de horas (funcionário, gestor e RH)

**PNG:** nenhum (o PNG 33 e 47 tem o link "Ver extrato completo"; o 32 tem "Ver extrato") · **Incremento:** 3c · **Rota:** `obterMeuBancoDeHoras` (`GET /api/eu/ponto/banco-de-horas`).

**Descrição (desenho meu):** painel lateral "Banco de horas" sobre Meu ponto (`/eu/meu-ponto/extrato`; o link "Ver extrato" da tela 32 leva ao mesmo endereço).
- Topo: saldo atual ("+6h20") e "Atualizado até {dd/MM}" (`atualizadoAte`; `null`: "ainda não atualizado").
- Filtros: **Período** ("De" e "Até", `de` e `ate`) e **Tipo** (Todos, Crédito, Débito; `tipo`).
- Tabela, do mais recente ao mais antigo (ordem fixa da API): "Dia" (`diaReferencia`), "Tipo" ("Crédito" ou "Débito"), "Descrição" (`descricao`: "Hora extra", "Saldo negativo do dia" ou "Falta") e "Quantidade" (`quantidadeMinutos` com sinal pelo tipo: Crédito "+", Débito "−"). Paginação.
- O `saldoMinutos` é o **total** (não só dos lançamentos filtrados).
- **Sem** a palavra "compensação" nem lançamento manual (decisão do dono; contrato).

**Estados:** vazio "Nenhum lançamento neste período."; erro e carregando como nas regras comuns.

---

## Tela 04: Ficha do colaborador, aba Ponto (RH)

**PNG:** `Figma\telas\04-rh-colaboradores-ficha-aba-ponto.png` · **Incremento:** 3a · **Rota:** `obterEspelhoDePontoDeFuncionario` (`GET /api/rh/funcionarios/{funcionarioId}/ponto`).

A moldura da ficha (cabeçalho "Bruno Lima", abas, botões "Editar dados" e "Inativar") é da **fatia 1b** (tela 03). Esta fatia entrega só o **conteúdo da aba Ponto**.

**O que fica (li no PNG 04)**
- Três números: "Banco de horas" (`resumo.saldoBancoHorasMinutos`, "+6h20"), "Horas em outubro" (`resumo.horasTrabalhadasNoMesMinutos`; o mês do rótulo é o do espelho aberto) e "Pedidos de ajuste em análise" (`resumo.pedidosEmAnalise`, âmbar).
- Tabela do mês atual: "Dia" ("01/10", "quinta"), "Períodos registrados" (chips), "Horas" (`horasMinutos`, negrito, "—"), "Saldo" (`saldoMinutos` com sinal) e "Situação" (chip, ver "Regras comuns").
- **Sem** sábados e domingos (o PNG pula 03 e 04/10; contrato: "A aba da ficha mostra os mesmos dados sem os sábados e domingos"). Suponho: esconda as linhas com `situacao: FOLGA`, assim um sábado trabalhado (com batidas) não some.
- Botão **"Abrir espelho completo"** (contornado): leva à tela 12 com este colaborador e este mês já escolhidos (`/rh/ponto-e-feriados?funcionarioId=<id>&ano=<aaaa>&mes=<m>`).

**O que sai ou muda (com a razão)**
- **"Lançar no banco de horas" sai** (PNG 04, li): o banco de horas só tem lançamento automático (decisão do dono, roadmap, lista A e design seção 7).
- O contrato aceita `ano` e `mes`; o PNG não tem seletor de mês na aba: mostre o **mês atual** e use "Abrir espelho completo" para outros meses.
- Sem os botões "Editar" e "Revisar pedido" aqui (o PNG não os tem; eles ficam na tela 12).

**Estados:** carregando (esqueleto); vazio do mês (`dias: []`): "Nada registrado neste mês."; pessoa em admissão (admissão no futuro): `dias: []` (contrato: dias antes da admissão não aparecem).

**Observações:** `funcionarioId` vem da ficha. O gestor tem uma aba equivalente (ver "Aba Ponto da ficha da equipe").

---

## Tela 12: Ponto e feriados (RH)

**PNG:** `Figma\telas\12-rh-ponto-e-feriados.png` · **Incremento:** 3a e 3c · **Rotas:** `obterEspelhoDePontoDeFuncionario`, `baixarEspelhoDePontoPdf`, `exportarPontoParaFolha`, `listarFeriados`, `criarFeriado`, `editarFeriado`, `listarPedidosDeAjusteParaRh` (só o número do botão), `corrigirMarcacoesDoDia`.

**O que fica (li no PNG 12)**
- Título "Ponto e feriados" e subtítulo "Espelho de ponto de cada colaborador e feriados da empresa".
- Dois botões no canto: **"Pedidos de ajuste"** com um número ao lado (4; é `resumo.comRh`, o mesmo da bolinha "Ponto e feriados" do menu) que abre o modal da tela 13 na lista (`?pedidos=abertos`), e **"Exportar para a folha"**.
- Campos **"Colaborador"** (busca por nome; lista de `listarFuncionarios`) e **"Período"** (mês e ano, "Outubro de 2026"), e o botão **"Baixar PDF"** (`baixarEspelhoDePontoPdf` com `ano` e `mes`). Sem colaborador escolhido: o espelho mostra "Escolha um colaborador para ver o espelho." e "Baixar PDF" fica desligado. O PNG já traz "Bruno Lima" escolhido, mas isso é exemplo; ao vir da aba Ponto da ficha (tela 04), o colaborador e o mês já vêm escolhidos.
- Seção **"Espelho de ponto"** com quatro números: "Horas trabalhadas no mês" (`resumo.horasTrabalhadasNoMesMinutos`), "Banco de horas" (`saldoBancoHorasMinutos`), "Atrasos" (`resumo.atrasos`, âmbar) e "Faltas" (`resumo.faltas`).
- **Tabela**: "Dia" (dd/MM e dia da semana), "Entrada", "Saída almoço", "Volta" e "Saída" (as batidas de `ordem` 1 a 4 em `HH:mm`, "—" quando não há; se houver mais de quatro, mostre "+N batidas" na célula "Saída"), "Total" (`horasMinutos`, negrito), "Situação" (chip; ver "Regras comuns") e a coluna de ação:
  - botão **"Editar"** (contornado) nos dias sem pedido em análise e que não são folga;
  - botão principal **"Revisar pedido"** no dia com `pedidoEmAnaliseId` (abre o modal da tela 13 já nesse pedido: `?pedido=<pedidoEmAnaliseId>`);
  - **nenhum** botão nas linhas de folga (03/10 e 04/10, fundo cinza, "Folga").
- Sob o chip do dia, quando uma batida foi corrigida pelo RH, um texto pequeno **"{rótulo} corrigida pelo RH em {dd/MM}"** (PNG: "Saída corrigida pelo RH em 07/10"; vem de `origem: CORRIGIDA_RH` e `corrigidaEmUtc` da batida; rótulo = `rotulo` da batida: "Saída", "Volta"...).
- Em "Saída" aparece "Sem registro" em âmbar quando a batida falta (02/10 no PNG: entrada 08:03, saída almoço 12:00, volta 13:02, saída vazia). Suponho: período aberto (`saida: null`) mostra o texto "Sem registro" na célula da saída que falta.
- Seção **"Feriados deste mês"** (à esquerda): linhas com a data em destaque ("12/10") e o nome, e embaixo "Nacional, importado automaticamente" ou "Municipal"; botão **"Editar"** só nos municipais (`podeEditar`). Vem de `listarFeriados` com `ano` e `mes` do período escolhido.
- Cartão **"Adicionar feriado municipal"** (à direita): texto "Os feriados nacionais e estaduais entram sozinhos. Aqui você cadastra os da sua cidade.", campos "Nome do feriado" (`nome`, até 100; placeholder "Ex.: aniversário da cidade") e "Data" (`data`), caixa **"Repetir todos os anos"** (`repeteTodoAno`) e botão **"Salvar feriado"**.

**O que sai ou muda (com a razão)**
- **"Vale para: Todas as unidades" sai:** não há unidades (decisão do dono, 2026-10-28). Os feriados municipais valem para todos; UF e município vêm da configuração da empresa.
- **"adicionado por Ana Souza" sai:** o banco não guarda quem cadastrou (só a auditoria); o contrato não traz isso.
- **"Ajuste pendente" vira "Ajuste em análise"** (o contrato fixa o texto; o PNG 04, 33 e 56 usam "Ajuste em análise").
- **"Repetir todos os anos":** o PNG mostra a caixa marcada; o contrato tem `repeteTodoAno` com padrão `false` (valem os documentos). Deixe **desmarcada** por padrão (suponho que o PNG só mostra um exemplo).
- **O botão "Editar" do feriado** abre o mesmo cartão com os dados preenchidos ("Editar feriado") e acrescenta o controle **"Desativar este feriado"** (`ativo: false`; o Figma não o desenha, mas sem ele um feriado cadastrado por engano não teria como sair, contrato `editarFeriado`). Feriado desativado some da lista (`incluirInativos` só se a tela tiver o controle; não precisa).
- **"Exportar para a folha"** baixa o CSV do **mês do campo "Período"** para **todas as pessoas** (não só o colaborador escolhido): `exportarPontoParaFolha` com `ano` e `mes` obrigatórios; nome do arquivo `ponto-AAAA-MM.csv`.
- **Modal "Editar"** (corrigir batidas): ver "Tela sem print: Corrigir batidas do dia".
- **Sem "Lançar no banco de horas"** (já cortado na aba da ficha).
- As linhas de **folga** (03/10 e 04/10, "Folga") **ficam** na tela 12 (li no PNG 12); só a aba da ficha (tela 04) as esconde.

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Campo "Colaborador" | `listarFuncionarios` (fatia 1b) | busca; só ativos (suponho) |
| Cartões e tabela do espelho | `obterEspelhoDePontoDeFuncionario` | `funcionarioId` no caminho; `ano` e `mes` |
| "Baixar PDF" | `baixarEspelhoDePontoPdf` | devolve `LinkTemporario`; `ano` e `mes` |
| "Exportar para a folha" | `exportarPontoParaFolha` | arquivo direto; `ano` e `mes` obrigatórios |
| Número do botão "Pedidos de ajuste" | `listarPedidosDeAjusteParaRh` (`resumo.comRh`) ou `obterContadoresDoMenuDoRh` | o mesmo número da bolinha do menu |
| "Feriados deste mês" | `listarFeriados` | `ano` obrigatório; `mes` do período |
| "Salvar feriado" | `criarFeriado` | `{ nome, data, repeteTodoAno }` |
| "Editar" do feriado | `editarFeriado` | `{ nome, data, repeteTodoAno, ativo }` |
| "Editar" do dia | `corrigirMarcacoesDoDia` | ver a tela sem print |
| "Revisar pedido" | `obterPedidoDeAjusteParaRh` | abre o modal (tela 13) |

**Estados**
- Carregando: esqueleto nos números e na tabela; listas de feriados com esqueleto.
- Vazio: sem colaborador escolhido (texto acima); mês sem dias; "Nenhum feriado neste mês." (a lista de feriados nunca dá erro por falta de empresa configurada: vêm só os nacionais e os da empresa, contrato).
- Erro: nas regras comuns. Erros do feriado: 409 `FERIADO_DUPLICADO` "Já existe um feriado com este nome nesta data."; 422 `EMPRESA_NAO_CONFIGURADA` "Cadastre a UF e o município da empresa antes de criar feriados municipais." (com link para Cadastros, Empresa); 422 `FERIADO_NAO_EDITAVEL`.
- Sem permissão: só RH.

**Observações para quem monta**
- Os cartões e a tabela **vêm prontos** do espelho: nenhuma soma no front.
- A aba "Período" pode oferecer os últimos 12 meses (suponho; o contrato aceita qualquer `ano` e `mes`).
- "Exportar para a folha" baixa um arquivo com acentos (UTF-8 com BOM) e separador `;`: o front só entrega o arquivo.
- Mais de 4 batidas em um dia: mostre "+N batidas" e deixe o detalhe para o modal "Editar".

**Perguntas abertas**
- `paths > /api/rh/ponto/folha/exportacao > get`: formato do "exportar para a folha" (colunas, separador, horas em minutos ou decimal, por pessoa ou por dia).
- `components > schemas > PontoEspelhoDia > properties > atrasoMinutos`: como a tolerância de atraso e a hora extra entram na conta.
- `components > schemas > PontoDiaSituacao`: quando um dia é "incompleto".

---

## Tela 13: Modal "Pedidos de ajuste de ponto" (RH)

**PNG:** `Figma\telas\13-rh-ponto-e-feriados-modal-pedidos-de-ajuste.png` · **Incremento:** 3b · **Rotas:** `listarPedidosDeAjusteParaRh`, `obterPedidoDeAjusteParaRh`, `aprovarPedidoDeAjusteComoRh`, `recusarPedidoDeAjusteComoRh`.

**O que fica (li no PNG 13)**
- Modal largo, título "Pedidos de ajuste de ponto" e botão fechar. Duas colunas.
- **Coluna da esquerda: lista de pedidos** (cartões): avatar com iniciais, nome, "Dia 02/10, Financeiro" (`data` em dd/MM e `funcionario.setor`) e chip da etapa (ver "Etiqueta do pedido"). O cartão selecionado tem borda e fundo destacados. A lista abre com `status=PENDENTE`, `ordenarPor=CRIADO_EM` e `direcao=ASC` (o mais antigo primeiro; contrato). Paginação se passar de uma página.
- **Coluna da direita: detalhe do pedido selecionado**: nome em destaque e "Analista pleno, setor Financeiro"; três campos "Dia do ponto" ("Sexta, 02/10"), "Pedido em" ("02/10, 18:05"; `criadoEmUtc`) e "Gestor(a)" ("Marina Costa"; `gestorResponsavel.nome`); "Registro atual" (chips de `periodosDoDia`, com "13:02 - sem saída" em âmbar); "Correção pedida" (`resumoDaCorrecao`, em caixa verde-clara, "Incluir a saída às 17:52"); "Justificativa" (entre aspas, caixa cinza); uma linha de status com chip ("Com o gestor") e a frase "Marina Costa ainda não analisou."; caixa "Observação para Bruno (opcional)" (`observacao`, até 500; o nome é o primeiro nome do colaborador); botões **"Aprovar e corrigir o ponto"** (principal) e **"Recusar"** (contornado em vermelho).
- Rodapé: "4 pedidos esperando o RH. O gestor do setor também pode analisar." (o número é `resumo.comRh`; singular: "1 pedido esperando o RH") e botão **"Fechar"**.

**O que sai ou muda (com a razão)**
- **Os botões só ficam ativos quando `podeDecidir` é `true`.** O PNG os mostra ativos mesmo no pedido "Com o gestor" (divergência já registrada no roadmap, fatia 3, e no contrato): pelo desenho do dono **não há "RH decide direto"**; o RH só decide depois do gestor (ou no pedido sem etapa do gestor). Com `podeDecidir: false`, mostre o botão desligado e, ao lado, o `motivoNaoPodeDecidir` (um dos cinco textos fixos: "Aguardando a análise do gestor.", "Aguardando a análise do RH.", "Este pedido já foi decidido.", "Este pedido não passa pelo gestor e o banco exige que quem decide também esteja cadastrado como gestor. Peça a outra pessoa do RH." ou "Você não pode decidir o seu próprio pedido.").
- **O RH pode recusar o que o gestor aprovou** (decisão do dono, 2026-10-30): "Recusar" vale no pedido "Aprovado pelo gestor".
- **Frase de status** (suponho a redação; o PNG só mostra "ainda não analisou"): `etapa: GESTOR` "{gestor} ainda não analisou."; `etapa: RH` com gestor "{decididaPor} aprovou em {dd/MM, HH:mm}." (e a `observacao` do gestor, se houver, em itálico); aprovada sozinha pela ausência do gestor "O gestor está ausente: etapa aprovada automaticamente."; sem etapa do gestor "Este pedido não passa pelo gestor."; `CONCLUIDA` "Aprovado" ou "Recusado" por {nome} em {data}.
- **"Gestor(a)" quando não há** (`gestorResponsavel: null`): mostre "Sem gestor (só o RH decide)" (suponho).
- **Pedido no mesmo dia** (PNG: "Dia 02/10", "Pedido em 02/10, 18:05"): no contrato o pedido é sempre depois do dia.
- **O cartão "Aprovado pelo gestor" do Thiago Mello** do PNG é exemplo; o exemplo do contrato tem seis pedidos pendentes (`total: 6`, `resumo: { comGestor: 2, comRh: 4 }`).
- "O gestor do setor também pode analisar" é **só informação** (contrato).

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Lista à esquerda e rodapé | `listarPedidosDeAjusteParaRh` | `status=PENDENTE`, `ordenarPor=CRIADO_EM`, `direcao=ASC`; `resumo.comRh` |
| Detalhe à direita; "Revisar pedido" da tela 12 | `obterPedidoDeAjusteParaRh` | para abrir direto um pedido pelo `pedidoEmAnaliseId` |
| "Aprovar e corrigir o ponto" | `aprovarPedidoDeAjusteComoRh` | `{ observacao }`; corrige **todas** as batidas do pedido ou nenhuma |
| "Recusar" | `recusarPedidoDeAjusteComoRh` | `{ observacao }` |

**Estados**
- Carregando: esqueleto na lista e no detalhe.
- Vazio: "Nenhum pedido de ajuste em análise." (e rodapé "0 pedidos esperando o RH.").
- Erro: 409 `PEDIDO_JA_DECIDIDO` "Este pedido já foi respondido. Atualize a lista."; 409 `PEDIDO_ETAPA_INCORRETA` "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista."; 422 `PEDIDO_RH_FORA_DE_GESTORES` "Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor."; 422 `PEDIDO_PROPRIO` "Você não pode decidir o próprio pedido."; 422 `PONTO_SEQUENCIA_INVALIDA` "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários." (o dia mudou desde o pedido); 409 `PONTO_BATIDA_DUPLICADA` "Há duas batidas no mesmo horário neste dia. Corrija os horários." (um instante pedido já existe). Nos 409, recarregue a lista e o detalhe.
- Sucesso: aprovar mostra "Pedido aprovado e ponto corrigido."; recusar, "Pedido recusado."; passe ao próximo pedido da lista; invalide o espelho do colaborador, a lista, o número do botão e as bolinhas do menu.

**Observações para quem monta**
- Use `podeDecidir` e `motivoNaoPodeDecidir` para **tudo**. Nunca decida a habilitação do botão pela `etapa`.
- O pedido de uma pessoa do RH que é gestor só pode ser decidido por quem do RH também está em `GESTORES` (contrato); o `motivoNaoPodeDecidir` já avisa.
- A observação do RH **substitui** a do gestor na resposta (o gestor deixa de aparecer no `decisaoGestor.observacao`).

**Perguntas abertas:** nenhuma `x-provisorio` própria. (A pergunta antiga "o RH pode recusar o que o gestor aprovou" foi decidida em 2026-10-30.)

---

## Tela sem print: Corrigir batidas do dia (modal "Editar", RH)

**PNG:** nenhum (o botão "Editar" está no PNG 12) · **Incremento:** 3b · **Rota:** `corrigirMarcacoesDoDia` (`PUT /api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}`).

**Descrição (desenho meu, apoiada em `corrigirMarcacoesDoDia`)** — modal "Corrigir ponto de {dd/MM}" para {nome}:
- Lista das batidas do dia (de `periodos` do espelho): cada linha com **Tipo** (Entrada ou Saída), **Hora** (`HH:mm`), caixa "No dia seguinte" e um botão "Remover". Batidas já existentes levam o `id`.
- Botão "Incluir batida" (linha nova, sem `id`).
- Texto de apoio: "A correção vale na hora e não passa pela segunda etapa. A pessoa não recebe e-mail. A batida corrigida fica marcada como corrigida pelo RH."
- Botões "Cancelar" e "Salvar correção".
- **Corpo enviado**: a **lista completa** do dia (`marcacoes`, no máximo 20): as existentes com `id`, as novas sem `id`. **Batida que não vai na lista é apagada pela API.**

**Mensagens (contrato)**
- 409 `PONTO_DIA_COM_PEDIDO_EM_ANALISE` "Responda o pedido de ajuste deste dia antes de editar as batidas." (por isso a linha com pedido em análise mostra "Revisar pedido", não "Editar").
- 409 `PONTO_MARCACAO_EM_USO` "Esta batida está ligada a um pedido de ajuste e não pode ser removida."
- 409 `PONTO_BATIDA_DUPLICADA` "Há duas batidas no mesmo horário neste dia. Corrija os horários."
- 422 `PONTO_SEQUENCIA_INVALIDA` "Com estas batidas o dia continua com uma entrada sem saída. Confira os horários."; 422 `PONTO_INSTANTE_FORA_DO_DIA` e `PONTO_INSTANTE_NO_FUTURO` (textos como no pedido de ajuste).
- 422 `FUNCIONARIO_INATIVO` "Este colaborador está inativo e o ponto dele não pode mais ser alterado."

**Estados:** salvando; sucesso fecha o modal e atualiza a linha do dia (a resposta é um `PontoEspelhoDia`) e os números do topo; a linha passa a mostrar "{rótulo} corrigida pelo RH em {dd/MM}".

---

## Tela sem print: Atestados (RH)

**PNG:** nenhum · **Incremento:** 3b · **Rotas:** `listarAtestados`, `baixarAtestado`, `validarAtestado`, `recusarAtestado`.

**Descrição (desenho meu, apoiada no contrato)**
- **Onde:** botão **"Atestados"** na tela 12, ao lado de "Pedidos de ajuste", com um número (quantos aguardam validação: `listarAtestados` com `situacao=ENVIADO`, `tamanhoPagina=1`, `total`). Abre um painel lateral sobre a tela 12 (`/rh/ponto-e-feriados/atestados`). (O painel do RH, da fatia 7, mostra "N atestados para validar" e leva para este mesmo endereço.)
- **Painel "Atestados"**: filtros **Situação** (Aguardando validação [padrão `ENVIADO`], Validados, Recusados, Todos) e busca por nome (`busca`). Tabela, do mais recente ao mais antigo: "Colaborador", "Período" (dias inteiros "07/10 a 08/10" ou de horas "25/09, das 13:00 às 17:48"), "Arquivo" (`nomeArquivo`), "Enviado em", "Situação" e ações.
- Ações por linha: **"Ver arquivo"** (`baixarAtestado`; abre o link temporário), **"Validar"** (`validarAtestado`; confirma em um diálogo "Validar este atestado? O dia deixa de contar como falta e não desconta do banco de horas.") e **"Recusar"** (`recusarAtestado`; diálogo com campo **"Motivo"** obrigatório, até 500 caracteres, "A pessoa vê o motivo e o dia segue como falta."). Linhas já decididas mostram quem decidiu (`decididoPorNome`), quando e o `motivoRecusa`.
- Aviso discreto junto de "Ver arquivo": "O acesso a este arquivo fica registrado." (o contrato registra cada link gerado na auditoria; o texto é desenho meu).

**Mensagens:** 409 `ATESTADO_JA_DECIDIDO` "Este atestado já foi respondido. Atualize a lista." (recarregue). O RH **pode** validar o próprio atestado (decisão do dono). Não há e-mail para o atestado.

**Estados:** vazio "Nenhum atestado aguardando validação."; erro e carregando nas regras comuns.

---

## Tela 54: Ponto da equipe (gestor)

**PNG:** `Figma\telas\54-gestor-ponto-da-equipe.png` · **Incremento:** 3a · **Rotas:** `obterPontoDaEquipe`, `listarPedidosDeAjusteDaEquipe`, `listarEquipeDoGestor` (fatia 1; nome do setor e total).

**O que fica (li no PNG 54)**
- Título "Ponto da equipe" e subtítulo "Setor Comercial, 8 pessoas. Os pedidos de ajuste do setor chegam primeiro para você analisar." (o setor e o total vêm de `listarEquipeDoGestor`: `setor.nome` e `total`).
- Campo de data **"Hoje, 7 de outubro"** (`data`; não pode ser futura). Ao escolher um dia passado, a tela mostra o dia consultado.
- **Quatro números** (`resumo`): "Trabalhando agora" (`contagensHoje.trabalhando`, "6"), "Sem registro hoje" (`contagensHoje.semRegistro`, "1", com o nome da primeira pessoa de `pessoasSemRegistro` embaixo: "Diego Farias"; havendo mais, "e mais N"), "De férias" (`contagensHoje.ferias`, "1", com "{nome}, até {dd/MM}" de `pessoasDeFerias`: "Larissa Moura, até 17/10") e "Pedidos para analisar" (`pedidosParaAnalisar`, "2", âmbar).
- Seção **"Pedidos de ajuste para analisar"**: cartões lado a lado, cada um com avatar e nome, "Ajuste do dia 05/10", a correção em caixa verde-clara ("Incluir o período 13:05 - 17:50" = `resumoDaCorrecao`), "Pedido em 05/10, 19:12" e o chip **"Aguardando você"**; uma seta à direita. Clicar abre o modal da tela 55 (`/gestor/ponto-da-equipe/pedidos/<pedidoId>`). Vem de `listarPedidosDeAjusteDaEquipe` com `status=PENDENTE`, `etapa=GESTOR`, `ordenarPor=CRIADO_EM`, `direcao=ASC`.
- Seção **"Hoje no setor"** (tabela): "Pessoa" (avatar, nome e cargo), "Períodos de hoje" (chips; sem batida, "Nenhum registro"), "Horas até agora" (`horasAteAgoraMinutos`, negrito, "—"), "Banco de horas" (`saldoBancoHorasMinutos` com sinal), "Situação" (chip) e o botão **"Ver espelho"** (abre o painel da tela 56: `/gestor/ponto-da-equipe/espelho/<funcionarioId>`). Sem paginação (a equipe é de um setor).
- **Chip de situação** (de `situacaoHoje`): `TRABALHANDO` "Trabalhando" (ou **"Saiu"** quando `periodoAberto` é `false`: contrato), `FERIAS` "Férias", `SEM_REGISTRO` "Sem registro" (âmbar), `ATESTADO` "Atestado", `SEM_EXPEDIENTE` "Sem expediente", `AINDA_NAO_COMECOU` "Ainda não começou". Em **dia passado** `situacaoHoje` vem `null` e o chip vem de `situacaoDoDia` (regras comuns).

**O que sai ou muda (com a razão)**
- **"Baixar espelho do setor" sai** (decisão do dono, 2026-10-30; design seção 7). O espelho **de uma pessoa** em PDF fica na tela 56.
- **Ordem da tabela:** a API devolve por nome (Diego, Fernanda, Gustavo, Helena, Igor, Júlia, Larissa, Pedro); o PNG mostra outra ordem (Júlia, Pedro, Larissa, Diego...). Vale a ordem da API.
- **Dia sem expediente** (`diaUtil: false`): o cartão "Trabalhando agora" mostra "Hoje não há expediente" e, se alguém bateu entrada, só o número de `trabalhando` (contrato; `CONVENCOES.md` seção 10).
- **Dia passado:** `contagensHoje` é `null`. Suponho: o cartão "Trabalhando agora" some (ou vira "Com registro", contado dos `itens` com `periodos` não vazios) e os cartões "Sem registro" e "De férias" usam as listas `pessoasSemRegistro` e `pessoasDeFerias`. Ajuste na conferência visual.
- **Pedidos do PNG** (Diego Farias e Helena Prates) são exemplo; o `ELENCO.md` tem os pedidos 303 (Júlia Nunes) e 304 (Helena Prates) com o Carlos Ribeiro. O pedido "do dia 05/10, em 05/10" é no mesmo dia: no contrato é sempre depois.
- A seção "Pedidos de ajuste para analisar" mostra **só** os que aguardam este gestor (`etapa: GESTOR`). Pedido de gestor ou do próprio gestor **não aparece** (vai direto ao RH; contrato).

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Subtítulo (setor e total) | `listarEquipeDoGestor` (fatia 1) | `setor.nome`, `total` |
| Quatro números e tabela "Hoje no setor" | `obterPontoDaEquipe` | `data` opcional |
| Cartões de pedidos | `listarPedidosDeAjusteDaEquipe` | `status=PENDENTE`, `etapa=GESTOR`, `ordenarPor=CRIADO_EM`, `direcao=ASC` |
| Clicar no cartão | `obterPedidoDeAjusteDaEquipe` | abre a tela 55 |
| "Ver espelho" | `obterEspelhoDePontoDaPessoaDaEquipe` | abre a tela 56 |

**Estados**
- Carregando: esqueleto nos números, nos cartões e na tabela.
- Vazio: sem pedidos, a seção "Pedidos de ajuste para analisar" mostra "Nenhum pedido para analisar." (suponho); equipe vazia (gestor sem setor: `itens: []`): "Você ainda não é gestor de um setor." (suponho).
- Erro: nas regras comuns. 403 para quem não é gestor (inclusive RH que só está em `GESTORES`).

**Observações para quem monta**
- Escolha por **uma** lista (`obterPontoDaEquipe`) para os números e para a tabela: não recalcule nada no front.
- Duas "situações" convivem aqui: `situacaoHoje` (para hoje) e `situacaoDoDia` (para o dia consultado). Ver "Regras comuns".
- Depois de aprovar ou recusar no modal, invalide `obterPontoDaEquipe`, `listarPedidosDeAjusteDaEquipe` e `obterContadoresDoMenuDoGestor`.

**Perguntas abertas:** `components > schemas > PontoDiaSituacao` (quando um dia é "incompleto") só afeta o chip de dia passado.

---

## Tela 55: Modal "Pedido de ajuste de ponto" (gestor)

**PNG:** `Figma\telas\55-gestor-ponto-da-equipe-modal-pedido-de-ajuste.png` · **Incremento:** 3b · **Rotas:** `obterPedidoDeAjusteDaEquipe`, `aprovarPedidoDeAjusteComoGestor`, `recusarPedidoDeAjusteComoGestor`.

**O que fica (li no PNG 55)**
- Título "Pedido de ajuste de ponto" e botão fechar.
- Cabeçalho do pedido: avatar e nome ("Diego Farias") e "Ajuste do dia 05/10, pedido em 05/10, 19:12" (`data` e `criadoEmUtc`).
- "Registro atual" (chips de `periodosDoDia`), "Correção pedida" (`resumoDaCorrecao`, caixa verde-clara, "Incluir o período 13:05 - 17:50"), "Justificativa" (entre aspas, caixa cinza) e a caixa **"Observação para Diego (opcional)"** (`observacao`, até 500; primeiro nome).
- Rodapé: "Depois de você, o RH confirma o ajuste." e os botões **"Recusar"** (contornado em vermelho) e **"Aprovar"** (principal).

**O que sai ou muda (com a razão)**
- O chip laranja **"volta do almoço sem registro"** ao lado de "08:02 - 12:00" (PNG) **não existe no contrato**: o dia só com 08:02 - 12:00 é uma sequência consistente e o contrato só chama de `INCOMPLETO` a sequência quebrada (`x-provisorio` de `PontoDiaSituacao`). Mostre apenas os chips de período. Se o dono quiser o texto, é decisão dele.
- **"Recusar" encerra o pedido** (`etapa: CONCLUIDA`, `REJEITADA`) e o RH **não** confirma; "Depois de você, o RH confirma o ajuste." vale para a **aprovação**. A pessoa recebe e-mail com o resultado de **cada** decisão (gestor e RH).
- Gestor ausente: se a ausência do gestor passou o pedido ao RH antes, aprovar ou recusar dá 409 `PEDIDO_ETAPA_INCORRETA` (recarregue).

**Rotas por elemento**

| Elemento | `operationId` | Observação |
|---|---|---|
| Abrir o modal | `obterPedidoDeAjusteDaEquipe` | 404 para pedido de outra equipe, de outro gestor ou do próprio gestor |
| "Aprovar" | `aprovarPedidoDeAjusteComoGestor` | `{ observacao }`; o pedido passa à etapa do RH |
| "Recusar" | `recusarPedidoDeAjusteComoGestor` | `{ observacao }` |

**Estados**
- Carregando: esqueleto; vazio: não se aplica.
- Erro: 409 `PEDIDO_JA_DECIDIDO` "Este pedido já foi respondido. Atualize a lista."; 409 `PEDIDO_ETAPA_INCORRETA` "Este pedido não está mais na etapa em que você pode decidir. Atualize a lista." (recarregue a tela 54); 404 "Não encontrado."
- Sucesso: aprovar "Pedido aprovado. Agora o RH confirma."; recusar "Pedido recusado."; fecha o modal e atualiza a tela 54.

**Observações:** os botões usam `podeDecidir` (na rota do gestor é `true` só na etapa dele). Sem `podeDecidir`, mostre o `motivoNaoPodeDecidir`.

---

## Tela 56: Espelho de ponto da pessoa (gestor)

**PNG:** `Figma\telas\56-gestor-ponto-da-equipe-espelho-da-pessoa.png` · **Incremento:** 3a · **Rotas:** `obterEspelhoDePontoDaPessoaDaEquipe`, `baixarEspelhoDePontoPdfDaEquipe`.

**O que fica (li no PNG 56)**
- Painel lateral à direita, título "Espelho de ponto" e botão fechar.
- Cabeçalho: avatar, nome ("Diego Farias") e "Vendedor, jornada de 44 h semanais" (`funcionario.cargo` e `jornada.cargaSemanalMinutos` ÷ 60); campo **"Período"** ("Outubro de 2026", mês e ano).
- Três números: "Horas no mês" (`resumo.horasTrabalhadasNoMesMinutos`), "Banco de horas" (`saldoBancoHorasMinutos`, em âmbar quando negativo) e "Ajustes em análise" (`resumo.pedidosEmAnalise`).
- Tabela: "Dia", "Períodos registrados", "Horas", "Saldo", "Situação". Dias de folga (sábado e domingo) **aparecem** com "Folga". Hoje sem batida aparece "Sem registro".
- Nota: "Você pode ver o espelho, mas só o RH altera marcações. Ajustes chegam como pedidos."
- Botões **"Baixar PDF"** (`baixarEspelhoDePontoPdfDaEquipe`, com `ano` e `mes`) e **"Fechar"**.

**O que sai ou muda (com a razão)**
- O chip "volta sem registro" (05/10 no PNG, junto de "Ajuste em análise") **não existe no contrato** (ver tela 55).
- **Só o RH altera marcações:** nenhum botão de editar aqui.
- Só **pessoas da equipe** (404 para outro setor).
- O PDF é **só de uma pessoa** (o do setor inteiro foi cortado).
- **Atestados da pessoa** (sem print): um bloco "Atestados" abaixo da tabela (ver "Atestados da equipe").

**Estados:** carregando (esqueleto); mês vazio "Nada registrado neste mês."; erro e 404 nas regras comuns.

**Observações:** o painel abre da tela 54 ("Ver espelho") e também compõe a aba Ponto da ficha da equipe (ver abaixo).

---

## Tela sem print: Aba Ponto da ficha da equipe e Atestados da equipe (gestor)

**PNG:** nenhum para estes elementos. `Figma\telas\52-gestor-painel-da-equipe-ficha.png` (li) mostra a ficha da pessoa da equipe com as abas **Dados**, **Ponto**, **Férias**, **Treinamentos** e **Advertências** e o botão "Registrar advertência"; só a aba Dados tem print · **Incremento:** 3a (aba) e 3b (atestados) · **Rotas:** `obterEspelhoDePontoDaPessoaDaEquipe`, `listarAtestadosDaEquipe`, `baixarAtestadoDaEquipe`.

**Descrição (desenho meu, apoiada no contrato)**
- **Aba Ponto da ficha da equipe** (moldura da ficha: fatia 1b): os mesmos três números e a mesma tabela da tela 56, para o mês atual, **sem sábados e domingos** (esconda `FOLGA`) e com o botão "Baixar PDF". É o mesmo conteúdo da tela 56 sem o cabeçalho.
- **Atestados da pessoa** (na aba Ponto e no painel da tela 56): bloco "Atestados" com `listarAtestadosDaEquipe` filtrado por `funcionarioId`: "Período", "Enviado em", "Situação" e o botão **"Ver arquivo"** (`baixarAtestadoDaEquipe`). O gestor **vê** o arquivo mas **não valida nem recusa** (só o RH).
- **Atestados da equipe** (todos): na tela 54, um link **"Atestados da equipe"** no topo abre um painel (`/gestor/ponto-da-equipe/atestados`) com a lista completa (`listarAtestadosDaEquipe` sem `funcionarioId`), com a coluna "Pessoa".
- Aviso discreto: "O acesso a este arquivo fica registrado." (contrato: cada link gerado vai para a auditoria).
- Pessoa de outra equipe: a lista vem vazia; o download dá 404.

**Estados:** vazio "Nenhum atestado."; erro nas regras comuns.

---

## Conferência: toda rota da fatia 3 tem tela

| `operationId` | Rota | Tela |
|---|---|---|
| `obterPontoHoje` | `GET /api/eu/ponto/hoje` | 32, 33, 47 |
| `registrarMarcacaoDePonto` | `POST /api/eu/ponto/marcacoes` | 32, 33, 47 |
| `obterMeuEspelhoDePonto` | `GET /api/eu/ponto/espelho` | 33, 47 e modal "Pedir ajuste" (registro atual) |
| `listarMeusPedidosDeAjuste` | `GET /api/eu/ponto/pedidos-de-ajuste` | 33, 47, aviso da 32 |
| `criarPedidoDeAjuste` | `POST /api/eu/ponto/pedidos-de-ajuste` | modal "Pedir ajuste" (sem print) |
| `obterMeuPedidoDeAjuste` | `GET /api/eu/ponto/pedidos-de-ajuste/{pedidoId}` | "Ver pedido" da 32 |
| `obterMeuBancoDeHoras` | `GET /api/eu/ponto/banco-de-horas` | 33, 47, 32 ("Ver extrato"), extrato completo (sem print) |
| `listarMeusAtestados` | `GET /api/eu/ponto/atestados` | "Meus atestados" (sem print) |
| `enviarAtestado` | `POST /api/eu/ponto/atestados` | "Enviar atestado" (sem print) |
| `obterPontoDaEquipe` | `GET /api/gestor/ponto/equipe` | 54 |
| `obterEspelhoDePontoDaPessoaDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}/ponto` | 56 e aba Ponto da ficha da equipe |
| `baixarEspelhoDePontoPdfDaEquipe` | `GET /api/gestor/equipe/{funcionarioId}/ponto/espelho/download` | 56 |
| `listarPedidosDeAjusteDaEquipe` | `GET /api/gestor/ponto/pedidos-de-ajuste` | 54 |
| `obterPedidoDeAjusteDaEquipe` | `GET /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}` | 55 |
| `aprovarPedidoDeAjusteComoGestor` | `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | 55 |
| `recusarPedidoDeAjusteComoGestor` | `POST /api/gestor/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | 55 |
| `listarAtestadosDaEquipe` | `GET /api/gestor/ponto/atestados` | Atestados da equipe (sem print) |
| `baixarAtestadoDaEquipe` | `GET /api/gestor/ponto/atestados/{atestadoId}/download` | Atestados da equipe (sem print) |
| `obterEspelhoDePontoDeFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}/ponto` | 04, 12 |
| `baixarEspelhoDePontoPdf` | `GET /api/rh/funcionarios/{funcionarioId}/ponto/espelho/download` | 12 |
| `corrigirMarcacoesDoDia` | `PUT /api/rh/funcionarios/{funcionarioId}/ponto/dias/{dia}` | 12 ("Editar"; modal sem print) |
| `obterBancoDeHorasDeFuncionario` | `GET /api/rh/funcionarios/{funcionarioId}/ponto/banco-de-horas` | **sem tela (decisão da ficha; o contrato diz que pode ficar sem tela)** |
| `listarPedidosDeAjusteParaRh` | `GET /api/rh/ponto/pedidos-de-ajuste` | 13 (e o número do botão da 12) |
| `obterPedidoDeAjusteParaRh` | `GET /api/rh/ponto/pedidos-de-ajuste/{pedidoId}` | 13 e "Revisar pedido" da 12 |
| `aprovarPedidoDeAjusteComoRh` | `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/aprovar` | 13 |
| `recusarPedidoDeAjusteComoRh` | `POST /api/rh/ponto/pedidos-de-ajuste/{pedidoId}/recusar` | 13 |
| `listarFeriados` | `GET /api/rh/ponto/feriados` | 12 |
| `criarFeriado` | `POST /api/rh/ponto/feriados` | 12 |
| `editarFeriado` | `PUT /api/rh/ponto/feriados/{feriadoId}` | 12 |
| `exportarPontoParaFolha` | `GET /api/rh/ponto/folha/exportacao` | 12 |
| `listarAtestados` | `GET /api/rh/ponto/atestados` | Atestados do RH (sem print) |
| `baixarAtestado` | `GET /api/rh/ponto/atestados/{atestadoId}/download` | Atestados do RH |
| `validarAtestado` | `POST /api/rh/ponto/atestados/{atestadoId}/validar` | Atestados do RH |
| `recusarAtestado` | `POST /api/rh/ponto/atestados/{atestadoId}/recusar` | Atestados do RH |

34 de 34 rotas listadas; 33 com tela e 1 sem tela por decisão declarada (`obterBancoDeHorasDeFuncionario`).
