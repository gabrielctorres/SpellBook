# GAS {#gas}

O GAS (Gameplay Ability System) é o sistema modular e orientado a dados do SpellBook para
habilidades, efeitos e atributos. Esta página é o guia de uso.

## 1. Visão geral {#visao-geral}

### 1.1 Tags (GameplayTags) {#tags}
- **O que são:** identificadores hierárquicos (ex: `State.Stunned`, `Ability.Fireball.Cooldown`).
- **Uso:** controle de fluxo. Uma habilidade pode ser bloqueada se o personagem tiver a tag `State.Stunned`.

### 1.2 Atributos {#attributeset}
- **O que são:** valores como Vida, Mana e Velocidade, agrupados em um `AttributeSet`.

#### AttributeDefinition {#attributedefinition}
Cada valor tem uma `AttributeDefinition`.

#### Fórmulas de modificadores {#formulas}
Os atributos mantêm um histórico de modificadores (Add, Multiply, Override), permitindo cálculos precisos de bônus e penalidades.

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
- **WaitAction:** pausa a execução por X segundos.
- **TargetingAction:** busca alvos usando uma estratégia (Raio, Mouse...). `Mode` Replace substitui os alvos atuais, Append adiciona.
- **ApplyEffectAction:** aplica GameplayEffects aos alvos ou ao conjurador.
- **PlayCueAction:** dispara efeitos visuais e sonoros.
- **AnimationAction:** altera parâmetros (Trigger, Bool, Int) no Animator do personagem.
- **SpawnProjectileAction:** dispara projéteis (detalhes em [SpawnProjectileAction](#projectile)).
- **RepeatAction:** repete uma lista de ações N vezes, com intervalo opcional.
- **ParallelAction:** executa várias ações ao mesmo tempo e espera todas terminarem.
- **ConditionalAction:** executa `Then` ou `Else` conforme tags do conjurador ou existência de alvos.

#### SpawnProjectileAction {#projectile}
Dispara projéteis. As `OnHitActions` rodam no impacto com um contexto derivado (mesma habilidade e nível).

### 3.1 Criando ações próprias {#acoes-proprias}
Herde de `AbilityAction` e implemente `IEnumerator Execute(AbilityContext context)`.

- A mesma instância da ação é compartilhada por todos que usam a habilidade: guarde estado de execução no `context.CustomData`, nunca em campos.
- Sobrescreva `OnCancel(AbilityContext context)` para restaurar estado se a habilidade for cancelada no meio da ação (ex: reabilitar movimento após um dash).
- Para rodar sub-ações use `AbilityAction.RunSequence(lista, context)`; para corrotinas paralelas use `context.StartChildRoutine(...)`, que é interrompida junto no cancelamento.
- Use `context.GetValidTargets()` em vez de `context.Targets` quando alvos podem ter sido destruídos durante a habilidade.

### 3.2 Targeting {#targeting}
`TargetingStrategy` respeita `physicsMode` (2D/3D), procura o `AbilitySystemComponent` no collider ou nos pais (sem duplicatas) e filtra por:

- `includeSelf` (padrão: falso);
- `requiredTargetTags` / `ignoredTargetTags` (ex: ignorar a tag do próprio time).

`CircleTargeting` aceita `maxTargets` (prioriza os mais próximos). `MouseTargetingStrategy` retorna o alvo válido mais próximo.

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
`OnAbilityActivated(spec)`, `OnAbilityEnded(spec, cancelled)` e `OnAbilityFailed(ability, result)`, este último útil para feedback de UI ("Sem mana", "Em recarga"). O `AbilityActivatedEvent` global do `EventManager` continua sendo disparado.

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
