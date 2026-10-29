# Pedidos para o Kauan (banco de dados)

Lista corrida, atualizada em 2026-10-28. Cada item é algo que o Figma mostra ou que decidimos, e que o banco ainda não atende. A ideia é fazer **antes do primeiro deploy no Azure**: depois dele os scripts aplicados ficam congelados e qualquer mudança vira um script novo (com `ALTER`). Sem pressa para os itens, mas vale resolver antes disso.

## O que preciso que você ajuste

### Triggers de ponto (já em andamento)
1. **`TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE` ainda usam `NEW.HORA_ENTRADA` e `NEW.HORA_SAIDA`**, colunas que saíram de `PONTOS` no commit `e3eb1be`. Remover o bloco que valida "saída depois da entrada" e calcula `HORAS_TRABALHADAS` (as funções novas já calculam as horas pelas marcações; a coluna `PONTOS.HORAS_TRABALHADAS` deixa de ser preenchida por trigger).
2. **Carga horária.** `CARGOS.CARGA_HORARIA` passa a ser **semanal** (por exemplo 44). `PONTOS.CARGA_HORARIA` continua **por dia**. Ao copiar do cargo para o ponto, o trigger deve **dividir por 5** (44 ÷ 5 = 8,8). Hoje ele copia o valor sem dividir.

### Funcionários e empresa
3. **Matrícula** (texto, por exemplo `00482`) e **tipo de contrato** (CLT, estágio etc.; combinar os valores) em `FUNCIONARIOS`.
4. **Horário padrão da empresa** (um só para todos): horário de **entrada** e de **saída** e a **tolerância de atraso**, para o sistema calcular o atraso (o Figma mostra "Atraso de 14 min"). Sugestão: uma tabela de configuração da empresa com uma única linha, que também guardará o **nome da empresa**, a **UF** e o **município** (a UF escolhe os feriados estaduais). Se preferir outro desenho, combine comigo.

### Ponto
5. **`PONTOS_MARCACOES`: origem da batida**, para distinguir a batida feita pelo próprio funcionário da corrigida pelo RH (o Figma mostra "saída corrigida pelo RH").
6. **`PONTOS_MANUTENCOES`: a correção pedida.** Guardar a batida proposta pelo funcionário (data e hora e tipo `ENTRADA` ou `SAIDA`). Hoje só existe a justificativa em texto.
7. **`BANCOS_HORAS`: coluna "dia de referência"** com chave única `(ID_FUNCIONARIO, dia de referência)`. O banco de horas será **só automático** (sem lançamento manual), e essa coluna evita lançar o mesmo dia duas vezes e permite recalcular um dia depois de um ajuste aprovado. **Não** precisa de coluna de origem.

### Aprovação em duas etapas
8. **`PONTOS_MANUTENCOES` e `FERIAS`:** o pedido passa por **duas etapas**, o gestor analisa e **depois o RH confirma** (o RH também pode decidir direto). Precisa dos status novos e de um lugar para registrar quem confirmou e quando. Hoje só existe um aprovador. *(Os triggers dessas duas tabelas ficam para depois; por enquanto o pedido é só o esquema.)*

### Holerites
9. **Tipo do holerite** (mensal, 13º, férias; combinar os valores) e **ajustar a chave única** de `HOLERITES`. Hoje `(ID_FUNCIONARIO, ANO, MES)` só permite um holerite por pessoa por mês, e em dezembro o mensal e o 13º caem no mesmo mês. **Não** precisa da marca de "novo" ou "visto".

### Avisos
10. **`AVISOS`: fixar no topo e rascunho.**

### Advertências (com contestação)
11. **`ADVERTENCIAS`:** o funcionário confirma a **ciência** ou **contesta** com um texto, e o RH analisa. Sugestão: guardar a **data da ciência**, o **texto da contestação** e a **data da contestação**, e acrescentar o status **`CONTESTADA`** ao `CHK_ADVERTENCIAS_STATUS` (hoje só aceita `PENDENTE`, `APLICADA` e `CANCELADA`). Fluxo: `PENDENTE` (aguardando ciência) vira `APLICADA` (ciente) ou `CONTESTADA`; depois da análise do RH, `CONTESTADA` vira `APLICADA` (mantida) ou `CANCELADA` (acatada). **O trigger `TRG_ADVERTENCIAS_BEFORE_UPDATE` já permite essas transições** (só bloqueia sair de `CANCELADA` e voltar de `APLICADA` para `PENDENTE`). A resposta do RH vai por e-mail; guardar o texto dela é opcional e decisão sua.

## Ainda sem decisão

- Faltam os itens de formulários, treinamentos e férias (13º adiantado e "sugerir outra data").

## Decidido: nada a criar (só para você saber)

- **Sem unidades:** a empresa é um lugar só; feriado municipal vale para todos.
- **Sem período de experiência.**
- **Avisos:** sempre para todos, sem anexo PDF; o e-mail é sempre enviado na publicação.
- **Situação "em admissão":** é o funcionário com data de admissão no futuro; não precisa de coluna.
- **Inativar funcionário** continua como está hoje no seu trigger (desativa usuário e benefícios e cancela férias futuras).
- **Níveis de acesso** (`NIVEIS_DE_ACESSO`): os nomes serão `RH`, `Funcionario` e `Funcionario Gestor`. Quem faz a carga inicial sou eu, num script à parte; se você semear a tabela, use esses nomes.
- **Triggers:** por enquanto, fora esta lista, não é para mexer em nenhum.
