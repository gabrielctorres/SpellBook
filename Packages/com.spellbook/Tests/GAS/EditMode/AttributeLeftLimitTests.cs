using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary><c>IsAtMin</c>, <c>IsAtMax</c>, <c>OnLeftMin</c> e <c>OnLeftMax</c> no <c>AttributeState</c> sem dono (B-010, DM5).</summary>
    public class AttributeLeftLimitTests
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

        [TestCase(0f, true, false)]
        [TestCase(50f, false, false)]
        [TestCase(100f, false, true)]
        public void BornAt_IsAtMinAndIsAtMax_ReflectValue(float initial, bool atMin, bool atMax)
        {
            AttributeState state = CreateState(initial);

            Assert.That(state.IsAtMin, Is.EqualTo(atMin));
            Assert.That(state.IsAtMax, Is.EqualTo(atMax));
        }

        [Test]
        public void LeftMin_OnceOnHeal_NotWhileAway()
        {
            AttributeState health = CreateState(50f);
            int leftMin = 0;
            health.OnLeftMin += _ => leftMin++;

            health.SetBaseValue(0f);
            int atMinCount = leftMin;
            health.SetBaseValue(30f);
            health.SetBaseValue(40f);

            Assert.That(atMinCount, Is.EqualTo(0), "não disparou ao chegar ao mínimo");
            Assert.That(leftMin, Is.EqualTo(1), "uma vez ao sair do mínimo");
            Assert.That(health.IsAtMin, Is.False);
        }

        [Test]
        public void LeftMax_OnceOnLeavingMax()
        {
            AttributeState mana = CreateState(100f);
            int leftMax = 0;
            mana.OnLeftMax += _ => leftMax++;

            mana.SetBaseValue(80f);
            mana.SetBaseValue(70f);

            Assert.That(leftMax, Is.EqualTo(1));
            Assert.That(mana.IsAtMax, Is.False);
        }

        [Test]
        public void MaxToMin_DepletedBeforeLeftMax_MinToMax_ReachedMaxBeforeLeftMin()
        {
            AttributeState state = CreateState(100f);
            var calls = new List<string>();
            state.OnDepleted += _ => calls.Add("depleted");
            state.OnReachedMax += _ => calls.Add("reachedMax");
            state.OnLeftMin += _ => calls.Add("leftMin");
            state.OnLeftMax += _ => calls.Add("leftMax");

            state.SetBaseValue(0f);
            state.SetBaseValue(100f);

            Assert.That(calls, Is.EqualTo(new[] { "depleted", "leftMax", "reachedMax", "leftMin" }));
        }

        /// <summary>Atributo <c>Primary</c> em [0, 100], sem dono (EditMode, sem cena).</summary>
        private AttributeState CreateState(float baseValue)
        {
            AttributeDefinition definition = _gas.CreateAttribute("Health");
            definition.MinValue = 0f;
            definition.MaxValue = 100f;
            return new AttributeState(definition, baseValue, null);
        }
    }
}
