#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.GAS.Text;
using SpellBook.GAS.Values;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using UnityEngine.TestTools.Constraints;
using Is = UnityEngine.TestTools.Constraints.Is;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte do tooltip de atributo (Spec UI-3, TT5b): valor formatado e decomposição opcional (CA5), valores vivos,
    /// aberto sem alocar nem tick (CA13, parte), eventos soltos ao fechar e só o asset sem ASC.
    /// </summary>
    public class AttributeTooltipSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private AttributeDefinition _strength;
        private AttributeDefinition _other;
        private UIUpdateDriver _driver;
        private TooltipBinder _binder;
        private Recorder _view;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _strength = _gas.CreateAttribute("Força");
            _strength.DisplayName = new LocalizedText("test.strength", "Força");
            _strength.Display = new AttributeDisplay { Kind = ValueKind.Flat, Decimals = 1, Role = TextRole.Value };
            _strength.Icon = Track(Sprite.Create(Track(new Texture2D(1, 1)), new Rect(0f, 0f, 1f, 1f), Vector2.zero));
            _other = _gas.CreateAttribute("Outro");

            var theme = Track(ScriptableObject.CreateInstance<UITheme>());
            using (var serialized = new SerializedObject(theme))
            {
                serialized.FindProperty("text").objectReferenceValue = Track(ScriptableObject.CreateInstance<TestText>());
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            _driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            _driver.SetTimeSource(_clock);
            var tooltip = Track(new GameObject("tooltip"));
            tooltip.SetActive(false);
            tooltip.transform.SetParent(_driver.transform);
            _binder = tooltip.AddComponent<TooltipBinder>();
            _view = tooltip.AddComponent<Recorder>();
            _binder.SetView(_view);
            _binder.SetTheme(theme);
            tooltip.SetActive(true);
        }

        [TearDown]
        public void TearDown()
        {
            for (int i = _created.Count - 1; i >= 0; i--)
            {
                if (_created[i] != null) Object.DestroyImmediate(_created[i]);
            }

            _created.Clear();
            _gas.Dispose();
            _time.Dispose();
        }

        [Test]
        public void Attribute_ValueFormatted_BreakdownOffByDefault_BaseAndSourcesWhenOn()
        {
            AbilitySystemComponent asc = Asc();
            asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            State(asc).AddModifier(new AttributeModifier(0.5f, ModifierOperation.Multiply, new object()));
            float before = asc.GetAttributeValue(_strength);

            Open(new AttributeTooltipSource { Attribute = _strength }, asc);
            TooltipModel plain = _view.Last;
            string[] plainLines = Lines(plain);
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);
            TooltipModel model = _view.Last;

            Assert.That(new AttributeTooltipSource().Breakdown, Is.False, "decomposição desligada por padrão");
            Assert.That(plain.Title, Is.EqualTo("Strength"), "o nome pelo provedor");
            Assert.That(plain.Icon, Is.SameAs(_strength.Icon));
            Assert.That(plainLines, Is.EqualTo(new[] { "Value||22.5" }), "só o valor, no formato do atributo");
            Assert.That(plain.Lines[0].Role, Is.EqualTo(TextRole.Value));
            Assert.That(plain.Lines[0].Subject, Is.SameAs(_strength));
            Assert.That(Lines(model), Is.EqualTo(new[]
            {
                "Value||22.5",
                "Breakdown|Base value|10.0",
                "Breakdown|Blessing|+5.0",
                "Breakdown|Other source|+50.0%",
            }), "base, efeito pelo nome e outra fonte pela chave");
            Assert.That(model.Lines[2].Role, Is.EqualTo(TextRole.Beneficial), "papel pela variação");
            Assert.That(model.Lines[3].Subject, Is.SameAs(_strength));
            Assert.That(asc.GetAttributeValue(_strength), Is.EqualTo(before), "o tooltip não altera o ASC");
        }

        [Test]
        public void ValueAndModifiersChangeWhileOpen_LinesUpdate()
        {
            AbilitySystemComponent asc = Asc();
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);
            Assert.That(Lines(_view.Last), Is.EqualTo(new[] { "Value||10.0", "Breakdown|Base value|10.0" }));

            State(asc).SetBaseValue(12f);
            string[] afterBase = Lines(_view.Last);
            ActiveEffectInstance blessing = asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            string[] afterApply = Lines(_view.Last);
            asc.RemoveEffect(blessing);

            Assert.That(afterBase, Is.EqualTo(new[] { "Value||12.0", "Breakdown|Base value|12.0" }), "a base muda aberta");
            Assert.That(afterApply, Is.EqualTo(new[] { "Value||17.0", "Breakdown|Base value|12.0", "Breakdown|Blessing|+5.0" }),
                "o efeito entra aberto");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[] { "Value||12.0", "Breakdown|Base value|12.0" }), "o efeito sai aberto");
        }

        [Test]
        public void ModifierAtLimit_BreakdownUpdatesByEffectEvent()
        {
            _strength.MaxValue = 15f;
            AbilitySystemComponent asc = Asc();
            asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);

            asc.ApplyEffect(Bonus(string.Empty, "Fúria", 5f));

            Assert.That(asc.GetAttributeValue(_strength), Is.EqualTo(15f), "no limite, o valor não muda");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[]
            {
                "Value||15.0",
                "Breakdown|Base value|10.0",
                "Breakdown|Blessing|+5.0",
                "Breakdown|Fúria|+5.0",
            }), "o modificador novo aparece pelo evento do efeito");
        }

        [Test]
        public void ModifierAtLimit_EffectRemoved_BreakdownUpdates()
        {
            _strength.MaxValue = 15f;
            AbilitySystemComponent asc = Asc();
            asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            ActiveEffectInstance fury = asc.ApplyEffect(Bonus(string.Empty, "Fúria", 5f));
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);

            asc.RemoveEffect(fury);

            Assert.That(asc.GetAttributeValue(_strength), Is.EqualTo(15f), "no limite, o valor não muda");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[]
            {
                "Value||15.0",
                "Breakdown|Base value|10.0",
                "Breakdown|Blessing|+5.0",
            }), "o modificador sai pelo evento de remoção");
        }

        [Test]
        public void ModifierAtLimit_StacksChanged_BreakdownUpdates()
        {
            _strength.MaxValue = 15f;
            AbilitySystemComponent asc = Asc();
            asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            ActiveEffectInstance fury = asc.ApplyEffect(Bonus(string.Empty, "Fúria", 5f, 3));
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);

            asc.SetEffectStacks(fury, 2);

            Assert.That(asc.GetAttributeValue(_strength), Is.EqualTo(15f), "no limite, o valor não muda");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[]
            {
                "Value||15.0",
                "Breakdown|Base value|10.0",
                "Breakdown|Blessing|+5.0",
                "Breakdown|Fúria|+10.0",
            }), "o modificador segue as pilhas pelo evento de pilhas");
        }

        [Test]
        public void Updates10000_NoAlloc_NoTickRequested()
        {
            AbilitySystemComponent asc = Asc();
            asc.ApplyEffect(Bonus("test.blessing", "Bênção", 5f));
            Open(new AttributeTooltipSource { Attribute = _strength, Breakdown = true }, asc);
            AttributeState state = State(asc);
            state.SetBaseValue(11f);
            _driver.Tick();
            int rendersBefore = _view.Renders;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    state.SetBaseValue(10f + i % 5);
                    _driver.Tick();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(_view.Renders - rendersBefore, Is.EqualTo(10000), "cada mudança refaz o tooltip");
            Assert.That(Lines(_view.Last)[0], Is.EqualTo("Value||19.0"));
            Assert.That(_driver.TickingCount, Is.Zero, "o tooltip de atributo não pede tick");
        }

        [Test]
        public void ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange()
        {
            AbilitySystemComponent first = Asc();
            AbilitySystemComponent second = Asc();
            var source = new AttributeTooltipSource { Attribute = _strength, Breakdown = true };
            var requester = new object();
            _binder.Show(requester, source, first, null, null, 0f);
            _binder.Hide(requester);
            Open(source, second);
            int renders = _view.Renders;

            State(first).SetBaseValue(20f);
            ActiveEffectInstance fury = first.ApplyEffect(Bonus(string.Empty, "Fúria", 5f, 3));
            first.SetEffectStacks(fury, 2);
            first.RemoveEffect(fury);
            int afterFirst = _view.Renders;
            State(second).SetBaseValue(20f);

            Assert.That(afterFirst, Is.EqualTo(renders), "o Detach solta o atributo e os três eventos de efeito do ASC anterior");
            Assert.That(_view.Renders - renders, Is.EqualTo(1), "uma assinatura por evento depois de reabrir");
            Assert.That(Lines(_view.Last)[0], Is.EqualTo("Value||20.0"));
        }

        [Test]
        public void NoOwnerOrNoAttribute_AssetOnly_NoException()
        {
            AttributeDefinition gone = _gas.CreateAttribute("Sumido");
            Object.DestroyImmediate(gone);
            var fromItem = new AttributeTooltipSource { Breakdown = true };

            Assert.That(() => _binder.Show(new object(), fromItem, null, _strength, null, 0f), Throws.Nothing);
            TooltipModel noOwner = _view.Last;
            Assert.That(() => _binder.Show(new object(), fromItem, _gas.CreateAsc((_other, 1f)), _strength, null, 0f),
                Throws.Nothing);
            TooltipModel withoutAttribute = _view.Last;
            Assert.That(() => _binder.Show(new object(), new AttributeTooltipSource { Attribute = gone }, Asc(), null, null, 0f),
                Throws.Nothing);
            TooltipModel destroyed = _view.Last;
            TooltipModel preview = new AttributeTooltipSource { Attribute = _strength, Breakdown = true }
                .CreatePreview(new UIPreviewContext(1, _binder.Theme.Text));

            Assert.That(_binder.IsOpen, Is.True, "sem dono, o tooltip abre");
            Assert.That(noOwner.Title, Is.EqualTo("Strength"), "sem dono: o atributo do item");
            Assert.That(noOwner.Lines, Is.Empty, "sem dono, sem valor");
            Assert.That(withoutAttribute.Title, Is.EqualTo("Strength"));
            Assert.That(withoutAttribute.Lines, Is.Empty, "ASC sem o atributo, sem valor");
            Assert.That(destroyed.IsEmpty, Is.True, "atributo destruído e sem assunto: modelo vazio");
            Assert.That(preview.Title, Is.EqualTo("Strength"), "a prévia segue o caminho sem dono");
            Assert.That(preview.Lines, Is.Empty);
        }

        private void Open(AttributeTooltipSource source, AbilitySystemComponent asc)
        {
            _binder.Show(new object(), source, asc, null, null, 0f);
            Assert.That(_view.Last.IsEmpty, Is.False, "o tooltip deveria estar aberto");
        }

        // Efeito infinito que soma amount à força por pilha, com o nome pela chave
        private GameplayEffect Bonus(string key, string name, float amount, int maxStacks = 1)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Infinite, 0f);
            effect.CanStack = maxStacks > 1;
            effect.MaxStacks = maxStacks;
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _strength,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource
                {
                    Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(amount)
                }
            });
            using var serialized = new SerializedObject(effect);
            serialized.FindProperty("localizedName.key").stringValue = key;
            serialized.FindProperty("localizedName.text").stringValue = name;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return effect;
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_strength, 10f));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private AttributeState State(AbilitySystemComponent asc)
        {
            Assert.That(asc.AttributeSet.TryGetState(_strength, out AttributeState state), Is.True);
            return state;
        }

        private static string[] Lines(in TooltipModel model)
        {
            var lines = new string[model.Lines.Count];
            for (int i = 0; i < lines.Length; i++)
            {
                TooltipLine line = model.Lines[i];
                lines[i] = $"{line.Kind}|{line.Label}|{Text(line.Value)}";
            }

            return lines;
        }

        private static string Text(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>View de teste: guarda o último modelo e conta os desenhos.</summary>
        public sealed class Recorder : MonoBehaviour, IView<TooltipModel>
        {
            public TooltipModel Last;
            public int Renders;

            public void Render(in TooltipModel model)
            {
                Last = model;
                Renders++;
            }
        }

        /// <summary>Provedor de teste em en-US: troca o nome do atributo, o do efeito e as chaves do tooltip.</summary>
        public sealed class TestText : TextProviderAsset
        {
            private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

            public override IFormatProvider Culture => EnUs;

            public override string GetText(string key, string fallback)
            {
                switch (key)
                {
                    case "test.strength": return "Strength";
                    case "test.blessing": return "Blessing";
                    case GasTextKeys.TooltipBase: return "Base value";
                    case GasTextKeys.TooltipOtherSource: return "Other source";
                    default: return fallback;
                }
            }
        }
    }
}
#endif
