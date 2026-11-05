# DS: Inspectors da habilidade e do efeito (abas no topo)
Status: Aceito (aprovado em 2026-11-05) · Data: 2026-11-05 · Dono: Desenhista
Origem: [design doc B-041](../spec/inspector-de-habilidade.md), respostas do usuário de 2026-11-05 (layout B, sem resumo no topo, habilidade e efeito), [artefato](https://claude.ai/artifact/94QTsdexDe1eAtYyPzdDuA?sk=wUikJrY-YHgCHtBS16TfKw) (aba "Inspector da habilidade", layout B), [ADR 0026](../adr/0026-spellbook-sem-odin-editor-proprio-em-ui-toolkit.md), [DS do Inspector de ações](inspector-de-acoes.md).

Este DS define onde fica cada campo e como as abas se comportam. As listas de ações dentro da aba Ações seguem o DS de ações, sem mudança.

## 1. Escopo
Vale para o `GameplayAbility` e o `GameplayEffect`. Um épico seguinte pode aplicá-lo ao `AttributeDefinition`. Não cobre o assistente de criação (B-019a) nem o gerenciador do GAS.

## 2. Estrutura comum
1. Avisos do topo e validação do `SpellBookEditor`, como hoje, acima das abas. As mensagens aparecem em qualquer aba.
2. Faixa de abas (`TabView`), com uma linha de altura e os nomes em português.
3. Conteúdo da aba ativa. Dentro de uma aba, os campos ficam em grupos com título em negrito, sem dobra.

Regras:
- A aba ativa é lembrada por asset no `SessionState`, com chave própria, separada da chave das abas de eventos. Um asset sem aba lembrada abre na primeira.
- Cada campo serializado visível fica em uma aba só. Campo que o editor não conhece fica no fim da primeira aba, num grupo "Outros", e nunca some.
- Campo que não vale na configuração atual some (`display: none`) e guarda o valor. Não há campo desabilitado em cinza.
- Os títulos dos grupos vêm do editor. Os `[Header]` do runtime não aparecem duplicados.

## 3. Abas da habilidade

| Aba | Grupo | Campos (na ordem) |
|---|---|---|
| Configuração | Identidade | ícone, nome, tag da habilidade, descrição |
| | Comportamento | comportamento, modo de commit, duração e intervalo da canalização (`Channel`), `reactivatePassive` (`Passive`), autocast (`Instant` e `Channel`) |
| | Custo e recarga | efeito de custo, efeito de recarga, atributo e política de redução, cargas |
| | Condições de ativação | alcance, regras de ativação e, abaixo, o resumo das regras (uma linha por regra: resumo e motivo) |
| Ações | | a faixa de abas de eventos do DS de ações, com "A cada passo" só em `Channel` |
| Tags e gatilhos | Tags | as seis listas, compactas (seção 6), com "N de 6 com itens" no título |
| | Gatilhos | lista de gatilhos |
| Níveis e prévia | Níveis | nível máximo, tabela de valores |
| | Prévia | nível de prévia, prévia por nível (seção 5), prévia da descrição no nível de prévia |

## 4. Abas do efeito

| Aba | Grupo | Campos (na ordem) |
|---|---|---|
| Configuração | Identidade | nome, ícone, buff, debuff, oculto, purgável |
| | Duração | política, duração (`HasDuration`), nível padrão, período |
| | Empilhamento | pode empilhar, máximo, renovar duração, multiplicar valor (os três só com pode empilhar) |
| Modificadores | Persistentes | modificadores |
| | Do tique | modificadores do tique (só com período maior que 0) |
| Ações | | a faixa de abas de eventos, com "A cada tick" só com período maior que 0; abaixo do grupo "Reações", os atributos de dano e o atributo de morte |
| Tags | Tags | tipos de dano, concedidas, requisitos de aplicação, imunidade, compactas (seção 6) |

## 5. Prévia por nível
- Uma tabela com uma linha por nível, de 1 ao nível máximo, e uma coluna por valor nomeado, mais recarga, custo e cargas. Como hoje, a recarga só aparece com efeito de recarga com duração, e as cargas só com as cargas ligadas.
- A linha do nível de prévia tem fundo de seleção e o marcador `◂` no número do nível, para não depender só de cor.
- O campo "Nível de prévia" continua, limitado a [1, máximo].
- Recarga sem a redução do atributo, como hoje. Custo e máximo de cargas que vêm de atributo ou fórmula mostram o nome da fonte, como a prévia das cargas faz hoje.
- Valores com até três casas decimais, na cultura atual do Editor.

## 6. Lista de tags compacta
- Vazia: uma linha com o nome da lista em texto secundário e o botão "+ adicionar" à direita.
- Com itens: o nome, os itens um por linha e o botão de adicionar no rodapé.
- O tooltip do nome é o `[Tooltip]` do campo.

## 7. Estados especiais

| Estado | Comportamento |
|---|---|
| Várias seleções | As abas aparecem, e os campos editam todos os assets. A aba Ações segue o DS de ações (seção 9). A prévia por nível usa o primeiro asset |
| Aba de eventos escondida com ações | Aviso do DS de ações (3.1) no grupo que causou: Comportamento na habilidade, Duração no efeito |
| Modo Play | Igual ao Inspector padrão: tudo editável |

## 8. Acessibilidade
- Teclado: as abas recebem foco e trocam com as setas, como o `TabView` da Unity.
- Nenhuma informação só por cor (seção 5).
- Os textos usam a fonte padrão do Inspector e respeitam o zoom do Editor.

## 9. Perguntas em aberto
Nenhuma. O gate de 2026-11-05 aprovou as abas do efeito e deixou fora a prévia por nível do efeito e o ponto de aviso nas abas do topo.

## 10. Histórico

| Data | Mudança |
|---|---|
| 2026-11-05 | Primeira versão, com as respostas do usuário de 2026-11-05, para o gate do design doc |
| 2026-11-05 | Aprovado no gate do design doc: abas do efeito como propostas; prévia do efeito e ponto de aviso nas abas ficam fora |
