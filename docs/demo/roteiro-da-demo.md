# Roteiro da demo (banca do TCC)

Uma proposta de roteiro de **cerca de 15 minutos**, com o que dizer, onde clicar e o que deve acontecer. Os fluxos escolhidos são os do roadmap (login, bater ponto, importar planilha, pedir e aprovar férias) mais os que mostram o que o sistema tem de diferente: holerite, atestado, treinamento com certificado e o formulário de admissão. **Quais fluxos entram e o que mostrar como trabalhos futuros é decisão do dono:** corte o que não couber; cada passo é independente dos outros.

**Nomes de menu e de botão.** Os rótulos abaixo seguem o desenho das telas e podem diferir um pouco do front pronto: confira no primeiro ensaio e ajuste o texto deste arquivo.

**O ensaio é automático.** Quase todo passo abaixo tem um teste em `back-end/tests/RHSystem.Api.IntegrationTests/Demo/` (`FluxosDaDemoTests` e `SeedDaDemoTests`) que faz o mesmo pela API, sobre o banco que o seed monta, com o relógio fixo em quarta-feira, 09:42 (por isso as datas dos testes são fixas e as do roteiro, não). O que não tem teste é o que só existe na tela (os botões, a prévia da planilha linha a linha) e a avaliação de desempenho do gestor, de que o teste só confere que o ciclo aparece no painel. Se uma rota ou o seed mudar e o roteiro deixar de funcionar, o teste do passo cai. Rode antes de cada ensaio presencial:

```
dotnet test back-end/tests/RHSystem.Api.IntegrationTests --filter "FullyQualifiedName~Demo."
```

## Preparação (véspera e uma hora antes)

1. **Banco novo e seed:** `docker compose down -v`, `docker compose up -d --wait`, suba a API (ela aplica os scripts `V001` a `V005`) e rode `Banco de Dados/Demo/seed_demo.sql` (`Banco de Dados/Demo/LEIA-ME.md`). Faça isto **no dia da apresentação**: as datas do seed saem de "hoje", então a demo nunca fica velha, e as férias, o ponto e os prazos ficam coerentes com o dia.
2. **Rode o seed antes das 09:30 ou depois?** (só vale no dia de hoje do servidor, sem `@hoje` no passado e em dia útil) Depois das 09:30, seis pessoas já aparecem com a entrada batida de hoje ("quem está trabalhando agora" no painel do RH). Antes, ninguém: o passo 1 mostra a batida ao vivo. Escolha o que prefere mostrar.
3. **E-mail:** com o envio desligado (`Email:EnvioAtivo=false`, o padrão do `appsettings.json`) os e-mails ficam na fila e nada sai; em desenvolvimento o envio está ligado e vai para o Mailpit (`http://localhost:8025`), que mostra o que sairia. Se for ligar o envio de verdade, leia a seção "E-mails que não geram devolução" do `LEIA-ME.md` do seed.
4. **Quatro janelas de navegador (ou perfis) já logadas**, uma por pessoa: `helena.martins` (RH), `rafael.costa` (gestor), `lucas.pereira` e `bruno.azevedo` (funcionários). Senha de todos: `Demo#RH2026` (domínio `rhsystem-demo.example`, ou o que você passou ao seed).
5. **Tela de 1280 px ou mais:** o front foi desenhado só para desktop.
6. **Arquivos prontos** em uma pasta: um PDF qualquer para o holerite e outro para o atestado, e a planilha de importação (baixe o modelo em *Colaboradores → Importar* e preencha duas linhas como as do passo 6, abaixo).
7. **Plano B:** um vídeo curto ou prints de cada passo, caso a rede ou o banco falhem.

## Os passos

### 1. Entrar e bater o ponto (funcionário) — 2 min
*Teste:* `Passo_1_o_funcionario_entra_...`
- Entre como **Bruno Azevedo** (`bruno.azevedo`). A tela inicial mostra o cartão "Seu ponto hoje", as férias, os avisos e os treinamentos dele.
- Registre a **entrada** pelo cartão. O cartão passa a mostrar a batida, com a hora em horário de Brasília.
- *O que dizer:* a batida é pelo navegador, sem relógio físico (veja `limitacoes.md`); o sistema cuida de intervalo, jornada e banco de horas.
- Abra **Meu ponto** (o espelho): os dias anteriores já têm as quatro batidas e as horas somadas.

### 2. Pedir férias e aprovar em duas etapas — 3 min
*Teste:* `Passo_2_a_pessoa_pede_ferias_...`
- Como **Diego Farias**, abra **Minhas férias → Pedir férias** com **15 dias seguidos que comecem numa segunda-feira daqui a pelo menos 35 dias** e longe de feriado (use datas de hoje, não as do teste: a regra de 30 dias de antecedência recusa data próxima, e o seed sai do dia em que você o rodou). O sistema **confere as regras antes de enviar** (duração, saldo, parte de 14 dias, antecedência de 30 dias, feriado e descanso, sobreposição) e mostra cada uma como atendida ou não.
- Envie. Como **Rafael Costa** (gestor), abra o **Painel da equipe**: o pedido do Diego (e o do Lucas, que já estava lá) em "Esperando sua aprovação". **Aprove** o do Diego.
- Como **Helena Martins** (RH), as **Pendências**: o pedido aparece na etapa do RH, junto com o da Camila. **Confirme**.
- Volte ao Diego: o pedido está **Aprovado**. *O que dizer:* um e-mail por decisão (visto no Mailpit), nenhum para criar nem para o registro do RH.

### 3. Painel do RH e pendências — 2 min
*Testes:* `O_painel_do_RH_mostra_as_pendencias_da_demo` (em `SeedDaDemoTests`) e `Passo_3_o_RH_aprova_o_pedido_de_beneficio...`
- Como **Helena**, abra o **Painel**: cartões de pendência (ajustes de ponto, férias, atestados, admissão e benefícios), próximos dias (feriados e aniversários), treinamentos em andamento, quem está trabalhando agora e as últimas alterações.
- Clique em **Benefícios**: o plano odontológico do Diego e a troca de trajeto da Vanessa. **Aprove** um e mostre que o outro continua na fila.
- Clique em **Admissão**: a Júlia começa em 5 dias e ainda não enviou o formulário.

### 4. Holerite (RH envia, pessoa recebe) — 2 min
*Teste:* `Passo_4_o_RH_envia_o_holerite_...`
- Como **Helena**, abra a ficha do **Lucas → Holerites → Enviar**: a competência do mês anterior, tipo mensal, o PDF da pasta. (O seed **não** traz holerites de propósito: o arquivo precisa existir no armazenamento.)
- Como **Lucas**, **Meus holerites**: o novo aparece e o download entrega um link temporário.

### 5. Atestado — 1 min
*Teste:* `Passo_5_a_pessoa_envia_o_atestado_...`
- Como **Carla Mendes**, **Ponto → Atestados → Enviar** (os dois últimos dias úteis, o PDF). Como **Helena**, o cartão de atestados no painel; **validar**.

### 6. Importar a planilha — 2 min
*Teste:* `Passo_6_o_RH_importa_a_planilha_...`
- Como **Helena**, **Colaboradores → Importar por planilha**: envie a planilha com duas linhas como estas (cargo, setor e nível já existem no seed; CPFs de teste conhecidos; admissão depois de hoje):

  | Nome | CPF | Nascimento | Telefone | E-mail | Admissão | Contrato | Cargo | Setor | Nível | Benefícios |
  |---|---|---|---|---|---|---|---|---|---|---|
  | Marcos Oliveira | 529.982.247-25 | 12/05/1993 | (11) 90000-0101 | marcos.oliveira@rhsystem-demo.example | *uma data futura* | CLT | Desenvolvedor | Tecnologia | Colaborador | Plano de saúde |
  | Renata Souza | 111.444.777-35 | 03/09/1995 | (11) 90000-0102 | renata.souza@rhsystem-demo.example | *uma data futura* | CLT | Vendedor | Comercial | Colaborador | |

  A **prévia** mostra linha a linha o que vai ser criado e os erros (mostre uma linha com CPF inválido para ver a mensagem). **Confirme**: as pessoas são cadastradas com matrícula gerada, e o convite fica na fila.

### 7. Treinamento e certificado — 2 min
*Teste:* `Passo_7_a_pessoa_conclui_o_treinamento_...`
- Como **Lucas**, **Meus treinamentos → Segurança da informação e LGPD**: faltam dois módulos; conclua. Ao concluir o último, o **certificado** é emitido.
- *Conferência pública:* abra `/api/publico/certificados/<código>` (o da Camila: `SELECT CODIGO_VALIDACAO FROM TREINAMENTOS_CERTIFICADOS`) **sem login**: só nome, treinamento e data.

### 8. Formulário de admissão e avaliação — 1 min
*Teste:* `Passo_8_quem_vai_comecar_...`
- Entre como **Júlia Andrade**: o **formulário de admissão** espera. Responda; volte ao painel da Helena e mostre que a pendência de admissão sumiu.
- Como **Rafael**, a **Avaliação de desempenho, ciclo 2026.2** espera a avaliação da equipe.

### 9. Auditoria — 30 s
*Teste:* `Passo_9_o_RH_ve_o_log_...`
- Como **Helena**, **Auditoria**: tudo o que foi feito nos passos acima aparece com quem, quando e o quê (sem dado sensível: CPF, dependente e motivo de recusa nunca vão para o log).

## O que mostrar como trabalhos futuros (sugestão para o dono decidir)

Folha de pagamento integrada (hoje há exportação do ponto para a folha), aplicativo móvel e relógio de ponto físico ou por geolocalização, assinatura digital de holerite e de advertência, avaliação 360 com relatório comparativo, vídeos de treinamento enviados ao sistema (hoje só por link) e quiz de treinamento (cortado em 2026-10-29), e mais de uma empresa no mesmo banco (hoje é uma só).

## Se algo falhar na hora

| Sintoma | O que fazer |
|---|---|
| Login recusado | Confira o domínio do e-mail (o do seed) e a senha `Demo#RH2026`; se o banco foi recriado sem o seed, rode o seed de novo. |
| "Quem está trabalhando agora" vazio | O seed rodou antes das 09:30 ou o dia é fim de semana: bata o ponto ao vivo (passo 1). |
| E-mail não aparece | Em desenvolvimento ele vai para o Mailpit (`http://localhost:8025`); com `Email:EnvioAtivo=false` fica só na fila `FILA_EMAILS`. |
| Download de arquivo falha | O armazenamento (Blob ou Azurite) não está no ar: suba o Azurite do compose ou use o plano B. |
| Pedido de férias recusado pelas regras | Escolha um início com mais de 30 dias de antecedência e que não caia em feriado, sábado, domingo nem nos 2 dias antes de feriado ou sábado. |
