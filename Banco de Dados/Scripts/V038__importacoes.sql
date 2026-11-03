-- ============================================================
-- V038 - Importacao de colaboradores por planilha (IMPORTACOES)
-- Fatia 2 (contrato: POST /api/rh/importacoes e .../{importacaoId}/confirmar)
-- ============================================================
-- POR QUE
--   A importacao tem dois passos: o RH envia a planilha e recebe uma PREVIA
--   (cada linha com os valores e os erros por coluna, sem cadastrar ninguem)
--   e, num segundo pedido, CONFIRMA pelo importacaoId. Entre os dois passos
--   alguem precisa guardar o que foi lido, a situacao (COM_ERROS, PRONTA,
--   CONFIRMADA), a validade de 1 HORA e o arquivo. O contrato diz que isso
--   vive numa tabela nova da aplicacao, e nao nas tabelas de colaboradores.
--   O roadmap pede o registro de cada importacao (quem, quando, totais).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria IMPORTACOES, uma linha por planilha enviada (cada envio corrigido
--   e uma importacao nova):
--   - ID_FUNCIONARIO_RH (quem enviou), NOME_ARQUIVO, CRIADA_EM, EXPIRA_EM
--     (a confirmacao e recusada depois dele: 422 IMPORTACAO_EXPIRADA).
--   - SITUACAO: COM_ERROS (tem linha com erro, nao confirma), PRONTA (sem
--     erros) ou CONFIRMADA. "Vencida" NAO e uma situacao: e PRONTA ou
--     COM_ERROS com EXPIRA_EM no passado.
--   - TOTAL_LINHAS (1 a 500, o limite do contrato), LINHAS_VALIDAS e
--     LINHAS_COM_ERRO (a soma bate com o total e COM_ERROS so existe com
--     pelo menos um erro: "tudo ou nada").
--   - PREVIA: JSON com as linhas lidas, os valores como foram digitados e os
--     erros (o que a resposta da previa mostra).
--   - CAMINHO_ARQUIVO: onde o arquivo guardado esta (so a referencia, como
--     em HOLERITES e documentos).
--   - CONFIRMADA_EM e os totais do resultado (TOTAL_CRIADOS, CONVITES_ENVIADOS,
--     INCLUIDOS_NO_ONBOARDING, INCLUIDOS_NO_FORMULARIO_ADMISSAO): so existem
--     na importacao CONFIRMADA e andam juntos.
--
-- PRIVACIDADE: A PREVIA TEM DADO PESSOAL (roadmap, decisao 13)
--   A previa guarda nome, CPF, data de nascimento, telefone e e-mail de
--   pessoas que talvez nem cheguem a ser cadastradas. Por isso:
--   - O banco recusa (CHECK) uma importacao CONFIRMADA com PREVIA: ao
--     confirmar, a aplicacao grava PREVIA = NULL na mesma instrucao que
--     muda a situacao. Ficam so os totais, que nao identificam ninguem.
--   - Importacao que VENCEU sem ser confirmada (PRONTA ou COM_ERROS com
--     EXPIRA_EM no passado) tem de ter a PREVIA e o arquivo apagados por uma
--     tarefa agendada de limpeza (TAREFAS_AGENDADAS, V036): grava PREVIA e
--     CAMINHO_ARQUIVO = NULL e apaga o arquivo do armazenamento. Isto o
--     banco NAO consegue exigir (depende do relogio).
--   - O arquivo (CAMINHO_ARQUIVO) tambem e apagado do armazenamento depois
--     de confirmado ou vencido, com o contrato. A aplicacao grava NULL aqui
--     quando o apaga. Se o processo cair entre apagar e gravar, a sobra e
--     achada pelo job de limpeza de arquivos orfaos (roadmap, decisao 7).
--   - O que a auditoria guarda da importacao e so o fato e os totais
--     (area IMPORTACAO, acao IMPORTOU), nunca as linhas.
--
-- COMO A APLICACAO USA
--   - Previa: INSERT com a situacao calculada (COM_ERROS ou PRONTA),
--     EXPIRA_EM = CRIADA_EM + 1 hora (valor da aplicacao, nao do banco).
--   - Confirmar sem corrida: UPDATE ... SET SITUACAO = CONFIRMADA, ... WHERE
--     ID_IMPORTACAO = ? AND SITUACAO = PRONTA AND EXPIRA_EM > agora, na mesma
--     transacao que cadastra os colaboradores, e conferir que UMA linha
--     mudou. Confirmar duas vezes a mesma importacao: nenhuma linha muda e a
--     aplicacao devolve 409 IMPORTACAO_JA_CONFIRMADA. Se um colaborador for
--     recusado no meio, a transacao inteira desfaz (tudo ou nada).
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Validar o arquivo (tipo, 5 MB, 500 linhas, tamanho descompactado) e
--     as linhas, e conferir tudo DE NOVO ao confirmar.
--   - O limite de 500 linhas e do contrato: se ele mudar, troque o CHECK.
--
-- FUSO: CRIADA_EM, EXPIRA_EM e CONFIRMADA_EM sao UTC, em tabela so da
-- aplicacao (nenhum CHECK ou trigger a compara com colunas do primeiro
-- grupo). O padrao de CRIADA_EM usa UTC_TIMESTAMP(6), que NAO depende do
-- fuso do servidor. No EF Core, mapeie CRIADA_EM como gerada na inclusao.
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE IMPORTACOES (
    ID_IMPORTACAO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FUNCIONARIO_RH INT NOT NULL,
    NOME_ARQUIVO VARCHAR(255) NOT NULL,
    SITUACAO VARCHAR(10) NOT NULL,
    TOTAL_LINHAS SMALLINT NOT NULL,
    LINHAS_VALIDAS SMALLINT NOT NULL,
    LINHAS_COM_ERRO SMALLINT NOT NULL,
    CRIADA_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    EXPIRA_EM DATETIME(6) NOT NULL,
    CAMINHO_ARQUIVO VARCHAR(255) NULL,
    PREVIA JSON NULL,
    CONFIRMADA_EM DATETIME(6) NULL,
    TOTAL_CRIADOS SMALLINT NULL,
    CONVITES_ENVIADOS SMALLINT NULL,
    INCLUIDOS_NO_ONBOARDING SMALLINT NULL,
    INCLUIDOS_NO_FORMULARIO_ADMISSAO SMALLINT NULL,

    CONSTRAINT FK_IMPORTACOES_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    CONSTRAINT CHK_IMPORTACOES_SITUACAO CHECK (SITUACAO IN ('COM_ERROS', 'PRONTA', 'CONFIRMADA')),
    CONSTRAINT CHK_IMPORTACOES_TOTAL_LINHAS CHECK (TOTAL_LINHAS BETWEEN 1 AND 500),
    CONSTRAINT CHK_IMPORTACOES_LINHAS_SOMAM
        CHECK (LINHAS_VALIDAS >= 0 AND LINHAS_COM_ERRO >= 0 AND LINHAS_VALIDAS + LINHAS_COM_ERRO = TOTAL_LINHAS),
    -- COM_ERROS so com erro, e sem erro nunca e COM_ERROS (tudo ou nada)
    CONSTRAINT CHK_IMPORTACOES_ERROS_E_SITUACAO CHECK ((SITUACAO = 'COM_ERROS') = (LINHAS_COM_ERRO > 0)),
    CONSTRAINT CHK_IMPORTACOES_VALIDADE CHECK (EXPIRA_EM > CRIADA_EM),
    -- a confirmacao e o resultado andam com a situacao CONFIRMADA
    CONSTRAINT CHK_IMPORTACOES_CONFIRMADA_EM CHECK ((SITUACAO = 'CONFIRMADA') = (CONFIRMADA_EM IS NOT NULL)),
    CONSTRAINT CHK_IMPORTACOES_RESULTADO_JUNTO
        CHECK ((TOTAL_CRIADOS IS NOT NULL) = (SITUACAO = 'CONFIRMADA')
           AND (CONVITES_ENVIADOS IS NOT NULL) = (SITUACAO = 'CONFIRMADA')
           AND (INCLUIDOS_NO_ONBOARDING IS NOT NULL) = (SITUACAO = 'CONFIRMADA')
           AND (INCLUIDOS_NO_FORMULARIO_ADMISSAO IS NOT NULL) = (SITUACAO = 'CONFIRMADA')),
    -- tudo ou nada: quem foi cadastrado e o total de linhas, e os demais
    -- numeros nao passam disso (COALESCE: sem resultado, nao ha o que comparar)
    CONSTRAINT CHK_IMPORTACOES_RESULTADO_COERENTE
        CHECK (COALESCE(TOTAL_CRIADOS = TOTAL_LINHAS, TRUE)
           AND COALESCE(CONVITES_ENVIADOS BETWEEN 0 AND TOTAL_CRIADOS, TRUE)
           AND COALESCE(INCLUIDOS_NO_ONBOARDING BETWEEN 0 AND TOTAL_CRIADOS, TRUE)
           AND COALESCE(INCLUIDOS_NO_FORMULARIO_ADMISSAO BETWEEN 0 AND TOTAL_CRIADOS, TRUE)),
    -- depois de confirmada, os dados pessoais da previa nao ficam
    CONSTRAINT CHK_IMPORTACOES_CONFIRMADA_SEM_PREVIA CHECK (SITUACAO <> 'CONFIRMADA' OR PREVIA IS NULL)
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;

-- a limpeza de importacoes vencidas e o registro de quem importou
CREATE INDEX IDX_IMPORTACOES_EXPIRA_EM ON IMPORTACOES(EXPIRA_EM);
CREATE INDEX IDX_IMPORTACOES_RH_CRIADA ON IMPORTACOES(ID_FUNCIONARIO_RH, CRIADA_EM);
