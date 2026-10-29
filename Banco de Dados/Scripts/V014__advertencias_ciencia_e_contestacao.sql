-- ============================================================
-- V014 - Advertencias: ciencia e contestacao
-- Item 11 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O funcionario confirma a CIENCIA da advertencia ou CONTESTA com
--   um texto, e o RH analisa. Hoje ADVERTENCIAS nao guarda nada disso
--   e o status so aceita PENDENTE, APLICADA e CANCELADA.
--
-- O FLUXO QUE ESTE SCRIPT PERMITE
--   PENDENTE  (aguardando a ciencia)
--     -> APLICADA    (a pessoa deu ciencia)
--     -> CONTESTADA  (a pessoa contestou, com texto)
--   CONTESTADA, depois da analise do RH:
--     -> APLICADA    (contestacao negada, advertencia mantida)
--     -> CANCELADA   (contestacao aceita)
--   O trigger TRG_ADVERTENCIAS_BEFORE_UPDATE JA PERMITE tudo isso (so
--   bloqueia sair de CANCELADA e voltar de APLICADA para PENDENTE).
--   Por isso NAO mexi em trigger.
--   ATENCAO: o trigger permite MAIS do que o fluxo acima (tambem deixa
--   APLICADA virar CONTESTADA e CONTESTADA voltar para PENDENTE). A
--   aplicacao nao deve fazer isso. Uma SEGUNDA contestacao sobrescreve o
--   texto da primeira (a primeira fica so no log de auditoria). Tambem nao
--   ha coluna para "quem do RH analisou e quando": o log guarda.
--
-- O QUE ESTE SCRIPT FAZ
--   1) Acrescenta DATA_CIENCIA, TEXTO_CONTESTACAO e DATA_CONTESTACAO.
--   2) Troca o CHECK de status para aceitar tambem CONTESTADA. O MySQL
--      nao altera CHECK: remove e recria.
--   3) CHECKs de coerencia: texto e data da contestacao andam juntos,
--      status CONTESTADA exige o texto.
--
-- NAO FIZ (decisao sua, Kauan)
--   A resposta do RH a contestacao vai POR E-MAIL (decidido). Guardar o
--   texto dessa resposta no banco e opcional, nao criei coluna. Se
--   quiser, e so acrescentar RESPOSTA_RH TEXT depois.
-- ============================================================

ALTER TABLE ADVERTENCIAS
    ADD COLUMN DATA_CIENCIA DATETIME NULL,
    ADD COLUMN TEXTO_CONTESTACAO TEXT NULL,
    ADD COLUMN DATA_CONTESTACAO DATETIME NULL;

-- troca do CHECK numa instrucao so: se fosse em duas e a segunda se
-- perdesse, STATUS_ADVERTENCIA ficaria sem CHECK nenhum
ALTER TABLE ADVERTENCIAS
    DROP CHECK CHK_ADVERTENCIAS_STATUS,
    ADD CONSTRAINT CHK_ADVERTENCIAS_STATUS
        CHECK (STATUS_ADVERTENCIA IS NULL OR STATUS_ADVERTENCIA IN ('PENDENTE', 'APLICADA', 'CANCELADA', 'CONTESTADA')),
    ADD CONSTRAINT CHK_ADVERTENCIAS_CONTESTACAO_JUNTAS
        CHECK ((TEXTO_CONTESTACAO IS NULL) = (DATA_CONTESTACAO IS NULL)),
    ADD CONSTRAINT CHK_ADVERTENCIAS_CONTESTADA_COM_TEXTO
        CHECK (STATUS_ADVERTENCIA IS NULL OR STATUS_ADVERTENCIA <> 'CONTESTADA' OR TEXTO_CONTESTACAO IS NOT NULL);
