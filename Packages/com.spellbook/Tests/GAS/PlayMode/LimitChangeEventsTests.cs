using NUnit.Framework;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.Time;

namespace SpellBook.GAS.Tests
{
    /// <summary>Mudança de limite vinda de outro atributo dispara <c>OnReachedMax</c> (B-005, AR2: CA13, R8).</summary>
    public class LimitChangeEventsTests
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
        public void MaxDropsToValue_FiresReachedMax_AlreadyAtMax_DoesNot()
        {
            // Os dois seguem o mesmo máximo: a Mana está abaixo dele e a Vida já está nele
            AttributeDefinition maxPool = CreateAttribute("MaxPool", 0f, 9999f);
            AttributeDefinition mana = CreateAttribute("Mana", 0f, 9999f);
            AttributeDefinition health = CreateAttribute("Health", 0f, 9999f);
            BindMax(mana, maxPool);
            BindMax(health, maxPool);
            AbilitySystemComponent asc = CreateManualAsc((maxPool, 100f), (mana, 80f), (health, 100f));
            AttributeState manaState = GetState(asc, mana);
            AttributeState healthState = GetState(asc, health);
            int manaReachedMax = 0;
            int healthReachedMax = 0;
            manaState.OnReachedMax += _ => manaReachedMax++;
            healthState.OnReachedMax += _ => healthReachedMax++;

            GetState(asc, maxPool).SetBaseValue(80f);

            Assert.That(manaState.CurrentValue, Is.EqualTo(80f), "a Mana mudou de valor");
            Assert.That(manaReachedMax, Is.EqualTo(1), "o máximo caiu até o valor e não disparou");
            Assert.That(healthState.CurrentValue, Is.EqualTo(80f));
            Assert.That(healthReachedMax, Is.EqualTo(0), "já estava no máximo e disparou de novo");
        }

        private AttributeDefinition CreateAttribute(string name, float min, float max)
        {
            AttributeDefinition definition = _gas.CreateAttribute(name);
            definition.MinValue = min;
            definition.MaxValue = max;
            return definition;
        }

        private static void BindMax(AttributeDefinition definition, AttributeDefinition source)
        {
            definition.MaxSource = LimitSource.Attribute;
            definition.MaxAttribute = source;
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
