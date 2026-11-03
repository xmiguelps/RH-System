-- ============================================================
-- V004 - Refresh tokens do login (REFRESH_TOKENS)
-- Fatia 0 (contrato: /api/auth/entrar, /renovar e /sair)
-- ============================================================
-- POR QUE
--   O login devolve um token de acesso de vida curta (15 minutos) e um
--   refresh token (7 dias) para renova-lo. O refresh token precisa ser
--   revogavel (sair, uso repetido) e girar a cada uso. Por regra de
--   privacidade do projeto, o token em si nunca fica no banco: so o hash.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria REFRESH_TOKENS, uma linha por refresh token emitido:
--   - TOKEN_HASH: SO O HASH (SHA-256, 64 caracteres hexadecimais) do token.
--     A chave unica impede dois tokens com o mesmo hash.
--   - CRIADO_EM, EXPIRA_EM, REVOGADO_EM: todos em UTC. REVOGADO_EM so e
--     preenchido quando o token e usado na renovacao (rotacao) ou no sair.
--
-- COMO A APLICACAO USA
--   - Renovar: UPDATE ... SET REVOGADO_EM = agora WHERE TOKEN_HASH = ? AND
--     REVOGADO_EM IS NULL AND EXPIRA_EM > agora, e conferir que UMA linha
--     mudou; so entao emite o token novo. Duas renovacoes simultaneas com o
--     mesmo token: so uma conta 1 e a outra recebe 401, sem revogar os
--     demais tokens da pessoa.
--
-- O QUE O BANCO GARANTE
--   Hash no formato, validade posterior a emissao, um hash so por token.
--
-- FUSO: CRIADO_EM, EXPIRA_EM e REVOGADO_EM sao UTC. Tabela so da
-- aplicacao: nenhum CHECK ou trigger a compara com colunas do primeiro
-- grupo, que ficam em -03:00. A aplicacao SEMPRE preenche as datas a partir
-- do relogio dela (IRelogio); o padrao de CRIADO_EM (UTC_TIMESTAMP(6)) e so
-- rede de seguranca e nao depende do fuso do servidor.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE REFRESH_TOKENS (
    ID_REFRESH_TOKEN INT AUTO_INCREMENT PRIMARY KEY,
    ID_USUARIO INT NOT NULL,
    TOKEN_HASH CHAR(64) NOT NULL,
    CRIADO_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    EXPIRA_EM DATETIME(6) NOT NULL,
    REVOGADO_EM DATETIME(6) NULL,

    CONSTRAINT FK_REFRESH_TOKENS_USUARIOS
        FOREIGN KEY (ID_USUARIO)
        REFERENCES USUARIOS(ID_USUARIO),

    CONSTRAINT UQ_REFRESH_TOKENS_HASH UNIQUE (TOKEN_HASH),
    CONSTRAINT CHK_REFRESH_TOKENS_HASH CHECK (TOKEN_HASH REGEXP '^[0-9A-Fa-f]{64}$'),
    CONSTRAINT CHK_REFRESH_TOKENS_VALIDADE CHECK (EXPIRA_EM > CRIADO_EM)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
