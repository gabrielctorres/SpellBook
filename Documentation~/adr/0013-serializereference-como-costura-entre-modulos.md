# 0013 — `[SerializeReference]` como costura entre módulos

Status: Aceita (gate 2026-10-29)
Data: 2026-10-29

## Contexto

O pacote já usa `[SerializeReference]` para listas polimórficas: `AbilityAction` (`Runtime/GAS/Abilities/AbilityAction.cs:13`), `EffectAction` (`Runtime/GAS/Effects/EffectAction.cs:8`) e `DescriptionElement` (`Runtime/GAS/Abilities/AbilityDescription.cs:12`). Também usa subclasses de `ScriptableObject` como ponto de extensão (`Formula`, `TargetingStrategy`).

Com a divisão em módulos (0012), o GAS não pode conhecer Cues, Pooling ou Teams, mas o designer precisa, dentro de uma habilidade, escolher uma ação que toca uma cue. Uma classe concreta em `SpellBook.GAS.Bridges` que estende o tipo base do GAS aparece no seletor do Odin sem o GAS saber dela.

## Decisão

1. Pontos de extensão escolhidos no Inspector são **classes abstratas `[Serializable]`** definidas no módulo dono do conceito e guardadas em campos `[SerializeReference]`.
2. As implementações concretas podem morar em qualquer assembly que referencie o dono: o próprio módulo, uma ponte (`SpellBook.GAS.Bridges`) ou o projeto do usuário.
3. O módulo dono nunca lista, procura por nome nem faz `switch` sobre tipos concretos.
4. Regras para tipos concretos:
   - `[Serializable]`, não genéricos, com construtor sem parâmetros;
   - guardam só **configuração**. O estado de uma execução fica no contexto da execução, nunca na instância serializada, porque ela pertence ao asset e é compartilhada por todos os usos;
   - cada entrada é uma instância própria; não se compartilha a mesma referência entre campos;
   - têm metadados para o seletor e o catálogo (categoria e resumo de uma linha, 0007).
5. Critério entre `[SerializeReference]` e `ScriptableObject`:
   - **`[SerializeReference]`** para configuração inline, própria do asset que a contém (uma ação, uma condição, um seletor de alvo);
   - **subclasse de `ScriptableObject`** para algo reutilizado por muitos assets e editado num lugar só (um tema, uma fonte de tempo, um spawner).

## Motivo

- Extensão sem dependência: o núcleo define o contrato, as pontes e o usuário trazem as implementações.
- Configuração inline evita proliferar assets pequenos.
- Estado fora da instância serializada evita bugs de duas entidades compartilhando dados do mesmo asset.

## Consequências

- Mover ou renomear um tipo concreto quebra os assets que o usam. Na 3.0 isso é aceito (0009); depois do `3.0.0`, exige MAJOR e migração (0008).
- Os tipos base precisam ser pensados para extensão de fora do assembly (membros `public` ou `protected` estáveis).

## O que os agentes NÃO podem fazer

- Fazer o módulo dono depender de uma implementação concreta, inclusive via reflexão por nome de tipo.
- Guardar estado de execução (timers, alvos, contadores) em campos de uma instância `[SerializeReference]`.
- Criar tipo concreto genérico, sem `[Serializable]` ou derivado de `UnityEngine.Object` para uso em `[SerializeReference]`.
- Usar serialização do Odin no lugar de `[SerializeReference]` (0011).

## Decisões incorporadas (2026-10-29)

- **Genéricos fechados em `[SerializeReference]`** (decisão técnica, **com verificação no ticket 0M-32**): a Unity passou a serializar instâncias de tipos genéricos fechados em `[SerializeReference]` na linha 2023.1, da qual a 6000.0 descende. A regra fica assim:
  - continua **proibido** oferecer tipo genérico no seletor do Odin (o designer nunca escolhe `Reaction<int>` numa lista);
  - é **permitido** que o código crie uma instância genérica fechada quando o tipo é deduzido de outro campo, sem escolha do designer. Caso único na 0-M: `EventEmission<T>` (canal + payload fixo), criada a partir do tipo de payload do canal escolhido, usada pelo `EventEmitter` e pela `RaiseEventAction` (0016);
  - o red do ticket 0M-32 inclui um teste que serializa e desserializa (`EditorJsonUtility`) um `EventEmitter` com `EventEmission<float>` e com `EventEmission<struct de teste>`. **Se falhar**, o plano B é uma classe concreta por tipo (`FloatEventEmission : EventEmission<float>`), o que faz o payload próprio exigir **três** classes de uma linha em vez de duas (desvio do R14 do PRD, que volta ao usuário).
- **Reações sem genérico na lista** (0016): a lista de reações é `List<Reaction>` com tipos concretos não genéricos; o payload chega por um método genérico (`Execute<T>`), não pelo tipo da instância.

## Em aberto

- `Formula` e `TargetingStrategy`, hoje `ScriptableObject`, migram para `[SerializeReference]` pelo critério do item 5? Isso se decide nas Specs das fases correspondentes.

## Relacionadas

- 0011 (Odin), 0012 (módulos), 0016 (reações de evento), 0017 (fontes de UI), 0018 (ações), 0019 (condições).
