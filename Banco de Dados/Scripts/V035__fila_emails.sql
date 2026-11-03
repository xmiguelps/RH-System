-- ============================================================
-- V035 - Fila de e-mails (FILA_EMAILS)
-- Fatias 1, 3, 4, 5 e 7 (CONVENCOES, secao 12: lista fechada de e-mails)
-- ============================================================
-- POR QUE
--   O sistema envia e-mails (convite, redefinicao de senha, aviso e holerite
--   publicados, resultado de ajuste de ponto e de ferias, advertencia,
--   lembretes de formulario). O roadmap (decisao 2) pede uma fila com limite
--   de vazao (500 convites de uma vez nao saem juntos), limite de tentativas,
--   tratamento de devolucao e CHAVE DE IDEMPOTENCIA POR MENSAGEM: o e-mail de
--   um aviso agendado ou de um holerite nao pode sair duas vezes se o
--   agendador repetir a tarefa, e o banco nao guarda "e-mail enviado"
--   (AVISOS.ENVIADO so diz que o aviso foi publicado).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria FILA_EMAILS, uma linha por mensagem a um destinatario:
--   - TIPO: codigo da mensagem em maiusculas (CONVITE, REDEFINICAO_SENHA,
--     AVISO_PUBLICADO, HOLERITE_PUBLICADO, AJUSTE_PONTO_DECIDIDO,
--     FERIAS_DECIDIDAS, ADVERTENCIA_REGISTRADA, FORMULARIO_LEMBRETE e outros).
--     O banco confere so o FORMATO, nao uma lista: a lista fechada de
--     e-mails e decisao do dono e fica na aplicacao (e o lembrete manual de
--     aviso, ainda provisorio no contrato, nao exige script).
--   - CHAVE_IDEMPOTENCIA: UNICA e sempre comeca por "TIPO:" (CHECK). A
--     aplicacao a monta com o que identifica a mensagem, por exemplo
--       AVISO_PUBLICADO:aviso=15:func=21
--       HOLERITE_PUBLICADO:holerite=77
--       AJUSTE_PONTO_DECIDIDO:pedido=301:etapa=RH:func=12
--       FORMULARIO_LEMBRETE:form=9:func=21:dia=2026-10-24
--     Reenfileirar a mesma mensagem bate na chave unica (erro 1062): e o
--     sinal de "ja esta na fila ou ja saiu". A aplicacao trata como sucesso.
--   - DESTINATARIO_EMAIL e ID_FUNCIONARIO (quem recebe, quando e uma pessoa
--     do sistema), REFERENCIA_ID (aviso, pedido, formulario... o que a
--     mensagem trata, sem chave estrangeira de proposito: aponta para
--     tabelas diferentes conforme o TIPO).
--   - CARGA: JSON com o que o modelo do e-mail precisa (nome, link, datas).
--   - SITUACAO: PENDENTE, EM_ENVIO (reservada por um processo), ENVIADO,
--     FALHOU (esgotou as tentativas), DEVOLVIDO (o provedor avisou que o
--     e-mail voltou) ou CANCELADO (nao precisa mais sair, por exemplo a
--     pessoa foi inativada).
--   - TENTATIVAS, PROXIMA_TENTATIVA_EM (nova tentativa e tambem o LIMITE DE
--     VAZAO: um lote de 500 convites entra com horarios espacados),
--     RESERVADO_ATE (quem pega a mensagem a reserva por alguns minutos, e
--     se o processo cair, outro a retoma depois), ULTIMO_ERRO, CRIADO_EM,
--     ENVIADO_EM.
--
-- A CARGA NAO FICA DEPOIS DO ENVIO (privacidade, roadmap decisao 13)
--   O e-mail de convite e o de redefinicao levam o TOKEN no link, e o token
--   so pode ficar legivel enquanto a mensagem espera para sair. Por isso um
--   CHECK exige CARGA nula em toda mensagem que nao esta PENDENTE nem
--   EM_ENVIO: ao terminar (ENVIADO, FALHOU, DEVOLVIDO ou CANCELADO) a
--   aplicacao grava CARGA = NULL na mesma instrucao que muda a situacao.
--
-- LEMBRETE DE FORMULARIO ANONIMO (SIGILOSO)
--   Quem ainda nao participou de um formulario anonimo recebe o lembrete,
--   mas o sistema NAO pode guardar quem recebeu nem quantos receberam
--   (contrato, fatia 5 e auditoria). Guardar "formulario + pessoa + dia"
--   seria guardar quem ainda nao participou. Para isso:
--   - SIGILOSO = VERDADEIRO marca a mensagem. O banco recusa (CHECK) que
--     uma mensagem sigilosa chegue a ENVIADO, FALHOU, DEVOLVIDO ou CANCELADO,
--     isto e, que fique guardada com o endereco legivel depois do envio: a
--     aplicacao APAGA a linha (DELETE) assim que enviar (ou desistir), em
--     vez de mudar a situacao. Tambem recusa ID_FUNCIONARIO nessa mensagem
--     (so o endereco, que o envio exige, e so enquanto espera).
--   - Como a linha some, quem impede mandar o mesmo lembrete duas vezes no
--     mesmo dia e a tarefa "lembrete do formulario ja enviado hoje" em
--     TAREFAS_AGENDADAS (V036), uma por formulario, dia e alcance. A chave
--     desta tabela, nesse caso, nao leva a pessoa (nem a ordem dela na
--     lista): usa um sufixo aleatorio, por exemplo
--     FORMULARIO_LEMBRETE:form=9:dia=2026-10-24:r=<um GUID novo>
--   - A auditoria registra so "lembrete enviado" e a resposta da API nao diz
--     quantos receberam: contar as linhas desta tabela nunca devolve o total
--     (elas sao apagadas), mas enquanto estao na fila quem tem acesso ao
--     banco as ve. O dono aceitou em 2026-10-29 que o anonimato vale contra
--     quem usa a aplicacao, nao contra quem le o banco.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - A lista fechada de e-mails, o texto de cada modelo e o provedor.
--   - Tentativas maximas, espacamento e o que fazer com uma devolucao.
--   - Limpeza de mensagens antigas (sugestao: apagar o que terminou ha mais
--     de 90 dias, por uma tarefa agendada), porque o endereco e dado pessoal.
--   - Nunca colocar senha, hash ou token em ULTIMO_ERRO.
--
-- FUSO: CRIADO_EM, PROXIMA_TENTATIVA_EM, RESERVADO_ATE e ENVIADO_EM sao UTC,
-- em tabela so da aplicacao (nenhum CHECK ou trigger a compara com colunas
-- do primeiro grupo). Os padroes usam UTC_TIMESTAMP(6), que NAO depende do
-- fuso do servidor. No EF Core, mapeie CRIADO_EM e PROXIMA_TENTATIVA_EM como
-- geradas na inclusao (senao o EF manda 0001-01-01).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE FILA_EMAILS (
    ID_EMAIL BIGINT AUTO_INCREMENT PRIMARY KEY,
    TIPO VARCHAR(40) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    CHAVE_IDEMPOTENCIA VARCHAR(190) CHARACTER SET ascii COLLATE ascii_bin NOT NULL,
    REFERENCIA_ID INT NULL,
    ID_FUNCIONARIO INT NULL,
    DESTINATARIO_EMAIL VARCHAR(150) NOT NULL,
    SIGILOSO BOOLEAN NOT NULL DEFAULT FALSE,
    CARGA JSON NULL,
    SITUACAO VARCHAR(10) NOT NULL DEFAULT 'PENDENTE',
    TENTATIVAS SMALLINT NOT NULL DEFAULT 0,
    PROXIMA_TENTATIVA_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    RESERVADO_ATE DATETIME(6) NULL,
    ULTIMO_ERRO VARCHAR(500) NULL,
    CRIADO_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    ENVIADO_EM DATETIME(6) NULL,

    CONSTRAINT FK_FILA_EMAILS_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT UQ_FILA_EMAILS_CHAVE UNIQUE (CHAVE_IDEMPOTENCIA),
    CONSTRAINT CHK_FILA_EMAILS_TIPO CHECK (REGEXP_LIKE(TIPO, '^[A-Z][A-Z0-9_]{2,39}$', 'c')),
    -- a chave comeca por "TIPO:" (LEFT em vez de LIKE, porque "_" no LIKE e curinga)
    CONSTRAINT CHK_FILA_EMAILS_CHAVE_COM_TIPO
        CHECK (LEFT(CHAVE_IDEMPOTENCIA, CHAR_LENGTH(TIPO) + 1) = CONCAT(TIPO, ':')),
    CONSTRAINT CHK_FILA_EMAILS_DESTINATARIO CHECK (DESTINATARIO_EMAIL LIKE '%_@_%'),
    CONSTRAINT CHK_FILA_EMAILS_SITUACAO
        CHECK (SITUACAO IN ('PENDENTE', 'EM_ENVIO', 'ENVIADO', 'FALHOU', 'DEVOLVIDO', 'CANCELADO')),
    CONSTRAINT CHK_FILA_EMAILS_TENTATIVAS CHECK (TENTATIVAS >= 0),
    -- a carga (que pode ter o token do link) nao fica depois que a mensagem termina
    CONSTRAINT CHK_FILA_EMAILS_CARGA_SO_ENQUANTO_ESPERA
        CHECK (SITUACAO IN ('PENDENTE', 'EM_ENVIO') OR CARGA IS NULL),
    -- reservada se e so se esta EM_ENVIO
    CONSTRAINT CHK_FILA_EMAILS_RESERVA CHECK ((SITUACAO = 'EM_ENVIO') = (RESERVADO_ATE IS NOT NULL)),
    -- data do envio so em quem saiu (ENVIADO) e em quem saiu e voltou (DEVOLVIDO)
    CONSTRAINT CHK_FILA_EMAILS_ENVIADO_EM
        CHECK ((SITUACAO IN ('ENVIADO', 'DEVOLVIDO')) = (ENVIADO_EM IS NOT NULL)),
    -- lembrete sigiloso: nunca guardado depois do envio e sem ligar a uma pessoa
    CONSTRAINT CHK_FILA_EMAILS_SIGILOSO_NAO_FICA
        CHECK (SIGILOSO = FALSE OR SITUACAO IN ('PENDENTE', 'EM_ENVIO')),
    CONSTRAINT CHK_FILA_EMAILS_SIGILOSO_SEM_PESSOA
        CHECK (SIGILOSO = FALSE OR ID_FUNCIONARIO IS NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- a fila: quem esta pendente e ja pode sair (e as reservas vencidas)
CREATE INDEX IDX_FILA_EMAILS_SITUACAO_PROXIMA ON FILA_EMAILS(SITUACAO, PROXIMA_TENTATIVA_EM);

-- "o que ja foi enfileirado para este aviso, pedido ou formulario"
CREATE INDEX IDX_FILA_EMAILS_TIPO_REFERENCIA ON FILA_EMAILS(TIPO, REFERENCIA_ID);
