using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.Time;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.UI.Tests
{
    /// <summary>
    /// Binder do tooltip (Spec UI-3, Design 2): um aberto por vez e sem duplicar (CA7, parte), atraso pelo driver, alvo
    /// destruído fecha, reabrir sem alocar, view própria recebe o modelo (CA12) e aberto sem alocar (CA13, parte).
    /// </summary>
    public class TooltipBinderTests
    {
        private readonly List<Object> _created = new List<Object>();
        private UIUpdateDriver _driver;
        private ManualTimeSource _time;
        private TooltipBinder _binder;
        private RecordingView _view;

        [SetUp]
        public void SetUp()
        {
            _driver = CreateObject("tela").AddComponent<UIUpdateDriver>();
            _time = Create<ManualTimeSource>();
            _driver.SetTimeSource(_time);
            GameObject gameObject = CreateObject("tooltip");
            gameObject.SetActive(false);
            gameObject.transform.SetParent(_driver.transform);
            _binder = gameObject.AddComponent<TooltipBinder>();
            _view = gameObject.AddComponent<RecordingView>();
            _binder.SetView(_view);
            gameObject.SetActive(true);
        }

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
        public void Show_OneAtATime_SecondClosesFirst()
        {
            Owner first = CreateOwner(1);
            Owner second = CreateOwner(2);
            var firstSource = new TestSource();
            var secondSource = new TestSource();

            _binder.Show(new object(), firstSource, first, null, null, 0f);
            _binder.Show(new object(), secondSource, second, null, null, 0f);
            first.Set(5);

            Assert.That(firstSource.LastBinding.IsAttached, Is.False, "abrir o segundo deveria fechar o primeiro");
            Assert.That(first.Listeners, Is.Zero);
            Assert.That(second.Listeners, Is.EqualTo(1));
            Assert.That(ValueOf(_view.Last), Is.EqualTo("2"));
        }

        [Test]
        public void SameRequesterTwice_NoDuplicate()
        {
            Owner owner = CreateOwner(1);
            var source = new TestSource();
            var requester = new object();
            RectTransform target = CreateTarget();

            _binder.Show(requester, source, owner, null, target, 0f);
            _binder.Show(requester, source, owner, null, target, 0f);

            Assert.That(source.CreatedBindings, Is.EqualTo(1));
            Assert.That(source.LastBinding.Attaches, Is.EqualTo(1));
            Assert.That(_view.Renders, Is.EqualTo(1));
            Assert.That(_view.Places, Is.EqualTo(1));
            Assert.That(_view.LastTarget, Is.SameAs(target));
        }

        [Test]
        public void Delay_CountedByDriver()
        {
            Owner owner = CreateOwner(3);
            var source = new TestSource();
            var requester = new object();

            _binder.Show(requester, source, owner, null, null, 0.3f);
            int rendersAtShow = _view.Renders;
            _time.Advance(0.2f);
            _driver.Tick();
            int rendersAt02 = _view.Renders;
            _time.Advance(0.2f);
            _driver.Tick();
            string valueAt04 = ValueOf(_view.Last);
            var cancelled = new object();
            _binder.Show(cancelled, source, owner, null, null, 0.3f);
            _binder.Hide(cancelled);
            _time.Advance(1f);
            _driver.Tick();

            Assert.That(rendersAtShow, Is.Zero, "o atraso deveria segurar a abertura");
            Assert.That(rendersAt02, Is.Zero, "0,2 s do driver não chega a 0,3 s");
            Assert.That(valueAt04, Is.EqualTo("3"));
            Assert.That(_view.Last.IsEmpty, Is.True, "o Hide antes do tempo deveria cancelar a abertura");
            Assert.That(source.LastBinding.IsAttached, Is.False);
            Assert.That(_driver.TickingCount, Is.Zero, "fechado, o binder não deveria pedir tick");
        }

        [Test]
        public void TargetDestroyed_ClosesNoException()
        {
            Owner owner = CreateOwner(1);
            var source = new TestSource();
            RectTransform target = CreateTarget();
            _binder.Show(new object(), source, owner, null, target, 0f);

            _driver.Tick();
            int placesStill = _view.Places;
            target.position += new Vector3(10f, 0f, 0f);
            _driver.Tick();
            int placesMoved = _view.Places;
            Object.DestroyImmediate(target.gameObject);

            Assert.DoesNotThrow(() => _driver.Tick());
            Assert.That(placesStill, Is.EqualTo(1), "alvo parado não deveria reposicionar");
            Assert.That(placesMoved, Is.EqualTo(2), "alvo movido deveria reposicionar");
            Assert.That(_view.Last.IsEmpty, Is.True, "alvo destruído deveria fechar o tooltip");
            Assert.That(source.LastBinding.IsAttached, Is.False);
            Assert.That(owner.Listeners, Is.Zero);
            Assert.That(_driver.TickingCount, Is.Zero);
        }

        [Test]
        public void TargetDestroyedDuringDelay_ClosesWithoutOpening()
        {
            Owner owner = CreateOwner(1);
            var source = new TestSource();
            RectTransform target = CreateTarget();
            _binder.Show(new object(), source, owner, null, target, 0.3f);
            Object.DestroyImmediate(target.gameObject);
            _time.Advance(0.4f);

            Assert.DoesNotThrow(() => _driver.Tick());
            Assert.That(_view.Renders, Is.Zero, "alvo destruído no atraso não deveria abrir o tooltip");
            Assert.That(source.LastBinding?.IsAttached ?? false, Is.False, "a fonte não deveria ser ligada");
            Assert.That(owner.Listeners, Is.Zero);
            Assert.That(_driver.TickingCount, Is.Zero);
        }

        [Test]
        public void Reopen_SameSource_NoAllocation()
        {
            Owner owner = CreateOwner(4);
            var source = new TestSource();
            var requester = new object();
            RectTransform target = CreateTarget();
            _binder.Show(requester, source, owner, null, target, 0f);
            _binder.Hide(requester);

            Assert.That(() =>
            {
                for (int i = 0; i < 100; i++)
                {
                    _binder.Show(requester, source, owner, null, target, 0f);
                    _binder.Hide(requester);
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(source.CreatedBindings, Is.EqualTo(1));
            Assert.That(source.LastBinding.Attaches, Is.EqualTo(101));
            Assert.That(_view.Last.IsEmpty, Is.True);
        }

        [Test]
        public void CustomView_ReceivesModel()
        {
            Owner owner = CreateOwner(3);
            var texture = new Texture2D(1, 1);
            _created.Add(texture);
            Sprite icon = Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero);
            _created.Add(icon);
            var source = new TestSource { Icon = icon };
            Subject subject = Create<Subject>();

            _binder.Show(new object(), source, owner, subject, null, 0f);

            TooltipModel model = _view.Last;
            Assert.That(model.IsEmpty, Is.False);
            Assert.That(model.Title, Is.EqualTo("Teste"));
            Assert.That(model.Icon, Is.SameAs(icon));
            Assert.That(model.Lines.Count, Is.EqualTo(1));
            Assert.That(model.Lines[0].Label, Is.EqualTo("Valor"));
            Assert.That(model.Lines[0].Role, Is.EqualTo(TextRole.Value));
            Assert.That(model.Lines[0].Kind, Is.EqualTo(TooltipLineKind.Value));
            Assert.That(model.Lines[0].Subject, Is.SameAs(subject), "o assunto do Show deveria chegar ao contexto da ligação");
            Assert.That(ValueOf(model), Is.EqualTo("3"));
            Assert.That(model.HasStacks, Is.True);
            Assert.That(model.Stacks, Is.EqualTo(3));
            Assert.That(model.HasRemaining, Is.False);
            Assert.That(typeof(RecordingView).Assembly.GetReferencedAssemblies().Select(a => a.Name),
                Has.None.StartsWith("SpellBook.GAS"));
        }

        [Test]
        public void OpenThenUpdates10000_NoAlloc()
        {
            Owner owner = CreateOwner(0);
            var source = new TestSource { TickOnAttach = true };
            _binder.Show(new object(), source, owner, null, CreateTarget(), 0f);
            owner.Set(1);
            _driver.Tick();
            int rendersBefore = _view.Renders;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    owner.Set(i);
                    _driver.Tick();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(_view.Renders - rendersBefore, Is.EqualTo(10000));
            Assert.That(source.LastBinding.Ticks, Is.GreaterThanOrEqualTo(10000), "a ligação aberta deveria receber o tick");
            Assert.That(ValueOf(_view.Last), Is.EqualTo("9999"));
        }

        private static string ValueOf(in TooltipModel model)
        {
            Assert.That(model.IsEmpty, Is.False, "o tooltip deveria estar aberto");
            TextSpan value = model.Lines[0].Value;
            return value.Buffer.ToString(value);
        }

        private Owner CreateOwner(int value)
        {
            Owner owner = CreateObject("dono").AddComponent<Owner>();
            owner.Set(value);
            return owner;
        }

        private RectTransform CreateTarget()
        {
            GameObject gameObject = CreateObject("alvo");
            return gameObject.AddComponent<RectTransform>();
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

        public sealed class Owner : MonoBehaviour
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

        public sealed class Subject : ScriptableObject
        {
        }

        [Serializable]
        public sealed class TestSource : UISource<TooltipModel>
        {
            public int CreatedBindings;
            public bool TickOnAttach;
            public Sprite Icon;
            public TestBinding LastBinding;

            public override Type OwnerType => typeof(Owner);

            public override UIBinding<TooltipModel> CreateBinding()
            {
                CreatedBindings++;
                LastBinding = new TestBinding(this);
                return LastBinding;
            }

            public override TooltipModel CreatePreview(in UIPreviewContext context) => default;
        }

        public sealed class TestBinding : UIBinding<TooltipModel>
        {
            public int Attaches;
            public int Ticks;

            private readonly TestSource _source;
            private readonly Action _onChanged;
            private readonly List<TooltipLine> _lines = new List<TooltipLine>();
            private readonly TextBuffer _buffer = new TextBuffer();
            private Owner _owner;

            public TestBinding(TestSource source)
            {
                _source = source;
                _onChanged = OnChanged;
            }

            public override void Tick(double time) => Ticks++;

            protected override void OnAttach(Object owner)
            {
                Attaches++;
                _owner = (Owner)owner;
                _owner.Changed += _onChanged;
                RequestTick(_source.TickOnAttach);
                OnChanged();
            }

            protected override void OnDetach() => _owner.Changed -= _onChanged;

            private void OnChanged()
            {
                _buffer.Clear();
                _lines.Clear();
                TextSpan value = _buffer.Append(_owner.Value, "0", CultureInfo.InvariantCulture);
                _lines.Add(new TooltipLine("Valor", value, TextRole.Value, TooltipLineKind.Value, Context.Subject as ScriptableObject));
                Publish(new TooltipModel("Teste", _source.Icon, _lines).WithStacks(_owner.Value));
            }
        }

        public sealed class RecordingView : MonoBehaviour, IView<TooltipModel>, ITooltipPlacementTarget
        {
            public int Renders;
            public int Places;
            public TooltipModel Last;
            public RectTransform LastTarget;

            public void Render(in TooltipModel model)
            {
                Renders++;
                Last = model;
            }

            public void Place(RectTransform target)
            {
                Places++;
                LastTarget = target;
            }
        }
    }
}
