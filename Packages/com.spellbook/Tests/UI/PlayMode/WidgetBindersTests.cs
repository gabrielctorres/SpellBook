using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Reflection;
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
    /// Modelos e binders de barra, texto e indicador (R7): fração da barra, duas barras independentes sem campo de escudo
    /// (CA3, lado da UI) e atualizações sem alocar (CA13, parte). ADR 0039, itens 1 a 4.
    /// </summary>
    public class WidgetBindersTests
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
        public void Bar_Fraction_ZeroMax_Zero()
        {
            var zeroMax = new BarModel(30f, 0f, default, TextRole.Value);
            var negativeMax = new BarModel(30f, -5f, default, TextRole.Value);
            var half = new BarModel(50f, 100f, default, TextRole.Harmful);
            var over = new BarModel(150f, 100f, default, TextRole.Value);

            Assert.That(zeroMax.Fraction, Is.EqualTo(0f));
            Assert.That(negativeMax.Fraction, Is.EqualTo(0f));
            Assert.That(half.Fraction, Is.EqualTo(0.5f));
            Assert.That(half.TrailFraction, Is.EqualTo(0.5f));
            Assert.That(half.Current, Is.EqualTo(50f));
            Assert.That(half.Max, Is.EqualTo(100f));
            Assert.That(half.Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(half.IsEmpty, Is.False);
            Assert.That(over.Fraction, Is.EqualTo(1f));
            Assert.That(half.WithTrail(2f).TrailFraction, Is.EqualTo(1f));
            Assert.That(half.WithTrail(0.8f).Fraction, Is.EqualTo(0.5f));
            Assert.That(default(BarModel).IsEmpty, Is.True);
        }

        [Test]
        public void TwoBars_Independent_NoShieldField()
        {
            ValueOwner owner = CreateOwner();
            owner.Set(1, 40f);
            BarBinder life = CreateBar(owner, 0, null, out Recorder lifeView);
            BarBinder shield = CreateBar(owner, 1, null, out Recorder shieldView);
            life.gameObject.SetActive(true);
            shield.gameObject.SetActive(true);

            owner.Set(1, 10f);
            int lifeRendersAfterShield = lifeView.BarRenders;
            owner.Set(0, 70f);

            Assert.That(lifeRendersAfterShield, Is.EqualTo(1), "mudar o escudo não deveria redesenhar a vida");
            Assert.That(shieldView.BarRenders, Is.EqualTo(2), "mudar a vida não deveria redesenhar o escudo");
            Assert.That(lifeView.Bar.Current, Is.EqualTo(70f));
            Assert.That(shieldView.Bar.Current, Is.EqualTo(10f));
            const BindingFlags flags = BindingFlags.Instance | BindingFlags.Public | BindingFlags.NonPublic;
            List<string> members = typeof(BarModel).GetMembers(flags).Select(m => m.Name).ToList();
            Assert.That(members.Where(n => n.IndexOf("shield", StringComparison.OrdinalIgnoreCase) >= 0
                || n.IndexOf("escudo", StringComparison.OrdinalIgnoreCase) >= 0), Is.Empty);
        }

        [Test]
        public void TextAndTag_Binders_DeliverModel_EmptyByDefault()
        {
            ValueOwner owner = CreateOwner();
            Recorder view = CreateTextAndTag(owner, null);

            owner.Set(0, 0f);

            Assert.That(default(TextModel).IsEmpty, Is.True);
            Assert.That(default(TagIndicatorModel).IsEmpty, Is.True);
            Assert.That(view.Text.IsEmpty, Is.False);
            Assert.That(view.Text.Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(view.Text.Text.Buffer.ToString(view.Text.Text), Is.EqualTo("0"));
            Assert.That(view.TagRenders, Is.EqualTo(2));
            Assert.That(view.Tag.IsEmpty, Is.False);
            Assert.That(view.Tag.Active, Is.False, "valor zero apaga o indicador de teste");
        }

        [Test]
        public void ModelUpdates10000_DoNotAllocate()
        {
            UIUpdateDriver driver = CreateDriver(out ManualTimeSource time);
            ValueOwner owner = CreateOwner();
            BarBinder bar = CreateBar(owner, 0, driver.transform, out Recorder barView);
            bar.gameObject.SetActive(true);
            Recorder view = CreateTextAndTag(owner, driver.transform);
            Updates(owner, driver, time, 100);

            Assert.That(() => Updates(owner, driver, time, 10000), Is.Not.AllocatingGCMemory());
            Assert.That(barView.BarRenders, Is.GreaterThan(10000));
            Assert.That(view.TextRenders, Is.GreaterThan(10000));
            Assert.That(view.TagRenders, Is.GreaterThan(10000));
        }

        private static void Updates(ValueOwner owner, UIUpdateDriver driver, ManualTimeSource time, int count)
        {
            for (int i = 0; i < count; i++)
            {
                owner.Set(0, i % 2 == 0 ? 30f : 90f);
                time.Advance(0.05f);
                driver.Tick();
            }
        }

        private UIUpdateDriver CreateDriver(out ManualTimeSource time)
        {
            UIUpdateDriver driver = CreateObject("tela", null).AddComponent<UIUpdateDriver>();
            time = ScriptableObject.CreateInstance<ManualTimeSource>();
            _created.Add(time);
            driver.SetTimeSource(time);
            return driver;
        }

        private BarBinder CreateBar(ValueOwner owner, int slot, Transform parent, out Recorder view)
        {
            GameObject gameObject = CreateObject("barra", parent);
            gameObject.SetActive(false);
            view = gameObject.AddComponent<Recorder>();
            BarBinder binder = gameObject.AddComponent<BarBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(owner));
            binder.SetSource(new ValueBarSource { Slot = slot });
            binder.SetView(view);
            return binder;
        }

        // Texto e indicador no mesmo objeto, com a mesma view de teste, já ligados
        private Recorder CreateTextAndTag(ValueOwner owner, Transform parent)
        {
            GameObject gameObject = CreateObject("texto e tag", parent);
            gameObject.SetActive(false);
            Recorder view = gameObject.AddComponent<Recorder>();
            TextBinder text = gameObject.AddComponent<TextBinder>();
            text.SetOwner(ActorResolver.ForExplicit(owner));
            text.SetSource(new ValueTextSource());
            text.SetView(view);
            TagIndicatorBinder tag = gameObject.AddComponent<TagIndicatorBinder>();
            tag.SetOwner(ActorResolver.ForExplicit(owner));
            tag.SetSource(new ActiveTagSource());
            tag.SetView(view);
            gameObject.SetActive(true);
            return view;
        }

        private ValueOwner CreateOwner()
        {
            ValueOwner owner = CreateObject("ator", null).AddComponent<ValueOwner>();
            owner.Max = 100f;
            owner.Set(0, 100f);
            owner.Set(1, 100f);
            return owner;
        }

        private GameObject CreateObject(string name, Transform parent)
        {
            var gameObject = new GameObject(name);
            gameObject.transform.SetParent(parent);
            _created.Add(gameObject);
            return gameObject;
        }

        /// <summary>Dono de teste com dois valores (vida no 0, escudo no 1) e um evento por valor.</summary>
        public sealed class ValueOwner : MonoBehaviour
        {
            public float Max;

            private readonly float[] _values = new float[2];
            private readonly Action[] _changed = new Action[2];

            public float Get(int slot) => _values[slot];

            public void Listen(int slot, Action listener) => _changed[slot] += listener;

            public void Unlisten(int slot, Action listener) => _changed[slot] -= listener;

            public void Set(int slot, float value)
            {
                _values[slot] = value;
                _changed[slot]?.Invoke();
            }
        }

        // Ligação de teste: publica o modelo montado do valor do índice a cada mudança
        private sealed class ValueBinding<TModel> : UIBinding<TModel> where TModel : struct
        {
            private readonly TextBuffer _buffer = new TextBuffer(16);
            private readonly Action _onChanged;
            private readonly int _slot;
            private ValueOwner _owner;

            public ValueBinding(int slot, Func<float, float, TextBuffer, TModel> build)
            {
                _slot = slot;
                _onChanged = () => Publish(build(_owner.Get(_slot), _owner.Max, _buffer));
            }

            protected override void OnAttach(Object owner)
            {
                _owner = (ValueOwner)owner;
                _owner.Listen(_slot, _onChanged);
                _onChanged();
            }

            protected override void OnDetach() => _owner.Unlisten(_slot, _onChanged);
        }

        [Serializable]
        public sealed class ValueBarSource : UISource<BarModel>
        {
            public int Slot;

            public override Type OwnerType => typeof(ValueOwner);

            public override UIBinding<BarModel> CreateBinding() =>
                new ValueBinding<BarModel>(Slot, (value, max, _) => new BarModel(value, max, default, TextRole.Value));

            public override BarModel CreatePreview(in UIPreviewContext context) => default;
        }

        [Serializable]
        public sealed class ValueTextSource : UISource<TextModel>
        {
            public override Type OwnerType => typeof(ValueOwner);

            public override UIBinding<TextModel> CreateBinding() => new ValueBinding<TextModel>(0, (value, _, buffer) =>
            {
                buffer.Clear();
                return new TextModel(buffer.Append(value, "0", CultureInfo.InvariantCulture), TextRole.Harmful);
            });

            public override TextModel CreatePreview(in UIPreviewContext context) => default;
        }

        [Serializable]
        public sealed class ActiveTagSource : UISource<TagIndicatorModel>
        {
            public override Type OwnerType => typeof(ValueOwner);

            public override UIBinding<TagIndicatorModel> CreateBinding() =>
                new ValueBinding<TagIndicatorModel>(0, (value, _, _) => new TagIndicatorModel(value > 0f));

            public override TagIndicatorModel CreatePreview(in UIPreviewContext context) => default;
        }

        /// <summary>View de teste dos três modelos: guarda o último de cada e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<BarModel>, IView<TextModel>, IView<TagIndicatorModel>
        {
            public int BarRenders;
            public int TextRenders;
            public int TagRenders;
            public BarModel Bar;
            public TextModel Text;
            public TagIndicatorModel Tag;

            public void Render(in BarModel model)
            {
                BarRenders++;
                Bar = model;
            }

            public void Render(in TextModel model)
            {
                TextRenders++;
                Text = model;
            }

            public void Render(in TagIndicatorModel model)
            {
                TagRenders++;
                Tag = model;
            }
        }
    }
}
