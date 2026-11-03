-- ============================================================
-- V041 - Motivo da recusa das ferias (FERIAS.MOTIVO_RECUSA)
-- Fatia 7 (gestao). Item declarado no contrato.
-- ============================================================
-- POR QUE
--   Quando o gestor ou o RH recusa um pedido de ferias, o motivo e obrigatorio
--   (5 a 500 caracteres), a pessoa o ve no historico e no e-mail, e ele
--   aparece em decisaoGestor.observacao ou decisaoRh.observacao
--   (contrato/fontes/70-fatia-7-gestao.yaml, FeriasRecusarComando). O contrato
--   diz que o motivo mora numa coluna nova de FERIAS. A coluna OBSERVACAO que
--   ja existe nao serve: ela guarda a mensagem que a pessoa escreveu ao pedir
--   (ou que o RH escreveu ao registrar) e recebe o texto "Cancelada
--   automaticamente: funcionario inativado" do trigger de inativacao
--   (TRG_FUNCIONARIOS_AFTER_UPDATE), entao misturaria tres coisas.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta MOTIVO_RECUSA (TEXT, como nas tabelas V021, V023, V024 e V025)
--   e tres CHECKs:
--   - o motivo so existe com o pedido REJEITADO,
--   - o motivo nao e vazio (so espacos),
--   - uma recusa registrada pelo gestor (RESULTADO_GESTOR = RECUSOU) ou pelo RH
--     (ID_FUNCIONARIO_RH preenchido) tem motivo.
--
-- POR QUE UMA COLUNA SO
--   Quem recusa e o gestor OU o RH, nunca os dois: se o gestor recusa, o
--   pedido acaba na hora (CHK_FERIAS_RECUSA_ENCERRA, V011) e nao chega ao RH.
--   O contrato sabe de quem e o motivo por RESULTADO_GESTOR (gestor) ou por
--   ID_FUNCIONARIO_RH (RH).
--
-- POR QUE O ULTIMO CHECK TEM UMA SAIDA
--   Ferias REJEITADA que ja existiam antes das colunas da etapa do gestor
--   (V011) nao tem RESULTADO_GESTOR nem ID_FUNCIONARIO_RH e nao tem motivo. O
--   CHECK deixa essas linhas passarem, para o script nao falhar sobre dados
--   antigos, e exige o motivo de toda recusa nova.
--
-- SE O SCRIPT FALHAR (CHK_FERIAS_RECUSA_COM_MOTIVO violado)
--   Existem ferias REJEITADA com etapa do gestor ou do RH registrada e sem motivo.
--   Antes da aplicacao existir isso so pode ser dado de teste. Nada e alterado
--   quando falha (a coluna e os tres CHECKs entram numa instrucao so). Apague esses
--   pedidos de teste e rode de novo. Veja quais sao com
--     SELECT ID_FERIAS FROM FERIAS WHERE STATUS_FERIAS = 'REJEITADA'
--        AND (RESULTADO_GESTOR IS NOT NULL OR ID_FUNCIONARIO_RH IS NOT NULL)
--
-- O QUE FICA COM A APLICACAO
--   - Gravar o motivo no mesmo UPDATE que muda o status para REJEITADA (o CHECK
--     vale por instrucao).
--   - O tamanho de 5 a 500 caracteres (o banco confere so "nao vazio").
--   - Texto, nunca HTML (roadmap, decisao 6).
--   - O cancelamento da sugestao de data (RESPOSTA_SUGESTAO = RECUSOU ou
--     EXPIROU, V020) NAO tem motivo e fica CANCELADA, nao REJEITADA.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE FERIAS
    ADD COLUMN MOTIVO_RECUSA TEXT NULL,

    ADD CONSTRAINT CHK_FERIAS_MOTIVO_SO_REJEITADA
        CHECK (MOTIVO_RECUSA IS NULL OR STATUS_FERIAS = 'REJEITADA'),

    ADD CONSTRAINT CHK_FERIAS_MOTIVO_NAO_VAZIO
        CHECK (MOTIVO_RECUSA IS NULL OR TRIM(MOTIVO_RECUSA) <> ''),

    -- STATUS_FERIAS, RESULTADO_GESTOR e ID_FUNCIONARIO_RH so entram em
    -- comparacoes que nao dao NULL (IS NULL, IS NOT NULL e <> com NOT NULL)
    ADD CONSTRAINT CHK_FERIAS_RECUSA_COM_MOTIVO
        CHECK (STATUS_FERIAS <> 'REJEITADA'
               OR MOTIVO_RECUSA IS NOT NULL
               OR (RESULTADO_GESTOR IS NULL AND ID_FUNCIONARIO_RH IS NULL));
