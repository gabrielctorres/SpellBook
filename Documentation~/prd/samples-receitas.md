# PRD: Sample "Receitas" (B-019f)
Backlog: B-019f (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.8 (documentação, "Receitas") e Fase 9. ADR [0007](../adr/0007-documentacao-como-codigo-e-gate-de-doc.md) (gate de doc).
Depende de: [B-019a templates](ferramentas-templates-de-habilidade.md) (as receitas de habilidade usam os templates) e, **por receita**, do recurso dela: Passiva **B-012**, Aura **B-014**, Escudo **B-010**; Área e Buff sem espera extra. O **Sandbox** e o **HUD S4** são do **B-032** ([sample-sandbox.md](sample-sandbox.md)) e não mudam aqui.
Sem foco em nenhum jogo.

---

## 1. Problema

O Sandbox prova que os módulos funcionam juntos, mas não ensina "como faço um escudo, uma aura, uma passiva".

## 2. Objetivo

Um sample **"Receitas"** em `Samples~`, com **uma cena por receita**, cada uma com a sua página no manual: o que o sample mostra, os assets que importa e o que apertar.

**Princípio (modular, sem remendos).** Cada receita só entra **quando o recurso dela existe**; nenhuma cena finge recurso. As receitas de habilidade nascem **dos templates** do B-019a.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Quem adota o pacote** | Importar, abrir a cena e copiar a receita |
| **Designer** | Ver o template aplicado num caso real |

- **C1, importar e abrir (S):** o sample aparece na aba Samples, importa sem erro e cada cena abre sem referência faltando, com entrada pelo **Input Manager legado**.
- **C2, uma cena por receita:** cada receita tem cena própria e jogável sozinha.
- **C3, página por receita:** cada receita tem página no manual que aponta a cena, explica os assets e diz o que apertar.
- **C4, recurso ausente:** receita cujo recurso não existe não está no sample.

## 4. Requisitos funcionais

R1. **Um sample "Receitas"** declarado em `samples` do `package.json`, com asmdef próprio, **sem dependência de terceiros** e entrada pelo **Input Manager legado**.
R2. **Uma cena por receita:** Passiva, Área, Aura, Escudo, Buff (e as que o B-019a entregar). **Sem Dash** (decisão do usuário, P5).
R3. **Página por receita** no manual (gate de doc, ADR 0007).
R4. **Só com o recurso presente** (C4); a receita entra no mesmo ticket que o recurso permitir.
R5. **Usa os templates** do B-019a quando a receita é uma habilidade.
R6. O **HUD do Sandbox (S4)** continua no B-032; este sample não o traz (decisão do usuário, P2).

## 5. Requisitos não funcionais

N1. Samples sem dependência de terceiros; asmdef próprio (criado pela Spec).
N2. Dados e cenas independentes entre receitas (apagar uma não quebra as outras).

## 6. Fora do escopo

- Sandbox e HUD S4 (B-032); receita de **Dash**; sample por receita (um sample só, P2).
- Guias por gênero, FAQ, migração 2.x para 3.0; traduzir o manual.

## 7. Critérios de aceite

- CA1 (S). C1, C2: o sample importa sem erro; cada cena abre sem referência faltando e responde ao Input Manager legado.
- CA2 (E). C3, C4: cada cena tem página; nenhuma usa recurso inexistente.
- CA3 (T). Cada habilidade do sample passa no `ProjectValidator` sem erro.
- CA4 (E). `package.json` declara o sample; CHANGELOG cita as receitas.

## 8. Perguntas em aberto

### Com o usuário
- **P1. Receita de Buff e de DoT. RESPONDIDA (2026-10-31): F, aceito.** **Buff** é receita própria e o **DoT** (dano por tempo) fica **dentro da cena de Área**. Cena própria de DoT fica descartada.

### Com o Arquiteto
- **P1.** Estrutura do sample (cenas, dados compartilhados) sem acoplar as receitas.
- **P2.** Ordem: uma receita por recurso, depois do ticket do recurso.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco F do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P2: um sample com uma cena por receita; HUD S4 no B-032. P5: sem Dash | R2, R6, seção 6 | Aplicado |
