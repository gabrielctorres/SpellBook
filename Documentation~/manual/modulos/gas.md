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

O `Projectile` também tem um campo `timeSource`, usado no movimento do `FixedUpdate`.

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
