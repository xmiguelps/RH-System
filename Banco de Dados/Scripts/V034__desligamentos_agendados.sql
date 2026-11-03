-- ============================================================
-- V034 - Desligamento agendado (DESLIGAMENTOS_AGENDADOS)
-- Fatia 1 (contrato: POST/DELETE /api/rh/funcionarios/{id}/desligamento,
--          FuncionarioDesligamento e a readmissao)
-- ============================================================
-- POR QUE
--   O banco inativa a pessoa assim que FUNCIONARIOS.DATA_DESLIGAMENTO tem
--   valor, mesmo no futuro (TRG_FUNCIONARIOS_BEFORE_INSERT e _UPDATE), e
--   a partir dai recusa a batida de ponto e o lancamento do banco de horas
--   dela. O RH informa o ULTIMO DIA DE TRABALHO, que pode ser amanha. Por
--   isso a aplicacao guarda a data AQUI e so a grava em FUNCIONARIOS no dia
--   seguinte ao ultimo dia de trabalho (se esse momento ja passou, na hora).
--   O contrato ainda precisa de tres coisas que FUNCIONARIOS nao guarda:
--   a situacao AGENDADO ou EFETIVADO da ficha, o cancelamento antes de
--   efetivar (DELETE .../desligamento) e a readmissao, que apaga a data de
--   desligamento do cadastro (o banco tem um unico par de datas por pessoa).
--
-- O QUE ESTE SCRIPT FAZ
--   Cria DESLIGAMENTOS_AGENDADOS, uma linha por desligamento:
--   - SITUACAO: AGENDADO (registrado, a pessoa ainda esta ativa), EFETIVADO
--     (a data ja foi gravada em FUNCIONARIOS e a pessoa esta inativa),
--     CANCELADO (o RH cancelou antes de efetivar) ou REVERTIDO (estava
--     efetivado e a readmissao o encerrou). A ficha mostra so AGENDADO e
--     EFETIVADO (FuncionarioDesligamentoSituacao): CANCELADO e REVERTIDO
--     ficam como historico e a ficha nao os mostra.
--   - VIGENTE: coluna GERADA, 1 quando a situacao e AGENDADO ou EFETIVADO,
--     senao NULL. A chave unica (ID_FUNCIONARIO, VIGENTE) deixa no maximo
--     UM desligamento vigente por pessoa: e o "ja inativa ou com
--     desligamento ja registrado: 409" do contrato, valendo tambem com dois
--     cliques simultaneos. Depois de CANCELADO ou REVERTIDO a pessoa pode
--     ter outro desligamento.
--   - Os instantes EFETIVADO_EM, CANCELADO_EM e REVERTIDO_EM andam com a
--     situacao (CHECK): so existem quando a situacao os explica.
--
-- COMO A APLICACAO USA
--   - Agendar: INSERT com SITUACAO AGENDADO e uma tarefa em
--     TAREFAS_AGENDADAS (tipo de desligamento, REFERENCIA_ID = o id desta
--     linha, EXECUTAR_APOS = 00:00 do dia seguinte ao ultimo dia de trabalho
--     no fuso -03:00, convertido para UTC). A tarefa e a que efetiva.
--   - Efetivar, na mesma transacao: UPDATE FUNCIONARIOS SET DATA_DESLIGAMENTO
--     = ULTIMO_DIA_TRABALHO (o trigger do grupo inativa a pessoa, desativa o
--     login e os beneficios e cancela as ferias futuras) e
--     UPDATE desta tabela para EFETIVADO. Se o ultimo dia ja passou, tudo
--     acontece na hora, sem esperar a tarefa.
--   - Cancelar: UPDATE para CANCELADO (so vale em AGENDADO) e cancelar a
--     tarefa.
--   - Readmitir: UPDATE FUNCIONARIOS SET DATA_DESLIGAMENTO = NULL, ATIVO = TRUE
--     e esta linha vai de EFETIVADO para REVERTIDO. As datas do vinculo
--     anterior ficam aqui (ULTIMO_DIA_TRABALHO) e no log de auditoria.
--   - A ficha do RH mostra "desligamento" a partir da linha vigente (se
--     houver), sem consultar o historico.
--
-- REGRAS QUE FICAM COM A APLICACAO
--   - Ultimo dia de trabalho nao pode ser anterior a data de admissao (400).
--     E regra entre duas tabelas: sem trigger, fica na aplicacao.
--   - A fatia 3 acrescenta a condicao "depois de a consolidacao do banco de
--     horas daquele dia fechar" antes de efetivar.
--   - Pessoa que gerencia um setor: ao efetivar, os pedidos que esperavam
--     por ela passam para o RH (CONVENCOES, secao 9).
--
-- FUSO: ULTIMO_DIA_TRABALHO e um dia de calendario (DATE, o dia no fuso
-- -03:00). CRIADO_EM, EFETIVADO_EM, CANCELADO_EM e REVERTIDO_EM sao UTC, em
-- tabela so da aplicacao (nenhum CHECK ou trigger a compara com colunas do
-- primeiro grupo). No EF Core, mapeie CRIADO_EM como gerada na inclusao e
-- VIGENTE como calculada (fora do INSERT).
--
-- TABELA NOVA: SEM TRIGGER (regra definida pelo dono em 2026-10-29).
-- ============================================================

CREATE TABLE DESLIGAMENTOS_AGENDADOS (
    ID_DESLIGAMENTO_AGENDADO INT AUTO_INCREMENT PRIMARY KEY,
    ID_FUNCIONARIO INT NOT NULL,
    ULTIMO_DIA_TRABALHO DATE NOT NULL,
    SITUACAO VARCHAR(10) NOT NULL DEFAULT 'AGENDADO',
    CRIADO_EM DATETIME(6) NOT NULL DEFAULT (UTC_TIMESTAMP(6)),
    EFETIVADO_EM DATETIME(6) NULL,
    CANCELADO_EM DATETIME(6) NULL,
    REVERTIDO_EM DATETIME(6) NULL,
    VIGENTE TINYINT GENERATED ALWAYS AS
        (IF(SITUACAO IN ('AGENDADO', 'EFETIVADO'), 1, NULL)) STORED,

    CONSTRAINT FK_DESLIGAMENTOS_AGENDADOS_FUNCIONARIOS
        FOREIGN KEY (ID_FUNCIONARIO)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    -- no maximo um desligamento vigente (AGENDADO ou EFETIVADO) por pessoa
    CONSTRAINT UQ_DESLIGAMENTOS_AGENDADOS_VIGENTE UNIQUE (ID_FUNCIONARIO, VIGENTE),
    CONSTRAINT CHK_DESLIGAMENTOS_AGENDADOS_SITUACAO
        CHECK (SITUACAO IN ('AGENDADO', 'EFETIVADO', 'CANCELADO', 'REVERTIDO')),
    -- os instantes andam com a situacao (SITUACAO e NOT NULL, entao nenhum
    -- dos lados da igualdade da NULL)
    CONSTRAINT CHK_DESLIGAMENTOS_AGENDADOS_EFETIVADO_EM
        CHECK ((SITUACAO IN ('EFETIVADO', 'REVERTIDO')) = (EFETIVADO_EM IS NOT NULL)),
    CONSTRAINT CHK_DESLIGAMENTOS_AGENDADOS_CANCELADO_EM
        CHECK ((SITUACAO = 'CANCELADO') = (CANCELADO_EM IS NOT NULL)),
    CONSTRAINT CHK_DESLIGAMENTOS_AGENDADOS_REVERTIDO_EM
        CHECK ((SITUACAO = 'REVERTIDO') = (REVERTIDO_EM IS NOT NULL))
) ENGINE=InnoDB DEFAULT CHARSET=utf8mb4 COLLATE=utf8mb4_unicode_ci;
