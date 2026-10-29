-- ============================================================
-- V021 - Beneficios: pedido de adesao
-- Item 15 de docs/pedidos-para-o-kauan.md
-- ============================================================
-- POR QUE
--   O funcionario pode PEDIR um beneficio opcional (Figma, "Aderir ao
--   plano odontologico"): escolhe quem entra (ele e dependentes), a data
--   de inicio e autoriza o desconto em folha. O RH confere e responde por
--   e-mail em ate 5 dias uteis (prazo da aplicacao). Hoje
--   BENEFICIOS_FUNCIONARIOS so guarda o vinculo ATIVO.
--
-- O QUE ESTE SCRIPT FAZ
--   Acrescenta em BENEFICIOS_FUNCIONARIOS o estado do pedido:
--   - STATUS_PEDIDO: PENDENTE, APROVADO, RECUSADO ou CANCELADO. As
--     linhas que ja existem viram APROVADO ao criar a coluna. DEPOIS
--     DISSO NAO HA PADRAO (a ultima instrucao do script o remove): toda
--     gravacao tem que informar o status, e o beneficio que o RH da direto
--     entra como APROVADO, informado pela aplicacao. Motivo: com padrao
--     APROVADO, uma tela do funcionario que esquecesse o status criaria um
--     vinculo aprovado e ativo, sem erro nenhum. (Para quem mapeia no EF
--     Core: nao declare padrao do banco para esta coluna, para o EF sempre
--     enviar o valor.)
--   - DATA_PEDIDO, DATA_INICIO_DESEJADA ("Comecar em") e
--     AUTORIZA_DESCONTO_FOLHA (a caixa "Autorizo o desconto mensal em
--     folha"),
--   - ID_FUNCIONARIO_RH, DATA_RESPOSTA e MOTIVO_RECUSA.
--
-- OS TRES PROBLEMAS QUE A LISTA APONTAVA E COMO FORAM RESOLVIDOS
--   1) Chave unica UQ_BENEFICIOS_FUNCIONARIOS_FUNCIONARIO_BENEFICIO (uma
--      linha por pessoa e beneficio): um pedido recusado, ou cancelado e
--      refeito, colidiria. SOLUCAO: a chave unica NAO muda. Refazer um
--      pedido e ATUALIZAR a mesma linha (volta a PENDENTE, nova
--      DATA_PEDIDO, limpa a resposta). A linha e a "relacao da pessoa com
--      o beneficio", o historico do que aconteceu fica no log de
--      auditoria da aplicacao. (Alternativa mais pesada: uma tabela
--      separada de pedidos. Nao fiz para manter o desenho do grupo e os
--      dependentes ligados a adesao, como o dono decidiu.)
--   2) BENEFICIO_ATIVO tem padrao VERDADEIRO. Um pedido pendente ou
--      recusado NAO PODE nascer ativo. SOLUCAO: o CHECK
--      CHK_BENEFICIOS_FUNCIONARIOS_ATIVO_SO_APROVADO so aceita
--      BENEFICIO_ATIVO = verdadeiro com STATUS_PEDIDO = APROVADO. Quem
--      cria um pedido TEM QUE gravar BENEFICIO_ATIVO = FALSE
--      explicitamente, se esquecer, o banco recusa (em vez de ativar sem
--      querer). E o beneficio dado direto pelo RH continua nascendo ativo
--      e aprovado, sem aparecer como pedido pendente.
--   3) A inativacao do funcionario (TRG_FUNCIONARIOS_AFTER_UPDATE) so
--      desliga o que esta ativo. Um pedido PENDENTE (BENEFICIO_ATIVO
--      falso) sobrevive a ela. Cuidado da aplicacao: nao aprovar pedido
--      de funcionario inativo e cancelar os pendentes ao inativar (ou
--      decidir outra regra com o dono).
--
-- AO REFAZER UM PEDIDO NA MESMA LINHA (cuidados da aplicacao)
--   - DATA_PEDIDO, MOTIVO_RECUSA e AUTORIZA_DESCONTO_FOLHA sao
--     sobrescritos. A autorizacao do desconto em folha e uma prova de
--     consentimento: a autorizacao ANTERIOR fica so no log de auditoria
--     (que precisa guardar este campo), e o novo pedido pede nova
--     autorizacao.
--   - Os dependentes (V024) e os trajetos (V025) da adesao anterior
--     continuam ligados a esta linha. Ao reabrir, a aplicacao tem que
--     marcar como CANCELADO os que nao fazem parte do novo pedido, senao
--     o filho de uma adesao antiga reaparece "aprovado" numa adesao nova.
--
-- O QUE O BANCO GARANTE (CHECK)
--   - pedido PENDENTE tem DATA_PEDIDO,
--   - pedido RECUSADO tem motivo, quem respondeu e quando,
--   - quem respondeu e quando andam juntos.
--
-- O FIGMA TAMBEM MOSTRA, em "Seus pedidos", a inclusao de dependente e a
-- atualizacao do trajeto do vale-transporte com situacao. Isso e
-- tratado nas tabelas DEPENDENTES (V024) e TRAJETOS_VALE_TRANSPORTE
-- (V025), que tem as mesmas colunas de situacao.
--
-- NENHUM TRIGGER E ALTERADO POR ESTE SCRIPT.
-- ============================================================

ALTER TABLE BENEFICIOS_FUNCIONARIOS
    ADD COLUMN STATUS_PEDIDO VARCHAR(20) NOT NULL DEFAULT 'APROVADO',
    ADD COLUMN DATA_PEDIDO DATETIME NULL,
    ADD COLUMN DATA_INICIO_DESEJADA DATE NULL,
    ADD COLUMN AUTORIZA_DESCONTO_FOLHA BOOLEAN NOT NULL DEFAULT FALSE,
    ADD COLUMN ID_FUNCIONARIO_RH INT NULL,
    ADD COLUMN DATA_RESPOSTA DATETIME NULL,
    ADD COLUMN MOTIVO_RECUSA TEXT NULL,

    ADD CONSTRAINT FK_BENEFICIOS_FUNCIONARIOS_FUNCIONARIOS_RH
        FOREIGN KEY (ID_FUNCIONARIO_RH)
        REFERENCES FUNCIONARIOS(ID_FUNCIONARIO),

    ADD CONSTRAINT CHK_BENEFICIOS_FUNCIONARIOS_STATUS_PEDIDO
        CHECK (STATUS_PEDIDO IN ('PENDENTE', 'APROVADO', 'RECUSADO', 'CANCELADO')),

    ADD CONSTRAINT CHK_BENEFICIOS_FUNCIONARIOS_ATIVO_SO_APROVADO
        CHECK (BENEFICIO_ATIVO = FALSE OR STATUS_PEDIDO = 'APROVADO'),

    ADD CONSTRAINT CHK_BENEFICIOS_FUNCIONARIOS_PENDENTE_COM_DATA
        CHECK (STATUS_PEDIDO <> 'PENDENTE' OR DATA_PEDIDO IS NOT NULL),

    ADD CONSTRAINT CHK_BENEFICIOS_FUNCIONARIOS_RESPOSTA_JUNTAS
        CHECK ((ID_FUNCIONARIO_RH IS NULL) = (DATA_RESPOSTA IS NULL)),

    ADD CONSTRAINT CHK_BENEFICIOS_FUNCIONARIOS_RECUSA_COMPLETA
        CHECK (STATUS_PEDIDO <> 'RECUSADO'
               OR (MOTIVO_RECUSA IS NOT NULL AND TRIM(MOTIVO_RECUSA) <> '' AND ID_FUNCIONARIO_RH IS NOT NULL));

-- sem padrao de proposito (ver o comentario no topo): as linhas que ja
-- existiam ficaram APROVADO, e toda linha nova tem que informar o status
ALTER TABLE BENEFICIOS_FUNCIONARIOS
    ALTER COLUMN STATUS_PEDIDO DROP DEFAULT;
