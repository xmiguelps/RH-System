-- ============================================================
-- V044 - Nome do arquivo do modulo (TREINAMENTOS_MODULOS.NOME_ARQUIVO_ORIGINAL)
-- Fatia 6 (TAD). Lacuna achada na analise do contrato.
-- ============================================================
-- POR QUE
--   O modulo de tipo PDF tem um arquivo no Blob Storage (V026 guarda o caminho em
--   CAMINHO_ARQUIVO). O contrato devolve ao RH o nome do arquivo no campo
--   nomeArquivo de cada modulo (contrato/fontes/60-fatia-6-tad.yaml, schema
--   TreinamentoModuloRh, exemplo "canais-de-denuncia.pdf"), para a tela de
--   edicao mostrar o que foi enviado. O V026 nao guarda esse nome. As tabelas
--   irmas guardam: FUNCIONARIOS_DOCUMENTOS (V022) e ATESTADOS (V023) tem
--   NOME_ARQUIVO_ORIGINAL.
--   Tirar o nome do caminho do Blob so serve se a aplicacao sempre gravar o arquivo
--   com o nome original no fim do caminho, o que o banco nao garante nem o
--   contrato diz.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta NOME_ARQUIVO_ORIGINAL (o nome que a pessoa deu ao arquivo) e um
--   CHECK: so ha nome quando ha arquivo.
--
-- DECISOES QUE VOCE PODE REVER
--   1) A coluna aceita NULL: modulo de texto ou de link nao tem arquivo, e linhas
--      antigas com caminho (se existirem) nao tem o nome.
--   2) O CHECK nao exige o nome quando ha caminho, para nao falhar sobre linha
--      antiga. Quem exige e a aplicacao ao gravar.
--
-- O QUE FICA COM A APLICACAO
--   Gravar CAMINHO_ARQUIVO e NOME_ARQUIVO_ORIGINAL juntos ao enviar o PDF, e
--   devolver o nome ao RH (nunca o caminho, que e interno).
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE TREINAMENTOS_MODULOS
    ADD COLUMN NOME_ARQUIVO_ORIGINAL VARCHAR(255) NULL,
    ADD CONSTRAINT CHK_TREINAMENTOS_MODULOS_NOME_ARQUIVO
        CHECK (NOME_ARQUIVO_ORIGINAL IS NULL OR CAMINHO_ARQUIVO IS NOT NULL);
