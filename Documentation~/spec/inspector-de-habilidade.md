# Design doc: Inspector da habilidade reorganizado
Épico: `epic:inspector-de-habilidade` · Backlog: B-041 · ADRs: nenhuma nova (segue 0026 e o DS `ds/inspector-de-acoes.md`) · Status: Rascunho (para discutir com o usuário antes do gate)

## Fusão com o B-019a
O B-041 tinha duas partes:
- **Criar a habilidade, com as tags e os efeitos, por um menu do Editor.** É o objetivo do B-019a (wizard de templates: `GA_`, efeitos de custo, recarga e dano, e as tags, ligados entre si e com Undo). Essa parte **foi fundida no B-019a** e não está neste documento. O que o B-041 pede além do B-019a, um template "Em branco" sem ações, ficou como pergunta no PRD do B-019a, seção 9.
- **Organizar o Inspector da habilidade.** O B-019a não cobre essa parte, que é este épico.

## Problema
O Inspector do `GameplayAbility` (`Editor/GAS/GameplayAbilityEditor.cs`) mostra o Inspector padrão e acrescenta as seções no fim:
- **O principal fica no fim.** As abas de ações ("o que a habilidade faz") aparecem depois de cerca de 30 campos.
- **Campos sem uso continuam visíveis.** A seção "Ativação" do Inspector padrão mistura 12 campos de assuntos diferentes: recarga, custo, cargas, commit, comportamento, canalização, alcance e regras. Os campos de canalização e de passiva aparecem em qualquer comportamento.
- **As prévias ficam longe dos campos.** O nível de prévia e os valores ficam longe de `Níveis`. A prévia das cargas e o resumo das regras ficam numa segunda seção "Ativação", separada da primeira.
- **As listas de tags sempre ocupam espaço.** As seis listas ficam sempre abertas, até quando estão vazias.

O designer rola o Inspector para achar o que quer mudar e não consegue ler a habilidade de relance.

## Critérios de aceite (proposta para a discussão)
1. **CA1.** O topo do Inspector mostra um resumo da habilidade no nível de prévia: ícone, nome, comportamento, custo, recarga e cargas. O resumo acompanha a edição sem precisar de Play.
2. **CA2.** As seções seguem a ordem do trabalho do designer: Identidade → Comportamento → Custo e recarga → Condições de ativação → Ações → Tags → Gatilhos → Níveis. Cada seção pode ser recolhida, e o estado fica guardado por tipo entre seleções.
3. **CA3.** Um campo que não vale no comportamento atual não aparece: os de canalização só em `Channel`, o `reactivatePassive` só em `Passive` e o autocast só em `Instant` e `Channel`. Trocar o comportamento não apaga o valor guardado.
4. **CA4.** Cada prévia fica junto do campo a que se refere: a recarga e as cargas em "Custo e recarga", o resumo das regras em "Condições de ativação" e os valores por nível em "Níveis".
5. **CA5.** Uma lista de tags vazia aparece numa linha, com o nome e o botão de adicionar. A seção "Tags" mostra no título quantas listas têm itens.
6. **CA6.** Nada muda no formato serializado nem na API pública. Os assets existentes abrem iguais, e os testes do `GameplayAbilityEditor` continuam passando, com os nomes dos elementos (`PreviewName`, `ActivationName` etc.) preservados ou migrados no mesmo ticket.
7. **CA7.** O layout segue a 0026 (UI Toolkit, sem IMGUI e sem Odin) e o DS novo, `ds/inspector-de-habilidade.md`. As abas de ações continuam as do `ds/inspector-de-acoes.md`.

## Cenário no Sandbox
1. Selecione `Data/Abilities/GA_BolaDeFogo`. O resumo no topo mostra o ícone, "Bola de Fogo", Instant, o custo de mana e a recarga de 1,5 s (CA1).
2. Recolha "Tags", selecione outro asset e volte. A seção continua recolhida (CA2).
3. Troque o comportamento para `Channel`. Aparecem a duração e o intervalo da canalização e a aba "A cada passo". Volte para `Instant`: os campos somem, e o valor digitado continua lá ao voltar (CA3).
4. Mude o nível de prévia. O resumo, a recarga e os valores por nível mudam juntos (CA1, CA4).
5. Entre em Play e conjure a Bola de Fogo. O comportamento é o mesmo de antes (CA6).

## Fora do escopo
- Criar a habilidade pelo Editor, que foi para o B-019a.
- Os Inspectors do `GameplayEffect` e do `AttributeDefinition`. Se o layout for aprovado, cada um pode virar um épico seguinte, com o mesmo DS.
- Mudar campos, nomes serializados ou o modelo de dados da habilidade.

## Decisões do épico
- A forma do layout depende da primeira pergunta abaixo. O protótipo das alternativas é feito num artefato antes do gate, como o backlog pediu, e o DS é escrito depois da escolha.

## Perguntas para discutir com o usuário
Todas **em aberto** em 2026-11-04. O layout espera o protótipo no artefato.
1. **Layout (aberta até o protótipo):** (A) uma coluna com seções que se recolhem, na ordem do CA2, ou (B) abas no topo: "Configuração", "Ações", "Tags e gatilhos" e "Níveis e prévia".
2. **Resumo do CA1:** fica no topo, ou a prévia por nível concentra todos os valores numa seção só?
3. **Escopo:** só a habilidade, ou a habilidade e o efeito juntos (o efeito tem o mesmo problema das seções)?

## Tickets (para o layout A; mudam se for o B)
| Ticket | Objetivo | CA | Depende de | Unity |
|---|---|---|---|---|
| IH1 | Seções na ordem do CA2, recolhíveis, com estado guardado; listas de tags compactas. ~300 linhas, ~4 arquivos | CA2, CA5, CA6, CA7 | gate e DS; issues abertas que mexem no `GameplayAbilityEditor` | sim |
| IH2 | Campos por comportamento (CA3) com `ShowIf` no runtime e a prévia junto de cada campo (CA4). ~250 linhas, ~4 arquivos | CA3, CA4, CA6 | IH1 | sim |
| IH3 | Resumo no topo, ligado ao nível de prévia. ~200 linhas, ~3 arquivos | CA1, CA6 | IH2 | sim |

## Validação de uso
| Data | Observação | Ajuste pedido | Status |
|---|---|---|---|
