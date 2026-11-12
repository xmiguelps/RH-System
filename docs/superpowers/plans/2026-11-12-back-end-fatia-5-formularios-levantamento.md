# Levantamento da fatia 5 (formulários) do back-end (anexo do plano da fatia 5)

Feito em 2026-11-12, na branch `feat/fatia-5-formularios` (criada da `main` com as fatias 0 a 4), só lendo. Fontes: `contrato/fontes/50-fatia-5-formularios.yaml` inteiro (citado como `yaml:N`), `00-base.yaml` (`base:N`), `CONVENCOES.md`, `PROVISORIOS.md`, `STATUS.md`, `ELENCO.md`, trechos de `20-fatia-1`, `25-fatia-2` e `70-fatia-7`; roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, citado `roadmap:N`); `Banco de Dados/Scripts/V001`, `V002` e `LEIA-ME.md` (`V003` a `V005` não tocam formulário); `back-end/src` e os apoios de teste. Caminhos de código relativos a `back-end/src/`. **INFERIDO** = conclusão minha, não lida nem medida. **LACUNA** = nenhuma fonte responde; o executor decide e registra, ou pergunta ao dono (seção H). O contrato, as convenções, o banco e o roadmap foram lidos diretamente; as citações de linha do código e dos testes (E.3, E.4, F) vieram de uma varredura delegada e só uma amostra foi reconferida (a porta `IEnvioDoFormularioDeAdmissao`, o interceptor de auditoria, o limitador e `tabelas-sem-mapeamento.txt`): conferir a linha ao usar.

O contrato é a autoridade. Onde o roadmap diverge, vale o contrato e a divergência está em E.3.

---

## A. As 32 operações

Perfis: F = Funcionario, G = Gestor, RH. `[F,G,RH]` = família `/api/eu` (a própria pessoa; nunca 403). **PUB** = `/api/publico`, sem login (`security: []`, `x-perfis: [Publico]`). Paginação = `pagina` (≥1, padrão 1) e `tamanhoPagina` (1-100, padrão 20) (`base:25-34`). Toda operação tem 500; todas menos as duas públicas têm 401; não repito. `{formularioId}`, `{perguntaId}`, `{funcionarioId}`, `{candidatoId}`, `{respostaId}` são inteiros ≥ 1 (`yaml:2400-2429`). Os nomes `Erro409...`/`Erro422...` são os grupos de resposta do contrato (códigos de cada grupo em B).

| # | Rota | operationId | Perfil | Entrada | 2xx | Erros (além de 401/500) | Efeito declarado |
|---|---|---|---|---|---|---|---|
| 1 | GET `/api/rh/formularios` | listarFormulariosRh | RH | paginação, `busca` (título), `tipo` (`FormularioTipo`), `situacao` (`FormularioSituacao`), `ordenarPor` (`criadoEm`\|`titulo`\|`prazo`, padrão `criadoEm`), `direcao` | 200 `FormularioRhLista` | 400, 403 | ordem padrão: mais recentes primeiro; `contagemPorTipo` respeita `busca` e `situacao` e **ignora** `tipo`; avaliação conta **avaliações**; por link e Sugestão `esperados`/taxa `null`; anônimo não encerrado `respondidos`/taxa `null` e só `esperados` exato (`yaml:74-102`) |
| 2 | POST `/api/rh/formularios` | criarFormulario | RH | `FormularioDetalhesComando` | 201 `FormularioDetalheRh` | 400, 403 | nasce `RASCUNHO` com **`ATIVO = falso` explícito**; `lembreteAutomatico: true` gravado pela API; Sugestão anônima = 400 em `errors.anonimo`; `sentidoAvaliacao` obrigatório em `AVALIACAO` e proibido nos outros (`yaml:283-304`, exemplo `:343`) |
| 3 | GET `/api/rh/formularios/{formularioId}` | obterFormularioRh | RH | — | 200 `FormularioDetalheRh` | 403, 404 | qualquer situação; `temRespostas` `null` em anônimo **em qualquer situação**; `tokenDoLink` só depois de publicar/agendar por link (`yaml:359-364`) |
| 4 | PUT `/api/rh/formularios/{formularioId}` | editarFormulario | RH | `FormularioDetalhesComando` | 200 `FormularioDetalheRh` | 400, 403, 404; 409 `Erro409Anonimato`; 422 `Erro422Edicao` | matriz por situação em D.6 (`yaml:433-449`) |
| 5 | DELETE `/api/rh/formularios/{formularioId}` | excluirFormulario | RH | `confirmar` (bool, padrão false) | 204 | 403, 404; 409 `CONFIRMACAO_NECESSARIA` | qualquer situação; **exclusão física** em uma transação, na ordem respostas → participações → destinatários → candidatos → alternativas → perguntas → formulário; depois apaga os arquivos do armazenamento; recorrente deixa de reabrir; auditoria "Excluiu" com título e **quantas** respostas (sem a contagem no anônimo não encerrado); **não declara 422** (`yaml:506-531`); x-provisorio (encerrado também se exclui) |
| 6 | POST `.../{formularioId}/perguntas` | adicionarPerguntaFormulario | RH | `PerguntaComando` | 201 `PerguntaFormulario` | 400, 403, 404; 409 `ANONIMO_PERGUNTAS_FIXAS`; 422 `Erro422Edicao` | sem `ordem` vai para o fim; com `ordem` entra ali e as seguintes descem; com nome `PUBLICADO` já respondido **aceita** pergunta nova; `ENCERRADO` 422; `ARQUIVO` em anônimo 422 `ANONIMO_SEM_ARQUIVO` (`yaml:551-564`) |
| 7 | PUT `.../perguntas/{perguntaId}` | editarPerguntaFormulario | RH | `PerguntaComando` | 200 `PerguntaFormulario` | 400, 403, 404; 409 `ANONIMO_PERGUNTAS_FIXAS`; 422 `Erro422Edicao` | troca o conteúdo inteiro; alternativa com `id` mantém, sem `id` é nova, a que sumiu é apagada; pergunta respondida: só `obrigatoria` e `ordem` mudam (422 `PERGUNTA_JA_RESPONDIDA` + `regraDoBanco`); anônimo publicado: sempre 409, **inclusive** `obrigatoria` e `ordem` (`yaml:607-619`) |
| 8 | DELETE `.../perguntas/{perguntaId}` | excluirPerguntaFormulario | RH | — | 204 | 403, 404; 409 `ANONIMO_PERGUNTAS_FIXAS`; 422 `Erro422Edicao` | as seguintes sobem; respondida = 422 `PERGUNTA_JA_RESPONDIDA`; anônimo publicado sempre 409 (`yaml:665-668`) |
| 9 | GET `.../{formularioId}/verificacao` | verificarFormulario | RH | — | 200 `FormularioVerificacao` | 403, 404 | itens de código fixo, na ordem (D.7); `AVISO` não impede; x-provisorio (dado sensível por palavra-chave) (`yaml:688-706`) |
| 10 | PUT `.../{formularioId}/publicacao` | salvarPublicacaoFormulario | RH | `FormularioPublicacaoComando` | 200 `FormularioDetalheRh` | 400, 403, 404; 422 `Erro422Publicacao` | só guarda; continua `RASCUNHO` (ou `AGENDADO`); **não** roda verificação nem o mínimo de 5; sem e-mail, sem link; `PUBLICADO`/`ENCERRADO` = 422; **`agendarPara` de rascunho é aceito e não guardado** (volta `null`) (`yaml:748-759`) |
| 11 | POST `.../{formularioId}/publicar` | publicarFormulario | RH | `FormularioPublicacaoComando` | 200 `FormularioDetalheRh` | 400, 403, 404; 422 `Erro422Publicacao` | 1º mínimo de 5 do anônimo, 2º verificação com o público do corpo, 3º publica agora (`PUBLICADO`, `publicadoEmUtc`) ou agenda (`AGENDADO`, publica no início do dia -03:00, uma vez); `LINK` gera `tokenDoLink` (43 caracteres) mesmo agendando; **sem e-mail**; tarefas de publicar, encerrar pelo prazo, reabrir e lembrete (`yaml:819-879`) |
| 12 | POST `.../{formularioId}/encerrar` | encerrarFormulario | RH | `confirmar` | 200 `FormularioDetalheRh` | 403, 404; 409 `CONFIRMACAO_NECESSARIA`; 422 `Erro422Edicao` (`FORMULARIO_NAO_PUBLICADO`) | só `PUBLICADO`; grava `encerradoEmUtc`; recorrente: **cancela as próximas reaberturas**; não há reabrir; anônimo: confirmação **sempre**, `pendentes` `null`; auditoria só "Encerrou", sem contagem (`yaml:1001-1014`) |
| 13 | POST `.../{formularioId}/destinatarios` | adicionarDestinatariosFormulario | RH | `{funcionarioIds}` (1-200) | 200 `{adicionados, jaEstavam}` | 400, 403, 404; 409 `ANONIMO_SEM_NOVOS_DESTINATARIOS`; 422 `Erro422Alvo` | só `alvo = PESSOAS` em `RASCUNHO`/`AGENDADO`/`PUBLICADO`; já incluído conta em `jaEstavam`; inativo = 422 `FUNCIONARIO_INATIVO` e **nada** entra; sem e-mail; não vale para avaliação; **`criarFuncionario` e `confirmarImportacao` chamam isto por dentro**; nenhuma tela chama direto (`yaml:1080-1099`) |
| 14 | GET `.../{formularioId}/respondentes` | listarRespondentesFormulario | RH | paginação, `situacao` (`RESPONDIDO`\|`NAO_RESPONDEU`), `setorId`, `busca` (nome) | 200 `FormularioRespondentesRh` | 400, 403, 404; 422 `FORMULARIO_POR_LINK` | `resumo` não muda com filtros; ordem nome A-Z; anônimo: `itens: []`, `total: 0`, filtros ignorados, contagem só depois de `ENCERRADO`; avaliação: uma linha por avaliação; Sugestão: uma linha por envio (nome e, na mesma pessoa, o mais recente primeiro), sem `NAO_RESPONDEU`; quem deve responder é **calculado na hora** (no anônimo, a lista fixada) (`yaml:1133-1159`) |
| 15 | GET `.../{formularioId}/candidatos` | listarCandidatosFormulario | RH | paginação, `busca` (nome e e-mail) | 200 `CandidatoLista` | 400, 403, 404; 422 `Erro422Alvo` (`FORMULARIO_SEM_LINK`) | só `alvo = LINK`; mais recentes primeiro; dado pessoal só para o RH (`yaml:1267-1272`) |
| 16 | GET `.../respostas/funcionarios/{funcionarioId}` | obterRespostasDeFuncionario | RH | `avaliadoId` (obrig. em avaliação, proibido nos outros: 400), `respondidoEmUtc` (obrig. na Sugestão, proibido nos outros: 400) | 200 `RespostaFormularioFuncionario` | 400, 403, 404; 422 `FORMULARIO_ANONIMO` | só com nome; quem não respondeu = 404; Sugestão: 404 se não há envio naquele instante (`yaml:1306-1315`) |
| 17 | GET `.../respostas/candidatos/{candidatoId}` | obterRespostasDeCandidato | RH | — | 200 `RespostaFormularioCandidato` | 403, 404; 422 `FORMULARIO_SEM_LINK` | candidato de outro formulário = 404 (`yaml:1372`) |
| 18 | GET `.../arquivos/{respostaId}/download` | baixarArquivoDeResposta | RH | — | 200 `LinkTemporario` | 403, 404 | resposta de pergunta `ARQUIVO` de funcionário ou candidato; anexo; anônimo = 404; caminho nunca aparece (`yaml:1424-1427`) |
| 19 | GET `.../candidatos/{candidatoId}/curriculo/download` | baixarCurriculoDeCandidato | RH | — | 200 `LinkTemporario` | 403, 404 | sem currículo = 404; anexo (`yaml:1449`) |
| 20 | GET `.../{formularioId}/resultado` | obterResultadoFormulario | RH | `avaliadoId` (obrig. em avaliação, proibido nos outros: 400) | 200 `FormularioResultado` | 400, 403, 404; 409 `RESULTADO_ANONIMO_INDISPONIVEL`; 422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES` | anônimo: só `ENCERRADO`; pergunta com < 3 respostas some e conta em `perguntasOcultas`; textos em **ordem alfabética**; avaliado anônimo com < 3 = 422; com nome: qualquer situação, sem mínimo (`yaml:1472-1501`) |
| 21 | GET `.../{formularioId}/avaliados` | listarAvaliadosFormulario | RH | — | 200 `FormularioAvaliadosLista` | 403, 404; 409 `RESULTADO_ANONIMO_INDISPONIVEL`; 422 `Erro422Alvo` (`FORMULARIO_NAO_E_AVALIACAO`) | sem paginação, por nome; `esperadas` sempre exato; anônimo: só depois de encerrado e `totalRespostas: null` para quem tem < 3 (`yaml:1636-1647`) |
| 22 | GET `.../{formularioId}/exportacao` | exportarRespostasFormulario | RH | — | 200 `text/csv` (anexo) | 403, 404; 409 `RESULTADO_ANONIMO_INDISPONIVEL`; 422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES` | `;`, UTF-8 com BOM, cabeçalho, datas -03:00, células `=`,`+`,`-`,`@` neutralizadas; com nome/link: uma linha por resposta; anônimo: só o agregado (`yaml:1676-1689`) |
| 23 | POST `.../{formularioId}/lembretes` | lembrarQuemFaltaFormulario | RH | `{funcionarioIds}` (0-200; vazio = todos que faltam) | **200** `FormularioLembreteResultado` | 400, 403, 404; 422 `Erro422Anonimo` | só `PUBLICADO`; não vale por link nem na Sugestão; 1 lembrete por pessoa/formulário/dia; anônimo: só lista vazia (senão 422 `FORMULARIO_ANONIMO`), resposta **sempre igual** (`enviados`/`ignorados` `null`), limite por formulário/dia, auditoria sem número nem destinatário (`yaml:1718-1738`) |
| 24 | GET `/api/gestor/formularios` | listarFormulariosDaEquipe | G | — | 200 `FormularioEquipeLista` | 403 | só `PUBLICADOS` em que gente do setor do gestor deve responder; contagens só da equipe (sem o gestor); **fora**: por link, Sugestão, as duas avaliações, encerrado; sem paginação; ordem prazo mais próximo e título (`yaml:1783-1799`) |
| 25 | GET `/api/gestor/formularios/{formularioId}/respondentes` | listarRespondentesDaEquipe | G | — | 200 `FormularioEquipeRespondentes` | 403, 404 | só quem, nunca o conteúdo; outro setor, Sugestão e as duas avaliações = 404; sem paginação, por nome; anônimo: `itens: []` e contagem só depois de `ENCERRADO`; `observacao` = férias aprovadas que cobrem hoje ("de férias até 17/10") ou `null` (`yaml:1821-1832`) |
| 26 | POST `/api/gestor/formularios/{formularioId}/lembretes` | lembrarQuemFaltaNaEquipe | G | sem corpo | 200 `FormularioLembreteResultado` | 403, 404; 422 `Erro422Anonimo` | só a equipe; mesmas regras do #23; anônimo: limite por formulário, dia **e alcance** (o setor); 404 fora da equipe, Sugestão e avaliação do gestor (`yaml:1884-1894`) |
| 27 | GET `/api/eu/formularios` | listarMeusFormularios | F,G,RH | paginação, `busca` (título), `tipo`, `situacao` (`FormularioMinhaSituacao`) | 200 `FormularioMeusLista` | 400 | sem `situacao` traz `PARA_RESPONDER` + `DISPONIVEL`; `contagens` respeitam `busca` e `tipo` e ignoram `situacao`; entram os `PUBLICADOS` em que a pessoa deve responder **e todos os que já respondeu** (mesmo encerrados); por link nunca; **`contagens.paraResponder` é a bolinha do menu** (`yaml:1926-1951`; `70-fatia-7:2204-2206`) |
| 28 | GET `/api/eu/formularios/{formularioId}` | obterFormularioParaResponder | F,G,RH | — | 200 `FormularioParaResponder` | 404; 422 `Erro422Resposta` | não deve responder = 404; encerrado/agendado = 422; avaliação traz `avaliados` com `respondido` (`yaml:2070-2081`) |
| 29 | POST `/api/eu/formularios/{formularioId}/respostas` | responderFormulario | F,G,RH | multipart `RespostaFormularioEnvio` (`respostas` JSON, `arquivos` repetido, `avaliadoId`) | 201 `RespostaFormularioRecibo` | 400, 404; 409 `JA_RESPONDIDO`; 422 `Erro422Resposta` | tudo ou nada; uma resposta por pessoa (por avaliado), Sugestão aceita várias; mesmo instante em todas as linhas do envio; anônimo: sem funcionário, só o dia, participação UUID v4, **nada na auditoria** (`yaml:2141-2175`) |
| 30 | GET `/api/eu/formularios/{formularioId}/minhas-respostas` | obterMinhasRespostas | F,G,RH | `avaliadoId` | 200 `RespostaFormularioMinhas` | 400, 404; 422 `Erro422Anonimo` (`FORMULARIO_ANONIMO`, `FORMULARIO_SUGESTAO`) | só com nome; não respondeu = 404; arquivo só com o nome, sem download de volta (`yaml:2219-2223`) |
| 31 | GET `/api/publico/formularios/{token}` | obterFormularioPublico | **PUB** | `token` (`^[A-Za-z0-9_-]{22,64}$`) | 200 `FormularioPaginaPublica` | 404; 422 `Erro422Resposta`; **429** | token inexistente ou de rascunho = o mesmo 404; agendado 422 `FORMULARIO_AINDA_NAO_ABERTO`; encerrado ou prazo vencido 422 `FORMULARIO_ENCERRADO`; sem empresa 422 `EMPRESA_NAO_CONFIGURADA`; **30 consultas/min por IP**; **não declara 400** (`yaml:2284-2291`); x-provisorio |
| 32 | POST `/api/publico/formularios/{token}/respostas` | responderFormularioPublico | **PUB** | multipart `CandidatoEnvio` | 201 `CandidatoRecibo` | 400, 404; 409 `JA_RESPONDIDO`; 422 `Erro422Resposta`; **429** (`Retry-After`) | o mesmo e-mail responde uma vez; arquivo errado = 400 em `curriculo` ou `arquivos`; **10 envios/hora por IP**; auditoria sem o valor dos dados; candidato apagado pela retenção (`yaml:2348-2360`); x-provisorio |

Contagem por incremento sugerido (G): construtor 13 (#1-#13), responder e acompanhar 13 (#14, #16, #18, #20, #22, #23-#30), link público 5 (#15, #17, #19, #31, #32), avaliações 1 rota exclusiva (#21) mais o parâmetro `avaliadoId` em #16, #20, #29, #30.

---

## B. Catálogo de erros

Os títulos de 409 e 422 são os genéricos `Conflito.` e `Operação recusada.` (`base:238`, `:248`), **exceto** `CONFIRMACAO_NECESSARIA`, que tem título próprio `Confirmação necessária.` e a extensão `pendentes` (schema `FormularioConfirmacaoNecessaria`, `yaml:3136-3150`). Copiar os textos dos exemplos, como nas fatias 3 e 4, com um teste que os confere contra o yaml.

### B.1 Com código

| codigo (status) | detail exato (variações) | regraDoBanco | Onde sai |
|---|---|---|---|
| ANONIMATO_FIXO_APOS_PUBLICAR (409) | Depois de publicado, o formulário não muda entre anônimo e identificado. (`yaml:2473`) | — | #4 (trocar `anonimo` de publicado, nos dois sentidos, com ou sem resposta; mudar `tipo`/`sentidoAvaliacao` de anônimo publicado) |
| ANONIMO_PERGUNTAS_FIXAS (409) | As perguntas de um formulário anônimo publicado não mudam. (`yaml:2481`) | — | #6, #7, #8 (sempre, sem olhar resposta) |
| ANONIMO_SEM_NOVOS_DESTINATARIOS (409) | Um formulário anônimo publicado não recebe pessoas novas. (`yaml:2489`) | — | #13 |
| CONFIRMACAO_NECESSARIA (409) | com nome: "63 pessoas ainda não responderam. Confirme para continuar." + `pendentes: 63` (`yaml:2505-2510`) · anônimo: "Num formulário anônimo o sistema não diz quantas pessoas ainda não responderam. Confirme para continuar." + `pendentes: null` (`yaml:2514-2519`). **Título "Confirmação necessária."** | — | #5, #12 |
| RESULTADO_ANONIMO_INDISPONIVEL (409) | O resultado de um formulário anônimo só aparece depois que ele é encerrado. (`yaml:2530`) | — | #20, #21, #22 |
| JA_RESPONDIDO (409) | funcionário: "Você já respondeu este formulário." (`yaml:2761`) · candidato: "Já recebemos uma candidatura com este e-mail." (`yaml:2769`) | — | #29, #32 |
| FORMULARIO_ENCERRADO (422) | edição: "O formulário está encerrado e não pode mais ser alterado." (`yaml:2565`) · resposta/abrir: "Este formulário não está mais aceitando respostas." (`yaml:2720`) · cadastro e importação: "O formulário de admissão escolhido já foi encerrado. Escolha outro." (`20-fatia-1:254`, `25-fatia-2:342`) | — (na resposta pode vir `Formulario inativo nao aceita respostas.`) | #4, #6-#8, #10, #11, #13, #28, #29, #31, #32, `criarFuncionario`, `confirmarImportacao` |
| FORMULARIO_NAO_PUBLICADO (422) | lembrete: "Só dá para lembrar quem falta em formulário publicado." (`yaml:2667`) · encerrar: **sem exemplo** | — | #12, #23, #26 |
| PERGUNTA_JA_RESPONDIDA (422) | Esta pergunta já foi respondida. Só dá para mudar se ela é obrigatória e a posição dela. (`yaml:2548`). No DELETE o texto não serve: **sem exemplo** | editar: `Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.` (`yaml:2551`) · excluir: `Pergunta ja respondida: nao e possivel exclui-la.` (`yaml:665-666`) | #7, #8 (**nunca** em anônimo publicado) |
| ANONIMO_SEM_ARQUIVO (422) | Formulário anônimo não pode ter pergunta de envio de arquivo. (`yaml:2557`) | — | #4 (passar a anônimo com pergunta de arquivo), #6, #7, #11 (lista de `Erro422Publicacao`) |
| ANONIMO_SEM_LINK (422) | Formulário anônimo não pode ser respondido por link público. (`yaml:2608`) | **`CHK_FORMULARIOS_ANONIMO_SEM_LINK`** (nome do CHECK, não mensagem de trigger; `yaml:2611`) | #4, #10, #11 |
| LINK_SO_PARA_ENTREVISTA (422) | Só a entrevista automática pode ser respondida por link. Escolha setores ou pessoas. (`yaml:2600`) | — | #4 (mudar o tipo de um formulário por link), #10, #11 |
| FORMULARIO_ANONIMO_PUBLICO_PEQUENO (422) | Um formulário anônimo precisa ir para pelo menos 5 pessoas. Este vai para 4. (`yaml:2584`; o número é montado) | — | #11 (antes da verificação) |
| VERIFICACAO_COM_BLOQUEIO (422) | O formulário tem itens a corrigir antes de publicar. Veja a etapa Verificação. (`yaml:2592`) | — | #11 |
| FORMULARIO_JA_PUBLICADO (422) | **sem exemplo** ("publicar o que já está publicado ou agendado", `yaml:2572-2573`) | — | #10 (em `PUBLICADO`), #11 |
| FUNCIONARIO_INATIVO (422) | destinatários/publicação: "Uma das pessoas escolhidas está inativa e não pode receber o formulário." (`yaml:2642`) · responder: **sem exemplo** | responder: `Funcionario inativo nao pode responder formularios.` (`yaml:2150-2151`) | #10, #11, #13, #29 |
| FORMULARIO_POR_LINK (422) | lista: "Este formulário é respondido por link. Veja a lista de candidatos." (`yaml:2626`) · lembrete: **sem exemplo** | — | #14, #23 |
| FORMULARIO_SEM_LINK (422) | **sem exemplo** ("use a lista de funcionários", `yaml:2615`) | — | #15, #17 |
| FORMULARIO_NAO_E_POR_PESSOAS (422) | Só dá para incluir pessoas em formulário feito para pessoas escolhidas. (`yaml:2634`) | — | #13, cadastro, importação |
| FORMULARIO_NAO_E_AVALIACAO (422) | **sem exemplo** | — | #21 |
| FORMULARIO_ANONIMO (422) | Este formulário é anônimo: o sistema não guarda quem respondeu o quê. (`yaml:2659`) | — | #16, #23 (lista não vazia em anônimo), #30 |
| FORMULARIO_SUGESTAO (422) | lembrete: "A caixa de sugestões é sempre aberta e ninguém é obrigado a responder, então não há quem lembrar." (`yaml:2675`) · minhas respostas: **sem exemplo** | — | #23, #30 |
| RESULTADO_SEM_RESPOSTAS_SUFICIENTES (422) | exportação: "Nenhuma pergunta deste formulário anônimo teve 3 respostas ou mais, então não há resultado para exportar." (`yaml:2693`) · avaliado: "Esta pessoa recebeu menos de 3 avaliações, então o resultado dela não aparece." (`yaml:2701`). **Nunca diz quantas.** | — | #20, #22 |
| FORMULARIO_AINDA_NAO_ABERTO (422) | Este formulário ainda não está aberto. Tente de novo a partir da data de publicação. (`yaml:2728`) | — | #28, #29, #31, #32 |
| AVALIADO_INVALIDO (422) | Você não deve avaliar esta pessoa neste formulário. (`yaml:2736`) | — | #29 |
| EMPRESA_NAO_CONFIGURADA (422) | Este formulário ainda não está disponível. Fale com o RH. (`yaml:2744`) (texto **diferente** do da 1c e do da 4) | — | #31, #32 |
| (sem código, 422) | detail genérico de `Erro422RegraDeNegocio` (`base:250`) | `Pergunta obrigatoria: informe a resposta.` e `Formulario inativo nao aceita respostas.` (`yaml:2148-2150`, `:2709-2710`) | #29, #32 (só se a validação da aplicação deixar passar) |
| (429) | título "Muitas tentativas."; "Muitas tentativas. Aguarde um minuto e tente de novo." + `Retry-After` (`base:253-264`) | — | #31, #32 |

Erros de **campo** (400, mapa `errors`) que o contrato nomeia: `anonimo` (Sugestão anônima, `yaml:299`), `alternativas` (fora de 2-10 em múltipla escolha, não vazia nos outros tipos, `yaml:2869-2870`), `respostas[2].texto` e `arquivos` (`yaml:2148`), `curriculo` ou `arquivos` (tipo/tamanho, `yaml:2354-2356`), `formularioAdmissaoId` (cadastro e importação: formulário que não existe, não é de admissão ou não está publicado, `20-fatia-1:161-162`, `25-fatia-2:234-235`). Os demais nomes de campo seguem o corpo (`setorIds`, `funcionarioIds`, `prazo`, `agendarPara`, `recorrencia`, `sentidoAvaliacao`, `avaliadoId`, `respondidoEmUtc`, `aceitouAvisoDePrivacidade`).

### B.2 O que o banco impõe e o contrato não declara (validar antes; senão vira 500 ou 409 genérico)

| Recusa do banco | Quando acontece | O que a API deve fazer |
|---|---|---|
| 1451 na FK `FK_FORMULARIOS_RESPOSTAS_FORMULARIOS_ANONIMO` | `UPDATE FORMULARIOS SET ANONIMO` com resposta gravada | 409 `ANONIMATO_FIXO_APOS_PUBLICAR` **antes** (`yaml:444-445`); o 1451 é só a 2ª barreira |
| 1452 na mesma FK | resposta gravada com `ANONIMO` diferente do formulário (padrão `FALSE`) | sempre gravar `ANONIMO` igual ao do formulário (`V001:2245-2247`) |
| 3819 `CHK_FORMULARIOS_ANONIMO_SEM_LINK` | anônimo com `PUBLICO = 'LINK'` | 422 `ANONIMO_SEM_LINK` antes |
| 3819 `CHK_FORMULARIOS_LINK_SO_COM_PUBLICO_LINK` | trocar o alvo de um **agendado por link** sem apagar o código | ao trocar o alvo, anular `LINK_PUBLICO_CODIGO` no mesmo UPDATE |
| 3819 `CHK_FORMULARIOS_ENCERRADO_INATIVO` | gravar `DATA_ENCERRAMENTO` sem `ATIVO = FALSE` (encerrar) ou ligar `ATIVO` sem limpar a data (reabrir) | os dois campos no mesmo UPDATE |
| 3819 `CHK_FORMULARIOS_SENTIDO_AVALIACAO` | mudar o tipo de `AVALIACAO` para outro sem anular o sentido | anular junto; o 400 de `sentidoAvaliacao` vem antes |
| 3819 `CHK_FORMULARIOS_RECORRENCIA_*` (4) | intervalo sem unidade; tirar a recorrência deixando `PROXIMA_REABERTURA` ou `RECORRENCIA_ATE` | limpar as quatro colunas juntas |
| 3819 `CHK_FORMULARIOS_RESPOSTAS_AUTOR` / `_ANONIMA_SEM_AUTOR` / `_ARQUIVO_JUNTOS` / `_ANONIMA_SEM_ARQUIVO` | resposta com funcionário **e** candidato; anônima com autor; com nome sem autor; caminho sem nome; arquivo em anônima | construir a linha por uma única fábrica por "dono" (funcionário, candidato, ninguém) |
| 3819 `CHK_FORMULARIOS_PARTICIPACOES_UUID_V4` | chave que não é UUID v4 (o `UUID()` do MySQL é v1) | `Guid.NewGuid()` na aplicação, mapeado **sem geração de valor** (roadmap:81 d) |
| 3819 `CHK_FORMULARIOS_DESTINATARIOS_PESSOA_OU_SETOR` | linha com pessoa e setor, ou sem nenhum | uma linha por pessoa **ou** por setor |
| 1062 `UQ_FORMULARIOS_PERGUNTAS_FORMULARIO_ORDEM` | reordenar sem passo intermediário; duas abas acrescentando pergunta no mesmo lugar | permutar com `ORDEM + 1000` e depois o valor final (`LEIA-ME.md:91`); a corrida vira 409 genérico ou nova tentativa (LACUNA) |
| 1062 `UQ_FORMULARIOS_ALTERNATIVAS_PERGUNTA_ORDEM` | idem, nas alternativas | idem |
| 1062 `UQ_FORMULARIOS_CANDIDATOS_FORMULARIO_EMAIL` | o mesmo e-mail duas vezes (inclusive em corrida) | 409 `JA_RESPONDIDO` (texto do candidato) |
| 1062 `UQ_FORMULARIOS_PARTICIPACOES_PESSOA` | a mesma pessoa (e avaliado) duas vezes no anônimo | 409 `JA_RESPONDIDO` |
| 1062 `UQ_FORMULARIOS_DESTINATARIOS_DESTINO` | incluir de novo a mesma pessoa/setor/avaliado (corrida) | contar em `jaEstavam` |
| 1062 `UQ_FORMULARIOS_LINK_PUBLICO_CODIGO` | colisão de token (improvável com 32 bytes) | gerar outro |
| 1451 nas FKs sem `ON DELETE` | apagar pergunta com alternativas; alternativa com resposta; formulário com filhos; candidato com respostas | apagar na ordem de `yaml:522-524`; alternativa respondida = 422 `PERGUNTA_JA_RESPONDIDA` antes |
| 45000 `Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.` / `...exclui-la.` | #7 e #8 em pergunta com resposta | 422 `PERGUNTA_JA_RESPONDIDA` antes; **em anônimo publicado nunca chegar aqui** (409 antes) |
| 45000 `Formulario inativo nao aceita respostas.` | responder agendado/encerrado, ou a corrida com o encerramento | 422 `FORMULARIO_AINDA_NAO_ABERTO`/`FORMULARIO_ENCERRADO` antes; na corrida, 422 `FORMULARIO_ENCERRADO` com `regraDoBanco` |
| 45000 `Funcionario inativo nao pode responder formularios.` | resposta **com nome** de inativo | 422 `FUNCIONARIO_INATIVO`; **o trigger não confere o anônimo** (`ID_FUNCIONARIO` nulo, `V001:2224-2232`): ali só a aplicação barra |
| 45000 `Pergunta obrigatoria: informe a resposta.` | linha de pergunta obrigatória com `RESPOSTA` vazia | sempre preencher `RESPOSTA` (texto da alternativa, o número da escala, o nome do arquivo: `V001:2175-2178`, `LEIA-ME.md:90`); 400 antes |
| 413 / 415 | corpo acima do teto da requisição; corpo que não é multipart em #29 e #32 | padrão já aceito nas fatias 1c, 3 e 4 (não estão no contrato) |
| Sem recusa (o banco **aceita**) | resposta depois do prazo (`V001:2145-2146`); a mesma pessoa duas vezes em formulário com nome (sem chave única, `yaml:2155-2157`); pergunta obrigatória **não enviada** (o trigger só olha a linha inserida); participação de pessoa inativa | tudo conferido pela aplicação |

---

## C. Schemas (`yaml:2773-3690`)

### C.1 Enums

- **FormularioTipo** `ENTREVISTA|ADMISSAO|AVALIACAO|PESQUISA|OUTRO` (= `CHK_FORMULARIOS_TIPO`; `OUTRO` é a **Sugestão**) (`yaml:2775-2791`).
- **FormularioSentidoAvaliacao** `GESTOR_AVALIA_EQUIPE|EQUIPE_AVALIA_GESTOR` (`yaml:2792-2797`).
- **FormularioSituacao** `RASCUNHO|AGENDADO|PUBLICADO|ENCERRADO`, **calculada** de `ATIVO`, `DATA_PUBLICACAO`, `DATA_ENCERRAMENTO` (`yaml:2798-2804`).
- **FormularioAlvo** `LINK|SETORES|PESSOAS` (= `FORMULARIOS.PUBLICO`) (`yaml:2805-2810`).
- **FormularioRespondenteSituacao** `RESPONDIDO|NAO_RESPONDEU`. **FormularioMinhaSituacao** `PARA_RESPONDER|DISPONIVEL|RESPONDIDO` (x-provisorio: `ADMISSAO` e `AVALIACAO` = para responder; `PESQUISA` e `OUTRO` = disponíveis; `yaml:2815-2821`).
- **PerguntaTipo** `RESPOSTA_CURTA|RESPOSTA_LONGA|MULTIPLA_ESCOLHA|ESCALA|ARQUIVO` (texto livre em `TIPO_RESPOSTA`; `yaml:2822-2829`). **FormularioRecorrenciaUnidade** `DIA|SEMANA|MES`. Situação do item de verificação `OK|AVISO|BLOQUEIO` (inline, `yaml:3123`).

### C.2 Perguntas

- **PerguntaAlternativa** `{id, ordem ≥1, texto ≤255}` (todos obrig.). **PerguntaAlternativaComando** `{id?, texto 1-255}`.
- **PerguntaFormulario** (todos obrig.) `id, ordem ≥1, texto ≤500 (markdown, nunca HTML), tipo, obrigatoria, alternativas[]` (só em `MULTIPLA_ESCOLHA`, de 2 a 10; nos outros `[]`).
- **PerguntaComando** obrig. `texto (1-500), tipo, obrigatoria`; opc. `alternativas` (≤10, padrão `[]`; **2 a 10 em `MULTIPLA_ESCOLHA`, vazia nos outros: 400 em `errors.alternativas`**), `ordem ≥1`.

### C.3 Formulário (RH)

- **FormularioDetalhesComando** obrig. `titulo (1-200), tipo, anonimo`; opc. `descricao (string|null, ≤1000, markdown)`, `sentidoAvaliacao` (`oneOf` null ou o enum).
- **FormularioRecorrencia** obrig. `intervalo (1-365), unidade`; opc. `ate (date|null)`. x-provisorio: o prazo da rodada nova avança o mesmo número de dias da anterior (`yaml:2917`).
- **FormularioPublicacaoComando** obrig. `alvo`; opc. `setorIds[]`, `funcionarioIds[]` (padrão `[]`), `agendarPara (date|null)`, `prazo (date|null)`, `recorrencia (null|objeto)`, `lembreteAutomatico (bool, padrão true)`. Regras de campo (400): `LINK` exige as duas listas vazias; `SETORES` exige `setorIds` ≥1 e `funcionarioIds` vazio; `PESSOAS` o contrário; `prazo` ≥ hoje e ≥ `agendarPara`; `agendarPara` não no passado; Sugestão com `prazo` ou `recorrencia` = 400. Pessoa inativa = 422 (`yaml:2928-2936`).
- **FormularioPublicacao** (todos obrig.) `alvo|null, setores[{id,nome}], pessoas[FuncionarioResumo], agendarPara|null (só enquanto AGENDADO), prazo|null, recorrencia|null, lembreteAutomatico, publicadoEmUtc|null, encerradoEmUtc|null, proximaReaberturaEmUtc|null, tokenDoLink|null`.
- **FormularioDetalheRh** (todos obrig.) `id, titulo, descricao|null, tipo, sentidoAvaliacao|null, anonimo, situacao, autor (FuncionarioResumo|null), criadoEmUtc, temRespostas (bool|null: **null em anônimo**), perguntas[], publicacao`.
- **FormularioRhItem** (todos obrig.) `id, titulo, tipo, sentidoAvaliacao|null, anonimo, situacao, alvo|null, quemRespondeResumo (texto pronto), respondidos (int|null), esperados (int|null), taxaDeRespostaPercentual (0-100|null), prazo|null, encerradoEmUtc|null, criadoEmUtc`. Textos de `quemRespondeResumo` (`yaml:3076`): "Candidatos, por link", "3 pessoas escolhidas", "Todos os setores (101 pessoas)", "3 gestores (24 avaliações)", "Todos os setores (24 avaliações)", "Ainda não definido". **LACUNA:** o texto quando são **alguns** setores (não todos) e o singular.
- **FormularioContagemPorTipo** `{todos, entrevistas, admissoes, avaliacoes, pesquisas, sugestoes}`. **FormularioRhLista** = Paginação + `itens` + `contagemPorTipo`.
- **FormularioVerificacao** `{podePublicar, itens[{codigo, situacao, mensagem, perguntaId|null}]}`.
- **FormularioConfirmacaoNecessaria** = `ProblemaDeApi` + `pendentes (int ≥1 | null)` obrigatório.
- **FormularioDestinatariosComando** `{funcionarioIds: 1-200}`; resultado `{adicionados, jaEstavam}`.

### C.4 Acompanhar, respostas e resultado

- **FormularioParticipacaoResumo** `{respondidos, faltam, esperados, taxaDeRespostaPercentual}` (todos `int|null`).
- **FormularioRespondenteRh** `{funcionario, avaliado|null (só em avaliação), situacao, respondidoEmUtc|null, podeVerRespostas}`. **FormularioRespondentesRh** = Paginação + `anonimo, resumo, itens`.
- **CandidatoResumo** (todos obrig.) `id, nome, email, telefone|null, temCurriculo, respondidoEmUtc`. **CandidatoLista** = Paginação + `itens`.
- **RespostaFormularioDada** (todos obrig.) `perguntaId, ordem, perguntaTexto, tipo, texto|null, alternativas[PerguntaAlternativa], valorEscala (1-5|null), arquivo ({respostaId, nome}|null)`: **só o campo do tipo da pergunta vem preenchido**; pergunta opcional em branco vem com tudo vazio (logo a lista sai das **perguntas**, não das linhas de resposta).
- **RespostaFormularioFuncionario** `{formularioId, titulo, funcionario, avaliado|null, respondidoEmUtc, respostas[]}`; **...Candidato** `{formularioId, titulo, candidato, respostas[]}`; **...Minhas** `{formularioId, titulo, avaliado|null, respondidoEmUtc, respostas[]}`.
- **FormularioResultado** (todos obrig.) `formularioId, titulo, anonimo, situacao, totalRespostas, minimoPorPergunta (3 no anônimo | null), perguntasOcultas (0 com nome), avaliado|null, perguntas[]`.
- **FormularioResultadoPergunta** (todos obrig.) `perguntaId, ordem, texto, tipo, totalRespostas, alternativas[{alternativaId, texto, total, percentual (1 casa)}], escala ({media (1 casa), distribuicao[{valor 1-5, total}] sempre os cinco} | null), textos[string]`. Por tipo: múltipla escolha → `alternativas` (todas, inclusive com 0: `yaml:1597`); escala → `escala`; curta/longa → `textos`; arquivo → só `totalRespostas`.
- **FormularioAvaliadosLista** `{minimoPorAvaliado (3|null), itens[{funcionario, totalRespostas (int|null), esperadas, resultadoLiberado}]}`.
- **FormularioLembreteComando** `{funcionarioIds: 0-200}` (obrigatório, pode ser vazio). **FormularioLembreteResultado** `{anonimo, enviados (int|null), ignorados (int|null)}`.

### C.5 Gestor

- **FormularioEquipeItem** `{id, titulo, tipo, sentidoAvaliacao|null, anonimo, prazo|null, respondidos (int|null), esperados (int, **não nulo**), taxaDeRespostaPercentual|null}`; lista `{itens}`.
- **FormularioEquipeRespondentes** `{formularioId, titulo, anonimo, respondidos|null, faltam|null, esperados (int), taxaDeRespostaPercentual|null, itens[{funcionario, observacao|null, situacao, respondidoEmUtc|null}]}`.

### C.6 Responder

- **FormularioMeuItem** (todos obrig.) `id, titulo, descricao|null, tipo, sentidoAvaliacao|null, anonimo, situacao, totalPerguntas, minutosEstimados (≥1, "regra do back-end"), prazo|null, respondidos, total (≥1), respondidoEmUtc|null, podeVerMinhasRespostas`. **FormularioMeusLista** = Paginação + `itens` + `contagens{todos, paraResponder, disponiveis, respondidos}` (`todos = paraResponder + disponiveis`).
- **FormularioParaResponder** `{id, titulo, descricao|null, tipo, sentidoAvaliacao|null, anonimo, prazo|null, minutosEstimados, perguntas[], avaliados[{funcionario, respondido}]}` (`avaliados: []` fora das avaliações).
- **RespostaFormularioItem** obrig. `perguntaId`; **um só** de `texto (≤4000; curta ≤200)`, `alternativaIds (exatamente 1 item; x-provisorio)`, `valorEscala (1-5)`, `arquivoIndice (≥0)`; campo de outro tipo = 400; pergunta opcional sem resposta pode ficar fora da lista (`yaml:3580-3601`).
- **RespostaFormularioEnvio** (multipart) obrig. `respostas` (JSON, ≥1 item); opc. `avaliadoId`, `arquivos[]` (binários). `encoding`: `respostas` `application/json`; `arquivos` PDF, `.doc`, `.docx` (`yaml:2184-2186`).
- **RespostaFormularioRecibo** `{formularioId, participacaoId (UUID v4 minúsculo | null), restantes (≥0; 0 fora das avaliações)}`.

### C.7 Público

- **FormularioPaginaPublica** (todos obrig.) `empresaNome, titulo, descricao|null, prazo|null, avisoDePrivacidade (markdown), retencaoDias (≥1; 180 por hipótese), perguntas[]`. **Sem `id` do formulário.** Texto do aviso no exemplo (`yaml:2308`): "Usamos seu nome, e-mail, telefone, currículo e respostas só para avaliar a sua candidatura. Só o RH da empresa acessa esses dados. Guardamos por até 180 dias e depois apagamos. Se quiser que apaguemos antes, fale com o RH."
- **CandidatoEnvio** (multipart) obrig. `nome (1-150), email (format email, ≤150), aceitouAvisoDePrivacidade (precisa ser true), respostas (JSON ≥1)`; opc. `telefone (≤30, ^[0-9()+\- ]{8,30}$)`, `curriculo` (binário), `arquivos[]`. **CandidatoRecibo** `{mensagem}` ("Recebemos as suas respostas. Obrigado por se candidatar.", `yaml:2390`).

### C.8 Como o contrato representa anonimato, candidato e avaliação

- **Anonimato:** um booleano no formulário. O que muda está em campos **nulos** (`temRespostas`, `respondidos`, `faltam`, taxa, `pendentes`, `enviados`, `ignorados`, `respondidoEmUtc`, `totalRespostas` do avaliado), em **listas vazias** (`itens` de quem respondeu) e em **erros** (409 em vez de 422 nas perguntas; 409 no resultado antes de encerrar; 422 `FORMULARIO_ANONIMO` onde apareceria a pessoa). Não há nenhum campo que identifique ou ordene uma resposta anônima. O recibo devolve `participacaoId` só à própria pessoa.
- **Candidato:** existe só nas rotas públicas e nas três rotas do RH (#15, #17, #19) mais #18 e #22. Nunca aparece em `/api/eu` nem em `/api/gestor`.
- **Avaliação:** não há schema próprio. É `tipo = AVALIACAO` + `sentidoAvaliacao`, e o par **(quem avalia, `avaliado`)** aparece como `avaliado` (linhas de #14, respostas, resultado), `avaliadoId` (consulta em #16, #20, #30; campo do multipart em #29), `avaliados[]` (#28), `restantes` (recibo) e `total` (cartão). "Ciclo" não existe no contrato da fatia 5: está **no título** (`70-fatia-7:3684-3686`, x-provisorio).

---

## D. O banco

### D.1 Tabelas (`V001`), com a nulabilidade que `MapeamentoContraBancoTests` vai conferir

| Tabela | Colunas (NN = `NOT NULL`) | Restrições e índices |
|---|---|---|
| `FORMULARIOS` (546-608) | `ID_FORMULARIO` PK AI; `TITULO` VARCHAR(200) NN; `DESCRICAO` TEXT NULL; `TIPO_FORMULARIO` VARCHAR(50) **NULL**; `ID_FUNCIONARIO_AUTOR` INT NULL; `DATA_CRIACAO` DATETIME **NULL**; `ATIVO` BOOLEAN NN **padrão TRUE**; `SENTIDO_AVALIACAO` VARCHAR(30) NULL; `PUBLICO` VARCHAR(10) NULL; `DATA_PUBLICACAO` DATETIME NULL; `DATA_ENCERRAMENTO` DATETIME NULL; `DATA_PRAZO` DATE NULL; `ANONIMO` BOOLEAN NN padrão FALSE; `RECORRENCIA_INTERVALO` SMALLINT NULL; `RECORRENCIA_UNIDADE` VARCHAR(10) NULL; `PROXIMA_REABERTURA` DATETIME NULL; `RECORRENCIA_ATE` DATE NULL; `LINK_PUBLICO_CODIGO` VARCHAR(64) ascii_bin NULL; `LINK_PUBLICO_VALIDO_ATE` DATETIME NULL; `LINK_PUBLICO_LIMITE_RESPOSTAS` INT NULL; `LEMBRETE_AUTOMATICO` BOOLEAN NN **padrão FALSE** | FK autor; `CHK_FORMULARIOS_TIPO`; UQ `LINK_PUBLICO_CODIGO`; UQ `(ID_FORMULARIO, ANONIMO)` (alvo da FK das respostas); `CHK_..._ENCERRADO_INATIVO` (data de encerramento ⇒ `ATIVO = FALSE`); `CHK_..._SENTIDO_AVALIACAO`; `CHK_..._PUBLICO`; `CHK_..._ANONIMO_SEM_LINK`; `CHK_..._LINK_SO_COM_PUBLICO_LINK`; `CHK_..._LINK_LIMITE`; quatro `CHK_..._RECORRENCIA_*`; índices `(ATIVO, DATA_ENCERRAMENTO, DATA_PUBLICACAO)`, `(PROXIMA_REABERTURA)`, `(DATA_PRAZO)`, `(TIPO_FORMULARIO, DATA_CRIACAO)` |
| `FORMULARIOS_PERGUNTAS` (614-629) | `ID_FORMULARIO_PERGUNTA` PK AI; `ID_FORMULARIO` NN; `TEXTO_PERGUNTA` TEXT NN; `TIPO_RESPOSTA` VARCHAR(50) **NULL**; `ORDEM` INT **NULL**; `OBRIGATORIA` BOOLEAN NN padrão FALSE | FK formulário (sem `ON DELETE`); **UQ `(ID_FORMULARIO, ORDEM)`**; UQ `(ID_FORMULARIO_PERGUNTA, ID_FORMULARIO)`; CHECK `ORDEM` nula ou > 0 |
| `FORMULARIOS_PERGUNTAS_ALTERNATIVAS` (970-983) | `ID_ALTERNATIVA` PK AI; `ID_FORMULARIO_PERGUNTA` NN; `TEXTO_ALTERNATIVA` VARCHAR(255) NN; `ORDEM` INT NN | FK pergunta; **UQ `(ID_FORMULARIO_PERGUNTA, ORDEM)`**; UQ `(ID_ALTERNATIVA, ID_FORMULARIO_PERGUNTA)`; CHECK `ORDEM > 0`; sem trigger |
| `FORMULARIOS_RESPOSTAS` (639-681, FKs 1648-1656) | `ID_FORMULARIO_RESPOSTA` PK AI; `ID_FORMULARIO_PERGUNTA` NN; `ID_FORMULARIO` NN; `ID_FUNCIONARIO` INT **NULL**; `RESPOSTA` TEXT NULL; `DATA_RESPOSTA` DATETIME NULL; `ID_AVALIADO` NULL; `ID_FORMULARIO_CANDIDATO` NULL; `ID_ALTERNATIVA` NULL; `ANONIMO` BOOLEAN NN padrão FALSE; `CAMINHO_ARQUIVO` VARCHAR(255) NULL; `NOME_ARQUIVO_ORIGINAL` VARCHAR(255) NULL | FK composta `(pergunta, formulário)`; FK funcionário; FK avaliado; **FK `(ID_FORMULARIO, ANONIMO)` → `FORMULARIOS`**; FK `(candidato, formulário)`; FK `(alternativa, pergunta)`; quatro CHECKs (autor; anônima sem autor; arquivo juntos; anônima sem arquivo); índice `(ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO)`. **Sem chave única por pessoa. Sem tabela de "envio".** |
| `FORMULARIOS_CANDIDATOS` (990-1009) | `ID_FORMULARIO_CANDIDATO` PK AI; `ID_FORMULARIO` NN; `NOME` VARCHAR(150) NN; `EMAIL` VARCHAR(150) NN; `TELEFONE` VARCHAR(30) NULL; `CAMINHO_CURRICULO` VARCHAR(255) NULL; `DATA_CADASTRO` DATETIME NN **padrão `CURRENT_TIMESTAMP`** | FK formulário; UQ `(ID_FORMULARIO_CANDIDATO, ID_FORMULARIO)`; **UQ `(ID_FORMULARIO, EMAIL)`** (collation `utf8mb4_unicode_ci`: não diferencia caixa nem acento); índice `(DATA_CADASTRO)` para a retenção. **Sem o nome original do currículo, sem o aceite do aviso.** |
| `FORMULARIOS_DESTINATARIOS` (1016-1045) | `ID_FORMULARIO_DESTINATARIO` PK AI; `ID_FORMULARIO` NN; `ID_FUNCIONARIO` NULL; `ID_SETOR` NULL; `ID_AVALIADO` NULL; `CHAVE_DESTINO` VARCHAR(40) **gerada STORED** | FKs formulário, funcionário, setor, avaliado; **UQ `(ID_FORMULARIO, CHAVE_DESTINO)`**; CHECK pessoa **ou** setor (nunca os dois, nunca nenhum) |
| `FORMULARIOS_PARTICIPACOES` (1052-1074) | `ID_PARTICIPACAO` CHAR(36) NN PK (sem AI); `ID_FORMULARIO` NN; `ID_FUNCIONARIO` NN; `ID_AVALIADO` NULL; `CHAVE_AVALIADO` INT **gerada STORED NN** | **UQ `(ID_FORMULARIO, ID_FUNCIONARIO, CHAVE_AVALIADO)`**; CHECK UUID v4 (caractere 15 = `4`); FKs. **Sem data, sem hora, sem ligação com a resposta** (`V001:2324-2338`) |
| `FILA_EMAILS` (1412-1455), UTC | `SIGILOSO` BOOLEAN NN padrão FALSE; `ID_FUNCIONARIO` NULL; `DESTINATARIO_EMAIL` NN | `CHK_FILA_EMAILS_SIGILOSO_NAO_FICA` (sigiloso só em `PENDENTE`/`EM_ENVIO`: **a aplicação APAGA a linha ao enviar ou desistir**) e `..._SIGILOSO_SEM_PESSOA` (sem `ID_FUNCIONARIO`); tipo sugerido `FORMULARIO_LEMBRETE`, chave `FORMULARIO_LEMBRETE:form=9:func=21:dia=2026-10-24`; no anônimo `...:form=9:dia=2026-10-24:r=<GUID novo>` (`V001:3123`, `:3132`, `:3157-3178`) |
| `TAREFAS_AGENDADAS` (1466-1494), UTC | `TIPO`, `CHAVE_IDEMPOTENCIA` UQ, `REFERENCIA_ID`, `DIA_REFERENCIA`, `PARAMETROS` (**nunca dado pessoal**) | tipos sugeridos `PUBLICAR_FORMULARIO`, `ENCERRAR_FORMULARIO`, `REABRIR_FORMULARIO`, `LEMBRETE_FORMULARIO` (`V001:3213-3214`); marca do lembrete `LEMBRETE_FORMULARIO:form=9:dia=2026-10-24:alcance=TODOS` (ou `alcance=SETOR_3`), inserida **já `CONCLUIDA`** no ato de mandar; o automático é a mesma chave em `PENDENTE` com `EXECUTAR_APOS` no dia (`V001:3239-3250`) |
| `AUDITORIA_EVENTOS` (1507-1551) | `AREA` aceita `FORMULARIOS`; `ACAO` ∈ `ENTROU, ACESSO_NEGADO, CRIOU, ALTEROU, EXCLUIU, INATIVOU, REGISTROU, PEDIU_AJUSTE, APROVOU, RECUSOU, PUBLICOU, ENVIOU_EMAIL, IMPORTOU, BAIXOU` | **não existe ação "ENCERROU" nem "RESPONDEU"**: encerrar é `ALTEROU` com a descrição "Encerrou..." (INFERIDO) |
| `IMPORTACOES` (1609) | `INCLUIDOS_NO_FORMULARIO_ADMISSAO` SMALLINT NULL | preenchida só em `CONFIRMADA`, entre 0 e `TOTAL_CRIADOS` (CHECKs 1624-1635) |

### D.2 Triggers (`V002`), texto exato, todos `SQLSTATE '45000'`

| Trigger | Regra |
|---|---|
| `TRG_FORMULARIOS_BEFORE_INSERT` (799-806) | `DATA_CRIACAO = NOW()` se nula. Só isso. **UPDATE e DELETE de `FORMULARIOS` não têm trigger.** |
| `TRG_FORMULARIOS_PERGUNTAS_BEFORE_UPDATE` (808-826) | se mudou `TEXTO_PERGUNTA`, `TIPO_RESPOSTA` ou `ID_FORMULARIO` e há **qualquer** linha em `FORMULARIOS_RESPOSTAS` da pergunta: `Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.` Mudar só `ORDEM`/`OBRIGATORIA` passa |
| `TRG_FORMULARIOS_PERGUNTAS_BEFORE_DELETE` (828-842) | pergunta com resposta: `Pergunta ja respondida: nao e possivel exclui-la.` |
| `TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT` (844-882) | nesta ordem: formulário com `ATIVO = FALSE` → `Formulario inativo nao aceita respostas.`; funcionário com `ATIVO = FALSE` → `Funcionario inativo nao pode responder formularios.` (com `ID_FUNCIONARIO` nulo o `SELECT` não acha linha e **não dispara**); pergunta obrigatória com `RESPOSTA` nula ou só espaços → `Pergunta obrigatoria: informe a resposta.`; `DATA_RESPOSTA = NOW()` se nula |
| (nenhum) | `FORMULARIOS_RESPOSTAS` **não tem** trigger de UPDATE nem de DELETE; alternativas, candidatos, destinatários e participações não têm trigger nenhum (`yaml:526-527`; `V001:2187`, `:2271`, `:2307`, `:2357`) |

### D.3 Como os estados ficam nas colunas (`V001:2100-2122`)

- `RASCUNHO` = `ATIVO` falso, **sem** `DATA_PUBLICACAO` e sem `DATA_ENCERRAMENTO`. `AGENDADO` = `ATIVO` falso, `DATA_PUBLICACAO` preenchida (o dia agendado), sem encerramento. `PUBLICADO` = `ATIVO` verdadeiro. `ENCERRADO` = `ATIVO` falso e `DATA_ENCERRAMENTO` preenchida.
- Por isso o rascunho **não guarda** `agendarPara` (`yaml:753-759`), e o alvo, os destinatários, o prazo, a recorrência e o lembrete ficam guardados no rascunho normalmente.
- Publicar agora: `ATIVO = TRUE`, `DATA_PUBLICACAO = agora`. Publicar o agendado (tarefa): `ATIVO = TRUE` (e a data efetiva; INFERIDO). Encerrar: `ATIVO = FALSE` + `DATA_ENCERRAMENTO` **no mesmo UPDATE**. Reabrir (recorrente): `ATIVO = TRUE` + `DATA_ENCERRAMENTO = NULL` juntos (`V001:2114-2116`).
- **Encerramento pelo prazo:** os exemplos gravam o **instante nominal**, o começo do dia seguinte ao prazo: prazo `2026-09-30` → `encerradoEmUtc 2026-10-01T03:00:00Z` (`yaml:243-244`, `:257-258`, `:271-272`, `:1060-1064`). INFERIDO: a tarefa `ENCERRAR_FORMULARIO` grava `InicioDoDiaEmUtc(prazo + 1)`, não a hora em que rodou.
- **Prazo vencido com a tarefa atrasada:** o banco **não** confere o prazo (`V001:2145-2146`) e `ATIVO` ainda está ligado. A situação devolvida pela API e a aceitação de resposta têm de considerar o prazo pelo `IRelogio` (ver E.4.6).
- `tokenDoLink` = `LINK_PUBLICO_CODIGO`, guardado **em claro** de propósito (o RH precisa ver o link de novo; `V001:2147-2149`).
- `LINK_PUBLICO_VALIDO_ATE` e `LINK_PUBLICO_LIMITE_RESPOSTAS` existem e o contrato **não as usa** (x-provisorio, `yaml:2292`): ficam nulas.

### D.4 Como cada resposta é gravada

| Tipo da pergunta | `RESPOSTA` | Outras colunas |
|---|---|---|
| `RESPOSTA_CURTA` / `RESPOSTA_LONGA` | o texto (≤200 / ≤4000) | — |
| `MULTIPLA_ESCOLHA` | **o texto da alternativa** (o trigger recusa obrigatória vazia) | `ID_ALTERNATIVA` (`V001:2175-2178`) |
| `ESCALA` | o número como texto ("4") (INFERIDO: a mesma razão) | — |
| `ARQUIVO` | **o nome do arquivo** (`LEIA-ME.md:90`, `V001:3757-3761`) | `CAMINHO_ARQUIVO` + `NOME_ARQUIVO_ORIGINAL`, sempre os dois |

Dono da linha: funcionário (`ID_FUNCIONARIO`, `ANONIMO = FALSE`), candidato (`ID_FORMULARIO_CANDIDATO`, `ANONIMO = FALSE`) ou ninguém (`ANONIMO = TRUE`, os dois nulos). `ID_AVALIADO` nas avaliações (inclusive na anônima). Pergunta opcional em branco **não gera linha**.

`DATA_RESPOSTA`: com nome, **o mesmo instante em todas as linhas do envio** (é a única chave de agrupamento da Sugestão; `yaml:2158-2160`); anônima, **valor grosso, nunca a hora** ("só o dia", `yaml:2163`; "por exemplo o dia de abertura da rodada", `V001:2249-2251`). Sempre preenchida pela aplicação: se vier nula o trigger põe `NOW()` real, com a hora.

### D.5 O que a aplicação tem de garantir (o banco não garante)

1. **`ATIVO = FALSE` explícito ao criar** e nunca declarar o padrão do banco no EF em `ATIVO` (senão o rascunho nasce aceitando resposta, sem erro); `LEMBRETE_AUTOMATICO = TRUE` gravado pela API (`yaml:283-284`, `:857-858`; `LEIA-ME.md:89`, `:176`; roadmap:81 a).
2. **Uma resposta por pessoa** (por avaliado) em formulário com nome: conferir e gravar na mesma transação, com trava, para dois envios simultâneos não passarem (`yaml:2155-2157`). No anônimo a UQ da participação resolve; no candidato, a UQ do e-mail.
3. **Todas as obrigatórias respondidas** e ao menos uma pergunta respondida (`yaml:2146-2147`); alternativa da própria pergunta (a FK composta é a 2ª barreira).
4. **Prazo** (até 23:59 -03:00 do dia; `yaml:846-847`) e a situação (agendado, encerrado) antes de aceitar resposta.
5. **Anonimato:**
   - resposta sem autor, sem arquivo e com data grossa; participação com **UUID v4 gerado pela aplicação** (`Guid.NewGuid()`), devolvido no recibo; participação e respostas **sem ligação** entre si;
   - **ordem de exibição nunca é a de gravação**: textos em ordem alfabética, sem diferenciar maiúscula, em português (`yaml:1489-1490`; roadmap:222);
   - **mínimo de 5 destinatários** ao publicar (e de novo quando o agendador publica e quando reabre) (`yaml:10-13`, `:824-827`, `:872-876`);
   - **mínimo de 3 respostas por pergunta** e, na avaliação, **3 por avaliado**, os dois juntos (`yaml:22-26`);
   - **resultado, avaliados e exportação só depois de `ENCERRADO`** (`yaml:20-21`);
   - **nenhuma contagem antes de encerrar** (`respondidos`, `faltam`, taxa, `temRespostas`, `pendentes`), só `esperados` (`yaml:27-33`);
   - **perguntas, anonimato, tipo, sentido e destinatários congelados depois de publicar**, sem olhar se há resposta (`yaml:14-19`); o público é a **fotografia do dia da publicação** (uma linha por pessoa em `FORMULARIOS_DESTINATARIOS`, `yaml:836-838`);
   - **"quem falta"** = fotografia menos participações; nunca listado, só usado para o lembrete;
   - lembrete: e-mail **sigiloso** (linha apagada ao enviar), limite por formulário, dia e alcance na tabela de tarefas, resposta sempre igual, auditoria só "lembrete enviado" (`yaml:34-38`, `:1728-1738`);
   - **nada na auditoria** sobre responder ou participar (`yaml:2165-2167`; `20-fatia-1:2184-2192`).
6. **Sugestão:** separar os envios da mesma pessoa pelo instante (ver E.4.9).
7. **Quem deve responder** (com nome): resolver setor em pessoas **na hora** e incluir quem entrar depois (`yaml:834-836`; `V001:2302-2303`); avaliações pela regra de D.8.
8. **Alternativa já respondida não muda** (o trigger não enxerga a tabela de alternativas; `V001:2179-2180`).
9. **Reabrir recorrente:** apagar as respostas da rodada anterior **e** as participações (senão ninguém responde de novo; `V001:2352-2355`).
10. **Retenção de candidato:** job que apaga candidato vencido, as respostas dele e os arquivos (currículo e respostas de arquivo) (`V001:2267-2269`, `:3763-3766`).
11. **Token** por gerador seguro, não sequencial (`V001:2147-2149`): 32 bytes aleatórios em base64url dão exatamente os 43 caracteres do contrato (`yaml:828`).
12. **Limites que o banco não tem:** número de perguntas por formulário (LACUNA: o contrato não limita), 2 a 10 alternativas, 200 destinatários por chamada, 5 MB por arquivo.

### D.6 Matriz de edição (`editarFormulario`, `yaml:433-449`)

| Situação | Anônimo | Com nome |
|---|---|---|
| `RASCUNHO`, `AGENDADO` | tudo muda, inclusive `anonimo`. Passar a anônimo com pergunta `ARQUIVO` = 422 `ANONIMO_SEM_ARQUIVO`; com link = 422 `ANONIMO_SEM_LINK`; Sugestão anônima = 400 | idem |
| `PUBLICADO` | só `titulo` e `descricao`, **sempre** (não olha resposta); `anonimo`, `tipo`, `sentidoAvaliacao` = 409 `ANONIMATO_FIXO_APOS_PUBLICAR` | `anonimo` nunca muda (409). **Sem resposta:** o resto muda, mas formulário por link não sai de `ENTREVISTA` (422 `LINK_SO_PARA_ENTREVISTA`). **Com resposta:** só `titulo` e `descricao` |
| `ENCERRADO` | nada: 422 `FORMULARIO_ENCERRADO` | idem |

Perguntas: `RASCUNHO`/`AGENDADO` livre; `PUBLICADO` com nome: acrescentar sempre, editar/apagar só a não respondida (a respondida muda `obrigatoria` e `ordem`); `PUBLICADO` anônimo: 409 em tudo; `ENCERRADO`: 422. **LACUNA:** o que "o resto muda" quer dizer para formulário com nome publicado **com resposta** quando o corpo traz `tipo` diferente (400? 422 com qual código?). O contrato só diz "só `titulo` e `descricao`".

### D.7 Itens da verificação, na ordem (`yaml:688-702`, exemplos `:716-736`)

| codigo | OK | AVISO | BLOQUEIO |
|---|---|---|---|
| `TITULO_E_DESCRICAO` | "Título e descrição preenchidos." | "O formulário não tem descrição." | — |
| `PERGUNTAS` | "4 perguntas, 3 obrigatórias." (singular: "2 perguntas, 1 obrigatória.") | — | nenhuma pergunta (**sem texto no contrato**) |
| `ALTERNATIVAS` | "Toda pergunta de múltipla escolha tem pelo menos 2 alternativas." | — | "A pergunta 1 é de múltipla escolha e precisa de pelo menos 2 alternativas." + `perguntaId` |
| `ANONIMO_SEM_ARQUIVO` | (INFERIDO: o item só aparece quando bloqueia; o exemplo anônimo não o traz) | — | anônimo com pergunta de arquivo (**sem texto**) |
| `ANONIMO_RESULTADO_MINIMO` (só anônimo) | — | fixo: "O formulário é anônimo: o resultado só aparece depois de encerrar, e só das perguntas com 3 respostas ou mais." (na avaliação, também dos avaliados com 3 ou mais: **sem texto**) | — |
| `ANONIMO_PUBLICO_MINIMO` (só anônimo) | "N pessoas" se ≥ 5 | "escolha pelo menos 5 pessoas na etapa Publicação" se o público não foi escolhido | "O formulário anônimo está para 4 pessoas e precisa de pelo menos 5." |
| `DADO_SENSIVEL` | "Nenhuma pergunta pede dado sensível, como saúde, religião ou orientação sexual." | "A pergunta 2 parece pedir dado de saúde. Confira se ela é necessária." + `perguntaId` | nunca |

`podePublicar` = nenhum `BLOQUEIO`. A verificação usa o público **já guardado**; `publicarFormulario` a repete com o público do corpo.

### D.8 Quem deve responder (`yaml:832-845`, `:91-93`, `:1152-1153`)

| Caso | Quem responde | `esperados` |
|---|---|---|
| `LINK` | qualquer candidato com o link | `null` (`respondidos` = candidatos) |
| `PESSOAS`, com nome | as pessoas escolhidas (mais as incluídas por #13, cadastro e importação) | pessoas ativas |
| `SETORES`, com nome | quem está no setor **agora**; quem entrar ou trocar de setor depois também | calculado na hora |
| qualquer alvo, **anônimo** | a fotografia do dia da publicação, pessoa por pessoa | fixo ("os destinatários não mudam") |
| `AVALIACAO` + `GESTOR_AVALIA_EQUIPE` | as pessoas escolhidas são **gestores**; cada um avalia cada pessoa da equipe dele, menos ele | número de **avaliações** ("3 gestores (24 avaliações)") |
| `AVALIACAO` + `EQUIPE_AVALIA_GESTOR` | as pessoas ou os setores escolhidos **avaliam o gestor do seu setor**; quem é de setor sem gestor fica fora (e fora do mínimo de 5) | só quem tem gestor ("Todos os setores (24 avaliações)") |
| Sugestão (`OUTRO`) | os destinatários, quando quiserem, várias vezes | `null` (`respondidos` = envios) |

Equipe = setor do gestor, sem o gestor e sem quem está em admissão (ELENCO.md:57: Comercial = 8 pessoas, 21 a 28). Setores sem gestor no elenco: Recursos Humanos, Administrativo, Tecnologia e Compras.

### D.9 Armadilhas do banco

1. **Relógio falso × `NOW()`/`CURRENT_TIMESTAMP` reais.** `DATA_CRIACAO`, `DATA_RESPOSTA` e `FORMULARIOS_CANDIDATOS.DATA_CADASTRO` ficam com a hora real do servidor se vierem vazias. O `LEIA-ME.md:178` sugere mapear o `DEFAULT CURRENT_TIMESTAMP` como "gerado na inclusão"; aqui isso quebra os testes da retenção (relógio em 2026-10-07) e a ordem "mais recentes primeiro". **Sempre preencher pelo `IRelogio`** (decisão 1 da fatia 3) e conferir como `MapeamentoContraBancoTests` trata o padrão dessa coluna.
2. **`DATETIME` sem fração** nas tabelas do grupo (fuso -03:00, `ConversoresDeTempo.Brasil`): dois envios de Sugestão da mesma pessoa no mesmo segundo viram **um** (E.4.9); truncar ao segundo antes de gravar e antes de comparar `respondidoEmUtc`.
3. **Permutar `ORDEM`**: a UQ `(ID_FORMULARIO, ORDEM)` é conferida linha a linha; `+1000` e depois o valor final (`LEIA-ME.md:91`). O EF, atualizando em lote, pode violar no meio: usar duas gravações ou SQL com `ORDER BY`. O mesmo nas alternativas.
4. **Colunas geradas** `CHAVE_DESTINO` e `CHAVE_AVALIADO`: mapear como calculadas, fora do INSERT (roadmap:81 c). **`ID_PARTICIPACAO` sem geração de valor** e com `Guid` v4 em minúsculas (o `pattern` do recibo é minúsculo, `yaml:3627`).
5. **Padrões que mentem:** `ATIVO` (TRUE) e `ANONIMO` das respostas (FALSE): nunca omitir. `OBRIGATORIA` e `LEMBRETE_AUTOMATICO` têm padrão FALSE (omitir é inofensivo).
6. **Colunas anuláveis que a aplicação sempre preenche** (`TIPO_FORMULARIO`, `DATA_CRIACAO`, `TIPO_RESPOSTA`, `ORDEM`, `RESPOSTA`, `DATA_RESPOSTA`): a entidade as declara anuláveis para o teste de mapeamento passar (como `ADVERTENCIAS` na 4).
7. **`EMAIL` do candidato**: a UQ não diferencia caixa nem acento; normalizar (aparar, minúsculas) antes de conferir, e tratar o 1062 como `JA_RESPONDIDO`.
8. **Apagar em ordem** (FKs sem `ON DELETE`), com `ExecuteDelete` + evento explícito (`GuardaDeExecuteUpdateTests`), como na exclusão de aviso da fatia 4.
9. **ID sequencial das respostas** compartilhado entre formulários com nome e anônimos, e linhas de um mesmo envio anônimo com IDs vizinhos: aceito pelo dono (vale só contra quem usa a aplicação; `V001:2259-2265`), mas **nenhuma rota pode expor o id de resposta anônima** nem ordenar por ele.
10. **Trigger de pergunta conta qualquer resposta**, inclusive de candidato e anônima (por isso estão na mesma tabela; `V001:2217-2222`): depois de reabrir (respostas apagadas) a pergunta volta a ser editável.

---

## E. Decisões, contradições, lacunas e riscos

### E.1 Decisões do dono já registradas (não reabrir)

| Data | Decisão | Fonte |
|---|---|---|
| 2026-10-28/29 | Triggers do grupo não se reescrevem; tabelas novas sem trigger | roadmap:50; `V001:2187`, `:2271` |
| 2026-10-29 | **Sem quiz**, sem nota, sem nota mínima, sem eliminatória, sem "nota dada pelo RH"; tipos `ENTREVISTA`, `ADMISSAO`, `AVALIACAO`, `PESQUISA`, `OUTRO` (Sugestão) | roadmap:46, 66, 216; `yaml:82-83`, `:552-553`, `:2828` |
| 2026-10-29 | Os cinco tipos de pergunta ficam, inclusive envio de arquivo | `V001:3734-3735` |
| 2026-10-29 | **O RH marca o formulário como anônimo ao criar**; o anonimato vale **contra quem usa a aplicação**, não contra quem lê o banco ou o backup | roadmap:222; `V001:2259-2265`; `LEIA-ME.md:168`; `yaml:43-44`, `:291` |
| 2026-10-29 | Candidato externo por link e resposta anônima existem | roadmap:55 |
| 2026-10-29 | **Formulário recorrente sem histórico:** ao reabrir, as respostas da rodada anterior são apagadas (vale para todo recorrente); o RH exporta antes | roadmap:65, 214; `yaml:867-870` |
| 2026-10-29 | O lembrete do anônimo não mostra a lista ao RH | `yaml:1144` |
| 2026-10-29 e 30 | As oito regras do anônimo (mínimo de 5, congelamento, resultado só encerrado, mínimo de 3 por pergunta **e por avaliado**, sem contagem antes de encerrar, auditoria sem número e sem nome, o que nunca é devolvido, o que a proteção não cobre) | `yaml:9-50`; roadmap:9 |
| 2026-10-30 | **Lista fechada de e-mails:** nos formulários só o lembrete manual do RH, o manual do gestor e o automático de 7 dias; **sem** "formulário atribuído" e **sem** aviso de "formulário publicado" | CONVENCOES:114; `yaml:832-833`, `:1088-1089` |
| 2026-10-30 | Lembrete automático 7 dias antes do prazo, **ligado por padrão** | `yaml:2964`; `V001:3694-3695` |
| 2026-10-30 | **Sugestão** é caixa sempre aberta e aceita várias respostas da mesma pessoa | `yaml:1948`, `:2154`, `:2783` |
| 2026-10-30 | "Nota mínima para seguir", "Enviar o resultado por e-mail ao RH" e "Convidar aprovados" **não existem** | `yaml:863-865`; `LEIA-ME.md:172` |
| 2026-10-30 | Encerrar e excluir formulário pedem `CONFIRMACAO_NECESSARIA` quando há pendentes | CONVENCOES:106, 118 |
| 2026-10-30 | Auditoria: responder e participar de anônimo não geram evento; o lembrete do anônimo entra só como fato | `20-fatia-1:2184-2192` |
| 2026-11-02 | O botão "Lembrar quem falta" do **gestor** fica e entra na lista fechada | `yaml:1894`; CONVENCOES:114 |

### E.2 Abertas no contrato (`x-provisorio`; `PROVISORIOS.md:17-21`, `:50-54`, `:62`; roadmap:227)

1. Sugestão anônima (hipótese: não pode).
2. Excluir formulário encerrado (hipótese: pode, sem confirmação).
3. Verificação de dado sensível (hipótese: palavras-chave, só `AVISO`).
4. Proteção do link público (hipótese: só limite por IP, 30/min e 10/h, "valores de partida configuráveis"; captcha mudaria o corpo) e retenção (180 dias).
5. Respostas da admissão não chegam ao cadastro (hipótese: o RH copia à mão).
6. "Para responder" × "Disponível" só pelo tipo.
7. Prazo da rodada nova do recorrente (avança o mesmo número de dias).
8. Múltipla escolha com uma só opção.
9. Ciclo de avaliação só no título (fatia 7).

### E.3 Contradições

**Contrato × roadmap (vale o contrato):**
- **E-mail "formulário atribuído"**: o roadmap o lista como regra (roadmap:223); o contrato e as convenções o proíbem (`yaml:832-833`, `:1088`; CONVENCOES:114, decisão posterior, de 2026-10-30).
- **Mínimo por avaliado**: a nota de atualização do roadmap diz que "ainda espera o dono" (roadmap:9); o contrato já o fixa em 3, igual ao mínimo por pergunta (`yaml:22-26`).
- **Ordem das respostas anônimas**: o roadmap aceita "aleatória ou alfabética" (roadmap:222); o contrato manda **alfabética** (`yaml:1489-1490`).
- **"O mesmo link respondido várias vezes (limite)"** (roadmap:225): o contrato não tem limite por link; tem um envio por e-mail e 10 envios por hora por IP. A coluna `LINK_PUBLICO_LIMITE_RESPOSTAS` existe e fica sem uso (`yaml:2292`).
- **"5d. avaliações por ciclo"** (roadmap:210): no contrato não há ciclo, rodada nem rota de ciclo; a avaliação é um formulário comum com `avaliado`, e o "ciclo" é texto do título.
- **"Formulário anônimo não deixa rastro"** (roadmap:225) é mais forte que o contrato: a participação **fica** (pessoa × formulário, sem data) e o próprio contrato lista o que a proteção não cobre (`yaml:43-50`).

**Contrato × banco e scripts:**
- **Contagem de participações**: o comentário do script diz que "o RH vê a contagem" (`V001:2318-2319`); o contrato esconde qualquer contagem até encerrar (regra 5). Vale o contrato.
- **Data da resposta anônima**: "só o dia" (`yaml:2163`) × "por exemplo o dia de abertura da rodada" (`V001:2249-2251`). O dia **da resposta** permite cruzar com qualquer registro datado da presença da pessoa (log de acesso, ou "só a Ana estava trabalhando naquele dia"); o dia **da publicação da rodada** é igual para todos. Proposta: o dia da publicação (pergunta 7 de H).
- **`DATA_CADASTRO` do candidato**: o `LEIA-ME.md:178` e a sonda (`SondaMapeamentoDosScriptsTests.cs:138`, `:526`) a mapeiam como gerada na inclusão; o contrato precisa dela pelo relógio da aplicação (retenção, ordem). Com `ValueGeneratedOnAdd` o EF ainda envia o valor quando ele é preenchido: **preencher sempre**.
- **`regraDoBanco: CHK_FORMULARIOS_ANONIMO_SEM_LINK`** (`yaml:2611`): em todas as outras fatias `regraDoBanco` é a mensagem de um trigger; aqui é o **nome de um CHECK** e a API responde antes de o banco recusar. Copiar o texto fixo.

**Dentro do contrato:**
- **Alternativas**: `PerguntaComando` recusa com 400 a múltipla escolha fora de 2 a 10 (`yaml:2869-2870`), mas a verificação tem o `BLOQUEIO` "menos de 2 alternativas" com exemplo (`yaml:693`, `:733`). Com o 400, o bloqueio é inalcançável pela API. Proposta: valer o 400 e manter o item como defesa.
- **Prazo depois de publicar**: o x-provisorio da recorrência diz "o RH pode editar o prazo depois" (`yaml:2917`), mas `salvarPublicacaoFormulario` não vale para `PUBLICADO` (`yaml:751`) e `editarFormulario` só tem os detalhes. **Não há rota** para mudar prazo, recorrência ou lembrete de um formulário publicado.
- **Formulário `AGENDADO` não tem saída**: publicar de novo é 422 `FORMULARIO_JA_PUBLICADO` (`yaml:2572-2573`), não há "cancelar agendamento" nem "publicar agora"; só `salvarPublicacaoFormulario` (que "continua `AGENDADO`") e excluir. E o contrato não diz o que `agendarPara` faz nessa chamada num agendado (muda a data? vazio volta a rascunho ou mantém?).
- **`ANONIMO_SEM_ARQUIVO` ao publicar**: é item de `BLOQUEIO` da verificação (viraria `VERIFICACAO_COM_BLOQUEIO`) e também código próprio em `Erro422Publicacao` (`yaml:2573`). Falta a precedência.
- **`excluirFormulario` não declara 422** e `obterFormularioPublico` **não declara 400**: token fora do padrão tem de sair como 404 (não como 400 de validação de rota).
- **"Não vale para avaliação"** em `adicionarDestinatariosFormulario` (`yaml:1089-1090`) sem dizer qual erro.
- **`criarFuncionario`/`confirmarImportacao`** dizem "mesmo efeito" de #13, mas não declaram o 409 `ANONIMO_SEM_NOVOS_DESTINATARIOS` (formulário de admissão anônimo publicado) nem o `FUNCIONARIO_INATIVO`; e "não está publicado = 400" × "já encerrou = 422" exige ordem: encerrado → 422, rascunho/agendado → 400 (INFERIDO).
- **`FormularioEquipeItem.esperados` não aceita nulo**, coerente com a lista do gestor excluir link e Sugestão.

**Contrato × código existente:**
- A porta `IEnvioDoFormularioDeAdmissao` (`Application/Colaboradores/ICadastroDeFuncionarios.cs:70-77`) hoje responde 400 "O formulário de admissão escolhido não existe ou não está publicado." (`Infrastructure/Colaboradores/CaixasIndisponiveis.cs:26`). A fatia 5 registra a implementação real **depois** da padrão (`Infrastructure/DependencyInjection.cs:192`; o último registro vale). São **três** chamadores: cadastro (`CadastroDeFuncionarios.cs:105-108`, `:148`), readmissão (`ReadmissaoDeFuncionarios.cs:165-168`, `:111-114`) e importação.
- A validação de arquivo existente só conhece PDF, JPG e PNG e lê 8 bytes (`Application/Arquivos/IdentificadorDeTipoDeArquivo.cs:19`; `Application/Documentos/ArquivoDeDocumento.cs:55`). **Word não existe.**
- O limitador existente é de janela de 1 minuto, com duas políticas (`login`, `senha`; `Api/Configuracao/LimiteDeTentativas.cs:9-47`). Os "10 por hora" pedem política nova.
- Não existe `CONFIRMACAO_NECESSARIA` nem o título "Confirmação necessária." (`Api/Configuracao/TextosDeErro.cs:13`).
- `AuditoriaAcao` não tem "Encerrou", "Reabriu" nem "Respondeu" (`Domain/Auditoria/AuditoriaAcao.cs:4`).

### E.4 Riscos e lacunas, por assunto

**1. Anonimato × auditoria (o risco que o roadmap aponta).** O interceptor só registra entidades marcadas com `[Auditado]` e só os campos `[Auditar]`; INSERT e DELETE de entidade marcada **sempre** geram evento com autor e hora (`Infrastructure/Auditoria/AuditoriaInterceptor.cs:57`, `:172-192`). Requisição anônima não registra (`:59`). Consequência direta: **`FormularioResposta`, `FormularioParticipacao` e `FormularioCandidato` não podem levar `[Auditado]`**, e o caso de uso de responder não pode chamar `auditor.Registrar`. As entidades da fatia 4 já são auditadas à mão (não têm `[Auditado]`); seguir o mesmo caminho em `Formulario`, e nunca auditar `LINK_PUBLICO_CODIGO` (o token é segredo). Teste: depois de responder um anônimo, **nenhuma** linha nova em `AUDITORIA_EVENTOS`/`AUDITORIA_MUDANCAS`.

**2. Anonimato: outros vazamentos por contraste.**
- **Evento do lembrete**: se a auditoria só gravar "lembrete enviado" quando alguém recebeu, o log passa a dizer quando todos já responderam. No anônimo, gravar o evento **sempre** (com ou sem destinatário). O envio sigiloso já não gera `ENVIOU_EMAIL` (`Infrastructure/Emails/ProcessadorDeEmails.cs:132-136`).
- **Tempo de resposta do lembrete**: enfileirar 100 e-mails demora mais que enfileirar nenhum. Proposta: no anônimo, a rota só grava a marca e uma tarefa; quem resolve os destinatários e enfileira é a tarefa.
- **Erros que revelam resposta**: `PERGUNTA_JA_RESPONDIDA`, o 1451 da troca de `anonimo`, `temRespostas`, `pendentes`, a falta do pedido de confirmação. Toda rota de escrita em anônimo publicado decide **antes** de consultar respostas.
- **Depois de encerrar** os números são exatos, inclusive por equipe para o gestor: numa equipe de 2 (o elenco de teste), "2 de 2" identifica todos. O contrato aceita (regra 8), mas vale registrar.
- **Log de requisições**: um log de acesso com usuário, rota e hora de `POST /api/eu/formularios/8/respostas` refaz "quem participou e quando" (não "o quê"). A regra 6 fala só da auditoria; conferir o que o log da API grava (INFERIDO; não li a configuração de log).
- **`esperados` no anônimo**: "só `esperados` vem exato (os destinatários não mudam)". LACUNA: quem é desligado depois da fotografia continua contando? Proposta: conta a fotografia inteira (número fixo).
- **Fotografia × `publicacao.setores`**: guardando uma linha por pessoa (`yaml:836-838`) perde-se a lista de setores que o detalhe devolve. Proposta (INFERIDO): manter as linhas de setor **e** acrescentar as de pessoa; no anônimo publicado só as de pessoa definem quem responde.

**3. Avaliações.**
- **Avaliação da equipe anônima não tem resultado**: em `GESTOR_AVALIA_EQUIPE` cada pessoa é avaliada por **um** gestor, então nenhum avaliado chega a 3 respostas. O contrato não proíbe a combinação nem diz como contar o mínimo de 5 nela (só define para a avaliação do gestor, `yaml:10-12`). Pergunta 4 de H.
- **Equipe ou gestor mudam no meio**: com nome, "quem deve responder é calculado na hora" (`yaml:1152`). Se alguém troca de setor, a avaliação já feita sobre ele fica com `ID_AVALIADO` fora da lista de hoje; se o gestor do setor muda, as respostas sobre o antigo ficam órfãs e `esperados` muda. LACUNA: fotografia dos pares na publicação (a tabela de destinatários tem `ID_AVALIADO`) ou cálculo na hora. Pergunta 5.
- **O gestor avalia a si mesmo?** Em `EQUIPE_AVALIA_GESTOR` com `SETORES`, o gestor está no setor: fica fora (INFERIDO de "menos ele mesmo"). Gestor ausente, inativo ou setor sem gestor: fora da conta.
- **Mínimo por avaliado** conta **participações** de (formulário, avaliado), não linhas de resposta.
- **Fatia 7** vai precisar de duas leituras daqui: "a avaliação `GESTOR_AVALIA_EQUIPE` aberta para este gestor" (`70-fatia-7:2291-2293`) e "quem começa em até 7 dias e não respondeu o formulário de admissão" (`70-fatia-7:2378`). Não são da 5; deixar as consultas fáceis de expor.

**4. Recorrente.**
- **Reabrir apaga respostas de rodada ainda aberta?** O contrato só protege o anônimo ("só reabre depois de encerrado", `yaml:872-873`). Num formulário com nome sem prazo, ou com prazo maior que o intervalo, a leitura literal apaga as respostas no meio da rodada. Pergunta 2.
- **Recorrência com `alvo = LINK`** não é proibida: reabrir apagaria as respostas dos candidatos (e os candidatos? e os currículos?). Pergunta 2.
- **Arquivos das respostas apagadas** na reabertura: o contrato só fala dos arquivos na exclusão. Apagar também aqui (senão a limpeza de sobras os pega, se a fonte de referências existir).
- **Tarefa com chave eterna**: `IAgendador.CancelarAsync` deixa a chave `CANCELADA` e ela nunca é recriada (`Application/Agendamento/IAgendador.cs:31-34`). As chaves de publicar, encerrar e reabrir precisam levar o instante, como `PUBLICAR_AVISO:aviso={id}:em={instante}` (`Infrastructure/Comunicacao/AgendamentoDoAviso.cs:15`), e a tarefa relê o formulário antes de agir.
- **Encerrar à mão** cancela as próximas reaberturas (`yaml:1003`); o **encerramento pelo prazo** não cancela. LACUNA: se `recorrencia` continua aparecendo no detalhe depois de cancelada.
- **`publicadoEmUtc` da rodada nova**, `proximaReaberturaEmUtc` (a partir de quê: da publicação ou da última reabertura) e o que a auditoria grava na reabertura (ator Sistema; sem contagem no anônimo): LACUNA.
- **Anônimo recorrente** que cai abaixo de 5: continua `ENCERRADO` e a recorrência é cancelada (`yaml:874-876`).

**5. Agendado.**
- O agendador publica "uma única vez" no início do dia. No anônimo abaixo de 5 ele **não publica** e o formulário fica `AGENDADO` com data vencida (`yaml:824-827`). LACUNA: quem publica depois de o RH corrigir o público (a recuperação de hora em hora? nunca?).
- Com nome, se uma pessoa escolhida foi inativada entre agendar e publicar: LACUNA (proposta: publica, e a pessoa inativa simplesmente não conta).
- Trocar o alvo de um agendado por link exige anular o token (B.2).

**6. Prazo.** A tarefa de encerrar pode atrasar (ambiente parado, retentativas de 1, 5, 30 e 60 min, `Infrastructure/Agendamento/ExecutorDeTarefas.cs:29`). Até ela rodar, `ATIVO` está ligado e o trigger aceita resposta. A aplicação precisa de **uma** função de situação que trate "prazo vencido" como `ENCERRADO` em todas as leituras (lista, detalhe, responder, página pública, resultado do anônimo), senão o resultado anônimo abre antes de o banco fechar ou a resposta entra depois do prazo. "Hoje" sempre por `FusoBrasil.Hoje(relogio.AgoraUtc)`.

**7. Link público (superfície sem login).**
- **Limite por IP atrás de proxy**: o IP vem de `RemoteIpAddress`, que só é o do cliente com `ProxyReverso:Habilitado` (`Api/Configuracao/ProxyReverso.cs:16-60`). Sem isso, em produção, **todos os candidatos dividem o balde** (10 envios por hora para o mundo inteiro). Com isso, candidatos atrás do mesmo NAT (uma faculdade, uma lan house) dividem o balde.
- **Enumeração de e-mail**: o 409 "Já recebemos uma candidatura com este e-mail." diz a qualquer um com o link se um e-mail se candidatou. O contrato aceita; o limite de 10/h é a única barreira.
- **Armazenamento**: cada envio pode trazer currículo e N arquivos de 5 MB, sem limite de candidatos por link. Definir o teto da requisição pelo número de perguntas `ARQUIVO` (ou um teto fixo) e registrar.
- **Token no caminho**: aparece em qualquer log de acesso; não gravar o caminho das rotas públicas em log de aplicação (INFERIDO).
- **Mesmo 404** para token inexistente, de rascunho e fora do padrão; 429 vem antes de tudo.
- **Aceite do aviso**: `aceitouAvisoDePrivacidade` não tem coluna; não fica prova do aceite. LACUNA (pergunta 8).
- **Requisição anônima não gera auditoria pelo interceptor**; o contrato pede só que a auditoria não grave o valor dos dados (`yaml:2359`). LACUNA: se a candidatura gera evento (ator Sistema, sem dados).
- **Matriz de perfis**: manda `2147483647` no `{token}` e `{}` em JSON (`tests/.../Matriz/MatrizDePerfis.cs:20`, `:88-91`); as rotas públicas só precisam não responder 401/403. Se a rota tiver limite, a matriz gasta o balde de `127.0.0.1`: pôr os limites novos em 1000 no `ApiFactory` (`Infra/ApiFactory.cs:216-218`).

**8. Arquivos (currículo e respostas de arquivo).**
- **Word**: `.docx` é um ZIP (`PK\x03\x04`) e só se distingue de qualquer outro ZIP abrindo-o (procurar `word/document.xml`); `.doc` é OLE2 (`D0 CF 11 E0 A1 B1 1A E1`), a mesma assinatura de `.xls`, `.ppt` e `.msi`. LACUNA: até onde conferir. O arquivo só sai como anexo por link temporário, o que limita o dano.
- **Currículo sem nome original**: `FORMULARIOS_CANDIDATOS` só tem `CAMINHO_CURRICULO`; o nome do download é gerado (por exemplo `curriculo-<nome>.pdf`, com a extensão do tipo detectado).
- **Receita de envio** (fatias 1c e 4): validar tudo → gravar o blob → transação curta → apagar o blob se falhar (`EnvioDeArquivoDeDocumento.GravarEEfetivarAsync`, `Application/Documentos/ArquivoDeDocumento.cs:82-89`, serve para um arquivo; aqui são vários por envio). O contrato diz "o arquivo só é guardado depois de o banco aceitar" (`yaml:2350-2351`), a mesma frase do holerite.
- **Sobras**: registrar `IFonteDeReferenciasDeArquivo` do prefixo novo (`formularios/`), lendo `FORMULARIOS_RESPOSTAS.CAMINHO_ARQUIVO` e `FORMULARIOS_CANDIDATOS.CAMINHO_CURRICULO`; sem ela a limpeza não toca a pasta (`Infrastructure/Arquivos/Tarefas/LimparArquivosOrfaosTarefa.cs:17`).
- **Download pelo RH** (#18, #19): o contrato não pede evento de auditoria (o do holerite pede). LACUNA (pergunta 8).
- `arquivoIndice` fora da lista, arquivo enviado e não citado, dois itens para a mesma pergunta: 400 (o contrato só nomeia os campos).

**9. Sugestão.** O envio é identificado por (pessoa, `DATA_RESPOSTA`) e a coluna não tem fração de segundo: dois envios no mesmo segundo se fundem, e `respondidoEmUtc` na consulta tem de bater ao segundo. Proposta: truncar ao segundo e, dentro da transação com a pessoa travada, se já houver envio naquele segundo, usar o segundo seguinte.

**10. Uma resposta por pessoa (com nome).** Sem chave única: travar (a pessoa, como `OperacoesComTrava` no ponto, ou a linha do formulário) e conferir dentro da transação. Teste de duas abas obrigatório.

**11. Exportação CSV.** `CsvSeguro` já neutraliza `=`, `+`, `-`, `@`, TAB e CR, com BOM, `;` e CRLF (`Application/Comum/Exportacao/CsvSeguro.cs:18`, `:35`). O exemplo do contrato usa `\n` (`yaml:1704`); `RespostaConformeAoContrato` só confere o tipo de mídia do CSV. Nome do arquivo: `formulario-{id}-respostas.csv` (`yaml:1700`). Formato da data: `2026-10-02 11:10`, em -03:00. LACUNA: o cabeçalho e as "seções por avaliado" do CSV anônimo (o contrato dá só as colunas `pergunta`, `opcao`, `total`, `percentual`), e como sai a pergunta de texto.

**12. Gestor.** "Só quem, nunca o conteúdo": nenhuma rota `/api/gestor/formularios` devolve resposta. As duas avaliações e a Sugestão são 404 nas três rotas; gestor sem setor recebe lista vazia (padrão das fatias 3 e 4). `observacao` vem de `IFeriasDoPonto.NoPeriodoAsync(hoje, hoje)` (`Application/Ponto/ICalendarioDoPonto.cs:27-29`), que já lê `FERIAS` aprovadas. A lista (#24) só traz `PUBLICADOS`, mas #25 responde também para o encerrado (é onde os números do anônimo aparecem).

**13. Arredondamentos.** Taxa inteira: 5 de 8 = **63** (`yaml:1810`), logo `MidpointRounding.AwayFromZero` (o padrão do .NET daria 62). Percentual e média com uma casa (`yaml:1531-1533`, `:1543`).

**14. `minutosEstimados`** é "regra do back-end" e os exemplos não seguem uma fórmula única (12 perguntas → 10 min; 20 → 8; 3 → 2; 18 → 15; avaliação de 3 perguntas para 8 pessoas → 16). Escolher uma regra por tipo de pergunta, multiplicar pelos avaliados, mínimo 1, e registrar.

**15. "Para responder" × "Disponível"** só define quatro tipos (`yaml:2820`); `ENTREVISTA` para funcionários (alvo setores ou pessoas) fica sem regra. LACUNA.

**16. Quem conta como destinatário de um setor:** pessoas ativas; quem está "em admissão" (admissão futura)? A equipe do gestor as exclui (`Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs:13`; ELENCO.md:57), mas o formulário de admissão é justamente para elas (por `PESSOAS`). Pergunta 6.

**17. Auditoria dos atos do RH** entra normalmente (`20-fatia-1:2191`): criar (`CRIOU`), editar e encerrar (`ALTEROU`), publicar (`PUBLICOU`), excluir (`EXCLUIU`), lembrete (`ENVIOU_EMAIL`), área `FORMULARIOS`. LACUNA: evento para responder formulário **com nome** (o contrato não pede).

**18. Textos que faltam** (B.1, "sem exemplo"): `FORMULARIO_JA_PUBLICADO`, `FORMULARIO_SEM_LINK`, `FORMULARIO_NAO_E_AVALIACAO`, `FORMULARIO_NAO_PUBLICADO` ao encerrar, `PERGUNTA_JA_RESPONDIDA` ao excluir, `FORMULARIO_POR_LINK` no lembrete, `FORMULARIO_SUGESTAO` em minhas respostas, `FUNCIONARIO_INATIVO` ao responder, os bloqueios `PERGUNTAS` e `ANONIMO_SEM_ARQUIVO` da verificação, o plural e o singular de `pendentes` (e "avaliações"). O executor escreve e registra; o teste de mensagens precisa de lista de exceções, como `MensagensDaComunicacaoTests.cs:86-88`.

---

## F. Infraestrutura: o que reutilizar e o que é novo

### F.1 Reutilizar

| Peça | Onde | Como entra na 5 |
|---|---|---|
| `GrupoRh`, `GrupoGestor`, `GrupoEu`, **`GrupoPublico`** (`MapGroup("/api/publico").AllowAnonymous()`) | Api/Autorizacao/FamiliasDeRota.cs:10-19 | o grupo público já existe e ainda não tem rota de negócio |
| `LimiteDeTentativasExtensions` (políticas `login` e `senha`, janela fixa por IP, `OnRejected` com `Retry-After` e o texto do contrato) | Api/Configuracao/LimiteDeTentativas.cs:9-56 | molde para duas políticas novas (consulta 30/min, envio 10/h), configuráveis |
| `UsarProxyReverso` (`X-Forwarded-For`, `ProxiesConfiaveis`) | Api/Configuracao/ProxyReverso.cs:16-60 | de onde vem o IP do candidato |
| `ProduzProblemas(params int[])`; `Problemas.Criar` (extensões em camelCase) | Api/Configuracao/TextosDeErro.cs:43; TratamentoDeErros.cs:20-61 | declarar os erros de cada rota |
| `ProblemaDeApiException(status, titulo, detalhe, codigo, extensoes, cabecalhos)` | Application/Comum/ProblemaDeApiException.cs:12 | `CONFIRMACAO_NECESSARIA` (título próprio + `pendentes`), erros com `regraDoBanco` |
| `ErroDeNegocioException`, `ConflitoException`, `ErroDeValidacaoException`, `ColetorDeErros`, `NaoEncontradoException`, `OuNaoEncontrado()` | Application/Comum/Excecoes.cs:6-76 | 422, 409, 400 (todos os campos juntos), 404 |
| `MensagensDaComunicacao` + `RegrasDoBancoDaComunicacao` + teste contra o `openapi.yaml` montado | Application/Comunicacao/Mensagens/MensagensDaComunicacao.cs:12-114; tests/.../Contrato/MensagensDaComunicacaoTests.cs | molde de `MensagensDosFormularios` |
| `TradutorDeRecusaDaComunicacao.Traduzir` (45000 pela mensagem, 1062 pelo nome da chave) | Infrastructure/Comunicacao/TradutorDeRecusaDaComunicacao.cs:29 | molde do tradutor da 5 |
| `ErrosDoBanco` (`EhChaveRepetida`, `NomeDaChaveRepetida`, `EhChaveEstrangeiraEmUso`, `NomeDaChaveEstrangeira`) | Infrastructure/Persistencia/ErrosDoBanco.cs:15-55 | 1062 de candidato, participação e destinatário; 1451 da troca de `anonimo` |
| `FormulariosDoContrato` (multipart com campo JSON em array e array de binários) | Api/Configuracao/FormulariosDoContrato.cs:36, :90-106 | acrescentar `responderFormulario` e `responderFormularioPublico` |
| `LeituraDeFormulario.LerAsync` (415 e 400); leitura de `Files.GetFiles("arquivos")` e do JSON como texto | Api/Comum/LeituraDeFormulario.cs:13; Api/Comunicacao/HoleritesRhEndpoints.cs:101-121; Application/Comunicacao/Holerites/EnviarHoleritesEmLoteCasoDeUso.cs:109-159 | o lote de holerites é o molde de `respostas` + `arquivos` |
| `RequestSizeLimitAttribute` + `RequestFormLimitsAttribute` + `.DisableAntiforgery()` | Api/Comunicacao/HoleritesRhEndpoints.cs:82 | teto das duas rotas multipart |
| `EnumsDeParametrosDoContrato`, `EnumsDeConsulta.Ler`, `ParametrosDeLista`, `RespostaPaginada<T>`, `ToRespostaPaginadaAsync`, `MapaDeOrdenacao`, `BuscaLike.Contem` | Api/Configuracao/EnumsDeParametrosDoContrato.cs:26; Application/Comum/Consultas/; Infrastructure/Persistencia/ConsultasEf.cs:13, :25 | listas #1, #14, #15, #27 (`MapaDeOrdenacao` na #1) |
| `CsvSeguro.Texto/Escrever`, `ArquivoExportado`, resposta `text/csv` com `Content-Disposition` | Application/Comum/Exportacao/CsvSeguro.cs:18, :35; Api/Auditoria/AuditoriaEndpoints.cs:32, :52-54 | #22 |
| `IAgendador` (`AgendarAsync`, **`RegistrarComoConcluidaAsync`**, `CancelarAsync`, `ReativarAsync`), `ITarefaAgendada`, `ChaveDeIdempotencia.Montar` | Application/Agendamento/IAgendador.cs:19-34; ITarefaAgendada.cs:12-21; Application/Comum/ChaveDeIdempotencia.cs:26 | publicar, encerrar, reabrir, lembrete automático, a marca "lembrete de hoje", retenção. **Não há consulta "a chave existe?"**: só tentar inserir |
| `AgendamentoDoAviso` + `PublicarAvisoTarefa` (chave com o instante; a tarefa relê e só age se o estado ainda bate; `RecuperarAsync`) | Infrastructure/Comunicacao/AgendamentoDoAviso.cs:11-37; PublicarAvisoTarefa.cs:19-51; uso na transação em Application/Comunicacao/Avisos/CasosDeUsoDosAvisosDoRh.cs:92-106, :163-167 | molde das quatro tarefas de formulário |
| `IFilaDeEmails` (`EnfileirarAsync` devolve `false` no repetido; `EnfileirarLoteAsync` embaralha se houver sigilosa; `CancelarPendentesAsync`), `EmailParaEnfileirar(..., Sigiloso)` | Application/Emails/IFilaDeEmails.cs:7-33; Infrastructure/Emails/FilaDeEmails.cs:35-76 | lembrete com nome (chave formulário + pessoa + dia → `ignorados`) e **sigiloso** no anônimo. **O sigiloso está pronto e testado, sem nenhum chamador** (linha apagada ao enviar e ao falhar, sem `ENVIOU_EMAIL`: Infrastructure/Emails/ProcessadorDeEmails.cs:132-136, :163-165) |
| `IRenderizadorDeEmail`/`ModeloDeEmail`; `AvisoLembreteRenderizador`; `LembrarQuemNaoViuAvisoCasoDeUso` | Application/Emails/IRenderizadorDeEmail.cs:7-13; Infrastructure/Emails/AvisoLembreteRenderizador.cs:12; Application/Comunicacao/Avisos/CasosDeUsoDasLeiturasDoAviso.cs:41-74 | molde do renderizador `FORMULARIO_LEMBRETE` e do caso de uso de lembrete |
| `IArmazenamentoDeArquivos` (`GravarAsync`, `CriarLinkTemporarioAsync`, `ApagarAsync`), `CaminhosDeArquivo.Novo`, `NomeParaDownload.Limpar`, `LinkTemporario` | Application/Arquivos/IArmazenamentoDeArquivos.cs:4-26; CaminhosDeArquivo.cs:23; NomeParaDownload.cs:15 | pasta nova (`formularios`); download como anexo (#18, #19) |
| `ArquivoRecebido`, `ValidacaoDeArquivoDeDocumento.ValidarAsync/ValidarPdfAsync`, `EnvioDeArquivoDeDocumento.GravarEEfetivarAsync` | Application/Documentos/DocumentosDtos.cs:34; ArquivoDeDocumento.cs:17-89 | a validação precisa de variante "PDF ou Word"; o envio grava **um** arquivo por vez |
| `IFonteDeReferenciasDeArquivo` + `LimparArquivosOrfaosTarefa` | Application/Arquivos/IFonteDeReferenciasDeArquivo.cs:7; Infrastructure/Comunicacao/FonteDeReferenciasDosHolerites.cs:12 | fonte do prefixo `formularios/` |
| `IEscopoDoUsuario`, `EquipeDoGestor`, `DaEquipe(e)` | Application/Escopo/IEscopoDoUsuario.cs:14-41; Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs:13 | rotas do gestor; `new EquipeDoGestor(...)` para a equipe de um setor qualquer (Infrastructure/Gestores/GestoresRepositorio.cs:88) |
| `IGestorDoFuncionario.ResolverAsync(setorId, funcionarioId)` (devolve `GestorId` e nome, **não** o `FuncionarioId` do gestor) | Application/Colaboradores/ICadastroDeFuncionarios.cs:42-56; Infrastructure/Colaboradores/GestorDoFuncionario.cs:18-23 | o join serve de base para "o gestor do setor de cada pessoa" |
| `IAuditor` (`Registrar`, `RegistrarEGravarAsync`, `RegistrarComo`, `SuspenderRegistroAutomatico`), `AuditoriaArea.Formularios` | Application/Auditoria/IAuditor.cs:28-52; Domain/Auditoria/AuditoriaArea.cs:17 | eventos do RH e do agendador, à mão |
| `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`, `OperacoesComTrava` | Application/Autenticacao/Contratos.cs:34-45; Infrastructure/Colaboradores/OperacoesComTrava.cs | responder (conferir e gravar juntos), excluir, reabrir |
| `IRelogio`, `FusoBrasil` (`Hoje`, `InicioDoDiaEmUtc`, `DiaLocalDe`), `ConversoresDeTempo.Brasil`, `InstanteAoSegundo` | Application/Autenticacao/Contratos.cs:76; Application/Comum/Tempo/FusoBrasil.cs:13-44; Infrastructure/Persistencia/ConversoresDeTempo.cs:19; Application/Comunicacao/Regras/ | prazo, agendamento, encerramento nominal, datas do grupo em -03:00 |
| `IConfiguracaoDaEmpresa.ObterAsync` (`NomeEmpresa`; nulo sem configuração) | Application/Empresa/IConfiguracaoDaEmpresa.cs:4-12 | `empresaNome` e o 422 próprio da página pública (**não** usar `ObterOuFalharAsync`: o texto é outro) |
| `IFeriasDoPonto.NoPeriodoAsync` | Application/Ponto/ICalendarioDoPonto.cs:27-29; Infrastructure/Ponto/FeriasDoPonto.cs:10-18 | `observacao` do #25 |
| `IFonteDeContadorDoMenu` + `ContadorDoMenu.FormulariosPendentes` | Application/Painel/IFonteDeContadorDoMenu.cs:10; ContadorDoMenu.cs:10; molde Infrastructure/Comunicacao/ConsultaDeAvisosDaPessoa.cs:101-106 | a bolinha "Formulários" (= `contagens.paraResponder`) |
| **`IEnvioDoFormularioDeAdmissao`** (`ConferirAsync`, `AdicionarDestinatariosAsync`) | Application/Colaboradores/ICadastroDeFuncionarios.cs:70-77; padrão em Infrastructure/Colaboradores/CaixasIndisponiveis.cs:23-30 | a 5 registra a implementação real (o último registro vale) |
| `FuncionarioResumo` | Application/Colaboradores/FuncionarioResumo.cs | todas as listas |
| Sonda já escrita para estas tabelas (`ATIVO = false`, participação v4, colunas calculadas, `DATA_CADASTRO`, `LEMBRETE_AUTOMATICO`) | tests/.../Persistencia/SondaMapeamentoDosScriptsTests.cs:59-247, :472-527 | o mapeamento real copia as escolhas da sonda |
| Testes: `ApiFactory` (relógio fixo em `ElencoDeTeste.Hoje` = 2026-10-07T12:42Z, `Armazenamento`, `Enviador`, agendador desligado: `IExecutorDeTarefas.ExecutarCicloAsync` ou a tarefa direto), `ApiDeAcesso.ObterAsync(mysql, nameof(Classe))` (um banco por classe), `AfirmarConformeAoContratoAsync`, `MatrizDePerfis`, `ContratoDivergenciaTests.OperationIdsNoEscopo`, `ContratoEStatusTests`, `MapeamentoContraBancoTests`, `ConformidadeDoModeloTests`, `GuardaDeExecuteUpdateTests`, decoradores de consulta para provar corrida (`AvisosRhTests.cs:254`, `AdvertenciasGestorTests.cs:378`), `AdvertenciasDeTeste.NovoSetorAsync/NovoMembroAsync/ComLoginAsync` | back-end/tests/RHSystem.Api.IntegrationTests/ | **o elenco de teste tem 12 pessoas ativas e a equipe do Gestor tem 2**: o anônimo (5 destinatários, 3 respostas) exige criar gente |

### F.2 Novo (não existe)

- **Entidades e mapeamentos** das sete tabelas (`Domain/Formularios`, `Infrastructure/Persistencia/Configuracoes`), tirando-as de `tests/.../Persistencia/tabelas-sem-mapeamento.txt` (linhas 7-13).
- **Regras puras**: situação do formulário (com o prazo), matriz de edição, validação de detalhes, pergunta e publicação, verificação (com a lista de palavras do dado sensível), quem deve responder e os pares de avaliação, mínimos do anônimo, agregação do resultado, ordem alfabética em português, `minutosEstimados`, `quemRespondeResumo`, próxima reabertura, gerador de token.
- **Catálogo de erros** `MensagensDosFormularios` + teste contra o contrato; tradutor de recusa da 5; o problema `CONFIRMACAO_NECESSARIA`.
- **Reconhecimento de Word** (`.doc`, `.docx`) e a validação "PDF ou Word até 5 MB".
- **Envio de vários arquivos** num só pedido com tudo ou nada.
- **Duas políticas de limite por IP** (com as chaves de configuração e os valores altos no `ApiFactory`).
- **Tarefas**: `PUBLICAR_FORMULARIO`, `ENCERRAR_FORMULARIO`, `REABRIR_FORMULARIO`, `LEMBRETE_FORMULARIO` (nomes de `V001:3213-3214`) e a de retenção de candidatos (nome a escolher).
- **Renderizador** `FORMULARIO_LEMBRETE` (nome de `V001:3123`).
- **Fontes**: referências de arquivo de `formularios/`, contador `FormulariosPendentes`, implementação de `IEnvioDoFormularioDeAdmissao`.
- **Endpoints** `FormulariosRhEndpoints`, `FormulariosGestorEndpoints`, `FormulariosEuEndpoints`, `FormulariosPublicoEndpoints` (ids com `:int`; `{token}` sem restrição que produza 400).
- **Opções** configuráveis: limites por IP, `retencaoDias` (180), texto do aviso de privacidade.
- **STATUS.md**: 32 linhas novas e a correção das linhas 32-34 (contadores), 46 (`criarFuncionario`) e 77 (`confirmarImportacao`).
- **Testes que mudam**: `CadastroDeFuncionariosTests.cs:245-258`, `CriarFuncionarioTests.cs:312`, `ReadmissaoDeFuncionariosTests.cs:356-369`, `ConfirmarImportacaoTests.cs:591-601` (afirmam o 400 da caixa indisponível) e `ContadoresDoMenuTests.cs:65-66` (afirma zero).

---

## G. Proposta de incrementos e tarefas

O contrato não recomenda ordem. Mantenho os quatro incrementos do roadmap (roadmap:210), com um ajuste: os **pares de avaliação** entram cedo, na regra de "quem deve responder" (T6), porque `esperados`, o mínimo de 5 e os lembretes dependem dela; o incremento 5d fica só com o que é exclusivo da avaliação. Cada tarefa entrega os `operationId` em `OperationIdsNoEscopo`, a linha no `STATUS.md`, `AfirmarConformeAoContratoAsync` nas respostas e a matriz de perfis passando. **[S]** = toca segurança (público, candidato ou anonimato). Nomes de arquivo são sugestões.

### 5a. Construtor (#1-#13)

**T1. Mapeamento das sete tabelas.** Entidades `Formulario`, `FormularioPergunta`, `FormularioAlternativa`, `FormularioResposta`, `FormularioCandidato`, `FormularioDestinatario`, `FormularioParticipacao` (**`FormularioResposta`, `FormularioCandidato` e `FormularioParticipacao` sem `[Auditado]`**), conversores -03:00, anuláveis do banco, colunas calculadas, `ID_PARTICIPACAO` sem geração. Entrega: `MapeamentoContraBancoTests` e `ConformidadeDoModeloTests` verdes, tabelas fora de `tabelas-sem-mapeamento.txt`, sonda que grava formulário `Ativo = false`, resposta anônima, participação v4 e candidato com data do relógio falso, e relê. **[S]**

**T2. Regras puras do formulário.** Situação (as quatro, com prazo vencido), matriz de edição (D.6), validação de `FormularioDetalhesComando`, `PerguntaComando` e `FormularioPublicacaoComando`, instantes (início do dia agendado, fim do prazo, encerramento nominal), gerador de token. Testes de unidade e mutação. Arquivos: `Application/Formularios/Regras/*`.

**T3. Erros da fatia 5.** `MensagensDosFormularios` (B.1) com teste contra o contrato e lista de exceções para os textos sem exemplo; `RegrasDoBancoDosFormularios` (as cinco mensagens de trigger e o nome do CHECK); tradutor (45000, 1062 por chave, 1451 da FK do anônimo); fábrica do `CONFIRMACAO_NECESSARIA` (conferir que `pendentes: null` **sai no JSON**, porque o schema o exige).

**T4. Detalhes** (#2, #3, #4). Criar (rascunho, `ATIVO = false`, `LEMBRETE_AUTOMATICO = true`, autor, data do relógio), obter (com `temRespostas` nulo no anônimo), editar pela matriz, decidindo o anônimo publicado antes de olhar respostas. Arquivos: `Application/Formularios/Construtor/*`, `Infrastructure/Formularios/FormulariosRepositorio.cs`, `Api/Formularios/FormulariosRhEndpoints.cs`. **[S]** (anonimato)

**T5. Perguntas** (#6, #7, #8). Ordem com permuta em dois passos, alternativas (manter, criar, apagar), pergunta respondida (422 com `regraDoBanco`) × anônimo publicado (409 sempre), `ARQUIVO` em anônimo. Entrega: mover a pergunta 3 para a posição 1 sem 1062; duas abas acrescentando no mesmo lugar. **[S]** (anonimato)

**T6. Quem deve responder.** Uma porta só (`IDestinatariosDoFormulario`) que devolve os pares (pessoa, avaliado ou nulo) para: pessoas, setores (na hora), fotografia do anônimo, `GESTOR_AVALIA_EQUIPE` e `EQUIPE_AVALIA_GESTOR`; contagem de `esperados`; `quemRespondeResumo`. Testes com setor sem gestor, gestor dentro do setor, pessoa inativa, em admissão. Sem rota nova.

**T7. Publicação guardada e verificação** (#10, #9). Guardar alvo, destinatários, prazo, recorrência e lembrete (não o `agendarPara` de rascunho); anular o token ao trocar o alvo; verificação com os sete itens e a lista de palavras. Entrega: os dois exemplos de `yaml:716-736`.

**T8. Publicar, agendar e encerrar** (#11, #12). Mínimo de 5, verificação com o público do corpo, publicar agora ou agendar, token por link, fotografia do anônimo; tarefas `PublicarFormularioTarefa` (relê, confere o mínimo, publica uma vez) e `EncerrarFormularioTarefa` (instante nominal); encerrar à mão com confirmação (sempre no anônimo) por UPDATE condicional. Entrega: agendado publica uma vez mesmo rodando a tarefa duas vezes; anônimo agendado que caiu para 4 não publica; duas abas encerrando. **[S]** (anonimato)

**T9. Lista do RH e exclusão** (#1, #5). Lista com as quatro ordenações, `contagemPorTipo`, contagens por tipo de alvo e nulos do anônimo; exclusão física na ordem do contrato, arquivos apagados depois da transação, tarefas e e-mails pendentes cancelados, evento `EXCLUIU` com ou sem contagem. **[S]** (anonimato)

**T10. Destinatários e a caixa do cadastro** (#13). A rota e a implementação de `IEnvioDoFormularioDeAdmissao` registrada depois da padrão; cadastro, readmissão e importação passam a incluir de verdade, na mesma transação. Entrega: os quatro testes de recusa viram testes do caminho feliz e dos erros 400/422; `incluidosNoFormularioDeAdmissao` conta certo.

### 5b. Responder e acompanhar (#14, #16, #18, #20, #22-#30)

**T11. Meus formulários e abrir** (#27, #28) e a bolinha do menu (`FonteDeContadorDeFormularios`). Entrega: `contagens.paraResponder` igual ao contador; respondido some de "Todos"; Sugestão sempre `DISPONIVEL`; 404 para quem não deve responder.

**T12. Responder com nome** (#29, parte) e minhas respostas (#30). Leitura do multipart, validação por tipo, obrigatórias, gravação por tipo (D.4), mesmo instante no envio, trava contra duas abas, Sugestão com vários envios, arquivos PDF ou Word com tudo ou nada e a fonte de referências de `formularios/`. Arquivos: `Application/Formularios/Responder/*`, `Application/Arquivos` (Word), `Api/Configuracao/FormulariosDoContrato.cs`. **[S]** (arquivos)

**T13. Responder anônimo** (#29, o resto). Resposta sem autor e com data grossa, participação v4, recibo, 409 pela chave única, pessoa inativa barrada pela aplicação, **nenhum evento de auditoria**. Entrega: depois do envio não há linha de auditoria, a resposta não tem autor nem hora, a participação não tem data; duas abas = um 201 e um 409. **[S]** (anonimato)

**T14. Quem respondeu (RH)** (#14, #16, #18). Lista calculada na hora com filtros, resumo, anônimo sem itens e sem contagem até encerrar, Sugestão por envio; ver respostas de uma pessoa (inclusive por `respondidoEmUtc`); download do arquivo de resposta. **[S]** (anonimato)

**T15. Resultado e exportação** (#20, #22). Agregação por tipo, mínimo de 3 por pergunta, `perguntasOcultas`, textos em ordem alfabética, 409 antes de encerrar; CSV com nome (uma linha por resposta) e anônimo (só o agregado) com `CsvSeguro`. Entrega: os exemplos de `yaml:1513-1621`; texto começando com `=` neutralizado; anônimo com 2 respostas numa pergunta a esconde. **[S]** (anonimato)

**T16. Lembretes do RH e o automático** (#23). Com nome: chave formulário + pessoa + dia, `enviados` e `ignorados`. Anônimo: marca por formulário, dia e alcance (`RegistrarComoConcluidaAsync`), e-mails sigilosos embaralhados, resposta constante, evento sempre e sem número. `LembreteDeFormularioTarefa` 7 dias antes do prazo (não agenda se o prazo está a 7 dias ou menos; uma por rodada). Renderizador `FORMULARIO_LEMBRETE`. **[S]** (anonimato)

**T17. Gestor** (#24, #25, #26). Só a equipe, sem conteúdo, avaliações e Sugestão = 404, `observacao` das férias, lembrete com alcance do setor. Entrega: o gestor não acha rota que devolva resposta; anônimo aberto só com `esperados`. **[S]** (anonimato)

**T18. Recorrência.** `ReabrirFormularioTarefa`: apaga respostas, participações e arquivos, religa `ATIVO`, limpa o encerramento, recalcula prazo e próxima reabertura, agenda encerramento e lembrete da rodada; anônimo só reabre encerrado e com 5 ou mais ativos da fotografia. Depende das respostas das perguntas 2 e 3 de H. **[S]** (anonimato)

### 5c. Link público e entrevista (#15, #17, #19, #31, #32)

**T19. Página pública** (#31). `GrupoPublico`, política de 30/min por IP, mesmo 404 para inexistente, rascunho e token fora do padrão, 422 de agendado, encerrado e empresa sem configuração, aviso de privacidade montado com `retencaoDias`. Arquivos: `Api/Formularios/FormulariosPublicoEndpoints.cs`, `Api/Configuracao/LimiteDeTentativas.cs`, `Infra/ApiFactory.cs` (limites altos). **[S]** (público)

**T20. Candidatura** (#32). Política de 10/h, validação de nome, e-mail, telefone e aceite, currículo e arquivos PDF ou Word, tudo ou nada, 409 pela chave única do e-mail (inclusive em corrida), data do relógio, nada de valor pessoal na auditoria nem em log. **[S]** (público, candidato)

**T21. Candidatos para o RH** (#15, #17, #19) e o CSV do formulário por link (parte do #22). Só RH; candidato de outro formulário = 404; currículo como anexo. **[S]** (candidato)

**T22. Retenção de candidatos.** Tarefa diária que apaga candidatos com mais de `retencaoDias`: respostas, candidato e, depois da transação, os arquivos; evento do Sistema sem dado pessoal. Entrega: com o relógio avançado 181 dias, o candidato, as respostas e os blobs somem; o de 179 dias fica. **[S]** (candidato)

### 5d. Avaliações (#21 e `avaliadoId` em #16, #20, #28, #29, #30)

**T23. Responder avaliação.** `avaliados` em #28, `avaliadoId` em #29 (422 `AVALIADO_INVALIDO`), `restantes`, `total` do cartão, uma resposta por avaliado (com nome e anônima), linhas por avaliação em #14 e `avaliadoId` em #16 e #30. **[S]** (anonimato)

**T24. Resultado por avaliado** (#21 e #20 com `avaliadoId`). `esperadas`, `totalRespostas` nulo abaixo de 3, `resultadoLiberado`, 422 `RESULTADO_SEM_RESPOSTAS_SUFICIENTES`, seções por avaliado no CSV anônimo. Entrega: o exemplo de `yaml:1657-1662`. **[S]** (anonimato)

**T25. Fechamento.** Os 32 `operationId` no escopo, STATUS.md (inclusive as linhas 32-34, 46 e 77), matriz de perfis, mutação por incremento, revisão de segurança das rotas públicas e do anonimato.

**Ordem e dependências:** T1 → T2 → T3 → T4 → T5 → T6 → T7 → T8 → T9 → T10 (fim da 5a) → T11 → T12 → T13 → T14 → T15 → T16 → T17 → T18 (fim da 5b) → T19 → T20 → T21 → T22 (fim da 5c) → T23 → T24 → T25. T19 a T22 só dependem de T1-T8 e T12 (validação de arquivos) e podem vir antes da 5b se o dono quiser a entrevista primeiro; T18 espera as respostas do dono; T22 é independente de T21.

---

## H. Perguntas ao dono e matriz de testes

### H.1 Perguntas (só as que mudam o que se constrói)

1. **Link público:** fica só o limite por IP (30 consultas por minuto, 10 envios por hora) ou entra captcha? E a retenção dos dados de candidato é de 180 dias?
2. **Recorrente:** um formulário com nome que ainda está aberto na data de reabrir (sem prazo, ou com prazo maior que o intervalo) tem as respostas apagadas no meio da rodada, ou só reabre depois de encerrado, como o anônimo? E formulário **por link** pode ser recorrente (reabrir apagaria as candidaturas)?
3. **Depois de agendar ou publicar:** o RH pode mudar a data de um formulário agendado, publicá-lo antes ou devolvê-lo a rascunho? E mudar o prazo de um publicado? Hoje o contrato não tem rota para nada disso.
4. **Avaliação da equipe anônima** (o gestor avalia cada pessoa): cada pessoa recebe uma só avaliação, então o resultado nunca aparece. Proibir essa combinação, ou ela fica e o resultado fica sempre oculto?
5. **Avaliação com nome quando a equipe muda no meio** (alguém troca de setor, o gestor muda): vale a equipe do dia da publicação ou a de hoje?
6. **Formulário para setores:** quem está em admissão (ainda não começou) recebe o formulário do setor?
7. **Data gravada na resposta anônima:** o dia em que a rodada foi publicada (igual para todos) ou o dia em que a pessoa respondeu?
8. **Dado de candidato:** baixar currículo e arquivo de resposta entra no log de auditoria (como o holerite)? E a candidatura precisa deixar registrado que a pessoa aceitou o aviso de privacidade (hoje não há onde guardar)?

As demais hipóteses do contrato (E.2) seguem como estão até o dono dizer o contrário.

### H.2 Matriz de testes obrigatórios

| Caso | O que prova | Origem |
|---|---|---|
| Candidato com currículo de 6 MB; com `.png`; com `.zip` renomeado para `.docx` | 400 em `curriculo`; nada gravado, nenhum blob | roadmap:225; `yaml:2354-2356` |
| Arquivo de pergunta `ARQUIVO` grande ou de tipo errado, com o resto válido | 400 em `arquivos`; tudo ou nada | `yaml:2144`, `:2350` |
| O mesmo link respondido duas vezes com o mesmo e-mail (inclusive com caixa diferente, e em paralelo) | um 201 e um 409 `JA_RESPONDIDO` | roadmap:225; `yaml:2353-2354` |
| 11º envio na mesma hora pelo mesmo IP; 31ª consulta no minuto | 429 com `Retry-After` e o texto do contrato; outro IP passa | `yaml:2287`, `:2356` |
| Token inexistente, de rascunho e fora do padrão | o mesmo 404 | `yaml:2284-2285` |
| Página pública de agendado, de encerrado, com prazo vencido (tarefa ainda não rodou) e sem empresa | 422 com o código certo | `yaml:2285-2287` |
| Responder fora do prazo (funcionário e candidato) com a tarefa de encerrar atrasada | 422 `FORMULARIO_ENCERRADO`; nada gravado | roadmap:225; `yaml:846-847` |
| Gestor tentando abrir respostas (rotas do RH, `/api/eu/.../minhas-respostas` de outra pessoa, resultado, exportação) | 403 nas do RH; nenhuma rota do gestor devolve conteúdo | roadmap:219, 225; `yaml:1799`, `:1822` |
| Gestor: formulário de outro setor, Sugestão, as duas avaliações, gestor sem setor | 404; lista vazia | `yaml:1786-1789`, `:1829-1831`, `:1891-1893` |
| Formulário agendado publica uma vez (tarefa rodada duas vezes; API reiniciada) | um só `PUBLICADO`, uma só data | roadmap:225; `yaml:823-824` |
| Anônimo agendado que cai abaixo de 5 antes do dia | não publica, continua `AGENDADO`, a verificação mostra o bloqueio | `yaml:824-827` |
| Publicar anônimo para 4 pessoas; para 5 com setor sem gestor na avaliação do gestor | 422 `FORMULARIO_ANONIMO_PUBLICO_PEQUENO` antes da verificação | `yaml:819-820`, `:841-844` |
| Anônimo sem rastro: depois de responder | nenhuma linha nova de auditoria; resposta sem funcionário, sem candidato, sem hora; participação sem data; chave UUID v4 | roadmap:225; `yaml:2163-2167` |
| Anônimo aberto: lista, detalhe, quem respondeu (RH e gestor), encerrar, excluir, lembrete | `respondidos`, `faltam`, taxa, `temRespostas`, `pendentes`, `enviados`, `ignorados` nulos; só `esperados`; confirmação pedida mesmo com todos respondidos | `yaml:27-33`, `:1728-1732` |
| Anônimo publicado: editar tipo, anonimato, pergunta (texto, ordem, obrigatória), apagar pergunta, incluir destinatário, **com e sem resposta** | sempre 409, o mesmo corpo nos dois casos | `yaml:14-19`, `:616-619` |
| Resultado, avaliados e exportação de anônimo aberto | 409 `RESULTADO_ANONIMO_INDISPONIVEL` | `yaml:20-21` |
| Anônimo encerrado com pergunta de 2 respostas; com nenhuma pergunta de 3 | a pergunta some e `perguntasOcultas = 1`; exportação 422 | `yaml:1485-1488`, `:1685-1687` |
| Textos de respostas anônimas gravados em ordem Z, A, M | saem A, M, Z (sem diferenciar maiúscula e acento) | `yaml:1489-1490` |
| Avaliado anônimo com 2 respostas | `totalRespostas: null`, `resultadoLiberado: false`; resultado 422; fora do CSV | `yaml:1644-1647`, `:1496-1499` |
| Lembrete anônimo com muitos, com um, com ninguém e repetido no dia | a mesma resposta; um só lote de e-mails no dia por alcance; linhas da fila apagadas depois do envio; evento sem número nem nome | `yaml:1728-1737`; `V001:3157-3178` |
| Lembrete com nome duas vezes no dia (RH e gestor) | a segunda vai para `ignorados` | `yaml:1723-1726` |
| Lembrete automático: prazo a 8 dias (sai no dia certo, uma vez); prazo a 7 dias (não sai); caixa desligada; por link | conforme `yaml:849-853` | `yaml:849-861` |
| Exportação com resposta começando por `=`, `+`, `-`, `@` | célula neutralizada; BOM; `;`; anexo com o nome do contrato | roadmap:78, 225; `yaml:1676-1677` |
| Duas abas respondendo o mesmo formulário com nome | um 201 e um 409; nenhuma linha duplicada | `yaml:2155-2157` |
| Duas abas encerrando; encerrar × responder ao mesmo tempo | um efeito só; a resposta tardia vira 422 com `regraDoBanco` | B.2 |
| Duas abas mexendo na ordem das perguntas | nenhuma 500; ordens sem buraco nem repetição | B.2 |
| Sugestão: a mesma pessoa manda duas vezes (inclusive no mesmo segundo) | duas linhas em quem respondeu; cada uma abre o seu envio | `yaml:1155-1159`, `:1311-1315` |
| Recorrente reabrindo: com nome e anônimo; anônimo ainda aberto na data; anônimo com menos de 5 ativos | respostas, participações e arquivos apagados; a pessoa volta a "para responder"; o aberto não reabre e tenta na próxima data; o pequeno fica encerrado e sem recorrência | `yaml:867-876` |
| Encerrar à mão um recorrente | `proximaReaberturaEmUtc` nulo; a tarefa antiga não reabre | `yaml:1003` |
| Excluir formulário com respostas, candidatos, arquivos e tarefas | nada sobra no banco nem no armazenamento; nenhuma tarefa roda depois; evento com a contagem (sem ela no anônimo aberto) | `yaml:519-531` |
| Pergunta respondida: editar texto, tipo, alternativas; apagar; mudar só `obrigatoria` e `ordem` | 422 `PERGUNTA_JA_RESPONDIDA` com `regraDoBanco`; a última passa | `yaml:611-614` |
| Trocar `anonimo` de publicado sem resposta e com resposta | 409 `ANONIMATO_FIXO_APOS_PUBLICAR` nos dois; nunca o 1451 | `yaml:440-445` |
| Rascunho nasce inativo; `lembreteAutomatico` nasce ligado | relido do banco | `yaml:283-284`, `:857-858` |
| `agendarPara` salvo num rascunho | volta `null`, continua `RASCUNHO` | `yaml:753-759` |
| Cadastro, readmissão e importação com `formularioAdmissaoId`: publicado por pessoas; rascunho; por setores; encerrado; inexistente | inclui; 400 no campo; 422 `FORMULARIO_NAO_E_POR_PESSOAS`; 422 `FORMULARIO_ENCERRADO`; 400; nada cadastrado nos erros | `20-fatia-1:158-164`; `25-fatia-2:232-236` |
| Incluir destinatário inativo junto com dois ativos | 422 e **ninguém** entra | `yaml:1089` |
| Avaliação: avaliar quem não está na lista; a mesma pessoa duas vezes; `avaliadoId` em formulário que não é avaliação | 422 `AVALIADO_INVALIDO`; 409; 400 | `yaml:2160-2161` |
| Avaliação do gestor com setor sem gestor | essas pessoas não veem o formulário e não contam em `esperados` | `yaml:91-93`, `:841-844` |
| Funcionário inativo respondendo com nome e anônimo | 422 `FUNCIONARIO_INATIVO` nos dois (no anônimo, sem ajuda do trigger) | `yaml:2150-2151`; B.2 |
| Retenção: candidato com 181 dias e com 179 | o primeiro some com respostas e arquivos; o segundo fica | `yaml:2360`; `V001:2267-2269` |
| Arquivo órfão depois de recusa do banco | o blob é apagado na hora ou pela limpeza (a fonte de `formularios/` existe) | roadmap:76 |
| Matriz de perfis das 32 rotas | 403 nas famílias erradas; as públicas nunca 401/403 | CONVENCOES:29-30 |

---

## O que mais pode dar errado

As cinco condições que o contrato não cobre e que mais machucam: **(1) vazamento por contraste no anônimo:** qualquer diferença observável entre "já há resposta" e "não há" (um 422 em vez do 409, o evento de lembrete que só aparece quando alguém recebeu, o pedido de confirmação que some, o tempo da resposta) desfaz a regra 5 sem nenhum teste funcional falhar; a única defesa é testar os dois estados e comparar as respostas inteiras. **(2) O agendador atrasado:** prazo vencido com `ATIVO` ainda ligado deixa entrar resposta fora do prazo e, se a situação for lida só das colunas, mantém fechado um resultado que já devia abrir (ou o contrário); e a reabertura do recorrente, que **apaga dados**, rodando duas vezes ou sobre uma rodada aberta, não tem volta. **(3) O limite por IP sem o IP certo:** sem o proxy configurado, todos os candidatos dividem um balde de 10 envios por hora e a entrevista para de receber candidatura na primeira hora de divulgação; com ele, um atacante que rotaciona IP enche o armazenamento com currículos de 5 MB, porque não há limite por link. **(4) Duas abas e duplo clique:** responder duas vezes um formulário com nome (o banco aceita), publicar duas vezes, encerrar enquanto alguém responde, reordenar perguntas em paralelo (1062 → 500) e dois envios de Sugestão no mesmo segundo (viram um). **(5) Fuso e relógio:** `prazo` e `agendarPara` são dias em -03:00 e depois das 21:00 locais o dia UTC já virou; os padrões do banco (`NOW()`, `CURRENT_TIMESTAMP`) usam o relógio real, então um candidato criado sem data explícita nasce "hoje de verdade" e o teste da retenção passa ou falha conforme o calendário.
