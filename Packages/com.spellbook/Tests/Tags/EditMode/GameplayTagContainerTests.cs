using System;
using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;

namespace SpellBook.Tags.Tests
{
    /// <summary>Conjunto de tags: consultas hierárquicas e exatas, montagem, ordem e serialização (CA1 a CA10, CA22).</summary>
    public class GameplayTagContainerTests
    {
        [Serializable]
        private class ContainerHolder
        {
            public GameplayTagContainer container = new GameplayTagContainer();
        }

        private TagTestFactory _factory;
        private GameplayTag _element;
        private GameplayTag _fire;
        private GameplayTag _flame;
        private GameplayTag _ice;

        [SetUp]
        public void SetUp()
        {
            _factory = new TagTestFactory();
            GameplayTagDefinition element = _factory.Create("Elemento");
            GameplayTagDefinition fire = _factory.Create("Elemento.Fogo", element);
            _element = new GameplayTag(element);
            _fire = new GameplayTag(fire);
            _flame = new GameplayTag(_factory.Create("Elemento.Fogo.Chama", fire));
            _ice = new GameplayTag(_factory.Create("Elemento.Gelo", element));
        }

        [TearDown]
        public void TearDown() => _factory.Dispose();

        private GameplayTag NewTag(string name) => new GameplayTag(_factory.Create(name));

        [Test]
        public void HasTag_Hierarchical()
        {
            var container = new GameplayTagContainer(_flame);

            Assert.That(container.HasTag(_flame), Is.True);
            Assert.That(container.HasTag(_fire), Is.True);
            Assert.That(container.HasTag(_element), Is.True);
            Assert.That(container.HasTag(_ice), Is.False);
        }

        [Test]
        public void HasTagExact_NoHierarchy()
        {
            var container = new GameplayTagContainer(_flame);

            Assert.That(container.HasTagExact(_flame), Is.True);
            Assert.That(container.HasTagExact(_fire), Is.False);
            Assert.That(container.HasTagExact(_element), Is.False);
        }

        [Test]
        public void InvalidTag_NeverMatches_InAnyMethod()
        {
            var invalid = new GameplayTag(null);
            var container = new GameplayTagContainer();
            container.Add(_flame);
            var withInvalid = new List<GameplayTag> { invalid };

            Assert.That(container.HasTag(invalid), Is.False);
            Assert.That(container.HasTagExact(invalid), Is.False);
            Assert.That(container.HasAny(withInvalid), Is.False);
            Assert.That(container.HasAll(withInvalid), Is.False);
            Assert.That(container.HasAll(new[] { _flame, invalid }), Is.False);
        }

        [Test]
        public void Add_DuplicateOrInvalid_ReturnsFalse_Unchanged()
        {
            var container = new GameplayTagContainer(_fire);

            bool duplicate = container.Add(_fire);
            bool invalid = container.Add(new GameplayTag(null));

            Assert.That(duplicate, Is.False);
            Assert.That(invalid, Is.False);
            Assert.That(container.Count, Is.EqualTo(1));
            Assert.That(container[0], Is.EqualTo(_fire));
        }

        [Test]
        public void Add_New_ReturnsTrue()
        {
            var container = new GameplayTagContainer();

            bool added = container.Add(_fire);

            Assert.That(added, Is.True);
            Assert.That(container.Count, Is.EqualTo(1));
            Assert.That(container.IsEmpty, Is.False);
            Assert.That(container.HasTagExact(_fire), Is.True);
        }

        [Test]
        public void Remove_OnlyIfPresent()
        {
            var container = new GameplayTagContainer(_fire);

            bool missing = container.Remove(_ice);
            bool present = container.Remove(_fire);
            bool again = container.Remove(_fire);

            Assert.That(missing, Is.False);
            Assert.That(present, Is.True);
            Assert.That(again, Is.False);
            Assert.That(container.IsEmpty, Is.True);
        }

        [Test]
        public void RemoveParent_KeepsChildren()
        {
            var container = new GameplayTagContainer(_fire, _flame);

            bool removed = container.Remove(_fire);

            Assert.That(removed, Is.True);
            Assert.That(container.Count, Is.EqualTo(1));
            Assert.That(container.HasTagExact(_flame), Is.True);
        }

        [Test]
        public void HasAny_HasAll_FourCasesOfC3()
        {
            GameplayTag a = NewTag("A");
            GameplayTag b = NewTag("B");
            GameplayTag c = NewTag("C");
            var container = new GameplayTagContainer(a, b);

            Assert.That(container.HasAny(new[] { b, c }), Is.True);
            Assert.That(container.HasAll(new[] { b, c }), Is.False);
            Assert.That(container.HasAll(new[] { a, b }), Is.True);
            Assert.That(container.HasAny(new[] { c }), Is.False);
        }

        [Test]
        public void HasAll_Empty_IsTrue_HasAny_Empty_IsFalse()
        {
            var container = new GameplayTagContainer(_fire);

            Assert.That(container.HasAll(new GameplayTag[0]), Is.True);
            Assert.That(container.HasAll(null), Is.True);
            Assert.That(container.HasAny(new GameplayTag[0]), Is.False);
            Assert.That(container.HasAny(null), Is.False);
        }

        [Test]
        public void HasAll_HasAny_IgnoreOrder()
        {
            GameplayTag a = NewTag("A");
            GameplayTag b = NewTag("B");
            GameplayTag c = NewTag("C");
            var forward = new GameplayTagContainer(a, b);
            var backward = new GameplayTagContainer(b, a);

            Assert.That(forward.HasAll(new[] { b, a }), Is.True);
            Assert.That(backward.HasAll(new[] { a, b }), Is.True);
            Assert.That(forward.HasAny(new[] { c, b }), Is.EqualTo(forward.HasAny(new[] { b, c })));
            Assert.That(backward.HasAny(new[] { c, a }), Is.True);
        }

        [Test]
        public void Foreach_InsertionOrder()
        {
            var container = new GameplayTagContainer();
            container.Add(_ice);
            container.Add(_element);
            container.Add(_flame);

            var visited = new List<GameplayTag>();
            foreach (GameplayTag tag in container)
            {
                visited.Add(tag);
            }

            Assert.That(visited, Is.EqualTo(new[] { _ice, _element, _flame }));
        }

        [Test]
        public void JsonRoundTrip_SameTagsSameOrder()
        {
            var holder = new ContainerHolder();
            holder.container.Add(_ice);
            holder.container.Add(_flame);
            holder.container.Add(_element);

            string json = JsonUtility.ToJson(holder);
            var restored = JsonUtility.FromJson<ContainerHolder>(json);

            var tags = new List<GameplayTag>();
            foreach (GameplayTag tag in restored.container)
            {
                tags.Add(tag);
            }

            Assert.That(tags, Is.EqualTo(new[] { _ice, _flame, _element }));
        }

        [Test]
        public void FromList_DropsDuplicatesAndInvalid()
        {
            var source = new List<GameplayTag> { _fire, new GameplayTag(null), _ice, _fire };

            var container = new GameplayTagContainer(source);

            Assert.That(container.Count, Is.EqualTo(2));
            Assert.That(container[0], Is.EqualTo(_fire));
            Assert.That(container[1], Is.EqualTo(_ice));
        }

        [Test]
        public void Queries_DoNotAllocate_10000Times()
        {
            var container = new GameplayTagContainer(_flame, _ice);
            var query = new List<GameplayTag> { _fire, _element };
            var array = new[] { _ice };
            bool sink = false;
            sink |= container.HasTag(_fire) | container.HasTagExact(_flame) | container.HasAny(query) | container.HasAll(array);

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    sink |= container.HasTag(_fire);
                    sink |= container.HasTagExact(_flame);
                    sink |= container.HasAny(query);
                    sink |= container.HasAll(array);
                    sink |= container.HasAll(container);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(sink, Is.True);
        }

        [Test]
        public void Foreach_DoesNotAllocate()
        {
            var container = new GameplayTagContainer(_flame, _ice, _element);
            int count = 0;
            foreach (GameplayTag _ in container)
            {
                count++;
            }

            Assert.That(() =>
            {
                foreach (GameplayTag _ in container)
                {
                    count++;
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(count, Is.EqualTo(6));
        }

        [Test]
        public void AcceptsListAndArray_AsArgument()
        {
            var container = new GameplayTagContainer(_flame);
            var list = new List<GameplayTag> { _fire };
            GameplayTag[] array = { _element };
            var other = new GameplayTagContainer(_ice);

            Assert.That(container.HasAll(list), Is.True);
            Assert.That(container.HasAny(array), Is.True);
            Assert.That(container.HasAny(other), Is.False);
        }
    }
}
