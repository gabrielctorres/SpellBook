---
name: design-doc
description: Desenhista escreve a parte fixa do design doc de um épico novo em Documentation~/spec/ (problema, critérios de aceite, cenário no Sandbox e tickets). Use só quando nasce um épico.
argument-hint: <épico ou link da issue>
---

Escreva a parte fixa do design doc de: $ARGUMENTS

1. Leia o item do backlog, a issue (se houver), o DS e as ADRs que valem, e o código que o épico toca.
2. Faça ao usuário as perguntas que faltarem. Não invente requisito.
3. Copie `Documentation~/spec/_modelo-design-doc.md` para `Documentation~/spec/<épico-em-kebab-case>.md` e preencha a parte fixa em 1 a 2 páginas: problema, CA numerados e verificáveis, cenário no Sandbox (`Packages/com.spellbook/Samples~/Sandbox`) e tickets com objetivo, CA e dependências. Não escreva o Detalhe dos tickets: ele é da Fábrica, na entrada da fila.
4. Decisão que vale para mais de um épico vira ADR nova, com status `Proposta`, em `Documentation~/adr/`. A que vale só para o épico fica em "Decisões do épico".
5. O design doc só vira `Aprovado` no gate único do usuário, junto com as ADRs novas (ADR 0002). Devolva os caminhos e as perguntas para o gate.
