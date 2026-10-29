-- ============================================================
-- V025 - Trajeto do vale-transporte (TRAJETOS_VALE_TRANSPORTE)
-- Item 20 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O vale-transporte depende do trajeto que o funcionario informa (o
--   Figma mostra "Trajeto: 2 onibus por dia" e o botao "Atualizar
--   trajeto", em "Seus pedidos" aparece "Atualizacao do trajeto do
--   vale-transporte" com situacao).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria TRAJETOS_VALE_TRANSPORTE. Dados aprovados pelo dono em
--   2026-10-29: origem, destino e as linhas ou meios de transporte (se
--   faltar campo, vira um script novo).
--
-- HISTORICO E "TRAJETO ATUAL"
--   Atualizar o trajeto e um PEDIDO novo, que o RH aprova. Por isso cada
--   atualizacao e uma LINHA NOVA, e o trajeto em vigor e a linha
--   APROVADA mais recente da adesao, a de maior DATA_RESPOSTA (nao
--   sobrescreve). Assim a aplicacao
--   mantem o que valia ate o RH aprovar o novo. As colunas de situacao
--   sao as mesmas de V021 e V024.
--
-- PRIVACIDADE (importante)
--   ORIGEM e normalmente o ENDERECO DE CASA: so o RH e o proprio
--   funcionario veem, o gestor NAO ve (a aplicacao filtra).
--
-- LIGACAO
--   Com a adesao ao beneficio (BENEFICIOS_FUNCIONARIOS), como os
--   dependentes (V024).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE TRAJETOS_VALE_TRANSPORTE (
    ID_TRAJETO INT AUTO_INCREMENT PRIMARY KEY,
    ID_BENEFICIO_FUNCIONARIOS INT NOT NULL,
    ORIGEM VARCHAR(200) NOT NULL,
    DESTINO VARCHAR(200) NOT NULL,
    LINHAS_MEIOS VARCHAR(255) NOT NULL,
    STATUS_PEDIDO VARCHAR(20) NOT NULL DEFAULT 'PENDENTE',
    DATA_PEDIDO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,
    ID_FUNCIONARIO_RH INT NULL,
    DATA_RESPOSTA DATETIME NULL,
    MOTIVO_RECUSA TEXT NULL,

    CONSTRAINT FK_TRAJETOS_VT_BENEFICIOS_FUNCIONARIOS
        FOREIGN KEY (ID_BENEFICIO_FUNCIONARIOS)
        REFERENCES BENEFICIOS_FUNCIONARIOS(ID_BENEFICIO_FUNCIONARIOS),

    CONSTRAINT FK_TRAJETOS_VT_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_TRAJETOS_VT_STATUS_PEDIDO
        CHECK (STATUS_PEDIDO IN ('PENDENTE', 'APROVADO', 'RECUSADO', 'CANCELADO')),
    CONSTRAINT CHK_TRAJETOS_VT_RESPOSTA_JUNTAS
        CHECK ((ID_FUNCIONARIO_RH IS NULL) = (DATA_RESPOSTA IS NULL)),
    CONSTRAINT CHK_TRAJETOS_VT_RECUSA_COM_MOTIVO
        CHECK (STATUS_PEDIDO <> 'RECUSADO' OR (MOTIVO_RECUSA IS NOT NULL AND TRIM(MOTIVO_RECUSA) <> ''))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
