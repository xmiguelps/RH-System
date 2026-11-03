## Não relatar inferência com a confiança de medição

Antes de escrever "medido" ou "confirmado", liste quais hipóteses o resultado
admite. Se admitir duas, ou desenhe a sonda que as separa, ou diga as duas em
voz alta — uma conclusão errada com aparência de fato é pior que uma dúvida
declarada, porque outras sessões passam a citá-la.

Quando eu empurrar de volta com conhecimento de negócio contra uma conclusão
sua ("não é possível, temos mais de 300 clientes"), trate como sinal de que
falta um teste, não como pedido de reexplicação.

Caso que originou isto: uma sonda recusava um subdomínio inventado e aceitava
dois reais — compatível com "lista literal" E com "padrão + registro"; escolhi
uma e apresentei como medida.

## Modelos e delegação: Sonnet 5.5 no centro, Haiku 5.5 busca, Opus 5.5 revisa o plano

A sessão principal roda no Sonnet 5.5 (já é o padrão do `settings.json`) e não troca de modelo sozinha. Duas
tarefas saem dela para agentes de leitura, definidos em `~/.claude/agents/`, para o contexto central durar mais:

- **Busca ampla vai para o agente `pesquisador` (Haiku 5.5)**, e não para uma fila de greps e leituras na sessão
  principal. Busca ampla é a que exige varrer vários arquivos ou pastas: "onde isso é usado", "como o projeto faz X",
  "o que toca em Y". **Busca pontual é direta:** arquivo ou nome exato já conhecido, uma ou duas leituras. Abrir
  agente para isso custa mais do que ler. Perguntas independentes podem ir a vários pesquisadores em paralelo.
- **Plano passa pelo agente `revisor-de-plano` (Opus 5.5) antes de executar** quando tem mais de uma fatia, ou
  mexe em contrato, banco, autenticação, pipeline ou em arquivo que outra sessão também toca. Ele confere o plano
  contra o código e aponta contradições, lacunas e riscos. **Ele não decide.** O que ele levantar e mudar o que o
  usuário vê vai para mim; o que for de engenharia a sessão resolve e me diz depois.

**O que o pesquisador devolve é achado, e não veredito.** Ele localiza bem e conclui pior. Por isso:

- Antes de agir sobre um achado que decide algo (apagar, mudar contrato, afirmar que algo "não existe"), abra o
  arquivo na sessão principal e confira a linha.
- Ausência só vale com o que foi varrido listado e com um controle positivo (o mesmo método acha um caso que se sabe
  que existe). Sem isso, diga "o pesquisador não achou", nunca "não existe".
- Ao me contar, diga de onde veio: "o pesquisador achou X" ou "li X em `arquivo:linha`". Achado delegado e não
  conferido não se escreve como medido (regra da seção acima).

Os dois agentes só leem: quem edita é a sessão principal.
