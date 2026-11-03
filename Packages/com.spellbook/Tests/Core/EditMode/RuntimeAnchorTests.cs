using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>Âncora de teste concreta: a base é genérica e abstrata.</summary>
    public class TestObjectAnchor : RuntimeAnchor<GameObject>
    {
    }

    /// <summary><c>RuntimeAnchor&lt;T&gt;</c>: valor de runtime publicado num asset (ADR 0012; ADR 0037, item 2).</summary>
    public class RuntimeAnchorTests
    {
        private readonly List<Object> _created = new List<Object>();

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null)
                {
                    Object.DestroyImmediate(_created[i]);
                }
            }

            _created.Clear();
        }

        [Test]
        public void Provide_SetsAndNotifiesOnce_SameValueNoEvent()
        {
            TestObjectAnchor anchor = CreateAnchor();
            GameObject value = CreateObject("valor");
            var received = new List<GameObject>();
            anchor.OnChanged += received.Add;

            anchor.Provide(value);
            anchor.Provide(value);

            Assert.That(anchor.Value, Is.SameAs(value));
            Assert.That(anchor.HasValue, Is.True);
            Assert.That(received, Is.EqualTo(new[] { value }));
        }

        [Test]
        public void Unset_OnlyClearsSameValue()
        {
            TestObjectAnchor anchor = CreateAnchor();
            GameObject value = CreateObject("valor");
            GameObject other = CreateObject("outro");
            anchor.Provide(value);
            var received = new List<GameObject>();
            anchor.OnChanged += received.Add;

            anchor.Unset(other);
            GameObject afterOther = anchor.Value;
            anchor.Unset(value);

            Assert.That(afterOther, Is.SameAs(value));
            Assert.That(anchor.HasValue, Is.False);
            Assert.That(received, Is.EqualTo(new GameObject[] { null }));
        }

        [Test]
        public void DestroyedValue_ReadsAsNull()
        {
            TestObjectAnchor anchor = CreateAnchor();
            GameObject value = CreateObject("valor");
            anchor.Provide(value);
            bool hadValue = anchor.HasValue;

            Object.DestroyImmediate(value);

            Assert.That(hadValue, Is.True);
            Assert.That(ReferenceEquals(anchor.Value, null), Is.True, "Value deveria ser nulo de verdade, não um objeto destruído.");
            Assert.That(anchor.HasValue, Is.False);
        }

        [Test]
        public void SessionStart_ClearsAllAnchors()
        {
            TestObjectAnchor first = CreateAnchor();
            TestObjectAnchor second = CreateAnchor();
            first.Provide(CreateObject("primeiro"));
            second.Provide(CreateObject("segundo"));
            int oldSubscriberCalls = 0;
            first.OnChanged += _ => oldSubscriberCalls++;

            RuntimeAnchorRegistry.ResetAllOnSessionStart();
            first.Provide(CreateObject("novo"));

            Assert.That(second.HasValue, Is.False);
            Assert.That(first.HasValue, Is.True);
            Assert.That(oldSubscriberCalls, Is.Zero, "Assinantes da sessão anterior não deveriam ser avisados.");
        }

        [Test]
        public void Value_IsNotSerialized()
        {
            TestObjectAnchor anchor = CreateAnchor();
            anchor.Provide(CreateObject("valor"));

            TestObjectAnchor copy = Track(Object.Instantiate(anchor));

            Assert.That(anchor.HasValue, Is.True);
            Assert.That(copy.HasValue, Is.False);
        }

        private TestObjectAnchor CreateAnchor()
        {
            return Track(ScriptableObject.CreateInstance<TestObjectAnchor>());
        }

        private GameObject CreateObject(string name)
        {
            return Track(new GameObject(name));
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }
    }
}
