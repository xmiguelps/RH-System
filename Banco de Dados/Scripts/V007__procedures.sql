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
-- (preenchido pela secao A)






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
-- (preenchido pela secao C)






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- (preenchido pela secao D)






-- [[DOMINIO-E: CADASTROS]]
-- (preenchido pela secao E)






DELIMITER ;
