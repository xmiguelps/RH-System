-- ============================================================
-- V047 - Indices para as listas e filas das fatias 3, 5, 6 e 7
-- ============================================================
-- POR QUE
--   O contrato tem listas filtradas (filas de pedidos do RH e do gestor,
--   calendario de ferias, "esta pessoa tem atestado hoje?") e o agendador faz
--   perguntas do tipo "quais formularios ja estao na hora de publicar?". As
--   chaves estrangeiras ja criam um indice por coluna de ligacao (a lista de uma
--   pessoa ja e rapida), mas nenhuma cobre o filtro por situacao e data. Sao poucas
--   linhas num TCC, entao isto nao e para ganhar tempo agora: e para o plano de
--   consulta nao virar varredura quando a demonstracao tiver 300 pessoas e anos
--   de ponto. Cada indice abaixo diz qual lista do contrato ele atende.
--
-- O QUE ESTE SCRIPT FAZ
--   So cria indices (nenhuma coluna, nenhum CHECK, nenhum dado).
--
-- POR QUE O FIM VEM ANTES DO INICIO NOS INDICES DE PERIODO
--   "Quem esta de ferias ou com atestado hoje" e "ferias da semana" perguntam
--   fim >= hoje e inicio <= hoje. Quase tudo comecou no passado, entao filtrar
--   pelo inicio quase nao poupa linha, e filtrar pelo fim poupa quase todas (so
--   sobra o que ainda nao acabou). Por isso o indice e (situacao, fim, inicio).
--   Medido (EXPLAIN ANALYZE, MySQL 8.4.13, 20000 atestados, 27 cobrem hoje): com o
--   fim primeiro o indice le 27 linhas, com o inicio primeiro le 18000.
--
-- QUAL INDICE ATENDE O QUE
--   PONTOS_MANUTENCOES (STATUS_SOLICITACAO, DATA_SOLICITACAO)
--       filas de pedidos de ajuste (status PENDENTE, do mais antigo ao mais novo)
--   PONTOS (DATA)
--       "ponto da equipe" e "presenca agora": todas as pessoas num dia so
--   ATESTADOS (STATUS_ATESTADO, DATA_ENVIO)
--       fila de atestados do RH (ENVIADO, do mais antigo)
--   ATESTADOS (STATUS_ATESTADO, DATA_FIM, DATA_INICIO)
--       "quem tem atestado validado que cobre o dia?" (situacao de hoje, falta
--       compensada, consolidacao do banco de horas). A lista de uma pessoa so ja
--       e rapida pela chave estrangeira.
--   FERIAS (STATUS_FERIAS, DATA_FIM, DATA_INICIO)
--       ferias que cobrem hoje, calendario da equipe, agendador (APROVADA para
--       EM_GOZO e CONCLUIDA)
--   FERIAS (STATUS_FERIAS, DATA_SOLICITACAO)
--       filas de ferias do gestor e do RH
--   BENEFICIOS_FUNCIONARIOS, DEPENDENTES e TRAJETOS_VALE_TRANSPORTE
--   (STATUS_PEDIDO, DATA_PEDIDO)
--       fila do RH "esperam o RH" (adesoes, dependentes e trajetos PENDENTE)
--   FORMULARIOS (ATIVO, DATA_ENCERRAMENTO, DATA_PUBLICACAO)
--       agendador: publicar o que esta agendado e ja venceu
--   FORMULARIOS (PROXIMA_REABERTURA)
--       agendador: reabrir os formularios recorrentes
--   FORMULARIOS (DATA_PRAZO)
--       agendador: encerrar pelo prazo e lembrete 7 dias antes
--   FORMULARIOS (TIPO_FORMULARIO, DATA_CRIACAO)
--       lista do RH por aba (entrevista, admissao, avaliacao...), do mais novo
--   FORMULARIOS_RESPOSTAS (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO)
--       "quem respondeu" e a conferencia de JA_RESPONDIDO (nao ha chave unica por
--       pessoa em formulario com nome)
--   FORMULARIOS_CANDIDATOS (DATA_CADASTRO)
--       job de retencao: apagar os candidatos vencidos (roadmap, decisao 13)
--   TREINAMENTOS (PUBLICADO, ENCERRADO_EM, DATA_INICIO)
--       situacao do treinamento (rascunho, agendado, em andamento, encerrado)
--   TREINAMENTOS (TIPO_TREINAMENTO)
--       lista de treinamentos de onboarding (valor ONBOARDING)
--
-- NAO CRIEI INDICE PARA
--   - listas por pessoa (as chaves estrangeiras ja cobrem),
--   - busca por texto no titulo (LIKE com % no comeco nao usa indice),
--   - o que o contrato calcula sobre poucas linhas (setores, cargos).
--
-- ESTE SCRIPT DEPENDE DE
--   V042 (TREINAMENTOS.ENCERRADO_EM). Os demais indices so usam colunas dos
--   scripts V006 a V029 e do esquema do grupo.
--
-- FUSO: nao ha coluna de data neste script.
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE PONTOS_MANUTENCOES
    ADD INDEX IDX_PONTOS_MANUTENCOES_STATUS_DATA (STATUS_SOLICITACAO, DATA_SOLICITACAO);

ALTER TABLE PONTOS
    ADD INDEX IDX_PONTOS_DATA (DATA);

ALTER TABLE ATESTADOS
    ADD INDEX IDX_ATESTADOS_STATUS_ENVIO (STATUS_ATESTADO, DATA_ENVIO),
    ADD INDEX IDX_ATESTADOS_STATUS_PERIODO (STATUS_ATESTADO, DATA_FIM, DATA_INICIO);

ALTER TABLE FERIAS
    ADD INDEX IDX_FERIAS_STATUS_PERIODO (STATUS_FERIAS, DATA_FIM, DATA_INICIO),
    ADD INDEX IDX_FERIAS_STATUS_SOLICITACAO (STATUS_FERIAS, DATA_SOLICITACAO);

ALTER TABLE BENEFICIOS_FUNCIONARIOS
    ADD INDEX IDX_BENEFICIOS_FUNCIONARIOS_STATUS_PEDIDO (STATUS_PEDIDO, DATA_PEDIDO);

ALTER TABLE DEPENDENTES
    ADD INDEX IDX_DEPENDENTES_STATUS_PEDIDO (STATUS_PEDIDO, DATA_PEDIDO);

ALTER TABLE TRAJETOS_VALE_TRANSPORTE
    ADD INDEX IDX_TRAJETOS_VT_STATUS_PEDIDO (STATUS_PEDIDO, DATA_PEDIDO);

ALTER TABLE FORMULARIOS
    ADD INDEX IDX_FORMULARIOS_AGENDADOR (ATIVO, DATA_ENCERRAMENTO, DATA_PUBLICACAO),
    ADD INDEX IDX_FORMULARIOS_PROXIMA_REABERTURA (PROXIMA_REABERTURA),
    ADD INDEX IDX_FORMULARIOS_DATA_PRAZO (DATA_PRAZO),
    ADD INDEX IDX_FORMULARIOS_TIPO_CRIACAO (TIPO_FORMULARIO, DATA_CRIACAO);

ALTER TABLE FORMULARIOS_RESPOSTAS
    ADD INDEX IDX_FORMULARIOS_RESPOSTAS_RESPONDENTE (ID_FORMULARIO, ID_FUNCIONARIO, ID_AVALIADO);

ALTER TABLE FORMULARIOS_CANDIDATOS
    ADD INDEX IDX_FORMULARIOS_CANDIDATOS_CADASTRO (DATA_CADASTRO);

ALTER TABLE TREINAMENTOS
    ADD INDEX IDX_TREINAMENTOS_SITUACAO (PUBLICADO, ENCERRADO_EM, DATA_INICIO),
    ADD INDEX IDX_TREINAMENTOS_TIPO (TIPO_TREINAMENTO);
