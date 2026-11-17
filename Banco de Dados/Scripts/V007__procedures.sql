-- ============================================================
-- V007: PROCEDURES (decisao do dono, 2026-11-17)
-- Reescrita do arquivo de procedures do grupo (RHSystem_Procedures_RHSystem_Atual.sql, 24 procedures) alinhada ao contrato da API
-- (contrato/) e aos scripts V001 a V006, testada em MySQL 8.4 (back-end/tests/.../Procedures).
--
-- Somente PROCEDURES: nenhuma tabela, FUNCTION ou TRIGGER e criada aqui. Cada procedure e UMA operacao transacional de negocio, com as mesmas
-- regras e os mesmos efeitos no BANCO que o caso de uso equivalente da API. O que fica de fora de proposito (a API faz e o banco nao pode):
-- autorizacao por perfil, envio de e-mail, arquivo no armazenamento, eventos de auditoria e tarefas agendadas.
-- Mensagens de recusa: SIGNAL SQLSTATE '45000', sem acento (como as dos triggers do V002).
-- ============================================================

DELIMITER $$

-- [[DOMINIO-A: PONTO]]
-- (preenchido pela secao A)






-- [[DOMINIO-B: FERIAS]]
-- (preenchido pela secao B)






-- [[DOMINIO-C: HOLERITE, ATESTADO E BENEFICIOS]]
-- (preenchido pela secao C)






-- [[DOMINIO-D: ADVERTENCIA, TREINAMENTO, FORMULARIO E DESLIGAMENTO]]
-- (preenchido pela secao D)






-- [[DOMINIO-E: CADASTROS]]
-- (preenchido pela secao E)






DELIMITER ;
