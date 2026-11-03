-- ============================================================
-- V011 - Aprovacao em duas etapas (gestor e depois RH)
--        em PONTOS_MANUTENCOES e FERIAS, mais a chave de
--        aprovacao automatica em GESTORES
-- Item 8 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O dono decidiu (2026-10-28 e 2026-10-29) que ajuste de ponto e
--   ferias passam por DUAS etapas: primeiro o gestor, depois o RH
--   confirma. Hoje as duas tabelas tem uma unica aprovacao.
--
-- A REGRA MAIS IMPORTANTE (e o motivo do desenho)
--   O dono mandou NAO criar status novos e NAO mexer nos triggers.
--   Os triggers de hoje so aceitam as transicoes que ja existem:
--   - PONTOS_MANUTENCOES: qualquer status diferente de PENDENTE e
--     final (TRG_PONTOS_MANUTENCOES_BEFORE_UPDATE),
--   - FERIAS: so valem SOLICITADA -> APROVADA/REJEITADA/CANCELADA etc.
--     (TRG_FERIAS_BEFORE_UPDATE), e a inativacao do funcionario so
--     cancela ferias SOLICITADA e APROVADA (TRG_FUNCIONARIOS_AFTER_UPDATE).
--   Entao o STATUS NAO MUDA na etapa do gestor. O pedido fica
--   PENDENTE (ponto) ou SOLICITADA (ferias) durante as duas etapas, e a
--   etapa do gestor fica registrada em COLUNAS NOVAS. O status so muda
--   na DECISAO FINAL (APROVADA ou REJEITADA).
--
-- COMO AS COLUNAS SE ENCAIXAM
--   - ID_GESTOR (ja existia): passa a ser gravado NA ETAPA DO GESTOR.
--     E o gestor que analisou ou, na aprovacao automatica, o gestor
--     ausente. A decisao final do trigger exige um ID_GESTOR
--     preenchido e diferente da propria pessoa, por isso ele ja
--     precisa estar gravado desde a etapa do gestor.
--   - RESULTADO_GESTOR: APROVOU ou RECUSOU (e SUGERIU_DATA, so em ferias).
--   - DATA_ETAPA_GESTOR: quando o gestor decidiu.
--   - ETAPA_GESTOR_AUTOMATICA: verdadeiro quando quem "decidiu" foi a
--     chave de aprovacao automatica (gestor ausente).
--   - ID_FUNCIONARIO_RH e DATA_CONFIRMACAO_RH: quem do RH confirmou e
--     quando. (Nao confundir com ID_GESTOR.)
--   - DATA_ANALISE (ja existia): continua sendo a data da DECISAO
--     FINAL, o trigger a preenche com NOW() quando vem vazia. Nao criei
--     outra coluna para isso, de proposito.
--
-- CASOS ESPECIAIS (decididos pelo dono em 2026-10-29)
--   - O GESTOR RECUSA: o pedido acaba na hora (REJEITADA, com o
--     ID_GESTOR dele). O CHECK abaixo garante: RESULTADO_GESTOR =
--     'RECUSOU' so existe com status REJEITADA, e o RH nao confirma
--     um pedido que o gestor recusou.
--   - PEDIDO DE QUEM E GESTOR: nao tem etapa do gestor (RESULTADO_GESTOR
--     fica NULL), so o RH decide. Como a decisao final exige um
--     ID_GESTOR diferente de quem pediu, a pessoa do RH que decide
--     esse pedido precisa estar tambem cadastrada em GESTORES, e so um
--     cadastro, nao muda nenhum trigger. ATENCAO: se a unica pessoa
--     do RH for tambem gestora, ninguem consegue decidir os pedidos
--     dela.
--   - Por isso NAO coloquei o CHECK opcional "status final APROVADA
--     exige etapa do gestor preenchida": ele travaria o pedido de quem
--     e gestor (que nao tem etapa do gestor).
--   - ACOES DIRETAS DO RH (registrar ferias pela ficha, corrigir uma
--     batida) ficam FORA da segunda etapa: o INSERT de FERIAS nao valida
--     status inicial nem gestor, entao o RH pode registrar ja como
--     APROVADA. Nada a fazer aqui.
--
-- CHAVE DE APROVACAO AUTOMATICA (GESTORES)
--   O RH liga quando o gestor esta ausente. Vale POR GESTOR (toda a
--   equipe dele, ferias e ajuste de ponto). Enquanto estiver ligada, os
--   pedidos da equipe recebem RESULTADO_GESTOR = APROVOU, marcados como
--   automaticos. Ao ligar, os pedidos que ja esperavam o gestor passam
--   tambem. APROVACAO_AUTOMATICA_ATE e a DATA DE VOLTA do gestor
--   (opcional): a chave vale ATE O DIA ANTERIOR a ela. No proprio dia da
--   volta ela ja NAO vale (o gestor esta de volta).
--   A APLICACAO decide se a chave vale: ligada E (sem data OU hoje <
--   data). Nao deixei por conta de um agendador desligar a coluna,
--   porque se o agendador atrasar a aprovacao automatica continuaria
--   valendo depois da data de volta. Nao ha trigger nem EVENT do MySQL.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

-- ------------------------------------------------------------
-- GESTORES: chave de aprovacao automatica
-- ------------------------------------------------------------
ALTER TABLE GESTORES
    ADD COLUMN APROVACAO_AUTOMATICA BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN APROVACAO_AUTOMATICA_ATE DATE NULL,
    ADD CONSTRAINT CHK_GESTORES_APROVACAO_AUTOMATICA_ATE
        CHECK (APROVACAO_AUTOMATICA = TRUE OR APROVACAO_AUTOMATICA_ATE IS NULL);

-- ------------------------------------------------------------
-- PONTOS_MANUTENCOES: etapa do gestor e confirmacao do RH
-- ------------------------------------------------------------
ALTER TABLE PONTOS_MANUTENCOES
    ADD COLUMN RESULTADO_GESTOR VARCHAR(20) NULL,
    ADD COLUMN DATA_ETAPA_GESTOR DATETIME NULL,
    ADD COLUMN ETAPA_GESTOR_AUTOMATICA BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ID_FUNCIONARIO_RH INT NULL,
    ADD COLUMN DATA_CONFIRMACAO_RH DATETIME NULL,

    ADD CONSTRAINT FK_PONTOS_MANUTENCOES_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_RESULTADO_GESTOR
        CHECK (RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR IN ('APROVOU', 'RECUSOU')),
    -- resultado e data da etapa andam juntos
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_ETAPA_GESTOR
        CHECK ((RESULTADO_GESTOR IS NULL) = (DATA_ETAPA_GESTOR IS NULL)),
    -- a etapa do gestor so existe com o gestor identificado (ID_GESTOR).
    -- Sem isto, o erro "informe o gestor" so apareceria na decisao final,
    -- quando a tentacao e preencher com quem do RH confirma
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_ETAPA_COM_GESTOR
        CHECK (RESULTADO_GESTOR IS NULL OR ID_GESTOR IS NOT NULL),
    -- so existe aprovacao automatica de quem aprovou
    -- (COALESCE de proposito: no MySQL um CHECK que da NULL PASSA, sem o
    --  COALESCE, "automatica = verdadeiro e resultado vazio" passaria)
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_AUTOMATICA
        CHECK (ETAPA_GESTOR_AUTOMATICA = FALSE OR COALESCE(RESULTADO_GESTOR, '') = 'APROVOU'),
    -- gestor recusou => pedido rejeitado na hora
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_RECUSA_ENCERRA
        CHECK (RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR <> 'RECUSOU' OR STATUS_SOLICITACAO = 'REJEITADA'),
    -- quem do RH confirmou e quando andam juntos
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_CONFIRMACAO_RH
        CHECK ((ID_FUNCIONARIO_RH IS NULL) = (DATA_CONFIRMACAO_RH IS NULL)),
    -- o RH nao confirma o que o gestor recusou
    ADD CONSTRAINT CHK_PONTOS_MANUTENCOES_RH_APOS_GESTOR
        CHECK (ID_FUNCIONARIO_RH IS NULL OR RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR <> 'RECUSOU');

-- ------------------------------------------------------------
-- FERIAS: etapa do gestor e confirmacao do RH
-- (SUGERIU_DATA e o "sugerir outra data" do gestor, as colunas da
--  sugestao e a resposta da pessoa ficam no script V020)
-- ------------------------------------------------------------
ALTER TABLE FERIAS
    ADD COLUMN RESULTADO_GESTOR VARCHAR(20) NULL,
    ADD COLUMN DATA_ETAPA_GESTOR DATETIME NULL,
    ADD COLUMN ETAPA_GESTOR_AUTOMATICA BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ID_FUNCIONARIO_RH INT NULL,
    ADD COLUMN DATA_CONFIRMACAO_RH DATETIME NULL,

    ADD CONSTRAINT FK_FERIAS_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    ADD CONSTRAINT CHK_FERIAS_RESULTADO_GESTOR
        CHECK (RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR IN ('APROVOU', 'RECUSOU', 'SUGERIU_DATA')),
    ADD CONSTRAINT CHK_FERIAS_ETAPA_GESTOR
        CHECK ((RESULTADO_GESTOR IS NULL) = (DATA_ETAPA_GESTOR IS NULL)),
    ADD CONSTRAINT CHK_FERIAS_ETAPA_COM_GESTOR
        CHECK (RESULTADO_GESTOR IS NULL OR ID_GESTOR IS NOT NULL),
    ADD CONSTRAINT CHK_FERIAS_AUTOMATICA
        CHECK (ETAPA_GESTOR_AUTOMATICA = FALSE OR COALESCE(RESULTADO_GESTOR, '') = 'APROVOU'),
    ADD CONSTRAINT CHK_FERIAS_RECUSA_ENCERRA
        CHECK (RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR <> 'RECUSOU' OR STATUS_FERIAS = 'REJEITADA'),
    ADD CONSTRAINT CHK_FERIAS_CONFIRMACAO_RH
        CHECK ((ID_FUNCIONARIO_RH IS NULL) = (DATA_CONFIRMACAO_RH IS NULL)),
    ADD CONSTRAINT CHK_FERIAS_RH_APOS_GESTOR
        CHECK (ID_FUNCIONARIO_RH IS NULL OR RESULTADO_GESTOR IS NULL OR RESULTADO_GESTOR <> 'RECUSOU');
