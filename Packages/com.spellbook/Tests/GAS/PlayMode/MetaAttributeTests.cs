#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using UnityEditor;

namespace SpellBook.GAS.Tests
{
    /// <summary>Meta-atributo: a variação final é descartada e a base continua 0 (B-010, DM4: CA9 de runtime, C5).</summary>
    public class MetaAttributeTests
    {
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
        public void Meta40_ConvertedToShieldAndHealth_StaysZero_NoValueEvents()
        {
            AttributeDefinition damage = CreateMeta("DanoRecebido");
            AttributeDefinition health = CreateAttribute("Vida");
            AttributeDefinition shield = CreateAttribute("Escudo");
            SetProcessors(damage, new AbsorbWithAttribute { Shield = shield }, new RedirectAll { Target = health });
            AbilitySystemComponent asc = CreateManualAsc((damage, 0f), (health, 100f), (shield, 30f));
            asc.AttributeSet.TryGetState(damage, out AttributeState damageState);
            int damageValueEvents = 0;
            damageState.OnValueChanged += (_, _) => damageValueEvents++;

            asc.ApplyEffect(Damage(damage, 40f));

            Assert.That(asc.GetAttributeBaseValue(shield), Is.EqualTo(0f), "o escudo absorve 30");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(90f), "os 10 que sobram vão à vida");
            Assert.That(asc.GetAttributeBaseValue(damage), Is.EqualTo(0f), "o meta não guarda valor");
            Assert.That(asc.GetAttributeValue(damage), Is.EqualTo(0f), "valor atual do meta também 0");
            Assert.That(damageValueEvents, Is.EqualTo(0), "nenhum evento de valor do meta");
        }

        [Test]
        public void MetaWithoutProcessors_AffectsNobody()
        {
            AttributeDefinition damage = CreateMeta("DanoRecebido");
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent asc = CreateManualAsc((damage, 0f), (health, 100f));
            asc.AttributeSet.TryGetState(damage, out AttributeState damageState);
            int damageValueEvents = 0;
            damageState.OnValueChanged += (_, _) => damageValueEvents++;

            asc.ApplyEffect(Damage(damage, 40f));
            float applied = asc.ApplyAttributeChange(damage, -25f);

            Assert.That(applied, Is.EqualTo(0f), "nada aplicado pelo código");
            Assert.That(asc.GetAttributeBaseValue(damage), Is.EqualTo(0f), "o meta continua 0");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(100f), "ninguém mais muda");
            Assert.That(damageValueEvents, Is.EqualTo(0), "nenhum evento de valor do meta");
        }

        [Test]
        public void Meta_ReportHasFinalAndZeroApplied()
        {
            AttributeDefinition damage = CreateMeta("DanoRecebido");
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(damage, new RedirectAll { Target = health });
            AbilitySystemComponent asc = CreateManualAsc((damage, 0f), (health, 100f));
            var reports = new List<AttributeChangeReport>();
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports.Add(report);
            GameplayEffect hit = Damage(damage, 40f);

            asc.ApplyEffect(hit);

            Assert.That(reports.Count, Is.EqualTo(2), "a redirecionada e a do meta");
            Assert.That(reports[0].Attribute, Is.SameAs(health), "a vida primeiro");
            Assert.That(reports[0].Applied, Is.EqualTo(-40f), "a vida recebe o golpe");
            Assert.That(reports[1].Attribute, Is.SameAs(damage), "o relatório do meta vem por último");
            Assert.That(reports[1].Requested, Is.EqualTo(-40f), "pedido do efeito");
            Assert.That(reports[1].Final, Is.EqualTo(-40f), "final depois dos processadores");
            Assert.That(reports[1].Applied, Is.EqualTo(0f), "nada aplicado no meta");
            Assert.That(reports[1].Effect, Is.SameAs(hit), "efeito da alteração");
        }

        /// <summary>Passa a variação inteira a outro atributo e não deixa nada no original.</summary>
        [Serializable]
        public sealed class RedirectAll : AttributeChangeProcessor
        {
            public AttributeDefinition Target;

            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                queue.Redirect(Target, change.Delta);
            }
        }

        private GameplayEffect Damage(AttributeDefinition attribute, float amount)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(-amount) },
            });
            return effect;
        }

        private AttributeDefinition CreateAttribute(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = 0f;
            definition.MaxValue = 1000f;
            return definition;
        }

        private AttributeDefinition CreateMeta(string name)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = -1000f;
            definition.MaxValue = 1000f;
            using (var so = new SerializedObject(definition))
            {
                so.FindProperty("isMeta").boolValue = true;
                so.ApplyModifiedPropertiesWithoutUndo();
            }

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
