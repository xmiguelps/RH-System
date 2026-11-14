# Seed fictício da demo

`seed_demo.sql` enche um banco **novo** com uma empresa inventada: uma pessoa de RH, dois gestores, oito funcionários e uma pessoa que ainda vai começar, com ponto dos últimos dois meses, férias em quatro situações, benefícios (com um dependente e um trajeto), avisos, três formulários, dois treinamentos (um com certificado emitido), documentos pedidos, uma advertência e a data de pagamento do mês seguinte. **Tudo é inventado**: nomes, CPFs (dígitos verificadores válidos, para passar na validação, mas sem dono conhecido: por acaso um deles pode coincidir com o de uma pessoa real), telefones (`119000000xx`, fora de uso) e e-mails. Nenhum dado de pessoa real foi usado; **não ponha dado real nesta demo**.

Isto **não é um script de versão**: fica fora de `Banco de Dados/Scripts` de propósito, e a API não o aplica sozinha. O teste `SeedDaDemoTests` roda o arquivo inteiro contra um MySQL novo, entra com o login de verdade e confere os painéis, então o que está aqui funciona com a API que está no repositório.

## Como rodar

1. Suba o banco e a API uma vez, para ela aplicar os scripts `V001` a `V005` (veja o `README.md` da raiz). O banco não pode ter nenhum colaborador ainda.
2. Rode o arquivo **numa sessão só** (ele usa variáveis de usuário), com o `mysql` do cliente:

```
mysql --default-character-set=utf8mb4 -h <servidor> -u <usuario> -p <banco> < "Banco de Dados/Demo/seed_demo.sql"
```

No compose local: `docker compose exec -T mysql mysql --default-character-set=utf8mb4 -u root -p<senha do .env> rh_system < "Banco de Dados/Demo/seed_demo.sql"`.

**No PowerShell o operador `<` não existe** (dá erro de operador reservado), e a saída de `Get-Content | ...` no Windows PowerShell 5.1 troca a codificação e **estraga os acentos**. Use o Git Bash (o comando acima funciona como está) ou chame o `cmd`, que repassa os bytes sem mexer:

```
cmd /c 'docker compose exec -T mysql mysql --default-character-set=utf8mb4 -u root -p<senha do .env> rh_system < "Banco de Dados\Demo\seed_demo.sql"'
```

3. Entre na API com `helena.martins@rhsystem-demo.example` (RH) e a senha `Demo#RH2026`.

A segunda rodada **falha logo no primeiro comando** (a configuração da empresa já existe), sem duplicar nada. Para recomeçar: `docker compose down -v` e suba tudo de novo, ou use um banco novo.

## Variáveis (opcionais, definidas na mesma sessão antes do arquivo)

| Variável | Para quê | Padrão |
|---|---|---|
| `@hoje` | o dia "de hoje" da demo: todas as datas do seed saem dele (o ponto dos 14 dias antes, as férias, os avisos, o prazo dos formulários) | `CURDATE()` |
| `@dominio` | o domínio dos e-mails fictícios | `rhsystem-demo.example` |

`@hoje` **não pode ser depois do dia de hoje do servidor**: o primeiro comando do script confere isso e falha com `CHK_SEQUENCIAS_ULTIMO_VALOR` (a mensagem é de uma guarda, nada foi gravado). Serve para fixar um dia no passado (os testes usam `2026-10-07`); com `@hoje` no passado o seed **não** cria o ponto "de hoje". Ao mudar `@dominio`, os e-mails de login mudam junto.

## Quem entra

Todos usam a mesma senha de demonstração, **`Demo#RH2026`** (o hash no arquivo é o do `SenhaHasher` da API). **Troque a senha antes de abrir a demo numa URL pública.**

| Login (antes do `@dominio`) | Papel | O que a demo mostra com essa pessoa |
|---|---|---|
| `helena.martins` | RH | painel com pendências (férias da Camila, benefícios, admissão da Júlia), ficha, ponto, avisos, formulários, treinamentos |
| `rafael.costa` | gestor de Tecnologia | painel da equipe com o pedido de férias do Lucas esperando, avaliação da equipe |
| `beatriz.nogueira` | gestora do Comercial | equipe do Comercial (a Vanessa está de férias hoje) |
| `lucas.pereira` | funcionário | meu ponto, minhas férias (um pedido esperando o gestor), plano de saúde com dependente, treinamento em andamento |
| `camila.duarte` | funcionária | treinamento concluído e certificado; férias esperando o RH |
| `julia.andrade` | funcionária, começa em 5 dias | formulário de admissão e treinamento de integração esperando |

Os outros: `diego.farias` (atrasos e advertência pendente, pediu o plano odontológico), `thiago.mello` (uma falta, férias já tiradas), `vanessa.lima` (de férias hoje; troca de trajeto pendente), `patricia.gomes` e `carla.mendes`, `bruno.azevedo` (documentos pedidos pelo RH).

## O que o seed não cria, de propósito

- **Holerites, atestados e documentos enviados**: todos apontam para um **arquivo** no Blob, e um caminho que não existe lá quebra o download. O roteiro da demo envia esses **ao vivo**, pela própria API (o teste `FluxosDaDemoTests` faz o mesmo).
- **Eventos de auditoria**: o log nasce das ações feitas na demo.
- **Lançamentos do banco de horas.** O domínio só lança banco de horas pelo cálculo automático: o agendador consolida cada dia a partir das batidas (a falta do Thiago vira débito; saída depois do horário vira crédito) e recupera os últimos 60 dias a cada ciclo. Com a API no ar, o saldo aparece **alguns minutos depois** do seed; um lançamento gravado pelo seed seria apagado ou reescrito no primeiro recálculo.
- **O ponto de hoje** só nasce se `@hoje` é o dia de hoje do servidor, não é fim de semana e o script roda depois das 09:30 (seis pessoas já com a entrada batida); senão a demo começa sem ninguém trabalhando, e o roteiro manda bater o ponto ao vivo.
- **Ponto dos últimos dois meses:** o seed grava os dias úteis dos 62 dias anteriores a `@hoje` (o mês atual e o anterior inteiros), pulando os feriados da tabela `FERIADOS` (o `V001` traz os de 2026; em outro ano os feriados que só existem no código não são pulados). Um mês mais antigo que isso aparece como falta no espelho.

## E-mails que não geram devolução

O envio de e-mail da API só acontece com `Email:EnvioAtivo=true`; desligado (o padrão do `appsettings.json`), a fila apenas acumula e **nada sai**. Em desenvolvimento (`appsettings.Development.json`) o envio está **ligado** e aponta para o **Mailpit** do compose, que captura tudo (`http://localhost:8025`): nada vai para a internet.

O seed **não enfileira e-mail**: os avisos entram publicados há 8 a 10 dias, fora da janela de 7 dias em que a recuperação do agendador reenfileira o e-mail de aviso. Se a demo for hospedada com o envio **ligado**, o e-mail de uma ação feita na demo (convite da importação, decisão de férias) para um domínio que não existe é devolvido pelo servidor de destino. Escolha uma das três saídas **antes** de ligar o envio:

1. **Deixe o envio desligado** (o roteiro funciona; só os e-mails não saem). É o padrão.
2. **Rode o seed com `@dominio` de um domínio seu** que aceite e descarte e-mail (um *catch-all*), assim nada é devolvido.
3. **Ponha um endereço seu no cadastro** das poucas pessoas que o roteiro usa (o RH edita a ficha) e deixe os demais fictícios.

O domínio padrão termina em `.example` (reservado pela RFC 2606): **não existe de verdade** e nunca vai ser de ninguém, mas por isso mesmo um envio real para ele é devolvido.
