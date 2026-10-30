using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Tags.Tests
{
    /// <summary>Match hierárquico e igualdade de <see cref="GameplayTag"/> (CA31, CA32).</summary>
    public class GameplayTagTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            foreach (Object obj in _created)
            {
                Object.DestroyImmediate(obj);
            }

            _created.Clear();
        }

        [Test]
        public void Matches_SameDefinition_IsTrue()
        {
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned");

            bool matches = new GameplayTag(stunned).Matches(new GameplayTag(stunned));

            Assert.That(matches, Is.True);
        }

        [Test]
        public void Matches_ChildAgainstParent_IsTrue()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned", state);

            bool matches = new GameplayTag(stunned).Matches(new GameplayTag(state));

            Assert.That(matches, Is.True);
        }

        [Test]
        public void Matches_GrandchildAgainstGrandparent_IsTrue()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition debuff = CreateDefinition("State.Debuff", state);
            GameplayTagDefinition slowed = CreateDefinition("State.Debuff.Slowed", debuff);

            bool matches = new GameplayTag(slowed).Matches(new GameplayTag(state));

            Assert.That(matches, Is.True);
        }

        [Test]
        public void Matches_ParentAgainstChild_IsFalse()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned", state);

            bool matches = new GameplayTag(state).Matches(new GameplayTag(stunned));

            Assert.That(matches, Is.False);
        }

        [Test]
        public void Matches_Siblings_IsFalse()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned", state);
            GameplayTagDefinition rooted = CreateDefinition("State.Rooted", state);

            bool matches = new GameplayTag(stunned).Matches(new GameplayTag(rooted));

            Assert.That(matches, Is.False);
        }

        [Test]
        public void Matches_InvalidTag_NeverMatches()
        {
            var valid = new GameplayTag(CreateDefinition("State"));
            var invalid = new GameplayTag(null);

            Assert.That(invalid.IsValid, Is.False);
            Assert.That(invalid.Matches(valid), Is.False);
            Assert.That(valid.Matches(invalid), Is.False);
            Assert.That(invalid.Matches(invalid), Is.False);
        }

        [Test]
        public void Equality_SameDefinition_IsEqual()
        {
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned");
            var a = new GameplayTag(stunned);
            var b = new GameplayTag(stunned);

            Assert.That(a == b, Is.True);
            Assert.That(a != b, Is.False);
            Assert.That(a.Equals(b), Is.True);
            Assert.That(a.GetHashCode(), Is.EqualTo(b.GetHashCode()));
        }

        [Test]
        public void Equality_DefinitionsWithSameName_AreDifferent()
        {
            var a = new GameplayTag(CreateDefinition("State.Stunned"));
            var b = new GameplayTag(CreateDefinition("State.Stunned"));

            Assert.That(a == b, Is.False);
            Assert.That(a != b, Is.True);
        }

        [Test]
        public void Equality_ChildAndParent_AreDifferent()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned", state);

            Assert.That(new GameplayTag(stunned) == new GameplayTag(state), Is.False);
        }

        [Test]
        public void Name_ComesFromDefinitionOrNone()
        {
            var valid = new GameplayTag(CreateDefinition("State.Stunned"));
            var invalid = new GameplayTag(null);

            Assert.That(valid.Name, Is.EqualTo("State.Stunned"));
            Assert.That(invalid.Name, Is.EqualTo("None"));
        }

        private GameplayTagDefinition CreateDefinition(string name, GameplayTagDefinition parent = null)
        {
            var definition = ScriptableObject.CreateInstance<GameplayTagDefinition>();
            definition.name = name;
            _created.Add(definition);

            if (parent != null)
            {
                using (var serialized = new SerializedObject(definition))
                {
                    serialized.FindProperty("parent").objectReferenceValue = parent;
                    serialized.ApplyModifiedPropertiesWithoutUndo();
                }
            }

            return definition;
        }
    }
}
