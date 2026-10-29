# Time de desenvolvimento

Cada papel é um agente em `.claude/agents/` e pode rodar de dois jeitos:

- **Subagente:** o Controlador (sessão principal) aciona o papel pela Agent tool.
- **Terminal dedicado:** `claude --agent <papel>` abre uma sessão que já é aquele papel.

| Papel | Agente | Função | Não faz |
|---|---|---|---|
| Controlador | sessão principal (regras no `CLAUDE.md`) | Triagem, backlog, fila e WIP. Cria issues (`/issue`), abre e fecha terminais, leva dúvidas e gates ao usuário | Não lê código, não implementa, não commita |
| Criativo | `criativo` | Cristaliza ideias, escreve PRD (`/prd-feature`), GDD e DS. Registra ajustes de playtest (`/ajustar`, seção 9 do PRD) | Código e Spec |
| Arquiteto | `arquiteto` | Pesquisa o código (só lê), escreve a Spec com os tickets (≤ ~400 linhas cada) e as ADRs | Implementar |
| Fábrica | `fabrica` | Implementa um ticket por vez (`/implementar`): testes red primeiro, depois o mínimo de código. Única que usa o Editor (editor-lock) | PRD, Spec, git |
| Examinador | `examinador` | Revisa (`/revisao`: padrões, Spec, ADR, DS) e roda os testes. Veredito APROVADO com nota ≥ 80% e zero achados | Implementar |
| DevOps | `devops` | Git: commit, versão, tag, PR contra a `main` (`/commit`), rebase e release. Sobe em modo manual | Código |
| Prisma | `prisma` | Shaders e trilha de arte técnica | — |

## Fluxo

```
ideia ─► /issue (Controlador)
      ─► /prd-feature (Criativo)          Documentation~/prd/<feature>.md
      ─► Spec + ADRs (Arquiteto)          Documentation~/specs/<feature>.md, Documentation~/adr/NNNN-*.md
      ─► /implementar <ticket> (Fábrica)  red ─► green, um ticket por vez
      ─► /revisao <ticket> (Examinador)   APROVADO ou REPROVADO com achados ─► volta para a Fábrica
      ─► /commit (DevOps)                 Conventional Commits, CHANGELOG, versão, PR
playtest ─► /ajustar (Criativo)           seção 9 do PRD ─► nova rodada no Arquiteto
```

**Gates com o usuário** (o Controlador sempre pergunta antes): aprovar PRD, aprovar Spec, aprovar ADR,
criar issue, abrir PR, publicar tag/release.

**WIP:** no máximo um ticket em implementação. A Fábrica não pega o próximo ticket antes de o
Examinador aprovar o atual.

## Documentos

```
Documentation~/
  TIME.md          este arquivo
  gdd/             Game Design Document (visão de jogo que motiva as features)
  ds/              Design System (padrões de API, nomes, UX do Inspector)
  prd/             um PRD por feature
  specs/           uma Spec por feature, com os tickets
  adr/             decisões de arquitetura (NNNN-titulo.md)
```

`Documentation~/` segue a convenção UPM: a Unity ignora a pasta, então ela não gera `.meta` nem
entra no pacote importado.

## Editor-lock

Só a Fábrica abre o Unity Editor ou roda o Unity em batchmode. Antes, ela cria
`.claude/editor.lock` com o ticket e o horário. Ao terminar, mesmo se falhar, ela remove o arquivo.
O Examinador roda os testes só quando não há lock. Se o lock existir, ele espera ou avisa o
Controlador. O lock é local e está no `.gitignore`.

## Issues

As issues do SpellBook ficam em `gabrielctorres/PlaygroundRumble`, com título `[SpellBook] ...` e
label `spellbook`. O código é corrigido aqui, de forma genérica e reutilizável entre jogos.
