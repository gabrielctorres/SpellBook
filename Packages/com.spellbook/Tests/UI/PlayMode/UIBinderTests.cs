using System;
using System.Collections.Generic;
using System.Reflection;
using System.Text.RegularExpressions;
using NUnit.Framework;
using SpellBook.Core;
using UnityEngine;
using UnityEngine.TestTools;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Binder base no Play: liga e desenha por evento (CA1, parte), religa pela âncora (CA5), mostra o vazio sem lançar
    /// (CA6) e entrega o modelo a uma view própria (CA11). ADR 0039, itens 1, 3 e 5.
    /// </summary>
    public class UIBinderTests
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
        public void OnEnable_BindsAndRendersCurrent_NoUpdate()
        {
            CountOwner owner = CreateOwner("ator", 7);
            CountBinder binder = CreateBinder(ActorResolver.ForSelfOrParent(), new CountSource(), out RecordingView view);
            binder.transform.SetParent(owner.transform);

            binder.gameObject.SetActive(true);
            int rendersOnEnable = view.Renders;
            int valueOnEnable = view.Last.Value;
            owner.Set(9);

            Assert.That(rendersOnEnable, Is.EqualTo(1));
            Assert.That(valueOnEnable, Is.EqualTo(7));
            Assert.That(view.Renders, Is.EqualTo(2));
            Assert.That(view.Last.Value, Is.EqualTo(9));
            for (Type type = typeof(CountBinder).BaseType; type != typeof(MonoBehaviour); type = type.BaseType)
            {
                const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.DeclaredOnly;
                Assert.That(type.GetMethod("Update", flags), Is.Null, $"{type.Name} tem Update");
                Assert.That(type.GetMethod("LateUpdate", flags), Is.Null, $"{type.Name} tem LateUpdate");
            }
        }

        [Test]
        public void AnchorChanges_Rebinds_OldOwnerListenerRemoved()
        {
            CountOwnerAnchor anchor = Create<CountOwnerAnchor>();
            CountOwner first = CreateOwner("primeiro", 1);
            CountOwner second = CreateOwner("segundo", 2);
            var source = new CountSource();
            CountBinder binder = CreateBinder(ActorResolver.ForAnchor(anchor), source, out RecordingView view);

            binder.gameObject.SetActive(true);
            bool emptyBeforeSpawn = !view.Last.HasValue;
            anchor.Provide(first);
            int valueFirst = view.Last.Value;
            anchor.Provide(second);
            first.Set(5);
            int valueAfterOldChanged = view.Last.Value;
            second.Set(6);
            int valueAfterNewChanged = view.Last.Value;
            anchor.Unset(second);

            Assert.That(emptyBeforeSpawn, Is.True, "âncora vazia deveria mostrar o estado vazio");
            Assert.That(valueFirst, Is.EqualTo(1));
            Assert.That(valueAfterOldChanged, Is.EqualTo(2), "o dono antigo ainda chega à view");
            Assert.That(valueAfterNewChanged, Is.EqualTo(6));
            Assert.That(first.Listeners, Is.Zero);
            Assert.That(second.Listeners, Is.Zero);
            Assert.That(view.Last.HasValue, Is.False, "âncora esvaziada deveria mostrar o estado vazio");
            Assert.That(source.CreatedBindings, Is.EqualTo(1));
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void Disable_Detaches_NoMoreRenders()
        {
            CountOwnerAnchor anchor = Create<CountOwnerAnchor>();
            CountOwner owner = CreateOwner("ator", 3);
            CountOwner other = CreateOwner("outro", 4);
            anchor.Provide(owner);
            CountBinder binder = CreateBinder(ActorResolver.ForAnchor(anchor), new CountSource(), out RecordingView view);
            binder.gameObject.SetActive(true);

            binder.enabled = false;
            owner.Set(8);
            anchor.Provide(other);

            Assert.That(view.Renders, Is.EqualTo(1));
            Assert.That(view.Last.Value, Is.EqualTo(3));
            Assert.That(owner.Listeners, Is.Zero);
            Assert.That(other.Listeners, Is.Zero);
        }

        [Test]
        public void OwnerMissing_RendersEmpty_WarnsOnce_NoException()
        {
            CountBinder binder = CreateBinder(ActorResolver.ForSelfOrParent(), new CountSource(), out RecordingView view);
            LogAssert.Expect(LogType.Warning, new Regex("CountOwner"));

            Assert.DoesNotThrow(() =>
            {
                binder.gameObject.SetActive(true);
                binder.enabled = false;
                binder.enabled = true;
                binder.Rebind();
            });

            Assert.That(view.Renders, Is.EqualTo(3));
            Assert.That(view.Last.HasValue, Is.False);
            LogAssert.NoUnexpectedReceived();
        }

        [Test]
        public void CustomView_ReceivesModel_SourceUnchanged()
        {
            CountOwner owner = CreateOwner("ator", 3);
            var source = new CountSource();
            CountBinder binder = CreateBinder(ActorResolver.ForExplicit(owner), source, out RecordingView first);
            binder.gameObject.SetActive(true);
            RecordingView second = CreateObject("outra view").AddComponent<RecordingView>();

            binder.SetView(second);
            owner.Set(4);

            Assert.That(second.Renders, Is.EqualTo(2));
            Assert.That(second.Last.Value, Is.EqualTo(4));
            Assert.That(first.Renders, Is.EqualTo(1));
            Assert.That(first.Last.Value, Is.EqualTo(3));
            Assert.That(binder.View, Is.SameAs(second));
            Assert.That(binder.Source, Is.SameAs(source));
            Assert.That(source.CreatedBindings, Is.EqualTo(1));
        }

        [Test]
        public void Rebind_SameBinding_NoAllocation()
        {
            CountOwnerAnchor anchor = Create<CountOwnerAnchor>();
            CountOwner owner = CreateOwner("ator", 5);
            anchor.Provide(owner);
            var source = new CountSource();
            CountBinder binder = CreateBinder(ActorResolver.ForAnchor(anchor), source, out RecordingView view);
            binder.gameObject.SetActive(true);
            binder.Rebind();

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    binder.Rebind();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(source.CreatedBindings, Is.EqualTo(1));
            Assert.That(owner.Listeners, Is.EqualTo(1));
            Assert.That(view.Last.Value, Is.EqualTo(5));
        }

        private CountBinder CreateBinder(ActorResolver resolver, CountSource source, out RecordingView view)
        {
            GameObject gameObject = CreateObject("binder");
            gameObject.SetActive(false);
            CountBinder binder = gameObject.AddComponent<CountBinder>();
            view = gameObject.AddComponent<RecordingView>();
            binder.SetOwner(resolver);
            binder.SetSource(source);
            binder.SetView(view);
            return binder;
        }

        private CountOwner CreateOwner(string name, int value)
        {
            CountOwner owner = CreateObject(name).AddComponent<CountOwner>();
            owner.Set(value);
            return owner;
        }

        private GameObject CreateObject(string name)
        {
            var gameObject = new GameObject(name);
            _created.Add(gameObject);
            return gameObject;
        }

        private T Create<T>() where T : ScriptableObject
        {
            T instance = ScriptableObject.CreateInstance<T>();
            _created.Add(instance);
            return instance;
        }

        public readonly struct CountModel
        {
            public readonly int Value;
            public readonly bool HasValue;

            public CountModel(int value)
            {
                Value = value;
                HasValue = true;
            }
        }

        public sealed class CountOwner : MonoBehaviour
        {
            public event Action Changed;

            public int Value { get; private set; }

            public int Listeners => Changed?.GetInvocationList().Length ?? 0;

            public void Set(int value)
            {
                Value = value;
                Changed?.Invoke();
            }
        }

        public sealed class CountOwnerAnchor : RuntimeAnchor<CountOwner>
        {
        }

        [Serializable]
        public sealed class CountSource : UISource<CountModel>
        {
            public int CreatedBindings;

            public override Type OwnerType => typeof(CountOwner);

            public override UIBinding<CountModel> CreateBinding()
            {
                CreatedBindings++;
                return new CountBinding();
            }

            public override CountModel CreatePreview(in UIPreviewContext context) => new CountModel(50);
        }

        public sealed class CountBinding : UIBinding<CountModel>
        {
            private readonly Action _onChanged;
            private CountOwner _owner;

            public CountBinding() => _onChanged = OnChanged;

            protected override void OnAttach(Object owner)
            {
                _owner = (CountOwner)owner;
                _owner.Changed += _onChanged;
                OnChanged();
            }

            protected override void OnDetach() => _owner.Changed -= _onChanged;

            private void OnChanged() => Publish(new CountModel(_owner.Value));
        }

        public sealed class RecordingView : MonoBehaviour, IView<CountModel>
        {
            public int Renders;
            public CountModel Last;

            public void Render(in CountModel model)
            {
                Renders++;
                Last = model;
            }
        }

        public sealed class CountBinder : UIBinder<CountModel>
        {
        }
    }
}
