# Scripts novos do banco (V006 a V029)

Estes 24 scripts fazem o que a lista `docs/pedidos-para-o-kauan.md` pedia ao Kauan (itens 3 a 27). Foram escritos pelo lado da aplicação, a pedido do dono (2026-10-29), **para o Kauan conferir depois**. Cada script começa com um comentário que explica **por que** existe, **o que faz**, quais decisões foram tomadas e o que **fica com a aplicação**. Leia o comentário antes do SQL.

Nada do que o grupo escreveu foi alterado: os três arquivos em `Banco de Dados/` continuam como estavam.

## Quais itens estão aqui

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

## Itens 1 e 2: feitos direto no SEU arquivo de triggers (para você validar)

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

Os números começam em `V006` porque o plano da fatia 0 reserva `V001` a `V003` para os três arquivos do grupo (que ainda serão movidos para esta pasta) e `V004` e `V005` para `REFRESH_TOKENS` e os níveis de acesso iniciais. **Quem numera é o dono**; se a ordem mudar, é só renomear (os scripts não citam o número uns dos outros, exceto nos comentários). O nome do arquivo precisa seguir `V{nnn}__{nome}.sql`: o executor recusa outro padrão.

Regra do projeto: **depois do primeiro deploy no Azure, script aplicado não se edita mais**. Mudança vira script novo (`ALTER`; `DROP` e `CREATE` para trigger). **Antes** do primeiro deploy, o grupo pode, se preferir um esquema "limpo", incorporar estas mudanças direto nos `CREATE TABLE` do V001. É decisão sua e do grupo; por isso os scripts são pequenos e independentes.

**Prazo da sua conferência:** o primeiro deploy (fim da fatia 0) **congela tudo o que estiver nesta pasta**, inclusive estes 24 scripts. O dono disse em 2026-10-29 que "vai demorar para termos um verdadeiro deploy", então **até lá estes scripts podem ser corrigidos no lugar**, sem criar script novo; não há pressa. Se a sua conferência vier depois do deploy, qualquer correção vira script novo. Se o grupo incorporar uma mudança no V001, o script correspondente (V0nn) precisa sair **na mesma mudança**, senão ele falha por coluna repetida. E o DDL do MySQL não desfaz: um script com várias instruções que falhe no meio deixa o banco parcial (o executor avisa); por isso, onde possível, a troca de `CHECK` foi feita numa instrução só.

## Como foi testado

Num MySQL **8.4.13** descartável (Docker), em ordem: `V001`, `V002` (o **arquivo de triggers do grupo, já com os itens 1 e 2 corrigidos**; as primeiras rodadas de teste usaram uma cópia, e foram repetidas com o arquivo real), `V003` e depois `V006` a `V029`. Os 36 triggers do arquivo são criados, e os 204 testes de comportamento, os 25 com dados antigos e 30 testes dos dois triggers de ponto (carga ÷ 5, `HORAS_TRABALHADAS`, datas, funcionário inativo, funções) passaram com ele.

- Os 24 scripts aplicam sem erro num banco **vazio** e também sobre um banco com **linhas antigas** (funcionário, ponto, holerite, aviso, treinamento, formulário com resposta, benefício, férias etc.), que é onde um `ALTER` com `CHECK` costuma falhar.
- 204 testes de comportamento: cada regra que os comentários prometem foi exercitada, nos dois sentidos (aceita o que deve e recusa o que deve), inclusive com os **triggers do grupo ligados**. Por exemplo: resposta de candidato e resposta anônima passam pelo trigger de resposta; o banco recusa gravar nome numa resposta de formulário anônimo e recusa trocar o `ANONIMO` depois da primeira resposta; inativar um funcionário ainda cancela as férias e desliga os benefícios com os `CHECK` novos; o trigger de holerite continua recusando competência futura que não seja de férias. Nos testes de recusa conferi também **o motivo** do erro (um teste de "recusa" passa com qualquer erro).
- Uma **revisão independente** (agente `revisor-de-plano`, 2026-10-29) leu os 24 scripts. O que ela apontou e eu conferi foi corrigido e testado de novo: `;` dentro de comentário (o executor pode se confundir), data de volta da chave de aprovação, `ID_GESTOR` obrigatório na etapa do gestor, estado "encerrado" do formulário, anonimato garantido pelo banco, participação anônima sem data e com UUID aleatório, padrões `FALSE` em avisos e treinamentos, troca do `CHECK` de advertência numa instrução só, `STATUS_PEDIDO` sem padrão, código do link com `ascii_bin`.
- Os testes não estão no repositório (são scripts de shell que usam um container). Se quiser, eles podem entrar.

Para repetir à mão:

```
docker run -d --name rh-mysql-teste -e MYSQL_ALLOW_EMPTY_PASSWORD=yes -e MYSQL_DATABASE=rh_system mysql:8.4
# depois, na ordem: V001 (tabelas), V002 (triggers, já corrigido), V003 (funções), V006 ... V029
docker exec -i rh-mysql-teste mysql -uroot --default-character-set=utf8mb4 < "Banco de Dados/RHSystem_Criacao_Tabelas_MySQL.sql"
docker exec -i rh-mysql-teste mysql -uroot --default-character-set=utf8mb4 rh_system < "Banco de Dados/Scripts/V006__funcionarios_matricula_contrato.sql"
```

## O que eu gostaria que você questionasse (decisões minhas)

Estas não vieram da lista; foram escolhas de desenho, todas explicadas no comentário do script:

- **V011** não cria o `CHECK` opcional "status `APROVADA` exige a etapa do gestor", porque travaria o pedido de quem é gestor (que só o RH decide).
- **V021** coloca o estado do pedido dentro de `BENEFICIOS_FUNCIONARIOS` (e **não** numa tabela de pedidos), para manter os dependentes ligados à adesão, como o dono decidiu. O custo: refazer um pedido recusado é um `UPDATE` da mesma linha. Dependentes (`V024`) e trajeto (`V025`) têm as mesmas colunas de situação, porque o Figma lista os três em "Seus pedidos".
- **V015** acrescenta `SENTIDO_AVALIACAO` (o Figma tem duas avaliações: "da equipe" e "do gestor"). Não estava na lista.
- **V017** guarda candidato, resposta anônima e resposta comum **na mesma tabela** (`FORMULARIOS_RESPOSTAS`), porque os triggers de pergunta só contam respostas ali.
- **V018 e V029** usam uma **coluna gerada** (`CHAVE_DESTINO`) só para a chave única funcionar com `NULL`; **V019** usa outra (`CHAVE_AVALIADO`) na chave primária.
- **V019** não tem `ID` automático nem hora, de propósito: assim não dá para cruzar a ordem de quem participou com a ordem das respostas.
- **V012** é o único que altera um trigger do grupo. Se alguém rodar de novo o script original de triggers, ele desfaz a mudança; a ordem dos scripts evita isso.
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
