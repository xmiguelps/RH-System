-- ============================================================
-- V036 - Tarefas agendadas (TAREFAS_AGENDADAS)
-- Fatias 1, 3, 4, 5, 6 e 7 (roadmap, decisao 3: agendador)
-- ============================================================
-- POR QUE
--   Varias fatias dependem de um servico em segundo plano que faz algo "em
--   tal dia": efetivar um desligamento (1), consolidar o banco de horas do dia
--   e repetir feriados (3), publicar aviso e holerite agendados (4), publicar,
--   encerrar e reabrir formulario e mandar o lembrete (5), liberar
--   treinamento (6) e passar ferias para EM_GOZO e CONCLUIDA (7). O
--   roadmap exige que o ambiente possa ficar parado entre sessoes: cada
--   tarefa RECUPERA o que ficou para tras ao subir e e IDEMPOTENTE. Para
--   isso o estado de cada trabalho precisa estar no banco, nao na memoria.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria TAREFAS_AGENDADAS, uma linha por trabalho a fazer ou ja feito:
--   - TIPO: codigo do trabalho em maiusculas (EFETIVAR_DESLIGAMENTO,
--     PUBLICAR_AVISO, PUBLICAR_HOLERITE, CONSOLIDAR_BANCO_HORAS,
--     REPETIR_FERIADO, PUBLICAR_FORMULARIO, ENCERRAR_FORMULARIO,
--     REABRIR_FORMULARIO, LEMBRETE_FORMULARIO, LIBERAR_TREINAMENTO,
--     FERIAS_EM_GOZO... a lista e das fatias, o banco confere so o formato).
--   - CHAVE_IDEMPOTENCIA: UNICA e sempre comeca por "TIPO:" (CHECK). E o
--     que impede o mesmo trabalho de ser criado duas vezes. Exemplos:
--       CONSOLIDAR_BANCO_HORAS:dia=2026-10-06
--       PUBLICAR_AVISO:aviso=15
--       EFETIVAR_DESLIGAMENTO:desligamento=3
--       LEMBRETE_FORMULARIO:form=9:dia=2026-10-24:alcance=TODOS
--     Criar de novo bate na chave unica (erro 1062) ou use INSERT IGNORE:
--     e o sinal de "ja existe". Recuperar o que ficou para tras e inserir,
--     para cada dia ou item que falta, a tarefa com a sua chave: as que ja
--     existem nao duplicam.
--   - REFERENCIA_ID (o aviso, o holerite, o formulario... a que a tarefa se
--     refere, sem chave estrangeira de proposito: aponta para tabelas
--     diferentes conforme o TIPO), DIA_REFERENCIA (o dia de calendario que a
--     tarefa trata, como na consolidacao do dia) e PARAMETROS (JSON para o
--     que mais um tipo precisar, NUNCA dado pessoal).
--   - SITUACAO: PENDENTE, EM_EXECUCAO (reservada por um processo),
--     CONCLUIDA, FALHOU (esgotou as tentativas) ou CANCELADA (nao precisa
--     mais rodar, por exemplo o aviso agendado foi excluido).
--   - EXECUTAR_APOS (nao roda antes deste instante), TENTATIVAS,
--     RESERVADA_ATE (quem pega a tarefa a reserva por alguns minutos, e se o
--     processo cair, outro a retoma depois), ULTIMA_TENTATIVA_EM,
--     CONCLUIDA_EM, ULTIMO_ERRO, CRIADA_EM.
--
-- A MARCA "LEMBRETE DO FORMULARIO JA ENVIADO HOJE" (contrato, fatia 5)
--   O limite e um lembrete por formulario, por dia e por alcance (todos, no
--   do RH e no automatico, ou o setor do gestor). A marca vive AQUI, nunca
--   no formulario nem com o nome de ninguem: a aplicacao insere, no ato de
--   mandar, uma linha LEMBRETE_FORMULARIO ja CONCLUIDA com a chave
--   LEMBRETE_FORMULARIO:form=9:dia=2026-10-24:alcance=TODOS (ou
--   alcance=SETOR_3). Se o INSERT bater na chave unica, o lembrete de hoje
--   ja saiu e a aplicacao nao manda outro (e responde igual, para o
--   contraste nao revelar nada). No formulario ANONIMO e esta linha, e nao
--   uma por pessoa, que guarda o limite (ver o V035).
--   O lembrete AUTOMATICO 7 dias antes do prazo e uma tarefa PENDENTE com
--   EXECUTAR_APOS no dia certo e a mesma chave.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Reservar a tarefa sem corrida: UPDATE ... SET SITUACAO = EM_EXECUCAO,
--     RESERVADA_ATE = agora + alguns minutos WHERE ID = ? AND SITUACAO =
--     PENDENTE e conferir que UMA linha mudou. Tarefa EM_EXECUCAO com a
--     reserva vencida e retomada.
--   - Cada tipo e idempotente por si: ele confere o estado real (o aviso ja
--     foi publicado? o holerite ja tem e-mail na fila?) antes de agir, porque
--     o processo pode cair entre agir e marcar CONCLUIDA.
--   - Numero maximo de tentativas, espacamento e limpeza de tarefas antigas
--     (sugestao: apagar CONCLUIDA com mais de 30 dias, por uma tarefa
--     agendada).
--
-- FUSO: EXECUTAR_APOS, RESERVADA_ATE, ULTIMA_TENTATIVA_EM, CONCLUIDA_EM e
-- CRIADA_EM sao UTC, em tabela so da aplicacao (nenhum CHECK ou trigger a
-- compara com colunas do primeiro grupo). DIA_REFERENCIA e um dia de
-- calendario (DATE, o dia no fuso -03:00). Os padroes usam UTC_TIMESTAMP(6),
-- que NAO depende do fuso do servidor. No EF Core, mapeie CRIADA_EM e
-- EXECUTAR_APOS como geradas na inclusao (senao o EF manda 0001-01-01).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE TAREFAS_AGENDADAS (
    ID_TAREFA BIGINT AUTO_INCREMENT PRIMARY KEY,
    TIPO VARCHAR(50) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    CHAVE_IDEMPOTENCIA VARCHAR(190) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    REFERENCIA_ID INT NULL,
    DIA_REFERENCIA DATE NULL,
    PARAMETROS JSON NULL,
    SITUACAO VARCHAR(12) NOT NULL DEFAULT 'PENDENTE',
    EXECUTAR_APOS DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    TENTATIVAS SMALLINT NOT NULL DEFAULT 0,
    RESERVADA_ATE DATETIME(6) NULL,
    ULTIMA_TENTATIVA_EM DATETIME(6) NULL,
    CONCLUIDA_EM DATETIME(6) NULL,
    ULTIMO_ERRO VARCHAR(500) NULL,
    CRIADA_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),

    CONSTRAINT UQ_TAREFAS_AGENDADAS_CHAVE UNIQUE (CHAVE_IDEMPOTENCIA),
    CONSTRAINT CHK_TAREFAS_AGENDADAS_TIPO CHECK (REGEXP_LIKE(TIPO, '^[A-Z][A-Z0-9_]{2,49}$', 'c')),
    -- a chave comeca por "TIPO:" (LEFT em vez de LIKE, porque "_" no LIKE e curinga)
    CONSTRAINT CHK_TAREFAS_AGENDADAS_CHAVE_COM_TIPO
        CHECK (LEFT(CHAVE_IDEMPOTENCIA, CHAR_LENGTH(TIPO) + 1) = CONCAT(TIPO, ':')),
    CONSTRAINT CHK_TAREFAS_AGENDADAS_SITUACAO
        CHECK (SITUACAO IN ('PENDENTE', 'EM_EXECUCAO', 'CONCLUIDA', 'FALHOU', 'CANCELADA')),
    CONSTRAINT CHK_TAREFAS_AGENDADAS_TENTATIVAS CHECK (TENTATIVAS >= 0),
    -- reservada se e so se esta EM_EXECUCAO
    CONSTRAINT CHK_TAREFAS_AGENDADAS_RESERVA CHECK ((SITUACAO = 'EM_EXECUCAO') = (RESERVADA_ATE IS NOT NULL)),
    -- hora da conclusao se e so se esta CONCLUIDA
    CONSTRAINT CHK_TAREFAS_AGENDADAS_CONCLUIDA_EM CHECK ((SITUACAO = 'CONCLUIDA') = (CONCLUIDA_EM IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- o agendador: o que esta pendente e ja pode rodar (e as reservas vencidas)
CREATE INDEX IDX_TAREFAS_AGENDADAS_SITUACAO_EXECUTAR ON TAREFAS_AGENDADAS(SITUACAO, EXECUTAR_APOS);

-- "a tarefa deste aviso, holerite ou formulario" e "as tarefas deste dia"
CREATE INDEX IDX_TAREFAS_AGENDADAS_TIPO_REFERENCIA ON TAREFAS_AGENDADAS(TIPO, REFERENCIA_ID);
CREATE INDEX IDX_TAREFAS_AGENDADAS_TIPO_DIA ON TAREFAS_AGENDADAS(TIPO, DIA_REFERENCIA);
