# Documentação: SpellBook Gameplay Ability System (GAS)

Este documento é o manual oficial para utilizar o sistema de habilidades modular e data-driven do SpellBook.

## 1. Pilares do Sistema

### 1.1 Tags (GameplayTags)
- **O que são:** Identificadores hierárquicos (ex: `State.Stunned`, `Ability.Fireball.Cooldown`).
- **Uso:** Controle de fluxo. Uma habilidade pode ser bloqueada se o personagem tiver a tag `State.Stunned`.

### 1.2 Atributos (Smart Attributes)
- **O que são:** Valores como Vida, Mana e Velocidade.
- **Diferencial:** Mantêm um histórico de modificadores (Add, Multiply, Override), permitindo cálculos precisos de bônus e penalidades.

### 1.3 Gameplay Effects (GE)
- **Instant:** Modifica o valor base (ex: Dano, Cura).
- **Duration/Infinite:** Adiciona um modificador temporário ou permanente e pode conceder Tags enquanto ativo.
- **Stacking:** Suporta acúmulo de instâncias (ex: Veneno que stacka até 5 vezes).

### 1.4 Gameplay Abilities (GA) - Workflow Inline (Odin)
As habilidades agora são compostas **dentro do próprio arquivo da habilidade**. Você não precisa criar ScriptableObjects separados para cada ação.

---

## 2. Guia de Criação de Habilidades (Receitas)

### Passo a Passo:
1. Clique com o botão direito no Project -> **Create -> GAS -> Gameplay Ability**.
2. No Inspector, localize a lista **Actions**.
3. Clique no botão **+** (ou no botão de dropdown do Odin) e selecione a ação desejada (ex: `WaitAction`, `TargetingAction`).
4. Configure os parâmetros da ação diretamente ali.

### Exemplo: Habilidade de Área (Explosão)
1. **Action 0 (AnimationAction):** Trigger: "Attack".
2. **Action 1 (WaitAction):** Duration: 0.5s.
3. **Action 2 (TargetingAction):** Strategy: (Arraste um TS_Circle).
4. **Action 3 (PlayCueAction):** VFX_Prefab: (Explosão), SFX_Clip: (Explosão).
5. **Action 4 (ApplyEffectAction):** Effects: (Arraste um GE_Damage).

---

## 3. Lista de Ações Disponíveis
- **WaitAction:** Pausa a execução por X segundos.
- **TargetingAction:** Busca alvos usando uma estratégia (Raio, Mouse...). `Mode` Replace substitui os alvos atuais, Append adiciona.
- **ApplyEffectAction:** Aplica GameplayEffects aos alvos ou ao conjurador.
- **PlayCueAction:** Dispara efeitos visuais e sonoros.
- **AnimationAction:** Altera parâmetros (Trigger, Bool, Int) no Animator do personagem.
- **SpawnProjectileAction:** Dispara projéteis; as `OnHitActions` rodam no impacto com um contexto derivado (mesma habilidade e nível).
- **RepeatAction:** Repete uma lista de ações N vezes, com intervalo opcional.
- **ParallelAction:** Executa várias ações ao mesmo tempo e espera todas terminarem.
- **ConditionalAction:** Executa `Then` ou `Else` conforme tags do conjurador ou existência de alvos.

### 3.1 Criando ações próprias
Herde de `AbilityAction` e implemente `IEnumerator Execute(AbilityContext context)`.
- A mesma instância da ação é compartilhada por todos que usam a habilidade: guarde estado de execução no `context.CustomData`, nunca em campos.
- Sobrescreva `OnCancel(AbilityContext context)` para restaurar estado se a habilidade for cancelada no meio da ação (ex: reabilitar movimento após um dash).
- Para rodar sub-ações use `AbilityAction.RunSequence(lista, context)`; para corrotinas paralelas use `context.StartChildRoutine(...)`, que é interrompida junto no cancelamento.
- Use `context.GetValidTargets()` em vez de `context.Targets` quando alvos podem ter sido destruídos durante a habilidade.

### 3.2 Targeting
`TargetingStrategy` respeita `physicsMode` (2D/3D), procura o `AbilitySystemComponent` no collider ou nos pais (sem duplicatas) e filtra por:
- `includeSelf` (padrão: falso);
- `requiredTargetTags` / `ignoredTargetTags` (ex: ignorar a tag do próprio time).

`CircleTargeting` aceita `maxTargets` (prioriza os mais próximos). `MouseTargetingStrategy` retorna o alvo válido mais próximo.

---

## 4. Ciclo de Vida, Cooldown e Custo

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

## 5. Integração via Código (C#)

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
