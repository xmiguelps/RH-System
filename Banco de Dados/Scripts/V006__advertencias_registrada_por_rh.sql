-- ============================================================
-- V006 - Quem do RH registrou a advertencia (ADVERTENCIAS.ID_FUNCIONARIO_RH)
-- Decisao do dono, 2026-11-17 (contrato 0.3.0: AdvertenciaDetalheRh.registradaPor)
-- ============================================================
-- POR QUE
--   Quando o RH registra uma advertencia, a lista e o detalhe mostram so "RH"
--   (ou o gestor, quando o RH registra em nome dele): o banco nao guardava QUAL
--   pessoa do RH registrou. O dono quer que o RH saiba, e que o colaborador e o
--   gestor nao vejam.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta ADVERTENCIAS.ID_FUNCIONARIO_RH, anulavel, com chave estrangeira
--   para FUNCIONARIOS:
--   - preenchida pela API quando o RH registra (nos dois jeitos de registrar:
--     "aplicada pelo gestor da pessoa" e "aplicada pelo RH");
--   - NULL quando o gestor registra pela propria tela dele e nas advertencias
--     que ja existiam antes desta coluna (nao ha como saber quem foi).
--   Nao muda nenhuma linha existente, nenhum trigger e nenhuma outra tabela.
--
-- COMO A APLICACAO USA
--   So o detalhe do RH (GET /api/rh/advertencias/{id} e as respostas de registrar
--   e de responder a contestacao) devolve o campo "registradaPor". A lista do RH,
--   o gestor e a propria pessoa nunca o recebem.
-- ============================================================

ALTER TABLE ADVERTENCIAS
    ADD COLUMN ID_FUNCIONARIO_RH INT NULL;

ALTER TABLE ADVERTENCIAS
    ADD CONSTRAINT FK_ADVERTENCIAS_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO);
