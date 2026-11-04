using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.Testing;
using SpellBook.UI;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fontes de barra e texto de atributo (R8): atualização por evento (CA1), máximo pela fonte de limite do AR1, texto
    /// pelo formato do atributo e cultura injetada (CA2), barras independentes (CA3) e atualizações sem alocar (CA13, parte).
    /// </summary>
    public class AttributeSourcesTests
    {
        private static readonly CultureInfo PtBr = CultureInfo.GetCultureInfo("pt-BR");

        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
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
            _gas.Dispose();
        }

        [Test]
        public void Bar_VidaAndVidaMax_ByEvent_NoUpdate()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition vidaMax = _gas.CreateAttribute("VidaMax");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 80f), (vidaMax, 100f));
            Recorder view = CreateBar(asc, new AttributeBarSource { Current = vida, Max = vidaMax });
            BarModel initial = view.Bar;

            State(asc, vida).SetBaseValue(30f);
            BarModel afterHit = view.Bar;
            State(asc, vidaMax).SetBaseValue(60f);

            Assert.That(initial.Current, Is.EqualTo(80f));
            Assert.That(initial.Fraction, Is.EqualTo(0.8f).Within(1e-5f));
            Assert.That(afterHit.Current, Is.EqualTo(30f));
            Assert.That(afterHit.Fraction, Is.EqualTo(0.3f).Within(1e-5f));
            Assert.That(view.Bar.Max, Is.EqualTo(60f));
            Assert.That(view.Bar.Fraction, Is.EqualTo(0.5f).Within(1e-5f));
            Assert.That(view.Bars, Is.EqualTo(3), "um desenho ao ligar e um por evento, sem driver");
        }

        [Test]
        public void Bar_MaxFromLimitSource_FollowsAR1()
        {
            AttributeDefinition vidaMax = _gas.CreateAttribute("VidaMax");
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            vida.MaxSource = LimitSource.Attribute;
            vida.MaxAttribute = vidaMax;
            AbilitySystemComponent asc = _gas.CreateAsc((vidaMax, 200f), (vida, 100f));
            Recorder view = CreateBar(asc, new AttributeBarSource { Current = vida });
            float initialMax = view.Bar.Max;

            State(asc, vidaMax).SetBaseValue(400f);

            Assert.That(initialMax, Is.EqualTo(200f));
            Assert.That(view.Bar.Max, Is.EqualTo(400f));
            Assert.That(view.Bar.Fraction, Is.EqualTo(0.25f).Within(1e-5f));
        }

        [Test]
        public void Text_FormatAndInjectedCulture_NoMarkup()
        {
            AttributeDefinition mana = _gas.CreateAttribute("Mana");
            AttributeDefinition manaMax = _gas.CreateAttribute("ManaMax");
            mana.Display.Decimals = 1;
            mana.Display.Suffix = " MP";
            mana.Display.Role = TextRole.Beneficial;
            AbilitySystemComponent asc = _gas.CreateAsc((mana, 12.5f), (manaMax, 40f));
            var source = new AttributeTextSource
            {
                Current = mana, Max = manaMax, TextMode = AttributeTextMode.CurrentOfMax,
                Separator = new LocalizedText(null, " de "),
            };
            var received = new Receiver<TextModel>();
            UIBinding<TextModel> binding = source.CreateBinding();
            binding.SetReceiver(received);

            binding.Attach(asc, new UIBindContext(null, PtBr));
            string text = received.Last.Text.Buffer.ToString(received.Last.Text);

            Assert.That(text, Is.EqualTo("12,5 MP de 40,0 MP"));
            Assert.That(text, Does.Not.Contain("<"));
            Assert.That(received.Last.Role, Is.EqualTo(TextRole.Beneficial));
        }

        [Test]
        public void TwoBars_VidaAndEscudo_Independent()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition escudo = _gas.CreateAttribute("Escudo");
            AttributeDefinition maximo = _gas.CreateAttribute("Maximo");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f), (escudo, 50f), (maximo, 100f));
            Recorder life = CreateBar(asc, new AttributeBarSource { Current = vida, Max = maximo });
            Recorder shield = CreateBar(asc, new AttributeBarSource { Current = escudo, Max = maximo });

            State(asc, escudo).SetBaseValue(10f);

            Assert.That(shield.Bar.Current, Is.EqualTo(10f));
            Assert.That(shield.Bars, Is.EqualTo(2));
            Assert.That(life.Bar.Current, Is.EqualTo(100f));
            Assert.That(life.Bars, Is.EqualTo(1), "a barra de vida não deveria redesenhar com o escudo");
        }

        [Test]
        public void Bar_MissingAttribute_PublishesEmpty()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition ausente = _gas.CreateAttribute("Ausente");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f));

            Recorder missingCurrent = CreateBar(asc, new AttributeBarSource { Current = ausente });
            Recorder missingMax = CreateBar(asc, new AttributeBarSource { Current = vida, Max = ausente });

            Assert.That(missingCurrent.Bars, Is.EqualTo(1));
            Assert.That(missingCurrent.Bar.IsEmpty, Is.True);
            Assert.That(missingMax.Bar.IsEmpty, Is.True);
        }

        [Test]
        public void Updates10000_DoNotAllocate()
        {
            AttributeDefinition vida = _gas.CreateAttribute("Vida");
            AttributeDefinition vidaMax = _gas.CreateAttribute("VidaMax");
            AbilitySystemComponent asc = _gas.CreateAsc((vida, 100f), (vidaMax, 100f));
            Recorder bar = CreateBar(asc, new AttributeBarSource { Current = vida, Max = vidaMax });
            Recorder text = CreateText(asc, new AttributeTextSource { Current = vida, Max = vidaMax });
            AttributeState state = State(asc, vida);
            Updates(state, 100);

            Assert.That(() => Updates(state, 10000), Is.Not.AllocatingGCMemory());
            Assert.That(bar.Bars, Is.GreaterThan(10000));
            Assert.That(text.Texts, Is.GreaterThan(10000));
        }

        private static void Updates(AttributeState state, int count)
        {
            for (int i = 0; i < count; i++)
            {
                state.SetBaseValue(i % 2 == 0 ? 30f : 90f);
            }
        }

        private static AttributeState State(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
            return state;
        }

        // Barra ligada ao ASC pelo dono explícito, sem driver: só os eventos do GAS a atualizam
        private Recorder CreateBar(AbilitySystemComponent asc, AttributeBarSource source)
        {
            GameObject gameObject = CreateInactive("barra");
            Recorder view = gameObject.AddComponent<Recorder>();
            BarBinder binder = gameObject.AddComponent<BarBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(source);
            binder.SetView(view);
            gameObject.SetActive(true);
            return view;
        }

        private Recorder CreateText(AbilitySystemComponent asc, AttributeTextSource source)
        {
            GameObject gameObject = CreateInactive("texto");
            Recorder view = gameObject.AddComponent<Recorder>();
            TextBinder binder = gameObject.AddComponent<TextBinder>();
            binder.SetOwner(ActorResolver.ForExplicit(asc));
            binder.SetSource(source);
            binder.SetView(view);
            gameObject.SetActive(true);
            return view;
        }

        private GameObject CreateInactive(string name)
        {
            var gameObject = new GameObject(name);
            gameObject.SetActive(false);
            _created.Add(gameObject);
            return gameObject;
        }

        private sealed class Receiver<TModel> : IUIModelReceiver<TModel> where TModel : struct
        {
            public TModel Last;

            public void Receive(in TModel model) => Last = model;
        }

        /// <summary>View de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<BarModel>, IView<TextModel>
        {
            public int Bars;
            public int Texts;
            public BarModel Bar;
            public TextModel Text;

            public void Render(in BarModel model)
            {
                Bars++;
                Bar = model;
            }

            public void Render(in TextModel model)
            {
                Texts++;
                Text = model;
            }
        }
    }
}
