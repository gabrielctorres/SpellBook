# Changelog

Todas as mudanças relevantes do SpellBook são registradas aqui.
Formato baseado em [Keep a Changelog](https://keepachangelog.com/pt-BR/1.1.0/); versões seguem [SemVer](https://semver.org/lang/pt-BR/) (mudança de versão MAJOR = quebra de compatibilidade).

> **Para projetos que usam o pacote:** ao atualizar o `com.spellbook`, leia as seções **Quebras** e **Migração** da versão nova.

## [Não lançado]

### Quebras
- **Base limitada:** `AttributeState.BaseValue` nunca sai de `[MinValue, MaxValue]`. Quem contava com a "dívida" de vida abaixo de 0 vê as curas valerem na hora. Valor inicial fora do intervalo nasce limitado e gera um aviso no Console, uma vez por atributo.

### Adicionado
- Sample **Sandbox** (Package Manager → SpellBook → Samples): cena 3D que liga GAS, Events, Pooling e Time, com Bola de Fogo, queimadura, canal de dano recebido, pausa e câmera lenta. Entrada pelo Input Manager legado.

## [2.0.0] - 2026-10-14

### Quebras
- **Habilidades precisam ser concedidas ao ASC.** `TryActivateAbility` agora retorna `AbilityActivationResult.NotGranted` para habilidades que não passaram por `GiveAbility` (ou pela lista `startingAbilities` do `AbilitySystemComponent`). Não há erro de compilação; a habilidade simplesmente não ativa.
- `GameplayAbility.Activate()` foi removido; a execução é feita pelo `AbilitySystemComponent`.
- `TryActivateAbility` retorna `AbilityActivationResult` em vez de `void`.
- `TargetingStrategy.GetTargets(Transform, Vector3)` → `GetTargets(AbilitySystemComponent source, Vector3 origin)`. Estratégias customizadas precisam ser atualizadas.
- `ICooldownModifier` removido. `CooldownReductionAttribute` virou campo público (assets antigos migram via `[FormerlySerializedAs]`).
- `MouseTargetingStrategy` passou a respeitar `physicsMode` (antes usava sempre física 3D) e retorna o alvo válido mais próximo.
- Estratégias de targeting não incluem mais o próprio conjurador por padrão (`includeSelf = false`).

### Adicionado
- `AbilitySpec`: estado da habilidade por personagem (nível, se está ativa, contexto atual). API no ASC: `GiveAbility`, `RemoveAbility`, `FindSpec`, `IsAbilityActive`, `Abilities`.
- Ciclo de vida: `CancelAbility`, `CancelAbilitiesWithTags`, `CancelAllAbilities`; desativar o ASC cancela as habilidades ativas.
- `AbilityAction.OnCancel(context)` para restaurar estado quando a habilidade é cancelada no meio de uma ação.
- Novos campos em `GameplayAbility`: `CostEffect`, `ActivationOwnedTags`, `CancelAbilitiesWithTag`, `BlockAbilitiesWithTag`, `CancelledByTags`.
- `AbilityActivationResult` com o motivo da falha; `GameplayAbility.CanActivate(asc, out reason)`.
- Checagem de custo (`ASC.CanAffordCost`) e de cooldown por instância ativa do efeito (`ASC.IsOnCooldown`), não só por tag.
- Eventos no ASC: `OnAbilityActivated`, `OnAbilityEnded`, `OnAbilityFailed`.
- `AbilityContext`: `Ability`, `Spec`, `Level`, `IsCancelled`, `GetValidTargets()`, `StartChildRoutine()`, `CreateDerived()`.
- `AbilityAction.Run` / `RunSequence` para executar sub-ações respeitando cancelamento.
- Ações de fluxo: `RepeatAction`, `ParallelAction`, `ConditionalAction`.
- Filtros de targeting: `includeSelf`, `requiredTargetTags`, `ignoredTargetTags`; `CircleTargeting.maxTargets`.
- `TargetingAction.Mode` (Replace/Append).
- `Projectile.Setup(pool, AbilityContext, onHitActions)`: o impacto herda habilidade e nível.
- `ASC.AddTags` / `RemoveTags`.

### Corrigido
- Efeitos Instant e ticks periódicos ignoravam `Multiply` e `Override` (PlaygroundRumble #13).
- Tags do ASC sem contagem de referência: remover um de dois efeitos com a mesma tag removia a tag (PlaygroundRumble #14).
- `NullReferenceException` com listas nulas em `GameplayEffect`/`GameplayAbility` e modifiers sem atributo (PlaygroundRumble #15).
- `CooldownEffect` sem `GrantedTags` não bloqueava a reativação.
- Targeting contava várias vezes a mesma unidade com vários colliders.
- NRE em `ApplyEffectAction`/`PlayCueAction` quando um alvo era destruído durante a habilidade.
- `SpawnProjectileAction` continuava a rajada após o cancelamento da habilidade.

### Migração
1. Conceder as habilidades ao ASC: `asc.GiveAbility(ability)` ao montar o loadout (e `RemoveAbility` ao trocar), ou preencher `startingAbilities`.
2. Usar o retorno de `TryActivateAbility` e só disparar feedback/UI de ativação quando for `AbilityActivationResult.Success`.
3. Em ações customizadas que alteram estado do personagem (movimento, invencibilidade etc.), sobrescrever `OnCancel` para restaurá-lo. Guardar estado de execução em `context.CustomData`, nunca em campos da ação (a instância é compartilhada).
4. Atualizar `TargetingStrategy` customizadas para a nova assinatura de `GetTargets`.
5. Revisar assets de targeting: marcar `includeSelf` onde o conjurador deve ser alvo.

## [1.3.5] - 2026-05-06
- Última versão antes deste changelog: sistema modular de habilidades (GAS), targeting, projéteis 2D/3D, cooldown, sistema de descrição, EventManager e pooling.
