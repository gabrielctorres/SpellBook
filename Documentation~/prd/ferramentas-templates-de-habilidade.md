# PRD: Wizard de templates de habilidade (B-019a)
Backlog: B-019a (parte do B-019) · Issues: (criadas depois do gate) · Status: Rascunho · Data: 2026-10-31

Origem: [planos/gas-estudo-e-roadmap.md](../planos/gas-estudo-e-roadmap.md), H.6 (fluxo do designer, passo 1) e H.7 (ferramentas de editor). ADRs [0007](../adr/0007-documentacao-como-codigo-e-gate-de-doc.md), [0009](../adr/) (quebra aceita na 3.0) e [0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md) (editor em UI Toolkit, sem Odin, sem IMGUI).
Depende de: **B-006** (API pública de tags), **B-007** (valores por nível nos modelos), e, **por template**, do recurso dele: Passiva **B-012**, Aura **B-014**. Projétil, Área e Buff não esperam nada além do B-007. Este PRD usa o que eles entregam e não reescreve o escopo deles.
Relacionado: [B-019b balanceamento](ferramentas-tabela-de-balanceamento.md), [B-019f samples](samples-receitas.md) (os samples usam estes templates). Este PRD não tem foco em nenhum jogo.

---

## 1. Problema

Criar uma habilidade é montar vários assets sem guia: habilidade, custo, recarga, dano e tags, ligados um no outro, com nomes inventados. Não há "comece daqui". O único atalho (`Tools/Create Default Project Folders`, `Editor/Core/FolderStructureCreator.cs`) cria pastas de um jogo específico e não é do pacote.

## 2. Objetivo

Quem usa o pacote cria uma **habilidade pronta pelo Editor, sem script**, escolhendo um template (Projétil, Área, Buff, Passiva, Aura). O wizard copia assets modelo, liga um no outro, nomeia (`GA_`, `GE_`) e cria as tags. Um projeto cria os **seus** templates **do mesmo tipo de asset**, sem código.

**Princípio (critério do usuário: modular, sem remendos).** Um template é **um asset de dados**, não código; o wizard usa só as APIs públicas e os assets do pacote; não há segundo mecanismo.

## 3. Usuários e cenários

| Usuário | O que espera |
|---|---|
| **Designer técnico** | Bola de Fogo em um clique, pronta para ajustar |
| **Quem adota o pacote** | Um ponto de partida que já passa no validador |
| **Time de um jogo** | Seus próprios templates no mesmo menu |

Cada cenário vira teste automatizado, salvo S (verificável no host).
- **C1, criar do template:** `Create > SpellBook > GAS > Ability (template) > Projétil` com nome "Fireball" cria, na pasta escolhida, `GA_Fireball`, o efeito de recarga, o de custo e o de dano, com as referências ligadas **entre as cópias**, e as tags `Ability.Fireball` e `Cooldown.Fireball`. O `ProjectValidator` não acusa erro.
- **C2, cópia independente:** editar a cópia não muda o modelo; criar duas vezes com nomes diferentes dá dois conjuntos independentes.
- **C3, sem sobrescrever:** se um asset do conjunto já existe, o wizard recusa, diz qual e pede outro nome; nada muda.
- **C4, tags existentes:** tag que já existe é reutilizada, não duplicada.
- **C5, desfazer (S):** Ctrl+Z desfaz assets e tags de uma vez.
- **C6, template próprio:** um projeto cria o seu `AbilityTemplate` apontando para os seus modelos e ele aparece no mesmo menu.
- **C7, recurso ausente:** template cujo recurso não existe (Aura sem o B-014) não aparece no menu; o validador acusa template com modelo ausente.

## 4. Requisitos funcionais

R1. **`AbilityTemplate`.** `ScriptableObject` que descreve **o que copiar**: lista de assets modelo (habilidade e efeitos), regras de nome (prefixos e nomes derivados) e tags a criar (`Ability.<Nome>`, `Cooldown.<Nome>`). O pacote entrega os templates; o projeto cria os seus do mesmo tipo (C6).
R2. **Menu.** `SpellBook/Criar habilidade ou efeito…` e um item só no Create, `Create > SpellBook > GAS > Ability from Template…`, que abre o wizard com a pasta selecionada (gate de 2026-11-05). O wizard pede **nome** e **pasta**. Templates do pacote: **Projétil, Área, Buff, Aura, Em branco e Só efeito**; a Passiva depende da Q3 do [design doc](../spec/menu-de-editor-do-gas.md). **Não há template de Dash** (decisão do usuário, 2026-10-31).
R3. **Cópia ligada.** Copia os modelos e **refaz as referências entre as cópias** (a habilidade aponta para custo e recarga **copiados**). Os modelos do pacote não mudam. Prefabs (projétil, thinker) não são copiados: a cópia aponta para o prefab do pacote (gate de 2026-11-05).
R4. **Atômico e sem sobrescrever.** Ou cria o conjunto inteiro, ou nenhum asset; colisão de nome recusa o conjunto (C3).
R5. **Tags** criadas pela API pública de tags (B-006), reutilizando as existentes (C4). Nome de tag inválido é recusado com a mensagem da validação de tags.
R6. **Uma operação de Undo** (C5), e o botão "Desfazer criação" no wizard (gate de 2026-11-05). Ao fim, seleciona a habilidade criada.
R7. **Disponibilidade por recurso.** O template só aparece se os tipos de que depende existem (C7). Os modelos são referenciados por asset; modelo ausente é achado do validador.
R8. **Substitui o `FolderStructureCreator`.** O menu e a classe saem do pacote (quebra aceita na 3.0, com CHANGELOG). O wizard não cria pastas de projeto.
R9. **Modelos são dados do pacote**, validados pelo `ProjectValidator` e cobertos por teste (cada template cria conjunto sem erro).
R10. **Editor em UI Toolkit**, sem Odin e sem IMGUI; mensagens em português e clicáveis; `[HelpURL]` nos tipos novos. A lógica (planejador de cópia, regras de nome) fica em classes sem UI, testáveis em EditMode.

## 5. Requisitos não funcionais

N1. Fica em `.Editor` do módulo GAS; um `.Editor` não referencia outro (só `SpellBook.Core.Editor`); a casa final é do Arquiteto. Nenhum runtime referencia `.Editor`.
N2. Sem estado global, sem `Find`, sem singleton.
N3. Testes determinísticos, com factories e sem disco real quando possível. Asmdefs são da Spec.

## 6. Fora do escopo

- Template de **Dash** (P5, decidido) e de **Escudo** (a receita do Escudo é do B-019f).
- Criar pastas de projeto; **tabela de balanceamento** (B-019b); simulador (B-019c).
- Editor visual de templates; importar template de fora.

## 7. Critérios de aceite

T = teste automatizado; E = estrutural; S = verificável no host.
- CA1 (T). C1: cada template do pacote cria o conjunto, ligado, com tags e **zero erro** no validador.
- CA2 (T). C2, C3: cópia independente; colisão recusa o conjunto e não altera nada.
- CA3 (T). C4: tag existente reutilizada; tag inválida recusada.
- CA4 (S). C5: um Undo desfaz tudo.
- CA5 (T). C6, C7: template do projeto aparece; template com modelo ausente é acusado e, sem o recurso, não aparece.
- CA6 (E). R8: `FolderStructureCreator` e o menu saem; o CHANGELOG lista a remoção.
- CA7 (E). Página do manual em `Ferramentas`, `[HelpURL]` nos tipos, CHANGELOG cita a ferramenta; nenhum `OnGUI` com lógica nem `Sirenix`.

## 8. Perguntas em aberto

### Com o usuário
- Nenhuma nova neste PRD (P5 e P7 do B-019 respondidas).

### Com o Arquiteto
- **P1.** Casa do `.Editor` (GAS ou ferramentas) respeitando a 0026.
- **P2.** Cópia ligada: `AssetDatabase.CopyAsset` e mapa de substituição de referências; atomicidade se um passo falhar.
- **P3.** Como o template lista modelos e como o validador detecta modelo ausente.
- **P4.** Ordem: Projétil, Área e Buff primeiro; Passiva depois do B-012; Aura depois do B-014.

## 9. Ajustes de playtest

| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
| 2026-10-31 | Bloco A do B-019, separado em PRD próprio por decisão do usuário (P1: um PRD por bloco) | Todo o documento | Rascunho |
| 2026-10-31 | usuário (via Controlador) P5: sem template de Dash | R2, seção 6 | Aplicado |
| 2026-11-04 | Desenhista: a parte "criar habilidade, tags e efeitos por um menu do Editor" do B-041 foi fundida neste PRD, porque tem o mesmo objetivo de R1 a R6. A parte do Inspector virou o design doc [spec/inspector-de-habilidade.md](../spec/inspector-de-habilidade.md). O menu segue a ADR 0043 (Proposta). O nome padrão do pacote não tem prefixo (gate parcial de 2026-11-04). Os prefixos `GA_` e `GE_` do R1 são regras de nome de cada template, e o projeto pode trocá-los. **Pergunta ao usuário:** entra um template "Em branco" (habilidade, recarga, custo e as duas tags, sem ações) além dos cinco do R2? | R1, R2 | Proposto |
| 2026-11-05 | Desenhista: o assistente e o gerenciador do GAS viraram um épico só, no design doc [spec/menu-de-editor-do-gas.md](../spec/menu-de-editor-do-gas.md). Este PRD segue como a parte fixa do assistente, e o design doc faz o papel da Spec. Pelo código de hoje: uma tag é um asset `GameplayTagDefinition`, e o B-006 não tem API para criar tag, então o R5 vira regra de nome do épico (Q7); a Passiva e a Aura já têm o recurso no código (Q3, Q4). A pergunta do "Em branco" passou para a Q5 do design doc | R2, R5, R7 | Proposto |
| 2026-11-05 | usuário (gate do design doc): menu `SpellBook/` e um item só no Create (Q1, Q2); Aura e Em branco entram (Q4, Q5); prefabs apontam para o pacote, sem cópia (Q6); a tag é um asset com o nome inteiro e o pai (Q7); botão "Desfazer criação" além do Ctrl+Z (Q8). A Passiva segue aberta (Q3) | R2, R3, R5, R6 | Aplicado |
