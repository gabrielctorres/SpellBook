using System.Collections.Generic;
using NUnit.Framework;
using UnityEditor;
using UnityEngine;

namespace SpellBook.Tags.Tests
{
    /// <summary>Acesso público à definição e ao pai de uma tag (CA16, CA17).</summary>
    public class TagDefinitionAccessTests
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
        public void GetDefinition_ReturnsDefinitionUsedToCreate()
        {
            GameplayTagDefinition stunned = CreateDefinition("State.Stunned");
            var tag = new GameplayTag(stunned);

            GameplayTagDefinition definition = tag.GetDefinition();

            Assert.That(definition, Is.SameAs(stunned));
        }

        [Test]
        public void GetDefinition_InvalidTag_ReturnsNull()
        {
            var tag = new GameplayTag(null);

            GameplayTagDefinition definition = tag.GetDefinition();

            Assert.That(definition, Is.Null);
        }

        [Test]
        public void Parent_RootIsNull()
        {
            GameplayTagDefinition root = CreateDefinition("State");

            Assert.That(root.Parent, Is.Null);
        }

        [Test]
        public void Parent_WalkChainUsingPublicApiOnly()
        {
            GameplayTagDefinition state = CreateDefinition("State");
            GameplayTagDefinition debuff = CreateDefinition("State.Debuff", state);
            GameplayTagDefinition slowed = CreateDefinition("State.Debuff.Slowed", debuff);
            var tag = new GameplayTag(slowed);

            var chain = new List<string>();
            for (GameplayTagDefinition current = tag.GetDefinition(); current != null; current = current.Parent)
            {
                chain.Add(current.name);
            }

            Assert.That(chain, Is.EqualTo(new[] { "State.Debuff.Slowed", "State.Debuff", "State" }));
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
