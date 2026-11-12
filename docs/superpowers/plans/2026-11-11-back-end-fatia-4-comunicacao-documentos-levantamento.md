# Levantamento da fatia 4 (comunicação e documentos) do back-end (anexo do plano da fatia 4)

Feito em 2026-11-11, na branch `feat/fatia-3-ponto` (último commit `ba27bbe`), só lendo. Fontes: `contrato/fontes/40-fatia-4-comunicacao-e-documentos.yaml` inteiro (citado como `yaml:N`), `00-base.yaml`, `CONVENCOES.md`, `STATUS.md`, `PROVISORIOS.md`, trechos de `20-fatia-1` e `70-fatia-7`; roadmap (`docs/superpowers/plans/2026-10-28-roadmap-completo.md`, 35-83 e 182-202, citado `roadmap:N`); `V001`, `V002`, `V003`, `LEIA-ME.md`; `back-end/src` e os apoios de teste. Caminhos de código relativos a `back-end/src/`. **INFERIDO** = conclusão minha, não lida nem medida.

---

## A. As 35 operações

Perfis: F = Funcionario, G = Gestor, RH. `[F,G,RH]` = família `/api/eu` (a própria pessoa; nunca 403). Paginação = `pagina` (≥1, padrão 1) e `tamanhoPagina` (1-100, padrão 20). Toda operação tem 401 e 500; não repito. **Nenhuma lista da fatia 4 tem `ordenarPor`/`direcao`: a ordem é fixa.**

| # | Rota | operationId | Perfil | Entrada | 2xx | Erros (além de 401/500) | Efeito declarado |
|---|---|---|---|---|---|---|---|
| 1 | GET `/api/rh/avisos` | listarAvisos | RH | paginação, `busca` (título e conteúdo), `situacao` (`AvisoSituacao`, padrão `ATIVO`), `tipo` | 200 `AvisoListaRh` | 400, 403 | ordem: ativos/expirados por publicação desc; agendados por quem publica primeiro; rascunhos por criação desc (`yaml:28-29`); `leitura` só em ATIVO/EXPIRADO |
| 2 | POST `/api/rh/avisos` | criarAviso | RH | `AvisoCriacaoComando` | 201 `AvisoDetalheRh` | 400, 403 | `publicar:false` rascunho; `true` + `publicarEmUtc` vazio/passado = agora, futuro = `AGENDADO`; e-mail a todos os ativos **sempre** na publicação; `expiraEm` < dia da publicação = 400 (`yaml:106-118`) |
| 3 | GET `/api/rh/avisos/resumo` | obterResumoDosAvisos | RH | — | 200 `AvisoPainelRh` | 403 | contagens por aba; média de `viram÷total` dos ativos (inteiro, `null` sem ativo); expiram em 7 dias |
| 4 | GET `/api/rh/avisos/{avisoId}` | obterAviso | RH | — | 200 `AvisoDetalheRh` | 403, 404 | qualquer situação |
| 5 | PUT `/api/rh/avisos/{avisoId}` | editarAviso | RH | `AvisoEdicaoComando` | 200 `AvisoDetalheRh` | 400, 403, 404; 422 `AVISO_PUBLICACAO_NAO_EDITAVEL` | tipo não muda; não reenvia e-mail; `publicarEmUtc` só muda em RASCUNHO/AGENDADO; EXPIRADO com expiração futura volta a ATIVO; `expiraEm` vazio tira a expiração (`yaml:239-247`) |
| 6 | DELETE `/api/rh/avisos/{avisoId}` | excluirAviso | RH | — | 204 | 403, 404; 422 `AVISO_NAO_PODE_SER_EXCLUIDO` | só RASCUNHO/AGENDADO; **físico**: apaga as linhas de leitura e depois o aviso na mesma transação; auditoria `AVISOS`/`EXCLUIU` (`yaml:297-307`) |
| 7 | POST `/api/rh/avisos/{avisoId}/publicar` | publicarAviso | RH | sem corpo | 200 `AvisoDetalheRh` | 400 (expiração anterior à publicação), 403, 404; 422 `AVISO_JA_PUBLICADO` | usa o `publicarEmUtc` guardado; grava a data **efetiva** |
| 8 | POST `/api/rh/avisos/{avisoId}/encerrar` | encerrarAviso | RH | sem corpo | 200 `AvisoDetalheRh` | 403, 404; 422 `AVISO_NAO_ESTA_ATIVO` | expiração = agora → `EXPIRADO`; leituras ficam |
| 9 | GET `/api/rh/avisos/{avisoId}/leituras` | listarLeiturasDoAviso | RH | paginação, `situacao` (`LIDO\|NAO_LIDO`, enum inline) | 200 `AvisoLeituras` | 400, 403, 404; 422 `AVISO_AINDA_NAO_PUBLICADO` | `viram`/`aindaNaoViram` não dependem do filtro; ordem por nome; conta "ativos na criação + admitidos depois que abriram" (`yaml:429-436`) |
| 10 | POST `/api/rh/avisos/{avisoId}/lembretes` | lembrarQuemNaoViuAviso | RH | sem corpo | **202** `AvisoLembreteResultado` | 403, 404; 422 `AVISO_NAO_ESTA_ATIVO` | e-mail a cada **ativo** que não abriu; `quantidadeDeEmails` (0 se todos viram); **sem limite** de lembretes (`yaml:489-493`) |
| 11 | GET `/api/eu/avisos` | listarMeusAvisos | F,G,RH | paginação, `busca`, `apenasNaoLidos` (bool), `fixadosPrimeiro` (bool) | 200 `AvisoListaEu` | 400 | só vigentes; admitido depois vê; ordem publicação desc (fixados antes com `fixadosPrimeiro`); `contagens` não dependem de busca/filtro (= bolinha do menu) |
| 12 | GET `/api/eu/avisos/{avisoId}` | obterMeuAviso | F,G,RH | — | 200 `AvisoDetalheEu` | 404 (inexistente ou não vigente) | **não marca lido** |
| 13 | POST `/api/eu/avisos/{avisoId}/leitura` | marcarAvisoComoLido | F,G,RH | sem corpo | 204 | 404 | idempotente (mantém a 1ª data); cria a linha se não houver |
| 14 | GET `/api/rh/funcionarios/{funcionarioId}/holerites` | listarHoleritesDoFuncionario | RH | paginação, `ano` (2000-2100), `tipo`, `todasAsVersoes` (bool) | 200 `HoleriteListaRh` | 400, 403, 404 | sem `todasAsVersoes` só a versão atual de cada (ano, mês, tipo); inclui AGENDADO; ordem ano, mês desc, versão desc, id desc; `ultimoPublicado` ignora filtros; vale para inativo |
| 15 | POST `/api/rh/funcionarios/{funcionarioId}/holerites` | enviarHolerite | RH | multipart `HoleriteEnvioComando` | 201 `HoleriteItemRh` | 400, 403, 404; 409 `HOLERITE_JA_EXISTE`; 422 `HOLERITE_COMPETENCIA_FUTURA`, `..._ANTERIOR_A_ADMISSAO`, `..._POSTERIOR_AO_DESLIGAMENTO` (+`regraDoBanco`), `EMPRESA_NAO_CONFIGURADA` (sem `regraDoBanco`) | PDF ≤ 5 MiB pelo conteúdo; `publicarEm` × `noDiaDoPagamento` exclusivos (400); dia passado 400; e-mail sempre; arquivo só depois da regra do banco; auditoria (`yaml:746-766`) |
| 16 | POST `/api/rh/holerites/lote` | enviarHoleritesEmLote | RH | multipart `HoleriteLoteComando` (`itens` JSON, `arquivos` repetido) | **sempre 200** `HoleriteLoteResultado` | 400 (lote todo), 403; 422 `EMPRESA_NAO_CONFIGURADA` (lote todo) | item a item, sem tudo ou nada; até 200 PDFs e 100 MB; nome repetido, arquivo sem item ou item sem arquivo = 400; lote **não cria versão**; **sem 404** (pessoa inexistente vira item `FUNCIONARIO_NAO_ENCONTRADO`); x-provisorio formato (`yaml:859-877`) |
| 17 | POST `/api/rh/holerites/{holeriteId}/versoes` | substituirHolerite | RH | multipart `HoleriteNovaVersaoComando` | 201 `HoleriteItemRh` | 400, 403, 404; 409 `HOLERITE_VERSAO_DESATUALIZADA`; 422 `..._ANTERIOR_A_ADMISSAO`, `..._POSTERIOR_AO_DESLIGAMENTO`, `EMPRESA_NAO_CONFIGURADA` | versão = MAX+1; `{holeriteId}` tem de ser a maior versão; novo e-mail ao publicar; x-provisorio (`yaml:947-957`) |
| 18 | GET `/api/rh/holerites/{holeriteId}/download` | obterLinkDeDownloadDoHolerite | RH | — | 200 `LinkTemporario` | 403, 404 | qualquer versão, publicada ou agendada; auditoria |
| 19 | GET `/api/eu/holerites` | listarMeusHolerites | F,G,RH | paginação, `ano` (padrão ano atual), `tipo` | 200 `HoleriteListaEu` | 400 | só publicados e só a versão atual; sem "novo/visto"; `contagens` do ano, independem de `tipo`; `anosDisponiveis` desc; ordem ano, mês desc, publicação desc |
| 20 | GET `/api/eu/holerites/{holeriteId}/download` | obterLinkDeDownloadDoMeuHolerite | F,G,RH | — | 200 `LinkTemporario` | 404 (de outro, agendado, versão substituída) | auditoria; x-provisorio "Ver" × "Baixar" |
| 21 | GET `/api/rh/advertencias` | listarAdvertencias | RH | paginação, `busca` (nome e motivo), `ano` (padrão atual, pela data do ocorrido), `situacao`, `setorId` (setor atual), `tipo` | 200 `AdvertenciaLista` | 400, 403 | ordem data desc, depois "o mais recente registrado" |
| 22 | GET `/api/rh/advertencias/resumo` | obterResumoDasAdvertencias | RH | `ano` | 200 `AdvertenciaResumoRh` | 400, 403 | `todas` inclui canceladas; `setorComMaisRegistros` só não canceladas, empate alfabético |
| 23 | GET `/api/rh/advertencias/{advertenciaId}` | obterAdvertencia | RH | — | 200 `AdvertenciaDetalheRh` | 403, 404 | com contestação |
| 24 | POST `/api/rh/advertencias/{advertenciaId}/resposta` | responderContestacaoDeAdvertencia | RH | `AdvertenciaRespostaComando` | 200 `AdvertenciaDetalheRh` | 400, 403, 404; 422 `ADVERTENCIA_NAO_ESTA_CONTESTADA` | MANTER→`APLICADA`/`MANTIDA`; ACATAR→`CANCELADA`/`ACATADA`; `resposta` **só por e-mail e na auditoria**, nunca no banco; x-provisorio |
| 25 | GET `/api/rh/funcionarios/{funcionarioId}/advertencias` | listarAdvertenciasDoFuncionario | RH | paginação | 200 `AdvertenciaLista` | 400, 403, 404 | todos os anos, inclui canceladas; vale para inativo |
| 26 | POST `/api/rh/funcionarios/{funcionarioId}/advertencias` | registrarAdvertencia | RH | `AdvertenciaRegistroRhComando` | 201 `AdvertenciaDetalheRh` | 400, 403, 404; 422 `FUNCIONARIO_INATIVO` (+`regraDoBanco`), `ADVERTENCIA_SEM_GESTOR`, `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO` | nasce PENDENTE; data futura 400; e-mail **só à pessoa** (sem cópia ao gestor); auditoria |
| 27 | GET `/api/gestor/advertencias` | listarAdvertenciasDaEquipe | G | paginação, `ano`, `situacao` | 200 `AdvertenciaLista` | 400, 403 | só a equipe (= `GET /api/gestor/equipe`), registradas por qualquer um |
| 28 | GET `/api/gestor/advertencias/resumo` | obterResumoDasAdvertenciasDaEquipe | G | `ano` | 200 `AdvertenciaResumoGestor` | 400, 403 | regra única: não canceladas do ano; `porPessoa` com todos (inclusive 0), quantidade desc e nome |
| 29 | GET `/api/gestor/advertencias/{advertenciaId}` | obterAdvertenciaDaEquipe | G | — | 200 `AdvertenciaDetalheGestor` | 403, 404 (fora da equipe) | sem texto da contestação nem desfecho (x-provisorio) |
| 30 | GET `/api/gestor/equipe/{funcionarioId}/advertencias` | listarAdvertenciasDoFuncionarioDaEquipe | G | paginação | 200 `AdvertenciaLista` | 400, 403, 404 | todos os anos, inclui canceladas |
| 31 | POST `/api/gestor/equipe/{funcionarioId}/advertencias` | registrarAdvertenciaDaEquipe | G | `AdvertenciaRegistroGestorComando` | 201 `AdvertenciaDetalheGestor` | 400, 403, 404 (outro setor, o próprio id); 422 `FUNCIONARIO_INATIVO` (+`regraDoBanco`) | aplicada pelo gestor logado; e-mail à pessoa **e cópia ao RH** (`yaml:1650`) |
| 32 | GET `/api/eu/advertencias` | listarMinhasAdvertencias | F,G,RH | paginação, `situacao` | 200 `AdvertenciaListaEu` | 400 | PENDENTE primeiro, depois data desc; item completo |
| 33 | POST `/api/eu/advertencias/{advertenciaId}/ciencia` | confirmarCienciaDeAdvertencia | F,G,RH | sem corpo | 200 `AdvertenciaEu` | 404 (de outra pessoa); 422 `ADVERTENCIA_NAO_ESTA_PENDENTE` | PENDENTE→APLICADA + data; repetir = 422 |
| 34 | POST `/api/eu/advertencias/{advertenciaId}/contestacao` | contestarAdvertencia | F,G,RH | `{texto}` 10-2000 | 200 `AdvertenciaEu` | 400, 404; 422 `ADVERTENCIA_NAO_ESTA_PENDENTE` | só de PENDENTE; nunca sobrescreve; **sem e-mail ao RH** |
| 35 | GET `/api/eu/aniversariantes` | listarAniversariantes | F,G,RH | — | 200 `AniversariantesResposta` | — | empresa toda; ativos com nascimento; hoje..+14 (-03:00); 29/02→28/02 em ano comum; sem paginação; nunca o ano nem a idade |

**Textos exatos dos erros com código** (copiar dos exemplos, como `MensagensDoPonto`; aqui **todos os títulos são os genéricos** "Operação recusada." (422) e "Conflito." (409) que `TextosDeErro.Titulo` já dá, então `ErroDeNegocioException`/`ConflitoException` bastam, salvo onde há `regraDoBanco`, que pede `ProblemaDeApiException`):

| codigo (status) | detail (variações por rota) | regraDoBanco |
|---|---|---|
| AVISO_PUBLICACAO_NAO_EDITAVEL (422) | Este aviso já foi publicado; a data de publicação não pode mudar. (`yaml:289`) | — |
| AVISO_NAO_PODE_SER_EXCLUIDO (422) | Só é possível excluir um aviso que ainda não foi publicado. Para tirar um aviso do ar, encerre-o. (`yaml:322`) | — |
| AVISO_JA_PUBLICADO (422) | Este aviso já foi publicado. (`yaml:371`) | — |
| AVISO_NAO_ESTA_ATIVO (422) | #8: "Só um aviso ativo pode ser encerrado." (`yaml:418`) · #10: "Só é possível lembrar sobre um aviso ativo." (`yaml:515`) | — |
| AVISO_AINDA_NAO_PUBLICADO (422) | Este aviso ainda não foi publicado. (`yaml:478`) | — |
| HOLERITE_JA_EXISTE (409) | #15: "Já existe um holerite mensal de outubro de 2026 para esta pessoa. Para corrigir, envie uma nova versão." (`yaml:811`) · #16 (`mensagemDoErro`): sem a 2ª frase (`yaml:924`). **Texto montado** com tipo e mês por extenso | — |
| HOLERITE_VERSAO_DESATUALIZADA (409) | Este holerite já foi substituído por uma versão mais nova. Atualize a lista. (`yaml:994`) | — |
| EMPRESA_NAO_CONFIGURADA (422) | A empresa ainda não tem o dia do pagamento configurado. Configure em Cadastros > Empresa ou escolha uma data. (`yaml:830`, `:936`) | nenhum |
| HOLERITE_COMPETENCIA_FUTURA (422) | Só o holerite de férias pode ter competência futura. (`yaml:838`) | `Nao e possivel cadastrar holerite de competencia futura.` |
| HOLERITE_COMPETENCIA_ANTERIOR_A_ADMISSAO (422) | A competência do holerite é anterior à admissão da pessoa. (`yaml:847`) | `A competencia do holerite e anterior a admissao.` |
| HOLERITE_COMPETENCIA_POSTERIOR_AO_DESLIGAMENTO (422) | A competência do holerite é posterior ao desligamento da pessoa. (`yaml:1008`) | `A competencia do holerite e posterior ao desligamento.` |
| ARQUIVO_INVALIDO, FUNCIONARIO_NAO_ENCONTRADO | só em `codigoDoErro` do lote (`yaml:2306-2307`); **sem texto no contrato** | — |
| ADVERTENCIA_NAO_ESTA_CONTESTADA (422) | Só uma advertência contestada pode receber resposta. (`yaml:1317`) | — |
| FUNCIONARIO_INATIVO (422) | Não é possível registrar advertência para uma pessoa inativa. (`yaml:1432`, `:1693`) | `Nao e possivel registrar advertencia para funcionario inativo.` (sempre, como na 3) |
| ADVERTENCIA_SEM_GESTOR (422) | Esta pessoa não tem gestor ativo. Escolha "RH" em "Aplicada por". (`yaml:1441`) | — |
| ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO (422) | **sem exemplo** (só na descrição, `yaml:1421-1422`): o executor escolhe e registra | `O gestor nao pode advertir a si mesmo.` |
| ADVERTENCIA_NAO_ESTA_PENDENTE (422) | Esta advertência já teve ciência ou contestação. (`yaml:1796`, `:1850`) | — |

Erros que o banco impõe e o contrato **não** declara (validar antes, senão viram 500 ou 409 genérico): `CHK_AVISOS_DATAS` (3819) ao editar/publicar com expiração antes da publicação → 400; `TRG_AVISOS_BEFORE_INSERT` "A expiracao do aviso nao pode ser anterior a publicacao." (45000) → 400 (`yaml:116-117` manda validar antes); FK de `AVISOS_VISUALIZACOES` (1451) se excluir o aviso sem apagar as leituras; `UQ_AVISOS_VISUALIZACOES_AVISO_FUNCIONARIO` (1062) em leitura simultânea → sucesso; `UQ_HOLERITES_COMPETENCIA_TIPO_VERSAO` (1062) → `HOLERITE_JA_EXISTE` (#15) ou `HOLERITE_VERSAO_DESATUALIZADA` (#17); FK de `HOLERITES`/`ADVERTENCIAS` para pessoa inexistente (1452) → 404 antes; `TRG_*_BEFORE_DELETE` de holerite e advertência (nunca apagar); 413 acima do teto da requisição e 415 em corpo não multipart (padrão já aceito na 1c/3).

---

## B. Schemas que importam (`yaml:1911-2555`)

- **AvisoTipo** `SIMPLES|DETALHADO` (texto livre `TIPO_AVISO`). **AvisoSituacao** `RASCUNHO|AGENDADO|ATIVO|EXPIRADO`, calculada, nunca enviada. **Não há** `ENCERRADO` nem `EXCLUIDO`: encerrado = `EXPIRADO` com expiração = instante do encerramento; excluído = apagado.
- **AvisoItemRh** (todos obrig.): `id, tipo, titulo (≤200), previa, situacao, fixado, publicacaoEmUtc (null só em rascunho sem data prevista), expiraEmUtc (null = não expira), autorNome (null), leitura ({viram,total} | null fora de ATIVO/EXPIRADO)`. **AvisoDetalheRh** + `conteudo`.
- **previa**: simples = a mensagem inteira; detalhado = "início do conteúdo em texto puro (sem markdown), até 200 caracteres", **sem reticências** (a tela corta). Os exemplos ensinam a regra (INFERIDO, conferir): 398 (`yaml:351` vs `:358`) tira os títulos `##` inteiros e junta os parágrafos com espaço; 409 (`yaml:215` vs `:222-230`) para no 1º parágrafo porque o 2º passaria de 200 → "parágrafos inteiros enquanto couber em 200; se o 1º sozinho passar, cortar em 200".
- **AvisoCriacaoComando** obrig. `tipo, titulo (1-200), conteudo (1-10000; simples ≤280 → 400), fixado, publicar`; opc. `publicarEmUtc (date-time|null)`, `expiraEm (date|null, último dia, fim do dia -03:00)`. **AvisoEdicaoComando** obrig. `titulo, conteudo, fixado`; opc. `publicarEmUtc`, `expiraEm`. Expiração gravada = `dia 23:59:59 -03:00` (exemplos `expiraEm 2026-10-18` → `2026-10-19T02:59:59Z`, `yaml:132`/`:159`).
- **AvisoPainelRh** `{contagens{ativos,agendados,expirados,rascunhos}, leituraMediaDosAtivosPercentual (0-100|null), expiramNosProximos7Dias, proximoAExpirar ({avisoId,titulo,expiraEmUtc}|null)}`.
- **AvisoLeituras** = Paginação + `viram, aindaNaoViram, itens[{funcionario (FuncionarioResumo), lido, lidoEmUtc}]`. **AvisoLembreteResultado** `{quantidadeDeEmails}`.
- **AvisoItemEu** `id, tipo, titulo, previa, fixado, publicadoEmUtc (não nulo), expiraEmUtc, autorNome, lido`; **AvisoDetalheEu** + `lidoEmUtc, conteudo`; **AvisoListaEu** + `contagens{naoLidos,total}`.
- **HoleriteTipo** `MENSAL|DECIMO_TERCEIRO|FERIAS` (= `CHK_HOLERITES_TIPO`). **HoleriteSituacao** `PUBLICADO|AGENDADO`. **HoleriteItemRh** `id, funcionarioId, tipo, mes (1-12), ano (2000-2100), versao (≥1), versaoAtual, situacao, publicacaoEmUtc (agendado = começo do dia, ex. `2026-11-06T03:00:00Z`)`. **HoleriteListaRh** + `ultimoPublicado|null`. **HoleriteItemEu** `id, tipo, mes, ano, publicadoEmUtc`; **HoleriteListaEu** + `ano, anosDisponiveis, contagens{todos,mensais,ferias,decimoTerceiro}`.
- **HoleriteEnvioComando** (multipart) obrig. `mes, ano, tipo, arquivo`; opc. `publicarEm (date)` **ou** `noDiaDoPagamento (bool)`. **HoleriteNovaVersaoComando** obrig. `arquivo`; o resto igual. **HoleriteLoteComando** obrig. `mes, ano, tipo, itens (1-200, JSON [{funcionarioId, nomeDoArquivo ≥1}]), arquivos (1-200 binários)`; `encoding: itens application/json, arquivos application/pdf` (`yaml:883-885`). **HoleriteLoteItemResultado** todos obrig.: `funcionarioId, nomeDoArquivo, sucesso, holerite|null, codigoDoErro|null, mensagemDoErro|null`; **HoleriteLoteResultado** `total, enviados (publicados + agendados), comErro, itens`.
- **AdvertenciaTipo** `VERBAL|ESCRITA` (x-provisorio). **AdvertenciaSituacao** `PENDENTE|APLICADA|CONTESTADA|CANCELADA` (valores do banco). **AdvertenciaDesfecho** `MANTIDA|ACATADA`. **AdvertenciaAplicadaPor** `{tipo: GESTOR|RH, nome (null no RH)}` (x-provisorio).
- **AdvertenciaItem** `id, data (DATA_ADVERTENCIA), funcionario, tipo, motivo (≤255), aplicadaPor, situacao`. **DetalheRh** + `descricao, dataCienciaUtc, textoContestacao, dataContestacaoUtc, desfechoDaContestacao`. **DetalheGestor** + só `descricao, dataCienciaUtc`. **AdvertenciaEu** (sem `funcionario`) com tudo. **ResumoRh** `{ano, contagens{todas,pendentes,aplicadas,contestadas,canceladas}, setorComMaisRegistros{setor,quantidade≥1}|null}`. **ResumoGestor** `{ano, registradas, aguardandoCiencia{quantidade,pessoas[]}, pessoasComAdvertencia, totalDaEquipe, porPessoa[{funcionario,quantidade}]}`.
- **Comandos de advertência**: RH obrig. `dataDoOcorrido (date, ≤ hoje), tipo, aplicadaPor (GESTOR_DO_COLABORADOR|RH), motivo (1-255), descricao (1-5000)`; gestor igual sem `aplicadaPor`; contestação `texto 10-2000`; resposta `decisao (MANTER|ACATAR), resposta (1-2000)`.
- **Aniversariantes** `{dias (fixo 14), itens[{funcionario, aniversarioEm (date), diasAte (0-14)}]}`.
- `LinkTemporario {url, expiraEmUtc}`; `FuncionarioResumo {id, nome, cargo|null, setor|null, ativo}` (`00-base.yaml:119-136`).

---

## C. O banco

### C.1 Tabelas (`V001`)

| Tabela | Colunas | Restrições |
|---|---|---|
| `AVISOS` (206-223) | `ID_AVISO`, `ID_FUNCIONARIO` NULL (autor), `TITULO` VARCHAR(200) NN, `CONTEUDO` TEXT NN, `DATA_PUBLICACAO` DATETIME NULL, `DATA_EXPIRACAO` DATETIME NULL, `TIPO_AVISO` VARCHAR(50) NULL, `FIXADO` NN def FALSE, `ENVIADO` NN def FALSE | `CHK_AVISOS_DATAS` (expiração ≥ publicação quando as duas existem); FK autor |
| `AVISOS_VISUALIZACOES` (229-246) | `ID_AVISO_VISUALIZACAO`, `ID_AVISO` NN, `ID_FUNCIONARIO` NN, `VISUALIZADO` NN def FALSE, `DATA_VISUALIZACAO` DATETIME NULL | **UQ `(ID_AVISO, ID_FUNCIONARIO)`**; CHECK lido ⇒ data; FK para `AVISOS` **sem `ON DELETE`** |
| `HOLERITES` (375-396) | `ID_HOLERITE`, `ID_FUNCIONARIO` NN, `MES` TINYINT NN, `ANO` YEAR NN, `DATA_PUBLICACAO` DATETIME NULL, `CAMINHO_ARQUIVO` VARCHAR(255) **NULL**, `TIPO_HOLERITE` VARCHAR(20) NN def 'MENSAL', `VERSAO` SMALLINT NN def 1 | CHECK mês 1-12; CHECK tipo; CHECK versão ≥1; **UQ `(ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO)`**; **sem nome original do arquivo, sem quem enviou** |
| `ADVERTENCIAS` (402-430) | `ID_ADVERTENCIA`, `ID_FUNCIONARIO` NN, `ID_GESTOR` NULL (FK `GESTORES`), `DATA_ADVERTENCIA` DATE NULL, `TIPO_ADVERTENCIA` VARCHAR(50) NULL, `MOTIVO` VARCHAR(255) NULL, `DESCRICAO` TEXT NULL, `STATUS_ADVERTENCIA` VARCHAR(30) NULL, `DATA_CIENCIA`, `TEXTO_CONTESTACAO` TEXT, `DATA_CONTESTACAO` (todas NULL) | CHECK status ∈ {PENDENTE, APLICADA, CANCELADA, CONTESTADA} ou NULL; CHECK texto⇔data da contestação; CHECK CONTESTADA ⇒ texto. **Sem coluna de desfecho, de resposta, de quem do RH registrou/respondeu, nem de "registrado em"** |
| `FUNCIONARIOS` (56-88) | `DATA_NASCIMENTO` DATE **NULL**, `ATIVO`, `DATA_ADMISSAO` NN, `DATA_DESLIGAMENTO`, `EMAIL_CORPORATIVO` NULL (UQ), `ID_SETOR`, `ID_GESTOR` (derivado do setor; o gestor do setor fica sem) | CHECK ativo ⇒ sem desligamento |
| `CONFIGURACOES_EMPRESA` (923-938) | linha única; **`DIA_PAGAMENTO` TINYINT NN** (1-31) | "sem dia de pagamento" = **sem a linha** (nasce vazia) |
| `PAGAMENTOS_EXCECOES` (1279-1289) | `ANO` YEAR, `MES`, `DATA_PAGAMENTO` | UQ `(ANO, MES)`; data dentro do mês; sem entidade (dona: 7c) |
| `FILA_EMAILS` (1412-1455), UTC | `TIPO` (`^[A-Z][A-Z0-9_]{2,39}$`), `CHAVE_IDEMPOTENCIA` UQ ASCII ≤190 começando por "TIPO:", `REFERENCIA_ID`, `ID_FUNCIONARIO`, `DESTINATARIO_EMAIL` NN (`%_@_%`), `CARGA` JSON (nula ao terminar) | tipos sugeridos no comentário: `AVISO_PUBLICADO`, `HOLERITE_PUBLICADO`, `ADVERTENCIA_REGISTRADA` (3121-3123); chaves `AVISO_PUBLICADO:aviso=15:func=21`, `HOLERITE_PUBLICADO:holerite=77` (3129-3130) |
| `TAREFAS_AGENDADAS` (1466-1494), UTC | `TIPO` ≤50, chave UQ, `REFERENCIA_ID`, `DIA_REFERENCIA`, `PARAMETROS` (nunca dado pessoal), `EXECUTAR_APOS` | tipos sugeridos `PUBLICAR_AVISO`, `PUBLICAR_HOLERITE`, chave `PUBLICAR_AVISO:aviso=15` (3211-3219); "CANCELADA ... o aviso agendado foi excluido" (3233) |
| `AUDITORIA_EVENTOS`/`_MUDANCAS` (1507-1582) | `DESCRICAO` VARCHAR(500); `VALOR_ANTES/DEPOIS` TEXT | áreas `HOLERITES`, `ADVERTENCIAS`, `AVISOS` (1535-1538); ações `CRIOU, ALTEROU, EXCLUIU, REGISTROU, APROVOU, RECUSOU, PUBLICOU, ENVIOU_EMAIL, BAIXOU` (1539-1541) |

### C.2 Triggers (`V002`)

| Trigger | Regra |
|---|---|
| `TRG_AVISOS_BEFORE_INSERT` (318-331) | `DATA_PUBLICACAO = NOW()` se NULL (**inclusive no rascunho**); recusa expiração < publicação ("A expiracao do aviso nao pode ser anterior a publicacao."). **Só INSERT**: UPDATE e DELETE livres |
| `TRG_AVISOS_AFTER_INSERT` (333-341) | cria uma linha `VISUALIZADO=FALSE` para **todo `FUNCIONARIOS.ATIVO = TRUE`** (inclusive "em admissão", inclusive no rascunho), no momento do INSERT |
| `TRG_AVISOS_VISUALIZACOES_BEFORE_INSERT/UPDATE` (343-367) | lido sem data → `NOW()`; não lido → data NULL |
| `TRG_HOLERITES_BEFORE_INSERT` (573-608) | competência = dia 1 do mês; recusa `LAST_DAY(comp) < admissão`, `comp > desligamento`, e `comp > CURDATE()` **se o tipo não é `FERIAS`**; `DATA_PUBLICACAO = NOW()` se NULL. **Não confere ativo** |
| `TRG_HOLERITES_BEFORE_DELETE` (610-616) | recusa todo DELETE ("Exclusao fisica de holerite nao permitida.") |
| `TRG_ADVERTENCIAS_BEFORE_INSERT` (622-656) | recusa inativo; com `ID_GESTOR`, recusa gestor = a própria pessoa; `STATUS = 'PENDENTE'` e `DATA_ADVERTENCIA = CURDATE()` se NULL |
| `TRG_ADVERTENCIAS_BEFORE_UPDATE` (658-673) | só recusa sair de `CANCELADA` e `APLICADA→PENDENTE`. **Permite** `APLICADA→CONTESTADA`, `CONTESTADA→PENDENTE` e qualquer outra: a máquina de estados é da aplicação (`V001` 2056-2060) |
| `TRG_ADVERTENCIAS_BEFORE_DELETE` (675-681) | recusa todo DELETE |

### C.3 Funções (`V003`)

`FN_CONTAR_AVISOS_NAO_LIDOS` (276-291): conta linhas não lidas de avisos com publicação ≤ `NOW()` e expiração ≥ `NOW()`. **Não usar** (`V001` 2025-2027, roadmap:51): não conhece `ENVIADO` (conta rascunho e o agendado de data NULL), depende da linha (quem foi admitido depois some da conta) e usa `NOW()` real. Nenhuma outra função toca a fatia 4.

### C.4 Como os estados ficam nas colunas (lido no `V001` 2005-2035 e nos triggers; a derivação é INFERIDA)

- **Aviso** (agora = `IRelogio`): `RASCUNHO` = `ENVIADO=FALSE` (`DATA_PUBLICACAO` = data prevista ou NULL). `AGENDADO` = `ENVIADO=TRUE` e `DATA_PUBLICACAO > agora` (`V001` 2034-2035). `ATIVO` = `ENVIADO`, publicação ≤ agora e (expiração NULL ou > agora). `EXPIRADO` = `ENVIADO` e expiração ≤ agora (≤, para o "encerrar agora" sair já `EXPIRADO`). **A visibilidade não depende do agendador**: o aviso agendado aparece sozinho na hora; a tarefa só manda o e-mail (e completa as linhas de leitura, ver D).
- **Leitura**: linha com `VISUALIZADO=TRUE` = viu; linha `FALSE` ou sem linha = não viu; `total` = linhas do aviso (INFERIDO de `yaml:433-434`).
- **Holerite**: `AGENDADO` = `DATA_PUBLICACAO > agora`, senão `PUBLICADO`. Versão atual (RH) = maior `VERSAO` de `(func, ano, mes, tipo)`; versão que a pessoa vê = **maior versão já publicada** (`yaml:949-950`), então com a v2 agendada a v1 segue visível e baixável.
- **Advertência**: `ID_GESTOR` NULL = aplicada pelo RH (`aplicadaPor.nome` = null); preenchido = gestor (nome = `FUNCIONARIOS.NOME` da linha de `GESTORES`). `desfechoDaContestacao` **não tem coluna**: `CANCELADA` ⇒ `ACATADA` (cancelar só existe por acatar, `yaml:1270`); `APLICADA` com `TEXTO_CONTESTACAO` ⇒ `MANTIDA` (contestar só sai de PENDENTE); senão `null`. "Mais recente registrado" = `ID_ADVERTENCIA` desc (não há data de registro).

### C.5 Armadilhas

1. **Relógio falso × `NOW()`/`CURDATE()` reais.** Testes fixam 2026-10-07; os triggers usam a data real. `DATA_PUBLICACAO` (aviso e holerite), `DATA_VISUALIZACAO`, `DATA_ADVERTENCIA` e `STATUS` se vierem NULL ficam com a data real: **sempre preencher pelo `IRelogio`** (decisão 1 da fatia 3). E a competência futura do holerite é julgada pelo `CURDATE()` real: nos testes, novembro/2026 é "futuro" para a aplicação e "passado" para o banco. A API valida pelo `IRelogio` e devolve o `regraDoBanco` fixo; o teste do caminho do trigger usa competência bem futura (2100).
2. **Rascunho sem data**: o trigger põe `NOW()`; para `publicacaoEmUtc: null` faça `UPDATE ... SET DATA_PUBLICACAO = NULL` logo depois do INSERT, na mesma transação (`LEIA-ME.md:87`).
3. **Linhas de leitura nascem no INSERT**, para quem é ativo então (e para "em admissão"). Rascunho publicado semanas depois e aviso agendado não têm linha de quem entrou no meio; quem saiu no meio tem. Inativo sem linha vira 1452 se a leitura tentar criar para pessoa inexistente (não acontece com `/api/eu`).
4. **Excluir**: `ExecuteDelete` em `AVISOS_VISUALIZACOES` antes do aviso (sem isso, 1451 → 500), com `// AUDITORIA-OK` + o evento `EXCLUIU` explícito (`GuardaDeExecuteUpdateTests`); cancelar a tarefa `PUBLICAR_AVISO` do agendado e os e-mails pendentes (`IFilaDeEmails.CancelarPendentesAsync`).
5. **Segundos e o `CHECK`**: `DATETIME` sem fração **arredonda** (INFERIDO, medido no ponto). Encerrar no mesmo segundo da publicação com frações diferentes pode gravar expiração < publicação → 3819. Truncar ao segundo e garantir `expiração ≥ publicação`.
6. **Holerite não se apaga** (trigger): se a linha entrar sem arquivo (o `CAMINHO_ARQUIVO` aceita NULL e convida a "inserir, aprovar, depois subir"), uma falha no blob deixa um holerite órfão **eterno** que ainda bloqueia a v1 (`HOLERITE_JA_EXISTE` para sempre). Usar a receita da 1c: validar tudo → blob fora de transação → transação curta → apagar o blob se falhar. Pelo mesmo motivo, nos testes holerite gravado nunca sai (um banco por classe já resolve).
7. **Chave única do holerite** só cobre `VERSAO` igual: duas `substituirHolerite` paralelas calculam o mesmo `MAX+1` → 1062 (→ 409 `HOLERITE_VERSAO_DESATUALIZADA`); dois `enviarHolerite` paralelos da mesma competência → 1062 na v1 (→ 409 `HOLERITE_JA_EXISTE`). `ErrosDoBanco.NomeDaChaveRepetida` dá `UQ_HOLERITES_COMPETENCIA_TIPO_VERSAO` nos dois: decidir pela operação.
8. **Chave da tarefa é eterna**: `IAgendador.CancelarAsync` deixa a chave `CANCELADA` e reagendar com a mesma chave devolve `false` (`Application/Agendamento/IAgendador.cs`). Editar a data de um aviso agendado exige **chave com o instante** (`PUBLICAR_AVISO:aviso=15:em=2026-10-09T11:00:00Z`) ou a tarefa reler a data e reagendar outra chave.
9. **Retenção**: tarefas `CONCLUIDA` somem em 30 dias e e-mails terminados em 90 (`Infrastructure/Agendamento/OpcoesDoAgendador.cs`). Recuperação que reenfileira "todo aviso publicado sem e-mail" **reenviaria** depois de 90 dias. Recuperar só o que ainda é futuro ou recente.
10. **Vazão**: o envio sai em lotes de 20 a cada 10 s (`Infrastructure/Emails/OpcoesDeEmail.cs`), 120/min; `EnfileirarAsync` faz um `SaveChanges` por mensagem. 500 pessoas = 500 idas ao banco dentro da transação se enfileirar no pedido HTTP. Por isso a sugestão (D) de enfileirar o aviso pela tarefa.
11. **Destinatário sem e-mail**: `EMAIL_CORPORATIVO` NULL → o `CHECK` da fila recusa; pular (como `RESULTADO_AJUSTE_PONTO`, STATUS.md:90).
12. **Advertência: colunas anuláveis** (`TIPO`, `MOTIVO`, `DESCRICAO`, `STATUS`, `DATA_ADVERTENCIA`): `MapeamentoContraBancoTests` exige a mesma nulabilidade, então a entidade as declara anuláveis mesmo que a aplicação sempre preencha. Padrões do banco (`FIXADO`, `ENVIADO`, `TIPO_HOLERITE`, `VERSAO`, `VISUALIZADO`) **não** se declaram no EF (roadmap:81 decisão 12a).
13. **Fusos**: `AVISOS`, `AVISOS_VISUALIZACOES`, `HOLERITES`, `ADVERTENCIAS` são do grupo (-03:00, `ConversoresDeTempo.Brasil`); `FILA_EMAILS`/`TAREFAS_AGENDADAS` são UTC. `publicarEm`/`expiraEm`/`dataDoOcorrido` são `date` em -03:00; "hoje" sempre por `FusoBrasil.Hoje(relogio.AgoraUtc)` (às 22:30 locais o dia UTC já virou).
14. **`ANO` é `YEAR`** em `HOLERITES` (1901-2155; o contrato aceita 2000-2100, cabe). Mapear como `short`/`int` com conversão conferida pela sonda.
15. **Gestor do colaborador** = `FUNCIONARIOS.ID_GESTOR` derivado do setor, e o gestor do setor fica com `ID_GESTOR` NULL (`Infrastructure/Colaboradores/GestorDoFuncionario.cs`). Logo `ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO` quase nunca é alcançável pelo RH (a pessoa que é gestora cai em `ADVERTENCIA_SEM_GESTOR`); fica como tradução defensiva do 45000. Conferir `ATIVO` do gestor ao ler (o `ID_GESTOR` pode apontar para gestor desligado).
16. **`DIA_PAGAMENTO` é NN**: "empresa sem dia de pagamento" = `IConfiguracaoDaEmpresa.ObterAsync()` nulo. `ObterOuFalharAsync` lança o texto da 1c ("A empresa ainda não foi configurada."), **diferente** do texto da fatia 4: não reusar.
17. **Desligamento agendado** só grava `DATA_DESLIGAMENTO` na efetivação: até lá o trigger aceita holerite de competência posterior ao último dia.

---

## D. Regras de negócio que o código tem de cumprir

| Regra | Fonte |
|---|---|
| Aviso sempre para todos os ativos; sem público por setor, sem anexo | `yaml:32`, `:114`; roadmap:45, 195 |
| Simples ≤280, texto puro; detalhado markdown ≤10000; HTML nunca interpretado (a API guarda texto; o renderizador de e-mail faz `HtmlEncode`) | `yaml:109-111`, `:2025-2031`; roadmap:75 (decisão 6); CONVENCOES:41 |
| E-mail de aviso **sempre** na publicação (agora ou na data); rascunho não; editar não reenvia; idempotente por (aviso, pessoa) | `yaml:112-115`, `:240`; CONVENCOES:114; roadmap:71 |
| Publicar agendado na hora; o agendador recupera o atraso e é idempotente | roadmap:72; `V001` 3196-3233 |
| Publicar rascunho grava a data **efetiva**; `expiraEm` anterior → 400 | `yaml:333-335`; `V001` 2028-2029 |
| Excluir só não publicado, físico, auditado; publicado só se encerra | `yaml:298-307`, `:382-385` |
| Leitura: abrir marca lido (chamada separada), idempotente, mantém a 1ª data, cria a linha de quem entrou depois | `yaml:608`, `:649-651`; roadmap:196 |
| Quem entrou depois vê os avisos vigentes | `yaml:529`; roadmap:196, 200 |
| Lembrete manual a quem não viu, só ativos, sem limite, 202 | `yaml:489-493`; CONVENCOES:114 (dono, 2026-11-02) |
| Holerite PDF ≤ 5 MiB, tipo pelo conteúdo, arquivo só depois da regra do banco, sobras por job, download por link temporário como anexo, validando o escopo | `yaml:750-751`; roadmap:76, 197 |
| Holerite não se apaga: substitui por nova versão; a pessoa vê a maior publicada; o RH vê o histórico | `yaml:764-765`, `:949-950`; `V001` 1994-1997 |
| Competência futura só em `FERIAS`; anterior à admissão e posterior ao desligamento recusadas | `yaml:762-763`; `V002` 588-603 |
| "No dia do pagamento": o sistema calcula (próximo pagamento de hoje em diante; hoje = publica agora); sem configuração 422 | `yaml:756-759`; `70-fatia-7:2025-2027`, `:2053-2056` |
| Regra do pagamento: dia do mês; mês sem o dia = último; fim de semana/feriado antecipa para o dia útil anterior; exceção do mês vale como está | roadmap:270; `70-fatia-7:2053-2056` |
| E-mail de holerite sempre ao publicar (agora ou no dia agendado, 00:00 -03:00); também a cada nova versão publicada | `yaml:754-755`, `:760-761`, `:951` |
| Holerite sem "novo"/"visto"; gestor não vê holerite da equipe | `yaml:1047-1049`; roadmap:195 |
| Lote: um PDF por pessoa, item a item, resultado por linha; erros do lote todo recusam tudo | `yaml:859-872` |
| Downloads de holerite (RH e da própria pessoa) entram na auditoria; caminho do arquivo nunca sai da API | `yaml:1020-1022`, `:1100-1103`; CONVENCOES:109 |
| Advertência nasce PENDENTE; ciência → APLICADA; contestar só de PENDENTE e nunca sobrescreve; resposta só de CONTESTADA: MANTER → APLICADA, ACATAR → CANCELADA (final) | `yaml:1371`, `:1762-1764`, `:1807-1810`, `:1264-1270`, `:2335` |
| Gestor registra só para a equipe; aplica em nome dele; o RH vê tudo | `yaml:1645-1648`; roadmap:198 |
| E-mails de advertência: registrada (pessoa sempre; cópia **só ao RH** quando o gestor registra; nunca ao gestor); resposta do RH à contestação; **nenhum** na contestação | CONVENCOES:114; `yaml:1377-1380`, `:1650`, `:1808-1809` |
| Contagem única de advertências = não canceladas do ano (resumo do gestor e indicador `advertenciasQuantidade` da ficha, ano corrente) | `yaml:1515-1522`; `20-fatia-1:480`, `:891` |
| Gestor não vê texto de contestação nem desfecho | `yaml:1564-1565` (x-provisorio) |
| Aniversariantes: empresa toda, ativos com nascimento, 0-14 dias em -03:00, 29/02→28/02, sem ano nem idade | `yaml:1862-1867` |
| Gestor reusa as telas de funcionário (`/api/eu/...`) para avisos, holerites e advertências próprias | roadmap:83 (decisão 14); CONVENCOES:23 |
| Mensagem de trigger: texto da API + `regraDoBanco` | roadmap:79; CONVENCOES:53 |
| Privacidade: holerite (salário) e advertência não vão para log; auditoria sem conteúdo do arquivo; `PARAMETROS` de tarefa sem dado pessoal | roadmap:82; `V001` 1472-1473, 3229-3230 |

---

## E. O que existe e o que falta

### E.1 Reaproveitar

| Peça | Onde | Uso na 4 |
|---|---|---|
| `GrupoEu/GrupoGestor/GrupoRh` | Api/Autorizacao/FamiliasDeRota.cs | 403 automático |
| `IEscopoDoUsuario`, `EquipeDoGestor`, `DaEquipe(e)` + `OuNaoEncontrado()` | Application/Escopo/IEscopoDoUsuario.cs; Infrastructure/Persistencia/EscopoDeEquipeExtensions.cs | advertências do gestor (equipe = ativos, admitidos até hoje, do setor, sem o gestor); `GestorId` do gestor logado vai em `ADVERTENCIAS.ID_GESTOR` |
| `IGestorDoFuncionario`/`GestorDoFuncionario` | Infrastructure/Colaboradores/GestorDoFuncionario.cs | quem é "o gestor do colaborador" (via `FUNCIONARIOS.ID_GESTOR`) |
| `IFilaDeEmails` (`EnfileirarAsync`, `EnfileirarLoteAsync`, `CancelarPendentesAsync`), `EmailParaEnfileirar` (`EnviarApos`), `IRenderizadorDeEmail`/`ModeloDeEmail` | Application/Emails/; Infrastructure/Emails/FilaDeEmails.cs | 1062 = já está na fila (sucesso); modelo de renderizador: `ResultadoAjustePontoRenderizador.cs` (HtmlEncode, auditoria sem destinatário) |
| `IAgendador` (`AgendarAsync`, `CancelarAsync`, `RegistrarComoConcluidaAsync`), `ITarefaAgendada` (`ExecutarAsync`, `RecuperarAsync` na subida e a cada hora), `ChaveDeIdempotencia.Montar` | Application/Agendamento/; Application/Comum/ChaveDeIdempotencia.cs | modelo: `EfetivarDesligamentoTarefa` (vencido executa direto, futuro agenda); retentativas 1/5/30/60 min (`ExecutorDeTarefas.cs:29`) |
| `IArmazenamentoDeArquivos`, `CaminhosDeArquivo.Novo`, `IdentificadorDeTipoDeArquivo`, `NomeParaDownload`, `LinkTemporario`, `IFonteDeReferenciasDeArquivo`, `LimparArquivosOrfaosTarefa` | Application/Arquivos/; Infrastructure/Arquivos/ | pasta `holerites`; fonte de referências do prefixo `holerites/` (sem ela, sobras ficam para sempre) |
| `EnvioDeArquivoDeDocumento.GravarEEfetivarAsync(arquivo, pasta, tipo, efetivar)`, `ValidacaoDeArquivoDeDocumento.ValidarAsync(..., tamanhoMaximoBytes, mensagemDoLimite, ...)` | Application/Documentos/ArquivoDeDocumento.cs | a validação aceita **PDF, JPG e PNG** e diz "Envie um arquivo PDF, JPG ou PNG.": o holerite precisa de variante **só PDF** (parametrizar os tipos aceitos) |
| `LeituraDeFormulario.LerAsync/Arquivo`, `RequestSizeLimitAttribute` + `RequestFormLimitsAttribute` | Api/Comum/LeituraDeFormulario.cs; Api/Documentos/DocumentosEndpoints.cs:40; `LimitesDeAtestado` (Application/Ponto/Atestados/AtestadosDtos.cs:13-20) | 415/400 certos; teto da requisição; o lote lê `formulario.Files` inteiro |
| `FormulariosDoContrato` | Api/Configuracao/FormulariosDoContrato.cs | acrescentar `enviarHolerite`, `substituirHolerite`, `enviarHoleritesEmLote` (este com `itens` array e `arquivos` array binário; conferir se o comparador olha `encoding`) |
| `IAuditor` (`Registrar` na transação, `RegistrarEGravarAsync` fora, para download), `[Auditado]`/`[Auditar]`, `MudancaDeAuditoriaNova` | Application/Auditoria/IAuditor.cs; Domain/Auditoria/ | download auditado antes do link: `DocumentosDoColaboradorCasoDeUso.BaixarAsync` (Application/Documentos, 70-91); resposta da contestação em `VALOR_DEPOIS` (TEXT; a `DESCRICAO` tem 500) |
| `ProblemaDeApiException`, `ErroDeNegocioException(codigo, detalhe)`, `ConflitoException(detalhe, codigo)`, `ColetorDeErros`, `NaoEncontradoException` | Application/Comum/ | títulos genéricos já batem com o contrato da 4 |
| `ErrosDoBanco` (`EhChaveRepetida`, `NomeDaChaveRepetida`, `EhChaveEstrangeiraEmUso/SemPai`, `ErroDoMySql`); `TradutorDeRecusaDoPonto`; `MensagensDoPonto` + `RegrasDoBancoDoPonto` | Infrastructure/Persistencia/ErrosDoBanco.cs; Infrastructure/Ponto/; Application/Ponto/Mensagens/ | moldes para `MensagensDaComunicacao` (com teste que lê o yaml) e o tradutor da 4 |
| `ParametrosDeLista`, `RespostaPaginada<T>`, `ToRespostaPaginadaAsync`, `BuscaLike`, `EnumsDeConsulta.Ler`; `EnumsDeParametrosDoContrato`, `NomesDeParametrosDoContrato` | Application/Comum/Consultas/; Infrastructure/Persistencia/ConsultasEf.cs; Api/Configuracao/ | `MapaDeOrdenacao` não é preciso (ordem fixa) |
| `IRelogio`, `FusoBrasil` (`Hoje`, `InicioDoDiaEmUtc`, `ParaUtc`), `ConversoresDeTempo.Brasil` | Application/Comum/Tempo/FusoBrasil.cs; Infrastructure/Persistencia/ConversoresDeTempo.cs | fim do dia = `InicioDoDiaEmUtc(dia+1) - 1 s` |
| `IUnidadeDeTrabalho.ExecutarEmTransacaoAsync`, `OperacoesComTrava` | Application/Autenticacao/Contratos.cs; Infrastructure/Colaboradores/OperacoesComTrava.cs | versão do holerite por pessoa (opcional: a UQ já protege) |
| `IConfiguracaoDaEmpresa.ObterAsync` (`DiaPagamento`); `ICalendarioDoPonto.DiaAsync/FeriadosQueValemAsync` | Application/Empresa/IConfiguracaoDaEmpresa.cs; Application/Ponto/ICalendarioDoPonto.cs | base do cálculo do próximo pagamento (dia útil e feriados da UF/município já existem) |
| `IFonteDoProximoPagamento` (vazia; dita "fatia 7") | Application/Colaboradores/IFonteDeIndicadorDaFicha.cs:24-27 | se a 4 implementar o cálculo, registrar aqui liga o `proximoPagamento` da ficha (decidir: o contrato o dá à 7) |
| `IFonteDeContadorDoMenu` (`ContadorDoMenu.AvisosNaoLidos`) | Application/Painel/ | a 4 registra a fonte (= `contagens.naoLidos`, `70-fatia-7:2204`) |
| `IFonteDeIndicadorDaFicha` (`IndicadorDaFicha.AdvertenciasQuantidade`) | Application/Colaboradores/IFonteDeIndicadorDaFicha.cs | a 4 registra (o comentário do código diz "advertências: 7", o contrato diz 4: `20-fatia-1:480`) |
| `FuncionarioResumo` | Application/Colaboradores/FuncionarioResumo.cs | listas, leituras, aniversariantes |
| Testes: `ApiFactory` (`Relogio.Fixar`, `Armazenamento`, `Enviador`), `ElencoDeTeste` (hoje 2026-10-07T12:42Z; Rh, Gestor, MembroA/B, EmAdmissao, Inativo, GestorDoFinanceiro, DeOutroSetor, GestorSemSetor; **sem data de nascimento**), `ArmazenamentoEmMemoria`, `ContratoDivergenciaTests.OperationIdsNoEscopo`, `RespostaConformeAoContrato`, `MatrizDePerfis` (id `int.MaxValue`, corpo `{}`: 400/404 antes de efeito), `ContratoEStatusTests`, `MapeamentoContraBancoTests`, `GuardaDeExecuteUpdateTests`; `PdfPig` (só testes) e `PDFsharp-MigraDoc 6.2.4` (src) para gerar PDFs de teste | back-end/tests/RHSystem.Api.IntegrationTests/ | |

### E.2 Não existe (criar)

- **Entidades e mapeamentos**: `Aviso`, `LeituraDeAviso`, `Holerite`, `Advertencia` (Domain/Comunicacao ou por assunto), com a sonda de padrões e anuláveis (C.5.12, C.5.14). Nenhuma entidade para `PAGAMENTOS_EXCECOES` (dona: 7c): ler por `Database.SqlQuery` como a 3 leu `FERIAS`.
- **Regras puras**: situação do aviso, fim do dia, prévia do detalhado (C/B), validação de datas do aviso; versão atual/visível do holerite, nome do tipo e do mês para o `HOLERITE_JA_EXISTE`; transições e desfecho da advertência; janela de aniversário com 29/02 e virada de ano; **próximo pagamento** (regra + exceção + dia útil anterior).
- **Catálogo de erros** `MensagensDaComunicacao` + teste contra o yaml; tradutor de recusa (45000 por mensagem; 1062 de holerite pela operação; 1451/1452 → 404 ou 500 consciente).
- **Tarefas**: `PUBLICAR_AVISO` e `PUBLICAR_HOLERITE` (nomes do `V001` 3212), com recuperação.
- **Renderizadores** (lista fechada, CONVENCOES:114): aviso publicado, lembrete de aviso, holerite publicado, advertência registrada (pessoa e cópia ao RH), resposta à contestação. Nomes sugeridos (≤40): `AVISO_PUBLICADO`, `AVISO_LEMBRETE`, `HOLERITE_PUBLICADO`, `ADVERTENCIA_REGISTRADA`, `ADVERTENCIA_RESPONDIDA`.
- **Fontes**: `FonteDeReferenciasDosHolerites` (`holerites/`), contador `AvisosNaoLidos`, indicador `AdvertenciasQuantidade`.
- **Endpoints**: `AvisosRhEndpoints`, `AvisosEuEndpoints`, `HoleritesRhEndpoints`, `HoleritesEuEndpoints`, `AdvertenciasRh/Gestor/EuEndpoints`, `AniversariantesEndpoints`; restrição `{id:int}` para `/resumo` e `/lote` não colidirem com `{avisoId}`/`{holeriteId}`.
- **STATUS.md**: 35 linhas; atualizar as observações de STATUS.md:32-34 ("avisos: 4") e 43-45/51-53 ("os outros `null` até as fatias 4 e 7").

---

## F. Lacunas, contradições e perguntas

### F.1 Decisões do dono já registradas (não reabrir)

Sem público por setor e sem anexo PDF em aviso; e-mail de aviso sempre (2026-10-28); rascunho por `ENVIADO` (2026-10-29); "Lembrar quem não viu" fica e entra na lista fechada (2026-11-02); holerite com tipo e sem "visto" (2026-10-28); competência futura só em férias e holerite errado por versão (2026-10-29; a versão é decisão por delegação, x-provisorio); envio em lote entra (2026-10-30; o **formato** é x-provisorio); advertência com ciência e contestação (2026-10-28); e-mail de advertência só à pessoa quando o RH registra, cópia só ao RH quando o gestor registra (2026-10-30); triggers do grupo não se reescrevem (2026-10-28/29); aniversariantes só API, tela na 7d.

### F.2 Abertas (`x-provisorio`, `PROVISORIOS.md:13-16, 46-49`, roadmap:202)

1. Formato do lote (item a item × tudo ou nada; nome do arquivo com matrícula).
2. Correção por nova versão (o dono pode preferir apagar e reenviar, o que o trigger proíbe).
3. "Ver" × "Baixar PDF" (link sem `attachment`).
4. Desfecho da contestação (guardar a resposta? status próprio?).
5. Tipos de advertência (só `VERBAL`/`ESCRITA`).
6. `aplicadaPor` do RH sem nome (falta coluna) e opções do "Aplicada por".
7. Gestor ver texto da contestação e desfecho.
8. Retenção de holerite de desligado (roadmap:202).

### F.3 Contradições

- **"No dia do pagamento" × ordem das fatias.** O contrato exige o cálculo e o 422 já na 4 (`yaml:756-759`); o roadmap diz "Agenda por data; a regra de pagamento liga depois" (roadmap:305) e põe a regra na 7c (roadmap:270). Proposta: a 4 implementa a regra pura e uma porta (`IProximoPagamento`) que a 7c reusa em `obterProximoPagamento`; perguntar se a 4 já liga o `proximoPagamento` da ficha.
- **Linhas de leitura: criação × publicação.** Contrato: "ativos no momento em que o aviso foi **criado**, mais quem foi admitido depois e abriu" (`yaml:433-434`); roadmap: "as linhas de leitura nascem na **publicação**" (roadmap:196). Divergem em rascunho e agendado. Proposta (INFERIDO): na publicação efetiva (agora ou tarefa) completar com `INSERT ... SELECT` os ativos sem linha; registrar.
- **`publicacaoEmUtc: null` no rascunho** × trigger que sempre preenche: resolvido pelo `UPDATE` para NULL (C.5.2).
- **`FUNCIONARIO_INATIVO` na rota do gestor** (`yaml:1652`, `:1685-1696`) × equipe só de ativos: pela `DaEquipe`, inativo é 404 antes. O 422 só sai na corrida (inativado entre a leitura e o INSERT: o trigger recusa). Mesma lógica para "em admissão" (fora da equipe = 404).
- **`ADVERTENCIA_GESTOR_ADVERTE_A_SI_MESMO`** declarado sem exemplo e quase inalcançável (C.5.15).
- **Cópia ao RH** (`yaml:1650`; CONVENCOES:114; roadmap:198) sem dizer **quem** é "o RH" (todos os usuários ativos de perfil RH com e-mail? um endereço configurado?). `yaml:1647` ainda chama a lista do RH de "cópia".
- **Comentário do código** `IFonteDeIndicadorDaFicha` diz "advertências: 7"; o contrato diz fatia 4 (`20-fatia-1:480`, `:3033`).
- **Download do próprio arquivo**: na 1c "baixar o próprio documento não grava evento" (STATUS.md:72); o holerite da própria pessoa **grava** (`yaml:1103`). Não copiar a 1c.
- **Validação de arquivo**: a 1c aceita PDF/JPG/PNG com texto fixo; o holerite é só PDF (`yaml:2227`).
- **Editar `publicarEmUtc` de AGENDADO**: "vazio ou igual" só vale para ATIVO/EXPIRADO; em AGENDADO, vazio quer dizer "manter" ou "publicar agora"? E `expiraEm` vazio **tira** a expiração (PUT de substituição), mas `publicarEmUtc` vazio em ATIVO **mantém**: semânticas diferentes no mesmo corpo.
- **`FN_CONTAR_AVISOS_NAO_LIDOS`** existe e não serve (C.3); usar seria "trabalho do grupo" (roadmap:80), mas o próprio `V001` 2025-2027 manda não usar.

### F.4 Lacunas que o executor decide e registra

1. **Quem recebe e-mail de aviso**: ativos na hora da publicação; e "em admissão" (ativos com admissão futura, que o trigger inclui)? Sugestão: ativos com `DATA_ADMISSAO ≤ hoje` e e-mail.
2. **`leitura.total`**: todas as linhas (inclusive de quem saiu depois) ou só ativos? Lembrete = linhas não lidas de ativos (o exemplo dá `quantidadeDeEmails 28 = aindaNaoViram 28`, `yaml:458`, `:503`).
3. **Chave do lembrete** sem limite: `AVISO_LEMBRETE:aviso=N:func=M:em=<instante>`; duplo clique manda dois lotes (o contrato aceita; avaliar janela de 1 min).
4. **Resumo dos avisos**: arredondamento da média (`MidpointRounding.AwayFromZero`?); "próximos 7 dias" = `agora < expiração ≤ agora + 7 dias` ou por dia de calendário.
5. **Aviso EXPIRADO editado para expiração futura** volta ATIVO: não manda e-mail (editar não reenvia). ATIVO editado com expiração passada vira EXPIRADO na hora?
6. **Holerite agendado substituído antes do dia**: a tarefa da v2 tem de conferir se ainda é a maior versão antes de mandar e-mail; com v2 agendada para 06/11 e v3 para 10/11, a pessoa vê a v2 de 06 a 10 (leitura literal do contrato).
7. **Holerite de pessoa inativa**: o trigger aceita; mandar e-mail ao endereço corporativo de quem saiu? (contrato diz "sempre").
8. **Nome do tipo no texto do 409** ("mensal", "de férias", "de 13º salário") e mês por extenso em pt-BR.
9. **Lote**: teto da requisição (> 100 MB + campos, senão 413 em vez do 400 do contrato); 100 MB = 100 MiB?; arquivo que passa de 5 MiB vira item `ARQUIVO_INVALIDO` (não 400 do lote); `itens` JSON inválido = 400; textos de `ARQUIVO_INVALIDO` e `FUNCIONARIO_NAO_ENCONTRADO`; um evento de auditoria por item ou um resumo do lote (`yaml:872` "o lote inteiro entra na auditoria"); `FileName` com caminho (navegador antigo) e comparação exata de nomes.
10. **PDF corrompido**: conferir só `%PDF-` (o que o `IdentificadorDeTipoDeArquivo` faz) ou abrir com PDFsharp (custa memória)? O contrato diz "confere o conteúdo, não só a extensão".
11. **Advertência**: data do ocorrido antes da admissão; advertência para "em admissão" pelo RH; e-mail de quem não tem e-mail; auditoria (`REGISTROU`/`CRIOU`, ciência `ALTEROU`?, contestação, resposta `APROVOU`/`RECUSOU`?); quem do RH respondeu só no log.
12. **Gestor sem setor** nas rotas do gestor: listas vazias, resumo com zeros, `porPessoa: []`, registrar = 404 (padrão da 3).
13. **Aniversariantes**: "em admissão" entra? quem não tem `DATA_NASCIMENTO` some; 01/03 de ano comum com aniversário 29/02 → 28/02 do ano seguinte (ou 29/02 se bissexto); virada de ano (25/12 + 14).
14. **Setor do resumo do RH**: pessoa sem setor fica fora do `setorComMaisRegistros`.
15. **E-mails pendentes de quem é inativado** (cancelar ou deixar sair).

---

## G. Proposta de divisão em tarefas

Cada tarefa entrega rotas com os `operationId` em `OperationIdsNoEscopo`, linha no `STATUS.md`, `AfirmarConformeAoContratoAsync` nas respostas e a matriz de perfis passando. Nomes são sugestões.

### 4a. Avisos (#1-#13)

**T1. Mapeamento das quatro tabelas.** `Aviso`, `LeituraDeAviso`, `Holerite`, `Advertencia` com conversores -03:00, anuláveis do banco e sem padrões declarados. Entrega: `MapeamentoContraBancoTests` verde; sonda que grava `FIXADO=false`/`ENVIADO=false`, rascunho com `DATA_PUBLICACAO` NULL depois do `UPDATE`, holerite com `ANO` 2026 e `VERSAO` 2, e relê.

**T2. Regras puras dos avisos.** `SituacaoDoAviso.De(enviado, publicacao, expiracao, agora)`, `ExpiracaoDoAviso.FimDoDia(dia)`, `PreviaDoAviso.Montar(tipo, conteudo)` (os exemplos 398 e 409 como teste), `ValidacaoDoAviso` (280, datas, `expiraEm` ≥ dia da publicação). Testes de unidade + mutação.

**T3. Erros da fatia 4.** `MensagensDaComunicacao` (tabela de A) com teste contra o yaml; `RegrasDoBancoDaComunicacao` (as 5 mensagens de trigger); tradutor 45000/1062.

**T4. Gestão do aviso** (#1-#8). Casos de uso de criar (rascunho: `UPDATE` da data para NULL), editar (regras de `publicarEmUtc` e reagendamento com chave nova), excluir (leituras + aviso + cancelar tarefa e e-mails + `EXCLUIU`), publicar (UPDATE condicional `WHERE ENVIADO = FALSE` → 422 no segundo clique), encerrar (expiração truncada ≥ publicação), obter, listar com as quatro ordens e `leitura`, resumo. Depende de T1-T3.

**T5. Publicação e e-mail.** `PublicarAvisoTarefa` (`PUBLICAR_AVISO:aviso={id}:em={instante}`, `ExecutarApos` = publicação, também para "agora"): confere que o aviso ainda está publicado com aquela data, completa as linhas de leitura dos ativos sem linha, enfileira `AVISO_PUBLICADO:aviso={id}:func={id}` (pula sem e-mail); `RecuperarAsync` agenda o que é futuro. Renderizador com `HtmlEncode` e o conteúdo como texto. Entrega: agendado dispara uma vez; rodar duas vezes não duplica; editar a data reagenda; excluir cancela; 300 pessoas sem segurar o pedido HTTP.

**T6. Leituras e lembrete** (#9, #10). `AVISO_LEMBRETE` com chave por clique; 202; só ativos não lidos com e-mail. Entrega: os totais de `yaml:453-465`; `NAO_LIDO` + `tamanhoPagina=5`.

**T7. Avisos da própria pessoa** (#11-#13) e a bolinha `AvisosNaoLidos`. Vigentes sem depender da linha; marcar lido idempotente (UPDATE condicional; sem linha → INSERT; 1062 → sucesso; data do `IRelogio`). Entrega: admitido depois vê e marca; rascunho/agendado/expirado = 404; contador do menu igual a `contagens.naoLidos`.

### 4b. Holerites (#14-#20)

**T8. Próximo pagamento.** Regra pura (dia, último do mês, antecipa por fim de semana e feriado de `ICalendarioDoPonto`, exceção de `PAGAMENTOS_EXCECOES` por `SqlQuery`, de hoje em diante) e porta `IProximoPagamento.ObterAsync(hoje) : DateOnly?`. Testes: fevereiro com dia 30, dia em feriado, sábado, hoje é o dia, exceção.

**T9. Envio individual e nova versão** (#15, #17). Validação só PDF ≤ 5 MiB, `publicarEm`/`noDiaDoPagamento`, competência pelo `IRelogio` (com `regraDoBanco` fixo), 404 antes, versão `MAX+1`, receita blob → transação → apagar blob, pasta `holerites`, e-mail na hora (`HOLERITE_PUBLICADO:holerite={id}`) ou `PublicarHoleriteTarefa` (`PUBLICAR_HOLERITE:holerite={id}`, `ExecutarApos` = 00:00 -03:00, confere versão atual antes de mandar), `FonteDeReferenciasDosHolerites`, `FormulariosDoContrato`. Entrega: 6 MiB e PNG = 400; arquivo órfão apagado quando o banco recusa; paralelo = um 201 e um 409; agendado invisível até o dia.

**T10. Lote** (#16). Leitura de `itens` + `arquivos`, erros do lote (400/422) antes de qualquer efeito, cada item com a receita da T9 em transação própria, resultado por item. Entrega: lote com 1 linha inválida; nome repetido; arquivo sem item; 200 PDFs; teto da requisição.

**T11. Listas e downloads** (#14, #18, #19, #20). Versão atual × visível; `ultimoPublicado`; contagens e anos; download com evento `HOLERITES`/`BAIXOU` gravado **antes** do link. Entrega: A não baixa o de B (404); agendado e versão substituída = 404 para a pessoa; RH baixa qualquer versão.

### 4c. Advertências e aniversariantes (#21-#35)

**T12. Advertências do RH** (#21-#26). Registrar (inativo com `regraDoBanco`, gestor do colaborador ativo ou `ADVERTENCIA_SEM_GESTOR`, data futura 400, e-mail `ADVERTENCIA_REGISTRADA:advertencia={id}:func={id}`), listas, resumo, detalhe com desfecho derivado, responder (UPDATE condicional `WHERE STATUS='CONTESTADA'`, e-mail com a resposta na carga, resposta na auditoria em `VALOR_DEPOIS`). Indicador `AdvertenciasQuantidade`.

**T13. Advertências do gestor** (#27-#31). `DaEquipe` em tudo; `ID_GESTOR` = `equipe.GestorId`; cópia ao RH (destinatários decididos em F.3). Entrega: outra equipe, o próprio, inativo = 404; corrida de inativação = 422.

**T14. Advertências da própria pessoa** (#32-#34). PENDENTE primeiro; ciência e contestação por UPDATE condicional `WHERE STATUS='PENDENTE'` (contestar duas vezes, contestar depois da ciência e ciência depois de contestar = 422; nada sobrescreve). Sem e-mail.

**T15. Aniversariantes** (#35). Regra pura (janela, 29/02, virada de ano) + consulta (ativos com nascimento, só `FuncionarioResumo`). Entrega: teste com o relógio em 28/02/2027 e em 25/12.

**T16. Fechamento.** Os 35 `operationId` no escopo, STATUS.md (inclusive as observações das linhas 32-34, 43-45 e 51-53), matriz de perfis, mutação por incremento, revisão.

**Ordem e dependências:** T1 → T2 → T3 → T4 → T5 → T6 → T7 (fim da 4a) → T8 → T9 → T10 → T11 (fim da 4b) → T12 → T13 → T14 → T15 → T16. T8 é independente e pode correr junto da 4a; T15 só depende de T3; T10 depende da T9.

---

## O que mais pode dar errado

As cinco condições que o contrato não cita e que mais machucam: **(1) duplo clique e duas abas** em publicar aviso, enviar holerite, dar ciência e responder contestação: sem UPDATE condicional e sem mapear o 1062 da chave única, sai e-mail em dobro ou um 500; o lembrete, que não tem limite, manda o lote duas vezes. **(2) Dois RHs mexendo no mesmo aviso**: o PUT não tem versão nem 409, então vence o último; pior, um muda a data de um agendado enquanto o outro exclui, e a tarefa antiga, se não reler a data e a situação, manda e-mail de um aviso que não existe ou na hora errada (chave da tarefa é eterna: C.5.8). **(3) PDF enorme ou corrompido**: acima do teto da requisição o servidor corta com 413 (que o contrato não lista); arquivo que começa com `%PDF-` e está quebrado passa na conferência de bytes e só falha no navegador da pessoa, depois do e-mail; holerite com arquivo faltando nunca mais sai do banco. **(4) Lote com 200 pessoas e 100 MB**: o pedido fica minutos aberto (200 uploads ao blob, 200 transações, 200 e-mails), estoura o tempo do proxy e o RH reenvia: metade volta como `HOLERITE_JA_EXISTE`, o que é correto mas confuso; e uma queda no meio deixa blobs sem linha (a limpeza de sobras só os vê com a fonte de referências registrada). **(5) Fuso nas datas**: `publicarEmUtc` montado pelo front com a hora local como se fosse UTC adianta o aviso em 3 h; `publicarEm`/`expiraEm`/`dataDoOcorrido` comparados com o dia UTC viram "amanhã" depois das 21:00 locais; e os triggers julgam competência e datas pelo relógio real do servidor, não pelo `IRelogio`, então um teste que passa hoje com a competência de novembro falha (ou passa por acaso) quando o calendário real andar.
