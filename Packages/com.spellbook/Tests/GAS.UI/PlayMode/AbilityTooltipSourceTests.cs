#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Abilities;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Description;
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
    /// Fonte do tooltip de habilidade (Spec UI-3, TT4): título, ícone, subtítulo e linhas no nível (CA1), valores vivos
    /// (CA2), nível (CA3), cultura e provedor (CA9), sem dono pelo binder (CA10), aberto sem alocar nem tick (CA13, parte),
    /// eventos soltos ao fechar e habilidade destruída como nula.
    /// </summary>
    public class AbilityTooltipSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private AttributeDefinition _mana;
        private AttributeDefinition _health;
        private AttributeDefinition _reduction;
        private AttributeDefinition _maxMana;
        private UIUpdateDriver _driver;
        private TooltipBinder _binder;
        private Recorder _view;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _mana = Attribute("Mana");
            _health = Attribute("Vida");
            _reduction = Attribute("Redução");
            _maxMana = Attribute("Mana máxima");

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
        public void Fireball_Level3_TitleIconSubtitle_DescriptionCostCooldownCharges_AscUnchanged()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            AbilitySpec spec = asc.GiveAbility(fireball, 3);
            int chargesBefore = asc.GetCharges(spec);

            Open(new AbilityTooltipSource { Ability = fireball, Subtitle = new LocalizedText(null, "Fogo") }, asc);
            TooltipModel model = _view.Last;

            Assert.That(model.Title, Is.EqualTo("Bola de Fogo"));
            Assert.That(model.Icon, Is.SameAs(fireball.Icon));
            Assert.That(model.Subtitle, Is.EqualTo("Fogo"));
            Assert.That(Lines(model), Is.EqualTo(new[]
            {
                "Description|Arremessa fogo|",
                "Modifier|Vida|-40",
                "Cost|Mana|30",
                "Cooldown|Cooldown|4 s",
                "Charges||2 charges, one every 5 s",
            }));
            Assert.That(model.Lines[1].Subject, Is.SameAs(_health), "a linha do modificador leva o atributo");
            Assert.That(asc.GetAttributeValue(_mana), Is.EqualTo(100f), "o tooltip não gasta mana");
            Assert.That(asc.IsOnCooldown(fireball.CooldownEffect), Is.False, "o tooltip não põe em recarga");
            Assert.That(asc.GetCharges(spec), Is.EqualTo(chargesBefore), "o tooltip não gasta carga");
            Assert.That(spec.Level, Is.EqualTo(3), "o tooltip não muda o nível");
        }

        [Test]
        public void ReductionChangesWhileOpen_CooldownUpdates()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            asc.GiveAbility(fireball, 3);
            Open(new AbilityTooltipSource { Ability = fireball }, asc);
            Assert.That(Line(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo("4 s"));

            State(asc, _reduction).SetBaseValue(0.5f);

            Assert.That(Line(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo("2 s"), "a recarga real, após a redução");
            Assert.That(Line(_view.Last, TooltipLineKind.Charges), Is.EqualTo("2 charges, one every 2.5 s"));
        }

        [TestCase(ModifierValueSource.SourceType.Attribute, "200", "300")]
        [TestCase(ModifierValueSource.SourceType.Formula, "100", "150")]
        public void CostAttributeChangesWhileOpen_CostUpdates_OneRenderPerChange(
            ModifierValueSource.SourceType type, string before, string after)
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            fireball.CostEffect = CostFromMaxMana(type);
            asc.GiveAbility(fireball, 1);
            Open(new AbilityTooltipSource { Ability = fireball }, asc);
            Assert.That(Line(_view.Last, TooltipLineKind.Cost), Is.EqualTo(before));
            int renders = _view.Renders;

            State(asc, _maxMana).SetBaseValue(300f);

            Assert.That(Line(_view.Last, TooltipLineKind.Cost), Is.EqualTo(after), "o custo vivo, com o tooltip aberto");
            Assert.That(_view.Renders - renders, Is.EqualTo(1), "uma mudança do atributo do custo, um desenho");
        }

        [Test]
        public void LevelChangesWhileOpen_LinesUpdate()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            asc.GiveAbility(fireball, 1);
            Open(new AbilityTooltipSource { Ability = fireball }, asc);
            string[] level1 = Lines(_view.Last);

            asc.SetAbilityLevel(fireball, 3);

            Assert.That(level1, Is.EqualTo(new[]
            {
                "Description|Arremessa fogo|",
                "Modifier|Vida|-20",
                "Cost|Mana|10",
                "Cooldown|Cooldown|8 s",
                "Charges||2 charges, one every 5 s",
            }));
            Assert.That(Line(_view.Last, TooltipLineKind.Modifier), Is.EqualTo("-40"), "o nível 3 chega pelo evento");
            Assert.That(Line(_view.Last, TooltipLineKind.Cost), Is.EqualTo("30"));
            Assert.That(Line(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo("4 s"));
        }

        [Test]
        public void EnUs_ProviderKeys_NoMarkup_RoleOnLines()
        {
            AbilitySystemComponent asc = Asc(0.25f);
            GameplayAbility fireball = Fireball();
            fireball.DisplayName = new LocalizedText("test.fireball", "Bola de Fogo");
            asc.GiveAbility(fireball, 2);

            Open(new AbilityTooltipSource { Ability = fireball, Subtitle = new LocalizedText("test.category", "Fogo") }, asc);
            TooltipModel model = _view.Last;

            Assert.That(model.Title, Is.EqualTo("Fireball"), "o título vem do provedor pela chave");
            Assert.That(model.Subtitle, Is.EqualTo("Fire"));
            Assert.That(Label(model, TooltipLineKind.Cooldown), Is.EqualTo("Cooldown"), "o rótulo vem do provedor pela chave");
            Assert.That(Line(model, TooltipLineKind.Cooldown), Is.EqualTo("4.5 s"), "decimal com ponto em en-US");
            Assert.That(Line(model, TooltipLineKind.Charges), Is.EqualTo("2 charges, one every 3.8 s"));
            foreach (string line in Lines(model))
            {
                Assert.That(line, Does.Not.Contain("<"), "o GAS não escreve marcação");
            }

            Assert.That(model.Lines[0].Role, Is.EqualTo(TextRole.None));
            Assert.That(model.Lines[1].Role, Is.EqualTo(TextRole.Harmful), "dano na vida é prejudicial");
            Assert.That(model.Lines[2].Role, Is.EqualTo(TextRole.Value));
            Assert.That(model.Lines[3].Role, Is.EqualTo(TextRole.Value));
        }

        [Test]
        public void NoOwner_ConfiguredLevel_AssetValues_NoException()
        {
            GameplayAbility fireball = Fireball();
            var configured = new AbilityTooltipSource { Ability = fireball, LevelWithoutOwner = 3 };
            var fromItem = new AbilityTooltipSource { LevelWithoutOwner = 1 };

            Assert.That(() => _binder.Show(new object(), configured, null, null, null, 0f), Throws.Nothing);
            TooltipModel configuredModel = _view.Last;
            Assert.That(() => _binder.Show(new object(), fromItem, null, fireball, null, 0f), Throws.Nothing);
            TooltipModel itemModel = _view.Last;
            TooltipModel preview = configured.CreatePreview(new UIPreviewContext(3, _binder.Theme.Text));

            Assert.That(_binder.IsOpen, Is.True, "sem dono, o tooltip abre");
            Assert.That(configuredModel.Title, Is.EqualTo("Bola de Fogo"));
            Assert.That(Lines(configuredModel), Is.EqualTo(new[]
            {
                "Description|Arremessa fogo|",
                "Modifier|Vida|-40",
                "Cost|Mana|30",
                "Cooldown|Cooldown|4 s",
                "Charges||2 charges, one every 5 s",
            }), "nível configurado e valores do asset, sem redução");
            Assert.That(Line(itemModel, TooltipLineKind.Cooldown), Is.EqualTo("8 s"), "o assunto do item, no nível 1");
            Assert.That(Lines(preview), Is.EqualTo(Lines(configuredModel)), "a prévia no mesmo nível dá as mesmas linhas");
        }

        [Test]
        public void DescriptionWithCostElement_NoDuplicateLine()
        {
            GameplayAbility fireball = Fireball();
            fireball.Description.Elements.Add(new CostElement());

            Open(new AbilityTooltipSource { Ability = fireball }, Asc(0f));

            Assert.That(Count(_view.Last, TooltipLineKind.Cost), Is.EqualTo(1), "o custo da descrição não se repete");
            Assert.That(Count(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo(1));
        }

        [Test]
        public void Updates10000_NoAlloc_NoTickRequested()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            asc.GiveAbility(fireball, 3);
            Open(new AbilityTooltipSource { Ability = fireball }, asc);
            AttributeState reduction = State(asc, _reduction);
            reduction.SetBaseValue(0.1f);
            _driver.Tick();
            int rendersBefore = _view.Renders;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    reduction.SetBaseValue((i % 5) * 0.1f);
                    _driver.Tick();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(_view.Renders - rendersBefore, Is.EqualTo(10000), "cada mudança da redução refaz o tooltip");
            Assert.That(Line(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo("2.4 s"));
            Assert.That(_driver.TickingCount, Is.Zero, "o tooltip de habilidade não pede tick");
        }

        [Test]
        public void ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange()
        {
            GameplayAbility fireball = Fireball();
            AbilitySystemComponent first = Asc(0f);
            AbilitySystemComponent second = Asc(0f);
            first.GiveAbility(fireball, 1);
            second.GiveAbility(fireball, 3);
            var source = new AbilityTooltipSource { Ability = fireball };
            var requester = new object();
            _binder.Show(requester, source, first, null, null, 0f);
            _binder.Hide(requester);
            Open(source, second);
            int renders = _view.Renders;

            State(first, _reduction).SetBaseValue(0.5f);
            first.SetAbilityLevel(fireball, 2);
            Assert.That(first.TryActivateAbility(fireball, Vector3.zero), Is.EqualTo(AbilityActivationResult.Success));
            int afterFirst = _view.Renders;
            State(second, _reduction).SetBaseValue(0.5f);

            Assert.That(afterFirst, Is.EqualTo(renders), "o Detach desassina o ASC anterior: redução, nível, recarga e cargas");
            Assert.That(_view.Renders - renders, Is.EqualTo(1), "uma assinatura por evento depois de reabrir");
            Assert.That(Line(_view.Last, TooltipLineKind.Cooldown), Is.EqualTo("2 s"));
        }

        [Test]
        public void DestroyedAbilityOrSubject_CountsAsNull_EmptyModel_NoException()
        {
            AbilitySystemComponent asc = Asc(0f);
            GameplayAbility fireball = Fireball();
            asc.GiveAbility(fireball, 3);
            GameplayAbility gone = Fireball();
            Object.DestroyImmediate(gone);

            Assert.That(() => _binder.Show(new object(), new AbilityTooltipSource { Ability = gone }, asc, null, null, 0f),
                Throws.Nothing);
            Assert.That(_view.Last.IsEmpty, Is.True, "habilidade configurada destruída e sem assunto: modelo vazio");
            Assert.That(() => _binder.Show(new object(), new AbilityTooltipSource(), asc, gone, null, 0f), Throws.Nothing);
            Assert.That(_view.Last.IsEmpty, Is.True, "assunto destruído: modelo vazio");

            Open(new AbilityTooltipSource { Ability = fireball }, asc);
            Object.DestroyImmediate(fireball);
            Assert.That(() => State(asc, _reduction).SetBaseValue(0.5f), Throws.Nothing);
            Assert.That(_view.Last.IsEmpty, Is.True, "habilidade destruída com o tooltip aberto: modelo vazio");
        }

        private void Open(AbilityTooltipSource source, AbilitySystemComponent asc)
        {
            _binder.Show(new object(), source, asc, null, null, 0f);
            Assert.That(_view.Last.IsEmpty, Is.False, "o tooltip deveria estar aberto");
        }

        // Fireball de três níveis: texto, dano na vida, custo de mana, recarga 8/6/4 s, duas cargas a cada 5 s e redução
        private GameplayAbility Fireball()
        {
            GameplayAbility fireball = _gas.CreateAbilityWithCooldown(8f);
            fireball.DisplayName = new LocalizedText(null, "Bola de Fogo");
            var texture = Track(new Texture2D(1, 1));
            fireball.Icon = Track(Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero));
            fireball.CooldownEffect.Duration = ScalableFloat.FromArray(8f, 6f, 4f);
            fireball.CostEffect = Effect(_mana, ScalableFloat.FromArray(-10f, -20f, -30f));
            fireball.CooldownReductionAttribute = _reduction;
            fireball.Charges.Enabled = true;
            fireball.Charges.MaxCharges = new ModifierValueSource
            {
                Type = ModifierValueSource.SourceType.Value,
                Value = ScalableFloat.Constant(2f)
            };
            fireball.Charges.RechargeTime = ScalableFloat.Constant(5f);
            fireball.Description = new AbilityDescription();
            fireball.Description.Elements.Add(new TextElement { Text = new LocalizedText(null, "Arremessa fogo") });
            fireball.Description.Elements.Add(new EffectModifiersElement
            {
                Effect = Effect(_health, ScalableFloat.FromArray(-20f, -30f, -40f))
            });
            using var serialized = new SerializedObject(fireball);
            serialized.FindProperty("maxLevel").intValue = 3;
            serialized.ApplyModifiedPropertiesWithoutUndo();
            return fireball;
        }

        private GameplayEffect Effect(AttributeDefinition attribute, ScalableFloat amount)
        {
            GameplayEffect effect = _gas.CreateEffect(EffectDurationPolicy.Instant, 0f);
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = attribute,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = amount }
            });
            return effect;
        }

        // Custo de mana lido da mana máxima do ASC: o atributo inteiro ou a fórmula com metade dele
        private GameplayEffect CostFromMaxMana(ModifierValueSource.SourceType type)
        {
            GameplayEffect effect = Effect(_mana, ScalableFloat.Constant(0f));
            ModifierValueSource value = effect.Modifiers[0].ValueSource;
            value.Type = type;
            if (type == ModifierValueSource.SourceType.Attribute)
            {
                value.Attribute = _maxMana;
                return effect;
            }

            var formula = Track(ScriptableObject.CreateInstance<SimpleAttributeFormula>());
            using (var serialized = new SerializedObject(formula))
            {
                SerializedProperty attributes = serialized.FindProperty("attributes");
                attributes.arraySize = 1;
                SerializedProperty entry = attributes.GetArrayElementAtIndex(0);
                entry.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Attribute)).objectReferenceValue = _maxMana;
                entry.FindPropertyRelative(nameof(SimpleAttributeFormula.AttributeComponent.Multiplier)).floatValue = 0.5f;
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            value.Formula = formula;
            return effect;
        }

        private AttributeDefinition Attribute(string name)
        {
            AttributeDefinition attribute = _gas.CreateAttribute(name);
            attribute.DisplayName = new LocalizedText(null, name);
            return attribute;
        }

        private AbilitySystemComponent Asc(float reduction)
        {
            AbilitySystemComponent asc = _gas.CreateAsc((_mana, 100f), (_health, 100f), (_reduction, reduction), (_maxMana, 200f));
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private static AttributeState State(AbilitySystemComponent asc, AttributeDefinition definition)
        {
            Assert.That(asc.AttributeSet.TryGetState(definition, out AttributeState state), Is.True);
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

        private static string Line(in TooltipModel model, TooltipLineKind kind) => Text(Find(model, kind).Value);

        private static string Label(in TooltipModel model, TooltipLineKind kind) => Find(model, kind).Label;

        private static TooltipLine Find(in TooltipModel model, TooltipLineKind kind)
        {
            Assert.That(Count(model, kind), Is.EqualTo(1), $"uma linha {kind}");
            foreach (TooltipLine line in model.Lines)
            {
                if (line.Kind == kind) return line;
            }

            return default;
        }

        private static int Count(in TooltipModel model, TooltipLineKind kind)
        {
            int count = 0;
            foreach (TooltipLine line in model.Lines)
            {
                if (line.Kind == kind) count++;
            }

            return count;
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

        /// <summary>Provedor de teste em en-US: troca o título, o subtítulo, a recarga e as cargas pela chave.</summary>
        public sealed class TestText : TextProviderAsset
        {
            private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

            public override IFormatProvider Culture => EnUs;

            public override string GetText(string key, string fallback)
            {
                switch (key)
                {
                    case "test.fireball": return "Fireball";
                    case "test.category": return "Fire";
                    case GasTextKeys.DescriptionCooldown: return "Cooldown";
                    case GasTextKeys.DescriptionCharges: return "{0} charges, one every {1}";
                    default: return fallback;
                }
            }
        }
    }
}
#endif
