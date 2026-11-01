using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.GAS.Tests
{
    /// <summary>Tick periódico sem alocação (B-002, NC2: CA8, C6).</summary>
    public class AttributeAllocationTests
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
        public void Tick_InfinitePeriodicEffect_DoesNotAllocate_300Frames()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            mana.MinValue = 0f;
            mana.MaxValue = 1000000f;
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 0f));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_time.CreateManual());
            GameplayEffect regen = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            regen.Period = 0.1f;
            regen.PeriodicModifiers.Add(new AttributeEffectModifier
            {
                Attribute = mana,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(1f) },
            });
            asc.ApplyEffect(regen);
            for (int i = 0; i < 12; i++) asc.Tick(1f / 60f); // aquecimento, com ao menos um tique
            float before = asc.GetAttributeValue(mana);

            Assert.That(() =>
            {
                for (int i = 0; i < 300; i++) asc.Tick(1f / 60f);
            }, Is.Not.AllocatingGCMemory());
            Assert.That(asc.GetAttributeValue(mana), Is.GreaterThan(before), "o efeito periódico não tiquetou na medição");
        }
    }
}
