# Seed fictício da demo

`seed_demo.sql` enche um banco **novo** com uma empresa inventada: uma pessoa de RH, dois gestores, oito funcionários e uma pessoa que ainda vai começar, com ponto dos últimos dias, banco de horas, férias em quatro situações, benefícios (com um dependente e um trajeto), avisos, três formulários, dois treinamentos (um com certificado emitido), documentos pedidos, uma advertência e a data de pagamento do mês seguinte. **Tudo é fictício**: nomes, CPFs (dígitos válidos, de ninguém), telefones e e-mails. Nenhum dado de pessoa real entra aqui.

Isto **não é um script de versão**: fica fora de `Banco de Dados/Scripts` de propósito, e a API não o aplica sozinha. O teste `SeedDaDemoTests` roda o arquivo inteiro contra um MySQL novo, entra com o login de verdade e confere os painéis, então o que está aqui funciona com a API que está no repositório.

## Como rodar

1. Suba o banco e a API uma vez, para ela aplicar os scripts `V001` a `V005` (veja o `README.md` da raiz). O banco não pode ter nenhum colaborador ainda.
2. Rode o arquivo **numa sessão só** (ele usa variáveis de usuário), com o `mysql` do cliente:

```
mysql --default-character-set=utf8mb4 -h <servidor> -u <usuario> -p <banco> < "Banco de Dados/Demo/seed_demo.sql"
```

No compose local: `docker compose exec -T mysql mysql --default-character-set=utf8mb4 -u root -p<senha do .env> rh_system < "Banco de Dados/Demo/seed_demo.sql"`.

3. Entre na API com `helena.martins@rhsystem-demo.example` (RH) e a senha `Demo#RH2026`.

A segunda rodada **falha logo no primeiro comando** (a configuração da empresa já existe), sem duplicar nada. Para recomeçar: `docker compose down -v` e suba tudo de novo, ou use um banco novo.

## Variáveis (opcionais, definidas na mesma sessão antes do arquivo)

| Variável | Para quê | Padrão |
|---|---|---|
| `@hoje` | o dia "de hoje" da demo: todas as datas do seed saem dele (o ponto dos 14 dias antes, as férias, os avisos, o prazo dos formulários) | `CURDATE()` |
| `@dominio` | o domínio dos e-mails fictícios | `rhsystem-demo.example` |

`@hoje` **não pode ser depois do dia de hoje do servidor**: o trigger do banco de horas recusa lançamento com data futura. Serve para fixar um dia no passado (os testes usam `2026-10-07`). Ao mudar `@dominio`, os e-mails de login mudam junto.

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
- **O ponto de hoje** só nasce se o script rodar depois das 09:30 (seis pessoas já com a entrada batida); antes disso a demo começa sem ninguém trabalhando, e o roteiro manda bater o ponto ao vivo.

## E-mails que não geram devolução

O envio de e-mail da API só acontece com `Email:EnvioAtivo=true`; desligado (o padrão), a fila apenas acumula e **nada sai**. Em desenvolvimento, o **Mailpit** do compose captura tudo (`http://localhost:8025`), então nada vai para a internet.

Se a demo for hospedada com o envio **ligado**, e-mail para um domínio que não existe é devolvido pelo servidor de destino. Escolha uma das três saídas **antes** de ligar o envio:

1. **Deixe o envio desligado** (o roteiro funciona; só os e-mails não saem). É o padrão.
2. **Rode o seed com `@dominio` de um domínio seu** que aceite e descarte e-mail (um *catch-all*), assim nada é devolvido.
3. **Ponha um endereço seu no cadastro** das poucas pessoas que o roteiro usa (o RH edita a ficha) e deixe os demais fictícios.

O domínio padrão termina em `.example` (reservado pela RFC 2606): **não existe de verdade** e nunca vai ser de ninguém, mas por isso mesmo um envio real para ele é devolvido.
