---
name: prd-feature
description: Criativo escreve o PRD de uma feature do SpellBook em Documentation~/prd/. Use quando uma ideia ou issue precisar virar requisitos antes da Spec.
argument-hint: <feature ou link da issue>
---

Escreva o PRD de: $ARGUMENTS

1. Leia a issue (se houver), o GDD e o DS relevantes em `Documentation~/`.
2. Faça ao usuário as perguntas que faltarem. Não invente requisito.
3. Crie `Documentation~/prd/<feature-em-kebab-case>.md` com este modelo:

```markdown
# PRD: <feature>
Issue: <link> · Status: Rascunho | Aprovado · Data: <AAAA-MM-DD>

## 1. Problema
## 2. Objetivo
## 3. Usuários e cenários de uso
## 4. Requisitos funcionais
## 5. Requisitos não funcionais   (performance, alocação, compatibilidade Unity)
## 6. Fora do escopo
## 7. Critérios de aceite          (verificáveis, um por linha)
## 8. Perguntas em aberto
## 9. Ajustes de playtest          (preenchido via /ajustar)
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
```

4. O PRD só vira `Aprovado` com o OK do usuário. Depois disso, o próximo papel é o Arquiteto.
