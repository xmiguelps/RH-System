# Levantamento da fatia 7 (gestão: férias, benefícios, pagamento e painéis) do back-end

Levantamento só de leitura, feito em 2026-11-13 sobre a branch `feat/fatia-6-tad` (commit `028b067`). Nada foi executado (sem build, teste nem docker): o que é comportamento do banco foi **lido** nos scripts, não medido.

**Abreviações das citações.** `yaml` = `contrato/fontes/70-fatia-7-gestao.yaml`; `base` = `contrato/fontes/00-base.yaml`; `CONV` = `contrato/CONVENCOES.md`; `PROV` = `contrato/PROVISORIOS.md`; `STATUS` = `contrato/STATUS.md`; `ELENCO` = `contrato/ELENCO.md`; `f1` = `contrato/fontes/20-fatia-1-colaboradores.yaml`; `f4` = `contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml`; `roadmap` = `docs/superpowers/plans/2026-10-28-roadmap-completo.md`; `V001`/`V002` = `Banco de Dados/Scripts/V001__esquema_inicial.sql` e `V002__triggers.sql`; `LEIA` = `Banco de Dados/Scripts/LEIA-ME.md`; `App/` = `back-end/src/RHSystem.Application/`; `Infra/` = `back-end/src/RHSystem.Infrastructure/`; `Api/` = `back-end/src/RHSystem.Api/`; `Dom/` = `back-end/src/RHSystem.Domain/`; `tests/` = `back-end/tests/RHSystem.Api.IntegrationTests/`.

**Regra de leitura:** o contrato vale mais que o roadmap, os comentários dos scripts e o código. Onde o contrato e os scripts se calam está escrito **LACUNA** e o que o código teria de decidir.

---

## A. As 41 operações

Respostas comuns: toda rota tem 401 e 500; as de `/api/gestor` e `/api/rh` têm 403; as de `/api/eu` **não** listam 403 (`CONV:108`). "Pg" = `pagina` e `tamanhoPagina` (`base:25-34`).

### A.1 Férias (17)

| # | Método e caminho | operationId (`yaml`) | Perfis | Parâmetros e corpo | Respostas | Comportamento-chave |
|---|---|---|---|---|---|---|
| 1 | GET `/api/eu/ferias` | `obterMinhasFerias` (:22) | F, G, RH | nenhum | 200 `FeriasEuResposta` | `resumo`, `gestorQueAnalisa` (`null` quando só o RH decide, :33-35), `periodosAquisitivos` (antigo para novo), `historico` (novo para antigo, sem paginação, inclui cancelados e recusados). `podeDecidir` sempre falso; `motivoNaoPodeDecidir` com os textos da fatia 3 (:38-41). Reserva: `COM_O_GESTOR` e `COM_O_RH` reservam, `SUGESTAO_DE_DATA` não (:44-47) |
| 2 | POST `/api/eu/ferias` | `pedirFerias` (:221) | F, G, RH | `FeriasPedirComando` (datas, abono, `diasAbono`, `adiantar13Salario`, `mensagem` até 500) | 201 `FeriasPedido`; 400; 422 `FeriasProblemaDeRegra` | Nasce `SOLICITADA`; etapa `GESTOR` ou `RH` fixada na criação (:252-257); gestor ausente = etapa do gestor já aprovada, automática (:240-246); período = o mais antigo com saldo suficiente (:274-275); todas as regras (:277-286); **sem e-mail** ao criar (:269). 422: `FERIAS_REGRAS_NAO_ATENDIDAS` (com `regras`), `FUNCIONARIO_INATIVO`, `FERIAS_SEM_PERIODO_AQUISITIVO` (:288-290) |
| 3 | POST `/api/eu/ferias/conferencia` | `conferirPedidoDeFerias` (:353) | F, G, RH | `FeriasConferenciaComando` | 200 `FeriasConferencia`; 400 | Sempre 200, mesmo com regra falha (:358); não grava; mesma conferência do #2 |
| 4 | POST `/api/eu/ferias/{feriasId}/sugestao/aceitar` | `aceitarSugestaoDeDataDasFerias` (:394) | F, G, RH | caminho | 200 `FeriasPedido`; 404; 409 `Erro409FeriasPedido`; 422 regras | Só o próprio pedido em `SUGESTAO_DE_DATA`. Datas sugeridas viram as do pedido; vira `COM_O_RH`; `decisaoGestor` = `APROVADA` com `decididaPor` = quem sugeriu e `decididaEmUtc` = a resposta da pessoa (:398-401). Reconfere tudo **menos** antecedência, **inclusive** `CABE_NO_SALDO` (:402-404). Sem e-mail (:406). De outra pessoa: 404; fora de `SUGESTAO_DE_DATA`: 409 `PEDIDO_ETAPA_INCORRETA`; terminado: 409 `PEDIDO_JA_DECIDIDO` (:407-408) |
| 5 | POST `/api/eu/ferias/{feriasId}/sugestao/recusar` | `recusarSugestaoDeDataDasFerias` (:468) | F, G, RH | caminho | 200 `FeriasPedido`; 404; 409 | Vira `CANCELADA`, `respostaDaPessoa: RECUSOU`, `decisaoGestor` e `decisaoRh` `null` (:472-473). Sem e-mail. `dataInicio`/`dataFim` continuam as **originais** (exemplo :491-492) |
| 6 | GET `/api/gestor/ferias` | `listarFeriasDaEquipe` (:524) | G | Pg; `situacao` (`FeriasSituacao`, padrão `COM_O_GESTOR`) | 200 `FeriasListaAnalise`; 400 | Equipe = a de `GET /api/gestor/equipe`; sem o próprio gestor nem outros gestores (:527-532); ordem do pedido mais antigo ao mais novo; cada item com `conflitos` e `equipeDisponivel` (:534-536) |
| 7 | GET `/api/gestor/ferias/calendario` | `obterCalendarioDeFeriasDaEquipe` (:591) | G | `inicio` (date; a API usa a segunda da semana, :606), `semanas` 1 a 26, padrão 13 | 200 `FeriasCalendario`; 400 | Itens que encostam na janela: `APROVADA` (inclui em gozo e concluída), `COM_O_RH`, `AGUARDANDO_VOCE`; `SUGESTAO_DE_DATA` fora (:596-599); só pessoas com férias na janela |
| 8 | POST `/api/gestor/ferias/{feriasId}/aprovar` | `aprovarFeriasComoGestor` (:637) | G | caminho, **sem corpo** | 200 `FeriasPedidoAnalise`; 404; 409 | `status` continua `SOLICITADA`; vira `COM_O_RH`; e-mail à pessoa (:640-643). Ordem dos erros: 404 (não existe, outro setor, **pedido do próprio gestor**, de outro gestor), 409 etapa, 409 já decidido; **não há** `PEDIDO_PROPRIO` aqui (:645-649) |
| 9 | POST `/api/gestor/ferias/{feriasId}/recusar` | `recusarFeriasComoGestor` (:695) | G | `FeriasRecusarComando` (`motivo` 5 a 500) | 200; 400; 404; 409 | `REJEITADA` na hora; motivo em `MOTIVO_RECUSA`, visível em `decisaoGestor.observacao` e no e-mail (:698-703) |
| 10 | POST `/api/gestor/ferias/{feriasId}/sugerir-outra-data` | `sugerirOutraDataDasFerias` (:752) | G | `FeriasSugerirComando` (datas) | 200; 400; 404; 409; 422 regras | Continua `SOLICITADA`, etapa `GESTOR`, situação `SUGESTAO_DE_DATA`; `decisaoGestor` `null`; `sugestao` preenchida; e-mail com as datas (:755-762). "Segue as regras do banco": 5 a 30 dias e depois do fim do período aquisitivo do pedido (:758-759) |
| 11 | GET `/api/gestor/equipe/{funcionarioId}/ferias` | `obterFeriasDaPessoaDaEquipe` (:836) | G | caminho | 200 `FeriasFicha`; 404 | Fora do setor e **quem é gestor**: 404; lista todos os pedidos, inclusive os que esperam o gestor (:839-840) |
| 12 | GET `/api/rh/ferias` | `listarFeriasParaORh` (:969) | RH | Pg; `busca` (nome de quem pediu, :993); `situacao` (padrão `COM_O_RH`); `setorId` | 200 `FeriasListaAnalise`; 400 | Ordem do mais antigo ao mais novo; `conflitos` e `equipeDisponivel` pelo setor de quem pede (:976-977) |
| 13 | POST `/api/rh/ferias/{feriasId}/confirmar` | `confirmarFeriasComoRh` (:1081) | RH | caminho, sem corpo | 200; 404; 409; 422 `Erro422FeriasDecisaoRh` | Só em `COM_O_RH`; **não existe "o RH aprova direto"** (:1085-1086); vira `APROVADA`; e-mail. Sem etapa do gestor: quem decide precisa de linha em `GESTORES` (422 `PEDIDO_RH_FORA_DE_GESTORES`) e não pode ser a dona (422 `PEDIDO_PROPRIO`); com etapa do gestor feita por outro, o RH confirma o próprio pedido (:1092-1100) |
| 14 | POST `/api/rh/ferias/{feriasId}/recusar` | `recusarFeriasComoRh` (:1143) | RH | `FeriasRecusarComando` | 200; 400; 404; 409; 422 | `REJEITADA`, inclusive do que o gestor aprovou (:1146-1147); mesma regra de quem decide; motivo em `decisaoRh.observacao`; e-mail |
| 15 | GET `/api/rh/funcionarios/{funcionarioId}/ferias` | `obterFeriasDaFichaDoFuncionario` (:1203) | RH | caminho | 200 `FeriasFicha`; 404 | A lista **não traz** `COM_O_GESTOR` nem `SUGESTAO_DE_DATA` (:1207-1208), mas `resumo.pedidosEmAnalise` os conta (:1209-1210) |
| 16 | POST `/api/rh/funcionarios/{funcionarioId}/ferias` | `registrarFeriasPeloRh` (:1368) | RH | `FeriasRegistrarComando` (`observacao` até 500) | 201 `FeriasPedido`; 400; 404; 422 regras | Nasce `APROVADA`, `etapa: CONCLUIDA`, `gestorResponsavel`, `decisaoGestor` e `decisaoRh` `null` (:1372); sem e-mail (:1373-1374); mesmas regras **menos** antecedência; pode ser no passado (:1376-1378) |
| 17 | POST `/api/rh/funcionarios/{funcionarioId}/ferias/conferencia` | `conferirRegistroDeFerias` (:1444) | RH | `FeriasConferenciaComando` | 200 `FeriasConferencia`; 400; 404 | Sem `ANTECEDENCIA_DE_30_DIAS` na lista (exemplo :1471-1477); `pagamentoAte` alimenta o aviso laranja (:1447-1448) |

### A.2 Benefícios (11)

| # | Método e caminho | operationId | Perfis | Parâmetros e corpo | Respostas | Comportamento-chave |
|---|---|---|---|---|---|---|
| 18 | GET `/api/eu/beneficios` | `obterMeusBeneficios` (:1487) | F, G, RH | nenhum | 200 `BeneficioAdesaoEuResposta` | `beneficios` (ativos e aprovados, com dependentes aprovados e trajeto em vigor), `disponiveis` (opcionais que a pessoa não tem e não pede; recusado ou cancelado volta com `ultimoPedido`), `pedidos` (novo para antigo), `dependentesCadastrados` (aprovados em qualquer benefício, sem repetir) (:1494-1508). Sem paginação |
| 19 | POST `/api/eu/beneficios-adesoes` | `pedirAdesaoABeneficio` (:1577) | F, G, RH | `BeneficioAdesaoPedirComando` | 201 `BeneficioAdesaoPedidoResumo`; 400; 404; 409 `BENEFICIO_JA_ATIVO_OU_PENDENTE`; 422 | Só benefício `opcional`; dependentes só com `aceitaDependentes`; trajeto só com `aceitaTrajeto`; pedido recusado ou cancelado **reabre a mesma linha** (:1590-1592); nasce `PENDENTE`; sem e-mail |
| 20 | POST `/api/eu/beneficios-adesoes/{adesaoId}/dependentes` | `pedirInclusaoDeDependente` (:1630) | F, G, RH | `BeneficioAdesaoDependenteComando` | 201; 400; 404; 409 `BENEFICIO_ADESAO_NAO_ATIVA`; 422 | Só adesão **ativa e aprovada** da própria pessoa (:1633-1636) |
| 21 | POST `/api/eu/beneficios-adesoes/{adesaoId}/trajetos` | `pedirAtualizacaoDeTrajeto` (:1664) | F, G, RH | `BeneficioAdesaoTrajetoComando` | 201; 400; 404; 409; 422 | Pedido novo; o trajeto em vigor continua até aprovar (:1667-1668); origem fora da auditoria (:1672-1673) |
| 22 | GET `/api/rh/beneficios-adesoes` | `listarPedidosDeBeneficio` (:1700) | RH | Pg; `busca` (nome); `situacao` (padrão `PENDENTE`, que inclui aprovada com filho pendente); `beneficioId` | 200 `BeneficioAdesaoRhLista`; 400 | Uma linha por adesão; "pedido mais antigo primeiro"; pendentes de inativado são cancelados pela aplicação (:1706-1708) |
| 23 | POST `.../{adesaoId}/aprovar` | `aprovarAdesaoABeneficio` (:1758) | RH | caminho | 200 `BeneficioAdesaoRh`; 404; 409 `BENEFICIO_PEDIDO_JA_DECIDIDO`; 422 `FUNCIONARIO_INATIVO` | Aprova **junto** os dependentes e o trajeto `PENDENTE` do mesmo pedido (:1761-1765) |
| 24 | POST `.../{adesaoId}/recusar` | `recusarAdesaoABeneficio` (:1800) | RH | `BeneficioAdesaoRecusarComando` (`motivo` 1 a 500) | 200; 400; 404; 409 | Filhos `PENDENTE` viram `CANCELADO` (:1803-1805) |
| 25 | POST `.../{adesaoId}/dependentes/{dependenteId}/aprovar` | `aprovarDependenteDeBeneficio` (:1846) | RH | caminho | 200; 404; 409 `Erro409BeneficioDecisao` | Adesão tem de estar `APROVADO` (409 `BENEFICIO_ADESAO_NAO_ATIVA`); dependente `PENDENTE` (409 `BENEFICIO_PEDIDO_JA_DECIDIDO`); de outra adesão: 404 (:1849-1851) |
| 26 | POST `.../dependentes/{dependenteId}/recusar` | `recusarDependenteDeBeneficio` (:1886) | RH | motivo | 200; 400; 404; 409 | idem |
| 27 | POST `.../{adesaoId}/trajetos/{trajetoId}/aprovar` | `aprovarTrajetoDeBeneficio` (:1932) | RH | caminho | 200; 404; 409 | Passa a ser o trajeto em vigor (o aprovado mais recente, :1935) |
| 28 | POST `.../trajetos/{trajetoId}/recusar` | `recusarTrajetoDeBeneficio` (:1973) | RH | motivo | 200; 400; 404; 409 | O trajeto em vigor não muda (:1976-1977) |

### A.3 Pagamento (5)

| # | Método e caminho | operationId | Perfis | Parâmetros e corpo | Respostas | Comportamento-chave |
|---|---|---|---|---|---|---|
| 29 | GET `/api/eu/pagamento/proximo` | `obterProximoPagamento` (:2019) | F, G, RH | nenhum | 200 `PagamentoProximo` | De hoje em diante (hoje inclusive); exceção vale mais que a regra; sem dia configurado: os dois campos `null` (:2025-2027). **Definição única** (:2023-2024) |
| 30 | GET `/api/rh/pagamento/excecoes` | `listarExcecoesDePagamento` (:2046) | RH | `ano` 2020 a 2100 | 200 `PagamentoExcecaoLista`; 400 | Sem `ano`: do mês atual em diante; ordem de data; sem paginação (:2058) |
| 31 | GET `/api/rh/pagamento/calendario` | `obterCalendarioDePagamento` (:2082) | RH | `ano` (padrão o atual) | 200 `PagamentoCalendario`; 400 | 12 meses; mês sem regra e sem exceção **não vem** (:2089); `ajustes` podem vir dois (:2088-2089) |
| 32 | PUT `/api/rh/pagamento/excecoes/{ano}/{mes}` | `definirExcecaoDePagamento` (:2117) | RH | `PagamentoExcecaoComando` | 200 `PagamentoExcecao`; 400 | Substitui se existe; data fora do mês: 400; funciona sem `diaPagamento` (:2120-2122) |
| 33 | DELETE `/api/rh/pagamento/excecoes/{ano}/{mes}` | `removerExcecaoDePagamento` (:2147) | RH | caminho | 204; 404 | Exceção que não existe: 404 (:2149). **Não lista 400** |

### A.4 Painéis (8)

| # | Método e caminho | operationId | Perfis | Parâmetros | Respostas | Comportamento-chave |
|---|---|---|---|---|---|---|
| 34 | GET `/api/eu/painel` | `obterPainelDoFuncionario` (:2164) | F, G, RH | nenhum | 200 `PainelFuncionario` | Só os cartões da fatia 7: férias, próximo pagamento, benefícios na ordem do catálogo (:2167-2170) |
| 35 | GET `/api/eu/contadores-do-menu` | `obterContadoresDoMenuDoFuncionario` (:2200) | F, G, RH | nenhum | 200 `PainelContadoresFuncionario` | **Já existe** (`Api/Painel/ContadoresDoMenuEndpoints.cs:24-27`); nada de férias aqui |
| 36 | GET `/api/gestor/contadores-do-menu` | `obterContadoresDoMenuDoGestor` (:2224) | G | nenhum | 200 `PainelContadoresGestor` | **Já existe** (:29-32); falta a parte de férias de `pendenciasDaEquipe` (`COM_O_GESTOR`; `SUGESTAO_DE_DATA` não conta, :2229-2231) |
| 37 | GET `/api/rh/contadores-do-menu` | `obterContadoresDoMenuDoRh` (:2251) | RH | nenhum | 200 `PainelContadoresRh` | **Já existe** e está completo (só ponto, :2254-2257) |
| 38 | GET `/api/gestor/painel` | `obterPainelDaEquipe` (:2273) | G | nenhum | 200 `PainelGestor` | `setor`, `totalPessoas`, `aguardandoAprovacao` (férias = os itens de #6; ajustes = resumo da fatia 3), `avaliacao`, `pessoas` em ordem alfabética, sem paginação; gestor sem setor: `setor` `null`, 0 e listas vazias (:2296-2297) |
| 39 | GET `/api/rh/painel` | `obterPainelDoRh` (:2369) | RH | nenhum | 200 `PainelRh` | `totalPendencias` = soma; `pendencias` só com quantidade maior que zero; `proximosDias` 21 dias, máximo 8; `treinamentosEmAndamento` máximo 5; `presencaAgora`; `ultimasAlteracoes` 4 linhas (:2375-2398) |
| 40 | GET `/api/rh/painel/pendencias` | `listarPendenciasDoRh` (:2457) | RH | Pg; `tipo` (`PainelPendenciaTipo`) | 200 `PainelPendenciaLista`; 400 | Uma a uma, do mais antigo ao mais novo, de cinco origens (:2460-2464) |
| 41 | GET `/api/rh/painel/presenca/sem-registro` | `listarPessoasSemRegistroHoje` (:2504) | RH | Pg | 200 `PainelSemRegistroLista`; 400 | Só `SEM_REGISTRO`, ordem alfabética; dia sem expediente: vazia (:2507-2509) |

### A.5 Rotas que dividem prefixo com outras fatias (registrar por `operationId`, nunca por prefixo)

- `/api/gestor/equipe/{funcionarioId}/ferias` (#11): o prefixo já tem ficha e indicadores (`Api/Colaboradores/EquipeEndpoints.cs:33`, `:38`), ponto (`Api/Ponto/PontoGestorEndpoints.cs:28`, `:33`), treinamentos (`Api/Treinamentos/TreinamentosEquipeEndpoints.cs:43`) e advertências (`Api/Comunicacao/AdvertenciasGestorEndpoints.cs:48`, `:58`). O aviso está nos próprios arquivos (`EquipeEndpoints.cs:9-10`; `PontoGestorEndpoints.cs:13`).
- `/api/rh/funcionarios/{funcionarioId}/ferias` e `/ferias/conferencia` (#15 a #17): o prefixo tem `Api/Colaboradores/ColaboradoresEndpoints.cs:63-101`, `Api/Ponto/PontoRhEndpoints.cs:23-43`, `Api/Documentos/DocumentosEndpoints.cs:32-49`, `Api/Comunicacao/HoleritesRhEndpoints.cs:36`, `:51`, `Api/Comunicacao/AdvertenciasRhEndpoints.cs:66`, `:76`, `Api/Treinamentos/TreinamentosEquipeEndpoints.cs:26`. Usar `{funcionarioId:int}` como os vizinhos.
- `/api/rh/beneficios-adesoes` (#22 a #28) ao lado de `/api/rh/beneficios` (catálogo, fatia 1): segmentos diferentes, sem colisão de rota; o risco é só de nome de schema (`BeneficioCatalogo...` × `BeneficioAdesao...`, `CONV:27`).
- `/api/gestor/ferias/calendario` (literal) × `/api/gestor/ferias/{feriasId}/...`: sem ambiguidade se `{feriasId:int}`. O mesmo para `/api/eu/ferias/conferencia` × `/api/eu/ferias/{feriasId}/sugestao/...` e `/api/rh/pagamento/calendario` × `/api/rh/pagamento/excecoes/{ano}/{mes}`.
- `/api/{eu,gestor,rh}/contadores-do-menu`: já mapeadas; a fatia 7 **não** as registra de novo (duplicar o `WithName` quebra o OpenAPI gerado).
- `/api/gestor/painel`, `/api/rh/painel`, `/api/eu/painel`: nenhuma rota de hoje usa `painel` (busca em `Api/`: só `AvisoPainelRh`, um DTO, em `Api/Comunicacao/AvisosRhEndpoints.cs:56`).

---

## B. Catálogo de erros

### B.1 Com código (texto exato do contrato)

| Código | HTTP | `title` | `detail` | Extra | Onde (`yaml`) |
|---|---|---|---|---|---|
| `PEDIDO_ETAPA_INCORRETA` | 409 | "Pedido na etapa errada." | "Este pedido ainda não chegou à sua etapa ou já passou por ela." | | :2549-2553 |
| `PEDIDO_JA_DECIDIDO` | 409 | "Pedido já decidido." | "Este pedido já foi decidido." | | :2557-2561 |
| `FERIAS_REGRAS_NAO_ATENDIDAS` (pedir e aceitar) | 422 | "Pedido de férias recusado." | "O pedido não cumpre uma ou mais regras de férias." | `regras[]` | :340-347, :456-462 |
| `FERIAS_REGRAS_NAO_ATENDIDAS` (sugerir) | 422 | "Sugestão recusada pelas regras." | "As datas sugeridas não cumprem uma ou mais regras de férias." | `regras[]` | :824-830 |
| `FERIAS_REGRAS_NAO_ATENDIDAS` (registrar) | 422 | "Registro de férias recusado." | "O registro não cumpre uma ou mais regras de férias." | `regras[]` | :1432-1438 |
| `FUNCIONARIO_INATIVO` (férias) | 422 | **sem exemplo** | **sem exemplo** | `regraDoBanco`: "Funcionario inativo nao pode solicitar ferias." | :288-289 |
| `FERIAS_SEM_PERIODO_AQUISITIVO` | 422 | **sem exemplo** | **sem exemplo** | | :289-290 |
| `PEDIDO_PROPRIO` | 422 | "Você não pode decidir o próprio pedido." | "A operação foi recusada por uma regra do banco de dados." | `regraDoBanco`: "O gestor nao pode analisar as proprias ferias." | :2721-2726 |
| `PEDIDO_RH_FORA_DE_GESTORES` | 422 | "Quem decide precisa ser gestor também." | "Este pedido não tem etapa do gestor. Para decidi-lo, você precisa estar cadastrado também como gestor." | `regraDoBanco`: "Informe o gestor responsavel pela analise das ferias." | :2730-2735 |
| `BENEFICIO_JA_ATIVO_OU_PENDENTE` | 409 | "Você já pediu este benefício." | "Você já tem este benefício ou já pediu a adesão e o RH ainda vai responder." | | :2568-2572 |
| `BENEFICIO_ADESAO_NAO_ATIVA` | 409 | "A adesão não está ativa." | "Este benefício ainda não está ativo para a pessoa; o pedido só vale em adesão aprovada." | | :2579-2583 |
| `BENEFICIO_PEDIDO_JA_DECIDIDO` | 409 | "Pedido de benefício já respondido." | "Este pedido de benefício já foi aprovado, recusado ou cancelado." | | :2590-2594 |
| `BENEFICIO_NAO_OPCIONAL` | 422 | "Este benefício não se pede." | "Este benefício não é opcional: o RH o concede direto na ficha." | | :2626-2630 |
| `BENEFICIO_NAO_ACEITA_DEPENDENTES` | 422 | "Este benefício não aceita dependentes." | "Este benefício não permite incluir dependentes." | | :2634-2638 |
| `BENEFICIO_NAO_ACEITA_TRAJETO` | 422 | "Este benefício não tem trajeto." | "Só o vale-transporte tem trajeto." | | :2642-2646 |
| `FUNCIONARIO_INATIVO` (pedir benefício, dependente, trajeto) | 422 | "Operação recusada." | "Não é possível pedir nem ativar benefício para uma pessoa inativa." | sem `regraDoBanco` no exemplo | :2650-2654 |
| `FUNCIONARIO_INATIVO` (aprovar adesão) | 422 | "Operação recusada." | "Não é possível ativar benefício para uma pessoa inativa." | a descrição cita a regra do banco "Nao e possivel ativar beneficio para funcionario inativo." (:1765); o exemplo não traz o campo | :2705-2709 |

Os cinco textos de `motivoNaoPodeDecidir` (`yaml:3007-3013`) **já são constantes** em `App/Ponto/Regras/RegrasDeDecisaoDoPedido.cs:27-31`; o sexto, só de férias: "Esperando a resposta de {nome} à sugestão de outra data." (`yaml:3015-3016`).

**Atenção: os textos dos dois 409 de pedido são diferentes dos do ponto.** O ponto usa "Pedido fora da vez." e "Este pedido já foi respondido. Atualize a lista." (`App/Ponto/Mensagens/MensagensDoPonto.cs:66-74`); a fatia 7 tem textos próprios (tabela acima). `tests/Contrato/MensagensDoPontoTests.cs:82` já registra que cada fatia tem o seu texto. Logo: um catálogo `MensagensDaGestao` próprio, com teste contra o contrato, no molde de `tests/Contrato/MensagensDosTreinamentosTests.cs`.

**LACUNA (textos sem exemplo):** `FUNCIONARIO_INATIVO` de férias e `FERIAS_SEM_PERIODO_AQUISITIVO` não têm `title` nem `detail` no contrato; as mensagens de cada `FeriasRegraCodigo` só têm exemplos de alguns estados (D.5). O código escolhe o texto e o teste de conformidade só confere a forma.

**400 (sem código, com `errors` por campo):** `motivo` fora de 5 a 500 (férias, `yaml:3133-3136`) ou de 1 a 500 (benefício, :3370-3373); `mensagem`/`observacao` acima de 500 (:2843, :2858); `dataFim` antes de `dataInicio` (:2816); `diasAbono` fora de 0 a 10 (:2822-2825); `semanas` fora de 1 a 26 (:611); `ano` fora de 2020 a 2100 e `mes` fora de 1 a 12 (:2125-2126); `dataPagamento` fora do mês da rota (:2121, :3475); `dataInicioDesejada` anterior a hoje (:3351); `dataNascimento` futura (:3215); `dependentes` acima de 10 (:3357); tamanhos de `nome` 150, `parentesco` 30, `origem` 200, `destino` 200, `linhasMeios` 255 (:3208-3212, :3236-3242); enum inválido em `situacao` e `tipo`.

**LACUNA (400 × 422 em abono):** `abonoPecuniario` falso com `diasAbono` maior que zero, ou verdadeiro com 0, fere o `CHECK` do banco (`V001:794-797`). O contrato tem a regra `ABONO_ATE_10_DIAS` ("abono de 1 a 10 dias", `yaml:2777`) e o texto de `diasAbono` ("0 quando falso, 1 a 10 quando verdadeiro", :2827). Decidir: 400 no campo ou regra `ABONO_ATE_10_DIAS` não atendida. Sugestão: a combinação incoerente vira a regra não atendida (a conferência responde 200 e mostra o visto), e só o número fora de 0 a 10 vira 400.

### B.2 O que o banco impõe e o contrato não declara (validar antes; senão vira 500 ou 409 genérico)

**`FERIAS` (`V001:754-848`; triggers `V002:910-992`)**

| Regra do banco | Onde | Vira o quê |
|---|---|---|
| `CHK_FERIAS_DURACAO` (5 a 30 dias) | `V001:793` | regra `DURACAO_DA_PARTE` |
| `CHK_FERIAS_ABONO` | `V001:794-797` | regra `ABONO_ATE_10_DIAS` (ver lacuna acima) |
| `CHK_FERIAS_INICIO_APOS_AQUISITIVO` e o trigger "Inicio das ferias deve ser posterior ao fim do periodo aquisitivo." | `V001:792`; `V002:933-936` | **não há `FeriasRegraCodigo` para isto** (E.4.3): a escolha do período tem de só considerar períodos que terminam antes do início |
| Trigger "Periodo aquisitivo nao pode comecar antes da admissao." | `V002:928-931` | só acontece com período calculado errado (ou readmissão, E.4.13) |
| Trigger "Funcionario inativo nao pode solicitar ferias." | `V002:923-926` | 422 `FUNCIONARIO_INATIVO` (conferir antes; o registro pelo RH é o caminho real) |
| Trigger "Funcionario desligado nao pode iniciar ferias depois do desligamento." | `V002:938-941` | inalcançável: quem tem `DATA_DESLIGAMENTO` já está inativo (`LEIA:116`) |
| Trigger "Transicao de status de ferias invalida." | `V002:958-966` | todo UPDATE de status é condicional ao status anterior; a tarefa agendada só faz as transições aceitas |
| Trigger "Informe o gestor responsavel pela analise das ferias." (status vira `APROVADA` ou `REJEITADA` com `ID_GESTOR` nulo) | `V002:968-972` | 422 `PEDIDO_RH_FORA_DE_GESTORES`, conferido antes |
| Trigger "O gestor nao pode analisar as proprias ferias." | `V002:974-981` | 422 `PEDIDO_PROPRIO`, conferido antes |
| `CHK_FERIAS_DATAS_ANALISE` (`DATA_ANALISE >= DATA_SOLICITACAO`) | `V001:799` | as duas datas em **-03:00** (roadmap:73: gravar a solicitação em UTC faz este `CHECK` recusar a aprovação) |
| `CHK_FERIAS_ETAPA_GESTOR`, `_ETAPA_COM_GESTOR`, `_AUTOMATICA` | `V001:806-811` | resultado e data juntos; resultado exige `ID_GESTOR`; automática só com `APROVOU` |
| `CHK_FERIAS_RECUSA_ENCERRA`, `_RH_APOS_GESTOR` | `V001:812-817` | `RECUSOU` e `REJEITADA` no **mesmo** UPDATE; nunca gravar o RH num pedido que o gestor recusou |
| `CHK_FERIAS_CONFIRMACAO_RH` | `V001:814-815` | `ID_FUNCIONARIO_RH` e `DATA_CONFIRMACAO_RH` juntos |
| `CHK_FERIAS_SUGESTAO_*` (datas juntas, 5 a 30, depois do aquisitivo, só com `SUGERIU_DATA`) | `V001:821-829` | validar a sugestão antes (regra `DURACAO_DA_PARTE`); gravar resultado e datas no mesmo UPDATE |
| `CHK_FERIAS_RESPOSTA_*` e `_SUGESTAO_NAO_ACEITA_CANCELA` | `V001:830-835` | `RECUSOU` ou `EXPIROU` no mesmo UPDATE que põe `CANCELADA`; resposta e data juntas |
| `CHK_FERIAS_MOTIVO_SO_REJEITADA`, `_NAO_VAZIO`, `_RECUSA_COM_MOTIVO` | `V001:837-844` | motivo no mesmo UPDATE do `REJEITADA`; `Trim` antes de medir 5 a 500 (um motivo de 5 espaços passaria no 400 e cairia no `CHECK`) |
| `FK_FERIAS_GESTORES`, `FK_FERIAS_FUNCIONARIOS_RH` | `V001:787-789`, `:801-803` | ids vêm do servidor, nunca do corpo |

**`BENEFICIOS_FUNCIONARIOS` (`V001:703-743`; trigger `V002:888-902`)**

| Regra | Onde | Vira o quê |
|---|---|---|
| `UQ_BENEFICIOS_FUNCIONARIOS_FUNCIONARIO_BENEFICIO` | `V001:725` | pedido de novo = UPDATE da mesma linha; a corrida de dois pedidos novos dá 1062, que vira 409 `BENEFICIO_JA_ATIVO_OU_PENDENTE` |
| `CHK_..._ATIVO_SO_APROVADO` (o padrão de `BENEFICIO_ATIVO` é `TRUE`, `V001:707`) | `V001:732-733` | o pedido grava `BENEFICIO_ATIVO = FALSE` **explicitamente**; não declarar o padrão do banco no EF (a coluna já está mapeada pela fatia 1) |
| `STATUS_PEDIDO` sem padrão | `V001:709`; `LEIA:165` | sempre informar |
| `CHK_..._PENDENTE_COM_DATA` | `V001:734-735` | `DATA_PEDIDO` no INSERT e no UPDATE que reabre |
| `CHK_..._RESPOSTA_JUNTAS` | `V001:736-737` | ao reabrir, limpar `ID_FUNCIONARIO_RH` **e** `DATA_RESPOSTA` juntos |
| `CHK_..._RECUSA_COMPLETA` | `V001:738-740` | motivo com `Trim` não vazio (o contrato aceita 1 caractere, `yaml:3372`: um espaço passaria no 400 e cairia no `CHECK`) |
| Trigger "Nao e possivel ativar beneficio para funcionario inativo." **só no INSERT com ativo verdadeiro** | `V002:888-902` | a aprovação é UPDATE: **o banco não barra** aprovar pedido de inativo; a aplicação responde 422 `FUNCIONARIO_INATIVO` |

**`DEPENDENTES` (`V001:1151-1179`) e `TRAJETOS_VALE_TRANSPORTE` (`V001:1186-1214`):** `STATUS_PEDIDO` com padrão `'PENDENTE'` e `DATA_PEDIDO` com `DEFAULT CURRENT_TIMESTAMP` (mapear como gerada na inclusão ou gravar sempre; D.6); resposta juntas (`:1173-1174`, `:1208-1209`); recusa com motivo não vazio (`:1175-1176`, `:1210-1211`), **sem** exigir quem respondeu (a adesão exige). Nenhuma chave única: nada impede o mesmo dependente duas vezes nem dois trajetos pendentes (D.5).

**`PAGAMENTOS_EXCECOES` (`V001:1279-1289`):** `UQ (ANO, MES)` (:1285): dois PUT ao mesmo tempo dão 1062, tratar como "já existe, atualizar"; `CHK_..._MES` (:1286); `CHK_..._DATA_NO_MES` (:1287-1288) = o 400 do contrato; `ANO` é do tipo `YEAR` (1901 a 2155), mais largo que o 2020 a 2100 do contrato.

**`FILA_EMAILS`:** tipo `^[A-Z][A-Z0-9_]{2,39}$` e chave começando por `TIPO:` (`V001:1435-1438`); o comentário do script já prevê o tipo `FERIAS_DECIDIDAS` (`V001:3123`). **`AUDITORIA_EVENTOS`:** área e ação em `CHECK` (`V001`, seção 44): as áreas `FERIAS` e `BENEFICIOS` existem (`Dom/Auditoria/AuditoriaArea.cs:13`, `:19`); **não há** área de pagamento nem ações "sugeriu" ou "cancelou" (`Dom/Auditoria/AuditoriaAcao.cs:4-20`), e valor novo é mudança de versão menor do contrato (`CONV:26`).

---

## C. Schemas (`yaml:2736-3803`; base em `base:51-175`)

### C.1 Enums

| Enum | Valores | Origem |
|---|---|---|
| `FeriasStatus` | `SOLICITADA`, `APROVADA`, `REJEITADA`, `EM_GOZO`, `CONCLUIDA`, `CANCELADA` | :2740-2746; iguais ao banco (`V001:798`) |
| `FeriasSituacao` | `COM_O_GESTOR`, `SUGESTAO_DE_DATA`, `COM_O_RH`, `APROVADA`, `EM_GOZO`, `CONCLUIDA`, `REJEITADA`, `CANCELADA` | :2747-2763; calculada |
| `FeriasRegraCodigo` | `DURACAO_DA_PARTE`, `CABE_NO_SALDO`, `PARTE_DE_14_DIAS`, `MAXIMO_DE_3_PARTES`, `ABONO_ATE_10_DIAS`, `ANTECEDENCIA_DE_30_DIAS`, `NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO`, `SEM_SOBREPOSICAO` | :2765-2790; **provisório** |
| situação do período aquisitivo | `EM_AQUISICAO`, `COM_SALDO`, `SEM_SALDO` | :2891-2896 |
| `respostaDaPessoa` | `ACEITOU`, `RECUSOU`, `EXPIROU`, `null` | :2963-2966; banco `V001:819-820` |
| situação do item do calendário | `APROVADA`, `COM_O_RH`, `AGUARDANDO_VOCE` | :3150-3155 (**não** é `FeriasSituacao`) |
| `BeneficioAdesaoSituacao` | `PENDENTE`, `APROVADO`, `RECUSADO`, `CANCELADO` | :3186-3192; as três tabelas |
| tipo do pedido de benefício | `ADESAO`, `DEPENDENTE`, `TRAJETO` | :3313 |
| `ultimoPedido.situacao` | `RECUSADO`, `CANCELADO` | :3291 |
| origem e ajustes do pagamento | `REGRA`, `EXCECAO`; `ULTIMO_DIA_DO_MES`, `ANTECIPADO_FIM_DE_SEMANA`, `ANTECIPADO_FERIADO` | :3427-3438; já existem em `App/Comunicacao/Regras/RegraDoPagamento.cs:4-16` |
| `PainelPendenciaTipo` | `AJUSTE_DE_PONTO`, `FERIAS`, `ATESTADO`, `ADMISSAO_COM_DOCUMENTOS_PENDENTES`, `BENEFICIOS` | :3497-3510; `BENEFICIOS` **provisório** |
| tipo do próximo dia | `FERIADO`, `ANIVERSARIO` | :3566 |
| `avaliacaoSituacao` | `RESPONDIDA`, `PENDENTE`, `NAO_SE_APLICA`, `null` | :3715-3718 |
| da base | `EtapaDoPedido` (`GESTOR`, `RH`, `CONCLUIDA`), `SituacaoHoje`, `DecisaoDeEtapa.decisao` (`APROVADA`, `REJEITADA`) | `base:61-107` |

### C.2 Férias

- **`FeriasPedido`** (:2974-3052): 20 campos, todos obrigatórios (inclusive os que podem ser `null`). `dias` conta o primeiro e o último dia. `etapa` é fixada na criação e só avança (:2993).
- **`FeriasPedidoAnalise`** (:3064-3077) = `FeriasPedido` + `conflitos[]` (`FeriasConflito`: pessoa, datas, `diasEmComum`) + `equipeDisponivel` (`null` quando a pessoa não tem setor). `conflitos` vazio quando o pedido já foi decidido (:3074).
- **`FeriasSugestao`** (:2953-2972): `expiraEm` é data (**provisório**: 5 dias corridos depois da sugestão ou 2 dias antes do início sugerido, o que vier primeiro). Os exemplos batem com "data local da sugestão + 5" (:803-806; :443-446).
- **`FeriasPeriodoAquisitivo`** (:2884-2916): `diasDeDireito` 30; `diasTirados` = gozo + abono de `APROVADA`, `EM_GOZO`, `CONCLUIDA`; `diasEmPedido` = gozo + abono de `COM_O_GESTOR` e `COM_O_RH`; `diasDisponiveis` = direito menos tirados (0 em `EM_AQUISICAO`); `prazoParaTirar` = fim + 12 meses; `mesesTrabalhados` e `disponivelAPartirDe` só em `EM_AQUISICAO`.
- **`FeriasResumo`** (:2926-2951): `diasDisponiveis` = soma dos períodos fechados (**o mesmo número de todas as telas**, inclusive o indicador da fatia 1); `periodoDoSaldo` = o fechado mais antigo com saldo; `proximasFerias` = as próximas `APROVADA` que ainda não começaram; `pedidosEmAnalise` conta as três situações em análise.
- **`FeriasConferencia`** (:2861-2882): `totalDias`, `diasQueSobram` (pode ser negativo), `podeEnviar`, `pagamentoAte` (2 dias **corridos** antes do início; `null` com datas inválidas), `regras[]`.
- **`FeriasProblemaDeRegra`** (:3171-3181): `ProblemaDeRegraDeNegocio` + `regras[]` (só com `FERIAS_REGRAS_NAO_ATENDIDAS`). O exemplo do 422 traz **só algumas** regras (:345-347), a conferência traz todas: forma livre.
- Comandos: `FeriasConferenciaComando` (:2810-2829), `FeriasPedirComando` (+ `adiantar13Salario`, `mensagem`), `FeriasRegistrarComando` (+ `adiantar13Salario`, `observacao`), `FeriasSugerirComando`, `FeriasRecusarComando`.
- `FeriasEuResposta` (:3090-3105), `FeriasFicha` (:3107-3119), `FeriasCalendario` (:3157-3169).

### C.3 Benefícios

- **`BeneficioAdesaoCartao`** (:3258-3284): `inicioEm` = a data pedida (`null` quando o RH deu direto); `dependentes` só os `APROVADO`; `trajetoAtual` = o `APROVADO` mais recente.
- **`BeneficioAdesaoDisponivel`** (:3295-3306) com `ultimoPedido` (`BeneficioAdesaoUltimoPedido`, :3286-3293).
- **`BeneficioAdesaoPedidoResumo`** (:3308-3327): `id` muda de significado conforme `tipo`; `titulo` pronto; `respostaPrevistaAte` **obrigatória e não nula** (5 dias úteis depois do pedido, contando os feriados da empresa).
- **`BeneficioAdesaoRh`** (:3376-3403): `pedidoEmUtc`, `respostaPrevistaAte` e `dataInicioDesejada` aceitam `null` (benefício dado direto); `trajetos[]` traz **todos**, não só o em vigor.
- `BeneficioAdesaoDependente` (:3217-3229), `BeneficioAdesaoTrajeto` (:3244-3256), comandos (:3203-3215, :3231-3242, :3346-3374).
- `parentesco`: texto livre de 1 a 30 (**provisório**, :3209-3214).

### C.4 Pagamento

`PagamentoData` (:3419-3438), `PagamentoCompetencia`, `PagamentoProximo` (:3447-3461; a competência é **sempre o mês anterior ao do pagamento**), `PagamentoExcecao`, `PagamentoExcecaoComando`, `PagamentoExcecaoLista`, `PagamentoCalendario`. O record `PagamentoDoMes` do código já tem os cinco campos de `PagamentoData` e a competência (`App/Comunicacao/Regras/RegraDoPagamento.cs:22-25`).

### C.5 Painéis

`PainelPendenciaResumo` (:3512-3527; `quantidade` mínima 1), `PainelPendenciaItem` (:3529-3548), `PainelProximoDia`, `PainelTreinamentoAndamento`, `PainelPresencaAgora` (:3583-3606), `PainelAlteracao` (:3608-3615), `PainelRh`, `PainelSemRegistroItem` (`FuncionarioResumo` + `situacaoHoje`), `PainelSetor`, `PainelAjusteDePontoPendente` (:3663-3677), `PainelAvaliacaoDoCiclo` (**provisório**), `PainelPontoHoje` (:3688-3702; `horario` em `HH:mm` de São Paulo), `PainelPessoaDaEquipe` (:3704-3722), `PainelGestorAprovacoes`, `PainelGestor`, `PainelBeneficioResumo`, `PainelFeriasResumo`, `PainelFuncionario`, e os três de contadores (já existem em `App/Painel/PainelContadoresDtos.cs`).

### C.6 Definições únicas que várias rotas usam (um cálculo só, senão as telas divergem)

1. **Saldo de férias** (`diasDisponiveis` de `FeriasResumo`): #1, #11, #15, #34, a coluna de #38 e o indicador `feriasDisponiveisDias` da fatia 1 (`f1:479`, `:889`; `yaml:2931`, `:3714`).
2. **Conferência das regras**: #2, #3, #4, #10 (parte), #16, #17.
3. **Situação e etapa do pedido**, **`podeDecidir`** e os seis textos: todas as rotas de férias.
4. **`conflitos` e `equipeDisponivel`**: #6, #8, #9, #10, #12, #13, #14, #38 e o `resumo` de férias do #39.
5. **Equipe do gestor**: a de `DaEquipe` (`App/Escopo/IEscopoDoUsuario.cs:6-14`).
6. **Próximo pagamento**: #29, #34, o indicador da fatia 1 (`f1:481`) e o holerite agendado (`f4:756-759`).
7. **`SituacaoHoje` e `PresencaContagens`**: #38, #39, #41 (regra pronta em `App/Colaboradores/SituacaoDeHoje.cs:44-74`).
8. **Fila do RH por tipo**: `quantidade` do #39, os itens do #40 e, para o ponto, o contador do #37 (`yaml:2256-2257`).
9. **5 dias úteis** (`respostaPrevistaAte`): #18 a #28.

---

## D. O banco

Os scripts que o contrato exige (`yaml:3-7`) **já existem**, todos dentro do `V001`: `BENEFICIOS.OPCIONAL`, `ACEITA_DEPENDENTES`, `ACEITA_TRAJETO` (`V001:692-694`); `FERIAS.MOTIVO_RECUSA` (`V001:781`); `TREINAMENTOS.ENCERRADO_EM` (fatia 6); `FILA_EMAILS` (`V001:1412`); `AUDITORIA_EVENTOS` (`V001:1507`). **Nenhum script novo é necessário** para o que o contrato descreve. O próximo número livre é `V006` e quem numera é o dono (`LEIA:122`).

### D.1 Tabelas, com a nulabilidade que o teste de mapeamento vai conferir

Faltam mapear (estão em `tests/Persistencia/tabelas-sem-mapeamento.txt:5-9`): **`FERIAS`, `DEPENDENTES`, `TRAJETOS_VALE_TRANSPORTE`, `PAGAMENTOS_EXCECOES`**. `BENEFICIOS_FUNCIONARIOS` já tem entidade, mas só com cinco colunas (`Dom/Beneficios/BeneficioDoFuncionario.cs:16-32`): faltam as seis do pedido.

**`FERIAS`** (`V001:754-848`)

| Coluna | Tipo | Nulo | Padrão | Uso |
|---|---|---|---|---|
| `ID_FERIAS` | INT | não | auto | `id` |
| `ID_FUNCIONARIO` | INT | não | | FK |
| `DATA_INICIO_PERIODO_AQUISITIVO`, `DATA_FIM_PERIODO_AQUISITIVO` | DATE | não | | `periodoAquisitivo` |
| `DATA_INICIO`, `DATA_FIM` | DATE | não | | gozo |
| `ABONO_PECUNIARIO` | BOOLEAN | não | `FALSE` | |
| `DIAS_ABONO` | TINYINT | não | `0` | |
| `STATUS_FERIAS` | VARCHAR(30) | não | `'SOLICITADA'` | o registro do RH grava `APROVADA`: **não declarar o padrão no EF** |
| `DATA_SOLICITACAO` | DATETIME | **sim** | o trigger põe `NOW()` (`V002:943-945`) | `solicitadaEmUtc`, em -03:00 |
| `DATA_ANALISE` | DATETIME | sim | o trigger põe `NOW()` na decisão final (`V002:983-985`) | data da decisão final |
| `ID_GESTOR` | INT | sim | | FK para `GESTORES`: o gestor da etapa |
| `OBSERVACAO` | TEXT | sim | | a mensagem da pessoa ou a observação do RH; **o trigger de inativação escreve nela** (`V002:134-135`) |
| `RESULTADO_GESTOR` | VARCHAR(20) | sim | | `APROVOU`, `RECUSOU`, `SUGERIU_DATA` |
| `DATA_ETAPA_GESTOR` | DATETIME | sim | | quando o gestor decidiu ou sugeriu |
| `ETAPA_GESTOR_AUTOMATICA` | BOOLEAN | não | `FALSE` | gestor ausente |
| `ID_FUNCIONARIO_RH` | INT | sim | | quem do RH decidiu |
| `DATA_CONFIRMACAO_RH` | DATETIME | sim | | quando |
| `ADIANTAR_13_SALARIO` | BOOLEAN | não | `FALSE` | |
| `SUGESTAO_DATA_INICIO`, `SUGESTAO_DATA_FIM` | DATE | sim | | |
| `RESPOSTA_SUGESTAO` | VARCHAR(10) | sim | | |
| `DATA_RESPOSTA_SUGESTAO` | DATETIME | sim | | |
| `MOTIVO_RECUSA` | TEXT | sim | | |

Índices: `IDX_FERIAS_STATUS_PERIODO`, `IDX_FERIAS_STATUS_SOLICITACAO` (`V001:846-847`), `IDX_FERIAS_FUNCIONARIO_INICIO` (`V001:896-897`).

**`BENEFICIOS_FUNCIONARIOS`** (`V001:703-743`): acrescentar ao mapeamento `DATA_PEDIDO` (DATETIME, nulo), `DATA_INICIO_DESEJADA` (DATE, nulo), `AUTORIZA_DESCONTO_FOLHA` (BOOLEAN, não nulo, padrão `FALSE`), `ID_FUNCIONARIO_RH` (INT, nulo), `DATA_RESPOSTA` (DATETIME, nulo), `MOTIVO_RECUSA` (TEXT, nulo).

**`DEPENDENTES`** (`V001:1151-1179`): `ID_DEPENDENTE` (auto), `ID_BENEFICIO_FUNCIONARIOS` (não nulo), `NOME` VARCHAR(150), `PARENTESCO` VARCHAR(30), `DATA_NASCIMENTO` DATE (todos não nulos), `STATUS_PEDIDO` (não nulo, padrão `'PENDENTE'`), `DATA_PEDIDO` (não nulo, `DEFAULT CURRENT_TIMESTAMP`), `ID_FUNCIONARIO_RH`, `DATA_RESPOSTA`, `MOTIVO_RECUSA` (nulos).

**`TRAJETOS_VALE_TRANSPORTE`** (`V001:1186-1214`): `ID_TRAJETO`, `ID_BENEFICIO_FUNCIONARIOS`, `ORIGEM` VARCHAR(200), `DESTINO` VARCHAR(200), `LINHAS_MEIOS` VARCHAR(255) (não nulos) e as mesmas cinco colunas de situação.

**`PAGAMENTOS_EXCECOES`** (`V001:1279-1289`): `ID_PAGAMENTO_EXCECAO` (auto), `ANO` **YEAR** não nulo, `MES` TINYINT não nulo, `DATA_PAGAMENTO` DATE não nulo. A leitura de hoje converte o `YEAR` com `CAST(ANO AS SIGNED)` em SQL cru (`Infra/Comunicacao/ProximoPagamento.cs:31`): o mapeamento do tipo `YEAR` pelo Pomelo **não foi conferido** e pede uma sonda.

**`GESTORES`** (`V001:112-127`, já mapeada): `APROVACAO_AUTOMATICA`, `APROVACAO_AUTOMATICA_ATE`. **`CONFIGURACOES_EMPRESA`** (`V001:923-938`, já mapeada): `DIA_PAGAMENTO` é **não nulo** (1 a 31). Logo "empresa sem dia de pagamento configurado" (`yaml:2027`) só existe quando a linha da empresa não existe.

### D.2 Triggers (texto exato, todos `SQLSTATE '45000'`)

- `TRG_FERIAS_BEFORE_INSERT` (`V002:910-950`): as quatro recusas de B.2; preenche `DATA_SOLICITACAO` e `STATUS_FERIAS` quando vêm nulos. **Não valida status inicial nem gestor**: o RH registra já `APROVADA` com `ID_GESTOR` nulo (`V001:1916-1919`).
- `TRG_FERIAS_BEFORE_UPDATE` (`V002:952-992`): só age **quando o status muda**. Transições aceitas: `SOLICITADA` para `APROVADA`, `REJEITADA`, `CANCELADA`; `APROVADA` para `EM_GOZO`, `CONCLUIDA`, `CANCELADA`; `EM_GOZO` para `CONCLUIDA`. Na ida para `APROVADA` ou `REJEITADA` exige `ID_GESTOR` e que o funcionário dessa linha de `GESTORES` não seja a pessoa do pedido. **O contrato cita `Criacao_Triggers:966-979`** (`yaml:1092`): hoje é `V002:968-981`.
- `TRG_FUNCIONARIOS_AFTER_UPDATE` (`V002:117-140`): ao inativar, desliga `BENEFICIO_ATIVO` e cancela as férias `SOLICITADA` e `APROVADA` com `DATA_INICIO > CURDATE()`, anexando a `OBSERVACAO` o texto " | Cancelada automaticamente: funcionario inativado". **Não** toca em `EM_GOZO`, em férias que começam hoje, em `STATUS_PEDIDO` de adesão, dependente ou trajeto, nem nas colunas da etapa.
- `TRG_BENEFICIOS_FUNCIONARIOS_BEFORE_INSERT` (`V002:888-902`): só recusa INSERT **ativo** para inativo.
- Tabelas novas (`DEPENDENTES`, `TRAJETOS_VALE_TRANSPORTE`, `PAGAMENTOS_EXCECOES`): **sem trigger** (`V001:2611`, `:2644`, `:2717`).

### D.3 Como os estados ficam nas colunas (férias)

Não existe coluna de etapa. O ponto já resolveu assim e a fatia 7 copia (`App/Ponto/Regras/EtapaDoPedidoDeAjuste.cs:7-20`): **o `ID_GESTOR` é gravado na criação** quando o pedido tem etapa do gestor.

| `situacao` | `STATUS_FERIAS` | `ID_GESTOR` | `RESULTADO_GESTOR` | `RESPOSTA_SUGESTAO` | `etapa` |
|---|---|---|---|---|---|
| `COM_O_GESTOR` | `SOLICITADA` | preenchido | nulo | nulo | `GESTOR` |
| `SUGESTAO_DE_DATA` | `SOLICITADA` | preenchido | `SUGERIU_DATA` | nulo | `GESTOR` |
| `COM_O_RH` (gestor aprovou) | `SOLICITADA` | preenchido | `APROVOU` (automática ou não) | nulo | `RH` |
| `COM_O_RH` (sugestão aceita) | `SOLICITADA` | preenchido | `SUGERIU_DATA` | `ACEITOU` | `RH` |
| `COM_O_RH` (sem etapa do gestor) | `SOLICITADA` | **nulo** | nulo | nulo | `RH` |
| `APROVADA`, `EM_GOZO`, `CONCLUIDA` | iguais | qualquer | qualquer | | `CONCLUIDA` |
| `REJEITADA` pelo gestor | `REJEITADA` | preenchido | `RECUSOU` | | `CONCLUIDA` |
| `REJEITADA` pelo RH | `REJEITADA` | preenchido | nulo, `APROVOU` ou `SUGERIU_DATA` | | `CONCLUIDA` (`ID_FUNCIONARIO_RH` preenchido) |
| `CANCELADA` | `CANCELADA` | qualquer | qualquer | `RECUSOU`, `EXPIROU` ou nulo | `CONCLUIDA` |

Tradução para `decisaoGestor` (`yaml:3030-3039`): `APROVOU` vira `APROVADA` (`automatica` de `ETAPA_GESTOR_AUTOMATICA`, `decididaPor` `null` quando automática); `RECUSOU` vira `REJEITADA` com `observacao` = `MOTIVO_RECUSA`; `SUGERIU_DATA` só vira `APROVADA` com `RESPOSTA_SUGESTAO = ACEITOU`, e então `decididaEmUtc` = `DATA_RESPOSTA_SUGESTAO` e `decididaPor` = o gestor do `ID_GESTOR`; nos outros casos `null`. `sugestao.sugeridaEmUtc` = `DATA_ETAPA_GESTOR`. `decisaoRh` (`yaml:3040-3048`): existe quando `ID_FUNCIONARIO_RH` está preenchido; `REJEITADA` leva `MOTIVO_RECUSA`. O registro direto do RH (`APROVADA` sem `ID_FUNCIONARIO_RH`) tem `decisaoRh` `null`.

**LACUNA:** férias `APROVADA` pelo RH e depois `CANCELADA` pela inativação ficam com `ID_FUNCIONARIO_RH` preenchido. O contrato só define `decisaoRh` para `APROVADA` e `REJEITADA` (:3044-3045). Sugestão: manter a `decisaoRh` (`APROVADA`) no histórico, porque a decisão existiu.

**Benefícios:** adesão ativa = `STATUS_PEDIDO = 'APROVADO'` **e** `BENEFICIO_ATIVO = TRUE`. "Inativa" (`yaml:1634`) = `APROVADO` com `BENEFICIO_ATIVO = FALSE` (o RH tirou na ficha ou a pessoa foi inativada). Dado direto pelo RH = `APROVADO` com `DATA_PEDIDO` nulo (`Dom/Beneficios/BeneficioDoFuncionario.cs:35-42`). Trajeto em vigor = o `APROVADO` de maior `DATA_RESPOSTA` (`V001:2630-2632`).

### D.4 Como cada coisa é gravada

- **Pedir férias:** INSERT com `STATUS_FERIAS = SOLICITADA`, o período escolhido, `OBSERVACAO` = `mensagem`, `ADIANTAR_13_SALARIO`, e a etapa de `IGestorAusente.EtapaInicialDoPedidoAsync` (`App/Gestores/IGestorAusente.cs:10-32`): com gestor, `ID_GESTOR`; com gestor ausente, `ID_GESTOR` + `RESULTADO_GESTOR = APROVOU` + `DATA_ETAPA_GESTOR` + `ETAPA_GESTOR_AUTOMATICA = TRUE`; sem gestor, tudo nulo.
- **Gestor aprova:** UPDATE condicional (`SOLICITADA`, `ID_GESTOR` = o dele, resultado nulo) pondo `APROVOU` e a data. **Não tocar em `OBSERVACAO`** (o ponto sobrescreve a observação na decisão, `Infra/Ponto/PedidosDeAjusteRepositorio.cs:63`; em férias a coluna é a mensagem da pessoa).
- **Gestor recusa:** um UPDATE só com `STATUS = REJEITADA`, `RECUSOU`, a data e `MOTIVO_RECUSA`.
- **Gestor sugere:** UPDATE condicional com `SUGERIU_DATA`, a data e as duas datas sugeridas, juntos.
- **Pessoa aceita:** UPDATE condicional (resultado `SUGERIU_DATA`, resposta nula) copiando as datas sugeridas para `DATA_INICIO` e `DATA_FIM` e gravando `ACEITOU` e a data. As datas sugeridas **ficam** (o `CHECK` `V001:828-829` exige). As datas originais se perdem da tabela: só o log de auditoria as guarda.
- **Pessoa recusa / sugestão expira:** UPDATE com `CANCELADA` + `RECUSOU` ou `EXPIROU` + a data.
- **RH confirma ou recusa:** UPDATE condicional na etapa do RH com o status final, `ID_FUNCIONARIO_RH`, `DATA_CONFIRMACAO_RH`, `ID_GESTOR = COALESCE(ID_GESTOR, linha de GESTORES de quem decide)` e, na recusa, o motivo (molde: `Infra/Ponto/PedidosDeAjusteRepositorio.cs:79-97`).
- **RH registra:** INSERT com `STATUS_FERIAS = APROVADA`, sem gestor e sem RH.
- **Pedir adesão:** INSERT (`PENDENTE`, `BENEFICIO_ATIVO = FALSE`, `DATA_PEDIDO`, `DATA_INICIO_DESEJADA`, `AUTORIZA_DESCONTO_FOLHA`) ou, se a linha já existe, UPDATE que reabre (limpa resposta e motivo) e **cancela os dependentes e trajetos antigos que não fazem parte do novo pedido** (`V001:2466-2469`); mais os dependentes e o trajeto do corpo, `PENDENTE`.
- **Aprovar adesão:** `APROVADO`, `BENEFICIO_ATIVO = TRUE`, quem e quando; os filhos `PENDENTE` viram `APROVADO` com a mesma resposta (exemplo :1785-1788).
- **Recusar adesão:** `RECUSADO` com motivo, quem e quando; filhos `PENDENTE` viram `CANCELADO` **sem** resposta (`respondidoEmUtc: null` no exemplo :1834).
- **Exceção de pagamento:** INSERT ou UPDATE pela chave `(ANO, MES)`; DELETE.

### D.5 O que a aplicação tem de garantir (o banco não garante)

1. Todas as regras entre linhas de férias: saldo, 3 partes, parte de 14 dias, sobreposição, antecedência, feriado e descanso (`V001:748-752`; `V002:904-908`).
2. A reserva (`diasEmPedido`) e a escolha do período aquisitivo.
3. Que só o gestor do setor decida a etapa dele e que o próprio pedido nunca chegue a ele.
4. Aprovar benefício só de pessoa ativa; cancelar pedidos `PENDENTE` (adesão, dependente, trajeto) ao inativar (`yaml:1708`; `V001:2454-2458`, `:2590-2592`).
5. Só benefício `OPCIONAL` pode ser pedido; dependente só com `ACEITA_DEPENDENTES`; trajeto só com `ACEITA_TRAJETO` (`V001:2912-2916`).
6. Dependente e trajeto pertencem à adesão da rota (404) e a adesão pertence à pessoa logada (404).
7. Privacidade: dependentes e trajeto só para o RH e a própria pessoa; origem do trajeto fora do log (`yaml:1492`, `:1585-1586`).
8. Guardar no log a autorização de desconto anterior quando o pedido reabre a linha (`V001:2460-2465`).
9. Que a chave "gestor ausente" valha só até a véspera da data de volta (`V001:1926-1932`; já feito em `Infra/Gestores/GestorAusente.cs:43-45`).
10. Nenhum dependente ou trajeto duplicado (não há chave única).

### D.6 Armadilhas do banco

1. **Fuso.** `FERIAS`, `BENEFICIOS_FUNCIONARIOS`, `DEPENDENTES` e `TRAJETOS_VALE_TRANSPORTE` guardam `DATETIME` em **-03:00** (regra do roadmap:73; só as tabelas "da aplicação" dos antigos `V033` a `V038` são UTC, `LEIA:80`). Usar o conversor das outras tabelas do grupo (`Infra/Persistencia/ConversoresDeTempo.cs`). As seções dos antigos `V024` e `V025` não têm a linha "FUSO" que outras têm: tratei como -03:00 pela regra geral, **a confirmar com uma sonda**.
2. **`CURDATE()` do trigger de inativação** (`V002:138`) usa o relógio do banco, não o `IRelogio`: teste com relógio fixo e férias "futuras" pode cancelar ou não conforme o dia real em que roda.
3. **`DEFAULT CURRENT_TIMESTAMP`** em `DATA_PEDIDO` de dependente e trajeto: gravar pela aplicação (com o relógio dela) é mais simples de testar do que mapear como gerada; sem um dos dois o EF manda `0001-01-01` (`LEIA:178`).
4. **`BENEFICIO_ATIVO` com padrão `TRUE`**: a entidade já existe e a fatia 1 a grava sempre; conferir que o mapeamento não declara o padrão (`LEIA:176`).
5. **`STATUS_FERIAS` com padrão `'SOLICITADA'`**: se declarado no EF, o registro do RH (`APROVADA`) funciona, mas é a mesma classe de armadilha; não declarar.
6. **`OBSERVACAO` recebe texto do trigger** sem acento ("Cancelada automaticamente: funcionario inativado"), que apareceria para a pessoa em `observacao`. **LACUNA:** mostrar como está ou tirar o sufixo. Sugestão: tirar o sufixo na leitura.
7. **O `ExecuteUpdate` não embrulha o erro do MySQL** em `DbUpdateException` (`Infra/Ponto/PedidosDeAjusteRepositorio.cs:102-105`): capturar os dois.
8. **`MOTIVO_RECUSA` é `TEXT`** e o contrato limita a 500: o limite é só da API.
9. **`GESTORES` não tem coluna de ativo**: vale o `ATIVO` da pessoa (`yaml:233`); `GestorIdDaPessoaAsync` do ponto não confere se a pessoa do RH está ativa (irrelevante: inativo não loga).
10. **`ANO YEAR`** aceita 1901 a 2155 e devolve tipo próprio (D.1).

### D.7 O que depende de tarefa agendada

| Tarefa | O quê | Origem | Observações |
|---|---|---|---|
| Entrar em gozo | `APROVADA` com `DATA_INICIO <= hoje <= DATA_FIM` vira `EM_GOZO` | `yaml:2745`; roadmap:268 | o ponto **não depende** dela: lê `APROVADA`, `EM_GOZO` e `CONCLUIDA` (`Infra/Ponto/FeriasDoPonto.cs:15`) |
| Concluir | `APROVADA` ou `EM_GOZO` com `DATA_FIM < hoje` vira `CONCLUIDA` | idem | o trigger aceita `APROVADA` direto para `CONCLUIDA` (bom para recuperar dias parados) |
| Cancelar pedido vencido | `SOLICITADA` "cuja data de início já passou" vira `CANCELADA` | roadmap:268; `yaml:2762` | **LACUNA:** "já passou" é início menor que hoje ou menor ou igual; e o que fazer com `SUGESTAO_DE_DATA` cujo início **original** venceu (E.4.5) |
| Expirar sugestão | `SUGERIU_DATA` sem resposta depois de `expiraEm` vira `CANCELADA` + `EXPIROU` | `yaml:475`, `:2972` | prazo **provisório** |

Não precisam de tarefa: a volta do gestor ausente (calculada na leitura, `V001:1929-1932`), o próximo pagamento, os painéis. Uma tarefa diária só, com recuperação, basta para as quatro linhas (molde: `Infra/Treinamentos/IncluirAdmitidosNoTreinamentoTarefa.cs`). Nenhuma manda e-mail (a lista fechada só tem decisões de gestor e de RH, `CONV:114`).

---

## E. Decisões, contradições, lacunas e riscos

### E.1 Decisões do dono já registradas (não reabrir)

1. Duas etapas, sem status novo e sem mexer em trigger (`V001:1868-1883`; `CONV:70-86`).
2. O gestor que recusa encerra o pedido na hora (2026-10-29; `yaml:698-699`).
3. O RH pode recusar o que o gestor aprovou (2026-10-30; `yaml:228-229`).
4. O RH pode confirmar o **próprio** pedido quando a etapa do gestor foi de outra pessoa (2026-10-29; `yaml:1099-1100`; `LEIA:167`).
5. Recusar a sugestão de data cancela o pedido (2026-10-29; `yaml:471`); sem resposta, expira.
6. O registro de férias pelo RH é ação direta, sem as duas etapas (2026-10-29; `yaml:1371-1372`).
7. E-mails: lista fechada (2026-10-30; `CONV:114`). Em férias: um por decisão (gestor aprova, recusa, sugere; RH confirma, recusa). **Não** há e-mail ao criar, ao aceitar ou recusar a sugestão, na aprovação automática, no registro do RH, em benefício, nem lembrete.
8. Catálogo de benefícios com `opcional`, `aceitaDependentes`, `aceitaTrajeto` (2026-10-30; `yaml:1510-1512`).
9. Dependente: nome, parentesco e data de nascimento; trajeto: origem, destino e linhas (2026-10-29; `yaml:3205`, `:3233`). Dependente ligado à adesão (`V001:2583-2594`).
10. Pagamento: último dia quando o mês não tem o dia; antecipa em fim de semana e feriado; exceções guardadas por ano e mês (2026-10-29; `yaml:2053-2056`).
11. Painel do RH sem "Folga ou compensação" (2026-10-30; `yaml:2391`), sem "Fim do período de experiência" e sem "Treinamento presencial" (:2384-2385).
12. Painel do gestor sem "Lembrar por e-mail" (:2282-2284) e sem "Baixar espelho do setor" (:2298).
13. Os cancelamentos automáticos da inativação não têm registro próprio (2026-10-28; roadmap:74; `f1:520-522`).
14. Readmissão não restaura benefícios nem férias (`f1:648-649`).
15. Decisões de engenharia já tomadas no contrato: o catálogo não marca quais benefícios têm desconto em folha (`yaml:1587-1589`); a competência é sempre o mês anterior (:3458).

### E.2 Abertas no contrato (`x-provisorio`)

| Item | Onde | Hipótese que se constrói |
|---|---|---|
| Regras de férias (partes, abono, 13º, 30 dias, 2 dias antes de feriado) | `PROV:35-38`, `:56`, `:58`; `yaml:291` | as oito de `FeriasRegraCodigo` |
| `diasAbono` (o Figma só tem a caixa) | `PROV:57`; `yaml:2829` | campo numérico de 1 a 10 |
| Prazo da sugestão (`expiraEm`) | `PROV:59`; `yaml:2972` | 5 dias corridos ou 2 dias antes do início sugerido |
| `parentesco` | `PROV:60` | texto livre |
| `BENEFICIOS` como pendência do RH | `PROV:39-40`, `:61` | conta |
| Ciclo da avaliação | `PROV:62`; `yaml:3686` | rótulo tirado do título do formulário |

Escolhas de engenharia "a avisar o dono": o efeito de ligar "gestor ausente" sobre pedidos já parados (`CONV:80`) e a passagem ao RH quando o setor fica sem gestor (`yaml:248-250`).

### E.3 Contradições

1. **Quem é o gestor do pedido.** Roadmap:268 fala em `FUNCIONARIOS.ID_GESTOR`; o contrato fala no gestor **do setor** (`yaml:233-238`, `:527`) e o código usa `SETORES.ID_GESTOR` (`Infra/Gestores/GestorAusente.cs:35-37`). Vale o contrato.
2. **E-mail de benefício.** `V001:2416-2417` e o roadmap:266 ("E-mail com o resultado") × contrato: sem e-mail de benefício (`yaml:1505-1506`; `CONV:114`). Vale o contrato.
3. **"O gestor aprovando o próprio pedido: negado"** (roadmap:274): pelo contrato é **404**, não 422 (`yaml:645-649`; `CONV:85`).
4. **Texto dos 409 de pedido** difere entre a fatia 3 (código) e a fatia 7 (contrato): B.1.
5. **Linha citada do trigger** (`yaml:1092`) está defasada: hoje `V002:968-981`. E `CONV:84` ainda diz "script a numerar pelo dono" para o motivo, que já existe (`V001:781`).
6. **Benefício dado direto pelo RH.** Os exemplos do contrato mostram adesão direta com `respondidoPor` e `respondidoEmUtc` preenchidos e `pedidoEmUtc` nulo (`yaml:1867-1872`); o código da fatia 1 não grava quem nem quando (`Dom/Beneficios/BeneficioDoFuncionario.cs:35-42`). O schema aceita `null`, então não quebra a conformidade; decidir se a fatia 7 passa a gravar (mexe em código da fatia 1).
7. **`quantidadeDependentes` da ficha do RH** (`f1:2724`: "vêm da fatia 7; antes dela, 0") está fixo em 0 (`App/Colaboradores/FuncionarioDtos.cs:40-41`): a fatia 7 tem de preencher, numa rota que é da fatia 1.
8. **Figma × contrato:** tela 51 sem "Recusar" (`yaml:702`), tela 06 com caixa de e-mail que fica escondida (:1373-1374), tela 46 com "14/03" contra 13/03 (:3772), tela 00 com quatro tipos de pendência contra cinco. Vale o contrato.
9. **Mensagens das regras.** O mesmo código tem dois textos nos exemplos: "...ou do fim de semana." (:385) e "...ou do descanso semanal." (:1476); "Não coincide com outras férias suas." (:386) e "...da pessoa." (:1477). São textos por rota (da pessoa × do RH).
10. **`STATUS:32-34`** diz "ponto: 3" como pendente nos contadores; o ponto já registrou as fontes (`Infra/DependencyInjection.cs:250-252`). Corrigir a observação ao fechar a fatia.

### E.4 Riscos e lacunas, por assunto

**1. Concorrência das duas etapas.** Toda decisão tem de ser UPDATE condicional ao estado esperado, como no ponto (`App/Ponto/IPedidosDeAjusteRepositorio.cs:45-60`): gestor aprova × gestor sugere (duplo clique); gestor decide × "gestor ausente" ligado na mesma hora; RH confirma × RH recusa; pessoa aceita × tarefa expira; RH decide × inativação (o trigger cancela). Quando o UPDATE não aplica, reler e responder o 409 certo (`App/Ponto/CasosDeUsoDosPedidosDaEquipe.cs:95-101`). **O 409 de `SUGESTAO_DE_DATA` é `PEDIDO_ETAPA_INCORRETA`** para o gestor e para o RH (`yaml:647-648`, `:1085-1086`).

**2. Saldo em corrida.** Dois `POST /api/eu/ferias` simultâneos (ou pedido × registro do RH × aceitar sugestão) passam os dois por `CABE_NO_SALDO` e reservam mais de 30 dias. O banco não protege. Travar a pessoa (`FOR UPDATE` em `FUNCIONARIOS`, `Infra/Colaboradores/OperacoesComTrava.cs:34`) antes de conferir, na mesma transação da gravação.

**3. Período aquisitivo.**
- **LACUNA:** não há `FeriasRegraCodigo` para "início depois do fim do período aquisitivo" (`yaml:283-284`, `:1377`), que o banco exige. No pedido da pessoa o início é sempre futuro, então todo período fechado serve; no **registro do RH no passado** e na **sugestão do gestor** pode falhar. Sugestão: só são candidatos os períodos que terminam antes do início; se nenhum, `CABE_NO_SALDO` não atendida (registro) e `DURACAO_DA_PARTE` não serve para a sugestão: a sugestão antes do fim do período do pedido precisa de um código e o contrato não tem. Perguntar (H.1.3).
- **LACUNA:** quando nenhum período tem saldo suficiente sozinho, `diasQueSobram` e "faltam N dias" se referem a qual período? Sugestão: ao período fechado mais antigo com saldo (o `periodoDoSaldo`).
- **LACUNA:** o pedido não se divide entre dois períodos (10 dias num e 30 no outro, pedido de 20: entra inteiro no segundo). É o que o contrato diz (:274-275); registrar.
- **LACUNA:** o prazo para tirar (`prazoParaTirar`) vencido não zera o saldo; faltas não reduzem os 30 dias; estagiário tem a mesma regra. O contrato não diz nada: 30 dias sempre e saldo que não expira.
- **LACUNA:** `FERIAS_SEM_PERIODO_AQUISITIVO` × `CABE_NO_SALDO`: quem tem períodos fechados e todos sem saldo recebe qual? Sugestão: o primeiro só para quem não tem **nenhum** período fechado.
- Admissão em 29/02 e a virada do ano: `inicio` = admissão + n anos, `fim` = início seguinte menos um dia (exemplos :58-60, :95-114).

**4. Regras das partes.**
- `MAXIMO_DE_3_PARTES` e `PARTE_DE_14_DIAS` contam quais pedidos? O contrato não lista. Sugestão coerente com a reserva: `APROVADA`, `EM_GOZO`, `CONCLUIDA`, `COM_O_GESTOR`, `COM_O_RH` do mesmo período (não `SUGESTAO_DE_DATA`, `REJEITADA`, `CANCELADA`). **LACUNA.**
- `PARTE_DE_14_DIAS`: "depois deste pedido ainda precisa ser possível ter uma parte de pelo menos 14 dias" (:279-280). O `ELENCO` dá o caso: Bruno, com 12 tirados e 18 de saldo, só pode pedir uma parte de 14 a 18. O abono conta no saldo mas não é parte. A fórmula exata (partes restantes × dias restantes) **não está escrita**: a implementação precisa de testes de tabela e da aprovação do dono.
- **LACUNA:** o pedido pode deixar um resto de 1 a 4 dias que nunca poderá ser tirado (parte mínima de 5). O exemplo aceita: 18 de saldo, pede 15, "Sobram 3 dias" com `podeEnviar: true` (:375-377).
- `NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO`: **LACUNA** sobre o que é "descanso semanal". Se é o domingo, os dias proibidos são sexta e sábado; se é o fim de semana (o texto do exemplo :385), quinta e sexta. E o próprio feriado, o sábado e o domingo como dia de início? Sugestão: proibido começar em feriado, sábado ou domingo e nos 2 dias corridos antes de feriado ou de sábado. Os feriados são os do calendário do ponto (`App/Ponto/ICalendarioDoPonto.cs:18-24`). Perguntar (H.1.2).
- `ANTECEDENCIA_DE_30_DIAS`: "pelo menos 30 dias depois de hoje" (:283): início maior ou igual a hoje + 30. O exemplo diz "Começa daqui a mais de 30 dias." para um início 40 dias à frente; a borda não tem exemplo.
- `SEM_SOBREPOSICAO`: "em andamento, aprovado ou em gozo" (:286). **LACUNA:** `SUGESTAO_DE_DATA` conta como "em andamento" (com as datas originais ou as sugeridas)? O exemplo do aceite diz que o 346 "não coincide com o 345" (:406), o que sugere conferir no aceite contra os demais. Sugestão: o pedido em `SUGESTAO_DE_DATA` não bloqueia outros pedidos; ao aceitar confere-se de novo.
- `adiantar13Salario` é só um campo guardado: nenhum efeito, nenhuma regra (não há folha no sistema).

**5. Sugestão de data.**
- O contrato manda validar a sugestão só pelas "regras do banco" (:758-759). Se o gestor sugerir datas que ferem as outras regras (feriado, saldo, sobreposição), a pessoa recebe 422 ao aceitar, para sempre, e o pedido só sai pela recusa ou pela expiração. **LACUNA** de produto. Sugestão: conferir na sugestão todas as regras menos a antecedência e a reserva do próprio pedido (H.1.4).
- **LACUNA:** sugestão com início no passado ou antes de hoje: nada proíbe. Sugestão: 400.
- **LACUNA:** a sugestão muda o número de dias? O banco deixa. O abono fica o do pedido.
- **Tarefas em conflito:** um pedido em `SUGESTAO_DE_DATA` cujo início **original** já passou seria cancelado pela tarefa de "pedido vencido" antes de a sugestão expirar, ficando `CANCELADA` com `respostaDaPessoa` nula. Sugestão: para `SUGERIU_DATA` sem resposta vale só a expiração da sugestão.
- "Gestor ausente" **não** toca em `SUGESTAO_DE_DATA` (:245); a implementação da porta sai certa se filtrar `RESULTADO_GESTOR IS NULL`, como o ponto (`Infra/Ponto/PedidosParadosNoGestorDoPonto.cs:24`).
- Gestor que sugeriu e saiu do setor: o pedido fica em `SUGESTAO_DE_DATA` com o `ID_GESTOR` dele (o `CHECK` exige); se a pessoa aceita, vai ao RH com o gestor antigo como responsável. Coerente; testar.

**6. Gestor aprovando o próprio pedido e quem é a equipe.**
- O pedido de quem é gestor nasce sem `ID_GESTOR` (`Infra/Gestores/GestorAusente.cs:28-33`), então nunca aparece na fila de nenhum gestor: 404 natural.
- **Pessoa que troca de setor com pedido parado no gestor antigo:** o pedido fica com o `ID_GESTOR` antigo, some da equipe do gestor antigo (a consulta filtra pela equipe) e não entra na do novo; o RH não pode decidir (etapa `GESTOR`). Fica parado até a tarefa cancelar. É a mesma dívida do ponto, já anotada (`STATUS:213`). **LACUNA:** o contrato só cobre "o gestor deixa o setor", não "a pessoa deixa o setor". Sugestão: ao trocar o setor de uma pessoa, passar ao RH os pedidos dela que esperam o gestor (H.1.6).
- A fatia 7 tem de **registrar a sua `IPedidosParadosNoGestorDaFatia`** (`App/Colaboradores/IPedidosParadosNoGestor.cs:26-31`); sem ela trocar, tirar, desligar ou marcar ausente um gestor não move nenhum pedido de férias (`STATUS:48`, `:50`, `:63`, `:66`, `:213`).

**7. Quem do RH decide.** As regras de `PEDIDO_PROPRIO` e `PEDIDO_RH_FORA_DE_GESTORES` já estão escritas e testadas para o ponto (`App/Ponto/Regras/RegrasDeDecisaoDoPedido.cs:33-68`; `App/Ponto/CasosDeUsoDosPedidosParaRh.cs:140-157`): a ordem é etapa (409) antes de quem decide (422)? No ponto, **a etapa vem primeiro** (`:72-73`). O contrato de férias não dá ordem entre o 409 e o 422 do RH: copiar a do ponto. Com uma só pessoa do RH em `GESTORES`, ninguém decide o pedido dela (`yaml:1098-1099`): a demo tem duas (Ana e Sofia, `ELENCO`).

**8. Inativar cancela férias futuras.** É o trigger, sem aviso e sem registro (E.1.13). Consequências para a fatia 7: (a) férias `EM_GOZO` ou que começam hoje **não** são canceladas e a tarefa as conclui normalmente; (b) pedidos `SOLICITADA` com início hoje ou no passado ficam `SOLICITADA` de um inativo até a tarefa de vencidos; (c) o saldo e a fila do RH têm de ignorar ou tolerar pedidos de inativos; (d) os pedidos de **benefício** pendentes o trigger não toca: a aplicação cancela (D.5.4), e para isso é preciso um ponto de extensão na efetivação do desligamento (`Infra/Colaboradores/DesligamentoDeFuncionarios.cs:22-24`), que hoje só tem `ICondicaoParaEfetivarDesligamento` e `IPedidosParadosNoGestor`. **LACUNA:** esse cancelamento da aplicação gera evento de auditoria? A decisão do dono fala dos cancelamentos automáticos da inativação em geral. Sugestão: sem evento próprio.

**9. Férias e o ponto.** Registrar férias **no passado** (permitido, `yaml:1377`) muda dias que a consolidação do banco de horas já lançou como falta. O ponto tem a porta `IRecalculoDoBancoDeHoras.RecalcularDiaAsync` (usada em `App/Ponto/CasosDeUsoDosPedidosDaEquipe.cs:106`). **LACUNA:** o contrato não diz que o registro retroativo recalcula o banco de horas. Sugestão: recalcular os dias passados do período no registro e, por simetria, quando o trigger ou a tarefa cancela férias que já tinham dias passados (não acontece: o trigger só cancela futuras).

**10. Benefícios.**
- **Adesão aprovada e inativa** (o RH tirou o benefício, ou a pessoa foi readmitida "sem benefícios"): a pessoa "ainda não tem" o benefício, então ele deveria aparecer em `disponiveis`; mas `ultimoPedido` só aceita `RECUSADO` e `CANCELADO` e o texto do reabrir só fala desses dois (`yaml:1590`). **LACUNA.** Sugestão: aparece em `disponiveis` com `ultimoPedido: null` e o pedido reabre a linha.
- **Reabrir:** os dependentes e trajetos antigos que não vêm no novo pedido viram `CANCELADO` (`V001:2466-2469`), inclusive os que estavam `APROVADO`. O contrato não repete isso. Seguir o script.
- **`dependentesCadastrados`:** "sem repetir" por qual chave? Sugestão: nome (sem diferenciar maiúsculas), parentesco e nascimento. Inclui aprovados de adesão inativa? Sugestão: sim. **LACUNA.**
- **Dependente repetido** (o mesmo, pendente ou aprovado, na mesma adesão): nada no contrato. Sugestão: aceitar (o RH recusa) ou 409; perguntar só se incomodar.
- **Segundo trajeto pendente:** a situação `CANCELADO` cita "pedido substituído" (`yaml:3190`). Sugestão: o pedido novo cancela o trajeto `PENDENTE` anterior da mesma adesão. **LACUNA.**
- **Trajeto obrigatório** quando o benefício tem `aceitaTrajeto`? O schema aceita `null` (:3360-3364). Sugestão: opcional.
- **Aprovar junto:** "os dependentes e o trajeto que vieram no mesmo pedido" (:1761). Não há coluna que ligue o filho ao pedido: na prática são todos os `PENDENTE` da adesão.
- **Lista do RH:** "pedido mais antigo primeiro" (:1707) para uma adesão já aprovada com filho pendente: ordenar pela menor data pendente da linha. **LACUNA.** Com `situacao=APROVADO` vêm também as dadas direto (sem pedido)? Sugestão: sim (é a situação da adesão).
- **`respostaPrevistaAte`** é obrigatória em `BeneficioAdesaoPedidoResumo`, e só há linha de pedido quando há data de pedido: adesão dada direto **não** entra em `pedidos` (o exemplo confirma: :1566-1568 não lista as adesões diretas).
- **`titulo`:** "Adesão ao plano odontológico" exige pôr o nome em minúscula e escolher "ao/no/do" (:3320). Um benefício de nome feminino sai errado. Sugestão: minúscula só na primeira letra e as três preposições fixas do contrato.
- **Privacidade:** nenhuma rota de gestor devolve dependente ou trajeto; conferir com teste que `GET /api/gestor/equipe/{id}` não ganhou campo. Log: origem como campo sensível (`Dom/Auditoria/AuditoriaAtributos.cs:18-28`); **LACUNA** sobre o nascimento do dependente (menor de idade; roadmap:82 cita "de menores (dependentes)" entre os dados pessoais, mas a lista do que o log não grava não o inclui). Sugestão: nome e parentesco no log, nascimento como sensível.
- **O RH decide o próprio pedido de benefício:** permitido (`LEIA:167`).
- **Editar a ficha (fatia 1) com pedido pendente:** se o RH inclui na lista de `beneficioIds` um benefício com linha `PENDENTE`, o código "reconcede" (aprova e ativa) sem gravar resposta (`Dom/Beneficios/BeneficioDoFuncionario.cs:44-52`); os filhos pendentes continuam pendentes numa adesão ativa. O contrato da fatia 1 diz que pedidos pendentes "não são tocados" (`f1:343`), o que vale para quem **não** está na lista. Testar os dois casos.

**11. Pagamento.**
- A regra e a porta já existem e estão testadas (`App/Comunicacao/Regras/RegraDoPagamento.cs:31-95`; `tests/Comunicacao/ProximoPagamentoTests.cs`). Falta o CRUD das exceções, o calendário (12 chamadas a `DoMes`) e as rotas.
- **Fevereiro:** dia 29, 30 ou 31 cai no último dia (`ULTIMO_DIA_DO_MES`), e se esse dia for sábado, domingo ou feriado, antecipa: dois ajustes (:2088-2089). `DoMes` já devolve os dois (`:54-66`), mas marca **só um** dos dois "antecipado" (fim de semana **ou** feriado, `:62-69`), mesmo que a antecipação passe por um feriado e por um fim de semana. O contrato diz "podem vir dois" sem dizer quais: aceitar o que existe.
- **A regra antecipada pode cair no mês anterior** (dia 1 num domingo); o `ano` e o `mes` continuam os da regra (`:19-20`). O calendário de um ano pode então trazer uma data do ano anterior. Coerente com o schema; testar.
- **Holerite já agendado "no dia do pagamento"** tem o instante calculado **na hora do envio** (`App/Comunicacao/Holerites/CasosDeUsoDeEnvioDeHolerite.cs:24-33`). Criar, trocar ou remover uma exceção depois **não move** o holerite agendado. **LACUNA** (o contrato diz só "é a mesma conta", `yaml:2086-2087`). Sugestão: não mover e deixar escrito (H.1.8).
- **DELETE com `ano` ou `mes` fora da faixa:** a rota não lista 400. Sugestão: 404.
- **Auditoria:** não existe área de pagamento. Sugestão: `CADASTROS` (a descrição da área já diz "empresa", `f1:3325`).

**12. Painéis.**
- **"Presença agora":** quem conta são as pessoas "ativas e já admitidas" (`yaml:3587`); em dia sem expediente `total` vale só `trabalhando` (:3596-3597). A regra por pessoa já existe (`App/Colaboradores/SituacaoDeHoje.cs:50-73`) e `PresencaContagens.Total` soma as **seis** (`:21`): o `total` do painel é outra soma (cinco), não reutilizar a propriedade.
- **`pendencias`:** a frase `resumo` de cada tipo só tem exemplos (:2412-2427). **LACUNA:** o texto e a regra de cada frase ("O mais antigo é de...", "Um deles se sobrepõe...", "Enviados hoje por..."). Sugestão: uma frase simples por tipo, sempre sobre o item mais antigo; `resumo` aceita `null`.
- **`desdeEmUtc`:** "desde quando espera o RH". Para férias o exemplo usa a hora em que o gestor aprovou (:2490 = :1020). Para pedido sem etapa do gestor: a solicitação; para sugestão aceita: a resposta da pessoa; para pedido que **passou ao RH porque o gestor saiu**: não há coluna com essa hora. **LACUNA.** Sugestão: a solicitação.
- **Fila unida de cinco origens com paginação:** não dá para paginar no banco de forma simples. Os volumes são pequenos (dezenas): montar em memória e paginar. Registrar o limite.
- **Admissão:** "começa em até 7 dias e ainda não respondeu o formulário de admissão" (:2378). **LACUNA:** quem não tem formulário de admissão atribuído conta? (O nome do tipo fala em "documentos pendentes", a regra fala em formulário.) Sugestão: só quem tem o formulário atribuído e não respondeu. Já admitidos que não responderam não contam (a regra fala em "em admissão").
- **`treinamentosEmAndamento`:** "em ordem de liberação" (:2388): a data de início quando há, senão a de publicação. `total` = "pessoas do público" (:3581): o público de hoje (fatia 6) ou as linhas existentes? **LACUNA.** Sugestão: o mesmo número que a lista de treinamentos do RH já mostra.
- **`ultimasAlteracoes`:** as 4 primeiras de `apenasAlteracoes=true` (:2395-2397); o filtro existe (`Infra/Auditoria/ConsultaDeAuditoria.cs:35`). `autor` é texto pronto "Ana Souza (RH)", "Carlos Ribeiro (gestor)", "Sistema" (:3614): **não encontrei** esse formato pronto no código (busca por "(RH)" em `App/Auditoria` e `Infra/Auditoria` sem resultado); é novo. A consulta de auditoria exige um intervalo de datas (`App/Auditoria/IConsultaDeAuditoria.cs:14`): escolher uma janela larga.
- **Painel do gestor:** `aguardandoAprovacao.ferias` são "os mesmos itens" de #6, que é paginada; o painel não tem paginação. Trazer todos.
- **`bancoDeHorasMinutos`** "consolidado até ontem" (:3713): `ISaldoDoBancoDeHoras.ObterSaldosAsync` (`App/Ponto/IPontoRepositorio.cs:19-20`).
- **`avaliacaoSituacao`** e **`avaliacao`:** dependem do que a fatia 5 expõe sobre a avaliação `GESTOR_AVALIA_EQUIPE` aberta (`App/Formularios/Responder/RegrasDosMeusFormularios.cs:18`). **LACUNA:** o que é `NAO_SE_APLICA` (o exemplo marca o estagiário e quem está de férias, :2358, :2360) e de onde sai o rótulo "2026.2". Sugestão: `NAO_SE_APLICA` = a pessoa não é par do gestor naquele formulário; `ciclo` = o que vier depois de "ciclo " no título, ou o título inteiro. Com dois formulários abertos: o de prazo mais próximo.
- **`advertencias`** = não canceladas do ano corrente (:3722), a mesma conta de `Infra/Comunicacao/FonteDoIndicadorDeAdvertencias.cs`.
- **N+1:** o painel do gestor e o indicador de férias chamam o cálculo de saldo por pessoa. O cálculo precisa de uma forma em lote (várias pessoas, uma consulta).

**13. Readmissão.** A readmissão troca a data de admissão (`f1:644-647`); os períodos aquisitivos passam a contar da nova data e as férias antigas ficam com períodos do vínculo anterior. O saldo sai certo se o cálculo casar férias e períodos pelas datas exatas (as antigas não casam com nenhum período novo), mas elas continuam no histórico. **LACUNA:** mostrar ou esconder as férias do vínculo anterior. Sugestão: mostrar.

**14. Pessoa sem e-mail corporativo.** O ponto simplesmente não enfileira (`App/Ponto/CasosDeUsoDosPedidosDaEquipe.cs:166-170`); copiar.

**15. Volume e ordem.** Listas "curtas, sem paginação" (histórico de férias, benefícios): sem limite no contrato. Ordenações sempre com desempate por id.

---

## F. Infraestrutura: o que reutilizar e o que é novo

### F.1 Reutilizar

| Peça | Onde | Como entra na 7 |
|---|---|---|
| `IGestorAusente.EtapaInicialDoPedidoAsync` e `EstaAusenteHojeAsync` | `App/Gestores/IGestorAusente.cs:20-36`; `Infra/Gestores/GestorAusente.cs:21-54` | etapa de nascimento do pedido e `gestorQueAnalisa`; foi escrita para as fatias 3 e 7 |
| `IPedidosParadosNoGestorDaFatia` + composto | `App/Colaboradores/IPedidosParadosNoGestor.cs:9-31`; `PedidosParadosNoGestorComposto.cs`; `PedidosAprovados.cs:7-14`; registro em `Infra/DependencyInjection.cs:231`, `:263` | a 7 registra a de férias; molde completo em `Infra/Ponto/PedidosParadosNoGestorDoPonto.cs:16-45` |
| Etapa deduzida das colunas, `podeDecidir`, os cinco textos, papéis | `App/Ponto/Regras/EtapaDoPedidoDeAjuste.cs`; `RegrasDeDecisaoDoPedido.cs:5-68` | copiar ou generalizar (os tipos são do ponto: decidir se sobem para `App/Comum`) |
| Decisão como UPDATE condicional, releitura, evento e e-mail na mesma transação | `App/Ponto/CasosDeUsoDosPedidosDaEquipe.cs:62-120`; `CasosDeUsoDosPedidosParaRh.cs:51-157`; `Infra/Ponto/PedidosDeAjusteRepositorio.cs:55-106` | molde dos casos de uso de férias |
| Trava da pessoa | `Infra/Colaboradores/OperacoesComTrava.cs:21-47`; `App/Ponto/IPontoRepositorio.cs:30` | pedir, registrar e aceitar sugestão |
| `IEscopoDoUsuario`, `EquipeDoGestor`, `DaEquipe` | `App/Escopo/IEscopoDoUsuario.cs:14-41`; `Infra/Persistencia/EscopoDeEquipeExtensions.cs` | todas as rotas de gestor e de `/api/eu` |
| `IConsultaDaEquipe` (lista, contagens de hoje, ficha) | `App/Colaboradores/IConsultaDaEquipe.cs:9-16` | `totalPessoas` e a base de `pessoas` do painel do gestor |
| `ISituacaoDeHoje`, `RegraDeSituacaoDeHoje`, `PresencaContagens`, `IFonteDeFatosDoDia` | `App/Colaboradores/SituacaoDeHoje.cs:6-94`; fonte do ponto registrada em `Infra/DependencyInjection.cs:326-327` | presença agora, lista de sem registro, `pontoHoje` |
| `IFeriasDoPonto` | `App/Ponto/ICalendarioDoPonto.cs:26-33`; `Infra/Ponto/FeriasDoPonto.cs:10-18` | `feriasAte` do painel; hoje lê `FERIAS` por SQL cru: pode passar a usar a entidade, **sem mudar o conjunto de status** |
| `ICalendarioDoPonto` (dia útil, feriados que valem) | `App/Ponto/ICalendarioDoPonto.cs:18-24` | regra de feriado, 5 dias úteis, próximos dias do RH, ajustes do pagamento |
| `IConfiguracaoDaEmpresa` | `App/Empresa/IConfiguracaoDaEmpresa.cs:4-18` | dia de pagamento |
| `IProximoPagamento`, `RegraDoPagamento` (`DoMes`, `Proximo`), `PagamentoDoMes` | `App/Comunicacao/IProximoPagamento.cs:9-12`; `App/Comunicacao/Regras/RegraDoPagamento.cs:4-96`; `Infra/Comunicacao/ProximoPagamento.cs:14-36` | #29, #31, #34; a leitura das exceções por SQL cru (`:31`) passa a usar a entidade |
| `IFonteDoProximoPagamento` | `App/Colaboradores/IFonteDeIndicadorDaFicha.cs:23-27`; leitura em `App/Colaboradores/FuncionariosCasosDeUsoDeLeitura.cs:69-94` | a 7 registra a fonte (um adaptador sobre `IProximoPagamento`) |
| `IFonteDeIndicadorDaFicha` + `IndicadorDaFicha.FeriasDisponiveisDias` | `App/Colaboradores/IFonteDeIndicadorDaFicha.cs:4-21`; `FontesDeIndicador.cs:6-17`; registros vizinhos em `Infra/DependencyInjection.cs:260`, `:288`, `:319` | a 7 registra a fonte de férias (RH e gestor leem a mesma) |
| Contadores do menu | `App/Painel/ContadorDoMenu.cs:7-15`; `IFonteDeContadorDoMenu.cs:10-16`; `ContadoresDoMenuCasoDeUso.cs:12-48`; `Api/Painel/ContadoresDoMenuEndpoints.cs:22-40` | só falta a fonte de férias de `PendenciasDaEquipe`; molde em `Infra/Ponto/FontesDePedidosDeAjusteNoMenu.cs:13-29`; várias fontes somam |
| Fila do RH e do gestor de ajustes de ponto | `App/Ponto/IPedidosDeAjusteRepositorio.cs:11-30` (`IConsultaDePedidosDeAjuste`); `Infra/Ponto/FontesDePedidosDeAjusteNoMenu.cs:32-38` | `ajustesDePonto` do painel do gestor e a pendência `AJUSTE_DE_PONTO` |
| Atestados, aniversariantes, auditoria, treinamentos, advertências, saldo do banco de horas | `App/Ponto/Atestados/`; `App/Comunicacao/Aniversarios/AniversariantesCasoDeUso.cs:17-20`; `App/Auditoria/IConsultaDeAuditoria.cs:30-32`; `App/Treinamentos/Construtor/CasosDeUsoDaListaDoRh.cs`; `Infra/Comunicacao/FonteDoIndicadorDeAdvertencias.cs`; `App/Ponto/IPontoRepositorio.cs:14-20` | as partes dos painéis que vêm das fatias 3 a 6 (as assinaturas exatas de atestados e treinamentos **não foram lidas**: conferir ao planejar a 7d) |
| Fila de e-mails e renderizadores | `App/Emails/IFilaDeEmails.cs:7-34`; `App/Emails/IRenderizadorDeEmail.cs:7-18`; molde `Infra/Emails/ResultadoAjustePontoRenderizador.cs`; enfileirar em `App/Ponto/CasosDeUsoDosPedidosDaEquipe.cs:160-185`; registro em `Infra/DependencyInjection.cs:242` | um renderizador novo (`FERIAS_DECIDIDAS`), chave por pedido e por decisão |
| Agendador | `App/Agendamento/IAgendador.cs:7-34`; `ITarefaAgendada.cs:12-21`; registros em `Infra/DependencyInjection.cs:143-151`, `:286` | a tarefa diária de férias; molde `Infra/Treinamentos/IncluirAdmitidosNoTreinamentoTarefa.cs` |
| Auditoria | `App/Auditoria/IAuditor.cs:28-49`; `Dom/Auditoria/AuditoriaAtributos.cs:9-38` (`Sensivel`); áreas `Ferias` e `Beneficios` | eventos explícitos nas decisões (o `ExecuteUpdate` não passa pelo interceptor, roadmap:74) |
| Erros | `App/Comum/ProblemaDeApiException.cs`; `App/Comum/Excecoes.cs`; catálogo e teste no molde de `App/Treinamentos/Mensagens/` e `tests/Contrato/MensagensDosTreinamentosTests.cs`; tradutor no molde de `Infra/Ponto/TradutorDeRecusaDoPonto.cs` | catálogo da fatia 7 com `regras[]` como extensão |
| Listas | `App/Comum/Consultas/` (`ParametrosDeLista`, `RespostaPaginada`); `Infra/Persistencia/ConsultasEf.cs`; `Api/Configuracao/EnumsDeParametrosDoContrato.cs` | #6, #12, #22, #40, #41 |
| Tempo | `App/Comum/Tempo/FusoBrasil.cs`; `Infra/Persistencia/ConversoresDeTempo.cs`; `IRelogio` | "hoje" em -03:00 em todas as regras |
| Catálogo e adesão já mapeados | `Dom/Beneficios/Beneficio.cs`; `Dom/Beneficios/BeneficioDoFuncionario.cs`; `Infra/Persistencia/AppDbContext.cs:42-44` | estender a adesão; o catálogo só se lê |
| Testes | `tests/Infra/ApiFactory.cs`, `ElencoDeTeste.cs`, `PedidosParadosEspiao.cs`; `tests/Matriz/MatrizDePerfis.cs`; `tests/Contrato/ContratoDivergenciaTests.cs:19` (`OperationIdsNoEscopo`), `ContratoEStatusTests.cs`; `tests/Persistencia/MapeamentoContraBancoTests.cs`, `ConformidadeDoModeloTests.cs`; semente de férias em `tests/Ponto/PontoDeTeste.cs:124` | o mesmo jeito das fatias 3 a 6 |

### F.2 Novo (não existe)

- **Entidades e mapeamentos:** `Ferias` (em `Dom/`, pasta nova), `Dependente`, `TrajetoDeValeTransporte`, `ExcecaoDePagamento`; as seis colunas a mais em `BeneficioDoFuncionario`; saída das quatro tabelas de `tabelas-sem-mapeamento.txt`.
- **Regras puras de férias:** períodos aquisitivos e saldo (com a forma em lote), as oito regras e as mensagens, escolha do período, situação e etapa, tradução das decisões, `expiraEm`, `pagamentoAte`, conflitos e equipe disponível, calendário por semana.
- **Regras puras de benefício:** o que entra em cada lista, o título, `respostaPrevistaAte` (5 dias úteis), "sem repetir" dos dependentes.
- **Catálogo de erros** da fatia 7 + teste contra o contrato; tradutor de recusa (as mensagens de trigger de D.2 e as chaves únicas).
- **Casos de uso e endpoints:** férias (eu, gestor, RH), benefícios (eu, RH), pagamento (eu, RH), painéis (eu, gestor, RH).
- **Renderizador** do e-mail de férias (cinco variações: gestor aprovou, gestor recusou com motivo, gestor sugeriu com as datas, RH confirmou, RH recusou com motivo).
- **Tarefa agendada** de férias (D.7).
- **`IPedidosParadosNoGestorDaFatia` de férias**, a fonte de `FeriasDisponiveisDias`, a fonte de `IFonteDoProximoPagamento`, a fonte de `PendenciasDaEquipe`.
- **Ponto de extensão na inativação** para cancelar os pedidos de benefício pendentes (não existe; E.4.8).
- **`quantidadeDependentes`** de verdade na ficha do RH (`App/Colaboradores/FuncionarioDtos.cs:40-41`).
- **Fila unida de pendências do RH** e os textos prontos (`resumo`, `descricao`, `autor`).
- **`STATUS.md`:** 38 linhas novas, a correção das três de contadores (`:32-34`) e das observações de `:43-45`, `:48`, `:50`, `:63`, `:66`, `:213`. **README:** seção da fatia 7.

---

## G. Proposta de incrementos e tarefas

O roadmap dá 7a férias, 7b benefícios e dependentes, 7c pagamento e calendário, 7d painéis (roadmap:260). Cada tarefa segue o ciclo das fatias anteriores: teste vermelho, implementar, verde, `operationId` em `OperationIdsNoEscopo`, linha no `STATUS.md`, conformidade nos 2xx e nos erros, matriz de perfis, commit. **[S]** pede revisão de segurança. Um PR e uma rodada de mutação por incremento.

### 7a. Férias (#1 a #17)

**T1. Mapeamento de `FERIAS` e sonda dos triggers.** Arquivos: `Dom/Ferias/Ferias.cs` (e enums), `Infra/Persistencia/Configuracoes/FeriasConfiguracao.cs`, `tests/Persistencia/MapeamentoDasFeriasTests.cs`. Testar: os dois testes de conformidade verdes; uma sonda que grava pelo EF um registro `APROVADA` (o padrão do banco não vence), prova as recusas de trigger de D.2 com o texto exato, as transições aceitas e recusadas, e o cancelamento pela inativação; datas em -03:00 (o `CHECK` de análise não recusa).

**T2. Regras puras: períodos e saldo.** `App/Ferias/Regras/PeriodosAquisitivos.cs`, `SaldoDeFerias.cs`. Testar (unidade, alvo de mutação): os exemplos do contrato (Bruno :56-114; Diego :853-899), admissão em 29/02, menos de um ano de casa, pessoa desligada, reserva (`SUGESTAO_DE_DATA` não reserva), `periodoDoSaldo`, `proximasFerias`, `pedidosEmAnalise`.

**T3. Regras puras: as oito regras e a conferência.** `App/Ferias/Regras/ConferenciaDeFerias.cs`, `MensagensDasRegras.cs`. Testar: uma tabela por regra com as bordas (4, 5, 30 e 31 dias; abono 0, 1, 10, 11; 29 e 30 dias de antecedência; véspera e antevéspera de feriado e de fim de semana; três e quatro partes; o caso do Bruno para a parte de 14 dias; sobreposição de um dia), `diasQueSobram` negativo, `pagamentoAte`, a lista sem antecedência no modo RH e no aceite. **Depende das respostas H.1.1 a H.1.3.**

**T4. Regras puras: situação, etapa, decisões e `podeDecidir`.** `App/Ferias/Regras/SituacaoDoPedidoDeFerias.cs`, `MapeadorDoPedidoDeFerias.cs`. Testar: cada linha da tabela D.3; os seis textos; `expiraEm`.

**T5. Erros da fatia 7.** `App/Gestao/Mensagens/` (ou por assunto), `tests/Contrato/MensagensDaGestaoTests.cs`, tradutor de recusa. Testar: textos iguais aos do contrato; `regras[]` e `regraDoBanco` saem no JSON.

**T6. Minhas férias, conferência e pedir** (#1, #3, #2). `App/Ferias/CasosDeUsoDasMinhasFerias.cs`, `Infra/Ferias/FeriasRepositorio.cs`, `ConsultaDeFerias.cs`, `Api/Ferias/FeriasEuEndpoints.cs`. Testar: nasce com gestor, sem gestor (gestor, setor sem gestor, gestor desligado) e com gestor ausente (automática, sem e-mail); nenhum e-mail ao criar; dois pedidos em paralelo não estouram o saldo; 422 com a lista; evento de auditoria. **[S]** (só o próprio).

**T7. Gestor: lista, aprovar, recusar, sugerir** (#6, #8, #9, #10). `App/Ferias/CasosDeUsoDasFeriasDaEquipe.cs`, `Api/Ferias/FeriasGestorEndpoints.cs`, `Infra/Emails/ResultadoFeriasRenderizador.cs`. Testar: 404 para pedido próprio, de outro gestor, de outro setor; os dois 409; motivo de 4 e de 501 caracteres e só de espaços; `conflitos` e `equipeDisponivel`; um e-mail por decisão com a chave certa; decisão em corrida. **[S]** (escopo do gestor).

**T8. Resposta à sugestão** (#4, #5). Testar: aceitar troca as datas e reserva; 422 quando não cabe; recusar cancela; pedido de outra pessoa 404; sem e-mail; aceitar × expirar em corrida.

**T9. RH: lista, confirmar, recusar** (#12, #13, #14). `App/Ferias/CasosDeUsoDasFeriasParaRh.cs`, `Api/Ferias/FeriasRhEndpoints.cs`. Testar: confirmar na etapa do gestor = 409; `PEDIDO_PROPRIO` e `PEDIDO_RH_FORA_DE_GESTORES` antes de tocar no banco; o RH confirma o próprio pedido que o gestor aprovou; recusa do que o gestor aprovou; `ID_GESTOR` gravado no pedido sem etapa. **[S]**

**T10. Fichas, registrar e conferir pelo RH** (#11, #15, #16, #17). Testar: a ficha do RH esconde `COM_O_GESTOR` e `SUGESTAO_DE_DATA` e conta os dois no resumo; a do gestor mostra tudo e dá 404 para outro setor e para quem é gestor; registro no passado, sem antecedência, sem e-mail; inativo = 422.

**T11. Calendário da equipe** (#7). Testar: segunda da semana, 1 e 26 semanas, itens que encostam na borda, as três situações, `SUGESTAO_DE_DATA` fora.

**T12. Pontes.** `Infra/Ferias/PedidosParadosNoGestorDasFerias.cs`, `FonteDoIndicadorDeFerias.cs`, `FonteDeFeriasParaOGestor.cs` (contador). Testar: ligar a ausência aprova os parados e devolve a contagem em `pedidosAprovadosAgora.ferias`; trocar, tirar e desligar o gestor passa ao RH; `SUGESTAO_DE_DATA` não muda; o indicador da ficha é igual a `resumo.diasDisponiveis`; a bolinha do gestor soma férias e ponto.

**T13. Tarefa agendada de férias.** `Infra/Ferias/AtualizarFeriasTarefa.cs`. Testar: as quatro ações; rodar duas vezes não muda nada; dias parados são recuperados (`APROVADA` direto para `CONCLUIDA`); eventos como Sistema; nenhum e-mail.

**Mutação do incremento 7a** (T2 a T13).

### 7b. Benefícios e dependentes (#18 a #28)

**T14. Mapeamento** das colunas novas da adesão, de `DEPENDENTES` e de `TRAJETOS_VALE_TRANSPORTE`, com a origem marcada como sensível. Testar: conformidade; pedido nasce com `BENEFICIO_ATIVO = FALSE`; recusas dos `CHECK` de B.2.

**T15. Regras puras** (listas, título, 5 dias úteis, sem repetir). Testar: os três exemplos de `respostaPrevistaAte` do contrato (:1567-1568, :1619).

**T16. Meus benefícios e os três pedidos** (#18 a #21). `App/Beneficios/Adesoes/`, `Api/Beneficios/BeneficiosEuEndpoints.cs`. Testar: os quatro 422; 409 de já ativo ou pendente; reabrir linha recusada (autorização anterior no log, filhos antigos cancelados); adesão de outra pessoa = 404; origem fora do log. **[S]** (dado de menor e endereço).

**T17. RH: lista e seis decisões** (#22 a #28). `Api/Beneficios/BeneficiosRhEndpoints.cs`. Testar: aprovar junto; recusar cancela filhos; dependente de outra adesão = 404; os dois 409 na ordem do contrato; inativo = 422; motivo só de espaços = 400; nenhum e-mail na fila. **[S]**

**T18. Pontes de benefício.** Cancelar pendentes ao inativar (ponto de extensão novo na efetivação do desligamento); `quantidadeDependentes` da ficha; gravar quem concedeu direto, se o dono quiser (H.1.7). Testar: inativar cancela adesão, dependente e trajeto pendentes e eles somem da lista do RH; os testes de desligamento continuam verdes.

**Mutação do incremento 7b.**

### 7c. Pagamento e calendário (#29 a #33)

**T19. Mapeamento de `PAGAMENTOS_EXCECOES`** e troca da leitura por SQL cru. Testar: sonda do tipo `YEAR`; `tests/Comunicacao/ProximoPagamentoTests.cs` continua verde.

**T20. Exceções e calendário** (#30 a #33). `App/Pagamento/`, `Api/Pagamento/PagamentoRhEndpoints.cs`. Testar: PUT cria e substitui; data fora do mês = 400; dois PUT em paralelo; DELETE inexistente = 404; calendário com fevereiro (dia 30 e 31), dois ajustes, exceção em fim de semana (vale como está), empresa sem configuração (só os meses com exceção).

**T21. Próximo pagamento** (#29) e a fonte do indicador. `Api/Pagamento/PagamentoEuEndpoints.cs`, `Infra/Pagamento/FonteDoProximoPagamento.cs`. Testar: hoje é o dia; competência em janeiro (dezembro do ano anterior); sem configuração = `null`; o indicador da ficha é igual a `pagamento.data`.

**Mutação do incremento 7c** (pequeno; pode juntar com o 7d).

### 7d. Painéis e contadores (#34, #36, #38 a #41; #35 e #37 já prontos)

**T22. Painel do funcionário** (#34). Testar: os três cartões iguais às rotas de origem; ordem do catálogo; `detalhe` com dependentes e com trajeto.

**T23. Painel do gestor** (#38). Testar: gestor sem setor; equipe igual à de `GET /api/gestor/equipe`; nenhum CPF, endereço nem trajeto; número de consultas constante (sem N+1). **[S]**

**T24. Painel do RH e as duas listas** (#39, #40, #41). Testar: soma das pendências; tipos com zero não vêm; admissão só em até 7 dias; próximos dias (21 dias, máximo 8, ordem); treinamentos (máximo 5); presença em dia útil antes e depois do limite e em dia sem expediente; as 4 últimas alterações sem login nem batida; paginação da fila unida; filtro por tipo.

**T25. Fechamento.** Os 38 `operationId` novos no escopo, `STATUS.md`, matriz de perfis das 41 rotas, README, mutação do 7d, revisão de segurança (escopo do gestor, privacidade de dependentes e trajeto, decisão do RH).

**Ordem e dependências:** T1 a T13 em sequência (T12 depende de T6 a T9; T13 de T7 e T8); T14 a T18 só dependem de T5 (erros); T19 a T21 não dependem de nada da 7a nem da 7b e **podem vir primeiro** se o dono quiser o cartão "Próximo pagamento" cedo; T22 depende de T2, T16 e T21; T23 de T7; T24 de T9 e T17.

---

## H. Perguntas ao dono e matriz de testes

### H.1 Perguntas (só as que mudam o que se constrói)

| # | Pergunta | Hipótese que se constrói sem resposta | Custo se errada |
|---|---|---|---|
| 1 | **As oito regras de férias valem como estão** (5 a 30 dias, 3 partes, uma de 14, abono de 1 a 10 escolhido pela pessoa, 30 dias de antecedência, saldo que não vence, 30 dias sempre)? Pode sobrar um resto de 1 a 4 dias que não dá para tirar? | sim para tudo; o resto pode sobrar (o exemplo do contrato aceita) | médio: mexe em T3 e nas mensagens; alto se entrar perda de saldo por prazo ou por faltas (cálculo novo) |
| 2 | **"2 dias antes de feriado ou do descanso":** os dias proibidos para começar são quinta e sexta, ou sexta e sábado? Pode começar no próprio feriado, no sábado ou no domingo? | proibido começar em feriado, sábado e domingo e nos 2 dias corridos antes de feriado ou de sábado | baixo: uma regra e os testes dela |
| 3 | **Registro do RH no passado e sugestão do gestor** antes do fim do período aquisitivo: qual resposta (o banco recusa e o contrato não tem código)? | só entram períodos que terminam antes do início; sem período, "não cabe no saldo" | baixo no código; pode pedir um código novo no contrato |
| 4 | **Sugestão do gestor:** conferir só duração (como o contrato diz) ou todas as regras menos a antecedência? | todas menos a antecedência (senão a pessoa nunca consegue aceitar) | médio: diverge do texto do contrato; mexe em T7 |
| 5 | **Pedido vencido:** a tarefa cancela o pedido sem decisão quando o início **chega** ou só depois que **passa**? E o prazo da sugestão (5 dias ou 2 dias antes) fica? | cancela quando o início já passou (ontem ou antes); prazo como proposto | baixo |
| 6 | **Pessoa que troca de setor com pedido parado no gestor antigo** (férias e ponto): passa ao RH? | sim, passa ao RH na troca de setor | médio: mexe na edição da ficha (fatia 1) e fecha a dívida do ponto |
| 7 | **Benefício dado direto pelo RH:** gravar quem deu e quando? E quem teve o benefício tirado pode pedir de novo (ele volta a "Disponíveis")? | grava; pode pedir de novo | baixo |
| 8 | **Holerite agendado "no dia do pagamento"** quando o RH muda a exceção depois: a data do holerite acompanha? | não acompanha (fica a data calculada no envio) | médio: reagendar exige achar e mover as tarefas pendentes |
| 9 | **Registrar férias no passado** recalcula o banco de horas dos dias já fechados? | sim | médio: chamada a mais no registro; se não, saldo errado em silêncio |
| 10 | **Benefícios contam como pendência** do painel do RH (já provisório)? | sim | baixo: um tipo a menos |
| 11 | **Painel do gestor:** o que é "não se aplica" na coluna de avaliação e de onde vem o rótulo do ciclo? | quem não é avaliado pelo gestor naquele formulário; rótulo tirado do título | baixo |

As demais lacunas de E.4 seguem com a sugestão escrita lá; o executor as registra no plano.

### H.2 Matriz de testes obrigatórios

| Caso | O que prova | Origem |
|---|---|---|
| Período sobreposto (um dia em comum; com pedido em andamento, aprovado e em gozo) | `SEM_SOBREPOSICAO` | roadmap:274; `yaml:286` |
| Férias perto de feriado e de fim de semana (véspera, antevéspera, 3 dias antes) | regra de feriado e descanso com o calendário da empresa (nacional, estadual, municipal, da empresa) | roadmap:274 |
| Saldo insuficiente; saldo reservado por pedido em análise; sugestão não reserva | `CABE_NO_SALDO`, `diasQueSobram` negativo | roadmap:274; `yaml:44-47` |
| Dois pedidos em paralelo; pedido × registro do RH em paralelo | o saldo nunca estoura; nenhum 500 | E.4.2 |
| Duas etapas completas (gestor aprova, RH confirma) e os desvios (gestor recusa; RH recusa o aprovado) | status do banco só muda na decisão final; `decisaoGestor` e `decisaoRh` certos | `yaml:226-229` |
| Gestor tenta decidir o próprio pedido, o de outro gestor e o de outro setor | o **mesmo** 404 | roadmap:274; `yaml:645-649` |
| RH confirma com o pedido no gestor e em `SUGESTAO_DE_DATA`; pedido já decidido | 409 `PEDIDO_ETAPA_INCORRETA`; 409 `PEDIDO_JA_DECIDIDO` | `yaml:1085-1086` |
| RH decide pedido sem etapa: sendo a dona; sem linha em `GESTORES`; com linha | 422 `PEDIDO_PROPRIO` (vence); 422 `PEDIDO_RH_FORA_DE_GESTORES`; 200 com `ID_GESTOR` gravado | `yaml:1092-1100` |
| RH confirma o próprio pedido que o gestor aprovou | 200 | `yaml:1099-1100` |
| Decisão dupla (dois cliques; gestor × ausência; confirmar × recusar; aceitar × expirar) | um efeito, um e-mail, o outro recebe 409 | E.4.1 |
| Gestor ausente: pedido novo; ligar com pedidos parados; pedido em sugestão; desligar | etapa automática sem e-mail; parados andam e a contagem volta; sugestão não muda; só os novos voltam ao normal | `yaml:240-246` |
| Gestor desligado, trocado ou tirado do setor com pedido parado; setor que ganha gestor depois | passa ao RH sem gestor; o que nasceu no RH continua no RH | `yaml:248-257` |
| Sugerir, aceitar, recusar, expirar | datas trocadas no aceite; `CANCELADA` com `RECUSOU` e com `EXPIROU`; regras reconferidas sem antecedência | `yaml:397-408`, `:471-477` |
| Registro pelo RH: no passado, sem antecedência, para inativo | 201 `APROVADA` com as três decisões nulas; 422 | `yaml:1371-1378` |
| E-mails que saem: gestor aprova, recusa (com motivo), sugere (com datas); RH confirma, recusa (com motivo) | um por decisão, chave de idempotência por pedido e decisão, todo valor com `HtmlEncode` | `CONV:114` |
| E-mails que **não** saem: criar, aceitar, recusar sugestão, aprovação automática, registro do RH, expirar, cancelar, qualquer coisa de benefício | fila vazia | `yaml:269-272`, `:406`, `:473`, `:1373`, `:1505` |
| Pessoa sem e-mail corporativo | decisão funciona, nada na fila | E.4.14 |
| Inativar o funcionário | férias futuras `SOLICITADA` e `APROVADA` canceladas pelo trigger (sem evento); `EM_GOZO` não; pedidos de benefício pendentes cancelados pela aplicação; saldo e filas não quebram | roadmap:274; `yaml:1708` |
| Tarefa de férias: duas vezes; API parada por dias | idempotente; recupera; só transições aceitas pelo trigger | roadmap:72, :268 |
| Motivo da recusa: 4, 5, 500, 501 caracteres, só espaços | 400 nos de fora; nunca o `CHECK` do banco | `yaml:3133-3140` |
| Auditoria de férias: pedir, cada decisão, sugestão, resposta, registro, tarefas (Sistema) | um evento por fato, com a área `FERIAS`; datas originais guardadas no aceite | roadmap:74; `yaml:2449` |
| Benefício: não opcional, sem dependentes, sem trajeto, já ativo, já pendente | os três 422 e o 409 | `yaml:1597-1598` |
| Benefício: pedir de novo depois de recusado | a mesma linha volta a `PENDENTE`; autorização anterior no log; filhos antigos cancelados | `yaml:1590-1592`; `V001:2460-2469` |
| Dependente e trajeto: adesão pendente, recusada, inativa, de outra pessoa | 409 `BENEFICIO_ADESAO_NAO_ATIVA`; 404 | `yaml:1633-1636` |
| RH: aprovar junto; recusar cancela filhos; dependente de outra adesão; decidir duas vezes; aprovar de inativo | exemplos :1785-1789 e :1833-1835; 404; 409; 422 | `yaml:1761-1765`, `:1849-1851` |
| Privacidade: rotas do gestor; log de auditoria do trajeto | nenhum dependente, trajeto ou origem; "campo mudou" sem valor | `yaml:1491-1492`, `:1672-1673`, `:2300` |
| Pagamento em fevereiro (dia 29, 30, 31; ano bissexto) | último dia e, se preciso, antecipação: dois ajustes | roadmap:274; `yaml:2088-2089` |
| Pagamento em fim de semana e em feriado; exceção em fim de semana | antecipa; a exceção vale como está | `yaml:2053-2056` |
| Próximo pagamento: hoje é o dia; virada de ano; sem configuração; só com exceção | hoje; competência dezembro; `null`; a exceção | `yaml:2025-2027`, `:3458` |
| Exceção: criar, trocar, remover, data fora do mês, dois PUT juntos | 200, 200, 204, 400, sem 500 | `yaml:2120-2122` |
| Painel de gestor sem equipe (sem setor) e com equipe vazia | `setor: null`, zeros e listas vazias | roadmap:274; `yaml:2296-2297` |
| Contador do gestor | férias `COM_O_GESTOR` + ajustes na etapa dele; sugestão não conta | `yaml:2229-2233` |
| Painel do RH: presença em dia útil (antes e depois do limite) e sem expediente | contagens e `total` do contrato | `yaml:3587-3598` |
| Painel do RH: pendências, próximos dias, treinamentos, alterações | limites (8, 5, 4), ordem, tipos com zero fora, admissão só em 7 dias | `yaml:2375-2398` |
| Indicadores da ficha (RH e gestor) | `feriasDisponiveisDias` e `proximoPagamento` iguais às rotas de origem | `f1:479-481`, `:889` |
| Matriz de perfis das 41 rotas | 403 nas famílias erradas; `/api/eu` para os três perfis | `x-perfis` |

### H.3 Testes antigos que afirmam "ainda sem fatia 7" e vão mudar

| Teste | O que afirma hoje | O que acontece |
|---|---|---|
| `tests/Colaboradores/PedidosParadosNoGestorRegistroTests.cs:39` | **uma única** `IPedidosParadosNoGestorDaFatia` registrada, a do ponto (`Assert.Single`) | **quebra** quando a 7 registrar a de férias: passar a esperar as duas |
| `tests/Persistencia/tabelas-sem-mapeamento.txt:5-7`, `:9` | `DEPENDENTES`, `FERIAS`, `PAGAMENTOS_EXCECOES`, `TRAJETOS_VALE_TRANSPORTE` sem mapeamento | saem da lista no passo em que cada uma é mapeada (o teste confere os dois lados) |
| `tests/Contrato/ContratoDivergenciaTests.cs:19` | os 38 `operationId` novos fora do escopo | entram um a um |
| `tests/Contrato/ContratoEStatusTests.cs` + `STATUS:32-34` | as três rotas de contadores "em andamento, parcial" | atualizar a observação (e a situação, se o dono fechar) |
| `tests/Colaboradores/IndicadoresDaFichaTests.cs:35-52` | "sem nenhuma fonte, os quatro campos são `null`", incluindo `proximoPagamento` | o teste remove só `IFonteDeIndicadorDaFicha` (`:38`); quando a 7 registrar a `IFonteDoProximoPagamento` de verdade, `proximoPagamento` deixa de ser `null` nesse teste: remover também essa fonte, ou a asserção quebra |
| `tests/Colaboradores/IndicadoresDaFichaTests.cs:55-76` | a fonte falsa de próximo pagamento devolve 2026-10-30 | com a fonte real também registrada, o caso de uso percorre as fontes em ordem (`App/Colaboradores/FuncionariosCasosDeUsoDeLeitura.cs:83`): a real pode responder antes da falsa. Remover a real no teste |
| `tests/Equipe/IndicadoresDaEquipeTests.cs:76-90` | com uma só fonte, `feriasDisponiveisDias` é `null` | continua verde (o teste remove todas as fontes, `:79`), mas deixa de descrever a produção; acrescentar o teste com a fonte real |
| `tests/Painel/ContadoresDoMenuTests.cs:90-105` e `tests/Ponto/DecidirComoGestorTests.cs:329` | `pendenciasDaEquipe` só com fontes falsas ou só com ajustes | conferir se algum deles semeia férias `SOLICITADA` com `ID_GESTOR` (`tests/Ponto/PontoDeTeste.cs:124` semeia férias direto no banco): se sim, a soma muda |
| Testes de desligamento, de troca de gestor e de "gestor ausente" (fatias 1b e 1c) | `pedidosAprovadosAgora.ferias` = 0; nenhum efeito sobre benefícios pendentes | continuam verdes sem dados de férias; ganham os casos novos (T12, T18) |
| Ficha do RH (`quantidadeDependentes` = 0) | 0 fixo | ganha o caso com dependente aprovado |

Não rodei nenhum deles: a lista vem da leitura do código e de buscas por "fatia 7", `FeriasDisponiveis`, `ProximoPagamento` e `PendenciasDaEquipe` em `back-end/tests`.

---

## O que mais pode dar errado

As condições que o contrato não cobre e que mais machucam: **(1) o saldo em corrida:** sem travar a pessoa antes de conferir, dois pedidos simultâneos (ou pedido, registro do RH e aceite de sugestão) reservam mais de 30 dias e nenhum teste sequencial pega; **(2) a porta dos pedidos parados esquecida:** se a fatia 7 não registrar a implementação de férias, trocar ou desligar um gestor deixa pedidos de férias presos na etapa dele para sempre, e "gestor ausente" não aprova nada, tudo em silêncio; **(3) as datas dos triggers:** `NOW()` e `CURDATE()` do banco contra o relógio da aplicação e o fuso -03:00: uma data gravada em UTC faz o `CHECK` de análise recusar a aprovação, e o teste de inativação muda de resultado conforme o dia em que roda; **(4) os `CHECK` em cadeia de `FERIAS`:** cada mudança de estado tem de gravar três ou quatro colunas no mesmo UPDATE (resultado, data, status, motivo); esquecer uma vira 500 só naquele caminho; **(5) a mensagem da pessoa sobrescrita:** copiar o repositório do ponto ao pé da letra apaga `OBSERVACAO` na decisão do gestor; **(6) o cálculo de saldo espalhado:** seis telas mostram o mesmo número, e a ficha da fatia 1 também; duas implementações divergem na primeira borda (admissão em 29/02, pedido em sugestão, readmissão); **(7) a regra da parte de 14 dias,** que o contrato descreve em uma frase e cuja fórmula ninguém escreveu: é a mais provável de sair errada e a que o dono mais vai testar na demo; **(8) privacidade por vazamento lateral:** dependente de menor e endereço de casa aparecendo no log de auditoria, numa rota de gestor ou numa mensagem de erro. Fora isso: a sugestão que a pessoa nunca consegue aceitar; o pedido de quem trocou de setor, invisível para os dois gestores; o holerite agendado que não acompanha a exceção de pagamento; o painel do RH que soma cinco fontes e passa a responder devagar ou a cair inteiro quando uma delas falha (os contadores do menu já isolam a falha por fonte, `App/Painel/ContadoresDoMenuCasoDeUso.cs:33-44`; os painéis não têm isso); o N+1 do saldo no painel do gestor; e as frases prontas (`resumo`, `descricao`, `titulo`, `autor`), que o contrato só mostra por exemplo e que cada tela vai esperar de um jeito.
