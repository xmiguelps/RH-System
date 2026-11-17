# As procedures do banco (script `V007`)

Registro da reescrita das procedures que o grupo do banco entregou (decisão do dono, 2026-11-17). O arquivo antigo tinha 24 procedures escritas sem conhecer as regras que a API realmente aplica; o dono mandou reescrevê-las **alinhadas ao contrato e aos scripts `V001` a `V006`, testadas num MySQL 8.4**. O resultado é o `Banco de Dados/Scripts/V007__procedures.sql` (28 procedures, uma seção por domínio) e os testes de `back-end/tests/RHSystem.Api.IntegrationTests/Procedures/` (uma classe por domínio, 261 testes).

**A API não chama procedures.** Elas existem como a interface transacional do banco (para o grupo do banco, para a banca, para quem roda SQL à mão). Por isso cada uma faz **as mesmas regras e os mesmos efeitos no banco** que o caso de uso equivalente da API, na mesma ordem de recusa, e nada que a contradiga. O que a API faz fora do banco não se replica (veja "O que a API faz e a procedure não").

## Como chamar sem a API

- Cada procedure abre a **própria transação** e desfaz tudo ao recusar. **Chame fora de uma transação**: o `START TRANSACTION` confirma a que estiver aberta e uma recusa (`ROLLBACK`) desfaz o que a transação de quem chama tivesse pendente.
- "Hoje" é o `CURDATE()`/`NOW()` da **sessão** do banco, que o projeto exige em `-03:00` (como os triggers do `V002` e o `docker-compose.yml`; no Azure é o parâmetro `time_zone` do servidor). Só o ponto calcula o próprio horário de Brasília (UTC − 3 h) e não depende da sessão. A API usa um relógio próprio. Não há como bater ponto no passado.
- Nenhuma confere o **perfil** de quem chama (só que a pessoa existe e está ativa), nenhuma grava evento de auditoria, manda e-mail, guarda arquivo ou cria tarefa agendada.
- Recusa de negócio é `SQLSTATE 45000`, com mensagem sem acento (como as dos triggers do `V002`). Parâmetro maior que o tamanho declarado vem como erro do próprio MySQL.
- O banco de horas só nasce da consolidação diária da API. Sem ela no ar, `SP_EFETIVAR_DESLIGAMENTO` sempre recusa ("banco de horas ainda não consolidado"; usar o desligamento só pelo banco exige inserir à mão a tarefa `CONSOLIDAR_BANCO_HORAS` concluída), e a decisão dos pedidos de ajuste de ponto não tem procedure.

## As 28 procedures

Das 24 do arquivo antigo, **22 ficaram (todas corrigidas) e 2 foram removidas**. Seis são novas, porque a API tem a operação e o arquivo antigo não.

| Procedure | Espelha na API | O que mudou em relação ao arquivo antigo |
|---|---|---|
| `SP_REGISTRAR_MARCACAO_PONTO` | `registrarMarcacaoDePonto` | Sem o horário de quem chama (a API bate com a hora do servidor); janela de 12 horas (a saída de uma entrada de ontem com menos de 12 h cai no dia de ontem); tipo esperado conferido; recusa batida no mesmo segundo; admissão |
| `SP_SOLICITAR_AJUSTE_PONTO` | `criarPedidoDeAjuste` | Recebe o dia (a API cria o registro do dia), não o id; etapa inicial do pedido com o gestor do setor (gestor ausente, sem gestor, aprovação automática) e as 10 regras da API |
| ~~`SP_LANCAR_BANCO_HORAS`~~ | (a API não permite lançamento manual) | **Removida**: o banco de horas só nasce da consolidação diária; um lançamento à mão seria apagado ou reescrito no próximo recálculo |
| `SP_CONFERIR_FERIAS` (nova) | `conferirPedidoDeFerias`, `conferirRegistroDeFerias` | Conferência das regras de férias sem gravar; as outras quatro a chamam dentro da transação |
| `SP_SOLICITAR_FERIAS` | `pedirFerias` | O período aquisitivo é escolhido pela regra, não por quem pede; saldo, partes de 14 dias, no máximo 3 partes, antecedência de 30 dias, feriado e descanso; etapa do gestor (ausente, aprovação automática, sem gestor); mensagem da pessoa |
| `SP_REGISTRAR_FERIAS_RH` (nova) | `registrarFeriasPeloRh` | O RH registra direto: nasce aprovada, até no passado, sem antecedência |
| `SP_DECIDIR_FERIAS_GESTOR` | decisão do gestor (aprovar, recusar, sugerir) | Gestor conferido pelo setor e pelo `ID_GESTOR` do pedido; só decide o que está com o gestor; o motivo não apaga mais a mensagem da pessoa; sugestão com todas as regras |
| `SP_RESPONDER_SUGESTAO_FERIAS` (nova) | resposta à sugestão de datas | Aceitar reconfere e manda ao RH; recusar cancela; passado o prazo o pedido expira |
| `SP_CONFIRMAR_FERIAS_RH` | confirmação do RH | Confirma todo pedido que chegou ao RH (gestor aprovou, aprovação automática, sugestão aceita, sem etapa do gestor), não só o aprovado pelo gestor |
| `SP_RECUSAR_FERIAS_RH` (nova) | `recusarFeriasComoRh` | Recusa pelo RH, com motivo de 5 a 500 caracteres |
| `SP_PUBLICAR_HOLERITE` | `enviarHolerite` | Só a versão 1: competência que já existe é recusada (409 na API), não vira versão nova em silêncio; agenda por data; regras de competência na ordem da API |
| `SP_SUBSTITUIR_HOLERITE` (nova) | `substituirHolerite` | Nova versão no lugar do holerite errado, só a partir da versão atual |
| `SP_ENVIAR_ATESTADO` | `enviarAtestado` | Uma recusa por regra, na ordem da API (dias × horas, fim depois do início, só um dia nas horas) |
| `SP_DECIDIR_ATESTADO` | `validarAtestado`, `recusarAtestado` | Motivo aparado e limitado a 500; só decide o enviado; trava a pessoa e depois o atestado |
| `SP_SOLICITAR_BENEFICIO` | `pedirAdesaoABeneficio` | Quem foi recusado pode pedir de novo (a linha é reaberta); só benefício opcional; dependentes (JSON) e trajeto do vale-transporte; data de início e autorização obrigatórias |
| `SP_DECIDIR_BENEFICIO` | `aprovarAdesaoABeneficio`, `recusarAdesaoABeneficio` | Aprovar aprova junto os dependentes e o trajeto pendentes; recusar os cancela; pessoa inativa é recusada |
| `SP_GERAR_ADVERTENCIA` | `registrarAdvertencia`, `registrarAdvertenciaDaEquipe` | Data do ocorrido (não futura, não anterior à admissão), tipo, tamanhos; os três jeitos de registrar (só RH, RH em nome do gestor, só gestor da equipe); grava quem do RH registrou (`V006`) |
| `SP_REGISTRAR_CIENCIA_ADVERTENCIA` | ciência da pessoa | Só a partir de `PENDENTE`, e leva a `APLICADA` |
| `SP_CONTESTAR_ADVERTENCIA` | contestação da pessoa | Só a partir de `PENDENTE`; texto de 10 a 2000 caracteres; a primeira contestação nunca é sobrescrita |
| `SP_RESPONDER_CONTESTACAO_ADVERTENCIA` (nova) | `responderContestacaoDeAdvertencia` | `MANTER` volta a `APLICADA`, `ACATAR` cancela |
| `SP_CONCLUIR_MODULO_TREINAMENTO` | conclusão de módulo | A antiga só gravava o progresso (nenhum trigger conclui o treinamento): agora o último módulo conclui, o trigger cria o certificado e o código vira UUID v4 como na API; inscrição, data de início e encerrado conferidos |
| `SP_ENCERRAR_FORMULARIO` | encerrar formulário | Só o formulário publicado; limpa a reabertura marcada (o recorrente não reabre sozinho) |
| `SP_AGENDAR_DESLIGAMENTO` | agendar desligamento | Limite de 10 anos; efetiva na hora quando o dia seguinte já começou e o banco de horas do último dia está consolidado |
| `SP_CANCELAR_DESLIGAMENTO` | cancelar desligamento | Só o agendado; o id passou de `BIGINT` para `INT` (o tipo da coluna) |
| `SP_EFETIVAR_DESLIGAMENTO` | efetivar desligamento | Só a partir do **dia seguinte** ao último dia e **depois** de o banco de horas desse dia estar consolidado (a antiga inativava no próprio último dia); passa ao RH os pedidos parados no gestor; cancela os pedidos de benefício pendentes; já efetivado termina sem erro |
| `SP_CRIAR_USUARIO` | `enviarConviteDeAcesso` (o login) | Sem o hash de senha: cria o **login provisório** (a API não grava senha de outra pessoa); devolve o que já existe |
| `SP_CADASTRAR_FUNCIONARIO` | `criarFuncionario` | A matrícula é gerada pela tabela `SEQUENCIAS` (não vem por parâmetro); o gestor vem do setor; CPF com dígito verificador, e-mail, telefone, contrato, cargo, setor e nível conferidos; devolve a matrícula |
| ~~`SP_CADASTRAR_NIVEL_ACESSO`~~ | (a API não cria níveis) | **Removida**: o sistema só conhece os três níveis do `V005` |
| `SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO` | troca de `perfil` em `editarFuncionario` | Só os três níveis; mesmo nível não é erro; quem gerencia um setor continua `Funcionario Gestor` |
| `SP_CADASTRAR_CARGO` | `criarCargo` | Nome único (sem maiúsculas nem acento), carga semanal obrigatória de até 168 h com até duas casas |

## Decisões que tomei (Rulings)

Cada decisão onde o contrato, o código e o esquema não decidiam, com o custo se estiver errada. Os cinco autores trabalharam em paralelo; as decisões estão agrupadas por domínio.

**Ponto**
- `SP_LANCAR_BANCO_HORAS` removida, sem substituta em SQL: a API conta em minutos inteiros descartando os segundos de cada batida e depende de feriado por UF e município, férias, atestado e pedido em análise; as funções do `V003` arredondam horas com duas casas. Custo se errado: o grupo fica sem procedure de banco de horas até alguém escrever a consolidação em SQL e mantê-la igual à da API.
- Sem procedures de decisão do pedido de ajuste (gestor e RH): toda decisão recalcula o banco de horas do dia na mesma transação, e a aprovação do RH ainda aplica as batidas. Custo se errado: o fluxo em SQL puro para na criação do pedido.
- `P_DATA_HORA` saiu da marcação (a API não permite escolher a hora) e `P_ID_PONTO` virou `P_DATA` no ajuste. Custo se errado: quem carrega batidas antigas pelo banco usa `INSERT` direto, sem as regras; quem já chamava com o id troca a chamada.
- O "agora" é `UTC_TIMESTAMP() - INTERVAL 3 HOUR`, o deslocamento fixo da API, não `NOW()`; dá o mesmo com o servidor em `-03:00`. Custo se errado: nenhum na configuração padrão.
- Tipo comparado pela collation e gravado canônico (`entrada` vira `ENTRADA`): mais tolerante que a API, que recusa com 400.

**Férias**
- Saíram os dois parâmetros do período aquisitivo de `SP_SOLICITAR_FERIAS`: a API calcula. Custo se errado: quem chamava com 9 argumentos ajusta a chamada.
- Acrescentadas `SP_CONFERIR_FERIAS` (evita quatro cópias da conferência), `SP_RESPONDER_SUGESTAO_FERIAS` (sem ela o pedido com sugestão não sai do lugar), `SP_REGISTRAR_FERIAS_RH` e `SP_RECUSAR_FERIAS_RH` (a API tem as operações). Custo se errado: procedures a mais para remover.
- Resposta à sugestão fora do prazo grava a expiração (`COMMIT`) e depois recusa com `SIGNAL`, como a API (expira e devolve 409). Custo se errado: quem espera "recusa = nada gravado" se surpreende (está no comentário e testado).
- Motivo sem recusa, ou datas sem sugestão, são recusados em vez de ignorados (ignorar perderia dado em silêncio). Resultado e resposta valem em qualquer caixa e são gravados em maiúsculas.
- Pessoa inativa, "própria pessoa" e "fora de `GESTORES`" são conferidos na procedure, antes do trigger, para a ordem ser a da API. Custo se errado: regra duplicada com o `V002`.
- `SP_REGISTRAR_FERIAS_RH` não tem parâmetro de quem do RH registrou: a API só grava isso na auditoria. Custo se errado: o registro pelo banco fica sem autor.

**Holerite, atestado e benefícios**
- `SP_PUBLICAR_HOLERITE` dividida em duas (a API tem `enviarHolerite`, com 409 se já existe, e `substituirHolerite`); "no dia do pagamento" não entra, quem chama passa a data em `P_PUBLICAR_EM` (a data depende do calendário e das exceções, calculados em código). Custo se errado: quem agenda pelo banco calcula a data por fora.
- A procedure repete as três regras de competência do trigger `TRG_HOLERITES_BEFORE_INSERT`, só para a ordem das recusas ser a da API. Custo se errado: texto duplicado; se o trigger mudar, a procedure muda junto.
- Tipo e decisão comparados em binário (`mensal`, `aprovado` são recusados), porque os `CHECK` do esquema aceitariam minúsculas por causa da collation. Custo se errado: recusa um valor que o banco aceitaria.
- `SP_ENVIAR_ATESTADO` recusa pessoa inativa (a API não confere porque inativo não tem sessão); horas só em minutos cheios, de 00:00 a 23:59.
- As duas decisões do RH exigem que o responsável exista e esteja ativo, sem conferir o perfil. Custo se errado: um funcionário comum ativo decide pelo banco.
- Dependentes entram como JSON e o trajeto em três parâmetros (os nomes dos campos são os do contrato). Não há procedures para dependente ou trajeto pedido depois, numa adesão já ativa (quatro operações da API). Custo se errado: esses pedidos só existem pela API; seriam cerca de quatro procedures pequenas.

**Advertência, treinamento, formulário e desligamento**
- Acrescentada `SP_RESPONDER_CONTESTACAO_ADVERTENCIA` (sem ela a advertência contestada ficaria parada). Ela não recebe o texto da resposta do RH: o banco não tem coluna para ele (a API o manda por e-mail e o guarda na auditoria).
- `SP_GERAR_ADVERTENCIA` não confere o perfil de quem registra (só que existe e está ativo). Custo se errado: `ID_FUNCIONARIO_RH` pode apontar para quem não é do RH.
- `SP_ENCERRAR_FORMULARIO` vale como a chamada já confirmada (a contagem de pendentes é em código) e recusa o formulário com prazo vencido e `ATIVO` ainda ligado (a API já o trata como encerrado). Custo se errado: encerrar pelo banco não avisa que ainda há gente por responder.
- O bloco de efetivação do desligamento está **duplicado** em `SP_AGENDAR_DESLIGAMENTO` e `SP_EFETIVAR_DESLIGAMENTO` (com aviso nos dois). Custo se errado: os dois divergirem numa manutenção; cada efeito está testado nos dois caminhos.
- Efetivar o já efetivado termina sem erro (a API é idempotente). `SP_CONCLUIR_MODULO_TREINAMENTO` ganhou o `OUT P_TREINAMENTO_CONCLUIDO`.

**Cadastros**
- `SP_CRIAR_USUARIO` sem `P_SENHA_HASH`: cria login provisório, o único jeito de a API criar acesso; devolve o login provisório que já existe, sem erro. Custo se errado: quem queria definir a senha por SQL usa o convite ou um `UPDATE`.
- `SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO` sem parâmetro de ator: a regra "o RH não muda o próprio nível" depende de quem está logado. A API também não tem regra de "último RH": por SQL dá para deixar o sistema sem nenhum RH. Promover a `Funcionario Gestor` não cria linha em `GESTORES` (a API também não).
- `SP_CADASTRAR_FUNCIONARIO` mantém os opcionais (RG, e-mail pessoal, endereço), validados pelas regras da edição; `P_MATRICULA` virou `OUT` e `P_ID_GESTOR` saiu. Custo se errado: não dá para importar matrícula antiga pela procedure. Devolve a primeira recusa; a API devolve todos os erros de campo juntos.
- O contador de matrículas é travado no começo da transação (a API reserva no fim): uma recusa segura a trava por milissegundos.

## O que a API faz e a procedure não

- **Em todas:** autorização por perfil e eventos de auditoria.
- **Ponto:** decisão do pedido, aplicação das batidas, recálculo do banco de horas e e-mail do resultado. A `dataHora` dos itens do ajuste é horário de Brasília, sem `Z`.
- **Férias:** e-mail de cada decisão; a tarefa diária `ATUALIZAR_FERIAS` (aprovada vira em gozo e concluída, pedido vencido é cancelado, sugestão sem resposta expira) e o recálculo do banco de horas dos dias passados no registro do RH: pelo banco nada disso acontece sozinho. Trocar o gestor do setor não mexe nos pedidos já parados.
- **Holerite:** não guarda nem valida o PDF (o caminho precisa apontar para um arquivo que já exista), não calcula o dia do pagamento, não cria a tarefa de publicação (o e-mail não sai).
- **Atestado:** não guarda o arquivo; **validar não recalcula o banco de horas** dos dias cobertos.
- **Benefício:** não grava auditoria (é lá que a API guarda a autorização anterior na reabertura).
- **Advertência:** e-mails à pessoa, cópia ao RH e o e-mail da resposta da contestação.
- **Treinamento:** o PDF do certificado (`CAMINHO_ARQUIVO` fica `NULL`; a API gera no primeiro download).
- **Formulário:** a confirmação de pendentes e o cancelamento da tarefa de encerramento.
- **Desligamento:** criar e cancelar a tarefa `EFETIVAR_DESLIGAMENTO` (com a API no ar a recuperação, na subida e a cada hora, efetiva todo agendado vencido).
- **Cadastros:** não dá benefícios, não cria login nem convite ao cadastrar funcionário, não inclui no onboarding nem envia o formulário de admissão; `SP_CRIAR_USUARIO` não gera o link nem o e-mail, então **o login criado não serve para entrar** até o RH usar "Reenviar convite" na API.

## Achados fora das procedures (para a revisão com o grupo do banco)

Apontados pelos autores ao ler os scripts `V001` a `V006` (não alterei nenhum deles). Linhas de `V002` e `V001` são as do arquivo atual.

- `V002` (`TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT`): `REPEAT(LEFT(variavel, 1), 11)` sobre uma **variável local** de procedure altera a variável no MySQL 8.4 (reproduzido num contêiner à parte); o trigger usa a mesma expressão com `NEW.CPF` e não sofre. Quem copiar essa linha para uma procedure quebra tudo.
- `V002` (`TRG_PONTOS_BEFORE_INSERT`): inativo e admissão só são conferidos quando o dia é criado; uma batida em dia já existente passa no banco. Não há trigger em `PONTOS_MARCACOES` (sequência, janela de 12 h e origem sem guarda contra `INSERT` direto) nem em `PONTOS_MANUTENCOES_ITENS`.
- `V002` (`TRG_BANCOS_HORAS_BEFORE_INSERT`): nada impede o `INSERT` manual no banco de horas que a API não permite.
- `V002`: os triggers usam `NOW()` e `CURDATE()`: num MySQL em UTC (Azure) gravariam três horas adiantado se o fuso não for configurado.
- `V003` (`FN_CALCULAR_HORAS_PONTO`): segundos arredondados em horas com duas casas, contra minutos inteiros na API (já está como decisão no `LEIA-ME.md` dos scripts).
- `V002` (`TRG_FERIAS_BEFORE_INSERT/UPDATE`): saldo, fracionamento e sobreposição o esquema não confere; só as procedures e a API garantem. O trigger exige `ID_GESTOR` em toda aprovação ou rejeição (por isso o RH precisa estar em `GESTORES` no pedido sem etapa do gestor). A inativação só cancela férias com início depois de hoje. `V001` (`FERIAS`): não há coluna para quem do RH fez o registro direto.
- `V002` (`TRG_BENEFICIOS_FUNCIONARIOS_BEFORE_INSERT`): só olha o `INSERT`; a aprovação por `UPDATE` de pessoa inativa não é barrada. `TRG_FUNCIONARIOS_AFTER_UPDATE` desativa os benefícios ativos ao inativar, mas deixa os pedidos pendentes (a API cancela).
- `V002` (`TRG_ADVERTENCIAS_BEFORE_UPDATE`): só trava `CANCELADA` e `APLICADA → PENDENTE`; um `UPDATE` direto ainda faz `APLICADA → CONTESTADA`, que a API proíbe. `V006`: `ID_FUNCIONARIO_RH` só tem chave estrangeira (o banco não confere que a pessoa é do RH). `V001` (`ADVERTENCIAS`): não há coluna para a resposta do RH à contestação.
- `V002` (`TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_UPDATE`): cria o certificado com `UUID()` versão 1 e mesmo com `EMITE_CERTIFICADO = FALSE`; a API e a procedure compensam. Não há trigger sobre `TREINAMENTOS_MODULOS_PROGRESSO` (o comentário da procedure antiga dizia que havia).
- `V002` (`TRG_USUARIOS_BEFORE_INSERT`): só confere 40 caracteres na senha e lê `FUNCIONARIOS.ATIVO` sem trava. `TRG_FUNCIONARIOS_BEFORE_INSERT`: "gestor de si mesmo" nunca dispara no `INSERT` (o id ainda é 0); inofensivo.
- `V001`: `CARGOS.CARGA_HORARIA` aceita `NULL` (o contrato não); `TIPO_CONTRATO` é texto livre sem `CHECK` (a API só lê `CLT` e `ESTAGIO`); nada impede um quarto nível em `NIVEIS_DE_ACESSO`; os `CHECK ... IN (...)` de tipo e situação aceitam minúsculas por causa da collation `_ci` (`aprovado` gravado à mão pode não ser lido pela API).
- Contrato (`20-fatia-1-colaboradores.yaml`): diz nome de cargo único "sem diferenciar maiúsculas"; o banco e o código também ignoram acento.

## Como foi provado

- 261 testes no MySQL 8.4 (`Procedures/`), um banco novo por teste: caminho feliz com todas as colunas, cada recusa com a mensagem e a prova de que nada ficou gravado, os efeitos dos triggers, e as corridas (duas sessões na mesma linha).
- Cada autor fez dezenas de mutações (cerca de 170 no total: regra removida, limite trocado, trava retirada) e todas derrubaram algum teste, com raras exceções documentadas (uma regra duplicada com o trigger que recusa com o mesmo texto). Os autores escreveram o SQL e os testes juntos, sem ver cada teste falhar antes (o docker estava ocupado); as mutações cobrem essa falta.
- **Collation:** as provas rodam também num banco criado com a collation padrão do MySQL 8 (`RH_TESTES_COLACAO=utf8mb4_0900_ai_ci`, o padrão de um Flexible Server do Azure): as tabelas continuam em `utf8mb4_unicode_ci`, mas variáveis e parâmetros de procedure ganham a do banco. A primeira versão das procedures de ponto, férias e documentos quebrava ali (60 dos 156 testes dessas três seções, com "Illegal mix of collations"); hoje todas passam nas duas collations.
- Revisão independente por três revisores (cada um com uma fatia), com um passe de correção depois; veja a seção abaixo.

## Revisão independente e o que ela mudou

Três revisores de contexto novo leram o V007 (ponto e férias; holerite, atestado, benefício, advertência, treinamento, formulário e desligamento; cadastros e a visão geral): **nenhum achado Critical**. O que foi corrigido, cada item com um teste visto falhar antes:

- **Faltava `DROP PROCEDURE IF EXISTS`** nas 28: uma subida interrompida no meio deixaria a API sem conseguir subir (o script não ficaria registrado e a próxima tentativa pararia em "already exists"). Agora cada procedure é recriada, e um teste aplica o V007 duas vezes.
- **Collation:** comparação de variável ou parâmetro com coluna, sem `COLLATE`, quebrava num banco com a collation padrão do MySQL 8 (acima). `COLLATE utf8mb4_unicode_ci` explícito nos pontos de ponto (11), férias (3) e holerite (2).
- **Tipo da advertência com acento:** `SP_GERAR_ADVERTENCIA` aceitava `vérbal` (a collation ignora acento) e gravava um valor que a API não lê (as listas de advertências responderiam 500). Agora o tipo e a decisão da contestação são comparados em binário e gravados no literal canônico (minúsculas valem e viram maiúsculas; acento é recusado).
- **Deadlock de intervalo:** a leitura de existência com `FOR UPDATE` onde a pessoa ainda não tem linha travava um intervalo do índice, e duas pessoas diferentes ao mesmo tempo davam erro 1213 em vez de gravar (`SP_CRIAR_USUARIO`, `SP_PUBLICAR_HOLERITE`, `SP_SOLICITAR_BENEFICIO`). Tirado o `FOR UPDATE` dessas leituras (a trava da pessoa e a chave única já serializam a mesma pessoa), com um teste de várias pessoas em paralelo.
- **Ordem de trava no treinamento:** `SP_CONCLUIR_MODULO_TREINAMENTO` travava o módulo antes do treinamento (a API trava o treinamento primeiro): possível deadlock com o RH editando o módulo. Agora trava o treinamento primeiro, relê o módulo depois e recusa se ele foi excluído durante a espera.
- **Testes de corrida da parte D** não forçavam a sobreposição: passaram a segurar a trava numa terceira sessão e só soltar quando as chamadas estão esperando (e as mutações "retire o `FOR UPDATE`" caem de forma determinística).
- Outros: dois testes de férias que passavam sem a regra, o comentário do `SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO`, a mensagem de nível inexistente (uma só) e os textos (fuso, transação de quem chama, parâmetros do servidor).

**Decidido e não mudado (Rulings):** (1) as seções de férias, holerite, advertência, desligamento e cadastros leem "hoje" da **sessão** do banco (`NOW()`, `CURDATE()`), como os triggers do `V002`; só o ponto calcula UTC − 3 h. O projeto já exige o servidor em `-03:00`; com ele em UTC, entre 21h e 0h as datas sairiam três horas adiantadas (antecedência de 30 dias, prazo da sugestão, `DATA_SOLICITACAO`). O parâmetro `time_zone` do Azure está agora em `docs/demo/custos-e-como-desligar.md`. (2) O tempo dos testes (um banco novo por teste, uns 11 s cada sob carga; o `BancoCompartilhado` existe) não entrou no passe: não é defeito de comportamento.

**Minors adiados:** o "agora" é lido antes da espera pela trava em algumas procedures (as datas gravadas ficam alguns segundos antes); `CHAR_LENGTH` conta caracteres e a API conta UTF-16 (um emoji passa na procedure e leva 400 na API; sem dano); a seção de holerite, atestado e benefício apara só espaço (a de advertência apara qualquer espaço em branco); `SP_SUBSTITUIR_HOLERITE` lê a competência antes da trava (só importa contra uma efetivação de desligamento concorrente); JSON de dependentes com tipo errado é convertido; testes de holerite e advertência que passam sem a regra da procedure porque o trigger recusa com o mesmo texto; ramos de férias sem teste (período de desligado, readmissão, pessoa inativa na decisão do gestor); os testes de férias dependem do dia real (29/02 e a virada de ano não são exercitados); mensagens diferentes para a mesma regra em algumas procedures; `SP_ENVIAR_ATESTADO` trava a pessoa com `FOR SHARE` (as outras `FOR UPDATE`).

**Não consegui julgar:** isolamento `REPEATABLE READ` das procedures contra `READ COMMITTED` da API; `dataHora` com deslocamento no JSON do ajuste de ponto; deadlock entre dois RH que decidem, ao mesmo tempo, o pedido um do outro; e o Azure de verdade (collation, fuso, `sql_mode` e MySQL 8.4 com esses parâmetros).
