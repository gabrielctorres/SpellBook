<!-- Página gerada do código por SpellBook.Core.Editor.Documentation. Não edite: mude o [TypeCategory] do tipo e rode SpellBook > Documentação > Gerar referência. -->
# Catálogo de tipos {#catalogo}

Esta página lista os tipos que o seletor do Inspector oferece nos campos de ações, reações, respostas e elementos do pacote. Clique no nome de um tipo para abrir a página dele na referência da interface de programação (API).

## `Condition<ConditionContext>` {#spellbook-core-conditions-condition-spellbook-gas-conditions-conditioncontext}

Aparece em: `Activation Rule > Condition`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [And Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.AndCondition.html) | Lógica | Todas as condições valem. |
| [Attribute Threshold Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.AttributeThresholdCondition.html) | Atributos | Compara um atributo do dono com um valor. |
| [Cast Range Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.CastRangeCondition.html) | Alvos | O ponto-alvo está dentro do alcance da habilidade. |
| [Has Target Point Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.HasTargetPointCondition.html) | Alvos | A ativação tem um ponto-alvo. |
| [Has Targets Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.HasTargetsCondition.html) | Alvos | Há ao menos um alvo. |
| [Not Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.NotCondition.html) | Lógica | A condição não vale. |
| [Or Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.OrCondition.html) | Lógica | Alguma condição vale. |
| [Tag Query Condition](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Conditions.TagQueryCondition.html) | Tags | As tags do dono satisfazem a consulta. |

## `EventResponse` {#spellbook-events-eventresponse}

Aparece em: `Event Listener > Responses`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Ability Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.AbilityEventResponse.html) | Respostas | Roda reações quando uma habilidade é ativada, termina, falha, é concedida ou removida. |
| [Attribute Changed Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.AttributeChangedEventResponse.html) | Respostas | Roda reações quando o valor atual de um atributo muda. |
| [Attribute Limit Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.AttributeLimitEventResponse.html) | Respostas | Roda reações quando um atributo chega ao mínimo ou ao máximo. |
| [Blocked Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.BlockedEventResponse.html) | Respostas | Roda reações quando um ASC informa um bloqueio, com a tag do motivo. |
| [Bool Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.BoolEventResponse.html) | Respostas | Roda reações quando um canal de bool dispara. |
| [Effect Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.EffectEventResponse.html) | Respostas | Roda reações quando um efeito é aplicado, removido, muda de pilhas ou é bloqueado. |
| [Float Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.FloatEventResponse.html) | Respostas | Roda reações quando um canal de float dispara. |
| [Game Object Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.GameObjectEventResponse.html) | Respostas | Roda reações quando um canal de GameObject dispara. |
| [Int Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.IntEventResponse.html) | Respostas | Roda reações quando um canal de int dispara. |
| [String Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.StringEventResponse.html) | Respostas | Roda reações quando um canal de string dispara. |
| [Tag Changed Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.TagChangedEventResponse.html) | Respostas | Roda reações quando uma tag entra no ASC ou sai dele. |
| [Vector 3 Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.Vector3EventResponse.html) | Respostas | Roda reações quando um canal de Vector3 dispara. |
| [Void Event Response](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.VoidEventResponse.html) | Respostas | Roda reações quando um canal sem payload dispara. |

## `Reaction` {#spellbook-events-reaction}

Aparece em: `Delayed Reaction > Reactions`, `Event Response`1 > Reactions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Delayed Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.DelayedReaction.html) | Fluxo | Roda uma lista de reações depois de um atraso. |
| [Log Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.LogReaction.html) | Depuração | Escreve uma mensagem no console. |
| [Raise Channel Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.RaiseChannelReaction.html) | Eventos | Dispara outro canal, repassando ou não o payload. |
| [Set Active Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.SetActiveReaction.html) | Objeto | Ativa ou desativa um GameObject. |
| [Toggle Component Reaction](https://gabrielctorres.github.io/SpellBook/api/SpellBook.Events.ToggleComponentReaction.html) | Objeto | Liga, desliga ou inverte um componente. |

## `AbilityAction` {#spellbook-gas-abilities-abilityaction}

Aparece em: `Conditional Action > Else`, `Conditional Action > Then`, `Gameplay Ability > Actions`, `Parallel Action > Actions`, `Repeat Action > Actions`, `Spawn Projectile Action > On Hit Actions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Animation Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.AnimationAction.html) | Apresentação | Muda um parâmetro do Animator de quem lançou. |
| [Apply Effect Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.ApplyEffectAction.html) | Efeito | Aplica efeitos nos alvos e, se marcado, em quem lançou. |
| [Conditional Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.ConditionalAction.html) | Fluxo | Roda as ações de Then ou de Else conforme a condição. |
| [Parallel Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.ParallelAction.html) | Fluxo | Roda as ações ao mesmo tempo e espera todas terminarem. |
| [Play Cue Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.PlayCueAction.html) | Apresentação | Cria um efeito visual e toca um som em quem lançou, no alvo ou no ponto. |
| [Raise Event Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Bridges.RaiseEventAction.html) | Eventos | Dispara um canal de eventos com um payload fixo ou da habilidade. |
| [Repeat Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.RepeatAction.html) | Fluxo | Repete as ações várias vezes, com intervalo opcional. |
| [Spawn Projectile Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.SpawnProjectileAction.html) | Mundo | Lança projéteis que rodam ações ao acertar. |
| [Targeting Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.TargetingAction.html) | Alvo | Busca alvos com uma estratégia e troca ou soma aos alvos atuais. |
| [Wait Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.Actions.WaitAction.html) | Fluxo | Espera alguns segundos no tempo do sistema de habilidades do ator. |

## `DescriptionElement` {#spellbook-gas-abilities-descriptionelement}

Aparece em: `Ability Description > Elements`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Attribute Element](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.AttributeElement.html) | Descrição | Valor de um atributo de quem lançou, com cor. |
| [Template Element](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.TemplateElement.html) | Descrição | Modelo com marcadores trocados por atributos ou texto. |
| [Text Element](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Abilities.TextElement.html) | Descrição | Texto fixo. |

## `AttributeChangeProcessor` {#spellbook-gas-attributes-attributechangeprocessor}

Aparece em: `Attribute Definition > Processors`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Absorb With Attribute](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Attributes.AbsorbWithAttribute.html) | Dano/Escudo | Desvia o dano para um atributo de escudo do dono. |
| [Immune To Tags](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Attributes.ImmuneToTags.html) | Dano/Imunidade | Anula a alteração de efeitos com certas tags de dano. |
| [Reduce By Attribute](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Attributes.ReduceByAttribute.html) | Dano/Redução | Reduz a alteração pelo valor de um atributo do dono. |

## `EffectAction` {#spellbook-gas-effects-effectaction}

Aparece em: `Gameplay Effect > On Application`, `Gameplay Effect > On Removal`, `Gameplay Effect > On Tick Actions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| [Debug Log Effect Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Effects.DebugLogEffectAction.html) | Depuração | Escreve uma mensagem colorida no Console quando o efeito roda. |
| [Spawn VFX Effect Action](https://gabrielctorres.github.io/SpellBook/api/SpellBook.GAS.Effects.SpawnVFXEffectAction.html) | Apresentação | Cria um efeito visual no alvo e, se marcado, o remove quando o efeito sai. |
