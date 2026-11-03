# PRD: Simulador de efeito (B-019c)
Backlog: B-019c (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.7. ADRs [0014](../adr/) (`ITimeSource`, `ManualTimeSource`) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md).
Depende de: **B-007** (nível), **B-003** (efeitos ativos e eventos), **B-004** (eventos públicos do ASC) e **B-005** (limites de atributo e ordem de cálculo). Este PRD usa o que eles entregam e não reescreve o escopo deles.
Relacionado: [B-019b](ferramentas-tabela-de-balanceamento.md) (valores por nível). Sem foco em nenhum jogo.

---

## 1. Problema

Para ver "queima 10 por segundo durante 5 s e empilha até 3", o designer monta cena, ASC, chama o efeito e olha o Debugger. Não há como ver a linha do tempo sem jogar.

## 2. Objetivo

Uma janela `SpellBook/Simulador de efeito` que aplica um `GameplayEffect` num ASC de teste, avança o tempo e mostra o **gráfico dos atributos** e a **lista de eventos**, **sem entrar no Play**. Simula **só efeitos** (decisão do usuário, P8).

**Princípio (modular, sem remendos).** O simulador roda o **GAS real** com `ManualTimeSource`. Não há fórmula, ordem de cálculo nem política de pilhas própria: se o GAS muda, o simulador muda junto.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Ver a queimadura no tempo e conferir pilhas e limites |
| **Mantenedor** | Garantia de que o simulador não diverge do GAS |

- **C1, linha do tempo:** efeito de 5 s com 10 de dano por segundo: curva da vida de 100 a 50 em 5 s, com marcas de aplicado, cada tique e expirado.
- **C2, nível e pilhas:** nível 3 e 3 pilhas mudam a curva como o GAS real; reaplicar num instante escolhido empilha ou renova pela política do efeito.
- **C3, limites e composição:** com `Min`/`Max` e modificadores concorrentes, a curva obedece à ordem de cálculo única do B-005.
- **C4, determinismo:** mesma entrada, mesma curva.
- **C5, limpeza:** fechar a janela destrói o ASC e a cena de simulação; nada fica no projeto.

## 4. Requisitos funcionais

R1. **Janela** `SpellBook/Simulador de efeito` (UI Toolkit): escolher o efeito, nível, pilhas, atributos de partida (conjunto padrão editável), duração simulada e instantes de reaplicação.
R2. **GAS real, tempo manual.** ASC **isolado, só em memória** (sem cena do projeto, sem Play), com `ManualTimeSource`; aplica o efeito e avança o tempo.
R3. **Saída.** Gráfico dos atributos escolhidos e lista de eventos com instante (aplicado, cada tique, pilha mudou, expirado, bloqueado), lidos pelos eventos públicos do ASC; valor do instante ao passar o mouse.
R4. **Determinismo e limpeza** (C4, C5).
R5. **Só efeitos** (P8): habilidade com alvo, projétil e cena fica fora.
R6. **Execução separada da janela** (classe sem UI, EditMode). `[HelpURL]` e página em `Ferramentas`.

## 5. Requisitos não funcionais

N1. `.Editor` (lê GAS, Tags, Core; casa do Arquiteto); sem referência cruzada de `.Editor`; nenhum runtime referencia `.Editor`.
N2. Sem singleton, `Find` nem estático mutável. Testes com `ManualTimeSource` e `GasFactory`.

## 6. Fora do escopo

- **Ativação de habilidade** (custo, recarga, cargas), projétil, alvo, cena (P8, decidido).
- Comparar simulações lado a lado, exportar, editar o efeito na janela.
- Replicação e rede.

## 7. Critérios de aceite

- CA1 (T). C1: a curva e os eventos do efeito de 5 s com 10 por segundo batem com o `ManualTimeSource`.
- CA2 (T). C2, C3: nível, pilhas, reaplicação, limites e composição dão **o mesmo resultado** de um ASC criado direto pela `GasFactory`.
- CA3 (T). C4, C5: execução determinística; fechar volta a contagem de objetos ao valor inicial.
- CA4 (S). A janela mostra gráfico e eventos no host, sem Play.
- CA5 (E). Página, `[HelpURL]`, CHANGELOG; sem `OnGUI` com lógica nem `Sirenix`; busca de código não acha fórmula própria.

## 8. Perguntas em aberto

### Com o usuário
- **P1. Atributos de partida. RESPONDIDA (2026-10-31): C, aceito.** Conjunto padrão (vida 100, mana 100) **editável na janela**. Ler do `AttributeSet` de um prefab fica descartado.

### Com o Arquiteto
- **P1.** Casa do `.Editor`.
- **P2.** Criar e destruir um ASC com `ManualTimeSource` no Editor sem cena do projeto e sem vazar objeto.
- **P3.** Eventos do ASC que o B-003/B-004 expõem e o que falta para a lista de eventos.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco C do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P8: simulador só de efeitos | R5, seção 6 | Aplicado |
