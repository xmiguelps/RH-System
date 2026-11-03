-- ============================================================
-- V042 - Treinamento encerrado (TREINAMENTOS.ENCERRADO_EM)
-- Fatia 6 (TAD) e fatia 7 (painel do RH). Item declarado no contrato.
-- ============================================================
-- POR QUE
--   Treinamento nao tem prazo final (CONVENCOES.md, secao 13): o RH publica e,
--   quando quiser, ENCERRA. Encerrado quer dizer que ninguem novo entra, quem ja
--   comecou termina, e a situacao do treinamento passa a ENCERRADO
--   (contrato/fontes/60-fatia-6-tad.yaml, encerrarTreinamento e
--   TreinamentoSituacao). A fatia 7 conta so os treinamentos "em andamento",
--   que sao os publicados, ja liberados e nao encerrados (painel do RH). O V029
--   guarda PUBLICADO, e DATA_FIM nao serve (o contrato a deixa sem uso), entao
--   falta onde dizer "encerrado e quando".
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta ENCERRADO_EM (DATETIME, NULL enquanto nao encerrado) e um CHECK:
--   so se encerra o que foi publicado. A situacao continua sem coluna propria:
--     RASCUNHO     = PUBLICADO falso
--     AGENDADO     = PUBLICADO verdadeiro, DATA_INICIO no futuro, ENCERRADO_EM vazio
--     EM_ANDAMENTO = PUBLICADO verdadeiro, DATA_INICIO ja chegou, ENCERRADO_EM vazio
--     ENCERRADO    = ENCERRADO_EM preenchido
--
-- FUSO
--   ENCERRADO_EM e DATETIME no fuso -03:00 (regra de fuso do roadmap, decisao
--   4), a API converte para UTC em encerradoEmUtc. TREINAMENTOS nao tem trigger,
--   entao a aplicacao grava o valor (ou usa NOW(), que com o servidor em
--   -03:00 da o mesmo).
--
-- DECISOES QUE VOCE PODE REVER
--   1) Encerrar e uma via de mao unica na aplicacao: o contrato nao tem
--      "reabrir". O banco nao impede voltar ENCERRADO_EM para NULL (nao ha
--      trigger), a regra e da aplicacao.
--   2) O CHECK usa PUBLICADO = TRUE de proposito: PUBLICADO e NOT NULL (V029).
--
-- O QUE FICA COM A APLICACAO
--   - Quem ja estava EM_ANDAMENTO continua e termina. Quem nao tinha comecado
--     deixa de ver o treinamento (contrato: TREINAMENTO_ENCERRADO).
--   - Os certificados ja emitidos continuam valendo.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE TREINAMENTOS
    ADD COLUMN ENCERRADO_EM DATETIME NULL,
    ADD CONSTRAINT CHK_TREINAMENTOS_ENCERRADO_PUBLICADO
        CHECK (ENCERRADO_EM IS NULL OR PUBLICADO = TRUE);
