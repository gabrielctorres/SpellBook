# PRD: Tabela de balanceamento (B-019b)
Backlog: B-019b (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.6 e H.7. ADR [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (UI Toolkit, sem Odin, sem IMGUI).
Depende de: **B-007** (valores nomeados por nível, `GetValue(nome, nível)`) e **B-005** (limites e `MaxLevel`, regras do validador). Este PRD usa o que eles entregam e não reescreve o escopo deles.
Relacionado: [B-019a templates](ferramentas-templates-de-habilidade.md) (cria as habilidades que a tabela lista). Sem foco em nenhum jogo.

---

## 1. Problema

Para saber se o dano do nível 3 da Bola de Fogo está acima do da Cura, o designer abre cada habilidade e lê a tabela. Não há uma visão de **todas as habilidades por nível**.

## 2. Objetivo

Uma janela `SpellBook/Balanceamento` que mostra as habilidades do projeto (linhas) e os valores nomeados por nível (colunas), permite filtrar, ordenar e **editar na célula, com Undo** (decisão do usuário, P7).

**Princípio (modular, sem remendos).** A tabela **não guarda dado próprio**: o asset é a única fonte; ler e escrever passa pela API pública do B-007.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Comparar dano, recarga e custo de todas as habilidades por nível e ajustar ali mesmo |
| **Mantenedor** | Lógica testável sem janela |

- **C1, visão geral:** lista as habilidades e, por nível, os valores nomeados (`damage`, `cooldown`, `cost`...) lidos por `GetValue(nome, nível)`.
- **C2, filtro e níveis:** filtrar por tag, pasta e nome; escolher o intervalo de níveis; ordenar por coluna.
- **C3, editar na célula:** alterar uma célula grava no asset, com Undo; reabrir mostra o valor. Valor que não é lista de números (curva, `%nome` encadeado) é só leitura.
- **C4, destaque:** valor ausente para um nível, nome inexistente e nível acima de `MaxLevel` aparecem destacados, com o motivo.

## 4. Requisitos funcionais

R1. **Janela** `SpellBook/Balanceamento` (UI Toolkit): linhas = habilidades; colunas = valores nomeados por nível, pela API pública.
R2. **Filtro, intervalo e ordem** (C2).
R3. **Edição na célula com Undo** (C3). Curva e `%nome` ficam só leitura; pilhas e política também.
R4. **Destaque de problemas** pelas **mesmas regras do validador** (C4), com mensagem em português.
R5. **Modelo da tabela sem UI** (classe testável em EditMode): devolve valores por habilidade e nível, aplica filtro/ordem e grava a edição.
R6. `[HelpURL]` nos tipos novos; página em `Ferramentas`.

## 5. Requisitos não funcionais

N1. Fica em `.Editor`; um `.Editor` não referencia outro; lê GAS, Tags e Core (casa é do Arquiteto). Nenhum runtime referencia `.Editor`.
N2. Sem estado global. Testes com factories, sem asset fixo.

## 6. Fora do escopo

- Exportar/importar **CSV**; ajuste automático de balanceamento; gráficos comparativos entre habilidades.
- Editar curva, `%nome` encadeado, pilhas e política na tabela.
- Simular o efeito no tempo (B-019c).

## 7. Critérios de aceite

- CA1 (T). C1, C2: o modelo devolve valores por habilidade e nível pela API pública, com filtro, intervalo e ordem.
- CA2 (T). C3: editar uma célula grava no asset com Undo; curva é só leitura.
- CA3 (T). C4: valor ausente, nome inexistente e nível acima de `MaxLevel` são destacados com o motivo, iguais às regras do validador.
- CA4 (S). A janela abre no host com as habilidades do projeto.
- CA5 (E). Página do manual, `[HelpURL]`, CHANGELOG; sem `OnGUI` com lógica nem `Sirenix`.

## 8. Perguntas em aberto

### Com o usuário
- **P1. Habilidades sem tabela de valores. RESPONDIDA (2026-10-31): B, aceito.** A habilidade é **listada** com as células vazias e a marca "sem valores por nível". Ocultar fica descartado.

### Com o Arquiteto
- **P1.** Casa do `.Editor`.
- **P2.** Como reutilizar as regras do validador no destaque sem duplicar.
- **P3.** Descoberta das habilidades do projeto (busca de assets) sem alocar a cada repintura.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco B do B-019, PRD próprio (P1 do usuário) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P7: tabela editável com Undo | R3, C3 | Aplicado |
