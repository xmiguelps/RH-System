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
-- Horarios: o banco guarda DATETIME no relogio de Brasilia (-03:00). NOW() ja e esse relogio (o servidor roda com default-time-zone=-03:00);
-- os horarios que entram por parametro tambem sao de Brasilia.

-- ------------------------------------------------------------
-- SP_REGISTRAR_MARCACAO_PONTO: a pessoa bate o ponto AGORA.
-- Espelha: operationId registrarMarcacaoDePonto (POST /api/eu/ponto/marcacoes), RegistrarMarcacaoCasoDeUso + SequenciaDoDia.ProximaMarcacao.
--
-- Parametros: P_ID_FUNCIONARIO; P_TIPO_MARCACAO = a batida que a pessoa ACHA que esta fazendo ('ENTRADA' ou 'SAIDA', o tipoEsperado da API);
--             OUT P_ID_MARCACAO = a batida criada.
-- O horario e sempre o do servidor (NOW(), lido depois da trava, sem fracao de segundo); o tipo e o dia do ponto quem calcula e a procedure:
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
    SET V_AGORA = NOW();
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

    SET V_AGORA_EXATO = NOW(6);
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
-- (preenchido pela secao B)






-- [[DOMINIO-C: HOLERITE, ATESTADO E BENEFICIOS]]
-- (preenchido pela secao C)






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- (preenchido pela secao D)






-- [[DOMINIO-E: CADASTROS]]
-- (preenchido pela secao E)






DELIMITER ;
