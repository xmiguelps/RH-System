-- ============================================================
-- V007: PROCEDURES (decisao do dono, 2026-11-17)
-- Reescrita do arquivo de procedures do grupo (RHSystem_Procedures_RHSystem_Atual.sql, 24 procedures) alinhada ao contrato da API
-- (contrato/) e aos scripts V001 a V006, testada em MySQL 8.4 (back-end/tests/.../Procedures).
--
-- Somente PROCEDURES: nenhuma tabela, FUNCTION ou TRIGGER e criada aqui. Cada procedure e UMA operacao transacional de negocio, com as mesmas
-- regras e os mesmos efeitos no BANCO que o caso de uso equivalente da API. O que fica de fora de proposito (a API faz e o banco nao pode):
-- autorizacao por perfil, envio de e-mail, arquivo no armazenamento, eventos de auditoria e tarefas agendadas.
-- Mensagens de recusa: SIGNAL SQLSTATE '45000', sem acento (como as dos triggers do V002).
-- ============================================================

DELIMITER $$

-- [[DOMINIO-A: PONTO]]
-- Duas procedures: SP_REGISTRAR_MARCACAO_PONTO e SP_SOLICITAR_AJUSTE_PONTO. SP_LANCAR_BANCO_HORAS, do arquivo antigo, foi REMOVIDA de proposito
-- (o porque esta no fim desta secao), e as decisoes do pedido de ajuste (gestor e RH) continuam so na API (tambem explicado no fim).
-- As duas travam primeiro a linha da pessoa em FUNCIONARIOS (FOR UPDATE), a mesma trava que a API usa em tudo o que grava ponto: bater ponto,
-- pedir ajuste e decidir pedido da mesma pessoa nunca correm entre si, venham da API ou daqui.
-- Horarios: o banco guarda DATETIME no relogio de Brasilia (-03:00) e os horarios que entram por parametro tambem sao de Brasilia. O "agora"
-- das duas e UTC_TIMESTAMP() - INTERVAL 3 HOUR (o mesmo deslocamento fixo da API, FusoBrasil): com o servidor em default-time-zone=-03:00 e
-- igual a NOW(), e continua certo se a sessao ou o servidor estiverem em outro fuso (um MySQL gerenciado costuma vir em UTC).

-- ------------------------------------------------------------
-- SP_REGISTRAR_MARCACAO_PONTO: a pessoa bate o ponto AGORA.
-- Espelha: operationId registrarMarcacaoDePonto (POST /api/eu/ponto/marcacoes), RegistrarMarcacaoCasoDeUso + SequenciaDoDia.ProximaMarcacao.
--
-- Parametros: P_ID_FUNCIONARIO; P_TIPO_MARCACAO = a batida que a pessoa ACHA que esta fazendo ('ENTRADA' ou 'SAIDA', o tipoEsperado da API);
--             OUT P_ID_MARCACAO = a batida criada.
-- O horario e sempre o do servidor (lido depois da trava, sem fracao de segundo); o tipo e o dia do ponto quem calcula e a procedure:
--   - sem batida hoje e com a ultima batida de ONTEM sendo uma ENTRADA de menos de 12 horas atras: e a SAIDA dessa jornada e entra no dia de
--     ONTEM (jornada que atravessa a meia-noite; a janela de 12 horas e a do contrato, x-provisorio);
--   - senao alterna a ultima batida de hoje (por horario e depois por id): a primeira do dia e ENTRADA, depois de ENTRADA vem SAIDA.
-- Regras, na ordem de recusa:
--   1. entrada: funcionario informado; tipo ENTRADA ou SAIDA;
--   2. a pessoa existe;
--   3. a pessoa esta ativa (mesmo texto do trigger TRG_PONTOS_BEFORE_INSERT, que so confere quando o dia e criado);
--   4. hoje nao e anterior a admissao (mesmo texto do trigger). "Posterior ao desligamento" nao tem como acontecer aqui: quem tem data de
--      desligamento esta inativo (CHK_FUNCIONARIOS_ATIVO_DESLIGAMENTO) e ja caiu na regra 3;
--   5. nao ha batida da pessoa neste mesmo segundo, hoje ou ontem (duplo clique);
--   6. o tipo informado e o que a procedure calcula; se nao for, nada e gravado e a mensagem diz qual e a proxima.
-- Efeitos: cria a linha de PONTOS do dia se faltar (o trigger copia a carga do cargo / 5) e grava a batida com ORIGEM 'SISTEMA'.
-- A API faz e a procedure nao: evento de auditoria (AUDITORIA_EVENTOS) e autorizacao (na API a pessoa so bate o proprio ponto).
-- Bater em feriado, fim de semana ou ferias e aceito, como na API.
-- Mudou em relacao ao arquivo antigo:
--   - saiu o parametro P_DATA_HORA: a API nao deixa escolher o horario da batida (batida em outro horario e pedido de ajuste ou correcao do RH);
--   - entrou a jornada que atravessa a meia-noite (janela de 12 horas) e a batida no dia anterior;
--   - o tipo passou a ser conferido contra o calculado (antes eram tres mensagens de sequencia sobre o dia de P_DATA_HORA);
--   - entraram a recusa de batida no mesmo segundo e a de dia anterior a admissao; pessoa inexistente e inativa tem mensagens separadas.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_REGISTRAR_MARCACAO_PONTO(
    IN P_ID_FUNCIONARIO INT,
    IN P_TIPO_MARCACAO VARCHAR(10),
    OUT P_ID_MARCACAO INT
)
BEGIN
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_AGORA DATETIME;
    DECLARE V_HOJE DATE;
    DECLARE V_ID_PONTO_HOJE INT DEFAULT NULL;
    DECLARE V_ID_PONTO_ONTEM INT DEFAULT NULL;
    DECLARE V_ULTIMO_TIPO_HOJE VARCHAR(10) DEFAULT NULL;
    DECLARE V_ULTIMO_TIPO_ONTEM VARCHAR(10) DEFAULT NULL;
    DECLARE V_ULTIMA_HORA_ONTEM DATETIME DEFAULT NULL;
    DECLARE V_PROXIMO_TIPO VARCHAR(10);
    DECLARE V_DIA DATE;
    DECLARE V_ID_PONTO INT;
    DECLARE V_MENSAGEM VARCHAR(128);

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_MARCACAO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario.';
    END IF;

    IF P_TIPO_MARCACAO IS NULL OR P_TIPO_MARCACAO NOT IN ('ENTRADA', 'SAIDA') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Tipo de marcacao invalido: informe ENTRADA ou SAIDA.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO, DATA_ADMISSAO INTO V_ATIVO, V_ADMISSAO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    -- So depois da trava: a segunda de duas chamadas simultaneas ve a batida da primeira e bate com o proprio horario.
    SET V_AGORA = UTC_TIMESTAMP() - INTERVAL 3 HOUR;
    SET V_HOJE = DATE(V_AGORA);

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel registrar ponto de funcionario inativo.';
    END IF;

    IF V_HOJE < V_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data do ponto e anterior a admissao.';
    END IF;

    SELECT MAX(ID_PONTO) INTO V_ID_PONTO_HOJE
      FROM PONTOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO AND DATA = V_HOJE;

    SELECT MAX(ID_PONTO) INTO V_ID_PONTO_ONTEM
      FROM PONTOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO AND DATA = V_HOJE - INTERVAL 1 DAY;

    IF EXISTS (SELECT 1
                 FROM PONTOS_MARCACOES
                WHERE ID_PONTO IN (V_ID_PONTO_HOJE, V_ID_PONTO_ONTEM)
                  AND DATA_HORA_MARCACAO = V_AGORA) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O ponto ja foi registrado neste mesmo segundo.';
    END IF;

    SELECT (SELECT TIPO_MARCACAO
              FROM PONTOS_MARCACOES
             WHERE ID_PONTO = V_ID_PONTO_HOJE
             ORDER BY DATA_HORA_MARCACAO DESC, ID_MARCACAO DESC
             LIMIT 1)
      INTO V_ULTIMO_TIPO_HOJE;

    SELECT (SELECT TIPO_MARCACAO
              FROM PONTOS_MARCACOES
             WHERE ID_PONTO = V_ID_PONTO_ONTEM
             ORDER BY DATA_HORA_MARCACAO DESC, ID_MARCACAO DESC
             LIMIT 1),
           (SELECT DATA_HORA_MARCACAO
              FROM PONTOS_MARCACOES
             WHERE ID_PONTO = V_ID_PONTO_ONTEM
             ORDER BY DATA_HORA_MARCACAO DESC, ID_MARCACAO DESC
             LIMIT 1)
      INTO V_ULTIMO_TIPO_ONTEM, V_ULTIMA_HORA_ONTEM;

    IF V_ULTIMO_TIPO_HOJE IS NULL
       AND V_ULTIMO_TIPO_ONTEM = 'ENTRADA'
       AND TIMESTAMPDIFF(SECOND, V_ULTIMA_HORA_ONTEM, V_AGORA) < 12 * 3600 THEN
        -- A jornada que atravessou a meia-noite: a SAIDA entra no dia da ENTRADA.
        SET V_PROXIMO_TIPO = 'SAIDA';
        SET V_DIA = V_HOJE - INTERVAL 1 DAY;
        SET V_ID_PONTO = V_ID_PONTO_ONTEM;
    ELSE
        SET V_PROXIMO_TIPO = IF(COALESCE(V_ULTIMO_TIPO_HOJE, '') = 'ENTRADA', 'SAIDA', 'ENTRADA');
        SET V_DIA = V_HOJE;
        SET V_ID_PONTO = V_ID_PONTO_HOJE;
    END IF;

    IF P_TIPO_MARCACAO <> V_PROXIMO_TIPO THEN
        SET V_MENSAGEM = CONCAT('A sequencia do ponto mudou: a proxima marcacao e ', V_PROXIMO_TIPO, '.');
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = V_MENSAGEM;
    END IF;

    IF V_ID_PONTO IS NULL THEN
        INSERT INTO PONTOS (ID_FUNCIONARIO, DATA)
        VALUES (P_ID_FUNCIONARIO, V_DIA);
        SET V_ID_PONTO = LAST_INSERT_ID();
    END IF;

    INSERT INTO PONTOS_MARCACOES (ID_PONTO, DATA_HORA_MARCACAO, TIPO_MARCACAO, ORIGEM, DATA_CORRECAO)
    VALUES (V_ID_PONTO, V_AGORA, V_PROXIMO_TIPO, 'SISTEMA', NULL);

    SET P_ID_MARCACAO = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_SOLICITAR_AJUSTE_PONTO: a pessoa pede o ajuste das batidas de UM dia que ja passou.
-- Espelha: operationId criarPedidoDeAjuste (POST /api/eu/ponto/pedidos-de-ajuste), CriarPedidoDeAjusteCasoDeUso + AplicacaoDosItens +
--          GestorAusente.EtapaInicialDoPedidoAsync (aprovacao em duas etapas, gestor e depois RH).
--
-- Parametros: P_ID_FUNCIONARIO; P_DATA = o dia do ponto; P_JUSTIFICATIVA; P_ITENS = lista JSON de 1 a 10 batidas pedidas;
--             OUT P_ID_MANUTENCAO = o pedido criado.
-- Formato de P_ITENS (horario de Brasilia; a fracao de segundo e descartada):
--   [{"idMarcacao": null, "dataHora": "2026-11-09 08:00:00", "tipo": "ENTRADA"}, {"idMarcacao": 15, "dataHora": "2026-11-09 17:05:00", "tipo": "SAIDA"}]
--   idMarcacao nulo (ou ausente) = batida nova; preenchido = corrigir essa batida, que tem de ser do mesmo dia da mesma pessoa.
-- Regras, na ordem de recusa:
--    1. entrada: funcionario e dia informados; justificativa de 5 a 500 caracteres (sem os espacos das pontas); de 1 a 10 itens; cada item com
--       tipo ENTRADA ou SAIDA, dataHora valida e idMarcacao inteiro ou nulo;
--    2. a pessoa existe;
--    3. a pessoa esta ativa (texto do trigger de PONTOS);
--    4. o dia nao e anterior a admissao (texto do trigger de PONTOS);
--    5. o dia ja passou (antes de hoje);
--    6. nenhum horario esta no futuro;
--    7. cada horario cai no dia do pedido ou no dia seguinte (jornada que atravessa a meia-noite);
--    8. o dia nao tem outro pedido PENDENTE (a chave UQ_PONTOS_MANUTENCOES_UM_PENDENTE garante o mesmo se algo escapar);
--    9. cada idMarcacao e de uma batida deste dia, e nenhuma batida e corrigida duas vezes (vale o primeiro item com problema, na ordem da lista);
--   10. o dia, DEPOIS de aplicar os itens, alterna ENTRADA e SAIDA em ordem de horario, comeca em ENTRADA, termina em SAIDA e nao tem duas
--       batidas no mesmo segundo (a regra de FN_VERIFICAR_CONSISTENCIA_PONTO, aplicada ao dia como ele ficaria).
-- Etapa em que o pedido nasce (nao ha coluna de etapa: ela se le de ID_GESTOR e RESULTADO_GESTOR):
--   - quem pede e gestor (linha em GESTORES e gerencia algum setor, ou tem o nivel 'RH'), ou nao tem setor, ou o setor nao tem gestor, ou o
--     gestor do setor esta inativo: etapa do RH, sem etapa do gestor (ID_GESTOR nulo);
--   - o gestor do setor (SETORES.ID_GESTOR; FUNCIONARIOS.ID_GESTOR nao e usado) esta ausente hoje (GESTORES.APROVACAO_AUTOMATICA ligada e hoje
--     antes de APROVACAO_AUTOMATICA_ATE, ou sem data de volta): ID_GESTOR dele, etapa do gestor ja aprovada sozinha (RESULTADO_GESTOR
--     'APROVOU', DATA_ETAPA_GESTOR agora, ETAPA_GESTOR_AUTOMATICA verdadeiro) e o pedido segue para o RH;
--   - senao: etapa do gestor (ID_GESTOR dele, sem resultado).
-- Efeitos: cria a linha de PONTOS do dia se faltar (dia de falta, sem nenhuma batida), o pedido PENDENTE em PONTOS_MANUTENCOES e os itens em
-- PONTOS_MANUTENCOES_ITENS. NENHUMA batida muda: as batidas so mudam quando o RH aprova o pedido (pela API).
-- A API faz e a procedure nao: evento de auditoria e autorizacao (na API a pessoa so pede para si). Nenhum e-mail sai na criacao, nem na API.
-- Mudou em relacao ao arquivo antigo:
--   - P_ID_PONTO virou P_DATA: a API recebe o DIA e cria o registro do dia se faltar (com P_ID_PONTO nao dava para pedir ajuste de um dia sem batida);
--   - o pedido nasce com o gestor e a etapa certos (antes ID_GESTOR ficava sempre nulo e todo pedido caia direto no RH);
--   - entraram as regras 4 a 10 e os limites da justificativa e da lista; antes um item com tipo nulo passava da validacao e um idMarcacao
--     de outro dia so era barrado pela chave estrangeira.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SOLICITAR_AJUSTE_PONTO(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA DATE,
    IN P_JUSTIFICATIVA TEXT,
    IN P_ITENS JSON,
    OUT P_ID_MANUTENCAO INT
)
BEGIN
    DECLARE V_JUSTIFICATIVA TEXT;
    DECLARE V_QTD INT DEFAULT 0;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_ID_SETOR INT DEFAULT NULL;
    DECLARE V_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_AGORA_EXATO DATETIME(6);
    DECLARE V_AGORA DATETIME;
    DECLARE V_ID_PONTO INT DEFAULT NULL;
    DECLARE V_ITEM_DE_OUTRO_DIA INT DEFAULT NULL;
    DECLARE V_ITEM_REPETIDO INT DEFAULT NULL;
    DECLARE V_TOTAL INT DEFAULT 0;
    DECLARE V_HORARIOS INT DEFAULT 0;
    DECLARE V_FORA_DE_ORDEM INT DEFAULT 0;
    DECLARE V_E_GESTOR BOOLEAN DEFAULT FALSE;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;
    DECLARE V_GESTOR_AUSENTE BOOLEAN DEFAULT FALSE;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_MANUTENCAO = NULL;

    IF P_ID_FUNCIONARIO IS NULL OR P_DATA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario e o dia do ponto.';
    END IF;

    SET V_JUSTIFICATIVA = REGEXP_REPLACE(COALESCE(P_JUSTIFICATIVA, ''), '^[[:space:]]+|[[:space:]]+$', '');
    IF CHAR_LENGTH(V_JUSTIFICATIVA) NOT BETWEEN 5 AND 500 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A justificativa deve ter de 5 a 500 caracteres.';
    END IF;

    IF P_ITENS IS NULL OR JSON_TYPE(P_ITENS) <> 'ARRAY' OR JSON_LENGTH(P_ITENS) NOT BETWEEN 1 AND 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe de 1 a 10 marcacoes em P_ITENS (lista JSON).';
    END IF;

    -- Um valor que nao converte (tipo comprido, data que nao existe, idMarcacao que nao e numero) chega aqui como NULL.
    SELECT COUNT(*) INTO V_QTD
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (
               ID_INFORMADO JSON PATH '$.idMarcacao',
               ID_MARCACAO INT PATH '$.idMarcacao',
               DATA_HORA DATETIME(6) PATH '$.dataHora',
               TIPO VARCHAR(10) PATH '$.tipo'
           )) AS J
     WHERE J.TIPO IS NULL OR J.TIPO NOT IN ('ENTRADA', 'SAIDA')
        OR J.DATA_HORA IS NULL
        OR (J.ID_MARCACAO IS NULL AND J.ID_INFORMADO IS NOT NULL AND JSON_TYPE(J.ID_INFORMADO) <> 'NULL');

    IF V_QTD > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ha item com tipo, dataHora ou idMarcacao invalido.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO, DATA_ADMISSAO, ID_SETOR INTO V_ATIVO, V_ADMISSAO, V_ID_SETOR
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SET V_AGORA_EXATO = UTC_TIMESTAMP(6) - INTERVAL 3 HOUR;
    SET V_AGORA = V_AGORA_EXATO - INTERVAL MICROSECOND(V_AGORA_EXATO) MICROSECOND;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel registrar ponto de funcionario inativo.';
    END IF;

    IF P_DATA < V_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data do ponto e anterior a admissao.';
    END IF;

    IF P_DATA >= DATE(V_AGORA) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'So e possivel pedir ajuste de um dia que ja passou.';
    END IF;

    SELECT COUNT(*) INTO V_QTD
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (DATA_HORA DATETIME(6) PATH '$.dataHora')) AS J
     WHERE J.DATA_HORA > V_AGORA_EXATO;

    IF V_QTD > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ha marcacao com horario no futuro.';
    END IF;

    SELECT COUNT(*) INTO V_QTD
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (DATA_HORA DATETIME(6) PATH '$.dataHora')) AS J
     WHERE J.DATA_HORA < P_DATA OR J.DATA_HORA >= P_DATA + INTERVAL 2 DAY;

    IF V_QTD > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cada marcacao deve cair no dia do pedido ou no dia seguinte.';
    END IF;

    SELECT MAX(ID_PONTO) INTO V_ID_PONTO
      FROM PONTOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO AND DATA = P_DATA;

    IF EXISTS (SELECT 1
                 FROM PONTOS_MANUTENCOES
                WHERE ID_PONTO = V_ID_PONTO
                  AND ID_FUNCIONARIO = P_ID_FUNCIONARIO
                  AND STATUS_SOLICITACAO = 'PENDENTE') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este dia ja tem um pedido de ajuste em analise.';
    END IF;

    -- O primeiro item (na ordem da lista) que aponta uma batida que nao e deste dia, e o primeiro que repete a batida de um item anterior.
    SELECT MIN(J.ORDEM) INTO V_ITEM_DE_OUTRO_DIA
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (ORDEM FOR ORDINALITY, ID_MARCACAO INT PATH '$.idMarcacao')) AS J
     WHERE J.ID_MARCACAO IS NOT NULL
       AND NOT EXISTS (SELECT 1
                         FROM PONTOS_MARCACOES M
                        WHERE M.ID_MARCACAO = J.ID_MARCACAO AND M.ID_PONTO = V_ID_PONTO);

    SELECT MIN(DEPOIS.ORDEM) INTO V_ITEM_REPETIDO
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (ORDEM FOR ORDINALITY, ID_MARCACAO INT PATH '$.idMarcacao')) AS ANTES
      JOIN JSON_TABLE(P_ITENS, '$[*]' COLUMNS (ORDEM FOR ORDINALITY, ID_MARCACAO INT PATH '$.idMarcacao')) AS DEPOIS
        ON DEPOIS.ID_MARCACAO = ANTES.ID_MARCACAO AND DEPOIS.ORDEM > ANTES.ORDEM;

    IF V_ITEM_DE_OUTRO_DIA IS NOT NULL AND (V_ITEM_REPETIDO IS NULL OR V_ITEM_DE_OUTRO_DIA < V_ITEM_REPETIDO) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A marcacao a corrigir (idMarcacao) nao e deste dia.';
    END IF;

    IF V_ITEM_REPETIDO IS NOT NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Cada marcacao so pode ser corrigida uma vez no mesmo pedido.';
    END IF;

    -- O dia como ficaria: as batidas que nenhum item corrige, mais os itens (novas e corrigidas), em ordem de horario.
    WITH ITENS AS (
        SELECT J.ID_MARCACAO,
               J.DATA_HORA - INTERVAL MICROSECOND(J.DATA_HORA) MICROSECOND AS DATA_HORA,
               IF(J.TIPO = 'ENTRADA', 'ENTRADA', 'SAIDA') AS TIPO
          FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (
                   ID_MARCACAO INT PATH '$.idMarcacao',
                   DATA_HORA DATETIME(6) PATH '$.dataHora',
                   TIPO VARCHAR(10) PATH '$.tipo'
               )) AS J
    ),
    DIA AS (
        SELECT M.DATA_HORA_MARCACAO AS DATA_HORA, IF(M.TIPO_MARCACAO = 'ENTRADA', 'ENTRADA', 'SAIDA') AS TIPO
          FROM PONTOS_MARCACOES M
         WHERE M.ID_PONTO = V_ID_PONTO
           AND NOT EXISTS (SELECT 1 FROM ITENS I WHERE I.ID_MARCACAO = M.ID_MARCACAO)
        UNION ALL
        SELECT I.DATA_HORA, I.TIPO
          FROM ITENS I
    ),
    EM_ORDEM AS (
        SELECT D.DATA_HORA, D.TIPO, ROW_NUMBER() OVER (ORDER BY D.DATA_HORA) AS POSICAO
          FROM DIA D
    )
    SELECT COUNT(*),
           COUNT(DISTINCT E.DATA_HORA),
           COALESCE(SUM(E.TIPO <> IF(E.POSICAO % 2 = 1, 'ENTRADA', 'SAIDA')), 0)
      INTO V_TOTAL, V_HORARIOS, V_FORA_DE_ORDEM
      FROM EM_ORDEM E;

    IF V_HORARIOS <> V_TOTAL OR V_FORA_DE_ORDEM > 0 OR V_TOTAL % 2 = 1 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Com estas marcacoes o dia nao fecha em pares de ENTRADA e SAIDA. Confira os horarios.';
    END IF;

    -- Quem decide primeiro (CONVENCOES.md, secao 9; GestorAusente.EtapaInicialDoPedidoAsync).
    SELECT MAX(N.NOME_NIVEL_ACESSO) INTO V_NIVEL
      FROM FUNCIONARIOS F
      JOIN NIVEIS_DE_ACESSO N ON N.ID_NIVEL_ACESSO = F.ID_NIVEL_ACESSO
     WHERE F.ID_FUNCIONARIO = P_ID_FUNCIONARIO;

    SELECT EXISTS (SELECT 1
                     FROM GESTORES G
                    WHERE G.ID_FUNCIONARIO = P_ID_FUNCIONARIO
                      AND (COALESCE(V_NIVEL, '') = 'RH'
                           OR EXISTS (SELECT 1 FROM SETORES S WHERE S.ID_GESTOR = G.ID_GESTOR)))
      INTO V_E_GESTOR;

    IF NOT V_E_GESTOR AND V_ID_SETOR IS NOT NULL THEN
        SELECT MAX(G.ID_GESTOR),
               COALESCE(MAX(G.APROVACAO_AUTOMATICA AND (G.APROVACAO_AUTOMATICA_ATE IS NULL OR DATE(V_AGORA) < G.APROVACAO_AUTOMATICA_ATE)), FALSE)
          INTO V_ID_GESTOR, V_GESTOR_AUSENTE
          FROM SETORES S
          JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
          JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
         WHERE S.ID_SETOR = V_ID_SETOR
           AND F.ATIVO = TRUE;
    END IF;

    IF V_ID_PONTO IS NULL THEN
        INSERT INTO PONTOS (ID_FUNCIONARIO, DATA)
        VALUES (P_ID_FUNCIONARIO, P_DATA);
        SET V_ID_PONTO = LAST_INSERT_ID();
    END IF;

    INSERT INTO PONTOS_MANUTENCOES
        (ID_PONTO, ID_FUNCIONARIO, JUSTIFICATIVA, DATA_SOLICITACAO, STATUS_SOLICITACAO,
         ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ETAPA_GESTOR_AUTOMATICA)
    VALUES
        (V_ID_PONTO, P_ID_FUNCIONARIO, V_JUSTIFICATIVA, V_AGORA, 'PENDENTE',
         V_ID_GESTOR, IF(V_GESTOR_AUSENTE, 'APROVOU', NULL), IF(V_GESTOR_AUSENTE, V_AGORA, NULL), V_GESTOR_AUSENTE);

    SET P_ID_MANUTENCAO = LAST_INSERT_ID();

    INSERT INTO PONTOS_MANUTENCOES_ITENS
        (ID_MANUTENCAO_PONTO, ID_PONTO, ID_MARCACAO, DATA_HORA_PROPOSTA, TIPO_MARCACAO)
    SELECT P_ID_MANUTENCAO,
           V_ID_PONTO,
           J.ID_MARCACAO,
           J.DATA_HORA - INTERVAL MICROSECOND(J.DATA_HORA) MICROSECOND,
           IF(J.TIPO = 'ENTRADA', 'ENTRADA', 'SAIDA')
      FROM JSON_TABLE(P_ITENS, '$[*]' COLUMNS (
               ORDEM FOR ORDINALITY,
               ID_MARCACAO INT PATH '$.idMarcacao',
               DATA_HORA DATETIME(6) PATH '$.dataHora',
               TIPO VARCHAR(10) PATH '$.tipo'
           )) AS J
     ORDER BY J.ORDEM;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_LANCAR_BANCO_HORAS (arquivo antigo): REMOVIDA, nao existe no V007.
-- Ela lancava credito ou debito a mao em BANCOS_HORAS. A API nao tem essa operacao: o banco de horas so nasce do calculo do dia (tarefa
-- agendada CONSOLIDAR_BANCO_HORAS e o recalculo depois de pedido decidido, correcao do RH ou atestado validado), uma linha por pessoa e dia
-- (UQ_BANCOS_HORAS_FUNCIONARIO_DIA), e cada recalculo REESCREVE ou APAGA a linha do dia. Um lancamento manual seria perdido no proximo
-- recalculo daquele dia, ou ficaria para sempre no saldo se o dia nunca mais fosse recalculado: nos dois casos o saldo deixa de bater com o
-- espelho de ponto.
-- Tambem nao foi trocada por uma "consolidacao do dia em SQL": a conta da API nao sai das funcoes do V003. Ela e feita em minutos inteiros,
-- descartando os segundos de cada batida (FN_CALCULAR_HORAS_PONTO soma segundos e arredonda em horas com 2 casas), e depende do calendario que
-- a API monta (feriado que vale para a UF e o municipio da empresa, ferias, atestado validado de dia inteiro e de horas, pedido em analise,
-- carga do dia). Reescrever isso aqui criaria uma segunda conta, que divergiria da primeira na primeira mudanca de regra.
-- Para corrigir o saldo de alguem: corrija as batidas do dia (pedido de ajuste ou correcao do RH, pela API) e o dia e recalculado.
--
-- Decisoes do pedido de ajuste (aprovar e recusar, do gestor e do RH): NAO ha procedure, de proposito. Na API toda decisao que encerra o
-- pedido (recusa do gestor, aprovacao e recusa do RH) recalcula o banco de horas do dia na mesma transacao, e a aprovacao do RH ainda
-- aplica as batidas; como o recalculo so existe na API (acima), uma procedure de decisao deixaria o banco de horas errado. O pedido criado
-- por SP_SOLICITAR_AJUSTE_PONTO aparece nas filas do gestor e do RH da API e e decidido por la.
-- ------------------------------------------------------------






-- [[DOMINIO-B: FERIAS]]
-- Sete procedures. As tres do arquivo antigo (SP_SOLICITAR_FERIAS, SP_DECIDIR_FERIAS_GESTOR, SP_CONFIRMAR_FERIAS_RH) foram refeitas e
-- quatro foram acrescentadas, porque a API tem a operacao e sem ela o fluxo nao fecha: SP_CONFERIR_FERIAS (as regras, sem gravar),
-- SP_REGISTRAR_FERIAS_RH (o RH registra direto), SP_RECUSAR_FERIAS_RH (o RH tambem recusa) e SP_RESPONDER_SUGESTAO_FERIAS (a pessoa
-- responde a sugestao de outra data; sem ela o pedido com SUGERIU_DATA ficaria parado para sempre).
--
-- O que vale para todas:
--   * Nao ha coluna de etapa em FERIAS. A situacao de um pedido SOLICITADA sai das colunas, como na API (SituacaoDoPedidoDeFerias):
--       com o gestor  : ID_GESTOR preenchido e RESULTADO_GESTOR nulo;
--       sugestao      : RESULTADO_GESTOR = SUGERIU_DATA e RESPOSTA_SUGESTAO nula (espera a PESSOA; ninguem decide);
--       com o RH      : o resto (sem ID_GESTOR; ou o gestor APROVOU, inclusive automatico; ou a sugestao foi aceita).
--     APROVADA, EM_GOZO, CONCLUIDA, REJEITADA e CANCELADA sao finais.
--   * Ordem das travas, a mesma de toda escrita de ferias na API: primeiro a linha da pessoa em FUNCIONARIOS (FOR UPDATE), depois a linha do
--     pedido. E o que serializa dois pedidos, o pedido e o registro do RH, o aceite e o pedido: o saldo nunca estoura.
--   * OBSERVACAO e a mensagem da pessoa (ou a observacao do RH no registro direto) e nenhuma decisao a sobrescreve; o motivo de uma recusa
--     vai em MOTIVO_RECUSA.
--   * Fica de fora, porque a API faz fora do banco: autorizacao por perfil (se quem chama e mesmo gestor ou RH), e-mail da decisao, evento
--     de auditoria, e a tarefa diaria ATUALIZAR_FERIAS (APROVADA vira EM_GOZO e CONCLUIDA; pedido cujo inicio passou sem decisao e cancelado;
--     sugestao sem resposta expira).

-- ------------------------------------------------------------
-- SP_CONFERIR_FERIAS: confere as regras de um periodo de ferias SEM gravar nada e devolve o periodo aquisitivo em que ele entra.
-- Espelha: conferirPedidoDeFerias (POST /api/eu/ferias/conferencia, com P_EXIGE_ANTECEDENCIA = TRUE) e conferirRegistroDeFerias
-- (POST /api/rh/funcionarios/{funcionarioId}/ferias/conferencia, com FALSE); e a MESMA conferencia que pedir, registrar, sugerir e aceitar
-- fazem (ConferenciaDeFerias.Conferir), por isso as outras procedures a chamam, ja dentro da transacao e com a pessoa travada.
-- NAO abre transacao e nao trava nada: sozinha, e so uma consulta.
-- P_ID_FERIAS: nulo para um pedido novo; o id de um pedido que ja existe quando as datas dele estao sendo trocadas (sugestao e aceite):
--   esse pedido nao conta contra si mesmo e o periodo aquisitivo e o dele, fixo.
-- A API devolve a lista de todas as regras; aqui a PRIMEIRA que falha recusa, nesta ordem:
--   1. entradas: datas obrigatorias, entre 2000 e 2100, fim nao antes do inicio; abono de 1 a 10 dias, so com o abono marcado;
--   2. a pessoa existe;
--   3. pedido novo: ha periodo aquisitivo fechado (um ano de casa completo). Os periodos contam da admissao, de ano em ano;
--   4. DURACAO_DA_PARTE: de 5 a 30 dias corridos;
--   5. CABE_NO_SALDO: entra no periodo fechado MAIS ANTIGO, que termina antes do inicio, em que cabe inteiro (gozo + abono). Livre = 30 menos
--      o tirado (APROVADA, EM_GOZO, CONCLUIDA) menos o reservado (SOLICITADA que nao espera resposta de sugestao);
--   6. PARTE_DE_14_DIAS: uma das partes do periodo tem de ter 14 dias (esta, outra que ja existe, ou ainda sobra para uma);
--   7. MAXIMO_DE_3_PARTES por periodo aquisitivo;
--   8. ANTECEDENCIA_DE_30_DIAS (so com P_EXIGE_ANTECEDENCIA);
--   9. NAO_COMECA_PERTO_DE_FERIADO_OU_DESCANSO: nao comeca em feriado, sabado ou domingo, nem nos 2 dias antes de feriado ou de sabado
--      (feriados ativos que valem para a empresa: NACIONAL, EMPRESA, ESTADUAL da UF e MUNICIPAL do municipio de CONFIGURACOES_EMPRESA);
--  10. SEM_SOBREPOSICAO com outras ferias da pessoa (tiradas ou em pedido).
-- Em relacao ao arquivo antigo: nao existia; a antiga SP_SOLICITAR_FERIAS so conferia a duracao e a sobreposicao.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CONFERIR_FERIAS(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA_INICIO DATE,
    IN P_DATA_FIM DATE,
    IN P_ABONO_PECUNIARIO BOOLEAN,
    IN P_DIAS_ABONO TINYINT,
    IN P_EXIGE_ANTECEDENCIA BOOLEAN,
    IN P_ID_FERIAS INT,
    OUT P_DATA_INICIO_AQUISITIVO DATE,
    OUT P_DATA_FIM_AQUISITIVO DATE
)
BEGIN
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_DESLIGAMENTO DATE DEFAULT NULL;
    DECLARE V_HOJE DATE DEFAULT CURDATE();
    DECLARE V_ABONO BOOLEAN DEFAULT COALESCE(P_ABONO_PECUNIARIO, FALSE);
    DECLARE V_DIAS_ABONO INT DEFAULT COALESCE(P_DIAS_ABONO, 0);
    DECLARE V_GOZO INT DEFAULT 0;
    DECLARE V_CONSUMO INT DEFAULT 0;
    DECLARE V_FIXO_INICIO DATE DEFAULT NULL;
    DECLARE V_N INT DEFAULT 0;
    DECLARE V_FECHADOS INT DEFAULT 0;
    DECLARE V_P_INICIO DATE;
    DECLARE V_P_FIM DATE;
    DECLARE V_TIRADOS INT;
    DECLARE V_EM_PEDIDO INT;
    DECLARE V_DISPONIVEIS INT;
    DECLARE V_ORDEM INT;
    DECLARE V_REF_ORDEM INT DEFAULT 99;
    DECLARE V_REF_INICIO DATE DEFAULT NULL;
    DECLARE V_REF_FIM DATE DEFAULT NULL;
    DECLARE V_REF_LIVRE INT DEFAULT 0;
    DECLARE V_SOBRAM INT;
    DECLARE V_PARTES INT DEFAULT 0;
    DECLARE V_MAIOR_PARTE INT DEFAULT 0;
    DECLARE V_UF CHAR(2) DEFAULT NULL;
    DECLARE V_MUNICIPIO VARCHAR(100) DEFAULT NULL;
    DECLARE V_QTD INT DEFAULT 0;
    DECLARE V_MSG VARCHAR(128);

    SET P_DATA_INICIO_AQUISITIVO = NULL;
    SET P_DATA_FIM_AQUISITIVO = NULL;

    IF P_DATA_INICIO IS NULL OR P_DATA_FIM IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim das ferias.';
    END IF;

    IF YEAR(P_DATA_INICIO) NOT BETWEEN 2000 AND 2100 OR YEAR(P_DATA_FIM) NOT BETWEEN 2000 AND 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe datas entre 2000 e 2100.';
    END IF;

    IF P_DATA_FIM < P_DATA_INICIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data final nao pode ser antes da inicial.';
    END IF;

    IF (V_ABONO = FALSE AND V_DIAS_ABONO <> 0) OR (V_ABONO = TRUE AND V_DIAS_ABONO NOT BETWEEN 1 AND 10) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O abono e de 1 a 10 dias, e so com o abono pecuniario marcado.';
    END IF;

    SELECT DATA_ADMISSAO, DATA_DESLIGAMENTO
      INTO V_ADMISSAO, V_DESLIGAMENTO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;

    IF V_ADMISSAO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF P_ID_FERIAS IS NOT NULL THEN
        SELECT DATA_INICIO_PERIODO_AQUISITIVO INTO V_FIXO_INICIO
          FROM FERIAS
         WHERE ID_FERIAS = P_ID_FERIAS
           AND ID_FUNCIONARIO = P_ID_FUNCIONARIO;

        IF V_FIXO_INICIO IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado para esta pessoa.';
        END IF;
    END IF;

    SET V_GOZO = DATEDIFF(P_DATA_FIM, P_DATA_INICIO) + 1;
    SET V_CONSUMO = V_GOZO + V_DIAS_ABONO;

    -- Os periodos aquisitivos FECHADOS, do mais antigo ao mais novo: o n-esimo comeca em admissao + n anos (sempre da admissao original) e
    -- termina na vespera do seguinte; so esta fechado depois do ultimo dia; o desligado so tem os que fecharam ate o desligamento.
    periodos: LOOP
        SET V_P_INICIO = DATE_ADD(V_ADMISSAO, INTERVAL V_N YEAR);
        SET V_P_FIM = DATE_SUB(DATE_ADD(V_ADMISSAO, INTERVAL V_N + 1 YEAR), INTERVAL 1 DAY);

        IF V_P_FIM >= V_HOJE OR (V_DESLIGAMENTO IS NOT NULL AND V_P_FIM > V_DESLIGAMENTO) THEN
            LEAVE periodos;
        END IF;

        SET V_FECHADOS = V_FECHADOS + 1;

        IF V_FIXO_INICIO IS NULL OR V_FIXO_INICIO = V_P_INICIO THEN
            -- Cada linha de FERIAS pertence ao periodo de datas EXATAMENTE iguais (as ferias de um vinculo antigo nao entram em nenhum).
            SELECT COALESCE(SUM(CASE WHEN STATUS_FERIAS IN ('APROVADA', 'EM_GOZO', 'CONCLUIDA')
                                     THEN DATEDIFF(DATA_FIM, DATA_INICIO) + 1 + DIAS_ABONO ELSE 0 END), 0),
                   COALESCE(SUM(CASE WHEN STATUS_FERIAS = 'SOLICITADA'
                                          AND NOT (COALESCE(RESULTADO_GESTOR, '') = 'SUGERIU_DATA' AND RESPOSTA_SUGESTAO IS NULL)
                                     THEN DATEDIFF(DATA_FIM, DATA_INICIO) + 1 + DIAS_ABONO ELSE 0 END), 0)
              INTO V_TIRADOS, V_EM_PEDIDO
              FROM FERIAS
             WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
               AND DATA_INICIO_PERIODO_AQUISITIVO = V_P_INICIO
               AND DATA_FIM_PERIODO_AQUISITIVO = V_P_FIM
               AND (P_ID_FERIAS IS NULL OR ID_FERIAS <> P_ID_FERIAS);

            SET V_DISPONIVEIS = GREATEST(0, 30 - V_TIRADOS);

            -- Qual periodo vale: 0 = o escolhido (o do pedido que ja existe; ou o mais antigo que termina antes do inicio e comporta o pedido);
            -- senao o de referencia dos numeros: 1 = o mais antigo candidato com saldo, 2 = o mais antigo candidato, 3 = o mais antigo fechado.
            SET V_ORDEM = CASE
                WHEN V_FIXO_INICIO IS NOT NULL THEN 0
                WHEN V_P_FIM < P_DATA_INICIO AND V_DISPONIVEIS - V_EM_PEDIDO >= V_CONSUMO THEN 0
                WHEN V_P_FIM < P_DATA_INICIO AND V_DISPONIVEIS > 0 THEN 1
                WHEN V_P_FIM < P_DATA_INICIO THEN 2
                ELSE 3
            END;

            IF V_ORDEM < V_REF_ORDEM THEN
                SET V_REF_ORDEM = V_ORDEM;
                SET V_REF_INICIO = V_P_INICIO;
                SET V_REF_FIM = V_P_FIM;
                SET V_REF_LIVRE = V_DISPONIVEIS - V_EM_PEDIDO;
            END IF;

            IF V_ORDEM = 0 THEN
                LEAVE periodos;
            END IF;
        END IF;

        SET V_N = V_N + 1;
    END LOOP;

    IF P_ID_FERIAS IS NULL AND V_FECHADOS = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A pessoa ainda nao completou um ano de casa: nao ha periodo aquisitivo fechado.';
    END IF;

    IF V_GOZO NOT BETWEEN 5 AND 30 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A parte precisa ter entre 5 e 30 dias.';
    END IF;

    IF V_REF_INICIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O periodo aquisitivo do pedido nao e um periodo fechado da pessoa.';
    END IF;

    IF V_REF_FIM >= P_DATA_INICIO THEN
        SET V_MSG = CONCAT('As ferias precisam comecar depois do fim do periodo aquisitivo (', DATE_FORMAT(V_REF_FIM, '%d/%m/%Y'), ').');
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = V_MSG;
    END IF;

    SET V_SOBRAM = V_REF_LIVRE - V_CONSUMO;

    IF V_SOBRAM < 0 THEN
        SET V_MSG = IF(V_SOBRAM = -1, 'Passa do saldo disponivel: falta 1 dia.', CONCAT('Passa do saldo disponivel: faltam ', -V_SOBRAM, ' dias.'));
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = V_MSG;
    END IF;

    -- As partes que ja contam no periodo (tiradas ou em pedido) e a maior delas, em dias de gozo.
    SELECT COUNT(*), COALESCE(MAX(DATEDIFF(DATA_FIM, DATA_INICIO) + 1), 0)
      INTO V_PARTES, V_MAIOR_PARTE
      FROM FERIAS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND DATA_INICIO_PERIODO_AQUISITIVO = V_REF_INICIO
       AND DATA_FIM_PERIODO_AQUISITIVO = V_REF_FIM
       AND (P_ID_FERIAS IS NULL OR ID_FERIAS <> P_ID_FERIAS)
       AND (STATUS_FERIAS IN ('APROVADA', 'EM_GOZO', 'CONCLUIDA')
            OR (STATUS_FERIAS = 'SOLICITADA' AND NOT (COALESCE(RESULTADO_GESTOR, '') = 'SUGERIU_DATA' AND RESPOSTA_SUGESTAO IS NULL)));

    IF V_GOZO < 14 AND V_MAIOR_PARTE < 14 AND NOT (V_PARTES + 1 < 3 AND V_SOBRAM >= 14) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Depois deste pedido ainda precisa dar para ter uma parte de 14 dias seguidos.';
    END IF;

    IF V_PARTES + 1 > 3 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O periodo aquisitivo ja tem 3 partes.';
    END IF;

    IF COALESCE(P_EXIGE_ANTECEDENCIA, FALSE) = TRUE AND P_DATA_INICIO < DATE_ADD(V_HOJE, INTERVAL 30 DAY) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Precisa comecar pelo menos 30 dias depois de hoje.';
    END IF;

    -- Comecar na quinta ou na sexta e comecar nos 2 dias antes do sabado (WEEKDAY: 0 = segunda ... 5 = sabado, 6 = domingo).
    SELECT UF, MUNICIPIO INTO V_UF, V_MUNICIPIO
      FROM CONFIGURACOES_EMPRESA
     WHERE ID_CONFIGURACAO_EMPRESA = 1;

    SELECT COUNT(*) INTO V_QTD
      FROM FERIADOS
     WHERE ATIVO = TRUE
       AND DATA_FERIADO BETWEEN P_DATA_INICIO AND DATE_ADD(P_DATA_INICIO, INTERVAL 2 DAY)
       AND (TIPO_FERIADO IN ('NACIONAL', 'EMPRESA')
            OR (TIPO_FERIADO = 'ESTADUAL' AND UF = V_UF)
            OR (TIPO_FERIADO = 'MUNICIPAL' AND UF = V_UF AND MUNICIPIO = V_MUNICIPIO));

    IF WEEKDAY(P_DATA_INICIO) >= 3 OR V_QTD > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao pode comecar em feriado, no fim de semana nem nos 2 dias antes deles.';
    END IF;

    SELECT COUNT(*) INTO V_QTD
      FROM FERIAS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND (P_ID_FERIAS IS NULL OR ID_FERIAS <> P_ID_FERIAS)
       AND (STATUS_FERIAS IN ('APROVADA', 'EM_GOZO', 'CONCLUIDA')
            OR (STATUS_FERIAS = 'SOLICITADA' AND NOT (COALESCE(RESULTADO_GESTOR, '') = 'SUGERIU_DATA' AND RESPOSTA_SUGESTAO IS NULL)))
       AND DATA_INICIO <= P_DATA_FIM
       AND DATA_FIM >= P_DATA_INICIO;

    IF V_QTD > 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Coincide com outras ferias da pessoa.';
    END IF;

    SET P_DATA_INICIO_AQUISITIVO = V_REF_INICIO;
    SET P_DATA_FIM_AQUISITIVO = V_REF_FIM;
END$$

-- ------------------------------------------------------------
-- SP_SOLICITAR_FERIAS: a pessoa pede as proprias ferias; o pedido nasce SOLICITADA.
-- Espelha: pedirFerias (POST /api/eu/ferias, PedirFeriasCasoDeUso).
-- Recusas, em ordem:
--   1. entradas (antes da transacao): datas, abono (ver SP_CONFERIR_FERIAS) e a mensagem, de ate 500 caracteres;
--   2. a pessoa existe e esta ativa (travada com FOR UPDATE: dois pedidos ao mesmo tempo entram em fila);
--   3. todas as regras de SP_CONFERIR_FERIAS, COM a antecedencia de 30 dias.
-- Quem decide primeiro (IGestorAusente.EtapaInicialDoPedidoAsync), gravado na criacao:
--   * sem etapa do gestor (ID_GESTOR nulo, so o RH decide): a pessoa e gestor (linha em GESTORES e gerencia um setor, ou tem o nivel RH),
--     ou nao tem setor, ou o setor nao tem gestor (SETORES.ID_GESTOR), ou o gestor do setor esta inativo;
--   * gestor do setor ausente hoje (GESTORES.APROVACAO_AUTOMATICA ligada e, se ha APROVACAO_AUTOMATICA_ATE, hoje antes dela): ID_GESTOR dele
--     e a etapa do gestor ja nasce aprovada (RESULTADO_GESTOR = APROVOU, ETAPA_GESTOR_AUTOMATICA); o pedido ja esta com o RH;
--   * senao: ID_GESTOR do gestor do setor, esperando a decisao dele.
-- A API faz e a procedure nao: o evento de auditoria (nenhum e-mail sai ao pedir).
-- Em relacao ao arquivo antigo:
--   * P_DATA_INICIO_AQUISITIVO e P_DATA_FIM_AQUISITIVO sairam: quem escolhe o periodo aquisitivo e a regra (o mais antigo com saldo), nao
--     quem chama; a antiga aceitava qualquer periodo inventado;
--   * entrou P_MENSAGEM (a mensagem da pessoa, em OBSERVACAO);
--   * a antiga nao conferia saldo, fracionamento, antecedencia nem feriado, nao gravava o gestor da etapa (o pedido nascia sem etapa do
--     gestor para todo mundo) e ignorava o gestor ausente; a sobreposicao contava ate pedido que espera resposta de sugestao.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SOLICITAR_FERIAS(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA_INICIO DATE,
    IN P_DATA_FIM DATE,
    IN P_ABONO_PECUNIARIO BOOLEAN,
    IN P_DIAS_ABONO TINYINT,
    IN P_ADIANTAR_13 BOOLEAN,
    IN P_MENSAGEM TEXT,
    OUT P_ID_FERIAS INT
)
BEGIN
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_SETOR INT DEFAULT NULL;
    DECLARE V_ID_NIVEL INT DEFAULT NULL;
    DECLARE V_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_E_GESTOR INT DEFAULT 0;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;
    DECLARE V_GESTOR_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_AUSENTE BOOLEAN DEFAULT FALSE;
    DECLARE V_AUSENTE_ATE DATE DEFAULT NULL;
    DECLARE V_ABONO BOOLEAN DEFAULT COALESCE(P_ABONO_PECUNIARIO, FALSE);
    DECLARE V_DIAS_ABONO INT DEFAULT COALESCE(P_DIAS_ABONO, 0);
    DECLARE V_MENSAGEM TEXT DEFAULT NULLIF(REGEXP_REPLACE(P_MENSAGEM, '^[[:space:]]+|[[:space:]]+$', ''), '');
    DECLARE V_PA_INICIO DATE DEFAULT NULL;
    DECLARE V_PA_FIM DATE DEFAULT NULL;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_FERIAS = NULL;

    IF P_DATA_INICIO IS NULL OR P_DATA_FIM IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim das ferias.';
    END IF;

    IF YEAR(P_DATA_INICIO) NOT BETWEEN 2000 AND 2100 OR YEAR(P_DATA_FIM) NOT BETWEEN 2000 AND 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe datas entre 2000 e 2100.';
    END IF;

    IF P_DATA_FIM < P_DATA_INICIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data final nao pode ser antes da inicial.';
    END IF;

    IF (V_ABONO = FALSE AND V_DIAS_ABONO <> 0) OR (V_ABONO = TRUE AND V_DIAS_ABONO NOT BETWEEN 1 AND 10) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O abono e de 1 a 10 dias, e so com o abono pecuniario marcado.';
    END IF;

    IF CHAR_LENGTH(V_MENSAGEM) > 500 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A mensagem pode ter ate 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO, ID_SETOR, ID_NIVEL_ACESSO
      INTO V_ATIVO, V_ID_SETOR, V_ID_NIVEL
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    -- A API recusa a pessoa inativa ANTES de conferir as regras; o trigger TRG_FERIAS_BEFORE_INSERT so recusaria no INSERT, com este mesmo texto.
    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario inativo nao pode solicitar ferias.';
    END IF;

    CALL SP_CONFERIR_FERIAS(P_ID_FUNCIONARIO, P_DATA_INICIO, P_DATA_FIM, V_ABONO, V_DIAS_ABONO, TRUE, NULL, V_PA_INICIO, V_PA_FIM);

    -- Quem decide primeiro.
    SELECT NOME_NIVEL_ACESSO INTO V_NIVEL
      FROM NIVEIS_DE_ACESSO
     WHERE ID_NIVEL_ACESSO = V_ID_NIVEL;

    SELECT COUNT(*) INTO V_E_GESTOR
      FROM GESTORES G
     WHERE G.ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND (COALESCE(V_NIVEL, '') = 'RH' OR EXISTS (SELECT 1 FROM SETORES S WHERE S.ID_GESTOR = G.ID_GESTOR));

    IF V_E_GESTOR = 0 AND V_ID_SETOR IS NOT NULL THEN
        SELECT G.ID_GESTOR, F.ATIVO, G.APROVACAO_AUTOMATICA, G.APROVACAO_AUTOMATICA_ATE
          INTO V_ID_GESTOR, V_GESTOR_ATIVO, V_AUSENTE, V_AUSENTE_ATE
          FROM SETORES S
          JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
          JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
         WHERE S.ID_SETOR = V_ID_SETOR;

        IF V_ID_GESTOR IS NOT NULL AND V_GESTOR_ATIVO = FALSE THEN
            SET V_ID_GESTOR = NULL;
        END IF;
    END IF;

    SET V_AUSENTE = V_ID_GESTOR IS NOT NULL AND V_AUSENTE = TRUE AND (V_AUSENTE_ATE IS NULL OR CURDATE() < V_AUSENTE_ATE);

    INSERT INTO FERIAS (
        ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO,
        DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO,
        STATUS_FERIAS, DATA_SOLICITACAO, ADIANTAR_13_SALARIO, OBSERVACAO,
        ID_GESTOR, RESULTADO_GESTOR, DATA_ETAPA_GESTOR, ETAPA_GESTOR_AUTOMATICA
    )
    VALUES (
        P_ID_FUNCIONARIO, V_PA_INICIO, V_PA_FIM,
        P_DATA_INICIO, P_DATA_FIM, V_ABONO, V_DIAS_ABONO,
        'SOLICITADA', V_AGORA, COALESCE(P_ADIANTAR_13, FALSE), V_MENSAGEM,
        V_ID_GESTOR, IF(V_AUSENTE, 'APROVOU', NULL), IF(V_AUSENTE, V_AGORA, NULL), V_AUSENTE
    );

    SET P_ID_FERIAS = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_REGISTRAR_FERIAS_RH: o RH registra ferias direto; elas ja nascem APROVADA, sem as duas etapas (ID_GESTOR, RESULTADO_GESTOR,
-- ID_FUNCIONARIO_RH e as datas de decisao ficam nulos, como a API grava), ate no passado.
-- Espelha: registrarFeriasPeloRh (POST /api/rh/funcionarios/{funcionarioId}/ferias, RegistrarFeriasPeloRhCasoDeUso).
-- Recusas, em ordem: as mesmas de SP_SOLICITAR_FERIAS (entradas; pessoa existe e esta ativa; regras), SEM a antecedencia de 30 dias.
-- So entram os periodos aquisitivos que terminam antes do inicio das ferias.
-- A API faz e a procedure nao: o evento de auditoria (a API tambem nao grava em FERIAS QUEM do RH registrou, so no log de auditoria; por
-- isso nao ha parametro do RH) e, no registro de dias que ja passaram, o recalculo do banco de horas desses dias depois do commit (o dia
-- consolidado como falta deixa de ser falta). Quem registrar ferias no passado pela procedure precisa refazer o banco de horas desses dias.
-- Em relacao ao arquivo antigo: nao existia. Sem ela, pela interface do banco, o RH so conseguiria confirmar um pedido da pessoa.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_REGISTRAR_FERIAS_RH(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA_INICIO DATE,
    IN P_DATA_FIM DATE,
    IN P_ABONO_PECUNIARIO BOOLEAN,
    IN P_DIAS_ABONO TINYINT,
    IN P_ADIANTAR_13 BOOLEAN,
    IN P_OBSERVACAO TEXT,
    OUT P_ID_FERIAS INT
)
BEGIN
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ABONO BOOLEAN DEFAULT COALESCE(P_ABONO_PECUNIARIO, FALSE);
    DECLARE V_DIAS_ABONO INT DEFAULT COALESCE(P_DIAS_ABONO, 0);
    DECLARE V_OBSERVACAO TEXT DEFAULT NULLIF(REGEXP_REPLACE(P_OBSERVACAO, '^[[:space:]]+|[[:space:]]+$', ''), '');
    DECLARE V_PA_INICIO DATE DEFAULT NULL;
    DECLARE V_PA_FIM DATE DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_FERIAS = NULL;

    IF P_DATA_INICIO IS NULL OR P_DATA_FIM IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim das ferias.';
    END IF;

    IF YEAR(P_DATA_INICIO) NOT BETWEEN 2000 AND 2100 OR YEAR(P_DATA_FIM) NOT BETWEEN 2000 AND 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe datas entre 2000 e 2100.';
    END IF;

    IF P_DATA_FIM < P_DATA_INICIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data final nao pode ser antes da inicial.';
    END IF;

    IF (V_ABONO = FALSE AND V_DIAS_ABONO <> 0) OR (V_ABONO = TRUE AND V_DIAS_ABONO NOT BETWEEN 1 AND 10) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O abono e de 1 a 10 dias, e so com o abono pecuniario marcado.';
    END IF;

    IF CHAR_LENGTH(V_OBSERVACAO) > 500 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A observacao pode ter ate 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    -- Como em SP_SOLICITAR_FERIAS: a API recusa a pessoa inativa antes das regras, com o texto do trigger.
    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario inativo nao pode solicitar ferias.';
    END IF;

    CALL SP_CONFERIR_FERIAS(P_ID_FUNCIONARIO, P_DATA_INICIO, P_DATA_FIM, V_ABONO, V_DIAS_ABONO, FALSE, NULL, V_PA_INICIO, V_PA_FIM);

    INSERT INTO FERIAS (
        ID_FUNCIONARIO, DATA_INICIO_PERIODO_AQUISITIVO, DATA_FIM_PERIODO_AQUISITIVO,
        DATA_INICIO, DATA_FIM, ABONO_PECUNIARIO, DIAS_ABONO,
        STATUS_FERIAS, DATA_SOLICITACAO, ADIANTAR_13_SALARIO, OBSERVACAO
    )
    VALUES (
        P_ID_FUNCIONARIO, V_PA_INICIO, V_PA_FIM,
        P_DATA_INICIO, P_DATA_FIM, V_ABONO, V_DIAS_ABONO,
        'APROVADA', NOW(), COALESCE(P_ADIANTAR_13, FALSE), V_OBSERVACAO
    );

    SET P_ID_FERIAS = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_DECIDIR_FERIAS_GESTOR: a decisao do gestor na etapa dele. P_RESULTADO: APROVOU, RECUSOU ou SUGERIU_DATA.
-- Espelha: aprovarFeriasComoGestor, recusarFeriasComoGestor e sugerirOutraDataDasFerias (POST /api/gestor/ferias/{feriasId}/aprovar,
-- /recusar e /sugerir-outra-data; DecidirFeriasComoGestorCasoDeUso e SugerirOutraDataDasFeriasCasoDeUso).
--   APROVOU      : grava o resultado e a data da etapa; o status continua SOLICITADA e o pedido passa ao RH (so o RH aprova de vez);
--   RECUSOU      : encerra o pedido (REJEITADA) com o motivo em MOTIVO_RECUSA; P_OBSERVACAO e o motivo, de 5 a 500 caracteres;
--   SUGERIU_DATA : grava as datas sugeridas; o pedido fica esperando a resposta da PESSOA (SP_RESPONDER_SUGESTAO_FERIAS).
-- Recusas, em ordem:
--   1. entradas (antes da transacao): resultado; motivo so (e sempre) na recusa; datas so (e sempre) na sugestao, validas como em
--      SP_CONFERIR_FERIAS;
--   2. o pedido existe; o gestor existe, esta ativo e gerencia um setor (SETORES.ID_GESTOR; com mais de um, vale o de menor id);
--   3. o pedido e da equipe dele: tem o ID_GESTOR dele E a pessoa esta hoje, ativa e ja admitida, no setor dele (o pedido do proprio gestor,
--      de outro gestor ou de outro setor "nao existe" para ele);
--   4. a etapa: so o pedido que esta com o gestor; com o RH ou esperando a resposta da pessoa = etapa errada; terminado = ja decidido;
--   5. so na sugestao: comeca depois do fim do periodo aquisitivo do pedido, nao antes de hoje, e cumpre as regras de SP_CONFERIR_FERIAS
--      com o abono do pedido, SEM a antecedencia e sem contar o proprio pedido (senao a pessoa nunca conseguiria aceitar).
-- A API faz e a procedure nao: conferir o perfil Gestor de quem chama, o e-mail da decisao a pessoa e o evento de auditoria.
-- Em relacao ao arquivo antigo (mesmos parametros, na mesma ordem):
--   * a antiga gravava P_OBSERVACAO em OBSERVACAO, apagando a mensagem da pessoa; agora o motivo vai so em MOTIVO_RECUSA;
--   * a antiga conferia o gestor por FUNCIONARIOS.ID_GESTOR; na API a equipe e o SETOR do gestor (SETORES.ID_GESTOR) e o pedido precisa ter
--     nascido com o ID_GESTOR dele (um pedido sem etapa do gestor nao e dele);
--   * a antiga aceitava decidir de novo um pedido ja aprovado pelo gestor ou com sugestao pendente (so olhava SOLICITADA);
--   * a recusa nao gravava DATA_ANALISE; o motivo nao tinha tamanho minimo; a sugestao nao conferia nenhuma regra alem da duracao.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_DECIDIR_FERIAS_GESTOR(
    IN P_ID_FERIAS INT,
    IN P_ID_GESTOR INT,
    IN P_RESULTADO VARCHAR(20),
    IN P_OBSERVACAO TEXT,
    IN P_SUGESTAO_INICIO DATE,
    IN P_SUGESTAO_FIM DATE
)
BEGIN
    DECLARE V_RESULTADO VARCHAR(20) DEFAULT UPPER(TRIM(COALESCE(P_RESULTADO, '')));
    DECLARE V_MOTIVO TEXT DEFAULT NULLIF(REGEXP_REPLACE(P_OBSERVACAO, '^[[:space:]]+|[[:space:]]+$', ''), '');
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_PESSOA_ATIVA BOOLEAN DEFAULT NULL;
    DECLARE V_PESSOA_SETOR INT DEFAULT NULL;
    DECLARE V_PESSOA_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;
    DECLARE V_PEDIDO_GESTOR INT DEFAULT NULL;
    DECLARE V_PEDIDO_RESULTADO VARCHAR(20) DEFAULT NULL;
    DECLARE V_PEDIDO_RESPOSTA VARCHAR(10) DEFAULT NULL;
    DECLARE V_PA_FIM DATE DEFAULT NULL;
    DECLARE V_ABONO BOOLEAN DEFAULT FALSE;
    DECLARE V_DIAS_ABONO INT DEFAULT 0;
    DECLARE V_GESTOR_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_GESTOR_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_GESTOR_SETOR INT DEFAULT NULL;
    DECLARE V_SITUACAO VARCHAR(10);
    DECLARE V_IGNORADA_1 DATE;
    DECLARE V_IGNORADA_2 DATE;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF V_RESULTADO NOT IN ('APROVOU', 'RECUSOU', 'SUGERIU_DATA') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Resultado do gestor invalido: use APROVOU, RECUSOU ou SUGERIU_DATA.';
    END IF;

    IF V_RESULTADO = 'RECUSOU' AND (V_MOTIVO IS NULL OR CHAR_LENGTH(V_MOTIVO) NOT BETWEEN 5 AND 500) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O motivo da recusa tem de 5 a 500 caracteres.';
    END IF;

    IF V_RESULTADO <> 'RECUSOU' AND V_MOTIVO IS NOT NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A observacao e o motivo da recusa: so se informa com RECUSOU.';
    END IF;

    IF V_RESULTADO <> 'SUGERIU_DATA' AND (P_SUGESTAO_INICIO IS NOT NULL OR P_SUGESTAO_FIM IS NOT NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'As datas sugeridas so se informam com SUGERIU_DATA.';
    END IF;

    IF V_RESULTADO = 'SUGERIU_DATA' THEN
        IF P_SUGESTAO_INICIO IS NULL OR P_SUGESTAO_FIM IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim sugeridas.';
        END IF;

        IF YEAR(P_SUGESTAO_INICIO) NOT BETWEEN 2000 AND 2100 OR YEAR(P_SUGESTAO_FIM) NOT BETWEEN 2000 AND 2100 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe datas entre 2000 e 2100.';
        END IF;

        IF P_SUGESTAO_FIM < P_SUGESTAO_INICIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data final nao pode ser antes da inicial.';
        END IF;
    END IF;

    -- A pessoa do pedido e lida antes da transacao, so para trava-la primeiro (a ordem das travas); o pedido e relido ja travado.
    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado na equipe deste gestor.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO, ID_SETOR, DATA_ADMISSAO
      INTO V_PESSOA_ATIVA, V_PESSOA_SETOR, V_PESSOA_ADMISSAO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_FERIAS, ID_GESTOR, RESULTADO_GESTOR, RESPOSTA_SUGESTAO, DATA_FIM_PERIODO_AQUISITIVO, ABONO_PECUNIARIO, DIAS_ABONO
      INTO V_STATUS, V_PEDIDO_GESTOR, V_PEDIDO_RESULTADO, V_PEDIDO_RESPOSTA, V_PA_FIM, V_ABONO, V_DIAS_ABONO
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS
     FOR UPDATE;

    SELECT G.ID_FUNCIONARIO, F.ATIVO
      INTO V_GESTOR_FUNCIONARIO, V_GESTOR_ATIVO
      FROM GESTORES G
      JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
     WHERE G.ID_GESTOR = P_ID_GESTOR;

    SELECT MIN(ID_SETOR) INTO V_GESTOR_SETOR
      FROM SETORES
     WHERE ID_GESTOR = P_ID_GESTOR;

    IF V_GESTOR_FUNCIONARIO IS NULL OR V_GESTOR_ATIVO = FALSE OR V_GESTOR_SETOR IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Gestor inexistente, inativo ou sem setor.';
    END IF;

    IF NOT (V_PEDIDO_GESTOR <=> P_ID_GESTOR)
       OR V_PESSOA_ATIVA = FALSE
       OR V_PESSOA_ADMISSAO > CURDATE()
       OR NOT (V_PESSOA_SETOR <=> V_GESTOR_SETOR)
       OR V_ID_FUNCIONARIO = V_GESTOR_FUNCIONARIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado na equipe deste gestor.';
    END IF;

    SET V_SITUACAO = CASE
        WHEN V_STATUS <> 'SOLICITADA' THEN 'FINAL'
        WHEN V_PEDIDO_RESULTADO = 'SUGERIU_DATA' AND V_PEDIDO_RESPOSTA IS NULL THEN 'SUGESTAO'
        WHEN V_PEDIDO_GESTOR IS NOT NULL AND V_PEDIDO_RESULTADO IS NULL THEN 'GESTOR'
        ELSE 'RH'
    END;

    IF V_SITUACAO = 'FINAL' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de ferias ja foi decidido.';
    END IF;

    IF V_SITUACAO <> 'GESTOR' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido na etapa errada: ele nao esta esperando a decisao do gestor.';
    END IF;

    IF V_RESULTADO = 'APROVOU' THEN
        UPDATE FERIAS
           SET RESULTADO_GESTOR = 'APROVOU',
               DATA_ETAPA_GESTOR = V_AGORA
         WHERE ID_FERIAS = P_ID_FERIAS;
    ELSEIF V_RESULTADO = 'RECUSOU' THEN
        UPDATE FERIAS
           SET STATUS_FERIAS = 'REJEITADA',
               RESULTADO_GESTOR = 'RECUSOU',
               DATA_ETAPA_GESTOR = V_AGORA,
               DATA_ANALISE = V_AGORA,
               MOTIVO_RECUSA = V_MOTIVO
         WHERE ID_FERIAS = P_ID_FERIAS;
    ELSE
        IF P_SUGESTAO_INICIO <= V_PA_FIM THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A sugestao precisa comecar depois do fim do periodo aquisitivo do pedido.';
        END IF;

        IF P_SUGESTAO_INICIO < CURDATE() THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A sugestao nao pode comecar antes de hoje.';
        END IF;

        CALL SP_CONFERIR_FERIAS(V_ID_FUNCIONARIO, P_SUGESTAO_INICIO, P_SUGESTAO_FIM, V_ABONO, V_DIAS_ABONO, FALSE, P_ID_FERIAS, V_IGNORADA_1, V_IGNORADA_2);

        UPDATE FERIAS
           SET RESULTADO_GESTOR = 'SUGERIU_DATA',
               DATA_ETAPA_GESTOR = V_AGORA,
               SUGESTAO_DATA_INICIO = P_SUGESTAO_INICIO,
               SUGESTAO_DATA_FIM = P_SUGESTAO_FIM
         WHERE ID_FERIAS = P_ID_FERIAS;
    END IF;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_RESPONDER_SUGESTAO_FERIAS: a resposta da PESSOA a sugestao de outra data do gestor. P_RESPOSTA: ACEITOU ou RECUSOU.
-- Espelha: aceitarSugestaoDeDataDasFerias e recusarSugestaoDeDataDasFerias (POST /api/eu/ferias/{feriasId}/sugestao/aceitar e /recusar;
-- ResponderASugestaoDeFeriasCasoDeUso).
--   ACEITOU : as datas sugeridas passam a ser as do pedido (as sugeridas continuam gravadas; as de antes so ficam no log de auditoria da
--             API), o pedido continua SOLICITADA e vai para o RH;
--   RECUSOU : o pedido e cancelado (CANCELADA).
-- Recusas, em ordem:
--   1. entrada: a resposta;
--   2. o pedido existe e e da pessoa informada (o de outra pessoa "nao existe");
--   3. a etapa: so o pedido que espera a resposta (SUGERIU_DATA sem resposta); ainda com o gestor ou ja com o RH = etapa errada;
--      terminado = ja decidido;
--   4. o prazo: o ultimo dia para responder e o menor entre 5 dias corridos depois do dia da sugestao e 2 dias antes do inicio sugerido.
--      Passado o prazo, o pedido EXPIRA aqui mesmo (CANCELADA, RESPOSTA_SUGESTAO = EXPIROU), isso fica GRAVADO, e a chamada termina com a
--      recusa (e o que a API faz quando a resposta chega antes da tarefa diaria);
--   5. so no aceite: as regras de SP_CONFERIR_FERIAS com as datas sugeridas e o abono do pedido, SEM a antecedencia e sem contar o proprio
--      pedido (e agora que esses dias passam a ser reservados no saldo).
-- A API faz e a procedure nao: o evento de auditoria (com as datas de antes e de depois); nenhum e-mail sai. A expiracao de quem nunca
-- responde e da tarefa diaria ATUALIZAR_FERIAS.
-- Em relacao ao arquivo antigo: nao existia; a antiga SP_DECIDIR_FERIAS_GESTOR gravava a sugestao e nada mais mexia no pedido.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_RESPONDER_SUGESTAO_FERIAS(
    IN P_ID_FERIAS INT,
    IN P_ID_FUNCIONARIO INT,
    IN P_RESPOSTA VARCHAR(10)
)
BEGIN
    DECLARE V_RESPOSTA VARCHAR(10) DEFAULT UPPER(TRIM(COALESCE(P_RESPOSTA, '')));
    DECLARE V_EXISTE INT DEFAULT NULL;
    DECLARE V_DONO INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;
    DECLARE V_PEDIDO_GESTOR INT DEFAULT NULL;
    DECLARE V_PEDIDO_RESULTADO VARCHAR(20) DEFAULT NULL;
    DECLARE V_PEDIDO_RESPOSTA VARCHAR(10) DEFAULT NULL;
    DECLARE V_SUGESTAO_INICIO DATE DEFAULT NULL;
    DECLARE V_SUGESTAO_FIM DATE DEFAULT NULL;
    DECLARE V_SUGERIDA_EM DATETIME DEFAULT NULL;
    DECLARE V_ABONO BOOLEAN DEFAULT FALSE;
    DECLARE V_DIAS_ABONO INT DEFAULT 0;
    DECLARE V_SITUACAO VARCHAR(10);
    DECLARE V_IGNORADA_1 DATE;
    DECLARE V_IGNORADA_2 DATE;
    DECLARE V_EXPIROU BOOLEAN DEFAULT FALSE;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF V_RESPOSTA NOT IN ('ACEITOU', 'RECUSOU') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Resposta invalida: use ACEITOU ou RECUSOU.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_EXISTE
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT ID_FUNCIONARIO, STATUS_FERIAS, ID_GESTOR, RESULTADO_GESTOR, RESPOSTA_SUGESTAO,
           SUGESTAO_DATA_INICIO, SUGESTAO_DATA_FIM, DATA_ETAPA_GESTOR, ABONO_PECUNIARIO, DIAS_ABONO
      INTO V_DONO, V_STATUS, V_PEDIDO_GESTOR, V_PEDIDO_RESULTADO, V_PEDIDO_RESPOSTA,
           V_SUGESTAO_INICIO, V_SUGESTAO_FIM, V_SUGERIDA_EM, V_ABONO, V_DIAS_ABONO
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS
       AND ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_EXISTE IS NULL OR V_DONO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado para esta pessoa.';
    END IF;

    SET V_SITUACAO = CASE
        WHEN V_STATUS <> 'SOLICITADA' THEN 'FINAL'
        WHEN V_PEDIDO_RESULTADO = 'SUGERIU_DATA' AND V_PEDIDO_RESPOSTA IS NULL THEN 'SUGESTAO'
        WHEN V_PEDIDO_GESTOR IS NOT NULL AND V_PEDIDO_RESULTADO IS NULL THEN 'GESTOR'
        ELSE 'RH'
    END;

    IF V_SITUACAO = 'FINAL' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de ferias ja foi decidido.';
    END IF;

    IF V_SITUACAO <> 'SUGESTAO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido na etapa errada: ele nao esta esperando a resposta a uma sugestao de data.';
    END IF;

    IF CURDATE() > LEAST(DATE_ADD(DATE(V_SUGERIDA_EM), INTERVAL 5 DAY), DATE_SUB(V_SUGESTAO_INICIO, INTERVAL 2 DAY)) THEN
        SET V_EXPIROU = TRUE;

        UPDATE FERIAS
           SET STATUS_FERIAS = 'CANCELADA',
               RESPOSTA_SUGESTAO = 'EXPIROU',
               DATA_RESPOSTA_SUGESTAO = V_AGORA
         WHERE ID_FERIAS = P_ID_FERIAS;
    ELSEIF V_RESPOSTA = 'ACEITOU' THEN
        CALL SP_CONFERIR_FERIAS(P_ID_FUNCIONARIO, V_SUGESTAO_INICIO, V_SUGESTAO_FIM, V_ABONO, V_DIAS_ABONO, FALSE, P_ID_FERIAS, V_IGNORADA_1, V_IGNORADA_2);

        UPDATE FERIAS
           SET DATA_INICIO = V_SUGESTAO_INICIO,
               DATA_FIM = V_SUGESTAO_FIM,
               RESPOSTA_SUGESTAO = 'ACEITOU',
               DATA_RESPOSTA_SUGESTAO = V_AGORA
         WHERE ID_FERIAS = P_ID_FERIAS;
    ELSE
        UPDATE FERIAS
           SET STATUS_FERIAS = 'CANCELADA',
               RESPOSTA_SUGESTAO = 'RECUSOU',
               DATA_RESPOSTA_SUGESTAO = V_AGORA
         WHERE ID_FERIAS = P_ID_FERIAS;
    END IF;

    COMMIT;

    -- Depois do COMMIT, de proposito: a expiracao fica gravada e quem respondeu tarde recebe a recusa (o ROLLBACK do handler ja nao desfaz nada).
    IF V_EXPIROU THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O prazo para responder a sugestao terminou: o pedido de ferias foi cancelado.';
    END IF;
END$$

-- ------------------------------------------------------------
-- SP_CONFIRMAR_FERIAS_RH: a decisao FINAL do RH que aprova o pedido (APROVADA).
-- Espelha: confirmarFeriasComoRh (POST /api/rh/ferias/{feriasId}/confirmar, DecidirFeriasComoRhCasoDeUso).
-- Vale para TODO pedido que esta com o RH, nao so o que o gestor aprovou:
--   * o gestor aprovou (ele mesmo, ou a aprovacao automatica do gestor ausente);
--   * a pessoa aceitou a sugestao de outra data do gestor;
--   * o pedido nasceu (ou ficou) sem etapa do gestor: de quem e gestor, de setor sem gestor ativo, de gestor que saiu do setor.
-- Recusas, em ordem:
--   1. o pedido existe; quem decide existe e esta ativo;
--   2. a etapa: ainda com o gestor ou esperando a resposta da pessoa = etapa errada; terminado = ja decidido;
--   3. so no pedido SEM etapa do gestor (ID_GESTOR nulo): quem decide nao pode ser a propria pessoa do pedido (vence) e precisa ter linha
--      em GESTORES, porque o banco grava em ID_GESTOR a linha de quem decide (TRG_FERIAS_BEFORE_UPDATE exige o gestor e que ele nao seja a
--      pessoa do pedido; a API confere antes do UPDATE, nesta ordem, e a procedure tambem, para a recusa dizer o que consertar).
-- Com etapa do gestor feita por outra pessoa, quem e do RH pode confirmar o proprio pedido (decisao do dono, 2026-10-29).
-- Grava: STATUS_FERIAS, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH e DATA_ANALISE; o ID_GESTOR fica o que ja era.
-- A API faz e a procedure nao: conferir o perfil RH de quem chama, o e-mail da decisao a pessoa e o evento de auditoria. Para registrar
-- ferias sem pedido da pessoa, use SP_REGISTRAR_FERIAS_RH.
-- Em relacao ao arquivo antigo (mesmos parametros): a antiga so confirmava o pedido com RESULTADO_GESTOR = APROVOU e recusava os outros
-- caminhos (sugestao aceita e pedido sem etapa do gestor) com "ainda nao aprovadas pelo gestor"; sem ID_GESTOR o trigger recusaria.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CONFIRMAR_FERIAS_RH(
    IN P_ID_FERIAS INT,
    IN P_ID_FUNCIONARIO_RH INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;
    DECLARE V_PEDIDO_GESTOR INT DEFAULT NULL;
    DECLARE V_PEDIDO_RESULTADO VARCHAR(20) DEFAULT NULL;
    DECLARE V_PEDIDO_RESPOSTA VARCHAR(10) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_GESTOR_DO_RH INT DEFAULT NULL;
    DECLARE V_SITUACAO VARCHAR(10);
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    -- A pessoa do pedido e lida antes da transacao, so para trava-la primeiro (a ordem das travas); o pedido e relido ja travado.
    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_PESSOA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_FERIAS, ID_GESTOR, RESULTADO_GESTOR, RESPOSTA_SUGESTAO
      INTO V_STATUS, V_PEDIDO_GESTOR, V_PEDIDO_RESULTADO, V_PEDIDO_RESPOSTA
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS
     FOR UPDATE;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    SET V_SITUACAO = CASE
        WHEN V_STATUS <> 'SOLICITADA' THEN 'FINAL'
        WHEN V_PEDIDO_RESULTADO = 'SUGERIU_DATA' AND V_PEDIDO_RESPOSTA IS NULL THEN 'SUGESTAO'
        WHEN V_PEDIDO_GESTOR IS NOT NULL AND V_PEDIDO_RESULTADO IS NULL THEN 'GESTOR'
        ELSE 'RH'
    END;

    IF V_SITUACAO = 'FINAL' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de ferias ja foi decidido.';
    END IF;

    IF V_SITUACAO <> 'RH' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido na etapa errada: ele ainda nao chegou ao RH (esta com o gestor ou esperando a pessoa).';
    END IF;

    IF V_PEDIDO_GESTOR IS NULL THEN
        IF P_ID_FUNCIONARIO_RH = V_ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido sem etapa do gestor: quem decide nao pode ser a propria pessoa do pedido.';
        END IF;

        SELECT ID_GESTOR INTO V_GESTOR_DO_RH
          FROM GESTORES
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

        IF V_GESTOR_DO_RH IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido sem etapa do gestor: quem decide precisa estar cadastrado tambem em GESTORES.';
        END IF;
    END IF;

    UPDATE FERIAS
       SET STATUS_FERIAS = 'APROVADA',
           ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
           DATA_CONFIRMACAO_RH = V_AGORA,
           DATA_ANALISE = V_AGORA,
           ID_GESTOR = COALESCE(ID_GESTOR, V_GESTOR_DO_RH)
     WHERE ID_FERIAS = P_ID_FERIAS;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_RECUSAR_FERIAS_RH: a decisao FINAL do RH que recusa o pedido (REJEITADA), com o motivo que a pessoa ve.
-- Espelha: recusarFeriasComoRh (POST /api/rh/ferias/{feriasId}/recusar, DecidirFeriasComoRhCasoDeUso).
-- O RH pode recusar ate o que o gestor aprovou (decisao do dono, 2026-10-30).
-- Recusas, em ordem: o motivo, de 5 a 500 caracteres (antes da transacao); depois as mesmas de SP_CONFIRMAR_FERIAS_RH (pedido e responsavel;
-- etapa; e, no pedido sem etapa do gestor, quem decide nao e a propria pessoa e tem linha em GESTORES).
-- Grava: STATUS_FERIAS, MOTIVO_RECUSA, ID_FUNCIONARIO_RH, DATA_CONFIRMACAO_RH e DATA_ANALISE; o ID_GESTOR fica o que ja era (ou, sem etapa
-- do gestor, a linha de quem decide). A OBSERVACAO (mensagem da pessoa) nao muda.
-- A API faz e a procedure nao: conferir o perfil RH de quem chama, o e-mail da decisao a pessoa e o evento de auditoria.
-- Em relacao ao arquivo antigo: nao existia; pela interface do banco o RH so podia confirmar.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_RECUSAR_FERIAS_RH(
    IN P_ID_FERIAS INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_MOTIVO TEXT
)
BEGIN
    DECLARE V_MOTIVO TEXT DEFAULT NULLIF(REGEXP_REPLACE(P_MOTIVO, '^[[:space:]]+|[[:space:]]+$', ''), '');
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_PESSOA INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(30) DEFAULT NULL;
    DECLARE V_PEDIDO_GESTOR INT DEFAULT NULL;
    DECLARE V_PEDIDO_RESULTADO VARCHAR(20) DEFAULT NULL;
    DECLARE V_PEDIDO_RESPOSTA VARCHAR(10) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_GESTOR_DO_RH INT DEFAULT NULL;
    DECLARE V_SITUACAO VARCHAR(10);
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF V_MOTIVO IS NULL OR CHAR_LENGTH(V_MOTIVO) NOT BETWEEN 5 AND 500 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O motivo da recusa tem de 5 a 500 caracteres.';
    END IF;

    -- A pessoa do pedido e lida antes da transacao, so para trava-la primeiro (a ordem das travas); o pedido e relido ja travado.
    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de ferias nao encontrado.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_PESSOA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_FERIAS, ID_GESTOR, RESULTADO_GESTOR, RESPOSTA_SUGESTAO
      INTO V_STATUS, V_PEDIDO_GESTOR, V_PEDIDO_RESULTADO, V_PEDIDO_RESPOSTA
      FROM FERIAS
     WHERE ID_FERIAS = P_ID_FERIAS
     FOR UPDATE;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    SET V_SITUACAO = CASE
        WHEN V_STATUS <> 'SOLICITADA' THEN 'FINAL'
        WHEN V_PEDIDO_RESULTADO = 'SUGERIU_DATA' AND V_PEDIDO_RESPOSTA IS NULL THEN 'SUGESTAO'
        WHEN V_PEDIDO_GESTOR IS NOT NULL AND V_PEDIDO_RESULTADO IS NULL THEN 'GESTOR'
        ELSE 'RH'
    END;

    IF V_SITUACAO = 'FINAL' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de ferias ja foi decidido.';
    END IF;

    IF V_SITUACAO <> 'RH' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido na etapa errada: ele ainda nao chegou ao RH (esta com o gestor ou esperando a pessoa).';
    END IF;

    IF V_PEDIDO_GESTOR IS NULL THEN
        IF P_ID_FUNCIONARIO_RH = V_ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido sem etapa do gestor: quem decide nao pode ser a propria pessoa do pedido.';
        END IF;

        SELECT ID_GESTOR INTO V_GESTOR_DO_RH
          FROM GESTORES
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

        IF V_GESTOR_DO_RH IS NULL THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido sem etapa do gestor: quem decide precisa estar cadastrado tambem em GESTORES.';
        END IF;
    END IF;

    UPDATE FERIAS
       SET STATUS_FERIAS = 'REJEITADA',
           MOTIVO_RECUSA = V_MOTIVO,
           ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
           DATA_CONFIRMACAO_RH = V_AGORA,
           DATA_ANALISE = V_AGORA,
           ID_GESTOR = COALESCE(ID_GESTOR, V_GESTOR_DO_RH)
     WHERE ID_FERIAS = P_ID_FERIAS;

    COMMIT;
END$$






-- [[DOMINIO-C: HOLERITE, ATESTADO E BENEFICIOS]]

-- ------------------------------------------------------------
-- SP_PUBLICAR_HOLERITE: o RH publica (ou agenda) o PRIMEIRO holerite de uma pessoa para uma competencia e um tipo (versao 1).
-- Espelha: enviarHolerite, POST /api/rh/funcionarios/{funcionarioId}/holerites (EnviarHoleriteCasoDeUso).
--
-- P_PUBLICAR_EM: NULL ou a data de hoje publica agora (DATA_PUBLICACAO = NOW()); uma data futura AGENDA: DATA_PUBLICACAO = 00:00 desse dia
-- e a pessoa so ve o holerite a partir dai (a API so mostra holerite com DATA_PUBLICACAO <= agora).
-- P_VERSAO devolve sempre 1 (foi mantido para quem ja chamava a procedure antiga).
--
-- Recusas, na ordem:
--   1. entradas: funcionario, mes (1 a 12), ano (2000 a 2100), tipo (MENSAL, DECIMO_TERCEIRO, FERIAS, em maiusculas), caminho do arquivo,
--      data de publicacao anterior a hoje;
--   2. funcionario nao encontrado (pessoa INATIVA pode receber holerite: a rescisao sai depois do desligamento);
--   3. competencia anterior a admissao, posterior ao desligamento, ou futura (so FERIAS pode ser futura): as mesmas regras e os mesmos
--      textos do trigger TRG_HOLERITES_BEFORE_INSERT, conferidas aqui para sairem ANTES da recusa 4, como na API (o trigger segue de rede);
--   4. ja existe holerite (qualquer versao) dessa pessoa, competencia e tipo: para corrigir, SP_SUBSTITUIR_HOLERITE.
-- A linha da pessoa fica travada (FOR UPDATE): dois envios iguais ao mesmo tempo viram um holerite e uma recusa 4.
--
-- O que a API faz e a procedure NAO: guardar o PDF no armazenamento e conferir que e PDF de ate 5 MB (aqui so entra o caminho, que precisa
-- apontar para um arquivo que ja existe la); calcular a data "no dia do pagamento" (dia da empresa, feriados e excecoes do mes: quem chama
-- passa a data pronta em P_PUBLICAR_EM); criar a tarefa PUBLICAR_HOLERITE que manda o e-mail (a API recupera sozinha, ao iniciar, os
-- holerites agendados e os publicados nos ultimos 7 dias); o evento de auditoria; o envio em lote (e a mesma operacao, uma chamada por pessoa).
--
-- Mudou em relacao ao arquivo antigo: a antiga criava em silencio uma VERSAO NOVA quando a competencia ja existia (a API recusa com
-- HOLERITE_JA_EXISTE e tem outra operacao para corrigir: SP_SUBSTITUIR_HOLERITE); nao agendava (sempre NOW()); nao conferia o ano;
-- aceitava o tipo em minusculas; P_ANO e P_MES viraram INT (um valor fora da faixa agora tem recusa de negocio); entrou P_PUBLICAR_EM.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_PUBLICAR_HOLERITE(
    IN P_ID_FUNCIONARIO INT,
    IN P_ANO INT,
    IN P_MES INT,
    IN P_TIPO_HOLERITE VARCHAR(20),
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_PUBLICAR_EM DATE,
    OUT P_ID_HOLERITE INT,
    OUT P_VERSAO SMALLINT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ADMISSAO DATE DEFAULT NULL;
    DECLARE V_DESLIGAMENTO DATE DEFAULT NULL;
    DECLARE V_COMPETENCIA DATE DEFAULT NULL;
    DECLARE V_EXISTE INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_HOLERITE = NULL;
    SET P_VERSAO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario do holerite.';
    END IF;

    IF P_MES IS NULL OR P_MES NOT BETWEEN 1 AND 12 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o mes do holerite, de 1 a 12.';
    END IF;

    IF P_ANO IS NULL OR P_ANO NOT BETWEEN 2000 AND 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o ano do holerite, de 2000 a 2100.';
    END IF;

    IF P_TIPO_HOLERITE IS NULL OR CAST(P_TIPO_HOLERITE AS BINARY) NOT IN ('MENSAL', 'DECIMO_TERCEIRO', 'FERIAS') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o tipo do holerite: MENSAL, DECIMO_TERCEIRO ou FERIAS.';
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do holerite.';
    END IF;

    IF P_PUBLICAR_EM IS NOT NULL AND P_PUBLICAR_EM < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de publicacao do holerite nao pode ser anterior a hoje.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, DATA_ADMISSAO, DATA_DESLIGAMENTO
      INTO V_ID_FUNCIONARIO, V_ADMISSAO, V_DESLIGAMENTO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SET V_COMPETENCIA = STR_TO_DATE(CONCAT(P_ANO, '-', LPAD(P_MES, 2, '0'), '-01'), '%Y-%m-%d');

    IF LAST_DAY(V_COMPETENCIA) < V_ADMISSAO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A competencia do holerite e anterior a admissao.';
    END IF;

    IF V_DESLIGAMENTO IS NOT NULL AND V_COMPETENCIA > V_DESLIGAMENTO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A competencia do holerite e posterior ao desligamento.';
    END IF;

    IF V_COMPETENCIA > CURDATE() AND P_TIPO_HOLERITE <> 'FERIAS' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel cadastrar holerite de competencia futura.';
    END IF;

    -- Leitura com trava: ve o que outra sessao acabou de gravar (a leitura comum olharia a foto do comeco da transacao).
    SELECT ID_HOLERITE INTO V_EXISTE
      FROM HOLERITES
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND ANO = P_ANO
       AND MES = P_MES
       AND TIPO_HOLERITE = P_TIPO_HOLERITE
     ORDER BY VERSAO DESC
     LIMIT 1
     FOR UPDATE;

    IF V_EXISTE IS NOT NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Ja existe holerite desta competencia e tipo para o funcionario. Para corrigir, use SP_SUBSTITUIR_HOLERITE.';
    END IF;

    INSERT INTO HOLERITES
        (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, DATA_PUBLICACAO, CAMINHO_ARQUIVO)
    VALUES
        (P_ID_FUNCIONARIO, P_ANO, P_MES, P_TIPO_HOLERITE, 1,
         CASE WHEN P_PUBLICAR_EM IS NULL OR P_PUBLICAR_EM <= CURDATE() THEN NOW() ELSE CAST(P_PUBLICAR_EM AS DATETIME) END,
         TRIM(P_CAMINHO_ARQUIVO));

    SET P_ID_HOLERITE = LAST_INSERT_ID();
    SET P_VERSAO = 1;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_SUBSTITUIR_HOLERITE: o RH corrige um holerite enviado errado guardando a VERSAO SEGUINTE da mesma pessoa, competencia e tipo
-- (holerite nunca e apagado nem alterado: TRG_HOLERITES_BEFORE_DELETE). A pessoa passa a ver a maior versao ja publicada.
-- Espelha: substituirHolerite, POST /api/rh/holerites/{holeriteId}/versoes (SubstituirHoleriteCasoDeUso).
--
-- P_ID_HOLERITE tem de ser a versao ATUAL (a maior) da competencia. P_PUBLICAR_EM como em SP_PUBLICAR_HOLERITE: se a versao nova e
-- agendada, a antiga segue valendo ate o dia. Devolve o id da linha nova e a versao dela.
--
-- Recusas, na ordem:
--   1. entradas: holerite, caminho do arquivo, data de publicacao anterior a hoje;
--   2. holerite nao encontrado;
--   3. o holerite informado ja foi substituido por uma versao mais nova (HOLERITE_VERSAO_DESATUALIZADA);
--   4. a competencia contra a pessoa de HOJE (anterior a admissao, posterior ao desligamento, futura): recusa do trigger
--      TRG_HOLERITES_BEFORE_INSERT, com o texto dele.
-- A linha da pessoa fica travada (FOR UPDATE, a mesma trava de SP_PUBLICAR_HOLERITE): duas substituicoes do mesmo holerite ao mesmo
-- tempo viram uma versao nova e uma recusa 3.
--
-- O que a API faz e a procedure NAO: o mesmo de SP_PUBLICAR_HOLERITE (arquivo, dia do pagamento, tarefa do e-mail, auditoria).
--
-- Nao existia no arquivo antigo: la a SP_PUBLICAR_HOLERITE fazia as duas coisas, sem conferir qual versao estava sendo corrigida.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SUBSTITUIR_HOLERITE(
    IN P_ID_HOLERITE INT,
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_PUBLICAR_EM DATE,
    OUT P_ID_HOLERITE_NOVO INT,
    OUT P_VERSAO SMALLINT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ANO INT DEFAULT NULL;
    DECLARE V_MES INT DEFAULT NULL;
    DECLARE V_TIPO VARCHAR(20) DEFAULT NULL;
    DECLARE V_VERSAO INT DEFAULT NULL;
    DECLARE V_MAIOR_VERSAO INT DEFAULT NULL;
    DECLARE V_TRAVA INT DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_HOLERITE_NOVO = NULL;
    SET P_VERSAO = NULL;

    IF P_ID_HOLERITE IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o holerite a substituir.';
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do holerite.';
    END IF;

    IF P_PUBLICAR_EM IS NOT NULL AND P_PUBLICAR_EM < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de publicacao do holerite nao pode ser anterior a hoje.';
    END IF;

    START TRANSACTION;

    -- A linha do holerite nao muda nunca; o que serializa e a trava da pessoa, tomada antes de olhar a maior versao.
    SELECT ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO
      INTO V_ID_FUNCIONARIO, V_ANO, V_MES, V_TIPO, V_VERSAO
      FROM HOLERITES
     WHERE ID_HOLERITE = P_ID_HOLERITE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Holerite nao encontrado.';
    END IF;

    SELECT ID_FUNCIONARIO INTO V_TRAVA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    -- Leitura com trava: ve a versao que outra sessao acabou de gravar (a leitura comum olharia a foto do comeco da transacao).
    SELECT VERSAO INTO V_MAIOR_VERSAO
      FROM HOLERITES
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
       AND ANO = V_ANO
       AND MES = V_MES
       AND TIPO_HOLERITE = V_TIPO
     ORDER BY VERSAO DESC
     LIMIT 1
     FOR UPDATE;

    IF V_VERSAO <> V_MAIOR_VERSAO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Este holerite ja foi substituido por uma versao mais nova. Substitua a versao atual.';
    END IF;

    INSERT INTO HOLERITES
        (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO, DATA_PUBLICACAO, CAMINHO_ARQUIVO)
    VALUES
        (V_ID_FUNCIONARIO, V_ANO, V_MES, V_TIPO, V_VERSAO + 1,
         CASE WHEN P_PUBLICAR_EM IS NULL OR P_PUBLICAR_EM <= CURDATE() THEN NOW() ELSE CAST(P_PUBLICAR_EM AS DATETIME) END,
         TRIM(P_CAMINHO_ARQUIVO));

    SET P_ID_HOLERITE_NOVO = LAST_INSERT_ID();
    SET P_VERSAO = V_VERSAO + 1;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_ENVIAR_ATESTADO: a pessoa envia o atestado dela, de DIAS INTEIROS (sem horas) ou de ALGUMAS HORAS de um dia so. Nasce ENVIADO.
-- Espelha: enviarAtestado, POST /api/eu/ponto/atestados (EnviarAtestadoCasoDeUso).
--
-- Recusas, na ordem:
--   1. entradas: funcionario; data de inicio e de fim; fim antes do inicio; as duas horas juntas ou nenhuma; horas so em minutos cheios,
--      de 00:00 a 23:59 (a API le HH:mm); atestado de horas em um dia so; hora de fim depois da de inicio; caminho do arquivo;
--   2. funcionario nao encontrado;
--   3. funcionario inativo (na API quem esta inativo nem entra no sistema; aqui a recusa e explicita).
-- A API nao limita o periodo: atestado de dia futuro, de dia antigo ou de antes da admissao e aceito (quem julga e o RH, ao decidir).
--
-- O que a API faz e a procedure NAO: guardar o arquivo (PDF ou imagem, ate 5 MB, conferido pelo conteudo) e limpar o nome dele (aqui
-- entram o caminho ja gravado no armazenamento e o nome como vier; nome vazio vira NULL); o evento de auditoria. Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: uma mensagem por regra (a antiga respondia tudo com "Periodo, horario ou arquivo do atestado
-- invalido."); funcionario inexistente e inativo separados; horas com segundos ou fora do dia recusadas; nome do arquivo vazio vira NULL.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_ENVIAR_ATESTADO(
    IN P_ID_FUNCIONARIO INT,
    IN P_DATA_INICIO DATE,
    IN P_DATA_FIM DATE,
    IN P_HORA_INICIO TIME,
    IN P_HORA_FIM TIME,
    IN P_CAMINHO_ARQUIVO VARCHAR(255),
    IN P_NOME_ARQUIVO VARCHAR(255),
    OUT P_ID_ATESTADO INT
)
BEGIN
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_ATESTADO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario do atestado.';
    END IF;

    IF P_DATA_INICIO IS NULL OR P_DATA_FIM IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio e a data de fim do atestado.';
    END IF;

    IF P_DATA_FIM < P_DATA_INICIO THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de fim do atestado nao pode ser antes da data de inicio.';
    END IF;

    IF (P_HORA_INICIO IS NULL) <> (P_HORA_FIM IS NULL) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a hora de inicio e a hora de fim juntas.';
    END IF;

    IF P_HORA_INICIO IS NOT NULL THEN
        IF P_HORA_INICIO NOT BETWEEN '00:00:00' AND '23:59:00' OR P_HORA_FIM NOT BETWEEN '00:00:00' AND '23:59:00'
           OR SECOND(P_HORA_INICIO) <> 0 OR SECOND(P_HORA_FIM) <> 0 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe as horas do atestado em horas e minutos, de 00:00 a 23:59.';
        END IF;

        IF P_DATA_INICIO <> P_DATA_FIM THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O atestado de algumas horas vale para um dia so: use a mesma data de inicio e de fim.';
        END IF;

        IF P_HORA_FIM <= P_HORA_INICIO THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A hora de fim do atestado precisa ser depois da hora de inicio.';
        END IF;
    END IF;

    IF P_CAMINHO_ARQUIVO IS NULL OR TRIM(P_CAMINHO_ARQUIVO) = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o caminho do arquivo do atestado.';
    END IF;

    START TRANSACTION;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR SHARE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario inativo nao pode enviar atestado.';
    END IF;

    INSERT INTO ATESTADOS
        (ID_FUNCIONARIO, DATA_INICIO, DATA_FIM, HORA_INICIO, HORA_FIM,
         CAMINHO_ARQUIVO, NOME_ARQUIVO_ORIGINAL, STATUS_ATESTADO, DATA_ENVIO)
    VALUES
        (P_ID_FUNCIONARIO, P_DATA_INICIO, P_DATA_FIM, P_HORA_INICIO, P_HORA_FIM,
         TRIM(P_CAMINHO_ARQUIVO), NULLIF(TRIM(P_NOME_ARQUIVO), ''), 'ENVIADO', NOW());

    SET P_ID_ATESTADO = LAST_INSERT_ID();
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_DECIDIR_ATESTADO: a decisao FINAL do RH sobre um atestado ENVIADO. P_DECISAO: VALIDADO (o dia ou as horas deixam de contar como
-- falta) ou RECUSADO (com o motivo; o dia segue como falta).
-- Espelha: validarAtestado e recusarAtestado, POST /api/rh/ponto/atestados/{atestadoId}/validar e /recusar (DecidirAtestadoCasoDeUso).
--
-- Grava STATUS_ATESTADO, ID_FUNCIONARIO_RH, DATA_DECISAO = NOW() e MOTIVO_RECUSA (o motivo sem espacos nas pontas; NULL ao validar, mesmo
-- que venha um). O RH pode decidir o proprio atestado, como na API.
--
-- Recusas, na ordem:
--   1. entradas: atestado; responsavel do RH; decisao (VALIDADO ou RECUSADO, em maiusculas); na recusa, o motivo de 1 a 500 caracteres;
--   2. atestado nao encontrado;
--   3. atestado ja decidido (ATESTADO_JA_DECIDIDO): a decisao e final;
--   4. responsavel do RH inexistente ou inativo.
-- Travas: a pessoa do atestado (FOR UPDATE, a mesma trava do ponto, como a API faz ao validar) e depois o atestado: de duas decisoes ao
-- mesmo tempo uma vale e a outra cai na recusa 3.
--
-- O que a API faz e a procedure NAO: RECALCULAR O BANCO DE HORAS. Ao validar, a API recalcula cada dia do atestado que ja passou (da
-- admissao ate ontem): o lancamento de falta desses dias em BANCOS_HORAS e refeito pelo calculo do espelho, que e codigo da API. Depois de
-- validar por aqui um atestado com dia passado, o saldo desses dias fica como estava ate a API recalcular o dia. Tambem ficam de fora:
-- conferir que o responsavel tem perfil de RH (so se confere que existe e esta ativo) e o evento de auditoria. Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: "inexistente" e "ja analisado" viraram duas recusas; o motivo e aparado e limitado a 500; a decisao
-- em minusculas e recusada; a pessoa do atestado e travada antes do atestado (mesma ordem de travas da API).
-- ------------------------------------------------------------
CREATE PROCEDURE SP_DECIDIR_ATESTADO(
    IN P_ID_ATESTADO INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_DECISAO VARCHAR(10),
    IN P_MOTIVO_RECUSA TEXT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_TRAVA INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(10) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_ATESTADO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o atestado.';
    END IF;

    IF P_ID_FUNCIONARIO_RH IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o responsavel do RH pela decisao.';
    END IF;

    IF P_DECISAO IS NULL OR CAST(P_DECISAO AS BINARY) NOT IN ('VALIDADO', 'RECUSADO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Decisao de atestado invalida: informe VALIDADO ou RECUSADO.';
    END IF;

    IF P_DECISAO = 'RECUSADO'
       AND (P_MOTIVO_RECUSA IS NULL OR CHAR_LENGTH(TRIM(P_MOTIVO_RECUSA)) NOT BETWEEN 1 AND 500) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o motivo da recusa, de 1 a 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM ATESTADOS
     WHERE ID_ATESTADO = P_ID_ATESTADO;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Atestado nao encontrado.';
    END IF;

    SELECT ID_FUNCIONARIO INTO V_TRAVA
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_ATESTADO INTO V_STATUS
      FROM ATESTADOS
     WHERE ID_ATESTADO = P_ID_ATESTADO
     FOR UPDATE;

    IF V_STATUS <> 'ENVIADO' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este atestado ja foi decidido.';
    END IF;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    UPDATE ATESTADOS
       SET STATUS_ATESTADO = CASE WHEN P_DECISAO = 'RECUSADO' THEN 'RECUSADO' ELSE 'VALIDADO' END,
           ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
           DATA_DECISAO = NOW(),
           MOTIVO_RECUSA = CASE WHEN P_DECISAO = 'RECUSADO' THEN TRIM(P_MOTIVO_RECUSA) ELSE NULL END
     WHERE ID_ATESTADO = P_ID_ATESTADO;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_SOLICITAR_BENEFICIO: a pessoa pede a adesao a um beneficio OPCIONAL, com a data de inicio, a autorizacao de desconto em folha e,
-- se o beneficio aceitar, os dependentes e o trajeto do vale-transporte. O pedido nasce PENDENTE e INATIVO; os dependentes e o trajeto
-- nascem PENDENTE junto, com a mesma data.
-- Espelha: pedirAdesaoABeneficio, POST /api/eu/beneficios-adesoes (PedirAdesaoABeneficioCasoDeUso).
--
-- P_DEPENDENTES: NULL (nenhum) ou o texto de uma lista JSON com ate 10 itens, com os nomes de campo do contrato:
--   '[{"nome": "Ana Lima", "parentesco": "Filha", "dataNascimento": "2015-03-09"}]'
-- P_TRAJETO_ORIGEM, P_TRAJETO_DESTINO, P_TRAJETO_LINHAS_MEIOS: os tres NULL (sem trajeto) ou os tres preenchidos.
--
-- QUEM PEDE DE NOVO: existe UMA linha por pessoa e beneficio (chave unica). Se a pessoa ja tem a linha RECUSADA, CANCELADA ou aprovada e
-- DESATIVADA pelo RH, a MESMA linha e reaberta: volta a PENDENTE com a data, o inicio e a autorizacao novos; quem respondeu, quando e o
-- motivo da recusa sao limpos; os dependentes e trajetos dela que estavam PENDENTE ou APROVADO viram CANCELADO (os RECUSADO e CANCELADO
-- ficam de historico); e entram os do pedido novo. P_ID_BENEFICIO_FUNCIONARIOS devolve o id da linha (a nova ou a reaberta).
--
-- Recusas, na ordem:
--   1. entradas: funcionario; beneficio; data de inicio (obrigatoria, de hoje ate 2100); autorizacao de desconto (TRUE ou FALSE, obrigatoria);
--      dependentes (JSON valido, lista, ate 10; nome de 1 a 150; parentesco de 1 a 30; nascimento AAAA-MM-DD, de 1900 ate hoje); trajeto
--      (os tres campos; origem e destino ate 200, linhas e meios ate 255);
--   2. funcionario nao encontrado;
--   3. beneficio nao encontrado;
--   4. a pessoa ja tem o beneficio ATIVO ou um pedido PENDENTE dele (BENEFICIO_JA_ATIVO_OU_PENDENTE);
--   5. funcionario inativo;
--   6. beneficio nao opcional (o RH concede direto na ficha);
--   7. vieram dependentes e o beneficio nao aceita dependentes;
--   8. veio trajeto e o beneficio nao aceita trajeto.
-- A linha da pessoa fica travada (FOR UPDATE): dois pedidos iguais ao mesmo tempo viram um pedido e uma recusa 4.
--
-- O que a API faz e a procedure NAO: o evento de auditoria (que guarda a autorizacao anterior na reabertura). Nenhum e-mail sai.
--
-- Mudou em relacao ao arquivo antigo: a antiga recusava QUALQUER linha existente (quem foi recusado nunca mais podia pedir); nao conferia
-- se o beneficio e opcional; nao gravava dependentes nem trajeto; aceitava data de inicio nula ou passada e autorizacao nula (virava FALSE).
-- Entraram P_DEPENDENTES e os tres P_TRAJETO_*, antes do OUT.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_SOLICITAR_BENEFICIO(
    IN P_ID_FUNCIONARIO INT,
    IN P_ID_BENEFICIO INT,
    IN P_DATA_INICIO_DESEJADA DATE,
    IN P_AUTORIZA_DESCONTO_FOLHA BOOLEAN,
    IN P_DEPENDENTES TEXT,
    IN P_TRAJETO_ORIGEM TEXT,
    IN P_TRAJETO_DESTINO TEXT,
    IN P_TRAJETO_LINHAS_MEIOS TEXT,
    OUT P_ID_BENEFICIO_FUNCIONARIOS INT
)
BEGIN
    DECLARE V_DEPENDENTES JSON DEFAULT NULL;
    DECLARE V_QTD_DEPENDENTES INT DEFAULT 0;
    DECLARE V_ERRO_DEPENDENTE INT DEFAULT NULL;
    DECLARE V_TEM_TRAJETO BOOLEAN DEFAULT FALSE;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_BENEFICIO INT DEFAULT NULL;
    DECLARE V_OPCIONAL BOOLEAN DEFAULT NULL;
    DECLARE V_ACEITA_DEPENDENTES BOOLEAN DEFAULT NULL;
    DECLARE V_ACEITA_TRAJETO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_ADESAO INT DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(20) DEFAULT NULL;
    DECLARE V_BENEFICIO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_BENEFICIO_FUNCIONARIOS = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario que pede o beneficio.';
    END IF;

    IF P_ID_BENEFICIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o beneficio.';
    END IF;

    IF P_DATA_INICIO_DESEJADA IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de inicio desejada.';
    END IF;

    IF P_DATA_INICIO_DESEJADA < CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de inicio nao pode ser anterior a hoje.';
    END IF;

    IF YEAR(P_DATA_INICIO_DESEJADA) > 2100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe uma data de inicio ate 2100.';
    END IF;

    IF P_AUTORIZA_DESCONTO_FOLHA IS NULL OR P_AUTORIZA_DESCONTO_FOLHA NOT IN (0, 1) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe se autoriza o desconto em folha: TRUE ou FALSE.';
    END IF;

    IF P_DEPENDENTES IS NOT NULL AND TRIM(P_DEPENDENTES) <> '' THEN
        IF NOT JSON_VALID(P_DEPENDENTES) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Os dependentes precisam vir como uma lista JSON.';
        END IF;

        SET V_DEPENDENTES = CAST(P_DEPENDENTES AS JSON);

        IF JSON_TYPE(V_DEPENDENTES) = 'NULL' THEN
            SET V_DEPENDENTES = NULL;
        ELSEIF JSON_TYPE(V_DEPENDENTES) <> 'ARRAY' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Os dependentes precisam vir como uma lista JSON.';
        ELSE
            SET V_QTD_DEPENDENTES = JSON_LENGTH(V_DEPENDENTES);
        END IF;
    END IF;

    IF V_QTD_DEPENDENTES > 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe no maximo 10 dependentes.';
    END IF;

    IF V_QTD_DEPENDENTES > 0 THEN
        -- O menor codigo de erro entre os itens (NULL: todos valem). A data e conferida como texto antes de virar DATE.
        SELECT MIN(CASE
                   WHEN D.NOME IS NULL OR CHAR_LENGTH(TRIM(D.NOME)) = 0 THEN 1
                   WHEN CHAR_LENGTH(TRIM(D.NOME)) > 150 THEN 2
                   WHEN D.PARENTESCO IS NULL OR CHAR_LENGTH(TRIM(D.PARENTESCO)) = 0 THEN 3
                   WHEN CHAR_LENGTH(TRIM(D.PARENTESCO)) > 30 THEN 4
                   WHEN D.NASCIMENTO IS NULL OR NOT REGEXP_LIKE(D.NASCIMENTO, '^[0-9]{4}-[0-9]{2}-[0-9]{2}$') THEN 5
                   WHEN SUBSTRING(D.NASCIMENTO, 1, 4) < '1900' THEN 7
                   WHEN SUBSTRING(D.NASCIMENTO, 6, 2) NOT BETWEEN '01' AND '12' OR SUBSTRING(D.NASCIMENTO, 9, 2) < '01' THEN 5
                   WHEN CAST(SUBSTRING(D.NASCIMENTO, 9, 2) AS UNSIGNED)
                        > DAY(LAST_DAY(CONCAT(SUBSTRING(D.NASCIMENTO, 1, 7), '-01'))) THEN 5
                   WHEN D.NASCIMENTO > DATE_FORMAT(CURDATE(), '%Y-%m-%d') THEN 6
               END)
          INTO V_ERRO_DEPENDENTE
          FROM JSON_TABLE(V_DEPENDENTES, '$[*]' COLUMNS (
                   NOME TEXT PATH '$.nome' NULL ON EMPTY NULL ON ERROR,
                   PARENTESCO TEXT PATH '$.parentesco' NULL ON EMPTY NULL ON ERROR,
                   NASCIMENTO VARCHAR(40) PATH '$.dataNascimento' NULL ON EMPTY NULL ON ERROR)) AS D;

        IF V_ERRO_DEPENDENTE = 1 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome de cada dependente.';
        ELSEIF V_ERRO_DEPENDENTE = 2 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome do dependente pode ter ate 150 caracteres.';
        ELSEIF V_ERRO_DEPENDENTE = 3 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o parentesco de cada dependente.';
        ELSEIF V_ERRO_DEPENDENTE = 4 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O parentesco do dependente pode ter ate 30 caracteres.';
        ELSEIF V_ERRO_DEPENDENTE = 5 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de nascimento de cada dependente no formato AAAA-MM-DD.';
        ELSEIF V_ERRO_DEPENDENTE = 6 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de nascimento do dependente nao pode ser futura.';
        ELSEIF V_ERRO_DEPENDENTE = 7 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe uma data de nascimento do dependente a partir de 1900.';
        END IF;
    END IF;

    SET V_TEM_TRAJETO = COALESCE(TRIM(P_TRAJETO_ORIGEM), '') <> ''
                        OR COALESCE(TRIM(P_TRAJETO_DESTINO), '') <> ''
                        OR COALESCE(TRIM(P_TRAJETO_LINHAS_MEIOS), '') <> '';

    IF V_TEM_TRAJETO THEN
        IF COALESCE(TRIM(P_TRAJETO_ORIGEM), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a origem do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_ORIGEM)) > 200 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A origem do trajeto pode ter ate 200 caracteres.';
        ELSEIF COALESCE(TRIM(P_TRAJETO_DESTINO), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o destino do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_DESTINO)) > 200 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O destino do trajeto pode ter ate 200 caracteres.';
        ELSEIF COALESCE(TRIM(P_TRAJETO_LINHAS_MEIOS), '') = '' THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe as linhas ou os meios de transporte do trajeto.';
        ELSEIF CHAR_LENGTH(TRIM(P_TRAJETO_LINHAS_MEIOS)) > 255 THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'As linhas ou os meios do trajeto podem ter ate 255 caracteres.';
        END IF;
    END IF;

    START TRANSACTION;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
     FOR UPDATE;

    IF V_ATIVO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SELECT ID_BENEFICIO, OPCIONAL, ACEITA_DEPENDENTES, ACEITA_TRAJETO
      INTO V_ID_BENEFICIO, V_OPCIONAL, V_ACEITA_DEPENDENTES, V_ACEITA_TRAJETO
      FROM BENEFICIOS
     WHERE ID_BENEFICIO = P_ID_BENEFICIO
     FOR SHARE;

    IF V_ID_BENEFICIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Beneficio nao encontrado.';
    END IF;

    SELECT ID_BENEFICIO_FUNCIONARIOS, STATUS_PEDIDO, BENEFICIO_ATIVO
      INTO V_ID_ADESAO, V_STATUS, V_BENEFICIO_ATIVO
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       AND ID_BENEFICIO = P_ID_BENEFICIO
     FOR UPDATE;

    IF V_STATUS = 'PENDENTE' OR (V_STATUS = 'APROVADO' AND V_BENEFICIO_ATIVO = TRUE) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'O funcionario ja tem este beneficio ou ja pediu a adesao e o RH ainda vai responder.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel pedir beneficio para funcionario inativo.';
    END IF;

    IF V_OPCIONAL = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao e opcional: o RH o concede direto na ficha.';
    END IF;

    IF V_QTD_DEPENDENTES > 0 AND V_ACEITA_DEPENDENTES = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao permite incluir dependentes.';
    END IF;

    IF V_TEM_TRAJETO AND V_ACEITA_TRAJETO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este beneficio nao tem trajeto: so o vale-transporte tem.';
    END IF;

    IF V_ID_ADESAO IS NULL THEN
        INSERT INTO BENEFICIOS_FUNCIONARIOS
            (ID_FUNCIONARIO, ID_BENEFICIO, BENEFICIO_ATIVO, STATUS_PEDIDO,
             DATA_PEDIDO, DATA_INICIO_DESEJADA, AUTORIZA_DESCONTO_FOLHA)
        VALUES
            (P_ID_FUNCIONARIO, P_ID_BENEFICIO, FALSE, 'PENDENTE',
             V_AGORA, P_DATA_INICIO_DESEJADA, P_AUTORIZA_DESCONTO_FOLHA);

        SET V_ID_ADESAO = LAST_INSERT_ID();
    ELSE
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'PENDENTE',
               BENEFICIO_ATIVO = FALSE,
               DATA_PEDIDO = V_AGORA,
               DATA_INICIO_DESEJADA = P_DATA_INICIO_DESEJADA,
               AUTORIZA_DESCONTO_FOLHA = P_AUTORIZA_DESCONTO_FOLHA,
               ID_FUNCIONARIO_RH = NULL,
               DATA_RESPOSTA = NULL,
               MOTIVO_RECUSA = NULL
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO;

        -- O que nao faz parte do pedido novo e cancelado: senao o dependente ou o trajeto de uma adesao antiga reapareceria aprovado
        -- numa adesao que o RH ainda nem decidiu.
        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO
           AND STATUS_PEDIDO IN ('PENDENTE', 'APROVADO');

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO
           AND STATUS_PEDIDO IN ('PENDENTE', 'APROVADO');
    END IF;

    IF V_QTD_DEPENDENTES > 0 THEN
        INSERT INTO DEPENDENTES
            (ID_BENEFICIO_FUNCIONARIOS, NOME, PARENTESCO, DATA_NASCIMENTO, STATUS_PEDIDO, DATA_PEDIDO)
        SELECT V_ID_ADESAO, TRIM(D.NOME), TRIM(D.PARENTESCO), CAST(D.NASCIMENTO AS DATE), 'PENDENTE', V_AGORA
          FROM JSON_TABLE(V_DEPENDENTES, '$[*]' COLUMNS (
                   ORDEM FOR ORDINALITY,
                   NOME TEXT PATH '$.nome' NULL ON EMPTY NULL ON ERROR,
                   PARENTESCO TEXT PATH '$.parentesco' NULL ON EMPTY NULL ON ERROR,
                   NASCIMENTO VARCHAR(40) PATH '$.dataNascimento' NULL ON EMPTY NULL ON ERROR)) AS D
         ORDER BY D.ORDEM;
    END IF;

    IF V_TEM_TRAJETO THEN
        INSERT INTO TRAJETOS_VALE_TRANSPORTE
            (ID_BENEFICIO_FUNCIONARIOS, ORIGEM, DESTINO, LINHAS_MEIOS, STATUS_PEDIDO, DATA_PEDIDO)
        VALUES
            (V_ID_ADESAO, TRIM(P_TRAJETO_ORIGEM), TRIM(P_TRAJETO_DESTINO), TRIM(P_TRAJETO_LINHAS_MEIOS), 'PENDENTE', V_AGORA);
    END IF;

    SET P_ID_BENEFICIO_FUNCIONARIOS = V_ID_ADESAO;
    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_DECIDIR_BENEFICIO: o RH aprova ou recusa um pedido de ADESAO pendente. P_DECISAO: APROVADO ou RECUSADO (com o motivo).
-- Espelha: aprovarAdesaoABeneficio e recusarAdesaoABeneficio, POST /api/rh/beneficios-adesoes/{adesaoId}/aprovar e /recusar
-- (DecidirPedidoDeBeneficioCasoDeUso).
--
-- APROVADO: a adesao fica APROVADO e ATIVA, com quem respondeu e quando, sem motivo; os dependentes e o trajeto PENDENTE dela sao
--   aprovados JUNTO, com o mesmo RH e a mesma data.
-- RECUSADO: a adesao fica RECUSADO (inativa), com quem respondeu, quando e o motivo (sem espacos nas pontas); os dependentes e o trajeto
--   PENDENTE dela viram CANCELADO, sem resposta. A pessoa pode pedir de novo (SP_SOLICITAR_BENEFICIO reabre a mesma linha).
-- O RH pode decidir o proprio pedido, como na API.
--
-- Recusas, na ordem:
--   1. entradas: pedido; responsavel do RH; decisao (APROVADO ou RECUSADO, em maiusculas); na recusa, o motivo de 1 a 500 caracteres;
--   2. pedido nao encontrado;
--   3. pedido ja respondido (BENEFICIO_PEDIDO_JA_DECIDIDO): so o PENDENTE se decide;
--   4. ao aprovar, a pessoa do pedido esta inativa (o trigger so olha o INSERT; a regra e da API);
--   5. responsavel do RH inexistente ou inativo.
-- Travas: a pessoa do pedido (FOR UPDATE) e depois a adesao, na ordem da API: de dois RHs ao mesmo tempo um decide e o outro cai na recusa 3.
--
-- O que a API faz e a procedure NAO: conferir que o responsavel tem perfil de RH (so se confere que existe e esta ativo) e o evento de
-- auditoria. Nenhum e-mail sai. A decisao de um dependente ou de um trajeto pedido DEPOIS, numa adesao ja ativa, e outra operacao da API
-- (aprovar/recusar dependente e trajeto) e nao tem procedure.
--
-- Mudou em relacao ao arquivo antigo: a antiga nao tocava nos dependentes nem no trajeto do pedido (ficavam PENDENTE para sempre, dentro
-- de uma adesao recusada ou ja ativa); aprovava beneficio de pessoa inativa; "inexistente" e "ja respondido" eram uma recusa so; o motivo
-- nao era aparado nem limitado.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_DECIDIR_BENEFICIO(
    IN P_ID_BENEFICIO_FUNCIONARIOS INT,
    IN P_ID_FUNCIONARIO_RH INT,
    IN P_DECISAO VARCHAR(10),
    IN P_MOTIVO_RECUSA TEXT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_STATUS VARCHAR(20) DEFAULT NULL;
    DECLARE V_RH_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_AGORA DATETIME DEFAULT NOW();

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_BENEFICIO_FUNCIONARIOS IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o pedido de beneficio.';
    END IF;

    IF P_ID_FUNCIONARIO_RH IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o responsavel do RH pela decisao.';
    END IF;

    IF P_DECISAO IS NULL OR CAST(P_DECISAO AS BINARY) NOT IN ('APROVADO', 'RECUSADO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Decisao de beneficio invalida: informe APROVADO ou RECUSADO.';
    END IF;

    IF P_DECISAO = 'RECUSADO'
       AND (P_MOTIVO_RECUSA IS NULL OR CHAR_LENGTH(TRIM(P_MOTIVO_RECUSA)) NOT BETWEEN 1 AND 500) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o motivo da recusa, de 1 a 500 caracteres.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO INTO V_ID_FUNCIONARIO
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Pedido de beneficio nao encontrado.';
    END IF;

    SELECT ATIVO INTO V_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = V_ID_FUNCIONARIO
     FOR UPDATE;

    SELECT STATUS_PEDIDO INTO V_STATUS
      FROM BENEFICIOS_FUNCIONARIOS
     WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
     FOR UPDATE;

    IF V_STATUS <> 'PENDENTE' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este pedido de beneficio ja foi respondido.';
    END IF;

    IF P_DECISAO = 'APROVADO' AND V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel ativar beneficio para funcionario inativo.';
    END IF;

    SELECT ATIVO INTO V_RH_ATIVO
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO_RH;

    IF V_RH_ATIVO IS NULL OR V_RH_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Responsavel do RH inexistente ou inativo.';
    END IF;

    IF P_DECISAO = 'APROVADO' THEN
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'APROVADO',
               BENEFICIO_ATIVO = TRUE,
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA,
               MOTIVO_RECUSA = NULL
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'APROVADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'APROVADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';
    ELSE
        UPDATE BENEFICIOS_FUNCIONARIOS
           SET STATUS_PEDIDO = 'RECUSADO',
               ID_FUNCIONARIO_RH = P_ID_FUNCIONARIO_RH,
               DATA_RESPOSTA = V_AGORA,
               MOTIVO_RECUSA = TRIM(P_MOTIVO_RECUSA)
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS;

        UPDATE DEPENDENTES
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';

        UPDATE TRAJETOS_VALE_TRANSPORTE
           SET STATUS_PEDIDO = 'CANCELADO'
         WHERE ID_BENEFICIO_FUNCIONARIOS = P_ID_BENEFICIO_FUNCIONARIOS
           AND STATUS_PEDIDO = 'PENDENTE';
    END IF;

    COMMIT;
END$$






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- (preenchido pela secao D)






-- [[DOMINIO-E: CADASTROS]]
-- Quatro procedures: SP_CRIAR_USUARIO, SP_CADASTRAR_FUNCIONARIO, SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO e SP_CADASTRAR_CARGO.
-- A quinta do arquivo antigo, SP_CADASTRAR_NIVEL_ACESSO, foi REMOVIDA de proposito (ver o bloco no fim desta secao).
--
-- Textos: "aparar" aqui e tirar os espacos em branco das pontas (inclusive tabulacao e quebra de linha, como o Trim da API), com
-- REGEXP_REPLACE(x, '^[[:space:]]+|[[:space:]]+$', ''). As comparacoes de variavel com coluna levam COLLATE utf8mb4_unicode_ci (a
-- collation das tabelas), para a procedure nao depender da collation padrao do banco em que foi criada.

-- ------------------------------------------------------------
-- SP_CRIAR_USUARIO: cria o login (USUARIOS) de um funcionario, SEM senha utilizavel.
--
-- Espelha: a criacao do login dentro de enviarConviteDeAcesso (POST /api/rh/usuarios/{funcionarioId}/convite) e de criarFuncionario com
-- enviarConvite = true; codigo em ServicoDeConvite.EmitirAsync. Na API ninguem recebe senha e ninguem grava o hash de outra pessoa: o
-- login nasce com SENHA_PROVISORIA = TRUE e um valor em SENHA que nao confere com senha nenhuma (HashProvisorio: Base64 de 48 bytes
-- aleatorios, o primeiro 0xFF, 64 caracteres); a pessoa escolhe a senha pelo link do convite.
--
-- Regras, na ordem das recusas (a mesma do contrato):
--   1. P_ID_FUNCIONARIO obrigatorio;
--   2. o funcionario existe (a linha fica travada, FOR UPDATE: e o que poe em fila duas criacoes do login da mesma pessoa);
--   3. esta ativo;
--   4. tem e-mail corporativo (e o login dele);
--   5. se ja tem login com a senha definida: recusa (SENHA_JA_DEFINIDA na API: a pessoa usa "Esqueci minha senha");
--   6. se ja tem login desativado: recusa (USUARIO_DESATIVADO na API: o RH ativa antes);
--   7. se ja tem login provisorio e ativo: nao grava nada e devolve o ID_USUARIO que existe (a API tambem so cria "o login que falta").
--
-- O que a API faz e esta procedure NAO: gerar o link do convite (USUARIOS_TOKENS_SENHA guarda so o hash do token; o token em claro so
-- existe no e-mail), enfileirar o e-mail, registrar o evento de auditoria e conferir o perfil de quem chama. Depois desta procedure o RH
-- envia o convite pela API (Usuarios do sistema, "Reenviar convite"): ela acha o login provisorio e emite o link.
--
-- Mudou em relacao ao arquivo antigo: saiu o parametro P_SENHA_HASH (a antiga aceitava qualquer texto de 40 caracteres como senha
-- definida, deixava SENHA_PROVISORIA = FALSE e a pessoa nunca poderia receber o convite); "inexistente" e "inativo" viraram recusas
-- separadas; login que ja existe deixou de estourar a chave unica (erro 1062) e passou a ter as tres respostas acima.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CRIAR_USUARIO(
    IN P_ID_FUNCIONARIO INT,
    OUT P_ID_USUARIO INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_EMAIL VARCHAR(150) DEFAULT NULL;
    DECLARE V_ID_USUARIO INT DEFAULT NULL;
    DECLARE V_USUARIO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_SENHA_PROVISORIA BOOLEAN DEFAULT NULL;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_USUARIO = NULL;

    IF P_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario.';
    END IF;

    START TRANSACTION;

    SELECT ID_FUNCIONARIO, ATIVO, EMAIL_CORPORATIVO
      INTO V_ID_FUNCIONARIO, V_ATIVO, V_EMAIL
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    IF V_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nao e possivel criar usuario para funcionario inativo.';
    END IF;

    IF V_EMAIL IS NULL OR REGEXP_REPLACE(V_EMAIL, '^[[:space:]]+|[[:space:]]+$', '') = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario precisa ter e-mail corporativo para criar o usuario.';
    END IF;

    SELECT ID_USUARIO, ATIVO, SENHA_PROVISORIA
      INTO V_ID_USUARIO, V_USUARIO_ATIVO, V_SENHA_PROVISORIA
      FROM USUARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_USUARIO IS NOT NULL THEN
        IF V_SENHA_PROVISORIA = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario ja tem usuario com a senha definida.';
        END IF;

        IF V_USUARIO_ATIVO = FALSE THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O usuario do funcionario esta desativado. Ative o usuario em vez de criar outro.';
        END IF;
    ELSE
        -- O mesmo formato do HashProvisorio da API: 48 bytes aleatorios com o primeiro 0xFF, em Base64 (64 caracteres, sem quebra de linha).
        INSERT INTO USUARIOS (ID_FUNCIONARIO, SENHA, ATIVO, SENHA_PROVISORIA)
        VALUES (P_ID_FUNCIONARIO, TO_BASE64(CONCAT(0xFF, RANDOM_BYTES(47))), TRUE, TRUE);

        SET V_ID_USUARIO = LAST_INSERT_ID();
    END IF;

    COMMIT;

    SET P_ID_USUARIO = V_ID_USUARIO;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_FUNCIONARIO: cadastra um colaborador (FUNCIONARIOS) e os dados pessoais dele (FUNCIONARIOS_DETALHES) numa transacao.
--
-- Espelha: criarFuncionario (POST /api/rh/funcionarios); codigo em CriarFuncionarioCasoDeUso, CadastroDeFuncionarios.CadastrarAsync,
-- ValidacaoDeCamposDoCadastro, GeradorDeMatriculas e GestorDoFuncionario. Os dados pessoais opcionais (RG, e-mail pessoal e endereco) nao
-- existem no cadastro da API (la entram depois, por editarFuncionario): aqui sao opcionais (NULL = como a API cadastra) e, quando vem,
-- seguem as regras de editarFuncionario (ValidacaoDeCamposDaEdicao).
--
-- Regras, na ordem das recusas (entradas primeiro, depois o estado do banco; a API devolve os erros de campo todos juntos, aqui sai o primeiro):
--   1. nome obrigatorio, ate 150 caracteres (aparado);
--   2. CPF: so os digitos contam (a pontuacao e descartada, como na API); 11 digitos, nao todos iguais, digitos verificadores certos;
--   3. data de nascimento obrigatoria e nao futura;
--   4. telefone obrigatorio: 10 ou 11 digitos, com DDD (so os digitos sao guardados);
--   5. e-mail corporativo obrigatorio, no formato algo@dominio.ext, ate 150 caracteres (guardado aparado e em minusculas);
--   6. data de admissao obrigatoria (pode ser futura: a pessoa fica "em admissao" ate o dia);
--   7. tipo de contrato: CLT ou ESTAGIO;
--   8. opcionais: e-mail pessoal (mesmo formato), RG e orgao emissor (ate 20), CEP (8 digitos, sem pontuacao), UF (uma das 27 siglas),
--      logradouro (150), numero (10), complemento, bairro e cidade (100); texto em branco vale como ausente;
--   9. cargo e setor existem; o nivel de acesso existe e e um dos tres do V005 (RH, Funcionario, Funcionario Gestor);
--  10. CPF ainda nao cadastrado (inclusive em cadastro INATIVO: quem volta a empresa e readmitido, nao cadastrado de novo);
--  11. e-mail corporativo ainda nao cadastrado (idem; sem diferenciar maiusculas nem acentos, como a chave unica);
--  12. o gestor do setor nao pode estar inativo (GESTOR_DO_SETOR_INATIVO na API; o trigger TRG_FUNCIONARIOS_BEFORE_INSERT recusaria o mesmo);
--  13. ainda ha matricula de 5 digitos (o contador nao passa de 99999).
--
-- Efeitos: a MATRICULA e gerada aqui (contador SEQUENCIAS, linha MATRICULA: 00001, 00002...) e devolvida em P_MATRICULA; o ID_GESTOR e o
-- gestor do setor escolhido (NULL se o setor nao tem gestor); ATIVO = TRUE. A linha do contador e travada logo no comeco da transacao:
-- dois cadastros ao mesmo tempo ficam em fila (sem matricula repetida e sem buraco: se o cadastro e recusado, o numero volta no ROLLBACK).
-- Se outro programa gravar o mesmo CPF ou e-mail no meio do caminho, a chave unica do banco decide (erro 1062) e a recusa sai com a
-- mesma mensagem das regras 10 e 11.
--
-- O que a API faz e esta procedure NAO: dar os beneficios escolhidos (beneficioIds), criar o login e enviar o convite (use
-- SP_CRIAR_USUARIO e depois o convite pela API), incluir no onboarding, enviar o formulario de admissao, registrar auditoria e conferir
-- o perfil de quem chama.
--
-- Mudou em relacao ao arquivo antigo: sairam P_MATRICULA de entrada (a matricula e gerada; agora e parametro de SAIDA, no fim) e
-- P_ID_GESTOR (o gestor e derivado do setor); os textos ficaram mais largos so para a recusa por tamanho sair com mensagem propria. A
-- antiga nao validava CPF (so o tamanho), nascimento, telefone, e-mail, contrato, cargo, setor nem nivel, aceitava gestor de qualquer
-- setor, gravava telefone com pontuacao e e-mail com maiusculas e deixava a duplicidade estourar como erro 1062.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CADASTRAR_FUNCIONARIO(
    IN P_NOME VARCHAR(255),
    IN P_DATA_NASCIMENTO DATE,
    IN P_DATA_ADMISSAO DATE,
    IN P_EMAIL_CORPORATIVO VARCHAR(255),
    IN P_ID_CARGO INT,
    IN P_ID_SETOR INT,
    IN P_ID_NIVEL_ACESSO INT,
    IN P_TIPO_CONTRATO VARCHAR(30),
    IN P_CPF VARCHAR(30),
    IN P_RG VARCHAR(60),
    IN P_RG_ORGAO_EMISSOR VARCHAR(60),
    IN P_EMAIL_PESSOAL VARCHAR(255),
    IN P_TELEFONE VARCHAR(30),
    IN P_CEP VARCHAR(20),
    IN P_LOGRADOURO VARCHAR(255),
    IN P_NUMERO VARCHAR(30),
    IN P_COMPLEMENTO VARCHAR(255),
    IN P_BAIRRO VARCHAR(255),
    IN P_CIDADE VARCHAR(255),
    IN P_UF VARCHAR(10),
    OUT P_ID_FUNCIONARIO INT,
    OUT P_MATRICULA VARCHAR(20)
)
BEGIN
    DECLARE V_NOME VARCHAR(255);
    DECLARE V_CPF VARCHAR(30);
    DECLARE V_TELEFONE VARCHAR(30);
    DECLARE V_EMAIL VARCHAR(255);
    DECLARE V_TIPO_CONTRATO VARCHAR(30);
    DECLARE V_EMAIL_PESSOAL VARCHAR(255);
    DECLARE V_RG VARCHAR(60);
    DECLARE V_RG_ORGAO_EMISSOR VARCHAR(60);
    DECLARE V_CEP VARCHAR(20);
    DECLARE V_UF VARCHAR(10);
    DECLARE V_LOGRADOURO VARCHAR(255);
    DECLARE V_NUMERO VARCHAR(30);
    DECLARE V_COMPLEMENTO VARCHAR(255);
    DECLARE V_BAIRRO VARCHAR(255);
    DECLARE V_CIDADE VARCHAR(255);
    DECLARE V_I INT DEFAULT 1;
    DECLARE V_SOMA INT DEFAULT 0;
    DECLARE V_D1 INT DEFAULT 0;
    DECLARE V_D2 INT DEFAULT 0;
    DECLARE V_NOME_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_REPETIDO_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_ID_GESTOR INT DEFAULT NULL;
    DECLARE V_GESTOR_ATIVO BOOLEAN DEFAULT NULL;
    DECLARE V_NUMERO_MATRICULA BIGINT DEFAULT NULL;
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;

    -- A chave unica recusou (CPF ou e-mail corporativo gravados por outro programa durante este cadastro): desfaz e diz qual dado se repete.
    DECLARE EXIT HANDLER FOR 1062
    BEGIN
        ROLLBACK;
        IF EXISTS (SELECT 1 FROM FUNCIONARIOS_DETALHES WHERE CPF = V_CPF COLLATE utf8mb4_unicode_ci) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este CPF.';
        ELSEIF EXISTS (SELECT 1 FROM FUNCIONARIOS WHERE EMAIL_CORPORATIVO = V_EMAIL COLLATE utf8mb4_unicode_ci) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este e-mail corporativo.';
        ELSE
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um registro com estes dados.';
        END IF;
    END;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_FUNCIONARIO = NULL;
    SET P_MATRICULA = NULL;

    -- ---------------- entradas
    SET V_NOME = REGEXP_REPLACE(COALESCE(P_NOME, ''), '^[[:space:]]+|[[:space:]]+$', '');
    IF V_NOME = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome do funcionario.';
    END IF;
    IF CHAR_LENGTH(V_NOME) > 150 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome pode ter ate 150 caracteres.';
    END IF;

    SET V_CPF = REGEXP_REPLACE(COALESCE(P_CPF, ''), '[^0-9]', '');
    -- A lista dos "todos iguais" e escrita por extenso de proposito: no MySQL 8.4, REPEAT(LEFT(V_CPF, 1), 11) sobre uma variavel local
    -- ALTERA a propria variavel (ela passa a valer o digito repetido), e todo CPF sairia como invalido.
    IF V_CPF NOT REGEXP '^[0-9]{11}$'
       OR V_CPF IN ('00000000000', '11111111111', '22222222222', '33333333333', '44444444444',
                    '55555555555', '66666666666', '77777777777', '88888888888', '99999999999') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;
    WHILE V_I <= 9 DO
        SET V_SOMA = V_SOMA + CAST(SUBSTRING(V_CPF, V_I, 1) AS UNSIGNED) * (11 - V_I);
        SET V_I = V_I + 1;
    END WHILE;
    SET V_D1 = (V_SOMA * 10) MOD 11;
    IF V_D1 = 10 THEN SET V_D1 = 0; END IF;
    SET V_SOMA = 0;
    SET V_I = 1;
    WHILE V_I <= 10 DO
        SET V_SOMA = V_SOMA + CAST(SUBSTRING(V_CPF, V_I, 1) AS UNSIGNED) * (12 - V_I);
        SET V_I = V_I + 1;
    END WHILE;
    SET V_D2 = (V_SOMA * 10) MOD 11;
    IF V_D2 = 10 THEN SET V_D2 = 0; END IF;
    IF V_D1 <> CAST(SUBSTRING(V_CPF, 10, 1) AS UNSIGNED) OR V_D2 <> CAST(SUBSTRING(V_CPF, 11, 1) AS UNSIGNED) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;

    IF P_DATA_NASCIMENTO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de nascimento.';
    END IF;
    IF P_DATA_NASCIMENTO > CURDATE() THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A data de nascimento nao pode ser no futuro.';
    END IF;

    SET V_TELEFONE = REGEXP_REPLACE(COALESCE(P_TELEFONE, ''), '[^0-9]', '');
    IF CHAR_LENGTH(V_TELEFONE) NOT IN (10, 11) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o telefone com DDD (10 ou 11 digitos).';
    END IF;

    SET V_EMAIL = LOWER(REGEXP_REPLACE(COALESCE(P_EMAIL_CORPORATIVO, ''), '^[[:space:]]+|[[:space:]]+$', ''));
    IF V_EMAIL = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o e-mail corporativo.';
    END IF;
    IF CHAR_LENGTH(V_EMAIL) > 150 OR V_EMAIL NOT REGEXP '^[^@[:space:]]+@[^@[:space:]]+[.][^@[:space:]]+$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe um e-mail corporativo valido, com ate 150 caracteres.';
    END IF;

    IF P_DATA_ADMISSAO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a data de admissao.';
    END IF;

    -- O valor gravado e sempre o texto exato que a API le ('CLT' ou 'ESTAGIO'), mesmo que chegue em minusculas.
    SET V_TIPO_CONTRATO = CASE UPPER(REGEXP_REPLACE(COALESCE(P_TIPO_CONTRATO, ''), '^[[:space:]]+|[[:space:]]+$', '')) COLLATE utf8mb4_bin
        WHEN 'CLT' THEN 'CLT'
        WHEN 'ESTAGIO' THEN 'ESTAGIO'
        ELSE NULL
    END;
    IF V_TIPO_CONTRATO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o tipo de contrato: CLT ou ESTAGIO.';
    END IF;

    -- ---------------- entradas opcionais (texto em branco vale como ausente)
    SET V_EMAIL_PESSOAL = NULLIF(REGEXP_REPLACE(COALESCE(P_EMAIL_PESSOAL, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF V_EMAIL_PESSOAL IS NOT NULL AND (CHAR_LENGTH(V_EMAIL_PESSOAL) > 150 OR V_EMAIL_PESSOAL NOT REGEXP '^[^@[:space:]]+@[^@[:space:]]+[.][^@[:space:]]+$') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe um e-mail pessoal valido, com ate 150 caracteres.';
    END IF;

    SET V_RG = NULLIF(REGEXP_REPLACE(COALESCE(P_RG, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_RG) > 20 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O RG pode ter ate 20 caracteres.';
    END IF;

    SET V_RG_ORGAO_EMISSOR = NULLIF(REGEXP_REPLACE(COALESCE(P_RG_ORGAO_EMISSOR, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_RG_ORGAO_EMISSOR) > 20 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O orgao emissor do RG pode ter ate 20 caracteres.';
    END IF;

    SET V_CEP = NULLIF(REGEXP_REPLACE(COALESCE(P_CEP, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF V_CEP IS NOT NULL AND V_CEP NOT REGEXP '^[0-9]{8}$' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O CEP tem 8 digitos, sem pontuacao.';
    END IF;

    SET V_UF = UPPER(NULLIF(REGEXP_REPLACE(COALESCE(P_UF, ''), '^[[:space:]]+|[[:space:]]+$', ''), ''));
    IF V_UF IS NOT NULL AND V_UF COLLATE utf8mb4_bin NOT IN ('AC', 'AL', 'AP', 'AM', 'BA', 'CE', 'DF', 'ES', 'GO', 'MA', 'MT', 'MS', 'MG', 'PA', 'PB', 'PR', 'PE', 'PI', 'RJ', 'RN', 'RS', 'RO', 'RR', 'SC', 'SP', 'SE', 'TO') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a sigla do estado (por exemplo SP).';
    END IF;

    SET V_LOGRADOURO = NULLIF(REGEXP_REPLACE(COALESCE(P_LOGRADOURO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_LOGRADOURO) > 150 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O logradouro pode ter ate 150 caracteres.';
    END IF;

    SET V_NUMERO = NULLIF(REGEXP_REPLACE(COALESCE(P_NUMERO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_NUMERO) > 10 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O numero do endereco pode ter ate 10 caracteres.';
    END IF;

    SET V_COMPLEMENTO = NULLIF(REGEXP_REPLACE(COALESCE(P_COMPLEMENTO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_COMPLEMENTO) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O complemento pode ter ate 100 caracteres.';
    END IF;

    SET V_BAIRRO = NULLIF(REGEXP_REPLACE(COALESCE(P_BAIRRO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_BAIRRO) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O bairro pode ter ate 100 caracteres.';
    END IF;

    SET V_CIDADE = NULLIF(REGEXP_REPLACE(COALESCE(P_CIDADE, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_CIDADE) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A cidade pode ter ate 100 caracteres.';
    END IF;

    -- ---------------- estado
    START TRANSACTION;

    -- Primeiro comando da transacao: trava o contador da matricula. Dois cadastros ao mesmo tempo ficam em fila aqui, e as conferencias
    -- abaixo ja enxergam o que o cadastro da frente gravou.
    UPDATE SEQUENCIAS
       SET ULTIMO_VALOR = ULTIMO_VALOR + 1
     WHERE NOME = 'MATRICULA';

    IF ROW_COUNT() = 0 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A linha MATRICULA nao existe na tabela SEQUENCIAS (o script V001 a cria).';
    END IF;

    SELECT ULTIMO_VALOR INTO V_NUMERO_MATRICULA
      FROM SEQUENCIAS
     WHERE NOME = 'MATRICULA';

    IF NOT EXISTS (SELECT 1 FROM CARGOS WHERE ID_CARGO = P_ID_CARGO) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O cargo nao existe.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM SETORES WHERE ID_SETOR = P_ID_SETOR) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O setor nao existe.';
    END IF;

    SELECT NOME_NIVEL_ACESSO INTO V_NOME_NIVEL
      FROM NIVEIS_DE_ACESSO
     WHERE ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO;

    IF V_NOME_NIVEL IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nivel de acesso nao existe.';
    END IF;

    IF V_NOME_NIVEL COLLATE utf8mb4_bin NOT IN ('RH', 'Funcionario', 'Funcionario Gestor') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.';
    END IF;

    SELECT F.ATIVO INTO V_REPETIDO_ATIVO
      FROM FUNCIONARIOS_DETALHES D
      JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = D.ID_FUNCIONARIO
     WHERE D.CPF = V_CPF COLLATE utf8mb4_unicode_ci
     LIMIT 1;

    IF V_REPETIDO_ATIVO = TRUE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este CPF.';
    END IF;
    IF V_REPETIDO_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este CPF pertence a um cadastro inativo. Se a pessoa esta voltando, readmita em vez de cadastrar de novo.';
    END IF;

    SELECT ATIVO INTO V_REPETIDO_ATIVO
      FROM FUNCIONARIOS
     WHERE EMAIL_CORPORATIVO = V_EMAIL COLLATE utf8mb4_unicode_ci
     LIMIT 1;

    IF V_REPETIDO_ATIVO = TRUE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um colaborador com este e-mail corporativo.';
    END IF;
    IF V_REPETIDO_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Este e-mail corporativo pertence a um cadastro inativo. Se a pessoa esta voltando, readmita.';
    END IF;

    -- O gestor da pessoa e o gestor do setor (FOR SHARE: ninguem inativa o gestor nem troca o gestor do setor ate o COMMIT).
    SELECT G.ID_GESTOR, F.ATIVO
      INTO V_ID_GESTOR, V_GESTOR_ATIVO
      FROM SETORES S
      JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
      JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
     WHERE S.ID_SETOR = P_ID_SETOR
       FOR SHARE;

    IF V_GESTOR_ATIVO = FALSE THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O gestor do setor escolhido esta inativo. Troque o gestor do setor antes de continuar.';
    END IF;

    IF V_NUMERO_MATRICULA > 99999 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'As matriculas de 5 digitos acabaram: o contador passaria de 99999.';
    END IF;

    INSERT INTO FUNCIONARIOS (
        NOME, DATA_NASCIMENTO, DATA_ADMISSAO, ATIVO, EMAIL_CORPORATIVO,
        ID_CARGO, ID_SETOR, ID_GESTOR, ID_NIVEL_ACESSO, MATRICULA, TIPO_CONTRATO
    )
    VALUES (
        V_NOME, P_DATA_NASCIMENTO, P_DATA_ADMISSAO, TRUE, V_EMAIL,
        P_ID_CARGO, P_ID_SETOR, V_ID_GESTOR, P_ID_NIVEL_ACESSO, LPAD(V_NUMERO_MATRICULA, 5, '0'), V_TIPO_CONTRATO
    );

    SET V_ID_FUNCIONARIO = LAST_INSERT_ID();

    INSERT INTO FUNCIONARIOS_DETALHES (
        ID_FUNCIONARIO, CPF, RG, RG_ORGAO_EMISSOR, EMAIL_PESSOAL, TELEFONE,
        CEP, LOGRADOURO, NUMERO, COMPLEMENTO, BAIRRO, CIDADE, UF
    )
    VALUES (
        V_ID_FUNCIONARIO, V_CPF, V_RG, V_RG_ORGAO_EMISSOR, V_EMAIL_PESSOAL, V_TELEFONE,
        V_CEP, V_LOGRADOURO, V_NUMERO, V_COMPLEMENTO, V_BAIRRO, V_CIDADE, V_UF
    );

    COMMIT;

    SET P_ID_FUNCIONARIO = V_ID_FUNCIONARIO;
    SET P_MATRICULA = LPAD(V_NUMERO_MATRICULA, 5, '0');
END$$

-- ------------------------------------------------------------
-- SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO: troca o nivel de acesso (o perfil) de um funcionario.
--
-- Espelha: a troca do campo "perfil" em editarFuncionario (PUT /api/rh/funcionarios/{funcionarioId}); codigo em
-- EdicaoDeFuncionarios.ConferirRegrasDaEdicaoAsync. So muda FUNCIONARIOS.ID_NIVEL_ACESSO: promover alguem a Funcionario Gestor NAO cria
-- linha em GESTORES nem lhe da setor (na API isso acontece quando o RH escolhe a pessoa como gestor de um setor, que tambem a promove).
--
-- Regras, na ordem das recusas:
--   1. funcionario e nivel obrigatorios;
--   2. o funcionario existe (linha travada, FOR UPDATE); pode estar inativo (a API tambem edita o cadastro de quem esta inativo);
--   3. o nivel existe e e um dos tres do V005 (RH, Funcionario, Funcionario Gestor): quem tem outro nivel nao consegue entrar no sistema;
--   4. o nivel ja e o pedido: nao grava nada e NAO recusa (na API, salvar a ficha sem mudar o perfil nao e erro);
--   5. quem gerencia um setor (SETORES.ID_GESTOR aponta para a linha dele em GESTORES) precisa continuar Funcionario Gestor
--      (GESTOR_PRECISA_TER_PERFIL_GESTOR na API): o RH tira a pessoa do setor antes.
--
-- O que a API faz e esta procedure NAO: impedir que o RH logado mude o PROPRIO nivel (NAO_PODE_ALTERAR_O_PROPRIO_PERFIL: depende de quem
-- esta logado, e a procedure nao sabe quem a chama), registrar auditoria e conferir o perfil de quem chama. A API nao tem regra de
-- "ultimo RH"; esta procedure tambem nao.
--
-- Mudou em relacao ao arquivo antigo: aceitava qualquer linha de NIVEIS_DE_ACESSO, recusava a troca pelo mesmo nivel (tratava como
-- erro o que nao e), nao distinguia "funcionario nao existe" de "nivel igual" e deixava rebaixar quem gerencia um setor.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO(
    IN P_ID_FUNCIONARIO INT,
    IN P_ID_NIVEL_ACESSO INT
)
BEGIN
    DECLARE V_ID_FUNCIONARIO INT DEFAULT NULL;
    DECLARE V_ID_NIVEL_ATUAL INT DEFAULT NULL;
    DECLARE V_NOME_NIVEL VARCHAR(100) DEFAULT NULL;
    DECLARE V_GERENCIA_SETOR INT DEFAULT 0;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    IF P_ID_FUNCIONARIO IS NULL OR P_ID_NIVEL_ACESSO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o funcionario e o nivel de acesso.';
    END IF;

    START TRANSACTION;

    -- Primeiro comando da transacao (uma leitura com trava enxerga o que ja foi confirmado): a troca de gestor de setor da API tambem
    -- grava nesta linha, entao as duas operacoes ficam em fila.
    SELECT ID_FUNCIONARIO, ID_NIVEL_ACESSO
      INTO V_ID_FUNCIONARIO, V_ID_NIVEL_ATUAL
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO
       FOR UPDATE;

    IF V_ID_FUNCIONARIO IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Funcionario nao encontrado.';
    END IF;

    SELECT NOME_NIVEL_ACESSO INTO V_NOME_NIVEL
      FROM NIVEIS_DE_ACESSO
     WHERE ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO;

    IF V_NOME_NIVEL IS NULL THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso nao encontrado.';
    END IF;

    IF V_NOME_NIVEL COLLATE utf8mb4_bin NOT IN ('RH', 'Funcionario', 'Funcionario Gestor') THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Nivel de acesso desconhecido: use RH, Funcionario ou Funcionario Gestor.';
    END IF;

    IF NOT (V_ID_NIVEL_ATUAL <=> P_ID_NIVEL_ACESSO) THEN
        IF V_NOME_NIVEL COLLATE utf8mb4_bin <> 'Funcionario Gestor' THEN
            SELECT COUNT(*) INTO V_GERENCIA_SETOR
              FROM SETORES S
              JOIN GESTORES G ON G.ID_GESTOR = S.ID_GESTOR
             WHERE G.ID_FUNCIONARIO = P_ID_FUNCIONARIO;

            IF V_GERENCIA_SETOR > 0 THEN
                SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O funcionario gerencia um setor e precisa ter o nivel Funcionario Gestor. Tire-o do setor antes de mudar o nivel.';
            END IF;
        END IF;

        UPDATE FUNCIONARIOS
           SET ID_NIVEL_ACESSO = P_ID_NIVEL_ACESSO
         WHERE ID_FUNCIONARIO = P_ID_FUNCIONARIO;
    END IF;

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_CARGO: cria um cargo.
--
-- Espelha: criarCargo (POST /api/rh/cargos); codigo em CargosCasoDeUso.CriarAsync, ValidacaoDosCadastros e Cargo.Criar.
--
-- Regras, na ordem das recusas:
--   1. nome obrigatorio, de 1 a 100 caracteres (aparado);
--   2. descricao opcional, ate 255 caracteres (em branco vale como ausente);
--   3. carga horaria OBRIGATORIA: e a carga SEMANAL em horas (ex.: 44), maior que zero, de no maximo 168 (as horas de uma semana), com
--      ate duas casas decimais. O ponto de um dia usa a carga semanal dividida por 5;
--   4. nome unico, sem diferenciar maiusculas nem acentos (a chave unica UQ_CARGOS_NOME, utf8mb4_unicode_ci, e quem decide: assim dois
--      cadastros iguais ao mesmo tempo nao passam os dois).
--
-- O que a API faz e esta procedure NAO: registrar auditoria e conferir o perfil de quem chama.
--
-- Mudou em relacao ao arquivo antigo: aceitava cargo SEM carga horaria (o contrato nao aceita, e a API mostraria 0 horas), carga acima
-- de 168 e com mais de duas casas (arredondava em silencio), e o nome repetido estourava como erro 1062. P_CARGA_HORARIA ficou mais
-- largo (DECIMAL(12,6)) so para a procedure enxergar a terceira casa e recusar em vez de arredondar; os textos, idem, para a recusa por
-- tamanho sair com mensagem propria.
-- ------------------------------------------------------------
CREATE PROCEDURE SP_CADASTRAR_CARGO(
    IN P_NOME_CARGO VARCHAR(255),
    IN P_DESCRICAO VARCHAR(1000),
    IN P_CARGA_HORARIA DECIMAL(12,6),
    OUT P_ID_CARGO INT
)
BEGIN
    DECLARE V_NOME VARCHAR(255);
    DECLARE V_DESCRICAO VARCHAR(1000);

    DECLARE EXIT HANDLER FOR 1062
    BEGIN
        ROLLBACK;
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Ja existe um cargo com este nome.';
    END;

    DECLARE EXIT HANDLER FOR SQLEXCEPTION
    BEGIN
        ROLLBACK;
        RESIGNAL;
    END;

    SET P_ID_CARGO = NULL;

    SET V_NOME = REGEXP_REPLACE(COALESCE(P_NOME_CARGO, ''), '^[[:space:]]+|[[:space:]]+$', '');
    IF V_NOME = '' THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe o nome do cargo.';
    END IF;
    IF CHAR_LENGTH(V_NOME) > 100 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'O nome do cargo pode ter ate 100 caracteres.';
    END IF;

    SET V_DESCRICAO = NULLIF(REGEXP_REPLACE(COALESCE(P_DESCRICAO, ''), '^[[:space:]]+|[[:space:]]+$', ''), '');
    IF CHAR_LENGTH(V_DESCRICAO) > 255 THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'A descricao do cargo pode ter ate 255 caracteres.';
    END IF;

    IF P_CARGA_HORARIA IS NULL OR P_CARGA_HORARIA <= 0 OR P_CARGA_HORARIA > 168 OR P_CARGA_HORARIA <> ROUND(P_CARGA_HORARIA, 2) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'Informe a carga semanal em horas: maior que zero, ate 168, com ate duas casas decimais.';
    END IF;

    START TRANSACTION;

    INSERT INTO CARGOS (NOME_CARGO, DESCRICAO, CARGA_HORARIA)
    VALUES (V_NOME, V_DESCRICAO, P_CARGA_HORARIA);

    SET P_ID_CARGO = LAST_INSERT_ID();

    COMMIT;
END$$

-- ------------------------------------------------------------
-- SP_CADASTRAR_NIVEL_ACESSO: REMOVIDA (nao existe no V007).
--
-- A API nao cria niveis de acesso: os tres niveis sao fixos, semeados pelo V005 (RH, Funcionario, Funcionario Gestor), e o codigo
-- converte o NOME do nivel no perfil do login (Perfis.PerfilPorNome). Um nivel com outro nome nao da perfil nenhum: quem o recebesse
-- nao conseguiria entrar ("Funcionario sem nivel ou com nivel desconhecido nao entra", V005). Uma procedure que cria niveis so criaria
-- linhas que o sistema nao sabe usar; por isso ela saiu, e SP_CADASTRAR_FUNCIONARIO e SP_ALTERAR_NIVEL_ACESSO_FUNCIONARIO recusam
-- qualquer nivel fora dos tres.
-- ------------------------------------------------------------


DELIMITER ;
