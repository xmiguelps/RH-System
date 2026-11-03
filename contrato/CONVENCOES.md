# Convenções do contrato da API

O contrato é escrito **antes** do back-end (design `docs/superpowers/specs/2026-10-30-planos-backend-frontend-design.md`, seção 5). Quem faz o back-end implementa o que está aqui; quem faz o front gera tipos e dados simulados (MSW) a partir daqui. **Só o dono muda o contrato.** O front nunca edita estes arquivos: se faltar algo, vira dúvida para o dono.

Tudo abaixo é **escolha de engenharia** da sessão que escreveu o contrato (2026-10-30), a ser revista pelo dono. O que já estava fixado no plano da fatia 0 está marcado como **(fixado)**.

## 1. Arquivos

- `fontes/NN-nome.yaml`: um arquivo por fatia (ou parte dela). Cada um tem `paths` e/ou `components`. `00-base.yaml` tem o cabeçalho, a segurança e o que todas as fatias compartilham.
- `montar.mjs` junta tudo em `openapi.yaml` (**gerado, não editar**; é este arquivo que o front e o teste do back-end leem) e gera `ROTAS.md` (lista de rotas em português simples, para o dono) e `PROVISORIOS.md`.
- Comandos (a partir de `contrato/`): `npm ci`, `npm run montar`, `npm run validar` (monta e roda o lint do Redocly).
- Referências `$ref` são sempre locais (`#/components/schemas/Nome`). Nomes de schema, resposta e parâmetro são **globais**: o `montar` recusa nome repetido, então use nomes específicos (`FeriasPedido`, não `Pedido`).
- OpenAPI **3.1.0** (a versão que o gerador nativo do .NET 10 deve produzir; **a conferir** na Task 9 da fatia 0).

## 2. Rotas

- Prefixo `/api`. Português **sem acento**, plural, `kebab-case`: `/api/funcionarios/{id}`, `/api/ponto/pedidos-de-ajuste`.
- O recurso "colaborador" da tela se chama **`funcionario`** na API (é o nome do banco, do domínio e do perfil). Referência a uma pessoa é sempre `funcionarioId`.
- **Famílias de rota** (cada uma com seu perfil, para que cada perfil receba o seu DTO e a matriz de perfis fique simples):
  - `/api/auth/...`: login e sessão **(fixado)**; `entrar`, `renovar` e `sair` são públicas.
  - `/api/rh/...`: só o perfil `RH`.
  - `/api/gestor/...`: só o perfil `Gestor`, sempre sobre **a equipe dele** (o setor do gestor).
  - `/api/eu/...`: os dados **da própria pessoa logada**, para qualquer perfil (um gestor ou alguém do RH também usa `/api/eu/...` para o ponto, os holerites e os documentos dele).
  - `/api/publico/...`: **sem login** (formulário público do candidato).
  - `/health`: saúde da API **(fixado)**.
- **Nomes que várias fatias usam** (para as partes se encaixarem): abas da ficha da pessoa são sub-recursos, `/api/rh/funcionarios/{funcionarioId}/{aba}` (`ponto`, `ferias`, `treinamentos`, `advertencias`, `documentos`) e, para o gestor, `/api/gestor/equipe/{funcionarioId}/{aba}` (a equipe em `/api/gestor/equipe`). Assuntos: `/api/rh/avisos` (gestão) e `/api/eu/avisos` (leitura); `/api/rh/holerites` e `/api/eu/holerites`; `/api/rh/formularios`, `/api/eu/formularios` (responder) e `/api/publico/formularios/{token}`; `/api/rh/treinamentos` e `/api/eu/treinamentos`; `/api/rh/ponto`, `/api/gestor/ponto` e `/api/eu/ponto`; cadastros de apoio em `/api/rh/cargos`, `/api/rh/setores` e `/api/rh/beneficios`; empresa em `/api/eu/empresa` (leitura, qualquer perfil) e `/api/rh/empresa` (escrita); importação em `/api/rh/importacoes`; contadores do menu em `/api/{rh,gestor,eu}/contadores-do-menu`. **Exceção aceita:** em rotas que já têm um `funcionarioId` (quem avalia, quem responde), a outra pessoa tem o nome do papel (`avaliadoId`). **Auditoria:** todas as fatias usam os enums `AuditoriaArea` e `AuditoriaAcao` da fatia 1; valor novo é mudança de versão menor.
- **Nomes de schema** levam o assunto como prefixo (`Ponto...`, `Ferias...`, `Aviso...`, `Holerite...`, `Advertencia...`, `Formulario...`, `Treinamento...`, `BeneficioCatalogo...` e `BeneficioAdesao...`, `Cargo...`, `Setor...`, `Importacao...`, `Painel...`), porque o `montar` recusa nome repetido.
- Nunca um campo "pode ou não vir conforme o perfil": se os campos mudam, é outra rota e outro schema (`FuncionarioFichaRh`, `FuncionarioFichaGestor`).
- Toda operação declara **`x-perfis`**: lista com `RH`, `Gestor`, `Funcionario` ou `Publico`. Rota para qualquer logado: `[Funcionario, Gestor, RH]`. Rota pública: `[Publico]` e `security: []`.
- Perfil errado na rota: **403**. Objeto que existe mas está **fora do escopo** da pessoa (um gestor pedindo alguém de outro setor): **404**, para não revelar que existe.
- `operationId`: `camelCase` em português sem acento, verbo no infinitivo (`listarFuncionarios`, `aprovarPedidoDeAjuste`). `summary`: **uma linha em português simples** (o dono lê isto); `description`: regras de negócio, perfis e erros específicos. `tags`: um por assunto.

## 3. JSON

- Propriedades em `camelCase` **(fixado)**; ids inteiros (`id`, `funcionarioId`) **(fixado)**; exceção: ids que o banco define como UUID (`participacaoId`).
- **Enums** como texto em `MAIÚSCULAS_COM_SUBLINHADO`, **iguais aos valores do banco** quando existirem (`CREDITO`, `DEBITO`, `PENDENTE`). Exceção **(fixada)**: `perfil` é `Funcionario`, `Gestor` ou `RH`.
- **Instante** (um momento no tempo): `date-time` em **UTC terminando em `Z`** **(fixado)**; o front mostra em `America/Sao_Paulo`. **Data de calendário** (admissão, dia do ponto, feriado): `date` (`2026-10-30`), é o dia no fuso `-03:00`. **Horário do dia** (horário padrão, entrada e saída): texto `HH:mm`, sem fuso. A conversão UTC ↔ `-03:00` é do back-end (regra de fuso do roadmap, decisão 4).
- **Duração**: minutos inteiros, sufixo `Minutos` (`saldoMinutos`). **Dinheiro**: centavos inteiros, sufixo `EmCentavos` (`valorEmCentavos`).
- Campo que pode ser vazio: `type: [string, 'null']`; campo que pode faltar fica fora de `required`. Lista nunca é `null`: é `[]`.
- CPF e telefone: o RH recebe **completos**, sem máscara, em `cpf` e `telefone` (decisão do dono, 2026-10-30); o gestor não recebe CPF e recebe o telefone em `telefoneMascarado` (`(11) 9****-7783`). O contrato nunca expõe hash de senha, token de convite nem dado que a regra de privacidade proíbe (roadmap, decisão 13).
- Conteúdo escrito por usuário (avisos, perguntas, mensagens) é **texto ou markdown**, nunca HTML (roadmap, decisão 6): o schema diz `format: markdown` na descrição.

## 4. Listas

- Parâmetros: `pagina` (a partir de 1, padrão 1), `tamanhoPagina` (padrão 20, máximo 100), `busca` (texto livre), `ordenarPor` (enum próprio de cada rota) e `direcao` (`ASC` ou `DESC`), mais um parâmetro por filtro, com nome do campo (`setorId`, `situacao`).
- Resposta: `itens` + `pagina`, `tamanhoPagina`, `total` e `totalPaginas`. Use o schema `Paginacao` com `allOf` (ver `10-fatia-0-autenticacao.yaml` e os exemplos da fatia 1).
- Lista curta e limitada (cargos, setores) pode vir sem paginação, como `itens` sem os outros campos; diga isso na `description`.
- Exportações (CSV, PDF): `GET ...` que devolve o arquivo com `Content-Disposition: attachment`; células de CSV que começam com `=`, `+`, `-` ou `@` são neutralizadas (roadmap, decisão 9).

## 5. Erros

- Todo erro é `application/problem+json` em português **com acento**, **sem stack trace** **(fixado)**: `title`, `status`, `detail`, `traceId`, e `codigo` (texto estável, em `MAIÚSCULAS_COM_SUBLINHADO`, quando o front precisa decidir algo pela regra violada).
- Respostas padrão já definidas em `00-base.yaml`: **400** validação (mapa `errors` por campo), **401** sem login ou token vencido, **403** perfil errado, **404** não existe ou fora do escopo, **409** conflito (CPF repetido, versão desatualizada), **422** regra de negócio ou erro de trigger do banco (`regraDoBanco` com a mensagem original, `detail` genérico) **(fixado)**, **429** muitas tentativas (cabeçalho `Retry-After`) **(fixado)**, **500** erro inesperado.
- Cada operação lista **só** os erros que ela pode dar, com `$ref` para essas respostas; erros específicos (por exemplo, o 403 do login) ganham `example` próprio.

## 6. Arquivos enviados e baixados

- Envio: `multipart/form-data`; o schema diz tipos aceitos e tamanho máximo.
- Download: nunca devolve os bytes pela API de negócio. `GET .../{id}/download` devolve `LinkTemporario` (`url` e `expiraEmUtc`); o arquivo vem por link temporário e como anexo (roadmap, decisão 7).

## 7. Exemplos

- **Todo corpo de resposta 2xx em JSON tem `example`** (ou `examples`) no próprio objeto de mídia, com dados verossímeis do domínio (nomes, setores e valores brasileiros), porque o front gera os dados simulados deles. O `montar` recusa a falta.
- Todo corpo de requisição tem `example`.

## 8. Decisão ainda aberta

- O que depende de uma decisão do dono sem resposta recebe **`x-provisorio: "qual decisão e o que muda"`** (na operação, no schema ou na propriedade). O `montar` junta todos em `PROVISORIOS.md`. O front constrói normalmente em cima do que está escrito; a lista diz quais decisões podem mudá-lo.

## 9. Aprovação em duas etapas (ponto e férias): um só modelo

Vale para o pedido de ajuste de ponto (fatia 3) e para o pedido de férias (fatia 7). **Valores de `status` são os do banco** (ponto: `PENDENTE`, `APROVADA`, `REJEITADA`; férias: `SOLICITADA`, `APROVADA`, `REJEITADA` e os demais do banco), nunca renomeados. A resposta de qualquer pedido traz:
- `etapa`: `GESTOR`, `RH` ou `CONCLUIDA`;
- `podeDecidir` (boolean, para **quem está consultando**) e `motivoNaoPodeDecidir` (texto em português ou `null`), para o front habilitar ou desabilitar os botões sem adivinhar;
- `situacao` calculada, quando a tela precisar de mais valores que `status` (por exemplo, sugestão de data em férias), **além** de `status` e `etapa`, nunca no lugar.

Regras:
- **Sem etapa do gestor:** pedido de quem é gestor (tem linha em `GESTORES`), de quem não tem gestor ativo no setor e de quem tem gestor inativo. O pedido nasce com `etapa: RH`. Só o RH decide (o RH que decide precisa estar também em `GESTORES`, por causa do trigger).
- **O gestor recusa:** o pedido termina na hora (`REJEITADA`, `etapa: CONCLUIDA`). **O RH pode recusar o que o gestor aprovou** (decisão do dono, 2026-10-30).
- **"Gestor ausente"** (antiga "chave de aprovação automática"; rota única da fatia 1, `PUT /api/rh/gestores/{gestorId}/ausencia`, com data de volta opcional): o RH liga quando o gestor está ausente **por qualquer motivo, a qualquer momento** (vale para ponto e para férias). Enquanto ligada, a etapa do gestor é aprovada sozinha nos pedidos novos. **Ao ligar**, a API também aprova a etapa do gestor dos pedidos já parados nele, exceto férias com sugestão de data em andamento. **Ao desligar** (ou na data de volta), só os pedidos novos voltam ao normal. (Efeito sobre os pedidos já parados: escolha de engenharia; avisar o dono.)
- **Erros, mesmos códigos nas duas fatias:** decidir fora da vez (gestor depois do RH ou RH antes do gestor) é **409** `PEDIDO_ETAPA_INCORRETA`; pedido que já foi decidido é **409** `PEDIDO_JA_DECIDIDO`; decidir o **próprio** pedido é **422** `PEDIDO_PROPRIO` **só onde o trigger recusa**: quem decide pelo gestor (por não haver etapa do gestor) e é a dona do pedido. Se a etapa do gestor foi feita por **outra pessoa**, o RH pode confirmar o próprio pedido (decisão do dono, 2026-10-29; `Scripts/LEIA-ME.md`, seção «O que eu gostaria que você questionasse»). O RH que decide sem etapa do gestor precisa ter linha em `GESTORES`: senão **422** `PEDIDO_RH_FORA_DE_GESTORES`.
- **Setor que fica sem gestor ativo** enquanto um pedido espera o gestor: o pedido passa para a etapa do RH. O mesmo quando o gestor **deixa** o setor (ou é trocado): os pedidos já parados nele passam ao RH.
- **Só quem tem perfil `Gestor` é gestor de setor** (um gestor, um setor). Alguém do RH tem linha em `GESTORES` apenas para poder decidir pedidos de gestores; cadastrá-lo como gestor de um setor é **422** `GESTOR_PRECISA_TER_PERFIL_GESTOR`. Quem deixa de ser gestor de setor volta ao perfil `Funcionario`.
- **Forma única da decisão de cada etapa** (ponto e férias): o pedido traz `decisaoGestor` e `decisaoRh`, ambas do schema `DecisaoDeEtapa` (`null` enquanto não decidida), mais `gestorResponsavel` (`FuncionarioResumo` ou `null`). Nada de campos soltos diferentes por fatia. A observação da decisão é opcional no ponto e **obrigatória ao recusar férias** (5 a 500 caracteres; coluna nova de motivo, script a numerar pelo dono) e a pessoa a vê.
- **Pedido de gestor ou de quem não é da equipe**: nas rotas `/api/gestor/...` é **404** (fora do escopo), nunca 422.
- **Textos de `motivoNaoPodeDecidir`:** os mesmos nas duas fatias (os cinco textos fixos estão em `30-fatia-3-ponto.yaml`, schema de pedido de ajuste).

## 10. Situação de hoje e "presença agora" (uma definição só)

Enum `SituacaoHoje` (em `00-base.yaml`), usado por todas as fatias que mostram a situação de uma pessoa **hoje**:
- `SEM_EXPEDIENTE`: sábado, domingo ou feriado.
- `FERIAS`: férias aprovadas que cobrem hoje. `ATESTADO`: atestado validado que cobre hoje.
- `TRABALHANDO`: já registrou entrada hoje (mesmo que já tenha saído).
- `SEM_REGISTRO`: dia com expediente, sem entrada, **depois do horário padrão de entrada mais a tolerância**.
- `AINDA_NAO_COMECOU`: dia com expediente, sem entrada, **antes** desse limite. Não entra em "sem registro".

**Precedência** (escolha de engenharia): quem já registrou entrada hoje é `TRABALHANDO` (a batida vence). Sem entrada, vale a primeira que se aplica nesta ordem: `SEM_EXPEDIENTE`, `FERIAS`, `ATESTADO` (só atestado de dia inteiro), `SEM_REGISTRO`, `AINDA_NAO_COMECOU`.

**Contagens:** o schema `PresencaContagens` (base) é a única forma de contar situações de hoje (equipe do gestor, painel do RH, ponto): `trabalhando`, `ferias`, `atestado`, `semRegistro`, `aindaNaoComecou`, `semExpediente`; `diaUtil` (boolean) fica ao lado, no objeto que as contém. Listas de nomes (quem está sem registro, quem está de férias) vão em campos separados (`pessoasSemRegistro`, `pessoasDeFerias`). Num dia sem expediente o cartão mostra "Hoje não há expediente" e, se alguém bateu entrada, só `trabalhando`. Em horário normal `aindaNaoComecou` só é maior que zero antes do limite (entrada + tolerância); às 09:42 vale 0.

"Presença agora" (cartão da tela 00) conta, entre os colaboradores ativos **com expediente hoje**, quantos estão em `TRABALHANDO`, `FERIAS`, `ATESTADO` e `SEM_REGISTRO`; a soma dos quatro mais `AINDA_NAO_COMECOU` é o `total`. Em dia `SEM_EXPEDIENTE` o cartão mostra só "Hoje não há expediente". Quem pede só a lista de quem está sem registro recebe `FuncionarioResumo` com `SituacaoHoje`. A situação do **dia no espelho de ponto** (histórico de um dia passado) é outro conceito e tem enum próprio na fatia 3.

## 11. Nomes que se repetem

- **Exportar** (CSV ou xlsx devolvido direto): `GET .../exportacao`. **Baixar arquivo guardado** (PDF, anexo): `GET .../{id}/download`, que devolve `LinkTemporario`. O espelho de ponto em PDF é `GET .../espelho/download`.
- **Erros iguais têm o mesmo `codigo` em todas as fatias:** `FUNCIONARIO_INATIVO` (422), `EMPRESA_NAO_CONFIGURADA` (404 ao ler a empresa; 422 quando outra operação precisa dela), `PEDIDO_ETAPA_INCORRETA`, `PEDIDO_PROPRIO`, `PEDIDO_JA_DECIDIDO`, `CONFIRMACAO_NECESSARIA` (409, ao encerrar ou excluir com pendentes; traz `pendentes` e a operação aceita `confirmar=true`; em formulário anônimo publicado a confirmação é pedida sempre e `pendentes` não vem, para não revelar quantos faltam; treinamento que já emitiu certificado **não se exclui**: 422 `TREINAMENTO_COM_CERTIFICADOS`, só se encerra), `JA_RESPONDIDO`, `PEDIDO_RH_FORA_DE_GESTORES`.
- **Parâmetro de caminho nunca se chama `data`** (conflita com geradores de código): use `{dia}`.
- **Rotas abertas a todos os perfis não listam 403.**
- **Exemplos:** só usam as pessoas, os ids e a data de `ELENCO.md`. Exemplo de arquivo nunca mostra caminho interno de armazenamento (`holerites/2026/...`): o link temporário é uma URL assinada genérica.
- **Eu:** `UsuarioResposta` traz também `cargo` e `setor` (texto, pode ser `null`), porque o rodapé do menu os mostra.

## 12. E-mails: lista fechada (dono, 2026-10-30)

O sistema envia **só** estes e-mails: convite; redefinição de senha; aviso publicado (sempre); holerite publicado (sempre); **resultado de pedido de ajuste de ponto e de férias** (a pessoa recebe um e-mail a **cada decisão**: a do gestor, a do RH e, nas férias, quando o gestor sugere outra data; decisão do dono, 2026-10-30; o pedido **criado** e a correção direta do RH não mandam e-mail); advertência registrada (a pessoa; e, como cópia, **só o RH** quando quem registra é o gestor; o gestor **não** recebe cópia quando o RH registra, decisão do dono, 2026-10-30) e resposta do RH à contestação; o lembrete manual do RH a quem ainda não viu um **aviso** ("Lembrar quem não viu"; decisão do dono, 2026-11-02); e, nos **formulários**, o lembrete manual do RH ("Lembrar quem falta"), o lembrete manual do **gestor** só para a própria equipe (decisão do dono, 2026-11-02) e o **lembrete automático 7 dias antes do prazo**. No formulário anônimo o lembrete vai só a quem ainda não participou, **nunca registra o destinatário** e **nunca devolve nem registra número** (a auditoria guarda só "lembrete enviado"). **Nenhum outro e-mail existe:** sem aviso automático de treinamento ou formulário publicado (o RH usa um Aviso, manualmente), sem lembrete de treinamento nem de documento, sem e-mail de resposta de benefício e sem e-mail de férias registradas pelo RH. **Também não há e-mail de "formulário atribuído"** (o dono manteve o aviso manual, 2026-10-30). Por isso não há campo `avisarPorEmail` nem botão "Lembrar" nessas telas.

## 13. Treinamentos não têm prazo (dono, 2026-10-30)

Treinamento fica disponível para quem entrar na empresa: não existe prazo final, "atrasada" nem lembrete antes do fim. Existe publicar, a data de início (liberação agendada), e o RH pode **encerrar** ou **excluir** a qualquer momento; se houver pessoas que ainda não concluíram, a operação devolve 409 `CONFIRMACAO_NECESSARIA` com `pendentes` e só executa com `confirmar=true`. O mesmo vale para encerrar e excluir **formulário**, que tem prazo e se encerra sozinho nele.

## 14. Versão e mudanças

- `info.version` em `00-base.yaml`; `CHANGELOG.md` registra cada mudança. Tirar ou renomear rota ou campo, ou tornar um campo obrigatório: versão **maior**. Acrescentar rota ou campo opcional: **menor**. Só texto ou exemplo: **correção**.
- `STATUS.md` (mantido pelo dono) diz quais rotas já existem de verdade na API; é por ele que o front sabe se um passo de integração já pode rodar.
