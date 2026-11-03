-- ============================================================
-- V012 - Holerites: tipo, versao e ajuste de UM trigger
-- Item 9 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- ESTE E O UNICO SCRIPT DESTA SERIE QUE ALTERA UM TRIGGER DO GRUPO.
-- Foi autorizado pelo dono em 2026-10-29.
--
-- POR QUE
--   1) TIPO. Em dezembro a mesma pessoa tem o holerite do mes e o do
--      13o, e ha o holerite de ferias. A chave unica de hoje
--      UQ_HOLERITES_FUNCIONARIO_COMPETENCIA (ID_FUNCIONARIO, ANO, MES)
--      so permite UM holerite por pessoa por mes.
--   2) VERSAO. Se o RH enviar um holerite errado, nao da para apagar
--      (TRG_HOLERITES_BEFORE_DELETE recusa todo DELETE, e nao vou
--      mexer nisso). O jeito certo e guardar VERSOES: o holerite
--      corrigido entra como versao 2, o funcionario ve so a versao
--      atual (a maior) e o RH ve o historico. Decisao da sessao, por
--      delegacao do dono, ele pode discordar.
--   3) FERIAS. O holerite de ferias e pago ANTES de as ferias
--      comecarem, entao a competencia dele pode estar no futuro. O
--      trigger de hoje recusa qualquer holerite de competencia
--      futura. O dono escolheu ajustar o trigger.
--
-- O QUE ESTE SCRIPT FAZ
--   a) Acrescenta TIPO_HOLERITE (MENSAL, DECIMO_TERCEIRO, FERIAS,
--      padrao MENSAL, valores PROPOSTOS, confira) e VERSAO (padrao 1).
--      As linhas que ja existem viram MENSAL, versao 1.
--   b) Troca a chave unica por (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE,
--      VERSAO). ORDEM IMPORTANTE: cria a nova ANTES de remover a antiga.
--      O MySQL usa a chave unica antiga como indice da FK de
--      ID_FUNCIONARIO, se removessemos primeiro, daria erro 1553. A nova
--      tambem comeca por ID_FUNCIONARIO, entao assume esse papel.
--   c) Recria TRG_HOLERITES_BEFORE_INSERT. A UNICA mudanca e a linha
--      marcada com "<<< MUDANCA" abaixo: a recusa de competencia futura
--      passa a valer so para os tipos diferentes de FERIAS. Todo o resto
--      do trigger esta IGUAL ao original (V002). Compare os dois.
--
-- CUIDADO
--   Se alguem rodar de novo, na mao, o script ORIGINAL de triggers
--   (V002), ele recria o trigger antigo e desfaz a mudanca (o proprio
--   script avisa que remove e recria os triggers). Depois do primeiro
--   deploy os scripts ficam congelados e rodam em ordem, entao
--   V002 -> V012 sempre termina com o trigger novo.
--
-- LIMITES DESTE DESENHO (para conferir)
--   - VERSAO e CORRECAO: a versao 2 substitui a 1 para o funcionario. Se um
--     dia houver dois holerites LEGITIMOS do mesmo tipo e mes (por exemplo
--     adiantamento quinzenal, ou as duas parcelas do 13o na mesma
--     competencia), o segundo NAO deve virar "versao 2": precisa de outro
--     valor em TIPO_HOLERITE.
--   - Este script tem varias instrucoes e o DDL do MySQL nao desfaz. Se ele
--     falhar entre o DROP TRIGGER e o CREATE TRIGGER, HOLERITES fica SEM o
--     trigger de INSERT: rode o CREATE TRIGGER (no fim deste arquivo) na mao.
--
-- O QUE FICA COM A APLICACAO
--   - A proxima versao e MAX(VERSAO) + 1 da mesma pessoa, competencia e
--     tipo. O banco so garante que nao se repete.
--   - O funcionario enxerga so a maior versao de cada (ano, mes, tipo).
-- ============================================================

ALTER TABLE HOLERITES
    ADD COLUMN TIPO_HOLERITE VARCHAR(20) NOT NULL DEFAULT 'MENSAL',
    ADD COLUMN VERSAO SMALLINT NOT NULL DEFAULT 1,
    ADD CONSTRAINT CHK_HOLERITES_TIPO CHECK (TIPO_HOLERITE IN ('MENSAL', 'DECIMO_TERCEIRO', 'FERIAS')),
    ADD CONSTRAINT CHK_HOLERITES_VERSAO CHECK (VERSAO >= 1);

-- cria a chave nova ANTES de remover a antiga (ver explicacao acima)
ALTER TABLE HOLERITES
    ADD CONSTRAINT UQ_HOLERITES_COMPETENCIA_TIPO_VERSAO
        UNIQUE (ID_FUNCIONARIO, ANO, MES, TIPO_HOLERITE, VERSAO);

ALTER TABLE HOLERITES
    DROP INDEX UQ_HOLERITES_FUNCIONARIO_COMPETENCIA;

-- ------------------------------------------------------------
-- Trigger: aceitar competencia futura quando o tipo e FERIAS
-- ------------------------------------------------------------
DROP TRIGGER IF EXISTS TRG_HOLERITES_BEFORE_INSERT;

DELIMITER $$

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

    -- <<< MUDANCA: antes era apenas "IF v_competencia > CURDATE() THEN".
    -- O holerite de ferias e pago antes das ferias, entao pode ter
    -- competencia futura. Os outros tipos continuam recusados.
    IF v_competencia > CURDATE() AND NEW.TIPO_HOLERITE <> 'FERIAS' THEN
        SIGNAL SQLSTATE '45000'
            SET MESSAGE_TEXT = 'Nao e possivel cadastrar holerite de competencia futura.';
    END IF;

    IF NEW.DATA_PUBLICACAO IS NULL THEN
        SET NEW.DATA_PUBLICACAO = NOW();
    END IF;
END$$

DELIMITER ;
