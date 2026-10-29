# 0018 — Hierarquia única de ações (evento → ações → alvos)

Status: Aceita como direção (gate 2026-10-29; seções "Em aberto" detalhadas no épico)
Data: 2026-10-29

## Contexto

Existem duas hierarquias de ação incompatíveis:

- `AbilityAction` (`Runtime/GAS/Abilities/AbilityAction.cs:13-21`): corrotina `IEnumerator Execute(AbilityContext)`, com `OnCancel`. Usada em `GameplayAbility.Actions` (`GameplayAbility.cs:79`), nas ações de fluxo (`FlowActions.cs`) e no impacto do projétil (`Projectile.cs:54`, rodando em `Source.StartCoroutine`, `:317`).
- `EffectAction` (`Runtime/GAS/Effects/EffectAction.cs:8-21`): síncrona, `Execute(source, target, instance)` e `Cleanup`. Usada em `GameplayEffect.OnApplication`, `OnTickActions` e `OnRemoval` (`GameplayEffect.cs:61-72`).

Um efeito não roda ações de habilidade, toda ação usa a mesma lista `context.Targets`, e não há como escolher alvo por ação. A decisão I.8 do usuário: uma hierarquia única, no modelo evento → ações → alvos, usada por habilidades, efeitos, projéteis e thinkers.

## Decisão

1. Uma **única classe base de ação** no `SpellBook.GAS`, escolhida por `[SerializeReference]` (0013). `AbilityAction` e `EffectAction` deixam de existir (0009).
2. **Eventos nomeados:** cada dono (habilidade, efeito, projétil, thinker) expõe pontos de evento, cada um com uma lista de ações. Exemplos: início da habilidade, impacto do projétil, efeito aplicado, tick, efeito removido.
3. **Alvos por ação:** cada ação pode ter um seletor de alvo próprio (`[SerializeReference]`), que escolhe a partir do contexto (quem lançou, alvos do evento, ponto, área com filtros). Sem seletor, a ação usa o alvo padrão do evento.
4. **Uma execução, um contexto:** a ação recebe um contexto de execução com a origem, os alvos do evento, o nível, os dados do efeito (`EffectContext`) e os valores por nível (0020). Estado da execução fica no contexto, nunca na ação (0013).
5. Ações instantâneas e ações que duram (espera, repetição, canalização) seguem o mesmo contrato, com cancelamento. O tempo vem do `ITimeSource` (0014).
6. Ações de fluxo (sequência, paralelo, repetição, condicional, chance, atraso sem bloqueio, para cada alvo) são ações como as outras; as condições usam a hierarquia da 0019.
7. Ações que dependem de outros módulos (cue, spawn, time, evento em canal) ficam em `SpellBook.GAS.Bridges` (0012).
   > Correção de referência (2026-10-29): pelas decisões I22 e I37 e pelas emendas da 0012 e da 0015, ações de spawn e o `PlayCueAction` ficam em `SpellBook.GAS` usando só o `SpawnerAsset` do Core. Vão para o Bridges as ações que usam Events, Pooling, Cues ou Teams ("time" acima é Teams, não o módulo Time, que o GAS referencia).
8. O desenho do Inspector (abas, resumo de uma linha por ação, categorias, seletor de alvo inline) vai para o DS.

## Motivo

- Um efeito, um projétil e uma habilidade passam a ter o mesmo vocabulário, e o designer aprende uma coisa só.
- Alvo por ação elimina a lista de alvos compartilhada e permite "dano no alvo, cura em quem lançou" na mesma lista.
- É o modelo data-driven que permite montar habilidades sem código.

## Consequências

- Todas as ações atuais são reescritas, e os assets que as usam precisam ser refeitos (0009).
- A Fase 7 depende desta ADR; a Fase 8 (thinkers, auras, projéteis) depende da Fase 7.
- Teams entra nos filtros de alvo só via Bridges, porque o GAS não referencia Teams (0012).

## O que os agentes NÃO podem fazer

- Criar uma segunda hierarquia de ação, ou adaptadores entre hierarquias.
- Guardar estado de execução em campos da ação.
- Usar `StartCoroutine`, `WaitForSeconds` ou relógio da Unity para ações que duram (0014).
- Fazer uma ação do `SpellBook.GAS` referenciar Cues, Pooling, Teams ou Events.

## Em aberto

- **Modelo de execução** das ações que duram: um executor próprio, avançado pelo `Tick` do dono com o `ITimeSource` (recomendação do Arquiteto, por causa da 0014 e da testabilidade), ou corrotinas da Unity mantidas?
- **Tipo do contexto:** um contexto único de execução substitui `AbilityContext` e o futuro `EffectContext`, ou o efeito carrega o seu e a ação recebe os dois?
- Lista fechada de eventos por dono (quais eventos a habilidade, o efeito, o projétil e o thinker expõem na 3.0).
- O seletor de alvo por ação convive com a `TargetingStrategy` (hoje `ScriptableObject`) ou a substitui?
- Ações disparadas por efeito (síncronas hoje) podem durar? Se o efeito for removido, a ação em curso é cancelada?

## Relacionadas

- 0009 (quebras), 0012 (módulos), 0013 (costura e estado), 0014 (tempo), 0019 (condições), 0020 (valores por nível), 0024 (dano).
