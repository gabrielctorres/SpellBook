#if UNITY_EDITOR
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Abilities.Actions;
using SpellBook.GAS.Actions;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Magnitude ao vivo e ciclos em runtime (B-007, VN8, ADR 0021 e 0030: CA20 a CA23, CA26, CA27, CA30).</summary>
    public class LiveMagnitudeTests
    {
        private const float Tolerance = 1e-4f;

        private GasFactory _gas;
        private TimeFactory _time;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Snapshot_SourceChange_NoEffect()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            GameplayEffect buff = CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Snapshot));
            target.ApplyEffect(buff, caster);

            SetBase(caster, attack, 150f);

            Assert.That(target.GetAttributeValue(bonus), Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void Live_SourceChange_UpdatesWithoutTick()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            GameplayEffect buff = CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Live));
            target.ApplyEffect(buff, caster);

            SetBase(caster, attack, 150f);

            Assert.That(target.GetAttributeValue(bonus), Is.EqualTo(150f).Within(Tolerance));
        }

        [Test]
        public void DefaultIsSnapshot_OldValuesSame()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            var modifier = new AttributeEffectModifier
            {
                Attribute = bonus,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute, Attribute = attack },
            };
            // Formato 2.x: o modificador sem o campo Mode
            JsonUtility.FromJsonOverwrite("{\"Operation\":0}", modifier);
            ActiveEffectInstance instance = target.ApplyEffect(CreateInfinite(modifier), caster);

            SetBase(caster, attack, 150f);

            Assert.That(modifier.Mode, Is.EqualTo(MagnitudeMode.Snapshot));
            Assert.That(instance.LiveBindingCount, Is.EqualTo(0));
            Assert.That(target.GetAttributeValue(bonus), Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void LiveCycle_CutWithSingleWarning()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition defense = CreateAttribute("Defense");
            AbilitySystemComponent asc = CreateManualAsc((attack, 10f), (defense, 5f));
            // A depende de B e B depende de A, os dois ao vivo (C12)
            GameplayEffect effect = CreateInfinite(
                ReadAttribute(attack, defense, MagnitudeMode.Live),
                ReadAttribute(defense, attack, MagnitudeMode.Live));
            asc.ApplyEffect(effect);

            LogAssert.Expect(LogType.Warning, new Regex("ciclo"));
            SetBase(asc, attack, 20f);
            SetBase(asc, attack, 21f);

            LogAssert.NoUnexpectedReceived();
            Assert.That(float.IsNaN(asc.GetAttributeValue(attack)), Is.False);
        }

        [Test]
        public void Tick_EvaluatesWithInstanceContext()
        {
            AttributeDefinition power = CreateAttribute("Power");
            AttributeDefinition health = CreateAttribute("Health");
            AbilitySystemComponent caster = CreateManualAsc((power, 2f));
            AbilitySystemComponent target = CreateManualAsc((health, 100f));
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.Period = 1f;
            // Ao vivo num periódico não tem efeito: o tique sempre avalia com o contexto da instância (P9)
            effect.PeriodicModifiers.Add(ReadAttribute(health, power, MagnitudeMode.Live));
            ActiveEffectInstance instance = target.ApplyEffect(effect, caster);

            target.Tick(1f);
            SetBase(caster, power, 5f);
            target.Tick(1f);

            Assert.That(instance.LiveBindingCount, Is.EqualTo(0));
            Assert.That(target.GetAttributeBaseValue(health), Is.EqualTo(107f).Within(Tolerance)); // 100 + 2 + 5
        }

        [Test]
        public void Live_1000Recalcs_DoNotAllocate()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            target.ApplyEffect(CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Live)), caster);
            Assert.That(caster.AttributeSet.TryGetState(attack, out AttributeState state), Is.True);
            state.SetBaseValue(101f);
            state.SetBaseValue(100f);

            Assert.That(() =>
            {
                for (int i = 0; i < 1000; i++) state.SetBaseValue(i % 2 == 0 ? 101f : 100f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(target.GetAttributeValue(bonus), Is.EqualTo(100f).Within(Tolerance));
        }

        [Test]
        public void Snapshot_HasZeroBindings()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));

            ActiveEffectInstance snapshot = target.ApplyEffect(CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Snapshot)), caster);
            ActiveEffectInstance live = target.ApplyEffect(CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Live)), caster);

            Assert.That(snapshot.LiveBindingCount, Is.EqualTo(0));
            Assert.That(live.LiveBindingCount, Is.EqualTo(1));
        }

        [Test]
        public void LiveLevel_RecalculatesOnLevelUp_SnapshotDoesNot()
        {
            AttributeDefinition liveBonus = CreateAttribute("LiveBonus");
            AttributeDefinition snapshotBonus = CreateAttribute("SnapshotBonus");
            AbilitySystemComponent caster = CreateManualAsc((liveBonus, 0f), (snapshotBonus, 0f));
            GameplayEffect effect = CreateInfinite(
                ByLevel(liveBonus, MagnitudeMode.Live, 10f, 20f, 30f),
                ByLevel(snapshotBonus, MagnitudeMode.Snapshot, 10f, 20f, 30f));
            GameplayAbility ability = CreateAbility(3, new ApplyEffectAction { Effects = new() { effect }, ApplyToSource = true });
            caster.GiveAbility(ability, 1);
            caster.TryActivateAbility(ability, Vector3.zero);
            ActiveEffectInstance instance = caster.ActiveEffects[0];

            caster.SetAbilityLevel(ability, 3);

            Assert.That(caster.GetAttributeValue(liveBonus), Is.EqualTo(30f).Within(Tolerance));
            Assert.That(caster.GetAttributeValue(snapshotBonus), Is.EqualTo(10f).Within(Tolerance));
            Assert.That(instance.Context.Level, Is.EqualTo(3), "o ao vivo que depende do nível atualiza o nível guardado");
        }

        [Test]
        public void RemoveEffect_DisposesBindings()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            GameplayEffect buff = CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Live));
            ActiveEffectInstance instance = target.ApplyEffect(buff, caster);
            AttributeModifier modifier = target.AttributeSet.TryGetState(bonus, out AttributeState bonusState) ? bonusState.Modifiers[0] : null;

            target.RemoveEffect(instance);
            SetBase(caster, attack, 150f);

            Assert.That(instance.LiveBindingCount, Is.EqualTo(0));
            Assert.That(modifier.Value, Is.EqualTo(100f).Within(Tolerance), "a ligação desfeita não recalcula mais");
            Assert.That(target.GetAttributeValue(bonus), Is.EqualTo(0f).Within(Tolerance));
        }

        [Test]
        public void TargetDestroyed_LiveBindingOnOtherAsc_DisposedAndNotRecalculated()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AttributeDefinition bonus = CreateAttribute("Bonus");
            AbilitySystemComponent caster = CreateManualAsc((attack, 100f));
            AbilitySystemComponent target = CreateManualAsc((bonus, 0f));
            ActiveEffectInstance instance = target.ApplyEffect(CreateInfinite(ReadAttribute(bonus, attack, MagnitudeMode.Live)), caster);
            Assert.That(target.AttributeSet.TryGetState(bonus, out AttributeState bonusState), Is.True);
            AttributeModifier modifier = bonusState.Modifiers[0];
            int targetChanges = 0;
            bonusState.OnValueChanged += (_, _) => targetChanges++;

            Object.DestroyImmediate(target.gameObject);
            SetBase(caster, attack, 150f);

            Assert.That(instance.LiveBindingCount, Is.EqualTo(0), "o alvo destruído desfaz a ligação no ASC do instigador");
            Assert.That(targetChanges, Is.EqualTo(0), "nada é recalculado no alvo destruído");
            Assert.That(modifier.Value, Is.EqualTo(100f).Within(Tolerance));
        }

        private GameplayEffect CreateInfinite(params AttributeEffectModifier[] modifiers)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.Modifiers.AddRange(modifiers);
            return effect;
        }

        private static AttributeEffectModifier ReadAttribute(AttributeDefinition attribute, AttributeDefinition source, MagnitudeMode mode)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                Mode = mode,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Attribute, Attribute = source },
            };
        }

        private static AttributeEffectModifier ByLevel(AttributeDefinition attribute, MagnitudeMode mode, params float[] values)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                Mode = mode,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.FromArray(values) },
            };
        }

        private GameplayAbility CreateAbility(int maxLevel, params GameplayAction[] actions)
        {
            GameplayAbility ability = _gas.CreateAbility(actions);
            var so = new SerializedObject(ability);
            so.FindProperty("maxLevel").intValue = maxLevel;
            so.ApplyModifiedPropertiesWithoutUndo();
            return ability;
        }

        private static void SetBase(AbilitySystemComponent asc, AttributeDefinition attribute, float value)
        {
            Assert.That(asc.AttributeSet.TryGetState(attribute, out AttributeState state), Is.True);
            state.SetBaseValue(value);
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }
    }
}
#endif
