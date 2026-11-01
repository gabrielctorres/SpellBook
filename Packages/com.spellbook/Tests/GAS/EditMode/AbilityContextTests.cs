using System;
using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.Testing;
using UnityEngine;

namespace SpellBook.GAS.Tests
{
    /// <summary><see cref="AbilityContext.CreateDerived"/> copia o <c>CustomData</c> (NC6, CA18 a CA20).</summary>
    public class AbilityContextTests
    {
        private static AbilityContext CreateContext()
        {
            return new AbilityContext(null, Vector3.zero);
        }

        // Ability e Level têm setter internal e o GAS não expõe internals aos testes.
        private static void SetInternal(AbilityContext context, string property, object value)
        {
            typeof(AbilityContext).GetProperty(property).SetValue(context, value);
        }

        [Test]
        public void CreateDerived_SeesOriginalKeysAndValues()
        {
            AbilityContext original = CreateContext();
            original.CustomData["crit"] = true;
            original.CustomData["hits"] = 3;

            AbilityContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData["crit"], Is.EqualTo(true));
            Assert.That(derived.CustomData["hits"], Is.EqualTo(3));
            Assert.That(derived.CustomData.Count, Is.EqualTo(2));
        }

        [Test]
        public void CreateDerived_AddingKeyInDerived_DoesNotChangeOriginal()
        {
            AbilityContext original = CreateContext();
            original.CustomData["crit"] = true;
            AbilityContext derived = original.CreateDerived(Vector3.one);

            derived.CustomData["impact"] = 1;
            derived.CustomData["crit"] = false;

            Assert.That(original.CustomData.ContainsKey("impact"), Is.False);
            Assert.That(original.CustomData["crit"], Is.EqualTo(true));
        }

        [Test]
        public void CreateDerived_AddingKeyInOriginal_DoesNotChangeDerived()
        {
            AbilityContext original = CreateContext();
            original.CustomData["crit"] = true;
            AbilityContext derived = original.CreateDerived(Vector3.one);

            original.CustomData["late"] = 1;
            original.CustomData["crit"] = false;

            Assert.That(derived.CustomData.ContainsKey("late"), Is.False);
            Assert.That(derived.CustomData["crit"], Is.EqualTo(true));
        }

        [Test]
        public void CreateDerived_SharesValueReferences()
        {
            AbilityContext original = CreateContext();
            var shared = new List<int> { 1 };
            original.CustomData["list"] = shared;

            AbilityContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData["list"], Is.SameAs(shared));
        }

        [Test]
        public void CreateDerived_KeepsComparer()
        {
            AbilityContext original = CreateContext();
            original.CustomData = new Dictionary<string, object>(StringComparer.OrdinalIgnoreCase) { ["Crit"] = true };

            AbilityContext derived = original.CreateDerived(Vector3.one);

            Assert.That(derived.CustomData.Comparer, Is.SameAs(StringComparer.OrdinalIgnoreCase));
            Assert.That(derived.CustomData.ContainsKey("crit"), Is.True);
        }

        [Test]
        public void CreateDerived_KeepsSourceAbilityAndLevel()
        {
            using var gas = new GasFactory();
            GameplayAbility ability = gas.CreateAbility();
            AbilityContext original = CreateContext();
            SetInternal(original, nameof(AbilityContext.Ability), ability);
            SetInternal(original, nameof(AbilityContext.Level), 4);

            AbilityContext derived = original.CreateDerived(new Vector3(1f, 2f, 3f));

            Assert.That(derived.Source, Is.SameAs(original.Source));
            Assert.That(derived.Ability, Is.SameAs(ability));
            Assert.That(derived.Level, Is.EqualTo(4));
            Assert.That(derived.TargetPosition, Is.EqualTo(new Vector3(1f, 2f, 3f)));
            Assert.That(derived.Spec, Is.Null);
            Assert.That(derived.IsCancelled, Is.False);
        }
    }
}
