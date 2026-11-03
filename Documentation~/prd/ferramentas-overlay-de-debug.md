# PRD: Overlay de debug em build (B-019d)
Backlog: B-019d (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.7. ADRs [0012](../adr/) (módulos), [0017](../adr/) (UI) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md).
Depende de: **B-003** (modelo de leitura do GAS Debugger e efeitos ativos) e **B-015** (`ActorResolver`). Não espera o Lote B a D além disso. Este PRD usa o que eles entregam e não reescreve o escopo deles.
Relacionado: o **GAS Debugger v1** (janela de Editor) é do B-003. Sem foco em nenhum jogo.

---

## 1. Problema

O GAS Debugger (B-003) e o Event Debugger existem só no Editor. Um bug que só aparece numa build de teste não tem janela para ver atributos, tags, efeitos e recarga.

## 2. Objetivo

Um componente `GasDebugOverlay` que mostra, numa **build de desenvolvimento** e no Play do Editor, o estado de um ASC. É **só leitura** e **só em desenvolvimento** (decisão do usuário, P4).

**Princípio (modular, sem remendos).** O overlay e a janela do Debugger leem o **mesmo modelo de leitura**, que mora em assembly de runtime; nenhum reimplementa a leitura do ASC.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Desenvolvedor de gameplay** | Ver o ASC numa build de teste |
| **QA do projeto** | Abrir e fechar o overlay com o botão do próprio jogo |

- **C1, ver o ASC:** atributos (base e atual), tags com contagem, efeitos com tempo restante e pilhas, habilidades com recarga e cargas, último motivo de falha.
- **C2, abrir e fechar sem entrada própria:** `Show`, `Hide`, `Toggle` públicos e um canal de evento opcional; o pacote não lê teclado nem controle.
- **C3, custo zero fora do uso:** fechado ou ausente, sem `Update` do overlay nem alocação por quadro; em **release** o código não existe.
- **C4, mesmo modelo do Debugger:** overlay e janela mostram os mesmos números para o mesmo ASC.

## 4. Requisitos funcionais

R1. **`GasDebugOverlay`.** Desenha o estado do ASC (C1). O ASC vem pelo `ActorResolver` (B-015) ou por campo, **sem `Find` e sem singleton**.
R2. **Um modelo, duas telas.** Modelo de leitura em assembly de runtime, compartilhado com o Debugger (C4). A posição é do Arquiteto e **pode emendar a Spec do B-003**.
R3. **Abrir e fechar** por `Show`/`Hide`/`Toggle` e canal de evento opcional (C2).
R4. **Só leitura.** Sem aplicar efeito, conceder habilidade nem cheat (P4).
R5. **Só em desenvolvimento.** Compilado só em build de desenvolvimento e no Editor; em release não existe (C3).
R6. **Sem dependência de pacote de UI** (`com.unity.ugui`, `SpellBook.UI`); a tecnologia de desenho é do Arquiteto.
R7. `[HelpURL]`, `[CustomEditor]` e página em `Ferramentas`.

## 5. Requisitos não funcionais

N1. Aberto, atualizar não aloca depois do aquecimento; fechado, não faz trabalho.
N2. O GAS não referencia UI nem Events (ADR 0012). Nenhum runtime referencia `.Editor`.
N3. Sem singleton, `Find` nem estático mutável.

## 6. Fora do escopo

- **Ações e cheats** no overlay; **release por ligação explícita** (P4, decidido).
- Console em jogo, rede, replicação, perfilador; GAS Debugger v1 (B-003).
- Ler teclado, mouse ou controle.

## 7. Critérios de aceite

- CA1 (T). C1, C4: o modelo de leitura devolve os mesmos números para a janela do Debugger e para o overlay.
- CA2 (S). C1, C2: na build de desenvolvimento, `Toggle` abre e fecha com o ASC correto.
- CA3 (T). C3: fechado, não aloca nem trabalha por quadro; aberto, 10 000 atualizações alocam 0 bytes gerenciados depois do aquecimento.
- CA4 (E). O código não existe em release; o pacote não lê entrada; nenhum `Find`, singleton ou estático mutável.
- CA5 (E). Página, `[HelpURL]`, CHANGELOG.

## 8. Perguntas em aberto

### Com o usuário
- **P1. Vários ASCs. RESPONDIDA (2026-10-31): D, aceito.** O overlay mostra **um ASC por vez**, com troca por chamada pública (`SetTarget`). A lista de todos os ASCs vivos fica descartada.

### Com o Arquiteto
- **P1.** Onde mora o modelo de leitura (módulo de debug novo ou dentro do GAS) e a ordem em relação ao ticket do Debugger do B-003.
- **P2.** Tecnologia de desenho (UI Toolkit em runtime, IMGUI de runtime ou uGUI): custo de dependência e alocação.
- **P3.** Como o overlay ouve o `EventChannel` sem o GAS referenciar o Events.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco D do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P4: só leitura, só em build de desenvolvimento | R4, R5, seção 6 | Aplicado |
| 2026-10-31 | Achado: o modelo do Debugger (B-003) precisa estar em runtime; pode emendar a Spec do B-003 | R2, seção 8 (P1 do Arquiteto) | Aguardando decisão do Arquiteto |
