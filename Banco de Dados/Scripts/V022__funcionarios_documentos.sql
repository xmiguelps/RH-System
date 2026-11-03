-- ============================================================
-- V022 - Documentos do colaborador (FUNCIONARIOS_DOCUMENTOS)
-- Item 17 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O Figma (ficha do colaborador, aba Documentos, e o modal "Adicionar
--   documento") tem uma lista de documentos por pessoa, cada um com tipo,
--   validade, descricao, arquivo e situacao (Enviado ou Pendente). O RH
--   pode "Anexar agora" (ja tem o arquivo) ou "Pedir para a pessoa enviar"
--   (ela recebe um aviso e envia pelo portal) e "Lembrar" quem ainda nao
--   enviou. A pessoa tambem tem a tela "Meus documentos".
--   O dono decidiu (2026-10-29) que isso entra e que SO O RH E O PROPRIO
--   COLABORADOR veem (nem o gestor).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria FUNCIONARIOS_DOCUMENTOS, uma linha por documento:
--   - TIPO_DOCUMENTO: texto. A lista de tipos ainda nao foi decidida
--     pelo dono, entao nao ha CHECK. Quando for, e so um script novo.
--   - STATUS_DOCUMENTO: PENDENTE (pedido, ainda sem arquivo) ou ENVIADO.
--   - CAMINHO_ARQUIVO: onde o arquivo esta guardado (Blob Storage, o
--     banco guarda so a referencia, como em HOLERITES e atestados).
--     NOME_ARQUIVO_ORIGINAL guarda o nome que a pessoa deu, para o
--     download.
--   - DATA_PEDIDO (quando o RH pediu), DATA_ENVIO e DATA_VALIDADE
--     (opcional, alimenta o aviso de documento vencendo).
--
-- O QUE O BANCO GARANTE (CHECK)
--   Documento ENVIADO tem arquivo e data de envio.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Quem pode ver: so RH e o proprio colaborador (e a aplicacao que
--     filtra, o banco nao tem permissao por linha).
--   - Tipo e tamanho do arquivo (o Figma diz PDF, JPG ou PNG, ate 10 MB).
--   - O lembrete ("Lembrar") e o e-mail.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29). Por
-- isso a data de cadastro usa DEFAULT CURRENT_TIMESTAMP, que e o jeito de
-- ter "data automatica" sem trigger.
-- ============================================================

CREATE TABLE FUNCIONARIOS_DOCUMENTOS (
    ID_FUNCIONARIO_DOCUMENTO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FUNCIONARIO INT NOT NULL,
    TIPO_DOCUMENTO VARCHAR(50) NOT NULL,
    DESCRICAO VARCHAR(255) NULL,
    DATA_VALIDADE DATE NULL,
    STATUS_DOCUMENTO VARCHAR(10) NOT NULL DEFAULT 'PENDENTE',
    CAMINHO_ARQUIVO VARCHAR(255) NULL,
    NOME_ARQUIVO_ORIGINAL VARCHAR(255) NULL,
    DATA_PEDIDO DATETIME NULL,
    DATA_ENVIO DATETIME NULL,
    DATA_CADASTRO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_FUNCIONARIOS_DOCUMENTOS_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_FUNCIONARIOS_DOCUMENTOS_STATUS CHECK (STATUS_DOCUMENTO IN ('PENDENTE', 'ENVIADO')),
    CONSTRAINT CHK_FUNCIONARIOS_DOCUMENTOS_ENVIADO_COM_ARQUIVO
        CHECK (STATUS_DOCUMENTO <> 'ENVIADO' OR (CAMINHO_ARQUIVO IS NOT NULL AND DATA_ENVIO IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
