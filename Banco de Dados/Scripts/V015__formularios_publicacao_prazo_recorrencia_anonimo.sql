-- ============================================================
-- V015 - Formularios: publicacao, prazo, recorrencia, anonimo,
--        link publico e sentido da avaliacao
-- Item 12 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE (o que o Figma e o dono pedem, 2026-10-29)
--   - PUBLICACAO (Figma, etapa 4 do assistente): "Quem responde"
--     (candidatos por link / setores / pessoas escolhidas) e "Quando
--     publicar" (agora ou agendar para uma data). Existe tambem
--     "Salvar rascunho e sair" em todas as etapas.
--   - PRAZO: "Aceitar respostas ate" (so no formulario, nao em cada
--     atribuicao).
--   - RECORRENCIA: o RH escolhe um intervalo, o formulario reabre
--     sozinho e as pessoas respondem de novo. SEM HISTORICO: ao reabrir,
--     as respostas da rodada anterior sao APAGADAS (o RH exporta antes).
--     Nao ha tabela de rodadas.
--   - ANONIMO: o RH marca cada formulario como anonimo ou nao, ao criar.
--     Formulario anonimo nao tem envio de arquivo nem link publico.
--   - LINK PUBLICO: a entrevista automatica e respondida por candidatos
--     que NAO sao funcionarios, por um link.
--   - SENTIDO DA AVALIACAO: o Figma tem DOIS tipos de avaliacao
--     ("Avaliacao da equipe: o gestor avalia cada pessoa" e "Avaliacao
--     do gestor: a equipe avalia o gestor"). Ambos sao TIPO_FORMULARIO =
--     'AVALIACAO', esta coluna diz qual dos dois e. (Acrescentado por mim
--     ao ler a tela 17 do Figma, nao estava na lista. Se achar
--     desnecessario, e so nao aplicar a coluna.)
--
-- COMO FUNCIONA O ESTADO DO FORMULARIO (sem coluna de status nova)
--   O unico bloqueio de resposta que o banco ja tem e ATIVO
--   (TRG_FORMULARIOS_RESPOSTAS_BEFORE_INSERT recusa formulario inativo).
--   Entao:
--     rascunho   = ATIVO falso, sem DATA_PUBLICACAO e sem DATA_ENCERRAMENTO
--     agendado   = ATIVO falso, DATA_PUBLICACAO preenchida e sem
--                  DATA_ENCERRAMENTO (o agendador liga ATIVO quando a
--                  data chega)
--     publicado  = ATIVO verdadeiro
--     encerrado  = ATIVO falso e DATA_ENCERRAMENTO preenchida
--   DATA_ENCERRAMENTO existe porque, sem ela, "agendado com a data ja
--   vencida" e "encerrado" seriam o MESMO estado (ATIVO falso e
--   DATA_PUBLICACAO no passado), e um agendador que publica "o que esta
--   inativo e com data vencida" reabriria formularios encerrados. O
--   agendador so publica o que tem DATA_ENCERRAMENTO vazia. Ao reabrir um
--   formulario recorrente a aplicacao liga ATIVO e limpa DATA_ENCERRAMENTO
--   (o CHECK nao deixa os dois existirem juntos).
--   IMPORTANTE: ATIVO continua com padrao VERDADEIRO (nao mudei).
--   Quem cria um formulario TEM QUE gravar ATIVO = FALSE explicitamente
--   para ele nascer como rascunho, se esquecer, o formulario ja aceita
--   resposta. Esse cuidado e da aplicacao. (A alternativa seria mudar o
--   padrao para FALSE, deixei como esta para nao alterar o que ja
--   funciona, mas a escolha e sua.)
--
-- REGRAS QUE O BANCO GARANTE (CHECK, sem trigger)
--   - anonimo nao pode ter PUBLICO = 'LINK' (nem codigo de link),
--   - o codigo/validade/limite do link so existem com PUBLICO = 'LINK',
--   - intervalo e unidade da recorrencia andam juntos, a proxima
--     reabertura e o "ate quando" so existem se ha recorrencia,
--   - formulario encerrado esta inativo (DATA_ENCERRAMENTO so com ATIVO falso),
--   - a chave unica (ID_FORMULARIO, ANONIMO) NAO muda nada nos dados: e o
--     "alvo" da chave estrangeira composta de V017, que faz o banco
--     garantir que o ANONIMO de cada resposta bate com o do formulario e
--     que o ANONIMO do formulario NAO PODE MUDAR depois da primeira
--     resposta (o MySQL recusa alterar uma chave que algum filho cita).
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Anonimo sem pergunta de envio de arquivo (isso e outra tabela:
--     FORMULARIOS_PERGUNTAS.TIPO_RESPOSTA).
--   - Quem reabre o formulario recorrente (agendador): apaga as
--     respostas da rodada anterior e as participacoes anonimas (V019).
--     O banco permite apagar: FORMULARIOS_RESPOSTAS nao tem trigger de
--     DELETE. E, sem respostas, a pergunta volta a poder ser editada
--     (os triggers de FORMULARIOS_PERGUNTAS so travam pergunta ja
--     respondida).
--   - Aceitar ou nao resposta depois do prazo (DATA_PRAZO): o banco
--     nao confere, a aplicacao bloqueia ou desliga ATIVO.
--   - O CODIGO do link e um segredo na URL, foi guardado direto (e nao
--     em hash) porque o RH precisa ver o link de novo. Gere com um
--     gerador seguro (nao sequencial).
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE FORMULARIOS
    ADD COLUMN SENTIDO_AVALIACAO VARCHAR(30) NULL,
    ADD COLUMN PUBLICO VARCHAR(10) NULL,
    ADD COLUMN DATA_PUBLICACAO DATETIME NULL,
    ADD COLUMN DATA_ENCERRAMENTO DATETIME NULL,
    ADD COLUMN DATA_PRAZO DATE NULL,
    ADD COLUMN ANONIMO BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN RECORRENCIA_INTERVALO SMALLINT NULL,
    ADD COLUMN RECORRENCIA_UNIDADE VARCHAR(10) NULL,
    ADD COLUMN PROXIMA_REABERTURA DATETIME NULL,
    ADD COLUMN RECORRENCIA_ATE DATE NULL,
    -- ascii_bin: o codigo do link diferencia maiuscula de minuscula (a
    -- collation padrao, unicode_ci, nao diferencia e enfraqueceria o codigo)
    ADD COLUMN LINK_PUBLICO_CODIGO VARCHAR(64) CHARACTER SET ascii COLLATE ascii_bin NULL,
    ADD COLUMN LINK_PUBLICO_VALIDO_ATE DATETIME NULL,
    ADD COLUMN LINK_PUBLICO_LIMITE_RESPOSTAS INT NULL,

    ADD CONSTRAINT UQ_FORMULARIOS_LINK_PUBLICO_CODIGO UNIQUE (LINK_PUBLICO_CODIGO),
    ADD CONSTRAINT UQ_FORMULARIOS_ID_ANONIMO UNIQUE (ID_FORMULARIO, ANONIMO),

    -- encerrado implica inativo
    ADD CONSTRAINT CHK_FORMULARIOS_ENCERRADO_INATIVO
        CHECK (DATA_ENCERRAMENTO IS NULL OR ATIVO = FALSE),

    ADD CONSTRAINT CHK_FORMULARIOS_SENTIDO_AVALIACAO
        CHECK (SENTIDO_AVALIACAO IS NULL
               OR (COALESCE(TIPO_FORMULARIO, '') = 'AVALIACAO'
                   AND SENTIDO_AVALIACAO IN ('GESTOR_AVALIA_EQUIPE', 'EQUIPE_AVALIA_GESTOR'))),

    ADD CONSTRAINT CHK_FORMULARIOS_PUBLICO
        CHECK (PUBLICO IS NULL OR PUBLICO IN ('LINK', 'SETORES', 'PESSOAS')),

    -- anonimo nao tem link publico
    ADD CONSTRAINT CHK_FORMULARIOS_ANONIMO_SEM_LINK
        CHECK (ANONIMO = FALSE OR PUBLICO IS NULL OR PUBLICO <> 'LINK'),

    -- codigo, validade e limite so existem para formulario por link
    -- (COALESCE de proposito: no MySQL um CHECK que da NULL PASSA, sem ele,
    --  um codigo de link com PUBLICO vazio passaria)
    ADD CONSTRAINT CHK_FORMULARIOS_LINK_SO_COM_PUBLICO_LINK
        CHECK ((LINK_PUBLICO_CODIGO IS NULL AND LINK_PUBLICO_VALIDO_ATE IS NULL AND LINK_PUBLICO_LIMITE_RESPOSTAS IS NULL)
               OR COALESCE(PUBLICO, '') = 'LINK'),

    ADD CONSTRAINT CHK_FORMULARIOS_LINK_LIMITE
        CHECK (LINK_PUBLICO_LIMITE_RESPOSTAS IS NULL OR LINK_PUBLICO_LIMITE_RESPOSTAS > 0),

    -- recorrencia: intervalo e unidade juntos, o resto depende deles
    ADD CONSTRAINT CHK_FORMULARIOS_RECORRENCIA_UNIDADE
        CHECK (RECORRENCIA_UNIDADE IS NULL OR RECORRENCIA_UNIDADE IN ('DIA', 'SEMANA', 'MES')),
    ADD CONSTRAINT CHK_FORMULARIOS_RECORRENCIA_JUNTAS
        CHECK ((RECORRENCIA_INTERVALO IS NULL) = (RECORRENCIA_UNIDADE IS NULL)),
    ADD CONSTRAINT CHK_FORMULARIOS_RECORRENCIA_INTERVALO
        CHECK (RECORRENCIA_INTERVALO IS NULL OR RECORRENCIA_INTERVALO > 0),
    ADD CONSTRAINT CHK_FORMULARIOS_RECORRENCIA_DEPENDENTES
        CHECK (RECORRENCIA_INTERVALO IS NOT NULL
               OR (PROXIMA_REABERTURA IS NULL AND RECORRENCIA_ATE IS NULL));
