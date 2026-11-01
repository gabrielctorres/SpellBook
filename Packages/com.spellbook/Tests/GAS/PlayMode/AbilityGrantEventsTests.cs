#if UNITY_EDITOR
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Testing;

namespace SpellBook.GAS.Tests
{
    /// <summary>Eventos de concessão e remoção de habilidade (B-003, EA5: CA16).</summary>
    public class AbilityGrantEventsTests
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

        [Test]
        public void Give_FiresGrantedOnce_GiveAgainDoesNot()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            var granted = new List<AbilitySpec>();
            asc.OnAbilityGranted += granted.Add;

            AbilitySpec spec = asc.GiveAbility(ability);
            AbilitySpec again = asc.GiveAbility(ability);

            Assert.That(again, Is.SameAs(spec));
            Assert.That(granted, Is.EqualTo(new[] { spec }), "conceder de novo disparou outra vez, ou a primeira não disparou");
            Assert.That(asc.Abilities, Does.Contain(spec), "o evento disparou antes de o spec entrar na lista");
        }

        [Test]
        public void Remove_FiresRemovedOnce()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GameplayAbility ability = _gas.CreateAbility();
            AbilitySpec spec = asc.GiveAbility(ability);
            var removed = new List<AbilitySpec>();
            bool listedDuringEvent = true;
            asc.OnAbilityRemoved += s =>
            {
                removed.Add(s);
                listedDuringEvent = asc.FindSpec(ability) != null;
            };

            asc.RemoveAbility(ability);
            asc.RemoveAbility(ability);

            Assert.That(removed, Is.EqualTo(new[] { spec }));
            Assert.That(listedDuringEvent, Is.False, "o evento disparou antes de o spec sair da lista");
        }
    }
}
#endif
