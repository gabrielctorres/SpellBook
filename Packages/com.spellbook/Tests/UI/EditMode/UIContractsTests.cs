using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>Contratos do <c>SpellBook.UI</c>: resolvedor do dono (CA6, C6) e ciclo da ligação (ADR 0039, itens 1 e 5).</summary>
    public class UIContractsTests
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
        public void Resolver_SelfOrParent_FindsOwnerInParent()
        {
            GameObject parent = CreateObject("pai");
            BoxCollider collider = parent.AddComponent<BoxCollider>();
            GameObject child = CreateObject("filho");
            child.transform.SetParent(parent.transform);
            ActorResolver resolver = ActorResolver.ForSelfOrParent();

            bool fromChild = resolver.TryResolve(child.transform, typeof(BoxCollider), out Object childOwner);
            bool fromSelf = resolver.TryResolve(parent.transform, typeof(Collider), out Object selfOwner);

            Assert.That(fromChild, Is.True);
            Assert.That(childOwner, Is.SameAs(collider));
            Assert.That(fromSelf, Is.True);
            Assert.That(selfOwner, Is.SameAs(collider));
        }

        [Test]
        public void Resolver_Explicit()
        {
            GameObject from = CreateObject("ui");
            GameObject actor = CreateObject("ator");
            BoxCollider collider = actor.AddComponent<BoxCollider>();

            bool byComponent = ActorResolver.ForExplicit(collider).TryResolve(from.transform, typeof(BoxCollider), out Object componentOwner);
            bool byGameObject = ActorResolver.ForExplicit(actor).TryResolve(from.transform, typeof(BoxCollider), out Object gameObjectOwner);

            Assert.That(byComponent, Is.True);
            Assert.That(componentOwner, Is.SameAs(collider));
            Assert.That(byGameObject, Is.True);
            Assert.That(gameObjectOwner, Is.SameAs(collider));
        }

        [Test]
        public void Resolver_Anchor_ReadsUntypedValue_TypeChecked()
        {
            GameObject from = CreateObject("ui");
            BoxCollider collider = CreateObject("ator").AddComponent<BoxCollider>();
            ColliderTestAnchor anchor = Create<ColliderTestAnchor>();
            ActorResolver resolver = ActorResolver.ForAnchor(anchor);
            anchor.Provide(collider);

            bool exact = resolver.TryResolve(from.transform, typeof(BoxCollider), out Object exactOwner);
            bool baseType = resolver.TryResolve(from.transform, typeof(Collider), out Object baseOwner);
            bool otherType = resolver.TryResolve(from.transform, typeof(Rigidbody), out Object otherOwner);
            anchor.Unset(collider);
            bool afterUnset = resolver.TryResolve(from.transform, typeof(BoxCollider), out Object unsetOwner);

            Assert.That(exact, Is.True);
            Assert.That(exactOwner, Is.SameAs(collider));
            Assert.That(baseType, Is.True);
            Assert.That(baseOwner, Is.SameAs(collider));
            Assert.That(otherType, Is.False);
            Assert.That(otherOwner, Is.Null);
            Assert.That(afterUnset, Is.False);
            Assert.That(unsetOwner, Is.Null);
        }

        [Test]
        public void Resolver_Missing_FalseNoException()
        {
            GameObject from = CreateObject("ui");
            GameObject destroyed = CreateObject("destruído");
            Object.DestroyImmediate(destroyed);
            var cases = new (string name, ActorResolver resolver, Component from, Type ownerType)[]
            {
                ("SelfOrParent sem dono", ActorResolver.ForSelfOrParent(), from.transform, typeof(BoxCollider)),
                ("SelfOrParent com tipo que não é componente", ActorResolver.ForSelfOrParent(), from.transform, typeof(ScriptableObject)),
                ("FromAnchor sem âncora", ActorResolver.ForAnchor(null), from.transform, typeof(BoxCollider)),
                ("FromAnchor com âncora vazia", ActorResolver.ForAnchor(Create<ColliderTestAnchor>()), from.transform, typeof(BoxCollider)),
                ("Explicit sem referência", ActorResolver.ForExplicit(null), from.transform, typeof(BoxCollider)),
                ("Explicit destruído", ActorResolver.ForExplicit(destroyed), from.transform, typeof(BoxCollider)),
                ("Explicit sem o componente", ActorResolver.ForExplicit(from), from.transform, typeof(BoxCollider)),
                ("from nulo", ActorResolver.ForSelfOrParent(), null, typeof(BoxCollider)),
                ("tipo nulo", ActorResolver.ForSelfOrParent(), from.transform, null),
            };
            var found = new List<string>();

            foreach ((string name, ActorResolver resolver, Component origin, Type ownerType) in cases)
            {
                if (resolver.TryResolve(origin, ownerType, out Object owner) || !ReferenceEquals(owner, null))
                {
                    found.Add(name);
                }
            }

            Assert.That(found, Is.Empty, "casos que acharam dono");
        }

        [Test]
        public void Binding_AttachPublishesCurrent_DetachStops()
        {
            CounterOwner owner = Create<CounterOwner>();
            owner.Set(5);
            var receiver = new RecordingReceiver();
            var culture = new CultureInfo("pt-BR");
            var binding = (CounterBinding)new CounterSource().CreateBinding();
            binding.SetReceiver(receiver);

            binding.Attach(owner, new UIBindContext(null, culture));
            owner.Set(7);
            bool attached = binding.IsAttached;
            binding.Detach();
            owner.Set(9);
            binding.PublishNow(11);

            Assert.That(attached, Is.True);
            Assert.That(binding.AttachedCulture, Is.SameAs(culture));
            Assert.That(receiver.Values, Is.EqualTo(new[] { 5, 7 }));
            Assert.That(binding.IsAttached, Is.False);
            Assert.That(owner.ListenerCount, Is.Zero);
        }

        [Test]
        public void Binding_AttachAgain_MovesToNewOwner()
        {
            CounterOwner first = Create<CounterOwner>();
            CounterOwner second = Create<CounterOwner>();
            first.Set(1);
            second.Set(2);
            var receiver = new RecordingReceiver();
            UIBinding<CounterModel> binding = new CounterSource().CreateBinding();
            binding.SetReceiver(receiver);

            binding.Attach(first, new UIBindContext(null));
            binding.Attach(second, new UIBindContext(null));
            first.Set(3);
            second.Set(4);

            Assert.That(receiver.Values, Is.EqualTo(new[] { 1, 2, 4 }));
            Assert.That(first.ListenerCount, Is.Zero);
            Assert.That(() => binding.Attach(null, new UIBindContext(null)), Throws.ArgumentNullException);
        }

        [Test]
        public void Binding_RequestTick_TogglesWantsTick()
        {
            CounterOwner owner = Create<CounterOwner>();
            var binding = (CounterBinding)new CounterSource().CreateBinding();

            binding.SetTicking(true);
            bool beforeAttach = binding.WantsTick;
            binding.Attach(owner, new UIBindContext(null));
            binding.SetTicking(true);
            bool requested = binding.WantsTick;
            binding.SetTicking(false);
            bool released = binding.WantsTick;
            binding.SetTicking(true);
            binding.Detach();

            Assert.That(beforeAttach, Is.False);
            Assert.That(requested, Is.True);
            Assert.That(released, Is.False);
            Assert.That(binding.WantsTick, Is.False);
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
    }

    public class ColliderTestAnchor : RuntimeAnchor<BoxCollider>
    {
    }

    /// <summary>Dono de teste: um valor inteiro e o evento de mudança.</summary>
    public class CounterOwner : ScriptableObject
    {
        public event Action<int> Changed;

        public int Value { get; private set; }

        public int ListenerCount => Changed?.GetInvocationList().Length ?? 0;

        public void Set(int value)
        {
            Value = value;
            Changed?.Invoke(value);
        }
    }

    public struct CounterModel
    {
        public int Value;
    }

    [Serializable]
    public class CounterSource : UISource<CounterModel>
    {
        public override Type OwnerType => typeof(CounterOwner);

        public override UIBinding<CounterModel> CreateBinding()
        {
            return new CounterBinding();
        }

        public override CounterModel CreatePreview(in UIPreviewContext context)
        {
            return new CounterModel { Value = 50 };
        }
    }

    public class CounterBinding : UIBinding<CounterModel>
    {
        private CounterOwner _owner;

        public IFormatProvider AttachedCulture { get; private set; }

        public void SetTicking(bool on)
        {
            RequestTick(on);
        }

        public void PublishNow(int value)
        {
            Publish(new CounterModel { Value = value });
        }

        protected override void OnAttach(Object owner)
        {
            _owner = (CounterOwner)owner;
            _owner.Changed += OnChanged;
            AttachedCulture = Context.Culture;
            OnChanged(_owner.Value);
        }

        protected override void OnDetach()
        {
            _owner.Changed -= OnChanged;
            _owner = null;
        }

        private void OnChanged(int value)
        {
            Publish(new CounterModel { Value = value });
        }
    }

    public class RecordingReceiver : IUIModelReceiver<CounterModel>
    {
        public List<int> Values { get; } = new List<int>();

        public void Receive(in CounterModel model)
        {
            Values.Add(model.Value);
        }
    }
}
