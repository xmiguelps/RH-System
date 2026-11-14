# Custos e como desligar o ambiente da demo

**Este arquivo diz o que cada peça é, o que cobra mesmo parada e como desligar. Não traz preços.** Preço de nuvem muda e depende da região, do nível e da conta; calcule na **Calculadora de preços do Azure** com os níveis que você escolheu, e confira a **Cost Management** da assinatura depois de um dia de uso. Antes de criar qualquer recurso, **configure um orçamento com alerta** (Cost Management → Budgets) no valor que você aceita gastar: é a única proteção contra surpresa.

> O deploy no Azure ainda **não foi feito** (é a Task 14 da fatia 0, um passo guiado do dono). O que está abaixo é a lista dos recursos que o sistema precisa, escrita a partir do código e do plano; quando o dono criar o ambiente, confira cada nome.

## O que o sistema precisa

| Peça | Para quê | Cobra parada? | Como parar |
|---|---|---|---|
| **App Service** (nível pago, com **Always On**) | roda a API e o agendador em segundo plano. Sem Always On o app dorme e **nada agendado roda** | **Sim**, enquanto o plano existir, mesmo sem acesso | **parar o app não basta**: o plano (App Service Plan) continua cobrando. Apague o plano ou reduza para o nível gratuito |
| **MySQL Flexible Server** | o banco (MySQL 8.4) | **Sim**: o armazenamento e o backup continuam; a computação só deixa de cobrar com o servidor **parado** (o Azure religa sozinho depois de alguns dias parado) | `az mysql flexible-server stop` para a pausa curta; `delete` para acabar |
| **Storage Account** (Blob) | arquivos: holerite, atestado, documentos, certificado, PDF de módulo | **Sim**, pelo que está guardado | apague o container ou a conta |
| **Envio de e-mail** (provedor configurado em `Email:*`) | e-mails da lista fechada | só quando envia | deixe `Email:EnvioAtivo=false` e nada é enviado nem cobrado |
| **Front-end** (arquivos estáticos) | as telas | conforme onde for hospedado | apague junto com o grupo de recursos |

Tudo no **mesmo grupo de recursos** facilita: um comando apaga tudo.

## Para a apresentação: o mínimo que funciona

- **Em vez de nuvem, rode local.** O `docker-compose.yml` sobe o MySQL 8.4 e o Mailpit; o Azurite (armazenamento de arquivos de desenvolvimento) cobre os arquivos; a API roda com `dotnet run`. **Custo zero** e nada depende de internet. O roteiro foi ensaiado assim (`FluxosDaDemoTests` usa um MySQL em contêiner).
- Se for para a nuvem: crie **no dia anterior**, ensaie, apresente e **apague no mesmo dia** ou no seguinte.
- O agendador exige **Always On** (nível pago do App Service): se o orçamento não permitir, use o nível gratuito e aceite que as tarefas agendadas (concluir férias, consolidar o banco de horas, efetivar desligamentos) só rodam quando alguém abre o sistema e a recuperação refaz o que ficou para trás. Para a demo isso basta.

## Como desligar

Troque `<grupo>`, `<servidor>` e `<plano>` pelos nomes reais. Os comandos são do Azure CLI (`az`).

**Pausar (volta depois):**
```
az mysql flexible-server stop --resource-group <grupo> --name <servidor>
az webapp stop --resource-group <grupo> --name <app>
```
Isto **não zera** o custo: o plano do App Service e o armazenamento do banco e dos arquivos continuam. Serve para uma pausa de poucos dias.

**Zerar o custo de verdade (apaga tudo, sem volta):**
```
az group delete --name <grupo> --yes --no-wait
```
Antes, **tire uma cópia do que quiser guardar**: o seed recria a demo em minutos (`Banco de Dados/Demo/LEIA-ME.md`), então normalmente não há o que guardar.

**Conferir que parou:** depois de 24 horas, abra **Cost Management → Cost analysis**, filtre pelo grupo e veja se o gasto diário caiu a zero. Procure por **IPs públicos e discos** esquecidos fora do grupo.

## Checklist de fim de demo

1. Rodou o roteiro e **ninguém mais precisa** do ambiente?
2. `az group delete` feito (ou o servidor e o app parados, se for voltar em poucos dias)?
3. Alerta de orçamento ligado e o e-mail dele certo?
4. Chaves e senhas do ambiente (JWT, banco, armazenamento) **trocadas ou apagadas**; a senha de demonstração `Demo#RH2026` **nunca** fica numa URL pública depois da banca.
5. O repositório não tem segredo: confira o `.env` (o git o ignora) e os `user-secrets`.
