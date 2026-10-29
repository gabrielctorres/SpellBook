# Backlog do SpellBook

Esta é a porta de entrada de todo trabalho. Quem cuida dele é o Controlador.

Uma ideia, um bug ou uma pendência de documentação entra aqui primeiro. Só vira trabalho quando passa por uma Spec (via completa) ou pela via rápida.

**Estados:**
- `bruta`: ainda é só uma ideia.
- `cristalizada`: o escopo já está claro.
- `em pesquisa`: o PRD ou a Spec estão sendo escritos.
- `Spec aprovada`: passou pelo gate.
- `em execução`: os tickets viraram issues.
- `feito`.

**Vias:**
- `rápida`: um ticket só, sem PRD.
- `completa`: passa por PRD, Spec e ADRs.

A cada três tickets fechados, o Controlador propõe uma faxina: promover, fundir ou descartar itens.

O plano de origem está em [planos/gas-estudo-e-roadmap.md](planos/gas-estudo-e-roadmap.md) (seções E, H e I).

| ID | Data | Origem | Descrição | Estado | Via |
|---|---|---|---|---|---|
| B-001 | 2026-10-29 | usuário | **Fase 0-M, modularização:** asmdefs por módulo (Core, Time, Events, Pooling, Tags, GAS, GAS.Bridges, Editor, Tests), Unity 6000.0, regra do Odin, módulo Time (`ITimeSource`), Events unificado com Event Debugger, `SpawnerAsset`, asmdefs de teste, validadores + teste EditMode, `[HelpURL]`, esqueleto do MkDocs, mover `GAS_Documentation.md` | Spec aprovada | completa |
| B-002 | 2026-10-29 | estudo | **Correções do núcleo do GAS:** limitar o valor base, `RemoveEffect` idempotente, refresh com a duração aplicada, cooldown normalizado, match de tag no cooldown, `Recalculate` sem LINQ, `CreateDerived` copiar `CustomData`, NRE do `PoolManager` | cristalizada | completa |
| B-003 | 2026-10-29 | usuário | **Fase 1, efeitos ativos e recarga controláveis:** lista pública, Find/Remove por tag, Extend/Reduce/Restart, eventos aplicado/removido/pilhas, `ReduceCooldown`/`ResetCooldown`, `CooldownReductionPolicy`, `TryGetValue`/`GetValueOrDefault`, `OnAbilityGranted/Removed`, GAS Debugger v1 (itens 3, 11, 12) | cristalizada | completa |
| B-004 | 2026-10-29 | usuário | **Resultado de aplicação e relay:** `TryApplyEffect`/`EffectApplicationResult`, `OnEffectBlocked`, `AbilitySystemEventRelay`, `RaiseEventEffectAction` e canais do GAS em `GAS.Bridges` (itens 9 e 10). A `RaiseEventAction` foi para a 0-M (decisão I22) | cristalizada | completa |
| B-005 | 2026-10-29 | usuário | **Fase 2, atributos ricos:** `AttributeDisplay` + Format, `TextRoleTheme`, `MultiplyCompound`/`AddFinal`, Max/Min por atributo com política, `OnDepleted`/`OnReachedMax`, `startingEffects`, metadados IsBuff/IsDebuff/IsHidden/IsPurgable (itens 1, 6, 8, 13) | cristalizada | completa |
| B-006 | 2026-10-29 | estudo | **Tags avançadas:** `GameplayTagContainer`, `GameplayTagQuery` (all/any/none), `GetDefinition` público | cristalizada | completa |
| B-007 | 2026-10-29 | usuário | **Fase 3, valores por nível e EffectSpec:** `ScalableFloat` (array ou curva), `AbilityValues` com `%nome`, `EffectContext`, SetByCaller, magnitude snapshot ou ao vivo, pilhas multiplicando a magnitude, MaxLevel e `OnLevelChanged` | cristalizada | completa |
| B-008 | 2026-10-29 | usuário | **Fase 4, descrição estruturada:** `EffectDescriber`, `IModifierTextFormatter`, `DescriptionContext`, `ITextProvider` + adaptador opcional do Unity Localization, elementos de custo, recarga, duração e cargas (itens 2 e 14) | cristalizada | completa |
| B-009 | 2026-10-29 | usuário | **Fase 5, ativação avançada:** `ActivationConditions` (hierarquia `Condition` com And/Or/Not), `ActivationFailure` com motivo, ordem tags → cargas/recarga → custo → condições, cargas com recarga sequencial (itens 4 e 5) | cristalizada | completa |
| B-010 | 2026-10-29 | usuário | **Fase 6, dano, escudo e morte:** processadores de pré-alteração, meta-atributo de dano, absorção, `OnDepleted` como morte, tipos de dano por tag (item 7) | cristalizada | completa |
| B-011 | 2026-10-29 | usuário | **Módulo Teams:** `TeamDefinition`, `TeamRelationMatrix`, `TeamMember`, condições e filtros de time | cristalizada | completa |
| B-012 | 2026-10-29 | usuário | **Fase 7, modelo evento → ações → alvos:** ADR da hierarquia única de ações, `TargetSelector` por ação, ações Chance/Delayed/ForEach/RemoveEffect, `SendGameplayEvent` e gatilhos, Passive/Toggle/Channel, alcance de cast, ativação com unidade-alvo, commit explícito, abas do Inspector | cristalizada | completa |
| B-013 | 2026-10-29 | usuário | **Módulo Cues:** `CueDefinition` por tag, roteador, `CueParameters`, VFX e áudio em pool | cristalizada | completa |
| B-014 | 2026-10-29 | usuário | **Fase 8, thinkers, auras e projéteis:** `GameplayThinker` em pool, `CreateThinkerAction`, aura, projétil com pierce, finish e filtro de time | cristalizada | completa |
| B-015 | 2026-10-29 | usuário | **UI-1:** `SpellBook.UI` (contratos), `UI.UGUI`, `GAS.UI`: barra e texto de atributo, indicador de tag, números flutuantes, `RuntimeAnchor`/`ActorResolver`, prévia em edição | cristalizada | completa |
| B-016 | 2026-10-29 | usuário | **UI-2:** slot e barra de habilidades por `AbilityLoadout`, recarga, cargas e motivo de falha | cristalizada | completa |
| B-017 | 2026-10-29 | usuário | **UI-3:** tooltip e descrição estruturada | cristalizada | completa |
| B-018 | 2026-10-29 | usuário | **UI-4:** lista de buffs e debuffs com ícone, duração e pilhas | cristalizada | completa |
| B-019 | 2026-10-29 | usuário | **Fase 9, ferramentas:** wizard de templates, tabela de balanceamento, simulador de efeito, overlay de debug, wizard "Create Event Type", `Samples~`, referência de API (DocFX) | bruta | completa |
| B-020 | 2026-10-29 | Controlador | Ajustar a skill `/issue` para criar issues no repositório do SpellBook (hoje aponta para o PlaygroundRumble) | Spec aprovada (tickets 0M-01–0M-04) | rápida |
| B-021 | 2026-10-29 | Controlador | Adaptar os agentes recém-instalados (`gameplay-programmer` etc.) ao fluxo do `TIME.md`: tirar Write/Edit de quem só propõe e remover as referências a `docs/engine-reference`, `docs/architecture` e `lead-programmer` | Spec aprovada (tickets 0M-01–0M-04) | rápida |
| B-022 | 2026-10-29 | Controlador | Atualizar `TIME.md` e `CLAUDE.md` com o fluxo novo (backlog, `spec/`, gate único, issues no repositório do SpellBook, PRD na visão de quem usa o pacote) | Spec aprovada (tickets 0M-01–0M-04) | rápida |
