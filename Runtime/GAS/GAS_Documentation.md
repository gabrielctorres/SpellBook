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
- **TargetingAction:** Busca alvos usando uma estratégia (Raio, Cone, etc).
- **ApplyEffectAction:** Aplica GameplayEffects aos alvos ou ao conjurador.
- **PlayCueAction:** Dispara efeitos visuais e sonoros.
- **AnimationAction:** Altera parâmetros (Trigger, Bool, Int) no Animator do personagem.

---

## 4. Sistema de Cooldown e Custo
Para adicionar custo ou cooldown, basta adicionar uma **ApplyEffectAction** no início ou fim da lista de ações da habilidade, aplicando o efeito ao **Source**.

---

## 5. Integração via Código (C#)

```csharp
using SpellBook.GAS.Core;
using SpellBook.GAS.Abilities;

public class PlayerCombat : MonoBehaviour {
    public AbilitySystemComponent asc;
    public GameplayAbility fireball;

    void Update() {
        if (Input.GetKeyDown(KeyCode.E)) {
            Vector3 targetPos = GetMouseWorldPosition();
            asc.TryActivateAbility(fireball, targetPos);
        }
    }
}
```


