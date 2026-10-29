-- ============================================================
-- V017 - Respostas de formulario: candidato de fora, resposta anonima,
--        quem e avaliado e a alternativa escolhida
-- Item 13 (e a coluna do item 22) de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   FORMULARIOS_RESPOSTAS.ID_FUNCIONARIO e obrigatorio. Entao hoje:
--   a) o CANDIDATO de uma entrevista por link (que nao e funcionario)
--      nao tem como responder,
--   b) uma resposta ANONIMA nao cabe (o banco guardaria o autor),
--   c) numa AVALIACAO nao se sabe SOBRE QUEM e a resposta (o gestor
--      responde o mesmo formulario uma vez por pessoa da equipe, e a
--      equipe responde sobre o gestor),
--   d) uma pergunta de multipla escolha nao tem onde guardar a
--      alternativa escolhida.
--
-- O QUE ESTE SCRIPT FAZ
--   1) Cria FORMULARIOS_CANDIDATOS: os dados de quem responde pelo
--      link (nome, e-mail, telefone, curriculo). Fica POR FORMULARIO.
--   2) Em FORMULARIOS_RESPOSTAS:
--      - ID_FUNCIONARIO passa a aceitar NULL,
--      - ID_AVALIADO: sobre quem e a resposta (avaliacoes),
--      - ID_FORMULARIO_CANDIDATO: o candidato, se for o caso,
--      - ID_ALTERNATIVA: a opcao escolhida (V016),
--      - ANONIMO: copia do ANONIMO do formulario (ver "ANONIMATO").
--      Uma resposta tem um destes tres "donos": um funcionario, um
--      candidato ou ninguem (anonima). Os CHECKs impedem funcionario e
--      candidato juntos e amarram "ninguem" ao formulario anonimo.
--
-- POR QUE AS RESPOSTAS ANONIMAS E DE CANDIDATO FICAM NA MESMA TABELA
--   Os triggers de pergunta (TRG_FORMULARIOS_PERGUNTAS_BEFORE_UPDATE e
--   _DELETE) so contam respostas em FORMULARIOS_RESPOSTAS para travar
--   a edicao de pergunta ja respondida. Numa tabela separada, uma
--   pergunta respondida por candidato ou anonimamente poderia ser
--   editada ou apagada. Por isso mantive tudo aqui.
--
-- SOBRE O TRIGGER DE RESPOSTA E ID_FUNCIONARIO NULL (LEIA)
--   TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT faz um SELECT do
--   funcionario e so recusa se ATIVO = FALSE. Com ID_FUNCIONARIO
--   NULL o SELECT nao acha linha, a variavel fica NULL e o IF com NULL
--   nao dispara: a resposta passa. Esse comportamento e o que
--   precisamos, mas e SUTIL, por isso foi TESTADO num MySQL 8.4.13 (ver
--   LEIA-ME.md desta pasta): a resposta de candidato e a anonima passam,
--   e o trigger continua recusando funcionario inativo, formulario
--   inativo e pergunta obrigatoria vazia. Nao mexi no trigger.
--
-- ANONIMATO DE VERDADE
--   O que o BANCO garante (sem trigger):
--   - ANONIMO na resposta e uma COPIA do ANONIMO do formulario, e a chave
--     estrangeira composta (ID_FORMULARIO, ANONIMO) obriga os dois a
--     coincidir. O CHECK de autor entao exige: resposta de formulario
--     anonimo NAO tem funcionario nem candidato, e resposta de formulario
--     nao anonimo tem um dos dois.
--   - Essa mesma chave estrangeira IMPEDE trocar o ANONIMO do formulario
--     depois da primeira resposta (o MySQL recusa alterar uma chave que
--     algum filho cita). Ou seja, "o anonimo nao muda depois da primeira
--     resposta" e garantido pelo banco, nao pela aplicacao.
--   - A aplicacao TEM QUE gravar ANONIMO = verdadeiro nas respostas de
--     formulario anonimo (o padrao e falso). Se esquecer, a chave
--     estrangeira recusa. Nao ha como gravar nome num formulario anonimo.
--   O que fica com a APLICACAO:
--   - DATA_RESPOSTA: o trigger so preenche NOW() quando vem vazia. Para
--     resposta anonima a aplicacao grava um valor GROSSO (por exemplo o dia
--     de abertura da rodada) e nunca a hora.
--   - O minimo de respostas (comeca em 3, ajustavel) vale POR AVALIADO nas
--     avaliacoes anonimas: um gestor com um unico subordinado teria a
--     resposta sobre ele identificada mesmo que o formulario tenha 30
--     respostas no total.
--   - A auditoria NAO registra o autor nem a hora de resposta anonima.
--   - "Quem ja respondeu" fica em FORMULARIOS_PARTICIPACOES (V019),
--     separada e sem ordem, sem ligacao com as respostas.
--   CONTRA QUEM O ANONIMATO VALE (decisao do dono, ainda nao tomada):
--   vale contra quem USA A APLICACAO (RH e gestor), que nunca ve nome,
--   numero nem hora da resposta anonima. NAO vale contra quem le o BANCO ou
--   o BACKUP: o ID das respostas e sequencial e compartilhado com os
--   formularios com nome (quem responde um formulario com nome e logo
--   depois um anonimo deixa IDs vizinhos), e o log binario do servidor
--   guarda a ordem das gravacoes.
--
-- LGPD: FORMULARIOS_CANDIDATOS guarda dado pessoal de quem nao e
--   funcionario (e o curriculo). A aplicacao precisa de aviso de
--   privacidade e de um job que apague candidatos vencidos.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE FORMULARIOS_CANDIDATOS (
    ID_FORMULARIO_CANDIDATO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FORMULARIO INT NOT NULL,
    NOME VARCHAR(150) NOT NULL,
    EMAIL VARCHAR(150) NOT NULL,
    TELEFONE VARCHAR(30) NULL,
    CAMINHO_CURRICULO VARCHAR(255) NULL,
    DATA_CADASTRO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_FORMULARIOS_CANDIDATOS_FORMULARIOS
        FOREIGN KEY (ID_FORMULARIO)
        REFERENCES FORMULARIOS(ID_FORMULARIO),

    -- alvo da FK composta das respostas: candidato e resposta no mesmo formulario
    CONSTRAINT UQ_FORMULARIOS_CANDIDATOS_ID_FORMULARIO UNIQUE (ID_FORMULARIO_CANDIDATO, ID_FORMULARIO),
    -- o mesmo e-mail nao se candidata duas vezes ao mesmo formulario
    CONSTRAINT UQ_FORMULARIOS_CANDIDATOS_FORMULARIO_EMAIL UNIQUE (ID_FORMULARIO, EMAIL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

ALTER TABLE FORMULARIOS_RESPOSTAS
    MODIFY COLUMN ID_FUNCIONARIO INT NULL,
    ADD COLUMN ID_AVALIADO INT NULL,
    ADD COLUMN ID_FORMULARIO_CANDIDATO INT NULL,
    ADD COLUMN ID_ALTERNATIVA INT NULL,
    ADD COLUMN ANONIMO BOOLEAN NOT NULL DEFAULT FALSE,

    ADD CONSTRAINT FK_FORMULARIOS_RESPOSTAS_AVALIADOS
        FOREIGN KEY (ID_AVALIADO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    -- o candidato precisa ser do mesmo formulario da resposta
    ADD CONSTRAINT FK_FORMULARIOS_RESPOSTAS_CANDIDATOS
        FOREIGN KEY (ID_FORMULARIO_CANDIDATO, ID_FORMULARIO)
        REFERENCES FORMULARIOS_CANDIDATOS(ID_FORMULARIO_CANDIDATO, ID_FORMULARIO),

    -- a alternativa precisa ser da mesma pergunta da resposta
    ADD CONSTRAINT FK_FORMULARIOS_RESPOSTAS_ALTERNATIVAS
        FOREIGN KEY (ID_ALTERNATIVA, ID_FORMULARIO_PERGUNTA)
        REFERENCES FORMULARIOS_PERGUNTAS_ALTERNATIVAS(ID_ALTERNATIVA, ID_FORMULARIO_PERGUNTA),

    -- o ANONIMO da resposta e igual ao do formulario, e o do formulario
    -- nao pode mudar enquanto houver resposta (ver o comentario acima)
    ADD CONSTRAINT FK_FORMULARIOS_RESPOSTAS_FORMULARIOS_ANONIMO
        FOREIGN KEY (ID_FORMULARIO, ANONIMO)
        REFERENCES FORMULARIOS(ID_FORMULARIO, ANONIMO),

    -- um "dono" so: funcionario ou candidato, nunca os dois
    ADD CONSTRAINT CHK_FORMULARIOS_RESPOSTAS_AUTOR
        CHECK (ID_FUNCIONARIO IS NULL OR ID_FORMULARIO_CANDIDATO IS NULL),

    -- anonima: sem funcionario e sem candidato. Nao anonima: com um dos dois
    ADD CONSTRAINT CHK_FORMULARIOS_RESPOSTAS_ANONIMA_SEM_AUTOR
        CHECK ((ANONIMO = TRUE AND ID_FUNCIONARIO IS NULL AND ID_FORMULARIO_CANDIDATO IS NULL)
               OR (ANONIMO = FALSE AND (ID_FUNCIONARIO IS NOT NULL OR ID_FORMULARIO_CANDIDATO IS NOT NULL)));
