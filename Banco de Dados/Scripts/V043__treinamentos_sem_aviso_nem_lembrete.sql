-- ============================================================
-- V043 - Treinamentos: as caixas de aviso e de lembrete passam a nascer FALSE
-- Fatia 6 (TAD). Conflito entre o V029 e o contrato.
-- ============================================================
-- POR QUE
--   O V029 criou AVISA_POR_EMAIL ("Avisar por e-mail ao publicar") e
--   LEMBRA_ANTES_DO_FIM ("Lembrar quem nao comecou 7 dias antes do fim") com
--   padrao VERDADEIRO, porque na epoca o agendador as consultaria. O dono
--   decidiu depois (2026-10-30, CONVENCOES.md, secoes 12 e 13) que:
--   - o sistema NAO manda e-mail de treinamento publicado nem lembrete de
--     treinamento (a lista de e-mails e fechada, o RH usa um Aviso),
--   - treinamento NAO tem prazo final, entao nao ha "7 dias antes do fim".
--   O contrato (contrato/fontes/60-fatia-6-tad.yaml, criarTreinamento) diz que
--   as duas colunas e DATA_FIM "deixam de ter uso" e que a API grava FALSE
--   nelas, explicitamente.
--
-- O PROBLEMA QUE ISTO RESOLVE
--   Com padrao VERDADEIRO no banco e a aplicacao querendo gravar FALSE, o EF Core
--   pode omitir o valor FALSE (e o valor padrao do tipo bool) e o banco gravar
--   VERDADEIRO sem nenhum erro. E a armadilha do roadmap, decisao 12 (a). Aqui
--   ela nao precisa de cuidado no mapeamento: com padrao FALSE, omitir o valor e
--   gravar FALSE dao o mesmo resultado.
--
-- O QUE ESTE SCRIPT FAZ
--   1) Troca o padrao das duas colunas para FALSE.
--   2) Marca FALSE os treinamentos que ja existiam (o V029 os deixou TRUE), para
--      nenhuma leitura futura encontrar "avisar" ligado onde nada avisa.
--
-- DECISOES QUE VOCE PODE REVER
--   - NAO removi as colunas (nem DATA_FIM). O dono pode preferir apagar o que
--     o contrato nao usa. Remover e um ALTER TABLE ... DROP COLUMN num script
--     novo, e e seguro enquanto nada as ler.
--   - EMITE_CERTIFICADO e INCLUI_ADMITIDOS continuam com padrao VERDADEIRO: o
--     contrato diz que o padrao delas e verdadeiro (TreinamentoComando), mas a
--     aplicacao ainda precisa gravar o valor recebido sem declarar o padrao no
--     mapeamento do EF, como pede o LEIA-ME.md.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE TREINAMENTOS
    ALTER COLUMN AVISA_POR_EMAIL SET DEFAULT FALSE,
    ALTER COLUMN LEMBRA_ANTES_DO_FIM SET DEFAULT FALSE;

UPDATE TREINAMENTOS
   SET AVISA_POR_EMAIL = FALSE,
       LEMBRA_ANTES_DO_FIM = FALSE;
