# Ficha da fatia 7: gestão (front-end)

Contrato lido: `contrato\fontes\70-fatia-7-gestao.yaml` (41 rotas; a versão do contrato é o `info.version` de `contrato\openapi.yaml`: confira-a antes de começar e anote-a no `PROGRESSO.md`, tela por tela). Notas de tela: `docs\plano-front-end\telas\fatia-7.md` (**leia a nota inteira de cada tela antes de montá-la**: ela diz o que fica, o que sai e por quê). Fontes de apoio: `contrato\CONVENCOES.md` (seções 9, 10, 12 e 13), `contrato\ELENCO.md` (as pessoas e os números dos exemplos), `contrato\PROVISORIOS.md` e o design `docs\superpowers\specs\2026-10-30-planos-backend-frontend-design.md` (seção 7).

Esta ficha **não traz passo a passo fino**. Repita o padrão da fatia 1b (`fatia-1b-telas.md`): um passo pequeno por vez, cada um com um comando que prova, `PROGRESSO.md` atualizado a cada passo, commit e push por passo na sua branch (regras em `01-regras.md`). Caminhos de tela: `docs\plano-front-end\05-caminhos-de-tela.md` (tabela única; traz 7.2, 7.3, 7.5, 7.6, os destinos de "Revisar" do painel 00 e a ficha da equipe como painel sobre o Painel da equipe).

## 1. Objetivo e entrega demonstrável

Fechar a **gestão** do dia a dia: **férias** em duas etapas (a pessoa pede, o gestor aprova, sugere outra data ou recusa, o RH confirma ou recusa; o RH também registra férias direto pela ficha), **benefícios** (a pessoa pede adesão, dependente e trajeto do vale-transporte; o RH decide), **pagamento** (a data do próximo pagamento e as exceções por mês) e os **painéis**: a tela inicial do RH, a tela inicial do funcionário e do gestor, e o Painel da equipe do gestor.

Demonstração com os dados simulados (pessoas de `ELENCO.md`, "hoje" é quarta-feira, 2026-10-07, 09:42): o RH (Ana Souza) abre o painel e vê "Há 10 pedidos esperando revisão" e "Presença agora: 86 de 101"; o funcionário (Bruno Lima) abre "Pedir férias", digita 16/11 a 30/11 e vê "15 dias corridos. Sobram 3 dias." com as verificações; o gestor (Carlos Ribeiro) vê no Painel da equipe "Pedro Santos pediu 15 dias, de 10/11 a 24/11." com o aviso "Coincide 8 dias com as férias já aprovadas de Júlia Nunes" e os botões "Aprovar", "Sugerir outra data" e "Recusar".

**Atenção:** os dados simulados são o `example` de cada resposta, **fixo**: depois de "Aprovar", a lista simulada não muda sozinha. Os testes provam a **chamada certa** (rota, parâmetros, corpo) e que a tela **pede os dados de novo**; ver a mudança de verdade é só na integração com a API real (seção 7).

## 2. Telas

11 telas com PNG e 6 sem PNG (descritas em texto na nota). Numeração dos PNG como em `Figma\telas\NN-perfil-tela.png`; as sem print levam 7.1 a 7.6 (número da nota, não do Figma).

| Tela | Nome | Perfil | PNG | Incremento |
|---|---|---|---|---|
| 00 | Painel inicial | RH | `00-rh-painel-inicial.png` | 7d |
| 05 | Ficha do colaborador, aba Férias | RH | `05-rh-colaboradores-ficha-aba-ferias.png` | 7a |
| 06 | Modal "Registrar férias" | RH | `06-rh-colaboradores-ficha-modal-registrar-ferias.png` | 7a |
| 32 | Tela inicial | Funcionário | `32-func-tela-inicial.png` | 7d |
| 34 | Férias | Funcionário | `34-func-ferias.png` | 7a |
| 35 | Painel "Pedir férias" | Funcionário | `35-func-ferias-pedir-ferias.png` | 7a |
| 44 | Benefícios (o gestor usa a mesma) | Funcionário | `44-func-beneficios.png` | 7b |
| 45 | Painel "Aderir ao ..." | Funcionário | `45-func-beneficios-aderir-a-beneficio.png` | 7b |
| 46 | Tela inicial | Gestor | `46-gestor-tela-inicial.png` | 7d |
| 48 | Férias (a mesma da 34, menu do gestor) | Gestor | `48-gestor-ferias.png` | 7a |
| 51 | Painel da equipe | Gestor | `51-gestor-painel-da-equipe.png` | 7a (cartões e calendário) e 7d (tela completa) |
| 7.1 | Aba Férias da ficha da equipe | Gestor | sem print (a ficha é `52-gestor-painel-da-equipe-ficha.png`) | 7a |
| 7.2 | Pedidos de férias para o RH | RH | sem print | 7a |
| 7.3 | Pedidos de benefícios para o RH | RH | sem print | 7b |
| 7.4 | Pagamento (em Cadastros) | RH | sem print | 7c |
| 7.5 | Pendências do RH | RH | sem print | 7d |
| 7.6 | Pessoas sem registro hoje | RH | sem print | 7d |

As telas 05 e 7.1 são **abas dentro da ficha** da fatia 1 (painel lateral com as abas); a 7.4 é um item novo do menu lateral de **Cadastros** (tela 29); as telas 35 e 45 e o modal 06 são painéis ou modais sobre outra tela.

## 3. Rotas do contrato por tela

As **41 rotas** da fatia (`ROTAS.md`, "fatia 7 gestao") e o que cada uma serve, mais, no fim da tabela, as **2 operações de outras fatias** que o teste de cobertura desta ficha também confere. O detalhe (parâmetros, corpo, erros, estados) está em cada nota de tela.

| Incremento | Rotas (`operationId`) | Telas |
|---|---|---|
| **7a férias (17)** | `obterMinhasFerias`, `pedirFerias`, `conferirPedidoDeFerias`, `aceitarSugestaoDeDataDasFerias`, `recusarSugestaoDeDataDasFerias` | 34, 35, 48 |
| | `obterCalendarioDeFeriasDaEquipe`, `aprovarFeriasComoGestor`, `recusarFeriasComoGestor`, `sugerirOutraDataDasFerias` | 51 |
| | `obterFeriasDaPessoaDaEquipe` | 7.1 |
| | `listarFeriasParaORh`, `confirmarFeriasComoRh`, `recusarFeriasComoRh` | 7.2 |
| | `obterFeriasDaFichaDoFuncionario`, `registrarFeriasPeloRh`, `conferirRegistroDeFerias` | 05, 06 |
| | `listarFeriasDaEquipe` | **nenhuma tela** (ver abaixo) |
| **7b benefícios (11)** | `obterMeusBeneficios`, `pedirInclusaoDeDependente`, `pedirAtualizacaoDeTrajeto` | 44 |
| | `pedirAdesaoABeneficio` | 45 |
| | `listarPedidosDeBeneficio`, `aprovarAdesaoABeneficio`, `recusarAdesaoABeneficio`, `aprovarDependenteDeBeneficio`, `recusarDependenteDeBeneficio`, `aprovarTrajetoDeBeneficio`, `recusarTrajetoDeBeneficio` | 7.3 |
| **7c pagamento (5)** | `obterProximoPagamento` | cartão "Próximo pagamento" das telas 36 e 49 (fatia 4) e a data do modal 08 (fatia 4); **as telas 32 e 46 não a chamam**: o painel delas já traz o campo (ver "Cartão Próximo pagamento", abaixo) |
| | `obterCalendarioDePagamento`, `listarExcecoesDePagamento`, `definirExcecaoDePagamento`, `removerExcecaoDePagamento` | 7.4 |
| **7d painéis (8)** | `obterPainelDoFuncionario`, `obterContadoresDoMenuDoFuncionario` | 32, 46 |
| | `obterContadoresDoMenuDoGestor`, `obterPainelDaEquipe` | 46, 51 |
| | `obterContadoresDoMenuDoRh`, `obterPainelDoRh` | 00 |
| | `listarPendenciasDoRh`, `listarPessoasSemRegistroHoje` | 7.5, 7.6 |
| **Outras fatias, cobertas aqui (2)** | `listarEquipeDoGestor` (fatia 1) | 51 (o quadro "Hoje": `contagensHoje` e quem está sem registro) |
| | `listarAniversariantes` (fatia 4) | 32 e 46 (o cartão "Aniversariantes") |

**`listarFeriasDaEquipe` não tem tela:** a lista "Esperando sua aprovação" do Painel da equipe já vem dentro de `obterPainelDaEquipe` (os mesmos itens). Não crie tela para ela; anote no `PROGRESSO.md` "sem uso em tela".

**Cartão "Próximo pagamento"** (`contrato\fontes\70-fatia-7-gestao.yaml`, `PagamentoProximo`, "definição única do dado"; lido): **um** componente, que recebe `{ data, mesReferencia }` por props (`data` é `pagamento.data`, `mesReferencia` é `competencia.mes`; sem `data`, não desenha nada). Cada tela o alimenta **pela rota do próprio painel quando ela traz o campo**: as telas **32 e 46** (as duas usam `obterPainelDoFuncionario`, `GET /api/eu/painel`, que traz `proximoPagamento`) leem `obterPainelDoFuncionario.proximoPagamento` e **não** chamam `obterProximoPagamento`; as telas **36 e 49** (fatia 4, sem painel) o alimentam por `obterProximoPagamento`. O painel da equipe (tela 51) e o do RH (tela 00) não têm o cartão. As notas (`telas\fatia-7.md`) dizem a mesma coisa. O componente nasce na fatia 4 (telas 36 e 49); se ainda não existir quando chegar aqui, crie-o com essas props e registre no `PROGRESSO.md`.

**Rotas de outras fatias que estas telas só leem ou chamam** (os dados simulados gerados do contrato cobrem todas): `obterUsuarioLogado` (fatia 0); `listarEquipeDoGestor`, `listarFuncionarios`, `listarSetores`, `listarBeneficiosDoCatalogo`, `obterEmpresa` (fatia 1); `obterPontoHoje`, `listarMeusPedidosDeAjuste`, `aprovarPedidoDeAjusteComoGestor`, `recusarPedidoDeAjusteComoGestor` (fatia 3); `listarMeusAvisos`, `listarAniversariantes` (fatia 4). **Duas destas, `listarEquipeDoGestor` e `listarAniversariantes`, entram no teste de cobertura desta fatia** (as duas linhas "Outras fatias, cobertas aqui" da tabela acima): a tela delas é daqui (51 e 32/46), e nenhum outro teste de cobertura as conta.

## 4. Regras e cortes que valem

Do contrato, do design (seção 7) e do roadmap (fatia 7). O contrato é lei: se faltar algo, registre uma dúvida, não edite.

**O que o Figma mostra e esta fatia não terá** (cada um tem a razão na nota da tela):
- **Tela 00:** a linha **"Folga ou compensação"** de "Presença agora"; os itens **"Fim do período de experiência"** e **"Treinamento presencial"** de "Próximos dias".
- **Tela 51:** o botão **"Lembrar Diego por e-mail"** (a lista de e-mails é fechada).
- **Tela 06:** a caixa **"Avisar Bruno e o gestor por e-mail"** (não há e-mail de férias registradas pelo RH).
- **Telas 44 e 45:** os detalhes que o banco não guarda (crédito no dia 1º, operadora, desconto de 6%, "Desde 14/03/2022", beneficiários, valor da mensalidade), os botões "Ver regras de uso", "Ver carteirinha" e "Ver beneficiários", e a promessa de resposta "por e-mail".
- Sem **unidades**; sem **período de experiência**; sem **turmas presenciais** (spec 13.3).

**O que o contrato tem e o Figma não desenha** (a nota diz como montar): o botão **"Recusar"** do gestor no cartão de férias (com motivo de 5 a 500 caracteres); o modal **"Sugerir outra data"** e a resposta da pessoa à sugestão; o campo **"Quantos dias"** do abono; as listas dos RH (7.2, 7.3, 7.5, 7.6) e a tela de pagamento (7.4); a aba Férias do gestor (7.1); o botão **"Incluir dependente"**.

**Regras de negócio que a tela obedece, sem reimplementar:**
- **Aprovação em duas etapas** (`CONVENCOES.md`, seção 9): `etapa` (`GESTOR`, `RH`, `CONCLUIDA`), `podeDecidir` e `motivoNaoPodeDecidir` mandam nos botões. **Não existe "o RH aprova direto"**; o gestor que recusa encerra o pedido; **o RH pode recusar o que o gestor aprovou**. Pedido de gestor, de setor sem gestor ou de gestor desligado só passa pelo RH ("O RH analisa o pedido."). **"Gestor ausente"** (ligado pelo RH, fatia 1): a etapa do gestor aparece como "Aprovado automaticamente (gestor ausente)".
- **Regras de férias** (partes, abono, 13º, 30 dias de antecedência, 2 dias antes de feriado, saldo, sobreposição): **só a API as conhece**. A tela chama a conferência e mostra a lista que vier; valida apenas "duas datas preenchidas e fim não anterior ao início".
- **"Presença agora"** e a situação de hoje de cada pessoa: só por `SituacaoHoje` (`CONVENCOES.md`, seção 10). Quem já bateu entrada é "trabalhando". "Ainda não começou" só aparece se for maior que zero. Dia sem expediente: "Hoje não há expediente".
- **E-mails** (seção 12): a pessoa recebe e-mail a **cada decisão de férias**; **não** recebe de pedido criado, de férias registradas pelo RH nem de resposta de benefício. Todo texto que prometa e-mail fora disso muda.
- **Privacidade:** o gestor não recebe CPF, endereço, holerite, documentos, benefícios, dependentes nem trajeto da equipe; o **trajeto** (endereço de casa) e os **dependentes** só aparecem para a própria pessoa e para o RH, e não vão ao console, a `localStorage` nem à URL.
- **Texto de usuário** (mensagem, observação, motivo, justificativa): texto puro, nunca HTML.
- **Datas:** `date` mostra-se como está (`2026-11-16` é 16/11/2026, **sem converter fuso**); `date-time` (`...Z`) converte-se para `America/Sao_Paulo`.
- **O gestor** usa as telas de férias e de benefícios do funcionário (rotas `/api/eu/...`) com o menu dele; o menu do gestor ganha "Benefícios" (e "Meus documentos"; a fundação já os traz); ver a nota da tela 46. **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o RH tem **"Férias"** (`/eu/ferias`) e **"Benefícios"** (`/eu/beneficios`) como itens **ativos** do grupo **"Meu espaço"** do menu; o gestor já tem os dois em "Seu dia a dia".

## 5. Ordem de construção e dependências

**Usa da fundação (1a):** cliente de API gerado do contrato, menu e rotas por perfil, componentes-base (cartão, tabela, etiqueta, painel lateral, modal, aviso, campo de data), mensagens de erro do `application/problem+json`, MSW. **Usa da fatia 1b:** a ficha com abas (telas 03 e 52), Cadastros (tela 29), a lista de colaboradores. **Usa das fatias 3 a 6 só para o 7d:** o cartão "Seu ponto hoje" e a decisão de ajuste de ponto (3), avisos e aniversariantes (4), a avaliação do ciclo (5) e "Gerenciar treinamentos" (6). **Se uma dessas fatias ainda não tiver o componente**, monte o cartão com os dados simulados e registre no `PROGRESSO.md`.

Ordem sugerida (cada item é um passo, com teste; **7d por último**, porque junta o que as outras fatias já fizeram):

**7a, férias**
1. Utilitários: **os formatadores já existem na 1a** (`fatia-1a-fundacao.md`, Task 2): `formatarData` (`date` sem mexer no fuso, sem `Date`), `plural` ("N dia(s)"), `formatarMinutosComoHoras(min, { sinal: true })` (minutos como "+2h10" e "−1h30", com o sinal U+2212), `formatarDataHora`, `formatarHora` e `formatarDiaMes`; e a 1b acrescentou `formatarDiaEMes` e `somarDias`. **Use-os; não os recrie.** Se a tela precisar de um formato que falta, acrescente-o **ao mesmo arquivo** e ao teste dele. O que é só desta fatia e se escreve aqui: o rótulo da situação do pedido e a frase da decisão de cada etapa (nota, "Regras comuns"). Testes, inclusive a prova "outro fuso" rodada como a fundação a roda (`fatia-1a-fundacao.md`, Task 2, "Prova em outro fuso", com `TZ` diferente de São Paulo) e o **controle** dela: um teste que confere o deslocamento sob `TZ` (sob `TZ=Pacific/Honolulu`, `new Date(2026, 0, 1).getTimezoneOffset()` é 600), porque sem o controle a prova passaria por acaso se o `TZ` fosse ignorado.
2. O componente de **conferência de férias** (datas, caixas, "Quantos dias", total e lista de verificações), com espera curta depois de digitar e descarte de resposta antiga. Serve às telas 35 e 06 (muda só a rota).
3. Tela **34 e 48** (a mesma), com o bloco de sugestão de data (aceitar e recusar); depois o painel da tela **35**.
4. A aba **Férias da ficha do RH (05)** e o modal **06**; a aba **7.1** da ficha da equipe (reaproveita a 05 sem o botão).
5. **Reestruture a rota da ficha da equipe e crie a página do Painel da equipe:** a 1b deixou `equipe/:funcionarioId` solta em `ROTAS_GESTOR` (a página do Painel da equipe ainda não existia). Troque por `{ caminho: 'equipe', elemento: <PainelDaEquipe />, filhas: [{ caminho: ':funcionarioId', elemento: <PainelDaFichaDaEquipe /> }] }`, com `PainelDaEquipe` terminando em `<Outlet />` (o mesmo padrão de `colaboradores` e `:funcionarioId` no lado do RH): a ficha 52 passa a ser **painel sobre o Painel da equipe**, e fechar o painel volta a `/gestor/equipe` (que agora existe). O caminho `/gestor/equipe/:funcionarioId` não muda; o item "Painel da equipe" do menu passa a aparecer sozinho (`05-caminhos-de-tela.md`). Teste: `/gestor/equipe/21` mostra o Painel da equipe atrás e a ficha de Diego Farias por cima; fechar leva a `/gestor/equipe`. Depois, os **cartões de férias** do gestor (Aprovar, Sugerir outra data, Recusar com motivo) e o **calendário** "Férias da equipe", montados na página da tela 51 **sem** os blocos que dependem de outras fatias (o resto entra no 7d).
6. A tela **7.2** (lista do RH, confirmar e recusar).

**7b, benefícios:** 7. Tela **44** (inclui os dois modais). 8. Painel **45**. 9. Tela **7.3**.

**7c, pagamento:** 10. Tela **7.4** (item novo em Cadastros). O cartão "Próximo pagamento" **não é passo desta ficha**: é o componente único descrito na seção 3 (`{ data, mesReferencia }`), que a fatia 4 cria para as telas 36 e 49; aqui ele só é **montado** nas telas 32 e 46 (passo 11), alimentado por `obterPainelDoFuncionario.proximoPagamento`. Depois de salvar ou remover uma exceção (tela 7.4), invalide `obterPainelDoFuncionario` e `obterProximoPagamento`.

**7d, painéis:** 11. Telas **32 e 46** (juntam cartões de várias fatias, em paralelo). 12. Tela **51** completa (faixa "Hoje", ajustes de ponto, tabela "Pessoas da equipe", botão "Avaliar a equipe"). 13. Tela **00** e as telas **7.5** e **7.6**. 14. Revisão: os testes de **ausência** da seção 6 e a lista de conferência visual. **As bolinhas do menu não são um passo desta ficha:** a fundação (1a) já traz `useContadoresDoMenu` lendo `obterContadoresDoMenuDoFuncionario`, `...DoGestor` e `...DoRh`; os valores reais chegam quando a API existir (registre "pendente de integração"). Aqui as telas só chamam `invalidarContadoresDoMenu` depois de decidir.

## 6. Definição de pronto

**Prova padrão:** o bloco **Prova padrão de fatia**, na seção final de `fatia-1b-telas.md` (`test`, `lint`, `format:check`, `build`, `csp` e o teste de cobertura de operações da fatia), rodado da raiz do repositório (`01-regras.md`, A14). Aqui o teste de cobertura (`coberturaDaFatia7.test.ts`) confere **42 operações**: as **41** da seção 3 (`ROTAS.md`, "fatia 7 gestao"), **menos** `listarFeriasDaEquipe` (sem uso em tela; a exclusão, com o motivo, fica escrita no teste), **mais** `listarEquipeDoGestor` (tela 51) e `listarAniversariantes` (telas 32 e 46), as duas da seção 3 "Outras fatias, cobertas aqui" (41 − 1 + 2 = 42; a conta fecha com `ROTAS.md`: 17 de férias, 11 de benefícios, 5 de pagamento e 8 de painéis, 41 no total). `obterProximoPagamento` fica **dentro** da conta: ela é chamada pelas telas 36 e 49 (fatia 4), cujo teste de contrato é o que a cobertura encontra (a cobertura lê todos os `*.test.ts(x)` de `src\features` e `src\app`, `fatia-1b-telas.md`, Task 10); as telas 32 e 46 **não** a chamam. O que esta ficha acrescenta é a lista abaixo, que é da fatia.

**Passos novos do `csp`:** **nenhum** além da matriz de perfis das telas desta fatia (a sonda de download da 1a já cobre Blob e link temporário), **exceto** gráfico: as notas **não** pedem `recharts` (as barras da tela 00 e o quadro de 13 semanas da tela 51 são desenho próprio, proporcional), mas **se** a sessão usar `recharts` em alguma tela, o passo novo abre **aquela tela com o perfil certo** (`/rh/painel` com o RH; `/gestor/equipe` com o gestor; `/eu/inicio`) e conta violações (`02-pilha.md` mediu 0 com `recharts`; o `csp` confirma na tela real). Aqui só se descreve o passo, sem código de `.mjs`: o script e o ponto de acréscimo (`ACRESCENTE AQUI`) são os que a 1b trouxe (`fatia-1b-telas.md`, Task 10, passo 4).

**Exemplos nomeados** (`exemploDoContrato(operationId, status?, nome?)`, em `src\shared\teste\`; ver `03-contrato-e-mocks.md`): `aprovarFeriasComoGestor`, `recusarFeriasComoGestor`, `sugerirOutraDataDasFerias`, `confirmarFeriasComoRh`, `recusarFeriasComoRh`, `aceitarSugestaoDeDataDasFerias` e `recusarSugestaoDeDataDasFerias` 409 `etapaIncorreta`, `jaDecidido`; `confirmarFeriasComoRh` e `recusarFeriasComoRh` 422 `pedidoProprio`, `foraDeGestores`; `pedirAdesaoABeneficio` 422 `naoOpcional`, `naoAceitaDependentes`, `naoAceitaTrajeto`, `funcionarioInativo`; `aprovarDependenteDeBeneficio` e as outras três decisões de dependente e trajeto 409 `adesaoNaoAtiva`, `pedidoJaDecidido`.

**Testes com MSW que precisam existir** (use os `example` do contrato e as pessoas de `ELENCO.md`; o padrão é o da fatia 1b):
1. **Teste de contrato por tela** (a lacuna conhecida do `tsc` com chave extra em `query` ou `params.path`): cada chamada envia **exatamente** os parâmetros certos. Exemplos: `listarFeriasParaORh` com `situacao`, `setorId` e `busca`; `obterCalendarioDeFeriasDaEquipe` sem parâmetros (e com `inicio` e `semanas` quando a tela mandar); `obterCalendarioDePagamento` e `listarExcecoesDePagamento` com `ano`; `definirExcecaoDePagamento` com `ano` e `mes` no caminho e `{ dataPagamento }` no corpo; a conferência com `{ dataInicio, dataFim, abonoPecuniario, diasAbono }`; `pedirAdesaoABeneficio` com `dataInicioDesejada`; `listarEquipeDoGestor` sem parâmetros (tela 51) e `listarAniversariantes` sem parâmetros (telas 32 e 46): as duas entram na cobertura desta fatia, então cada uma precisa do seu `capturar('<operationId>').conforme()`.
2. **Conferência de férias:** não chama com datas vazias nem com fim antes do início; a lista mostra todas as `regras` que vierem; o botão "Enviar pedido" fica desabilitado com `podeEnviar: false`; resposta antiga é descartada; 422 `FERIAS_REGRAS_NAO_ATENDIDAS` mostra as regras do erro.
3. **Sugestão de data:** aceitar chama `aceitarSugestaoDeDataDasFerias`; recusar chama `recusarSugestaoDeDataDasFerias` e mostra exatamente "Seu pedido foi encerrado. Converse com o seu gestor antes de abrir outro pedido de férias."
4. **Decisão do gestor e do RH:** os botões seguem `podeDecidir` e mostram `motivoNaoPodeDecidir`; recusar exige motivo de 5 a 500 caracteres (benefício: 1 a 500); 409 `PEDIDO_ETAPA_INCORRETA` e `PEDIDO_JA_DECIDIDO` mostram o `detail` e pedem os dados de novo; a decisão automática do "Gestor ausente" aparece como "Aprovado automaticamente (gestor ausente)"; o motivo da recusa aparece para quem pediu.
5. **Calendário:** a barra que começa antes da janela mostra "Até 17/10"; quem não tem férias na janela aparece sem barra; as três cores (aprovada, aguardando você, aguardando o RH).
6. **"Presença agora":** `diaUtil: false` mostra só "Hoje não há expediente"; `aindaNaoComecou: 0` não mostra a legenda; o número grande é `trabalhando` e o total é o `total` da API.
7. **Ausência dos textos cortados** (o teste procura o texto **nos rótulos da tela** e **não** o encontra): "Folga ou compensação", "Fim do período de experiência", "Treinamento presencial" (tela 00); "Lembrar Diego por e-mail" e "Lembrar" por e-mail (tela 51); "Avisar" e "por e-mail" no modal 06; "responde por e-mail" nas telas 44 e 45. Método (o mesmo da ficha 5, seção 7, **já pronto na 1b**): `rotulosDaTela(contêiner)` e `acharTextoProibido(rotulos, texto)` de `src\test\textosProibidos.ts` (**não os reescreva**) e as linhas desta fatia acrescentadas à tabela de `src\app\textosProibidos.test.tsx`; **limite de palavra** (`/\bavisar\b/i`, `/\blembrar\b/i`; nas frases, a frase inteira) e **só nos rótulos de interface** dentro do contêiner da tela (`within(screen.getByRole('main'))`: títulos, botões, abas, `columnheader`, `label`, `legend`, `placeholder`, `aria-label`; o `rotulosDaTela` da 1b não lista `placeholder` nem `aria-label`: se a tela os usar com um texto proibido, acrescente-os ao `rotulosDaTela` e ao teste dele), **nunca em células nem no texto vindo dos mocks** (a `mensagem` de um pedido de exemplo pode dizer "avisar").
8. **Datas:** `2026-11-16` aparece como "16/11/2026" com o fuso do processo em `UTC` e em `America/Sao_Paulo` (a prova "outro fuso" do passo 1, com o controle do `TZ`); "Atualizado às 9:42" vem de `2026-10-07T12:42:00Z`.
9. **Perfil:** o gestor chamando a tela do RH (ou o contrário) não a vê (403 tratado); o gestor sem setor vê "Você ainda não é gestor de um setor."
10. **Dados sensíveis:** o cartão de benefício mostra só as linhas ou meios do trajeto, não a origem; nada disso vai ao `console`.
11. **Estados** de cada tela: carregando, vazio, erro (e sem permissão onde couber), como na nota.
12. **Cartão "Próximo pagamento"** (componente único, props `{ data, mesReferencia }`): nas telas **32 e 46** a data vem de `obterPainelDoFuncionario.proximoPagamento` ("6 de novembro", "Holerite de outubro") e **`capturar('obterProximoPagamento').quantas()` é 0**; com `pagamento: null` o cartão **some**; o link "Ver holerites" leva a `/eu/holerites`. A prova de que as telas 36 e 49 o alimentam por `obterProximoPagamento` é da ficha 4 (teste 4).
13. **Menu "Meu espaço"** (decisão do dono, 2026-11-02): com o perfil RH, o grupo "Meu espaço" mostra "Férias" e "Benefícios", e o clique leva a `/eu/ferias` e `/eu/beneficios` (a tela abre com o menu do RH ainda à vista); com o perfil Gestor, "Férias" e "Benefícios" continuam em "Seu dia a dia" e levam aos mesmos endereços. O teste usa `menuVisivel`/`todosOsCaminhosDoMenu` da 1a e `renderizarTela`.

### Pendente de integração

As **41 rotas** da fatia 7 estão `planejada` em `contrato\STATUS.md` (e as das fatias 1, 3, 4, 5 e 6 que estas telas leem também). Enquanto uma rota não estiver `pronta`, tudo roda com MSW e o `PROGRESSO.md` registra "pendente de integração" para: **ver o pedido mudar de etapa de verdade** (pedir, aprovar, confirmar, recusar, sugerir), o calendário e as listas com dados reais, a conferência de férias com as regras reais, as bolinhas do menu, os números de "Presença agora", o e-mail a cada decisão (que é do back-end, só confira que a tela não promete outro) e o cálculo real do próximo pagamento (fim de semana, feriado, mês sem o dia). A sessão **não roda** a API real (design, seção 5, "Pronto no front"): deixe o **roteiro pronto** no `PROGRESSO.md` e consulte `STATUS.md` só para saber se a rota existe; **a pessoa ou o dono** roda o roteiro depois.

## 7. Riscos

- **Dia deslocado por fuso:** `new Date('2026-11-16')` vira 15/11 em São Paulo. Use as funções do passo 1 em **toda** data `date` (inclusive nos campos de data e nas barras do calendário).
- **Regras provisórias de férias:** não as escreva no código; o texto fixo de "Como funciona" (telas 34 e 48) fica num único lugar para trocar rápido.
- **Corrida na conferência:** o RH ou a pessoa muda a data duas vezes seguidas e a resposta da primeira chega depois; vale a última.
- **Dados simulados fixos:** o `example` não muda depois de uma decisão; escreva os testes de "pede de novo" com um handler próprio por teste (`servidor.use`).
- **Calendário de 13 semanas:** colunas por segunda-feira, meses na virada, barras que cortam a janela dos dois lados; é o cálculo mais fácil de errar. Teste com `2026-10-05` (a janela do exemplo: 5/10 a 28/12).
- **Largura de projetor (1366×768, provisório):** as telas mais densas são a **51** (tabela de 7 colunas e quadro de 13 semanas), a **00** (duas colunas, barra e tabela) e o modal **06**, que mede cerca de **900 px de altura** no PNG: com 768 px de tela, o corpo precisa rolar por dentro e os botões ficam sempre visíveis. Projete para 1366 desde já; a fatia 8 fecha.
- **Muitas rotas por tela:** 32, 46, 51 e 00 chamam várias rotas de assuntos diferentes. Cada cartão carrega e falha **sozinho**; um erro não derruba a tela.
- **Quem decide primeiro:** dois decisores (ou o RH ligando "Gestor ausente") fazem o pedido mudar de etapa; o 409 não é erro grave, é "recarregue".
- **Dependência de outras fatias:** o 7d depende dos componentes das fatias 3 a 6; se faltarem, monte o cartão pelo que a nota da tela descreve (com os dados simulados do contrato), **sem inventar rota**, e registre no `PROGRESSO.md`.
- **Texto do Figma que contradiz o contrato:** quando a sessão achar um, vale o contrato; registre em `DUVIDAS.md` só se mudar o que o usuário vê além do que a nota já diz.

## 8. Decisões do dono ainda abertas (`x-provisorio`; construa normalmente)

Lista completa e o efeito em cada tela: no fim de `telas\fatia-7.md` ("Perguntas abertas desta fatia, juntas"). Resumo: **regras de férias do protótipo** (mudam textos e códigos, não a forma); **campo "Quantos dias" do abono**; **prazo da sugestão de data**; **lista de parentescos**; **tipo `BENEFICIOS` nas pendências do RH**; **como o ciclo de avaliação é definido** (fatia 5). Sem `x-provisorio`, mas abertas no roadmap: a definição de "presença agora" (construída pela do contrato) e o catálogo de benefícios.

## 9. Decisão do dono sobre o menu e dúvidas que a sessão já pode registrar (o conteúdo abaixo; o texto para colar sai do modelo de `DUVIDAS.md`)

Registre **na hora em que chegar ao ponto**, com o modelo de `DUVIDAS.md` (onde no plano, o que achou, o que sugere, a pergunta exata); nenhuma delas bloqueia. O item 1 **não é dúvida**: é decisão do dono.
1. **Menu do RH e telas 34 e 35. Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`; **não se registra como dúvida**): o RH tem "Férias" (`/eu/ferias`) e "Benefícios" (`/eu/beneficios`) como itens **ativos** do grupo "Meu espaço" do menu; o gestor tem os dois em "Seu dia a dia". Outra pessoa do RH também pode registrar as férias pela ficha (tela 06).
2. **D-F7-1, tela 51.** Achei: o gestor não vê as sugestões de outra data que fez e ainda esperam resposta (não entram no painel nem no calendário); só as vê na aba Férias da ficha da pessoa (tela 7.1). A rota `listarFeriasDaEquipe` (com `situacao=SUGESTAO_DE_DATA`) as listaria, mas não há tela. Sugiro: deixar assim. Pergunta: "O gestor precisa de uma lista das sugestões de data que aguardam resposta?"
3. **D-F7-2, tela 7.4.** Achei: a tela de pagamento (exceções por mês e calendário) não está no Figma; o contrato sugere um item "Pagamento" em Cadastros, ao lado de "Empresa". Sugiro: seguir o contrato. Pergunta: "Posso montar 'Pagamento' dentro de Cadastros?"

## 10. Conferência visual (uma vez, ao fim da fatia)

No relatório final da fatia, liste para a pessoa conferir **ao lado do print**: 00, 05, 06, 32, 34, 35, 44, 45, 46, 48 e 51. As telas **7.1 a 7.6 não têm print**: a pessoa confere pela descrição de `telas\fatia-7.md`. Peça atenção especial a: a **tela 06** (o período mostrado é o do saldo, não o em andamento), a **tela 51** (ordem alfabética das pessoas, sem "Lembrar Diego", com "Recusar") e a **tela 00** (sem "Folga ou compensação", com "Sem registro 7"). A sessão **não depende** da conferência para seguir.

## 11. Repita o padrão da fatia 1b

Mesma estrutura de pasta, mesmos hooks de dados gerados do contrato, mesmos componentes-base, mesmas mensagens de erro, mesmo ciclo de cada passo (teste que falha, implementação, teste passa, lint e `format:check`, `PROGRESSO.md` com a versão do contrato, commit e push) e a mesma conferência visual no fim.
