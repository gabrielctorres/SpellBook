#if UNITY_EDITOR
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.Tests
{
    /// <summary>Limite da base do atributo e aviso único do valor inicial (B-002, NC1: CA1 a CA5, CA29 e CA30).</summary>
    public class AttributeLimitTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private SimpleAttributeFormula _formula;

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
            if (_formula != null) Object.Destroy(_formula);
        }

        [Test]
        public void InstantDamageAboveHealth_ClampsBaseAtMin_ThenHealCounts()
        {
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            AttributeState state = GetState(asc, health);

            asc.ApplyEffect(CreateInstantEffect(health, -150f));
            float baseAfterDamage = state.BaseValue;
            float currentAfterDamage = state.CurrentValue;
            asc.ApplyEffect(CreateInstantEffect(health, 30f));

            Assert.That(baseAfterDamage, Is.EqualTo(0f), "a base passou do mínimo");
            Assert.That(currentAfterDamage, Is.EqualTo(0f));
            Assert.That(state.BaseValue, Is.EqualTo(30f), "a cura não valeu inteira");
            Assert.That(state.CurrentValue, Is.EqualTo(30f));
        }

        [Test]
        public void SetBaseValue_AboveMax_ClampsToMax()
        {
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            AttributeState state = GetState(CreateManualAsc((health, 50f)), health);

            state.SetBaseValue(150f);

            Assert.That(state.BaseValue, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void InitialValueOutOfRange_IsClamped()
        {
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);

            AttributeState state = GetState(CreateManualAsc((health, 150f)), health);

            Assert.That(state.BaseValue, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void PeriodicDamage_DoesNotTakeBaseBelowMin()
        {
            AttributeDefinition health = CreateAttribute("Health", 0f, 100f);
            AbilitySystemComponent asc = CreateManualAsc((health, 50f));
            AttributeState state = GetState(asc, health);
            asc.ApplyEffect(CreatePeriodicEffect(health, -30f));

            asc.Tick(1f);
            asc.Tick(1f); // 50 - 30 - 30 = -10 sem limite

            Assert.That(state.BaseValue, Is.EqualTo(0f));
            Assert.That(state.CurrentValue, Is.EqualTo(0f));
        }

        [Test]
        public void PeriodicRegen_StopsAtMax_BaseAndCurrent()
        {
            AttributeDefinition mana = CreateAttribute("Mana", 0f, 100f);
            AbilitySystemComponent asc = CreateManualAsc((mana, 90f));
            AttributeState state = GetState(asc, mana);
            asc.ApplyEffect(CreatePeriodicEffect(mana, 20f));

            asc.Tick(1f);
            asc.Tick(1f); // 90 + 20 + 20 = 130 sem limite

            Assert.That(state.BaseValue, Is.EqualTo(100f));
            Assert.That(state.CurrentValue, Is.EqualTo(100f));
        }

        [Test]
        public void InitialValueOutOfRange_LogsOneWarningWithAttributeName()
        {
            AttributeDefinition health = CreateAttribute("Health_Limit", 0f, 100f);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[GAS\] Valor inicial .*'Health_Limit'.*foi limitado para 100"));

            CreateManualAsc((health, 150f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DuplicateStartingAttribute_DoesNotRepeatWarning()
        {
            AttributeDefinition health = CreateAttribute("Health_Duplicate", 0f, 100f);
            LogAssert.Expect(LogType.Warning, new Regex(@"^\[GAS\] Valor inicial .*'Health_Duplicate'"));

            CreateManualAsc((health, 150f), (health, 200f));

            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void DerivedAttribute_StillWarnsAndUsesFormula()
        {
            AttributeDefinition strength = CreateAttribute("Strength", 0f, 100f);
            AttributeDefinition power = CreateAttribute("Power", 0f, 100f);
            power.Type = AttributeType.Derived;
            power.Formula = CreateDoubleFormula(strength);
            AbilitySystemComponent asc = CreateManualAsc((strength, 10f));
            var state = new AttributeState(power, 0f, asc);
            LogAssert.Expect(LogType.Warning, new Regex("Derived attribute"));

            state.SetBaseValue(50f);

            Assert.That(state.CurrentValue, Is.EqualTo(20f), "o derivado deixou de usar a fórmula (2 x Força)");
        }

        private AttributeDefinition CreateAttribute(string name, float min, float max)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = min;
            definition.MaxValue = max;
            return definition;
        }

        private GameplayEffect CreateInstantEffect(AttributeDefinition attribute, float value)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(ConstantAdd(attribute, value));
            return effect;
        }

        /// <summary>Efeito de 10 s com um tique por segundo que soma <paramref name="value"/> na base.</summary>
        private GameplayEffect CreatePeriodicEffect(AttributeDefinition attribute, float value)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(ConstantAdd(attribute, value));
            return effect;
        }

        private static AttributeEffectModifier ConstantAdd(AttributeDefinition attribute, float value)
        {
            return new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Constant, ConstantValue = value },
            };
        }

        /// <summary>Fórmula 2 x <paramref name="attribute"/> do conjurador, com os campos privados preenchidos como o Inspector faria.</summary>
        private SimpleAttributeFormula CreateDoubleFormula(AttributeDefinition attribute)
        {
            var formula = ScriptableObject.CreateInstance<SimpleAttributeFormula>();
            using (var so = new SerializedObject(formula))
            {
                SerializedProperty list = so.FindProperty("attributes");
                list.arraySize = 1;
                SerializedProperty item = list.GetArrayElementAtIndex(0);
                item.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Attribute)).objectReferenceValue = attribute;
                item.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Multiplier)).floatValue = 2f;
                item.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Source)).enumValueIndex =
                    (int)SimpleAttributeFormula.TargetSource.Source;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

            _formula = formula;
            return formula;
        }

        private AbilitySystemComponent CreateManualAsc(params (AttributeDefinition Definition, float InitialValue)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            return asc;
        }

        private static AttributeState GetState(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
            return state;
        }
    }
}
#endif
