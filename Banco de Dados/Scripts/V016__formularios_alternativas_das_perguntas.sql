-- ============================================================
-- V016 - Alternativas das perguntas de multipla escolha
-- Item 22 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   Uma pergunta de multipla escolha precisa das suas opcoes (ex.:
--   "Sim" e "Nao", na previa do Figma). Hoje FORMULARIOS_PERGUNTAS so
--   tem o texto da pergunta e TIPO_RESPOSTA.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria FORMULARIOS_PERGUNTAS_ALTERNATIVAS: uma linha por opcao, com
--   a ordem dentro da pergunta. A coluna que liga a RESPOSTA a uma
--   alternativa (ID_ALTERNATIVA em FORMULARIOS_RESPOSTAS) e criada no
--   script V017, junto com as outras mudancas dessa tabela.
--
-- A UNIQUE (ID_ALTERNATIVA, ID_FORMULARIO_PERGUNTA) EXTRA
--   Serve de alvo da FK composta de FORMULARIOS_RESPOSTAS, para o banco
--   garantir que a alternativa escolhida pertence a MESMA PERGUNTA da
--   resposta. E a mesma tecnica que o grupo usou entre respostas e
--   perguntas (UQ_FORMULARIOS_PERGUNTAS_PERGUNTA_FORMULARIO).
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - A resposta guarda o ID da alternativa (nova coluna) E TAMBEM o
--     texto dela em RESPOSTA. Motivo: o trigger de resposta recusa
--     pergunta obrigatoria com RESPOSTA vazia
--     (TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT).
--   - Nao editar o texto de uma alternativa ja respondida (o trigger do
--     grupo faz isso para a pergunta, mas nao enxerga esta tabela).
--   - Se a multipla escolha aceita MAIS DE UMA opcao: ainda a
--     confirmar com o dono. O desenho aguenta os dois casos: escolha
--     unica = uma linha de resposta, varias = uma linha por opcao
--     marcada (FORMULARIOS_RESPOSTAS nao tem chave unica por
--     pergunta e pessoa).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE FORMULARIOS_PERGUNTAS_ALTERNATIVAS (
    ID_ALTERNATIVA INT AUTO_INCREMENT PRIMARY KEY,
    ID_FORMULARIO_PERGUNTA INT NOT NULL,
    TEXTO_ALTERNATIVA VARCHAR(255) NOT NULL,
    ORDEM INT NOT NULL,

    CONSTRAINT FK_FORMULARIOS_ALTERNATIVAS_PERGUNTAS
        FOREIGN KEY (ID_FORMULARIO_PERGUNTA)
        REFERENCES FORMULARIOS_PERGUNTAS(ID_FORMULARIO_PERGUNTA),

    CONSTRAINT UQ_FORMULARIOS_ALTERNATIVAS_PERGUNTA_ORDEM UNIQUE (ID_FORMULARIO_PERGUNTA, ORDEM),
    CONSTRAINT UQ_FORMULARIOS_ALTERNATIVAS_ID_PERGUNTA UNIQUE (ID_ALTERNATIVA, ID_FORMULARIO_PERGUNTA),
    CONSTRAINT CHK_FORMULARIOS_ALTERNATIVAS_ORDEM CHECK (ORDEM > 0)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
