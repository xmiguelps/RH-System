# Esquema do banco: o que a aplicação acrescentou aos arquivos do grupo

**Atualização de 2026-11-03: os antigos scripts `V006` a `V047` foram incorporados ao `V001` e ao `V002`.** A pasta tem hoje `V001` a `V005` e este arquivo. O texto original de cada script antigo continua no histórico do git (commit `10fe79a`; por exemplo `git show 10fe79a:"Banco de Dados/Scripts/V033__usuarios_tokens_senha.sql"`). **Neste arquivo, os números `V006` a `V047` são os dos scripts antigos**; as tabelas abaixo dizem de que pedido veio cada um e onde está hoje.

| Script | O que tem hoje |
|---|---|
| `V001` | todas as tabelas (as 25 do grupo, já com as colunas, regras e índices que vieram depois, e as 22 tabelas novas nas seções 26 a 47), os índices, as chaves estrangeiras, os feriados de 2026 e o contador `SEQUENCIAS` |
| `V002` | os 36 triggers do grupo (com os itens 1 e 2 corrigidos e a exceção de férias no trigger de holerite) |
| `V003` | as 8 funções do grupo |
| `V004` | `REFRESH_TOKENS` (login da API) |
| `V005` | os níveis de acesso iniciais (`RH`, `Funcionario`, `Funcionario Gestor`) |

Dentro do `V001`, cada grupo de colunas ou regras acrescentado depois vem com um comentário que diz de que pedido veio (o título do antigo script), e cada tabela nova tem uma seção numerada com o mesmo título. **Para conferir o que a aplicação mudou**, procure esses comentários ou compare com o `V001` e o `V002` de antes da junção (no histórico, commit `f69db4e`). O banco que sai do `V001` ao `V005` foi conferido contra o que os 47 scripts produziam: veja "Como a base nova foi conferida".

## Como chegamos aqui (histórico)

**Atualização de 2026-10-30:** a pasta ganhou 18 scripts (`V030` a `V047`), escritos pelo lado da aplicação a partir do **contrato da API** (`contrato/`), a pedido do dono, para **todo o esquema ficar pronto antes de o back-end começar**; o Kauan confere depois, como nos `V006` a `V029`. Veja "Scripts V030 a V047" mais abaixo. Os números são provisórios: o dono pode renumerar.

Os 24 scripts `V006` a `V029` fazem o que a lista `docs/pedidos-para-o-kauan.md` pedia ao Kauan (itens 3 a 27). Foram escritos pelo lado da aplicação, a pedido do dono (2026-10-29), **para o Kauan conferir depois**. Cada script começa com um comentário que explica **por que** existe, **o que faz**, quais decisões foram tomadas e o que **fica com a aplicação**. Leia o comentário antes do SQL.

Naquele momento, dos três arquivos do grupo em `Banco de Dados/`, **só o de triggers tinha sido alterado** (itens 1 e 2, com autorização do dono; veja "Itens 1 e 2" abaixo). Em 2026-11-03 o `V001` também recebeu as mudanças dos scripts antigos (o dono decidiu juntar tudo antes do primeiro deploy) e o `V002` ganhou a exceção de férias do trigger de holerite.

## Mapa: de que pedido veio cada script antigo (`V006` a `V029`)

Hoje todos estão dentro do `V001`, exceto a parte de trigger do `V012`, que está no `V002`.

| Script | Item da lista | O que faz |
|---|---|---|
| `V006` | 3 | matrícula e tipo de contrato em `FUNCIONARIOS` |
| `V007` | 4 e 16 | `CONFIGURACOES_EMPRESA` (horário padrão, tolerância, UF, município, dia de pagamento) |
| `V008` | 5 | origem da batida em `PONTOS_MARCACOES` |
| `V009` | 6 | `PONTOS_MANUTENCOES_ITENS`: as batidas pedidas no ajuste de ponto (várias por pedido) |
| `V010` | 7 | dia de referência em `BANCOS_HORAS` |
| `V011` | 8 | aprovação em duas etapas (ponto e férias) e chave de aprovação automática em `GESTORES` |
| `V012` | 9 | holerites: tipo e versão, e **ajuste de um trigger** |
| `V013` | 10 | avisos: `FIXADO` e `ENVIADO` (rascunho) |
| `V014` | 11 | advertências: ciência e contestação |
| `V015` | 12 | formulários: publicação, prazo, recorrência, anônimo, link público |
| `V016` | 22 | alternativas das perguntas de múltipla escolha |
| `V017` | 13 | respostas: candidato de fora, resposta anônima, quem é avaliado |
| `V018` | 23 | quem deve responder cada formulário |
| `V019` | 24 | participação em formulário anônimo (sem ligar pessoa a resposta) |
| `V020` | 14 | férias: 13º adiantado e "sugerir outra data" |
| `V021` | 15 | benefícios: pedido de adesão |
| `V022` | 17 | documentos do colaborador |
| `V023` | 18 | atestados |
| `V024` | 19 | dependentes |
| `V025` | 20 | trajeto do vale-transporte |
| `V026` | 21 | módulos do treinamento e progresso por módulo |
| `V027` | 25 | exceções do dia de pagamento |
| `V028` | 26 | feriados: "repetir todo ano" |
| `V029` | 27 | treinamentos: rascunho, público e "emitir certificado" |

## Scripts V030 a V047 (2026-10-30): colunas e tabelas que o contrato da API exige

Hoje tudo isto está no `V001`. Medição de 2026-10-30, com os scripts antigos: testados em conjunto num MySQL 8.4.13 limpo com `--default-time-zone=-03:00`: os arquivos do grupo e `V006` a `V047` aplicam sem erro e deixam **47 tabelas, 36 triggers, 8 funções e 238 índices**. **Sem triggers novos.** Cada script começa com um comentário que diz por que existe, o que faz e o que fica com a aplicação.

| Script | O que faz |
|---|---|
| `V030` | matrícula gerada pelo sistema (tabela `SEQUENCIAS` e `CHECK` de 5 dígitos em `FUNCIONARIOS.MATRICULA`) |
| `V031` | `BENEFICIOS`: `OPCIONAL`, `ACEITA_DEPENDENTES`, `ACEITA_TRAJETO` |
| `V032` | `USUARIOS.SENHA_PROVISORIA` (convite ainda não aceito) |
| `V033` | `USUARIOS_TOKENS_SENHA` (convite e redefinição: só o hash, uso único) |
| `V034` | `DESLIGAMENTOS_AGENDADOS` (agendar, efetivar, cancelar, readmitir) |
| `V035` | `FILA_EMAILS` (idempotência, vazão, carga apagada ao terminar, lembrete anônimo sem destinatário) |
| `V036` | `TAREFAS_AGENDADAS` (estado idempotente) |
| `V037` | `AUDITORIA_EVENTOS` e `AUDITORIA_MUDANCAS` (só inclusão, `VALOR_OCULTO`) |
| `V038` | `IMPORTACOES` (prévia em JSON, validade de 1 hora, tudo ou nada) |
| `V039` | `PONTOS_MARCACOES.DATA_CORRECAO` (quando a batida foi corrigida) |
| `V040` | um pedido de ajuste de ponto pendente por dia (coluna calculada e chave única) |
| `V041` | `FERIAS.MOTIVO_RECUSA` (obrigatório ao recusar) |
| `V042` | `TREINAMENTOS.ENCERRADO_EM` |
| `V043` | `AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM` do treinamento passam a nascer `FALSE` |
| `V044` | `TREINAMENTOS_MODULOS.NOME_ARQUIVO_ORIGINAL` |
| `V045` | `FORMULARIOS.LEMBRETE_AUTOMATICO` |
| `V046` | arquivo na resposta do formulário (`CAMINHO_ARQUIVO` e `NOME_ARQUIVO_ORIGINAL`; anônimo nunca tem arquivo) |
| `V047` | 17 índices para os filtros do contrato |

**Armadilhas novas (para o mapeamento do EF e para a aplicação):**
- Tabelas só da aplicação (`V033` a `V038`) guardam `DATETIME` em **UTC**, diferente das tabelas do grupo (`-03:00`); cada script diz o fuso. O padrão `DEFAULT (UTC_TIMESTAMP(6))` deve ser mapeado como "gerado na inclusão", senão o EF manda `0001-01-01`.
- **Colunas calculadas** (mapear como calculadas, fora do `INSERT`): `USUARIOS_TOKENS_SENHA.EM_ABERTO`, `DESLIGAMENTOS_AGENDADOS.VIGENTE` e `CHAVE_PENDENTE` (`V040`).
- **Chave de idempotência** (`FILA_EMAILS` e `TAREFAS_AGENDADAS`): ASCII, começa por "TIPO:", única; o erro 1062 significa "já existe" e se trata como sucesso.
- **Matrícula:** `UPDATE SEQUENCIAS ... LAST_INSERT_ID(ULTIMO_VALOR + 1)` na **mesma transação e conexão** do cadastro: sem buraco; cadastros simultâneos esperam.
- **Fila de e-mails:** a `CARGA` (que pode ter o token do link) fica nula quando a mensagem termina. O lembrete de formulário anônimo (sigiloso) não tem `ID_FUNCIONARIO` e nunca chega a `ENVIADO`; a aplicação apaga a linha ao enviar, e o limite diário fica em `TAREFAS_AGENDADAS`.
- **Auditoria:** sem trigger (só inclusão é regra da aplicação). `VALOR_OCULTO` verdadeiro exige antes e depois nulos.
- `IMPORTACOES.PREVIA` tem dado pessoal: o banco recusa `CONFIRMADA` com `PREVIA`. A limpeza das vencidas é tarefa agendada.
- **Rascunho de aviso:** o trigger põe `NOW()` em `DATA_PUBLICACAO`; para "sem data prevista", `UPDATE ... SET DATA_PUBLICACAO = NULL` logo depois do `INSERT`.
- Sem `CHECK` em `TIPO_CONTRATO`, `TIPO_DOCUMENTO` e `TIPO_ADVERTENCIA` (listas provisórias no contrato).
- `LEMBRETE_AUTOMATICO`, `AVISA_POR_EMAIL` e `LEMBRA_ANTES_DO_FIM` têm padrão `FALSE` no banco **de propósito**: a API grava `TRUE` onde o contrato pede ligado (padrão do banco que o EF omite é armadilha).
- **Pergunta de arquivo:** grave o **nome** do arquivo também em `RESPOSTA` (o trigger recusa obrigatória vazia), e `CAMINHO_ARQUIVO` e `NOME_ARQUIVO_ORIGINAL` juntos.
- **Permutar `ORDEM`** (perguntas, alternativas, módulos): `ORDEM + 1000` e depois o valor final (o `CHECK` recusa negativo). Para subir posições depois de apagar, `UPDATE` com `ORDER BY ORDEM`.
- **Banco de horas:** converta **cada linha** para minutos e depois some. Somar horas e converter no fim erra até alguns minutos (medido), e `FN_CALCULAR_SALDO_BANCO_HORAS` soma horas.
- O trigger de treinamento cria o certificado com **UUID versão 1** e também quando `EMITE_CERTIFICADO` é `FALSE`: a aplicação troca o código por v4 e apaga essas linhas ao excluir.
- **Excluir formulário e treinamento:** apague os filhos na ordem do contrato (as chaves estrangeiras não têm `ON DELETE`).
- O DDL do MySQL não desfaz: se o `V001` falhar no meio, o banco fica parcial. O executor avisa, e em desenvolvimento se recria o banco (`docker compose down -v`). (Os scripts antigos `V039` a `V041` e `V030` eram escritos para não deixar nada pela metade; isso não vale mais para o `V001`, que é um arquivo só.)

**O que o Kauan deve conferir** (os números abaixo são dos scripts antigos; hoje as mudanças estão no `V001`, marcadas por comentário com o título do pedido): `V030` (contador no lugar de `AUTO_INCREMENT`), `V031` e `V032` (colunas em tabelas dele), `V040` (chave única sobre coluna calculada em `PONTOS_MANUTENCOES`), `V041` (o terceiro `CHECK` deixa passar recusa antiga sem motivo), `V043` (mexe no padrão do `V029`), `V046` (altera `FORMULARIOS_RESPOSTAS`, do `V017`) e `V047` (só índices). **Decisões do dono:** triggers de imutabilidade na auditoria; as listas `CLT`/`ESTAGIO`, tipos de documento e `VERBAL`/`ESCRITA`; a coluna com o nome de quem do RH aplicou a advertência; o saldo do banco de horas em horas com 2 casas contra minutos no contrato; o trigger de certificado com UUID v1.

## Itens 1 e 2: feitos direto no SEU arquivo de triggers (para você validar)

(Os três arquivos do grupo foram movidos para esta pasta como `V001`, `V002` e `V003`; o arquivo de triggers de que esta seção fala é hoje o `V002`.)

Os itens 1 e 2 são os triggers `TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE`, que você já estava corrigindo. **Eles bloqueavam tudo:** o script de triggers do grupo **não aplicava**. Falhava na linha 374 com `Unknown column 'HORA_ENTRADA' in 'NEW'`, porque os triggers ainda usavam `HORA_ENTRADA` e `HORA_SAIDA`, colunas que saíram de `PONTOS` no commit `e3eb1be`. Medi no MySQL 8.4.13: o arquivo original criava **13 dos 36** triggers e parava. Por isso não dava para escrevê-los como script novo (o V002 precisa aplicar antes).

**O dono autorizou em 2026-10-29 corrigir direto no seu arquivo**, `Banco de Dados/RHSystem_Criacao_Triggers_MySQL.sql`, no mesmo padrão: um bloco de comentário no cabeçalho da seção "PONTOS" diz **por que** mudou, **o que o código antigo fazia**, **o que acontece agora**, **o que fica para você decidir** e **como conferir**. Para ver só o que mudou: `git diff -- "Banco de Dados/RHSystem_Criacao_Triggers_MySQL.sql"`. As mudanças, iguais nos dois triggers:

1. removido o bloco `IF NEW.HORA_ENTRADA IS NOT NULL AND NEW.HORA_SAIDA IS NOT NULL ... END IF;` (as funções já calculam as horas pelas marcações);
2. a carga do cargo é copiada **dividida por 5**: `SET NEW.CARGA_HORARIA = ROUND(v_carga / 5, 2);`, porque `CARGOS.CARGA_HORARIA` passou a ser semanal.

**O que eu gostaria que você decidisse** (está também no comentário do arquivo):

- O **5** supõe cinco dias de trabalho por semana. Cargo de seis dias precisa de outra regra.
- `PONTOS.HORAS_TRABALHADAS` **deixa de ser preenchida** pelo trigger (e o `UPDATE` não a zera mais). Nenhum script SQL desta pasta lê essa coluna. Se quiser a coluna sempre preenchida, o caminho seria um trigger nas marcações, que não fiz.
- A regra "saída depois da entrada" **sai dos triggers**: quem confere agora são `FN_VERIFICAR_CONSISTENCIA_PONTO` (marca o dia como inconsistente) e `FN_CALCULAR_HORAS_PONTO` (devolve `NULL` nesse caso). Testei também uma jornada de 22:00 a 06:00 do dia seguinte: dá 8,00 horas.
- Pontos **já gravados** com a carga semanal não são corrigidos pelo trigger; precisariam de um `UPDATE` à parte.
- **Não mudei, mas reparei:** `TRG_FUNCIONARIOS_BEFORE_INSERT/UPDATE` põem `ATIVO = FALSE` sempre que `DATA_DESLIGAMENTO` tem valor, mesmo no futuro (`:64-66` e `:94-96`). Por isso a mensagem "A data do ponto e posterior ao desligamento" do trigger de ponto não é alcançável: a de funcionário inativo vem antes.

Se você já tiver a sua própria correção, compare: o resultado esperado é o mesmo (sem `HORA_ENTRADA`/`HORA_SAIDA`, e a carga dividida por 5). **Atenção:** em 2026-10-28 o dono tinha pedido para desfazer uma correção minha, justamente para não conflitar com a sua. Se você subir a sua pela web antes de isto ser enviado ao GitHub, vai haver conflito neste arquivo.

## Numeração

A pasta tem `V001` a `V005`; o **próximo script é o `V006`**. **Quem numera é o dono**: o grupo envia o conteúdo e o dono dá o número, para não haver dois com o mesmo. O nome do arquivo precisa seguir `V{nnn}__{nome}.sql`: o executor recusa outro padrão.

Regra do projeto: **depois do primeiro deploy no Azure, script aplicado não se edita mais**. Mudança vira script novo (`ALTER` para tabela; `DROP` e `CREATE` para trigger e função). **Antes** do primeiro deploy foi possível ter um esquema "limpo", com as mudanças dentro dos `CREATE TABLE`: o dono decidiu fazer isso em 2026-11-03, e é a junção descrita no topo. Quem tiver um banco local criado antes precisa recriá-lo (`docker compose down -v`), porque o executor recusa um banco cujo `V001` ou `V002` aplicado mudou.

**Prazo da sua conferência:** o primeiro deploy (fim da fatia 0) **congela tudo o que estiver nesta pasta**. O dono disse em 2026-10-29 que "vai demorar para termos um verdadeiro deploy", então **até lá o `V001` e o `V002` podem ser corrigidos no lugar**, sem criar script novo; não há pressa. Se a sua conferência vier depois do deploy, qualquer correção vira script novo. Não esqueça que o DDL do MySQL não desfaz: um script com várias instruções que falhe no meio deixa o banco parcial (o executor avisa).

## Como foi testado (os scripts antigos, 2026-10-29)

Registro do que foi medido **antes** da junção, com os scripts antigos; a conferência da base nova está na próxima seção. Num MySQL **8.4.13** descartável (Docker), em ordem: `V001`, `V002` (o **arquivo de triggers do grupo, já com os itens 1 e 2 corrigidos**; as primeiras rodadas de teste usaram uma cópia, e foram repetidas com o arquivo real), `V003` e depois `V006` a `V029`. Os 36 triggers do arquivo são criados, e os 204 testes de comportamento, os 25 com dados antigos e 30 testes dos dois triggers de ponto (carga ÷ 5, `HORAS_TRABALHADAS`, datas, funcionário inativo, funções) passaram com ele.

- Os 24 scripts aplicam sem erro num banco **vazio** e também sobre um banco com **linhas antigas** (funcionário, ponto, holerite, aviso, treinamento, formulário com resposta, benefício, férias etc.), que é onde um `ALTER` com `CHECK` costuma falhar.
- 204 testes de comportamento: cada regra que os comentários prometem foi exercitada, nos dois sentidos (aceita o que deve e recusa o que deve), inclusive com os **triggers do grupo ligados**. Por exemplo: resposta de candidato e resposta anônima passam pelo trigger de resposta; o banco recusa gravar nome numa resposta de formulário anônimo e recusa trocar o `ANONIMO` depois da primeira resposta; inativar um funcionário ainda cancela as férias e desliga os benefícios com os `CHECK` novos; o trigger de holerite continua recusando competência futura que não seja de férias. Nos testes de recusa conferi também **o motivo** do erro (um teste de "recusa" passa com qualquer erro).
- Uma **revisão independente** (agente `revisor-de-plano`, 2026-10-29) leu os 24 scripts. O que ela apontou e eu conferi foi corrigido e testado de novo: `;` dentro de comentário (o executor pode se confundir), data de volta da chave de aprovação, `ID_GESTOR` obrigatório na etapa do gestor, estado "encerrado" do formulário, anonimato garantido pelo banco, participação anônima sem data e com UUID aleatório, padrões `FALSE` em avisos e treinamentos, troca do `CHECK` de advertência numa instrução só, `STATUS_PEDIDO` sem padrão, código do link com `ascii_bin`.
- Os testes não estão no repositório (são scripts de shell que usam um container). Se quiser, eles podem entrar.

(Com os scripts antigos, repetir à mão era aplicar `V001`, `V002`, `V003` e depois `V006` a `V029`, na ordem, num MySQL 8.4 descartável.)

## Como a base nova foi conferida (2026-11-03)

Os 47 scripts antigos (os da pasta `Banco de Dados/Scripts` no commit `f69db4e`) foram aplicados num banco `cmp_ref` e a base nova (`V001` a `V005`) em outro, `cmp_novo`, num MySQL 8.4 com `--default-time-zone=-03:00` e `--log-bin-trust-function-creators=1`. Depois se comparou o **retrato** de cada um: colunas na ordem (tipo, padrão, coluna gerada, charset, comentário), índices, restrições (`CHECK`, chaves únicas, chaves estrangeiras com as colunas e as regras), triggers, funções e parâmetros, opções das tabelas e os dados. Resultado: **48 tabelas, 426 colunas, 36 triggers e as 8 funções, iguais.** As únicas diferenças:

- o **comentário** dentro do trigger de holerite (o texto do antigo `V012` contava "antes era..."; hoje são duas linhas). Sem os comentários, o corpo do trigger é idêntico, e os outros 35 triggers são idênticos no texto exato;
- a **ordem** de três linhas `KEY` no `SHOW CREATE TABLE` (`IDX_FERIAS_FUNCIONARIO_INICIO`, as duas chaves de FK de `FORMULARIOS_RESPOSTAS` e `IDX_PONTOS_FUNCIONARIO_DATA`): o conjunto de índices é o mesmo.

"Igual" vale no sentido lógico: no banco antigo as colunas acrescentadas por `ALTER` entram como `INSTANT` (versões de linha do InnoDB), e no novo nascem no `CREATE`; isso não muda nenhum resultado. Um achado que vale guardar: os `CREATE INDEX` dos antigos `V035` a `V038` **ficaram soltos, logo depois da tabela**, porque dentro do `CREATE TABLE` o MySQL guarda a constante de três `CHECK` (`FILA_EMAILS` e `TAREFAS_AGENDADAS`) como `_utf8mb4':'` e não como `_ascii':'`. É só a forma do texto guardado, mas assim a comparação ficou exata.

A comparação foi validada com defeitos de propósito (tipo de coluna, ordem de colunas, `CHECK` apagado, índice a mais, ordem das colunas dentro de um índice, coluna de origem de uma FK, valor de `SEQUENCIAS`, `DEFAULT`, comentário de tabela, função e trigger apagados): cada um foi acusado no arquivo certo. E duas aplicações independentes dos 47 scripts antigos deram retratos iguais.

## O que eu gostaria que você questionasse (decisões minhas)

(Os números abaixo são dos scripts antigos.) Estas não vieram da lista; foram escolhas de desenho, todas explicadas no comentário do script:

- **V011** não cria o `CHECK` opcional "status `APROVADA` exige a etapa do gestor", porque travaria o pedido de quem é gestor (que só o RH decide).
- **V021** coloca o estado do pedido dentro de `BENEFICIOS_FUNCIONARIOS` (e **não** numa tabela de pedidos), para manter os dependentes ligados à adesão, como o dono decidiu. O custo: refazer um pedido recusado é um `UPDATE` da mesma linha. Dependentes (`V024`) e trajeto (`V025`) têm as mesmas colunas de situação, porque o Figma lista os três em "Seus pedidos".
- **V015** acrescenta `SENTIDO_AVALIACAO` (o Figma tem duas avaliações: "da equipe" e "do gestor"). Não estava na lista.
- **V017** guarda candidato, resposta anônima e resposta comum **na mesma tabela** (`FORMULARIOS_RESPOSTAS`), porque os triggers de pergunta só contam respostas ali.
- **V018 e V029** usam uma **coluna gerada** (`CHAVE_DESTINO`) só para a chave única funcionar com `NULL`; **V019** usa outra (`CHAVE_AVALIADO`) na chave primária.
- **V019** não tem `ID` automático nem hora, de propósito: assim não dá para cruzar a ordem de quem participou com a ordem das respostas.
- **V012** era o único que alterava um trigger do grupo (a exceção de férias no trigger de holerite). Hoje essa exceção está no próprio `V002`. Se o grupo trocar o `V002` por uma versão sem ela, a exceção some em silêncio: o teste `Trigger_de_holerite_aceita_competencia_futura_so_para_ferias` (em `ScriptsDoGrupoTests`) é quem avisa.
- Os `CHECK` usam `COALESCE` em alguns pontos de propósito: no MySQL um `CHECK` que dá `NULL` **passa**.
- **V015/V017:** o anonimato é garantido pelo banco com uma chave estrangeira composta `(ID_FORMULARIO, ANONIMO)` entre a resposta e o formulário. É uma cópia de uma coluna de propósito: com ela o MySQL recusa trocar o `ANONIMO` do formulário depois da primeira resposta, sem trigger. Se preferir trigger, é só trocar.
- **V015:** `DATA_ENCERRAMENTO` existe para "agendado com a data vencida" não ser igual a "encerrado".
- **V019:** a chave é um `UUID` versão 4 que **a aplicação gera**; o `CHECK` recusa o `UUID()` do MySQL (versão 1, quase sequencial).
- **V021:** `STATUS_PEDIDO` **não tem padrão** depois do script; toda gravação precisa informá-lo.
- **V029:** guarda as três caixas da publicação (`INCLUI_ADMITIDOS`, `AVISA_POR_EMAIL`, `LEMBRA_ANTES_DO_FIM`) e copia os treinamentos antigos para o público `PESSOAS`.
- **Não criei** `CHECK` para o RH não confirmar o próprio pedido (férias, ajuste, atestado, adesão): com um RH só, travaria os pedidos dele. O dono confirmou em 2026-10-29 ("pode"): o RH pode confirmar o próprio pedido.
- **Anonimato:** o dono aceitou em 2026-10-29 ("basta") que ele valha só contra quem usa a aplicação, não contra quem lê o banco ou o backup. Não é preciso mudar nada nos scripts por isso.

## O que o Figma mostra e eu **não** criei

Na etapa de publicação do formulário (tela 20) há "Nota mínima para seguir", "Enviar o resultado por e-mail ao RH" e "Convidar aprovados para a entrevista". A nota mínima foi cortada pelo dono (sem nota, sem eliminatória). Os outros dois dependem de uma decisão ainda aberta (o convite automático dos aprovados, sem nota). Por isso nenhuma coluna foi criada para eles.

## Para quem mapeia no EF Core (não testado, inferido)

- **Não declare "padrão do banco"** nas colunas em que a aplicação grava `false` e o banco tem padrão `TRUE` (`FORMULARIOS.ATIVO`, `TREINAMENTOS.EMITE_CERTIFICADO` e as três caixas): o EF pode omitir o valor `false` e o banco gravar `TRUE` sem erro.
- As colunas `CHAVE_DESTINO` (V018, V029) e `CHAVE_AVALIADO` (V019) são **calculadas**: mapeie como calculadas (fora do `INSERT`).
- As tabelas novas usam `DEFAULT CURRENT_TIMESTAMP` no lugar de trigger: mapeie como geradas na inclusão.
- Sonda sugerida: um teste de integração que grave pelo EF um formulário com `Ativo = false`, um treinamento com `EmiteCertificado = false` e uma participação, e releia.

## Padrões usados

Nomes em maiúsculas; `FK_`, `UQ_` e `CHK_` com o nome da tabela; textos fechados com `VARCHAR` e `CHECK` (como o grupo faz); tabelas novas **sem trigger** (decisão do dono) e, por isso, datas automáticas com `DEFAULT CURRENT_TIMESTAMP`. Os comentários não têm acento, como nos seus scripts.
