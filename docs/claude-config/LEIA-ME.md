# Configuração do Claude Code para este projeto

Cópia, feita em 2026-10-28, da configuração pessoal do Miguel que mora na pasta de usuário do Claude Code (`C:\Users\<usuario>\.claude\` no Windows). Serve para recriar o mesmo método de trabalho em outro PC.

## O que copiar e para onde

| Arquivo aqui | Destino no outro PC |
|---|---|
| `CLAUDE.md` | `C:\Users\<usuario>\.claude\CLAUDE.md` (se já existir lá, **acrescente** o conteúdo ao final, sem apagar o que existe) |
| `agents\pesquisador.md` | `C:\Users\<usuario>\.claude\agents\pesquisador.md` |
| `agents\revisor-de-plano.md` | `C:\Users\<usuario>\.claude\agents\revisor-de-plano.md` |

Os agentes só são carregados quando o Claude Code inicia: copie os arquivos **antes** de abrir o Claude Code.

## Configuração de modelo (`settings.json`)

Não copiei o `settings.json` inteiro. Em `C:\Users\<usuario>\.claude\settings.json`, mescle estas chaves **sem apagar as que já existirem**:

```json
{
  "model": "sonnet",
  "env": { "ANTHROPIC_DEFAULT_SONNET_MODEL": "claude-sonnet-5-5" },
  "effortLevel": "xhigh"
}
```

## O que o método faz

A sessão principal roda no Sonnet 5.5. A busca ampla vai para o agente `pesquisador` (Haiku 5.5) e a revisão de plano vai para o `revisor-de-plano` (Opus 5.5). Os dois só leem; quem edita é a sessão principal. As regras completas estão no `CLAUDE.md`.

Se um dos modelos não estiver disponível na conta, o Claude deve parar e avisar, sem trocar em silêncio por outro.
