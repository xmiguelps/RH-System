-- ============================================================
-- V023 - Atestados (ATESTADOS)
-- Item 18 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O funcionario envia um atestado (arquivo). Regras decididas pelo dono
--   em 2026-10-29:
--   - O GESTOR da equipe VE o arquivo, SO O RH VALIDA OU RECUSA.
--   - VALIDADO = "Falta Compensada": o dia (ou as horas cobertas) nao
--     conta como falta e nao desconta do banco de horas.
--   - RECUSADO (com motivo): a pessoa ve "recusado" e o motivo, e o dia
--     segue como falta.
--   - Existe atestado de DIAS INTEIROS e de ALGUMAS HORAS.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria ATESTADOS, uma linha por atestado:
--   - DATA_INICIO e DATA_FIM: o periodo (dias inteiros). Para um dia so,
--     as duas sao iguais.
--   - HORA_INICIO e HORA_FIM: preenchidas SO no atestado de horas, nesse
--     caso so vale um dia (DATA_INICIO = DATA_FIM) e so esse periodo e
--     compensado. (Proposta minha, o dono aprovou o conceito de atestado
--     de horas, nao o formato.)
--   - CAMINHO_ARQUIVO: referencia ao arquivo no Blob Storage.
--   - STATUS_ATESTADO: ENVIADO (aguardando o RH), VALIDADO ou RECUSADO,
--     mais quem do RH decidiu, quando, e o MOTIVO_RECUSA.
--
-- O QUE O BANCO GARANTE (CHECK)
--   - fim >= inicio, hora de inicio e de fim juntas e, se houver, no
--     mesmo dia e com fim depois do inicio,
--   - ENVIADO nao tem decisao, VALIDADO e RECUSADO tem quem decidiu e
--     quando,
--   - RECUSADO tem motivo.
--
-- COMO O BANCO DE HORAS USA ESTA TABELA (decisao de engenharia)
--   A consolidacao diaria le os atestados VALIDADOS e REDUZ o esperado
--   do dia pelas horas cobertas (dia inteiro: esperado zero). Nao ha
--   coluna nova em PONTOS. Gravar ou nao uma linha zerada em BANCOS_HORAS
--   para um dia totalmente compensado e escolha da consolidacao.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - O gestor ve o arquivo so da propria equipe, so o RH valida/recusa.
--   - A aplicacao nao guarda diagnostico (CID): nao foi pedido e e dado
--     sensivel.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE ATESTADOS (
    ID_ATESTADO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FUNCIONARIO INT NOT NULL,
    DATA_INICIO DATE NOT NULL,
    DATA_FIM DATE NOT NULL,
    HORA_INICIO TIME NULL,
    HORA_FIM TIME NULL,
    CAMINHO_ARQUIVO VARCHAR(255) NOT NULL,
    NOME_ARQUIVO_ORIGINAL VARCHAR(255) NULL,
    STATUS_ATESTADO VARCHAR(10) NOT NULL DEFAULT 'ENVIADO',
    ID_FUNCIONARIO_RH INT NULL,
    DATA_DECISAO DATETIME NULL,
    MOTIVO_RECUSA TEXT NULL,
    DATA_ENVIO DATETIME NOT NULL DEFAULT CURRENT_TIMESTAMP,

    CONSTRAINT FK_ATESTADOS_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT FK_ATESTADOS_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_ATESTADOS_PERIODO CHECK (DATA_FIM >= DATA_INICIO),
    CONSTRAINT CHK_ATESTADOS_HORAS_JUNTAS CHECK ((HORA_INICIO IS NULL) = (HORA_FIM IS NULL)),
    CONSTRAINT CHK_ATESTADOS_HORAS_NO_MESMO_DIA
        CHECK (HORA_INICIO IS NULL OR (DATA_FIM = DATA_INICIO AND HORA_FIM > HORA_INICIO)),
    CONSTRAINT CHK_ATESTADOS_STATUS CHECK (STATUS_ATESTADO IN ('ENVIADO', 'VALIDADO', 'RECUSADO')),
    CONSTRAINT CHK_ATESTADOS_DECISAO
        CHECK ((STATUS_ATESTADO = 'ENVIADO' AND ID_FUNCIONARIO_RH IS NULL AND DATA_DECISAO IS NULL)
               OR (STATUS_ATESTADO <> 'ENVIADO' AND ID_FUNCIONARIO_RH IS NOT NULL AND DATA_DECISAO IS NOT NULL)),
    CONSTRAINT CHK_ATESTADOS_RECUSA_COM_MOTIVO
        CHECK (STATUS_ATESTADO <> 'RECUSADO' OR (MOTIVO_RECUSA IS NOT NULL AND TRIM(MOTIVO_RECUSA) <> ''))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
