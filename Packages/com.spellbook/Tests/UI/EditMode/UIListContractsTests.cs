using System.Collections.Generic;
using NUnit.Framework;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Contratos de lista (Spec UI-2, Design 1; ADR 0039, item 6): a ligação de lista só repassa itens enquanto ligada,
    /// o <see cref="UIItemContext"/> avisa só quando o valor muda e o "+N" não fica negativo.
    /// </summary>
    public class UIListContractsTests
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
        public void ListBinding_ForwardsOnlyWhileAttached()
        {
            var receiver = new RecordingListReceiver();
            var binding = new NumberListBinding();
            binding.SetReceiver(receiver);
            Object owner = Track(ScriptableObject.CreateInstance<ListTestOwner>());

            binding.Add(1, 10);
            binding.Attach(owner, default);
            binding.Add(1, 10);
            binding.Update(1, 11);
            binding.Remove(1);
            binding.Clear();
            binding.Detach();
            binding.Add(2, 20);
            binding.Clear();

            Assert.That(receiver.Calls, Is.EqualTo(new[] { "add 1:10", "update 1:11", "remove 1", "clear" }));
        }

        [Test]
        public void ItemContext_Changed_OnlyWhenValueChanges()
        {
            UIItemContext context = Track(new GameObject("item")).AddComponent<UIItemContext>();
            ListTestOwner subject = Track(ScriptableObject.CreateInstance<ListTestOwner>());
            int changes = 0;
            context.Changed += () => changes++;

            context.SetSubject(subject);
            context.SetSubject(subject);

            Assert.That(context.Subject, Is.SameAs(subject));
            Assert.That(changes, Is.EqualTo(1));
        }

        [Test]
        public void ListOverflowModel_NegativeIsZero()
        {
            var negative = new ListOverflowModel(-3);
            var three = new ListOverflowModel(3);

            Assert.That(negative.Hidden, Is.Zero);
            Assert.That(three.Hidden, Is.EqualTo(3));
            Assert.That(default(ListOverflowModel).Hidden, Is.Zero);
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private sealed class RecordingListReceiver : IUIListReceiver<int>
        {
            public readonly List<string> Calls = new List<string>();

            public void AddItem(int id, in int item) => Calls.Add($"add {id}:{item}");

            public void UpdateItem(int id, in int item) => Calls.Add($"update {id}:{item}");

            public void RemoveItem(int id) => Calls.Add($"remove {id}");

            public void ClearItems() => Calls.Add("clear");
        }

        // Ligação de teste: expõe os métodos protegidos da lista
        private sealed class NumberListBinding : UIListBinding<int>
        {
            public void Add(int id, int value) => AddItem(id, value);

            public void Update(int id, int value) => UpdateItem(id, value);

            public void Remove(int id) => RemoveItem(id);

            public void Clear() => ClearItems();

            protected override void OnAttach(Object owner)
            {
            }

            protected override void OnDetach()
            {
            }
        }
    }

    /// <summary>Dono e assunto de teste dos contratos de lista, qualquer <c>ScriptableObject</c>.</summary>
    public sealed class ListTestOwner : ScriptableObject
    {
    }
}
