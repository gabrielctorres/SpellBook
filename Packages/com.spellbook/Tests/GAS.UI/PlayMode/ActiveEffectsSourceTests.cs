#if UNITY_EDITOR
using System;
using System.Collections.Generic;
using System.Reflection;
using NUnit.Framework;
using SpellBook.Core;
using SpellBook.Core.Text;
using SpellBook.GAS.Core;
using SpellBook.GAS.Effects;
using SpellBook.Tags;
using SpellBook.Testing;
using SpellBook.Time;
using SpellBook.UI;
using UnityEditor;
using UnityEngine;
using Object = UnityEngine.Object;

namespace SpellBook.GAS.UI.Tests
{
    /// <summary>
    /// Fonte da lista de buffs e debuffs (Spec do B-018, BF1): um ícone por efeito na ordem em que apareceu, pelos eventos do
    /// ASC e pelo <see cref="EffectAggregate"/>; relógio pelo tick só com duração; papel, ícone e nome do asset; o estado
    /// vem só do ASC (CA1 a CA4, CA6, CA8, CA11, CA17 e CA18).
    /// </summary>
    public class ActiveEffectsSourceTests
    {
        private readonly List<Object> _created = new List<Object>();
        private GasFactory _gas;
        private TimeFactory _time;
        private ManualTimeSource _clock;
        private UIUpdateDriver _driver;

        [SetUp]
        public void SetUp()
        {
            _gas = new GasFactory();
            _time = new TimeFactory();
            _clock = _time.CreateManual();
            _driver = Track(new GameObject("tela")).AddComponent<UIUpdateDriver>();
            _driver.SetTimeSource(_clock);
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
        public void Apply_AddsIcon_Expire_RemovedAndReleased_RemoveByCodeTagStacks_OnlyWhenLast()
        {
            AbilitySystemComponent asc = Asc();
            GameplayTag poison = _gas.CreateTag("Status.Veneno");
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 5f);
            GameplayEffect venom = Effect(EffectDurationPolicy.HasDuration, 5f, poison);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);

            asc.ApplyEffect(burn);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { burn }), "aplicar acrescenta o ícone");
            asc.Tick(5f);
            Assert.That(list.Count, Is.Zero, "expirar remove");
            Assert.That(list.Removes, Is.EqualTo(1), "o id é devolvido uma vez");

            asc.RemoveEffect(asc.ApplyEffect(burn));
            Assert.That(list.Count, Is.Zero, "remover por código");

            asc.ApplyEffect(venom);
            asc.RemoveEffectsWithTag(poison);
            Assert.That(list.Count, Is.Zero, "remover por tag");

            ActiveEffectInstance stacked = asc.ApplyEffect(burn);
            asc.ApplyEffect(burn);
            asc.RemoveEffectStacks(stacked, 1);
            Assert.That(list.Count == 1 && list.Model(0).Stacks == 1, Is.True, "tirar uma pilha atualiza e não remove");
            int removes = list.Removes;
            asc.SetEffectStacks(stacked, 0);
            Assert.That(list.Count, Is.Zero, "pilhas zeradas removem");
            Assert.That(list.Removes - removes, Is.EqualTo(1), "pilhas zeradas e o OnEffectRemoved removem uma vez só");
        }

        [Test]
        public void Fraction_Seconds_ManualTime_PauseStops_InfiniteNoClock()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            Receiver list = Attach(new ActiveEffectsSource(), asc);

            ActiveEffectInstance timed = asc.ApplyEffect(burn);
            asc.ApplyEffect(aura);
            Assert.That(list.Model(0).Fraction, Is.EqualTo(1f).Within(0.001f));
            Assert.That(Text(list.Model(0).RemainingText), Is.EqualTo("10 s"));
            Assert.That(_driver.TickingCount, Is.EqualTo(1), "com duração: pede tick");

            asc.Tick(2.5f);
            _clock.Advance(2.5f);
            _driver.Tick();
            Assert.That(list.Model(0).Remaining, Is.EqualTo(7.5f).Within(0.001f), "o tick lê o tempo restante");
            Assert.That(list.Model(0).Fraction, Is.EqualTo(0.75f).Within(0.001f));
            Assert.That(Text(list.Model(0).RemainingText), Is.EqualTo("8 s"), "segundos inteiros para cima");

            _driver.Tick();
            Assert.That(list.Model(0).Remaining, Is.EqualTo(7.5f).Within(0.001f), "relógio parado: o ícone não anda");

            EffectIconModel infinite = list.Model(1);
            Assert.That(infinite.IsInfinite, Is.True);
            Assert.That(infinite.RemainingText.IsEmpty && infinite.Fraction == 0f, Is.True, "infinito: sem relógio");

            asc.RemoveEffect(timed);
            Assert.That(_driver.TickingCount, Is.Zero, "só infinito: solta o tick");
        }

        [Test]
        public void Stacks_ByEvent_HiddenAtOneByDefault()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);
            Receiver always = Attach(new ActiveEffectsSource { MinStacksToShow = 1 }, asc);

            ActiveEffectInstance instance = asc.ApplyEffect(burn);
            Assert.That(list.Model(0).Stacks == 1 && !list.Model(0).ShowStacks, Is.True, "1 pilha não aparece por padrão");
            Assert.That(always.Model(0).ShowStacks, Is.True, "o mínimo é configurável");

            asc.ApplyEffect(burn);
            Assert.That(list.Model(0).Stacks == 2 && list.Model(0).ShowStacks, Is.True, "empilhar atualiza por evento");
            asc.SetEffectStacks(instance, 4);
            Assert.That(list.Model(0).Stacks, Is.EqualTo(4));
            Assert.That(list.Adds, Is.EqualTo(1), "pilhas não criam ícone novo");
        }

        [Test]
        public void Role_FromBuffDebuff_NeutralDefault()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect buff = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect debuff = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect plain = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect both = Effect(EffectDurationPolicy.Infinite, 0f);
            Set(buff, "isBuff", true);
            Set(debuff, "isDebuff", true);
            Set(both, "isBuff", true);
            Set(both, "isDebuff", true);
            Receiver list = Attach(new ActiveEffectsSource(), asc);

            asc.ApplyEffect(buff);
            asc.ApplyEffect(debuff);
            asc.ApplyEffect(plain);
            asc.ApplyEffect(both);

            Assert.That(new[] { list.Model(0).Role, list.Model(1).Role, list.Model(2).Role, list.Model(3).Role },
                Is.EqualTo(new[] { TextRole.Beneficial, TextRole.Harmful, TextRole.Neutral, TextRole.Neutral }));
        }

        [Test]
        public void ApplicationOrder_NewAtEnd_PositionFixed()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect a = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect b = Effect(EffectDurationPolicy.HasDuration, 3f);
            GameplayEffect c = Effect(EffectDurationPolicy.Infinite, 0f);
            a.CanStack = true;
            a.MaxStacks = 5;
            Receiver list = Attach(new ActiveEffectsSource(), asc);
            asc.ApplyEffect(a);
            ActiveEffectInstance second = asc.ApplyEffect(b);
            asc.ApplyEffect(c);

            asc.ApplyEffect(a);
            asc.Tick(1f);
            _driver.Tick();
            asc.ApplyEffect(b);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { a, b, c }), "tempo, pilhas e renovação não reordenam");

            asc.RemoveEffect(second);
            asc.ApplyEffect(b);
            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { a, c, b }), "o efeito que volta entra no fim");
        }

        [Test]
        public void SameEffect_TwoSources_WithAndWithoutCanStack_OneIcon_SumStacks_MaxRemaining_InfiniteIfAny_DifferentAssetsNeverMerge()
        {
            AbilitySystemComponent target = Asc();
            AbilitySystemComponent first = Asc();
            AbilitySystemComponent second = Asc();
            GameplayEffect stacking = Effect(EffectDurationPolicy.HasDuration, 10f);
            stacking.CanStack = true;
            stacking.MaxStacks = 5;
            GameplayEffect single = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            GameplayEffect sameName = Effect(EffectDurationPolicy.HasDuration, 10f);
            Receiver list = Attach(new ActiveEffectsSource(), target);

            target.ApplyEffect(stacking, first);
            target.ApplyEffect(stacking, second);
            target.ApplyEffect(single, first);
            target.Tick(4f);
            _driver.Tick();
            Assert.That(list.Model(1).Remaining, Is.EqualTo(6f).Within(0.001f), "antes da renovação, a restante já caiu");
            target.ApplyEffect(single, second);
            target.ApplyEffect(aura, first);
            target.ApplyEffect(aura, second);
            target.ApplyEffect(sameName, first);
            _driver.Tick();

            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { stacking, single, aura, sameName }),
                "um ícone por asset; o mesmo nome em outro asset não se funde");
            Assert.That(list.Model(0).Stacks, Is.EqualTo(2), "com CanStack: a soma das pilhas das duas fontes");
            Assert.That(list.Model(1).Stacks, Is.EqualTo(1), "sem CanStack: conta 1");
            Assert.That(list.Model(1).Remaining, Is.EqualTo(10f).Within(0.001f), "a maior restante, renovada pela segunda fonte");
            Assert.That(list.Model(2).IsInfinite, Is.True, "infinito");
        }

        [Test]
        public void OpenWithActiveEffects_ShowsAll()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            burn.CanStack = true;
            burn.MaxStacks = 5;
            GameplayEffect hidden = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect aura = Effect(EffectDurationPolicy.Infinite, 0f);
            Set(hidden, "isHidden", true);
            for (int i = 0; i < 3; i++) asc.ApplyEffect(burn);
            asc.ApplyEffect(hidden);
            asc.ApplyEffect(aura);

            Receiver list = Attach(new ActiveEffectsSource(), asc);

            Assert.That(list.Subjects(), Is.EqualTo(new Object[] { burn, aura }), "todos os visíveis, na ordem de aplicação");
            Assert.That(list.Model(0).Stacks, Is.EqualTo(3));
            Assert.That(_driver.TickingCount, Is.EqualTo(1), "abrir com duração pede tick");
            asc.ApplyEffect(Effect(EffectDurationPolicy.Infinite, 0f, hidden: true));
            Assert.That(list.Count, Is.EqualTo(2), "IsHidden nunca aparece");
        }

        [Test]
        public void IconAndName_FromEffectFields()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect burn = Effect(EffectDurationPolicy.HasDuration, 10f);
            GameplayEffect bare = Effect(EffectDurationPolicy.HasDuration, 10f);
            Sprite icon = Track(Sprite.Create(Track(new Texture2D(1, 1)), new Rect(0f, 0f, 1f, 1f), Vector2.zero));
            using (var serialized = new SerializedObject(burn))
            {
                serialized.FindProperty("icon").objectReferenceValue = icon;
                serialized.FindProperty("localizedName.key").stringValue = "test.burn";
                serialized.FindProperty("localizedName.text").stringValue = "Queimadura";
                serialized.ApplyModifiedPropertiesWithoutUndo();
            }

            Receiver list = Attach(new ActiveEffectsSource(), asc, Track(ScriptableObject.CreateInstance<TestText>()));
            asc.ApplyEffect(burn);
            asc.ApplyEffect(bare);

            Assert.That(list.Model(0).Icon, Is.SameAs(icon));
            Assert.That(list.Model(0).Name, Is.EqualTo("Burn"), "o nome pelo provedor");
            Assert.That(list.Model(0).Subject, Is.SameAs(burn), "o asset é o assunto do item");
            Assert.That(list.Model(1).Icon, Is.Null, "sem ícone: a view usa o padrão");
            Assert.That(list.Model(1).Name, Is.Null.Or.Empty);
        }

        [Test]
        public void List_MatchesAscAfterRandomOps_NoOwnRegistry()
        {
            AbilitySystemComponent asc = Asc();
            GameplayEffect[] effects =
            {
                Effect(EffectDurationPolicy.HasDuration, 3f), Effect(EffectDurationPolicy.HasDuration, 6f),
                Effect(EffectDurationPolicy.Infinite, 0f), Effect(EffectDurationPolicy.HasDuration, 4f, hidden: true),
            };
            effects[0].CanStack = true;
            effects[0].MaxStacks = 4;
            var source = new ActiveEffectsSource();
            Receiver list = Attach(source, asc);
            var random = new System.Random(268);

            for (int step = 0; step < 300; step++)
            {
                IReadOnlyList<ActiveEffectInstance> active = asc.ActiveEffects;
                switch (random.Next(4))
                {
                    case 0: asc.ApplyEffect(effects[random.Next(effects.Length)]); break;
                    case 1: if (active.Count > 0) asc.RemoveEffect(active[random.Next(active.Count)]); break;
                    case 2: if (active.Count > 0) asc.SetEffectStacks(active[random.Next(active.Count)], random.Next(4)); break;
                    default: asc.Tick(1f); _driver.Tick(); break;
                }

                Assert.That(list.Describe(), Is.EqualTo(Expected(asc)), $"passo {step}");
            }

            UIListBinding<EffectIconModel> binding = list.Binding;
            foreach (FieldInfo field in binding.GetType().GetFields(BindingFlags.Instance | BindingFlags.NonPublic))
            {
                if (field.GetValue(binding) is List<ActiveEffectInstance> held)
                {
                    Assert.That(held, Is.Empty, $"{field.Name} não guarda instâncias entre eventos");
                }
            }

            binding.Detach();
            list.Clear();
            binding.Attach(asc, new UIBindContext(null));
            Assert.That(list.Describe(), Is.EqualTo(Expected(asc)), "religar relê o ASC");
        }

        [Test]
        public void Preview_BuffStacks_DebuffClock_Permanent()
        {
            var items = new List<EffectIconModel>();

            new ActiveEffectsSource().CreatePreview(new UIPreviewContext(1), items);

            Assert.That(items.Count, Is.EqualTo(3));
            Assert.That(items[0].Role == TextRole.Beneficial && items[0].Stacks == 3 && items[0].ShowStacks, Is.True,
                "buff com 3 pilhas");
            Assert.That(items[1].Role, Is.EqualTo(TextRole.Harmful));
            Assert.That(items[1].Fraction, Is.EqualTo(0.6f).Within(0.001f), "debuff em 60 %");
            Assert.That(Text(items[1].RemainingText), Is.EqualTo("3 s"));
            Assert.That(items[2].IsInfinite, Is.True, "permanente");
        }

        // Uma linha por efeito visível de ActiveEffects (uma instância por asset), com as pilhas somadas
        private static string[] Expected(AbilitySystemComponent asc)
        {
            var lines = new List<string>();
            var instances = new List<ActiveEffectInstance>();
            foreach (ActiveEffectInstance instance in asc.ActiveEffects)
            {
                if (instance.IsHidden) continue;

                EffectAggregate aggregate = EffectAggregate.Of(asc, instance.Effect, instances);
                lines.Add(Line(instance.Effect, aggregate.Stacks, aggregate.IsInfinite));
            }

            return lines.ToArray();
        }

        private static string Line(Object effect, int stacks, bool infinite) => $"{effect.GetInstanceID()}|{stacks}|{infinite}";

        private Receiver Attach(ActiveEffectsSource source, AbilitySystemComponent asc, ITextProvider text = null)
        {
            var receiver = new Receiver { Binding = source.CreateBinding() };
            receiver.Binding.SetReceiver(receiver);
            _driver.Register(receiver.Binding);
            receiver.Binding.Attach(asc, new UIBindContext(text));
            return receiver;
        }

        private GameplayEffect Effect(EffectDurationPolicy policy, float duration, GameplayTag tag = default, bool hidden = false)
        {
            GameplayEffect effect = tag.IsValid ? _gas.CreateEffect(policy, duration, tag) : _gas.CreateEffect(policy, duration);
            if (hidden) Set(effect, "isHidden", true);
            return effect;
        }

        private static void Set(GameplayEffect effect, string field, bool value)
        {
            using var serialized = new SerializedObject(effect);
            serialized.FindProperty(field).boolValue = value;
            serialized.ApplyModifiedPropertiesWithoutUndo();
        }

        private AbilitySystemComponent Asc()
        {
            AbilitySystemComponent asc = _gas.CreateAsc();
            GasFactory.SetAutoTick(asc, false);
            asc.SetTimeSource(_clock);
            return asc;
        }

        private static string Text(TextSpan span) => span.IsEmpty ? string.Empty : span.Buffer.ToString(span);

        private T Track<T>(T created) where T : Object
        {
            _created.Add(created);
            return created;
        }

        /// <summary>Receptor de teste: os itens na ordem da lista, e quantos foram acrescentados e removidos.</summary>
        private sealed class Receiver : IUIListReceiver<EffectIconModel>
        {
            private readonly List<int> _ids = new List<int>();
            private readonly List<EffectIconModel> _models = new List<EffectIconModel>();

            public UIListBinding<EffectIconModel> Binding;
            public int Adds;
            public int Removes;

            public int Count => _ids.Count;

            public EffectIconModel Model(int index) => _models[index];

            public void AddItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0)
                {
                    _models[index] = item;
                    return;
                }

                Adds++;
                _ids.Add(id);
                _models.Add(item);
            }

            public void UpdateItem(int id, in EffectIconModel item)
            {
                int index = _ids.IndexOf(id);
                if (index >= 0) _models[index] = item;
            }

            public void RemoveItem(int id)
            {
                int index = _ids.IndexOf(id);
                if (index < 0) return;

                Removes++;
                _ids.RemoveAt(index);
                _models.RemoveAt(index);
            }

            public void ClearItems() => Clear();

            public void Clear()
            {
                _ids.Clear();
                _models.Clear();
            }

            public Object[] Subjects()
            {
                var subjects = new Object[_models.Count];
                for (int i = 0; i < subjects.Length; i++) subjects[i] = _models[i].Subject;
                return subjects;
            }

            public string[] Describe()
            {
                var lines = new string[_models.Count];
                for (int i = 0; i < lines.Length; i++) lines[i] = Line(_models[i].Subject, _models[i].Stacks, _models[i].IsInfinite);
                return lines;
            }
        }

        /// <summary>Provedor de teste: troca o nome do efeito pela chave.</summary>
        public sealed class TestText : TextProviderAsset
        {
            public override IFormatProvider Culture => System.Globalization.CultureInfo.InvariantCulture;

            public override string GetText(string key, string fallback) => key == "test.burn" ? "Burn" : fallback;
        }
    }
}
#endif
