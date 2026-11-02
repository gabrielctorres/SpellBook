#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>Escudo com <see cref="AbsorbWithAttribute"/> e ordem escudo → vida (B-010, DM3: CA4 de runtime, CA7, CA8).</summary>
    public class ShieldTests
    {
        private GasFactory _gas;
        private TimeFactory _time;
        private GameplayTag _damage;
        private GameplayTag _fire;
        private GameplayTag _poison;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _damage = _gas.CreateTag("Dano");
            _fire = _gas.CreateTag("Dano.Fogo", _damage);
            _poison = _gas.CreateTag("Dano.Veneno", _damage);
        }

        [TearDown]
        public void TearDown()
        {
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Shield50_Damage30_Shield20_Health100()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 50f));

            asc.ApplyEffect(Damage(health, 30f));

            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(20f), "escudo absorve os 30");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(100f), "vida intacta");
        }

        [Test]
        public void Shield20_Damage30_Shield0_HealthMinus10()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 20f));
            var reports = new List<AttributeChangeReport>();
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports.Add(report);

            asc.ApplyEffect(Damage(health, 30f));

            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(0f), "escudo gasto até o mínimo");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(90f), "o que sobra vai à vida");
            Assert.That(reports.Count, Is.EqualTo(2), "uma alteração do escudo e uma da vida");
            Assert.That(reports[0].Attribute, Is.SameAs(shield), "escudo primeiro");
            Assert.That(reports[0].RedirectedFrom, Is.SameAs(health), "redirecionada da vida");
            Assert.That(reports[0].Applied, Is.EqualTo(-20f), "escudo aplicado");
            Assert.That(reports[1].Requested, Is.EqualTo(-30f), "pedido original");
            Assert.That(reports[1].Final, Is.EqualTo(-10f), "final da vida");
        }

        [Test]
        public void ShieldBreaks_DepletedOnce_ThenDamageGoesToHealth()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 20f));
            asc.AttributeSet.TryGetState(shield, out AttributeState shieldState);
            int shieldDepleted = 0;
            shieldState.OnDepleted += _ => shieldDepleted++;
            GameplayEffect hit = Damage(health, 30f);

            asc.ApplyEffect(hit);
            asc.ApplyEffect(hit);
            asc.ApplyEffect(hit);

            Assert.That(shieldDepleted, Is.EqualTo(1), "o escudo quebra uma vez");
            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(0f), "escudo continua no mínimo");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(30f), "100 - 10 - 30 - 30");
        }

        [Test]
        public void OnlyConfiguredTags_AndFraction()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            var absorb = new AbsorbWithAttribute { Shield = shield, Fraction = 0.5f };
            absorb.DamageTags.Add(_fire);
            SetProcessors(health, absorb);
            AbilitySystemComponent asc = CreateManualAsc((health, 100f), (shield, 100f));

            asc.ApplyEffect(Damage(health, 40f, _fire));
            float shieldAfterFire = asc.GetAttributeBaseValue(shield);
            float healthAfterFire = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(Damage(health, 40f, _poison));
            asc.ApplyAttributeChange(health, -10f);
            asc.ApplyAttributeChange(health, 25f);

            Assert.That(shieldAfterFire, Is.EqualTo(80f), "metade do fogo no escudo");
            Assert.That(healthAfterFire, Is.EqualTo(80f), "a outra metade na vida");
            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(80f), "veneno, código sem efeito e cura não tocam o escudo");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(55f), "80 - 40 - 10 + 25");
        }

        [Test]
        public void ShieldDepletedBeforeHealthDepleted()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(health, new AbsorbWithAttribute { Shield = shield });
            AbilitySystemComponent asc = CreateManualAsc((health, 10f), (shield, 10f));
            var order = new List<AttributeDefinition>();
            asc.AttributeSet.TryGetState(shield, out AttributeState shieldState);
            asc.AttributeSet.TryGetState(health, out AttributeState healthState);
            shieldState.OnDepleted += s => order.Add(s.Definition);
            healthState.OnDepleted += s => order.Add(s.Definition);

            asc.ApplyEffect(Damage(health, 30f));

            Assert.That(order, Is.EqualTo(new[] { shield, health }), "escudo quebra antes da vida chegar ao mínimo");
        }

        [Test]
        public void CustomProcessor_InPickerAndWorks()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(health, new HalveDamage());
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));

            asc.ApplyEffect(Damage(health, 30f));

            Type custom = typeof(HalveDamage);
            Assert.That(TypeCache.GetTypesDerivedFrom<AttributeChangeProcessor>(), Does.Contain(custom), "o seletor encontra o tipo");
            Assert.That(custom.IsSerializable && !custom.IsAbstract && custom.GetConstructor(Type.EmptyTypes) != null,
                Is.True, "candidato do seletor: concreto, [Serializable], construtor sem parâmetros");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(85f), "o processador próprio roda");
        }

        [Serializable]
        public sealed class HalveDamage : AttributeChangeProcessor
        {
            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                if (change.Delta < 0f) change.Delta *= 0.5f;
            }
        }

        private GameplayEffect Damage(AttributeDefinition attribute, float amount, params GameplayTag[] damageTags)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) },
            });
            foreach (GameplayTag tag in damageTags) effect.DamageTags.Add(tag);
            return effect;
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private static void SetProcessors(AttributeDefinition definition, params AttributeChangeProcessor[] processors)
        {
            using (var so = new SerializedObject(definition))
            {
                SerializedProperty list = so.FindProperty("processors");
                list.arraySize = processors.Length;
                for (int i = 0; i < processors.Length; i++)
                {
                    list.GetArrayElementAtIndex(i).managedReferenceValue = processors[i];
                }

                so.ApplyModifiedPropertiesWithoutUndo();
            }
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
