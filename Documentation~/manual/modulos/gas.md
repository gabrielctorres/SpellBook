# GAS {#gas}

O GAS (Gameplay Ability System) é o sistema modular e orientado a dados do SpellBook para
habilidades, efeitos e atributos. Esta página é o guia de uso.

O GAS fica no assembly `SpellBook.GAS`. Um asmdef seu que usa o GAS precisa referenciar o
`SpellBook.GAS` (e o `SpellBook.Core`, se usar tipos do Core). Veja [Conceitos](../inicio/conceitos.md#modulos).

## 1. Visão geral {#visao-geral}

### 1.1 Tags (GameplayTags) {#tags}
- **O que são:** identificadores hierárquicos (ex: `State.Stunned`, `Ability.Fireball.Cooldown`).
- **Uso:** controle de fluxo. Uma habilidade pode ser bloqueada se o personagem tiver a tag `State.Stunned`.

### 1.2 Atributos {#attributeset}
- **O que são:** valores como Vida, Mana e Velocidade, agrupados em um `AttributeSet`.

#### AttributeDefinition {#attributedefinition}
Cada valor tem uma `AttributeDefinition`.

- **Limites:** `MinValue` e `MaxValue` valem para o valor base e para o valor atual. O valor base nunca sai do intervalo: um dano maior que a vida deixa a base em `MinValue`, e a cura seguinte conta inteira. Isso vale para efeitos instantâneos, tiques periódicos e `SetBaseValue`.
- **Valor inicial fora do intervalo:** o `AttributeSet` limita o valor inicial ao intervalo e registra um aviso no Console, uma vez por atributo, com o nome do atributo e do objeto. Revise o asset ou o `startingAttributes` apontado pelo aviso.

#### Fórmulas de modificadores {#formulas}
Os atributos mantêm um histórico de modificadores (Add, Multiply, Override), permitindo cálculos precisos de bônus e penalidades.

Uma fórmula calcula um valor a partir dos atributos de quem aplica (Source) e de quem recebe (Target). Ela serve no `Formula` de um atributo `Derived` e na fonte `Formula` de um modificador.

#### SimpleAttributeFormula {#simpleattributeformula}
Um valor base mais a soma de atributos multiplicados (`Base Value` + `Attribute` × `Multiplier`). Crie em **Create → GAS → Formulas → Simple Attribute Formula**.

#### ExpressionFormula {#expressionformula}
Uma expressão em texto, como `([ATK] * 2) / [DEF]`. Cada token entre colchetes é ligado a um atributo da Source ou da Target em `Mappings`. Crie em **Create → GAS → Formulas → Expression Formula**.

### 1.3 Gameplay Effects (GE) {#gameplayeffect}
- **Instant:** modifica o valor base (ex: Dano, Cura).
- **Duration/Infinite:** adiciona um modificador temporário ou permanente e pode conceder Tags enquanto ativo.
- **Stacking:** suporta acúmulo de instâncias (ex: Veneno que acumula até 5 vezes).

### 1.4 Gameplay Abilities (GA) {#gameplayability}
As habilidades são compostas **dentro do próprio arquivo da habilidade**, como uma lista de ações.
Não é preciso criar ScriptableObjects separados para cada ação.

### 1.5 AbilitySystemComponent (ASC) {#abilitysystemcomponent}
O `AbilitySystemComponent` é o componente que guarda as habilidades concedidas, os atributos,
os efeitos ativos e as tags de um personagem. Veja a seção [Ciclo de vida](#ciclo-de-vida).

---

## 2. Guia de criação de habilidades {#criacao}

### Passo a passo
1. Clique com o botão direito no Project → **Create → GAS → Gameplay Ability**.
2. No Inspector, localize a lista **Actions**.
3. Clique no botão **+** e selecione a ação desejada (ex: `WaitAction`, `TargetingAction`).
4. Configure os parâmetros da ação diretamente ali.

### Exemplo: habilidade de área (explosão)
1. **Action 0 (AnimationAction):** Trigger: "Attack".
2. **Action 1 (WaitAction):** Duration: 0.5s.
3. **Action 2 (TargetingAction):** Strategy: (arraste um TS_Circle).
4. **Action 3 (PlayCueAction):** VFX_Prefab: (Explosão), SFX_Clip: (Explosão).
5. **Action 4 (ApplyEffectAction):** Effects: (arraste um GE_Damage).

---

## 3. Lista de ações disponíveis {#acoes}
- **WaitAction:** pausa a execução por X segundos do tempo do ASC (veja [Ações que esperam](#acoes-que-esperam)).
- **TargetingAction:** busca alvos usando uma estratégia (Raio, Mouse...). `Mode` Replace substitui os alvos atuais, Append adiciona.
- **ApplyEffectAction:** aplica GameplayEffects aos alvos ou ao conjurador.
- **PlayCueAction:** dispara efeitos visuais e sonoros (veja [Ações de spawn](#acoes-de-spawn)).
- **AnimationAction:** altera parâmetros (Trigger, Bool, Int) no Animator do personagem.
- **SpawnProjectileAction:** dispara projéteis (detalhes em [Projéteis](#projectile)).
- **RepeatAction:** repete uma lista de ações N vezes, com intervalo opcional.
- **ParallelAction:** executa várias ações ao mesmo tempo e espera todas terminarem.
- **ConditionalAction:** executa `Then` ou `Else` conforme tags do conjurador ou existência de alvos.
- **RaiseEventAction:** dispara um canal de eventos (detalhes em [RaiseEventAction](#raiseeventaction)).

#### Ações que esperam {#acoes-que-esperam}
O `Duration` da `WaitAction`, o `Interval` da `RepeatAction` e o `DelayBetweenShots` da `SpawnProjectileAction` contam o tempo do ASC que conjurou, o [`TickedTime`](#tempo-do-asc), e não o relógio da Unity.

- Com a fonte padrão (tempo escalado), a espera dura o mesmo que na 2.x.
- Se o ASC usa um [grupo pausável](tempo.md#pausabletimesource), pausar o grupo pausa a habilidade no meio da espera. Ao retomar, ela continua de onde parou.
- Com `autoTick` desligado, a espera só anda quando alguém chama `Tick`.
- Se o ASC for destruído, a espera termina.
- **Início do `Interval`:** o `Interval` da `RepeatAction` começa a contar no quadro seguinte ao fim de cada repetição. Um `Tick` no mesmo quadro em que a repetição termina não entra no intervalo. O `DelayBetweenShots` começa a contar logo depois de cada tiro. Com `autoTick` ligado, a diferença é de no máximo um quadro.
- **Wait 0:** uma `WaitAction` com `Duration` zero ou negativo espera exatamente um quadro, como na 2.x, e não lê o tempo. Serve para passar a lista de ações ao quadro seguinte e termina mesmo com o ASC parado.

Numa ação própria, espere com `yield return context.WaitSeconds(segundos)`, e não com `WaitForSeconds`.

#### Projéteis {#projectile}
A `SpawnProjectileAction` dispara projéteis: cria o `ProjectilePrefab` pelo campo `Spawner` e chama `Setup` no componente `Projectile`. As `OnHitActions` rodam no impacto com um contexto derivado (mesma habilidade e nível).

- **`Setup(SpawnerAsset spawner, AbilityContext parentContext, List<AbilityAction> onHitActions)`** guarda o spawner que criou o projétil. Há uma sobrecarga com `AbilitySystemComponent` no lugar do contexto.
- **`ReturnToPool()`** devolve o projétil por esse mesmo spawner (`spawner.Release`). Sem spawner, ele volta pelo `SpawnerAsset.Default`, que destrói a instância.
- O `Projectile` reinicia o estado no `OnEnable`, que roda tanto no `Instantiate` quanto ao sair de um pool. Ele não implementa mais o `IPoolable`.

#### Ações de spawn {#acoes-de-spawn}
`SpawnProjectileAction`, `PlayCueAction` e `SpawnVFXEffectAction` (ação de efeito) criam objetos só pelo campo **`Spawner`**, um [`SpawnerAsset`](pooling.md#spawnerasset). Vazio usa o spawner padrão, por `Instantiate`. Elas não procuram nenhum `PoolManager` na cena, então funcionam numa cena sem ele.

- **`SpawnProjectileAction`:** o projétil volta pelo mesmo spawner ([Projéteis](#projectile)).
- **`SpawnVFXEffectAction`:** com `DestroyOnRemoval`, o VFX volta pelo spawner quando o efeito é removido. Sem `DestroyOnRemoval`, um VFX com `ParticleSystem` na raiz volta quando a partícula para ([`ReleaseWhenParticlesStop`](pooling.md#releasewhenparticlesstop)).
- **`PlayCueAction`:** cria o VFX pelo spawner e toca o som com `AudioSource.PlayClipAtPoint`.

!!! warning "Armadilha: o VFX do `PlayCueAction` não volta"
    O `PlayCueAction` não devolve o VFX ao spawner. Com o padrão por `Instantiate`, o VFX precisa se destruir sozinho (por exemplo, `Stop Action` = `Destroy` na partícula). Com um spawner de pool, o pool cresce a cada uso. Isso continua assim até o módulo Cues.

#### RaiseEventAction {#raiseeventaction}
A `RaiseEventAction` dispara um [canal de eventos](eventos.md) quando a habilidade chega nela. Ela fica no assembly `SpellBook.GAS.Bridges`, que liga o GAS ao módulo de Eventos, e aparece no seletor de ações na categoria **Eventos**.

A ação guarda uma **emissão**, o mesmo campo do [`EventEmitter`](eventos.md#eventemitter): o canal e o payload fixo.

- **Canal sem payload** (Void): a ação só dispara o canal.
- **Canal com payload** (`EventChannel<T>`, como Int ou um payload próprio): a ação envia o valor fixo do campo **Payload**. Trocar o canal por um de outro tipo recria a emissão, e o payload volta ao valor padrão.
- Cada passagem pela ação dispara o canal uma vez, e a ação termina no mesmo quadro. O ASC que conjurou aparece como quem disparou no log do canal.
- Sem emissão ou sem canal, a ação não faz nada, e o validador acusa o erro na habilidade.

O payload é sempre o valor fixo do Inspector. Um payload calculado na hora (o alvo, o dano causado) ainda não existe.

### 3.1 Criando ações próprias {#acoes-proprias}
Herde de `AbilityAction` e implemente `IEnumerator Execute(AbilityContext context)`.

- A mesma instância da ação é compartilhada por todos que usam a habilidade: guarde estado de execução no `context.CustomData`, nunca em campos.
- Sobrescreva `OnCancel(AbilityContext context)` para restaurar estado se a habilidade for cancelada no meio da ação (ex: reabilitar movimento após um dash).
- Para rodar sub-ações use `AbilityAction.RunSequence(lista, context)`; para corrotinas paralelas use `context.StartChildRoutine(...)`, que é interrompida junto no cancelamento.
- Use `context.GetValidTargets()` em vez de `context.Targets` quando alvos podem ter sido destruídos durante a habilidade.
- Para esperar, use `yield return context.WaitSeconds(segundos)`, que conta o tempo do ASC ([Ações que esperam](#acoes-que-esperam)).
- Para acusar uma configuração inválida, sobrescreva `Validate(ValidationResult result)` e chame `result.AddError(...)`. O `GameplayAbility` repassa a validação a cada ação da lista.

### 3.2 Targeting {#targeting}
`TargetingStrategy` respeita `physicsMode` (2D/3D), procura o `AbilitySystemComponent` no collider ou nos pais (sem duplicatas) e filtra por:

- `includeSelf` (padrão: falso);
- `requiredTargetTags` / `ignoredTargetTags` (ex: ignorar a tag do próprio time).

#### CircleTargeting {#circletargeting}
Os alvos num raio em volta da origem. Aceita `maxTargets` (prioriza os mais próximos). Crie em **Create → GAS → Targeting → Circle**.

#### MouseTargetingStrategy {#mousetargetingstrategy}
O alvo válido mais próximo do ponto mirado, num raio pequeno. Crie em **Create → GAS → Targeting → Mouse**.

---

## 4. Ciclo de vida, cooldown e custo {#ciclo-de-vida}

### 4.1 Concedendo habilidades
O ASC só ativa habilidades concedidas. Use a lista `startingAbilities` no Inspector ou, via código, `asc.GiveAbility(ability, level)` / `asc.RemoveAbility(ability)`. Cada habilidade concedida vira um `AbilitySpec` (nível, se está ativa e o contexto da execução atual).

### 4.2 Ativação
`asc.TryActivateAbility(ability, targetPos)` retorna um `AbilityActivationResult`. Em ordem, as checagens são:

1. concedida (`NotGranted`) e não ativa (`AlreadyActive`);
2. `BlockedTags` (`BlockedByTags`) e `RequiredTags` (`MissingRequiredTags`);
3. `BlockAbilitiesWithTag` de habilidades ativas (`BlockedByActiveAbility`);
4. cooldown (`OnCooldown`), pelas tags do `CooldownEffect` **ou** por uma instância ativa dele;
5. custo (`CannotAffordCost`).

Em caso de sucesso, o ASC:

- aplica o `CooldownEffect` (duração reduzida por `CooldownReductionAttribute`) e o `CostEffect`;
- cancela as habilidades cujo `AbilityTag` está em `CancelAbilitiesWithTag`;
- concede as `ActivationOwnedTags` enquanto a habilidade roda.

Para consultar sem ativar, use `ability.CanActivate(asc, out var reason)`.

### 4.3 Custo
`CostEffect` é um GameplayEffect Instant, normalmente com modifier `Add` negativo (ex: `Mana Add -20`). A ativação falha se algum atributo ficaria abaixo do `MinValue`.

### 4.4 Cancelamento
- `asc.CancelAbility(ability)` interrompe a habilidade e chama `OnCancel` nas ações em execução.
- `CancelledByTags`: a habilidade é cancelada quando o dono recebe uma destas tags (ex: `State.Stunned`).
- Desativar o GameObject do ASC cancela todas as habilidades ativas.

### 4.5 Eventos do ASC
`OnAbilityActivated(spec)`, `OnAbilityEnded(spec, cancelled)` e `OnAbilityFailed(ability, result)`, este último útil para feedback de UI ("Sem mana", "Em recarga").
O ASC não dispara mais evento global; veja a [Migração 2.x → 3.0](../regras/migracao-2-para-3.md#eventos-do-gas).

### 4.6 Tempo do ASC {#tempo-do-asc}
Durações de efeitos, ticks periódicos e recargas contam só o tempo que o ASC recebe. O ASC não lê o `Time` da Unity.

- **`timeSource`** (Inspector): um `TimeSourceAsset` do módulo [Tempo](tempo.md). Vazio usa o tempo escalado da Unity, como na 2.x.
- **`autoTick`** (Inspector, ligado por padrão): o ASC avança sozinho no `Update` pela diferença do `ElapsedTime` da fonte ([consumo por diferença](tempo.md#consumo-por-diferenca)). Desligado, ele só avança quando alguém chama `Tick`.
- **`Tick(float deltaTime)`**: avança efeitos e recargas em `deltaTime` segundos. Delta zero ou negativo é ignorado.
- **`TickedTime`**: o relógio do ASC, a soma de todos os `Tick` aceitos.
- **`SetTimeSource(ITimeSource)`**: troca a fonte por código, por exemplo para um `ManualTimeSource` num teste. `null` volta à fonte do Inspector. O tempo que passou na fonte antiga não é contado.

Com `autoTick` desligado, a mesma sequência de `Tick` sempre dá o mesmo estado, o que serve para testes e para simulação.

```csharp
asc.SetTimeSource(manualTime); // com autoTick desligado no Inspector
asc.ApplyEffect(burning);      // efeito de 2 s
asc.Tick(1.9f);                // ainda ativo
asc.Tick(0.2f);                // expirou
```

**Receita: pausar a gameplay e manter a UI.** Crie um grupo pausável pelo menu *SpellBook > Time > Pausable Time Group*, chame-o de "Gameplay" e coloque-o no `timeSource` dos ASCs dos personagens. Ao chamar `Pause` no grupo, as recargas e as durações desses ASCs param. Um ASC com fonte não escalada e o resto da cena continuam. Ao chamar `Resume`, tudo volta de onde parou. Veja [Grupo pausável](tempo.md#pausabletimesource).

O `Projectile` também tem um campo `timeSource`. Ele conta o movimento, o `Lifetime` e a `InitialInvulnerabilityDuration` no `FixedUpdate`, e não o relógio da Unity.

!!! warning "Armadilha: somar floats na fronteira"
    Um efeito expira quando o tempo restante fica menor ou igual a 0. Em `float`, `2f - 1.9f - 0.1f` dá cerca de `+2,2e-8`, um valor positivo, e não 0. Por isso uma recarga de 2 s **não** expira depois de `Tick(1.9f)` seguido de `Tick(0.1f)`. Já `3f - 2.9f - 0.1f` dá cerca de `-9,7e-8`, e essa recarga expira. O resultado na fronteira depende dos valores, então não conte com ele. Nos testes, avance direto até o valor da fronteira (`Tick(2f)`) ou passe um pouco dela.

---

## 5. Integração via código (C#) {#codigo}

```csharp
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;

public class PlayerCombat : MonoBehaviour {
    public AbilitySystemComponent asc;
    public GameplayAbility fireball;

    void Start() {
        asc.GiveAbility(fireball);
        asc.OnAbilityFailed += (ability, reason) => Debug.Log($"{ability.DisplayName}: {reason}");
    }

    void Update() {
        if (Input.GetKeyDown(KeyCode.E)) {
            Vector3 targetPos = GetMouseWorldPosition();
            var result = asc.TryActivateAbility(fireball, targetPos);
        }
    }
}
```
