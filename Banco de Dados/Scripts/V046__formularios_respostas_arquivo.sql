-- ============================================================
-- V046 - Arquivo enviado numa resposta de formulario
-- Fatia 5 (formularios). Lacuna achada na analise do contrato.
-- ============================================================
-- POR QUE
--   O tipo de pergunta ARQUIVO (envio de arquivo) esta entre os cinco que o dono
--   manteve (2026-10-29). O contrato devolve a resposta com
--   arquivo { respostaId, nome } e baixa o arquivo por link temporario
--   (contrato/fontes/50-fatia-5-formularios.yaml, RespostaFormularioDada,
--   baixarArquivoDeResposta). O arquivo fica no Blob Storage e o banco guarda
--   so a referencia (spec, secao 4). Mas FORMULARIOS_RESPOSTAS so tem RESPOSTA
--   (texto): os V015 a V019 nao criaram onde guardar o caminho do arquivo nem o
--   nome original. Sem isso a API nao acha o arquivo para baixar e o job que
--   apaga os arquivos de um formulario excluido ou de um candidato vencido nao
--   sabe quais apagar.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta duas colunas em FORMULARIOS_RESPOSTAS, no mesmo padrao de
--   FUNCIONARIOS_DOCUMENTOS (V022) e ATESTADOS (V023):
--   - CAMINHO_ARQUIVO: onde o arquivo esta guardado (referencia do Blob),
--   - NOME_ARQUIVO_ORIGINAL: o nome que a pessoa deu, para mostrar e para o download.
--   e dois CHECKs:
--   - caminho e nome andam juntos,
--   - resposta de formulario ANONIMO nunca tem arquivo (o arquivo identifica quem
--     respondeu, e o contrato proibe pergunta de arquivo em formulario anonimo).
--     E a segunda barreira, depois da conferencia da aplicacao, como o ANONIMO da
--     resposta no V017.
--
-- COMO A RESPOSTA DE ARQUIVO FICA (para a aplicacao)
--   O trigger TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT recusa pergunta obrigatoria
--   com RESPOSTA vazia. Por isso, como na alternativa escolhida (V016), a
--   aplicacao grava o NOME do arquivo tambem em RESPOSTA, junto com as duas
--   colunas novas.
--
-- PRIVACIDADE
--   O arquivo de um candidato (V017) e dado pessoal e entra na retencao do
--   candidato (roadmap, decisao 13): ao apagar um candidato vencido, a aplicacao
--   apaga as respostas dele e os arquivos que elas citam.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE FORMULARIOS_RESPOSTAS
    ADD COLUMN CAMINHO_ARQUIVO VARCHAR(255) NULL,
    ADD COLUMN NOME_ARQUIVO_ORIGINAL VARCHAR(255) NULL,

    ADD CONSTRAINT CHK_FORMULARIOS_RESPOSTAS_ARQUIVO_JUNTOS
        CHECK ((CAMINHO_ARQUIVO IS NULL) = (NOME_ARQUIVO_ORIGINAL IS NULL)),

    -- ANONIMO e NOT NULL (V017), entao a comparacao nunca da NULL
    ADD CONSTRAINT CHK_FORMULARIOS_RESPOSTAS_ANONIMA_SEM_ARQUIVO
        CHECK (ANONIMO = FALSE OR CAMINHO_ARQUIVO IS NULL);
