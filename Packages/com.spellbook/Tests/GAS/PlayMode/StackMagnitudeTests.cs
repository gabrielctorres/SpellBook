#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary>Pilhas multiplicam a magnitude dos modificadores (B-007, VN7, ADR 0021 e 0029 item 2: CA24, CA25, R21).</summary>
    public class StackMagnitudeTests
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
        public void Plus5_3Stacks_Is15()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AbilitySystemComponent asc = CreateManualAsc((attack, 10f));
            GameplayEffect effect = CreateStackingEffect(5);
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Add, 5f));

            asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);

            Assert.That(asc.GetAttributeValue(attack), Is.EqualTo(25f).Within(Tolerance)); // 10 + 3 x 5
        }

        [Test]
        public void MultiplyOff_Is5()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AbilitySystemComponent asc = CreateManualAsc((attack, 10f));
            GameplayEffect effect = CreateStackingEffect(5);
            effect.StacksMultiplyMagnitude = false;
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Add, 5f));

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);
            asc.ApplyEffect(effect);

            Assert.That(instance.CurrentStacks, Is.EqualTo(3));
            Assert.That(asc.GetAttributeValue(attack), Is.EqualTo(15f).Within(Tolerance)); // 10 + 5
        }

        [Test]
        public void RemoveStacks_ReducesValue_FiresAttributeEvents()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AbilitySystemComponent asc = CreateManualAsc((attack, 10f));
            GameplayEffect effect = CreateStackingEffect(5);
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Add, 5f));
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            instance.SetStacks(3);
            Assert.That(asc.AttributeSet.TryGetState(attack, out AttributeState state), Is.True);
            var changes = new List<(float, float)>();
            state.OnValueChanged += (oldValue, newValue) => changes.Add((oldValue, newValue));

            bool changed = instance.RemoveStacks(2);

            Assert.That(changed, Is.True);
            Assert.That(asc.GetAttributeValue(attack), Is.EqualTo(15f).Within(Tolerance)); // 10 + 1 x 5
            Assert.That(changes, Is.EqualTo(new[] { (25f, 15f) }));
        }

        [Test]
        public void StacksBeforeComposition_AddAndMultiply()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AbilitySystemComponent asc = CreateManualAsc((attack, 100f));
            GameplayEffect effect = CreateStackingEffect(5);
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Add, 5f));
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Multiply, 0.1f));

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            instance.SetStacks(3);

            Assert.That(asc.GetAttributeValue(attack), Is.EqualTo(149.5f).Within(Tolerance)); // (100 + 15) x (1 + 0,3)
        }

        [Test]
        public void Tick_MultipliedByStacks()
        {
            AttributeDefinition health = CreateAttribute("Health");
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            GameplayEffect effect = CreateStackingEffect(5);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(Modifier(health, ModifierOperation.Add, -2f));
            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            instance.SetStacks(3);

            asc.Tick(1f);

            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(94f).Within(Tolerance)); // 100 - 3 x 2
        }

        [Test]
        public void OldAssetWithoutField_MultipliesByDefault()
        {
            AttributeDefinition attack = CreateAttribute("Attack");
            AbilitySystemComponent asc = CreateManualAsc((attack, 10f));
            GameplayEffect effect = CreateStackingEffect(5);
            // Formato 2.x: sem o campo stacksMultiplyMagnitude
            JsonUtility.FromJsonOverwrite("{\"CanStack\":true,\"MaxStacks\":5,\"RefreshDurationOnStack\":true}", effect);
            effect.Modifiers.Add(Modifier(attack, ModifierOperation.Add, 5f));

            ActiveEffectInstance instance = asc.ApplyEffect(effect);
            instance.SetStacks(3);

            Assert.That(effect.StacksMultiplyMagnitude, Is.True);
            Assert.That(asc.GetAttributeValue(attack), Is.EqualTo(25f).Within(Tolerance));
        }

        private GameplayEffect CreateStackingEffect(int maxStacks)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.CanStack = true;
            effect.MaxStacks = maxStacks;
            return effect;
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private static AttributeEffectModifier Modifier(AttributeDefinition attribute, ModifierOperation operation, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = operation,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(value) },
            };
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
