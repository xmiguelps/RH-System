-- ============================================================
-- V020 - Ferias: 13o adiantado e "sugerir outra data"
-- Item 14 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   - O Figma (pedir ferias) tem a opcao "adiantar a 1a parcela do 13o".
--   - O gestor pode SUGERIR OUTRA DATA em vez de aprovar ou recusar. O
--     dono decidiu (2026-10-29) o fluxo:
--       1) o gestor sugere outras datas (RESULTADO_GESTOR = SUGERIU_DATA,
--          que ja foi criado em V011),
--       2) a pessoa ACEITA: as datas sugeridas viram DATA_INICIO e
--          DATA_FIM, e o pedido segue para o RH confirmar,
--       3) ou RECUSA: o pedido ENCERRA (CANCELADA) e ela e orientada a
--          conversar melhor com o gestor antes de abrir outro,
--       4) ou NAO RESPONDE: a sugestao EXPIRA depois de um tempo e o
--          pedido tambem vira CANCELADA.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta em FERIAS:
--   - ADIANTAR_13_SALARIO (sim/nao, padrao nao),
--   - SUGESTAO_DATA_INICIO e SUGESTAO_DATA_FIM: as datas sugeridas,
--   - RESPOSTA_SUGESTAO: ACEITOU, RECUSOU ou EXPIROU, e a data dela.
--
-- POR QUE NAO CRIEI STATUS NOVO
--   O pedido continua SOLICITADA enquanto espera a resposta da pessoa
--   (decisao do dono: sem status novos, sem mexer em trigger).
--   CANCELADA ja e aceita a partir de SOLICITADA
--   (TRG_FERIAS_BEFORE_UPDATE). A coluna RESPOSTA_SUGESTAO (RECUSOU ou
--   EXPIROU) e o que distingue esse cancelamento do cancelamento por
--   inativacao do funcionario (que so anota OBSERVACAO).
--
-- O QUE O BANCO GARANTE (CHECK)
--   - as datas sugeridas obedecem as MESMAS regras das datas normais:
--     5 a 30 dias (como CHK_FERIAS_DURACAO) e depois do fim do periodo
--     aquisitivo (como CHK_FERIAS_INICIO_APOS_AQUISITIVO),
--   - ha datas sugeridas se e somente se o resultado do gestor e
--     SUGERIU_DATA,
--   - so ha resposta se houve sugestao, resposta e data andam juntas,
--   - RECUSOU ou EXPIROU so existem com status CANCELADA.
--
-- O QUE FICA COM A APLICACAO
--   - Ao ACEITAR: copiar as datas sugeridas para DATA_INICIO e
--     DATA_FIM (os CHECK de hoje valem no UPDATE).
--   - EXPIRAR: o agendador cancela e grava EXPIROU. PRAZO PROPOSTO (a
--     ajustar com o dono): 5 dias corridos depois da sugestao ou 2 dias
--     antes da data de inicio sugerida, o que vier primeiro.
--   - Orientar a pessoa a conversar com o gestor.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE FERIAS
    ADD COLUMN ADIANTAR_13_SALARIO BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN SUGESTAO_DATA_INICIO DATE NULL,
    ADD COLUMN SUGESTAO_DATA_FIM DATE NULL,
    ADD COLUMN RESPOSTA_SUGESTAO VARCHAR(10) NULL,
    ADD COLUMN DATA_RESPOSTA_SUGESTAO DATETIME NULL,

    ADD CONSTRAINT CHK_FERIAS_RESPOSTA_SUGESTAO
        CHECK (RESPOSTA_SUGESTAO IS NULL OR RESPOSTA_SUGESTAO IN ('ACEITOU', 'RECUSOU', 'EXPIROU')),

    ADD CONSTRAINT CHK_FERIAS_SUGESTAO_DATAS_JUNTAS
        CHECK ((SUGESTAO_DATA_INICIO IS NULL) = (SUGESTAO_DATA_FIM IS NULL)),

    ADD CONSTRAINT CHK_FERIAS_SUGESTAO_DURACAO
        CHECK (SUGESTAO_DATA_INICIO IS NULL
               OR DATEDIFF(SUGESTAO_DATA_FIM, SUGESTAO_DATA_INICIO) + 1 BETWEEN 5 AND 30),

    ADD CONSTRAINT CHK_FERIAS_SUGESTAO_APOS_AQUISITIVO
        CHECK (SUGESTAO_DATA_INICIO IS NULL OR SUGESTAO_DATA_INICIO > DATA_FIM_PERIODO_AQUISITIVO),

    -- ha datas sugeridas <=> o gestor sugeriu (COALESCE: ver nota em V011)
    ADD CONSTRAINT CHK_FERIAS_SUGESTAO_E_RESULTADO
        CHECK ((COALESCE(RESULTADO_GESTOR, '') = 'SUGERIU_DATA') = (SUGESTAO_DATA_INICIO IS NOT NULL)),

    ADD CONSTRAINT CHK_FERIAS_RESPOSTA_SO_COM_SUGESTAO
        CHECK (RESPOSTA_SUGESTAO IS NULL OR SUGESTAO_DATA_INICIO IS NOT NULL),

    ADD CONSTRAINT CHK_FERIAS_RESPOSTA_E_DATA
        CHECK ((RESPOSTA_SUGESTAO IS NULL) = (DATA_RESPOSTA_SUGESTAO IS NULL)),

    -- recusou ou expirou => o pedido ja esta cancelado
    ADD CONSTRAINT CHK_FERIAS_SUGESTAO_NAO_ACEITA_CANCELA
        CHECK (RESPOSTA_SUGESTAO IS NULL OR RESPOSTA_SUGESTAO = 'ACEITOU' OR STATUS_FERIAS = 'CANCELADA');
