using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;

namespace SpellBook.Core.Tests
{
    /// <summary>
    /// Base não genérica <c>RuntimeAnchor</c>: valor sem tipo, tipo do valor e evento <c>Changed</c> (ADR 0039, item 5).
    /// </summary>
    public class RuntimeAnchorBaseTests
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
        public void Base_UntypedValue_ValueType_ChangedWithTyped()
        {
            TestObjectAnchor anchor = CreateAnchor();
            RuntimeAnchor untyped = anchor;
            GameObject value = CreateObject("valor");
            var order = new List<string>();
            var untypedSeen = new List<Object>();
            anchor.OnChanged += _ => order.Add("typed");
            untyped.Changed += sender =>
            {
                order.Add("untyped");
                Assert.That(sender, Is.SameAs(anchor));
                untypedSeen.Add(sender.UntypedValue);
            };

            anchor.Provide(value);
            Object afterProvide = untyped.UntypedValue;
            anchor.Unset(value);

            Assert.That(untyped.ValueType, Is.EqualTo(typeof(GameObject)));
            Assert.That(afterProvide, Is.SameAs(value));
            Assert.That(untyped.UntypedValue, Is.Null);
            Assert.That(untypedSeen, Is.EqualTo(new Object[] { value, null }));
            Assert.That(order, Is.EqualTo(new[] { "typed", "untyped", "typed", "untyped" }));
        }

        [Test]
        public void ProvideUntyped_WrongType_Rejected()
        {
            TestObjectAnchor anchor = CreateAnchor();
            RuntimeAnchor untyped = anchor;
            GameObject value = CreateObject("valor");
            TestObjectAnchor wrong = CreateAnchor();
            bool accepted = untyped.ProvideUntyped(value);
            int events = 0;
            untyped.Changed += _ => events++;

            bool rejected = untyped.ProvideUntyped(wrong);
            untyped.UnsetUntyped(wrong);

            Assert.That(accepted, Is.True);
            Assert.That(rejected, Is.False);
            Assert.That(anchor.Value, Is.SameAs(value));
            Assert.That(events, Is.Zero);
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
