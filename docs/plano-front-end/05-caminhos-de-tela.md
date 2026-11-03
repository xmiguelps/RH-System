# 05 · Caminhos de tela (a tabela única)

Esta é a **única** tabela de caminhos (endereços) das telas. Ela vale sobre qualquer caminho proposto nas notas `telas\fatia-N.md` e sobre a tabela "Caminhos: o que as notas propuseram e o que a fundação fixou" de `fatia-1a-fundacao.md`. Os caminhos dos **itens de menu** são os de `src\app\menu.ts` (fatia 1a) e estão aqui sem mudança. Os demais seguem as regras abaixo. Trocar um caminho é **dúvida** (`01-regras.md`, seção D), não iniciativa.

Os números das telas são os dos PNG (`Figma\telas\NN-perfil-tela.png`). **7.1 a 7.6** são os números das telas sem print da fatia 7 (`telas\fatia-7.md`). Telas sem número são as sem print das outras fatias.

## Regras

1. **A família do caminho decide o perfil** (`PERFIS_POR_FAMILIA`, 1a): `/rh/...` só o RH, `/gestor/...` só o Gestor, `/eu/...` os três perfis. Perfil errado vê o 403 no lugar do conteúdo, com o endereço mantido. As páginas públicas ficam **fora** da área logada, sem menu.
2. **Nomes:** português sem acento, minúsculas, com hífen. O id vai no caminho (`:funcionarioId`); filtro, aba e período vão na **consulta** (`?aba=ponto`). Id que não é inteiro maior ou igual a 1 vale 404 (padrão da 1b).
3. **Painel lateral sobre uma lista** é uma rota **filha** da lista: a lista continua atrás (o pai termina com `<Outlet />`, como `/rh/colaboradores` na 1b). Fechar o painel volta ao pai **mantendo a consulta** da lista.
4. **Quais modais têm caminho e quais não** (a lista é esta, sem outra):
   - **Têm caminho:** o **13** (`?pedido=<id>`, e `?pedidos=abertos` sem pedido escolhido), o **55**, o **38** e o modal "Respostas de uma pessoa ou candidato" (sobre a 21), que abrem já num registro; e os **assistentes de várias etapas**, **17 a 20** (formulário) e **23 a 26** (treinamento): **um caminho por assistente** (`/novo` e `/:id/editar`) e **um passo por etapa**, em `?etapa=` (17 a 20: `detalhes`, `perguntas`, `verificacao`, `publicacao`; 23 a 26: `detalhes`, `conteudo`, `certificado`, `publicacao`). Os "painéis de atestados" **não são modais**: são painéis laterais com caminho (regra 3).
   - **Não têm caminho** (modal de formulário, de confirmação ou de detalhe aberto por um botão da própria tela; é estado da tela): **06**, **08**, **10**, **30**, "Substituir holerite", "Enviar holerites em lote", "Pedir ajuste de ponto", "Enviar atestado", "Ver pedido" (o detalhe do pedido de ajuste, aberto pela faixa da tela 32), "Contestar", "Ver detalhes" (a advertência da pessoa), "Incluir dependente", "Atualizar trajeto", "Sugerir outra data", "Recusar", "Trocar senha" e as janelas de confirmação (encerrar, excluir, inativar, readmitir).
5. **Aba** é `?aba=<valor>` (minúsculas, sem acento); sem `aba`, vale a aba marcada "(padrão)" na tabela; nas fichas (03 e 52) é a primeira da lista (abaixo). Nas listas com abas de contagem, a padrão é a que o **PNG mostra escolhida** (avisos: "Todos", e não "Não lidos"; li nos PNG 37 e 50).
6. **Menu:** o item só aparece quando a rota existe (1a). **Decidido pelo dono em 2026-11-02** (`DUVIDAS.md`, "Decisões do dono"; antes `D-menu`): o RH e o gestor ganham o grupo **"Meu espaço"** no fim do menu, com os itens pessoais **ativos**, que abrem as telas pessoais (`/eu/...`). **RH:** "Meu ponto" (`/eu/meu-ponto`), "Férias" (`/eu/ferias`), "Holerites" (`/eu/holerites`), "Minhas advertências" (`/eu/advertencias`), "Meus formulários" (`/eu/formularios`), "Meus treinamentos" (`/eu/treinamentos`) e "Benefícios" (`/eu/beneficios`). **Gestor** (que já tem Meu ponto, Férias, Holerites e Benefícios em "Seu dia a dia"): "Meus formulários", "Meus treinamentos" e "Minhas advertências" (`/eu/advertencias`, o mesmo item e a mesma rota do RH). "Meus documentos" (`/eu/meus-documentos`) continua nos três menus. O funcionário não ganha o grupo. Os caminhos acima são os da tabela "Qualquer perfil". (Os avisos pessoais, `/eu/avisos`, não estão na lista do dono para o RH nem para o gestor: o gestor já tem "Avisos" no menu do Figma e o RH tem "Avisos" de gestão.)

## Páginas públicas (sem login, sem menu)

| Tela | Caminho | Observação |
|---|---|---|
| Entrar (fatia 0) | `/entrar` | aceita `?voltar=<caminho>` (1a: `destinoSeguro`) |
| Esqueci minha senha | `/esqueci-senha` | 1a e 1b |
| Definir senha (convite e redefinição) | `/definir-senha?token=<token>` | fixo pelo contrato (o e-mail traz este endereço) |
| Formulário público do candidato (fatia 5) | `/formulario/:token` | `token` é o `tokenDoLink` do formulário |
| Conferir certificado (fatia 6) | `/certificado` e `/certificado/:codigo` | a segunda abre já conferindo o código |
| Trocar senha | sem caminho | modal aberto pelo rodapé do menu (1b) |

## RH (`/rh/...`)

| Tela | Caminho | Observação |
|---|---|---|
| 00 Painel inicial | `/rh/painel` | item "Painel" do menu |
| 7.5 Pendências do RH | `/rh/painel/pendencias` | página; `?tipo=` filtra; aberta por "Ver todas as pendências" |
| 7.6 Pessoas sem registro hoje | `/rh/painel/sem-registro` | página; aberta por "Ver quem está sem registro" |
| 01 Colaboradores (lista) | `/rh/colaboradores` | `?situacao=&setorId=&tipoContrato=&busca=&pagina=` |
| 02 Adicionar colaborador | `/rh/colaboradores/novo` | painel lateral sobre a lista. O modo "Importar planilha" (fatia 2) é **estado do painel**: não vai na URL |
| Enviar holerites em lote (fatia 4) | sem caminho | modal aberto pelo botão da lista 01 |
| 03 Ficha do colaborador | `/rh/colaboradores/:funcionarioId` | painel lateral sobre a lista; `?aba=` com `dados` (padrão), `ponto`, `ferias`, `holerites`, `documentos`, `advertencias` |
| 04 aba Ponto | `/rh/colaboradores/:funcionarioId?aba=ponto` | |
| 05 aba Férias | `/rh/colaboradores/:funcionarioId?aba=ferias` | |
| 06 Registrar férias | sem caminho | modal da aba Férias |
| 07 aba Holerites | `/rh/colaboradores/:funcionarioId?aba=holerites` | |
| 08 Enviar holerite e "Substituir holerite" | sem caminho | modais da aba Holerites |
| 09 aba Documentos | `/rh/colaboradores/:funcionarioId?aba=documentos` | |
| 10 Adicionar documento | sem caminho | modal da aba Documentos |
| 11 aba Advertências | `/rh/colaboradores/:funcionarioId?aba=advertencias` | "Registrar advertência" abre o painel da tela 15 **sobre a ficha**, sem caminho, com o colaborador já escolhido |
| 12 Ponto e feriados | `/rh/ponto-e-feriados` | item "Ponto e feriados" do menu. `?funcionarioId=<id>&ano=<aaaa>&mes=<1 a 12>` escolhe colaborador e período (a aba Ponto, tela 04, usa isto em "Abrir espelho completo") |
| 13 Pedidos de ajuste de ponto | `/rh/ponto-e-feriados?pedido=<pedidoId>` | modal sobre a tela 12, **já com esse pedido selecionado** (é o que o painel 00 e a lista 7.5 usam, com o `referenciaId`, e o "Revisar pedido" da linha do dia). `/rh/ponto-e-feriados?pedidos=abertos` abre o mesmo modal na lista, sem pedido escolhido (botão "Pedidos de ajuste" da tela 12). Trocar de pedido dentro do modal troca o `?pedido=` (sem empilhar histórico); fechar tira os dois |
| Atestados (RH, fatia 3) | `/rh/ponto-e-feriados/atestados` | painel lateral sobre a tela 12; `?situacao=ENVIADO` (padrão), `VALIDADO`, `RECUSADO` ou `TODOS`. Aberto pelo botão "Atestados" da 12 e pelo "Revisar" de atestado do painel 00 e da 7.5 |
| 14 Advertências (lista) | `/rh/advertencias` | item "Advertências" do menu |
| 15 Nova advertência | `/rh/advertencias/nova` | painel lateral sobre a lista 14 |
| Detalhe da advertência e "Responder contestação" | `/rh/advertencias/:advertenciaId` | painel lateral sobre a lista 14 (da aba da ficha, tela 11, "Abrir" abre o mesmo painel **sem caminho**) |
| 16 Formulários (lista) | `/rh/formularios` | item "Formulários" do menu |
| 17 a 20 Assistente do formulário | `/rh/formularios/novo` e `/rh/formularios/:formularioId/editar` | modal sobre a lista; a etapa vai em `?etapa=detalhes`, `perguntas`, `verificacao` ou `publicacao` |
| 21 Quem respondeu | `/rh/formularios/:formularioId/respondentes` | em formulário por link, a mesma rota mostra a lista de candidatos |
| Respostas de uma pessoa ou candidato | `/rh/formularios/:formularioId/respondentes/funcionarios/:funcionarioId` e `.../candidatos/:candidatoId` | modal sobre a tela 21 |
| Resultado do formulário | `/rh/formularios/:formularioId/resultado` | página |
| 22 Treinamentos e "Quem fez" | `/rh/treinamentos` | item "Treinamentos" do menu; `?quemFez=<treinamentoId>` abre a seção de baixo |
| 23 a 26 Assistente do treinamento | `/rh/treinamentos/novo` e `/rh/treinamentos/:treinamentoId/editar` | modal sobre a lista; `?etapa=detalhes`, `conteudo`, `certificado` ou `publicacao` |
| 27 Avisos (lista e detalhe) | `/rh/avisos` | item "Avisos" do menu; `?situacao=&tipo=&busca=&aviso=<avisoId>` (o aviso selecionado) |
| 28 Novo aviso | `/rh/avisos/novo` e `/rh/avisos/:avisoId/editar` | painel lateral sobre a lista 27 |
| 7.2 Pedidos de férias para o RH | `/rh/pedidos-de-ferias` | página sem item de menu; `?situacao=&setorId=&busca=&pagina=` e `?pedido=<feriasId>` destaca um pedido |
| 7.3 Pedidos de benefícios para o RH | `/rh/pedidos-de-beneficios` | página sem item de menu; `?situacao=&beneficioId=&busca=&pagina=` |
| 29 Cadastros | `/rh/cadastros` (leva a `/rh/cadastros/cargos`) | item "Cadastros" do menu. Itens: `/rh/cadastros/cargos`, `.../setores`, `.../gestores`, `.../beneficios`, `.../usuarios`, `.../empresa` e `.../pagamento` (7.4, fatia 7) |
| 30 Novo cargo (e "Editar cargo") | sem caminho | modal sobre `/rh/cadastros/cargos` |
| 31 Log de auditoria | `/rh/auditoria` | item "Log de auditoria" do menu |

## Qualquer perfil (`/eu/...`)

O funcionário, o gestor e o RH abrem as mesmas telas. O que muda é o **menu** (1a): o RH e o gestor chegam a elas pelo grupo "Meu espaço" (regra 6).

| Tela | Caminho | Observação |
|---|---|---|
| 32 Início (funcionário) e 46 Início (gestor) | `/eu/inicio` | a mesma página, com o menu do perfil |
| 33 Meu ponto e 47 Meu ponto (gestor) | `/eu/meu-ponto` | a mesma página |
| Pedir ajuste de ponto, Enviar atestado e a seção "Meus atestados" | sem caminho | modais e seção de `/eu/meu-ponto`; o cartão "Seu ponto hoje" de `/eu/inicio` abre os mesmos modais |
| "Ver pedido" (detalhe do pedido de ajuste, só leitura: `obterMeuPedidoDeAjuste`) | sem caminho | modal aberto pelo "Ver pedido" da faixa de pedido em análise da tela 32 (e da 46); não abre de outra tela |
| Extrato completo do banco de horas | `/eu/meu-ponto/extrato` | painel lateral sobre Meu ponto; "Ver extrato" da tela 32 também leva aqui; `?de=&ate=&tipo=&pagina=` |
| 34 Férias e 48 Férias (gestor) | `/eu/ferias` | a mesma página |
| 35 Pedir férias | `/eu/ferias/pedir` | painel lateral sobre `/eu/ferias`; "Pedir férias" da tela 32 também leva aqui |
| 36 Holerites e 49 Holerites (gestor) | `/eu/holerites` | `?ano=&tipo=` |
| 37 Avisos e 50 Avisos (gestor) | `/eu/avisos` | `?aba=todos` (**padrão**, a aba que o PNG 37 e o 50 mostram escolhida) ou `nao-lidos`; `?busca=` |
| 38 Aviso aberto | `/eu/avisos/:avisoId` | modal sobre `/eu/avisos`; "Ler aviso completo" da tela inicial também leva aqui |
| 39 e 40 Meus formulários | `/eu/formularios` | `?aba=todos` (padrão), `para-responder`, `disponiveis` ou `respondidos`; `?tipo=&busca=` |
| Responder um formulário | `/eu/formularios/:formularioId/responder` | `?avaliadoId=<id>` nas avaliações |
| Minhas respostas | `/eu/formularios/:formularioId/minhas-respostas` | |
| 41 e 42 Meus treinamentos | `/eu/treinamentos` | `?aba=todos` (padrão), `disponiveis`, `em-andamento` ou `concluidos`; `?obrigatorio=&busca=` |
| Fazer o treinamento | `/eu/treinamentos/:treinamentoId` | os módulos abrem dentro da página, sem caminho próprio |
| 43 Minhas advertências | `/eu/advertencias` | "Contestar" e "Ver detalhes" são modais sem caminho |
| 44 Benefícios | `/eu/beneficios` | "Incluir dependente" e "Atualizar trajeto" são modais sem caminho |
| 45 Aderir ao benefício | `/eu/beneficios/aderir/:beneficioId` | painel lateral sobre `/eu/beneficios` |
| Meus documentos | `/eu/meus-documentos` | item "Meus documentos" dos três menus |

## Gestor (`/gestor/...`)

| Tela | Caminho | Observação |
|---|---|---|
| 51 Painel da equipe | `/gestor/equipe` | item "Painel da equipe" do menu |
| 52 Ficha da equipe | `/gestor/equipe/:funcionarioId` | **painel lateral sobre o Painel da equipe** (51); `?aba=` com `dados` (padrão), `ponto`, `ferias`, `treinamentos`, `advertencias`. Ao fechar volta a `/gestor/equipe` |
| 7.1 aba Férias da ficha da equipe | `/gestor/equipe/:funcionarioId?aba=ferias` | |
| aba Ponto da ficha da equipe | `/gestor/equipe/:funcionarioId?aba=ponto` | |
| aba Treinamentos da ficha da equipe | `/gestor/equipe/:funcionarioId?aba=treinamentos` | |
| aba Advertências da ficha da equipe | `/gestor/equipe/:funcionarioId?aba=advertencias` | "Registrar advertência" (tela 53) abre o painel **sobre a ficha**, sem caminho; "Abrir" abre o detalhe também sem caminho |
| 54 Ponto da equipe | `/gestor/ponto-da-equipe` | item "Ponto da equipe" do menu; `?data=<aaaa-mm-dd>` |
| 55 Pedido de ajuste de ponto | `/gestor/ponto-da-equipe/pedidos/:pedidoId` | modal sobre a tela 54 |
| 56 Espelho de ponto da pessoa | `/gestor/ponto-da-equipe/espelho/:funcionarioId` | painel lateral sobre a tela 54; `?ano=&mes=` |
| Atestados da equipe | `/gestor/ponto-da-equipe/atestados` | painel lateral sobre a tela 54 |
| 57 Formulários do setor | `/gestor/formularios` | item "Formulários" do menu |
| 58 Treinamentos do setor | `/gestor/treinamentos` | item "Treinamentos" do menu |
| 59 Advertências do setor | `/gestor/advertencias` | item "Advertências" do menu |
| 60 Registrar advertência | `/gestor/advertencias/nova` | painel lateral sobre a lista 59 |
| Detalhe da advertência (gestor) | `/gestor/advertencias/:advertenciaId` | painel lateral sobre a lista 59 |

## De onde cada tela abre (as ligações entre telas)

| Quem abre | O gesto | Vai para |
|---|---|---|
| 00 Painel do RH | "Revisar" em ajuste de ponto | `/rh/ponto-e-feriados?pedido=<referenciaId>` (modal 13 já nesse pedido) |
| 00 e 7.5 | "Revisar" em férias | `/rh/pedidos-de-ferias?pedido=<referenciaId>` |
| 00 e 7.5 | "Revisar" em atestado | `/rh/ponto-e-feriados/atestados` |
| 00 e 7.5 | "Revisar" em admissão com documentos pendentes | `/rh/colaboradores/<funcionarioId>?aba=documentos` |
| 00 e 7.5 | "Revisar" em benefícios | `/rh/pedidos-de-beneficios` |
| 00 | "Ver todas as pendências"; "Ver quem está sem registro" | `/rh/painel/pendencias`; `/rh/painel/sem-registro` |
| 00 | "Publicar aviso"; "Adicionar colaborador"; busca de colaborador (Enter) | `/rh/avisos/novo`; `/rh/colaboradores/novo`; `/rh/colaboradores?busca=<texto>` |
| 00 | "Gerenciar treinamentos"; "Abrir log de auditoria" | `/rh/treinamentos`; `/rh/auditoria` |
| 04 aba Ponto | "Abrir espelho completo" | `/rh/ponto-e-feriados?funcionarioId=<id>&ano=<aaaa>&mes=<m>` |
| 12 Ponto e feriados | "Pedidos de ajuste"; "Revisar pedido" (linha do dia); "Atestados" | `?pedidos=abertos`; `?pedido=<pedidoEmAnaliseId>`; `/rh/ponto-e-feriados/atestados` |
| 16 Formulários | "Quem respondeu"; "Ver resultado" | `/rh/formularios/<id>/respondentes`; `/rh/formularios/<id>/resultado` |
| 22 Treinamentos | "Ver quem fez" | `/rh/treinamentos?quemFez=<id>` |
| 32 e 46 Início | "Pedir férias"; "Ver extrato"; "Ver holerites"; "Ver detalhes dos benefícios"; "Ler aviso completo"; "Ver pedido" (faixa de ajuste em análise) | `/eu/ferias/pedir`; `/eu/meu-ponto/extrato`; `/eu/holerites`; `/eu/beneficios`; `/eu/avisos/<avisoId>`; o modal "Ver pedido", sem caminho |
| 46 Início do gestor | "Analisar agora" (ajustes de ponto do setor) | `/gestor/equipe` |
| 51 Painel da equipe | "Abrir ficha"; "Avaliar a equipe" | `/gestor/equipe/<funcionarioId>`; `/eu/formularios/<formularioId>/responder` |
| 54 Ponto da equipe | cartão de pedido; "Ver espelho"; "Atestados da equipe" | `/gestor/ponto-da-equipe/pedidos/<pedidoId>`; `/gestor/ponto-da-equipe/espelho/<funcionarioId>`; `/gestor/ponto-da-equipe/atestados` |
| 57 Formulários do setor | "Continuar avaliação" | `/eu/formularios/<formularioId>/responder` |
| 58 Treinamentos do setor | "Continuar treinamento" | `/eu/treinamentos/<treinamentoId>` |
| 39 e 40 | "Responder" ou "Continuar"; "Ver minhas respostas" | `/eu/formularios/<id>/responder`; `/eu/formularios/<id>/minhas-respostas` |
| 41 e 42 | "Começar", "Continuar" e "Rever conteúdo" | `/eu/treinamentos/<id>` |
| 44 Benefícios | "Quero aderir" | `/eu/beneficios/aderir/<beneficioId>` |
| Qualquer tela | fechar um painel ou modal com caminho | o caminho pai, com a consulta mantida |

## O Painel da equipe e a ficha 52

A 1b criou a rota da ficha da equipe solta (`equipe/:funcionarioId`), porque a página do Painel da equipe ainda não existia. A **ficha 7 a reestrutura**: `equipe` passa a ser a página do Painel da equipe (tela 51, terminada com `<Outlet />`) e `:funcionarioId` vira filha dela, como `colaboradores` e `:funcionarioId` no lado do RH. O caminho `/gestor/equipe/:funcionarioId` **não muda**.
