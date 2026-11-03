-- ============================================================
-- V033 - Links de convite e de redefinicao de senha (USUARIOS_TOKENS_SENHA)
-- Fatia 1 (contrato: /api/auth/senha/esqueci, /validar-link, /definir e
--          POST /api/rh/usuarios/{funcionarioId}/convite)
-- ============================================================
-- POR QUE
--   O e-mail de convite e o de "esqueci minha senha" levam um link com um
--   token. O token e de USO UNICO, vence (convite 7 dias, redefinicao 1
--   hora) e um pedido novo invalida o anterior. Hoje nao ha onde guardar
--   isso. Tambem vale a regra de privacidade do projeto: senhas, hashes e
--   tokens nunca ficam legiveis no banco nem no log.
--
-- O QUE ESTE SCRIPT FAZ
--   Cria USUARIOS_TOKENS_SENHA, uma linha por link emitido:
--   - TIPO: CONVITE (primeiro acesso) ou REDEFINICAO.
--   - TOKEN_HASH: SO O HASH (SHA-256, 64 caracteres hexadecimais) do token.
--     O token em si so existe no e-mail. A aplicacao recebe o token, calcula
--     o hash e procura aqui. O CHECK so confere o formato (64 hex): ele NAO
--     consegue saber se o que a aplicacao gravou e mesmo um hash, entao um
--     teste de integracao deve provar que o valor gravado nao e o token.
--   - CRIADO_EM, EXPIRA_EM, USADO_EM, INVALIDADO_EM: todos em UTC.
--   - EM_ABERTO: coluna GERADA, 1 enquanto o link nao foi usado nem
--     invalidado, senao NULL. A chave unica (ID_USUARIO, TIPO, EM_ABERTO)
--     deixa no MAXIMO UM link em aberto por pessoa e tipo (o MySQL aceita
--     varios NULL numa chave unica e so um 1). E o que faz "um pedido novo
--     invalida o anterior" valer mesmo com dois pedidos simultaneos.
--
-- COMO A APLICACAO USA
--   - Emitir: na mesma transacao, UPDATE ... SET INVALIDADO_EM = agora WHERE
--     ID_USUARIO = ? AND TIPO = ? AND EM_ABERTO = 1, e depois o INSERT novo.
--     Sem o UPDATE antes, o INSERT bate na chave unica (erro 1062).
--   - Usar (uso unico, sem corrida): UPDATE ... SET USADO_EM = agora WHERE
--     TOKEN_HASH = ? AND EM_ABERTO = 1 AND EXPIRA_EM > agora, e conferir que
--     UMA linha mudou. Duas pessoas com o mesmo link: so uma conta 1.
--   - Validar o link sem gasta-lo (/validar-link): SELECT com as mesmas
--     condicoes, sem UPDATE. Link inexistente, usado, invalidado ou vencido
--     voltam todos como 422 LINK_INVALIDO_OU_VENCIDO (nao revela qual).
--   - Link vencido continua "em aberto" ate alguem o invalidar. Por isso o
--     proximo pedido o invalida (passo "Emitir") e a limpeza periodica pode
--     apaga-lo depois de alguns dias (V036, tarefa agendada).
--   - Situacao do login (UsuarioSistemaSituacao): ver o V032.
--   - "conviteEnviadoEmUtc" de Usuarios do sistema: o CRIADO_EM do ultimo
--     token de CONVITE da pessoa.
--
-- O QUE O BANCO GARANTE (CHECK e chaves)
--   Tipo valido, hash no formato, validade posterior a emissao, link usado
--   OU invalidado (nunca os dois) e um so link em aberto por pessoa e tipo.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Gerar o token com 32 bytes aleatorios de um gerador criptografico.
--   - Prazo de validade (7 dias, 1 hora), limite de 5 tentativas por minuto
--     por IP e resposta uniforme no "esqueci minha senha".
--   - Nao colocar o token na fila de e-mails depois do envio (V035 apaga a
--     carga das mensagens que terminaram) e nunca na auditoria.
--
-- FUSO: CRIADO_EM, EXPIRA_EM, USADO_EM e INVALIDADO_EM sao UTC. Esta e uma
-- tabela so da aplicacao (como REFRESH_TOKENS, V004): nenhum CHECK ou
-- trigger a compara com colunas do primeiro grupo, que ficam em -03:00.
-- O padrao de CRIADO_EM usa UTC_TIMESTAMP(6), que NAO depende do fuso do
-- servidor. No EF Core, mapeie CRIADO_EM como gerada na inclusao e
-- EM_ABERTO como calculada (fora do INSERT).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE USUARIOS_TOKENS_SENHA (
    ID_USUARIO_TOKEN_SENHA INT AUTO_INCREMENT PRIMARY KEY,
    ID_USUARIO INT NOT NULL,
    TIPO VARCHAR(12) NOT NULL,
    TOKEN_HASH CHAR(64) NOT NULL,
    CRIADO_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    EXPIRA_EM DATETIME(6) NOT NULL,
    USADO_EM DATETIME(6) NULL,
    INVALIDADO_EM DATETIME(6) NULL,
    EM_ABERTO TINYINT GENERATED ALWAYS AS
        (IF(USADO_EM IS NULL AND INVALIDADO_EM IS NULL, 1, NULL)) STORED,

    CONSTRAINT FK_USUARIOS_TOKENS_SENHA_USUARIOS
        FOREIGN KEY (ID_USUARIO)
        REFERENCES USUARIOS(ID_USUARIO),

    CONSTRAINT UQ_USUARIOS_TOKENS_SENHA_HASH UNIQUE (TOKEN_HASH),
    -- no maximo um link em aberto por pessoa e tipo (EM_ABERTO e 1 ou NULL)
    CONSTRAINT UQ_USUARIOS_TOKENS_SENHA_EM_ABERTO UNIQUE (ID_USUARIO, TIPO, EM_ABERTO),
    CONSTRAINT CHK_USUARIOS_TOKENS_SENHA_TIPO CHECK (TIPO IN ('CONVITE', 'REDEFINICAO')),
    CONSTRAINT CHK_USUARIOS_TOKENS_SENHA_HASH CHECK (TOKEN_HASH REGEXP '^[0-9A-Fa-f]{64}$'),
    CONSTRAINT CHK_USUARIOS_TOKENS_SENHA_VALIDADE CHECK (EXPIRA_EM > CRIADO_EM),
    -- usado OU invalidado, nunca os dois (os dois lados do OR nunca dao NULL)
    CONSTRAINT CHK_USUARIOS_TOKENS_SENHA_USADO_OU_INVALIDADO
        CHECK (USADO_EM IS NULL OR INVALIDADO_EM IS NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
