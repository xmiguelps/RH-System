-- ============================================================
-- V013 - Avisos: fixar no topo e rascunho
-- Item 10 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   - O Figma permite FIXAR um aviso no topo da lista.
--   - O dono quer rascunho de aviso: guardar sem publicar. A ideia
--     foi dele: uma coluna ENVIADO (sim/nao) em AVISOS. Criado e
--     enviado de uma vez grava ENVIADO = verdadeiro, "salvar
--     rascunho" grava ENVIADO = falso.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta FIXADO (padrao falso) e ENVIADO em AVISOS.
--
-- POR QUE O PADRAO E FALSO E HA UM UPDATE NO FIM
--   O padrao e FALSO: quem esquecer de marcar cria um rascunho, e nao
--   publica sem querer. Mas os avisos que JA EXISTEM foram publicados,
--   entao o UPDATE final marca todos como enviados. A ordem foi pensada
--   para FALHAR SEGURO: se o UPDATE se perder, os avisos antigos viram
--   rascunho (um problema visivel) e nenhum aviso novo nasce publicado sem
--   querer. (O contrario, criar com padrao VERDADEIRO e trocar depois,
--   deixaria o padrao errado se a segunda instrucao se perdesse.)
--
-- SEM MEXER NOS TRIGGERS (decisao do dono, 2026-10-29)
--   AVISOS so tem triggers de INSERT, nada trava o UPDATE que envia o
--   rascunho. Cuidados que ficam com a APLICACAO:
--   1) Listar e contar so ENVIADO = verdadeiro. A funcao
--      FN_CONTAR_AVISOS_NAO_LIDOS nao conhece ENVIADO e contaria o
--      rascunho, entao a aplicacao NAO a usa para avisos.
--   2) Ao enviar o rascunho, atualizar DATA_PUBLICACAO: o trigger a
--      preenche com NOW() na CRIACAO, que seria a data do rascunho.
--   3) O trigger TRG_AVISOS_AFTER_INSERT cria a linha de leitura de
--      todos os funcionarios ativos ja no rascunho. E inofensivo (a
--      aplicacao nao depende dessas linhas ate o envio), mas voce vai
--      ver essas linhas no banco.
--   O aviso AGENDADO e ENVIADO = verdadeiro com DATA_PUBLICACAO no
--   futuro.
-- ============================================================

ALTER TABLE AVISOS
    ADD COLUMN FIXADO BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ENVIADO BOOLEAN NOT NULL DEFAULT FALSE;

-- os avisos que ja existiam estavam publicados (AVISOS nao tem trigger de UPDATE)
UPDATE AVISOS SET ENVIADO = TRUE;
