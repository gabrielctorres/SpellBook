# Documentação interna do SpellBook

`Documentation~/` segue a convenção UPM. A Unity ignora a pasta: ela não gera `.meta` e não entra no pacote importado.

Os agentes do time leem estes arquivos em vez de depender de memória ou de conversa. Nenhuma linha de código é escrita sem que antes exista registro de três coisas: o que se quer (PRD), como fazer (Spec) e as regras que valem (ADR e DS).

| Documento | Onde | Dono | Quando |
|---|---|---|---|
| Backlog | [backlog.md](backlog.md) | Controlador | Porta de entrada de toda ideia, bug ou pendência |
| Visão do pacote | `gdd/` | Criativo | Regras e visão dos sistemas, com selos implementado/parcial/proposta |
| PRD | `prd/<slug>.md` | Criativo (`/prd-feature`) | O quê e por quê, na visão de quem usa o pacote; CAs numerados; seção 9 é o diário (`/ajustar`) |
| Spec | `spec/<slug>.md` | Arquiteto | Como construir: pesquisa, ADRs respeitadas, desenho, testes red, riscos, tickets (≤ ~400 linhas, ≤ 10 arquivos) |
| ADR | `adr/NNNN-titulo.md` (índice em `adr/README.md`) | Arquiteto | Decisões que valem para o pacote inteiro. Uma ADR aceita não é reescrita: ganha emenda datada ou é substituída |
| DS | `ds/` | Criativo + Arquiteto | Regras do Inspector, da UI e de nomenclatura |
| Manual público | `manual/` | Writer / Arquiteto | Site MkDocs (GitHub Pages), no estilo TopDown Engine |
| Planos e estudos | `planos/` | Arquiteto | Estudos de origem, como o [gas-estudo-e-roadmap.md](planos/gas-estudo-e-roadmap.md) |
| Time | [TIME.md](TIME.md) | Controlador | Papéis e fluxo |

## Fluxo

1. A ideia nasce no backlog.
2. Vira um PRD.
3. O Arquiteto escreve a Spec, apoiada nas ADRs e no DS.
4. O usuário aprova PRD, Spec e ADRs novas num **único gate**.
5. O Controlador cria uma issue por ticket no repositório do SpellBook. Cada issue tem links para a seção do PRD, o ticket da Spec, as ADRs e o DS.

## Projeto host

O pacote é testado no projeto Unity em `HostProject~/` (ADR 0004). A raiz do repositório é a pasta do pacote e não pode ter `Assets/`, `Library/`, `Packages/` nem `ProjectSettings/`: tudo o que estiver ali entra como conteúdo do `com.spellbook`.

- Versão: a de `HostProject~/ProjectSettings/ProjectVersion.txt` (6000.3.13f1, decisão D5 da Spec da modularização).
- `HostProject~/Packages/manifest.json` traz o pacote por `"com.spellbook": "file:../.."` e em `testables`. O caminho relativo parte de `HostProject~/Packages/`, por isso são dois níveis.
- `HostProject~/Assets/` vem versionada com um `.gitkeep`, então já existe num clone novo. Não apague o `.gitkeep`: a Unity não abre o projeto sem `Assets/`.

Para abrir:

1. Confira se `.claude/editor.lock` existe. Se existir, não abra: outro papel está usando a Unity (ADR 0005).
2. Crie a trava com uma linha `papel; ticket; horário ISO-8601; caminho do host`.
3. Abra `HostProject~/` pelo Unity Hub (Add project from disk) ou rode a Unity em batchmode com `-projectPath HostProject~`.
4. Ao terminar, feche a Unity e apague a trava que você criou, também se algo falhar.

Em caso de conflito com o `CLAUDE.md`, vale a ADR.
