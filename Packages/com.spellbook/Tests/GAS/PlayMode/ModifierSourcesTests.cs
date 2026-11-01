#if UNITY_EDITOR
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Testing;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Origens SetByCaller e atributo do alvo, e o último valor do instigador nos tiques (B-007, VN6: CA14, CA15, CA18, CA26).</summary>
    public class ModifierSourcesTests
    {
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
        }

        private AbilitySystemComponent CreateAsc(params (AttributeDefinition, float)[] attributes)
        {
            AbilitySystemComponent asc = _gas.CreateAsc(attributes);
            GasFactory.SetAutoTick(asc, false);
            return asc;
        }

        private static AttributeEffectModifier Add(AttributeDefinition attribute, ModifierValueSource source)
        {
            return new AttributeEffectModifier { Attribute = attribute, Operation = ModifierOperation.Add, ValueSource = source };
        }

        private static ModifierValueSource Caller(string name, bool hasDefault, float fallback = 0f)
        {
            return new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.SetByCaller,
                CallerName = name,
                CallerHasDefault = hasDefault,
                CallerDefault = fallback
            };
        }

        private GameplayEffect CreateInstant(AttributeDefinition attribute, ModifierValueSource source)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(Add(attribute, source));
            return effect;
        }

        // Efeito de 10 s com tique a cada 1 s que soma a fonte ao atributo.
        private GameplayEffect CreatePeriodic(AttributeDefinition attribute, ModifierValueSource source)
        {
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            effect.Period = 1f;
            effect.PeriodicModifiers.Add(Add(attribute, source));
            return effect;
        }

        [Test]
        public void SetByCaller_120_DefaultWhenMissing_ZeroWithWarningWithoutDefault()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent target = CreateAsc((health, 0f));
            GameplayEffect withDefault = CreateInstant(health, Caller("Dano", true, 10f));
            GameplayEffect withoutDefault = CreateInstant(health, Caller("Dano", false));
            var values = new SetByCallerValues();
            values.Set("Dano", 120f);

            target.ApplyEffect(EffectSpec.Create(withDefault, target).WithCallerValues(values));
            float afterCaller = target.GetAttributeValue(health);
            target.ApplyEffect(EffectSpec.Create(withDefault, target));
            float afterDefault = target.GetAttributeValue(health);
            LogAssert.Expect(LogType.Warning, new Regex("SetByCaller.*Dano"));
            target.ApplyEffect(EffectSpec.Create(withoutDefault, target));

            Assert.That(afterCaller, Is.EqualTo(120f), "valor do chamador");
            Assert.That(afterDefault, Is.EqualTo(130f), "sem o valor, vale o padrão 10");
            Assert.That(target.GetAttributeValue(health), Is.EqualTo(130f), "sem valor e sem padrão, vale 0");
        }

        [Test]
        public void CallerValues_CopiedOnDurationInstance_LaterChangeIgnored()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent target = CreateAsc((health, 0f));
            GameplayEffect effect = CreatePeriodic(health, Caller("Dano", true, 1f));
            var values = new SetByCallerValues();
            values.Set("Dano", 5f);

            ActiveEffectInstance instance = target.ApplyEffect(EffectSpec.Create(effect, target).WithCallerValues(values));
            values.Set("Dano", 50f);
            target.Tick(1f);
            values.Clear();
            target.Tick(1f);

            Assert.That(target.GetAttributeValue(health), Is.EqualTo(10f), "os dois tiques leem o 5 fixado na aplicação");
            Assert.That(instance.Context.CallerValues, Is.Not.SameAs(values), "a instância guarda uma cópia");
        }

        [Test]
        public void Tick_UsesInstigatorAttribute()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent instigator = CreateAsc((attack, 7f));
            AbilitySystemComponent target = CreateAsc((attack, 100f), (health, 0f));
            GameplayEffect effect = CreatePeriodic(health, new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Attribute,
                Attribute = attack
            });

            target.ApplyEffect(effect, instigator);
            target.Tick(1f);

            Assert.That(target.GetAttributeValue(health), Is.EqualTo(7f), "o tique lê o ataque do instigador");
        }

        [Test]
        public void TargetAttribute_ReadsTarget()
        {
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent instigator = CreateAsc((armor, 100f));
            AbilitySystemComponent target = CreateAsc((armor, 3f), (health, 0f));
            GameplayEffect effect = CreateInstant(health, new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.TargetAttribute,
                TargetAttributeDefinition = armor
            });

            target.ApplyEffect(effect, instigator);

            Assert.That(target.GetAttributeValue(health), Is.EqualTo(3f), "lê a armadura do alvo, não a do instigador");
        }

        [Test]
        public void TargetAttribute_GetValueWithContext_ReadsContextTarget()
        {
            AttributeDefinition armor = _gas.CreateAttribute("Armor");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent instigator = CreateAsc((armor, 100f));
            AbilitySystemComponent target = CreateAsc((armor, 3f), (health, 0f));
            GameplayEffect effect = _gas.CreateDurationEffect(10f);
            var source = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.TargetAttribute,
                TargetAttributeDefinition = armor
            };

            ActiveEffectInstance instance = target.ApplyEffect(effect, instigator);
            EffectContext context = instance.Context;

            Assert.That(source.GetValue(in context), Is.EqualTo(3f));
        }

        [Test]
        public void DestroyedInstigator_TickKeepsLastValue()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent instigator = CreateAsc((attack, 10f));
            AbilitySystemComponent target = CreateAsc((health, 0f));
            GameplayEffect effect = CreatePeriodic(health, new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Attribute,
                Attribute = attack
            });
            target.ApplyEffect(effect, instigator);
            target.Tick(1f);

            Object.DestroyImmediate(instigator.gameObject);
            target.Tick(1f);

            Assert.That(target.GetAttributeValue(health), Is.EqualTo(20f), "o segundo tique usa o último ataque conhecido");
        }

        [Test]
        public void DestroyedInstigator_BeforeFirstTick_UsesValueAtApplication()
        {
            AttributeDefinition attack = _gas.CreateAttribute("Attack");
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent instigator = CreateAsc((attack, 10f));
            AbilitySystemComponent target = CreateAsc((health, 0f));
            GameplayEffect effect = CreatePeriodic(health, new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Attribute,
                Attribute = attack
            });
            target.ApplyEffect(effect, instigator);

            Object.DestroyImmediate(instigator.gameObject);
            target.Tick(1f);

            Assert.That(target.GetAttributeValue(health), Is.EqualTo(10f), "o valor conhecido na aplicação");
        }

        [Test]
        public void CallerLookup_DoesNotAllocate()
        {
            AttributeDefinition health = _gas.CreateAttribute("Health");
            AbilitySystemComponent target = CreateAsc((health, 0f));
            GameplayEffect effect = CreateInstant(health, Caller("Dano", true, 1f));
            var values = new SetByCallerValues();
            values.Set("Dano", 0.5f);
            EffectSpec spec = EffectSpec.Create(effect, target).WithCallerValues(values);
            target.TryApplyEffect(in spec, out _);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++) target.TryApplyEffect(in spec, out _);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(target.GetAttributeValue(health), Is.EqualTo(5000.5f), "10 001 aplicações de 0,5, abaixo do máximo padrão");
        }
    }
}
#endif
