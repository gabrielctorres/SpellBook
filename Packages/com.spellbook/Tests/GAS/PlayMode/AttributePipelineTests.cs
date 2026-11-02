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
    /// <summary>Pipeline de pré-alteração e relatório (B-010, DM1a: CA1, CA2, CA3 sem redirecionamento, CA5).</summary>
    public partial class AttributePipelineTests
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
        public void NoProcessors_SameValuesAsBefore()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            GameplayEffect hit = Instant((health, ModifierOperation.Add, -30f));
            GameplayEffect scale = Instant((health, ModifierOperation.Multiply, 0.5f), (health, ModifierOperation.Add, 10f));
            GameplayEffect overkill = Instant((health, ModifierOperation.Add, -5000f));
            GameplayEffect set = Instant((health, ModifierOperation.Override, 40f));
            GameplayEffect regen = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            regen.Period = 1f;
            regen.PeriodicModifiers.Add(Modifier(health, ModifierOperation.Add, 5f));

            asc.ApplyEffect(hit);
            float afterHit = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(scale);
            float afterScale = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(overkill);
            float afterOverkill = asc.GetAttributeBaseValue(health);
            asc.ApplyEffect(set);
            asc.ApplyEffect(regen);
            asc.Tick(1f);
            float afterTick = asc.GetAttributeBaseValue(health);
            float applied = asc.ApplyAttributeChange(health, -15f);

            Assert.That(afterHit, Is.EqualTo(70f), "instantâneo");
            Assert.That(afterScale, Is.EqualTo(120f).Within(Tolerance), "fórmula única"); // (70 + 10) x 1,5
            Assert.That(afterOverkill, Is.EqualTo(0f), "limite mínimo");
            Assert.That(afterTick, Is.EqualTo(45f), "override e tique");
            Assert.That(applied, Is.EqualTo(-15f), "ApplyAttributeChange sem processadores");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(30f), "base depois do código");
        }

        [Test]
        public void Order_AB_vs_BA_DiffersAsExpected_Deterministic()
        {
            AttributeDefinition ab = CreateAttribute("VidaAB");
            AttributeDefinition ba = CreateAttribute("VidaBA");
            SetProcessors(ab, new AddDelta { Amount = 10f }, new ScaleDelta { Factor = 0.5f });
            SetProcessors(ba, new ScaleDelta { Factor = 0.5f }, new AddDelta { Amount = 10f });
            AbilitySystemComponent asc = CreateManualAsc((ab, 100f), (ba, 100f));

            asc.ApplyAttributeChange(ab, -20f);
            asc.ApplyAttributeChange(ab, -20f);
            asc.ApplyAttributeChange(ba, -20f);
            asc.ApplyAttributeChange(ba, -20f);

            Assert.That(asc.GetAttributeBaseValue(ab), Is.EqualTo(90f), "A depois B: (-20 + 10) x 0,5 = -5, duas vezes");
            Assert.That(asc.GetAttributeBaseValue(ba), Is.EqualTo(100f), "B depois A: -20 x 0,5 + 10 = 0, duas vezes");
        }

        [Test]
        public void Processor_Reduce_Increase_Annul()
        {
            AttributeDefinition reduced = CreateAttribute("Reduzida");
            AttributeDefinition increased = CreateAttribute("Aumentada");
            AttributeDefinition annulled = CreateAttribute("Anulada");
            SetProcessors(reduced, new ScaleDelta { Factor = 0.5f });
            SetProcessors(increased, null, new ScaleDelta { Factor = 2f });
            SetProcessors(annulled, new Annul());
            AbilitySystemComponent asc = CreateManualAsc((reduced, 100f), (increased, 100f), (annulled, 100f));
            GameplayEffect hit = Instant(
                (reduced, ModifierOperation.Add, -40f),
                (increased, ModifierOperation.Add, -40f),
                (annulled, ModifierOperation.Add, -40f));

            asc.ApplyEffect(hit);

            Assert.That(asc.GetAttributeBaseValue(reduced), Is.EqualTo(80f), "reduz");
            Assert.That(asc.GetAttributeBaseValue(increased), Is.EqualTo(20f), "aumenta, pulando a entrada nula");
            Assert.That(asc.GetAttributeBaseValue(annulled), Is.EqualTo(100f), "anula");
        }

        [Test]
        public void FinalZero_NoValueChanged()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(health, new Annul());
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            int valueChanges = 0;
            asc.AttributeSet.TryGetState(health, out AttributeState state);
            state.OnValueChanged += (_, _) => valueChanges++;
            var reports = new List<AttributeChangeReport>();
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports.Add(report);

            float applied = asc.ApplyAttributeChange(health, -30f);

            Assert.That(valueChanges, Is.EqualTo(0), "OnValueChanged");
            Assert.That(applied, Is.EqualTo(0f), "devolvido");
            Assert.That(reports.Count, Is.EqualTo(1), "relatórios");
            Assert.That(reports[0].Requested, Is.EqualTo(-30f), "Requested");
            Assert.That(reports[0].Final, Is.EqualTo(0f), "Final");
            Assert.That(reports[0].Applied, Is.EqualTo(0f), "Applied");
        }

        [Test]
        public void DurationModifiers_NotProcessed()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(health, new Annul());
            AbilitySystemComponent asc = CreateManualAsc((health, 100f));
            int reports = 0;
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports++;
            GameplayEffect buff = _gas.CreateDurationEffect(10f);
            buff.Modifiers.Add(Modifier(health, ModifierOperation.Add, -30f));

            asc.ApplyEffect(buff);

            Assert.That(asc.GetAttributeValue(health), Is.EqualTo(70f), "valor atual com o modificador persistente");
            Assert.That(asc.GetAttributeBaseValue(health), Is.EqualTo(100f), "base");
            Assert.That(reports, Is.EqualTo(0), "relatórios");
        }

        [Test]
        public void Report_RequestedFinalApplied_EffectAndContext()
        {
            AttributeDefinition health = CreateAttribute("Vida");
            SetProcessors(health, new ScaleDelta { Factor = 2f });
            AbilitySystemComponent asc = CreateManualAsc((health, 30f));
            GameplayEffect hit = Instant((health, ModifierOperation.Add, -20f));
            var reports = new List<AttributeChangeReport>();
            asc.OnAttributeChangeApplied += (in AttributeChangeReport report) => reports.Add(report);

            asc.ApplyEffect(hit);

            Assert.That(reports.Count, Is.EqualTo(1), "relatórios");
            AttributeChangeReport report = reports[0];
            Assert.That(report.IsValid, Is.True, "IsValid");
            Assert.That(report.Attribute, Is.SameAs(health), "Attribute");
            Assert.That(report.Requested, Is.EqualTo(-20f), "Requested");
            Assert.That(report.Final, Is.EqualTo(-40f), "Final");
            Assert.That(report.Applied, Is.EqualTo(-30f), "Applied, limitado ao mínimo");
            Assert.That(report.Effect, Is.SameAs(hit), "Effect");
            Assert.That(report.Context.Target, Is.SameAs(asc), "Context");
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

        private GameplayEffect Instant(params (AttributeDefinition Attribute, ModifierOperation Operation, float Value)[] modifiers)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            foreach (var mod in modifiers) effect.Modifiers.Add(Modifier(mod.Attribute, mod.Operation, mod.Value));
            return effect;
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

        [Serializable]
        private sealed class AddDelta : AttributeChangeProcessor
        {
            public float Amount;

            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                change.Delta += Amount;
            }
        }

        [Serializable]
        private sealed class ScaleDelta : AttributeChangeProcessor
        {
            public float Factor;

            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                change.Delta *= Factor;
            }
        }

        [Serializable]
        private sealed class Annul : AttributeChangeProcessor
        {
            public override void Process(ref AttributeChange change, in EffectContext context, AttributeChangeQueue queue)
            {
                change.Delta = 0f;
            }
        }
    }
}
#endif
