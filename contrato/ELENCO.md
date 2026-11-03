# Elenco da demonstração

Os exemplos do contrato (e, por isso, os dados simulados do front e o seed da demo, fatia 8) usam **as mesmas pessoas, os mesmos ids e a mesma data**. Quem escreve um exemplo copia daqui; nenhum id abaixo pode aparecer com outro nome.

- **Hoje** nos exemplos: **quarta-feira, 2026-10-07, 09:42 no horário de Brasília** (`2026-10-07T12:42:00Z`), o dia que o Figma mostra. Horário padrão da empresa: entrada 08:00, saída 17:48 (jornada de 8 h 48 min com uma hora de almoço), tolerância de atraso 10 minutos. Dia de pagamento: o do exemplo da fatia 7 (próximo pagamento em `2026-11-06`).
- `funcionarioId` é o id da pessoa. `gestorId` é o id da linha dela em `GESTORES`.
- **Gestor**: só quem tem perfil `Gestor` é gestor de um setor (um gestor, um setor). **Setor sem gestor** (Recursos Humanos, Administrativo e Tecnologia): os pedidos de quem é de lá vão direto ao RH (`etapa: RH`, `gestor: null`), e as rotas `/api/gestor/...` não se aplicam. **Quem é gestor** (Carlos, Marina, Paulo) também tem os pedidos decididos só pelo RH. A Ana Souza e a Sofia Ramos (RH) têm linha em `GESTORES` **só** para poderem decidir os pedidos dos gestores (o trigger exige um `ID_GESTOR`); isso não as torna gestoras de setor nem dá acesso a `/api/gestor/...`.
- Setores, cargos e benefícios usam os ids que a fatia 1 já definiu (`20-fatia-1-colaboradores.yaml`): setor Comercial = 1, Financeiro = 2, Logística = 6, Compras = 7; Recursos Humanos = 3, Administrativo = 4, Tecnologia = 5. O setor do RH se chama **"Recursos Humanos"**.
- Treinamentos (fatia 6 é a dona): Onboarding 2026 = 1, LGPD na prática = 2, Segurança da informação = 3.
- Empresa: "Comercial Aurora Ltda", São Paulo (SP).

| funcionarioId | Nome | Cargo | Setor | Perfil | gestorId | Gestor do setor |
|---|---|---|---|---|---|---|
| 1 | Ana Souza | Gerente de RH | Recursos Humanos | RH | 4 | sem gestor |
| 2 | Sofia Ramos | Analista de RH | Recursos Humanos | RH | 5 | sem gestor |
| 3 | Roberto Alves | Analista de RH | Recursos Humanos | RH | | sem gestor |
| 4 | Carla Mendes | Assistente administrativa | Administrativo | Funcionario | | sem gestor |
| 7 | Carlos Ribeiro | Gerente comercial | Comercial | Gestor | 1 | (é o gestor do Comercial) |
| 8 | Marina Costa | Gerente financeira | Financeiro | Gestor | 2 | (é a gestora do Financeiro) |
| 9 | Paulo Vieira | Gerente de logística | Logística | Gestor | 3 | (é o gestor da Logística) |
| 12 | Bruno Lima | Analista pleno | Financeiro | Funcionario | | Marina Costa |
| 21 | Diego Farias | Vendedor | Comercial | Funcionario | | Carlos Ribeiro |
| 22 | Júlia Nunes | Vendedora | Comercial | Funcionario | | Carlos Ribeiro |
| 23 | Larissa Moura | Vendedora | Comercial | Funcionario | | Carlos Ribeiro |
| 24 | Pedro Santos | Vendedor | Comercial | Funcionario | | Carlos Ribeiro |
| 25 | Fernanda Lopes | Executiva de contas | Comercial | Funcionario | | Carlos Ribeiro |
| 26 | Gustavo Reis | Vendedor | Comercial | Funcionario | | Carlos Ribeiro |
| 27 | Helena Prates | Assistente comercial | Comercial | Funcionario | | Carlos Ribeiro |
| 28 | Igor Batista | Estagiário | Comercial | Funcionario | | Carlos Ribeiro |
| 31 | Thiago Mello | Auxiliar de logística | Logística | Funcionario | | Paulo Vieira |
| 40 | João Pedro Alves | Desenvolvedor | Tecnologia | Funcionario | | sem gestor |
| 41 | Vinícius Prado | Desenvolvedor pleno | Tecnologia | Funcionario | | sem gestor |
| 44 | Vanessa Lima | Assistente administrativa | Administrativo | Funcionario | | sem gestor |
| 104 | Rafael Torres | Vendedor | Comercial | Funcionario (começa em 2026-10-13, em admissão) | | Carlos Ribeiro |
| 105 | Tiago Nogueira | Estagiário | Tecnologia | Funcionario (importado) | | sem gestor |
| 200 | Lucas Prado | | | usado nas listas da fatia 1 | | |
| 201 | Henrique Alves | | | usado nas listas da fatia 1 | | |
| 202 | Aline Carvalho | | | usada no exemplo da importação | | |

## Cenário de hoje (os números que várias telas mostram)

Cada assunto tem **uma fonte**: as outras fatias copiam dela. Todo instante dos exemplos é **menor ou igual a `2026-10-07T12:42Z`** (exceto expirações de link e datas futuras reais).

- **Presença** (fonte: f7, painel do RH; 101 ativos com expediente, 09:42): trabalhando 86, férias 6, atestado 2, sem registro 7, ainda não começou 0 (soma 101). Equipe do Comercial (f1, `GET /api/gestor/equipe`; f3, tela 54): 8 pessoas, 6 trabalhando, 1 sem registro (Diego, 21), 1 de férias (Larissa, 23). Em férias hoje: Larissa e mais cinco pessoas fora do elenco (ids a partir de 300).
- **Pedidos de ajuste de ponto** (fonte: f3). **301** Bruno Lima, etapa RH (a Marina aprovou à mão em `2026-10-05T13:00Z`; a saída de 02/10 está sem registro: é o mais antigo da tela 00); **302** Carla Mendes, etapa RH (sem gestor); **303** Júlia Nunes e **304** Helena Prates, etapa GESTOR (Carlos Ribeiro); **305** Pedro Santos, concluído (aprovado pelo gestor e pelo RH); **306** João Pedro Alves e **307** Vanessa Lima, etapa RH (sem gestor). Fila do RH = 4 (301, 302, 306, 307); fila do Carlos = 2 (303, 304); bolinha do menu do RH "Ponto e feriados" = 4. Todo pedido é criado **depois** do dia a que se refere, e decidido depois de criado.
- **Gestor ausente** (fonte: f1): **Paulo Vieira** (gestorId 3) está ausente de `2026-10-06` até `2026-10-19`; ao ligar, não havia pedido parado (`pedidosAprovadosAgora`: 0 ajustes e 0 férias). **Marina e Carlos estão presentes.**
- **Férias** (fonte: f7). Fila do RH = 3: **338** Fernanda Lopes (se sobrepõe a férias já aprovadas na equipe Comercial; Carlos aprovou), **339** Gustavo Reis (Carlos aprovou), **341** Roberto Alves (RH, sem gestor). Com o gestor: **340** Bruno Lima (Marina; **15 dias, de 2026-11-16 a 2026-11-30**; Bruno tem saldo 18 e 12 dias já tirados, e **só o 340** em análise: pela regra das partes, uma parte com pelo menos 14 dias e nenhuma com menos de 5, com 18 dias de saldo ele só pode pedir **uma** parte de 14 a 18 dias) e do Pedro Santos (admitido em 2022-08-08, saldo 30 dias no período 2025-08-08 a 2026-08-07): **345** (15 dias, de 2026-11-10 a 2026-11-24, com o gestor Carlos) e **346** (8 dias; Carlos sugeriu outra data em `2026-10-06` e a sugestão espera a resposta do Pedro; por estar em sugestão não entra na lista padrão nem no calendário do gestor). Admitido em 2022-03-14, Bruno tem um período aquisitivo por ano de casa; Diego (admitido em 2023-08-08) também.
- **Atestados** (fonte: f3). **41** Bruno Lima e **45** Vinícius Prado, validados, cobrem hoje (os 2 de "atestado" na presença); **42** Diego Farias, validado, 29 e 30/09; **43** Carla Mendes e **44** João Pedro Alves, enviados hoje e **pendentes de validação** (o "2 atestados para validar" do painel). Carla e João Pedro **não** estão em `ATESTADO` hoje.
- **Admissões** (fonte: f1). Em admissão: Rafael Torres (104, começa 2026-10-13, cadastrado em 2026-09-28), Tiago Nogueira (105) e Aline Carvalho (202), estes dois importados em `2026-10-07T12:41Z`, começam em `2026-10-19`. Formulário de admissão (id 11, fonte f5) atribuído aos três, **ninguém respondeu ainda**. "Admissão com documentos pendentes" no painel conta quem começa em **até 7 dias** sem responder: só o Rafael (1).
- **Benefícios** (fonte: f1, catálogo): 1 Vale-refeição, 2 Plano de saúde, 3 Plano odontológico, 4 Vale-transporte, 5 Seguro de vida, 6 Auxílio-creche, 7 Vale-cultura. A fatia 7 só usa ids e nomes do catálogo da fatia 1.
- **Setores** (fonte: f1; `total` 7, todos numa página): Comercial (gestor Carlos, `gestorId` 1), Financeiro (Marina, 2), Logística (Paulo, 3), Recursos Humanos, Administrativo, Tecnologia e Compras **sem gestor**. A lista de gestores (sem paginação) tem 5 linhas: Carlos, Marina, Paulo e, só para decidir pedidos de gestores, Ana Souza (4) e Sofia Ramos (5).
- **Pagamento** (fonte: f7): próximo pagamento `2026-11-06`, competência outubro (mês anterior ao do pagamento: a regra é do mês anterior).
- **Datas do painel do RH** (fonte: f7), "Próximos dias": 12/10 Nossa Senhora Aparecida (feriado), 15/10 aniversário de Marina Costa, 21/10 aniversário de Sofia Ramos, 28/10 feriado municipal. (Sem "fim de período de experiência" nem "treinamento presencial".)
- **Formulários** (fonte: f5): a pesquisa de clima anônima é "Pesquisa de clima 2026" (prazo 2026-10-31; o lembrete automático dela só sai em 24/10: no log de hoje não há lembrete dela); a avaliação do gestor e o formulário 11 de admissão também são de f5.
- **Auditoria** (fonte: f1, "Últimas alterações" do painel do RH vem dela): o painel mostra só ações que mudam dado (criou, editou, excluiu, aprovou, recusou, publicou, inativou, importou...), nunca batida de ponto, download, login nem acesso negado.

A equipe do Comercial de Carlos Ribeiro tem **8 pessoas** (21 a 28, sem o gestor), como na tela 51; o gestor não conta como membro da própria equipe, e quem está em admissão (Rafael Torres, 104) também não entra. Quem não aparece aqui pode usar ids a partir de 300 sem conflito.
