using System.Collections.Generic;
using NUnit.Framework;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Core;
using SpellBook.Tags;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary><c>AbilityLoadout</c> (Spec do B-016, Design 3 e P4; R1 e R2 do PRD): lista ordenada que não concede.</summary>
    public class AbilityLoadoutTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
        }

        [TearDown]
        public void TearDown()
        {
            foreach (Object created in _created)
            {
                if (created != null) Object.DestroyImmediate(created);
            }

            _created.Clear();
            _gas.Dispose();
        }

        [Test]
        public void Entries_InListOrder()
        {
            GameplayTag slotQ = _gas.CreateTag("Slot.Q");
            GameplayTag slotE = _gas.CreateTag("Slot.E");
            GameplayTag slotR = _gas.CreateTag("Slot.R");
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility dash = _gas.CreateAbility();
            GameplayAbility heal = _gas.CreateAbility();

            AbilityLoadout loadout = Loadout((slotE, dash), (slotQ, fireball), (slotR, heal));

            Assert.That(loadout.Entries.Count, Is.EqualTo(3));
            Assert.That(loadout.Entries[0].Slot, Is.EqualTo(slotE));
            Assert.That(loadout.Entries[0].Ability, Is.SameAs(dash));
            Assert.That(loadout.Entries[1].Slot, Is.EqualTo(slotQ));
            Assert.That(loadout.Entries[1].Ability, Is.SameAs(fireball));
            Assert.That(loadout.Entries[2].Slot, Is.EqualTo(slotR));
            Assert.That(loadout.Entries[2].Ability, Is.SameAs(heal));
        }

        [Test]
        public void FindEntryBySlot_NoAllocation()
        {
            GameplayTag slotQ = _gas.CreateTag("Slot.Q");
            GameplayTag slotE = _gas.CreateTag("Slot.E");
            GameplayTag absent = _gas.CreateTag("Slot.R");
            GameplayAbility fireball = _gas.CreateAbility();
            GameplayAbility dash = _gas.CreateAbility();
            AbilityLoadout loadout = Loadout((slotQ, fireball), (slotE, dash));
            int found = 0;
            TestDelegate find10000 = () =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    if (loadout.TryFindEntry(slotE, out AbilityLoadout.Entry entry) && entry.Ability == dash) found++;
                    if (loadout.TryFindEntry(absent, out _)) found++;
                    if (loadout.TryFindEntry(default, out _)) found++;
                }
            };
            find10000(); // aquecimento

            bool foundQ = loadout.TryFindEntry(slotQ, out AbilityLoadout.Entry q);
            bool foundAbsent = loadout.TryFindEntry(absent, out AbilityLoadout.Entry missing);

            Assert.That(find10000, Is.Not.AllocatingGCMemory());
            Assert.That(found, Is.EqualTo(20000));
            Assert.That(foundQ, Is.True);
            Assert.That(q.Slot, Is.EqualTo(slotQ));
            Assert.That(q.Ability, Is.SameAs(fireball));
            Assert.That(foundAbsent, Is.False);
            Assert.That(missing.Ability, Is.Null);
        }

        [Test]
        public void DoesNotGrant()
        {
            GameplayTag slotQ = _gas.CreateTag("Slot.Q");
            GameplayAbility fireball = _gas.CreateAbility();
            AbilitySystemComponent asc = _gas.CreateAsc();
            AbilityLoadout loadout = Loadout((slotQ, fireball));

            loadout.TryFindEntry(slotQ, out AbilityLoadout.Entry entry);
            GameplayAbility listed = loadout.Entries[0].Ability;

            Assert.That(listed, Is.SameAs(fireball));
            Assert.That(entry.Ability, Is.SameAs(fireball));
            Assert.That(asc.Abilities.Count, Is.EqualTo(0), "o loadout só lista; quem concede é o jogo (R1)");
            Assert.That(asc.FindSpec(fireball), Is.Null);
        }

        /// <summary>Loadout com as entradas na ordem dada, gravadas pelo <see cref="SerializedObject"/>.</summary>
        private AbilityLoadout Loadout(params (GameplayTag Slot, GameplayAbility Ability)[] entries)
        {
            var loadout = ScriptableObject.CreateInstance<AbilityLoadout>();
            _created.Add(loadout);
            var serialized = new SerializedObject(loadout);
            SerializedProperty list = serialized.FindProperty("entries");
            for (int i = 0; i < entries.Length; i++)
            {
                list.InsertArrayElementAtIndex(i);
                SerializedProperty entry = list.GetArrayElementAtIndex(i);
                entry.FindPropertyRelative("slot.definition").objectReferenceValue = entries[i].Slot.GetDefinition();
                entry.FindPropertyRelative("ability").objectReferenceValue = entries[i].Ability;
            }

            serialized.ApplyModifiedPropertiesWithoutUndo();
            return loadout;
        }
    }
}
