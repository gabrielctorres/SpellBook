#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Testing;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Lista genérica (Spec UI-2, Design 1; ADR 0039, item 6): itens por id em ordem de chegada, criados e devolvidos só
    /// pelo <c>SpawnerAsset</c>, reaproveitados quando só o conteúdo muda, <c>maxVisible</c> com "+N" e promoção do
    /// oculto mais antigo, dono no <see cref="UIItemContext"/>, spawner sem instância pulado com aviso e sem alocar.
    /// </summary>
    public class UIListBinderTests
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
        public void Items_FromSpawner_ArrivalOrder()
        {
            Rig rig = CreateRig(maxVisible: 0);

            rig.Owner.Sink.Add(5, 50);
            rig.Owner.Sink.Add(2, 20);
            rig.Owner.Sink.Add(9, 90);

            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(3));
            Assert.That(VisibleValues(rig), Is.EqualTo(new[] { 50, 20, 90 }));
        }

        [Test]
        public void Remove_Releases_LiveCountStable()
        {
            Rig rig = CreateRig(maxVisible: 0);
            rig.Owner.Sink.Add(1, 10);
            rig.Owner.Sink.Add(2, 20);
            rig.Owner.Sink.Add(3, 30);

            rig.Owner.Sink.Remove(2);
            int[] afterRemove = VisibleValues(rig);
            var instances = new HashSet<GameObject>();
            for (int i = 0; i < 20; i++)
            {
                rig.Owner.Sink.Add(100 + i, i);
                instances.Add(rig.Spawner.Live[rig.Spawner.Live.Count - 1]);
                rig.Owner.Sink.Remove(100 + i);
            }

            Assert.That(afterRemove, Is.EqualTo(new[] { 10, 30 }));
            Assert.That(rig.Spawner.ReleaseCount, Is.EqualTo(21));
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(2), "a contagem de vivos não deveria crescer");
            Assert.That(instances.Count, Is.EqualTo(1), "o pool deveria reaproveitar a instância devolvida");
        }

        [Test]
        public void ContentChange_ReusesItem()
        {
            Rig rig = CreateRig(maxVisible: 0);
            rig.Owner.Sink.Add(1, 10);
            GameObject first = rig.Spawner.Live[0];

            rig.Owner.Sink.Update(1, 11);

            ItemView view = first.GetComponent<ItemView>();
            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(1), "mudar o conteúdo não deveria criar outro item");
            Assert.That(rig.Spawner.Live[0], Is.SameAs(first));
            Assert.That(view.Renders, Is.EqualTo(2));
            Assert.That(view.Last.Value, Is.EqualTo(11));
        }

        [Test]
        public void MaxVisible_HiddenCount_PromotesOldestHidden()
        {
            Rig rig = CreateRig(maxVisible: 2);
            for (int id = 1; id <= 4; id++)
            {
                rig.Owner.Sink.Add(id, id * 10);
            }

            int[] full = VisibleValues(rig);
            int hiddenFull = rig.Overflow.Last.Hidden;
            rig.Owner.Sink.Remove(1);
            int[] promoted = VisibleValues(rig);
            int hiddenPromoted = rig.Overflow.Last.Hidden;
            rig.Owner.Sink.Remove(4);

            Assert.That(full, Is.EqualTo(new[] { 10, 20 }));
            Assert.That(hiddenFull, Is.EqualTo(2));
            Assert.That(promoted, Is.EqualTo(new[] { 20, 30 }), "o oculto mais antigo deveria ganhar objeto, no fim");
            Assert.That(hiddenPromoted, Is.EqualTo(1));
            Assert.That(rig.Overflow.Last.Hidden, Is.Zero);
            Assert.That(rig.Spawner.SpawnCount, Is.EqualTo(3), "remover um oculto não deveria criar objeto");
            Assert.That(VisibleValues(rig), Is.EqualTo(new[] { 20, 30 }));
        }

        [Test]
        public void ItemContext_OwnerFromBinder()
        {
            Rig rig = CreateRig(maxVisible: 0);

            rig.Owner.Sink.Add(1, 10);
            UIItemContext context = rig.Spawner.Live[0].GetComponent<UIItemContext>();
            Object whileListed = context.Owner;
            rig.Owner.Sink.Remove(1);

            Assert.That(whileListed, Is.SameAs(rig.Owner));
            Assert.That(context.Owner, Is.Null, "o item devolvido não deveria guardar o dono");
        }

        [Test]
        public void NullSpawn_SkipsWithWarning()
        {
            Rig rig = CreateRig(maxVisible: 0);
            rig.Spawner.ReturnsNull = true;
            LogAssert.Expect(LogType.Warning, new Regex("spawner"));

            rig.Owner.Sink.Add(1, 10);
            rig.Owner.Sink.Add(2, 20);
            rig.Owner.Sink.Update(1, 11);

            Assert.That(rig.Spawner.Live, Is.Empty);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void AddRemove1000_DoNotAllocate()
        {
            Rig rig = CreateRig(maxVisible: 3);
            Churn(rig, 0, 100);

            Assert.That(() => Churn(rig, 100, 1000), Is.Not.AllocatingGCMemory());
            Assert.That(rig.Spawner.Live.Count, Is.EqualTo(3));
            Assert.That(rig.Overflow.Last.Hidden, Is.EqualTo(1));
        }

        // Só a ligação mexe na lista: chamada direta fora do Play criaria itens salvos na cena
        [Test]
        public void Receiver_NotPublicOnBinder()
        {
            var exposed = new List<string>();
            foreach (MethodInfo method in typeof(TestListBinder).GetMethods(BindingFlags.Public | BindingFlags.Instance))
            {
                if (method.Name == "AddItem" || method.Name == "UpdateItem" || method.Name == "RemoveItem" ||
                    method.Name == "ClearItems")
                {
                    exposed.Add(method.Name);
                }
            }

            Assert.That(exposed, Is.Empty, "o binder deveria receber a lista só pela ligação");
        }

        // Mantém de quatro a cinco itens com três visíveis: passa por acréscimo, mudança, oculto e promoção
        private static void Churn(Rig rig, int from, int count)
        {
            for (int id = from; id < from + count; id++)
            {
                rig.Owner.Sink.Add(id, id);
                rig.Owner.Sink.Update(id, id + 1);
                if (id >= 4)
                {
                    rig.Owner.Sink.Remove(id - 4);
                }
            }
        }

        private static int[] VisibleValues(Rig rig)
        {
            var values = new List<int>();
            foreach (Transform child in rig.Container)
            {
                if (child.gameObject.activeSelf)
                {
                    values.Add(child.GetComponent<ItemView>().Last.Value);
                }
            }

            return values.ToArray();
        }

        private Rig CreateRig(int maxVisible)
        {
            var rig = new Rig();
            rig.Spawner = Track(ScriptableObject.CreateInstance<RecordingSpawnerAsset>());
            rig.Owner = Track(new GameObject("ator")).AddComponent<ListOwner>();
            rig.Container = Track(new GameObject("itens")).transform;

            GameObject prefab = Track(new GameObject("item"));
            prefab.SetActive(false);
            prefab.AddComponent<ItemView>();
            prefab.AddComponent<UIItemContext>();

            GameObject host = Track(new GameObject("lista"));
            host.SetActive(false);
            rig.Overflow = host.AddComponent<OverflowView>();
            rig.Binder = host.AddComponent<TestListBinder>();
            rig.Binder.SetOwner(ActorResolver.ForExplicit(rig.Owner));
            rig.Binder.SetSource(new TestListSource());
            Modify(rig.Binder, so =>
            {
                so.FindProperty("itemPrefab").objectReferenceValue = prefab;
                so.FindProperty("spawner").objectReferenceValue = rig.Spawner;
                so.FindProperty("container").objectReferenceValue = rig.Container;
                so.FindProperty("maxVisible").intValue = maxVisible;
                so.FindProperty("overflowView").objectReferenceValue = rig.Overflow;
            });
            host.SetActive(true);
            return rig;
        }

        private T Track<T>(T obj) where T : Object
        {
            _created.Add(obj);
            return obj;
        }

        private static void Modify(Object target, Action<SerializedObject> edit)
        {
            var serialized = new SerializedObject(target);
            edit(serialized);
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private sealed class Rig
        {
            public RecordingSpawnerAsset Spawner;
            public ListOwner Owner;
            public Transform Container;
            public OverflowView Overflow;
            public TestListBinder Binder;
        }

        public readonly struct TestItem
        {
            public TestItem(int value)
            {
                Value = value;
            }

            public int Value { get; }
        }

        /// <summary>Dono de teste: a ligação se põe nele ao ligar, para o teste mexer na lista.</summary>
        public sealed class ListOwner : MonoBehaviour
        {
            public TestListBinding Sink;
        }

        /// <summary>View de item de teste: guarda o último modelo e quantas vezes desenhou.</summary>
        public sealed class ItemView : MonoBehaviour, IView<TestItem>
        {
            public TestItem Last;
            public int Renders;

            public void Render(in TestItem model)
            {
                Last = model;
                Renders++;
            }
        }

        /// <summary>View do "+N" de teste.</summary>
        public sealed class OverflowView : MonoBehaviour, IView<ListOverflowModel>
        {
            public ListOverflowModel Last;

            public void Render(in ListOverflowModel model) => Last = model;
        }

        public sealed class TestListBinder : UIListBinder<TestItem>
        {
        }

        public sealed class TestListBinding : UIListBinding<TestItem>
        {
            private ListOwner _owner;

            public void Add(int id, int value) => AddItem(id, new TestItem(value));

            public void Update(int id, int value) => UpdateItem(id, new TestItem(value));

            public void Remove(int id) => RemoveItem(id);

            protected override void OnAttach(Object owner)
            {
                _owner = (ListOwner)owner;
                _owner.Sink = this;
            }

            protected override void OnDetach() => _owner.Sink = null;
        }

        [Serializable]
        public sealed class TestListSource : UIListSource<TestItem>
        {
            public override Type OwnerType => typeof(ListOwner);

            public override UIListBinding<TestItem> CreateBinding() => new TestListBinding();

            public override void CreatePreview(in UIPreviewContext context, List<TestItem> items)
            {
            }
        }
    }
}
#endif
