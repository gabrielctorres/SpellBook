# Catálogo de tipos

Esta página lista os tipos que o seletor do Inspector oferece nos campos de ações, reações, respostas e elementos do pacote.

## `Condition<ConditionContext>`

Aparece em: `Activation Rule > Condition`, `Area Selector > Filter`, `Conditional Action > Condition`, `Projectile > Impact Filter`.

| Tipo | Categoria | Resumo |
|---|---|---|
| And Condition | Lógica | Todas as condições valem. |
| Attribute Threshold Condition | Atributos | Compara um atributo do dono com um valor. |
| Cast Range Condition | Alvos | O alvo está dentro do alcance da habilidade. |
| Has Target Point Condition | Alvos | A ativação tem um ponto-alvo. |
| Has Targets Condition | Alvos | Há ao menos um alvo. |
| Not Condition | Lógica | A condição não vale. |
| Or Condition | Lógica | Alguma condição vale. |
| Tag Query Condition | Tags | As tags do dono ou da unidade-alvo satisfazem a consulta. |
| Team Relation Condition | Time | A relação de time com a unidade-alvo está na máscara. |

## `CueItem`

Aparece em: `Cue Definition > Items`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Sound Cue Item | Apresentação | Toca um clipe num objeto de áudio do spawner e o devolve no fim do clipe. |
| Vfx Cue Item | Apresentação | Cria um prefab de VFX no alvo ou no ponto do cue e o devolve ao spawner no fim. |

## `SoundCueItem`

Aparece em: `Cue Definition > Sounds`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Sound Cue Item | Apresentação | Toca um clipe num objeto de áudio do spawner e o devolve no fim do clipe. |

## `EventResponse`

Aparece em: `Event Listener > Responses`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Ability Event Response | Respostas | Roda reações quando uma habilidade é ativada, termina, falha, é concedida ou removida. |
| Attribute Changed Event Response | Respostas | Roda reações quando o valor atual de um atributo muda. |
| Attribute Limit Event Response | Respostas | Roda reações quando um atributo chega ao mínimo ou ao máximo. |
| Blocked Event Response | Respostas | Roda reações quando um ASC informa um bloqueio, com a tag do motivo. |
| Bool Event Response | Respostas | Roda reações quando um canal de bool dispara. |
| Effect Event Response | Respostas | Roda reações quando um efeito é aplicado, removido, muda de pilhas ou é bloqueado. |
| Float Event Response | Respostas | Roda reações quando um canal de float dispara. |
| Game Object Event Response | Respostas | Roda reações quando um canal de GameObject dispara. |
| Int Event Response | Respostas | Roda reações quando um canal de int dispara. |
| String Event Response | Respostas | Roda reações quando um canal de string dispara. |
| Tag Changed Event Response | Respostas | Roda reações quando uma tag entra no ASC ou sai dele. |
| Vector 3 Event Response | Respostas | Roda reações quando um canal de Vector3 dispara. |
| Void Event Response | Respostas | Roda reações quando um canal sem payload dispara. |

## `Reaction`

Aparece em: `Delayed Reaction > Reactions`, `Event Response`1 > Reactions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Delayed Reaction | Fluxo | Roda uma lista de reações depois de um atraso. |
| Log Reaction | Depuração | Escreve uma mensagem no console. |
| Raise Channel Reaction | Eventos | Dispara outro canal, repassando ou não o payload. |
| Set Active Reaction | Objeto | Ativa ou desativa um GameObject. |
| Toggle Component Reaction | Objeto | Liga, desliga ou inverte um componente. |

## `AbilityTrigger`

Aparece em: `Gameplay Ability > Triggers`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Gameplay Event Trigger | Evento | Ativa a habilidade quando o ASC recebe um evento de gameplay. |
| Tag Added Trigger | Tags | Ativa a habilidade quando o ASC ganha uma tag. |
| Tag Removed Trigger | Tags | Ativa a habilidade quando o ASC perde uma tag. |
| When Ready Trigger | Autocast | Ativa a habilidade sozinha quando ela fica pronta. |

## `GameplayAction`

Aparece em: `Chance Action > Actions`, `Conditional Action > Else`, `Conditional Action > Then`, `Delayed Action > Actions`, `For Each Target Action > Actions`, `Gameplay Ability > On Activate`, `Gameplay Ability > On Cancel`, `Gameplay Ability > On Channel Tick`, `Gameplay Ability > On End`, `Gameplay Ability > On Granted`, `Gameplay Ability > On Removed`, `Gameplay Effect > On Ability Executed`, `Gameplay Effect > On Applied`, `Gameplay Effect > On Death`, `Gameplay Effect > On Removed`, `Gameplay Effect > On Take Damage`, `Gameplay Effect > On Tick`, `Gameplay Thinker > On Created`, `Gameplay Thinker > On Ended`, `Gameplay Thinker > On Pulse`, `Parallel Action > Actions`, `Projectile > On Ended`, `Repeat Action > Actions`, `Spawn Projectile Action > On Hit Actions`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Add Cue Action | Apresentação | Liga um cue contínuo que some quando o efeito sai ou a ativação termina. |
| Animation Action | Apresentação | Muda um parâmetro do Animator de quem lançou. |
| Apply Effect Action | Efeito | Aplica efeitos nos alvos e, se marcado, em quem lançou. |
| Chance Action | Fluxo | Roda as ações com uma chance, sorteada no ASC. |
| Commit Action | Efeito | Cobra a recarga, o custo e a carga da habilidade. |
| Conditional Action | Fluxo | Roda as ações de Then ou de Else conforme a condição. |
| Create Thinker Action | Mundo | Cria um thinker no mundo, ligado a quem o criou. |
| Debug Log Action | Depuração | Escreve uma mensagem colorida no Console quando o efeito roda. |
| Delayed Action | Fluxo | Roda as ações depois de um tempo, sem esperar por elas. |
| Execute Cue Action | Apresentação | Toca um cue de explosão em cada alvo, pelo roteador da âncora. |
| For Each Target Action | Fluxo | Roda as ações uma vez por alvo, com o alvo corrente como padrão. |
| Parallel Action | Fluxo | Roda as ações ao mesmo tempo e espera todas terminarem. |
| Raise Event Action | Eventos | Dispara um canal de eventos com um payload fixo, da habilidade ou do efeito. |
| Remove Effect Action | Efeito | Remove dos alvos um efeito, ou os efeitos que concedem uma tag. |
| Repeat Action | Fluxo | Repete as ações várias vezes, com intervalo opcional. |
| Send Gameplay Event Action | Evento | Envia um evento de gameplay ao ASC de cada alvo. |
| Spawn Projectile Action | Mundo | Lança projéteis que rodam ações ao acertar. |
| Targeting Action | Alvo | Troca ou soma aos alvos do evento os alvos que o seletor escolheu. |
| Wait Action | Fluxo | Espera alguns segundos no tempo do sistema de habilidades do ator. |

## `AttributeChangeProcessor`

Aparece em: `Attribute Definition > Processors`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Absorb With Attribute | Dano/Escudo | Desvia o dano para um atributo de escudo do dono. |
| Immune To Tags | Dano/Imunidade | Anula a alteração de efeitos com certas tags de dano. |
| Reduce By Attribute | Dano/Redução | Reduz a alteração pelo valor de um atributo do dono. |

## `DescriptionElement`

Aparece em: `Ability Description > Elements`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Attribute Element | Descrição/Atributo | Valor atual de um atributo de quem lança. |
| Charges Element | Descrição/Cargas | Máximo de cargas e a recarga de cada uma, no nível. |
| Cooldown Element | Descrição/Recarga | Recarga da habilidade no nível, com a redução de quem lança. |
| Cost Element | Descrição/Custo | Custo da habilidade: uma linha por atributo gasto, no nível. |
| Duration Element | Descrição/Duração | Duração de um efeito no nível, ou permanente. |
| Effect Modifiers Element | Descrição/Modificadores do efeito | Uma linha por modificador de um efeito. |
| Template Element | Descrição/Template | Texto com valores da tabela por nível e atalhos de recarga, custo e cargas. |
| Text Element | Descrição/Texto | Uma linha de texto, com chave opcional. |

## `ModifierTextFormatter`

Aparece em: `Ability Tooltip Source > Formatter`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Default Modifier Text Formatter | Descrição | Formato do atributo: texto do modificador com a operação, ou o valor. |

## `AreaSelector`

Aparece em: `Aura Behaviour > Area`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Area Selector | Alvo | Escolhe as unidades num raio, as mais perto ou sorteadas. |

## `TargetSelector`

Aparece em: `Gameplay Action > Target`, `When Ready Trigger > Selector`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Area Selector | Alvo | Escolhe as unidades num raio, as mais perto ou sorteadas. |
| Caster Selector | Alvo | Escolhe quem lançou a habilidade ou aplicou o efeito. |
| Event Targets Selector | Alvo | Escolhe os alvos do evento que disparou a lista. |
| Point Selector | Alvo | Não escolhe unidade; a ação usa o ponto do evento. |

## `ThinkerBehaviour`

Aparece em: `Gameplay Thinker > Behaviour`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Aura Behaviour | Mundo | Aplica um efeito em quem está no raio e o remove quando sai. |

## `UISource<TooltipModel>`

Aparece em: `Tooltip Trigger > Source`.

| Tipo | Categoria | Resumo |
|---|---|---|
| Ability Tooltip Source | Tooltip | Tooltip de uma habilidade: descrição, custo, recarga e cargas no nível do dono, ou no nível configurado sem dono. |
| Attribute Tooltip Source | Tooltip | Tooltip de um atributo do dono: valor atual no formato do atributo e, opcional, a base e os modificadores. |
| Effect Tooltip Source | Tooltip | Tooltip de um efeito: modificadores, pilhas e tempo restante no dono, ou só o asset sem instância ativa. |
