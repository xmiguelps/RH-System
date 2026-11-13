# Levantamento da fatia 6 (TAD, treinamentos) do back-end (anexo do plano da fatia 6)

Feito em 2026-11-13, na branch `feat/fatia-6-tad` (criada da `main` com as fatias 0 a 5), só lendo. Fontes lidas inteiras: `contrato/fontes/60-fatia-6-tad.yaml` (citado `yaml:N`), `00-base.yaml` (`base:N`); as tabelas do TAD e os comentários delas em `Banco de Dados/Scripts/V001__esquema_inicial.sql` (`V001:N`), os quatro triggers em `V002__triggers.sql` (`V002:N`) e `LEIA-ME.md`. Lidos por trecho (as linhas que falam de treinamento, onboarding e certificado): `CONVENCOES.md`, `PROVISORIOS.md`, `STATUS.md`, `ROTAS.md`, `ELENCO.md`, `CHANGELOG.md`, `20-fatia-1`, `25-fatia-2`, `70-fatia-7`, o roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, `roadmap:N`: decisões 1 a 14, seção da fatia 6 e dependências cruzadas), o README (testes, agendador, fatias 4 e 5), o levantamento da fatia 5 (modelo deste) e o código de `back-end/src` e dos testes citado em F. Caminhos de código relativos a `back-end/src/` (`Api/` = `RHSystem.Api/` etc.). Telas: abri `Figma/telas/22-rh-treinamentos.png` e `42-func-treinamentos-aba-concluidos.png`; as telas 23 a 26, 41 e 58 **não foram abertas** (o que digo delas vem da descrição do contrato). `Documentação/` só tem o PDF de requisitos, que não li. **INFERIDO** = conclusão minha, não lida nem medida. **LACUNA** = nenhuma fonte responde; o executor decide e registra, ou pergunta ao dono (H.1). Nada foi compilado nem executado: toda afirmação sobre comportamento do EF, do MySQL ou do MigraDoc que não esteja citada de uma sonda já existente é INFERIDA.

O contrato é a autoridade. Onde o roadmap ou os comentários do `V001` divergem, vale o contrato e a divergência está em E.3.

---

## A. As 31 operações

Perfis: F = Funcionario, G = Gestor, RH. `[F,G,RH]` = família `/api/eu` (a própria pessoa; nunca 403). **PUB** = `/api/publico`, sem login (`security: []`, `x-perfis: [Publico]`). Paginação = `pagina` (≥1, padrão 1) e `tamanhoPagina` (1-100, padrão 20) (`base:25-34`); `busca` até 100 caracteres (`base:35-39`). Toda operação tem 500; todas menos a pública têm 401; não repito. `{treinamentoId}`, `{moduloId}` e `{funcionarioId}` são inteiros (`yaml:1636-1651`; sem `minimum`). `confirmar` = query booleana, padrão `false` (`yaml:1652-1660`). `*` = tem `x-provisorio`.

| # | Rota | operationId | Perfil | Entrada | 2xx | Erros (além de 401/500) | Efeito declarado |
|---|---|---|---|---|---|---|---|
| 1 | GET `/api/rh/treinamentos` | listarTreinamentos | RH | paginação, `busca` (título), `direcao` (padrão `DESC`), `situacao` (`TreinamentoSituacao`), `ordenarPor` (`TITULO`\|`COMECA_EM`, padrão `COMECA_EM`) | 200 `TreinamentoRhLista` | 400, 403 | sem `situacao` = todos, **inclusive rascunhos**; `contagens` (todos, rascunhos, agendados, emAndamento, encerrados) ignora `situacao` e respeita `busca`; rascunho com `totalParticipantes` e `concluidos` zerados; `certificadosEmitidos` = `concluidos` se emite certificado, senão 0 (`yaml:106-121`, `:1922`) |
| 2 | POST `/api/rh/treinamentos` | criarTreinamento | RH | `TreinamentoComando` (só `titulo` obrigatório) | 201 `TreinamentoRh` | 400, 403 | nasce **rascunho** (`PUBLICADO` falso), sem módulos; a API **sempre grava** `incluiAdmitidos` e `emiteCertificado` recebidos; `DATA_FIM` fica `NULL`, `AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM` ficam `FALSE`; nenhum e-mail (`yaml:162-172`) |
| 3 | GET `/api/rh/treinamentos/resumo` | obterIndicadoresDeTreinamentos | RH | — | 200 `TreinamentoRhIndicadores` | 403 | `emAndamento` (situação `EM_ANDAMENTO`); `obrigatorios` (obrigatórios **publicados**, em qualquer situação); `certificadosEmitidosNoAno` (`DATA_EMISSAO` no ano corrente, só de treinamento que emite) + `ano`; `pessoasComObrigatorioPendente` (pessoas distintas com pendência) (`yaml:222-230`) |
| 4 | GET `/api/rh/treinamentos/onboarding` * | listarTreinamentosDeOnboarding | RH | — | 200 `TreinamentoOnboardingLista` | 403 | em vigor = publicado, marcado `onboarding` e **não encerrado** (agendado entra); sem paginação; lista vazia desliga a caixa do cadastro (`yaml:253-260`) |
| 5 | GET `/api/rh/treinamentos/publico/contagem` | contarPublicoDeTreinamento | RH | `publico` (obrigatório), `setorIds` (repetido, até 100), `funcionarioIds` (repetido, até 500) | 200 `{ total }` | 400, 403 | conta só **ativos cuja admissão já ocorreu**; `TODOS` ignora as listas; `SETORES` usa `setorIds`; `PESSOAS` usa `funcionarioIds` (`yaml:282-287`) |
| 6 | GET `/api/rh/treinamentos/{treinamentoId}` | obterTreinamento | RH | — | 200 `TreinamentoRh` | 403, 404 | qualquer situação; módulos em ordem, com texto e link; o PDF só como `nomeArquivo` (`yaml:320-322`) |
| 7 | PUT `/api/rh/treinamentos/{treinamentoId}` * | atualizarTreinamento | RH | `TreinamentoComando` | 200 `TreinamentoRh` | 400, 403, 404; 422 `TREINAMENTO_CAMPO_TRAVADO`, `TREINAMENTO_ENCERRADO` | substitui **todos** os campos (omitido volta ao padrão); rascunho: tudo muda, lista do outro público = 400; publicado: só `titulo`, `descricao`, `incluiAdmitidos` e, enquanto `AGENDADO`, `comecaEm`; os demais precisam vir com o valor atual; encerrado: nada muda (`yaml:359-375`) |
| 8 | DELETE `/api/rh/treinamentos/{treinamentoId}` | excluirTreinamento | RH | `confirmar` | 204 | 403, 404; 409 `CONFIRMACAO_NECESSARIA`; 422 `TREINAMENTO_COM_CERTIFICADOS` | exclusão **física**; 1º certificado emitido = 422 (mesmo com `confirmar=true`); rascunho e agendado apagam direto; em andamento ou encerrado sem certificado: 409 se há linha `INSCRITO`/`EM_ANDAMENTO`; uma transação com o treinamento **travado para escrita**, na ordem progresso → módulos → certificados → participantes → público → treinamento; depois apaga os PDFs dos módulos; auditoria `TREINAMENTOS`/`EXCLUIU` com título e `pendentes` (`yaml:453-491`) |
| 9 | POST `.../{treinamentoId}/publicar` | publicarTreinamento | RH | sem corpo | 200 `TreinamentoRh` | 400 (`errors` por campo), 403, 404; 422 `TREINAMENTO_JA_PUBLICADO` | exige `titulo`, `comecaEm`, `publico` (com pelo menos um setor ou uma pessoa) e pelo menos um módulo (`errors.modulos`); marca `PUBLICADO`; obrigatório cria linha `INSCRITO` para cada pessoa ativa do público (setor resolvido na hora; inativo pulado); opcional não cria linha; `TODOS` não guarda linhas de público; **nenhum e-mail**; a criação em lote vai à auditoria como evento explícito; não existe despublicar (`yaml:511-530`) |
| 10 | POST `.../{treinamentoId}/encerrar` | encerrarTreinamento | RH | `confirmar`; sem corpo | 200 `TreinamentoRh` | 403, 404; 409 `CONFIRMACAO_NECESSARIA`; 422 `TREINAMENTO_NAO_ESTA_EM_ANDAMENTO`, `TREINAMENTO_ENCERRADO` | só `EM_ANDAMENTO`; grava `ENCERRADO_EM` (agora), sem agendador; **sem reabrir**; quem começou termina, quem não começou perde o acesso, ninguém novo entra, deixa de ser pendência, certificados continuam; auditoria `TREINAMENTOS`/`ALTEROU` (`yaml:594-622`) |
| 11 | POST `.../{treinamentoId}/modulos` * | adicionarModuloDeTreinamento | RH | multipart `TreinamentoModuloComando` | 201 `TreinamentoModuloRh` | 400, 403, 404; 422 `TREINAMENTO_MODULOS_TRAVADOS` | entra no fim (`ordem` = último + 1); cada tipo exige o seu campo e recusa o dos outros; PDF até 10 MB conferido pelo conteúdo; **só em rascunho**; grava `CAMINHO_ARQUIVO` e `NOME_ARQUIVO_ORIGINAL` juntos e devolve só o nome (`yaml:687-696`) |
| 12 | PUT `.../{treinamentoId}/modulos/ordem` * | reordenarModulosDeTreinamento | RH | `{ moduloIds }` (todos, na nova ordem) | 200 `TreinamentoModulosRh` | 400, 403, 404; 422 `TREINAMENTO_MODULOS_TRAVADOS` | id faltando, repetido ou de outro treinamento = 400; só em rascunho; troca em duas fases numa transação (`yaml:734-736`) |
| 13 | PUT `.../modulos/{moduloId}` * | atualizarModuloDeTreinamento | RH | multipart `TreinamentoModuloComando` | 200 `TreinamentoModuloRh` | 400, 403, 404; 422 `TREINAMENTO_CAMPO_TRAVADO` | substitui o módulo sem mudar a `ordem`; sem `arquivo` num `PDF` o arquivo atual fica; rascunho: o `tipo` muda (o arquivo que deixa de valer é apagado); publicado: só o conteúdo, o `tipo` não muda (`yaml:772-776`) |
| 14 | DELETE `.../modulos/{moduloId}` * | excluirModuloDeTreinamento | RH | — | 204 | 403, 404; 422 `TREINAMENTO_MODULOS_TRAVADOS` | só em rascunho; os que sobram são renumerados; o arquivo é apagado (`yaml:813-814`) |
| 15 | GET `.../{treinamentoId}/participantes` | listarParticipantesDeTreinamento | RH | paginação, `busca` (nome), `direcao` (padrão `ASC`), `situacao` (`TreinamentoParticipanteSituacao`), `setorId`, `ordenarPor` (`NOME` padrão, `SETOR`, `SITUACAO`, `INICIO`, `CONCLUSAO`) | 200 `TreinamentoParticipantesLista` | 400, 403, 404 | só quem tem linha; `contagens` ignora `situacao` e respeita `setorId` e `busca`; rascunho = lista vazia; inativos continuam na lista; encerrado continua consultável (`yaml:836-846`) |
| 16 | GET `.../participantes/exportacao` | exportarParticipantesDeTreinamento | RH | `busca`, `situacao`, `setorId` | 200 `text/csv` (anexo) | 400, 403, 404 | todas as linhas, sem paginação; UTF-8 com BOM, `;`, colunas `Pessoa;Setor;Situação;Início;Conclusão`, datas `dd/MM/aaaa`, situação em português (Concluído, Em andamento, Não iniciado); células `=`, `+`, `-`, `@` neutralizadas; nome `treinamento-lgpd-na-pratica.csv` (`yaml:895-919`) |
| 17 | GET `.../participantes/{funcionarioId}/certificado/download` * | baixarCertificadoDeParticipante | RH | — | 200 `LinkTemporario` | 403, 404 | 404 se a pessoa não concluiu ou se o treinamento não emite certificado; o PDF é gerado na conclusão e, se a linha estiver sem arquivo, **este download o gera na hora, uma vez**; nunca insere outro certificado (`yaml:932-937`) |
| 18 | GET `/api/rh/funcionarios/{funcionarioId}/treinamentos` | listarTreinamentosDeFuncionario | RH | — | 200 `TreinamentoFichaRhResposta` | 403, 404 | as **linhas** da pessoa em treinamentos publicados já liberados (encerrado só se ela começou ou concluiu); obrigatórios primeiro, depois `comecaEm` mais antigo; `resumo` (pendentes, emAndamento, concluidos); sem paginação (`yaml:965-972`) |
| 19 | POST `/api/rh/funcionarios/{funcionarioId}/treinamentos/onboarding` * | incluirFuncionarioNoOnboarding | RH | sem corpo | 200 `TreinamentoOnboardingInclusaoResposta` | 403, 404; 422 `SEM_TREINAMENTO_DE_ONBOARDING`, `FUNCIONARIO_INATIVO` | cria linha `INSCRITO` em **cada** treinamento de onboarding em vigor, qualquer que seja o público; quem já tem linha não muda (`jaEstavaIncluida: true`); repetível; **é a regra das caixas do cadastro, da readmissão e da importação** (`yaml:998-1010`) |
| 20 | GET `/api/eu/treinamentos` | listarMeusTreinamentos | F,G,RH | paginação, `busca` (título e descrição), `aba` (`TreinamentoAbaMeus`), `obrigatorio` (bool) | 200 `TreinamentoMeuLista` | 400 | só publicados já liberados; linhas da pessoa + opcionais do público dela ainda não escolhidos (`DISPONIVEL`); encerrado só para quem começou ou concluiu; `aba` padrão `TODOS` (tudo que **não** foi concluído); `contagens` ignora `aba` e respeita `busca` e `obrigatorio`; ordem fixa: obrigatórios, `comecaEm` mais antigo, título (`yaml:1061-1078`) |
| 21 | GET `/api/eu/treinamentos/{treinamentoId}` | obterMeuTreinamento | F,G,RH | — | 200 `TreinamentoMeuDetalhe` | 404 | lista de módulos com `concluido`, sem o conteúdo; 404: sem linha e fora do público, não publicado, ainda não liberado, ou encerrado sem ter começado; opcional não escolhido vem `DISPONIVEL`; concluído traz `certificado` se emite (`yaml:1116-1120`) |
| 22 | POST `/api/eu/treinamentos/{treinamentoId}/inscricao` | inscreverMeEmTreinamento | F,G,RH | sem corpo | 200 `TreinamentoMeuDetalhe` | 404; 422 `TREINAMENTO_NAO_E_OPCIONAL`, `TREINAMENTO_ENCERRADO` | só opcional, do público, publicado e liberado; cria `INSCRITO`; repetir é inofensivo (`yaml:1162-1166`) |
| 23 | POST `/api/eu/treinamentos/{treinamentoId}/iniciar` | iniciarMeuTreinamento | F,G,RH | sem corpo | 200 `TreinamentoMeuDetalhe` | 404; 422 `INSCRICAO_PENDENTE`, `TREINAMENTO_ENCERRADO` | `INSCRITO` → `EM_ANDAMENTO`, `DATA_INICIO` = hoje; repetir devolve o estado atual **sem `UPDATE`**; não é obrigatório chamar (`yaml:1234-1238`) |
| 24 | GET `/api/eu/treinamentos/{treinamentoId}/modulos/{moduloId}` * | obterConteudoDeModulo | F,G,RH | — | 200 `TreinamentoModuloConteudo` | 404; 422 `INSCRICAO_PENDENTE` | módulos **em qualquer ordem**; vale para rever depois de concluído e com o treinamento encerrado (para quem começou ou concluiu); sem linha = 404; opcional sem inscrição = 422 (`yaml:1303-1308`) |
| 25 | GET `.../modulos/{moduloId}/download` * | baixarArquivoDeModulo | F,G,RH | — | 200 `LinkTemporario` | 404; 422 `INSCRICAO_PENDENTE` | só módulo com arquivo (outro = 404); mesmas regras de acesso do #24; anexo, nunca endereço público (`yaml:1343-1344`) |
| 26 | POST `.../modulos/{moduloId}/concluir` * | concluirModuloDeTreinamento | F,G,RH | sem corpo | 200 `TreinamentoModuloConclusaoResposta` | 404; 422 `INSCRICAO_PENDENTE`, `TREINAMENTO_ENCERRADO` | grava a linha de progresso; se era o último módulo, a API passa a linha da pessoa a `CONCLUIDO` (sem nota), **troca o código do certificado por UUID v4** e, se emite, gera o PDF; a primeira conclusão em `INSCRITO` também inicia; repetir não toca no banco; duas conclusões simultâneas = um `CONCLUIDO` e um certificado (`yaml:1371-1386`) |
| 27 | GET `/api/eu/treinamentos/{treinamentoId}/certificado/download` * | baixarMeuCertificado | F,G,RH | — | 200 `LinkTemporario` | 404 | 404 se não concluiu, se o certificado é de outra pessoa ou se o treinamento não emite; gera na hora, uma vez, se faltar o arquivo (`yaml:1456-1459`) |
| 28 | GET `/api/gestor/treinamentos` | listarTreinamentosDaEquipe | G | — | 200 `TreinamentoGestorLista` | 403 | publicados (agendados, em andamento e encerrados) que alcançam pelo menos uma pessoa do setor; a equipe **não inclui o gestor**; obrigatórios primeiro, depois `comecaEm` mais antigo; sem paginação (`yaml:1485-1491`) |
| 29 | GET `/api/gestor/treinamentos/{treinamentoId}/equipe` | listarPessoasDaEquipeNoTreinamento | G | — | 200 `TreinamentoGestorEquipeResposta` | 403, 404 | só pessoas do setor **com linha**; ordem por nome; sem nota e sem certificado; `emFeriasAte` lido de `FERIAS`; rascunho ou treinamento que não alcança o setor = 404 (`yaml:1514-1521`) |
| 30 | GET `/api/gestor/equipe/{funcionarioId}/treinamentos` | listarTreinamentosDePessoaDaEquipe | G | — | 200 `TreinamentoFichaGestorResposta` | 403, 404 | a mesma lista do #18, sem certificado; `resumo.pendentes` **é a fonte de `treinamentosPendentes` da fatia 1**; outro setor ou o próprio gestor = 404 (`yaml:1555-1559`) |
| 31 | GET `/api/publico/certificados/{codigoValidacao}` * | validarCertificado | **PUB** | `codigoValidacao` (`format: uuid`) | 200 `CertificadoValidacaoResposta` | 400, 404 (título próprio), **429** | 10 consultas por minuto por IP; resposta mínima (código, nome, título, data); código inexistente, de treinamento que não emite ou de conclusão sem certificado = o **mesmo 404** (`yaml:1587-1598`) |

Contagem por incremento sugerido (G): 6a construtor 12 (#1, #2, #3, #5 a #14), 6b fazer e certificar 10 (#17, #20 a #27, #31), 6c acompanhar, equipe e onboarding 9 (#4, #15, #16, #18, #19, #28, #29, #30 e a tarefa de admitidos, que não tem rota).

**O que cada tela precisa** (o contrato manda no que diverge do Figma, `yaml:16-26`): tela 22 (lista + "Quem fez"): #1, #3, #15, #16, #17 e o menu "..." (#6/#7, #10, #8). No PNG da tela 22 conferi que sobram do desenho, e **não existem**: a coluna "Período" com fim, "Prazos até 31/10", o filtro "Todos os tipos", a linha "Híbrido, turma presencial", a coluna "Nota", "com quiz final" e o botão "Lembrar quem não terminou"; e que **falta** no desenho a aba "Rascunhos" (`yaml:116`). Telas 23 a 26 (assistente): #2 e #7 (detalhes, certificado, publicação), #11 a #14 (conteúdo), #5 (contagem do público), #9. Telas 41 e 42: #20, #22, #23, #27 e a tela de execução, que não tem desenho (#21, #24, #25, #26; `yaml:42-45`). No PNG da tela 42 o cartão diz "Concluído com nota 9,0. Código do certificado: LGPD-2026-00482": o contrato tira a nota e troca o número por `CERT-{ano}-{id}` (`yaml:2119-2120`). Tela 58 (gestor): #28, #29 e a faixa "Sua parte", que sai do #20 (`yaml:1074-1076`). Tela 52 (ficha na equipe): #30. Tela 02 (cadastro): #4 e a caixa `incluirNoOnboarding`. A página pública de conferência não tem tela (`yaml:1598`).

---

## B. Catálogo de erros

Os títulos de 409 e 422 são os genéricos `Conflito.` e `Operação recusada.` (`base:238`, `:248`), com **três exceções que as exceções simples não cobrem**: `TREINAMENTO_ENCERRADO` tem título `Treinamento encerrado.` em todos os exemplos (`yaml:443`, `:673`, `:1221`, `:1290`, `:1443`); `CONFIRMACAO_NECESSARIA` tem título `Confirmação necessária.` e três campos extras (`yaml:1702-1709`); o 404 da rota pública tem título `Certificado não encontrado.` (`yaml:1627`). `ErroDeNegocioException` e `NaoEncontradoException` fixam o título (Application/Comum/Excecoes.cs:6, :22): os três casos saem por `ProblemaDeApiException` (Application/Comum/ProblemaDeApiException.cs:12). Copiar os textos dos exemplos, com um teste que os confere contra o yaml (o molde é `tests/RHSystem.Api.IntegrationTests/Contrato/MensagensDosFormulariosTests.cs`).

### B.1 Com código

| codigo (status) | title | detail exato (variações) | Extras | Onde sai |
|---|---|---|---|---|
| TREINAMENTO_JA_PUBLICADO (422) | Operação recusada. | Este treinamento já foi publicado. (`yaml:584`) | — | #9 (inclusive o encerrado) |
| TREINAMENTO_CAMPO_TRAVADO (422) | Operação recusada. | treinamento: "Depois de publicado, só dá para mudar o título, a descrição e a opção de incluir quem for admitido." (`yaml:438`) · módulo: "Depois de publicado, o tipo do módulo não muda. Dá para corrigir o título, a duração e o conteúdo." (`yaml:1683`) | — | #7, #13 |
| TREINAMENTO_MODULOS_TRAVADOS (422) | Operação recusada. | Este treinamento já foi publicado. Não dá mais para acrescentar, reordenar nem excluir módulos; só corrigir o conteúdo deles. (`yaml:1672`) | — | #11, #12, #14 (vale no encerrado) |
| TREINAMENTO_ENCERRADO (422) | **Treinamento encerrado.** | editar: "Este treinamento foi encerrado e não pode mais ser alterado." (`yaml:446`) · encerrar de novo: "Este treinamento já foi encerrado." (`yaml:676`) · rotas `/api/eu`: "O RH encerrou este treinamento. Fale com o RH se precisar dele." (`yaml:1224`, `:1293`, `:1446`) | — | #7, #10, #22, #23, #26 |
| TREINAMENTO_NAO_ESTA_EM_ANDAMENTO (422) | Operação recusada. | Só dá para encerrar um treinamento em andamento. Rascunho e agendado se excluem. (`yaml:668`) | — | #10 |
| TREINAMENTO_COM_CERTIFICADOS (422) | Operação recusada. | Este treinamento já emitiu 63 certificados e não pode ser excluído. Encerre o treinamento para que ninguém novo entre. (`yaml:1733`; o número é montado; o singular **não tem exemplo**) | `certificadosEmitidos` (≥ 1, obrigatório no schema, `yaml:2038-2041`) | #8 (antes da confirmação; vale com `confirmar=true`) |
| CONFIRMACAO_NECESSARIA (409) | **Confirmação necessária.** | encerrar: "38 pessoas ainda não concluíram este treinamento. Confirme para encerrar." (`yaml:1705`) · excluir: "9 pessoas ainda não concluíram este treinamento. Confirme para excluir." (`yaml:1719`); singular **sem exemplo** | `pendentes`, `emAndamento`, `naoIniciaram` (os três obrigatórios, `yaml:2024`; `pendentes` = soma dos outros dois) | #8, #10 |
| TREINAMENTO_NAO_E_OPCIONAL (422) | Operação recusada. | Este treinamento é obrigatório e não precisa de inscrição. (`yaml:1216`) | — | #22 |
| INSCRICAO_PENDENTE (422) | Operação recusada. | Inscreva-se no treinamento antes de começar. (`yaml:1285`, `:1694`) | — | #23, #24, #25, #26 |
| SEM_TREINAMENTO_DE_ONBOARDING (422) | Operação recusada. | rota #19: "Não há treinamento de onboarding publicado." (`yaml:1041`) · cadastro, readmissão e importação: "Não há treinamento de onboarding publicado. Desmarque a caixa ou publique o treinamento." (`20-fatia-1:246`, `:767`; `25-fatia-2:334`) | — | #19, `criarFuncionario`, `readmitirFuncionario`, `confirmarImportacao` |
| FUNCIONARIO_INATIVO (422) | Operação recusada. | Não é possível incluir uma pessoa inativa em treinamento. (`yaml:1049`) | `regraDoBanco: Nao e possivel inscrever funcionario inativo em treinamento.` (`yaml:1051`; o exemplo traz a regra mesmo quando é a aplicação que barra antes) | #19 |
| (404 próprio) | **Certificado não encontrado.** | Não encontramos um certificado com este código. (`yaml:1627-1629`) | — | #31 |
| (429) | Muitas tentativas. | Muitas tentativas. Aguarde um minuto e tente de novo. + `Retry-After` (`base:253-264`) | — | #31 |

Erros de **campo** (400, mapa `errors`) que o contrato nomeia: `modulos` ("Inclua pelo menos um módulo.") e `comecaEm` ("Informe a data em que o acesso é liberado.") em #9 (`yaml:569-571`). Os demais seguem o corpo: `titulo` (3 a 200 no treinamento, 1 a 200 no módulo), `descricao` (até 2000), `publico`, `setorIds` (até 100, sem repetição), `funcionarioIds` (até 500, sem repetição), `tipo`, `duracaoMinutos` (1 a 32767), `urlExterna` (até 500, `^https?://`), `conteudoTexto` (até 50000), `arquivo`, `moduloIds`, `situacao`, `ordenarPor`, `aba`, `codigoValidacao`. Os textos de todos eles são **LACUNA** (o executor escreve e registra).

### B.2 O que o banco impõe e o contrato não declara (validar antes; senão vira 500 ou 409 genérico)

| Recusa do banco | Quando acontece | O que a API deve fazer |
|---|---|---|
| 45000 `Nao e possivel inscrever funcionario inativo em treinamento.` (`V002:729-732`) | INSERT em `TREINAMENTOS_FUNCIONARIOS` de pessoa com `ATIVO = FALSE` (publicar, onboarding, inscrição, tarefa de admitidos) | publicar e a tarefa **pulam** o inativo no próprio `SELECT` (`yaml:521-522`); #19 devolve 422 `FUNCIONARIO_INATIVO` antes; na corrida (inativada no meio), traduzir para o mesmo 422 com `regraDoBanco`. O trigger só olha `ATIVO`: quem está "Em admissão" tem `Ativo = true` (Domain/Autenticacao/Funcionario.cs:26) e **passa** |
| 45000 `Treinamento concluido nao pode mudar de status.` (`V002:753-757`) | qualquer UPDATE que mude o `STATUS_TREINAMENTO` de uma linha `CONCLUIDO` | #23 e #26 repetidos devolvem o estado atual **sem UPDATE** (`yaml:1235-1236`, `:1380-1381`); todo UPDATE de situação é condicional na situação de partida |
| 45000 `Nota abaixo de 70: use o status REPROVADO.` (`V002:739-742`, `:760-763`) | `CONCLUIDO` com `NOTA` preenchida abaixo de 70 | a API **nunca grava nota** (`yaml:1373-1374`): não mapear `NOTA` |
| 1062 `UQ_TREINAMENTOS_CERTIFICADOS_TREINAMENTO_FUNCIONARIO` (dentro do trigger `AFTER`, `V002:774-779`, `:786-792`) | a aplicação insere um certificado antes do trigger, ou já existe linha de certificado da pessoa naquele treinamento | a aplicação **nunca insere** em `TREINAMENTOS_CERTIFICADOS`; só atualiza a linha do trigger (`yaml:1375`) |
| 1062 `UQ_TREINAMENTOS_CERTIFICADOS_CODIGO` | colisão do UUID v4 novo (improvável) | gerar outro |
| 1062 `UQ_TREINAMENTOS_FUNCIONARIOS_TREINAMENTO_FUNCIONARIO` (`V001:512`) | segunda linha da mesma pessoa (inscrição em duas abas; onboarding repetido; publicar × tarefa de admitidos; onboarding × publicar) | tratar como "já estava": #22 devolve o treinamento, #19 marca `jaEstavaIncluida`, a tarefa segue |
| 1062 `UQ_TREINAMENTOS_MODULOS_PROGRESSO_MODULO_FUNC` (`V001:1271`) | o mesmo módulo concluído duas vezes (clique duplo) | devolver o estado atual, sem erro |
| 1062 `UQ_TREINAMENTOS_MODULOS_TREINAMENTO_ORDEM` (`V001:1238`) | reordenar sem passo intermediário; excluir e renumerar; duas abas acrescentando módulo | duas fases numa transação (`yaml:735-736`; `LEIA-ME.md:91` usa `ORDEM + 1000` nos formulários); a corrida de duas abas é LACUNA (nova tentativa ou 409 genérico) |
| 1062 `UQ_TREINAMENTOS_PUBLICO_DESTINO` (`V001:1316`) | o mesmo setor ou pessoa duas vezes no público | `uniqueItems` já é 400; ao substituir o público, apagar antes de inserir, na mesma transação |
| 3819 `CHK_TREINAMENTOS_MODULOS_CONTEUDO` (`V001:1242-1246`) | módulo sem o campo do tipo; trocar o tipo sem preencher o campo novo | 400 por tipo antes; ao trocar o tipo, gravar o campo novo e **anular os dos outros** no mesmo UPDATE |
| 3819 `CHK_TREINAMENTOS_MODULOS_NOME_ARQUIVO` (`V001:1248-1249`) | `NOME_ARQUIVO_ORIGINAL` sem `CAMINHO_ARQUIVO` | os dois sempre juntos (gravar e anular) |
| 3819 `CHK_TREINAMENTOS_MODULOS_ORDEM` / `_DURACAO` / `_TIPO` (`V001:1239-1241`) | ordem ≤ 0, duração ≤ 0, tipo fora da lista | 400 antes; a fase intermediária da reordenação usa valores **positivos** |
| 3819 `CHK_TREINAMENTOS_ENCERRADO_PUBLICADO` (`V001:484-485`) | `ENCERRADO_EM` em rascunho | 422 `TREINAMENTO_NAO_ESTA_EM_ANDAMENTO` antes; UPDATE condicional em `PUBLICADO = TRUE` |
| 3819 `CHK_TREINAMENTOS_PUBLICO` (`V001:481-482`) | público fora de `TODOS`/`SETORES`/`PESSOAS` | 400 antes |
| 3819 `CHK_TREINAMENTOS_PUBLICO_SETOR_OU_PESSOA` (`V001:1318-1319`) | linha de público com setor e pessoa, ou sem nenhum | uma linha por setor **ou** por pessoa |
| 3819 `CHK_TREINAMENTOS_FUNCIONARIOS_DATAS` (`V001:514`) | `DATA_CONCLUSAO` menor que `DATA_INICIO` (ver a armadilha do relógio em D.6) | gravar as duas datas pelo mesmo relógio |
| 3819 `CHK_TREINAMENTOS_FUNCIONARIOS_STATUS` (`V001:515`) | situação fora da lista | o enum da API só tem três valores |
| 1452 nas FKs de `TREINAMENTOS_PUBLICO` (`V001:1308-1314`) | `setorIds` ou `funcionarioIds` com id que não existe | 400 no campo antes (LACUNA de texto) |
| 1451 nas FKs sem `ON DELETE` (`V001:504-510`, `:530-536`, `:1234-1236`, `:1263-1269`, `:1304-1314`) | apagar treinamento com filhos; apagar módulo com progresso | apagar na ordem de `yaml:473-476`; módulo com progresso só existe em treinamento publicado, onde excluir módulo já é 422 |
| 413 / 415 | corpo acima do teto; corpo que não é multipart em #11 e #13 | padrão já aceito nas fatias 1c, 3, 4 e 5 (não estão no contrato) |
| Sem recusa (o banco **aceita**) | reabrir (`ENCERRADO_EM` de volta a `NULL`, `V001:3604-3606`); despublicar; mudar `EMITE_CERTIFICADO` depois de emitir; módulo novo em treinamento publicado; inscrever em treinamento encerrado ou rascunho; concluir sem ter terminado os módulos; `CONCLUIDO` direto de `INSCRITO`; código de validação em qualquer formato (não há `CHECK` de UUID v4, ao contrário de `FORMULARIOS_PARTICIPACOES`); progresso de módulo de quem não tem linha no treinamento | tudo conferido pela aplicação |

---

## C. Schemas (`yaml:1737-2298`)

### C.1 Enums

- **TreinamentoSituacao** `RASCUNHO|AGENDADO|EM_ANDAMENTO|ENCERRADO`: calculada com a data de hoje em -03:00; não há coluna (`yaml:1739-1742`).
- **TreinamentoPublico** `TODOS|SETORES|PESSOAS` (= `CHK_TREINAMENTOS_PUBLICO`; não existe "por cargo") (`yaml:1743-1746`).
- **TreinamentoModuloTipo** `VIDEO|TEXTO|LINK|PDF` (= `CHK_TREINAMENTOS_MODULOS_TIPO`) (`yaml:1747-1750`).
- **TreinamentoParticipanteSituacao** `INSCRITO|EM_ANDAMENTO|CONCLUIDO` (= `STATUS_TREINAMENTO`; `REPROVADO` e `CANCELADO` existem no banco e a API nunca os grava nem devolve) (`yaml:1751-1754`).
- **TreinamentoMinhaSituacao** `DISPONIVEL|NAO_INICIADO|EM_ANDAMENTO|CONCLUIDO`: `DISPONIVEL` = opcional do público sem linha; `NAO_INICIADO` = linha `INSCRITO`; não há valor "encerrado" (`yaml:1755-1762`).
- **TreinamentoAbaMeus** `TODOS|DISPONIVEIS|EM_ANDAMENTO|CONCLUIDOS`: `TODOS` não inclui concluídos; `DISPONIVEIS` = `DISPONIVEL` + `NAO_INICIADO` (`yaml:1763-1766`, `:1067-1068`).
- Enums de consulta em linha: `ordenarPor` do #1 (`TITULO|COMECA_EM`) e do #15 (`NOME|SETOR|SITUACAO|INICIO|CONCLUSAO`) (`yaml:134`, `:864`).

### C.2 Construtor (RH)

- **TreinamentoComando** (`yaml:1769-1812`): `titulo` (3-200, único obrigatório), `descricao` (≤ 2000, texto ou markdown, nunca HTML), `comecaEm` (date ou null), `obrigatorio` (padrão `false`), `onboarding` (padrão `false`, x-provisorio), `emiteCertificado` (padrão **`true`**), `publico` (enum ou null), `setorIds` (≤ 100, únicos, padrão `[]`), `funcionarioIds` (≤ 500, únicos, padrão `[]`), `incluiAdmitidos` (padrão **`true`**; só tem efeito em obrigatório).
- **TreinamentoRh** (`yaml:1868-1899`): obrigatórios `id, titulo, obrigatorio, onboarding, emiteCertificado, publicado, situacao, encerradoEmUtc, setores, pessoas, incluiAdmitidos, modulos`; `descricao`, `comecaEm` e `publico` **não** são obrigatórios (mas os exemplos os trazem sempre: devolver sempre, com `null`). `setores` = `[{id, nome}]`; `pessoas` = `[FuncionarioResumo]` (`base:120-129`: `id, nome, ativo` obrigatórios; `cargo`, `setor` anuláveis).
- **TreinamentoModuloComando** (multipart, `yaml:1843-1855`): `titulo` (1-200), `tipo`, `duracaoMinutos` (1-32767 ou null), `urlExterna` (≤ 500, `^https?://`), `conteudoTexto` (≤ 50000), `arquivo` (binário). Obrigatórios: `titulo`, `tipo`.
- **TreinamentoModuloRh** (`yaml:1821-1833`): `id, ordem (≥1, sem buracos), titulo, tipo, duracaoMinutos, urlExterna, conteudoTexto, nomeArquivo`, todos obrigatórios (anuláveis os quatro últimos). `nomeArquivo` vem de `NOME_ARQUIVO_ORIGINAL`, **nunca** do caminho.
- **TreinamentoModulosOrdemComando** `{ moduloIds: [int] }` (≥ 1, únicos) e **TreinamentoModulosRh** `{ itens }` (`yaml:1835-1866`).

### C.3 Lista, indicadores e quem fez (RH)

- **TreinamentoRhResumo** (`yaml:1902-1922`): `id, titulo, obrigatorio, onboarding, emiteCertificado, situacao, comecaEm, encerradoEmUtc, publico, totalModulos, totalParticipantes` (pessoas com linha), `concluidos`, `certificadosEmitidos`. **TreinamentoRhLista** = paginação + `itens` + `contagens` (`todos, rascunhos, agendados, emAndamento, encerrados`).
- **TreinamentoRhIndicadores**: `emAndamento, obrigatorios, certificadosEmitidosNoAno, ano, pessoasComObrigatorioPendente` (`yaml:1946-1954`). **TreinamentoPublicoContagem** `{ total }`.
- **TreinamentoParticipantesLista** (`yaml:2003-2013`): paginação + `treinamento` (cabeçalho: `id, titulo, obrigatorio, onboarding, emiteCertificado, situacao, comecaEm, encerradoEmUtc, publico, totalModulos`) + `contagens` (`total, concluidos, emAndamento, naoIniciados`) + `itens` de **TreinamentoParticipante**: `funcionario` (`FuncionarioResumo`), `situacao`, `iniciadoEm` (date), `concluidoEmUtc`, `modulosConcluidos`, `certificadoDisponivel` (concluiu **e** o treinamento emite).
- **`concluidoEmUtc`** (regra única, `yaml:1999`): a hora em que a pessoa concluiu o **último módulo** (`TREINAMENTOS_MODULOS_PROGRESSO.DATA_CONCLUSAO`); conclusão sem linha de progresso (dado antigo): o dia de `DATA_CONCLUSAO` às 00:00 em -03:00.
- **TreinamentoFichaRhResposta** `{ resumo: {pendentes, emAndamento, concluidos}, itens }`; item: `treinamentoId, titulo, obrigatorio, onboarding, situacao, iniciadoEm, concluidoEmUtc, modulosConcluidos, totalModulos, certificadoDisponivel` (`yaml:2044-2075`).
- **TreinamentoOnboardingLista** `{ itens: [{id, titulo, comecaEm}] }`; **TreinamentoOnboardingInclusaoResposta** `{ funcionarioId, treinamentos: [{treinamentoId, titulo, jaEstavaIncluida}] }` (`yaml:2077-2108`).
- Problemas: **TreinamentoProblemaConfirmacao** (base `ProblemaDeApi`, `yaml:2015-2029`) e **TreinamentoProblemaComCertificados** (base `ProblemaDeRegraDeNegocio`, `yaml:2031-2041`).

### C.4 A própria pessoa

- **TreinamentoMeuCartao** (`yaml:2124-2147`), todos obrigatórios: `id, titulo, descricao, obrigatorio, onboarding, situacao, encerrado, comecaEm, totalModulos, duracaoTotalMinutos, modulosConcluidos, progressoPercentual, moduloAtual, iniciadoEm, concluidoEmUtc, certificado`.
  - `encerrado` só vem `true` em cartão `EM_ANDAMENTO` ou `CONCLUIDO`.
  - `duracaoTotalMinutos` = soma das durações dos módulos que têm duração; `null` se nenhum tem.
  - `progressoPercentual` = `modulosConcluidos / totalModulos`, **arredondado para baixo** (0 a 100).
  - `moduloAtual` = `ordem` do primeiro módulo ainda não concluído; `null` quando todos foram.
  - `certificado` = **CertificadoResumo** ou `null` (só quando concluiu e o treinamento emite).
- **CertificadoResumo** (`yaml:2111-2122`): `numero` (`CERT-{ano de emissão}-{id do certificado com 5 dígitos}`, calculado, sem coluna), `codigoValidacao` (UUID v4 da aplicação), `emitidoEm` (date, `DATA_EMISSAO`).
- **TreinamentoMeuLista** = paginação + `itens` + `contagens` (`todos, disponiveis, emAndamento, concluidos`). **TreinamentoMeuDetalhe** = cartão + `modulos` de **TreinamentoModuloEu** (`id, ordem, titulo, tipo, duracaoMinutos, concluido, concluidoEmUtc`; **sem conteúdo**).
- **TreinamentoModuloConteudo** (`yaml:2193-2207`): `id, ordem, titulo, tipo, duracaoMinutos, conteudoTexto` (só `TEXTO`), `urlExterna` (só `VIDEO` e `LINK`), `temArquivo` (verdadeiro em `PDF`), `concluido, concluidoEmUtc`.
- **TreinamentoModuloConclusaoResposta** `{ moduloId, treinamentoConcluido, treinamento: TreinamentoMeuDetalhe }` (`yaml:2209-2215`).

### C.5 Gestor e público

- **TreinamentoGestorResumo** (`yaml:2218-2231`): `id, titulo, obrigatorio, onboarding, situacao, comecaEm, totalNaEquipe, concluiram, inscritos`. `totalNaEquipe`: obrigatório = pessoas da equipe **com linha**; opcional = pessoas do setor **que estão no público**. `inscritos` = pessoas da equipe com linha.
- **TreinamentoGestorEquipeResposta** `{ treinamento, concluiram, faltamConcluir, itens }`; **TreinamentoGestorPessoa**: `funcionario, situacao, concluidoEmUtc, modulosConcluidos, totalModulos, moduloAtual, emFeriasAte` (sem `iniciadoEm`, sem certificado) (`yaml:2241-2263`).
- **TreinamentoFichaGestorResposta**: como a do RH, sem `certificadoDisponivel` (`yaml:2265-2287`).
- **CertificadoValidacaoResposta** `{ codigoValidacao, titularNome, treinamentoTitulo, emitidoEm }` e nada mais (`yaml:2290-2298`).

### C.6 Definições únicas que várias rotas usam

- **Pendência** (`yaml:47-49`): treinamento **obrigatório** `EM_ANDAMENTO` em que a pessoa tem linha `INSCRITO` ou `EM_ANDAMENTO`. Não é pendência: opcional, agendado, encerrado. Usada em #3, #18, #30, na faixa "Sua parte" e no painel da fatia 7.
- **Certificado emitido** (`yaml:55-56`, `:1922`): conclusão de uma pessoa em treinamento com `emiteCertificado` verdadeiro; não é a existência da linha em `TREINAMENTOS_CERTIFICADOS`.
- **Visibilidade para a pessoa** (`yaml:1062-1066`, `:1117-1119`): publicado, `comecaEm` já chegou, e (tem linha, ou é opcional do público dela); se encerrado, só com linha `EM_ANDAMENTO` ou `CONCLUIDO`.
- **Em vigor (onboarding)** (`yaml:255-256`): publicado, `onboarding`, não encerrado; agendado conta.

---

## D. O banco

### D.1 Tabelas (`V001`), com a nulabilidade que `MapeamentoContraBancoTests` vai conferir

O teste lê `information_schema.COLUMNS` (nulável, tem padrão, `EXTRA`) e compara com o modelo do EF (`tests/RHSystem.Api.IntegrationTests/Persistencia/MapeamentoContraBancoTests.cs:14-31`). As seis tabelas saem de `tests/.../Persistencia/tabelas-sem-mapeamento.txt` (linhas 10 a 15) no mesmo passo em que são mapeadas (`ConformidadeDoModeloTests`).

| Tabela (`V001`) | Coluna | Tipo | Nulo? | Padrão | Uso |
|---|---|---|---|---|---|
| `TREINAMENTOS` (:461-489) | `ID_TREINAMENTO` | INT AI | não | — | `id` |
| | `TITULO` | VARCHAR(200) | não | — | `titulo` |
| | `DESCRICAO` | TEXT | **sim** | — | `descricao` |
| | `TIPO_TREINAMENTO` | VARCHAR(50) | **sim** | — | `'ONBOARDING'` ou `NULL` (x-provisorio, `yaml:262`); índice `IDX_TREINAMENTOS_TIPO` |
| | `DATA_INICIO` | DATE | **sim** | — | `comecaEm` |
| | `DATA_FIM` | DATE | **sim** | — | **sem uso**, fica `NULL` (`yaml:169-170`) |
| | `OBRIGATORIO` | BOOLEAN | não | FALSE | `obrigatorio` |
| | `PUBLICADO` | BOOLEAN | não | FALSE | rascunho × publicado |
| | `PUBLICO` | VARCHAR(10) | **sim** | — | `publico` (`NULL` até o RH escolher) |
| | `EMITE_CERTIFICADO` | BOOLEAN | não | **TRUE** | gravar sempre; **não declarar o padrão no EF** |
| | `INCLUI_ADMITIDOS` | BOOLEAN | não | **TRUE** | idem |
| | `AVISA_POR_EMAIL` | BOOLEAN | não | FALSE | sem uso (`V001:3617-3655`) |
| | `LEMBRA_ANTES_DO_FIM` | BOOLEAN | não | FALSE | sem uso |
| | `ENCERRADO_EM` | DATETIME | **sim** | — | `encerradoEmUtc` (-03:00 no banco); índice `IDX_TREINAMENTOS_SITUACAO (PUBLICADO, ENCERRADO_EM, DATA_INICIO)` |
| `TREINAMENTOS_FUNCIONARIOS` (:495-516) | `ID_TREINAMENTO_FUNCIONARIO` | INT AI | não | — | |
| | `ID_TREINAMENTO`, `ID_FUNCIONARIO` | INT | não | — | chave única do par (:512) |
| | `STATUS_TREINAMENTO` | VARCHAR(30) | **sim** | — | o trigger troca `NULL` por `INSCRITO` no INSERT; a API grava explícito |
| | `DATA_INICIO` | DATE | **sim** | — | `iniciadoEm` |
| | `DATA_CONCLUSAO` | DATE | **sim** | — | o trigger preenche com `CURDATE()` se vier `NULL` |
| | `NOTA` | DECIMAL(5,2) | **sim** | — | **não mapear** |
| `TREINAMENTOS_CERTIFICADOS` (:522-540) | `ID_CERTIFICADO` | INT AI | não | — | o número de `CERT-{ano}-{id}` |
| | `ID_TREINAMENTO`, `ID_FUNCIONARIO` | INT | não | — | chave única do par (:539) |
| | `DATA_EMISSAO` | DATE | **sim** | — | o trigger grava `CURDATE()` |
| | `CODIGO_VALIDACAO` | VARCHAR(100) | **sim** | — | único (:538); o trigger grava `UUID()` (v1) |
| | `CAMINHO_ARQUIVO` | VARCHAR(255) | **sim** | — | o PDF; o trigger não preenche |
| `TREINAMENTOS_MODULOS` (:1221-1250) | `ID_MODULO` | INT AI | não | — | |
| | `ID_TREINAMENTO` | INT | não | — | |
| | `ORDEM` | INT | não | — | única por treinamento (:1238), > 0 |
| | `TITULO` | VARCHAR(200) | não | — | |
| | `TIPO_MODULO` | VARCHAR(10) | não | — | |
| | `DURACAO_MINUTOS` | SMALLINT | **sim** | — | |
| | `URL_EXTERNA` | VARCHAR(500) | **sim** | — | |
| | `CAMINHO_ARQUIVO` | VARCHAR(255) | **sim** | — | nunca devolvido |
| | `CONTEUDO_TEXTO` | MEDIUMTEXT | **sim** | — | |
| | `NOME_ARQUIVO_ORIGINAL` | VARCHAR(255) | **sim** | — | `nomeArquivo` |
| `TREINAMENTOS_MODULOS_PROGRESSO` (:1257-1272) | `ID_MODULO_PROGRESSO` | INT AI | não | — | |
| | `ID_MODULO`, `ID_FUNCIONARIO` | INT | não | — | chave única do par (:1271) |
| | `DATA_CONCLUSAO` | DATETIME | não | `CURRENT_TIMESTAMP` | instante em -03:00 |
| `TREINAMENTOS_PUBLICO` (:1296-1320) | `ID_TREINAMENTO_PUBLICO` | INT AI | não | — | |
| | `ID_TREINAMENTO` | INT | não | — | |
| | `ID_SETOR`, `ID_FUNCIONARIO` | INT | **sim** | — | um ou outro (`CHECK` :1318) |
| | `CHAVE_DESTINO` | VARCHAR(30) **gerada, STORED** | — | — | mapear como calculada (`CalculadaNoBanco`, Infrastructure/Persistencia/ColunaCalculadaNoBanco.cs:13; roadmap:81 c) |

Escolhas de mapeamento que o levantamento recomenda (INFERIDO, a conferir contra o que o teste exige): **não mapear** `DATA_FIM`, `AVISA_POR_EMAIL`, `LEMBRA_ANTES_DO_FIM` nem `NOTA` (são anuláveis ou têm padrão `FALSE`, e a API não as lê; o `V001:3633-3637` diz que omitir e gravar `FALSE` dão o mesmo); mapear `EMITE_CERTIFICADO` e `INCLUI_ADMITIDOS` **sem** padrão do banco (roadmap:81 a; `LEIA-ME.md:176`; a sonda `SondaMapeamentoDosScriptsTests.cs:72-82` já prova que `false` chega como `false`); `ENCERRADO_EM` e `TREINAMENTOS_MODULOS_PROGRESSO.DATA_CONCLUSAO` com `ConversoresDeTempo.Brasil` (Infrastructure/Persistencia/ConversoresDeTempo.cs:19), a segunda **gravada pela aplicação** com o `IRelogio` (não deixar o padrão do banco: o relógio dos testes é fixo); `STATUS_TREINAMENTO`, `PUBLICO` e `TIPO_MODULO` com `EnumComoTextoMaiusculo` (Infrastructure/Persistencia/EnumComoTextoMaiusculo.cs:10). Nenhuma das seis entidades como `[Auditado]` (os eventos são explícitos, E.4.9).

### D.2 Triggers (`V002:716-792`), texto exato, todos `SQLSTATE '45000'`

| Trigger | O que faz |
|---|---|
| `TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_INSERT` (:719-747) | recusa pessoa com `ATIVO = FALSE` ("Nao e possivel inscrever funcionario inativo em treinamento."); `STATUS_TREINAMENTO` nulo vira `INSCRITO`; se `CONCLUIDO`: recusa nota preenchida < 70 e preenche `DATA_CONCLUSAO = CURDATE()` se nula |
| `TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_UPDATE` (:749-768) | recusa mudar a situação de uma linha `CONCLUIDO` ("Treinamento concluido nao pode mudar de status."); ao virar `CONCLUIDO`, as mesmas duas regras do INSERT |
| `TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_INSERT` (:770-780) | se a linha nasce `CONCLUIDO`: INSERT em `TREINAMENTOS_CERTIFICADOS (ID_TREINAMENTO, ID_FUNCIONARIO, DATA_EMISSAO, CODIGO_VALIDACAO)` com `CURDATE()` e `UUID()` |
| `TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_UPDATE` (:782-792) | o mesmo INSERT quando a situação **passa** a `CONCLUIDO` (`<=>` trata o nulo) |

`TREINAMENTOS`, `TREINAMENTOS_MODULOS`, `TREINAMENTOS_MODULOS_PROGRESSO`, `TREINAMENTOS_PUBLICO` e `TREINAMENTOS_CERTIFICADOS` **não têm trigger** (`V001:2829-2830`, `:2692`, `:3615`). Não há trigger de `DELETE` em nenhuma das seis (`yaml:485-486`). `V003` tem duas funções do grupo (`FN_VERIFICAR_TREINAMENTO_CONCLUIDO`, `FN_CALCULAR_PERCENTUAL_TREINAMENTO`, `V003:78-111`): a segunda é o percentual de **treinamentos** concluídos da pessoa, não o progresso por módulo; nenhuma serve ao contrato (roadmap:80, decisão 11: reimplementar).

### D.3 Como os estados ficam nas colunas

Treinamento (`V001:3589-3594`), com `hoje` em -03:00:

| Situação | Colunas |
|---|---|
| `RASCUNHO` | `PUBLICADO = FALSE` |
| `AGENDADO` | `PUBLICADO = TRUE`, `DATA_INICIO > hoje`, `ENCERRADO_EM IS NULL` |
| `EM_ANDAMENTO` | `PUBLICADO = TRUE`, `DATA_INICIO <= hoje`, `ENCERRADO_EM IS NULL` |
| `ENCERRADO` | `ENCERRADO_EM IS NOT NULL` |

Publicado com `DATA_INICIO` nula só existe em dado antigo (publicar exige a data): tratar como já liberado (`70-fatia-7:2386`: "a data de início, quando há, já chegou"). O encerrado continua publicado e pode ter `DATA_INICIO` qualquer.

Pessoa: `STATUS_TREINAMENTO` direto (`INSCRITO`, `EM_ANDAMENTO`, `CONCLUIDO`). `TreinamentoMinhaSituacao`: sem linha + opcional do público = `DISPONIVEL`; `INSCRITO` = `NAO_INICIADO`. Linhas `REPROVADO`, `CANCELADO` ou de situação nula (dado antigo): **LACUNA**; hipótese: as consultas filtram pelos três valores e as demais linhas não aparecem em lista nem em contagem.

Onboarding: `TIPO_TREINAMENTO = 'ONBOARDING'`; `onboarding: false` grava `NULL` (INFERIDO; o contrato só diz o valor do verdadeiro, `yaml:262`).

### D.4 Como cada coisa é gravada

- **Público**: `TODOS` não guarda linhas; `SETORES` = uma linha por setor (`ID_SETOR`); `PESSOAS` = uma por pessoa (`ID_FUNCIONARIO`); trocar o público apaga as linhas antigas (`yaml:522`; `V001:2788-2790`).
- **Publicar** (`yaml:519-527`): `PUBLICADO = TRUE` num UPDATE condicional em `PUBLICADO = FALSE`; se obrigatório, um INSERT em lote de `(ID_TREINAMENTO, ID_FUNCIONARIO, 'INSCRITO')` para cada pessoa **ativa e com admissão já ocorrida** do público (`yaml:38-39`), pulando quem já tem linha (o onboarding pode ter incluído alguém antes? não: a inclusão exige publicado; mas a tarefa de admitidos e uma segunda aba podem correr junto).
- **Módulo**: por tipo, o campo dele e os outros nulos; PDF grava `CAMINHO_ARQUIVO` + `NOME_ARQUIVO_ORIGINAL`.
- **Concluir módulo**: uma linha em `TREINAMENTOS_MODULOS_PROGRESSO` (só existe linha de módulo concluído). Se a linha da pessoa estava `INSCRITO`: `EM_ANDAMENTO` + `DATA_INICIO = hoje`. Se era o último: `CONCLUIDO`; o trigger cria o certificado; a aplicação troca o código e (se emite) gera o PDF e grava o caminho.
- **Encerrar**: `ENCERRADO_EM = agora` (convertido para -03:00), UPDATE condicional em `PUBLICADO = TRUE AND ENCERRADO_EM IS NULL AND DATA_INICIO <= hoje`.
- **Excluir**: a ordem de `yaml:473-476`, com o treinamento travado.

### D.5 O que a aplicação tem de garantir (o banco não garante)

1. `SETORES` com pelo menos um setor e `PESSOAS` com pelo menos uma pessoa ao publicar (`V001:2821-2822`).
2. Publicado não volta a rascunho; encerrado não reabre (`V001:3604-3606`).
3. Campos travados depois de publicado (público, obrigatoriedade, onboarding, certificado; `comecaEm` fora do agendado), tipo do módulo e a lista de módulos.
4. Só conclui o treinamento quem concluiu **todos** os módulos; um `CONCLUIDO`, um certificado, mesmo com duas chamadas ao mesmo tempo.
5. O código de validação que aparece em qualquer resposta ou PDF é **v4**; o v1 do trigger nunca sai.
6. `emiteCertificado` falso: sem PDF, sem `certificado` nas respostas, 404 na conferência, `certificadosEmitidos = 0`.
7. Encerrado: ninguém novo entra (inscrição, onboarding, tarefa de admitidos, iniciar de quem estava `INSCRITO`).
8. A pessoa só mexe na própria linha; progresso só de módulo do treinamento da rota (`moduloId` de outro treinamento = 404, `yaml:1385`).
9. Excluir só sem certificado emitido, na ordem certa, e apagar os arquivos depois.
10. Visibilidade e pendência (C.6) iguais em todas as rotas.
11. A tarefa de `incluiAdmitidos` (D.7).

### D.6 Armadilhas do banco

- **Relógio do banco × relógio da aplicação.** O trigger usa `CURDATE()` para `DATA_CONCLUSAO` (se vier nula) e para `DATA_EMISSAO` (sempre). A aplicação usa `IRelogio`, que nos testes é fixo em 2026-10-07T12:42Z (levantamento da fatia 5, F.1). Consequências (INFERIDO): (a) `iniciadoEm` sai do relógio e a conclusão sairia do banco, e se o relógio da aplicação estiver **à frente** do banco o `CHK_TREINAMENTOS_FUNCIONARIOS_DATAS` recusa a conclusão; (b) `certificadosEmitidosNoAno` e `CERT-{ano}` usariam o ano real, e os testes com relógio em 2026 quebrariam em janeiro de 2027. Recomendo: a aplicação grava `DATA_CONCLUSAO` explícita (o trigger respeita valor não nulo, `V002:764-766`) e, no mesmo UPDATE em que troca o código do certificado, regrava `DATA_EMISSAO` com o dia do `IRelogio` em -03:00. Isto se afasta da regra "deixar vazias as colunas que os triggers preenchem" (roadmap:73), de propósito; registrar no plano.
- **A linha do certificado nasce fora do EF.** O INSERT é do trigger: o contexto não a rastreia. Depois do `SaveChanges` que grava `CONCLUIDO`, a aplicação faz um UPDATE direto na linha `(ID_TREINAMENTO, ID_FUNCIONARIO)`, **na mesma transação**, trocando `CODIGO_VALIDACAO` (e `DATA_EMISSAO`). Se a troca falhar, a transação inteira desfaz (a conclusão também). O interceptor de auditoria não vê nem o trigger nem `ExecuteUpdate` (roadmap:74): evento explícito.
- **`UUID()` é v1** e quase sequencial (`yaml:1588-1591`). Não há `CHECK` de formato na coluna: linha antiga ou troca perdida deixaria um v1 válido na conferência pública. Defesa barata: a conferência só aceita código cujo dígito de versão é `4` (INFERIDO; ver E.4.2).
- **Toda conclusão cria linha de certificado**, mesmo com `EMITE_CERTIFICADO = FALSE` (`V001:2800-2807`). Essas linhas são apagadas na exclusão (`LEIA-ME.md:93`) e ignoradas no resto.
- **Padrão `TRUE`** em `EMITE_CERTIFICADO` e `INCLUI_ADMITIDOS` (ver D.1).
- **`CONCLUIDO` é final**: um UPDATE "inofensivo" que regrave a mesma situação passa (`<=>`), mas qualquer mudança é 45000.
- **`STATUS_TREINAMENTO` anulável** e sem padrão: o trigger só corrige o nulo no INSERT.
- **Chaves estrangeiras sem `ON DELETE`** (= `RESTRICT`): a ordem da exclusão importa.
- **`(ID_TREINAMENTO, ORDEM)` única**: reordenar e renumerar em duas fases.
- **`DATETIME` em -03:00** (`ENCERRADO_EM`, progresso); `DATA_INICIO`, `DATA_CONCLUSAO` e `DATA_EMISSAO` são `DATE` (dia em -03:00).
- **Leitura repetível do InnoDB na conclusão**: duas transações concluindo os dois últimos módulos ao mesmo tempo não veem o INSERT uma da outra; sem trava, **nenhuma** das duas conclui o treinamento (INFERIDO; ver E.4.1).
- **`CONTEUDO_TEXTO` é `MEDIUMTEXT`** e o contrato limita a 50000 caracteres; `DESCRICAO` é `TEXT` (65535 bytes) e o contrato limita a 2000 caracteres: cabem.

### D.7 O que depende de tarefa agendada

O contrato tira quase tudo do agendador: liberar na data de início é **cálculo** ("sem agendador para isso", `yaml:519-520`); encerrar é à mão ("na hora, sem agendador", `yaml:597`); não há lembrete, prazo nem e-mail (`yaml:86-88`, `:171-172`). Sobra **uma** coisa: com `incluiAdmitidos`, "o agendador inclui depois, enquanto o treinamento não for encerrado, quem for admitido e estiver no público (só obrigatório)" (`yaml:522-523`), o que também é o caminho de quem estava "Em admissão" na publicação (`yaml:284-285`). O contrato não dá nome, frequência nem a regra exata (E.4.5). Tudo o mais é imediato, dentro do pedido HTTP.

---

## E. Decisões, contradições, lacunas e riscos

### E.1 Decisões do dono já registradas (não reabrir)

1. **Sem quiz, prova final, nota, nota mínima, turma presencial, modalidade** (2026-10-29; roadmap:60, :66, :306; `yaml:16-19`). A fatia 6 não depende da 5 (roadmap:14).
2. **Concluir todos os módulos conclui o treinamento** e aí sai o certificado (2026-10-29; `V001:2656-2658`; roadmap:244).
3. **A caixa "Emitir certificado" fica**, por treinamento (2026-10-29; roadmap:245; `V001:2765-2767`).
4. **Rascunho e acesso liberado na data de início** (2026-10-29; roadmap:233).
5. **Público: todos, setores ou pessoas; sem "por cargo"** (roadmap:239).
6. **Inscrição em opcional usa `INSCRITO`** (2026-10-29; `V001:2660`).
7. **Treinamento não tem prazo** (2026-10-30; `CONVENCOES.md:116-118`): sem data final, "atrasada", lembrete; encerrar e excluir com `CONFIRMACAO_NECESSARIA`.
8. **Encerrar**: quem começou termina, quem não começou perde, ninguém novo entra (2026-10-30; `yaml:51-53`, `:604-613`).
9. **Excluir só sem certificado emitido** (2026-10-30; `yaml:54-58`; `CONVENCOES.md:106`).
10. **Esta fatia não envia e-mail** (lista fechada, `CONVENCOES.md:114`; `yaml:86-88`).
11. **Código público = UUID, não o número sequencial; conferência com o mínimo de dados** (roadmap:246).
12. **Número do certificado `CERT-{ano}-{id}`**, sem sigla por treinamento (2026-11-02; `yaml:2119-2120`; `CHANGELOG.md:11`).
13. **O gestor reutiliza as telas do funcionário** para os treinamentos dele (roadmap:83).
14. Transversais: agendador idempotente e recuperável (roadmap:72); fuso (roadmap:73); auditoria explícita para trigger e lote (roadmap:74); texto de usuário nunca como HTML (roadmap:75); arquivo por link temporário e como anexo (roadmap:76); PDF com fonte que acentua, testado em Linux (roadmap:77); CSV neutralizado (roadmap:78); mapeamento sem padrão do banco (roadmap:81).

### E.2 Abertas no contrato (`x-provisorio`; `PROVISORIOS.md:22-34`, `:55`; roadmap:252)

| Assunto | Hipótese do contrato | Onde |
|---|---|---|
| Como o RH marca o treinamento de onboarding | caixa `onboarding` no passo Detalhes, em `TIPO_TREINAMENTO = 'ONBOARDING'`; pode haver mais de um em vigor e a pessoa entra em todos | #4, #19, `TreinamentoComando.onboarding` (`yaml:262`, `:1012`, `:1787`) |
| O que muda depois de publicar | só título, descrição, `incluiAdmitidos` e (agendado) `comecaEm` | #7 (`yaml:377`) |
| Onde ficam os vídeos | **só link externo**; `VIDEO` não aceita arquivo | #11, #13, #24, #25 (`yaml:698`, `:778`, `:1310`, `:1346`) |
| Mexer na estrutura dos módulos depois de publicado | proibido; só corrigir conteúdo | #11, #12, #13, #14 (`yaml:698`, `:738`, `:816`) |
| Modelo visual do certificado (o formato do número já foi decidido) | "o modelo simples que o back-end escolher"; o PDF traz o código e o endereço da página de conferência | #17, #26, #27, #31 (`yaml:939`, `:1388`, `:1461`, `:1600`) |

### E.3 Contradições

1. **Roadmap 6c "lembretes" e "Lembrete 7 dias antes do fim e inclusão de quem for admitido até o prazo" (roadmap:235, :247) × contrato sem prazo, sem lembrete e sem e-mail** (`yaml:10-14`, `:86-88`). Vale o contrato: o incremento 6c do roadmap não existe como lembretes; da frase só sobra a inclusão de admitidos, "enquanto o treinamento não for encerrado".
2. **`V001:3214` lista o tipo de tarefa `LIBERAR_TREINAMENTO` e `V001:3204` diz "liberar treinamento (6)" × contrato: liberar é cálculo, sem agendador** (`yaml:519-520`; `V001:2776-2777` diz o mesmo). Vale o contrato: não criar essa tarefa.
3. **`V001:2826-2827` "a lista de pendentes usa o prazo (DATA_FIM)" e `V001:2779` "DATA_FIM passa a valer como prazo" × contrato: `DATA_FIM` sem uso** (`yaml:169-170`, `:622`). Vale o contrato.
4. **`V001:2808-2811` "não mudar EMITE_CERTIFICADO depois da primeira conclusão" × contrato "não muda depois de publicado"** (`yaml:369-371`). O contrato é mais estrito; vale ele.
5. **Roadmap "o código público é o UUID [do trigger]" (roadmap:54, :241, :246) × contrato: a aplicação troca por v4** (`yaml:91-94`). Vale o contrato; o teste do roadmap "o PDF anexado à linha criada pelo trigger" continua valendo.
6. **"O arquivo só é gravado no Blob depois de o banco aceitar o módulo" (`yaml:694-695`; roadmap:76) × a receita das fatias 1c, 4 e 5** (validar tudo, gravar o arquivo sem transação aberta, transação curta, apagar o arquivo se falhar: Application/Arquivos/IArmazenamentoDeArquivos.cs:10-12). O `CHECK` do PDF exige o caminho no INSERT, então a linha não pode nascer antes do arquivo. Leitura que as fatias anteriores já adotaram: "o banco aceitar" = toda a validação da aplicação passou. Seguir a receita.
7. **Texto de `SEM_TREINAMENTO_DE_ONBOARDING`**: a rota #19 e o cadastro têm textos diferentes (B.1). Não é erro: a porta `IInclusaoNoOnboarding` lança o texto longo e o caso de uso do #19 lança o curto.
8. **`FUNCIONARIO_INATIVO` com `regraDoBanco` no exemplo** (`yaml:1051`) mesmo quando a aplicação barra antes: devolver a extensão sempre, com o texto fixo do trigger (o levantamento da fatia 5 fez o mesmo com `PERGUNTA_JA_RESPONDIDA`).
9. **"Agendado: `pendentes` = 0, ninguém o viu" (`yaml:2027`) × o obrigatório agendado já tem linhas `INSCRITO`** (criadas ao publicar, `yaml:519-522`): a contagem da confirmação não é "linhas não concluídas" e sim "linhas não concluídas de treinamento já liberado". Como agendado se exclui direto (`yaml:463-464`) e não se encerra, o caso só aparece na regra; não há contradição prática.
10. **STATUS.md:46 e :77** dizem que a caixa de onboarding "recusa até a fatia 6": o texto muda no fechamento. Os testes que hoje afirmam o 422 (`CadastroDeFuncionariosTests.cs:245-258`, `CriarFuncionarioTests.cs:311`, `ReadmissaoDeFuncionariosTests.cs:356-369`, `ConfirmarImportacaoTests.cs:576-585`) **continuam verdadeiros** depois da fatia (sem treinamento de onboarding no banco, o erro é o mesmo); ganham o par do caminho feliz.
11. **Figma × contrato**: lista em `yaml:16-26`; conferido nos PNG das telas 22 e 42 (A).

### E.4 Riscos e lacunas, por assunto

**1. Conclusão (o ponto mais delicado da fatia).**
- Ordem dos módulos: **livre** (`yaml:1306`). Percentual: piso de concluídos/total. O treinamento conclui quando o módulo concluído "era o último que faltava" (`yaml:1372`).
- Corrida: duas abas concluindo os dois últimos módulos. Com a leitura repetível, cada uma conta `n-1` e ninguém conclui; depois, repetir "concluir" não toca no banco (`yaml:1380`) e a pessoa fica presa em 100% sem `CONCLUIDO`. Defesa (INFERIDO): travar a linha de `TREINAMENTOS_FUNCIONARIOS` (`SELECT ... FOR UPDATE`) **antes** de ler o progresso; e, por segurança, a chamada repetida de um módulo já concluído confere "todos concluídos e a linha não está `CONCLUIDO`" e fecha o treinamento (isto toca no banco só no caso quebrado; registrar como desvio consciente de "sem tocar no banco").
- Conclusão × exclusão × encerramento: o contrato manda a exclusão travar o treinamento "para que uma conclusão no mesmo instante não seja apagada sem ter sido conferida" (`yaml:473-474`). As escritas de `/api/eu` (#22, #23, #26) precisam ler o treinamento com trava compartilhada ou de escrita, **sempre na mesma ordem** (treinamento, depois a linha da pessoa), senão há deadlock ou 1451/1452 cru.
- A geração do PDF não pode ficar dentro da transação (a receita do arquivo proíbe gravar blob com transação aberta) nem derrubar a conclusão: transação (progresso, `CONCLUIDO`, troca do código) → commit → gerar o PDF, gravar e UPDATE condicional `CAMINHO_ARQUIVO IS NULL`; se qualquer passo depois do commit falhar, registrar em log e deixar o download gerar (`yaml:936-937`).
- "Uma vez" no download sob demanda: dois downloads simultâneos geram dois PDFs; o UPDATE condicional decide quem fica e o perdedor apaga o próprio arquivo (ou a limpeza de sobras apaga, se `certificados/` tiver fonte de referências).
- `concluidoEmUtc` vem do progresso (máximo de `DATA_CONCLUSAO` dos módulos), não de `TREINAMENTOS_FUNCIONARIOS.DATA_CONCLUSAO`, que é só dia. Na resposta do #26 os dois precisam bater com o exemplo (`yaml:1417`, `:1423`).
- **LACUNA**: treinamento publicado cujo módulo teve o conteúdo trocado depois de alguém concluir: a conclusão fica (o contrato aceita, `yaml:774-775`).

**2. Certificado.**
- **Como, por quem, quando**: a aplicação gera o PDF na conclusão (se `emiteCertificado`), na linha que o trigger criou; o download gera se faltar (`yaml:936-937`, `:1376-1377`, `:1458-1459`). Quem conclui é sempre a própria pessoa (`/api/eu`); o RH só baixa.
- **Biblioteca**: já existe **PDFsharp-MigraDoc 6.2.4** (`back-end/Directory.Packages.props:37`; `Infrastructure/RHSystem.Infrastructure.csproj:21`), licença MIT, usada no espelho de ponto (Infrastructure/Ponto/GeradorDePdfDoEspelho.cs:15-29), com a fonte **Noto Sans embutida** e nenhum uso de fonte do sistema (Infrastructure/Ponto/FonteDoPdf.cs:10-49; registro global em :22-31). **PdfPig 0.1.16** existe só nos testes (`Directory.Packages.props:17`; `tests/.../Ponto/EspelhoEmPdfTests.cs`) para ler o texto do PDF e conferir acento. Não há QuestPDF nem iText. **Não falta pacote.** O que falta: um gerador do certificado (paisagem, uma página), reaproveitando `FonteDoPdf` (hoje no namespace de Ponto; mover para um lugar comum é uma decisão do executor), e, se o dono quiser QR code, aí sim faltaria biblioteca (o contrato não pede: só código e endereço, `yaml:1600`).
- **Conteúdo do PDF** (LACUNA, x-provisorio): hipótese mínima: nome da empresa (`IConfiguracaoDaEmpresa.ObterAsync`, nulo sem configuração: omitir), nome da pessoa, título do treinamento, data de emissão, carga horária se houver (`duracaoTotalMinutos`), `numero`, `codigoValidacao` e o endereço da página de conferência. O endereço precisa da URL do front: existe `App:UrlDoFront` (Api/appsettings.json:65; Infrastructure/Emails/OpcoesDeEmail.cs:73-78), mas ela só é **exigida** com o envio de e-mail ligado (:87-89): vazia, o PDF sai sem endereço ou com endereço relativo (decidir). O caminho da página pública no front é LACUNA (não há tela).
- **Código**: UUID v4 gerado pela aplicação (`Guid.NewGuid()`), em minúsculas com hífens (36 caracteres, como os exemplos), gravado na mesma transação da conclusão (D.6). O `numero` é calculado: `CERT-{ano de DATA_EMISSAO}-{ID_CERTIFICADO:00000}` (id acima de 99999 passa a ter mais dígitos: LACUNA sem importância).
- **O que a rota pública pode revelar**: só os quatro campos de `CertificadoValidacaoResposta`. **Não pode**: `numero`, id do certificado, id da pessoa ou do treinamento, CPF, matrícula, cargo, setor, e-mail, se a pessoa está ativa, se o treinamento está encerrado, link do PDF, e nenhuma diferença entre "não existe", "treinamento não emite" e "conclusão sem certificado" (mesmo status, mesmo corpo; `yaml:1594-1595`). Código que não é UUID: **400** (declarado, `yaml:1620`; ao contrário do formulário público, aqui o formato errado não é 404; não vaza nada, porque o formato é público). Limite de 10 por minuto por IP antes de qualquer consulta.
- **Riscos de privacidade que o contrato não trata** (LACUNA): (a) o nome de quem **saiu da empresa** continua aparecendo para qualquer um com o código ("um código válido continua válido", `yaml:1597`); (b) o título mostrado é o **atual**: o RH pode mudar o título depois de publicar (`yaml:367`), e o PDF já emitido guarda o título antigo; (c) a consulta pública não deixa evento de auditoria (hipótese: não registrar, como a página pública do formulário); (d) o tempo de resposta do 404 e do 200 difere pouco e o código tem 122 bits: aceitável.
- **Auditoria do download**: o holerite e o espelho gravam `BAIXOU` antes de o link existir (Application/Ponto/BaixarEspelhoEmPdfCasoDeUso.cs:66-74); o contrato da fatia 6 não fala de evento no download do certificado nem do arquivo de módulo. LACUNA; hipótese: o RH baixando certificado de outra pessoa registra `TREINAMENTOS`/`BAIXOU`; a própria pessoa e o arquivo de módulo não registram.
- Nome do download: LACUNA; hipótese `certificado-<título>.pdf`, limpo por `NomeParaDownload.Limpar` (Application/Arquivos/NomeParaDownload.cs:15).
- A pasta `certificados/` precisa de uma `IFonteDeReferenciasDeArquivo` (Application/Arquivos/IFonteDeReferenciasDeArquivo.cs:7-13), senão as sobras nunca são limpas; e com a fonte errada a limpeza **apagaria certificados válidos**: testar os dois lados.

**3. Módulos com arquivo.**
- Tipos e limites: C.2. Só `PDF` tem arquivo (até 10 MB, pelo conteúdo: `IdentificadorDeTipoDeArquivo`, assinatura `%PDF-`, Application/Arquivos/IdentificadorDeTipoDeArquivo.cs:12-24). "10 MB" lido como 10 MiB e o teto do pedido com folga, para o arquivo grande ser o 400 do contrato e não o 413 (o README registra a mesma escolha no holerite).
- "Campo que não pertence ao tipo é recusado" (`yaml:692`): `urlExterna` num `TEXTO`, `arquivo` num `VIDEO` etc. = 400 no campo. Campo de texto **vazio** de outro tipo num formulário multipart (o front costuma mandar `""`): LACUNA; hipótese: vazio = ausente.
- `PUT` sem `arquivo` num `PDF` mantém o arquivo; `PUT` que troca o tipo em rascunho apaga o arquivo antigo **depois** do commit; `PUT` com arquivo novo em treinamento publicado troca o arquivo de quem está no meio do treinamento (links antigos valem até 5 minutos: `Armazenamento:LinkValidoMinutos`, Infrastructure/Arquivos/OpcoesDeArmazenamento.cs:20).
- **O que a pessoa vê sem estar inscrita**: no opcional do público dela, o cartão e a **lista de módulos** (título, tipo, duração) pelo #21, com `situacao = DISPONIVEL`; o **conteúdo** (#24) e o **arquivo** (#25) dão 422 `INSCRICAO_PENDENTE`. Fora do público e sem linha: 404 em tudo. Com linha `INSCRITO` (obrigatório ou inscrito), o conteúdo abre **sem** precisar "Começar" (o contrato não condiciona o GET ao início; INFERIDO de `yaml:1307-1308`).
- Encerrado, com linha `INSCRITO` que nunca começou: #21 é 404 (`yaml:1118-1119`); #24 e #25 **LACUNA** (só declaram 404 e `INSCRICAO_PENDENTE`); hipótese: 404, igual ao #21.
- `urlExterna` só `http://` ou `https://` (barra `javascript:`); a API não visita o endereço. `conteudoTexto` e `descricao` "nunca HTML": a API guarda o texto como veio e o front mostra como texto ou markdown sanitizado (roadmap:75); conferir o que a fatia 4 fez com o `conteudo` do aviso e fazer igual.
- O caminho do armazenamento nunca sai (`yaml:1833`); o nome original vai só para a coluna e para o download.

**4. Onboarding e a ponte com o cadastro.**
- A porta já existe: `IInclusaoNoOnboarding` com `ConferirAsync(ct)` e `IncluirAsync(ids, ct)` (Application/Colaboradores/ICadastroDeFuncionarios.cs:59-67). A versão padrão `OnboardingIndisponivel` lança o 422 (Infrastructure/Colaboradores/CaixasIndisponiveis.cs:10-17) e está registrada em Infrastructure/DependencyInjection.cs:199; a fatia 6 registra a dela **depois** (o último `AddScoped` vale; foi assim com `EnvioDoFormularioDeAdmissao`, DependencyInjection.cs:200 e Infrastructure/Formularios/EnvioDoFormularioDeAdmissao.cs:16-49).
- Quem chama: o cadastro confere antes de gravar (Infrastructure/Colaboradores/CadastroDeFuncionarios.cs:100-103) e inclui depois dos convites (:147), **dentro da transação do chamador**; a readmissão confere (Infrastructure/Colaboradores/ReadmissaoDeFuncionarios.cs:160-163) e inclui (:106-109); a importação passa pelas mesmas opções (Application/Importacoes/ConfirmarImportacaoCasoDeUso.cs:65) e grava o número em `INCLUIDOS_NO_ONBOARDING` (:85).
- `IncluirAsync` devolve **quantas pessoas** foram incluídas (`incluidosNoOnboarding` = "pessoas inscritas", `25-fatia-2:460`), não quantas linhas: com dois treinamentos em vigor, 3 pessoas = 3.
- Quem está "Em admissão" (data futura) tem `Ativo = true` e o trigger aceita; a linha nasce `INSCRITO` e, se o treinamento de onboarding é obrigatório e está em andamento, **a pessoa já conta como pendência** antes do primeiro dia (LACUNA; hipótese: aceitar).
- Corrida: `ConferirAsync` passa e o RH encerra o treinamento antes de `IncluirAsync`: a inclusão não acha nenhum em vigor e tem de lançar o mesmo 422 (a transação do cadastro desfaz tudo). Não devolver 0 em silêncio.
- Readmitido que já tinha linha (`CONCLUIDO` de antes de sair): não muda, `jaEstavaIncluida`.
- Nenhum e-mail (`20-fatia-1:2868`). Auditoria da inclusão: o contrato não pede evento; LACUNA; hipótese: um evento `TREINAMENTOS`/`ALTEROU` por chamada do #19, e nenhum evento próprio dentro do cadastro e da importação (que já têm o deles).
- #4 devolve "em vigor" **inclusive agendado**; #19 inclui no agendado também ("assim o treinamento aparece para ela quando `comecaEm` chegar", `yaml:1005-1006`).

**5. `incluiAdmitidos` (a única tarefa).** LACUNAS: nome do tipo (o banco só exige `^[A-Z][A-Z0-9_]{2,49}$`, `V001:1483`); frequência; quem entra. Hipótese mais simples: tarefa diária (uma por dia, chave `...:dia=AAAA-MM-DD`, com `RecuperarAsync`), que para cada treinamento **obrigatório, publicado, não encerrado, com `INCLUI_ADMITIDOS`** insere `INSCRITO` para toda pessoa ativa, com admissão já ocorrida, **que está no público e não tem linha**. Isso cobre o admitido novo, o "Em admissão" que começou, o readmitido e **também quem mudou para um setor do público** (o contrato fala só de "quem for admitido": pergunta 3 de H.1). A caixa pode ser ligada depois de publicado (`yaml:367`): ao ligar, a tarefa inclui todo o atraso de uma vez. Ator = Sistema; um evento de auditoria por treinamento e dia com a contagem (lote, roadmap:74), sem nomes.

**6. Quem vê o quê.**
- **RH**: tudo, inclusive inativos (`yaml:842`) e o certificado de qualquer pessoa.
- **Gestor**: só a equipe (`IEscopoDoUsuario.ObterEquipeAsync`, Application/Escopo/IEscopoDoUsuario.cs:41; `DaEquipe`, Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs:13-18: **ativos, admissão começada, do setor, sem o gestor**), só números, situação, módulo atual e data; **nunca** certificado, código, conteúdo de módulo nem pessoas de outro setor; gestor sem setor: lista vazia no #28 e 404 no #29 e #30. A equipe é a de **hoje**: quem mudou de setor leva o histórico; o inativo com linha aparece para o RH e **some** para o gestor (LACUNA; hipótese: aceitar, é a definição única da fatia 1).
- **Pessoa**: só a própria linha, pelo `FuncionarioId` do token, nunca por id da requisição (Application/Escopo/IEscopoDoUsuario.cs:19-20); certificado de outra pessoa = 404.
- **Público**: C.5 e o item 2.
- "Alcançam pelo menos uma pessoa do setor" (#28): obrigatório = alguém da equipe com linha; opcional = alguém do setor no público (`yaml:1489-1490`, `:2229`). Opcional `TODOS` aparece para todo gestor.
- **Pendência e inativos** (LACUNA): a definição única conta por linha; uma pessoa inativada com linha `INSCRITO` continuaria como "pessoa com obrigatório pendente" para sempre (ninguém inativo consegue concluir). Hipótese: `pessoasComObrigatorioPendente` e os `pendentes` da confirmação contam **só pessoas ativas**; a lista "Quem fez" mostra todos. Pergunta 5 de H.1.

**7. Edição depois de publicado.**
- `PUT` do treinamento compara campo a campo com o valor atual; listas comparadas como **conjuntos**. Com `publico = TODOS` gravado e o corpo trazendo `setorIds` não vazio: 400 (lista do outro público) antes do 422.
- `comecaEm` no agendado pode ir para hoje ou para o passado (vira `EM_ANDAMENTO` na hora) e pode ser **anulado**? Publicado sem data não tem situação: hipótese: `comecaEm` nulo em publicado = 400.
- Corrigir módulo de treinamento **encerrado** (#13): o contrato só fala de "depois de publicado" e não declara `TREINAMENTO_ENCERRADO` nessa rota; quem estava em andamento ainda lê o conteúdo. LACUNA; hipótese: permitir a correção de conteúdo também no encerrado.
- Duas abas: publicar × editar × excluir módulo. Publicar por UPDATE condicional e reler; as rotas de módulo conferem "é rascunho" dentro da mesma transação da escrita.

**8. Listas e contas.**
- #1: `situacao` é calculada por data: o filtro e as `contagens` precisam do mesmo predicado SQL (D.3), com `hoje` em -03:00. Ordenar por `COMECA_EM` com rascunho sem data: posição do nulo é LACUNA (hipótese: por último em `DESC`). Desempate por id.
- #15: `ordenarPor = SITUACAO` (ordem alfabética do texto ou ordem lógica?) e `CONCLUSAO` (nulos) são LACUNA; `contagens.total` respeita `setorId` e `busca`.
- #16: nome do arquivo `treinamento-<título em minúsculas, sem acento, com hífens>.csv` (regra tirada de um exemplo só, `yaml:914`); `Início` = `DATA_INICIO`; `Conclusão` = dia de `concluidoEmUtc` em -03:00. Evento de auditoria da exportação: LACUNA (a exportação de formulário e a de auditoria têm o delas: fazer igual).
- #20: `todos` = `DISPONIVEL` + `NAO_INICIADO` + `EM_ANDAMENTO`; `disponiveis` = `DISPONIVEL` + `NAO_INICIADO`; o exemplo confere (`yaml:1105`: 2, 1, 1, 3). "Opcionais do público dela": público calculado **na hora** (setor de hoje); pessoa "Em admissão" ou inativa não entra em rota `/api/eu` na prática.
- #3: `ano` é o ano corrente em -03:00; `obrigatorios` conta agendado e encerrado, não rascunho.
- #5: ids que não existem simplesmente não contam (LACUNA; alternativa: 400); `publico = SETORES` sem `setorIds` = 0.
- A fatia 7 vai ler "treinamentos em andamento" com "concluíram de total" (`70-fatia-7:2386-2389`): deixar a consulta da lista reutilizável.

**9. Auditoria.** Eventos que o contrato **exige**: publicar (lote de linhas; ação não dita: `Publicou` existe, Domain/Auditoria/AuditoriaAcao.cs:16), encerrar (`ALTEROU`), excluir (`EXCLUIU`, com título e `pendentes`) (`yaml:526-527`, `:617-618`, `:477-479`). Área `AuditoriaArea.Treinamentos` já existe (Domain/Auditoria/AuditoriaArea.cs:18; rótulo em Application/Auditoria/RotulosDeAuditoria.cs:27). Criar, editar, módulos, inscrição, início e conclusão: o contrato não pede. LACUNA; hipótese: eventos explícitos para as ações do RH (criar, alterar, módulos) e para a **conclusão** (efeito de trigger: a linha do certificado; roadmap:74), nenhum para iniciar e para cada módulo (ruído). Entidades sem `[Auditado]`, para o lote de publicação não gerar centenas de eventos.

**10. Volume.** Publicar para `TODOS` cria uma linha por pessoa numa transação (101 no elenco; `funcionarioIds` até 500). Um INSERT por linha pelo EF funciona; um `INSERT ... SELECT` é mais simples de tornar atômico com o filtro de ativos, mas passa por fora do interceptor (por isso o evento explícito).

**11. Elenco** (`ELENCO.md:9`): Onboarding 2026 = 1, LGPD na prática = 2, Segurança da informação = 3. Os exemplos do contrato usam esses ids; os testes de conformidade comparam forma, não valores.

---

## F. Infraestrutura: o que reutilizar e o que é novo

### F.1 Reutilizar

| Peça | Onde | Como entra na 6 |
|---|---|---|
| `GrupoRh`, `GrupoGestor`, `GrupoEu`, `GrupoPublico` | Api/Autorizacao/FamiliasDeRota.cs:10-19 | as quatro famílias; registrar por `operationId`, nunca por prefixo (aviso em Api/Colaboradores/EquipeEndpoints.cs:10 e Api/Ponto/PontoGestorEndpoints.cs:13: `/api/gestor/equipe/{funcionarioId}/` é de várias fatias) |
| `LimiteDeTentativasExtensions` (políticas por IP em janela fixa; `OnRejected` com `Retry-After` e o texto do contrato) | Api/Configuracao/LimiteDeTentativas.cs:9-18, :32-87 | molde da política nova da conferência (10 por minuto), configurável; uso em Api/Formularios/FormulariosPublicoEndpoints.cs:33 |
| `ProduzProblemas(params int[])` | Api/Configuracao/TextosDeErro.cs:43 | declarar os erros de cada rota |
| `ProblemaDeApiException(status, titulo, detalhe, codigo, extensoes, cabecalhos)` | Application/Comum/ProblemaDeApiException.cs:12-31 | `CONFIRMACAO_NECESSARIA` (três extras), `TREINAMENTO_COM_CERTIFICADOS` (`certificadosEmitidos`), `TREINAMENTO_ENCERRADO` (título próprio), `FUNCIONARIO_INATIVO` (`regraDoBanco`), 404 da conferência; molde em Application/Formularios/Mensagens/MensagensDosFormularios.cs:81 |
| `ErroDeNegocioException`, `ConflitoException`, `ErroDeValidacaoException`, `ColetorDeErros`, `NaoEncontradoException`, `OuNaoEncontrado()` | Application/Comum/Excecoes.cs:6-77 | 422 de título genérico, 400 com todos os campos, 404 |
| Catálogo de mensagens + teste contra o contrato | Application/Formularios/Mensagens/MensagensDosFormularios.cs; tests/.../Contrato/MensagensDosFormulariosTests.cs | molde de `MensagensDosTreinamentos` |
| Tradutor de recusa do banco (45000 pela mensagem, 1062 pelo nome da chave) e `ErrosDoBanco` | Infrastructure/Formularios/TradutorDeRecusaDosFormularios.cs; Infrastructure/Persistencia/ErrosDoBanco.cs:15-55 | as três mensagens de trigger e as chaves únicas de B.2 |
| Multipart: `FormulariosDoContrato` (lista de `operationId` com corpo de formulário), `LeituraDeFormulario.LerAsync`/`Arquivo`, `RequestSizeLimitAttribute` + `RequestFormLimitsAttribute` + `.DisableAntiforgery()` | Api/Configuracao/FormulariosDoContrato.cs:18-38; Api/Comum/LeituraDeFormulario.cs:13, :37; Api/Formularios/FormulariosPublicoEndpoints.cs:39-40 | #11 e #13 (acrescentar os dois `operationId`) |
| `ArquivoRecebido`; `ValidacaoDeArquivoDeDocumento.ValidarPdfAsync(..., tamanhoMaximoBytes, mensagemDoLimite, ...)`; `EnvioDeArquivoDeDocumento.GravarEEfetivarAsync(arquivo, pasta, tipo, efetivar)` | Application/Documentos/DocumentosDtos.cs:34; Application/Documentos/ArquivoDeDocumento.cs:25, :82-89 | PDF do módulo até 10 MB pelo conteúdo; gravar e efetivar com a receita do arquivo (pasta `treinamentos`) |
| `IArmazenamentoDeArquivos` (`GravarAsync`, `CriarLinkTemporarioAsync`, `ApagarAsync`), `CaminhosDeArquivo.Novo`, `NomeParaDownload.Limpar`, `LinkTemporario` | Application/Arquivos/IArmazenamentoDeArquivos.cs:4-27; CaminhosDeArquivo.cs:23; NomeParaDownload.cs:15 | arquivo de módulo (#25) e certificado (#17, #27); a interface já previa "módulos" (:10) |
| `IFonteDeReferenciasDeArquivo` + `LimparArquivosOrfaosTarefa` | Application/Arquivos/IFonteDeReferenciasDeArquivo.cs:7-13; registro das outras em Infrastructure/DependencyInjection.cs:164, :189, :253, :277, :296 | uma fonte para `treinamentos/` e outra para `certificados/` (ou um prefixo só com as duas tabelas) |
| **PDF**: `PDFsharp-MigraDoc`, `FonteDoPdf` (Noto Sans embutida), `GeradorDePdfDoEspelho` (montagem em memória), `BaixarEspelhoEmPdfCasoDeUso` (gerar, gravar, auditar, link) | Infrastructure/Ponto/FonteDoPdf.cs:10-49; GeradorDePdfDoEspelho.cs:15-29; Application/Ponto/BaixarEspelhoEmPdfCasoDeUso.cs:17-20, :54-76; DependencyInjection.cs:252 | molde do gerador do certificado e do download |
| PdfPig nos testes | `Directory.Packages.props:17`; tests/.../Ponto/EspelhoEmPdfTests.cs | conferir nome com acento, número e código dentro do PDF |
| `IAgendador`, `ITarefaAgendada` (`Tipo`, `ExecutarAsync`, `RecuperarAsync`), `ChaveDeIdempotencia.Montar` | Application/Agendamento/IAgendador.cs:7-34; ITarefaAgendada.cs:12-21; Application/Comum/ChaveDeIdempotencia.cs:26; registro em DependencyInjection.cs:137-145, :280-284 | a tarefa de admitidos; molde diário em Infrastructure/Formularios/ApagarCandidatosVencidosTarefa.cs |
| `IAuditor` (`Registrar` na transação do fato; `RegistrarEGravarAsync` fora; `SuspenderRegistroAutomatico`), `AuditoriaArea.Treinamentos`, `AuditoriaAcao` (`Criou`, `Alterou`, `Excluiu`, `Publicou`, `Baixou`) | Application/Auditoria/IAuditor.cs:28-52; Domain/Auditoria/AuditoriaArea.cs:18; AuditoriaAcao.cs:4-20 | eventos explícitos (E.4.9) |
| `IEscopoDoUsuario`, `EquipeDoGestor`, `DaEquipe(e)` | Application/Escopo/IEscopoDoUsuario.cs:14-41; Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs:13-18 | #28, #29, #30 e o `FuncionarioId` de `/api/eu` |
| `IAtorAtual` (Sistema nas tarefas; `Ip`) | Application/Escopo/IAtorAtual.cs:18-39 | a tarefa de admitidos grava como Sistema |
| `IFeriasDoPonto.NoPeriodoAsync` | Application/Ponto/ICalendarioDoPonto.cs:27-33 | `emFeriasAte` do #29 (a fatia 5 usou a mesma porta na `observacao` do gestor) |
| `IFonteDeIndicadorDaFicha` + `IndicadorDaFicha.TreinamentosPendentes`; leitor em `ObterIndicadoresDaEquipeCasoDeUso` | Application/Colaboradores/IFonteDeIndicadorDaFicha.cs:4-21; ObterIndicadoresDaEquipeCasoDeUso.cs:19; registros das outras em DependencyInjection.cs:254, :293 | a 6 registra a fonte: o valor é o `resumo.pendentes` do #30 (`20-fatia-1:890`, `:3044`); testes com fonte falsa em tests/.../Equipe/IndicadoresDaEquipeTests.cs:60-87 |
| **`IInclusaoNoOnboarding`** | Application/Colaboradores/ICadastroDeFuncionarios.cs:59-67; padrão em Infrastructure/Colaboradores/CaixasIndisponiveis.cs:10-17; DependencyInjection.cs:199 | a 6 registra a real depois da padrão (E.4.4) |
| `ParametrosDeLista`, `RespostaPaginada<T>`, `MapaDeOrdenacao`, `EnumsDeConsulta`, `ToRespostaPaginadaAsync`, `BuscaLike.Contem`, `EnumsDeParametrosDoContrato` | Application/Comum/Consultas/ParametrosDeLista.cs:18-36; RespostaPaginada.cs:7-10; MapaDeOrdenacao.cs:10-26; Infrastructure/Persistencia/ConsultasEf.cs:13, :31; Api/Configuracao/EnumsDeParametrosDoContrato.cs:27 | #1, #15, #20 |
| `CsvSeguro.Texto/Escrever`, `ArquivoExportado` | Application/Comum/Exportacao/CsvSeguro.cs:18, :35 | #16 |
| `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`, `OperacoesComTrava` (o jeito de travar uma linha) | Application/Autenticacao/Contratos.cs:34-45; Infrastructure/Colaboradores/OperacoesComTrava.cs:21-47 | concluir, excluir, publicar, reordenar |
| `IRelogio`, `FusoBrasil` (`Hoje`, `DiaLocalDe`, `InicioDoDiaEmUtc`, `ParaLocal`), `ConversoresDeTempo.Brasil` | Application/Autenticacao/Contratos.cs:76; Application/Comum/Tempo/FusoBrasil.cs:13-44; Infrastructure/Persistencia/ConversoresDeTempo.cs:19 | situação por data, `encerradoEmUtc`, `concluidoEmUtc`, datas do CSV |
| `FuncionarioResumo`; `Funcionario.Ativo`, `DataAdmissao`, `Situacao(hoje)` | Application/Colaboradores/FuncionarioResumo.cs; Domain/Autenticacao/Funcionario.cs:23-26, :147 | listas; "ativo com admissão já ocorrida" |
| `App:UrlDoFront` | Api/appsettings.json:65; Infrastructure/Emails/OpcoesDeEmail.cs:73-89 | endereço da página de conferência no PDF |
| Sonda já escrita para `TREINAMENTOS` (`EMITE_CERTIFICADO = false`, caixas `FALSE`) | tests/.../Persistencia/SondaMapeamentoDosScriptsTests.cs:72-82, :249-266, :326, :483-487 | o mapeamento real copia as escolhas |
| Testes: `ApiFactory` (relógio fixo, armazenamento em memória, agendador desligado), um banco por classe, `AfirmarConformeAoContratoAsync`, `MatrizDePerfis`, `ContratoDivergenciaTests.OperationIdsNoEscopo`, `ContratoEStatusTests`, `MapeamentoContraBancoTests`, `ConformidadeDoModeloTests` | tests/.../Infra/ApiFactory.cs; Matriz/MatrizDePerfis.cs; Contrato/ContratoDivergenciaTests.cs:19, :184-204; Contrato/ContratoEStatusTests.cs; Persistencia/ | o mesmo jeito das fatias 4 e 5 (README, "Testes" e "O que o gerador de OpenAPI escreve") |

A fila de e-mail (`IFilaDeEmails`, Application/Emails/IFilaDeEmails.cs) **não entra**: a fatia não envia e-mail. Os contadores do menu também não: não há bolinha de treinamentos (Application/Painel/ContadorDoMenu.cs:7-15).

### F.2 Novo (não existe)

- **Entidades e mapeamentos** das seis tabelas (`Domain/Treinamentos`, `Infrastructure/Persistencia/Configuracoes`) e a retirada delas de `tabelas-sem-mapeamento.txt`.
- **Regras puras**: situação do treinamento, validação de `TreinamentoComando` (rascunho × publicar), campos travados, módulo por tipo, pendência, visibilidade para a pessoa, progresso (`modulosConcluidos`, percentual, `moduloAtual`, `duracaoTotalMinutos`), `TreinamentoMinhaSituacao`, abas, número do certificado, nome do CSV.
- **Catálogo de erros** `MensagensDosTreinamentos` + teste contra o contrato; tradutor de recusa; os problemas com extras.
- **Público do treinamento** como porta única (quem é do público agora: `TODOS`, setores, pessoas; ativos com admissão ocorrida), usada por #5, #9, #20, #21, #22, #28 e pela tarefa.
- **Gerador de PDF do certificado** e o serviço que emite (trocar o código, gerar, gravar, guardar o caminho; sob demanda no download).
- **Política de limite** da conferência pública (10 por minuto por IP; valor alto no `ApiFactory`).
- **Tarefa** de inclusão de admitidos (nome a escolher; **não** `LIBERAR_TREINAMENTO`).
- **Implementação de `IInclusaoNoOnboarding`**, a fonte de `TreinamentosPendentes` e as fontes de referência de arquivo.
- **Endpoints**: `TreinamentosRhEndpoints`, `TreinamentosRhAcompanhamentoEndpoints`, `TreinamentosEuEndpoints`, `TreinamentosGestorEndpoints`, `CertificadosPublicoEndpoints` (ids com `:int`; `{codigoValidacao}` com formato que produza o **400** declarado).
- **STATUS.md**: 31 linhas novas, a linha 15 (fatia 6) e a correção das linhas 46 e 77.
- **README**: seção "Treinamentos (fatia 6)" no molde das fatias 4 e 5.

---

## G. Proposta de incrementos e tarefas

O roadmap dá 6a "treinamentos e módulos", 6b "progresso e certificado", 6c "lembretes" (roadmap:235). O 6c do roadmap foi cortado pelo contrato (E.3.1); proponho usar o nome para o que sobra: acompanhar, equipe, onboarding e admitidos. Cada tarefa é pequena e segue o ciclo das fatias 4 e 5: teste vermelho, implementar, verde, `operationId` em `OperationIdsNoEscopo`, linha no `STATUS.md`, `AfirmarConformeAoContratoAsync` nos 2xx e nos erros, matriz de perfis, commit. **[S]** = pede revisão de segurança.

### 6a. Construtor do RH (#1, #2, #3, #5 a #14)

**T1. Mapeamento das seis tabelas e sonda dos triggers.** Entidades sem `[Auditado]`, conversores -03:00, enums em texto, `CHAVE_DESTINO` calculada, sem padrão do banco nas duas colunas `TRUE`, `NOTA`/`DATA_FIM`/caixas mortas fora do modelo. Entrega: `MapeamentoContraBancoTests` e `ConformidadeDoModeloTests` verdes; uma sonda que grava pelo EF uma linha `INSCRITO`, passa a `CONCLUIDO` e lê a linha de certificado criada pelo trigger (v1, `DATA_EMISSAO`), e que prova as três recusas de trigger de D.2.

**T2. Regras puras.** A lista de F.2. Testes de unidade; alvo da mutação.

**T3. Erros da fatia 6.** `MensagensDosTreinamentos` (B.1) com teste contra o contrato e a lista dos textos sem exemplo (singulares, 400 de campo); tradutor; fábricas dos problemas com extras e do título próprio. Entrega: os três campos do 409 e `certificadosEmitidos` saem no JSON.

**T4. Criar, obter, atualizar e contar o público** (#2, #6, #7, #5). Rascunho com só o título; substituição total; público trocado na mesma transação; campos travados; encerrado. A porta do público. **[S]** leve (ids de outras tabelas).

**T5. Módulos** (#11, #12, #13, #14). Multipart, validação por tipo, PDF pelo conteúdo, receita do arquivo, troca de tipo, reordenação em duas fases, renumeração, travas depois de publicado, fonte de referências de `treinamentos/`. Entrega: mover o módulo 3 para a posição 1 sem 1062; arquivo `.exe` renomeado para `.pdf` = 400 e nenhum blob; excluir módulo PDF apaga o blob.

**T6. Publicar** (#9). Validação com `errors` por campo, UPDATE condicional, linhas em lote para o obrigatório (ativos com admissão ocorrida), evento explícito. Entrega: publicar duas vezes em paralelo = um 200 e um 422, sem linha duplicada; inativo e "Em admissão" não recebem linha.

**T7. Encerrar e excluir** (#10, #8). Confirmação com os três números, 422 de certificado antes do 409, trava do treinamento, ordem dos filhos, blobs depois do commit, eventos. Entrega: excluir treinamento que não emite, com concluídos, apaga as linhas de certificado do trigger e não deixa nada no banco nem no armazenamento.

**T8. Lista e indicadores** (#1, #3). Situação por data no SQL, contagens, números por linha, `certificadosEmitidos`.

**Mutação do incremento 6a** (T2 a T8), no jeito das fatias 4 e 5 (spec gerado, `mutar-em-paralelo.mjs` com até 10 workers, em segundo plano e vigiado; um teste por sobrevivente real; equivalentes no ledger).

### 6b. Fazer o treinamento e certificar (#17, #20 a #27, #31)

**T9. Meus treinamentos e detalhe** (#20, #21). A regra de visibilidade num lugar só; abas, contagens, ordem fixa; `DISPONIVEL` pelo público de hoje; encerrado some para quem não começou. **[S]** (só o próprio).

**T10. Inscrever e iniciar** (#22, #23). Idempotentes; 1062 vira "já estava"; nenhum UPDATE em `CONCLUIDO`; encerrado.

**T11. Conteúdo e arquivo do módulo** (#24, #25). 404 × 422 `INSCRICAO_PENDENTE`; módulo de outro treinamento = 404; link temporário como anexo. **[S]**

**T12. Concluir módulo** (#26), sem o PDF. Trava da linha da pessoa, progresso, início implícito, `CONCLUIDO`, troca do código para v4 e da `DATA_EMISSAO` na mesma transação, evento da conclusão, resposta com `treinamentoConcluido`. Entrega: os dois últimos módulos concluídos em paralelo = um `CONCLUIDO`, uma linha de certificado, código v4; repetir não muda nada; treinamento que não emite conclui sem `certificado` na resposta. **[S]**

**T13. PDF e downloads do certificado** (#27, #17). Gerador (MigraDoc, fonte embutida), emissão depois do commit, geração sob demanda com UPDATE condicional, fonte de referências de `certificados/`, 404 de quem não concluiu, de outra pessoa e de treinamento que não emite. Entrega: PdfPig lê no PDF um nome com acento, o `numero` e o código; dois downloads simultâneos sem arquivo deixam um caminho só. **[S]**

**T14. Conferência pública** (#31). `GrupoPublico`, política de 10 por minuto, 400 de formato, o mesmo 404 nos três casos (e para código v1), resposta com quatro campos. **[S]** (rota pública)

**Mutação do incremento 6b** (T9 a T14).

### 6c. Acompanhar, equipe, onboarding e admitidos (#4, #15, #16, #18, #19, #28, #29, #30)

**T15. Quem fez e exportação** (#15, #16). Filtros, ordenações, contagens, `concluidoEmUtc` pelo progresso, CSV neutralizado.

**T16. Ficha do RH e rotas do gestor** (#18, #28, #29, #30) e a fonte do indicador `treinamentosPendentes`. Entrega: `resumo.pendentes` do #30 igual ao indicador da ficha da equipe; outro setor e o próprio gestor = 404; nenhum campo de certificado nas respostas do gestor. **[S]**

**T17. Onboarding** (#4, #19) e a ponte. Implementação de `IInclusaoNoOnboarding` registrada depois da padrão; cadastro, readmissão e importação passam a incluir de verdade, na mesma transação. Entrega: os quatro testes de recusa continuam verdes e ganham o caminho feliz; `incluidosNoOnboarding` conta pessoas; treinamento encerrado entre conferir e incluir desfaz o cadastro.

**T18. Tarefa de admitidos.** Diária, idempotente, com recuperação; só obrigatório, publicado, não encerrado, com a caixa; evento do Sistema com a contagem. Entrega: rodar duas vezes não duplica; pessoa admitida ontem entra; treinamento encerrado não recebe ninguém. Espera a resposta da pergunta 3.

**T19. Fechamento.** Os 31 `operationId` no escopo, STATUS.md (inclusive as linhas 15, 46 e 77), matriz de perfis, README, **mutação do incremento 6c** (T15 a T18), revisão de segurança (rota pública, escopo do gestor, arquivos).

**Ordem e dependências:** T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8 (fim da 6a) → T9 → T10 → T11 → T12 → T13 → T14 (fim da 6b) → T15 → T16 → T17 → T18 → T19. T13 depende de T12; T14 e o #17 dependem de T13; T15 e T16 só precisam de T1 a T6 e de T12 para ter dado concluído (os testes podem semear linhas direto no banco); **T17 só depende de T6** e pode subir para o fim da 6a se o dono quiser a caixa do cadastro cedo; T18 depende de T4 (público) e de T6. Uma rodada de mutação por incremento, antes do PR de cada um (um PR por incremento, como nas fatias anteriores).

---

## H. Perguntas ao dono e matriz de testes

### H.1 Perguntas (só as que mudam o que se constrói)

| # | Pergunta | Hipótese mais simples (a que se constrói sem resposta) | Custo se errada |
|---|---|---|---|
| 1 | **Vídeos:** só link externo, ou a empresa envia o arquivo do vídeo? | só link externo (`yaml:698`) | alto: upload grande (limite, tempo, custo de armazenamento), tipo de arquivo novo, `VIDEO` no #25; mexe em T5 e T11 |
| 2 | **Onboarding:** o RH marca com a caixa `onboarding`, pode haver vários ao mesmo tempo e a pessoa entra em todos? | sim (`yaml:262`) | médio: muda #4, #19, a validação do comando e a ponte (T17) |
| 3 | **"Incluir quem for admitido depois":** entra só quem é admitido depois da publicação, ou qualquer pessoa ativa que passe a estar no público e não tenha linha (quem mudou de setor, quem foi readmitido)? Uma vez por dia basta? | qualquer pessoa ativa, admitida, no público e sem linha; diária | médio: gente a mais ou a menos como pendente; mexe só em T18 |
| 4 | **Certificado:** o que o PDF mostra (empresa, carga horária, endereço de conferência) e o título do treinamento pode mudar depois de emitido (o PDF guarda o antigo e a conferência mostra o novo)? | modelo mínimo (E.4.2); o título continua editável | baixo no layout (troca o gerador); médio se o dono quiser travar o título ou guardar o título da emissão (não há coluna) |
| 5 | **Pessoa inativada com treinamento obrigatório não concluído** conta como pendente (indicador do RH e aviso de confirmação)? E quem está "Em admissão" e foi incluído no onboarding? | inativo não conta; "Em admissão" conta | baixo: um filtro nas contagens |
| 6 | **Conferência pública de quem saiu da empresa:** o nome continua aparecendo para quem tem o código? | sim (o contrato diz que o código continua válido) | baixo no código; é decisão de privacidade |
| 7 | **Depois de publicado:** fica como está (só título, descrição, a caixa de admitidos e a data enquanto agendado; módulos só com correção de conteúdo, inclusive no encerrado)? | sim (`yaml:377`, `:698`) | alto se liberar estrutura: módulo novo não reabre quem concluiu; rota nova para acrescentar pessoas |
| 8 | **Baixar certificado e arquivo de módulo entra no log de auditoria?** | só o RH baixando certificado de outra pessoa | baixo |

As demais hipóteses (E.2 e as LACUNAS de E.4) seguem como estão até o dono dizer o contrário; o executor as registra no plano.

### H.2 Matriz de testes obrigatórios

| Caso | O que prova | Origem |
|---|---|---|
| Concluir o treinamento sem terminar os módulos (n-1 módulos) | continua `EM_ANDAMENTO`, sem certificado | roadmap:250 |
| Último módulo concluído | `CONCLUIDO`, `treinamentoConcluido: true`, uma linha de certificado, **código v4**, `numero` `CERT-{ano}-{id}` | `yaml:1371-1378` |
| Os dois últimos módulos em paralelo; o mesmo módulo duas vezes | um `CONCLUIDO`, um certificado; nenhum 500; a repetição devolve o estado | `yaml:1380-1382` |
| Repetir iniciar e concluir depois de `CONCLUIDO` | 200, nenhuma escrita (o trigger recusaria) | `yaml:1235-1236` |
| Primeira conclusão em `INSCRITO` | inicia (`EM_ANDAMENTO`, `iniciadoEm` = hoje) | `yaml:1379` |
| Treinamento com `emiteCertificado` falso: concluir | sem PDF, `certificado: null`, download 404, o código da linha do trigger **não valida**, `certificadosEmitidos: 0` | roadmap:250; `yaml:1594` |
| PDF do certificado | lido pelo PdfPig: nome com acento, título, número, código; anexado à **linha do trigger** (nenhuma linha nova) | roadmap:250 |
| Download com a linha sem arquivo (geração falhou na conclusão); dois ao mesmo tempo | gera na hora; um caminho só | `yaml:936-937` |
| Conferência: código inexistente, de treinamento que não emite, v1 não trocado | o **mesmo** 404 (corpo idêntico) | `yaml:1594-1595` |
| Conferência: texto que não é UUID | 400 | `yaml:1620` |
| Conferência: 11ª consulta no minuto pelo mesmo IP | 429 com `Retry-After`; outro IP passa | `yaml:1592` |
| Conferência: resposta válida | exatamente quatro campos | `yaml:2290-2298` |
| Certificado de outra pessoa (`/api/eu`) | 404 | roadmap:250; `yaml:1457` |
| Gestor: treinamento que não alcança o setor, rascunho, pessoa de outro setor, o próprio gestor, gestor sem setor | 404 ou lista vazia; nenhum campo de certificado | roadmap:250; `yaml:1520-1521`, `:1559` |
| `treinamentosPendentes` da ficha da equipe | igual ao `resumo.pendentes` do #30 | `yaml:1557` |
| Publicar: sem módulo, sem data, `SETORES` sem setor, `PESSOAS` sem pessoa | 400 com `errors` por campo (`modulos`, `comecaEm`...) | `yaml:514-517` |
| Publicar obrigatório para `TODOS` com um inativo e um "Em admissão" | linhas só para ativos com admissão ocorrida; evento de auditoria com a contagem | `yaml:38-39`, `:526-527` |
| Publicar opcional | nenhuma linha; aparece como `DISPONIVEL` para o público | `yaml:522` |
| Publicar duas vezes (em sequência e em paralelo); publicar o encerrado | 422 `TREINAMENTO_JA_PUBLICADO`; sem linha duplicada | `yaml:528` |
| Treinamento agendado | não aparece em `/api/eu`; aparece para o RH e o gestor; na data vira `EM_ANDAMENTO` sem tarefa | `yaml:519-520`, `:1062-1063` |
| `PUT` publicado mudando `publico`, `obrigatorio`, `onboarding`, `emiteCertificado`; `comecaEm` em agendado e em andamento | 422 `TREINAMENTO_CAMPO_TRAVADO`; a data só muda no agendado | `yaml:367-371` |
| `PUT` em encerrado | 422 `TREINAMENTO_ENCERRADO` com o título próprio | `yaml:371`, `:443` |
| Módulos em publicado: acrescentar, reordenar, excluir; trocar o tipo; corrigir o conteúdo | 422 `MODULOS_TRAVADOS`; 422 `CAMPO_TRAVADO`; 200 | `yaml:693`, `:775` |
| Módulo: campo de outro tipo; URL sem `http`; PDF de 11 MB; `.exe` renomeado | 400 no campo; nenhum blob | `yaml:689-692` |
| Reordenar com id faltando, repetido, de outro treinamento | 400 | `yaml:734-735` |
| Encerrar: rascunho, agendado, já encerrado; com pendentes sem e com `confirmar` | 422 certos; 409 com os três números; 200 | `yaml:594-602` |
| Depois de encerrar: quem estava `EM_ANDAMENTO` conclui e recebe certificado; quem estava `INSCRITO` | conclui (`encerrado: true` no cartão); some da lista, 404 no detalhe, 422 em iniciar e concluir | `yaml:605-610` |
| Depois de encerrar: inscrição em opcional; onboarding; tarefa de admitidos | 422; sai do #4 e do #19; ninguém entra | `yaml:611-613` |
| Excluir com certificado emitido, com e sem `confirmar` | 422 `TREINAMENTO_COM_CERTIFICADOS` com o número; nada apagado | `yaml:457-462` |
| Excluir rascunho e agendado; em andamento com pendentes; treinamento que não emite com concluídos | 204 direto; 409 e depois 204; 204 sem sobra no banco nem no armazenamento; evento `EXCLUIU` | `yaml:463-479` |
| Excluir × concluir ao mesmo tempo | um efeito só; nenhum 500 | `yaml:473-474` |
| Opcional: abrir conteúdo, baixar, iniciar e concluir sem inscrição | 422 `INSCRICAO_PENDENTE` | `yaml:1237`, `:1308`, `:1383` |
| Inscrição: em obrigatório; fora do público; repetida; em duas abas | 422 `TREINAMENTO_NAO_E_OPCIONAL`; 404; 200 sem mudar; uma linha | `yaml:1162-1164` |
| Módulo de outro treinamento na rota | 404 | `yaml:1385` |
| Meus treinamentos: abas e contagens | `TODOS` sem concluídos; `contagens` ignora `aba` e respeita `busca` e `obrigatorio`; ordem fixa | `yaml:1067-1071` |
| Cartão: 3 de 5 módulos; nenhum módulo com duração | 60%, `moduloAtual` 4; `duracaoTotalMinutos: null` | `yaml:2138-2141` |
| Onboarding (#19): pessoa nova, repetida, inativa, sem treinamento em vigor, com dois em vigor | inclui; `jaEstavaIncluida`; 422 com `regraDoBanco`; 422; entra nos dois | `yaml:1004-1010` |
| Cadastro, readmissão e importação com `incluirNoOnboarding`: com e sem treinamento em vigor | inclui na mesma transação e conta pessoas; 422 e **ninguém** cadastrado | `20-fatia-1:153-157`; `25-fatia-2:229-231`, `:460` |
| Tarefa de admitidos: rodar duas vezes; API parada por dias; caixa desligada; opcional | sem duplicar; recupera; não inclui | `yaml:522-523` |
| Quem fez: filtros, ordenações, inativo na lista, rascunho | contagens conforme `yaml:839`; inativo com `ativo: false`; lista vazia | `yaml:836-846` |
| Exportação com nome começando por `=`, `+`, `-`, `@` | célula neutralizada; BOM; `;`; anexo com o nome do contrato | roadmap:78; `yaml:896-898` |
| Indicadores: obrigatório encerrado e agendado; certificado de outro ano; a mesma pessoa pendente em dois treinamentos | conta em `obrigatorios`; fora do ano; uma pessoa | `yaml:223-229` |
| Rascunho nasce com `EMITE_CERTIFICADO = false` quando pedido; `incluiAdmitidos = false` | relido do banco | `yaml:167-168`; roadmap:81 |
| Arquivo órfão depois de recusa do banco | apagado na hora ou pela limpeza; a limpeza **não** apaga módulo nem certificado em uso | roadmap:76 |
| Matriz de perfis das 31 rotas | 403 nas famílias erradas; a pública nunca 401/403 | `x-perfis` |

---

## O que mais pode dar errado

As condições que o contrato não cobre e que mais machucam: **(1) a conclusão em corrida:** sem travar a linha da pessoa antes de contar o progresso, duas abas nos dois últimos módulos deixam a pessoa em 100% e sem `CONCLUIDO` para sempre, e nenhum teste sequencial pega; **(2) o código v1 que escapa:** se a troca para v4 não estiver na mesma transação do `CONCLUIDO` (ou se uma linha antiga existir), um código quase sequencial passa a valer na rota pública e permite enumerar certificados e nomes; **(3) as datas do trigger:** `CURDATE()` do banco contra o relógio da aplicação dá teste que passa hoje e quebra na virada do ano, e conclusão recusada por `CHECK` se os relógios divergirem; **(4) a limpeza de sobras:** uma fonte de referências errada para `certificados/` ou `treinamentos/` apaga certificado válido ou PDF de módulo em uso um dia depois, em silêncio; e a ausência dela deixa lixo; **(5) a ponte do onboarding dentro da transação do cadastro:** qualquer exceção nova em `IncluirAsync` (trigger de inativo, chave repetida, treinamento encerrado no meio) desfaz o cadastro ou a importação inteira de até 500 pessoas, então cada recusa precisa virar o erro do contrato e não um 500. Fora isso: o `PUT` que "substitui tudo" com um front que omite um campo travado (o padrão do campo difere do valor gravado e vira 422 inexplicável); o padrão `true` de `emiteCertificado` e `incluiAdmitidos` no JSON (campo omitido precisa virar `true`, não `false`); publicar para 500 pessoas dentro de um pedido HTTP; a equipe do gestor calculada hoje sobre histórico antigo; e o front sem tela para a execução e para a conferência, que só vai exercitar essas rotas no fim.
