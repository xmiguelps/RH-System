# RH-System: design

Data: 2026-10-21 · Status: aprovado pelo Miguel em 2026-10-21 (seções 1 a 13); em 2026-10-28 a abordagem do banco mudou para scripts SQL versionados (seções 2, 3, 4, 5, 7, 8, 10, 11, 12 e 13.4 atualizadas, aguardando nova aprovação) · Tipo: TCC, com demo publicada no Azure

## 1. Objetivo e contexto

Sistema web de RH para cadastro e acompanhamento de colaboradores, ponto, comunicação, documentos, formulários e treinamentos. É um projeto acadêmico: o critério de sucesso é uma demo funcional no Azure, uma arquitetura defendível diante da banca (Clean Architecture) e um DER/MER e script SQL finais que batam com o banco real.

**Fontes deste design:** o esboço do dono, os scripts do grupo em `Banco de Dados/` (versão de 2026-10-28: [RHSystem_Criacao_Tabelas_MySQL.sql](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql) com 25 tabelas, [RHSystem_Criacao_Triggers_MySQL.sql](../../../Banco%20de%20Dados/RHSystem_Criacao_Triggers_MySQL.sql) com 36 triggers e [RHSystem_Functions_MySQL_Unificadas.sql](../../../Banco%20de%20Dados/RHSystem_Functions_MySQL_Unificadas.sql) com 8 funções; o dono informou que essa base **ainda não está 100% pronta**) e as respostas do dono durante o brainstorming. O protótipo do Figma (alta complexidade) foi lido em texto (seção 13). A pasta `Documentação/` não foi usada, a pedido do dono.

### Funcionalidades por perfil

**RH (acesso total):** gerenciar colaboradores (adicionar, editar, inativar, visualizar), importar colaboradores por Excel, criar formulários e quiz (entrevista de emprego automática, formulário de admissão, avaliação de funcionários para superiores e vice-versa), ponto (calendário do Brasil com feriados municipais cadastráveis, visualização e edição por funcionário), advertências, avisos simples e detalhados, holerites por funcionário, TAD (treinamentos de onboarding e similares), calendário do RH, notificações por e-mail e log de auditoria.

**Funcionário (acesso básico):** bater ponto, pedir manutenção de ponto (justificativa obrigatória), apresentar atestado, ver banco de horas, saldo de férias e próximo pagamento, pedir férias, ver holerites recebidos, ver aniversariantes, ver avisos, responder formulários pendentes, ver benefícios e emitir certificado após concluir um TAD.

**Gestor:** tudo do funcionário, mais o painel da equipe do **setor** de que é responsável. Da equipe vê contato, contrato, jornada, ponto, treinamentos, formulários respondidos e advertências. Faz a primeira etapa de aprovação de ajustes de ponto e de férias e registra advertências. **Não vê** CPF, endereço, holerites nem documentos, e não edita o cadastro (decisão do dono em 2026-10-21, seguindo o Figma).

## 2. Stack e restrições

- **Back-end:** C# no .NET 10 com Clean Architecture e EF Core **9.x**.
- **Banco:** MySQL 8.4, com o esquema mantido por **scripts SQL versionados** (seção 4). O EF Core 9.x com `Pomelo.EntityFrameworkCore.MySql` **9.0.0** só mapeia e consulta; **não há migrations do EF**.
- **Front-end:** React com TypeScript. Bibliotecas de UI, rotas e formulários serão escolhidas depois de ver o Figma.
- **Hospedagem:** API no Azure App Service, React no Azure Static Web Apps, Azure Database for MySQL Flexible Server e Azure Blob Storage.
- **Controle de versão:** GitHub (`xmiguelps/RH-System`), com GitHub Actions.

**Sobre o provider.** Pela API do NuGet (lida via resumo de ferramenta), o Pomelo termina na versão estável 9.0.0 e não existe versão 10.x. Um post de terceiros afirma que o Pomelo 9.0.0 exige EF Core 9.x e que o EF Core 9 roda no runtime do .NET 10. Isso é **inferido, não testado**; o teste de fumaça da fatia 0 confirma. Plano B: o fork Microting (`Microting.EntityFrameworkCore.MySql` 10.0.x, não oficial) ou trocar para SQL Server.

## 3. Arquitetura

```
RH-System/
  back-end/
    src/
      RHSystem.Domain          entidades, enums e regras de negócio puras
      RHSystem.Application     casos de uso, DTOs, interfaces, validações
      RHSystem.Infrastructure  EF Core + MySQL, executor de scripts SQL, Blob Storage, e-mail, Excel, jobs
      RHSystem.Api             endpoints, autenticação JWT, tratamento de erros, Swagger
    tests/
      Domain.Tests, Application.Tests, Api.IntegrationTests
  front-end/                   React + TypeScript
  Banco de Dados/ (com Scripts/, os scripts SQL versionados), Figma/, Documentação/   (já existem)
  docs/                        specs e planos
```

- **Dependências:** Api e Infrastructure dependem de Application; Application depende de Domain; Domain não depende de nada.
- **Monólito modular:** dentro de cada projeto há uma pasta por módulo (Colaboradores, Ponto, Avisos e assim por diante). Um único deploy.
- **Casos de uso** são classes simples, sem biblioteca de mediação.
- **Entidades do Domain** são mapeadas direto pelo EF (Fluent API na Infrastructure), sem um segundo modelo de persistência. **O professor não exige a separação** (dono, 2026-10-28).
- **Testes de integração** usam MySQL real em contêiner (exige Docker na máquina de desenvolvimento).

## 4. Modelo de dados e perfis

O banco é mantido por **scripts SQL versionados** (decisão do dono em 2026-10-28), no estilo database-first: os scripts do grupo são a fonte da verdade e o EF só mapeia as tabelas.

- Os scripts ficam em `Banco de Dados/Scripts/`, com nomes `V001__esquema_inicial.sql`, `V002__triggers.sql`, `V003__funcoes.sql`, `V004__...`. A `V001` parte do script de tabelas do grupo **sem o bloco de limpeza (`DROP TABLE`) e sem os selects de validação**; a `V002` é o script de triggers e a `V003` o de funções, ambos sem a linha `USE`. **Os números de versão são do dono:** o grupo envia o conteúdo e o dono numera.
- Um executor próprio, na inicialização da API (por configuração), aplica em ordem os scripts ainda não aplicados e registra cada um na tabela `SCHEMA_VERSOES` com o hash do conteúdo. Ele trata os blocos `DELIMITER` por conta própria.
- **Script já aplicado em um banco nunca é editado:** o executor recusa subir se o hash mudou. Mudanças futuras entram como script novo com `ALTER` (por exemplo `V005__telefone_emergencia.sql`).
- **A base do grupo ainda não está 100% pronta** (dono, 2026-10-28). Enquanto nenhum ambiente com dados a tiver aplicado, os scripts podem ser corrigidos e os bancos locais recriados. **A partir do primeiro deploy no Azure (fim da fatia 0), todo script aplicado fica congelado** e toda mudança vira script novo (`ALTER` para tabelas; `DROP` e `CREATE` para trigger e função). A CI vigia isso com uma lista de hashes dos scripts congelados e recusa `.sql` solto fora de `Banco de Dados/Scripts/` e `Validacao/`.
- Um teste de integração compara o mapeamento do EF com o banco criado pelos scripts (as colunas mapeadas existem e toda coluna obrigatória sem valor padrão está mapeada), para que uma mudança de script que desalinhe o C# quebre o build.
- Os triggers do v2 guardam regras de integridade no banco (por exemplo: ninguém é gestor de si mesmo, usuário só para funcionário ativo com e-mail corporativo, ponto não anterior à admissão). São **rede de segurança**: a aplicação valida antes, com mensagens próprias, e converte o erro de trigger (`SQLSTATE 45000`) em resposta 422 sem stack trace, com texto genérico em português e a mensagem original numa extensão. As 8 funções do grupo (saldo do banco de horas, horas trabalhadas, consistência do ponto e outras) podem ser chamadas pela aplicação ou reimplementadas no Domain; a escolha é feita em cada fatia. **Em correção pelo Kauan (2026-10-28):** `TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE` usam `HORA_ENTRADA` e `HORA_SAIDA`, colunas removidas de `PONTOS` (as batidas estão em `PONTOS_MARCACOES`). A correção esperada remove esse bloco, e `PONTOS.HORAS_TRABALHADAS` deixa de ser preenchida por trigger (as funções calculam pelas marcações). Até a correção chegar, a `V002` provavelmente não aplica.
- O MySQL não desfaz DDL em transação: um script que falha no meio deixa o banco parcialmente alterado. Os scripts são pequenos; a recuperação é recriar o banco (local) ou corrigir à mão (Azure).
- O DER/MER é atualizado a partir dos scripts na última fatia.

**Esquema do grupo (v2) adotado por este design** (no lugar das mudanças estruturais que eu havia proposto; o spec antigo mandava remover três tabelas):

1. `GESTORES`, `FUNCIONARIOS.ID_GESTOR` e `SETORES.ID_GESTOR` **permanecem**, como no v2. **Regra da aplicação (decisão do dono):** a equipe de um gestor são os funcionários do(s) setor(es) em que ele é o gestor (`SETORES.ID_GESTOR`). O cadastro preenche `FUNCIONARIOS.ID_GESTOR` com o gestor do setor escolhido; um select de validação aponta divergências. Confirmar com o grupo se uma pessoa pode ter gestor diferente do gestor do setor.
2. `FUNCIONARIOS_DETALHES` **permanece** e guarda os dados pessoais restritos por LGPD (CPF, RG, endereço, e-mail pessoal, telefone). Só endpoints do RH e do próprio colaborador a leem, o que concretiza a decisão de que o gestor não vê CPF nem endereço. Como as respostas do formulário de admissão chegam a esses campos é definido na fatia 5. O histórico de alterações é coberto pela auditoria.
3. `NIVEIS_DE_ACESSO` **permanece**. Um script semeia três níveis com os nomes `RH`, `Funcionario` e `Funcionario Gestor` (confirmados pelo dono em 2026-10-28), que o Domain mapeia pelo nome para `Perfil.RH`, `Perfil.Funcionario` e `Perfil.Gestor`; funcionário sem nível ou com nível desconhecido não entra (403). `USUARIOS` guarda só a ligação com o funcionário, o hash da senha e o último login; **nome e e-mail de login vêm de `FUNCIONARIOS` (`EMAIL_CORPORATIVO`)**, e não pode existir usuário sem funcionário.
4. Inativar não apaga: o funcionário continua no banco com holerites e histórico, e perde o login.

**Autorização.** Cada caso de uso recebe o usuário logado e passa por **um filtro de escopo único**: RH vê tudo; Gestor vê a equipe do setor, **só os campos permitidos** (contato, contrato, jornada, ponto, treinamentos, formulários e advertências); Funcionário vê só a si mesmo. CPF, endereço, holerites e documentos são só do RH e do próprio colaborador. A restrição vale também por **campo**: cada perfil recebe um DTO diferente, e esconder o dado só na tela não conta. Os testes de integração cobrem uma matriz de perfil por endpoint, verificando linhas e campos.

**Arquivos** (holerite, atestado, certificado) ficam no Blob Storage, e o banco guarda só a referência. O download passa por um endpoint que valida o escopo e devolve um link temporário. Nunca há link público.

**O que falta no esquema, por fatia** (cada item vira um script novo; o desenho detalhado vem no plano da fatia; o mapeamento do Figma está na seção 13.3 e as diferenças em relação ao v2, na 13.4):
- Fundação: refresh tokens (nova tabela), auditoria (com IP e origem), importações em lote, fila de e-mails e tarefas agendadas.
- Colaboradores: documentos do colaborador e configuração da empresa (nome, UF e município). **Sem unidades** (decisão do dono em 2026-10-28).
- Ponto: segunda etapa de aprovação do ajuste e atestados (as várias marcações por dia já existem em `PONTOS_MARCACOES`).
- Comunicação e documentos: holerites com tipo e agendamento, advertências com ciência e contestação.
- Formulários: ciclos de avaliação, atribuição a quem deve responder (gera os "pendentes"), respondentes externos, pontuação do quiz.
- TAD: módulos, progresso, inscrições em turmas.
- Gestão: segunda etapa de aprovação de férias, pedidos de benefício e dependentes, regra e exceções de pagamento.
- Já existem no esquema do grupo e são aproveitados: `PONTOS` e `PONTOS_MARCACOES`, `FERIAS`, `FERIADOS` (por UF e município), `BANCOS_HORAS`, `AVISOS`, `HOLERITES`, `ADVERTENCIAS`, `TREINAMENTOS` e certificados, `FORMULARIOS`, `BENEFICIOS`.

## 5. Autenticação, e-mails e auditoria

**Login.** E-mail e senha, com a senha guardada só como hash. O token de acesso é um JWT de vida curta, com refresh token no banco, revogável e com rotação. Como o React e a API ficam em domínios Azure diferentes, e navegadores costumam bloquear cookies entre domínios, os tokens vão por cabeçalho. O custo é um risco maior de roubo de token em caso de XSS, mitigado com expiração curta, rotação do refresh e cabeçalhos de segurança no front. **O refresh token fica no `localStorage`**, para o usuário não perder o login ao fechar a aba ou o navegador (decisão do dono em 2026-10-28); como as abas o compartilham, a renovação é serializada entre elas e o logout em uma aba vale para todas.

**Primeiro acesso e "esqueci minha senha"** usam a mesma mecânica: link por e-mail com token de uso único e prazo curto, guardado só como hash. Há limite de tentativas de login. O funcionário inativo não entra e perde o refresh token; se digitar a senha certa vê "Seu acesso está desativado. Fale com o RH." (403), e com a senha errada recebe a mesma resposta genérica de credencial inválida (decisão do dono em 2026-10-28). Funcionário sem nível de acesso ou com nível desconhecido também não entra (403, "Seu perfil de acesso não está configurado. Fale com o RH.").

**E-mails.** O back-end decide quando enviar, e uma interface com implementação trocável envia. O provedor (por exemplo o serviço de e-mail do Azure ou SMTP) é decidido na fatia 1 e ainda **não foi verificado**. No desenvolvimento, os e-mails são capturados localmente. Os e-mails são gravados numa **fila no banco**, na mesma transação do fato que os gerou, e um serviço em segundo plano os envia com novas tentativas.

**Tarefas agendadas.** O mesmo serviço em segundo plano da API executa as publicações agendadas (holerite, aviso, formulário e treinamento), a consolidação diária do banco de horas e os lembretes automáticos. O estado fica no banco, as tarefas são idempotentes, e há uma instância só.

**Eventos que disparam e-mail** (aprovados): (1) convite e redefinição de senha; (2) novo aviso publicado; (3) holerite disponível; (4) formulário atribuído; (5) resultado de pedido de ajuste de ponto e de férias; (6) advertência registrada.

**Eventos adicionais vindos do Figma, a confirmar:** lembretes manuais ("lembrar quem falta") e o lembrete automático do formulário de admissão; treinamento publicado e lembrete 7 dias antes do prazo; resposta a pedido de benefício; férias registradas pelo RH.

**Auditoria.** Um interceptor do EF registra quem, quando, qual entidade e quais campos mudaram (antes e depois). Ações que não alteram dados (login, tentativa de acesso negado, marcação de ponto, download de holerite, importação, e-mails automáticos) são registradas explicitamente, com **IP e origem**. Senhas, hashes e tokens ficam fora do log. O log é só de inclusão, sem endpoint de edição ou exclusão, e só o RH consulta, com filtros por período, pessoa, área e ação e exportação em CSV.

## 6. Importação de colaboradores por Excel

O botão na tela de cadastro alterna para o modo "Importar planilha". Fluxo:

1. O RH baixa o **modelo da planilha**, com as colunas certas e uma aba com os valores válidos de cargo e setor.
2. Envia o arquivo **em uma única requisição**. O servidor valida **todas** as linhas sem gravar nada: campos obrigatórios, formato de e-mail, e-mail duplicado no arquivo ou já cadastrado, e existência de cargo e setor (o gestor vem do setor).
3. A tela mostra uma **prévia** com os erros por linha e coluna.
4. Regra **tudo ou nada**: nada é gravado até o arquivo estar sem erros. O RH corrige a planilha e envia de novo.
5. Ao confirmar, o servidor grava em lotes, limpando o rastreamento do EF entre os lotes.
6. O resultado fica registrado (quem importou, quando, totais, erros) e entra na auditoria.

**Decisões:**
- A importação **só cadastra**. Atualizar por planilha está fora do escopo.
- O processamento é **síncrono, com limite por arquivo** (proposta: 500 linhas).
- Ninguém recebe senha: o usuário nasce sem senha e recebe um **convite por e-mail**.
- O arquivo enviado é apagado depois de confirmado ou expirado.
- Só o perfil RH acessa a importação.
- A biblioteca de leitura de Excel (licença livre, por exemplo o ClosedXML) é confirmada na implementação.
- A planilha espelha o cadastro mínimo do Figma (seção 13.3) e repete as ações "depois de salvar" (convite, formulário de admissão, onboarding).
- O Figma não tem essa tela; os estados (escolher arquivo, prévia, resultado) são descritos quando formos às telas.

## 7. Ambientes, deploy e testes

- **Local:** `docker compose` com MySQL, captador de e-mails e Azurite (emulador do Blob).
- **Azure:** um único ambiente na nuvem, num único grupo de recursos. A região depende da conta (algumas contas de estudante limitam regiões; **inferido, não verificado**).
- **Pipeline (GitHub Actions):** a cada push ou PR roda build, testes e verificação de formatação, no back-end e no front-end. O deploy no Azure é **manual ou por tag**.
- **Scripts SQL** são aplicados na **inicialização da API**, controlados por configuração, pelo executor da seção 4, com uma trava no banco para que duas instâncias não apliquem ao mesmo tempo. O usuário do banco da API precisa de permissão para criar e alterar tabelas e triggers. **No Azure, criar triggers pode exigir um parâmetro do servidor ou o privilégio SUPER quando o log binário está ligado (inferido, não verificado; conferir na documentação antes do deploy da fatia 0).**
- **Agendador e plano do App Service:** o serviço em segundo plano só roda enquanto a API está de pé. Isso exige o recurso Always On, que eu **inferi** não existir no plano gratuito (**não verificado**). O plano escolhido define o custo e é conferido antes do deploy da fatia 0.
- **Segredos** (connection string, chave do JWT, chaves do Blob) ficam nas configurações do App Service e nos secrets do GitHub. O Key Vault fica como melhoria futura.
- **Rede:** o firewall do MySQL libera só o App Service e, quando preciso, o IP do desenvolvedor.
- **Observabilidade:** endpoint de saúde e logs do App Service. O Application Insights é opcional.
- **Dados de demonstração:** um comando de seed cria dados **fictícios**, atrás de configuração. Nunca entra dado real.
- **Testes:** unidade (Domain e Application); integração com MySQL real (aplicando os mesmos scripts de `Banco de Dados/Scripts/`), incluindo a matriz de perfis e a comparação entre o mapeamento e o banco; testes de componente no front; ponta a ponta só para 2 ou 3 fluxos da demo (login, bater ponto, importar planilha).
- **Quando criar o ambiente Azure:** ao fim da fatia 0, depois de o dono confirmar qual conta tem os créditos, com novo deploy ao fim de cada fatia. Recursos que gastam crédito são parados ou apagados entre as sessões, e o custo de cada recurso é dito antes de criá-lo.

## 8. Fatias e ordem

A ordem segue as dependências. Se o escopo precisar encolher, o corte é por fatia inteira, de trás para a frente.

| # | Fatia | Depende de |
|---|---|---|
| 0 | **Esqueleto:** solution em camadas, `docker compose`, scripts SQL versionados com o executor, teste de fumaça do provider, login, shell do React, CI, deploy no Azure (a base de scripts congela neste deploy) | |
| 1 | **Colaboradores e base transversal:** escopo por perfil (linhas e campos), auditoria, e-mail com fila e agendador, convite, redefinição de senha, cadastros de apoio (cargos, setores, unidades), cadastro com os campos do Figma, ficha, documentos do colaborador, edição, inativação | 0 |
| 2 | **Importação por Excel** | 1 |
| 3 | **Ponto:** quatro marcações por dia, espelho, ajustes em duas etapas (gestor e RH), atestado, feriados (nacionais e estaduais automáticos, municipais cadastrados), banco de horas com consolidação diária e as telas de ponto da equipe do gestor | 1 |
| 4 | **Comunicação e documentos:** avisos (simples, detalhados, agendados, com leitura), aniversariantes, holerites (tipos e agendamento), advertências (ciência e contestação) | 1 |
| 5 | **Formulários e quiz:** entrevista com link público, admissão, avaliações por ciclo, pesquisa, sugestões, quiz, assistente de 4 etapas e pendentes | 1 |
| 6 | **TAD:** treinamentos com módulos e progresso, inscrições, quiz final e certificado em PDF com código de validação | 5 (usa o quiz) |
| 7 | **Gestão:** férias em duas etapas, benefícios (pedidos e dependentes), painéis do RH e do gestor, calendário do RH ("próximos dias"), próximo pagamento | 3, 4 |
| 8 | **Fechamento:** DER/MER atualizado a partir dos scripts, scripts revisados com o grupo, seed completo da demo, testes de ponta a ponta, ensaio da banca | todas |

**Próximo pagamento (fatia 7).** O RH cadastra uma **regra padrão** (dia do mês, por exemplo "todo dia 30") e pode lançar **exceções de data para um mês específico**, que valem mais que a regra. A regra é única para a empresa toda. Se o mês não tem o dia, vale o último dia do mês. Se a data cair em fim de semana ou feriado, o pagamento **antecipa para o dia útil anterior**. As duas últimas regras são padrões propostos, a confirmar na revisão deste spec.

**Fonte dos feriados nacionais (fatia 3).** A escolha entre calcular por regra no código (incluindo os móveis, derivados da Páscoa) e usar uma biblioteca ou API é feita na fatia 3, com a licença conferida.

## 9. Fora do escopo (trabalhos futuros)

Cálculo de folha de pagamento (o RH sobe o PDF do holerite), integração com relógio de ponto físico (REP ou biometria; o dono confirmou que não haverá equipamento físico, o ponto é só pelo sistema), app mobile e layout para celular (o Figma só tem desktop), integração direta com sistema de folha (o "exportar para a folha" gera só um arquivo), login corporativo (SSO), várias empresas no mesmo sistema, atualização de colaboradores por planilha.

**Limitação documentada:** o horário da batida de ponto vem sempre do servidor, nunca do navegador. A conformidade legal completa do ponto eletrônico não é objetivo deste TCC e fica registrada como limitação.

## 10. Decisões do dono durante o brainstorming

| Decisão | Escolha |
|---|---|
| Propósito | TCC, demo no Azure |
| Prazo | Sem prazo por enquanto |
| Hospedagem | App Service, Static Web Apps, MySQL Flexible Server, Blob Storage |
| Banco | Scripts SQL versionados (database-first): base v2 do grupo, mudanças por scripts novos com `ALTER`, script aplicado nunca é editado; a base congela no primeiro deploy no Azure (decisão de 2026-10-28, que substitui o code-first) |
| Mapeamento | EF Core 9 com Pomelo 9.0.0, sem migrations |
| Repositório | Duas pastas, `back-end/` e `front-end/` |
| Equipe do gestor | O setor de que ele é responsável (regra da aplicação; o esquema do grupo mantém `GESTORES` e `FUNCIONARIOS.ID_GESTOR`) |
| O que o gestor vê | Contato, contrato, jornada, ponto, treinamentos, formulários e advertências da equipe; sem CPF, endereço, holerites e documentos; não edita o cadastro |
| Importação com erro | Tudo ou nada |
| E-mails | Os 6 eventos da seção 5 |
| Ambiente Azure | Ao fim da fatia 0, após confirmar a conta |
| Holerite | O RH sobe o PDF |
| Próximo pagamento | Regra padrão mais exceções |
| Relógio de ponto físico | Fora do escopo; o ponto é só pelo sistema |
| Sessão no navegador | Refresh token no `localStorage` (o usuário não perde o login ao fechar a aba) |
| Usuário inativo | Com a senha certa vê "Seu acesso está desativado. Fale com o RH." (403); com a senha errada, a resposta genérica |
| Documentação da API | Gerada pela própria API; ligada no Azure na demo |
| Níveis de acesso | `RH`, `Funcionario` e `Funcionario Gestor` |
| Unidades (filiais) | Não haverá: a empresa é um lugar só, e o feriado municipal vale para todos (dono, 2026-10-28) |
| Aprovação de férias e de ajuste de ponto | Duas etapas, gestor e depois RH, como no Figma; o grupo acrescenta os status e reescreve os dois triggers (dono, 2026-10-28). O RH poder decidir direto, como o Figma mostra, a confirmar |
| E-mail do aviso | Todo aviso publicado manda e-mail (evento 2 da seção 5); a opção "enviar por e-mail também" do Figma não existe (dono, 2026-10-28) |
| Inativar funcionário | Continua como o banco do grupo faz: desativa usuário e benefícios e cancela férias futuras, sem aviso e sem registro próprio dessas consequências (dono, 2026-10-28) |
| Holerites | Com tipo (mensal, 13º, férias), criado pelo Kauan, que também ajusta a chave única de um holerite por pessoa por mês; sem "novo" ou "visto" (dono, 2026-10-28) |
| Avisos | Sempre para todos (sem público por setor) e sem anexo PDF; fixar no topo e rascunho serão feitos pelo Kauan (dono, 2026-10-28) |
| Triggers do grupo | Por enquanto não se mexe neles (dono, 2026-10-28), salvo a correção do Kauan nos de ponto |
| Horário padrão da empresa | Um só para todos (entrada, saída e tolerância), criado pelo Kauan; o atraso é medido contra ele (dono, 2026-10-28) |
| Advertências | Com ciência e contestação: o funcionário confirma ou contesta com texto, e o RH analisa; status novo `CONTESTADA` (dono, 2026-10-28) |
| Arquivos SQL do grupo | Movidos para `Banco de Dados/Scripts/` (V001 a V003), com autorização do dono em 2026-10-28; o dono numera as versões e avisa o grupo |

## 11. Riscos

1. **Provider MySQL no .NET 10.** Mitigado pelo teste de fumaça; plano B na seção 2. O runtime escolhido também precisa estar disponível no App Service (**não verificado**; se não estiver, usar .NET 9 com EF Core 9).
2. **Primeiro uso do Azure e créditos não confirmados.** Mitigado pelo deploy cedo e pelo controle de custo.
3. **Figma lido só em texto.** Capturas de tela e fluxos de navegação ainda não foram vistos e podem revelar detalhes que o texto não mostra.
4. **Regras de ponto, banco de horas e férias** (jornada, tolerância, hora extra, períodos aquisitivos) pesam em regra de negócio. O dono é consultado no início das fatias 3 e 7.
5. **Escopo grande.** Mitigado por fatias independentes e corte por fatia inteira.
6. **Volume do front-end:** 61 telas, só de desktop. Cada fatia inclui as suas próprias telas.
7. **Agendador e plano do App Service.** Se o Always On não estiver disponível no plano barato, as publicações agendadas e a consolidação do banco de horas podem não rodar. Conferir antes do deploy da fatia 0 (seção 7).
8. **Esquema do grupo ainda em mudança e desalinhamento entre SQL e C#.** Mitigado pelo teste que compara o mapeamento com o banco e pelo congelamento da `V001` e da `V002` no primeiro deploy.
9. **Triggers: executor de scripts e Azure.** O executor trata `DELIMITER` por conta própria e é testado contra o arquivo real de triggers (a ferramenta pronta, DbUp ou Flyway, não teve o tratamento de triggers confirmado). Criar triggers e funções no Azure pode exigir configuração do servidor (não verificado).
10. **Triggers do grupo desalinhados com as tabelas (em correção pelo Kauan).** `TRG_PONTOS_BEFORE_INSERT` e `TRG_PONTOS_BEFORE_UPDATE` usam colunas removidas de `PONTOS` em 2026-10-28. O teste que aplica os scripts reais é a prova, e a Task 4 do plano tem um ramo de decisão se ainda falhar.

## 12. Pendências

- Figma conectado e mapeado em texto (seção 13). Falta ver as capturas de tela das telas principais e os fluxos do protótipo.
- Confirmar qual conta do SENAI tem os créditos (antes do deploy da fatia 0).
- O `pesquisador` foi testado e funcionou. O `revisor-de-plano` ainda não rodou: o plano passa por ele antes de executar.
- Confirmar as regras de pagamento em fim de semana e feriado (seção 8).
- Definir quem aprova os pedidos de ajuste de ponto e de férias de um gestor (o Figma não mostra um gestor acima dele) e se o responsável do setor conta como membro da própria equipe. Decidir na fatia 1.
- Confirmar os eventos de e-mail adicionais da seção 5.
- Combinar com o grupo o fluxo dos scripts SQL (quem escreve cada script, nomes `Vnnn__descricao.sql`, a `V001` sem `DROP`, mover os arquivos atuais para `Banco de Dados/Scripts/`) e confirmar os nomes dos três níveis de acesso. **A base v2 ainda não está 100% pronta:** avisar o grupo de que `NIVEIS_DE_ACESSO`, `FUNCIONARIOS` e `USUARIOS` são usadas já na fatia 0 e que o teste de mapeamento quebra se mudarem sem aviso.
- Confirmar com o grupo se `FUNCIONARIOS.ID_GESTOR` pode diferir do gestor do setor (seção 4).
- Conferir no Azure se criar triggers e funções exige configuração do servidor (seção 7).
- **Conferir a correção do Kauan nos triggers de PONTOS** e que `PONTOS.HORAS_TRABALHADAS` não é mais preenchida por trigger: o grupo decide se mantém a coluna, a sincroniza por trigger nas marcações ou a remove. Sugestão ao grupo: a regra "inativo não registra ponto" hoje só vale ao criar o registro do dia, não ao registrar uma batida.
- Confirmar com o grupo o conteúdo e o uso das 8 funções (por exemplo, se a aplicação deve chamá-las).
- As mensagens dos triggers são técnicas e sem acento; decidir, por fatia, como traduzi-las para o usuário.

## 13. Mapeamento do Figma (alta complexidade)

**Fonte e limites.** Arquivo `bsZnCjHNeNgdGaLzVcaxjL`, página "Alta Complexidade" (`1:2`): 61 telas de 1440 px, só no tema claro. Li os **textos** das 61 telas, extraídos da estrutura do arquivo. **Não vi capturas de tela nem os fluxos de navegação** do protótipo. Nomes, datas e números nas telas são exemplos, não requisitos.

### 13.1 Telas por perfil

- **RH (32):** painel inicial; colaboradores (lista, adicionar, ficha com abas Dados, Ponto, Férias, Holerites, Documentos e Advertências, e modais registrar férias, enviar holerite e adicionar documento); ponto e feriados (com modal de pedidos de ajuste); advertências (e nova); formulários (lista, assistente de 4 etapas, quem respondeu); treinamentos (lista e assistente de 4 etapas); avisos (e novo); cadastros (cargos e modal); log de auditoria.
- **Funcionário (14):** tela inicial; meu ponto; férias (e pedir); holerites; avisos (e aviso aberto); formulários (e respondidos); treinamentos (e concluídos); advertências; benefícios (e aderir).
- **Gestor (15):** tela inicial, meu ponto, férias, holerites e avisos (as mesmas do funcionário); painel da equipe (com ficha e registrar advertência); ponto da equipe (com modal de pedido e espelho da pessoa); formulários, treinamentos e advertências da equipe (e registrar).
- **Não existem no Figma:** importação por Excel (já sabido), login, convite e redefinição de senha, tela de atestado (há só o botão "Enviar atestado"), calendário do RH como tela própria (aparece como "Próximos dias" no painel) e notificações.

### 13.2 Divergências com as seções aprovadas (decididas em 2026-10-21 e incorporadas às seções 1, 4, 5, 6, 7, 8, 9, 10 e 11)

1. **O que o gestor vê.** A seção 1 e o esboço dizem "tudo" da equipe. A ficha do gestor diz: "Só o RH altera dados cadastrais. CPF, endereço, holerites e documentos ficam visíveis apenas para o RH." O gestor vê contato, contrato, jornada, ponto, treinamentos e advertências.
2. **Quem é a equipe do gestor.** A seção 4 usa `GestorId` (subordinados diretos). O Figma define a equipe como o **setor** do gestor ("Equipe Comercial, 8 pessoas... quem está no seu setor"), e no cadastro o gestor é "o gestor do setor escolhido".

**Decisões do dono:** (1) o gestor vê o que o Figma desenha, sem CPF, endereço, holerites e documentos; (2) a equipe do gestor é o setor dele (regra da aplicação). Em 2026-10-28 o esquema do grupo, que mantém `GESTORES` e `FUNCIONARIOS.ID_GESTOR`, foi adotado, e a parte "`GestorId` deixa de existir" caiu.

### 13.3 O que o Figma acrescenta, por fatia

- **Fatia 1 (colaboradores):**
  - Cadastro mínimo: nome, CPF, nascimento, telefone, e-mail corporativo, admissão, tipo de contrato, cargo, setor, gestor, nível de acesso e benefícios. O resto (endereço e outros) o próprio colaborador completa no formulário de admissão.
  - "Depois de salvar": enviar convite, enviar o formulário de admissão e incluir no treinamento de onboarding.
  - Matrícula e tipo de contrato (serão criados pelo Kauan, que também aparece como Kafly21 nos commits), jornada (a carga semanal vem do cargo; o horário é o padrão da empresa) e situação hoje (Ativo, Em admissão, Atestado, Férias). **Sem unidade (filial) e sem período de experiência** (decisões do dono em 2026-10-28): o campo "Unidade", a etiqueta "Experiência" e o aviso de fim de experiência ficam escondidos.
  - Documentos do colaborador: tipos, validade, situação Enviado ou Pendente, "pedir para a pessoa enviar" e lembrete. Só o RH e o próprio colaborador veem.
  - Cadastros de apoio mantidos pelo RH, como cargos com carga semanal.
  - Lista com busca por nome, CPF ou matrícula e filtros por setor e contrato.
- **Fatia 2 (importação):** a planilha deve espelhar o cadastro mínimo e repetir as ações "depois de salvar".
- **Fatia 3 (ponto):**
  - Quatro marcações por dia (entrada, saída para o almoço, volta, saída), espelho por colaborador com total, banco de horas e faltas. **Com horário padrão da empresa** (um só para todos, criado pelo Kauan; decisão do dono em 2026-10-28): o atraso é medido contra esse horário, e o Figma mostra "44 h semanais, das 08:00 às 17:48" e "Atraso de 14 min".
  - Banco de horas "atualizado até ontem", o que indica consolidação diária. **Só lançamentos automáticos** (decisão do dono em 2026-10-28): o botão "Lançar no banco de horas" do Figma fica escondido, e a consolidação é recalculável por dia.
  - Pedido de ajuste com justificativa em **duas etapas: gestor e depois RH**, com e-mail em cada etapa. O RH também pode aprovar direto.
  - Atestado enviado pelo funcionário e validado pelo RH.
  - Feriados nacionais e estaduais **importados automaticamente**. Municipais são cadastrados pelo RH, valem para todos (**sem unidades**) e têm "repetir todo ano"; os estaduais seguem a UF da empresa (configuração).
  - Exportar para a folha, espelho em PDF.
- **Fatia 4 (comunicação e documentos):**
  - Avisos simples (até 280 caracteres) e detalhados (seções, **sem anexo PDF**), **sempre para todos** (sem público por setor), publicação agendada, expiração, e-mail sempre enviado na publicação (decisão de 2026-10-28), fixar no topo e contagem de quem leu. Abrir o aviso o marca como lido.
  - Holerites **com tipo** (mensal, 13º, férias; o Kauan cria) **e sem marca de "novo" ou "visto"** (decisão de 2026-10-28; a etiqueta do Figma fica escondida), PDF de até 5 MB, publicar agora ou agendar para o dia do pagamento, e e-mail ao publicar. A auditoria de exemplo cita publicação em lote para 101 colaboradores.
  - Advertências verbal ou escrita, aplicadas pelo RH ou pelo gestor. O colaborador confirma a ciência ou contesta com texto, e o RH responde por e-mail.
  - Aniversariantes dos próximos 14 dias na tela inicial.
- **Fatia 5 (formulários):**
  - Tipos: entrevista, admissão, avaliação (gestor avalia a equipe e a equipe avalia o gestor, por ciclo), quiz, pesquisa de clima e sugestões.
  - Assistente de 4 etapas com prévia ao vivo e verificação antes de publicar.
  - Perguntas: múltipla escolha, eliminatória, resposta curta e longa, nota dada pelo RH, quiz com pontuação, escala de 1 a 5 e envio de arquivo.
  - Publicação por link público (candidatos que **não são funcionários**), setores ou pessoas, agendada, com prazo e nota mínima.
  - Resultado por e-mail ao RH e convite automático dos aprovados.
- **Fatia 6 (TAD):**
  - Modalidade online, híbrido ou presencial, obrigatório ou opcional, com prazo.
  - Módulos de conteúdo (vídeo, texto, link, PDF, aula presencial) e progresso por módulo.
  - Inscrição em opcionais e em turmas presenciais.
  - Quiz final criado em Formulários, nota mínima e certificado em PDF com código (por exemplo `LGPD-2026-00482`).
  - Incluir quem for admitido até o fim do prazo e lembrete 7 dias antes.
- **Fatia 7 (gestão):**
  - Férias com as regras CLT do protótipo: até 3 partes (uma de pelo menos 14 dias e as outras de pelo menos 5), abono de até 10 dias, adiantamento do 13º, pedido com 30 dias de antecedência, não começar nos 2 dias antes de feriado ou fim de semana, e pagamento até 2 dias antes. Fluxo gestor e depois RH, com opção "sugerir outra data".
  - Benefícios com catálogo, pedidos de adesão ("quero aderir"), dependentes e beneficiários, trajeto do vale-transporte, resposta do RH em até 5 dias úteis.
  - Painel do RH: pendências, presença agora, próximos dias (que é o calendário do RH), treinamentos em andamento e últimas alterações.
  - Painel do gestor: equipe, férias por semana e aprovações.

### 13.4 Diferenças em relação ao script SQL

Referência: versão de 2026-10-28 (25 tabelas, 36 triggers e 8 funções). Os números de linha dos links são da versão de 2026-10-21 e podem ter se deslocado: vale o nome da tabela.

- `ADVERTENCIAS` aceita `PENDENTE`, `APLICADA` e `CANCELADA` ([linha 348](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L348)). O Figma usa Aguardando ciência, Ciente e Contestada, mais o tipo Verbal ou Escrita e o texto da contestação.
- `FORMULARIOS.TIPO_FORMULARIO` aceita `ENTREVISTA`, `ADMISSAO`, `AVALIACAO`, `PESQUISA` e `OUTRO`, sem quiz nem sugestão ([linha 456](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L456)).
- `HOLERITES` não tem tipo nem agendamento ([linhas 310-316](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L310-L316)).
- `BENEFICIOS_FUNCIONARIOS` guarda só o vínculo ativo ([linhas 521-526](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L521-L526)), sem pedido de adesão, dependente ou trajeto.
- `PONTOS` (um registro por funcionário por dia) e `PONTOS_MARCACOES` (várias batidas com data e hora) atendem às quatro marcações do Figma. `PONTOS_MANUTENCOES` tem uma única aprovação, por um gestor, com status `PENDENTE`, `APROVADA` e `REJEITADA` ([linhas 279-288](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L279-L288)), contra as duas etapas (gestor e RH) do Figma.
- `FERIAS` existe, com duração de 5 a 30 dias, abono de até 10 dias e uma aprovação por gestor ([linhas 547-560](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L547-L560)); o Figma pede duas etapas (gestor e RH).
- `FERIADOS` existe, por tipo (`NACIONAL`, `ESTADUAL`, `MUNICIPAL`, `EMPRESA`) com UF e município ([linhas 594-602](../../../Banco%20de%20Dados/RHSystem_Criacao_Tabelas_MySQL.sql#L594-L602)); o Figma fala em "vale para" uma unidade.
- Não existem tabelas para unidades, documentos do colaborador, dependentes, atestados, ciclos de avaliação, módulos de treinamento com progresso, inscrições em turmas, refresh tokens, auditoria, importações e fila de e-mails.

### 13.5 Implicações técnicas (a incorporar nas seções 5, 7 e 11)

- **Tarefas agendadas** passam de "fila de e-mails" para um agendador geral: publicação agendada (holerite, aviso, formulário, treinamento), consolidação diária do banco de horas e lembretes automáticos. Em um App Service, um agendador dentro da API precisa do recurso Always On, que **inferi** não existir no plano gratuito (não verificado). Isso pode afetar o plano e o custo.
- **PDFs gerados:** certificado e espelho de ponto. **Exportações:** CSV do log, lista de treinamentos, respostas de formulários e "exportar para a folha". A biblioteca de PDF e a licença são conferidas na implementação.
- **Formulários públicos** têm respondentes externos, sem login. Exigem proteção contra abuso, limite de tamanho de arquivo e uma política de retenção dos dados dos candidatos (LGPD).
- **Eventos de e-mail adicionais** (ampliam a lista da seção 5): lembretes manuais ("lembrar quem falta"), lembrete automático do formulário de admissão, treinamento publicado e lembrete de prazo, resposta a pedido de benefício, férias registradas pelo RH.
- **Auditoria:** o Figma pede IP e origem, filtros por período, pessoa, área e ação, exportação em CSV, e registro de login, ponto, acesso negado e e-mails automáticos.
- **Front-end:** 61 telas. Só há layout de desktop em 1440 px, e nenhum layout móvel foi desenhado.

### 13.6 Decisões do dono a perguntar no início de cada fatia

- **Fatia 3:** regras de hora extra e banco de horas, formato do "exportar para a folha". **Decidido em 2026-10-28:** a validação da sequência das marcações (ENTRADA e SAIDA alternadas, em ordem) e o cálculo das horas ficam **com a aplicação**, apoiados nas funções do grupo, e não em trigger; `FN_VERIFICAR_CONSISTENCIA_PONTO` e o select de validação do script detectam o que passar.
- **Fatia 4:** envio de holerites em lote (um PDF por colaborador, 101 de cada vez, seria inviável).
- **Fatia 5:** anonimato das avaliações e da pesquisa de clima, e como a "verificação de dado sensível" funciona.
- **Fatia 6:** onde ficam os vídeos (arquivo no Blob ou link externo).
- **Fatia 7:** confirmar as regras de férias do protótipo.
