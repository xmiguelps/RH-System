-- ============================================================
-- RHSystem - TRIGGERS
-- Compatível com o esquema RHSystem_Criacao_Tabelas_MySQL v2
-- Requer MySQL 8.0.16+
-- Executar DEPOIS de criar todas as tabelas.
--
-- ATENÇÃO: este script remove e recria os triggers com estes nomes.
-- Ele não apaga dados das tabelas.
-- ============================================================

USE rh_system;

-- Remover versões anteriores dos triggers deste script
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_AFTER_UPDATE;
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_BEFORE_DELETE;
DROP TRIGGER IF EXISTS TRG_GESTORES_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FUNCIONARIOS_DETALHES_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_USUARIOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_USUARIOS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_AVISOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_AVISOS_AFTER_INSERT;
DROP TRIGGER IF EXISTS TRG_AVISOS_VISUALIZACOES_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_AVISOS_VISUALIZACOES_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_PONTOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_PONTOS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_PONTOS_MANUTENCOES_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_PONTOS_MANUTENCOES_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_HOLERITES_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_HOLERITES_BEFORE_DELETE;
DROP TRIGGER IF EXISTS TRG_ADVERTENCIAS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_ADVERTENCIAS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_ADVERTENCIAS_BEFORE_DELETE;
DROP TRIGGER IF EXISTS TRG_BANCOS_HORAS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_INSERT;
DROP TRIGGER IF EXISTS TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_UPDATE;
DROP TRIGGER IF EXISTS TRG_FORMULARIOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FORMULARIOS_PERGUNTAS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_FORMULARIOS_PERGUNTAS_BEFORE_DELETE;
DROP TRIGGER IF EXISTS TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_BENEFICIOS_FUNCIONARIOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FERIAS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FERIAS_BEFORE_UPDATE;
DROP TRIGGER IF EXISTS TRG_FERIADOS_BEFORE_INSERT;
DROP TRIGGER IF EXISTS TRG_FERIADOS_BEFORE_UPDATE;

DELIMITER $$

-- ============================================================
-- FUNCIONARIOS
-- ============================================================

CREATE TRIGGER TRG_FUNCIONARIOS_BEFORE_INSERT
BEFORE INSERT ON FUNCIONARIOS
FOR EACH ROW
BEGIN
    DECLARE v_gestor_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_func_gestor INT DEFAULT NULL;

    IF NEW.DATA_DESLIGAMENTO IS NOT NULL THEN
        SET NEW.ATIVO = FALSE;
    END IF;

    IF NEW.ID_GESTOR IS NOT NULL AND NEW.ATIVO = TRUE THEN
        SELECT F.ATIVO, F.ID_FUNCIONARIO
          INTO v_gestor_ativo, v_func_gestor
          FROM GESTORES G
          JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
         WHERE G.ID_GESTOR = NEW.ID_GESTOR;

        IF v_func_gestor = NEW.ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Um funcionario nao pode ser gestor de si mesmo.';
        END IF;

        IF v_gestor_ativo = FALSE THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O gestor informado e um funcionario inativo.';
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_FUNCIONARIOS_BEFORE_UPDATE
BEFORE UPDATE ON FUNCIONARIOS
FOR EACH ROW
BEGIN
    DECLARE v_gestor_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_func_gestor INT DEFAULT NULL;

    IF NEW.DATA_DESLIGAMENTO IS NOT NULL THEN
        SET NEW.ATIVO = FALSE;
    END IF;

    IF NEW.ID_GESTOR IS NOT NULL AND NOT (NEW.ID_GESTOR <=> OLD.ID_GESTOR) THEN
        SELECT F.ATIVO, F.ID_FUNCIONARIO
          INTO v_gestor_ativo, v_func_gestor
          FROM GESTORES G
          JOIN FUNCIONARIOS F ON F.ID_FUNCIONARIO = G.ID_FUNCIONARIO
         WHERE G.ID_GESTOR = NEW.ID_GESTOR;

        IF v_func_gestor = OLD.ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Um funcionario nao pode ser gestor de si mesmo.';
        END IF;

        IF v_gestor_ativo = FALSE THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O gestor informado e um funcionario inativo.';
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_FUNCIONARIOS_AFTER_UPDATE
AFTER UPDATE ON FUNCIONARIOS
FOR EACH ROW
BEGIN
    IF OLD.ATIVO = TRUE AND NEW.ATIVO = FALSE THEN
        UPDATE USUARIOS
           SET ATIVO = FALSE
         WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO
           AND ATIVO = TRUE;

        UPDATE BENEFICIOS_FUNCIONARIOS
           SET BENEFICIO_ATIVO = FALSE
         WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO
           AND BENEFICIO_ATIVO = TRUE;

        UPDATE FERIAS
           SET STATUS_FERIAS = 'CANCELADA',
               OBSERVACAO = CONCAT_WS(' | ', OBSERVACAO,
                   'Cancelada automaticamente: funcionario inativado')
         WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO
           AND STATUS_FERIAS IN ('SOLICITADA', 'APROVADA')
           AND DATA_INICIO > CURDATE();
    END IF;
END$$

CREATE TRIGGER TRG_FUNCIONARIOS_BEFORE_DELETE
BEFORE DELETE ON FUNCIONARIOS
FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Exclusao fisica de funcionario nao permitida. Inative o registro.';
END$$

-- ============================================================
-- GESTORES
-- ============================================================

CREATE TRIGGER TRG_GESTORES_BEFORE_INSERT
BEFORE INSERT ON GESTORES
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;

    SELECT ATIVO INTO v_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel cadastrar como gestor um funcionario inativo.';
    END IF;
END$$

-- ============================================================
-- FUNCIONARIOS_DETALHES: CPF e UF
-- ============================================================

CREATE TRIGGER TRG_FUNCIONARIOS_DETALHES_BEFORE_INSERT
BEFORE INSERT ON FUNCIONARIOS_DETALHES
FOR EACH ROW
BEGIN
    DECLARE v_i INT DEFAULT 1;
    DECLARE v_soma INT DEFAULT 0;
    DECLARE v_d1 INT DEFAULT 0;
    DECLARE v_d2 INT DEFAULT 0;

    IF NEW.CPF IS NULL OR NEW.CPF NOT REGEXP '^[0-9]{11}$'
       OR NEW.CPF = REPEAT(LEFT(NEW.CPF, 1), 11) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;

    WHILE v_i <= 9 DO
        SET v_soma = v_soma + CAST(SUBSTRING(NEW.CPF, v_i, 1) AS UNSIGNED) * (11 - v_i);
        SET v_i = v_i + 1;
    END WHILE;
    SET v_d1 = (v_soma * 10) MOD 11;
    IF v_d1 = 10 THEN SET v_d1 = 0; END IF;

    IF v_d1 <> CAST(SUBSTRING(NEW.CPF, 10, 1) AS UNSIGNED) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;

    SET v_soma = 0;
    SET v_i = 1;
    WHILE v_i <= 10 DO
        SET v_soma = v_soma + CAST(SUBSTRING(NEW.CPF, v_i, 1) AS UNSIGNED) * (12 - v_i);
        SET v_i = v_i + 1;
    END WHILE;
    SET v_d2 = (v_soma * 10) MOD 11;
    IF v_d2 = 10 THEN SET v_d2 = 0; END IF;

    IF v_d2 <> CAST(SUBSTRING(NEW.CPF, 11, 1) AS UNSIGNED) THEN
        SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
    END IF;

    IF NEW.UF IS NOT NULL THEN SET NEW.UF = UPPER(NEW.UF); END IF;
END$$

CREATE TRIGGER TRG_FUNCIONARIOS_DETALHES_BEFORE_UPDATE
BEFORE UPDATE ON FUNCIONARIOS_DETALHES
FOR EACH ROW
BEGIN
    DECLARE v_i INT DEFAULT 1;
    DECLARE v_soma INT DEFAULT 0;
    DECLARE v_d1 INT DEFAULT 0;
    DECLARE v_d2 INT DEFAULT 0;

    IF NOT (NEW.CPF <=> OLD.CPF) THEN
        IF NEW.CPF IS NULL OR NEW.CPF NOT REGEXP '^[0-9]{11}$'
           OR NEW.CPF = REPEAT(LEFT(NEW.CPF, 1), 11) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
        END IF;

        WHILE v_i <= 9 DO
            SET v_soma = v_soma + CAST(SUBSTRING(NEW.CPF, v_i, 1) AS UNSIGNED) * (11 - v_i);
            SET v_i = v_i + 1;
        END WHILE;
        SET v_d1 = (v_soma * 10) MOD 11;
        IF v_d1 = 10 THEN SET v_d1 = 0; END IF;

        IF v_d1 <> CAST(SUBSTRING(NEW.CPF, 10, 1) AS UNSIGNED) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
        END IF;

        SET v_soma = 0;
        SET v_i = 1;
        WHILE v_i <= 10 DO
            SET v_soma = v_soma + CAST(SUBSTRING(NEW.CPF, v_i, 1) AS UNSIGNED) * (12 - v_i);
            SET v_i = v_i + 1;
        END WHILE;
        SET v_d2 = (v_soma * 10) MOD 11;
        IF v_d2 = 10 THEN SET v_d2 = 0; END IF;

        IF v_d2 <> CAST(SUBSTRING(NEW.CPF, 11, 1) AS UNSIGNED) THEN
            SIGNAL SQLSTATE '45000' SET MESSAGE_TEXT = 'CPF invalido.';
        END IF;
    END IF;

    IF NEW.UF IS NOT NULL THEN SET NEW.UF = UPPER(NEW.UF); END IF;
END$$

-- ============================================================
-- USUARIOS: apenas funcionarios ativos e senha em formato de hash
-- A aplicacao deve gerar o hash com bcrypt/Argon2.
-- ============================================================

CREATE TRIGGER TRG_USUARIOS_BEFORE_INSERT
BEFORE INSERT ON USUARIOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_email VARCHAR(150) DEFAULT NULL;

    SELECT ATIVO, EMAIL_CORPORATIVO
      INTO v_ativo, v_email
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel criar usuario para funcionario inativo.';
    END IF;

    IF v_email IS NULL OR TRIM(v_email) = '' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'O funcionario precisa ter e-mail corporativo para criar o usuario.';
    END IF;

    IF CHAR_LENGTH(NEW.SENHA) < 40 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Armazene a senha como hash, nunca em texto puro.';
    END IF;
END$$

CREATE TRIGGER TRG_USUARIOS_BEFORE_UPDATE
BEFORE UPDATE ON USUARIOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;

    IF OLD.ATIVO = FALSE AND NEW.ATIVO = TRUE THEN
        SELECT ATIVO INTO v_ativo
          FROM FUNCIONARIOS
         WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

        IF v_ativo = FALSE THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Nao e possivel reativar usuario de funcionario inativo.';
        END IF;
    END IF;

    IF NOT (NEW.SENHA <=> OLD.SENHA) AND CHAR_LENGTH(NEW.SENHA) < 40 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Armazene a senha como hash, nunca em texto puro.';
    END IF;
END$$

-- ============================================================
-- AVISOS e AVISOS_VISUALIZACOES
-- ============================================================

CREATE TRIGGER TRG_AVISOS_BEFORE_INSERT
BEFORE INSERT ON AVISOS
FOR EACH ROW
BEGIN
    IF NEW.DATA_PUBLICACAO IS NULL THEN
        SET NEW.DATA_PUBLICACAO = NOW();
    END IF;

    IF NEW.DATA_EXPIRACAO IS NOT NULL
       AND NEW.DATA_EXPIRACAO < NEW.DATA_PUBLICACAO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A expiracao do aviso nao pode ser anterior a publicacao.';
    END IF;
END$$

CREATE TRIGGER TRG_AVISOS_AFTER_INSERT
AFTER INSERT ON AVISOS
FOR EACH ROW
BEGIN
    INSERT INTO AVISOS_VISUALIZACOES (ID_AVISO, ID_FUNCIONARIO, VISUALIZADO)
    SELECT NEW.ID_AVISO, F.ID_FUNCIONARIO, FALSE
      FROM FUNCIONARIOS F
     WHERE F.ATIVO = TRUE;
END$$

CREATE TRIGGER TRG_AVISOS_VISUALIZACOES_BEFORE_INSERT
BEFORE INSERT ON AVISOS_VISUALIZACOES
FOR EACH ROW
BEGIN
    IF NEW.VISUALIZADO = TRUE AND NEW.DATA_VISUALIZACAO IS NULL THEN
        SET NEW.DATA_VISUALIZACAO = NOW();
    END IF;

    IF NEW.VISUALIZADO = FALSE THEN
        SET NEW.DATA_VISUALIZACAO = NULL;
    END IF;
END$$

CREATE TRIGGER TRG_AVISOS_VISUALIZACOES_BEFORE_UPDATE
BEFORE UPDATE ON AVISOS_VISUALIZACOES
FOR EACH ROW
BEGIN
    IF NEW.VISUALIZADO = TRUE AND NEW.DATA_VISUALIZACAO IS NULL THEN
        SET NEW.DATA_VISUALIZACAO = NOW();
    END IF;

    IF NEW.VISUALIZADO = FALSE THEN
        SET NEW.DATA_VISUALIZACAO = NULL;
    END IF;
END$$

-- ============================================================
-- PONTOS: valida datas e copia a carga horaria diaria do cargo
-- ============================================================
--
-- ALTERADO em 2026-10-29, a pedido do dono, PARA O KAUAN CONFERIR
-- (itens 1 e 2 de docs/pedidos-para-o-kauan.md). Os dois triggers
-- abaixo (INSERT e UPDATE) receberam as MESMAS duas mudancas.
-- Para ver so o que mudou neste arquivo: git diff.
--
-- MUDANCA 1: removido o bloco que usava NEW.HORA_ENTRADA e
-- NEW.HORA_SAIDA.
--   POR QUE: essas colunas sairam de PONTOS no commit e3eb1be. As
--   batidas agora ficam em PONTOS_MARCACOES, com data e hora
--   completas. O MySQL confere os nomes de coluna quando o trigger
--   e criado, entao o CREATE TRIGGER falhava com o erro Unknown
--   column HORA_ENTRADA in NEW (testado no MySQL 8.4.13) e este
--   script parava aqui: criava so 13 dos 36 triggers do arquivo.
--   O QUE O BLOCO FAZIA: (a) recusava saida menor ou igual a
--   entrada e (b) preenchia PONTOS.HORAS_TRABALHADAS.
--   O QUE ACONTECE AGORA COM (a): a ordem entrada e saida deixa de
--   ser conferida por trigger. Quem confere sao as funcoes:
--   FN_VERIFICAR_CONSISTENCIA_PONTO marca o dia como inconsistente
--   e FN_CALCULAR_HORAS_PONTO devolve NULL nesse caso. Como a
--   marcacao tem data e hora, a restricao de virada do dia deste
--   trigger tambem deixa de existir.
--   O QUE ACONTECE AGORA COM (b): o trigger NAO preenche mais
--   PONTOS.HORAS_TRABALHADAS. A coluna e o CHECK de 0 a 24
--   continuam, mas o valor so existe se a aplicacao gravar. As
--   horas do dia saem de FN_CALCULAR_HORAS_PONTO. Nenhum script SQL
--   da pasta Banco de Dados le essa coluna (conferido por busca).
--   PARA VOCE DECIDIR: se quiser a coluna sempre preenchida, o
--   caminho seria um trigger nas marcacoes. Nao fiz isso aqui.
--
-- MUDANCA 2: a carga horaria copiada do cargo agora e dividida por 5.
--   POR QUE: CARGOS.CARGA_HORARIA passou a ser SEMANAL (por exemplo
--   44) e PONTOS.CARGA_HORARIA e POR DIA. Antes o trigger copiava o
--   valor sem dividir, e o ponto ficava com 44 horas de carga do dia.
--   COMO: ROUND(v_carga / 5, 2). Exemplo: 44 / 5 = 8.80. Se o cargo
--   nao tem carga (NULL), o resultado continua NULL.
--   O 5 SIGNIFICA cinco dias de trabalho por semana (segunda a
--   sexta). PARA VOCE DECIDIR: se algum cargo trabalhar seis dias, o
--   5 nao serve e a carga do dia teria que vir de outro lugar.
--   A copia so acontece quando NEW.CARGA_HORARIA vem NULL (como ja
--   era). Se a aplicacao informar a carga do dia, o trigger respeita.
--   LINHAS ANTIGAS: este trigger nao corrige pontos ja gravados com
--   a carga semanal. Se existirem, precisam de um UPDATE a parte.
--
-- O QUE NAO MUDOU: recusar ponto de funcionario inativo, ponto
-- antes da admissao e ponto depois do desligamento.
--
-- COMO CONFERIR (banco de teste):
--   1) o script inteiro de triggers aplica ate o fim, sem erro
--   2) cargo com CARGA_HORARIA 44 e um funcionario nele: INSERT em
--      PONTOS sem informar CARGA_HORARIA grava 8.80
--   3) INSERT em PONTOS com data antes da admissao: recusa
--   4) INSERT em PONTOS informando CARGA_HORARIA 6: fica 6
--
-- Se voce ja tem a sua propria correcao destes triggers, compare:
-- o resultado esperado e o mesmo (sem NEW.HORA_ENTRADA e
-- NEW.HORA_SAIDA, e a carga do cargo dividida por 5).
-- ============================================================

CREATE TRIGGER TRG_PONTOS_BEFORE_INSERT
BEFORE INSERT ON PONTOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_admissao DATE DEFAULT NULL;
    DECLARE v_desligamento DATE DEFAULT NULL;
    DECLARE v_carga DECIMAL(5,2) DEFAULT NULL;

    SELECT F.ATIVO, F.DATA_ADMISSAO, F.DATA_DESLIGAMENTO, C.CARGA_HORARIA
      INTO v_ativo, v_admissao, v_desligamento, v_carga
      FROM FUNCIONARIOS F
      LEFT JOIN CARGOS C ON C.ID_CARGO = F.ID_CARGO
     WHERE F.ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel registrar ponto de funcionario inativo.';
    END IF;

    IF NEW.DATA < v_admissao THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A data do ponto e anterior a admissao.';
    END IF;

    IF v_desligamento IS NOT NULL AND NEW.DATA > v_desligamento THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A data do ponto e posterior ao desligamento.';
    END IF;

    -- Removido em 2026-10-29: o bloco de HORA_ENTRADA e HORA_SAIDA
    -- (colunas que nao existem mais em PONTOS). Ver cabecalho da secao.

    -- Carga do dia = carga semanal do cargo dividida por 5 dias.
    IF NEW.CARGA_HORARIA IS NULL THEN
        SET NEW.CARGA_HORARIA = ROUND(v_carga / 5, 2);
    END IF;
END$$

CREATE TRIGGER TRG_PONTOS_BEFORE_UPDATE
BEFORE UPDATE ON PONTOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_admissao DATE DEFAULT NULL;
    DECLARE v_desligamento DATE DEFAULT NULL;
    DECLARE v_carga DECIMAL(5,2) DEFAULT NULL;

    SELECT F.ATIVO, F.DATA_ADMISSAO, F.DATA_DESLIGAMENTO, C.CARGA_HORARIA
      INTO v_ativo, v_admissao, v_desligamento, v_carga
      FROM FUNCIONARIOS F
      LEFT JOIN CARGOS C ON C.ID_CARGO = F.ID_CARGO
     WHERE F.ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel alterar ponto de funcionario inativo.';
    END IF;

    IF NEW.DATA < v_admissao THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A data do ponto e anterior a admissao.';
    END IF;

    IF v_desligamento IS NOT NULL AND NEW.DATA > v_desligamento THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A data do ponto e posterior ao desligamento.';
    END IF;

    -- Removido em 2026-10-29: o bloco de HORA_ENTRADA e HORA_SAIDA
    -- (colunas que nao existem mais em PONTOS). Ver cabecalho da secao.

    -- Carga do dia = carga semanal do cargo dividida por 5 dias.
    IF NEW.CARGA_HORARIA IS NULL THEN
        SET NEW.CARGA_HORARIA = ROUND(v_carga / 5, 2);
    END IF;
END$$

-- ============================================================
-- PONTOS_MANUTENCOES
-- ============================================================

CREATE TRIGGER TRG_PONTOS_MANUTENCOES_BEFORE_INSERT
BEFORE INSERT ON PONTOS_MANUTENCOES
FOR EACH ROW
BEGIN
    DECLARE v_func_ponto INT DEFAULT NULL;

    SELECT ID_FUNCIONARIO INTO v_func_ponto
      FROM PONTOS
     WHERE ID_PONTO = NEW.ID_PONTO;

    IF v_func_ponto <> NEW.ID_FUNCIONARIO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'O ponto informado pertence a outro funcionario.';
    END IF;

    SET NEW.STATUS_SOLICITACAO = 'PENDENTE';

    IF NEW.DATA_SOLICITACAO IS NULL THEN
        SET NEW.DATA_SOLICITACAO = NOW();
    END IF;
END$$

CREATE TRIGGER TRG_PONTOS_MANUTENCOES_BEFORE_UPDATE
BEFORE UPDATE ON PONTOS_MANUTENCOES
FOR EACH ROW
BEGIN
    DECLARE v_func_gestor INT DEFAULT NULL;

    IF OLD.STATUS_SOLICITACAO <> 'PENDENTE'
       AND NEW.STATUS_SOLICITACAO <> OLD.STATUS_SOLICITACAO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Solicitacao analisada nao pode mudar de status.';
    END IF;

    IF OLD.STATUS_SOLICITACAO = 'PENDENTE'
       AND NEW.STATUS_SOLICITACAO IN ('APROVADA', 'REJEITADA') THEN
        IF NEW.ID_GESTOR IS NULL THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Informe o gestor responsavel pela analise.';
        END IF;

        SELECT ID_FUNCIONARIO INTO v_func_gestor
          FROM GESTORES
         WHERE ID_GESTOR = NEW.ID_GESTOR;

        IF v_func_gestor = NEW.ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O gestor nao pode analisar a propria solicitacao.';
        END IF;

        IF NEW.DATA_ANALISE IS NULL THEN
            SET NEW.DATA_ANALISE = NOW();
        END IF;
    END IF;
END$$

-- ============================================================
-- HOLERITES
-- ============================================================

CREATE TRIGGER TRG_HOLERITES_BEFORE_INSERT
BEFORE INSERT ON HOLERITES
FOR EACH ROW
BEGIN
    DECLARE v_admissao DATE DEFAULT NULL;
    DECLARE v_desligamento DATE DEFAULT NULL;
    DECLARE v_competencia DATE DEFAULT NULL;

    SELECT DATA_ADMISSAO, DATA_DESLIGAMENTO
      INTO v_admissao, v_desligamento
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    SET v_competencia = STR_TO_DATE(CONCAT(NEW.ANO, '-', LPAD(NEW.MES, 2, '0'), '-01'), '%Y-%m-%d');

    IF LAST_DAY(v_competencia) < v_admissao THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A competencia do holerite e anterior a admissao.';
    END IF;

    IF v_desligamento IS NOT NULL AND v_competencia > v_desligamento THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A competencia do holerite e posterior ao desligamento.';
    END IF;

    IF v_competencia > CURDATE() THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel cadastrar holerite de competencia futura.';
    END IF;

    IF NEW.DATA_PUBLICACAO IS NULL THEN
        SET NEW.DATA_PUBLICACAO = NOW();
    END IF;
END$$

CREATE TRIGGER TRG_HOLERITES_BEFORE_DELETE
BEFORE DELETE ON HOLERITES
FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Exclusao fisica de holerite nao permitida.';
END$$

-- ============================================================
-- ADVERTENCIAS
-- ============================================================

CREATE TRIGGER TRG_ADVERTENCIAS_BEFORE_INSERT
BEFORE INSERT ON ADVERTENCIAS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_func_gestor INT DEFAULT NULL;

    SELECT ATIVO INTO v_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel registrar advertencia para funcionario inativo.';
    END IF;

    IF NEW.ID_GESTOR IS NOT NULL THEN
        SELECT ID_FUNCIONARIO INTO v_func_gestor
          FROM GESTORES
         WHERE ID_GESTOR = NEW.ID_GESTOR;

        IF v_func_gestor = NEW.ID_FUNCIONARIO THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'O gestor nao pode advertir a si mesmo.';
        END IF;
    END IF;

    IF NEW.STATUS_ADVERTENCIA IS NULL THEN
        SET NEW.STATUS_ADVERTENCIA = 'PENDENTE';
    END IF;

    IF NEW.DATA_ADVERTENCIA IS NULL THEN
        SET NEW.DATA_ADVERTENCIA = CURDATE();
    END IF;
END$$

CREATE TRIGGER TRG_ADVERTENCIAS_BEFORE_UPDATE
BEFORE UPDATE ON ADVERTENCIAS
FOR EACH ROW
BEGIN
    IF OLD.STATUS_ADVERTENCIA = 'CANCELADA'
       AND NOT (NEW.STATUS_ADVERTENCIA <=> OLD.STATUS_ADVERTENCIA) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Advertencia cancelada nao pode mudar de status.';
    END IF;

    IF OLD.STATUS_ADVERTENCIA = 'APLICADA'
       AND NEW.STATUS_ADVERTENCIA = 'PENDENTE' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Advertencia aplicada nao pode voltar para pendente.';
    END IF;
END$$

CREATE TRIGGER TRG_ADVERTENCIAS_BEFORE_DELETE
BEFORE DELETE ON ADVERTENCIAS
FOR EACH ROW
BEGIN
    SIGNAL SQLSTATE '45000'
        SET MESSAGE_TEXT = 'Exclusao fisica de advertencia nao permitida. Use o status CANCELADA.';
END$$

-- ============================================================
-- BANCOS_HORAS
-- ============================================================

CREATE TRIGGER TRG_BANCOS_HORAS_BEFORE_INSERT
BEFORE INSERT ON BANCOS_HORAS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;

    SELECT ATIVO INTO v_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel lancar banco de horas para funcionario inativo.';
    END IF;

    IF NEW.TIPO_LANCAMENTO IS NULL OR NEW.QUANTIDADE IS NULL THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Informe o tipo e a quantidade do lancamento.';
    END IF;

    IF NEW.DATA_LANCAMENTO IS NULL THEN
        SET NEW.DATA_LANCAMENTO = CURDATE();
    ELSEIF NEW.DATA_LANCAMENTO > CURDATE() THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'A data do lancamento nao pode ser futura.';
    END IF;
END$$

-- ============================================================
-- TREINAMENTOS_FUNCIONARIOS e certificados
-- ============================================================

CREATE TRIGGER TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_INSERT
BEFORE INSERT ON TREINAMENTOS_FUNCIONARIOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;

    SELECT ATIVO INTO v_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel inscrever funcionario inativo em treinamento.';
    END IF;

    IF NEW.STATUS_TREINAMENTO IS NULL THEN
        SET NEW.STATUS_TREINAMENTO = 'INSCRITO';
    END IF;

    IF NEW.STATUS_TREINAMENTO = 'CONCLUIDO' THEN
        IF NEW.NOTA IS NOT NULL AND NEW.NOTA < 70 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Nota abaixo de 70: use o status REPROVADO.';
        END IF;
        IF NEW.DATA_CONCLUSAO IS NULL THEN
            SET NEW.DATA_CONCLUSAO = CURDATE();
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_TREINAMENTOS_FUNCIONARIOS_BEFORE_UPDATE
BEFORE UPDATE ON TREINAMENTOS_FUNCIONARIOS
FOR EACH ROW
BEGIN
    IF OLD.STATUS_TREINAMENTO = 'CONCLUIDO'
       AND NOT (NEW.STATUS_TREINAMENTO <=> OLD.STATUS_TREINAMENTO) THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Treinamento concluido nao pode mudar de status.';
    END IF;

    IF NEW.STATUS_TREINAMENTO = 'CONCLUIDO' THEN
        IF NEW.NOTA IS NOT NULL AND NEW.NOTA < 70 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Nota abaixo de 70: use o status REPROVADO.';
        END IF;
        IF NEW.DATA_CONCLUSAO IS NULL THEN
            SET NEW.DATA_CONCLUSAO = CURDATE();
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_INSERT
AFTER INSERT ON TREINAMENTOS_FUNCIONARIOS
FOR EACH ROW
BEGIN
    IF NEW.STATUS_TREINAMENTO = 'CONCLUIDO' THEN
        INSERT INTO TREINAMENTOS_CERTIFICADOS
            (ID_TREINAMENTO, ID_FUNCIONARIO, DATA_EMISSAO, CODIGO_VALIDACAO)
        VALUES
            (NEW.ID_TREINAMENTO, NEW.ID_FUNCIONARIO, CURDATE(), UUID());
    END IF;
END$$

CREATE TRIGGER TRG_TREINAMENTOS_FUNCIONARIOS_AFTER_UPDATE
AFTER UPDATE ON TREINAMENTOS_FUNCIONARIOS
FOR EACH ROW
BEGIN
    IF NEW.STATUS_TREINAMENTO = 'CONCLUIDO'
       AND NOT (OLD.STATUS_TREINAMENTO <=> NEW.STATUS_TREINAMENTO) THEN
        INSERT INTO TREINAMENTOS_CERTIFICADOS
            (ID_TREINAMENTO, ID_FUNCIONARIO, DATA_EMISSAO, CODIGO_VALIDACAO)
        VALUES
            (NEW.ID_TREINAMENTO, NEW.ID_FUNCIONARIO, CURDATE(), UUID());
    END IF;
END$$

-- ============================================================
-- FORMULARIOS
-- ============================================================

CREATE TRIGGER TRG_FORMULARIOS_BEFORE_INSERT
BEFORE INSERT ON FORMULARIOS
FOR EACH ROW
BEGIN
    IF NEW.DATA_CRIACAO IS NULL THEN
        SET NEW.DATA_CRIACAO = NOW();
    END IF;
END$$

CREATE TRIGGER TRG_FORMULARIOS_PERGUNTAS_BEFORE_UPDATE
BEFORE UPDATE ON FORMULARIOS_PERGUNTAS
FOR EACH ROW
BEGIN
    DECLARE v_qtd INT DEFAULT 0;

    IF NOT (NEW.TEXTO_PERGUNTA <=> OLD.TEXTO_PERGUNTA)
       OR NOT (NEW.TIPO_RESPOSTA <=> OLD.TIPO_RESPOSTA)
       OR NOT (NEW.ID_FORMULARIO <=> OLD.ID_FORMULARIO) THEN
        SELECT COUNT(*) INTO v_qtd
          FROM FORMULARIOS_RESPOSTAS
         WHERE ID_FORMULARIO_PERGUNTA = OLD.ID_FORMULARIO_PERGUNTA;

        IF v_qtd > 0 THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Pergunta ja respondida: nao e possivel alterar texto, tipo ou formulario.';
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_FORMULARIOS_PERGUNTAS_BEFORE_DELETE
BEFORE DELETE ON FORMULARIOS_PERGUNTAS
FOR EACH ROW
BEGIN
    DECLARE v_qtd INT DEFAULT 0;

    SELECT COUNT(*) INTO v_qtd
      FROM FORMULARIOS_RESPOSTAS
     WHERE ID_FORMULARIO_PERGUNTA = OLD.ID_FORMULARIO_PERGUNTA;

    IF v_qtd > 0 THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Pergunta ja respondida: nao e possivel exclui-la.';
    END IF;
END$$

CREATE TRIGGER TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT
BEFORE INSERT ON FORMULARIOS_RESPOSTAS
FOR EACH ROW
BEGIN
    DECLARE v_form_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_func_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_obrigatoria BOOLEAN DEFAULT NULL;

    SELECT ATIVO INTO v_form_ativo
      FROM FORMULARIOS
     WHERE ID_FORMULARIO = NEW.ID_FORMULARIO;

    SELECT ATIVO INTO v_func_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    SELECT OBRIGATORIA INTO v_obrigatoria
      FROM FORMULARIOS_PERGUNTAS
     WHERE ID_FORMULARIO_PERGUNTA = NEW.ID_FORMULARIO_PERGUNTA;

    IF v_form_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Formulario inativo nao aceita respostas.';
    END IF;

    IF v_func_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Funcionario inativo nao pode responder formularios.';
    END IF;

    IF v_obrigatoria = TRUE AND (NEW.RESPOSTA IS NULL OR TRIM(NEW.RESPOSTA) = '') THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Pergunta obrigatoria: informe a resposta.';
    END IF;

    IF NEW.DATA_RESPOSTA IS NULL THEN
        SET NEW.DATA_RESPOSTA = NOW();
    END IF;
END$$

-- ============================================================
-- BENEFICIOS_FUNCIONARIOS
-- ============================================================

CREATE TRIGGER TRG_BENEFICIOS_FUNCIONARIOS_BEFORE_INSERT
BEFORE INSERT ON BENEFICIOS_FUNCIONARIOS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;

    SELECT ATIVO INTO v_ativo
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE AND NEW.BENEFICIO_ATIVO = TRUE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel ativar beneficio para funcionario inativo.';
    END IF;
END$$

-- ============================================================
-- FERIAS
-- As regras entre diferentes registros (sobreposicao/fracionamento)
-- ficam na aplicacao, conforme documentado no script de tabelas.
-- ============================================================

CREATE TRIGGER TRG_FERIAS_BEFORE_INSERT
BEFORE INSERT ON FERIAS
FOR EACH ROW
BEGIN
    DECLARE v_ativo BOOLEAN DEFAULT NULL;
    DECLARE v_admissao DATE DEFAULT NULL;
    DECLARE v_desligamento DATE DEFAULT NULL;

    SELECT ATIVO, DATA_ADMISSAO, DATA_DESLIGAMENTO
      INTO v_ativo, v_admissao, v_desligamento
      FROM FUNCIONARIOS
     WHERE ID_FUNCIONARIO = NEW.ID_FUNCIONARIO;

    IF v_ativo = FALSE THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Funcionario inativo nao pode solicitar ferias.';
    END IF;

    IF NEW.DATA_INICIO_PERIODO_AQUISITIVO < v_admissao THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Periodo aquisitivo nao pode comecar antes da admissao.';
    END IF;

    IF NEW.DATA_INICIO <= NEW.DATA_FIM_PERIODO_AQUISITIVO THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Inicio das ferias deve ser posterior ao fim do periodo aquisitivo.';
    END IF;

    IF v_desligamento IS NOT NULL AND NEW.DATA_INICIO > v_desligamento THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Funcionario desligado nao pode iniciar ferias depois do desligamento.';
    END IF;

    IF NEW.DATA_SOLICITACAO IS NULL THEN
        SET NEW.DATA_SOLICITACAO = NOW();
    END IF;

    IF NEW.STATUS_FERIAS IS NULL THEN
        SET NEW.STATUS_FERIAS = 'SOLICITADA';
    END IF;
END$$

CREATE TRIGGER TRG_FERIAS_BEFORE_UPDATE
BEFORE UPDATE ON FERIAS
FOR EACH ROW
BEGIN
    DECLARE v_func_gestor INT DEFAULT NULL;

    IF NOT (NEW.STATUS_FERIAS <=> OLD.STATUS_FERIAS) THEN
        IF NOT (
               (OLD.STATUS_FERIAS = 'SOLICITADA' AND NEW.STATUS_FERIAS IN ('APROVADA', 'REJEITADA', 'CANCELADA'))
            OR (OLD.STATUS_FERIAS = 'APROVADA' AND NEW.STATUS_FERIAS IN ('EM_GOZO', 'CONCLUIDA', 'CANCELADA'))
            OR (OLD.STATUS_FERIAS = 'EM_GOZO' AND NEW.STATUS_FERIAS = 'CONCLUIDA')
        ) THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Transicao de status de ferias invalida.';
        END IF;

        IF NEW.STATUS_FERIAS IN ('APROVADA', 'REJEITADA') THEN
            IF NEW.ID_GESTOR IS NULL THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'Informe o gestor responsavel pela analise das ferias.';
            END IF;

            SELECT ID_FUNCIONARIO INTO v_func_gestor
              FROM GESTORES
             WHERE ID_GESTOR = NEW.ID_GESTOR;

            IF v_func_gestor = NEW.ID_FUNCIONARIO THEN
                SIGNAL SQLSTATE '45000'
                    SET MESSAGE_TEXT = 'O gestor nao pode analisar as proprias ferias.';
            END IF;

            IF NEW.DATA_ANALISE IS NULL THEN
                SET NEW.DATA_ANALISE = NOW();
            END IF;
        END IF;
    END IF;

    IF NEW.DATA_SOLICITACAO IS NULL THEN
        SET NEW.DATA_SOLICITACAO = NOW();
    END IF;
END$$

-- ============================================================
-- FERIADOS
-- ============================================================

CREATE TRIGGER TRG_FERIADOS_BEFORE_INSERT
BEFORE INSERT ON FERIADOS
FOR EACH ROW
BEGIN
    IF NEW.UF IS NOT NULL THEN SET NEW.UF = UPPER(NEW.UF); END IF;

    IF NEW.TIPO_FERIADO IN ('NACIONAL', 'EMPRESA') THEN
        SET NEW.UF = NULL;
        SET NEW.MUNICIPIO = NULL;
    ELSEIF NEW.TIPO_FERIADO = 'ESTADUAL' THEN
        IF NEW.UF IS NULL OR TRIM(NEW.UF) = '' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Feriado estadual exige UF.';
        END IF;
        SET NEW.MUNICIPIO = NULL;
    ELSEIF NEW.TIPO_FERIADO = 'MUNICIPAL' THEN
        IF NEW.UF IS NULL OR TRIM(NEW.UF) = ''
           OR NEW.MUNICIPIO IS NULL OR TRIM(NEW.MUNICIPIO) = '' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Feriado municipal exige UF e municipio.';
        END IF;
    END IF;
END$$

CREATE TRIGGER TRG_FERIADOS_BEFORE_UPDATE
BEFORE UPDATE ON FERIADOS
FOR EACH ROW
BEGIN
    IF NEW.UF IS NOT NULL THEN SET NEW.UF = UPPER(NEW.UF); END IF;

    IF NEW.TIPO_FERIADO IN ('NACIONAL', 'EMPRESA') THEN
        SET NEW.UF = NULL;
        SET NEW.MUNICIPIO = NULL;
    ELSEIF NEW.TIPO_FERIADO = 'ESTADUAL' THEN
        IF NEW.UF IS NULL OR TRIM(NEW.UF) = '' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Feriado estadual exige UF.';
        END IF;
        SET NEW.MUNICIPIO = NULL;
    ELSEIF NEW.TIPO_FERIADO = 'MUNICIPAL' THEN
        IF NEW.UF IS NULL OR TRIM(NEW.UF) = ''
           OR NEW.MUNICIPIO IS NULL OR TRIM(NEW.MUNICIPIO) = '' THEN
            SIGNAL SQLSTATE '45000'
                SET MESSAGE_TEXT = 'Feriado municipal exige UF e municipio.';
        END IF;
    END IF;
END$$

DELIMITER ;

-- ============================================================
-- FIM DO SCRIPT DE TRIGGERS
-- ============================================================
