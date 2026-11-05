// --8<-- [start:using-system]
using System;
// --8<-- [end:using-system]
using System.Collections.Generic;
using System.Linq;
using NUnit.Framework;
// --8<-- [start:using-core-conditions]
using SpellBook.Core.Conditions;
// --8<-- [end:using-core-conditions]
using SpellBook.Core.Editor.Inspector;
// --8<-- [start:using-core-inspector]
using SpellBook.Core.Inspector;
// --8<-- [end:using-core-inspector]
// --8<-- [start:using-gas-abilities]
using SpellBook.GAS.Abilities;
// --8<-- [end:using-gas-abilities]
using SpellBook.GAS.Attributes;
// --8<-- [start:using-gas-conditions]
using SpellBook.GAS.Conditions;
// --8<-- [end:using-gas-conditions]
// --8<-- [start:using-gas-core]
using SpellBook.GAS.Core;
// --8<-- [end:using-gas-core]
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
// --8<-- [start:using-unityengine]
using UnityEngine;
// --8<-- [end:using-unityengine]
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests.Examples
{
    /// <summary>
    /// Exemplos da página <c>modulos/gas.md</c> do manual. Cada trecho entre os marcadores <c>--8&lt;--</c> aparece
    /// na página, e os testes conferem o que o texto em volta diz dele (guia do manual, parte 9). Os ASCs são criados
    /// sem <c>Awake</c> (EditMode), então os exemplos usam habilidades, tags, recargas e efeitos sem modificador.
    /// </summary>
    public class GasManualExamples
    {
        private readonly List<GameObject> _objects = new List<GameObject>();
        private GasFactory _gas;

        // --8<-- [start:find-effects-field]
        private readonly List<ActiveEffectInstance> _found = new List<ActiveEffectInstance>();
        // --8<-- [end:find-effects-field]

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (GameObject go in _objects) Object.DestroyImmediate(go);
            _objects.Clear();
            _gas.Dispose();
        }

        [Test]
        public void EffectSpec_WithCallerValues_ReachesInstanceContext()
        {
            GameplayEffect efeito = _gas.CreateDurationEffect(5f);
            AbilitySystemComponent caster = CreateAsc("Caster");
            AbilitySystemComponent target = CreateAsc("Target");

            // --8<-- [start:effect-spec]
            var valores = new SetByCallerValues();   // guarde e reutilize; Clear() não libera a memória
            valores.Set("Dano", 120f);
            EffectSpec spec = EffectSpec.Create(efeito, caster).WithCallerValues(valores);
            target.TryApplyEffect(in spec, out ActiveEffectInstance instancia);
            // --8<-- [end:effect-spec]

            Assert.That(instancia, Is.Not.Null);
            Assert.That(instancia.Context.Instigator, Is.SameAs(caster));
            Assert.That(spec.Context.CallerValues.TryGet("Dano", out float dano), Is.True);
            Assert.That(dano, Is.EqualTo(120f));
        }

        [Test]
        public void TryApplyEffect_TargetWithImmunityTag_LogsImmune()
        {
            GameplayTag immune = _gas.CreateTag("State.Immune");
            GameplayEffect stun = _gas.CreateDurationEffect(2f);
            stun.name = "GE_Stun";
            stun.ApplicationImmunityTags.Add(immune);
            AbilitySystemComponent caster = CreateAsc("Caster");
            AbilitySystemComponent target = CreateAsc("Target");
            target.AddTag(immune);
            LogAssert.Expect(LogType.Log, "Target é imune a GE_Stun");

            // --8<-- [start:immunity]
            if (target.TryApplyEffect(stun, caster, out _) == EffectApplicationResult.BlockedByImmunity)
                Debug.Log($"{target.name} é imune a {stun.name}");
            // --8<-- [end:immunity]

            Assert.That(target.ActiveEffects, Is.Empty);
        }

        [Test]
        public void FindEffectsWithTag_ListsBurnsWithTimeRemaining()
        {
            GameplayTag statusBurn = _gas.CreateTag("Status.Burn");
            GameplayEffect burn = _gas.CreateDurationEffect(3f, statusBurn);
            burn.name = "GE_Burn";
            AbilitySystemComponent asc = CreateAsc();
            asc.ApplyEffect(burn);
            asc.ApplyEffect(_gas.CreateDurationEffect(3f));
            LogAssert.Expect(LogType.Log, $"GE_Burn: {3f:0.0} s");

            // --8<-- [start:find-effects-call]

            int burns = asc.FindEffectsWithTag(statusBurn, _found);
            for (int i = 0; i < burns; i++) Debug.Log($"{_found[i].Effect.name}: {_found[i].TimeRemaining:0.0} s");
            // --8<-- [end:find-effects-call]

            Assert.That(burns, Is.EqualTo(1));
            Assert.That(_found[0].Effect, Is.SameAs(burn));
        }

        [Test]
        public void AbilityValues_AddThenTryGetValue_ReadsLevel()
        {
            GameplayAbility habilidade = _gas.CreateAbility();

            // --8<-- [start:ability-values]
            habilidade.Values.Add("Damage", ScalableFloat.FromArray(50, 75, 100));
            habilidade.TryGetValue("Damage", 3, out float dano); // true, 100
            // --8<-- [end:ability-values]

            Assert.That(habilidade.TryGetValue("Damage", 3, out _), Is.True);
            Assert.That(dano, Is.EqualTo(100f));
        }

        [Test]
        public void Fireball_GivenAtLevel3_CooldownSixAndDamage100()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            SetMaxLevel(fireball, 4);
            AbilitySystemComponent asc = CreateAsc();

            // --8<-- [start:fireball]
            fireball.Values.Add("damage", ScalableFloat.FromArray(50, 75, 100, 125));
            fireball.Values.Add("cooldown", ScalableFloat.FromArray(8, 7, 6, 5));
            fireball.CooldownEffect.Duration = ScalableFloat.FromTable("cooldown");
            asc.GiveAbility(fireball, 3); // recarga de 6 s, dano 100
            // --8<-- [end:fireball]

            AbilityActivationResult result = asc.TryActivateAbility(fireball, AbilityTarget.None);
            fireball.TryGetValue("damage", asc.GetAbilityLevel(fireball), out float dano);
            Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(asc.ActiveEffects.Single().AppliedDuration, Is.EqualTo(6f));
            Assert.That(dano, Is.EqualTo(100f));
        }

        [Test]
        public void Dash_ActivateWithThreeCharges_LogsThreeToTwo()
        {
            GameplayAbility dash = _gas.CreateAbility();
            dash.name = "Dash";
            dash.Charges.Enabled = true;
            dash.Charges.MaxCharges = new ModifierValueSource { Value = ScalableFloat.Constant(3f) };
            dash.Charges.RechargeTime = ScalableFloat.Constant(6f);
            AbilitySystemComponent asc = CreateAsc();
            asc.GiveAbility(dash);
            LogAssert.Expect(LogType.Log, "Dash: 3 -> 2");

            // --8<-- [start:dash-charges]
            AbilitySpec spec = asc.FindSpec(dash);
            asc.OnChargesChanged += (s, antes, depois) => Debug.Log($"{s.Ability.name}: {antes} -> {depois}");
            asc.TryActivateAbility(dash, AbilityTarget.None); // 3 -> 2, a recarga começa
            float barra = asc.GetChargeRechargeNormalized(spec);
            // --8<-- [end:dash-charges]

            Assert.That(asc.GetCharges(spec), Is.EqualTo(2));
            Assert.That(asc.GetChargeRechargeRemaining(spec), Is.EqualTo(6f));
            Assert.That(barra, Is.EqualTo(1f));
        }

        [Test]
        public void MinLevelCondition_ComparesLevel_AndIsOfferedByPicker()
        {
            var condition = new MinLevelCondition();
            var level1 = new ConditionContext(null, null, 1, null, AbilityTarget.None, null);
            var level2 = new ConditionContext(null, null, 2, null, AbilityTarget.None, null);

            Type[] candidates = TypePickerCatalog.GetCandidates(typeof(Condition<ConditionContext>)).Select(e => e.Type).ToArray();
            Assert.That(condition.Evaluate(in level1), Is.False);
            Assert.That(condition.Evaluate(in level2), Is.True);
            Assert.That(condition.DefaultFailureKey, Is.EqualTo("game.activation.min_level"));
            Assert.That(candidates, Does.Contain(typeof(MinLevelCondition)));
        }

        [Test]
        public void Tick_WithManualTimeSource_ExpiresTwoSecondEffect()
        {
            var manualTime = ScriptableObject.CreateInstance<ManualTimeSource>();
            GameplayEffect burning = _gas.CreateDurationEffect(2f);
            AbilitySystemComponent asc = CreateAsc();

            try
            {
                // --8<-- [start:asc-tick]
                asc.SetTimeSource(manualTime); // com autoTick desligado no Inspector
                asc.ApplyEffect(burning);      // efeito de 2 s
                asc.Tick(1.9f);                // ainda ativo
                // --8<-- [end:asc-tick]

                Assert.That(asc.ActiveEffects.Count, Is.EqualTo(1));

                // --8<-- [start:asc-tick-expired]
                asc.Tick(0.2f);                // expirou
                // --8<-- [end:asc-tick-expired]

                Assert.That(asc.ActiveEffects, Is.Empty);
                Assert.That(asc.TimeSource, Is.SameAs(manualTime));
            }
            finally
            {
                Object.DestroyImmediate(manualTime);
            }
        }

        [Test]
        public void PlayerCombat_GiveThenTryActivate_Succeeds()
        {
            GameplayAbility fireball = _gas.CreateAbility();
            AbilitySystemComponent asc = CreateAsc();

            asc.GiveAbility(fireball);
            AbilityActivationResult result = asc.TryActivateAbility(fireball, Vector3.zero);

            Assert.That(result, Is.EqualTo(AbilityActivationResult.Success));
            Assert.That(typeof(MonoBehaviour).IsAssignableFrom(typeof(PlayerCombat)), Is.True);
        }

        // ASC sem Awake (EditMode): serve para habilidades, tags, recargas e efeitos sem modificador.
        private AbilitySystemComponent CreateAsc(string name = "ASC")
        {
            var go = new GameObject(name);
            _objects.Add(go);
            return go.AddComponent<AbilitySystemComponent>();
        }

        private static void SetMaxLevel(GameplayAbility ability, int maxLevel)
        {
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
        }
    }

    // --8<-- [start:min-level-condition]

    [Serializable]
    [TypeCategory("Nível", Summary = "A habilidade está no nível mínimo.", Line = "Nível ≥ {minLevel}")]
    public sealed class MinLevelCondition : Condition<ConditionContext>
    {
        [SerializeField] private int minLevel = 2;

        public override string DefaultFailureKey => "game.activation.min_level";

        public override bool Evaluate(in ConditionContext context) => context.Level >= minLevel;
    }
    // --8<-- [end:min-level-condition]

    // --8<-- [start:player-combat]

    public class PlayerCombat : MonoBehaviour
    {
        [SerializeField] private AbilitySystemComponent asc;
        [SerializeField] private GameplayAbility fireball;

        private void Start()
        {
            asc.GiveAbility(fireball);
            asc.OnAbilityFailed += (ability, falha) => Debug.Log($"{ability.DisplayName.Text}: {falha.ReasonKey ?? falha.Result.ToString()}");
        }

        private void Update()
        {
            if (Input.GetKeyDown(KeyCode.E))
            {
                Vector3 targetPos = GetMouseWorldPosition();
                var result = asc.TryActivateAbility(fireball, targetPos);
            }
        }

        private Vector3 GetMouseWorldPosition()
        {
            Ray ray = Camera.main.ScreenPointToRay(Input.mousePosition);
            return new Plane(Vector3.up, Vector3.zero).Raycast(ray, out float distance) ? ray.GetPoint(distance) : transform.position;
        }
    }
    // --8<-- [end:player-combat]
}
