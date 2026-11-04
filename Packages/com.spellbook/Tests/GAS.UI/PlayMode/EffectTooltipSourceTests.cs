#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Globalization;
using NUnit.Framework;
using SpellBook.Core.Text;
using SpellBook.GAS.Attributes;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
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
    /// Fonte do tooltip de efeito e agregado das instâncias (Spec UI-3, TT5a): título, ícone, modificadores, pilhas e tempo
    /// restante com tick só aberto (CA4), eventos filtrados pelo efeito, sem dono e prévia, aberto sem alocar (CA13, parte
    /// do efeito) e a regra do agregado (soma das pilhas, maior restante, infinito se alguma é).
    /// </summary>
    public class EffectTooltipSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private AttributeDefinition _strength;
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
            _strength.DisplayName = new LocalizedText(null, "Força");

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
        public void Effect_TitleIconModifiers_Stacks_Remaining_TickOnlyWhileOpen()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);
            for (int i = 0; i < 3; i++) asc.ApplyEffect(burn);
            var requester = new object();

            _binder.Show(requester, new EffectTooltipSource { Effect = burn }, asc, null, null, 0f);
            TooltipModel model = _view.Last;

            Assert.That(model.IsEmpty, Is.False, "o tooltip deveria estar aberto");
            Assert.That(model.Title, Is.EqualTo("Burn"), "título pelo provedor");
            Assert.That(model.Icon, Is.SameAs(burn.Icon));
            Assert.That(Lines(model), Is.EqualTo(new[] { "Modifier|Força|+6" }), "o modificador com as 3 pilhas");
            Assert.That(model.Lines[0].Subject, Is.SameAs(_strength));
            Assert.That(model.HasStacks && model.Stacks == 3, Is.True, "3 pilhas");
            Assert.That(model.HasRemaining, Is.True);
            Assert.That(Text(model.RemainingText), Is.EqualTo("10 s"));
            Assert.That(_driver.TickingCount, Is.EqualTo(1), "aberto com duração: pede tick");

            asc.Tick(2.5f);
            _driver.Tick();
            Assert.That(Text(_view.Last.RemainingText), Is.EqualTo("8 s"), "o tempo corre pelo tick");
            Assert.That(_view.Last.Remaining, Is.EqualTo(7.5f).Within(0.001f));

            _binder.Hide(requester);
            int renders = _view.Renders;
            asc.Tick(1f);
            asc.ApplyEffect(burn);
            _driver.Tick();
            Assert.That(_driver.TickingCount, Is.Zero, "fechado: sem tick");
            Assert.That(_view.Renders, Is.EqualTo(renders), "fechado: nada redesenha");
        }

        [Test]
        public void EffectEvents_FilteredByEffect_AppliedStacksRemoved()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);
            GameplayEffect other = Burn(EffectDurationPolicy.HasDuration);
            Open(new EffectTooltipSource { Effect = burn }, asc);
            Assert.That(Lines(_view.Last), Is.EqualTo(new[] { "Modifier|Força|+2" }), "sem instância: o asset, 1 pilha");
            Assert.That(_view.Last.HasStacks || _view.Last.HasRemaining, Is.False, "sem instância: sem pilhas nem tempo");
            Assert.That(_driver.TickingCount, Is.Zero, "sem instância: sem tick");
            int renders = _view.Renders;

            asc.ApplyEffect(other);
            Assert.That(_view.Renders, Is.EqualTo(renders), "outro efeito não redesenha");

            ActiveEffectInstance instance = asc.ApplyEffect(burn);
            Assert.That(Text(_view.Last.RemainingText), Is.EqualTo("10 s"), "aplicar atualiza");
            Assert.That(_view.Last.HasStacks, Is.False, "1 pilha não aparece");
            Assert.That(_driver.TickingCount, Is.EqualTo(1));

            asc.ApplyEffect(burn);
            Assert.That(_view.Last.Stacks, Is.EqualTo(2), "empilhar atualiza");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[] { "Modifier|Força|+4" }));

            asc.RemoveEffect(instance);
            Assert.That(_view.Last.HasStacks || _view.Last.HasRemaining, Is.False, "remover volta ao asset");
            Assert.That(_driver.TickingCount, Is.Zero, "sem instância: solta o tick");
        }

        [Test]
        public void ClosedThenReopenedOnOtherAsc_OldAscIgnored_OneRenderPerChange()
        {
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);
            AbilitySystemComponent first = Asc();
            AbilitySystemComponent second = Asc();
            ActiveEffectInstance old = first.ApplyEffect(burn);
            var source = new EffectTooltipSource { Effect = burn };
            var requester = new object();
            _binder.Show(requester, source, first, null, null, 0f);
            _binder.Hide(requester);
            Open(source, second);
            int renders = _view.Renders;

            first.ApplyEffect(burn);
            first.SetEffectStacks(old, 3);
            first.RemoveEffect(old);
            int afterFirst = _view.Renders;
            second.ApplyEffect(burn);

            Assert.That(afterFirst, Is.EqualTo(renders), "o Detach desassina o ASC anterior: aplicar, pilhas e remover");
            Assert.That(_view.Renders - renders, Is.EqualTo(1), "uma assinatura por evento depois de reabrir");
            Assert.That(Text(_view.Last.RemainingText), Is.EqualTo("10 s"));
        }

        [Test]
        public void InfiniteEffect_NoRemaining_NoTick()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect aura = Burn(EffectDurationPolicy.Infinite);
            asc.ApplyEffect(aura);

            Open(new EffectTooltipSource { Effect = aura }, asc);

            Assert.That(_view.Last.HasRemaining, Is.False, "infinito: sem tempo restante");
            Assert.That(_driver.TickingCount, Is.Zero, "infinito: sem tick");
        }

        [Test]
        public void NoOwnerOrPreview_AssetOnly_DestroyedEffect_Empty()
        {
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);

            Assert.That(() => _binder.Show(new object(), new EffectTooltipSource(), null, burn, null, 0f), Throws.Nothing);
            Assert.That(_view.Last.Title, Is.EqualTo("Burn"), "sem dono: o efeito do assunto");
            Assert.That(Lines(_view.Last), Is.EqualTo(new[] { "Modifier|Força|+2" }));
            Assert.That(_view.Last.HasRemaining, Is.False);

            TooltipModel preview = new EffectTooltipSource { Effect = burn }.CreatePreview(new UIPreviewContext(1));
            Assert.That(preview.Title, Is.EqualTo("Queimadura"), "prévia sem provedor: o texto padrão");
            Assert.That(preview.Lines.Count, Is.EqualTo(1));

            GameplayEffect gone = Burn(EffectDurationPolicy.HasDuration);
            Object.DestroyImmediate(gone);
            Assert.That(() => _binder.Show(new object(), new EffectTooltipSource { Effect = gone }, Asc(), null, null, 0f),
                Throws.Nothing);
            Assert.That(_view.Last.IsEmpty, Is.True, "efeito destruído: modelo vazio");
        }

        [Test]
        public void Updates10000_NoAlloc()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);
            burn.Duration = ScalableFloat.Constant(100000f);
            ActiveEffectInstance instance = asc.ApplyEffect(burn);
            Open(new EffectTooltipSource { Effect = burn }, asc);
            asc.SetEffectStacks(instance, 2);
            asc.Tick(1f);
            _driver.Tick();
            int rendersBefore = _view.Renders;

            Assert.That(() =>
            {
                for (int i = 0; i < 10000; i++)
                {
                    asc.SetEffectStacks(instance, 1 + i % 5);
                    asc.Tick(1f);
                    _driver.Tick();
                }
            }, Is.Not.AllocatingGCMemory());
            Assert.That(_view.Renders - rendersBefore, Is.GreaterThanOrEqualTo(10000), "cada mudança de pilha refaz o tooltip");
            Assert.That(_view.Last.Stacks, Is.EqualTo(5));
        }

        [Test]
        public void Aggregate_SumStacks_MaxRemaining_InfiniteIfAny()
        {
            GameplayEffect burn = Burn(EffectDurationPolicy.HasDuration);
            burn.MaxStacks = 3;
            AbilitySystemComponent first = Asc();
            AbilitySystemComponent second = Asc();
            for (int i = 0; i < 3; i++) first.ApplyEffect(burn);
            first.Tick(4f);
            ActiveEffectInstance stacked = first.ActiveEffects[0];
            var instances = new List<ActiveEffectInstance>();

            EffectAggregate one = EffectAggregate.Of(first, burn, instances);
            Assert.That(one.Count == 1 && one.Stacks == 3 && one.First == stacked, Is.True, "uma instância de 3 pilhas");
            Assert.That(one.Remaining, Is.EqualTo(6f).Within(0.001f));

            ActiveEffectInstance single = second.ApplyEffect(burn, 9f);
            var both = new List<ActiveEffectInstance> { stacked, single };
            EffectAggregate sum = EffectAggregate.Of(both);

            Assert.That(sum.Count, Is.EqualTo(2));
            Assert.That(sum.Stacks, Is.EqualTo(4), "a soma das pilhas");
            Assert.That(sum.Remaining, Is.EqualTo(9f).Within(0.001f), "a maior restante");
            Assert.That(sum.Duration, Is.EqualTo(9f).Within(0.001f), "a duração aplicada da maior restante");
            Assert.That(sum.IsInfinite, Is.False);
            Assert.That(sum.First, Is.SameAs(stacked));

            burn.CanStack = false;
            Assert.That(EffectAggregate.Of(both).Stacks, Is.EqualTo(2), "sem CanStack, cada instância conta 1");

            GameplayEffect aura = Burn(EffectDurationPolicy.Infinite);
            ActiveEffectInstance infinite = second.ApplyEffect(aura);
            Assert.That(EffectAggregate.Of(new List<ActiveEffectInstance> { single, infinite }).IsInfinite, Is.True,
                "infinito se alguma é");
            Assert.That(EffectAggregate.Of(Asc(), burn, instances).Count, Is.Zero, "ASC sem o efeito");
            Assert.That(instances, Is.Empty);
        }

        private void Open(EffectTooltipSource source, AbilitySystemComponent asc)
        {
            _binder.Show(new object(), source, asc, null, null, 0f);
            Assert.That(_view.Last.IsEmpty, Is.False, "o tooltip deveria estar aberto");
        }

        // Queimadura: +2 de força por pilha, 10 s, até 5 pilhas, com ícone e nome por chave
        private GameplayEffect Burn(EffectDurationPolicy policy)
        {
            GameplayEffect effect = _gas.CreateEffect(policy, 10f);
            effect.CanStack = true;
            effect.MaxStacks = 5;
            effect.Modifiers.Add(new AttributeEffectModifier
            {
                Attribute = _strength,
                Operation = ModifierOperation.Add,
                ValueSource = new ModifierValueSource { Type = ModifierValueSource.SourceType.Value, Value = ScalableFloat.Constant(2f) }
            });
            var texture = Track(new Texture2D(1, 1));
            Sprite icon = Track(Sprite.Create(texture, new Rect(0f, 0f, 1f, 1f), Vector2.zero));
            using var serialized = new SerializedObject(effect);
            serialized.FindProperty("icon").objectReferenceValue = icon;
            serialized.FindProperty("localizedName.key").stringValue = "test.burn";
            serialized.FindProperty("localizedName.text").stringValue = "Queimadura";
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

        /// <summary>Provedor de teste em en-US: troca o nome do efeito pela chave.</summary>
        public sealed class TestText : TextProviderAsset
        {
            private static readonly CultureInfo EnUs = CultureInfo.GetCultureInfo("en-US");

            public override IFormatProvider Culture => EnUs;

            public override string GetText(string key, string fallback) => key == "test.burn" ? "Burn" : fallback;
        }
    }
}
#endif
